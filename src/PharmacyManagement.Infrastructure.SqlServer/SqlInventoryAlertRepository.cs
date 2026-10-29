using System;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using PharmacyManagement.Application;

namespace PharmacyManagement.Infrastructure.SqlServer
{
    public sealed class SqlInventoryAlertRepository : IInventoryAlertRepository, IAsyncInventoryAlertRepository
    {
        private readonly Func<SqlConnection> connectionFactory;
        public SqlInventoryAlertRepository(Func<SqlConnection> connectionFactory)
        { this.connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory)); }

        public OperationalReportResult Load(InventoryAlertRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            using (SqlConnection connection = connectionFactory())
            using (SqlCommand command = connection.CreateCommand())
            using (var table = new DataTable())
            {
                command.CommandTimeout = 10;
                if (connection.State != ConnectionState.Open) connection.Open();
                command.CommandText = BuildInventoryAlertsQuery(IsFullCommercialModeRequested(connection));
                command.Parameters.Add("@AffectedMedicines", SqlDbType.Xml).Value = request.AffectedMedicines == null
                    ? (object)DBNull.Value
                    : new XElement("medicines", request.AffectedMedicines
                        .Select(name => new XElement("name", name))).ToString(SaveOptions.DisableFormatting);
                command.Parameters.Add("@LowStockThreshold", SqlDbType.Int).Value = request.LowStockThreshold;
                command.Parameters.Add("@ExpiringWithinDays", SqlDbType.Int).Value = request.ExpiringWithinDays;
                using (var adapter = new SqlDataAdapter(command)) adapter.Fill(table);
                return new OperationalReportResult(
                    table.Columns.Cast<DataColumn>().Select(column =>
                        new OperationalReportColumn(column.ColumnName, column.DataType)),
                    table.Rows.Cast<DataRow>().Select(row =>
                        row.ItemArray.Select(value => value == DBNull.Value ? null : value).ToArray()));
            }
        }

        public async Task<OperationalReportResult> LoadAsync(InventoryAlertRequest request, CancellationToken cancellation)
        {
            try { return await LoadCoreAsync(request, cancellation).ConfigureAwait(false); }
            catch (SqlException error) when (cancellation.IsCancellationRequested)
            { throw new OperationCanceledException("Inventory alert reading was cancelled.", error, cancellation); }
        }

        private async Task<OperationalReportResult> LoadCoreAsync(InventoryAlertRequest request, CancellationToken cancellation)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            cancellation.ThrowIfCancellationRequested();
            using (SqlConnection connection = connectionFactory())
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandTimeout = 30;
                await connection.OpenAsync(cancellation).ConfigureAwait(false);
                using (SqlCommand mode = new SqlCommand(@"IF OBJECT_ID('dbo.CommercialOperationalSettingsTb', 'U') IS NULL SELECT CAST(0 AS BIT)
ELSE SELECT CAST(CASE WHEN EXISTS (SELECT 1 FROM dbo.CommercialOperationalSettingsTb WITH (UPDLOCK, HOLDLOCK) WHERE SettingKey = N'Mode' AND SettingValue = N'FullCommercial') THEN 1 ELSE 0 END AS BIT);", connection))
                {
                    command.CommandText = BuildInventoryAlertsQuery(Convert.ToBoolean(await mode.ExecuteScalarAsync(cancellation).ConfigureAwait(false)));
                }
                command.Parameters.Add("@AffectedMedicines", SqlDbType.Xml).Value = request.AffectedMedicines == null
                    ? (object)DBNull.Value
                    : new XElement("medicines", request.AffectedMedicines.Select(name => new XElement("name", name))).ToString(SaveOptions.DisableFormatting);
                command.Parameters.Add("@LowStockThreshold", SqlDbType.Int).Value = request.LowStockThreshold;
                command.Parameters.Add("@ExpiringWithinDays", SqlDbType.Int).Value = request.ExpiringWithinDays;
                using (var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellation).ConfigureAwait(false))
                {
                    var columns = new List<OperationalReportColumn>();
                    for (int index = 0; index < reader.FieldCount; index++) columns.Add(new OperationalReportColumn(reader.GetName(index), reader.GetFieldType(index)));
                    var rows = new List<object[]>();
                    while (await reader.ReadAsync(cancellation).ConfigureAwait(false))
                    {
                        var values = new object[reader.FieldCount]; reader.GetValues(values);
                        for (int index = 0; index < values.Length; index++) if (values[index] == DBNull.Value) values[index] = null;
                        rows.Add(values);
                    }
                    return new OperationalReportResult(columns, rows);
                }
            }
        }

        private static bool IsFullCommercialModeRequested(SqlConnection connection)
        {
            const string sql = @"
IF OBJECT_ID('dbo.CommercialOperationalSettingsTb', 'U') IS NULL SELECT CAST(0 AS BIT)
ELSE SELECT CAST(CASE WHEN EXISTS (SELECT 1 FROM dbo.CommercialOperationalSettingsTb WITH (UPDLOCK, HOLDLOCK) WHERE SettingKey = N'Mode' AND SettingValue = N'FullCommercial') THEN 1 ELSE 0 END AS BIT);";
            using (SqlCommand command = new SqlCommand(sql, connection))
                return Convert.ToBoolean(command.ExecuteScalar());
        }

        private static string BuildInventoryAlertsQuery(bool lotMode)
        {
            // Select the expiry source independently from aggregate reorder quantities.
            string expirySource = lotMode ? @"
    SELECT m.MedName, l.QuantityRemaining AS MedQty, l.ExpiryDate AS ExpDate, m.CompName,
           N' التشغيلة: ' + l.BatchNumber AS BatchDetails
    FROM AlertMedicines m
    INNER JOIN dbo.InventoryLotsTb l ON l.MedName = m.MedName
    WHERE l.QuantityRemaining > 0" : @"
    SELECT MedName, MedQty, ExpDate, CompName, N'' AS BatchDetails
    FROM AlertMedicines";
            return @"
WITH AlertMedicines AS
(
    SELECT MedName, MedQty, ExpDate, CompName
    FROM dbo.MedicineTb1
    WHERE @AffectedMedicines IS NULL OR MedName IN
    (
        SELECT n.value('.', 'nvarchar(4000)')
        FROM @AffectedMedicines.nodes('/medicines/name') AS selected(n)
    )
),
ExpiryStock AS
(" + expirySource + @"
)
SELECT N'منتهي' AS [نوع التنبيه],
       MedName AS [الصنف],
       MedQty AS [الكمية],
       ExpDate AS [تاريخ الانتهاء],
       CompName AS [الشركة],
       N'الصنف منتهي الصلاحية ولا يجب صرفه.' + BatchDetails AS [التفاصيل]
FROM ExpiryStock
WHERE MedQty > 0 AND ExpDate < CAST(GETDATE() AS DATE)

UNION ALL

SELECT N'قريب الانتهاء' AS [نوع التنبيه],
       MedName AS [الصنف],
       MedQty AS [الكمية],
       ExpDate AS [تاريخ الانتهاء],
       CompName AS [الشركة],
       N'الصنف يقترب من تاريخ الانتهاء ضمن فترة التنبيه.' + BatchDetails AS [التفاصيل]
FROM ExpiryStock
WHERE MedQty > 0 AND ExpDate >= CAST(GETDATE() AS DATE)
  AND ExpDate <= DATEADD(DAY, @ExpiringWithinDays, CAST(GETDATE() AS DATE))

UNION ALL

SELECT N'مخزون منخفض' AS [نوع التنبيه],
       MedName AS [الصنف],
       MedQty AS [الكمية],
       ExpDate AS [تاريخ الانتهاء],
       CompName AS [الشركة],
       N'الكمية الحالية عند حد إعادة الطلب أو أقل منه.' AS [التفاصيل]
FROM AlertMedicines
WHERE MedQty <= @LowStockThreshold

ORDER BY [نوع التنبيه], [تاريخ الانتهاء], [الصنف];";
        }
    }
}

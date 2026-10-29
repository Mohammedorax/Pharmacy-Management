using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Application;

namespace PharmacyManagement.Infrastructure.SqlServer
{
    public sealed class SqlInventoryOperationalReportRepository : IInventoryOperationalReportRepository
    {
        private readonly Func<SqlConnection> connectionFactory;
        public SqlInventoryOperationalReportRepository(Func<SqlConnection> connectionFactory)
        { this.connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory)); }

        public Task<OperationalReportResult> LoadMovementSummaryAsync(InventoryMovementSummaryRequest request, CancellationToken cancellation)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            const string sql = @"
SELECT m.MedName AS [الصنف],
       m.CompName AS [الشركة],
       m.MedQty AS [الرصيد الحالي],
       ISNULL(SUM(CASE WHEN mv.QuantityDelta > 0 THEN mv.QuantityDelta ELSE 0 END), 0) AS [إجمالي الوارد],
       ISNULL(SUM(CASE WHEN mv.QuantityDelta < 0 THEN ABS(mv.QuantityDelta) ELSE 0 END), 0) AS [إجمالي الصادر],
       ISNULL(SUM(mv.QuantityDelta), 0) AS [صافي الحركة],
       COUNT(mv.MovementId) AS [عدد الحركات],
       MAX(mv.CreatedAt) AS [آخر حركة]
FROM dbo.MedicineTb1 m
LEFT JOIN dbo.InventoryMovementsTb mv
    ON mv.MedName = m.MedName
   AND mv.CreatedAt >= @FromDate
   AND mv.CreatedAt < DATEADD(DAY, 1, @ToDate)
WHERE (@Search = N'' OR m.MedName LIKE N'%' + @Search + N'%' OR m.CompName LIKE N'%' + @Search + N'%' OR ISNULL(m.Barcode, N'') LIKE N'%' + @Search + N'%')
GROUP BY m.MedName, m.CompName, m.MedQty
ORDER BY m.MedName;";
            return ExecuteAsync(sql, cancellation, command =>
            {
                command.Parameters.Add("@Search", SqlDbType.NVarChar, 200).Value = request.SearchText;
                AddDateParameters(command, request.FromDate, request.ToDate);
            });
        }

        public Task<OperationalReportResult> LoadTurnoverAsync(InventoryTurnoverRequest request, CancellationToken cancellation)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            const string sql = @"
WITH MovementWindow AS
(
    SELECT MedName,
           SUM(CASE WHEN QuantityDelta > 0 THEN QuantityDelta ELSE 0 END) AS ReceivedQty,
           SUM(CASE WHEN QuantityDelta < 0 THEN ABS(QuantityDelta) ELSE 0 END) AS IssuedQty,
           MAX(CASE WHEN QuantityDelta < 0 THEN CreatedAt ELSE NULL END) AS LastIssueAt,
           MAX(CreatedAt) AS LastMovementAt
    FROM dbo.InventoryMovementsTb
    WHERE CreatedAt >= @FromDate
      AND CreatedAt < DATEADD(DAY, 1, @ToDate)
    GROUP BY MedName
)
SELECT m.MedName AS [الصنف],
       m.CompName AS [الشركة],
       m.MedQty AS [الرصيد الحالي],
       ISNULL(w.ReceivedQty, 0) AS [الوارد خلال الفترة],
       ISNULL(w.IssuedQty, 0) AS [الصادر خلال الفترة],
       ISNULL(w.ReceivedQty, 0) - ISNULL(w.IssuedQty, 0) AS [صافي الحركة],
       CAST(CASE WHEN (m.MedQty + ISNULL(w.IssuedQty, 0)) <= 0 THEN 0
                 ELSE ISNULL(w.IssuedQty, 0) * 1.0 / NULLIF((m.MedQty + ISNULL(w.IssuedQty, 0)) / 2.0, 0)
            END AS DECIMAL(18, 2)) AS [معدل الدوران التقريبي],
       w.LastIssueAt AS [آخر صرف],
       w.LastMovementAt AS [آخر حركة],
       CASE WHEN m.MedQty > 0 AND (w.LastIssueAt IS NULL OR w.LastIssueAt < DATEADD(DAY, -@IdleDays, GETDATE())) THEN N'راكد'
            WHEN m.MedQty <= 10 THEN N'منخفض'
            WHEN ISNULL(w.IssuedQty, 0) > 0 THEN N'نشط'
            ELSE N'بدون حركة' END AS [التصنيف]
FROM dbo.MedicineTb1 m
LEFT JOIN MovementWindow w ON w.MedName = m.MedName
ORDER BY [التصنيف], m.MedName;";
            return ExecuteAsync(sql, cancellation, command =>
            {
                command.Parameters.Add("@IdleDays", SqlDbType.Int).Value = request.IdleDays;
                AddDateParameters(command, request.FromDate, request.ToDate);
            });
        }

        private async Task<OperationalReportResult> ExecuteAsync(string sql, CancellationToken cancellation, Action<SqlCommand> configure)
        {
            cancellation.ThrowIfCancellationRequested();
            using (SqlConnection connection = connectionFactory())
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandTimeout = 30;
                command.CommandText = sql;
                configure(command);
                await connection.OpenAsync(cancellation).ConfigureAwait(false);
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

        private static void AddDateParameters(SqlCommand command, DateTime fromDate, DateTime toDate)
        {
            command.Parameters.Add("@FromDate", SqlDbType.DateTime).Value = fromDate.Date;
            command.Parameters.Add("@ToDate", SqlDbType.DateTime).Value = toDate.Date;
        }
    }
}

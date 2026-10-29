using System;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Application;

namespace PharmacyManagement.Infrastructure.SqlServer
{
    public sealed class SqlInventoryIntegrityReadRepository : IInventoryIntegrityReadRepository, IAsyncInventoryIntegrityReadRepository
    {
        private readonly Func<SqlConnection> connectionFactory;
        public SqlInventoryIntegrityReadRepository(Func<SqlConnection> connectionFactory)
        { this.connectionFactory=connectionFactory??throw new ArgumentNullException(nameof(connectionFactory)); }

        public OperationalReportResult Load()
        {
            using(SqlConnection connection=connectionFactory()) return Load(connection,null);
        }

        public async Task<OperationalReportResult> LoadAsync(CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            using (SqlConnection connection = connectionFactory())
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandText = @"
WITH OrderedMovements AS
(
    SELECT m.MedName, m.CompName, m.MedQty AS CurrentQuantity, mv.MovementId, mv.CreatedAt,
           mv.SourceType, mv.SourceId, mv.QuantityBefore, mv.QuantityAfter,
           LAG(mv.QuantityAfter) OVER (PARTITION BY mv.MedName ORDER BY mv.MovementId) AS PreviousQuantityAfter,
           ROW_NUMBER() OVER (PARTITION BY mv.MedName ORDER BY mv.MovementId DESC) AS LatestMovementRank
    FROM dbo.MedicineTb1 m
    INNER JOIN dbo.InventoryMovementsTb mv ON mv.MedName = m.MedName
)
SELECT MedName AS [الصنف], CompName AS [الشركة], MovementId AS [رقم الحركة], CreatedAt AS [تاريخ الحركة],
       SourceType AS [المصدر], ISNULL(SourceId, N'') AS [رقم المرجع], QuantityBefore AS [الرصيد قبل],
       QuantityAfter AS [الرصيد بعد], CurrentQuantity AS [الرصيد الحالي],
       CASE WHEN LatestMovementRank = 1 AND QuantityAfter <> CurrentQuantity THEN N'الرصيد الحالي لا يطابق آخر حركة'
            WHEN PreviousQuantityAfter IS NOT NULL AND PreviousQuantityAfter <> QuantityBefore THEN N'تسلسل الحركات غير متصل'
            ELSE N'' END AS [الحالة]
FROM OrderedMovements
WHERE (LatestMovementRank = 1 AND QuantityAfter <> CurrentQuantity)
   OR (PreviousQuantityAfter IS NOT NULL AND PreviousQuantityAfter <> QuantityBefore)
ORDER BY [رقم الحركة] DESC;";
                await connection.OpenAsync(cancellation).ConfigureAwait(false);
                using (SqlDataReader reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellation).ConfigureAwait(false))
                {
                    var columns = new System.Collections.Generic.List<OperationalReportColumn>();
                    for (int i = 0; i < reader.FieldCount; i++)
                        columns.Add(new OperationalReportColumn(reader.GetName(i), reader.GetFieldType(i)));
                    var rows = new System.Collections.Generic.List<object[]>();
                    while (await reader.ReadAsync(cancellation).ConfigureAwait(false))
                    {
                        var values = new object[reader.FieldCount];
                        for (int i = 0; i < values.Length; i++) values[i] = await reader.IsDBNullAsync(i, cancellation).ConfigureAwait(false) ? null : reader.GetValue(i);
                        rows.Add(values);
                    }
                    return new OperationalReportResult(columns, rows);
                }
            }
        }

        // Caller-owned connections and transactions are retained for existing QA snapshots.
        public static OperationalReportResult Load(SqlConnection connection, SqlTransaction transaction)
        {
            if(connection==null) throw new ArgumentNullException(nameof(connection));
            using(SqlCommand command=connection.CreateCommand())
            using(var table=new DataTable())
            {
                command.Transaction=transaction;
                command.CommandText=@"
WITH OrderedMovements AS
(
    -- MovementId follows the locked stock write order; CreatedAt may be backdated or use a different time zone.
    SELECT
        m.MedName,
        m.CompName,
        m.MedQty AS CurrentQuantity,
        mv.MovementId,
        mv.CreatedAt,
        mv.SourceType,
        mv.SourceId,
        mv.QuantityBefore,
        mv.QuantityAfter,
        LAG(mv.QuantityAfter) OVER (PARTITION BY mv.MedName ORDER BY mv.MovementId) AS PreviousQuantityAfter,
        ROW_NUMBER() OVER (PARTITION BY mv.MedName ORDER BY mv.MovementId DESC) AS LatestMovementRank
    FROM dbo.MedicineTb1 m
    INNER JOIN dbo.InventoryMovementsTb mv ON mv.MedName = m.MedName
)
SELECT
    MedName AS [الصنف],
    CompName AS [الشركة],
    MovementId AS [رقم الحركة],
    CreatedAt AS [تاريخ الحركة],
    SourceType AS [المصدر],
    ISNULL(SourceId, N'') AS [رقم المرجع],
    QuantityBefore AS [الرصيد قبل],
    QuantityAfter AS [الرصيد بعد],
    CurrentQuantity AS [الرصيد الحالي],
    CASE
        WHEN LatestMovementRank = 1 AND QuantityAfter <> CurrentQuantity THEN N'الرصيد الحالي لا يطابق آخر حركة'
        WHEN PreviousQuantityAfter IS NOT NULL AND PreviousQuantityAfter <> QuantityBefore THEN N'تسلسل الحركات غير متصل'
        ELSE N''
    END AS [الحالة]
FROM OrderedMovements
WHERE (LatestMovementRank = 1 AND QuantityAfter <> CurrentQuantity)
   OR (PreviousQuantityAfter IS NOT NULL AND PreviousQuantityAfter <> QuantityBefore)
ORDER BY [رقم الحركة] DESC;";
                using(var adapter=new SqlDataAdapter(command)) adapter.Fill(table);
                return new OperationalReportResult(table.Columns.Cast<DataColumn>().Select(column=>
                    new OperationalReportColumn(column.ColumnName,column.DataType)),
                    table.Rows.Cast<DataRow>().Select(row=>row.ItemArray.Select(value=>value==DBNull.Value?null:value).ToArray()));
            }
        }
    }
}

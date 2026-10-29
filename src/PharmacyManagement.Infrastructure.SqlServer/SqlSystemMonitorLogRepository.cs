using System;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Application;

namespace PharmacyManagement.Infrastructure.SqlServer
{
    public sealed class SqlSystemMonitorLogRepository : IAsyncSystemMonitorLogRepository
    {
        private readonly Func<SqlConnection> connectionFactory;
        private const string AuditSql = @"
SELECT TOP (@Take)
       CreatedAt AS [Time],
       UserName AS [User],
       UserRole AS [Role],
       ActionName AS [Action],
       EntityName AS [Module],
       Details AS [Details]
FROM dbo.AuditLogTb
ORDER BY CreatedAt DESC, AuditId DESC";
        private const string MovementSql = @"
SELECT TOP (@Take)
       CreatedAt AS [Time],
       MedName AS [Medicine],
       CASE
           WHEN QuantityDelta > 0 THEN N'زيادة'
           WHEN QuantityDelta < 0 THEN N'نقص'
           ELSE N'بدون تغيير'
       END AS [Direction],
       QuantityDelta AS [Delta],
       QuantityBefore AS [Before],
       QuantityAfter AS [After],
       CASE SourceType
           WHEN N'SalesInvoice' THEN N'فاتورة بيع'
           WHEN N'PurchaseInvoice' THEN N'فاتورة شراء'
           WHEN N'OpeningStock' THEN N'مخزون أول المدة'
           WHEN N'StockAdjustment' THEN N'تسوية مخزون'
           WHEN N'StockReceipt' THEN N'توريد مخزني'
           WHEN N'StockIssue' THEN N'صرف مخزني'
           ELSE SourceType
       END AS [Source],
       SourceId AS [SourceId],
       UnitCost AS [UnitCost],
       UnitPrice AS [UnitPrice],
       CreatedBy AS [User],
       Notes AS [Notes]
FROM dbo.InventoryMovementsTb
ORDER BY CreatedAt DESC, MovementId DESC";

        public SqlSystemMonitorLogRepository(Func<SqlConnection> connectionFactory)
        { this.connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory)); }

        public OperationalReportResult LoadAudit(int take) { return Load(AuditSql, take); }
        public OperationalReportResult LoadInventoryMovements(int take) { return Load(MovementSql, take); }
        public Task<OperationalReportResult> LoadAuditAsync(int take, CancellationToken cancellation)
        { return LoadAsync(AuditSql, take, cancellation); }
        public Task<OperationalReportResult> LoadInventoryMovementsAsync(int take, CancellationToken cancellation)
        { return LoadAsync(MovementSql, take, cancellation); }

        private OperationalReportResult Load(string sql, int take)
        {
            using (SqlConnection connection = connectionFactory())
            using (SqlCommand command = connection.CreateCommand())
            using (var table = new DataTable())
            {
                command.CommandText = sql;
                command.Parameters.Add("@Take", SqlDbType.Int).Value = take;
                using (var adapter = new SqlDataAdapter(command)) adapter.Fill(table);
                return new OperationalReportResult(
                    table.Columns.Cast<DataColumn>().Select(column =>
                        new OperationalReportColumn(column.ColumnName, column.DataType)),
                    table.Rows.Cast<DataRow>().Select(row =>
                        row.ItemArray.Select(value => value == DBNull.Value ? null : value).ToArray()));
            }
        }

        private async Task<OperationalReportResult> LoadAsync(string sql, int take, CancellationToken cancellation)
        {
            try { return await LoadCoreAsync(sql, take, cancellation).ConfigureAwait(false); }
            catch (SqlException exception) when (cancellation.IsCancellationRequested)
            {
                // SqlClient can report ATTENTION cancellation as SqlException while reading metadata.
                throw new OperationCanceledException("System monitor SQL read cancelled.", exception, cancellation);
            }
        }

        private async Task<OperationalReportResult> LoadCoreAsync(string sql, int take, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            using (SqlConnection connection = connectionFactory())
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandText = sql;
                command.Parameters.Add("@Take", SqlDbType.Int).Value = take;
                await connection.OpenAsync(cancellation).ConfigureAwait(false);
                using (SqlDataReader reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellation).ConfigureAwait(false))
                {
                    var columns = Enumerable.Range(0, reader.FieldCount).Select(index =>
                        new OperationalReportColumn(reader.GetName(index), reader.GetFieldType(index))).ToArray();
                    var rows = new List<object[]>();
                    while (await reader.ReadAsync(cancellation).ConfigureAwait(false))
                    {
                        var values = new object[reader.FieldCount];
                        for (int index = 0; index < values.Length; index++)
                            values[index] = await reader.IsDBNullAsync(index, cancellation).ConfigureAwait(false)
                                ? null : await reader.GetFieldValueAsync<object>(index, cancellation).ConfigureAwait(false);
                        rows.Add(values);
                    }
                    cancellation.ThrowIfCancellationRequested();
                    return new OperationalReportResult(columns, rows);
                }
            }
        }
    }
}


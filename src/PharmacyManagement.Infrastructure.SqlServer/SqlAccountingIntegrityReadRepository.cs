using System;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using PharmacyManagement.Application;

namespace PharmacyManagement.Infrastructure.SqlServer
{
    public sealed class SqlAccountingIntegrityReadRepository : IAccountingIntegrityReadRepository, IAsyncAccountingIntegrityReadRepository
    {
        private const string IntegritySql = @"
SELECT
    e.JournalEntryId AS [رقم القيد],
    e.EntryDate AS [التاريخ],
    ISNULL(e.SourceType, N'Manual') AS [المصدر],
    ISNULL(e.SourceId, N'') AS [رقم المرجع],
    ISNULL(e.Description, N'') AS [البيان],
    COUNT(l.JournalEntryLineId) AS [عدد الأسطر],
    ISNULL(SUM(l.Debit), 0) AS [إجمالي المدين],
    ISNULL(SUM(l.Credit), 0) AS [إجمالي الدائن],
    ISNULL(SUM(l.Debit), 0) - ISNULL(SUM(l.Credit), 0) AS [الفرق],
    CASE
        WHEN COUNT(l.JournalEntryLineId) = 0 THEN N'قيد بلا أسطر'
        WHEN ABS(ISNULL(SUM(l.Debit), 0) - ISNULL(SUM(l.Credit), 0)) > 0.004 THEN N'القيد غير متوازن'
        ELSE N''
    END AS [الحالة]
FROM dbo.JournalEntriesTb e
LEFT JOIN dbo.JournalEntryLinesTb l ON l.JournalEntryId = e.JournalEntryId
WHERE e.EntryDate >= @FromDate AND e.EntryDate < DATEADD(DAY, 1, @ToDate)
GROUP BY e.JournalEntryId, e.EntryDate, e.SourceType, e.SourceId, e.Description
HAVING COUNT(l.JournalEntryLineId) = 0
    OR ABS(ISNULL(SUM(l.Debit), 0) - ISNULL(SUM(l.Credit), 0)) > 0.004
ORDER BY e.EntryDate DESC, e.JournalEntryId DESC;";
        private readonly Func<SqlConnection> connectionFactory;
        public SqlAccountingIntegrityReadRepository(Func<SqlConnection> connectionFactory)
        { this.connectionFactory=connectionFactory??throw new ArgumentNullException(nameof(connectionFactory)); }
        public OperationalReportResult Load(AccountingIntegrityRequest request)
        {
            if(request==null) throw new ArgumentNullException(nameof(request));
            using(SqlConnection connection=connectionFactory()) return Load(connection,request);
        }
        public async System.Threading.Tasks.Task<OperationalReportResult> LoadAsync(AccountingIntegrityRequest request, System.Threading.CancellationToken cancellation)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            cancellation.ThrowIfCancellationRequested();
            try
            {
                using (SqlConnection connection = connectionFactory())
                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = IntegritySql; command.CommandTimeout = 15;
                    command.Parameters.Add("@FromDate", SqlDbType.DateTime).Value = request.FromDate;
                    command.Parameters.Add("@ToDate", SqlDbType.DateTime).Value = request.ToDate;
                    await connection.OpenAsync(cancellation).ConfigureAwait(false);
                    using (SqlDataReader reader = await command.ExecuteReaderAsync(cancellation).ConfigureAwait(false))
                    {
                        var columns = new System.Collections.Generic.List<OperationalReportColumn>();
                        var rows = new System.Collections.Generic.List<object[]>();
                        for (int i = 0; i < reader.FieldCount; i++) columns.Add(new OperationalReportColumn(reader.GetName(i), reader.GetFieldType(i)));
                        while (await reader.ReadAsync(cancellation).ConfigureAwait(false))
                        {
                            cancellation.ThrowIfCancellationRequested();
                            var values = new object[reader.FieldCount]; reader.GetValues(values);
                            for (int i = 0; i < values.Length; i++) if (values[i] == DBNull.Value) values[i] = null;
                            rows.Add(values);
                        }
                        cancellation.ThrowIfCancellationRequested();
                        return new OperationalReportResult(columns, rows);
                    }
                }
            }
            catch (SqlException error) when (cancellation.IsCancellationRequested)
            { throw new OperationCanceledException("Accounting integrity read was canceled.", error, cancellation); }
        }
        // This overload retains the caller's connection ownership.
        public static OperationalReportResult Load(SqlConnection connection, AccountingIntegrityRequest request)
        {
            if(request==null) throw new ArgumentNullException(nameof(request));
            if(connection==null) throw new ArgumentNullException(nameof(connection));
            using(SqlCommand command=connection.CreateCommand())
            using(var table=new DataTable())
            {
                command.CommandText=IntegritySql;
                command.Parameters.Add("@FromDate",SqlDbType.DateTime).Value=request.FromDate;
                command.Parameters.Add("@ToDate",SqlDbType.DateTime).Value=request.ToDate;
                using(var adapter=new SqlDataAdapter(command)) adapter.Fill(table);
                return new OperationalReportResult(table.Columns.Cast<DataColumn>().Select(column=>
                    new OperationalReportColumn(column.ColumnName,column.DataType)),
                    table.Rows.Cast<DataRow>().Select(row=>row.ItemArray.Select(value=>value==DBNull.Value?null:value).ToArray()));
            }
        }
    }
}

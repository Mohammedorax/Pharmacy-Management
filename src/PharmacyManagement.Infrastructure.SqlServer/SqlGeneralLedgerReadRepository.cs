using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Application;
namespace PharmacyManagement.Infrastructure.SqlServer
{
    public sealed class SqlGeneralLedgerReadRepository : IGeneralLedgerReadRepository
    {
        private readonly Func<SqlConnection> connectionFactory;
        public SqlGeneralLedgerReadRepository(Func<SqlConnection> connectionFactory)
        { this.connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory)); }
        // Unique line/header/center keys keep ROWS totals identical to the original peer frame.
        // The date range drives the read through the entry and line indexes, so the plan is not
        // pinned to a lines-first scan of the whole account.
        private const string LedgerSql = @"DECLARE @OpeningBalance DECIMAL(18, 2);
SELECT @OpeningBalance = CASE WHEN @IncludePreviousBalance = 1 THEN ISNULL(SUM(l.Debit - l.Credit), 0) ELSE 0 END
FROM dbo.JournalEntryLinesTb l
INNER JOIN dbo.JournalEntriesTb e ON e.JournalEntryId = l.JournalEntryId
WHERE l.AccountCode = @AccountCode
  AND e.EntryDate < @FromDate
  AND (@SourceType IS NULL OR ISNULL(e.SourceType, N'Manual') = @SourceType)
  AND (@IncludeOpeningEntries = 1 OR ISNULL(e.SourceType, N'Manual') <> N'OpeningEntry')
  AND (@CostCenterId IS NULL OR (@CostCenterId = 0 AND l.CostCenterId IS NULL) OR l.CostCenterId = @CostCenterId);

SELECT @OpeningBalance AS PreviousBalance;

SELECT e.EntryDate AS [التاريخ],
       e.JournalEntryId AS [رقم المستند],
       ISNULL(e.SourceType, N'Manual') AS [نوع المستند],
       ISNULL(e.SourceId, N'') AS [رقم المرجع],
       ISNULL(c.CostCenterName, N'') AS [مركز التكلفة],
       e.Description AS [البيان],
       l.Debit AS [مدين],
       l.Credit AS [دائن],
       @OpeningBalance + SUM(l.Debit - l.Credit) OVER (ORDER BY e.EntryDate, e.JournalEntryId, l.JournalEntryLineId ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS [الرصيد]
FROM dbo.JournalEntryLinesTb l
INNER JOIN dbo.JournalEntriesTb e ON e.JournalEntryId = l.JournalEntryId
LEFT JOIN dbo.CostCentersTb c ON c.CostCenterId = l.CostCenterId
WHERE l.AccountCode = @AccountCode
  AND e.EntryDate >= @FromDate AND e.EntryDate < DATEADD(DAY, 1, @ToDate)
  AND (@SourceType IS NULL OR ISNULL(e.SourceType, N'Manual') = @SourceType)
  AND (@IncludeOpeningEntries = 1 OR ISNULL(e.SourceType, N'Manual') <> N'OpeningEntry')
  AND (@CostCenterId IS NULL OR (@CostCenterId = 0 AND l.CostCenterId IS NULL) OR l.CostCenterId = @CostCenterId)
ORDER BY e.EntryDate, e.JournalEntryId, l.JournalEntryLineId;";
        public async Task<GeneralLedgerTableRead> LoadTableAsync(GeneralLedgerReadRequest request, CancellationToken cancellationToken)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using (var connection = connectionFactory())
                {
                    await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();
                    using (var transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                    using (var command = CreateCommand(connection, transaction, request))
                    {
                        decimal previous;
                        decimal periodDebit = 0m, periodCredit = 0m, ending = 0m;
                        int rows = 0;
                        var table = CreateLedgerTable();
                        using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                        {
                            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) throw new InvalidOperationException("Opening balance result was missing.");
                            previous = reader.GetDecimal(0);
                            if (!await reader.NextResultAsync(cancellationToken).ConfigureAwait(false)) throw new InvalidOperationException("Ledger rows result was missing.");
                            if (reader.FieldCount != 9) throw new InvalidOperationException("Ledger row was incomplete.");
                            // The rows go straight into the table, so the read materialises them once.
                            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                            {
                                decimal debit = reader.IsDBNull(6) ? 0m : reader.GetDecimal(6);
                                decimal credit = reader.IsDBNull(7) ? 0m : reader.GetDecimal(7);
                                ending = reader.GetDecimal(8);
                                periodDebit += debit; periodCredit += credit; rows++;
                                table.Rows.Add(reader.GetDateTime(0), reader.GetInt32(1),
                                    reader.IsDBNull(2) ? (object)DBNull.Value : reader.GetString(2),
                                    reader.IsDBNull(3) ? (object)DBNull.Value : reader.GetString(3),
                                    reader.IsDBNull(4) ? (object)DBNull.Value : reader.GetString(4),
                                    reader.IsDBNull(5) ? (object)DBNull.Value : reader.GetString(5),
                                    reader.IsDBNull(6) ? (object)DBNull.Value : reader.GetDecimal(6),
                                    reader.IsDBNull(7) ? (object)DBNull.Value : reader.GetDecimal(7),
                                    ending);
                            }
                        }
                        cancellationToken.ThrowIfCancellationRequested(); transaction.Commit();
                        cancellationToken.ThrowIfCancellationRequested();
                        return new GeneralLedgerTableRead(table, previous, periodDebit, periodCredit,
                            rows == 0 ? previous : ending);
                    }
                }
            }
            catch (SqlException error) when (cancellationToken.IsCancellationRequested)
            { throw new OperationCanceledException("General ledger read was canceled.", error, cancellationToken); }
        }

        private SqlCommand CreateCommand(SqlConnection connection, SqlTransaction transaction, GeneralLedgerReadRequest request)
        {
            var command = connection.CreateCommand();
            command.Transaction = transaction; command.CommandTimeout = 15; command.CommandText = LedgerSql;
            command.Parameters.Add("@AccountCode", SqlDbType.NVarChar, 20).Value = request.AccountCode;
            command.Parameters.Add("@FromDate", SqlDbType.DateTime).Value = request.FromDate;
            command.Parameters.Add("@ToDate", SqlDbType.DateTime).Value = request.ToDate;
            command.Parameters.Add("@SourceType", SqlDbType.NVarChar, 50).Value = (object)request.SourceType ?? DBNull.Value;
            command.Parameters.Add("@IncludePreviousBalance", SqlDbType.Bit).Value = request.IncludePreviousBalance;
            command.Parameters.Add("@IncludeOpeningEntries", SqlDbType.Bit).Value = request.IncludeOpeningEntries;
            command.Parameters.Add("@CostCenterId", SqlDbType.Int).Value = (object)request.CostCenterId ?? DBNull.Value;
            return command;
        }

        // The presentation table keeps the historical Arabic column names and types.
        private static DataTable CreateLedgerTable()
        {
            var table = new DataTable();
            string[] names = { "التاريخ", "رقم المستند", "نوع المستند", "رقم المرجع", "مركز التكلفة", "البيان", "مدين", "دائن", "الرصيد" };
            Type[] types = { typeof(DateTime), typeof(int), typeof(string), typeof(string), typeof(string), typeof(string), typeof(decimal), typeof(decimal), typeof(decimal) };
            for (int i = 0; i < names.Length; i++) table.Columns.Add(names[i], types[i]);
            return table;
        }

        public async Task<GeneralLedgerReadResult> LoadAsync(GeneralLedgerReadRequest request, CancellationToken cancellationToken)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using (var connection = connectionFactory())
                {
                    await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();
                    // One read transaction protects the opening balance and its accounting sequence.
                    // This requires QA contention/lock-duration acceptance before production use.
                    using (var transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                    using (var command = CreateCommand(connection, transaction, request))
                    {
                        decimal previous;
                        var rows = new List<GeneralLedgerReadRow>();
                        using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                        {
                            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) throw new InvalidOperationException("Opening balance result was missing.");
                            previous = reader.GetDecimal(0);
                            if (!await reader.NextResultAsync(cancellationToken).ConfigureAwait(false)) throw new InvalidOperationException("Ledger rows result was missing.");
                            if (reader.FieldCount != 9) throw new InvalidOperationException("Ledger row was incomplete.");
                            // Read the typed values directly: a shared object buffer boxes nine values
                            // per row and the extra allocations showed up in the measured P95.
                            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                            {
                                rows.Add(new GeneralLedgerReadRow(
                                    reader.GetDateTime(0),
                                    reader.GetInt32(1),
                                    reader.IsDBNull(2) ? null : reader.GetString(2),
                                    reader.IsDBNull(3) ? null : reader.GetString(3),
                                    reader.IsDBNull(4) ? null : reader.GetString(4),
                                    reader.IsDBNull(5) ? null : reader.GetString(5),
                                    reader.IsDBNull(6) ? (decimal?)null : reader.GetDecimal(6),
                                    reader.IsDBNull(7) ? (decimal?)null : reader.GetDecimal(7),
                                    reader.GetDecimal(8)));
                            }
                        }
                        cancellationToken.ThrowIfCancellationRequested(); transaction.Commit();
                        cancellationToken.ThrowIfCancellationRequested();
                        return new GeneralLedgerReadResult(rows, previous);
                    }
                }
            }
            catch (SqlException error) when (cancellationToken.IsCancellationRequested)
            { throw new OperationCanceledException("General ledger read was canceled.", error, cancellationToken); }
        }
    }
}

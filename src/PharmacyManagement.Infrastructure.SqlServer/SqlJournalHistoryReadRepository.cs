using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Application;

namespace PharmacyManagement.Infrastructure.SqlServer
{
    public sealed class SqlJournalHistoryReadRepository : IJournalHistoryReadRepository
    {
        private const string HistorySql = @"
SELECT e.JournalEntryId, e.EntryDate, ISNULL(e.SourceType, N'Manual'), ISNULL(e.SourceId, N''),
       e.Description, SUM(l.Debit), SUM(l.Credit), e.CreatedBy
FROM dbo.JournalEntriesTb e
INNER JOIN dbo.JournalEntryLinesTb l ON l.JournalEntryId = e.JournalEntryId
WHERE e.EntryDate >= @FromDate AND (@ExclusiveEnd IS NULL OR e.EntryDate < @ExclusiveEnd)
  AND (@SourceType = N'' OR e.SourceType = @SourceType)
GROUP BY e.JournalEntryId, e.EntryDate, e.SourceType, e.SourceId, e.Description, e.CreatedBy
ORDER BY e.EntryDate DESC, e.JournalEntryId DESC";
        private readonly Func<SqlConnection> connectionFactory;
        public SqlJournalHistoryReadRepository(Func<SqlConnection> connectionFactory)
        { this.connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory)); }
        public IReadOnlyList<JournalHistoryRow> Load(JournalHistoryReadRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            using (var connection = connectionFactory())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = HistorySql;
                command.Parameters.Add("@FromDate", SqlDbType.DateTime).Value = request.From;
                command.Parameters.Add("@ExclusiveEnd", SqlDbType.DateTime).Value = (object)request.ExclusiveEnd ?? DBNull.Value;
                command.Parameters.Add("@SourceType", SqlDbType.NVarChar, 50).Value = request.SourceType;
                connection.Open(); var rows = new List<JournalHistoryRow>();
                using (var reader = command.ExecuteReader())
                    while (reader.Read())
                        rows.Add(new JournalHistoryRow(reader.GetInt32(0), reader.GetDateTime(1),
                            reader.GetString(2), reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetString(4),
                            reader.IsDBNull(5) ? (decimal?)null : reader.GetDecimal(5),
                            reader.IsDBNull(6) ? (decimal?)null : reader.GetDecimal(6),
                            reader.IsDBNull(7) ? null : reader.GetString(7)));
                return rows.AsReadOnly();
            }
        }
        public async Task<IReadOnlyList<JournalHistoryRow>> LoadAsync(JournalHistoryReadRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (request == null) throw new ArgumentNullException(nameof(request));
            using (var connection = connectionFactory())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = HistorySql;
                command.Parameters.Add("@FromDate", SqlDbType.DateTime).Value = request.From;
                command.Parameters.Add("@ExclusiveEnd", SqlDbType.DateTime).Value = (object)request.ExclusiveEnd ?? DBNull.Value;
                command.Parameters.Add("@SourceType", SqlDbType.NVarChar, 50).Value = request.SourceType;
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false); var rows = new List<JournalHistoryRow>();
                using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                        rows.Add(new JournalHistoryRow(reader.GetInt32(0), reader.GetDateTime(1),
                            reader.GetString(2), reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetString(4),
                            reader.IsDBNull(5) ? (decimal?)null : reader.GetDecimal(5),
                            reader.IsDBNull(6) ? (decimal?)null : reader.GetDecimal(6),
                            reader.IsDBNull(7) ? null : reader.GetString(7)));
                return rows.AsReadOnly();
            }
        }
    }
}

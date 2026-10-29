using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Application;
using PharmacyManagement.Domain;

namespace PharmacyManagement.Infrastructure.SqlServer
{
    public sealed class SqlJournalSubmissionStatusRepository : IJournalSubmissionStatusRepository
    {
        private readonly Func<SqlConnection> connectionFactory;
        public SqlJournalSubmissionStatusRepository(Func<SqlConnection> connectionFactory)
        { this.connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory)); }
        public async Task<JournalSubmissionStatus> FindAsync(JournalSubmissionStatusRequest request, OperationActor actor, CancellationToken token)
        {
            token.ThrowIfCancellationRequested(); request = JournalSubmissionStatusService.PrepareRequest(request);
            if (actor == null || string.IsNullOrWhiteSpace(actor.UserName)) throw new UnauthorizedAccessException("مستخدم قراءة حالة القيد مطلوب.");
            using (var connection = connectionFactory())
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (var transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        int id = 0; DateTime date = default(DateTime); string description = null; string storedSystemCurrencyCode=null; bool? matches = null;
                        using (var command = new SqlCommand(@"SELECT TOP (1) JournalEntryId, CreatedBy,EntryDate,Description,SystemCurrencyCode
FROM dbo.JournalEntriesTb WITH (UPDLOCK, HOLDLOCK)
WHERE SourceType = @SourceType AND SourceId = @SourceId", connection, transaction))
                        {
                            command.Parameters.Add("@SourceType", SqlDbType.NVarChar, 50).Value = request.SourceType;
                            command.Parameters.Add("@SourceId", SqlDbType.NVarChar, 50).Value = request.SourceId;
                            using (var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                            {
                                if (await reader.ReadAsync(token).ConfigureAwait(false))
                                {
                                    if (!string.Equals(Convert.ToString(reader[1]), actor.UserName.Trim(), StringComparison.OrdinalIgnoreCase))
                                        throw new UnauthorizedAccessException("القيد المحفوظ لا يخص مستخدم طلب الاستعادة.");
                                    id = Convert.ToInt32(reader[0]); if (id <= 0) throw new InvalidOperationException("معرّف القيد المحفوظ غير صالح.");
                                    date = reader.GetDateTime(2); description = reader.IsDBNull(3) ? null : reader.GetString(3);
                                    storedSystemCurrencyCode=reader.IsDBNull(4)?null:reader.GetString(4);
                                }
                            }
                        }
                        if (id > 0 && request.ExpectedRequest != null)
                        {
                            var lines = new List<JournalLineValues>();
                            using (var command = new SqlCommand("SELECT AccountCode,Debit,Credit,LineDescription,CostCenterId FROM dbo.JournalEntryLinesTb WHERE JournalEntryId=@Id ORDER BY JournalEntryLineId", connection, transaction))
                            {
                                command.Parameters.Add("@Id", SqlDbType.Int).Value = id;
                                using (var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                                    while (await reader.ReadAsync(token).ConfigureAwait(false)) lines.Add(new JournalLineValues(reader.GetString(0), reader.GetDecimal(1), reader.GetDecimal(2), reader.IsDBNull(3) ? null : reader.GetString(3), reader.IsDBNull(4) ? (int?)null : reader.GetInt32(4)));
                            }
                            matches = SqlJournalRequestComparison.Matches(request.ExpectedRequest, date, description, lines,storedSystemCurrencyCode);
                        }
                        token.ThrowIfCancellationRequested(); transaction.Commit(); return new JournalSubmissionStatus(id, matches);
                    }
                    catch
                    {
                        try { transaction.Rollback(); } catch { /* Keep the original failure. */ }
                        if (token.IsCancellationRequested) throw new OperationCanceledException(token); throw;
                    }
                }
            }
        }
    }
}

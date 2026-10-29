using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using PharmacyManagement.Application;
using PharmacyManagement.Domain;

namespace PharmacyManagement.Infrastructure.SqlServer
{
    // Comparison only: follow existing SQL datetime/text persistence semantics.
    // Never change the saved document or recalculate it using new settings.
    public static class SqlJournalRequestComparison
    {
        private static string Text(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        public static bool IsManualIdentity(JournalPostRequest request)
        {
            Guid identity;
            return request != null && (request.SourceType == "Manual" || request.SourceType == "OpeningEntry") &&
                Guid.TryParseExact(request.SourceId, "N", out identity) && identity != Guid.Empty;
        }
        public static bool Matches(JournalPostRequest expected, DateTime date, string description, IReadOnlyList<JournalLineValues> lines)
        {
            expected = JournalPostService.PrepareRequest(expected);
            if (lines == null) throw new ArgumentNullException(nameof(lines));
            if (new SqlDateTime(expected.Date).Value != date || Text(expected.Description) != Text(description) || expected.Lines.Count != lines.Count) return false;
            for (int index = 0; index < lines.Count; index++)
            {
                var a = expected.Lines[index]; var b = lines[index];
                if (b == null || a.AccountCode != b.AccountCode || a.Debit != b.Debit || a.Credit != b.Credit ||
                    Text(a.Description) != Text(b.Description) || a.CostCenterId != b.CostCenterId) return false;
            }
            return true;
        }
        public static bool Matches(JournalPostRequest expected, DateTime date, string description, IReadOnlyList<JournalLineValues> lines, string storedSystemCurrencyCode)
        {
            expected=JournalPostService.PrepareRequest(expected);
            if(expected.ExpectedSystemCurrencyCode!=null &&
                !string.Equals(expected.ExpectedSystemCurrencyCode,Text(storedSystemCurrencyCode),StringComparison.OrdinalIgnoreCase))return false;
            return Matches(expected,date,description,lines);
        }
        public static void EnsureExistingMatches(SqlConnection connection, SqlTransaction transaction, JournalPostRequest expected, OperationActor actor, int id)
        {
            if (!ExistingMatches(connection, transaction, expected, actor, id))
                throw new JournalAcceptedRequestConflictException(id, expected.SourceType, expected.SourceId);
        }
        public static bool ExistingMatches(SqlConnection connection, SqlTransaction transaction, JournalPostRequest expected, OperationActor actor, int id)
        { return ExistingMatchesAsync(connection,transaction,expected,actor,id,CancellationToken.None).GetAwaiter().GetResult(); }
        public static async Task<bool> ExistingMatchesAsync(SqlConnection connection, SqlTransaction transaction, JournalPostRequest expected, OperationActor actor, int id, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            DateTime date; string description; string storedSystemCurrencyCode;
            using (var command = new SqlCommand("SELECT EntryDate,Description,CreatedBy,SystemCurrencyCode FROM dbo.JournalEntriesTb WHERE JournalEntryId=@Id", connection, transaction))
            {
                command.Parameters.Add("@Id", SqlDbType.Int).Value = id;
                using (var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                {
                    if (!await reader.ReadAsync(token).ConfigureAwait(false)) throw new InvalidOperationException("القيد المعتمد غير موجود.");
                    if (actor == null || string.IsNullOrWhiteSpace(actor.UserName) || !string.Equals(Convert.ToString(reader[2]), actor.UserName.Trim(), StringComparison.OrdinalIgnoreCase))
                        throw new UnauthorizedAccessException("القيد المعتمد لا يخص مستخدم الطلب.");
                    date = reader.GetDateTime(0); description = reader.IsDBNull(1) ? null : reader.GetString(1);
                    storedSystemCurrencyCode=reader.IsDBNull(3)?null:reader.GetString(3);
                }
            }
            var lines = new List<JournalLineValues>();
            using (var command = new SqlCommand("SELECT AccountCode,Debit,Credit,LineDescription,CostCenterId FROM dbo.JournalEntryLinesTb WHERE JournalEntryId=@Id ORDER BY JournalEntryLineId", connection, transaction))
            {
                command.Parameters.Add("@Id", SqlDbType.Int).Value = id;
                using (var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false)) while (await reader.ReadAsync(token).ConfigureAwait(false)) lines.Add(new JournalLineValues(reader.GetString(0), reader.GetDecimal(1), reader.GetDecimal(2), reader.IsDBNull(3) ? null : reader.GetString(3), reader.IsDBNull(4) ? (int?)null : reader.GetInt32(4)));
            }
            return Matches(expected, date, description, lines,storedSystemCurrencyCode);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using PharmacyManagement.Application;
using PharmacyManagement.Domain;

namespace project_fo_3
{
    // Shared SQL boundary; the caller owns and commits the supplied transaction.
    internal static class PurchaseAsyncJournalWriter
    {
        public static async System.Threading.Tasks.Task<JournalPostResult> CreateAsync(SqlConnection connection, SqlTransaction transaction, DateTime entryDate, string sourceType, string sourceId, string description, string createdBy, IEnumerable<JournalLineValues> lines,System.Threading.CancellationToken cancellationToken,string expectedSystemCurrencyCode=null)
        {
            if(expectedSystemCurrencyCode!=null)return await PharmacyManagement.Infrastructure.SqlServer.SqlJournalWriter.CreateWithCurrencyAsync(connection,transaction,entryDate,sourceType,sourceId,description,createdBy,lines,expectedSystemCurrencyCode,cancellationToken).ConfigureAwait(false);
            var cleanLines = JournalEntryPreparation.Prepare(lines);

            int existingJournalEntryId = string.IsNullOrWhiteSpace(sourceType) || string.IsNullOrWhiteSpace(sourceId)
                ? 0
                : await GetJournalEntryIdBySourceAsync(connection, transaction, sourceType, sourceId,cancellationToken).ConfigureAwait(false);
            if (existingJournalEntryId > 0)
            {
                return new JournalPostResult(existingJournalEntryId, true);
            }

            // هذا هو الحد المشترك لكل قيد جديد: البيع والمشتريات والسندات
            // والإلغاءات والقيود اليدوية. يبقى تكرار مصدر سبق ترحيله آمناً أعلاه.
            await PurchaseAsyncBusinessPeriodPolicy.EnsureBusinessDateOpenAsync(connection, transaction, entryDate,cancellationToken).ConfigureAwait(false);

            int journalEntryId;
            using (SqlCommand command = new SqlCommand(@"
INSERT INTO dbo.JournalEntriesTb (EntryDate, SourceType, SourceId, Description, CreatedBy)
OUTPUT INSERTED.JournalEntryId
VALUES (@EntryDate, @SourceType, @SourceId, @Description, @CreatedBy);", connection, transaction))
            {
                command.Parameters.Add("@EntryDate", SqlDbType.DateTime).Value = entryDate;
                command.Parameters.Add("@SourceType", SqlDbType.NVarChar, 50).Value = ToDbValue(sourceType);
                command.Parameters.Add("@SourceId", SqlDbType.NVarChar, 50).Value = ToDbValue(sourceId);
                command.Parameters.Add("@Description", SqlDbType.NVarChar, 500).Value = ToDbValue(description);
                command.Parameters.Add("@CreatedBy", SqlDbType.NVarChar, 200).Value = ToDbValue(createdBy);
                journalEntryId = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
            }

            foreach (JournalLineValues line in cleanLines)
            {
                await EnsureAccountExistsAsync(connection, transaction, line.AccountCode,cancellationToken).ConfigureAwait(false);
                using (SqlCommand command = new SqlCommand(@"
INSERT INTO dbo.JournalEntryLinesTb (JournalEntryId, AccountCode, Debit, Credit, LineDescription, CostCenterId)
VALUES (@JournalEntryId, @AccountCode, @Debit, @Credit, @LineDescription, @CostCenterId);", connection, transaction))
                {
                    command.Parameters.Add("@JournalEntryId", SqlDbType.Int).Value = journalEntryId;
                    command.Parameters.Add("@AccountCode", SqlDbType.NVarChar, 20).Value = line.AccountCode;
                    command.Parameters.Add("@Debit", SqlDbType.Decimal).Value = line.Debit;
                    command.Parameters.Add("@Credit", SqlDbType.Decimal).Value = line.Credit;
                    command.Parameters.Add("@LineDescription", SqlDbType.NVarChar, 500).Value = ToDbValue(line.Description);
                    command.Parameters.Add("@CostCenterId", SqlDbType.Int).Value = line.CostCenterId.HasValue ? (object)line.CostCenterId.Value : DBNull.Value;
                    await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }
            }

            return new JournalPostResult(journalEntryId, false);
        }

        public static async System.Threading.Tasks.Task<int> GetJournalEntryIdBySourceAsync(SqlConnection connection, SqlTransaction transaction, string sourceType, string sourceId,System.Threading.CancellationToken cancellationToken)
        {
            using (SqlCommand command = new SqlCommand(@"
SELECT TOP 1 JournalEntryId
FROM dbo.JournalEntriesTb WITH (UPDLOCK, HOLDLOCK)
WHERE SourceType = @SourceType AND SourceId = @SourceId", connection, transaction))
            {
                command.Parameters.Add("@SourceType", SqlDbType.NVarChar, 50).Value = sourceType;
                command.Parameters.Add("@SourceId", SqlDbType.NVarChar, 50).Value = sourceId;
                object result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                return result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);
            }
        }

        private static async System.Threading.Tasks.Task EnsureAccountExistsAsync(SqlConnection connection, SqlTransaction transaction, string accountCode,System.Threading.CancellationToken cancellationToken)
        {
            using (SqlCommand command = new SqlCommand("SELECT COUNT(1) FROM dbo.AccountsTb WHERE AccountCode = @AccountCode AND IsActive = 1", connection, transaction))
            {
                command.Parameters.Add("@AccountCode", SqlDbType.NVarChar, 20).Value = accountCode;
                if (Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)) == 0)
                {
                    throw new InvalidOperationException("الحساب غير موجود أو غير مفعل: " + accountCode);
                }
            }
        }

        private static object ToDbValue(string value)
        { return string.IsNullOrWhiteSpace(value) ? (object)DBNull.Value : value.Trim(); }
    }
}

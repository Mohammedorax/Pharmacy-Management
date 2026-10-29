using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using PharmacyManagement.Application;
using PharmacyManagement.Domain;

namespace PharmacyManagement.Infrastructure.SqlServer
{
    // Shared SQL boundary; the caller owns and commits the supplied transaction.
    public static class SqlJournalWriter
    {
        public static JournalPostResult Create(SqlConnection connection, SqlTransaction transaction, DateTime entryDate, string sourceType, string sourceId, string description, string createdBy, IEnumerable<JournalLineValues> lines)
        { return CreateAsync(connection,transaction,entryDate,sourceType,sourceId,description,createdBy,lines,CancellationToken.None).GetAwaiter().GetResult(); }
        public static async Task<JournalPostResult> CreateAsync(SqlConnection connection, SqlTransaction transaction, DateTime entryDate, string sourceType, string sourceId, string description, string createdBy, IEnumerable<JournalLineValues> lines, CancellationToken token)
        { return await CreateWithCurrencyAsync(connection,transaction,entryDate,sourceType,sourceId,description,createdBy,lines,null,token).ConfigureAwait(false); }
        public static async Task<JournalPostResult> CreateWithCurrencyAsync(SqlConnection connection, SqlTransaction transaction, DateTime entryDate, string sourceType, string sourceId, string description, string createdBy, IEnumerable<JournalLineValues> lines, string expectedSystemCurrencyCode, CancellationToken token, bool validateCurrentSettings = true)
        {
            token.ThrowIfCancellationRequested();
            var cleanLines = JournalEntryPreparation.Prepare(lines);

            int existingJournalEntryId = string.IsNullOrWhiteSpace(sourceType) || string.IsNullOrWhiteSpace(sourceId)
                ? 0
                : await GetJournalEntryIdBySourceAsync(connection, transaction, sourceType, sourceId, token).ConfigureAwait(false);
            if (existingJournalEntryId > 0)
            {
                return new JournalPostResult(existingJournalEntryId, true);
            }

            if (expectedSystemCurrencyCode != null)
            {
                // Hold the settings lock until the caller commits; currency cannot change between validation and posting.
                using (var command = new SqlCommand("SELECT CurrencyCode FROM dbo.CurrencySettingsTb WITH (TABLOCK, HOLDLOCK) WHERE IsSystemCurrency=1", connection, transaction))
                using (var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                {
                    if (!await reader.ReadAsync(token).ConfigureAwait(false) || reader.IsDBNull(0) ||
                        !string.Equals(reader.GetString(0).Trim(), expectedSystemCurrencyCode, StringComparison.OrdinalIgnoreCase) ||
                        await reader.ReadAsync(token).ConfigureAwait(false))
                        throw new InvalidOperationException("تغيرت عملة النظام أو تعذر تحديدها؛ لم يعتمد القيد. احتفظ بالمسودة وراجع عملتها قبل الحفظ.");
                }
            }

            // هذا هو الحد المشترك لكل قيد جديد: البيع والمشتريات والسندات
            // والإلغاءات والقيود اليدوية. يبقى تكرار مصدر سبق ترحيله آمناً أعلاه.
            await SqlBusinessPeriodPolicy.EnsureBusinessDateOpenAsync(connection, transaction, entryDate, token).ConfigureAwait(false);

            int journalEntryId;
            string headerSql=expectedSystemCurrencyCode==null
                ? @"INSERT INTO dbo.JournalEntriesTb (EntryDate, SourceType, SourceId, Description, CreatedBy)
OUTPUT INSERTED.JournalEntryId VALUES (@EntryDate, @SourceType, @SourceId, @Description, @CreatedBy);"
                : @"INSERT INTO dbo.JournalEntriesTb (EntryDate, SourceType, SourceId, Description, CreatedBy, SystemCurrencyCode)
OUTPUT INSERTED.JournalEntryId VALUES (@EntryDate, @SourceType, @SourceId, @Description, @CreatedBy, @SystemCurrencyCode);";
            using (SqlCommand command = new SqlCommand(headerSql, connection, transaction))
            {
                command.Parameters.Add("@EntryDate", SqlDbType.DateTime).Value = entryDate;
                command.Parameters.Add("@SourceType", SqlDbType.NVarChar, 50).Value = ToDbValue(sourceType);
                command.Parameters.Add("@SourceId", SqlDbType.NVarChar, 50).Value = ToDbValue(sourceId);
                command.Parameters.Add("@Description", SqlDbType.NVarChar, 500).Value = ToDbValue(description);
                command.Parameters.Add("@CreatedBy", SqlDbType.NVarChar, 200).Value = ToDbValue(createdBy);
                if(expectedSystemCurrencyCode!=null)command.Parameters.Add("@SystemCurrencyCode",SqlDbType.NVarChar,10).Value=expectedSystemCurrencyCode;
                journalEntryId = Convert.ToInt32(await command.ExecuteScalarAsync(token).ConfigureAwait(false));
            }

            foreach (JournalLineValues line in cleanLines)
            {
                await EnsureAccountExistsAsync(connection, transaction, line.AccountCode, token).ConfigureAwait(false);
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
                    await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }
            }

            return new JournalPostResult(journalEntryId, false);
        }

        // Only these document producers may capture a saved document currency. The caller owns rollback/commit.
        // Historical reversals keep the original document currency. They must never fail because the
        // system currency changed after the original document was posted, so the settings lock is skipped.
        public static JournalPostResult CreateHistoricalCurrency(SqlConnection connection, SqlTransaction transaction,
            DateTime entryDate, string sourceType, string sourceId, string description, string createdBy,
            IEnumerable<JournalLineValues> lines, string systemCurrencyCode)
        {
            if (string.IsNullOrWhiteSpace(systemCurrencyCode))
                return Create(connection, transaction, entryDate, sourceType, sourceId, description, createdBy, lines);
            return CreateWithCurrencyAsync(connection, transaction, entryDate, sourceType, sourceId, description,
                createdBy, lines, systemCurrencyCode, CancellationToken.None, false).GetAwaiter().GetResult();
        }
        public static JournalPostResult CreateSalesDocument(SqlConnection connection, SqlTransaction transaction,
            DateTime entryDate, string sourceType, string sourceId, string description, string createdBy,
            IEnumerable<JournalLineValues> lines, string expectedCurrentCurrencyCode = null)
        {
            if (connection == null || transaction == null || transaction.Connection != connection)
                throw new ArgumentException("اتصال ومعاملة SQL مطلوبان لحفظ عملة المستند.");
            if (sourceType != "SalesInvoice" && sourceType != "SalesInvoiceCancellation" && sourceType != "CommercialSalesReturn")
                throw new ArgumentException("مصدر المستند غير مسموح لحفظ عملة البيع.", nameof(sourceType));
            if (string.IsNullOrWhiteSpace(sourceId)) throw new ArgumentException("مرجع المستند مطلوب.", nameof(sourceId));
            var prepared = JournalEntryPreparation.Prepare(lines);
            int existing = GetJournalEntryIdBySource(connection, transaction, sourceType, sourceId);
            if (existing > 0) return new JournalPostResult(existing, true); // Never backfill or reinterpret an accepted retry.

            string currency;
            bool hasCommercialDocument;
            if (sourceType == "SalesInvoice")
            {
                currency = ReadSalesDocumentCurrency(connection, transaction, sourceId, "Sale", out hasCommercialDocument);
                if (!hasCommercialDocument)
                {
                    // A new legacy sale has no document currency column; capture the transaction's locked current currency.
                    using (var command = new SqlCommand("SELECT CurrencyCode FROM dbo.CurrencySettingsTb WITH (TABLOCK,HOLDLOCK) WHERE IsSystemCurrency=1", connection, transaction))
                    using (var reader = command.ExecuteReader())
                    {
                        if (!reader.Read()) throw new InvalidOperationException("تعذر تحديد عملة البيع الجديدة.");
                        currency = NormalizeSalesCurrency(reader.IsDBNull(0) ? null : reader.GetString(0));
                        if (currency == null || reader.Read()) throw new InvalidOperationException("تعذر تحديد عملة بيع واحدة صالحة.");
                    }
                    if (expectedCurrentCurrencyCode != null &&
                        !string.Equals(currency, NormalizeSalesCurrency(expectedCurrentCurrencyCode), StringComparison.Ordinal))
                        throw new InvalidOperationException("تغيرت عملة مسودة البيع؛ لم تحفظ الفاتورة. افتح مسودة بالعملة الصحيحة.");
                    return CreateWithCurrencyAsync(connection, transaction, entryDate, sourceType, sourceId,
                        description, createdBy, prepared, currency, CancellationToken.None).GetAwaiter().GetResult();
                }
                if (expectedCurrentCurrencyCode != null &&
                    !string.Equals(currency, NormalizeSalesCurrency(expectedCurrentCurrencyCode), StringComparison.Ordinal))
                    throw new InvalidOperationException("عملة المستند التجاري لا تطابق عملة مسودة البيع.");
            }
            else if (sourceType == "CommercialSalesReturn")
            {
                currency = ReadSalesDocumentCurrency(connection, transaction, sourceId, "CreditNote", out hasCommercialDocument);
                if (!hasCommercialDocument) throw new InvalidOperationException("إشعار المرتجع المحفوظ مطلوب لحفظ عملة قيده.");
            }
            else
            {
                // Historical reversal: no comparison with today's currency setting and no monetary conversion.
                string journalCurrency = ReadOriginalSalesJournalCurrency(connection, transaction, sourceId);
                string documentCurrency = ReadSalesDocumentCurrency(connection, transaction, sourceId, "Sale", out hasCommercialDocument);
                string unitCurrency = ReadLegacySalesUnitCurrency(connection, transaction, sourceId);
                currency = MergeSalesCurrency(MergeSalesCurrency(journalCurrency, documentCurrency), unitCurrency);
            }
            // The original writer still enforces source idempotency, open periods, balancing and active accounts.
            var result = Create(connection, transaction, entryDate, sourceType, sourceId, description, createdBy, prepared);
            if (currency != null)
            {
                using (var command = new SqlCommand(@"UPDATE dbo.JournalEntriesTb SET SystemCurrencyCode=@Currency
WHERE JournalEntryId=@Id AND SystemCurrencyCode IS NULL;", connection, transaction))
                {
                    command.Parameters.Add("@Currency", SqlDbType.NVarChar, 10).Value = currency;
                    command.Parameters.Add("@Id", SqlDbType.Int).Value = result.Id;
                    if (command.ExecuteNonQuery() != 1) throw new InvalidOperationException("تعذر حفظ عملة القيد الجديد؛ يجب التراجع عن المستند.");
                }
            }
            return result;
        }

        private static string NormalizeSalesCurrency(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            value = value.Trim().ToUpperInvariant();
            if (value.Length > 10) throw new InvalidOperationException("عملة المستند المحفوظ غير صالحة.");
            return value;
        }
        private static string MergeSalesCurrency(string first, string second)
        {
            if (first != null && second != null && first != second)
                throw new InvalidOperationException("تعارضت عملة القيد مع المستند الأصلي؛ أوقف العكس وراجع البيانات.");
            return first ?? second;
        }
        private static string ReadSalesDocumentCurrency(SqlConnection connection, SqlTransaction transaction,
            string sourceId, string expectedType, out bool found)
        {
            found = false;
            Guid id;
            if (!Guid.TryParse(sourceId, out id)) return null;
            using (var schema = new SqlCommand("SELECT CASE WHEN COL_LENGTH(N'dbo.SalesInvoicesTb',N'CurrencyCode') IS NOT NULL AND COL_LENGTH(N'dbo.SalesInvoicesTb',N'InvoiceType') IS NOT NULL THEN 1 ELSE 0 END", connection, transaction))
                if (Convert.ToInt32(schema.ExecuteScalar()) == 0) return null;
            using (var command = new SqlCommand(@"SELECT h.InvoiceType,h.CurrencyCode,o.InvoiceType,o.CurrencyCode
FROM dbo.SalesInvoicesTb h WITH (UPDLOCK,HOLDLOCK)
LEFT JOIN dbo.SalesInvoicesTb o WITH (UPDLOCK,HOLDLOCK) ON o.InvoiceId=h.OriginalInvoiceId
WHERE h.InvoiceId=@Id;", connection, transaction))
            {
                command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = id;
                using (var reader = command.ExecuteReader())
                {
                    if (!reader.Read()) return null;
                    found = true;
                    if (Convert.ToString(reader[0]) != expectedType) throw new InvalidOperationException("نوع المستند المحفوظ لا يطابق مصدر القيد.");
                    string currency = NormalizeSalesCurrency(Convert.ToString(reader[1]));
                    if (currency == null) throw new InvalidOperationException("عملة المستند التجاري المحفوظ مفقودة؛ لم يعتمد القيد.");
                    if (expectedType == "CreditNote" && (Convert.ToString(reader[2]) != "Sale" ||
                        NormalizeSalesCurrency(Convert.ToString(reader[3])) != currency))
                        throw new InvalidOperationException("عملة إشعار المرتجع لا تطابق الفاتورة الأصلية.");
                    return currency;
                }
            }
        }
        private static string ReadOriginalSalesJournalCurrency(SqlConnection connection, SqlTransaction transaction, string sourceId)
        {
            bool hasCurrency;
            using (var schema = new SqlCommand("SELECT CASE WHEN COL_LENGTH(N'dbo.JournalEntriesTb',N'SystemCurrencyCode') IS NULL THEN 0 ELSE 1 END", connection, transaction))
                hasCurrency = Convert.ToInt32(schema.ExecuteScalar()) == 1;
            using (var command = new SqlCommand("SELECT " + (hasCurrency ? "SystemCurrencyCode" : "CAST(NULL AS nvarchar(10))") +
                " FROM dbo.JournalEntriesTb WITH (UPDLOCK,HOLDLOCK) WHERE SourceType=N'SalesInvoice' AND SourceId=@Source;", connection, transaction))
            {
                command.Parameters.Add("@Source", SqlDbType.NVarChar, 50).Value = sourceId;
                using (var reader = command.ExecuteReader())
                {
                    if (!reader.Read()) throw new InvalidOperationException("قيد البيع الأصلي مطلوب قبل الإلغاء.");
                    string currency = NormalizeSalesCurrency(Convert.ToString(reader[0]));
                    if (reader.Read()) throw new InvalidOperationException("أكثر من قيد أصلي لنفس الفاتورة؛ أوقف الإلغاء وراجع البيانات.");
                    return currency;
                }
            }
        }
        private static string ReadLegacySalesUnitCurrency(SqlConnection connection, SqlTransaction transaction, string sourceId)
        {
            using (var schema = new SqlCommand(@"SELECT CASE WHEN OBJECT_ID(N'dbo.LegacySalesInvoiceHeadersTb',N'U') IS NOT NULL
AND OBJECT_ID(N'dbo.LegacySalesInvoiceLinesTb',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.InvoiceUnitSnapshotsTb',N'U') IS NOT NULL
AND COL_LENGTH(N'dbo.BillsTb1',N'UnitSnapshotRequired') IS NOT NULL THEN 1 ELSE 0 END", connection, transaction))
                if (Convert.ToInt32(schema.ExecuteScalar()) == 0) return null;
            using (var command = new SqlCommand(@"SELECT CASE WHEN COUNT_BIG(l.BillId)>0
AND COUNT_BIG(l.BillId)=SUM(CASE WHEN b.UnitSnapshotRequired=1 AND u.LineKey IS NOT NULL AND u.MedName=b.MedName
 AND u.BaseQuantity=b.Quantity AND u.UnitPrice=b.Sprice AND u.EnteredQuantity*u.BaseUnitsPerUnit=u.BaseQuantity
 AND NULLIF(LTRIM(RTRIM(u.CurrencyCode)),N'') IS NOT NULL THEN CAST(1 AS bigint) ELSE 0 END)
AND MIN(NULLIF(UPPER(LTRIM(RTRIM(u.CurrencyCode))),N''))=MAX(NULLIF(UPPER(LTRIM(RTRIM(u.CurrencyCode))),N''))
THEN MIN(NULLIF(UPPER(LTRIM(RTRIM(u.CurrencyCode))),N'')) END
FROM dbo.LegacySalesInvoiceHeadersTb h WITH (UPDLOCK,HOLDLOCK)
LEFT JOIN dbo.LegacySalesInvoiceLinesTb l WITH (UPDLOCK,HOLDLOCK) ON l.InvoiceReference=h.InvoiceReference
LEFT JOIN dbo.BillsTb1 b WITH (UPDLOCK,HOLDLOCK) ON b.BillId=l.BillId
LEFT JOIN dbo.InvoiceUnitSnapshotsTb u WITH (UPDLOCK,HOLDLOCK) ON u.DocumentKind=N'Sale' AND u.LineKey=b.BillId
WHERE h.InvoiceReference=@Source;", connection, transaction))
            {
                command.Parameters.Add("@Source", SqlDbType.NVarChar, 100).Value = sourceId;
                return NormalizeSalesCurrency(Convert.ToString(command.ExecuteScalar()));
            }
        }

        public static int GetJournalEntryIdBySource(SqlConnection connection, SqlTransaction transaction, string sourceType, string sourceId)
        { return GetJournalEntryIdBySourceAsync(connection,transaction,sourceType,sourceId,CancellationToken.None).GetAwaiter().GetResult(); }
        public static async Task<int> GetJournalEntryIdBySourceAsync(SqlConnection connection, SqlTransaction transaction, string sourceType, string sourceId, CancellationToken token)
        {
            using (SqlCommand command = new SqlCommand(@"
SELECT TOP 1 JournalEntryId
FROM dbo.JournalEntriesTb WITH (UPDLOCK, HOLDLOCK)
WHERE SourceType = @SourceType AND SourceId = @SourceId", connection, transaction))
            {
                command.Parameters.Add("@SourceType", SqlDbType.NVarChar, 50).Value = sourceType;
                command.Parameters.Add("@SourceId", SqlDbType.NVarChar, 50).Value = sourceId;
                object result = await command.ExecuteScalarAsync(token).ConfigureAwait(false);
                return result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);
            }
        }

        private static async Task EnsureAccountExistsAsync(SqlConnection connection, SqlTransaction transaction, string accountCode, CancellationToken token)
        {
            using (SqlCommand command = new SqlCommand("SELECT COUNT(1) FROM dbo.AccountsTb WHERE AccountCode = @AccountCode AND IsActive = 1", connection, transaction))
            {
                command.Parameters.Add("@AccountCode", SqlDbType.NVarChar, 20).Value = accountCode;
                if (Convert.ToInt32(await command.ExecuteScalarAsync(token).ConfigureAwait(false)) == 0)
                {
                    throw new InvalidOperationException("الحساب غير موجود أو غير مفعل: " + accountCode);
                }
            }
        }

        private static object ToDbValue(string value)
        { return string.IsNullOrWhiteSpace(value) ? (object)DBNull.Value : value.Trim(); }
    }
}
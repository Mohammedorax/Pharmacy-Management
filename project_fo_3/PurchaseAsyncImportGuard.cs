using System;
using System.Data;
using System.Data.SqlClient;
using PharmacyManagement.Application;
namespace project_fo_3 { internal static class PurchaseAsyncImportGuard {
        internal static async System.Threading.Tasks.Task EnsureCurrencyAsync(SqlConnection connection, SqlTransaction transaction, string expectedCode,System.Threading.CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(expectedCode)) return;
            await EnsureCurrencyCoreAsync(connection, transaction, expectedCode,
                "عملة الفاتورة المستوردة لا تطابق عملة النظام الحالية؛ لا يحدث تحويل تلقائي.",cancellationToken).ConfigureAwait(false);
        }

        internal static async System.Threading.Tasks.Task EnsureDraftCurrencyAsync(SqlConnection connection, SqlTransaction transaction, string expectedCode,System.Threading.CancellationToken cancellationToken)
        {
            if (expectedCode == null) return;
            if (string.IsNullOrWhiteSpace(expectedCode) || expectedCode.Trim().Length > 10)
                throw new InvalidOperationException("عملة مسودة البيع غير صالحة. راجع إعدادات العملات.");
            await EnsureCurrencyCoreAsync(connection, transaction, expectedCode,
                "تغيرت عملة النظام منذ إعداد المسودة. افتح فاتورة جديدة بالعملة الصحيحة؛ لا يحدث تحويل تلقائي.",cancellationToken).ConfigureAwait(false);
        }

        private static async System.Threading.Tasks.Task EnsureCurrencyCoreAsync(SqlConnection connection, SqlTransaction transaction, string expectedCode, string mismatchMessage,System.Threading.CancellationToken cancellationToken)
        {
            using (var command = new SqlCommand(@"SELECT TOP (1) CurrencyCode FROM dbo.CurrencySettingsTb WITH (HOLDLOCK)
WHERE IsSystemCurrency=1 ORDER BY CurrencyId;", connection, transaction))
            {
                string actual = Convert.ToString(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))?.Trim() ?? string.Empty;
                if (!string.Equals(actual, expectedCode.Trim(), StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(mismatchMessage);
            }
        }

        internal static async System.Threading.Tasks.Task MarkDraftApprovedAsync(SqlConnection connection, SqlTransaction transaction, Guid? draftId,
            string kind, string sourceSystem, string sourceInvoiceId, string postedReference,System.Threading.CancellationToken cancellationToken)
        {
            if (!draftId.HasValue) return;
            if (draftId.Value == Guid.Empty || string.IsNullOrWhiteSpace(postedReference))
                throw new InvalidOperationException("معرّف المسودة أو مرجع اعتمادها غير صالح.");
            string system, invoice;
            if (!InvoiceImportSourceIdentity.TryNormalize(sourceSystem, sourceInvoiceId, out system, out invoice))
                throw new InvalidOperationException("تعذر اعتماد مسودة دون هوية فاتورة مصدر.");
            using (var command = new SqlCommand(@"UPDATE dbo.InvoiceImportDraftsTb
SET Status=N'Approved',ApprovedReference=@Reference,ApprovedAtUtc=SYSUTCDATETIME()
WHERE DraftId=@Id AND SourceSystem=@System AND InvoiceKind=@Kind
AND SourceInvoiceId=@SourceId AND Status=N'Draft';", connection, transaction))
            {
                command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = draftId.Value;
                command.Parameters.Add("@System", SqlDbType.NVarChar, 100).Value = system;
                command.Parameters.Add("@Kind", SqlDbType.NVarChar, 8).Value = kind;
                command.Parameters.Add("@SourceId", SqlDbType.NVarChar, 100).Value = invoice;
                command.Parameters.Add("@Reference", SqlDbType.NVarChar, 100).Value = postedReference;
                if (await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) != 1)
                    throw new InvalidOperationException("المسودة تغيرت أو اعتُمدت سابقًا؛ أُلغيت العملية المالية كاملة.");
            }
        }

}}

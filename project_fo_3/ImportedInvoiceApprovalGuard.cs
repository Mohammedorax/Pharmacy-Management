using System;
using System.Data;
using System.Data.SqlClient;
using PharmacyManagement.Application;

namespace project_fo_3
{
    internal static class ImportedInvoiceApprovalGuard
    {
        internal static void EnsureCurrency(SqlConnection connection, SqlTransaction transaction, string expectedCode)
        {
            if (string.IsNullOrWhiteSpace(expectedCode)) return;
            EnsureCurrencyCore(connection, transaction, expectedCode,
                "عملة الفاتورة المستوردة لا تطابق عملة النظام الحالية؛ لا يحدث تحويل تلقائي.");
        }

        internal static void EnsureDraftCurrency(SqlConnection connection, SqlTransaction transaction, string expectedCode)
        {
            if (expectedCode == null) return;
            if (string.IsNullOrWhiteSpace(expectedCode) || expectedCode.Trim().Length > 10)
                throw new InvalidOperationException("عملة مسودة البيع غير صالحة. راجع إعدادات العملات.");
            EnsureCurrencyCore(connection, transaction, expectedCode,
                "تغيرت عملة النظام منذ إعداد المسودة. افتح فاتورة جديدة بالعملة الصحيحة؛ لا يحدث تحويل تلقائي.");
        }

        private static void EnsureCurrencyCore(SqlConnection connection, SqlTransaction transaction, string expectedCode, string mismatchMessage)
        {
            using (var command = new SqlCommand(@"SELECT TOP (1) CurrencyCode FROM dbo.CurrencySettingsTb WITH (HOLDLOCK)
WHERE IsSystemCurrency=1 ORDER BY CurrencyId;", connection, transaction))
            {
                string actual = Convert.ToString(command.ExecuteScalar())?.Trim() ?? string.Empty;
                if (!string.Equals(actual, expectedCode.Trim(), StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(mismatchMessage);
            }
        }

        internal static void EnsureSalesTax(SqlConnection connection, SqlTransaction transaction,
            decimal? expectedRate, string priceMode)
        {
            if (!expectedRate.HasValue && string.IsNullOrWhiteSpace(priceMode)) return;
            if (!expectedRate.HasValue || priceMode != "Inclusive")
                throw new InvalidOperationException("يجب تحديد ضريبة البيع المستورد وسعره الشامل صراحة قبل الاعتماد.");
            decimal effective = AccountingService.GetEffectiveTaxRate(connection, transaction);
            if (expectedRate.Value != effective)
                throw new InvalidOperationException("ضريبة الفاتورة المستوردة تختلف عن السياسة الفعالة؛ راجعها قبل الاعتماد دون تبديل قيمتها.");
        }

        internal static void EnsurePurchaseTax(SqlConnection connection, SqlTransaction transaction,
            decimal? expectedRate)
        {
            if (!expectedRate.HasValue) return;
            decimal effective = AccountingService.GetEffectiveTaxRate(connection, transaction);
            if (expectedRate.Value != effective)
                throw new InvalidOperationException("ضريبة الشراء المستورد تختلف عن السياسة الفعالة؛ لم تتغير قيم الفاتورة أو القيد.");
        }

        internal static void MarkDraftApproved(SqlConnection connection, SqlTransaction transaction, Guid? draftId,
            string kind, string sourceSystem, string sourceInvoiceId, string postedReference)
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
                if (command.ExecuteNonQuery() != 1)
                    throw new InvalidOperationException("المسودة تغيرت أو اعتُمدت سابقًا؛ أُلغيت العملية المالية كاملة.");
            }
        }
    }
}

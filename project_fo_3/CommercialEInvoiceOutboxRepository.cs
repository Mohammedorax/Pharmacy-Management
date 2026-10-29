using System;
using System.Data;
using System.Data.SqlClient;

namespace project_fo_3
{
    // يحفظ أثراً محلياً غير موقع داخل المعاملة نفسها. هذا طابور تجهيز فقط وليس اتصالاً
    // حياً مع ZATCA؛ يبقى الإرسال الفعلي مسؤولية مزود معتمد في إصدار لاحق.
    internal static class CommercialEInvoiceOutboxRepository
    {
        public static void QueueLocalSandboxArtifact(
            SqlConnection connection,
            SqlTransaction transaction,
            Guid invoiceId,
            long invoiceNumber,
            CommercialSaleDraft draft,
            InvoiceTotals totals,
            DateTime issueDateUtc)
        {
            if (connection == null || transaction == null) throw new ArgumentNullException("اتصال ومعاملة SQL مطلوبان.");
            if (draft == null) throw new ArgumentNullException(nameof(draft));
            if (draft.EInvoiceKind != EInvoiceDocumentKind.SimplifiedB2C)
                throw new InvalidOperationException("المسار الحالي ينشئ فاتورة مبسطة B2C فقط؛ لا يجوز إنشاء B2B دون اسم المشتري ورقمه الضريبي.");
            QueueLocalSandboxArtifact(
                connection, transaction, invoiceId, invoiceNumber, draft.EInvoiceKind,
                draft.CurrencyCode, draft.InvoiceDiscountPercent, draft.Lines, totals,
                issueDateUtc, null, null);
        }

        // يستعمل إشعار الدائن نفس أثر الفاتورة المحلية، لكن لا يعيد استخدام مسودة بيع
        // أو ضريبة حالية؛ يمرر المتصل القيم المحسوبة من اللقطات المعتمدة فقط.
        public static void QueueLocalSandboxArtifact(
            SqlConnection connection,
            SqlTransaction transaction,
            Guid invoiceId,
            long invoiceNumber,
            EInvoiceDocumentKind kind,
            string currencyCode,
            decimal invoiceDiscountPercent,
            System.Collections.Generic.IReadOnlyList<CommercialInvoiceLine> lines,
            InvoiceTotals totals,
            DateTime issueDateUtc,
            Guid? originalInvoiceId,
            string adjustmentReason,
            System.Collections.Generic.IReadOnlyList<EInvoiceLineSnapshot> exactLines = null)
        {
            if (connection == null || transaction == null) throw new ArgumentNullException("اتصال ومعاملة SQL مطلوبان.");
            if(kind==EInvoiceDocumentKind.CreditNote)
            {
                string blocked=CommercialSalesReturnRepository.ReadElectronicBlock(connection,transaction,invoiceId);
                if(!string.IsNullOrWhiteSpace(blocked))throw new InvalidOperationException(blocked);
            }
            if (lines == null || lines.Count == 0 || totals == null)
                throw new InvalidOperationException("لا يمكن إنشاء أثر إلكتروني بلا بنود وإجماليات.");
            if (kind == EInvoiceDocumentKind.CreditNote && (!originalInvoiceId.HasValue || originalInvoiceId.Value == Guid.Empty || string.IsNullOrWhiteSpace(adjustmentReason)))
                throw new InvalidOperationException("الإشعار الدائن يتطلب الفاتورة الأصلية وسبب التعديل.");
            SaudiCommercialFeatureSettings.EnsureModeAtLeast(connection, transaction, SaudiCommercialFeatureSettings.EInvoice, SaudiFeatureMode.Simulator);
            CommercialEInvoiceConfiguration configuration = LoadConfiguration(connection, transaction);
            if (!configuration.LocalSandboxEnabled)
                throw new InvalidOperationException("لا يمكن اعتماد البيع التجاري قبل تفعيل وإكمال إعداد الأثر الإلكتروني المحلي في قاعدة الاختبار.");

            long previousCounter = 0;
            string previousHash = string.Empty;
            using (SqlCommand command = new SqlCommand(@"
SELECT TOP (1) CounterValue, DocumentHash
FROM dbo.EInvoiceOutboxTb WITH (UPDLOCK, HOLDLOCK)
ORDER BY CounterValue DESC;", connection, transaction))
            using (SqlDataReader reader = command.ExecuteReader())
            {
                if (reader.Read())
                {
                    previousCounter = reader.GetInt64(0);
                    previousHash = reader.GetString(1);
                }
            }

            EInvoiceArtifact artifact = new LocalZatcaSandboxProvider().Create(new EInvoiceRequest
            {
                InvoiceId = invoiceId,
                InvoiceNumber = invoiceNumber,
                Kind = kind,
                SellerName = configuration.SellerName,
                SellerVatNumber = configuration.SellerVatNumber,
                IssueDateUtc = issueDateUtc.ToUniversalTime(),
                CurrencyCode = currencyCode,
                InvoiceDiscountPercent = invoiceDiscountPercent,
                Totals = totals,
                Lines = lines,
                ExactLines = exactLines,
                OriginalInvoiceId = originalInvoiceId,
                AdjustmentReason = adjustmentReason
            }, previousCounter, previousHash, previousCounter + 1, previousHash);

            using (SqlCommand command = new SqlCommand(@"
INSERT INTO dbo.EInvoiceOutboxTb
(OutboxId, InvoiceId, Uuid, CounterValue, PreviousHash, DocumentHash, XmlPayload, QrTlvBase64, Status, AttemptCount)
VALUES
(@OutboxId, @InvoiceId, @Uuid, @CounterValue, @PreviousHash, @DocumentHash, @XmlPayload, @QrTlvBase64, @Status, 0);", connection, transaction))
            {
                command.Parameters.Add("@OutboxId", SqlDbType.UniqueIdentifier).Value = Guid.NewGuid();
                command.Parameters.Add("@InvoiceId", SqlDbType.UniqueIdentifier).Value = invoiceId;
                command.Parameters.Add("@Uuid", SqlDbType.UniqueIdentifier).Value = Guid.Parse(artifact.Uuid);
                command.Parameters.Add("@CounterValue", SqlDbType.BigInt).Value = artifact.Counter;
                command.Parameters.Add("@PreviousHash", SqlDbType.NVarChar, 200).Value = string.IsNullOrWhiteSpace(artifact.PreviousHash) ? (object)DBNull.Value : artifact.PreviousHash;
                command.Parameters.Add("@DocumentHash", SqlDbType.NVarChar, 200).Value = artifact.DocumentHash;
                command.Parameters.Add("@XmlPayload", SqlDbType.NVarChar).Value = artifact.Xml;
                command.Parameters.Add("@QrTlvBase64", SqlDbType.NVarChar).Value = artifact.QrTlvBase64;
                command.Parameters.Add("@Status", SqlDbType.NVarChar, 30).Value = artifact.Status.ToString();
                command.ExecuteNonQuery();
            }
        }

        private static CommercialEInvoiceConfiguration LoadConfiguration(SqlConnection connection, SqlTransaction transaction)
        {
            using (SqlCommand command = new SqlCommand(@"
SELECT TOP (1) SellerName, SellerVatNumber, LocalSandboxEnabled
FROM dbo.CommercialEInvoiceSettingsTb WITH (UPDLOCK, HOLDLOCK)
WHERE SettingsId = 1;", connection, transaction))
            using (SqlDataReader reader = command.ExecuteReader())
            {
                if (!reader.Read()) throw new InvalidOperationException("إعداد الأثر الإلكتروني المحلي غير موجود.");
                CommercialEInvoiceConfiguration configuration = new CommercialEInvoiceConfiguration
                {
                    SellerName = reader.IsDBNull(0) ? string.Empty : reader.GetString(0).Trim(),
                    SellerVatNumber = reader.IsDBNull(1) ? string.Empty : reader.GetString(1).Trim(),
                    LocalSandboxEnabled = reader.GetBoolean(2)
                };
                if (configuration.LocalSandboxEnabled && (string.IsNullOrWhiteSpace(configuration.SellerName) || string.IsNullOrWhiteSpace(configuration.SellerVatNumber)))
                    throw new InvalidOperationException("اسم البائع والرقم الضريبي مطلوبان قبل إنشاء الأثر الإلكتروني المحلي.");
                return configuration;
            }
        }

        private sealed class CommercialEInvoiceConfiguration
        {
            public string SellerName { get; set; }
            public string SellerVatNumber { get; set; }
            public bool LocalSandboxEnabled { get; set; }
        }
    }
}

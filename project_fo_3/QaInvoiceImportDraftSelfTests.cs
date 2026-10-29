using System;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using PharmacyManagement.Application;
using PharmacyManagement.Infrastructure.SqlServer;

namespace project_fo_3
{
    internal static class QaInvoiceImportDraftSelfTests
    {
        internal static void Run(string backupPath)
        {
            if (!RuntimeSettingsStore.IsBuildConnectionProfileForced || Database.GetConnectionProfile().DatabaseName != "PharmacyManagement_qa")
                throw new InvalidOperationException("اختبار مسودات الفواتير يتطلب حزمة QA مقيدة.");
            string system = "QA-DRAFT-" + Guid.NewGuid().ToString("N");
            long stockBefore, journalBefore, auditBefore;
            using (SqlConnection connection = Database.CreateConnection())
            {
                connection.Open();
                QaTaxPolicySelfTests.VerifyBackup(connection, backupPath);
                stockBefore = Scalar(connection, "SELECT COALESCE(SUM(CONVERT(BIGINT,MedQty)),0) FROM dbo.MedicineTb1;");
                journalBefore = Scalar(connection, "SELECT COUNT_BIG(*) FROM dbo.JournalEntriesTb;");
                auditBefore = Scalar(connection, "SELECT COUNT_BIG(*) FROM dbo.AuditLogTb;");
            }
            var repository = new SqlInvoiceImportDraftRepository(Database.CreateConnection);
            var service = new InvoiceImportDraftService(repository, actor => actor.UserRole == "Manager");
            var manager = new OperationActor { UserName = "QA draft verifier", UserRole = "Manager" };
            const decimal exactPrice = 12.345678901234567890123456m;
            try
            {
                var rows = new[] {
                    Row("SAME-001", ImportedInvoiceKind.Sale, 1, exactPrice),
                    Row("SAME-001", ImportedInvoiceKind.Purchase, 1, exactPrice)
                };
                var candidates = InvoiceImportValidation.Validate(InvoiceImportMode.Draft, system, rows);
                var ids = service.SaveBatch(candidates, manager);
                if (ids.Count != 2 || ids[0] == ids[1]) throw new InvalidOperationException("لم تُحفظ مسودتا البيع والشراء مستقلتين.");
                var recent = service.ListRecent(manager);
                if (!recent.Any(item => item.DraftId == ids[0]) || !recent.Any(item => item.DraftId == ids[1]))
                    throw new InvalidOperationException("المسودات المحفوظة لا تظهر في قائمة المراجعة.");
                foreach (Guid id in ids)
                {
                    InvoiceImportDraftRecord loaded = service.Get(id, manager);
                    if (loaded == null || loaded.Status != "Draft" || loaded.ApprovedReference != null ||
                        loaded.Candidate.Lines.Count != 1 || loaded.Candidate.Lines[0].UnitPrice != exactPrice ||
                        loaded.Candidate.Lines[0].Barcode != "000123" ||
                        loaded.Candidate.PartyReference != "C-001" || loaded.Candidate.TaxRatePercent != 0m ||
                        loaded.Candidate.PaymentMethod != (loaded.Candidate.Kind == ImportedInvoiceKind.Sale ? "Cash" : "Payable") ||
                        loaded.Candidate.Lines[0].BatchNumber != "B-01" ||
                        loaded.Candidate.Lines[0].ExpiryDate != new DateTime(2027, 1, 1))
                        throw new InvalidOperationException("قراءة مسودة QA غيّرت السعر أو الباركود أو بيانات الاعتماد أو حالة المسودة.");
                }
                bool duplicateRejected = false;
                try { service.SaveBatch(candidates, manager); }
                catch (InvalidOperationException error) when (error.Message.Contains("مسودة محفوظة")) { duplicateRejected = true; }
                if (!duplicateRejected) throw new InvalidOperationException("تكرار مسودة مصدر محفوظة لم يُرفض.");
                var mixed = InvoiceImportValidation.Validate(InvoiceImportMode.Draft, system, new[] {
                    Row("NEW-002", ImportedInvoiceKind.Sale, 1, exactPrice),
                    Row("SAME-001", ImportedInvoiceKind.Sale, 1, exactPrice)
                });
                duplicateRejected = false;
                try { service.SaveBatch(mixed, manager); }
                catch (InvalidOperationException error) when (error.Message.Contains("مسودة محفوظة")) { duplicateRejected = true; }
                if (!duplicateRejected || Count(system, "NEW-002") != 0)
                    throw new InvalidOperationException("فشل تراجع كامل دفعة المسودات عند تكرار أحد المصادر.");
                if (Count(system, null) != 2) throw new InvalidOperationException("تكرار المسودات غيّر العدد المحفوظ.");
                Console.WriteLine("PASS: QA draft save/read, exact decimal, sale/purchase namespace, duplicate and batch rollback.");
            }
            finally
            {
                using (SqlConnection connection = Database.CreateConnection())
                {
                    connection.Open();
                    using (SqlTransaction transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            using (var lines = new SqlCommand(@"DELETE l FROM dbo.InvoiceImportDraftLinesTb l
JOIN dbo.InvoiceImportDraftsTb d ON d.DraftId=l.DraftId WHERE d.SourceSystem=@System;", connection, transaction))
                            {
                                lines.Parameters.Add("@System", SqlDbType.NVarChar, 100).Value = system;
                                lines.ExecuteNonQuery();
                            }
                            using (var headers = new SqlCommand("DELETE FROM dbo.InvoiceImportDraftsTb WHERE SourceSystem=@System;", connection, transaction))
                            {
                                headers.Parameters.Add("@System", SqlDbType.NVarChar, 100).Value = system;
                                headers.ExecuteNonQuery();
                            }
                            transaction.Commit();
                        }
                        catch { transaction.Rollback(); throw; }
                    }
                    if (Count(system, null) != 0 ||
                        Scalar(connection, "SELECT COALESCE(SUM(CONVERT(BIGINT,MedQty)),0) FROM dbo.MedicineTb1;") != stockBefore ||
                        Scalar(connection, "SELECT COUNT_BIG(*) FROM dbo.JournalEntriesTb;") != journalBefore ||
                        Scalar(connection, "SELECT COUNT_BIG(*) FROM dbo.AuditLogTb;") != auditBefore)
                        throw new InvalidOperationException("مسودات QA تركت أثرًا ماليًا أو سجلًا بعد التنظيف.");
                }
            }
            Console.WriteLine("PASS: QA draft cleanup; retained draft/stock/journal/audit changes=0.");
        }

        private static InvoiceImportRow Row(string id, ImportedInvoiceKind kind, int line, decimal price)
        {
            return new InvoiceImportRow { SourceInvoiceId = id, Kind = kind, LineNumber = line,
                InvoiceDate = new DateTime(2026, 9, 29), PartyName = "عميل أو مورد QA", CurrencyCode = "YER",
                MedicineName = "باراسيتامول 500 mg", Barcode = "000123", Quantity = 1.25m, UnitPrice = price,
                PartyReference = "C-001", PartyPhone = "000000000", InvoiceDiscountPercent = 2.5m,
                TaxRatePercent = 0m, PriceTaxMode = "Inclusive", Notes = "QA review",
                PaymentMethod = kind == ImportedInvoiceKind.Sale ? "Cash" : "Payable",
                ProductionDate = new DateTime(2025, 1, 1), ExpiryDate = new DateTime(2027, 1, 1),
                BatchNumber = "B-01", Gtin = "01234567890128", SerialNumber = "SER-01" };
        }
        private static long Scalar(SqlConnection connection, string sql)
        { using (var command = new SqlCommand(sql, connection)) return Convert.ToInt64(command.ExecuteScalar()); }
        private static int Count(string system, string source)
        {
            using (SqlConnection connection = Database.CreateConnection())
            using (var command = new SqlCommand(@"SELECT COUNT(*) FROM dbo.InvoiceImportDraftsTb
WHERE SourceSystem=@System AND (@Source IS NULL OR SourceInvoiceId=@Source);", connection))
            {
                command.Parameters.Add("@System", SqlDbType.NVarChar, 100).Value = system;
                command.Parameters.Add("@Source", SqlDbType.NVarChar, 100).Value = (object)source ?? DBNull.Value;
                connection.Open();
                return Convert.ToInt32(command.ExecuteScalar());
            }
        }
    }
}

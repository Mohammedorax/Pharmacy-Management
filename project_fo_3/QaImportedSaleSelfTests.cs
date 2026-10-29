using System;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using PharmacyManagement.Application;

namespace project_fo_3
{
    internal static class QaImportedSaleSelfTests
    {
        internal static void Run(string backupPath)
        {
            if (!RuntimeSettingsStore.IsBuildConnectionProfileForced || Database.GetConnectionProfile().DatabaseName != "PharmacyManagement_qa")
                throw new InvalidOperationException("اختبار الفاتورة المستوردة يتطلب حزمة QA مقيدة.");
            Console.WriteLine("QA imported sale: profile accepted.");
            string medicine;
            int price, originalStock;
            string currency;
            decimal tax;
            using (SqlConnection connection = Database.CreateConnection())
            {
                connection.Open();
                QaTaxPolicySelfTests.VerifyBackup(connection, backupPath);
                using (var currencyCommand = new SqlCommand(@"SELECT TOP (1) CurrencyCode FROM dbo.CurrencySettingsTb
WHERE IsSystemCurrency=1 ORDER BY CurrencyId;", connection))
                    currency = Convert.ToString(currencyCommand.ExecuteScalar());
                tax = AccountingService.GetEffectiveTaxRate(connection, null);
                if (string.IsNullOrWhiteSpace(currency)) throw new InvalidOperationException("عملة النظام غير محددة في QA.");
                Console.WriteLine("QA imported sale: backup verified.");
                using (var command = new SqlCommand("SELECT TOP(1) MedName,Sprice,MedQty FROM dbo.MedicineTb1 WHERE MedQty>1 AND Sprice>0 AND ExpDate>GETDATE() ORDER BY MedName", connection))
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (!reader.Read()) throw new InvalidOperationException("لا يوجد صنف مناسب لاختبار بيع QA.");
                    medicine = Convert.ToString(reader[0]); price = Convert.ToInt32(reader[1]); originalStock = Convert.ToInt32(reader[2]);
                    Console.WriteLine("QA imported sale: stock fixture found.");
                }
            }
            string sourceSystem = "QA-ImportedSale";
            string sourceId = Guid.NewGuid().ToString("N");
            string postedReference = null;
            bool cancelled = false;
            SessionContext.Start("QA import verification", "Manager");
            try
            {
                InvoiceImportCandidate candidate = ReadInvoiceFromXlsx(sourceSystem, sourceId, medicine,
                    price, currency, tax, InvoiceImportMode.ImportAndApprove, "QA approved import mapping test");
                Console.WriteLine("QA imported sale: issuing.");
                InvoiceImportApprovalResult issued = ApplicationServices.InvoiceImportApprovals.Approve(candidate,
                    new OperationActor { UserName = SessionContext.CurrentUser, UserRole = SessionContext.CurrentRole });
                Console.WriteLine("QA imported sale: issued.");
                postedReference = issued.PostedReference;
                Require(!string.IsNullOrWhiteSpace(postedReference) && Stock(medicine) == originalStock - 1,
                    "البيع المستورد لم يخصم وحدة واحدة.");
                Require(SourceCount(sourceSystem, sourceId) == 1, "هوية المصدر لم تُحفظ مع البيع.");
                bool rejected = false;
                try { ApplicationServices.InvoiceImportApprovals.Approve(candidate,
                    new OperationActor { UserName = SessionContext.CurrentUser, UserRole = SessionContext.CurrentRole }); }
                catch (InvalidOperationException error) when (error.Message.Contains("معتمدة سابقًا")) { rejected = true; }
                Require(rejected && SourceCount(sourceSystem, sourceId) == 1 && Stock(medicine) == originalStock - 1,
                    "تكرار المصدر غيّر المخزون أو أنشأ فاتورة ثانية.");
                VerifyJournal(postedReference, false);
                Cancel(postedReference, "QA imported sale reversal");
                cancelled = true;
                Require(Stock(medicine) == originalStock, "إلغاء البيع المستورد لم يُعد الرصيد.");
                Require(SourceCount(sourceSystem, sourceId) == 1, "إلغاء الفاتورة أزال مانع تكرار الاستيراد.");
                VerifyJournal(postedReference, true);
                Console.WriteLine("Retained direct QA invoice=" + postedReference + "; source=" + sourceSystem + "/" + sourceId);
                Console.WriteLine("PASS: QA imported sale issued once, duplicate rolled back, cancellation restored stock; source key retained.");
                VerifyDraftApproval(medicine, price, currency, tax, originalStock);
            }
            finally
            {
                try { if (postedReference != null && !cancelled) Cancel(postedReference, "QA imported sale cleanup"); }
                finally { SessionContext.Clear(); }
            }
        }
        private static void VerifyDraftApproval(string medicine, int price, string currency, decimal tax, int originalStock)
        {
            string system = "QA-ImportedDraft-" + Guid.NewGuid().ToString("N");
            string sourceId = "D-" + Guid.NewGuid().ToString("N");
            Guid? draftId = null;
            string postedReference = null;
            bool cancelled = false;
            var actor = new OperationActor { UserName = SessionContext.CurrentUser, UserRole = SessionContext.CurrentRole };
            try
            {
                InvoiceImportCandidate draft = ReadInvoiceFromXlsx(system, sourceId, medicine,
                    price, currency, tax, InvoiceImportMode.Draft, "QA draft approval transaction test");
                draftId = ApplicationServices.InvoiceImportDrafts.SaveBatch(new[] { draft }, actor)[0];
                Require(ApplicationServices.InvoiceImportDrafts.Get(draftId.Value, actor).Status == "Draft" &&
                    Stock(medicine) == originalStock, "حفظ المسودة غيّر الرصيد أو حالتها قبل الاعتماد.");
                InvoiceImportApprovalResult result = ApplicationServices.InvoiceImportApprovals.ApproveDraft(draftId.Value, actor);
                postedReference = result.PostedReference;
                Require(result.DraftId == draftId && SourceCount(system, sourceId) == 1 &&
                    Stock(medicine) == originalStock - 1, "اعتماد المسودة لم يسجل الفاتورة والمخزون والمصدر معًا.");
                InvoiceImportDraftRecord postedDraft = ApplicationServices.InvoiceImportDrafts.Get(draftId.Value, actor);
                Require(postedDraft.Status == "Approved" && postedDraft.ApprovedReference == postedReference,
                    "لم تنتقل المسودة إلى معتمدة بنفس مرجع الفاتورة.");
                bool rejected = false;
                try { ApplicationServices.InvoiceImportApprovals.ApproveDraft(draftId.Value, actor); }
                catch (InvalidOperationException) { rejected = true; }
                Require(rejected && SourceCount(system, sourceId) == 1 && Stock(medicine) == originalStock - 1,
                    "إعادة اعتماد المسودة غيّرت المخزون أو أنشأت مصدرًا آخر.");
                VerifyJournal(postedReference, false);
                Cancel(postedReference, "QA imported draft reversal"); cancelled = true;
                Require(Stock(medicine) == originalStock, "إلغاء الفاتورة المعتمدة من مسودة لم يعد المخزون.");
                VerifyJournal(postedReference, true);
                Console.WriteLine("Retained draft QA invoice=" + postedReference + "; source=" + system + "/" + sourceId);
                Console.WriteLine("PASS: QA saved draft approved atomically, duplicate denied, cancellation restored stock.");
            }
            finally
            {
                try
                {
                    if (!cancelled)
                    {
                        string reference = postedReference ?? PostedReference(system, sourceId);
                        if (!string.IsNullOrWhiteSpace(reference)) Cancel(reference, "QA imported draft cleanup");
                    }
                }
                finally { if (draftId.HasValue) DeleteQaDraft(draftId.Value); }
            }
        }
        private static InvoiceImportCandidate ReadInvoiceFromXlsx(string sourceSystem, string sourceId,
            string medicine, int price, string currency, decimal tax, InvoiceImportMode mode, string notes)
        {
            using (DataTable workbook = InvoiceImportWorkbook.CreateTemplate(false))
            {
                workbook.Rows.Add("بيع", sourceId, "1", DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    "عميل QA نقدي", currency, medicine, "000123", "1", price.ToString(CultureInfo.InvariantCulture),
                    "0", "000000000", "0", tax.ToString(CultureInfo.InvariantCulture), "شامل", notes,
                    "", "", "", "", "", "نقدي");
                string path = Path.Combine(Path.GetTempPath(), "pharmacy-qa-invoice-import-" + Guid.NewGuid().ToString("N") + ".xlsx");
                try
                {
                    using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                        ExcelWorkbookWriter.Write(workbook, workbook.TableName, output);
                    var candidates = InvoiceImportWorkbook.Read(path, mode, sourceSystem);
                    if (candidates.Count != 1 || candidates[0].SourceInvoiceId != sourceId ||
                        candidates[0].Lines.Count != 1 || candidates[0].Lines[0].MedicineName != medicine ||
                        candidates[0].Lines[0].Barcode != "000123")
                        throw new InvalidOperationException("قراءة ملف XLSX الفعلي غيّرت معرف الفاتورة أو بيانات الصنف.");
                    return candidates[0];
                }
                finally { if (File.Exists(path)) File.Delete(path); }
            }
        }
        private static string PostedReference(string system, string sourceId)
        {
            using (SqlConnection connection = Database.CreateConnection())
            using (var command = new SqlCommand(@"SELECT PostedReference FROM dbo.ImportedInvoiceSourceTb
WHERE SourceSystem=@System AND InvoiceKind=N'Sale' AND SourceInvoiceId=@SourceId;", connection))
            {
                command.Parameters.Add("@System", SqlDbType.NVarChar, 100).Value = system;
                command.Parameters.Add("@SourceId", SqlDbType.NVarChar, 100).Value = sourceId;
                connection.Open(); return Convert.ToString(command.ExecuteScalar());
            }
        }
        private static void DeleteQaDraft(Guid id)
        {
            using (SqlConnection connection = Database.CreateConnection())
            {
                connection.Open();
                using (SqlTransaction transaction = connection.BeginTransaction())
                {
                    try
                    {
                        using (var lines = new SqlCommand("DELETE dbo.InvoiceImportDraftLinesTb WHERE DraftId=@Id;", connection, transaction))
                        { lines.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = id; lines.ExecuteNonQuery(); }
                        using (var header = new SqlCommand("DELETE dbo.InvoiceImportDraftsTb WHERE DraftId=@Id;", connection, transaction))
                        { header.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = id; header.ExecuteNonQuery(); }
                        transaction.Commit();
                    }
                    catch { transaction.Rollback(); throw; }
                }
            }
        }
        private static void VerifyJournal(string reference, bool cancelled)
        {
            using (SqlConnection connection = Database.CreateConnection())
            using (var command = new SqlCommand(@"SELECT
(SELECT COUNT(*) FROM dbo.JournalEntriesTb WHERE SourceId=@Reference AND SourceType=N'SalesInvoice'),
(SELECT COUNT(*) FROM dbo.JournalEntriesTb WHERE SourceId=@Reference AND SourceType=N'SalesInvoiceCancellation'),
(SELECT COUNT(*) FROM dbo.JournalEntryLinesTb l JOIN dbo.JournalEntriesTb e ON e.JournalEntryId=l.JournalEntryId
 WHERE e.SourceId=@Reference AND e.SourceType=N'SalesInvoice'),
(SELECT COUNT(*) FROM (SELECT e.JournalEntryId FROM dbo.JournalEntryLinesTb l
 JOIN dbo.JournalEntriesTb e ON e.JournalEntryId=l.JournalEntryId
 WHERE e.SourceId=@Reference AND e.SourceType IN (N'SalesInvoice',N'SalesInvoiceCancellation')
 GROUP BY e.JournalEntryId HAVING SUM(l.Debit-l.Credit)<>0) entries),
(SELECT COUNT(*) FROM (SELECT l.AccountCode FROM dbo.JournalEntryLinesTb l
 JOIN dbo.JournalEntriesTb e ON e.JournalEntryId=l.JournalEntryId
 WHERE e.SourceId=@Reference AND e.SourceType IN (N'SalesInvoice',N'SalesInvoiceCancellation')
 GROUP BY l.AccountCode HAVING SUM(l.Debit-l.Credit)<>0) accounts);", connection))
            {
                command.Parameters.Add("@Reference", SqlDbType.NVarChar, 200).Value = reference;
                connection.Open();
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    Require(reader.Read() && reader.GetInt32(0) == 1 && reader.GetInt32(1) == (cancelled ? 1 : 0) &&
                        reader.GetInt32(2) >= 2 && reader.GetInt32(3) == 0 && (!cancelled || reader.GetInt32(4) == 0),
                        "قيود الاستيراد مفقودة أو غير متزنة أو لم يعكس الإلغاء أرصدة كل حساب.");
                }
            }
            Console.WriteLine(cancelled ? "PASS: original and cancellation reconcile to zero for every account." :
                "PASS: issued invoice has exactly one balanced journal with at least two lines.");
        }

        private static void Cancel(string reference, string reason)
        {
            ApplicationServices.SalesInvoiceCancellations.Cancel(new SalesInvoiceCancellationRequest
            { InvoiceReference = reference, Reason = reason, ActorName = SessionContext.CurrentUser });
        }
        private static int Stock(string medicine)
        {
            using (SqlConnection connection = Database.CreateConnection())
            using (var command = new SqlCommand("SELECT MedQty FROM dbo.MedicineTb1 WHERE MedName=@Medicine", connection))
            {
                command.Parameters.Add("@Medicine", SqlDbType.NVarChar, 200).Value = medicine;
                connection.Open();
                return Convert.ToInt32(command.ExecuteScalar());
            }
        }
        private static int SourceCount(string system, string invoice)
        {
            using (SqlConnection connection = Database.CreateConnection())
            using (var command = new SqlCommand("SELECT COUNT(*) FROM dbo.ImportedInvoiceSourceTb WHERE SourceSystem=@System AND InvoiceKind=N'Sale' AND SourceInvoiceId=@Invoice", connection))
            {
                command.Parameters.Add("@System", SqlDbType.NVarChar, 100).Value = system;
                command.Parameters.Add("@Invoice", SqlDbType.NVarChar, 100).Value = invoice;
                connection.Open();
                return Convert.ToInt32(command.ExecuteScalar());
            }
        }
        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}

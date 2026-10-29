using System;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using PharmacyManagement.Application;

namespace project_fo_3
{
    internal static class QaCashBoxSaleSelfTests
    {
        internal static void Run(string backupPath, string temporaryDatabaseOwnerToken)
        {
            string expectedDatabase = QaSalesInvoiceSelfTests.RequireOwnedTemporaryQa(temporaryDatabaseOwnerToken);
            string medicine;
            int price, stock;
            using (SqlConnection connection = Database.CreateConnection())
            {
                connection.Open();
                Require(connection.Database == expectedDatabase, "Actual connection is not the owned temporary QA clone.");
                QaTaxPolicySelfTests.VerifyBackup(connection, backupPath);
                Require(!CommercialSchemaInstaller.IsFullCommercialMode(connection, null), "This roundtrip targets legacy mapped sales only.");
                using (var schema = new SqlCommand("SELECT COL_LENGTH(N'dbo.CashBoxJournalLinksTb',N'ReversesJournalEntryLineId');", connection))
                    Require(schema.ExecuteScalar() != DBNull.Value, "Migration 0022 is required before this test.");
                using (var command = new SqlCommand("SELECT TOP(1) MedName,Sprice,MedQty FROM dbo.MedicineTb1 WHERE MedQty>1 AND Sprice>0 AND ExpDate>GETDATE() ORDER BY MedName;", connection))
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    Require(reader.Read(), "No eligible QA stock.");
                    medicine = reader.GetString(0); price = Convert.ToInt32(reader[1]); stock = Convert.ToInt32(reader[2]);
                }
            }
            string nonce = "QA-CASH-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpperInvariant();
            string cashierName = "Cashbox QA " + nonce;
            Guid originalBox = Guid.NewGuid(), laterBox = Guid.NewGuid();
            var actor = new OperationActor { UserName = "QA cashbox sale verification", UserRole = "Manager" };
            bool employeeCreated = false, boxesCreated = false, cancelled = false;
            string invoice = null;
            SessionContext.Start(actor.UserName, actor.UserRole);
            try
            {
                using (SqlConnection connection = Database.CreateConnection())
                using (var command = new SqlCommand(@"INSERT dbo.EmployeeTb1(EmpId,EmpName,EmpSal,EmpAge,EmpPhone,EmpPassword,EmpRole)
VALUES(@Id,@Name,0,25,N'000',@RandomPassword,N'Cashier');", connection))
                {
                    command.Parameters.Add("@Id", SqlDbType.NVarChar, 50).Value = nonce;
                    command.Parameters.Add("@Name", SqlDbType.NVarChar, 200).Value = cashierName;
                    // This random fixture credential is never printed or supplied as a login.
                    command.Parameters.Add("@RandomPassword", SqlDbType.NVarChar, 50).Value = Guid.NewGuid().ToString("N");
                    connection.Open(); command.ExecuteNonQuery(); employeeCreated = true;
                }
                ApplicationServices.CashBoxes.SaveBatch(new[] {
                    new CashBoxInput { CashBoxId=originalBox,Code=nonce,Name="صندوق QA الأصلي",IsActive=true,EmployeeId=nonce },
                    new CashBoxInput { CashBoxId=laterBox,Code=nonce+"-NEW",Name="صندوق QA اللاحق",IsActive=true }
                }, actor);
                boxesCreated = true;
                long journalsBefore = Convert.ToInt64(Scalar("SELECT COUNT_BIG(*) FROM dbo.JournalEntriesTb;", null));
                var request = new SalesInvoiceIssueRequest {
                    CustomerName="عميل QA نقدي",PhoneNumber="000000000",CustomerAddress="",
                    EmployeeName="Incorrect cashier",CashierEmployeeId=nonce,InvoiceDate=DateTime.Now,
                    DisplayedGrossTotal=price,DisplayedNetTotal=price,DiscountPercent=0m,
                    Notes="QA cashbox sale roundtrip; retain audit trail",
                    Lines=new[] { new SalesInvoiceLineRequest { MedicineName=medicine,DisplayedSellingPrice=price,Quantity=1 } }
                };
                bool mismatchRejected = false;
                try { ApplicationServices.SalesInvoices.Issue(request); }
                catch (InvalidOperationException error) when (error.Message.Contains("اسم الكاشير")) { mismatchRejected = true; }
                Require(mismatchRejected && Stock(medicine)==stock &&
                    Convert.ToInt64(Scalar("SELECT COUNT_BIG(*) FROM dbo.JournalEntriesTb;", null))==journalsBefore,
                    "Cashier mismatch did not roll back the real sale and journal.");
                request.EmployeeName = cashierName;
                SalesInvoiceIssueResult issued = ApplicationServices.SalesInvoices.Issue(request);
                invoice = issued.AccountingSourceId;
                Require(Stock(medicine)==stock-1 && issued.CashCollected==price, "Real sale stock or cash amount differs.");
                Require(Scalar(@"SELECT COUNT(*) FROM dbo.CashBoxJournalLinksTb k
JOIN dbo.JournalEntryLinesTb l ON l.JournalEntryLineId=k.JournalEntryLineId
JOIN dbo.JournalEntriesTb e ON e.JournalEntryId=l.JournalEntryId
WHERE e.SourceType=N'SalesInvoice' AND e.SourceId=@Value AND k.CashBoxId=@Box;", invoice, originalBox)==1m,
                    "Real sale did not link its cash to the cashier's box.");
                ApplicationServices.CashBoxes.SaveBatch(new[] {
                    new CashBoxInput { CashBoxId=originalBox,Code=nonce,Name="صندوق QA الأصلي",IsActive=false },
                    new CashBoxInput { CashBoxId=laterBox,Code=nonce+"-NEW",Name="صندوق QA اللاحق",IsActive=true,EmployeeId=nonce }
                }, actor);
                SalesInvoiceCancellationOutcome reversal = ApplicationServices.SalesInvoiceCancellations.Cancel(
                    new SalesInvoiceCancellationRequest { InvoiceReference=invoice,ActorName=actor.UserName,Reason="QA cashbox reversal after reassignment" });
                cancelled = true;
                Require(reversal.RestoredQuantity==1 && reversal.TotalAmount==price && Stock(medicine)==stock,
                    "Real cancellation did not restore stock and original cash.");
                Require(Scalar(@"SELECT COUNT(*) FROM dbo.CashBoxJournalLinksTb k
JOIN dbo.JournalEntryLinesTb l ON l.JournalEntryLineId=k.JournalEntryLineId
JOIN dbo.JournalEntriesTb e ON e.JournalEntryId=l.JournalEntryId
WHERE e.SourceType=N'SalesInvoiceCancellation' AND e.SourceId=@Value AND k.CashBoxId=@Box
 AND k.ReversesJournalEntryLineId IS NOT NULL;", invoice, originalBox)==1m,
                    "Real cancellation lost the original box attribution.");
                Require(Scalar(@"SELECT COALESCE(SUM(l.Debit-l.Credit),0) FROM dbo.CashBoxJournalLinksTb k
JOIN dbo.JournalEntryLinesTb l ON l.JournalEntryLineId=k.JournalEntryLineId WHERE k.CashBoxId=@Box;", null, originalBox)==0m,
                    "Original cashbox net did not return to zero.");
                Require(Scalar("SELECT COUNT(*) FROM dbo.CashBoxJournalLinksTb WHERE CashBoxId=@Box;", null, laterBox)==0m,
                    "Later cashier assignment captured the original cancellation.");
                Require(Scalar(@"SELECT COUNT(*) FROM (SELECT l.AccountCode FROM dbo.JournalEntriesTb e
JOIN dbo.JournalEntryLinesTb l ON l.JournalEntryId=e.JournalEntryId
WHERE e.SourceId=@Value AND e.SourceType IN(N'SalesInvoice',N'SalesInvoiceCancellation')
GROUP BY l.AccountCode HAVING SUM(l.Debit-l.Credit)<>0) x;", invoice)==0m,
                    "Original and cancellation failed per-account reconciliation.");
                bool duplicateRejected = false;
                try { ApplicationServices.SalesInvoiceCancellations.Cancel(new SalesInvoiceCancellationRequest {
                    InvoiceReference=invoice,ActorName=actor.UserName,Reason="QA duplicate cancellation" }); }
                catch (InvalidOperationException) { duplicateRejected = true; }
                Require(duplicateRejected && Stock(medicine)==stock &&
                    Scalar("SELECT COUNT(*) FROM dbo.JournalEntriesTb WHERE SourceType=N'SalesInvoiceCancellation' AND SourceId=@Value;", invoice)==1m,
                    "Repeated cancellation changed stock or journal.");
                Console.WriteLine("PASS: real QA sale/cancellation preserved original box after reassignment/deactivation; mismatch rolled back sale; stock restored; each account and original box net zero; duplicate denied.");
                Console.WriteLine("Retained QA invoice="+invoice+"; cashier fixture="+nonce+"; two inactive unassigned box definitions and journal links remain for audit.");
            }
            finally
            {
                try
                {
                    if (invoice!=null && !cancelled)
                        ApplicationServices.SalesInvoiceCancellations.Cancel(new SalesInvoiceCancellationRequest {
                            InvoiceReference=invoice,ActorName=actor.UserName,Reason="QA cashbox cleanup after failed assertion" });
                }
                finally
                {
                    try
                    {
                        if (boxesCreated) ApplicationServices.CashBoxes.SaveBatch(new[] {
                            new CashBoxInput { CashBoxId=originalBox,Code=nonce,Name="صندوق QA الأصلي",IsActive=false },
                            new CashBoxInput { CashBoxId=laterBox,Code=nonce+"-NEW",Name="صندوق QA اللاحق",IsActive=false }
                        },actor);
                        else if (employeeCreated)
                        {
                            using (SqlConnection connection=Database.CreateConnection())
                            using (var command=new SqlCommand("DELETE dbo.EmployeeTb1 WHERE EmpId=@Value;",connection)) {
                                command.Parameters.Add("@Value",SqlDbType.NVarChar,50).Value=nonce;
                                connection.Open();command.ExecuteNonQuery();
                            }
                        }
                    }
                    finally { SessionContext.Clear(); }
                }
            }
        }
        internal static void RunImportOnOwnedRestore(string backupPath, string ownerToken)
        {
            string expectedDatabase = QaSalesInvoiceSelfTests.RequireOwnedTemporaryQa(ownerToken);
            using (SqlConnection connection = Database.CreateConnection())
            {
                connection.Open();
                Require(connection.Database == expectedDatabase, "Import must use the owned QA restore.");
                QaTaxPolicySelfTests.VerifyBackup(connection, backupPath);
            }
            string nonce = "QAIMP-" + ownerToken.Substring(0, 12).ToUpperInvariant();
            string employeeId = "000" + ownerToken.Substring(0, 12);
            string file = Path.Combine(Path.GetTempPath(), "qa-cashbox-" + ownerToken + ".xlsx");
            string exportedFile = Path.Combine(Path.GetTempPath(), "qa-cashbox-export-" + ownerToken + ".xlsx");
            Guid firstId = Guid.NewGuid(), secondId = Guid.NewGuid();
            string name = "صندوق الكاشير QA 500 mg";
            var actor = new OperationActor { UserName = "QA cashbox import verification", UserRole = "Manager" };
            string financeBefore = ReadImportSnapshot(true);
            SessionContext.Start(actor.UserName, actor.UserRole);
            try
            {
                int initialBoxes = ApplicationServices.CashBoxes.GetAll(actor).Count;
                using (SqlConnection connection = Database.CreateConnection())
                using (var command = new SqlCommand(@"INSERT dbo.EmployeeTb1(EmpId,EmpName,EmpSal,EmpAge,EmpPhone,EmpPassword,EmpRole)
VALUES(@Id,N'كاشير اختبار XLSX',0,25,N'000',@Random,N'Cashier');", connection))
                {
                    command.Parameters.Add("@Id", SqlDbType.NVarChar, 50).Value = employeeId;
                    command.Parameters.Add("@Random", SqlDbType.NVarChar, 50).Value = Guid.NewGuid().ToString("N");
                    connection.Open(); command.ExecuteNonQuery();
                }
                var records = new[] {
                    new CashBoxRecord { CashBoxId=firstId,Code=nonce,Name=name,IsActive=true,EmployeeId=employeeId },
                    new CashBoxRecord { CashBoxId=secondId,Code=nonce+"-OFF",Name="صندوق احتياطي QA",IsActive=false }
                };
                using (DataTable sheet = CashBoxWorkbook.CreateExport(records))
                    ExcelWorkbookWriter.WriteCompleteFile(sheet, "CashBoxes", file);

                System.Windows.Forms.Application.EnableVisualStyles();
                System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
                AppFonts.Initialize();
                int confirmations = 0;
                // The confirmation boundary is injected; no global desktop keys or native-dialog automation.
                using (var form = new CashBoxManagementForm(false, count =>
                {
                    Require(count == 2, "Confirmation must describe the complete imported batch.");
                    confirmations++; return true;
                }))
                {
                    form.Show(); System.Windows.Forms.Application.DoEvents(); WaitImportUi(form, null);
                    WaitImportUi(form, form.LoadFileAsync(file));
                    var grid = (DataGridView)ImportField(form, "grid");
                    var save = (AntdUI.Button)ImportField(form, "save");
                    Require(grid.Rows.Count == 2 && save.Enabled && ImportField(form, "pendingImport") != null,
                        "Actual cashbox form did not preview two XLSX definitions.");
                    save.PerformClick(); WaitImportUi(form, null);
                    Require(confirmations == 1 && ImportField(form, "pendingImport") == null &&
                        Convert.ToBoolean(ImportField(form, "statusSuccess")) && grid.Rows.Count == initialBoxes + 2,
                        "Actual cashbox form did not finish saving and refreshing its definitions.");
                    records[0].Name = name = "صندوق محدّث QA 500 mg";
                    using (DataTable sheet = CashBoxWorkbook.CreateExport(records))
                        ExcelWorkbookWriter.WriteCompleteFile(sheet, "CashBoxes", file);
                    WaitImportUi(form, form.LoadFileAsync(file));
                    save.PerformClick(); WaitImportUi(form, null);
                    Require(confirmations == 2 && ImportField(form, "pendingImport") == null &&
                        Convert.ToBoolean(ImportField(form, "statusSuccess")) && grid.Rows.Count == initialBoxes + 2,
                        "Re-import did not update existing IDs without duplicate definitions.");
                    form.Close();
                }
                var saved = ApplicationServices.CashBoxes.GetAll(actor);
                var first = saved.Single(box => box.CashBoxId == firstId);
                var second = saved.Single(box => box.CashBoxId == secondId);
                Require(saved.Count == initialBoxes + 2 && first.Code == nonce && first.Name == name &&
                    first.IsActive && first.EmployeeId == employeeId && !second.IsActive && string.IsNullOrEmpty(second.EmployeeId),
                    "Committed definitions or leading-zero cashier assignment differ after fresh SQL read.");
                using (DataTable sheet = CashBoxWorkbook.CreateExport(new[] { first, second }))
                    ExcelWorkbookWriter.WriteCompleteFile(sheet, "CashBoxes", exportedFile);
                using (DataTable sheet = ExcelWorkbookReader.Read(exportedFile))
                {
                    var reread = CashBoxWorkbook.Parse(sheet, saved);
                    Require(reread.Count == 2 && reread[0].CashBoxId == firstId &&
                        reread[0].Name == name && reread[0].EmployeeId == employeeId,
                        "Exported physical XLSX failed its Arabic/ID roundtrip.");
                }

                string stateBeforeFailure = ReadImportSnapshot(false);
                bool rollbackRejected = false;
                try
                {
                    ApplicationServices.CashBoxes.SaveBatch(new[] {
                        new CashBoxInput { CashBoxId=Guid.NewGuid(),Code=nonce+"-FAIL1",Name="لن يحفظ",IsActive=true },
                        new CashBoxInput { CashBoxId=Guid.NewGuid(),Code=nonce+"-FAIL2",Name="كاشير غير موجود",IsActive=true,EmployeeId=nonce+"-MISSING" }
                    }, actor);
                }
                catch (SqlException error) when (error.Number == 51002) { rollbackRejected = true; }
                Require(rollbackRejected && ReadImportSnapshot(false) == stateBeforeFailure,
                    "Invalid cashier did not roll back the whole batch, assignments and audit.");

                SessionContext.Start("QA cashier import denied", "Cashier", employeeId);
                bool cashierDenied = false;
                try { ApplicationServices.CashBoxes.SaveBatch(new[] { new CashBoxInput {
                    Code=nonce+"-DENIED",Name="غير مسموح",IsActive=true } }, new OperationActor {
                    UserName=SessionContext.CurrentUser,UserRole=SessionContext.CurrentRole }); }
                catch (UnauthorizedAccessException) { cashierDenied = true; }
                Require(cashierDenied && ReadImportSnapshot(false) == stateBeforeFailure,
                    "Cashier was not denied inside the real application service without persistent writes.");
                Require(ReadImportSnapshot(true) == financeBefore,
                    "Import changed a journal, cashbox journal attribution, or medicine stock.");
                Console.WriteLine("PASS: actual cashbox form previewed physical XLSX, saved two definitions and re-imported existing IDs; fresh SQL/XLSX reads preserved Arabic and leading-zero cashier ID.");
                Console.WriteLine("PASS: invalid cashier rolled back the complete SQL batch; cashier service authorization denied writes; journal/cash attribution/stock snapshots unchanged.");
                Console.WriteLine("Not exercised: native file-picker and confirmation-dialog input; confirmation boundary supplied by test host, all persistence used real SQL services.");
            }
            finally
            {
                SessionContext.Clear();
                if (File.Exists(file)) File.Delete(file);
                if (File.Exists(exportedFile)) File.Delete(exportedFile);
            }
        }

        private static object ImportField(CashBoxManagementForm form, string name)
        {
            return typeof(CashBoxManagementForm).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form);
        }

        private static void WaitImportUi(CashBoxManagementForm form, Task task)
        {
            var time = System.Diagnostics.Stopwatch.StartNew();
            while ((task != null ? !task.IsCompleted : (bool)ImportField(form, "busy")) && time.ElapsedMilliseconds < 30000)
            {
                System.Windows.Forms.Application.DoEvents(); System.Threading.Thread.Sleep(10);
            }
            Require(!(bool)ImportField(form, "busy") && (task == null || task.IsCompleted), "Cashbox form operation timed out.");
            task?.GetAwaiter().GetResult();
        }

        private static string ReadImportSnapshot(bool financialOnly)
        {
            string[] tables = financialOnly
                ? new[] { "JournalEntriesTb:JournalEntryId", "JournalEntryLinesTb:JournalEntryLineId", "CashBoxJournalLinksTb:JournalEntryLineId", "MedicineTb1:MedName" }
                : new[] { "CashBoxesTb:CashBoxId", "CashBoxAssignmentsTb:EmpId", "AuditLogTb:AuditId" };
            var text = new System.Text.StringBuilder();
            using (SqlConnection connection = Database.CreateConnection())
            {
                connection.Open();
                foreach (string item in tables)
                {
                    string[] pair = item.Split(':');
                    using (var command = new SqlCommand("SELECT CONVERT(nvarchar(max),(SELECT * FROM dbo.[" + pair[0] + "] ORDER BY [" + pair[1] + "] FOR XML RAW('r'),ROOT('rows'),TYPE));", connection))
                        text.Append(pair[0]).Append(Convert.ToString(command.ExecuteScalar()));
                }
            }
            using (var sha = System.Security.Cryptography.SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(text.ToString())));
        }

        private static int Stock(string medicine) { return (int)Scalar("SELECT MedQty FROM dbo.MedicineTb1 WHERE MedName=@Value;",medicine); }
        private static decimal Scalar(string sql,string value,Guid? box=null)
        {
            using (SqlConnection connection=Database.CreateConnection())
            using (var command=new SqlCommand(sql,connection)) {
                if (value!=null) command.Parameters.Add("@Value",SqlDbType.NVarChar,200).Value=value;
                if (box.HasValue) command.Parameters.Add("@Box",SqlDbType.UniqueIdentifier).Value=box.Value;
                connection.Open(); return Convert.ToDecimal(command.ExecuteScalar());
            }
        }
        private static void Require(bool condition,string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}

using System;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Threading;
using PharmacyManagement.Application;

namespace project_fo_3
{
    // Live SQL acceptance of the sales idempotency contract.
    // It runs only inside an owned temporary QA clone: the clone is restored from a verified
    // backup, tagged with the owner token, and deleted after the run by the calling script.
    internal static class QaSalesIdempotencySqlSelfTests
    {
        // A port that performs the real financial transaction and then loses the response.
        private sealed class LostResponseSalesPort : ISalesWriteRecoveryPort
        {
            private readonly SalesInvoiceOperation operation = new SalesInvoiceOperation();
            internal int RealIssues;
            internal SalesInvoiceIssueResult Committed;

            public SalesInvoiceIssueResult Issue(SalesWriteIntent intent, SalesWriteExecutionContext execution)
            {
                SalesInvoiceIssueResult result = operation.IssueIdentified(intent, null, execution);
                RealIssues++;
                Committed = result;
                throw new TimeoutException("QA lost response after the real SQL commit.");
            }

            public SalesReceiptObservation Observe(SalesWriteIntent intent, SalesWriteExecutionContext execution)
            {
                return operation.ObserveIdentified(intent, execution);
            }

            public SalesReceiptObservation Abandon(SalesWriteIntent intent, SalesWriteExecutionContext execution)
            {
                return operation.AbandonUnposted(intent, null, execution);
            }
        }

        private sealed class DisconnectBeforeCommitSalesPort : ISalesWriteRecoveryPort
        {
            private readonly SalesInvoiceOperation operation;
            internal int RealIssues;

            internal DisconnectBeforeCommitSalesPort()
            {
                operation = new SalesInvoiceOperation
                {
                    DisconnectConnectionBeforeCommitForVerification = (connection, transaction) => connection.Close()
                };
            }

            public SalesInvoiceIssueResult Issue(SalesWriteIntent intent, SalesWriteExecutionContext execution)
            {
                RealIssues++;
                return operation.IssueIdentified(intent, null, execution);
            }

            public SalesReceiptObservation Observe(SalesWriteIntent intent, SalesWriteExecutionContext execution)
            {
                return operation.ObserveIdentified(intent, execution);
            }

            public SalesReceiptObservation Abandon(SalesWriteIntent intent, SalesWriteExecutionContext execution)
            {
                return operation.AbandonUnposted(intent, null, execution);
            }
        }

        public static void Run(string ownerToken)
        {
            string expectedDatabase = QaSalesInvoiceSelfTests.RequireOwnedTemporaryQa(ownerToken);
            string medicine;
            int price;
            int stock;
            using (SqlConnection connection = Database.CreateConnection())
            {
                connection.Open();
                using (SqlCommand command = new SqlCommand(
                    "SELECT TOP 1 MedName, Sprice, MedQty FROM dbo.MedicineTb1 WHERE MedQty > 3 AND Sprice > 0 AND ExpDate > GETDATE() ORDER BY MedName", connection))
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    Require(reader.Read(), "No eligible QA stock.");
                    medicine = reader.GetString(0); price = Convert.ToInt32(reader[1]); stock = Convert.ToInt32(reader[2]);
                }
            }

            string directory = Path.Combine(Path.GetTempPath(), "PharmacyQaSalesIdempotency-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string employeeId; string employeeName; string employeeRole;
            using (SqlConnection connection = Database.CreateConnection())
            using (SqlCommand command = new SqlCommand(
                "SELECT TOP 1 EmpId, EmpName, EmpRole FROM dbo.EmployeeTb1 WHERE EmpRole=N'Manager' ORDER BY EmpId", connection))
            {
                connection.Open();
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    Require(reader.Read(), "The owned clone has no manager employee for the owned sale.");
                    employeeId = reader.GetString(0); employeeName = reader.GetString(1); employeeRole = Convert.ToString(reader[2]);
                }
            }
            SessionContext.Start(employeeName, employeeRole, employeeId);
            try
            {
                var actor = new OperationActor { UserName = SessionContext.CurrentUser, UserRole = SessionContext.CurrentRole };
                var snapshot = ApplicationServices.CurrencyRead.GetSystemCurrencySnapshotAsync(actor, CancellationToken.None).GetAwaiter().GetResult();
                var request = new SalesInvoiceIssueRequest
                {
                    ExpectedCurrencyCode = snapshot.CurrencyCode,
                    CustomerId = null,
                    CustomerName = "عميل الاسترجاع",
                    PhoneNumber = "000000000",
                    CustomerAddress = "عام",
                    EmployeeName = employeeName,
                    CashierEmployeeId = employeeId,
                    Notes = "QA sales idempotency",
                    InvoiceDate = DateTime.Now,
                    DisplayedGrossTotal = price,
                    DisplayedNetTotal = price,
                    DiscountPercent = 0m,
                    UseCustomerCredit = false,
                    Lines = new[]
                    {
                        new SalesInvoiceLineRequest { MedicineName = medicine, Quantity = 1, DisplayedSellingPrice = price }
                    }
                };

                var port = new LostResponseSalesPort();
                SalesInvoiceIssueResult original;
                // The proof counts the financial rows before and after the attempt, so a second
                // invoice with a different reference cannot hide behind the first one.
                FinancialCounts before = FinancialCounts.Read();
                using (var coordinator = new SalesWriteRecoveryCoordinator(directory, port))
                {
                    bool lost = false;
                    try { coordinator.Submit(request, false); }
                    catch (TimeoutException) { lost = true; }
                    Require(lost, "The real SQL commit must surface a lost response to the caller.");
                    Require(port.RealIssues == 1, "The real transaction must run exactly once.");
                    Require(Directory.GetFiles(directory, "*.pending").Length == 1, "The durable attempt must survive the lost response.");

                    original = port.Committed;
                    Require(original != null && !string.IsNullOrWhiteSpace(original.AccountingSourceId),
                        "The committed invoice reference is missing.");
                    string billId = original.AccountingSourceId;

                    FinancialCounts after = FinancialCounts.Read();
                    Require(after.InvoiceLines == before.InvoiceLines + 1, "The sale must add exactly one invoice line: " + before.InvoiceLines + " -> " + after.InvoiceLines);
                    Require(after.Journals == before.Journals + 1, "The sale must post exactly one journal: " + before.Journals + " -> " + after.Journals);
                    Require(after.Movements == before.Movements + 1, "The sale must post exactly one inventory movement: " + before.Movements + " -> " + after.Movements);
                    Require(after.Receipts == before.Receipts + 1, "The sale must leave exactly one durable receipt: " + before.Receipts + " -> " + after.Receipts);
                    Require(Stock(medicine) == stock - 1, "The sale did not deduct exactly one unit.");

                    // The retry must read the server receipt and must not write a second invoice.
                    SalesInvoiceIssueResult observed = coordinator.Submit(request, false);
                    Require(port.RealIssues == 1, "A retry after a lost response must not run the real transaction again.");
                    Require(observed != null && observed.AccountingSourceId == original.AccountingSourceId,
                        "A retry after a lost response must return the original committed invoice.");
                    Require(observed.NetTotal == original.NetTotal && observed.GrossTotal == original.GrossTotal,
                        "A retry after a lost response must return the original amounts.");

                    FinancialCounts afterRetry = FinancialCounts.Read();
                    Require(afterRetry.InvoiceLines == after.InvoiceLines, "A retry after a lost response must not add a second invoice line.");
                    Require(afterRetry.Journals == after.Journals, "A retry after a lost response must not post a second journal.");
                    Require(afterRetry.Movements == after.Movements, "A retry after a lost response must not post a second inventory movement.");
                    Require(afterRetry.Receipts == after.Receipts, "A retry after a lost response must not add a second receipt.");
                    Require(Stock(medicine) == stock - 1, "A retry after a lost response must not deduct the stock twice.");

                    // A different request is refused while the attempt is pending.
                    bool refused = false;
                    try { coordinator.Submit(ChangedRequest(request), false); }
                    catch (InvalidOperationException error)
                    {
                        refused = error.Message.Contains("توجد محاولة بيع معلقة ببيانات مختلفة");
                    }
                    Require(refused, "A different request must be refused while the pending attempt exists.");
                    Require(port.RealIssues == 1, "A refused request must not reach the real transaction.");

                    coordinator.CompleteDisplayed(original);
                    Require(Directory.GetFiles(directory, "*.pending").Length == 0, "A confirmed receipt must clear the pending attempt.");
                }

                VerifyDisconnectBeforeCommit(directory, request, medicine, stock);

                // F1 scenario: a legacy sale dated yesterday must post its journal on that date, not on today.
                DateTime invoiceDate = DateTime.Today.AddDays(-1);
                var dated = new SalesInvoiceIssueRequest
                {
                    ExpectedCurrencyCode = snapshot.CurrencyCode,
                    CustomerId = null,
                    CustomerName = "عميل التاريخ",
                    PhoneNumber = "000000000",
                    CustomerAddress = "عام",
                    EmployeeName = employeeName,
                    CashierEmployeeId = employeeId,
                    Notes = "QA legacy invoice date",
                    InvoiceDate = invoiceDate,
                    DisplayedGrossTotal = price,
                    DisplayedNetTotal = price,
                    DiscountPercent = 0m,
                    UseCustomerCredit = false,
                    Lines = new[]
                    {
                        new SalesInvoiceLineRequest { MedicineName = medicine, Quantity = 1, DisplayedSellingPrice = price }
                    }
                };
                SalesInvoiceIssueResult datedResult = ApplicationServices.SalesInvoices.Issue(dated);
                Require(datedResult != null && !string.IsNullOrWhiteSpace(datedResult.AccountingSourceId),
                    "The dated legacy sale has no accounting source.");
                DateTime postedDate;
                using (SqlConnection connection = Database.CreateConnection())
                using (SqlCommand command = new SqlCommand(
                    "SELECT TOP(1) EntryDate FROM dbo.JournalEntriesTb WHERE SourceType=N'SalesInvoice' AND SourceId=@Id ORDER BY JournalEntryId DESC", connection))
                {
                    command.Parameters.Add("@Id", SqlDbType.NVarChar, 50).Value = datedResult.AccountingSourceId;
                    connection.Open();
                    object value = command.ExecuteScalar();
                    Require(value != null && value != DBNull.Value, "The dated legacy sale posted no journal.");
                    postedDate = Convert.ToDateTime(value);
                }
                Require(postedDate.Date == invoiceDate.Date,
                    "Legacy journal date drifted: invoice=" + invoiceDate.ToString("yyyy-MM-dd") + "; posted=" + postedDate.ToString("yyyy-MM-dd") + ".");
                Console.WriteLine("QA legacy invoice date passed: invoice={0}; journal={1}.",
                    invoiceDate.ToString("yyyy-MM-dd"), postedDate.ToString("yyyy-MM-dd"));
                // F2 scenario: with tax enabled, the legacy result must report exactly the included tax
                // the journal posts to 2100, otherwise the server receipt rejects the sale.
                decimal previousRate = 0m; bool previousEnabled = false; bool hadSettings = false;
                using (SqlConnection connection = Database.CreateConnection())
                using (SqlCommand command = new SqlCommand("SELECT TOP(1) TaxRate, IsEnabled FROM dbo.TaxSettingsTb ORDER BY TaxSettingId", connection))
                {
                    connection.Open();
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            hadSettings = true;
                            previousRate = Convert.ToDecimal(reader[0]);
                            previousEnabled = Convert.ToBoolean(reader[1]);
                        }
                    }
                }
                string previousCountry = Properties.Setings.Default.TaxCountryCode;
                // Yemen disables tax by policy; the scenario needs an enabled country to exercise the
                // included-tax path, and the previous value is restored in the finally below.
                Properties.Setings.Default.TaxCountryCode = "OTHER";
                try
                {
                    using (SqlConnection connection = Database.CreateConnection())
                    using (SqlCommand command = new SqlCommand(
                        "DECLARE @Id INT; SELECT TOP 1 @Id=TaxSettingId FROM dbo.TaxSettingsTb ORDER BY TaxSettingId; " +
                        "IF @Id IS NULL INSERT INTO dbo.TaxSettingsTb(TaxRate,IsEnabled) VALUES(15,1); " +
                        "ELSE UPDATE dbo.TaxSettingsTb SET TaxRate=15, IsEnabled=1 WHERE TaxSettingId=@Id;", connection))
                    {
                        connection.Open();
                        command.ExecuteNonQuery();
                    }

                    var taxedRequest = new SalesInvoiceIssueRequest
                    {
                        ExpectedCurrencyCode = snapshot.CurrencyCode,
                        CustomerId = null,
                        CustomerName = "عميل الضريبة",
                        PhoneNumber = "000000000",
                        CustomerAddress = "عام",
                        EmployeeName = employeeName,
                        CashierEmployeeId = employeeId,
                        Notes = "QA legacy included tax",
                        InvoiceDate = DateTime.Now,
                        DisplayedGrossTotal = price,
                        DisplayedNetTotal = price,
                        DiscountPercent = 0m,
                        UseCustomerCredit = false,
                        Lines = new[]
                        {
                            new SalesInvoiceLineRequest { MedicineName = medicine, Quantity = 1, DisplayedSellingPrice = price }
                        }
                    };
                    SalesInvoiceIssueResult taxedResult = ApplicationServices.SalesInvoices.Issue(taxedRequest);
                    Require(taxedResult != null && !string.IsNullOrWhiteSpace(taxedResult.AccountingSourceId),
                        "The taxed legacy sale has no accounting source.");
                    decimal journalTax;
                    using (SqlConnection connection = Database.CreateConnection())
                    using (SqlCommand command = new SqlCommand(
                        "SELECT COALESCE(SUM(l.Credit),0) FROM dbo.JournalEntriesTb e JOIN dbo.JournalEntryLinesTb l ON l.JournalEntryId=e.JournalEntryId " +
                        "WHERE e.SourceType=N'SalesInvoice' AND e.SourceId=@Id AND l.AccountCode=N'2100'", connection))
                    {
                        command.Parameters.Add("@Id", SqlDbType.NVarChar, 50).Value = taxedResult.AccountingSourceId;
                        connection.Open();
                        journalTax = Convert.ToDecimal(command.ExecuteScalar());
                    }
                    Require(journalTax > 0m, "The journal posted no output tax while the tax setting is enabled.");
                    Require(taxedResult.TaxTotal == journalTax,
                        "Legacy TaxTotal " + taxedResult.TaxTotal + " differs from the journal output tax " + journalTax + ".");
                    Console.WriteLine("QA legacy included tax passed: net={0}; tax={1}; journal={2}.",
                        taxedResult.NetTotal, taxedResult.TaxTotal, journalTax);
                }
                finally
                {
                    using (SqlConnection connection = Database.CreateConnection())
                    using (SqlCommand command = new SqlCommand(
                        "DECLARE @Id INT; SELECT TOP 1 @Id=TaxSettingId FROM dbo.TaxSettingsTb ORDER BY TaxSettingId; " +
                        "IF @Id IS NULL INSERT INTO dbo.TaxSettingsTb(TaxRate,IsEnabled) VALUES(@Rate,@Enabled); " +
                        "ELSE UPDATE dbo.TaxSettingsTb SET TaxRate=@Rate, IsEnabled=@Enabled WHERE TaxSettingId=@Id;", connection))
                    {
                        command.Parameters.Add("@Rate", SqlDbType.Decimal).Value = hadSettings ? previousRate : 0m;
                        command.Parameters.Add("@Enabled", SqlDbType.Bit).Value = hadSettings && previousEnabled;
                        connection.Open();
                        command.ExecuteNonQuery();
                    }
                    Properties.Setings.Default.TaxCountryCode = previousCountry;
                }
                // F4: a failed audit insert must roll the whole sale back (no invoice, stock, journal or receipt).
                FinancialCounts auditBefore = FinancialCounts.Read();
                using (SqlConnection connection = Database.CreateConnection())
                using (SqlCommand command = new SqlCommand(@"CREATE TRIGGER dbo.QA_SalesAuditFailure ON dbo.AuditLogTb AFTER INSERT AS
BEGIN
    IF EXISTS (SELECT 1 FROM inserted WHERE ActionName = N'SalesInvoiceSaved')
        THROW 51000, N'QA forced audit failure', 1;
END", connection))
                {
                    connection.Open();
                    command.ExecuteNonQuery();
                }
                try
                {
                    var auditRequest = new SalesInvoiceIssueRequest
                    {
                        ExpectedCurrencyCode = snapshot.CurrencyCode,
                        CustomerId = null,
                        CustomerName = "عميل التدقيق",
                        PhoneNumber = "000000000",
                        CustomerAddress = "عام",
                        EmployeeName = employeeName,
                        CashierEmployeeId = employeeId,
                        Notes = "QA atomic audit",
                        InvoiceDate = DateTime.Now,
                        DisplayedGrossTotal = price,
                        DisplayedNetTotal = price,
                        DiscountPercent = 0m,
                        UseCustomerCredit = false,
                        Lines = new[]
                        {
                            new SalesInvoiceLineRequest { MedicineName = medicine, Quantity = 1, DisplayedSellingPrice = price }
                        }
                    };
                    bool auditBlocked = false;
                    try { ApplicationServices.SalesInvoices.Issue(auditRequest); }
                    catch (Exception) { auditBlocked = true; }
                    Require(auditBlocked, "A sale committed although its audit insert failed.");
                    FinancialCounts auditAfter = FinancialCounts.Read();
                    Require(auditAfter.InvoiceLines == auditBefore.InvoiceLines &&
                        auditAfter.Journals == auditBefore.Journals &&
                        auditAfter.Movements == auditBefore.Movements &&
                        auditAfter.Receipts == auditBefore.Receipts,
                        "A failed audit must not leave invoice, journal, movement or receipt rows.");
                    Console.WriteLine("QA sales atomic audit passed: the sale rolled back with its failed audit; no financial row changed.");
                }
                finally
                {
                    using (SqlConnection connection = Database.CreateConnection())
                    using (SqlCommand command = new SqlCommand("IF OBJECT_ID(N'dbo.QA_SalesAuditFailure',N'TR') IS NOT NULL DROP TRIGGER dbo.QA_SalesAuditFailure;", connection))
                    {
                        connection.Open();
                        command.ExecuteNonQuery();
                    }
                }

                // F8: a failed receipt insert must roll the identified sale back.
                FinancialCounts receiptBefore = FinancialCounts.Read();
                using (SqlConnection connection = Database.CreateConnection())
                using (SqlCommand command = new SqlCommand("IF OBJECT_ID(N'dbo.QA_ReceiptFailure',N'TR') IS NULL EXEC(N'CREATE TRIGGER dbo.QA_ReceiptFailure ON dbo.SalesWriteReceiptsTb AFTER INSERT AS THROW 51000, N''QA forced receipt failure'', 1;');", connection))
                {
                    connection.Open();
                    command.ExecuteNonQuery();
                }
                try
                {
                    var receiptRequest = new SalesInvoiceIssueRequest
                    {
                        ExpectedCurrencyCode = snapshot.CurrencyCode,
                        CustomerId = null,
                        CustomerName = "عميل الإيصال",
                        PhoneNumber = "000000000",
                        CustomerAddress = "عام",
                        EmployeeName = employeeName,
                        CashierEmployeeId = employeeId,
                        Notes = "QA receipt atomicity",
                        InvoiceDate = DateTime.Now,
                        DisplayedGrossTotal = price,
                        DisplayedNetTotal = price,
                        DiscountPercent = 0m,
                        UseCustomerCredit = false,
                        Lines = new[]
                        {
                            new SalesInvoiceLineRequest { MedicineName = medicine, Quantity = 1, DisplayedSellingPrice = price }
                        }
                    };
                    var receiptIntent = new SalesWriteIntent(Guid.NewGuid(), employeeId,
                        new OperationActor { UserName = employeeName, UserRole = employeeRole },
                        receiptRequest, SalesWriteSourceFingerprint.Compute(Database.ConnectionString), false);
                    bool receiptBlocked = false;
                    try { new SalesInvoiceOperation().IssueIdentified(receiptIntent); }
                    catch (Exception) { receiptBlocked = true; }
                    Require(receiptBlocked, "A sale committed although its durable receipt insert failed.");
                    FinancialCounts receiptAfter = FinancialCounts.Read();
                    Require(receiptAfter.InvoiceLines == receiptBefore.InvoiceLines &&
                        receiptAfter.Journals == receiptBefore.Journals &&
                        receiptAfter.Movements == receiptBefore.Movements &&
                        receiptAfter.Receipts == receiptBefore.Receipts,
                        "A failed receipt must not leave invoice, journal, movement or receipt rows.");
                    Console.WriteLine("QA sales receipt atomicity passed: the sale rolled back with its failed receipt; no financial row changed.");
                }
                finally
                {
                    using (SqlConnection connection = Database.CreateConnection())
                    using (SqlCommand command = new SqlCommand("IF OBJECT_ID(N'dbo.QA_ReceiptFailure',N'TR') IS NOT NULL DROP TRIGGER dbo.QA_ReceiptFailure;", connection))
                    {
                        connection.Open();
                        command.ExecuteNonQuery();
                    }
                }

                Console.WriteLine("QA sales idempotency passed: one real SQL commit added exactly one invoice line, one journal, one movement and one receipt; the retry returned the same invoice and changed no financial row. Database={0}; BillId={1}.",
                    expectedDatabase, original.AccountingSourceId);
            }
            finally
            {
                SessionContext.Clear();
                try { if (Directory.Exists(directory)) Directory.Delete(directory, true); } catch { }
            }
        }

        private static void VerifyDisconnectBeforeCommit(string directory, SalesInvoiceIssueRequest request,
            string medicine, int stock)
        {
            FinancialCounts before = FinancialCounts.Read();
            var port = new DisconnectBeforeCommitSalesPort();
            using (var coordinator = new SalesWriteRecoveryCoordinator(directory, port))
            {
                bool disconnected = false;
                try { coordinator.Submit(request, false); }
                catch (Exception) { disconnected = true; }
                Require(disconnected && port.RealIssues == 1,
                    "A connection loss immediately before COMMIT must surface after one real issue attempt.");
                Require(Directory.GetFiles(directory, "*.pending").Length == 1,
                    "A connection loss before COMMIT must retain the durable local request.");

                bool unresolved = false;
                try { coordinator.Submit(request, false); }
                catch (InvalidOperationException error)
                {
                    unresolved = error.Message.Contains("نتيجة حفظ البيع غير محسومة");
                }
                Require(unresolved && port.RealIssues == 1,
                    "An unresolved pre-COMMIT request must be observed without a second write attempt.");
                FinancialCounts after = FinancialCounts.Read();
                Require(after.InvoiceLines == before.InvoiceLines && after.Journals == before.Journals &&
                    after.Movements == before.Movements && after.Receipts == before.Receipts && Stock(medicine) == stock - 1,
                    "A disconnected pre-COMMIT attempt left financial rows, a receipt, or a second stock deduction.");

                SalesReceiptObservation abandoned = coordinator.ConfirmAbandon();
                Require(abandoned != null && abandoned.IsAbandoned && coordinator.Pending() == null &&
                    Directory.GetFiles(directory, "*.pending").Length == 0,
                    "An observed receipt-free attempt must close explicitly and clear only its matching local request.");
                FinancialCounts afterAbandon = FinancialCounts.Read();
                Require(afterAbandon.InvoiceLines == before.InvoiceLines && afterAbandon.Journals == before.Journals &&
                    afterAbandon.Movements == before.Movements && Stock(medicine) == stock - 1,
                    "Closing an unposted request must not create an invoice, journal, movement, or stock change.");
            }
            Console.WriteLine("PASS QA sales disconnect before COMMIT: financial writes rolled back, one encrypted request remained unresolved without retry, and explicit receipt-free abandonment cleared it.");
        }

        // QA diagnostics: the application log keeps no message, so the owned run prints the
        // failure reason of the temporary clone into its own evidence log only.
        internal static void ReportFailure(Exception error)
        {
            if (error == null) return;
            var sql = error as SqlException;
            string detail = sql != null && sql.Errors.Count > 0
                ? sql.Errors[0].Number + "/" + sql.Errors[0].State + ": " + sql.Errors[0].Message
                : error.Message;
            Console.Out.WriteLine("QA sales idempotency failure: " + detail);
            Console.Out.WriteLine(error.StackTrace);
        }

        // Concurrency acceptance: two independent connections submit the same durable request id
        // at the same time. The server lock and the durable receipt must admit exactly one invoice.
        public static void RunConcurrency(string ownerToken)
        {
            string expectedDatabase = QaSalesInvoiceSelfTests.RequireOwnedTemporaryQa(ownerToken);
            string medicine;
            int price;
            int stock;
            using (SqlConnection connection = Database.CreateConnection())
            {
                connection.Open();
                using (SqlCommand command = new SqlCommand(
                    "SELECT TOP 1 MedName, Sprice, MedQty FROM dbo.MedicineTb1 WHERE MedQty > 3 AND Sprice > 0 AND ExpDate > GETDATE() ORDER BY MedName", connection))
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    Require(reader.Read(), "No eligible QA stock.");
                    medicine = reader.GetString(0); price = Convert.ToInt32(reader[1]); stock = Convert.ToInt32(reader[2]);
                }
            }

            string employeeId; string employeeName; string employeeRole;
            using (SqlConnection connection = Database.CreateConnection())
            using (SqlCommand command = new SqlCommand(
                "SELECT TOP 1 EmpId, EmpName, EmpRole FROM dbo.EmployeeTb1 WHERE EmpRole=N'Manager' ORDER BY EmpId", connection))
            {
                connection.Open();
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    Require(reader.Read(), "The owned clone has no manager employee for the owned sale.");
                    employeeId = reader.GetString(0); employeeName = reader.GetString(1); employeeRole = Convert.ToString(reader[2]);
                }
            }

            SessionContext.Start(employeeName, employeeRole, employeeId);
            try
            {
                var actor = new OperationActor { UserName = SessionContext.CurrentUser, UserRole = SessionContext.CurrentRole };
                var snapshot = ApplicationServices.CurrencyRead.GetSystemCurrencySnapshotAsync(actor, CancellationToken.None).GetAwaiter().GetResult();
                var request = new SalesInvoiceIssueRequest
                {
                    ExpectedCurrencyCode = snapshot.CurrencyCode,
                    CustomerId = null,
                    CustomerName = "عميل التزامن",
                    PhoneNumber = "000000000",
                    CustomerAddress = "عام",
                    EmployeeName = employeeName,
                    CashierEmployeeId = employeeId,
                    Notes = "QA sales concurrency",
                    InvoiceDate = DateTime.Now,
                    DisplayedGrossTotal = price,
                    DisplayedNetTotal = price,
                    DiscountPercent = 0m,
                    UseCustomerCredit = false,
                    Lines = new[]
                    {
                        new SalesInvoiceLineRequest { MedicineName = medicine, Quantity = 1, DisplayedSellingPrice = price }
                    }
                };

                var owner = new OperationActor { UserName = employeeName, UserRole = employeeRole };
                string source = Database.ConnectionString;
                var intent = new SalesWriteIntent(Guid.NewGuid(), employeeId, owner, request,
                    SalesWriteSourceFingerprint.Compute(source), false);

                var operation = new SalesInvoiceOperation();
                var execution = new SalesWriteExecutionContext(source, SessionContext.CurrentSessionRevision);
                var results = new SalesInvoiceIssueResult[2];
                var failures = new Exception[2];
                using (var ready = new System.Threading.Barrier(2))
                {
                    var threads = new System.Threading.Thread[2];
                    for (int index = 0; index < threads.Length; index++)
                    {
                        int slot = index;
                        threads[index] = new System.Threading.Thread(() =>
                        {
                            try { ready.SignalAndWait(30000); }
                            catch (Exception) { failures[slot] = new InvalidOperationException("The concurrent attempt could not reach the start line."); return; }
                            try { results[slot] = operation.IssueIdentified(intent, null, execution); }
                            catch (Exception error) { failures[slot] = error; }
                        });
                        threads[index].IsBackground = true;
                    }
                    foreach (var thread in threads) thread.Start();
                    foreach (var thread in threads) Require(thread.Join(120000), "A concurrent sales attempt did not finish in time.");
                }

                Require(failures[0] == null && failures[1] == null,
                    "A concurrent duplicate attempt must observe the original receipt, not fail: " +
                    Describe(failures[0]) + " | " + Describe(failures[1]));
                Require(results[0] != null && results[1] != null, "Both concurrent attempts must return a result.");
                Require(results[0].AccountingSourceId == results[1].AccountingSourceId,
                    "The two concurrent attempts must report the same invoice: " + results[0].AccountingSourceId + " / " + results[1].AccountingSourceId);
                Require(results[0].NetTotal == results[1].NetTotal && results[0].GrossTotal == results[1].GrossTotal,
                    "The two concurrent attempts must report the same amounts.");

                string billId = results[0].AccountingSourceId;
                Require(Scalar("SELECT COUNT(*) FROM dbo.BillsTb1 WHERE BillId=@Value", billId) == 1m,
                    "The concurrent attempts must leave exactly one invoice line.");
                Require(Scalar("SELECT COUNT(*) FROM dbo.SalesWriteReceiptsTb WHERE RequestId=@Id", intent.RequestId) == 1m,
                    "The concurrent attempts must leave exactly one durable receipt.");
                Require(Scalar("SELECT COUNT(*) FROM dbo.JournalEntriesTb WHERE SourceType=N'SalesInvoice' AND SourceId=@Value", billId) == 1m,
                    "The concurrent attempts must post exactly one journal entry.");
                Require(Scalar("SELECT COUNT(*) FROM dbo.InventoryMovementsTb WHERE MedName=@Value AND SourceId=@Second", medicine, billId) == 1m,
                    "The concurrent attempts must post exactly one inventory movement.");
                Require(Stock(medicine) == stock - 1, "The concurrent attempts must deduct the stock exactly once.");

                Console.WriteLine("QA sales concurrency passed: two connections submitted the same request id at once; one invoice, one receipt, one journal and one inventory movement were committed. Database={0}; BillId={1}; RequestId={2}.",
                    expectedDatabase, billId, intent.RequestId);
            }
            finally
            {
                SessionContext.Clear();
            }
        }

        // The financial row counters make the "no second invoice" claim independent of the
        // generated invoice reference.
        internal sealed class FinancialCounts
        {
            internal long InvoiceLines;
            internal long Journals;
            internal long Movements;
            internal long Receipts;

            internal static FinancialCounts Read()
            {
                var counts = new FinancialCounts();
                counts.InvoiceLines = (long)Scalar("SELECT COUNT(*) FROM dbo.BillsTb1", "x");
                counts.Journals = (long)Scalar("SELECT COUNT(*) FROM dbo.JournalEntriesTb", "x");
                counts.Movements = (long)Scalar("SELECT COUNT(*) FROM dbo.InventoryMovementsTb", "x");
                counts.Receipts = (long)Scalar("SELECT COUNT(*) FROM dbo.SalesWriteReceiptsTb", "x");
                return counts;
            }
        }

        private static string Describe(Exception error)
        {
            if (error == null) return "none";
            var sql = error as SqlException;
            return sql != null && sql.Errors.Count > 0
                ? sql.Errors[0].Number + "/" + sql.Errors[0].State + ": " + sql.Errors[0].Message
                : error.GetType().Name + ": " + error.Message;
        }

        private static SalesInvoiceIssueRequest ChangedRequest(SalesInvoiceIssueRequest source)
        {
            return new SalesInvoiceIssueRequest
            {
                ExpectedCurrencyCode = source.ExpectedCurrencyCode,
                CustomerId = source.CustomerId,
                CustomerName = source.CustomerName,
                PhoneNumber = source.PhoneNumber,
                CustomerAddress = source.CustomerAddress,
                EmployeeName = source.EmployeeName,
                CashierEmployeeId = source.CashierEmployeeId,
                Notes = source.Notes + " (changed)",
                InvoiceDate = source.InvoiceDate,
                DisplayedGrossTotal = source.DisplayedGrossTotal,
                DisplayedNetTotal = source.DisplayedNetTotal,
                DiscountPercent = source.DiscountPercent,
                UseCustomerCredit = source.UseCustomerCredit,
                Lines = source.Lines
            };
        }

        private static Guid CoordinatorRequestId(SalesWriteRecoveryCoordinator coordinator)
        {
            SalesWriteIntent pending = coordinator.Pending();
            Require(pending != null, "The pending attempt must be readable.");
            return pending.RequestId;
        }

        private static int Stock(string medicine)
        {
            return (int)Scalar("SELECT ISNULL(SUM(MedQty),0) FROM dbo.MedicineTb1 WHERE MedName=@Value", medicine);
        }

        private static decimal Scalar(string sql, string value)
        {
            using (SqlConnection connection = Database.CreateConnection())
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@Value", value);
                connection.Open();
                return Convert.ToDecimal(command.ExecuteScalar());
            }
        }

        private static decimal Scalar(string sql, Guid value)
        {
            using (SqlConnection connection = Database.CreateConnection())
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@Id", value);
                connection.Open();
                return Convert.ToDecimal(command.ExecuteScalar());
            }
        }

        private static decimal Scalar(string sql, string value, string second)
        {
            using (SqlConnection connection = Database.CreateConnection())
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@Value", value);
                command.Parameters.AddWithValue("@Second", second);
                connection.Open();
                return Convert.ToDecimal(command.ExecuteScalar());
            }
        }

        private static string MovementRows(string medicine)
        {
            using (SqlConnection connection = Database.CreateConnection())
            using (SqlCommand command = new SqlCommand(
                "SELECT CONCAT(SourceType, N'|', SourceId, N'|', QuantityDelta) FROM dbo.InventoryMovementsTb WHERE MedName=@Value", connection))
            {
                command.Parameters.AddWithValue("@Value", medicine);
                connection.Open();
                var rows = new System.Text.StringBuilder();
                using (SqlDataReader reader = command.ExecuteReader())
                    while (reader.Read()) rows.Append(reader.GetString(0)).Append("; ");
                return rows.ToString();
            }
        }

        private static void Require(bool condition, string message)
        {
            if (condition) return;
            Console.Error.WriteLine("QA_SALES_IDEMPOTENCY_ASSERT: " + message);
            throw new InvalidOperationException(message);
        }
    }
}

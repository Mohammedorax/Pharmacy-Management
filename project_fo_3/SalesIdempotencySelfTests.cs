using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using PharmacyManagement.Application;

namespace project_fo_3
{
    // Acceptance of the sales idempotency contract at the durable recovery boundary.
    // The port is replaced with a controlled double, so the lost response is reproduced without SQL
    // and without any financial write. The server receipt is the only authority for a retry.
    internal static class SalesIdempotencySelfTests
    {
        private sealed class ControlledSalesPort : ISalesWriteRecoveryPort
        {
            internal int IssueCalls, ObserveCalls, AbandonCalls;
            internal SalesInvoiceIssueResult Committed;
            internal bool LoseResponseAfterCommit = true;
            internal bool ServerHasReceipt = true;

            public SalesInvoiceIssueResult Issue(SalesWriteIntent intent, SalesWriteExecutionContext execution)
            {
                IssueCalls++;
                if (Committed == null)
                {
                    Committed = new SalesInvoiceIssueResult
                    {
                        IsCommercial = false,
                        CustomerId = intent.Snapshot().CustomerId,
                        AccountingSourceId = "BILL-" + intent.RequestId.ToString("N").Substring(0, 8),
                        BillLineIds = new[] { "LINE-" + intent.RequestId.ToString("N").Substring(0, 8) },
                        GrossTotal = intent.Snapshot().DisplayedGrossTotal,
                        DiscountTotal = 0m,
                        TaxTotal = 0m,
                        NetTotal = intent.Snapshot().DisplayedNetTotal,
                        CostOfGoods = 0m,
                        CustomerCreditApplied = 0m,
                        CashCollected = intent.Snapshot().DisplayedNetTotal
                    };
                }
                if (LoseResponseAfterCommit) throw new TimeoutException("Simulated lost response after the server committed the invoice.");
                return Committed;
            }

            public SalesReceiptObservation Observe(SalesWriteIntent intent, SalesWriteExecutionContext execution)
            {
                ObserveCalls++;
                if (!ServerHasReceipt || Committed == null) return null;
                return new SalesReceiptObservation { RequestId = intent.RequestId, Result = Committed };
            }

            public SalesReceiptObservation Abandon(SalesWriteIntent intent, SalesWriteExecutionContext execution)
            {
                AbandonCalls++;
                return new SalesReceiptObservation { RequestId = intent.RequestId, IsAbandoned = true };
            }
        }

        internal static void RunAll()
        {
            VerifyLostResponseDoesNotDuplicate();
            VerifyDifferentRequestIsRefusedWhilePending();
            VerifyUncertainReceiptKeepsTheAttempt();
            VerifyReceiptIsClearedOnlyAfterConfirmation();
            VerifyPendingFileIsEncrypted();
            Console.WriteLine("PASS sales idempotency: a lost response after commit keeps one durable attempt, a retry returns the original receipt without a second write, a different request is refused, an uncertain receipt stays pending until explicitly closed, and the pending file is encrypted.");
        }

        private static SalesInvoiceIssueRequest Request(decimal quantity = 2m, decimal net = 400m)
        {
            return new SalesInvoiceIssueRequest
            {
                ExpectedCurrencyCode = "ر.ي",
                CustomerId = "CUST-1",
                CustomerName = "عميل الاسترجاع",
                PhoneNumber = "0770000000",
                CustomerAddress = "صنعاء",
                EmployeeName = "cashier",
                CashierEmployeeId = "EMP-1",
                Notes = "اختبار منع التكرار",
                InvoiceDate = new DateTime(2026, 10, 20, 10, 0, 0, DateTimeKind.Local),
                DisplayedGrossTotal = net,
                DisplayedNetTotal = net,
                DiscountPercent = 0m,
                UseCustomerCredit = false,
                Lines = new[]
                {
                    new SalesInvoiceLineRequest { MedicineName = "باراسيتامول 500 mg", Quantity = (int)quantity, DisplayedSellingPrice = (int)(net / quantity) }
                }
            };
        }

        private static string NewDirectory()
        {
            string directory = Path.Combine(Path.GetTempPath(), "PharmacySalesIdempotency-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            return directory;
        }

        private static void RemoveDirectory(string directory)
        {
            try { if (Directory.Exists(directory)) Directory.Delete(directory, true); } catch { }
        }

        private static void Require(bool condition, string message)
        {
            if (condition) return;
            Console.Error.WriteLine("SALES_IDEMPOTENCY_ASSERT: " + message);
            throw new InvalidOperationException(message);
        }

        private static void VerifyLostResponseDoesNotDuplicate()
        {
            string directory = NewDirectory();
            try
            {
                SessionContext.Start("sales-idempotency", "Manager", "EMP-1");
                var port = new ControlledSalesPort();
                using (var coordinator = new SalesWriteRecoveryCoordinator(directory, port))
                {
                    bool lost = false;
                    try { coordinator.Submit(Request(), false); }
                    catch (TimeoutException) { lost = true; }
                    Require(lost, "A lost response after the server commit must surface to the caller.");
                    Require(port.IssueCalls == 1, "The first attempt must reach the operation exactly once.");
                    Require(Directory.GetFiles(directory, "*.pending").Length == 1, "The pending attempt must stay durable after the lost response.");

                    SalesInvoiceIssueResult observed = coordinator.Submit(Request(), false);
                    Require(port.IssueCalls == 1, "A retry after a lost response must not issue a second invoice.");
                    Require(port.ObserveCalls == 1, "A retry must read the server receipt instead of writing again.");
                    Require(observed != null && observed.AccountingSourceId == port.Committed.AccountingSourceId,
                        "A retry after a lost response must return the original committed receipt.");
                    Require(observed.BillLineIds.SequenceEqual(port.Committed.BillLineIds),
                        "A retry after a lost response must return the original committed lines.");
                }
            }
            finally { SessionContext.Clear(); RemoveDirectory(directory); }
        }

        private static void VerifyDifferentRequestIsRefusedWhilePending()
        {
            string directory = NewDirectory();
            try
            {
                SessionContext.Start("sales-idempotency", "Manager", "EMP-1");
                var port = new ControlledSalesPort();
                using (var coordinator = new SalesWriteRecoveryCoordinator(directory, port))
                {
                    try { coordinator.Submit(Request(), false); } catch (TimeoutException) { }
                    int issued = port.IssueCalls;
                    bool refused = false;
                    try { coordinator.Submit(Request(quantity: 3m, net: 600m), false); }
                    catch (InvalidOperationException error)
                    {
                        refused = error.Message.Contains("توجد محاولة بيع معلقة ببيانات مختلفة");
                    }
                    Require(refused, "A different request must be refused while a pending attempt exists.");
                    Require(port.IssueCalls == issued, "A refused different request must not reach the operation.");
                    Require(Directory.GetFiles(directory, "*.pending").Length == 1, "A refused request must keep the original pending attempt.");
                }
            }
            finally { SessionContext.Clear(); RemoveDirectory(directory); }
        }

        private static void VerifyUncertainReceiptKeepsTheAttempt()
        {
            string directory = NewDirectory();
            try
            {
                SessionContext.Start("sales-idempotency", "Manager", "EMP-1");
                var port = new ControlledSalesPort { ServerHasReceipt = false };
                using (var coordinator = new SalesWriteRecoveryCoordinator(directory, port))
                {
                    try { coordinator.Submit(Request(), false); } catch (TimeoutException) { }
                    bool uncertain = false;
                    try { coordinator.Submit(Request(), false); }
                    catch (InvalidOperationException error)
                    {
                        uncertain = error.Message.Contains("نتيجة حفظ البيع غير محسومة");
                    }
                    Require(uncertain, "An uncertain server result must not be treated as a free retry.");
                    Require(port.IssueCalls == 1, "An uncertain result must not issue a second invoice.");
                    Require(Directory.GetFiles(directory, "*.pending").Length == 1, "An uncertain result must keep the pending attempt until it is closed explicitly.");

                    SalesReceiptObservation closed = coordinator.ConfirmAbandon();
                    Require(closed != null && closed.IsAbandoned, "Closing an unposted attempt must report the closed state.");
                    Require(Directory.GetFiles(directory, "*.pending").Length == 0, "Closing an unposted attempt must remove the pending file.");
                    port.LoseResponseAfterCommit = false;
                    SalesInvoiceIssueResult issued = coordinator.Submit(Request(), false);
                    Require(issued != null && port.IssueCalls == 2, "After closing the unposted attempt a new attempt may be issued.");
                }
            }
            finally { SessionContext.Clear(); RemoveDirectory(directory); }
        }

        private static void VerifyReceiptIsClearedOnlyAfterConfirmation()
        {
            string directory = NewDirectory();
            try
            {
                SessionContext.Start("sales-idempotency", "Manager", "EMP-1");
                var port = new ControlledSalesPort();
                using (var coordinator = new SalesWriteRecoveryCoordinator(directory, port))
                {
                    try { coordinator.Submit(Request(), false); } catch (TimeoutException) { }
                    SalesInvoiceIssueResult original = port.Committed;
                    var mismatched = new SalesInvoiceIssueResult
                    {
                        IsCommercial = original.IsCommercial,
                        CustomerId = original.CustomerId,
                        AccountingSourceId = "BILL-OTHER",
                        BillLineIds = original.BillLineIds,
                        GrossTotal = original.GrossTotal,
                        DiscountTotal = original.DiscountTotal,
                        TaxTotal = original.TaxTotal,
                        NetTotal = original.NetTotal,
                        CostOfGoods = original.CostOfGoods,
                        CustomerCreditApplied = original.CustomerCreditApplied,
                        CashCollected = original.CashCollected
                    };
                    bool refused = false;
                    try { coordinator.CompleteDisplayed(mismatched); }
                    catch (InvalidOperationException) { refused = true; }
                    Require(refused, "A displayed receipt that does not match the server receipt must be refused.");
                    Require(Directory.GetFiles(directory, "*.pending").Length == 1, "A refused receipt must keep the pending attempt.");

                    coordinator.CompleteDisplayed(original);
                    Require(Directory.GetFiles(directory, "*.pending").Length == 0, "A confirmed receipt must clear the pending attempt.");
                    Require(coordinator.Pending() == null, "A confirmed receipt must leave no pending attempt.");
                }
            }
            finally { SessionContext.Clear(); RemoveDirectory(directory); }
        }

        private static void VerifyPendingFileIsEncrypted()
        {
            string directory = NewDirectory();
            try
            {
                SessionContext.Start("sales-idempotency", "Manager", "EMP-1");
                var port = new ControlledSalesPort();
                using (var coordinator = new SalesWriteRecoveryCoordinator(directory, port))
                {
                    try { coordinator.Submit(Request(), false); } catch (TimeoutException) { }
                    string file = Directory.GetFiles(directory, "*.pending").Single();
                    byte[] raw = File.ReadAllBytes(file);
                    string text = Encoding.UTF8.GetString(raw);
                    Require(!text.Contains("باراسيتامول") && !text.Contains("عميل الاسترجاع") && !text.Contains("0770000000"),
                        "The pending attempt file must not expose business values in clear text.");
                    Require(raw.Length > 0, "The pending attempt file must hold the encrypted payload.");
                }
            }
            finally { SessionContext.Clear(); RemoveDirectory(directory); }
        }
    }
}

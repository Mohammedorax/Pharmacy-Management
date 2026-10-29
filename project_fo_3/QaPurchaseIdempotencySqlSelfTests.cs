using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using PharmacyManagement.Application;

namespace project_fo_3
{
    // Live SQL acceptance of the purchase idempotency contract inside an owned temporary QA clone.
    // It mirrors the sales acceptance: a real financial transaction, a lost response, a retry that
    // must not write twice, and two connections that submit the same request id at the same time.
    internal static class QaPurchaseIdempotencySqlSelfTests
    {
        public static void Run(string ownerToken)
        {
            string expectedDatabase = QaSalesInvoiceSelfTests.RequireOwnedTemporaryQa(ownerToken);
            StartOwnedManager();
            try
            {
                var context = Prepare();
                var operation = new PurchaseInvoiceOperation();
                var intent = new PurchaseWriteIntent(Guid.NewGuid(), SessionContext.CurrentEmployeeId,
                    new OperationActor { UserName = SessionContext.CurrentUser, UserRole = SessionContext.CurrentRole },
                    context.Request, context.Request.PurchaseDate, PurchaseWriteSourceFingerprint.Compute(Database.ConnectionString));

                PurchaseInvoiceResult committed = null;
                bool lost = false;
                // The financial row counters make the "no second purchase" claim independent of
                // the generated purchase reference.
                FinancialCounts before = FinancialCounts.Read();
                try
                {
                    operation.IssueIdentified(intent, result => { committed = result; throw new TimeoutException("QA lost response after the real purchase commit."); });
                }
                catch (TimeoutException) { lost = true; }
                Require(lost, "The real purchase commit must surface a lost response to the caller.");
                Require(committed != null && !string.IsNullOrWhiteSpace(committed.PurchaseId), "The committed purchase reference is missing.");
                string purchaseId = committed.PurchaseId;

                FinancialCounts after = FinancialCounts.Read();
                Require(after.PurchaseHeaders == before.PurchaseHeaders + 1, "The purchase must add exactly one header: " + before.PurchaseHeaders + " -> " + after.PurchaseHeaders);
                Require(after.PurchaseLines == before.PurchaseLines + 1, "The purchase must add exactly one line: " + before.PurchaseLines + " -> " + after.PurchaseLines);
                Require(after.Journals == before.Journals + 1, "The purchase must post exactly one journal: " + before.Journals + " -> " + after.Journals);
                Require(after.Movements == before.Movements + 1, "The purchase must post exactly one inventory movement: " + before.Movements + " -> " + after.Movements);
                Require(after.Receipts == before.Receipts + 1, "The purchase must leave exactly one durable receipt: " + before.Receipts + " -> " + after.Receipts);

                // The retry reads the server receipt instead of writing a second purchase.
                PurchaseInvoiceResult observed = operation.ObserveIdentified(intent)?.Result;
                Require(observed != null && observed.PurchaseId == purchaseId,
                    "The purchase receipt must return the original committed purchase.");
                FinancialCounts afterRetry = FinancialCounts.Read();
                Require(afterRetry.PurchaseHeaders == after.PurchaseHeaders, "The purchase receipt must not add a second purchase header.");
                Require(afterRetry.PurchaseLines == after.PurchaseLines, "The purchase receipt must not add a second purchase line.");
                Require(afterRetry.Journals == after.Journals, "The purchase receipt must not post a second journal.");
                Require(afterRetry.Movements == after.Movements, "The purchase receipt must not post a second inventory movement.");
                Require(afterRetry.Receipts == after.Receipts, "The purchase receipt must not add a second receipt.");

            // F6: the cancellation writer must keep the purchase currency snapshot, never NULL.
            string originalCurrency;
            using (SqlConnection connection = Database.CreateConnection())
            using (SqlCommand command = new SqlCommand("SELECT TOP(1) SystemCurrencyCode FROM dbo.JournalEntriesTb WHERE SourceType=N'PurchaseInvoice' AND SourceId=@Id ORDER BY JournalEntryId", connection))
            { command.Parameters.Add("@Id", SqlDbType.NVarChar, 50).Value = purchaseId; connection.Open(); object v = command.ExecuteScalar(); originalCurrency = v == null || v == DBNull.Value ? null : Convert.ToString(v); }
            Require(!string.IsNullOrWhiteSpace(originalCurrency), "The purchase journal carries no currency snapshot to preserve.");
            var originalLines = new List<JournalLineInput>();
            using (SqlConnection connection = Database.CreateConnection())
            using (SqlCommand command = new SqlCommand("SELECT l.AccountCode,l.Debit,l.Credit,l.LineDescription,l.CostCenterId FROM dbo.JournalEntriesTb e JOIN dbo.JournalEntryLinesTb l ON l.JournalEntryId=e.JournalEntryId WHERE e.SourceType=N'PurchaseInvoice' AND e.SourceId=@Id ORDER BY l.JournalEntryLineId", connection))
            { command.Parameters.Add("@Id", SqlDbType.NVarChar, 50).Value = purchaseId; connection.Open(); using (SqlDataReader reader = command.ExecuteReader()) while (reader.Read()) originalLines.Add(new JournalLineInput { AccountCode = reader.GetString(0), Debit = Convert.ToDecimal(reader[1]), Credit = Convert.ToDecimal(reader[2]), Description = reader.IsDBNull(3) ? string.Empty : reader.GetString(3), CostCenterId = reader.IsDBNull(4) ? (int?)null : Convert.ToInt32(reader[4]) }); }
            using (SqlConnection connection = Database.CreateConnection())
            { connection.Open(); using (SqlTransaction transaction = connection.BeginTransaction()) { AccountingService.PostPurchaseInvoiceCancellation(connection, transaction, purchaseId, "QA F6 currency snapshot", originalLines, DateTime.Today, SessionContext.CurrentUser, originalCurrency); transaction.Commit(); } }
            string cancellationCurrency;
            using (SqlConnection connection = Database.CreateConnection())
            using (SqlCommand command = new SqlCommand("SELECT TOP(1) SystemCurrencyCode FROM dbo.JournalEntriesTb WHERE SourceType=N'PurchaseInvoiceCancellation' AND SourceId=@Id ORDER BY JournalEntryId", connection))
            { command.Parameters.Add("@Id", SqlDbType.NVarChar, 50).Value = purchaseId; connection.Open(); object v = command.ExecuteScalar(); cancellationCurrency = v == null || v == DBNull.Value ? null : Convert.ToString(v); }
            Require(string.Equals(cancellationCurrency, originalCurrency, StringComparison.OrdinalIgnoreCase),
                "The cancellation currency " + (cancellationCurrency ?? "<null>") + " differs from the original " + originalCurrency + ".");
            Console.WriteLine("QA purchase cancellation currency passed: {0}.", originalCurrency);

                Console.WriteLine("QA purchase idempotency passed: one real SQL commit added exactly one purchase header, one line, one journal, one movement and one receipt; the receipt returned the original purchase and changed no financial row. Database={0}; PurchaseId={1}.",
                    expectedDatabase, purchaseId);
            // F3 negative proof: an employee that does not exist in the database must be rejected by
            // the in-transaction authority even though the in-process session accepts the identity.
            string previousEmployeeName = context.Request.EmployeeName;
            bool ghostDenied = false;
            try
            {
                context.Request.EmployeeName = "QA ghost employee";
                var ghost = new PurchaseWriteIntent(Guid.NewGuid(), "EMP-QA-GHOST",
                    new OperationActor { UserName = "QA ghost employee", UserRole = "Manager" },
                    context.Request, DateTime.Now, null);
                using (SqlConnection connection = Database.CreateConnection())
                {
                    connection.Open();
                    using (SqlTransaction transaction = connection.BeginTransaction())
                    {
                        try { PurchaseCommitAuthority.Acquire(connection, transaction, ghost, true); }
                        catch (UnauthorizedAccessException) { ghostDenied = true; }
                        finally { try { transaction.Rollback(); } catch { } }
                    }
                }
            }
            finally { context.Request.EmployeeName = previousEmployeeName; }
            Require(ghostDenied, "A purchase passed the in-transaction authority with an employee that does not exist.");
            Console.WriteLine("QA purchase authority negative passed: a fabricated employee was rejected inside the financial transaction.");
            }
            finally
            {
                SessionContext.Clear();
            }
        }

        // A purchase attempt that never reached a financial write must be closable, otherwise the
        // pending file blocks every later purchase.
        public static void RunAbandon(string ownerToken)
        {
            string expectedDatabase = QaSalesInvoiceSelfTests.RequireOwnedTemporaryQa(ownerToken);
            StartOwnedManager();
            string directory = Path.Combine(Path.GetTempPath(), "PharmacyQaPurchaseAbandon-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var context = Prepare();
                var operation = new PurchaseInvoiceOperation();
                var intent = new PurchaseWriteIntent(Guid.NewGuid(), SessionContext.CurrentEmployeeId,
                    new OperationActor { UserName = SessionContext.CurrentUser, UserRole = SessionContext.CurrentRole },
                    context.Request, context.Request.PurchaseDate, PurchaseWriteSourceFingerprint.Compute(Database.ConnectionString));
                FinancialCounts before = FinancialCounts.Read();
                // The attempt is stored but never written, exactly like a failure before commit.
                using (var store = new PurchasePendingStore(directory, PurchaseWriteSourceFingerprint.Compute(Database.ConnectionString),
                    SessionContext.CurrentEmployeeId, SessionContext.CurrentUser))
                {
                    store.Save(intent);
                }
                using (var coordinator = new PurchaseWriteRecoveryCoordinator(directory, operation))
                {
                    Require(coordinator.Pending() != null, "The pending purchase attempt was not stored.");
                    Require(Scalar("SELECT COUNT(*) FROM dbo.PurchaseWriteReceiptsTb WHERE RequestId=@Id", intent.RequestId) == 0m,
                        "The unposted attempt must not have a server receipt before it is closed.");

                    PurchaseReceiptObservation closed = coordinator.ConfirmAbandon();
                    Require(closed != null && closed.IsAbandoned, "The unposted attempt must report a closed state.");
                    Require(coordinator.Pending() == null, "A closed attempt must not leave a pending file.");
                    Require(Scalar("SELECT COUNT(*) FROM dbo.PurchaseWriteReceiptsTb WHERE RequestId=@Id", intent.RequestId) == 1m,
                        "A closed attempt must leave one durable closed marker.");

                    // The closed attempt reports the closed state instead of an unknown outcome.
                    bool reported = false;
                    try { operation.ObserveIdentified(intent); }
                    catch (PurchaseAttemptAbandonedException) { reported = true; }
                    Require(reported, "Observing a closed attempt must report that it was closed.");
                }

                FinancialCounts after = FinancialCounts.Read();
                Require(after.PurchaseHeaders == before.PurchaseHeaders, "Closing an attempt must not create a purchase header.");
                Require(after.PurchaseLines == before.PurchaseLines, "Closing an attempt must not create a purchase line.");
                Require(after.Journals == before.Journals, "Closing an attempt must not post a journal.");
                Require(after.Movements == before.Movements, "Closing an attempt must not post an inventory movement.");

                Console.WriteLine("QA purchase abandon passed: an attempt without a financial write was closed, its pending file was removed, the closed state is durable and no financial row changed. Database={0}.", expectedDatabase);
            }
            finally
            {
                SessionContext.Clear();
                try { if (Directory.Exists(directory)) Directory.Delete(directory, true); } catch { }
            }
        }

        public static void RunConcurrency(string ownerToken)
        {
            string expectedDatabase = QaSalesInvoiceSelfTests.RequireOwnedTemporaryQa(ownerToken);
            StartOwnedManager();
            try
            {
                var context = Prepare();
                var operation = new PurchaseInvoiceOperation();
                var intent = new PurchaseWriteIntent(Guid.NewGuid(), SessionContext.CurrentEmployeeId,
                    new OperationActor { UserName = SessionContext.CurrentUser, UserRole = SessionContext.CurrentRole },
                    context.Request, context.Request.PurchaseDate, PurchaseWriteSourceFingerprint.Compute(Database.ConnectionString));

                var results = new PurchaseInvoiceResult[2];
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
                            try { results[slot] = operation.IssueIdentified(intent); }
                            catch (Exception error) { failures[slot] = error; }
                        });
                        threads[index].IsBackground = true;
                    }
                    foreach (var thread in threads) thread.Start();
                    foreach (var thread in threads) Require(thread.Join(120000), "A concurrent purchase attempt did not finish in time.");
                }

                Require(failures[0] == null && failures[1] == null,
                    "A concurrent duplicate purchase attempt must observe the original receipt, not fail: " +
                    Describe(failures[0]) + " | " + Describe(failures[1]));
                Require(results[0] != null && results[1] != null, "Both concurrent purchase attempts must return a result.");
                Require(results[0].PurchaseId == results[1].PurchaseId,
                    "The two concurrent purchase attempts must report the same purchase: " + results[0].PurchaseId + " / " + results[1].PurchaseId);
                Require(results[0].TotalAmount == results[1].TotalAmount,
                    "The two concurrent purchase attempts must report the same total.");

                string purchaseId = results[0].PurchaseId;
                Require(Scalar("SELECT COUNT(*) FROM dbo.PurchasesTb WHERE PurchaseId=@Value", purchaseId) == 1m,
                    "The concurrent purchase attempts must leave exactly one purchase header.");
                Require(Scalar("SELECT COUNT(*) FROM dbo.PurchaseWriteReceiptsTb WHERE RequestId=@Id", intent.RequestId) == 1m,
                    "The concurrent purchase attempts must leave exactly one durable receipt.");
                Require(Scalar("SELECT COUNT(*) FROM dbo.JournalEntriesTb WHERE SourceType=N'PurchaseInvoice' AND SourceId=@Value", purchaseId) == 1m,
                    "The concurrent purchase attempts must post exactly one journal entry.");

                Console.WriteLine("QA purchase concurrency passed: two connections submitted the same request id at once; one purchase, one receipt and one journal were committed. Database={0}; PurchaseId={1}; RequestId={2}.",
                    expectedDatabase, purchaseId, intent.RequestId);
            }
            finally
            {
                SessionContext.Clear();
            }
        }

        private sealed class PurchaseContext
        {
            internal PurchaseInvoiceRequest Request;
        }

        private static PurchaseContext Prepare()
        {
            string supplier;
            string medicine;
            using (SqlConnection connection = Database.CreateConnection())
            {
                connection.Open();
                using (SqlCommand command = new SqlCommand(
                    "SELECT TOP 1 CompName FROM dbo.CompanyTb1 WHERE LEN(LTRIM(RTRIM(CompName)))>0 ORDER BY CompName", connection))
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    Require(reader.Read(), "The owned clone has no supplier for the owned purchase.");
                    supplier = reader.GetString(0);
                }
                using (SqlCommand command = new SqlCommand(
                    "SELECT TOP 1 MedName FROM dbo.MedicineTb1 WHERE LEN(LTRIM(RTRIM(MedName)))>0 AND ExpDate>GETDATE() ORDER BY MedName", connection))
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    Require(reader.Read(), "The owned clone has no medicine for the owned purchase.");
                    medicine = reader.GetString(0);
                }
            }
            string currency = Database.GetSystemCurrency().CurrencyCode ?? string.Empty;
            return new PurchaseContext
            {
                Request = new PurchaseInvoiceRequest
                {
                    SupplierName = supplier,
                    EmployeeName = SessionContext.CurrentUser,
                    ActorRole = SessionContext.CurrentRole,
                    PurchaseDate = DateTime.Now,
                    Notes = "QA purchase idempotency",
                    CommercialInventoryExpected = false,
                    ExpectedSystemCurrencyCode = string.IsNullOrWhiteSpace(currency) ? null : currency.Trim().ToUpperInvariant(),
                    Lines = new[]
                    {
                        new PurchaseInvoiceLineRequest
                        {
                            LineNumber = 1,
                            MedicineName = medicine,
                            BuyingPrice = 7,
                            Quantity = 2,
                            ProductionDate = new DateTime(2025, 1, 2),
                            ExpiryDate = new DateTime(2030, 1, 2)
                        }
                    }
                }
            };
        }

        // The financial row counters make the "no second purchase" claim independent of the
        // generated purchase reference.
        internal sealed class FinancialCounts
        {
            internal long PurchaseHeaders;
            internal long PurchaseLines;
            internal long Journals;
            internal long Movements;
            internal long Receipts;

            internal static FinancialCounts Read()
            {
                var counts = new FinancialCounts();
                counts.PurchaseHeaders = (long)Scalar("SELECT COUNT(*) FROM dbo.PurchasesTb", "x");
                counts.PurchaseLines = (long)Scalar("SELECT COUNT(*) FROM dbo.PurchaseDetailsTb", "x");
                counts.Journals = (long)Scalar("SELECT COUNT(*) FROM dbo.JournalEntriesTb", "x");
                counts.Movements = (long)Scalar("SELECT COUNT(*) FROM dbo.InventoryMovementsTb", "x");
                counts.Receipts = (long)Scalar("SELECT COUNT(*) FROM dbo.PurchaseWriteReceiptsTb", "x");
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

        // The commit authority now verifies the employee inside the financial transaction, so the
        // owned scenario must run as a real employee from the clone instead of a fabricated id.
        private static void StartOwnedManager()
        {
            using (SqlConnection connection = Database.CreateConnection())
            using (SqlCommand command = new SqlCommand("SELECT TOP 1 EmpId,EmpName,EmpRole FROM dbo.EmployeeTb1 WHERE EmpRole=N'Manager' ORDER BY EmpId", connection))
            {
                connection.Open();
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    Require(reader.Read(), "The owned clone has no manager employee for the purchase scenario.");
                    SessionContext.Start(reader.GetString(1), Convert.ToString(reader[2]), reader.GetString(0));
                }
            }
        }
        private static void Require(bool condition, string message)
        {
            if (condition) return;
            Console.Error.WriteLine("QA_PURCHASE_IDEMPOTENCY_ASSERT: " + message);
            throw new InvalidOperationException(message);
        }
    }
}

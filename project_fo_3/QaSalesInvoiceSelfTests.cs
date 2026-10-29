using System;
using System.Data;
using System.Data.SqlClient;

namespace project_fo_3
{
    internal static class QaSalesInvoiceSelfTests
    {
        public static void Run(string backupPath, string temporaryDatabaseOwnerToken)
        {
            string expectedDatabase = RequireOwnedTemporaryQa(temporaryDatabaseOwnerToken);
            string medicine;
            int price;
            int stock;
            using (SqlConnection connection = Database.CreateConnection())
            {
                connection.Open();
                QaTaxPolicySelfTests.VerifyBackup(connection, backupPath);
                Require(connection.Database == expectedDatabase, "Actual database is not the owned temporary QA clone.");
                Require(LegacySalesInvoiceRepository.HasSchema(connection, null), "QA invoice archive schema is missing; no automatic schema changes allowed.");
                Require(!CommercialSchemaInstaller.IsFullCommercialMode(connection, null), "This check targets the current legacy mapped sales workflow only.");
                using (var command = new SqlCommand("SELECT TOP 1 MedName, Sprice, MedQty FROM dbo.MedicineTb1 WHERE MedQty > 1 AND Sprice > 0 AND ExpDate > GETDATE() ORDER BY MedName", connection))
                using (var reader = command.ExecuteReader())
                {
                    Require(reader.Read(), "No eligible QA stock.");
                    medicine = reader.GetString(0); price = Convert.ToInt32(reader[1]); stock = Convert.ToInt32(reader[2]);
                }
            }
            DataRow previousTax = AccountingService.GetTaxSettings();
            Require(previousTax != null, "Existing tax row required.");
            string previousCountry = Properties.Setings.Default.TaxCountryCode;
            string invoice = null;
            bool cancelled = false;
            SessionContext.Start("QA invoice verification", "Manager");
            try
            {
                AccountingService.SaveTaxSettings(15m, true, "SA");
                VerifyCurrencyChangeRejection(medicine, price, temporaryDatabaseOwnerToken);
                using (var billing = new Billing()) invoice = billing.SaveQaVerificationInvoice(medicine, price, temporaryDatabaseOwnerToken);
                Require(!string.IsNullOrWhiteSpace(invoice) && !invoice.StartsWith("DRAFT"), "Saved invoice reference missing.");
                Console.WriteLine("QA invoice saved. Reference=" + invoice);
                Require(Stock(medicine) == stock - 1, "Sale did not deduct exactly one unit.");
                decimal expectedNet = price - decimal.Round(price * 0.05m, 2, MidpointRounding.AwayFromZero);
                using (SqlConnection connection = Database.CreateConnection())
                {
                    connection.Open();
                    var archive = LegacySalesInvoiceRepository.GetMappedInvoice(connection, null, invoice);
                    Require(archive.TotalAmount == expectedNet && archive.BillLineIds.Count == 1, "Archived invoice did not reload with the original discounted total.");
                }
                decimal tax = Scalar("SELECT COALESCE(SUM(l.Credit),0) FROM dbo.JournalEntriesTb e JOIN dbo.JournalEntryLinesTb l ON l.JournalEntryId=e.JournalEntryId WHERE e.SourceType=N'SalesInvoice' AND e.SourceId=@Value AND l.AccountCode=N'2100'", invoice);
                Require(tax == InvoiceTotalsCalculator.Round(expectedNet * 15m / 115m), "Posted included tax differs from independent expected amount.");
                AccountingService.SaveTaxSettings(15m, true, "YE");
                string auditFailureTrigger = "QA_RejectSalesCancellationAudit_" + Guid.NewGuid().ToString("N");
                System.Collections.Generic.Dictionary<string, string> beforeRejectedCancellation;
                using (SqlConnection connection = Database.CreateConnection())
                {
                    connection.Open();
                    beforeRejectedCancellation = FinancialWriteSnapshot(connection);
                    using (var command = new SqlCommand("CREATE TRIGGER dbo.[" + auditFailureTrigger + @"] ON dbo.AuditLogTb AFTER INSERT AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM inserted WHERE ActionName=N'SalesInvoiceCancelled' AND EntityName=N'Billing')
        THROW 51990, N'QA rejected sales cancellation audit', 1;
END;", connection)) command.ExecuteNonQuery();
                }
                bool auditFailureRejected = false;
                try
                {
                    ApplicationServices.SalesInvoiceCancellations.Cancel(new PharmacyManagement.Application.SalesInvoiceCancellationRequest
                    {
                        InvoiceReference = invoice,
                        Reason = "QA audit failure must roll back",
                        ActorName = SessionContext.CurrentUser
                    });
                }
                catch (SqlException) { auditFailureRejected = true; }
                finally
                {
                    using (SqlConnection connection = Database.CreateConnection())
                    {
                        connection.Open();
                        using (var command = new SqlCommand("DROP TRIGGER dbo.[" + auditFailureTrigger + "];", connection)) command.ExecuteNonQuery();
                    }
                }
                using (SqlConnection connection = Database.CreateConnection())
                {
                    connection.Open();
                    System.Collections.Generic.Dictionary<string, string> afterRejectedCancellation = FinancialWriteSnapshot(connection);
                    Require(auditFailureRejected, "A failed cancellation audit must reject the financial cancellation.");
                    foreach (var entry in beforeRejectedCancellation)
                        Require(afterRejectedCancellation[entry.Key] == entry.Value, "Rejected sales cancellation changed " + entry.Key + ".");
                    Require(Stock(medicine) == stock - 1 &&
                        Scalar("SELECT COUNT(*) FROM dbo.JournalEntriesTb WHERE SourceType=N'SalesInvoiceCancellation' AND SourceId=@Value", invoice) == 0m,
                        "Rejected sales cancellation changed inventory or posted a reversal.");
                }
                var sessionChangeOperation = new SalesInvoiceCancellationOperation
                {
                    BeforeCommitForVerification = () => SessionContext.Clear()
                };
                bool sessionChangeRejected = false;
                try
                {
                    sessionChangeOperation.Cancel(new PharmacyManagement.Application.SalesInvoiceCancellationRequest
                    {
                        InvoiceReference = invoice,
                        Reason = "QA session change before commit",
                        ActorName = SessionContext.CurrentUser
                    });
                }
                catch (UnauthorizedAccessException) { sessionChangeRejected = true; }
                finally
                {
                    if (!SessionContext.IsAuthenticated) SessionContext.Start("QA invoice verification", "Manager");
                }
                using (SqlConnection connection = Database.CreateConnection())
                {
                    connection.Open();
                    System.Collections.Generic.Dictionary<string, string> afterSessionChange = FinancialWriteSnapshot(connection);
                    Require(sessionChangeRejected, "A session change at the cancellation commit boundary must reject the write.");
                    foreach (var entry in beforeRejectedCancellation)
                        Require(afterSessionChange[entry.Key] == entry.Value, "Session change during sales cancellation changed " + entry.Key + ".");
                    Require(Stock(medicine) == stock - 1 &&
                        Scalar("SELECT COUNT(*) FROM dbo.JournalEntriesTb WHERE SourceType=N'SalesInvoiceCancellation' AND SourceId=@Value", invoice) == 0m,
                        "Session change during sales cancellation changed inventory or posted a reversal.");
                }
                var result = ApplicationServices.SalesInvoiceCancellations.Cancel(new PharmacyManagement.Application.SalesInvoiceCancellationRequest
                {
                    InvoiceReference = invoice,
                    Reason = "QA verification after country change",
                    ActorName = SessionContext.CurrentUser
                });
                cancelled = true;
                Require(result.TotalAmount == expectedNet && result.RestoredQuantity == 1 && Stock(medicine) == stock, "Cancellation did not restore cash/stock.");
                Require(Scalar(@"SELECT COUNT(*) FROM (
SELECT l.AccountCode, SUM(l.Debit-l.Credit) Balance
FROM dbo.JournalEntriesTb e JOIN dbo.JournalEntryLinesTb l ON l.JournalEntryId=e.JournalEntryId
WHERE e.SourceId=@Value AND e.SourceType IN (N'SalesInvoice',N'SalesInvoiceCancellation')
GROUP BY l.AccountCode HAVING SUM(l.Debit-l.Credit)<>0) x", invoice) == 0m, "Original and cancellation do not net to zero for each account.");
                bool duplicateRejected = false;
                try
                {
                    ApplicationServices.SalesInvoiceCancellations.Cancel(new PharmacyManagement.Application.SalesInvoiceCancellationRequest
                    {
                        InvoiceReference = invoice,
                        Reason = "QA duplicate rejection",
                        ActorName = SessionContext.CurrentUser
                    });
                }
                catch (InvalidOperationException) { duplicateRejected = true; }
                Require(duplicateRejected && Stock(medicine) == stock, "Duplicate cancellation changed stock.");
                Require(Scalar("SELECT COUNT(*) FROM dbo.JournalEntriesTb WHERE SourceType=N'SalesInvoiceCancellation' AND SourceId=@Value", invoice) == 1m, "Cancellation was posted more than once.");
                Console.WriteLine("QA archive reload, country-change reversal, per-account zero balance and duplicate rejection passed.");
            }
            finally
            {
                try
                {
                    if (invoice != null && !cancelled &&
                        Scalar("SELECT COUNT(*) FROM dbo.JournalEntriesTb WHERE SourceType=N'SalesInvoiceCancellation' AND SourceId=@Value", invoice) == 0m)
                        ApplicationServices.SalesInvoiceCancellations.Cancel(new PharmacyManagement.Application.SalesInvoiceCancellationRequest
                        {
                            InvoiceReference = invoice,
                            Reason = "QA cleanup after failed assertion",
                            ActorName = SessionContext.CurrentUser
                        });
                }
                finally
                {
                    AccountingService.SaveTaxSettings(Convert.ToDecimal(previousTax["TaxRate"]), Convert.ToBoolean(previousTax["IsEnabled"]));
                    Properties.Setings.Default.TaxCountryCode = previousCountry;
                    Properties.Setings.Default.Save();
                    SessionContext.Clear();
                }
            }
            Console.WriteLine("QA invoice verification finished. Original stock and tax settings restored; invoice/audit records retained.");
        }

        private static void VerifyCurrencyChangeRejection(string medicine, int price, string ownerToken)
        {
            RequireOwnedTemporaryQa(ownerToken);
            int currencyId; string originalCode, changedCode = null;
            using (var connection = Database.CreateConnection())
            {
                connection.Open();
                using (var command = new SqlCommand("SELECT TOP(1) CurrencyId,CurrencyCode FROM dbo.CurrencySettingsTb WHERE IsSystemCurrency=1 ORDER BY CurrencyId", connection))
                using (var reader = command.ExecuteReader())
                {
                    Require(reader.Read(), "QA system currency is required.");
                    currencyId = Convert.ToInt32(reader[0]); originalCode = Convert.ToString(reader[1]);
                }
                for (int index = 0; index < 100 && changedCode == null; index++)
                using (var command = new SqlCommand("SELECT COUNT(*) FROM dbo.CurrencySettingsTb WHERE CurrencyCode=@Code", connection))
                {
                    string candidate = "QA" + index.ToString("D2");
                    command.Parameters.Add("@Code", SqlDbType.NVarChar, 10).Value = candidate;
                    if (Convert.ToInt32(command.ExecuteScalar()) == 0) changedCode = candidate;
                }
                Require(changedCode != null, "No unused QA currency code.");
                using (var billing = new Billing())
                {
                    var request = billing.PrepareQaVerificationSalesRequest(medicine, price, ownerToken);
                    Require(request.ExpectedCurrencyCode == originalCode.Trim(), "Real POS draft did not freeze the original currency.");
                    var before = FinancialWriteSnapshot(connection);
                    bool changed = false;
                    try
                    {
                        ChangeCurrencyCode(connection, currencyId, originalCode, changedCode); changed = true;
                        bool rejected = false;
                        try { ApplicationServices.SalesInvoices.Issue(request); }
                        catch (InvalidOperationException exception)
                        {
                            rejected = exception.Message == "تغيرت عملة النظام منذ إعداد المسودة. افتح فاتورة جديدة بالعملة الصحيحة؛ لا يحدث تحويل تلقائي.";
                            if (!rejected) throw;
                        }
                        Require(rejected, "Real issue accepted a currency changed after the POS draft.");
                        var after = FinancialWriteSnapshot(connection);
                        foreach (var entry in before)
                            Require(after[entry.Key] == entry.Value, "Rejected currency issue changed persistent rows: " + entry.Key);
                        Require(request.ExpectedCurrencyCode == originalCode.Trim(), "Issue silently replaced the POS currency.");
                        Console.WriteLine("PASS QA real POS currency-change issue rejection: draft expected code preserved; all rows unchanged in " + before.Count + " invoice/customer/inventory/accounting/audit tables.");
                    }
                    finally { if (changed) ChangeCurrencyCode(connection, currencyId, changedCode, originalCode); }
                    var restored = FinancialWriteSnapshot(connection);
                    foreach (var entry in before) Require(restored[entry.Key] == entry.Value, "QA rejection cleanup changed table: " + entry.Key);
                }
            }
        }

        private static void ChangeCurrencyCode(SqlConnection connection, int id, string expected, string replacement)
        {
            using (var command = new SqlCommand("UPDATE dbo.CurrencySettingsTb SET CurrencyCode=@Replacement WHERE CurrencyId=@Id AND CurrencyCode=@Expected AND IsSystemCurrency=1", connection))
            {
                command.Parameters.Add("@Id", SqlDbType.Int).Value = id;
                command.Parameters.Add("@Expected", SqlDbType.NVarChar, 10).Value = expected;
                command.Parameters.Add("@Replacement", SqlDbType.NVarChar, 10).Value = replacement;
                Require(command.ExecuteNonQuery() == 1, "QA currency identity changed unexpectedly; update refused.");
            }
        }

        private static System.Collections.Generic.Dictionary<string,string> FinancialWriteSnapshot(SqlConnection connection)
        {
            var result = new System.Collections.Generic.Dictionary<string,string>();
            foreach (string table in new[] { "BillsTb1", "CustomersBillsTb", "LegacySalesInvoiceHeadersTb", "LegacySalesInvoiceLinesTb",
                "MedicineTb1", "InventoryMovementsTb", "InventoryLotsTb", "JournalEntriesTb", "JournalEntryLinesTb",
                "CashBoxJournalLinksTb", "AccountingVouchersTb", "AuditLogTb", "CurrencySettingsTb" })
            {
                // Compare sorted XML for every complete row; row counts alone cannot detect an UPDATE.
                // Currency is checked after restoration, separately from the deliberate configuration edit.
                if (table == "CurrencySettingsTb") continue;
                var rows = new System.Collections.Generic.List<string>();
                using (var command = new SqlCommand("SELECT CONVERT(nvarchar(max),(SELECT t.* FOR XML RAW, BINARY BASE64, TYPE)) FROM dbo.[" + table + "] t", connection))
                using (var reader = command.ExecuteReader()) while (reader.Read()) rows.Add(reader.GetString(0));
                rows.Sort(StringComparer.Ordinal);
                using (var sha = System.Security.Cryptography.SHA256.Create())
                    result[table] = BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(string.Join("\n", rows))));
            }
            return result;
        }
        internal static string RequireOwnedTemporaryQa(string expectedOwnerToken)
        {
            if (!RuntimeSettingsStore.IsBuildConnectionProfileForced || string.IsNullOrWhiteSpace(expectedOwnerToken) || expectedOwnerToken.Length != 32)
                throw new InvalidOperationException("Sale write verification requires a forced connection and exact temporary QA owner token.");
            string databaseName = Database.GetConnectionProfile().DatabaseName;
            string[] parts = (databaseName ?? string.Empty).Split('_');
            DateTime created;
            Guid ownerGuid;
            if (parts.Length != 4 || parts[0] != "PharmacyRestore" || parts[3] != "qa" ||
                !DateTime.TryParseExact(parts[1], "yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out created) ||
                !Guid.TryParseExact(parts[2], "N", out ownerGuid) ||
                !string.Equals(parts[2], expectedOwnerToken, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Sale write verification only permits a specifically named temporary QA clone.");

            using (SqlConnection connection = Database.CreateConnection())
            using (var command = connection.CreateCommand())
            {
                connection.Open();
                command.CommandText = "SELECT DB_NAME(); SELECT CONVERT(nvarchar(128), value) FROM sys.extended_properties WHERE class=0 AND name=N'QaTest.Owner';";
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    Require(reader.Read() && string.Equals(Convert.ToString(reader.GetValue(0)), databaseName, StringComparison.Ordinal),
                        "Connected database does not match the owned QA clone name.");
                    Require(reader.NextResult() && reader.Read() && string.Equals(Convert.ToString(reader.GetValue(0)), expectedOwnerToken, StringComparison.OrdinalIgnoreCase),
                        "Temporary QA database owner token does not match.");
                }
            }
            return databaseName;
        }

        private static int Stock(string medicine)
        {
            return (int)Scalar("SELECT MedQty FROM dbo.MedicineTb1 WHERE MedName=@Value", medicine);
        }

        private static decimal Scalar(string sql, string value)
        {
            using (SqlConnection connection = Database.CreateConnection())
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.Add("@Value", SqlDbType.NVarChar, 200).Value = value;
                connection.Open();
                return Convert.ToDecimal(command.ExecuteScalar());
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}

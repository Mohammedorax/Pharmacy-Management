using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using PharmacyManagement.Application;
using PharmacyManagement.Infrastructure.SqlServer;

namespace PharmacyManagement.CoreTests
{
    internal static class SalesHistoryTests
    {
        internal static int Run()
        {
            int checks = 0;
            var repository = new FakeRepository();
            var service = new SalesHistoryService(repository);

            SalesHistoryPage page = service.Load(false);
            Check(page.Mode == SalesHistoryMode.CustomerSummary && !repository.LastLegacyFlag,
                "Sales history mode was not returned through the service.", ref checks);
            service.LoadCustomerLines("  C-1  ");
            Check(repository.LastValue == "C-1", "Customer identity was not normalized.", ref checks);
            service.LoadLegacyInvoiceLines("  INV-1  ");
            Check(repository.LastValue == "INV-1", "Invoice reference was not normalized.", ref checks);
            Guid id = Guid.NewGuid();
            service.LoadCommercialInvoiceLines(id);
            Check(repository.LastInvoiceId == id, "Commercial invoice identity was not preserved.", ref checks);
            Reject(() => service.LoadCustomerLines(" "), "Blank customer identity was accepted.", ref checks);
            Reject(() => service.LoadLegacyInvoiceLines(new string('X', 51)), "Oversized invoice reference was accepted.", ref checks);
            Reject(() => service.LoadCommercialInvoiceLines(Guid.Empty), "Empty commercial invoice identity was accepted.", ref checks);
            return checks;
        }

        internal static void VerifyQaReadOnly(string server, string database)
        {
            string normalized = (database ?? string.Empty).ToLowerInvariant();
            if (!(normalized.EndsWith("_qa") || normalized.EndsWith("_test") || normalized.EndsWith("_sandbox")))
                throw new InvalidOperationException("Explicit QA database suffix required before connection.");

            var builder = new SqlConnectionStringBuilder
            {
                DataSource = server,
                InitialCatalog = database,
                IntegratedSecurity = true,
                ConnectTimeout = 5,
                ApplicationName = "Pharmacy.CoreTests.SalesHistory.ReadOnly"
            };
            var repository = new SqlSalesHistoryRepository(() => new SqlConnection(builder.ConnectionString));
            SalesHistoryPage page = repository.Load(false);
            if (page == null || page.Records == null)
                throw new InvalidOperationException("Typed sales history read returned an incomplete page.");

            int previewLines = 0;
            if (page.Records.Count > 0)
            {
                SalesHistoryRecord first = page.Records[0];
                IReadOnlyList<SalesHistoryLine> lines;
                if (page.Mode == SalesHistoryMode.CommercialInvoice)
                    lines = repository.LoadCommercialInvoiceLines(first.InvoiceId.GetValueOrDefault());
                else if (page.Mode == SalesHistoryMode.LegacyInvoice)
                    lines = repository.LoadLegacyInvoiceLines(first.InvoiceReference);
                else
                    lines = repository.LoadCustomerLines(first.CustomerId);
                previewLines = lines.Count;
            }

            Console.WriteLine("PASS: typed sales history reads. Database=" + database +
                "; Mode=" + page.Mode + "; Records=" + page.Records.Count +
                "; PreviewLines=" + previewLines + "; PersistentWrites=0.");
        }

        private static void Check(bool condition, string message, ref int checks)
        {
            checks++;
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void Reject(Action action, string message, ref int checks)
        {
            try { action(); }
            catch (InvalidOperationException) { checks++; return; }
            throw new InvalidOperationException(message);
        }

        private sealed class FakeRepository : ISalesHistoryRepository
        {
            internal bool LastLegacyFlag;
            internal string LastValue;
            internal Guid LastInvoiceId;

            public SalesHistoryPage Load(bool showLegacyInvoices)
            {
                LastLegacyFlag = showLegacyInvoices;
                return new SalesHistoryPage
                {
                    Mode = SalesHistoryMode.CustomerSummary,
                    HasLegacyArchive = true,
                    Records = new SalesHistoryRecord[0]
                };
            }

            public IReadOnlyList<SalesHistoryLine> LoadCustomerLines(string customerId)
            {
                LastValue = customerId;
                return new SalesHistoryLine[0];
            }

            public IReadOnlyList<SalesHistoryLine> LoadLegacyInvoiceLines(string invoiceReference)
            {
                LastValue = invoiceReference;
                return new SalesHistoryLine[0];
            }

            public IReadOnlyList<SalesHistoryLine> LoadCommercialInvoiceLines(Guid invoiceId)
            {
                LastInvoiceId = invoiceId;
                return new SalesHistoryLine[0];
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using PharmacyManagement.Application;
using PharmacyManagement.Infrastructure.SqlServer;

namespace PharmacyManagement.CoreTests
{
    internal static class PurchaseWorkspaceTests
    {
        internal static int Run()
        {
            int checks = 0;
            var repository = new FakeRepository();
            var service = new PurchaseWorkspaceService(repository);

            Check(service.GetHistory().Count == 1, "Purchase history was not returned.", ref checks);
            service.GetHistoryLines("  P-1  ");
            Check(repository.LastPurchaseId == "P-1", "Purchase identity was not normalized.", ref checks);
            service.GetMedicineNames("  مورد  ");
            Check(repository.LastSupplier == "مورد", "Supplier filter was not normalized.", ref checks);
            PurchaseMedicineSnapshot medicine = service.FindMedicine("  دواء  ");
            Check(repository.LastMedicine == "دواء" && medicine.BuyingPrice == 25,
                "Medicine lookup did not preserve the typed snapshot.", ref checks);
            Reject(() => service.GetHistoryLines(" "), "Blank purchase identity was accepted.", ref checks);
            Reject(() => service.FindMedicine(new string('م', 201)), "Oversized medicine name was accepted.", ref checks);
            Reject(() => service.GetMedicineNames(new string('س', 201)), "Oversized supplier name was accepted.", ref checks);
            PurchaseInvoiceRequest valid = PurchaseInvoiceValidation.Normalize(ValidInvoice(), new DateTime(2026, 9, 21));
            Check(valid.SupplierName == "مورد" && PurchaseInvoiceValidation.CalculateTotal(valid.Lines) == 50m,
                "Purchase invoice validation changed a valid total.", ref checks);
            Reject(() => PurchaseInvoiceValidation.Normalize(InvoiceWith(line => line.Quantity = 0), new DateTime(2026, 9, 21)),
                "Zero purchase quantity was accepted.", ref checks);
            Reject(() => PurchaseInvoiceValidation.Normalize(InvoiceWith(line => line.ProductionDate = new DateTime(2026, 9, 22)), new DateTime(2026, 9, 21)),
                "Future production date was accepted.", ref checks);
            Reject(() => PurchaseInvoiceValidation.Normalize(InvoiceWith(line => line.ExpiryDate = line.ProductionDate), new DateTime(2026, 9, 21)),
                "Expiry not after production was accepted.", ref checks);
            Reject(() => PurchaseInvoiceValidation.Normalize(InvoiceWith(line => { }, true), new DateTime(2026, 9, 21)),
                "Commercial purchase without a batch was accepted.", ref checks);
            return checks;
        }

        private static PurchaseInvoiceRequest ValidInvoice()
        {
            return new PurchaseInvoiceRequest
            {
                SupplierName = " مورد ", EmployeeName = "admin", PurchaseDate = new DateTime(2026, 9, 21),
                Lines = new[] { new PurchaseInvoiceLineRequest { LineNumber = 1, MedicineName = "دواء", BuyingPrice = 25,
                    Quantity = 2, ProductionDate = new DateTime(2026, 9, 20), ExpiryDate = new DateTime(2027, 9, 20) } }
            };
        }

        private static PurchaseInvoiceRequest InvoiceWith(Action<PurchaseInvoiceLineRequest> change)
        {
            PurchaseInvoiceRequest request = ValidInvoice();
            change((PurchaseInvoiceLineRequest)request.Lines[0]);
            return request;
        }

        private static PurchaseInvoiceRequest InvoiceWith(Action<PurchaseInvoiceLineRequest> change, bool commercial)
        {
            PurchaseInvoiceRequest request = InvoiceWith(change);
            request.CommercialInventoryExpected = commercial;
            return request;
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
                ApplicationName = "Pharmacy.CoreTests.PurchaseWorkspace.ReadOnly"
            };
            var repository = new SqlPurchaseWorkspaceRepository(() => new SqlConnection(builder.ConnectionString));
            IReadOnlyList<PurchaseHistoryRecord> history = repository.GetHistory();
            IReadOnlyList<string> suppliers = repository.GetSupplierNames();
            IReadOnlyList<string> medicines = repository.GetMedicineNames(string.Empty);
            IReadOnlyList<PurchaseEmployeeChoice> employees = repository.GetEmployees();
            int lines = 0;
            if (history.Count > 0) lines = repository.GetHistoryLines(history[0].PurchaseId).Count;
            if (medicines.Count > 0 && repository.FindMedicine(medicines[0]) == null)
                throw new InvalidOperationException("Typed purchase medicine lookup lost an existing record.");

            Console.WriteLine("PASS: typed purchase workspace reads. Database=" + database +
                "; History=" + history.Count + "; Lines=" + lines + "; Suppliers=" + suppliers.Count +
                "; Medicines=" + medicines.Count + "; Employees=" + employees.Count + "; PersistentWrites=0.");
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

        private sealed class FakeRepository : IPurchaseWorkspaceRepository
        {
            internal string LastPurchaseId;
            internal string LastSupplier;
            internal string LastMedicine;

            public IReadOnlyList<PurchaseHistoryRecord> GetHistory() =>
                new[] { new PurchaseHistoryRecord { PurchaseId = "P-1" } };

            public IReadOnlyList<PurchaseHistoryLine> GetHistoryLines(string purchaseId)
            {
                LastPurchaseId = purchaseId;
                return new PurchaseHistoryLine[0];
            }

            public IReadOnlyList<string> GetSupplierNames() => new[] { "مورد" };

            public IReadOnlyList<string> GetMedicineNames(string supplierName)
            {
                LastSupplier = supplierName;
                return new[] { "دواء" };
            }

            public IReadOnlyList<PurchaseEmployeeChoice> GetEmployees() => new PurchaseEmployeeChoice[0];

            public PurchaseMedicineSnapshot FindMedicine(string medicineName)
            {
                LastMedicine = medicineName;
                return new PurchaseMedicineSnapshot { MedicineName = medicineName, BuyingPrice = 25, StockQuantity = 10 };
            }
        }
    }
}

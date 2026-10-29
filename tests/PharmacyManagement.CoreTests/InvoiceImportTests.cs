using System;
using System.Linq;
using PharmacyManagement.Application;

namespace PharmacyManagement.CoreTests
{
    internal static class InvoiceImportTests
    {
        internal static int Run()
        {
            int checks = 0;
            foreach (InvoiceImportMode mode in new[] { InvoiceImportMode.Draft, InvoiceImportMode.ImportAndApprove })
            {
                var row = Row(1);
                var result = InvoiceImportValidation.Validate(mode, "Legacy", new[] { Row(2), row });
                Check(result.Count == 1 && result[0].Mode == mode && result[0].Lines[0].LineNumber == 1, "group/order/mode"); checks++;
                row.Quantity = 999;
                result[0].Lines[0].Quantity = 888;
                Check(result[0].Lines[0].Quantity == 1.25m && result[0].Lines[0].UnitPrice == 12.345m, "snapshot/decimal preservation"); checks++;
                Check(result[0].PartyReference == "C-001" && result[0].TaxRatePercent == 0m &&
                    result[0].PaymentMethod == "Cash" &&
                    result[0].Lines[0].BatchNumber == "B-01", "extended metadata snapshot"); checks++;
                Reject(() => InvoiceImportValidation.Validate(mode, "Legacy", new[] { Row(1), Row(1) })); checks++;
                var conflict = Row(2); conflict.CurrencyCode = "USD";
                Reject(() => InvoiceImportValidation.Validate(mode, "Legacy", new[] { Row(1), conflict })); checks++;
                conflict = Row(2); conflict.PartyName = "Other";
                Reject(() => InvoiceImportValidation.Validate(mode, "Legacy", new[] { Row(1), conflict })); checks++;
                conflict = Row(2); conflict.InvoiceDate = conflict.InvoiceDate.AddDays(1);
                Reject(() => InvoiceImportValidation.Validate(mode, "Legacy", new[] { Row(1), conflict })); checks++;
                conflict = Row(2); conflict.PartyReference = "C-OTHER";
                Reject(() => InvoiceImportValidation.Validate(mode, "Legacy", new[] { Row(1), conflict })); checks++;
                conflict = Row(2); conflict.PaymentMethod = "Payable";
                Reject(() => InvoiceImportValidation.Validate(mode, "Legacy", new[] { Row(1), conflict })); checks++;
            }
            var purchase = Row(1); purchase.Kind = ImportedInvoiceKind.Purchase;
            Check(InvoiceImportValidation.Validate(InvoiceImportMode.Draft, "Legacy", new[] { Row(1), purchase }).Count == 2, "sale/purchase namespace"); checks++;
            foreach (InvoiceImportMode mode in new[] { InvoiceImportMode.Draft, InvoiceImportMode.ImportAndApprove })
            {
                var second = Row(2); second.SourceInvoiceId = "inv-001";
                var first = Row(1); first.SourceInvoiceId = "INV-001";
                var sameInvoice = InvoiceImportValidation.Validate(mode, "Legacy", new[] { first, second });
                Check(sameInvoice.Count == 1 && sameInvoice[0].SourceInvoiceId == "INV-001" &&
                    sameInvoice[0].Lines.Count == 2 && sameInvoice[0].Lines[1].LineNumber == 2,
                    "case-only source ID must remain one invoice in both modes"); checks++;
                second.LineNumber = 1;
                Reject(() => InvoiceImportValidation.Validate(mode, "Legacy", new[] { first, second })); checks++;
                second.Kind = ImportedInvoiceKind.Purchase;
                Check(InvoiceImportValidation.Validate(mode, "Legacy", new[] { first, second }).Count == 2,
                    "sale and purchase IDs stay separate even when their case differs"); checks++;
            }
            Reject(() => InvoiceImportValidation.Validate(InvoiceImportMode.Draft, "Legacy", new InvoiceImportRow[0])); checks++;
            Reject(() => InvoiceImportValidation.Validate(InvoiceImportMode.Draft, "Legacy", Enumerable.Range(1, 5001).Select(Row))); checks++;
            var invalid = Row(1); invalid.Quantity = 0;
            Reject(() => InvoiceImportValidation.Validate(InvoiceImportMode.Draft, "Legacy", new[] { invalid })); checks++;
            invalid = Row(1); invalid.UnitPrice = -1;
            Reject(() => InvoiceImportValidation.Validate(InvoiceImportMode.ImportAndApprove, "Legacy", new[] { invalid })); checks++;
            invalid = Row(1); invalid.ExpiryDate = invalid.ProductionDate;
            Reject(() => InvoiceImportValidation.Validate(InvoiceImportMode.Draft, "Legacy", new[] { invalid })); checks++;
            invalid = Row(1); invalid.TaxRatePercent = 101m;
            Reject(() => InvoiceImportValidation.Validate(InvoiceImportMode.Draft, "Legacy", new[] { invalid })); checks++;
            string system, invoice;
            Check(!InvoiceImportSourceIdentity.TryNormalize(null, "  ", out system, out invoice) && system == "" && invoice == "", "ordinary invoice has no import identity"); checks++;
            Check(InvoiceImportSourceIdentity.TryNormalize("  Legacy ", " 001 ", out system, out invoice) && system == "Legacy" && invoice == "001", "source identity is trimmed"); checks++;
            Reject(() => { string ignoredSystem, ignoredInvoice; InvoiceImportSourceIdentity.TryNormalize("Legacy", null, out ignoredSystem, out ignoredInvoice); }); checks++;
            Reject(() => { string ignoredSystem, ignoredInvoice; InvoiceImportSourceIdentity.TryNormalize(null, "001", out ignoredSystem, out ignoredInvoice); }); checks++;
            Reject(() => { string ignoredSystem, ignoredInvoice; InvoiceImportSourceIdentity.TryNormalize(new string('x', 101), "001", out ignoredSystem, out ignoredInvoice); }); checks++;
            Reject(() => { string ignoredSystem, ignoredInvoice; InvoiceImportSourceIdentity.TryNormalize("Legacy", new string('x', 101), out ignoredSystem, out ignoredInvoice); }); checks++;            Console.WriteLine("PASS: " + checks + " invoice import staging assertions; no persistence or posting.");
            return checks;
        }
        private static InvoiceImportRow Row(int line)
        {
            return new InvoiceImportRow { SourceInvoiceId = "001", LineNumber = line, Kind = ImportedInvoiceKind.Sale,
                InvoiceDate = new DateTime(2026, 1, 1), PartyName = "عميل 500 mg", CurrencyCode = "YER",
                MedicineName = "باراسيتامول 500 mg", Barcode = "000123", Quantity = 1.25m, UnitPrice = 12.345m,
                PartyReference = "C-001", PartyPhone = "000000000", InvoiceDiscountPercent = 0m, TaxRatePercent = 0m,
                PriceTaxMode = "Inclusive", Notes = "QA", PaymentMethod = "Cash", ProductionDate = new DateTime(2025, 1, 1),
                ExpiryDate = new DateTime(2027, 1, 1), BatchNumber = "B-01", Gtin = "01234567890128" };
        }
        private static void Check(bool pass, string detail) { if (!pass) throw new InvalidOperationException("Import staging: " + detail); }
        private static void Reject(Action action)
        {
            try { action(); } catch (InvalidOperationException) { return; }
            throw new Exception("Invalid invoice import was accepted.");
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Xml.Linq;

namespace project_fo_3
{
    internal static class InvoiceCalculationRegressionTests
    {
        public static void RunAll()
        {
            SmallDiscountsStayWithinEachLine();
            ZeroValueTailNeverReceivesDiscount();
            FractionalQuantityWithFullLineDiscountHasZeroTax();
            MixedTaxAllocationDoesNotDependOnDisplayOrder();
            PublishedExamplesStayCorrect();
            AllocationsPreserveMoneyAcrossGeneratedCases();
            SandboxXmlPreservesRoundedLineAmounts();
        }

        private static CommercialInvoiceLine Line(string name, decimal price, decimal tax = 15m,
            CommercialPriceTaxMode mode = CommercialPriceTaxMode.Exclusive)
        {
            return new CommercialInvoiceLine { MedicineName = name, Quantity = 1m,
                UnitPrice = price, TaxRatePercent = tax, PriceTaxMode = mode };
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        public static void SmallDiscountsStayWithinEachLine()
        {
            var lines = Enumerable.Range(0, 6).Select(i => Line("Item-" + i, .01m)).ToArray();
            var discounts = InvoiceTotalsCalculator.CalculateAllocatedInvoiceDiscounts(lines, 50m);
            Check(discounts.Sum() == .03m, "CAL-01: total discount must be 0.03.");
            Check(discounts.All(x => x >= 0m && x <= .01m),
                "CAL-01: allocation escaped line bounds: " + string.Join(",", discounts));
        }

        public static void ZeroValueTailNeverReceivesDiscount()
        {
            var lines = new[] { Line("A", .01m), Line("B", .01m), Line("C", .01m), Line("Free", 0m) };
            var discounts = InvoiceTotalsCalculator.CalculateAllocatedInvoiceDiscounts(lines, 50m);
            Check(discounts[3] == 0m && discounts.Sum() == .02m,
                "CAL-01: a free line received a document discount.");
        }

        public static void FractionalQuantityWithFullLineDiscountHasZeroTax()
        {
            foreach (CommercialPriceTaxMode mode in Enum.GetValues(typeof(CommercialPriceTaxMode)))
            {
                var line = Line("HalfUnit", .01m, 100m, mode);
                line.Quantity = .5m;
                line.LineDiscount = .01m;
                var totals = InvoiceTotalsCalculator.Calculate(new[] { line }, 0m);
                var taxes = InvoiceTotalsCalculator.CalculateLineTaxAmounts(new[] { line }, 0m);
                Check(totals.NetTotal == 0m && taxes[0] == 0m,
                    "CAL-05: a fully discounted rounded line must have zero tax.");
            }
        }

        public static void MixedTaxAllocationDoesNotDependOnDisplayOrder()
        {
            var a = Line("A", .05m, 0m);
            var b = Line("B", .05m, 100m);
            var forward = InvoiceTotalsCalculator.Calculate(new[] { a, b }, 10m);
            var reverse = InvoiceTotalsCalculator.Calculate(new[] { b, a }, 10m);
            Check(forward.TaxTotal == reverse.TaxTotal && forward.NetTotal == reverse.NetTotal,
                "CAL-06: changing display order changed invoice tax or net.");
        }

        public static void PublishedExamplesStayCorrect()
        {
            var first = Line("A", 100m);
            first.Quantity = 2m;
            first.LineDiscount = 10m;
            var totals = InvoiceTotalsCalculator.Calculate(new[] { first }, 10m);
            Check(totals.TaxableTotal == 171m && totals.TaxTotal == 25.65m && totals.NetTotal == 196.65m,
                "CAL-02: exclusive reference invoice changed.");
            var inclusive = InvoiceTotalsCalculator.Calculate(new[] { Line("A", 115m, 15m, CommercialPriceTaxMode.Inclusive) }, 0m);
            Check(inclusive.TaxableTotal == 100m && inclusive.TaxTotal == 15m && inclusive.NetTotal == 115m,
                "CAL-02: inclusive reference invoice changed.");
            var mixed = InvoiceTotalsCalculator.Calculate(new[] { Line("A", 100m, 15m), Line("B", 100m, 0m) }, 10m);
            Check(mixed.TaxTotal == 13.50m && mixed.NetTotal == 193.50m,
                "CAL-03: mixed-tax reference invoice changed.");
        }

        public static void SandboxXmlPreservesRoundedLineAmounts()
        {
            foreach (CommercialPriceTaxMode mode in Enum.GetValues(typeof(CommercialPriceTaxMode)))
            {
                var line = Line("HalfUnit", .01m, 100m, mode);
                line.Quantity = .5m;
                line.LineDiscount = .01m;
                var lines = new[] { line };
                var totals = InvoiceTotalsCalculator.Calculate(lines, 0m);
                var artifact = new LocalZatcaSandboxProvider().Create(new EInvoiceRequest
                {
                    InvoiceId = Guid.NewGuid(), InvoiceNumber = 1, Kind = EInvoiceDocumentKind.SimplifiedB2C,
                    SellerName = "Regression test", SellerVatNumber = "310000000000003",
                    IssueDateUtc = new DateTime(2026, 9, 12, 0, 0, 0, DateTimeKind.Utc),
                    Totals = totals, Lines = lines
                }, 0, string.Empty, 1, string.Empty);
                var xml = XDocument.Parse(artifact.Xml);
                decimal sum = xml.Descendants("LineTotal").Sum(x => decimal.Parse(x.Value, CultureInfo.InvariantCulture));
                Check(sum == totals.NetTotal && sum == 0m,
                    "CAL-05: sandbox XML disagrees with the rounded, fully discounted invoice.");
                Check(xml.Descendants("TaxableAmount").All(x => decimal.Parse(x.Value, CultureInfo.InvariantCulture) >= 0m),
                    "CAL-05: sandbox XML contains a negative taxable line.");
            }
        }

        public static void AllocationsPreserveMoneyAcrossGeneratedCases()
        {
            var random = new Random(271828);
            decimal[] rates = { 0m, 5m, 15m, 100m };
            decimal[] percentages = { 0m, 1m, 10m, 33.3333m, 50m, 99.99m, 100m };
            for (int sample = 0; sample < 1000; sample++)
            {
                var mode = sample % 2 == 0 ? CommercialPriceTaxMode.Exclusive : CommercialPriceTaxMode.Inclusive;
                var lines = Enumerable.Range(0, random.Next(1, 50)).Select(i =>
                {
                    var line = Line("Item-" + i.ToString("D3"), random.Next(0, 1000) / 100m,
                        rates[random.Next(rates.Length)], mode);
                    line.Quantity = random.Next(1, 2000) / 1000m;
                    decimal roundedGross = decimal.Round(line.Quantity * line.UnitPrice, 2, MidpointRounding.AwayFromZero);
                    line.LineDiscount = random.Next(0, (int)(roundedGross * 100m) + 1) / 100m;
                    return line;
                }).ToArray();
                decimal percent = percentages[sample % percentages.Length];
                var discounts = InvoiceTotalsCalculator.CalculateAllocatedInvoiceDiscounts(lines, percent);
                decimal[] bases = lines.Select(x => decimal.Round(x.Quantity * x.UnitPrice, 2,
                    MidpointRounding.AwayFromZero) - x.LineDiscount).ToArray();
                decimal target = decimal.Round(bases.Sum() * percent / 100m, 2, MidpointRounding.AwayFromZero);
                Check(discounts.Sum() == target, "CAL-06: document discount was not conserved.");
                for (int i = 0; i < lines.Length; i++)
                    Check(discounts[i] >= 0m && discounts[i] <= bases[i], "CAL-06: generated allocation escaped its line.");
                var totals = InvoiceTotalsCalculator.Calculate(lines, percent);
                var taxes = InvoiceTotalsCalculator.CalculateLineTaxAmounts(lines, percent);
                InvoiceTotalsContract.Validate(totals);
                Check(taxes.All(x => x >= 0m) && taxes.Sum() == totals.TaxTotal,
                    "CAL-06: generated line tax is negative or disagrees with header.");
                var reverse = InvoiceTotalsCalculator.Calculate(lines.Reverse(), percent);
                Check(reverse.NetTotal == totals.NetTotal && reverse.TaxTotal == totals.TaxTotal,
                    "CAL-06: generated invoice depends on display order.");
            }
        }
    }
}

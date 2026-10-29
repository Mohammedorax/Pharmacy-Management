using System;
using PharmacyManagement.Domain;
namespace PharmacyManagement.CoreTests
{
    internal static class JournalAmountCalculatorTests
    {
        private static int checks;
        private static void Check(bool condition)
        { if (!condition) throw new Exception("Journal amount contract failed."); checks++; }
        private static void Reject(Action action)
        {
            try { action(); } catch (JournalAmountException) { checks++; return; }
            throw new Exception("Expected safe journal amount rejection.");
        }
        private static JournalLineValues Line(decimal debit, decimal credit)
            => new JournalLineValues("1000", debit, credit, "بيان عربي 500 mg", null);
        internal static int Run()
        {
            checks = 0;
            Check(JournalAmountCalculator.ToLocal(100m, 250m) == 25000m);
            Check(JournalAmountCalculator.ToLocal(10.005m, 1m) == 10m);
            Check(JournalAmountCalculator.ToLocal(10.015m, 1m) == 10.02m);
            Check(JournalAmountCalculator.ToLocal(.005m, 1m) == 0m);
            Check(JournalAmountCalculator.ToLocal(.015m, 1m) == .02m);
            Check(JournalAmountCalculator.ToLocal(1.125m, 2m) == 2.25m);
            Check(JournalAmountCalculator.ToLocal(250m, .001m) == .25m);
            Check(JournalAmountCalculator.ToLocal(0m, decimal.MaxValue) == 0m);
            Check(JournalAmountCalculator.ToLocal(decimal.MaxValue, 1m) == decimal.MaxValue);
            Reject(() => JournalAmountCalculator.ToLocal(-1m, 1m));
            Reject(() => JournalAmountCalculator.ToLocal(1m, 0m));
            Reject(() => JournalAmountCalculator.ToLocal(1m, -1m));
            Reject(() => JournalAmountCalculator.ToLocal(2m, decimal.MaxValue));
            Check(JournalAmountCalculator.Preview(10m, 0m, 2m) == 20m);
            Check(JournalAmountCalculator.Preview(0m, 10m, 2m) == 20m);
            Check(JournalAmountCalculator.Preview(3m, 5m, 2m) == 10m);
            Check(JournalAmountCalculator.Preview(.005m, 0m, 1m) == 0m);
            Reject(() => JournalAmountCalculator.Preview(-1m, 2m, 1m));
            Reject(() => JournalAmountCalculator.Preview(2m, 0m, decimal.MaxValue));
            var empty = JournalAmountCalculator.Sum(new JournalLineValues[0]);
            Check(empty.Debit == 0m && empty.Credit == 0m && empty.Difference == 0m);
            var totals = JournalAmountCalculator.Sum(new[] { Line(12.25m, 0m), Line(0m, 10m), Line(0m, 2.25m) });
            Check(totals.Debit == 12.25m && totals.Credit == 12.25m && totals.Difference == 0m);
            totals = JournalAmountCalculator.Sum(new[] { Line(.005m, 0m), Line(0m, .001m) });
            Check(totals.Debit == .005m && totals.Credit == .001m && totals.Difference == .004m);
            totals = JournalAmountCalculator.Sum(new[] { Line(0m, decimal.MaxValue) });
            Check(totals.Difference == -decimal.MaxValue);
            Reject(() => JournalAmountCalculator.Sum(new[] { Line(decimal.MaxValue, 0m), Line(1m, 0m) }));
            Reject(() => JournalAmountCalculator.Sum(new[] { Line(0m, decimal.MaxValue), Line(0m, 1m) }));
            Reject(() => JournalAmountCalculator.Sum(new[] { Line(-1m, 0m) }));
            Reject(() => JournalAmountCalculator.Sum(new JournalLineValues[] { null }));
            try { JournalAmountCalculator.Sum(null); throw new Exception("Expected null rejection."); }
            catch (ArgumentNullException) { checks++; }
            Console.WriteLine("PASS: " + checks + " journal conversion, midpoint, totals and overflow contracts; no database connection.");
            return checks;
        }
    }
}
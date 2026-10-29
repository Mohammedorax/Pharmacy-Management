using System;
using PharmacyManagement.Domain;

namespace PharmacyManagement.CoreTests
{
    internal static class VoucherAmountTests
    {
        internal static int Run()
        {
            int checks = 0;
            foreach (var sample in new[] {
                new[] { 1250.50m, 1m, 1250.50m }, new[] { 1250.50m, 3m, 3751.50m },
                new[] { 100m, 0.5m, 50m }, new[] { 123.455m, 1m, 123.46m },
                new[] { 123.445m, 1m, 123.44m }, new[] { 0.005m, 1m, 0m },
                new[] { 0.015m, 1m, 0.02m }, new[] { 0m, 2m, 0m },
                new[] { 1.234m, 2.345m, 2.89m } })
            {
                decimal value;
                if (!VoucherAmountCalculator.TryCalculate(sample[0], sample[1], out value) || value != sample[2])
                    throw new InvalidOperationException("Voucher amount/rate conversion or midpoint rounding changed.");
                checks++;
            }
            foreach (var invalid in new[] { new[] { 100m, 0m }, new[] { 100m, -1m },
                new[] { -1m, 1m }, new[] { decimal.MaxValue, 2m }, new[] { 2m, decimal.MaxValue } })
            {
                decimal value;
                if (VoucherAmountCalculator.TryCalculate(invalid[0], invalid[1], out value) || value != 0m)
                    throw new InvalidOperationException("Invalid voucher conversion accepted or overflow escaped.");
                checks++;
            }
            Console.WriteLine("PASS: " + checks + " voucher conversion and rounding assertions; no database connection.");
            return checks;
        }
    }
}

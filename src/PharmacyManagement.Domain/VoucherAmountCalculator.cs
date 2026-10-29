using System;

namespace PharmacyManagement.Domain
{
    public static class VoucherAmountCalculator
    {
        // Preserve the established voucher posting precision and midpoint rule.
        // Invalid input must never select a different exchange rate silently.
        public static bool TryCalculate(decimal originalAmount, decimal exchangeRate, out decimal localAmount)
        {
            localAmount = 0m;
            if (originalAmount < 0m || exchangeRate <= 0m) return false;
            try
            {
                localAmount = Math.Round(originalAmount * exchangeRate, 2, MidpointRounding.ToEven);
                return true;
            }
            catch (OverflowException) { return false; }
        }
    }
}

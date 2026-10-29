using System;

namespace project_fo_3
{
    internal sealed class TaxPolicy
    {
        public TaxPolicy(string countryCode, bool isEnabled, decimal rate)
        {
            if (rate < 0m || rate > 100m || decimal.Round(rate, 2) != rate)
                throw new ArgumentOutOfRangeException(nameof(rate), "نسبة الضريبة من 0 إلى 100 وبمنزلتين عشريتين كحد أقصى.");
            CountryCode = NormalizeCountryCode(countryCode);
            IsEnabled = isEnabled;
            Rate = rate;
            IsInEffect = IsCountryTaxEnabled(CountryCode) && IsEnabled;
            EffectiveRate = IsInEffect ? Rate : 0m;
        }

        public string CountryCode { get; }
        public bool IsEnabled { get; }
        public decimal Rate { get; }
        public bool IsInEffect { get; }
        public decimal EffectiveRate { get; }

        private static string NormalizeCountryCode(string countryCode)
        {
            return string.IsNullOrWhiteSpace(countryCode) ? "YE" : countryCode.Trim().ToUpperInvariant();
        }

        private static bool IsCountryTaxEnabled(string countryCode)
        {
            return !string.Equals(countryCode, "YE", StringComparison.OrdinalIgnoreCase);
        }
    }
}

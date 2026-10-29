using System;
using PharmacyManagement.Application;

namespace PharmacyManagement.CoreTests
{
    internal static class ReportSearchCriteriaTests
    {
        internal static int Run()
        {
            int checks = 0;
            foreach (string value in new[] { null, "", " \t\r\n", " باراسيتامول 500 mg ", new string('س', 120), "%_[]", "صَيْدَلِيَّة A-001" })
            {
                string expected = (value ?? string.Empty).Trim();
                if (ReportSearchCriteria.Normalize(value) != expected)
                    throw new InvalidOperationException("Report search normalization changed meaningful text.");
                checks++;
            }
            foreach (string value in new[] { new string('س', 121), new string('x', 200), " " + new string('س', 121) + " " })
            {
                bool rejected = false;
                try { ReportSearchCriteria.Normalize(value); }
                catch (InvalidOperationException ex) { rejected = ex.Message == ReportSearchCriteria.TooLongMessage; }
                if (!rejected) throw new InvalidOperationException("Overlong report search was silently accepted.");
                checks++;
            }
            Console.WriteLine("PASS: " + checks + " report search contracts; no database or filesystem access.");
            return checks;
        }
    }
}

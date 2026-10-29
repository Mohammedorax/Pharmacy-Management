using System;
using System.Linq;
using PharmacyManagement.Application;

namespace PharmacyManagement.CoreTests
{
    internal static class AccountingReportReadRequestTests
    {
        internal static int Run()
        {
            int checks = 0;
            DateTime from = new DateTime(2026, 9, 1, 23, 59, 58, DateTimeKind.Utc);
            DateTime to = new DateTime(2026, 9, 30, 1, 2, 3, DateTimeKind.Utc);
            foreach (string key in new[] { "Tax", "CostCenters", "GeneralJournal", "TrialBalance", "ProfitLoss", "BalanceSheet" })
            {
                var request = new AccountingReportReadRequest(key, from, to, 42, true, false);
                Check(request.ReportKey == key && request.FromDate == from.Date && request.ToDate == to.Date &&
                    request.FromDate.Kind == DateTimeKind.Utc && request.CostCenterId == 42 &&
                    request.IncludePreviousBalance && !request.ExcludeInventoryValue, ref checks);
            }
            var sameDay = new AccountingReportReadRequest("Tax", from, from.Date, null, false, true);
            Check(sameDay.FromDate == sameDay.ToDate && sameDay.CostCenterId == null && sameDay.ExcludeInventoryValue, ref checks);
            Check(typeof(AccountingReportReadRequest).GetProperties().All(property => !property.CanWrite), ref checks);
            new AccountingReportReadRequest("TrialBalance", new DateTime(1753,1,1), new DateTime(9998,12,31), null, false, false); checks++;
            foreach (string key in new[] { null, "", "tax", "Unknown" })
                Reject(() => new AccountingReportReadRequest(key, from, to, null, false, false), ref checks);
            Reject(() => new AccountingReportReadRequest("Tax", to, from, null, false, false), ref checks);
            Reject(() => new AccountingReportReadRequest("Tax", new DateTime(1752,12,31), to, null, false, false), ref checks);
            Reject(() => new AccountingReportReadRequest("Tax", from, new DateTime(9999,1,1), null, false, false), ref checks);
            foreach (int id in new[] { 0, -1 })
                Reject(() => new AccountingReportReadRequest("CostCenters", from, to, id, false, false), ref checks);
            Console.WriteLine("PASS: " + checks + " immutable accounting report request contracts; no database connection.");
            return checks;
        }
        private static void Check(bool condition, ref int checks)
        {
            if (!condition) throw new Exception("Accounting report request contract failed.");
            checks++;
        }
        private static void Reject(Action action, ref int checks)
        {
            try { action(); } catch (ArgumentException) { checks++; return; }
            throw new Exception("Invalid accounting report request was accepted.");
        }
    }
}

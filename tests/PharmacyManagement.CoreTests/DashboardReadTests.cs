using System;
using System.Collections.Generic;
using PharmacyManagement.Application;

namespace PharmacyManagement.CoreTests
{
    internal static class DashboardReadTests
    {
        internal static int Run()
        {
            int checks = 0;
            var repository = new FakeRepository();
            var service = new DashboardReadService(repository, new OperationActor { UserName="dashboard-test",UserRole="Manager" }, actor => actor.UserName=="dashboard-test" && actor.UserRole=="Manager");

            service.GetSalesTotal("  أحمد  ");
            Check(repository.LastCashier == "أحمد", "Dashboard cashier filter was not normalized.", ref checks);
            service.GetCustomerSummaries(null);
            Check(repository.LastCashier == string.Empty, "Dashboard all-cashiers filter was not normalized.", ref checks);
            service.GetRecentSalesTrend(" ");
            Check(repository.LastCashier == string.Empty, "Dashboard blank filter should mean all cashiers.", ref checks);
            Check(service.GetCompanyCount() == 2 && service.GetEmployeeCount() == 3,
                "Dashboard counts did not pass through the read contract.", ref checks);
            Check(service.GetCashierNames().Count == 1, "Dashboard cashier names were not returned.", ref checks);
            Reject(() => service.GetSalesTotal(new string('X', 201)), "Oversized dashboard cashier filter was accepted.", ref checks);
            return checks + DashboardAuthorizationContractTests.Run();
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

        private sealed class FakeRepository : IDashboardReadRepository
        {
            internal string LastCashier;

            public int GetCompanyCount() => 2;
            public int GetEmployeeCount() => 3;
            public decimal GetSalesTotal(string cashierName) { LastCashier = cashierName; return 10m; }
            public IReadOnlyList<string> GetCashierNames() => new[] { "أحمد" };
            public IReadOnlyList<DashboardCustomerSummary> GetCustomerSummaries(string cashierName)
            {
                LastCashier = cashierName;
                return new DashboardCustomerSummary[0];
            }
            public IReadOnlyList<DashboardSalesTrendPoint> GetRecentSalesTrend(string cashierName)
            {
                LastCashier = cashierName;
                return new DashboardSalesTrendPoint[0];
            }
        }
    }
}

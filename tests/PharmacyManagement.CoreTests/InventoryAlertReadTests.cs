using System;
using PharmacyManagement.Application;
using PharmacyManagement.Infrastructure.SqlServer;

namespace PharmacyManagement.CoreTests
{
    internal static class InventoryAlertReadTests
    {
        internal static int Run()
        {
            int checks = 0;
            var result = new OperationalReportResult(
                new[] { new OperationalReportColumn("الصنف", typeof(string)) },
                new[] { new object[] { "دواء" } });
            var repository = new Fake { Result = result };
            var service = new InventoryAlertReadService(repository);
            Check(ReferenceEquals(result, service.GetAlerts(10, 30)), ref checks);
            Check(repository.Request.LowStockThreshold == 10 && repository.Request.ExpiringWithinDays == 30 &&
                repository.Request.AffectedMedicines == null, ref checks);
            Check(ReferenceEquals(result, service.GetAlerts(0, 3650, new[] { "A", "", "A", "B" })), ref checks);
            Check(repository.Request.AffectedMedicines.Count == 2 &&
                repository.Request.AffectedMedicines[0] == "A" && repository.Request.AffectedMedicines[1] == "B", ref checks);
            ExpectInvalid(() => service.GetAlerts(-1, 30), repository, ref checks);
            ExpectInvalid(() => service.GetAlerts(0, -1), repository, ref checks);
            ExpectInvalid(() => service.GetAlerts(0, 3651), repository, ref checks);
            bool opened = false;
            var sql = new SqlInventoryAlertRepository(() => { opened = true; throw new InvalidOperationException(); });
            Check(sql != null && !opened, ref checks);
            Console.WriteLine("PASS: " + checks + " inventory alert read contracts; no database connection.");
            return checks;
        }

        private static void ExpectInvalid(Action action, Fake repository, ref int checks)
        {
            int before = repository.Calls;
            bool failed = false;
            try { action(); }
            catch (InvalidOperationException) { failed = true; }
            Check(failed && repository.Calls == before, ref checks);
        }

        private static void Check(bool condition, ref int checks)
        { if (!condition) throw new InvalidOperationException("Inventory alert read contract changed."); checks++; }

        private sealed class Fake : IInventoryAlertRepository
        {
            internal OperationalReportResult Result;
            internal InventoryAlertRequest Request;
            internal int Calls;
            public OperationalReportResult Load(InventoryAlertRequest request)
            { Calls++; Request = request; return Result; }
        }
    }
}

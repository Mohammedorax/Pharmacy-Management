using System;
using PharmacyManagement.Application;
using PharmacyManagement.Infrastructure.SqlServer;

namespace PharmacyManagement.CoreTests
{
    internal static class SystemMonitorLogReadTests
    {
        internal static int Run()
        {
            int checks = 0;
            var result = new OperationalReportResult(
                new[] { new OperationalReportColumn("Time", typeof(DateTime)) },
                new[] { new object[] { new DateTime(2026, 10, 1) } });
            var repository = new Fake { Result = result };
            var service = new SystemMonitorLogReadService(repository, () => true);
            Check(ReferenceEquals(service.GetRecentAuditLogs(0), result) && repository.AuditTake == 1, ref checks);
            Check(ReferenceEquals(service.GetRecentAuditLogs(501), result) && repository.AuditTake == 500, ref checks);
            Check(ReferenceEquals(service.GetRecentInventoryMovements(0), result) && repository.MovementTake == 1, ref checks);
            Check(ReferenceEquals(service.GetRecentInventoryMovements(1001), result) && repository.MovementTake == 1000, ref checks);
            Check(ReferenceEquals(service.GetRecentAuditLogs(), result) && repository.AuditTake == 100, ref checks);
            Check(ReferenceEquals(service.GetRecentInventoryMovements(), result) && repository.MovementTake == 200, ref checks);
            bool opened = false;
            var sql = new SqlSystemMonitorLogRepository(() => { opened = true; throw new InvalidOperationException(); });
            Check(sql != null && !opened, ref checks);
            Console.WriteLine("PASS: " + checks + " typed system-monitor read contracts; no database connection.");
            return checks;
        }

        private static void Check(bool condition, ref int checks)
        { if (!condition) throw new InvalidOperationException("System monitor read contract changed."); checks++; }

        private sealed class Fake : ISystemMonitorLogRepository
        {
            internal OperationalReportResult Result;
            internal int AuditTake;
            internal int MovementTake;
            public OperationalReportResult LoadAudit(int take) { AuditTake = take; return Result; }
            public OperationalReportResult LoadInventoryMovements(int take) { MovementTake = take; return Result; }
        }
    }
}


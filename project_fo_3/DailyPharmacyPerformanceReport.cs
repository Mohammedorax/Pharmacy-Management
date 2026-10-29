using PharmacyManagement.Application;
using PharmacyManagement.Infrastructure.SqlServer;

namespace project_fo_3
{
    // Compatibility name for existing report verification callers; SQL lives in Infrastructure.
    internal static class DailyPharmacyPerformanceReport
    {
        internal static string StockQuery => SqlOperationalReportQueries.Get(OperationalReportKind.DailyInventoryActivity);
        internal static string Query => SqlOperationalReportQueries.Get(OperationalReportKind.DailyPerformance);
    }
}
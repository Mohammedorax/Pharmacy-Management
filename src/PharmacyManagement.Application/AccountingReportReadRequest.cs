using System;

namespace PharmacyManagement.Application
{
    public sealed class AccountingReportReadRequest
    {
        public AccountingReportReadRequest(string reportKey, DateTime fromDate, DateTime toDate,
            int? costCenterId, bool includePreviousBalance, bool excludeInventoryValue)
        {
            if (reportKey != "Tax" && reportKey != "CostCenters" && reportKey != "GeneralJournal" &&
                reportKey != "TrialBalance" && reportKey != "ProfitLoss" && reportKey != "BalanceSheet")
                throw new ArgumentException("نوع التقرير غير صالح.", nameof(reportKey));
            DateTime minimum = new DateTime(1753, 1, 1), maximum = new DateTime(9998, 12, 31);
            if (fromDate.Date < minimum || fromDate.Date > maximum || toDate.Date < minimum || toDate.Date > maximum)
                throw new ArgumentException("التاريخ خارج النطاق المدعوم.");
            if (fromDate.Date > toDate.Date)
                throw new ArgumentException("تاريخ البداية يجب ألا يتجاوز تاريخ النهاية.");
            if (costCenterId.HasValue && costCenterId.Value <= 0)
                throw new ArgumentException("مركز التكلفة غير صالح.", nameof(costCenterId));
            ReportKey = reportKey;
            FromDate = fromDate.Date;
            ToDate = toDate.Date;
            CostCenterId = costCenterId;
            IncludePreviousBalance = includePreviousBalance;
            ExcludeInventoryValue = excludeInventoryValue;
        }

        public string ReportKey { get; }
        public DateTime FromDate { get; }
        public DateTime ToDate { get; }
        public int? CostCenterId { get; }
        public bool IncludePreviousBalance { get; }
        public bool ExcludeInventoryValue { get; }
    }
}

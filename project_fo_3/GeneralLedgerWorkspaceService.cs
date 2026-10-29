using System;
using System.Collections.Generic;
using System.Data;

namespace project_fo_3
{
    internal sealed class GeneralLedgerDefaultsSnapshot
    {
        internal GeneralLedgerDefaultsSnapshot(IList<string> accounts, IList<string> costCenters)
        {
            Accounts = new List<string>(accounts ?? new List<string>());
            CostCenters = new List<string>(costCenters ?? new List<string>());
        }

        internal IList<string> Accounts { get; private set; }
        internal IList<string> CostCenters { get; private set; }
    }

    internal sealed class GeneralLedgerQuery
    {
        internal GeneralLedgerQuery(string accountCode, DateTime fromDate, DateTime toDate, string sourceType, bool includePrevious, bool includeOpening, int? costCenterId)
        {
            AccountCode = accountCode;
            FromDate = fromDate;
            ToDate = toDate;
            SourceType = sourceType;
            IncludePrevious = includePrevious;
            IncludeOpening = includeOpening;
            CostCenterId = costCenterId;
        }

        internal string AccountCode { get; private set; }
        internal DateTime FromDate { get; private set; }
        internal DateTime ToDate { get; private set; }
        internal string SourceType { get; private set; }
        internal bool IncludePrevious { get; private set; }
        internal bool IncludeOpening { get; private set; }
        internal int? CostCenterId { get; private set; }
    }

    internal sealed class GeneralLedgerQueryResult
    {
        internal GeneralLedgerQueryResult(DataTable rows, decimal previousBalance)
        {
            Rows = rows ?? new DataTable();
            PreviousBalance = previousBalance;
        }

        internal DataTable Rows { get; private set; }
        internal decimal PreviousBalance { get; private set; }
    }

    // خدمة القراءة فقط لكشف الحساب، وتحفظ ترتيب الحسابات وقرارات الرصيد خارج الواجهة.
    internal static class GeneralLedgerWorkspaceService
    {
        internal static GeneralLedgerDefaultsSnapshot LoadDefaults()
        {
            var accounts = new List<string>();
            foreach (DataRow row in AccountingLookupDesktopService.LoadAccounts(true).Rows)
            {
                string code = Convert.ToString(row["AccountCode"]);
                if (!string.IsNullOrWhiteSpace(code)) accounts.Add(code + " - " + Convert.ToString(row["AccountName"]));
            }

            var costCenters = new List<string>();
            try
            {
                foreach (DataRow row in AccountingLookupDesktopService.LoadCostCenters().Rows)
                {
                    costCenters.Add(Convert.ToString(row["CostCenterId"]) + " - " + Convert.ToString(row["CostCenterName"]));
                }
            }
            catch
            {
            }

            return new GeneralLedgerDefaultsSnapshot(accounts, costCenters);
        }

        internal static GeneralLedgerQueryResult Load(GeneralLedgerQuery query)
        {
            if (query == null) throw new ArgumentNullException("query");
            DataTable rows = AccountingService.GetLedger(
                query.AccountCode,
                query.FromDate,
                query.ToDate,
                query.SourceType,
                query.IncludePrevious,
                query.IncludeOpening,
                query.CostCenterId);
            decimal previousBalance = query.IncludePrevious
                ? AccountingService.GetAccountBalanceBefore(query.AccountCode, query.FromDate, query.SourceType, query.IncludeOpening, query.CostCenterId)
                : 0m;
            return new GeneralLedgerQueryResult(rows, previousBalance);
        }
    }
}


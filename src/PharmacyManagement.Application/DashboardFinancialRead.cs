using System;
using System.Collections.Generic;
using System.Linq;

namespace PharmacyManagement.Application
{
    public enum DashboardAttributionMode { SellerPerformance, MovementOperator }

    public sealed class DashboardFinancialFact
    {
        public int JournalEntryId { get; set; }
        public DateTime BusinessDay { get; set; }
        public string SourceType { get; set; }
        public string SourceId { get; set; }
        public string MovementActor { get; set; }
        public string OriginalSeller { get; set; }
        public string AttributionIssue { get; set; }
        public string CurrencyCode { get; set; }
        public string CurrencyIssue { get; set; }
        public decimal Revenue { get; set; }
        public decimal Cost { get; set; }
        public decimal Tax { get; set; }
    }

    public sealed class DashboardFinancialTotal
    {
        public DateTime BusinessDay { get; internal set; }
        public string CurrencyCode { get; internal set; }
        public decimal? SalesRevenue { get; internal set; }
        public decimal? ReversedRevenue { get; internal set; }
        public decimal? NetRevenue { get; internal set; }
        public decimal? NetCost { get; internal set; }
        public decimal? NetTax { get; internal set; }
        public int MovementCount { get; internal set; }
        public int UnattributedCount { get; internal set; }
        public int CurrencyProblemCount { get; internal set; }
    }

    public static class DashboardFinancialProjection
    {
        public static IReadOnlyList<DashboardFinancialTotal> Summarize(
            IEnumerable<DashboardFinancialFact> facts, DashboardAttributionMode mode, string person)
        {
            if (facts == null) throw new ArgumentNullException(nameof(facts));
            if (!Enum.IsDefined(typeof(DashboardAttributionMode),mode)) throw new ArgumentOutOfRangeException(nameof(mode));
            person=(person??string.Empty).Trim();
            if(person.Length>200) throw new ArgumentException("اسم الموظف يتجاوز الحد المسموح.",nameof(person));
            Func<DashboardFinancialFact,string> actor=f=>mode==DashboardAttributionMode.SellerPerformance
                ? (string.IsNullOrEmpty(f.AttributionIssue)?f.OriginalSeller:null):f.MovementActor;
            var selected=facts.Where(f=>person.Length==0||string.Equals(actor(f),person,StringComparison.OrdinalIgnoreCase));
            return selected.GroupBy(f=>new { Day=f.BusinessDay.Date,
                    Currency=string.IsNullOrEmpty(f.CurrencyIssue)?NormalizeCurrency(f.CurrencyCode):null })
                .Select(g=>new DashboardFinancialTotal {
                    BusinessDay=g.Key.Day,CurrencyCode=g.Key.Currency,MovementCount=g.Count(),
                    UnattributedCount=g.Count(f=>string.IsNullOrWhiteSpace(actor(f))),
                    CurrencyProblemCount=g.Count(f=>!string.IsNullOrEmpty(f.CurrencyIssue)||NormalizeCurrency(f.CurrencyCode)==null),
                    SalesRevenue=g.Key.Currency==null?(decimal?)null:g.Where(f=>f.SourceType=="SalesInvoice").Sum(f=>f.Revenue),
                    ReversedRevenue=g.Key.Currency==null?(decimal?)null:-g.Where(f=>f.SourceType=="SalesInvoiceCancellation"||f.SourceType=="CommercialSalesReturn").Sum(f=>f.Revenue),
                    NetRevenue=g.Key.Currency==null?(decimal?)null:g.Sum(f=>f.Revenue),
                    NetCost=g.Key.Currency==null?(decimal?)null:g.Sum(f=>f.Cost),
                    NetTax=g.Key.Currency==null?(decimal?)null:g.Sum(f=>f.Tax)
                }).OrderBy(g=>g.BusinessDay).ThenBy(g=>g.CurrencyCode).ToList().AsReadOnly();
        }
        private static string NormalizeCurrency(string value) => string.IsNullOrWhiteSpace(value)?null:value.Trim().ToUpperInvariant();
    }
}

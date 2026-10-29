using System;
using System.Collections.Generic;
using PharmacyManagement.Application;

namespace project_fo_3
{
    // Isolated source draft: no storage access and no application integration yet.
    internal enum OperationalPresetPeriod { AllAvailable, Today, CurrentMonth, Last30Days }

    internal sealed class OperationalReportPreset
    {
        internal string Id { get; }
        internal string SourceKey { get; }
        internal string ArabicTitle { get; }
        internal string EnglishTitle { get; }
        internal OperationalReportViewMode ViewMode { get; }
        internal OperationalPresetPeriod Period { get; }

        internal OperationalReportPreset(int identity, string sourceKey, string arabicTitle, string englishTitle,
            OperationalReportViewMode viewMode, OperationalPresetPeriod period)
        {
            Id = new Guid("e9150000-0000-4000-8000-" + identity.ToString("D12", System.Globalization.CultureInfo.InvariantCulture)).ToString("N");
            SourceKey = sourceKey;
            ArabicTitle = arabicTitle;
            EnglishTitle = englishTitle;
            ViewMode = viewMode;
            Period = period;
        }

        // Resolve relative dates at Apply time, including after the form stays open overnight.
        internal OperationalReportTemplate CreateTemplate(DateTime today, bool leftToRight)
        {
            today = today.Date;
            DateTime from = today;
            if (Period == OperationalPresetPeriod.CurrentMonth) from = new DateTime(today.Year, today.Month, 1);
            else if (Period == OperationalPresetPeriod.Last30Days) from = today.AddDays(-29);
            return new OperationalReportTemplate
            {
                TemplateId = Id,
                Name = leftToRight ? EnglishTitle : ArabicTitle,
                SourceKey = SourceKey,
                FromDate = from,
                ToDate = today,
                SearchText = string.Empty,
                ViewMode = ViewMode.ToString(),
                // Keep all available source columns; optional legacy schemas may differ.
                VisibleColumns = new List<string>(),
                ColumnOrder = new List<string>(),
                // Resolve current company identity and print preferences through the existing dialog.
                HasPrintSettings = false
            };
        }
    }

    internal static class OperationalReportPresets
    {
        private static readonly IReadOnlyList<OperationalReportPreset> Presets = Array.AsReadOnly(new[]
        {
            new OperationalReportPreset(1,"Medicines","الأصناف والأرصدة الحالية","Current items and balances",OperationalReportViewMode.Analytical,OperationalPresetPeriod.AllAvailable),
            new OperationalReportPreset(2,"SalesLines","المبيعات المتاحة حسب الصنف والعملات والحالات","Available sales by item, currency and status",OperationalReportViewMode.Summary,OperationalPresetPeriod.AllAvailable),
            new OperationalReportPreset(3,"LegacySalesLines","البيع القديم حسب الصنف — كل المتاح دون تاريخ","Legacy sales by item — all available, undated",OperationalReportViewMode.Summary,OperationalPresetPeriod.AllAvailable),
            new OperationalReportPreset(4,"Purchases","مشتريات الشهر حسب الصنف","Month-to-date purchases by item",OperationalReportViewMode.Summary,OperationalPresetPeriod.CurrentMonth),
            new OperationalReportPreset(5,"PurchaseLines","سطور مشتريات الشهر","Month-to-date purchase lines",OperationalReportViewMode.Analytical,OperationalPresetPeriod.CurrentMonth),
            new OperationalReportPreset(6,"InventoryMovements","ملخص حركة المخزون للشهر","Month-to-date inventory movement summary",OperationalReportViewMode.Summary,OperationalPresetPeriod.CurrentMonth),
            new OperationalReportPreset(7,"InventoryMovements","حركات المخزون اليوم","Today's inventory movements",OperationalReportViewMode.Analytical,OperationalPresetPeriod.Today),
            new OperationalReportPreset(8,"JournalLines","ملخص الحسابات للشهر حسب العملة","Month-to-date accounts by currency",OperationalReportViewMode.Summary,OperationalPresetPeriod.CurrentMonth),
            new OperationalReportPreset(9,"JournalLines","قيود اليومية اليوم — حركة بحركة","Today's journal entries — each movement",OperationalReportViewMode.Analytical,OperationalPresetPeriod.Today),
            new OperationalReportPreset(10,"CashLedger","ملخص النقد المشترك للشهر حسب العملة","Month-to-date shared cash by currency",OperationalReportViewMode.Summary,OperationalPresetPeriod.CurrentMonth),
            new OperationalReportPreset(11,"CashLedger","حركات النقد المشترك اليوم","Today's shared cash movements",OperationalReportViewMode.Analytical,OperationalPresetPeriod.Today),
            new OperationalReportPreset(12,"DailyPerformance","الأداء المالي لآخر 30 يومًا حسب العملة","Daily financial performance by currency over 30 days",OperationalReportViewMode.Analytical,OperationalPresetPeriod.Last30Days),
            new OperationalReportPreset(13,"FastSellingToday","الأصناف الأعلى صافي مبيعًا اليوم","Items with highest net sales quantity today",OperationalReportViewMode.Analytical,OperationalPresetPeriod.Today),
            new OperationalReportPreset(14,"FastSelling30Days","الأصناف الأعلى صافي مبيعًا لآخر 30 يومًا","Items with highest net sales quantity over 30 days",OperationalReportViewMode.Analytical,OperationalPresetPeriod.Last30Days),
            new OperationalReportPreset(15,"MostMovementsToday","الأصناف الأكثر حركات مخزون اليوم","Items with most inventory movements today",OperationalReportViewMode.Analytical,OperationalPresetPeriod.Today),
            new OperationalReportPreset(16,"DailyInventoryActivity","ملخص المخزون اليومي لآخر 30 يومًا","Daily inventory activity over the last 30 days",OperationalReportViewMode.Analytical,OperationalPresetPeriod.Last30Days)
        });

        internal static IReadOnlyList<OperationalReportPreset> GetAll() { return Presets; }
    }
}

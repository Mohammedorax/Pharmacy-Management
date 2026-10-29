using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;

namespace project_fo_3
{
    internal sealed partial class InventoryMovementSummaryForm
    {
        private void InitializeComponent()
        {
            fromDate.Value = DateTime.Today.AddMonths(-1);
            toDate.Value = DateTime.Today;
            AddFilter("بحث", searchFrame, 280, 76);
            search.RightToLeft = UiDirection.IsLeftToRight ? RightToLeft.No : RightToLeft.Yes;
            search.TextAlign = HorizontalAlignment.Left;

            AddFilter("من تاريخ", fromDate, 150, 76);
            AddFilter("إلى تاريخ", toDate, 150, 76);
            var actionFooter = AddOutlinedActionFooter("inventorySummaryFooter");
            AddOutlinedFooterAction(actionFooter, "inventorySummaryRefresh", "تحديث", AppIcon.Refresh, (s, e) => RefreshData());
            AddOutlinedFooterAction(actionFooter, "inventorySummaryPrint", "طباعة", AppIcon.Print, (s, e) => PreviewCurrentGrid());
            AddOutlinedFooterAction(actionFooter, "inventorySummaryExport", "تصدير Excel", AppIcon.Report, (s, e) => ExportCurrentGrid());
            Load += (s, e) => { if (loadData) RefreshData(); };
        
        }
    }
}

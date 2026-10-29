using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;

namespace project_fo_3
{
    internal sealed partial class InventoryTurnoverForm
    {
        private void InitializeComponent()
        {
            fromDate.Value = DateTime.Today.AddMonths(-3);
            toDate.Value = DateTime.Today;
            AddFilter("من تاريخ", fromDate, 150, 72);
            AddFilter("إلى تاريخ", toDate, 150, 72);
            AddFilter("أيام الركود", idleDays, 140, 72);
            idleDays.RightToLeft = RightToLeft.No;
            idleDays.TextAlign = HorizontalAlignment.Left;
            var actionFooter = AddOutlinedActionFooter("inventoryTurnoverFooter");
            AddOutlinedFooterAction(actionFooter, "inventoryTurnoverRefresh", "تحديث", AppIcon.Refresh, (s, e) => RefreshData());
            AddOutlinedFooterAction(actionFooter, "inventoryTurnoverPrint", "طباعة", AppIcon.Print, (s, e) => PreviewCurrentGrid());
            AddOutlinedFooterAction(actionFooter, "inventoryTurnoverExport", "تصدير Excel", AppIcon.Report, (s, e) => ExportCurrentGrid());
            Load += (s, e) => { if (loadData) RefreshData(); };

        }
    }
}

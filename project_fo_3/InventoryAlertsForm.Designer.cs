using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;

namespace project_fo_3
{
    internal sealed partial class InventoryAlertsForm
    {
        private void InitializeComponent()
        {
            AddFilter("حد المخزون المنخفض", lowStockThreshold, 170, 72);
            AddFilter("الأيام قبل الانتهاء", expiringDays, 170, 72);
            foreach (AntdUI.Input threshold in new[] { lowStockThreshold, expiringDays })
            {
                threshold.RightToLeft = RightToLeft.No;
                threshold.TextAlign = HorizontalAlignment.Left;
            }
            var saveLimits = DesktopComponents.Action("inventoryAlertsSaveLimits", UiLanguageCatalog.GetDisplayText("حفظ الحدود"), AppIcon.Save);
            saveLimits.Tag = "حفظ الحدود";
            DesktopComponents.StyleOutlinedFooterAction(saveLimits);
            saveLimits.Click += (s, e) => SaveAlertPreferencesClicked();
            // Saving belongs beside the limits and leaves the report viewport usable on narrow windows.
            AddFilter(string.Empty, saveLimits, 140, 72);
            var actionFooter = AddOutlinedActionFooter("inventoryAlertsFooter");
            AddOutlinedFooterAction(actionFooter, "inventoryAlertsRefresh", "تحديث", AppIcon.Refresh, (s, e) => RefreshData());
            AddOutlinedFooterAction(actionFooter, "inventoryAlertsPrint", "طباعة", AppIcon.Print, (s, e) => PreviewCurrentGrid());
            AddOutlinedFooterAction(actionFooter, "inventoryAlertsExport", "تصدير Excel", AppIcon.Report, (s, e) => ExportCurrentGrid());
            Load += (s, e) => { if (loadData) RefreshData(); };

        }
    }
}

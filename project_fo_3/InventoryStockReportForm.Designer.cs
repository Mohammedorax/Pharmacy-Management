using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;

namespace project_fo_3
{
    internal sealed partial class InventoryStockReportForm
    {
        private void InitializeComponent()
        {
            AddFilter("بحث باسم الصنف / الباركود / الشركة", searchFrame, 280, 76);
            search.RightToLeft = UiDirection.IsLeftToRight ? RightToLeft.No : RightToLeft.Yes;
            search.TextAlign = HorizontalAlignment.Left;
            AddFilter("خيارات العرض", includeZero, 190, 72);
            includeZero.RightToLeft = UiDirection.IsLeftToRight ? RightToLeft.No : RightToLeft.Yes;
            includeZero.TextAlign = UiDirection.IsLeftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
            var actionFooter = AddOutlinedActionFooter("inventoryStockFooter");
            AddOutlinedFooterAction(actionFooter, "inventoryStockRefresh", "تحديث", AppIcon.Refresh, async (s, e) => await RefreshStockReportAsync());
            AddOutlinedFooterAction(actionFooter, "inventoryStockPrint", "طباعة", AppIcon.Print, (s, e) => { if (stockResultReady) PreviewCurrentGrid(); });
            AddOutlinedFooterAction(actionFooter, "inventoryStockExport", "تصدير Excel", AppIcon.Report, (s, e) => { if (stockResultReady) ExportCurrentGrid(); });
            search.TextChanged += (s, e) => InvalidateStockResult();
            includeZero.CheckedChanged += (s, e) => InvalidateStockResult();
            Disposed += (s, e) => CancelStockRead();
            Load += async (s, e) => { if (loadData) await RefreshStockReportAsync(); };
        
        }
    }
}

using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;

namespace project_fo_3
{
    internal sealed partial class InventoryMovementForm
    {
        private void InitializeComponent()
        {
            fromDate.MinDate = new DateTime(1753, 1, 1);
            toDate.MinDate = new DateTime(1753, 1, 1);
            fromDate.MaxDate = new DateTime(9998, 12, 31);
            toDate.MaxDate = new DateTime(9998, 12, 31);
            medicineFrame = DesktopComponents.NativeSelectFrame(medicine, "inventoryMovementMedicineFrame", Present("الصنف"), true);
            AddFilter("الصنف", medicineFrame, 290);
            medicine.AccessibleName = "الصنف";
            medicine.AccessibleRole = AccessibleRole.ComboBox;
            medicine.RightToLeft = UiDirection.IsLeftToRight ? RightToLeft.No : RightToLeft.Yes;
            AddFilter("من تاريخ", fromDate, 180);
            AddFilter("إلى تاريخ", toDate, 180);
            var actionFooter = AddOutlinedActionFooter("inventoryMovementFooter");
            AddOutlinedFooterAction(actionFooter, "inventoryMovementRefresh", "تحديث", AppIcon.Refresh, async (s, e) => await RefreshMovementOrLookupAsync());
            AddOutlinedFooterAction(actionFooter, "inventoryMovementPrint", "طباعة", AppIcon.Print, (s, e) => { if (movementResultReady) PreviewCurrentGrid(); });
            AddOutlinedFooterAction(actionFooter, "inventoryMovementExport", "تصدير Excel", AppIcon.Report, (s, e) => { if (movementResultReady) ExportCurrentGrid(); });
            medicine.SelectedIndexChanged += (s, e) => InvalidateMovementResults();
            fromDate.TextChanged += (s, e) => InvalidateMovementResults();
            toDate.TextChanged += (s, e) => InvalidateMovementResults();
            Disposed += (s, e) => CancelMovementRead();
            Load += async (s, e) =>
            {
                if (!loadData) return;
                await LoadMovementAsync();

            };
        
        }

    }
}

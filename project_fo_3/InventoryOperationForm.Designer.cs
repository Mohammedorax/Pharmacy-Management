using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;

namespace project_fo_3
{
    internal sealed partial class InventoryOperationForm
    {
        private void InitializeComponent(InventoryOperationMode mode)
        {
            FilterPanel.Height = 182;

            currentQuantity.BorderStyle = BorderStyle.None;
            currentQuantity.TextAlign = ContentAlignment.MiddleCenter;
            currentQuantity.Font = AppFonts.Create(11F, FontStyle.Bold);

            preview.BorderStyle = BorderStyle.None;
            preview.TextAlign = ContentAlignment.MiddleCenter;
            preview.Font = AppFonts.Create(10F, FontStyle.Bold);

            AddFilter("الصنف", medicine, 300);
            medicine.AccessibleName = "الصنف";
            medicine.AccessibleRole = AccessibleRole.ComboBox;
            medicine.RightToLeft = UiDirection.IsLeftToRight ? RightToLeft.No : RightToLeft.Yes;
            DesktopComponents.AddComboBoxDropdown(medicine, "inventoryOperationMedicineDropdown", "فتح قائمة أصناف المخزون");
            AddFilter("الرصيد الحالي", currentQuantityFrame, 150);
            AddFilter(GetQuantityCaption(mode), quantity, 150);
            if (mode == InventoryOperationMode.Receipt || mode == InventoryOperationMode.Issue)
            {
                AddFilter(mode == InventoryOperationMode.Receipt ? "تكلفة الوحدة" : "سعر الوحدة", unitAmount, 150);
            }
            AddFilter("المعاينة", previewFrame, 260);
            notes.MaxLength = 500;
            AddFilter("سبب الحركة أو مرجعها (إلزامي)", notesFrame, 310, 104);
            ConfigureOperationInput(quantity, GetQuantityCaption(mode), false);
            ConfigureOperationInput(unitAmount, mode == InventoryOperationMode.Receipt ? "تكلفة الوحدة" : "سعر الوحدة", false);
            notes.AccessibleName = "سبب الحركة أو مرجعها";
            notes.AccessibleRole = AccessibleRole.Text;
            notes.RightToLeft = RightToLeft.Yes;
            notes.TextAlign = HorizontalAlignment.Right;
            notes.Font = AppFonts.Create(9.5F);
            notes.TextChanged += (s, e) => UiDirection.ApplyContentDirection(notes, leftToRight);
            var actionFooter = AddOutlinedActionFooter("inventoryOperationFooter");
            var commitAction = DesktopComponents.Action("inventoryOperationCommit", "اعتماد الحركة", AppIcon.Save);
            commitAction.Size = new Size(148, 44);
            commitAction.Margin = new Padding(6, 2, 6, 2);
            DesktopComponents.StyleOutlinedFooterAction(commitAction);
            commitAction.Click += (s, e) => SaveOperation();
            actionFooter.Controls.Add(commitAction);
            commitAction.Tag = "اعتماد الحركة";

            Disposed += (s,e) => CancelInventoryRead();
            quantity.TextChanged += (s, e) => UpdatePreview();
            medicine.SelectedIndexChanged += async (s, e) =>
            {
                if (!loadData || bindingMedicine) return;
                await RefreshInventorySnapshotAsync(false);
            };
            Load += async (s, e) =>
            {
                if (!loadData) return;
                await LoadInventoryOperationAsync();
            };
        }

        private static void ConfigureOperationInput(AntdUI.Input input, string accessibleName, bool arabic)
        {
            input.AccessibleName = accessibleName;
            input.AccessibleRole = AccessibleRole.Text;
            input.RightToLeft = arabic ? RightToLeft.Yes : RightToLeft.No;
            input.TextAlign = arabic ? HorizontalAlignment.Right : HorizontalAlignment.Left;
            input.Font = AppFonts.Create(9.5F);
            input.Radius = input.Multiline ? DesktopDesignTokens.FieldRadius : DesktopDesignTokens.ActionRadius(input.Height);
            AntdThemeAdapter.Apply(input);
        }
    }
}

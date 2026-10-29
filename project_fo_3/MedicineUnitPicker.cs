using PharmacyManagement.Domain;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using PharmacyManagement.Application;
namespace project_fo_3
{
    // Compact package selection shares the quantity row; the name/barcode search stays unchanged.
    internal sealed class MedicineUnitPicker : ComboBox
    {
        internal string MedicineName { get; private set; }
        internal bool Ready { get; private set; }
        internal MedicineUnitSelection SelectedUnit => SelectedItem as MedicineUnitSelection;
        internal MedicineUnitPicker(string name, bool leftToRight)
        {
            Name=name; DropDownStyle=ComboBoxStyle.DropDownList; IntegralHeight=false;
            DropDownHeight=240; DropDownWidth=300; FormattingEnabled=true;
            Format += (sender,args)=> { var unit=args.ListItem as MedicineUnitSelection; if(unit!=null)
                args.Value=unit.Name+" | "+unit.UnitPrice.ToString("N2",CultureInfo.CurrentCulture)+" "+unit.CurrencyCode+" | ×"+unit.BaseUnitsPerUnit; }; AccessibleName=leftToRight ? "Package unit" : "وحدة العبوة";
            RightToLeft=leftToRight?RightToLeft.No:RightToLeft.Yes; Visible=false;
        }
        internal void ResetSelection()
        {
            Ready=false; MedicineName=null; Items.Clear(); Visible=false;
            var host = FindForm();
        }
        internal void SetUnits(string medicine, IReadOnlyList<MedicineUnitSelection> units)
        {
            BeginUpdate();
            try { Items.Clear(); MedicineName=medicine; foreach(var unit in units)Items.Add(unit); Ready=true; Visible=Items.Count>0; if(Items.Count>0)SelectedIndex=0; }
            finally { EndUpdate(); }
            var host = FindForm();
        }
        // The caption follows the picker directly: positioning one label is cheap, while a full form
        // relayout on every unit load made the whole screen redraw and feel extremely slow.
        private Label unitCaption;
        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            UpdateUnitCaption();
        }
        internal void UpdateUnitCaption()
        {
            if (Parent == null) return;
            string captionName = Name == "billingMedicineUnit" ? "lblMedicineUnit" : "lblPurchaseMedicineUnit";
            if (unitCaption == null || unitCaption.Parent != Parent)
            {
                unitCaption = null;
                foreach (Control existing in Parent.Controls)
                    if (existing is Label label && label.Name == captionName) { unitCaption = label; break; }
                if (unitCaption == null)
                {
                    unitCaption = new Label { Name = captionName, AutoSize = true, BackColor = Color.Transparent };
                    Parent.Controls.Add(unitCaption);
                }
            }
            unitCaption.Text = RightToLeft == RightToLeft.Yes ? "وحدة العبوة" : "Package unit";
            unitCaption.Font = AppFonts.Create(9.5F, FontStyle.Bold);
            unitCaption.ForeColor = UiTheme.Text;
            int captionHeight = TextRenderer.MeasureText(unitCaption.Text, unitCaption.Font).Height;
            unitCaption.Location = new Point(Left, Math.Max(0, Top - captionHeight - 6));
            unitCaption.Visible = Visible;
            if (Visible) unitCaption.BringToFront();
        }        internal MedicineUnitSelection CaptureSelection(string medicine, decimal quantity)
        {
            if(!Ready || !string.Equals(MedicineName,medicine,StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("انتظر اكتمال قراءة وحدات الصنف ثم أعد المحاولة.");
            return SelectedUnit?.WithQuantity(quantity);
        }
        internal static bool TryQuantity(string text, out decimal quantity) =>
            decimal.TryParse(text,NumberStyles.Number,CultureInfo.CurrentCulture,out quantity) && quantity>0m;
        internal static bool Same(MedicineUnitSelection first, MedicineUnitSelection second) =>
            first==null ? second==null : second!=null && first.UnitId==second.UnitId &&
            first.CatalogRevision==second.CatalogRevision && first.UnitPrice==second.UnitPrice &&
            first.BaseUnitsPerUnit==second.BaseUnitsPerUnit && first.CurrencyCode==second.CurrencyCode;
        internal static void EnsureColumn(DataGridView grid, bool leftToRight)
        {
            if(!grid.Columns.Contains("Unit"))grid.Columns.Add("Unit",leftToRight?"Unit":"الوحدة");
            var column=grid.Columns["Unit"]; column.ReadOnly=true; column.Width=80;
            if(grid.Columns.Contains("Quantity"))column.DisplayIndex=grid.Columns["Quantity"].DisplayIndex;
        }
    }
}

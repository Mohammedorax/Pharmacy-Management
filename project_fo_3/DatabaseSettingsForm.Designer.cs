using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace project_fo_3
{
    public partial class DatabaseSetingsForm
    {
        private ComboBox cmbFont;
        private Label lblFont;
        private AntdUI.Button btnSaveAppearance;

        private void InitializeAppearanceControls()
        {
            lblFont = CreateSettingsLabel(Present("خط الواجهة"), 228, 30, 145);
            lblFont.Name = "databaseFontLabel";
            cmbFont = new ComboBox
            {
                Name = "databaseFont",
                DropDownStyle = ComboBoxStyle.DropDown,
                AutoCompleteMode = AutoCompleteMode.SuggestAppend,
                AutoCompleteSource = AutoCompleteSource.ListItems,
                AccessibleName = Present("خط الواجهة"),
                TabIndex = 4,
                RightToLeft = RightToLeft.No
            };
            cmbFont.Items.Add(AppFonts.DefaultConfiguredFamily);
            if (!cmbFont.Items.Contains(AppFonts.OfficialFamilyName))
                cmbFont.Items.Add(AppFonts.OfficialFamilyName);
            try
            {
                using (var fonts = new System.Drawing.Text.InstalledFontCollection())
                    foreach (var family in fonts.Families.OrderBy(item => item.Name))
                        if (!cmbFont.Items.Contains(family.Name)) cmbFont.Items.Add(family.Name);
            }
            catch { /* A typed family and the built-in fallbacks remain available. */ }
            cmbFont.Text = UiTheme.LoadFontFamily();
            btnSaveAppearance = DesktopComponents.Action("databaseSaveAppearance", Present("حفظ المظهر"), AppIcon.Save);
            btnSaveAppearance.AccessibleName = Present("حفظ الخط والمظهر فقط");
            btnSaveAppearance.TabIndex = 5;
            btnSaveAppearance.Click += SaveAppearance_Click;
            Controls.AddRange(new Control[] { lblFont, cmbFont, btnSaveAppearance });
        }
        private void InitializeComponent()
        {
            this.SuspendLayout();
            // 
            // DatabaseSetingsForm
            // 
            this.ClientSize = new System.Drawing.Size(433, 365);
            this.Name = "DatabaseSetingsForm";
            this.ResumeLayout(false);

        }
    }
}

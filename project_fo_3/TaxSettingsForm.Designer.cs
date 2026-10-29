using System.Drawing;
using System.Windows.Forms;

namespace project_fo_3
{
    internal partial class TaxSettingsForm
    {
        private ComboBox cmbCountry;
        private CheckBox chkEnabled;
        private NumericUpDown numRate;
        private Label lblStatus;
        private AntdUI.Button btnSave;
        private AntdUI.Button btnCancel;

        private void InitializeComponent()
        {
            Text = "إعدادات الحسابات والضرائب";
            Name = "TaxSettingsForm";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(600, 460);
            MinimumSize = new Size(420, 390);
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = true;
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScroll = false;

            Panel header = new Panel { Name = "taxSettingsHeader", Dock = DockStyle.Top, Height = 90, Padding = new Padding(22, 12, 22, 10) };
            Label title = new Label { Name = "taxSettingsTitle", Text = "البلد وسياسة الضريبة", Dock = DockStyle.Top, Height = 34, TextAlign = ContentAlignment.MiddleRight, Font = AppFonts.Create(16F, FontStyle.Bold) };
            Label subtitle = new Label { Name = "taxSettingsSubtitle", Text = "تؤثر السياسة في الفواتير الجديدة فقط.", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight };
            header.Controls.Add(subtitle);
            header.Controls.Add(title);

            Panel body = new Panel { Name = "taxSettingsBody", Dock = DockStyle.Fill, AutoScroll = true,
                Padding = new Padding(24, 8, 24, 4), BackColor = UiTheme.Background };
            AntdUI.Panel card = DesktopComponents.Card("taxSettingsCard");
            card.Dock = DockStyle.None;
            card.Height = 230;
            card.Padding = new Padding(12, 4, 12, 4);
            TableLayoutPanel layout = new TableLayoutPanel
            {
                Name = "taxSettingsFields",
                Tag = "UiSurface",
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 6,
                AutoScroll = false,
                RightToLeft = RightToLeft.Yes
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));

            Label countryLabel = new Label { Name = "taxCountryLabel", Text = "بلد الضريبة", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight };
            cmbCountry = new ComboBox { Name = "cmbTaxCountry", DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "بلد الضريبة", TabIndex = 0 };
            cmbCountry.Items.AddRange(new object[] { "اليمن (YE)", "السعودية (SA)", "بلد آخر" });
            cmbCountry.SelectedIndex = 0;
            Panel countryHost = new Panel { Name = "taxCountryHost", Dock = DockStyle.Fill, Margin = new Padding(0, 2, 0, 4) };
            countryHost.Controls.Add(cmbCountry);
            countryHost.Resize += (sender, args) => cmbCountry.SetBounds(0, 0, countryHost.ClientSize.Width, UiTheme.StandardInputHeight);
            Panel enabledRow = new Panel { Name = "taxEnabledRow", Dock = DockStyle.Fill, BackColor = UiTheme.Surface };
            chkEnabled = new CheckBox { Name = "chkTaxEnabled", Text = string.Empty, Width = 24, Height = 44,
                AccessibleName = "تفعيل الضريبة", TabIndex = 1, Margin = Padding.Empty };
            Label enabledCaption = new Label { Name = "taxEnabledCaption", Text = "تفعيل الضريبة",
                AccessibleName = "حالة تفعيل الضريبة", TextAlign = ContentAlignment.MiddleRight };
            enabledRow.Controls.Add(chkEnabled);
            enabledRow.Controls.Add(enabledCaption);
            enabledRow.Resize += (sender, args) => LayoutEnabledRow();
            Label rateLabel = new Label { Name = "taxRateLabel", Text = "نسبة الضريبة (%)", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight };
            numRate = new NumericUpDown { Name = "numTaxRate", Dock = DockStyle.Fill, DecimalPlaces = 2, Minimum = 0, Maximum = 100, AccessibleName = "نسبة الضريبة", TabIndex = 2, Margin = new Padding(0, 2, 0, 4) };
            AntdUI.Panel rateFrame = DesktopComponents.NativeNumberFrame(numRate, "taxRateFrame");
            rateFrame.Dock = DockStyle.Fill;
            rateFrame.Margin = new Padding(0, 2, 0, 2);
            lblStatus = new Label { Name = "taxSettingsStatus", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Padding = new Padding(8), AccessibleName = "حالة إعدادات الضريبة" };
            FlowLayoutPanel actions = new FlowLayoutPanel
            {
                Name = "taxSettingsActions",
                Dock = DockStyle.Fill,
                FlowDirection = UiDirection.IsLeftToRight ? FlowDirection.LeftToRight : FlowDirection.RightToLeft,
                WrapContents = false,
                RightToLeft = RightToLeft.No,
                Padding = new Padding(22, 6, 22, 0)
            };
            btnSave = DesktopComponents.Action("btnSaveTaxSettings", "حفظ", AppIcon.Save);
            btnSave.Size = new Size(120, 44);
            btnSave.Enabled = false;
            btnSave.TabIndex = 3;
            btnCancel = DesktopComponents.Action("btnCancelTaxSettings", "إلغاء", AppIcon.Close);
            btnCancel.Size = new Size(120, 44);
            btnCancel.DialogResult = DialogResult.Cancel;
            btnCancel.TabIndex = 4;
            actions.Controls.Add(btnSave);
            actions.Controls.Add(btnCancel);

            layout.Controls.Add(countryLabel, 0, 0);
            layout.Controls.Add(countryHost, 0, 1);
            layout.Controls.Add(enabledRow, 0, 2);
            layout.Controls.Add(rateLabel, 0, 3);
            layout.Controls.Add(rateFrame, 0, 4);
            layout.Controls.Add(lblStatus, 0, 5);
            card.Controls.Add(layout);
            body.Controls.Add(card);
            Panel footer = new Panel { Name = "taxSettingsFooter", Dock = DockStyle.Bottom, Height = 62,
                BackColor = UiTheme.Background };
            footer.Controls.Add(actions);
            Controls.Add(body);
            Controls.Add(footer);
            Controls.Add(header);
            AcceptButton = btnSave;
            CancelButton = btnCancel;

            UiTheme.ApplyDialogHeader(header, title, subtitle, null);
            UiTheme.ApplyFieldLabels(countryLabel, rateLabel);
            lblStatus.Font = AppFonts.Create(9F, FontStyle.Bold);
            UiTheme.ApplyTo(btnSave);
            UiTheme.ApplyTo(btnCancel);
            AntdThemeAdapter.Apply(card);
            WinFormsShapeAdapter.ApplyInput(cmbCountry);
        }
    }
}

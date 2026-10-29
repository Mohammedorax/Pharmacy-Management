using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace project_fo_3
{
    internal sealed class ReportPrintOptionsDialog : Form
    {
        private readonly PharmacyProfile profile;
        private readonly bool identityUnavailable;
        private readonly ReportPrintOptions defaults;
        private readonly bool previewMode;
        private readonly bool invoiceOnly;
        private bool leftToRight;
        private readonly Control[] identityContents = new Control[7];
        private static readonly string[] IdentityLabels =
            { "اسم المنشأة", "الشعار", "الرقم الضريبي", "السجل التجاري", "الجوال", "العنوان", "الموقع" };
        private readonly AntdUI.Select orientation = DesktopComponents.Select("reportPrintOrientation", "اتجاه التقرير",
            UiLanguageCatalog.GetDisplayText("تلقائي حسب الأعمدة"), UiLanguageCatalog.GetDisplayText("طولي"),
            UiLanguageCatalog.GetDisplayText("عرضي"));
        private readonly AntdUI.Select density = DesktopComponents.Select("reportPrintDensity", "كثافة الصفوف",
            UiLanguageCatalog.GetDisplayText("كثافة عادية"), UiLanguageCatalog.GetDisplayText("كثافة مدمجة"));
        private readonly CheckBox companyName = new CheckBox { Name = "reportPrintCompanyName", Text = "اسم المنشأة" };
        private readonly CheckBox logo = new CheckBox { Name = "reportPrintLogo", Text = "الشعار" };
        private readonly CheckBox taxNumber = new CheckBox { Name = "reportPrintTaxNumber", Text = "الرقم الضريبي" };
        private readonly CheckBox registration = new CheckBox { Name = "reportPrintRegistration", Text = "السجل التجاري" };
        private readonly CheckBox mobile = new CheckBox { Name = "reportPrintMobile", Text = "الجوال" };
        private readonly CheckBox address = new CheckBox { Name = "reportPrintAddress", Text = "العنوان" };
        private readonly CheckBox website = new CheckBox { Name = "reportPrintWebsite", Text = "الموقع" };
        private readonly Label status = new Label { Name = "reportPrintStatus" };
        private readonly AntdUI.Button preview = DesktopComponents.Action("reportPrintPreview", "معاينة الطباعة", AppIcon.Print);
        private readonly AntdUI.Button cancel = DesktopComponents.Action("reportPrintCancel", "إلغاء", AppIcon.Close);
        private readonly ToolTip valueTips = new ToolTip();

        internal ReportPrintOptions SelectedOptions { get; private set; }

        internal ReportPrintOptionsDialog(PharmacyProfile profile, bool previewMode = false, ReportPrintOptions defaults = null, bool? useLeftToRight = null, bool invoiceOnly = false)
        {
            this.invoiceOnly = invoiceOnly;
            identityUnavailable = profile == null;
            this.profile = profile ?? new PharmacyProfile();
            this.defaults = defaults;
            this.previewMode = previewMode;
            leftToRight = useLeftToRight ?? UiDirection.IsLeftToRight;
            Name = "ReportPrintOptionsDialog";
            Text = "خيارات طباعة التقرير";
            Size = new Size(680, 550);
            MinimumSize = new Size(580, 500);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true; MinimizeBox = true;
            RightToLeft = RightToLeft.Yes; RightToLeftLayout = true;
            Font = AppFonts.Create(9.5F);
            KeyPreview = true;
            KeyDown += (sender, args) => { if (args.KeyCode == Keys.Escape) Close(); };
            ConfigureIdentityChoices();
            BuildLayout();
            if (defaults != null)
            {
                orientation.SelectedIndex = Math.Max(0, Math.Min(orientation.Items.Count - 1, (int)defaults.Orientation));
                density.SelectedIndex = Math.Max(0, Math.Min(density.Items.Count - 1, (int)defaults.Density));
            }
            UiTheme.Apply(this);
            ApplyPresentation(leftToRight);
        }

        private string Localize(string arabicText)
        {
            if (leftToRight && invoiceOnly)
            {
                switch (arabicText)
                {
                    case "خيارات ترويسة الفاتورة": return "Invoice header options";
                    case "اختر بيانات المنشأة التي تظهر في الفاتورة.": return "Choose the company details shown on the invoice.";
                    case "تعذر تحميل بيانات المنشأة؛ ستظهر الفاتورة بدون ترويسة منشأة.": return "Company details could not be loaded. The invoice will have no company header.";
                    case "اختر بيانات المنشأة التي تريد إظهارها في الفاتورة.": return "Choose the company details to show on the invoice.";
                }
            }
            return leftToRight ? UiLanguageCatalog.Translate(arabicText) : arabicText;
        }

        // English remains simulated until the other application screens pass language review.
        internal void ApplyPresentation(bool useLeftToRight)
        {
            leftToRight = useLeftToRight;
            Text = Localize(invoiceOnly ? "خيارات ترويسة الفاتورة" : "خيارات طباعة التقرير");
            Controls["reportPrintHeader"].AccessibleName = Text;
            Controls["reportPrintBody"].AccessibleName = Text;
            Controls["reportPrintFooter"].AccessibleName = Localize("إجراءات التقرير");
            Controls["reportPrintHeader"].Controls["reportPrintTitle"].Text = Text;
            Controls["reportPrintHeader"].Controls["reportPrintSubtitle"].Text =
                Localize(invoiceOnly ? "اختر بيانات المنشأة التي تظهر في الفاتورة." : "الاتجاه والكثافة وبيانات المنشأة اختيارات مستقلة عن بيانات التقرير.");
            var body = (AntdUI.Panel)Controls["reportPrintBody"];
            var selectors = (TableLayoutPanel)body.Controls.Find("reportPrintSelectors", true).Single();
            selectors.AccessibleName = Localize("اتجاه الصفحة وكثافة الصفوف");
            selectors.Visible = !invoiceOnly;
            selectors.TabStop = !invoiceOnly;
            selectors.GetControlFromPosition(0, 0).Text = Localize("اتجاه الصفحة");
            selectors.GetControlFromPosition(0, 1).Text = Localize("كثافة الصفوف");
            int orientationIndex = orientation.SelectedIndex;
            int densityIndex = density.SelectedIndex;
            orientation.SelectedIndex = -1;
            orientation.Items.Clear();
            foreach (string title in new[] { "تلقائي حسب الأعمدة", "طولي", "عرضي" }) orientation.Items.Add(Localize(title));
            density.SelectedIndex = -1;
            density.Items.Clear();
            foreach (string title in new[] { "كثافة عادية", "كثافة مدمجة" }) density.Items.Add(Localize(title));
            orientation.SelectedIndex = Math.Max(0, orientationIndex);
            density.SelectedIndex = Math.Max(0, densityIndex);
            orientation.Text = Convert.ToString(orientation.Items[orientation.SelectedIndex]);
            density.Text = Convert.ToString(density.Items[density.SelectedIndex]);
            orientation.AccessibleName = Localize("اتجاه التقرير");
            density.AccessibleName = Localize("كثافة الصفوف");
            body.Controls.Find("reportPrintIdentityTitle", true).Single().Text = Localize("بيانات المنشأة في الترويسة");
            var identity = (TableLayoutPanel)body.Controls.Find("reportPrintIdentity", true).Single();
            identity.AccessibleName = Localize("بيانات المنشأة في الترويسة");
            identity.SuspendLayout();
            for (int index = 0; index < identityContents.Length; index++)
            {
                Control content = identityContents[index];
                string caption = Localize(IdentityLabels[index]);
                content.Text = content is CheckBox ? caption : caption + (leftToRight ? " (unavailable)" : " (غير متوفر)");
                content.AccessibleName = content.Text;
                content.Dock = DockStyle.Fill;
                identity.SetCellPosition(content, new TableLayoutPanelCellPosition(leftToRight ? index % 2 : 1 - index % 2, index / 2));
                if (content is Label)
                    valueTips.SetToolTip(content, content.Text);
            }
            identity.ResumeLayout(true);
            preview.Text = Localize("معاينة الطباعة");
            cancel.Text = Localize("إلغاء");
            preview.AccessibleName = preview.Text;
            cancel.AccessibleName = cancel.Text;
            status.Text = Localize(previewMode ? "معاينة الخيارات؛ لن تُفتح طابعة أو قاعدة بيانات." :
                identityUnavailable ? (invoiceOnly ? "تعذر تحميل بيانات المنشأة؛ ستظهر الفاتورة بدون ترويسة منشأة." : "تعذر تحميل بيانات المنشأة؛ يمكن طباعة التقرير بدون ترويسة.") :
                (invoiceOnly ? "اختر بيانات المنشأة التي تريد إظهارها في الفاتورة." : "اختر شكل التقرير والبيانات التي تريد إظهارها في ترويسته."));
            status.AccessibleName = status.Text;
            UiDirection.Apply(this, leftToRight);
            UiTheme.UseDedicatedStatus(this);
            identity.RightToLeft = RightToLeft.No;
            ApplyVisualTheme();
        }

        internal static bool TryChoose(IWin32Window owner, PharmacyProfile profile, ReportPrintOptions defaults,
            out ReportPrintOptions options)
        {
            return TryChoose(owner,profile,defaults,out options,UiDirection.IsLeftToRight);
        }

        internal static bool TryChoose(IWin32Window owner, PharmacyProfile profile, ReportPrintOptions defaults,
            out ReportPrintOptions options, bool leftToRight, Func<bool> isCurrent = null, bool invoiceOnly = false)
        {
            options=null;if(!ReportPreviewLifetime.ContextCurrent(isCurrent))return false;
            using (var dialog = new ReportPrintOptionsDialog(profile, false, defaults, leftToRight, invoiceOnly))
            using (var lifetime=isCurrent==null?null:new ReportPreviewLifetime(dialog,isCurrent))
            {
                if(lifetime!=null&&!lifetime.CanUse)return false;
                bool accepted = dialog.ShowDialog(owner) == DialogResult.OK && (lifetime==null||lifetime.CanUse);
                options = accepted ? dialog.SelectedOptions : null;
                return accepted;
            }
        }

        private void BuildLayout()
        {
            var header = new Panel { Name = "reportPrintHeader", Dock = DockStyle.Top, Height = 80,
                Padding = new Padding(22, 10, 22, 8) };
            header.Controls.Add(new Label { Name = "reportPrintSubtitle", Dock = DockStyle.Fill,
                Text = "الاتجاه والكثافة وبيانات المنشأة اختيارات مستقلة عن بيانات التقرير.",
                TextAlign = ContentAlignment.MiddleRight });
            header.Controls.Add(new Label { Name = "reportPrintTitle", Dock = DockStyle.Top, Height = 38,
                Text = "خيارات طباعة التقرير", Font = AppFonts.Create(16F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleRight });

            var body = new AntdUI.Panel { Name = "reportPrintBody", Dock = DockStyle.Fill,
                AutoScroll = true,
                Padding = new Padding(22, 12, 22, 8) };
            var selectors = new TableLayoutPanel { Name = "reportPrintSelectors", Dock = DockStyle.Top,
                Height = 110, ColumnCount = 2, RowCount = 2 };
            selectors.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 134));
            selectors.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            selectors.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            selectors.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            AddSelector(selectors, "اتجاه الصفحة", orientation, 0);
            AddSelector(selectors, "كثافة الصفوف", density, 1);

            var identityTitle = new Label { Name = "reportPrintIdentityTitle", Dock = DockStyle.Top,
                Height = 42, Text = "بيانات المنشأة في الترويسة", Font = AppFonts.Create(10F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleRight };
            var identity = new TableLayoutPanel { Name = "reportPrintIdentity", Dock = DockStyle.Top,
                Height = 184, ColumnCount = 2, RowCount = 4, Padding = new Padding(4, 4, 4, 4) };
            identity.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            identity.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            for (int i = 0; i < 4; i++) identity.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            CheckBox[] fields = { companyName, logo, taxNumber, registration, mobile, address, website };
            for (int i = 0; i < fields.Length; i++)
            {
                Control content;
                if (fields[i].Enabled)
                {
                    fields[i].AutoSize = false;
                    fields[i].AutoEllipsis = true;
                    fields[i].AccessibleName = fields[i].Text;
                    content = fields[i];
                }
                else
                {
                    content = new Label { Name = fields[i].Name + "Unavailable",
                        Text = fields[i].Text + " (غير متوفر)", AutoSize = false, AutoEllipsis = true,
                        TextAlign = ContentAlignment.MiddleRight, AccessibleName = fields[i].Text + " غير متوفر" };
                }
                content.Dock = DockStyle.Fill;
                content.Margin = new Padding(6, 0, 6, 0);
                identityContents[i] = content;
                identity.Controls.Add(content, leftToRight ? i % 2 : 1 - i % 2, i / 2);
            }
            body.Controls.Add(identity);
            body.Controls.Add(identityTitle);
            body.Controls.Add(selectors);

            status.Dock = DockStyle.Bottom; status.Height = 42;
            status.Padding = new Padding(22, 5, 22, 5);
            status.TextAlign = ContentAlignment.MiddleRight;
            var footer = new FlowLayoutPanel { Name = "reportPrintFooter", Dock = DockStyle.Bottom,
                Height = 68, WrapContents = false, Padding = new Padding(20, 10, 20, 8) };
            preview.Size = new Size(162, 44);
            cancel.Size = new Size(112, 44);
            preview.Click += (sender, args) => Accept();
            cancel.Click += (sender, args) => Close();
            footer.Controls.Add(preview); footer.Controls.Add(cancel);
            Controls.Add(body); Controls.Add(status); Controls.Add(footer); Controls.Add(header);
        }

        private static void AddSelector(TableLayoutPanel parent, string title, AntdUI.Select selector, int row)
        {
            parent.Controls.Add(new Label { Text = title, Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight }, 0, row);
            selector.Dock = DockStyle.Fill;
            selector.Margin = new Padding(4, 4, 4, 4);
            parent.Controls.Add(selector, 1, row);
        }

        private void ConfigureIdentityChoices()
        {
            Configure(companyName, ResolveCompanyName(), defaults?.ShowCompanyName ?? true);
            Configure(logo, IsUsableLogo(profile.LogoPath) ? profile.LogoPath : string.Empty, defaults?.ShowLogo ?? true);
            Configure(taxNumber, profile.TaxNo, defaults?.ShowTaxNumber ?? false);
            Configure(registration, profile.CommercialRegNo, defaults?.ShowCommercialRegistration ?? false);
            Configure(mobile, profile.Mobile, defaults?.ShowMobile ?? false);
            Configure(address, profile.Address, defaults?.ShowAddress ?? false);
            Configure(website, profile.Website, defaults?.ShowWebsite ?? false);
        }

        private void Configure(CheckBox choice, string value, bool defaultSelected)
        {
            choice.Enabled = !string.IsNullOrWhiteSpace(value);
            choice.Checked = choice.Enabled && defaultSelected;
            valueTips.SetToolTip(choice, choice.Enabled ? value.Trim() : "غير متوفر في ملف الصيدلية");
        }

        private string ResolveCompanyName()
        {
            string preferred = leftToRight ? profile.NameEn : profile.NameAr;
            string alternative = leftToRight ? profile.NameAr : profile.NameEn;
            return !string.IsNullOrWhiteSpace(preferred) ? preferred : alternative ?? string.Empty;
        }

        private static bool IsUsableLogo(string path)
        {
            try { if (string.IsNullOrWhiteSpace(path) || !File.Exists(path) || new FileInfo(path).Length > 10L * 1024 * 1024) return false; using (Image image = Image.FromFile(path)) return image.Width > 0 && image.Height > 0; }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException ||
                exception is ArgumentException || exception is NotSupportedException || exception is OutOfMemoryException) { return false; }
        }

        private void ApplyVisualTheme()
        {
            var header = Controls["reportPrintHeader"];
            var body = Controls["reportPrintBody"];
            header.BackColor = UiTheme.Background;
            body.BackColor = UiTheme.Background;
            AntdThemeAdapter.Apply((AntdUI.Panel)body);
            foreach (Label label in header.Controls.OfType<Label>())
            { label.BackColor = UiTheme.Background; label.ForeColor = label.Name == "reportPrintTitle" ? UiTheme.Text : UiTheme.MutedText; }
            foreach (Control control in body.Controls)
                if (control is TableLayoutPanel || control is Label) control.BackColor = UiTheme.Background;
            foreach (Label label in body.Controls.Find("reportPrintSelectors", true).OfType<TableLayoutPanel>()
                .SelectMany(panel => panel.Controls.OfType<Label>()))
            { label.BackColor = UiTheme.Background; label.ForeColor = UiTheme.Text; }
            Control identityTitle = body.Controls.Find("reportPrintIdentityTitle", true).Single();
            identityTitle.ForeColor = UiTheme.Text;
            foreach (CheckBox choice in body.Controls.Find("reportPrintIdentity", true)
                .OfType<TableLayoutPanel>().SelectMany(panel => panel.Controls.OfType<CheckBox>()))
            { choice.BackColor = UiTheme.Background; choice.ForeColor = choice.Enabled ? UiTheme.Text : UiTheme.MutedText;
                choice.FlatStyle = FlatStyle.Flat; choice.FlatAppearance.BorderColor = UiTheme.Border;
                choice.RightToLeft = RightToLeft.No;
                choice.CheckAlign = leftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
                choice.TextAlign = leftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight; }
            foreach (Label unavailable in body.Controls.Find("reportPrintIdentity", true)
                .OfType<TableLayoutPanel>().SelectMany(panel => panel.Controls.OfType<Label>()))
            { unavailable.BackColor = UiTheme.Background; unavailable.ForeColor = UiTheme.MutedText; }
            AntdThemeAdapter.Apply(orientation); AntdThemeAdapter.Apply(density);
            var footer = (FlowLayoutPanel)Controls["reportPrintFooter"];
            footer.BackColor = UiTheme.Background;
            footer.RightToLeft = RightToLeft.No;
            footer.FlowDirection = leftToRight ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;
            DesktopComponents.StyleOutlinedFooterAction(preview);
            DesktopComponents.StyleOutlinedFooterAction(cancel);
            status.BackColor = UiTheme.Background;
            status.ForeColor = UiTheme.MutedText;
        }

        private void Accept()
        {
            if (previewMode)
            {
                UiTheme.SetStatus(status, Localize("معاينة فقط؛ لم تُفتح طابعة أو قاعدة بيانات."), null);
                status.AccessibleName = status.Text;
                return;
            }
            if (logo.Checked && !IsUsableLogo(profile.LogoPath))
            { UiTheme.SetStatus(status, Localize("ملف الشعار غير متاح أو كبير جدًا؛ صحح المسار في ملف الصيدلية."), false); return; }
            SelectedOptions = new ReportPrintOptions
            {
                Orientation = invoiceOnly ? ReportPageOrientation.Portrait : (ReportPageOrientation)Math.Max(0, orientation.SelectedIndex),
                Density = invoiceOnly ? ReportDensity.Standard : (ReportDensity)Math.Max(0, density.SelectedIndex),
                CompanyName = ResolveCompanyName(),
                LogoPath = profile.LogoPath, TaxNumber = profile.TaxNo,
                CommercialRegistration = profile.CommercialRegNo, Mobile = profile.Mobile,
                Address = profile.Address, Website = profile.Website,
                ShowCompanyName = companyName.Checked, ShowLogo = logo.Checked,
                ShowTaxNumber = taxNumber.Checked, ShowCommercialRegistration = registration.Checked,
                ShowMobile = mobile.Checked, ShowAddress = address.Checked, ShowWebsite = website.Checked
            };
            DialogResult = DialogResult.OK;
            Close();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                valueTips.Dispose();
                foreach (CheckBox choice in new[] { companyName, logo, taxNumber, registration, mobile, address, website })
                    if (choice.Parent == null) choice.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}


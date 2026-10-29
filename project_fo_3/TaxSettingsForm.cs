using System;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace project_fo_3
{
    internal partial class TaxSettingsForm : Form
    {
        private bool loaded;
        private bool busy;
        private bool saving;
        private bool saveCompleted;
        private bool closeWasCancelled;
        private readonly bool previewMode;
        private readonly bool presentationLeftToRight;
        internal bool PresentationLeftToRight => presentationLeftToRight;
        internal string SavedCountryCode { get; private set; }

        public TaxSettingsForm()
            : this(false, UiLanguageCatalog.IsEnglishActive)
        {
        }

        // This isolated constructor exists only for the desktop smoke test. It never reads or writes SQL.
        internal TaxSettingsForm(bool previewMode)
            : this(previewMode, UiLanguageCatalog.IsEnglishActive)
        {
        }

        private readonly Func<Task<TaxPolicy>> readPolicy;
        private readonly Func<decimal, bool, string, Task> savePolicy;

        internal TaxSettingsForm(bool previewMode, bool presentationLeftToRight)
            : this(previewMode, presentationLeftToRight, ReadPolicyAsync,
                (rate, enabled, country) => Task.Run(() => AccountingService.SaveTaxSettings(rate, enabled, country))) { }

        internal TaxSettingsForm(bool previewMode, bool presentationLeftToRight,
            Func<Task<TaxPolicy>> readPolicy, Func<decimal, bool, string, Task> savePolicy)
        {
            this.readPolicy = readPolicy ?? throw new ArgumentNullException(nameof(readPolicy));
            this.savePolicy = savePolicy ?? throw new ArgumentNullException(nameof(savePolicy));
            this.previewMode = previewMode;
            this.presentationLeftToRight = presentationLeftToRight;
            InitializeComponent();
            UiTheme.Apply(this);
            if (previewMode)
            {
                Control runtimeStatus = Controls.Find("__UiStatusStrip", true).FirstOrDefault();
                if (runtimeStatus != null) runtimeStatus.Dispose();
            }
            UiDirection.Apply(this, presentationLeftToRight);
            ApplyDisplayLanguage();
            StyleTaxShell();
            DesktopComponents.AddComboBoxDropdown(cmbCountry, "taxCountryDropdown", Present("فتح قائمة بلد الضريبة"));
            Control countryFrame = Controls.Find("taxCountryDropdownFrame", true).FirstOrDefault();
            if (countryFrame != null) countryFrame.AccessibleName = cmbCountry.AccessibleName;
            cmbCountry.SelectedIndexChanged += (sender, args) => RefreshPolicyState();
            chkEnabled.CheckedChanged += (sender, args) => RefreshPolicyState();
            btnSave.Click += SaveSettings;
            btnCancel.Click += (sender, args) =>
            {
                if (saving) return;
                DialogResult = DialogResult.Cancel;
                Close();
            };
            if (previewMode)
            {
                loaded = true;
            }
            else
            {
                Shown += async (sender, args) => await LoadSettingsAsync();
            }
            FormClosing += (sender, args) =>
            {
                if (saving)
                {
                    args.Cancel = true;
                    lblStatus.Text = Present("جار تأكيد حفظ سياسة الضريبة؛ انتظر اكتمال العملية قبل الإغلاق.");
                }
            };
            RefreshPolicyState();
            if (previewMode)
            {
                lblStatus.Text = Present("معاينة فقط: لا حفظ ولا اتصال بقاعدة البيانات.");
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs args)
        {
            base.OnFormClosing(args);
            closeWasCancelled = args.Cancel;
        }

        private string Present(string arabic)
        {
            return presentationLeftToRight ? UiLanguageCatalog.Translate(arabic) : arabic;
        }

        private void ApplyDisplayLanguage()
        {
            int selectedCountry = Math.Max(0, cmbCountry.SelectedIndex);
            Text = Present("إعدادات الحسابات والضرائب");
            Label title = Controls.Find("taxSettingsTitle", true).OfType<Label>().FirstOrDefault();
            Label subtitle = Controls.Find("taxSettingsSubtitle", true).OfType<Label>().FirstOrDefault();
            Label countryLabel = Controls.Find("taxCountryLabel", true).OfType<Label>().FirstOrDefault();
            Label enabledCaption = Controls.Find("taxEnabledCaption", true).OfType<Label>().FirstOrDefault();
            Label rateLabel = Controls.Find("taxRateLabel", true).OfType<Label>().FirstOrDefault();
            if (title != null) title.Text = Present("البلد وسياسة الضريبة");
            if (subtitle != null) subtitle.Text = Present("تؤثر السياسة في الفواتير الجديدة فقط.");
            if (countryLabel != null) countryLabel.Text = Present("بلد الضريبة");
            if (enabledCaption != null) enabledCaption.Text = Present("تفعيل الضريبة");
            if (rateLabel != null) rateLabel.Text = Present("نسبة الضريبة (%)");
            cmbCountry.Items.Clear();
            cmbCountry.Items.AddRange(new object[] { Present("اليمن (YE)"), Present("السعودية (SA)"), Present("بلد آخر") });
            cmbCountry.SelectedIndex = Math.Min(selectedCountry, cmbCountry.Items.Count - 1);
            foreach (var pair in new[]
            {
                new[] { "taxSettingsHeader", "البلد وسياسة الضريبة" },
                new[] { "taxSettingsBody", "إعدادات الحسابات والضرائب" },
                new[] { "taxSettingsCard", "البلد وسياسة الضريبة" },
                new[] { "taxSettingsFields", "حقول إعدادات الضريبة" },
                new[] { "taxCountryHost", "بلد الضريبة" },
                new[] { "taxEnabledRow", "تفعيل الضريبة" },
                new[] { "taxRateFrame", "نسبة الضريبة" },
                new[] { "taxSettingsFooter", "إجراءات إعدادات الضريبة" },
                new[] { "taxSettingsActions", "إجراءات إعدادات الضريبة" },
                new[] { "taxEnabledCaption", "تفعيل الضريبة" }
            })
            {
                Control named = Controls.Find(pair[0], true).FirstOrDefault();
                if (named != null) named.AccessibleName = Present(pair[1]);
            }
            cmbCountry.AccessibleName = Present("بلد الضريبة");
            chkEnabled.AccessibleName = Present("تفعيل الضريبة");
            numRate.AccessibleName = Present("نسبة الضريبة");
            lblStatus.AccessibleName = Present("حالة إعدادات الضريبة");
            btnSave.Text = btnSave.AccessibleName = Present("حفظ");
            btnCancel.Text = btnCancel.AccessibleName = Present("إلغاء");
            ContentAlignment alignment = presentationLeftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
            foreach (Label label in new[] { title, subtitle, countryLabel, enabledCaption, rateLabel, lblStatus })
                if (label != null) label.TextAlign = alignment;
            cmbCountry.RightToLeft = presentationLeftToRight ? RightToLeft.No : RightToLeft.Yes;
        }

        private string SelectedCountry => cmbCountry.SelectedIndex == 0 ? "YE" : cmbCountry.SelectedIndex == 1 ? "SA" : "OTHER";

        private void StyleTaxShell()
        {
            Panel header = Controls.Find("taxSettingsHeader", true).OfType<Panel>().FirstOrDefault();
            Label title = Controls.Find("taxSettingsTitle", true).OfType<Label>().FirstOrDefault();
            Label subtitle = Controls.Find("taxSettingsSubtitle", true).OfType<Label>().FirstOrDefault();
            Panel body = Controls.Find("taxSettingsBody", true).OfType<Panel>().FirstOrDefault();
            AntdUI.Panel card = Controls.Find("taxSettingsCard", true).OfType<AntdUI.Panel>().FirstOrDefault();
            Panel footer = Controls.Find("taxSettingsFooter", true).OfType<Panel>().FirstOrDefault();
            TableLayoutPanel fields = Controls.Find("taxSettingsFields", true).OfType<TableLayoutPanel>().FirstOrDefault();
            FlowLayoutPanel actions = Controls.Find("taxSettingsActions", true).OfType<FlowLayoutPanel>().FirstOrDefault();
            if (header != null) header.BackColor = UiTheme.Background;
            if (title != null) { title.BackColor = UiTheme.Background; title.ForeColor = UiTheme.Text; }
            if (subtitle != null) { subtitle.BackColor = UiTheme.Background; subtitle.ForeColor = UiTheme.MutedText; }
            if (body != null) body.BackColor = UiTheme.Background;
            if (card != null) AntdThemeAdapter.Apply(card);
            if (fields != null) fields.BackColor = UiTheme.Surface;
            if (footer != null) footer.BackColor = UiTheme.Background;
            Panel enabledRow = Controls.Find("taxEnabledRow", true).OfType<Panel>().FirstOrDefault();
            if (enabledRow != null) enabledRow.BackColor = UiTheme.Surface;
            if (actions != null)
            {
                actions.BackColor = UiTheme.Background;
                actions.RightToLeft = RightToLeft.No;
                actions.FlowDirection = presentationLeftToRight ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;
            }
            numRate.RightToLeft = RightToLeft.No;
            numRate.TextAlign = HorizontalAlignment.Left;
            DesktopComponents.StyleNativeNumberFrame((AntdUI.Panel)numRate.Parent, numRate);
            lblStatus.BorderStyle = BorderStyle.None;
            lblStatus.BackColor = UiTheme.Surface;
            lblStatus.ForeColor = UiTheme.Text;
            WinFormsShapeAdapter.RemoveInputShape(lblStatus);
            StyleTaxAction(btnSave, AppIcon.Save);
            StyleTaxAction(btnCancel, AppIcon.Close);
            btnSave.IconPosition = btnCancel.IconPosition = presentationLeftToRight
                ? AntdUI.TAlignMini.Left : AntdUI.TAlignMini.Right;
            if (body != null)
            {
                body.Resize += (sender, args) => LayoutTaxCard();
                LayoutTaxCard();
            }
            if (header != null)
            {
                header.Resize += (sender, args) => LayoutTaxHeader();
                LayoutTaxHeader();
            }
            lblStatus.TextChanged += (sender, args) =>
            {
                lblStatus.AccessibleName = lblStatus.Text;
                LayoutTaxCard();
            };
            LayoutEnabledRow();
            Label enabledCaption = Controls.Find("taxEnabledCaption", true).OfType<Label>().FirstOrDefault();
            if (enabledCaption != null)
                enabledCaption.Click += (sender, args) => { if (chkEnabled.Enabled) chkEnabled.Checked = !chkEnabled.Checked; };
        }

        private void LayoutTaxCard()
        {
            Panel body = Controls.Find("taxSettingsBody", true).OfType<Panel>().FirstOrDefault();
            AntdUI.Panel card = Controls.Find("taxSettingsCard", true).OfType<AntdUI.Panel>().FirstOrDefault();
            TableLayoutPanel fields = Controls.Find("taxSettingsFields", true).OfType<TableLayoutPanel>().FirstOrDefault();
            if (body == null || card == null || body.ClientSize.Width <= 0) return;
            bool compactHeight = body.ClientSize.Height < card.Height + body.Padding.Vertical;
            int scrollbarWidth = compactHeight ? SystemInformation.VerticalScrollBarWidth : 0;
            int viewportWidth = Math.Max(1, body.ClientSize.Width - scrollbarWidth);
            int originX = compactHeight && !presentationLeftToRight ? scrollbarWidth : 0;
            int width = Math.Min(640, Math.Max(1, viewportWidth - body.Padding.Horizontal - 6));
            if (fields != null)
            {
                int borderInset = (int)Math.Ceiling(card.BorderWidth * 2);
                int statusWidth = Math.Max(1, width - card.Padding.Horizontal - borderInset - lblStatus.Padding.Horizontal - lblStatus.Margin.Horizontal);
                int statusHeight = Math.Max(50, TextRenderer.MeasureText(lblStatus.Text, lblStatus.Font,
                    new Size(statusWidth, int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height + lblStatus.Padding.Vertical + lblStatus.Margin.Vertical + 4);
                fields.RowStyles[5].Height = statusHeight;
                card.Height = 184 + statusHeight + card.Padding.Vertical + borderInset;
                compactHeight = body.ClientSize.Height < card.Height + body.Padding.Vertical;
                scrollbarWidth = compactHeight ? SystemInformation.VerticalScrollBarWidth : 0;
                viewportWidth = Math.Max(1, body.ClientSize.Width - scrollbarWidth);
                originX = compactHeight && !presentationLeftToRight ? scrollbarWidth : 0;
                width = Math.Min(640, Math.Max(1, viewportWidth - body.Padding.Horizontal - 6));
                statusWidth = Math.Max(1, width - card.Padding.Horizontal - borderInset - lblStatus.Padding.Horizontal - lblStatus.Margin.Horizontal);
                statusHeight = Math.Max(50, TextRenderer.MeasureText(lblStatus.Text, lblStatus.Font,
                    new Size(statusWidth, int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height + lblStatus.Padding.Vertical + lblStatus.Margin.Vertical + 4);
                fields.RowStyles[5].Height = statusHeight;
                card.Height = 184 + statusHeight + card.Padding.Vertical + borderInset;
            }
            card.SetBounds(originX + Math.Max(body.Padding.Left, (viewportWidth - width) / 2),
                Math.Max(body.Padding.Top, (body.ClientSize.Height - card.Height) / 2), width, card.Height);
            body.AutoScrollMinSize = new Size(0, Math.Max(0, card.Bottom + body.Padding.Bottom - 1));
            body.PerformLayout();
        }

        private void LayoutTaxHeader()
        {
            Panel header = Controls.Find("taxSettingsHeader", true).OfType<Panel>().FirstOrDefault();
            if (header == null) return;
            Label title = (Label)header.Controls["taxSettingsTitle"];
            Label subtitle = (Label)header.Controls["taxSettingsSubtitle"];
            int width = Math.Max(1, header.ClientSize.Width - header.Padding.Horizontal);
            title.Height = TextRenderer.MeasureText(title.Text, title.Font, new Size(width, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height + 4;
            int subtitleHeight = Math.Max(24, TextRenderer.MeasureText(subtitle.Text, subtitle.Font, new Size(width, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height + 4);
            header.Height = header.Padding.Vertical + title.Height + subtitleHeight;
            title.AccessibleName = title.Text; subtitle.AccessibleName = subtitle.Text;
        }

        private void LayoutEnabledRow()
        {
            Panel row = Controls.Find("taxEnabledRow", true).OfType<Panel>().FirstOrDefault();
            Label caption = Controls.Find("taxEnabledCaption", true).OfType<Label>().FirstOrDefault();
            if (row == null || caption == null) return;
            const int gap = 8;
            bool leftToRight = presentationLeftToRight;
            chkEnabled.SetBounds(leftToRight ? 0 : row.ClientSize.Width - chkEnabled.Width, 0,
                chkEnabled.Width, row.ClientSize.Height);
            caption.SetBounds(leftToRight ? chkEnabled.Right + gap : 0, 0,
                Math.Max(0, row.ClientSize.Width - chkEnabled.Width - gap), row.ClientSize.Height);
            caption.RightToLeft = RightToLeft.No;
            caption.TextAlign = leftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
        }

        private static void StyleTaxAction(AntdUI.Button button, AppIcon icon)
        {
            DesktopComponents.StyleOutlinedFooterAction(button);
            button.Font = AppFonts.Create(10F, FontStyle.Bold);
            button.IconSvg = OutlinedIcons.Svg(icon, true);
            button.IconSize = new Size(18, 18);
        }

        private static Task<TaxPolicy> ReadPolicyAsync()
        {
            return Task.Run(() =>
            {
                TaxSettingsRecovery.Recover();
                string country = TaxPolicyService.GetConfiguredCountryCode().Trim().ToUpperInvariant();
                DataRow settings = AccountingService.GetTaxSettings();
                return new TaxPolicy(country, settings != null && Convert.ToBoolean(settings["IsEnabled"]),
                    settings == null ? 0m : Convert.ToDecimal(settings["TaxRate"]));
            });
        }

        private bool HasSettingsAccess()
        {
            if (IsDisposed || Disposing) return false;
            if (PermissionService.CanOpen(AppPermission.Settings)) return true;
            Close();
            return false;
        }

        private async Task LoadSettingsAsync()
        {
            if (!HasSettingsAccess()) return;
            SetBusy(true, Present("جار تحميل سياسة الضريبة..."));
            try
            {
                var result = await readPolicy();
                if (!HasSettingsAccess()) return;
                cmbCountry.SelectedIndex = result.CountryCode == "YE" ? 0 : result.CountryCode == "SA" ? 1 : 2;
                numRate.Value = result.Rate;
                chkEnabled.Checked = result.IsEnabled;
                loaded = true;
            }
            catch (Exception exception)
            {
                if (!HasSettingsAccess()) return;
                loaded = false;
                UiTheme.ShowOperationIssue(this, Present("تحميل إعدادات الضريبة"), exception);
            }
            finally
            {
                if (!IsDisposed)
                {
                    SetBusy(false, null);
                    if (!loaded) lblStatus.Text = Present("تعذر التحميل. أغلق النافذة وأعد المحاولة بعد التحقق من الاتصال.");
                }
            }
        }

        private void SetBusy(bool value, string status)
        {
            busy = value;
            UseWaitCursor = value;
            RefreshPolicyState();
            if (status != null) lblStatus.Text = status;
        }

        private void RefreshPolicyState()
        {
            bool yemen = SelectedCountry == "YE";
            cmbCountry.Enabled = loaded && !busy;
            chkEnabled.Enabled = loaded && !busy && !yemen;
            Label enabledCaption = Controls.Find("taxEnabledCaption", true).OfType<Label>().FirstOrDefault();
            if (enabledCaption != null)
            {
                enabledCaption.ForeColor = chkEnabled.Enabled ? UiTheme.Text : UiTheme.MutedText;
                enabledCaption.Cursor = chkEnabled.Enabled ? Cursors.Hand : Cursors.Default;
            }
            numRate.Enabled = loaded && !busy && !yemen && chkEnabled.Checked;
            btnSave.Enabled = loaded && !busy && !previewMode;
            if (busy) return;
            if (saveCompleted)
            {
                lblStatus.Text = Present("تم حفظ إعدادات الضريبة. أغلق النافذة وأعد فتحها للمراجعة.");
                return;
            }
            lblStatus.Text = yemen
                ? Present("اليمن: الضريبة غير مطبقة؛ النسبة الفعلية 0%.")
                : Present("تطبّق الضريبة على الفواتير الجديدة فقط؛ المستندات السابقة تحتفظ بقيمها.");
        }

        private async void SaveSettings(object sender, EventArgs args)
        {
            if (previewMode || saveCompleted || !loaded || busy) return;
            if (!HasSettingsAccess()) return;
            decimal rate = numRate.Value;
            bool enabled = chkEnabled.Checked;
            string country = SelectedCountry;
            saving = true;
            SetBusy(true, Present("جار حفظ سياسة الضريبة..."));
            try
            {
                await savePolicy(rate, enabled, country);
                saveCompleted = true;
                SavedCountryCode = country;
                loaded = false;
                saving = false;
                if (!HasSettingsAccess()) return;
                DialogResult = DialogResult.OK;
                Close();
                if (closeWasCancelled || (!Modal && !IsDisposed && Visible))
                    throw new InvalidOperationException("Saved tax settings window did not close.");
            }
            catch (Exception exception)
            {
                saving = false;
                if (IsDisposed || Disposing) return;
                if (saveCompleted)
                {
                    DialogResult = DialogResult.None;
                    if (PermissionService.CanOpen(AppPermission.Settings))
                        UiTheme.ShowCompletedOperationFollowUpFailure(this, "حفظ إعدادات الضريبة", country, exception,
                            "لا تكرر العملية؛ أغلق النافذة ثم افتحها مجددًا لمراجعة النتيجة.");
                }
                else
                {
                    if (!HasSettingsAccess()) return;
                    UiTheme.ShowOperationIssue(this, Present("حفظ إعدادات الضريبة"), exception);
                    // Reload also resolves an interrupted save from its committed SQL receipt.
                    await LoadSettingsAsync();
                }
            }
            finally
            {
                saving = false;
                if (!IsDisposed) SetBusy(false, null);
            }
        }

    }
}

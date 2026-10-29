// شرح الملف: شاشة الإعدادات — بوابة عصرية لخيارات النظام.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace project_fo_3
{
    public partial class SettingsForm : Form
    {
        private Panel cardCurrencies;
        private Label lblCurrencyIcon;
        private Label lblCurrencyTitle;
        private Label lblCurrencySubtitle;
        private Label lblCurrencyChevron;
        private Panel cardSaudiCompliance;
        private Label lblSaudiIcon;
        private Label lblSaudiTitle;
        private Label lblSaudiSubtitle;
        private Label lblSaudiChevron;
        private Panel cardLanguage;
        private Label lblLanguageIcon;
        private Label lblLanguageTitle;
        private Label lblLanguageSubtitle;
        private Label lblLanguageChevron;
        private Panel cardDevices;
        private Label lblDevicesIcon;
        private Label lblDevicesTitle;
        private Label lblDevicesSubtitle;
        private Label lblDevicesChevron;
        private bool arrangingCards;
        private bool presentationLeftToRight;
        private bool previewOnlyMode;
        private bool openingSettingsDialog;
        private string lastSavedTaxCountry;
        private const string SavedTaxPolicyTemplate = "تم حفظ سياسة الضريبة. البلد: {0}؛ تطبق على الفواتير الجديدة.";
        internal bool PresentationLeftToRight => presentationLeftToRight;


        public SettingsForm() : this(true, false, null) { }

        internal SettingsForm(bool useAntd, bool previewOnly = false) : this(useAntd, previewOnly, null) { }

        private readonly Func<bool, TaxSettingsForm> createTaxSettings;

        internal SettingsForm(bool useAntd, bool previewOnly, bool? forceLeftToRight)
            : this(useAntd, previewOnly, forceLeftToRight, english => new TaxSettingsForm(false, english)) { }

        private readonly Func<string, bool, Form> createSettingsDialog;

        internal SettingsForm(bool useAntd, bool previewOnly, bool? forceLeftToRight,
            Func<bool, TaxSettingsForm> createTaxSettings)
            : this(useAntd, previewOnly, forceLeftToRight, createTaxSettings, CreateSettingsDialog) { }

        internal SettingsForm(bool useAntd, bool previewOnly, bool? forceLeftToRight,
            Func<bool, TaxSettingsForm> createTaxSettings, Func<string, bool, Form> createSettingsDialog)
        {
            this.createSettingsDialog = createSettingsDialog ?? throw new ArgumentNullException(nameof(createSettingsDialog));
            this.createTaxSettings = createTaxSettings ?? throw new ArgumentNullException(nameof(createTaxSettings));
            presentationLeftToRight = forceLeftToRight ?? UiDirection.IsLeftToRight;
            previewOnlyMode = previewOnly;
            InitializeComponent();
            UiTheme.ApplyDialogShell(this, panelBody);
            UiTheme.Apply(this);
            ApplyModernLayout();
            panelBody.Resize += (sender, args) => LayoutSettingsCards();
            if (useAntd) ApplyAntdSettings(previewOnly);
            panelHeader.Resize += (sender, args) => LayoutPresentationHeader();
            ApplyPresentation(presentationLeftToRight);
        }

        private string Present(string arabicText)
        {
            return presentationLeftToRight ? UiLanguageCatalog.Translate(arabicText) : arabicText;
        }

        private void LayoutPresentationHeader()
        {
            if (panelHeader == null || lblTitle == null || lblSubtitle == null) return;
            int horizontalPadding = Math.Max(20, Font.Height * 2);
            int textWidth = Math.Max(1, panelHeader.ClientSize.Width - horizontalPadding * 2);
            lblTitle.AutoSize = false;
            lblSubtitle.AutoSize = false;
            lblTitle.Dock = DockStyle.None;
            lblSubtitle.Dock = DockStyle.None;
            lblTitle.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            lblSubtitle.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            lblTitle.SetBounds(horizontalPadding, 12, textWidth, 34);
            lblSubtitle.SetBounds(horizontalPadding, 48, textWidth, 26);
            lblTitle.TextAlign = presentationLeftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
            lblSubtitle.TextAlign = presentationLeftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
        }

        private void ApplyModernLayout()
        {
            ClientSize = new Size(760, 570);
            MinimumSize = new Size(360, 360);
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximumSize = Size.Empty;
            StartPosition = FormStartPosition.CenterParent;
            UiTheme.ApplyDialogHeader(panelHeader, lblTitle, lblSubtitle, lblClose);
            lblClose.Visible = false;
            panelBody.BackColor = UiTheme.Background;
            lblHint.ForeColor = UiTheme.MutedText;
            lblHint.BackColor = UiTheme.SurfaceVariant;
            lblHint.Padding = new Padding(8, 4, 8, 4);
            lblHint.Paint += (sender, args) =>
            {
                using (var border = new Pen(UiTheme.Border))
                    args.Graphics.DrawLine(border, 0, 0, lblHint.Width, 0);
            };

            UiTheme.ApplyMenuCard(
                cardBasicData,
                lblCardIcon,
                lblCardTitle,
                lblCardSubtitle,
                lblCardChevron,
                OpenBasicDataEmbedded);

            EnsureCurrencyCard();
            UiTheme.ApplyMenuCard(
                cardCurrencies,
                lblCurrencyIcon,
                lblCurrencyTitle,
                lblCurrencySubtitle,
                lblCurrencyChevron,
                OpenCurrencyEmbedded);

            EnsureSaudiComplianceCard();
            UiTheme.ApplyMenuCard(
                cardSaudiCompliance,
                lblSaudiIcon,
                lblSaudiTitle,
                lblSaudiSubtitle,
                lblSaudiChevron,
                OpenComplianceEmbedded);

            EnsureLanguageCard();
            UiTheme.ApplyMenuCard(
                cardLanguage,
                lblLanguageIcon,
                lblLanguageTitle,
                lblLanguageSubtitle,
                lblLanguageChevron,
                OpenLanguageEmbedded);

            EnsureDeviceCard();
            UiTheme.ApplyMenuCard(
                cardDevices,
                lblDevicesIcon,
                lblDevicesTitle,
                lblDevicesSubtitle,
                lblDevicesChevron,
                OpenDevicesEmbedded);


            InitializeTaxCard();
            panelBody.AutoScroll = true;
            panelBody.AutoScrollMargin = new Size(0, 12);
            panelBody.RightToLeft = RightToLeft.No;
            panelBody.Padding = Padding.Empty;
            lblHint.Parent = this;
            panelBody.Dock = DockStyle.None;
            panelBody.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            lblHint.Dock = DockStyle.None;
            lblHint.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            Resize += (sender, args) => LayoutSettingsShell();
            lblHint.TextChanged += (sender, args) =>
            {
                lblHint.AccessibleName = lblHint.Text;
                LayoutSettingsShell();
            };
            lblHint.Text = Present("إعدادات المعايير السعودية تعمل وفق الأوضاع المتاحة حتى اكتمال الاعتماد الرسمي");
            ApplySettingsCardFallbackIcons();
            LayoutSettingsShell();
            LayoutSettingsCards();
        }

        private void LayoutSettingsShell()
        {
            if (panelHeader == null || panelBody == null || lblHint == null) return;
            StatusStrip status = Controls["__UiStatusStrip"] as StatusStrip;
            // Visible is false before Show() even though the docked strip will appear.
            int statusHeight = status == null ? 0 : status.Height;
            int hintHeight = Math.Max(28, MeasureCardText(lblHint, Math.Max(1, ClientSize.Width - 24)) + lblHint.Padding.Vertical);
            int hintTop = Math.Max(panelHeader.Bottom, ClientSize.Height - statusHeight - hintHeight);
            lblHint.SetBounds(0, hintTop, ClientSize.Width, hintHeight);
            panelBody.SetBounds(0, panelHeader.Bottom, ClientSize.Width, Math.Max(1, hintTop - panelHeader.Bottom));
            panelBody.BringToFront();
            lblHint.BringToFront();
            panelHeader.BringToFront();
        }

        private void ApplySettingsCardFallbackIcons()
        {
            ApplyOutlinedIcon(lblCardIcon, AppIcon.Database);
            ApplyOutlinedIcon(lblCurrencyIcon, AppIcon.Currency);
            ApplyOutlinedIcon(lblSaudiIcon, AppIcon.Report);
            ApplyOutlinedIcon(lblLanguageIcon, AppIcon.Language);
            ApplyOutlinedIcon(lblDevicesIcon, AppIcon.Print);
            ApplyChevron(lblCardChevron);
            ApplyChevron(lblCurrencyChevron);
            ApplyChevron(lblSaudiChevron);
            ApplyChevron(lblLanguageChevron);
            ApplyChevron(lblDevicesChevron);
            ApplyOutlinedIcon((Label)cardTax.Controls["taxIcon"], AppIcon.Currency);
            ApplyChevron((Label)cardTax.Controls["taxChevron"]);
        }

        private static void ApplyOutlinedIcon(Label label, AppIcon icon)
        {
            if (label == null)
            {
                return;
            }

            Image previous = label.BackgroundImage;
            label.Text = string.Empty;
            label.BackgroundImage = OutlinedIcons.RenderBitmap(icon, 32, UiTheme.PrimaryDark, true);
            label.BackgroundImageLayout = ImageLayout.Center;
            label.TextAlign = ContentAlignment.MiddleCenter;
            if (previous != null && !ReferenceEquals(previous, label.BackgroundImage)) previous.Dispose();
        }

        private static void ApplyChevron(Label label)
        {
            if (label == null)
            {
                return;
            }

            Image previous = label.BackgroundImage;
            label.Text = string.Empty;
            label.BackgroundImage = OutlinedIcons.RenderBitmap(AppIcon.Back, 20, UiTheme.MutedText, true);
            label.BackgroundImageLayout = ImageLayout.Center;
            label.TextAlign = ContentAlignment.MiddleCenter;
            if (previous != null && !ReferenceEquals(previous, label.BackgroundImage)) previous.Dispose();
        }

        private void EnsureCurrencyCard()
        {
            if (cardCurrencies != null)
            {
                return;
            }

            cardCurrencies = new Panel
            {
                Name = "cardCurrencies",
                Size = cardBasicData.Size,
                Location = new Point(cardBasicData.Left - cardBasicData.Width - 24, cardBasicData.Top),
                TabIndex = 1
            };

            lblCurrencyChevron = new Label
            {
                Dock = DockStyle.Left,
                Size = new Size(47, cardCurrencies.Height),
                Text = ">",
                TextAlign = ContentAlignment.MiddleCenter
            };

            lblCurrencySubtitle = new Label
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(50, 70, 40, 40),
                Text = "الدول والعملات وعملة النظام",
                TextAlign = ContentAlignment.TopRight
            };

            lblCurrencyTitle = new Label
            {
                Dock = DockStyle.Top,
                Height = 28,
                Padding = new Padding(40, 5, 0, 0),
                Text = "العملات",
                TextAlign = ContentAlignment.MiddleRight
            };

            lblCurrencyIcon = new Label
            {
                Dock = DockStyle.Right,
                Width = 88,
                Text = "ر.س",
                TextAlign = ContentAlignment.MiddleCenter
            };

            cardCurrencies.Controls.Add(lblCurrencyChevron);
            cardCurrencies.Controls.Add(lblCurrencySubtitle);
            cardCurrencies.Controls.Add(lblCurrencyTitle);
            cardCurrencies.Controls.Add(lblCurrencyIcon);
            panelBody.Controls.Add(cardCurrencies);
            cardCurrencies.BringToFront();
        }

        private void EnsureSaudiComplianceCard()
        {
            if (cardSaudiCompliance != null)
            {
                return;
            }

            cardSaudiCompliance = new Panel
            {
                Name = "cardSaudiCompliance",
                Size = cardBasicData.Size,
                TabIndex = 2
            };

            lblSaudiChevron = new Label { Dock = DockStyle.Left, Size = new Size(47, cardSaudiCompliance.Height), TextAlign = ContentAlignment.MiddleCenter };
            lblSaudiSubtitle = new Label { Dock = DockStyle.Fill, Padding = new Padding(50, 70, 40, 40), Text = "إضافة أو تعطيل المزايا المدققة", TextAlign = ContentAlignment.TopRight };
            lblSaudiTitle = new Label { Dock = DockStyle.Top, Height = 28, Padding = new Padding(40, 5, 0, 0), Text = "المعايير السعودية", TextAlign = ContentAlignment.MiddleRight };
            lblSaudiIcon = new Label { Dock = DockStyle.Right, Width = 88, Text = "SA", TextAlign = ContentAlignment.MiddleCenter };

            cardSaudiCompliance.Controls.Add(lblSaudiChevron);
            cardSaudiCompliance.Controls.Add(lblSaudiSubtitle);
            cardSaudiCompliance.Controls.Add(lblSaudiTitle);
            cardSaudiCompliance.Controls.Add(lblSaudiIcon);
            panelBody.Controls.Add(cardSaudiCompliance);
            cardSaudiCompliance.BringToFront();
        }

        private void EnsureLanguageCard()
        {
            if (cardLanguage != null)
            {
                return;
            }

            cardLanguage = new Panel
            {
                Name = "cardLanguage",
                Size = cardBasicData.Size,
                TabIndex = 3
            };

            lblLanguageChevron = new Label { Dock = DockStyle.Left, Size = new Size(47, cardLanguage.Height), TextAlign = ContentAlignment.MiddleCenter };
            lblLanguageSubtitle = new Label { Dock = DockStyle.Fill, Padding = new Padding(50, 70, 40, 40), TextAlign = ContentAlignment.TopRight };
            lblLanguageTitle = new Label { Dock = DockStyle.Top, Height = 28, Padding = new Padding(40, 5, 0, 0), TextAlign = ContentAlignment.MiddleRight };
            lblLanguageIcon = new Label { Dock = DockStyle.Right, Width = 88, TextAlign = ContentAlignment.MiddleCenter };

            cardLanguage.Controls.Add(lblLanguageChevron);
            cardLanguage.Controls.Add(lblLanguageSubtitle);
            cardLanguage.Controls.Add(lblLanguageTitle);
            cardLanguage.Controls.Add(lblLanguageIcon);
            panelBody.Controls.Add(cardLanguage);
            cardLanguage.BringToFront();

            lblLanguageTitle.Text = Present("مركز اللغة");
            lblLanguageSubtitle.Text = Present("العربية الافتراضية وحالة توفر اللغات");
        }

        private void EnsureDeviceCard()
        {
            if (cardDevices != null)
            {
                return;
            }

            cardDevices = new Panel { Name = "cardDevices", Size = cardBasicData.Size, TabIndex = 4 };
            lblDevicesChevron = new Label { Dock = DockStyle.Left, Size = new Size(47, cardDevices.Height), TextAlign = ContentAlignment.MiddleCenter };
            lblDevicesSubtitle = new Label { Dock = DockStyle.Fill, Padding = new Padding(50, 70, 40, 40), TextAlign = ContentAlignment.TopRight };
            lblDevicesTitle = new Label { Dock = DockStyle.Top, Height = 28, Padding = new Padding(40, 5, 0, 0), TextAlign = ContentAlignment.MiddleRight };
            lblDevicesIcon = new Label { Dock = DockStyle.Right, Width = 88, TextAlign = ContentAlignment.MiddleCenter };

            cardDevices.Controls.Add(lblDevicesChevron);
            cardDevices.Controls.Add(lblDevicesSubtitle);
            cardDevices.Controls.Add(lblDevicesTitle);
            cardDevices.Controls.Add(lblDevicesIcon);
            panelBody.Controls.Add(cardDevices);
            cardDevices.BringToFront();

            lblDevicesTitle.Text = Present("مركز الأجهزة");
            lblDevicesSubtitle.Text = Present("الطابعة ومقاس الإيصال وقارئ الباركود");
        }

        private void LayoutSettingsCards()
        {
            if (antdCards != null) { LayoutAntdCards(); return; }
            if (cardTax == null || arrangingCards) return;
            arrangingCards = true;
            int scrollY = Math.Max(0, -panelBody.AutoScrollPosition.Y);
            panelBody.SuspendLayout();
            panelBody.AutoScroll = false;
            try
            {
            int gap = Math.Max(12, Font.Height);
            int availableWidth = Math.Max(1, panelBody.ClientSize.Width - gap * 2 - SystemInformation.VerticalScrollBarWidth);
            int columns = availableWidth >= 640 ? 2 : 1;
            int cardWidth = Math.Max(1, (availableWidth - (columns - 1) * gap) / columns);
            Panel[] cards = { cardBasicData, cardTax, cardCurrencies, cardLanguage, cardDevices, cardSaudiCompliance };
            Label[] titles = { lblCardTitle, (Label)cardTax.Controls["taxTitle"], lblCurrencyTitle, lblLanguageTitle, lblDevicesTitle, lblSaudiTitle };
            Label[] subtitles = { lblCardSubtitle, (Label)cardTax.Controls["taxSubtitle"], lblCurrencySubtitle, lblLanguageSubtitle, lblDevicesSubtitle, lblSaudiSubtitle };
            Label[] icons = { lblCardIcon, (Label)cardTax.Controls["taxIcon"], lblCurrencyIcon, lblLanguageIcon, lblDevicesIcon, lblSaudiIcon };
            Label[] arrows = { lblCardChevron, (Label)cardTax.Controls["taxChevron"], lblCurrencyChevron, lblLanguageChevron, lblDevicesChevron, lblSaudiChevron };
            int cardHeight = 132;
            int textWidth = Math.Max(1, cardWidth - gap * 3 - 24);
            for (int i = 0; i < cards.Length; i++)
            {
                int titleHeight = MeasureCardText(titles[i], textWidth);
                int subtitleHeight = Math.Max(subtitles[i].Font.Height * 2, MeasureCardText(subtitles[i], cardWidth - gap * 2));
                cardHeight = Math.Max(cardHeight, gap * 3 + titleHeight + subtitleHeight);
            }
            for (int i = 0; i < cards.Length; i++)
            {
                int column = i % columns;
                cards[i].Padding = Padding.Empty;
                cards[i].RightToLeft = RightToLeft.Yes;
                cards[i].Bounds = new Rectangle(gap + (columns - 1 - column) * (cardWidth + gap),
                    gap + (i / columns) * (cardHeight + gap), cardWidth, cardHeight);
                cards[i].TabIndex = i;
                // One measured text column; decorative icons must not consume the description area.
                foreach (Label label in new[] { titles[i], subtitles[i], icons[i], arrows[i] })
                {
                    label.Dock = DockStyle.None;
                    label.AutoSize = false;
                    label.Padding = Padding.Empty;
                    label.AutoEllipsis = true;
                }
                icons[i].Visible = false;
                int titleHeight = MeasureCardText(titles[i], textWidth);
                titles[i].Bounds = new Rectangle(gap + 24, gap, textWidth, titleHeight);
                titles[i].TextAlign = ContentAlignment.TopRight;
                arrows[i].Bounds = new Rectangle(gap, gap, 24, titleHeight);
                subtitles[i].Bounds = new Rectangle(gap, gap * 2 + titleHeight,
                    Math.Max(1, cardWidth - gap * 2), cardHeight - gap * 3 - titleHeight);
                subtitles[i].TextAlign = ContentAlignment.TopRight;
            }
            panelBody.AutoScrollMinSize = new Size(0, gap + ((cards.Length + columns - 1) / columns) * (cardHeight + gap));
            lblHint.Height = Math.Max(28, MeasureCardText(lblHint, Math.Max(1, ClientSize.Width - 24)) + 12);
            LayoutSettingsShell();
            }
            finally
            {
                panelBody.AutoScroll = true;
                panelBody.AutoScrollPosition = new Point(0, scrollY);
                panelBody.ResumeLayout(true);
                arrangingCards = false;
            }
        }

        private static int MeasureCardText(Label label, int width)
        {
            return TextRenderer.MeasureText(label.Text, label.Font, new Size(Math.Max(1, width), int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.RightToLeft).Height;
        }

        private void OpenTaxSettings(object sender, EventArgs e)
        {
            if (previewOnlyMode || openingSettingsDialog || IsDisposed || Disposing) return;
            if (!PermissionService.CanOpen(AppPermission.Settings)) { Close(); return; }
            openingSettingsDialog = true;
            bool completed = false;
            string country = null;
            try
            {
                using (var form = createTaxSettings(presentationLeftToRight))
                {
                    DialogResult result = form.ShowDialog(this);
                    if (IsDisposed || Disposing) return;
                    if (!PermissionService.CanOpen(AppPermission.Settings)) { Close(); return; }
                    if (result != DialogResult.OK) return;
                    country = form.SavedCountryCode;
                    if (string.IsNullOrWhiteSpace(country))
                        throw new InvalidOperationException("Confirmed tax country is missing from the dialog result.");
                    completed = true;
                    lastSavedTaxCountry = country;
                    lblHint.Text = UiLanguageCatalog.Format(presentationLeftToRight, SavedTaxPolicyTemplate, country);
                    lblHint.AccessibleName = lblHint.Text;
                }
            }
            catch (Exception error)
            {
                if (IsDisposed || Disposing || !PermissionService.CanOpen(AppPermission.Settings)) return;
                if (completed)
                    UiTheme.ShowCompletedOperationFollowUpFailure(this, "حفظ إعدادات الضريبة", country, error,
                        "لا تكرر العملية؛ أغلق النافذة ثم افتحها مجددًا لمراجعة النتيجة.");
                else UiTheme.ShowOperationIssue(this, Present("فتح إعدادات الضريبة"), error);
            }
            finally { openingSettingsDialog = false; }
        }

        private static Form CreateSettingsDialog(string route, bool leftToRight)
        {
            switch (route)
            {
                case "basic": return new PharmacyCompanyForm(false, leftToRight);
                case "currency": return new CurrencySettingsForm(false, leftToRight);
                case "compliance": return new SaudiCommercialSettingsForm(false, leftToRight);
                case "accessibility": return new AccessibilitySettingsForm(false, leftToRight);
                case "language": return new LanguageSettingsForm(false, leftToRight);
                case "devices": return new DeviceSettingsForm(false, leftToRight);
                case "sales-shortcuts": return new SalesShortcutSettingsForm(new SalesShortcutProfileStore(SalesShortcutProfileStore.DefaultDirectory), false, leftToRight);
                case "excel": return new ExcelDataTransferForm();
                case "excel-import": return new MedicineCatalogImportForm();
                case "invoice-drafts": return new InvoiceImportDraftForm();
                case "cashboxes": return new CashBoxManagementForm();
                default: throw new ArgumentOutOfRangeException(nameof(route));
            }
        }

        private void OpenSettingsDialog(string route)
        {
            if (previewOnlyMode || openingSettingsDialog || IsDisposed || Disposing) return;
            if (!PermissionService.CanOpen(AppPermission.Settings)) { Close(); return; }
            openingSettingsDialog = true;
            try
            {
                using (var form = createSettingsDialog(route, presentationLeftToRight)) form.ShowDialog(this);
                if (!IsDisposed && !Disposing && !PermissionService.CanOpen(AppPermission.Settings)) Close();
            }
            catch (Exception error)
            {
                if (IsDisposed || Disposing) return;
                if (!PermissionService.CanOpen(AppPermission.Settings)) { Close(); return; }
                UiTheme.ShowOperationIssue(this, Present("فتح إعدادات النظام"), error);
            }
            finally { openingSettingsDialog = false; }
        }

        private void OpenBasicData(object sender, EventArgs e) => OpenSettingsDialog("basic");
        private void OpenCurrencySettings(object sender, EventArgs e) => OpenSettingsDialog("currency");
        private void OpenSaudiComplianceSettings(object sender, EventArgs e) => OpenSettingsDialog("compliance");
        private void OpenAccessibilitySettings(object sender, EventArgs e) => OpenSettingsDialog("accessibility");
        private void OpenLanguageSettings(object sender, EventArgs e) => OpenSettingsDialog("language");
        private void OpenDeviceSettings(object sender, EventArgs e) => OpenSettingsDialog("devices");
        private void OpenSalesShortcuts(object sender, EventArgs e) => OpenSettingsDialog("sales-shortcuts");
        private void OpenExcelDataTransfer(object sender, EventArgs e) => OpenSettingsDialog("excel");
        private void OpenMedicineCatalogImport(object sender, EventArgs e) => OpenSettingsDialog("excel-import");
        private void OpenInvoiceImportDrafts(object sender, EventArgs e) => OpenSettingsDialog("invoice-drafts");
        private void OpenCashBoxes(object sender, EventArgs e) => OpenSettingsDialog("cashboxes");

        // The most-used settings open inside this window (embedded) instead of a modal popup,
        // so working through the sections feels like tab pages. The navigation methods stay
        // modal for the owned navigation verification contract.
        private Form embeddedSection;
        private Panel embeddedContainer;
        private bool embeddedClosing;

        private void OpenBasicDataEmbedded(object sender, EventArgs e) => OpenEmbeddedSection("basic");
        private void OpenCurrencyEmbedded(object sender, EventArgs e) => OpenEmbeddedSection("currency");
        private void OpenComplianceEmbedded(object sender, EventArgs e) => OpenEmbeddedSection("compliance");
        private void OpenAccessibilityEmbedded(object sender, EventArgs e) => OpenEmbeddedSection("accessibility");
        private void OpenLanguageEmbedded(object sender, EventArgs e) => OpenEmbeddedSection("language");
        private void OpenDevicesEmbedded(object sender, EventArgs e) => OpenEmbeddedSection("devices");
        private void OpenSalesShortcutsEmbedded(object sender, EventArgs e) => OpenEmbeddedSection("sales-shortcuts");
        private void OpenExcelEmbedded(object sender, EventArgs e) => OpenEmbeddedSection("excel");
        private void OpenExcelImportEmbedded(object sender, EventArgs e) => OpenEmbeddedSection("excel-import");
        private void OpenInvoiceDraftsEmbedded(object sender, EventArgs e) => OpenEmbeddedSection("invoice-drafts");
        private void OpenCashBoxesEmbedded(object sender, EventArgs e) => OpenEmbeddedSection("cashboxes");

        private void OpenEmbeddedSection(string route)
        {
            if (previewOnlyMode || embeddedSection != null || openingSettingsDialog || IsDisposed || Disposing) return;
            if (!PermissionService.CanOpen(AppPermission.Settings)) { Close(); return; }
            try
            {
                Form form = createSettingsDialog(route, presentationLeftToRight);
                form.TopLevel = false;
                form.FormBorderStyle = FormBorderStyle.None;
                form.Dock = DockStyle.Fill;

                Panel container = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.Background };
                Panel bar = new Panel { Dock = DockStyle.Top, Height = 46, BackColor = UiTheme.Surface };
                Button resume = new Button
                {
                    Text = Present("الرجوع إلى الأقسام"),
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter,
                    AccessibleName = Present("الرجوع إلى الأقسام")
                };
                resume.Click += (sender, args) => CloseEmbeddedSection();
                UiTheme.ApplyToolbarButtons(resume);
                bar.Controls.Add(resume);
                container.Controls.Add(form);
                container.Controls.Add(bar);

                embeddedSection = form;
                embeddedContainer = container;
                embeddedClosing = false;
                var hiddenCards = new List<Control>();
                foreach (Control child in panelBody.Controls)
                {
                    if (child != null && child != container && child.Visible) hiddenCards.Add(child);
                }
                embeddedSection.Tag = hiddenCards;
                panelBody.AutoScroll = false;
                foreach (Control card in hiddenCards) card.Visible = false;
                panelBody.Controls.Add(container);
                container.BringToFront();
                form.Disposed += EmbeddedSection_Disposed;
                form.Show();
                embeddedSection.BringToFront();
                resume.BringToFront();
            }
            catch (Exception error)
            {
                if (IsDisposed || Disposing) return;
                if (!PermissionService.CanOpen(AppPermission.Settings)) { Close(); return; }
                UiTheme.ShowOperationIssue(this, Present("فتح إعدادات النظام"), error);
            }
        }

        private void EmbeddedSection_Disposed(object sender, EventArgs e)
        {
            if (embeddedClosing) return;
            if (sender is Form form && !ReferenceEquals(form, embeddedSection)) return;
            CloseEmbeddedSection();
        }

        private void CloseEmbeddedSection()
        {
            if (embeddedClosing) return;
            embeddedClosing = true;
            Form section = embeddedSection;
            Panel container = embeddedContainer;
            embeddedSection = null;
            embeddedContainer = null;
            if (container != null)
            {
                var hiddenCards = section?.Tag as List<Control>;
                panelBody.Controls.Remove(container);
                foreach (Control card in hiddenCards ?? new List<Control>())
                {
                    if (card != null && !card.IsDisposed) card.Visible = true;
                }
            }
            panelBody.AutoScroll = true;
            LayoutSettingsShell();
            if (!IsDisposed && !Disposing && !PermissionService.CanOpen(AppPermission.Settings)) Close();
        }

        private void lblClose_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}

// مركز الأجهزة: يضبط تفضيل الطابعة ومقاس الإيصال بلا طباعة تجريبية أو تغيير بيانات.
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace project_fo_3
{
    internal sealed class DeviceSettingsForm : Form
    {
        private readonly ComboBox printer = new ComboBox { Name = "preferredPrinter" };
        private readonly ComboBox receiptPaper = new ComboBox { Name = "receiptPaperFormat" };
        private readonly ComboBox invoicePaper = new ComboBox { Name = "invoicePrintFormat" };
        private readonly Label printerStatus = new Label { Name = "printerStatus" };
        private readonly AntdUI.Button saveButton = new AccessibleAntdButton { Name = "saveDeviceSettings" };
        private readonly AntdUI.Button checkPrinterButton = new AccessibleAntdButton { Name = "checkPreferredPrinter" };
        private readonly AntdUI.Button closeButton = new AccessibleAntdButton { Name = "closeDeviceSettings" };
        private readonly bool previewOnly;
        private readonly bool leftToRight;
        internal bool PresentationLeftToRight => leftToRight;
        private Label titleLabel;
        private Label subtitleLabel;
        private Label printerLabel;
        private Label receiptPaperLabel;
        private Label invoicePaperLabel;
        private Label barcodeInfo;
        private bool arrangingContent;

        internal DeviceSettingsForm() : this(false, UiLanguageCatalog.IsEnglishActive) { }

        internal DeviceSettingsForm(bool previewOnly) : this(previewOnly, false) { }

        internal DeviceSettingsForm(bool previewOnly, bool leftToRight)
        {
            this.previewOnly = previewOnly;
            this.leftToRight = leftToRight;
            Text = Present("مركز الأجهزة");
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96F, 96F);
            ClientSize = new Size(610, 470);
            MinimumSize = new Size(480, 440);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = true;
            RightToLeft = leftToRight ? RightToLeft.No : RightToLeft.Yes;
            RightToLeftLayout = !leftToRight;
            BuildLayout();
            UiTheme.Apply(this);
            UiDirection.Apply(this, leftToRight);
            ApplyDisplayLanguage();
            ApplyNeutralSurface();
            if (previewOnly)
            {
                Control runtimeStatus = Controls["__UiStatusStrip"];
                if (runtimeStatus != null) runtimeStatus.Dispose();
            }
            foreach (ComboBox selector in new[] { printer, receiptPaper, invoicePaper })
                DesktopComponents.AddComboBoxDropdown(selector, selector.Name + "Dropdown", selector.AccessibleName + " - " + Present("فتح القائمة"));
            Panel actions = Controls.Find("deviceSettingsActions", true).OfType<Panel>().FirstOrDefault();
            if (actions != null) actions.BackColor = UiTheme.Background;
            foreach (AntdUI.Button action in new[] { saveButton, checkPrinterButton, closeButton })
                DesktopComponents.StyleOutlinedFooterAction(action);
            ConfigureActionDirection();
            LoadPreferences();
            ApplyPreviewMode();
            AcceptButton = saveButton;
            CancelButton = closeButton;
            closeButton.DialogResult = DialogResult.Cancel;
            Resize += (sender, args) => LayoutContent();
            LayoutContent();
        }

        private void BuildLayout()
        {
            Panel header = new Panel { Name = "deviceSettingsHeader", Tag = "UiSurface", Dock = DockStyle.Top, Height = 92, Padding = new Padding(22, 12, 22, 10) };
            titleLabel = new Label { Name = "deviceSettingsTitle", Dock = DockStyle.Top, Height = 38, Text = Present("مركز الأجهزة"), TextAlign = ReadingAlignment(), Font = AppFonts.Create(16F, FontStyle.Bold) };
            subtitleLabel = new Label { Name = "deviceSettingsSubtitle", Dock = DockStyle.Fill, Text = Present("اضبط الطابعة ومقاس الفاتورة لهذه المحطة."), TextAlign = ReadingAlignment() };
            header.Controls.Add(subtitleLabel);
            header.Controls.Add(titleLabel);

            AntdUI.Panel body = DesktopComponents.Card("deviceSettingsBody");
            body.Dock = DockStyle.Fill;
            body.Padding = new Padding(26, 16, 26, 12);
            body.AutoScroll = true;
            body.BackColor = UiTheme.Surface;
            TableLayoutPanel fields = new TableLayoutPanel
            {
                Name = "deviceSettingsFields",
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoScroll = false,
                ColumnCount = 1,
                RowCount = 8
            };
            for (int row = 0; row < 8; row++) fields.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            printerLabel = CreateLabel("devicePrinterLabel", Present("الطابعة المفضلة"));
            printer.DropDownStyle = ComboBoxStyle.DropDownList;
            printer.AccessibleName = Present("اختيار الطابعة المفضلة");
            printerStatus.Height = 48;
            printerStatus.Dock = DockStyle.Top;
            printerStatus.TextAlign = ReadingAlignment();
            printerStatus.AccessibleName = Present("حالة الطابعة");

            receiptPaperLabel = CreateLabel("deviceReceiptPaperLabel", Present("مقاس إيصال الكاشير"));
            receiptPaper.DropDownStyle = ComboBoxStyle.DropDownList;
            receiptPaper.AccessibleName = Present("اختيار مقاس إيصال الكاشير");

            invoicePaperLabel = CreateLabel("deviceInvoicePaperLabel", Present("المقاس الافتراضي لفاتورة البيع والشراء"));
            invoicePaper.DropDownStyle = ComboBoxStyle.DropDownList;
            invoicePaper.AccessibleName = Present("اختيار مقاس فاتورة البيع والشراء");

            barcodeInfo = new Label
            {
                Name = "deviceBarcodeInfo",
                Dock = DockStyle.Top,
                AutoSize = false,
                Height = 64,
                Padding = new Padding(8, 8, 8, 6),
                Text = Present("قارئ الباركود USB/HID: امسح الرمز ثم اضغط Enter."),
                TextAlign = ReadingAlignment(),
                AccessibleName = Present("معلومة قارئ الباركود")
            };

            Panel actions = new Panel { Name = "deviceSettingsActions", Dock = DockStyle.Bottom, Height = 66, BackColor = UiTheme.Background };
            saveButton.Size = new Size(132, 44);
            saveButton.Text = Present("حفظ الإعدادات");
            saveButton.AccessibleName = Present("حفظ إعدادات الأجهزة");
            saveButton.IconSvg = OutlinedIcons.Svg(AppIcon.Save, true);
            saveButton.IconSize = new Size(18, 18);
            saveButton.Click += SaveButton_Click;
            checkPrinterButton.Size = new Size(150, 44);
            checkPrinterButton.Text = Present("فحص الطابعة");
            checkPrinterButton.AccessibleName = Present("فحص توفر الطابعة المختارة");
            checkPrinterButton.AccessibleDescription = Present("يتحقق من توفر الطابعة محلياً دون إرسال مهمة طباعة.");
            checkPrinterButton.IconSvg = OutlinedIcons.Svg(AppIcon.Print, true);
            checkPrinterButton.IconSize = new Size(18, 18);
            checkPrinterButton.Click += CheckPrinterButton_Click;
            closeButton.Size = new Size(112, 44);
            closeButton.Text = Present("إغلاق");
            closeButton.AccessibleName = Present("إغلاق مركز الأجهزة");
            closeButton.IconSvg = OutlinedIcons.Svg(AppIcon.Close, true);
            closeButton.IconSize = new Size(18, 18);
            closeButton.Click += (sender, args) => { DialogResult = DialogResult.Cancel; Close(); };
            actions.Controls.Add(saveButton);
            actions.Controls.Add(checkPrinterButton);
            actions.Controls.Add(closeButton);

            fields.Controls.Add(printerLabel, 0, 0);
            fields.Controls.Add(CreateSelectorHost(printer), 0, 1);
            fields.Controls.Add(printerStatus, 0, 2);
            fields.Controls.Add(receiptPaperLabel, 0, 3);
            fields.Controls.Add(CreateSelectorHost(receiptPaper), 0, 4);
            fields.Controls.Add(invoicePaperLabel, 0, 5);
            fields.Controls.Add(CreateSelectorHost(invoicePaper), 0, 6);
            fields.Controls.Add(barcodeInfo, 0, 7);
            body.Controls.Add(fields);
            Controls.Add(body);
            Controls.Add(actions);
            Controls.Add(header);

            UiTheme.ApplyDialogHeader(header, titleLabel, subtitleLabel, null);
            AntdThemeAdapter.Apply(body);
            foreach (AntdUI.Button action in new[] { saveButton, checkPrinterButton, closeButton })
                DesktopComponents.StyleOutlinedFooterAction(action);
            actions.Resize += (sender, args) => LayoutActions(actions);
            KeyPreview = true;
            LayoutActions(actions);
        }

        private void LayoutActions(Panel actions)
        {
            actions.RightToLeft = RightToLeft.No;
            const int margin = 22;
            const int gap = 8;
            AntdUI.Button[] buttons = actions.Controls.OfType<AntdUI.Button>().ToArray();
            if (buttons.Length != 3) return;
            int x = leftToRight ? margin : actions.ClientSize.Width - margin;
            foreach (AntdUI.Button button in buttons)
            {
                button.Location = new Point(leftToRight ? x : x - button.Width, 11);
                x += leftToRight ? button.Width + gap : -button.Width - gap;
            }
        }

        private Label CreateLabel(string name, string text)
        {
            return new Label { Name = name, AutoSize = true, Height = 28, Text = text, TextAlign = ReadingAlignment(), Font = AppFonts.Create(9.5F, FontStyle.Bold) };
        }

        private static Panel CreateSelectorHost(ComboBox selector)
        {
            var host = new Panel { Name = selector.Name + "Host", Height = 48, Dock = DockStyle.Top, Margin = new Padding(0) };
            host.Controls.Add(selector);
            Action layout = () => selector.SetBounds(0, 2, host.ClientSize.Width, 44);
            host.Resize += (sender, args) => layout();
            layout();
            return host;
        }

        private void LoadPreferences()
        {
            printer.Items.Clear();
            printer.Items.Add(new DeviceChoice(Present("استخدام طابعة Windows الافتراضية"), string.Empty));
            foreach (string installedPrinter in DeviceSettings.GetInstalledPrinters())
            {
                printer.Items.Add(new DeviceChoice(installedPrinter, installedPrinter));
            }

            string preferredPrinter = DeviceSettings.LoadPreferredPrinter();
            int selectedPrinterIndex = printer.Items.Cast<DeviceChoice>().ToList().FindIndex(choice => string.Equals(choice.Value, preferredPrinter, StringComparison.OrdinalIgnoreCase));
            printer.SelectedIndex = selectedPrinterIndex >= 0 ? selectedPrinterIndex : 0;
            if (selectedPrinterIndex < 0 && !string.IsNullOrEmpty(preferredPrinter))
            {
                SetPrinterStatus(Present("الطابعة المحفوظة غير متصلة الآن؛ سيستخدم النظام طابعة Windows الافتراضية."), false);
            }
            else
            {
                SetPrinterStatus(string.IsNullOrEmpty(preferredPrinter)
                    ? Present("سيُستخدم اختيار Windows الافتراضي.")
                    : Present("الطابعة المحفوظة متاحة لهذه المحطة."), true);
            }

            receiptPaper.Items.Clear();
            receiptPaper.Items.Add(new DeviceChoice(Present("80mm — المقاس الافتراضي"), DeviceSettings.Paper80Mm));
            receiptPaper.Items.Add(new DeviceChoice(Present("58mm — للطابعات الصغيرة"), DeviceSettings.Paper58Mm));
            invoicePaper.Items.Clear();
            invoicePaper.Items.Add(new DeviceChoice(Present("A4 — فاتورة تفصيلية"), DeviceSettings.InvoiceA4));
            invoicePaper.Items.Add(new DeviceChoice(Present("إيصال حراري — حسب مقاس الإيصال"), DeviceSettings.InvoiceReceipt));
            receiptPaper.SelectedIndex = string.Equals(DeviceSettings.LoadReceiptPaperFormat(), DeviceSettings.Paper58Mm, StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            invoicePaper.SelectedIndex = DeviceSettings.IsReceiptInvoiceFormat() ? 1 : 0;
        }

        private void SaveButton_Click(object sender, EventArgs e)
        {
            if (previewOnly) return;
            DeviceChoice printerChoice = printer.SelectedItem as DeviceChoice;
            DeviceChoice paperChoice = receiptPaper.SelectedItem as DeviceChoice;
            DeviceChoice invoiceChoice = invoicePaper.SelectedItem as DeviceChoice;
            if (printerChoice == null || paperChoice == null || invoiceChoice == null || !DeviceSettings.Save(printerChoice.Value, paperChoice.Value, invoiceChoice.Value))
            {
                AppNotifications.Show(this, Present("تعذر حفظ إعدادات الأجهزة. تحقق من توفر الطابعة وإمكانية حفظ إعدادات المستخدم، ثم أعد المحاولة."), Present("مركز الأجهزة"), MessageBoxButtons.OK, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1, leftToRight);
                return;
            }

            AppNotifications.Show(this, Present("تم حفظ إعدادات هذه المحطة. ستستخدمها معاينات وطباعة الفواتير التالية."), Present("مركز الأجهزة"), MessageBoxButtons.OK, MessageBoxIcon.Information, MessageBoxDefaultButton.Button1, leftToRight);
            DialogResult = DialogResult.OK;
            Close();
        }

        private void CheckPrinterButton_Click(object sender, EventArgs e)
        {
            DeviceChoice printerChoice = printer.SelectedItem as DeviceChoice;
            if (printerChoice == null)
            {
                printerStatus.Text = Present("اختر طابعة أولاً ثم أعد الفحص.");
                UiTheme.SetStatus(printerStatus, printerStatus.Text, false);
                return;
            }

            if (string.IsNullOrEmpty(printerChoice.Value))
            {
                printerStatus.Text = Present("اختيار Windows الافتراضي لا يمكن التحقق منه مسبقاً؛ سيختاره Windows عند الطباعة.");
                UiTheme.SetStatus(printerStatus, printerStatus.Text, null);
                return;
            }

            bool isReady = DeviceSettings.IsReadyForPrinting(printerChoice.Value);
            SetPrinterStatus(
                isReady
                    ? Present("الطابعة المختارة متاحة وجاهزة للاستخدام من هذه المحطة. لم تُرسل أي طباعة تجريبية.")
                    : Present("تعذر التحقق من الطابعة المختارة. تأكد من اتصالها ومن تعريف Windows ثم حدّث القائمة."),
                isReady);
        }

        private void ConfigureActionDirection()
        {
            Panel actions = Controls.Find("deviceSettingsActions", true).OfType<Panel>().FirstOrDefault();
            if (actions != null) LayoutActions(actions);
        }

        private void ApplyPreviewMode()
        {
            if (!previewOnly)
            {
                return;
            }

            saveButton.Enabled = false;
            saveButton.AccessibleDescription = Present("الحفظ معطل في المعاينة؛ لا تتغير تفضيلات هذه المحطة.");
            SetPrinterStatus(Present("معاينة فقط: الحفظ معطّل."), null);
        }

        private void SetPrinterStatus(string message, bool? isSuccess)
        {
            printerStatus.Text = message ?? string.Empty;
            printerStatus.AccessibleName = Present("حالة الطابعة") + ": " + printerStatus.Text;
            UiTheme.SetStatus(printerStatus, printerStatus.Text, isSuccess);
            LayoutContent();
        }

        private void LayoutContent()
        {
            if (arrangingContent || titleLabel == null) return;
            arrangingContent = true;
            try
            {
                Panel header = Controls.Find("deviceSettingsHeader", true).OfType<Panel>().FirstOrDefault();
                AntdUI.Panel body = Controls.Find("deviceSettingsBody", true).OfType<AntdUI.Panel>().FirstOrDefault();
                TableLayoutPanel fields = Controls.Find("deviceSettingsFields", true).OfType<TableLayoutPanel>().FirstOrDefault();
                if (header == null || body == null || fields == null) return;
                // Coordinates are explicit; mirror text and selectors, not the scroll container twice.
                body.RightToLeft = RightToLeft.No;
                int width = Math.Max(160, header.ClientSize.Width - header.Padding.Horizontal);
                titleLabel.Height = Math.Max(38, MeasureHeight(titleLabel, width));
                header.Height = header.Padding.Vertical + titleLabel.Height + Math.Max(30, MeasureHeight(subtitleLabel, width));
                int available = Math.Max(200, body.ClientSize.Width - body.Padding.Horizontal -
                    SystemInformation.VerticalScrollBarWidth - (int)Math.Ceiling(body.BorderWidth * 2));
                int contentWidth = Math.Min(760, available);
                fields.Dock = DockStyle.None;
                fields.RightToLeft = RightToLeft.No;
                fields.Width = contentWidth;
                foreach (Label caption in new[] { printerLabel, receiptPaperLabel, invoicePaperLabel })
                {
                    caption.AutoSize = false;
                    caption.Dock = DockStyle.Top;
                    caption.Height = Math.Max(30, MeasureHeight(caption, contentWidth));
                }
                printerStatus.Height = Math.Max(48, MeasureHeight(printerStatus, contentWidth));
                barcodeInfo.Height = Math.Max(64, MeasureHeight(barcodeInfo, contentWidth - barcodeInfo.Padding.Horizontal) + barcodeInfo.Padding.Vertical);
                fields.Height = fields.GetPreferredSize(new Size(contentWidth, 0)).Height;
                // AntdUI places children inside its padded internal scroll panel.
                fields.Location = new Point(Math.Max(0, (available - contentWidth) / 2), 0);
                fields.PerformLayout();
            }
            finally { arrangingContent = false; }
        }

        private static int MeasureHeight(Label label, int width)
        {
            return TextRenderer.MeasureText(label.Text, label.Font, new Size(Math.Max(80, width), 1000),
                TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height + 6;
        }

        private string Present(string arabic)
        {
            return leftToRight ? UiLanguageCatalog.Translate(arabic) : arabic;
        }

        private ContentAlignment ReadingAlignment()
        {
            return leftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
        }

        private void ApplyDisplayLanguage()
        {
            Text = Present("مركز الأجهزة");
            titleLabel.Text = Present("مركز الأجهزة");
            subtitleLabel.Text = Present("اضبط الطابعة ومقاس الفاتورة لهذه المحطة.");
            printerLabel.Text = Present("الطابعة المفضلة");
            receiptPaperLabel.Text = Present("مقاس إيصال الكاشير");
            invoicePaperLabel.Text = Present("المقاس الافتراضي لفاتورة البيع والشراء");
            barcodeInfo.Text = Present("قارئ الباركود USB/HID: امسح الرمز ثم اضغط Enter.");
            saveButton.Text = Present("حفظ الإعدادات");
            checkPrinterButton.Text = Present("فحص الطابعة");
            closeButton.Text = Present("إغلاق");
            titleLabel.TextAlign = subtitleLabel.TextAlign = printerStatus.TextAlign = barcodeInfo.TextAlign = ReadingAlignment();
            printer.AccessibleName = Present("اختيار الطابعة المفضلة");
            receiptPaper.AccessibleName = Present("اختيار مقاس إيصال الكاشير");
            invoicePaper.AccessibleName = Present("اختيار مقاس فاتورة البيع والشراء");
            saveButton.AccessibleName = Present("حفظ إعدادات الأجهزة");
            checkPrinterButton.AccessibleName = Present("فحص توفر الطابعة المختارة");
            checkPrinterButton.AccessibleDescription = Present("يتحقق من توفر الطابعة محلياً دون إرسال مهمة طباعة.");
            closeButton.AccessibleName = Present("إغلاق مركز الأجهزة");
            barcodeInfo.AccessibleName = Present("معلومة قارئ الباركود");
        }

        private void ApplyNeutralSurface()
        {
            Panel header = Controls.Find("deviceSettingsHeader", true).OfType<Panel>().FirstOrDefault();
            AntdUI.Panel body = Controls.Find("deviceSettingsBody", true).OfType<AntdUI.Panel>().FirstOrDefault();
            TableLayoutPanel fields = Controls.Find("deviceSettingsFields", true).OfType<TableLayoutPanel>().FirstOrDefault();
            if (header != null) header.BackColor = UiTheme.Background;
            if (body != null)
            {
                body.BackColor = UiTheme.Surface;
                AntdThemeAdapter.Apply(body);
            }
            if (fields != null) fields.BackColor = UiTheme.Surface;
            titleLabel.BackColor = UiTheme.Background;
            titleLabel.ForeColor = UiTheme.Text;
            subtitleLabel.BackColor = UiTheme.Background;
            subtitleLabel.ForeColor = UiTheme.MutedText;
            foreach (Label label in new[] { printerLabel, receiptPaperLabel, invoicePaperLabel, printerStatus, barcodeInfo })
            {
                label.BackColor = UiTheme.Surface;
                if (label != printerStatus) label.ForeColor = label == barcodeInfo ? UiTheme.MutedText : UiTheme.Text;
            }
            foreach (Panel host in Controls.Find("preferredPrinterHost", true).OfType<Panel>()
                .Concat(Controls.Find("receiptPaperFormatHost", true).OfType<Panel>())
                .Concat(Controls.Find("invoicePrintFormatHost", true).OfType<Panel>()))
                host.BackColor = UiTheme.Surface;
        }
        private sealed class DeviceChoice
        {
            internal DeviceChoice(string text, string value) { Text = text; Value = value; }
            internal string Value { get; private set; }
            public override string ToString() { return Text; }
            private string Text { get; set; }
        }
    }
}


// مركز اللغة: نقطة واحدة لإدارة لغة الواجهة وتوضيح حالة توفر اللغات بدقة.
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace project_fo_3
{
    internal sealed class LanguageSettingsForm : Form
    {
        private readonly RadioButton arabicOption = new RadioButton { Name = "languageArabicOption" };
        private readonly RadioButton englishOption = new RadioButton { Name = "languageEnglishOption" };
        private readonly Label arabicCaption = new Label { Name = "languageArabicCaption" };
        private readonly Label englishCaption = new Label { Name = "languageEnglishCaption" };
        private readonly Label status = new Label { Name = "languageStatus" };
        private readonly AntdUI.Button saveButton = new AccessibleAntdButton { Name = "languageSaveButton" };
        private readonly AntdUI.Button closeButton = new AccessibleAntdButton { Name = "languageCloseButton" };
        private readonly bool previewOnly;
        private readonly bool forceLeftToRight;
        internal bool PresentationLeftToRight => forceLeftToRight;
        private Label titleLabel;
        private Label subtitleLabel;
        private Label descriptionLabel;

        internal LanguageSettingsForm() : this(false, UiLanguageCatalog.IsEnglishActive) { }

        internal LanguageSettingsForm(bool previewOnly, bool forceLeftToRight = false)
        {
            this.previewOnly = previewOnly;
            this.forceLeftToRight = forceLeftToRight;
            Text = Present("مركز اللغة");
            ClientSize = new Size(600, 390);
            MinimumSize = new Size(420, 310);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = true;
            RightToLeft = forceLeftToRight ? RightToLeft.No : RightToLeft.Yes;
            RightToLeftLayout = !forceLeftToRight;

            BuildLayout();
            UiTheme.Apply(this);
            ApplyNeutralHeader();
            RefreshState();
            UiDirection.Apply(this, forceLeftToRight);
            ApplyDisplayLanguage();
            Panel actions = Controls.Find("languageActions", true).OfType<Panel>().FirstOrDefault();
            if (actions != null) actions.BackColor = UiTheme.Background;
            AntdUI.Panel englishRow = Controls.Find("languageEnglishRow", true).OfType<AntdUI.Panel>().FirstOrDefault();
            if (englishRow != null) englishRow.BackColor = UiTheme.Surface;
            DesktopComponents.StyleOutlinedFooterAction(saveButton);
            DesktopComponents.StyleOutlinedFooterAction(closeButton);
            englishCaption.ForeColor = UiTheme.MutedText;
            ConfigureActionDirection();
            ApplyPreviewMode();
            AcceptButton = (IButtonControl)saveButton;
            CancelButton = (IButtonControl)closeButton;
            ((IButtonControl)closeButton).DialogResult = DialogResult.Cancel;
            Resize += (sender, args) => RefreshMeasuredLayout();
            RefreshMeasuredLayout();
            if (previewOnly)
            {
                Control runtimeStatus = Controls["__UiStatusStrip"];
                if (runtimeStatus != null) runtimeStatus.Dispose();
            }
        }

        private void BuildLayout()
        {
            Panel header = new Panel { Name = "languageHeader", Tag = "UiSurface", Dock = DockStyle.Top, Height = 92, Padding = new Padding(22, 12, 22, 10) };
            titleLabel = new Label { Name = "languageTitle", Dock = DockStyle.Top, Height = 38, Text = Present("لغة الواجهة"), TextAlign = ReadingAlignment(), Font = AppFonts.Create(16F, FontStyle.Bold) };
            subtitleLabel = new Label { Name = "languageSubtitle", Dock = DockStyle.Fill, Text = Present("اختر لغة الواجهة المتاحة."), TextAlign = ReadingAlignment() };
            header.Controls.Add(subtitleLabel);
            header.Controls.Add(titleLabel);

            AntdUI.Panel body = new AntdUI.Panel { Name = "languageBody", Dock = DockStyle.Fill, AutoScroll = true,
                Padding = new Padding(28, 12, 28, 12), BackColor = UiTheme.Surface };
            descriptionLabel = new Label
            {
                Name = "languageDescription",
                Dock = DockStyle.Top,
                Height = 46,
                Text = Present("العربية هي لغة الواجهة الافتراضية."),
                TextAlign = ReadingAlignment(),
                AccessibleName = Present("وصف لغة الواجهة")
            };

            AntdUI.Panel arabicRow = DesktopComponents.Card("languageArabicRow");
            arabicRow.Dock = DockStyle.Top; arabicRow.Height = 64; arabicRow.Padding = new Padding(12, 10, 12, 10);
            arabicOption.Height = 44;
            arabicOption.Width = 24;
            arabicOption.Text = string.Empty;
            arabicOption.AccessibleName = Present("اختيار اللغة العربية");
            arabicCaption.Text = Present("العربية (الافتراضية)");
            arabicCaption.Height = 44;
            arabicCaption.TextAlign = ReadingAlignment();
            arabicCaption.AccessibleName = Present("العربية هي اللغة الافتراضية");
            arabicRow.Controls.Add(arabicOption);
            arabicRow.Controls.Add(arabicCaption);
            arabicRow.Resize += (sender, args) => LayoutLanguageRow(arabicRow, arabicOption, arabicCaption);

            AntdUI.Panel englishRow = DesktopComponents.Card("languageEnglishRow");
            englishRow.Dock = DockStyle.Top; englishRow.Height = 64; englishRow.Padding = new Padding(12, 10, 12, 10);
            englishOption.Height = 44;
            englishOption.Width = 24;
            englishOption.Text = string.Empty;
            englishOption.AccessibleName = Present("اختيار اللغة الإنجليزية");
            englishOption.AccessibleDescription = Present("لا يمكن تفعيل الإنجليزية قبل اكتمال ترجمة الشاشات والتحقق منها.");
            englishOption.Enabled = UiLanguageSettings.IsEnglishAvailable;
            englishCaption.Text = Present("English — قيد الإعداد");
            englishCaption.Height = 44;
            englishCaption.TextAlign = ReadingAlignment();
            englishCaption.AccessibleName = Present("الإنجليزية قيد الإعداد");
            englishRow.Controls.Add(englishOption);
            englishRow.Controls.Add(englishCaption);
            englishRow.Resize += (sender, args) => LayoutEnglishRow(englishRow);

            status.Dock = DockStyle.Top;
            status.Height = 48;
            status.Padding = new Padding(8);
            status.TextAlign = ReadingAlignment();
            status.AccessibleName = Present("حالة توفر اللغة");

            Panel actions = new Panel { Name = "languageActions", Dock = DockStyle.Bottom,
                Height = 66, BackColor = UiTheme.Background };
            saveButton.Size = new Size(118, 44);
            saveButton.Text = Present("حفظ");
            saveButton.AccessibleName = Present("حفظ إعداد لغة الواجهة");
            saveButton.IconSvg = OutlinedIcons.Svg(AppIcon.Save, true);
            saveButton.IconSize = new Size(18, 18);
            saveButton.Click += SaveButton_Click;
            closeButton.Size = new Size(118, 44);
            closeButton.Text = Present("إغلاق");
            closeButton.AccessibleName = Present("إغلاق مركز اللغة");
            closeButton.IconSvg = OutlinedIcons.Svg(AppIcon.Close, true);
            closeButton.IconSize = new Size(18, 18);
            closeButton.Click += (sender, args) => { DialogResult = DialogResult.Cancel; Close(); };
            actions.Controls.Add(saveButton);
            actions.Controls.Add(closeButton);

            body.Controls.Add(status);
            body.Controls.Add(englishRow);
            body.Controls.Add(arabicRow);
            body.Controls.Add(descriptionLabel);
            Controls.Add(body);
            Controls.Add(actions);
            Controls.Add(header);

            UiTheme.ApplyDialogHeader(header, titleLabel, subtitleLabel, null);
            UiTheme.ApplyInfoLabels(status);
            AntdThemeAdapter.Apply(body);
            DesktopComponents.StyleOutlinedFooterAction(saveButton);
            DesktopComponents.StyleOutlinedFooterAction(closeButton);
            actions.Resize += (sender, args) => LayoutActions(actions);
            KeyPreview = true;
            KeyDown += (sender, args) => { if (args.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); } };
            LayoutActions(actions);
            LayoutLanguageRow(arabicRow, arabicOption, arabicCaption);
            LayoutEnglishRow(englishRow);
        }

        private void ApplyNeutralHeader()
        {
            Panel header = Controls.Find("languageHeader", true).OfType<Panel>().First();
            header.BackColor = UiTheme.Background;
            Label title = Controls.Find("languageTitle", true).OfType<Label>().First();
            Label subtitle = Controls.Find("languageSubtitle", true).OfType<Label>().First();
            title.BackColor = UiTheme.Background;
            title.ForeColor = UiTheme.Text;
            subtitle.BackColor = UiTheme.Background;
            subtitle.ForeColor = UiTheme.MutedText;
            AntdUI.Panel body = Controls.Find("languageBody", true).OfType<AntdUI.Panel>().First();
            body.BackColor = UiTheme.Surface;
            AntdThemeAdapter.Apply(body);
            Color contentBack = UiTheme.Surface;
            descriptionLabel.BackColor = contentBack;
            descriptionLabel.ForeColor = UiTheme.Text;
            AntdUI.Panel arabicRow = Controls.Find("languageArabicRow", true).OfType<AntdUI.Panel>().First();
            arabicRow.BackColor = contentBack;
            arabicCaption.BackColor = contentBack;
            arabicCaption.ForeColor = UiTheme.Text;
            AntdUI.Panel englishRow = Controls.Find("languageEnglishRow", true).OfType<AntdUI.Panel>().First();
            englishRow.BackColor = contentBack;
            englishCaption.BackColor = contentBack;
        }

        private void LayoutLanguageRow(Control row, RadioButton option, Label caption)
        {
            const int gap = 8;
            option.Location = new Point(forceLeftToRight ? 12 : row.ClientSize.Width - 12 - option.Width, 10);
            caption.SetBounds(forceLeftToRight ? option.Right + gap : 12, 10,
                Math.Max(0, row.ClientSize.Width - 24 - option.Width - gap), 44);
        }

        private void LayoutEnglishRow(Control englishRow)
        {
            LayoutLanguageRow(englishRow, englishOption, englishCaption);
        }

        private void LayoutActions(Panel actions)
        {
            const int margin = 28;
            const int gap = 12;
            int firstX = forceLeftToRight
                ? margin : actions.ClientSize.Width - margin - saveButton.Width;
            saveButton.Location = new Point(firstX, 11);
            closeButton.Location = new Point(forceLeftToRight
                ? saveButton.Right + gap : saveButton.Left - gap - closeButton.Width, 11);
        }

        private void RefreshMeasuredLayout()
        {
            Panel header = Controls.Find("languageHeader", true).OfType<Panel>().FirstOrDefault();
            if (header == null) return;
            int width = Math.Max(1, header.ClientSize.Width - header.Padding.Horizontal);
            titleLabel.Height = Math.Max(38, TextRenderer.MeasureText(titleLabel.Text, titleLabel.Font,
                new Size(width, int.MaxValue), TextFormatFlags.WordBreak).Height + 4);
            int subtitleHeight = Math.Max(26, TextRenderer.MeasureText(subtitleLabel.Text, subtitleLabel.Font,
                new Size(width, int.MaxValue), TextFormatFlags.WordBreak).Height + 4);
            header.Height = titleLabel.Height + subtitleHeight + header.Padding.Vertical;
            AntdUI.Panel body = Controls.Find("languageBody", true).OfType<AntdUI.Panel>().First();
            int contentWidth = Math.Max(1, body.ClientSize.Width - body.Padding.Horizontal - 18);
            descriptionLabel.Height = Math.Max(46, TextRenderer.MeasureText(descriptionLabel.Text, descriptionLabel.Font,
                new Size(contentWidth, int.MaxValue), TextFormatFlags.WordBreak).Height + 8);
            status.Height = Math.Max(48, TextRenderer.MeasureText(status.Text, status.Font,
                new Size(Math.Max(1, contentWidth - status.Padding.Horizontal), int.MaxValue), TextFormatFlags.WordBreak).Height + status.Padding.Vertical + 8);
            titleLabel.AccessibleName = titleLabel.Text;
            subtitleLabel.AccessibleName = subtitleLabel.Text;
            ConfigureActionDirection();
        }

        private void RefreshState()
        {
            bool englishActive = UiLanguageSettings.IsEnglishAvailable &&
                string.Equals(UiLanguageSettings.Load(), UiLanguageSettings.English, StringComparison.OrdinalIgnoreCase);
            arabicOption.Checked = !englishActive;
            englishOption.Checked = englishActive;
            bool? availabilityStatus = UiLanguageSettings.IsEnglishAvailable ? (bool?)true : null;
            SetLanguageStatus(
                UiLanguageSettings.IsEnglishAvailable
                    ? Present("التغيير يطبّق بعد إعادة تشغيل النظام.")
                    : Present("الإنجليزية قيد الإعداد؛ العربية هي اللغة المتاحة حاليًا."),
                availabilityStatus);
        }

        private void ConfigureActionDirection()
        {
            Panel actions = Controls.Find("languageActions", true).OfType<Panel>().FirstOrDefault();
            if (actions == null) return;
            LayoutActions(actions);
            AntdUI.Panel englishRow = Controls.Find("languageEnglishRow", true).OfType<AntdUI.Panel>().FirstOrDefault();
            if (englishRow != null) LayoutEnglishRow(englishRow);
            AntdUI.Panel arabicRow = Controls.Find("languageArabicRow", true).OfType<AntdUI.Panel>().FirstOrDefault();
            if (arabicRow != null) LayoutLanguageRow(arabicRow, arabicOption, arabicCaption);
        }

        private void ApplyPreviewMode()
        {
            if (!previewOnly) return;

            saveButton.Enabled = false;
            saveButton.AccessibleDescription = Present("الحفظ معطل في المعاينة؛ لا تتغير لغة المستخدم المحفوظة.");
            SetLanguageStatus(Present("معاينة فقط: لن تتغير اللغة المحفوظة."), null);
        }

        private void SetLanguageStatus(string message, bool? availabilityStatus)
        {
            status.Text = message ?? string.Empty;
            status.AccessibleName = Present("حالة توفر اللغة") + ": " + status.Text;
            UiTheme.SetStatus(status, status.Text, availabilityStatus);
            status.BackColor = UiTheme.Surface;
        }

        private void SaveButton_Click(object sender, EventArgs e)
        {
            if (previewOnly) return;
            if (!arabicOption.Checked || !UiLanguageSettings.TrySave(UiLanguageSettings.Arabic))
            {
                AppNotifications.Show(this, Present("تعذر حفظ تفضيل اللغة. بقي الإعداد السابق؛ أعد المحاولة."), Present("مركز اللغة"), MessageBoxButtons.OK, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1, forceLeftToRight);
                return;
            }

            AppNotifications.Show(this, Present("تم حفظ العربية كلغة افتراضية للواجهة."), Present("مركز اللغة"), MessageBoxButtons.OK, MessageBoxIcon.Information, MessageBoxDefaultButton.Button1, forceLeftToRight);
            DialogResult = DialogResult.OK;
            Close();
        }

        private string Present(string arabic)
        {
            return forceLeftToRight ? UiLanguageCatalog.Translate(arabic) : arabic;
        }

        private ContentAlignment ReadingAlignment()
        {
            return forceLeftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
        }

        private void ApplyDisplayLanguage()
        {
            Text = Present("مركز اللغة");
            titleLabel.Text = Present("لغة الواجهة");
            subtitleLabel.Text = Present("اختر لغة الواجهة المتاحة.");
            descriptionLabel.Text = Present("العربية هي لغة الواجهة الافتراضية.");
            arabicCaption.Text = Present("العربية (الافتراضية)");
            englishCaption.Text = Present("English — قيد الإعداد");
            saveButton.Text = Present("حفظ");
            closeButton.Text = Present("إغلاق");
            titleLabel.TextAlign = subtitleLabel.TextAlign = descriptionLabel.TextAlign =
                arabicCaption.TextAlign = englishCaption.TextAlign = status.TextAlign = ReadingAlignment();
            descriptionLabel.AccessibleName = Present("وصف لغة الواجهة");
            arabicOption.AccessibleName = Present("اختيار اللغة العربية");
            arabicCaption.AccessibleName = Present("العربية هي اللغة الافتراضية");
            englishOption.AccessibleName = Present("اختيار اللغة الإنجليزية");
            englishOption.AccessibleDescription = Present("لا يمكن تفعيل الإنجليزية قبل اكتمال ترجمة الشاشات والتحقق منها.");
            englishCaption.AccessibleName = Present("الإنجليزية قيد الإعداد");
            saveButton.AccessibleName = Present("حفظ إعداد لغة الواجهة");
            closeButton.AccessibleName = Present("إغلاق مركز اللغة");
        }
    }
}

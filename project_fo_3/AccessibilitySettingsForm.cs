using System;
using System.Drawing;
using System.Windows.Forms;

namespace project_fo_3
{
    internal class AccessibilitySettingsForm : Form
    {
        private readonly RadioButton centered = new RadioButton { Name = "openCentered", AutoSize = true };
        private readonly RadioButton maximized = new RadioButton { Name = "openMaximized", AutoSize = true };
        private readonly Label status = new Label { Name = "accessibilityStatus", AutoSize = true, Dock = DockStyle.Top };

        private readonly bool previewOnly;
        private readonly bool presentationLeftToRight;
        private readonly Func<bool, bool> saveOpeningPreference;
        internal bool PresentationLeftToRight => presentationLeftToRight;

        internal AccessibilitySettingsForm() : this(false, UiLanguageCatalog.IsEnglishActive) { }
        internal AccessibilitySettingsForm(bool previewOnly, bool presentationLeftToRight)
            : this(previewOnly, presentationLeftToRight, ScreenModeSettings.TrySaveOpeningPreference) { }
        internal AccessibilitySettingsForm(bool previewOnly, bool presentationLeftToRight, Func<bool, bool> saveOpeningPreference)
        {
            this.previewOnly = previewOnly;
            this.presentationLeftToRight = presentationLeftToRight;
            this.saveOpeningPreference = saveOpeningPreference ?? throw new ArgumentNullException(nameof(saveOpeningPreference));
            Text = Present("تسهيلات الاستخدام الافتراضية");
            ClientSize = new Size(620, 360);
            MinimumSize = new Size(360, 300);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = false;
            MinimizeBox = false;
            var body = new FlowLayoutPanel { Name = "accessibilityBody", Tag = "UiSurface", Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(24) };
            body.Controls.Add(new Label { Name = "accessibilityTitle", AccessibleName = Text, Text = Text, AutoSize = true, Font = AppFonts.Create(14F, FontStyle.Bold) });
            body.Controls.Add(new Label { Name = "accessibilityHint", Text = Present("اختر طريقة فتح الشاشات الجديدة."), AccessibleName = Present("اختر طريقة فتح الشاشات الجديدة."), AutoSize = true, Margin = new Padding(3, 12, 3, 12) });
            centered.Text = Present("فتح في وسط الصفحة");
            maximized.Text = Present("فتح كامل");
            centered.AccessibleName = centered.Text;
            maximized.AccessibleName = maximized.Text;
            centered.Padding = maximized.Padding = new Padding(4, 10, 4, 10);
            // Both options share their parent so keyboard and selection remain mutually exclusive.
            body.Controls.Add(centered);
            body.Controls.Add(maximized);
            status.Text = Present("يطبق الاختيار عند فتح الشاشات؛ تبقى الحوارات الصغيرة بحجمها المناسب.");
            status.Margin = new Padding(3, 14, 3, 3);
            body.Controls.Add(status);
            var actions = new FlowLayoutPanel { Name = "accessibilityActions", AccessibleName = Text, Tag = "UiSurface", Dock = DockStyle.Bottom, Height = 76, Padding = new Padding(12), FlowDirection = FlowDirection.RightToLeft };
            var save = DesktopComponents.Action("saveAccessibility", Present("حفظ"), AppIcon.Save);
            var close = DesktopComponents.Action("closeAccessibility", Present("إغلاق"), AppIcon.Close);
            save.Size = close.Size = new Size(120, DesktopDesignTokens.ActionHeight);
            save.AccessibleName = Present("حفظ تسهيلات الاستخدام الافتراضية");
            close.AccessibleName = Present("إغلاق دون حفظ");
            save.Click += (sender, args) =>
            {
                if (this.previewOnly) return;
                bool success;
                try { success = this.saveOpeningPreference(maximized.Checked); }
                catch (Exception error) { AppDiagnostics.Log("AccessibilitySettingsSave", error); success = false; }
                SetSaveStatus(success ? "تم الحفظ؛ سيطبق الاختيار عند فتح الشاشة التالية." :
                    "تعذر تأكيد حفظ الإعداد؛ حاول مرة أخرى.", success);
            };
            close.Click += (sender, args) => Close();
            AcceptButton = save;
            CancelButton = close;
            close.DialogResult = DialogResult.Cancel;
            actions.Controls.Add(save);
            actions.Controls.Add(close);
            Controls.Add(body);
            Controls.Add(actions);
            UiTheme.UseDedicatedStatus(this);
            UiTheme.Apply(this);
            UiDirection.Apply(this, presentationLeftToRight);
            body.AccessibleName = Text;
            actions.RightToLeft = RightToLeft.No;
            actions.FlowDirection = presentationLeftToRight ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;
            save.Enabled = !previewOnly;
            SetSaveStatus("يطبق الاختيار عند فتح الشاشات؛ تبقى الحوارات الصغيرة بحجمها المناسب.", null);
            centered.Checked = !ScreenModeSettings.LoadOpeningPreference();
            maximized.Checked = !centered.Checked;
            Action layoutBody = () =>
            {
                int width = Math.Max(1, body.ClientSize.Width - body.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth - 12);
                foreach (Control control in body.Controls) control.MaximumSize = new Size(width, 0);
            };
            body.Resize += (sender, args) => layoutBody();
            layoutBody();
            KeyPreview = true;
            KeyDown += (sender, args) => { if (args.KeyCode == Keys.Escape) { args.Handled = args.SuppressKeyPress = true; Close(); } };
        }

        private string Present(string message) => presentationLeftToRight ? UiLanguageCatalog.Translate(message) : message;

        private void SetSaveStatus(string message, bool? success)
        {
            UiTheme.SetStatus(status, Present(message), success);
            status.AccessibleName = status.Text;
            status.AccessibleDescription = Present("حالة حفظ تسهيلات الاستخدام");
        }
    }
}

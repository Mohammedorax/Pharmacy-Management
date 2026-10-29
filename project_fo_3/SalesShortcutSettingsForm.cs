using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace project_fo_3
{
    internal sealed class SalesShortcutSettingsForm : Form
    {
        private readonly SalesShortcutProfileStore store;
        private readonly bool previewOnly;
        private readonly bool leftToRight;
        internal bool PresentationLeftToRight => leftToRight;
        private readonly string userName;
        private readonly long revision;
        private bool measuringScrollExtent;
        private readonly Dictionary<SalesShortcutAction, ComboBox> choices = new Dictionary<SalesShortcutAction, ComboBox>();
        private readonly Label status = new Label { Name = "shortcutStatus", Dock = DockStyle.Bottom, Height = 68, Padding = new Padding(16, 8, 16, 8) };
        private readonly AntdUI.Button save = new AccessibleAntdButton { Name = "shortcutSave", Size = new Size(96, 40) };
        private readonly AntdUI.Button reset = new AccessibleAntdButton { Name = "shortcutReset", Size = new Size(130, 40) };
        private readonly AntdUI.Button close = new AccessibleAntdButton { Name = "shortcutClose", Size = new Size(96, 40) };
        private readonly TableLayoutPanel body = new TableLayoutPanel { Name = "shortcutBody", Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(16), ColumnCount = 2, RowCount = 6 };

        internal SalesShortcutSettingsForm() : this(new SalesShortcutProfileStore(SalesShortcutProfileStore.DefaultDirectory), false, UiDirection.IsLeftToRight) { }
        internal SalesShortcutSettingsForm(SalesShortcutProfileStore store, bool previewOnly, bool leftToRight)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.previewOnly = previewOnly;
            this.leftToRight = leftToRight;
            userName = SessionContext.CurrentUser;
            revision = SessionContext.CurrentSessionRevision;
            Text = Present("اختصارات البيع");
            ClientSize = new Size(620, 540);
            MinimumSize = new Size(380, 390);
            StartPosition = FormStartPosition.CenterParent;
            RightToLeft = leftToRight ? RightToLeft.No : RightToLeft.Yes;
            RightToLeftLayout = !leftToRight;
            FormBorderStyle = FormBorderStyle.Sizable;
            var header = new Panel { Name = "shortcutHeader", Dock = DockStyle.Top, Height = 88, Padding = new Padding(16, 12, 16, 8) };
            var title = new Label { Name = "shortcutTitle", Text = Text, Dock = DockStyle.Top, Height = 34, Font = AppFonts.Create(16F, FontStyle.Bold) };
            var subtitle = new Label { Text = Present("اختر مفتاحًا مختلفًا لكل إجراء."), Dock = DockStyle.Fill };
            header.Controls.Add(subtitle); header.Controls.Add(title);
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
            AddChoice(SalesShortcutAction.Search, Present("البحث عن صنف"), 0);
            AddChoice(SalesShortcutAction.Barcode, Present("حقل الصنف (الاسم أو الباركود)"), 1);
            AddChoice(SalesShortcutAction.Quantity, Present("الانتقال إلى الكمية"), 2);
            AddChoice(SalesShortcutAction.Preview, Present("بيع وطباعة"), 3);
            var hint = new Label { Name = "shortcutHint", AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(0, 12, 0, 0),
                Text = Present("يبقى Ctrl+K متاحًا للبحث. تطبق الإعدادات على شاشة بيع جديدة لهذا المستخدم.") };
            body.Controls.Add(hint, 0, 4); body.SetColumnSpan(hint, 2);
            body.RowStyles.Add(new RowStyle(SizeType.AutoSize)); body.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            body.RowStyles.Add(new RowStyle(SizeType.AutoSize)); body.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            body.RowStyles.Add(new RowStyle(SizeType.AutoSize)); body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var footer = new Panel { Name = "shortcutActions", Dock = DockStyle.Bottom, Height = 64 };
            save.Text = Present("حفظ");
            reset.Text = Present("الافتراضي");
            close.Text = Present("إغلاق");
            save.AccessibleName = Present("حفظ اختصارات البيع");
            reset.AccessibleName = Present("استعادة الاختصارات الافتراضية");
            close.AccessibleName = close.Text;
            footer.Controls.Add(save); footer.Controls.Add(reset); footer.Controls.Add(close);
            Action arrange = () =>
            {
                int gap = 8;
                int x = Math.Max(8, (footer.ClientSize.Width - (save.Width + reset.Width + close.Width + gap * 2)) / 2);
                var buttons = leftToRight ? new[] { save, reset, close } : new[] { close, reset, save };
                foreach (var button in buttons) { button.Location = new Point(x, 12); x += button.Width + gap; }
                hint.MaximumSize = new Size(Math.Max(1, body.ClientSize.Width - body.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth), 0);
            };
            footer.Resize += (sender, args) => arrange();
            body.Resize += (sender, args) => arrange();
            body.Layout += (sender, args) => UpdateScrollExtent();
            Controls.Add(body); Controls.Add(status); Controls.Add(footer); Controls.Add(header);
            save.Click += (sender, args) => SaveProfile();
            reset.Click += (sender, args) => { Bind(SalesShortcutProfile.Defaults); SetStatus(Present("استعدت القيم الافتراضية. اختر حفظ لتطبيقها."), false); };
            close.Click += (sender, args) => Close();
            UiTheme.Apply(this);
            UiTheme.RemoveStatusStrip(this);
            UiTheme.ApplyDialogHeader(header, title, subtitle, null);
            title.Height = Math.Max(34, title.Font.Height + 6);
            header.Height = Math.Max(88, title.Height + subtitle.Font.Height + header.Padding.Vertical + 12);
            UiDirection.Apply(this, leftToRight);
            save.Text = Present("حفظ");
            reset.Text = Present("الافتراضي");
            close.Text = Present("إغلاق");
            footer.BackColor = UiTheme.Background;
            foreach (var button in new[] { save, reset, close }) DesktopComponents.StyleOutlinedFooterAction(button);
            save.IconSvg = OutlinedIcons.Svg(AppIcon.Save, true);
            close.IconSvg = OutlinedIcons.Svg(AppIcon.Close, true);
            status.TextAlign = leftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
            Bind(SalesShortcutProfile.Defaults);
            if (!previewOnly && Current())
            {
                try { Bind(store.Load(userName)); }
                catch (Exception error)
                {
                    AppDiagnostics.Log("SalesShortcutSettingsLoad", error);
                    SetStatus(Present("تعذر قراءة الإعدادات. راجع الاختصارات ثم احفظ لاستبدال الملف."), true);
                }
            }
            save.Enabled = !previewOnly && Current();
            if (previewOnly) SetStatus(Present("معاينة فقط؛ لا تُحفظ إعدادات."), false);
            else if (!Current()) InvalidateSession();
            UiSessionSubscription.Attach(this, InvalidateSession);
            CancelButton = (IButtonControl)close;
            ((IButtonControl)close).DialogResult = DialogResult.Cancel;
            arrange();
        }

        private void UpdateScrollExtent()
        {
            if (measuringScrollExtent || body.IsDisposed || body.Disposing || IsDisposed || Disposing) return;
            measuringScrollExtent = true;
            try
            {
                // The table can place autosized rows beyond its viewport without publishing a scroll extent.
                int contentBottom = 0;
                foreach (Control control in body.Controls)
                    contentBottom = Math.Max(contentBottom, control.Bottom + control.Margin.Bottom);
                int height = Math.Max(0, contentBottom - body.AutoScrollPosition.Y + body.Padding.Bottom);
                if (body.AutoScrollMinSize.Height != height) body.AutoScrollMinSize = new Size(0, height);
            }
            finally { measuringScrollExtent = false; }
        }

        private string Present(string arabic) { return leftToRight ? UiLanguageCatalog.Translate(arabic) : arabic; }
        private bool Current() { return SessionContext.IsAuthenticated && revision == SessionContext.CurrentSessionRevision && string.Equals(userName, SessionContext.CurrentUser, StringComparison.OrdinalIgnoreCase); }
        private void InvalidateSession()
        {
            if (previewOnly || Current()) return;
            Bind(SalesShortcutProfile.Defaults);
            save.Enabled = false; reset.Enabled = false; body.Enabled = false;
            SetStatus(Present("تغيرت الجلسة. أغلق النافذة وافتحها من الحساب الحالي."), true);
        }
        private void AddChoice(SalesShortcutAction action, string caption, int row)
        {
            var label = new Label { Text = caption, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 8, 8, 12), TextAlign = leftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight };
            var choice = new ComboBox { Name = "shortcut" + action + "Key", DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Top, Margin = new Padding(0, 8, 0, 12), AccessibleName = caption, RightToLeft = RightToLeft.No };
            foreach (var key in SalesShortcutProfile.AvailableKeys) choice.Items.Add(key);
            choices.Add(action, choice);
            body.Controls.Add(label, 0, row); body.Controls.Add(choice, 1, row);
        }
        private void Bind(SalesShortcutProfile profile) { foreach (var choice in choices) choice.Value.SelectedItem = profile[choice.Key]; }
        private void SetStatus(string text, bool failure) { status.Text = text; status.ForeColor = failure ? UiTheme.Danger : UiTheme.MutedText; status.AccessibleDescription = text; }
        private void SaveProfile()
        {
            if (previewOnly || !Current()) { InvalidateSession(); return; }
            SalesShortcutProfile profile;
            try
            {
                var values = new Dictionary<SalesShortcutAction, Keys>();
                foreach (var choice in choices) values.Add(choice.Key, (Keys)choice.Value.SelectedItem);
                profile = new SalesShortcutProfile(values);
            }
            catch (ArgumentException)
            {
                SetStatus(Present("اختر مفتاحًا مختلفًا لكل إجراء؛ الاختصارات المتكررة غير مقبولة."), true);
                return;
            }
            try
            {
                store.Save(userName, profile, Current);
                if (!Current()) { InvalidateSession(); return; }
                SetStatus(Present("تم حفظ الاختصارات. افتح شاشة بيع جديدة لتطبيقها."), false);
            }
            catch (Exception error)
            {
                if (!Current()) { InvalidateSession(); return; }
                AppDiagnostics.Log("SalesShortcutSettingsSave", error);
                SetStatus(Present("تعذر حفظ الاختصارات. بقيت الاختصارات السابقة؛ حاول مجددًا."), true);
            }
        }
    }
}

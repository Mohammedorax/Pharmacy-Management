using System;
using System.Drawing;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace project_fo_3
{
    internal sealed class AppNotificationForm : Form
    {
        private readonly DialogResult dismissResult;
        private readonly DialogResult enterResult;
        private readonly string originalMessage;
        private readonly bool leftToRight;
        private Action<string> copyTextWriter = Clipboard.SetText;

        internal AppNotificationForm(string message, string caption, MessageBoxButtons buttons,
            MessageBoxIcon icon, MessageBoxDefaultButton defaultButton)
            : this(message, caption, buttons, icon, defaultButton, UiLanguageCatalog.IsEnglishActive) { }

        // Controlled presentation preview follows the same pattern as existing settings/report
        // previews. It does not enable or persist the application-wide English language.
        internal AppNotificationForm(string message, string caption, MessageBoxButtons buttons,
            MessageBoxIcon icon, MessageBoxDefaultButton defaultButton, bool forceLeftToRight)
        {
            leftToRight = forceLeftToRight;
            originalMessage = message ?? string.Empty;
            string messageBody, supportCodeValue;
            bool hasSupportCode = TrySplitSupportCode(originalMessage, out messageBody, out supportCodeValue);
            Text = caption ?? string.Empty;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(560, 300);
            var content = new Panel { Tag = "UiSurface", Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(20) };
            var title = new Label { Name = "notificationTitle", Text = Text, Dock = DockStyle.Top, AutoSize = true,
                MaximumSize = new Size(500, 0), Padding = new Padding(0, 0, 0, 12), Font = AppFonts.Create(13F, FontStyle.Bold) };
            var text = new Label { Name = "notificationMessage", Text = messageBody, AutoSize = true, MaximumSize = new Size(500, 0), Dock = DockStyle.Top };
            text.AccessibleName = messageBody;
            Label supportCode = null;
            if (hasSupportCode)
            {
                supportCode = new Label { Name = "notificationSupportCode", Text = supportCodeValue,
                    AutoSize = true, Dock = DockStyle.Top, MaximumSize = new Size(500, 0),
                    Padding = new Padding(8, 4, 8, 8), BorderStyle = BorderStyle.FixedSingle,
                    TextAlign = ContentAlignment.MiddleLeft, RightToLeft = RightToLeft.No };
                content.Controls.Add(supportCode);
            }
            var severity = new Label { Name = "notificationSeverity", Text = SeverityCaption(icon), Dock = DockStyle.Top,
                AutoSize = true, Padding = new Padding(6), Visible = icon != MessageBoxIcon.None };
            severity.AccessibleName = severity.Text;
            content.Controls.Add(text);
            content.Controls.Add(title);
            content.Controls.Add(severity);
            var actions = new FlowLayoutPanel { Name = "notificationActions", Tag = "UiSurface", Dock = DockStyle.Bottom,
                Height = 76, Padding = new Padding(12), FlowDirection = FlowDirection.RightToLeft, AutoScroll = true };
            DialogResult[] results = Results(buttons);
            dismissResult = AppNotifications.DismissResult(buttons);
            int selected = Math.Min(results.Length - 1, (int)defaultButton / 256);
            enterResult = results[Math.Max(0, selected)];
            Control selectedControl = null;
            foreach (DialogResult result in results)
            {
                DialogResult choice = result;
                var action = new AccessibleAntdButton { Name = "notification" + result, Text = Caption(result), DialogResult = result };
                DesktopComponents.StyleOutlinedFooterAction(action);
                action.Size = new Size(120, DesktopDesignTokens.ActionHeight);
                action.AccessibleName = action.Text;
                action.Click += (sender, args) => { DialogResult = choice; Close(); };
                actions.Controls.Add(action);
                if (result == enterResult) { selectedControl = action; AcceptButton = action; }
                if (result == dismissResult) CancelButton = action;
            }
            var copy = new AccessibleAntdButton { Name = "notificationCopy", Text = Localize("نسخ التفاصيل"),
                Size = new Size(120, DesktopDesignTokens.ActionHeight), DialogResult = DialogResult.None };
            copy.AccessibleName = copy.Text;
            DesktopComponents.StyleOutlinedFooterAction(copy);
            Action copyDetails = () => CopyToClipboard(copy, BuildCopyText());
            copy.Click += (sender, args) => copyDetails();
            actions.Controls.Add(copy);
            AccessibleAntdButton copyCode = null;
            if (hasSupportCode)
            {
                copyCode = new AccessibleAntdButton { Name = "notificationCopySupportCode", Text = Localize("نسخ الرمز"),
                    Size = new Size(120, DesktopDesignTokens.ActionHeight), DialogResult = DialogResult.None };
                copyCode.AccessibleName = copyCode.Text;
                DesktopComponents.StyleOutlinedFooterAction(copyCode);
                copyCode.Click += (sender, args) => CopyToClipboard(copyCode, supportCodeValue);
                actions.Controls.Add(copyCode);
            }
            KeyPreview = true;
            KeyDown += (sender, args) =>
            {
                if (args.Control && args.KeyCode == Keys.C)
                { args.Handled = args.SuppressKeyPress = true; copyDetails(); }
            };
            Controls.Add(content);
            Controls.Add(actions);
            UiTheme.UseDedicatedStatus(this);
            UiTheme.Apply(this);
            Text = title.Text = caption ?? string.Empty;
            AccessibleName = title.AccessibleName = Text;
            text.Text = messageBody;
            text.AccessibleName = messageBody;
            foreach (DialogResult result in results)
            {
                var action = actions.Controls["notification" + result];
                action.Text = Localize(Caption(result));
                action.AccessibleName = action.Text;
            }
            copy.Text = Localize("نسخ التفاصيل");
            copy.AccessibleName = copy.Text;
            severity.Text = Localize(SeverityCaption(icon));
            severity.AccessibleName = severity.Text;
            UiDirection.Apply(this, leftToRight);
            if (supportCode != null)
            {
                // Direction belongs to the code field, never to hidden marks in the copied data.
                supportCode.RightToLeft = RightToLeft.No;
                supportCode.TextAlign = ContentAlignment.MiddleLeft;
                supportCode.AccessibleName = Localize("رمز الدعم") + ": " + supportCodeValue;
                supportCode.BackColor = UiTheme.SurfaceVariant;
                supportCode.ForeColor = UiTheme.Text;
                var codeFont = AppFonts.Create(11F, FontStyle.Bold);
                supportCode.Font = codeFont;
                supportCode.Disposed += (sender, args) => codeFont.Dispose();
                copyCode.Text = Localize("نسخ الرمز");
                copyCode.AccessibleName = copyCode.Text;
            }
            actions.RightToLeft = RightToLeft.No;
            actions.FlowDirection = leftToRight ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;
            title.ForeColor = UiTheme.Text;
            severity.ForeColor = icon == MessageBoxIcon.Error ? UiTheme.Danger : UiTheme.Text;
            severity.BackColor = icon == MessageBoxIcon.Warning ? UiTheme.WarningSurface : UiTheme.SurfaceVariant;
            bool arranging = false;
            Action layout = () =>
            {
                if (arranging) return;
                arranging = true;
                try
                {
                    int width = Math.Max(1, content.ClientSize.Width - content.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth);
                    text.MaximumSize = title.MaximumSize = new Size(width, 0);
                    if (supportCode != null) supportCode.MaximumSize = new Size(width, 0);
                    int available = Math.Max(1, actions.ClientSize.Width - actions.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth);
                    int used = 0, rowHeight = 0, totalHeight = 0;
                    foreach (Control action in actions.Controls)
                    {
                        int measured = TextRenderer.MeasureText(action.Text, action.Font, Size.Empty,
                            TextFormatFlags.SingleLine | TextFormatFlags.NoPadding).Width;
                        // Reserve feedback text before copying, so the footer does not jump.
                        if (action == copy || action == copyCode)
                            foreach (string feedback in new[] { Localize(action == copy ? "نسخ التفاصيل" : "نسخ الرمز"), Localize("تم النسخ"), Localize("تعذر النسخ") })
                                measured = Math.Max(measured, TextRenderer.MeasureText(feedback, action.Font, Size.Empty,
                                    TextFormatFlags.SingleLine | TextFormatFlags.NoPadding).Width);
                        action.Size = new Size(Math.Min(Math.Max(120, measured + 32), Math.Max(1, available - action.Margin.Horizontal)),
                            Math.Max(DesktopDesignTokens.ActionHeight, action.Font.Height + 20));
                        int itemWidth = action.Width + action.Margin.Horizontal;
                        if (used > 0 && used + itemWidth > available)
                        { totalHeight += rowHeight; used = 0; rowHeight = 0; }
                        used += itemWidth;
                        rowHeight = Math.Max(rowHeight, action.Height + action.Margin.Vertical);
                    }
                    actions.Height = actions.Padding.Vertical + totalHeight + rowHeight;
                }
                finally { arranging = false; }
            };
            actions.SizeChanged += (sender, args) => layout();
            foreach (Control action in actions.Controls) action.FontChanged += (sender, args) => layout();
            Resize += (sender, args) => layout();
            Load += (sender, args) =>
            {
                Rectangle area = Owner == null ? Screen.FromControl(this).WorkingArea : Screen.FromControl(Owner).WorkingArea;
                int availableWidth = Math.Max(1, area.Width - 32);
                Width = Math.Min(Width, availableWidth);
                layout();
                int contentHeight = TextRenderer.MeasureText(text.Text, text.Font,
                    new Size(Math.Max(1, text.MaximumSize.Width), int.MaxValue), TextFormatFlags.WordBreak).Height;
                ClientSize = new Size(ClientSize.Width, Math.Max(180, Math.Min(480,
                    contentHeight + (supportCode == null ? 0 : supportCode.PreferredHeight) + title.PreferredHeight + (icon != MessageBoxIcon.None ? severity.PreferredHeight : 0)
                    + content.Padding.Vertical + actions.Height + 12)));
                Height = Math.Min(Height, Math.Max(1, area.Height - 32));
                layout();
            };
            layout();
            Shown += (sender, args) => { if (selectedControl != null) selectedControl.Focus(); };
            FormClosing += (sender, args) =>
            {
                // WinForms assigns Cancel before closing a modal window with X.
                // Map that implicit result to an action actually available in this dialog.
                if (DialogResult == DialogResult.None || Array.IndexOf(results, DialogResult) < 0)
                    DialogResult = dismissResult;
            };
        }

        private static DialogResult[] Results(MessageBoxButtons buttons)
        {
            switch (buttons)
            {
                case MessageBoxButtons.YesNo: return new[] { DialogResult.Yes, DialogResult.No };
                case MessageBoxButtons.YesNoCancel: return new[] { DialogResult.Yes, DialogResult.No, DialogResult.Cancel };
                case MessageBoxButtons.OKCancel: return new[] { DialogResult.OK, DialogResult.Cancel };
                case MessageBoxButtons.RetryCancel: return new[] { DialogResult.Retry, DialogResult.Cancel };
                case MessageBoxButtons.AbortRetryIgnore: return new[] { DialogResult.Abort, DialogResult.Retry, DialogResult.Ignore };
                default: return new[] { DialogResult.OK };
            }
        }

        internal string BuildCopyText() => Text + Environment.NewLine + Environment.NewLine + originalMessage;

        private static bool TrySplitSupportCode(string message, out string body, out string code)
        {
            body = message;
            code = null;
            // Only a labeled terminal diagnostic identifier is moved. Business references,
            // malformed dates and any trailing original data remain in the ordinary message.
            var match = Regex.Match(message,
                @"(?:رمز الدعم|support code)(?:\s+(?:إلى\s+مسؤول\s+النظام|to\s+(?:your\s+)?administrator))?\s*[:：]\s*(?<code>[0-9]{8}-[0-9]{6}-[a-f0-9]{8})\s*\z",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (!match.Success) return false;
            var value = match.Groups["code"];
            DateTime timestamp;
            if (!DateTime.TryParseExact(value.Value.Substring(0, 15), "yyyyMMdd-HHmmss",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out timestamp)) return false;
            code = value.Value;
            body = message.Substring(0, match.Index).TrimEnd();
            return true;
        }

        private void CopyToClipboard(AccessibleAntdButton action, string value)
        {
            try
            {
                copyTextWriter(value);
                action.Text = Localize("تم النسخ");
                action.ForeColor = UiTheme.Text;
            }
            catch (Exception error) when (error is System.Runtime.InteropServices.ExternalException ||
                error is System.Threading.ThreadStateException)
            {
                action.Text = Localize("تعذر النسخ");
                action.ForeColor = UiTheme.Danger;
            }
            action.AccessibleName = Localize(action.Name == "notificationCopySupportCode" ? "نسخ الرمز" : "نسخ التفاصيل");
            action.AccessibleDescription = action.Text;
        }

        private string Localize(string arabic) => leftToRight
            ? UiLanguageCatalog.Translate(arabic) : arabic;

        private static string Caption(DialogResult result)
        {
            switch (result)
            {
                case DialogResult.Yes: return "نعم";
                case DialogResult.No: return "لا";
                case DialogResult.Cancel: return "إلغاء";
                case DialogResult.Retry: return "إعادة المحاولة";
                case DialogResult.Abort: return "إيقاف";
                case DialogResult.Ignore: return "تجاهل";
                default: return "موافق";
            }
        }

        private static string SeverityCaption(MessageBoxIcon icon)
        {
            switch (icon)
            {
                case MessageBoxIcon.Error: return "خطأ";
                case MessageBoxIcon.Warning: return "تحذير";
                case MessageBoxIcon.Question: return "تأكيد";
                case MessageBoxIcon.Information: return "تنبيه";
                default: return string.Empty;
            }
        }
    }
}

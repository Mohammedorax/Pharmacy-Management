using System.Drawing;
using System.Windows.Forms;

namespace project_fo_3
{
    internal sealed class ReportTemplateNameDialog : Form
    {
        private readonly TextBox nameBox = new TextBox { Name = "reportTemplateName" };
        private readonly AntdUI.Panel nameFrame;
        private readonly string reportTitle;
        private readonly Panel header = new Panel { Name = "reportTemplateHeader" };
        private readonly AntdUI.Panel body = DesktopComponents.Card("reportTemplateBody");
        private readonly Label title = new Label { Name = "reportTemplateTitle" };
        private readonly Label subtitle = new Label { Name = "reportTemplateSubtitle" };
        private readonly Label nameLabel = new Label { Name = "reportTemplateNameLabel" };
        private readonly FlowLayoutPanel actions = new FlowLayoutPanel { Name = "reportTemplateActions" };
        private readonly AntdUI.Button save;
        private readonly AntdUI.Button close;
        private bool leftToRight;
        internal bool PresentationLeftToRight => leftToRight;

        internal ReportTemplateNameDialog(string reportTitle) : this(reportTitle, UiDirection.IsLeftToRight) { }

        internal ReportTemplateNameDialog(string reportTitle, bool useLeftToRight)
        {
            this.reportTitle = reportTitle ?? string.Empty;
            leftToRight = useLeftToRight;
            nameFrame = DesktopComponents.NativeTextFrame(nameBox, "reportTemplateNameFrame");
            nameBox.TextChanged += (sender, args) => UiDirection.ApplyContentDirection(nameBox, leftToRight);
            Text = "حفظ قالب التقرير";
            ClientSize = new Size(460, 300);
            MinimumSize = new Size(400, 320);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = true;
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;

            header.Dock = DockStyle.Top; header.Height = 72; header.Padding = new Padding(20, 10, 20, 8);
            title.Dock = DockStyle.Top; title.Height = 30; title.Text = Localize("حفظ قالب: ") + reportTitle;
            title.AutoEllipsis = true;
            title.TextAlign = ContentAlignment.MiddleRight; title.Font = AppFonts.Create(13F, FontStyle.Bold);
            subtitle.Dock = DockStyle.Fill; subtitle.Text = Localize("يحفظ هذا القالب الفلاتر والخيارات فقط، وليس بيانات التقرير.");
            subtitle.TextAlign = ContentAlignment.MiddleRight;
            header.Controls.Add(subtitle);
            header.Controls.Add(title);

            body.Dock = DockStyle.Fill; body.Padding = new Padding(24, 12, 24, 12); body.BackColor = UiTheme.Surface;
            nameLabel.Dock = DockStyle.Top; nameLabel.Height = 26; nameLabel.Text = Localize("اسم القالب");
            nameLabel.TextAlign = ContentAlignment.MiddleRight; nameLabel.Font = AppFonts.Create(9.5F, FontStyle.Bold);
            nameFrame.Dock = DockStyle.Top;
            nameBox.MaxLength = 60;
            nameBox.AccessibleName = "اسم قالب التقرير";
            actions.Tag = "UiSurface"; actions.Dock = DockStyle.Bottom; actions.Height = 68; actions.WrapContents = false;
            actions.Padding = new Padding(20, 10, 20, 8);
            save = DesktopComponents.Action("saveReportTemplate", "حفظ", AppIcon.Save);
            close = DesktopComponents.Action("cancelReportTemplate", "إلغاء", AppIcon.Close);
            save.Size = close.Size = new Size(118, 44);
            save.Click += (sender, args) => SaveTemplate();
            close.Click += (sender, args) => { DialogResult = DialogResult.Cancel; Close(); };
            actions.Controls.Add(save);
            actions.Controls.Add(close);
            body.Controls.Add(nameFrame);
            body.Controls.Add(nameLabel);
            Controls.Add(body);
            Controls.Add(actions);
            Controls.Add(header);
            KeyPreview = true;
            KeyDown += (sender, args) =>
            {
                if (args.KeyCode == Keys.Enter) { SaveTemplate(); args.Handled = args.SuppressKeyPress = true; }
                else if (args.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); args.Handled = args.SuppressKeyPress = true; }
            };
            UiTheme.Apply(this);
            UiTheme.ApplyTo(nameBox);
            ApplyPresentation(leftToRight);
            Resize += (sender, args) => LayoutHeader();
        }

        private string Localize(string arabicText)
        {
            return leftToRight ? UiLanguageCatalog.Translate(arabicText) : arabicText;
        }

        internal void ApplyPresentation(bool useLeftToRight)
        {
            leftToRight = useLeftToRight;
            Text = Localize("حفظ قالب التقرير");
            title.Text = Localize("حفظ قالب") + ": " + reportTitle;
            subtitle.Text = Localize("يحفظ هذا القالب الفلاتر والخيارات فقط، وليس بيانات التقرير.");
            nameLabel.Text = Localize("اسم القالب");
            nameBox.AccessibleName = Localize("اسم قالب التقرير");
            header.AccessibleName = Text;
            body.AccessibleName = nameFrame.AccessibleName = nameBox.AccessibleName;
            actions.AccessibleName = Localize("إجراءات التقرير");
            save.Text = Localize("حفظ");
            save.AccessibleName = Localize("حفظ قالب التقرير");
            close.Text = Localize("إلغاء");
            close.AccessibleName = Localize("إلغاء حفظ قالب التقرير");
            UiDirection.Apply(this, leftToRight);
            UiTheme.ApplyStatusStripPresentation(this, leftToRight);
            UiDirection.ApplyContentDirection(nameBox, leftToRight);
            actions.RightToLeft = RightToLeft.No;
            actions.FlowDirection = leftToRight ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;
            UiTheme.ApplyDialogHeader(header, title, subtitle, null);
            header.BackColor = UiTheme.Background;
            title.ForeColor = UiTheme.Text;
            subtitle.ForeColor = UiTheme.MutedText;
            AntdThemeAdapter.Apply(body);
            body.Shadow = 0;
            DesktopComponents.StyleNativeTextFrame(nameFrame, nameBox);
            DesktopComponents.StyleOutlinedFooterAction(save);
            DesktopComponents.StyleOutlinedFooterAction(close);
            LayoutHeader();
        }

        private void LayoutHeader()
        {
            title.Height = 42;
            int width = System.Math.Max(1, ClientSize.Width - header.Padding.Horizontal);
            int hintHeight = TextRenderer.MeasureText(subtitle.Text, subtitle.Font,
                new Size(width, int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPadding).Height;
            header.Height = header.Padding.Vertical + title.Height + hintHeight;
        }

        private void SaveTemplate()
        {
            nameBox.Text = (nameBox.Text ?? string.Empty).Trim();
            if (nameBox.Text.Length == 0)
            {
                AppNotifications.Show(this, Localize("أدخل اسمًا للقالب."), Text,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                nameBox.Focus();
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        }

        internal static bool TryGetName(IWin32Window owner, string reportTitle, out string name)
        {
            return TryGetName(owner, reportTitle, out name, UiDirection.IsLeftToRight);
        }

        internal static bool TryGetName(IWin32Window owner, string reportTitle, out string name, bool leftToRight)
        {
            using (ReportTemplateNameDialog dialog = new ReportTemplateNameDialog(reportTitle, leftToRight))
            {
                if (dialog.ShowDialog(owner) == DialogResult.OK)
                {
                    name = dialog.nameBox.Text;
                    return true;
                }
            }

            name = null;
            return false;
        }
    }
}

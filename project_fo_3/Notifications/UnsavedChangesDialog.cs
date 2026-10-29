using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace project_fo_3
{
    // Closing the dialog or pressing Escape/Enter keeps the editor open.
    // Discard requires choosing the explicitly labelled action.
    internal sealed class UnsavedChangesDialog : Form
    {
        private readonly bool leftToRight;

        internal UnsavedChangesDialog(bool english)
        {
            leftToRight = english;
            Name = "UnsavedChangesDialog";
            Text = Present("تعديلات غير محفوظة");
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96F, 96F);
            ClientSize = new Size(500, 240);
            MinimumSize = new Size(430, 280);
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = false;
            MinimizeBox = false;
            Font = AppFonts.Create(10F);

            var body = DesktopComponents.Card("unsavedChangesCard");
            body.Dock = DockStyle.Fill;
            body.Padding = new Padding(20);
            var message = new Label { Name = "unsavedChangesMessage", Dock = DockStyle.Fill,
                Text = Present("لديك تعديلات غير محفوظة. يمكنك متابعة التحرير أو الإغلاق وتجاهل هذه التعديلات."),
                AutoEllipsis = false, TextAlign = ContentAlignment.MiddleLeft };
            message.AccessibleName = message.Text;
            body.Controls.Add(message);
            var actions = new FlowLayoutPanel { Name = "unsavedChangesActions", Dock = DockStyle.Bottom,
                Height = 76, Padding = new Padding(16, 12, 16, 12), WrapContents = false };
            var keep = DesktopComponents.Action("keepEditing", Present("متابعة التحرير"), AppIcon.Back);
            var discard = DesktopComponents.Action("discardChanges", Present("تجاهل التعديلات"), AppIcon.Close);
            keep.Size = discard.Size = new Size(174, DesktopDesignTokens.ActionHeight);
            keep.TabIndex = 0;
            discard.TabIndex = 1;
            keep.DialogResult = DialogResult.Cancel;
            discard.DialogResult = DialogResult.OK;
            keep.Click += (sender, args) => { DialogResult = DialogResult.Cancel; Close(); };
            discard.Click += (sender, args) => { DialogResult = DialogResult.OK; Close(); };
            actions.Controls.Add(keep);
            actions.Controls.Add(discard);
            Controls.Add(body);
            Controls.Add(actions);
            UiTheme.Apply(this);
            UiDirection.Apply(this, english);
            message.RightToLeft = english ? RightToLeft.No : RightToLeft.Yes;
            message.TextAlign = ContentAlignment.MiddleLeft;
            message.BackColor = UiTheme.Surface;
            message.ForeColor = UiTheme.Text;
            actions.RightToLeft = RightToLeft.No;
            actions.BackColor = UiTheme.Background;
            actions.FlowDirection = english ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;
            keep.IconPosition = discard.IconPosition = english ? AntdUI.TAlignMini.Left : AntdUI.TAlignMini.Right;
            keep.IconSvg = OutlinedIcons.Svg(AppIcon.Back, !english);
            DesktopComponents.StyleOutlinedFooterAction(keep);
            DesktopComponents.StyleOutlinedFooterAction(discard);
            AcceptButton = keep;
            CancelButton = keep;
            ActiveControl = keep;
        }

        private string Present(string text) => leftToRight ? UiLanguageCatalog.Translate(text) : text;

        internal static bool ConfirmDiscard(Form owner, bool english)
        {
            if (owner == null || AppNotifications.IsOwnerUnavailable(owner)) return false;
            try
            {
                DialogResult result;
                using (var dialog = new UnsavedChangesDialog(english))
                    result = dialog.ShowDialog(owner);
                // Disposal callbacks can end the editor while the dialog finishes.
                // Only an explicit discard in a still-current editor is accepted.
                return !AppNotifications.IsOwnerUnavailable(owner) && result == DialogResult.OK;
            }
            catch (Exception error) when ((error is InvalidOperationException || error is ArgumentException || error is Win32Exception)
                && AppNotifications.IsOwnerUnavailable(owner))
            {
                return false;
            }
        }
    }
}

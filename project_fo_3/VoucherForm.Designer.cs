using System;
using System.Drawing;
using System.Windows.Forms;

namespace project_fo_3
{
    public partial class VoucherForm
    {
        private void BuildEditor()
        {
            AddVoucherField("التاريخ", voucherDate);
            AddVoucherField("الطرف", party);
            AddVoucherField("الحساب المقابل", account);
            AddVoucherField("المبلغ", amount);
            AddVoucherField("ملاحظات", notes);
            var save = new AccessibleAntdButton
            {
                Name = "legacyVoucherSave", Text = "حفظ السند", AccessibleName = "حفظ السند",
                Anchor = AnchorStyles.None, Size = new Size(144, 44), Font = AppFonts.Create(10.5F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter, Enabled = !voucherPreview,
                Margin = new Padding(8), Cursor = Cursors.Hand,
                IconSvg = OutlinedIcons.Svg(AppIcon.Save, true), IconSize = new Size(18, 18),
                IconPosition = UiDirection.IsLeftToRight ? AntdUI.TAlignMini.Left : AntdUI.TAlignMini.Right
            };
            DesktopComponents.StyleOutlinedFooterAction(save);
            save.Click += (s, e) => SaveVoucher();
            voucherStatus.Name = "legacyVoucherStatus";
            voucherStatus.Dock = DockStyle.Fill;
            voucherStatus.Font = AppFonts.Create(10F);
            voucherStatus.ForeColor = UiTheme.Text;
            voucherStatus.BackColor = UiTheme.Surface;
            voucherStatus.TextAlign = UiDirection.IsLeftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
            voucherStatus.Text = "أدخل بيانات السند ثم احفظه.";
            var actions = new TableLayoutPanel
            {
                Name = "legacyVoucherActions", Dock = DockStyle.Fill,
                ColumnCount = 4, RowCount = 1, BackColor = UiTheme.Surface,
                RightToLeft = UiDirection.IsLeftToRight ? RightToLeft.No : RightToLeft.Yes
            };
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120F));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 128F));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160F));
            actions.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            AttachAccountingCloseAction(actions);
            var close = AccountingCloseAction;
            close.Size = new Size(104, 44); close.Anchor = AnchorStyles.None; close.Margin = new Padding(8);
            actions.Controls.Add(close, 0, 0);
            actions.Controls.Add(voucherStatus, 1, 0);
            var refresh=new AccessibleAntdButton{Name="legacyVoucherRefresh",Text="تحديث",AccessibleName="تحديث",Font=AppFonts.Create(10.5F,FontStyle.Bold),TextAlign=ContentAlignment.MiddleCenter,IconSvg=OutlinedIcons.Svg(AppIcon.Refresh,true),IconSize=new Size(18,18)};
            refresh.Size=new Size(112,44);refresh.Anchor=AnchorStyles.None;refresh.Margin=new Padding(8);
            DesktopComponents.StyleOutlinedFooterAction(refresh);refresh.Click+=async(s,e)=>await LoadVoucherOptionsAsync();
            actions.Controls.Add(refresh,2,0);actions.Controls.Add(save,3,0);
            var footer = new AntdUI.Panel
            {
                Name = "legacyVoucherFooter", Dock = DockStyle.Bottom, Height = 84,
                Padding = new Padding(16, 8, 16, 8), BackColor = UiTheme.Surface,
                Radius = 0, Shadow = 0
            };
            footer.Controls.Add(actions);
            Controls.Add(footer);
        }

        private void AddVoucherField(string caption, Control input)
        {
            Control surface = input;
            if (input is TextBox text)
            {
                text.Dock = DockStyle.None; text.MinimumSize = Size.Empty;
                surface = DesktopComponents.NativeTextFrame(text, input.Name + "Frame");
            }
            else if (input is ComboBox selector)
            {
                selector.Dock = DockStyle.None; selector.MinimumSize = Size.Empty;
                surface = DesktopComponents.NativeSelectFrame(selector, input.Name + "Frame", caption);
            }
            AddField(caption, surface);
            surface.Height = DesktopDesignTokens.InputHeight;
            surface.Parent.Height = 78;
        }
    }
}

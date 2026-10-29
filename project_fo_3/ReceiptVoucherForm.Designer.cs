using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace project_fo_3
{
    public partial class ReceiptVoucherForm
    {
        private void InitializeComponent()
        {
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;

            Panel header = new Panel { Name = "receiptVoucherHeader", Dock = DockStyle.Top, Height = 58, BackColor = UiTheme.Background, RightToLeft = RightToLeft.Yes };
            Label title = new Label
            {
                Name = "receiptVoucherScreenTitle",
                Text = Text,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = UiTheme.Background,
                ForeColor = UiTheme.Text,
                Font = AppFonts.Create(12F, FontStyle.Bold)
            };
            header.Controls.Add(title);

            Panel side = new Panel { Name = "receiptVoucherSide", Tag = "UiSurface", Dock = DockStyle.Right, Width = 315, Padding = new Padding(14), BackColor = UiTheme.Background, RightToLeft = RightToLeft.Yes };
            side.AutoScroll = false;
            voucherSideContent.Name = "receiptVoucherSideContent";
            voucherSideContent.Tag = "UiSurface";
            voucherSideContent.BackColor = UiTheme.Surface;
            voucherSideContent.Controls.Add(BuildChequePanel());
            voucherSideContent.Controls.Add(new Panel { Name = "receiptVoucherSideGap", Dock = DockStyle.Top, Height = 16, BackColor = UiTheme.Background, Visible = false });
            voucherSideContent.Controls.Add(BuildBalancePanel());
            voucherSideViewport.Name = "receiptVoucherSideViewport";
            voucherSideViewport.Dock = DockStyle.Fill;
            voucherSideViewport.AttachContent(voucherSideContent);
            voucherSideViewport.Resize += (s, e) => UpdateVoucherSideContentHeight();
            side.Controls.Add(voucherSideViewport);

            Panel main = new Panel { Name = "receiptVoucherMain", Tag = "UiSurface", Dock = DockStyle.Fill, Padding = new Padding(10), BackColor = UiTheme.Background, RightToLeft = RightToLeft.Yes };
            TableLayoutPanel form = voucherEditor;
            form.SuspendLayout();
            form.Name = "receiptVoucherForm";
            form.TabIndex = 0;
            form.Tag = "UiSurface";
            form.Dock = DockStyle.Top;
            form.Height = 328;
            form.ColumnCount = 4;
            form.RowCount = 6;
            form.BackColor = UiTheme.Surface;
            form.Padding = new Padding(14);
            form.RightToLeft = RightToLeft.Yes;
            for (int i = 0; i < 4; i++) form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            for (int i = 0; i < 6; i++) form.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));
            WinFormsShapeAdapter.ApplyCard(form);

            branch.Items.Add("الفرع الرئيسي");
            branch.SelectedIndex = 0;
            exchangeRate.Text = "1";
            localValue.Text = "0";
            localValue.ReadOnly = true;
            voucherNo.Text = "جديد";
            voucherNo.Text = GetNextVoucherNumberText();
            voucherNo.ReadOnly = true;
            representative.Items.Add(SessionContext.CurrentUser);
            representative.SelectedIndex = 0;

            AddCell(form, "الفرع", branch, 0, 0);
            AddCell(form, "رقم السند", voucherNo, 1, 0);
            AddCell(form, "التاريخ", voucherDate, 2, 0);
            AddCell(form, "مركز التكلفة", costCenter, 3, 0);
            AddWideCell(form, GetPartyNameLabel(), partyName, 0, 1, 2);
            AddWideCell(form, "الحساب المقابل", partyAccount, 2, 1, 2);
            AddWideCell(form, "المبلغ", amount, 0, 2, 2);
            AddCell(form, "القيمة محلي", localValue, 2, 2);
            AddCell(form, "سعر التحويل", exchangeRate, 3, 2);
            AddCell(form, "العملة", currency, 0, 3);
            AddWideCell(form, isReceipt ? "يودع في حساب" : "يصرف من حساب", settlementAccount, 1, 3, 2);
            AddCell(form, "المندوب", representative, 3, 3);
            AddWideCell(form, "البيان", notes, 0, 4, 4);

            FlowLayoutPanel methods = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(0, 8, 0, 0)
            };
            methods.Controls.Add(CreateMethodButton("حوالة", "Transfer", false));
            methods.Controls.Add(CreateMethodButton("شيك", "Cheque", false));
            methods.Controls.Add(CreateMethodButton("كاش", "Cash", true));
            paymentMethodsContainer = methods;
            form.Controls.Add(methods, 0, 5);
            form.SetColumnSpan(methods, 4);
            form.ResumeLayout(true);

            history.Dock = DockStyle.Fill;
            history.ReadOnly = true;
            history.AllowUserToAddRows = false;
            history.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            history.Name = "receiptVoucherHistory";
            history.TabIndex = 1;
            history.BackgroundColor = UiTheme.Surface;
            history.BorderStyle = BorderStyle.None;
            history.SelectionChanged += (s, e) => CaptureSelectedHistoryVoucher();
            history.CellDoubleClick += (s, e) => CaptureSelectedHistoryVoucher();
            UiTheme.ApplyDataGridQuality(history);

              Label historyTitle = new Label
              {
                  Name = "receiptVoucherHistoryTitle",
                  Text = "آخر السندات",
                Dock = DockStyle.Top,
                Height = 26,
                TextAlign = ContentAlignment.MiddleRight,
                Font = AppFonts.Create(10F, FontStyle.Bold),
                ForeColor = UiTheme.PrimaryDark,
                BackColor = UiTheme.Background
            };

            statusLabel.Name = "receiptVoucherStatus";
            statusLabel.Dock = DockStyle.Bottom;
            statusLabel.Height = 20;
            statusLabel.TextAlign = ContentAlignment.MiddleRight;
            statusLabel.ForeColor = UiTheme.MutedText;
            statusLabel.BackColor = UiTheme.Background;

            voucherContentHost.Name = "receiptVoucherContentHost";
            voucherContentHost.Tag = "UiSurface";
            voucherContentHost.Dock = DockStyle.Top;
            voucherContentHost.Controls.Add(history);
            voucherContentHost.Controls.Add(historyTitle);
            voucherContentHost.Controls.Add(form);
            voucherContentViewport.Name = "receiptVoucherContentViewport";
            voucherContentViewport.Tag = "UiSurface";
            voucherContentViewport.Dock = DockStyle.Fill;
            voucherContentViewport.AutoScroll = true;
            voucherContentViewport.AttachContent(voucherContentHost);
            voucherContentViewport.Resize += (s, e) => UpdateVoucherContentHeight();
            main.Controls.Add(voucherContentViewport);
            main.Controls.Add(statusLabel);
            Controls.Add(main);
            Controls.Add(side);
            Controls.Add(header);
            Controls.Add(BuildBottomToolbar());

            amount.TextChanged += (s, e) => UpdateLocalValue();
            exchangeRate.TextChanged += (s, e) => UpdateLocalValue();
            partyAccount.SelectedIndexChanged += (s, e) => UpdateBalance();
            voucherDate.ValueChanged += (s, e) => UpdateBalance();
            voucherDate.TextChanged += (s, e) => UpdateBalance();
            SetPaymentMethod("Cash", methods);
        }
    }
}

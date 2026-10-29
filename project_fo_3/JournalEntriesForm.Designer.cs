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
    public partial class JournalEntriesForm
    {
        private void InitializeComponent()
        {
            Panel titleBar = new Panel { Name = "journalTitleBar", Dock = DockStyle.Top, Height = 52,
                Tag = "UiSurface", BackColor = UiTheme.Surface };
            Label title = new Label
            {
                Name = "journalScreenTitle",
                Text = formTitle,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = AppFonts.Create(16F, FontStyle.Bold),
                ForeColor = UiTheme.PrimaryDark
            };
            titleBar.Controls.Add(title);

            TableLayoutPanel header = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 154,
                ColumnCount = 6,
                RowCount = 2,
                Padding = new Padding(14, 8, 14, 8),
                Name = "journalHeaderFields",
                Tag = "UiSurface", BackColor = UiTheme.Surface
            };
            for (int i = 0; i < 6; i++) header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16.666F));
            header.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));
            header.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));

            branch.Items.Add("الفرع الرئيسي");
            branch.SelectedIndex = 0;
            attachmentType.Items.Add("دفتر قيد يومية");
            attachmentType.Items.Add("مرفق عام");
            attachmentType.Items.Add("بدون");
            attachmentType.SelectedIndex = 0;
            entryStatus.ReadOnly = true;
            entryStatus.Text = "يُعتمد عند الحفظ";
            entryStatus.AccessibleDescription = "حفظ القيد يعتمد العملية؛ هذه الشاشة لا تحفظ مسودة محاسبية مستقلة.";
            entryNo.ReadOnly = true;
            headerDescription.Text = string.Empty;

            AddCell(header, "الفرع", branch, 0, 0);
            AddCell(header, "رقم القيد", entryNo, 1, 0);
            AddCell(header, "تاريخ القيد", entryDate, 2, 0);
            AddCell(header, "حالة القيد", entryStatus, 3, 0);
            AddWideCell(header, "البيان", headerDescription, 4, 0, 2);
            header.Controls.Add(repeatDescription, 0, 1);

            TableLayoutPanel linePanel = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 170,
                ColumnCount = 6,
                RowCount = 2,
                Padding = new Padding(14, 10, 14, 10),
                Name = "journalLineFields",
                Tag = "UiSurface", BackColor = UiTheme.Surface
            };
            for (int i = 0; i < 6; i++) linePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16.666F));
            linePanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
            linePanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));

            costCenter.Items.Add("بدون");
            costCenter.SelectedIndex = 0;
            exchangeRate.Text = "1";
            localValue.ReadOnly = true;
            lineDate.Text = DateTime.Today.ToString("yyyy/MM/dd");

            AddCell(linePanel, "رقم المرجع", referenceNo, 5, 0);
            AddCell(linePanel, "مركز التكلفة", costCenter, 4, 0);
            AddCell(linePanel, "دائن", credit, 3, 0);
            AddCell(linePanel, "مدين", debit, 2, 0);
            AddWideCell(linePanel, "رقم الحساب", account, 0, 0, 2);

            AntdUI.Button postLine = DesktopComponents.Action("postJournalLine", "تنزيل", AppIcon.Add, true);
            postLine.Dock = DockStyle.None;
            postLine.Anchor = AnchorStyles.None;
            postLine.Margin = new Padding(6, 4, 6, 4);
            postLine.AccessibleName = "تنزيل سطر القيد";
            postLine.AccessibleDescription = "إضافة السطر الحالي إلى القيد";
            postLine.Click += (s, e) => AddLine();
            linePanel.Controls.Add(postLine, 5, 1);

            AddCell(linePanel, "التاريخ", lineDate, 4, 1);
            AddCell(linePanel, "القيمة محلي", localValue, 3, 1);
            AddCell(linePanel, "سعر التحويل", exchangeRate, 2, 1);
            AddCell(linePanel, "العملة", currency, 1, 1);
            AddWideCell(linePanel, "البيان", lineDescription, 0, 1, 1);

            linesGrid.Dock = DockStyle.Fill;
            linesGrid.DataSource = lines;
            linesGrid.AllowUserToAddRows = false;
            linesGrid.ReadOnly = true;
            linesGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            linesGrid.Name = "journalLinesGrid";
            linesGrid.BackgroundColor = UiTheme.Surface;
            linesGrid.EnableHeadersVisualStyles = false;
            linesGrid.ColumnHeadersDefaultCellStyle.BackColor = UiTheme.PrimaryDark;
            linesGrid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            linesGrid.ColumnHeadersDefaultCellStyle.Font = AppFonts.Create(9F, FontStyle.Bold);
            linesGrid.DataBindingComplete += (s, e) => ApplyJournalGridPresentation();
            linesGrid.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex >= 0 && e.ColumnIndex >= 0 && linesGrid.Columns[e.ColumnIndex].Name == "Action") RemoveSelectedLine();
            };
            UiTheme.ApplyDataGridQuality(linesGrid);

            Panel footerInfo = new Panel { Name = "journalFooterInfo", Dock = DockStyle.Bottom, Height = 28,
                Tag = "UiSurface", BackColor = UiTheme.Surface };
            totalsLabel.Dock = DockStyle.Right;
            totalsLabel.Name = "journalTotals";
            totalsLabel.Width = 520;
            totalsLabel.TextAlign = ContentAlignment.MiddleRight;
            totalsLabel.Font = AppFonts.Create(10F, FontStyle.Bold);
            totalsLabel.ForeColor = UiTheme.Text;
            totalsLabel.Padding = new Padding(8, 0, 8, 0);
            statusLabel.Dock = DockStyle.Fill;
            statusLabel.Name = "journalStatus";
            statusLabel.TextAlign = ContentAlignment.MiddleRight;
            statusLabel.ForeColor = UiTheme.MutedText;
            statusLabel.Padding = new Padding(8, 0, 8, 0);
            footerInfo.Controls.Add(statusLabel);
            footerInfo.Controls.Add(totalsLabel);

            Controls.Add(linesGrid);
            Controls.Add(footerInfo);
            journalEditorViewport.Controls.Add(linePanel);
            journalEditorViewport.Controls.Add(header);
            Controls.Add(journalEditorViewport);
            Controls.Add(titleBar);
            Controls.Add(BuildJournalToolbar());

            debit.TextChanged += (s, e) => UpdateLocalValue();
            credit.TextChanged += (s, e) => UpdateLocalValue();
            exchangeRate.TextChanged += (s, e) => UpdateLocalValue();
            currency.SelectedIndexChanged += (s, e) => UpdateLocalValue();
        }
    }
}

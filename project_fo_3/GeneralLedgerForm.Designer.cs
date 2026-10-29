using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace project_fo_3
{
    public partial class GeneralLedgerForm
    {
        private void InitializeComponent()
        {
            Panel top = new Panel { Name = "generalLedgerTop", Dock = DockStyle.Top, Height = 332,
                Tag = "UiSurface", BackColor = UiTheme.Surface };

            Label title = new Label
            {
                Name = "generalLedgerTitle", Text = "حركة حساب خلال فترة", Tag = "حركة حساب خلال فترة",
                Dock = DockStyle.Top,
                Height = 44,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = AppFonts.Create(17F, FontStyle.Bold),
                ForeColor = UiTheme.PrimaryDark
            };

            TableLayoutPanel filters = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 176,
                ColumnCount = 4,
                RowCount = 2,
                Padding = new Padding(18, 8, 18, 8),
                Name = "generalLedgerFilters",
                Tag = "UiSurface",
                BackColor = UiTheme.Surface
            };
            for (int i = 0; i < 4; i++) filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            filters.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));
            filters.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));

            AddFilter(filters, "رقم الحساب", account, 0, 0);
            AddFilter(filters, "مركز التكلفة", costCenter, 1, 0);
            AddFilter(filters, "من تاريخ", fromDate, 2, 0);
            AddFilter(filters, "إلى تاريخ", toDate, 3, 0);
            AddFilter(filters, "ترتيب حسب", sortBy, 0, 1);
            AddFilter(filters, "نوع المستند", documentType, 1, 1);
            filters.Controls.Add(includeOpening, 2, 1);
            filters.Controls.Add(includePrevious, 3, 1);
            includeOpening.Dock = includePrevious.Dock = DockStyle.Bottom;
            includeOpening.Height = includePrevious.Height = DesktopDesignTokens.InputHeight;
            includeOpening.Margin = includePrevious.Margin = new Padding(11, 2, 11, 4);
            includeOpening.Padding = includePrevious.Padding = Padding.Empty;
            includeOpening.TextAlign = includePrevious.TextAlign = UiDirection.IsLeftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
            includeOpening.Font = includePrevious.Font = AppFonts.Create(9.5F, FontStyle.Regular);
            includeOpening.ForeColor = includePrevious.ForeColor = UiTheme.Text;

            Panel actionsHost = new Panel { Name = "generalLedgerActionsHost", Dock = DockStyle.Bottom,
                Height = 68, Padding = new Padding(18, 4, 18, 4), Tag = "UiSurface", BackColor = UiTheme.Surface };
            FlowLayoutPanel actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                Width = 568,
                FlowDirection = UiDirection.IsLeftToRight ? FlowDirection.LeftToRight : FlowDirection.RightToLeft,
                Padding = new Padding(6, 2, 6, 0),
                Name = "generalLedgerActions",
                Tag = "UiSurface",
                BackColor = Color.Transparent
            };
            actions.Controls.Add(CreateActionButton("عرض النتيجة", AppIcon.Search, LoadData));
            var generalLedgerPrint=CreateActionButton("طباعة", AppIcon.Print, PrintReport);
            generalLedgerPrint.Name="generalLedgerPrint"; actions.Controls.Add(generalLedgerPrint);
            var generalLedgerExport=CreateActionButton("تصدير Excel", AppIcon.Report, ExportExcel);
            generalLedgerExport.Name="generalLedgerExport"; actions.Controls.Add(generalLedgerExport);
            actions.Controls.Add(CreateActionButton("خروج", AppIcon.Close, () => Close()));
            statusLabel.Name = "generalLedgerStatus";
            statusLabel.Dock = DockStyle.Fill;
            statusLabel.AutoEllipsis = true;
            statusLabel.TextAlign = UiDirection.IsLeftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
            statusLabel.Padding = new Padding(12, 0, 12, 0);
            statusLabel.Font = AppFonts.Create(9.5F, FontStyle.Regular);
            statusLabel.ForeColor = UiTheme.MutedText;
            actionsHost.Controls.Add(statusLabel);
            actionsHost.Controls.Add(actions);

            summaryCards.Dock = DockStyle.Top;
            summaryCards.Height = 108;
            summaryCards.FlowDirection = UiDirection.IsLeftToRight ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;
            summaryCards.WrapContents = false;
            summaryCards.AutoScroll = false;
            summaryCards.Padding = new Padding(8, 6, 8, 4);
            summaryCards.Name = "generalLedgerSummary";
            summaryCards.Tag = "UiSurface";
            summaryCards.BackColor = UiTheme.SurfaceVariant;

            Panel divider = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = UiTheme.Border };

            top.Controls.Add(summaryCards);
            top.Controls.Add(divider);
            top.Controls.Add(filters);
            top.Controls.Add(title);

            grid.Dock = DockStyle.Fill;
            grid.ReadOnly = true;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.Name = "generalLedgerGrid";
            grid.BackgroundColor = UiTheme.Surface;
            grid.BorderStyle = BorderStyle.None;
            grid.RowHeadersVisible = false;
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersDefaultCellStyle.BackColor = UiTheme.PrimaryDark;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            grid.ColumnHeadersDefaultCellStyle.Font = AppFonts.Create(9.5F, FontStyle.Bold);
            grid.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            grid.DefaultCellStyle.Font = AppFonts.Create(9.5F, FontStyle.Regular);
            grid.RowTemplate.Height = 36;
            UiTheme.ApplyDataGridQuality(grid);

            Controls.Add(grid);
            Controls.Add(top);
            Controls.Add(actionsHost);
        }
    }
}



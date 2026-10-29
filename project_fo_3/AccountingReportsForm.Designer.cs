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
    public partial class AccountingReportsForm
    {
        private void InitializeComponent()
        {
            Panel top = new Panel { Name = "accountingReportsTop", Dock = DockStyle.Top, Height = 440, AutoScroll = true, Tag = "UiSurface", BackColor = UiTheme.Surface };

            reportTitle.Text = GetReportTitle();
            reportTitle.Dock = DockStyle.Top;
            reportTitle.Height = 42;
            reportTitle.TextAlign = ContentAlignment.MiddleCenter;
            reportTitle.Font = AppFonts.Create(17F, FontStyle.Bold);
            reportTitle.ForeColor = UiTheme.PrimaryDark;

            Panel tabsHost = new Panel { Name = "accountingReportTabsHost", Dock = DockStyle.Top, Height = 72, Tag = "UiSurface", BackColor = UiTheme.Surface };
            reportTabs.Width = 1050;
            reportTabs.Height = 58;
            reportTabs.Top = 8;
            reportTabs.FlowDirection = FlowDirection.RightToLeft;
            reportTabs.WrapContents = true;
            reportTabs.Padding = new Padding(0, 0, 0, 0);
            reportTabs.Name = "accountingReportTabs";
            reportTabs.BackColor = UiTheme.Surface;
            tabsHost.Controls.Add(reportTabs);
            tabsHost.Resize += (s, e) => LayoutReportTabs(tabsHost);
            BuildReportTabs();
            CenterInParent(reportTabs);

            TableLayoutPanel filters = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 100,
                ColumnCount = 6,
                RowCount = 2,
                Padding = new Padding(16, 4, 16, 0),
                Name = "accountingReportFilters",
                Tag = "UiSurface",
                BackColor = UiTheme.Surface
            };
            for (int i = 0; i < 6; i++) filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16.666F));
            filters.RowStyles.Add(new RowStyle(SizeType.Absolute, 70F));
            filters.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));

            AddFilter(filters, "من تاريخ", fromDate, 0, 0);
            AddFilter(filters, "إلى تاريخ", toDate, 1, 0);
            AddFilter(filters, "مركز التكلفة", costCenterFrame, 2, 0);

            fromDate.Name = "fromDateFilter";
            toDate.Name = "toDateFilter";
            costCenter.Name = "costCenterFilter";
            branch.Name = "branchFilter";
            level.Name = "levelFilter";
            valuationMethod.Name = "valuationMethodFilter";

            includePreviousBalance.Name = "includePreviousBalance";
            includePreviousBalance.Text = "تضمين الرصيد الافتتاحي";
            includePreviousBalance.Checked = true;
            includePreviousBalance.Dock = DockStyle.Fill;
            includePreviousBalance.TextAlign = ContentAlignment.MiddleRight;
            includePreviousBalance.CheckAlign = ContentAlignment.MiddleRight;
            includePreviousBalance.Font = AppFonts.Create(10F, FontStyle.Regular);
            includePreviousBalance.ForeColor = UiTheme.Text;

            excludeInventoryValue.Name = "excludeInventoryValue";
            excludeInventoryValue.Text = "استثناء المخزون من الأرباح والخسائر";
            excludeInventoryValue.Checked = true;
            excludeInventoryValue.Dock = DockStyle.Fill;
            excludeInventoryValue.TextAlign = ContentAlignment.MiddleRight;
            excludeInventoryValue.CheckAlign = ContentAlignment.MiddleRight;
            excludeInventoryValue.Font = AppFonts.Create(10F, FontStyle.Regular);
            excludeInventoryValue.ForeColor = UiTheme.Text;
            filters.Controls.Add(includePreviousBalance, 0, 1);
            filters.SetColumnSpan(includePreviousBalance, 2);
            filters.Controls.Add(excludeInventoryValue, 2, 1);
            filters.SetColumnSpan(excludeInventoryValue, 3);

            TableLayoutPanel templatesHost = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 54,
                ColumnCount = 5,
                Padding = new Padding(16, 6, 16, 4),
                Name = "accountingReportTemplates",
                Tag = "UiSurface",
                BackColor = UiTheme.Surface
            };
            templatesHost.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            templatesHost.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90F));
            templatesHost.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 82F));
            templatesHost.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104F));
            templatesHost.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 76F));
            savedTemplatesFrame.Dock = DockStyle.Fill;
            savedTemplatesFrame.Margin = new Padding(4, 0, 4, 0);
            AntdUI.Button saveTemplate = CreateTemplateButton("حفظ قالب", AppIcon.Save, SaveCurrentTemplate);
            AntdUI.Button loadTemplate = CreateTemplateButton("تطبيق", AppIcon.Confirm, LoadSelectedTemplate);
            AntdUI.Button customizeColumns = CreateTemplateButton("تخصيص الأعمدة", AppIcon.Settings, CustomizeReportColumns);
            AntdUI.Button deleteTemplate = CreateTemplateButton("حذف", AppIcon.Delete, DeleteSelectedTemplate);
            saveTemplate.Name = "saveAccountingReportTemplate";
            loadTemplate.Name = "loadAccountingReportTemplate";
            customizeColumns.Name = "customizeAccountingReportColumns";
            deleteTemplate.Name = "deleteAccountingReportTemplate";
            saveTemplate.Enabled = deleteTemplate.Enabled = !previewMode;
            templatesHost.Controls.Add(savedTemplatesFrame, 0, 0);
            templatesHost.Controls.Add(saveTemplate, 1, 0);
            templatesHost.Controls.Add(loadTemplate, 2, 0);
            templatesHost.Controls.Add(customizeColumns, 3, 0);
            templatesHost.Controls.Add(deleteTemplate, 4, 0);
            templatesHost.Resize += (s, e) => LayoutReportTemplates(templatesHost);

            Panel actionsHost = new Panel { Name = "accountingReportActionsHost", Dock = DockStyle.Bottom, Height = 60, Tag = "UiSurface", BackColor = UiTheme.Surface };
            actionsBar.Width = 680;
            actionsBar.Height = 54;
            actionsBar.Top = 3;
            actionsBar.FlowDirection = FlowDirection.RightToLeft;
            actionsBar.Padding = new Padding(0);
            actionsBar.Name = "accountingReportActions";
            actionsBar.BackColor = UiTheme.Surface;
            actionsHost.Controls.Add(actionsBar);
            actionsHost.Resize += (s, e) => LayoutReportActions(actionsHost);
            actionsBar.Controls.Add(CreateActionButton("عرض النتيجة", AppIcon.Search, LoadData));
            actionsBar.Controls.Add(CreateActionButton("تصدير Excel", AppIcon.Report, ExportCsv));
            actionsBar.Controls.Add(CreateActionButton("طباعة", AppIcon.Print, PrintReport));
            actionsBar.Controls.Add(CreateActionButton("خروج", AppIcon.Close, () => Close()));
            CenterInParent(actionsBar);

            Panel divider = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = UiTheme.Border };

            summaryCards.Dock = DockStyle.Top;
            summaryCards.Height = 106;
            summaryCards.FlowDirection = FlowDirection.RightToLeft;
            summaryCards.WrapContents = true;
            summaryCards.Padding = new Padding(8, 10, 8, 6);
            summaryCards.Name = "accountingReportSummary";
            summaryCards.BackColor = UiTheme.SurfaceVariant;
            summaryCards.Visible = false;

            top.Controls.Add(summaryCards);
            top.Controls.Add(divider);
            templateContext.Controls.Add(templateDetails);
            top.Controls.Add(templateContext);
            top.Controls.Add(templatesHost);
            top.Controls.Add(filters);
            top.Controls.Add(tabsHost);
            top.Controls.Add(reportTitle);

            Action updateTopLayout = () =>
            {
                int preferredHeight = reportTitle.Height + tabsHost.Height + filters.Height + templatesHost.Height + divider.Height +
                    (summaryCards.Visible ? summaryCards.Height : 0) + (templateContext.Visible ? templateContext.Height : 0);
                top.Height = Math.Min(preferredHeight, Math.Max(200, ClientSize.Height - actionsHost.Height - 160));
                top.AutoScrollMinSize = new Size(0, preferredHeight);
            };
            tabsHost.Resize += (s, e) => updateTopLayout();
            filters.Resize += (s, e) => updateTopLayout();
            templatesHost.Resize += (s, e) => updateTopLayout();
            actionsHost.Resize += (s, e) => updateTopLayout();
            summaryCards.VisibleChanged += (s, e) => updateTopLayout();
            templateContext.VisibleChanged += (s, e) => updateTopLayout();
            templateContext.SizeChanged += (s, e) => updateTopLayout();
            summaryCards.ControlAdded += (s, e) => LayoutReportSummary();
            summaryCards.ControlRemoved += (s, e) => LayoutReportSummary();
            summaryCards.SizeChanged += (s, e) => { LayoutReportSummary(); updateTopLayout(); };
            Resize += (s, e) => updateTopLayout();
            ConfigureReportFilters(filters);
            filters.Resize += (s, e) => ConfigureReportFilters(filters);
            LayoutReportTabs(tabsHost);
            LayoutReportTemplates(templatesHost);
            LayoutReportActions(actionsHost);
            updateTopLayout();

            grid.Dock = DockStyle.Fill;
            grid.ReadOnly = true;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.Name = "accountingReportGrid";
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
            grid.AlternatingRowsDefaultCellStyle.BackColor = UiTheme.AlternateRow;
            grid.RowTemplate.Height = 36;
            UiTheme.ApplyDataGridQuality(grid);

            Controls.Add(grid);
            Controls.Add(top);
            Controls.Add(actionsHost);
        }

        private AntdUI.Button CreateTemplateButton(string text, AppIcon icon, Action action)
        {
            AntdUI.Button button = new AntdUI.Button
            {
                Dock = DockStyle.Fill,
                Text = text,
                Margin = new Padding(4, 0, 0, 0),
                Cursor = Cursors.Hand,
                Font = AppFonts.Create(9.5F, FontStyle.Bold),
                AccessibleName = text + " قالب التقرير",
                AccessibleDescription = "إجراء قالب التقرير",
                AccessibleRole = AccessibleRole.PushButton,
                RightToLeft = RightToLeft.Yes,
                IconSvg = OutlinedIcons.Svg(icon, true),
                IconSize = new Size(16, 16),
                IconPosition = AntdUI.TAlignMini.Right
            };
            DesktopComponents.StyleOutlinedFooterAction(button, text == "حذف");
            reportActionCaptions[button] = text;
            button.Click += (s, e) => action();
            return button;
        }

        private void LayoutReportTabs(Panel host)
        {
            int availableWidth = Math.Max(360, host.ClientSize.Width - 32);
            bool compact = availableWidth < 980;
            int columns = compact ? 3 : 6;
            int buttonWidth = Math.Max(108, (availableWidth - columns * 16) / columns);
            foreach (AntdUI.Button button in reportTabs.Controls.OfType<AntdUI.Button>())
            {
                button.Width = buttonWidth;
                button.Height = 36;
                button.Margin = new Padding(8, 2, 8, 2);
            }

            reportTabs.Width = availableWidth;
            reportTabs.Height = compact ? 82 : 44;
            reportTabs.Top = 6;
            host.Height = reportTabs.Height + 12;
            CenterInParent(reportTabs);
        }

        private void LayoutReportActions(Panel host)
        {
            int availableWidth = Math.Max(260, host.ClientSize.Width - 36);
            AntdUI.Button[] buttons = actionsBar.Controls.OfType<AntdUI.Button>().ToArray();
            bool compact = availableWidth < 520;
            int columns = compact ? 2 : Math.Max(1, buttons.Length);
            int buttonWidth = Math.Max(96, (availableWidth - columns * 10) / columns);
            foreach (AntdUI.Button button in buttons)
            {
                button.Width = buttonWidth;
                button.Height = 44;
                button.Margin = new Padding(5, 0, 5, 0);
            }

            actionsBar.Width = availableWidth;
            actionsBar.Height = compact ? 90 : 48;
            actionsBar.Top = 4;
            host.Height = compact ? 98 : 60;
            CenterInParent(actionsBar);
        }

        private void LayoutReportTemplates(TableLayoutPanel host)
        {
            bool compact = host.ClientSize.Width < 700;
            bool medium = host.ClientSize.Width < 980;
            host.SuspendLayout();
            host.ColumnStyles.Clear();
            host.RowStyles.Clear();
            host.Controls.OfType<AntdUI.Button>().ToList().ForEach(button => button.Margin = new Padding(4, 0, 0, 0));

            if (compact)
            {
                host.Height = 144;
                host.ColumnCount = 2;
                host.RowCount = 3;
                host.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
                host.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
                host.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
                host.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
                host.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
                host.SetCellPosition(savedTemplatesFrame, new TableLayoutPanelCellPosition(0, 0));
                host.SetColumnSpan(savedTemplatesFrame, 2);

                AntdUI.Button[] buttons = host.Controls.OfType<AntdUI.Button>().ToArray();
                for (int index = 0; index < buttons.Length; index++)
                {
                    host.SetCellPosition(buttons[index], new TableLayoutPanelCellPosition(index % 2, 1 + index / 2));
                    host.SetColumnSpan(buttons[index], 1);
                }
            }
            else if (medium)
            {
                host.Height = 104;
                host.ColumnCount = 4;
                host.RowCount = 2;
                for (int index = 0; index < 4; index++)
                    host.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
                host.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
                host.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
                host.SetCellPosition(savedTemplatesFrame, new TableLayoutPanelCellPosition(0, 0));
                host.SetColumnSpan(savedTemplatesFrame, 4);

                AntdUI.Button[] buttons = host.Controls.OfType<AntdUI.Button>().ToArray();
                for (int index = 0; index < buttons.Length; index++)
                {
                    host.SetCellPosition(buttons[index], new TableLayoutPanelCellPosition(index, 1));
                    host.SetColumnSpan(buttons[index], 1);
                }
            }
            else
            {
                host.Height = 54;
                host.ColumnCount = 5;
                host.RowCount = 1;
                host.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
                host.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120F));
                host.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 98F));
                host.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150F));
                host.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90F));
                host.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
                host.SetCellPosition(savedTemplatesFrame, new TableLayoutPanelCellPosition(0, 0));
                host.SetColumnSpan(savedTemplatesFrame, 1);

                AntdUI.Button[] buttons = host.Controls.OfType<AntdUI.Button>().ToArray();
                for (int index = 0; index < buttons.Length; index++)
                {
                    host.SetCellPosition(buttons[index], new TableLayoutPanelCellPosition(index + 1, 0));
                    host.SetColumnSpan(buttons[index], 1);
                }
            }

            host.ResumeLayout(true);
        }

        private void ConfigureReportFilters(TableLayoutPanel filters)
        {
            if (filters == null || fromDate.Parent == null || toDate.Parent == null || costCenterFrame.Parent == null) return;
            bool range = activeReport != "TrialBalance";
            bool centers = activeReport == "CostCenters";
            bool financial = activeReport == "BalanceSheet" || activeReport == "ProfitLoss";
            bool profit = activeReport == "ProfitLoss";
            fromDate.Parent.Visible = range;
            costCenterFrame.Parent.Visible = centers;
            costCenter.Enabled = centers;
            includePreviousBalance.Visible = financial;
            excludeInventoryValue.Visible = profit;
            toDate.Parent.Controls.OfType<Label>().Single().Text = Localize(range ? "إلى تاريخ" : "حتى تاريخ");
            var filterPanels = new List<Control>();
            if (range) filterPanels.Add(fromDate.Parent);
            filterPanels.Add(toDate.Parent);
            if (centers) filterPanels.Add(costCenterFrame.Parent);
            int columnCount = Math.Max(2, filterPanels.Count);
            filters.SuspendLayout();
            filters.ColumnCount = columnCount;
            filters.RowCount = financial ? 2 : 1;
            filters.ColumnStyles.Clear();
            for (int index = 0; index < columnCount; index++) filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / columnCount));
            filters.RowStyles.Clear();
            filters.RowStyles.Add(new RowStyle(SizeType.Absolute, 82F));
            if (financial) filters.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
            filters.Height = filters.Padding.Vertical + 82 + (financial ? 44 : 0);
            for (int index = 0; index < filterPanels.Count; index++)
            {
                filters.SetCellPosition(filterPanels[index], new TableLayoutPanelCellPosition(index, 0));
                filters.SetColumnSpan(filterPanels[index], 1);
            }
            if (financial)
            {
                filters.SetCellPosition(includePreviousBalance, new TableLayoutPanelCellPosition(0, 1));
                filters.SetColumnSpan(includePreviousBalance, profit ? 1 : columnCount);
                filters.SetCellPosition(excludeInventoryValue, new TableLayoutPanelCellPosition(1, 1));
                filters.SetColumnSpan(excludeInventoryValue, 1);
            }
            filters.ResumeLayout(true);
        }

        private bool layingOutReportSummary;
        private void LayoutReportSummary()
        {
            if (layingOutReportSummary || summaryCards.ClientSize.Width <= 0 || summaryCards.Controls.Count == 0) return;
            layingOutReportSummary = true; summaryCards.SuspendLayout();
            try
            {
                float scale = DeviceDpi / 96F;
                int gap = Math.Max(1, (int)Math.Round(10 * scale));
                int available = Math.Max(1, summaryCards.ClientSize.Width - summaryCards.Padding.Horizontal);
                // RTL FlowLayout uses the display width without its leading offset; reserve it so the last card stays inside padding.
                if (!templateLeftToRight) available = Math.Max(1, available - summaryCards.Padding.Left);
                int minimum = Math.Max(1, (int)Math.Round(240 * scale));
                int columns = Math.Min(summaryCards.Controls.Count, Math.Max(1, available / (minimum + gap)));
                int width = Math.Max(1, available / columns - gap);
                int cardHeight = Math.Max(106, (int)Math.Round(106 * scale));
                summaryCards.BackColor = UiTheme.Surface;
                summaryCards.FlowDirection = templateLeftToRight ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;
                summaryCards.RightToLeft = RightToLeft.No;
                foreach (AntdUI.Panel card in summaryCards.Controls.OfType<AntdUI.Panel>())
                {
                    card.RightToLeft = RightToLeft.No;
                    card.Size = new Size(width, cardHeight);
                    card.Margin = new Padding(gap / 2);
                    card.Padding = new Padding(Math.Max(8, (int)Math.Round(14 * scale)));
                    var heading = card.Controls["accountingReportSummaryTitle"] as Label;
                    var values = card.Controls["accountingReportSummaryValue"] as Label;
                    if (heading != null)
                    {
                        heading.Height = Math.Max(24, (int)Math.Round(24 * scale));
                        heading.RightToLeft = RightToLeft.No;
                        heading.TextAlign = templateLeftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
                    }
                    if (values != null) { values.RightToLeft = RightToLeft.No; values.TextAlign = ContentAlignment.MiddleCenter; }
                }
                int rows = (summaryCards.Controls.Count + columns - 1) / columns;
                int height = summaryCards.Padding.Vertical + rows * (cardHeight + gap);
                if (summaryCards.Height != height) summaryCards.Height = height;
            }
            finally { summaryCards.ResumeLayout(true); layingOutReportSummary = false; }
        }
    }
}

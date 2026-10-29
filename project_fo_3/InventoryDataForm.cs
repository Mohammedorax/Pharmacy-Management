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
    // قاعدة مشتركة لشاشات تقارير وعمليات المخزون، مستقلة عن واجهة المخزن الرئيسية.
    internal abstract partial class InventoryDataForm : Form
    {
        protected readonly FlowLayoutPanel FilterPanel = new FlowLayoutPanel();
        protected readonly FlowLayoutPanel SummaryPanel = new FlowLayoutPanel();
        protected readonly DataGridView Grid = new DataGridView();
        protected readonly Label StatusLabel = new Label();
        private readonly Label emptyGridHint = new Label { Name = "inventoryDataEmptyHint", Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter, Visible = false, Padding = new Padding(24),
            AccessibleRole = AccessibleRole.StaticText };
        private DataTable currentTable = new DataTable();
        private Func<bool> gridOutputContext;
        private readonly string titleArabic;
        protected bool leftToRight;

        protected InventoryDataForm(string title, bool loadData = true)
        {
            titleArabic = title;
            leftToRight = UiDirection.IsLeftToRight;
            Text = title;
            Size = new Size(1120, 700);
            MinimumSize = new Size(640, 480);
            StartPosition = FormStartPosition.CenterParent;
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            Font = AppFonts.Create(9.5F);
            BackColor = UiTheme.Background;

            InitializeComponent(title);
            Grid.CellFormatting += FormatSystemInventoryValue;
            UiTheme.Apply(this);
            ApplyInventoryDataVisualTheme();
            // FlowLayoutPanel combines inherited RTL with FlowDirection; use one
            // direction source so the first filter follows the language edge.
            FilterPanel.RightToLeft = RightToLeft.No;
            SummaryPanel.RightToLeft = RightToLeft.No;
            FilterPanel.FlowDirection = UiDirection.IsLeftToRight ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;
            SummaryPanel.FlowDirection = FilterPanel.FlowDirection;
            UiTheme.ApplyDataGridQuality(Grid, !string.IsNullOrWhiteSpace(emptyGridHint.Text)
                ? emptyGridHint.Text : leftToRight ? "No data available." : "لا توجد بيانات حالياً");
            ApplyQuantityColumnFormats();
            emptyGridHint.BackColor = UiTheme.Surface;
            emptyGridHint.ForeColor = UiTheme.MutedText;
            emptyGridHint.TextAlign = ContentAlignment.MiddleCenter;
            emptyGridHint.Font = AppFonts.Create(11F);
            Grid.Controls.Add(emptyGridHint);
            emptyGridHint.BringToFront();
            UiTheme.InstallStatusStrip(this);
            if (!loadData) UiTheme.RemoveStatusStrip(this);
            KeyPreview = true;
            KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); };
            FilterPanel.ControlAdded += (s, e) => ReflowTopPanels();
            FilterPanel.ControlRemoved += (s, e) => ReflowTopPanels();
            SummaryPanel.ControlAdded += (s, e) => ReflowTopPanels();
            SummaryPanel.ControlRemoved += (s, e) => ReflowTopPanels();
            Resize += (s, e) => ReflowTopPanels();
            Load += (s, e) =>
            {
                FitToAvailableArea();
                ReflowTopPanels();
            };
        }

        protected string Present(string arabicText) => leftToRight
            ? UiLanguageCatalog.Translate(arabicText) : arabicText;

        protected void ApplyInventoryPresentation(bool useLeftToRight)
        {
            leftToRight = useLeftToRight;
            UiDirection.Apply(this, leftToRight);
            Text = Present(titleArabic);
            Label title = Controls.Find("inventoryDataTitle", true).FirstOrDefault() as Label;
            if (title != null)
            {
                title.Text = Text;
                title.TextAlign = leftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
                // Explicit bounds avoid a stale mirrored Dock width after changing RightToLeftLayout.
                title.Dock = DockStyle.None;
                title.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
                title.Padding = new Padding(24, 0, 24, 0);
                if (title.Parent != null)
                {
                    title.Bounds = title.Parent.ClientRectangle;
                    title.Parent.PerformLayout();
                }
            }
            foreach (Label label in FilterPanel.Controls.Cast<Control>()
                .SelectMany(control => control.Controls.Cast<Control>()).OfType<Label>())
                if (label.Tag is string caption)
                {
                    label.Text = Present(caption);
                    label.TextAlign = leftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
                }
            FilterPanel.RightToLeft = RightToLeft.No;
            SummaryPanel.RightToLeft = RightToLeft.No;
            FilterPanel.FlowDirection = leftToRight ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;
            SummaryPanel.FlowDirection = FilterPanel.FlowDirection;
            foreach (FlowLayoutPanel footer in Controls.Cast<Control>().OfType<FlowLayoutPanel>()
                .Where(panel => panel.Name.EndsWith("Footer", StringComparison.Ordinal)))
            {
                footer.RightToLeft = RightToLeft.No;
                footer.FlowDirection = leftToRight ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;
            }
            foreach (AntdUI.Button action in Controls.Cast<Control>().SelectMany(AllControls).OfType<AntdUI.Button>())
                if (action.Tag is string caption)
                {
                    action.Text = Present(caption);
                    action.AccessibleName = action.Text;
                    action.IconPosition = leftToRight ? AntdUI.TAlignMini.Left : AntdUI.TAlignMini.Right;
                }
            foreach (FlowLayoutPanel footer in Controls.OfType<FlowLayoutPanel>().Where(panel => panel.Name.EndsWith("Footer", StringComparison.Ordinal)))
                LayoutInventoryActionFooter(footer);
            UiTheme.ApplyStatusStripPresentation(this, leftToRight);
            Grid.AccessibleName = Present("نتائج التقرير");
            ApplyGridPresentation();
            ReflowTopPanels();
        }

        private static IEnumerable<Control> AllControls(Control root)
        {
            yield return root;
            foreach (Control child in root.Controls)
                foreach (Control nested in AllControls(child)) yield return nested;
        }

        private void FitToAvailableArea()
        {
            Rectangle area = Owner == null
                ? Screen.FromControl(this).WorkingArea
                : Screen.FromControl(Owner).WorkingArea;

            MinimumSize = new Size(Math.Min(MinimumSize.Width, Math.Max(1, area.Width - 16)),
                Math.Min(MinimumSize.Height, Math.Max(1, area.Height - 16)));
            int margin = 36;
            int width = Math.Min(Width, Math.Max(MinimumSize.Width, area.Width - margin));
            int height = Math.Min(Height, Math.Max(MinimumSize.Height, area.Height - margin));
            Size = new Size(width, height);

            if (StartPosition == FormStartPosition.CenterParent && Owner != null)
            {
                Left = Owner.Left + Math.Max(0, (Owner.Width - Width) / 2);
                Top = Owner.Top + Math.Max(0, (Owner.Height - Height) / 2);
            }

            Left = Math.Max(area.Left + 8, Math.Min(Left, area.Right - Width - 8));
            Top = Math.Max(area.Top + 8, Math.Min(Top, area.Bottom - Height - 8));
        }

        protected void ReflowTopPanels()
        {
            if (FilterPanel == null || SummaryPanel == null || IsDisposed)
            {
                return;
            }

            // Dock order remains stable when the summary changes visibility.
            Control header = Controls.Find("inventoryDataHeader", false).FirstOrDefault();
            if (header != null)
            {
                Controls.SetChildIndex(header, Controls.Count - 1);
                Controls.SetChildIndex(FilterPanel, Controls.Count - 2);
                Controls.SetChildIndex(SummaryPanel, Controls.Count - 3);
            }
            bool hasSummary = SummaryPanel.Controls.Count > 0;
            if (hasSummary)
            {
                int available = Math.Max(1, SummaryPanel.Width - SummaryPanel.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth);
                int required = SummaryPanel.Controls.Cast<Control>().SelectMany(card => card.Controls.Cast<Control>())
                    .OfType<Label>().Select(label => TextRenderer.MeasureText(label.Text, label.Font).Width + 24).DefaultIfEmpty(136).Max();
                int minimum = Math.Max(136, Math.Min(360, required));
                int columns = Math.Min(SummaryPanel.Controls.Count, Math.Max(1, available / (minimum + 12)));
                int width = Math.Max(1, Math.Min(Math.Max(220, minimum), available / columns - 12));
                foreach (Control card in SummaryPanel.Controls) { ApplyMetricPresentation(card); card.Width = width; }
            }
            SummaryPanel.Visible = hasSummary;
            int chrome = Controls.Cast<Control>().Where(control => control != FilterPanel && control != SummaryPanel &&
                (control.Dock == DockStyle.Top || control.Dock == DockStyle.Bottom)).Sum(control => control.Height);
            // Reserve a usable table viewport; overflowing filters/metrics remain reachable by scrolling.
            int contentBudget = Math.Max(204, ClientSize.Height - chrome - 80);
            FilterPanel.AutoScroll = true;
            SummaryPanel.AutoScroll = true;
            ReflowFlowPanel(FilterPanel, 92, Math.Max(92, Math.Min(210, contentBudget - (hasSummary ? 112 : 0))));
            if (hasSummary) ReflowFlowPanel(SummaryPanel, 112, Math.Max(112, Math.Min(216, contentBudget - FilterPanel.Height)));
        }

        private void ReflowFlowPanel(FlowLayoutPanel panel, int minimumHeight, int maximumHeight)
        {
            if (panel == null || panel.ClientSize.Width <= 0)
            {
                return;
            }

            int availableWidth = Math.Max(260, panel.Width - panel.Padding.Left - panel.Padding.Right - (panel.AutoScroll ? SystemInformation.VerticalScrollBarWidth : 0));
            int usedWidth = 0;
            int rowHeight = 0;
            int totalHeight = panel.Padding.Top + panel.Padding.Bottom;

            foreach (Control control in panel.Controls)
            {
                if (!control.Visible)
                {
                    continue;
                }

                int itemWidth = control.Width + control.Margin.Left + control.Margin.Right;
                int itemHeight = control.Height + control.Margin.Top + control.Margin.Bottom;

                if (usedWidth > 0 && usedWidth + itemWidth > availableWidth)
                {
                    totalHeight += rowHeight;
                    usedWidth = 0;
                    rowHeight = 0;
                }

                usedWidth += itemWidth;
                rowHeight = Math.Max(rowHeight, itemHeight);
            }

            if (rowHeight > 0)
            {
                totalHeight += rowHeight;
            }

            panel.Height = Math.Max(minimumHeight, Math.Min(maximumHeight, totalHeight + 2));
            // Explicit metric extent includes padding; WinForms RTL flow otherwise stops before the final card.
            if (panel == SummaryPanel) panel.AutoScrollMinSize = new Size(0, totalHeight);
        }

        protected void AddFilter(string caption, Control control, int width = 180, int height = 64)
        {
            width = Math.Min(width, 280);
            Panel box = new Panel
            {
                Width = width,
                Height = height,
                Margin = new Padding(6, 4, 6, 4),
                BackColor = Color.Transparent
            };

            Label label = new Label
            {
                Text = UiLanguageCatalog.GetDisplayText(caption),
                Tag = caption,
                Dock = DockStyle.Top,
                Height = 24,
                TextAlign = UiDirection.IsLeftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight,
                Font = AppFonts.Create(9.25F, FontStyle.Bold),
                ForeColor = UiTheme.Text
            };

            control.Dock = DockStyle.Bottom;
            control.Height = Math.Max(30, height - 28);
            control.Font = AppFonts.Create(9.5F);
            UiTheme.ApplyTo(control);
            box.Controls.Add(control);
            box.Controls.Add(label);
            // A native editor already has an AntdUI frame; clipping its transparent host cuts the border.
            if (!(control is AntdUI.Panel)) WinFormsShapeAdapter.ApplyCard(box);
            FilterPanel.Controls.Add(box);
            ReflowTopPanels();
        }

        protected FlowLayoutPanel AddOutlinedActionFooter(string name)
        {
            var footer = new FlowLayoutPanel
            {
                Name = name,
                Dock = DockStyle.Bottom,
                Height = 64,
                BackColor = UiTheme.Surface,
                RightToLeft = RightToLeft.No,
                FlowDirection = leftToRight ? FlowDirection.LeftToRight : FlowDirection.RightToLeft,
                Padding = new Padding(18, 8, 18, 8),
                WrapContents = true
            };
            footer.Resize += (sender, args) => LayoutInventoryActionFooter(footer);
            footer.ControlAdded += (sender, args) =>
            {
                var action = args.Control as AntdUI.Button;
                if (action != null)
                {
                    action.TextChanged += InventoryFooterActionChanged;
                    action.FontChanged += InventoryFooterActionChanged;
                }
                LayoutInventoryActionFooter(footer);
            };
            footer.ControlRemoved += (sender, args) =>
            {
                var action = args.Control as AntdUI.Button;
                if (action != null)
                {
                    action.TextChanged -= InventoryFooterActionChanged;
                    action.FontChanged -= InventoryFooterActionChanged;
                }
                LayoutInventoryActionFooter(footer);
            };
            Controls.Add(footer);
            AddOutlinedFooterAction(footer, "inventoryDataCloseButton", "إغلاق", AppIcon.Close,
                (sender, args) => Close());
            LayoutInventoryActionFooter(footer);
            return footer;
        }
        protected static void AddOutlinedFooterAction(FlowLayoutPanel footer, string name, string text,
            AppIcon icon, EventHandler click)
        {
            AntdUI.Button button = DesktopComponents.Action(name, UiLanguageCatalog.GetDisplayText(text), icon);
            button.Tag = text;
            button.Size = new Size(104, 44);
            button.Margin = new Padding(6, 2, 6, 2);
            button.RightToLeft = UiDirection.IsLeftToRight ? RightToLeft.No : RightToLeft.Yes;
            button.IconPosition = UiDirection.IsLeftToRight ? AntdUI.TAlignMini.Left : AntdUI.TAlignMini.Right;
            button.Enabled = !IsGridOutputAction(name);
            DesktopComponents.StyleOutlinedFooterAction(button);
            button.Click += click;
            footer.Controls.Add(button);
        }

        private void InventoryFooterActionChanged(object sender, EventArgs args)
        {
            var action = sender as Control;
            var footer = action == null ? null : action.Parent as FlowLayoutPanel;
            if (footer != null) LayoutInventoryActionFooter(footer);
        }
        private bool inventoryFooterLayoutBusy;
        private void LayoutInventoryActionFooter(FlowLayoutPanel footer)
        {
            if (inventoryFooterLayoutBusy || IsDisposed || footer == null || footer.IsDisposed) return;
            inventoryFooterLayoutBusy = true;
            bool heightChanged = false;
            try
            {
                footer.BackColor = UiTheme.Surface;
                footer.RightToLeft = RightToLeft.No;
                footer.FlowDirection = leftToRight ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;
                footer.WrapContents = true;
                int available = Math.Max(1, footer.ClientSize.Width - footer.Padding.Horizontal);
                int used = 0, rows = 1;
                foreach (AntdUI.Button action in footer.Controls.OfType<AntdUI.Button>())
                {
                    int width = Math.Min(Math.Max(1, available - action.Margin.Horizontal),
                        Math.Max(104, TextRenderer.MeasureText(action.Text, action.Font).Width + 48));
                    action.Size = new Size(width, DesktopDesignTokens.ActionHeight);
                    DesktopComponents.StyleOutlinedFooterAction(action);
                    int occupied = width + action.Margin.Horizontal;
                    if (used > 0 && used + occupied > available) { rows++; used = 0; }
                    used += occupied;
                }
                int height = footer.Padding.Vertical + rows * (DesktopDesignTokens.ActionHeight + 4);
                heightChanged = footer.Height != height;
                footer.Height = height;
            }
            finally { inventoryFooterLayoutBusy = false; }
            if (heightChanged) ReflowTopPanels();
        }

        protected void SetSummary(params InventorySummaryItem[] items)
        {
            Control[] previousCards = SummaryPanel.Controls.Cast<Control>().ToArray();
            SummaryPanel.Controls.Clear();
            foreach (Control previousCard in previousCards) previousCard.Dispose();
            foreach (InventorySummaryItem item in items)
            {
                SummaryPanel.Controls.Add(CreateMetricCard(item.Title, item.Value));
            }
            ReflowTopPanels();
        }

        protected InventorySummaryItem Metric(string title, object value)
        {
            return new InventorySummaryItem(title, value == null ? "-" : Convert.ToString(value));
        }

        protected void BindGrid(DataTable table, Action validateContext = null)
        {
            currentTable = table ?? new DataTable();
            long revision=SessionContext.CurrentSessionRevision;
            string connection=Database.ConnectionString,user=SessionContext.CurrentUser,role=SessionContext.CurrentRole,employee=SessionContext.CurrentEmployeeId;
            gridOutputContext=()=>SessionContext.IsAuthenticated&&revision==SessionContext.CurrentSessionRevision&&connection==Database.ConnectionString&&
                string.Equals(user,SessionContext.CurrentUser,StringComparison.OrdinalIgnoreCase)&&string.Equals(role,SessionContext.CurrentRole,StringComparison.OrdinalIgnoreCase)&&
                string.Equals(employee,SessionContext.CurrentEmployeeId,StringComparison.OrdinalIgnoreCase)&&PermissionService.CanOpen(AppPermission.Inventory)&&
                ReportPreviewLifetime.ContextCurrent(()=>{validateContext?.Invoke();return true;});
            SetGridOutputActionsReady(currentTable.Rows.Count > 0);
            Grid.DataSource = currentTable;
            FormatGridColumns();
            UiTheme.ApplyDataGridQuality(Grid, !string.IsNullOrWhiteSpace(emptyGridHint.Text)
                ? emptyGridHint.Text : leftToRight ? "No data available." : "لا توجد بيانات حالياً");
            ApplyGridPresentation();
            ApplyQuantityColumnFormats();
            // Global theme defaults must not replace this screen's displayed language.
            UiDirection.Apply(Grid, leftToRight);
            UpdateEmptyGridHint();
        }

        private static bool IsGridOutputAction(string nameOrText)
        {
            if (string.IsNullOrWhiteSpace(nameOrText)) return false;
            return nameOrText.IndexOf("print", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   nameOrText.IndexOf("export", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   nameOrText.IndexOf("طباعة", StringComparison.Ordinal) >= 0 ||
                   nameOrText.IndexOf("تصدير", StringComparison.Ordinal) >= 0;
        }

        private void SetGridOutputActionsReady(bool ready)
        {
            foreach (Control control in Descendants(Controls))
                if (IsGridOutputAction(control.Name) && (control is Button || control is AntdUI.Button))
                    control.Enabled = ready;
        }

        private static System.Collections.Generic.IEnumerable<Control> Descendants(Control.ControlCollection controls)
        {
            foreach (Control control in controls)
            {
                yield return control;
                foreach (Control child in Descendants(control.Controls)) yield return child;
            }
        }
        protected void SetEmptyGridHint(string message)
        {
            emptyGridHint.Text = message ?? string.Empty;
            Label sharedEmptyState = Grid.Controls["__GridEmptyState"] as Label;
            if (sharedEmptyState != null) sharedEmptyState.Text = emptyGridHint.Text;
            UpdateEmptyGridHint();
        }

        private void UpdateEmptyGridHint()
        {
            emptyGridHint.BackColor = UiTheme.Surface;
            emptyGridHint.ForeColor = UiTheme.MutedText;
            emptyGridHint.TextAlign = ContentAlignment.MiddleCenter;
            emptyGridHint.Visible = !string.IsNullOrWhiteSpace(emptyGridHint.Text) && currentTable.Rows.Count == 0;
            if (emptyGridHint.Visible) emptyGridHint.BringToFront();
        }

        protected void SetLoadedStatus(DataTable table, string successMessage)
        {
            bool hasRows = table != null && table.Rows.Count > 0;
            UiTheme.SetStatus(
                StatusLabel,
                hasRows ? Present(successMessage) : Present("لا توجد بيانات مطابقة لخيارات البحث الحالية."),
                hasRows ? (bool?)true : null);
        }

        internal Func<bool> CaptureGridOutputContext()
        {
            var table=currentTable;var context=gridOutputContext;
            return ()=>!IsDisposed&&!Disposing&&table!=null&&table.Rows.Count>0&&ReferenceEquals(table,currentTable)&&ReferenceEquals(context,gridOutputContext)&&context!=null&&ReportPreviewLifetime.ContextCurrent(context);
        }
        private bool EnsureGridOutputContext(Func<bool> current)
        {
            if(ReportPreviewLifetime.ContextCurrent(current))return true;
            if(!IsDisposed&&!Disposing)UiTheme.SetStatus(StatusLabel,Present("تغيرت جلسة المستخدم؛ أعد تحميل التقرير."),false);
            return false;
        }
        protected void PreviewCurrentGrid()
        {
            if (currentTable == null || currentTable.Rows.Count == 0)
            {
                AppNotifications.Show(Present("لا توجد بيانات جاهزة للطباعة."), Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var outputContext=CaptureGridOutputContext();if(!EnsureGridOutputContext(outputContext))return;
            string subtitle = string.Format(Present("عدد السجلات: {0} | تاريخ الطباعة: {1}"),
                currentTable.Rows.Count.ToString("N0"), DateTime.Now.ToString("yyyy/MM/dd HH:mm"));
            try
            {
                ReportPrintOptions selected;
                using (DataTable snapshot = CreateGridOutputSnapshot())
                if (DataTablePrintPreview.ShowWithOptions(this, Text, subtitle, snapshot, null, out selected,
                    leftToRight, isCurrent: outputContext))
                {
                    UiTheme.SetStatus(StatusLabel, Present("تم فتح معاينة الطباعة."), true);
                }
            }
            catch (Exception ex)
            {
                UiTheme.SetStatus(StatusLabel, Present("تعذر فتح معاينة الطباعة."), false);
                UiTheme.ShowOperationIssue(this, Present("فتح معاينة الطباعة"), ex);
            }
        }

        protected void ExportCurrentGrid()
        {
            if (currentTable == null || currentTable.Rows.Count == 0)
            {
                AppNotifications.Show(Present("لا توجد بيانات جاهزة للتصدير."), Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var outputContext=CaptureGridOutputContext();if(!EnsureGridOutputContext(outputContext))return;
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Filter = "Excel Workbook (*.xlsx)|*.xlsx";
                dialog.DefaultExt = "xlsx";
                dialog.AddExtension = true;
                dialog.FileName = "Inventory-Report-" + DateTime.Now.ToString("yyyyMMdd-HHmm", System.Globalization.CultureInfo.InvariantCulture) + ".xlsx";
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                try
                {
                    if(!EnsureGridOutputContext(outputContext))return;
                    using (DataTable snapshot = CreateGridOutputSnapshot())
                    {
                        if(!EnsureGridOutputContext(outputContext))return;
                        ExcelWorkbookWriter.WriteReportCompleteFile(snapshot, Text, dialog.FileName, leftToRight);
                    }
                    UiTheme.SetStatus(StatusLabel, Present("تم تصدير التقرير بنجاح."), true);
                }
                catch (Exception ex)
                {
                    UiTheme.SetStatus(StatusLabel, Present("تعذر تأكيد تصدير التقرير؛ تحقق من الملف والمسار قبل إعادة المحاولة."), false);
                    AppNotifications.ShowFileOutputIssue(this, "تصدير التقرير", ex, leftToRight);
                }
            }
        }

        private DataTable CreateGridOutputSnapshot()
        {
            // Keep names and typed values as stable report keys; localize captions and known system values only.
            DataTable snapshot = currentTable.Copy();
            if (snapshot.Columns.Contains("الصنف"))
                snapshot.Columns["الصنف"].ExtendedProperties["RepeatOnPrint"] = true;
            if (snapshot.Columns.Contains("معرف التشغيلة"))
                snapshot.Columns["معرف التشغيلة"].ExtendedProperties["RepeatOnPrint"] = true;
            foreach (DataColumn column in snapshot.Columns)
            {
                column.Caption = Present(column.ColumnName);
                if (column.DataType != typeof(string)) continue;
                foreach (DataRow row in snapshot.Rows)
                {
                    if (row.IsNull(column)) continue;
                    string raw = (string)row[column];
                    string display = PresentInventoryValue(column.ColumnName, raw);
                    if (!string.Equals(raw, display, StringComparison.Ordinal)) row[column] = display;
                }
            }
            return snapshot;
        }

        private string BuildCurrentGridCsv()
        {
            using (DataTable snapshot = CreateGridOutputSnapshot())
            {
                StringBuilder builder = new StringBuilder();
                builder.AppendLine(string.Join(",", snapshot.Columns.Cast<DataColumn>().Select(column => Csv(column.Caption))));
                foreach (DataRow row in snapshot.Rows)
                    builder.AppendLine(string.Join(",", row.ItemArray.Select(value => Csv(Convert.ToString(value)))));
                return builder.ToString();
            }
        }

        private static string Csv(string value)
        {
            value = value ?? string.Empty;
            string trimmed = value.TrimStart();
            if (trimmed.Length > 0 && "=+-@".IndexOf(trimmed[0]) >= 0)
            {
                value = "'" + value;
            }

            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        protected decimal SumDecimal(DataTable table, string columnName)
        {
            if (table == null || !table.Columns.Contains(columnName))
            {
                return 0;
            }

            decimal total = 0;
            foreach (DataRow row in table.Rows)
            {
                if (row[columnName] != DBNull.Value)
                {
                    total += Convert.ToDecimal(row[columnName]);
                }
            }
            return total;
        }

        protected int SumInt(DataTable table, string columnName)
        {
            if (table == null || !table.Columns.Contains(columnName))
            {
                return 0;
            }

            int total = 0;
            foreach (DataRow row in table.Rows)
            {
                if (row[columnName] != DBNull.Value)
                {
                    total += Convert.ToInt32(row[columnName]);
                }
            }
            return total;
        }

        protected int ParseInt(Control input, string fieldName)
        {
            int value;
            if (!int.TryParse(input.Text.Trim(), out value))
            {
                throw new InvalidOperationException(string.Format(
                    Present("أدخل قيمة صحيحة في حقل {0}."), Present(fieldName)));
            }
            return value;
        }

        protected int ParseNonNegativeInt(Control input, string fieldName)
        {
            int value = ParseInt(input, fieldName);
            if (value < 0)
            {
                throw new InvalidOperationException(string.Format(
                    Present("يجب ألا تكون قيمة {0} سالبة."), Present(fieldName)));
            }
            return value;
        }

        protected bool TryValidateDateRange(DateTime from, DateTime to)
        {
            if (from.Date <= to.Date)
            {
                return true;
            }

            UiTheme.SetStatus(StatusLabel, Present("تاريخ البداية يجب أن يسبق تاريخ النهاية أو يساويه."), false);
            return false;
        }

        protected decimal? ParseOptionalMoney(Control input, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(input.Text))
            {
                return null;
            }

            decimal value;
            if (!decimal.TryParse(input.Text.Trim(), out value) || value < 0)
            {
                throw new InvalidOperationException(string.Format(
                    Present("أدخل قيمة مالية صحيحة في حقل {0}."), Present(fieldName)));
            }
            return value;
        }

        protected string FormatMoney(decimal value)
        {
            return value.ToString("N2");
        }

        private static void ConfigureGrid(DataGridView grid)
        {
            grid.Name = "inventoryDataGrid";
            // The screen owns bilingual headers and direction, including rebind events.
            grid.Tag = UiTheme.PreserveLocalizedGridDirection;
            grid.AccessibleName = "نتائج التقرير";
            grid.Dock = DockStyle.Fill;
            grid.ReadOnly = true;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.BackgroundColor = UiTheme.Surface;
            grid.BorderStyle = BorderStyle.None;
            grid.RowHeadersVisible = false;
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersHeight = 40;
            grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            grid.ColumnHeadersDefaultCellStyle.BackColor = UiTheme.PrimaryDark;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            grid.ColumnHeadersDefaultCellStyle.Font = AppFonts.Create(9.75F, FontStyle.Bold);
            grid.DefaultCellStyle.SelectionBackColor = UiTheme.Selection;
            grid.DefaultCellStyle.SelectionForeColor = UiTheme.Text;
            grid.AlternatingRowsDefaultCellStyle.BackColor = UiTheme.AlternateRow;
            grid.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            UiTheme.ApplyDataGridQuality(grid);
        }

        private void ApplyQuantityColumnFormats()
        {
            foreach(DataGridViewColumn column in Grid.Columns)
            {
                string name=string.IsNullOrEmpty(column.DataPropertyName)?column.Name:column.DataPropertyName;
                switch(name)
                {
                    case "الوارد":case "الصادر":case "صافي الحركة":case "الرصيد قبل":case "الرصيد بعد":
                    case "إجمالي الوارد":case "إجمالي الصادر":case "الصادر خلال الفترة":case "الوارد خلال الفترة":
                    case "الكمية الحالية":column.DefaultCellStyle.Format="#,##0.###";break;
                }
            }
        }
        private void FormatGridColumns()
        {
            foreach (DataGridViewColumn column in Grid.Columns)
            {
                if (column.ValueType == typeof(DateTime) || column.HeaderText.Contains("تاريخ") || column.HeaderText.Contains("آخر"))
                {
                    column.DefaultCellStyle.Format = column.HeaderText == "التاريخ" ? "yyyy-MM-dd HH:mm" : "yyyy-MM-dd";
                }
                else if (column.ValueType == typeof(decimal) || column.HeaderText.Contains("قيمة") || column.HeaderText.Contains("تكلفة") || column.HeaderText.Contains("سعر") || column.HeaderText.Contains("معدل"))
                {
                    column.DefaultCellStyle.Format = "N2";
                }
            }
        }

        private void FormatSystemInventoryValue(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (!leftToRight || e.RowIndex < 0 || e.ColumnIndex < 0 || !(e.Value is string)) return;
            string raw = (string)e.Value;
            string column = Grid.Columns[e.ColumnIndex].DataPropertyName;
            string display = PresentInventoryValue(column, raw);
            if (string.Equals(display, raw, StringComparison.Ordinal)) return;
            e.Value = display;
            e.FormattingApplied = true;
        }

        private string PresentInventoryValue(string column, string raw)
        {
            if (!leftToRight || raw == null) return raw;
            // Localize only SQL-generated labels; never translate stored names or lot identifiers.
            if (column == "الحالة" || column == "حالة التشغيلة" || column == "التصنيف" || column == "نوع التنبيه" ||
                (column == "الوحدة" && raw == "الرصيد الرئيسي")) return Present(raw);
            if (column != "التفاصيل") return raw;
            foreach (string message in new[] {
                "الصنف منتهي الصلاحية ولا يجب صرفه.",
                "الصنف يقترب من تاريخ الانتهاء ضمن فترة التنبيه.",
                "الكمية الحالية عند حد إعادة الطلب أو أقل منه." })
            {
                if (raw == message) return Present(message);
                const string lotPrefix = " التشغيلة: ";
                if (raw.StartsWith(message + lotPrefix, StringComparison.Ordinal))
                    return Present(message) + " Lot: " + raw.Substring(message.Length + lotPrefix.Length);
            }
            return raw;
        }

        private void ApplyGridPresentation()
        {
            foreach (DataGridViewColumn column in Grid.Columns)
            {
                string source = string.IsNullOrWhiteSpace(column.DataPropertyName)
                    ? column.Name : column.DataPropertyName;
                if (!string.IsNullOrWhiteSpace(source)) column.HeaderText = Present(source);
                if (!leftToRight) continue;
                // Keep whole header words readable while allowing wrapping and horizontal scrolling.
                Font headerFont = column.HeaderCell.InheritedStyle.Font ?? Grid.Font;
                int wordWidth = column.HeaderText.Split(new[] { ' ', '/', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(word => TextRenderer.MeasureText(word, headerFont).Width).DefaultIfEmpty(0).Max();
                column.MinimumWidth = Math.Max(column.MinimumWidth, wordWidth + 24);
            }
        }

        private Control CreateMetricCard(string title, string value)
        {
            var card = DesktopComponents.Card("inventoryMetricCard");
            card.Tag = title; card.Width = 220; card.Height = 96;
            card.Padding = new Padding(10, 8, 10, 8); card.Margin = new Padding(6, 0, 6, 0);
            card.Radius = DesktopDesignTokens.CardRadius; card.Shadow = 0;
            card.BackColor = UiTheme.Surface; card.AccessibleRole = AccessibleRole.StaticText;
            var valueLabel = new BusinessValueLabel { Name = "inventoryMetricValue", Text = value, RightToLeft = RightToLeft.No,
                TextAlign = ContentAlignment.MiddleCenter, Font = AppFonts.Create(13F, FontStyle.Bold),
                ForeColor = UiTheme.Primary, BackColor = UiTheme.Surface };
            var titleLabel = new Label { Name = "inventoryMetricTitle", Text = Present(title),
                TextAlign = ContentAlignment.MiddleCenter, Font = AppFonts.Create(10F),
                ForeColor = UiTheme.MutedText, BackColor = UiTheme.Surface };
            card.Controls.Add(titleLabel); card.Controls.Add(valueLabel);
            Action layout = () =>
            {
                int width = Math.Max(1, card.ClientSize.Width - 20);
                valueLabel.SetBounds(10, 8, width, 32); titleLabel.SetBounds(10, 44, width, 44);
            };
            card.SizeChanged += (sender, args) => layout(); card.Layout += (sender, args) => layout();
            layout(); ApplyMetricPresentation(card);
            return card;
        }

        private void ApplyMetricPresentation(Control card)
        {
            var title = card.Controls["inventoryMetricTitle"] as Label;
            var value = card.Controls["inventoryMetricValue"] as Label;
            if (title == null || value == null) return;
            title.Text = Present(Convert.ToString(card.Tag));
            title.TextAlign = value.TextAlign = ContentAlignment.MiddleCenter;
            value.RightToLeft = RightToLeft.No;
            title.RightToLeft = leftToRight ? RightToLeft.No : RightToLeft.Yes;
            title.ForeColor = UiTheme.MutedText; value.ForeColor = UiTheme.Primary;
            title.BackColor = value.BackColor = card.BackColor = UiTheme.Surface;
            card.AccessibleName = title.Text + ": " + value.Text;
            title.AccessibleName = title.Text; value.AccessibleName = value.Text;
        }

        internal void RestoreInventorySharedAppearance()
        {
            ApplyInventoryDataVisualTheme();
            ApplyInventoryPresentation(leftToRight);
            ApplyQuantityColumnFormats();
        }

        private void ApplyInventoryDataVisualTheme()
        {
            BackColor = UiTheme.Background;
            Panel header = Controls.Find("inventoryDataHeader", true).FirstOrDefault() as Panel;
            Label title = Controls.Find("inventoryDataTitle", true).FirstOrDefault() as Label;
            if (header != null) header.BackColor = UiTheme.Surface;
            if (title != null) title.ForeColor = UiTheme.Text;
            FilterPanel.BackColor = UiTheme.SurfaceVariant;
            SummaryPanel.BackColor = UiTheme.Background;
            StatusLabel.BackColor = UiTheme.SurfaceVariant;
            StatusLabel.ForeColor = UiTheme.MutedText;
        }
    }
}



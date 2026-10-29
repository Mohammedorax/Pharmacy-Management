using System;
using System.Data;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace project_fo_3
{
    internal sealed class InventoryIntegrityForm : Form
    {
        private readonly DataGridView grid = new DataGridView { Name = "inventoryIntegrityGrid", AccessibleName = "حركات المخزون التي تحتاج مراجعة" };
        private readonly Label status = new Label { Name = "inventoryIntegrityStatus" };
        private readonly bool previewMode;
        private bool leftToRight;
        private string statusArabic;
        private bool? statusSuccess;
        private int? statusCount;
        private InventoryIntegrityReadSession readSession;
        private CancellationTokenSource readCancellation;
        private int readGeneration;

        internal InventoryIntegrityForm() : this(false) { }

        internal InventoryIntegrityForm(bool previewMode)
        {
            this.previewMode = previewMode;
            leftToRight = UiDirection.IsLeftToRight;
            Text = "فحص مطابقة المخزون";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(760, 520);
            Size = new Size(940, 620);
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = true;
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            BuildLayout();
            Controls["inventoryIntegrityHeader"].Resize += (sender, args) => LayoutHeader();
            UiTheme.Apply(this);
            if (previewMode)
            {
                Control runtimeStatus = Controls["__UiStatusStrip"];
                if (runtimeStatus != null) { Controls.Remove(runtimeStatus); runtimeStatus.Dispose(); }
            }
            ApplyVisualChrome();
            // The shared grid quality handler sizes first; this screen then restores its audit priority columns.
            grid.DataBindingComplete += (sender, args) => ApplyGridPresentation();
            grid.CellFormatting += (sender, args) =>
            {
                if (!leftToRight || args.RowIndex < 0 || args.ColumnIndex < 0 ||
                    grid.Columns[args.ColumnIndex].Name != "الحالة" || args.Value == null) return;
                args.Value = Localize(Convert.ToString(args.Value));
                args.FormattingApplied = true;
            };
            if (previewMode)
                grid.DataSource = CreatePreviewIssues();
            ApplyPresentation(leftToRight);
            if (!previewMode)
            {
                Load += (sender, args) => LoadIssues();
                FormClosed += (sender, args) => CancelRead();
                Disposed += (sender, args) => CancelRead();
                UiSessionSubscription.Attach(this, () =>
                {
                    CancelRead();
                    readSession = null;
                    grid.DataSource = null;
                    SetStatus("تغيرت جلسة المستخدم. أعد تحميل القائمة.", false);
                });
            }
            else SetStatus("معاينة واجهة القراءة فقط؛ لم يُنفذ فحص على قاعدة البيانات.", null);
        }

        private string Localize(string arabic) { return leftToRight ? UiLanguageCatalog.Translate(arabic) : arabic; }

        private void SetStatus(string arabic, bool? success)
        {
            statusArabic = arabic;
            statusCount = null;
            statusSuccess = success;
            string localizedStatus = Localize(arabic);
            UiTheme.SetStatus(status, localizedStatus, success);
            status.AccessibleName = localizedStatus;
            status.BackColor = UiTheme.Background;
        }

        private void SetCountStatus(string arabicTemplate, int count, bool? success)
        {
            statusArabic = arabicTemplate;
            statusCount = count;
            statusSuccess = success;
            string localizedStatus = string.Format(Localize(arabicTemplate), count);
            UiTheme.SetStatus(status, localizedStatus, success);
            status.AccessibleName = localizedStatus;
            status.BackColor = UiTheme.Background;
        }

        internal void ApplyPresentation(bool useLeftToRight)
        {
            leftToRight = useLeftToRight;
            // This screen owns column order and widths in both directions.
            grid.Tag = UiTheme.PreserveLocalizedCustomGridColumns;
            Text = Localize("فحص مطابقة المخزون");
            Panel header = (Panel)Controls["inventoryIntegrityHeader"];
            header.Controls["inventoryIntegrityTitle"].Text = Text;
            header.Controls["inventoryIntegritySubtitle"].Text = Localize("فحص قراءة فقط: يراجع تسلسل حركات الصنف ومطابقة الرصيد الحالي مع آخر حركة مسجلة.");
            Panel footer = (Panel)Controls["inventoryIntegrityActions"];
            AntdUI.Button refresh = (AntdUI.Button)footer.Controls["refreshInventoryIntegrity"];
            AntdUI.Button close = (AntdUI.Button)footer.Controls["closeInventoryIntegrity"];
            refresh.Text = Localize("إعادة الفحص"); close.Text = Localize("إغلاق");
            refresh.AccessibleName = Localize("إعادة فحص مطابقة المخزون");
            close.AccessibleName = Localize("إغلاق فحص مطابقة المخزون");
            status.AccessibleName = Localize("نتيجة فحص مطابقة المخزون");
            grid.AccessibleName = Localize("حركات المخزون التي تحتاج مراجعة");
            UiDirection.Apply(this, leftToRight);
            ApplyGridPresentation();
            LayoutFooter(footer, refresh, close);
            LayoutHeader();
            ApplyVisualChrome();
            if (statusArabic != null)
            {
                if (statusCount.HasValue) SetCountStatus(statusArabic, statusCount.Value, statusSuccess);
                else SetStatus(statusArabic, statusSuccess);
            }
            grid.Invalidate();
        }

        private void ApplyGridPresentation()
        {
            UiDirection.Apply(grid, leftToRight);
            foreach (DataGridViewColumn column in grid.Columns) column.HeaderText = Localize(column.Name);
            ConfigureGridColumns();
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.ColumnHeadersHeight = 60;
        }

        private static DataTable CreatePreviewIssues()
        {
            var issues = new DataTable();
            issues.Columns.Add("الصنف", typeof(string));
            issues.Columns.Add("الشركة", typeof(string));
            issues.Columns.Add("رقم الحركة", typeof(int));
            issues.Columns.Add("تاريخ الحركة", typeof(DateTime));
            issues.Columns.Add("المصدر", typeof(string));
            issues.Columns.Add("رقم المرجع", typeof(string));
            issues.Columns.Add("الرصيد قبل", typeof(int));
            issues.Columns.Add("الرصيد بعد", typeof(int));
            issues.Columns.Add("الرصيد الحالي", typeof(int));
            issues.Columns.Add("الحالة", typeof(string));
            issues.Rows.Add("Amoxicillin 250 mg", "Global Pharma", 8325, new DateTime(2026, 9, 22),
                "PurchaseInvoice", "PUR-781", 8, 28, 31, "تسلسل الحركات غير متصل");
            issues.Rows.Add("باراسيتامول 500 mg", "شركة الدواء", 8341, new DateTime(2026, 9, 23),
                "SalesInvoice", "INV-1042", 24, 20, 18, "الرصيد الحالي لا يطابق آخر حركة");
            return issues;
        }

        private void ConfigureGridColumns()
        {
            foreach (DataGridViewColumn column in grid.Columns)
            {
                if (column.ValueType == typeof(decimal) || column.ValueType == typeof(int))
                    column.DefaultCellStyle.Format = "N0";
                if (column.ValueType == typeof(DateTime)) column.DefaultCellStyle.Format = "yyyy/MM/dd";
                if (column.Name == "الحالة") column.FillWeight = 170;
                if (column.Name == "الصنف") column.FillWeight = 155;
            }
            if (grid.Columns.Contains("الحالة"))
            {
                grid.Columns["الحالة"].DisplayIndex = Math.Min(1, grid.Columns.Count - 1);
                grid.Columns["الحالة"].MinimumWidth = 300;
                grid.Columns["الحالة"].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                grid.Columns["الحالة"].Width = 300;
            }
            if (grid.Columns.Contains("الصنف"))
            {
                grid.Columns["الصنف"].MinimumWidth = 200;
                grid.Columns["الصنف"].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                grid.Columns["الصنف"].Width = 200;
            }
        }

        private void BuildLayout()
        {
            Panel header = new Panel { Name = "inventoryIntegrityHeader", Dock = DockStyle.Top, Height = 82, Padding = new Padding(22, 12, 22, 10), BackColor = UiTheme.Background };
            Label title = new Label { Name = "inventoryIntegrityTitle", Dock = DockStyle.Top, Height = 34, Text = "فحص مطابقة المخزون", TextAlign = ContentAlignment.MiddleRight, Font = AppFonts.Create(16F, FontStyle.Bold) };
            Label subtitle = new Label { Name = "inventoryIntegritySubtitle", Dock = DockStyle.Fill, Text = "فحص قراءة فقط: يراجع تسلسل حركات الصنف ومطابقة الرصيد الحالي مع آخر حركة مسجلة.", TextAlign = ContentAlignment.MiddleRight };
            header.Controls.Add(subtitle);
            header.Controls.Add(title);

            Panel actions = new Panel { Name = "inventoryIntegrityActions", Dock = DockStyle.Bottom, Height = 64, BackColor = UiTheme.Background };
            AntdUI.Button refresh = DesktopComponents.Action("refreshInventoryIntegrity", "إعادة الفحص", AppIcon.Refresh);
            refresh.AccessibleName = "إعادة فحص مطابقة المخزون";
            refresh.Size = new Size(160, 44);
            refresh.Click += (sender, args) => LoadIssues();
            AntdUI.Button close = DesktopComponents.Action("closeInventoryIntegrity", "إغلاق", AppIcon.Close);
            close.AccessibleName = "إغلاق فحص مطابقة المخزون";
            close.Size = new Size(118, 44);
            close.Click += (sender, args) => Close();
            actions.Controls.Add(refresh);
            actions.Controls.Add(close);
            actions.Resize += (sender, args) => LayoutFooter(actions, refresh, close);

            status.Dock = DockStyle.Top;
            status.AccessibleRole = AccessibleRole.StatusBar;
            status.Height = 42;
            status.Padding = new Padding(24, 4, 24, 4);
            status.TextAlign = ContentAlignment.MiddleRight;
            status.AccessibleName = "نتيجة فحص مطابقة المخزون";

            AntdUI.Panel gridCard = DesktopComponents.Card("inventoryIntegrityGridCard");
            gridCard.Dock = DockStyle.Fill;
            gridCard.Padding = new Padding(12);
            grid.Dock = DockStyle.Fill;
            grid.ReadOnly = true;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AllowUserToOrderColumns = true;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.RowHeadersVisible = false;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            gridCard.Controls.Add(grid);
            Controls.Add(gridCard);
            Controls.Add(status);
            Controls.Add(actions);
            Controls.Add(header);
            UiTheme.ApplyDataGridQuality(grid, "لا توجد فروقات في تسلسل الحركات أو الرصيد الحالي");
            AntdThemeAdapter.Apply(gridCard);
            LayoutFooter(actions, refresh, close);
        }

        private void ApplyVisualChrome()
        {
            Panel header = (Panel)Controls["inventoryIntegrityHeader"];
            Panel footer = (Panel)Controls["inventoryIntegrityActions"];
            Control statusStrip = Controls["__UiStatusStrip"];
            if (statusStrip != null) Controls.SetChildIndex(statusStrip, Controls.Count - 1);
            header.BackColor = UiTheme.Background;
            Label title = (Label)header.Controls["inventoryIntegrityTitle"];
            Label subtitle = (Label)header.Controls["inventoryIntegritySubtitle"];
            title.BackColor = UiTheme.Background;
            title.ForeColor = UiTheme.Text;
            subtitle.BackColor = UiTheme.Background;
            subtitle.ForeColor = UiTheme.MutedText;
            status.BackColor = UiTheme.Background;
            status.ForeColor = UiTheme.MutedText;
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = UiTheme.Primary;
            grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = Color.White;
            foreach (DataGridViewColumn column in grid.Columns)
            {
                column.HeaderCell.Style.SelectionBackColor = UiTheme.Primary;
                column.HeaderCell.Style.SelectionForeColor = Color.White;
            }
            footer.BackColor = UiTheme.Background;
            AntdThemeAdapter.Apply((AntdUI.Panel)Controls["inventoryIntegrityGridCard"]);
            foreach (AntdUI.Button button in footer.Controls)
                DesktopComponents.StyleOutlinedFooterAction(button);
        }

        private void LayoutHeader()
        {
            Panel header = (Panel)Controls["inventoryIntegrityHeader"];
            Label title = (Label)header.Controls["inventoryIntegrityTitle"];
            Label subtitle = (Label)header.Controls["inventoryIntegritySubtitle"];
            int width = Math.Max(1, header.ClientSize.Width - header.Padding.Horizontal);
            title.Height = TextRenderer.MeasureText(title.Text, title.Font, new Size(width, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height + 4;
            int descriptionHeight = TextRenderer.MeasureText(subtitle.Text, subtitle.Font, new Size(width, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height + 4;
            header.Height = header.Padding.Vertical + title.Height + descriptionHeight;
            title.AccessibleName = title.Text;
            subtitle.AccessibleName = subtitle.Text;
        }

        private void LayoutFooter(Panel footer, AntdUI.Button refresh, AntdUI.Button close)
        {
            const int margin = 22;
            const int gap = 12;
            int x = leftToRight ? margin : footer.ClientSize.Width - margin - refresh.Width;
            refresh.SetBounds(x, 10, refresh.Width, 44);
            x += leftToRight ? refresh.Width + gap : -close.Width - gap;
            close.SetBounds(x, 10, close.Width, 44);
        }

        private async void LoadIssues()
        {
            if (previewMode)
            {
                SetStatus("المعاينة للعرض فقط؛ لم يُنفذ فحص على قاعدة البيانات.", null);
                return;
            }
            CancelRead();
            int generation = readGeneration;
            var cancellation = new CancellationTokenSource();
            readCancellation = cancellation;
            grid.DataSource = null;
            SetStatus("جارٍ فحص تسلسل حركات المخزون...", null);
            try
            {
                var session = readSession ?? (readSession = ApplicationServices.CreateInventoryIntegrityReadSession());
                var result = await session.LoadAsync(cancellation.Token);
                if (IsDisposed || Disposing || generation != readGeneration) return;
                session.EnsureContext();
                DataTable issues = InventoryStockPresentation.ToTable(result);
                grid.DataSource = issues;
                ApplyGridPresentation();

                if (issues.Rows.Count == 0)
                    SetStatus("فحص سليم: لا توجد فروقات في الحركات المسجلة أو الرصيد الحالي.", true);
                else
                    SetCountStatus("توجد {0} حركات تحتاج مراجعة. لم تُعدّل أي كمية تلقائياً.", issues.Rows.Count, false);
            }
            catch (Exception exception)
            {
                if (exception is OperationCanceledException || IsDisposed || Disposing || generation != readGeneration) return;
                ApplyLoadFailure();
                UiTheme.ShowOperationIssue(this, Localize("فحص مطابقة المخزون"), exception, leftToRight);
            }
        }

        private void CancelRead()
        {
            readGeneration++;
            var previous = readCancellation;
            readCancellation = null;
            if (previous != null) { previous.Cancel(); previous.Dispose(); }
        }

        private void ApplyLoadFailure()
        {
            SetStatus(grid.Rows.Count>0 ? "تعذر إكمال الفحص. النتائج المعروضة سابقة؛ أعد الفحص بعد التحقق من الاتصال." :
                "تعذر إكمال الفحص. تحقق من الاتصال ثم أعد المحاولة.", false);
        }
    }
}

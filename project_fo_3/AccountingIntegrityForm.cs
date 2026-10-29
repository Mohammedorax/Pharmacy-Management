using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;

namespace project_fo_3
{
    internal sealed class AccountingIntegrityReadSession
    {
        internal PharmacyManagement.Application.AccountingIntegrityReadService Service { get; }
        private readonly Func<bool> current;
        internal AccountingIntegrityReadSession(PharmacyManagement.Application.AccountingIntegrityReadService service, Func<bool> current)
        { Service = service ?? throw new ArgumentNullException(nameof(service)); this.current = current ?? throw new ArgumentNullException(nameof(current)); }
        internal void EnsureContext()
        { if (!current()) throw new UnauthorizedAccessException("تغير المستخدم أو الاتصال أو الصلاحية؛ أعد تحميل فحص سلامة القيود."); }
    }
    internal sealed class AccountingIntegrityForm : Form
    {
        private readonly AntdUI.DatePicker fromDate = DesktopComponents.DatePicker("integrityFromDate", "تاريخ بداية فحص سلامة القيود", DateTime.Today);
        private readonly AntdUI.DatePicker toDate = DesktopComponents.DatePicker("integrityToDate", "تاريخ نهاية فحص سلامة القيود", DateTime.Today);
        private readonly DataGridView grid = new DataGridView { Name = "accountingIntegrityGrid", AccessibleName = "القيود التي تحتاج مراجعة" };
        private readonly Label status = new Label { Name = "accountingIntegrityStatus" };
        private readonly bool previewMode;
        private readonly Func<AccountingIntegrityReadSession> sessionFactory;
        private bool leftToRight;
        private string statusArabic;
        private bool? statusSuccess;
        private int? statusCount;
        private System.Threading.CancellationTokenSource readCancellation;
        private int readGeneration;

        internal AccountingIntegrityForm() : this(false) { }

        internal AccountingIntegrityForm(bool previewMode) : this(previewMode, null) { }
        internal AccountingIntegrityForm(bool previewMode, Func<AccountingIntegrityReadSession> sessionFactory)
        {
            this.previewMode = previewMode;
            this.sessionFactory = sessionFactory ?? ApplicationServices.CreateAccountingIntegrityReadSession;
            leftToRight = UiDirection.IsLeftToRight;
            Text = "فحص سلامة القيود";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(760, 520);
            Size = new Size(940, 620);
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = true;
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            BuildLayout();
            Controls["accountingIntegrityHeader"].Resize += (sender, args) => LayoutHeader();
            UiTheme.Apply(this);
            if (previewMode)
            {
                Control runtimeStatus = Controls["__UiStatusStrip"];
                if (runtimeStatus != null) { Controls.Remove(runtimeStatus); runtimeStatus.Dispose(); }
            }
            ApplyVisualChrome();
            // The shared grid quality handler sizes first; this screen then restores its audit priority columns.
            grid.DataBindingComplete += (sender, args) => ApplyGridPresentation();
            fromDate.Value = new DateTime(DateTime.Today.Year, 1, 1);
            toDate.Value = DateTime.Today;
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
                Disposed += (sender, args) => { CancelRead(); ClearIssues(); };
                UiSessionSubscription.Attach(this, () =>
                {
                    CancelRead(); ClearIssues();
                    SetStatus("تغيرت جلسة المستخدم. أعد تحميل القائمة.", false);
                });
                fromDate.ValueChanged += (sender, args) => FiltersChanged();
                toDate.ValueChanged += (sender, args) => FiltersChanged();
                fromDate.TextChanged += (sender, args) => FiltersChanged();
                toDate.TextChanged += (sender, args) => FiltersChanged();
            }
            else SetStatus("معاينة واجهة القراءة فقط؛ لم يُنفذ فحص على قاعدة البيانات.", null);
        }

        private string Localize(string arabic) { return leftToRight ? UiLanguageCatalog.Translate(arabic) : arabic; }

        private void SetStatus(string arabic, bool? success)
        {
            statusArabic = arabic;
            statusSuccess = success;
            statusCount = null;
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
            Text = Localize("فحص سلامة القيود");
            Panel header = (Panel)Controls["accountingIntegrityHeader"];
            header.Controls["accountingIntegrityTitle"].Text = Text;
            header.Controls["accountingIntegritySubtitle"].Text = Localize("فحص قراءة فقط: يعرض القيود غير المتوازنة أو التي لا تحتوي أسطراً، ولا يصلحها تلقائياً.");
            TableLayoutPanel filters = (TableLayoutPanel)Controls["accountingIntegrityFilters"];
            filters.GetControlFromPosition(0, 0).Text = Localize("من تاريخ");
            filters.GetControlFromPosition(2, 0).Text = Localize("إلى تاريخ");
            fromDate.AccessibleName = Localize("تاريخ بداية فحص سلامة القيود");
            toDate.AccessibleName = Localize("تاريخ نهاية فحص سلامة القيود");
            Panel footer = (Panel)Controls["accountingIntegrityActions"];
            AntdUI.Button refresh = (AntdUI.Button)footer.Controls["refreshAccountingIntegrity"];
            AntdUI.Button close = (AntdUI.Button)footer.Controls["closeAccountingIntegrity"];
            refresh.Text = Localize("إعادة الفحص"); close.Text = Localize("إغلاق");
            refresh.AccessibleName = Localize("إعادة فحص سلامة القيود");
            close.AccessibleName = Localize("إغلاق فحص سلامة القيود");
            status.AccessibleName = Localize("نتيجة فحص سلامة القيود");
            grid.AccessibleName = Localize("القيود التي تحتاج مراجعة");
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
            issues.Columns.Add("رقم القيد", typeof(int));
            issues.Columns.Add("التاريخ", typeof(DateTime));
            issues.Columns.Add("المصدر", typeof(string));
            issues.Columns.Add("رقم المرجع", typeof(string));
            issues.Columns.Add("البيان", typeof(string));
            issues.Columns.Add("عدد الأسطر", typeof(int));
            issues.Columns.Add("إجمالي المدين", typeof(decimal));
            issues.Columns.Add("إجمالي الدائن", typeof(decimal));
            issues.Columns.Add("الفرق", typeof(decimal));
            issues.Columns.Add("الحالة", typeof(string));
            issues.Rows.Add(1037, new DateTime(2026, 9, 22), "Manual", "",
                "قيد مراجعة افتتاحي", 0, 0m, 0m, 0m, "قيد بلا أسطر");
            issues.Rows.Add(1042, new DateTime(2026, 9, 23), "SalesInvoice", "INV-1042",
                "فاتورة مبيعات — Batch 500 mg", 2, 1250.50m, 1200.00m, 50.50m, "القيد غير متوازن");
            return issues;
        }

        private void ConfigureGridColumns()
        {
            foreach (DataGridViewColumn column in grid.Columns)
            {
                if (column.ValueType == typeof(decimal)) column.DefaultCellStyle.Format = "N2";
                if (column.ValueType == typeof(DateTime)) column.DefaultCellStyle.Format = "yyyy/MM/dd";
                if (column.Name == "البيان") column.FillWeight = 190;
            }
            if (grid.Columns.Contains("الحالة"))
            {
                grid.Columns["الحالة"].DisplayIndex = Math.Min(1, grid.Columns.Count - 1);
                grid.Columns["الحالة"].MinimumWidth = 190;
                grid.Columns["الحالة"].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                grid.Columns["الحالة"].Width = 190;
            }
        }

        private void BuildLayout()
        {
            Panel header = new Panel { Name = "accountingIntegrityHeader", Dock = DockStyle.Top, Height = 82, Padding = new Padding(22, 12, 22, 10), BackColor = UiTheme.Background };
            Label title = new Label { Name = "accountingIntegrityTitle", Dock = DockStyle.Top, Height = 34, Text = "فحص سلامة القيود", TextAlign = ContentAlignment.MiddleRight, Font = AppFonts.Create(16F, FontStyle.Bold) };
            Label subtitle = new Label { Name = "accountingIntegritySubtitle", Dock = DockStyle.Fill, Text = "فحص قراءة فقط: يعرض القيود غير المتوازنة أو التي لا تحتوي أسطراً، ولا يصلحها تلقائياً.", TextAlign = ContentAlignment.MiddleRight };
            header.Controls.Add(subtitle);
            header.Controls.Add(title);

            TableLayoutPanel filters = new TableLayoutPanel { Name = "accountingIntegrityFilters", Dock = DockStyle.Top, Height = 62, ColumnCount = 4, Padding = new Padding(22, 10, 22, 6) };
            filters.RowCount = 1;
            filters.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            filters.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            filters.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            filters.Controls.Add(CreateCaption("من تاريخ"), 0, 0);
            filters.Controls.Add(fromDate, 1, 0);
            filters.Controls.Add(CreateCaption("إلى تاريخ"), 2, 0);
            filters.Controls.Add(toDate, 3, 0);
            fromDate.Dock = DockStyle.Fill;
            toDate.Dock = DockStyle.Fill;
            fromDate.Margin = new Padding(4, 0, 4, 0);
            toDate.Margin = new Padding(4, 0, 4, 0);
            fromDate.MinimumSize = new Size(0, 44);
            toDate.MinimumSize = new Size(0, 44);

            Panel actions = new Panel { Name = "accountingIntegrityActions", Dock = DockStyle.Bottom, Height = 64, BackColor = UiTheme.Background };
            AntdUI.Button refresh = DesktopComponents.Action("refreshAccountingIntegrity", "إعادة الفحص", AppIcon.Refresh);
            refresh.AccessibleName = "إعادة فحص سلامة القيود";
            refresh.Size = new Size(160, 44);
            refresh.Click += (sender, args) => LoadIssues();
            AntdUI.Button close = DesktopComponents.Action("closeAccountingIntegrity", "إغلاق", AppIcon.Close);
            close.AccessibleName = "إغلاق فحص سلامة القيود";
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
            status.AccessibleName = "نتيجة فحص سلامة القيود";

            AntdUI.Panel gridCard = DesktopComponents.Card("accountingIntegrityGridCard");
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
            Controls.Add(filters);
            Controls.Add(header);
            UiTheme.ApplyDataGridQuality(grid, "لا توجد قيود تحتاج مراجعة في الفترة المحددة");
            AntdThemeAdapter.Apply(fromDate);
            AntdThemeAdapter.Apply(toDate);
            AntdThemeAdapter.Apply(gridCard);
            LayoutFooter(actions, refresh, close);
        }

        private static Label CreateCaption(string text)
        {
            return new Label { AutoSize = true, Padding = new Padding(0, 6, 8, 0), Text = text, TextAlign = ContentAlignment.MiddleRight, Font = AppFonts.Create(9.5F, FontStyle.Bold) };
        }

        private void ApplyVisualChrome()
        {
            Panel header = (Panel)Controls["accountingIntegrityHeader"];
            Panel footer = (Panel)Controls["accountingIntegrityActions"];
            Control statusStrip = Controls["__UiStatusStrip"];
            if (statusStrip != null) Controls.SetChildIndex(statusStrip, Controls.Count - 1);
            header.BackColor = UiTheme.Background;
            Label title = (Label)header.Controls["accountingIntegrityTitle"];
            Label subtitle = (Label)header.Controls["accountingIntegritySubtitle"];
            title.BackColor = UiTheme.Background;
            title.ForeColor = UiTheme.Text;
            subtitle.BackColor = UiTheme.Background;
            subtitle.ForeColor = UiTheme.MutedText;
            TableLayoutPanel filters = (TableLayoutPanel)Controls["accountingIntegrityFilters"];
            filters.BackColor = UiTheme.Background;
            foreach (Control caption in filters.Controls)
                if (caption is Label) { caption.BackColor = UiTheme.Background; caption.ForeColor = UiTheme.Text; }
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
            AntdThemeAdapter.Apply((AntdUI.Panel)Controls["accountingIntegrityGridCard"]);
            foreach (AntdUI.Button button in footer.Controls)
                DesktopComponents.StyleOutlinedFooterAction(button);
        }

        private void LayoutHeader()
        {
            Panel header = (Panel)Controls["accountingIntegrityHeader"];
            Label title = (Label)header.Controls["accountingIntegrityTitle"];
            Label subtitle = (Label)header.Controls["accountingIntegritySubtitle"];
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

        private async void LoadIssues() { await LoadIssuesAsync(); }
        internal async System.Threading.Tasks.Task LoadIssuesAsync()
        {
            if (previewMode)
            {
                SetStatus("المعاينة للعرض فقط؛ لم يُنفذ فحص على قاعدة البيانات.", null);
                return;
            }
            CancelRead(); ClearIssues();
            DateTime startDate, endDate;
            if (!AntdDateInput.TryRead(fromDate, out startDate) || !AntdDateInput.TryRead(toDate, out endDate))
            {
                SetStatus("اختر تاريخ بداية ونهاية صحيحين للفحص.", false);
                return;
            }
            if (endDate < startDate)
            {
                SetStatus("تاريخ النهاية يجب أن يكون بعد تاريخ البداية أو مساوياً له.", false);
                return;
            }

            int generation = readGeneration;
            var cancellation = new System.Threading.CancellationTokenSource();
            readCancellation = cancellation;
            SetStatus("جارٍ فحص سلامة القيود...", null);
            try
            {
                var session = sessionFactory();
                var result = await session.Service.LoadAsync(startDate, endDate, cancellation.Token);
                if (IsDisposed || Disposing || generation != readGeneration) return;
                session.EnsureContext();
                DataTable issues = OperationalReportCatalog.ToDataTable(result);
                grid.DataSource = issues;
                ApplyGridPresentation();

                if (issues.Rows.Count == 0)
                    SetStatus("فحص سليم: لا توجد قيود غير متوازنة أو بلا أسطر في الفترة المحددة.", true);
                else
                    SetCountStatus("توجد {0} قيود تحتاج مراجعة محاسبية. لم يُنفذ أي إصلاح تلقائي.", issues.Rows.Count, false);
            }
            catch (Exception exception)
            {
                if (exception is OperationCanceledException || IsDisposed || Disposing || generation != readGeneration) return;
                ClearIssues();
                ApplyLoadFailure();
                UiTheme.ShowOperationIssue(this, Localize("فحص سلامة القيود"), exception, leftToRight);
            }
            finally
            {
                if (ReferenceEquals(readCancellation, cancellation))
                { readCancellation = null; cancellation.Dispose(); }
            }
        }

        private void FiltersChanged()
        { CancelRead(); ClearIssues(); SetStatus("تغيرت المرشحات؛ أعد عرض التقرير.", null); }
        private void CancelRead()
        {
            readGeneration++;
            var previous = readCancellation; readCancellation = null;
            if (previous != null) { previous.Cancel(); previous.Dispose(); }
        }
        private void ClearIssues()
        {
            var previous = grid.DataSource as DataTable;
            grid.DataSource = null;
            previous?.Dispose();
        }
        private void ApplyLoadFailure()
        {
            SetStatus(grid.Rows.Count>0 ? "تعذر إكمال الفحص. النتائج المعروضة سابقة؛ أعد الفحص بعد التحقق من الاتصال." :
                "تعذر إكمال الفحص. تحقق من الاتصال ثم أعد المحاولة.", false);
        }
    }
}

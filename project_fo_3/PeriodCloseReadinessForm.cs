using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;

namespace project_fo_3
{
    internal sealed class PeriodCloseReadinessForm : Form
    {
        private readonly AntdUI.DatePicker fromDate = DesktopComponents.DatePicker("periodReadinessFromDate", "تاريخ بداية فحص الجاهزية", DateTime.Today);
        private readonly AntdUI.DatePicker toDate = DesktopComponents.DatePicker("periodReadinessToDate", "تاريخ نهاية فحص الجاهزية", DateTime.Today);
        private readonly DataGridView grid = new DataGridView { Name = "periodReadinessGrid", AccessibleName = "نتائج فحص جاهزية الإقفال" };
        private readonly Label status = new Label { Name = "periodReadinessStatus" };
        private readonly bool previewMode;
        private bool leftToRight;
        internal bool PresentationLeftToRight => leftToRight;
        private string statusArabic;
        private bool? statusSuccess;

        internal PeriodCloseReadinessForm() : this(false) { }

        internal PeriodCloseReadinessForm(bool previewMode)
        {
            this.previewMode = previewMode;
            leftToRight = UiDirection.IsLeftToRight;
            Text = "جاهزية إقفال الفترة";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(760, 500);
            Size = new Size(940, 620);
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = true;
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            BuildLayout();
            Controls["periodReadinessHeader"].Resize += (sender, args) => LayoutHeader();
            UiTheme.Apply(this);
            if (previewMode)
            {
                Control runtimeStatus = Controls["__UiStatusStrip"];
                if (runtimeStatus != null) { Controls.Remove(runtimeStatus); runtimeStatus.Dispose(); }
            }
            ApplyVisualChrome();
            fromDate.Value = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            toDate.Value = DateTime.Today;
            grid.CellFormatting += (sender, args) =>
            {
                if (!leftToRight || args.RowIndex < 0 || args.ColumnIndex < 0 || args.Value == null) return;
                string column = grid.Columns[args.ColumnIndex].Name;
                if (column != "الفحص" && column != "الحالة" && column != "التفصيل") return;
                args.Value = Localize(Convert.ToString(args.Value));
                args.FormattingApplied = true;
            };
            if (previewMode) grid.DataSource = CreatePreviewResults();
            ApplyPresentation(leftToRight);
            if (!previewMode) Load += (sender, args) => LoadReadiness();
            else SetStatus("معاينة واجهة القراءة فقط؛ لم يُنفذ فحص على قاعدة البيانات.", null);
        }

        private string Localize(string arabic) { return leftToRight ? UiLanguageCatalog.Translate(arabic) : arabic; }

        private void SetStatus(string arabic, bool? success)
        {
            statusArabic = arabic;
            statusSuccess = success;
            string localizedStatus = Localize(arabic);
            UiTheme.SetStatus(status, localizedStatus, success);
            // Expose the live message itself to screen readers and UI automation.
            status.AccessibleName = localizedStatus;
            status.BackColor = UiTheme.Background;
        }

        internal void ApplyPresentation(bool useLeftToRight)
        {
            leftToRight = useLeftToRight;
            grid.Tag = UiTheme.PreserveLocalizedCustomGridColumns;
            Text = Localize("جاهزية إقفال الفترة");
            Panel header = (Panel)Controls["periodReadinessHeader"];
            header.AccessibleName = Text;
            header.Controls["periodReadinessTitle"].Text = Text;
            header.Controls["periodReadinessSubtitle"].Text = Localize("فحص قرار قراءة فقط قبل الإقفال. لا ينشئ قيود إقفال ولا يعالج الفروقات تلقائياً.");
            TableLayoutPanel filters = (TableLayoutPanel)Controls["periodReadinessFilters"];
            filters.AccessibleName = Localize("نطاق تاريخ فحص الجاهزية");
            filters.GetControlFromPosition(0, 0).Text = Localize("من تاريخ");
            filters.GetControlFromPosition(2, 0).Text = Localize("إلى تاريخ");
            fromDate.AccessibleName = Localize("تاريخ بداية فحص الجاهزية");
            toDate.AccessibleName = Localize("تاريخ نهاية فحص الجاهزية");
            Panel footer = (Panel)Controls["periodReadinessActions"];
            footer.AccessibleName = Localize("إجراءات فحص الجاهزية");
            Controls["periodReadinessGridCard"].AccessibleName = Localize("نتائج فحص جاهزية الإقفال");
            AntdUI.Button refresh = (AntdUI.Button)footer.Controls["refreshPeriodReadiness"];
            AntdUI.Button close = (AntdUI.Button)footer.Controls["closePeriodReadiness"];
            refresh.Text = Localize("فحص الجاهزية");
            close.Text = Localize("إغلاق");
            refresh.AccessibleName = Localize("فحص جاهزية إقفال الفترة");
            close.AccessibleName = Localize("إغلاق فحص جاهزية الفترة");
            status.AccessibleName = Localize("نتيجة جاهزية الإقفال");
            grid.AccessibleName = Localize("نتائج فحص جاهزية الإقفال");
            UiDirection.Apply(this, leftToRight);
            foreach (DataGridViewColumn column in grid.Columns) column.HeaderText = Localize(column.Name);
            LayoutResultColumns();
            LayoutFooter(footer, refresh, close);
            LayoutHeader();
            ApplyVisualChrome();
            if (statusArabic != null) SetStatus(statusArabic, statusSuccess);
            grid.Invalidate();
        }

        private static DataTable CreatePreviewResults()
        {
            var results = new DataTable();
            results.Columns.Add("الفحص", typeof(string));
            results.Columns.Add("عدد الملاحظات", typeof(int));
            results.Columns.Add("الحالة", typeof(string));
            results.Columns.Add("التفصيل", typeof(string));
            results.Rows.Add("سلامة القيود", 1, "يحتاج مراجعة", "قيود بلا أسطر أو غير متوازنة ضمن الفترة.");
            results.Rows.Add("مطابقة المشتريات", 0, "سليم", "إجمالي فاتورة شراء لا يطابق مجموع تفاصيلها.");
            results.Rows.Add("أرصدة مخزون سالبة", 0, "سليم", "رصيد حالي أقل من الصفر؛ الفحص يشمل المخزون الحالي.");
            results.Rows.Add("تسلسل حركات المخزون", 0, "سليم", "تطابق قبل/بعد كل حركة مع الرصيد الحالي؛ الفحص يشمل كل الحركات.");
            return results;
        }

        private void BuildLayout()
        {
            Panel header = new Panel { Name = "periodReadinessHeader", Dock = DockStyle.Top, Height = 82, Padding = new Padding(22, 12, 22, 10), BackColor = UiTheme.Background };
            Label title = new Label { Name = "periodReadinessTitle", Dock = DockStyle.Top, Height = 34, Text = Text, TextAlign = ContentAlignment.MiddleRight, Font = AppFonts.Create(16F, FontStyle.Bold) };
            Label subtitle = new Label { Name = "periodReadinessSubtitle", Dock = DockStyle.Fill, Text = "فحص قرار قراءة فقط قبل الإقفال. لا ينشئ قيود إقفال ولا يعالج الفروقات تلقائياً.", TextAlign = ContentAlignment.MiddleRight };
            header.Controls.Add(subtitle); header.Controls.Add(title);
            TableLayoutPanel filters = new TableLayoutPanel { Name = "periodReadinessFilters", Dock = DockStyle.Top, Height = 62, ColumnCount = 4, Padding = new Padding(22, 10, 22, 6) };
            filters.RowCount = 1;
            filters.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            filters.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F)); filters.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            filters.Controls.Add(Caption("من تاريخ"), 0, 0); filters.Controls.Add(fromDate, 1, 0); filters.Controls.Add(Caption("إلى تاريخ"), 2, 0); filters.Controls.Add(toDate, 3, 0);
            fromDate.Dock = DockStyle.Fill; toDate.Dock = DockStyle.Fill;
            fromDate.Margin = new Padding(4, 0, 4, 0); toDate.Margin = new Padding(4, 0, 4, 0);
            fromDate.MinimumSize = new Size(0, 44); toDate.MinimumSize = new Size(0, 44);
            Panel actions = new Panel { Name = "periodReadinessActions", Dock = DockStyle.Bottom, Height = 64, BackColor = UiTheme.Background };
            AntdUI.Button refresh = DesktopComponents.Action("refreshPeriodReadiness", "فحص الجاهزية", AppIcon.Compliance);
            refresh.AccessibleName = "فحص جاهزية إقفال الفترة";
            refresh.Size = new Size(160, 44);
            refresh.Click += (sender, args) => LoadReadiness();
            AntdUI.Button close = DesktopComponents.Action("closePeriodReadiness", "إغلاق", AppIcon.Close);
            close.AccessibleName = "إغلاق فحص جاهزية الفترة";
            close.Size = new Size(118, 44);
            close.Click += (sender, args) => Close();
            actions.Controls.Add(refresh); actions.Controls.Add(close);
            actions.Resize += (sender, args) => LayoutFooter(actions, refresh, close);
            status.Dock = DockStyle.Top; status.Height = 42; status.Padding = new Padding(24, 4, 24, 4); status.TextAlign = ContentAlignment.MiddleRight; status.AccessibleName = "نتيجة جاهزية الإقفال";
            AntdUI.Panel gridCard = DesktopComponents.Card("periodReadinessGridCard");
            gridCard.Dock = DockStyle.Fill;
            gridCard.Padding = new Padding(12);
            grid.Dock = DockStyle.Fill; grid.ReadOnly = true; grid.AllowUserToAddRows = false; grid.AllowUserToDeleteRows = false; grid.RowHeadersVisible = false; grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill; grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect; gridCard.Controls.Add(grid);
            Controls.Add(gridCard); Controls.Add(status); Controls.Add(actions); Controls.Add(filters); Controls.Add(header);
            UiTheme.ApplyDataGridQuality(grid, "لا توجد نتائج فحص");
            AntdThemeAdapter.Apply(fromDate);
            AntdThemeAdapter.Apply(toDate);
            AntdThemeAdapter.Apply(gridCard);
            LayoutFooter(actions, refresh, close);
        }

        private void ApplyVisualChrome()
        {
            Panel header = (Panel)Controls["periodReadinessHeader"];
            Panel footer = (Panel)Controls["periodReadinessActions"];
            Control statusStrip = Controls["__UiStatusStrip"];
            if (statusStrip != null) Controls.SetChildIndex(statusStrip, Controls.Count - 1);
            header.BackColor = UiTheme.Background;
            Label title = (Label)header.Controls["periodReadinessTitle"];
            Label subtitle = (Label)header.Controls["periodReadinessSubtitle"];
            title.BackColor = UiTheme.Background;
            title.ForeColor = UiTheme.Text;
            subtitle.BackColor = UiTheme.Background;
            subtitle.ForeColor = UiTheme.MutedText;
            TableLayoutPanel filters = (TableLayoutPanel)Controls["periodReadinessFilters"];
            filters.BackColor = UiTheme.Background;
            foreach (Control caption in filters.Controls)
                if (caption is Label) { caption.BackColor = UiTheme.Background; caption.ForeColor = UiTheme.Text; }
            status.BackColor = UiTheme.Background;
            status.ForeColor = UiTheme.MutedText;
            footer.BackColor = UiTheme.Background;
            AntdThemeAdapter.Apply((AntdUI.Panel)Controls["periodReadinessGridCard"]);
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = UiTheme.Primary;
            grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = Color.White;
            foreach (DataGridViewColumn column in grid.Columns)
            {
                column.HeaderCell.Style.SelectionBackColor = UiTheme.Primary;
                column.HeaderCell.Style.SelectionForeColor = Color.White;
            }
            foreach (AntdUI.Button button in footer.Controls)
                DesktopComponents.StyleOutlinedFooterAction(button);
        }

        private void LayoutHeader()
        {
            Panel header = (Panel)Controls["periodReadinessHeader"];
            Label title = (Label)header.Controls["periodReadinessTitle"];
            Label subtitle = (Label)header.Controls["periodReadinessSubtitle"];
            int width = Math.Max(1, header.ClientSize.Width - header.Padding.Horizontal);
            title.Height = TextRenderer.MeasureText(title.Text, title.Font, new Size(width, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height + 4;
            int descriptionHeight = TextRenderer.MeasureText(subtitle.Text, subtitle.Font, new Size(width, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height + 4;
            header.Height = header.Padding.Vertical + title.Height + descriptionHeight;
            title.AccessibleName = title.Text;
            subtitle.AccessibleName = subtitle.Text;
        }

        private void LayoutResultColumns()
        {
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
            grid.ScrollBars = ScrollBars.Both;
            foreach (DataGridViewColumn column in grid.Columns)
            {
                int contentWidth = column.Name == "التفصيل" ? 280 : column.Name == "الفحص" ? 180 :
                    column.Name == "الحالة" ? 120 : 75;
                int headerWidth = TextRenderer.MeasureText(column.HeaderText, grid.ColumnHeadersDefaultCellStyle.Font ?? grid.Font,
                    Size.Empty, TextFormatFlags.NoPrefix).Width + 20;
                column.MinimumWidth = Math.Max(contentWidth, headerWidth);
                if (column.Name == "التفصيل") column.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
                column.FillWeight = column.Name == "التفصيل" ? 45 : column.Name == "الفحص" ? 30 :
                    column.Name == "الحالة" ? 15 : 10;
            }
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

        private void LoadReadiness()
        {
            if (previewMode)
            {
                SetStatus("المعاينة للعرض فقط؛ لم يُنفذ فحص على قاعدة البيانات.", null);
                return;
            }
            DateTime startDate, endDate;
            if (!AntdDateInput.TryRead(fromDate, out startDate) || !AntdDateInput.TryRead(toDate, out endDate))
            {
                SetStatus("اختر تاريخ بداية ونهاية صحيحين للفحص.", false);
                return;
            }
            if(endDate<startDate)
            {
                SetStatus("تاريخ النهاية يجب أن يكون بعد تاريخ البداية أو مساوياً له.",false);
                return;
            }
            try
            {
                DataTable readiness = AccountingService.GetPeriodCloseReadiness(startDate, endDate);
                grid.DataSource = readiness;
                foreach (DataGridViewColumn column in grid.Columns) column.HeaderText = Localize(column.Name);
                LayoutResultColumns();
                bool ready = true;
                foreach (DataRow row in readiness.Rows) if (string.Equals(Convert.ToString(row["الحالة"]), "يحتاج مراجعة", StringComparison.Ordinal)) { ready = false; break; }
                SetStatus(ready ? "جاهز للمراجعة النهائية: لا توجد ملاحظات في الفحوص المعروضة. لم يُنفذ إقفال تلقائي." : "غير جاهز للإقفال: عالج الملاحظات الظاهرة ثم أعد الفحص. لم تُعدّل أي بيانات.", ready ? (bool?)true : false);
            }
            catch (Exception exception)
            {
                UiTheme.ShowOperationFailure(this, Localize("فحص جاهزية إقفال الفترة"), exception);
            }
        }

        private static Label Caption(string text) { return new Label { AutoSize = true, Padding = new Padding(0, 6, 8, 0), Text = text, Font = AppFonts.Create(9.5F, FontStyle.Bold) }; }
    }
}

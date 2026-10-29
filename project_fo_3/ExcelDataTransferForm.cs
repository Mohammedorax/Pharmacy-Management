using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using PharmacyManagement.Application;

namespace project_fo_3
{
    internal sealed class ExcelDataTransferForm : Form
    {
        internal enum ImportDestination { None, Medicines, Invoices, CashBoxes }

        private readonly bool previewMode;
        private bool exportInProgress;
        private bool arrangingLayout;
        private bool leftToRight;
        private bool rebuildingSources;
        private string statusArabic;
        private string statusSuffix;
        private object[] statusArguments = new object[0];
        private bool? statusSuccess;
        private readonly IReadOnlyList<OperationalReportSource> exportSources = ExcelDataExportService.GetSources();
        private readonly AntdUI.Select source = DesktopComponents.Select("excelTransferSource", "نوع البيانات");
        // AntdUI 2.4.11 reverses mixed Arabic/Latin fragments in an LTR Input.
        // A native editor inside an AntdUI frame preserves the exact search text.
        private readonly TextBox search = new TextBox
        {
            Name = "excelTransferSearch", BorderStyle = BorderStyle.None, AutoSize = false, MaxLength = 200
        };
        private readonly AntdUI.Panel searchFrame;
        private readonly AntdUI.DatePicker fromDate = DesktopComponents.DatePicker("excelTransferFrom", "من تاريخ", DateTime.Today.AddMonths(-1));
        private readonly AntdUI.DatePicker toDate = DesktopComponents.DatePicker("excelTransferTo", "إلى تاريخ", DateTime.Today);
        private readonly AntdUI.Button export = DesktopComponents.Action("excelTransferExport", "تصدير إكسل", AppIcon.Report);
        private readonly AntdUI.Button import = DesktopComponents.Action("excelTransferImport", "فتح الاستيراد", AppIcon.Database);
        private readonly AntdUI.Button close = DesktopComponents.Action("excelTransferClose", "إغلاق", AppIcon.Close);
        private readonly Label status = new Label { Name = "excelTransferStatus" };
        private readonly AntdUI.Panel card = DesktopComponents.Card("excelTransferCard");
        private readonly Panel footer = new Panel { Name = "excelTransferFooter" };

        internal ExcelDataTransferForm(bool previewMode = false)
        {
            this.previewMode = previewMode;
            leftToRight = UiDirection.IsLeftToRight;
            searchFrame = DesktopComponents.NativeTextFrame(search, "excelTransferSearchFrame");
            Name = "ExcelDataTransferForm"; Text = Localize("مركز Excel");
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96F, 96F);
            Size = new Size(800, 580); MinimumSize = new Size(520, 470);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable; MaximizeBox = true; MinimizeBox = true;
            RightToLeft = RightToLeft.Yes; RightToLeftLayout = true; KeyPreview = true;
            Font = AppFonts.Create(9.5F);
            KeyDown += (sender, args) => { if (args.KeyCode == Keys.Escape) Close(); };
            BuildLayout();
            UiTheme.Apply(this);
            UiTheme.UseDedicatedStatus(this);
            ApplyVisualTheme();
            // Changing WinForms RightToLeft recreates the editor handle. Never do it during typing.
            search.TextChanged += (sender, args) => { if (!search.Focused) ApplyInputDirection(); };
            search.Leave += (sender, args) => ApplyInputDirection();
            foreach (OperationalReportSource item in exportSources) source.Items.Add(DisplayTitle(item));
            source.SelectedIndex = 0;
            fromDate.MinDate = new DateTime(1753, 1, 1);
            toDate.MinDate = new DateTime(1753, 1, 1);
            fromDate.MaxDate = new DateTime(9999, 12, 30);
            toDate.MaxDate = new DateTime(9999, 12, 30);
            source.SelectedIndexChanged += (sender, args) => { if (!rebuildingSources) UpdateDateAvailability(); };
            UpdateDateAvailability();
            if (previewMode) { export.Enabled = false; SetStatusMessage("معاينة التصدير؛ لم تُقرأ بيانات ولم يُنشأ ملف."); }
            Resize += (sender, args) => LayoutCard();
            ApplyPresentation(leftToRight);
            Shown += (sender, args) => LayoutCard();
            FormClosing += (sender, args) =>
            {
                if (!exportInProgress) return;
                args.Cancel = true;
                SetStatusMessage("يجري حفظ ملف Excel؛ انتظر اكتمال العملية.");
            };
        }

        private string Localize(string arabicText)
        {
            return leftToRight ? UiLanguageCatalog.Translate(arabicText) : arabicText;
        }

        // Simulates LTR during layout tests while English remains disabled for production users.
        internal void ApplyPresentation(bool useLeftToRight)
        {
            leftToRight = useLeftToRight;
            Text = Localize("مركز Excel");
            Controls["excelTransferHeader"].Controls["excelTransferTitle"].Text = Text;
            Controls["excelTransferHeader"].Controls["excelTransferSubtitle"].Text =
                Localize("اختر البيانات للتصدير أو افتح مسار استيرادها؛ قوالب الاستيراد مستقلة عن كشوف التصدير.");
            string[] labels = { "نوع البيانات", "بحث اختياري", "من تاريخ", "إلى تاريخ" };
            var fields = (TableLayoutPanel)card.Controls["excelTransferFields"];
            for (int index = 0; index < labels.Length; index++)
                fields.Controls["excelTransferLabel" + index].Text = Localize(labels[index]);
            int selected = source.SelectedIndex;
            rebuildingSources = true;
            try
            {
                source.SelectedIndex = -1;
                source.Items.Clear();
                foreach (OperationalReportSource item in exportSources) source.Items.Add(Localize(DisplayTitle(item)));
                source.SelectedIndex = selected >= 0 && selected < exportSources.Count ? selected : 0;
            }
            finally { rebuildingSources = false; }
            source.AccessibleName = Localize("نوع البيانات");
            search.AccessibleName = Localize("بحث اختياري");
            fromDate.AccessibleName = Localize("من تاريخ");
            toDate.AccessibleName = Localize("إلى تاريخ");
            export.Text = Localize("تصدير إكسل");
            import.Text = Localize("فتح الاستيراد");
            import.AccessibleName = import.Text;
            close.Text = Localize("إغلاق");
            ApplyInputDirection();
            UpdateDateAvailability(false);
            UiDirection.Apply(this, leftToRight);
            ApplyVisualTheme();
            PresentStatus();
            ApplyInputDirection();
            LayoutCard();
        }
        private void BuildLayout()
        {
            var header = new Panel { Name = "excelTransferHeader", Dock = DockStyle.Top, Height = 100, Padding = new Padding(24, 12, 24, 10) };
            var title = new Label { Name = "excelTransferTitle", Dock = DockStyle.Top, Height = 38,
                Text = Localize("مركز Excel"), TextAlign = ContentAlignment.MiddleRight, Font = AppFonts.Create(16F, FontStyle.Bold) };
            var subtitle = new Label { Name = "excelTransferSubtitle", Dock = DockStyle.Fill,
                Text = Localize("اختر البيانات للتصدير أو افتح مسار استيرادها؛ قوالب الاستيراد مستقلة عن كشوف التصدير."), TextAlign = ContentAlignment.MiddleRight };
            header.Controls.Add(subtitle); header.Controls.Add(title);

            var body = new AntdUI.Panel { Name = "excelTransferBody", Dock = DockStyle.Fill,
                Padding = new Padding(18, 12, 18, 12), AutoScroll = true, Radius = 0, Shadow = 0, BorderWidth = 0 };
            card.Size = new Size(560, 220);
            card.Padding = new Padding(4);
            var fields = new TableLayoutPanel { Name = "excelTransferFields", Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 4,
                Padding = new Padding(16, 10, 16, 10) };
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 132));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int row = 0; row < 4; row++) fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            AddField(fields, "نوع البيانات", source, 0);
            AddField(fields, "بحث اختياري", searchFrame, 1);
            search.AccessibleName = Localize("بحث اختياري");
            AddField(fields, "من تاريخ", fromDate, 2);
            AddField(fields, "إلى تاريخ", toDate, 3);
            card.Controls.Add(fields); body.Controls.Add(card);
            fields.SizeChanged += (sender, args) => LayoutFieldInputs(fields);

            status.Dock = DockStyle.Bottom; status.Height = 52; status.Padding = new Padding(24, 8, 24, 8);
            status.TextAlign = ContentAlignment.MiddleRight; status.AccessibleName = Localize("حالة تصدير Excel");
            footer.Dock = DockStyle.Bottom; footer.Height = 68;
            var actions = new FlowLayoutPanel { Name = "excelTransferActions", Dock = DockStyle.Fill,
                WrapContents = false, AutoScroll = false, Padding = new Padding(20, 10, 20, 0) };
            export.Size = new Size(154, 44); export.Click += async (sender, args) => await Export();
            import.Size = new Size(146, 44); import.Click += (sender, args) => OpenImport();
            close.Size = new Size(108, 44); close.Click += (sender, args) => Close();
            actions.Controls.Add(export); actions.Controls.Add(import); actions.Controls.Add(close); footer.Controls.Add(actions);
            Controls.Add(body); Controls.Add(status); Controls.Add(footer); Controls.Add(header);
        }

        private void AddField(TableLayoutPanel fields, string text, Control input, int row)
        {
            fields.Controls.Add(new Label { Name = "excelTransferLabel" + row, Text = Localize(text), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight }, 0, row);
            input.Size = new Size(input is AntdUI.Select ? 300 : 190, 44);
            input.Margin = new Padding(6, 2, 6, 2);
            input.Dock = DockStyle.None; input.Anchor = AnchorStyles.Left;
            fields.Controls.Add(input, 1, row);
        }

        private void ApplyVisualTheme()
        {
            var header = Controls["excelTransferHeader"];
            var body = Controls["excelTransferBody"];
            header.BackColor = UiTheme.Background; body.BackColor = UiTheme.Background;
            var scrollBody = (AntdUI.Panel)body;
            scrollBody.Back = UiTheme.Background;
            scrollBody.Radius = 0; scrollBody.Shadow = 0; scrollBody.BorderWidth = 0;
            foreach (Control label in header.Controls)
            { label.BackColor = UiTheme.Background; label.ForeColor = label.Name == "excelTransferTitle" ? UiTheme.Text : UiTheme.MutedText; }
            card.BackColor = UiTheme.Surface;
            var fields = (TableLayoutPanel)card.Controls["excelTransferFields"];
            fields.BackColor = UiTheme.Surface;
            foreach (Control label in fields.Controls)
                if (label is Label) { label.BackColor = UiTheme.Surface; label.ForeColor = label.Name == "excelTransferNote" ? UiTheme.MutedText : UiTheme.Text; }
            AntdThemeAdapter.Apply(card);
            card.Shadow = 0;
            AntdThemeAdapter.Apply(source);
            DesktopComponents.StyleNativeTextFrame(searchFrame, search);
            AntdThemeAdapter.Apply(fromDate); AntdThemeAdapter.Apply(toDate);
            status.BackColor = UiTheme.Background; status.ForeColor = UiTheme.MutedText;
            footer.BackColor = UiTheme.Background;
            var actions = (FlowLayoutPanel)footer.Controls["excelTransferActions"];
            actions.BackColor = UiTheme.Background; actions.RightToLeft = RightToLeft.No;
            actions.FlowDirection = leftToRight ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;
            DesktopComponents.StyleOutlinedFooterAction(export);
            DesktopComponents.StyleOutlinedFooterAction(import);
            DesktopComponents.StyleOutlinedFooterAction(close);
            ApplyInputDirection();
        }

        private void LayoutFieldInputs(TableLayoutPanel fields)
        {
            int[] widths = fields.GetColumnWidths();
            if (widths.Length < 2 || widths[1] <= 0) return;
            int available = Math.Max(1, widths[1] - source.Margin.Horizontal);
            source.Width = Math.Min(Scale(300), available);
            searchFrame.Width = Math.Min(Scale(300), available);
            fromDate.Width = Math.Min(Scale(190), available);
            toDate.Width = Math.Min(Scale(190), available);
        }

        private void ApplyInputDirection()
        {
            UiDirection.ApplyContentDirection(search, leftToRight);
        }

        private void LayoutCard()
        {
            var body = Controls["excelTransferBody"] as AntdUI.Panel;
            if (arrangingLayout || body == null || body.ClientSize.Width <= 0) return;
            arrangingLayout = true;
            try
            {
                var header = Controls["excelTransferHeader"];
                var title = (Label)header.Controls["excelTransferTitle"];
                var subtitle = (Label)header.Controls["excelTransferSubtitle"];
                int textWidth = Math.Max(1, header.ClientSize.Width - header.Padding.Horizontal);
                title.Height = Math.Max(Scale(38), MeasureHeight(title, textWidth));
                header.Height = header.Padding.Vertical + title.Height + Math.Max(Scale(30), MeasureHeight(subtitle, textWidth));
                status.Height = Math.Max(Scale(44), MeasureHeight(status, Math.Max(1, ClientSize.Width - status.Padding.Horizontal)) + status.Padding.Vertical);
                export.Size = new Size(Math.Max(Scale(154), TextRenderer.MeasureText(export.Text, export.Font).Width + Scale(56)), Scale(44));
                import.Size = new Size(Math.Max(Scale(146), TextRenderer.MeasureText(import.Text, import.Font).Width + Scale(56)), Scale(44));
                close.Size = new Size(Math.Max(Scale(108), TextRenderer.MeasureText(close.Text, close.Font).Width + Scale(56)), Scale(44));
                var actions = (FlowLayoutPanel)footer.Controls["excelTransferActions"];
                int availableActionsWidth = Math.Max(1, footer.ClientSize.Width - actions.Padding.Horizontal);
                int rowWidth = 0;
                int rows = 1;
                foreach (Control action in actions.Controls)
                {
                    int actionWidth = action.Width + action.Margin.Horizontal;
                    if (rowWidth > 0 && rowWidth + actionWidth > availableActionsWidth)
                    { rows++; rowWidth = 0; }
                    rowWidth += actionWidth;
                }
                actions.WrapContents = rows > 1;
                footer.Height = Math.Max(Scale(68), actions.Padding.Vertical +
                    rows * (Scale(44) + Scale(6)) + Scale(8));
                // Keep a full-height editor; short windows scroll the body, never compress its rows.
                // Physical scroll coordinates are LTR; the field grid still follows the chosen language.
                body.RightToLeft = RightToLeft.No;
                var fields = (TableLayoutPanel)card.Controls["excelTransferFields"];
                fields.RightToLeft = leftToRight ? RightToLeft.No : RightToLeft.Yes;
                fields.ColumnStyles[0].Width = Scale(132);
                foreach (RowStyle row in fields.RowStyles) row.Height = Scale(48);
                foreach (Control input in new Control[] { source, searchFrame, fromDate, toDate }) input.Height = Scale(44);
                int available = Math.Max(1, body.ClientSize.Width - body.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth);
                int width = Math.Min(Scale(560), available);
                int height = Scale(192) + fields.Padding.Vertical + card.Padding.Vertical + (int)Math.Ceiling(card.BorderWidth * 2);
                card.SetBounds(Math.Max(0, (available - width) / 2), 0, width, height);
                fields.PerformLayout();
                LayoutFieldInputs(fields);
            }
            finally { arrangingLayout = false; }
        }

        private int Scale(int logical) { return (int)Math.Round(logical * DeviceDpi / 96F); }
        private static int MeasureHeight(Label label, int width)
        {
            return TextRenderer.MeasureText(label.Text, label.Font, new Size(width, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height;
        }

        private void UpdateDateAvailability(bool updateMessage = true)
        {
            OperationalReportSource item = SelectedSource();
            source.AccessibleDescription = item == null ? string.Empty : Localize(ExcelDataExportService.GetSheetName(item.Key));
            bool usesDates = item != null && item.UsesDateRange;
            fromDate.Enabled = !exportInProgress && usesDates; toDate.Enabled = !exportInProgress && usesDates;
            ImportDestination destination = ImportDestinationForSource(item?.Key);
            import.Enabled = !previewMode && !exportInProgress && destination != ImportDestination.None;
            if (!previewMode && updateMessage)
            {
                string detail = item != null && item.Key == "CashBoxes"
                ? "تُصدّر تعريفات الصناديق وإسناد الكاشير فقط دون أرصدة مالية، ويمكن استيراد الملف من شاشة صناديق النقد."
                : destination == ImportDestination.Invoices
                ? "يفتح استيراد الفواتير شاشة واحدة للبيع والشراء؛ استخدم قالبها المستقل وراجع البيانات قبل الاعتماد."
                : destination == ImportDestination.Medicines
                ? "كشوف التصدير للتحليل؛ افتح الاستيراد لتنزيل قالب مستقل ومراجعة البيانات قبل الاعتماد."
                : item != null && item.Key == "CashLedger"
                ? "هذا كشف حساب النقد المشترك، ولا يحدد أرصدة كل كاشير. يُرفض التصدير عند 2,000 صف."
                : usesDates
                    ? "يُرفض التصدير عند 2,000 صف؛ ضيّق الفترة أو استخدم البحث."
                    : "يُرفض التصدير عند 2,000 صف؛ استخدم البحث لتضييق النتائج.";
                SetStatusMessage(detail);
                statusSuffix = destination == ImportDestination.None ? "هذا النوع للتصدير فقط؛ لا يوجد له استيراد مباشر." : null;
                PresentStatus(); LayoutCard();
            }
        }

        private OperationalReportSource SelectedSource()
        {
            int index = source.SelectedIndex;
            return index >= 0 && index < exportSources.Count ? exportSources[index] : null;
        }

        private static string DisplayTitle(OperationalReportSource item)
        {
            return item.Key == "LegacySalesLines" ? item.PickerTitle : ExcelDataExportService.GetSheetName(item.Key);
        }

        internal static ImportDestination ImportDestinationForSource(string sourceKey)
        {
            switch (sourceKey)
            {
                case "Medicines": return ImportDestination.Medicines;
                case "SalesLines":
                case "PurchaseLines": return ImportDestination.Invoices;
                case "CashBoxes": return ImportDestination.CashBoxes;
                default: return ImportDestination.None;
            }
        }

        private void OpenImport()
        {
            if (previewMode || exportInProgress) return;
            if (!PermissionService.CanOpen(AppPermission.Settings))
            { SetStatusMessage("استيراد الإعدادات متاح للمدير فقط.", false); return; }
            switch (ImportDestinationForSource(SelectedSource()?.Key))
            {
                case ImportDestination.Medicines:
                    using (var form = new MedicineCatalogImportForm()) form.ShowDialog(this);
                    break;
                case ImportDestination.Invoices:
                    using (var form = new InvoiceImportDraftForm()) form.ShowDialog(this);
                    break;
                case ImportDestination.CashBoxes:
                    using (var form = new CashBoxManagementForm()) form.ShowDialog(this);
                    break;
                default:
                    SetStatusMessage("هذا النوع للتصدير فقط؛ لا يوجد له استيراد مباشر.", false);
                    break;
            }
        }

        private async Task Export()
        {
            if (previewMode || exportInProgress) return;
            if (!PermissionService.CanOpen(AppPermission.Settings)) { SetStatusMessage("تصدير الإعدادات متاح للمدير فقط.", false); return; }
            OperationalReportSource item = SelectedSource();
            if (item == null) { SetStatusMessage("اختر نوع البيانات أولاً.", false); return; }
            DateTime start = DateTime.Today;
            DateTime end = DateTime.Today;
            if (item.UsesDateRange && (!AntdDateInput.TryRead(fromDate, out start) ||
                !AntdDateInput.TryRead(toDate, out end) || start > end))
            { SetStatusMessage("فترة التصدير غير صحيحة.", false); return; }
            string searchText;
            try { searchText = ReportSearchCriteria.Normalize(search.Text); }
            catch (InvalidOperationException exception)
            {
                SetStatusMessage(exception.Message, false);
                search.Focus();
                return;
            }
            using (var dialog = new SaveFileDialog { Title = Localize("تصدير بيانات Excel"), Filter = "Excel Workbook (*.xlsx)|*.xlsx",
                DefaultExt = "xlsx", AddExtension = true, FileName = item.Key + "-" + DateTime.Today.ToString("yyyyMMdd") + ".xlsx" })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                // Snapshot UI values before the worker starts; no WinForms control is read off the UI thread.
                string sourceKey = item.Key;
                string target = dialog.FileName;
                exportInProgress = true;
                SetExportBusy(true);
                try
                {
                    int rows = await Task.Run(() => ExcelDataExportService.Export(sourceKey, start, end, searchText, target));
                    if (rows >= ExcelWorkbookWriter.MaximumCompleteExportRows)
                        SetStatusMessage("وصل المصدر إلى 2,000 صف؛ لم يُحفظ ملف جديد وبقي الملف السابق دون تغيير. ضيّق الفترة أو البحث.", false);
                    else
                        SetStatusMessageWithValues("تم تصدير {0} صفًا إلى Excel.", true, rows);
                }
                catch (Exception exception)
                {
                    if (item.Key == "CashBoxes" && exception is SqlException sql && sql.Number == 208)
                        SetStatusMessage("تعريفات الصناديق تحتاج ترحيل قاعدة البيانات قبل تصديرها.", false);
                    else
                    {
                        SetStatusMessage("تعذر تأكيد تصدير الملف؛ راجع الملف والمسار قبل إعادة المحاولة.", false);
                        UiTheme.ShowOperationIssue(this, Localize("تصدير إكسل"), exception, leftToRight);
                    }
                }
                finally { exportInProgress = false; SetExportBusy(false); }
            }
        }

        private void SetExportBusy(bool busy)
        {
            source.Enabled = !busy; search.Enabled = !busy;
            fromDate.Enabled = !busy && SelectedSource()?.UsesDateRange == true;
            toDate.Enabled = !busy && SelectedSource()?.UsesDateRange == true;
            export.Enabled = !busy && !previewMode;
            import.Enabled = !busy && !previewMode &&
                ImportDestinationForSource(SelectedSource()?.Key) != ImportDestination.None;
            close.Enabled = !busy;
            if (busy) SetStatusMessage("جارٍ تجهيز البيانات وحفظ ملف Excel...");
        }

        private void SetStatusMessage(string arabic, bool? success = null)
        {
            SetStatusMessageWithValues(arabic, success, new object[0]);
        }
        private void SetStatusMessageWithValues(string arabic, bool? success, params object[] arguments)
        {
            statusArabic = arabic; statusSuffix = null;
            statusArguments = arguments; statusSuccess = success;
            PresentStatus();
            LayoutCard();
        }
        private void PresentStatus()
        {
            string message = statusArguments.Length == 0 ? Localize(statusArabic ?? string.Empty) :
                string.Format(Localize(statusArabic ?? string.Empty), statusArguments);
            if (statusSuffix != null) message += " " + Localize(statusSuffix);
            UiTheme.SetStatus(status, message, statusSuccess);
            status.AccessibleName = status.Text;
            status.AccessibleRole = AccessibleRole.StatusBar;
            status.AutoEllipsis = false; status.UseMnemonic = false;
        }

    }
}

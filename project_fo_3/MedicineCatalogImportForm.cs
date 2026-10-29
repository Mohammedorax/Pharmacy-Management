using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using PharmacyManagement.Application;

namespace project_fo_3
{
    internal sealed class MedicineCatalogImportForm : Form
    {
        private readonly bool previewMode;
        private readonly Label fileName = new Label { Name = "catalogImportFileName" };
        private readonly Label status = new Label { Name = "catalogImportStatus" };
        private readonly DataGridView grid = new DataGridView { Name = "catalogImportGrid", AccessibleName = "معاينة أصناف Excel" };
        private readonly AntdUI.Button choose = DesktopComponents.Action("catalogImportChoose", "اختيار ملف", AppIcon.Search);
        private readonly AntdUI.Button template = DesktopComponents.Action("catalogImportTemplate", "تنزيل قالب", AppIcon.Report);
        private readonly AntdUI.Button approve = DesktopComponents.Action("catalogImportApprove", "اعتماد الأصناف", AppIcon.Confirm);
        private readonly AntdUI.Button details = DesktopComponents.Action("catalogImportErrors", "تفاصيل الأخطاء", AppIcon.Compliance);
        private readonly AntdUI.Button close = DesktopComponents.Action("catalogImportClose", "إغلاق", AppIcon.Close);
        private MedicineCatalogWorkbookPreview preview;
        private bool operationInProgress;
        private bool leftToRight;
        private string selectedFileName;
        private string statusSource;
        private object[] statusArguments = new object[0];
        private bool? statusSuccess;
        private bool previewStatus;
        private bool layingOutContent;

        internal MedicineCatalogImportForm(bool previewMode = false)
        {
            this.previewMode = previewMode;
            leftToRight = UiDirection.IsLeftToRight;
            Name = "MedicineCatalogImportForm"; Text = Localize("استيراد ملف إكسل لتعريفات الأصناف");
            ClientSize = new Size(980, 680); MinimumSize = new Size(640, 480);
            FormBorderStyle = FormBorderStyle.Sizable; MaximizeBox = true; MinimizeBox = true;
            StartPosition = FormStartPosition.CenterParent;
            RightToLeft = RightToLeft.Yes; RightToLeftLayout = true;
            Font = AppFonts.Create(9.5F); KeyPreview = true;
            KeyDown += (sender, args) => { if (args.KeyCode == Keys.Escape) Close(); };
            BuildLayout(); UiTheme.Apply(this); ApplyVisualTheme();
            Resize += (sender, args) => LayoutActions();
            approve.Enabled = false; details.Enabled = false;
            if (previewMode)
            {
                choose.Enabled = false; template.Enabled = false;
                DataTable sample = MedicineCatalogWorkbook.CreateTemplate();
                sample.Rows.Add("باراسيتامول 500 mg", "6281234567890", "10", "15",
                    "2026-01-01", "2027-01-01", "شركة الدواء");
                grid.DataSource = sample;
                SetStatus("معاينة مرئية؛ لم يُقرأ ملف أو تُفتح قاعدة بيانات.", null);
            }
            else SetStatus("استورد تعريفات جديدة برصيد صفر؛ أدخل الكميات وتكلفتها عبر شاشة المشتريات المعتمدة.", null);
            ApplyPresentation(leftToRight);
            if (previewMode) UiTheme.RemoveStatusStrip(this);
            FormClosing += (sender, args) =>
            {
                if (!operationInProgress) return;
                args.Cancel = true;
                SetStatus("العملية جارية؛ انتظر اكتمال المعاينة أو الاستيراد.", null);
            };
        }

        private string Localize(string arabicText)
        {
            return leftToRight ? UiLanguageCatalog.Translate(arabicText) : arabicText;
        }

        // English remains unavailable globally until all screens pass review; tests exercise this form's LTR layout.
        internal void ApplyPresentation(bool useLeftToRight)
        {
            leftToRight = useLeftToRight;
            Text = Localize("استيراد ملف إكسل لتعريفات الأصناف");
            Panel header = (Panel)Controls["catalogImportHeader"];
            header.Controls["catalogImportTitle"].Text = Text;
            header.Controls["catalogImportHint"].Text = Localize(
                "حمّل القالب، أدخل الأصناف والشركة المسجلة، ثم راجع الصفوف قبل الاعتماد. أدخل الكميات لاحقًا من المشتريات.");
            choose.Text = Localize("اختيار ملف");
            template.Text = Localize("تنزيل قالب");
            approve.Text = Localize("اعتماد الأصناف");
            details.Text = Localize("تفاصيل الأخطاء");
            close.Text = Localize("إغلاق");
            choose.AccessibleName = choose.Text;
            template.AccessibleName = template.Text;
            approve.AccessibleName = approve.Text;
            details.AccessibleName = details.Text;
            close.AccessibleName = close.Text;
            grid.AccessibleName = Localize("معاينة أصناف Excel");
            fileName.Text = selectedFileName == null ? Localize("لم يتم اختيار ملف.") :
                string.Format(Localize("الملف: {0}"), selectedFileName);
            fileName.AccessibleName = fileName.Text;
            UiDirection.Apply(this, leftToRight);
            UiTheme.ApplyStatusStripPresentation(this, leftToRight);
            ApplyGridHeaders();
            ApplyVisualTheme();
            LayoutContent();
        }

        private void ApplyGridHeaders()
        {
            foreach (DataGridViewColumn column in grid.Columns)
            {
                string sourceTitle = string.IsNullOrWhiteSpace(column.DataPropertyName)
                    ? column.HeaderText : column.DataPropertyName;
                column.HeaderText = Localize(sourceTitle);
            }
        }
        private void BuildLayout()
        {
            var header = new Panel { Name = "catalogImportHeader", Dock = DockStyle.Top,
                Height = 108, Padding = new Padding(24, 12, 24, 8) };
            var title = new Label { Name = "catalogImportTitle", Dock = DockStyle.Top, Height = 38,
                Text = "استيراد ملف إكسل لتعريفات الأصناف", TextAlign = ContentAlignment.MiddleRight,
                Font = AppFonts.Create(16F, FontStyle.Bold) };
            var hint = new Label { Name = "catalogImportHint", Dock = DockStyle.Fill,
                Text = "حمّل القالب، أدخل الأصناف والشركة المسجلة، ثم راجع الصفوف قبل الاعتماد. أدخل الكميات لاحقًا من المشتريات.",
                TextAlign = ContentAlignment.MiddleRight };
            header.Controls.Add(hint); header.Controls.Add(title);

            var info = new Panel { Name = "catalogImportInfo", Dock = DockStyle.Bottom,
                Height = 80, Padding = new Padding(24, 4, 24, 4) };
            fileName.Dock = DockStyle.Top; fileName.Height = 29;
            fileName.Text = Localize("لم يتم اختيار ملف."); fileName.TextAlign = ContentAlignment.MiddleRight;
            fileName.AutoEllipsis = true;
            status.Dock = DockStyle.Fill; status.TextAlign = ContentAlignment.MiddleRight;
            info.Controls.Add(status); info.Controls.Add(fileName);

            var footer = new FlowLayoutPanel { Name = "catalogImportFooter", Dock = DockStyle.Bottom,
                Height = 68, WrapContents = false, AutoScroll = false, Padding = new Padding(20, 10, 20, 8) };
            choose.Size = new Size(124, 44); choose.Click += async (sender, args) => await ChooseFileAsync();
            template.Size = new Size(132, 44); template.Click += (sender, args) => SaveTemplate();
            approve.Size = new Size(146, 44); approve.Click += async (sender, args) => await ImportAsync();
            close.Size = new Size(100, 44); close.Click += (sender, args) => Close();
            details.Size = new Size(124, 44); details.Click += (sender, args) => ShowAllErrors();
            footer.Controls.Add(choose); footer.Controls.Add(template);
            footer.Controls.Add(details); footer.Controls.Add(approve); footer.Controls.Add(close);

            grid.Dock = DockStyle.Fill; grid.ReadOnly = true;
            grid.Tag = UiTheme.PreserveLocalizedGridDirection;
            grid.AllowUserToAddRows = false; grid.AllowUserToDeleteRows = false;
            UiTheme.ApplyDataGridQuality(grid, Localize("اختر ملف Excel لعرض الأصناف."));
            Controls.Add(grid); Controls.Add(info); Controls.Add(footer); Controls.Add(header);
        }

        private void ApplyVisualTheme()
        {
            Panel header = (Panel)Controls["catalogImportHeader"];
            Panel info = (Panel)Controls["catalogImportInfo"];
            FlowLayoutPanel footer = (FlowLayoutPanel)Controls["catalogImportFooter"];
            foreach (Control surface in new Control[] { header, info, footer }) surface.BackColor = UiTheme.Background;
            foreach (Label label in header.Controls.OfType<Label>())
            { label.BackColor = UiTheme.Background; label.ForeColor = label.Name == "catalogImportTitle" ? UiTheme.Text : UiTheme.MutedText; }
            fileName.BackColor = UiTheme.Background; fileName.ForeColor = UiTheme.Text;
            status.BackColor = UiTheme.Background; status.ForeColor = UiTheme.MutedText;
            footer.RightToLeft = RightToLeft.No;
            footer.FlowDirection = leftToRight ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;
            foreach (AntdUI.Button button in new[] { choose, template, details, approve, close })
                DesktopComponents.StyleOutlinedFooterAction(button);
            grid.ColumnHeadersHeight = 52;
            LayoutActions();
        }

        private void LayoutActions()
        {
            var footer = Controls["catalogImportFooter"] as FlowLayoutPanel;
            if (footer == null) return;
            int available = Math.Max(1, footer.ClientSize.Width - footer.Padding.Horizontal);
            double scale = DeviceDpi / 96.0;
            int actionHeight = Math.Max(44, (int)Math.Round(44 * scale));
            int inset = Math.Max(4, (int)Math.Round(4 * scale));
            int gap = Math.Max(6, (int)Math.Round(6 * scale));
            int rows = 1, used = 0;
            foreach (AntdUI.Button action in footer.Controls.OfType<AntdUI.Button>())
            {
                action.Size = new Size(Math.Max((int)Math.Round(100 * scale), TextRenderer.MeasureText(action.Text, action.Font).Width + (int)Math.Round(36 * scale)), actionHeight);
                action.Margin = new Padding(inset, 0, inset, gap);
                int occupied = action.Width + action.Margin.Horizontal;
                if (used > 0 && used + occupied > available) { rows++; used = 0; }
                used += occupied;
            }
            footer.WrapContents = true;
            footer.Height = footer.Padding.Vertical + rows * (actionHeight + gap);
            footer.PerformLayout();
            LayoutContent();
        }

        private string BuildStatusText()
        {
            if (!previewStatus || preview == null)
                return string.Format(Localize(statusSource ?? string.Empty), statusArguments);
            string message = string.Format(Localize("صفوف الملف: {0} — أخطاء: {1}"), preview.Table.Rows.Count, preview.Issues.Count);
            if (preview.Issues.Count > 0)
                message += " — " + string.Join(leftToRight ? "; " : "؛ ",
                    preview.Issues.Take(2).Select(issue => issue.Format(leftToRight))) +
                    (preview.Issues.Count > 2 ? (leftToRight ? "; …" : "؛ …") : string.Empty);
            return message;
        }

        private void LayoutContent()
        {
            if (layingOutContent || IsDisposed) return;
            var header = Controls["catalogImportHeader"] as Panel;
            var info = Controls["catalogImportInfo"] as Panel;
            var footer = Controls["catalogImportFooter"] as FlowLayoutPanel;
            if (header == null || info == null || footer == null) return;
            layingOutContent = true;
            SuspendLayout();
            try
            {
                double scale = Math.Max(96, DeviceDpi) / 96.0;
                var hint = (Label)header.Controls["catalogImportHint"];
                var title = (Label)header.Controls["catalogImportTitle"];
                int width = Math.Max(1, ClientSize.Width - header.Padding.Horizontal);
                var flags = TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix |
                    (leftToRight ? 0 : TextFormatFlags.RightToLeft);
                hint.AutoEllipsis = false; hint.UseMnemonic = false;
                hint.AccessibleName = hint.Text;
                title.Height = (int)Math.Round(38 * scale);
                header.Height = header.Padding.Vertical + title.Height +
                    TextRenderer.MeasureText(hint.Text, hint.Font, new Size(width, int.MaxValue), flags).Height +
                    (int)Math.Round(4 * scale);
                fileName.Height = (int)Math.Round(29 * scale);
                string fullText = BuildStatusText();
                string visibleText = fullText;
                int statusWidth = Math.Max(1, ClientSize.Width - info.Padding.Horizontal);
                int textHeight = TextRenderer.MeasureText(fullText, status.Font,
                    new Size(statusWidth, int.MaxValue), flags).Height;
                int available = ClientSize.Height - header.Height - footer.Height - fileName.Height -
                    info.Padding.Vertical - (int)Math.Round(124 * scale);
                bool oversizedToken = fullText.Split((char[])null, StringSplitOptions.RemoveEmptyEntries)
                    .Any(token => TextRenderer.MeasureText(token, status.Font).Width > statusWidth);
                if (previewStatus && preview != null && preview.Issues.Count > 0 &&
                    (textHeight > available || oversizedToken))
                {
                    visibleText = string.Format(Localize("صفوف الملف: {0} — أخطاء: {1}"),
                        preview.Table.Rows.Count, preview.Issues.Count) + " — " +
                        (leftToRight ? "Open error details to review all issues." : "افتح تفاصيل الأخطاء لمراجعتها كاملة.");
                    textHeight = TextRenderer.MeasureText(visibleText, status.Font,
                        new Size(statusWidth, int.MaxValue), flags).Height;
                }
                status.AutoEllipsis = false; status.UseMnemonic = false;
                status.AccessibleRole = AccessibleRole.StatusBar;
                UiTheme.SetStatus(status, visibleText, statusSuccess);
                status.AccessibleName = status.Text;
                status.AccessibleDescription = fullText;
                info.Height = info.Padding.Vertical + fileName.Height +
                    Math.Max((int)Math.Round(32 * scale), textHeight + (int)Math.Round(4 * scale));
            }
            finally { ResumeLayout(true); layingOutContent = false; }
        }

        private async Task ChooseFileAsync()
        {
            if (previewMode || operationInProgress) return;
            using (var dialog = new OpenFileDialog { Title = Localize("اختيار ملف أصناف Excel"),
                Filter = "Excel Workbook (*.xlsx)|*.xlsx" })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                await LoadFileAsync(dialog.FileName);
            }
        }

        // Shared with QA so the selected workbook can be tested without automating the OS dialog.
        internal async Task LoadFileAsync(string selectedPath)
        {
            if (previewMode || operationInProgress || string.IsNullOrWhiteSpace(selectedPath)) return;
            preview = null; grid.DataSource = null; approve.Enabled = false; details.Enabled = false;
            selectedFileName = Path.GetFileName(selectedPath);
            fileName.Text = string.Format(Localize("الملف: {0}"), selectedFileName);
            operationInProgress = true;
            SetBusy(true);
            SetStatus("جارٍ قراءة ملف Excel والتحقق من صفوفه...", null);
            try
            {
                MedicineCatalogWorkbookPreview next = await Task.Run(() => MedicineCatalogWorkbook.Read(selectedPath));
                ShowPreview(next);
            }
            catch (Exception exception)
            {
                SetStatus("تعذرت معاينة الملف المحدد؛ لم تُعتمد أي أصناف.", false);
                UiTheme.ShowOperationIssue(this, Localize("معاينة ملف الأصناف"), exception, leftToRight);
            }
            finally { operationInProgress = false; SetBusy(false); }
        }
        // Binds parsed rows and displays validation codes in the active presentation language.
        internal void ShowPreview(MedicineCatalogWorkbookPreview next)
        {
            if (next == null) throw new ArgumentNullException(nameof(next));
            preview = next; grid.DataSource = next.Table; ApplyGridHeaders();
            previewStatus = true;
            statusSuccess = next.IsValid ? (bool?)true : false;
            LayoutContent();
            details.Enabled = !operationInProgress && next.Issues.Count > 0;
        }
        private void ShowAllErrors()
        {
            if (operationInProgress || preview == null || preview.Issues.Count == 0) return;
            using (var dialog = new MedicineCatalogIssuesForm(preview.Issues, leftToRight))
                dialog.ShowDialog(this);
        }
        private void SetBusy(bool busy)
        {
            choose.Enabled = !busy && !previewMode;
            template.Enabled = !busy && !previewMode;
            approve.Enabled = !busy && !previewMode && preview != null && preview.IsValid &&
                PermissionService.CanOpen(AppPermission.Settings);
            details.Enabled = !busy && preview != null && preview.Issues.Count > 0;
            close.Enabled = !busy;
        }
        private void SaveTemplate()
        {
            if (previewMode || operationInProgress || !PermissionService.CanOpen(AppPermission.Settings)) return;
            using (var dialog = new SaveFileDialog { Title = Localize("حفظ قالب استيراد الأصناف"),
                Filter = "Excel Workbook (*.xlsx)|*.xlsx", DefaultExt = "xlsx", AddExtension = true,
                FileName = "قالب-استيراد-الأصناف.xlsx" })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                string temporary = dialog.FileName + ".tmp-" + Guid.NewGuid().ToString("N");
                try
                {
                    using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                        ExcelWorkbookWriter.Write(MedicineCatalogWorkbook.CreateTemplate(), "الأصناف", output);
                    if (File.Exists(dialog.FileName))
                    {
                        string backup = dialog.FileName + ".backup-" + Guid.NewGuid().ToString("N");
                        File.Replace(temporary, dialog.FileName, backup);
                        try { File.Delete(backup); } catch (IOException) { }
                    }
                    else File.Move(temporary, dialog.FileName);
                    SetStatus("حُفظ قالب الأصناف؛ املأ الأعمدة ثم اختر الملف للمعاينة.", true);
                }
                catch (Exception exception) { UiTheme.ShowOperationIssue(this, Localize("حفظ قالب الأصناف"), exception, leftToRight); }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
            }
        }

        private async Task ImportAsync()
        {
            if (previewMode || operationInProgress || preview == null || !preview.IsValid) return;
            if (!PermissionService.CanOpen(AppPermission.Settings))
            { SetStatus("استيراد تعريفات الأصناف متاح للمدير فقط.", false); return; }
            MedicineCatalogWorkbookPreview candidate = preview;
            if (AppNotifications.Show(this, string.Format(Localize(
                "سيُضاف {0} تعريف صنف برصيد صفر. الأسماء الموجودة ستُتجاوز دون تعديل. متابعة؟"), candidate.Medicines.Count),
                Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            var actor = new OperationActor { UserName = SessionContext.CurrentUser, UserRole = SessionContext.CurrentRole };
            operationInProgress = true;
            SetBusy(true);
            SetStatus("جارٍ اعتماد تعريفات الأصناف؛ انتظر اكتمال العملية.", null);
            try
            {
                MedicineCatalogImportResult result = await Task.Run(() =>
                    ApplicationServices.MedicineCatalogImport.ImportNew(candidate.Medicines, actor));
                preview = null;
                SetStatus("اكتمل الاستيراد: {0} صنف جديد، {1} اسم موجود دون تعديل.", true,
                    result.Created, result.SkippedExisting);
            }
            catch (Exception exception)
            {
                SetStatus("تعذر تأكيد نتيجة الاستيراد؛ راجع قائمة الأصناف قبل إعادة المحاولة.", false);
                UiTheme.ShowOperationIssue(this, Localize("اعتماد أصناف Excel"), exception, leftToRight);
            }
            finally { operationInProgress = false; SetBusy(false); }
        }
        private void SetStatus(string arabicText, bool? success, params object[] arguments)
        {
            statusSource = arabicText;
            statusArguments = arguments;
            statusSuccess = success;
            previewStatus = false;
            LayoutContent();
        }
    }
    internal sealed class MedicineCatalogIssuesForm : Form
    {
        private readonly IReadOnlyList<MedicineCatalogValidationIssue> issues;
        private readonly bool leftToRight;
        private readonly DataGridView grid = new DataGridView { Name = "catalogIssuesGrid", ReadOnly = true };
        private readonly AntdUI.Button close = DesktopComponents.Action("catalogIssuesClose", "إغلاق", AppIcon.Close);

        internal MedicineCatalogIssuesForm(IReadOnlyList<MedicineCatalogValidationIssue> issues, bool leftToRight)
        {
            this.issues = issues ?? throw new ArgumentNullException(nameof(issues));
            this.leftToRight = leftToRight;
            Name = "MedicineCatalogIssuesForm";
            Text = Localize("تفاصيل أخطاء الملف");
            ClientSize = new Size(820, 560); MinimumSize = new Size(560, 420);
            FormBorderStyle = FormBorderStyle.Sizable; MaximizeBox = true; MinimizeBox = true;
            StartPosition = FormStartPosition.CenterParent; ShowInTaskbar = false;
            Font = AppFonts.Create(9.5F); KeyPreview = true;
            KeyDown += (sender, args) => { if (args.KeyCode == Keys.Escape) Close(); };

            var header = new Panel { Name = "catalogIssuesHeader", Dock = DockStyle.Top, Height = 88,
                Padding = new Padding(24, 12, 24, 8) };
            var title = new Label { Name = "catalogIssuesTitle", Dock = DockStyle.Top, Height = 38,
                Text = Text, Font = AppFonts.Create(16F, FontStyle.Bold) };
            var summary = new Label { Name = "catalogIssuesSummary", Dock = DockStyle.Fill,
                Text = string.Format(Localize("عدد الأخطاء: {0}"), issues.Count) };
            header.Controls.Add(summary); header.Controls.Add(title);

            var footer = new FlowLayoutPanel { Name = "catalogIssuesFooter", Dock = DockStyle.Bottom,
                Height = 68, WrapContents = false, Padding = new Padding(20, 10, 20, 8) };
            close.Size = new Size(108, 44); close.Text = Localize("إغلاق");
            close.AccessibleName = close.Text; close.Click += (sender, args) => Close();
            footer.Controls.Add(close);

            grid.Dock = DockStyle.Fill; grid.AllowUserToAddRows = false; grid.AllowUserToDeleteRows = false;
            grid.AllowUserToResizeRows = false; grid.RowHeadersVisible = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.Tag = UiTheme.PreserveLocalizedGridDirection;
            grid.AccessibleName = Localize("قائمة أخطاء استيراد الأصناف");
            Controls.Add(grid); Controls.Add(footer); Controls.Add(header);
            UiTheme.Apply(this);
            UiTheme.ApplyDataGridQuality(grid, Localize("لا توجد أخطاء في الملف."));
            grid.AutoGenerateColumns = false; grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            grid.VirtualMode = true;
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "catalogIssuesRow", HeaderText = Localize("الصف"),
                Width = 90, MinimumWidth = 70, SortMode = DataGridViewColumnSortMode.NotSortable });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "catalogIssuesReason", HeaderText = Localize("المشكلة"),
                MinimumWidth = 240, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                SortMode = DataGridViewColumnSortMode.NotSortable });
            grid.CellValueNeeded += (sender, args) =>
            {
                if (args.RowIndex < 0 || args.RowIndex >= this.issues.Count) return;
                MedicineCatalogValidationIssue issue = this.issues[args.RowIndex];
                args.Value = args.ColumnIndex == 0
                    ? (issue.RowNumber > 0 ? issue.RowNumber.ToString(System.Globalization.CultureInfo.InvariantCulture) : "—")
                    : issue.FormatMessage(this.leftToRight);
            };
            grid.RowCount = issues.Count;
            UiDirection.Apply(this, leftToRight);
            UiTheme.ApplyStatusStripPresentation(this, leftToRight);
            close.Text = Localize("إغلاق");
            close.AccessibleName = close.Text;
            grid.AccessibleName = Localize("قائمة أخطاء استيراد الأصناف");
            header.BackColor = UiTheme.Background; footer.BackColor = UiTheme.Background;
            title.BackColor = UiTheme.Background; title.ForeColor = UiTheme.Text;
            summary.BackColor = UiTheme.Background; summary.ForeColor = UiTheme.MutedText;
            footer.RightToLeft = RightToLeft.No;
            footer.FlowDirection = leftToRight ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;
            DesktopComponents.StyleOutlinedFooterAction(close);
        }

        private string Localize(string arabicText)
        { return leftToRight ? UiLanguageCatalog.Translate(arabicText) : arabicText; }
    }
}

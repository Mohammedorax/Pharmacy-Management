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
    internal sealed class InvoiceImportDraftForm : Form
    {
        private readonly bool previewOnly;
        private readonly IInvoiceImportDraftService draftService;
        private InvoiceImportDraftRecord detailRecord;
        private readonly Dictionary<Guid, string> confirmedApprovedDrafts = new Dictionary<Guid, string>();
        private int? confirmedSavedCount;
        private string confirmedPostedReference;
        private string refreshSupportCode;
        private bool leftToRight;
        private bool busy;
        private bool directModeSelected = true;
        private ViewMode view;
        private IReadOnlyList<InvoiceImportCandidate> candidates;
        private IReadOnlyList<InvoiceImportDraftSummary> saved;
        private string selectedFile;
        private readonly Panel header = new Panel { Name = "invoiceDraftHeader", Dock = DockStyle.Top, Height = 94 };
        private readonly Label title = new Label { Name = "invoiceDraftTitle", Dock = DockStyle.Top, Height = 42 };
        private readonly Label hint = new Label { Name = "invoiceDraftHint", Dock = DockStyle.Fill };
        private readonly Panel inputHost = new Panel { Name = "invoiceDraftInputHost", Dock = DockStyle.Top, Height = 104 };
        private readonly TableLayoutPanel fields = new TableLayoutPanel { Name = "invoiceDraftFields", ColumnCount = 3, RowCount = 2 };
        private readonly Label systemLabel = new Label { Name = "invoiceDraftSystemLabel" };
        private readonly Label fileLabel = new Label { Name = "invoiceDraftFileLabel", AutoEllipsis = true };
        private readonly AntdUI.Button directMode = DesktopComponents.Action("invoiceDraftApproveMode", "اعتماد مباشر", AppIcon.Confirm);
        private readonly AntdUI.Button draftMode = DesktopComponents.Action("invoiceDraftReviewMode", "حفظ كمسودة", AppIcon.Save);
        private readonly TextBox metadata = new TextBox { Name = "invoiceDraftMetadata", Multiline = true,
            ReadOnly = true, BorderStyle = BorderStyle.None, ScrollBars = ScrollBars.Vertical, TabStop = false };
        // AntdUI 2.4.11 preserves the value but visually reverses mixed RTL text.
        private readonly TextBox systemInput = new TextBox { Name = "invoiceDraftSystemInput", MaxLength = 100, BorderStyle = BorderStyle.None };
        private readonly AntdUI.Panel systemFrame;

        private readonly FlowLayoutPanel tabs = new FlowLayoutPanel { Name = "invoiceDraftTabs", Dock = DockStyle.Top, Height = 58, WrapContents = false };
        private readonly AntdUI.Button newTab = DesktopComponents.Action("invoiceDraftNewTab", "ملف جديد", AppIcon.Add);
        private readonly AntdUI.Button savedTab = DesktopComponents.Action("invoiceDraftSavedTab", "المسودات المحفوظة", AppIcon.Database);
        private readonly DataGridView grid = new DataGridView { Name = "invoiceDraftGrid", ReadOnly = true };
        private readonly Label status = new Label { Name = "invoiceDraftStatus", Dock = DockStyle.Bottom, Height = 54 };
        private readonly FlowLayoutPanel footer = new FlowLayoutPanel { Name = "invoiceDraftFooter", Dock = DockStyle.Bottom, Height = 68, WrapContents = false };
        private readonly AntdUI.Button choose = DesktopComponents.Action("invoiceDraftChoose", "اختيار ملف", AppIcon.Search);
        private readonly AntdUI.Button template = DesktopComponents.Action("invoiceDraftTemplate", "تنزيل قالب", AppIcon.Report);
        private readonly AntdUI.Button save = DesktopComponents.Action("invoiceDraftSave", "حفظ كمسودات", AppIcon.Save);
        private readonly AntdUI.Button approve = DesktopComponents.Action("invoiceDraftApprove", "اعتماد المسودة", AppIcon.Save);
        private readonly AntdUI.Button refresh = DesktopComponents.Action("invoiceDraftRefresh", "تحديث", AppIcon.Refresh);
        private readonly AntdUI.Button open = DesktopComponents.Action("invoiceDraftOpen", "فتح المسودة", AppIcon.Forward);
        private readonly AntdUI.Button close = DesktopComponents.Action("invoiceDraftClose", "إغلاق", AppIcon.Close);
        private Guid? detailDraftId;
        private bool detailDraftIsDraft;

        private enum ViewMode { New, Saved, Detail, CandidateDetail }

        internal InvoiceImportDraftForm(bool previewOnly = false) : this(previewOnly, null) { }

        internal InvoiceImportDraftForm(bool previewOnly, IInvoiceImportDraftService draftService)
        {
            this.previewOnly = previewOnly;
            this.draftService = draftService ?? ApplicationServices.InvoiceImportDrafts;
            leftToRight = UiDirection.IsLeftToRight;
            systemFrame = DesktopComponents.NativeTextFrame(systemInput, "invoiceDraftSystemFrame");
            systemInput.TextChanged += (sender, args) => UiDirection.ApplyContentDirection(systemInput, leftToRight);
            Name = "InvoiceImportDraftForm";
            Text = UiLanguageCatalog.Translate("استيراد ومراجعة فواتير Excel");
            ClientSize = new Size(1000, 680);
            MinimumSize = new Size(660, 500);
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true; MinimizeBox = true; ShowInTaskbar = previewOnly;
            StartPosition = FormStartPosition.CenterParent;
            Font = AppFonts.Create(9.5F);
            AutoScaleMode = AutoScaleMode.Dpi;
            RightToLeft = RightToLeft.Yes; RightToLeftLayout = true;
            KeyPreview = true;
            KeyDown += (sender, args) => { if (args.KeyCode == Keys.Escape) Close(); };
            if (previewOnly)
            {
                selectedFile = "sample-invoices.xlsx";
                systemInput.Text = "Legacy";
                candidates = InvoiceImportValidation.Validate(InvoiceImportMode.ImportAndApprove, "Legacy", new[] {
                    new InvoiceImportRow { SourceInvoiceId = "INV-001", Kind = ImportedInvoiceKind.Sale,
                        LineNumber = 1, InvoiceDate = new DateTime(2026, 9, 29), PartyName = "عميل Pharma",
                        CurrencyCode = "YER", MedicineName = "باراسيتامول 500 mg",
                        Barcode = "000123", Quantity = 1.25m, UnitPrice = 12.345m,
                        PartyReference = "C-001", PartyPhone = "000000000", InvoiceDiscountPercent = 2.5m,
                        TaxRatePercent = 0m, PriceTaxMode = "Inclusive",
                        PaymentMethod = "Cash",
                        ProductionDate = new DateTime(2025, 1, 1), ExpiryDate = new DateTime(2027, 1, 1),
                        BatchNumber = "B-01", Gtin = "01234567890128" }
                });
            }
            BuildLayout();
            hint.TextChanged += (sender, args) => LayoutShell();
            Resize += (sender, args) => LayoutShell();
            Shown += (sender, args) => LayoutShell();
            UiTheme.Apply(this);
            ApplyPresentation(leftToRight);
            if (previewOnly) UiTheme.RemoveStatusStrip(this);
            ShowNew();
            FormClosing += (sender, args) =>
            {
                if (!busy) return;
                args.Cancel = true;
                SetStatus(T("العملية جارية؛ انتظر اكتمالها.", "The operation is still running."), false);
            };
        }

        private string T(string arabic, string english) { return leftToRight ? english : arabic; }
        private bool DirectMode { get { return directModeSelected; } }
        private void SelectImportMode(bool approveNow)
        {
            if (busy || directModeSelected == approveNow) return;
            if (candidates != null)
                candidates = InvoiceImportValidation.Validate(approveNow ? InvoiceImportMode.ImportAndApprove : InvoiceImportMode.Draft,
                    candidates[0].SourceSystem, candidates.SelectMany(item => item.Lines));
            directModeSelected = approveNow;
            ApplyModeVisual();
            if (view == ViewMode.New) ShowNew(); else ApplyView();
        }
        private void ApplyModeVisual()
        {
            foreach (AntdUI.Button button in new[] { directMode, draftMode })
            {
                bool active = button == directMode ? DirectMode : !DirectMode;
                button.DefaultBorderColor = active ? UiTheme.Primary : UiTheme.Border;
                button.DefaultBack = active ? UiTheme.Selection : UiTheme.Surface;
                button.BackColor = button.DefaultBack;
                button.ForeColor = active ? UiTheme.Primary : UiTheme.Text;
                button.AccessibleDescription = active ? T("الخيار المحدد", "Selected mode") : string.Empty;
            }
        }
        internal void SetPreviewSourceEntry()
        {
            if (!previewOnly) throw new InvalidOperationException("هذا الخيار مخصص للمعاينة المرئية فقط.");
            candidates = null;
            selectedFile = null;
            fileLabel.Text = T("لم يُختر ملف.", "No file selected.");
            systemInput.Text = string.Empty;
            ShowNew();
        }

        internal void SetPreviewDirectMode()
        {
            if (!previewOnly) throw new InvalidOperationException("هذا الخيار مخصص للمعاينة المرئية فقط.");
            candidates = InvoiceImportValidation.Validate(InvoiceImportMode.ImportAndApprove,
                "Legacy", candidates.SelectMany(item => item.Lines));
            SelectImportMode(true);
            ShowNew();
        }
        internal void SetPreviewDraftMode()
        {
            if (!previewOnly) throw new InvalidOperationException("هذا الخيار مخصص للمعاينة المرئية فقط.");
            candidates = InvoiceImportValidation.Validate(InvoiceImportMode.Draft,
                "Legacy", candidates.SelectMany(item => item.Lines));
            SelectImportMode(false);
            ShowNew();
        }
        internal void ApplyPresentation(bool english)
        {
            leftToRight = english;
            Text = T("استيراد ومراجعة فواتير Excel", "Import and review Excel invoices");
            title.Text = Text;
            hint.Text = T("اختر طريقة الاستيراد، ثم راجع البنود قبل حفظها أو اعتمادها.",
                "Choose an import mode, then review lines before saving or approving.");
            systemLabel.Text = T("النظام المصدر", "Source system");
            fileLabel.Text = selectedFile == null ? T("لم يُختر ملف.", "No file selected.") :
                T("الملف: ", "File: ") + selectedFile;
            directMode.Text = T("اعتماد مباشر", "Approve now");
            draftMode.Text = T("حفظ كمسودة", "Save as draft");
            directMode.AccessibleName = directMode.Text;
            draftMode.AccessibleName = draftMode.Text;
            newTab.Text = T("ملف جديد", "New file"); savedTab.Text = T("المسودات المحفوظة", "Saved drafts");
            choose.Text = T("اختيار ملف", "Choose file"); template.Text = T("تنزيل قالب", "Template");
            save.Text = DirectMode ? T("اعتماد المحددة", "Approve selected") : T("حفظ كمسودات", "Save drafts");
            approve.Text = T("اعتماد المسودة", "Approve draft"); refresh.Text = T("تحديث", "Refresh");
            open.Text = view == ViewMode.Detail || view == ViewMode.CandidateDetail ? T("القائمة", "List") :
                view == ViewMode.New ? T("مراجعة البنود", "Review lines") : T("فتح المسودة", "Open draft");
            close.Text = T("إغلاق", "Close");
            foreach (AntdUI.Button action in new[] { newTab, savedTab, choose, template, save, approve, refresh, open, close })
                action.AccessibleName = action.Text;
            systemInput.AccessibleName = systemLabel.Text;
            grid.AccessibleName = T("جدول استيراد الفواتير والمسودات", "Invoice import and draft table");
            tabs.FlowDirection = footer.FlowDirection = english ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;
            tabs.RightToLeft = footer.RightToLeft = RightToLeft.No;
            title.TextAlign = hint.TextAlign = systemLabel.TextAlign = fileLabel.TextAlign = status.TextAlign =
                english ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
            metadata.TextAlign = english ? HorizontalAlignment.Left : HorizontalAlignment.Right;
            metadata.RightToLeft = english ? RightToLeft.No : RightToLeft.Yes;
            metadata.AccessibleName = T("بيانات الفاتورة للمراجعة", "Invoice metadata for review");
            UiDirection.Apply(this, english);
            UiDirection.ApplyContentDirection(systemInput, english);
            // Mixed Arabic names and Latin IDs use physical alignment here; WinForms
            // mirrors a multiline RTL TextBox's right alignment a second time.
            metadata.RightToLeft = RightToLeft.No;
            metadata.TextAlign = english ? HorizontalAlignment.Left : HorizontalAlignment.Right;
            UiTheme.ApplyStatusStripPresentation(this, english);
            ApplyVisualTheme();
            ApplyView();
            if (view == ViewMode.New && DirectMode)
                hint.Text = T("اعتماد المحددة يحدّث المخزون والحسابات؛ اختر «حفظ كمسودة» للمراجعة أولًا.",
                    "Approving the selected invoice posts stock and accounts; choose Save as draft to review first.");
            if (candidates != null && view == ViewMode.New) ShowCandidateRows();
            else if (view == ViewMode.Saved) ShowSavedRows();
            if (previewOnly && view == ViewMode.New)
                SetStatus(T("معاينة مرئية دون قراءة ملف أو قاعدة بيانات.", "Visual preview; no file or database read."), null);
            if (view == ViewMode.Detail && detailRecord != null)
            {
                ShowDetailRows(detailRecord.Candidate);
                ShowDetailHeader(detailRecord.Candidate);
                PresentDraftDetailStatus();
            }
            if (view == ViewMode.Saved) PresentConfirmedWriteStatus();
        }

        private void BuildLayout()
        {
            header.Padding = new Padding(24, 12, 24, 8);
            title.Font = AppFonts.Create(16F, FontStyle.Bold);
            hint.AutoEllipsis = false;
            hint.UseMnemonic = false;
            header.Controls.Add(hint); header.Controls.Add(title);
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
            fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            fields.Controls.Add(systemLabel, 0, 0);
            systemLabel.Dock = DockStyle.Fill;
            systemFrame.Dock = DockStyle.Fill; systemFrame.Margin = new Padding(4);
            fields.Controls.Add(systemFrame, 1, 0);
            fields.SetColumnSpan(systemFrame, 2);
            fileLabel.Dock = DockStyle.Fill;
            directMode.Dock = draftMode.Dock = DockStyle.Fill;
            directMode.Margin = draftMode.Margin = new Padding(4, 0, 4, 0);
            directMode.Click += (sender, args) => SelectImportMode(true);
            draftMode.Click += (sender, args) => SelectImportMode(false);
            fields.Controls.Add(directMode, 0, 1);
            fields.Controls.Add(draftMode, 1, 1);
            fields.Controls.Add(fileLabel, 2, 1);
            metadata.Dock = DockStyle.Fill;
            metadata.Margin = new Padding(24, 4, 24, 4);
            metadata.Visible = false;
            inputHost.Controls.Add(metadata);
            inputHost.Controls.Add(fields);
            inputHost.Resize += (sender, args) => LayoutFields();
            newTab.Size = new Size(154, 44); savedTab.Size = new Size(192, 44);
            newTab.Click += (sender, args) => ShowNew();
            savedTab.Click += async (sender, args) => await ShowSavedAsync();
            tabs.Padding = new Padding(20, 6, 20, 4);
            tabs.Controls.Add(newTab); tabs.Controls.Add(savedTab);
            grid.Dock = DockStyle.Fill;
            grid.Tag = UiTheme.PreserveLocalizedGridDirection;
            grid.AllowUserToAddRows = false; grid.AllowUserToDeleteRows = false;
            grid.AllowUserToResizeRows = false; grid.RowHeadersVisible = false;
            grid.MultiSelect = false; grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            UiTheme.ApplyDataGridQuality(grid, T("اختر ملفًا لمعاينته.", "Choose a file to preview."));
            grid.CellDoubleClick += async (sender, args) =>
            {
                if (args.RowIndex < 0) return;
                if (view == ViewMode.New) OpenCandidate();
                else if (view == ViewMode.Saved) await OpenSelectedAsync();
            };
            grid.SelectionChanged += (sender, args) =>
            {
                if (view == ViewMode.Saved || view == ViewMode.New)
                {
                    open.Enabled = !busy && grid.SelectedRows.Count > 0 &&
                        (view == ViewMode.Saved || candidates != null);
                    save.Enabled = !busy && !previewOnly && view == ViewMode.New && candidates != null &&
                        (!DirectMode || grid.SelectedRows.Count > 0);
                    approve.Enabled = !busy && !previewOnly && SelectedSavedIsDraft();
                }
            };
            status.Padding = new Padding(24, 6, 24, 6);
            status.AutoEllipsis = false;
            status.UseMnemonic = false;
            footer.Padding = new Padding(20, 10, 20, 8);
            footer.Resize += (sender, args) => LayoutReviewAction();
            choose.Size = new Size(132, 44); template.Size = new Size(136, 44);
            save.Size = new Size(180, 44); approve.Size = new Size(174, 44); refresh.Size = new Size(116, 44);
            open.Size = new Size(150, 44); close.Size = new Size(105, 44);
            choose.Click += async (sender, args) => await ChooseAsync();
            template.Click += (sender, args) => DownloadTemplate();
            save.Click += async (sender, args) => await SaveAsync();
            approve.Click += async (sender, args) => await ApproveDraftAsync();
            refresh.Click += async (sender, args) => await ShowSavedAsync();
            open.Click += async (sender, args) =>
            {
                if (view == ViewMode.Detail) { view = ViewMode.Saved; ApplyView(); ShowSavedRows(); }
                else if (view == ViewMode.CandidateDetail) ShowNew();
                else if (view == ViewMode.New) OpenCandidate();
                else await OpenSelectedAsync();
            };
            close.Click += (sender, args) => Close();
            footer.Controls.Add(choose); footer.Controls.Add(template); footer.Controls.Add(save);
            footer.Controls.Add(refresh); footer.Controls.Add(open); footer.Controls.Add(approve); footer.Controls.Add(close);
            Controls.Add(grid); Controls.Add(status); Controls.Add(footer); Controls.Add(tabs);
            Controls.Add(inputHost); Controls.Add(header);
        }

        private int ScaleLogical(int value)
        {
            return Math.Max(1, (int)Math.Round(value * Math.Max(96, DeviceDpi) / 96.0));
        }
        private void LayoutShell()
        {
            if (IsDisposed || fields.RowStyles.Count < 2) return;
            bool compact = ClientSize.Height < ScaleLogical(600);
            SuspendLayout();
            try
            {
                header.Padding = new Padding(ScaleLogical(24), ScaleLogical(compact ? 6 : 12),
                    ScaleLogical(24), ScaleLogical(compact ? 6 : 8));
                title.Height = ScaleLogical(compact ? 34 : 42);
                // Native Label mirrors horizontal alignment in RTL; keep the Arabic paragraph
                // direction while using Left for its physical right reading edge.
                title.RightToLeft = hint.RightToLeft = leftToRight ? RightToLeft.No : RightToLeft.Yes;
                title.TextAlign = hint.TextAlign = ContentAlignment.MiddleLeft;
                int hintWidth = Math.Max(1, ClientSize.Width - header.Padding.Horizontal);
                int hintHeight = TextRenderer.MeasureText(hint.Text, hint.Font,
                    new Size(hintWidth, int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix |
                        (leftToRight ? 0 : TextFormatFlags.RightToLeft)).Height;
                header.Height = header.Padding.Vertical + title.Height + hintHeight + ScaleLogical(4);
                hint.AccessibleName = hint.Text;
                tabs.Height = ScaleLogical(compact ? 50 : 58);
                tabs.Padding = new Padding(ScaleLogical(20), ScaleLogical(compact ? 3 : 6),
                    ScaleLogical(20), ScaleLogical(compact ? 3 : 4));
                int statusWidth = Math.Max(1, ClientSize.Width - status.Padding.Horizontal);
                int statusTextHeight = TextRenderer.MeasureText(status.Text ?? string.Empty, status.Font,
                    new Size(statusWidth, int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix |
                        (leftToRight ? 0 : TextFormatFlags.RightToLeft)).Height;
                status.Height = Math.Max(ScaleLogical(compact ? 34 : 54),
                    statusTextHeight + status.Padding.Vertical + ScaleLogical(4));
                footer.Height = ScaleLogical(compact ? 60 : 68);
                footer.Padding = new Padding(ScaleLogical(20), ScaleLogical(compact ? 8 : 10),
                    ScaleLogical(20), ScaleLogical(8));
                inputHost.Height = ScaleLogical(view == ViewMode.Detail || view == ViewMode.CandidateDetail ? 72 : 104);
                fields.ColumnStyles[0].Width = fields.ColumnStyles[1].Width = ScaleLogical(160);
                fields.RowStyles[0].Height = ScaleLogical(54);
                fields.RowStyles[1].Height = ScaleLogical(44);
                systemFrame.Margin = new Padding(ScaleLogical(4));
            }
            finally { ResumeLayout(true); }
            LayoutFields();
            LayoutReviewAction();
            directMode.Refresh();
            draftMode.Refresh();
        }

        private void LayoutFields()
        {
            int width = Math.Min(ScaleLogical(760), Math.Max(1, inputHost.ClientSize.Width - ScaleLogical(48)));
            fields.SetBounds(Math.Max(0, (inputHost.ClientSize.Width - width) / 2), ScaleLogical(2), width, ScaleLogical(98));
        }
        private void ApplyVisualTheme()
        {
            header.BackColor = inputHost.BackColor = tabs.BackColor = footer.BackColor = status.BackColor = UiTheme.Background;
            fields.BackColor = UiTheme.Background;
            title.BackColor = hint.BackColor = systemLabel.BackColor = fileLabel.BackColor = UiTheme.Background;
            metadata.BackColor = UiTheme.Background;
            title.ForeColor = systemLabel.ForeColor = fileLabel.ForeColor = UiTheme.Text;
            metadata.ForeColor = UiTheme.Text;
            metadata.BorderStyle = BorderStyle.None;
            hint.ForeColor = status.ForeColor = UiTheme.MutedText;
            DesktopComponents.StyleNativeTextFrame(systemFrame, systemInput);
            foreach (AntdUI.Button action in new[] { newTab, savedTab, choose, template, save, approve, refresh, open, close, directMode, draftMode })
                DesktopComponents.StyleOutlinedFooterAction(action);
            ApplyModeVisual();
            grid.BackgroundColor = UiTheme.Surface;
            RefreshGridEmptyState();
        }
        private void RefreshGridEmptyState()
        {
            Label emptyState = grid.Controls.Find("__GridEmptyState", false).OfType<Label>().FirstOrDefault();
            if (emptyState != null)
            {
                emptyState.Text = view == ViewMode.Saved ? T("لا توجد مسودات معروضة؛ اضغط تحديث.",
                    "No drafts are displayed; use Refresh.") : T("اختر ملفًا لمعاينته.", "Choose a file to preview.");
                emptyState.TextAlign = ContentAlignment.MiddleCenter;
            }
        }
        private void ApplyView()
        {
            bool detail = view == ViewMode.Detail || view == ViewMode.CandidateDetail;
            inputHost.Visible = view == ViewMode.New || detail;
            inputHost.Height = ScaleLogical(detail ? 72 : 104);
            fields.Visible = view == ViewMode.New;
            metadata.Visible = detail;
            choose.Visible = template.Visible = save.Visible = view == ViewMode.New;
            refresh.Visible = view == ViewMode.Saved;
            approve.Visible = view == ViewMode.Saved || view == ViewMode.Detail;
            open.Visible = view != ViewMode.New || candidates != null;
            open.Text = view == ViewMode.Detail || view == ViewMode.CandidateDetail ? T("القائمة", "List") :
                view == ViewMode.New ? T("مراجعة البنود", "Review lines") : T("فتح المسودة", "Open draft");
            open.AccessibleName = open.Text;
            save.Text = DirectMode ? T("اعتماد المحددة", "Approve selected") : T("حفظ كمسودات", "Save drafts");
            save.AccessibleName = save.Text;
            LayoutReviewAction();
            choose.Enabled = template.Enabled = !busy && !previewOnly;
            directMode.Enabled = draftMode.Enabled = !busy;
            systemInput.Enabled = !busy && candidates == null;
            save.Enabled = !busy && candidates != null && !previewOnly &&
                (!DirectMode || grid.SelectedRows.Count > 0);
            approve.Enabled = !busy && !previewOnly && SelectedSavedIsDraft();
            open.Enabled = !busy && (view == ViewMode.Detail || view == ViewMode.CandidateDetail ||
                (grid.SelectedRows.Count > 0 && (view == ViewMode.Saved || candidates != null)));
            newTab.DefaultBorderColor = view == ViewMode.New || view == ViewMode.CandidateDetail ? UiTheme.Primary : UiTheme.Border;
            savedTab.DefaultBorderColor = view == ViewMode.Saved || view == ViewMode.Detail ? UiTheme.Primary : UiTheme.Border;
            newTab.AccessibleDescription = view == ViewMode.New || view == ViewMode.CandidateDetail ? T("التبويب الحالي", "Current tab") : string.Empty;
            savedTab.AccessibleDescription = view == ViewMode.Saved || view == ViewMode.Detail ? T("التبويب الحالي", "Current tab") : string.Empty;
            LayoutShell();
            RefreshGridEmptyState();
        }
        private void LayoutReviewAction()
        {
            FlowLayoutPanel target = view == ViewMode.New && ClientSize.Width < ScaleLogical(780) ? tabs : footer;
            if (open.Parent == target) return;
            target.Controls.Add(open);
            if (target == footer) target.Controls.SetChildIndex(open, Math.Min(4, footer.Controls.Count - 1));
        }
        private void ShowNew()
        {
            if (busy) return;
            ClearConfirmedWriteStatus();
            view = ViewMode.New;
            hint.Text = DirectMode ? T("اعتماد المحددة يحدّث المخزون والحسابات؛ اختر «حفظ كمسودة» للمراجعة أولًا.",
                "Approving the selected invoice posts stock and accounts; choose Save as draft to review first.") :
                T("راجع فواتير Excel واحفظها كمسودات؛ لا يتغير المخزون أو الحسابات قبل اعتمادها لاحقًا.",
                "Review Excel invoices as drafts. Stock and accounts change only after later approval.");
            ApplyView(); ShowCandidateRows();
            SetStatus(previewOnly ? T("معاينة مرئية دون قراءة ملف أو قاعدة بيانات.", "Visual preview; no file or database read.") :
                DirectMode ? T("اختر ملفًا ثم حدد فاتورة واحدة لاعتمادها.", "Choose a file, then select one invoice to approve.") :
                T("اختر ملفًا ثم راجع السطور قبل حفظها كمسودات.", "Choose a file and review rows before saving drafts."), null);
        }
        private Task ShowSavedAsync()
        {
            return ShowSavedCoreAsync(false);
        }
        private async Task ShowSavedCoreAsync(bool allowWhileBusy)
        {
            if (busy && !allowWhileBusy) return;
            view = ViewMode.Saved; ApplyView();
            if (previewOnly)
            {
                saved = new[] { new InvoiceImportDraftSummary { DraftId = Guid.Empty, SourceSystem = "Legacy",
                    SourceInvoiceId = "INV-001", Kind = ImportedInvoiceKind.Sale, InvoiceDate = new DateTime(2026, 9, 29),
                    PartyName = "عميل Pharma", Status = "Draft" } };
                ShowSavedRows(); SetStatus(T("معاينة مرئية دون اتصال بقاعدة البيانات.", "Visual preview without a database connection."), null);
                return;
            }
            var actor = CurrentActor();
            SetBusy(true);
            try
            {
                saved = await Task.Run(() => draftService.ListRecent(actor));
                ClearConfirmedWriteStatus();
                ShowSavedRows();
                SetStatus(saved.Count == 0 ? T("لا توجد مسودات محفوظة.", "No saved drafts.") :
                    string.Format(T("أحدث {0} مسودة؛ افتح إحداها لمراجعة بنودها.", "Latest {0} drafts; open one to review its lines."), saved.Count), null);
            }
            catch (Exception error)
            {
                saved = null; grid.DataSource = null;
                if (confirmedSavedCount.HasValue || confirmedPostedReference != null)
                {
                    refreshSupportCode = AppDiagnostics.Log("InvoiceDraftRefreshAfterCommit", error);
                    PresentConfirmedWriteStatus();
                }
                else
                {
                    SetStatus(T("تعذر تحديث القائمة؛ أعد المحاولة عند عودة الاتصال.",
                        "Could not refresh the list; retry when the connection returns."), false);
                    UiTheme.ShowOperationIssue(this, T("قائمة مسودات الفواتير", "Invoice drafts"), error, leftToRight);
                }
            }
            finally { SetBusy(false); }
        }
        private async Task ChooseAsync()
        {
            if (busy || previewOnly) return;
            using (var picker = new OpenFileDialog { Title = T("اختيار ملف فواتير Excel", "Choose invoice workbook"),
                Filter = "Excel Workbook (*.xlsx)|*.xlsx" })
            {
                if (picker.ShowDialog(this) != DialogResult.OK) return;
                await LoadFileAsync(picker.FileName);
            }
        }
        internal async Task LoadFileAsync(string path)
        {
            if (busy || previewOnly) return;
            string system = (systemInput.Text ?? string.Empty).Trim();
            if (system.Length == 0) { SetStatus(T("أدخل اسم النظام المصدر أولًا.", "Enter the source system first."), false); return; }
            if (string.IsNullOrWhiteSpace(path)) return;
            candidates = null; grid.DataSource = null; selectedFile = Path.GetFileName(path);
            fileLabel.Text = T("الملف: ", "File: ") + selectedFile;
            SetBusy(true);
            try
            {
                InvoiceImportMode mode = DirectMode ? InvoiceImportMode.ImportAndApprove : InvoiceImportMode.Draft;
                candidates = await Task.Run(() => InvoiceImportWorkbook.Read(path, mode, system));
                ShowCandidateRows();
                SetStatus(string.Format(DirectMode ? T("تمت معاينة {0} فاتورة؛ حدد واحدة لاعتمادها فورًا.",
                    "Previewed {0} invoices; select one for immediate approval.") :
                    T("تمت معاينة {0} فاتورة. الحفظ التالي ينشئ مسودات فقط.",
                    "Previewed {0} invoices. Saving creates drafts only."), candidates.Count), true);
            }
            catch (Exception error)
            {
                SetStatus(T("تعذرت قراءة الملف؛ لم تُحفظ مسودات.", "Could not read the file; no drafts were saved."), false);
                UiTheme.ShowOperationIssue(this, T("معاينة فواتير Excel", "Preview Excel invoices"), error);
            }
            finally { SetBusy(false); }
        }
        private void DownloadTemplate()
        {
            if (busy || previewOnly) return;
            using (var dialog = new SaveFileDialog { Title = T("حفظ قالب استيراد الفواتير", "Save invoice import template"),
                Filter = "Excel Workbook (*.xlsx)|*.xlsx", FileName = "InvoiceImportTemplate.xlsx", AddExtension = true, DefaultExt = "xlsx" })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                string temporary = Path.Combine(Path.GetDirectoryName(dialog.FileName), "." + Guid.NewGuid().ToString("N") + ".xlsx.tmp");
                try
                {
                    using (DataTable workbook = InvoiceImportWorkbook.CreateTemplate(leftToRight))
                    using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                        ExcelWorkbookWriter.Write(workbook, workbook.TableName, output);
                    if (File.Exists(dialog.FileName)) File.Replace(temporary, dialog.FileName, null);
                    else File.Move(temporary, dialog.FileName);
                    SetStatus(T("حُفظ القالب. التواريخ yyyy-MM-dd؛ أصفار المعرّفات والباركود محفوظة.",
                        "Template saved. Dates: yyyy-MM-dd. IDs and barcodes retain zeros."), true);
                }
                catch (Exception error)
                {
                    if (File.Exists(temporary)) File.Delete(temporary);
                    SetStatus(T("تعذر حفظ قالب الفواتير.", "Could not save the invoice template."), false);
                    UiTheme.ShowOperationIssue(this, T("قالب استيراد الفواتير", "Invoice import template"), error);
                }
            }
        }

        private async Task SaveAsync()
        {
            if (busy || previewOnly || candidates == null || candidates.Count == 0) return;
            if (DirectMode) { await ApproveSelectedAsync(); return; }
            IReadOnlyList<InvoiceImportCandidate> batch = candidates;
            OperationActor actor = CurrentActor();
            SetBusy(true);
            try
            {
                IReadOnlyList<Guid> ids = await Task.Run(() => draftService.SaveBatch(batch, actor));
                candidates = null; selectedFile = null;
                ShowCandidateRows();
                await ShowSavedAfterSaveAsync(ids.Count, actor);
            }
            catch (UnauthorizedAccessException error)
            {
                SetStatus(T("حفظ المسودات متاح للمدير فقط.", "Only managers can save invoice drafts."), false);
                UiTheme.ShowOperationIssue(this, T("صلاحية حفظ المسودات", "Draft save permission"), error);
            }
            catch (Exception error)
            {
                SetStatus(T("تعذر تأكيد حفظ المسودات؛ راجع سجل المصادر قبل إعادة المحاولة.",
                    "Could not confirm draft saving. Check imported source IDs before retrying."), false);
                UiTheme.ShowOperationIssue(this, T("حفظ مسودات الفواتير", "Save invoice drafts"), error);
            }
            finally { SetBusy(false); }
        }
        private async Task ApproveSelectedAsync()
        {
            if (busy || previewOnly || candidates == null || grid.SelectedRows.Count == 0) return;
            int index = grid.SelectedRows[0].Index;
            if (index < 0 || index >= candidates.Count) return;
            InvoiceImportCandidate candidate = candidates[index];
            OperationActor actor = CurrentActor();
            try
            {
                // The mapping is pure. Reject an unsupported Excel contract before
                // asking for a financial confirmation or opening a SQL transaction.
                if (candidate.Kind == ImportedInvoiceKind.Sale)
                    InvoiceImportApprovalMapping.Sale(candidate, actor);
                else
                    InvoiceImportApprovalMapping.Purchase(candidate, actor, false);
            }
            catch (InvoiceImportMappingException error)
            {
                SetStatus(T("راجع بيانات الفاتورة قبل الاعتماد: ", "Review the invoice before approval: ") +
                    T(error.Message, error.EnglishMessage), false);
                return;
            }
            catch (UnauthorizedAccessException error)
            {
                UiTheme.ShowOperationIssue(this, T("صلاحية اعتماد الفاتورة", "Invoice approval permission"), error);
                return;
            }
            string message = string.Format(T("اعتماد فاتورة {0} الآن سيحدث المخزون والقيود المالية. لن تُعتمد بقية فواتير الملف تلقائيًا. متابعة؟",
                "Approve invoice {0} now? Stock and journal will change. Other invoices in the file are not approved automatically."),
                candidate.SourceInvoiceId);
            if (AppNotifications.Show(this, message, Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            SetBusy(true);
            try
            {
                InvoiceImportApprovalResult result = await Task.Run(() =>
                    ApplicationServices.InvoiceImportApprovals.Approve(candidate, actor));
                candidates = candidates.Where((item, position) => position != index).ToArray();
                if (candidates.Count == 0) candidates = null;
                ShowCandidateRows(); ApplyView();
                SetStatus(string.Format(T("اعتمدت الفاتورة {0} برقم {1}؛ المتبقي في الملف {2}.",
                    "Invoice {0} posted as {1}; {2} remain in the file."), result.SourceInvoiceId,
                    result.PostedReference, candidates?.Count ?? 0), true);
            }
            catch (InvoiceImportMappingException error)
            {
                SetStatus(T("راجع بيانات الفاتورة قبل الاعتماد: ", "Review the invoice before approval: ") +
                    T(error.Message, error.EnglishMessage), false);
            }
            catch (UnauthorizedAccessException error)
            {
                SetStatus(T("اعتماد الفاتورة غير متاح لهذا المستخدم.",
                    "This user cannot approve the invoice."), false);
                UiTheme.ShowOperationIssue(this, T("صلاحية اعتماد الفاتورة", "Invoice approval permission"), error);
            }
            catch (Exception error)
            {
                SetStatus(T("تعذر تأكيد نتيجة الاعتماد؛ راجع رقم المصدر وسجل الفواتير قبل إعادة المحاولة.",
                    "Approval result is uncertain. Check source ID and invoice history before retrying."), false);
                UiTheme.ShowOperationIssue(this, T("اعتماد فاتورة Excel", "Approve Excel invoice"), error);
            }
            finally { SetBusy(false); }
        }
        private async Task ApproveDraftAsync()
        {
            if (busy || previewOnly || !SelectedSavedIsDraft()) return;
            Guid id = view == ViewMode.Detail ? detailDraftId.Value : (Guid)((DataRowView)grid.SelectedRows[0].DataBoundItem)["DraftId"];
            if (AppNotifications.Show(this, T("اعتماد هذه المسودة سيحدث المخزون والقيود المالية فورًا. متابعة؟",
                "Approving this draft posts stock and journal immediately. Continue?"), Text,
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            OperationActor actor = CurrentActor();
            SetBusy(true);
            try
            {
                InvoiceImportApprovalResult result = await Task.Run(() =>
                    ApplicationServices.InvoiceImportApprovals.ApproveDraft(id, actor));
                await RefreshAfterConfirmedWriteAsync(null, result.PostedReference, id, actor);
            }
            catch (InvoiceImportMappingException error)
            {
                SetStatus(T("راجع بيانات المسودة قبل الاعتماد: ", "Review the draft before approval: ") +
                    T(error.Message, error.EnglishMessage), false);
            }
            catch (UnauthorizedAccessException error)
            {
                SetStatus(T("اعتماد المسودة غير متاح لهذا المستخدم.",
                    "This user cannot approve the draft."), false);
                UiTheme.ShowOperationIssue(this, T("صلاحية اعتماد المسودة", "Draft approval permission"), error);
            }
            catch (Exception error)
            {
                SetStatus(T("تعذر تأكيد نتيجة الاعتماد؛ راجع حالة المسودة والفاتورة قبل إعادة المحاولة.",
                    "Approval result is uncertain. Check draft status and invoice history before retrying."), false);
                UiTheme.ShowOperationIssue(this, T("اعتماد مسودة الفاتورة", "Approve invoice draft"), error);
            }
            finally { SetBusy(false); }
        }
        private async Task OpenSelectedAsync()
        {
            if (busy || view != ViewMode.Saved || grid.SelectedRows.Count == 0) return;
            if (!(grid.SelectedRows[0].DataBoundItem is DataRowView row)) return;
            Guid id = (Guid)row["DraftId"];
            if (previewOnly) { SetStatus(T("تفاصيل المعاينة لا تقرأ قاعدة البيانات.", "Preview detail does not read the database."), null); return; }
            var actor = CurrentActor();
            SetBusy(true);
            try
            {
                InvoiceImportDraftRecord record = await Task.Run(() => draftService.Get(id, actor));
                if (record == null) { SetStatus(T("لم تعد المسودة موجودة؛ حدّث القائمة.", "Draft no longer exists; refresh the list."), false); return; }
                if (confirmedApprovedDrafts.TryGetValue(id, out string confirmedReference))
                {
                    record.Status = "Approved";
                    record.ApprovedReference = confirmedReference;
                }
                detailRecord = record;
                detailDraftId = id; detailDraftIsDraft = record.Status == "Draft";
                view = ViewMode.Detail; ShowDetailRows(record.Candidate); ApplyView();
                ShowDetailHeader(record.Candidate);
                PresentDraftDetailStatus();
            }
            catch (Exception error) { UiTheme.ShowOperationIssue(this, T("فتح المسودة", "Open draft"), error); }
            finally { SetBusy(false); }
        }
        private void PresentDraftDetailStatus()
        {
            if (detailRecord.Status == "Draft")
                SetStatus(T("هذه مسودة مراجعة فقط؛ لا توجد حركة مخزون أو قيد محاسبي.",
                    "This is a review draft; it has no stock or journal posting."), null);
            else if (detailRecord.Status == "Approved")
                SetStatus(string.Format(T("هذه المسودة معتمدة برقم {0}؛ أُثبت أثرها في المخزون والقيود، ولا يمكن اعتمادها مرة أخرى.",
                    "This draft was approved as {0}; stock and journal were posted. It cannot be approved again."),
                    TextOrDash(detailRecord.ApprovedReference)), true);
            else
                SetStatus(T("حالة المسودة غير معروفة؛ حدّث القائمة قبل متابعة الاعتماد.",
                    "Draft status is unknown; refresh the list before proceeding."), false);
        }
        private void OpenCandidate()
        {
            if (busy || view != ViewMode.New || candidates == null || grid.SelectedRows.Count == 0) return;
            int index = grid.SelectedRows[0].Index;
            if (index < 0 || index >= candidates.Count) return;
            InvoiceImportCandidate candidate = candidates[index];
            view = ViewMode.CandidateDetail;
            ShowDetailRows(candidate); ApplyView(); ShowDetailHeader(candidate);
            SetStatus(DirectMode
                ? T("راجع البنود والبيانات؛ ستتغير أرصدة المخزون والحسابات عند اعتماد هذه الفاتورة فقط.",
                    "Review the lines and metadata; stock and accounts change when you approve this invoice only.")
                : T("راجع البنود والبيانات قبل حفظ الملف كمسودات؛ لا يحدث اعتماد مالي الآن.",
                    "Review the lines and metadata before saving drafts; no financial posting occurs now."), null);
        }
        private void ShowDetailHeader(InvoiceImportCandidate candidate)
        {
            hint.Text = T("تفاصيل الفاتورة وبنودها", "Invoice details and lines");
            string discount = candidate.InvoiceDiscountPercent?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "—";
            string tax = candidate.TaxRatePercent?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "—";
            string taxMode = candidate.PriceTaxMode == "Inclusive" ? T("شامل", "Inclusive") :
                candidate.PriceTaxMode == "Exclusive" ? T("غير شامل", "Exclusive") : "—";
            string payment = candidate.PaymentMethod == "Cash" ? T("نقدي", "Cash") :
                candidate.PaymentMethod == "Payable" ? T("ذمة مورد", "Payable") : "—";
            metadata.Text = string.Format(T("{0} رقم {1}، {2}، المعرّف {3}، الهاتف {4}",
                "{0} {1}, {2}, ID {3}, phone {4}"), KindText(candidate.Kind), candidate.SourceInvoiceId,
                candidate.PartyName, TextOrDash(candidate.PartyReference), TextOrDash(candidate.PartyPhone)) +
                Environment.NewLine + string.Format(T("الخصم {0} بالمئة، الضريبة {1} بالمئة، السعر {2}، السداد {3}، ملاحظات: {4}",
                    "Discount {0}%, tax {1}%, price {2}, payment {3}, notes: {4}"), discount, tax, taxMode,
                    payment, TextOrDash(candidate.Notes));
            metadata.AccessibleDescription = metadata.Text;
        }
        private static string TextOrDash(string value) { return string.IsNullOrWhiteSpace(value) ? "—" : value; }
        private bool SelectedSavedIsDraft()
        {
            if (view == ViewMode.Detail) return detailDraftId.HasValue && detailDraftIsDraft &&
                !confirmedApprovedDrafts.ContainsKey(detailDraftId.Value);
            if (view != ViewMode.Saved || saved == null || grid.SelectedRows.Count == 0 ||
                !(grid.SelectedRows[0].DataBoundItem is DataRowView row)) return false;
            Guid id = (Guid)row["DraftId"];
            return !confirmedApprovedDrafts.ContainsKey(id) && saved.Any(item => item.DraftId == id && item.Status == "Draft");
        }
        private static OperationActor CurrentActor()
        { return new OperationActor { UserName = SessionContext.CurrentUser, UserRole = SessionContext.CurrentRole }; }
        private void ShowCandidateRows()
        {
            var table = new DataTable();
            table.Columns.Add("Kind"); table.Columns.Add("SourceId"); table.Columns.Add("Date");
            table.Columns.Add("Party"); table.Columns.Add("Lines"); table.Columns.Add("Currency");
            if (candidates != null)
                foreach (InvoiceImportCandidate item in candidates)
                    table.Rows.Add(KindText(item.Kind), item.SourceInvoiceId, item.InvoiceDate.ToString("yyyy-MM-dd"),
                        item.PartyName, item.Lines.Count.ToString(), item.CurrencyCode);
            Bind(table, new[] { T("النوع", "Kind"), T("رقم المصدر", "Source ID"), T("التاريخ", "Date"),
                T("العميل/المورد", "Customer / supplier"), T("البنود", "Lines"), T("العملة", "Currency") });
        }
        private void ShowSavedRows()
        {
            if (view == ViewMode.Saved) { detailDraftId = null; detailDraftIsDraft = false; }
            hint.Text = T("راجع حالة كل مستند؛ المسودة لا تؤثر في الأرصدة، والمستند المعتمد أُثبت أثره بالفعل.",
                "Check each document's status: drafts do not affect balances; approved documents have already been posted.");
            var table = new DataTable();
            table.Columns.Add("DraftId", typeof(Guid)); table.Columns.Add("Kind"); table.Columns.Add("SourceSystem");
            table.Columns.Add("SourceId"); table.Columns.Add("Date"); table.Columns.Add("Party"); table.Columns.Add("Status");
            if (saved != null)
                foreach (InvoiceImportDraftSummary item in saved)
                    table.Rows.Add(item.DraftId, KindText(item.Kind), item.SourceSystem, item.SourceInvoiceId,
                        item.InvoiceDate.ToString("yyyy-MM-dd"), item.PartyName,
                        confirmedApprovedDrafts.ContainsKey(item.DraftId) || item.Status == "Approved" ? T("معتمدة", "Approved") :
                        item.Status == "Draft" ? T("مسودة", "Draft") : T("غير معروفة", "Unknown"));
            Bind(table, new[] { "", T("النوع", "Kind"), T("النظام المصدر", "Source system"),
                T("رقم المصدر", "Source ID"), T("التاريخ", "Date"), T("العميل/المورد", "Customer / supplier"), T("الحالة", "Status") });
            if (grid.Columns.Contains("DraftId")) grid.Columns["DraftId"].Visible = false;
            open.Enabled = !busy && grid.SelectedRows.Count > 0;
            approve.Enabled = !busy && !previewOnly && SelectedSavedIsDraft();
        }
        private void ShowDetailRows(InvoiceImportCandidate candidate)
        {
            var table = new DataTable();
            table.Columns.Add("Line"); table.Columns.Add("Medicine"); table.Columns.Add("Barcode");
            table.Columns.Add("Quantity"); table.Columns.Add("Price"); table.Columns.Add("Production");
            table.Columns.Add("Expiry"); table.Columns.Add("Batch"); table.Columns.Add("Gtin"); table.Columns.Add("Serial");
            foreach (InvoiceImportRow line in candidate.Lines)
                table.Rows.Add(line.LineNumber.ToString(), line.MedicineName, line.Barcode,
                    line.Quantity.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    line.UnitPrice.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    line.ProductionDate?.ToString("yyyy-MM-dd") ?? "", line.ExpiryDate?.ToString("yyyy-MM-dd") ?? "",
                    line.BatchNumber, line.Gtin, line.SerialNumber);
            Bind(table, new[] { T("السطر", "Line"), T("الصنف", "Medicine"), T("الباركود", "Barcode"),
                T("الكمية", "Quantity"), T("السعر", "Unit price"), T("الإنتاج", "Production"),
                T("الانتهاء", "Expiry"), T("التشغيلة", "Batch"), "GTIN", T("التسلسلي", "Serial") });
        }
        private string KindText(ImportedInvoiceKind kind)
        { return kind == ImportedInvoiceKind.Sale ? T("بيع", "Sale") : T("شراء", "Purchase"); }
        private void Bind(DataTable table, string[] titles)
        {
            grid.DataSource = table;
            for (int index = 0; index < titles.Length; index++)
                grid.Columns[index].HeaderText = titles[index];
            grid.AutoSizeColumnsMode = view == ViewMode.Detail || view == ViewMode.CandidateDetail
                ? DataGridViewAutoSizeColumnsMode.DisplayedCells : DataGridViewAutoSizeColumnsMode.Fill;
            grid.ClearSelection();
            ApplyView();
        }
        private void ClearConfirmedWriteStatus()
        {
            confirmedSavedCount = null;
            confirmedPostedReference = null;
            refreshSupportCode = null;
        }

        private void PresentConfirmedWriteStatus()
        {
            if (confirmedPostedReference != null && detailDraftId.HasValue && detailRecord != null)
            {
                detailRecord.Status = "Approved";
                detailRecord.ApprovedReference = confirmedPostedReference;
                detailDraftIsDraft = false;
                saved = new[] { new InvoiceImportDraftSummary {
                    DraftId = detailDraftId.Value, SourceSystem = detailRecord.Candidate.SourceSystem,
                    SourceInvoiceId = detailRecord.Candidate.SourceInvoiceId, Kind = detailRecord.Candidate.Kind,
                    InvoiceDate = detailRecord.Candidate.InvoiceDate, PartyName = detailRecord.Candidate.PartyName,
                    Status = "Approved" } };
            }
            view = ViewMode.Saved;
            ApplyView();
            ShowSavedRows();
            string message = confirmedPostedReference != null
                ? string.Format(T("اعتُمدت المسودة برقم {0}.", "Draft approved as {0}."), confirmedPostedReference)
                : string.Format(T("حُفظت {0} مسودة.", "Saved {0} drafts."), confirmedSavedCount ?? 0);
            if (!string.IsNullOrWhiteSpace(refreshSupportCode))
                message += Environment.NewLine + T("رمز الدعم: ", "Support code: ") + refreshSupportCode;
            SetStatus(message, true);
        }

        private async Task ShowSavedAfterSaveAsync(int savedCount, OperationActor actor)
        {
            confirmedSavedCount = savedCount;
            confirmedPostedReference = null;
            refreshSupportCode = null;
            view = ViewMode.Saved;
            ApplyView();
            await ShowSavedCoreAsync(true);
        }

        private async Task RefreshAfterConfirmedWriteAsync(int? savedCount, string postedReference, Guid? draftId, OperationActor actor)
        {
            if (savedCount.HasValue) confirmedSavedCount = savedCount.Value;
            if (!string.IsNullOrWhiteSpace(postedReference)) confirmedPostedReference = postedReference;
            refreshSupportCode = null;
            if (draftId.HasValue && !string.IsNullOrWhiteSpace(postedReference))
            {
                confirmedApprovedDrafts[draftId.Value] = postedReference;
                if (detailDraftId == draftId.Value && detailRecord != null)
                {
                    detailRecord.Status = "Approved";
                    detailRecord.ApprovedReference = postedReference;
                    detailDraftIsDraft = false;
                }
            }
            view = ViewMode.Saved;
            ApplyView();
            await ShowSavedCoreAsync(true);
        }
        private void SetBusy(bool value)
        {
            busy = value;
            newTab.Enabled = savedTab.Enabled = refresh.Enabled = close.Enabled = !value;
            choose.Enabled = template.Enabled = !value && !previewOnly;
            directMode.Enabled = draftMode.Enabled = !value;
            systemInput.Enabled = !value && candidates == null;
            save.Enabled = !value && candidates != null && !previewOnly &&
                (!DirectMode || grid.SelectedRows.Count > 0);
            approve.Enabled = !value && !previewOnly && SelectedSavedIsDraft();
            open.Enabled = !value && (view == ViewMode.Detail || view == ViewMode.CandidateDetail ||
                (grid.SelectedRows.Count > 0 && (view == ViewMode.Saved || candidates != null)));
        }
        private void SetStatus(string message, bool? success)
        {
            UiTheme.SetStatus(status, message, success);
            status.AccessibleName = status.Text;
            LayoutShell();
        }
    }
}



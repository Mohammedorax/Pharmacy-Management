using System;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace project_fo_3
{
    public partial class InventoryHomeForm : Form
    {
        private int activePage = 1;
        private int renderedTileColumns;

        public InventoryHomeForm()
        {
            InitializeComponent();
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            UpdateStyles();
            Text = "المخزون";
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = true;
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(1200, 760);
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            KeyPreview = true;
            UiTheme.Apply(this);
            ApplyInventoryVisualTheme();
            FitToWorkingArea();

            closeButton.Click += (s, e) => Close();
            tabs.Resize += (s, e) => AlignTabsRight();
            tileGrid.Resize += (s, e) =>
            {
                UpdateTileGridDensity();
                int columns = GetTileColumnCount();
                if (columns != renderedTileColumns && !tileGrid.IsDisposed)
                {
                    RenderPage();
                }
            };

            Load += (s, e) =>
            {
                AlignTabsRight();
            };
            KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); };
            SuspendLayout();
            BuildTabs();
            RenderPage();
            ResumeLayout(true);
        }

        private void FitToWorkingArea()
        {
            Rectangle workingArea = Screen.FromControl(this).WorkingArea;
            MaximumSize = workingArea.Size;
            MinimumSize = new Size(Math.Min(760, workingArea.Width), Math.Min(560, workingArea.Height));
            Size = new Size(Math.Min(Math.Max(Width, MinimumSize.Width), workingArea.Width), Math.Min(Math.Max(Height, MinimumSize.Height), workingArea.Height));
        }

        private void ApplyInventoryVisualTheme()
        {
            BackColor = UiTheme.Background;
            header.BackColor = UiTheme.PrimaryDark;
            tabs.BackColor = UiTheme.Background;
            tileGrid.BackColor = UiTheme.Background;
            actionFooter.BackColor = UiTheme.Surface;
            actionFooter.RightToLeft = RightToLeft.No;
            actionFooter.FlowDirection = UiDirection.IsLeftToRight
                ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;
            titleLabel.TextAlign = UiDirection.IsLeftToRight
                ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
            closeButton.AccessibleName = "إغلاق مركز المخزون";
            DesktopComponents.StyleOutlinedFooterAction(closeButton);
        }

        private void BuildTabs()
        {
            tabs.SuspendLayout();
            try
            {
                if (tabs.Controls.Count != 2)
                {
                    Control[] previousTabs = tabs.Controls.Cast<Control>().ToArray();
                    tabs.Controls.Clear();
                    foreach (Control previousTab in previousTabs) previousTab.Dispose();
                    for (int page = 1; page <= 2; page++)
                    {
                        int targetPage = page;
                        var button = new AccessibleAntdButton
                        {
                            Name = "inventoryPageTab" + page,
                            Width = 190,
                            Height = 46,
                            Cursor = Cursors.Hand,
                            Font = AppFonts.Create(10.5F, FontStyle.Bold),
                            BorderWidth = 1,
                            BackHover = UiTheme.Selection,
                            ForeHover = UiTheme.Text,
                            Radius = DesktopDesignTokens.ActionRadius(46),
                            TextAlign = ContentAlignment.MiddleCenter,
                            AccessibleRole = AccessibleRole.PageTab,
                            TabStop = true,
                            TabIndex = page - 1
                        };
                        button.Click += (s, e) =>
                        {
                            if (activePage == targetPage) return;
                            activePage = targetPage;
                            BuildTabs();
                            RenderPage();
                        };
                        tabs.Controls.Add(button);
                    }
                }
                for (int page = 1; page <= 2; page++)
                {
                    var button = tabs.Controls[page - 1] as AntdUI.Button;
                    ApplyTabState(button, page);
                }
                AlignTabsRight();
            }
            finally { tabs.ResumeLayout(true); }
        }

        private void ApplyTabState(AntdUI.Button button, int page)
        {
            bool selected = page == activePage;
            button.Text = UiLanguageCatalog.GetDisplayText(GetPageTitle(page));
            button.AccessibleName = button.Text;
            button.RightToLeft = UiDirection.IsLeftToRight ? RightToLeft.No : RightToLeft.Yes;
            button.DefaultBack = selected ? UiTheme.SurfaceVariant : UiTheme.Surface;
            button.BackColor = selected ? UiTheme.SurfaceVariant : UiTheme.Surface;
            button.ForeColor = selected ? UiTheme.Text : UiTheme.MutedText;
            button.DefaultBorderColor = selected ? UiTheme.Primary : UiTheme.Border;
            button.AccessibleDescription = (selected
                ? (UiDirection.IsLeftToRight ? "Current tab. " : "التبويب الحالي. ")
                : string.Empty) + (page == 1
                ? (UiDirection.IsLeftToRight ? "Open reports, stock counts and alerts." : "عرض التقارير والجرد والتنبيهات وحركات المخزون.")
                : (UiDirection.IsLeftToRight ? "Open stock balance, receipts and issues." : "عرض عمليات الرصيد الافتتاحي والتوريد والصرف."));
        }

        private static string GetPageTitle(int page)
        {
            return page == 1 ? "المتابعة والتقارير" : "عمليات المخزون";
        }

        private void AlignTabsRight()
        {
            tabs.FlowDirection = UiDirection.IsLeftToRight ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;
            tabs.RightToLeft = RightToLeft.No;
            tabs.Padding = new Padding(28, 10, 28, 0);
        }

        private void RenderPage()
        {
            tileGrid.SuspendLayout();
            try
            {
                Control[] previousTiles = tileGrid.Controls.Cast<Control>().ToArray();
                tileGrid.Controls.Clear();
                foreach (Control previousTile in previousTiles) previousTile.Dispose();
                InventoryTile[] tiles = GetPageTiles(activePage);
                UpdateTileGridDensity();
                int columns = GetTileColumnCount();
                renderedTileColumns = columns;
                tileGrid.ColumnCount = columns;
                tileGrid.ColumnStyles.Clear();
                for (int column = 0; column < columns; column++)
                {
                    tileGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / columns));
                }

                int rows = Math.Max(1, (int)Math.Ceiling(tiles.Length / (double)columns));
                tileGrid.RowCount = rows;
                tileGrid.RowStyles.Clear();
                for (int row = 0; row < rows; row++)
                {
                    tileGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100F / rows));
                }

                for (int i = 0; i < tiles.Length; i++)
                {
                    tileGrid.Controls.Add(CreateTile(tiles[i]), i % columns, i / columns);
                }
            }
            finally
            {
                tileGrid.ResumeLayout(true);
            }
        }

        private void UpdateTileGridDensity()
        {
            if (tileGrid.ClientSize.Width <= 0 || tileGrid.ClientSize.Height <= 0) return;
            // Keep touch-sized tiles readable without letting them become empty billboards on wide displays.
            int side = Math.Max(28, (tileGrid.ClientSize.Width - 1260) / 2);
            int usableWidth = Math.Max(0, tileGrid.ClientSize.Width - 2 * side);
            int columns = usableWidth >= 960 ? 3 : usableWidth >= 620 ? 2 : 1;
            int rows = Math.Max(1, (GetPageTiles(activePage).Length + columns - 1) / columns);
            int top = Math.Max(24, (tileGrid.ClientSize.Height - rows * 228) / 2);
            var target = new Padding(side, top, side, top);
            if (tileGrid.Padding != target) tileGrid.Padding = target;
        }
        private int GetTileColumnCount()
        {
            int availableWidth = Math.Max(0, tileGrid.ClientSize.Width - tileGrid.Padding.Horizontal);
            return availableWidth >= 960 ? 3 : availableWidth >= 620 ? 2 : 1;
        }

        private InventoryTile[] GetPageTiles(int page)
        {
            if (page == 1)
            {
                return new[]
                {
                    new InventoryTile("كشف جرد المخزن", AppIcon.Database, () => ShowChild(new InventoryStockReportForm(false))),
                    new InventoryTile("جرد التشغيلات والصلاحيات", AppIcon.Database, () => ShowChild(new InventoryStockReportForm(true))),
                    new InventoryTile("حركة صنف محددة", AppIcon.Refresh, () => ShowChild(new InventoryMovementForm())),
                    new InventoryTile("تقرير إجمالي حركة الصنف", AppIcon.Report, () => ShowChild(new InventoryMovementSummaryForm())),
                    new InventoryTile("تسوية المخزون", AppIcon.Settings, () => OpenStockOperation(InventoryOperationMode.Adjustment), AppOperation.PostInventoryOperation),
                    new InventoryTile("تنبيهات المخزون", AppIcon.Compliance, () => ShowChild(new InventoryAlertsForm()))
                };
            }

            return new[]
            {
                new InventoryTile("مخزون أول المدة", AppIcon.Database, () => OpenStockOperation(InventoryOperationMode.OpeningStock), AppOperation.PostInventoryOperation),
                new InventoryTile("التوريد المخزني", AppIcon.Add, () => OpenStockOperation(InventoryOperationMode.Receipt), AppOperation.PostInventoryOperation),
                new InventoryTile("الصرف المخزني", AppIcon.Sale, () => OpenStockOperation(InventoryOperationMode.Issue), AppOperation.PostInventoryOperation),
                new InventoryTile("معدل الدوران والركود للأصناف", AppIcon.Refresh, () => ShowChild(new InventoryTurnoverForm()))
            };
        }

        private void OpenStockOperation(InventoryOperationMode mode)
        {


            if (!PermissionService.EnsureAllowed(AppOperation.PostInventoryOperation, this)) return;
            try
            {
                if (!InventoryService.IsLegacyManualStockOperationAllowed())
                {
                    AppNotifications.Show(this,
                        "الوضع التجاري يمنع تعديل الرصيد المجمع مباشرة حتى تبقى كميات الدفعات والقيود متطابقة. " +
                        (mode == InventoryOperationMode.Receipt || mode == InventoryOperationMode.OpeningStock
                            ? "أدخل الكميات عبر فاتورة مشتريات معتمدة."
                            : "استخدم عملية تجارية موثقة للدفعات؛ هذه الحركة اليدوية غير متاحة."),
                        "عمليات المخزون", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                ShowChild(new InventoryOperationForm(mode));
            }
            catch (Exception exception) { UiTheme.ShowOperationIssue(this, "فتح عملية المخزون", exception); }
        }

        private Control CreateTile(InventoryTile tile)
        {
            bool allowed = !tile.Operation.HasValue || PermissionService.CanExecute(tile.Operation.Value);
            AntdUI.Button button = new AccessibleAntdButton
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(18, 14, 18, 14),
                Type = AntdUI.TTypeMini.Default,
                DefaultBack = allowed ? UiTheme.Surface : UiTheme.SurfaceVariant,
                BackColor = allowed ? UiTheme.Surface : UiTheme.SurfaceVariant,
                ForeColor = allowed ? UiTheme.Text : UiTheme.MutedText,
                Cursor = allowed ? Cursors.Hand : Cursors.No,
                RightToLeft = UiDirection.IsLeftToRight ? RightToLeft.No : RightToLeft.Yes,
                Text = UiLanguageCatalog.GetDisplayText(tile.Title) + (allowed ? string.Empty : Environment.NewLine + UiLanguageCatalog.GetDisplayText("صلاحية مطلوبة")),
                Font = AppFonts.Create(12F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                IconSvg = OutlinedIcons.Svg(tile.Icon, !UiDirection.IsLeftToRight),
                IconPosition = AntdUI.TAlignMini.Top,
                IconSize = new Size(40, 40),
                IconGap = 0.45F,
                BorderWidth = 1,
                DefaultBorderColor = UiTheme.Border,
                BackHover = null,
                ForeHover = allowed ? UiTheme.Text : UiTheme.MutedText,
                AccessibleName = UiLanguageCatalog.GetDisplayText(tile.Title),
                AccessibleRole = AccessibleRole.PushButton,
                AccessibleDescription = allowed
                    ? (UiDirection.IsLeftToRight ? "Open " : "فتح ") + UiLanguageCatalog.GetDisplayText(tile.Title)
                    : (UiDirection.IsLeftToRight ? "Permission required: " : "لا تملك صلاحية ") + UiLanguageCatalog.GetDisplayText(PermissionService.GetArabicName(tile.Operation.Value)),
                TabStop = true
            };
            button.Click += (s, e) => tile.Open();
            button.SizeChanged += (s, e) => FitTileContent(button);
            FitTileContent(button);
            return button;
        }

        private static void FitTileContent(AntdUI.Button button)
        {
            if (button == null || button.IsDisposed || button.ClientSize.Width <= 0 || button.ClientSize.Height <= 0)
                return;
            bool compact = button.ClientSize.Height < 112;
            float targetFontSize = compact ? 10F : 12F;
            if (Math.Abs(button.Font.SizeInPoints - targetFontSize) > 0.1F)
                button.Font = AppFonts.Create(targetFontSize, FontStyle.Bold);
            button.IconPosition = compact
                ? (UiDirection.IsLeftToRight ? AntdUI.TAlignMini.Left : AntdUI.TAlignMini.Right)
                : AntdUI.TAlignMini.Top;
            int iconSize = compact ? 24 : Math.Max(32, Math.Min(42, Math.Min(button.ClientSize.Width, button.ClientSize.Height) / 3));
            button.IconSize = new Size(iconSize, iconSize);
            button.Radius = DesktopDesignTokens.CardRadius;
        }

        private void ShowChild(Form form)
        {
            using (form)
            {
                form.ShowDialog(this);
            }
        }

        private sealed class InventoryTile
        {
            public InventoryTile(string title, AppIcon icon, Action open, AppOperation? operation = null)
            {
                Title = title;
                Icon = icon;
                Open = open;
                Operation = operation;
            }

            public string Title { get; private set; }
            public AppIcon Icon { get; private set; }
            public Action Open { get; private set; }
            public AppOperation? Operation { get; private set; }
        }
    }

    internal enum InventoryOperationMode
    {
        OpeningStock,
        Adjustment,
        Receipt,
        Issue
    }

    // UI lookup adapter: display labels may include a company while the service
    // receives the stable MedName column used by existing inventory queries.
    // Keep a native ComboBox for mixed Arabic/Latin drug labels and arrow keys.
    internal static class InventoryMedicineSelection
    {
        internal static void Bind(ComboBox selector, DataTable medicines)
        {
            selector.DataSource = null;
            selector.DisplayMember = "DisplayName";
            selector.ValueMember = "MedName";
            if (medicines != null) selector.DataSource = medicines;
        }

        internal static string SelectedName(ComboBox selector)
        {
            return selector.SelectedValue == null || selector.SelectedValue is DataRowView
                ? string.Empty : Convert.ToString(selector.SelectedValue);
        }
    }

    internal sealed class InventorySummaryItem
    {
        public InventorySummaryItem(string title, string value)
        {
            Title = title;
            Value = value;
        }

        public string Title { get; private set; }
        public string Value { get; private set; }
    }

    internal sealed partial class InventoryStockReportForm : InventoryDataForm
    {
        private readonly bool lotMode;
        private readonly bool loadData;
        private readonly TextBox search = new TextBox { Name = "inventoryStockSearch", MaxLength = 200 };
        private readonly AntdUI.Panel searchFrame;
        private readonly AntdUI.Checkbox includeZero = DesktopComponents.Checkbox(
            "inventoryStockIncludeZero", "إظهار الأرصدة الصفرية", true);

        public InventoryStockReportForm(bool lotMode)
            : this(lotMode, true)
        {
        }

        private readonly Func<InventoryStockReadSession> stockSessionFactory;
        internal InventoryStockReportForm(bool lotMode, bool loadData, Func<InventoryStockReadSession> sessionFactory = null)
            : base(lotMode ? "جرد التشغيلات والصلاحيات" : "كشف جرد المخزن", loadData)
        {
            this.lotMode = lotMode;
            this.loadData = loadData;
            searchFrame = DesktopComponents.NativeTextFrame(search, "inventoryStockSearchFrame");
            InitializeComponent();
            SetStockOutputReady(false);
            stockSessionFactory = sessionFactory ?? ApplicationServices.CreateInventoryStockReadSession;
            if (loadData) stockReadSession = stockSessionFactory();
            UiSessionSubscription.Attach(this, () =>
            {
                if (!loadData) return;
                CancelStockRead(); stockReadSession = null; ClearStockResult();
                UiTheme.SetStatus(StatusLabel, Present("تغيرت جلسة المستخدم. أعد تحميل القائمة."), false);
            });
            search.TextChanged += (sender, args) => UiDirection.ApplyContentDirection(search, leftToRight);

            ApplyPresentation(UiDirection.IsLeftToRight);
        }

        internal void ApplyPresentation(bool useLeftToRight)
        {
            ApplyInventoryPresentation(useLeftToRight);
            search.AccessibleName = Present("بحث باسم الصنف أو الباركود أو الشركة");
            // Windows supplies bidi shaping; the logical search value is never rewritten.
            UiDirection.ApplyContentDirection(search, leftToRight);
            DesktopComponents.StyleNativeTextFrame(searchFrame, search);
            includeZero.Text = Present(lotMode ? "إظهار التشغيلات الصفرية" : "إظهار الأرصدة الصفرية");
            includeZero.AccessibleName = includeZero.Text;
            includeZero.RightToLeft = leftToRight ? RightToLeft.No : RightToLeft.Yes;
            includeZero.TextAlign = leftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
            SetEmptyGridHint(Present(lotMode
                ? "ابحث أو اضغط تحديث لعرض أرصدة التشغيلات وصلاحياتها."
                : "ابحث عن صنف أو اضغط تحديث لعرض أرصدة المخزون الحالية."));
        }

        private async void RefreshData() { await RefreshStockReportAsync(); }
    }

    internal sealed partial class InventoryMovementForm : InventoryDataForm
    {
        private readonly bool loadData;
        private readonly ComboBox medicine = new ComboBox { Name = "inventoryMovementMedicine", DropDownStyle = ComboBoxStyle.DropDownList };
        private AntdUI.Panel medicineFrame;
        private readonly AntdUI.DatePicker fromDate = DesktopComponents.DatePicker("inventoryMovementFromDate", "من تاريخ", DateTime.Today.AddMonths(-1));
        private readonly AntdUI.DatePicker toDate = DesktopComponents.DatePicker("inventoryMovementToDate", "إلى تاريخ", DateTime.Today);

        public InventoryMovementForm()
            : this(true)
        {
        }

        internal InventoryMovementForm(bool loadData)
            : base("حركة صنف محددة", loadData)
        {
            this.loadData = loadData;
            InitializeComponent();
            SetMovementOutputReady(false);
            UiSessionSubscription.Attach(this, () =>
            {
                if (!loadData) return;
                CancelMovementRead();
                movementSession = null;
                movementLookupReady = false;
                bindingMovementMedicine = true;
                try { medicine.DataSource = null; medicine.Items.Clear(); medicine.SelectedIndex = -1; }
                finally { bindingMovementMedicine = false; }
                ClearMovementResults();
                EnableMovementFilters(true);
                UiTheme.SetStatus(StatusLabel, Present("تغيرت جلسة المستخدم. أعد تحميل القائمة."), false);
            });
            ApplyPresentation(UiDirection.IsLeftToRight);
        }

        internal void ApplyPresentation(bool useLeftToRight)
        {
            ApplyInventoryPresentation(useLeftToRight);
            medicine.AccessibleName = Present("الصنف");
            medicine.RightToLeft = useLeftToRight ? RightToLeft.No : RightToLeft.Yes;
            if (medicineFrame != null) DesktopComponents.StyleNativeSelectFrame(medicineFrame, medicine, useLeftToRight);
            fromDate.AccessibleName = Present("من تاريخ");
            toDate.AccessibleName = Present("إلى تاريخ");
            SetEmptyGridHint(Present("اختر الصنف والفترة، ثم اضغط تحديث لعرض حركات المخزون."));
        }

        private string SelectedMedicine()
        {
            return InventoryMedicineSelection.SelectedName(medicine);
        }

        private async void RefreshData() { await RefreshMovementAsync(); }

        internal void PresentMovementTable(DataTable table,Action validateContext=null)
        {
                SetEmptyGridHint(Present("لا توجد حركات مخزون للصنف والفترة المحددين."));
                BindGrid(table,validateContext);
                SetSummary(
                    Metric("عدد الحركات", table.Rows.Count.ToString("N0")),
                    Metric("إجمالي الوارد", SumDecimal(table, "الوارد").ToString("#,##0.###")),
                    Metric("إجمالي الصادر", SumDecimal(table, "الصادر").ToString("#,##0.###")),
                    Metric("صافي الحركة", SumDecimal(table, "صافي الحركة").ToString("#,##0.###")));
                SetLoadedStatus(table, "تم تحميل حركة الصنف.");
                SetMovementOutputReady(table.Rows.Count > 0);
        }
    }

    internal sealed partial class InventoryMovementSummaryForm : InventoryDataForm
    {
        private readonly bool loadData;
        private readonly AntdUI.DatePicker fromDate = DesktopComponents.DatePicker("inventorySummaryFromDate", "من تاريخ", DateTime.Today.AddMonths(-1));
        private readonly AntdUI.DatePicker toDate = DesktopComponents.DatePicker("inventorySummaryToDate", "إلى تاريخ", DateTime.Today);
        private readonly TextBox search = new TextBox { Name = "inventorySummarySearch" };
        private readonly AntdUI.Panel searchFrame;

        public InventoryMovementSummaryForm()
            : this(true)
        {
        }

        internal InventoryMovementSummaryForm(bool loadData)
            : base("تقرير إجمالي حركة الصنف", loadData)
        {
            this.loadData = loadData;
            searchFrame = DesktopComponents.NativeTextFrame(search, "inventorySummarySearchFrame");
            InitializeComponent();
            FilterPanel.Resize += (sender, args) => FitSearchWidth();
            FitSearchWidth();
            search.TextChanged += (s, e) => { UiDirection.ApplyContentDirection(search, leftToRight); InvalidateSummaryResults(); };
            fromDate.TextChanged += (s, e) => InvalidateSummaryResults();
            toDate.TextChanged += (s, e) => InvalidateSummaryResults();
            FormClosed += (s, e) => CancelSummaryRead();
            Disposed += (s, e) => CancelSummaryRead();
            UiSessionSubscription.Attach(this, () =>
            {
                if (!loadData) return;
                CancelSummaryRead();
                summaryReadSession = null;
                ClearSummaryResults();
                UiTheme.SetStatus(StatusLabel, Present("تغيرت جلسة المستخدم. أعد تحميل القائمة."), false);
            });

            ApplyPresentation(UiDirection.IsLeftToRight);
        }

        internal void ApplyPresentation(bool useLeftToRight)
        {
            ApplyInventoryPresentation(useLeftToRight);
            search.AccessibleName = Present("بحث عن صنف");
            UiDirection.ApplyContentDirection(search, leftToRight);
            DesktopComponents.StyleNativeTextFrame(searchFrame, search);
            fromDate.AccessibleName = Present("من تاريخ");
            toDate.AccessibleName = Present("إلى تاريخ");
            SetEmptyGridHint(Present("اضغط تحديث لعرض ملخص حركة الأصناف خلال الفترة المحددة."));
        }

        private void FitSearchWidth()
        {
            if (searchFrame.Parent == null || fromDate.Parent == null || toDate.Parent == null) return;
            int available = FilterPanel.Width - FilterPanel.Padding.Horizontal -
                SystemInformation.VerticalScrollBarWidth - fromDate.Parent.Width - fromDate.Parent.Margin.Horizontal -
                toDate.Parent.Width - toDate.Parent.Margin.Horizontal - searchFrame.Parent.Margin.Horizontal;
            int width = Math.Max(230, Math.Min(280, available));
            if (searchFrame.Parent.Width == width) return;
            searchFrame.Parent.Width = width;
            ReflowTopPanels();
        }

        private async void RefreshData()
        {
            try { await RefreshMovementSummaryAsync(); }
            catch (Exception error) { UiTheme.SetStatus(StatusLabel, Present("تعذر تحميل التقرير: ") + error.Message, false); }
        }
    }

    internal sealed partial class InventoryOperationForm : InventoryDataForm
    {
        private readonly InventoryOperationMode mode;
        private readonly bool loadData;
        private readonly ComboBox medicine = new ComboBox { Name = "inventoryOperationMedicine", DropDownStyle = ComboBoxStyle.DropDownList };
        private bool bindingMedicine;
        private readonly Label currentQuantity = new Label();
        private readonly AntdUI.Input quantity = new AntdUI.Input { Name = "inventoryOperationQuantity" };
        private readonly AntdUI.Input unitAmount = new AntdUI.Input { Name = "inventoryOperationUnitAmount" };
        private readonly TextBox notes = new TextBox { Name = "inventoryOperationNotes", Multiline = true, ScrollBars = ScrollBars.None };
        private readonly Label preview = new Label();
        private readonly AntdUI.Panel currentQuantityFrame, previewFrame, notesFrame;
        private int currentValue;
        private bool inventoryWriteBusy;
        private bool inventorySessionInvalidated;
        private bool acceptedInventoryDraft;
        private PharmacyManagement.Application.InventoryPostRequest pendingInventoryRequest;
        private InventoryPostingSession pendingInventorySession;
        private readonly ToolTip inventoryStatusDetails = new ToolTip();

        public InventoryOperationForm(InventoryOperationMode mode)
            : this(mode, true)
        {
        }

        internal InventoryOperationForm(InventoryOperationMode mode, bool loadData)
            : base(GetTitle(mode), loadData)
        {
            this.mode = mode;
            this.loadData = loadData;
            currentQuantityFrame = DesktopComponents.ReadOnlyLabelFrame(currentQuantity, "inventoryOperationBalanceFrame");
            previewFrame = DesktopComponents.ReadOnlyLabelFrame(preview, "inventoryOperationPreviewFrame");
            notesFrame = DesktopComponents.NativeTextFrame(notes, "inventoryOperationNotesFrame", true);
            InitializeComponent(mode);
            UiSessionSubscription.Attach(this, () =>
            {
                if (!this.loadData) return;
                inventorySessionInvalidated = true;
                CancelInventoryRead(); inventoryReadReady = false;
                bindingMedicine = true;
                try { medicine.DataSource = null; medicine.Items.Clear(); medicine.SelectedIndex = -1; }
                finally { bindingMedicine = false; }
                quantity.Clear(); unitAmount.Clear(); notes.Clear(); currentValue = 0;
                currentQuantity.Text = "-"; preview.Text = "-";
                BindGrid(new DataTable()); SetSummary(Metric("آخر الحركات", "-"), Metric("الرصيد الحالي", "-"));
                FilterPanel.Enabled = false; Grid.Enabled = false; SetInventoryCommitEnabled(false);
                SetInventoryRecoveryStatus("تغيرت جلسة المستخدم؛ أغلق الحركة وافتحها مجددًا لمراجعة الطلب غير المؤكد.", string.Empty, false);
            });
            FormClosed += (sender, args) => inventoryStatusDetails.Dispose();
            ApplyPresentation(UiDirection.IsLeftToRight);
            FormClosing += (sender, args) => { if (inventoryWriteBusy && args.CloseReason == CloseReason.UserClosing) args.Cancel = true; };
        }

        internal void ApplyPresentation(bool useLeftToRight)
        {
            ApplyInventoryPresentation(useLeftToRight);
            medicine.AccessibleName = Present("الصنف");
            medicine.RightToLeft = leftToRight ? RightToLeft.No : RightToLeft.Yes;
            quantity.AccessibleName = Present(GetQuantityCaption(mode));
            unitAmount.AccessibleName = Present(mode == InventoryOperationMode.Receipt ? "تكلفة الوحدة" : "سعر الوحدة");
            notes.AccessibleName = Present("سبب الحركة أو مرجعها");
            notes.RightToLeft = leftToRight ? RightToLeft.No : RightToLeft.Yes;
            UiDirection.ApplyContentDirection(notes, leftToRight);
            DesktopComponents.StyleNativeTextFrame(notesFrame, notes);
            notesFrame.BackColor = Color.Transparent;
            DesktopComponents.StyleReadOnlyLabelFrame(currentQuantityFrame, currentQuantity);
            DesktopComponents.StyleReadOnlyLabelFrame(previewFrame, preview);
            AntdUI.Button commit = Controls.Find("inventoryOperationCommit", true).FirstOrDefault() as AntdUI.Button;
            if (commit != null)
            {
                commit.Text = Present("اعتماد الحركة");
                commit.AccessibleName = commit.Text;
                commit.IconPosition = leftToRight ? AntdUI.TAlignMini.Left : AntdUI.TAlignMini.Right;
            }
            SetEmptyGridHint(Present("اختر الصنف وأدخل تفاصيل الحركة ثم اعتمدها لعرض أحدث الحركات."));
            UpdatePreview();
        }

        private static string GetTitle(InventoryOperationMode mode)
        {
            switch (mode)
            {
                case InventoryOperationMode.OpeningStock:
                    return "مخزون أول المدة";
                case InventoryOperationMode.Receipt:
                    return "التوريد المخزني";
                case InventoryOperationMode.Issue:
                    return "الصرف المخزني";
                default:
                    return "تسوية المخزون";
            }
        }

        private static string GetQuantityCaption(InventoryOperationMode mode)
        {
            switch (mode)
            {
                case InventoryOperationMode.OpeningStock:
                    return "رصيد أول المدة";
                case InventoryOperationMode.Adjustment:
                    return "الكمية الفعلية";
                case InventoryOperationMode.Receipt:
                    return "كمية التوريد";
                case InventoryOperationMode.Issue:
                    return "كمية الصرف";
                default:
                    return "الكمية";
            }
        }



        private void SetMedicineOptions(DataTable medicines)
        {
            bindingMedicine = true;
            try { InventoryMedicineSelection.Bind(medicine, medicines); }
            finally { bindingMedicine = false; }
        }

        private string SelectedMedicine()
        {
            return InventoryMedicineSelection.SelectedName(medicine);
        }



        // هذا تحقق إرشادي للواجهة فقط؛ الخدمة تتحقق مرة أخرى داخل المعاملة لمنع السباق بين الأجهزة.
        internal static bool IsLocallyValidQuantity(InventoryOperationMode mode, int value)
        {
            return mode == InventoryOperationMode.OpeningStock || mode == InventoryOperationMode.Adjustment
                ? value >= 0
                : value > 0;
        }

        private void UpdatePreview()
        {
            if(inventoryAsyncReads&&!inventoryReadReady){preview.Text="-";return;}
            int typed;
            if (!int.TryParse(quantity.Text.Trim(), out typed))
            {
                UiTheme.SetStatus(preview, Present("أدخل كمية صحيحة."), null);
                return;
            }

            if (!IsLocallyValidQuantity(mode, typed))
            {
                UiTheme.SetStatus(preview,
                    mode == InventoryOperationMode.OpeningStock || mode == InventoryOperationMode.Adjustment
                        ? Present("الرصيد المحدد لا يمكن أن يكون أقل من صفر.")
                        : Present("كمية التوريد أو الصرف يجب أن تكون أكبر من صفر."),
                    false);
                return;
            }

            try
            {
                bool setBalance=mode==InventoryOperationMode.OpeningStock||mode==InventoryOperationMode.Adjustment;
                var change=PharmacyManagement.Domain.InventoryQuantityCalculator.Calculate(currentValue,
                    setBalance?(int?)typed:null,setBalance?(int?)null:(mode==InventoryOperationMode.Receipt?typed:-typed));
                UiTheme.SetStatus(preview,string.Format(Present("بعد العملية: {0:N0} | الفرق: {1:N0}"),change.After,change.Delta),true);
            }
            catch(PharmacyManagement.Domain.InventoryQuantityException ex)
            {
                UiTheme.SetStatus(preview,Present(ex.Message),false);
            }
        }
        private async void SaveOperation()
        {
            if (inventorySessionInvalidated) return;
            if (inventoryWriteBusy || acceptedInventoryDraft)
            {
                if (acceptedInventoryDraft) UiTheme.SetStatus(StatusLabel, Present("هذه الحركة محفوظة بالفعل؛ أغلق النافذة وافتح حركة جديدة."), null);
                return;
            }
            if (!loadData)
            {
                UiTheme.SetStatus(StatusLabel, Present("معاينة عملية المخزون؛ لم تُحفظ بيانات ولم تُقرأ قاعدة البيانات."), null);
                return;
            }

            if(inventoryAsyncReads&&!inventoryReadReady)return;
            if(inventoryRecoveryUnavailable){SetInventoryRecoveryStatus("تعذر فتح استعادة الحركة؛ الاعتماد غير متاح.",StatusLabel.AccessibleDescription,false);return;}

            if (!PermissionService.EnsureAllowed(AppOperation.PostInventoryOperation, this))
            {
                return;
            }

            try
            {
                string selectedMedicine = SelectedMedicine();
                int enteredQuantity = ParseInt(quantity, Present(GetQuantityCaption(mode)));
                if (!ValidateBeforeSave(selectedMedicine, enteredQuantity))
                {
                    return;
                }

                decimal? money = ParseOptionalMoney(unitAmount, Present(mode == InventoryOperationMode.Receipt ? "تكلفة الوحدة" : "سعر الوحدة"));

                if (pendingInventoryRequest == null && (mode == InventoryOperationMode.OpeningStock || mode == InventoryOperationMode.Adjustment) &&
                    !ConfirmSetBalance(selectedMedicine, enteredQuantity))
                {
                    UiTheme.SetStatus(StatusLabel, Present("تم إلغاء العملية قبل تعديل الرصيد."), null);
                    return;
                }

                var request=new PharmacyManagement.Application.InventoryPostRequest(
                    mode==InventoryOperationMode.OpeningStock?PharmacyManagement.Application.InventoryPostKind.OpeningStock:
                    mode==InventoryOperationMode.Adjustment?PharmacyManagement.Application.InventoryPostKind.Adjustment:
                    mode==InventoryOperationMode.Receipt?PharmacyManagement.Application.InventoryPostKind.Receipt:PharmacyManagement.Application.InventoryPostKind.Issue,
                    "INV-"+Guid.NewGuid().ToString("N"),selectedMedicine,enteredQuantity,money,notes.Text);
                request=BindInventorySubmission(request);
                await RunInventoryWriteWithRefreshAsync(() => PostInventorySubmissionAsync(request),
                    () => PrepareNextInventoryDraft(request),
                    () => RefreshInventorySnapshotAsync(true));
            }
            catch (Exception ex)
            {
                if (inventorySessionInvalidated || IsDisposed || Disposing) return;
                ShowInventoryWriteIssue(ex);
            }
        }

        internal System.Threading.Tasks.Task<PharmacyManagement.Application.InventoryPostResult> PostInventorySubmissionAsync(PharmacyManagement.Application.InventoryPostRequest request)
        {
            if (inventorySessionInvalidated) throw new InvalidOperationException("تغيرت جلسة الحركة؛ أعد فتحها قبل الاعتماد.");
            if (pendingInventorySession == null)
                pendingInventorySession = InventoryPostingDesktopService.CaptureSession();
            pendingInventorySession.EnsureContext();
            if(loadData && pendingInventoryStore==null)throw new InvalidOperationException("استعادة الحركة غير جاهزة؛ لم ترسل محاولة حفظ.");
            if(pendingInventoryStore!=null && pendingInventoryStore.ContextKey!=pendingInventorySession.ContextKey)throw new InvalidOperationException("سياق ملف الحركة مختلف.");
            pendingInventoryStore?.Save(request);
            return pendingInventorySession.PostAsync(request);
        }
        internal void HandleInventoryWriteIssue(Exception ex)
        {
            string sourceId = pendingInventoryRequest?.SourceId;
            bool confirmedRejection = false;
            var rejected=ex as PharmacyManagement.Application.InventoryPostNotCommittedException;
            if(rejected!=null&&pendingInventoryRequest!=null&&string.Equals(rejected.SourceId,pendingInventoryRequest.SourceId,StringComparison.Ordinal))
            {
                try{pendingInventoryStore?.Resolve(rejected.SourceId);}
                catch(Exception cleanupError)
                {
                    AppDiagnostics.Log("InventoryConfirmedRejection", ex);
                    SetInventoryIssueStatus("رفض الحفظ مؤكد؛ تعذر تحديث ملف الاستعادة.", cleanupError, false, sourceId);
                    return;
                }
                pendingInventoryRequest=null;
                confirmedRejection = true;
                if(pendingInventoryStore==null)pendingInventorySession=null;
            }
            string inputHint = InventoryInputIssue(ex);
            string message = pendingInventoryRequest != null
                ? Present("تعذر تأكيد الحفظ؛ أعد المحاولة بنفس بيانات الحركة.")
                : confirmedRejection || inputHint != null
                    ? string.Format(Present("لم يتم الحفظ: {0}"), inputHint ?? Present("راجع بيانات الحركة أو الفترة المحاسبية ثم أعد المحاولة."))
                    : Present("تعذر إكمال عملية المخزون؛ راجع حالة الحركة قبل إعادة المحاولة.");
            SetInventoryIssueStatus(message, ex, false, sourceId, pendingInventoryRequest == null ? null : inputHint);
        }
        private void ShowInventoryWriteIssue(Exception error)
        {
            HandleInventoryWriteIssue(error);
            AppNotifications.Show(this, StatusLabel.AccessibleDescription, Present("تنفيذ عملية المخزون"),
                MessageBoxButtons.OK, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1, leftToRight);
        }

        private string InventoryInputIssue(Exception error)
        {
            // Exact known business validation only. Never echo arbitrary infrastructure messages.
            if (!(error is InvalidOperationException) && !(error is UnauthorizedAccessException)) return null;
            string[] known = {
                "نوع حركة المخزون غير صالح.",
                "مخزون أول المدة لا يمكن أن يكون أقل من صفر.",
                "الكمية الفعلية لا يمكن أن تكون أقل من صفر.",
                "كمية التوريد يجب أن تكون أكبر من صفر.",
                "كمية الصرف يجب أن تكون أكبر من صفر.",
                "مبلغ الوحدة غير صالح أو يتجاوز حد التخزين.",
                "مرجع الحركة مطلوب.",
                "مرجع الحركة يتجاوز حد التخزين.",
                "اختر الصنف أولاً.",
                "اسم الصنف يتجاوز حد التخزين.",
                "أدخل سبب الحركة أو مرجعها قبل الاعتماد.",
                "سبب الحركة يجب ألا يتجاوز 500 حرف.",
                "مرجع حركة المخزون غير صالح.",
                "لا يمكن تنفيذ العملية لأن الرصيد سيصبح أقل من صفر.",
                "الرصيد الناتج يتجاوز حد تخزين الكمية.",
                "فرق حركة المخزون يتجاوز حد تخزين الكمية.",
                "لا تملك صلاحية تنفيذ حركة المخزون.",
                "نتيجة الحركة السابقة غير مؤكدة؛ استعد بياناتها وأعد المحاولة قبل اعتماد حركة مختلفة.",
                "تغيرت جلسة الحركة؛ أعد فتحها قبل الاعتماد.",
                "استعادة الحركة غير جاهزة؛ لم ترسل محاولة حفظ.",
                "سياق ملف الحركة مختلف.",
                "يوجد طلب غير مؤكد؛ لا يمكن استبداله بحركة أخرى.",
                "مرجع الحركة مستخدم لبيانات مختلفة أو سجل غير متسق؛ راجع الحركة المحفوظة.",
                "سجل حركة المخزون غير متسق؛ لا يمكن تأكيد إعادة الطلب.",
                "سجل اعتماد حركة المخزون غير متسق؛ لا يمكن تأكيد إعادة الطلب.",
                "تعذر تأكيد حركة المخزون الآن؛ أعد محاولة الطلب نفسه.",
                "تعذر تحديث رصيد الصنف.",
                "وضع البيع التجاري مطلوب؛ لا يمكن إجراء تسوية أو توريد أو صرف يدوي على الرصيد المجمع. استخدم عملية دفعات موثقة حتى لا تنفصل كمية الصنف عن التشغيلة.",
            };
            foreach (string value in known)
                if (string.Equals(error.Message, value, StringComparison.Ordinal) ||
                    string.Equals(error.Message, Present(value), StringComparison.Ordinal)) return Present(value);
            foreach (string caption in new[] { GetQuantityCaption(mode), "تكلفة الوحدة", "سعر الوحدة" })
            foreach (string template in new[] { "أدخل قيمة صحيحة في حقل {0}.", "أدخل قيمة مالية صحيحة في حقل {0}.", "يجب ألا تكون قيمة {0} سالبة." })
            {
                string localized = string.Format(Present(template), Present(caption));
                if (string.Equals(error.Message, string.Format(template, caption), StringComparison.Ordinal) ||
                    string.Equals(error.Message, localized, StringComparison.Ordinal)) return localized;
            }
            return null;
        }

        internal PharmacyManagement.Application.InventoryPostRequest BindInventorySubmission(PharmacyManagement.Application.InventoryPostRequest candidate)
        {
            candidate=PharmacyManagement.Application.InventoryPostService.PrepareRequest(candidate);
            if(pendingInventoryRequest!=null&&!pendingInventoryRequest.HasSameContent(candidate))
                throw new InvalidOperationException("نتيجة الحركة السابقة غير مؤكدة؛ استعد بياناتها وأعد المحاولة قبل اعتماد حركة مختلفة.");
            if(pendingInventoryRequest==null)pendingInventoryRequest=candidate;
            return pendingInventoryRequest;
        }
        // Compatibility seam for existing synchronous, SQL-free acceptance checks.
        internal bool RunInventoryWrite(Action write, Action prepareNewDraft, Action refresh)
            => RunInventoryWriteAsync(() => {write();return System.Threading.Tasks.Task.CompletedTask;},prepareNewDraft,refresh).GetAwaiter().GetResult();
        internal System.Threading.Tasks.Task<bool> RunInventoryWriteAsync(Func<System.Threading.Tasks.Task> write, Action prepareNewDraft, Action refresh)
            => RunInventoryWriteWithRefreshAsync(write,prepareNewDraft,()=>{refresh();return System.Threading.Tasks.Task.CompletedTask;});
        internal async System.Threading.Tasks.Task<bool> RunInventoryWriteWithRefreshAsync(Func<System.Threading.Tasks.Task> write, Action prepareNewDraft, Func<System.Threading.Tasks.Task> refresh)
        {
            if (inventorySessionInvalidated || IsDisposed || Disposing) return false;
            if (inventoryWriteBusy || acceptedInventoryDraft)
            {
                if (acceptedInventoryDraft) UiTheme.SetStatus(StatusLabel, Present("هذه الحركة محفوظة بالفعل؛ أغلق النافذة وافتح حركة جديدة."), null);
                return false;
            }
            var commit = Controls.Find("inventoryOperationCommit", true).FirstOrDefault();
            string sourceId = pendingInventoryRequest?.SourceId;
            bool filterEnabled=FilterPanel.Enabled, gridEnabled=Grid.Enabled, commitEnabled=commit==null||commit.Enabled;
            inventoryWriteBusy=true;
            try
            {
                FilterPanel.Enabled=false;
                if (inventorySessionInvalidated || IsDisposed || Disposing) return false;
                Grid.Enabled=false;
                if (inventorySessionInvalidated || IsDisposed || Disposing) return false;
                if(commit!=null)commit.Enabled=false;
                if (inventorySessionInvalidated || IsDisposed || Disposing) return false;
                await write();
                acceptedInventoryDraft=true;
                if (inventorySessionInvalidated || IsDisposed || Disposing) return true;
                try
                {
                    prepareNewDraft();
                    acceptedInventoryDraft=false;
                    pendingInventorySession?.EnsureContext();
                    await refresh();
                    if (inventorySessionInvalidated || IsDisposed || Disposing) return true;
                    inventoryStatusDetails.SetToolTip(StatusLabel, null);
                    StatusLabel.AccessibleDescription = string.Empty;
                    UiTheme.SetStatus(StatusLabel, Present("تم حفظ العملية وتحديث الرصيد."), true);
                }
                catch(Exception ex)
                {
                    if (inventorySessionInvalidated || IsDisposed || Disposing) return true;
                    SetInventoryIssueStatus("تم حفظ حركة المخزون، لكن تعذر تحديث العرض. لا تعِد اعتماد الحركة.", ex, null, sourceId);
                }
                return true;
            }
            finally
            {
                inventoryWriteBusy=false;
                if (!IsDisposed && !Disposing && !FilterPanel.IsDisposed)
                    FilterPanel.Enabled=filterEnabled&&!inventorySessionInvalidated;
                if (!IsDisposed && !Disposing && !Grid.IsDisposed)
                    Grid.Enabled=gridEnabled&&!inventorySessionInvalidated;
                if (!IsDisposed && !Disposing && commit!=null && !commit.IsDisposed)
                    commit.Enabled=commitEnabled&&!inventorySessionInvalidated&&(!inventoryAsyncReads||inventoryReadReady)&&!inventoryRecoveryUnavailable;
            }
        }
        private bool ValidateBeforeSave(string selectedMedicine, int enteredQuantity)
        {
            if (string.IsNullOrWhiteSpace(selectedMedicine))
            {
                UiTheme.SetStatus(StatusLabel, Present("اختر صنفًا قبل اعتماد حركة المخزون."), false);
                return false;
            }

            if (string.IsNullOrWhiteSpace(notes.Text))
            {
                UiTheme.SetStatus(StatusLabel, Present("أدخل سبب الحركة أو مرجعها قبل الاعتماد."), false);
                return false;
            }

            if (!IsLocallyValidQuantity(mode, enteredQuantity))
            {
                UiTheme.SetStatus(StatusLabel,
                    mode == InventoryOperationMode.OpeningStock || mode == InventoryOperationMode.Adjustment
                        ? Present("الرصيد المحدد لا يمكن أن يكون أقل من صفر.")
                        : Present("كمية التوريد أو الصرف يجب أن تكون أكبر من صفر."),
                    false);
                return false;
            }

            if (pendingInventoryRequest == null && mode == InventoryOperationMode.Issue && enteredQuantity > currentValue)
            {
                UiTheme.SetStatus(StatusLabel, Present("الرصيد المتاح لا يكفي للصرف. تحقق من الكمية أو حدّث الرصيد."), false);
                return false;
            }

            return true;
        }

        private bool ConfirmSetBalance(string medicineName, int finalQuantity)
        {
            int difference = finalQuantity - currentValue;
            string operationName = Present(mode == InventoryOperationMode.OpeningStock ? "رصيد أول المدة" : "تسوية المخزون");
            string selectedName = string.IsNullOrWhiteSpace(medicineName)
                ? (leftToRight ? "Not specified" : "غير محدد") : medicineName;
            string message = leftToRight
                ? operationName + " will set the medicine balance to a specific value." + Environment.NewLine + Environment.NewLine +
                    "Medicine: " + selectedName + Environment.NewLine +
                    "Current balance: " + currentValue.ToString("N0") + Environment.NewLine +
                    "Balance after movement: " + finalQuantity.ToString("N0") + Environment.NewLine +
                    "Difference: " + difference.ToString("N0") + Environment.NewLine + Environment.NewLine +
                    "Post this movement?"
                : operationName + " سيضبط رصيد الصنف إلى قيمة محددة." + Environment.NewLine + Environment.NewLine +
                    "الصنف: " + selectedName + Environment.NewLine +
                    "الرصيد الحالي: " + currentValue.ToString("N0") + Environment.NewLine +
                    "الرصيد بعد العملية: " + finalQuantity.ToString("N0") + Environment.NewLine +
                    "الفرق: " + difference.ToString("N0") + Environment.NewLine + Environment.NewLine +
                    "هل تريد اعتماد الحركة؟";

            return AppNotifications.Show(message, operationName, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes;
        }


    }

    internal sealed partial class InventoryAlertsForm : InventoryDataForm
    {
        private readonly AntdUI.Input lowStockThreshold = DesktopComponents.Input("inventoryAlertsLowStock", "حد المخزون المنخفض", "10");
        private readonly AntdUI.Input expiringDays = DesktopComponents.Input("inventoryAlertsExpiringDays", "الأيام قبل الانتهاء", "30");
        private readonly bool loadData;

        public InventoryAlertsForm()
            : this(true)
        {
        }

        internal InventoryAlertsForm(bool loadData)
            : base("تنبيهات المخزون", loadData)
        {
            this.loadData = loadData;
            InitializeComponent();
            lowStockThreshold.TextChanged += (s, e) => InvalidateAlertResults();
            expiringDays.TextChanged += (s, e) => InvalidateAlertResults();
            FormClosed += (s, e) => { CancelAlertRead(); CancelAlertPreferenceOperation(); };
            Disposed += (s, e) => { CancelAlertRead(); CancelAlertPreferenceOperation(); };
            UiSessionSubscription.Attach(this, () =>
            {
                if (!loadData) return;
                InvalidateAlertPreferenceContext();
                CancelAlertRead();
                alertReadSession = null;
                ClearAlertResults();
                UiTheme.SetStatus(StatusLabel, Present("تغيرت جلسة المستخدم. أعد تحميل القائمة."), false);
            });
            ApplyPresentation(UiDirection.IsLeftToRight);
            SetEmptyGridHint(UiDirection.IsLeftToRight
                ? "Set the stock and expiry thresholds, then refresh. Zero expiry days restricts near-expiry alerts to today."
                : "حدد حدود المخزون والصلاحية، ثم اضغط تحديث. صفر يوم يقصر تنبيه قرب الانتهاء على اليوم الحالي.");
        }

        internal void ApplyPresentation(bool useLeftToRight)
        {
            ApplyInventoryPresentation(useLeftToRight);
            lowStockThreshold.PlaceholderText = Present("حد المخزون المنخفض");
            lowStockThreshold.AccessibleName = lowStockThreshold.PlaceholderText;
            expiringDays.PlaceholderText = Present("الأيام قبل الانتهاء");
            expiringDays.AccessibleName = expiringDays.PlaceholderText;
            foreach (AntdUI.Input threshold in new[] { lowStockThreshold, expiringDays })
            {
                threshold.RightToLeft = RightToLeft.No;
                threshold.TextAlign = HorizontalAlignment.Left;
            }
            SetEmptyGridHint(useLeftToRight
                ? "Set the stock and expiry thresholds, then refresh. Zero expiry days restricts near-expiry alerts to today."
                : "حدد حدود المخزون والصلاحية، ثم اضغط تحديث. صفر يوم يقصر تنبيه قرب الانتهاء على اليوم الحالي.");
        }

        private async void RefreshData()
        {
            try { if (await EnsureAlertPreferencesLoadedAsync()) await RefreshAlertsAsync(); }
            catch (Exception error) { UiTheme.SetStatus(StatusLabel, Present("تعذر تحميل التنبيهات: ") + error.Message, false); }
        }

        private int CountByType(DataTable table, string type)
        {
            int count = 0;
            foreach (DataRow row in table.Rows)
            {
                if (Convert.ToString(row["نوع التنبيه"]) == type)
                {
                    count++;
                }
            }
            return count;
        }

        private void HighlightRows()
        {
            foreach (DataGridViewRow row in Grid.Rows)
            {
                string type = Convert.ToString(row.Cells["نوع التنبيه"].Value);
                if (type == "منتهي")
                {
                    ApplyAlertRowStyle(row, UiTheme.Danger);
                }
                else if (type == "قريب الانتهاء")
                {
                    ApplyAlertRowStyle(row, UiTheme.Warning);
                }
                else if (type == "مخزون منخفض")
                {
                    ApplyAlertRowStyle(row, UiTheme.Secondary);
                }
            }
        }

        private static void ApplyAlertRowStyle(DataGridViewRow row, Color accent)
        {
            Color surface = UiTheme.Surface;
            bool darkSurface = surface.GetBrightness() < 0.5F;
            int accentWeight = darkSurface ? 62 : 28;
            int surfaceWeight = 100 - accentWeight;
            Color background = Color.FromArgb(
                (surface.R * surfaceWeight + accent.R * accentWeight) / 100,
                (surface.G * surfaceWeight + accent.G * accentWeight) / 100,
                (surface.B * surfaceWeight + accent.B * accentWeight) / 100);

            row.DefaultCellStyle.BackColor = background;
            row.DefaultCellStyle.ForeColor = UiTheme.Text;
            row.DefaultCellStyle.SelectionBackColor = UiTheme.Selection;
            row.DefaultCellStyle.SelectionForeColor = UiTheme.Text;
        }
    }

    internal sealed partial class InventoryTurnoverForm : InventoryDataForm
    {
        private readonly AntdUI.DatePicker fromDate = DesktopComponents.DatePicker("inventoryTurnoverFromDate", "من تاريخ", DateTime.Today.AddMonths(-3));
        private readonly AntdUI.DatePicker toDate = DesktopComponents.DatePicker("inventoryTurnoverToDate", "إلى تاريخ", DateTime.Today);
        private readonly AntdUI.Input idleDays = DesktopComponents.Input("inventoryTurnoverIdleDays", "أيام الركود", "60");
        private readonly bool loadData;

        public InventoryTurnoverForm()
            : this(true)
        {
        }

        internal InventoryTurnoverForm(bool loadData)
            : base("معدل الدوران والركود للأصناف", loadData)
        {
            this.loadData = loadData;
            InitializeComponent();
            fromDate.TextChanged += (s, e) => InvalidateTurnoverResults();
            toDate.TextChanged += (s, e) => InvalidateTurnoverResults();
            idleDays.TextChanged += (s, e) => InvalidateTurnoverResults();
            FormClosed += (s, e) => CancelTurnoverRead();
            Disposed += (s, e) => CancelTurnoverRead();
            UiSessionSubscription.Attach(this, () =>
            {
                if (!loadData) return;
                CancelTurnoverRead();
                turnoverReadSession = null;
                ClearTurnoverResults();
                UiTheme.SetStatus(StatusLabel, Present("تغيرت جلسة المستخدم. أعد تحميل القائمة."), false);
            });
            ApplyPresentation(UiDirection.IsLeftToRight);
        }

        internal void ApplyPresentation(bool useLeftToRight)
        {
            ApplyInventoryPresentation(useLeftToRight);
            fromDate.AccessibleName = Present("من تاريخ");
            toDate.AccessibleName = Present("إلى تاريخ");
            idleDays.PlaceholderText = Present("أيام الركود");
            idleDays.AccessibleName = idleDays.PlaceholderText;
            idleDays.RightToLeft = RightToLeft.No;
            idleDays.TextAlign = HorizontalAlignment.Left;
            SetEmptyGridHint(Present("حدد الفترة وحدّ أيام الركود، ثم اضغط تحديث لعرض التقرير."));
        }

        private async void RefreshData()
        {
            try { await RefreshTurnoverAsync(); }
            catch (Exception error) { UiTheme.SetStatus(StatusLabel, Present("تعذر تحميل معدل الدوران: ") + error.Message, false); }
        }

        private int CountClassification(DataTable table, string classification)
        {
            int count = 0;
            foreach (DataRow row in table.Rows)
            {
                if (Convert.ToString(row["التصنيف"]) == classification)
                {
                    count++;
                }
            }
            return count;
        }
    }
}




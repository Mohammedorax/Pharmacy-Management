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
    public partial class AccountingHomeForm : Form
    {
        private int activePage = 1;
        private int renderedTileColumns;
        private bool leftToRight = UiDirection.IsLeftToRight;

        public AccountingHomeForm()
        {
            InitializeComponent();
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            UpdateStyles();
            Text = "المحاسبة";
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = true;
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(1200, 760);
            RightToLeft = leftToRight ? RightToLeft.No : RightToLeft.Yes;
            RightToLeftLayout = !leftToRight;
            KeyPreview = true;
            UiTheme.Apply(this);
            ApplyAccountingVisualTheme();
            FitToWorkingArea();

            closeButton.Click += (s, e) => Close();
            tabs.Resize += (s, e) => AlignTabsRight();
            tileGrid.Resize += (s, e) =>
            {
                if (GetTileColumnCount() != renderedTileColumns && !tileGrid.IsDisposed)
                {
                    ReflowTiles();
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
            ApplyPresentation(leftToRight);
        }

        private void FitToWorkingArea()
        {
            Rectangle workingArea = Screen.FromControl(this).WorkingArea;
            MaximumSize = workingArea.Size;
            MinimumSize = new Size(Math.Min(760, workingArea.Width), Math.Min(560, workingArea.Height));
            Size = new Size(Math.Min(Math.Max(Width, MinimumSize.Width), workingArea.Width), Math.Min(Math.Max(Height, MinimumSize.Height), workingArea.Height));
        }

        private void ApplyAccountingVisualTheme()
        {
            BackColor = UiTheme.Background;
            header.BackColor = UiTheme.PrimaryDark;
            tabs.BackColor = UiTheme.Background;
            tileGrid.BackColor = UiTheme.Background;
            actionFooter.BackColor = UiTheme.Surface;
            DesktopComponents.StyleOutlinedFooterAction(closeButton);
        }

        private void BuildTabs()
        {
            tabs.SuspendLayout();
            try
            {
                if (tabs.Controls.Count != 3)
                {
                    Control[] previousTabs = tabs.Controls.Cast<Control>().ToArray();
                    tabs.Controls.Clear();
                    foreach (Control previousTab in previousTabs) previousTab.Dispose();
                    for (int page = 1; page <= 3; page++)
                    {
                        int targetPage = page;
                        var button = new AccessibleAntdButton
                        {
                            Name = "accountingPageTab" + page,
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
                for (int page = 1; page <= 3; page++)
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
            button.Text = Present(GetPageTitle(page));
            button.AccessibleName = button.Text;
            button.RightToLeft = leftToRight ? RightToLeft.No : RightToLeft.Yes;
            button.DefaultBack = selected ? UiTheme.SurfaceVariant : UiTheme.Surface;
            button.BackColor = selected ? UiTheme.SurfaceVariant : UiTheme.Surface;
            button.ForeColor = selected ? UiTheme.Text : UiTheme.MutedText;
            button.DefaultBorderColor = selected ? UiTheme.Primary : UiTheme.Border;
            button.AccessibleDescription = (selected
                ? (leftToRight ? "Current tab. " : "التبويب الحالي. ")
                : string.Empty) + (page == 1
                ? (leftToRight ? "Accounts and cash vouchers." : "دليل الحسابات وسندات القبض والصرف.")
                : page == 2
                    ? (leftToRight ? "Journal entries and tax reports." : "القيود اليومية والافتتاحية والتقارير الضريبية.")
                    : (leftToRight ? "Financial reports and analysis." : "التقارير والتحليل المالي."));
        }

        private static string GetPageTitle(int page)
        {
            if (page == 1) return "السندات والحسابات";
            if (page == 2) return "القيود والضريبة";
            return "التقارير والتحليل";
        }

        private void AlignTabsRight()
        {
            if (tabs == null) return;
            tabs.FlowDirection = leftToRight ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;
            tabs.RightToLeft = RightToLeft.No;
            tabs.Padding = new Padding(28, 10, 28, 0);
        }

        private string Present(string text) => leftToRight ? UiLanguageCatalog.Translate(text) : text;

        internal void ApplyPresentation(bool useLeftToRight)
        {
            leftToRight = useLeftToRight;
            UiDirection.Apply(this, leftToRight);
            Text = Present("المحاسبة");
            titleLabel.Text = Present("نظام الحسابات");
            titleLabel.TextAlign = leftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
            actionFooter.RightToLeft = RightToLeft.No;
            actionFooter.FlowDirection = leftToRight ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;
            closeButton.Text = Present("إغلاق");
            closeButton.AccessibleName = leftToRight ? "Close accounting center" : "إغلاق مركز الحسابات";
            closeButton.IconPosition = leftToRight ? AntdUI.TAlignMini.Left : AntdUI.TAlignMini.Right;
            BuildTabs();
            tileGrid.RightToLeft = leftToRight ? RightToLeft.No : RightToLeft.Yes;
            foreach (AntdUI.Button button in tileGrid.Controls.OfType<AntdUI.Button>())
                if (button.Tag is AccountingTile tile) ApplyTilePresentation(button, tile);
            UiTheme.ApplyStatusStripPresentation(this, leftToRight);
            PerformLayout();
        }

        private void RenderPage()
        {
            tileGrid.SuspendLayout();
            try
            {
                Control[] previousTiles = tileGrid.Controls.Cast<Control>().ToArray();
                tileGrid.Controls.Clear();
                foreach (Control previousTile in previousTiles) previousTile.Dispose();
                AccountingTile[] tiles = GetPageTiles(activePage);
                int columns = GetTileColumnCount();
                renderedTileColumns = columns;
                tileGrid.ColumnCount = columns;
                tileGrid.ColumnStyles.Clear();
                for (int column = 0; column < columns; column++)
                    tileGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / columns));
                int rows = Math.Max(1, (int)Math.Ceiling(tiles.Length / (double)columns));
                tileGrid.RowCount = rows;
                tileGrid.RowStyles.Clear();
                for (int row = 0; row < rows; row++)
                    tileGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100F / rows));
                for (int i = 0; i < tiles.Length; i++)
                    tileGrid.Controls.Add(CreateTile(tiles[i]), i % columns, i / columns);
            }
            finally
            {
                tileGrid.ResumeLayout(true);
            }
        }

        private void ReflowTiles()
        {
            int columns=GetTileColumnCount();
            Control[] tiles=tileGrid.Controls.Cast<Control>().ToArray();
            int rows=Math.Max(1,(int)Math.Ceiling(tiles.Length/(double)columns));
            renderedTileColumns=columns;
            tileGrid.SuspendLayout();
            try
            {
                tileGrid.RowCount=Math.Max(tileGrid.RowCount,rows);
                tileGrid.ColumnCount=columns;
                tileGrid.ColumnStyles.Clear();
                for(int column=0;column<columns;column++)tileGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100F/columns));
                for(int index=0;index<tiles.Length;index++)tileGrid.SetCellPosition(tiles[index],new TableLayoutPanelCellPosition(index%columns,index/columns));
                tileGrid.RowCount=rows;
                tileGrid.RowStyles.Clear();
                for(int row=0;row<rows;row++)tileGrid.RowStyles.Add(new RowStyle(SizeType.Percent,100F/rows));
            }
            finally{tileGrid.ResumeLayout(true);}
        }
        private int GetTileColumnCount()
        {
            int availableWidth = Math.Max(0, tileGrid.ClientSize.Width - tileGrid.Padding.Horizontal);
            return availableWidth >= 960 ? 3 : availableWidth >= 620 ? 2 : 1;
        }

        private AccountingTile[] GetPageTiles(int page)
        {
            if (page == 1)
            {
                return new[]
                {
                    new AccountingTile("شجرة الحسابات", AppIcon.Database, AppOperation.ManageChartOfAccounts, () => ShowChild(new ChartOfAccountsForm())),
                    new AccountingTile("كشف حساب عام", AppIcon.Report, AppOperation.ViewAccountingReports, () => ShowChild(new GeneralLedgerForm())),
                    new AccountingTile("سند قبض خارجي", AppIcon.Add, AppOperation.PostCashVoucher, () => ShowChild(new ReceiptVoucherForm("ReceiptExternal"))),
                    new AccountingTile("سند صرف خارجي", AppIcon.Sale, AppOperation.PostCashVoucher, () => ShowChild(new ReceiptVoucherForm("PaymentExternal"))),
                    new AccountingTile("سند قبض داخلي", AppIcon.Add, AppOperation.PostCashVoucher, () => ShowChild(new ReceiptVoucherForm("ReceiptInternal"))),
                    new AccountingTile("سند صرف داخلي", AppIcon.Sale, AppOperation.PostCashVoucher, () => ShowChild(new ReceiptVoucherForm("PaymentInternal")))
                };
            }

            if (page == 2)
            {
                return new[]
                {
                    new AccountingTile("القيود اليومية", AppIcon.Report, AppOperation.PostAccountingEntry, () => ShowChild(new JournalEntriesForm())),
                    new AccountingTile("الإقرار الضريبي", AppIcon.Currency, AppOperation.ViewAccountingReports, () => ShowChild(new AccountingReportsForm("Tax"))),
                    new AccountingTile("ميزان المراجعة", AppIcon.Report, AppOperation.ViewAccountingReports, () => ShowChild(new AccountingReportsForm("TrialBalance"))),
                    new AccountingTile("القيود الافتتاحية", AppIcon.Database, AppOperation.PostAccountingEntry, () => ShowChild(new OpeningEntriesForm())),
                };
            }

            return new[]
            {
                new AccountingTile("تقارير نظام الحسابات", AppIcon.Report, AppOperation.ViewAccountingReports, () => ShowChild(new AccountingReportsForm("TrialBalance"))),
                new AccountingTile("منشئ تقارير تشغيلي", AppIcon.Report, AppOperation.ViewAccountingReports, () => ShowChild(new OperationalReportBuilderForm())),
                new AccountingTile("تقرير تحليل مراكز التكلفة", AppIcon.Currency, AppOperation.ViewAccountingReports, () => ShowChild(new AccountingReportsForm("CostCenters"))),
                new AccountingTile("تقرير اليومية العامة", AppIcon.Report, AppOperation.ViewAccountingReports, () => ShowChild(new AccountingReportsForm("GeneralJournal"))),
                new AccountingTile("جاهزية إقفال الفترة", AppIcon.Compliance, AppOperation.ViewAccountingReports, () => ShowChild(new PeriodCloseReadinessForm())),
                new AccountingTile("إدارة الفترات المحاسبية", AppIcon.Settings, AppOperation.ManageAccountingPeriods, () => ShowChild(new AccountingPeriodManagementForm())),
                new AccountingTile("فحص سلامة القيود", AppIcon.Compliance, AppOperation.ViewAccountingReports, () => ShowChild(new AccountingIntegrityForm())),
                new AccountingTile("فحص مطابقة المخزون", AppIcon.Database, AppOperation.ViewAccountingReports, () => ShowChild(new InventoryIntegrityForm()))
            };
        }

        private Control CreateTile(AccountingTile tile)
        {
            bool allowed = PermissionService.CanExecute(tile.Operation);
            AntdUI.Button button = new AccessibleAntdButton
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(18, 14, 18, 14),
                Type = AntdUI.TTypeMini.Default,
                DefaultBack = allowed ? UiTheme.Surface : UiTheme.SurfaceVariant,
                BackColor = allowed ? UiTheme.Surface : UiTheme.SurfaceVariant,
                ForeColor = allowed ? UiTheme.Text : UiTheme.MutedText,
                Cursor = allowed ? Cursors.Hand : Cursors.No,
                RightToLeft = leftToRight ? RightToLeft.No : RightToLeft.Yes,
                Text = Present(tile.Title) + (allowed
                    ? string.Empty : Environment.NewLine + Present("صلاحية مطلوبة")),
                Font = AppFonts.Create(12F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                IconSvg = OutlinedIcons.Svg(tile.Icon, !leftToRight),
                IconPosition = AntdUI.TAlignMini.Top,
                IconSize = new Size(40, 40),
                IconGap = 0.45F,
                BorderWidth = 1,
                DefaultBorderColor = UiTheme.Border,
                BackHover = null,
                ForeHover = allowed ? UiTheme.Text : UiTheme.MutedText,
                AccessibleName = Present(tile.Title),
                AccessibleRole = AccessibleRole.PushButton,
                TabStop = true,
                Tag = tile,
                AccessibleDescription = allowed
                    ? (leftToRight ? "Open " : "فتح ") + Present(tile.Title)
                    : (leftToRight ? "Permission required: " : "لا تملك صلاحية ") +
                        Present(PermissionService.GetArabicName(tile.Operation))
            };
            button.Click += (s, e) => OpenTile(tile);
            button.SizeChanged += (s, e) => FitTileContent(button);
            FitTileContent(button);
            return button;
        }

        private void ApplyTilePresentation(AntdUI.Button button, AccountingTile tile)
        {
            bool allowed = PermissionService.CanExecute(tile.Operation);
            button.RightToLeft = leftToRight ? RightToLeft.No : RightToLeft.Yes;
            button.Text = Present(tile.Title) + (allowed ? string.Empty : Environment.NewLine + Present("صلاحية مطلوبة"));
            button.AccessibleName = Present(tile.Title);
            button.AccessibleDescription = allowed
                ? (leftToRight ? "Open " : "فتح ") + Present(tile.Title)
                : (leftToRight ? "Permission required: " : "لا تملك صلاحية ") + Present(PermissionService.GetArabicName(tile.Operation));
            button.IconSvg = OutlinedIcons.Svg(tile.Icon, !leftToRight);
            FitTileContent(button);
        }

        private void FitTileContent(AntdUI.Button button)
        {
            if (button == null || button.IsDisposed || button.ClientSize.Width <= 0 || button.ClientSize.Height <= 0)
                return;
            bool compact = button.ClientSize.Height < 112;
            float targetFontSize = compact ? 10F : 12F;
            if (Math.Abs(button.Font.SizeInPoints - targetFontSize) > 0.1F)
                button.Font = AppFonts.Create(targetFontSize, FontStyle.Bold);
            button.IconPosition = compact
                ? (leftToRight ? AntdUI.TAlignMini.Left : AntdUI.TAlignMini.Right)
                : AntdUI.TAlignMini.Top;
            int iconSize = compact ? 24 : Math.Max(32, Math.Min(42, Math.Min(button.ClientSize.Width, button.ClientSize.Height) / 3));
            button.IconSize = new Size(iconSize, iconSize);
            button.Radius = DesktopDesignTokens.CardRadius;
        }

        private void OpenTile(AccountingTile tile)
        {
            if (!PermissionService.EnsureAllowed(tile.Operation, this))
            {
                return;
            }

            tile.Open();
        }

        private void ShowChild(Form form)
        {
            using (form)
            {
                form.ShowDialog(this);
            }
        }

        private sealed class AccountingTile
        {
            public AccountingTile(string title, AppIcon icon, AppOperation operation, Action open)
            {
                Title = title;
                Icon = icon;
                Operation = operation;
                Open = open;
            }

            public string Title { get; private set; }
            public AppIcon Icon { get; private set; }
            public AppOperation Operation { get; private set; }
            public Action Open { get; private set; }
        }
    }

    public partial class ChartOfAccountsForm : Form
    {
        private readonly bool presentationLeftToRight;
        private readonly TreeView accountsTree = new TreeView();
        private readonly AntdUI.Input txtCode = DesktopComponents.Input("chartAccountCode", "رقم الحساب");
        private readonly TextBox txtName = new TextBox { Name = "chartAccountName", AccessibleName = "اسم الحساب" };
        private readonly ComboBox parentAccount = new ComboBox { Name = "chartParentAccount", AccessibleName = "الحساب الأب", DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly AntdUI.Select accountNature = DesktopComponents.Select("chartAccountNature", "طبيعة الحساب");
        private readonly RadioButton mainAccount = new RadioButton { Text = "رئيسي", Checked = true };
        private readonly RadioButton childAccount = new RadioButton { Text = "فرعي" };
        private readonly CheckBox chkActive = new CheckBox { Text = "نشط", Checked = true };
        private readonly Label statusLabel = new Label();
        private DataTable accountsTable;
        private readonly bool previewMode;
        private bool chartWriteBusy,chartDataReady,chartStatusIssue,chartStatusSizing;
        private int chartReadGeneration,chartHierarchyIssues;
        private string acceptedAccountCode,acceptedAccountMessage;
        private readonly Func<Func<bool>> chartContextFactory;
        private Func<bool> chartContextCurrent,chartEditorCurrent;
        private System.Threading.CancellationTokenSource chartReadCancellation;
        private bool chartInvalidating;
        internal Func<bool> ChartDeleteConfirmation;

        private Func<bool> CaptureChartContext()
        {
            if(chartContextFactory!=null)return chartContextFactory();
            if(previewMode)return ()=>true;
            var session=ApplicationServices.CreateAccountingReadSession();return ()=>session.IsLookupCurrent;
        }
        private bool IsChartContextCurrent(Func<bool> context)
            =>context!=null?context():previewMode&&chartContextFactory==null;
        private void CancelChartRead()
        {var cancellation=chartReadCancellation;if(cancellation!=null&&!cancellation.IsCancellationRequested)cancellation.Cancel();}
        private void InvalidateChartContext()
        {
            if(chartInvalidating||IsDisposed)return;
            chartInvalidating=true;
            try
            {
                ++chartReadGeneration;CancelChartRead();chartDataReady=false;
                chartContextCurrent=chartEditorCurrent=null;acceptedAccountCode=acceptedAccountMessage=null;
                accountsTree.Nodes.Clear();parentAccount.Items.Clear();parentAccount.Text=string.Empty;parentAccount.SelectedIndex=-1;txtCode.Clear();txtName.Clear();
                chkActive.Checked=true;mainAccount.Checked=true;
                if(accountNature.Items.Count>0)accountNature.SelectedIndex=0;
                var previous=accountsTable;accountsTable=null;previous?.Dispose();
                SetChartStatus(ChartText("تغيرت جلسة الحسابات أو صلاحيتها. حدّث الشجرة قبل المتابعة.","The accounts session or permission changed. Refresh the tree before continuing."),true);
                SetChartBusy(chartWriteBusy);
            }
            finally{chartInvalidating=false;}
        }
        private bool EnsureChartContext(bool editor=false)
        {
            if(chartInvalidating||IsDisposed)return false;
            if(chartDataReady&&IsChartContextCurrent(chartContextCurrent)&&(!editor||IsChartContextCurrent(chartEditorCurrent)))return true;
            InvalidateChartContext();return false;
        }

        public ChartOfAccountsForm()
            : this(false)
        {
        }

        internal ChartOfAccountsForm(bool previewMode)
            : this(previewMode, UiDirection.IsLeftToRight)
        {
        }

        internal ChartOfAccountsForm(bool previewMode, bool presentationLeftToRight, Func<Func<bool>> contextFactory = null)
        {
            this.previewMode = previewMode;
            this.presentationLeftToRight = presentationLeftToRight;
            chartContextFactory = contextFactory;
            Text = ChartText("شجرة الحسابات", "Chart of accounts");
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = true;
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(1100, 680);
            MinimumSize = new Size(760, 560);
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            KeyPreview = true;
            InitializeComponent();
            statusLabel.UseMnemonic=false;
            statusLabel.TextChanged += (s,e)=>LayoutChartStatus();
            statusLabel.SizeChanged += (s,e)=>LayoutChartStatus();
            statusLabel.FontChanged += (s,e)=>LayoutChartStatus();
            UiTheme.Apply(this);
            UiDirection.Apply(this, presentationLeftToRight);
            txtCode.AccessibleName = ChartFieldLabel("رقم الحساب");
            txtName.AccessibleName = ChartFieldLabel("اسم الحساب");
            txtName.TextChanged += (s, e) => UiDirection.ApplyContentDirection(txtName, presentationLeftToRight);
            parentAccount.TextChanged += (s, e) => UiDirection.ApplyContentDirection(parentAccount, presentationLeftToRight);
            parentAccount.AccessibleName = ChartFieldLabel("الحساب الأب");
            accountNature.AccessibleName = ChartFieldLabel("طبيعة الحساب");
            mainAccount.Text = ChartText("رئيسي", "Main");
            childAccount.Text = ChartText("فرعي", "Subaccount");
            chkActive.Text = ChartText("نشط", "Active");
            foreach (Control choice in new Control[] { mainAccount, childAccount, chkActive })
            {
                choice.Size = new Size(choice == childAccount ? 130 : 100, 44);
                choice.Margin = new Padding(3, 0, 3, 0);
                choice.AccessibleName = choice.Text;
            }
            AutoScroll = false;
            ApplyChartVisualTheme();
            FitToWorkingArea();
            if(previewMode){PopulatePreviewData();chartDataReady=true;}
            else Shown += (s,e)=>LoadInitialDataAsync();
            SetChartBusy(false);
            FormClosing += (s,e)=>{if(chartWriteBusy && e.CloseReason==CloseReason.UserClosing){e.Cancel=true;SetChartStatus(ChartText("انتظر اكتمال عملية الحساب قبل الإغلاق.","Wait for the account operation before closing."));}};
            FormClosing += (s,e)=>{if(!e.Cancel){++chartReadGeneration;CancelChartRead();}};
            Disposed += (s,e)=>{++chartReadGeneration;CancelChartRead();};
            Resize += (s, e) => ApplyResponsiveLayout();
            Activated += (s, e) => { ApplyChartVisualTheme(); if(chartDataReady&&!IsChartContextCurrent(chartContextCurrent))InvalidateChartContext(); };
            UiSessionSubscription.Attach(this,()=>{if((chartContextCurrent!=null&&!IsChartContextCurrent(chartContextCurrent))||(chartEditorCurrent!=null&&!IsChartContextCurrent(chartEditorCurrent)))InvalidateChartContext();});
            Load += (s, e) => FitToWorkingArea();
            Load += (s, e) => ApplyResponsiveLayout();
            KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); };
        }

        private void PopulatePreviewData()
        {
            statusLabel.Text = ChartText("بيانات توضيحية للمعاينة فقط — لا اتصال بقاعدة البيانات.", "Preview data only; no database connection.");
            parentAccount.Items.Add(ChartText("بدون", "None"));
            parentAccount.Items.Add("1000 - الأصول");
            parentAccount.Items.Add("2000 - الخصوم");
            parentAccount.SelectedIndex = 0;
            TreeNode assets = accountsTree.Nodes.Add("1000  الأصول");
            assets.Nodes.Add("1100  النقدية والبنوك");
            assets.Nodes.Add("1200  المخزون");
            TreeNode liabilities = accountsTree.Nodes.Add("2000  الخصوم");
            liabilities.Nodes.Add("2100  الموردون");
            accountsTree.ExpandAll();
        }

        internal void ApplyResponsiveLayout()
        {
            Panel treeHost = Controls.Find("chartOfAccountsTreeHost", true).OfType<Panel>().FirstOrDefault();
            Panel editorHost = Controls.Find("chartOfAccountsEditorHost", true).OfType<Panel>().FirstOrDefault();
            Panel toolbar = Controls.Find("chartOfAccountsToolbar", true).OfType<Panel>().FirstOrDefault();
            Panel contentHost = Controls.Find("chartOfAccountsContentHost", true).OfType<Panel>().FirstOrDefault();
            AntdUI.Panel editorCard = Controls.Find("chartOfAccountsEditorCard", true).OfType<AntdUI.Panel>().FirstOrDefault();
            TableLayoutPanel fields = Controls.Find("chartOfAccountsFields", true).OfType<TableLayoutPanel>().FirstOrDefault();
            if (treeHost == null || editorHost == null || toolbar == null || contentHost == null || editorCard == null || fields == null) return;

            bool compact = ClientSize.Width < 980 || contentHost.ClientSize.Height < 500;
            treeHost.SuspendLayout();
            editorHost.SuspendLayout();
            toolbar.SuspendLayout();
            if (compact)
            {
                contentHost.AutoScroll = true;
                int contentWidth = Math.Max(1, contentHost.ClientSize.Width - SystemInformation.VerticalScrollBarWidth);
                treeHost.Dock = DockStyle.None;
                treeHost.Location = Point.Empty;
                treeHost.Size = new Size(contentWidth, 260);
                editorHost.Dock = DockStyle.None;
                editorHost.Location = new Point(0, treeHost.Bottom);
                editorHost.Size = new Size(contentWidth, 480);
                editorHost.Padding = new Padding(10, 10, 10, 8);
                editorCard.Padding = new Padding(22);
                fields.RowStyles[0].Height = 48;
                fields.RowStyles[1].Height = 80;
                fields.RowStyles[2].Height = 80;
                fields.RowStyles[3].SizeType = SizeType.Absolute;
                fields.RowStyles[3].Height = 132;
                editorCard.Height = 392;
                contentHost.AutoScrollMinSize = new Size(0, editorHost.Bottom);
            }
            else
            {
                contentHost.AutoScroll = false;
                contentHost.AutoScrollMinSize = Size.Empty;
                treeHost.Dock = presentationLeftToRight ? DockStyle.Left : DockStyle.Right;
                treeHost.Width = Math.Min(560, Math.Max(440, ClientSize.Width / 2));
                treeHost.Height = contentHost.ClientSize.Height;
                editorHost.Dock = DockStyle.Fill;
                editorHost.Padding = new Padding(18, 14, 12, 14);
                editorCard.Padding = new Padding(26);
                fields.RowStyles[0].Height = 48;
                fields.RowStyles[1].Height = 80;
                fields.RowStyles[2].Height = 80;
                fields.RowStyles[3].SizeType = SizeType.Absolute;
                fields.RowStyles[3].Height = 132;
                editorCard.Height = 400;
                treeHost.Size = new Size(treeHost.Width, contentHost.ClientSize.Height);
                editorHost.Size = contentHost.ClientSize;
            }
            FlowLayoutPanel rightActions = toolbar.Controls.OfType<FlowLayoutPanel>().FirstOrDefault(x => x.Dock == DockStyle.Right);
            FlowLayoutPanel leftActions = toolbar.Controls.OfType<FlowLayoutPanel>().FirstOrDefault(x => x.Dock == DockStyle.Left);
            if (rightActions != null) rightActions.Width = compact ? 455 : 560;
            if (leftActions != null) leftActions.Width = compact ? 160 : 170;
            toolbar.ResumeLayout(true);
            contentHost.ResumeLayout(true);
            editorHost.ResumeLayout(true);
            treeHost.ResumeLayout(true);
            PerformLayout();
        }

        private void ApplyChartVisualTheme()
        {
            Panel content = Controls.Find("chartOfAccountsContentHost", true).OfType<Panel>().FirstOrDefault();
            Panel treeHost = Controls.Find("chartOfAccountsTreeHost", true).OfType<Panel>().FirstOrDefault();
            Panel editorHost = Controls.Find("chartOfAccountsEditorHost", true).OfType<Panel>().FirstOrDefault();
            AntdUI.Panel editorCard = Controls.Find("chartOfAccountsEditorCard", true).OfType<AntdUI.Panel>().FirstOrDefault();
            Panel toolbar = Controls.Find("chartOfAccountsToolbar", true).OfType<Panel>().FirstOrDefault();
            FlowLayoutPanel treeTools = Controls.Find("chartOfAccountsTreeTools", true).OfType<FlowLayoutPanel>().FirstOrDefault();
            FlowLayoutPanel typeOptions = Controls.Find("chartAccountTypeOptions", true).OfType<FlowLayoutPanel>().FirstOrDefault();
            if (content != null) content.BackColor = UiTheme.Background;
            if (treeHost != null) treeHost.BackColor = UiTheme.Background;
            if (editorHost != null) editorHost.BackColor = UiTheme.Background;
            if (editorCard != null) AntdThemeAdapter.Apply(editorCard);
            AntdUI.Panel treeCard = Controls.Find("chartOfAccountsTreeCard", true).OfType<AntdUI.Panel>().FirstOrDefault();
            if (treeCard != null) AntdThemeAdapter.Apply(treeCard);
            LayoutChartHeader();
            accountsTree.BackColor = UiTheme.Surface;
            accountsTree.ForeColor = UiTheme.Text;
            accountsTree.RightToLeft = presentationLeftToRight ? RightToLeft.No : RightToLeft.Yes;
            accountsTree.RightToLeftLayout = !presentationLeftToRight;
            statusLabel.BackColor = UiTheme.Background;
            statusLabel.ForeColor = chartStatusIssue ? UiTheme.Danger : UiTheme.MutedText;
            if (toolbar != null)
            {
                toolbar.BackColor = UiTheme.Background;
                foreach (FlowLayoutPanel actions in toolbar.Controls.OfType<FlowLayoutPanel>())
                {
                    actions.BackColor = UiTheme.Background;
                    actions.RightToLeft = RightToLeft.No;
                    actions.FlowDirection = presentationLeftToRight ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;
                    foreach (AntdUI.Button button in actions.Controls.OfType<AntdUI.Button>())
                    {
                        switch (button.Name)
                        {
                            case "chartNewAccount": button.Text = ChartText("جديد", "New"); break;
                            case "chartSaveAccount": button.Text = ChartText("حفظ التعديلات", "Save changes"); break;
                            case "chartDeleteAccount": button.Text = ChartText("حذف", "Delete"); break;
                            case "chartRefreshAccount": button.Text = ChartText("تحديث", "Refresh"); break;
                            case "chartExit": button.Text = ChartText("خروج", "Close"); break;
                        }
                        button.AccessibleName = button.Text;
                DesktopComponents.StyleOutlinedFooterAction(button, button.Name == "chartDeleteAccount");
                        button.IconPosition = presentationLeftToRight ? AntdUI.TAlignMini.Left : AntdUI.TAlignMini.Right;
                    }
                }
            }
            if (typeOptions != null) typeOptions.BackColor = UiTheme.Surface;
            if (treeTools != null)
            {
                treeTools.BackColor = UiTheme.Surface;
                treeTools.RightToLeft = RightToLeft.No;
                treeTools.FlowDirection = presentationLeftToRight ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;
                foreach (AntdUI.Button button in treeTools.Controls.OfType<AntdUI.Button>())
                {
                    if (presentationLeftToRight)
                    {
                        switch (button.Text)
                        {
                            case "أعلى": button.Text = "Up"; break;
                            case "أسفل": button.Text = "Down"; break;
                            case "طي": button.Text = "Collapse"; button.Width = 90; break;
                            case "فتح": button.Text = "Expand"; button.Width = 90; break;
                        }
                        button.AccessibleName = button.Text;
                    }
                    DesktopComponents.StyleOutlinedFooterAction(button);
                }
            }
            var nameFrame = txtName.Parent as AntdUI.Panel;
            if (nameFrame != null) DesktopComponents.StyleNativeTextFrame(nameFrame, txtName);
            UiDirection.ApplyContentDirection(txtName, presentationLeftToRight);
            var parentFrame = Controls.Find("chartParentAccountFrame", true).OfType<AntdUI.Panel>().FirstOrDefault();
            if (parentFrame != null) DesktopComponents.StyleNativeSelectFrame(parentFrame, parentAccount, presentationLeftToRight);
        }

        private void FitToWorkingArea()
        {
            Rectangle workingArea = Screen.FromControl(this).WorkingArea;
            MaximumSize = workingArea.Size;
            MinimumSize = new Size(Math.Min(760, workingArea.Width), Math.Min(560, workingArea.Height));
            Size bounded = new Size(Math.Min(Size.Width, workingArea.Width), Math.Min(Size.Height, workingArea.Height));
            if (bounded != Size) Size = bounded;
        }

        private string ChartText(string arabic, string english) => presentationLeftToRight ? english : arabic;
        private string ChartText(string text)=>presentationLeftToRight?UiLanguageCatalog.Translate(text):text;

        private string ChartFieldLabel(string text)
        {
            switch (text)
            {
                case "رقم الحساب": return ChartText(text, "Account code");
                case "اسم الحساب": return ChartText(text, "Account name");
                case "الحساب الأب": return ChartText(text, "Parent account");
                case "طبيعة الحساب": return ChartText(text, "Account nature");
                case "نوع الحساب": return ChartText(text, "Account type");
                default: return text;
            }
        }

        private void LayoutChartHeader()
        {
            Panel header = Controls.Find("chartOfAccountsHeader", true).OfType<Panel>().FirstOrDefault();
            if (header == null) return;
            Label title = header.Controls.Find("chartHeaderTitle", true).OfType<Label>().FirstOrDefault();
            Label hint = header.Controls.Find("chartHeaderHint", true).OfType<Label>().FirstOrDefault();
            if (title == null || hint == null) return;
            header.Padding = new Padding(24, 10, 24, 10);
            int width = Math.Max(1, header.ClientSize.Width - header.Padding.Horizontal);
            title.Height = TextRenderer.MeasureText(title.Text, title.Font, new Size(width, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height + 4;
            int hintHeight = Math.Max(24, TextRenderer.MeasureText(hint.Text, hint.Font, new Size(width, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height + 4);
            header.Height = title.Height + hintHeight + header.Padding.Vertical;
            header.BackColor = title.BackColor = hint.BackColor = UiTheme.Background;
            title.ForeColor = UiTheme.Text;
            hint.ForeColor = UiTheme.MutedText;
            title.AccessibleName = title.Text; hint.AccessibleName = hint.Text;
        }

        private Panel BuildHeaderPanel()
        {
            Panel header = new Panel
            {
                Name = "chartOfAccountsHeader",
                Dock = DockStyle.Top,
                Height = 72,
                BackColor = UiTheme.Surface,
                Padding = new Padding(24, 0, 24, 0)
            };

            Label title = new Label
            {
                Name = "chartHeaderTitle", Text = ChartText("شجرة الحسابات", "Chart of accounts"),
                Dock = DockStyle.Top,
                TextAlign = presentationLeftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight,
                Font = AppFonts.Create(18F, FontStyle.Bold),
                ForeColor = UiTheme.Text
            };

            Label hint = new Label
            {
                Name = "chartHeaderHint", Text = ChartText("رتب الحسابات الرئيسية والفرعية من هنا", "Manage main accounts and subaccounts here"),
                Dock = DockStyle.Fill,
                TextAlign = presentationLeftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight,
                Font = AppFonts.Create(10F, FontStyle.Regular),
                ForeColor = UiTheme.MutedText
            };

            header.Controls.Add(hint);
            header.Controls.Add(title);
            header.Resize += (sender, args) => LayoutChartHeader();
            return header;
        }

        private Panel BuildTreePanel()
        {
            Panel panel = new Panel
            {
                Dock = DockStyle.Right,
                Width = 560,
                Padding = new Padding(12, 14, 14, 14),
                Name = "chartOfAccountsTreeHost",
                BackColor = UiTheme.SurfaceVariant
            };

            AntdUI.Panel card = DesktopComponents.Card("chartOfAccountsTreeCard");
            card.Dock = DockStyle.Fill;

            Label title = new Label
            {
                Text = ChartText("الحسابات", "Accounts"),
                Dock = DockStyle.Top,
                Height = 58,
                TextAlign = presentationLeftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight,
                Padding = new Padding(0, 0, 6, 0),
                Font = AppFonts.Create(16F, FontStyle.Bold),
                ForeColor = UiTheme.Text
            };

            FlowLayoutPanel tools = new FlowLayoutPanel
            {
                Name = "chartOfAccountsTreeTools",
                Dock = DockStyle.Top,
                Height = 50,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(0, 3, 0, 6),
                BackColor = UiTheme.Surface
            };
            tools.Controls.Add(CreateTinyButton("أعلى", () => MoveTreeSelection(true), 58));
            tools.Controls.Add(CreateTinyButton("أسفل", () => MoveTreeSelection(false), 58));
            tools.Controls.Add(CreateTinyButton("طي", () => accountsTree.CollapseAll(), 58));
            tools.Controls.Add(CreateTinyButton("فتح", () => accountsTree.ExpandAll(), 58));

            accountsTree.Dock = DockStyle.Fill;
            accountsTree.Name = "accountsTree";
            accountsTree.BorderStyle = BorderStyle.None;
            accountsTree.BackColor = UiTheme.Surface;
            accountsTree.ForeColor = UiTheme.Text;
            accountsTree.Font = AppFonts.Create(10.5F, FontStyle.Regular);
            accountsTree.RightToLeft = RightToLeft.Yes;
            accountsTree.RightToLeftLayout = true;
            accountsTree.HideSelection = false;
            accountsTree.ItemHeight = 28;
            accountsTree.ShowLines = true;
            accountsTree.ShowPlusMinus = true;
            accountsTree.ShowRootLines = true;
            accountsTree.AfterSelect += (s, e) => SelectAccountFromNode(e.Node);

            card.Controls.Add(accountsTree);
            card.Controls.Add(tools);
            card.Controls.Add(title);
            panel.Controls.Add(card);
            return panel;
        }

        private Panel BuildEditorPanel()
        {
            Panel shell = new Panel
            {
                Name = "chartOfAccountsEditorHost",
                Dock = DockStyle.Fill,
                Padding = new Padding(18, 14, 12, 14),
                BackColor = UiTheme.SurfaceVariant
            };

            AntdUI.Panel form = DesktopComponents.Card("chartOfAccountsEditorCard");
            form.Dock = DockStyle.Top;
            form.Height = 400;
            form.Padding = new Padding(26);

            txtCode.TextAlign = HorizontalAlignment.Right;
            txtName.TextAlign = HorizontalAlignment.Right;
            foreach (string nature in new[] { "Asset", "Liability", "Equity", "Revenue", "Expense" })
                accountNature.Items.Add(ToDisplayType(nature));
            accountNature.SelectedIndex = 0;
            parentAccount.SelectedIndexChanged += (s, e) =>
            {
                if(chartInvalidating||!chartDataReady)return;
                if(!EnsureChartContext())return;
                if (parentAccount.SelectedIndex > 0)
                {
                    childAccount.Checked = true;
                    accountNature.Text = FindAccountType(SelectedParentCode());
                }
            };

            TableLayoutPanel fields = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 4,
                Name = "chartOfAccountsFields",
                BackColor = UiTheme.Surface
            };
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));
            fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));
            fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 132));

            Label formTitle = new Label
            {
                Text = ChartText("بيانات الحساب", "Account details"),
                Dock = DockStyle.Fill,
                TextAlign = presentationLeftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight,
                Font = AppFonts.Create(15F, FontStyle.Bold),
                ForeColor = UiTheme.Text
            };
            fields.Controls.Add(formTitle, 0, 0);
            fields.SetColumnSpan(formTitle, 2);

            fields.Controls.Add(CreateFieldBox("رقم الحساب", txtCode), 0, 1);
            fields.Controls.Add(CreateFieldBox("اسم الحساب", txtName), 1, 1);
            fields.Controls.Add(CreateFieldBox("الحساب الأب", parentAccount), 0, 2);
            fields.Controls.Add(CreateFieldBox("طبيعة الحساب", accountNature), 1, 2);

            FlowLayoutPanel typePanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                Name = "chartAccountTypeOptions",
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(8, 0, 8, 0),
                BackColor = UiTheme.Surface
            };
            typePanel.Controls.Add(mainAccount);
            typePanel.Controls.Add(childAccount);
            typePanel.Controls.Add(chkActive);
            mainAccount.Font = childAccount.Font = chkActive.Font = AppFonts.Create(10F, FontStyle.Regular);
            fields.Controls.Add(CreateFieldBox("نوع الحساب", typePanel), 0, 3);
            fields.SetColumnSpan(fields.GetControlFromPosition(0, 3), 2);

            form.Controls.Add(fields);
            statusLabel.Dock = DockStyle.Top;
            statusLabel.Height = 34;
            statusLabel.TextAlign = ContentAlignment.MiddleRight;
            statusLabel.Font = AppFonts.Create(9.5F, FontStyle.Regular);
            statusLabel.Name = "chartOfAccountsStatus";
            statusLabel.BackColor = UiTheme.SurfaceVariant;
            statusLabel.ForeColor = chartStatusIssue ? UiTheme.Danger : UiTheme.MutedText;
            statusLabel.Text = ChartText("اختر حسابا من الشجرة أو أنشئ حسابا جديدا.");
            shell.Controls.Add(form);
            shell.Controls.Add(statusLabel);
            return shell;
        }

        private Panel BuildBottomToolbar()
        {
            var toolbar = new Panel { Name = "chartOfAccountsToolbar", Dock = DockStyle.Bottom,
                Height = 64, BackColor = UiTheme.Background };
            var actions = new FlowLayoutPanel { Name = "chartFooterActions", Dock = DockStyle.Fill,
                RightToLeft = RightToLeft.No, WrapContents = false,
                FlowDirection = presentationLeftToRight ? FlowDirection.LeftToRight : FlowDirection.RightToLeft,
                Padding = new Padding(16, 4, 16, 0) };
            actions.Controls.Add(CreateToolbarButton("chartNewAccount", ChartText("جديد", "New"), AppIcon.Add, NewAccount, 106, UiTheme.Primary));
            actions.Controls.Add(CreateToolbarButton("chartSaveAccount", ChartText("حفظ التعديلات", "Save changes"), AppIcon.Save, SaveAccount, 186, UiTheme.Primary));
            actions.Controls.Add(CreateToolbarButton("chartDeleteAccount", ChartText("حذف", "Delete"), AppIcon.Delete, DeleteAccount, 110, UiTheme.Danger));
            actions.Controls.Add(CreateToolbarButton("chartRefreshAccount",ChartText("تحديث","Refresh"),AppIcon.Refresh,LoadInitialDataAsync,104,UiTheme.Primary));
            actions.Controls.Add(CreateToolbarButton("chartExit", ChartText("خروج", "Close"), AppIcon.Close, () => Close(), 104, UiTheme.Danger));
            toolbar.Controls.Add(actions);
            return toolbar;
        }
        private async void LoadInitialDataAsync()
        { await RefreshChartAsync(); }

        private void SetChartBusy(bool writing)
        {
            if(IsDisposed)return;
            chartWriteBusy=writing;
            foreach(string name in new[]{"chartSaveAccount","chartDeleteAccount"})
            {var button=Controls.Find(name,true).OfType<AntdUI.Button>().FirstOrDefault();if(button!=null)button.Enabled=!previewMode&&chartDataReady&&!writing;}
            foreach(string name in new[]{"chartNewAccount","chartRefreshAccount"})
            {var button=Controls.Find(name,true).OfType<AntdUI.Button>().FirstOrDefault();if(button!=null)button.Enabled=!writing;}
            accountsTree.Enabled=chartDataReady&&!writing;
            var editor=Controls.Find("chartOfAccountsEditorCard",true).FirstOrDefault();if(editor!=null)editor.Enabled=chartDataReady&&!writing;
        }
        private void SetChartStatus(string text,bool issue=false)
        {if(IsDisposed)return;statusLabel.Text=text;statusLabel.AccessibleDescription=text;chartStatusIssue=issue;statusLabel.ForeColor=issue?UiTheme.Danger:UiTheme.MutedText;}
        private void LayoutChartStatus()
        {
            if(chartStatusSizing||IsDisposed||statusLabel.Parent==null||statusLabel.ClientSize.Width<=0)return;
            chartStatusSizing=true;
            try
            {
                statusLabel.TextAlign=presentationLeftToRight?ContentAlignment.MiddleLeft:ContentAlignment.MiddleRight;
                var flags=TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix;
                if(!presentationLeftToRight)flags|=TextFormatFlags.RightToLeft;
                int height=TextRenderer.MeasureText(statusLabel.Text,statusLabel.Font,new Size(statusLabel.ClientSize.Width,int.MaxValue),flags).Height;
                int wanted=Math.Max(34,height+8);
                if(statusLabel.Height!=wanted)statusLabel.Height=wanted;
                var editor=Controls.Find("chartOfAccountsEditorHost",true).FirstOrDefault();
                var card=Controls.Find("chartOfAccountsEditorCard",true).FirstOrDefault();
                var tree=Controls.Find("chartOfAccountsTreeHost",true).FirstOrDefault();
                var content=Controls.Find("chartOfAccountsContentHost",true).OfType<Panel>().FirstOrDefault();
                if(editor!=null&&card!=null&&tree!=null&&content!=null&&editor.Dock==DockStyle.None&&content.AutoScroll)
                {editor.Height=Math.Max(480,card.Height+editor.Padding.Vertical+statusLabel.Height);content.AutoScrollMinSize=new Size(0,tree.Height+editor.Height);}
            }
            finally{chartStatusSizing=false;}
        }
        private void ChartPresentationIssue(Exception error)
        {
            if(IsDisposed)return;
            string message=string.IsNullOrEmpty(acceptedAccountCode)
                ?ChartText("تعذر تحديث الشجرة. بقي العرض السابق؛ أعد محاولة التحديث.","Could not refresh the tree. The previous view remains; retry refresh.")
                :acceptedAccountMessage+ChartText(" تعذر تحديث العرض. لا تعد تنفيذ العملية نفسها؛ أعد التحديث."," Could not refresh the view. Do not repeat the operation; retry refresh.");
            if(!previewMode)message+=ChartText(" رمز الدعم: "," Support code: ")+AppDiagnostics.Log("ChartPresentation",error);
            SetChartStatus(message,true);
        }
        internal async System.Threading.Tasks.Task<bool> RefreshChartAsync(Func<System.Threading.Tasks.Task<DataTable>> read=null,
            Func<System.Threading.CancellationToken,System.Threading.Tasks.Task<DataTable>> cancellableRead=null)
        {
            CancelChartRead();
            if(chartDataReady&&!IsChartContextCurrent(chartContextCurrent))InvalidateChartContext();
            int generation=++chartReadGeneration;
            var cancellation=new System.Threading.CancellationTokenSource();chartReadCancellation=cancellation;
            Func<bool> requestCurrent=null;DataTable loaded=null;
            try
            {
                requestCurrent=CaptureChartContext();
                if(!IsChartContextCurrent(requestCurrent)){InvalidateChartContext();return false;}
                SetChartStatus(string.IsNullOrEmpty(acceptedAccountCode)?ChartText("جار تحميل شجرة الحسابات...","Loading chart of accounts..."):acceptedAccountMessage);
                loaded=await(cancellableRead!=null?cancellableRead(cancellation.Token):read!=null?read():AccountingLookupDesktopService.LoadAccountsAsync(false,cancellation.Token));
                if(IsDisposed||generation!=chartReadGeneration||cancellation.IsCancellationRequested){loaded?.Dispose();return false;}
                if(!IsChartContextCurrent(requestCurrent)){loaded?.Dispose();InvalidateChartContext();return false;}
                if(loaded==null)throw new InvalidOperationException("لم تُرجع قراءة الحسابات بيانات صالحة.");
                var previous=accountsTable;var previousContext=chartContextCurrent;
                bool previousReady=chartDataReady;
                accountsTable=loaded;chartContextCurrent=requestCurrent;chartDataReady=true;
                try {BuildTree();LoadParentAccounts();}
                catch {accountsTable=previous;chartContextCurrent=previousContext;chartDataReady=previousReady;throw;}
                if(chartEditorCurrent!=null&&!IsChartContextCurrent(chartEditorCurrent))ClearChartEditor();
                chartEditorCurrent=requestCurrent;
                if(previous!=loaded)previous?.Dispose();
                SetChartBusy(chartWriteBusy);
                SetChartStatus(string.IsNullOrEmpty(acceptedAccountCode)?ChartText("تم تحديث شجرة الحسابات.","Chart of accounts refreshed."):acceptedAccountMessage);
                if(chartHierarchyIssues>0)SetChartStatus(statusLabel.Text+ChartText(" توجد روابط حسابات غير سليمة؛ عُرضت الحسابات في الجذر للمراجعة."," Invalid account links were shown at the root for review."),true);
                return true;
            }
            catch(OperationCanceledException)
            {
                if(!IsDisposed&&generation==chartReadGeneration)
                {
                    if(!IsChartContextCurrent(requestCurrent))InvalidateChartContext();
                    else {SetChartStatus(ChartText("أُلغي تحميل شجرة الحسابات. يمكنك إعادة التحديث.","Loading the chart was cancelled. You can refresh again."));SetChartBusy(chartWriteBusy);}
                }
                return false;
            }
            catch(Exception error)
            {
                if(IsDisposed||generation!=chartReadGeneration)return false;
                if(loaded!=null&&loaded!=accountsTable)loaded.Dispose();
                if(!IsChartContextCurrent(requestCurrent)||(chartDataReady&&!IsChartContextCurrent(chartContextCurrent)))InvalidateChartContext();
                else {ChartPresentationIssue(error);SetChartBusy(chartWriteBusy);}return false;
            }
            finally
            {
                if(ReferenceEquals(chartReadCancellation,cancellation))chartReadCancellation=null;
                cancellation.Dispose();
            }
        }

        private void LoadParentAccounts()
        {
            string current=SelectedParentCode();
            parentAccount.Items.Clear();parentAccount.Items.Add(ChartText("بدون","None"));
            foreach(DataRow row in accountsTable.Rows)
            {
                string code=Convert.ToString(row["AccountCode"]),name=Convert.ToString(row["AccountName"]);
                if(!string.Equals(code,txtCode.Text.Trim(),StringComparison.OrdinalIgnoreCase))parentAccount.Items.Add(code+" - "+name);
            }
            SelectParent(current);
        }
        private void BuildTree()
        {
            var nodes=new Dictionary<string,TreeNode>(StringComparer.OrdinalIgnoreCase);
            var parents=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
            foreach(DataRow row in accountsTable.Rows)
            {
                string code=Convert.ToString(row["AccountCode"]);
                nodes.Add(code,new TreeNode(code+" - "+Convert.ToString(row["AccountName"])){Tag=row});
                parents.Add(code,Convert.ToString(row["ParentAccountCode"]));
            }
            var roots=new List<TreeNode>();int issues=0;
            foreach(var pair in parents)
            {
                bool valid=true;
                try {PharmacyManagement.Domain.AccountHierarchy.ValidateParent(pair.Key,pair.Value,parents);}
                catch(InvalidOperationException){valid=false;issues++;}
                if(valid&&!string.IsNullOrWhiteSpace(pair.Value))nodes[pair.Value.Trim()].Nodes.Add(nodes[pair.Key]);
                else roots.Add(nodes[pair.Key]);
            }
            accountsTree.BeginUpdate();
            try {accountsTree.Nodes.Clear();accountsTree.Nodes.AddRange(roots.ToArray());accountsTree.ExpandAll();chartHierarchyIssues=issues;}
            finally {accountsTree.EndUpdate();}
        }


        private void SelectAccountFromNode(TreeNode node)
        {
            if(!EnsureChartContext())return;
            if (node == null || node.TreeView!=accountsTree || !(node.Tag is DataRow)) return;
            DataRow row = (DataRow)node.Tag;
            if(row.Table!=accountsTable)return;
            chartEditorCurrent=chartContextCurrent;
            txtCode.Text = Convert.ToString(row["AccountCode"]);
            txtName.Text = Convert.ToString(row["AccountName"]);
            accountNature.Text = ToDisplayType(Convert.ToString(row["AccountType"]));
            chkActive.Checked = Convert.ToBoolean(row["IsActive"]);
            LoadParentAccounts();
            SelectParent(Convert.ToString(row["ParentAccountCode"]));
            childAccount.Checked = !string.IsNullOrWhiteSpace(SelectedParentCode());
            mainAccount.Checked = !childAccount.Checked;
        }

        private void NewAccount()
        {
            if(!EnsureChartContext())return;
            ClearChartEditor();chartEditorCurrent=chartContextCurrent;
            SetChartStatus(ChartText("جاهز لإضافة حساب جديد.","Ready to add a new account."));txtCode.Focus();
        }
        private void ClearChartEditor()
        {
            txtCode.Clear();
            txtName.Clear();
            chkActive.Checked = true;
            mainAccount.Checked = true;
            if (parentAccount.Items.Count > 0) parentAccount.SelectedIndex = 0;
            if (accountNature.Items.Count > 0) accountNature.SelectedIndex = 0;

        }

        private async void SaveAccount()
        {
            if(!EnsureChartContext(true)||previewMode||chartWriteBusy)return;
            if(!PermissionService.EnsureAllowed(AppOperation.ManageChartOfAccounts,this)||!EnsureChartContext(true))return;
            var writeContext=chartEditorCurrent;
            PharmacyManagement.Application.AccountWriteResult result;
            CancelChartRead();SetChartBusy(true);++chartReadGeneration;
            try
            {
                string parent=childAccount.Checked?SelectedParentCode():string.Empty;
                string type=childAccount.Checked&&!string.IsNullOrWhiteSpace(parent)?FindAccountType(parent):accountNature.Text;
                var request=new PharmacyManagement.Application.AccountSaveRequest(txtCode.Text,txtName.Text,ToStoredType(type),parent,chkActive.Checked);
                SetChartStatus(ChartText("جار حفظ الحساب...","Saving account..."));
                result=await AccountWritingDesktopService.SaveAsync(request);
            }
            catch(Exception error)
            {if(!IsChartContextCurrent(writeContext)){InvalidateChartContext();SetChartBusy(false);return;}if(!IsDisposed){SetChartStatus(ChartText("لم يُحفظ الحساب. راجع البيانات وأعد المحاولة.","Account was not saved. Check the data and retry."),true);UiTheme.ShowOperationIssue(this,"حفظ الحساب",error);}SetChartBusy(false);return;}
            if(!IsChartContextCurrent(writeContext)){InvalidateChartContext();SetChartBusy(false);return;}
            try {await CompleteSavedAccountPresentationAsync(result,false);}
            finally {SetChartBusy(false);}
        }
        private async void DeleteAccount()
        {
            if(!EnsureChartContext(true)||previewMode||chartWriteBusy)return;
            if(!PermissionService.EnsureAllowed(AppOperation.ManageChartOfAccounts,this)||string.IsNullOrWhiteSpace(txtCode.Text)||!EnsureChartContext(true))return;
            var writeContext=chartEditorCurrent;
            bool confirmed=ChartDeleteConfirmation!=null?ChartDeleteConfirmation():AppNotifications.ConfirmDelete(this,ChartText("هل تريد حذف الحساب المحدد؟","Delete the selected account?"),Text);
            if(!confirmed||!EnsureChartContext(true))return;
            string code=txtCode.Text.Trim();PharmacyManagement.Application.AccountWriteResult result;
            CancelChartRead();SetChartBusy(true);++chartReadGeneration;
            try {SetChartStatus(ChartText("جار حذف الحساب...","Deleting account..."));result=await AccountWritingDesktopService.DeleteAsync(code);}
            catch(Exception error)
            {if(!IsChartContextCurrent(writeContext)){InvalidateChartContext();SetChartBusy(false);return;}if(!IsDisposed){SetChartStatus(ChartText("لم يُحذف الحساب. راجع السبب وأعد المحاولة.","Account was not deleted. Check the issue and retry."),true);UiTheme.ShowOperationIssue(this,"حذف الحساب",error);}SetChartBusy(false);return;}
            if(!IsChartContextCurrent(writeContext)){InvalidateChartContext();SetChartBusy(false);return;}
            try {await CompleteSavedAccountPresentationAsync(result,true,null,NewAccount);}
            finally {SetChartBusy(false);}
        }
        internal async System.Threading.Tasks.Task<bool> CompleteSavedAccountPresentationAsync(PharmacyManagement.Application.AccountWriteResult result,
            bool deletion,Func<System.Threading.Tasks.Task<DataTable>> read=null,Action clearEditor=null)
        {
            if(result==null||string.IsNullOrWhiteSpace(result.Code))throw new ArgumentException("نتيجة الحساب غير صالحة.");
            acceptedAccountCode=result.Code;
            acceptedAccountMessage=deletion
                ?(result.Changed?ChartText("تم حذف الحساب ","Account deleted: "):ChartText("الحساب غير موجود: ","Account does not exist: "))+result.Code+"."
                :ChartText("تم حفظ الحساب ","Account saved: ")+result.Code+".";
            ++chartReadGeneration;
            if(IsDisposed)return false;
            SetChartStatus(acceptedAccountMessage);
            try
            {
                clearEditor?.Invoke();
                bool refreshed=await RefreshChartAsync(read);
                if(refreshed&&!deletion&&!IsDisposed)SelectNodeByCode(result.Code);
                return refreshed;
            }
            catch(Exception error){ChartPresentationIssue(error);return false;}
        }


        private Panel CreateFieldBox(string label, Control control)
        {
            Panel panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8, 0, 8, 8), BackColor = UiTheme.Surface };
            Label caption = new Label
            {
                Text = ChartFieldLabel(label),
                Dock = DockStyle.Top,
                Height = 24,
                TextAlign = presentationLeftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight,
                ForeColor = UiTheme.MutedText,
                Font = AppFonts.Create(9F, FontStyle.Regular)
            };
            // Preserve native bidi editing/selection while retaining the shared AntdUI field shell.
            if (control is TextBox) control = DesktopComponents.NativeTextFrame((TextBox)control, control.Name + "Frame");
            else if (control is ComboBox) control = DesktopComponents.NativeSelectFrame((ComboBox)control, control.Name + "Frame", control.AccessibleName, true);
            control.Dock = control is FlowLayoutPanel ? DockStyle.Fill : DockStyle.None;
            if (!(control is FlowLayoutPanel))
            {
                control.Height = 44;
                panel.Resize += (sender, args) => control.SetBounds(panel.Padding.Left, 24 +
                    Math.Max(0, (panel.ClientSize.Height - panel.Padding.Vertical - 24 - 44) / 2),
                    Math.Max(1, panel.ClientSize.Width - panel.Padding.Horizontal), 44);
            }
            control.Font = AppFonts.Create(10F, FontStyle.Regular);
            panel.Controls.Add(control);
            panel.Controls.Add(caption);
            return panel;
        }

        private AntdUI.Button CreateToolbarButton(string name, string text, AppIcon icon, Action action, int width, Color backColor)
        {
            bool primary = backColor == UiTheme.Primary;
            AntdUI.Button button = DesktopComponents.Action(name, text, icon, primary);
            button.Width = width;
            button.Height = DesktopDesignTokens.ActionHeight;
            button.Margin = new Padding(5);
            button.Font = AppFonts.Create(10.5F, FontStyle.Bold);
            button.AccessibleDescription = "إجراء شجرة الحسابات";
            if (name == "chartSaveAccount" || name == "chartDeleteAccount") button.Enabled = !previewMode;
            AntdThemeAdapter.Apply(button);
            button.Click += (s, e) => action();
            return button;
        }

        private AntdUI.Button CreateTinyButton(string text, Action action, int width)
        {
            AntdUI.Button button = new AccessibleAntdButton
            {
                Text = text,
                Width = Math.Max(64, width),
                Height = 44,
                Margin = new Padding(4, 3, 0, 3),
                Font = AppFonts.Create(9F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                AccessibleName = text,
                AccessibleDescription = "أداة شجرة الحسابات"
            };
            DesktopComponents.StyleOutlinedFooterAction(button);
            button.Click += (s, e) => action();
            return button;
        }

        private void MoveTreeSelection(bool up)
        {
            if (accountsTree.SelectedNode == null) return;
            TreeNode next = up ? accountsTree.SelectedNode.PrevVisibleNode : accountsTree.SelectedNode.NextVisibleNode;
            if (next != null) accountsTree.SelectedNode = next;
        }

        private string SelectedParentCode()
        {
            if (parentAccount.SelectedIndex <= 0 || parentAccount.SelectedIndex >= parentAccount.Items.Count) return string.Empty;
            string value = Convert.ToString(parentAccount.Items[parentAccount.SelectedIndex]);
            if (string.IsNullOrWhiteSpace(value) || value == "بدون") return string.Empty;
            int dash = value.IndexOf(" - ", StringComparison.Ordinal);
            return dash > 0 ? value.Substring(0, dash).Trim() : value.Trim();
        }

        private void SelectParent(string code)
        {
            if (parentAccount.Items.Count == 0) return;
            if (string.IsNullOrWhiteSpace(code))
            {
                parentAccount.SelectedIndex = 0;
                return;
            }
            for (int i = 0; i < parentAccount.Items.Count; i++)
            {
                if (Convert.ToString(parentAccount.Items[i]).StartsWith(code + " - ", StringComparison.Ordinal))
                {
                    parentAccount.SelectedIndex = i;
                    return;
                }
            }
            parentAccount.SelectedIndex = 0;
        }

        private string FindAccountType(string code)
        {
            if (accountsTable == null || string.IsNullOrWhiteSpace(code)) return accountNature.Text;
            foreach (DataRow row in accountsTable.Rows)
            {
                if (string.Equals(Convert.ToString(row["AccountCode"]), code, StringComparison.OrdinalIgnoreCase))
                {
                    return ToDisplayType(Convert.ToString(row["AccountType"]));
                }
            }
            return accountNature.Text;
        }

        private string ToDisplayType(string storedType)
        {
            switch ((storedType ?? string.Empty).Trim())
            {
                case "Asset": return ChartText("الأصول", "Assets");
                case "Liability": return ChartText("الخصوم", "Liabilities");
                case "Equity": return ChartText("حقوق الملكية", "Equity");
                case "Revenue": return ChartText("الإيرادات", "Revenue");
                case "Expense": return ChartText("المصروفات", "Expenses");
                default: return string.IsNullOrWhiteSpace(storedType) ? "الأصول" : storedType;
            }
        }

        private string ToStoredType(string displayType)
        {
            switch ((displayType ?? string.Empty).Trim())
            {
                case "Assets": case "الأصول": return "Asset";
                case "Liabilities": case "الخصوم": return "Liability";
                case "Equity": case "حقوق الملكية": return "Equity";
                case "Revenue": case "الإيرادات": return "Revenue";
                case "Expenses": case "المصروفات": return "Expense";
                default: return string.IsNullOrWhiteSpace(displayType) ? "Asset" : displayType;
            }
        }

        private void SelectNodeByCode(string code)
        {
            foreach (TreeNode node in EnumerateNodes(accountsTree.Nodes))
            {
                if (node.Tag is DataRow && string.Equals(Convert.ToString(((DataRow)node.Tag)["AccountCode"]), code, StringComparison.OrdinalIgnoreCase))
                {
                    accountsTree.SelectedNode = node;
                    node.EnsureVisible();
                    return;
                }
            }
        }

        private IEnumerable<TreeNode> EnumerateNodes(TreeNodeCollection nodes)
        {
            foreach (TreeNode node in nodes)
            {
                yield return node;
                foreach (TreeNode child in EnumerateNodes(node.Nodes))
                {
                    yield return child;
                }
            }
        }
    }

    public partial class JournalEntriesForm : Form
    {
        private string JournalText(string text) => RightToLeft == RightToLeft.No ? UiLanguageCatalog.Translate(text) : text;
        private string JournalFormat(string template, params object[] values) => UiLanguageCatalog.Format(RightToLeft == RightToLeft.No, template, values);
        private readonly AntdUI.DatePicker entryDate = DesktopComponents.DatePicker("journalEntryDate", "تاريخ القيد", DateTime.Today);
        private readonly TextBox entryNo = new TextBox { Name = "journalEntryNumber", AccessibleName = "رقم القيد", ReadOnly = true };
        private readonly ComboBox branch = new NativeThemedSelect { Name = "journalBranch", AccessibleName = "الفرع", DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly AntdUI.Select attachmentType = DesktopComponents.Select("journalAttachmentType", "نوع المرفق");
        private readonly AntdUI.Input attachmentNo = CreateLtrJournalInput("journalAttachmentNumber", "رقم المرفق");
        private readonly TextBox entryStatus = new TextBox { Name = "journalEntryStatus", AccessibleName = "حالة القيد", ReadOnly = true };
        private readonly CheckBox repeatDescription = new CheckBox { Text = "تكرار البيان" };
        private readonly TextBox headerDescription = new TextBox { Name = "journalHeaderDescription", AccessibleName = "بيان القيد" };
        private readonly AntdUI.Input referenceNo = CreateLtrJournalInput("journalReferenceNumber", "رقم المرجع");
        private readonly ComboBox costCenter = new NativeThemedSelect { Name = "journalCostCenter", AccessibleName = "مركز التكلفة", DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly ComboBox account = new NativeThemedSelect { Name = "journalAccount", AccessibleName = "رقم الحساب", Font = AppFonts.Create(10F), DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly AntdUI.Input debit = CreateLtrJournalInput("journalDebit", "المدين");
        private readonly AntdUI.Input credit = CreateLtrJournalInput("journalCredit", "الدائن");
        private readonly AntdUI.Input lineDate = CreateLtrJournalInput("journalLineDate", "تاريخ السطر");
        private readonly AntdUI.Input exchangeRate = CreateLtrJournalInput("journalExchangeRate", "سعر التحويل");
        private readonly AntdUI.Input localValue = CreateLtrJournalInput("journalLocalValue", "القيمة بالعملة المحلية");
        private readonly AntdUI.Select currency = CreateLtrJournalSelect("journalCurrency", "العملة");
        private readonly TextBox lineDescription = new TextBox { Name = "journalLineDescription", AccessibleName = "بيان السطر" };
        private readonly Panel journalEditorViewport = new PhysicalScrollViewport { Name = "journalEditorViewport", Dock = DockStyle.Top, AutoScroll = true, Tag = "UiSurface", AutoScrollMargin = Size.Empty };
        private readonly DataGridView linesGrid = new DataGridView();
        private readonly Label totalsLabel = new Label();
        private readonly Label statusLabel = new Label();
        private readonly DataTable lines = new DataTable();
        private readonly string formTitle;
        private readonly string entrySourceType;
        private readonly bool previewMode;
        private int journalActionId;
        private int lookupLoadGeneration;
        private bool journalWriteBusy;
        private bool journalDraftAccepted;
        private int acceptedJournalId;
        private bool journalStatusSizing;
        private bool journalStatusLayoutPending;
        private bool journalViewportSizing;
        private Func<bool> journalEditorCurrent;
        private bool journalOptionsReady,journalInvalidating;
        private System.Threading.CancellationTokenSource journalLookupCancellation;
        private System.Threading.CancellationTokenSource journalPrintCancellation;
        private bool journalPrintPending;
        private void CancelJournalPrint(){var cancellation=journalPrintCancellation;if(cancellation!=null&&!cancellation.IsCancellationRequested)cancellation.Cancel();}
        private Func<bool> CaptureJournalEditorContext()
        {
            if(!previewMode){var session=ApplicationServices.CreateAccountingReadSession();return ()=>session.IsLookupCurrent;}
            if(!SessionContext.IsAuthenticated)return ()=>true;
            long revision=SessionContext.CurrentSessionRevision;
            return ()=>SessionContext.IsAuthenticated&&SessionContext.CurrentSessionRevision==revision;
        }
        internal Func<bool> CreateJournalPrintContext(Func<bool> editorContext)
        {
            var session=previewMode?null:ApplicationServices.CreateJournalDocumentReadSession();
            return ()=>!IsDisposed&&!Disposing&&ReferenceEquals(editorContext,journalEditorCurrent)&&JournalContextCurrent(editorContext)&&(session==null||session.IsCurrent);
        }
        private bool JournalContextCurrent(Func<bool> current)
        {try{return current!=null&&current();}catch{return false;}}
        private void CancelJournalLookup()
        {var cancellation=journalLookupCancellation;if(cancellation!=null&&!cancellation.IsCancellationRequested)cancellation.Cancel();}
        private void InvalidateJournalContext()
        {
            if(journalInvalidating||IsDisposed)return;journalInvalidating=true;
            try
            {
                ++lookupLoadGeneration;CancelJournalLookup();CancelJournalPrint();ReleaseJournalRecovery();journalOptionsReady=false;journalEditorCurrent=null;
                ResetJournalDraft();acceptedJournalId=0;journalDraftAccepted=false;
                account.Items.Clear();account.Text=string.Empty;currency.SelectedIndex=-1;currency.Items.Clear();currency.Text=string.Empty;
                costCenter.Items.Clear();costCenter.Text=string.Empty;
                journalEditorViewport.Enabled=false;linesGrid.Enabled=false;
                SetJournalStatus("تغيرت جلسة القيد أو صلاحيتها. اختر جديد لإعادة تحميل الخيارات بمسودة فارغة.");
            }
            finally{journalInvalidating=false;}
        }
        private bool EnsureJournalEditor()
        {
            if(IsDisposed||Disposing||journalInvalidating)return false;
            if(journalOptionsReady&&JournalContextCurrent(journalEditorCurrent))return true;
            InvalidateJournalContext();return false;
        }
        internal string SelectedJournalDraftText()
        {
            if(journalWriteBusy||!EnsureJournalEditor())return null;
            var row=linesGrid.CurrentRow;if(row==null||row.IsNewRow)return null;
            return string.Join("\t",linesGrid.Columns.Cast<DataGridViewColumn>().OrderBy(c=>c.Index).Select(c=>{
                var value=row.Cells[c.Index].Value;return value==null||value==DBNull.Value?string.Empty:Convert.ToString(value,System.Globalization.CultureInfo.InvariantCulture);
            }));
        }
        private async void ReloadJournalEditor()
        {await LoadInitialLookupsAsync();}


        private static AntdUI.Input CreateLtrJournalInput(string name, string accessibleName)
        {
            AntdUI.Input input = DesktopComponents.Input(name, accessibleName);
            input.RightToLeft = RightToLeft.No;
            input.TextAlign = HorizontalAlignment.Right;
            return input;
        }

        private static AntdUI.Select CreateLtrJournalSelect(string name, string accessibleName)
        {
            AntdUI.Select select = DesktopComponents.Select(name, accessibleName);
            select.RightToLeft = RightToLeft.No;
            select.TextAlign = HorizontalAlignment.Left;
            return select;
        }

        public JournalEntriesForm() : this("القيود اليومية", "Manual")
        {
        }

        public JournalEntriesForm(string title, string sourceType)
            : this(title, sourceType, false)
        {
        }

        internal JournalEntriesForm(string title, string sourceType, bool previewMode)
        {
            formTitle = string.IsNullOrWhiteSpace(title) ? "القيود اليومية" : title;
            entrySourceType = string.IsNullOrWhiteSpace(sourceType) ? "Manual" : sourceType;
            this.previewMode = previewMode;
            Text = formTitle;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = true;
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(1100, 720);
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            KeyPreview = true;
            InitializeLineTable();
            InitializeComponent();
            account.Name = "journalAccount";
            currency.Name = "journalCurrency";
            entryNo.Name = "journalEntryNumber";
            UiTheme.Apply(this);
            foreach (TextBox description in new[] { headerDescription, lineDescription, entryNo, entryStatus })
                description.TextChanged += (s, e) => UiDirection.ApplyContentDirection(description, RightToLeft == RightToLeft.No);
            foreach (ComboBox selector in new[] { branch, costCenter })
                selector.TextChanged += (s, e) => UiDirection.ApplyContentDirection(selector, RightToLeft == RightToLeft.No);
            ApplyJournalToolbarTheme();
            linesGrid.BackgroundColor = UiTheme.Surface;
            linesGrid.GridColor = UiTheme.Border;
            FitToWorkingArea();
            ResetJournalDraft();
            if (previewMode) PopulatePreviewData();
            else Load += async (s, e) =>
            {
                FitToWorkingArea();
                await LoadInitialLookupsAsync();
            };
            Resize += (s, e) => ApplyResponsiveLayout();
            Layout += (s, e) => FitJournalEditorViewport();
            Load += (s, e) => ApplyResponsiveLayout();
            ApplyResponsiveLayout();
            FormClosing += (s, e) =>
            {
                if (journalWriteBusy && !journalPrintPending && e.CloseReason == CloseReason.UserClosing && !(journalRecovery != null && (journalRecovery.Pending != null || journalRecovery.AcceptedId > 0)))
                { e.Cancel = true; SetJournalStatus("انتظر اكتمال عملية القيد قبل إغلاق النافذة."); }
            };
            statusLabel.UseMnemonic = false;
            statusLabel.TextChanged += (s, e) => FitJournalStatus();
            statusLabel.SizeChanged += (s, e) => ScheduleJournalStatusLayout();
            statusLabel.FontChanged += (s, e) => FitJournalStatus();
            totalsLabel.TextChanged += (s, e) => FitJournalStatus();
            totalsLabel.FontChanged += (s, e) => FitJournalStatus();
            FitJournalStatus();
            FormClosed += (s, e) => lookupLoadGeneration++;
            FormClosing += (s,e)=>{if(!e.Cancel){++lookupLoadGeneration;CancelJournalLookup();CancelJournalPrint();}};
            Disposed += (s,e)=>{++lookupLoadGeneration;CancelJournalLookup();CancelJournalPrint();ReleaseJournalRecovery();};
            Activated += (s,e)=>{if(journalEditorCurrent!=null&&!JournalContextCurrent(journalEditorCurrent))InvalidateJournalContext();};
            UiSessionSubscription.Attach(this,()=>{if(journalEditorCurrent!=null&&!JournalContextCurrent(journalEditorCurrent))InvalidateJournalContext();});
            linesGrid.ClipboardCopyMode=DataGridViewClipboardCopyMode.Disable;
            linesGrid.KeyDown += (s,e)=>{if(e.Control&&e.KeyCode==Keys.C){e.Handled=true;e.SuppressKeyPress=true;string text=SelectedJournalDraftText();if(text!=null)Clipboard.SetText(text);}};

            KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); };
        }

        private void FitToWorkingArea()
        {
            Rectangle workingArea = Screen.FromControl(this).WorkingArea;
            MaximumSize = workingArea.Size;
            MinimumSize = new Size(Math.Min(760, workingArea.Width), Math.Min(600, workingArea.Height));
            Size = new Size(Math.Min(Math.Max(Width, MinimumSize.Width), workingArea.Width), Math.Min(Math.Max(Height, MinimumSize.Height), workingArea.Height));
        }

        internal void ApplyResponsiveLayout()
        {
            FlowLayoutPanel primary = Controls.Find("journalPrimaryActions", true).OfType<FlowLayoutPanel>().FirstOrDefault();
            FlowLayoutPanel secondary = Controls.Find("journalSecondaryActions", true).OfType<FlowLayoutPanel>().FirstOrDefault();
            TableLayoutPanel header = Controls.Find("journalHeaderFields", true).OfType<TableLayoutPanel>().FirstOrDefault();
            TableLayoutPanel lineFields = Controls.Find("journalLineFields", true).OfType<TableLayoutPanel>().FirstOrDefault();
            if (primary == null || secondary == null || header == null || lineFields == null) return;
            bool compact = ClientSize.Width < 1120;
            primary.SuspendLayout();
            secondary.SuspendLayout();
            header.SuspendLayout();
            lineFields.SuspendLayout();
            AntdUI.Button addLine = lineFields.Controls.Find("postJournalLine", true).OfType<AntdUI.Button>().FirstOrDefault();
            if (compact) ConfigureCompactJournalFields(header, lineFields, addLine);
            else ConfigureDesktopJournalFields(header, lineFields, addLine);
            primary.Width = compact ? 330 : 430;
            foreach (AntdUI.Button button in primary.Controls.OfType<AntdUI.Button>())
            {
                if (button.AccessibleName == "إنشاء قيد جديد") button.Width = compact ? 84 : 116;
                else if (button.AccessibleName == "حفظ القيد") button.Width = compact ? 88 : 120;
                else if (button.AccessibleName == "حفظ القيد وطباعته") button.Width = compact ? 120 : 150;
            }
            foreach (AntdUI.Button button in secondary.Controls.OfType<AntdUI.Button>())
            {
                string action = button.AccessibleName ?? string.Empty;
                button.Visible = !compact || (action != "السطر الأول" && action != "السطر الأخير" && action != "بدء قيد جديد");
                if (!compact) continue;
                if (action == "خروج") button.Width = 78;
                else if (action == "حذف السطر") button.Width = 96;
                else if (action == "بحث") button.Width = 72;
                else if (action == "السطر السابق" || action == "السطر التالي") button.Width = 44;
            }
            header.ResumeLayout(true);
            lineFields.ResumeLayout(true);
            primary.ResumeLayout(true);
            secondary.ResumeLayout(true);
            ApplyJournalToolbarTheme();
            if (account.Parent is AntdUI.Panel) DesktopComponents.StyleNativeSelectFrame((AntdUI.Panel)account.Parent, account, RightToLeft == RightToLeft.No);
            ApplyJournalGridPresentation();
            FitJournalStatus();
            FitJournalEditorViewport();
        }

        private void FitJournalEditorViewport()
        {
            if (journalViewportSizing || IsDisposed || journalEditorViewport.Parent == null) return;
            var header = Controls.Find("journalHeaderFields", true).OfType<TableLayoutPanel>().FirstOrDefault();
            var rows = Controls.Find("journalLineFields", true).OfType<TableLayoutPanel>().FirstOrDefault();
            var title = Controls.Find("journalTitleBar", true).FirstOrDefault();
            var toolbar = Controls.Find("journalToolbar", true).FirstOrDefault();
            var footer = Controls.Find("journalFooterInfo", true).FirstOrDefault();
            if (header == null || rows == null || title == null || toolbar == null || footer == null) return;
            journalViewportSizing = true;
            try
            {
                // The viewport is physical LTR; its field grids keep the reading direction.
                // This avoids WinForms RTL AutoScroll suppressing vertical scrolling.
                int fullHeight = header.Height + rows.Height;
                int available = ClientSize.Height - title.Height - toolbar.Height - footer.Height - 128;
                journalEditorViewport.AutoScrollMinSize = new Size(0, fullHeight);
                journalEditorViewport.Height = Math.Min(fullHeight, Math.Max(80, available));
            }
            finally { journalViewportSizing = false; }
        }
        private void ConfigureCompactJournalFields(TableLayoutPanel header, TableLayoutPanel lineFields, AntdUI.Button addLine)
        {
            ConfigureJournalGrid(header, 3, 2, 172, 78, 0);
            ConfigureJournalGrid(lineFields, 4, 4, 302, 78, 10);
            lineFields.RowStyles[3].Height = 52;
            Place(header, branch.Parent.Parent.Parent, 0, 0);
            Place(header, entryNo.Parent.Parent, 1, 0);
            Place(header, entryDate.Parent, 2, 0);
            Place(header, entryStatus.Parent.Parent, 0, 1);
            Place(header, repeatDescription, 1, 1);
            Place(header, headerDescription.Parent.Parent, 2, 1);

            Place(lineFields, account.Parent.Parent, 0, 0, 2);
            Place(lineFields, referenceNo.Parent, 2, 0);
            Place(lineFields, costCenter.Parent.Parent.Parent, 3, 0);
            Place(lineFields, debit.Parent, 0, 1);
            Place(lineFields, credit.Parent, 1, 1);
            Place(lineFields, exchangeRate.Parent, 2, 1);
            Place(lineFields, currency.Parent, 3, 1);
            Place(lineFields, localValue.Parent, 0, 2);
            Place(lineFields, lineDate.Parent, 1, 2);
            Place(lineFields, lineDescription.Parent.Parent, 2, 2, 2);
            if (addLine != null) Place(lineFields, addLine, 0, 3, 4);
        }

        private void ConfigureDesktopJournalFields(TableLayoutPanel header, TableLayoutPanel lineFields, AntdUI.Button addLine)
        {
            ConfigureJournalGrid(header, 6, 2, 130, 78, 14);
            header.RowStyles[1].Height = 36;
            ConfigureJournalGrid(lineFields, 6, 2, 172, 78, 14);
            Place(header, branch.Parent.Parent.Parent, 0, 0);
            Place(header, entryNo.Parent.Parent, 1, 0);
            Place(header, entryDate.Parent, 2, 0);
            Place(header, entryStatus.Parent.Parent, 3, 0);
            Place(header, headerDescription.Parent.Parent, 4, 0, 2);
            Place(header, repeatDescription, 0, 1);

            Place(lineFields, referenceNo.Parent, 5, 0);
            Place(lineFields, costCenter.Parent.Parent.Parent, 4, 0);
            Place(lineFields, credit.Parent, 3, 0);
            Place(lineFields, debit.Parent, 2, 0);
            Place(lineFields, account.Parent.Parent, 0, 0, 2);
            Place(lineFields, addLine, 5, 1);
            Place(lineFields, lineDate.Parent, 4, 1);
            Place(lineFields, localValue.Parent, 3, 1);
            Place(lineFields, exchangeRate.Parent, 2, 1);
            Place(lineFields, currency.Parent, 1, 1);
            Place(lineFields, lineDescription.Parent.Parent, 0, 1);
        }

        private static void ConfigureJournalGrid(TableLayoutPanel panel, int columns, int rows, int height, int rowHeight, int horizontalPadding)
        {
            panel.ColumnCount = columns;
            panel.RowCount = rows;
            panel.Height = height;
            panel.Padding = new Padding(horizontalPadding, 8, horizontalPadding, 8);
            panel.ColumnStyles.Clear();
            panel.RowStyles.Clear();
            for (int column = 0; column < columns; column++) panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / columns));
            for (int row = 0; row < rows; row++) panel.RowStyles.Add(new RowStyle(SizeType.Absolute, rowHeight));
        }

        private static void Place(TableLayoutPanel panel, Control control, int column, int row, int columnSpan = 1)
        {
            if (control == null) return;
            panel.SetCellPosition(control, new TableLayoutPanelCellPosition(column, row));
            panel.SetColumnSpan(control, columnSpan);
        }

        private void PopulatePreviewData()
        {
            account.Items.Add("1000 - الصندوق");
            account.Items.Add("2000 - الموردون");
            account.SelectedIndex = 0;
            currency.Items.Add("RS");
            currency.Items.Add("USD");
            currency.SelectedIndex = 0;
            lines.Rows.Add("1000", "الصندوق", 1250.50m, 0m, 1250.50m, 0m, "RS", 1m, "بدون", DBNull.Value, "REF-01", DateTime.Today.ToString("yyyy/MM/dd"), "إيداع نقدي توضيحي", "حذف");
            lines.Rows.Add("2000", "الموردون", 0m, 1250.50m, 0m, 1250.50m, "RS", 1m, "بدون", DBNull.Value, "REF-01", DateTime.Today.ToString("yyyy/MM/dd"), "إيداع نقدي توضيحي", "حذف");
            UpdateTotals();
            statusLabel.Text = JournalText("بيانات توضيحية للعرض فقط.");
            journalOptionsReady=true;journalEditorCurrent=CaptureJournalEditorContext();
        }

        private void InitializeLineTable()
        {
            lines.Columns.Add("رقم الحساب");
            lines.Columns.Add("الحساب");
            lines.Columns.Add("مدين", typeof(decimal));
            lines.Columns.Add("دائن", typeof(decimal));
            lines.Columns.Add("مدين محلي", typeof(decimal));
            lines.Columns.Add("دائن محلي", typeof(decimal));
            lines.Columns.Add("العملة");
            lines.Columns.Add("سعر التحويل", typeof(decimal));
            lines.Columns.Add("مركز التكلفة");
            lines.Columns.Add("CostCenterId", typeof(int));
            lines.Columns.Add("رقم المرجع");
            lines.Columns.Add("التاريخ");
            lines.Columns.Add("البيان");
            lines.Columns.Add("Action");
        }

        private Control BuildJournalToolbar()
        {
            Panel toolbar = new Panel { Name = "journalToolbar", Dock = DockStyle.Bottom, Height = 72,
                Tag = "UiSurface", BackColor = UiTheme.Surface };
            FlowLayoutPanel right = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                Width = 430,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(6, 10, 8, 8),
                BackColor = Color.Transparent
            };
            right.Name = "journalPrimaryActions";
            right.Controls.Add(CreateToolbarButton("جديد", AppIcon.Add, NewEntry, 116, UiTheme.Primary, "إنشاء قيد جديد"));
            right.Controls.Add(CreateToolbarButton("حفظ", AppIcon.Save, async () => { await SaveEntryAsync(false); }, 120, UiTheme.Primary, "حفظ القيد"));
            right.Controls.Add(CreateToolbarButton("حفظ وطباعة", AppIcon.Print, async () => { await SaveEntryAsync(true); }, 150, UiTheme.Secondary, "حفظ القيد وطباعته"));

            FlowLayoutPanel left = new FlowLayoutPanel
            {
                Name = "journalSecondaryActions",
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(8, 10, 6, 8),
                BackColor = Color.Transparent
            };
            left.Controls.Add(CreateToolbarButton("خروج", AppIcon.Close, () => Close(), 104, UiTheme.Danger));
            left.Controls.Add(CreateToolbarButton("حذف السطر", AppIcon.Delete, RemoveSelectedLine, 96, UiTheme.Primary));
            left.Controls.Add(CreateToolbarButton("بحث", AppIcon.Search, () => linesGrid.Focus(), 88, UiTheme.Primary));
            left.Controls.Add(CreateToolbarButton(string.Empty, AppIcon.Back, () => MoveSelection(true, true), 44, UiTheme.PrimaryDark, "السطر الأول"));
            left.Controls.Add(CreateToolbarButton(string.Empty, AppIcon.Back, () => MoveSelection(false, true), 44, UiTheme.PrimaryDark, "السطر السابق"));
            left.Controls.Add(CreateToolbarButton(string.Empty, AppIcon.Forward, () => MoveSelection(false, false), 44, UiTheme.PrimaryDark, "السطر التالي"));
            left.Controls.Add(CreateToolbarButton(string.Empty, AppIcon.Forward, () => MoveSelection(true, false), 44, UiTheme.PrimaryDark, "السطر الأخير"));
            left.Controls.Add(CreateToolbarButton("جديد", AppIcon.Refresh, NewEntry, 72, UiTheme.PrimaryDark, "بدء قيد جديد"));

            toolbar.Controls.Add(left);
            toolbar.Controls.Add(right);
            return toolbar;
        }

        private void ApplyJournalToolbarTheme()
        {
            Panel toolbar = Controls.Find("journalToolbar", true).OfType<Panel>().FirstOrDefault();
            if (toolbar == null) return;
            toolbar.BackColor = UiTheme.Surface;
            foreach (FlowLayoutPanel group in toolbar.Controls.OfType<FlowLayoutPanel>())
            {
                group.BackColor = UiTheme.Surface;
                foreach (AntdUI.Button button in group.Controls.OfType<AntdUI.Button>())
                DesktopComponents.StyleOutlinedFooterAction(button);
            }
        }

        private AntdUI.Button CreateToolbarButton(string text, AppIcon icon, Action action, int width, Color backColor, string accessibleName = null)
        {
            string semanticName = string.IsNullOrWhiteSpace(accessibleName) ? text : accessibleName;
            AntdUI.Button button = DesktopComponents.Action("journalAction" + (++journalActionId), text, icon, backColor == UiTheme.Primary);
            button.Width = width;
            button.Height = 46;
            button.Margin = new Padding(3);
            button.Font = AppFonts.Create(9.5F, FontStyle.Bold);
            button.UseMnemonic = false;
            button.AccessibleName = semanticName;
            button.AccessibleDescription = "إجراء القيد اليومي";
            AntdThemeAdapter.Apply(button);
            button.Click += (s, e) => action();
            return button;
        }

        internal async System.Threading.Tasks.Task<bool> LoadInitialLookupsAsync(Func<System.Threading.CancellationToken,System.Threading.Tasks.Task<JournalEntryLookupSnapshot>> read=null)
        {
            if(journalEditorCurrent!=null&&!JournalContextCurrent(journalEditorCurrent))InvalidateJournalContext();
            CancelJournalLookup();int generation=++lookupLoadGeneration;
            var cancellation=new System.Threading.CancellationTokenSource();journalLookupCancellation=cancellation;
            Func<bool> current=null;
            bool retained=journalOptionsReady;
            string oldAccount=account.Text,oldCurrency=currency.Text;
            string oldCenter=costCenter.Text;
            account.Enabled=currency.Enabled=false;
            SetJournalStatus("جار تحميل الحسابات والعملات...");
            try
            {
                current=CaptureJournalEditorContext();if(!JournalContextCurrent(current)){InvalidateJournalContext();return false;}
                JournalEntryLookupSnapshot snapshot=await(read!=null?read(cancellation.Token):JournalEntryLookupService.LoadAsync(cancellation.Token));
                if(IsDisposed||generation!=lookupLoadGeneration||cancellation.IsCancellationRequested)return false;
                if(!JournalContextCurrent(current)){InvalidateJournalContext();return false;}
                if(snapshot==null)throw new InvalidOperationException("لم ترجع خيارات القيد بيانات صالحة.");
                account.Items.Clear();foreach(string item in snapshot.Accounts)account.Items.Add(item);
                account.SelectedIndex=retained?account.Items.IndexOf(oldAccount):account.Items.Count>0?0:-1;
                if(account.SelectedIndex<0)account.Text=string.Empty;
                currency.SelectedIndex=-1;currency.Items.Clear();foreach(string item in snapshot.Currencies)if(!currency.Items.Contains(item))currency.Items.Add(item);
                if(string.IsNullOrWhiteSpace(snapshot.SystemCurrencyCode)||!currency.Items.Contains(snapshot.SystemCurrencyCode))throw new PharmacyManagement.Application.CurrencySnapshotUnavailableException();
                currency.SelectedIndex=retained?currency.Items.IndexOf(oldCurrency):currency.Items.IndexOf(snapshot.SystemCurrencyCode);
                if(!retained&&currency.SelectedIndex<0)throw new PharmacyManagement.Application.CurrencySnapshotUnavailableException();
                if(currency.SelectedIndex<0)currency.Text=string.Empty;
                costCenter.SelectedIndex=-1;costCenter.Items.Clear();costCenter.Items.Add("بدون");foreach(string center in snapshot.CostCenters)costCenter.Items.Add(center);
                costCenter.SelectedIndex=retained?costCenter.Items.IndexOf(oldCenter):0;if(costCenter.SelectedIndex<0)costCenter.Text=string.Empty;
                if(!retained){journalSystemCurrencyCode=snapshot.SystemCurrencyCode.Trim().ToUpperInvariant();ResetJournalDraft();}
                journalOptionsReady=true;journalEditorCurrent=current;
                account.Enabled=currency.Enabled=journalEditorViewport.Enabled=linesGrid.Enabled=true;
                SetJournalStatus("تم تحميل خيارات القيد.");if(!previewMode&&read==null&&!InitializeJournalRecovery())return false;ApplyJournalRecoveryLock();return true;
            }
            catch(OperationCanceledException){return false;}
            catch(Exception error)
            {
                if(IsDisposed||generation!=lookupLoadGeneration)return false;
                if(error is PharmacyManagement.Application.CurrencySnapshotUnavailableException){InvalidateJournalContext();SetJournalStatus(error.Message);}
                else if(!JournalContextCurrent(current))InvalidateJournalContext();
                else
                {
                    account.Enabled=currency.Enabled=retained;
                    SetJournalStatus("تعذر تحميل خيارات القيد. اختر جديد لإعادة المحاولة؛ لا تستخدم خيارات جلسة سابقة.");
                    if(!previewMode)statusLabel.AccessibleDescription+=" رمز الدعم: "+AppDiagnostics.Log("JournalEditorLookups",error);
                }
                return false;
            }
            finally{if(ReferenceEquals(journalLookupCancellation,cancellation))journalLookupCancellation=null;cancellation.Dispose();}
        }
        private string journalSystemCurrencyCode;
        private void NewEntry()
        {
            if(journalWriteBusy)return;
            if(!EnsureJournalEditor()){ReloadJournalEditor();return;}
            if(journalRecovery!=null&&journalRecovery.AcceptedId>0)journalRecovery.RetryAcceptedCleanup();
            if(HasPendingJournal())return;
            ResetJournalDraft();ApplyJournalRecoveryLock();
        }
        private void ResetJournalDraft()
        {
            if(HasPendingJournal())return;
            lines.Rows.Clear();
            entryNo.Text = GetNextJournalEntryNumberText();
            entryDate.Value = DateTime.Today;
            attachmentNo.Clear();
            headerDescription.Clear();
            referenceNo.Clear();
            debit.Clear();
            credit.Clear();
            lineDescription.Clear();
            exchangeRate.Text = "1";
            lineDate.Text = DateTime.Today.ToString("yyyy/MM/dd");
            UpdateTotals();
            statusLabel.Text = journalRecovery!=null&&journalRecovery.CanCorrectRejected ? "تم مسح الحقول لتصحيح الطلب المرفوض؛ سيُحفظ التصحيح بالمعرّف نفسه." : "تم تجهيز قيد جديد.";
            journalDraftAccepted = false;
        }

        private string GetNextJournalEntryNumberText()
        {
            return "تلقائي عند الحفظ";
        }

        private void AddLine()
        {
            if(journalWriteBusy||!EnsureJournalEditor()||HasPendingJournal())return;
            decimal debitValue;
            decimal creditValue;
            if (!TryParseJournalAmount(debit, "المدين", out debitValue) || !TryParseJournalAmount(credit, "الدائن", out creditValue))
            {
                return;
            }
            if ((debitValue <= 0 && creditValue <= 0) || (debitValue > 0 && creditValue > 0))
            {
                AppNotifications.Show(JournalText("أدخل مبلغًا في المدين أو الدائن فقط."), Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            string accountCode = SelectedAccount(account);
            if (string.IsNullOrWhiteSpace(accountCode))
            {
                AppNotifications.Show(JournalText("اختر الحساب."), Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            decimal rate;
            if (!decimal.TryParse(exchangeRate.Text.Trim(), out rate) || rate <= 0)
            {
                AppNotifications.Show(JournalText("أدخل سعر تحويل موجبًا وصحيحًا."), Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                exchangeRate.Focus();
                return;
            }
            decimal debitLocal, creditLocal;
            if (!TryCalculateJournalLine(debitValue, creditValue, rate, out debitLocal, out creditLocal)) return;
            string desc = string.IsNullOrWhiteSpace(lineDescription.Text) ? headerDescription.Text : lineDescription.Text;
            int? costCenterId = SelectedCostCenterId();
            lines.Rows.Add(accountCode, account.Text, debitValue, creditValue, debitLocal, creditLocal, currency.Text, rate, costCenter.Text, costCenterId.HasValue ? (object)costCenterId.Value : DBNull.Value, referenceNo.Text, lineDate.Text, desc, "حذف");
            if (!repeatDescription.Checked) lineDescription.Clear();
            debit.Clear();
            credit.Clear();
            localValue.Text = "0";
            UpdateTotals();
        }

        private bool TryParseJournalAmount(Control field, string fieldName, out decimal value)
        {
            string raw = (field.Text ?? string.Empty).Trim();
            if (raw.Length == 0)
            {
                value = 0;
                return true;
            }

            if (!decimal.TryParse(raw, out value) || value < 0)
            {
                AppNotifications.Show(JournalFormat("أدخل قيمة موجبة وصحيحة في حقل {0}.", JournalText(fieldName)), Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                field.Focus();
                return false;
            }

            return true;
        }

        private async System.Threading.Tasks.Task<int> SaveEntryAsync(bool printAfterSave)
        {
            if (journalWriteBusy || !EnsureJournalEditor()) return 0;
            if (journalDraftAccepted) { SetJournalStatus("القيد رقم " + acceptedJournalId + " معتمد؛ اختر جديد بدل إعادة حفظه."); return 0; }
            if (previewMode) { SetJournalStatus("المعاينة للعرض فقط؛ لا يمكن حفظ قيد تجريبي."); return 0; }
            if (!PermissionService.EnsureAllowed(AppOperation.PostAccountingEntry, this)) return 0;
            Func<System.Threading.Tasks.Task<int>> write;
            try
            {
                if(!InitializeJournalRecovery())return 0;
                if(journalRecovery.Pending==null||journalRecovery.CanCorrectRejected)
                {
                DateTime documentDate;
                if (!TryGetJournalDate(out documentDate)) return 0;
                if (lines.Rows.Count < 2)
                { AppNotifications.Show(this, "أضف سطرين على الأقل للقيد.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information); return 0; }
                var values = new List<PharmacyManagement.Domain.JournalLineValues>();
                foreach (DataRow row in lines.Rows)
                    values.Add(new PharmacyManagement.Domain.JournalLineValues(Convert.ToString(row["رقم الحساب"]),
                        Convert.ToDecimal(row["مدين محلي"]), Convert.ToDecimal(row["دائن محلي"]), Convert.ToString(row["البيان"]),
                        row.IsNull("CostCenterId") ? (int?)null : Convert.ToInt32(row["CostCenterId"])));
                    if(journalRecovery.CanCorrectRejected)
                        journalRecovery.CorrectRejected(PharmacyManagement.Application.JournalPendingRequest.Restore(
                            new PharmacyManagement.Application.JournalPostRequest(documentDate,entrySourceType,journalRecovery.Pending.Request.SourceId,headerDescription.Text,values,journalSystemCurrencyCode)));
                    else journalRecovery.Prepare(PharmacyManagement.Application.JournalPendingRequest.Create(documentDate,entrySourceType,headerDescription.Text,values,journalSystemCurrencyCode));
                }
                ApplyJournalRecoveryLock();
                var recovery=journalRecovery;
                write = () => ExecuteJournalRecoveryAsync(recovery);
            }
            catch (Exception error) { UiTheme.ShowOperationIssue(this, "التحقق من القيد", error); return 0; }
            return await RunJournalWriteAsync(write, printAfterSave);
        }

        private void ScheduleJournalStatusLayout()
        {
            if (journalStatusLayoutPending || IsDisposed || !IsHandleCreated) return;
            journalStatusLayoutPending = true;
            BeginInvoke((Action)(() =>
            {
                journalStatusLayoutPending = false;
                if (IsDisposed || statusLabel.Parent == null) return;
                FitJournalStatus();
                statusLabel.Parent.PerformLayout();
            }));
        }
        private void FitJournalStatus()
        {
            if (journalStatusSizing || IsDisposed || statusLabel.Parent == null || statusLabel.Parent.ClientSize.Width <= 0) return;
            journalStatusSizing = true;
            try
            {
                int width = statusLabel.Parent.ClientSize.Width;
                bool leftToRight = RightToLeft == RightToLeft.No;
                statusLabel.TextAlign = totalsLabel.TextAlign = leftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
                int totalsWidth = TextRenderer.MeasureText(totalsLabel.Text, totalsLabel.Font, Size.Empty, TextFormatFlags.NoPrefix).Width + 16;
                bool stacked = width < 1120 || totalsWidth > width - 240;
                totalsLabel.Dock = stacked ? DockStyle.Top : (leftToRight ? DockStyle.Left : DockStyle.Right);
                totalsLabel.Width = stacked ? width : totalsWidth;
                int totalsHeight = TextRenderer.MeasureText(totalsLabel.Text, totalsLabel.Font,
                    new Size(Math.Max(1, (stacked ? width : totalsWidth) - totalsLabel.Padding.Horizontal), int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height + 8;
                int statusHeight = TextRenderer.MeasureText(statusLabel.Text, statusLabel.Font,
                    new Size(Math.Max(1, (stacked ? width : width - totalsWidth) - statusLabel.Padding.Horizontal), int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height + 8;
                totalsLabel.Height = Math.Max(28, totalsHeight);
                statusLabel.Parent.Height = stacked ? totalsLabel.Height + Math.Max(28, statusHeight) : Math.Max(28, Math.Max(totalsHeight, statusHeight));
            }
            finally { journalStatusSizing = false; }
        }

        private void ApplyJournalGridPresentation()
        {
            if (linesGrid.Columns.Contains("CostCenterId")) linesGrid.Columns["CostCenterId"].Visible = false;
            if (linesGrid.Columns.Contains("Action")) linesGrid.Columns["Action"].HeaderText = UiDirection.IsLeftToRight ? "Action" : "الإجراء";
        }
        private void SetJournalStatus(string message)
        { if (!IsDisposed && !Disposing) { statusLabel.Text = message; statusLabel.AccessibleDescription = message; } }

        internal async System.Threading.Tasks.Task<int> RunJournalWriteAsync(Func<System.Threading.Tasks.Task<int>> write,
            bool printAfterSave, Func<int, System.Threading.Tasks.Task<PharmacyManagement.Application.JournalDocumentSnapshot>> read = null,
            Action clearEditor = null, Action<PharmacyManagement.Application.JournalDocumentSnapshot> print = null,
            Func<int,System.Threading.CancellationToken,System.Threading.Tasks.Task<PharmacyManagement.Application.JournalDocumentSnapshot>> cancellableRead = null)
        {
            if (write == null) throw new ArgumentNullException(nameof(write));
            if (journalWriteBusy || journalDraftAccepted || !EnsureJournalEditor()) return 0;
            var writeContext=journalEditorCurrent;CancelJournalLookup();
            System.Threading.CancellationTokenSource printCancellation=null;
            var states = Controls.Cast<Control>().Where(control => control.Name != "journalTitleBar" && control.Name != "journalFooterInfo" && control != statusLabel)
                .ToDictionary(control => control, control => control.Enabled);
            journalWriteBusy = true;
            try
            {
                // Disabling controls and changing status can run UI event handlers.
                // Keep preparation inside the lifetime cleanup and recheck before dispatch.
                foreach (var control in states.Keys)
                {
                    if (IsDisposed || Disposing) return 0;
                    control.Enabled = false;
                }
                SetJournalStatus("جار حفظ القيد؛ انتظر اكتمال العملية.");
                if (IsDisposed || Disposing) return 0;
                if (!JournalContextCurrent(writeContext)) { InvalidateJournalContext(); return 0; }
                int id;
                try
                {
                    id = await write();
                    if (id <= 0) throw new InvalidOperationException("لم ترجع عملية الحفظ رقم قيد صالحًا.");
                }
                catch (Exception error)
                {
                    if(!JournalContextCurrent(writeContext)){InvalidateJournalContext();return 0;}
                    SetJournalStatus(journalRecovery!=null&&journalRecovery.CanCorrectRejected ? "رفضت المحاولة وأُكد التراجع. صحح المسودة ثم احفظ بالمعرّف نفسه؛ لا تنشئ قيدًا بديلًا للطلب.": journalRecovery!=null&&journalRecovery.Pending!=null ? "لم يتأكد اعتماد القيد. احتُفظ بالطلب دون تغيير؛ اختر حفظ للتحقق وإعادة الطلب نفسه، أو أغلق لاستعادته لاحقًا." : "تعذر حفظ القيد؛ راجع رسالة الخطأ قبل إعادة المحاولة.");
                    if (!previewMode && !IsDisposed && !Disposing) UiTheme.ShowOperationIssue(this, "حفظ القيد", error);
                    return 0;
                }
                if(!JournalContextCurrent(writeContext)){InvalidateJournalContext();return id;}
                acceptedJournalId = id; journalDraftAccepted = true;
                bool differentAcceptedPayload=journalRecovery!=null&&!journalRecovery.AcceptedContentMatchesRequest;
                string message = differentAcceptedPayload ? "القيد رقم " + id + " معتمد سابقًا ببيانات مختلفة؛ لم تعتمد التعديلات الحالية. راجع القيد المحفوظ، واختر جديد عند الحاجة." : "تم حفظ القيد رقم " + id + ".";
                if(journalRecovery!=null&&journalRecovery.CleanupError!=null){message+=" تعذر تنظيف سجل الاستعادة المحلي؛ القيد معتمد. اختر جديد لإعادة التنظيف دون إعادة الترحيل.";if(!previewMode)AppDiagnostics.Log("JournalRecoveryCleanup",journalRecovery.CleanupError);}
                if (IsDisposed || Disposing) return id;
                try { if(!differentAcceptedPayload){if (clearEditor != null) clearEditor(); else ResetJournalDraft();} journalDraftAccepted = differentAcceptedPayload||(journalRecovery!=null&&journalRecovery.Pending!=null&&journalRecovery.AcceptedId>0); }
                catch (Exception error)
                {
                    message += " تعذر تجهيز قيد جديد؛ لا تعِد الحفظ. اختر جديد للمحاولة.";
                    if (!previewMode) AppDiagnostics.Log("JournalAcceptedEditor", error);
                }
                if (IsDisposed || Disposing) return id;
                if (printAfterSave)
                {
                    if(!JournalContextCurrent(writeContext)){InvalidateJournalContext();return id;}
                    try
                    {
                        printCancellation=new System.Threading.CancellationTokenSource();journalPrintCancellation=printCancellation;journalPrintPending=true;
                        var printToken=printCancellation.Token;
                        var document = await (cancellableRead!=null?cancellableRead(id,printToken):read == null ? JournalDocumentDesktopService.ReadAsync(id,printToken) : read(id));
                        printToken.ThrowIfCancellationRequested();
                        if (document == null || document.Id != id) throw new InvalidOperationException("القيد المقروء لا يطابق القيد المقبول.");
                        if (IsDisposed || Disposing) return id;
                        if(!JournalContextCurrent(writeContext)){InvalidateJournalContext();return id;}
                        if (print != null) print(document);
                        else if (!previewMode)
                        {
                            bool printLeftToRight=UiDirection.IsLeftToRight;
                            using (var rows = JournalDocumentDesktopService.PrintLines(document,printLeftToRight))
                                DataTablePrintPreview.Show(this, JournalDocumentDesktopService.PrintTitle(document,printLeftToRight),
                                    JournalDocumentDesktopService.PrintSubtitle(document,printLeftToRight), rows, null, printLeftToRight, isCurrent:CreateJournalPrintContext(writeContext));
                        }
                        message += " انتهت خطوة معاينة الطباعة.";
                    }
                    catch(OperationCanceledException)
                    {message+=" أُلغيت قراءة المعاينة؛ القيد محفوظ ولا يحتاج إعادة حفظ.";}
                    catch (Exception error)
                    {
                        message += " تعذر فتح معاينة القيد المحفوظ؛ لا تعِد الحفظ.";
                        if (!previewMode) AppDiagnostics.Log("JournalAcceptedPrint", error);
                    }
                }
                if(!JournalContextCurrent(writeContext)){InvalidateJournalContext();return id;}
                SetJournalStatus(message);
                return id;
            }
            finally
            {
                journalPrintPending=false;if(ReferenceEquals(journalPrintCancellation,printCancellation))journalPrintCancellation=null;printCancellation?.Dispose();
                journalWriteBusy = false;
                if (!IsDisposed && !Disposing) foreach (var state in states) if (!state.Key.IsDisposed) state.Key.Enabled = state.Value;
                if(!IsDisposed&&!Disposing&&!JournalContextCurrent(writeContext))InvalidateJournalContext();else ApplyJournalRecoveryLock();
            }
        }

        private bool TryGetJournalDate(out DateTime documentDate)
        {
            if (!AntdDateInput.TryRead(entryDate, out documentDate))
            {
                statusLabel.Text = JournalText("اختر تاريخ قيد صحيحًا قبل الحفظ.");
                entryDate.Focus();
                return false;
            }
            return true;
        }

        private void RemoveSelectedLine()
        {
            if(journalWriteBusy||!EnsureJournalEditor()||HasPendingJournal())return;
            if (linesGrid.CurrentRow == null || linesGrid.CurrentRow.Index < 0 || linesGrid.CurrentRow.Index >= lines.Rows.Count) return;
            lines.Rows.RemoveAt(linesGrid.CurrentRow.Index);
            UpdateTotals();
        }

        private void MoveSelection(bool edge, bool backwards)
        {
            if(journalWriteBusy||!EnsureJournalEditor())return;
            if (linesGrid.Rows.Count == 0) return;
            int index = linesGrid.CurrentCell == null ? 0 : linesGrid.CurrentCell.RowIndex;
            index = edge ? (backwards ? 0 : linesGrid.Rows.Count - 1) : Math.Max(0, Math.Min(linesGrid.Rows.Count - 1, index + (backwards ? -1 : 1)));
            linesGrid.CurrentCell = linesGrid.Rows[index].Cells[0];
            linesGrid.Rows[index].Selected = true;
        }

        internal bool TryCalculateJournalLine(decimal debitValue, decimal creditValue, decimal rate,
            out decimal debitLocal, out decimal creditLocal)
        {
            debitLocal = creditLocal = 0m;
            try
            {
                decimal convertedDebit = PharmacyManagement.Domain.JournalAmountCalculator.ToLocal(debitValue, rate);
                decimal convertedCredit = PharmacyManagement.Domain.JournalAmountCalculator.ToLocal(creditValue, rate);
                PharmacyManagement.Domain.JournalEntryPreparation.ValidateLineAmounts(convertedDebit, convertedCredit);
                PharmacyManagement.Domain.JournalAmountCalculator.Sum(JournalAmountLines().Concat(new[]
                {
                    new PharmacyManagement.Domain.JournalLineValues(null, convertedDebit, convertedCredit, null, null)
                }));
                debitLocal = convertedDebit;
                creditLocal = convertedCredit;
                return true;
            }
            catch (PharmacyManagement.Domain.JournalAmountException error)
            {
                SetJournalStatus(error.Message);
                exchangeRate.Focus();
                return false;
            }
        }

        private IEnumerable<PharmacyManagement.Domain.JournalLineValues> JournalAmountLines()
        {
            return lines.Rows.Cast<DataRow>().Select(row => new PharmacyManagement.Domain.JournalLineValues(
                Convert.ToString(row["رقم الحساب"]), Convert.ToDecimal(row["مدين محلي"]),
                Convert.ToDecimal(row["دائن محلي"]), Convert.ToString(row["البيان"]), null));
        }

        private void UpdateLocalValue()
        {
            decimal debitValue, creditValue, rate;
            string debitText = (debit.Text ?? string.Empty).Trim();
            string creditText = (credit.Text ?? string.Empty).Trim();
            if (!decimal.TryParse(debitText.Length == 0 ? "0" : debitText, out debitValue) ||
                !decimal.TryParse(creditText.Length == 0 ? "0" : creditText, out creditValue) ||
                !decimal.TryParse(exchangeRate.Text, out rate))
            {
                localValue.Text = "—";
                localValue.AccessibleDescription = "أدخل مبلغًا وسعر تحويل صحيحين لعرض القيمة المحلية.";
                return;
            }
            try
            {
                localValue.Text = PharmacyManagement.Domain.JournalAmountCalculator.Preview(debitValue, creditValue, rate).ToString("0.##");
                localValue.AccessibleDescription = "القيمة المحلية بنفس تقريب سطر القيد.";
            }
            catch (PharmacyManagement.Domain.JournalAmountException error)
            {
                localValue.Text = "—";
                localValue.AccessibleDescription = error.Message;
            }
        }

        private void UpdateTotals()
        {
            try
            {
                var totals = PharmacyManagement.Domain.JournalAmountCalculator.Sum(JournalAmountLines());
                totalsLabel.Text = JournalFormat("إجمالي المدين: {0:N2}    إجمالي الدائن: {1:N2}    الفرق: {2:N2}", totals.Debit, totals.Credit, totals.Difference);
            }
            catch (PharmacyManagement.Domain.JournalAmountException error)
            {
                totalsLabel.Text = JournalText("تعذر حساب الإجمالي؛ راجع مبالغ السطور.");
                SetJournalStatus(error.Message);
            }
        }
        private string SelectedAccount(Control combo)
        {
            string value = combo.Text;
            int dash = value.IndexOf(" - ", StringComparison.Ordinal);
            return dash > 0 ? value.Substring(0, dash).Trim() : value.Trim();
        }

        private int? SelectedCostCenterId()
        {
            string value = costCenter.Text;
            int dash = value.IndexOf(" - ", StringComparison.Ordinal);
            int id;
            if (dash > 0 && int.TryParse(value.Substring(0, dash), out id))
            {
                return id;
            }

            return null;
        }

        private void AddCell(TableLayoutPanel panel, string label, Control control, int col, int row)
        {
            panel.Controls.Add(CreateFieldBox(label, control), col, row);
        }

        private void AddWideCell(TableLayoutPanel panel, string label, Control control, int col, int row, int span)
        {
            Panel box = CreateFieldBox(label, control);
            panel.Controls.Add(box, col, row);
            panel.SetColumnSpan(box, span);
        }

        private Panel CreateFieldBox(string label, Control control)
        {
            Panel panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(4, 2, 4, 2), Tag = "UiSurface" };
            Label caption = new Label
            {
                Text = label,
                Dock = DockStyle.Top,
                Height = 22,
                TextAlign = ContentAlignment.MiddleRight,
                ForeColor = UiTheme.PrimaryDark,
                Font = AppFonts.Create(8.5F, FontStyle.Bold)
            };
            // The caption and editor need separate vertical slots. DockStyle.Fill
            // placed the editor behind the caption at both compact and desktop widths.
            TextBox editor = control as TextBox;
            if (editor != null) control = DesktopComponents.NativeTextFrame(editor, editor.Name + "Frame");
            ComboBox selector = control as ComboBox;
            if (selector != null)
                control = DesktopComponents.NativeSelectFrame(selector, selector.Name + "Frame", selector.AccessibleName, !ReferenceEquals(selector, account));
            control.Dock = DockStyle.Bottom;
            control.Height = DesktopDesignTokens.InputHeight;
            control.Font = AppFonts.Create(10F, FontStyle.Regular);
            panel.Controls.Add(control);
            panel.Controls.Add(caption);
            (selector == null ? control : (Control)selector).Enter += (sender, args) => journalEditorViewport.ScrollControlIntoView(panel);
            return panel;
        }
    }

    public partial class JournalEntriesQuickForm : AccountingDataForm
    {
        private readonly AntdUI.DatePicker fromDate = DesktopComponents.DatePicker("quickJournalFromDate", "من تاريخ", DateTime.Today);
        private readonly AntdUI.DatePicker toDate = DesktopComponents.DatePicker("quickJournalToDate", "إلى تاريخ", DateTime.Today);
        private readonly AntdUI.Panel quickActions = new AntdUI.Panel { Name = "quickJournalActions", Dock = DockStyle.Bottom, Height = 112, Radius = 0, Shadow = 0 };
        private readonly Label quickStatus = new Label { Name = "quickJournalStatus", Font = AppFonts.Create(10F), RightToLeft = RightToLeft.No };
        private readonly AntdUI.Select source = DesktopComponents.Select("quickJournalSource", "مصدر القيد");
        private readonly ComboBox debitAccount = new NativeThemedSelect { Name = "quickJournalDebitAccount", AccessibleName = "الحساب المدين", Font = AppFonts.Create(10F) };
        private readonly ComboBox creditAccount = new NativeThemedSelect { Name = "quickJournalCreditAccount", AccessibleName = "الحساب الدائن", Font = AppFonts.Create(10F) };
        private readonly AntdUI.Input amount = CreateQuickAmountInput();
        private readonly TextBox description = new TextBox { Name = "quickJournalDescription", AccessibleName = "بيان القيد", Font = AppFonts.Create(10F) };
        private int quickLoadGeneration;
        private int quickLookupGeneration;
        private int acceptedQuickJournalId;
        private bool quickWriteBusy;
        private readonly bool previewMode;
        private readonly Func<bool,Func<bool>> quickContextFactory;
        private readonly Action<string> quickCopySelected;
        private Func<bool> quickEditorCurrent,quickHistoryCurrent;
        private bool quickOptionsReady,quickInvalidating;
        private bool quickLeftToRight=UiDirection.IsLeftToRight;
        private bool quickStatusLayout;
        private readonly Dictionary<Label,string> quickFieldCaptions=new Dictionary<Label,string>();
        private string QuickText(string arabic,string english)=>quickLeftToRight?english:arabic;
        internal void ApplyQuickPresentation(bool english)
        {
            quickLeftToRight=english;UiDirection.Apply(this,english);
            Text=QuickText("القيود اليومية السريعة","Quick journal entries");
            var title=Controls.Find("accountingDataTitle",true).OfType<Label>().FirstOrDefault();if(title!=null)title.Text=Text;
            ApplyAccountingClosePresentation(english);
            foreach(var caption in quickFieldCaptions)caption.Key.ForeColor=UiTheme.Text;
            foreach(var separator in Editor.Controls.OfType<Label>())separator.ForeColor=UiTheme.MutedText;
            foreach(var caption in quickFieldCaptions)caption.Key.Text=!english?caption.Value:
                caption.Value=="حساب مدين"?"Debit account":caption.Value=="حساب دائن"?"Credit account":caption.Value=="المبلغ"?"Amount":UiLanguageCatalog.Translate(caption.Value);
            var save=quickActions.Controls["quickJournalSave"] as AntdUI.Button;
            var refresh=quickActions.Controls["quickJournalRefresh"] as AntdUI.Button;
            var create=quickActions.Controls["quickJournalNew"] as AntdUI.Button;
            if(create!=null){create.Text=QuickText("جديد","New");create.AccessibleName=create.Text;}
            if(save!=null){save.Text=QuickText("حفظ القيد","Save entry");save.AccessibleName=save.Text;}
            if(refresh!=null){refresh.Text=QuickText("تحديث","Refresh");refresh.AccessibleName=refresh.Text;}
            foreach(var separator in Editor.Controls.OfType<Label>().Where(label=>label.Text=="إضافة قيد يدوي سريع"||label.Text=="Add a quick manual entry"))separator.Text=QuickText("إضافة قيد يدوي سريع","Add a quick manual entry");
            ApplyQuickHistoryHeadings();
            source.RightToLeft=RightToLeft.No;source.TextAlign=HorizontalAlignment.Left;
            UiDirection.ApplyContentDirection(description,english);
            quickStatus.TextAlign=english?ContentAlignment.MiddleLeft:ContentAlignment.MiddleRight;
            LayoutQuickJournalActions();
        }

        private System.Threading.CancellationTokenSource quickHistoryCancellation,quickLookupCancellation;
        private PharmacyManagement.Application.JournalHistoryReadRequest quickAcceptedFilters;

        private Func<bool> CaptureQuickContext(bool history)
        {
            if(quickContextFactory!=null)return quickContextFactory(history);
            if(previewMode)return ()=>true;
            var session=ApplicationServices.CreateAccountingReadSession();
            return history?(Func<bool>)(()=>session.IsHistoryCurrent):()=>session.IsLookupCurrent;
        }
        private bool IsQuickCurrent(Func<bool> current)=>current!=null&&current();
        private void CancelQuickReads(bool lookups)
        {
            var history=quickHistoryCancellation;if(history!=null&&!history.IsCancellationRequested)history.Cancel();
            if(lookups){var options=quickLookupCancellation;if(options!=null&&!options.IsCancellationRequested)options.Cancel();}
        }
        private void InvalidateQuickContext()
        {
            if(quickInvalidating||IsDisposed)return;
            quickInvalidating=true;
            try
            {
                ++quickLoadGeneration;++quickLookupGeneration;CancelQuickReads(true);
                ReleaseQuickRecovery();quickAcceptedReview=false;quickOptionsReady=false;quickEditorCurrent=quickHistoryCurrent=null;quickAcceptedFilters=null;acceptedQuickJournalId=0;
                var previous=Grid.DataSource as DataTable;Grid.DataSource=null;previous?.Dispose();
                debitAccount.Items.Clear();creditAccount.Items.Clear();debitAccount.Text=creditAccount.Text=string.Empty;
                amount.Text=string.Empty;description.Clear();
                debitAccount.Enabled=creditAccount.Enabled=amount.Enabled=description.Enabled=false;
                var save=quickActions.Controls["quickJournalSave"];if(save!=null)save.Enabled=false;
                SetQuickJournalStatus(QuickText("تغيرت جلسة القيود أو صلاحيتها. حدّث الحسابات والسجل قبل المتابعة.","The journal session or permission changed. Refresh accounts and history before continuing."));
            }
            finally{quickInvalidating=false;}
        }
        private bool EnsureQuickEditor()
        {
            if(IsDisposed||quickInvalidating)return false;
            if(quickOptionsReady&&IsQuickCurrent(quickEditorCurrent))return true;
            InvalidateQuickContext();return false;
        }
        internal string SelectedQuickHistoryText()
        {
            if(IsDisposed||quickWriteBusy)return null;
            if(!IsQuickCurrent(quickHistoryCurrent)){InvalidateQuickContext();return null;}
            DateTime from,to;
            if(quickAcceptedFilters==null||!AntdDateInput.TryRead(fromDate,out from)||!AntdDateInput.TryRead(toDate,out to)||
                from.Date!=quickAcceptedFilters.From.Date||to.Date!=quickAcceptedFilters.To.Date||(source.Text??string.Empty).Trim()!=quickAcceptedFilters.SourceType)return null;
            var row=Grid.CurrentRow;if(row==null||row.IsNewRow)return null;
            return string.Join("\t",Grid.Columns.Cast<DataGridViewColumn>().OrderBy(c=>c.Index).Select(c=>{
                var value=row.Cells[c.Index].Value;
                return value==null||value==DBNull.Value?string.Empty:value is DateTime?((DateTime)value).ToString("yyyy-MM-dd HH:mm:ss",System.Globalization.CultureInfo.InvariantCulture):Convert.ToString(value,System.Globalization.CultureInfo.InvariantCulture);
            }));
        }
        private void QuickFiltersChanged()
        {
            if(IsDisposed||quickInvalidating)return;
            ++quickLoadGeneration;CancelQuickReads(false);
            if(!quickWriteBusy)SetQuickJournalStatus(QuickText("تغيرت مرشحات السجل. اضغط تحديث؛ العرض السابق يخص آخر قراءة.","History filters changed. Refresh; the displayed rows belong to the previous read."));
        }
        private void QuickHistoryKeyDown(object sender,KeyEventArgs e)
        {
            if(!e.Control||e.KeyCode!=Keys.C)return;
            e.Handled=true;e.SuppressKeyPress=true;
            string text=SelectedQuickHistoryText();if(text!=null)quickCopySelected(text);
        }
        private async void ReloadQuickWorkspace()
        {if(await LoadQuickJournalAccountsAsync())await RefreshQuickJournalHistoryAsync();}


        private static AntdUI.Input CreateQuickAmountInput()
        {
            AntdUI.Input input = DesktopComponents.Input("quickJournalAmount", "المبلغ");
            input.RightToLeft = RightToLeft.No;
            input.TextAlign = HorizontalAlignment.Right;
            return input;
        }

        public JournalEntriesQuickForm() : this(false)
        {
        }

        internal JournalEntriesQuickForm(bool previewMode, Func<bool,Func<bool>> contextFactory = null, Action<string> copySelected = null) : base("القيود اليومية")
        {
            this.previewMode = previewMode;
            quickContextFactory=contextFactory;quickCopySelected=copySelected??System.Windows.Forms.Clipboard.SetText;
            description.TextChanged += (sender, args) => UiDirection.ApplyContentDirection(description, quickLeftToRight);
            fromDate.MinDate = toDate.MinDate = new DateTime(1753, 1, 1);
            fromDate.MaxDate = toDate.MaxDate = DateTimePicker.MaximumDateTime;
            if (previewMode)
            {
                Control runtimeStatus = Controls["__UiStatusStrip"];
                if (runtimeStatus != null) { Controls.Remove(runtimeStatus); runtimeStatus.Dispose(); }
            }
            MinimumSize = new Size(760, 600);
            Size = new Size(1120, 700);
            fromDate.Value = DateTime.Today.AddMonths(-1);
            toDate.Value = DateTime.Today;
            source.Items.Add("");
            source.Items.Add("Manual");
            source.Items.Add("SalesInvoice");
            source.Items.Add("PurchaseInvoice");
            source.Items.Add("OpeningEntry");
            source.Items.Add("Voucher");
            source.SelectedIndex = 0;
            source.RightToLeft = RightToLeft.No;
            source.TextAlign = HorizontalAlignment.Left;
            BuildEditor();
            foreach(var caption in Editor.Controls.OfType<Panel>().SelectMany(field=>field.Controls.OfType<Label>()))quickFieldCaptions[caption]=caption.Text;
            quickActions.BackColor = UiTheme.Surface;
            quickStatus.ForeColor = UiTheme.Text; quickStatus.BackColor = UiTheme.Surface;
            quickStatus.TextAlign = UiDirection.IsLeftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
            quickStatus.Text = QuickText("اختر نطاق السجل، أو أدخل قيدًا يدويًا متوازنًا.","Choose a history range or enter a balanced manual entry.");
            quickActions.Controls.Add(quickStatus);
            AttachAccountingCloseAction(quickActions);
            quickActions.Resize += (sender, args) => LayoutQuickJournalActions();
            Controls.Add(quickActions);
            quickActions.SendToBack();
            LayoutQuickJournalActions();
            debitAccount.Name = "quickJournalDebitAccount";
            creditAccount.Name = "quickJournalCreditAccount";
            debitAccount.Enabled = false;
            creditAccount.Enabled = false;
            Resize += (sender, args) => ApplyQuickJournalLayout();
            if (previewMode) PopulatePreviewData();
            else Load += async (sender, args) => await LoadInitialDataAsync();
            FormClosing += (sender, args) =>
            {
                if (quickWriteBusy && args.CloseReason == CloseReason.UserClosing && !(quickRecovery != null && (quickRecovery.Pending != null || quickRecovery.AcceptedId > 0)))
                { args.Cancel = true; SetQuickJournalStatus(QuickText("انتظر اكتمال حفظ القيد قبل إغلاق النافذة.","Wait for the journal entry to finish before closing.")); }
            };
            FormClosed += (sender, args) => { quickLoadGeneration++; quickLookupGeneration++; };
            FormClosing += (sender,args)=>{if(!args.Cancel){++quickLoadGeneration;++quickLookupGeneration;CancelQuickReads(true);}};
            Disposed += (sender,args)=>{++quickLoadGeneration;++quickLookupGeneration;CancelQuickReads(true);ReleaseQuickRecovery();};
            Activated += (sender,args)=>{if((quickEditorCurrent!=null&&!IsQuickCurrent(quickEditorCurrent))||(quickHistoryCurrent!=null&&!IsQuickCurrent(quickHistoryCurrent)))InvalidateQuickContext();};
            UiSessionSubscription.Attach(this,()=>{if((quickEditorCurrent!=null&&!IsQuickCurrent(quickEditorCurrent))||(quickHistoryCurrent!=null&&!IsQuickCurrent(quickHistoryCurrent)))InvalidateQuickContext();});
            Grid.ClipboardCopyMode=DataGridViewClipboardCopyMode.Disable;Grid.KeyDown+=QuickHistoryKeyDown;
            Grid.DataBindingComplete+=(sender,args)=>ApplyQuickHistoryHeadings();
            fromDate.ValueChanged += (sender,args)=>QuickFiltersChanged();
            toDate.ValueChanged += (sender,args)=>QuickFiltersChanged();
            source.SelectedIndexChanged += (sender,args)=>QuickFiltersChanged();
            quickStatus.TextChanged += (sender,args)=>LayoutQuickJournalActions();
            quickStatus.FontChanged += (sender,args)=>LayoutQuickJournalActions();
            ApplyQuickJournalLayout();ApplyQuickPresentation(quickLeftToRight);
        }

        internal void ApplyQuickJournalLayout()
        {
            if (Editor == null || IsDisposed) return;
            Editor.Width = Math.Max(260, Math.Min(360, ClientSize.Width / 3));
            LayoutQuickJournalActions();
        }

        protected override bool ShowWithoutActivation => previewMode || base.ShowWithoutActivation;
        protected override void OnLoad(EventArgs e)
        {
            if (previewMode) { LayoutAccountingDataForm(); ApplyQuickJournalLayout(); return; }
            base.OnLoad(e);
        }

        private void LayoutQuickJournalActions()
        {
            if(quickStatusLayout||IsDisposed)return;
            AntdUI.Button save=quickActions.Controls["quickJournalSave"] as AntdUI.Button;
            AntdUI.Button refresh=quickActions.Controls["quickJournalRefresh"] as AntdUI.Button;
            if(save==null||refresh==null)return;
            quickStatusLayout=true;
            try
            {
                const int edge=22,gap=12;
                int width=Math.Max(1,quickActions.ClientSize.Width-edge*2);
                int statusHeight=Math.Max(44,TextRenderer.MeasureText(quickStatus.Text,quickStatus.Font,new Size(width,int.MaxValue),TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix).Height+4);
                int wanted=Math.Max(112,statusHeight+68);if(quickActions.Height!=wanted)quickActions.Height=wanted;
                quickStatus.SetBounds(edge,6,width,statusHeight);
                save.SetBounds(quickLeftToRight?edge:quickActions.ClientSize.Width-edge-save.Width,quickActions.Height-54,save.Width,44);
                var create=quickActions.Controls["quickJournalNew"] as AntdUI.Button;
                Control previous=save;
                if(create!=null){create.SetBounds(quickLeftToRight?save.Right+gap:save.Left-gap-create.Width,quickActions.Height-54,create.Width,44);previous=create;}
                refresh.SetBounds(quickLeftToRight?previous.Right+gap:previous.Left-gap-refresh.Width,quickActions.Height-54,refresh.Width,44);
                var close = AccountingCloseAction;
                int closeWidth = Math.Max(104, TextRenderer.MeasureText(close.Text, close.Font).Width + 48);
                close.SetBounds(quickLeftToRight ? refresh.Right + gap : refresh.Left - gap - closeWidth, quickActions.Height - 54, closeWidth, 44);
            }
            finally{quickStatusLayout=false;}
        }

        private void PopulatePreviewData()
        {
            debitAccount.Items.Add("1000 - الصندوق");
            debitAccount.SelectedIndex = 0;
            creditAccount.Items.Add("2000 - الموردون");
            creditAccount.SelectedIndex = 0;
            debitAccount.Enabled = true;
            creditAccount.Enabled = true;
            var sample = new DataTable();
            sample.Columns.Add("رقم القيد");
            sample.Columns.Add("التاريخ");
            sample.Columns.Add("المصدر");
            sample.Columns.Add("مدين", typeof(decimal));
            sample.Columns.Add("دائن", typeof(decimal));
            sample.Columns.Add("البيان");
            sample.Rows.Add("JV-PREVIEW-01", DateTime.Today.ToString("yyyy/MM/dd"), "Manual", 1250.50m, 1250.50m, "قيد متوازن توضيحي");
            Grid.DataSource = sample;
            quickOptionsReady=true;quickEditorCurrent=CaptureQuickContext(false);quickHistoryCurrent=CaptureQuickContext(true);
        }

        private void AddAntdQuickAction(string name, string text, AppIcon icon, Action action)
        {
            AntdUI.Button button = DesktopComponents.Action(name, text, icon, text == "حفظ القيد");
            button.Size = new Size(text == "حفظ القيد" ? 160 : 132, 44);
            button.AccessibleDescription = "إجراء في شاشة القيود اليومية السريعة";
            DesktopComponents.StyleOutlinedFooterAction(button);
            if (name == "quickJournalSave") button.Enabled = !previewMode && PermissionService.CanExecute(AppOperation.PostAccountingEntry);
            button.Click += (sender, args) => action();
            quickActions.Controls.Add(button);
        }

        private async System.Threading.Tasks.Task LoadInitialDataAsync()
        {if(await LoadQuickJournalAccountsAsync())await RefreshQuickJournalHistoryAsync();}

        internal async System.Threading.Tasks.Task<bool> LoadQuickJournalAccountsAsync(
            Func<System.Threading.CancellationToken,System.Threading.Tasks.Task<DataTable>> read=null)
        {
            if(quickEditorCurrent!=null&&!IsQuickCurrent(quickEditorCurrent))InvalidateQuickContext();
            var previous=quickLookupCancellation;if(previous!=null&&!previous.IsCancellationRequested)previous.Cancel();
            var cancellation=new System.Threading.CancellationTokenSource();quickLookupCancellation=cancellation;
            int generation=++quickLookupGeneration;Func<bool> current=null;
            try
            {
                current=CaptureQuickContext(false);if(!IsQuickCurrent(current)){InvalidateQuickContext();return false;}
                using(var rows=await(read==null?JournalEntryLookupService.LoadQuickAccountsAsync(cancellation.Token):read(cancellation.Token)))
                {
                    if(IsDisposed||generation!=quickLookupGeneration||cancellation.IsCancellationRequested)return false;
                    if(!IsQuickCurrent(current)){InvalidateQuickContext();return false;}
                    if(rows==null)throw new InvalidOperationException("لم ترجع خيارات الحسابات بيانات صالحة.");
                    var options=rows.Rows.Cast<DataRow>().Select(row=>Convert.ToString(row["AccountCode"])+" - "+Convert.ToString(row["AccountName"])).ToArray();
                    string debitSelection=debitAccount.Text,creditSelection=creditAccount.Text;
                    bool retain=quickOptionsReady&&IsQuickCurrent(quickEditorCurrent);
                    foreach(ComboBox combo in new[]{debitAccount,creditAccount})
                    {
                        combo.Items.Clear();combo.Items.AddRange(options);
                        string selected=combo==debitAccount?debitSelection:creditSelection;
                        int index=retain?Array.IndexOf(options,selected):-1;
                        combo.SelectedIndex=index>=0?index:!retain&&combo.Items.Count>0?0:-1;
                        if(combo.SelectedIndex<0)combo.Text=string.Empty;combo.Enabled=true;
                    }
                    if(!retain){quickSystemCurrencyCode=rows.ExtendedProperties["SystemCurrencyCode"] as string;amount.Text=string.Empty;description.Clear();}
                    quickOptionsReady=true;quickEditorCurrent=current;amount.Enabled=description.Enabled=true;
                    if(!InitializeQuickRecovery()){quickOptionsReady=false;ApplyQuickRecoveryLock();return false;}
                    if(quickRecovery!=null&&quickRecovery.Pending!=null)RestoreQuickPending();
                    ApplyQuickRecoveryLock();
                    var save=quickActions.Controls["quickJournalSave"];if(save!=null)save.Enabled=!previewMode&&!quickWriteBusy&&PermissionService.CanExecute(AppOperation.PostAccountingEntry);
                }
                return true;
            }
            catch(OperationCanceledException){return false;}
            catch(Exception error)
            {
                if(!IsDisposed&&generation==quickLookupGeneration)
                {
                    if(!IsQuickCurrent(current))InvalidateQuickContext();
                    else {SetQuickJournalStatus(QuickText("تعذر تحميل الحسابات. أعد التحديث؛ بقي المحرر السابق إذا كانت جلسته صالحة.","Could not load accounts. Refresh again; the previous editor remains while its session is valid."));if(!previewMode)quickStatus.AccessibleDescription+=" رمز الدعم: "+AppDiagnostics.Log("QuickJournalLookups",error);}
                }
                return false;
            }
            finally{if(ReferenceEquals(quickLookupCancellation,cancellation))quickLookupCancellation=null;cancellation.Dispose();}
        }
        private async void SaveManualEntry() { await SaveQuickEntryAsync(); }

        internal async System.Threading.Tasks.Task<int> RunQuickJournalWriteAsync(
            Func<System.Threading.Tasks.Task<int>> write,
            Func<PharmacyManagement.Application.JournalHistoryReadRequest, System.Threading.Tasks.Task<DataTable>> read = null)
        {
            if (write == null) throw new ArgumentNullException(nameof(write));
            if (quickWriteBusy || !EnsureQuickEditor()) return 0;
            var writeContext=quickEditorCurrent;CancelQuickReads(true);
            bool editorEnabled = Editor.Enabled, gridEnabled = Grid.Enabled, actionsEnabled = quickActions.Enabled;
            quickWriteBusy = true; ++quickLoadGeneration;
            try
            {
                foreach (Control control in new Control[] { Editor, Grid, quickActions })
                {
                    if (IsDisposed || Disposing) return 0;
                    control.Enabled = false;
                }
                SetQuickJournalStatus(QuickText("جار حفظ القيد؛ انتظر اكتمال العملية.","Saving the journal entry; wait for completion."));
                if (IsDisposed || Disposing) return 0;
                if (!IsQuickCurrent(writeContext)) { InvalidateQuickContext(); return 0; }
                int acceptedId;
                try
                {
                    acceptedId = await write();
                    if (acceptedId <= 0) throw new InvalidOperationException("لم ترجع عملية الحفظ رقم قيد صالحًا.");
                }
                catch (Exception error)
                {
                    if(!IsQuickCurrent(writeContext)){InvalidateQuickContext();return 0;}
                    if (!IsDisposed && !Disposing)
                    {
                        SetQuickJournalStatus(quickRecovery!=null&&quickRecovery.CanCorrectRejected ?
                            QuickText("رفضت المحاولة وأُكد التراجع؛ صحح المسودة واحفظ بالمعرّف والتاريخ نفسيهما.","The attempt was rejected and rolled back. Correct the draft and save with its original identity and date.") :
                            quickRecovery!=null&&quickRecovery.Pending!=null ?
                            QuickText("نتيجة الاعتماد غير مؤكدة. احتُفظ بالطلب؛ اختر حفظ للتحقق وإعادة الطلب نفسه.","Acceptance is uncertain. The request was retained; Save checks and retries the same request.") :
                            QuickText("تعذر حفظ القيد؛ راجع رسالة الخطأ قبل إعادة المحاولة.","Could not save the journal entry. Review the error before retrying."));
                        if (!previewMode) UiTheme.ShowOperationIssue(this, "حفظ القيد السريع", error);
                    }
                    return 0;
                }
                if(!IsQuickCurrent(writeContext)){InvalidateQuickContext();return acceptedId;}
                acceptedQuickJournalId = acceptedId;
                if(quickRecovery!=null&&!quickRecovery.AcceptedContentMatchesRequest)
                {
                    quickAcceptedReview=true;
                    SetQuickJournalStatus(QuickText("القيد رقم ","Entry ")+acceptedId+
                        QuickText(" معتمد ببيانات مختلفة؛ لم تعتمد التعديلات الحالية. راجع القيد المحفوظ واختر جديد."," was accepted with different data; these edits were not accepted. Review the saved entry and choose New."));
                    return acceptedId;
                }
                try { await CompleteSavedQuickJournalPresentationAsync(acceptedId, null, read); }
                catch (Exception error) { if (!IsDisposed && !Disposing) ShowQuickJournalPresentationIssue(error, QuickText("تحديث الشاشة","refreshing the screen")); }
                if(quickRecovery!=null&&quickRecovery.CleanupError!=null)
                {
                    SetQuickJournalStatus(QuickText("القيد معتمد؛ تعذر تنظيف سجل الاستعادة. اختر جديد لإعادة التنظيف دون ترحيل آخر.","The entry is accepted; recovery cleanup failed. Choose New to retry cleanup without posting again."));
                    if(!previewMode)AppDiagnostics.Log("QuickJournalRecoveryCleanup",quickRecovery.CleanupError);
                }
                return acceptedId;
            }
            finally
            {
                quickWriteBusy = false;
                if (!IsDisposed && !Disposing)
                { Editor.Enabled = editorEnabled; Grid.Enabled = gridEnabled; quickActions.Enabled = actionsEnabled; if(!IsQuickCurrent(writeContext))InvalidateQuickContext(); else if(quickRecovery!=null)ApplyQuickRecoveryLock(); }
            }
        }

        internal bool TryReadQuickManualInput(out decimal value, out string debitCode, out string creditCode)
        {
            value=0;debitCode=creditCode=string.Empty;
            if(!EnsureQuickEditor())return false;
            debitCode = SelectedAccount(debitAccount); creditCode = SelectedAccount(creditAccount);
            if (!decimal.TryParse(amount.Text.Trim(), out value) || value <= 0)
            { SetQuickJournalStatus(QuickText("أدخل مبلغًا موجبًا وصحيحًا قبل حفظ القيد.","Enter a valid positive amount before saving.")); if (!previewMode) amount.Focus(); return false; }
            if (string.IsNullOrWhiteSpace(debitCode) || string.IsNullOrWhiteSpace(creditCode))
            { SetQuickJournalStatus(QuickText("اختر الحساب المدين والحساب الدائن قبل حفظ القيد.","Choose both debit and credit accounts before saving.")); return false; }
            if (string.Equals(debitCode, creditCode, StringComparison.OrdinalIgnoreCase))
            { SetQuickJournalStatus(QuickText("اختر حسابين مختلفين للمدين والدائن قبل حفظ القيد.","Choose different debit and credit accounts before saving.")); if (!previewMode) creditAccount.Focus(); return false; }
            return true;
        }

        internal async System.Threading.Tasks.Task CompleteSavedQuickJournalPresentationAsync(int savedId,
            Action clearEditor = null, Func<PharmacyManagement.Application.JournalHistoryReadRequest, System.Threading.Tasks.Task<DataTable>> read = null)
        {
            if (savedId <= 0) throw new ArgumentOutOfRangeException(nameof(savedId));
            acceptedQuickJournalId = savedId; if (IsDisposed) return;
            SetQuickJournalStatus(QuickText("تم حفظ القيد رقم ","Journal entry saved: ") + savedId + ".");
            try { if (clearEditor != null) clearEditor(); else { amount.Text = string.Empty; description.Clear(); } }
            catch (Exception error) { ShowQuickJournalPresentationIssue(error, QuickText("تنظيف حقول الشاشة","clearing the editor")); return; }
            await RefreshQuickJournalHistoryAsync(read);
        }

        protected override async void LoadData()
        { if (!previewMode) await RefreshQuickJournalHistoryAsync(); }

        internal async System.Threading.Tasks.Task RefreshQuickJournalHistoryAsync(
            Func<PharmacyManagement.Application.JournalHistoryReadRequest,System.Threading.Tasks.Task<DataTable>> read=null,
            Func<PharmacyManagement.Application.JournalHistoryReadRequest,System.Threading.CancellationToken,System.Threading.Tasks.Task<DataTable>> cancellableRead=null)
        {
            if((quickEditorCurrent!=null&&!IsQuickCurrent(quickEditorCurrent))||(quickHistoryCurrent!=null&&!IsQuickCurrent(quickHistoryCurrent)))InvalidateQuickContext();
            CancelQuickReads(false);int generation=++quickLoadGeneration;
            var cancellation=new System.Threading.CancellationTokenSource();quickHistoryCancellation=cancellation;
            Func<bool> current=null;DataTable rows=null;
            try
            {
                current=CaptureQuickContext(true);if(!IsQuickCurrent(current)){InvalidateQuickContext();return;}
                DateTime startDate,endDate;
                if(!AntdDateInput.TryRead(fromDate,out startDate)||!AntdDateInput.TryRead(toDate,out endDate)||startDate>endDate)
                {SetQuickJournalStatus((acceptedQuickJournalId>0?QuickText("تم حفظ القيد رقم ","Journal entry saved: ")+acceptedQuickJournalId+". ":"")+QuickText("اختر تاريخ بداية ونهاية صحيحين لتحديث السجل.","Choose a valid start and end date to refresh history."));return;}
                var request=new PharmacyManagement.Application.JournalHistoryReadRequest(startDate,endDate,source.Text);
                rows=await(cancellableRead!=null?cancellableRead(request,cancellation.Token):read!=null?read(request):JournalEntriesQuickReadService.LoadAsync(request,cancellation.Token));
                if(IsDisposed||generation!=quickLoadGeneration||cancellation.IsCancellationRequested){rows?.Dispose();return;}
                if(!IsQuickCurrent(current)){rows?.Dispose();InvalidateQuickContext();return;}
                if(rows==null)throw new InvalidOperationException("لم تُرجع قراءة سجل القيود بيانات صالحة.");
                var previous=Grid.DataSource as DataTable;Grid.DataSource=rows;quickHistoryCurrent=current;quickAcceptedFilters=request;
                if(previous!=rows)previous?.Dispose();
                string historyMessage=rows.Rows.Count==0?QuickText("لا توجد قيود في النطاق المحدد.","No journal entries in the selected range."):QuickText("تم تحديث سجل القيود.","Journal history refreshed.");
                SetQuickJournalStatus((acceptedQuickJournalId>0?QuickText("تم حفظ القيد رقم ","Journal entry saved: ")+acceptedQuickJournalId+". ":string.Empty)+historyMessage);
            }
            catch(OperationCanceledException){}
            catch(Exception error)
            {
                if(IsDisposed||generation!=quickLoadGeneration)return;
                if(rows!=null&&rows!=Grid.DataSource)rows.Dispose();
                if(!IsQuickCurrent(current))InvalidateQuickContext();else ShowQuickJournalPresentationIssue(error,QuickText("تحديث السجل","refreshing history"));
            }
            finally{if(ReferenceEquals(quickHistoryCancellation,cancellation))quickHistoryCancellation=null;cancellation.Dispose();}
        }

        private void SetQuickJournalStatus(string text)
        { if (IsDisposed) return; quickStatus.Text = text; quickStatus.AccessibleDescription = text; }
        private void ShowQuickJournalPresentationIssue(Exception error, string step)
        {
            if (IsDisposed) return;
            SetQuickJournalStatus(acceptedQuickJournalId > 0
                ? QuickText("تم حفظ القيد رقم ","Journal entry saved: ") + acceptedQuickJournalId + QuickText(". تعذر ",". Could not complete ") + step + QuickText(". لا تعد حفظ القيد نفسه.",". Do not save the same entry again.")
                : QuickText("تعذر ","Could not complete ") + step + QuickText(". بقي السجل السابق ظاهرًا؛ أعد محاولة التحديث.",". The previous history remains; retry refresh."));
            if (!previewMode) quickStatus.AccessibleDescription += " رمز الدعم: " + AppDiagnostics.Log("QuickJournalPresentation", error);
        }
    }

    public partial class OpeningEntriesForm : JournalEntriesForm
    {
        public OpeningEntriesForm() : base("القيود الإفتتاحية", "OpeningEntry")
        {
        }
    }

    public partial class VoucherForm : AccountingDataForm
    {
        private readonly string voucherType;
        private readonly TimeSpan voucherTimeOfDay=DateTime.Now.TimeOfDay;
        private readonly AntdUI.DatePicker voucherDate=DesktopComponents.DatePicker("legacyVoucherDate","تاريخ السند",DateTime.Today);
        private readonly TextBox party=CreateLegacyVoucherText("legacyVoucherParty","الطرف");
        private readonly ComboBox account=new NativeThemedSelect{Name="legacyVoucherAccount",AccessibleName="الحساب المقابل",Font=AppFonts.Create(10F)};
        private readonly AntdUI.Input amount=DesktopComponents.Input("legacyVoucherAmount","المبلغ");
        private readonly TextBox notes=CreateLegacyVoucherText("legacyVoucherNotes","ملاحظات");
        private readonly Label voucherStatus=new Label();
        private readonly bool voucherPreview,voucherLeftToRight;
        private bool voucherWriteBusy,voucherPresentationPending,voucherOptionsReady,voucherInvalidating,voucherHistoryLoading;
        private int voucherLoadGeneration,voucherOptionsGeneration,acceptedVoucherId,knownCommittedVoucherId;
        private Func<bool> voucherEditorCurrent;
        private string voucherSystemCurrencyCode;
        private System.Threading.CancellationTokenSource voucherOptionsCancellation,voucherHistoryCancellation;
        public VoucherForm(string type):this(type,false){}
        internal VoucherForm(string type,bool preview,bool? forceLeftToRight=null):base(GetVoucherTitle(type))
        {
            voucherType=type??string.Empty;voucherPreview=preview;voucherLeftToRight=forceLeftToRight??UiDirection.IsLeftToRight;
            voucherDate.MinDate=new DateTime(1753,1,1);voucherDate.MaxDate=DateTimePicker.MaximumDateTime;
            BuildEditor();foreach(var input in new[]{party,notes})input.TextChanged+=(sender,args)=>UiDirection.ApplyContentDirection((TextBox)sender,voucherLeftToRight);voucherOptionsReady=preview;voucherSystemCurrencyCode=preview?"YER":null;voucherEditorCurrent=CaptureVoucherContext();
            ApplyVoucherPresentation();SetVoucherAvailability();SetVoucherStatus(VoucherText("أدخل بيانات السند ثم احفظه."));
            Load+=async(s,e)=>{ApplyVoucherPresentation();if(!voucherPreview)await LoadVoucherOptionsAsync();};
            Activated+=(s,e)=>{ApplyVoucherPresentation();if(voucherEditorCurrent!=null&&!VoucherContextCurrent(voucherEditorCurrent))InvalidateVoucherContext();};
            UiSessionSubscription.Attach(this,()=>{if(voucherEditorCurrent!=null&&!VoucherContextCurrent(voucherEditorCurrent))InvalidateVoucherContext();});
            Resize+=(s,e)=>ApplyVoucherPresentation();
            FormClosing+=(s,e)=>{
                if(voucherWriteBusy&&!voucherPresentationPending&&e.CloseReason==CloseReason.UserClosing&&!(voucherRecovery!=null&&(voucherRecovery.Operation.Pending!=null||voucherRecovery.Operation.Accepted!=null)))
                {e.Cancel=true;SetVoucherStatus(VoucherText("انتظر اكتمال حفظ السند قبل إغلاق النافذة."));}
                if(!e.Cancel){++voucherLoadGeneration;++voucherOptionsGeneration;CancelVoucherReads();}
            };
            Disposed+=(s,e)=>{CancelVoucherReads();ReleaseVoucherRecovery();};
            Grid.RightToLeftChanged+=(sender,args)=>{var expected=voucherLeftToRight?RightToLeft.No:RightToLeft.Yes;if(Grid.RightToLeft!=expected)Grid.RightToLeft=expected;};
            Grid.CellFormatting+=(sender,args)=>{
                if(!voucherLeftToRight||args.ColumnIndex<0||Grid.Columns[args.ColumnIndex].Name!="نوع السند")return;
                switch(Convert.ToString(args.Value)){
                    case "قبض داخلي":args.Value="Internal receipt";break;case "قبض خارجي":args.Value="External receipt";break;case "قبض متعدد":args.Value="Multi receipt";break;
                    case "صرف داخلي":args.Value="Internal payment";break;case "صرف خارجي":args.Value="External payment";break;case "صرف متعدد":args.Value="Multi payment";break;
                }
            };
            Grid.ClipboardCopyMode=DataGridViewClipboardCopyMode.Disable;
            Grid.KeyDown+=(s,e)=>{if(e.Control&&e.KeyCode==Keys.C){e.Handled=true;e.SuppressKeyPress=true;string text=SelectedVoucherHistoryText();if(text!=null)Clipboard.SetText(text);}};
        }
        private string VoucherText(string arabic,string english)=>voucherLeftToRight?english:arabic;
        private string VoucherText(string text)=>voucherLeftToRight?UiLanguageCatalog.Translate(text):text;
        private Func<bool> CaptureVoucherContext()
        {
            if(!voucherPreview){var session=ApplicationServices.CreateVoucherHistoryReadSession();return ()=>session.IsCurrent;}
            if(!SessionContext.IsAuthenticated)return ()=>true;
            long revision=SessionContext.CurrentSessionRevision;return ()=>SessionContext.IsAuthenticated&&SessionContext.CurrentSessionRevision==revision;
        }
        private bool VoucherContextCurrent(Func<bool> current){try{return current!=null&&current();}catch{return false;}}
        private void CancelVoucherReads()
        {
            foreach(var cancellation in new[]{voucherOptionsCancellation,voucherHistoryCancellation})
                if(cancellation!=null&&!cancellation.IsCancellationRequested)cancellation.Cancel();
        }
        private void SetVoucherAvailability()
        {
            if(IsDisposed||Disposing)return;
            bool ready=voucherOptionsReady&&VoucherContextCurrent(voucherEditorCurrent);
            Editor.Enabled=ready&&!voucherWriteBusy;account.Enabled=ready&&!voucherWriteBusy;
            Grid.Enabled=ready&&!voucherWriteBusy&&!voucherHistoryLoading;
            var save=Controls.Find("legacyVoucherSave",true).FirstOrDefault();
            if(save!=null)save.Enabled=ready&&!voucherPreview&&!voucherWriteBusy&&!voucherHistoryLoading&&PermissionService.CanExecute(AppOperation.PostCashVoucher);
            var refresh=Controls.Find("legacyVoucherRefresh",true).FirstOrDefault();if(refresh!=null)refresh.Enabled=!voucherWriteBusy;
            ApplyVoucherRecoveryLock();
        }
        private void InvalidateVoucherContext()
        {
            if(voucherInvalidating||IsDisposed)return;voucherInvalidating=true;
            try
            {
                ++voucherLoadGeneration;++voucherOptionsGeneration;CancelVoucherReads();ReleaseVoucherRecovery();voucherOptionsReady=false;voucherSystemCurrencyCode=null;voucherEditorCurrent=null;acceptedVoucherId=0;
                account.Items.Clear();account.Text=string.Empty;voucherDate.Value=DateTime.Today;party.Clear();amount.Text=string.Empty;notes.Clear();
                var previous=Grid.DataSource as DataTable;Grid.DataSource=null;previous?.Dispose();Grid.ClearSelection();SetVoucherAvailability();
                SetVoucherStatus(VoucherText("تغيرت جلسة السند أو صلاحيتها. حدّث الخيارات للبدء بمسودة فارغة."),true);
            }
            finally{voucherInvalidating=false;}
        }
        private bool EnsureVoucherEditor()
        {
            if(IsDisposed||Disposing||voucherInvalidating)return false;
            if(voucherOptionsReady&&VoucherContextCurrent(voucherEditorCurrent))return true;
            InvalidateVoucherContext();return false;
        }
        internal int KnownCommittedVoucherId=>knownCommittedVoucherId;
        internal string SelectedVoucherHistoryText()
        {
            if(voucherWriteBusy||voucherHistoryLoading||!EnsureVoucherEditor())return null;
            var row=Grid.CurrentRow;if(row==null||row.IsNewRow)return null;
            return string.Join("\t",Grid.Columns.Cast<DataGridViewColumn>().OrderBy(c=>c.Index).Select(c=>{
                var value=row.Cells[c.Index].Value;return value==null||value==DBNull.Value?string.Empty:Convert.ToString(value,System.Globalization.CultureInfo.InvariantCulture);
            }));
        }
        private static TextBox CreateLegacyVoucherText(string name,string caption)
        {
            var input=new TextBox{Name=name,AccessibleName=caption,Font=AppFonts.Create(10F),RightToLeft=UiDirection.IsLeftToRight?RightToLeft.No:RightToLeft.Yes,
                TextAlign=UiDirection.IsLeftToRight?HorizontalAlignment.Left:HorizontalAlignment.Right};
            input.TextChanged+=(s,e)=>UiDirection.ApplyContentDirection(input,UiDirection.IsLeftToRight);return input;
        }
        internal bool TryReadVoucherDate(out DateTime date)
        {if(!AntdDateInput.TryRead(voucherDate,out date))return false;date=date.Add(voucherTimeOfDay);return true;}
        private static async System.Threading.Tasks.Task<PharmacyManagement.Application.JournalEditorLookupSnapshot> ReadVoucherInitialAsync(System.Threading.CancellationToken token)
        {
            var session=ApplicationServices.CreateJournalEditorReadSession();
            var result=await session.Service.LoadAsync(session.Actor,token);
            token.ThrowIfCancellationRequested();if(!session.IsCurrent)throw new UnauthorizedAccessException("تغيرت جلسة خيارات السند.");
            return result;
        }
        internal async System.Threading.Tasks.Task<bool> LoadVoucherOptionsAsync(Func<System.Threading.CancellationToken,System.Threading.Tasks.Task<IList<string>>> read=null,
            Func<System.Threading.CancellationToken,System.Threading.Tasks.Task<PharmacyManagement.Application.JournalEditorLookupSnapshot>> initialRead=null)
        {
            if(voucherWriteBusy||IsDisposed)return false;
            if(voucherEditorCurrent!=null&&!VoucherContextCurrent(voucherEditorCurrent))InvalidateVoucherContext();
            if(voucherOptionsCancellation!=null&&!voucherOptionsCancellation.IsCancellationRequested)voucherOptionsCancellation.Cancel();
            int generation=++voucherOptionsGeneration;var cancellation=new System.Threading.CancellationTokenSource();voucherOptionsCancellation=cancellation;
            var current=CaptureVoucherContext();voucherOptionsReady=false;SetVoucherAvailability();
            try
            {
                if(!VoucherContextCurrent(current)){InvalidateVoucherContext();return false;}
                IList<string> options;string systemCode=voucherSystemCurrencyCode;
                if(read==null)
                {
                    var snapshot=await(initialRead==null?ReadVoucherInitialAsync(cancellation.Token):initialRead(cancellation.Token));
                    if(snapshot==null)throw new InvalidOperationException("Voucher lookup source returned no snapshot.");
                    systemCode=snapshot.SystemCurrency.Code.Trim().ToUpperInvariant();
                    options=snapshot.Accounts.Select(row=>row.Code+" - "+row.Name).ToList();
                }
                else options=await read(cancellation.Token);
                if(IsDisposed||generation!=voucherOptionsGeneration)return false;
                cancellation.Token.ThrowIfCancellationRequested();if(!VoucherContextCurrent(current)){InvalidateVoucherContext();return false;}
                if(options==null)throw new InvalidOperationException("Voucher account source returned no result.");
                string selected=account.Text;account.Items.Clear();foreach(string option in options)account.Items.Add(option);
                if(account.Items.Contains(selected))account.SelectedItem=selected;else if(account.Items.Count>0&&string.IsNullOrWhiteSpace(selected))account.SelectedIndex=0;else{account.SelectedIndex=-1;account.Text=string.Empty;}
                bool changedDraftCurrency=!string.IsNullOrWhiteSpace(voucherSystemCurrencyCode)&&!string.IsNullOrWhiteSpace(amount.Text)&&
                    !string.Equals(voucherSystemCurrencyCode,systemCode,StringComparison.OrdinalIgnoreCase);
                if(!changedDraftCurrency)voucherSystemCurrencyCode=systemCode;
                amount.AccessibleDescription=VoucherText("المبلغ بعملة النظام: ")+voucherSystemCurrencyCode;
                ApplyVoucherPresentation();
                voucherEditorCurrent=current;voucherOptionsReady=true;SetVoucherAvailability();
                SetVoucherStatus(changedDraftCurrency?VoucherText("تغيرت عملة النظام؛ بقيت المسودة بعملتها السابقة. افتح سندًا جديدًا وراجع المبلغ قبل الحفظ."):
                    VoucherText("تم تحميل حسابات السند وعملته."),changedDraftCurrency);
                if(!voucherPreview&&read==null&&initialRead==null)InitializeVoucherRecovery();
                if(read==null&&initialRead==null)await RefreshVoucherHistoryAsync();ApplyVoucherRecoveryLock();return true;
            }
            catch(OperationCanceledException){return false;}
            catch(Exception error)
            {
                if(IsDisposed||generation!=voucherOptionsGeneration)return false;
                if(!VoucherContextCurrent(current)){InvalidateVoucherContext();return false;}
                voucherOptionsReady=false;SetVoucherAvailability();SetVoucherStatus(VoucherText("تعذر تحميل حسابات السند. أعد التحديث."),true);
                if(!voucherPreview)UiTheme.ShowOperationIssue(this,"تحميل بيانات السند",error);return false;
            }
            finally{if(ReferenceEquals(voucherOptionsCancellation,cancellation))voucherOptionsCancellation=null;cancellation.Dispose();}
        }
        private static string GetVoucherTitle(string type)
        {
            bool multi=type!=null&&type.IndexOf("Multi",StringComparison.OrdinalIgnoreCase)>=0;
            bool payment=type!=null&&type.IndexOf("Payment",StringComparison.OrdinalIgnoreCase)>=0;
            return payment?(multi?"سند صرف متعدد":"سند صرف"):(multi?"سند قبض متعدد":"سند قبض");
        }
        private async void SaveVoucher()
        {
            if(voucherPreview||voucherWriteBusy||!EnsureVoucherEditor())return;
            if(!InitializeVoucherRecovery())return;
            if(voucherRecovery!=null&&voucherRecovery.Operation.Pending!=null&&!voucherRecovery.Operation.CanCorrectRejected){await RunVoucherWriteAsync(()=>voucherRecovery.ExecuteAsync());return;}
            DateTime date;decimal value;
            if(!TryReadVoucherDate(out date)){SetVoucherStatus(VoucherText("أدخل تاريخًا صحيحًا للسند بصيغة yyyy/MM/dd."),true);voucherDate.Focus();return;}
            if(!decimal.TryParse(amount.Text.Trim(),out value)||value<=0){SetVoucherStatus(VoucherText("أدخل مبلغًا موجبًا وصحيحًا."),true);amount.Focus();return;}
            string code=SelectedAccount(account);
            if(string.IsNullOrWhiteSpace(code)){SetVoucherStatus(VoucherText("اختر الحساب المقابل قبل حفظ السند."),true);account.Focus();return;}
            PharmacyManagement.Application.VoucherPostRequest request;
            try{request=CreateVoucherRequest(date,value,code);}catch(Exception error){SetVoucherStatus(VoucherText("عملة السند غير متاحة؛ حدّث الخيارات قبل الحفظ."),true);if(!voucherPreview)UiTheme.ShowOperationIssue(this,"عملة السند",error);return;}
            try{voucherRecovery.Prepare(request);}catch(Exception error){SetVoucherStatus(error.Message,true);return;}
            await RunVoucherWriteAsync(()=>voucherRecovery.ExecuteAsync());
        }
        internal PharmacyManagement.Application.VoucherPostRequest CreateVoucherRequest(DateTime date,decimal value,string code)
        {
            if(!EnsureVoucherEditor()||string.IsNullOrWhiteSpace(voucherSystemCurrencyCode))throw new UnauthorizedAccessException("عملة مسودة السند غير متاحة أو جلستها تغيرت.");
            return new PharmacyManagement.Application.VoucherPostRequest(voucherType,date,party.Text,code,value,notes.Text,AccountingService.CashAccount,null,voucherSystemCurrencyCode);
        }
        internal async System.Threading.Tasks.Task<bool> RunVoucherWriteAsync(Func<System.Threading.Tasks.Task<int>> write,
            Func<System.Threading.Tasks.Task<DataTable>> read=null,Func<System.Threading.CancellationToken,System.Threading.Tasks.Task<DataTable>> cancellableRead=null)
        {
            if(write==null)throw new ArgumentNullException(nameof(write));if(voucherWriteBusy||!EnsureVoucherEditor())return false;
            var current=voucherEditorCurrent;voucherWriteBusy=true;++voucherLoadGeneration;
            try
            {
                CancelVoucherReads();SetVoucherAvailability();
                if(IsDisposed||Disposing)return false;
                SetVoucherStatus(VoucherText("جار حفظ السند؛ انتظر اكتمال العملية."));
                if(IsDisposed||Disposing)return false;
                if(!VoucherContextCurrent(current)){InvalidateVoucherContext();return false;}
                int id;
                try{id=await write();if(id<=0)throw new InvalidOperationException("Invalid accepted voucher id.");}
                catch(Exception error)
                {
                    if(IsDisposed)return false;if(!VoucherContextCurrent(current)){InvalidateVoucherContext();return false;}
                    SetVoucherStatus(voucherRecovery?.Message??VoucherText("تعذر حفظ السند؛ راجع رسالة الخطأ قبل إعادة المحاولة."),true);
                    if(!voucherPreview&&!VoucherEditorsOwnedQaSelfTests.QuietNotifications)UiTheme.ShowOperationIssue(this,"حفظ السند",error);return false;
                }
                knownCommittedVoucherId=id;
                if(IsDisposed)return true;if(!VoucherContextCurrent(current)){InvalidateVoucherContext();return true;}
                voucherPresentationPending=true;
                try{await CompleteSavedVoucherPresentationAsync(id,null,read,cancellableRead);}
                catch(Exception error){if(VoucherContextCurrent(current))ShowVoucherPresentationIssue(id,"تحديث الشاشة",error);else InvalidateVoucherContext();}
                return true;
            }
            finally{voucherPresentationPending=false;voucherWriteBusy=false;if(!IsDisposed){if(!VoucherContextCurrent(current))InvalidateVoucherContext();SetVoucherAvailability();}}
        }
        internal async System.Threading.Tasks.Task CompleteSavedVoucherPresentationAsync(int savedId,Action clearEditor=null,
            Func<System.Threading.Tasks.Task<DataTable>> read=null,Func<System.Threading.CancellationToken,System.Threading.Tasks.Task<DataTable>> cancellableRead=null)
        {
            if(savedId<=0)throw new ArgumentOutOfRangeException(nameof(savedId));knownCommittedVoucherId=savedId;
            if(!EnsureVoucherEditor())return;var current=voucherEditorCurrent;acceptedVoucherId=savedId;
            SetVoucherStatus(VoucherText("تم حفظ السند رقم ")+savedId+".");
            try{if(clearEditor!=null)clearEditor();else{amount.Text=string.Empty;notes.Clear();}}
            catch(Exception error){if(VoucherContextCurrent(current))ShowVoucherPresentationIssue(savedId,"تنظيف حقول الشاشة",error);else InvalidateVoucherContext();return;}
            if(!VoucherContextCurrent(current)){InvalidateVoucherContext();return;}
            await RefreshVoucherHistoryAsync(read,cancellableRead);
        }
        protected override async void LoadData()=>await RefreshVoucherHistoryAsync();
        internal async System.Threading.Tasks.Task RefreshVoucherHistoryAsync(Func<System.Threading.Tasks.Task<DataTable>> read=null,
            Func<System.Threading.CancellationToken,System.Threading.Tasks.Task<DataTable>> cancellableRead=null)
        {
            if(!EnsureVoucherEditor())return;
            if(voucherHistoryCancellation!=null&&!voucherHistoryCancellation.IsCancellationRequested)voucherHistoryCancellation.Cancel();
            int generation=++voucherLoadGeneration;var current=voucherEditorCurrent;
            var cancellation=new System.Threading.CancellationTokenSource();voucherHistoryCancellation=cancellation;voucherHistoryLoading=true;SetVoucherAvailability();
            DataTable rows=null;
            try
            {
                rows=await(cancellableRead!=null?cancellableRead(cancellation.Token):read!=null?read():ReceiptVoucherWorkspaceService.LoadHistoryAsync(voucherType,cancellation.Token));
                if(IsDisposed||generation!=voucherLoadGeneration){rows?.Dispose();return;}
                cancellation.Token.ThrowIfCancellationRequested();if(!VoucherContextCurrent(current)){rows?.Dispose();InvalidateVoucherContext();return;}
                if(rows==null)throw new InvalidOperationException("Voucher history source returned no result.");
                var previous=Grid.DataSource as DataTable;Grid.DataSource=rows;if(previous!=rows)previous?.Dispose();ApplyVoucherPresentation();
                SetVoucherStatus(acceptedVoucherId>0?VoucherText("تم حفظ السند رقم ")+acceptedVoucherId+VoucherText(". تم تحديث السجل."):VoucherText("تم تحديث سجل السندات."));
            }
            catch(OperationCanceledException)
            {if(IsDisposed||rows!=Grid.DataSource)rows?.Dispose();if(!IsDisposed&&generation==voucherLoadGeneration&&!VoucherContextCurrent(current))InvalidateVoucherContext();}
            catch(Exception error)
            {
                if(IsDisposed||generation!=voucherLoadGeneration)return;
                if(!VoucherContextCurrent(current)){rows?.Dispose();InvalidateVoucherContext();return;}
                ShowVoucherPresentationIssue(acceptedVoucherId,"تحديث السجل",error);
            }
            finally{if(ReferenceEquals(voucherHistoryCancellation,cancellation)){voucherHistoryCancellation=null;voucherHistoryLoading=false;SetVoucherAvailability();}cancellation.Dispose();}
        }
        private void ShowVoucherPresentationIssue(int savedId,string step,Exception error)
        {
            SetVoucherStatus(savedId>0?VoucherText("تم حفظ السند رقم ")+savedId+VoucherText(". تعذر "+step+". لا تعد حفظ السند نفسه.",". Unable to refresh the view. Do not save this voucher again."):
                VoucherText("تعذر "+step+". بقي السجل السابق ظاهرًا؛ أعد محاولة التحديث.","Unable to refresh. Previous history remains; retry the refresh."),true);
            if(!voucherPreview)voucherStatus.AccessibleDescription+=" "+VoucherText("رمز الدعم: ")+AppDiagnostics.Log("LegacyVoucherPresentation",error);
        }
        private void SetVoucherStatus(string message,bool issue=false)
        {if(IsDisposed)return;voucherStatus.Text=message;voucherStatus.AccessibleDescription=message;voucherStatus.ForeColor=issue?UiTheme.Danger:UiTheme.MutedText;}
        private void ApplyVoucherPresentation()
        {
            if(IsDisposed||voucherStatus.Parent==null)return;
            RightToLeft=voucherLeftToRight?RightToLeft.No:RightToLeft.Yes;RightToLeftLayout=!voucherLeftToRight;
            Text=VoucherText(GetVoucherTitle(voucherType),voucherType.Contains("Payment")?(voucherType.Contains("Multi")?"Multi payment voucher":"Payment voucher"):(voucherType.Contains("Multi")?"Multi receipt voucher":"Receipt voucher"));
            var title=Controls.Find("accountingDataTitle",true).OfType<Label>().FirstOrDefault();if(title!=null){title.Text=Text;title.TextAlign=voucherLeftToRight?ContentAlignment.MiddleLeft:ContentAlignment.MiddleRight;}
            ApplyAccountingClosePresentation(voucherLeftToRight);
            voucherDate.AccessibleName=VoucherText("تاريخ السند");party.AccessibleName=VoucherText("الطرف");account.AccessibleName=VoucherText("الحساب المقابل");amount.AccessibleName=VoucherText("المبلغ");notes.AccessibleName=VoucherText("ملاحظات");
            amount.RightToLeft=RightToLeft.No;amount.TextAlign=HorizontalAlignment.Right;
            var footer=Controls.Find("legacyVoucherFooter",true).FirstOrDefault();var actions=Controls.Find("legacyVoucherActions",true).FirstOrDefault();
            if(footer!=null)footer.BackColor=UiTheme.Surface;if(actions!=null){actions.BackColor=UiTheme.Surface;actions.RightToLeft=voucherLeftToRight?RightToLeft.No:RightToLeft.Yes;}
            Editor.RightToLeft=RightToLeft.No;Editor.AutoScrollMinSize=new Size(0,Editor.Controls.Cast<Control>().Sum(c=>c.Height)+Editor.Padding.Vertical);
            Grid.RightToLeft=voucherLeftToRight?RightToLeft.No:RightToLeft.Yes;
            var headers=new Dictionary<string,string>{{"رقم السند","No."},{"التاريخ","Date"},{"نوع السند","Type"},{"الطرف","Party"},{"الحساب","Account"},{"المبلغ","Amount"},{"ملاحظات","Notes"},{"رقم القيد","Journal"},{"المستخدم","User"}};
            foreach(DataGridViewColumn column in Grid.Columns)if(headers.ContainsKey(column.Name))column.HeaderText=VoucherText(column.Name,headers[column.Name]);
            voucherStatus.RightToLeft=RightToLeft.No;voucherStatus.TextAlign=voucherLeftToRight?ContentAlignment.MiddleLeft:ContentAlignment.MiddleRight;voucherStatus.BackColor=UiTheme.Surface;
            foreach(var input in new[]{party,notes}){if(input.Parent is AntdUI.Panel frame)DesktopComponents.StyleNativeTextFrame(frame,input);UiDirection.ApplyContentDirection(input,voucherLeftToRight);}
            if(account.Parent is AntdUI.Panel selectFrame)DesktopComponents.StyleNativeSelectFrame(selectFrame,account,voucherLeftToRight);
            foreach(var label in Editor.Controls.Cast<Control>().SelectMany(c=>c.Controls.OfType<Label>()))
            {
                label.RightToLeft=RightToLeft.No;label.TextAlign=voucherLeftToRight?ContentAlignment.MiddleLeft:ContentAlignment.MiddleRight;
                string key=label.Name;label.Text=key.Contains("Date")?VoucherText("التاريخ"):key.Contains("Party")?VoucherText("الطرف"):key.Contains("Account")?VoucherText("الحساب المقابل"):key.Contains("Amount")?VoucherText("المبلغ"):VoucherText("ملاحظات");
                if(key.Contains("Amount")&&!string.IsNullOrWhiteSpace(voucherSystemCurrencyCode))label.Text+=" ("+voucherSystemCurrencyCode+")";
            }
            foreach(string name in new[]{"legacyVoucherSave","legacyVoucherRefresh"})
            {
                var button=Controls.Find(name,true).OfType<AntdUI.Button>().FirstOrDefault();if(button==null)continue;
                button.Text=button.AccessibleName=name=="legacyVoucherSave"?VoucherText("حفظ السند"):VoucherText("تحديث");
                button.IconPosition=voucherLeftToRight?AntdUI.TAlignMini.Left:AntdUI.TAlignMini.Right;DesktopComponents.StyleOutlinedFooterAction(button);
            }
        }
    }

    public partial class ReceiptVoucherForm : Form
    {
        private string ReceiptText(string text) => (receiptPresentationDirection ?? (RightToLeft == RightToLeft.No)) ? UiLanguageCatalog.Translate(text) : text;
        private readonly string voucherType;
        private readonly bool isReceipt;
        private readonly bool isInternal;
        private readonly bool isMulti;
        private readonly BusinessValueLabel balanceLabel = new BusinessValueLabel();
        private readonly TextBox voucherNo = CreateVoucherTextInput("receiptVoucherNumber", "رقم السند");
        private readonly AntdUI.DatePicker voucherDate = DesktopComponents.DatePicker("receiptVoucherDate", "تاريخ السند", DateTime.Today);
        private readonly AntdUI.Select branch = DesktopComponents.Select("receiptVoucherBranch", "الفرع");
        private readonly ComboBox costCenter = CreateVoucherMixedSelector("receiptVoucherCostCenter", "مركز التكلفة");
        private readonly ComboBox partyAccount = CreateVoucherMixedSelector("receiptVoucherPartyAccount", "الحساب المقابل");
        private readonly TextBox partyName = CreateVoucherTextInput("receiptVoucherPartyName", "اسم الطرف");
        private readonly AntdUI.Input amount = CreateVoucherNumberInput("receiptVoucherAmount", "المبلغ");
        private readonly ComboBox currency = CreateVoucherMixedSelector("receiptVoucherCurrency", "العملة");
        private readonly AntdUI.Input exchangeRate = CreateVoucherNumberInput("receiptVoucherExchangeRate", "سعر التحويل", "1");
        private readonly AntdUI.Input localValue = CreateVoucherNumberInput("receiptVoucherLocalValue", "القيمة محلي", "0");
        private readonly ComboBox settlementAccount = CreateVoucherMixedSelector("receiptVoucherSettlementAccount", "حساب الصندوق أو الإيداع");
        private readonly ComboBox representative = CreateVoucherMixedSelector("receiptVoucherRepresentative", "المندوب");
        private readonly TextBox notes = CreateVoucherTextInput("receiptVoucherNotes", "البيان");
        private readonly AntdUI.Input chequeNumber = DesktopComponents.Input("receiptVoucherChequeNumber", "رقم الشيك");
        private readonly AntdUI.DatePicker chequeDate = DesktopComponents.DatePicker("receiptVoucherChequeDate", "تاريخ استحقاق الشيك", DateTime.Today);
        private readonly AntdUI.Select chequeStatus = DesktopComponents.Select("receiptVoucherChequeStatus", "حالة الشيك");
        private readonly TextBox chequeBank = CreateVoucherTextInput("receiptVoucherChequeBank", "البنك المسحوب عليه");
        private readonly DataGridView history = new DataGridView();
        private readonly TableLayoutPanel voucherEditor = new TableLayoutPanel();
        private readonly ThemedDocumentViewport voucherContentViewport = new ThemedDocumentViewport();
        private readonly Panel voucherContentHost = new Panel();
        private readonly ThemedDocumentViewport voucherSideViewport = new ThemedDocumentViewport();
        private readonly Panel voucherSideContent = new Panel();
        private readonly Panel chequePanel = new Panel();
        private readonly Label statusLabel = new Label();
        private string paymentMethod = "Cash";
        private Control paymentMethodsContainer;
        private int lastSavedVoucherId;
        private string lastSavedVoucherSummary = string.Empty;
        private string receiptSystemCurrencyCode;
        private bool? receiptPresentationDirection;
        internal bool? ReceiptPresentationDirection => receiptPresentationDirection;
        private int lookupLoadGeneration;
        private bool receiptInitialLoading, receiptOptionsReady;
        private System.Threading.CancellationTokenSource receiptInitialCancellation;
        private Func<bool> receiptOptionsCurrent;
        private void CancelReceiptInitialRead()
        {
            var pending = receiptInitialCancellation; receiptInitialCancellation = null;
            if (pending != null) { pending.Cancel(); pending.Dispose(); }
        }
        private Func<bool> CaptureReceiptOptionsContext()
        {
            if (!previewMode) { var session = ApplicationServices.CreateVoucherHistoryReadSession(); return () => session.IsCurrent; }
            long revision = SessionContext.CurrentSessionRevision;
            return () => SessionContext.CurrentSessionRevision == revision;
        }
        private void SetReceiptOptionsEnabled(bool enabled)
        {
            voucherEditor.Enabled = enabled;
            if(enabled)
            {
                history.Enabled=true;
                foreach(string name in new[]{"receiptVoucherSide","receiptVoucherToolbar"})
                    foreach(Control panel in Controls.Find(name,true))panel.Enabled=true;
            }
            foreach (var selector in new[] { partyAccount, settlementAccount, costCenter, currency }) selector.Enabled = enabled;
            foreach(string name in new[]{"receiptVoucherAction_Print_طباعة كاشير","receiptVoucherAction_Print_حفظ وطباعة"})
                foreach(Control action in Controls.Find(name,true))action.Enabled=enabled&&!voucherPrintLoading&&!receiptWriteBusy;
            ApplyReceiptRecoveryLock();
        }

        private int balanceLoadGeneration;
        private System.Threading.CancellationTokenSource receiptBalanceCancellation;
        private void CancelReceiptBalanceRead()
        {
            var pending=receiptBalanceCancellation; receiptBalanceCancellation=null;
            if(pending!=null){pending.Cancel();pending.Dispose();}
        }

        private readonly bool previewMode;
        private readonly PharmacyManagement.Application.IAccountBalanceReadService accountBalances;
        private readonly bool usePreviewBalance;
        private readonly PharmacyManagement.Application.IVoucherDocumentReadService voucherDocuments;
        private bool voucherPrintLoading;
        private int voucherDocumentGeneration;
        private System.Threading.CancellationTokenSource voucherDocumentCancellation;
        private void CancelVoucherDocumentRead()
        {
            voucherDocumentGeneration++;
            var pending=voucherDocumentCancellation;voucherDocumentCancellation=null;
            if(pending!=null){pending.Cancel();pending.Dispose();}
        }

        private bool receiptWriteBusy;
        private int historyLoadGeneration;
        private System.Threading.CancellationTokenSource receiptHistoryCancellation;
        private bool receiptHistoryLoading;
        private void CancelReceiptHistoryRead()
        {
            historyLoadGeneration++;receiptHistoryLoading=false;var pending=receiptHistoryCancellation;receiptHistoryCancellation=null;
            if(pending!=null){pending.Cancel();pending.Dispose();}
        }
        internal string SelectedReceiptHistoryText()
        {
            if(IsDisposed||Disposing||receiptInitialLoading||receiptHistoryLoading||!EnsureReceiptHistoryContext()||history.CurrentRow==null)return null;
            var row=history.CurrentRow.DataBoundItem as DataRowView;
            return row==null?null:string.Join("\t",row.Row.ItemArray.Select(value=>Convert.ToString(value,System.Globalization.CultureInfo.InvariantCulture)));
        }
        private bool EnsureReceiptHistoryContext()
        {
            if(receiptOptionsCurrent!=null&&receiptOptionsCurrent())return true;
            InvalidateReceiptEditorContext();return false;
        }


        private void InvalidateReceiptEditorContext()
        {
            if(IsDisposed||Disposing)return;
            ReleaseReceiptRecovery();
            receiptOptionsReady=false;receiptInitialLoading=true;
            CancelReceiptInitialRead();lookupLoadGeneration++;CancelReceiptHistoryRead();CancelReceiptBalanceRead();balanceLoadGeneration++;CancelVoucherDocumentRead();
            try
            {
                var previous=history.DataSource as DataTable;history.DataSource=null;previous?.Dispose();
                partyName.Clear();amount.Clear();notes.Clear();chequeNumber.Clear();chequeBank.Clear();voucherNo.Clear();
                exchangeRate.Text="1";localValue.Text="0";
                receiptSystemCurrencyCode=null;ApplyReceiptCurrencyPresentation();
                foreach(var selector in new[]{partyAccount,settlementAccount,costCenter,currency,representative})
                {selector.Items.Clear();selector.SelectedIndex=-1;selector.Text=string.Empty;}
                AntdDateInput.SetDate(voucherDate,DateTime.Today);AntdDateInput.SetDate(chequeDate,DateTime.Today);
                lastSavedVoucherSummary=string.Empty;
                SetReceiptOptionsEnabled(false);
                SetBalanceState("—",UiTheme.MutedText,"تغيرت جلسة السند؛ أعد فتح الشاشة.");
                statusLabel.Text=ReceiptText("تغيرت جلسة السند؛ تم إخفاء المسودة القديمة. أعد تحميل السند قبل المتابعة.");
                statusLabel.AccessibleDescription=statusLabel.Text;
            }
            finally{receiptInitialLoading=false;}
        }

        private static TextBox CreateVoucherTextInput(string name, string caption)
        {
            var editor = new TextBox { Name = name, AccessibleName = caption, BorderStyle = BorderStyle.None, Font = AppFonts.Create(10F) };
            editor.TextChanged += (sender, args) => UiDirection.ApplyContentDirection(editor, UiDirection.IsLeftToRight);
            return editor;
        }

        private static ComboBox CreateVoucherMixedSelector(string name, string caption) => new NativeThemedSelect
        { Name = name, AccessibleName = caption, DropDownStyle = ComboBoxStyle.DropDownList, Font = AppFonts.Create(10F) };

        private Control VoucherInputSurface(Control input)
        {
            // AntdUI 2.4.11 reverses Latin runs in these mixed Arabic values.
            // Keep Windows bidi rendering inside the same rounded AntdUI field shell.
            if (input is TextBox text)
            {
                text.Dock = DockStyle.None; text.MinimumSize = Size.Empty; text.AutoSize = true;
                return DesktopComponents.NativeTextFrame(text, input.Name + "Frame");
            }
            if (input is ComboBox selector)
            {
                selector.MinimumSize = Size.Empty;
                return DesktopComponents.NativeSelectFrame(selector, input.Name + "Frame", input.AccessibleName);
            }
            return input;
        }

        private void StyleVoucherMixedFields()
        {
            foreach (TextBox editor in new[] { voucherNo, partyName, notes, chequeBank })
                DesktopComponents.StyleNativeTextFrame((AntdUI.Panel)editor.Parent, editor);
            foreach (ComboBox selector in new[] { costCenter, partyAccount, currency, settlementAccount, representative })
                DesktopComponents.StyleNativeSelectFrame((AntdUI.Panel)selector.Parent, selector, UiDirection.IsLeftToRight);
        }

        private static AntdUI.Input CreateVoucherNumberInput(string name, string accessibleName, string text = "")
        {
            AntdUI.Input input = DesktopComponents.Input(name, accessibleName, text);
            input.RightToLeft = RightToLeft.No;
            input.TextAlign = HorizontalAlignment.Right;
            return input;
        }

        public ReceiptVoucherForm(string voucherType)
            : this(voucherType, false)
        {
        }

        internal ReceiptVoucherForm(string voucherType, bool previewMode)
            : this(voucherType, previewMode, null, true)
        {
        }

        internal ReceiptVoucherForm(string voucherType, bool previewMode,
            PharmacyManagement.Application.IAccountBalanceReadService accountBalances, bool usePreviewBalance)
            : this(voucherType, previewMode, accountBalances, usePreviewBalance, null)
        {
        }

        internal ReceiptVoucherForm(string voucherType, bool previewMode,
            PharmacyManagement.Application.IAccountBalanceReadService accountBalances, bool usePreviewBalance,
            PharmacyManagement.Application.IVoucherDocumentReadService voucherDocuments)
        {
            this.voucherDocuments = voucherDocuments;
            this.accountBalances = accountBalances;
            this.usePreviewBalance = usePreviewBalance;
            this.previewMode = previewMode;
            receiptOptionsReady = previewMode;
            receiptOptionsCurrent = CaptureReceiptOptionsContext();
            this.voucherType = string.IsNullOrWhiteSpace(voucherType) ? "ReceiptExternal" : voucherType;
            isReceipt = this.voucherType.IndexOf("Receipt", StringComparison.OrdinalIgnoreCase) >= 0;
            isInternal = this.voucherType.IndexOf("Internal", StringComparison.OrdinalIgnoreCase) >= 0;
            isMulti = this.voucherType.IndexOf("Multi", StringComparison.OrdinalIgnoreCase) >= 0;
            voucherDate.MinDate = new DateTime(1753, 1, 1);
            chequeDate.MinDate = new DateTime(1753, 1, 1);

            string voucherScope = isMulti ? " متعدد" : isInternal ? " داخلي" : " خارجي";
            Text = ReceiptText(isReceipt ? "سند قبض" : "سند صرف") + ReceiptText(voucherScope);
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = true;
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(1280, 760);
            MinimumSize = new Size(960, 600);
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            InitializeComponent();
            partyAccount.Enabled = false;
            settlementAccount.Enabled = false;
            costCenter.Enabled = false;
            currency.Enabled = false;
            UiTheme.Apply(this);
            ApplyVoucherSurfacePalette(this);
            ApplyVoucherHeaderTheme();
            ApplyVoucherFooterTheme();
            StyleVoucherMixedFields();
            statusLabel.TextChanged += (s, e) => FitVoucherStatusText();
            statusLabel.SizeChanged += (s, e) => FitVoucherStatusText();
            statusLabel.FontChanged += (s, e) => FitVoucherStatusText();
            statusLabel.Text = ReceiptText("جار تحميل بيانات السند...");
            SizeChanged += (s, e) => ApplyVoucherResponsiveLayout();
            ApplyVoucherResponsiveLayout();
            Activated += (sender,args) =>
            {
                if(receiptOptionsCurrent!=null&&!receiptOptionsCurrent())
                {
                    InvalidateReceiptEditorContext();
                }
            };
              history.ClipboardCopyMode=DataGridViewClipboardCopyMode.Disable;
              // The voucher grid keeps the right-to-left layout, so its empty caption is supplied
              // by the screen instead of following the grid direction.
              UiTheme.ApplyDataGridQuality(history, ReceiptText("لا توجد بيانات حالياً"));
            history.KeyDown+=(sender,args)=>{if(args.Control&&args.KeyCode==Keys.C){args.Handled=true;args.SuppressKeyPress=true;string text=SelectedReceiptHistoryText();if(text!=null)Clipboard.SetText(text);}};
            if (previewMode) { receiptSystemCurrencyCode="YER"; PopulatePreviewData(); }
            else Load += async (s, e) => await LoadInitialDataAsync();
            ApplyReceiptCurrencyPresentation();
            FormClosing += (s, e) =>
            {
                if (receiptWriteBusy && e.CloseReason == CloseReason.UserClosing && !(receiptRecovery != null && (receiptRecovery.Operation.Pending != null || receiptRecovery.Operation.Accepted != null)))
                { e.Cancel = true; statusLabel.Text = ReceiptText("انتظر اكتمال حفظ السند قبل إغلاق النافذة."); }
            };
            UiSessionSubscription.Attach(this,()=>{if(receiptOptionsCurrent!=null&&!receiptOptionsCurrent())InvalidateReceiptEditorContext();});
            Disposed += (s, e) => { ReleaseReceiptRecovery();CancelReceiptInitialRead(); CancelReceiptBalanceRead(); CancelVoucherDocumentRead(); CancelReceiptHistoryRead(); lookupLoadGeneration++; balanceLoadGeneration++; historyLoadGeneration++; };
            FormClosed += (s, e) =>
            {
                CancelReceiptInitialRead(); CancelReceiptBalanceRead(); CancelVoucherDocumentRead(); CancelReceiptHistoryRead();
                lookupLoadGeneration++;
                balanceLoadGeneration++;
                historyLoadGeneration++;
            };
        }

        private string GetNextVoucherNumberText()
        {
            return ReceiptText("تلقائي عند الحفظ");
        }

        private static void ApplyVoucherSurfacePalette(Control root)
        {
            if (root == null) return;
            foreach (Control control in root.Controls)
            {
                Panel panel = control as Panel;
                if (panel != null && string.Equals(Convert.ToString(panel.Tag), "UiSurface", StringComparison.Ordinal))
                    panel.BackColor = UiTheme.Surface;
                Label label = control as Label;
                if (label != null && label.Parent is Panel && string.Equals(Convert.ToString(label.Parent.Tag), "UiSurface", StringComparison.Ordinal))
                    label.ForeColor = string.Equals(label.Name, "receiptVoucherBalance", StringComparison.Ordinal) ? UiTheme.Primary : UiTheme.Text;
                ApplyVoucherSurfacePalette(control);
            }
        }

        private void FitVoucherStatusText()
        {
            if (statusLabel.IsDisposed || statusLabel.Width < 100) return;
            int textHeight = TextRenderer.MeasureText(statusLabel.Text, statusLabel.Font,
                new Size(statusLabel.ClientSize.Width, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.NoPadding).Height;
            int height = Math.Max(20, textHeight + 6);
            if (statusLabel.Height != height) statusLabel.Height = height;
        }

        internal void RestoreReceiptPresentation()
        {
            if (IsDisposed || Disposing) return;
            ApplyVoucherSurfacePalette(this);
            ApplyVoucherHeaderTheme();
            ApplyVoucherFooterTheme();
            StyleVoucherMixedFields();
            Control gap = voucherSideContent.Controls.Find("receiptVoucherSideGap", false).FirstOrDefault();
            if (gap != null) gap.BackColor = UiTheme.Background;
            ApplyVoucherResponsiveLayout();
        }
        private void ApplyVoucherFooterTheme()
        {
            Panel toolbar = Controls.Find("receiptVoucherToolbar", true).OfType<Panel>().FirstOrDefault();
            if (toolbar == null) return;
            toolbar.BackColor = UiTheme.Background;
            foreach (FlowLayoutPanel group in toolbar.Controls.OfType<FlowLayoutPanel>())
            {
                group.BackColor = UiTheme.Background;
                foreach (AntdUI.Button button in group.Controls.OfType<AntdUI.Button>())
                DesktopComponents.StyleOutlinedFooterAction(button);
            }
        }

        private void ApplyVoucherHeaderTheme()
        {
            Panel header = Controls.Find("receiptVoucherHeader", true).OfType<Panel>().FirstOrDefault();
            Label title = Controls.Find("receiptVoucherScreenTitle", true).OfType<Label>().FirstOrDefault();
            if (header == null || title == null) return;
            header.BackColor = UiTheme.Background;
            title.BackColor = UiTheme.Background;
            title.ForeColor = UiTheme.Text;
        }

        private void ApplyVoucherResponsiveLayout()
        {
            if (voucherEditor.IsDisposed || voucherEditor.RowStyles.Count != 6) return;
            Panel side = Controls.Find("receiptVoucherSide", true).OfType<Panel>().FirstOrDefault();
            if (side != null)
            {
                int sideWidth = ClientSize.Width < 1120 && !string.Equals(paymentMethod, "Cheque", StringComparison.OrdinalIgnoreCase)
                    ? 260 : 315;
                if (side.Width != sideWidth) side.Width = sideWidth;
            }
            int captionHeight=16;
            foreach(Panel field in voucherEditor.Controls.OfType<Panel>())
                foreach(Label caption in field.Controls.OfType<Label>())
                {
                    int width=Math.Max(96,field.ClientSize.Width-field.Padding.Horizontal);
                    int measured=TextRenderer.MeasureText(caption.Text,caption.Font,new Size(width,int.MaxValue),TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix).Height;
                    caption.Height=Math.Max(caption.Font.Height+4,measured+4);
                    captionHeight=Math.Max(captionHeight,caption.Height);
                }
            int fieldInsets=voucherEditor.Controls.OfType<Panel>().Select(field=>field.Margin.Vertical+field.Padding.Vertical).DefaultIfEmpty(0).Max();
            float rowHeight=Math.Max(68,captionHeight+44+fieldInsets);
            foreach (RowStyle row in voucherEditor.RowStyles) row.Height = rowHeight;
            foreach (Panel fieldBox in voucherEditor.Controls.OfType<Panel>())
            {
                Control field = fieldBox.Controls.Cast<Control>().FirstOrDefault(control => !(control is Label));
                if (field != null) field.MinimumSize = new Size(field.MinimumSize.Width, 44);
            }
            voucherEditor.Height = (int)(rowHeight * voucherEditor.RowStyles.Count) + voucherEditor.Padding.Vertical;
            voucherEditor.PerformLayout();
            UpdateVoucherContentHeight();
            UpdateVoucherSideContentHeight();
        }

        private bool arrangingVoucherSide;
        private void UpdateVoucherSideContentHeight()
        {
            if (arrangingVoucherSide || voucherSideViewport.IsDisposed || chequePanel.IsDisposed) return;
            arrangingVoucherSide = true;
            try
            {
                voucherSideViewport.SizeContent(voucherSideContent, 126);
                chequePanel.PerformLayout();
                foreach (Label caption in chequePanel.Controls.OfType<Label>())
                {
                    int width = Math.Max(96, caption.ClientSize.Width);
                    int measured = TextRenderer.MeasureText(caption.Text, caption.Font, new Size(width, int.MaxValue),
                        TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height;
                    caption.Height = Math.Max(caption.Name == "receiptVoucherChequeTitle" ? 42 : 18,
                        Math.Max(caption.Font.Height + 4, measured + 4));
                }
                chequePanel.Height = chequePanel.Padding.Vertical + chequePanel.Controls.Cast<Control>().Sum(control => control.Height);
                bool cheque = string.Equals(paymentMethod, "Cheque", StringComparison.OrdinalIgnoreCase);
                Control gap = voucherSideContent.Controls.Find("receiptVoucherSideGap", false).FirstOrDefault();
                if (gap != null) gap.Visible = cheque;
                Control balance = voucherSideContent.Controls.Find("receiptVoucherBalancePanel", false).FirstOrDefault();
                int height = (balance == null ? 126 : balance.Height) + (cheque ? chequePanel.Height + 16 : 0);
                voucherSideViewport.SizeContent(voucherSideContent, height);
            }
            finally { arrangingVoucherSide = false; }
        }

        private void UpdateVoucherContentHeight()
        {
            if (voucherContentViewport.IsDisposed || voucherContentHost.IsDisposed) return;
            voucherContentViewport.SizeContent(voucherContentHost, voucherEditor.Height + 26 + 120);
        }


        private void PopulatePreviewData()
        {
            if (branch.Items.Count == 0) branch.Items.Add("الفرع الرئيسي");
            branch.SelectedIndex = 0;
            ApplyAccountOptions(partyAccount, new[] { "1100 - العملاء", "2100 - الموردون" }, "1100");
            ApplyAccountOptions(settlementAccount, new[] { "1000 - الصندوق", "1010 - البنك" }, "1000");
            if (costCenter.Items.Count == 0) costCenter.Items.Add("بدون");
            costCenter.SelectedIndex = 0;
            if (currency.Items.Count == 0) currency.Items.Add("YER - ر.ي");
            currency.SelectedIndex = 0;
            if (representative.Items.Count == 0) representative.Items.Add("مستخدم المعاينة");
            representative.SelectedIndex = 0;
            partyAccount.Enabled = settlementAccount.Enabled = costCenter.Enabled = currency.Enabled = true;
            partyName.Text = "عميل تجريبي";
            amount.Text = "1,250.50";
            exchangeRate.Text = "1";
            localValue.Text = "1,250.50";
            notes.Text = "بيانات توضيحية للمعاينة فقط";
            balanceLabel.Text = "2,500.00 ر.ي";
            statusLabel.Text = ReceiptText("وضع معاينة — البيانات محلية ولا يتم حفظها.");

            DataTable previewHistory = new DataTable();
            previewHistory.Columns.Add("رقم السند");
            previewHistory.Columns.Add("التاريخ");
            previewHistory.Columns.Add("الطرف");
            previewHistory.Columns.Add("المبلغ");
            previewHistory.Columns.Add("البيان");
            previewHistory.Rows.Add("PREVIEW-01", DateTime.Today.ToString("yyyy/MM/dd"), "عميل تجريبي", "1,250.50", "سجل تجريبي غير محفوظ");
            history.DataSource = previewHistory;
        }

        private string GetPartyNameLabel()
        {
            if (isInternal)
            {
                return isReceipt ? "استلمت من حساب/موظف" : "صرف إلى حساب/موظف";
            }

            return isReceipt ? "استلمت من السيد" : "صرف إلى السيد";
        }

        private Control BuildBottomToolbar()
        {
            Panel toolbar = new Panel
            {
                Name = "receiptVoucherToolbar",
                Dock = DockStyle.Bottom,
                Height = 64,
                BackColor = UiTheme.PrimaryDark
            };

            FlowLayoutPanel rightActions = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                Width = 440,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(8),
                BackColor = Color.Transparent
            };
            rightActions.Controls.Add(CreateToolbarButton("طباعة كاشير", AppIcon.Print, PrintCashierVoucher, 142, UiTheme.Secondary, allowInPreview: false));
            rightActions.Controls.Add(CreateToolbarButton("حفظ وطباعة", AppIcon.Print, SaveAndPrintVoucher, 142, UiTheme.Secondary, allowInPreview: false));
            rightActions.Controls.Add(CreateToolbarButton("حفظ", AppIcon.Save, async () => { await SaveVoucherAsync(); }, 112, UiTheme.Primary, allowInPreview: false));

            FlowLayoutPanel leftActions = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(8),
                BackColor = Color.Transparent
            };
            leftActions.Controls.Add(CreateToolbarButton("خروج", AppIcon.Close, () => Close(), 82, UiTheme.Danger));
            leftActions.Controls.Add(CreateToolbarButton("بحث", AppIcon.Search, FocusHistorySearch, 82, UiTheme.Primary));
            leftActions.Controls.Add(CreateToolbarButton("جديد", AppIcon.Add, NewVoucher, 82, UiTheme.Primary));
            leftActions.Controls.Add(CreateToolbarButton(string.Empty, AppIcon.Back, () => MoveHistorySelection(true, true), 44, UiTheme.PrimaryDark, "السند الأول"));
            leftActions.Controls.Add(CreateToolbarButton(string.Empty, AppIcon.Back, () => MoveHistorySelection(false, true), 44, UiTheme.PrimaryDark, "السند السابق"));
            leftActions.Controls.Add(CreateToolbarButton(string.Empty, AppIcon.Forward, () => MoveHistorySelection(false, false), 44, UiTheme.PrimaryDark, "السند التالي"));
            leftActions.Controls.Add(CreateToolbarButton(string.Empty, AppIcon.Forward, () => MoveHistorySelection(true, false), 44, UiTheme.PrimaryDark, "السند الأخير"));

            toolbar.Controls.Add(leftActions);
            toolbar.Controls.Add(rightActions);
            return toolbar;
        }

        private AntdUI.Button CreateToolbarButton(string text, AppIcon icon, Action action, int width, Color backColor, string accessibleName = null, bool allowInPreview = true)
        {
            AntdUI.Button button = new AccessibleAntdButton
            {
                Name = "receiptVoucherAction_" + icon.ToString() + "_" + (string.IsNullOrWhiteSpace(text) ? accessibleName : text),
                Text = text,
                Width = width,
                Height = 44,
                Margin = new Padding(4, 2, 4, 2),
                Font = AppFonts.Create(9.5F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter,
                UseMnemonic = false,
                AccessibleName = string.IsNullOrWhiteSpace(accessibleName) ? text : accessibleName,
                AccessibleDescription = "إجراء سند القبض أو الصرف",
                Enabled = !previewMode || allowInPreview,
                RightToLeft = RightToLeft.Yes,
                IconSvg = OutlinedIcons.Svg(icon, true),
                IconSize = new Size(width <= 48 ? 18 : 20, width <= 48 ? 18 : 20),
                IconPosition = string.IsNullOrWhiteSpace(text) ? AntdUI.TAlignMini.Top : AntdUI.TAlignMini.Right,
                Type = backColor == UiTheme.Danger ? AntdUI.TTypeMini.Error
                    : backColor == UiTheme.Primary || backColor == UiTheme.Secondary ? AntdUI.TTypeMini.Primary : AntdUI.TTypeMini.Default,
                BorderWidth = 1
            };
            AntdThemeAdapter.Apply(button);
            button.Click += (s, e) => action();
            return button;
        }

        private Control BuildBalancePanel()
        {
            Panel panel = new Panel { Name = "receiptVoucherBalancePanel", Tag = "UiSurface", Dock = DockStyle.Top, Height = 126, Margin = new Padding(0, 0, 0, 16), BackColor = UiTheme.Surface };
            WinFormsShapeAdapter.ApplyCard(panel);
            Label caption = new Label
            {
                Text = "رصيد الحساب حتى تاريخ السند",
                Dock = DockStyle.Top,
                Height = 48,
                TextAlign = ContentAlignment.BottomCenter,
                Font = AppFonts.Create(10F, FontStyle.Bold)
            };
            balanceLabel.Name = "receiptVoucherBalance";
            balanceLabel.Text = "—";
            balanceLabel.ForeColor = UiTheme.Primary;
            balanceLabel.Dock = DockStyle.Fill;
            balanceLabel.TextAlign = ContentAlignment.TopCenter;
            balanceLabel.Font = AppFonts.Create(16F, FontStyle.Regular);
            panel.Controls.Add(balanceLabel);
            panel.Controls.Add(caption);
            return panel;
        }

        private Control BuildChequePanel()
        {
            Panel panel = chequePanel;
            panel.Name = "receiptVoucherChequePanel";
            panel.Tag = "UiSurface";
            panel.Dock = DockStyle.Top;
            panel.Height = 330;
            panel.BackColor = UiTheme.Surface;
            panel.Padding = new Padding(26, 18, 26, 18);
            WinFormsShapeAdapter.ApplyCard(panel);
            Label title = new Label
            {
                Name = "receiptVoucherChequeTitle",
                Text = "بيانات الشيك",
                Dock = DockStyle.Top,
                Height = 42,
                TextAlign = ContentAlignment.MiddleRight,
                Font = AppFonts.Create(18F, FontStyle.Regular)
            };
            chequeStatus.Items.Add("تحت التحصيل");
            chequeStatus.Items.Add("محصل");
            chequeStatus.Items.Add("مرتجع");
            chequeStatus.SelectedIndex = 0;
            AddStackField(panel, "الشيك مسحوب على بنك", chequeBank);
            AddStackField(panel, "حالة الشيك", chequeStatus);
            AddStackField(panel, "تاريخ استحقاق الشيك", chequeDate);
            AddStackField(panel, "رقم الشيك", chequeNumber);
            title.FontChanged += (s, e) => UpdateVoucherSideContentHeight();
            title.SizeChanged += (s, e) => UpdateVoucherSideContentHeight();
            panel.Controls.Add(title);
            return panel;
        }

        private AntdUI.Button CreateMethodButton(string text, string value, bool selected)
        {
            AntdUI.Button button = new AccessibleAntdButton
            {
                Name = "receiptVoucherMethod_" + value,
                Text = text,
                Tag = value,
                Width = 96,
                Height = 44,
                Margin = new Padding(6),
                Cursor = Cursors.Hand,
                AccessibleName = text,
                AccessibleDescription = "اختيار طريقة دفع السند",
                RightToLeft = RightToLeft.Yes,
                Type = selected ? AntdUI.TTypeMini.Primary : AntdUI.TTypeMini.Default
            };
            button.Click += (s, e) =>
            {
                SetPaymentMethod(Convert.ToString(button.Tag), button.Parent);
            };
            AntdThemeAdapter.Apply(button);
            return button;
        }

        private void SetPaymentMethod(string methodName, Control methodsContainer)
        {
            paymentMethod = string.IsNullOrWhiteSpace(methodName) ? "Cash" : methodName;

            if (methodsContainer != null)
            {
                foreach (Control control in methodsContainer.Controls)
                {
                    AntdUI.Button method = control as AntdUI.Button;
                    if (method == null) continue;
                    bool isSelected = string.Equals(Convert.ToString(method.Tag), paymentMethod, StringComparison.OrdinalIgnoreCase)||
                        (paymentMethod=="Transfer"&&Convert.ToString(method.Tag)=="Bank");
                    method.Type = isSelected ? AntdUI.TTypeMini.Primary : AntdUI.TTypeMini.Default;
                    method.AccessibleDescription = method.RightToLeft == RightToLeft.No
                        ? (isSelected ? "Selected payment method" : "Choose payment method")
                        : (isSelected ? "طريقة الدفع المختارة" : "اختيار طريقة دفع السند");
                    AntdThemeAdapter.Apply(method);
                }
            }

            bool chequeMode = string.Equals(paymentMethod, "Cheque", StringComparison.OrdinalIgnoreCase);
            SetChequeInputsEnabled(chequeMode);
            statusLabel.Text = chequeMode
                ? "طريقة الدفع شيك: رقم الشيك والبنك مطلوبان قبل الحفظ."
                : "طريقة الدفع الحالية: " + GetPaymentMethodDisplay(paymentMethod);
        }

        private void SetChequeInputsEnabled(bool enabled)
        {
            chequePanel.Visible = enabled;
            chequePanel.Enabled = enabled;
            foreach (Control control in chequePanel.Controls)
            {
                if (!(control is Label))
                {
                    control.Enabled = enabled;
                }
            }

            chequePanel.BackColor = enabled ? UiTheme.Surface : UiTheme.SurfaceVariant;
            ApplyVoucherResponsiveLayout();
        }

        private void NewVoucher()
        {
            if(receiptRecovery!=null&&(receiptRecovery.Operation.Pending!=null||receiptRecovery.Mismatch))
            {if(receiptRecovery.Operation.Accepted!=null&&!receiptRecovery.Mismatch&&receiptRecovery.Operation.RetryAcceptedCleanup()){}else{ApplyReceiptRecoveryLock();return;}}
            partyName.Clear();
            amount.Clear();
            localValue.Text = "0";
            exchangeRate.Text = "1";
            notes.Clear();
            chequeNumber.Clear();
            chequeBank.Clear();
            AntdDateInput.SetDate(chequeDate, DateTime.Today);
            AntdDateInput.SetDate(voucherDate, DateTime.Today);
            voucherNo.Text = GetNextVoucherNumberText();
            SetPaymentMethod("Cash", paymentMethodsContainer);
            lastSavedVoucherId = 0;
            lastSavedVoucherSummary = string.Empty;
            UpdateBalance();
            statusLabel.Text = ReceiptText("تم تجهيز سند جديد.");
        }

        private void FocusHistorySearch()
        {
            if (history.Rows.Count > 0)
            {
                history.Focus();
                history.ClearSelection();
                history.Rows[0].Selected = true;
                history.CurrentCell = history.Rows[0].Cells[0];
                CaptureSelectedHistoryVoucher();
            }

            if (lastSavedVoucherId <= 0)
            {
                statusLabel.Text = ReceiptText("استخدم سجل آخر السندات للمراجعة والتنقل.");
            }
        }

        private void MoveHistorySelection(bool edge, bool backwards)
        {
            if (history.Rows.Count == 0)
            {
                statusLabel.Text = ReceiptText("لا توجد سندات للتنقل.");
                return;
            }

            int index = history.CurrentCell == null ? 0 : history.CurrentCell.RowIndex;
            if (edge)
            {
                index = backwards ? 0 : history.Rows.Count - 1;
            }
            else
            {
                index += backwards ? -1 : 1;
                index = Math.Max(0, Math.Min(history.Rows.Count - 1, index));
            }

            history.ClearSelection();
            history.CurrentCell = history.Rows[index].Cells[0];
            history.Rows[index].Selected = true;
            CaptureSelectedHistoryVoucher();
        }

        private async void SaveAndPrintVoucher()
        {
            int acceptedId = await SaveVoucherAsync();
            if (acceptedId > 0) PrintCashierVoucherAsync(acceptedId);
        }

        internal int ResolveVoucherPrintId(int? documentId)
        {
            if(receiptOptionsCurrent==null||!receiptOptionsCurrent())
            {InvalidateReceiptEditorContext();throw new UnauthorizedAccessException("تغيرت جلسة طباعة السند.");}
            int id = documentId ?? lastSavedVoucherId;
            if (id <= 0) throw new InvalidOperationException("اختر سندًا محفوظًا صالحًا للطباعة.");
            return id;
        }

        internal Func<bool> CreateVoucherPrintContext(Func<bool> optionsContext,int requestedId,bool explicitId)
        {
            var session=previewMode?null:ApplicationServices.CreateVoucherDocumentReadSession();
            return ()=>!IsDisposed&&!Disposing&&receiptOptionsReady&&optionsContext!=null&&ReferenceEquals(optionsContext,receiptOptionsCurrent)&&ReportPreviewLifetime.ContextCurrent(optionsContext)&&(session==null||session.IsCurrent)&&(explicitId||lastSavedVoucherId==requestedId);
        }
        private void PrintCashierVoucher() => PrintCashierVoucherAsync(null);

        private async void PrintCashierVoucherAsync(int? documentId) => await OpenVoucherPrintAsync(documentId);

        internal async System.Threading.Tasks.Task<bool> OpenVoucherPrintAsync(int? documentId,
            Func<PharmacyManagement.Application.VoucherDocumentSnapshot,bool> present=null)
        {
            if(previewMode&&present==null){statusLabel.Text=ReceiptText("المعاينة للعرض فقط؛ لا يمكن طباعة سند تجريبي.");return false;}
            if(voucherPrintLoading||receiptWriteBusy||IsDisposed||Disposing)return false;
            Func<bool> current=receiptOptionsCurrent;
            if(!receiptOptionsReady||current==null||!current())
            {statusLabel.Text=ReceiptText("أعد فتح السند لتحميل بيانات الجلسة الحالية.");return false;}
            if(!documentId.HasValue){if(receiptHistoryLoading||!EnsureReceiptHistoryContext())return false;CaptureSelectedHistoryVoucher();}
            if(!documentId.HasValue&&lastSavedVoucherId<=0)
            {statusLabel.Text=ReceiptText("لم يتم اختيار سند للطباعة.");return false;}
            int requestedId=ResolveVoucherPrintId(documentId);
            var print=Controls.Find("receiptVoucherAction_Print_طباعة كاشير",true).OfType<AntdUI.Button>().First();
            var savePrint=Controls.Find("receiptVoucherAction_Print_حفظ وطباعة",true).OfType<AntdUI.Button>().First();
            bool printEnabled=print.Enabled,savePrintEnabled=savePrint.Enabled;
            voucherPrintLoading=true;print.Enabled=savePrint.Enabled=false;statusLabel.Text=ReceiptText("جار قراءة السند المحفوظ للطباعة...");
            try
            {
                var document=await ReadVoucherPrintDocumentAsync(requestedId);
                if(IsDisposed||Disposing||!current())return false;
                if(!documentId.HasValue&&lastSavedVoucherId!=requestedId)
                {statusLabel.Text=ReceiptText("تغير السند المحدد؛ اطلب طباعة السند المطلوب مجددًا.");return false;}
                bool opened=present!=null?present(document):DataTablePrintPreview.Show(this,GetVoucherTypeDisplay(document.Type)+" رقم "+document.VoucherId,
                    VoucherTableProjection.PrintSubtitle(document,UiDirection.IsLeftToRight),CreateVoucherPrintTable(document),null,UiDirection.IsLeftToRight,isCurrent:CreateVoucherPrintContext(current,requestedId,documentId.HasValue));
                if(IsDisposed||Disposing||!current())return false;
                if(opened)statusLabel.Text=ReceiptText("تم فتح معاينة طباعة السند رقم ")+requestedId+".";
                return opened;
            }
            catch(OperationCanceledException)
            {if(!IsDisposed&&!Disposing&&current())statusLabel.Text=ReceiptText("أُلغيت قراءة السند للطباعة.");return false;}
            catch(Exception error)
            {
                if(IsDisposed||Disposing)return false;
                if(!current())statusLabel.Text=ReceiptText("تغيرت جلسة السند؛ أعد فتح الشاشة.");
                else{statusLabel.Text=ReceiptText("تعذر قراءة السند رقم ")+requestedId+ReceiptText("؛ لم تُنشأ معاينة طباعة.");if(!previewMode)statusLabel.AccessibleDescription=statusLabel.Text+" "+AppDiagnostics.Log("VoucherPrintRead",error);}
                return false;
            }
            finally
            {
                voucherPrintLoading=false;
                if(!IsDisposed&&!Disposing)
                {bool enabled=current()&&receiptOptionsReady;print.Enabled=printEnabled&&enabled;savePrint.Enabled=savePrintEnabled&&enabled;}
            }
        }

        private void AddCell(TableLayoutPanel form, string label, Control control, int col, int row)
        {
            form.Controls.Add(CreateFieldBox(label, control), col, row);
        }

        private void AddWideCell(TableLayoutPanel form, string label, Control control, int col, int row, int span)
        {
            Panel box = CreateFieldBox(label, control);
            form.Controls.Add(box, col, row);
            form.SetColumnSpan(box, span);
        }

        private Panel CreateFieldBox(string label, Control control)
        {
            Panel panel = new Panel { Tag = "UiSurface", Dock = DockStyle.Fill, Padding = new Padding(1) };
            Label caption = new Label
            {
                Name=control.Name+"Caption",
                AccessibleName=label,
                Text = label,
                Dock = DockStyle.Top,
                Height = 16,
                TextAlign = ContentAlignment.MiddleRight,
                ForeColor = UiTheme.PrimaryDark,
                Font = AppFonts.Create(9F, FontStyle.Regular)
            };
            ApplyVoucherControlDirection(control);
            control.AutoSize = false;
            control.MinimumSize = new Size(control.MinimumSize.Width, 44);
            control.Dock = DockStyle.Fill;
            control.Margin = Padding.Empty;
            control.Font = AppFonts.Create(10F, FontStyle.Regular);
            UiTheme.ApplyTo(control);
            Control surface = VoucherInputSurface(control);
            surface.Dock = DockStyle.Fill;
            surface.MinimumSize = new Size(surface.MinimumSize.Width, 44);
            panel.Controls.Add(surface);
            panel.Controls.Add(caption);
            return panel;
        }

        private void ApplyVoucherControlDirection(Control control)
        {
            if (control == null)
            {
                return;
            }

            AntdUI.Input textBox = control as AntdUI.Input;
            if (textBox != null) return;

            control.RightToLeft = RightToLeft.Yes;

            AntdUI.Select comboBox = control as AntdUI.Select;
            if (comboBox != null)
            {
                comboBox.RightToLeft = RightToLeft.Yes;
                return;
            }

            DateTimePicker picker = control as DateTimePicker;
            if (picker != null)
            {
                picker.RightToLeft = RightToLeft.Yes;
                picker.RightToLeftLayout = true;
            }
        }

        private void AddStackField(Panel panel, string labelText, Control control)
        {
            ApplyVoucherControlDirection(control);
            control.Dock = DockStyle.Top;
            control.Height = 44;
            control.Font = AppFonts.Create(9.5F, FontStyle.Regular);
            Label label = new Label
            {
                Name = control.Name + "Caption",
                Text = labelText,
                Dock = DockStyle.Top,
                Height = 18,
                ForeColor = UiTheme.MutedText,
                TextAlign = ContentAlignment.MiddleRight
            };
            label.FontChanged += (s, e) => UpdateVoucherSideContentHeight();
            label.SizeChanged += (s, e) => UpdateVoucherSideContentHeight();
            Control surface = VoucherInputSurface(control);
            surface.Dock = DockStyle.Top;
            surface.Height = 44;
            panel.Controls.Add(surface);
            panel.Controls.Add(label);
            control.TabIndex = control == chequeNumber ? 0 : control == chequeDate ? 1 : control == chequeStatus ? 2 : 3;
            surface.TabIndex = control.TabIndex;

        }

        private async System.Threading.Tasks.Task LoadInitialDataAsync() => await LoadReceiptInitialAsync();

        internal async System.Threading.Tasks.Task<bool> LoadReceiptInitialAsync(
            Func<System.Threading.CancellationToken, System.Threading.Tasks.Task<ReceiptVoucherInitialDataSnapshot>> read = null)
        {
            if (receiptWriteBusy || voucherPrintLoading || IsDisposed || Disposing) return false;
            int generation = ++lookupLoadGeneration;
            CancelReceiptInitialRead();
            var cancellation = receiptInitialCancellation = new System.Threading.CancellationTokenSource();
            var token = cancellation.Token;
            Func<bool> current = CaptureReceiptOptionsContext();
            receiptOptionsReady = false; receiptInitialLoading = true; CancelReceiptHistoryRead(); CancelReceiptBalanceRead(); balanceLoadGeneration++;
            SetReceiptOptionsEnabled(false);
            if (receiptOptionsCurrent != null && !receiptOptionsCurrent()) NewVoucher();
            SetBalanceState("—", UiTheme.MutedText, "جار تحميل خيارات السند...");
            statusLabel.Text = ReceiptText("جار تحميل بيانات السند...");
            ReceiptVoucherInitialDataSnapshot snapshot = null;
            bool bound = false;
            try
            {
                if (!current()) throw new UnauthorizedAccessException("تغيرت جلسة خيارات السند.");
                snapshot = await (read == null ? ReceiptVoucherWorkspaceService.LoadInitialAsync(voucherType, token) : read(token));
                if (IsDisposed || Disposing || generation != lookupLoadGeneration || token.IsCancellationRequested) return false;
                if (!current()) throw new UnauthorizedAccessException("تغيرت جلسة خيارات السند.");
                if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.SystemCurrencyCode) ||
                    !snapshot.Currencies.Any(code => string.Equals(code, snapshot.SystemCurrencyCode, StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException("عملة النظام غير متاحة؛ تعذر تجهيز السند.");
                ApplyAccountOptions(partyAccount, snapshot.Accounts, isReceipt ? AccountingService.CustomersAccount : AccountingService.SuppliersAccount);
                ApplyAccountOptions(settlementAccount, snapshot.Accounts, AccountingService.CashAccount);
                costCenter.Items.Clear(); costCenter.Items.Add("بدون");
                foreach (string item in snapshot.CostCenters) costCenter.Items.Add(item);
                costCenter.SelectedIndex = 0;
                currency.Items.Clear();
                foreach (string item in snapshot.Currencies) if (!currency.Items.Contains(item)) currency.Items.Add(item);
                currency.SelectedIndex = -1;
                for (int i = 0; i < currency.Items.Count; i++)
                    if (string.Equals(Convert.ToString(currency.Items[i]), snapshot.SystemCurrencyCode, StringComparison.OrdinalIgnoreCase))
                    { currency.SelectedIndex = i; break; }
                var previous = history.DataSource as DataTable;
                history.DataSource = snapshot.History; bound = true;
                if (previous != snapshot.History) previous?.Dispose();
                receiptSystemCurrencyCode=snapshot.SystemCurrencyCode.Trim().ToUpperInvariant();
                ApplyReceiptCurrencyPresentation();
                receiptOptionsCurrent = current; receiptOptionsReady = true;
                SetReceiptOptionsEnabled(true);
                statusLabel.Text = ReceiptText("تم تجهيز سند جديد.");
                if(!previewMode&&read==null)InitializeReceiptRecovery();
                return true;
            }
            catch (Exception ex)
            {
                if (IsDisposed || Disposing || generation != lookupLoadGeneration || token.IsCancellationRequested) return false;
                foreach (var selector in new[] { partyAccount, settlementAccount, costCenter, currency })
                { selector.Items.Clear(); selector.SelectedIndex = -1; selector.Text = string.Empty; }
                var previous = history.DataSource as DataTable; history.DataSource = null; previous?.Dispose();
                SetReceiptOptionsEnabled(false);
                statusLabel.Text = ReceiptText("تعذر تحميل بيانات السند. تحقق من الاتصال ثم أعد فتح الشاشة.");
                if (!previewMode) statusLabel.AccessibleDescription = statusLabel.Text + " " + AppDiagnostics.Log("ReceiptVoucherInitialRead", ex);
                return false;
            }
            finally
            {
                if (!bound) snapshot?.History.Dispose();
                if (generation == lookupLoadGeneration)
                {
                    receiptInitialLoading = false;
                    if (ReferenceEquals(receiptInitialCancellation, cancellation)) receiptInitialCancellation = null;
                    cancellation.Dispose();
                    if (receiptOptionsReady && !IsDisposed && !Disposing) UpdateBalance();
                }
            }
        }

        private void ApplyAccountOptions(ComboBox combo, IEnumerable<string> options, string preferredAccountCode)
        {
            combo.Items.Clear();
            foreach (string item in options) combo.Items.Add(item);
            SelectAccount(combo, preferredAccountCode);
        }
        private void SelectAccount(ComboBox combo, string accountCode)
        {
            for (int i = 0; i < combo.Items.Count; i++)
            {
                if (Convert.ToString(combo.Items[i]).StartsWith(accountCode + " - ", StringComparison.Ordinal))
                {
                    combo.SelectedIndex = i;
                    return;
                }
            }
            if (combo.Items.Count > 0) combo.SelectedIndex = 0;
        }

        private string SelectedAccount(ComboBox combo)
        {
            string value = combo.Text;
            int dash = value.IndexOf(" - ", StringComparison.Ordinal);
            return dash > 0 ? value.Substring(0, dash).Trim() : value.Trim();
        }

        private int? SelectedCostCenterId()
        {
            string value = costCenter.Text;
            int dash = value.IndexOf(" - ", StringComparison.Ordinal);
            int id;
            if (dash > 0 && int.TryParse(value.Substring(0, dash), out id))
            {
                return id;
            }

            return null;
        }

        // The list captions follow the text direction of the screen, so they are refreshed
        // whenever the presentation changes.
        private void RefreshReceiptListCaptions()
        {
            ReplaceListItem(branch, 0, "الفرع الرئيسي");
            ReplaceListItem(costCenter, 0, "بدون");
            ReplaceListItem(representative, 0, "مستخدم المعاينة");
            ReplaceListItem(chequeStatus, 0, "تحت التحصيل");
            ReplaceListItem(chequeStatus, 1, "محصل");
            ReplaceListItem(chequeStatus, 2, "مرتجع");
            const string autoNumber = "تلقائي عند الحفظ";
            if (voucherNo != null && (string.Equals(voucherNo.Text, autoNumber, StringComparison.Ordinal) ||
                string.Equals(voucherNo.Text, ReceiptText(autoNumber), StringComparison.Ordinal)))
            {
                voucherNo.Text = ReceiptText(autoNumber);
            }
        }

        // The voucher screen keeps the right-to-left layout, so the shared language pass never
        // runs on it. Its plain captions are translated here, while value labels stay untouched.
        private void TranslateReceiptCaptions()
        {
            bool english = receiptPresentationDirection ?? (RightToLeft == RightToLeft.No);
            if (!english) return;
            if (history.AccessibleName == null || history.AccessibleName.Length == 0)
            {
                history.AccessibleName = ReceiptText("سجل السندات");
                history.AccessibleDescription = ReceiptText("السجل المحفوظ لسندات القبض والصرف");
            }
            foreach (Control control in AllControls(this))
            {
                if (control.AccessibleName != null && control.AccessibleName.Length > 0)
                {
                    string translatedName = UiLanguageCatalog.Translate(control.AccessibleName);
                    // Only a changed value is written: an unchanged assignment forces a layout pass.
                    if (!string.Equals(translatedName, control.AccessibleName, StringComparison.Ordinal))
                        control.AccessibleName = translatedName;
                }
                Label label = control as Label;
                if (label != null && !(label is BusinessValueLabel) && !string.IsNullOrWhiteSpace(label.Text))
                {
                    string translatedText = UiLanguageCatalog.Translate(label.Text);
                    if (!string.Equals(translatedText, label.Text, StringComparison.Ordinal))
                        label.Text = translatedText;
                    continue;
                }
                ButtonBase button = control as ButtonBase;
                if (button != null && !string.IsNullOrWhiteSpace(button.Text))
                {
                    string translatedText = UiLanguageCatalog.Translate(button.Text);
                    if (!string.Equals(translatedText, button.Text, StringComparison.Ordinal))
                        button.Text = translatedText;
                    continue;
                }
                AntdUI.Button antdButton = control as AntdUI.Button;
                if (antdButton != null && !string.IsNullOrWhiteSpace(antdButton.Text))
                {
                    string translatedText = UiLanguageCatalog.Translate(antdButton.Text);
                    if (!string.Equals(translatedText, antdButton.Text, StringComparison.Ordinal))
                        antdButton.Text = translatedText;
                }
            }
        }

        private static IEnumerable<Control> AllControls(Control root)
        {
            foreach (Control child in root.Controls)
            {
                yield return child;
                foreach (Control nested in AllControls(child)) yield return nested;
            }
        }

        private void ReplaceListItem(AntdUI.Select list, int index, string arabic)
        {
            if (list == null || list.Items.Count <= index) return;
            string current = Convert.ToString(list.Items[index]);
            string localized = ReceiptText(arabic);
            if (string.Equals(current, localized, StringComparison.Ordinal)) return;
            if (!string.Equals(current, arabic, StringComparison.Ordinal)) return;
            var values = new List<string>();
            foreach (object item in list.Items) values.Add(Convert.ToString(item));
            values[index] = localized;
            int selected = list.SelectedIndex;
            list.Items.Clear();
            foreach (string value in values) list.Items.Add(value);
            list.SelectedIndex = selected;
        }

        private void ReplaceListItem(ComboBox list, int index, string arabic)
        {
            if (list == null || list.Items.Count <= index) return;
            string current = Convert.ToString(list.Items[index]);
            string localized = ReceiptText(arabic);
            if (string.Equals(current, localized, StringComparison.Ordinal)) return;
            if (!string.Equals(current, arabic, StringComparison.Ordinal)) return;
            int selected = list.SelectedIndex;
            list.Items[index] = localized;
            list.SelectedIndex = selected;
        }

        internal void ApplyReceiptCurrencyPresentation(bool? leftToRight=null)
        {
            if(leftToRight.HasValue) receiptPresentationDirection=leftToRight.Value;
            bool english=receiptPresentationDirection??UiDirection.IsLeftToRight;
            // The voucher grid keeps the right-to-left layout, so its empty caption follows the
            // text direction of the screen rather than the grid direction.
            UiTheme.ApplyDataGridQuality(history, ReceiptText("لا توجد بيانات حالياً"));
            var historyTitle = Controls.Find("receiptVoucherHistoryTitle", true).OfType<Label>().FirstOrDefault();
            if (historyTitle != null) historyTitle.Text = ReceiptText("آخر السندات");
            foreach (Control control in AllControls(this))
            {
                var methodButton = control as AntdUI.Button;
                if (methodButton == null || methodButton.Name == null ||
                    !methodButton.Name.StartsWith("receiptVoucherMethod_", StringComparison.Ordinal)) continue;
                methodButton.Text = ReceiptText(methodButton.AccessibleName);
                methodButton.AccessibleName = methodButton.Text;
            }
            RefreshReceiptListCaptions();
            TranslateReceiptCaptions();
            var caption=Controls.Find("receiptVoucherLocalValueCaption",true).OfType<Label>().FirstOrDefault();
            if(caption==null)return;
            string title=english?"Base amount":"قيمة النظام";
            caption.Text=title+(string.IsNullOrWhiteSpace(receiptSystemCurrencyCode)?string.Empty:" ("+receiptSystemCurrencyCode+")");
            caption.AccessibleName=caption.Text;caption.RightToLeft=RightToLeft.No;
            caption.TextAlign=english?ContentAlignment.MiddleLeft:ContentAlignment.MiddleRight;
            localValue.AccessibleName=caption.Text;
            localValue.AccessibleDescription=string.IsNullOrWhiteSpace(receiptSystemCurrencyCode)?(english?"System currency unavailable":"عملة النظام غير متاحة"):caption.Text;
            ApplyVoucherResponsiveLayout();
        }

        private void UpdateLocalValue()
        {
            decimal value;
            localValue.Text = TryReadLocalVoucherAmount(out value) ? value.ToString("0.##") : "—";
        }

        private async void UpdateBalance() => await RefreshBalanceAsync();

        private void SetBalanceState(string text, Color color, string hint)
        {
            // The balance is a business value: it stays raw, and the caption follows the screen.
            balanceLabel.Text = text;
            balanceLabel.ForeColor = color;
            balanceLabel.AccessibleName = ReceiptText("رصيد الحساب");
            balanceLabel.AccessibleDescription = ReceiptText(hint);
        }

        internal async System.Threading.Tasks.Task RefreshBalanceAsync()
        {
            int generation = ++balanceLoadGeneration;
            CancelReceiptBalanceRead();
            if (IsDisposed || Disposing || receiptInitialLoading) return;
            if (receiptOptionsCurrent == null || !receiptOptionsCurrent())
            {
                receiptOptionsReady=false; SetReceiptOptionsEnabled(false);
                SetBalanceState("—",UiTheme.MutedText,"تغيرت جلسة السند؛ أعد فتح الشاشة."); return;
            }
            if (previewMode && usePreviewBalance)
            { SetBalanceState("2,500.00 ر.ي", UiTheme.Primary, "رصيد الحساب"); return; }
            DateTime documentDate;
            if (!AntdDateInput.TryRead(voucherDate, out documentDate))
            { SetBalanceState("—",UiTheme.MutedText,"تاريخ السند غير صحيح؛ الرصيد غير متاح."); return; }
            string accountCode=SelectedAccount(partyAccount);
            if(string.IsNullOrWhiteSpace(accountCode))
            { SetBalanceState("—",UiTheme.MutedText,"اختر الحساب لعرض رصيده."); return; }
            var cancellation=receiptBalanceCancellation=new System.Threading.CancellationTokenSource();var token=cancellation.Token;
            Func<bool> current=receiptOptionsCurrent;
            SetBalanceState("...",UiTheme.MutedText,"جار تحميل الرصيد...");
            try
            {
                var session=accountBalances==null?ApplicationServices.CreateAccountBalanceReadSession():null;
                var balanceReader=accountBalances??session.Service;
                var actor=session==null?new PharmacyManagement.Application.OperationActor{UserName=SessionContext.CurrentUser,UserRole=SessionContext.CurrentRole}:session.Actor;
                decimal balance=await balanceReader.LoadAsync(new PharmacyManagement.Application.AccountBalanceReadRequest(accountCode,documentDate),actor,token);
                if(IsDisposed||Disposing||generation!=balanceLoadGeneration||token.IsCancellationRequested)return;
                if(!current()||(session!=null&&!session.IsCurrent))throw new UnauthorizedAccessException("تغيرت جلسة رصيد السند.");
                SetBalanceState(balance.ToString("N2"),UiTheme.Primary,"رصيد الحساب");
            }
            catch(Exception exception)
            {
                if(IsDisposed||Disposing||generation!=balanceLoadGeneration||token.IsCancellationRequested)return;
                if(!current()||exception is UnauthorizedAccessException)
                {receiptOptionsReady=false;SetReceiptOptionsEnabled(false);SetBalanceState("—",UiTheme.MutedText,"تغيرت جلسة السند؛ أعد فتح الشاشة.");return;}
                SetBalanceState("غير متاح",UiTheme.Danger,"تعذر تحميل الرصيد؛ أعد اختيار الحساب للمحاولة مجددًا.");
                if(!previewMode)balanceLabel.AccessibleDescription+=" "+AppDiagnostics.Log("VoucherBalanceRead",exception);
            }
            finally
            {
                if(ReferenceEquals(receiptBalanceCancellation,cancellation))receiptBalanceCancellation=null;
                cancellation.Dispose();
            }
        }

        private async System.Threading.Tasks.Task<int> SaveVoucherAsync()
        {
            if (receiptWriteBusy || voucherPrintLoading) return 0;
            if (!EnsureReceiptHistoryContext() || !receiptOptionsReady)
            { receiptOptionsReady = false; SetReceiptOptionsEnabled(false); statusLabel.Text = ReceiptText("أعد فتح الشاشة لتحميل بيانات السند الحالية."); return 0; }
            if(string.IsNullOrWhiteSpace(receiptSystemCurrencyCode))
            {statusLabel.Text=ReceiptText("عملة النظام غير متاحة؛ أعد تحميل السند قبل الحفظ.");return 0;}
            if (previewMode)
            {
                statusLabel.Text = ReceiptText("المعاينة للعرض فقط؛ لا يمكن حفظ سند تجريبي.");
                return 0;
            }
            if (!PermissionService.EnsureAllowed(AppOperation.PostCashVoucher, this))
            {
                return 0;
            }
            if(!InitializeReceiptRecovery())return 0;
            if(receiptRecovery!=null&&receiptRecovery.Operation.Pending!=null&&!receiptRecovery.Operation.CanCorrectRejected)
                return await RunReceiptWriteAsync(()=>receiptRecovery.ExecuteAsync(),receiptRecovery.Message);

            Func<System.Threading.Tasks.Task<int>> write;
            string savedVoucherSummary;
            try
            {
                DateTime documentDate;
                if (!AntdDateInput.TryRead(voucherDate, out documentDate))
                {
                    statusLabel.Text = ReceiptText("لم يتم الحفظ: تاريخ السند غير صحيح.");
                    voucherDate.Focus();
                    return 0;
                }
                decimal voucherAmount;
                if (!TryReadLocalVoucherAmount(out voucherAmount))
                {
                    statusLabel.Text = ReceiptText("لم يتم الحفظ: أدخل مبلغًا صالحًا وسعر تحويل أكبر من صفر ضمن النطاق المسموح.");
                    decimal rate;
                    if (!decimal.TryParse(exchangeRate.Text, out rate) || rate <= 0) exchangeRate.Focus();
                    else amount.Focus();
                    return 0;
                }
                if (voucherAmount <= 0)
                {
                    AppNotifications.Show(ReceiptText("أدخل مبلغ السند بشكل صحيح."), Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    statusLabel.Text = ReceiptText("لم يتم الحفظ: مبلغ السند غير صحيح.");
                    return 0;
                }
                localValue.Text = voucherAmount.ToString("0.##");

                string selectedPartyAccount = SelectedAccount(partyAccount);
                string selectedSettlementAccount = SelectedAccount(settlementAccount);
                if (string.IsNullOrWhiteSpace(selectedPartyAccount) || string.IsNullOrWhiteSpace(selectedSettlementAccount))
                {
                    AppNotifications.Show(ReceiptText("اختر الحساب المقابل وحساب الصندوق/الإيداع."), Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    statusLabel.Text = ReceiptText("لم يتم الحفظ: الحسابات المطلوبة غير مكتملة.");
                    return 0;
                }

                if (string.Equals(selectedPartyAccount, selectedSettlementAccount, StringComparison.OrdinalIgnoreCase))
                {
                    AppNotifications.Show(ReceiptText("الحساب المقابل لا يمكن أن يكون نفس حساب الصندوق/البنك."), Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    statusLabel.Text = ReceiptText("لم يتم الحفظ: الحسابان متطابقان.");
                    return 0;
                }

                if (paymentMethod == "Cheque" && (string.IsNullOrWhiteSpace(chequeNumber.Text) || string.IsNullOrWhiteSpace(chequeBank.Text)))
                {
                    AppNotifications.Show(ReceiptText("أدخل رقم الشيك واسم البنك قبل حفظ سند الشيك."), Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    statusLabel.Text = ReceiptText("لم يتم الحفظ: بيانات الشيك ناقصة.");
                    return 0;
                }

                DateTime dueDate = default(DateTime);
                if (paymentMethod == "Cheque" && !AntdDateInput.TryRead(chequeDate, out dueDate))
                {
                    statusLabel.Text = ReceiptText("لم يتم الحفظ: تاريخ استحقاق الشيك غير صحيح.");
                    chequeDate.Focus();
                    return 0;
                }
                string details = notes.Text.Trim();
                if (paymentMethod == "Cheque")
                {
                    details += $" | شيك رقم: {chequeNumber.Text} | البنك: {chequeBank.Text} | الاستحقاق: {dueDate:yyyy-MM-dd} | الحالة: {chequeStatus.Text}";
                }
                else
                {
                    details += " | طريقة الدفع: " + GetPaymentMethodDisplay(paymentMethod);
                }

                decimal originalAmount,originalRate;
                if(!decimal.TryParse(amount.Text,out originalAmount)||!decimal.TryParse(exchangeRate.Text,out originalRate))throw new InvalidOperationException("مدخلات التحويل الأصلية غير صالحة.");
                var draftInputs=new PharmacyManagement.Application.VoucherDraftInputs(originalAmount,originalRate,currency.Text,paymentMethod,notes.Text,chequeNumber.Text,chequeBank.Text,paymentMethod=="Cheque"?(DateTime?)dueDate:null,chequeStatus.Text,representative.Text,partyName.Text);
                details=draftInputs.BuildNotes();
                savedVoucherSummary = Text + " - " + (string.IsNullOrWhiteSpace(partyName.Text) ? partyAccount.Text : partyName.Text.Trim()) + " - " + voucherAmount.ToString("N2");
                var request = new PharmacyManagement.Application.VoucherPostRequest(
                    voucherType,
                    documentDate,
                    string.IsNullOrWhiteSpace(partyName.Text) ? partyAccount.Text : partyName.Text.Trim(),
                    selectedPartyAccount,
                    voucherAmount,
                    details,
                    selectedSettlementAccount,
                    SelectedCostCenterId(),receiptSystemCurrencyCode);
                if(receiptRecovery.Operation.CanCorrectRejected&&receiptRecoveryRestoredNotes==notes.Text&&receiptRecovery.Operation.Pending.DraftInputs==null)
                    throw new InvalidOperationException("هذا سجل استعادة قديم؛ راجع بيان الدفع وأعد إدخاله قبل تصحيح الطلب. لم يرسل حفظ.");
                receiptRecovery.Prepare(request,draftInputs);write = () => receiptRecovery.ExecuteAsync();

            }
            catch (Exception error)
            {
                UiTheme.ShowOperationIssue(this, "حفظ السند", error);
                statusLabel.Text = receiptRecovery?.Message??"تعذر حفظ السند. راجع بيانات السند والحسابات.";
                return 0;
            }
            return await RunReceiptWriteAsync(write, savedVoucherSummary);
        }

        internal async System.Threading.Tasks.Task<int> RunReceiptWriteAsync(
            Func<System.Threading.Tasks.Task<int>> write, string summary, Action refreshHistory = null)
        {
            if (write == null) throw new ArgumentNullException(nameof(write));
            if (receiptWriteBusy || voucherPrintLoading) return 0;
            if(!receiptOptionsReady||!EnsureReceiptHistoryContext())return 0;
            Func<bool> current=receiptOptionsCurrent;
            var controls = new[] { (Control)voucherEditor, history,
                Controls.Find("receiptVoucherSide", true).FirstOrDefault(),
                Controls.Find("receiptVoucherToolbar", true).FirstOrDefault() };
            var states = controls.Where(control => control != null).Distinct().ToDictionary(control => control, control => control.Enabled);
            receiptWriteBusy = true;
            try
            {
                CancelReceiptHistoryRead();CancelReceiptBalanceRead();++balanceLoadGeneration;
                foreach (var control in states.Keys)
                {
                    if(IsDisposed||Disposing)return 0;
                    control.Enabled = false;
                }
                if(IsDisposed||Disposing)return 0;
                statusLabel.Text = ReceiptText("جار حفظ السند؛ انتظر اكتمال العملية.");
                if(IsDisposed||Disposing)return 0;
                if(!current()){InvalidateReceiptEditorContext();return 0;}
                int acceptedId;
                try
                {
                    acceptedId = await write();
                    if (acceptedId <= 0) throw new InvalidOperationException("لم ترجع عملية الحفظ رقم سند صالحًا.");
                }
                catch (Exception error)
                {
                    if (!IsDisposed && !Disposing)
                    {
                        if(!current()){InvalidateReceiptEditorContext();return 0;}
                        statusLabel.Text = receiptRecovery?.Message??"تعذر حفظ السند. راجع بيانات السند والحسابات.";
                        if (!previewMode&&!VoucherEditorsOwnedQaSelfTests.QuietNotifications) UiTheme.ShowOperationIssue(this, "حفظ السند", error);
                    }
                    return 0;
                }
                lastSavedVoucherId = acceptedId; lastSavedVoucherSummary = current()?summary:string.Empty;
                if (!IsDisposed && !Disposing)
                {
                    if(!current()){InvalidateReceiptEditorContext();return acceptedId;}
                    bool refreshed = CompleteSavedVoucherPresentation(acceptedId, summary, refreshHistory);
                    if (!previewMode&&!VoucherEditorsOwnedQaSelfTests.QuietNotifications) AppNotifications.Show(this, statusLabel.AccessibleDescription, Text, MessageBoxButtons.OK,
                        refreshed ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
                }
                return acceptedId;
            }
            finally
            {
                receiptWriteBusy = false;
                if (!IsDisposed && !Disposing)
                {
                    if(current()&&receiptOptionsReady)
                        foreach (var state in states) if (!state.Key.IsDisposed) state.Key.Enabled = state.Value;
                    else InvalidateReceiptEditorContext();
                    ApplyReceiptRecoveryLock();
                }
            }
        }
        private bool CompleteSavedVoucherPresentation(int savedVoucherId, string savedVoucherSummary, Action refreshHistory = null)
        {
            lastSavedVoucherId = savedVoucherId;
            lastSavedVoucherSummary = savedVoucherSummary;
            try
            {
                amount.Clear();
                localValue.Text = "0";
                notes.Clear();
                chequeNumber.Clear();
                chequeBank.Clear();
                voucherNo.Text = GetNextVoucherNumberText();
                if (refreshHistory != null) refreshHistory();
                UpdateBalance();
                statusLabel.Text = ReceiptText("تم حفظ السند رقم ")+savedVoucherId+ReceiptText(" وتوليد القيد المحاسبي بنجاح.");
                statusLabel.AccessibleDescription = statusLabel.Text;
                if (refreshHistory == null) LoadHistory();
                return true;
            }
            catch (Exception error)
            {
                statusLabel.Text = ReceiptText("تم حفظ السند رقم ")+savedVoucherId+ReceiptText("؛ تعذر تحديث السجل. لا تعِد الحفظ.");
                statusLabel.AccessibleDescription = statusLabel.Text + " أعد فتح الشاشة لتحديث السجل.";
                if (!previewMode)
                    statusLabel.AccessibleDescription += " رمز الدعم: " + AppDiagnostics.Log("ReceiptVoucherPostCommitRefresh", error);
                return false;
            }
            finally
            {
                // History rebinding may select an older row; print the newly
                // accepted document until the user explicitly selects another.
                lastSavedVoucherId = savedVoucherId;
                lastSavedVoucherSummary = savedVoucherSummary;
            }
        }

        private bool TryReadLocalVoucherAmount(out decimal localAmount)
        {
            localAmount = 0m;
            decimal originalAmount;
            decimal rate;
            return decimal.TryParse(amount.Text, out originalAmount) &&
                decimal.TryParse(exchangeRate.Text, out rate) &&
                PharmacyManagement.Domain.VoucherAmountCalculator.TryCalculate(originalAmount, rate, out localAmount);
        }

        private string GetPaymentMethodDisplay(string methodName)
        {
            if (string.Equals(methodName, "Cheque", StringComparison.OrdinalIgnoreCase))
            {
                return "شيك";
            }

            if (string.Equals(methodName, "Bank", StringComparison.OrdinalIgnoreCase) || string.Equals(methodName, "Transfer", StringComparison.OrdinalIgnoreCase))
            {
                return "تحويل";
            }

            return "نقدي";
        }

        private async System.Threading.Tasks.Task<PharmacyManagement.Application.VoucherDocumentSnapshot> ReadVoucherPrintDocumentAsync(int voucherId)
        {
            CancelVoucherDocumentRead();int generation=voucherDocumentGeneration;
            var cancellation=voucherDocumentCancellation=new System.Threading.CancellationTokenSource();var token=cancellation.Token;
            Func<bool> current=receiptOptionsCurrent;
            try
            {
                if(IsDisposed||Disposing||current==null||!current())throw new UnauthorizedAccessException("تغيرت جلسة طباعة السند.");
                var session=voucherDocuments==null?ApplicationServices.CreateVoucherDocumentReadSession():null;
                var reader=voucherDocuments??session.Service;
                var actor=session==null?new PharmacyManagement.Application.OperationActor{UserName=SessionContext.CurrentUser,UserRole=SessionContext.CurrentRole}:session.Actor;
                var document=await reader.LoadAsync(new PharmacyManagement.Application.VoucherDocumentReadRequest(voucherId),actor,token);
                token.ThrowIfCancellationRequested();
                if(IsDisposed||Disposing||generation!=voucherDocumentGeneration)throw new OperationCanceledException("أُغلقت نافذة قراءة السند.",token);
                if(!current()||(session!=null&&!session.IsCurrent))throw new UnauthorizedAccessException("تغيرت جلسة طباعة السند.");
                if(document==null||document.VoucherId!=voucherId)throw new InvalidOperationException("السند المقروء غير موجود أو لا يطابق طلب الطباعة.");
                return document;
            }
            finally
            {if(ReferenceEquals(voucherDocumentCancellation,cancellation))voucherDocumentCancellation=null;cancellation.Dispose();}
        }

        private static DataTable CreateVoucherPrintTable(PharmacyManagement.Application.VoucherDocumentSnapshot document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            return VoucherTableProjection.Create(new[] { document });
        }

        private static string GetVoucherTypeDisplay(string type)
        {
            return VoucherTableProjection.TypeDisplay(type);
        }

        private void CaptureSelectedHistoryVoucher()
        {
            if(receiptInitialLoading||receiptHistoryLoading||!EnsureReceiptHistoryContext())return;
            if (history.CurrentRow == null || history.CurrentRow.IsNewRow)
            {
                return;
            }

            object voucherIdValue = GetSelectedHistoryValue("رقم السند");
            int selectedVoucherId;
            if (voucherIdValue == null || voucherIdValue == DBNull.Value || !int.TryParse(Convert.ToString(voucherIdValue), out selectedVoucherId))
            {
                return;
            }

            lastSavedVoucherId = selectedVoucherId;
            string party = Convert.ToString(GetSelectedHistoryValue("الطرف"));
            string amountText = Convert.ToString(GetSelectedHistoryValue("المبلغ"));
            lastSavedVoucherSummary = (string.IsNullOrWhiteSpace(party) ? Text : party) + " - " + amountText;
            statusLabel.Text = ReceiptText("تم تحديد السند رقم ")+lastSavedVoucherId+ReceiptText(" من السجل.");
        }

        private object GetSelectedHistoryValue(string columnName)
        {
            if(receiptHistoryLoading||!EnsureReceiptHistoryContext())return null;
            if (history.CurrentRow == null || history.CurrentRow.DataBoundItem == null)
            {
                return null;
            }

            DataRowView rowView = history.CurrentRow.DataBoundItem as DataRowView;
            if (rowView != null && rowView.Row.Table.Columns.Contains(columnName))
            {
                return rowView[columnName];
            }

            return null;
        }

        private async void LoadHistory()
        {
            if (previewMode) return;
            await RefreshVoucherHistoryAsync(null, lastSavedVoucherId);
        }

        internal async System.Threading.Tasks.Task RefreshVoucherHistoryAsync(Func<System.Threading.Tasks.Task<DataTable>> read = null, int acceptedId = 0,
            Func<System.Threading.CancellationToken,System.Threading.Tasks.Task<DataTable>> cancellableRead=null)
        {
            CancelReceiptHistoryRead();int generation=historyLoadGeneration;
            if(IsDisposed||Disposing||!EnsureReceiptHistoryContext())return;
            var cancellation=receiptHistoryCancellation=new System.Threading.CancellationTokenSource();var token=cancellation.Token;
            Func<bool> current=receiptOptionsCurrent;receiptHistoryLoading=true;
            DataTable rows=null;bool bound=false;
            try
            {
                rows=await(cancellableRead!=null?cancellableRead(token):read!=null?read():ReceiptVoucherWorkspaceService.LoadHistoryAsync(voucherType,token));
                if(IsDisposed||Disposing||generation!=historyLoadGeneration||token.IsCancellationRequested)return;
                if(!current()){EnsureReceiptHistoryContext();return;}
                if(rows==null)throw new InvalidOperationException("Voucher history source returned no table.");
                int selectedId=lastSavedVoucherId;string summary=lastSavedVoucherSummary;var previous=history.DataSource as DataTable;
                try{history.DataSource=rows;bound=true;}
                finally{lastSavedVoucherId=selectedId;lastSavedVoucherSummary=summary;}
                if(previous!=rows)previous?.Dispose();
            }
            catch(Exception error)
            {
                if(IsDisposed||Disposing||generation!=historyLoadGeneration||token.IsCancellationRequested)return;
                if(!current()){EnsureReceiptHistoryContext();return;}
                statusLabel.Text=acceptedId>0?"تم حفظ السند رقم "+acceptedId+"؛ تعذر تحديث السجل. لا تعِد الحفظ.":"تعذر تحديث سجل السندات؛ البيانات المعروضة لم تتغير.";
                statusLabel.AccessibleDescription=statusLabel.Text;if(!previewMode)statusLabel.AccessibleDescription+=" "+AppDiagnostics.Log("VoucherHistoryRead",error);
            }
            finally
            {
                if(!bound)rows?.Dispose();if(ReferenceEquals(receiptHistoryCancellation,cancellation))receiptHistoryCancellation=null;
                if(generation==historyLoadGeneration)receiptHistoryLoading=false;cancellation.Dispose();
            }
        }

    }

    public partial class GeneralLedgerForm : Form
    {
        private static readonly string[] LedgerSourceTypeCodes =
            { null, "Manual", "Voucher", "SalesInvoice", "PurchaseInvoice", "OpeningEntry" };
        private readonly ComboBox account = new ComboBox();
        private readonly AntdUI.DatePicker fromDate = DesktopComponents.DatePicker<LedgerDatePicker>("generalLedgerFromDate", "من تاريخ", new DateTime(DateTime.Today.Year, 1, 1), "u");
        private readonly AntdUI.DatePicker toDate = DesktopComponents.DatePicker<LedgerDatePicker>("generalLedgerToDate", "إلى تاريخ", DateTime.Today, "u");
        private readonly ComboBox costCenter = new ComboBox();
        private readonly AntdUI.Select sortBy = DesktopComponents.Select("generalLedgerSortBy", "ترتيب حسب",
            UiDirection.IsLeftToRight ? "By date" : "حسب التاريخ",
            UiDirection.IsLeftToRight ? "By date and document number" : "حسب التاريخ ثم رقم المستند");
        private readonly AntdUI.Select documentType = DesktopComponents.Select("generalLedgerDocumentType", "نوع المستند",
            UiDirection.IsLeftToRight ? "All" : "الكل",
            UiDirection.IsLeftToRight ? "Manual entry" : "قيد يدوي",
            UiDirection.IsLeftToRight ? "Voucher" : "سند",
            UiDirection.IsLeftToRight ? "Sales invoice" : "فاتورة بيع",
            UiDirection.IsLeftToRight ? "Purchase invoice" : "فاتورة شراء",
            UiDirection.IsLeftToRight ? "Opening entry" : "قيد افتتاحي");
        private readonly AntdUI.Checkbox includeOpening = DesktopComponents.Checkbox("generalLedgerIncludeOpening", "الرصيد الافتتاحي", true);
        private readonly AntdUI.Checkbox includePrevious = DesktopComponents.Checkbox("generalLedgerIncludePrevious", "الرصيد السابق", true);
        private readonly FlowLayoutPanel summaryCards = new FlowLayoutPanel();
        private readonly DataGridView grid = new DataGridView();
        private DataTable currentTable;
        private readonly Label statusLabel = new Label();
        private int ledgerLoadGeneration;
        private readonly Func<GeneralLedgerReadSession> ledgerSessionFactory;
        private GeneralLedgerReadSession ledgerReadSession;
        private System.Threading.CancellationTokenSource ledgerCancellation;
        private bool bindingLedgerFilters, ledgerOptionsReady;
        private PharmacyManagement.Application.GeneralLedgerReadRequest loadedLedgerRequest;
        private string loadedLedgerAccount;


        public GeneralLedgerForm() : this(null) { }
        internal GeneralLedgerForm(Func<GeneralLedgerReadSession> sessionFactory)
        {
            ledgerSessionFactory = sessionFactory ?? ApplicationServices.CreateGeneralLedgerReadSession;
            Text = "حركة حساب خلال فترة";
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = true;
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(1200, 760);
            RightToLeft = UiDirection.IsLeftToRight ? RightToLeft.No : RightToLeft.Yes;
            RightToLeftLayout = !UiDirection.IsLeftToRight;
            KeyPreview = true;
            account.Name = "generalLedgerAccount";
            costCenter.Name = "generalLedgerCostCenter";
            InitializeComponent();
            account.Enabled = false;
            costCenter.Enabled = false;

            UiTheme.Apply(this);
            summaryCards.BackColor = UiTheme.SurfaceVariant;
            FlowLayoutPanel ledgerActions = Controls.Find("generalLedgerActions", true).OfType<FlowLayoutPanel>().First();
            foreach (AntdUI.Button button in ledgerActions.Controls.OfType<AntdUI.Button>())
                DesktopComponents.StyleOutlinedFooterAction(button);
            FitToWorkingArea();
            UiTheme.ApplyDataGridQuality(grid, "لا توجد حركات لهذا الحساب");
            InitializeStaticDefaults();
            summaryCards.Resize += (s, e) => LayoutSummaryCards();
            RenderSummary(0, 0, 0, 0);
            SetLedgerStatus("جار تحميل خيارات كشف الحساب...", null);
            Load += async (s, e) =>
            {
                FitToWorkingArea();
                await LoadInitialDataAsync();
            };
            Disposed += (s, e) => CancelLedgerRead();
            SetLedgerOutputReady(false);
            account.SelectedIndexChanged += (s, e) => InvalidateLedgerFilters();
            costCenter.SelectedIndexChanged += (s, e) => InvalidateLedgerFilters();
            fromDate.TextChanged += (s, e) => InvalidateLedgerFilters();
            toDate.TextChanged += (s, e) => InvalidateLedgerFilters();
            sortBy.SelectedValueChanged += (s, e) => InvalidateLedgerFilters();
            documentType.SelectedValueChanged += (s, e) => InvalidateLedgerFilters();
            includeOpening.CheckedChanged += (s, e) => InvalidateLedgerFilters();
            includePrevious.CheckedChanged += (s, e) => InvalidateLedgerFilters();
            UiSessionSubscription.Attach(this, () =>
            {
                if (ledgerReadSession == null && !ledgerOptionsReady) return;
                CancelLedgerRead(); ledgerReadSession = null; ledgerOptionsReady = false;
                bindingLedgerFilters = true;
                try { account.Items.Clear(); costCenter.Items.Clear(); }
                finally { bindingLedgerFilters = false; }
                account.Enabled = false; costCenter.Enabled = false; ClearLedgerResult();
                SetLedgerStatus( "تغيرت جلسة المستخدم؛ أعد تحميل كشف الحساب.", false);
            });
            KeyDown += (s, e) =>
            {
                if (e.KeyCode != Keys.Escape) return;
                if (CloseLedgerDatePickerPopup(fromDate) || CloseLedgerDatePickerPopup(toDate))
                {
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    return;
                }
                Close();
            };
            grid.CellFormatting += FormatLedgerSourceCell;
            Controls.Find("generalLedgerActionsHost", true).First().Resize += (s, e) => LayoutLedgerActions();
            statusLabel.TextChanged += (s, e) => { if (ledgerPresentationReady) LayoutLedgerActions(); };
            ledgerPresentationReady = true;
            ApplyPresentation(UiDirection.IsLeftToRight);
        }

        private void FitToWorkingArea()
        {
            Rectangle workingArea = Screen.FromControl(this).WorkingArea;
            MaximumSize = workingArea.Size;
            MinimumSize = new Size(Math.Min(760, workingArea.Width), Math.Min(600, workingArea.Height));
            Size = new Size(Math.Min(Math.Max(Width, MinimumSize.Width), workingArea.Width), Math.Min(Math.Max(Height, MinimumSize.Height), workingArea.Height));
        }

        private void InitializeStaticDefaults()
        {
            fromDate.Value = new DateTime(DateTime.Today.Year, 1, 1);
            toDate.Value = DateTime.Today;
            sortBy.SelectedIndex = 0;
            documentType.SelectedIndex = 0;
        }

        private void CancelLedgerRead()
        {
            ledgerLoadGeneration++; var previous=ledgerCancellation; ledgerCancellation=null;
            if(previous!=null){previous.Cancel();previous.Dispose();}
        }
        private void SetLedgerOutputReady(bool enabled)
        {
            foreach(string name in new[]{"generalLedgerPrint","generalLedgerExport"})
                foreach(Control button in Controls.Find(name,true))button.Enabled=enabled;
        }
        private void ClearLedgerResult()
        {
            SetLedgerOutputReady(false); loadedLedgerRequest=null; loadedLedgerAccount=null;
            var previous=currentTable; currentTable=null; grid.DataSource=null; previous?.Dispose();
            RenderSummary(0,0,0,0);
        }
        private void InvalidateLedgerFilters()
        {
            if(bindingLedgerFilters)return;
            CancelLedgerRead(); ClearLedgerResult();
            if(ledgerOptionsReady)SetLedgerStatus("تغيرت المرشحات؛ اضغط عرض النتيجة لتحديث كشف الحساب.",null);
        }
        private void LedgerReadIssue(Exception error,string operation)
        {
            string support=AppDiagnostics.Log("GeneralLedger:"+operation,error);
            SetLedgerStatus("تعذر تحميل كشف الحساب؛ أعد المحاولة. رمز الدعم: {0}",false,support);
            statusLabel.AccessibleDescription=support;
        }
        internal async System.Threading.Tasks.Task LoadInitialDataAsync()
        {
            CancelLedgerRead();ClearLedgerResult();ledgerOptionsReady=false;
            int generation=ledgerLoadGeneration;
            var cancellation=new System.Threading.CancellationTokenSource();ledgerCancellation=cancellation;
            account.Enabled=false;costCenter.Enabled=false;
            try
            {
                var session=ledgerSessionFactory();ledgerReadSession=session;
                var accounts=await session.LoadAccountsAsync(cancellation.Token);
                if(IsDisposed||Disposing||generation!=ledgerLoadGeneration)return;
                var centers=await session.LoadCostCentersAsync(cancellation.Token);
                if(IsDisposed||Disposing||generation!=ledgerLoadGeneration)return;
                session.EnsureContext();bindingLedgerFilters=true;
                try
                {
                    account.Items.Clear();
                    foreach(var row in accounts)if(!string.IsNullOrWhiteSpace(row.Code))account.Items.Add(row.Code+" - "+row.Name);
                    if(account.Items.Count>0)account.SelectedIndex=0;
                    costCenter.Items.Clear();costCenter.Items.Add(LedgerText("الكل"));costCenter.Items.Add(LedgerText("بدون مركز تكلفة"));
                    foreach(var row in centers)costCenter.Items.Add(row.Id+" - "+row.Name);
                    costCenter.SelectedIndex=0;
                }
                finally{bindingLedgerFilters=false;}
                ledgerOptionsReady=true;account.Enabled=true;costCenter.Enabled=true;
                await LoadDataAsync();
            }
            catch(OperationCanceledException){}
            catch(Exception error)
            {
                if(IsDisposed||Disposing||generation!=ledgerLoadGeneration)return;
                bindingLedgerFilters=true;try{account.Items.Clear();costCenter.Items.Clear();}finally{bindingLedgerFilters=false;}
                ledgerOptionsReady=false;LedgerReadIssue(error,"options");
            }
        }
        private async void LoadData(){await LoadDataAsync();}
        internal async System.Threading.Tasks.Task LoadDataAsync()
        {
            DateTime validatedStart,validatedEnd;
            if(!TryGetDateRange(out validatedStart,out validatedEnd)){CancelLedgerRead();ClearLedgerResult();return;}
            if(!ledgerOptionsReady){await LoadInitialDataAsync();return;}
            CancelLedgerRead();ClearLedgerResult();int generation=ledgerLoadGeneration;
            var cancellation=new System.Threading.CancellationTokenSource();ledgerCancellation=cancellation;
            if(account.Items.Count==0){SetLedgerStatus("لا توجد حسابات متاحة لعرض كشف الحساب.",null);return;}
            if(account.SelectedIndex<0||costCenter.SelectedIndex<0){SetLedgerStatus("اختر حسابًا ومركز تكلفة صالحين قبل عرض كشف الحساب.",false);return;}
            DateTime start,end;if(!TryGetDateRange(out start,out end))return;
            try
            {
                var session=ledgerReadSession??throw new InvalidOperationException("سياق كشف الحساب غير جاهز.");session.EnsureContext();
                var request=new PharmacyManagement.Application.GeneralLedgerReadRequest(SelectedAccount(),start,end,SelectedLedgerSourceType(),includePrevious.Checked,includeOpening.Checked,SelectedLedgerCostCenterId());
                string accountDisplay=account.Text;
                SetLedgerStatus("جار تحميل حركات الحساب...",null);
                // The rows are read straight into the table, so they are materialised once.
                var result=await session.LoadTableAsync(request,cancellation.Token);
                if(IsDisposed||Disposing||generation!=ledgerLoadGeneration)return;
                session.EnsureContext();
                currentTable=result.Table;
                loadedLedgerRequest=request;loadedLedgerAccount=accountDisplay;
                grid.DataSource=currentTable;ApplyLedgerGridPresentation();
                RenderSummary(result.PreviousBalance,result.PeriodDebit,result.PeriodCredit,result.EndingBalance);
                SetLedgerOutputReady(currentTable.Rows.Count>0);
                if (currentTable.Rows.Count == 0)
                    SetLedgerStatus("لا توجد حركات مطابقة. راجع الحساب والفترة والمرشحات.", null);
                else
                    SetLedgerStatus("تم تحميل {0} حركة.",true,currentTable.Rows.Count);
            }
            catch(OperationCanceledException){}
            catch(Exception error){if(!IsDisposed&&!Disposing&&generation==ledgerLoadGeneration)LedgerReadIssue(error,"rows");}
        }
        internal Func<bool> CaptureLedgerOutputContext()
        {
            var outputSession = ledgerReadSession;
            var outputRequest = loadedLedgerRequest;
            var outputTable = currentTable;
            return () => !IsDisposed && !Disposing && outputSession != null && outputRequest != null &&
                outputTable != null && ReferenceEquals(outputSession, ledgerReadSession) && outputSession.IsCurrent &&
                ReferenceEquals(outputRequest, loadedLedgerRequest) && ReferenceEquals(outputTable, currentTable) &&
                outputTable.Rows.Count > 0;
        }

        internal bool EnsureLedgerOutputContext(Func<bool> current)
        {
            if (IsDisposed || Disposing) return false;
            if (current != null && ReportPreviewLifetime.ContextCurrent(current) && EnsureLedgerOutputCurrent()) return true;
            SetLedgerStatus("تغير سياق العملية؛ أعد المحاولة.", false);
            return false;
        }

        private bool EnsureLedgerOutputCurrent()
        {
            if(loadedLedgerRequest!=null&&ledgerReadSession!=null&&ledgerReadSession.IsCurrent&&currentTable!=null&&currentTable.Rows.Count>0)return true;
            if(loadedLedgerRequest==null||ledgerReadSession==null||!ledgerReadSession.IsCurrent)ClearLedgerResult();
            SetLedgerStatus(loadedLedgerRequest!=null?"لا توجد حركات في الفترة المحددة للطباعة أو التصدير.":"لا توجد حركات جاهزة للطباعة أو التصدير؛ أعد تحميل كشف الحساب.",false);return false;
        }
        private void RenderSummary(decimal previousBalance, decimal periodDebit, decimal periodCredit, decimal endingBalance)
        {
            summaryPrevious = previousBalance; summaryDebit = periodDebit;
            summaryCredit = periodCredit; summaryEnding = endingBalance;
            if(summaryCards.Controls.Count!=3)
            {
                foreach(Control old in summaryCards.Controls.Cast<Control>().ToArray()){summaryCards.Controls.Remove(old);old.Dispose();}
                summaryCards.Controls.Add(CreateSummaryCard("رصيد الفترة المحددة",0,0));
                summaryCards.Controls.Add(CreateSummaryCard("إجمالي الفترة المحددة",0,0));
                summaryCards.Controls.Add(CreateSummaryCard("الرصيد السابق",0,0));
            }
            decimal[,] amounts={{Math.Max(endingBalance,0),Math.Max(-endingBalance,0)},{periodDebit,periodCredit},{Math.Max(previousBalance,0),Math.Max(-previousBalance,0)}};
            for(int i=0;i<3;i++)
            {
                var values=(Label)summaryCards.Controls[i].Controls["generalLedgerSummaryValues"];
                values.Text=LedgerText("مدين")+": "+amounts[i,0].ToString("N2")+Environment.NewLine+LedgerText("دائن")+": "+amounts[i,1].ToString("N2");
            }
            LayoutSummaryCards();
        }

        private void LayoutSummaryCards()
        {
            if (summaryCards.ClientSize.Width <= 0 || summaryCards.Controls.Count == 0) return;
            bool leftToRight = RightToLeft == RightToLeft.No;
            bool compact = summaryCards.ClientSize.Width < 1150;
            int cardWidth = compact
                ? Math.Max(180, (summaryCards.ClientSize.Width - summaryCards.Padding.Horizontal - summaryCards.Controls.Count * 10 - 2) / summaryCards.Controls.Count)
                : 360;
            summaryCards.SuspendLayout();
            try
            {
                foreach (AntdUI.Panel card in summaryCards.Controls.OfType<AntdUI.Panel>())
                {
                    card.Width = cardWidth;
                    card.Padding = compact ? new Padding(8) : new Padding(16);
                    Label heading = card.Controls["generalLedgerSummaryTitle"] as Label;
                    if (heading == null) continue;
                    card.RightToLeft = RightToLeft.No;
                    Label displayedValues = card.Controls["generalLedgerSummaryValues"] as Label;
                    int titleWidth = TextRenderer.MeasureText(heading.Text, heading.Font).Width + 12;
                    int valuesWidth = displayedValues == null ? 0 : displayedValues.Text.Split(new[] { Environment.NewLine }, StringSplitOptions.None)
                        .Max(line => TextRenderer.MeasureText(line, displayedValues.Font).Width) + 8;
                    bool stacked = compact || cardWidth - card.Padding.Horizontal < titleWidth + valuesWidth + 12;
                    heading.Dock = stacked ? DockStyle.Top : (leftToRight ? DockStyle.Left : DockStyle.Right);
                    heading.Height = stacked ? Math.Max(24, TextRenderer.MeasureText(heading.Text, heading.Font).Height + 4) : card.Height;
                    heading.Width = stacked ? cardWidth - card.Padding.Horizontal : titleWidth;
                    heading.RightToLeft = RightToLeft.No;
                    heading.TextAlign = leftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
                    Label values = card.Controls["generalLedgerSummaryValues"] as Label;
                    if (values != null) { values.RightToLeft = RightToLeft.No; values.TextAlign = heading.TextAlign; }
                }
            }
            finally { summaryCards.ResumeLayout(true); }
        }

        private AntdUI.Panel CreateSummaryCard(string title, decimal debit, decimal credit)
        {
            AntdUI.Panel card = new AntdUI.Panel
            {
                Width = 360,
                Height = 88,
                Margin = new Padding(5),
                Padding = new Padding(16),
                Back = UiTheme.Surface, BackColor = Color.Transparent, Radius = DesktopDesignTokens.CardRadius, Shadow = 0
            };
            Label titleLabel = new Label
            {
                Name = "generalLedgerSummaryTitle",
                Text = LedgerText(title), Tag = title,
                Dock = DockStyle.Right,
                Width = 140,
                TextAlign = ContentAlignment.MiddleRight,
                Font = AppFonts.Create(10F, FontStyle.Bold),
                BackColor = Color.Transparent, ForeColor = UiTheme.Text
            };
            Label values = new Label
            {
                Name = "generalLedgerSummaryValues",
                Text = LedgerText("مدين: ") + debit.ToString("N2") + Environment.NewLine + LedgerText("دائن: ") + credit.ToString("N2"),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight,
                Font = AppFonts.Create(9.5F, FontStyle.Regular),
                BackColor = Color.Transparent, ForeColor = UiTheme.Text
            };
            card.Controls.Add(values);
            card.Controls.Add(titleLabel);
            return card;
        }

        private void FormatGrid()
        {
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.ColumnHeadersHeight = 44;
            grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;

            foreach (DataGridViewColumn column in grid.Columns)
            {
                column.SortMode = DataGridViewColumnSortMode.NotSortable;
                column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                column.MinimumWidth = 86;
                column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

                if (column.ValueType == typeof(decimal) || column.ValueType == typeof(double) || column.ValueType == typeof(float))
                {
                    column.DefaultCellStyle.Format = "N2";
                    column.MinimumWidth = 104;
                    column.FillWeight = 90;
                }

                switch (column.Index)
                {
                    case 0:
                        column.MinimumWidth = 112;
                        column.FillWeight = 90;
                        column.DefaultCellStyle.Format = "yyyy-MM-dd";
                        column.DefaultCellStyle.FormatProvider = System.Globalization.CultureInfo.InvariantCulture;
                        break;
                    case 1:
                    case 3:
                        column.MinimumWidth = 108;
                        column.FillWeight = 86;
                        break;
                    case 2:
                        column.MinimumWidth = 116;
                        column.FillWeight = 95;
                        break;
                    case 4:
                        column.MinimumWidth = 142;
                        column.FillWeight = 125;
                        break;
                    case 5:
                        column.MinimumWidth = 220;
                        column.FillWeight = 210;
                        column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                        break;
                    case 6:
                    case 7:
                    case 8:
                        column.MinimumWidth = 104;
                        column.FillWeight = 90;
                        column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                        break;
                }
            }
        }

        private string SelectedAccount()
        {
            string value = Convert.ToString(account.SelectedItem);
            int dash = value.IndexOf(" - ", StringComparison.Ordinal);
            return dash > 0 ? value.Substring(0, dash).Trim() : value.Trim();
        }

        private string SelectedLedgerSourceType()
        {
            int index = documentType.SelectedIndex;
            return index >= 0 && index < LedgerSourceTypeCodes.Length ? LedgerSourceTypeCodes[index] : null;
        }

        private int? SelectedLedgerCostCenterId()
        {
            if (costCenter.SelectedIndex <= 0)
            {
                return null;
            }
            if (costCenter.SelectedIndex == 1)
            {
                return 0;
            }

            string value = Convert.ToString(costCenter.SelectedItem);
            int dash = value.IndexOf(" - ", StringComparison.Ordinal);
            int id;
            return dash > 0 && int.TryParse(value.Substring(0, dash), out id) ? (int?)id : null;
        }

        private void ExportExcel()
        {
            if(!EnsureLedgerOutputCurrent())return;
            if (currentTable == null || currentTable.Rows.Count == 0)
            {
                AppNotifications.Show(this, LedgerText("لا توجد بيانات للتصدير."), Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var outputTable = currentTable;
            var outputContext = CaptureLedgerOutputContext();
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                ConfigureLedgerExcelDialog(dialog);
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                if(!EnsureLedgerOutputContext(outputContext))return;

                try
                {
                    WriteLedgerExcelFile(outputTable, dialog.FileName);
                    SetLedgerStatus("تم تصدير كشف الحساب إلى Excel.", true);
                    AppNotifications.Show(this, LedgerText("تم تصدير كشف الحساب إلى Excel."), Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception exportError)
                {
                    SetLedgerStatus("تعذر حفظ Excel؛ بقي كشف الحساب كما هو. تحقق من المسار أو صلاحية الكتابة ثم أعد المحاولة.", false);
                    AppNotifications.ShowFileOutputIssue(this, "تصدير كشف الحساب إلى Excel", exportError, ledgerLeftToRight);
                }
            }
        }

        internal static void ConfigureLedgerExcelDialog(SaveFileDialog dialog)
        {
            if (dialog == null) throw new ArgumentNullException(nameof(dialog));
            dialog.Filter = "Excel Workbook (*.xlsx)|*.xlsx";
            dialog.DefaultExt = "xlsx";
            dialog.AddExtension = true;
            dialog.FileName = "GeneralLedger.xlsx";
        }

        internal void WriteLedgerExcelFile(DataTable table, string path)
        {
            using (DataTable snapshot = CreateLedgerOutputSnapshot(table))
                ExcelWorkbookWriter.WriteReportCompleteFile(snapshot, Text, path, ledgerLeftToRight);
        }

        internal DataTable CreateLedgerOutputSnapshot(DataTable source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            DataTable snapshot = source.Copy();
            foreach (DataColumn column in snapshot.Columns)
                column.Caption = LedgerText(column.ColumnName);
            // Printed amount columns can span another horizontal part; keep the
            // document identity beside them without changing the source table.
            foreach (string identity in new[] { "التاريخ", "رقم المستند", "نوع المستند" })
                if (snapshot.Columns.Contains(identity))
                    snapshot.Columns[identity].ExtendedProperties["RepeatOnPrint"] = true;
            if (snapshot.Columns.Contains("نوع المستند") && snapshot.Columns["نوع المستند"].DataType == typeof(string))
            {
                // SQL-loaded tables can mark expression columns read-only. Localize only
                // the detached output copy, preserving the source and its column contract.
                DataColumn sourceType = snapshot.Columns["نوع المستند"];
                bool readOnly = sourceType.ReadOnly;
                sourceType.ReadOnly = false;
                foreach (DataRow row in snapshot.Rows)
                    if (!row.IsNull("نوع المستند"))
                        row["نوع المستند"] = LedgerSourceDisplay((string)row["نوع المستند"]);
                sourceType.ReadOnly = readOnly;
            }
            return snapshot;
        }

        internal string BuildLedgerCsvContent(DataTable table)
        {
            using (DataTable snapshot = CreateLedgerOutputSnapshot(table))
            {
                StringBuilder builder = new StringBuilder();
                builder.AppendLine(string.Join(",", snapshot.Columns.Cast<DataColumn>().Select(c => ReportCsvWriter.Cell(c.Caption))));
                foreach (DataRow row in snapshot.Rows)
                    builder.AppendLine(string.Join(",", snapshot.Columns.Cast<DataColumn>().Select(c => ReportCsvWriter.Cell(row[c]))));
                return builder.ToString();
            }
        }
        private void PrintReport()
        {
            if(!EnsureLedgerOutputCurrent())return;
            if (currentTable == null || currentTable.Rows.Count == 0)
            {
                SetLedgerStatus("لا توجد حركات جاهزة للطباعة.", false);
                return;
            }
            DateTime periodStart=loadedLedgerRequest.FromDate;
            DateTime periodEnd=loadedLedgerRequest.ToDate;
            string selectedAccount = loadedLedgerAccount;
            string subtitle = LedgerPeriodText(periodStart, periodEnd);
            var outputTable=currentTable;var outputContext=CaptureLedgerOutputContext();
            ReportPrintOptions selectedOptions;
            using (DataTable printTable = CreateLedgerOutputSnapshot(outputTable))
            DataTablePrintPreview.ShowWithOptions(this, string.Format(LedgerText("كشف حساب: {0}"), selectedAccount), subtitle, printTable,
                null, out selectedOptions, ledgerLeftToRight, isCurrent:
                outputContext);
        }

        private bool TryGetDateRange(out DateTime periodStart, out DateTime periodEnd)
        {
            periodStart = default(DateTime);
            periodEnd = default(DateTime);
            if (!TryReadLedgerDate(fromDate, out periodStart) || !TryReadLedgerDate(toDate, out periodEnd))
            {
                SetLedgerStatus("اختر تاريخ بداية ونهاية صحيحين قبل عرض كشف الحساب.", false);
                return false;
            }
            if (periodStart > periodEnd)
            {
                SetLedgerStatus("تاريخ البداية يجب ألا يتجاوز تاريخ النهاية.", false);
                return false;
            }
            return true;
        }

        private AntdUI.Button CreateActionButton(string text, AppIcon icon, Action action)
        {
            AntdUI.Button button = new AccessibleAntdButton
            {
                Text = text, Tag = text,
                Width = 126,
                Height = 48,
                Margin = new Padding(5),
                Font = AppFonts.Create(10F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                AccessibleName = text,
                AccessibleDescription = "إجراء دفتر الأستاذ العام",
                AccessibleRole = AccessibleRole.PushButton,
                RightToLeft = UiDirection.IsLeftToRight ? RightToLeft.No : RightToLeft.Yes,
                IconSvg = OutlinedIcons.Svg(icon, !UiDirection.IsLeftToRight),
                IconSize = new Size(20, 20),
                IconPosition = UiDirection.IsLeftToRight ? AntdUI.TAlignMini.Left : AntdUI.TAlignMini.Right
            };
            button.Click += (s, e) => action();
            return button;
        }

        private void AddFilter(TableLayoutPanel table, string label, Control control, int col, int row)
        {
            Panel panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8, 2, 8, 2),
                RightToLeft = UiDirection.IsLeftToRight ? RightToLeft.No : RightToLeft.Yes };
            Label caption = new Label
            {
                Text = label, Tag = label,
                Dock = DockStyle.Top,
                Height = 24,
                TextAlign = UiDirection.IsLeftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight,
                Font = AppFonts.Create(9.5F, FontStyle.Bold),
                ForeColor = UiTheme.Text
            };
            control.Dock = DockStyle.Bottom;
            control.Height = 44;
            control.RightToLeft = UiDirection.IsLeftToRight ? RightToLeft.No : RightToLeft.Yes;
            control.Font = AppFonts.Create(10F, FontStyle.Regular);
            UiTheme.ApplyTo(control);
            if (control is ComboBox comboBox)
            {
                UiTheme.ApplyComboBoxQuality(comboBox);
                control = DesktopComponents.NativeSelectFrame(comboBox, comboBox.Name + "Frame", label, true);
                control.Dock = DockStyle.Bottom;
                control.Height = DesktopDesignTokens.InputHeight;
                AntdThemeAdapter.Apply(control);
            }
            if (control is AntdUI.Select selector)
            {
                AntdThemeAdapter.Apply(selector);
            }
            if (control is AntdUI.DatePicker picker)
            {
                picker.RightToLeft = RightToLeft.No;
                picker.TextAlign = HorizontalAlignment.Left;
                AntdThemeAdapter.Apply(picker);
            }
            panel.Controls.Add(control);
            panel.Controls.Add(caption);
            table.Controls.Add(panel, col, row);
        }
    }

    public partial class AccountingReportsForm : Form
    {
        private readonly string initialMode;
        private string activeReport = "BalanceSheet";
        private readonly AntdUI.DatePicker fromDate = DesktopComponents.DatePicker("fromDateFilter", "من تاريخ", DateTime.Today);
        private readonly AntdUI.DatePicker toDate = DesktopComponents.DatePicker("toDateFilter", "إلى تاريخ", DateTime.Today);
        private readonly ComboBox costCenter = new NativeThemedSelect { Name = "costCenterFilter", AccessibleName = "مركز التكلفة" };
        private readonly AntdUI.Panel costCenterFrame;
        private readonly ComboBox branch = new ComboBox();
        private readonly ComboBox level = new ComboBox();
        private readonly ComboBox valuationMethod = new ComboBox();
        private readonly CheckBox includePreviousBalance = new CheckBox();
        private readonly CheckBox excludeInventoryValue = new CheckBox();
        private readonly Label reportTitle = new Label();
        private readonly FlowLayoutPanel reportTabs = new FlowLayoutPanel();
        private readonly FlowLayoutPanel actionsBar = new FlowLayoutPanel();
        private readonly FlowLayoutPanel summaryCards = new FlowLayoutPanel();
        private readonly ComboBox savedTemplates = new NativeThemedSelect { Name = "savedReportTemplates", AccessibleName = "قوالب التقارير المحفوظة" };
        private readonly AntdUI.Panel savedTemplatesFrame;
        private readonly AntdUI.Panel templateContext = new AntdUI.Panel { Name = "accountingReportTemplateContext", Dock = DockStyle.Top, Radius = 12, Shadow = 0, Padding = new Padding(16, 4, 16, 4), Visible = false, Height = 0 };
        private readonly TextBox templateDetails = new TextBox { Name = "accountingReportTemplateDetails", Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, WordWrap = true, BorderStyle = BorderStyle.None, Font = AppFonts.Create(9.5F, FontStyle.Regular) };
        private readonly bool previewMode;
        private bool layingOutTemplateContext;
        private bool templateLeftToRight;
        private readonly Dictionary<AntdUI.Button, string> reportActionCaptions = new Dictionary<AntdUI.Button, string>();
        private readonly List<ReportTemplateChoice> savedTemplateChoices = new List<ReportTemplateChoice>();
        private readonly DataGridView grid = new DataGridView();
        private DataTable currentTable;
        private List<string> activeVisibleColumns;
        private List<string> activeColumnOrder;
        private ReportPrintOptions currentPrintOptions;
        private int reportLoadGeneration;

        private PharmacyManagement.Application.AccountingReportReadRequest loadedRequest;

        public AccountingReportsForm(string mode)
            : this(mode, false)
        {
        }

        internal AccountingReportsForm(string mode, bool previewMode) : this(mode,previewMode,null) { }
        internal AccountingReportsForm(string mode,bool previewMode,Func<AccountingReportsReadSession> sessionFactory)
        {
            reportsSessionFactory=sessionFactory??ApplicationServices.CreateAccountingReportsReadSession;
            this.previewMode = previewMode;
            templateLeftToRight = UiDirection.IsLeftToRight;
            savedTemplatesFrame = DesktopComponents.NativeSelectFrame(savedTemplates, "savedReportTemplatesFrame", "قوالب التقارير المحفوظة");
            costCenterFrame = DesktopComponents.NativeSelectFrame(costCenter, "costCenterFilterFrame", "مركز التكلفة");
            fromDate.MinDate = toDate.MinDate = new DateTime(1753, 1, 1);
            fromDate.MaxDate = toDate.MaxDate = new DateTime(9998, 12, 31);
            initialMode = string.IsNullOrWhiteSpace(mode) ? "TrialBalance" : mode;
            activeReport = initialMode == "Tax" || initialMode == "CostCenters" || initialMode == "GeneralJournal" ||
                           initialMode == "TrialBalance" || initialMode == "ProfitLoss" || initialMode == "BalanceSheet"
                ? initialMode
                : "BalanceSheet";

            Text = GetReportTitle();
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = true;
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(1200, 760);
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            KeyPreview = true;
            InitializeComponent();
            savedTemplates.SelectedIndexChanged += (s, e) =>
            {
                UpdateAccountingTemplateContext();
                KeepAccountingTemplateVisible();
            };
            templateContext.Resize += (s, e) => LayoutAccountingTemplateContext();
            templateDetails.FontChanged += (s, e) => LayoutAccountingTemplateContext();
            UiTheme.Apply(this);
            actionsBar.BackColor = UiTheme.Surface;
            foreach (AntdUI.Button action in actionsBar.Controls.OfType<AntdUI.Button>())
                DesktopComponents.StyleOutlinedFooterAction(action);
            var templatesHost = Controls.Find("accountingReportTemplates", true).OfType<TableLayoutPanel>().FirstOrDefault();
            if (templatesHost != null)
                foreach (AntdUI.Button action in templatesHost.Controls.OfType<AntdUI.Button>())
                    DesktopComponents.StyleOutlinedFooterAction(action, action.Text == "حذف");
            UiTheme.ApplyDataGridQuality(grid);
            FitToWorkingArea();
            LoadDefaults(false);
            costCenter.FormattingEnabled = true;
            costCenter.Format += (s, e) => { if (Convert.ToString(e.ListItem) == "الكل") e.Value = Localize("الكل"); };
            RefreshSavedTemplates();
            ApplyTemplatePresentation(templateLeftToRight);
            Load += (s, e) => FitToWorkingArea();
            Shown += (s, e) =>
            {
                ApplyTemplatePresentation(templateLeftToRight);
                if (!previewMode) BeginInvoke(new Action(LoadData));
            };
            FormClosed += (s, e) => reportLoadGeneration++;
            Disposed += (s, e) => { branch.Dispose(); level.Dispose(); valuationMethod.Dispose(); };
            KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); };
            AttachReportReadLifetime();
        }

        private void FitToWorkingArea()
        {
            Rectangle workingArea = Screen.FromControl(this).WorkingArea;
            MaximumSize = workingArea.Size;
            MinimumSize = new Size(Math.Min(760, workingArea.Width), Math.Min(600, workingArea.Height));
            Size = new Size(Math.Min(Math.Max(Width, MinimumSize.Width), workingArea.Width), Math.Min(Math.Max(Height, MinimumSize.Height), workingArea.Height));
        }

        private void BuildReportTabs()
        {
            foreach (Control oldTab in reportTabs.Controls.Cast<Control>().ToArray())
            {
                reportTabs.Controls.Remove(oldTab);
                oldTab.Dispose();
            }
            reportTabs.Controls.Add(CreateReportTab("الميزانية العمومية", "BalanceSheet"));
            reportTabs.Controls.Add(CreateReportTab("أرباح وخسائر", "ProfitLoss"));
            reportTabs.Controls.Add(CreateReportTab("ميزان المراجعة", "TrialBalance"));
            reportTabs.Controls.Add(CreateReportTab("اليومية العامة", "GeneralJournal"));
            reportTabs.Controls.Add(CreateReportTab("ضريبة", "Tax"));
            reportTabs.Controls.Add(CreateReportTab("مراكز التكلفة", "CostCenters"));
            CenterInParent(reportTabs);
        }

        private string GetReportTitle()
        {
            return Localize(GetReportDisplayTitle(activeReport));
        }

        private string Localize(string arabicText)
        {
            return templateLeftToRight ? UiLanguageCatalog.Translate(arabicText) : arabicText;
        }

        private static string GetReportDisplayTitle(string reportKey)
        {
            switch (reportKey)
            {
                case "GeneralJournal": return "تقرير اليومية العامة";
                case "CostCenters": return "تقرير تحليل مراكز التكلفة";
                case "Tax": return "تقرير الضريبة";
                case "TrialBalance": return "ميزان المراجعة";
                case "ProfitLoss": return "أرباح وخسائر";
                case "BalanceSheet": return "الميزانية العمومية";
                default: return "تقرير نظام الحسابات";
            }
        }

        private AntdUI.Button CreateReportTab(string text, string report)
        {
            bool selected = activeReport == report;
            AntdUI.Button button = new AntdUI.Button
            {
                Text = Localize(text),
                Tag = report,
                Width = 150,
                Height = 40,
                Margin = new Padding(8, 0, 8, 0),
                Font = AppFonts.Create(9.5F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                AccessibleName = Localize(text),
                AccessibleDescription = Localize(selected ? "نوع التقرير المحدد" : "تبديل نوع التقرير"),
                AccessibleRole = AccessibleRole.PageTab,
                RightToLeft = templateLeftToRight ? RightToLeft.No : RightToLeft.Yes,
                Type = selected ? AntdUI.TTypeMini.Primary : AntdUI.TTypeMini.Default,
                BorderWidth = 1
            };
            AntdThemeAdapter.Apply(button);
            button.Click += (s, e) =>
            {
                activeReport = Convert.ToString(button.Tag);
                Text = GetReportTitle();
                reportTitle.Text = GetReportTitle();
                BuildReportTabs();
                ConfigureReportFilters((TableLayoutPanel)Controls.Find("accountingReportFilters", true)[0]);
                LoadData();
            };
            return button;
        }

        private void LoadDefaults(bool loadCostCenters)
        {
            fromDate.Value = new DateTime(DateTime.Today.Year, 1, 1);
            toDate.Value = DateTime.Today;
            costCenter.Items.Add("الكل");
            branch.Items.AddRange(new object[] { "الكل", "الفرع الرئيسي" });
            level.Items.AddRange(new object[] { "1", "2", "3", "4", "5" });
            valuationMethod.Items.AddRange(new object[] { "متوسط سعر التكلفة", "آخر سعر تكلفة" });
            costCenter.SelectedIndex = 0;
            branch.SelectedIndex = 0;
            level.SelectedIndex = 3;
            valuationMethod.SelectedIndex = 0;
        }

        private async void LoadData() { await LoadDataAsync(); }

        private PharmacyManagement.Application.AccountingReportReadRequest CaptureReportRequest()
        {
            DateTime to = RequiredReportDate(toDate, "إلى تاريخ");
            DateTime from = activeReport == "TrialBalance" ? to : RequiredReportDate(fromDate, "من تاريخ");
            return new PharmacyManagement.Application.AccountingReportReadRequest(activeReport,
                from, to, activeReport == "CostCenters" ? SelectedCostCenterId() : null, includePreviousBalance.Checked,
                excludeInventoryValue.Checked);
        }

        private DateTime RequiredReportDate(AntdUI.DatePicker picker, string label)
        {
            DateTime parsed;
            if (!picker.Value.HasValue || !DateTime.TryParseExact(picker.Text, "yyyy/MM/dd",
                System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out parsed) ||
                parsed.Date != picker.Value.Value.Date)
                throw new ArgumentException(Localize(label) + ": " + Localize("اختر تاريخًا صالحًا."));
            return picker.Value.Value.Date;
        }

        private string GetLoadedReportTitle()
        {
            return Localize(GetReportDisplayTitle(loadedRequest == null ? activeReport : loadedRequest.ReportKey));
        }

        private string GetLoadedReportPeriod()
        {
            DateTime to = loadedRequest == null ? RequiredReportDate(toDate, "إلى تاريخ") : loadedRequest.ToDate;
            if ((loadedRequest == null ? activeReport : loadedRequest.ReportKey) == "TrialBalance")
                return Localize("حتى تاريخ") + ": " + to.ToString("yyyy/MM/dd");
            DateTime from = loadedRequest == null ? RequiredReportDate(fromDate, "من تاريخ") : loadedRequest.FromDate;
            return Localize("الفترة") + ": " + from.ToString("yyyy/MM/dd") + " - " + to.ToString("yyyy/MM/dd");
        }

        private void PresentLoadedReportMetadata()
        {
            if (currentTable == null) return;
            reportTitle.Text = GetLoadedReportTitle();
            grid.AccessibleDescription = GetLoadedReportTitle() + Environment.NewLine + GetLoadedReportPeriod();
        }
        private void RefreshSavedTemplates(string selectTemplateName = null)
        {
            savedTemplates.Items.Clear();
            savedTemplateChoices.Clear();
            savedTemplateChoices.Add(new ReportTemplateChoice("القوالب المحفوظة لهذا المستخدم", null));
            foreach (ReportTemplate template in previewMode ? new List<ReportTemplate>() : ReportTemplateStore.Load())
            {
                savedTemplateChoices.Add(new ReportTemplateChoice(template.Name + " — " + GetReportTitle(template.ReportKey), template));
            }
            foreach (ReportTemplateChoice choice in savedTemplateChoices)
                savedTemplates.Items.Add(TemplateChoiceCaption(choice));

            int selectedIndex = 0;
            if (!string.IsNullOrWhiteSpace(selectTemplateName))
            {
                for (int index = 1; index < savedTemplateChoices.Count; index++)
                {
                    ReportTemplateChoice choice = savedTemplateChoices[index];
                    if (choice != null && choice.Template != null && string.Equals(choice.Template.Name, selectTemplateName, StringComparison.CurrentCultureIgnoreCase))
                    {
                        selectedIndex = index;
                        break;
                    }
                }
            }

            savedTemplates.SelectedIndex = selectedIndex;
            UpdateAccountingTemplateContext();
        }

        internal void ApplyTemplatePresentation(bool leftToRight)
        {
            ApplyPresentation(leftToRight);
        }

        internal void ApplyPresentation(bool leftToRight)
        {
            templateLeftToRight = leftToRight;
            Text = GetReportTitle();
            reportTitle.Text = currentTable != null && loadedRequest != null ? GetLoadedReportTitle() : Text;
            includePreviousBalance.Text = Localize("تضمين الرصيد الافتتاحي");
            excludeInventoryValue.Text = Localize("استثناء المخزون من الأرباح والخسائر");
            fromDate.AccessibleName = Localize("من تاريخ");
            toDate.AccessibleName = Localize(activeReport == "TrialBalance" ? "حتى تاريخ" : "إلى تاريخ");
            fromDate.Parent.Controls.OfType<Label>().Single().Text = Localize("من تاريخ");
            toDate.Parent.Controls.OfType<Label>().Single().Text = toDate.AccessibleName;
            costCenterFrame.Parent.Controls.OfType<Label>().Single().Text = Localize("مركز التكلفة");
            costCenter.AccessibleName = Localize("مركز التكلفة");
            savedTemplates.AccessibleName = Localize("قوالب التقارير المحفوظة");
            foreach (var caption in reportActionCaptions)
            {
                caption.Key.Text = caption.Key.AccessibleName = Localize(caption.Value);
                caption.Key.AccessibleDescription = Localize(caption.Key.Parent == actionsBar ? "إجراء تقرير الحسابات" : "إجراء قالب التقرير");
                caption.Key.IconPosition = leftToRight ? AntdUI.TAlignMini.Left : AntdUI.TAlignMini.Right;
            }
            foreach (AntdUI.Button tab in reportTabs.Controls.OfType<AntdUI.Button>())
            {
                string key = Convert.ToString(tab.Tag);
                tab.Text = tab.AccessibleName = Localize(GetReportTitle(key));
                tab.AccessibleDescription = Localize(key == activeReport ? "نوع التقرير المحدد" : "تبديل نوع التقرير");
            }
            int selectedTemplate = savedTemplates.SelectedIndex;
            // Rebuild display captions from retained objects, never reload or translate persisted names.
            savedTemplates.BeginUpdate();
            try
            {
                for (int index = 0; index < savedTemplateChoices.Count; index++)
                    if (index < savedTemplates.Items.Count) savedTemplates.Items[index] = TemplateChoiceCaption(savedTemplateChoices[index]);
                if (selectedTemplate >= 0 && selectedTemplate < savedTemplates.Items.Count) savedTemplates.SelectedIndex = selectedTemplate;
            }
            finally { savedTemplates.EndUpdate(); }
            costCenter.Refresh();
            if (currentTable != null && loadedRequest != null) RenderSummary();
            // Direction belongs to the whole report, including dynamically created controls.
            // Preserve raw report columns, rows and template identifiers during presentation changes.
            grid.Tag = UiTheme.PreserveLocalizedGridDirection;
            // WinForms recreates bound columns with the HWND during RTL changes.
            // Generate them only when accepting a new result schema, not while changing presentation.
            if (grid.Columns.Count > 0) grid.AutoGenerateColumns = false;
            UiDirection.Apply(this, leftToRight);
            DesktopComponents.StyleNativeSelectFrame(savedTemplatesFrame, savedTemplates, leftToRight);
            DesktopComponents.StyleNativeSelectFrame(costCenterFrame, costCenter, leftToRight);
            AntdThemeAdapter.Apply(fromDate);
            AntdThemeAdapter.Apply(toDate);
            AntdThemeAdapter.Apply(templateContext);
            templateContext.Radius = DesktopDesignTokens.CardRadius;
            templateContext.Shadow = 0;
            templateDetails.BackColor = UiTheme.Surface;
            templateDetails.ForeColor = UiTheme.Text;
            templateDetails.BorderStyle = BorderStyle.None;
            UpdateAccountingTemplateContext();
            FormatGridPresentation();
            if (loadedRequest != null) PresentLoadedReportMetadata();
            LayoutReportSummary();
        }

        private string TemplateChoiceCaption(ReportTemplateChoice choice)
        {
            return choice.Template == null ? Localize(choice.ToString()) : choice.Template.Name + " — " + Localize(GetReportTitle(choice.Template.ReportKey));
        }

        private void UpdateAccountingTemplateContext()
        {
            ReportTemplate selected = SelectedSavedTemplateChoice()?.Template;
            templateContext.Visible = selected != null;
            Func<string, string> localize = text => templateLeftToRight ? UiLanguageCatalog.Translate(text) : text;
            string full = selected == null ? string.Empty : localize("القالب المحدد") + ": " + selected.Name +
                Environment.NewLine + localize("مصدر القالب") + ": " + localize(GetReportTitle(selected.ReportKey));
            templateDetails.Text = full;
            templateDetails.AccessibleName = localize("تفاصيل القالب المحدد");
            templateDetails.AccessibleDescription = savedTemplates.AccessibleDescription = full;
            UiDirection.ApplyContentDirection(savedTemplates, templateLeftToRight);
            UiDirection.ApplyContentDirection(templateDetails, templateLeftToRight);
            LayoutAccountingTemplateContext();
        }

        private void LayoutAccountingTemplateContext()
        {
            if (layingOutTemplateContext || templateContext.IsDisposed) return;
            layingOutTemplateContext = true;
            try
            {
                if (SelectedSavedTemplateChoice()?.Template == null) { templateContext.Height = 0; return; }
                templateContext.PerformLayout();
                int lines = templateDetails.GetLineFromCharIndex(templateDetails.TextLength) + 1;
                int lineHeight = templateDetails.Font.Height;
                if (lines > 1)
                    lineHeight = Math.Max(lineHeight, templateDetails.GetPositionFromCharIndex(
                        templateDetails.GetFirstCharIndexFromLine(1)).Y - templateDetails.GetPositionFromCharIndex(0).Y);
                templateContext.Height = templateContext.Padding.Vertical + lines * lineHeight + 6;
            }
            finally { layingOutTemplateContext = false; }
            KeepAccountingTemplateVisible();
        }

        private void KeepAccountingTemplateVisible()
        {
            if (!templateContext.Visible || SelectedSavedTemplateChoice()?.Template == null || !(templateContext.Parent is Panel)) return;
            var host = (Panel)templateContext.Parent;
            host.PerformLayout();
            host.ScrollControlIntoView(templateContext);
        }

        private void CustomizeReportColumns()
        {
            if(!previewMode&&!EnsureReportOutputCurrent())return;
            var columnSession=reportsReadSession;var columnRequest=loadedRequest;
            if (currentTable == null || grid.Columns.Count == 0)
            {
                ShowReportMessage("اعرض نتيجة التقرير أولاً ثم خصص أعمدته.", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            List<string> visibleColumns;
            List<string> columnOrder;
            if (!ReportColumnLayoutDialog.TryGetLayout(this, grid.Columns.Cast<DataGridViewColumn>(), out visibleColumns, out columnOrder, templateLeftToRight))
            {
                return;
            }

            if(!previewMode&&!SameReportOutputContext(columnSession,columnRequest))return;
            activeVisibleColumns = visibleColumns;
            activeColumnOrder = columnOrder;
            ApplyColumnLayout();
            UiDirection.Apply(grid, templateLeftToRight);
        }

        private void SaveCurrentTemplate()
        {
            if (previewMode) return;
            if(!EnsureReportTemplateContext())return;
            var templateSession=reportsReadSession;
            PharmacyManagement.Application.AccountingReportReadRequest request;
            try { request = CaptureReportRequest(); }
            catch (ArgumentException exception) { UiTheme.ShowOperationIssue(this, "حفظ قالب التقرير", exception, templateLeftToRight); return; }
            string name;
            if (!ReportTemplateNameDialog.TryGetName(this, GetReportTitle(), out name, templateLeftToRight))
            {
                return;
            }

            if(!SameReportOperationContext(templateSession))return;
            ReportTemplate template = new ReportTemplate
            {
                Name = name,
                ReportKey = activeReport,
                FromDate = request.FromDate,
                ToDate = request.ToDate,
                CostCenter = Convert.ToString(costCenter.SelectedItem),
                Branch = branch.Text,
                Level = level.Text,
                ValuationMethod = valuationMethod.Text,
                IncludePreviousBalance = includePreviousBalance.Checked,
                ExcludeInventoryValue = excludeInventoryValue.Checked,
                VisibleColumns = CaptureVisibleColumns(),
                ColumnOrder = CaptureColumnOrder(),
                HasPrintSettings = currentPrintOptions != null,
                PrintOrientation = (currentPrintOptions?.Orientation ?? ReportPageOrientation.Automatic).ToString(),
                PrintDensity = (currentPrintOptions?.Density ?? ReportDensity.Standard).ToString(),
                PrintCompanyName = currentPrintOptions?.ShowCompanyName ?? false,
                PrintLogo = currentPrintOptions?.ShowLogo ?? false,
                PrintTaxNumber = currentPrintOptions?.ShowTaxNumber ?? false,
                PrintCommercialRegistration = currentPrintOptions?.ShowCommercialRegistration ?? false,
                PrintMobile = currentPrintOptions?.ShowMobile ?? false,
                PrintAddress = currentPrintOptions?.ShowAddress ?? false,
                PrintWebsite = currentPrintOptions?.ShowWebsite ?? false
            };

            if (!ReportTemplateStore.Save(template))
            {
                ShowReportMessage("تعذر حفظ القالب. تأكد من الاسم والفترة الزمنية ثم أعد المحاولة.", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            RefreshSavedTemplates(name);
                ShowReportMessage("تم حفظ قالب التقرير لهذا المستخدم؛ يحفظ الفلاتر والخيارات دون بيانات التقرير.", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void LoadSelectedTemplate()
        {
            if(!EnsureReportTemplateContext())return;
            ReportTemplateChoice choice = SelectedSavedTemplateChoice();
            if (choice == null || choice.Template == null)
            {
                ShowReportMessage("اختر قالباً محفوظاً أولاً.", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            ReportTemplate template = choice.Template;
            if (!ReportTemplateStore.IsAllowedReportKey(template.ReportKey))
            {
                ShowReportMessage("هذا القالب غير صالح وتم تجاهله بأمان.", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                RefreshSavedTemplates();
                return;
            }

            ApplyAccountingTemplateFields(template);
            LoadData();
        }

        private void ApplyAccountingTemplateFields(ReportTemplate template)
        {
            activeReport = template.ReportKey;
            Text = GetReportTitle();
            reportTitle.Text = GetReportTitle();
            // Clear first so an equal cached Value cannot retain stale invalid editor text.
            fromDate.Value = null;
            toDate.Value = null;
            fromDate.Value = template.FromDate;
            toDate.Value = template.ToDate;
            SelectText(costCenter, template.CostCenter);
            SelectText(branch, template.Branch);
            SelectText(level, template.Level);
            SelectText(valuationMethod, template.ValuationMethod);
            includePreviousBalance.Checked = template.IncludePreviousBalance;
            excludeInventoryValue.Checked = template.ExcludeInventoryValue;
            activeVisibleColumns = template.VisibleColumns == null ? new List<string>() : template.VisibleColumns.ToList();
            activeColumnOrder = template.ColumnOrder == null ? new List<string>() : template.ColumnOrder.ToList();
            currentPrintOptions = template.HasPrintSettings ? new ReportPrintOptions
            {
                Orientation = Enum.TryParse(template.PrintOrientation, out ReportPageOrientation orientation) ? orientation : ReportPageOrientation.Automatic,
                Density = Enum.TryParse(template.PrintDensity, out ReportDensity density) ? density : ReportDensity.Standard,
                ShowCompanyName = template.PrintCompanyName, ShowLogo = template.PrintLogo,
                ShowTaxNumber = template.PrintTaxNumber, ShowCommercialRegistration = template.PrintCommercialRegistration,
                ShowMobile = template.PrintMobile, ShowAddress = template.PrintAddress, ShowWebsite = template.PrintWebsite
            } : null;
            BuildReportTabs();
            ConfigureReportFilters((TableLayoutPanel)Controls.Find("accountingReportFilters", true)[0]);
        }

        private void DeleteSelectedTemplate()
        {
            if (previewMode) return;
            if(!EnsureReportTemplateContext())return;
            var templateSession=reportsReadSession;
            ReportTemplateChoice choice = SelectedSavedTemplateChoice();
            if (choice == null || choice.Template == null)
            {
                ShowReportMessage("اختر قالباً محفوظاً لحذفه.", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (ShowReportMessage("هل تريد حذف قالب «{0}»؟ لا يؤثر ذلك على بيانات التقارير.", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, choice.Template.Name) != DialogResult.Yes)
            {
                return;
            }

            if(!SameReportOperationContext(templateSession))return;
            if (!ReportTemplateStore.Delete(choice.Template.TemplateId))
            {
                ShowReportMessage("تعذر حذف القالب.", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            RefreshSavedTemplates();
        }

        private static void SelectText(ComboBox comboBox, string value)
        {
            int index = comboBox.FindStringExact(value ?? string.Empty);
            comboBox.SelectedIndex = index >= 0 ? index : 0;
        }

        private List<string> CaptureVisibleColumns()
        {
            return grid.Columns.Cast<DataGridViewColumn>().Where(column => column.Visible).Select(column => column.Name).ToList();
        }

        private List<string> CaptureColumnOrder()
        {
            return grid.Columns.Cast<DataGridViewColumn>().OrderBy(column => column.DisplayIndex).Select(column => column.Name).ToList();
        }

        private void ApplyColumnLayout()
        {
            if (grid.Columns.Count == 0)
            {
                return;
            }

            HashSet<string> existingColumns = new HashSet<string>(grid.Columns.Cast<DataGridViewColumn>().Select(column => column.Name), StringComparer.Ordinal);
            HashSet<string> visibleColumns = activeVisibleColumns == null || activeVisibleColumns.Count == 0
                ? null
                : new HashSet<string>(activeVisibleColumns.Where(existingColumns.Contains), StringComparer.Ordinal);

            // A template from an older report schema must not hide every current column.
            if (visibleColumns != null && visibleColumns.Count == 0) visibleColumns = null;

            foreach (DataGridViewColumn column in grid.Columns)
            {
                column.Visible = visibleColumns == null || visibleColumns.Contains(column.Name);
            }

            if (activeColumnOrder == null || activeColumnOrder.Count == 0)
            {
                return;
            }

            int displayIndex = 0;
            foreach (string columnName in activeColumnOrder.Where(existingColumns.Contains).Distinct(StringComparer.Ordinal))
            {
                grid.Columns[columnName].DisplayIndex = displayIndex++;
            }
        }

        private static string GetReportTitle(string reportKey)
        {
            switch (reportKey)
            {
                case "GeneralJournal": return "اليومية العامة";
                case "CostCenters": return "مراكز التكلفة";
                case "Tax": return "الضريبة";
                case "TrialBalance": return "ميزان المراجعة";
                case "ProfitLoss": return "أرباح وخسائر";
                case "BalanceSheet": return "الميزانية العمومية";
                default: return "تقرير غير معروف";
            }
        }

        private ReportTemplateChoice SelectedSavedTemplateChoice()
        {
            int index = savedTemplates.SelectedIndex;
            return index >= 0 && index < savedTemplateChoices.Count ? savedTemplateChoices[index] : null;
        }

        private sealed class ReportTemplateChoice
        {
            internal ReportTemplateChoice(string text, ReportTemplate template) { Text = text; Template = template; }
            internal ReportTemplate Template { get; private set; }
            public override string ToString() { return Text; }
            private string Text { get; set; }
        }

        private void RenderSummary()
        {
            foreach (Control card in summaryCards.Controls.Cast<Control>().ToArray())
                card.Dispose();
            summaryCards.Controls.Clear();
            summaryCards.Visible = currentTable != null;
            if (currentTable == null)
            {
                return;
            }

            var summary = AccountingReportsPresentation.Summarize(currentTable);
            string resultReport = loadedRequest == null ? activeReport : loadedRequest.ReportKey;
            DateTime resultTo = loadedRequest == null ? RequiredReportDate(toDate, "إلى تاريخ") : loadedRequest.ToDate;
            DateTime resultFrom = loadedRequest == null ? (resultReport == "TrialBalance" ? resultTo : RequiredReportDate(fromDate, "من تاريخ")) : loadedRequest.FromDate;

            if (resultReport == "GeneralJournal")
            {
                summaryCards.Controls.Add(CreateMetricCard("عدد السجلات", summary.RowCount.ToString("N0")));
                summaryCards.Controls.Add(CreateMetricCard("إجمالي (المدين)", summary.Total("مدين").ToString("N2")));
                summaryCards.Controls.Add(CreateMetricCard("إجمالي (الدائن)", summary.Total("دائن").ToString("N2")));
                return;
            }

            if (resultReport == "CostCenters")
            {
                summaryCards.Controls.Add(CreateMetricCard("عدد الحسابات المتأثرة", summary.RowCount.ToString("N0")));
                summaryCards.Controls.Add(CreateSummaryCard("إجمالي الفترة المحددة", summary.Total("مدين"), summary.Total("دائن")));
                summaryCards.Controls.Add(CreateProfitCard("صافي مركز التكلفة", summary.Total("الصافي")));
                summaryCards.Controls.Add(CreateMetricCard("من تاريخ", resultFrom.ToString("yyyy/MM/dd")));
                summaryCards.Controls.Add(CreateMetricCard("إلى تاريخ", resultTo.ToString("yyyy/MM/dd")));
                return;
            }

            if (resultReport == "Tax")
            {
                summaryCards.Controls.Add(CreateSummaryCard("حركة الضريبة", summary.Total("مدين"), summary.Total("دائن")));
                summaryCards.Controls.Add(CreateMetricCard("صافي الضريبة", summary.Total("صافي الضريبة").ToString("N2")));
                summaryCards.Controls.Add(CreateMetricCard("عدد الحسابات", summary.RowCount.ToString("N0")));
                summaryCards.Controls.Add(CreateMetricCard("الفترة", resultFrom.ToString("yyyy/MM/dd") + " - " + resultTo.ToString("yyyy/MM/dd")));
                return;
            }

            if (resultReport == "TrialBalance")
            {
                summaryCards.Controls.Add(CreateSummaryCard("ميزان المراجعة", summary.Total("مدين"), summary.Total("دائن")));
                summaryCards.Controls.Add(CreateMetricCard("الرصيد الصافي", summary.Total("الرصيد").ToString("N2")));
                summaryCards.Controls.Add(CreateMetricCard("عدد الحسابات", summary.RowCount.ToString("N0")));
                summaryCards.Controls.Add(CreateMetricCard("حتى تاريخ", resultTo.ToString("yyyy/MM/dd")));
                return;
            }

            decimal openingDebit = summary.Total("الرصيد الافتتاحي مدين");
            decimal openingCredit = summary.Total("الرصيد الافتتاحي دائن");
            decimal previousDebit = summary.Total("الرصيد السابق مدين");
            decimal previousCredit = summary.Total("الرصيد السابق دائن");
            decimal movementDebit = summary.Total("حركة الحساب مدين");
            decimal movementCredit = summary.Total("حركة الحساب دائن");
            decimal balanceDebit = summary.Total("رصيد الحساب مدين");
            decimal balanceCredit = summary.Total("رصيد الحساب دائن");
            decimal profit = summary.MovementNet;

            summaryCards.Controls.Add(CreateSummaryCard("الرصيد الافتتاحي", openingDebit, openingCredit));
            summaryCards.Controls.Add(CreateSummaryCard("الرصيد السابق", previousDebit, previousCredit));
            summaryCards.Controls.Add(CreateSummaryCard("حركة الحساب", movementDebit, movementCredit));
            summaryCards.Controls.Add(CreateSummaryCard("رصيد الحساب", balanceDebit, balanceCredit));
            summaryCards.Controls.Add(CreateProfitCard(resultReport == "ProfitLoss" ? (profit < 0m ? "صافي الخسارة" : "صافي الربح") : "صافي الحركة", profit));
        }

        private AntdUI.Panel CreateReportCard(string title, string value, Color valueColor, float valueSize)
        {
            var card = new AntdUI.Panel
            {
                Width = 310, Height = 106, Margin = new Padding(5), Padding = new Padding(14),
                Back = UiTheme.Surface, BackColor = Color.Transparent,
                Radius = DesktopDesignTokens.CardRadius, Shadow = 0, BorderWidth = 1, BorderColor = UiTheme.Border,
                AccessibleName = Localize(title), AccessibleDescription = value
            };
            var values = new Label
            {
                Name = "accountingReportSummaryValue", Text = value, Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter, RightToLeft = RightToLeft.No,
                Font = AppFonts.Create(valueSize, FontStyle.Bold), ForeColor = valueColor,
                BackColor = Color.Transparent, AutoEllipsis = true, AccessibleName = value
            };
            var heading = new Label
            {
                Name = "accountingReportSummaryTitle", Text = Localize(title), Dock = DockStyle.Top,
                Height = 24, TextAlign = templateLeftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight,
                RightToLeft = RightToLeft.No, Font = AppFonts.Create(10F, FontStyle.Bold),
                ForeColor = UiTheme.MutedText, BackColor = Color.Transparent, AutoEllipsis = true,
                AccessibleName = Localize(title)
            };
            card.Controls.Add(values); card.Controls.Add(heading);
            return card;
        }
        private AntdUI.Panel CreateSummaryCard(string title, decimal debit, decimal credit)
        {
            return CreateReportCard(title, Localize("مدين") + ": " + debit.ToString("N2") + Environment.NewLine +
                Localize("دائن") + ": " + credit.ToString("N2"), UiTheme.Text, 10F);
        }
        private AntdUI.Panel CreateProfitCard(string title, decimal value)
        {
            return CreateReportCard(title, value.ToString("N2"), value < 0 ? UiTheme.Danger : UiTheme.Primary, 13F);
        }
        private AntdUI.Panel CreateMetricCard(string title, string value)
        {
            return CreateReportCard(title, value, UiTheme.Text, 12F);
        }
        private void CenterInParent(Control control)
        {
            if (control == null || control.Parent == null) return;
            control.Left = Math.Max(0, (control.Parent.ClientSize.Width - control.Width) / 2);
        }

        private void BindReportTable(DataTable table)
        {
            grid.AutoGenerateColumns = true;
            grid.DataSource = table;
            grid.AutoGenerateColumns = false;
            FormatGrid();
        }

        private void FormatGrid()
        {
            FormatGridPresentation();
            ApplyColumnLayout();
        }

        private void FormatGridPresentation()
        {
            if (grid.Columns.Count > 0) grid.AutoGenerateColumns = false;
            foreach (DataGridViewColumn column in grid.Columns)
            {
                column.HeaderText = Localize(column.Name);
                column.SortMode = DataGridViewColumnSortMode.NotSortable;
                if (column.ValueType == typeof(decimal) || column.ValueType == typeof(double) || column.ValueType == typeof(float))
                {
                    column.DefaultCellStyle.Format = "N2";
                }
            }

            if (grid.Columns.Contains("البيان")) grid.Columns["البيان"].FillWeight = 190;
            if (grid.Columns.Contains("الحساب")) grid.Columns["الحساب"].FillWeight = 150;
            if (grid.Columns.Contains("اسم الحساب")) grid.Columns["اسم الحساب"].FillWeight = 150;
            if (grid.Columns.Contains("مركز التكلفة")) grid.Columns["مركز التكلفة"].FillWeight = 140;
            if (grid.Columns.Contains("رقم الحساب")) grid.Columns["رقم الحساب"].FillWeight = 85;
            if (grid.Columns.Contains("المستند")) grid.Columns["المستند"].FillWeight = 70;
            if (grid.Columns.Contains("نوع العملية")) grid.Columns["نوع العملية"].FillWeight = 110;
            if (grid.Columns.Contains("عدد الحركات")) grid.Columns["عدد الحركات"].FillWeight = 75;
            UiDirection.Apply(grid, templateLeftToRight);
        }

        private int? SelectedCostCenterId()
        {
            if (costCenter.SelectedIndex <= 0 || costCenter.Text == "بدون")
            {
                return null;
            }

            string value = costCenter.Text;
            int dash = value.IndexOf(" - ", StringComparison.Ordinal);
            int id;
            if (dash > 0 && int.TryParse(value.Substring(0, dash), out id))
            {
                return id;
            }

            return null;
        }

        private void ExportCsv()
        {
            if (previewMode) return;
            if(!EnsureReportOutputCurrent())return;
            var outputSession=reportsReadSession;var outputRequest=loadedRequest;
            if (currentTable == null || currentTable.Rows.Count == 0)
            {
                ShowReportMessage("لا توجد بيانات للتصدير.", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                using (DataTable exportTable = GetVisibleTable())
                using (SaveFileDialog dialog = new SaveFileDialog { Filter = "Excel Workbook (*.xlsx)|*.xlsx", DefaultExt = "xlsx", AddExtension = true, FileName = "AccountingReport.xlsx" })
                {
                    if (exportTable.Columns.Count == 0)
                    {
                        ShowReportMessage("يجب إظهار عمود واحد على الأقل.", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
                    if (dialog.ShowDialog(this) != DialogResult.OK) return;
                    if(!SameReportOutputContext(outputSession,outputRequest))return;
                    ExcelWorkbookWriter.WriteReportCompleteFile(exportTable, Text, dialog.FileName, templateLeftToRight);
                    ShowReportMessage("تم تصدير التقرير بنجاح.", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception exception)
            {
                AppNotifications.ShowFileOutputIssue(this, "تصدير التقرير", exception, templateLeftToRight);
            }
        }

        private DataTable GetVisibleTable()
        {
            if (currentTable == null) return null;
            DataGridViewColumn[] columns = grid.Columns.Cast<DataGridViewColumn>()
                .Where(column => column.Visible && currentTable.Columns.Contains(column.Name))
                .OrderBy(column => column.DisplayIndex).ToArray();
            var result = new DataTable(currentTable.TableName) { Locale = currentTable.Locale };
            foreach (DataGridViewColumn column in columns)
            {
                DataColumn sourceColumn = currentTable.Columns[column.Name];
                result.Columns.Add(sourceColumn.ColumnName, sourceColumn.DataType).Caption = sourceColumn.Caption;
            }
            if (columns.Length == 0) return result;
            foreach (DataRow row in currentTable.Rows)
                result.Rows.Add(columns.Select(column => row[column.Name]).ToArray());
            return result;
        }

        private void PrintReport()
        {
            if (previewMode) return;
            if(!EnsureReportOutputCurrent())return;
            var outputSession=reportsReadSession;var outputRequest=loadedRequest;
            try
            {
                using (DataTable printTable = GetPrintTable())
                {
                    if (printTable != null && printTable.Columns.Count == 0)
                    {
                        ShowReportMessage("يجب إظهار عمود واحد على الأقل.", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
                    if (printTable == null || printTable.Rows.Count == 0)
                    {
                        ShowReportMessage("لا توجد بيانات جاهزة للطباعة.", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
                    string subtitle = GetLoadedReportPeriod();
                    ReportPrintOptions selected;
                    if (DataTablePrintPreview.ShowWithOptions(this, GetLoadedReportTitle(), subtitle, printTable, currentPrintOptions, out selected, templateLeftToRight, isCurrent:()=>!IsDisposed&&!Disposing&&ReferenceEquals(outputSession,reportsReadSession)&&outputSession!=null&&outputSession.IsCurrent&&ReferenceEquals(outputRequest,loadedRequest))
                        && selected != null && SameReportOutputContext(outputSession,outputRequest)) currentPrintOptions = selected.Copy();
                }
            }
            catch (Exception exception)
            {
                UiTheme.ShowOperationIssue(this, "طباعة التقرير", exception, templateLeftToRight);
            }
        }

        private DataTable GetPrintTable()
        {
            if (currentTable == null) return null;
            DataTable snapshot = GetVisibleTable();
            foreach (DataColumn column in snapshot.Columns)
                column.Caption = Localize(column.ColumnName);
            return snapshot;
        }

        private AntdUI.Button CreateActionButton(string text, AppIcon icon, Action action)
        {
            AntdUI.Button button = new AntdUI.Button
            {
                Text = text,
                Width = 156,
                Height = 44,
                Margin = new Padding(5),
                Font = AppFonts.Create(10F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Enabled = !previewMode || icon == AppIcon.Close,
                AccessibleName = text,
                AccessibleDescription = "إجراء تقرير الحسابات",
                Name = icon==AppIcon.Print?"accountingReportPrint":icon==AppIcon.Report?"accountingReportExport":icon==AppIcon.Search?"accountingReportReload":"accountingReportClose",
                AccessibleRole = AccessibleRole.PushButton,
                RightToLeft = templateLeftToRight ? RightToLeft.No : RightToLeft.Yes,
                IconSvg = OutlinedIcons.Svg(icon, true),
                IconSize = new Size(18, 18),
                IconPosition = AntdUI.TAlignMini.Right
            };
            DesktopComponents.StyleOutlinedFooterAction(button);
            reportActionCaptions[button] = text;
            button.Click += (s, e) => action();
            return button;
        }

        private string FormatReportMessage(string arabicText, params object[] values)
        {
            return values.Length == 0 ? Localize(arabicText) : string.Format(Localize(arabicText), values);
        }

        private DialogResult ShowReportMessage(string arabicText, MessageBoxButtons buttons, MessageBoxIcon icon, params object[] values)
        {
            return AppNotifications.Show(this, FormatReportMessage(arabicText, values), Text, buttons, icon);
        }

        private void AddFilter(TableLayoutPanel table, string label, Control control, int col, int row)
        {
            Panel panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(4, 2, 4, 2), Tag = "UiSurface",
                MaximumSize = control is AntdUI.DatePicker ? new Size(420, 0) : Size.Empty };
            Label caption = new Label
            {
                Text = label,
                Dock = DockStyle.Top,
                Height = 24,
                TextAlign = ContentAlignment.MiddleRight,
                Font = AppFonts.Create(9.5F, FontStyle.Bold),
                ForeColor = UiTheme.Text
            };
            control.Dock = DockStyle.Bottom;
            control.Height = DesktopDesignTokens.InputHeight;
            control.Font = AppFonts.Create(10F, FontStyle.Regular);
            UiTheme.ApplyTo(control);
            panel.Controls.Add(control);
            panel.Controls.Add(caption);
            table.Controls.Add(panel, col, row);
        }
    }

    public partial class AccountingDataForm : Form
    {
        protected readonly Panel Editor = new Panel();
        protected readonly DataGridView Grid = new DataGridView();

        protected AccountingDataForm(string title)
        {
            InitializeComponent(title);
            ApplyAccountingDataVisualTheme();
            UiTheme.ApplyDataGridQuality(Grid);
            UiTheme.InstallStatusStrip(this);
            KeyPreview = true;
            KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); };
            Load += (s, e) =>
            {
                FitToAvailableArea();
                LayoutAccountingDataForm();
            };
            Resize += (s, e) => LayoutAccountingDataForm();
        }

        private void FitToAvailableArea()
        {
            Rectangle area = Owner == null
                ? Screen.FromControl(this).WorkingArea
                : Screen.FromControl(Owner).WorkingArea;

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

        protected void LayoutAccountingDataForm()
        {
            if (Editor == null || Grid == null || IsDisposed)
            {
                return;
            }

            // Apply after the form handle and RTL layout exist; the initial dock
            // assignment during construction is mirrored inconsistently by WinForms.
            Editor.Dock = DockStyle.Left;
            Editor.Width = Math.Max(280, Math.Min(360, ClientSize.Width / 3));
            LayoutHeaderCloseButton();
        }

        protected AntdUI.Button AccountingCloseAction => CloseButton;
        protected void AttachAccountingCloseAction(Control footer)
        {
            if (footer == null) throw new ArgumentNullException(nameof(footer));
            footer.Controls.Add(CloseButton); AccountingCloseFooter.Visible = false;
        }
        protected void ApplyAccountingClosePresentation(bool english)
        {
            accountingCloseLeftToRight = english;
            CloseButton.Text = english ? "Close" : "إغلاق";
            CloseButton.AccessibleName = CloseButton.Text;
            CloseButton.IconPosition = english ? AntdUI.TAlignMini.Left : AntdUI.TAlignMini.Right;
            CloseButton.RightToLeft = english ? RightToLeft.No : RightToLeft.Yes;
            DesktopComponents.StyleOutlinedFooterAction(CloseButton);
            LayoutHeaderCloseButton();
        }
        private void LayoutHeaderCloseButton()
        {
            if (CloseButton == null || AccountingCloseFooter == null) return;
            if (CloseButton.Parent == AccountingCloseFooter)
            {
                int width = Math.Min(Math.Max(104, TextRenderer.MeasureText(CloseButton.Text, CloseButton.Font).Width + 48),
                    Math.Max(1, AccountingCloseFooter.ClientSize.Width - 24));
                CloseButton.SetBounds(accountingCloseLeftToRight ? 12 : AccountingCloseFooter.ClientSize.Width - width - 12, 10, width, 44);
            }
            CloseButton.BringToFront();
            DesktopComponents.StyleOutlinedFooterAction(CloseButton);
        }
        protected virtual void LoadData()
        {
        }

        protected void AddField(string label, Control control)
        {
            Panel field = new Panel
            {
                Name = "accountingField_" + (string.IsNullOrEmpty(control.Name) ? Editor.Controls.Count.ToString() : control.Name),
                Dock = DockStyle.Top,
                Height = 78,
                BackColor = UiTheme.Surface
            };
            Label caption = new Label
            {
                Name = "accountingFieldLabel_" + control.Name,
                Text = label,
                Height = 22,
                Dock = DockStyle.Top,
                // Alignment is physical; inheriting RTL would mirror it a second time.
                RightToLeft = RightToLeft.No,
                TextAlign = UiDirection.IsLeftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight,
                ForeColor = UiTheme.Text,
                Font = AppFonts.Create(9.5F, FontStyle.Bold)
            };
            control.Dock = DockStyle.Bottom;
            control.Height = DesktopDesignTokens.InputHeight;
            UiTheme.ApplyTo(control);
            field.Controls.Add(control);
            field.Controls.Add(caption);
            Editor.Controls.Add(field);
            field.BringToFront();
        }

        protected void AddSeparator(string text)
        {
            Label separator = new Label
            {
                Text = text,
                Dock = DockStyle.Top,
                Height = 34,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = AppFonts.Create(10F, FontStyle.Bold),
                ForeColor = UiTheme.Text
            };
            Editor.Controls.Add(separator);
            separator.BringToFront();
        }

        protected Button CreateActionButton(string text)
        {
            Button button = new Button
            {
                Text = text,
                Dock = DockStyle.Top,
                Height = 38,
                BackColor = UiTheme.Primary,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                AccessibleName = text,
                AccessibleDescription = "إجراء في شاشة الحسابات"
            };
            button.FlatAppearance.BorderSize = 0;
            UiTheme.ApplyPrimaryButton(button);
            WinFormsShapeAdapter.ApplyAction(button);
            return button;
        }

        private void ApplyAccountingDataVisualTheme()
        {
            BackColor = UiTheme.Background;
            HeaderPanel.BackColor = UiTheme.PrimaryDark;
            Editor.BackColor = UiTheme.Surface;
            AccountingCloseFooter.BackColor = UiTheme.Surface;
            ApplyAccountingClosePresentation(accountingCloseLeftToRight);
        }

        protected void LoadAccounts(params ComboBox[] combos)
        {
            DataTable accounts = AccountingLookupDesktopService.LoadAccounts(true);
            foreach (ComboBox combo in combos)
            {
                combo.Items.Clear();
                foreach (DataRow row in accounts.Rows)
                {
                    combo.Items.Add(row["AccountCode"] + " - " + row["AccountName"]);
                }
                if (combo.Items.Count > 0)
                {
                    combo.SelectedIndex = 0;
                }
            }
        }

        protected string SelectedAccount(ComboBox combo)
        {
            string value = combo.Text;
            int dash = value.IndexOf(" - ", StringComparison.Ordinal);
            return dash > 0 ? value.Substring(0, dash).Trim() : value.Trim();
        }

        protected string SelectedAccount(AntdUI.Select combo)
        {
            string value = combo.Text;
            int dash = value.IndexOf(" - ", StringComparison.Ordinal);
            return dash > 0 ? value.Substring(0, dash).Trim() : value.Trim();
        }

        protected decimal ParseMoney(string value)
        {
            decimal result;
            return decimal.TryParse(value, out result) ? result : 0;
        }
    }
}













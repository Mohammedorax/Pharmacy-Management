// شرح الملف: الشاشة الرئيسية للتنقل بين وحدات النظام.
// تم تحسين التنقل حتى لا تتراكم شاشات مخفية في الذاكرة.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace project_fo_3
{
    // شاشة لوحة التحكم الرئيسية للتنقل بين وحدات النظام.
    // تم ضبط التنقل بحيث لا تتراكم نماذج مخفية في الذاكرة.
    public partial class Home : Form
    {
        private const int AlertStripHeight = 74;
        private const int GettingStartedPanelHeight = 112;
        private readonly Size normalWindowSize = new Size(1463, 774);
        private FlowLayoutPanel alertStrip;
        private Panel gettingStartedPanel;
        private Label gettingStartedTitle;
        private Label gettingStartedDescription;
        private AntdUI.Button addFirstMedicineButton;
        private AntdUI.Button openFirstPurchaseButton;
        private AntdUI.Button screenModeButton;
        private AntdUI.Button layoutModeButton;
        private AntdUI.Button workspaceButton;
        private AntdUI.Button workspaceToggleButton;
        private HomeSessionHeader sessionLabel;
        private AntdUI.Button monitorButton;
        private AntdUI.Button dailyOperationsButton;
        private AntdUI.Button accountingTile;
        private Label accountingLabel;
        private AntdUI.Button inventoryTile;
        private Label inventoryLabel;
        private bool isFullScreen;
        private bool isApplyingScreenMode;
        private bool isApplyingHomeLayout;
        private bool isRefreshingGettingStarted;
        private bool isWorkCenterMode = true;
        private bool presentationLeftToRight;
        private HomeWorkspacePreferences workspacePreferences =
            new HomeWorkspacePreferences(HomeWorkspaceMode.Full, HomeWorkspaceModuleCatalog.AllIds());
        private HomeWorkspacePreferences expandedWorkspacePreferences =
            new HomeWorkspacePreferences(HomeWorkspaceMode.Full, HomeWorkspaceModuleCatalog.AllIds());
        private bool homeModuleCardsReady;
        private readonly List<HomeTileAccess> homeTileAccessItems = new List<HomeTileAccess>();
        private readonly ToolTip homeToolTip = new ToolTip();

        // ينشئ الشاشة الرئيسية ويطبق التصميم الموحد.
        public Home()
        {
            presentationLeftToRight = UiDirection.IsLeftToRight;
            InitializeComponent();
            UiTheme.Apply(this);
            KeyPreview = true;
            KeyDown += Home_KeyDown;
            Resize += (sender, args) =>
            {
                if (isApplyingScreenMode)
                {
                    return;
                }

                LayoutHomeHeaderActions();
                LayoutHomeAlertCards();
                LayoutHomeTiles();
            };

            BuildHomeScreenModeControl();
            BuildHomeLayoutModeControl();
            BuildHomeWorkspaceControl();
            BuildHomeWorkspaceToggleControl();
            BuildOperationalHeader();
            panel1.Resize += (sender, args) => LayoutHomeHeaderActions();
            // Docked viewport geometry settles after the Form resize notification.
            panelContent.SizeChanged += (sender, args) => LayoutHomeTiles();
            panelContent.VisibleChanged += (sender, args) => { if (panelContent.Visible) LayoutHomeTiles(); };
            BuildAlertStrip();
            BuildGettingStartedPanel();
            ApplyHomeVisualPolish();
            ApplySavedHomeLayout();
            ApplySavedWorkspacePreferences();
            ApplyInitialScreenMode();
            ApplyPresentation(presentationLeftToRight);
            Shown += (sender, args) => { RefreshGettingStartedPanelAsync(); RefreshAlertStrip(); };
            UiSessionSubscription.Attach(this, InvalidateHomeAlertReads);
            Disposed += (sender, args) =>
            {
                CancelHomeAlertReads();
                homeToolTip.Dispose();
            };
            Activated += (sender, args) =>
            {
                UpdateSessionHeader();
                if (SessionContext.IsAuthenticated) ApplyHomeAccessRules();
            };
        }

        private string Present(string arabicText)
        {
            return presentationLeftToRight ? UiLanguageCatalog.Translate(arabicText) : arabicText;
        }

        internal void ApplyPresentation(bool leftToRight)
        {
            presentationLeftToRight = leftToRight;
            UiDirection.Apply(this, leftToRight);
            Text = Present("الرئيسية");

            if (sessionLabel != null)
            {
                UpdateSessionHeader();
            }

            if (dailyOperationsButton != null)
                dailyOperationsButton.AccessibleName = Present("سجل العمليات اليومية");
            if (monitorButton != null)
                monitorButton.AccessibleName = Present("مراقبة النظام");
            if (screenModeButton != null)
                screenModeButton.AccessibleName = Present("تبديل وضع الشاشة");
            if (layoutModeButton != null)
                layoutModeButton.AccessibleName = Present("تبديل تخطيط الصفحة الرئيسية");
            if (workspaceButton != null)
                workspaceButton.AccessibleName = Present("تخصيص مساحة العمل");
            if (workspaceToggleButton != null)
                workspaceToggleButton.AccessibleName = Present("تبديل كثافة مساحة العمل");

            ApplyGettingStartedPresentation();
            ApplyHomeVisualPolish();
            UpdateLayoutModeButtonText();
            UpdateWorkspaceButtonText();
            UpdateWorkspaceToggleButtonText();
            UpdateScreenModeButtonText();
            foreach (var action in new[] { screenModeButton, layoutModeButton, workspaceButton, workspaceToggleButton, monitorButton, dailyOperationsButton, addFirstMedicineButton, openFirstPurchaseButton }) ApplyHeaderActionStyle(action);
            LayoutHomeHeaderActions();
            LayoutHomeAlertCards();
            LayoutHomeTiles(leftToRight);
        }

        private void LayoutHomeTiles() => LayoutHomeTiles(presentationLeftToRight);

        private void LayoutHomeTiles(bool leftToRight)
        {
            if (panelContent == null || panelContent.IsDisposed || isApplyingHomeLayout)
            {
                return;
            }

            isApplyingHomeLayout = true;
            try
            {
                TileLayoutItem[] items = GetHomeTileLayoutItems();
                int visibleCount = items.Count(item => item.Icon != null && item.Label != null && item.Icon.Visible);
                if (visibleCount == 0)
                {
                    return;
                }

                int viewportWidth = Math.Max(300, panelContent.ClientRectangle.Width);
                int reservedScrollWidth = SystemInformation.VerticalScrollBarWidth;
                int horizontalPadding = viewportWidth >= 1200 ? 72 : viewportWidth >= 620 ? 44 : 20;
                int rawWidth = Math.Max(280, viewportWidth - (horizontalPadding * 2) - reservedScrollWidth);
                // تسعة أقسام رئيسية تبدو أكثر اتساقاً في شبكة 3×3؛ لا نترك صفاً أخيراً
                // وحيداً أو نسمح لبطاقات عريضة جداً بأن تضعف مسار المسح البصري.
                int columns = rawWidth >= 900 ? 3 : rawWidth >= 620 ? 2 : 1;
                int gapX = columns == 1 ? 0 : rawWidth >= 900 ? 36 : 24;
                int gapY = rawWidth >= 900 ? 24 : 22;
                int availableWidth = Math.Max(300, rawWidth);
                int tileWidth = Math.Min(420, Math.Max(220, (availableWidth - (gapX * (columns - 1))) / columns));
                int tileHeight = Math.Max(118, Math.Min(172, tileWidth * 42 / 100));
                // مركز العمل يبرز عمليات اليوم فقط على الشاشات العريضة؛ في الشاشات
                // الأصغر نرجع تلقائياً للشبكة كي لا يصبح أي عنصر مقصوصاً أو مزدحماً.
                bool useWorkCenterLayout = isWorkCenterMode && columns == 3;
                int primaryTileHeight = useWorkCenterLayout
                    ? Math.Min(208, Math.Max(tileHeight, tileHeight + 28))
                    : tileHeight;
                int labelHeight = Math.Max(
                    32,
                    items.Max(item => item.Label == null ? 0 : TextRenderer.MeasureText(item.Label.Text, item.Label.Font).Height) + 8);
                int cellHeight = tileHeight + labelHeight + gapY;
                int gridWidth = (tileWidth * columns) + (gapX * (columns - 1));
                int left = Math.Max(horizontalPadding, (viewportWidth - gridWidth) / 2);
                // لا نعتمد على Bottom لشريط Dock؛ أثناء إعادة التخطيط قد تكون
                // قيمته مؤقتة فتظهر البطاقات خلفه أو مقصوصة من الأعلى.
                LayoutGettingStartedPanel();
                int alertHeight = alertStrip != null && alertStrip.Visible
                    ? Math.Max(AlertStripHeight, alertStrip.Height)
                    : 0;
                int gettingStartedHeight = gettingStartedPanel != null && gettingStartedPanel.Visible
                    ? Math.Max(GettingStartedPanelHeight, gettingStartedPanel.Height)
                    : 0;
                int top = alertHeight + gettingStartedHeight + 28;
                // Fit the complete desktop grid when readable cards can stay above the fold.
                int visibleRows = (int)Math.Ceiling(visibleCount / (double)columns);
                if (!useWorkCenterLayout && columns == 3)
                {
                    int fittedTileHeight = (panelContent.ClientSize.Height - top - 20) / visibleRows - labelHeight - gapY;
                    if (fittedTileHeight >= 118)
                    {
                        tileHeight = Math.Min(tileHeight, fittedTileHeight);
                        primaryTileHeight = tileHeight;
                        cellHeight = tileHeight + labelHeight + gapY;
                    }
                }
                int index = 0;

                panelContent.SuspendLayout();
                try
                {
                    foreach (TileLayoutItem item in items)
                    {
                        if (item.Icon == null || item.Label == null || !item.Icon.Visible)
                        {
                            continue;
                        }

                        int column = index % columns;
                        int row = index / columns;
                        int visualColumn = leftToRight ? column : columns - 1 - column;
                        int x = left + visualColumn * (tileWidth + gapX);
                        int y = top + (useWorkCenterLayout && row > 0
                            ? primaryTileHeight + labelHeight + gapY + ((row - 1) * cellHeight)
                            : row * cellHeight);
                        int currentTileHeight = useWorkCenterLayout && row == 0 ? primaryTileHeight : tileHeight;

                        item.Icon.Bounds = new Rectangle(x, y, tileWidth, currentTileHeight);
                        DesktopComponents.StyleNavigationIconCard(item.Icon, GetHomeModuleIcon(item.Icon.Name), presentationLeftToRight);
                        item.Label.Bounds = new Rectangle(x - 8, y + currentTileHeight + 7, tileWidth + 16, labelHeight);
                        item.Label.TextAlign = ContentAlignment.MiddleCenter;
                        item.Label.AutoEllipsis = true;
                        item.Icon.BringToFront();
                        item.Label.BringToFront();
                        index++;
                    }

                    int rows = (int)Math.Ceiling(visibleCount / (double)columns);
                    int contentBottom = top + (useWorkCenterLayout
                        ? primaryTileHeight + labelHeight + gapY + Math.Max(0, rows - 1) * cellHeight
                        : rows * cellHeight) + 20;
                    // العرض حُسب من مساحة العميل نفسها؛ لا نفرض عرضاً أكبر وننشئ
                    // تمريراً أفقياً وهمياً على الشاشات الصغيرة أو مع DPI مرتفع.
                    panelContent.AutoScrollMinSize = new Size(0, contentBottom);
                    if (contentBottom <= panelContent.ClientSize.Height)
                    {
                        panelContent.AutoScrollPosition = Point.Empty;
                    }
                    ApplyHomeAccessRules();
                    // البطاقات تُعاد إلى المقدمة أثناء توزيعها، لذا نعيد شريط
                    // التنبيهات فوقها دائماً حتى لا يحجب الجزء العلوي من الصف الأول.
                    if (alertStrip != null && alertStrip.Visible)
                    {
                        alertStrip.BringToFront();
                    }
                    if (gettingStartedPanel != null && gettingStartedPanel.Visible)
                    {
                        gettingStartedPanel.BringToFront();
                    }
                }
                finally
                {
                    panelContent.ResumeLayout();
                }
            }
            finally
            {
                isApplyingHomeLayout = false;
            }
        }

        private TileLayoutItem[] GetHomeTileLayoutItems()
        {
            // في واجهة RTL يُعرض أول عنصر في أقصى اليمين. نرتب البطاقات بحسب
            // دورة العمل اليومية: المتابعة ثم البيع/الشراء ثم المخزون، ونضع
            // الإدارة والإعدادات في الصفوف اللاحقة بدلاً من خلطها مع العمليات.
            TileLayoutItem[] all = isWorkCenterMode
                ? new[]
                {
                    new TileLayoutItem("Sales", pictureBox4, label6),
                    new TileLayoutItem("Purchases", pictureBox6, label9),
                    new TileLayoutItem("Dashboard", DashBoard, label7),
                    new TileLayoutItem("Inventory", inventoryTile, inventoryLabel),
                    new TileLayoutItem("Medicines", pictureBox1, label3),
                    new TileLayoutItem("CustomersSuppliers", pictureBox3, label5),
                    new TileLayoutItem("Accounting", accountingTile, accountingLabel),
                    new TileLayoutItem("Employees", pictureBox2, label4),
                    new TileLayoutItem("Settings", butSetings, label8)
                }
                : new[]
                {
                    new TileLayoutItem("Dashboard", DashBoard, label7),
                    new TileLayoutItem("Sales", pictureBox4, label6),
                    new TileLayoutItem("Purchases", pictureBox6, label9),
                    new TileLayoutItem("Inventory", inventoryTile, inventoryLabel),
                    new TileLayoutItem("Medicines", pictureBox1, label3),
                    new TileLayoutItem("CustomersSuppliers", pictureBox3, label5),
                    new TileLayoutItem("Accounting", accountingTile, accountingLabel),
                    new TileLayoutItem("Employees", pictureBox2, label4),
                    new TileLayoutItem("Settings", butSetings, label8)
                };

            IEnumerable<string> visibleIds;
            if (workspacePreferences == null || workspacePreferences.Mode == HomeWorkspaceMode.Full)
                visibleIds = all.Select(item => item.Id);
            else if (workspacePreferences.Mode == HomeWorkspaceMode.Basic)
                visibleIds = HomeWorkspaceModuleCatalog.BasicIds();
            else
                visibleIds = workspacePreferences.OrderedVisibleModuleIds;

            var visible = new HashSet<string>(visibleIds, StringComparer.OrdinalIgnoreCase);
            foreach (TileLayoutItem item in all)
            {
                bool show = visible.Contains(item.Id);
                if (item.Icon != null) item.Icon.Visible = show;
                if (item.Label != null) item.Label.Visible = show;
            }

            if (workspacePreferences != null && workspacePreferences.Mode == HomeWorkspaceMode.Custom)
            {
                Dictionary<string, TileLayoutItem> byId = all.ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
                return workspacePreferences.OrderedVisibleModuleIds
                    .Where(byId.ContainsKey)
                    .Select(id => byId[id])
                    .ToArray();
            }

            return all.Where(item => visible.Contains(item.Id)).ToArray();
        }

        private void BuildHomeScreenModeControl()
        {
            screenModeButton = new AccessibleAntdButton
            {
                Name = "screenModeButton",
                Cursor = Cursors.Hand,
                Size = new Size(130, 44),
                Font = AppFonts.Create(9.5F, FontStyle.Bold),
                BackColor = UiTheme.SurfaceVariant,
                ForeColor = UiTheme.Text,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                AccessibleName = "تبديل وضع الشاشة"
            };
            ApplyHeaderActionStyle(screenModeButton);
            screenModeButton.Click += (sender, args) => ToggleScreenMode();
            panel1.Controls.Add(screenModeButton);
            screenModeButton.BringToFront();
        }

        private void BuildHomeLayoutModeControl()
        {
            layoutModeButton = new AccessibleAntdButton
            {
                Name = "layoutModeButton",
                Cursor = Cursors.Hand,
                Size = new Size(132, 44),
                Font = AppFonts.Create(9.5F, FontStyle.Bold),
                BackColor = UiTheme.SurfaceVariant,
                ForeColor = UiTheme.Text,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                AccessibleName = "تبديل تخطيط الصفحة الرئيسية"
            };
            ApplyHeaderActionStyle(layoutModeButton);
            layoutModeButton.Click += (sender, args) => ToggleHomeLayout();
            panel1.Controls.Add(layoutModeButton);
            layoutModeButton.BringToFront();
        }

        private void BuildHomeWorkspaceControl()
        {
            workspaceButton = new AccessibleAntdButton
            {
                Name = "workspaceButton",
                Cursor = Cursors.Hand,
                Size = new Size(132, 44),
                Font = AppFonts.Create(9.5F, FontStyle.Bold),
                BackColor = UiTheme.SurfaceVariant,
                ForeColor = UiTheme.Text,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                AccessibleName = "تخصيص مساحة العمل"
            };
            ApplyHeaderActionStyle(workspaceButton);
            workspaceButton.Click += (sender, args) => OpenWorkspaceSettings();
            panel1.Controls.Add(workspaceButton);
            workspaceButton.BringToFront();
        }

        private void BuildHomeWorkspaceToggleControl()
        {
            workspaceToggleButton = new AccessibleAntdButton
            {
                Name = "workspaceToggleButton",
                Cursor = Cursors.Hand,
                Size = new Size(132, 44),
                Font = AppFonts.Create(9.5F, FontStyle.Bold),
                BackColor = UiTheme.SurfaceVariant,
                ForeColor = UiTheme.Text,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                AccessibleName = "تبديل كثافة مساحة العمل"
            };
            ApplyHeaderActionStyle(workspaceToggleButton);
            workspaceToggleButton.Click += (sender, args) => ToggleWorkspaceDensity();
            panel1.Controls.Add(workspaceToggleButton);
            workspaceToggleButton.BringToFront();
        }

        private void ApplySavedHomeLayout()
        {
            string savedMode = Properties.Setings.Default.HomeLayoutMode;
            isWorkCenterMode = !string.Equals(savedMode, "Grid", StringComparison.OrdinalIgnoreCase);
            UpdateLayoutModeButtonText();
        }

        private void ApplySavedWorkspacePreferences()
        {
            workspacePreferences = HomeWorkspacePreferencesStore.Load(SessionContext.CurrentUser);
            expandedWorkspacePreferences = workspacePreferences.Mode == HomeWorkspaceMode.Basic
                ? new HomeWorkspacePreferences(HomeWorkspaceMode.Full, HomeWorkspaceModuleCatalog.AllIds())
                : workspacePreferences.Clone();
            UpdateWorkspaceButtonText();
            UpdateWorkspaceToggleButtonText();
            if (panelContent != null) panelContent.AutoScrollPosition = Point.Empty;
            LayoutHomeTiles();
        }

        internal void ApplyWorkspacePreferencesForPreview(HomeWorkspacePreferences preferences)
        {
            workspacePreferences = (preferences ??
                new HomeWorkspacePreferences(HomeWorkspaceMode.Full, HomeWorkspaceModuleCatalog.AllIds())).Clone();
            if (workspacePreferences.Mode != HomeWorkspaceMode.Basic)
                expandedWorkspacePreferences = workspacePreferences.Clone();
            UpdateWorkspaceButtonText();
            UpdateWorkspaceToggleButtonText();
            LayoutHomeTiles();
        }

        private void OpenWorkspaceSettings()
        {
            using (var dialog = new HomeWorkspaceSettingsForm(workspacePreferences, presentationLeftToRight))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    HomeWorkspacePreferencesStore.Save(SessionContext.CurrentUser, dialog.SelectedPreferences);
                    workspacePreferences = dialog.SelectedPreferences.Clone();
                    if (workspacePreferences.Mode != HomeWorkspaceMode.Basic)
                        expandedWorkspacePreferences = workspacePreferences.Clone();
                    UpdateWorkspaceButtonText();
                    UpdateWorkspaceToggleButtonText();
                    LayoutHomeTiles();
                }
                catch (Exception ex)
                {
                    UiTheme.ShowOperationIssue(this, Present("حفظ تخصيص مساحة العمل"), ex);
                }
            }
        }

        private void UpdateWorkspaceButtonText()
        {
            if (workspaceButton == null) return;
            HomeWorkspaceMode mode = workspacePreferences == null ? HomeWorkspaceMode.Full : workspacePreferences.Mode;
            string modeText = mode == HomeWorkspaceMode.Basic ? "أساسي" : mode == HomeWorkspaceMode.Custom ? "مخصص" : "كامل";
            workspaceButton.Text = Present("المساحة: ") + Present(modeText);
            workspaceButton.AccessibleDescription = presentationLeftToRight
                ? "Choose the visible home modules and their order. Current mode: " + Present(modeText) + "."
                : "اختيار الأقسام الظاهرة وترتيبها. الوضع الحالي: " + modeText + ".";
            homeToolTip.SetToolTip(workspaceButton, workspaceButton.AccessibleDescription);
        }

        private void ToggleWorkspaceDensity()
        {
            if (workspacePreferences != null && workspacePreferences.Mode == HomeWorkspaceMode.Basic)
            {
                workspacePreferences = (expandedWorkspacePreferences ??
                    new HomeWorkspacePreferences(HomeWorkspaceMode.Full, HomeWorkspaceModuleCatalog.AllIds())).Clone();
            }
            else
            {
                expandedWorkspacePreferences = (workspacePreferences ??
                    new HomeWorkspacePreferences(HomeWorkspaceMode.Full, HomeWorkspaceModuleCatalog.AllIds())).Clone();
                workspacePreferences = new HomeWorkspacePreferences(
                    HomeWorkspaceMode.Basic,
                    HomeWorkspaceModuleCatalog.BasicIds());
            }

            UpdateWorkspaceButtonText();
            UpdateWorkspaceToggleButtonText();
            if (panelContent != null) panelContent.AutoScrollPosition = Point.Empty;
            LayoutHomeTiles();
        }

        private void UpdateWorkspaceToggleButtonText()
        {
            if (workspaceToggleButton == null) return;
            bool compact = workspacePreferences != null && workspacePreferences.Mode == HomeWorkspaceMode.Basic;
            workspaceToggleButton.Text = Present(compact ? "توسيع الأقسام" : "اختصار الأقسام");
            workspaceToggleButton.AccessibleName = Present(compact ? "توسيع الأقسام" : "اختصار الأقسام");
            workspaceToggleButton.AccessibleDescription = presentationLeftToRight
                ? (compact
                    ? "Restore the previous expanded workspace for this session."
                    : "Temporarily show the essential daily modules only.")
                : (compact
                    ? "استعادة مساحة العمل الموسعة السابقة لهذه الجلسة."
                    : "عرض الأقسام اليومية الأساسية مؤقتًا فقط.");
            ApplyHeaderActionStyle(workspaceToggleButton);
            homeToolTip.SetToolTip(workspaceToggleButton, workspaceToggleButton.AccessibleDescription);
        }

        private void ToggleHomeLayout()
        {
            isWorkCenterMode = !isWorkCenterMode;
            try
            {
                Properties.Setings.Default.HomeLayoutMode = isWorkCenterMode ? "WorkCenter" : "Grid";
                Properties.Setings.Default.Save();
            }
            catch (Exception ex)
            {
                AppDiagnostics.Log("HomeLayoutPreference", ex);
                AppNotifications.Show(this, "تم تبديل العرض لهذه الجلسة، لكن تعذر حفظ التفضيل.", "الصفحة الرئيسية", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            UpdateLayoutModeButtonText();
            LayoutHomeTiles();
        }

        private void UpdateLayoutModeButtonText()
        {
            if (layoutModeButton == null)
            {
                return;
            }

            layoutModeButton.Text = Present(isWorkCenterMode ? "العرض الشبكي" : "مركز العمل");
            layoutModeButton.AccessibleDescription = presentationLeftToRight
                ? (isWorkCenterMode
                    ? "Switch the home page to an equal grid."
                    : "Switch the home page to a work center focused on sales, purchases, and monitoring.")
                : (isWorkCenterMode
                    ? "تغيير الصفحة الرئيسية إلى العرض الشبكي المتساوي."
                    : "تغيير الصفحة الرئيسية إلى مركز عمل يبرز المبيعات والمشتريات والمتابعة.");
            homeToolTip.SetToolTip(layoutModeButton, layoutModeButton.AccessibleDescription);
        }

        private void ApplyInitialScreenMode()
        {
            // Each workspace starts maximized; restoring it remains available for this session.
            isFullScreen = true;
            ApplyCurrentScreenMode();
        }

        private void ApplyCurrentScreenMode()
        {
            isApplyingScreenMode = true;
            try
            {
                ScreenModeSettings.Apply(this, isFullScreen, normalWindowSize);
                AutoScroll = false;
            }
            finally
            {
                isApplyingScreenMode = false;
            }

            UpdateScreenModeButtonText();
            LayoutHomeHeaderActions();
            LayoutHomeTiles();
        }

        private void ToggleScreenMode()
        {
            isFullScreen = !isFullScreen;
            ApplyCurrentScreenMode();
        }

        private void UpdateScreenModeButtonText()
        {
            if (screenModeButton == null)
            {
                return;
            }

            screenModeButton.Text = Present(isFullScreen ? "وضع عادي" : "ملء الشاشة");
            ApplyHeaderActionStyle(screenModeButton);
            screenModeButton.AccessibleDescription = presentationLeftToRight
                ? (isFullScreen ? "Restore the home window." : "Show the home window in full screen.")
                : (isFullScreen ? "استعادة نافذة الصفحة الرئيسية إلى حجمها العادي." : "تكبير نافذة الصفحة الرئيسية إلى ملء الشاشة.");
            homeToolTip.SetToolTip(screenModeButton, screenModeButton.AccessibleDescription);
        }

        private void LayoutHomeHeaderActions()
        {
            if (panel1 == null) return;
            bool compact = panel1.Width < 1260;
            if (dailyOperationsButton != null) dailyOperationsButton.Text = presentationLeftToRight ? (compact ? "Operations" : "Daily operations") : (compact ? "العمليات" : "عمليات اليوم");
            var actions = new[] { dailyOperationsButton, monitorButton, screenModeButton, layoutModeButton, workspaceToggleButton, workspaceButton };
            if (actions.Any(action => action == null)) return;
            if (compact)
            {
                monitorButton.Text = Present("مراقبة");
                screenModeButton.Text = Present(isFullScreen ? "عادي" : "كامل");
                layoutModeButton.Text = Present("تخطيط");
                workspaceButton.Text = Present("تخصيص");
                workspaceToggleButton.Text = Present(workspacePreferences != null && workspacePreferences.Mode == HomeWorkspaceMode.Basic ? "توسيع" : "اختصار");
            }
            else
            {
                monitorButton.Text = Present("مراقبة النظام");
                UpdateScreenModeButtonText(); UpdateLayoutModeButtonText(); UpdateWorkspaceButtonText(); UpdateWorkspaceToggleButtonText();
            }
            const int gap = 8, inset = 12;
            int available = Math.Max(1, panel1.ClientSize.Width - inset * 2);
            int desired = Math.Max(112, actions.Max(action => TextRenderer.MeasureText(action.Text, action.Font, Size.Empty, TextFormatFlags.SingleLine | TextFormatFlags.NoPadding).Width + 40));
            int columns = Math.Min(actions.Length, Math.Max(1, (available + gap) / (desired + gap)));
            int width = Math.Min(desired, Math.Max(1, (available - (columns - 1) * gap) / columns));
            int rows = (actions.Length + columns - 1) / columns;
            panel1.Height = 82 + rows * 52 + 8;
            for (int index = 0; index < actions.Length; index++)
            {
                var action = actions[index];
                int x = presentationLeftToRight ? inset + (index % columns) * (width + gap) : panel1.ClientSize.Width - inset - width - (index % columns) * (width + gap);
                action.Bounds = new Rectangle(x, 82 + (index / columns) * 52, width, 44);
                action.BringToFront();
            }
            if (pictureBox5 != null) pictureBox5.Bounds = new Rectangle(presentationLeftToRight ? Math.Max(12, panel1.Width - 58) : 12, 12, 44, 44);
            if (sessionLabel != null)
            {
                sessionLabel.Visible = !compact;
                sessionLabel.Bounds = new Rectangle(presentationLeftToRight ? 24 : panel1.Width - 244, 8, 220, 60);
            }
            int titleLeft = presentationLeftToRight ? (compact ? 12 : 270) : 70;
            int titleRightInset = presentationLeftToRight ? 70 : (compact ? 12 : 270);
            int titleWidth = Math.Max(110, panel1.Width - titleLeft - titleRightInset);
            label1.Bounds = new Rectangle(titleLeft, 9, titleWidth, 28);
            label2.Bounds = new Rectangle(titleLeft, 37, titleWidth, 24);
            label1.BringToFront(); label2.BringToFront();
        }
        private void Home_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F11)
            {
                ToggleScreenMode();
                e.Handled = true;
            }
            else if (e.Control && e.Shift && e.KeyCode == Keys.H)
            {
                OpenWorkspaceSettings();
                e.Handled = true;
            }
            else if (e.Control && e.Shift && e.KeyCode == Keys.M)
            {
                ToggleWorkspaceDensity();
                e.Handled = true;
            }
        }

        private void BuildOperationalHeader()
        {
            sessionLabel = new HomeSessionHeader();
            UpdateSessionHeader();

            monitorButton = new AccessibleAntdButton
            {
                Name = "monitorButton",
                Text = "مراقبة النظام",
                Font = AppFonts.Create(9.5F, FontStyle.Bold),
                BackColor = UiTheme.SurfaceVariant,
                ForeColor = UiTheme.Text,
                Cursor = Cursors.Hand,
                AccessibleName = "مراقبة النظام"
            };
            ApplyHeaderActionStyle(monitorButton);
            monitorButton.Click += (sender, args) =>
            {
                OpenHomeModule(AppPermission.SystemMonitor, () => new SystemMonitorForm());
            };
            monitorButton.Enabled = true;
            monitorButton.Cursor = PermissionService.CanOpen(AppPermission.SystemMonitor) ? Cursors.Hand : Cursors.No;

            panel1.Controls.Add(sessionLabel);
            panel1.Controls.Add(monitorButton);
            dailyOperationsButton = new AccessibleAntdButton { Name="dailyOperationsButton", Text=Present("عمليات اليوم"), AccessibleName=Present("سجل العمليات اليومية"), Font=AppFonts.Create(9.5F,FontStyle.Bold), Cursor=Cursors.Hand };
            ApplyHeaderActionStyle(dailyOperationsButton);
            dailyOperationsButton.Click += (s,e) => {
                if (!SessionContext.IsAuthenticated) return;
                ShowChild(new DailyOperationsForm());
            };
            panel1.Controls.Add(dailyOperationsButton);
            LayoutHomeHeaderActions();
        }

        private void ApplyHeaderActionStyle(AntdUI.Button action)
        {
            if (action == null) return;
            action.SizeChanged -= HomeActionSizeChanged;
            action.SizeChanged += HomeActionSizeChanged;
            DesktopComponents.StyleOutlinedFooterAction(action);
            if (action.Font.Size != 9.5F || action.Font.Style != FontStyle.Bold) action.Font = AppFonts.Create(9.5F, FontStyle.Bold);
            action.Cursor = action.Enabled ? Cursors.Hand : Cursors.No;
            action.IconSize = action == pictureBox5 ? new Size(24, 24) : new Size(16, 16);
            action.IconPosition = presentationLeftToRight ? AntdUI.TAlignMini.Left : AntdUI.TAlignMini.Right;
            switch (action.Name)
            {
                case "pictureBox5": action.IconSvg = OutlinedIcons.Svg(AppIcon.Logout); break;
                case "screenModeButton": action.IconSvg = OutlinedIcons.Svg(isFullScreen ? AppIcon.FullscreenExit : AppIcon.Fullscreen); break;
                case "layoutModeButton": action.IconSvg = OutlinedIcons.Svg(AppIcon.Dashboard); break;
                case "workspaceButton": action.IconSvg = OutlinedIcons.Svg(AppIcon.Settings); break;
                case "workspaceToggleButton": action.IconSvg = workspacePreferences != null && workspacePreferences.Mode == HomeWorkspaceMode.Basic ? OutlinedIcons.Down() : OutlinedIcons.Up(); break;
                case "dailyOperationsButton": action.IconSvg = OutlinedIcons.Svg(AppIcon.Report); break;
                case "monitorButton": action.IconSvg = OutlinedIcons.Svg(AppIcon.Report); break;
                case "addFirstMedicineButton": action.IconSvg = OutlinedIcons.Svg(AppIcon.Add); break;
                case "openFirstPurchaseButton": action.IconSvg = OutlinedIcons.Svg(AppIcon.Sale); break;
            }
        }
        private void HomeActionSizeChanged(object sender, EventArgs e) => ApplyHeaderActionStyle(sender as AntdUI.Button);

        private void UpdateSessionHeader()
        {
            if (sessionLabel == null || sessionLabel.IsDisposed || IsDisposed || Disposing) return;
            string databaseName = null;
            try { databaseName = HomeWorkspaceService.GetCurrentDatabaseName(); }
            catch (Exception ex) { AppDiagnostics.Log("HomeConnectionIndicator", ex); }
            sessionLabel.SetDetails(SessionContext.CurrentUser, SessionContext.CurrentRole, databaseName, presentationLeftToRight);
            homeToolTip.SetToolTip(sessionLabel, sessionLabel.Text);
            label1.AccessibleDescription = sessionLabel.Text;
            homeToolTip.SetToolTip(label1, sessionLabel.Text);
            foreach (Control line in sessionLabel.Controls) homeToolTip.SetToolTip(line, line.Text);
        }

        private void BuildGettingStartedPanel()
        {
            gettingStartedPanel = new Panel
            {
                Name = "gettingStartedPanel",
                Dock = DockStyle.Top,
                Height = GettingStartedPanelHeight,
                BackColor = UiTheme.Surface,
                BorderStyle = BorderStyle.FixedSingle,
                Visible = false,
                RightToLeft = RightToLeft.Yes,
                AccessibleName = "إرشاد بدء تشغيل النظام"
            };

            gettingStartedTitle = new Label
            {
                Text = "ابدأ بتجهيز بيانات الصيدلية",
                Font = AppFonts.Create(11F, FontStyle.Bold),
                ForeColor = UiTheme.PrimaryDark,
                TextAlign = ContentAlignment.MiddleRight,
                AccessibleName = "عنوان إرشاد البدء"
            };

            gettingStartedDescription = new Label
            {
                Text = "لا توجد أصناف أو عمليات مسجلة بعد. أضف الصنف الأول، ثم سجّل أول فاتورة مشتريات لتحديث المخزون.",
                Font = AppFonts.Create(9.25F),
                ForeColor = UiTheme.MutedText,
                TextAlign = ContentAlignment.MiddleRight,
                AccessibleName = "وصف خطوات بدء التشغيل"
            };

            addFirstMedicineButton = new AccessibleAntdButton
            {
                Name = "addFirstMedicineButton",
                Text = "إضافة الصنف الأول",
                AccessibleName = "إضافة الصنف الأول",
                AccessibleDescription = "يفتح شاشة الأدوية لإضافة أول صنف في قاعدة جديدة."
            };
            ApplyHeaderActionStyle(addFirstMedicineButton);
            addFirstMedicineButton.Click += (sender, args) =>
                OpenHomeModule(AppPermission.ManageMedicines, () => new Medicine());

            openFirstPurchaseButton = new AccessibleAntdButton
            {
                Name = "openFirstPurchaseButton",
                Text = "فتح المشتريات",
                AccessibleName = "فتح المشتريات",
                AccessibleDescription = "يفتح شاشة المشتريات لإضافة مخزون بعد تجهيز الأصناف."
            };
            ApplyHeaderActionStyle(openFirstPurchaseButton);
            openFirstPurchaseButton.Click += (sender, args) =>
                OpenHomeModule(AppPermission.Purchases, () => new Purchases());

            gettingStartedPanel.Controls.Add(gettingStartedTitle);
            gettingStartedPanel.Controls.Add(gettingStartedDescription);
            gettingStartedPanel.Controls.Add(addFirstMedicineButton);
            gettingStartedPanel.Controls.Add(openFirstPurchaseButton);
            panelContent.Controls.Add(gettingStartedPanel);
            gettingStartedPanel.BringToFront();
            LayoutGettingStartedPanel();
        }

        private void ApplyGettingStartedPresentation()
        {
            if (gettingStartedPanel == null) return;

            gettingStartedPanel.AccessibleName = Present("إرشاد بدء تشغيل النظام");
            gettingStartedTitle.Text = Present("ابدأ بتجهيز بيانات الصيدلية");
            gettingStartedTitle.AccessibleName = Present("عنوان إرشاد البدء");
            gettingStartedDescription.Text = Present("لا توجد أصناف أو عمليات مسجلة بعد. أضف الصنف الأول، ثم سجّل أول فاتورة مشتريات لتحديث المخزون.");
            gettingStartedDescription.AccessibleName = Present("وصف خطوات بدء التشغيل");
            addFirstMedicineButton.Text = Present("إضافة الصنف الأول");
            addFirstMedicineButton.AccessibleName = addFirstMedicineButton.Text;
            addFirstMedicineButton.AccessibleDescription = presentationLeftToRight
                ? "Open medicines to add the first item to a new database."
                : "يفتح شاشة الأدوية لإضافة أول صنف في قاعدة جديدة.";
            openFirstPurchaseButton.Text = Present("فتح المشتريات");
            openFirstPurchaseButton.AccessibleName = openFirstPurchaseButton.Text;
            openFirstPurchaseButton.AccessibleDescription = presentationLeftToRight
                ? "Open purchases to add stock after preparing medicines."
                : "يفتح شاشة المشتريات لإضافة مخزون بعد تجهيز الأصناف.";
            gettingStartedTitle.TextAlign = presentationLeftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
            gettingStartedDescription.TextAlign = presentationLeftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
            LayoutGettingStartedPanel();
        }

        private async void RefreshGettingStartedPanelAsync()
        {
            if (isRefreshingGettingStarted || gettingStartedPanel == null || IsDisposed)
            {
                return;
            }

            isRefreshingGettingStarted = true;
            try
            {
                DatabaseStartupState state = await Task.Run(() => HomeWorkspaceService.GetStartupState());
                if (IsDisposed)
                {
                    return;
                }

                gettingStartedPanel.Visible = state.IsNewInstallation;
                UpdateGettingStartedActions();
            }
            catch (Exception ex)
            {
                // فشل الإرشاد لا يحجب لوحة العمل؛ لا نفترض أن القاعدة فارغة عند انقطاع الاتصال.
                AppDiagnostics.Log("HomeGettingStarted", ex);
                gettingStartedPanel.Visible = false;
            }
            finally
            {
                isRefreshingGettingStarted = false;
                if (!IsDisposed)
                {
                    LayoutHomeTiles();
                }
            }
        }

        private void UpdateGettingStartedActions()
        {
            if (addFirstMedicineButton != null)
            {
                bool canManageMedicines = PermissionService.CanOpen(AppPermission.ManageMedicines);
                addFirstMedicineButton.Enabled = canManageMedicines;
                addFirstMedicineButton.Cursor = canManageMedicines ? Cursors.Hand : Cursors.No;
            }

            if (openFirstPurchaseButton != null)
            {
                bool canOpenPurchases = PermissionService.CanOpen(AppPermission.Purchases);
                openFirstPurchaseButton.Enabled = canOpenPurchases;
                openFirstPurchaseButton.Cursor = canOpenPurchases ? Cursors.Hand : Cursors.No;
            }
        }

        private void LayoutGettingStartedPanel()
        {
            if (gettingStartedPanel == null || gettingStartedPanel.IsDisposed)
            {
                return;
            }

            int width = Math.Max(320, gettingStartedPanel.ClientSize.Width);
            bool compact = width < 680;
            int horizontalPadding = compact ? 16 : 28;
            int actionWidth = compact ? Math.Max(128, (width - (horizontalPadding * 2) - 12) / 2) : 164;

            bool stackActions = compact && width < 420;
            gettingStartedPanel.Height = stackActions ? 202 : compact ? 156 : GettingStartedPanelHeight;
            gettingStartedTitle.Bounds = new Rectangle(horizontalPadding, 12, compact ? width - (horizontalPadding * 2) : width - (horizontalPadding * 2) - 360, 28);
            gettingStartedDescription.Bounds = new Rectangle(horizontalPadding, 43, compact ? width - (horizontalPadding * 2) : width - (horizontalPadding * 2) - 360, compact ? 38 : 44);

            if (stackActions)
            {
                addFirstMedicineButton.Bounds = new Rectangle(horizontalPadding, 98, width - horizontalPadding * 2, 40);
                openFirstPurchaseButton.Bounds = new Rectangle(horizontalPadding, 146, width - horizontalPadding * 2, 40);
            }
            else if (compact)
            {
                addFirstMedicineButton.Bounds = new Rectangle(width - horizontalPadding - actionWidth, 98, actionWidth, 40);
                openFirstPurchaseButton.Bounds = new Rectangle(horizontalPadding, 98, actionWidth, 40);
            }
            else
            {
                addFirstMedicineButton.Bounds = new Rectangle(width - horizontalPadding - actionWidth, 34, actionWidth, 42);
                openFirstPurchaseButton.Bounds = new Rectangle(width - horizontalPadding - (actionWidth * 2) - 12, 34, actionWidth, 42);
            }
        }

        private void BuildAlertStrip()
        {
            alertStrip = new FlowLayoutPanel
            {
                Height = 76,
                Dock = DockStyle.Top,
                BackColor = UiTheme.Background,
                Padding = new Padding(32, 12, 32, 10),
                // FlowLayoutPanel يعكس ترتيب البطاقات مرتين عند الجمع بين الخاصيتين.
                // نحتفظ باتجاه السطح عاديًا ونضبط التدفق وحده حتى تبدأ التنبيهات من اليمين.
                FlowDirection = FlowDirection.RightToLeft,
                RightToLeft = RightToLeft.No,
                WrapContents = false,
                AutoScroll = true
            };
            panelContent.Controls.Add(alertStrip);
            alertStrip.BringToFront();
            alertStrip.Visible = false;
            alertStrip.SizeChanged += (sender, args) => { LayoutHomeAlertCards(); LayoutHomeTiles(); };
        }

        private Control CreateAlertCard(string title, int count, Color accent, string alertType)
        {
            var card = new AccessibleAntdActionCard { Name = "homeAlertCard_" + alertType,
                Width = 248, Height = 56, Radius = DesktopDesignTokens.CardRadius, Shadow = 0,
                BorderWidth = 1, BorderColor = UiTheme.Border, BackColor = UiTheme.Surface,
                Margin = new Padding(0, 0, 8, 8), Cursor = Cursors.Hand,
                AccessibleName = Present(title) + ": " + count, Tag = title,
                RightToLeft = presentationLeftToRight ? RightToLeft.No : RightToLeft.Yes };
            var countLabel = new Label { Name = "homeAlertCardCount", Text = count.ToString(),
                AutoSize = false, Font = AppFonts.Create(16F, FontStyle.Bold), ForeColor = accent,
                BackColor = UiTheme.Surface, TextAlign = ContentAlignment.MiddleCenter, RightToLeft = RightToLeft.No };
            var titleLabel = new Label { Name = "homeAlertCardTitle", Text = Present(title),
                AutoSize = false, Font = AppFonts.Create(10F, FontStyle.Bold), ForeColor = UiTheme.Text,
                BackColor = UiTheme.Surface, TextAlign = presentationLeftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight };
            card.Controls.Add(titleLabel); card.Controls.Add(countLabel);
            Action layout = () => {
                int numberWidth = Math.Max(44, TextRenderer.MeasureText(countLabel.Text, countLabel.Font).Width + 12);
                int inset = 12, gap = 8;
                if (presentationLeftToRight)
                {
                    countLabel.SetBounds(card.Width - inset - numberWidth, 8, numberWidth, card.Height - 16);
                    titleLabel.SetBounds(inset, 8, Math.Max(1, card.Width - 2 * inset - numberWidth - gap), card.Height - 16);
                }
                else
                {
                    countLabel.SetBounds(inset, 8, numberWidth, card.Height - 16);
                    titleLabel.SetBounds(inset + numberWidth + gap, 8, Math.Max(1, card.Width - 2 * inset - numberWidth - gap), card.Height - 16);
                }
            };
            card.SizeChanged += (sender, args) => layout();
            card.Layout += (sender, args) => layout(); layout();
            EventHandler click = (sender, args) => {
                if (string.IsNullOrWhiteSpace(alertType)) OpenSystemMonitor();
                else OpenAlertDetails(alertType, title, accent);
            };
            card.Click += click; titleLabel.Click += (sender, args) => card.PerformAction();
            countLabel.Click += (sender, args) => card.PerformAction();
            return card;
        }

        private void LayoutHomeAlertCards()
        {
            if (alertStrip == null || alertStrip.IsDisposed) return;
            alertStrip.SuspendLayout();
            try
            {
                alertStrip.Padding = new Padding(12, 8, 12, 8);
                alertStrip.FlowDirection = presentationLeftToRight ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;
                alertStrip.RightToLeft = RightToLeft.No; alertStrip.WrapContents = true;
                alertStrip.AutoScroll = false;
                int available = Math.Max(1, alertStrip.ClientSize.Width - alertStrip.Padding.Horizontal);
                int rows = 1, used = 0, rowHeight = 0;
                foreach (Control control in alertStrip.Controls)
                {
                    var card = control as AccessibleAntdActionCard;
                    if (card != null)
                    {
                        var title = (Label)card.Controls["homeAlertCardTitle"];
                        var count = (Label)card.Controls["homeAlertCardCount"];
                        title.Text = Present((string)card.Tag);
                        count.RightToLeft = RightToLeft.No;
                        title.ForeColor = UiTheme.Text; title.BackColor = UiTheme.Surface;
                        count.BackColor = UiTheme.Surface;
                        count.ForeColor = card.Name.EndsWith("Expired") ? UiTheme.Danger : card.Name.EndsWith("Expiring Soon") ? UiTheme.Warning : UiTheme.Primary;
                        card.BackColor = UiTheme.Surface; card.Shadow = 0;
                        card.BorderColor = card.Focused ? UiTheme.Primary : UiTheme.Border;
                        title.TextAlign = presentationLeftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
                        card.AccessibleName = title.Text + ": " + count.Text;
                        card.RightToLeft = presentationLeftToRight ? RightToLeft.No : RightToLeft.Yes;
                        int desired = TextRenderer.MeasureText(title.Text, title.Font).Width +
                            Math.Max(44, TextRenderer.MeasureText(count.Text, count.Font).Width + 12) + 32;
                        card.Width = Math.Min(Math.Max(180, desired), Math.Max(1, available - card.Margin.Horizontal));
                        card.PerformLayout();
                    }
                    else
                    {
                        var status = control as Label;
                        if (status != null)
                        {
                            if (status.Tag is string) status.Text = Present((string)status.Tag);
                            status.ForeColor = UiTheme.MutedText; status.BackColor = UiTheme.Background;
                            status.TextAlign = presentationLeftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
                        }
                        control.Width = available;
                        control.Height = Math.Max(44, TextRenderer.MeasureText(control.Text, control.Font,
                            new Size(available, int.MaxValue), TextFormatFlags.WordBreak).Height + 8);
                    }
                    int width = control.Width + control.Margin.Horizontal;
                    if (used > 0 && used + width > available) { rows++; used = 0; }
                    used += width; rowHeight = Math.Max(rowHeight, control.Height + control.Margin.Vertical);
                }
                alertStrip.Height = Math.Max(AlertStripHeight, alertStrip.Padding.Vertical + rows * rowHeight);
            }
            finally { alertStrip.ResumeLayout(true); }
        }


        internal Form CreateAlertDetailsDialog(string alertType, DataTable rows, Color accent, Size availableSize)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));
            var dialog = new Form
            {
                Name = "homeAlertDetails", Text = Present(GetAlertArabicTitle(alertType)),
                StartPosition = FormStartPosition.CenterParent, ShowInTaskbar = false,
                AutoScaleMode = AutoScaleMode.Dpi, AutoScaleDimensions = new SizeF(96F, 96F),
                Font = AppFonts.Create(10F), MinimizeBox = false, MaximizeBox = true,
                FormBorderStyle = FormBorderStyle.Sizable,
                ClientSize = new Size(Math.Min(800, Math.Max(320, availableSize.Width - 48)),
                    Math.Min(480, Math.Max(240, availableSize.Height - 96)))
            };
            var root = new TableLayoutPanel { Name = "alertDetailsLayout", Dock = DockStyle.Fill,
                ColumnCount = 1, RowCount = 3, Padding = new Padding(12), Margin = Padding.Empty };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 96F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));
            var header = DesktopComponents.Card("alertDetailsHeader");
            header.Dock = DockStyle.Fill; header.Margin = new Padding(0, 0, 0, 12);
            header.Padding = new Padding(12); header.Shadow = 0;
            var headings = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1,
                Margin = Padding.Empty, Padding = Padding.Empty };
            headings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            headings.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 64F));
            var titleLabel = new Label { Name = "alertDetailsTitle", Text = dialog.Text,
                AccessibleName = dialog.Text, Dock = DockStyle.Fill, Margin = Padding.Empty,
                AutoSize = false, Font = AppFonts.Create(12F, FontStyle.Bold),
                TextAlign = presentationLeftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight };
            var countLabel = new Label { Name = "alertDetailsCount", Text = rows.Rows.Count.ToString(),
                AccessibleName = Present("عدد التنبيهات") + ": " + rows.Rows.Count,
                Dock = DockStyle.Fill, Margin = Padding.Empty, AutoSize = false,
                Font = AppFonts.Create(20F, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter };
            headings.Controls.Add(titleLabel, 0, 0); headings.Controls.Add(countLabel, 1, 0);
            header.Controls.Add(headings);
            var grid = new DataGridView { Name = "alertDetailsGrid", Tag = UiTheme.PreserveLocalizedCustomGridColumns, AutoGenerateColumns = true, Dock = DockStyle.Fill,
                DataSource = rows, ReadOnly = true, AllowUserToAddRows = false,
                AllowUserToDeleteRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect, RowHeadersVisible = false,
                Margin = Padding.Empty, AccessibleName = dialog.Text, TabIndex = 0 };
            grid.CellFormatting += (sender, args) =>
            {
                if (args.RowIndex < 0 || args.ColumnIndex < 0 ||
                    grid.Columns[args.ColumnIndex].Name != "Type") return;
                string caption;
                switch (Convert.ToString(args.Value))
                {
                    case "Expired": caption = Present("منتهي"); break;
                    case "Expiring Soon": caption = Present("قريب الانتهاء"); break;
                    case "Low Stock": caption = Present("مخزون منخفض"); break;
                    default: return;
                }
                args.Value = caption;
                args.FormattingApplied = true;
            };
            var footer = new Panel { Name = "alertDetailsFooter", Dock = DockStyle.Fill,
                Margin = Padding.Empty, Padding = Padding.Empty };
            var closeButton = DesktopComponents.Action("alertDetailsClose", Present("إغلاق"), AppIcon.Close);
            closeButton.Font = AppFonts.Create(10F, FontStyle.Bold);
            closeButton.IconPosition = presentationLeftToRight ? AntdUI.TAlignMini.Left : AntdUI.TAlignMini.Right;
            closeButton.DialogResult = DialogResult.Cancel; closeButton.TabIndex = 1;
            closeButton.Click += (sender, args) => dialog.Close();
            footer.Controls.Add(closeButton);
            footer.SizeChanged += (sender, args) =>
            {
                int desired = Math.Max(120, TextRenderer.MeasureText(closeButton.Text, closeButton.Font).Width + 48);
                int width = Math.Min(desired, Math.Max(1, footer.ClientSize.Width));
                closeButton.SetBounds(presentationLeftToRight ? 0 : footer.ClientSize.Width - width, 8, width, 44);
            };
            root.Controls.Add(header, 0, 0); root.Controls.Add(grid, 0, 1); root.Controls.Add(footer, 0, 2);
            dialog.Controls.Add(root); dialog.AcceptButton = closeButton; dialog.CancelButton = closeButton;
            UiTheme.Apply(dialog); UiDirection.Apply(dialog, presentationLeftToRight);
            dialog.Text = Present(GetAlertArabicTitle(alertType));
            titleLabel.Text = dialog.Text; titleLabel.AccessibleName = dialog.Text;
            closeButton.Text = Present("إغلاق"); closeButton.AccessibleName = closeButton.Text;
            root.BackColor = UiTheme.Background; headings.BackColor = UiTheme.Surface;
            footer.BackColor = UiTheme.Background; header.BackColor = UiTheme.Surface;
            titleLabel.ForeColor = UiTheme.Text; countLabel.ForeColor = accent;
            countLabel.RightToLeft = RightToLeft.No;
            closeButton.IconPosition = presentationLeftToRight ? AntdUI.TAlignMini.Left : AntdUI.TAlignMini.Right;
            DesktopComponents.StyleOutlinedFooterAction(closeButton);
            Action layoutShell = () =>
            {
                bool compact = dialog.ClientSize.Height < 320;
                root.Padding = new Padding(compact ? 8 : 12);
                root.RowStyles[0].Height = compact ? 56F : 96F;
                header.Padding = new Padding(compact ? 8 : 12);
                header.Margin = new Padding(0, 0, 0, compact ? 8 : 12);
            };
            dialog.ClientSizeChanged += (sender, args) => layoutShell();
            layoutShell();
            Action configureGrid = () =>
            {
                LocalizeAlertGrid(grid);
                foreach (DataGridViewColumn column in grid.Columns)
                {
                    column.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                    column.Width = column.Name == "Medicine" || column.Name == "Message" ? 200 : 120;
                    column.MinimumWidth = 70;
                }
            };
            grid.DataBindingComplete += (sender, args) => configureGrid();
            dialog.Shown += (sender, args) => configureGrid();
            return dialog;
        }


        private string GetAlertArabicTitle(string alertType)
        {
            switch (alertType)
            {
                case "Expired":
                    return "الأصناف المنتهية";
                case "Expiring Soon":
                    return "الأصناف قرب الانتهاء";
                case "Low Stock":
                    return "الأصناف منخفضة المخزون";
                default:
                    return "تفاصيل التنبيه";
            }
        }

        private void LocalizeAlertGrid(DataGridView grid)
        {
            if (grid.Columns.Contains("Type")) grid.Columns["Type"].HeaderText = Present("النوع");
            if (grid.Columns.Contains("Medicine")) grid.Columns["Medicine"].HeaderText = Present("الصنف");
            if (grid.Columns.Contains("Quantity")) grid.Columns["Quantity"].HeaderText = Present("الكمية");
            if (grid.Columns.Contains("Expiry"))
            {
                grid.Columns["Expiry"].HeaderText = Present("تاريخ الانتهاء");
                grid.Columns["Expiry"].DefaultCellStyle.Format = "yyyy-MM-dd";
            }
            if (grid.Columns.Contains("Company")) grid.Columns["Company"].HeaderText = Present("الشركة");
            if (grid.Columns.Contains("Message")) grid.Columns["Message"].HeaderText = Present("التنبيه");
        }

        private void ApplyHomeVisualPolish()
        {
            panelContent.BackColor = UiTheme.Background;
            panel1.BackColor = UiTheme.PrimaryDark;
            ApplyHomeLogoutAppearance();

            if (alertStrip != null)
            {
                alertStrip.BackColor = UiTheme.Background;
            }

            label1.Text = Present("الرئيسية");
            label1.AutoSize = false;
            label1.TextAlign = presentationLeftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
            label1.Bounds = new Rectangle(300, 8, Math.Max(520, panel1.Width - 600), 42);
            label1.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            label1.ForeColor = Color.White;

            label2.Text = Present("مساحة العمل اليومية");
            label2.AutoSize = false;
            label2.TextAlign = presentationLeftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
            label2.Bounds = new Rectangle(300, 50, Math.Max(520, panel1.Width - 600), 28);
            label2.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            label2.ForeColor = Color.FromArgb(220, 240, 235);
            LayoutHomeHeaderActions();

            ConfigureHomeTile("DashBoard", label7, "لوحة التحكم");
            ConfigureHomeTile("pictureBox6", label9, "المشتريات");
            ConfigureHomeTile("pictureBox4", label6, "المبيعات");
            ConfigureHomeTile("butSetings", label8, "الإعدادات");
            ConfigureHomeTile("pictureBox2", label4, "الموظفون");
            ConfigureHomeTile("pictureBox1", label3, "الأدوية");
            ConfigureHomeTile("pictureBox3", label5, "العملاء والموردون");
            BuildAccountingTile();
            BuildInventoryTile();

            homeModuleCardsReady = true;
            RestoreHomeModuleAppearance();
            ApplyHomeLogoutAppearance();
            LayoutHomeTiles();
            ConfigureHomeTileAccessRules();
            ApplyHomeAccessRules();
        }

        private sealed class TileLayoutItem
        {
            public TileLayoutItem(string id, AntdUI.Button icon, Label label)
            {
                Id = id;
                Icon = icon;
                Label = label;
            }

            public string Id { get; private set; }

            public AntdUI.Button Icon { get; private set; }

            public Label Label { get; private set; }
        }

        private sealed class HomeTileAccess
        {
            public HomeTileAccess(Control icon, Label label, AppPermission permission, string title)
            {
                Icon = icon;
                Label = label;
                Permission = permission;
                Title = title;
            }

            public Control Icon { get; private set; }

            public Label Label { get; private set; }

            public AppPermission Permission { get; private set; }

            public string Title { get; private set; }
        }

        private sealed class HomeTileCommand
        {
            public HomeTileCommand(AppPermission permission, string title, Action openAction)
            {
                Permission = permission;
                Title = title;
                OpenAction = openAction;
            }

            public AppPermission Permission { get; private set; }

            public string Title { get; private set; }

            public Action OpenAction { get; private set; }
        }

        private void BuildAccountingTile()
        {
            if (accountingTile == null)
            {
                accountingTile = new AccessibleAntdButton
                {
                    Name = "AccountingTile",
                    Size = new Size(300, 160),
                    Location = new Point(578, 555),
                    Cursor = Cursors.Hand
                };
                accountingTile.Click += HomeTileIcon_Click;
                panelContent.Controls.Add(accountingTile);
            }

            if (accountingLabel == null)
            {
                accountingLabel = new Label
                {
                    Name = "AccountingLabel",
                    BackColor = Color.Transparent,
                    Cursor = Cursors.Hand
                };
                panelContent.Controls.Add(accountingLabel);
            }

            ConfigureHomeTile("AccountingTile", accountingLabel, "المحاسبة");

        }

        private void BuildInventoryTile()
        {
            if (inventoryTile == null)
            {
                inventoryTile = new AccessibleAntdButton
                {
                    Name = "InventoryTile",
                    Size = new Size(300, 160),
                    Location = new Point(228, 555),
                    Cursor = Cursors.Hand
                };
                inventoryTile.Click += HomeTileIcon_Click;
                panelContent.Controls.Add(inventoryTile);
            }

            if (inventoryLabel == null)
            {
                inventoryLabel = new Label
                {
                    Name = "InventoryLabel",
                    BackColor = Color.Transparent,
                    Cursor = Cursors.Hand
                };
                panelContent.Controls.Add(inventoryLabel);
            }

            ConfigureHomeTile("InventoryTile", inventoryLabel, "المخزون");

        }

        internal void RestoreHomePresentation()
        {
            if (homeModuleCardsReady && !IsDisposed && !Disposing)
                ApplyPresentation(presentationLeftToRight);
        }

        internal void RestoreHomeModuleAppearance()
        {
            if (!homeModuleCardsReady || IsDisposed || Disposing) return;
            foreach (var button in new[] { DashBoard, pictureBox6, pictureBox4, butSetings, pictureBox2, pictureBox1, pictureBox3, inventoryTile, accountingTile })
            {
                if (button == null) continue;
                DesktopComponents.StyleNavigationIconCard(button, GetHomeModuleIcon(button.Name), presentationLeftToRight);
                if (!button.TabStop) button.ForeColor = button.ForeHover = UiTheme.MutedText;
            }
            foreach (var item in homeTileAccessItems)
                item.Label.ForeColor = item.Icon.TabStop ? UiTheme.Text : UiTheme.MutedText;
        }

        private static AppIcon GetHomeModuleIcon(string name)
        {
            switch (name)
            {
                case "DashBoard": return AppIcon.Dashboard;
                case "pictureBox6": return AppIcon.Report;
                case "pictureBox4": return AppIcon.Sale;
                case "butSetings": return AppIcon.Settings;
                case "pictureBox2": return AppIcon.User;
                case "pictureBox1": return AppIcon.Medicine;
                case "pictureBox3": return AppIcon.User;
                case "AccountingTile": return AppIcon.Currency;
                case "InventoryTile": return AppIcon.Database;
                default: throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown home module icon");
            }
        }

        private void ApplyHomeLogoutAppearance()
        {
            if (pictureBox5 == null || pictureBox5.IsDisposed) return;
            ApplyHeaderActionStyle(pictureBox5);
            pictureBox5.Text = string.Empty;
            pictureBox5.IconSvg = OutlinedIcons.Svg(AppIcon.Logout);
            pictureBox5.IconSize = new Size(24, 24);
            pictureBox5.RightToLeft = presentationLeftToRight ? RightToLeft.No : RightToLeft.Yes;
            pictureBox5.AccessibleName = Present("تسجيل الخروج");
            pictureBox5.AccessibleDescription = pictureBox5.AccessibleName;
            pictureBox5.AccessibleRole = AccessibleRole.PushButton;
            pictureBox5.TabStop = true;
            homeToolTip.SetToolTip(pictureBox5, pictureBox5.AccessibleName);
        }

        private void ConfigureHomeTile(string sourceName, Label titleLabel, string title)
        {
            Control[] sourceControls = panelContent.Controls.Find(sourceName, false);
            ConfigureHomeTileLabel(titleLabel, sourceControls.Length > 0 ? sourceControls[0] : null, title);
        }

        private void ConfigureHomeTileAccessRules()
        {
            homeTileAccessItems.Clear();
            BindHomeTile(DashBoard, label7, AppPermission.ViewDashboard, () => OpenHomeModule(AppPermission.ViewDashboard, () => new Dashboard(ScreenModeSettings.LoadOpeningPreference())));
            BindHomeTile(pictureBox6, label9, AppPermission.Purchases, () => OpenHomeModule(AppPermission.Purchases, () => new Purchases()));
            BindHomeTile(pictureBox4, label6, AppPermission.Sales, () => OpenHomeModule(AppPermission.Sales, () => new Billing()));
            BindHomeTile(butSetings, label8, AppPermission.Settings, OpenSettingsDialog);
            BindHomeTile(pictureBox2, label4, AppPermission.ManageEmployees, () => OpenHomeModule(AppPermission.ManageEmployees, () => new Agent()));
            BindHomeTile(pictureBox1, label3, AppPermission.ManageMedicines, () => OpenHomeModule(AppPermission.ManageMedicines, () => new Medicine()));
            BindHomeTile(pictureBox3, label5, AppPermission.ManageManufacturers, () => OpenHomeModule(AppPermission.ManageManufacturers, () => new CustomerSupplierManagementForm()));
            BindHomeTile(inventoryTile, inventoryLabel, AppPermission.Inventory, () => OpenHomeModule(AppPermission.Inventory, () => new InventoryHomeForm()));
            BindHomeTile(accountingTile, accountingLabel, AppPermission.Accounting, () => OpenHomeModule(AppPermission.Accounting, () => new AccountingHomeForm()));
        }

        private void BindHomeTile(Control icon, Label label, AppPermission permission, Action openAction)
        {
            if (icon == null || label == null)
            {
                return;
            }

            string title = label.Text;
            HomeTileCommand command = new HomeTileCommand(permission, title, openAction);
            icon.Tag = command;
            label.Tag = command;
            icon.AccessibleName = title;
            icon.AccessibleDescription = presentationLeftToRight ? "Open " + title : "فتح " + title;
            icon.AccessibleRole = AccessibleRole.PushButton;
            icon.TabStop = true;
            // AccessibleAntdButton owns Enter/Space activation; no second key handler.
            icon.GotFocus -= HomeModuleFocusChanged; icon.GotFocus += HomeModuleFocusChanged;
            icon.LostFocus -= HomeModuleFocusChanged; icon.LostFocus += HomeModuleFocusChanged;

            label.AccessibleName = title;
            label.AccessibleDescription = presentationLeftToRight ? "Open " + title : "فتح " + title;
            label.Click -= HomeTileLabel_Click;
            label.Click += HomeTileLabel_Click;

            homeTileAccessItems.Add(new HomeTileAccess(icon, label, permission, title));
        }

        private void ApplyHomeAccessRules()
        {
            if (homeTileAccessItems.Count == 0)
            {
                return;
            }

            foreach (HomeTileAccess item in homeTileAccessItems)
            {
                bool allowed = PermissionService.CanOpen(item.Permission);
                ApplyHomeTileAccessState(item.Icon, item.Label, item.Title, allowed);
            }
        }

        private void ApplyHomeTileAccessState(Control icon, Label label, string title, bool allowed)
        {
            string tip = presentationLeftToRight
                ? (allowed ? "Open " + title : "You do not have permission to open " + title)
                : (allowed ? "فتح " + title : "لا تملك صلاحية فتح " + title);

            icon.Enabled = true;
            icon.Cursor = allowed ? Cursors.Hand : Cursors.No;
            icon.TabStop = allowed;
            var module = icon as AntdUI.Button;
            if (module != null) module.ForeColor = module.ForeHover = allowed ? UiTheme.Primary : UiTheme.MutedText;
            icon.AccessibleDescription = tip;
            homeToolTip.SetToolTip(icon, tip);

            label.Enabled = true;
            label.Cursor = allowed ? Cursors.Hand : Cursors.No;
            label.ForeColor = allowed ? UiTheme.Text : Color.FromArgb(140, 150, 148);
            label.AccessibleDescription = tip;
            homeToolTip.SetToolTip(label, tip);


        }

        private void HomeModuleFocusChanged(object sender, EventArgs e)
        {
            var button = sender as AntdUI.Button;
            if (button != null) button.DefaultBorderColor = button.Focused ? UiTheme.Primary : UiTheme.Border;
        }

        private void HomeTileLabel_Click(object sender, EventArgs e)
        {
            ExecuteHomeTileCommand(sender as Control);
        }

        private void HomeTileIcon_Click(object sender, EventArgs e)
        {
            ExecuteHomeTileCommand(sender as Control);
        }

        private void ExecuteHomeTileCommand(Control source)
        {
            HomeTileCommand command = source == null ? null : source.Tag as HomeTileCommand;
            if (command == null)
            {
                return;
            }

            if (!PermissionService.CanOpen(command.Permission))
            {
                ShowAccessDenied(command);
                return;
            }

            if (command.OpenAction != null)
            {
                command.OpenAction();
            }
        }

        private void OpenHomeModule(AppPermission permission, Func<Form> createForm)
        {
            string title = PermissionService.GetArabicName(permission);
            if (!PermissionService.CanOpen(permission))
            {
                ShowAccessDenied(new HomeTileCommand(permission, title, null));
                return;
            }

            ShowChild(createForm());
        }

        private void OpenSettingsDialog()
        {
            if (!PermissionService.CanOpen(AppPermission.Settings))
            {
                ShowAccessDenied(new HomeTileCommand(
                    AppPermission.Settings,
                    PermissionService.GetArabicName(AppPermission.Settings),
                    null));
                return;
            }

            using (SettingsForm settings = new SettingsForm())
            {
                ScreenModeSettings.ApplyOpeningPreference(settings, this);
                settings.ShowDialog(this);
            }
        }

        private void ShowAccessDenied(HomeTileCommand command)
        {
            AppNotifications.Show(
                "لا تملك صلاحية فتح " + command.Title + "." + Environment.NewLine +
                "الصلاحية الحالية: " + SessionContext.CurrentRole,
                "الصلاحيات",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            HomeWorkspaceService.RecordSecurityAudit("AccessDenied", "Permissions", "Denied module: " + command.Permission);
        }

        private void ConfigureHomeTileLabel(Label label, Control tile, string title)
        {
            if (label == null)
            {
                return;
            }

            label.Text = Present(title);
            label.AutoSize = false;
            label.TextAlign = ContentAlignment.MiddleCenter;
            label.Font = AppFonts.Create(12.5F, FontStyle.Bold);
            label.ForeColor = UiTheme.Text;
            label.BackColor = Color.Transparent;
            label.Cursor = Cursors.Hand;
            label.AccessibleRole = AccessibleRole.PushButton;
            label.AccessibleName = label.Text;
            homeToolTip.SetToolTip(label, presentationLeftToRight ? "Open " + label.Text : "فتح " + label.Text);

            if (tile == null)
            {
                return;
            }

            label.Width = tile.Width + 24;
            label.Height = Math.Max(40, TextRenderer.MeasureText(label.Text, label.Font).Height + 12);
            label.Left = tile.Left - 12;
            label.Top = tile.Bottom + 10;
        }

        private void OpenSystemMonitor()
        {
            if (!PermissionService.CanOpen(AppPermission.SystemMonitor))
            {
                ShowAccessDenied(new HomeTileCommand(
                    AppPermission.SystemMonitor,
                    PermissionService.GetArabicName(AppPermission.SystemMonitor),
                    null));
                return;
            }

            using (var monitor = new SystemMonitorForm())
            {
                monitor.ShowDialog(this);
            }
            RefreshAlertStrip();
        }



        // يفتح شاشة الأدوية.
        private void pictureBox1_Click(object sender, EventArgs e)
        {
            ExecuteHomeTileCommand(pictureBox1);
        }

        // يفتح شاشة الشركات المصنعة.
        private void pictureBox3_Click(object sender, EventArgs e)
        {
            ExecuteHomeTileCommand(pictureBox3);
        }

        // يفتح شاشة الموظفين.
        private void pictureBox2_Click(object sender, EventArgs e)
        {
            ExecuteHomeTileCommand(pictureBox2);
        }

        // يفتح شاشة الفواتير.
        private void pictureBox4_Click(object sender, EventArgs e)
        {
            ExecuteHomeTileCommand(pictureBox4);
        }

        // يفتح شاشة المشتريات.
        private void pictureBox6_Click(object sender, EventArgs e)
        {
            ExecuteHomeTileCommand(pictureBox6);
        }

        // يسجل خروج المستخدم ويعود إلى شاشة الدخول.
        private void pictureBox5_Click(object sender, EventArgs e)
        {
            HomeWorkspaceService.RecordSecurityAudit("Logout", "Security", "User signed out.");
            Close();
        }

        // يفتح شاشة لوحة البيانات.
        private void DashBoard_Click(object sender, EventArgs e)
        {
            ExecuteHomeTileCommand(DashBoard);
        }

        // يعرض شاشة فرعية ثم يعيد إظهار الشاشة الرئيسية بعد إغلاقها.
        private void ShowChild(Form childForm)
        {
            Hide();
            try
            {
                using (childForm)
                {
                    ScreenModeSettings.ApplyOpeningPreference(childForm, this);
                    childForm.ShowDialog(this);
                }

                if (!IsDisposed)
                {
                    RefreshAlertStrip();
                    RefreshGettingStartedPanelAsync();
                }
            }
            finally
            {
                // A failed child must not leave the running application hidden.
                if (!IsDisposed && !Disposing) Show();
            }
        }

        private void butSetings_Click(object sender, EventArgs e)
        {
            ExecuteHomeTileCommand(butSetings);
        }

        private void panelContent_Paint(object sender, PaintEventArgs e)
        {
            Rectangle bounds = panelContent.ClientRectangle;
            using (LinearGradientBrush brush = new LinearGradientBrush(
                bounds,
                UiTheme.Background,
                UiTheme.Surface,
                LinearGradientMode.Vertical))
            {
                e.Graphics.FillRectangle(brush, bounds);
            }
        }
    }
}



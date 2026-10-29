using System;
using System.Data;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace project_fo_3
{
    public partial class SystemMonitorForm : Form
    {
        private bool isRefreshing;
        private readonly System.Windows.Forms.Timer contextWatch = new System.Windows.Forms.Timer { Interval = 1000 };
        private readonly Func<Func<bool>> profileContextFactory;
        private Func<bool> profileContext;
        private ISystemMonitorReadSession readSession;
        private CancellationTokenSource accessWatchCancellation;
        private readonly Func<ISystemMonitorReadSession> sessionFactory;
        private CancellationTokenSource refreshCancellation;
        private long refreshGeneration;
        private SystemMonitorReadService.Snapshot displayedSnapshot;
        private readonly bool previewMode;
        private bool leftToRight;
        private string summaryStatusArabic;
        private string auditStatusArabic;
        private string inventoryStatusArabic;
        private int? summaryStatusCount;
        private int? auditStatusCount;
        private int? inventoryStatusCount;
        private readonly FlowLayoutPanel navigation = new FlowLayoutPanel();
        private readonly Panel pageViewport = new Panel();
        private readonly AccessibleAntdButton[] navigationButtons = new AccessibleAntdButton[3];

        public SystemMonitorForm() : this(false) { }

        internal SystemMonitorForm(bool previewMode) : this(previewMode, ApplicationServices.CreateSystemMonitorReadSession) { }
        internal SystemMonitorForm(bool previewMode, Func<ISystemMonitorReadSession> sessionFactory)
            : this(previewMode, sessionFactory, ApplicationServices.CaptureRuntimeConnectionContext) { }

        internal SystemMonitorForm(bool previewMode, Func<ISystemMonitorReadSession> sessionFactory,
            Func<Func<bool>> profileContextFactory)
        {
            this.previewMode = previewMode;
            this.profileContextFactory = profileContextFactory ?? throw new ArgumentNullException(nameof(profileContextFactory));
            this.sessionFactory = sessionFactory ?? throw new ArgumentNullException(nameof(sessionFactory));
            leftToRight = UiDirection.IsLeftToRight;
            InitializeComponent();
            UiTheme.Apply(this);
            if (previewMode) UiTheme.RemoveStatusStrip(this);
            Text = "مراقبة النظام";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(760, 540);
            Size = new Size(1080, 680);
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = true;
            BackColor = UiTheme.Background;
            Font = AppFonts.Create(10F);
            UiDirection.Apply(this);

            ConfigureGrid(alertsGrid);
            ConfigureGrid(auditGrid);
            ConfigureGrid(inventoryGrid);
            ConfigureStatusLabel(summaryLabel);
            ConfigureStatusLabel(auditLabel);
            ConfigureStatusLabel(inventoryLabel);
            InstallDataCard(alertsPage, alertsGrid, summaryLabel, "monitorAlertsCard");
            InstallDataCard(auditPage, auditGrid, auditLabel, "monitorAuditCard");
            InstallDataCard(inventoryPage, inventoryGrid, inventoryLabel, "monitorInventoryCard");
            BuildNavigation();

            refreshButton.AccessibleName = "تحديث بيانات مراقبة النظام";
            closeButton.AccessibleName = "إغلاق مراقبة النظام";
            tabs.AccessibleName = "تبويبات مراقبة النظام";
            tabs.AccessibleDescription = "تنبيهات المخزون وسجل التدقيق وحركات المخزون.";

            refreshButton.Click += (sender, args) => RefreshData();
            closeButton.Click += (sender, args) => Close();
            BuildFooter();
            header.Resize += (sender, args) => LayoutHeaderText();
            LayoutHeaderText();
            ApplyPresentation(leftToRight);
            if (!previewMode)
            {
                Load += (sender, args) => { contextWatch.Start(); RefreshData(); };
                contextWatch.Tick += async (sender, args) => { CheckReadContext(); await CheckReadAccessAsync(); };
                Activated += (sender, args) => CheckReadContext();
                PermissionService.CachedOverridesCleared += OnPermissionContextCleared;
                UiSessionSubscription.Attach(this, () =>
                {
                    InvalidateRead();
                    SetReadStatus(leftToRight ? "Session changed. Refresh to load current data." :
                        "تغيرت الجلسة. حدّث لتحميل البيانات الحالية.");
                });
            }
            else
            {
                SetSummary("معاينة مراقبة النظام دون اتصال بقاعدة البيانات.");
                SetAudit("لا تُقرأ سجلات التدقيق في المعاينة.");
                SetInventory("لا تُقرأ حركات المخزون في المعاينة.");
            }
        }

        private void OnPermissionContextCleared(object sender, EventArgs args)
        {
            if (IsDisposed || Disposing || !IsHandleCreated) return;
            if (InvokeRequired)
            {
                try { BeginInvoke(new Action(() => OnPermissionContextCleared(sender, args))); }
                catch (InvalidOperationException) when (IsDisposed || Disposing || !IsHandleCreated) { }
                return;
            }
            InvalidateRead();
            SetReadStatus(leftToRight ? "Permissions changed. Refresh current data." :
                "تغيرت الصلاحيات. حدّث البيانات الحالية.");
        }

        internal void CheckReadContext()
        {
            if (previewMode || IsDisposed || Disposing || profileContext == null) return;
            bool current;
            try { current = profileContext(); }
            catch { current = false; }
            if (current) return;
            InvalidateRead();
            SetReadStatus(leftToRight ? "Connection changed. Refresh current data." :
                "تغير الاتصال. حدّث البيانات الحالية.");
        }

        internal async Task CheckReadAccessAsync()
        {
            if (previewMode || IsDisposed || Disposing || displayedSnapshot == null ||
                readSession == null || accessWatchCancellation != null) return;
            long generation = refreshGeneration;
            var session = readSession;
            var cancellation = new CancellationTokenSource();
            cancellation.CancelAfter(2000);
            accessWatchCancellation = cancellation;
            try
            {
                await session.EnsureAccessAsync(cancellation.Token);
            }
            catch (Exception)
            {
                if (!IsDisposed && !Disposing && generation == refreshGeneration && ReferenceEquals(session, readSession))
                {
                    InvalidateRead();
                    SetReadStatus(leftToRight ? "Access could not be verified. Refresh after checking permissions and connection." :
                        "تعذر التحقق من الصلاحية. حدّث بعد مراجعة الصلاحيات والاتصال.");
                }
            }
            finally
            {
                if (ReferenceEquals(accessWatchCancellation, cancellation)) accessWatchCancellation = null;
                cancellation.Dispose();
            }
        }
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            InvalidateRead();
            base.OnFormClosed(e);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                accessWatchCancellation?.Cancel();
                accessWatchCancellation = null;
                readSession = null;
                contextWatch.Stop();
                contextWatch.Dispose();
                PermissionService.CachedOverridesCleared -= OnPermissionContextCleared;
                refreshGeneration++;
                refreshCancellation?.Cancel();
                refreshCancellation = null;
                displayedSnapshot?.Dispose();
                displayedSnapshot = null;
            }
            base.Dispose(disposing);
        }

        private string Present(string arabicText) => leftToRight ? UiLanguageCatalog.Translate(arabicText) : arabicText;

        internal void ApplyPresentation(bool useLeftToRight)
        {
            leftToRight = useLeftToRight;
            UiDirection.Apply(this, leftToRight);
            tabs.RightToLeftLayout = !leftToRight;
            Text = titleLabel.Text = Present("مراقبة النظام");
            subtitleLabel.Text = Present("تنبيهات المخزون وسجل التدقيق وحركات المخزون");
            alertsPage.Text = Present("تنبيهات المخزون");
            auditPage.Text = Present("سجل التدقيق");
            inventoryPage.Text = Present("حركات المخزون");
            refreshButton.Text = Present("تحديث");
            closeButton.Text = Present("إغلاق");
            refreshButton.AccessibleName = Present("تحديث بيانات مراقبة النظام");
            closeButton.AccessibleName = Present("إغلاق مراقبة النظام");
            tabs.AccessibleName = Present("تبويبات مراقبة النظام");
            tabs.AccessibleDescription = subtitleLabel.Text;
            UiTheme.ApplyStatusStripPresentation(this, leftToRight);
            LayoutHeaderText();
            LayoutFooterActions();
            LayoutNavigation();
            LocalizeAlertsGrid();
            LocalizeAuditGrid();
            LocalizeInventoryGrid();
            if (summaryStatusArabic != null) summaryLabel.Text = FormatStatus(summaryStatusArabic, summaryStatusCount);
            if (auditStatusArabic != null) auditLabel.Text = FormatStatus(auditStatusArabic, auditStatusCount);
            if (inventoryStatusArabic != null) inventoryLabel.Text = FormatStatus(inventoryStatusArabic, inventoryStatusCount);
        }

        private string FormatStatus(string key, int? count) => count.HasValue
            ? string.Format(Present(key), count.Value) : Present(key);
        private void SetSummary(string key, int? count = null)
        { summaryStatusArabic = key; summaryStatusCount = count; summaryLabel.Text = FormatStatus(key, count); }
        private void SetAudit(string key, int? count = null)
        { auditStatusArabic = key; auditStatusCount = count; auditLabel.Text = FormatStatus(key, count); }
        private void SetInventory(string key, int? count = null)
        { inventoryStatusArabic = key; inventoryStatusCount = count; inventoryLabel.Text = FormatStatus(key, count); }

        private void MarkRefreshFailed()
        {
            SetSummary("تعذر تحديث التنبيهات؛ قد تكون البيانات المعروضة قديمة.");
            SetAudit("تعذر تحديث سجل التدقيق؛ قد تكون البيانات المعروضة قديمة.");
            SetInventory("تعذر تحديث حركات المخزون؛ قد تكون البيانات المعروضة قديمة.");
        }

        private void BuildNavigation()
        {
            var workspace = new Panel { Name = "monitorWorkspace", Dock = DockStyle.Fill, TabIndex = 1 };
            navigation.Name = "monitorNavigation";
            navigation.Dock = DockStyle.Top;
            navigation.TabIndex = 0;
            navigation.WrapContents = true;
            pageViewport.Name = "monitorPageViewport";
            pageViewport.Dock = DockStyle.Fill;
            pageViewport.TabIndex = 1;
            Controls.Remove(tabs);
            tabs.Dock = DockStyle.None;
            tabs.TabStop = false;
            pageViewport.Controls.Add(tabs);
            workspace.Controls.Add(pageViewport);
            workspace.Controls.Add(navigation);
            Controls.Add(workspace);
            Controls.SetChildIndex(workspace, 0);
            for (int index = 0; index < navigationButtons.Length; index++)
            {
                int target = index;
                var button = new AccessibleAntdButton { Name = "monitorPageTab" + index, TabIndex = index,
                    AccessibleRole = AccessibleRole.PageTab, Font = AppFonts.Create(10.5F, FontStyle.Bold),
                    BorderWidth = 1, Cursor = Cursors.Hand, TextAlign = ContentAlignment.MiddleCenter };
                button.Click += (sender, args) => tabs.SelectedIndex = target;
                navigationButtons[index] = button;
                navigation.Controls.Add(button);
            }
            tabs.SelectedIndexChanged += (sender, args) => LayoutNavigation();
            workspace.Resize += (sender, args) => LayoutNavigation();
            pageViewport.Resize += (sender, args) => LayoutPageViewport();
            LayoutNavigation();
        }

        private void LayoutNavigation()
        {
            if (navigationButtons[0] == null) return;
            int gap = (int)Math.Round(8F * DeviceDpi / 96F);
            int height = (int)Math.Round(44F * DeviceDpi / 96F);
            navigation.SuspendLayout();
            navigation.RightToLeft = RightToLeft.No;
            navigation.FlowDirection = leftToRight ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;
            navigation.Padding = new Padding(gap * 2, gap / 2, gap * 2, gap / 2);
            navigation.BackColor = UiTheme.Background;
            navigation.AccessibleName = tabs.AccessibleName;
            for (int index = 0; index < navigationButtons.Length; index++)
            {
                var button = navigationButtons[index];
                bool selected = tabs.SelectedIndex == index;
                button.Text = tabs.TabPages[index].Text;
                button.AccessibleName = button.Text;
                button.AccessibleDescription = selected ? (leftToRight ? "Current tab" : "التبويب الحالي") : "";
                button.RightToLeft = leftToRight ? RightToLeft.No : RightToLeft.Yes;
                button.Width = TextRenderer.MeasureText(button.Text, button.Font).Width + gap * 4;
                button.Height = height;
                button.Margin = new Padding(gap / 2);
                button.Radius = DesktopDesignTokens.ActionRadius(height);
                button.DefaultBack = selected ? UiTheme.Selection : UiTheme.Surface;
                button.BackColor = button.DefaultBack.Value;
                button.ForeColor = UiTheme.Text;
                button.DefaultBorderColor = selected ? UiTheme.Primary : UiTheme.Border;
                button.BackHover = UiTheme.Selection;
                button.ForeHover = UiTheme.Text;
            }
            navigation.ResumeLayout(true);
            navigation.Height = navigation.GetPreferredSize(new Size(navigation.ClientSize.Width, 0)).Height;
            LayoutPageViewport();
        }

        private void LayoutPageViewport()
        {
            if (pageViewport.ClientSize.Width < 1 || pageViewport.ClientSize.Height < 1) return;
            // Keep the native page/focus engine, with its entire chrome clipped by
            // the viewport. Navigation is drawn by shared accessible AntdUI buttons.
            pageViewport.RightToLeft = RightToLeft.No;
            tabs.SetBounds(0, 0, pageViewport.ClientSize.Width, pageViewport.ClientSize.Height);
            Rectangle content = tabs.DisplayRectangle;
            int bottom = tabs.ClientSize.Height - content.Bottom;
            tabs.SetBounds(-content.Left, -content.Top,
                pageViewport.ClientSize.Width + content.Left * 2,
                pageViewport.ClientSize.Height + content.Top + bottom);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.Tab) || keyData == (Keys.Control | Keys.Shift | Keys.Tab))
            {
                int delta = (keyData & Keys.Shift) != 0 ? -1 : 1;
                tabs.SelectedIndex = (tabs.SelectedIndex + delta + tabs.TabCount) % tabs.TabCount;
                navigationButtons[tabs.SelectedIndex].Focus();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private static void InstallDataCard(TabPage page, DataGridView grid, Label status, string name)
        {
            page.Padding = new Padding((int)Math.Round(4F * page.DeviceDpi / 96F));
            var card = DesktopComponents.Card(name);
            card.Dock = DockStyle.Fill;
            card.Padding = new Padding((int)Math.Round(12F * page.DeviceDpi / 96F));
            page.Controls.Remove(grid);
            page.Controls.Remove(status);
            card.Controls.Add(grid);
            card.Controls.Add(status);
            page.Controls.Add(card);
            AntdThemeAdapter.Apply(card);
            Action layoutStatus = () =>
            {
                int width = Math.Max(1, card.ClientSize.Width - card.Padding.Horizontal - status.Padding.Horizontal);
                status.Height = TextRenderer.MeasureText(status.Text, status.Font, new Size(width, int.MaxValue),
                    TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height + status.Padding.Vertical;
                status.AccessibleName = status.Text;
            };
            card.Resize += (sender, args) => layoutStatus();
            status.TextChanged += (sender, args) => layoutStatus();
            layoutStatus();
        }

        private void BuildFooter()
        {
            Control statusStrip = Controls["__UiStatusStrip"];
            if (statusStrip != null) Controls.SetChildIndex(statusStrip, Controls.Count - 1);
            footer.BackColor = UiTheme.Background;
            header.BackColor = UiTheme.Background;
            header.Height = 100;
            titleLabel.BackColor = UiTheme.Background;
            titleLabel.ForeColor = UiTheme.Text;
            subtitleLabel.BackColor = UiTheme.Background;
            subtitleLabel.ForeColor = UiTheme.MutedText;
            UiDirection.Apply(footer);
            footer.Resize += (sender, args) => LayoutFooterActions();
            LayoutFooterActions();
        }

        private void LayoutFooterActions()
        {
            const int margin = 24;
            const int gap = 12;
            int x = leftToRight ? margin : footer.ClientSize.Width - margin - refreshButton.Width;
            refreshButton.SetBounds(x, 10, refreshButton.Width, 44);
            x += leftToRight ? refreshButton.Width + gap : -closeButton.Width - gap;
            closeButton.SetBounds(x, 10, closeButton.Width, 44);
            DesktopComponents.StyleOutlinedFooterAction(refreshButton);
            DesktopComponents.StyleOutlinedFooterAction(closeButton);
        }

        private void LayoutHeaderText()
        {
            int width = Math.Max(120, header.ClientSize.Width - 48);
            int titleHeight = TextRenderer.MeasureText(titleLabel.Text, titleLabel.Font, new Size(width, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height + 4;
            int subtitleHeight = TextRenderer.MeasureText(subtitleLabel.Text, subtitleLabel.Font, new Size(width, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height + 4;
            titleLabel.SetBounds(24, 14, width, titleHeight);
            subtitleLabel.SetBounds(24, titleLabel.Bottom + 4, width, subtitleHeight);
            header.Height = subtitleLabel.Bottom + 14;
            titleLabel.TextAlign = leftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
            subtitleLabel.TextAlign = titleLabel.TextAlign;
        }
        private static void ConfigureGrid(DataGridView grid)
        {
            grid.Tag = UiTheme.PreserveLocalizedGridDirection;
            UiTheme.ApplyDataGridQuality(grid, "لا توجد بيانات للمراجعة حالياً");
            grid.Dock = DockStyle.Fill;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.ReadOnly = true;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.BackgroundColor = UiTheme.Surface;
            grid.BorderStyle = BorderStyle.None;
            grid.RowHeadersVisible = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersDefaultCellStyle.BackColor = UiTheme.SurfaceVariant;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = UiTheme.Text;
            grid.ColumnHeadersDefaultCellStyle.Font = AppFonts.Create(10F, FontStyle.Bold);
            grid.DefaultCellStyle.SelectionBackColor = UiTheme.Selection;
            grid.DefaultCellStyle.SelectionForeColor = UiTheme.Text;
            grid.AlternatingRowsDefaultCellStyle.BackColor = UiTheme.AlternateRow;
        }

        private static void ConfigureStatusLabel(Label label)
        {
            label.Font = AppFonts.Create(10.5F, FontStyle.Bold);
            label.ForeColor = UiTheme.Text;
            label.BackColor = Color.Transparent;
            label.Padding = new Padding(8, 4, 8, 4);
            label.TextAlign = ContentAlignment.MiddleRight;
        }

        private void ClearRows()
        {
            alertsGrid.DataSource = null;
            auditGrid.DataSource = null;
            inventoryGrid.DataSource = null;
            displayedSnapshot?.Dispose();
            displayedSnapshot = null;
        }
        private void SetReadStatus(string message)
        {
            SetSummary(message); SetAudit(message); SetInventory(message);
        }
        private void InvalidateRead()
        {
            profileContext = null;
            readSession = null;
            accessWatchCancellation?.Cancel();
            accessWatchCancellation = null;
            refreshGeneration++;
            refreshCancellation?.Cancel();
            refreshCancellation = null;
            isRefreshing = false;
            ClearRows();
            refreshButton.Enabled = true;
            UseWaitCursor = false;
        }
        private async void RefreshData() { await RefreshDataAsync(); }
        internal async Task RefreshDataAsync()
        {
            if (previewMode)
            {
                SetSummary("المعاينة للعرض فقط؛ لم تُقرأ بيانات من قاعدة البيانات.");
                return;
            }
            if (isRefreshing || IsDisposed || Disposing) return;
            long generation = ++refreshGeneration;
            accessWatchCancellation?.Cancel();
            accessWatchCancellation = null;
            readSession = null;
            var cancellation = new CancellationTokenSource();
            refreshCancellation = cancellation;
            isRefreshing = true;
            ClearRows();
            SetReadStatus(leftToRight ? "Loading current data..." : "جارٍ تحميل البيانات الحالية...");
            refreshButton.Enabled = false;
            UseWaitCursor = true;
            SystemMonitorReadService.Snapshot data = null;
            try
            {
                profileContext = profileContextFactory();
                var session = sessionFactory();
                readSession = session;
                session.EnsureContext();
                data = await session.LoadAsync(cancellation.Token);
                if (IsDisposed || Disposing || generation != refreshGeneration) return;
                cancellation.Token.ThrowIfCancellationRequested();
                session.EnsureContext();
                if (data == null || data.Alerts == null || data.Audit == null || data.Movements == null)
                    throw new InvalidOperationException("Incomplete monitor snapshot.");
                alertsGrid.DataSource = data.Alerts;
                auditGrid.DataSource = data.Audit;
                inventoryGrid.DataSource = data.Movements;
                LocalizeAlertsGrid(); LocalizeAuditGrid(); LocalizeInventoryGrid();
                if (data.Alerts.Rows.Count == 0) SetSummary("لا توجد تنبيهات مخزون أو انتهاء حالياً.");
                else SetSummary("{0} تنبيه يحتاج إلى مراجعة.", data.Alerts.Rows.Count);
                if (data.Audit.Rows.Count == 0) SetAudit("لا توجد سجلات تدقيق حتى الآن.");
                else SetAudit("عرض آخر {0} سجل تدقيق.", data.Audit.Rows.Count);
                if (data.Movements.Rows.Count == 0) SetInventory("لا توجد حركات مخزون مسجلة حتى الآن.");
                else SetInventory("عرض آخر {0} حركة مخزون.", data.Movements.Rows.Count);
                displayedSnapshot = data;
                data = null;
            }
            catch (OperationCanceledException)
            {
                if (!IsDisposed && !Disposing && generation == refreshGeneration)
                {
                    ClearRows();
                    SetReadStatus(leftToRight ? "Read cancelled. Refresh to try again." : "أُلغيت القراءة. حدّث لإعادة المحاولة.");
                }
            }
            catch (UnauthorizedAccessException)
            {
                if (!IsDisposed && !Disposing && generation == refreshGeneration)
                {
                    ClearRows();
                    SetReadStatus(leftToRight ? "Session or permission changed. Refresh after signing in." :
                        "تغيرت الجلسة أو الصلاحية. حدّث بعد تسجيل الدخول.");
                }
            }
            catch (Exception)
            {
                if (!IsDisposed && !Disposing && generation == refreshGeneration)
                {
                    ClearRows();
                    SetReadStatus(leftToRight ? "Unable to load data. Check the connection and refresh." :
                        "تعذر تحميل البيانات. تحقق من الاتصال ثم حدّث.");
                }
            }
            finally
            {
                data?.Dispose();
                cancellation.Dispose();
                if (generation == refreshGeneration && !IsDisposed && !Disposing)
                {
                    refreshCancellation = null;
                    isRefreshing = false;
                    refreshButton.Enabled = true;
                    UseWaitCursor = false;
                }
            }
        }
        private void LocalizeAlertsGrid()
        {
            alertsGrid.RightToLeft = leftToRight ? RightToLeft.No : RightToLeft.Yes;
            if (alertsGrid.Columns.Contains("Type")) alertsGrid.Columns["Type"].HeaderText = Present("النوع");
            if (alertsGrid.Columns.Contains("Medicine")) alertsGrid.Columns["Medicine"].HeaderText = Present("الصنف");
            if (alertsGrid.Columns.Contains("Quantity")) alertsGrid.Columns["Quantity"].HeaderText = Present("الكمية");
            if (alertsGrid.Columns.Contains("Expiry"))
            {
                alertsGrid.Columns["Expiry"].HeaderText = Present("تاريخ الانتهاء");
                alertsGrid.Columns["Expiry"].DefaultCellStyle.Format = "yyyy-MM-dd";
            }
            if (alertsGrid.Columns.Contains("Company")) alertsGrid.Columns["Company"].HeaderText = Present("الشركة");
            if (alertsGrid.Columns.Contains("Message")) alertsGrid.Columns["Message"].HeaderText = Present("التنبيه");
        }

        private void LocalizeAuditGrid()
        {
            auditGrid.RightToLeft = leftToRight ? RightToLeft.No : RightToLeft.Yes;
            if (auditGrid.Columns.Contains("Time"))
            {
                auditGrid.Columns["Time"].HeaderText = Present("الوقت");
                auditGrid.Columns["Time"].DefaultCellStyle.Format = "yyyy-MM-dd HH:mm";
            }
            if (auditGrid.Columns.Contains("User")) auditGrid.Columns["User"].HeaderText = Present("المستخدم");
            if (auditGrid.Columns.Contains("Role")) auditGrid.Columns["Role"].HeaderText = Present("الصلاحية");
            if (auditGrid.Columns.Contains("Action")) auditGrid.Columns["Action"].HeaderText = Present("الإجراء");
            if (auditGrid.Columns.Contains("Module")) auditGrid.Columns["Module"].HeaderText = Present("الوحدة");
            if (auditGrid.Columns.Contains("Details")) auditGrid.Columns["Details"].HeaderText = Present("التفاصيل");
        }

        private void LocalizeInventoryGrid()
        {
            inventoryGrid.RightToLeft = leftToRight ? RightToLeft.No : RightToLeft.Yes;
            if (inventoryGrid.Columns.Contains("Time"))
            {
                inventoryGrid.Columns["Time"].HeaderText = Present("الوقت");
                inventoryGrid.Columns["Time"].DefaultCellStyle.Format = "yyyy-MM-dd HH:mm";
            }
            if (inventoryGrid.Columns.Contains("Medicine")) inventoryGrid.Columns["Medicine"].HeaderText = Present("الصنف");
            if (inventoryGrid.Columns.Contains("Direction")) inventoryGrid.Columns["Direction"].HeaderText = Present("الاتجاه");
            if (inventoryGrid.Columns.Contains("Delta")) inventoryGrid.Columns["Delta"].HeaderText = Present("التغير");
            if (inventoryGrid.Columns.Contains("Before")) inventoryGrid.Columns["Before"].HeaderText = Present("قبل");
            if (inventoryGrid.Columns.Contains("After")) inventoryGrid.Columns["After"].HeaderText = Present("بعد");
            if (inventoryGrid.Columns.Contains("Source")) inventoryGrid.Columns["Source"].HeaderText = Present("المصدر");
            if (inventoryGrid.Columns.Contains("SourceId")) inventoryGrid.Columns["SourceId"].HeaderText = Present("رقم المصدر");
            if (inventoryGrid.Columns.Contains("UnitCost")) inventoryGrid.Columns["UnitCost"].HeaderText = Present("تكلفة الوحدة");
            if (inventoryGrid.Columns.Contains("UnitPrice")) inventoryGrid.Columns["UnitPrice"].HeaderText = Present("سعر الوحدة");
            if (inventoryGrid.Columns.Contains("User")) inventoryGrid.Columns["User"].HeaderText = Present("المستخدم");
            if (inventoryGrid.Columns.Contains("Notes")) inventoryGrid.Columns["Notes"].HeaderText = Present("ملاحظات");
        }
    }
}








// شرح الملف: شاشة لوحة البيانات.
// تعرض أعداد الشركات والموظفين وإجمالي المبيعات وسجلات العملاء مع مؤشرات أداء متطورة ورسومات بيانية.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Reflection;
using System.Globalization;
using PharmacyManagement.Application;

namespace project_fo_3
{
    // شاشة لوحة البيانات.
    // تعرض مؤشرات سريعة عن الشركات والموظفين والمبيعات وسجلات العملاء ورسوم بيانية ذكية.
    public partial class Dashboard : Form
    {
        // تعتمد الشاشة على عقد قراءة فقط؛ لا تمرر عناصر WinForms أو أوامر SQL إلى طبقة العمليات.
        private readonly IDashboardReadService dashboardReadService;
        private readonly ICurrencyReadService currencyReadService;
        private readonly System.Threading.CancellationTokenSource currencyLoadCancellation = new System.Threading.CancellationTokenSource();
        private string dashboardCurrencyDisplay = string.Empty;
        private decimal? displayedSalesTotal;
        private string selectedCustomerId = "";
        private readonly List<KeyValuePair<string, float>> chartData = new List<KeyValuePair<string, float>>();
        private bool chartLoadFailed;
        private bool isLoadingCashierFilter;
        private readonly List<string> cashierNames = new List<string>();
        private Panel dashboardViewport;
        private bool presentationLeftToRight;


        // ينشئ شاشة لوحة البيانات ويطبق التصميم الموحد.
        public Dashboard()
            : this(ScreenModeSettings.LoadOpeningPreference(), ApplicationServices.CreateDashboardReadSession())
        {
        }

        internal Dashboard(bool openFullScreen)
            : this(openFullScreen, ApplicationServices.CreateDashboardReadSession())
        {
        }

        internal Dashboard(bool openFullScreen, IDashboardReadService dashboardReadService)
            : this(openFullScreen, dashboardReadService, ApplicationServices.CurrencyRead)
        {
        }

        internal Dashboard(bool openFullScreen, DashboardReadSession session)
            : this(openFullScreen, session.Service, session.Currency, () => session.IsCurrent) { InitializeFinancialRead(session.Financial); }

        internal Dashboard(bool openFullScreen, IDashboardReadService dashboardReadService, ICurrencyReadService currencyReadService)
            : this(openFullScreen, dashboardReadService, currencyReadService, () => true) { }

        internal Dashboard(bool openFullScreen, IDashboardReadService dashboardReadService, ICurrencyReadService currencyReadService, Func<bool> current)
        {
            this.dashboardReadService = dashboardReadService ?? throw new ArgumentNullException(nameof(dashboardReadService));
            dashboardContextIsCurrent = current ?? throw new ArgumentNullException(nameof(current));
            dashboardAsyncRead = dashboardReadService as IAsyncDashboardReadService;
            this.currencyReadService = currencyReadService ?? throw new ArgumentNullException(nameof(currencyReadService));
            presentationLeftToRight = UiDirection.IsLeftToRight;
            InitializeComponent();
            // Preserve the designed window minimum when the Arabic theme font changes.
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96F, 96F);
            MinimumSize = new Size(960, 640);
            UiTheme.Apply(this);
            InitializeDashboardViewport();
            cmbCashierFilter.AccessibleName = Present("الكاشير");
            cmbCashierFilter.AccessibleRole = AccessibleRole.ComboBox;
            AntdThemeAdapter.Apply(cmbCashierFilter);
            UiSessionSubscription.Attach(this, () => { if (!DashboardContextCurrent) RejectDashboardContext(); });
            Disposed += (sender, args) => ReleaseDashboardReadResources();
            // Opening mode is an initial window choice. Responsive redraws must
            // preserve later Restore, resize and minimize actions from Windows.
            if (openFullScreen) WindowState = FormWindowState.Maximized;
        }

        private string Present(string arabicText)
        {
            return presentationLeftToRight ? UiLanguageCatalog.Translate(arabicText) : arabicText;
        }

        internal void ApplyPresentation(bool leftToRight)
        {
            presentationLeftToRight = leftToRight;
            RefreshCashierAllOption();
            SetupDashboardLayout(leftToRight);
            FormatDashboardGrid();
            UiTheme.ApplyStatusStripPresentation(this, leftToRight);
            chartPanel?.Invalidate();
            if (financialReadService != null) RenderFinancialFacts();
        }

        private void RefreshCashierAllOption()
        {
            if (cmbCashierFilter == null || cmbCashierFilter.Items.Count == 0) return;
            string desired = Present("جميع الكاشيرين");
            int selectedIndex = cmbCashierFilter.SelectedIndex;
            if (string.Equals(Convert.ToString(cmbCashierFilter.Items[0]), desired, StringComparison.Ordinal) &&
                (selectedIndex != 0 || string.Equals(cmbCashierFilter.Text, desired, StringComparison.Ordinal)))
                return;

            bool previousLoading = isLoadingCashierFilter;
            isLoadingCashierFilter = true;
            try
            {
                cmbCashierFilter.SelectedIndex = -1;
                cmbCashierFilter.Items[0] = desired;
                if (selectedIndex >= 0 && selectedIndex < cmbCashierFilter.Items.Count)
                    cmbCashierFilter.SelectedIndex = selectedIndex;
            }
            finally { isLoadingCashierFilter = previousLoading; }
        }

        private void InitializeDashboardViewport()
        {
            AutoScroll = false;
            dashboardViewport = new Panel
            {
                Name = "dashboardViewport", Dock = DockStyle.None,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = UiTheme.Background, AutoScroll = false
            };
            foreach (Control control in Controls.Cast<Control>().Where(control => control.Name != "__UiStatusStrip").ToArray())
                dashboardViewport.Controls.Add(control);
            Controls.Add(dashboardViewport);
            dashboardViewport.BringToFront();
            Controls["__UiStatusStrip"]?.BringToFront();
            UpdateDashboardViewportBounds();
        }

        private void UpdateDashboardViewportBounds()
        {
            if (dashboardViewport == null) return;
            Control status = Controls["__UiStatusStrip"];
            int statusHeight = status == null ? 0 : status.Height;
            dashboardViewport.SetBounds(0, 0, ClientSize.Width, Math.Max(1, ClientSize.Height - statusHeight));
        }

        // يغلق التطبيق بالكامل.
        private void label8_Click(object sender, EventArgs e)
        {
            Close();
        }

        // يعود للشاشة الرئيسية.
        private void button2_Click(object sender, EventArgs e)
        {
            Close();
        }

        // يحمل جميع مؤشرات اللوحة عند فتح الشاشة.
        private async void Dashboard_Load(object sender, EventArgs e)
        {
            SetupDashboardLayout();CustomizeCustomerGridView();FormatDashboardGrid();
            await ReloadDashboardAsync(true);
        }

        // يضبط مظهر جدول العملاء.
        private void CustomizeCustomerGridView()
        {
            UiTheme.ApplyTo(CustomerGV);
        }

        private bool isLayoutInitialized = false;

        // إعداد وتوزيع عناصر لوحة التحكم ديناميكياً لتشابه الأنظمة البرمجية العالمية
        private bool isApplyingDashboardLayout = false;

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);

            if (!IsHandleCreated || isApplyingDashboardLayout || WindowState == FormWindowState.Minimized)
            {
                return;
            }

            UpdateDashboardViewportBounds();
            SetupDashboardLayout();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (dashboardViewport != null && dashboardViewport.AutoScroll)
                dashboardViewport.AutoScrollPosition = Point.Empty;
        }

        private void SetupDashboardLayout() => SetupDashboardLayout(presentationLeftToRight);

        private void SetupDashboardLayout(bool leftToRight)
        {
            presentationLeftToRight = leftToRight;
            RefreshCashierAllOption();
            if (isApplyingDashboardLayout)
            {
                return;
            }

            isApplyingDashboardLayout = true;
            bool compactDashboardLayout = false;
            dashboardViewport.SuspendLayout();
            try
            {
                UpdateDashboardViewportBounds();
                RightToLeft = leftToRight ? RightToLeft.No : RightToLeft.Yes;
                RightToLeftLayout = !leftToRight;
                Text = Present("لوحة التحكم");
                AccessibleName = Text;
                cmbCashierFilter.AccessibleName = Present("الكاشير");
                BackColor = UiTheme.Background;
                StartPosition = FormStartPosition.Manual;

                FormBorderStyle = FormBorderStyle.Sizable;
                MaximizeBox = true;
                MinimizeBox = true;
                // لا نفرض مساحة سطح مكتب على الشاشات الصغيرة. بدلاً من قص البطاقات
                // والحقول، ننتقل إلى مسار عمودي يمكن الوصول إليه بالتمرير.
                int formWidth = Math.Max(dashboardViewport.ClientSize.Width, 320);
                int formHeight = Math.Max(dashboardViewport.ClientSize.Height, 480);
                bool useCompactLayout = formWidth < 1220 || formHeight < 660; compactDashboardLayout = useCompactLayout;
                if (useCompactLayout)
                    formWidth = Math.Max(320, formWidth - SystemInformation.VerticalScrollBarWidth);
                // Position the children in unscrolled coordinates, then enable scrolling.
                // Keeping AutoScroll active while assigning Bounds accumulates a stale offset
                // after resizing and can open this screen on a large empty area.
                dashboardViewport.AutoScrollPosition = Point.Empty;
                dashboardViewport.AutoScroll = false;
                dashboardViewport.AutoScrollMinSize = Size.Empty;
                int side = 40;
                int gap = 24;

                panel1.Bounds = new Rectangle(0, 0, formWidth, 88);
                panel1.BackColor = UiTheme.PrimaryDark;

                label1.AutoSize = false;
                label1.Text = Present("لوحة التحكم");
                label1.Font = AppFonts.Create(18F, FontStyle.Bold);
                label1.ForeColor = Color.White;
                label1.TextAlign = leftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
                int headerTitleWidth = Math.Max(110, formWidth - 80);
                int headerTitleLeft = 40;
                label1.Bounds = new Rectangle(headerTitleLeft, 14, headerTitleWidth, 34);

                label2.AutoSize = false;
                label2.Text = leftToRight ? "Pharmacy indicators" : "مؤشرات الصيدلية";
                label2.Font = AppFonts.Create(10.5F, FontStyle.Bold);
                label2.ForeColor = Color.FromArgb(220, 255, 255, 255);
                label2.TextAlign = leftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
                label2.Bounds = new Rectangle(headerTitleLeft, 50, headerTitleWidth, 24);

                // The standard window chrome already provides Close/Maximize/Minimize.
                label8.Visible = false;

                if (useCompactLayout)
                {
                    SetupCompactDashboardLayout(formWidth, leftToRight);
                    ActiveControl = null;
                    // Scrolling is restored after the financial controls receive their final bounds.
                    return;
                }

                int cardTop = 126;
                int cardHeight = 146;
                int cardWidth = (formWidth - (side * 2) - (gap * 2)) / 3;
                int firstCardX = leftToRight ? side : formWidth - side - cardWidth;
                int cardStep = cardWidth + gap;
                SellsNumberPanel.Bounds = new Rectangle(firstCardX, cardTop, cardWidth, cardHeight);
                CompanyNumberPanel.Bounds = new Rectangle(firstCardX + (leftToRight ? cardStep : -cardStep), cardTop, cardWidth, cardHeight);
                EmployeeNumberPanel.Bounds = new Rectangle(firstCardX + (leftToRight ? 2 * cardStep : -2 * cardStep), cardTop, cardWidth, cardHeight);

                ConfigureDashboardCard(SellsNumberPanel, SellsPanel, label6, SellsNumber, "إجمالي المبيعات", "قيمة الفواتير المسجلة", OutlinedIcons.RenderBitmap(AppIcon.Sale, 58, UiTheme.Primary, true));
                ConfigureDashboardCard(CompanyNumberPanel, CompanyPanel, label5, NumberOfEmp, "الشركات", "عدد الشركات المصنعة", OutlinedIcons.RenderBitmap(AppIcon.Language, 58, UiTheme.Primary, true));
                ConfigureDashboardCard(EmployeeNumberPanel, EmployeePanel, label4, EmployeeNumber, "الموظفون", "عدد مستخدمي النظام", OutlinedIcons.RenderBitmap(AppIcon.User, 58, UiTheme.Primary, true));

                panel3.Visible = false;
                if (label3.Parent == panel3)
                {
                    panel3.Controls.Remove(label3);
                    dashboardViewport.Controls.Add(label3);
                }

                int contentTop = cardTop + cardHeight + 66;
                // Reserve a full action band above the status strip. TextBox controls keep
                // their native height at high DPI, so a narrow footer can otherwise look
                // as if it is sitting underneath the grid even when bounds only touch.
                int footerHeight = 124;
                int contentBottom = formHeight - footerHeight - 28;
                int contentHeight = Math.Max(280, contentBottom - contentTop);
                int chartWidth = Math.Max(360, (int)((formWidth - (side * 2) - gap) * 0.36));
                int gridWidth = formWidth - (side * 2) - gap - chartWidth;
                int chartX = leftToRight ? side + gridWidth + gap : side;
                int gridX = leftToRight ? side : chartX + chartWidth + gap;

                label3.AutoSize = false;
                label3.Text = Present("سجل الفواتير وتحليل المبيعات");
                label3.Font = AppFonts.Create(13F, FontStyle.Bold);
                label3.ForeColor = UiTheme.Text;
                label3.TextAlign = leftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
                label3.Bounds = new Rectangle(gridX, contentTop - 40, gridWidth, 28);
                label3.BringToFront();

                CustomerGV.Bounds = new Rectangle(gridX, contentTop, gridWidth, contentHeight);
                CustomerGV.RightToLeft = leftToRight ? RightToLeft.No : RightToLeft.Yes;
                CustomerGV.BackgroundColor = UiTheme.Surface;
                CustomerGV.BorderStyle = BorderStyle.None;
                CustomerGV.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
                CustomerGV.ColumnHeadersHeight = 38;
                CustomerGV.RowTemplate.Height = 32;
                CustomerGV.EnableHeadersVisualStyles = false;

                chartPanel.Bounds = new Rectangle(chartX, contentTop, chartWidth, contentHeight);
                chartPanel.BackColor = UiTheme.Surface;
                chartPanel.Cursor = Cursors.Hand;

                if (lblCashierFilter != null && cmbCashierFilter != null)
                {
                    lblCashierFilter.AutoSize = false;
                    lblCashierFilter.Text = Present("الكاشير");
                    lblCashierFilter.Font = AppFonts.Create(9.5F, FontStyle.Bold);
                    lblCashierFilter.ForeColor = UiTheme.Text;
                    lblCashierFilter.TextAlign = leftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
                    int filterRight = gridX + gridWidth;
                    const int filterBandWidth = 364;
                    int filterBandLeft = leftToRight ? filterRight - filterBandWidth : gridX;
                    lblCashierFilter.Bounds = new Rectangle(
                        leftToRight ? filterBandLeft : filterBandLeft + 260,
                        contentTop - 47, 90, 36);

                    cmbCashierFilter.Font = AppFonts.Create(10F, FontStyle.Regular);
                    cmbCashierFilter.RightToLeft = leftToRight ? RightToLeft.No : RightToLeft.Yes;
                    cmbCashierFilter.TextAlign = leftToRight ? HorizontalAlignment.Left : HorizontalAlignment.Right;
                    cmbCashierFilter.Bounds = new Rectangle(
                        leftToRight ? filterBandLeft + 114 : filterBandLeft,
                        contentTop - 50, 250, 44);
                    int sectionTitleWidth = Math.Max(180, gridWidth - filterBandWidth - 16);
                    label3.Bounds = new Rectangle(
                        leftToRight ? gridX : gridX + filterBandWidth + 16,
                        contentTop - 40, sectionTitleWidth, 28);
                }

                int inputTop = formHeight - 98;
                int labelTop = inputTop - 24;
                int fieldWidth = 170;
                int fieldGap = 14;
                int fieldsRight = formWidth - side;

                PositionDashboardField(lblCustomerName, txtCustomerName, "اسم العميل", leftToRight ? side : fieldsRight - fieldWidth, labelTop, inputTop, fieldWidth);
                PositionDashboardField(lblPhoneNumber, txtPhoneNumber, "رقم الهاتف", leftToRight ? side + fieldWidth + fieldGap : fieldsRight - (fieldWidth * 2) - fieldGap, labelTop, inputTop, fieldWidth);
                PositionDashboardField(lblCustomerAddress, txtCustomerAddress, "العنوان", leftToRight ? side + (fieldWidth + fieldGap) * 2 : fieldsRight - (fieldWidth * 3) - (fieldGap * 2), labelTop, inputTop, fieldWidth);
                PositionDashboardField(lblTotalSpent, txtTotalSpent, "الإجمالي", leftToRight ? side + (fieldWidth + fieldGap) * 3 : fieldsRight - (fieldWidth * 4) - (fieldGap * 3), labelTop, inputTop, 140);

                int buttonWidth = 126;
                int buttonHeight = 44;
                int buttonGap = 12;
                int buttonsLeft = leftToRight ? formWidth - side - (buttonWidth * 2 + buttonGap) : side;
                StyleDashboardActionButton(button2, "رجوع", UiTheme.Primary);
                StyleDashboardActionButton(btnPrint, "إرشاد الطباعة", Color.FromArgb(47, 143, 118));
                StyleDashboardActionButton(btnDelete, "حذف", UiTheme.Danger);
                StyleDashboardActionButton(btnEdit, "تعديل", UiTheme.Primary);
                // هذا الجدول يعرض ملخصات مبيعات/عملاء وليس ملف عميل مستقلاً؛ لا تسمح
                // بتغيير الإجماليات أو حذف التاريخ من لوحة المبيعات.
                txtCustomerName.ReadOnly = true;
                txtCustomerAddress.ReadOnly = true;
                txtPhoneNumber.ReadOnly = true;
                txtTotalSpent.ReadOnly = true;
                btnEdit.Visible = false;
                btnDelete.Visible = false;

                button2.Bounds = new Rectangle(buttonsLeft, inputTop - 2, buttonWidth, buttonHeight);
                btnPrint.Bounds = new Rectangle(buttonsLeft + buttonWidth + buttonGap, inputTop - 2, buttonWidth, buttonHeight);
                btnDelete.Bounds = new Rectangle(buttonsLeft + (buttonWidth + buttonGap) * 2, inputTop - 2, buttonWidth, buttonHeight);
                btnEdit.Bounds = new Rectangle(buttonsLeft + (buttonWidth + buttonGap) * 3, inputTop - 2, buttonWidth, buttonHeight);

                panel2.Bounds = new Rectangle(0, formHeight - 24, formWidth, 24);
                panel2.Visible = false;

                EnsureDashboardInteractions();
            }
            finally
            {
                UiDirection.Apply(this, leftToRight);
                UiTheme.ApplyStatusStripPresentation(this, leftToRight);
                ApplyDashboardReadStatus();
                LayoutFinancialRead();
                if (compactDashboardLayout)
                {
                    dashboardViewport.AutoScrollMinSize = new Size(0, Math.Max(panel2.Bottom + 18, button2.Bottom + 40));
                    dashboardViewport.AutoScroll = true;
                    dashboardViewport.AutoScrollPosition = Point.Empty;
                }
                dashboardViewport.ResumeLayout(true);
                isApplyingDashboardLayout = false;
            }
        }

        internal void RefreshResponsiveLayout()
        {
            SetupDashboardLayout();
            PerformLayout();
        }

        // نسخة واضحة للشاشات الصغيرة: بطاقات فوق بعضها ثم الرسم والجدول والبيانات.
        // يضمن هذا أن كل معلومة تبقى متاحة بدلاً من تمديد النموذج أفقياً خارج الشاشة.
        private void SetupCompactDashboardLayout(int formWidth, bool leftToRight)
        {
            const int side = 20;
            const int gap = 14;
            int cardWidth = Math.Max(280, formWidth - (side * 2));
            int cardHeight = 112;
            int cardX = Math.Max(side, (formWidth - cardWidth) / 2);
            int cardTop = 106;

            SellsNumberPanel.Bounds = new Rectangle(cardX, cardTop, cardWidth, cardHeight);
            CompanyNumberPanel.Bounds = new Rectangle(cardX, cardTop + cardHeight + gap, cardWidth, cardHeight);
            EmployeeNumberPanel.Bounds = new Rectangle(cardX, cardTop + ((cardHeight + gap) * 2), cardWidth, cardHeight);
            ConfigureDashboardCard(SellsNumberPanel, SellsPanel, label6, SellsNumber, "إجمالي المبيعات", "قيمة الفواتير المسجلة", OutlinedIcons.RenderBitmap(AppIcon.Sale, 48, UiTheme.Primary, true));
            ConfigureDashboardCard(CompanyNumberPanel, CompanyPanel, label5, NumberOfEmp, "الشركات", "عدد الشركات المصنعة", OutlinedIcons.RenderBitmap(AppIcon.Language, 48, UiTheme.Primary, true));
            ConfigureDashboardCard(EmployeeNumberPanel, EmployeePanel, label4, EmployeeNumber, "الموظفون", "عدد مستخدمي النظام", OutlinedIcons.RenderBitmap(AppIcon.User, 48, UiTheme.Primary, true));

            panel3.Visible = false;
            if (label3.Parent == panel3)
            {
                panel3.Controls.Remove(label3);
                dashboardViewport.Controls.Add(label3);
            }

            int chartTop = cardTop + ((cardHeight + gap) * 3) + 22;
            int contentWidth = Math.Max(280, formWidth - (side * 2));
            chartPanel.Bounds = new Rectangle(side, chartTop, contentWidth, 210);
            chartPanel.BackColor = UiTheme.Surface;
            chartPanel.Cursor = Cursors.Hand;

            int gridTitleTop = chartPanel.Bottom + 22;
            label3.AutoSize = false;
            label3.Text = Present("سجل الفواتير وتحليل المبيعات");
            label3.Font = AppFonts.Create(12F, FontStyle.Bold);
            label3.ForeColor = UiTheme.Text;
            label3.TextAlign = leftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
            label3.Bounds = new Rectangle(side, gridTitleTop, contentWidth, 26);
            label3.BringToFront();

            if (lblCashierFilter != null && cmbCashierFilter != null)
            {
                lblCashierFilter.AutoSize = false;
                lblCashierFilter.Text = Present("الكاشير");
                lblCashierFilter.Font = AppFonts.Create(9.5F, FontStyle.Bold);
                lblCashierFilter.ForeColor = UiTheme.Text;
                lblCashierFilter.TextAlign = leftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
                lblCashierFilter.Bounds = new Rectangle(leftToRight ? side : side + contentWidth - 82,
                    gridTitleTop + 36, 82, 28);

                cmbCashierFilter.Font = AppFonts.Create(10F, FontStyle.Regular);
                cmbCashierFilter.RightToLeft = leftToRight ? RightToLeft.No : RightToLeft.Yes;
                cmbCashierFilter.TextAlign = leftToRight ? HorizontalAlignment.Left : HorizontalAlignment.Right;
                cmbCashierFilter.Bounds = new Rectangle(leftToRight ? side + 98 : side,
                    gridTitleTop + 28, Math.Max(190, contentWidth - 98), 44);
            }

            int gridTop = gridTitleTop + 84;
            CustomerGV.Bounds = new Rectangle(side, gridTop, contentWidth, 290);
            CustomerGV.RightToLeft = leftToRight ? RightToLeft.No : RightToLeft.Yes;
            CustomerGV.BackgroundColor = UiTheme.Surface;
            CustomerGV.BorderStyle = BorderStyle.None;
            CustomerGV.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            CustomerGV.ColumnHeadersHeight = 38;
            CustomerGV.RowTemplate.Height = 32;
            CustomerGV.EnableHeadersVisualStyles = false;

            int fieldTop = CustomerGV.Bottom + 24;
            PositionDashboardField(lblCustomerName, txtCustomerName, "اسم العميل", side, fieldTop, fieldTop + 22, contentWidth);
            fieldTop += 62;
            PositionDashboardField(lblPhoneNumber, txtPhoneNumber, "رقم الهاتف", side, fieldTop, fieldTop + 22, contentWidth);
            fieldTop += 62;
            PositionDashboardField(lblCustomerAddress, txtCustomerAddress, "العنوان", side, fieldTop, fieldTop + 22, contentWidth);
            fieldTop += 62;
            PositionDashboardField(lblTotalSpent, txtTotalSpent, "الإجمالي", side, fieldTop, fieldTop + 22, contentWidth);

            txtCustomerName.ReadOnly = true;
            txtCustomerAddress.ReadOnly = true;
            txtPhoneNumber.ReadOnly = true;
            txtTotalSpent.ReadOnly = true;
            btnEdit.Visible = false;
            btnDelete.Visible = false;
            StyleDashboardActionButton(button2, "رجوع", UiTheme.Primary);
            StyleDashboardActionButton(btnPrint, "إرشاد الطباعة", Color.FromArgb(47, 143, 118));

            int actionsTop = fieldTop + 66;
            int actionWidth = Math.Max(124, (contentWidth - gap) / 2);
            button2.Bounds = new Rectangle(leftToRight ? side : side + actionWidth + gap, actionsTop, actionWidth, 44);
            btnPrint.Bounds = new Rectangle(leftToRight ? side + actionWidth + gap : side, actionsTop, actionWidth, 44);

            panel2.Bounds = new Rectangle(0, actionsTop + 62, formWidth, 24);
            panel2.Visible = false;
            // A width equal to the window creates a horizontal scrollbar once the
            // vertical scrollbar consumes its own width.
            EnsureDashboardInteractions();
        }

        private void EnsureDashboardInteractions()
        {
            if (!isLayoutInitialized)
            {
                chartPanel.Paint += ChartPanel_Paint;
                chartPanel.Click += async (s, e) =>
                {
                    if(dashboardAsyncRead!=null)await ReloadDashboardAsync(cashierNames.Count==0);
                    else {FetchChartData();chartPanel.Invalidate();}
                };

                // اختيار الصف بالمؤشر أو بلوحة المفاتيح يجب أن يعرض الملخص نفسه
                // الذي يظهر عند النقر بالفأرة؛ السجل هنا للعرض فقط ولا يحرر بياناته.
                CustomerGV.SelectionChanged += CustomerGV_SelectionChanged;

                if(dashboardAsyncRead==null)FetchChartData();
                isLayoutInitialized = true;
            }

            FormatDashboardGrid();
            chartPanel.Invalidate();
        }

        private void ConfigureDashboardCard(Panel card, Panel footer, Label title, Label number, string titleText, string subtitleText, Image icon)
        {
            if (card == null) return;

            card.BackColor = UiTheme.SurfaceVariant;
            card.BorderStyle = BorderStyle.None;
            card.Padding = new Padding(0);
            WinFormsShapeAdapter.ApplyCard(card);

            footer.Visible = true;
            footer.BackColor = UiTheme.Primary;
            WinFormsShapeAdapter.ApplyCard(footer);
            bool compactCard = card.Height < 130;
            int footerHeight = compactCard ? 34 : 42;
            int horizontalPadding = card.Width < 330 ? 16 : 34;
            int iconSize = compactCard ? 44 : card.Width < 330 ? 48 : 58;
            int iconX = presentationLeftToRight
                ? horizontalPadding
                : card.Width - iconSize - horizontalPadding;
            int textX = presentationLeftToRight
                ? iconX + iconSize + horizontalPadding
                : horizontalPadding;
            int textWidth = presentationLeftToRight
                ? Math.Max(120, card.Width - textX - horizontalPadding)
                : Math.Max(120, iconX - textX - horizontalPadding);

            footer.Bounds = new Rectangle(0, card.Height - footerHeight, card.Width, footerHeight);
            footer.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;

            title.AutoSize = false;
            title.Text = Present(titleText);
            title.Font = AppFonts.Create(10.75F, FontStyle.Bold);
            title.ForeColor = Color.White;
            title.TextAlign = ContentAlignment.MiddleCenter;
            title.Dock = DockStyle.Fill;

            number.AutoSize = false;
            number.Font = AppFonts.Create(22F, FontStyle.Bold);
            number.ForeColor = UiTheme.Primary;
            number.BackColor = Color.Transparent;
            number.TextAlign = ContentAlignment.MiddleCenter;
            number.Bounds = new Rectangle(textX, compactCard ? 10 : 28, textWidth, compactCard ? 32 : 40);
            number.BringToFront();

            Label subtitle = card.Controls.Find(card.Name + "Subtitle", false).FirstOrDefault() as Label;
            if (subtitle == null)
            {
                subtitle = new Label { Name = card.Name + "Subtitle" };
                card.Controls.Add(subtitle);
            }

            subtitle.AutoSize = false;
            subtitle.Text = Present(subtitleText);
            subtitle.Font = AppFonts.Create(9F, FontStyle.Regular);
            subtitle.ForeColor = UiTheme.MutedText;
            subtitle.BackColor = Color.Transparent;
            subtitle.TextAlign = ContentAlignment.MiddleCenter;
            subtitle.Bounds = new Rectangle(textX, compactCard ? 43 : 72, textWidth, compactCard ? 22 : 24);

            PictureBox iconBox = card.Controls.Find(card.Name + "Icon", false).FirstOrDefault() as PictureBox;
            if (iconBox == null)
            {
                iconBox = new PictureBox { Name = card.Name + "Icon" };
                card.Controls.Add(iconBox);
            }

            Image previousIcon = iconBox.Image;
            iconBox.Image = icon;
            if (previousIcon != null && !ReferenceEquals(previousIcon, icon)) previousIcon.Dispose();
            iconBox.SizeMode = PictureBoxSizeMode.Zoom;
            iconBox.BackColor = Color.Transparent;
            iconBox.Bounds = new Rectangle(iconX, compactCard ? 17 : 28, iconSize, iconSize);
            iconBox.BringToFront();

            footer.BringToFront();
        }

        private void PositionDashboardField(Label label, TextBox input, string text, int x, int labelY, int inputY, int width)
        {
            label.AutoSize = false;
            label.Text = Present(text);
            label.Font = AppFonts.Create(9F, FontStyle.Bold);
            label.ForeColor = UiTheme.Text;
            label.TextAlign = presentationLeftToRight
                ? ContentAlignment.MiddleLeft
                : ContentAlignment.MiddleRight;
            label.Bounds = new Rectangle(x, labelY, width, 22);

            input.Font = AppFonts.Create(10F, FontStyle.Regular);
            input.TextAlign = HorizontalAlignment.Center;
            input.RightToLeft = presentationLeftToRight ? RightToLeft.No : RightToLeft.Yes;
            input.Bounds = new Rectangle(x, inputY, width, 30);
        }

        private void StyleDashboardActionButton(AntdUI.Button button, string text, Color color)
        {
            button.Text = Present(text);
            button.Font = AppFonts.Create(9.5F, FontStyle.Bold);
            button.TextAlign = ContentAlignment.MiddleCenter;
            button.MinimumSize = new Size(104, 0);
            button.Cursor = Cursors.Hand;
            AntdThemeAdapter.Apply(button);
            DesktopComponents.StyleOutlinedFooterAction(button, color == UiTheme.Danger);
            AppIcon? icon = UiTheme.ResolveButtonIcon(text);
            button.IconSvg = icon.HasValue ? OutlinedIcons.Svg(icon.Value, !presentationLeftToRight) : null;
            button.IconSize = icon.HasValue ? new Size(16, 16) : Size.Empty;
            button.IconPosition = presentationLeftToRight ? AntdUI.TAlignMini.Left : AntdUI.TAlignMini.Right;
            button.AccessibleName = button.Text;
        }

        private void FormatDashboardGrid()
        {
            if (CustomerGV == null) return;

            CustomerGV.RightToLeft = presentationLeftToRight ? RightToLeft.No : RightToLeft.Yes;
            CustomerGV.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            CustomerGV.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            CustomerGV.MultiSelect = false;
            CustomerGV.ReadOnly = true;
            CustomerGV.AllowUserToAddRows = false;
            CustomerGV.AllowUserToDeleteRows = false;
            CustomerGV.AllowUserToResizeRows = false;
            CustomerGV.RowHeadersVisible = false;
            CustomerGV.EnableHeadersVisualStyles = false;
            CustomerGV.TabStop = true;
            CustomerGV.AccessibleName = Present("سجل الفواتير");
            CustomerGV.AccessibleDescription = Present("استخدم الأسهم لاختيار فاتورة وعرض ملخص العميل أدناه. السجل للقراءة فقط.");

            CustomerGV.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            CustomerGV.ColumnHeadersDefaultCellStyle.BackColor = UiTheme.PrimaryDark;
            CustomerGV.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            CustomerGV.ColumnHeadersDefaultCellStyle.Font = AppFonts.Create(9.5F, FontStyle.Bold);
            CustomerGV.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            CustomerGV.DefaultCellStyle.Font = AppFonts.Create(9.25F, FontStyle.Regular);
            CustomerGV.DefaultCellStyle.SelectionBackColor = UiTheme.Selection;
            CustomerGV.DefaultCellStyle.SelectionForeColor = UiTheme.Text;
            CustomerGV.AlternatingRowsDefaultCellStyle.BackColor = UiTheme.AlternateRow;

            SetColumnHeader("CustomerId", "رقم الفاتورة");
            SetColumnHeader("CustomerName", "اسم العميل");
            SetColumnHeader("CustomerAddress", "العنوان");
            SetColumnHeader("PhoneNumber", "رقم الهاتف");
            SetColumnHeader("TotalSpent", "الإجمالي");
            SetColumnHeader("InvoiceDate", "التاريخ");
            SetColumnHeader("EmployeeName", "الكاشير");
        }

        private void SetColumnHeader(string columnName, string headerText)
        {
            if (CustomerGV.Columns.Contains(columnName))
            {
                CustomerGV.Columns[columnName].HeaderText = Present(headerText);
            }
        }

        private void FetchChartData()
        {
            chartData.Clear();
            chartLoadFailed = false;
            try
            {
                foreach (DashboardSalesTrendPoint point in dashboardReadService.GetRecentSalesTrend(SelectedCashierName()))
                    chartData.Add(new KeyValuePair<string, float>(point.SaleDay.ToString("MM/dd"), Convert.ToSingle(point.DailyTotal)));
            }
            catch (Exception)
            {
                // لا نعرض أرقامًا تجريبية على أنها مبيعات حقيقية عند فشل القراءة.
                chartData.Clear();
                chartLoadFailed = true;
            }
        }

        // رسم كارت الأداء ومؤشر خط مبيعات الأدوية الفخم (GDI+ Anti-Aliased Area Line Chart)
        private void ChartPanel_Paint(object sender, PaintEventArgs e)
        {
            if (financialReadService != null) { PaintFinancialChart(sender, e); return; }
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            Panel panel = sender as Panel;
            if (panel == null) return;

            // رسم إطار خارجي ناعم جداً
            using (Pen borderPen = new Pen(UiTheme.Border, 1))
            {
                g.DrawRectangle(borderPen, 0, 0, panel.Width - 1, panel.Height - 1);
            }

            // رسم خلفية ترويسة المؤشر البياني
            Color primaryColor = UiTheme.Primary;
            using (SolidBrush headerBrush = new SolidBrush(UiTheme.SurfaceVariant))
            {
                g.FillRectangle(headerBrush, 1, 1, panel.Width - 2, 45);
            }

            // كتابة عنوان المؤشر بالخط الفخم
            using (Font titleFont = AppFonts.Create(10F, FontStyle.Bold))
            using (SolidBrush titleBrush = new SolidBrush(UiTheme.Text))
            {
                string titleText = Present("مؤشر المبيعات حسب آخر الفواتير");
                SizeF titleSize = g.MeasureString(titleText, titleFont);
                float titleX = presentationLeftToRight ? 15 : Math.Max(15, panel.Width - titleSize.Width - 15);
                g.DrawString(titleText, titleFont, titleBrush, titleX, 12);
            }

            if (chartData.Count == 0)
            {
                using (Font emptyFont = AppFonts.Create(10F, FontStyle.Regular))
                using (SolidBrush emptyBrush = new SolidBrush(UiTheme.MutedText))
                {
                    string emptyText = chartLoadFailed
                        ? Present("تعذر تحميل بيانات المبيعات")
                        : Present("لا توجد بيانات مبيعات متاحة");
                    SizeF textSize = g.MeasureString(emptyText, emptyFont);
                    g.DrawString(
                        emptyText,
                        emptyFont,
                        emptyBrush,
                        Math.Max(12, (panel.Width - textSize.Width) / 2),
                        Math.Max(58, (panel.Height - textSize.Height) / 2));
                }
                return;
            }

            // تحديد حدود الرسم
            float marginX = 50;
            float marginY = 70;
            float chartWidth = panel.Width - (marginX * 2);
            float chartHeight = panel.Height - marginY - 45;

            // حساب أعلى قيمة للمبيعات لرسم المقياس
            float maxVal = 0;
            foreach (var kp in chartData)
            {
                if (kp.Value > maxVal) maxVal = kp.Value;
            }
            if (maxVal == 0) maxVal = 10000;
            maxVal *= 1.25f; // ترك مساحة تنفس 25% بالأعلى

            // تحديد إحداثيات النقاط البيانية
            PointF[] points = new PointF[chartData.Count];
            float stepX = chartData.Count > 1 ? chartWidth / (chartData.Count - 1) : 0;

            for (int i = 0; i < chartData.Count; i++)
            {
                float x = chartData.Count > 1 ? marginX + (i * stepX) : marginX + (chartWidth / 2);
                float y = (panel.Height - 38) - ((chartData[i].Value / maxVal) * chartHeight);
                points[i] = new PointF(x, y);
            }

            // رسم خطوط الشبكة الأفقية وقيم محور Y
            int gridLines = 4;
            using (Pen gridPen = new Pen(UiTheme.Border, 1))
            using (Font axisFont = AppFonts.Create(7.5F))
            using (SolidBrush axisBrush = new SolidBrush(UiTheme.MutedText))
            {
                for (int i = 0; i <= gridLines; i++)
                {
                    float y = (panel.Height - 38) - ((i / (float)gridLines) * chartHeight);
                    g.DrawLine(gridPen, marginX, y, marginX + chartWidth, y);

                    float labelVal = (i / (float)gridLines) * maxVal;
                    string labelStr = FormatChartAxisAmount(labelVal, dashboardCurrencyDisplay);
                    g.DrawString(labelStr, axisFont, axisBrush, 5, y - 6);
                }
            }

            // رسم تعبئة متدرجة أنيقة جداً أسفل منحنى الرسم (Area Gradient Spline)
            if (points.Length > 1)
            {
                using (GraphicsPath path = new GraphicsPath())
                {
                    path.AddLine(points[0].X, panel.Height - 38, points[0].X, points[0].Y);
                    path.AddCurve(points, 0.4f);
                    path.AddLine(points[points.Length - 1].X, points[points.Length - 1].Y, points[points.Length - 1].X, panel.Height - 38);
                    path.CloseAllFigures();

                    Color startColor = Color.FromArgb(90, UiTheme.Primary);
                    Color endColor = Color.FromArgb(5, UiTheme.Surface);
                    using (LinearGradientBrush bgBrush = new LinearGradientBrush(
                        new PointF(0, marginY), new PointF(0, panel.Height - 38), startColor, endColor))
                    {
                        g.FillPath(bgBrush, path);
                    }
                }

                // رسم خط المنحنى البياني الانسيابي
                using (Pen linePen = new Pen(UiTheme.Primary, 3))
                {
                    linePen.LineJoin = LineJoin.Round;
                    g.DrawCurve(linePen, points, 0.4f);
                }
            }

            // رسم العقد المتوهجة فوق المنحنى وعناوين المحور الأفقي
            using (SolidBrush nodeBrush = new SolidBrush(UiTheme.Primary))
            using (SolidBrush whiteBrush = new SolidBrush(UiTheme.Surface))
            using (Pen nodeOutline = new Pen(UiTheme.Primary, 2))
            using (Font axisFont = AppFonts.Create(8F, FontStyle.Bold))
            using (SolidBrush axisBrush = new SolidBrush(UiTheme.Text))
            {
                for (int i = 0; i < points.Length; i++)
                {
                    // رسم النقطة المتوهجة
                    g.FillEllipse(whiteBrush, points[i].X - 5, points[i].Y - 5, 10, 10);
                    g.DrawEllipse(nodeOutline, points[i].X - 5, points[i].Y - 5, 10, 10);
                    g.FillEllipse(nodeBrush, points[i].X - 2.5f, points[i].Y - 2.5f, 5, 5);

                    // رسم تسمية التواريخ أسفل المحور
                    string xLabel = chartData[i].Key;
                    float xLabelWidth = g.MeasureString(xLabel, axisFont).Width;
                    g.DrawString(xLabel, axisFont, axisBrush, points[i].X - (xLabelWidth / 2), panel.Height - 25);

                    // رسم قيم المبيعات لكل نقطة بشكل عائم وأنيق
                    string valLabel = chartData[i].Value >= 1000 ? $"{chartData[i].Value / 1000:N0}k" : $"{chartData[i].Value:N0}";
                    float valWidth = g.MeasureString(valLabel, axisFont).Width;
                    g.DrawString(valLabel, axisFont, nodeBrush, points[i].X - (valWidth / 2), points[i].Y - 20);
                }
            }
        }

        // يحدث عدد الشركات في اللوحة.
        private void UpdateCompanyCountLabel()
        {
            try
            {
                NumberOfEmp.Text = dashboardReadService.GetCompanyCount().ToString();
            }
            catch (Exception ex)
            {
                ShowDashboardError("تعذر تحميل عدد الشركات.", ex);
            }
        }

        // يحدث عدد الموظفين في اللوحة.
        private void UpdateEmployeeCountLabel()
        {
            try
            {
                EmployeeNumber.Text = dashboardReadService.GetEmployeeCount().ToString();
            }
            catch (Exception ex)
            {
                ShowDashboardError("تعذر تحميل عدد الموظفين.", ex);
            }
        }

        // يحدث إجمالي المبيعات بالكامل.
        internal static string FormatChartAxisAmount(float amount, string currencyDisplay)
        {
            string number = amount >= 1000
                ? (amount / 1000).ToString("N0", CultureInfo.InvariantCulture) + "k"
                : amount.ToString("N0", CultureInfo.InvariantCulture);
            return AppendDashboardCurrency(number, currencyDisplay);
        }

        private static string AppendDashboardCurrency(string number, string currencyDisplay)
        {
            string symbol = (currencyDisplay ?? string.Empty).Trim();
            return symbol.Length == 0 ? number : number + " " + symbol;
        }

        private void RefreshSalesAmountDisplay()
        {
            if (displayedSalesTotal.HasValue)
                SellsNumber.Text = AppendDashboardCurrency(displayedSalesTotal.Value.ToString("N2", CultureInfo.InvariantCulture), dashboardCurrencyDisplay);
        }

        private async Task LoadDashboardCurrencyAsync()
        {
            long currencySessionRevision = SessionContext.CurrentSessionRevision;
            var actor = new OperationActor { UserName = SessionContext.CurrentUser, UserRole = SessionContext.CurrentRole };
            try
            {
                CurrencyDisplaySnapshot snapshot = await currencyReadService.GetSystemCurrencySnapshotAsync(actor, currencyLoadCancellation.Token);
                if (IsDisposed || Disposing || currencyLoadCancellation.IsCancellationRequested || currencySessionRevision != SessionContext.CurrentSessionRevision || !DashboardContextCurrent) return;
                dashboardCurrencyDisplay = snapshot.DisplayText;
                RefreshSalesAmountDisplay();
                Invalidate(true);
            }
            catch (OperationCanceledException) { }
            catch (Exception)
            {
                if (IsDisposed || Disposing || currencyLoadCancellation.IsCancellationRequested || currencySessionRevision != SessionContext.CurrentSessionRevision || !DashboardContextCurrent) return;
                dashboardCurrencyDisplay = string.Empty;
                RefreshSalesAmountDisplay();
                Invalidate(true);
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            CancelDashboardRead();CancelDashboardCurrencyRead();
            base.OnFormClosed(e);
        }

        private void UpdateSalesTotalLabel()
        {
            try
            {
                decimal totalSales = dashboardReadService.GetSalesTotal(SelectedCashierName());
                displayedSalesTotal = totalSales;
                RefreshSalesAmountDisplay();
            }
            catch (Exception ex)
            {
                ShowDashboardError("تعذر تحميل إجمالي المبيعات.", ex);
            }
        }

        // دالة لتحميل بيانات فواتير العملاء.
        private void LoadCustomerBillData()
        {
            try
            {
                DataTable dataTable = CreateCustomerSummaryTable();
                foreach (DashboardCustomerSummary summary in dashboardReadService.GetCustomerSummaries(SelectedCashierName()))
                {
                    string customerId = (summary.CustomerId ?? string.Empty).Replace("★", string.Empty).Trim();
                    if (summary.TotalSpent >= 15000m) customerId += " ★";
                    dataTable.Rows.Add(customerId, summary.CustomerName, summary.CustomerAddress, summary.PhoneNumber,
                        summary.TotalSpent, summary.InvoiceDate.HasValue ? (object)summary.InvoiceDate.Value : DBNull.Value, summary.EmployeeName);
                }

                CustomerGV.DataSource = dataTable;
                SetCustomerColumnWidths();
            }
            catch (Exception ex)
            {
                ShowDashboardError("تعذر تحميل سجل العملاء.", ex);
            }
        }

        private static DataTable CreateCustomerSummaryTable()
        {
            DataTable table = new DataTable();
            table.Columns.Add("CustomerId", typeof(string));
            table.Columns.Add("CustomerName", typeof(string));
            table.Columns.Add("CustomerAddress", typeof(string));
            table.Columns.Add("PhoneNumber", typeof(string));
            table.Columns.Add("TotalSpent", typeof(decimal));
            table.Columns.Add("InvoiceDate", typeof(DateTime));
            table.Columns.Add("EmployeeName", typeof(string));
            return table;
        }

        private void SetCustomerColumnWidths()
        {
            if (CustomerGV.Columns.Count == 0) return;
            if (CustomerGV.Columns["CustomerId"] != null) CustomerGV.Columns["CustomerId"].Width = 100;
            if (CustomerGV.Columns["CustomerName"] != null) CustomerGV.Columns["CustomerName"].Width = 150;
            if (CustomerGV.Columns["CustomerAddress"] != null) CustomerGV.Columns["CustomerAddress"].Width = 150;
            if (CustomerGV.Columns["PhoneNumber"] != null) CustomerGV.Columns["PhoneNumber"].Width = 110;
            if (CustomerGV.Columns["TotalSpent"] != null) CustomerGV.Columns["TotalSpent"].Width = 100;
            if (CustomerGV.Columns["InvoiceDate"] != null) CustomerGV.Columns["InvoiceDate"].Width = 130;
            if (CustomerGV.Columns["EmployeeName"] != null) CustomerGV.Columns["EmployeeName"].Width = 150;
        }

        // يقوم بتعبئة حقول الإدخال عند تحديد سطر في الجدول.
        private void CustomerGV_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            PopulateCustomerFields(e.RowIndex);
        }

        // يقوم بتعبئة حقول الإدخال عند النقر بالفأرة على سطر في الجدول.
        private void CustomerGV_CellContentClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            PopulateCustomerFields(e.RowIndex);
        }

        // يحدّث الملخص أيضاً عند التنقل بين الصفوف بالأسهم أو Tab.
        private void CustomerGV_SelectionChanged(object sender, EventArgs e)
        {
            if (CustomerGV.CurrentRow != null)
            {
                PopulateCustomerFields(CustomerGV.CurrentRow.Index);
            }
        }

        // يقوم بتعبئة حقول النصوص ببيانات العميل المختار من الجدول.
        private void PopulateCustomerFields(int rowIndex)
        {
            if(!DashboardContextCurrent){RejectDashboardContext();return;}
            try
            {
                if (rowIndex >= 0 && rowIndex < CustomerGV.Rows.Count)
                {
                    DataGridViewRow row = CustomerGV.Rows[rowIndex];
                    if (row.Cells["CustomerId"].Value != null)
                    {
                        selectedCustomerId = row.Cells["CustomerId"].Value.ToString().Replace("★", "").Trim();
                        txtCustomerName.Text = row.Cells["CustomerName"].Value?.ToString() ?? "";
                        txtCustomerAddress.Text = row.Cells["CustomerAddress"].Value?.ToString() ?? "";
                        txtPhoneNumber.Text = row.Cells["PhoneNumber"].Value?.ToString() ?? "";
                        txtTotalSpent.Text = row.Cells["TotalSpent"].Value?.ToString() ?? "";
                    }
                }
            }
            catch (Exception ex)
            {
                ShowDashboardError("تعذر عرض ملخص العميل المحدد.", ex);
            }
        }

        // يمسح حقول النصوص.
        private void ClearInputFields()
        {
            selectedCustomerId = "";
            txtCustomerName.Clear();
            txtCustomerAddress.Clear();
            txtPhoneNumber.Clear();
            txtTotalSpent.Clear();
        }

        // زر التعديل لتحديث بيانات العميل المختار.
        private void btnEdit_Click(object sender, EventArgs e)
        {
            ExplainReadOnlyCustomerSummary();
        }

        // زر الحذف لحذف العميل المختار.
        private void btnDelete_Click(object sender, EventArgs e)
        {
            ExplainReadOnlyCustomerSummary();
        }

        private void ExplainReadOnlyCustomerSummary()
        {
            if(!DashboardContextCurrent){RejectDashboardContext();return;}
            AppNotifications.Show(
                "ملخص العميل وسجل المبيعات للعرض فقط؛ لا يمكن تعديل الإجماليات أو حذفها من لوحة التحكم.\n\n" +
                "تتم التصحيحات المالية عبر مرتجع أو إشعار دائن معتمد، وليس بتعديل التاريخ.",
                "سجل مبيعات محمي",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void ShowDashboardError(string userMessage, Exception exception)
        {
            string supportCode = AppDiagnostics.Log("Dashboard", exception);
            AppNotifications.Show(UiLanguageCatalog.Format(presentationLeftToRight,
                Present("{0}\n\nرمز الدعم: {1}"), Present(userMessage), supportCode),
                Present("لوحة التحكم"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private void SellsNumberPanel_Paint(object sender, PaintEventArgs e)
        {
        }

        private void SellsNumber_Click(object sender, EventArgs e)
        {
        }

        // يوضح قيود سجل العملاء حتى لا يتحول تجميع العميل إلى فاتورة مضللة.
        private void btnPrint_Click(object sender, EventArgs e)
        {
            if (financialReadService != null && !showFinancialCustomers) { PreviewFinancialRead(); return; }
            if(!DashboardContextCurrent){RejectDashboardContext();return;}
            // سجل العملاء مجمّع بحسب العميل ولا يملك رقم فاتورة فريداً؛ طباعته كفاتورة
            // كانت تخلط مبيعات العميل المتكررة في مستند واحد. لا ننتج مستنداً مضللاً.
            AppNotifications.Show(
                "سجل العملاء يعرض إجماليات تاريخية، وليس فاتورة محددة قابلة لإعادة الطباعة.\n\n" +
                "يمكن إعادة طباعة الفاتورة التجارية فقط من سجل فواتير يعتمد رقم الفاتورة ولقطتها المعتمدة.",
                "إعادة طباعة آمنة",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        // إعداد وتجهيز قائمة فلترة الكاشير من خلال أدوات المصمم (Toolbox)
        private void InitializeCashierFilter()
        {
            lblCashierFilter.BringToFront();
            cmbCashierFilter.BringToFront();

            LoadCashiersIntoSelector();
        }

        // جلب أسماء الكاشيرز من قاعدة البيانات
        private void LoadCashiersIntoSelector()
        {
            isLoadingCashierFilter = true;
            try
            {
                IReadOnlyList<string> names = dashboardReadService.GetCashierNames() ?? Array.Empty<string>();
                cmbCashierFilter.Items.Clear();
                cmbCashierFilter.Items.Add(Present("جميع الكاشيرين"));
                cashierNames.Clear();
                cashierNames.Add(string.Empty);
                foreach (string cashierName in names)
                {
                    if (string.IsNullOrWhiteSpace(cashierName)) continue;
                    cmbCashierFilter.Items.Add(cashierName);
                    cashierNames.Add(cashierName);
                }

                cmbCashierFilter.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                ShowDashboardError("تعذر تحميل قائمة الكاشيرين.", ex);
            }
            finally { isLoadingCashierFilter = false; }
        }

        private string SelectedCashierName()
        {
            int index = cmbCashierFilter == null ? -1 : cmbCashierFilter.SelectedIndex;
            if (index <= 0 || index >= cashierNames.Count)
                return string.Empty;
            return cashierNames[index];
        }

        // معالج حدث تغيير الاختيار في فلتر الكاشير
        private async void CmbCashierFilter_SelectedIndexChanged(object sender, EventArgs e)
        {
            if(isLoadingCashierFilter||!IsHandleCreated)return;
            if(dashboardAsyncRead!=null){await ReloadDashboardAsync(false);return;}
            UpdateSalesTotalLabel();LoadCustomerBillData();FetchChartData();chartPanel.Invalidate();
        }
    }
}




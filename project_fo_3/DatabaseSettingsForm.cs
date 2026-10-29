// شرح الملف: نافذة إعداد اتصال قاعدة البيانات التي تفتح من شاشة الدخول عبر F1.
// تسمح بتحميل قواعد البيانات، اختبار الاتصال، وحفظ إعدادات SQL Server.
using System;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace project_fo_3
{
    // نافذة إعداد الاتصال بقاعدة البيانات التي تظهر عند الضغط على F1 من شاشة الدخول.
    // توفر اختيار السيرفر، تحميل قواعد البيانات، اختبار الاتصال، وحفظ الإعدادات.
    public partial class DatabaseSetingsForm : Form
    {
        // قائمة/حقل اسم السيرفر، مثل . أو .\SQLEXPRESS.
        private readonly ComboBox txtServer;

        // قائمة/حقل اسم قاعدة البيانات المطلوب استخدامها أو إنشاؤها.
        private readonly ComboBox cmbDatabase;

        // يحدد استخدام مصادقة Windows بدل اسم مستخدم وكلمة مرور SQL.
        private readonly CheckBox chkWindowsAuth;

        // يحدد الوضع اللوني للتطبيق.
        private readonly ComboBox cmbTheme;

        private Label lblTitle;
        private Label lblSubtitle;
        private Label lblServer;
        private Label lblDatabase;
        private Label lblTheme;

        // يعرض حالة آخر عملية للمستخدم.
        private readonly Label lblStatus;

        // زر اكتشاف السيرفرات المتاحة أو إعادة تعبئة المقترحات.
        private readonly AntdUI.Button btnFindServers;

        // زر تحميل قواعد البيانات من السيرفر.
        private readonly AntdUI.Button btnLoadDatabases;

        // زر اختبار الاتصال دون حفظ أو تهيئة الجداول.
        private readonly AntdUI.Button btnTest;

        // زر حفظ إعدادات الاتصال.
        private readonly AntdUI.Button btnSave;
        private readonly AntdUI.Button btnClose;
        private readonly AntdUI.Panel settingsBody;
        private readonly Panel settingsHeader;
        private readonly Panel settingsFooter;
        private readonly bool previewMode;
        private readonly bool leftToRight;
        internal bool PresentationLeftToRight => leftToRight;
        private bool layoutInProgress;
        private bool isBusy;
        private bool closeBlockedForWrite;

        // يبني نافذة إعداد الاتصال ويملأها بالقيم الحالية.
        public DatabaseSetingsForm() : this(false, UiLanguageCatalog.IsEnglishActive) { }

        internal DatabaseSetingsForm(bool previewMode) : this(previewMode, false) { }

        internal DatabaseSetingsForm(bool previewMode, bool leftToRight)
        {
            this.previewMode = previewMode;
            this.leftToRight = leftToRight;
            Text = Present("إعدادات اتصال قاعدة البيانات");
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = true;
            // تبقى القياسات الأصلية مرجعاً للتصميم، لكن لا نسمح لنافذة مسؤول النظام
            // أن تخرج خارج شاشة منخفضة الدقة أو مع تكبير DPI مرتفع.
            Rectangle workingArea = Screen.PrimaryScreen.WorkingArea;
            int clientWidth = Math.Min(760, Math.Max(360, workingArea.Width - 48));
            int clientHeight = Math.Min(570, Math.Max(320, workingArea.Height - 48));
            ClientSize = new Size(clientWidth, clientHeight);
            MinimumSize = new Size(360, 320);
            AutoScaleMode = AutoScaleMode.Dpi;
            // نسمح بالتمرير الرأسي عند الحاجة، لكن لا نفرض عرض تصميم ثابتاً
            // يتسبب في قص حقول الاتصال أو تمرير أفقي على الشاشات الصغيرة.
            AutoScroll = false;
            Font = AppFonts.Create(9F);
            BackColor = Color.WhiteSmoke;
            RightToLeft = leftToRight ? RightToLeft.No : RightToLeft.Yes;
            RightToLeftLayout = !leftToRight;

            const int labelLeft = 30;
            const int labelWidth = 145;
            const int fieldLeft = 190;
            const int fieldWidth = 500;
            const int fieldHeight = 30;

            lblTitle = new Label
            {
                Name = "databaseSettingsTitle",
                Text = Present("إعدادات الاتصال"),
                Font = AppFonts.Create(15F, FontStyle.Bold),
                AutoSize = false,
                Location = new Point(labelLeft, 18)
            };

            lblSubtitle = new Label
            {
                Name = "databaseSettingsSubtitle",
                Text = Present("اختر الخادم وقاعدة التشغيل، ثم اختبر الاتصال قبل الحفظ."),
                Font = AppFonts.Create(9.5F),
                AutoSize = false,
                ForeColor = UiTheme.MutedText
            };

            lblServer = CreateSettingsLabel(Present("خادم SQL"), 74, labelLeft, labelWidth);
            lblServer.Name = "databaseServerLabel";
            txtServer = new ComboBox
            {
                Name = "databaseServer",
                Location = new Point(fieldLeft, 72),
                Size = new Size(fieldWidth, fieldHeight),
                DropDownStyle = ComboBoxStyle.DropDown,
                TabIndex = 0
            };
            LoadServerSuggestions();

            chkWindowsAuth = new CheckBox
            {
                Text = Present("مصادقة Windows (المدعومة حالياً)"),
                Checked = true,
                AutoSize = true,
                Location = new Point(fieldLeft, 112),
                TabIndex = 1
            };

            lblDatabase = CreateSettingsLabel(Present("قاعدة البيانات"), 150, labelLeft, labelWidth);
            lblDatabase.Name = "databaseCatalogLabel";
            cmbDatabase = new ComboBox
            {
                Name = "databaseCatalog",
                Location = new Point(fieldLeft, 148),
                Size = new Size(fieldWidth, fieldHeight),
                DropDownStyle = ComboBoxStyle.DropDown,
                TabIndex = 2
            };

            lblTheme = CreateSettingsLabel(Present("المظهر"), 190, labelLeft, labelWidth);
            lblTheme.Name = "databaseThemeLabel";
            cmbTheme = new ComboBox
            {
                Name = "databaseTheme",
                Location = new Point(fieldLeft, 188),
                Size = new Size(fieldWidth, fieldHeight),
                DropDownStyle = ComboBoxStyle.DropDownList,
                TabIndex = 3
            };
            cmbTheme.Items.Add(Present("فاتح"));
            cmbTheme.Items.Add(Present("داكن"));
            cmbTheme.Items.Add(Present("حسب النظام"));

            btnFindServers = DesktopComponents.Action("databaseFindServers", Present("بحث"), AppIcon.Search);
            btnFindServers.TabIndex = 4;
            btnFindServers.Click += FindServers_Click;

            btnLoadDatabases = DesktopComponents.Action("databaseLoadCatalogs", Present("تحميل قواعد البيانات"), AppIcon.Database);
            btnLoadDatabases.TabIndex = 5;
            btnLoadDatabases.Click += LoadDatabases_Click;

            btnTest = DesktopComponents.Action("databaseTestConnection", Present("اختبار"), AppIcon.Confirm);
            btnTest.TabIndex = 6;
            btnTest.Click += TestConnection_Click;

            btnSave = DesktopComponents.Action("databaseSaveConnection", Present("حفظ"), AppIcon.Save);
            btnSave.TabIndex = 7;
            btnSave.Click += Save_Click;

            btnClose = DesktopComponents.Action("databaseClose", Present("إغلاق"), AppIcon.Close);
            btnClose.TabIndex = 8;
            btnClose.DialogResult = DialogResult.Cancel;
            btnClose.CausesValidation = false;
            btnClose.Click += (sender, e) => Close();

            lblStatus = new Label
            {
                Name = "databaseStatus",
                AutoSize = false,
                ForeColor = Color.DimGray,
                Location = new Point(30, 312),
                Size = new Size(700, 62),
                TextAlign = leftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight,
                Text = Present("اختر الخادم وقاعدة البيانات، ثم اختبر الاتصال واحفظ الإعدادات. الحفظ يمنع الرجوع إلى قاعدة افتراضية عند التشغيل التالي.")
            };

            Controls.Add(lblTitle);
            Controls.Add(lblServer);
            Controls.Add(txtServer);
            Controls.Add(chkWindowsAuth);
            Controls.Add(lblDatabase);
            Controls.Add(cmbDatabase);
            Controls.Add(lblTheme);
            Controls.Add(cmbTheme);
            Controls.Add(btnFindServers);
            Controls.Add(btnLoadDatabases);
            Controls.Add(btnTest);
            Controls.Add(btnSave);
            Controls.Add(btnClose);
            Controls.Add(lblStatus);

            InitializeAppearanceControls();
            settingsHeader = new Panel { Name = "databaseSettingsHeader", Tag = "UiSurface",
                Dock = DockStyle.Top, Height = 92, BackColor = UiTheme.Background };
            settingsBody = new AntdUI.Panel { Name = "databaseSettingsBody", Dock = DockStyle.Fill,
                AutoScroll = true, BackColor = UiTheme.Background };
            settingsFooter = new Panel { Name = "databaseSettingsFooter", Dock = DockStyle.Bottom,
                Tag = "UiSurface", BackColor = UiTheme.Background };
            settingsHeader.Controls.Add(lblTitle);
            settingsHeader.Controls.Add(lblSubtitle);
            foreach (Control control in new Control[] { lblServer, txtServer, chkWindowsAuth,
                lblDatabase, cmbDatabase, lblTheme, cmbTheme, lblFont, cmbFont, btnSaveAppearance, lblStatus })
                settingsBody.Controls.Add(control);
            foreach (Control control in new Control[] { btnSave, btnTest, btnLoadDatabases, btnFindServers, btnClose })
                settingsFooter.Controls.Add(control);
            lblStatus.TextChanged += (sender, args) => LayoutSettingsSurface();
            Controls.Add(settingsBody);
            Controls.Add(settingsFooter);
            Controls.Add(settingsHeader);
            settingsBody.Resize += (sender, args) => LayoutSettingsSurface();
            Resize += (sender, args) => LayoutSettingsSurface();
            LayoutSettingsSurface();

            // يسمح Enter بالحفظ وEsc بالخروج بشكل متوقع حتى عند استخدام النافذة
            // من لوحة المفاتيح أو قارئ الشاشة.
            AcceptButton = btnSave;
            CancelButton = btnClose;
            KeyPreview = true;
            txtServer.AccessibleName = Present("خادم SQL");
            txtServer.AccessibleDescription = Present("اكتب اسم خادم SQL أو اختره من المقترحات.");
            cmbDatabase.AccessibleName = Present("قاعدة البيانات");
            cmbDatabase.AccessibleDescription = Present("اختر قاعدة موجودة أو اكتب اسم قاعدة العمل الصحيحة.");
            chkWindowsAuth.AccessibleName = Present("مصادقة Windows");
            chkWindowsAuth.AccessibleDescription = Present("هذا الإصدار يستخدم مصادقة Windows للاتصال بقاعدة البيانات.");
            cmbTheme.AccessibleName = Present("مظهر التطبيق");
            cmbTheme.AccessibleDescription = Present("اختر المظهر الفاتح أو الداكن أو حسب النظام.");
            btnFindServers.AccessibleName = Present("البحث عن خوادم SQL");
            btnLoadDatabases.AccessibleName = Present("تحميل قواعد البيانات");
            btnTest.AccessibleName = Present("اختبار اتصال قاعدة البيانات");
            btnTest.AccessibleDescription = Present("يختبر الاتصال من دون حفظ الإعدادات أو تهيئة الجداول.");
            btnSave.AccessibleName = Present("حفظ اتصال قاعدة البيانات");
            btnSave.AccessibleDescription = Present("يحفظ اتصال التشغيل بعد تأكيد قاعدة البيانات.");
            btnClose.AccessibleName = Present("إغلاق إعدادات قاعدة البيانات");
            lblStatus.AccessibleRole = AccessibleRole.StatusBar;
            lblStatus.AccessibleName = Present("حالة إعداد الاتصال");
            lblStatus.TabStop = false;

            if (previewMode)
            {
                txtServer.Text = ".";
                cmbDatabase.Text = "PreviewDb";
                cmbTheme.SelectedIndex = 0;
                lblStatus.Text = Present("معاينة الواجهة فقط: الاتصال والحفظ معطّلان.");
                foreach (AntdUI.Button action in new[] { btnFindServers, btnLoadDatabases, btnTest, btnSave, btnSaveAppearance })
                    action.Enabled = false;
            }
            else LoadCurrentSetings();
            if (!previewMode && ConnectionSettingsService.IsConnectionProfileFixed)
            {
                SetBusy(false);
                AcceptButton = null;
                lblStatus.Text = Present("هذه الحزمة مرتبطة بقاعدة عرض ثابتة. يمكنك اختبار اتصالها، لكن تغيير الخادم أو القاعدة والحفظ معطّل.");
                btnSave.AccessibleDescription = lblStatus.Text;
            }
            UiTheme.Apply(this);
            UiDirection.Apply(this, leftToRight);
            // The isolated fixture must not display the workstation's real connection profile.
            if (previewMode)
            {
                Control runtimeStatus = Controls["__UiStatusStrip"];
                if (runtimeStatus != null) runtimeStatus.Dispose();
            }
            StyleSettingsSurface();
            ApplyDisplayLanguage();
            txtServer.RightToLeft = RightToLeft.No;
            cmbDatabase.RightToLeft = RightToLeft.No;
            cmbFont.RightToLeft = RightToLeft.No;
            DesktopComponents.AddComboBoxDropdown(txtServer, "databaseServerDropdown", Present("فتح خوادم SQL المقترحة"), 2);
            DesktopComponents.AddComboBoxDropdown(cmbDatabase, "databaseCatalogDropdown", Present("فتح قواعد البيانات"), 2);
            DesktopComponents.AddComboBoxDropdown(cmbTheme, "databaseThemeDropdown", Present("فتح خيارات المظهر"), 2);
            DesktopComponents.AddComboBoxDropdown(cmbFont, "databaseFontDropdown", Present("فتح الخطوط المتاحة"), 2);
            LayoutSettingsSurface();
        }

        // يعيد تدفق الإعدادات بدلاً من الاحتفاظ بإحداثيات عريضة ثابتة.
        // في الشاشات الصغيرة تتكدس التسمية والحقل رأسياً ويصبح التمرير رأسياً فقط.
        private void LayoutSettingsSurface()
        {
            if (lblTitle == null || txtServer == null || settingsBody == null || IsDisposed || layoutInProgress)
            {
                return;
            }
            layoutInProgress = true;
            try
            {
            int width = Math.Max(320, settingsBody.ClientSize.Width - SystemInformation.VerticalScrollBarWidth);
            // تبقى إجراءات الاتصال ظاهرة في تذييل ثابت، ويتحرك محتوى الحقول وحده.
            bool stacked = width < 720;
            int margin = stacked ? 20 : 30;
            int fieldWidth = Math.Max(220, width - (stacked ? margin * 2 : 240));
            int fieldLeft = stacked ? margin : 190;
            int labelLeft = stacked ? margin : 30;
            int labelWidth = stacked ? fieldWidth : 145;
            if (!stacked && !leftToRight)
            {
                fieldLeft = margin;
                labelLeft = width - margin - labelWidth;
            }
            int statusTop;

            int headerWidth = Math.Max(1, settingsHeader.ClientSize.Width - margin * 2);
            int titleHeight = TextRenderer.MeasureText(lblTitle.Text, lblTitle.Font, new Size(headerWidth, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height + 4;
            int subtitleHeight = Math.Max(26, TextRenderer.MeasureText(lblSubtitle.Text, lblSubtitle.Font, new Size(headerWidth, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height + 4);
            lblTitle.SetBounds(margin, 12, headerWidth, titleHeight);
            lblSubtitle.SetBounds(margin, lblTitle.Bottom + 4, headerWidth, subtitleHeight);
            settingsHeader.Height = lblSubtitle.Bottom + 10;
            lblTitle.AccessibleName = lblTitle.Text;
            lblSubtitle.AccessibleName = lblSubtitle.Text;
            lblTitle.TextAlign = leftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
            lblSubtitle.TextAlign = leftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;

            if (stacked)
            {
                lblServer.Bounds = new Rectangle(labelLeft, 16, labelWidth, 24);
                txtServer.Bounds = new Rectangle(fieldLeft, 42, fieldWidth, 44);
                chkWindowsAuth.Location = new Point(leftToRight ? fieldLeft : Math.Max(fieldLeft, fieldLeft + fieldWidth - chkWindowsAuth.Width), 96);
                lblDatabase.Bounds = new Rectangle(labelLeft, 128, labelWidth, 24);
                cmbDatabase.Bounds = new Rectangle(fieldLeft, 154, fieldWidth, 44);
                lblTheme.Bounds = new Rectangle(labelLeft, 208, labelWidth, 24);
                cmbTheme.Bounds = new Rectangle(fieldLeft, 234, fieldWidth, 44);

                lblFont.Bounds = new Rectangle(labelLeft, 288, labelWidth, 24);
                cmbFont.Bounds = new Rectangle(fieldLeft, 314, fieldWidth, 44);
                btnSaveAppearance.Bounds = new Rectangle(margin, 372, fieldWidth, 44);
                statusTop = 428;
                settingsFooter.Height = 112;
                int actionWidth = (settingsFooter.ClientSize.Width - margin * 2 - 8) / 2;
                int tripleWidth = (settingsFooter.ClientSize.Width - margin * 2 - 16) / 3;
                int actionX = leftToRight ? margin : settingsFooter.ClientSize.Width - margin;
                foreach (AntdUI.Button action in new[] { btnSave, btnTest, btnClose })
                {
                    if (!leftToRight) actionX -= tripleWidth;
                    action.SetBounds(actionX, 8, tripleWidth, 44);
                    actionX += leftToRight ? tripleWidth + 8 : -8;
                }
                PlaceFooterPair(btnLoadDatabases, btnFindServers, margin, 60, actionWidth);
            }
            else
            {
                lblServer.Bounds = new Rectangle(labelLeft, 16, labelWidth, 44);
                txtServer.Bounds = new Rectangle(fieldLeft, 16, fieldWidth, 44);
                chkWindowsAuth.Location = new Point(leftToRight ? fieldLeft : Math.Max(fieldLeft, fieldLeft + fieldWidth - chkWindowsAuth.Width), 70);
                lblDatabase.Bounds = new Rectangle(labelLeft, 108, labelWidth, 44);
                cmbDatabase.Bounds = new Rectangle(fieldLeft, 108, fieldWidth, 44);
                lblTheme.Bounds = new Rectangle(labelLeft, 160, labelWidth, 44);
                cmbTheme.Bounds = new Rectangle(fieldLeft, 160, fieldWidth, 44);

                lblFont.Bounds = new Rectangle(labelLeft, 212, labelWidth, 44);
                cmbFont.Bounds = new Rectangle(fieldLeft, 212, fieldWidth, 44);
                btnSaveAppearance.Bounds = new Rectangle(fieldLeft, 268, fieldWidth, 44);
                statusTop = 324;
                settingsFooter.Height = 62;
                AntdUI.Button[] actions = { btnSave, btnTest, btnLoadDatabases, btnFindServers, btnClose };
                int[] widths = { 116, 116, 168, 116, 116 };
                int x = leftToRight ? margin : settingsFooter.ClientSize.Width - margin;
                for (int index = 0; index < actions.Length; index++)
                {
                    int actionWidth = widths[index];
                    if (!leftToRight) x -= actionWidth;
                    actions[index].SetBounds(x, 9, actionWidth, 44);
                    x += leftToRight ? actionWidth + 8 : -8;
                }
            }

            int statusWidth = Math.Max(220, width - margin * 2);
            int statusHeight = Math.Max(64, TextRenderer.MeasureText(lblStatus.Text, lblStatus.Font, new Size(statusWidth, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height + 4);
            lblStatus.Bounds = new Rectangle(margin, statusTop, statusWidth, statusHeight);
            lblStatus.AccessibleName = lblStatus.Text;
            StyleSettingsSurface();
            settingsBody.PerformLayout();
            }
            finally { layoutInProgress = false; }
        }

        private void PlaceFooterPair(AntdUI.Button leading, AntdUI.Button trailing, int margin, int top, int width)
        {
            int right = settingsFooter.ClientSize.Width - margin - width;
            leading.SetBounds(leftToRight ? margin : right, top, width, 44);
            trailing.SetBounds(leftToRight ? right : margin, top, width, 44);
        }

        private void StyleSettingsSurface()
        {
            settingsBody.BackColor = UiTheme.Background;
            AntdThemeAdapter.Apply(settingsBody);
            settingsHeader.BackColor = UiTheme.Background;
            settingsFooter.BackColor = UiTheme.Background;
            lblTitle.BackColor = UiTheme.Background;
            lblTitle.ForeColor = UiTheme.Text;
            lblSubtitle.BackColor = UiTheme.Background;
            lblSubtitle.ForeColor = UiTheme.MutedText;
            lblStatus.ForeColor = UiTheme.MutedText;
            foreach (AntdUI.Button button in new[] { btnSaveAppearance, btnSave, btnTest, btnLoadDatabases, btnFindServers, btnClose })
            {
                DesktopComponents.StyleOutlinedFooterAction(button);
                button.IconPosition = leftToRight ? AntdUI.TAlignMini.Left : AntdUI.TAlignMini.Right;
                button.IconSize = new Size(18, 18);
            }
        }

        private string Present(string arabic)
        {
            return leftToRight ? UiLanguageCatalog.Translate(arabic) : arabic;
        }

        internal string PresentConnectionHelp(Exception exception)
        {
            return string.Join("\n", ConnectionSettingsService.BuildConnectionHelp(exception).Split('\n').Select(Present));
        }

        // UiTheme follows the persisted application language. Preview windows may deliberately
        // render the opposite language, so restore this form's requested language after theming.
        private void ApplyDisplayLanguage()
        {
            Text = Present("إعدادات اتصال قاعدة البيانات");
            lblTitle.Text = Present("إعدادات الاتصال");
            lblSubtitle.Text = Present(!previewMode && ConnectionSettingsService.IsConnectionProfileFixed
                ? "اتصال ثابت لهذه الحزمة؛ الاختبار متاح وتغيير الاتصال معطّل."
                : "اختر الخادم وقاعدة التشغيل، ثم اختبر الاتصال قبل الحفظ.");
            lblServer.Text = Present("خادم SQL");
            chkWindowsAuth.Text = Present("مصادقة Windows (المدعومة حالياً)");
            lblDatabase.Text = Present("قاعدة البيانات");
            lblTheme.Text = Present("المظهر");
            lblFont.Text = Present("خط الواجهة");
            btnFindServers.Text = Present("بحث");
            btnLoadDatabases.Text = Present("تحميل قواعد البيانات");
            btnTest.Text = Present("اختبار");
            btnSave.Text = Present("حفظ");
            btnClose.Text = Present("إغلاق");
            btnSaveAppearance.Text = Present("حفظ المظهر");
        }

        private void SaveAppearance_Click(object sender, EventArgs e)
        {
            if (previewMode) return;
            try
            {
                string mode = cmbTheme.SelectedIndex == 1 ? "Dark" : cmbTheme.SelectedIndex == 2 ? "System" : "Light";
                UiTheme.SaveAppearance(mode, cmbFont.Text);
                UiTheme.Apply(this);
                UiDirection.Apply(this, leftToRight);
                StyleSettingsSurface();
                ApplyDisplayLanguage();
                txtServer.RightToLeft = RightToLeft.No;
                cmbDatabase.RightToLeft = RightToLeft.No;
                cmbFont.RightToLeft = RightToLeft.No;
                using (Font effective = AppFonts.Create(9F))
                    lblStatus.Text = string.Format(Present("تم حفظ المظهر. الخط المستخدم: {0}. إذا لم يتوفر الخط المختار، يُستخدم البديل تلقائياً."), effective.FontFamily.Name);
                LayoutSettingsSurface();
            }
            catch (Exception exception)
            {
                lblStatus.Text = Present("تعذر حفظ المظهر. احتُفظ بالإعداد السابق.");
                UiTheme.ShowOperationIssue(this, Present("حفظ المظهر"), exception, leftToRight);
            }
        }
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (previewMode) return;

            FitOpeningToWorkingArea();

            if (LocalAdministratorAccess.IsCurrentUserAdministrator())
            {
                return;
            }

            AppNotifications.Show(
                this,
                Present("إعدادات قاعدة البيانات محمية. شغّل التطبيق بحساب Windows محلي مسؤول للوصول إليها."),
                Text,
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            Close();
        }

        internal void FitOpeningToWorkingArea()
        {
            // Setting ClientSize while maximized replaces WinForms' normal
            // restore bounds. Windows already fits maximized/minimized windows.
            if (WindowState != FormWindowState.Normal) return;
            // قد تكون النافذة انتقلت إلى شاشة ثانوية أصغر من الشاشة الأساسية.
            Rectangle workingArea = Screen.FromControl(this).WorkingArea;
            Size safeClientSize = new Size(
                Math.Min(ClientSize.Width, Math.Max(320, workingArea.Width - 48)),
                Math.Min(ClientSize.Height, Math.Max(260, workingArea.Height - 48)));
            if (safeClientSize != ClientSize)
            {
                ClientSize = safeClientSize;
                LayoutSettingsSurface();
            }
        }

        private Label CreateSettingsLabel(string text, int top, int left, int width)
        {
            return new Label
            {
                Text = text,
                AutoSize = false,
                Location = new Point(left, top),
                Size = new Size(width, 28),
                TextAlign = leftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight
            };
        }

        // يقرأ الإعدادات التشغيلية الثابتة؛ لا يعاد ضبطها عند بناء نسخة جديدة.
        private void LoadCurrentSetings()
        {
            SqlConnectionStringBuilder builder = ConnectionSettingsService.LoadSettings();

            txtServer.Text = string.IsNullOrWhiteSpace(builder.DataSource)
                ? "."
                : builder.DataSource;
            cmbDatabase.Text = string.IsNullOrWhiteSpace(builder.InitialCatalog)
                ? "PharmacyManagementDb"
                : builder.InitialCatalog;
            chkWindowsAuth.Checked = builder.IntegratedSecurity;
            cmbTheme.Text = ToThemeDisplay(UiTheme.LoadMode());

            lblStatus.Text = string.Format(Present("الاتصال النشط: {0} على {1}. اختبره ثم احفظه قبل الاستخدام."), builder.InitialCatalog, builder.DataSource);
        }

        // يعيد تعبئة قائمة السيرفرات بالمقترحات والسيرفرات المكتشفة.
        private void LoadServerSuggestions(bool includeDiscovery = false)
        {
            string selectedServer = txtServer.Text;
            txtServer.Items.Clear();

            foreach (string server in ConnectionSettingsService.GetSuggestedServers(includeDiscovery))
            {
                txtServer.Items.Add(server);
            }

            if (!string.IsNullOrWhiteSpace(selectedServer))
            {
                txtServer.Text = selectedServer;
            }
        }

        // يبحث عن سيرفرات SQL المتاحة ويضيفها للقائمة.
        private async void FindServers_Click(object sender, EventArgs e)
        {
            if (previewMode) return;
            try
            {
                SetBusy(true, Present("جار البحث عن خوادم SQL Server..."));
                string selectedServer = txtServer.Text;
                string[] servers = await Task.Run(() => ConnectionSettingsService.GetSuggestedServers(includeDiscovery: true));

                if (IsDisposed)
                {
                    return;
                }

                txtServer.Items.Clear();
                foreach (string server in servers)
                {
                    txtServer.Items.Add(server);
                }

                if (!string.IsNullOrWhiteSpace(selectedServer))
                {
                    txtServer.Text = selectedServer;
                }

                lblStatus.Text = Present("تم تحديث قائمة الخوادم. يمكنك تجربة '.', أو '.\\SQLEXPRESS'، أو اختيار خادم مكتشف.");
            }
            catch (Exception ex)
            {
                if (IsDisposed)
                {
                    return;
                }

                lblStatus.Text = Present("تعذر البحث عن خوادم SQL Server.");
                AppNotifications.Show(this, lblStatus.Text + "\n\n" + PresentConnectionHelp(ex), Present("إعدادات قاعدة البيانات"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                SetBusy(false);
            }
        }

        // يحمل أسماء قواعد البيانات الموجودة على السيرفر المختار.
        private async void LoadDatabases_Click(object sender, EventArgs e)
        {
            if (previewMode) return;
            if (!ValidateInputs(requireDatabase: false))
            {
                return;
            }

            try
            {
                SetBusy(true, Present("جار تحميل قواعد البيانات..."));
                string serverName = txtServer.Text.Trim();
                bool integratedSecurity = chkWindowsAuth.Checked;
                DataTable databases = await Task.Run(() => ConnectionSettingsService.GetDatabases(serverName, integratedSecurity));
                if (IsDisposed)
                {
                    return;
                }

                cmbDatabase.Items.Clear();

                foreach (DataRow row in databases.Rows)
                {
                    cmbDatabase.Items.Add(row["name"].ToString());
                }

                lblStatus.Text = cmbDatabase.Items.Count == 0
                    ? Present("لم يتم العثور على قواعد بيانات للمستخدم. يمكنك كتابة اسم قاعدة جديدة.")
                    : Present("تم تحميل قواعد البيانات. اختر قاعدة موجودة أو اكتب اسماً جديداً.");
            }
            catch (Exception ex)
            {
                if (IsDisposed)
                {
                    return;
                }

                lblStatus.Text = Present("تعذر تحميل قواعد البيانات.");
                AppNotifications.Show(this, lblStatus.Text + "\n\n" + PresentConnectionHelp(ex), Present("إعدادات قاعدة البيانات"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                SetBusy(false);
            }
        }

        // يختبر الاتصال المؤقت وينشئ الجداول إن لزم بدون حفظ الإعدادات.
        private async void TestConnection_Click(object sender, EventArgs e)
        {
            if (previewMode) return;
            if (!ValidateInputs(requireDatabase: true))
            {
                return;
            }

            try
            {
                SetBusy(true, Present("جار اختبار الاتصال..."));
                string serverName = txtServer.Text.Trim();
                string databaseName = cmbDatabase.Text.Trim();
                bool integratedSecurity = chkWindowsAuth.Checked;
                await Task.Run(() => ConnectionSettingsService.TestSettings(serverName, databaseName, integratedSecurity));

                if (IsDisposed)
                {
                    return;
                }

                lblStatus.Text = Present("نجح الاتصال. لم يتم حفظ الإعدادات أو تهيئة الجداول بعد.");
            }
            catch (Exception ex)
            {
                if (IsDisposed)
                {
                    return;
                }

                lblStatus.Text = Present("فشل اختبار الاتصال.");
                AppNotifications.Show(this, lblStatus.Text + "\n\n" + PresentConnectionHelp(ex), Present("إعدادات قاعدة البيانات"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                SetBusy(false);
            }
        }

        // يحفظ إعدادات الاتصال بعد التحقق من صلاحيتها وجاهزية القاعدة.
        private async void Save_Click(object sender, EventArgs e)
        {
            if (previewMode) return;
            if (ConnectionSettingsService.IsConnectionProfileFixed)
            {
                lblStatus.Text = Present("هذه الحزمة مرتبطة بقاعدة عرض ثابتة. يمكنك اختبار اتصالها، لكن تغيير الخادم أو القاعدة والحفظ معطّل.");
                return;
            }
            if (!ValidateInputs(requireDatabase: true))
            {
                return;
            }

            string requestedServer = txtServer.Text.Trim();
            string requestedDatabase = cmbDatabase.Text.Trim();
            DatabaseConnectionProfile previousProfile = ConnectionSettingsService.GetConnectionProfile();
            DatabaseInspectionResult inspection = null;

            try
            {
                SetBusy(true, Present("جار التحقق من قاعدة التشغيل دون تعديلها..."));
                inspection = await Task.Run(() => ConnectionSettingsService.InspectSettings(requestedServer, requestedDatabase, chkWindowsAuth.Checked));
                if (IsDisposed)
                {
                    return;
                }

                SetBusy(false);
                if (!ConfirmConnectionChange(requestedServer, requestedDatabase, inspection))
                {
                    lblStatus.Text = Present("لم يتم تغيير اتصال التشغيل.");
                    return;
                }

                SetBusy(true, Present("جار حفظ الإعدادات..."), true);
                string serverName = txtServer.Text.Trim();
                string databaseName = cmbDatabase.Text.Trim();
                bool integratedSecurity = chkWindowsAuth.Checked;
                string themeMode = ToThemeMode(cmbTheme.Text);
                bool needsManagerBootstrap = await Task.Run(() =>
                {
                    ConnectionSettingsService.PrepareSettings(serverName, databaseName, integratedSecurity);
                    ConnectionSettingsService.SaveSettings(serverName, databaseName, integratedSecurity);
                    AppDiagnostics.LogSafeEvent(
                        "RuntimeDatabaseProfileChanged",
                        "PreviousDatabase=" + (previousProfile.DatabaseName ?? string.Empty)
                            + "; NewDatabase=" + databaseName
                            + "; DatabaseExisted=" + inspection.DatabaseExists
                            + "; KnownCoreTables=" + inspection.ExistingCoreTableCount
                            + "; Source=SavedProfile");
                    return !ConnectionSettingsService.HasManager();
                });
                if (IsDisposed)
                {
                    return;
                }

                bool themeSaved = true;
                try
                {
                    UiTheme.SaveAppearance(themeMode, cmbFont.Text);
                }
                catch
                {
                    themeSaved = false;
                }

                UiTheme.Apply(this);
                UiDirection.Apply(this, leftToRight);
                StyleSettingsSurface();
                ApplyDisplayLanguage();
                txtServer.RightToLeft = RightToLeft.No;
                cmbDatabase.RightToLeft = RightToLeft.No;
                cmbFont.RightToLeft = RightToLeft.No;

                if (needsManagerBootstrap)
                {
                    using (BootstrapManagerForm bootstrapForm = new BootstrapManagerForm())
                    {
                        if (bootstrapForm.ShowDialog(this) != DialogResult.OK)
                        {
                            lblStatus.Text = Present("تم حفظ الاتصال، لكن يجب إنشاء المدير الأول قبل استخدام النظام.");
                            AppNotifications.Show(
                                this,
                                Present("تم حفظ إعدادات الاتصال. أنشئ المدير الأول قبل تسجيل الدخول."),
                                Present("إعدادات قاعدة البيانات"),
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Warning);
                            return;
                        }
                    }
                }

                if (themeSaved)
                {
                    DatabaseConnectionProfile profile = ConnectionSettingsService.GetConnectionProfile();
                    lblStatus.Text = Present("تم حفظ إعدادات قاعدة البيانات في ملف التشغيل الدائم.");
                    AppNotifications.Show(this, Present("تم حفظ إعدادات قاعدة البيانات بنجاح."), Present("إعدادات قاعدة البيانات"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    lblStatus.Text = Present("تم حفظ اتصال قاعدة البيانات، لكن تعذر حفظ إعداد المظهر.");
                    AppNotifications.Show(this, Present("تم حفظ اتصال قاعدة البيانات بنجاح، لكن تعذر حفظ إعداد المظهر."), Present("إعدادات قاعدة البيانات"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                if (IsDisposed)
                {
                    return;
                }

                lblStatus.Text = Present("تعذر حفظ الإعدادات.");
                string supportCode = AppDiagnostics.Log("DatabaseSettingsSave", ex);
                AppNotifications.Show(this, lblStatus.Text + "\n\n" + PresentConnectionHelp(ex) + "\n\n" + Present("رمز الدعم: ") + supportCode, Present("إعدادات قاعدة البيانات"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                SetBusy(false);
            }
        }

        // يتحقق من إدخال اسم السيرفر واسم القاعدة عندما يكون مطلوبًا.
        private bool ValidateInputs(bool requireDatabase)
        {
            if (string.IsNullOrWhiteSpace(txtServer.Text))
            {
                AppNotifications.Show(this, Present("الرجاء إدخال اسم خادم SQL."), Present("إعدادات قاعدة البيانات"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                txtServer.Focus();
                return false;
            }

            if (requireDatabase && string.IsNullOrWhiteSpace(cmbDatabase.Text))
            {
                AppNotifications.Show(this, Present("الرجاء اختيار أو كتابة اسم قاعدة البيانات."), Present("إعدادات قاعدة البيانات"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                cmbDatabase.Focus();
                return false;
            }

            if (!chkWindowsAuth.Checked)
            {
                AppNotifications.Show(this, Present("هذه النسخة تدعم مصادقة Windows فقط. فعّل الخيار ثم أعد المحاولة."), Present("إعدادات قاعدة البيانات"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                chkWindowsAuth.Checked = true;
                return false;
            }

            return true;
        }

        // يضبط حالة الأزرار والمؤشر أثناء العمليات الطويلة نسبيًا.
        private void SetBusy(bool busy, string status = null, bool blockCloseForWrite = false)
        {
            if (IsDisposed)
            {
                return;
            }

            bool connectionEditable = !ConnectionSettingsService.IsConnectionProfileFixed;
            btnFindServers.Enabled = !busy && !previewMode && connectionEditable;
            btnLoadDatabases.Enabled = !busy && !previewMode && connectionEditable;
            btnTest.Enabled = !busy && !previewMode;
            btnSave.Enabled = !busy && !previewMode && connectionEditable;
            btnClose.Enabled = !busy || !blockCloseForWrite;
            txtServer.Enabled = !busy && (previewMode || connectionEditable);
            cmbDatabase.Enabled = !busy && (previewMode || connectionEditable);
            chkWindowsAuth.Enabled = !busy && (previewMode || connectionEditable);
            cmbTheme.Enabled = !busy;
            cmbFont.Enabled = !busy;
            btnSaveAppearance.Enabled = !busy && !previewMode;
            isBusy = busy;
            closeBlockedForWrite = busy && blockCloseForWrite;
            Cursor = busy ? Cursors.WaitCursor : Cursors.Default;

            if (!string.IsNullOrWhiteSpace(status))
            {
                lblStatus.Text = Present(status);
            }
        }

        private bool ConfirmConnectionChange(string serverName, string databaseName, DatabaseInspectionResult inspection)
        {
            if (inspection == null)
            {
                return false;
            }

            if (!inspection.DatabaseExists)
            {
                return ConfirmNewDatabaseCreation(serverName, databaseName);
            }

            DialogResult confirmation = AppNotifications.Show(
                this,
                BuildConnectionChangeMessage(serverName, databaseName, inspection.ExistingCoreTableCount),
                Present("تأكيد تغيير قاعدة التشغيل"),
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            return confirmation == DialogResult.Yes;
        }

        internal string BuildConnectionChangeMessage(string serverName, string databaseName, int coreTableCount)
        {
            return string.Format(Present("سيصبح الاتصال التالي هو اتصال التشغيل:\n\nالخادم: {0}\nقاعدة البيانات: {1}\nحالة الفحص: قاعدة موجودة، والجداول الأساسية المكتشفة: {2}/4\n\nلا يؤدي تغيير الاتصال إلى حذف بيانات القاعدة السابقة. ستبقى البيانات هناك، لكن التطبيق سيعرض بيانات القاعدة الجديدة فقط."),
                serverName, databaseName, coreTableCount);
        }

        private bool ConfirmNewDatabaseCreation(string serverName, string databaseName)
        {
            using (Form confirmation = CreateNewDatabaseConfirmation(serverName, databaseName))
                return confirmation.ShowDialog(this) == DialogResult.OK;
        }

        // Separate construction lets the preview gate inspect the real confirmation without touching SQL.
        internal Form CreateNewDatabaseConfirmation(string serverName, string databaseName)
        {
            Rectangle workingArea = Screen.FromControl(this).WorkingArea;
            var confirmation = new Form
            {
                Name = "databaseCreationConfirmation",
                Text = Present("تأكيد إنشاء قاعدة جديدة"),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.Sizable,
                MinimumSize = new Size(336, 340),
                MaximizeBox = false,
                MinimizeBox = false,
                ShowInTaskbar = false,
                AutoScaleMode = AutoScaleMode.Dpi,
                RightToLeft = leftToRight ? RightToLeft.No : RightToLeft.Yes,
                RightToLeftLayout = !leftToRight,
                BackColor = UiTheme.Background,
                Font = AppFonts.Create(9.5F)
            };
            confirmation.ClientSize = new Size(
                Math.Max(320, Math.Min(560, workingArea.Width - 48)),
                Math.Max(300, Math.Min(380, workingArea.Height - 48)));
            var viewport = new System.Windows.Forms.Panel { Name = "databaseCreationViewport", Dock = DockStyle.Fill,
                AutoScroll = true, RightToLeft = RightToLeft.No, BackColor = UiTheme.Background };
            var body = new AntdUI.Panel { Name = "databaseCreationBody", RightToLeft = RightToLeft.No,
                Back = UiTheme.Background, BackColor = UiTheme.Background };
            var footer = new Panel { Name = "databaseCreationFooter", Dock = DockStyle.Bottom,
                BackColor = UiTheme.Background };
            var title = new Label { Name = "databaseCreationTitle", AutoSize = false,
                Text = Present("سيتم إنشاء قاعدة تشغيل فارغة"), Font = AppFonts.Create(12F, FontStyle.Bold),
                ForeColor = UiTheme.Warning, BackColor = UiTheme.Background,
                RightToLeft = leftToRight ? RightToLeft.No : RightToLeft.Yes,
                TextAlign = leftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight };
            var message = new Label { Name = "databaseCreationMessage", AutoSize = false,
                Text = Present("الخادم: ") + serverName + "\n" + Present("قاعدة جديدة: ") + databaseName
                    + "\n" + Present("لن تُنقل بيانات أي قاعدة سابقة تلقائياً."),
                ForeColor = UiTheme.Text, BackColor = UiTheme.Background,
                RightToLeft = leftToRight ? RightToLeft.No : RightToLeft.Yes,
                TextAlign = leftToRight ? ContentAlignment.TopLeft : ContentAlignment.TopRight };
            var instruction = new Label { Name = "databaseCreationInstruction", AutoSize = false,
                Text = Present("للتأكيد، اكتب اسم القاعدة كما هو:"), ForeColor = UiTheme.MutedText,
                BackColor = UiTheme.Background, RightToLeft = leftToRight ? RightToLeft.No : RightToLeft.Yes,
                TextAlign = leftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight };
            var nameInput = DesktopComponents.Input("databaseCreationName", Present("اسم قاعدة البيانات للتأكيد"));
            nameInput.RightToLeft = RightToLeft.No;
            nameInput.TextAlign = HorizontalAlignment.Left;
            var confirmButton = DesktopComponents.Action("databaseCreationConfirm", Present("إنشاء القاعدة"), AppIcon.Confirm, true);
            var cancelButton = DesktopComponents.Action("databaseCreationCancel", Present("إلغاء"), AppIcon.Close);
            DesktopComponents.StyleOutlinedFooterAction(cancelButton);
            confirmButton.Enabled = false;
            confirmButton.Click += (sender, args) => confirmation.DialogResult = DialogResult.OK;
            cancelButton.Click += (sender, args) => confirmation.DialogResult = DialogResult.Cancel;
            nameInput.TextChanged += (sender, args) =>
                confirmButton.Enabled = string.Equals(nameInput.Text.Trim(), databaseName, StringComparison.OrdinalIgnoreCase);
            foreach (AntdUI.Button action in new[] { confirmButton, cancelButton })
                action.IconPosition = leftToRight ? AntdUI.TAlignMini.Left : AntdUI.TAlignMini.Right;
            body.Controls.Add(title);
            body.Controls.Add(message);
            body.Controls.Add(instruction);
            body.Controls.Add(nameInput);
            footer.Controls.Add(confirmButton);
            footer.Controls.Add(cancelButton);
            viewport.Controls.Add(body);
            confirmation.Controls.Add(viewport);
            confirmation.Controls.Add(footer);
            confirmation.AcceptButton = confirmButton;
            confirmation.CancelButton = cancelButton;
            bool arranging = false;
            Action arrange = () =>
            {
                if (arranging || confirmation.IsDisposed) return;
                arranging = true;
                try
                {
                    int footerWidth = footer.ClientSize.Width;
                    bool stacked = footerWidth < 440;
                    footer.Height = stacked ? 100 : 68;
                    int actionWidth = stacked ? Math.Max(1, footerWidth - 32) : Math.Min(228, (footerWidth - 48) / 2);
                    if (stacked)
                    {
                        confirmButton.Bounds = new Rectangle(16, 4, actionWidth, 44);
                        cancelButton.Bounds = new Rectangle(16, 52, actionWidth, 44);
                    }
                    else
                    {
                        int first = leftToRight ? 16 : footerWidth - 16 - actionWidth;
                        int second = leftToRight ? footerWidth - 16 - actionWidth : 16;
                        confirmButton.Bounds = new Rectangle(first, 12, actionWidth, 44);
                        cancelButton.Bounds = new Rectangle(second, 12, actionWidth, 44);
                    }
                    // Reset the old virtual width before shrinking from the initial desktop size.
                    viewport.AutoScroll = false;
                    viewport.AutoScrollMinSize = Size.Empty;
                    body.SetBounds(0, 0, Math.Max(1, viewport.ClientSize.Width - SystemInformation.VerticalScrollBarWidth), body.Height);
                    int width = Math.Max(1, body.ClientSize.Width - 48);
                    int x = 24;
                    int y = 12;
                    title.Bounds = new Rectangle(x, y, width, Math.Max(36,
                        TextRenderer.MeasureText(title.Text, title.Font, new Size(width, int.MaxValue), TextFormatFlags.WordBreak).Height));
                    y = title.Bottom + 6;
                    message.Bounds = new Rectangle(x, y, width, Math.Max(66,
                        TextRenderer.MeasureText(message.Text, message.Font, new Size(width, int.MaxValue), TextFormatFlags.WordBreak).Height + 4));
                    y = message.Bottom + 6;
                    instruction.Bounds = new Rectangle(x, y, width, Math.Max(28,
                        TextRenderer.MeasureText(instruction.Text, instruction.Font, new Size(width, int.MaxValue), TextFormatFlags.WordBreak).Height));
                    y = instruction.Bottom + 6;
                    nameInput.Bounds = new Rectangle(x, y, width, 44);
                    body.Height = nameInput.Bottom + 16;
                    viewport.AutoScroll = true;
                    viewport.AutoScrollMinSize = new Size(1, body.Bottom);
                    viewport.PerformLayout();
                }
                finally { arranging = false; }
            };
            footer.Resize += (sender, args) => arrange();
            viewport.Resize += (sender, args) => arrange();
            confirmation.Resize += (sender, args) => arrange();
            confirmation.Shown += (sender, args) => { arrange(); nameInput.Focus(); };
            arrange();
            return confirmation;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (isBusy && closeBlockedForWrite)
            {
                e.Cancel = true;
                lblStatus.Text = Present("انتظر حتى تكتمل العملية الحالية ثم أغلق النافذة.");
                return;
            }

            base.OnFormClosing(e);
        }

        private string ToThemeDisplay(string mode)
        {
            if (string.Equals(mode, "Dark", StringComparison.OrdinalIgnoreCase))
            {
                return Present("داكن");
            }

            if (string.Equals(mode, "System", StringComparison.OrdinalIgnoreCase))
            {
                return Present("حسب النظام");
            }

            return Present("فاتح");
        }

        private static string ToThemeMode(string display)
        {
            if (string.Equals(display, "داكن", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(display, "Dark", StringComparison.OrdinalIgnoreCase))
            {
                return "Dark";
            }

            if (string.Equals(display, "حسب النظام", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(display, "System", StringComparison.OrdinalIgnoreCase))
            {
                return "System";
            }

            return "Light";
        }

    }
}


// شرح الملف: شاشة تسجيل الدخول للنظام.
// تتحقق من المستخدم وتتيح فتح إعدادات قاعدة البيانات بالضغط على F1 قبل الدخول.
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading.Tasks;
using System.Windows.Forms;
using PharmacyManagement.Application;

namespace project_fo_3
{
    // شاشة تسجيل الدخول الرئيسية.
    // تحتوي على اختصار F1 لإعداد الاتصال بقاعدة البيانات قبل الدخول للنظام.
    public partial class Login : Form
    {
        private readonly Size loginWindowSize = new Size(820, 600);
        private Panel heroPanel;
        private Label heroTitle;
        private Label heroSubtitle;
        private Label heroNote;
        private AntdUI.Panel loginCard;
        private Label loginSubtitle;
        private AntdUI.Button databaseSettingsButton;
        private AntdUI.Button passwordVisibilityButton;
        private Label rememberHint;
        private string rememberedUserName = string.Empty;
        private bool isInitializingRememberMe;
        private int consecutiveFailedLoginAttempts;
        private DateTime nextLoginAttemptUtc = DateTime.MinValue;
        private bool arrangingLogin;
        private bool isPasswordVisible;
        private readonly bool previewMode;
        private bool presentationLeftToRight;

        // ينشئ شاشة الدخول ويطبق التصميم الموحد ويدعم اختصار F1.
        public Login() : this(true, false, UiDirection.IsLeftToRight)
        {
        }

        internal Login(bool loadUsernames) : this(loadUsernames, false, UiDirection.IsLeftToRight)
        {
        }

        internal Login(bool loadUsernames, bool previewMode, bool leftToRight)
        {
            this.previewMode = previewMode;
            presentationLeftToRight = leftToRight;
            InitializeComponent();
            UiTheme.Apply(this);

            // كانت واجهة الدخول الحديثة معرفة بالكامل ولكن لا تُستدعى، فيظهر تخطيط المصمم
            // الخام بدلاً من هوية النظام الموحدة عند التشغيل.
            BuildLoginSurface();
            ApplyLoginWindow();
            ApplyLoginVisualPolish();
            Resize += (sender, e) => LayoutLoginSurface();

            KeyPreview = true;
            KeyDown += Login_KeyDown;
            if (!previewMode) InitializeRememberMe();
            else
            {
                EnsureRememberHint();
                username.Text = "مدير Admin";
                password.Text = "admin123";
                chkRememberMe.Checked = true;
            }
            chkRememberMe.CheckedChanged += chkRememberMe_CheckedChanged;
            if (loadUsernames && !previewMode) LoadUsernames();
            ApplyPresentation(leftToRight);
        }

        private void BuildLoginSurface()
        {
            heroPanel = new Panel
            {
                Name = "loginHero",
                BackColor = UiTheme.PrimaryDark
            };
            heroPanel.Paint += HeroPanel_Paint;

            heroTitle = new Label
            {
                Name = "heroTitle",
                AutoSize = false,
                Text = "إدارة الصيدلية",
                TextAlign = ContentAlignment.MiddleRight,
                BackColor = Color.Transparent
            };

            heroSubtitle = new Label
            {
                Name = "heroSubtitle",
                AutoSize = false,
                Text = "دخول آمن وسريع لإدارة المبيعات والمخزون والموظفين.",
                TextAlign = ContentAlignment.TopRight,
                BackColor = Color.Transparent
            };

            heroNote = new Label
            {
                Name = "heroNote",
                AutoSize = false,
                Text = BuildConnectionIdentityText(),
                TextAlign = ContentAlignment.MiddleRight,
                BackColor = Color.Transparent
            };

            heroPanel.Controls.Add(heroTitle);
            heroPanel.Controls.Add(heroSubtitle);
            heroPanel.Controls.Add(heroNote);

            loginCard = new AntdUI.Panel
            {
                Name = "loginCard",
                BackColor = UiTheme.Surface,
                BorderWidth = 1,
                Radius = DesktopDesignTokens.CardRadius
            };

              loginSubtitle = new Label
              {
                Name = "loginSubtitle",
                AutoSize = false,
                  TextAlign = ContentAlignment.MiddleCenter
              };

              databaseSettingsButton = new AccessibleAntdButton
              {
                  Name = "databaseSettingsButton",
                  Text = "إعداد قاعدة البيانات (F1)",
                  Cursor = Cursors.Hand
              };
              databaseSettingsButton.Click += (sender, e) => OpenDatabaseSettings();

              passwordVisibilityButton = new AccessibleAntdButton
              {
                  Name = "passwordVisibilityButton",
                  Text = "إظهار",
                  Cursor = Cursors.Hand
              };
              passwordVisibilityButton.Click += TogglePasswordVisibility;

            Control[] cardControls =
            {
                  pictureBox1,
                  label1,
                  loginSubtitle,
                  databaseSettingsButton,
                  label2,
                username,
                label3,
                password,
                passwordVisibilityButton,
                chkRememberMe,
                button1,
                button3
            };

            foreach (Control control in cardControls)
            {
                if (control.Parent != null)
                {
                    control.Parent.Controls.Remove(control);
                }

                loginCard.Controls.Add(control);
            }

            Controls.Add(heroPanel);
            Controls.Add(loginCard);
            heroPanel.BringToFront();
            loginCard.BringToFront();
        }

        private void ApplyLoginVisualPolish()
        {
            // Start with the requested presentation. Creating the window as RTL and
            // switching its native layout later can leave mirrored glyph rendering behind.
            RightToLeft = presentationLeftToRight ? RightToLeft.No : RightToLeft.Yes;
            RightToLeftLayout = !presentationLeftToRight;
            BackColor = UiTheme.Background;
              MinimumSize = new Size(360, 360);
            MaximumSize = Size.Empty;
            AcceptButton = button1;
            CancelButton = button3;

            heroTitle.Font = AppFonts.Create(22F, FontStyle.Bold);
            heroTitle.ForeColor = Color.White;
            heroSubtitle.Font = AppFonts.Create(10.5F, FontStyle.Regular);
            heroSubtitle.ForeColor = Color.FromArgb(220, 255, 255, 255);
            heroNote.Font = AppFonts.Create(9.5F, FontStyle.Bold);
            heroNote.ForeColor = Color.FromArgb(205, 255, 255, 255);

            label1.Text = Present("تسجيل الدخول");
            label1.AutoSize = false;
            label1.TextAlign = ContentAlignment.MiddleCenter;
            label1.Font = AppFonts.Create(18F, FontStyle.Bold);
            label1.ForeColor = UiTheme.Text;

            loginSubtitle.Text = BuildDatabaseSubtitle(ConnectionSettingsService.GetConnectionProfile());
            loginSubtitle.Font = AppFonts.Create(9.5F, FontStyle.Bold);
            loginSubtitle.ForeColor = UiTheme.MutedText;
            loginSubtitle.AccessibleName = "قاعدة التشغيل الحالية";
            loginSubtitle.AccessibleDescription = "اسم قاعدة البيانات التي سيستخدمها النظام بعد تسجيل الدخول.";
              if (RuntimeSettingsStore.HasUnreadableSavedProfile)
            {
                loginSubtitle.Text = Present("تعذر قراءة إعداد الاتصال — اضغط F1 للإصلاح");
                loginSubtitle.ForeColor = UiTheme.Danger;
              }

              StyleActionButton(databaseSettingsButton, UiTheme.SurfaceVariant, UiTheme.Text, true);
              SetLoginButtonIcon(databaseSettingsButton, AppIcon.Database);
              databaseSettingsButton.Font = AppFonts.Create(9F, FontStyle.Bold);
              databaseSettingsButton.TabIndex = 0;
              databaseSettingsButton.AccessibleName = "إعداد قاعدة البيانات";
              databaseSettingsButton.AccessibleDescription = "يفتح إعدادات اختيار الخادم وقاعدة البيانات. قد يطلب Windows موافقة مسؤول.";

            label2.Text = Present("المستخدم");
            label3.Text = Present("كلمة المرور");
            StyleFieldLabel(label2);
            StyleFieldLabel(label3);

            username.Font = AppFonts.Create(10.5F, FontStyle.Regular);
            username.ForeColor = UiTheme.Text;
            username.BackColor = UiTheme.Surface;
            username.RightToLeft = RightToLeft.Yes;
            username.DropDownStyle = ComboBoxStyle.DropDown;
              username.TabIndex = 1;
            username.AccessibleName = "اسم المستخدم";
            username.AccessibleDescription = "أدخل اسم مستخدم النظام.";
            UiTheme.ApplyComboBoxQuality(username);

            password.Font = AppFonts.Create(10.5F, FontStyle.Regular);
            password.ForeColor = UiTheme.Text;
            password.BackColor = UiTheme.Surface;
            password.UseSystemPasswordChar = false;
            password.PasswordChar = DesktopDesignTokens.PasswordMaskCharacter;
            password.RightToLeft = RightToLeft.No;
              password.TabIndex = 2;
            password.AccessibleName = "كلمة المرور";
            password.AccessibleDescription = "أدخل كلمة المرور ثم اضغط دخول.";

            StyleActionButton(passwordVisibilityButton, UiTheme.SurfaceVariant, UiTheme.Text, true);
            passwordVisibilityButton.Font = AppFonts.Create(9F, FontStyle.Bold);
            passwordVisibilityButton.TabIndex = 3;
            passwordVisibilityButton.AccessibleName = "إظهار كلمة المرور";
            passwordVisibilityButton.AccessibleDescription = "يعرض كلمة المرور مؤقتاً، واضغط مرة أخرى لإخفائها.";

            chkRememberMe.Text = Present("تذكرني");
            chkRememberMe.Font = AppFonts.Create(9.5F);
            chkRememberMe.ForeColor = UiTheme.MutedText;
            chkRememberMe.BackColor = UiTheme.Surface;
            chkRememberMe.TextAlign = ContentAlignment.MiddleRight;
              chkRememberMe.TabIndex = 4;
            chkRememberMe.AccessibleName = "تذكر اسم المستخدم";
            chkRememberMe.AccessibleDescription = "يحفظ اسم المستخدم وكلمة المرور (مشفرة) على هذا الجهاز.";

            button1.Text = Present("دخول");
            StyleActionButton(button1, UiTheme.PrimaryDark, Color.White, false);
            SetLoginButtonIcon(button1, AppIcon.Confirm);
              button1.TabIndex = 5;
            button1.AccessibleName = "دخول إلى النظام";

            button3.Text = Present("إغلاق");
            StyleActionButton(button3, UiTheme.SurfaceVariant, UiTheme.Text, true);
            SetLoginButtonIcon(button3, AppIcon.Close);
              button3.TabIndex = 6;
            button3.AccessibleName = "إغلاق التطبيق";

            Image legacyLoginImage = pictureBox1.Image;
            pictureBox1.Image = OutlinedIcons.RenderBitmap(AppIcon.Medicine, 96, UiTheme.Primary, true);
            pictureBox1.BackColor = Color.Transparent;
            pictureBox1.SizeMode = PictureBoxSizeMode.Zoom;
            if (legacyLoginImage != null && !ReferenceEquals(legacyLoginImage, pictureBox1.Image)) legacyLoginImage.Dispose();
            LayoutLoginSurface();
        }

        // يعرض قاعدة التشغيل المصدرية بوضوح قبل الدخول؛ اختلافها هو السبب الأكثر شيوعاً
        // لاعتقاد المستخدم أن البيانات حُذفت بينما هو متصل بقاعدة أخرى.
        private string Present(string arabicText)
        {
            return presentationLeftToRight ? UiLanguageCatalog.Translate(arabicText) : arabicText;
        }

        internal void ApplyPresentation(bool leftToRight)
        {
            presentationLeftToRight = leftToRight;
            Text = Present("تسجيل الدخول");
            heroTitle.Text = Present("إدارة الصيدلية");
            heroSubtitle.Text = Present("دخول آمن وسريع لإدارة المبيعات والمخزون والموظفين.");
            heroNote.Text = BuildConnectionIdentityText();
            label1.Text = Present("تسجيل الدخول");
            loginSubtitle.Text = RuntimeSettingsStore.HasUnreadableSavedProfile
                ? Present("تعذر قراءة إعداد الاتصال — اضغط F1 للإصلاح")
                : BuildDatabaseSubtitle(ConnectionSettingsService.GetConnectionProfile());
            loginSubtitle.AccessibleName = Present("قاعدة التشغيل الحالية");
            loginSubtitle.AccessibleDescription = Present("اسم قاعدة البيانات التي سيستخدمها النظام بعد تسجيل الدخول.");
            databaseSettingsButton.Text = Present("إعداد قاعدة البيانات (F1)");
            databaseSettingsButton.AccessibleName = Present("إعداد قاعدة البيانات");
            databaseSettingsButton.AccessibleDescription = Present("يفتح إعدادات اختيار الخادم وقاعدة البيانات.");
            label2.Text = Present("المستخدم");
            label3.Text = Present("كلمة المرور");
            username.AccessibleName = Present("اسم المستخدم");
            username.AccessibleDescription = Present("أدخل اسم مستخدم النظام.");
            password.AccessibleName = Present("كلمة المرور");
            password.AccessibleDescription = Present("أدخل كلمة المرور ثم اضغط دخول.");
            chkRememberMe.Text = Present("تذكرني");
            chkRememberMe.AccessibleName = Present("تذكر اسم المستخدم");
            chkRememberMe.AccessibleDescription = Present("يحفظ اسم المستخدم وكلمة المرور (مشفرة) على هذا الجهاز.");
            rememberHint.Text = Present("يحفظ اسم المستخدم وكلمة المرور (مشفرة) على هذا الجهاز.");
            button1.Text = Present("دخول");
            button1.AccessibleName = Present("دخول إلى النظام");
            button3.Text = Present("إغلاق");
            button3.AccessibleName = Present("إغلاق التطبيق");
            UpdatePasswordVisibilityPresentation();

            UiDirection.Apply(this, leftToRight);
            UiTheme.ApplyStatusStripPresentation(this, leftToRight);
            heroTitle.TextAlign = leftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
            heroSubtitle.TextAlign = leftToRight ? ContentAlignment.TopLeft : ContentAlignment.TopRight;
            heroNote.TextAlign = leftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
            label2.TextAlign = leftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
            label3.TextAlign = leftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
            username.RightToLeft = leftToRight ? RightToLeft.No : RightToLeft.Yes;
            password.RightToLeft = RightToLeft.No;
            chkRememberMe.TextAlign = leftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
            chkRememberMe.CheckAlign = leftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
            rememberHint.TextAlign = leftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
            foreach (AntdUI.Button action in new[] { databaseSettingsButton, passwordVisibilityButton, button1, button3 })
                action.IconPosition = leftToRight ? AntdUI.TAlignMini.Left : AntdUI.TAlignMini.Right;
            LayoutLoginSurface();
        }

        private string BuildConnectionIdentityText()
        {
            DatabaseConnectionProfile profile = ConnectionSettingsService.GetConnectionProfile();
            return Present("الخادم") + ": " + WrapLeftToRight(profile.ServerName);
        }

        private string BuildDatabaseSubtitle(DatabaseConnectionProfile profile)
        {
            return Present("قاعدة التشغيل") + "  •  " + WrapLeftToRight(profile.DatabaseName);
        }

        private static string WrapLeftToRight(string value)
        {
            return "\u202A" + (value ?? string.Empty) + "\u202C";
        }

        private void TogglePasswordVisibility(object sender, EventArgs e)
        {
            int selectionStart = password.SelectionStart;
            int selectionLength = password.SelectionLength;
            isPasswordVisible = !isPasswordVisible;
            password.UseSystemPasswordChar = false;
            password.PasswordChar = isPasswordVisible ? '\0' : DesktopDesignTokens.PasswordMaskCharacter;
            UpdatePasswordVisibilityPresentation();
            password.Focus();
            password.Select(selectionStart, selectionLength);
        }

        private void UpdatePasswordVisibilityPresentation()
        {
            bool visible = isPasswordVisible;
            passwordVisibilityButton.Text = Present(visible ? "إخفاء" : "إظهار");
            passwordVisibilityButton.AccessibleName = Present(visible ? "إخفاء كلمة المرور" : "إظهار كلمة المرور");
            passwordVisibilityButton.AccessibleDescription = Present(visible
                ? "يخفي كلمة المرور مرة أخرى."
                : "يعرض كلمة المرور مؤقتاً، واضغط مرة أخرى لإخفائها.");
        }

        private void RefreshConnectionIdentity()
        {
            DatabaseConnectionProfile profile = ConnectionSettingsService.GetConnectionProfile();
            heroNote.Text = BuildConnectionIdentityText();
            loginSubtitle.Text = BuildDatabaseSubtitle(profile);
            LayoutLoginSurface();
            Invalidate();
        }

        private void StyleFieldLabel(Label label)
        {
            label.AutoSize = false;
            label.TextAlign = ContentAlignment.MiddleRight;
            label.Font = AppFonts.Create(10F, FontStyle.Bold);
            label.ForeColor = UiTheme.MutedText;
        }

        private void StyleActionButton(AntdUI.Button button, Color backColor, Color foreColor, bool bordered)
        {
            AntdThemeAdapter.Apply(button);
            button.Type = bordered ? AntdUI.TTypeMini.Default : AntdUI.TTypeMini.Primary;
            button.BackColor = backColor;
            button.DefaultBack = backColor;
            button.ForeColor = foreColor;
            button.Font = AppFonts.Create(10F, FontStyle.Bold);
            button.Cursor = Cursors.Hand;
            button.BorderWidth = bordered ? 1F : 0F;
            button.DefaultBorderColor = bordered ? UiTheme.Primary : backColor;
            button.BackHover = bordered ? UiTheme.Selection : UiTheme.Primary;
            button.ForeHover = foreColor;
            button.Radius = DesktopDesignTokens.ActionRadius(button.Height);
            button.AccessibleRole = AccessibleRole.PushButton;
        }

        private void SetLoginButtonIcon(AntdUI.Button button, AppIcon icon)
        {
            button.IconSvg = OutlinedIcons.Svg(icon, !presentationLeftToRight);
            button.IconSize = new Size(18, 18);
            button.IconPosition = presentationLeftToRight ? AntdUI.TAlignMini.Left : AntdUI.TAlignMini.Right;
        }

        private void ApplyLoginWindow()
        {
            WindowState = FormWindowState.Normal;
            FormBorderStyle = FormBorderStyle.Sizable;
            // تسمح الشاشة القصيرة أو DPI المرتفع بالوصول إلى جميع عناصر البطاقة
            // بدلاً من قص زري الدخول والإغلاق في نافذة ثابتة.
            AutoScroll = true;
            AutoScrollMinSize = Size.Empty;
            ControlBox = true;
            MinimizeBox = true;
            MaximizeBox = false;
            ShowIcon = false;
            StartPosition = FormStartPosition.CenterScreen;
            Text = "تسجيل الدخول";
            Rectangle workingArea = Screen.FromPoint(Cursor.Position).WorkingArea;
            int clientWidth = Math.Min(loginWindowSize.Width, Math.Max(360, workingArea.Width - 48));
            int clientHeight = Math.Min(loginWindowSize.Height, Math.Max(360, workingArea.Height - 48));
            ClientSize = new Size(clientWidth, clientHeight);
        }

        private void LayoutLoginSurface()
        {
            if (loginCard == null || arrangingLogin)
            {
                return;
            }

            arrangingLogin = true;
            SuspendLayout();
            try
            {
            Point scroll = AutoScrollPosition;
            int availableWidth = Math.Max(1, ClientSize.Width - SystemInformation.VerticalScrollBarWidth);
            int margin = availableWidth < 600 ? 16 : 30;
            int cardWidth = 370;
            bool shortWindow = ClientSize.Height < 500;
            int minimumCardContentHeight = shortWindow ? Math.Max(1, ClientSize.Height - margin * 2) : 424;
            int cardHeight = Math.Max(minimumCardContentHeight, ClientSize.Height - margin * 2);
            const int minimumHeroWidth = 300;
            bool compactLayout = availableWidth < cardWidth + minimumHeroWidth + margin * 3;

            if (compactLayout)
            {
                // على الشاشات الضيقة أو التكبير العالي لا نسمح للوحة الترحيب بالخروج
                // خارج النافذة؛ تبقى بطاقة الدخول واضحة وقابلة للاستخدام.
                heroPanel.Visible = false;
                cardWidth = Math.Max(1, availableWidth - margin * 2);
                loginCard.Bounds = new Rectangle(
                    margin + scroll.X,
                    margin + scroll.Y,
                    cardWidth,
                    cardHeight);
            }
            else
            {
                heroPanel.Visible = true;
                int heroWidth = availableWidth - cardWidth - margin * 3;
                // Keep one logical order. RightToLeftLayout mirrors it for Arabic;
                // assigning mirrored coordinates here would move the form card twice.
                loginCard.Bounds = new Rectangle(margin + scroll.X, margin + scroll.Y, cardWidth, cardHeight);
                heroPanel.Bounds = new Rectangle(margin * 2 + cardWidth + scroll.X, margin + scroll.Y, heroWidth, cardHeight);
            }

            loginCard.Radius = DesktopDesignTokens.CardRadius;
            loginCard.BorderColor = UiTheme.Border;
            loginCard.Shadow = 0;
            if (heroPanel.Visible)
            {
                ApplyRoundedPanel(heroPanel, 16);
            }

            int heroPadding = 34;
            heroTitle.Bounds = new Rectangle(heroPadding, 82, heroPanel.Width - heroPadding * 2, 48);
            heroSubtitle.Bounds = new Rectangle(heroPadding, 142, heroPanel.Width - heroPadding * 2, 78);
            heroNote.Bounds = new Rectangle(heroPadding, heroPanel.Height - 92, heroPanel.Width - heroPadding * 2, 30);

            // يحافظ التسلسل المضغوط على ظهور زري الدخول والإغلاق داخل بطاقة ارتفاعها
            // الافتراضي حتى مع تحجيم DPI المرتفع، بدلاً من قص الزر الأخير.
            int padding = compactLayout ? 18 : 34;
            int inputWidth = Math.Max(1, loginCard.Width - padding * 2);
            pictureBox1.Visible = !shortWindow;
            button3.Visible = !shortWindow;
            if (rememberHint != null) rememberHint.Visible = !shortWindow;
            int y = shortWindow ? 12 : 22;

            // تبقى الإجراءات داخل البطاقة حتى عند ارتفاع عميل محدود (أو تكبير DPI).
            // المسافة بعد خيار التذكر مخصصة أيضاً لرسالة المساعدة التي يضعها
            // PositionRememberHint أسفل الخيار.
            pictureBox1.Bounds = new Rectangle((loginCard.Width - 72) / 2, y, 72, 44);
            if (!shortWindow) y += 50;
            label1.Bounds = new Rectangle(padding, y, inputWidth, Math.Max(28, label1.PreferredHeight));
            y = label1.Bottom + 4;
            int subtitleHeight = TextRenderer.MeasureText(loginSubtitle.Text, loginSubtitle.Font,
                new Size(inputWidth, int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height;
            loginSubtitle.Bounds = new Rectangle(padding, y, inputWidth, subtitleHeight);
            y = loginSubtitle.Bottom + 8;
            databaseSettingsButton.Bounds = new Rectangle(padding, y, inputWidth, 44);
            y = databaseSettingsButton.Bottom + 8;
            label2.Bounds = new Rectangle(padding, y, inputWidth, Math.Max(18, label2.PreferredHeight));
            y = label2.Bottom + 4;
            username.Bounds = new Rectangle(padding, y, inputWidth, 44);
            y = username.Bottom + 8;
            label3.Bounds = new Rectangle(padding, y, inputWidth, Math.Max(18, label3.PreferredHeight));
            y = label3.Bottom + 4;
            const int visibilityButtonWidth = 72;
            int passwordHeight = 44;
            if (presentationLeftToRight)
            {
                password.Bounds = new Rectangle(padding, y, Math.Max(1, inputWidth - visibilityButtonWidth), passwordHeight);
                passwordVisibilityButton.Bounds = new Rectangle(password.Right + 8, y, visibilityButtonWidth - 8, passwordHeight);
            }
            else
            {
                passwordVisibilityButton.Bounds = new Rectangle(padding, y, visibilityButtonWidth - 8, passwordHeight);
                password.Bounds = new Rectangle(padding + visibilityButtonWidth, y, Math.Max(1, inputWidth - visibilityButtonWidth), passwordHeight);
            }
            y = Math.Max(password.Bottom, passwordVisibilityButton.Bottom) + 8;
            chkRememberMe.AutoSize = false;
            chkRememberMe.TextAlign = ContentAlignment.MiddleLeft;
            chkRememberMe.Bounds = new Rectangle(padding, y, inputWidth, Math.Max(20, chkRememberMe.Font.Height + 4));
            y = chkRememberMe.Bottom + (shortWindow ? 10 : rememberHint == null ? 30 : rememberHint.Font.Height + 14);
            button1.Bounds = new Rectangle(padding, y, inputWidth, 44);
            y = button1.Bottom + 10;
            button3.Bounds = new Rectangle(padding, y, inputWidth, 44);
            foreach (AntdUI.Button action in new[] { databaseSettingsButton, passwordVisibilityButton, button1, button3 })
                action.Radius = DesktopDesignTokens.ActionRadius(action.Height);
            PositionRememberHint();
            cardHeight = Math.Max(cardHeight, (shortWindow ? button1.Bottom : button3.Bottom) + 18);
            loginCard.Height = cardHeight;
            heroPanel.Height = cardHeight;
            heroNote.Top = cardHeight - 92;
            loginCard.Radius = DesktopDesignTokens.CardRadius;
            loginCard.BorderColor = UiTheme.Border;
            loginCard.Shadow = 0;
            if (heroPanel.Visible) ApplyRoundedPanel(heroPanel, 16);
            AutoScrollMinSize = new Size(0, margin + cardHeight + margin);
            }
            finally
            {
                ResumeLayout(true);
                arrangingLogin = false;
            }
        }

        private void ApplyRoundedPanel(Control control, int radius)
        {
            if (control.Width <= 0 || control.Height <= 0)
            {
                return;
            }

            if (control.Region != null)
            {
                control.Region.Dispose();
            }

            using (GraphicsPath path = CreateRoundedRectangle(new Rectangle(0, 0, control.Width, control.Height), radius))
            {
                control.Region = new Region(path);
            }
        }

        private void LoginCard_Paint(object sender, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle bounds = new Rectangle(0, 0, loginCard.Width - 1, loginCard.Height - 1);
            using (GraphicsPath path = CreateRoundedRectangle(bounds, 14))
            using (Pen border = new Pen(UiTheme.Border, 1F))
            {
                e.Graphics.DrawPath(border, path);
            }
        }

        private void HeroPanel_Paint(object sender, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle bounds = new Rectangle(0, 0, heroPanel.Width - 1, heroPanel.Height - 1);
            using (GraphicsPath path = CreateRoundedRectangle(bounds, 16))
            using (LinearGradientBrush brush = new LinearGradientBrush(bounds, UiTheme.PrimaryDark, UiTheme.Primary, 45F))
            {
                e.Graphics.FillPath(brush, path);
            }

            using (Pen line = new Pen(Color.FromArgb(70, 255, 255, 255), 1F))
            {
                e.Graphics.DrawLine(line, 34, heroPanel.Height - 122, heroPanel.Width - 34, heroPanel.Height - 122);
            }
        }

        private GraphicsPath CreateRoundedRectangle(Rectangle bounds, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int diameter = radius * 2;

            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        // تهيئة مربع اختيار تذكرني وتحميل البيانات المحفوظة.
        private void InitializeRememberMe()
        {
            chkRememberMe.BringToFront();
            EnsureRememberHint();

            try
            {
                isInitializingRememberMe = true;
            RememberMeService.RememberedCredentials credentials = RememberMeService.LoadRememberedCredentials();
            rememberedUserName = credentials.UserName;
            if (!string.IsNullOrWhiteSpace(rememberedUserName))
            {
                chkRememberMe.Checked = true;
                username.Text = rememberedUserName;
                if (!string.IsNullOrEmpty(credentials.Password))
                {
                    password.Text = credentials.Password;
                    button1.Focus();
                }
                else
                {
                    password.Clear();
                    password.Focus();
                }
            }
                else
                {
                    chkRememberMe.Checked = false;
                }
            }
            catch
            {
                chkRememberMe.Checked = false;
                rememberedUserName = string.Empty;
                password.Clear();
            }
            finally
            {
                isInitializingRememberMe = false;
            }
        }

        private void EnsureRememberHint()
        {
            if (rememberHint == null)
            {
                rememberHint = new Label
                {
                    Name = "rememberHint",
                    AutoSize = false,
                    Text = "يحفظ اسم المستخدم وكلمة المرور (مشفرة) على هذا الجهاز.",
                    TextAlign = ContentAlignment.MiddleLeft,
                    Font = AppFonts.Create(8.5F),
                    ForeColor = UiTheme.MutedText,
                    BackColor = Color.Transparent
                };

                Controls.Add(rememberHint);
            }

            rememberHint.Visible = chkRememberMe.Checked;
            PositionRememberHint();
            rememberHint.BringToFront();
        }

        private void PositionRememberHint()
        {
            if (rememberHint == null)
            {
                return;
            }

            Control parent = chkRememberMe.Parent ?? this;
            if (rememberHint.Parent != parent)
            {
                rememberHint.Parent.Controls.Remove(rememberHint);
                parent.Controls.Add(rememberHint);
            }

            rememberHint.Bounds = new Rectangle(
                chkRememberMe.Left,
                chkRememberMe.Bottom + 2,
                chkRememberMe.Width,
                Math.Max(22, rememberHint.Font.Height + 4));

            EnsureRememberHintSpacing();
        }

        private void EnsureRememberHintSpacing()
        {
            if (rememberHint == null || !rememberHint.Visible)
            {
                return;
            }

            int requiredTop = rememberHint.Bottom + 8;
            if (button1.Top < requiredTop)
            {
                button1.Top = requiredTop;
            }

            int closeTop = button1.Bottom + 10;
            if (button3.Top < closeTop)
            {
                button3.Top = closeTop;
            }
        }

        private void chkRememberMe_CheckedChanged(object sender, EventArgs e)
        {
            if (isInitializingRememberMe || previewMode)
            {
                return;
            }

            if (!chkRememberMe.Checked)
            {
                rememberedUserName = string.Empty;
                RememberMeService.Clear();
            }

            if (rememberHint != null)
            {
                rememberHint.Visible = chkRememberMe.Checked;
                PositionRememberHint();
            }
        }

        // يتحقق من بيانات الدخول ويتأكد من جاهزية قاعدة البيانات قبل فتح النظام.
        private async void button1_Click_1(object sender, EventArgs e)
        {
            if (previewMode)
            {
                loginSubtitle.Text = Present("معاينة فقط؛ لم تُفتح قاعدة البيانات ولم تُنفذ مصادقة.");
                loginSubtitle.AccessibleName = loginSubtitle.Text;
                LayoutLoginSurface();
                return;
            }

            if (RuntimeSettingsStore.HasUnreadableSavedProfile)
            {
                AppNotifications.Show(
                    Present("تعذر قراءة إعداد اتصال قاعدة البيانات ونسخته الاحتياطية. لا يمكن الدخول إلى قاعدة افتراضية مختلفة. افتح إعدادات الاتصال عبر F1 ليصلحها مسؤول Windows المحلي."),
                    Present("إعدادات قاعدة البيانات"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            string name = username.Text.Trim();
            string pass = password.Text;

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(pass))
            {
                AppNotifications.Show(Present("الرجاء إدخال اسم المستخدم وكلمة المرور."), Present("تسجيل الدخول"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (TryGetLoginCooldown(out int remainingSeconds))
            {
                AppNotifications.Show(
                    string.Format(Present("تم إيقاف محاولات تسجيل الدخول مؤقتًا. حاول بعد {0} ثانية."), remainingSeconds),
                    Present("تسجيل الدخول"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            long requestedSessionRevision = SessionContext.CurrentSessionRevision;
            string requestedConnection = null;
            AuthenticationResult authentication;
            try
            {
                Cursor = Cursors.WaitCursor;
                button1.Enabled = false;
                requestedConnection = ConnectionSettingsService.LoadSettings().ConnectionString;
                IAuthenticationService authenticationService = ApplicationServices.CreateAuthenticationSession(requestedConnection);
                authentication = await Task.Run(() => authenticationService.Authenticate(
                    new AuthenticationRequest { UserName = name, Password = pass }));
                if (!CanApplyAuthenticationReply(name, pass, requestedConnection, requestedSessionRevision)) return;
            }
            catch (Exception ex)
            {
                if (!CanApplyAuthenticationReply(name, pass, requestedConnection, requestedSessionRevision)) return;
                string connectionHelp = presentationLeftToRight
                    ? Present("تحقق من إعدادات الخادم وقاعدة البيانات عبر F1 ثم أعد المحاولة.")
                    : ConnectionSettingsService.BuildConnectionHelp(ex);
                AppNotifications.Show(
                    Present("فشل الاتصال بقاعدة البيانات. يجب أن يفتح مسؤول Windows المحلي إعدادات الاتصال عبر F1.") + "\n\n" + connectionHelp,
                    Present("تسجيل الدخول"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }
            finally
            {
                if (!IsDisposed && !Disposing)
                {
                    button1.Enabled = true;
                    Cursor = Cursors.Default;
                }
            }

            if (authentication.Status == AuthenticationStatus.ManagerSetupRequired)
            {
                AppNotifications.Show(
                    Present("لا يوجد مدير مسجل للنظام بعد. يجب على مسؤول Windows محلي فتح إعدادات قاعدة البيانات عبر F1 وإنشاء المدير الأول قبل تسجيل الدخول."),
                    Present("تهيئة مطلوبة"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            if (authentication.Status == AuthenticationStatus.InvalidCredentials)
            {
                RegisterFailedLoginAttempt();
                AppNotifications.Show(Present("اسم المستخدم أو كلمة المرور غير صحيحة."), Present("خطأ في تسجيل الدخول"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                password.Clear();
                return;
            }
            if (authentication.Status != AuthenticationStatus.Success)
            {
                AppNotifications.Show(Present("الرجاء إدخال اسم المستخدم وكلمة المرور."), Present("تسجيل الدخول"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            ResetLoginThrottle();
            password.Clear();
            isPasswordVisible = false;
            password.PasswordChar = DesktopDesignTokens.PasswordMaskCharacter;
            UpdatePasswordVisibilityPresentation();
            name = authentication.UserName;
            try
            {
                SessionContext.StartAuthenticated(name,authentication.Role,authentication.EmployeeId,requestedConnection,requestedSessionRevision);
            }
            catch(UnauthorizedAccessException error)
            {
                AppDiagnostics.Log("LoginPublicationContext",error);
                loginSubtitle.Text=Present("تغير اتصال قاعدة البيانات أو جلسة المستخدم أثناء المحاولة. أعد المحاولة.");
                loginSubtitle.AccessibleName=loginSubtitle.Text;
                LayoutLoginSurface();
                return;
            }


            try
            {
                if (chkRememberMe.Checked)
                {
                    RememberMeService.SaveRememberedCredentials(name, pass);
                    rememberedUserName = name;
                }
                else
                {
                    rememberedUserName = string.Empty;
                    RememberMeService.Clear();
                }
            }
            catch (Exception ex)
            {
                AppNotifications.Show(
                    Present("تم تسجيل الدخول، لكن تعذر حفظ خيار تذكرني.") + "\n\n" + ex.Message,
                    Present("تذكرني"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }

            Hide();

            using (Home home = new Home())
            {
                home.ShowDialog(this);
            }

            if (!IsDisposed)
            {
                SessionContext.Clear();
                Show();
            }
        }

        // لا تُطبق نتيجة قديمة على بيانات دخول عُدلت أثناء الانتظار.
        private bool CanApplyAuthenticationReply(string requestedName, string requestedPassword, string requestedConnection, long requestedSessionRevision)
        {
            if (IsDisposed || Disposing) return false;
            bool sameConnection = requestedConnection == null;
            if (requestedConnection != null)
            {
                try
                {
                    sameConnection = !RuntimeSettingsStore.HasUnreadableSavedProfile &&
                        string.Equals(ConnectionSettingsService.LoadSettings().ConnectionString, requestedConnection, StringComparison.Ordinal);
                }
                catch (Exception) { sameConnection = false; }
            }
            if (!sameConnection || SessionContext.CurrentSessionRevision != requestedSessionRevision)
            {
                loginSubtitle.Text = Present("تغير اتصال قاعدة البيانات أو جلسة المستخدم أثناء المحاولة. أعد المحاولة.");
                loginSubtitle.AccessibleName = loginSubtitle.Text;
                LayoutLoginSurface();
                return false;
            }
            if (string.Equals(username.Text.Trim(), requestedName, StringComparison.Ordinal) &&
                string.Equals(password.Text, requestedPassword, StringComparison.Ordinal)) return true;
            loginSubtitle.Text = Present("تغيرت بيانات الدخول أثناء المحاولة. أعد المحاولة.");
            loginSubtitle.AccessibleName = loginSubtitle.Text;
            LayoutLoginSurface();
            return false;
        }
        // يمسح حقول اسم المستخدم وكلمة المرور.
        private void button2_Click_1(object sender, EventArgs e)
        {
            username.Text = password.Text = null;
        }

        // يغلق التطبيق من شاشة الدخول.
        private void button3_Click_1(object sender, EventArgs e)
        {
            Application.Exit();
        }

        // يخفي كلمة المرور أثناء الكتابة ويثبت خط الحقل.
        private void password_TextChanged(object sender, EventArgs e)
        {
            // Editing must retain the user's chosen visibility state and avoid font allocations.
            char mask = isPasswordVisible ? '\0' : DesktopDesignTokens.PasswordMaskCharacter;
            if (password.PasswordChar != mask) password.PasswordChar = mask;
        }

        // لا يعرض أسماء الموظفين قبل المصادقة؛ حقل اسم المستخدم يبقى قابلاً للكتابة.
        private void LoadUsernames()
        {
            username.DropDownStyle = ComboBoxStyle.DropDown;
            username.Items.Clear();
            if (!string.IsNullOrWhiteSpace(rememberedUserName))
            {
                username.Text = rememberedUserName;
                password.Clear();
            }
        }

        // يفتح إعدادات قاعدة البيانات عند الضغط على F1.
        private void Login_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F1)
            {
                OpenDatabaseSettings();
                e.Handled = true;
            }
        }

        // يفتح نافذة إعداد الاتصال بقاعدة البيانات.
        private void OpenDatabaseSettings()
        {
            if (previewMode)
            {
                loginSubtitle.Text = Present("معاينة فقط؛ لم تُفتح إعدادات قاعدة البيانات.");
                loginSubtitle.AccessibleName = loginSubtitle.Text;
                LayoutLoginSurface();
                return;
            }

            if (LocalAdministratorAccess.IsCurrentUserAdministrator())
            {
                using (DatabaseSetingsForm settingsForm = new DatabaseSetingsForm())
                {
                    settingsForm.ShowDialog(this);
                }
                RefreshConnectionIdentity();
                LoadUsernames();
                return;
            }

            DialogResult confirmation = AppNotifications.Show(
                Present("تعديل اتصال قاعدة البيانات يتطلب صلاحية مسؤول Windows لحماية بيانات الصيدلية. سيتم فتح نافذة إعداد مستقلة بعد موافقتك على طلب Windows."),
                Present("فتح إعدادات قاعدة البيانات"),
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Information);
            if (confirmation != DialogResult.OK)
            {
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = Application.ExecutablePath,
                    Arguments = "--database-settings",
                    UseShellExecute = true,
                    Verb = "runas"
                });
                AppNotifications.Show(
                    Present("فُتحت نافذة إعدادات الاتصال بصلاحية مسؤول. احفظ التغيير فيها ثم أغلق شاشة الدخول وافتح النظام من جديد لاستخدام الاتصال الجديد."),
                    Present("إعدادات قاعدة البيانات"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Win32Exception exception) when (exception.NativeErrorCode == 1223)
            {
                // ألغى المستخدم نافذة UAC؛ لا نعرض رسالة خطأ مضللة.
            }
            catch (Exception)
            {
                AppNotifications.Show(
                    Present("تعذر فتح إعدادات قاعدة البيانات بصلاحية مسؤول. تأكد من السماح بطلب Windows ثم أعد المحاولة."),
                    Present("إعدادات قاعدة البيانات"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private bool TryGetLoginCooldown(out int remainingSeconds)
        {
            TimeSpan remaining = nextLoginAttemptUtc - DateTime.UtcNow;
            remainingSeconds = Math.Max(0, (int)Math.Ceiling(remaining.TotalSeconds));
            return remainingSeconds > 0;
        }

        private void RegisterFailedLoginAttempt()
        {
            consecutiveFailedLoginAttempts++;
            int exponent = Math.Min(consecutiveFailedLoginAttempts - 1, 5);
            int delaySeconds = Math.Min(30, 1 << exponent);
            nextLoginAttemptUtc = DateTime.UtcNow.AddSeconds(delaySeconds);
        }

        private void ResetLoginThrottle()
        {
            consecutiveFailedLoginAttempts = 0;
            nextLoginAttemptUtc = DateTime.MinValue;
        }

    }
}

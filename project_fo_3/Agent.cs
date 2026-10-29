// شرح الملف: شاشة إدارة الموظفين.
// تحتوي على عمليات إضافة وتعديل وحذف وعرض الموظفين من قاعدة البيانات.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using PharmacyManagement.Application;

namespace project_fo_3
{
    // شاشة إدارة الموظفين.
    // تنفذ عمليات الإضافة والتعديل والحذف والعرض مع اتصال SQL مركزي من Database.cs.
    public partial class Agent : Form
    {
        private IEmployeeService Service => ApplicationServices.Employees;
        private Label roleLabel;
        private AntdUI.Select roleCombo;
        private AntdUI.Button detailedPermissionsButton;
        private AntdUI.Panel contentHost;
        private Panel actionBar;
        private AntdUI.Panel editorCard;
        private AntdUI.Panel nameFrame;
        private AntdUI.Panel passwordFrame;
        private readonly bool previewMode;
        private readonly bool presentationLeftToRight;

        // ينشئ شاشة الموظفين ويطبق التصميم الموحد.
        public Agent() : this(false, UiLanguageCatalog.IsEnglishActive)
        {
        }

        internal Agent(bool previewMode)
            : this(previewMode, UiLanguageCatalog.IsEnglishActive)
        {
        }

        internal Agent(bool previewMode, bool presentationLeftToRight)
        {
            this.previewMode = previewMode;
            this.presentationLeftToRight = presentationLeftToRight;
            InitializeComponent();
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96F, 96F);
            MinimumSize = new Size(760, 640);
            EmployeeGV.Tag = UiTheme.PreserveLocalizedCustomGridColumns;
            EmployeeGV.CellPainting += EmployeeGV_CellPainting;
            if (previewMode) Load -= Ageny_Load;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = true;
            label8.Visible = false;
            label1.Visible = false;
            panel2.Visible = false;
            label2.Font = AppFonts.Create(18F, FontStyle.Bold);
            UiTheme.Apply(this);
            ConfigureAgentInputs();
            ConfigureRoleControls();
            ApplyResponsiveLayout();
            ApplyPresentation();
            LayoutResponsiveScreen();
            EmployeeGV.CellFormatting += EmployeeGV_CellFormatting;
            if (previewMode) PopulatePreviewGrid();
        }

        private string Present(string arabicText)
        {
            return presentationLeftToRight ? UiLanguageCatalog.Translate(arabicText) : arabicText;
        }

        private void ApplyPresentation()
        {
            Text = Present("الموظفون");
            label2.Text = Present("الموظفون");
            label6.Text = Present("رقم الموظف");
            label5.Text = Present("اسم الموظف");
            label4.Text = Present("العمر");
            label3.Text = Present("الراتب");
            label7.Text = Present("رقم الهاتف");
            label9.Text = Present("كلمة مرور جديدة (اختيارية، 3 رموز فأكثر)");
            label10.Text = Present("قائمة الموظفين");
            roleLabel.Text = Present("الصلاحية");

            ConfigureEmployeeAction(button4, "إضافة", "إضافة موظف", AppIcon.Add);
            ConfigureEmployeeAction(button3, "تعديل", "تعديل موظف", AppIcon.Settings);
            ConfigureEmployeeAction(button1, "حذف", "حذف موظف", AppIcon.Delete);
            ConfigureEmployeeAction(button5, "مسح", "مسح حقول الموظف", AppIcon.Refresh);
            ConfigureEmployeeAction(button2, "رجوع", "إغلاق شاشة الموظفين", AppIcon.Back);
            detailedPermissionsButton.Text = detailedPermissionsButton.AccessibleName = Present("إدارة صلاحيات الشاشات والعمليات");
            detailedPermissionsButton.IconPosition = presentationLeftToRight ? AntdUI.TAlignMini.Left : AntdUI.TAlignMini.Right;

            foreach (Label label in new[] { label2, label3, label4, label5, label6, label7, label9, label10, roleLabel })
                label.TextAlign = presentationLeftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
            tb2.RightToLeft = presentationLeftToRight ? RightToLeft.No : RightToLeft.Yes;
            tb2.TextAlign = presentationLeftToRight ? HorizontalAlignment.Left : HorizontalAlignment.Right;
            roleCombo.RightToLeft = presentationLeftToRight ? RightToLeft.No : RightToLeft.Yes;
            roleCombo.TextAlign = presentationLeftToRight ? HorizontalAlignment.Left : HorizontalAlignment.Right;
            RebuildRoleItems(GetSelectedRole());
            EmployeeGV.RightToLeft = presentationLeftToRight ? RightToLeft.No : RightToLeft.Yes;
            EmployeeGV.AccessibleName = Present("قائمة الموظفين");
            contentHost.AccessibleName = Present("بيانات الموظفين ومحرر الموظف");
            actionBar.AccessibleName = Present("إجراءات الموظفين");
            panel1.BackColor = UiTheme.Background;
            label2.BackColor = UiTheme.Background;
            label2.ForeColor = UiTheme.Text;
            UiDirection.Apply(this, presentationLeftToRight);

            if (previewMode)
            {
                button4.Enabled = button3.Enabled = button1.Enabled = detailedPermissionsButton.Enabled = false;
                button4.AccessibleDescription = button3.AccessibleDescription = button1.AccessibleDescription =
                    Present("معاينة فقط؛ لا تُحفظ بيانات موظفين.");
            }
        }

        private void ConfigureEmployeeAction(AntdUI.Button button, string text, string accessibleName, AppIcon icon)
        {
            button.Text = Present(text);
            button.AccessibleName = Present(accessibleName);
            button.IconSvg = OutlinedIcons.Svg(icon, !presentationLeftToRight);
            button.IconSize = new Size(18, 18);
            button.IconPosition = presentationLeftToRight ? AntdUI.TAlignMini.Left : AntdUI.TAlignMini.Right;
            DesktopComponents.StyleOutlinedFooterAction(button, button == button1);
        }

        private void RebuildRoleItems(string selectedRole)
        {
            roleCombo.Items.Clear();
            roleCombo.Items.AddRange(new object[]
            {
                new AntdUI.SelectItem(Present("مدير"), "Manager"),
                new AntdUI.SelectItem(Present("كاشير"), "Cashier"),
                new AntdUI.SelectItem(Present("مسؤول المخزون"), "Inventory"),
                new AntdUI.SelectItem(Present("محاسب"), "Accountant")
            });
            SelectRole(selectedRole);
        }

        private void PopulatePreviewGrid()
        {
            EmployeeGV.DataSource = new List<EmployeeRecord>
            {
                new EmployeeRecord { EmployeeId = "EMP-001", UserName = "مدير النظام — System Admin", Age = 35, Salary = 180000, Phone = "+967 711 222 333", Role = "Manager", PasswordStatus = Present("محمية") },
                new EmployeeRecord { EmployeeId = "EMP-014", UserName = "أحمد صالح", Age = 28, Salary = 95000, Phone = "+967 733 444 555", Role = "Cashier", PasswordStatus = Present("محمية") }
            };
            ApplyEmployeeGridPresentation();
        }

        private void EmployeeGV_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0 || EmployeeGV.Columns[e.ColumnIndex].Name != nameof(EmployeeRecord.Phone)) return;
            DesktopComponents.PaintLeftToRightGridCell(e, EmployeeGV.Font);
        }

        private void ConfigureAgentInputs()
        {
            ConfigureAgentInput(tb1, "رقم الموظف", false);
            tb2.AccessibleName = Present("اسم الموظف");
            tb2.AccessibleRole = AccessibleRole.Text;
            tb2.RightToLeft = presentationLeftToRight ? RightToLeft.No : RightToLeft.Yes;
            tb2.TextAlign = presentationLeftToRight ? HorizontalAlignment.Left : HorizontalAlignment.Right;
            tb2.Font = AppFonts.Create(10F);
            tb2.BorderStyle = BorderStyle.FixedSingle;
            tb2.BackColor = UiTheme.Surface;
            tb2.ForeColor = UiTheme.Text;
            ConfigureAgentInput(tb3, "العمر", false);
            ConfigureAgentInput(tb4, "الراتب", false);
            ConfigureAgentInput(tb5, "رقم الهاتف", false);
            tb6.AccessibleName = Present("كلمة مرور جديدة");
            tb6.PasswordChar = DesktopDesignTokens.PasswordMaskCharacter;
            tb6.RightToLeft = RightToLeft.No;
            tb6.TextAlign = HorizontalAlignment.Left;
            tb6.Font = AppFonts.Create(10F);
        }

        private void ConfigureAgentInput(AntdUI.Input input, string label, bool naturalLanguage)
        {
            input.AccessibleName = Present(label);
            input.AccessibleRole = AccessibleRole.Text;
            input.RightToLeft = naturalLanguage && !presentationLeftToRight ? RightToLeft.Yes : RightToLeft.No;
            input.TextAlign = naturalLanguage && !presentationLeftToRight ? HorizontalAlignment.Right : HorizontalAlignment.Left;
            input.Font = AppFonts.Create(10F);
            AntdThemeAdapter.Apply(input);
        }

        private void ApplyResponsiveLayout()
        {
            AutoScroll = false;
            panel1.Dock = DockStyle.None;
            panel2.Dock = DockStyle.None;
            contentHost = new AntdUI.Panel { Name = "employeeContentHost", AutoScroll = true,
                Back = UiTheme.Background, BackColor = UiTheme.Background, Shadow = 0, Radius = 0 };
            actionBar = new Panel { Name = "employeeActionBar", BackColor = UiTheme.Background };
            editorCard = DesktopComponents.Card("employeeEditorCard");
            WinFormsShapeAdapter.RemoveInputShape(tb2);
            WinFormsShapeAdapter.RemoveInputShape(tb6);
            nameFrame = DesktopComponents.NativeTextFrame(tb2, "employeeNameFrame");
            passwordFrame = DesktopComponents.NativeTextFrame(tb6, "employeePasswordFrame");
            foreach (Control control in new Control[] { label6, tb1, label5, nameFrame, label4, tb3,
                label3, tb4, label7, tb5, label9, passwordFrame, roleLabel, roleCombo, detailedPermissionsButton })
                editorCard.Controls.Add(control);
            contentHost.Controls.Add(editorCard);
            foreach (Control control in new Control[]
            {
                label10, EmployeeGV
            }) contentHost.Controls.Add(control);
            foreach (AntdUI.Button button in new[] { button4, button3, button1, button5, button2 })
            {
                actionBar.Controls.Add(button);
                DesktopComponents.StyleOutlinedFooterAction(button, button == button1);
            }
            Controls.Add(contentHost);
            Controls.Add(actionBar);
            Resize += Agent_Resize;
            ClientSizeChanged += Agent_Resize;
            Shown += Agent_Shown;
            LayoutResponsiveScreen();
        }

        private void Agent_Resize(object sender, EventArgs e)
        {
            LayoutResponsiveScreen();
        }

        private void Agent_Shown(object sender, EventArgs e)
        {
            LayoutResponsiveScreen();
            // Reapply after handle creation: WinForms can retain the designer's RTL
            // column geometry until the grid handle and auto-generated columns exist.
            ApplyEmployeeGridPresentation();
        }

        private void LayoutResponsiveScreen()
        {
            if (ClientSize.Width <= 0 || ClientSize.Height <= 0 || contentHost == null) return;
            SuspendLayout();
            try
            {
                Control statusStrip = Controls["__UiStatusStrip"];
                int statusHeight = statusStrip != null ? statusStrip.Height : 0;
                int workspaceBottom = ClientSize.Height - statusHeight;
                int titleWidth = Math.Max(240, ClientSize.Width - 48);
                int titleHeight = Math.Max(44, TextRenderer.MeasureText(label2.Text, label2.Font,
                    new Size(titleWidth, 1000), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height + 4);
                panel1.SetBounds(0, 0, ClientSize.Width, titleHeight + 38);
                actionBar.SetBounds(0, Math.Max(panel1.Bottom, workspaceBottom - 70), ClientSize.Width, 70);
                contentHost.SetBounds(0, panel1.Bottom, ClientSize.Width, Math.Max(120, actionBar.Top - panel1.Bottom));
                label2.SetBounds(24, 18, titleWidth, titleHeight);
                label2.AccessibleName = label2.Text;
                label2.TextAlign = presentationLeftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;

                int contentWidth = contentHost.ClientSize.Width;
                int margin = contentWidth < 980 ? 24 : 34;
                int gap = 28;
                bool compact = contentWidth < 980;
                int editorWidth = compact ? Math.Max(320, contentWidth - margin * 2)
                    : Math.Min(420, Math.Max(320, contentWidth / 3));
                int editorX = compact || presentationLeftToRight ? margin : contentWidth - margin - editorWidth;
                int gridX = compact || !presentationLeftToRight ? margin : editorX + editorWidth + gap;
                int gridWidth = compact ? Math.Max(360, contentWidth - margin * 2)
                    : Math.Max(360, contentWidth - margin * 2 - editorWidth - gap);
                EmployeeGV.Anchor = AnchorStyles.Top | AnchorStyles.Left;
                EmployeeGV.Dock = DockStyle.None;
                int gridHeight = compact ? Math.Max(190, Math.Min(250, contentHost.ClientSize.Height / 3))
                    : Math.Max(220, contentHost.ClientSize.Height - 66);
                label10.SetBounds(gridX, 12, gridWidth, 34);
                EmployeeGV.SetBounds(gridX, 54, gridWidth, gridHeight);

                int editorTop = compact ? EmployeeGV.Bottom + 18 : 16;
                editorCard.SetBounds(editorX, editorTop, editorWidth, 600);
                int fieldWidth = Math.Max(100, editorCard.ClientSize.Width - 32);
                int y = 16;
                y += SetEditorField(label6, tb1, 16, y, fieldWidth);
                y += SetEditorField(label5, nameFrame, 16, y, fieldWidth);
                y += SetEditorField(label4, tb3, 16, y, fieldWidth);
                y += SetEditorField(label3, tb4, 16, y, fieldWidth);
                y += SetEditorField(label7, tb5, 16, y, fieldWidth);
                y += SetEditorField(label9, passwordFrame, 16, y, fieldWidth);
                y += SetEditorField(roleLabel, roleCombo, 16, y, fieldWidth);
                detailedPermissionsButton.Visible = PermissionService.CanManageDetailedPermissions();
                if (detailedPermissionsButton.Visible)
                {
                    detailedPermissionsButton.SetBounds(16, y, fieldWidth, 44);
                    DesktopComponents.StyleOutlinedFooterAction(detailedPermissionsButton);
                    y += 58;
                }
                editorCard.Height = y + 16;
                // AntdUI.Panel derives the scroll extent from its child controls.
                LayoutFooterActions();
            }
            finally
            {
                UiDirection.Apply(this, presentationLeftToRight);
                AntdThemeAdapter.Apply(editorCard);
                DesktopComponents.StyleNativeTextFrame(nameFrame, tb2);
                DesktopComponents.StyleNativeTextFrame(passwordFrame, tb6);
                foreach (Label label in new[] { label6, label5, label4, label3, label7, label9, roleLabel })
                { label.BackColor = UiTheme.Surface; label.ForeColor = UiTheme.MutedText; }
                // The shared direction pass can recreate the grid handle while resizing.
                // Reassert its explicit direction and scroll contract after
                // every layout pass instead of inheriting the parent form's former value.
                ApplyEmployeeGridDirection();
                ResumeLayout(false);
            }
        }

        private int SetEditorField(Label label, Control field, int x, int y, int width)
        {
            int captionHeight = label == null ? 26 : Math.Max(26, TextRenderer.MeasureText(label.Text, label.Font,
                new Size(width, 1000), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height + 2);
            if (label != null)
            {
                label.SetBounds(x, y, width, captionHeight);
                label.AccessibleName = label.Text;
            }

            if (field != null)
            {
                field.SetBounds(x, y + captionHeight + 4, width, 44);
            }
            return captionHeight + 4 + 44 + 8;
        }

        private void LayoutFooterActions()
        {
            const int gap = 12;
            int buttonWidth = Math.Min(122, Math.Max(104, (actionBar.ClientSize.Width - 48 - gap * 4) / 5));
            int x = presentationLeftToRight ? 24 : actionBar.ClientSize.Width - 24 - buttonWidth;
            foreach (AntdUI.Button button in new[] { button4, button3, button1, button5, button2 })
            {
                button.SetBounds(x, 13, buttonWidth, 44);
                DesktopComponents.StyleOutlinedFooterAction(button, button == button1);
                x += (presentationLeftToRight ? 1 : -1) * (buttonWidth + gap);
            }
        }

        // يغلق الشاشة الحالية فقط مثل زر إغلاق ويندوز.
        private void ConfigureRoleControls()
        {
            roleLabel = new Label
            {
                Name = "roleLabel",
                AutoSize = false,
                Location = new Point(1010, 482),
                Size = new Size(420, 22),
                Text = "الصلاحية",
                TextAlign = presentationLeftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight,
                Font = AppFonts.Create(10.2F, FontStyle.Bold),
                ForeColor = Color.FromArgb(106, 125, 120)
            };

            roleCombo = new AntdUI.Select
            {
                Name = "roleCombo",
                List = true,
                AccessibleName = Present("صلاحية الموظف"),
                AccessibleRole = AccessibleRole.ComboBox,
                Location = new Point(1010, 508),
                Size = new Size(420, 34),
                Font = AppFonts.Create(10F),
                RightToLeft = presentationLeftToRight ? RightToLeft.No : RightToLeft.Yes,
                TextAlign = presentationLeftToRight ? HorizontalAlignment.Left : HorizontalAlignment.Right,
                TabIndex = 26
            };

            roleCombo.Items.AddRange(new object[]
            {
                new AntdUI.SelectItem(Present("مدير"), "Manager"),
                new AntdUI.SelectItem(Present("كاشير"), "Cashier"),
                new AntdUI.SelectItem(Present("مسؤول المخزون"), "Inventory"),
                new AntdUI.SelectItem(Present("محاسب"), "Accountant")
            });
            AntdThemeAdapter.Apply(roleCombo);
            ResetRoleSelection();

            button4.Location = new Point(1318, 560);
            button3.Location = new Point(1194, 560);
            button1.Location = new Point(1070, 560);
            button5.Location = new Point(1318, 612);
            button2.Location = new Point(1194, 612);

            Controls.Add(roleLabel);
            Controls.Add(roleCombo);

            detailedPermissionsButton = DesktopComponents.Action("detailedPermissionsButton",
                Present("إدارة صلاحيات الشاشات والعمليات"), AppIcon.User);
            detailedPermissionsButton.Visible = PermissionService.CanManageDetailedPermissions();
            DesktopComponents.StyleOutlinedFooterAction(detailedPermissionsButton);
            detailedPermissionsButton.Click += DetailedPermissionsButton_Click;
            Controls.Add(detailedPermissionsButton);
            roleLabel.BringToFront();
            roleCombo.BringToFront();
            detailedPermissionsButton.BringToFront();
        }

        private void DetailedPermissionsButton_Click(object sender, EventArgs e)
        {
            if (previewMode) return;
            if (!PermissionService.CanManageDetailedPermissions())
            {
                AppNotifications.Show(Present("لا تملك صلاحية إدارة الصلاحيات التفصيلية."), Present("الصلاحيات"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string employeeId = tb1.Text.Trim();
            string employeeName = tb2.Text.Trim();
            if (string.IsNullOrWhiteSpace(employeeId) || string.IsNullOrWhiteSpace(employeeName))
            {
                AppNotifications.Show(Present("اختر موظفاً من الجدول أولاً، ثم افتح صلاحيات الشاشات والعمليات."), Present("الصلاحيات"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (EmployeeOperationPermissionsForm form = new EmployeeOperationPermissionsForm(employeeId, employeeName))
            {
                form.ShowDialog(this);
            }
        }

        private string GetSelectedRole()
        {
            return roleCombo == null || roleCombo.SelectedValue == null
                ? "Cashier"
                : roleCombo.SelectedValue.ToString();
        }

        private void SelectRole(string role)
        {
            string normalizedRole = string.IsNullOrWhiteSpace(role) ? "Cashier" : role.Trim();
            int selectedIndex = FindRoleIndex(normalizedRole);
            if (selectedIndex >= 0)
            {
                roleCombo.SelectedIndex = selectedIndex;
                return;
            }

            ResetRoleSelection();
        }

        private void ResetRoleSelection()
        {
            if (roleCombo != null)
            {
                roleCombo.SelectedIndex = FindRoleIndex("Cashier");
            }
        }

        private int FindRoleIndex(string role)
        {
            if (roleCombo == null) return -1;
            for (int index = 0; index < roleCombo.Items.Count; index++)
            {
                var item = roleCombo.Items[index] as AntdUI.SelectItem;
                if (item != null && string.Equals(item.Tag?.ToString(), role, StringComparison.OrdinalIgnoreCase))
                    return index;
            }
            return -1;
        }

        private void label8_Click(object sender, EventArgs e)
        {
            Close();
        }

        // يمسح الحقول ويعود للشاشة الرئيسية.
        private void button2_Click_1(object sender, EventArgs e)
        {
            // مسح الحقول بعد إضافة البيانات
            tb1.Clear();
            tb2.Clear();
            tb3.Clear();
            tb4.Clear();
            tb5.Clear();
            tb6.Clear();
            ResetRoleSelection();

            Close();
        }


        private bool ValidateEmployeeInput(out int employeeAge, out int employeeSalary, bool requirePassword)
        {
            employeeAge = 0;
            employeeSalary = 0;

            if (string.IsNullOrWhiteSpace(tb1.Text) || string.IsNullOrWhiteSpace(tb2.Text) ||
                string.IsNullOrWhiteSpace(tb3.Text) || string.IsNullOrWhiteSpace(tb4.Text) ||
                string.IsNullOrWhiteSpace(tb5.Text) || (requirePassword && string.IsNullOrWhiteSpace(tb6.Text)))
            {
                AppNotifications.Show(Present("الرجاء تعبئة جميع الحقول."), Present("الموظفون"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            if (!int.TryParse(tb3.Text, out employeeAge) || employeeAge <= 0 ||
                !int.TryParse(tb4.Text, out employeeSalary) || employeeSalary < 0)
            {
                AppNotifications.Show(Present("أدخل عمرًا موجبًا وراتبًا رقميًا غير سالب."), Present("الموظفون"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            if (!string.IsNullOrEmpty(tb6.Text))
            {
                try
                {
                    PasswordSecurity.ValidatePasswordInput(tb6.Text);
                }
                catch (ArgumentException ex)
                {
                    AppNotifications.Show(presentationLeftToRight ? Present("يجب أن تتكون كلمة المرور من 3 رموز على الأقل.") : ex.Message,
                        Present("الموظفون"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return false;
                }
            }

            return true;
        }

        // زر لإضافة موظف جديد
        private void button4_Click_1(object sender, EventArgs e)
        {
            if (previewMode) return;
            try
            {
                int empAge, empSal;
                if (!ValidateEmployeeInput(out empAge, out empSal, true))
                {
                    return;
                }

                Service.Create(ReadEmployee(empAge, empSal), CurrentActor());

                // عرض رسالة نجاح
                AppNotifications.Show(Present("تمت إضافة الموظف بنجاح."), Present("الموظفون"), MessageBoxButtons.OK, MessageBoxIcon.Information);

                // تحديث البيانات في DataGridView
                PopulateGrid(EmployeeGV); // استدعاء دالة تحديث الـ DataGridView

                // مسح الحقول بعد إضافة البيانات
                tb1.Clear();
                tb2.Clear();
                tb3.Clear();
                tb4.Clear();
                tb5.Clear();
                tb6.Clear();
                ResetRoleSelection();
            }
            catch (Exception ex)
            {
                UiTheme.ShowOperationIssue(this, Present("إضافة الموظف"), ex);
            }
        }



        // زر لتحديث بيانات موظف
        private void button3_Click(object sender, EventArgs e)
        {
            if (previewMode) return;
            try
            {
                int empAge, empSal;
                if (!ValidateEmployeeInput(out empAge, out empSal, false))
                {
                    return;
                }

                string originalEmployeeName = Service.Update(ReadEmployee(empAge, empSal), CurrentActor());
                if (originalEmployeeName == null)
                {
                    AppNotifications.Show(Present("رقم الموظف غير موجود، لا يمكن التعديل."), Present("الموظفون"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (InvalidateCurrentSessionIfTarget(originalEmployeeName))
                {
                    return;
                }

                // عرض رسالة نجاح
                AppNotifications.Show(Present("تم تعديل بيانات الموظف بنجاح."), Present("الموظفون"), MessageBoxButtons.OK, MessageBoxIcon.Information);

                PopulateGrid(EmployeeGV); // تحديث DataGridView بعد التحديث

                // مسح الحقول بعد التحديث
                tb1.Clear();
                tb2.Clear();
                tb3.Clear();
                tb4.Clear();
                tb5.Clear();
                tb6.Clear();
                ResetRoleSelection();
            }
            catch (Exception ex)
            {
                UiTheme.ShowOperationIssue(this, Present("تعديل الموظف"), ex);
            }
        }



        // زر لحذف موظف
        private void button1_Click(object sender, EventArgs e)
        {
            if (previewMode) return;
            try
            {
                if (string.IsNullOrEmpty(tb1.Text))
                {
                    AppNotifications.Show(Present("الرجاء إدخال رقم الموظف المراد حذفه."), Present("الموظفون"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // نافذة تأكيد قبل الحذف
                DialogResult result = AppNotifications.Show(Present("هل تريد حذف هذا الموظف؟"),
                                                      Present("تأكيد الحذف"),
                                                      MessageBoxButtons.YesNo,
                                                      MessageBoxIcon.Warning);

                if (result == DialogResult.Yes)
                {
                    string employeeIdToDelete = tb1.Text.Trim();

                    string originalEmployeeName = Service.Delete(employeeIdToDelete, CurrentActor());
                    if (originalEmployeeName != null)
                    {
                        if (InvalidateCurrentSessionIfTarget(originalEmployeeName))
                        {
                            return;
                        }

                        // إفراغ الحقول بعد الحذف
                        tb1.Clear();
                        tb2.Clear();
                        tb3.Clear();
                        tb4.Clear();
                        tb5.Clear();
                        tb6.Clear();
                        ResetRoleSelection();

                        AppNotifications.Show(Present("تم حذف الموظف بنجاح."), Present("الموظفون"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        AppNotifications.Show(Present("لم يتم العثور على الموظف."), Present("الموظفون"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }

                    // تحديث DataGridView بعد الحذف
                    PopulateGrid(EmployeeGV);
                }
                else
                {
                    // إذا اختار "No"، إلغاء العملية
                    AppNotifications.Show(Present("تم إلغاء الحذف."), Present("الموظفون"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                UiTheme.ShowOperationIssue(this, Present("حذف الموظف"), ex);
            }
        }


        // ملء DataGridView ببيانات الموظفين
        public void PopulateGrid(DataGridView EmployeeGV)
        {
            try
            {
                EmployeeGV.DataSource = Service.GetAll(CurrentActor());
                ApplyEmployeeGridPresentation();
            }
            catch (Exception ex)
            {
                UiTheme.ShowOperationIssue(this, Present("البحث عن موظف"), ex);
            }
        }






        // يحمل بيانات الموظفين عند فتح الشاشة.
        private void Ageny_Load(object sender, EventArgs e)
        {
            try
            {
                    // تمرير عنصر التحكم MedicineGV عند استدعاء الدالة
                    PopulateGrid(EmployeeGV);

            }
            catch (Exception ex)
            {
                // عرض رسالة خطأ إذا فشل التحميل
                UiTheme.ShowOperationIssue(this, Present("تحميل بيانات الموظفين"), ex);
            }
        }

        private void PopulateFieldsFromRow(int rowIndex)
        {
            try
            {
                if (rowIndex >= 0)
                {
                    DataGridViewRow row = EmployeeGV.Rows[rowIndex];
                    if (row.IsNewRow) return;

                    EmployeeRecord selected = row.DataBoundItem as EmployeeRecord;
                    if (selected != null)
                    {
                        tb1.Text = selected.EmployeeId;
                        tb2.Text = selected.UserName;
                        tb3.Text = selected.Age.ToString();
                        tb4.Text = selected.Salary.ToString();
                        tb5.Text = selected.Phone;

                        tb6.PasswordChar = DesktopDesignTokens.PasswordMaskCharacter;
                        tb6.Clear();
                    }
                }
            }
            catch (Exception ex)
            {
                UiTheme.ShowOperationIssue(this, Present("تحميل تفاصيل الموظف"), ex);
            }
        }

        // يملأ الحقول من الصف المحدد في جدول الموظفين.
        private void EmployeeGV_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            PopulateFieldsFromRow(e.RowIndex);
            PopulateRoleFromRow(e.RowIndex);
        }

        private void PopulateRoleFromRow(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= EmployeeGV.Rows.Count)
            {
                return;
            }

            DataGridViewRow row = EmployeeGV.Rows[rowIndex];
            EmployeeRecord selected = row.DataBoundItem as EmployeeRecord;
            if (row.IsNewRow || selected == null)
            {
                ResetRoleSelection();
                return;
            }

            SelectRole(selected.Role);
        }

        private bool InvalidateCurrentSessionIfTarget(string originalEmployeeName)
        {
            if (!string.Equals(SessionContext.CurrentUser, originalEmployeeName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            SessionContext.Clear();
            AppNotifications.Show(
                Present("تم تغيير حساب المستخدم الحالي أو حذفه، لذلك تم إنهاء الجلسة. سجّل الدخول من جديد."),
                Present("انتهت الجلسة"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            Close();
            return true;
        }

        // يمسح حقول الإدخال.
        private void button5_Click_1(object sender, EventArgs e)
        { // مسح الحقول بعد إضافة البيانات
            tb1.Clear();
            tb2.Clear();
            tb3.Clear();
            tb4.Clear();
            tb5.Clear();
            tb6.Clear();
            ResetRoleSelection();
        }

        private EmployeeInput ReadEmployee(int age, int salary)
        {
            return new EmployeeInput
            {
                EmployeeId = tb1.Text,
                UserName = tb2.Text,
                Age = age,
                Salary = salary,
                Phone = tb5.Text,
                NewPassword = tb6.Text,
                Role = GetSelectedRole()
            };
        }

        private static OperationActor CurrentActor()
        {
            return new OperationActor { UserName = SessionContext.CurrentUser, UserRole = SessionContext.CurrentRole };
        }

        private void ApplyEmployeeGridPresentation()
        {
            UiTheme.ApplyDataGridQuality(EmployeeGV);
            // ApplyDataGridQuality follows the saved application language. Preview and
            // automated presentation checks can deliberately render the opposite language,
            // so the screen contract must win after the shared theme has been applied.
            EmployeeGV.RightToLeft = presentationLeftToRight ? RightToLeft.No : RightToLeft.Yes;
            EmployeeGV.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            EmployeeGV.ScrollBars = ScrollBars.Both;
            SetHeader(EmployeeGV, nameof(EmployeeRecord.EmployeeId), "رقم الموظف");
            SetHeader(EmployeeGV, nameof(EmployeeRecord.UserName), "اسم المستخدم");
            SetHeader(EmployeeGV, nameof(EmployeeRecord.Salary), "الراتب");
            SetHeader(EmployeeGV, nameof(EmployeeRecord.Age), "العمر");
            SetHeader(EmployeeGV, nameof(EmployeeRecord.Phone), "الهاتف");
            SetHeader(EmployeeGV, nameof(EmployeeRecord.Role), "الصلاحية");
            SetHeader(EmployeeGV, nameof(EmployeeRecord.PasswordStatus), "حالة كلمة المرور");
            ConfigureGridColumn(nameof(EmployeeRecord.EmployeeId), 105F, 104);
            ConfigureGridColumn(nameof(EmployeeRecord.UserName), 190F, 140);
            ConfigureGridColumn(nameof(EmployeeRecord.Salary), 86F, 72);
            ConfigureGridColumn(nameof(EmployeeRecord.Age), 62F, 56);
            ConfigureGridColumn(nameof(EmployeeRecord.Phone), 132F, 105);
            ConfigureGridColumn(nameof(EmployeeRecord.Role), 92F, 78);
            ConfigureGridColumn(nameof(EmployeeRecord.PasswordStatus), 116F, 96);
            ApplyEmployeeGridDirection();
        }

        private void ApplyEmployeeGridDirection()
        {
            DataGridViewContentAlignment textAlignment = presentationLeftToRight
                ? DataGridViewContentAlignment.MiddleLeft : DataGridViewContentAlignment.MiddleRight;
            EmployeeGV.RightToLeft = presentationLeftToRight ? RightToLeft.No : RightToLeft.Yes;
            EmployeeGV.ColumnHeadersDefaultCellStyle.Alignment = textAlignment;
            EmployeeGV.ScrollBars = ScrollBars.Both;
            string[] logicalOrder =
            {
                nameof(EmployeeRecord.EmployeeId), nameof(EmployeeRecord.UserName),
                nameof(EmployeeRecord.Salary), nameof(EmployeeRecord.Age),
                nameof(EmployeeRecord.Phone), nameof(EmployeeRecord.Role),
                nameof(EmployeeRecord.PasswordStatus)
            };
            for (int displayIndex = 0; displayIndex < logicalOrder.Length; displayIndex++)
            {
                string propertyName = logicalOrder[displayIndex];
                if (EmployeeGV.Columns.Contains(propertyName))
                    EmployeeGV.Columns[propertyName].DisplayIndex = displayIndex;
            }
            foreach (DataGridViewColumn column in EmployeeGV.Columns)
            {
                column.HeaderCell.Style.Alignment = textAlignment;
                column.DefaultCellStyle.Alignment = column.Name == nameof(EmployeeRecord.UserName)
                    ? textAlignment : DataGridViewContentAlignment.MiddleCenter;
            }
            int headerHeight = 44;
            Font headerFont = EmployeeGV.ColumnHeadersDefaultCellStyle.Font ?? EmployeeGV.Font;
            foreach (DataGridViewColumn column in EmployeeGV.Columns)
                headerHeight = Math.Max(headerHeight, TextRenderer.MeasureText(column.HeaderText, headerFont,
                    new Size(Math.Max(30, column.Width - 16), 1000),
                    TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height + 12);
            EmployeeGV.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;
            EmployeeGV.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            EmployeeGV.ColumnHeadersHeight = headerHeight;
        }

        private void ConfigureGridColumn(string propertyName, float fillWeight, int minimumWidth)
        {
            if (!EmployeeGV.Columns.Contains(propertyName)) return;
            DataGridViewColumn column = EmployeeGV.Columns[propertyName];
            column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            column.FillWeight = fillWeight;
            column.MinimumWidth = minimumWidth;
            column.HeaderCell.Style.Alignment = presentationLeftToRight
                ? DataGridViewContentAlignment.MiddleLeft : DataGridViewContentAlignment.MiddleRight;
            column.DefaultCellStyle.Alignment = propertyName == nameof(EmployeeRecord.UserName)
                ? (presentationLeftToRight ? DataGridViewContentAlignment.MiddleLeft : DataGridViewContentAlignment.MiddleRight)
                : DataGridViewContentAlignment.MiddleCenter;
        }

        private void EmployeeGV_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            DataGridViewColumn column = EmployeeGV.Columns[e.ColumnIndex];
            if (column == null || column.Name != nameof(EmployeeRecord.Role) || e.Value == null) return;
            string code = Convert.ToString(e.Value);
            if (string.Equals(code, "Manager", StringComparison.OrdinalIgnoreCase)) e.Value = Present("مدير");
            else if (string.Equals(code, "Cashier", StringComparison.OrdinalIgnoreCase)) e.Value = Present("كاشير");
            else if (string.Equals(code, "Inventory", StringComparison.OrdinalIgnoreCase)) e.Value = Present("مسؤول المخزون");
            else if (string.Equals(code, "Accountant", StringComparison.OrdinalIgnoreCase)) e.Value = Present("محاسب");
            e.FormattingApplied = true;
        }

        private void SetHeader(DataGridView grid, string propertyName, string header)
        {
            if (grid.Columns.Contains(propertyName)) grid.Columns[propertyName].HeaderText = Present(header);
        }
    }



}

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace project_fo_3
{
    // شاشة محمية لإنشاء المدير الأول بعد إعداد قاعدة بيانات لا تحتوي على مدير.
    internal sealed class BootstrapManagerForm : Form
    {
        private readonly TextBox employeeId = CreateTextBox();
        private readonly TextBox employeeName = CreateTextBox();
        private readonly TextBox employeeAge = CreateTextBox();
        private readonly TextBox employeeSalary = CreateTextBox();
        private readonly TextBox employeePhone = CreateTextBox();
        private readonly TextBox password = CreateTextBox();
        private readonly TextBox confirmPassword = CreateTextBox();
        private readonly bool previewMode;
        private readonly bool presentationLeftToRight;
        private readonly Dictionary<string, Label> captions = new Dictionary<string, Label>(StringComparer.Ordinal);
        private readonly Dictionary<TextBox, AntdUI.Panel> inputFrames = new Dictionary<TextBox, AntdUI.Panel>();
        private Panel header;
        private Label title;
        private Label note;
        private AntdUI.Panel content;
        private AntdUI.Panel fieldsCard;
        private TableLayoutPanel fields;
        private FlowLayoutPanel actions;
        private AccessibleAntdButton saveButton;
        private AccessibleAntdButton cancelButton;

        public BootstrapManagerForm() : this(false, UiLanguageCatalog.IsEnglishActive) { }

        internal BootstrapManagerForm(bool previewMode, bool presentationLeftToRight)
        {
            this.previewMode = previewMode;
            this.presentationLeftToRight = presentationLeftToRight;
            BuildInterface();
            ApplyTheme();
            ApplyPresentation();
            UiDirection.Apply(this, presentationLeftToRight);
            UiTheme.ApplyStatusStripPresentation(this, presentationLeftToRight);
            ApplyInputDirections();
            if (previewMode) BindPreviewData();
            AcceptButton = saveButton;
            CancelButton = cancelButton;
            cancelButton.DialogResult = DialogResult.Cancel;
            Resize += (sender, args) => LayoutContent();
            LayoutContent();
        }

        private string Present(string arabic)
        {
            return presentationLeftToRight ? UiLanguageCatalog.Translate(arabic) : arabic;
        }

        private void BuildInterface()
        {
            Text = Present("إعداد المدير الأول");
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = true;
            ClientSize = new Size(620, 570);
            MinimumSize = new Size(480, 500);
            BackColor = UiTheme.Background;
            RightToLeft = presentationLeftToRight ? RightToLeft.No : RightToLeft.Yes;
            RightToLeftLayout = !presentationLeftToRight;
            Font = AppFonts.Create(9F);
            KeyPreview = true;

            header = new Panel { Name = "bootstrapManagerHeader", Dock = DockStyle.Top, Height = 88, Padding = new Padding(24, 10, 24, 8) };
            title = new Label
            {
                Name = "bootstrapManagerTitle", Dock = DockStyle.Top, Height = 36,
                Font = AppFonts.Create(14F, FontStyle.Bold), ForeColor = Color.White, BackColor = Color.Transparent,
                RightToLeft = RightToLeft.No
            };
            note = new Label
            {
                Name = "bootstrapManagerNote", Dock = DockStyle.Top, Height = 30,
                Font = AppFonts.Create(9.5F), ForeColor = Color.White, BackColor = Color.Transparent,
                RightToLeft = RightToLeft.No
            };
            header.Controls.Add(note);
            header.Controls.Add(title);

            fields = new TableLayoutPanel
            {
                Name = "bootstrapManagerFieldsGrid", Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 7,
                Padding = new Padding(20, 14, 20, 12), BackColor = UiTheme.Surface, RightToLeft = RightToLeft.No
            };
            for (int row = 0; row < 7; row++) fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 58F));

            AddField(0, "رقم الموظف", employeeId);
            AddField(1, "اسم المستخدم", employeeName);
            AddField(2, "العمر", employeeAge);
            AddField(3, "الراتب", employeeSalary);
            AddField(4, "رقم الهاتف", employeePhone);
            AddField(5, "كلمة المرور", password);
            AddField(6, "تأكيد كلمة المرور", confirmPassword);
            password.PasswordChar = DesktopDesignTokens.PasswordMaskCharacter;
            confirmPassword.PasswordChar = DesktopDesignTokens.PasswordMaskCharacter;

            fieldsCard = DesktopComponents.Card("bootstrapManagerFields");
            fieldsCard.Padding = Padding.Empty;
            fieldsCard.Controls.Add(fields);
            content = new AntdUI.Panel
            {
                Name = "bootstrapManagerContent", Dock = DockStyle.Fill, BackColor = UiTheme.Background,
                AutoScroll = true, Padding = Padding.Empty, RightToLeft = RightToLeft.No
            };
            content.Controls.Add(fieldsCard);

            saveButton = new AccessibleAntdButton
            {
                Name = "createBootstrapManager", Width = 176, Height = 44,
                IconSvg = OutlinedIcons.Svg(AppIcon.User, true), IconSize = new Size(18, 18)
            };
            cancelButton = new AccessibleAntdButton
            {
                Name = "cancelBootstrapManager", Width = 120, Height = 44,
                IconSvg = OutlinedIcons.Svg(AppIcon.Close, true), IconSize = new Size(18, 18)
            };
            saveButton.Click += SaveButton_Click;
            cancelButton.Click += (sender, args) => { DialogResult = DialogResult.Cancel; Close(); };
            actions = new FlowLayoutPanel
            {
                Name = "bootstrapManagerActions", Dock = DockStyle.Bottom, Height = 68,
                Padding = new Padding(20, 8, 20, 8), WrapContents = false, BackColor = UiTheme.Surface
            };
            actions.Controls.Add(saveButton);
            actions.Controls.Add(cancelButton);

            Controls.Add(content);
            Controls.Add(actions);
            Controls.Add(header);
            employeeId.Name = "bootstrapEmployeeId";
            employeeName.Name = "bootstrapEmployeeName";
            employeeAge.Name = "bootstrapEmployeeAge";
            employeeSalary.Name = "bootstrapEmployeeSalary";
            employeePhone.Name = "bootstrapEmployeePhone";
            password.Name = "bootstrapPassword";
            confirmPassword.Name = "bootstrapConfirmPassword";
            content.Resize += (sender, args) => LayoutContent();
            Shown += (sender, args) => LayoutContent();
            actions.RightToLeft = RightToLeft.No;
            LayoutContent();
        }

        private void AddField(int row, string arabicLabel, TextBox input)
        {
            Label label = new Label
            {
                Name = "bootstrapFieldLabel" + row, Dock = DockStyle.Fill,
                Font = AppFonts.Create(9F, FontStyle.Bold), ForeColor = UiTheme.MutedText,
                BackColor = Color.Transparent, RightToLeft = RightToLeft.No
            };
            captions[arabicLabel] = label;
            input.AccessibleRole = AccessibleRole.Text;
            input.AccessibleName = Present(arabicLabel);
            input.Dock = DockStyle.None;
            input.BorderStyle = BorderStyle.None;
            input.Margin = Padding.Empty;
            var frame = DesktopComponents.NativeTextFrame(input, "bootstrapInputFrame" + row);
            frame.Dock = DockStyle.Fill;
            frame.Margin = new Padding(4, 7, 4, 7);
            inputFrames[input] = frame;

            int labelColumn = presentationLeftToRight ? 0 : 1;
            int inputColumn = presentationLeftToRight ? 1 : 0;
            fields.Controls.Add(label, labelColumn, row);
            fields.Controls.Add(frame, inputColumn, row);
        }

        private void ApplyPresentation()
        {
            Text = Present("إعداد المدير الأول");
            title.Text = Present("إنشاء المدير الأول للنظام");
            note.Text = previewMode
                ? Present("معاينة تخطيطية فقط؛ لم يُنشأ مستخدم ولم تُفتح قاعدة البيانات.")
                : Present("هذه العملية متاحة فقط بعد إعداد قاعدة بيانات لا تحتوي على مدير.");
            title.TextAlign = presentationLeftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
            note.TextAlign = presentationLeftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
            foreach (var pair in captions)
            {
                pair.Value.Text = Present(pair.Key);
                pair.Value.TextAlign = presentationLeftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
            }
            saveButton.Text = Present("إنشاء المدير");
            saveButton.AccessibleName = Present("إنشاء المدير الأول");
            saveButton.AccessibleDescription = Present("يتحقق من الحقول ثم ينشئ حساب المدير الأول.");
            cancelButton.Text = Present("إلغاء");
            cancelButton.AccessibleName = Present("إلغاء إعداد المدير الأول");
            saveButton.IconPosition = presentationLeftToRight ? AntdUI.TAlignMini.Left : AntdUI.TAlignMini.Right;
            cancelButton.IconPosition = presentationLeftToRight ? AntdUI.TAlignMini.Left : AntdUI.TAlignMini.Right;
            saveButton.Enabled = !previewMode;
        }

        private void ApplyTheme()
        {
            UiTheme.Apply(this);
            UiTheme.ApplyDialogHeader(header, title, note, null);
            header.BackColor = title.BackColor = note.BackColor = UiTheme.Background;
            title.ForeColor = UiTheme.Text;
            note.ForeColor = UiTheme.MutedText;
            content.BackColor = UiTheme.Background;
            fieldsCard.Back = UiTheme.Surface;
            fieldsCard.BackColor = UiTheme.Background;
            fieldsCard.BorderColor = UiTheme.Border;
            fields.BackColor = UiTheme.Surface;
            actions.BackColor = UiTheme.Background;
            foreach (Label label in captions.Values)
            {
                label.ForeColor = UiTheme.MutedText;
                label.BackColor = Color.Transparent;
            }
            foreach (var pair in inputFrames)
            {
                pair.Key.BackColor = UiTheme.Surface;
                pair.Key.ForeColor = UiTheme.Text;
                pair.Value.Back = UiTheme.Surface;
                pair.Value.BackColor = UiTheme.Surface;
                DesktopComponents.StyleNativeTextFrame(pair.Value, pair.Key);
            }
            DesktopComponents.StyleOutlinedFooterAction(saveButton, false);
            DesktopComponents.StyleOutlinedFooterAction(cancelButton);
            saveButton.Font = AppFonts.Create(9.5F, FontStyle.Bold);
            cancelButton.Font = AppFonts.Create(9.5F, FontStyle.Bold);
            actions.FlowDirection = presentationLeftToRight ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;
        }

        private void ApplyInputDirections()
        {
            SetInputDirection(employeeId, false);
            SetInputDirection(employeeAge, false);
            SetInputDirection(employeeSalary, false);
            SetInputDirection(employeePhone, false);
            SetInputDirection(password, false);
            SetInputDirection(confirmPassword, false);
            SetInputDirection(employeeName, !presentationLeftToRight);
        }

        private static void SetInputDirection(TextBox input, bool rtl)
        {
            input.RightToLeft = rtl ? RightToLeft.Yes : RightToLeft.No;
            input.TextAlign = rtl ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        }

        private void BindPreviewData()
        {
            employeeId.Text = "1001";
            employeeName.Text = presentationLeftToRight ? "System Manager" : "مدير النظام";
            employeeAge.Text = "32";
            employeeSalary.Text = "150000";
            employeePhone.Text = "+967 700 000 000";
            password.Text = "admin123";
            confirmPassword.Text = "admin123";
        }

        private void LayoutContent()
        {
            if (content == null || content.ClientSize.Width <= 0) return;
            int headerWidth = Math.Max(1, ClientSize.Width - header.Padding.Horizontal);
            title.Height = Math.Max(36, TextRenderer.MeasureText(title.Text, title.Font,
                new Size(headerWidth, int.MaxValue), TextFormatFlags.WordBreak).Height + 4);
            note.Height = Math.Max(30, TextRenderer.MeasureText(note.Text, note.Font,
                new Size(headerWidth, int.MaxValue), TextFormatFlags.WordBreak).Height + 4);
            int headerHeight = title.Height + note.Height + header.Padding.Vertical;
            if (header.Height != headerHeight) header.Height = headerHeight;
            title.AccessibleName = title.Text;
            note.AccessibleName = note.Text;
            // Coordinates and column positions are already chosen per language.
            // Keep layout containers physical to avoid native RTL scroll mirroring.
            content.RightToLeft = fieldsCard.RightToLeft = fields.RightToLeft = actions.RightToLeft = RightToLeft.No;
            const int margin = 16;
            int width = Math.Min(820, Math.Max(1, content.ClientSize.Width - margin * 2 -
                SystemInformation.VerticalScrollBarWidth));
            fieldsCard.SetBounds(Math.Max(margin, (content.ClientSize.Width - width) / 2), 4, width, 434);

            fields.ColumnStyles.Clear();
            if (presentationLeftToRight)
            {
                fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34F));
                fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 66F));
            }
            else
            {
                fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 66F));
                fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34F));
            }
        }

        private static TextBox CreateTextBox()
        {
            return new TextBox { Font = AppFonts.Create(9.5F), Margin = Padding.Empty };
        }

        private void SaveButton_Click(object sender, EventArgs e)
        {
            if (previewMode) return;
            if (!int.TryParse(employeeAge.Text, out int age) || age <= 0
                || !int.TryParse(employeeSalary.Text, out int salary) || salary < 0)
            {
                AppNotifications.Show(this, Present("يرجى إدخال عمر وراتب صحيحين."), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                PasswordSecurity.ValidatePasswordInput(password.Text);
            }
            catch (ArgumentException ex)
            {
                AppNotifications.Show(this, Present(ex.Message), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!string.Equals(password.Text, confirmPassword.Text, StringComparison.Ordinal))
            {
                AppNotifications.Show(this, Present("كلمتا المرور غير متطابقتين."), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                BootstrapManagerService.CreateInitialManager(employeeId.Text, employeeName.Text, age, salary, employeePhone.Text, password.Text);
                AppNotifications.Show(this, Present("تم إنشاء المدير الأول بنجاح. سجّل الدخول بالحساب الجديد."), Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (InvalidOperationException ex)
            {
                UiTheme.ShowOperationIssue(this, Present("تهيئة قاعدة البيانات"), ex);
            }
            catch (Exception)
            {
                AppNotifications.Show(this, Present("تعذر إنشاء المدير الأول. تحقق من الاتصال والبيانات ثم أعد المحاولة."), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}

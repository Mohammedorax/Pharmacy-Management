using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using PharmacyManagement.Application;

namespace project_fo_3
{
    internal sealed class CashBoxManagementForm : Form
    {
        private sealed class EmployeeChoice
        {
            internal string Id;
            internal string Name;
            public override string ToString() => string.IsNullOrEmpty(Id) ? Name : Id + " — " + Name;
        }

        private readonly bool previewOnly;
        private readonly Func<int, bool> confirmSave;
        private bool english;
        private bool busy;
        private bool schemaAvailable = true;
        private bool reloadRequired;
        private string statusArabic;
        private string statusEnglish;
        private object[] statusArguments = new object[0];
        private bool? statusSuccess;
        private bool rebinding;
        private Guid selectedId;
        private IReadOnlyList<CashBoxRecord> saved = new CashBoxRecord[0];
        private IReadOnlyList<CashBoxInput> pendingImport;
        private readonly Label title = new Label { Name = "cashBoxTitle" };
        private readonly Label subtitle = new Label { Name = "cashBoxSubtitle" };
        private readonly Label status = new Label { Name = "cashBoxStatus" };
        private readonly Label codeLabel = new Label { Name = "cashBoxCodeLabel" };
        private readonly Label nameLabel = new Label { Name = "cashBoxNameLabel" };
        private readonly Label employeeLabel = new Label { Name = "cashBoxEmployeeLabel" };
        private readonly AntdUI.Input code = new AntdUI.Input { Name = "cashBoxCode", MaxLength = 30 };
        // Keep Windows bidi shaping until AntdUI mixed Arabic display passes.
        private readonly TextBox boxName = new TextBox { Name = "cashBoxName", MaxLength = 100, BorderStyle = BorderStyle.None };
        private readonly ComboBox employee = new ComboBox { Name = "cashBoxEmployee", DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly AntdUI.Button employeeDropdown = new AccessibleAntdButton
        {
            Name = "cashBoxEmployeeDropdown", Text = string.Empty, IconSvg = OutlinedIcons.Down(),
            IconSize = new Size(16, 16), AccessibleRole = AccessibleRole.PushButton,
            TabStop = false, BorderWidth = 0
        };
        private readonly AntdUI.Checkbox active = new AntdUI.Checkbox { Name = "cashBoxActive", Checked = true };
        private readonly DataGridView grid = new DataGridView { Name = "cashBoxGrid", ReadOnly = true,
            AllowUserToAddRows = false, AllowUserToDeleteRows = false, MultiSelect = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };
        private readonly AntdUI.Button create = DesktopComponents.Action("cashBoxNew", "جديد", AppIcon.Add);
        private readonly AntdUI.Button save = DesktopComponents.Action("cashBoxSave", "حفظ التعريف", AppIcon.Save);
        private readonly AntdUI.Button import = DesktopComponents.Action("cashBoxImport", "استيراد", AppIcon.Database);
        private readonly AntdUI.Button export = DesktopComponents.Action("cashBoxExport", "تصدير", AppIcon.Report);
        private readonly AntdUI.Button close = DesktopComponents.Action("cashBoxClose", "إغلاق", AppIcon.Close);
        private readonly Panel editor = new Panel { Name = "cashBoxEditor", Dock = DockStyle.Top };
        private readonly FlowLayoutPanel fields = new FlowLayoutPanel { Name = "cashBoxFields",
            Dock = DockStyle.Fill, WrapContents = true, AutoScroll = true, RightToLeft = RightToLeft.No };
        private readonly FlowLayoutPanel actions = new FlowLayoutPanel { Name = "cashBoxActions",
            Dock = DockStyle.Fill, WrapContents = true, AutoScroll = false, RightToLeft = RightToLeft.No };

        internal CashBoxManagementForm(bool previewOnly = false, Func<int, bool> confirmSave = null)
        {
            this.previewOnly = previewOnly;
            this.confirmSave = confirmSave ?? ConfirmSave;
            english = UiDirection.IsLeftToRight;
            Name = "CashBoxManagementForm";
            Size = new Size(880, 680);
            MinimumSize = new Size(560, 480);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = MinimizeBox = true;
            Font = AppFonts.Create(9.5F);
            BuildLayout();
            grid.Tag = UiTheme.PreserveLocalizedGridDirection;
            UiTheme.Apply(this);
            ApplyPresentation(english);
            Resize += (sender, args) => LayoutResponsive();
            Shown += async (sender, args) => await LoadDataAsync();
            FormClosing += (sender, args) =>
            {
                if (!busy) return;
                args.Cancel = true;
                SetStatus("العملية جارية؛ انتظر اكتمالها.", "Operation in progress; please wait.", false);
            };
        }

        private string T(string ar, string en) => english ? en : ar;
        private OperationActor Actor() => new OperationActor
        {
            UserName = SessionContext.CurrentUser, UserRole = SessionContext.CurrentRole
        };

        private void BuildLayout()
        {
            var header = new Panel { Name = "cashBoxHeader", Dock = DockStyle.Top, Height = 104,
                Padding = new Padding(20, 10, 20, 8) };
            title.Dock = DockStyle.Top; title.Height = 42; title.Font = AppFonts.Create(16F, FontStyle.Bold);
            subtitle.Dock = DockStyle.Fill;
            header.Controls.Add(subtitle); header.Controls.Add(title);

            var footer = new Panel { Name = "cashBoxFooter", Dock = DockStyle.Bottom, Height = 68 };
            actions.Padding = new Padding(16, 4, 16, 4);
            foreach (AntdUI.Button button in new[] { create, save, import, export, close })
            {
                button.Size = new Size(130, 44);
                button.Margin = new Padding(4, 0, 4, 4);
                DesktopComponents.StyleOutlinedFooterAction(button);
                actions.Controls.Add(button);
            }
            create.Click += (sender, args) => ClearEditor();
            save.Click += async (sender, args) => await SaveAsync();
            import.Click += async (sender, args) => await ImportPreviewAsync();
            export.Click += async (sender, args) => await ExportAsync();
            close.Click += (sender, args) => Close();
            footer.Controls.Add(actions);

            status.Dock = DockStyle.Bottom; status.Height = 52;
            status.Padding = new Padding(20, 8, 20, 8);

            editor.Padding = new Padding(16, 8, 16, 6);
            foreach (var pair in new[]
            {
                new { Label = codeLabel, Input = (Control)code, Key = "code" },
                new { Label = nameLabel, Input = (Control)boxName, Key = "name" },
                new { Label = employeeLabel, Input = (Control)employee, Key = "employee" }
            })
            {
                var block = new Panel { Name = "cashBoxField_" + pair.Key, Size = new Size(255, 70), Margin = new Padding(4) };
                pair.Label.Dock = DockStyle.Top; pair.Label.Height = 25;
                if (pair.Input is AntdUI.Input textInput)
                {
                    textInput.Dock = DockStyle.Fill;
                    textInput.Margin = Padding.Empty;
                    textInput.Radius = DesktopDesignTokens.FieldRadius;
                    block.Controls.Add(textInput);
                }
                else if (pair.Input is TextBox nativeName)
                {
                    AntdUI.Panel frame = DesktopComponents.NativeTextFrame(nativeName, "cashBoxFrame_" + pair.Key);
                    frame.Dock = DockStyle.Fill;
                    block.Controls.Add(frame);
                    nativeName.TextChanged += (sender, args) => UiDirection.ApplyContentDirection(nativeName, english);
                }
                else
                {
                    var frame = new AntdUI.Panel { Name = "cashBoxFrame_" + pair.Key,
                        Dock = DockStyle.Fill, Radius = DesktopDesignTokens.FieldRadius, BorderWidth = 1 };
                    pair.Input.Parent = frame;
                    pair.Input.Anchor = AnchorStyles.Left | AnchorStyles.Right;
                    frame.Resize += (sender, args) =>
                    {
                        int inputHeight = pair.Input is TextBox nativeText ? nativeText.PreferredHeight : 28;
                        pair.Input.SetBounds(10, Math.Max(0, (frame.ClientSize.Height - inputHeight) / 2),
                            Math.Max(1, frame.ClientSize.Width - 20), inputHeight);
                        if (pair.Input != employee) return;
                        int arrowWidth = 32;
                        int arrowX = employee.RightToLeft == RightToLeft.Yes ?
                            employee.Left + 1 : employee.Right - arrowWidth - 1;
                        employeeDropdown.SetBounds(arrowX, employee.Top + 1, arrowWidth, Math.Max(1, employee.Height - 2));
                    };
                    if (pair.Input == employee)
                    {
                        frame.Controls.Add(employeeDropdown);
                        employeeDropdown.BringToFront();
                        employeeDropdown.Click += (sender, args) => { employee.Focus(); employee.DroppedDown = true; };
                        employee.EnabledChanged += (sender, args) => employeeDropdown.Enabled = employee.Enabled;
                    }
                    else
                    {
                        pair.Input.Enter += (sender, args) => frame.BorderColor = UiTheme.Primary;
                        pair.Input.Leave += (sender, args) => frame.BorderColor = UiTheme.Border;
                    }
                    frame.Shadow = 0;
                    block.Controls.Add(frame);
                }
                block.Controls.Add(pair.Label);
                fields.Controls.Add(block);
            }
            var activePanel = new AntdUI.Panel { Name = "cashBoxActivePanel", Size = new Size(180, 70),
                Margin = new Padding(4), Padding = new Padding(12), Radius = DesktopDesignTokens.FieldRadius,
                BorderWidth = 1 };
            active.Dock = DockStyle.Fill; activePanel.Controls.Add(active); fields.Controls.Add(activePanel);
            editor.Controls.Add(fields);

            var body = new Panel { Name = "cashBoxBody", Dock = DockStyle.Fill, Padding = new Padding(20, 8, 20, 0) };
            grid.Dock = DockStyle.Fill;
            grid.RowHeadersVisible = false;
            // Reformatting can recreate the grid handle and select its current row.
            // Only an interaction in the grid may replace the user's editor values.
            grid.SelectionChanged += (sender, args) =>
            {
                if (grid.ContainsFocus) PopulateSelection();
            };
            body.Controls.Add(grid);
            Controls.Add(body); Controls.Add(editor); Controls.Add(status); Controls.Add(footer); Controls.Add(header);
            LayoutResponsive();
        }

        internal void ApplyPresentation(bool leftToRight)
        {
            english = leftToRight;
            Text = title.Text = T("إدارة صناديق النقد", "Cashboxes");
            subtitle.Text = T("تعريف الصناديق وإسناد كاشير لها. الأرصدة والقيود القديمة غير منسوبة بعد.",
                "Define cashboxes and assign cashiers. Existing cash balances and historical journals are not assigned.");
            codeLabel.Text = T("رمز الصندوق", "Cashbox code");
            nameLabel.Text = T("اسم الصندوق", "Cashbox name");
            employeeLabel.Text = T("الكاشير", "Cashier");
            active.Text = T("نشط", "Active");
            create.Text = T("جديد", "New");
            save.Text = pendingImport == null ? T("حفظ التعريف", "Save definition") :
                T("اعتماد التعريفات", "Apply definitions");
            import.Text = T("استيراد", "Import");
            export.Text = T("تصدير", "Export");
            close.Text = T("إغلاق", "Close");
            foreach (AntdUI.Button button in new[] { create, save, import, export, close })
                button.AccessibleName = button.Text;
            code.AccessibleName = codeLabel.Text;
            boxName.AccessibleName = nameLabel.Text;
            employee.AccessibleName = employeeLabel.Text;
            employeeDropdown.AccessibleName = T("فتح قائمة الكاشير", "Open cashier list");
            grid.AccessibleName = T("جدول تعريفات صناديق النقد", "Cashbox definitions table");
            title.TextAlign = subtitle.TextAlign = status.TextAlign = leftToRight ?
                ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
            foreach (Label label in new[] { codeLabel, nameLabel, employeeLabel })
                label.TextAlign = leftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
            code.RightToLeft = RightToLeft.No;
            boxName.RightToLeft = leftToRight ? RightToLeft.No : RightToLeft.Yes;
            employee.RightToLeft = leftToRight ? RightToLeft.No : RightToLeft.Yes;
            UiDirection.Apply(this, leftToRight);
            UiDirection.ApplyContentDirection(boxName, leftToRight);
            // Explicit physical flow avoids a second RTL mirror in nested WinForms containers.
            fields.RightToLeft = actions.RightToLeft = RightToLeft.No;
            fields.FlowDirection = actions.FlowDirection = leftToRight ?
                FlowDirection.LeftToRight : FlowDirection.RightToLeft;
            UiTheme.UseDedicatedStatus(this);
            ApplyVisualTheme();
            grid.RightToLeft = leftToRight ? RightToLeft.No : RightToLeft.Yes;
            ShowRows(pendingImport);
            PresentStatus();
            LayoutResponsive();
        }

        private void ApplyVisualTheme()
        {
            BackColor = UiTheme.Background;
            foreach (Control area in new[] { Controls["cashBoxHeader"], Controls["cashBoxFooter"], Controls["cashBoxBody"], editor, fields, actions })
                area.BackColor = UiTheme.Background;
            title.ForeColor = UiTheme.Text;
            subtitle.ForeColor = status.ForeColor = UiTheme.MutedText;
            foreach (Label label in new[] { codeLabel, nameLabel, employeeLabel })
                label.ForeColor = UiTheme.Text;
            foreach (Control block in fields.Controls)
            {
                block.BackColor = UiTheme.Background;
                var frame = block.Controls.OfType<AntdUI.Panel>().FirstOrDefault();
                if (frame == null) continue;
                frame.Back = UiTheme.Surface; frame.BackColor = UiTheme.Surface;
                frame.BorderColor = UiTheme.Border;
            }
            AntdThemeAdapter.Apply(code);
            DesktopComponents.StyleNativeTextFrame((AntdUI.Panel)boxName.Parent, boxName);
            code.Radius = DesktopDesignTokens.FieldRadius;
            employee.BackColor = UiTheme.Surface;
            employee.ForeColor = UiTheme.Text;
            employee.FlatStyle = FlatStyle.Flat;
            employeeDropdown.DefaultBack = UiTheme.Surface;
            employeeDropdown.BackColor = UiTheme.Surface;
            employeeDropdown.ForeColor = UiTheme.MutedText;
            employeeDropdown.BackHover = UiTheme.Selection;
            employeeDropdown.BorderWidth = 0;
            foreach (AntdUI.Button button in new[] { create, save, import, export, close })
                DesktopComponents.StyleOutlinedFooterAction(button);
            var activePanel = fields.Controls.OfType<AntdUI.Panel>().FirstOrDefault(panel => panel.Name == "cashBoxActivePanel");
            if (activePanel != null)
            {
                activePanel.Back = UiTheme.Surface;
                activePanel.BackColor = UiTheme.Surface;
                activePanel.BorderColor = UiTheme.Border;
                activePanel.Radius = DesktopDesignTokens.FieldRadius;
            }
            AntdThemeAdapter.Apply(active);
            UiTheme.ApplyTo(grid);
            UiTheme.ApplyDataGridQuality(grid, T("لا توجد صناديق نقد معرفة.", "No cashboxes defined."));
            // Two-line Arabic/English headers must remain readable at compact widths.
            grid.ColumnHeadersHeight = 48;
        }

        private void LayoutResponsive()
        {
            if (editor.Parent == null) return;
            bool compact = ClientSize.Width < 810;
            Control header = Controls["cashBoxHeader"];
            header.Padding = compact ? new Padding(20, 4, 20, 4) : new Padding(20, 10, 20, 8);
            editor.Padding = compact ? new Padding(16, 4, 16, 4) : new Padding(16, 8, 16, 6);
            title.Height = compact ? Math.Max(32, TextRenderer.MeasureText(title.Text, title.Font, Size.Empty, TextFormatFlags.NoPadding).Height) : 42;
            int hintHeight = TextRenderer.MeasureText(subtitle.Text, subtitle.Font,
                new Size(Math.Max(1, ClientSize.Width - header.Padding.Horizontal), int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.NoPadding).Height;
            header.Height = Math.Max(compact ? 74 : 104, header.Padding.Vertical + title.Height + hintHeight);
            editor.Height = compact ? 172 : 102;
            status.Padding = compact ? new Padding(20, 3, 20, 3) : new Padding(20, 8, 20, 8);
            int statusTextHeight = TextRenderer.MeasureText(status.Text ?? string.Empty, status.Font,
                new Size(Math.Max(1, ClientSize.Width - status.Padding.Horizontal), int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height;
            status.Height = Math.Max(compact ? 28 : 52, statusTextHeight + status.Padding.Vertical + 4);
            Controls["cashBoxBody"].Padding = compact ? new Padding(20, 0, 20, 0) : new Padding(20, 8, 20, 0);
            int actionRows = 1, used = 0;
            int actionWidth = Math.Max(1, actions.ClientSize.Width - actions.Padding.Horizontal);
            foreach (AntdUI.Button button in actions.Controls.OfType<AntdUI.Button>())
            {
                button.Width = Math.Max(100, TextRenderer.MeasureText(button.Text, button.Font).Width + 36);
                int occupied = button.Width + button.Margin.Horizontal;
                if (used > 0 && used + occupied > actionWidth) { actionRows++; used = 0; }
                used += occupied;
            }
            Controls["cashBoxFooter"].Height = actions.Padding.Vertical + actionRows * 48;
            int minimumBody = grid.ColumnHeadersHeight + grid.RowTemplate.Height +
                Controls["cashBoxBody"].Padding.Vertical + SystemInformation.HorizontalScrollBarHeight + 2;
            editor.Height = Math.Max(88, Math.Min(compact ? 172 : 102,
                ClientSize.Height - header.Height - status.Height - Controls["cashBoxFooter"].Height - minimumBody));
            fields.SuspendLayout();
            try
            {
                int available = Math.Max(1, fields.ClientSize.Width - fields.Padding.Horizontal - 20);
                int columns = compact ? 2 : 4;
                int width = Math.Max(180, available / columns - 10);
                foreach (Control block in fields.Controls)
                    block.Width = width;
                if (!compact && fields.Controls.Count == 4)
                {
                    int usable = Math.Max(1, fields.ClientSize.Width - 48);
                    fields.Controls[0].Width = (int)(usable * .20);
                    fields.Controls[1].Width = (int)(usable * .29);
                    fields.Controls[2].Width = (int)(usable * .36);
                    fields.Controls[3].Width = usable - fields.Controls[0].Width -
                        fields.Controls[1].Width - fields.Controls[2].Width;
                }
            }
            finally { fields.ResumeLayout(true); }
        }

        private async Task LoadDataAsync()
        {
            if (previewOnly)
            {
                saved = new[] { new CashBoxRecord { CashBoxId = Guid.NewGuid(), Code = "MAIN-1",
                    Name = "الصندوق الرئيسي", IsActive = true, EmployeeId = "E001", EmployeeName = "كاشير تجريبي" } };
                employee.Items.Add(new EmployeeChoice { Name = T("دون إسناد", "Unassigned") });
                employee.Items.Add(new EmployeeChoice { Id = "E001", Name = T("كاشير تجريبي", "Sample cashier") });
                employee.SelectedIndex = 0;
                ShowRows(null);
                save.Enabled = import.Enabled = export.Enabled = false;
                SetStatus("معاينة مرئية؛ لا اتصال بقاعدة البيانات ولا حفظ.", "Visual preview; no database connection or save.");
                return;
            }
            SetBusy(true);
            try
            {
                OperationActor actor = Actor();
                var result = await Task.Run(() => new
                {
                    Boxes = ApplicationServices.CashBoxes.GetAll(actor),
                    Employees = ApplicationServices.Employees.GetAll(actor)
                });
                saved = result.Boxes;
                reloadRequired = false;
                schemaAvailable = true;
                employee.Items.Clear();
                employee.Items.Add(new EmployeeChoice { Name = T("دون إسناد", "Unassigned") });
                foreach (EmployeeRecord item in result.Employees)
                    employee.Items.Add(new EmployeeChoice { Id = item.EmployeeId, Name = item.UserName });
                employee.SelectedIndex = 0;
                ShowRows(null);
                SetStatus("التعريفات جاهزة؛ إسناد الصندوق لا ينشئ رصيدًا نقديًا.",
                    "Definitions loaded; assigning a cashbox does not create a cash balance.", true);
            }
            catch (Exception error)
            {
                if (error is SqlException sql && sql.Number == 208)
                {
                    schemaAvailable = false;
                    SetStatus("تعريف صناديق النقد يحتاج ترحيل قاعدة البيانات؛ تواصل مع المدير التقني قبل الاستخدام.",
                        "Cashbox definitions require a database migration before use.", false);
                }
                else
                {
                    SetStatus("تعذر تحميل الصناديق؛ تحقق من صلاحية المدير والاتصال.",
                        "Could not load cashboxes; check manager access and connection.", false);
                    UiTheme.ShowOperationIssue(this, Text, error, english);
                }
            }
            finally { SetBusy(false); }
        }

        private void ShowRows(IReadOnlyList<CashBoxInput> imported)
        {
            var table = new DataTable();
            table.Columns.Add("CashBoxId", typeof(Guid));
            table.Columns.Add(T("الرمز", "Code"));
            table.Columns.Add(T("اسم الصندوق", "Cashbox"));
            table.Columns.Add(T("الحالة", "Status"));
            table.Columns.Add(T("رقم الكاشير", "Cashier ID"));
            table.Columns.Add(T("اسم الكاشير", "Cashier"));
            if (imported == null)
                foreach (CashBoxRecord box in saved)
                    table.Rows.Add(box.CashBoxId, box.Code, box.Name,
                        box.IsActive ? T("نشط", "Active") : T("معطل", "Inactive"),
                        box.EmployeeId ?? string.Empty, box.EmployeeName ?? string.Empty);
            else
                foreach (CashBoxInput box in imported)
                    table.Rows.Add(box.CashBoxId, box.Code, box.Name,
                        box.IsActive ? T("نشط", "Active") : T("معطل", "Inactive"),
                        box.EmployeeId ?? string.Empty, string.Empty);
            rebinding = true;
            try
            {
                DataTable previous = grid.DataSource as DataTable;
                grid.DataSource = table;
                previous?.Dispose();
                grid.Columns["CashBoxId"].Visible = false;
                // Binding can recreate columns after the parent has been mirrored.
                grid.RightToLeft = english ? RightToLeft.No : RightToLeft.Yes;
                for (int index = 0; index < grid.Columns.Count; index++)
                    grid.Columns[index].DisplayIndex = index;
                grid.ClearSelection();
            }
            finally { rebinding = false; }
        }

        private void PopulateSelection()
        {
            if (rebinding || reloadRequired || pendingImport != null || grid.SelectedRows.Count != 1 ||
                !(grid.SelectedRows[0].DataBoundItem is DataRowView row))
                return;
            Guid id = (Guid)row["CashBoxId"];
            CashBoxRecord box = saved.FirstOrDefault(item => item.CashBoxId == id);
            if (box == null) return;
            selectedId = id;
            code.Text = box.Code; boxName.Text = box.Name; active.Checked = box.IsActive;
            for (int index = 0; index < employee.Items.Count; index++)
                if (string.Equals(((EmployeeChoice)employee.Items[index]).Id ?? string.Empty,
                    box.EmployeeId ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                { employee.SelectedIndex = index; break; }
        }

        private void ClearEditor()
        {
            if (reloadRequired) return;
            pendingImport = null; selectedId = Guid.Empty; grid.ClearSelection();
            code.Clear(); boxName.Clear(); active.Checked = true; employee.SelectedIndex = employee.Items.Count == 0 ? -1 : 0;
            save.Text = T("حفظ التعريف", "Save definition");
            ShowRows(null); code.Focus();
        }

        private async Task SaveAsync()
        {
            if (previewOnly || busy || reloadRequired || !schemaAvailable) return;
            IReadOnlyList<CashBoxInput> batch = pendingImport ?? new[]
            {
                new CashBoxInput { CashBoxId = selectedId, Code = code.Text, Name = boxName.Text,
                    IsActive = active.Checked, EmployeeId = (employee.SelectedItem as EmployeeChoice)?.Id }
            };
            if (pendingImport == null && (string.IsNullOrWhiteSpace(code.Text) || string.IsNullOrWhiteSpace(boxName.Text)))
            {
                SetStatus("أدخل رمز الصندوق واسمه أولًا.", "Enter the cashbox code and name first.", false);
                return;
            }
            if (!confirmSave(batch.Count)) return;
            SetBusy(true);
            try
            {
                OperationActor actor = Actor();
                CashBoxSaveCompletion completion = await CashBoxSaveWorkflow.ExecuteAsync(
                    () => Task.Run(() => ApplicationServices.CashBoxes.SaveBatch(batch, actor)),
                    () => Task.Run(() => ApplicationServices.CashBoxes.GetAll(actor)));
                pendingImport = null; selectedId = Guid.Empty;
                if (completion.ReloadError != null)
                {
                    ApplyConfirmedReloadFailure(completion.ReloadError);
                    return;
                }
                saved = completion.Boxes;
                ShowRows(null);
                save.Text = T("حفظ التعريف", "Save definition");
                SetStatus("حُفظت التعريفات والإسنادات؛ لم يتغير الرصيد النقدي.",
                    "Definitions and assignments saved; cash balances were unchanged.", true);
            }
            catch (Exception error)
            {
                SetStatus("تعذر تأكيد الحفظ؛ راجع التعريفات قبل إعادة المحاولة.",
                    "Save could not be confirmed; review definitions before retrying.", false);
                UiTheme.ShowOperationIssue(this, Text, error, english);
            }
            finally { SetBusy(false); }
        }

        private bool ConfirmSave(int count)
        {
            return AppNotifications.Show(this, string.Format(T(
                "حفظ {0} تعريف صندوق وإسناده؟ لن تُنشأ أرصدة أو قيود نقدية.",
                "Save {0} cashbox definitions and assignments? No balance or journal will be created."), count),
                Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
        }

        private async Task ImportPreviewAsync()
        {
            if (previewOnly || busy || reloadRequired || !schemaAvailable) return;
            using (var picker = new OpenFileDialog { Title = T("استيراد تعريفات الصناديق", "Import cashbox definitions"),
                Filter = "Excel Workbook (*.xlsx)|*.xlsx" })
            {
                if (picker.ShowDialog(this) != DialogResult.OK) return;
                await LoadFileAsync(picker.FileName);
            }
        }

        internal async Task LoadFileAsync(string path)
        {
            if (previewOnly || busy || reloadRequired || !schemaAvailable) return;
            IReadOnlyList<CashBoxRecord> existing = saved;
            SetBusy(true);
            try
            {
                pendingImport = await Task.Run(() =>
                {
                    using (DataTable sheet = ExcelWorkbookReader.Read(path))
                        return CashBoxWorkbook.Parse(sheet, existing);
                });
                ShowRows(pendingImport);
                save.Text = T("اعتماد التعريفات", "Apply definitions");
                SetStatus("عُرض {0} تعريف للمراجعة. الاعتماد يحفظها دفعة واحدة دون ترحيل نقدي.",
                    "Previewing {0} definitions. Apply saves them in one transaction without cash posting.",
                    true, pendingImport.Count);
            }
            catch (Exception error)
            {
                pendingImport = null; ShowRows(null);
                save.Text = T("حفظ التعريف", "Save definition");
                SetStatus("تعذرت معاينة الملف؛ لم تُحفظ تعريفات جديدة.",
                    "File preview failed; no new definitions were saved.", false);
                UiTheme.ShowOperationIssue(this, Text, error, english);
            }
            finally { SetBusy(false); }
        }

        private async Task ExportAsync()
        {
            if (previewOnly || busy || reloadRequired || !schemaAvailable) return;
            using (var picker = new SaveFileDialog { Title = T("تصدير تعريفات الصناديق", "Export cashbox definitions"),
                Filter = "Excel Workbook (*.xlsx)|*.xlsx", DefaultExt = "xlsx", AddExtension = true,
                FileName = "cashboxes-" + DateTime.Today.ToString("yyyyMMdd") + ".xlsx" })
            {
                if (picker.ShowDialog(this) != DialogResult.OK) return;
                SetBusy(true);
                try
                {
                    OperationActor actor = Actor();
                    string target = picker.FileName;
                    int count = await Task.Run(() =>
                    {
                        IReadOnlyList<CashBoxRecord> boxes = ApplicationServices.CashBoxes.GetAll(actor);
                        using (DataTable table = CashBoxWorkbook.CreateExport(boxes))
                        {
                            return ExcelWorkbookWriter.WriteCompleteFile(table, "CashBoxes", target);
                        }
                    });
                    SetStatus("صُدّر {0} صندوق؛ لا يحتوي الملف أرصدة مالية.",
                        "Exported {0} cashboxes; the file contains no balances.", true, count);
                }
                catch (Exception error)
                {
                    SetStatus("تعذر التصدير؛ راجع المسار والاتصال قبل إعادة المحاولة.",
                        "Export failed; check the path and connection before retrying.", false);
                    UiTheme.ShowOperationIssue(this, Text, error, english);
                }
                finally { SetBusy(false); }
            }
        }

        private void SetBusy(bool value)
        {
            busy = value;
            foreach (Control control in new Control[] { code, boxName, employee, employeeDropdown, active, create, save, import, export, close, grid })
                control.Enabled = !value && (!previewOnly || control == grid) &&
                    ((schemaAvailable && !reloadRequired) || control == close || control == grid);
        }

        private void ApplyConfirmedReloadFailure(Exception error)
        {
            // Preserve the completed write, but do not reuse stale definitions for another write.
            ClearEditor();
            reloadRequired = true;
            AppDiagnostics.Log("CashBoxReloadAfterSave", error);
            SetStatus("حُفظت تعريفات الصناديق، لكن تعذر تحديث القائمة. أغلق الشاشة وافتحها مجددًا قبل أي إعادة محاولة.",
                "Cashbox definitions were saved, but the list could not refresh. Reopen this screen before retrying.");
            SetBusy(busy);
        }
        private void SetStatus(string arabic, string englishText, bool? success = null, params object[] arguments)
        {
            statusArabic = arabic; statusEnglish = englishText;
            statusArguments = arguments; statusSuccess = success;
            PresentStatus(); LayoutResponsive();
        }
        private void PresentStatus()
        {
            UiTheme.SetStatus(status, string.Format(T(statusArabic ?? string.Empty, statusEnglish ?? string.Empty), statusArguments), statusSuccess);
            status.AccessibleName = status.Text;
            status.AccessibleRole = AccessibleRole.StatusBar;
            status.AutoEllipsis = false; status.UseMnemonic = false;
        }
    }

    internal sealed class CashBoxSaveCompletion
    {
        internal CashBoxSaveCompletion(IReadOnlyList<CashBoxRecord> boxes, Exception reloadError)
        {
            Boxes = boxes;
            ReloadError = reloadError;
        }

        internal IReadOnlyList<CashBoxRecord> Boxes { get; private set; }
        internal Exception ReloadError { get; private set; }
    }

    internal static class CashBoxSaveWorkflow
    {
        internal static async Task<CashBoxSaveCompletion> ExecuteAsync(
            Func<Task> save, Func<Task<IReadOnlyList<CashBoxRecord>>> reload)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));
            if (reload == null) throw new ArgumentNullException(nameof(reload));
            // A save failure is uncertain and still propagates. Once save returns,
            // a reload failure must not be reported as a failed database commit.
            await save();
            try
            {
                IReadOnlyList<CashBoxRecord> boxes = await reload();
                if (boxes == null) throw new InvalidOperationException("قائمة الصناديق بعد الحفظ غير متاحة.");
                return new CashBoxSaveCompletion(boxes, null);
            }
            catch (Exception error)
            {
                return new CashBoxSaveCompletion(null, error);
            }
        }
    }
}

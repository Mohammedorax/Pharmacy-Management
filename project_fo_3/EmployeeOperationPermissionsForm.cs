using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using PharmacyManagement.Application;

namespace project_fo_3
{
    internal sealed class EmployeeOperationPermissionsForm : Form
    {
        private IEmployeePermissionService Service => ApplicationServices.EmployeePermissions;
        private const string DefaultChoice = "افتراضي حسب الدور";
        private const string AllowChoice = "سماح";
        private const string DenyChoice = "منع";

        private readonly string employeeId;
        private readonly string employeeName;
        private readonly bool previewMode;
        private bool leftToRight;
        private string statusArabic;
        private object[] statusValues;
        private bool? statusSuccess;
        private bool permissionsEditable;
        private Dictionary<string, bool> savedOverrides;
        private const string EditHint = "اختر التجاوزات المطلوبة ثم احفظ. تُراجع عند المحاولة التالية، وقد يتأخر التحديث حتى خمس ثوانٍ.";
        private readonly DataGridView grid = new DataGridView();
        private readonly TextBox search = new TextBox { Name = "permissionsSearch", MaxLength = 200, TabIndex = 0 };
        private readonly AntdUI.Panel searchFrame;
        private readonly Label searchCaption = new Label();
        private readonly Label status = new Label();
        private readonly AntdUI.Button saveButton = new AccessibleAntdButton();
        private readonly AntdUI.Button closeButton = new AccessibleAntdButton();

        public EmployeeOperationPermissionsForm(string employeeId, string employeeName)
            : this(employeeId, employeeName, false)
        {
        }

        internal EmployeeOperationPermissionsForm(string employeeId, string employeeName, bool previewMode)
        {
            this.employeeId = employeeId == null ? string.Empty : employeeId.Trim();
            this.employeeName = employeeName == null ? string.Empty : employeeName.Trim();
            this.previewMode = previewMode;
            leftToRight = UiDirection.IsLeftToRight;

            Text = "صلاحيات الشاشات والعمليات";
            Name = "EmployeeOperationPermissionsForm";
            Size = new Size(860, 630);
            MinimumSize = new Size(620, 500);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = true;
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            BackColor = UiTheme.Background;
            Font = AppFonts.Create(9.5F);
            KeyPreview = true;

            searchFrame = DesktopComponents.NativeTextFrame(search, "permissionsSearchFrame");
            BuildLayout();
            Resize += (sender, args) => { LayoutHeader(); LayoutPermissionColumns(); };
            UiTheme.Apply(this);
            StartPosition = FormStartPosition.CenterParent;
            StyleShell();
            DesktopComponents.StyleOutlinedFooterAction(saveButton);
            DesktopComponents.StyleOutlinedFooterAction(closeButton);
            search.TextChanged += (sender, args) =>
            {
                UiDirection.ApplyContentDirection(search, leftToRight);
                ApplyFilter();
            };
            KeyDown += (sender, args) =>
            {
                if (args.Control && args.KeyCode == Keys.F)
                { search.Focus(); search.SelectAll(); args.Handled = true; args.SuppressKeyPress = true; }
            };
            ApplyPresentation(leftToRight);
            FormClosing += (sender, args) =>
            {
                if (!previewMode && !ConfirmPendingChanges(() =>
                    UnsavedChangesDialog.ConfirmDiscard(this, leftToRight))) args.Cancel = true;
            };
            if (previewMode)
            {
                saveButton.Enabled = false;
                PopulateRows("Cashier", new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase));
                SetStatus("معاينة تخطيط فقط؛ الحفظ غير متاح.", null);
            }
            else Load += (s, e) => LoadPermissions();
            RefreshEditState();
        }

        private sealed class PermissionChoice
        {
            public string Code { get; set; }
            public string Text { get; set; }
        }

        private string Localize(string arabicText)
        { return leftToRight ? UiLanguageCatalog.Translate(arabicText) : arabicText; }

        private void SetStatus(string arabicText, bool? success)
        {
            statusArabic = arabicText;
            statusValues = null;
            statusSuccess = success;
            UiTheme.SetStatus(status, Localize(arabicText), success);
        }

        private void SetStatusFormat(string template, bool? success, params object[] values)
        {
            statusArabic = template;
            statusValues = (object[])values.Clone();
            statusSuccess = success;
            UiTheme.SetStatus(status, UiLanguageCatalog.Format(leftToRight, template, statusValues), success);
        }

        internal void ApplyPresentation(bool useLeftToRight)
        {
            leftToRight = useLeftToRight;
            Text = Localize("صلاحيات الشاشات والعمليات");
            Label title = (Label)Controls.Find("permissionsTitle", true)[0];
            title.Text = string.Format(Localize("صلاحيات الشاشات والعمليات للموظف: {0}"), employeeName);
            ((Label)Controls.Find("permissionsSubtitle", true)[0]).Text = Localize(
                "حدّد سماحًا أو منعًا عند الحاجة؛ الإعدادات وإدارة الموظفين للمدير فقط.");
            grid.Columns["Operation"].HeaderText = Localize("الشاشة أو العملية");
            grid.Columns["RoleDefault"].HeaderText = Localize("الدور");
            grid.Columns["Override"].HeaderText = Localize("تجاوز للموظف");
            grid.Columns["Effective"].HeaderText = Localize("النتيجة الفعلية");
            grid.Columns["Operation"].DefaultCellStyle.Alignment = leftToRight
                ? DataGridViewContentAlignment.MiddleLeft : DataGridViewContentAlignment.MiddleRight;
            grid.Columns["Operation"].HeaderCell.Style.Alignment = leftToRight
                ? DataGridViewContentAlignment.MiddleLeft : DataGridViewContentAlignment.MiddleRight;
            ((DataGridViewComboBoxColumn)grid.Columns["Override"]).DataSource = new[]
            {
                new PermissionChoice { Code = DefaultChoice, Text = Localize(DefaultChoice) },
                new PermissionChoice { Code = AllowChoice, Text = Localize(AllowChoice) },
                new PermissionChoice { Code = DenyChoice, Text = Localize(DenyChoice) }
            };
            foreach (DataGridViewRow row in grid.Rows)
            {
                string arabicName = row.Cells["Operation"].Tag as string;
                if (arabicName != null) row.Cells["Operation"].Value = arabicName.StartsWith("شاشة: ", StringComparison.Ordinal)
                    ? Localize("شاشة:") + " " + Localize(arabicName.Substring("شاشة: ".Length)) : Localize(arabicName);
                row.Cells["Operation"].ToolTipText = Convert.ToString(row.Cells["Operation"].Value);
                if (row.Cells["RoleDefault"].Tag is bool)
                    row.Cells["RoleDefault"].Value = Localize((bool)row.Cells["RoleDefault"].Tag ? "مسموح" : "غير مسموح");
                UpdateEffectiveCell(row);
            }
            grid.AccessibleName = Localize("جدول تجاوزات صلاحيات الشاشات والعمليات");
            status.AccessibleName = Localize("حالة صلاحيات الشاشات والعمليات");
            saveButton.Text = Localize("حفظ الصلاحيات");
            saveButton.AccessibleName = Localize("حفظ صلاحيات الشاشات والعمليات");
            closeButton.Text = Localize("إغلاق");
            closeButton.AccessibleName = Localize("إغلاق صلاحيات الشاشات والعمليات");
            UiDirection.Apply(this, leftToRight);
            saveButton.IconPosition = leftToRight ? AntdUI.TAlignMini.Left : AntdUI.TAlignMini.Right;
            StyleShell();
            search.AccessibleName = leftToRight ? "Find a screen or operation permission" : "بحث عن صلاحية شاشة أو عملية";
            searchCaption.Dock = leftToRight ? DockStyle.Left : DockStyle.Right;
            UiDirection.ApplyContentDirection(search, leftToRight);
            DesktopComponents.StyleNativeTextFrame(searchFrame, search);
            ApplyFilter();
            LayoutPermissionColumns();
            if (statusArabic != null) UiTheme.SetStatus(status, statusValues == null ? Localize(statusArabic) :
                UiLanguageCatalog.Format(leftToRight, statusArabic, statusValues), statusSuccess);
        }
        private void StyleShell()
        {
            Panel header = (Panel)Controls.Find("permissionsHeader", true)[0];
            Label title = (Label)Controls.Find("permissionsTitle", true)[0];
            Label subtitle = (Label)Controls.Find("permissionsSubtitle", true)[0];
            FlowLayoutPanel actions = (FlowLayoutPanel)Controls.Find("permissionsActions", true)[0];
            header.BackColor = UiTheme.Surface;
            ((Panel)Controls.Find("permissionsSearchBar", true)[0]).BackColor = UiTheme.Surface;
            searchCaption.BackColor = UiTheme.Surface;
            searchCaption.ForeColor = UiTheme.MutedText;
            status.BorderStyle = BorderStyle.None;
            title.ForeColor = UiTheme.Text;
            subtitle.ForeColor = UiTheme.MutedText;
            actions.BackColor = UiTheme.Background;
            actions.RightToLeft = RightToLeft.No;
            actions.FlowDirection = leftToRight ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = UiTheme.PrimaryDark;
            grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = Color.White;
            AntdThemeAdapter.Apply((AntdUI.Panel)Controls.Find("permissionsCard", true)[0]);
            closeButton.IconPosition = leftToRight ? AntdUI.TAlignMini.Left : AntdUI.TAlignMini.Right;
            LayoutHeader();
            LayoutPermissionColumns();
        }

        private void LayoutPermissionColumns()
        {
            if (!grid.Columns.Contains("RoleDefault")) return;
            // Compact windows keep the effective decision visible; the role default stays in its tooltip.
            grid.Columns["RoleDefault"].Visible = grid.ClientSize.Width >= 690;
        }

        private void LayoutHeader()
        {
            Panel header = Controls.Find("permissionsHeader", true).FirstOrDefault() as Panel;
            if (header == null) return;
            int availableWidth = Math.Max(1, ClientSize.Width - header.Padding.Horizontal);
            int height = header.Padding.Vertical;
            foreach (Label caption in header.Controls.OfType<Label>())
            {
                caption.RightToLeft = leftToRight ? RightToLeft.No : RightToLeft.Yes;
                caption.TextAlign = ContentAlignment.MiddleLeft;
                caption.AccessibleName = caption.Text;
                int measured = TextRenderer.MeasureText(caption.Text, caption.Font,
                    new Size(availableWidth, int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix |
                        (leftToRight ? 0 : TextFormatFlags.RightToLeft)).Height + 4;
                if (caption.Dock == DockStyle.Top) caption.Height = Math.Max(34, measured);
                height += caption.Dock == DockStyle.Top ? caption.Height : measured;
            }
            header.Height = height;
        }

        private void BuildLayout()
        {
            Panel header = new Panel { Name = "permissionsHeader", Dock = DockStyle.Top, Height = 86, Padding = new Padding(22, 12, 22, 10) };
            Label title = new Label
            {
                Name = "permissionsTitle",
                Dock = DockStyle.Top,
                Height = 34,
                Text = Localize("صلاحيات الشاشات والعمليات للموظف: ") + employeeName,
                TextAlign = ContentAlignment.MiddleRight,
                AutoEllipsis = false,
                Font = AppFonts.Create(16F, FontStyle.Bold)
            };
            Label subtitle = new Label
            {
                Name = "permissionsSubtitle",
                Dock = DockStyle.Fill,
                Text = "حدّد سماحًا أو منعًا عند الحاجة؛ الإعدادات وإدارة الموظفين للمدير فقط.",
                TextAlign = ContentAlignment.MiddleRight,
                AutoEllipsis = false
            };
            header.Controls.Add(subtitle);
            header.Controls.Add(title);

            Panel body = new Panel { Name = "permissionsBody", Dock = DockStyle.Fill, Padding = new Padding(24, 20, 24, 18), BackColor = UiTheme.Background };
            AntdUI.Panel card = DesktopComponents.Card("permissionsCard");
            card.Dock = DockStyle.Fill; card.Padding = new Padding(18);

            ConfigureGrid();

            status.Name = "permissionsStatus";
            status.Dock = DockStyle.Bottom;
            status.Height = 38;
            status.Padding = new Padding(8, 0, 8, 0);
            status.TextAlign = ContentAlignment.MiddleRight;
            status.AutoEllipsis = true;
            status.AccessibleName = "حالة صلاحيات الشاشات والعمليات";

            FlowLayoutPanel actions = new FlowLayoutPanel
            {
                Name = "permissionsActions",
                Dock = DockStyle.Bottom,
                Height = 70,
                FlowDirection = UiDirection.IsLeftToRight ? FlowDirection.LeftToRight : FlowDirection.RightToLeft,
                WrapContents = false,
                Padding = new Padding(24, 13, 24, 0),
                BackColor = UiTheme.Background
            };
            saveButton.Name = "saveEmployeeOperationPermissions";
            saveButton.Text = Localize("حفظ الصلاحيات");
            saveButton.Width = 156;
            saveButton.Height = 44;
            saveButton.AccessibleName = "حفظ صلاحيات الشاشات والعمليات";
            saveButton.IconSvg = OutlinedIcons.Svg(AppIcon.Save, true);
            saveButton.IconSize = new Size(16, 16);
            saveButton.IconPosition = AntdUI.TAlignMini.Right;
            saveButton.Click += (s, e) => SavePermissions();

            closeButton.Name = "closeEmployeeOperationPermissions";
            closeButton.Text = Localize("إغلاق");
            closeButton.Width = 110;
            closeButton.Height = 44;
            closeButton.AccessibleName = "إغلاق صلاحيات الشاشات والعمليات";
            closeButton.IconSvg = OutlinedIcons.Svg(AppIcon.Close);
            closeButton.IconSize = new Size(16, 16);
            closeButton.Click += (s, e) => Close();
            actions.Controls.Add(saveButton);
            actions.Controls.Add(closeButton);

            var searchBar = new Panel { Name = "permissionsSearchBar", Dock = DockStyle.Top, Height = 56,
                Padding = new Padding(0, 6, 0, 6), BackColor = UiTheme.Surface, TabIndex = 0 };
            searchCaption.Name = "permissionsSearchCaption";
            searchCaption.Width = 140;
            searchCaption.Dock = DockStyle.Right;
            searchCaption.TextAlign = ContentAlignment.MiddleRight;
            searchCaption.BackColor = UiTheme.Surface;
            searchFrame.Dock = DockStyle.Fill;
            searchBar.Controls.Add(searchFrame);
            searchBar.Controls.Add(searchCaption);
            grid.TabIndex = 1;
            card.Controls.Add(grid);
            card.Controls.Add(status);
            card.Controls.Add(searchBar);
            body.Controls.Add(card);
            Controls.Add(body);
            Controls.Add(actions);
            Controls.Add(header);

            UiTheme.ApplyDialogHeader(header, title, subtitle, null);
            UiTheme.ApplyInfoLabels(status);
        }

        private void ConfigureGrid()
        {
            grid.Name = "employeeOperationPermissionsGrid";
            grid.Tag = UiTheme.PreserveLocalizedCustomGridColumns;
            grid.Dock = DockStyle.Fill;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AllowUserToResizeRows = false;
            grid.RowHeadersVisible = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.BackgroundColor = UiTheme.Surface;
            grid.BorderStyle = BorderStyle.None;
            grid.AccessibleName = "جدول تجاوزات صلاحيات الشاشات والعمليات";
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Operation",
                HeaderText = "الشاشة أو العملية",
                ReadOnly = true,
                FillWeight = 50
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "RoleDefault",
                HeaderText = "الدور",
                ToolTipText = "الصلاحية الافتراضية لدور الموظف",
                ReadOnly = true,
                FillWeight = 25
            });
            DataGridViewComboBoxColumn overrideColumn = new DataGridViewComboBoxColumn
            {
                Name = "Override",
                HeaderText = "تجاوز للموظف",
                FlatStyle = FlatStyle.Flat,
                FillWeight = 35
            };
            overrideColumn.DisplayMember = nameof(PermissionChoice.Text);
            overrideColumn.ValueMember = nameof(PermissionChoice.Code);
            overrideColumn.ValueType = typeof(string);
            grid.Columns.Add(overrideColumn);
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Effective",
                HeaderText = "النتيجة الفعلية",
                ReadOnly = true,
                FillWeight = 19
            });
            grid.CurrentCellDirtyStateChanged += (sender, args) =>
            {
                if (grid.IsCurrentCellDirty) grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
            grid.CellValueChanged += (sender, args) =>
            {
                if (args.RowIndex >= 0 && args.ColumnIndex == grid.Columns["Override"].Index)
                {
                    UpdateEffectiveCell(grid.Rows[args.RowIndex]);
                    RefreshEditState();
                }
            };
            UiTheme.ApplyDataGridQuality(grid);
            grid.Columns["Operation"].FillWeight = 40;
            grid.Columns["RoleDefault"].FillWeight = 17;
            grid.Columns["Override"].FillWeight = 26;
            grid.Columns["Effective"].FillWeight = 17;
            grid.Columns["Operation"].MinimumWidth = 190;
            grid.Columns["RoleDefault"].MinimumWidth = 90;
            grid.Columns["Override"].MinimumWidth = 165;
            grid.Columns["Effective"].MinimumWidth = 100;
        }

        private void LoadPermissions()
        {
            try
            {
                if (!PermissionService.CanManageDetailedPermissions())
                {
                    throw new UnauthorizedAccessException("لا تملك صلاحية إدارة الصلاحيات التفصيلية.");
                }

                EmployeePermissionSnapshot snapshot = Service.GetByEmployeeId(employeeId);
                if (!snapshot.IsAvailable)
                {
                    saveButton.Enabled = false;
                    SetStatus("يلزم ترقية مخطط قاعدة البيانات أولاً لتفعيل الصلاحيات التفصيلية.", false);
                    return;
                }

                string role = snapshot.Role;
                if (PermissionService.IsManagerRole(role))
                {
                    saveButton.Enabled = false;
                    SetStatus("حساب المدير يحتفظ بصلاحيات كاملة ولا يقبل تجاوزات تفصيلية.", null);
                    return;
                }

                PopulateRows(role, snapshot.Overrides);

                permissionsEditable = true;
                RefreshEditState();
                SetStatus(EditHint, null);
            }
            catch (Exception ex)
            {
                permissionsEditable = false;
                saveButton.Enabled = false;
                SetStatusFormat("تعذر تحميل الصلاحيات: {0}", false, ex.Message);
            }
        }

        private void PopulateRows(string role, IReadOnlyDictionary<string, bool> overrides)
        {
            savedOverrides = null;
            grid.Rows.Clear();
            foreach (AppPermission screen in (AppPermission[])Enum.GetValues(typeof(AppPermission)))
            {
                string key = "Screen." + screen;
                if (!EmployeeScreenPermissionKeys.IsDelegableKey(key)) continue;
                bool allowed;
                string selected = overrides.TryGetValue(key, out allowed)
                    ? (allowed ? AllowChoice : DenyChoice) : DefaultChoice;
                string arabicName = "شاشة: " + PermissionService.GetArabicName(screen);
                bool roleAllowed = PermissionService.GetRoleDefault(role, screen);
                int rowIndex = grid.Rows.Add(Localize("شاشة:") + " " + Localize(PermissionService.GetArabicName(screen)),
                    Localize(roleAllowed ? "مسموح" : "غير مسموح"), selected, string.Empty);
                grid.Rows[rowIndex].Tag = key;
                grid.Rows[rowIndex].Cells["Operation"].Tag = arabicName;
                grid.Rows[rowIndex].Cells["Operation"].ToolTipText = Convert.ToString(grid.Rows[rowIndex].Cells["Operation"].Value);
                grid.Rows[rowIndex].Cells["RoleDefault"].Tag = roleAllowed;
                UpdateEffectiveCell(grid.Rows[rowIndex]);
            }
            foreach (AppOperation operation in (AppOperation[])Enum.GetValues(typeof(AppOperation)))
            {
                bool allowed;
                string selected = overrides.TryGetValue(operation.ToString(), out allowed)
                    ? (allowed ? AllowChoice : DenyChoice) : DefaultChoice;
                string arabicName = PermissionService.GetArabicName(operation);
                bool roleAllowed = PermissionService.GetRoleDefault(role, operation);
                int rowIndex = grid.Rows.Add(Localize(arabicName),
                    Localize(roleAllowed ? "مسموح" : "غير مسموح"), selected, string.Empty);
                grid.Rows[rowIndex].Tag = operation.ToString();
                grid.Rows[rowIndex].Cells["Operation"].Tag = arabicName;
                grid.Rows[rowIndex].Cells["Operation"].ToolTipText = Convert.ToString(grid.Rows[rowIndex].Cells["Operation"].Value);
                grid.Rows[rowIndex].Cells["RoleDefault"].Tag = roleAllowed;
                UpdateEffectiveCell(grid.Rows[rowIndex]);
            }
            LayoutPermissionColumns();
            ApplyFilter();
            CaptureSavedOverrides();
        }

        private void UpdateEffectiveCell(DataGridViewRow row)
        {
            if (row == null || !(row.Cells["RoleDefault"].Tag is bool)) return;
            bool roleAllowed = (bool)row.Cells["RoleDefault"].Tag;
            string selection = Convert.ToString(row.Cells["Override"].Value);
            bool effective = selection == AllowChoice || (selection != DenyChoice && roleAllowed);
            string outcome = Localize(effective ? "مسموح" : "غير مسموح");
            row.Cells["Effective"].Value = outcome;
            row.Cells["Effective"].Style.ForeColor = effective ? UiTheme.Secondary : UiTheme.Danger;
            string explanation = Localize("الدور") + ": " + Localize(roleAllowed ? "مسموح" : "غير مسموح") +
                " · " + Localize("النتيجة الفعلية") + ": " + outcome;
            row.Cells["Effective"].ToolTipText = explanation;
            row.Cells["Override"].ToolTipText = explanation;
        }

        private void ApplyFilter()
        {
            if (grid.IsCurrentCellDirty) grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            grid.EndEdit();
            grid.CurrentCell = null;
            string query = search.Text.Trim();
            int matches = 0;
            foreach (DataGridViewRow row in grid.Rows)
            {
                bool visible = query.Length == 0 ||
                    Convert.ToString(row.Cells["Operation"].Value).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    Convert.ToString(row.Cells["Operation"].Tag).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    Convert.ToString(row.Tag).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
                row.Visible = visible;
                if (visible) matches++;
            }
            searchCaption.Text = Localize("بحث") + " (" + matches + "/" + grid.Rows.Count + ")";
            search.AccessibleDescription = leftToRight ? "Visible permissions: " + matches + "/" + grid.Rows.Count :
                "الصلاحيات الظاهرة: " + matches + "/" + grid.Rows.Count;
        }

        private Dictionary<string, bool> ReadOverrides()
        {
            if (grid.IsCurrentCellDirty) grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            grid.EndEdit();
            return ReadCommittedOverrides();
        }

        private Dictionary<string, bool> ReadCommittedOverrides()
        {
            Dictionary<string, bool> overrides = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            foreach (DataGridViewRow row in grid.Rows)
            {
                if (row.IsNewRow || !(row.Tag is string))
                {
                    continue;
                }

                string permissionKey = (string)row.Tag;
                string selection = Convert.ToString(row.Cells["Override"].Value);
                if (string.Equals(selection, AllowChoice, StringComparison.Ordinal))
                {
                    overrides[permissionKey] = true;
                }
                else if (string.Equals(selection, DenyChoice, StringComparison.Ordinal))
                {
                    overrides[permissionKey] = false;
                }
            }

            return overrides;
        }

        internal bool HasPendingChanges
        {
            get
            {
                if (savedOverrides == null) return false;
                Dictionary<string, bool> current = ReadCommittedOverrides();
                return current.Count != savedOverrides.Count || current.Any(item =>
                {
                    bool saved;
                    return !savedOverrides.TryGetValue(item.Key, out saved) || saved != item.Value;
                });
            }
        }

        private void CaptureSavedOverrides()
        {
            savedOverrides = ReadOverrides();
            RefreshEditState();
        }

        private void RefreshEditState()
        {
            bool pending = HasPendingChanges;
            saveButton.Enabled = !previewMode && permissionsEditable && pending;
            if (!previewMode && permissionsEditable && savedOverrides != null)
                SetStatus(pending ? "توجد تعديلات غير محفوظة على الصلاحيات." : EditHint, null);
        }

        internal bool ConfirmPendingChanges(Func<bool> confirmDiscard)
        {
            ReadOverrides();
            return !HasPendingChanges || confirmDiscard();
        }

        private void SavePermissions()
        {
            if (previewMode || !permissionsEditable) return;
            try
            {
                Dictionary<string, bool> overrides = ReadOverrides();
                if (!HasPendingChanges) return;

                Service.Replace(employeeId, overrides, new OperationActor
                {
                    UserName = SessionContext.CurrentUser,
                    UserRole = SessionContext.CurrentRole
                });
                CaptureSavedOverrides();
                SetStatus("تم حفظ الصلاحيات. تُراجع عند المحاولة التالية، وقد يتأخر التحديث حتى خمس ثوانٍ.", true);
                // The inline status already confirms success; keep repeated administration saves uninterrupted.
            }
            catch (Exception ex)
            {
                SetStatusFormat("لم يتم الحفظ: {0}", false, ex.Message);
                UiTheme.ShowOperationIssue(this, Localize("حفظ الصلاحيات"), ex);
            }
        }
    }
}

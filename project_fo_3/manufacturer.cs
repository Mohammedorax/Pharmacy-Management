using System;
using System.Collections.Generic;
using System.Drawing;
using System.Data;
using System.Windows.Forms;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Application;

namespace project_fo_3
{
    // العرض وتنسيق التفاعل فقط؛ قواعد التحقق وSQL في Application/Infrastructure.
    public partial class Manufacturer : Form
    {
        private readonly IManufacturerService Service;
        private readonly bool useDefaultRead;
        private Func<bool> listContextIsCurrent, editorContextIsCurrent;
        private bool invalidatingContext;
        private readonly bool useAntdTable;
        private readonly Action<string> copyRow;
        private readonly Func<bool> confirmDelete;
        private readonly ManufacturerAccessibleTable companyTable = (ManufacturerAccessibleTable)DesktopComponents.Table("manufacturerTable", "جدول الشركات المصنعة", new ManufacturerAccessibleTable());
        private readonly List<ManufacturerRecord> companyRows = new List<ManufacturerRecord>();
        internal Control ActiveCompanyGrid => useAntdTable ? (Control)companyTable : CompanyGV;
        private CancellationTokenSource readCancellation;
        private long readVersion;
        private bool loading, saving;
        private AntdUI.Button refreshButton;
        private Panel contentHost;
        private Panel actionBar;
        private AntdUI.Panel editorCard;
        private AntdUI.Panel nameFrame;
        private readonly bool previewMode;
        private readonly bool presentationLeftToRight;

        public Manufacturer() : this(false, UiLanguageCatalog.IsEnglishActive)
        {
        }

        internal Manufacturer(bool previewMode) : this(previewMode, UiLanguageCatalog.IsEnglishActive)
        {
        }

        internal Manufacturer(bool previewMode, bool presentationLeftToRight, IManufacturerService service = null, bool useAntdTable = true, Action<string> copyRow = null, Func<bool> confirmDelete = null)
        {
            Service = service ?? ApplicationServices.Manufacturers;
            useDefaultRead = service == null;
            editorContextIsCurrent = CaptureSessionContext();
            this.useAntdTable = useAntdTable;
            this.copyRow = copyRow ?? (text => Clipboard.SetText(text));
            this.confirmDelete = confirmDelete ?? (() => AppNotifications.Show(this, Present("هل تريد حذف هذه الشركة؟"),
                Present("تأكيد الحذف"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes);
            this.previewMode = previewMode;
            this.presentationLeftToRight = presentationLeftToRight;
            InitializeComponent();
            companyTable.ReadContextIsCurrent = () => !IsDisposed && !Disposing && !loading && !saving &&
                (previewMode || (listContextIsCurrent != null && listContextIsCurrent()));
            // Font autoscaling previously enlarged MinimumSize after the IBM theme was applied.
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96F, 96F);
            CompanyGV.Tag = UiTheme.PreserveLocalizedCustomGridColumns;
            // Layout owns table bounds; bottom anchoring stretches it into the editor when AutoScroll grows.
            CompanyGV.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            CompanyGV.DataBindingComplete += CompanyGV_DataBindingComplete;
            CompanyGV.CellPainting += CompanyGV_CellPainting;
            CompanyGV.ClipboardCopyMode = DataGridViewClipboardCopyMode.Disable;
            CompanyGV.KeyDown += ModernTableKeyDown;
            companyTable.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            companyTable.AutoSizeColumnsMode = AntdUI.ColumnsMode.Fill;
            companyTable.EditMode = AntdUI.TEditMode.None;
            companyTable.ClipboardCopy = false;
            companyTable.ClipboardCopyFocusedCell = false;
            companyTable.SelectIndexChanged += (sender, args) => LoadModernSelection();
            companyTable.KeyDown += ModernTableKeyDown;
            CompanyGV.Visible = !useAntdTable;
            if (previewMode) Load -= Company_Load;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = true;
            label8.Visible = false;
            MinimumSize = new Size(760, 620);
            UiTheme.Apply(this);
            ApplyResponsiveLayout();
            ApplyPresentation();
            LayoutResponsiveScreen();
            if (previewMode)
            {
                PopulatePreviewGrid();
                UiTheme.SetFormStatus(this, Present("معاينة تخطيطية فقط؛ لم تُقرأ قاعدة البيانات ولن تُحفظ بيانات."));
            }
            FormClosed += (sender, args) => CancelRead();
            Disposed += (sender, args) => CancelRead();
            UiSessionSubscription.Attach(this, () =>
            {
                if (previewMode) return;
                CancelRead();
                loading = false;
                InvalidateCachedContext();
            });
        }

        private string Present(string arabicText)
        {
            return presentationLeftToRight ? UiLanguageCatalog.Translate(arabicText) : arabicText;
        }

        private void ApplyPresentation()
        {
            Text = Present("الشركات المصنعة");
            label2.Text = Present("الشركات المصنعة");
            label7.Text = Present("قائمة الشركات");
            label6.Text = Present("رقم الشركة");
            label5.Text = Present("اسم الشركة");
            label4.Text = Present("رقم الهاتف");
            label3.Text = Present("العنوان");

            ConfigureManufacturerAction(button4, "إضافة", "إضافة شركة مصنعة", AppIcon.Add);
            ConfigureManufacturerAction(button3, "تعديل", "تعديل شركة مصنعة", AppIcon.Settings);
            ConfigureManufacturerAction(button1, "حذف", "حذف شركة مصنعة", AppIcon.Delete);
            ConfigureManufacturerAction(button5, "مسح", "مسح حقول الشركة المصنعة", AppIcon.Refresh);
            ConfigureManufacturerAction(refreshButton, "تحديث", "تحديث قائمة الشركات", AppIcon.Refresh);
            ConfigureManufacturerAction(button2, "رجوع", "إغلاق شاشة الشركات المصنعة", AppIcon.Back);

            UiDirection.Apply(this, presentationLeftToRight);
            foreach (Label label in new[] { label2, label7, label6, label5, label4, label3 })
                label.TextAlign = presentationLeftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
            ApplyInputDirection();
            ApplyCompanyGridDirection();
            CompanyGV.AccessibleName = Present("جدول الشركات المصنعة");
            contentHost.AccessibleName = Present("بيانات الشركات المصنعة ومحرر الشركة");
            actionBar.AccessibleName = Present("إجراءات الشركات المصنعة");
            panel1.BackColor = UiTheme.Background;
            label2.BackColor = UiTheme.Background;
            label2.ForeColor = UiTheme.Text;
            UiTheme.ApplyStatusStripPresentation(this, presentationLeftToRight);

            if (previewMode)
            {
                refreshButton.Enabled = button4.Enabled = button3.Enabled = button1.Enabled = false;
                button4.AccessibleDescription = button3.AccessibleDescription = button1.AccessibleDescription =
                    Present("معاينة فقط؛ لا تُحفظ بيانات شركات مصنعة.");
            }
        }

        private void ConfigureManufacturerAction(AntdUI.Button button, string text, string accessibleName, AppIcon icon)
        {
            button.Text = Present(text);
            button.AccessibleName = Present(accessibleName);
            button.IconSvg = OutlinedIcons.Svg(icon, !presentationLeftToRight);
            button.IconSize = new Size(18, 18);
            button.IconPosition = presentationLeftToRight ? AntdUI.TAlignMini.Left : AntdUI.TAlignMini.Right;
            DesktopComponents.StyleOutlinedFooterAction(button, button == button1);
        }

        private void ApplyInputDirection()
        {
            SetInputDirection(tb1, "رقم الشركة", false, 0);
            tb2.RightToLeft = presentationLeftToRight ? RightToLeft.No : RightToLeft.Yes;
            tb2.TextAlign = presentationLeftToRight ? HorizontalAlignment.Left : HorizontalAlignment.Right;
            tb2.AccessibleName = Present("اسم الشركة");
            tb2.AccessibleRole = AccessibleRole.Text;
            tb2.TabIndex = 1;
            tb2.Font = AppFonts.Create(10F);
            if (nameFrame != null) DesktopComponents.StyleNativeTextFrame(nameFrame, tb2);
            SetInputDirection(tb3, "رقم الهاتف", false, 2);
            SetInputDirection(tb4, "العنوان", true, 3);
            button4.TabIndex = 4;
            button3.TabIndex = 5;
            button1.TabIndex = 6;
            button5.TabIndex = 7;
            button2.TabIndex = 8;
            CompanyGV.AccessibleName = Present("جدول الشركات المصنعة");
        }

        private void SetInputDirection(AntdUI.Input field, string label, bool naturalLanguage, int tabIndex)
        {
            bool rightToLeft = naturalLanguage && !presentationLeftToRight;
            field.RightToLeft = rightToLeft ? RightToLeft.Yes : RightToLeft.No;
            field.TextAlign = rightToLeft ? HorizontalAlignment.Right : HorizontalAlignment.Left;
            field.AccessibleName = Present(label);
            field.AccessibleRole = AccessibleRole.Text;
            field.TabIndex = tabIndex;
            field.Font = AppFonts.Create(9.5F);
            AntdThemeAdapter.Apply(field);
        }
        private void ApplyResponsiveLayout()
        {
            AutoScroll = false;
            panel1.Dock = DockStyle.None;
            panel2.Dock = DockStyle.None;
            panel2.Visible = false;
            label2.Text = Present("الشركات المصنعة");
            label2.Font = AppFonts.Create(18F, FontStyle.Bold);

            contentHost = new Panel { Name = "manufacturerContentHost", AutoScroll = true, BackColor = UiTheme.Background };
            actionBar = new Panel { Name = "manufacturerActionBar", BackColor = UiTheme.Background };
            editorCard = DesktopComponents.Card("manufacturerEditorCard");
            WinFormsShapeAdapter.RemoveInputShape(tb2);
            nameFrame = DesktopComponents.NativeTextFrame(tb2, "manufacturerNameFrame");
            tb2.Dock = DockStyle.None;
            foreach (Control control in new Control[] { label6, label5, label4, label3, tb1, nameFrame, tb3, tb4 })
                editorCard.Controls.Add(control);
            contentHost.Controls.Add(editorCard);
            foreach (Control control in new Control[] { label7, ActiveCompanyGrid })
                contentHost.Controls.Add(control);
            foreach (AntdUI.Button button in new[] { button4, button3, button1, button5, button2 })
            {
                actionBar.Controls.Add(button);
                DesktopComponents.StyleOutlinedFooterAction(button, button == button1);
            }
            refreshButton = DesktopComponents.Action("refreshManufacturers", "تحديث", AppIcon.Refresh, false);
            refreshButton.Click += async (sender, args) => await ReloadGridAsync();
            actionBar.Controls.Add(refreshButton);
            Controls.Add(contentHost);
            Controls.Add(actionBar);
            Resize += Manufacturer_Resize;
            Shown += Manufacturer_Shown;
            LayoutResponsiveScreen();
        }

        private void Manufacturer_Resize(object sender, EventArgs e) => LayoutResponsiveScreen();
        private void Manufacturer_Shown(object sender, EventArgs e) => LayoutResponsiveScreen();

        private void LayoutResponsiveScreen()
        {
            if (ClientSize.Width <= 0 || ClientSize.Height <= 0) return;
            SuspendLayout();
            try
            {
                Control statusStrip = Controls["__UiStatusStrip"];
                // Visible is false while the form is still hidden, although the docked status strip
                // will occupy this space as soon as the window is shown.
                int statusHeight = statusStrip != null ? statusStrip.Height : 0;
                int workspaceBottom = ClientSize.Height - statusHeight;
                int titleWidth = Math.Max(240, ClientSize.Width - 48);
                int titleHeight = Math.Max(44, TextRenderer.MeasureText(label2.Text, label2.Font,
                    new Size(titleWidth, 1000), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height + 4);
                panel1.SetBounds(0, 0, ClientSize.Width, titleHeight + 38);
                actionBar.SetBounds(0, Math.Max(panel1.Bottom, workspaceBottom - 70), ClientSize.Width, 70);
                contentHost.SetBounds(0, panel1.Bottom, ClientSize.Width, Math.Max(120, actionBar.Top - panel1.Bottom));
                int contentWidth = contentHost.ClientSize.Width;
                // لا نسمح للمنسق العام بإلغاء موضع الجدول الذي يحسبه هذا التخطيط المتجاوب.
                ActiveCompanyGrid.Dock = DockStyle.None;
                int contentTop = 12;
                int contentBottom = contentHost.ClientSize.Height - 12;
                int leftMargin = 34;
                int rightMargin = 32;
                int gap = 28;
                bool compact = contentWidth < 1000;
                int editorWidth = compact ? Math.Max(320, contentWidth - leftMargin - rightMargin) : Math.Min(420, Math.Max(320, contentWidth / 3));
                int editorX = compact || presentationLeftToRight
                    ? leftMargin
                    : contentWidth - rightMargin - editorWidth;
                int gridX = compact || !presentationLeftToRight
                    ? leftMargin
                    : editorX + editorWidth + gap;
                int gridWidth = compact ? Math.Max(360, contentWidth - leftMargin - rightMargin) : Math.Max(360, contentWidth - leftMargin - rightMargin - editorWidth - gap);
                int gridHeight = compact ? Math.Max(180, Math.Min(240, (contentBottom - contentTop) / 3)) : Math.Max(220, contentBottom - contentTop - 54);

                label2.SetBounds(24, 18, titleWidth, titleHeight);
                label2.AccessibleName = label2.Text;
                label7.SetBounds(gridX, contentTop, gridWidth, 34);
                ActiveCompanyGrid.SetBounds(gridX, contentTop + 42, gridWidth, gridHeight);

                int editorTop = compact ? ActiveCompanyGrid.Bottom + 18 : contentTop;
                editorCard.SetBounds(editorX, editorTop, editorWidth, 336);
                int fieldWidth = Math.Max(100, editorCard.ClientSize.Width - 32);
                int y = 18;
                SetEditorField(label6, tb1, 16, y, fieldWidth); y += 76;
                SetEditorField(label5, nameFrame, 16, y, fieldWidth); y += 76;
                SetEditorField(label4, tb3, 16, y, fieldWidth); y += 76;
                SetEditorField(label3, tb4, 16, y, fieldWidth);
                contentHost.AutoScrollMinSize = new Size(0, Math.Max(ActiveCompanyGrid.Bottom, editorCard.Bottom) + 18);
                LayoutFooterActions();
            }
            finally
            {
                UiDirection.Apply(this, presentationLeftToRight);
                if (editorCard != null)
                {
                    AntdThemeAdapter.Apply(editorCard);
                    foreach (Label label in new[] { label6, label5, label4, label3 })
                    { label.BackColor = UiTheme.Surface; label.ForeColor = UiTheme.MutedText; }
                    DesktopComponents.StyleNativeTextFrame(nameFrame, tb2);
                }
                ApplyCompanyGridDirection();
                ResumeLayout(false);
            }
        }

        private static void SetEditorField(Label label, Control field, int x, int y, int width)
        {
            if (label != null) label.SetBounds(x, y, width, 26);
            if (field != null) field.SetBounds(x, y + 28, width, 44);
        }

        private void LayoutFooterActions()
        {
            const int gap = 12;
            int buttonWidth = Math.Min(122, Math.Max(96, (actionBar.ClientSize.Width - 48 - gap * 5) / 6));
            int x = presentationLeftToRight ? 24 : actionBar.ClientSize.Width - 24 - buttonWidth;
            foreach (AntdUI.Button button in new[] { button4, button3, button1, button5, refreshButton, button2 })
            {
                button.SetBounds(x, 13, buttonWidth, 44);
                DesktopComponents.StyleOutlinedFooterAction(button, button == button1);
                x += (presentationLeftToRight ? 1 : -1) * (buttonWidth + gap);
            }
        }

        private void label8_Click(object sender, EventArgs e) => Close();
        private void button2_Click(object sender, EventArgs e) { ClearFields(); Close(); }
        private void button5_Click(object sender, EventArgs e) { ClearFields(); ApplyBusyState(); }

        private async void button4_Click(object sender, EventArgs e)
        {
            if (previewMode || loading || saving || !EnsureEditorContext()) return;
            var operationContextIsCurrent = CaptureSessionContext();
            var operationService = useDefaultRead ? ApplicationServices.CreateManufacturerSession().Service : Service;
            var actor = CurrentActor(); string employeeId = SessionContext.CurrentEmployeeId;
            saving = true; ApplyBusyState();
            try
            {
                operationService.Create(ReadEditor(), actor);
                bool refreshed = await ReloadGridAsync();
                if (!operationContextIsCurrent()) return;
                if (refreshed) ClearFields();
                UiTheme.SetFormStatus(this, Present(refreshed ? "تمت إضافة الشركة بنجاح." :
                    "تم حفظ التغيير، لكن تعذر تحديث القائمة. اضغط تحديث."), refreshed ? (bool?)true : false);
            }
            catch (Exception ex) { if (!IsDisposed && !Disposing) UiTheme.ShowOperationIssue(this, Present("إضافة الشركة المصنعة"), ex); }
            finally { saving = false; if (!IsDisposed && !Disposing) ApplyBusyState(); }
        }

        private async void button3_Click(object sender, EventArgs e)
        {
            if (previewMode || loading || saving || !EnsureEditorContext()) return;
            var operationContextIsCurrent = CaptureSessionContext();
            var operationService = useDefaultRead ? ApplicationServices.CreateManufacturerSession().Service : Service;
            var actor = CurrentActor(); string employeeId = SessionContext.CurrentEmployeeId;
            saving = true; ApplyBusyState();
            try
            {
                if (!operationService.Update(ReadEditor(), actor))
                {
                    AppNotifications.Show(this, Present("لم يتم العثور على الشركة."), Present("الشركات المصنعة"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                bool refreshed = await ReloadGridAsync();
                if (!operationContextIsCurrent()) return;
                if (refreshed) ClearFields();
                UiTheme.SetFormStatus(this, Present(refreshed ? "تم تعديل بيانات الشركة بنجاح." :
                    "تم حفظ التغيير، لكن تعذر تحديث القائمة. اضغط تحديث."), refreshed ? (bool?)true : false);
            }
            catch (Exception ex) { if (!IsDisposed && !Disposing) UiTheme.ShowOperationIssue(this, Present("تعديل الشركة المصنعة"), ex); }
            finally { saving = false; if (!IsDisposed && !Disposing) ApplyBusyState(); }
        }

        private async void button1_Click(object sender, EventArgs e)
        {
            if (previewMode || loading || saving || !EnsureEditorContext()) return;
            var operationContextIsCurrent = CaptureSessionContext();
            var operationService = useDefaultRead ? ApplicationServices.CreateManufacturerSession().Service : Service;
            string companyId = (tb1.Text ?? string.Empty).Trim();
            if (companyId.Length == 0)
            {
                AppNotifications.Show(this, Present("الرجاء إدخال رقم الشركة المراد حذفها."), Present("الشركات المصنعة"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (!confirmDelete()) return;
            if (!operationContextIsCurrent()) { InvalidateCachedContext(); return; }

            var actor = CurrentActor(); string employeeId = SessionContext.CurrentEmployeeId;
            saving = true; ApplyBusyState();
            try
            {
                if (!operationService.Delete(companyId, actor))
                {
                    AppNotifications.Show(this, Present("لم يتم العثور على الشركة."), Present("الشركات المصنعة"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                bool refreshed = await ReloadGridAsync();
                if (!operationContextIsCurrent()) return;
                if (refreshed) ClearFields();
                UiTheme.SetFormStatus(this, Present(refreshed ? "تم حذف الشركة بنجاح." :
                    "تم حفظ التغيير، لكن تعذر تحديث القائمة. اضغط تحديث."), refreshed ? (bool?)true : false);
            }
            catch (Exception ex) { if (!IsDisposed && !Disposing) UiTheme.ShowOperationIssue(this, Present("حذف الشركة المصنعة"), ex); }
            finally { saving = false; if (!IsDisposed && !Disposing) ApplyBusyState(); }
        }

        private bool IsCurrentSession(OperationActor actor, string employeeId)
        {
            return !IsDisposed && !Disposing && SessionContext.IsAuthenticated &&
                string.Equals(actor.UserName, SessionContext.CurrentUser, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(actor.UserRole, SessionContext.CurrentRole, StringComparison.OrdinalIgnoreCase) &&
                employeeId == SessionContext.CurrentEmployeeId;
        }

        private Func<bool> CaptureSessionContext()
        {
            long revision = SessionContext.CurrentSessionRevision;
            var actor = CurrentActor(); string employee = SessionContext.CurrentEmployeeId;
            var profileIsCurrent = ApplicationServices.CaptureRuntimeConnectionContext();
            return () => SessionContext.CurrentSessionRevision == revision && profileIsCurrent() &&
                IsCurrentSession(actor, employee) && PermissionService.CanOpen(AppPermission.ManageManufacturers);
        }

        private bool EnsureEditorContext()
        {
            if (previewMode) return true;
            if (editorContextIsCurrent != null && editorContextIsCurrent()) return true;
            InvalidateCachedContext(); return false;
        }

        private bool EnsureListContext()
        {
            if (previewMode) return true;
            if (listContextIsCurrent != null && listContextIsCurrent()) return true;
            InvalidateCachedContext(); return false;
        }

        private void InvalidateCachedContext()
        {
            if (invalidatingContext || IsDisposed || Disposing) return;
            invalidatingContext = true;
            try
            {
                listContextIsCurrent = null;
                ClearCompanyRows(); ClearFields();
                // A stale operation must not become authorized merely because its fields were cleared.
                editorContextIsCurrent = null;
                UiTheme.SetFormStatus(this, Present("تغيرت جلسة المستخدم. أعد تحميل القائمة."));
                ApplyBusyState();
            }
            finally { invalidatingContext = false; }
        }

        private void CancelRead()
        {
            readVersion++;
            var pending = readCancellation; readCancellation = null; pending?.Cancel();
        }

        private void ApplyBusyState()
        {
            bool canEdit = !previewMode && !loading && !saving && editorContextIsCurrent != null && editorContextIsCurrent() && SessionContext.IsAuthenticated &&
                PermissionService.CanOpen(AppPermission.ManageManufacturers);
            button4.Enabled = button3.Enabled = button1.Enabled = canEdit;
            tb1.Enabled = tb3.Enabled = tb4.Enabled = canEdit; tb2.ReadOnly = !canEdit;
            button5.Enabled = !loading && !saving;
            refreshButton.Enabled = !previewMode && !saving;
            ActiveCompanyGrid.Enabled = !loading && !saving;
        }

        internal async Task<bool> ReloadGridAsync()
        {
            if (previewMode || IsDisposed || Disposing) return false;
            CancelRead();
            var cancellation = new CancellationTokenSource(); readCancellation = cancellation;
            long version = readVersion;
            var actor = CurrentActor(); string employeeId = SessionContext.CurrentEmployeeId;
            var readContextIsCurrent = CaptureSessionContext();
            var scopedRead = useDefaultRead ? ApplicationServices.CreateManufacturerReadSession() : null;
            var readService = scopedRead == null ? Service : scopedRead.Service;
            if (editorContextIsCurrent == null || !editorContextIsCurrent()) ClearFields();
            listContextIsCurrent = null;
            loading = true; ClearCompanyRows(); ApplyBusyState();
            UiTheme.SetFormStatus(this, Present("جارٍ تحميل الشركات…"));
            try
            {
                var rows = await readService.GetAllAsync(actor, cancellation.Token);
                if (cancellation.IsCancellationRequested || version != readVersion || !readContextIsCurrent()) return false;
                if (!PermissionService.CanOpen(AppPermission.ManageManufacturers))
                    throw new UnauthorizedAccessException("لا تملك صلاحية إدارة بيانات الموردين.");
                listContextIsCurrent = readContextIsCurrent;
                editorContextIsCurrent = readContextIsCurrent;
                BindCompanyRows(rows);
                UiTheme.SetFormStatus(this, Present(rows.Count == 0 ? "لا توجد شركات مسجلة." : "تم تحديث قائمة الشركات."));
                return true;
            }
            catch (OperationCanceledException) { return false; }
            catch (Exception exception)
            {
                if (version != readVersion || !readContextIsCurrent()) return false;
                AppDiagnostics.Log("ManufacturerListRead", exception);
                UiTheme.SetFormStatus(this, Present("تعذر تحميل الشركات. تحقق من الاتصال ثم اضغط تحديث."), false);
                return false;
            }
            finally
            {
                if (version == readVersion && !IsDisposed && !Disposing)
                {
                    readCancellation = null; loading = false;
                    if (!readContextIsCurrent()) InvalidateCachedContext();
                    ApplyBusyState();
                }
                cancellation.Dispose();
            }
        }

        private void ApplyCompanyGridPresentation(DataGridView grid)
        {
            UiTheme.ApplyDataGridQuality(grid);
            SetHeader(grid, "CompId", "رقم الشركة");
            SetHeader(grid, "CompName", "اسم الشركة");
            SetHeader(grid, "CompPhone", "رقم الهاتف");
            SetHeader(grid, "CompAddress", "العنوان");
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            ConfigureGridColumn(grid, "CompId", 84F, 76);
            ConfigureGridColumn(grid, "CompName", 150F, 130);
            ConfigureGridColumn(grid, "CompPhone", 105F, 96);
            ConfigureGridColumn(grid, "CompAddress", 180F, 140);
            ApplyCompanyGridDirection();
        }

        private void ApplyCompanyGridDirection()
        {
            if (useAntdTable)
            {
                var align = presentationLeftToRight ? AntdUI.ColumnAlign.Left : AntdUI.ColumnAlign.Right;
                var columns = new AntdUI.ColumnCollection {
                    new AntdUI.Column("CompId", Present("رقم الشركة"), AntdUI.ColumnAlign.Left) { Width = "120", MinWidth = "100", ColAlign = align },
                    new AntdUI.Column("CompName", Present("اسم الشركة"), align) { Width = "fill", MinWidth = "180" },
                    new AntdUI.Column("CompPhone", Present("رقم الهاتف"), AntdUI.ColumnAlign.Left) { Width = "180", MinWidth = "160", ColAlign = align },
                    new AntdUI.Column("CompAddress", Present("العنوان"), align) { Width = "240", MinWidth = "200" }
                };
                if (!presentationLeftToRight)
                {
                    var reversed = new AntdUI.ColumnCollection();
                    for (int index = columns.Count - 1; index >= 0; index--) reversed.Add(columns[index]);
                    columns = reversed;
                }
                companyTable.Columns = columns;
                companyTable.RightToLeft = presentationLeftToRight ? RightToLeft.No : RightToLeft.Yes;
                companyTable.EmptyText = Present("لا توجد بيانات");
                companyTable.AccessibleName = Present("جدول الشركات المصنعة");
                AntdThemeAdapter.Apply(companyTable);
                return;
            }
            if (CompanyGV == null) return;
            SetHeader(CompanyGV, "CompId", "رقم الشركة");
            SetHeader(CompanyGV, "CompName", "اسم الشركة");
            SetHeader(CompanyGV, "CompPhone", "رقم الهاتف");
            SetHeader(CompanyGV, "CompAddress", "العنوان");
            DataGridViewContentAlignment textAlignment = presentationLeftToRight
                ? DataGridViewContentAlignment.MiddleLeft : DataGridViewContentAlignment.MiddleRight;
            CompanyGV.RightToLeft = presentationLeftToRight ? RightToLeft.No : RightToLeft.Yes;
            CompanyGV.ColumnHeadersDefaultCellStyle.Alignment = textAlignment;
            CompanyGV.ColumnHeadersDefaultCellStyle.SelectionBackColor = UiTheme.Primary;
            CompanyGV.ColumnHeadersDefaultCellStyle.SelectionForeColor = Color.White;
            CompanyGV.ScrollBars = ScrollBars.Both;
            string[] logicalOrder = { "CompId", "CompName", "CompPhone", "CompAddress" };
            for (int displayIndex = 0; displayIndex < logicalOrder.Length; displayIndex++)
            {
                string columnName = logicalOrder[displayIndex];
                if (CompanyGV.Columns.Contains(columnName)) CompanyGV.Columns[columnName].DisplayIndex = displayIndex;
            }
            foreach (DataGridViewColumn column in CompanyGV.Columns)
            {
                column.HeaderCell.Style.Alignment = textAlignment;
                column.HeaderCell.Style.SelectionBackColor = UiTheme.Primary;
                column.HeaderCell.Style.SelectionForeColor = Color.White;
                column.DefaultCellStyle.Alignment = column.Name == "CompName" || column.Name == "CompAddress"
                    ? textAlignment : DataGridViewContentAlignment.MiddleCenter;
            }
            // Changing DisplayIndex on an RTL-generated grid can recreate its header cells.
            // Set localized captions last so the data source cannot restore Arabic captions
            // after the English physical order has been applied.
            SetHeader(CompanyGV, "CompId", "رقم الشركة");
            SetHeader(CompanyGV, "CompName", "اسم الشركة");
            SetHeader(CompanyGV, "CompPhone", "رقم الهاتف");
            SetHeader(CompanyGV, "CompAddress", "العنوان");
        }

        private static void ConfigureGridColumn(DataGridView grid, string columnName, float fillWeight, int minimumWidth)
        {
            if (!grid.Columns.Contains(columnName)) return;
            DataGridViewColumn column = grid.Columns[columnName];
            column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            column.FillWeight = fillWeight;
            column.MinimumWidth = minimumWidth;
        }

        private void SetHeader(DataGridView grid, string columnName, string header)
        {
            if (grid.Columns.Contains(columnName)) grid.Columns[columnName].HeaderText = Present(header);
        }

        private void PopulatePreviewGrid()
        {
            BindCompanyRows(new List<ManufacturerRecord>
            {
                new ManufacturerRecord { CompId = "COMP-001", CompName = "الشفاء — Al Shifa", CompPhone = "+967 1 234 567", CompAddress = "صنعاء — شارع الزبيري" },
                new ManufacturerRecord { CompId = "COMP-014", CompName = "Pharma 500 mg", CompPhone = "+967 777 555 444", CompAddress = "عدن — المنصورة" }
            });
        }

        private void ClearCompanyRows()
        {
            companyRows.Clear();
            if (useAntdTable) { companyTable.DataSource = new DataTable(); companyTable.SelectedIndex = 0; companyTable.NotifyAccessibleRowsChanged(); }
            else CompanyGV.DataSource = null;
        }

        private void BindCompanyRows(IReadOnlyList<ManufacturerRecord> rows)
        {
            ClearCompanyRows(); companyRows.AddRange(rows);
            if (!useAntdTable)
            {
                CompanyGV.AutoGenerateColumns = true;
                CompanyGV.DataSource = new List<ManufacturerRecord>(rows);
                ApplyCompanyGridPresentation(CompanyGV);
                return;
            }
            var table = new DataTable();
            foreach (string key in new[] { "CompId", "CompName", "CompPhone", "CompAddress" }) table.Columns.Add(key, typeof(string));
            foreach (ManufacturerRecord row in rows) table.Rows.Add(row.CompId, row.CompName, row.CompPhone, row.CompAddress);
            companyTable.DataSource = table;
            ApplyCompanyGridDirection();
            companyTable.NotifyAccessibleRowsChanged();
        }

        private void LoadModernSelection()
        {
            int index = companyTable.SelectedIndex - 1;
            if (!useAntdTable || loading || saving || index < 0 || index >= companyRows.Count) return;
            if (!EnsureListContext()) return;
            var selected = companyRows[index];
            tb1.Text = selected.CompId; tb2.Text = selected.CompName;
            tb3.Text = selected.CompPhone; tb4.Text = selected.CompAddress;
        }

        internal string SelectedCompanyText()
        {
            int index = useAntdTable ? companyTable.SelectedIndex - 1 : (CompanyGV.CurrentRow?.Index ?? -1);
            if (loading || saving || index < 0 || index >= companyRows.Count) return null;
            if (!EnsureListContext()) return null;
            var row = companyRows[index];
            return string.Join("\t", new[] { row.CompId, row.CompName, row.CompPhone, row.CompAddress });
        }

        private void ModernTableKeyDown(object sender, KeyEventArgs args)
        {
            if (args.Control && args.KeyCode == Keys.C)
            {
                string text = SelectedCompanyText();
                if (text != null) copyRow(text);
                args.Handled = args.SuppressKeyPress = true;
            }
            else if (args.KeyCode == Keys.Enter)
            {
                if (useAntdTable) LoadModernSelection();
                else if (CompanyGV.CurrentCell != null)
                    CompanyGV_CellContentClick(CompanyGV, new DataGridViewCellEventArgs(CompanyGV.CurrentCell.ColumnIndex, CompanyGV.CurrentCell.RowIndex));
                args.Handled = args.SuppressKeyPress = true;
            }
        }

        private void CompanyGV_DataBindingComplete(object sender, DataGridViewBindingCompleteEventArgs e)
        {
            ApplyCompanyGridDirection();
        }

        private void CompanyGV_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0 || CompanyGV.Columns[e.ColumnIndex].Name != "CompPhone") return;
            // Phone numbers read LTR in both languages. Paint the original formatted value;
            // do not add direction marks to stored values or clipboard/accessibility text.
            DesktopComponents.PaintLeftToRightGridCell(e, CompanyGV.Font);
        }

        private async void Company_Load(object sender, EventArgs e) => await ReloadGridAsync();

        private void CompanyGV_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= CompanyGV.Rows.Count || loading || saving || !EnsureListContext()) return;
            ManufacturerRecord selected = CompanyGV.Rows[e.RowIndex].DataBoundItem as ManufacturerRecord;
            if (selected == null) return;
            tb1.Text = selected.CompId;
            tb2.Text = selected.CompName;
            tb3.Text = selected.CompPhone;
            tb4.Text = selected.CompAddress;
        }

        private ManufacturerRecord ReadEditor()
        {
            return new ManufacturerRecord
            {
                CompId = tb1.Text,
                CompName = tb2.Text,
                CompPhone = tb3.Text,
                CompAddress = tb4.Text
            };
        }

        private static OperationActor CurrentActor()
        {
            return new OperationActor { UserName = SessionContext.CurrentUser, UserRole = SessionContext.CurrentRole };
        }

        private void ClearFields()
        {
            editorContextIsCurrent = CaptureSessionContext();
            tb1.Clear();
            tb2.Clear();
            tb3.Clear();
            tb4.Clear();
            tb1.Focus();
        }
    }

    // Read-only values follow the current rendered row model; cached objects fail closed after rebinding.
    internal sealed class ManufacturerAccessibleTable : AntdUI.Table
    {
        internal Func<bool> ReadContextIsCurrent { get; set; }
        private T Read<T>(Func<T> read, T unavailable)
        {
            if (IsDisposed || Disposing || !IsHandleCreated) return unavailable;
            try
            {
                if (InvokeRequired) return (T)Invoke(new Func<T>(() => Read(read, unavailable)));
                if (ReadContextIsCurrent == null || !ReadContextIsCurrent()) return unavailable;
                T result = read();
                return ReadContextIsCurrent != null && ReadContextIsCurrent() ? result : unavailable;
            }
            catch (InvalidOperationException) { return unavailable; }
        }
        private AntdUI.Column ColumnAt(int visibleIndex)
        {
            if (Columns == null) return null;
            foreach (AntdUI.Column column in Columns)
                if (column.Visible && visibleIndex-- == 0) return column;
            return null;
        }
        private int VisibleColumnCount()
        {
            int count = 0;
            if (Columns != null) foreach (AntdUI.Column column in Columns) if (column.Visible) count++;
            return count;
        }
        protected override AccessibleObject CreateAccessibilityInstance() => new TableAccess(this);
        internal void NotifyAccessibleRowsChanged()
        {
            if (IsHandleCreated && !IsDisposed && !Disposing)
                AccessibilityNotifyClients(AccessibleEvents.Reorder, -1);
        }
        private sealed class TableAccess : ControlAccessibleObject
        {
            private readonly ManufacturerAccessibleTable table;
            internal TableAccess(ManufacturerAccessibleTable table) : base(table) { this.table = table; }
            public override AccessibleRole Role => AccessibleRole.Table;
            public override int GetChildCount() => table.Read(() =>
            {
                var source = table.DataSource as DataTable;
                return source == null || source.Columns.Count == 0 ? 0 : table.RowCount + 1;
            }, 0);
            public override AccessibleObject GetChild(int index) => table.Read<AccessibleObject>(() =>
            {
                var source = table.DataSource as DataTable;
                if (source == null || source.Columns.Count == 0 || index < 0 || index > table.RowCount) return null;
                var record = index == 0 ? null : table.GetRow(index - 1)?.record as DataRow;
                if (index > 0 && (record == null || !ReferenceEquals(record.Table, source))) return null;
                return new RowAccess(table, this, source, record, index);
            }, null);
        }
        private sealed class RowAccess : AccessibleObject
        {
            private readonly ManufacturerAccessibleTable table;
            private readonly AccessibleObject parent;
            private readonly DataTable source;
            private readonly DataRow record;
            private readonly int index;
            internal RowAccess(ManufacturerAccessibleTable table, AccessibleObject parent, DataTable source, DataRow record, int index)
            { this.table = table; this.parent = parent; this.source = source; this.record = record; this.index = index; }
            private bool Current() => ReferenceEquals(table.DataSource, source) &&
                (index == 0 || (record != null && record.RowState != DataRowState.Deleted && record.RowState != DataRowState.Detached &&
                ReferenceEquals(table.GetRow(index - 1)?.record, record) && ReferenceEquals(record.Table, source)));
            private string CellText(int column)
            {
                var definition = table.ColumnAt(column);
                if (!Current() || definition == null) return string.Empty;
                if (index == 0) return definition.Title ?? string.Empty;
                return source.Columns.Contains(definition.Key) ? Convert.ToString(record[definition.Key], System.Globalization.CultureInfo.InvariantCulture) : string.Empty;
            }
            public override AccessibleObject Parent => parent;
            public override AccessibleRole Role => AccessibleRole.Row;
            public override string Name
            {
                get => table.Read(() =>
                {
                    if (!Current()) return string.Empty;
                    var values = new List<string>();
                    for (int column = 0; column < table.VisibleColumnCount(); column++) values.Add(CellText(column));
                    return string.Join("\t", values);
                }, string.Empty);
                set { }
            }
            public override int GetChildCount() => table.Read(() => Current() ? table.VisibleColumnCount() : 0, 0);
            public override AccessibleObject GetChild(int childIndex) => table.Read<AccessibleObject>(() =>
            {
                var column = table.ColumnAt(childIndex);
                return Current() && childIndex >= 0 && column != null ? new CellAccess(this, childIndex, column.Key) : null;
            }, null);
            public override Rectangle Bounds => table.Read(() =>
            {
                if (!Current()) return Rectangle.Empty;
                Rectangle bounds = Rectangle.Empty;
                for (int column = 0; column < table.VisibleColumnCount(); column++)
                {
                    var cell = CellBounds(column);
                    if (!cell.IsEmpty) bounds = bounds.IsEmpty ? cell : Rectangle.Union(bounds, cell);
                }
                return bounds;
            }, Rectangle.Empty);
            private Rectangle CellBounds(int column)
            {
                var local = Rectangle.Intersect(table.ClientRectangle, table.CellRectangle(index, column));
                return local.IsEmpty ? Rectangle.Empty : table.RectangleToScreen(local);
            }
            public override AccessibleStates State => table.Read(() => Current()
                ? AccessibleStates.ReadOnly | (Bounds.IsEmpty ? AccessibleStates.Offscreen : AccessibleStates.None)
                : AccessibleStates.ReadOnly | AccessibleStates.Unavailable, AccessibleStates.ReadOnly | AccessibleStates.Unavailable);
            private sealed class CellAccess : AccessibleObject
            {
                private readonly RowAccess row;
                private readonly int column;
                private readonly string key;
                internal CellAccess(RowAccess row, int column, string key) { this.row = row; this.column = column; this.key = key; }
                private bool Current() => row.Current() && row.table.ColumnAt(column)?.Key == key;
                public override AccessibleObject Parent => row;
                public override AccessibleRole Role => row.index == 0 ? AccessibleRole.ColumnHeader : AccessibleRole.Cell;
                public override string Name
                {
                    get => row.table.Read(() => Current() ? (row.index == 0 ? row.CellText(column) :
                        row.table.ColumnAt(column).Title + ": " + row.CellText(column)) : string.Empty, string.Empty);
                    set { }
                }
                public override string Value
                {
                    get => row.table.Read(() => Current() ? row.CellText(column) : string.Empty, string.Empty);
                    set { throw new InvalidOperationException("Supplier accessibility cells are read-only."); }
                }
                public override Rectangle Bounds => row.table.Read(() => Current() ? row.CellBounds(column) : Rectangle.Empty, Rectangle.Empty);
                public override AccessibleStates State => row.table.Read(() => Current()
                    ? AccessibleStates.ReadOnly | (Bounds.IsEmpty ? AccessibleStates.Offscreen : AccessibleStates.None)
                    : AccessibleStates.ReadOnly | AccessibleStates.Unavailable, AccessibleStates.ReadOnly | AccessibleStates.Unavailable);
            }
        }
    }

    internal sealed class ManufacturerUiAutomationPreview : Manufacturer
    {
        internal ManufacturerUiAutomationPreview(bool english) : base(true, english) { }
    }
}



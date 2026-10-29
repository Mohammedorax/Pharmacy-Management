// شرح الملف: نظام تصميم موحد لكل شاشات التطبيق.
// يطبق مظهرًا هادئًا بألوان تركواز/نعناع مناسبة لبيئة الصيدلية، مع دعم الوضع الليلي.
using System;
using System.Configuration;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace project_fo_3
{
    // نظام التصميم الموحد للتطبيق.
    // ألوان هادئة وخطوط واضحة؛ يستبدل الصور غير الضرورية بأيقونات نصية خفيفة.
    internal static class UiTheme
    {
        // اسم إعداد الوضع اللوني داخل App.config.
        private const string ThemeModeKey = "ThemeMode";
        private const string UiFontFamilyKey = "UiFontFamily";
        private const string AutoCloseButtonName = "__AutoCloseButton";
        private static readonly ToolTip AutoCloseToolTip = new ToolTip();
        private static readonly ConditionalWeakTable<Form, object> DedicatedStatusForms =
            new ConditionalWeakTable<Form, object>();
        internal static readonly object PreserveCustomGridColumns = new object();
        internal static readonly object PreserveLocalizedGridDirection = new object();
        internal static readonly object PreserveLocalizedCustomGridColumns = new object();

        internal static bool OwnsLocalizedGridPresentation(DataGridView grid)
        {
            return grid != null && (ReferenceEquals(grid.Tag, PreserveLocalizedGridDirection) ||
                ReferenceEquals(grid.Tag, PreserveLocalizedCustomGridColumns));
        }

        internal static bool OwnsCustomGridColumns(DataGridView grid)
        {
            return grid != null && (ReferenceEquals(grid.Tag, PreserveCustomGridColumns) ||
                ReferenceEquals(grid.Tag, PreserveLocalizedCustomGridColumns));
        }

        // لوحة الألوان النشطة حاليًا.
        private static ThemePalette activePalette = CreateLightPalette();

        // ألوان الخلفية والأسطح والعناصر الأساسية.
        internal static Color Background { get { return activePalette.Background; } }
        internal static Color Surface { get { return activePalette.Surface; } }
        internal static Color SurfaceVariant { get { return activePalette.SurfaceVariant; } }
        internal static Color Primary { get { return activePalette.Primary; } }
        internal static Color PrimaryDark { get { return activePalette.PrimaryDark; } }
        internal static Color Secondary { get { return activePalette.Secondary; } }
  // The success text colour must clear the 4.5:1 contrast on the surface, so the state stays
  // readable without relying on the colour alone.
  internal static Color SuccessText { get { return activePalette.SuccessText; } }
        internal static Color Warning { get { return activePalette.Warning; } }
        internal static Color WarningSurface { get { return activePalette.WarningSurface; } }
        internal static Color Danger { get { return activePalette.Danger; } }
        internal static Color Text { get { return activePalette.Text; } }
        internal static Color MutedText { get { return activePalette.MutedText; } }
        internal static Color Border { get { return activePalette.Border; } }
        internal static Color Selection { get { return activePalette.Selection; } }
        internal static Color AlternateRow { get { return activePalette.AlternateRow; } }
        internal const int StandardInputHeight = DesktopDesignTokens.InputHeight;
        internal const int StandardActionHeight = DesktopDesignTokens.ActionHeight;
        internal const int StandardGridRowHeight = DesktopDesignTokens.GridRowHeight;

        // يطبق التصميم على نموذج كامل.
        /// <summary>Times the theme + layout pass per screen so slow opens are measured, not guessed.</summary>
        public static void Apply(Form form)
        {
            var perfClock = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                ApplyCore(form);
            }
            finally
            {
                PerfTrace.Log("theme", form == null ? "null" : form.GetType().Name, perfClock.Elapsed.TotalMilliseconds);
            }
        }

        private static void ApplyCore(Form form)
        {
            EnableDoubleBuffer(form);
            activePalette = CreatePalette(LoadMode());
            AntdThemeAdapter.SynchronizePalette();
            form.Font = AppFonts.Create(9.25F, FontStyle.Regular);
            form.BackColor = Background;
            // Respect dialog ownership and caller-selected monitor/position.
            if (form.StartPosition == FormStartPosition.WindowsDefaultLocation ||
                form.StartPosition == FormStartPosition.WindowsDefaultBounds)
                form.StartPosition = FormStartPosition.CenterScreen;
            form.ShowIcon = false;
            ConfigureResponsiveForm(form);
            ScreenModeSettings.InstallOpeningPreference(form);
            bool leftToRight = (form as ReceiptVoucherForm)?.ReceiptPresentationDirection ?? UiLanguageCatalog.IsEnglishActive;
            if (leftToRight)
            {
                UiLanguageCatalog.Apply(form);
            }
            else
            {
                ArabicUi.Apply(form);
            }

            ApplyRecursive(form, form);

            // شاشة الشعار تستخدم PictureBox لعرض صورة المستخدم — لا نستبدلها بأيقونة.
            if (!(form is PharmacyCompanyForm) && !(form is Home) && !(form is Login))
            {
                ReplaceImageControls(form);
            }

            InstallCloseNavigation(form);
            InstallStatusStrip(form);
            ApplyStandardDataGrids(form);
            // آخر خطوة دائمًا: بعض منسقات الثيم تعيد خصائص الاتجاه أثناء التجميل.
            // إعادة السياسة هنا تضمن RTL للعربية وLTR للإنجليزية على الشجرة النهائية كلها.
            UiDirection.Apply(form, leftToRight);
            var inventorySurface = form as InventoryDataForm;
            if (inventorySurface != null) inventorySurface.RestoreInventorySharedAppearance();
            var homeSurface = form as Home;
            if (homeSurface != null) homeSurface.RestoreHomePresentation();
            var ledgerSurface = form as GeneralLedgerForm;
            if (ledgerSurface != null) ledgerSurface.RestoreLedgerPresentation();
            var receiptSurface = form as ReceiptVoucherForm;
            if (receiptSurface != null) receiptSurface.RestoreReceiptPresentation();
        }

        // يحافظ على قابلية الوصول إلى الحقول والأزرار في الشاشات الصغيرة أو ذات تكبير DPI
        // مرتفع، من دون تحويل النوافذ الحوارية المقصودة إلى شاشات تمرير غير مريحة.
        private static void ConfigureResponsiveForm(Form form)
        {
            form.AutoScaleMode = AutoScaleMode.Dpi;

            bool isResizableSurface = form.FormBorderStyle == FormBorderStyle.Sizable
                || form.FormBorderStyle == FormBorderStyle.SizableToolWindow
                || form.FormBorderStyle == FormBorderStyle.None;

            if (isResizableSurface)
            {
                form.AutoScroll = true;
                form.AutoScrollMargin = new Size(12, 12);
            }
        }

        // يطبق التصميم على عنصر واحد بعد تغييره برمجيًا.
        public static void ApplyTo(Control control)
        {
            activePalette = CreatePalette(LoadMode());
            AntdThemeAdapter.SynchronizePalette();
            StyleControl(control);

            if (control.HasChildren)
            {
                ApplyRecursive(control, control.FindForm());
            }
            UiDirection.Apply(control);
        }

        // ينسق مجموعة Labels كمربعات معلومات واضحة في الوضعين الليلي والنهاري.
        public static void ApplyInfoLabels(params Label[] labels)
        {
            activePalette = CreatePalette(LoadMode());

            foreach (Label label in labels)
            {
                if (label == null)
                {
                    continue;
                }

                label.BackColor = SurfaceVariant;
                label.ForeColor = Text;
                label.BorderStyle = BorderStyle.FixedSingle;
                label.Font = AppFonts.Create(Math.Max(9F, label.Font.Size), FontStyle.Bold);
                label.TextAlign = ContentAlignment.MiddleCenter;
                WinFormsShapeAdapter.ApplyInput(label);
            }
        }

        // ينسق نص الحالة بلون مناسب حسب نتيجة العملية.
        public static void SetStatus(Label label, string text, bool? success = null)
        {
            if (label == null)
            {
                return;
            }

            activePalette = CreatePalette(LoadMode());
  label.Text = text;
  label.BackColor = Color.Transparent;
  // The success colour must clear the 4.5:1 text contrast, and it never carries the state alone:
  // the message text states the outcome.
  label.ForeColor = success == true ? SuccessText : success == false ? Danger : MutedText;
  }

        // تعرض أخطاء التشغيل بلغة عملية، وتحافظ على التفاصيل التقنية في السجل المحلي فقط.
        // رسائل التحقق المتوقعة (مثل كمية أو تاريخ غير صالح) تبقى واضحة للمستخدم.
        public static void ShowOperationFailure(IWin32Window owner, string operation, Exception exception, bool preserveValidationMessage = false)
        {
            bool english = AppNotifications.ResolvePresentationDirection(owner);
            string caption = english ? UiLanguageCatalog.Translate(operation ?? "تعذر إكمال العملية") : operation ?? "تعذر إكمال العملية";
            Exception leaf = exception;
            while (leaf != null && leaf.InnerException != null)
            {
                leaf = leaf.InnerException;
            }

            if (preserveValidationMessage && !ContainsSqlException(exception) && leaf is InvalidOperationException)
            {
                AppNotifications.Show(owner, leaf is UiValidationException localized ? localized.DisplayMessage(english) : leaf.Message, caption, MessageBoxButtons.OK, MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button1, english);
                return;
            }

            // A catch may run after a successful write followed by a failed refresh.
            // An exception alone cannot prove rollback; keep the support log category.
            string supportCode = AppDiagnostics.Log("UiOperation:" + (operation ?? "Unknown"), exception);
            AppNotifications.Show(owner, BuildOperationIssueMessage(operation, supportCode, english), caption,
                MessageBoxButtons.OK, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1, english);
        }

        // للعمليات التي قد تكون قراءة أو قد تكون قد وصلت إلى نقطة غير معروفة من الحفظ.
        // لا نكشف تفاصيل SQL ولا نزعم أن السجل لم يُحفظ؛ نوجّه المستخدم للتحقق قبل الإعادة.
        public static void ShowOperationIssue(IWin32Window owner, string operation, Exception exception)
        {
            ShowOperationIssue(owner, operation, exception, AppNotifications.ResolvePresentationDirection(owner));
        }

        public static void ShowOperationIssue(IWin32Window owner, string operation, Exception exception, bool leftToRight)
        {
            string supportCode = AppDiagnostics.Log("UiIssue:" + (operation ?? "Unknown"), exception);
            bool english = leftToRight;
            AppNotifications.Show(
                owner,
                BuildOperationIssueMessage(operation, supportCode, english),
                english ? UiLanguageCatalog.Translate(operation ?? "تعذر إكمال العملية") : operation ?? "تعذر إكمال العملية",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button1,
                english);
        }

        // A returned application outcome proves completion; a later UI exception cannot undo it.
        internal static void ShowCompletedOperationFollowUpFailure(IWin32Window owner, string operation, string reference, Exception exception)
            => ShowCompletedOperationFollowUpFailure(owner, operation, reference, exception,
                "لا تكرر العملية؛ أعد فتح السجل لمراجعة النتيجة.");

        internal static void ShowCompletedOperationFollowUpFailure(IWin32Window owner, string operation, string reference,
            Exception exception, string recoveryInstruction)
        {
            bool english = AppNotifications.ResolvePresentationDirection(owner);
            string supportCode = AppDiagnostics.Log("UiCompletedFollowUp:" + operation, exception);
            const string template = "تمت العملية بنجاح: {0}.\nالمرجع: {1}\n\nتعذر تحديث العرض فقط. {2}\n\nرمز الدعم: {3}";
            string message = UiLanguageCatalog.Format(english, template,
                english ? UiLanguageCatalog.Translate(operation) : operation, reference,
                english ? UiLanguageCatalog.Translate(recoveryInstruction) : recoveryInstruction, supportCode);
            const string caption = "تمت العملية؛ تعذر تحديث العرض";
            AppNotifications.Show(owner, message, english ? UiLanguageCatalog.Translate(caption) : caption,
                MessageBoxButtons.OK, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1, english);
        }

        internal static string BuildOperationIssueMessage(string operation, string supportCode, bool english)
        {
            const string template = "تعذر {0} الآن. لا تُعرض التفاصيل التقنية هنا.\n\nإذا كانت العملية تتضمن حفظاً، راجع السجل قبل تكرارها. وإذا تكرر الأمر، أرسل رمز الدعم إلى مسؤول النظام: {1}";
            string displayOperation = english ? UiLanguageCatalog.Translate(operation ?? "إكمال العملية") : operation ?? "إكمال العملية";
            return string.Format(english ? UiLanguageCatalog.Translate(template) : template,
                displayOperation, supportCode);
        }

        private static bool ContainsSqlException(Exception exception)
        {
            for (Exception current = exception; current != null; current = current.InnerException)
            {
                if (string.Equals(current.GetType().FullName, "System.Data.SqlClient.SqlException", StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        public static void ApplyDataGridQuality(DataGridView grid, string emptyMessage = "لا توجد بيانات حالياً")
        {
            if (grid == null)
            {
                return;
            }

            // Data grids repaint row by row while loading and scrolling; double buffering
            // removes that flicker, which was the visible "every screen redraws" complaint.
            EnableDoubleBuffer(grid);

            activePalette = CreatePalette(LoadMode());
            StyleGrid(grid);
            ApplyGridDirection(grid);
            if (!OwnsCustomGridColumns(grid))
            {
                grid.AutoGenerateColumns = true;
                grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            }
            grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
            grid.AllowUserToResizeColumns = true;
            grid.MultiSelect = false;
            grid.ShowCellToolTips = true;
            grid.CellToolTipTextNeeded -= DataGrid_CellToolTipTextNeeded;
            grid.CellToolTipTextNeeded += DataGrid_CellToolTipTextNeeded;
            grid.RowTemplate.Height = 34;

            grid.DataBindingComplete -= DataGrid_DataBindingComplete;
            grid.DataBindingComplete += DataGrid_DataBindingComplete;
            grid.RowsAdded -= DataGrid_RowsChanged;
            grid.RowsAdded += DataGrid_RowsChanged;
            grid.RowsRemoved -= DataGrid_RowsChanged;
            grid.RowsRemoved += DataGrid_RowsChanged;
            grid.Resize -= DataGrid_Resize;
            grid.Resize += DataGrid_Resize;

            Label emptyLabel = EnsureEmptyStateLabel(grid, emptyMessage);
            EmptyStateMessages.Remove(emptyLabel);
            EmptyStateMessages.Add(emptyLabel, emptyMessage);
            emptyLabel.Text = EmptyStateMessage(grid, emptyMessage);
            ApplySmartColumnSizing(grid);
            UpdateEmptyState(grid);
        }

        // The empty-state caption follows the grid direction instead of staying Arabic.
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Label, string> EmptyStateMessages =
            new System.Runtime.CompilerServices.ConditionalWeakTable<Label, string>();

        internal static void RefreshEmptyStateText(DataGridView grid)
        {
            if (grid == null) return;
            Label label = FindEmptyStateLabel(grid);
            if (label == null) return;
            string message = null;
            EmptyStateMessages.TryGetValue(label, out message);
            label.Text = EmptyStateMessage(grid, message);
        }

        private static string EmptyStateMessage(DataGridView grid, string emptyMessage)
        {
            const string arabicDefault = "لا توجد بيانات حالياً";
            if (!string.IsNullOrWhiteSpace(emptyMessage) &&
                !string.Equals(emptyMessage.Trim(), arabicDefault, StringComparison.Ordinal))
            {                return emptyMessage;
            }
            bool leftToRight = grid != null && grid.RightToLeft == RightToLeft.No;
            return leftToRight ? "No data available." : arabicDefault;
        }

        public static void ApplyComboBoxQuality(ComboBox comboBox)
        {
            if (comboBox == null)
            {
                return;
            }

            activePalette = CreatePalette(LoadMode());
            StyleComboBox(comboBox);
            UiDirection.Apply(comboBox);
        }

        public static void InstallStatusStrip(Form form, string initialText = null)
        {
            object dedicatedStatus;
            if (form == null || form is Login || form is Home ||
                DedicatedStatusForms.TryGetValue(form, out dedicatedStatus) ||
                form.Controls.ContainsKey("__UiStatusStrip"))
            {
                return;
            }

            bool isDefaultStatus = string.IsNullOrWhiteSpace(initialText);
            string fullStatusText = isDefaultStatus ? BuildDefaultStatusText(form) : initialText;
            StatusStrip status = new StatusStrip
            {
                Name = "__UiStatusStrip",
                Dock = DockStyle.Bottom,
                SizingGrip = false,
                ShowItemToolTips = true,
                AutoSize = false,
                Height = 22,
                BackColor = SurfaceVariant,
                RightToLeft = UiDirection.IsLeftToRight ? RightToLeft.No : RightToLeft.Yes
            };

            ToolStripStatusLabel info = new ToolStripStatusLabel
            {
                Name = "__UiStatusInfo",
                Text = fullStatusText,
                Tag = isDefaultStatus,
                ToolTipText = fullStatusText,
                Spring = true,
                TextAlign = UiDirection.IsLeftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight,
                RightToLeft = UiDirection.IsLeftToRight ? RightToLeft.No : RightToLeft.Yes,
                ForeColor = MutedText,
                Font = AppFonts.Create(8.75F, FontStyle.Regular)
            };

            ToolStripStatusLabel clock = new ToolStripStatusLabel
            {
                Name = "__UiStatusClock",
                Text = DateTime.Now.ToString("yyyy/MM/dd HH:mm", System.Globalization.CultureInfo.InvariantCulture),
                RightToLeft = RightToLeft.No,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = MutedText,
                Font = AppFonts.Create(8.75F, FontStyle.Regular)
            };

            status.Items.Add(info);
            status.Items.Add(clock);
            form.Controls.Add(status);
            status.Width = form.ClientSize.Width;
            form.ClientSizeChanged += delegate
            {
                if (!status.IsDisposed && !form.IsDisposed)
                {
                    status.Width = form.ClientSize.Width;
                    RefreshStatusStripLayout(form, status, info, clock);
                }
            };
            RefreshStatusStripLayout(form, status, info, clock);
            status.BringToFront();
        }

        // Forms with their own operation status do not need a second connection/status strip.
        // The weak marker also prevents a later UiTheme.Apply from adding it back.
        internal static void UseDedicatedStatus(Form form)
        {
            if (form == null) return;
            DedicatedStatusForms.GetValue(form, key => new object());
            Control strip = form.Controls["__UiStatusStrip"];
            if (strip == null) return;
            form.Controls.Remove(strip);
            strip.Dispose();
        }
        internal static void ApplyStatusStripPresentation(Form form, bool leftToRight)
        {
            if (form == null) return;
            StatusStrip strip = form.Controls["__UiStatusStrip"] as StatusStrip;
            ToolStripStatusLabel info = strip?.Items["__UiStatusInfo"] as ToolStripStatusLabel;
            ToolStripStatusLabel clock = strip?.Items["__UiStatusClock"] as ToolStripStatusLabel;
            if (info != null && clock != null)
            {
                strip.Tag = leftToRight;
                RefreshStatusStripLayout(form, strip, info, clock, leftToRight);
            }
        }

        private static void RefreshStatusStripLayout(Form form, StatusStrip strip, ToolStripStatusLabel info,
            ToolStripStatusLabel clock, bool? directionOverride = null)
        {
            bool compact = form.ClientSize.Width < 520;
            // A screen that already applied its English presentation sets RightToLeft=No; that must win
            // over the strip's saved tag, otherwise the status text stays Arabic inside an English screen.
            bool leftToRight = directionOverride
                ?? (form.RightToLeft == RightToLeft.No ? true
                    : form.RightToLeft == RightToLeft.Yes ? false
                    : strip.Tag is bool savedDirection ? savedDirection : UiDirection.IsLeftToRight);
            strip.RightToLeft = leftToRight ? RightToLeft.No : RightToLeft.Yes;
            info.RightToLeft = leftToRight ? RightToLeft.No : RightToLeft.Yes;
            info.TextAlign = leftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
            clock.RightToLeft = RightToLeft.No;
            clock.TextAlign = ContentAlignment.MiddleLeft;
            clock.Visible = !compact;

            bool isDefault = info.Tag is bool defaultValue && defaultValue;
            string fullText = isDefault ? BuildDefaultStatusText(form, leftToRight) :
                info.ToolTipText ?? info.Text ?? string.Empty;
            if (isDefault) info.ToolTipText = fullText;
            if (!compact)
            {
                info.Text = fullText;
                return;
            }

            if (isDefault)
            {
                string title = string.IsNullOrWhiteSpace(form.Text)
                    ? (leftToRight ? "System" : "النظام") : form.Text.Trim();
                info.Text = title + " • " + (leftToRight ? "Ready" : "جاهز");
                return;
            }

            info.AccessibleName = fullText;
            const int maxCompactLength = 38;
            info.Text = fullText.Length <= maxCompactLength
                ? fullText
                : fullText.Substring(0, maxCompactLength - 1) + "…";
        }
        public static void SetFormStatus(Form form, string text, bool? success = null)
        {
            if (form == null || string.IsNullOrWhiteSpace(text)) return;

            InstallStatusStrip(form);
            Control[] matches = form.Controls.Find("__UiStatusStrip", false);
            if (matches.Length == 0) return;

            StatusStrip strip = matches[0] as StatusStrip;
            ToolStripStatusLabel label = strip == null ? null : strip.Items["__UiStatusInfo"] as ToolStripStatusLabel;
            if (label == null) return;

            label.ToolTipText = text;
            label.Tag = false;
            label.Text = text;
            label.ForeColor = success == true ? SuccessText : success == false ? Danger : MutedText;
            ToolStripStatusLabel clock = strip.Items["__UiStatusClock"] as ToolStripStatusLabel;
            if (clock != null) RefreshStatusStripLayout(form, strip, label, clock);
        }
        // النماذج ذات AutoScroll تعتمد مساحة افتراضية أوسع من نافذة العميل؛
        // شريط الحالة المثبّت فيها يصبح جزءًا من مساحة التمرير ويعيد التمرير
        // الأفقي. تستخدم هذه النماذج تلميحاتها داخل محتوى الشاشة بدلًا منه.
        public static void RemoveStatusStrip(Form form)
        {
            if (form == null) return;
            Control[] matches = form.Controls.Find("__UiStatusStrip", false);
            foreach (Control control in matches)
            {
                form.Controls.Remove(control);
                control.Dispose();
            }
        }

        // يقرأ الوضع اللوني الحالي: Light أو Dark أو System.
        private static void ApplyStandardDataGrids(Control root)
        {
            if (root == null)
            {
                return;
            }

            foreach (Control control in GetAllControls(root))
            {
                DataGridView grid = control as DataGridView;
                if (grid != null)
                {
                    ApplyDataGridQuality(grid);
                }
            }
        }

        private static string BuildDefaultStatusText(Form form, bool? directionOverride = null)
        {
            bool leftToRight = directionOverride ?? UiDirection.IsLeftToRight;
            string title = form == null || string.IsNullOrWhiteSpace(form.Text)
                ? (leftToRight ? "System" : "النظام") : form.Text.Trim();
            string user = string.IsNullOrWhiteSpace(SessionContext.CurrentUser)
                ? (leftToRight ? "Not specified" : "غير محدد") : SessionContext.CurrentUser;
            DatabaseConnectionProfile profile = Database.GetConnectionProfile();
            return title + "  |  " + (leftToRight ? "User" : "المستخدم") + ": " + user +
                "  |  " + (leftToRight ? "Database" : "قاعدة البيانات") + ": " + profile.DatabaseName +
                "  |  " + (leftToRight ? "Ready" : "جاهز");
        }

        private static void DataGrid_DataBindingComplete(object sender, DataGridViewBindingCompleteEventArgs e)
        {
            DataGridView grid = sender as DataGridView;
            ApplySmartColumnSizing(grid);
            UpdateEmptyState(grid);
        }

        private static void DataGrid_CellToolTipTextNeeded(object sender, DataGridViewCellToolTipTextNeededEventArgs e)
        {
            DataGridView grid = sender as DataGridView;
            if (grid == null || e.RowIndex < 0 || e.ColumnIndex < 0 || !string.IsNullOrWhiteSpace(e.ToolTipText))
            {
                return;
            }

            DataGridViewCell cell = grid.Rows[e.RowIndex].Cells[e.ColumnIndex];
            if (cell is DataGridViewImageCell)
            {
                return;
            }

            object formattedValue = cell.FormattedValue;
            string value = formattedValue == null ? string.Empty : formattedValue as string ?? Convert.ToString(formattedValue);
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            Font font = cell.InheritedStyle.Font ?? grid.Font;
            Size measured = TextRenderer.MeasureText(value, font, Size.Empty,
                TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
            int availableWidth = Math.Max(0, cell.Size.Width - cell.InheritedStyle.Padding.Horizontal - 12);
            if (measured.Width > availableWidth)
            {
                e.ToolTipText = value;
            }
        }

        private static void DataGrid_RowsChanged(object sender, EventArgs e)
        {
            // RowsAdded fires once per row: updating the empty-state label on every row of a large
            // history relayouts the grid thousands of times and freezes the screen.
            DataGridView grid = sender as DataGridView;
            if (grid == null || grid.Rows.Count > 200) return;
            UpdateEmptyState(grid);
        }

        private static void DataGrid_Resize(object sender, EventArgs e)
        {
            UpdateEmptyState(sender as DataGridView);
        }

        private static Label EnsureEmptyStateLabel(DataGridView grid, string text)
        {
            Label label = FindEmptyStateLabel(grid);
            if (label != null)
            {
                return label;
            }

              label = new Label
              {
                  Name = "__GridEmptyState",
                  Text = EmptyStateMessage(grid, text),
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Surface,
                ForeColor = MutedText,
                Font = AppFonts.Create(11F, FontStyle.Bold),
                Visible = false
            };

              grid.Controls.Add(label);
              label.BringToFront();
              // The caption follows the grid direction, so a direction change refreshes it.
              EmptyStateMessages.Remove(label);
              EmptyStateMessages.Add(label, text);
              grid.RightToLeftChanged += (sender, args) => RefreshEmptyStateText(grid);
              return label;
          }

        private static Label FindEmptyStateLabel(DataGridView grid)
        {
            if (grid == null)
            {
                return null;
            }

            Control[] matches = grid.Controls.Find("__GridEmptyState", false);
            return matches.Length == 0 ? null : matches[0] as Label;
        }

        private static void UpdateEmptyState(DataGridView grid)
        {
            if (grid == null || grid.IsDisposed)
            {
                return;
            }

            Label label = EnsureEmptyStateLabel(grid, "لا توجد بيانات حالياً");
            // Enumerating Rows unshares every bound row just to decide emptiness.
            // Count the placeholder separately; hidden rows remain data, as before.
  int dataRows = grid.RowCount - (grid.NewRowIndex >= 0 ? 1 : 0);
  // A grid without a bound source shows the empty state only when the screen asked for its own
  // caption, so a programmatically filled grid keeps its rows visible.
  bool customCaption = false;
  Label emptyLabel = FindEmptyStateLabel(grid);
  if (emptyLabel != null)
  {
    string stored = null;
    EmptyStateMessages.TryGetValue(emptyLabel, out stored);
    customCaption = !string.IsNullOrWhiteSpace(stored) &&
      !string.Equals(stored.Trim(), "لا توجد بيانات حالياً", StringComparison.Ordinal);
  }
  bool isEmpty = dataRows == 0 && (grid.DataSource != null || customCaption);
            label.Visible = isEmpty;
            if (isEmpty)
            {
                int top = Math.Max(grid.ColumnHeadersHeight + 12, 48);
                label.Bounds = new Rectangle(0, top, grid.ClientSize.Width, Math.Max(80, grid.ClientSize.Height - top));
                label.BringToFront();
            }
        }

        private static void ApplySmartColumnSizing(DataGridView grid)
        {
            if (OwnsCustomGridColumns(grid)) return;
            if (grid == null || grid.Columns.Count == 0)
            {
                return;
            }

            foreach (DataGridViewColumn column in grid.Columns)
            {
                string name = ((column.DataPropertyName ?? column.Name ?? string.Empty) + " " + (column.HeaderText ?? string.Empty)).ToLowerInvariant();
                column.SortMode = DataGridViewColumnSortMode.NotSortable;
                column.MinimumWidth = 70;
                column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

                if (name.Contains("id") || name.Contains("رقم") || name.Contains("كود") || name.Contains("code"))
                {
                    column.FillWeight = 75;
                    column.MinimumWidth = 80;
                }
                else if (name.Contains("date") || name.Contains("تاريخ"))
                {
                    column.FillWeight = 95;
                    column.MinimumWidth = 105;
                    column.DefaultCellStyle.Format = "yyyy/MM/dd";
                }
                else if (name.Contains("price") || name.Contains("amount") || name.Contains("total") || name.Contains("balance")
                    || name.Contains("سعر") || name.Contains("مبلغ") || name.Contains("إجمالي") || name.Contains("رصيد") || name.Contains("تكلفة"))
                {
                    column.FillWeight = 85;
                    column.MinimumWidth = 95;
                    column.DefaultCellStyle.Format = "N2";
                }
                else if (name.Contains("note") || name.Contains("description") || name.Contains("ملاحظ") || name.Contains("بيان"))
                {
                    column.FillWeight = 165;
                    column.MinimumWidth = 150;
                    column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                }
                else if (name.Contains("name") || name.Contains("اسم") || name.Contains("account") || name.Contains("حساب"))
                {
                    column.FillWeight = 140;
                    column.MinimumWidth = 140;
                    column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                }
                else
                {
                    column.FillWeight = 100;
                }
            }
        }

        public static string LoadMode()
        {
            try
            {
                // تفضيل شخصي لا يجب أن يضيع عند إعادة بناء التطبيق أو تحديثه.
                string mode = Properties.Setings.Default.ThemeMode;

                if (string.Equals(mode, "Dark", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(mode, "System", StringComparison.OrdinalIgnoreCase))
                {
                    return mode;
                }

                return "Light";
            }
            catch (ConfigurationErrorsException)
            {
                // ملف إعدادات المستخدم تالف — نرجع لإعداد الإصدار للمرونة مع النسخ القديمة.
            }
            catch
            {
                // لا يمنع تعذر الوصول لإعدادات المستخدم فتح البرنامج.
            }

            try
            {
                string fallbackMode = ConfigurationManager.AppSettings[ThemeModeKey];
                return string.Equals(fallbackMode, "Dark", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(fallbackMode, "System", StringComparison.OrdinalIgnoreCase)
                    ? fallbackMode
                    : "Light";
            }
            catch
            {
            }

            return "Light";
        }

        public static string LoadFontFamily()
        {
            try
            {
                string configured = Properties.Setings.Default.UiFontFamily;
                if (!string.IsNullOrWhiteSpace(configured))
                {
                    return configured.Trim();
                }
            }
            catch (ConfigurationErrorsException)
            {
                // ملف إعدادات المستخدم قديم أو غير مكتمل؛ نحتفظ بتعريف التوافق.
            }
            catch
            {
                // لا نسمح لفشل القراءة بمحاولة تعريض التطبيق للتوقف.
            }

            try
            {
                string fallback = ConfigurationManager.AppSettings[UiFontFamilyKey];
                if (!string.IsNullOrWhiteSpace(fallback))
                {
                    return fallback.Trim();
                }
            }
            catch
            {
                // App.config قديم أو محجوب؛ نتحول مباشرة إلى قيمة افتراضية داخلية.
            }

            return AppFonts.DefaultConfiguredFamily;
        }

        // يحفظ إعداد الخط في ملف الإعدادات المحلية ويُحدّث إعدادات الجلسة.
        public static void SaveAppearance(string mode, string fontFamily)
        {
            string normalizedMode = string.Equals(mode, "Dark", StringComparison.OrdinalIgnoreCase)
                ? "Dark" : string.Equals(mode, "System", StringComparison.OrdinalIgnoreCase) ? "System" : "Light";
            string normalizedFont = string.IsNullOrWhiteSpace(fontFamily) ? AppFonts.DefaultConfiguredFamily : fontFamily.Trim();
            string previousMode = Properties.Setings.Default.ThemeMode;
            string previousFont = Properties.Setings.Default.UiFontFamily;
            try
            {
                Properties.Setings.Default.ThemeMode = normalizedMode;
                Properties.Setings.Default.UiFontFamily = normalizedFont;
                Properties.Setings.Default.Save();
            }
            catch
            {
                Properties.Setings.Default.ThemeMode = previousMode;
                Properties.Setings.Default.UiFontFamily = previousFont;
                throw;
            }
            AppFonts.Initialize(normalizedFont);
            activePalette = CreatePalette(normalizedMode);
        }
        public static void SaveFontFamily(string fontFamily)
        {
            string normalized = string.IsNullOrWhiteSpace(fontFamily)
                ? AppFonts.DefaultConfiguredFamily
                : fontFamily.Trim();

            string previous = Properties.Setings.Default.UiFontFamily;
            try
            {
                Properties.Setings.Default.UiFontFamily = normalized;
                Properties.Setings.Default.Save();
            }
            catch
            {
                Properties.Setings.Default.UiFontFamily = previous;
                throw;
            }
            AppFonts.Initialize(normalized);
        }
        // يحفظ الوضع اللوني المختار في ملف الإعدادات.
        public static void SaveMode(string mode)
        {
            SaveAppearance(mode, LoadFontFamily());
        }

        // يمر على كل عناصر النموذج لتطبيق التصميم عليها.
        private static void ApplyRecursive(Control root, Form form)
        {
            foreach (Control control in root.Controls)
            {
                StyleControl(control);

                if (control.HasChildren)
                {
                    ApplyRecursive(control, form);
                }
            }
        }

        // يختار نمط التصميم المناسب حسب نوع عنصر الواجهة.
        private static void StyleControl(Control control)
        {
            if (!AppFonts.IsIconFont(control.Font))
            {
                control.Font = AppFonts.Create(Math.Max(9F, control.Font.SizeInPoints), control.Font.Style);
            }
            control.ForeColor = Text;

            if (AntdThemeAdapter.Apply(control)) return;

            if (control is Panel panel)
            {
                bool forceSurface = string.Equals(Convert.ToString(panel.Tag), "UiSurface", StringComparison.Ordinal);
                panel.BackColor = forceSurface
                    ? Surface
                    : IsTopPanel(panel) || panel.Dock == DockStyle.Bottom
                    ? PrimaryDark
                    : Surface;
            }
            else if (control is FlowLayoutPanel flowPanel)
            {
                flowPanel.BackColor = flowPanel.Parent != null && flowPanel.Parent.Dock == DockStyle.Bottom
                    ? PrimaryDark
                    : Surface;
            }
            else if (control is LinkLabel linkLabel)
            {
                StyleLinkLabel(linkLabel);
            }
            else if (control is Label label)
            {
                StyleLabel(label);
            }
            else if (control is Button button)
            {
                StyleButton(button);
            }
            else if (control is TextBox textBox)
            {
                StyleTextBox(textBox);
            }
            else if (control is ComboBox comboBox)
            {
                StyleComboBox(comboBox);
            }
            else if (control is ListBox listBox)
            {
                listBox.BackColor = Surface;
                listBox.ForeColor = Text;
                listBox.BorderStyle = BorderStyle.FixedSingle;
                listBox.Font = AppFonts.Create(Math.Max(9F, listBox.Font.SizeInPoints));
            }
            else if (control is DataGridView grid)
            {
                StyleGrid(grid);
            }
            else if (control is TabControl tabs)
            {
                StyleTabControl(tabs);
            }
            else if (control is TabPage page)
            {
                page.BackColor = Surface;
                page.ForeColor = Text;
            }
            else if (control is DateTimePicker picker)
            {
                picker.BackColor = Surface;
                picker.ForeColor = Text;
                picker.CalendarForeColor = Text;
                picker.CalendarMonthBackground = Surface;
                picker.CalendarTitleBackColor = SurfaceVariant;
                picker.CalendarTitleForeColor = Text;
                picker.CalendarTrailingForeColor = MutedText;
                picker.Font = AppFonts.Create(9F, FontStyle.Regular);
                picker.Format = DateTimePickerFormat.Custom;
                picker.CustomFormat = "yyyy/MM/dd";
                picker.Height = Math.Max(picker.Height, StandardInputHeight);
                WinFormsShapeAdapter.ApplyInput(picker);
            }
            else if (control is NumericUpDown numeric)
            {
                var frame = numeric.Parent as AntdUI.Panel;
                if (frame != null && ReferenceEquals(frame.Tag, DesktopComponents.NativeNumberFrameMarker))
                {
                    DesktopComponents.StyleNativeNumberFrame(frame, numeric);
                    return;
                }
                numeric.BackColor = Surface;
                numeric.ForeColor = Text;
                numeric.Height = Math.Max(numeric.Height, StandardInputHeight);
                WinFormsShapeAdapter.ApplyInput(numeric);
            }

        }

        // يرسم تبويبات متسقة مع الوضعين بدلاً من ترك رؤوس Windows الافتراضية بلون فاتح.
        private static void StyleTabControl(TabControl tabs)
        {
            if (tabs == null) return;
            tabs.BackColor = SurfaceVariant;
            tabs.ForeColor = Text;
            tabs.DrawMode = TabDrawMode.OwnerDrawFixed;
            tabs.DrawItem -= DrawThemedTab;
            tabs.DrawItem += DrawThemedTab;
            foreach (TabPage page in tabs.TabPages)
            {
                page.BackColor = Surface;
                page.ForeColor = Text;
            }
        }

        private static void DrawThemedTab(object sender, DrawItemEventArgs e)
        {
            TabControl tabs = sender as TabControl;
            if (tabs == null || e.Index < 0 || e.Index >= tabs.TabPages.Count) return;
            bool selected = e.Index == tabs.SelectedIndex;
            Rectangle bounds = e.Bounds;
            Color backColor = selected ? Primary : SurfaceVariant;
            Color foreColor = selected ? Color.White : Text;
            using (SolidBrush background = new SolidBrush(backColor))
            {
                e.Graphics.FillRectangle(background, bounds);
            }
            if (selected)
            {
                using (Pen accent = new Pen(Secondary, 2F))
                {
                    e.Graphics.DrawLine(accent, bounds.Left + 8, bounds.Bottom - 2, bounds.Right - 8, bounds.Bottom - 2);
                }
            }
            TextFormatFlags flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding;
            if (tabs.RightToLeft == RightToLeft.Yes) flags |= TextFormatFlags.RightToLeft;
            TextRenderer.DrawText(e.Graphics, tabs.TabPages[e.Index].Text, tabs.Font, bounds, foreColor, flags);
        }
        // يتحقق هل اللوحة في أعلى الشاشة.
        private static void InstallCloseNavigation(Form form)
        {
            if (form == null || form is Login || form is Home)
            {
                return;
            }

            form.KeyPreview = true;
            form.KeyDown -= AutoCloseForm_KeyDown;
            form.KeyDown += AutoCloseForm_KeyDown;
            form.Shown -= AutoCloseForm_Refresh;
            form.Shown += AutoCloseForm_Refresh;
            form.Resize -= AutoCloseForm_Refresh;
            form.Resize += AutoCloseForm_Refresh;
            form.ControlAdded -= AutoCloseForm_ControlAdded;
            form.ControlAdded += AutoCloseForm_ControlAdded;

            RefreshAutoCloseButton(form);
        }

        private static void AutoCloseForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Escape)
            {
                return;
            }

            Form form = sender as Form;
            if (form == null || form.IsDisposed)
            {
                return;
            }

            form.Close();
            e.Handled = true;
        }

        private static void AutoCloseForm_Refresh(object sender, EventArgs e)
        {
            RefreshAutoCloseButton(sender as Form);
        }

        private static void AutoCloseForm_ControlAdded(object sender, ControlEventArgs e)
        {
            Form form = sender as Form;
            if (form == null || form.IsDisposed || !form.IsHandleCreated)
            {
                return;
            }

            form.BeginInvoke(new Action(() => RefreshAutoCloseButton(form)));
        }

        private static void RefreshAutoCloseButton(Form form)
        {
            if (form == null || form.IsDisposed)
            {
                return;
            }

            Button autoClose = FindAutoCloseButton(form);
            bool shouldUseNativeChrome = form.FormBorderStyle != FormBorderStyle.None && form.ControlBox;
            bool hasVisibleClose = HasVisibleCloseAffordance(form);

            if (shouldUseNativeChrome || hasVisibleClose)
            {
                if (autoClose != null)
                {
                    autoClose.Visible = false;
                }

                return;
            }

            if (autoClose == null)
            {
                autoClose = CreateAutoCloseButton(form);
                form.Controls.Add(autoClose);
            }

            LayoutAutoCloseButton(form, autoClose);
            autoClose.Visible = true;
            autoClose.BringToFront();
        }

        private static Button FindAutoCloseButton(Form form)
        {
            Control[] matches = form.Controls.Find(AutoCloseButtonName, false);
            return matches.Length > 0 ? matches[0] as Button : null;
        }

        private static Button CreateAutoCloseButton(Form form)
        {
            Button button = new Button
            {
                Name = AutoCloseButtonName,
                Text = "إغلاق",
                Width = 104,
                Height = 34,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                BackColor = Danger,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = AppFonts.Create(9.5F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                TabStop = false
            };

            button.FlatAppearance.BorderSize = 0;
            button.Click += delegate { form.Close(); };
            AutoCloseToolTip.SetToolTip(button, "إغلاق الشاشة (Esc)");
            return button;
        }

        private static void LayoutAutoCloseButton(Form form, Button button)
        {
            int x = Math.Max(10, form.ClientSize.Width - button.Width - 18);
            button.Bounds = new Rectangle(x, 14, button.Width, button.Height);
        }

        private static bool HasVisibleCloseAffordance(Form form)
        {
            foreach (Control control in GetAllControls(form))
            {
                if (control == null || control.Name == AutoCloseButtonName || !control.Visible)
                {
                    continue;
                }

                if (!LooksLikeCloseControl(control))
                {
                    continue;
                }

                Rectangle bounds = GetBoundsInForm(form, control);
                if (bounds.Width > 0 && bounds.Height > 0 && bounds.Top <= 130 && form.ClientRectangle.IntersectsWith(bounds))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool LooksLikeCloseControl(Control control)
        {
            string text = (control.Text ?? string.Empty).Trim().ToLowerInvariant();
            if (text == "×" || text == "x")
            {
                return true;
            }

            return text.Contains("close")
                || text.Contains("exit")
                || text.Contains("إغلاق")
                || text.Contains("اغلاق")
                || text.Contains("خروج");
        }

        private static Rectangle GetBoundsInForm(Form form, Control control)
        {
            try
            {
                Point topLeft = form.PointToClient(control.PointToScreen(Point.Empty));
                return new Rectangle(topLeft, control.Size);
            }
            catch
            {
                return Rectangle.Empty;
            }
        }

        private static System.Collections.Generic.IEnumerable<Control> GetAllControls(Control root)
        {
            foreach (Control control in root.Controls)
            {
                yield return control;

                foreach (Control child in GetAllControls(control))
                {
                    yield return child;
                }
            }
        }

        private static bool IsTopPanel(Control control)
        {
            return control.Dock == DockStyle.Top || control.Top <= 5;
        }

        private static bool IsAccentPanel(Control control)
        {
            return control is Panel && IsTopPanel(control)
                && !string.Equals(Convert.ToString(control.Tag), "UiSurface", StringComparison.Ordinal);
        }

        // يضبط شكل النصوص والعناوين.
        private static void StyleLabel(Label label)
        {
            label.BackColor = Color.Transparent;

            if (label.Text == "×")
            {
                label.ForeColor = Danger;
                label.Font = AppFonts.Create(18F, FontStyle.Bold);
                return;
            }

            label.ForeColor = IsAccentPanel(label.Parent) ? Color.White : Text;

            if (label.Text.IndexOf("Press ", StringComparison.OrdinalIgnoreCase) >= 0
                || label.Text.IndexOf("Loading", StringComparison.OrdinalIgnoreCase) >= 0
                || label.Text.IndexOf("Failed", StringComparison.OrdinalIgnoreCase) >= 0
                || label.Text.IndexOf("succeeded", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                label.ForeColor = MutedText;
            }

            if (label.Text.IndexOf("Pharmacy", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                label.Font = AppFonts.Create(18F, FontStyle.Bold);
                label.ForeColor = IsAccentPanel(label.Parent) ? Color.White : Primary;
            }
            else if (label.Font.Bold)
            {
                label.Font = AppFonts.Create(Math.Max(10F, label.Font.Size), FontStyle.Bold);
            }
        }

        // يضبط أزرار شريط الأدوات: أيقونة فوق النص (مثل شاشات النظام).
        public static void ApplyToolbarButtons(params Button[] buttons)
        {
            activePalette = CreatePalette(LoadMode());

            foreach (Button button in buttons)
            {
                if (button == null)
                {
                    continue;
                }

                ApplyButtonChrome(button, AppFonts.Create(8.25F, FontStyle.Bold));
                AppIcon? icon = ResolveButtonIcon(button.Text);
                if (!icon.HasValue)
                {
                    continue;
                }

                SetButtonIcon(button, icon.Value, vertical: true, iconSize: 16);
            }
        }

        // يضبط شكل الأزرار وألوانها حسب وظيفتها ويضيف أيقونة متجهة بجانب النص.
        private static void StyleButton(Button button)
        {
            ApplyButtonChrome(button, AppFonts.Create(9.5F, FontStyle.Bold));
            AppIcon? icon = ResolveButtonIcon(button.Text);
            if (icon.HasValue)
            {
                SetButtonIcon(button, icon.Value, vertical: false, iconSize: 13);
            }
        }

        private static void ApplyButtonChrome(Button button, Font font)
        {
            string text = (button.Text ?? string.Empty).ToLower().Trim();

            bool isDanger = text.Contains("delete")
                || text.Contains("close")
                || text.Contains("exit")
                || text.Contains("حذف")
                || text.Contains("خروج")
                || text.Contains("إغلاق")
                || text.Contains("اغلاق");

            bool isSecondary = text.Contains("clear")
                || text.Contains("back")
                || text.Contains("مسح")
                || text.Contains("عودة")
                || text.Contains("رجوع")
                || text.Contains("إلغاء")
                || text.Contains("الغاء");

            bool isNav = text == "|<" || text == "<" || text == ">" || text == ">|";
            bool isHelp = text.Contains("help") || text.Contains("مساعدة") || text.Contains("أوراكس");

            button.UseVisualStyleBackColor = false;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.BackColor = isDanger ? Danger : isNav || isSecondary ? PrimaryDark : isHelp ? Secondary : Primary;
            button.ForeColor = Color.White;
            button.Font = font;
            button.Cursor = Cursors.Hand;
            button.TextAlign = ContentAlignment.MiddleCenter;
            button.ImageAlign = ContentAlignment.MiddleCenter;
            button.TextImageRelation = TextImageRelation.ImageBeforeText;
            button.Padding = new Padding(6, 0, 6, 0);
            int minimumWidth = isNav ? 48 : Math.Max(button.MinimumSize.Width, 104);
            button.MinimumSize = new Size(minimumWidth, button.MinimumSize.Height);
            if (button.Width < minimumWidth)
            {
                button.Width = minimumWidth;
            }
            button.Height = Math.Max(button.Height, StandardActionHeight);
            WinFormsShapeAdapter.ApplyAction(button);
        }

        internal static AppIcon? ResolveButtonIcon(string buttonText)
        {
            string text = (buttonText ?? string.Empty).ToLower().Trim();
            bool isNav = text == "|<" || text == "<" || text == ">" || text == ">|";

            if (text.Contains("add") || text.Contains("إضافة") || text.Contains("اضافة") || text.Contains("insert") || text.Contains("create"))
            {
                return AppIcon.Add;
            }

            if (isNav)
            {
                return text == "|<" || text == "<" ? AppIcon.Back : AppIcon.Forward;
            }

            if (text.Contains("help") || text.Contains("مساعدة") || text.Contains("أوراكس"))
            {
                return AppIcon.Settings;
            }

            if (text.Contains("update") || text.Contains("edit") || text.Contains("تعديل") || text.Contains("change"))
            {
                return AppIcon.Settings;
            }

            if (text.Contains("delete") || text.Contains("remove") || text.Contains("حذف"))
            {
                return AppIcon.Delete;
            }

            if (text.Contains("clear") || text.Contains("reset") || text.Contains("مسح") || text.Contains("جديد"))
            {
                return AppIcon.Refresh;
            }

            if (text.Contains("back") || text.Contains("عودة") || text.Contains("رجوع"))
            {
                return AppIcon.Back;
            }

            if (text.Contains("close") || text.Contains("exit") || text.Contains("خروج") || text.Contains("إغلاق") || text.Contains("اغلاق") || text.Contains("إلغاء") || text.Contains("الغاء"))
            {
                return AppIcon.Close;
            }

            if (text.Contains("اعتماد") || text.Contains("تطبيق") || text.Contains("توليد"))
            {
                return AppIcon.Confirm;
            }

            if (text.Contains("search") || text.Contains("filter") || text.Contains("بحث"))
            {
                return AppIcon.Search;
            }

            if (text.Contains("print") || text.Contains("طباعة") || text.Contains("pdf"))
            {
                return AppIcon.Print;
            }

            if (text.Contains("export") || text.Contains("تصدير") || text.Contains("csv") || text.Contains("excel"))
            {
                return AppIcon.Report;
            }

            if (text.Contains("save") || text.Contains("confirm") || text.Contains("حفظ"))
            {
                return AppIcon.Save;
            }

            if (text.Contains("refresh") || text.Contains("sync") || text.Contains("تحديث"))
            {
                return AppIcon.Refresh;
            }

            if (text.Contains("pharmacy info")
                || text.Contains("بيانات الصيدلية")
                || (text.Contains("صيدلية") && !text.Contains("مصنعة")))
            {
                return AppIcon.Medicine;
            }

            if (text.Contains("basic data")
                || text.Contains("master data")
                || text.Contains("البيانات الأساسية"))
            {
                return AppIcon.Database;
            }

            if (text.Contains("medicine") || text.Contains("أدوية") || text.Contains("دواء"))
            {
                return AppIcon.Medicine;
            }

            if (text.Contains("employee") || text.Contains("موظف"))
            {
                return AppIcon.User;
            }

            if (text.Contains("compan") || text.Contains("شركات") || text.Contains("مصنعة"))
            {
                return AppIcon.Language;
            }

            if (text.Contains("settings") || text.Contains("options") || text.Contains("إعدادات"))
            {
                return AppIcon.Settings;
            }

            return null;
        }

        private static void SetButtonIcon(Button button, AppIcon icon, bool vertical, int iconSize)
        {
            Image oldImage = button.Image;
            button.Image = OutlinedIcons.RenderBitmap(icon, iconSize, button.ForeColor, true);

            if (oldImage != null)
            {
                oldImage.Dispose();
            }

            if (vertical)
            {
                button.TextImageRelation = TextImageRelation.ImageBeforeText;
                button.ImageAlign = ContentAlignment.MiddleCenter;
                button.TextAlign = ContentAlignment.MiddleCenter;
                button.Padding = new Padding(6, 0, 6, 0);
            }
            else
            {
                button.TextImageRelation = TextImageRelation.ImageBeforeText;
                button.ImageAlign = ContentAlignment.MiddleCenter;
                button.TextAlign = ContentAlignment.MiddleCenter;
                button.Padding = new Padding(6, 0, 6, 0);
            }
        }

        public static void ApplyToolbarButtons(params AntdUI.Button[] buttons)
        {
            activePalette = CreatePalette(LoadMode());
            foreach (AntdUI.Button button in buttons)
            {
                if (button == null) continue;
                button.Font = AppFonts.Create(8.25F, FontStyle.Bold);
                button.Type = AntdUI.TTypeMini.Default;
                button.IconSvg = ResolveButtonIcon(button.Text).HasValue
                    ? OutlinedIcons.Svg(ResolveButtonIcon(button.Text).Value, true)
                    : null;
                button.IconSize = new Size(16, 16);
                button.IconPosition = AntdUI.TAlignMini.Top;
                AntdThemeAdapter.Apply(button);
            }
        }

        // يضبط شكل روابط النص.
        private static void StyleLinkLabel(LinkLabel linkLabel)
        {
            linkLabel.BackColor = Color.Transparent;
            linkLabel.LinkColor = Primary;
            linkLabel.ActiveLinkColor = PrimaryDark;
            linkLabel.VisitedLinkColor = Primary;
            linkLabel.Font = AppFonts.Create(Math.Max(9F, linkLabel.Font.Size), FontStyle.Bold);
        }

        // يضبط عناوين حقول الإدخال (تسميات الحقول).
        public static void ApplyFieldLabels(params Label[] labels)
        {
            activePalette = CreatePalette(LoadMode());

            foreach (Label label in labels)
            {
                if (label == null)
                {
                    continue;
                }

                label.BackColor = Color.Transparent;
                label.ForeColor = Primary;
                label.Font = AppFonts.Create(Math.Max(9F, label.Font.Size), FontStyle.Bold);
            }
        }

        // يضبط شكل حقول النص.
        private static void StyleTextBox(TextBox textBox)
        {
            var numeric = textBox.Parent as NumericUpDown;
            var numericFrame = numeric == null ? null : numeric.Parent as AntdUI.Panel;
            if (numericFrame != null && ReferenceEquals(numericFrame.Tag, DesktopComponents.NativeNumberFrameMarker))
            {
                DesktopComponents.StyleNativeNumberFrame(numericFrame, numeric);
                return;
            }
            var frame = textBox.Parent as AntdUI.Panel;
            if (frame != null && ReferenceEquals(frame.Tag, DesktopComponents.NativeTextFrameMarker))
            {
                DesktopComponents.StyleNativeTextFrame(frame, textBox);
                return;
            }
            textBox.BackColor = Surface;
            textBox.ForeColor = Text;
            textBox.BorderStyle = BorderStyle.FixedSingle;
            if (!textBox.Multiline) textBox.Height = Math.Max(textBox.Height, StandardInputHeight);
            WinFormsShapeAdapter.ApplyInput(textBox);
            UiDirection.Apply(textBox);
            InstallInputFocusFeedback(textBox);
        }

        // يضبط شكل القوائم المنسدلة.
        private static void StyleComboBox(ComboBox comboBox)
        {
            comboBox.BackColor = Surface;
            comboBox.ForeColor = Text;
            comboBox.FlatStyle = FlatStyle.Flat;
            comboBox.DrawMode = DrawMode.OwnerDrawFixed;
            comboBox.ItemHeight = Math.Max(comboBox.ItemHeight, 28);
            comboBox.Height = Math.Max(comboBox.Height, StandardInputHeight);
            WinFormsShapeAdapter.ApplyInput(comboBox);

            comboBox.DrawItem -= ComboBox_DrawItem;
            comboBox.DrawItem += ComboBox_DrawItem;
            UiDirection.Apply(comboBox);
            comboBox.Enter -= ComboBox_ClearTextSelection;
            comboBox.Enter += ComboBox_ClearTextSelection;
            var frame = comboBox.Parent as AntdUI.Panel;
            if (frame != null && ReferenceEquals(frame.Tag, DesktopComponents.NativeSelectFrameMarker))
                WinFormsShapeAdapter.RemoveInputShape(comboBox);
            comboBox.GotFocus -= ComboBox_ClearTextSelection;
            comboBox.GotFocus += ComboBox_ClearTextSelection;
            comboBox.MouseUp -= ComboBox_ClearTextSelection;
            comboBox.MouseUp += ComboBox_ClearTextSelection;
            comboBox.SelectedIndexChanged -= ComboBox_ClearTextSelection;
            comboBox.SelectedIndexChanged += ComboBox_ClearTextSelection;
            comboBox.SelectionChangeCommitted -= ComboBox_ClearTextSelection;
            comboBox.SelectionChangeCommitted += ComboBox_ClearTextSelection;
            comboBox.DropDownClosed -= ComboBox_ClearTextSelection;
            comboBox.DropDownClosed += ComboBox_ClearTextSelection;
            InstallInputFocusFeedback(comboBox);
        }

        // WinForms لا يتيح لون إطار ثابتاً للحقول الأصلية في جميع الإصدارات؛
        // لذلك نستخدم سطحاً فاتحاً عند التركيز، وهو واضح ومتسق مع الهوية في الوضعين.
  private static void InstallInputFocusFeedback(Control input)
  {
    input.Enter -= Input_Enter;
    input.Enter += Input_Enter;
    input.Leave -= Input_Leave;
    input.Leave += Input_Leave;
    input.EnabledChanged -= Input_EnabledChanged;
    input.EnabledChanged += Input_EnabledChanged;
    // The plain editors have no themed frame, so the keyboard focus is painted with a high
    // contrast outline instead of a background tint that cannot be told apart.
    input.Paint -= Input_Paint;
    input.Paint += Input_Paint;
  }

  private static void Input_Paint(object sender, PaintEventArgs e)
  {
    Control input = sender as Control;
    if (input == null || !input.Enabled || !input.Focused) return;
    using (Pen pen = new Pen(Primary, 2F))
    {
      Rectangle outline = input.ClientRectangle;
      outline.Width -= 1;
      outline.Height -= 1;
      e.Graphics.DrawRectangle(pen, outline);
    }
  }


        private static void Input_Enter(object sender, EventArgs e)
        {
            Control input = sender as Control;
            if (input != null && input.Enabled)
            {
                input.BackColor = Selection;
            }
        }

        private static void Input_Leave(object sender, EventArgs e)
        {
            ResetInputSurface(sender as Control);
        }

        private static void Input_EnabledChanged(object sender, EventArgs e)
        {
            ResetInputSurface(sender as Control);
        }

        private static void ResetInputSurface(Control input)
        {
            if (input == null || input.IsDisposed)
            {
                return;
            }

            input.BackColor = input.Enabled ? Surface : SurfaceVariant;
        }

        private static void ComboBox_DrawItem(object sender, DrawItemEventArgs e)
        {
            ComboBox comboBox = sender as ComboBox;
            if (comboBox == null)
            {
                return;
            }

            bool isEditArea = (e.State & DrawItemState.ComboBoxEdit) == DrawItemState.ComboBoxEdit;
            bool isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            Color backColor = isEditArea ? comboBox.BackColor : isSelected ? Selection : comboBox.BackColor;
            Color foreColor = comboBox.Enabled ? comboBox.ForeColor : MutedText;
            if (!comboBox.Enabled)
            {
                backColor = SurfaceVariant;
            }

            using (SolidBrush backgroundBrush = new SolidBrush(backColor))
            {
                e.Graphics.FillRectangle(backgroundBrush, e.Bounds);
            }

            string text = e.Index >= 0 && e.Index < comboBox.Items.Count
                ? comboBox.GetItemText(comboBox.Items[e.Index])
                : comboBox.Text;

            Rectangle textBounds = new Rectangle(e.Bounds.X + 6, e.Bounds.Y, Math.Max(0, e.Bounds.Width - 12), e.Bounds.Height);
            TextFormatFlags flags = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;
            flags |= comboBox.RightToLeft == RightToLeft.Yes
                ? TextFormatFlags.Right | TextFormatFlags.RightToLeft
                : TextFormatFlags.Left;

            TextRenderer.DrawText(e.Graphics, text, comboBox.Font, textBounds, foreColor, flags);
        }

        private static void ComboBox_ClearTextSelection(object sender, EventArgs e)
        {
            ComboBox comboBox = sender as ComboBox;
            if (comboBox == null || comboBox.DropDownStyle == ComboBoxStyle.DropDownList || !comboBox.IsHandleCreated)
            {
                return;
            }

            try
            {
                comboBox.BeginInvoke((MethodInvoker)delegate
                {
                    if (comboBox.IsDisposed || comboBox.DropDownStyle == ComboBoxStyle.DropDownList)
                    {
                        return;
                    }

                    int end = string.IsNullOrEmpty(comboBox.Text) ? 0 : comboBox.Text.Length;
                    comboBox.SelectionStart = end;
                    comboBox.SelectionLength = 0;
                });
            }
            catch (InvalidOperationException)
            {
            }
        }

        // يضبط شكل الجداول.
        private static void StyleGrid(DataGridView grid)
        {
            grid.BackgroundColor = Surface;
            grid.BorderStyle = BorderStyle.None;
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.EnableHeadersVisualStyles = false;
            grid.GridColor = Border;
            grid.RowHeadersVisible = false;
            grid.AllowUserToResizeRows = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            if (!OwnsCustomGridColumns(grid)) grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.ColumnHeadersHeight = OwnsCustomGridColumns(grid) ? Math.Max(40, grid.ColumnHeadersHeight) : 40;
            grid.RowTemplate.Height = StandardGridRowHeight;
            grid.ColumnHeadersDefaultCellStyle.BackColor = PrimaryDark;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = PrimaryDark;
            grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = Color.White;
            grid.ColumnHeadersDefaultCellStyle.Font = AppFonts.Create(9.75F, FontStyle.Bold);
            grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            grid.DefaultCellStyle.BackColor = Surface;
            grid.DefaultCellStyle.ForeColor = Text;
            grid.DefaultCellStyle.SelectionBackColor = Selection;
            grid.DefaultCellStyle.SelectionForeColor = Text;
            grid.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            grid.DefaultCellStyle.Padding = new Padding(4, 0, 4, 0);
            grid.AlternatingRowsDefaultCellStyle.BackColor = AlternateRow;
            AppFonts.ApplyToGrid(grid);
            if (!OwnsLocalizedGridPresentation(grid))
            {
                if (UiLanguageCatalog.IsEnglishActive) UiLanguageCatalog.Apply(grid);
                else ArabicUi.ApplyGrid(grid);
            }
            ApplyGridDirection(grid);
        }

        private static void ApplyGridDirection(DataGridView grid)
        {
            if (OwnsLocalizedGridPresentation(grid))
                UiDirection.Apply(grid, grid.RightToLeft == RightToLeft.No);
            else UiDirection.Apply(grid);
        }

        private static ThemePalette CreatePalette(string mode)
        {
            bool useDarkMode = string.Equals(mode, "Dark", StringComparison.OrdinalIgnoreCase)
                || (string.Equals(mode, "System", StringComparison.OrdinalIgnoreCase) && IsSystemDarkMode());

            return useDarkMode ? CreateDarkPalette() : CreateLightPalette();
        }

        // لوحة ألوان الوضع النهاري — نغمات هادئة مناسبة لبيئة صيدلية (تركواز / نعناع فاتح).
        private static ThemePalette CreateLightPalette()
        {
            return new ThemePalette(
                Color.FromArgb(255, 255, 255),
                Color.FromArgb(255, 255, 255),
                Color.FromArgb(246, 248, 247),
  Color.FromArgb(47, 137, 116),
  Color.FromArgb(27, 91, 78),
  Color.FromArgb(87, 163, 139),
  Color.FromArgb(27, 91, 78),
  Color.FromArgb(155, 94, 0),
                Color.FromArgb(255, 246, 229),
                Color.FromArgb(190, 73, 84),
                Color.FromArgb(31, 47, 43),
                Color.FromArgb(99, 116, 111),
                Color.FromArgb(222, 230, 226),
                Color.FromArgb(225, 243, 236),
                Color.FromArgb(250, 252, 251));
        }

        // لوحة ألوان الوضع الليلي بنفس الهوية مع تباين مريح للقراءة.
        private static ThemePalette CreateDarkPalette()
        {
            return new ThemePalette(
                Color.FromArgb(13, 22, 20),
                Color.FromArgb(25, 37, 34),
                Color.FromArgb(35, 52, 47),
  Color.FromArgb(82, 201, 180),
  Color.FromArgb(24, 92, 82),
  Color.FromArgb(109, 210, 166),
  Color.FromArgb(109, 210, 166),
  Color.FromArgb(255, 196, 92),
                Color.FromArgb(73, 55, 25),
                Color.FromArgb(238, 126, 139),
                Color.FromArgb(242, 249, 246),
                Color.FromArgb(182, 204, 196),
                Color.FromArgb(72, 102, 94),
                Color.FromArgb(47, 118, 101),
                Color.FromArgb(20, 31, 28));
        }

        // يقرأ تفضيل ويندوز للوضع الليلي عند اختيار System.
        private static bool IsSystemDarkMode()
        {
            try
            {
                object value = Registry.GetValue(
                    @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                    "AppsUseLightTheme",
                    1);

                return value is int && (int)value == 0;
            }
            catch { /* silent fallback */ return false; }
        }

        // يبدأ عملية استبدال الصور بالأيقونات.
        private static void ReplaceImageControls(Form form)
        {
            ReplaceImageControlsRecursive(form, form);
        }

        // يبحث داخل عناصر النموذج عن PictureBox لاستبداله.
        private static void ReplaceImageControlsRecursive(Control root, Form form)
        {
            Control[] controls = new Control[root.Controls.Count];
            root.Controls.CopyTo(controls, 0);

            foreach (Control control in controls)
            {
                if (IsImageControl(control) && !IsUserImagePictureBox(control))
                {
                    ReplaceImageControl(root, control, form);
                    continue;
                }

                if (control.HasChildren)
                {
                    ReplaceImageControlsRecursive(control, form);
                }
            }
        }

        // يتحقق هل العنصر عنصر صورة.
        private static bool IsImageControl(Control control)
        {
            string typeName = control.GetType().Name;
            return typeName.IndexOf("PictureBox", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // يبقي PictureBox لعرض صور يختارها المستخدم (شعار الشركة وغيره).
        private static bool IsUserImagePictureBox(Control control)
        {
            if (control == null || !IsImageControl(control))
            {
                return false;
            }

            string name = control.Name ?? string.Empty;
            if (name.StartsWith("picLogo", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return string.Equals(control.Tag as string, "UserImage", StringComparison.OrdinalIgnoreCase);
        }

        // يستبدل عنصر الصورة بأيقونة ويحافظ على حدث الضغط الأصلي.
        private static void ReplaceImageControl(Control parent, Control imageControl, Form form)
        {
            SetProperty(imageControl, "Image", null);
            imageControl.Visible = false;

            string targetName = imageControl.Name + "Icon";
            Control[] existing = parent.Controls.Find(targetName, false);
            if (existing.Length > 0 && existing[0] is Label existingIcon)
            {
                existingIcon.ForeColor = PrimaryDark;
                existingIcon.BackColor = SurfaceVariant;
                SetOutlinedLabelImage(existingIcon, GetAppIcon(imageControl.Name), imageControl);
                
                if (existingIcon.Tag is IconState state)
                {
                    // A palette refresh must not adopt the current hover zoom as layout.
                    if (state.Progress == 0.0 && state.TargetProgress == 0.0)
                        CaptureIconIdleState(state);
                    state.OriginalBackColor = SurfaceVariant;
                    state.OriginalForeColor = PrimaryDark;
                    UpdateLabelAnimationState(state);
                }
                
                existingIcon.BringToFront();
                return;
            }

            Label icon = new Label
            {
                Name = targetName,
                Text = string.Empty,
                Font = AppFonts.Create(9F, FontStyle.Regular),
                ForeColor = PrimaryDark,
                BackColor = SurfaceVariant,
            
                TextAlign = ContentAlignment.MiddleCenter,
                Bounds = imageControl.Bounds,
                Cursor = Cursors.Hand,
                BorderStyle = BorderStyle.None
            };
            SetOutlinedLabelImage(icon, GetAppIcon(imageControl.Name), imageControl);

            MethodInfo clickHandler = form.GetType().GetMethod(
                imageControl.Name + "_Click",
                BindingFlags.Instance | BindingFlags.NonPublic);

            if (clickHandler != null)
            {
                icon.Click += (sender, e) => clickHandler.Invoke(form, new object[] { sender, e });
            }
            // fallback: no click handler found — icon still visible, click does nothing by design

            icon.MouseEnter += Icon_MouseEnter;
            icon.MouseLeave += Icon_MouseLeave;

            parent.Controls.Add(icon);
            icon.BringToFront();
        }

        internal static void Icon_MouseEnter(object sender, EventArgs e)
        {
            Label label = sender as Label;
            if (label == null) return;

            // Initialize or retrieve the animation state for the control
            if (label.Tag == null)
            {
                label.Tag = new IconState
                {
                    OriginalBounds = label.Bounds,
                    OriginalFont = label.Font,
                    OriginalBackColor = label.BackColor,
                    OriginalForeColor = label.ForeColor,
                    Progress = 0.0,
                    TargetProgress = 0.0,
                    LabelControl = label
                };
                label.Disposed += IconAnimationDisposed;
            }

            IconState state = (IconState)label.Tag;
            if (state.Progress == 0.0 && state.TargetProgress == 0.0)
                CaptureIconIdleState(state);
            state.TargetProgress = 1.0;

            if (state.AnimTimer == null)
            {
                state.AnimTimer = new Timer();
                state.AnimTimer.Interval = 16; // ~60 FPS
                state.AnimTimer.Tick += AnimTimer_Tick;
                state.AnimTimer.Tag = state;
            }

            label.BringToFront();
            state.AnimTimer.Start();
        }

        internal static void Icon_MouseLeave(object sender, EventArgs e)
        {
            Label label = sender as Label;
            if (label == null) return;

            IconState state = label.Tag as IconState;
            if (state == null) return;

            state.TargetProgress = 0.0;

            if (state.AnimTimer == null)
            {
        
                state.AnimTimer = new Timer();
                state.AnimTimer.Interval = 16; // ~60 FPS
                state.AnimTimer.Tick += AnimTimer_Tick;
                state.AnimTimer.Tag = state;
            }

            state.AnimTimer.Start();
        }

        private static void CaptureIconIdleState(IconState state)
        {
            Label label = state.LabelControl;
            state.OriginalBounds = label.Bounds;
            // A retained generated font must never become a caller-owned baseline.
            if (!ReferenceEquals(label.Font, state.AnimationFont))
                state.OriginalFont = label.Font;
            state.OriginalBackColor = label.BackColor;
            state.OriginalForeColor = label.ForeColor;
        }

        private static void IconAnimationDisposed(object sender, EventArgs e)
        {
            IconState state = (sender as Label)?.Tag as IconState;
            if (state != null) DisposeIconAnimation(state);
        }

        private static void DisposeIconAnimation(IconState state)
        {
            if (state.AnimTimer != null)
            {
                state.AnimTimer.Stop();
                state.AnimTimer.Tag = null;
                state.AnimTimer.Dispose();
            }
            Font generated = state.AnimationFont;
            state.AnimationFont = null;
            if (generated != null) generated.Dispose();
        }

        private static void AnimTimer_Tick(object sender, EventArgs e)
        {
            Timer timer = sender as Timer;
            if (timer == null) return;

            IconState state = timer.Tag as IconState;
            if (state == null)
            {
                timer.Stop();
                timer.Dispose();
                return;
            }
            if (state.LabelControl == null || state.LabelControl.IsDisposed)
            {
                DisposeIconAnimation(state);
                return;
            }

            // Smooth interpolation step
            double step = 0.20;
            if (state.Progress < state.TargetProgress)
            {
                state.Progress = Math.Min(state.Progress + step, state.TargetProgress);
            }
            else if (state.Progress > state.TargetProgress)
            {
                state.Progress = Math.Max(state.Progress - step, state.TargetProgress);
            }

            // Repeated double subtraction can leave a positive residue whose float
            // font size already equals the original. Finish at the exact endpoint.
            if (Math.Abs(state.Progress - state.TargetProgress) < 1e-9)
                state.Progress = state.TargetProgress;
            UpdateLabelAnimationState(state);

            if (state.Progress == state.TargetProgress)
            {
                timer.Stop();
            }
        }

        private static void UpdateLabelAnimationState(IconState state)
        {
            Label label = state.LabelControl;
            double progress = state.Progress;

            // 1. Interpolate Bounds (grow by 12%)
            Rectangle originalBounds = state.OriginalBounds;
            int zoomWidth = (int)(originalBounds.Width * 0.12);
            int zoomHeight = (int)(originalBounds.Height * 0.12);

            Rectangle targetBounds = new Rectangle(
                originalBounds.Left - zoomWidth / 2,
                originalBounds.Top - zoomHeight / 2,
                originalBounds.Width + zoomWidth,
                originalBounds.Height + zoomHeight
            );

            int currentLeft = (int)(originalBounds.Left + (targetBounds.Left - originalBounds.Left) * progress);
            int currentTop = (int)(originalBounds.Top + (targetBounds.Top - originalBounds.Top) * progress);
            int currentWidth = (int)(originalBounds.Width + (targetBounds.Width - originalBounds.Width) * progress);
            int currentHeight = (int)(originalBounds.Height + (targetBounds.Height - originalBounds.Height) * progress);

            label.Bounds = new Rectangle(currentLeft, currentTop, currentWidth, currentHeight);

            // 2. Interpolate Font Size (scale vector icon)
            Font originalFont = state.OriginalFont;
            float targetFontSize = originalFont.Size * 1.12f;
            float currentFontSize = (float)(originalFont.Size + (targetFontSize - originalFont.Size) * progress);
            Font previousGenerated = state.AnimationFont;
            bool generated = progress > 0.0;
            Font nextFont = generated
                ? new Font(originalFont.FontFamily, currentFontSize, originalFont.Style)
                : originalFont;
            try { label.Font = nextFont; }
            catch
            {
                if (generated && !ReferenceEquals(label.Font, nextFont)) nextFont.Dispose();
                throw;
            }
            // Control.Font may keep its existing reference when values are equal.
            // Dispose only fonts we created and that are actually detached.
            Font assigned = label.Font;
            if (generated && !ReferenceEquals(assigned, nextFont)) nextFont.Dispose();
            if (previousGenerated != null && !ReferenceEquals(assigned, previousGenerated))
                previousGenerated.Dispose();
            state.AnimationFont = generated && ReferenceEquals(assigned, nextFont) ? nextFont
                : ReferenceEquals(assigned, previousGenerated) ? previousGenerated : null;
            if (label.IsDisposed) { DisposeIconAnimation(state); return; }

            // Preserve the exact base style after the transition ends.
            label.BackColor = progress <= 0.0 ? state.OriginalBackColor
                : InterpolateColor(state.OriginalBackColor, Primary, progress);
            label.ForeColor = progress <= 0.0 ? state.OriginalForeColor
                : InterpolateColor(state.OriginalForeColor, Color.White, progress);
        }

        private static Color InterpolateColor(Color c1, Color c2, double progress)
        {
            int r = (int)(c1.R + (c2.R - c1.R) * progress);
            int g = (int)(c1.G + (c2.G - c1.G) * progress);
            int b = (int)(c1.B + (c2.B - c1.B) * progress);
            return Color.FromArgb(r, g, b);
        }

        private class IconState
        {
            public Rectangle OriginalBounds { get; set; }
            public Font OriginalFont { get; set; }
            public Font AnimationFont { get; set; }
            public Color OriginalBackColor { get; set; }
            public Color OriginalForeColor { get; set; }
            public double Progress { get; set; }
            public double TargetProgress { get; set; }
            public Timer AnimTimer { get; set; }
            public Label LabelControl { get; set; }
        }

        // يحسب حجم الأيقونة المناسب لمساحة الصورة القديمة.
        private static float GetIconSize(Control control)
        {
            return Math.Max(24F, Math.Min(control.Width, control.Height) * 0.45F);
        }

        private static void SetOutlinedLabelImage(Label label, AppIcon icon, Control source)
        {
            Image previous = label.BackgroundImage;
            int size = Math.Max(24, Math.Min(64, (int)GetIconSize(source)));
            label.BackgroundImage = OutlinedIcons.RenderBitmap(icon, size, PrimaryDark, true);
            label.BackgroundImageLayout = ImageLayout.Center;
            if (previous != null && !ReferenceEquals(previous, label.BackgroundImage)) previous.Dispose();
        }

        // يختار أيقونة Outlined دلالية حسب اسم عنصر الصورة القديم.
        private static AppIcon GetAppIcon(string controlName)
        {
            switch (controlName)
            {
                case "pictureBox1":
                    return AppIcon.Medicine;
                case "pictureBox2":
                    return AppIcon.User;
                case "pictureBox3":
                    return AppIcon.Language;
                case "pictureBox4":
                    return AppIcon.Sale;
                case "pictureBox5":
                    return AppIcon.Logout;
                case "pictureBox6":
                    return AppIcon.Database;
                case "DashBoard":
                    return AppIcon.Dashboard;
                case "butSetings":
                    return AppIcon.Settings;
                default: return AppIcon.Settings;

            }
        }

        // يمثل مجموعة ألوان كاملة للوضع الحالي.
        private sealed class ThemePalette
        {
            public readonly Color Background;
            public readonly Color Surface;
            public readonly Color SurfaceVariant;
            public readonly Color Primary;
            public readonly Color PrimaryDark;
            public readonly Color Secondary;
            public readonly Color SuccessText;
            public readonly Color Warning;
            public readonly Color WarningSurface;
            public readonly Color Danger;
            public readonly Color Text;
            public readonly Color MutedText;
            public readonly Color Border;
            public readonly Color Selection;
            public readonly Color AlternateRow;

            public ThemePalette(
                Color background,
                Color surface,
                Color surfaceVariant,
                Color primary,
                Color primaryDark,
                Color secondary,
                Color successText,
                Color warning,
                Color warningSurface,
                Color danger,
                Color text,
                Color mutedText,
                Color border,
                Color selection,
                Color alternateRow)
            {
                Background = background;
                Surface = surface;
                SurfaceVariant = surfaceVariant;
                Primary = primary;
                PrimaryDark = primaryDark;
                Secondary = secondary;
            SuccessText = successText;
                Warning = warning;
                WarningSurface = warningSurface;
                Danger = danger;
                Text = text;
                MutedText = mutedText;
                Border = border;
                Selection = selection;
                AlternateRow = alternateRow;
            }
        }

        // يفعّل الرسم المزدوج وخلفية نافذة الحوار لتجربة سلسة.
        public static void ApplyDialogShell(Form form, Panel body)
        {
            activePalette = CreatePalette(LoadMode());
            if (form != null)
            {
                form.BackColor = Background;
                EnableDoubleBuffer(form);
            }

            if (body != null)
            {
                body.BackColor = Background;
                EnableDoubleBuffer(body);
            }
        }

        // شريط أدوات سفلي موحّد (شاشة بيانات الشركة وغيرها).
        public static void ApplyToolbarDock(Panel toolbar, FlowLayoutPanel flow)
        {
            activePalette = CreatePalette(LoadMode());
            if (toolbar != null)
            {
                toolbar.BackColor = PrimaryDark;
                EnableDoubleBuffer(toolbar);
            }

            if (flow != null)
            {
                flow.BackColor = PrimaryDark;
            }
        }

        // زر إجراء رئيسي (اختيار صورة، حفظ سريع، …).
        public static void ApplyPrimaryButton(Button button)
        {
            if (button == null)
            {
                return;
            }

            activePalette = CreatePalette(LoadMode());
            button.UseVisualStyleBackColor = false;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.BackColor = Primary;
            button.ForeColor = Color.White;
            button.Font = AppFonts.Create(9.5F, FontStyle.Bold);
            button.Cursor = Cursors.Hand;
            button.Padding = new Padding(6, 4, 6, 4);
            button.Height = Math.Max(button.Height, 38);

            button.MouseEnter += delegate { button.BackColor = PrimaryDark; };
            button.MouseLeave += delegate { button.BackColor = Primary; };
        }

        public static void ApplyPrimaryButton(AntdUI.Button button)
        {
            if (button == null) return;
            activePalette = CreatePalette(LoadMode());
            button.Type = AntdUI.TTypeMini.Primary;
            button.BorderWidth = 0;
            button.BackColor = Primary;
            button.ForeColor = Color.White;
            button.Font = AppFonts.Create(9.5F, FontStyle.Bold);
            button.Cursor = Cursors.Hand;
            button.Padding = new Padding(6, 4, 6, 4);
            button.Height = Math.Max(button.Height, 38);
            AntdThemeAdapter.Apply(button);
        }

        // إطار مجموعة حقول بمظهر مسطح عصري.
        public static void ApplyGroupBox(GroupBox box)
        {
            if (box == null)
            {
                return;
            }

            activePalette = CreatePalette(LoadMode());
            box.ForeColor = PrimaryDark;
            box.Font = AppFonts.Create(9.5F, FontStyle.Bold);
            box.BackColor = Surface;
        }

        // يفعّل الرسم المزدوج لتقليل الوميض في النوافذ والبطاقات.
        public static void EnableDoubleBuffer(Control control)
        {
            if (control == null)
            {
                return;
            }

            try
            {
                typeof(Control).InvokeMember(
                    "DoubleBuffered",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.SetProperty,
                    null,
                    control,
                    new object[] { true });
            }
            catch { /* silent fallback */ }
        }

        // يضبط رأس نوافذ القوائم (إعدادات، بيانات أساسية، …).
        public static void ApplyDialogHeader(Panel header, Label title, Label subtitle, Label closeButton)
        {
            activePalette = CreatePalette(LoadMode());
            header.BackColor = PrimaryDark;
            header.Padding = new Padding(20, 12, 20, 10);

            if (title != null)
            {
                title.BackColor = Color.Transparent;
                title.ForeColor = Color.White;
                title.Font = AppFonts.Create(16F, FontStyle.Bold);
                ApplyHeaderTextDirection(title);
            }

            if (subtitle != null)
            {
                subtitle.BackColor = Color.Transparent;
                subtitle.ForeColor = Color.FromArgb(220, 240, 235);
                subtitle.Font = AppFonts.Create(9.5F, FontStyle.Regular);
                ApplyHeaderTextDirection(subtitle);
            }

            if (closeButton != null)
            {
                closeButton.BackColor = Color.Transparent;
                closeButton.ForeColor = Color.White;
                closeButton.Font = AppFonts.Create(16F, FontStyle.Bold);
                closeButton.Cursor = Cursors.Hand;
            }
        }

        // عنوان الحوار يرث اتجاه النموذج. نجمده بصرياً كي لا يقلب RightToLeft.TextAlign مرتين.
        private static void ApplyHeaderTextDirection(Label label)
        {
            if (label == null) return;

            label.RightToLeftChanged -= HeaderTextDirectionChanged;
            label.RightToLeftChanged += HeaderTextDirectionChanged;
            ApplyHeaderTextDirection(label, UiDirection.IsLeftToRight);
        }

        private static void HeaderTextDirectionChanged(object sender, EventArgs e)
        {
            Label label = sender as Label;
            if (label != null) ApplyHeaderTextDirection(label, UiDirection.IsLeftToRight);
        }

        private static void ApplyHeaderTextDirection(Label label, bool leftToRight)
        {
            label.RightToLeft = RightToLeft.No;
            label.TextAlign = leftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
        }
        // بطاقة قائمة عصرية: أيقونة، عنوان، وصف، سهم — مع تأثير تمرير وضغط.
        public static void ApplyMenuCard(
            Panel card,
            Label icon,
            Label title,
            Label subtitle,
            Label chevron,
            EventHandler click)
        {
            activePalette = CreatePalette(LoadMode());
            EnableDoubleBuffer(card);
            card.BackColor = Surface;
            card.Cursor = Cursors.Hand;
            card.Padding = new Padding(18, 14, 18, 14);
            card.Margin = new Padding(0);
            card.Tag = new MenuCardState();
            WinFormsShapeAdapter.ApplyCard(card);

            if (icon != null)
            {
                EnableDoubleBuffer(icon);
                icon.BackColor = SurfaceVariant;
                icon.ForeColor = PrimaryDark;
                icon.Font = AppFonts.Create(9F, FontStyle.Regular);
                icon.TextAlign = ContentAlignment.MiddleCenter;
                icon.Cursor = Cursors.Hand;
                icon.Padding = new Padding(4);
            }

            if (title != null)
            {
                title.BackColor = Color.Transparent;
                title.ForeColor = Text;
                title.Font = AppFonts.Create(11.5F, FontStyle.Bold);
                title.Cursor = Cursors.Hand;
            }

            if (subtitle != null)
            {
                subtitle.BackColor = Color.Transparent;
                subtitle.ForeColor = MutedText;
                subtitle.Font = AppFonts.Create(9.25F, FontStyle.Regular);
                subtitle.Cursor = Cursors.Hand;
            }

            if (chevron != null)
            {
                chevron.BackColor = Color.Transparent;
                chevron.ForeColor = Primary;
                Image previous = chevron.BackgroundImage;
                chevron.Font = AppFonts.Create(9F, FontStyle.Regular);
                chevron.Text = string.Empty;
                chevron.BackgroundImage = OutlinedIcons.RenderBitmap(AppIcon.Back, 18, Primary, true);
                chevron.BackgroundImageLayout = ImageLayout.Center;
                if (previous != null && !ReferenceEquals(previous, chevron.BackgroundImage)) previous.Dispose();
                chevron.TextAlign = ContentAlignment.MiddleCenter;
                chevron.Cursor = Cursors.Hand;
            }

            EventHandler handler = click ?? delegate { };
            WireCardClick(card, handler, icon, title, subtitle, chevron);

            EventHandler setHover = delegate
            {
                MenuCardState state = card.Tag as MenuCardState;
                if (state == null || state.Pressed)
                {
                    return;
                }

                state.Hover = true;
                card.BackColor = SurfaceVariant;
                if (icon != null)
                {
                    icon.BackColor = Color.FromArgb(
                        Math.Min(255, SurfaceVariant.R + 10),
                        Math.Min(255, SurfaceVariant.G + 10),
                        Math.Min(255, SurfaceVariant.B + 10));
                }

                card.Invalidate();
            };

            EventHandler clearHover = delegate
            {
                MenuCardState state = card.Tag as MenuCardState;
                if (state == null)
                {
                    return;
                }

                state.Hover = false;
                state.Pressed = false;
                card.BackColor = Surface;
                if (icon != null)
                {
                    icon.BackColor = SurfaceVariant;
                }

                card.Invalidate();
            };

            MouseEventHandler press = delegate
            {
                MenuCardState state = card.Tag as MenuCardState;
                if (state == null)
                {
                    return;
                }

                state.Pressed = true;
                card.BackColor = Color.FromArgb(
                    Math.Max(0, SurfaceVariant.R - 6),
                    Math.Max(0, SurfaceVariant.G - 6),
                    Math.Max(0, SurfaceVariant.B - 6));
                card.Invalidate();
            };

            MouseEventHandler release = delegate
            {
                MenuCardState state = card.Tag as MenuCardState;
                if (state == null)
                {
                    return;
                }

                state.Pressed = false;
                setHover(null, EventArgs.Empty);
            };

            card.MouseEnter += setHover;
            card.MouseLeave += clearHover;
            card.MouseDown += press;
            card.MouseUp += release;

            if (icon != null)
            {
                icon.MouseEnter += setHover;
                icon.MouseLeave += clearHover;
                icon.MouseDown += press;
                icon.MouseUp += release;
            }

            card.Paint -= MenuCard_Paint;
            card.Paint += MenuCard_Paint;
        }

        private sealed class MenuCardState
        {
            public bool Hover;
            public bool Pressed;
        }

        private static void MenuCard_Paint(object sender, PaintEventArgs e)
        {
            Panel card = sender as Panel;
            if (card == null)
            {
                return;
            }

            activePalette = CreatePalette(LoadMode());
            MenuCardState state = card.Tag as MenuCardState;
            bool hover = state != null && state.Hover;

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle bounds = card.ClientRectangle;
            bounds.Inflate(-1, -1);

            using (GraphicsPath path = CreateRoundedRect(bounds, 10))
            using (SolidBrush fill = new SolidBrush(card.BackColor))
            using (Pen border = new Pen(hover ? Primary : Border, hover ? 1.6f : 1f))
            {
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(border, path);
            }

        }

        private static GraphicsPath CreateRoundedRect(Rectangle bounds, int radius)
        {
            int diameter = radius * 2;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static void WireCardClick(Panel card, EventHandler handler, params Control[] parts)
        {
            card.Click += handler;
            if (parts == null)
            {
                return;
            }

            foreach (Control part in parts)
            {
                if (part != null)
                {
                    part.Click += handler;
                }
            }
        }

        // يضبط خاصية بالانعكاس لدعم مكتبات عناصر طرف ثالث عند الحاجة.
        private static void SetProperty(object target, string propertyName, object value)
        {
            PropertyInfo property = target.GetType().GetProperty(propertyName);

            if (property == null || !property.CanWrite)
            {
                return;
            }

            try
            {
                property.SetValue(target, value, null);
            }
            catch
            {
                // Some third-party controls reject properties at runtime; ignore and continue styling.
            }
        }
    
        
    


    }
}


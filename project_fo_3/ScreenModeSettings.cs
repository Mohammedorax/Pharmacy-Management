using System;
using System.ComponentModel;
using System.Configuration;
using System.Drawing;
using System.Windows.Forms;

namespace project_fo_3
{
    internal static class ScreenModeSettings
    {
        internal static bool LoadOpeningPreference()
        {
            try { return Properties.Setings.Default.OpenScreensMaximized; }
            catch { return true; }
        }

        internal static bool TrySaveOpeningPreference(bool maximized)
        {
            bool previous = LoadOpeningPreference();
            try
            {
                Properties.Setings.Default.OpenScreensMaximized = maximized;
                Properties.Setings.Default.Save();
                return true;
            }
            catch
            {
                try { Properties.Setings.Default.OpenScreensMaximized = previous; } catch { }
                return false;
            }
        }

        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Form, object> OpeningPolicy =
            new System.Runtime.CompilerServices.ConditionalWeakTable<Form, object>();

        internal static void InstallOpeningPreference(Form form)
        {
            if (form == null || form is Home || form is Login || OpeningPolicy.TryGetValue(form, out _)) return;
            OpeningPolicy.Add(form, new object());
            form.Shown += (sender, args) =>
            {
                // Resizable application screens follow the user's preference even when owned.
                // Keep explicit business families compatible with derived external screens.
                bool nestedBusinessScreen = form.GetType().Assembly == typeof(ScreenModeSettings).Assembly ||
                    form is InventoryDataForm || form is CommercialSalesReturnForm ||
                    form is CommercialSalesReturnHistoryForm || form is InventoryIntegrityForm ||
                    form is MedicineCatalogImportForm || form is AccountingIntegrityForm ||
                    form is AccountingPeriodManagementForm || form is PeriodCloseReadinessForm ||
                    form is ActiveIngredientManagementForm || form is CustomerSupplierManagementForm ||
                    form is EmployeeOperationPermissionsForm || form is ExcelDataTransferForm ||
                    form is InvoiceImportDraftForm || form is QuarantineResolutionForm ||
                    form is SystemMonitorForm || form is CashBoxManagementForm ||
                    form is BootstrapManagerForm || form is MedicineUnitCatalogEditor ||
                    form is AccountingDataForm || form is PharmacyCompanyForm || form is QaCsvImportForm;
                bool compactEditor = form is ReportPrintOptionsDialog || form is ReportColumnLayoutDialog || form is ReportTemplateNameDialog;
                if (form.Parent != null || compactEditor || (form.StartPosition == FormStartPosition.CenterParent && !nestedBusinessScreen) ||
                    form.FormBorderStyle != FormBorderStyle.Sizable || !form.MaximizeBox) return;
                ApplyOpeningPreference(form, form.Owner);
            };
        }

        internal static void ApplyOpeningPreference(Form form, Control owner)
        {
            if (form == null) return;
            Size normalSize = form.WindowState == FormWindowState.Normal || form.RestoreBounds.IsEmpty
                ? form.Size : form.RestoreBounds.Size;
            Rectangle area = GetWorkingArea(form, owner);
            form.SuspendLayout();
            try
            {
                // Opening preference changes geometry only. Each screen owns its scroll containers,
                // border, buttons and minimum sizing; changing those breaks compact editor layouts.
                form.WindowState = FormWindowState.Normal;
                form.StartPosition = FormStartPosition.Manual;
                int width = Math.Min(normalSize.Width, Math.Max(1, area.Width - 48));
                int height = Math.Min(normalSize.Height, Math.Max(1, area.Height - 48));
                form.Bounds = new Rectangle(area.Left + Math.Max(0, (area.Width - width) / 2),
                    area.Top + Math.Max(0, (area.Height - height) / 2), width, height);
                // Windows enforces the screen's MinimumSize while assigning Bounds.
                // Center the resulting size, rather than the smaller requested size.
                form.Location = new Point(area.Left + Math.Max(0, (area.Width - form.Width) / 2),
                    area.Top + Math.Max(0, (area.Height - form.Height) / 2));
                // Set the normal bounds first so Restore returns to the usual centered size.
                // Assigning the working area before maximizing would overwrite RestoreBounds.
                if (LoadOpeningPreference()) form.WindowState = FormWindowState.Maximized;
            }
            finally { form.ResumeLayout(true); }
        }
        private const string FullScreenKey = "UseFullScreen";

        public static bool IsFullScreenEnabled()
        {
            try
            {
                // تفضيل واجهة خاص بالمستخدم؛ لا نضعه في ملف exe.config الذي يستبدله البناء والنشر.
                return Properties.Setings.Default.UseFullScreen;
            }
            catch
            {
                // توافق مع الإصدارات السابقة التي كانت تحفظه ضمن App.config.
                bool value;
                string savedValue = ConfigurationManager.AppSettings[FullScreenKey];
                return bool.TryParse(savedValue, out value) ? value : true;
            }
        }

        public static void SaveFullScreen(bool enabled)
        {
            try
            {
                Properties.Setings.Default.UseFullScreen = enabled;
                Properties.Setings.Default.Save();
                return;
            }
            catch
            {
                // يستمر التغيير للجلسة الحالية حتى لو منع النظام الكتابة في ملف المستخدم.
            }
        }

        public static void Apply(Form form, bool fullScreen, Size normalSize)
        {
            Apply(form, fullScreen, normalSize, null);
        }

        public static void Apply(Form form, bool fullScreen, Size normalSize, Control screenSource)
        {
            if (form == null)
            {
                return;
            }

            Rectangle area = GetWorkingArea(form, screenSource);
            form.SuspendLayout();
            try
            {
                form.WindowState = FormWindowState.Normal;
                // نستخدم إطار Windows القياسي: يحافظ على الوصول، Alt+Tab، السحب بين
                // الشاشات، وأزرار التصغير/التكبير بجانب الإغلاق.
                form.FormBorderStyle = FormBorderStyle.Sizable;
                form.StartPosition = FormStartPosition.Manual;
                form.ShowIcon = false;
                form.MaximizeBox = true;
                form.MinimizeBox = true;
                form.AutoScroll = true;

                int width = Math.Min(normalSize.Width, Math.Max(360, area.Width - 48));
                int height = Math.Min(normalSize.Height, Math.Max(420, area.Height - 48));
                Rectangle targetBounds = new Rectangle(
                    area.Left + Math.Max(0, (area.Width - width) / 2),
                    area.Top + Math.Max(0, (area.Height - height) / 2),
                    width,
                    height);

                if (form.Bounds != targetBounds)
                {
                    form.Bounds = targetBounds;
                }
                // Preserve the supplied normal size for the standard Restore button.
                if (fullScreen) form.WindowState = FormWindowState.Maximized;
            }
            finally
            {
                form.ResumeLayout(true);
            }
        }

        private static Rectangle GetWorkingArea(Form form, Control screenSource)
        {
            Control[] candidates =
            {
                screenSource,
                form != null && form.IsHandleCreated ? form : null,
                Form.ActiveForm
            };

            foreach (Control candidate in candidates)
            {
                if (candidate == null || candidate.IsDisposed || candidate.Disposing || !candidate.IsHandleCreated)
                {
                    continue;
                }

                try
                {
                    return Screen.FromControl(candidate).WorkingArea;
                }
                catch (Win32Exception)
                {
                }
                catch (InvalidOperationException)
                {
                }
            }

            Screen cursorScreen = Screen.FromPoint(Cursor.Position);
            if (cursorScreen != null)
            {
                return cursorScreen.WorkingArea;
            }

            return Screen.PrimaryScreen.WorkingArea;
        }

    }
}




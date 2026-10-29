// شرح الملف: نقطة بداية تشغيل التطبيق.
// يبدأ النظام من شاشة الدخول حتى تكون إعدادات قاعدة البيانات متاحة قبل أي اتصال.
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace project_fo_3
{
    // نقطة تشغيل التطبيق.
    // يبدأ النظام من شاشة الدخول مباشرة حتى يستطيع المستخدم تعديل اتصال قاعدة البيانات عبر F1 عند الحاجة.
    static class Program
    {
        // الدالة الرئيسية التي تضبط إعدادات WinForms وتفتح شاشة الدخول.
        [STAThread]
        static void Main(string[] args)
        {
            bool isAutomationCommand = IsAutomationCommand(args);
            try
            {
                // يجب تفعيل التسجيل قبل أي فحص أو اتصال حتى لا تتحول أخطاء أوامر الصيانة
                // إلى نافذة Windows عامة بلا رمز دعم أو سجل قابل للتشخيص.
                AppDiagnostics.ConfigureGlobalExceptionHandling();
                AntdDateInput.UseGregorianCalendarForProcess();
                if(isAutomationCommand)ConfigureAutomationConsole();
                using (var previewLanguage = isAutomationCommand && args != null && args.Length == 1 &&
                    args[0].StartsWith("--preview-", StringComparison.OrdinalIgnoreCase)
                    ? AntdControlLocalization.BeginPreviewLanguage(args[0].IndexOf("-english", StringComparison.OrdinalIgnoreCase) >= 0)
                    : null)
                    RunApplication(args);
            }
            catch (Exception exception)
            {
                Environment.ExitCode = 1;
                string category = isAutomationCommand ? "CommandLine:" + args[0] : "Startup";
                string supportCode = AppDiagnostics.Log(category, exception);

                if (isAutomationCommand)
                {
                    try
                    {
                        string failureReason = SafeVerificationFailureReason(exception);
                        Console.Error.WriteLine(
                            "Command failed safely. SupportCode={0}; ExceptionType={1}; HResult=0x{2:X8}{3}",
                            supportCode,
                            exception.GetType().FullName,
                            exception.HResult,
                            string.IsNullOrEmpty(failureReason) ? string.Empty : "; Reason=" + failureReason);
                    }
                    catch
                    {
                        // لا نسمح لفشل قناة stderr بإعادة الخطأ العام الذي يعالجه هذا الحاجز.
                    }
                    return;
                }

                AppNotifications.Show(
                    "تعذر بدء النظام بشكل آمن. لم تُنفذ أي عملية حفظ.\n\n" +
                    "أغلق التطبيق ثم أعد تشغيله. إذا تكرر الخطأ، أرسل رمز الدعم: " + supportCode,
                    "تعذر بدء النظام",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private static void ConfigureAutomationConsole()
        {
            var encoding=new System.Text.UTF8Encoding(false);
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput(),encoding){AutoFlush=true});
            Console.SetError(new StreamWriter(Console.OpenStandardError(),encoding){AutoFlush=true});
        }

        // رسائل فحوص التحقق ثابتة ولا تحمل اتصالًا أو بيانات عميل، فنطبعها لتسريع تشخيص البوابة.
        // أي استثناء آخر (SQL أو اتصال أو إعداد) يبقي تفاصيله في السجل الداخلي فقط.
        private static string SafeVerificationFailureReason(Exception exception)
        {
            if (!(exception is InvalidOperationException)) return null;
            string message = exception.Message;
            if (string.IsNullOrWhiteSpace(message)) return null;
            string[] forbidden = { "Server=", "Data Source=", "Initial Catalog=", "Password=", "User ID=", "SqlException", "connection string" };
            foreach (string marker in forbidden)
            {
                if (message.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0) return null;
            }
            message = message.Replace("\r", " ").Replace("\n", " ").Trim();
            if (message.Length > 500) message = message.Substring(0, 500);
            return message;
        }
        private static void RunQaLiveInventoryReportUi(string reportKey)
        {
            // Never let this UI test seam inherit the shared ProgramData runtime profile.
            // It only starts with a copied app.config that pins a disposable _qa database.
            if (!RuntimeSettingsStore.IsBuildConnectionProfileForced)
                throw new UnauthorizedAccessException("تشغيل تقرير حي للاختبار يتطلب ملف اتصال QA ثابتًا، ولا يستخدم إعداد التشغيل المشترك.");
            DatabaseConnectionProfile profile = Database.GetConnectionProfile();
            if (profile == null || string.IsNullOrWhiteSpace(profile.DatabaseName) ||
                !profile.DatabaseName.EndsWith("_qa", StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("معاينة التقرير الحية مسموحة فقط على قاعدة ينتهي اسمها بـ _qa.");

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            AppFonts.Initialize();
            SessionContext.Start("QALiveReadVerifier", "Administrator", "QA-READ-ONLY");
            System.Windows.Forms.Form form = null;
            try
            {
                switch ((reportKey ?? string.Empty).Trim().ToLowerInvariant())
                {
                    case "inventory-stock": form = new InventoryStockReportForm(false); break;
                    case "inventory-lots": form = new InventoryStockReportForm(true); break;
                    case "inventory-summary": form = new InventoryMovementSummaryForm(true); break;
                    case "inventory-turnover": form = new InventoryTurnoverForm(true); break;
                    case "inventory-alerts": form = new InventoryAlertsForm(true); break;
                    case "inventory-movement": form = new InventoryMovementForm(true); break;
                    case "inventory-integrity": form = new InventoryIntegrityForm(false); break;
                    default: throw new ArgumentException("نوع تقرير QA الحي غير مدعوم.", nameof(reportKey));
                }
                Application.Run(form);
            }
            finally
            {
                if (form != null) form.Dispose();
                SessionContext.Clear();
            }
        }

        private static void RunApplication(string[] args)
        {
            if (args != null && args.Length == 2 &&
                string.Equals(args[0], "--qa-live-inventory-report-ui", StringComparison.OrdinalIgnoreCase))
            {
                RunQaLiveInventoryReportUi(args[1]);
                return;
            }

            if (args != null && args.Length == 2 && string.Equals(args[0], "--verify-print-layout-simulation", StringComparison.OrdinalIgnoreCase))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                PrintLayoutSimulationChecks.Run(args[1]);
                return;
            }

            if (args != null && args.Length == 2 && args[0] == "--verify-antdui-components")
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                AntdComponentChecks.Run(args[1]);
                return;
            }
            if (args != null && args.Length == 1 &&
                (string.Equals(args[0], "--preview-login-ui", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--preview-login-ui-dark", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--preview-login-ui-english", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--preview-login-ui-english-dark", StringComparison.OrdinalIgnoreCase)))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                AppFonts.Initialize();
                string originalTheme = Properties.Setings.Default.ThemeMode;
                try
                {
                    Properties.Setings.Default.ThemeMode = args[0].EndsWith("-dark", StringComparison.OrdinalIgnoreCase)
                        ? "Dark" : "Light";
                    bool english = args[0].IndexOf("-english", StringComparison.OrdinalIgnoreCase) >= 0;
                    using (var preview = new Login(false, true, english)) Application.Run(preview);
                }
                finally { Properties.Setings.Default.ThemeMode = originalTheme; }
                return;
            }
            if (args != null && args.Length == 1 &&
                (string.Equals(args[0], "--preview-purchases-ui", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--preview-purchases-ui-light", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--preview-purchases-ui-dark", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--preview-purchases-ui-english", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--preview-purchases-ui-english-dark", StringComparison.OrdinalIgnoreCase)))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                AppFonts.Initialize();
                string originalThemeMode = Properties.Setings.Default.ThemeMode;
                try
                {
                    Properties.Setings.Default.ThemeMode = args[0].EndsWith("-dark", StringComparison.OrdinalIgnoreCase)
                        ? "Dark" : "Light";
                    bool english = args[0].IndexOf("-english", StringComparison.OrdinalIgnoreCase) >= 0;
                    using (var preview = new PurchasesUiAutomationPreview(english)) Application.Run(preview);
                }
                finally { Properties.Setings.Default.ThemeMode = originalThemeMode; }
                return;
            }
            if (args != null && args.Length == 1 &&
                (string.Equals(args[0], "--preview-dashboard-ui", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--preview-dashboard-ui-english", StringComparison.OrdinalIgnoreCase)))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                AppFonts.Initialize();
                using (var preview = ResponsiveLayoutSelfTests.CreateDashboardUiPreview())
                {
                    preview.StartPosition = FormStartPosition.CenterScreen;
                    preview.Size = new System.Drawing.Size(1366, 768);
                    if (string.Equals(args[0], "--preview-dashboard-ui-english", StringComparison.OrdinalIgnoreCase))
                        preview.ApplyPresentation(true);
                    Application.Run(preview);
                }
                return;
            }
            if (args != null && args.Length == 1 &&
                (string.Equals(args[0], "--preview-home-ui", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--preview-home-ui-english", StringComparison.OrdinalIgnoreCase)))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                AppFonts.Initialize();
                using (var preview = ResponsiveLayoutSelfTests.CreateHomeUiPreview())
                {
                    preview.StartPosition = FormStartPosition.CenterScreen;
                    preview.Size = new System.Drawing.Size(1366, 768);
                    preview.ApplyPresentation(string.Equals(args[0], "--preview-home-ui-english", StringComparison.OrdinalIgnoreCase));
                    Application.Run(preview);
                }
                SessionContext.Clear();
                return;
            }
            if (args != null && args.Length == 1 &&
                (string.Equals(args[0], "--preview-home-workspace-ui", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--preview-home-workspace-ui-english", StringComparison.OrdinalIgnoreCase)))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                AppFonts.Initialize();
                using (var preview = ResponsiveLayoutSelfTests.CreateHomeWorkspaceUiPreview(
                    string.Equals(args[0], "--preview-home-workspace-ui-english", StringComparison.OrdinalIgnoreCase)))
                {
                    preview.StartPosition = FormStartPosition.CenterScreen;
                    preview.Size = new System.Drawing.Size(680, 610);
                    Application.Run(preview);
                }
                return;
            }
            if (args != null && args.Length == 1 &&
                (string.Equals(args[0], "--preview-operational-report-ui", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--preview-operational-report-ui-dark", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--preview-operational-report-ui-english", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--preview-operational-report-ui-english-dark", StringComparison.OrdinalIgnoreCase)))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                AppFonts.Initialize();
                string originalTheme = Properties.Setings.Default.ThemeMode;
                try
                {
                    Properties.Setings.Default.ThemeMode = args[0].EndsWith("-dark", StringComparison.OrdinalIgnoreCase)
                        ? "Dark" : "Light";
                    bool english = args[0].IndexOf("-english", StringComparison.OrdinalIgnoreCase) >= 0;
                    using (var preview = new OperationalReportBuilderForm(true))
                    {
                        preview.ApplyPresentation(english);
                        Application.Run(preview);
                    }
                }
                finally { Properties.Setings.Default.ThemeMode = originalTheme; }
                return;
            }
            if (args != null && args.Length == 1 &&
                (string.Equals(args[0], "--preview-system-monitor-ui", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--preview-system-monitor-ui-english", StringComparison.OrdinalIgnoreCase)))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                AppFonts.Initialize();
                using (var preview = new SystemMonitorForm(true))
                {
                    if (string.Equals(args[0], "--preview-system-monitor-ui-english", StringComparison.OrdinalIgnoreCase))
                        preview.ApplyPresentation(true);
                    Application.Run(preview);
                }
                return;
            }
            if (args != null && args.Length == 1 &&
                (string.Equals(args[0], "--preview-inventory-alerts-ui", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--preview-inventory-alerts-ui-english", StringComparison.OrdinalIgnoreCase)))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                AppFonts.Initialize();
                using (var preview = new InventoryAlertsForm(false))
                {
                    if (string.Equals(args[0], "--preview-inventory-alerts-ui-english", StringComparison.OrdinalIgnoreCase))
                        preview.ApplyPresentation(true);
                    Application.Run(preview);
                }
                return;
            }
            if (args != null && args.Length == 1 &&
                (string.Equals(args[0], "--preview-report-print-options-ui", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--preview-report-print-options-ui-dark", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--preview-report-print-options-ui-english", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--preview-report-print-options-ui-english-dark", StringComparison.OrdinalIgnoreCase)))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                AppFonts.Initialize();
                string originalTheme = Properties.Setings.Default.ThemeMode;
                try
                {
                    Properties.Setings.Default.ThemeMode = args[0].EndsWith("-dark", StringComparison.OrdinalIgnoreCase)
                        ? "Dark" : "Light";
                    bool english = args[0].IndexOf("-english", StringComparison.OrdinalIgnoreCase) >= 0;
                    using (var preview = new ReportPrintOptionsDialog(new PharmacyProfile
                    {
                        NameAr = "صيدلية المهندس", NameEn = "Al-Mohandes Pharmacy",
                        TaxNo = "123456789", CommercialRegNo = "QA-100",
                        Mobile = "700000000", Address = "عدن — Aden", Website = "pharmacy.example"
                    }, true))
                    {
                        preview.ApplyPresentation(english);
                        Application.Run(preview);
                    }
                }
                finally { Properties.Setings.Default.ThemeMode = originalTheme; }
                return;
            }
            if (args != null && args.Length == 1 &&
                (string.Equals(args[0], "--preview-report-columns-ui", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--preview-report-columns-ui-dark", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--preview-report-columns-ui-english", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--preview-report-columns-ui-english-dark", StringComparison.OrdinalIgnoreCase)))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                AppFonts.Initialize();
                string originalTheme = Properties.Setings.Default.ThemeMode;
                try
                {
                    Properties.Setings.Default.ThemeMode = args[0].EndsWith("-dark", StringComparison.OrdinalIgnoreCase)
                        ? "Dark" : "Light";
                    bool english = args[0].IndexOf("-english", StringComparison.OrdinalIgnoreCase) >= 0;
                    var previewColumns = new DataGridViewColumn[]
                    {
                        new DataGridViewTextBoxColumn { Name = "MedicineName", HeaderText = english ? "Medicine" : "الصنف", Visible = true },
                        new DataGridViewTextBoxColumn { Name = "Quantity", HeaderText = english ? "Quantity" : "الكمية", Visible = true },
                        new DataGridViewTextBoxColumn { Name = "Total", HeaderText = english ? "Total" : "الإجمالي", Visible = true },
                        new DataGridViewTextBoxColumn { Name = "Notes", HeaderText = english ? "Notes" : "الملاحظات", Visible = false }
                    };
                    using (var preview = new ReportColumnLayoutDialog(previewColumns))
                    {
                        preview.ApplyPresentation(english);
                        Application.Run(preview);
                    }
                }
                finally { Properties.Setings.Default.ThemeMode = originalTheme; }
                return;
            }
            if (args != null && args.Length == 1 &&
                (string.Equals(args[0], "--preview-report-template-name-ui", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--preview-report-template-name-ui-dark", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--preview-report-template-name-ui-english", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--preview-report-template-name-ui-english-dark", StringComparison.OrdinalIgnoreCase)))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                AppFonts.Initialize();
                string originalTheme = Properties.Setings.Default.ThemeMode;
                try
                {
                    Properties.Setings.Default.ThemeMode = args[0].EndsWith("-dark", StringComparison.OrdinalIgnoreCase)
                        ? "Dark" : "Light";
                    bool english = args[0].IndexOf("-english", StringComparison.OrdinalIgnoreCase) >= 0;
                    using (var preview = new ReportTemplateNameDialog(english ? "Daily sales" : "المبيعات اليومية"))
                    {
                        preview.ApplyPresentation(english);
                        Application.Run(preview);
                    }
                }
                finally { Properties.Setings.Default.ThemeMode = originalTheme; }
                return;
            }
            if (args != null && args.Length == 1 &&
                (string.Equals(args[0], "--preview-excel-import-ui", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--preview-excel-import-errors-ui-english", StringComparison.OrdinalIgnoreCase)))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                AppFonts.Initialize();
                bool errorsEnglish = args[0].IndexOf("-errors-", StringComparison.OrdinalIgnoreCase) >= 0;
                using (var preview = new QaCsvImportForm(true, errorsEnglish ? (bool?)true : null))
                {
                    if (errorsEnglish) preview.ShowValidationPreview();
                    Application.Run(preview);
                }
                return;
            }
            if (args != null && args.Length == 1 &&
                (string.Equals(args[0], "--preview-excel-export-ui", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--preview-excel-export-ui-dark", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--preview-excel-export-ui-english", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--preview-excel-export-ui-english-dark", StringComparison.OrdinalIgnoreCase)))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                AppFonts.Initialize();
                string originalTheme = Properties.Setings.Default.ThemeMode;
                try
                {
                    Properties.Setings.Default.ThemeMode = args[0].EndsWith("-dark", StringComparison.OrdinalIgnoreCase)
                        ? "Dark"
                        : "Light";
                    bool english = args[0].IndexOf("-english", StringComparison.OrdinalIgnoreCase) >= 0;
                    using (var preview = new ExcelDataTransferForm(true))
                    {
                        preview.ApplyPresentation(english);
                        Application.Run(preview);
                    }
                }
                finally { Properties.Setings.Default.ThemeMode = originalTheme; }
                return;
            }
            if (args != null && args.Length == 1 && args[0].StartsWith("--preview-cashboxes-ui", StringComparison.OrdinalIgnoreCase))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                AppFonts.Initialize();
                string originalTheme = Properties.Setings.Default.ThemeMode;
                try
                {
                    Properties.Setings.Default.ThemeMode = args[0].IndexOf("-dark", StringComparison.OrdinalIgnoreCase) >= 0 ? "Dark" : "Light";
                    bool english = args[0].IndexOf("-english", StringComparison.OrdinalIgnoreCase) >= 0;
                    using (var preview = new CashBoxManagementForm(true))
                    {
                        preview.ApplyPresentation(english);
                        Application.Run(preview);
                    }
                }
                finally { Properties.Setings.Default.ThemeMode = originalTheme; }
                return;
            }
            if (args != null && args.Length == 1 && args[0].StartsWith("--preview-invoice-drafts-ui", StringComparison.OrdinalIgnoreCase))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                AppFonts.Initialize();
                string originalTheme = Properties.Setings.Default.ThemeMode;
                try
                {
                    Properties.Setings.Default.ThemeMode = args[0].IndexOf("-dark", StringComparison.OrdinalIgnoreCase) >= 0 ? "Dark" : "Light";
                    bool english = args[0].IndexOf("-english", StringComparison.OrdinalIgnoreCase) >= 0;
                    using (var preview = new InvoiceImportDraftForm(true))
                    {
                        if (args[0].IndexOf("-direct", StringComparison.OrdinalIgnoreCase) >= 0)
                            preview.SetPreviewDirectMode();
                        else preview.SetPreviewDraftMode();
                        preview.ApplyPresentation(english);
                        if (args[0].IndexOf("-source", StringComparison.OrdinalIgnoreCase) >= 0)
                            preview.SetPreviewSourceEntry();
                        Application.Run(preview);
                    }
                }
                finally { Properties.Setings.Default.ThemeMode = originalTheme; }
                return;
            }
            if (args != null && args.Length == 1 && string.Equals(args[0], "--preview-active-ingredients-ui", StringComparison.OrdinalIgnoreCase))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                AppFonts.Initialize();
                using (var preview = new ActiveIngredientManagementForm(true)) Application.Run(preview);
                return;
            }
            if (args != null && args.Length == 1 && (string.Equals(args[0], "--preview-customer-management-ui", StringComparison.OrdinalIgnoreCase) || string.Equals(args[0], "--preview-customer-management-ui-dark", StringComparison.OrdinalIgnoreCase) || string.Equals(args[0], "--preview-customer-management-ui-english", StringComparison.OrdinalIgnoreCase) || string.Equals(args[0], "--preview-customer-management-ui-english-dark", StringComparison.OrdinalIgnoreCase)))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                AppFonts.Initialize();
                string originalTheme = Properties.Setings.Default.ThemeMode;
                try
                {
                    Properties.Setings.Default.ThemeMode = args[0].EndsWith("-dark", StringComparison.OrdinalIgnoreCase) ? "Dark" : "Light";
                    bool english = args[0].IndexOf("-english", StringComparison.OrdinalIgnoreCase) >= 0;
                    using (var preview = new CustomerSupplierManagementForm(previewMode: true, presentationLeftToRight: english)) Application.Run(preview);
                }
                finally { Properties.Setings.Default.ThemeMode = originalTheme; }
                return;
            }
            if (args != null && args.Length == 1 &&
                (string.Equals(args[0], "--preview-medicine-catalog-errors-ui", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--preview-medicine-catalog-errors-ui-dark", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--preview-medicine-catalog-errors-ui-english", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--preview-medicine-catalog-errors-ui-english-dark", StringComparison.OrdinalIgnoreCase)))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                AppFonts.Initialize();
                string originalTheme = Properties.Setings.Default.ThemeMode;
                try
                {
                    Properties.Setings.Default.ThemeMode = args[0].EndsWith("-dark", StringComparison.OrdinalIgnoreCase)
                        ? "Dark" : "Light";
                    bool english = args[0].IndexOf("-english", StringComparison.OrdinalIgnoreCase) >= 0;
                    DataTable sample = MedicineCatalogWorkbook.CreateTemplate();
                    sample.Rows.Add("باراسيتامول 500 mg", "6281234567890", "0", "15",
                        "2026-01-01", "2027-01-01", "شركة الدواء");
                    sample.Rows.Add("", "6281234567891", "10", "15",
                        "2026-01-01", "2027-01-01", "شركة الدواء");
                    sample.Rows.Add("صنف ثالث", "6281234567892", "10", "15",
                        "2027-01-01", "2026-01-01", "شركة الدواء");
                    MedicineCatalogWorkbookPreview result = MedicineCatalogWorkbook.Parse(sample);
                    using (var preview = new MedicineCatalogIssuesForm(result.Issues, english))
                    {
                        preview.ShowInTaskbar = true;
                        preview.StartPosition = FormStartPosition.CenterScreen;
                        Application.Run(preview);
                    }
                }
                finally { Properties.Setings.Default.ThemeMode = originalTheme; }
                return;
            }
            if (args != null && args.Length == 1 &&
                (string.Equals(args[0], "--preview-medicine-catalog-import-ui", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--preview-medicine-catalog-import-ui-dark", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--preview-medicine-catalog-import-ui-english", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--preview-medicine-catalog-import-ui-english-dark", StringComparison.OrdinalIgnoreCase)))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                AppFonts.Initialize();
                string originalTheme = Properties.Setings.Default.ThemeMode;
                try
                {
                    Properties.Setings.Default.ThemeMode = args[0].EndsWith("-dark", StringComparison.OrdinalIgnoreCase)
                        ? "Dark" : "Light";
                    bool english = args[0].IndexOf("-english", StringComparison.OrdinalIgnoreCase) >= 0;
                    using (var preview = new MedicineCatalogImportForm(true))
                    {
                        preview.ApplyPresentation(english);
                        Application.Run(preview);
                    }
                }
                finally { Properties.Setings.Default.ThemeMode = originalTheme; }
                return;
            }
            if (args != null && args.Length == 1 &&
                (string.Equals(args[0], "--preview-inventory-operation-ui", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--preview-inventory-operation-ui-english", StringComparison.OrdinalIgnoreCase)))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                AppFonts.Initialize();
                using (var preview = new InventoryOperationForm(InventoryOperationMode.Receipt, false))
                {
                    var lookup = new DataTable();
                    lookup.Columns.Add("DisplayName", typeof(string));
                    lookup.Columns.Add("MedName", typeof(string));
                    lookup.Rows.Add("باراسيتامول 500 mg — شركة أ", "باراسيتامول 500 mg");
                    lookup.Rows.Add("أموكسيسيلين 250 mg — شركة ب", "أموكسيسيلين 250 mg");
                    var selector = preview.Controls.Find("inventoryOperationMedicine", true).FirstOrDefault() as ComboBox;
                    if (selector == null) throw new InvalidOperationException("Inventory operation preview selector is missing.");
                    InventoryMedicineSelection.Bind(selector, lookup);
                    if (args[0].EndsWith("-english", StringComparison.OrdinalIgnoreCase)) preview.ApplyPresentation(true);
                    Application.Run(preview);
                }
                return;
            }
            if (args != null && args.Length == 1 &&
                (string.Equals(args[0], "--preview-inventory-movement-ui", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--preview-inventory-movement-ui-english", StringComparison.OrdinalIgnoreCase)))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                AppFonts.Initialize();
                using (var preview = new InventoryMovementForm(false))
                {
                    var lookup = new DataTable();
                    lookup.Columns.Add("DisplayName", typeof(string));
                    lookup.Columns.Add("MedName", typeof(string));
                    lookup.Rows.Add("باراسيتامول 500 mg — شركة أ", "باراسيتامول 500 mg");
                    lookup.Rows.Add("أموكسيسيلين 250 mg — شركة ب", "أموكسيسيلين 250 mg");
                    var selector = preview.Controls.Find("inventoryMovementMedicine", true).FirstOrDefault() as ComboBox;
                    if (selector == null) throw new InvalidOperationException("Inventory preview medicine selector is missing.");
                    InventoryMedicineSelection.Bind(selector, lookup);
                    if (args[0].EndsWith("-english", StringComparison.OrdinalIgnoreCase)) preview.ApplyPresentation(true);
                    Application.Run(preview);
                }
                return;
            }
            if (args != null && args.Length == 1 &&
                (args[0] == "--preview-inventory-stock-ui" || args[0] == "--preview-inventory-stock-ui-english" ||
                 args[0] == "--preview-inventory-stock-ui-dark" || args[0] == "--preview-inventory-stock-ui-english-dark" ||
                 args[0] == "--preview-inventory-lots-ui" || args[0] == "--preview-inventory-lots-ui-english" ||
                 args[0] == "--preview-inventory-lots-ui-dark" || args[0] == "--preview-inventory-lots-ui-english-dark"))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                AppFonts.Initialize();
                string originalTheme = Properties.Setings.Default.ThemeMode;
                try
                {
                    Properties.Setings.Default.ThemeMode = args[0].EndsWith("-dark", StringComparison.OrdinalIgnoreCase) ? "Dark" : "Light";
                    using (var preview = new InventoryStockReportForm(args[0].Contains("-lots-"), false))
                    {
                        preview.ApplyPresentation(args[0].Contains("-english"));
                        Application.Run(preview);
                    }
                }
                finally { Properties.Setings.Default.ThemeMode = originalTheme; }
                return;
            }
            if (args != null && args.Length == 1 &&
                (string.Equals(args[0], "--preview-inventory-summary-ui", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--preview-inventory-summary-ui-english", StringComparison.OrdinalIgnoreCase)))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                AppFonts.Initialize();
                using (var preview = new InventoryMovementSummaryForm(false))
                {
                    if (args[0].EndsWith("-english", StringComparison.OrdinalIgnoreCase)) preview.ApplyPresentation(true);
                    Application.Run(preview);
                }
                return;
            }
            if (args != null && args.Length == 1 &&
                (string.Equals(args[0], "--preview-inventory-turnover-ui", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--preview-inventory-turnover-ui-english", StringComparison.OrdinalIgnoreCase)))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                AppFonts.Initialize();
                using (var preview = new InventoryTurnoverForm(false))
                {
                    if (args[0].EndsWith("-english", StringComparison.OrdinalIgnoreCase)) preview.ApplyPresentation(true);
                    Application.Run(preview);
                }
                return;
            }
            if (args != null && args.Length == 1 &&
                (string.Equals(args[0], "--preview-quick-search-ui", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--preview-quick-search-ui-dark", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--preview-quick-search-ui-english", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--preview-quick-search-ui-english-dark", StringComparison.OrdinalIgnoreCase)))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                AppFonts.Initialize();
                string originalThemeMode = Properties.Setings.Default.ThemeMode;
                try
                {
                    bool dark = args[0].EndsWith("-dark", StringComparison.OrdinalIgnoreCase);
                    bool english = args[0].IndexOf("-english", StringComparison.OrdinalIgnoreCase) >= 0;
                    Properties.Setings.Default.ThemeMode = dark ? "Dark" : "Light";
                    using (var paletteRefresh = new Form()) UiTheme.Apply(paletteRefresh);
                    using (var preview = new MedicineQuickSearchForm("باراسيتامول 500 mg", true, english))
                    {
                        preview.ShowInTaskbar = true;
                        preview.StartPosition = FormStartPosition.CenterScreen;
                        preview.Size = new System.Drawing.Size(760, 480);
                        Application.Run(preview);
                    }
                }
                finally { Properties.Setings.Default.ThemeMode = originalThemeMode; }
                return;
            }
            if (args != null && args.Length == 1 &&
                (string.Equals(args[0], "--preview-medicine-ui", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--preview-medicine-ui-dark", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--preview-medicine-ui-english", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--preview-medicine-ui-english-dark", StringComparison.OrdinalIgnoreCase)))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                AppFonts.Initialize();
                string originalThemeMode = Properties.Setings.Default.ThemeMode;
                try
                {
                    bool dark = args[0].EndsWith("-dark", StringComparison.OrdinalIgnoreCase);
                    bool english = args[0].IndexOf("-english", StringComparison.OrdinalIgnoreCase) >= 0;
                    Properties.Setings.Default.ThemeMode = dark ? "Dark" : "Light";
                    using (var paletteRefresh = new Form()) UiTheme.Apply(paletteRefresh);
                    using (var preview = new MedicineUiAutomationPreview(english))
                    {
                        preview.Size = new System.Drawing.Size(960, 640);
                        Application.Run(preview);
                    }
                }
                finally { Properties.Setings.Default.ThemeMode = originalThemeMode; }
                return;
            }
            if (args != null && args.Length == 1 &&
                (string.Equals(args[0], "--preview-agent-ui", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--preview-agent-ui-dark", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--preview-agent-ui-english", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--preview-agent-ui-english-dark", StringComparison.OrdinalIgnoreCase)))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                AppFonts.Initialize();
                string originalTheme = Properties.Setings.Default.ThemeMode;
                try
                {
                    bool dark = args[0].EndsWith("-dark", StringComparison.OrdinalIgnoreCase);
                    bool english = args[0].IndexOf("-english", StringComparison.OrdinalIgnoreCase) >= 0;
                    Properties.Setings.Default.ThemeMode = dark ? "Dark" : "Light";
                    using (var paletteRefresh = new Form()) UiTheme.Apply(paletteRefresh);
                    using (var preview = new AgentUiAutomationPreview(english))
                    {
                        preview.Size = new System.Drawing.Size(960, 640);
                        Application.Run(preview);
                    }
                }
                finally { Properties.Setings.Default.ThemeMode = originalTheme; }
                return;
            }
            if (args != null && args.Length == 1 &&
                (string.Equals(args[0], "--preview-billing-ui", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--preview-billing-ui-dark", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--preview-billing-ui-english", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--preview-billing-ui-english-dark", StringComparison.OrdinalIgnoreCase)))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                AppFonts.Initialize();
                string originalTheme = Properties.Setings.Default.ThemeMode;
                try
                {
                    Properties.Setings.Default.ThemeMode = args[0].EndsWith("-dark", StringComparison.OrdinalIgnoreCase) ? "Dark" : "Light";
                    bool english = args[0].IndexOf("-english", StringComparison.OrdinalIgnoreCase) >= 0;
                    using (var preview = new BillingUiAutomationPreview(english))
                    {
                        preview.StartPosition = FormStartPosition.CenterScreen;
                        preview.Size = new System.Drawing.Size(960, 640);
                        Application.Run(preview);
                    }
                }
                finally { Properties.Setings.Default.ThemeMode = originalTheme; }
                return;
            }
            if (args != null && args.Length == 1 &&
                (string.Equals(args[0], "--preview-saudi-settings-ui", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--preview-saudi-settings-ui-dark", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--preview-saudi-settings-ui-english", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--preview-saudi-settings-ui-english-dark", StringComparison.OrdinalIgnoreCase)))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                AppFonts.Initialize();
                string originalTheme = Properties.Setings.Default.ThemeMode;
                try
                {
                    Properties.Setings.Default.ThemeMode = args[0].EndsWith("-dark", StringComparison.OrdinalIgnoreCase) ? "Dark" : "Light";
                    bool english = args[0].IndexOf("-english", StringComparison.OrdinalIgnoreCase) >= 0;
                    using (var preview = new SaudiCommercialSettingsForm(true, english)) Application.Run(preview);
                }
                finally { Properties.Setings.Default.ThemeMode = originalTheme; }
                return;
            }
            if (args != null && args.Length == 1 &&
                (string.Equals(args[0], "--preview-pharmacy-profile-ui", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--preview-pharmacy-profile-ui-dark", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--preview-pharmacy-profile-ui-english", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--preview-pharmacy-profile-ui-english-dark", StringComparison.OrdinalIgnoreCase)))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                AppFonts.Initialize();
                string originalTheme = Properties.Setings.Default.ThemeMode;
                try
                {
                    Properties.Setings.Default.ThemeMode = args[0].EndsWith("-dark", StringComparison.OrdinalIgnoreCase) ? "Dark" : "Light";
                    bool english = args[0].IndexOf("-english", StringComparison.OrdinalIgnoreCase) >= 0;
                    using (var preview = new PharmacyCompanyForm(true, english)) Application.Run(preview);
                }
                finally { Properties.Setings.Default.ThemeMode = originalTheme; }
                return;
            }
            if (args != null && args.Length == 1 &&
                (string.Equals(args[0], "--preview-bootstrap-manager-ui", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--preview-bootstrap-manager-ui-dark", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--preview-bootstrap-manager-ui-english", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--preview-bootstrap-manager-ui-english-dark", StringComparison.OrdinalIgnoreCase)))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                AppFonts.Initialize();
                string originalTheme = Properties.Setings.Default.ThemeMode;
                try
                {
                    Properties.Setings.Default.ThemeMode = args[0].EndsWith("-dark", StringComparison.OrdinalIgnoreCase) ? "Dark" : "Light";
                    bool english = args[0].IndexOf("-english", StringComparison.OrdinalIgnoreCase) >= 0;
                    using (var preview = new BootstrapManagerForm(true, english)) Application.Run(preview);
                }
                finally { Properties.Setings.Default.ThemeMode = originalTheme; }
                return;
            }
            if (args != null && args.Length == 1 &&
                (string.Equals(args[0], "--preview-commercial-return-ui", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--preview-commercial-return-ui-dark", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--preview-commercial-return-ui-english", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--preview-commercial-return-ui-english-dark", StringComparison.OrdinalIgnoreCase)))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                AppFonts.Initialize();
                string originalTheme = Properties.Setings.Default.ThemeMode;
                try
                {
                    Properties.Setings.Default.ThemeMode = args[0].EndsWith("-dark", StringComparison.OrdinalIgnoreCase) ? "Dark" : "Light";
                    bool english = args[0].IndexOf("-english", StringComparison.OrdinalIgnoreCase) >= 0;
                    using (var preview = new CommercialSalesReturnForm(Guid.Empty, true, english)) Application.Run(preview);
                }
                finally { Properties.Setings.Default.ThemeMode = originalTheme; }
                return;
            }
            if (args != null && args.Length == 1 &&
                (string.Equals(args[0], "--preview-return-history-ui", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--preview-return-history-ui-dark", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--preview-return-history-ui-english", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--preview-return-history-ui-english-dark", StringComparison.OrdinalIgnoreCase)))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                AppFonts.Initialize();
                string originalTheme = Properties.Setings.Default.ThemeMode;
                try
                {
                    Properties.Setings.Default.ThemeMode = args[0].EndsWith("-dark", StringComparison.OrdinalIgnoreCase) ? "Dark" : "Light";
                    bool english = args[0].IndexOf("-english", StringComparison.OrdinalIgnoreCase) >= 0;
                    using (var preview = new CommercialSalesReturnHistoryForm(Guid.Empty, true, english)) Application.Run(preview);
                }
                finally { Properties.Setings.Default.ThemeMode = originalTheme; }
                return;
            }
            if (args != null && args.Length == 1 &&
                (string.Equals(args[0], "--preview-quarantine-resolution-ui", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--preview-quarantine-resolution-ui-dark", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--preview-quarantine-resolution-ui-english", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--preview-quarantine-resolution-ui-english-dark", StringComparison.OrdinalIgnoreCase)))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                AppFonts.Initialize();
                string originalTheme = Properties.Setings.Default.ThemeMode;
                try
                {
                    Properties.Setings.Default.ThemeMode = args[0].EndsWith("-dark", StringComparison.OrdinalIgnoreCase) ? "Dark" : "Light";
                    bool english = args[0].IndexOf("-english", StringComparison.OrdinalIgnoreCase) >= 0;
                    using (var preview = new QuarantineResolutionForm(true, english)) Application.Run(preview);
                }
                finally { Properties.Setings.Default.ThemeMode = originalTheme; }
                return;
            }
            if (args != null && args.Length == 1 && string.Equals(args[0], "--preview-permissions-ui", StringComparison.OrdinalIgnoreCase))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                AppFonts.Initialize();
                using (var preview = new EmployeeOperationPermissionsForm("preview", "موظف تجريبي", true))
                    Application.Run(preview);
                return;
            }
            if (args != null && args.Length == 1 &&
                (string.Equals(args[0], "--preview-manufacturer-ui", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--preview-manufacturer-ui-dark", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--preview-manufacturer-ui-english", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--preview-manufacturer-ui-english-dark", StringComparison.OrdinalIgnoreCase)))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                AppFonts.Initialize();
                string originalTheme = Properties.Setings.Default.ThemeMode;
                try
                {
                    Properties.Setings.Default.ThemeMode = args[0].EndsWith("-dark", StringComparison.OrdinalIgnoreCase) ? "Dark" : "Light";
                    bool english = args[0].IndexOf("-english", StringComparison.OrdinalIgnoreCase) >= 0;
                    using (var preview = new ManufacturerUiAutomationPreview(english))
                    {
                        preview.Size = new System.Drawing.Size(960, 640);
                        Application.Run(preview);
                    }
                }
                finally { Properties.Setings.Default.ThemeMode = originalTheme; }
                return;
            }
            if (args != null && args.Length == 1 && string.Equals(args[0], "--preview-quick-journal-ui", StringComparison.OrdinalIgnoreCase))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                AppFonts.Initialize();
                using (var preview = new JournalEntriesQuickForm(true))
                {
                    preview.Size = new System.Drawing.Size(760, 640);
                    Application.Run(preview);
                }
                return;
            }
            if (args != null && args.Length == 1 &&
                (string.Equals(args[0], "--preview-accounting-period-ui", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--preview-accounting-period-ui-dark", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--preview-accounting-period-ui-english", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--preview-accounting-period-ui-english-dark", StringComparison.OrdinalIgnoreCase)))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                AppFonts.Initialize();
                string originalTheme = Properties.Setings.Default.ThemeMode;
                try
                {
                    Properties.Setings.Default.ThemeMode = args[0].EndsWith("-dark", StringComparison.OrdinalIgnoreCase)
                        ? "Dark" : "Light";
                    bool english = args[0].IndexOf("-english", StringComparison.OrdinalIgnoreCase) >= 0;
                    using (var preview = new AccountingPeriodManagementForm(true))
                    {
                        preview.ApplyPresentation(english);
                        preview.Size = new System.Drawing.Size(760, 560);
                        Application.Run(preview);
                    }
                }
                finally { Properties.Setings.Default.ThemeMode = originalTheme; }
                return;
            }
            if (args != null && args.Length == 1 &&
                (string.Equals(args[0], "--preview-period-readiness-ui", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--preview-period-readiness-ui-dark", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--preview-period-readiness-ui-english", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--preview-period-readiness-ui-english-dark", StringComparison.OrdinalIgnoreCase)))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                AppFonts.Initialize();
                string originalTheme = Properties.Setings.Default.ThemeMode;
                try
                {
                    Properties.Setings.Default.ThemeMode = args[0].EndsWith("-dark", StringComparison.OrdinalIgnoreCase)
                        ? "Dark" : "Light";
                    bool english = args[0].IndexOf("-english", StringComparison.OrdinalIgnoreCase) >= 0;
                    using (var preview = new PeriodCloseReadinessForm(true))
                    {
                        preview.ApplyPresentation(english);
                        preview.Size = new System.Drawing.Size(760, 560);
                        Application.Run(preview);
                    }
                }
                finally { Properties.Setings.Default.ThemeMode = originalTheme; }
                return;
            }
            if (args != null && args.Length == 1 &&
                (string.Equals(args[0], "--preview-accounting-integrity-ui", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--preview-accounting-integrity-ui-dark", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--preview-accounting-integrity-ui-english", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--preview-accounting-integrity-ui-english-dark", StringComparison.OrdinalIgnoreCase)))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                AppFonts.Initialize();
                string originalTheme = Properties.Setings.Default.ThemeMode;
                try
                {
                    Properties.Setings.Default.ThemeMode = args[0].EndsWith("-dark", StringComparison.OrdinalIgnoreCase)
                        ? "Dark" : "Light";
                    bool english = args[0].IndexOf("-english", StringComparison.OrdinalIgnoreCase) >= 0;
                    using (var preview = new AccountingIntegrityForm(true))
                    {
                        preview.ApplyPresentation(english);
                        preview.Size = new System.Drawing.Size(940, 620);
                        Application.Run(preview);
                    }
                }
                finally { Properties.Setings.Default.ThemeMode = originalTheme; }
                return;
            }
            if (args != null && args.Length == 1 &&
                (string.Equals(args[0], "--preview-inventory-integrity-ui", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--preview-inventory-integrity-ui-dark", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--preview-inventory-integrity-ui-english", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--preview-inventory-integrity-ui-english-dark", StringComparison.OrdinalIgnoreCase)))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                AppFonts.Initialize();
                string originalTheme = Properties.Setings.Default.ThemeMode;
                try
                {
                    Properties.Setings.Default.ThemeMode = args[0].EndsWith("-dark", StringComparison.OrdinalIgnoreCase)
                        ? "Dark" : "Light";
                    bool english = args[0].IndexOf("-english", StringComparison.OrdinalIgnoreCase) >= 0;
                    using (var preview = new InventoryIntegrityForm(true))
                    {
                        preview.ApplyPresentation(english);
                        preview.Size = new System.Drawing.Size(940, 620);
                        Application.Run(preview);
                    }
                }
                finally { Properties.Setings.Default.ThemeMode = originalTheme; }
                return;
            }
            if (args != null && args.Length == 1 &&
                (string.Equals(args[0], "--preview-journal-ui", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--preview-journal-ui-dark", StringComparison.OrdinalIgnoreCase)))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                AppFonts.Initialize();
                string originalTheme = Properties.Setings.Default.ThemeMode;
                try
                {
                    if (string.Equals(args[0], "--preview-journal-ui-dark", StringComparison.OrdinalIgnoreCase))
                        Properties.Setings.Default.ThemeMode = "Dark";
                    using (var preview = new JournalEntriesForm("القيود اليومية", "Manual", true))
                    {
                        preview.Size = new System.Drawing.Size(960, 640);
                        Application.Run(preview);
                    }
                }
                finally { Properties.Setings.Default.ThemeMode = originalTheme; }
                return;
            }
            if (args != null && args.Length == 1 &&
                (string.Equals(args[0], "--preview-voucher-ui", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--preview-voucher-ui-dark", StringComparison.OrdinalIgnoreCase)))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                AppFonts.Initialize();
                string originalTheme = Properties.Setings.Default.ThemeMode;
                try
                {
                    Properties.Setings.Default.ThemeMode = string.Equals(args[0], "--preview-voucher-ui-dark", StringComparison.OrdinalIgnoreCase)
                        ? "Dark" : "Light";
                    SessionContext.Start("Preview", "Manager");
                    using (var preview = new ReceiptVoucherForm("ReceiptExternal", true))
                    {
                        preview.Size = new System.Drawing.Size(960, 640);
                        Application.Run(preview);
                    }
                }
                finally { Properties.Setings.Default.ThemeMode = originalTheme; }
                return;
            }
            if (args != null && args.Length == 1 &&
                (string.Equals(args[0], "--preview-database-settings-ui", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--preview-database-settings-ui-dark", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--preview-database-settings-ui-english", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--preview-database-settings-ui-english-dark", StringComparison.OrdinalIgnoreCase)))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                AppFonts.Initialize();
                string originalTheme = Properties.Setings.Default.ThemeMode;
                try
                {
                    Properties.Setings.Default.ThemeMode = args[0].EndsWith("-dark", StringComparison.OrdinalIgnoreCase)
                        ? "Dark" : "Light";
                    bool english = args[0].IndexOf("-english", StringComparison.OrdinalIgnoreCase) >= 0;
                    using (var preview = new DatabaseSetingsForm(true, english)) Application.Run(preview);
                }
                finally { Properties.Setings.Default.ThemeMode = originalTheme; }
                return;
            }
            if (args != null && args.Length == 1 &&
                (args[0] == "--preview-currency-settings" || args[0] == "--preview-currency-settings-dark" ||
                 args[0] == "--preview-currency-settings-english" || args[0] == "--preview-currency-settings-english-dark"))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                AppFonts.Initialize();
                string originalTheme = Properties.Setings.Default.ThemeMode;
                try
                {
                    Properties.Setings.Default.ThemeMode = args[0].EndsWith("-dark", StringComparison.OrdinalIgnoreCase)
                        ? "Dark" : "Light";
                    bool english = args[0].IndexOf("-english", StringComparison.OrdinalIgnoreCase) >= 0;
                    using (var preview = new CurrencySettingsForm(true, english)) Application.Run(preview);
                }
                finally { Properties.Setings.Default.ThemeMode = originalTheme; }
                return;
            }
            if (args != null && args.Length == 1 &&
                (args[0] == "--preview-device-settings" || args[0] == "--preview-device-settings-dark" ||
                 args[0] == "--preview-device-settings-english" || args[0] == "--preview-device-settings-english-dark"))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                AppFonts.Initialize();
                string originalTheme = Properties.Setings.Default.ThemeMode;
                try
                {
                    Properties.Setings.Default.ThemeMode = args[0].EndsWith("-dark", StringComparison.OrdinalIgnoreCase)
                        ? "Dark" : "Light";
                    bool english = args[0].IndexOf("-english", StringComparison.OrdinalIgnoreCase) >= 0;
                    using (var preview = new DeviceSettingsForm(true, english)) Application.Run(preview);
                }
                finally { Properties.Setings.Default.ThemeMode = originalTheme; }
                return;
            }
            if (args != null && args.Length == 1 &&
                (args[0] == "--preview-language-settings" || args[0] == "--preview-language-settings-dark" ||
                 args[0] == "--preview-language-settings-english" || args[0] == "--preview-language-settings-english-dark"))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                AppFonts.Initialize();
                string originalTheme = Properties.Setings.Default.ThemeMode;
                try
                {
                    Properties.Setings.Default.ThemeMode = args[0].EndsWith("-dark", StringComparison.OrdinalIgnoreCase)
                        ? "Dark" : "Light";
                    bool english = args[0].IndexOf("-english", StringComparison.OrdinalIgnoreCase) >= 0;
                    using (var preview = new LanguageSettingsForm(true, english)) Application.Run(preview);
                }
                finally { Properties.Setings.Default.ThemeMode = originalTheme; }
                return;
            }
            if (args != null && args.Length == 1 &&
                (args[0] == "--preview-tax-settings" || args[0] == "--preview-tax-settings-dark" ||
                 args[0] == "--preview-tax-settings-english" || args[0] == "--preview-tax-settings-english-dark"))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                AppFonts.Initialize();
                string originalTheme = Properties.Setings.Default.ThemeMode;
                try
                {
                    Properties.Setings.Default.ThemeMode = args[0].EndsWith("-dark", StringComparison.OrdinalIgnoreCase)
                        ? "Dark" : "Light";
                    bool english = args[0].IndexOf("-english", StringComparison.OrdinalIgnoreCase) >= 0;
                    using (var preview = new TaxSettingsForm(true, english)) Application.Run(preview);
                }
                finally { Properties.Setings.Default.ThemeMode = originalTheme; }
                return;
            }
            if (args != null && args.Length == 1 && (args[0] == "--preview-antdui-components" || args[0] == "--preview-antdui-settings" || args[0] == "--preview-antdui-settings-english"))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                AppFonts.Initialize();
                using (Form preview = args[0] == "--preview-antdui-settings"
                    ? (Form)new SettingsForm(true, true, false)
                    : args[0] == "--preview-antdui-settings-english"
                        ? (Form)new SettingsForm(true, true, true)
                    : new AntdComponentsForm())
                    Application.Run(preview);
                return;
            }
            if (args != null && args.Length == 2 && string.Equals(args[0], "--set-ui-font", StringComparison.OrdinalIgnoreCase))
            {
                UiTheme.SaveFontFamily(args[1]);
                Properties.Setings.Default.Reload();
                Console.WriteLine("Saved UI font: " + UiTheme.LoadFontFamily());
                return;
            }
            if (args != null && args.Length == 2 && string.Equals(args[0], "--verify-inventory-report-previews", StringComparison.OrdinalIgnoreCase))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                ResponsiveLayoutSelfTests.SaveInventoryReportPreviews(args[1]);
                return;
            }
            if (args != null && args.Length == 2 && string.Equals(args[0], "--verify-layout-previews", StringComparison.OrdinalIgnoreCase))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                ResponsiveLayoutSelfTests.SaveLayoutPreviews(args[1]);
                return;
            }
            if (args != null && args.Length == 2 && string.Equals(args[0], "--verify-active-ingredient-previews", StringComparison.OrdinalIgnoreCase))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                ResponsiveLayoutSelfTests.SaveActiveIngredientPreviews(args[1]);
                return;
            }
            if (args != null && args.Length == 2 && string.Equals(args[0], "--verify-manufacturer-previews", StringComparison.OrdinalIgnoreCase))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                ResponsiveLayoutSelfTests.SaveManufacturerPreviews(args[1]);
                return;
            }
            if (args != null && args.Length == 2 && string.Equals(args[0], "--verify-medicine-previews", StringComparison.OrdinalIgnoreCase))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                ResponsiveLayoutSelfTests.SaveMedicinePreviews(args[1]);
                return;
            }
            if (args != null && args.Length == 2 && string.Equals(args[0], "--verify-billing-read-previews", StringComparison.OrdinalIgnoreCase))
            {
                Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
                ResponsiveLayoutSelfTests.SaveBillingReadPreviews(args[1]); return;
            }
            if (args != null && args.Length == 2 && string.Equals(args[0], "--verify-billing-barcode-previews", StringComparison.OrdinalIgnoreCase))
            {
                Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
                ResponsiveLayoutSelfTests.SaveBillingReadPreviews(args[1],true); return;
            }
            if (args != null && args.Length == 3 && string.Equals(args[0], "--verify-qa-currency-guard-readonly", StringComparison.OrdinalIgnoreCase))
            {
                PermissionSelfTests.VerifyCurrencyGuardQaReadOnly(args[1],args[2]); return;
            }
            if (args != null && args.Length == 1 && string.Equals(args[0], "--verify-quick-search-authorization", StringComparison.OrdinalIgnoreCase))
            {
                Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
                ResponsiveLayoutSelfTests.VerifyQuickSearchAuthorization(); return;
            }
            if (args != null && args.Length == 1 && string.Equals(args[0], "--verify-billing-currency-snapshot", StringComparison.OrdinalIgnoreCase))
            {
                Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
                ResponsiveLayoutSelfTests.VerifyBillingCurrencySnapshot(); return;
            }
            if(args!=null && args.Length==1 && args[0]=="--verify-invoice-alert-status")
            {
                Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
                InvoiceInventoryAlertStatusSelfTests.Run(); return;
            }
            if (args != null && args.Length == 1 && string.Equals(args[0], "--verify-purchase-barcode-async", StringComparison.OrdinalIgnoreCase))
            {
                Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
                PurchaseBarcodeAsyncSelfTests.Run(); return;
            }
            if (args != null && args.Length == 1 && string.Equals(args[0], "--verify-billing-barcode-async", StringComparison.OrdinalIgnoreCase))
            {
                Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
                ResponsiveLayoutSelfTests.VerifyBillingBarcodeAsync(); return;
            }
            if (args != null && args.Length == 1 && string.Equals(args[0], "--verify-billing-medicine-reads", StringComparison.OrdinalIgnoreCase))
            {
                Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
                ResponsiveLayoutSelfTests.VerifyBillingMedicineReads(); return;
            }
            if (args != null && args.Length == 1 && string.Equals(args[0], "--verify-medicine-async", StringComparison.OrdinalIgnoreCase))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                ResponsiveLayoutSelfTests.VerifyMedicineAsyncOnly();
                return;
            }
            if (args != null && args.Length == 1 && string.Equals(args[0], "--verify-manufacturer-async", StringComparison.OrdinalIgnoreCase))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                ResponsiveLayoutSelfTests.VerifyManufacturerAsyncOnly();
                return;
            }
            if (args != null && args.Length == 1 && string.Equals(args[0], "--verify-inventory-integrity-notification", StringComparison.OrdinalIgnoreCase))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                ResponsiveLayoutSelfTests.VerifyInventoryIntegrityNotificationOnly();
                return;
            }
            if (args != null && args.Length == 1 && string.Equals(args[0], "--verify-inventory-alert-notification", StringComparison.OrdinalIgnoreCase))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                ResponsiveLayoutSelfTests.VerifyInventoryAlertNotificationOnly();
                return;
            }
            if (args != null && args.Length == 1 && string.Equals(args[0], "--verify-inventory-report-notification", StringComparison.OrdinalIgnoreCase))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                ResponsiveLayoutSelfTests.VerifyInventoryReportNotificationOnly();
                return;
            }
            if (args != null && args.Length == 1 && string.Equals(args[0], "--verify-inventory-movement-notification", StringComparison.OrdinalIgnoreCase))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                ResponsiveLayoutSelfTests.VerifyInventoryMovementNotificationOnly();
                return;
            }
            if(args!=null&&args.Length==1&&string.Equals(args[0],"--verify-accounting-reports-async",StringComparison.OrdinalIgnoreCase))
            {
                Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);AccountingReportsAsyncSelfTests.Run();return;
            }
            if(args!=null&&args.Length==1&&string.Equals(args[0],"--verify-accounting-reports-session",StringComparison.OrdinalIgnoreCase))
            {
                AccountingReportsSessionSelfTests.Run();return;
            }
            if(args!=null&&args.Length==1&&string.Equals(args[0],"--verify-general-ledger-async",StringComparison.OrdinalIgnoreCase))
            {
                Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
                GeneralLedgerReadSelfTests.Run();return;
            }
            if (args != null && args.Length == 1 && string.Equals(args[0], "--verify-inventory-operation-frames", StringComparison.OrdinalIgnoreCase))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                ResponsiveLayoutSelfTests.VerifyInventoryOperationFramesOnly();
                return;
            }
            if (args != null && args.Length == 1 && string.Equals(args[0], "--verify-inventory-operation-notification", StringComparison.OrdinalIgnoreCase))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                ResponsiveLayoutSelfTests.VerifyInventoryOperationNotificationOnly();
                return;
            }
            if (args != null && args.Length == 1 && string.Equals(args[0], "--verify-inventory-stock-async", StringComparison.OrdinalIgnoreCase))
            {
                Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);AppFonts.Initialize();
                InventoryStockReadSelfTests.Run();return;
            }
            if(args!=null&&args.Length==3&&string.Equals(args[0],"--verify-qa-operational-report-async",StringComparison.OrdinalIgnoreCase))
            {OperationalReportAsyncSelfTests.RunQa(args[1],args[2]);return;}
            if(args!=null&&args.Length==1&&string.Equals(args[0],"--verify-customer-write-recovery",StringComparison.OrdinalIgnoreCase))
            {
                Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);AppFonts.Initialize();
                string directory=System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"owned-customer-write-"+Guid.NewGuid().ToString("N"));
                if(CustomerMasterWriteSessionSelfTests.Run()!=0)throw new InvalidOperationException("Customer write session tests failed.");
                if(CustomerMasterPendingStoreSelfTests.Run(directory+"-store")!=0)throw new InvalidOperationException("Customer pending store tests failed.");
                Console.WriteLine(CustomerMasterWriteUiProbe.Run(directory+"-ui"));return;
            }
            if(args!=null&&args.Length==1&&string.Equals(args[0],"--verify-billing-customer-reads",StringComparison.OrdinalIgnoreCase))
            {Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);AppFonts.Initialize();BillingCustomerReadSelfTests.Run();return;}
            if(args!=null&&args.Length==1&&string.Equals(args[0],"--verify-commercial-return-reads",StringComparison.OrdinalIgnoreCase))
            {Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);AppFonts.Initialize();CommercialReturnReadSelfTests.Run();return;}
            if(args!=null&&args.Length==3&&string.Equals(args[0],"--verify-qa-commercial-return-reads",StringComparison.OrdinalIgnoreCase))
            {CommercialReturnReadSelfTests.RunQa(args[1],args[2]);return;}
            if(args!=null&&args.Length==1&&string.Equals(args[0],"--verify-operational-report-async",StringComparison.OrdinalIgnoreCase))
            {Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);AppFonts.Initialize();OperationalReportAsyncSelfTests.Run();return;}
            if (args != null && args.Length == 1 && string.Equals(args[0], "--verify-stock-session-notification", StringComparison.OrdinalIgnoreCase))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                InventoryStockReadSelfTests.RunNotification();
                return;
            }
            if (args != null && args.Length == 1 && string.Equals(args[0], "--verify-customer-recovery", StringComparison.OrdinalIgnoreCase))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                ResponsiveLayoutSelfTests.VerifyCustomerRecoveryOnly();
                return;
            }
            if (args != null && args.Length == 2 && string.Equals(args[0], "--verify-customer-previews", StringComparison.OrdinalIgnoreCase))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                ResponsiveLayoutSelfTests.SaveCustomerPreviews(args[1]);
                return;
            }
            if (args != null && args.Length == 2 && string.Equals(args[0], "--verify-integrity-previews", StringComparison.OrdinalIgnoreCase))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                ResponsiveLayoutSelfTests.SaveIntegrityPreviews(args[1]);
                return;
            }
            if (args != null && args.Length == 2 && string.Equals(args[0], "--verify-monitor-previews", StringComparison.OrdinalIgnoreCase))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                ResponsiveLayoutSelfTests.SaveSystemMonitorPreviews(args[1]);
                return;
            }
            if (args != null && args.Length == 2 && string.Equals(args[0], "--verify-inventory-alerts-previews", StringComparison.OrdinalIgnoreCase))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                ResponsiveLayoutSelfTests.SaveInventoryAlertsPreviews(args[1]);
                return;
            }
            if (args != null && args.Length == 2 && string.Equals(args[0], "--verify-database-settings-previews", StringComparison.OrdinalIgnoreCase))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                ResponsiveLayoutSelfTests.SaveDatabaseSettingsPreviews(args[1]);
                return;
            }
            if (args != null && args.Length == 2 && string.Equals(args[0], "--verify-period-previews", StringComparison.OrdinalIgnoreCase))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                ResponsiveLayoutSelfTests.SaveAccountingPeriodPreviews(args[1]);
                return;
            }
            if (args != null && args.Length == 2 && string.Equals(args[0], "--verify-quarantine-previews", StringComparison.OrdinalIgnoreCase))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                ResponsiveLayoutSelfTests.SaveQuarantinePreviews(args[1]);
                return;
            }
            if (args != null && args.Length == 2 && string.Equals(args[0], "--verify-return-history-previews", StringComparison.OrdinalIgnoreCase))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                ResponsiveLayoutSelfTests.SaveReturnHistoryPreviews(args[1]);
                return;
            }
            if (args != null && args.Length == 2 && string.Equals(args[0], "--verify-commercial-return-previews", StringComparison.OrdinalIgnoreCase))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                ResponsiveLayoutSelfTests.SaveCommercialReturnPreviews(args[1]);
                return;
            }
            if (args != null && args.Length == 2 && string.Equals(args[0], "--verify-quick-search-previews", StringComparison.OrdinalIgnoreCase))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                ResponsiveLayoutSelfTests.SaveQuickSearchPreviews(args[1]);
                return;
            }
            if (args != null && args.Length == 2 && string.Equals(args[0], "--verify-excel-transfer-previews", StringComparison.OrdinalIgnoreCase))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                ResponsiveLayoutSelfTests.SaveExcelTransferPreviews(args[1]);
                return;
            }
            if (args != null && args.Length == 2 && string.Equals(args[0], "--verify-excel-import-previews", StringComparison.OrdinalIgnoreCase))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                ResponsiveLayoutSelfTests.SaveExcelImportPreviews(args[1]);
                return;
            }
            if (args != null && args.Length == 2 && string.Equals(args[0], "--verify-medicine-catalog-import-previews", StringComparison.OrdinalIgnoreCase))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                ResponsiveLayoutSelfTests.SaveMedicineCatalogImportPreviews(args[1]);
                return;
            }
            if (args != null && args.Length == 2 && string.Equals(args[0], "--verify-operational-report-previews", StringComparison.OrdinalIgnoreCase))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                ResponsiveLayoutSelfTests.SaveOperationalReportPreviews(args[1]);
                return;
            }
            if (args != null && args.Length == 2 && string.Equals(args[0], "--verify-report-print-options-previews", StringComparison.OrdinalIgnoreCase))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                ResponsiveLayoutSelfTests.SaveReportPrintOptionsPreviews(args[1]);
                return;
            }

            if (args != null && args.Length == 1 && string.Equals(args[0], "--verify-commercial-domain", StringComparison.OrdinalIgnoreCase))
            {
                CommercialSelfTests.RunAll();
                return;
            }
            if (args != null && args.Length == 1 && string.Equals(args[0], "--verify-excel-workbook", StringComparison.OrdinalIgnoreCase))
            {
                ExcelWorkbookSelfTests.Run();
                return;
            }
            if (args != null && args.Length == 2 && string.Equals(args[0], "--verify-excel-workbook-preview", StringComparison.OrdinalIgnoreCase))
            {
                ExcelWorkbookSelfTests.WriteFixture(args[1]);
                return;
            }

            if (args != null && args.Length == 1 && string.Equals(args[0], "--verify-inventory-report-output-states", StringComparison.OrdinalIgnoreCase))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                try
                {
                    ResponsiveLayoutSelfTests.VerifyInventoryReportOutputStates();
                    return;
                }
                catch (Exception error)
                {
                    Console.Error.WriteLine(error.ToString());
                    Environment.ExitCode = 1;
                    return;
                }
            }
            if (args != null && args.Length == 1 && string.Equals(args[0], "--verify-chart-session", StringComparison.OrdinalIgnoreCase))
            {
                try { ResponsiveLayoutSelfTests.VerifyChartSessionOnly(); Console.WriteLine("PASS chart retained session/context and cancellation contracts; no SQL writes or native input."); Environment.ExitCode=0; }
                catch(Exception error) { Console.Error.WriteLine("FAIL chart session: "+AppDiagnostics.Log("ChartSessionSelfTest",error)); Environment.ExitCode=1; }
                return;
            }

            if (args != null && args.Length == 1 && string.Equals(args[0], "--verify-quick-journal-session", StringComparison.OrdinalIgnoreCase))
            {
                try { ResponsiveLayoutSelfTests.VerifyQuickJournalSessionOnly(); Console.WriteLine("PASS quick journal session/cancellation contracts; no native input."); Environment.ExitCode=0; }
                catch(Exception error) { Console.Error.WriteLine("FAIL quick journal session: "+AppDiagnostics.Log("QuickJournalSessionSelfTest",error)); Environment.ExitCode=1; }
                return;
            }

            if (args != null && args.Length == 1 && string.Equals(args[0], "--verify-opening-preference", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    ResponsiveLayoutSelfTests.VerifyOpeningPreferenceOnly();
                    Console.WriteLine("PASS opening preference: Save/Close actions and accessible feedback in both themes; centered/maximized choice survives reload and process restart; original setting restored.");
                }
                catch (Exception exception)
                {
                    Console.Error.WriteLine("Opening preference verification failed: " + exception);
                    Environment.ExitCode = 1;
                }
                return;
            }

            if (args != null && args.Length == 2 && string.Equals(args[0], "--verify-language-settings-save-failure", StringComparison.OrdinalIgnoreCase))
            {
                LanguageSettingsStorageSelfTests.VerifyFileLock(args[1]);
                return;
            }
            if (args != null && args.Length == 2 && string.Equals(args[0], "--verify-device-settings-save-failure", StringComparison.OrdinalIgnoreCase))
            {
                DeviceSettingsStorageSelfTests.VerifyFileLock(args[1]);
                return;
            }
            if (args != null && args.Length == 2 && string.Equals(args[0], "--verify-accessibility-keyboard", StringComparison.OrdinalIgnoreCase))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                AccessibilitySettingsStorageSelfTests.VerifyKeyboard(args[1]);
                return;
            }
            if (args != null && args.Length == 2 && string.Equals(args[0], "--verify-accessibility-save-failure", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    AccessibilitySettingsStorageSelfTests.VerifySaveFileLock(args[1]);
                }
                catch (Exception exception)
                {
                    Console.Error.WriteLine("Accessibility settings storage verification failed: " + exception);
                    Environment.ExitCode = 1;
                }
                return;
            }

            if (args != null && args.Length == 2 && string.Equals(args[0], "--verify-opening-preference-state", StringComparison.OrdinalIgnoreCase))
            {
                bool expected;
                if (!bool.TryParse(args[1], out expected))
                {
                    Console.Error.WriteLine("Opening preference restart verifier requires true or false.");
                    Environment.ExitCode = 2;
                    return;
                }

                bool actual = ScreenModeSettings.LoadOpeningPreference();
                if (actual != expected)
                {
                    Console.Error.WriteLine("Opening preference restart mismatch. Expected={0}; Actual={1}.", expected, actual);
                    Environment.ExitCode = 1;
                    return;
                }

                Console.WriteLine("PASS opening preference survived process restart. Value={0}.", actual);
                return;
            }

            if (args != null && args.Length == 1 && string.Equals(args[0], "--verify-ledger-date-escape", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    ResponsiveLayoutSelfTests.VerifyLedgerDateEscapeOnly();
                    Console.WriteLine("PASS ledger date Escape: closes the open picker before the report screen.");
                }
                catch (Exception exception)
                {
                    Console.Error.WriteLine("Ledger date Escape verification failed: " + exception);
                    Environment.ExitCode = 1;
                }
                return;
            }

            if (args != null && args.Length == 1 && string.Equals(args[0], "--verify-pdf-output-presentation", StringComparison.OrdinalIgnoreCase))
            {
                PdfCompletionSelfTests.VerifyPresentation();
                return;
            }
            if (args != null && args.Length == 1 && string.Equals(args[0], "--verify-pdf-printer-completion", StringComparison.OrdinalIgnoreCase))
            {
                PdfCompletionSelfTests.RunPrinterAsync().GetAwaiter().GetResult();
                return;
            }
            if (args != null && args.Length == 1 && string.Equals(args[0], "--verify-pdf-completion", StringComparison.OrdinalIgnoreCase))
            {
                PdfCompletionSelfTests.RunAsync().GetAwaiter().GetResult();
                return;
            }
            if (args != null && args.Length == 1 && string.Equals(args[0], "--verify-accounting-history-readonly", StringComparison.OrdinalIgnoreCase))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                AccountingHistoryReadOnlySelfTests.Run();
                return;
            }
            if (args != null && args.Length == 1 && string.Equals(args[0], "--verify-daily-details-readonly", StringComparison.OrdinalIgnoreCase))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                DailyOperationDetailsSelfTests.Run();
                return;
            }
            if (args != null && args.Length == 1 && string.Equals(args[0], "--verify-database-opening", StringComparison.OrdinalIgnoreCase))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                ResponsiveLayoutSelfTests.VerifyDatabaseOpeningOnly();
                return;
            }
            if (args != null && args.Length == 1 && string.Equals(args[0], "--verify-dashboard-history-readonly", StringComparison.OrdinalIgnoreCase))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                ResponsiveLayoutSelfTests.VerifyDashboardHistoryReadOnlyOnly();
                return;
            }
            if (args != null && args.Length == 1 && string.Equals(args[0], "--verify-dashboard-opening", StringComparison.OrdinalIgnoreCase))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                ResponsiveLayoutSelfTests.VerifyDashboardOpeningOnly();
                return;
            }
            if (args != null && args.Length == 1 && string.Equals(args[0], "--verify-opening-preference-shown", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    ResponsiveLayoutSelfTests.VerifyOpeningPreferenceShownOnly();
                    Console.WriteLine("PASS opening preference shown integration: centered/maximized modes, normal restore bounds, repeated pre-show/Shown application and main workspace flow; settings not persisted.");
                }
                catch (Exception exception)
                {
                    Console.Error.WriteLine("Opening preference shown integration verification failed: " + exception);
                    Environment.ExitCode = 1;
                }
                return;
            }

            if (args != null && args.Length == 1 &&
                (args[0] == "--preview-validation-notification-english" || args[0] == "--preview-validation-notification-english-dark"))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                string theme = Properties.Setings.Default.ThemeMode;
                try
                {
                    Properties.Setings.Default.ThemeMode = args[0].EndsWith("-dark", StringComparison.Ordinal) ? "Dark" : "Light";
                    Console.WriteLine("BEGIN validation notification preview");
                    // Presentation only: no login, connection, cancellation, save or persisted language.
                    var sample = new UiValidationException("لا يكفي المخزون الحالي لإلغاء كمية {0}.", "QA-{0}-شركة Save");
                    using (var preview = new AppNotificationForm(sample.DisplayMessage(true) +
                        "\n\nSupport code: 20261018-123456-a1b2c3d4", UiLanguageCatalog.Translate("إلغاء فاتورة المشتريات"),
                        MessageBoxButtons.OKCancel, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2, true)
                    { ShowInTaskbar = true, StartPosition = FormStartPosition.CenterScreen })
                    {
                        Console.WriteLine("SHOW validation notification preview");
                        Application.Run(preview);
                        Console.WriteLine("END validation notification preview");
                    }
                }
                finally { Properties.Setings.Default.ThemeMode = theme; }
                return;
            }

            if (args != null && args.Length == 1 && string.Equals(args[0], "--verify-pharmacy-profile-followup", StringComparison.OrdinalIgnoreCase))
            {
                Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false); AppFonts.Initialize();
                try { PharmacyProfileFollowUpSelfTests.Run(); }
                catch (Exception error) { Console.Error.WriteLine(error); throw; }
                return;
            }

            if (args != null && args.Length == 1 && string.Equals(args[0], "--verify-settings-navigation", StringComparison.OrdinalIgnoreCase))
            {
                Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false); AppFonts.Initialize();
                try { SettingsNavigationSelfTests.Run(); }
                catch (Exception error) { Console.Error.WriteLine(error); throw; }
                return;
            }

            if (args != null && args.Length == 1 && string.Equals(args[0], "--verify-tax-layout", StringComparison.OrdinalIgnoreCase))
            {
                Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
                AppFonts.Initialize();
                try { ResponsiveLayoutSelfTests.VerifyTaxSharedShell(); }
                catch (Exception error) { Console.Error.WriteLine(error); throw; }
                Console.WriteLine("PASS tax preview layout: minimum/compact/wide, both themes/directions, accessible names, policy text, editing and dialog contracts; no SQL.");
                return;
            }

            if (args != null && args.Length == 1 && string.Equals(args[0], "--verify-currency-layout", StringComparison.OrdinalIgnoreCase))
            {
                Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
                AppFonts.Initialize();
                ResponsiveLayoutSelfTests.VerifyCurrencySettingsLayout();
                Console.WriteLine("PASS currency preview layout: minimum/compact/wide, both directions, fields/footer/refresh; no SQL.");
                return;
            }

            if (args != null && args.Length == 1 && string.Equals(args[0], "--verify-workspace-followup", StringComparison.OrdinalIgnoreCase))
            {
                Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
                WorkspaceWriteFollowUpSelfTests.Run(); return;
            }

            if (args != null && args.Length == 1 && string.Equals(args[0], "--verify-cancellation-followup", StringComparison.OrdinalIgnoreCase))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                CancellationFollowUpSelfTests.Run();
                return;
            }

            if (args != null && args.Length == 1 && string.Equals(args[0], "--verify-app-notifications", StringComparison.OrdinalIgnoreCase))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                AppNotificationSelfTests.Run();
                return;
            }

            if (args != null && args.Length == 1 && string.Equals(args[0], "--verify-permission-search", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    ResponsiveLayoutSelfTests.VerifyPermissionSearchOnly();
                    Console.WriteLine("PASS permission search layout, filtering, language and retained edits.");
                }
                catch (Exception exception)
                {
                    Console.Error.WriteLine("Permission search verification failed: " + exception);
                    Environment.ExitCode = 1;
                }
                return;
            }

            if (args != null && args.Length == 1 && string.Equals(args[0], "--verify-responsive-layout", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    // Match normal desktop bootstrap before constructing test controls.
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    // Layout fixtures set explicit viewports; keep the user opening preference
                    // from maximizing each fixture before its requested size is applied.
                    Properties.Setings.Default.OpenScreensMaximized = false;
                    ResponsiveLayoutSelfTests.RunAll();
                }
                catch (InvalidOperationException exception)
                {
                    // رسائل فحص التخطيط ثابتة ولا تتضمن اتصالاً أو بيانات عميل،
                    // لذا نعرضها في أمر التحقق لتقصير وقت تشخيص فشل QA.
                    Console.Error.WriteLine("Responsive layout verification: " + exception.Message);
                    throw;
                }
                return;
            }

            if (args != null && args.Length == 1 && string.Equals(args[0], "--verify-ledger-csv-delivery", StringComparison.OrdinalIgnoreCase))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                GeneralLedgerPresentationSelfTests.VerifyCsvDelivery();
                return;
            }

            if (args != null && args.Length == 1 && string.Equals(args[0], "--verify-ledger-presentation", StringComparison.OrdinalIgnoreCase))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                GeneralLedgerPresentationSelfTests.Run();
                return;
            }

            if (args != null && args.Length == 1 && string.Equals(args[0], "--verify-permissions-layout", StringComparison.OrdinalIgnoreCase))
            {
                try { ResponsiveLayoutSelfTests.VerifySettingsCardsOnly(); }
                catch (InvalidOperationException exception)
                {
                    Console.Error.WriteLine("Permission settings layout verification: " + exception.Message);
                    throw;
                }
                return;
            }
            if (args != null && args.Length == 1 && string.Equals(args[0], "--verify-billing-layout", StringComparison.OrdinalIgnoreCase))
            {
                try { ResponsiveLayoutSelfTests.VerifyBillingLayoutOnly(); }
                catch (InvalidOperationException exception)
                {
                    Console.Error.WriteLine("Billing responsive layout verification: " + exception.Message);
                    throw;
                }
                return;
            }

            if (args != null && args.Length == 1 && string.Equals(args[0], "--verify-manufacturer-layout", StringComparison.OrdinalIgnoreCase))
            {
                ResponsiveLayoutSelfTests.VerifyManufacturerLayoutOnly();
                return;
            }

            bool databaseSettingsOnly = args != null
                && args.Length == 1
                && string.Equals(args[0], "--database-settings", StringComparison.OrdinalIgnoreCase);

            if (args != null && args.Length == 1 && string.Equals(args[0], "--verify-journal-print-lifetime", StringComparison.OrdinalIgnoreCase))
            {
                ResponsiveLayoutSelfTests.VerifyJournalPrintLifetimeOnly();
                return;
            }
            if (args != null && args.Length == 1 && string.Equals(args[0], "--verify-journal-print-date", StringComparison.OrdinalIgnoreCase))
            {
                JournalDocumentDesktopService.VerifyPrintDate();
                return;
            }
            if (args != null && args.Length == 1 && string.Equals(args[0], "--verify-journal-print-presentation", StringComparison.OrdinalIgnoreCase))
            {
                JournalDocumentDesktopService.VerifyPrintPresentation();
                return;
            }
            if (args != null && args.Length == 2 && string.Equals(args[0], "--verify-journal-print-pdf", StringComparison.OrdinalIgnoreCase))
            {
                JournalDocumentDesktopService.VerifyPrintPdfAsync(args[1]).GetAwaiter().GetResult();
                return;
            }
            if(args!=null&&args.Length==2&&string.Equals(args[0],"--render-receipt-caption-previews",StringComparison.OrdinalIgnoreCase))
            {ResponsiveLayoutSelfTests.RenderReceiptCaptionPreviews(args[1]);return;}
            if(args!=null&&args.Length==1&&string.Equals(args[0],"--verify-receipt-currency-caption",StringComparison.OrdinalIgnoreCase))
            {ResponsiveLayoutSelfTests.VerifyReceiptCurrencyCaptionOnly();return;}
            if(args!=null&&args.Length==1&&string.Equals(args[0],"--verify-short-voucher-currency",StringComparison.OrdinalIgnoreCase))
            {ResponsiveLayoutSelfTests.VerifyShortVoucherCurrencyOnly();return;}
            if(args!=null&&args.Length==1&&string.Equals(args[0],"--verify-receipt-editor-context",StringComparison.OrdinalIgnoreCase))
            {ResponsiveLayoutSelfTests.VerifyReceiptEditorContextOnly();return;}
            if(args!=null&&args.Length==1&&string.Equals(args[0],"--verify-receipt-history-session",StringComparison.OrdinalIgnoreCase))
            {ResponsiveLayoutSelfTests.VerifyReceiptHistorySessionOnly();return;}
            if(args!=null&&args.Length==1&&string.Equals(args[0],"--verify-voucher-document-async",StringComparison.OrdinalIgnoreCase))
            {ResponsiveLayoutSelfTests.VerifyVoucherDocumentAsyncOnly();return;}
            if(args!=null&&args.Length==1&&string.Equals(args[0],"--verify-voucher-balance-async",StringComparison.OrdinalIgnoreCase))
            {ResponsiveLayoutSelfTests.VerifyReceiptBalanceAsyncOnly();return;}
            if(args!=null&&args.Length==1&&string.Equals(args[0],"--verify-receipt-initial-async",StringComparison.OrdinalIgnoreCase))
            {ResponsiveLayoutSelfTests.VerifyReceiptInitialAsyncOnly();return;}
            if(args!=null&&args.Length==4&&args[0]=="--owned-voucher-editors-qa"&&args[3]=="--confirm-owned-voucher-editor-writes")
            {VoucherEditorsOwnedQaSelfTests.Verify(args[1],args[2]);return;}
            if(args!=null&&args.Length==6&&args[0]=="--owned-voucher-editors-reader"&&args[5]=="--confirm-owned-voucher-editor-writes")
            {VoucherEditorsOwnedQaSelfTests.VerifyReader(args[1],args[2],args[3],args[4]);return;}
            if(args!=null&&args.Length==5&&args[0]=="--owned-voucher-editors-reader"&&args[4]=="--confirm-owned-voucher-editor-writes")
            {VoucherEditorsOwnedQaSelfTests.VerifyReader(args[1],args[2],args[3]);return;}
            if(args!=null&&args.Length==1&&string.Equals(args[0],"--verify-voucher-editors-recovery",StringComparison.OrdinalIgnoreCase)){VoucherEditorsRecoverySelfTests.Verify();return;}
            if(args!=null&&args.Length==1&&string.Equals(args[0],"--verify-voucher-status-session",StringComparison.OrdinalIgnoreCase))
            {VoucherSubmissionStatusSessionSelfTests.Verify();return;}
            if(args!=null&&args.Length==1&&string.Equals(args[0],"--verify-legacy-voucher-session",StringComparison.OrdinalIgnoreCase))
            {ResponsiveLayoutSelfTests.VerifyLegacyVoucherAcceptanceOnly();return;}
            if (args != null && args.Length == 1 && string.Equals(args[0], "--verify-full-journal-session", StringComparison.OrdinalIgnoreCase))
            {
                ResponsiveLayoutSelfTests.VerifyFullJournalSessionOnly();
                return;
            }
            if (args != null && args.Length == 1 && string.Equals(args[0], "--verify-permission-policy", StringComparison.OrdinalIgnoreCase))
            {
                PermissionSelfTests.RunAll();
                return;
            }

            if (args != null && args.Length == 1 && string.Equals(args[0], "--verify-appearance-persistence", StringComparison.OrdinalIgnoreCase))
            {
                ResponsiveLayoutSelfTests.VerifyAppearancePersistence();
                return;
            }
            if (args != null && args.Length == 1 && string.Equals(args[0], "--verify-device-settings", StringComparison.OrdinalIgnoreCase))
            {
                DeviceSettingsSelfTests.RunAll();
                return;
            }

            if (args != null && args.Length == 1 && string.Equals(args[0], "--verify-report-preview-lifetime", StringComparison.OrdinalIgnoreCase))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                AppFonts.Initialize();
                ReportPreviewLifetimeSelfTests.Run();
                return;
            }
            if (args != null && args.Length == 1 && string.Equals(args[0], "--verify-home-module-cards", StringComparison.OrdinalIgnoreCase))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                AppFonts.Initialize();
                HomeModuleAntdSelfTests.Run();
                return;
            }
            if (args != null && args.Length == 1 && string.Equals(args[0], "--verify-ui-language-catalog", StringComparison.OrdinalIgnoreCase))
            {
                UiLanguageCatalogSelfTests.RunAll();
                UiDirectionSelfTests.RunAll();
                return;
            }

            if (args != null && args.Length == 1 && string.Equals(args[0], "--verify-qa-readonly-guards", StringComparison.OrdinalIgnoreCase))
            {
                VerifyQaReadOnlyGuards();
                return;
            }

            if (args != null && args.Length == 1 && string.Equals(args[0], "--verify-template-file-safety", StringComparison.OrdinalIgnoreCase))
            {
                ReportTemplateStorageSelfTests.Run();
                return;
            }
            if (args != null && args.Length == 1 && string.Equals(args[0], "--verify-report-templates", StringComparison.OrdinalIgnoreCase))
            {
                ReportTemplateStoreSelfTests.RunAll();
                return;
            }

            if (args != null && args.Length == 1 && string.Equals(args[0], "--verify-operational-reports", StringComparison.OrdinalIgnoreCase))
            {
                OperationalReportSelfTests.RunAll();
                return;
            }

            if (args != null && args.Length == 3 && string.Equals(args[0], "--verify-qa-medicine-catalog-import", StringComparison.OrdinalIgnoreCase)
                && args[2] == "--confirm-qa-catalog-rollback")
            {
                QaCsvImportSelfTests.RunMedicineCatalogRollback(args[1]);
                return;
            }
            if (args != null && args.Length == 4 && string.Equals(args[0], "--verify-qa-medicine-catalog-commit", StringComparison.OrdinalIgnoreCase)
                && args[3] == "--confirm-owned-qa-catalog-commit")
            {
                QaCsvImportSelfTests.RunMedicineCatalogCommittedOnOwnedRestore(args[1], args[2]);
                return;
            }
            if (args != null && args.Length == 1 && string.Equals(args[0], "--verify-commercial-return-contract", StringComparison.OrdinalIgnoreCase))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                AppFonts.Initialize();
                ResponsiveLayoutSelfTests.VerifyCommercialReturnContract();
                return;
            }

            if (args != null && args.Length == 1 && string.Equals(args[0], "--verify-sales-idempotency", StringComparison.OrdinalIgnoreCase))
            {
                SalesIdempotencySelfTests.RunAll();
                return;
            }

            if (args != null && args.Length == 4 && string.Equals(args[0], "--verify-qa-medicine-writes", StringComparison.OrdinalIgnoreCase)
                && args[3] == "--confirm-owned-qa-medicine-writes")
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                QaMedicineWritesSelfTests.Run(args[1], args[2]);
                return;
            }
            if (args != null && args.Length == 4 && string.Equals(args[0], "--verify-qa-manufacturer-crud", StringComparison.OrdinalIgnoreCase)
                && args[3] == "--confirm-owned-qa-manufacturer-crud")
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                QaManufacturerCrudSelfTests.Run(args[1], args[2]);
                return;
            }
            if (args != null && args.Length == 5 && args[0] == "--verify-qa-quick-journal-editor-restart"
                && args[4] == "--confirm-owned-qa-quick-journal-editor-restart")
            {
                QuickJournalRecoveryQaSelfTests.VerifyRestartChild(args[1],args[2],args[3]); return;
            }
            if (args != null && args.Length == 6 && args[0] == "--verify-qa-journal-editor-crash"
                && args[5] == "--confirm-owned-qa-journal-editor-crash")
            {
                JournalEntriesRecoveryQaSelfTests.RunCrashChild(args[1], args[2], args[3], args[4]);
                return;
            }
            if (args != null && args.Length == 5 && args[0] == "--verify-qa-journal-editor-restart"
                && args[4] == "--confirm-owned-qa-journal-editor-restart")
            {
                JournalEntriesRecoveryQaSelfTests.VerifyRestartChild(args[1],args[2],args[3]);
                return;
            }
            if (args != null && args.Length == 4 && string.Equals(args[0], "--verify-qa-journal-posting", StringComparison.OrdinalIgnoreCase)
                && args[3] == "--confirm-owned-qa-journal-posting")
            {
                PermissionSelfTests.RunOwnedQaJournalPosting(args[1],args[2]);
                return;
            }
            if (args != null && args.Length == 4 && string.Equals(args[0], "--verify-qa-employee-permissions", StringComparison.OrdinalIgnoreCase)
                && args[3] == "--confirm-owned-qa-employee-permissions")
            {
                PermissionSelfTests.RunOwnedQaEmployeePermissions(args[1], args[2]);
                return;
            }
            if (args != null && args.Length == 4 && string.Equals(args[0], "--verify-qa-cashbox-import", StringComparison.OrdinalIgnoreCase)
                && args[3] == "--confirm-owned-qa-cashbox-import")
            {
                QaCashBoxSaleSelfTests.RunImportOnOwnedRestore(args[1], args[2]);
                return;
            }
            if (args != null && args.Length == 1 && string.Equals(args[0], "--verify-qa-csv-import", StringComparison.OrdinalIgnoreCase))
            {
                QaCsvImportSelfTests.RunAll();
                return;
            }

            if (args != null && args.Length == 3 && string.Equals(args[0], "--verify-operational-report-sources-readonly", StringComparison.OrdinalIgnoreCase))
            {
                // Reading report sources is a verification activity: it must run on an isolated QA
                // database with an explicit server and database, never on the customer profile.
                int verified = 0;
                foreach (OperationalReportSource source in OperationalReportCatalog.GetSources())
                {
                    DataTable table = OperationalReportCatalog.LoadQaReadOnly(args[1], args[2], source.Key, DateTime.Today.AddDays(-30), DateTime.Today, string.Empty);
                    if (table.Columns.Count == 0)
                    {
                        throw new InvalidOperationException("مصدر التقرير التشغيلي لم يُرجع أعمدة: " + source.Key);
                    }
                    verified++;
                }
                Console.WriteLine("Operational report sources complete. Database={0}; Sources={1}.", args[2], verified);
                return;
            }

            if (args != null && args.Length == 1 && string.Equals(args[0], "--verify-operational-report-sources-readonly", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("--verify-operational-report-sources-readonly requires <server> <database>; use a database whose name ends with _qa.");
            }

            if (args != null && args.Length == 1 && string.Equals(args[0], "--verify-accounting-integrity-readonly", StringComparison.OrdinalIgnoreCase))
            {
                DataTable issues = AccountingService.GetJournalIntegrityIssues(DateTime.Today.AddYears(-20), DateTime.Today);
                if (!issues.Columns.Contains("رقم القيد") || !issues.Columns.Contains("الحالة"))
                {
                    throw new InvalidOperationException("نتيجة فحص سلامة القيود لا تحتوي الأعمدة المتوقعة.");
                }
                return;
            }

            if (args != null && args.Length == 1 && string.Equals(args[0], "--verify-period-close-readiness-readonly", StringComparison.OrdinalIgnoreCase))
            {
                DataTable readiness = AccountingService.GetPeriodCloseReadiness(DateTime.Today.AddYears(-1), DateTime.Today);
                if (!readiness.Columns.Contains("الفحص") || !readiness.Columns.Contains("الحالة") || readiness.Rows.Count < 4)
                {
                    throw new InvalidOperationException("نتيجة جاهزية الإقفال لا تحتوي الفحوص المتوقعة.");
                }
                return;
            }

            if (args != null && args.Length == 1 && string.Equals(args[0], "--verify-inventory-integrity-readonly", StringComparison.OrdinalIgnoreCase))
            {
                DataTable issues = InventoryService.GetMovementIntegrityIssues();
                if (!issues.Columns.Contains("الصنف") || !issues.Columns.Contains("الحالة"))
                {
                    throw new InvalidOperationException("نتيجة فحص مطابقة المخزون لا تحتوي الأعمدة المتوقعة.");
                }
                return;
            }

            if (args != null && args.Length == 1 && string.Equals(args[0], "--report-runtime-connection", StringComparison.OrdinalIgnoreCase))
            {
                DatabaseConnectionProfile profile = Database.GetConnectionProfile();
                Console.WriteLine("Environment={0}; Database={1}; SettingsFile={2}", profile.EnvironmentNameAr, profile.DatabaseName, RuntimeSettingsStore.SettingsPath);
                return;
            }

            if (args != null && args.Length == 2 && string.Equals(args[0], "--verify-qa-import-registry-rollback", StringComparison.OrdinalIgnoreCase)
                && args[1] == "--confirm-qa-transaction")
            {
                ImportedInvoiceRegistryQaSelfTests.Run();
                return;
            }
            if (args != null && args.Length == 3 && string.Equals(args[0], "--verify-qa-invoice-drafts", StringComparison.OrdinalIgnoreCase)
                && args[2] == "--confirm-qa-draft-write")
            {
                QaInvoiceImportDraftSelfTests.Run(args[1]);
                return;
            }
            if (args != null && args.Length == 3 && string.Equals(args[0], "--verify-qa-imported-sale-roundtrip", StringComparison.OrdinalIgnoreCase)
                && args[2] == "--confirm-qa-imported-sale")
            {
                QaImportedSaleSelfTests.Run(args[1]);
                return;
            }
            if (args != null && args.Length == 4 && string.Equals(args[0], "--verify-qa-cashbox-sale-roundtrip", StringComparison.OrdinalIgnoreCase)
                && args[3] == "--confirm-qa-cashbox-sale")
            {
                QaCashBoxSaleSelfTests.Run(args[1], args[2]);
                return;
            }
            if (args != null && args.Length == 3 && string.Equals(args[0], "--verify-responsive-layout-group", StringComparison.OrdinalIgnoreCase))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                // Keep the user's opening preference from maximizing each fixture before its size is applied.
                Properties.Setings.Default.OpenScreensMaximized = false;
                int groupIndex, groupCount;
                if (!int.TryParse(args[1], out groupIndex) || !int.TryParse(args[2], out groupCount))
                    throw new ArgumentException("--verify-responsive-layout-group requires <groupIndex> <groupCount>.");
                try
                {
                    ResponsiveLayoutSelfTests.RunGroup(groupIndex, groupCount);
                }
                catch (InvalidOperationException exception)
                {
                    Console.Error.WriteLine("Responsive layout verification: " + exception.Message);
                    throw;
                }
                return;
            }

  if (args != null && args.Length == 1 && string.Equals(args[0], "--verify-window-stability", StringComparison.OrdinalIgnoreCase))
  {
  Application.EnableVisualStyles();
  Application.SetCompatibleTextRenderingDefault(false);
  AppFonts.Initialize();
  WindowStabilitySelfTests.RunAll();
  return;
  }
  if (args != null && args.Length == 1 && string.Equals(args[0], "--verify-live-accessibility", StringComparison.OrdinalIgnoreCase))
  {
  Application.EnableVisualStyles();
  Application.SetCompatibleTextRenderingDefault(false);
  AppFonts.Initialize();
  LiveAccessibilitySelfTests.RunAll();
  return;
  }
  if (args != null && args.Length == 4 && string.Equals(args[0], "--verify-live-accessibility-screen", StringComparison.OrdinalIgnoreCase))
  {
  Application.EnableVisualStyles();
  Application.SetCompatibleTextRenderingDefault(false);
  AppFonts.Initialize();
  LiveAccessibilitySelfTests.RunSingle(int.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture), args[2], args[3]);
  return;
  }

            if (args != null && args.Length == 3 && string.Equals(args[0], "--verify-qa-purchase-abandon", StringComparison.OrdinalIgnoreCase)
                && args[2] == "--confirm-qa-purchase-abandon")
            {
                try
                {
                    QaPurchaseIdempotencySqlSelfTests.RunAbandon(args[1]);
                }
                catch (Exception error)
                {
                    QaSalesIdempotencySqlSelfTests.ReportFailure(error);
                    throw;
                }
                return;
            }

            if (args != null && args.Length == 3 && string.Equals(args[0], "--verify-qa-purchase-concurrency", StringComparison.OrdinalIgnoreCase)
                && args[2] == "--confirm-qa-purchase-concurrency")
            {
                try
                {
                    QaPurchaseIdempotencySqlSelfTests.RunConcurrency(args[1]);
                }
                catch (Exception error)
                {
                    QaSalesIdempotencySqlSelfTests.ReportFailure(error);
                    throw;
                }
                return;
            }

            if (args != null && args.Length == 3 && string.Equals(args[0], "--verify-qa-purchase-idempotency", StringComparison.OrdinalIgnoreCase)
                && args[2] == "--confirm-qa-purchase-idempotency")
            {
                try
                {
                    QaPurchaseIdempotencySqlSelfTests.Run(args[1]);
                }
                catch (Exception error)
                {
                    QaSalesIdempotencySqlSelfTests.ReportFailure(error);
                    throw;
                }
                return;
            }

            if (args != null && args.Length == 3 && string.Equals(args[0], "--verify-qa-sales-concurrency", StringComparison.OrdinalIgnoreCase)
                && args[2] == "--confirm-qa-sales-concurrency")
            {
                try
                {
                    QaSalesIdempotencySqlSelfTests.RunConcurrency(args[1]);
                }
                catch (Exception error)
                {
                    QaSalesIdempotencySqlSelfTests.ReportFailure(error);
                    throw;
                }
                return;
            }

            if (args != null && args.Length == 3 && string.Equals(args[0], "--verify-qa-sales-idempotency", StringComparison.OrdinalIgnoreCase)
                && args[2] == "--confirm-qa-sales-idempotency")
            {
                try
                {
                    QaSalesIdempotencySqlSelfTests.Run(args[1]);
                }
                catch (Exception error)
                {
                    QaSalesIdempotencySqlSelfTests.ReportFailure(error);
                    throw;
                }
                return;
            }

            if (args != null && args.Length == 4 && string.Equals(args[0], "--verify-qa-sales-roundtrip", StringComparison.OrdinalIgnoreCase)
                && args[3] == "--confirm-qa-invoice-writes")
            {
                QaSalesInvoiceSelfTests.Run(args[1], args[2]);
                return;
            }
            if (args != null && args.Length == 3 && string.Equals(args[0], "--verify-qa-tax-recovery", StringComparison.OrdinalIgnoreCase)
                && args[2] == "--confirm-qa-tax-writes")
            {
                QaTaxPolicySelfTests.Run(args[1]);
                return;
            }
            if (args != null && args.Length == 3 && string.Equals(args[0], "--verify-qa-saudi-batch-rollback", StringComparison.OrdinalIgnoreCase)
                && args[2] == "--confirm-qa-transaction-writes")
            {
                QaSaudiBatchRollbackVerification.Run(args[1]);
                return;
            }
            if (args != null && args.Length == 1 && string.Equals(args[0], "--verify-qa-package-profile", StringComparison.OrdinalIgnoreCase))
            {
                DatabaseConnectionProfile profile = Database.GetConnectionProfile();
                if (!RuntimeSettingsStore.IsBuildConnectionProfileForced || !profile.IsTestEnvironment)
                {
                    throw new InvalidOperationException("حزمة QA يجب أن تفرض اتصالاً باسم قاعدة ينتهي بـ _qa أو _test أو _sandbox، دون استخدام إعداد التشغيل المحفوظ.");
                }

                Console.WriteLine("QA package profile is pinned. Environment={0}; Database={1}; Source={2}.", profile.EnvironmentNameAr, profile.DatabaseName, RuntimeSettingsStore.ProfileSourceText);
                return;
            }

            if (args != null && args.Length == 3 && string.Equals(args[0], "--initialize-core-schema", StringComparison.OrdinalIgnoreCase))
            {
                DatabaseSchemaValidationResult result = Database.InitializeCoreSchema(args[1], args[2]);
                Console.WriteLine("Core database schema is ready. Database={0}; Tables={1}; Version={2}.", result.DatabaseName, result.TableCount, result.VersionNo);
                return;
            }

            if (args != null && args.Length == 1 && string.Equals(args[0], "--verify-core-schema", StringComparison.OrdinalIgnoreCase))
            {
                DatabaseSchemaValidationResult result = Database.ValidateConfiguredCoreSchema();
                Console.WriteLine("Core database schema is healthy. Database={0}; Tables={1}; Version={2}.", result.DatabaseName, result.TableCount, result.VersionNo);
                return;
            }

            if (args != null && args.Length == 3 && string.Equals(args[0], "--verify-qa-core-schema-readonly", StringComparison.OrdinalIgnoreCase))
            {
                DatabaseSchemaValidationResult result = Database.ValidateQaCoreSchemaReadOnly(args[1], args[2]);
                Console.WriteLine("QA core database schema is healthy. Database={0}; Tables={1}; Version={2}.", result.DatabaseName, result.TableCount, result.VersionNo);
                return;
            }

            if (args != null && args.Length == 3 && string.Equals(args[0], "--verify-qa-integrity-readonly", StringComparison.OrdinalIgnoreCase))
            {
                DataTable journalIssues = AccountingService.GetQaJournalIntegrityIssuesReadOnly(args[1], args[2], DateTime.Today.AddYears(-20), DateTime.Today);
                DataTable inventoryIssues = InventoryService.GetQaMovementIntegrityIssuesReadOnly(args[1], args[2]);
                if (!journalIssues.Columns.Contains("رقم القيد") || !inventoryIssues.Columns.Contains("الصنف"))
                {
                    throw new InvalidOperationException("نتيجة فحص QA لا تحتوي الأعمدة المتوقعة.");
                }
                Console.WriteLine("QA integrity check complete. Database={0}; JournalIssues={1}; InventoryIssues={2}.", args[2], journalIssues.Rows.Count, inventoryIssues.Rows.Count);
                return;
            }

            if (args != null && args.Length == 3 && string.Equals(args[0], "--verify-qa-period-close-readiness-readonly", StringComparison.OrdinalIgnoreCase))
            {
                DataTable readiness = AccountingService.GetQaPeriodCloseReadinessReadOnly(args[1], args[2], DateTime.Today.AddYears(-1), DateTime.Today);
                if (!readiness.Columns.Contains("الفحص") || !readiness.Columns.Contains("الحالة") || readiness.Rows.Count < 4)
                {
                    throw new InvalidOperationException("نتيجة جاهزية إقفال QA لا تحتوي الفحوص المتوقعة.");
                }
                int observations = readiness.AsEnumerable().Sum(row => Convert.ToInt32(row["عدد الملاحظات"]));
                Console.WriteLine("QA period-close readiness complete. Database={0}; Checks={1}; Observations={2}.", args[2], readiness.Rows.Count, observations);
                return;
            }

            if (args != null && args.Length == 3 && string.Equals(args[0], "--verify-qa-operational-report-sources-readonly", StringComparison.OrdinalIgnoreCase))
            {
                int sources = 0;
                foreach (OperationalReportSource source in OperationalReportCatalog.GetSources())
                {
                    DataTable table = OperationalReportCatalog.LoadQaReadOnly(args[1], args[2], source.Key, DateTime.Today.AddDays(-30), DateTime.Today, string.Empty);
                    if (table.Columns.Count == 0)
                    {
                        throw new InvalidOperationException("مصدر التقرير التشغيلي لم يُرجع أعمدة: " + source.Key);
                    }
                    sources++;
                }
                Console.WriteLine("QA operational report sources complete. Database={0}; Sources={1}.", args[2], sources);
                return;
            }

            if (args != null && args.Length == 3 && string.Equals(args[0], "--verify-qa-stock-readonly", StringComparison.OrdinalIgnoreCase))
            {
                InventoryStockReadQaVerification.RunReadOnly(args[1], args[2]);
                return;
            }
            if (args != null && args.Length == 3 && string.Equals(args[0], "--verify-qa-lot-stock-readonly", StringComparison.OrdinalIgnoreCase))
            {
                DataTable all = InventoryService.GetLotStockSnapshotQaReadOnly(args[1], args[2], string.Empty, true);
                DataTable stocked = InventoryService.GetLotStockSnapshotQaReadOnly(args[1], args[2], string.Empty, false);
                foreach (string name in new[] { "رقم التشغيلة", "الكمية الحالية", "الكمية القابلة للصرف", "قيمة التكلفة", "حالة التشغيلة", "معرف التشغيلة" })
                    if (!all.Columns.Contains(name))
                        throw new InvalidOperationException("عمود جرد التشغيلات مفقود: " + name);
                if (all.Rows.Cast<DataRow>().Any(row =>
                    Convert.ToDecimal(row["الكمية القابلة للصرف"]) < 0 ||
                    Convert.ToDecimal(row["الكمية القابلة للصرف"]) > Convert.ToDecimal(row["الكمية الحالية"])))
                    throw new InvalidOperationException("الكمية القابلة للصرف تتجاوز رصيد التشغيلة.");
                if (stocked.Rows.Count > all.Rows.Count || stocked.Rows.Cast<DataRow>().Any(row => Convert.ToDecimal(row["الكمية الحالية"]) <= 0))
                    throw new InvalidOperationException("فلتر التشغيلات ذات الرصيد الموجب غير صحيح.");
                Console.WriteLine("QA lot stock read complete. Database={0}; Lots={1}; Stocked={2}; PersistentWrites=0.",
                    args[2], all.Rows.Count, stocked.Rows.Count);
                return;
            }
            if (args != null && args.Length == 3 && string.Equals(args[0], "--verify-qa-inventory-alerts-readonly", StringComparison.OrdinalIgnoreCase))
            {
                DataTable todayOnly = InventoryService.GetInventoryAlertsQaReadOnly(args[1], args[2], 10, 0);
                DataTable withinThirtyDays = InventoryService.GetInventoryAlertsQaReadOnly(args[1], args[2], 10, 30);
                string[] required = { "نوع التنبيه", "الصنف", "الكمية", "تاريخ الانتهاء", "الشركة", "التفاصيل" };
                if (required.Any(column => !todayOnly.Columns.Contains(column)) ||
                    required.Any(column => !withinThirtyDays.Columns.Contains(column)))
                    throw new InvalidOperationException("نتيجة تنبيهات المخزون لا تحتوي الأعمدة المطلوبة.");
                foreach (DataRow row in todayOnly.Rows)
                {
                    if (!string.Equals(Convert.ToString(row["نوع التنبيه"]), "قريب الانتهاء", StringComparison.Ordinal) ||
                        row["تاريخ الانتهاء"] == DBNull.Value) continue;
                    if (Convert.ToDateTime(row["تاريخ الانتهاء"]).Date > DateTime.Today)
                        throw new InvalidOperationException("فترة صفر يوم أعادت صنفًا ينتهي بعد اليوم.");
                }
                if (todayOnly.Rows.Count > withinThirtyDays.Rows.Count)
                    throw new InvalidOperationException("نتيجة فترة صفر يوم تجاوزت نتيجة فترة الثلاثين يومًا.");
                Console.WriteLine("QA inventory alerts read-only passed. Database={0}; TodayRows={1}; ThirtyDayRows={2}; PersistentWrites=0.",
                    args[2], todayOnly.Rows.Count, withinThirtyDays.Rows.Count);
                return;
            }
            if (args != null && args.Length == 4 &&
                string.Equals(args[0], "--verify-qa-lot-stock-rollback", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(args[3], "--confirm-qa-lot-stock-writes", StringComparison.OrdinalIgnoreCase))
            {
                InventoryService.VerifyQaLotStockRollback(args[1], args[2]);
                Console.WriteLine("QA lot stock transaction and rollback passed. Database={0}; PersistentWrites=0.", args[2]);
                return;
            }

            if (args != null && args.Length == 3 && string.Equals(args[0], "--verify-qa-excel-export-readonly", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(args[2]) || !args[2].EndsWith("_qa", StringComparison.OrdinalIgnoreCase))
                    throw new UnauthorizedAccessException("Excel source roundtrip is QA-only.");
                string[] approvedSources = { "Medicines", "SalesLines", "LegacySalesLines", "PurchaseLines", "InventoryMovements", "JournalLines", "CashLedger", "CashBoxes" };
                DateTime qaExportTo = DateTime.Today;
                DateTime qaExportFrom = qaExportTo.AddDays(-30);
                foreach (string sourceKey in approvedSources)
                {
                    DataTable source;
                    if (sourceKey == "CashBoxes")
                    {
                        var repository = new PharmacyManagement.Infrastructure.SqlServer.SqlCashBoxRepository(() =>
                            new System.Data.SqlClient.SqlConnection(Database.BuildSettingsConnectionString(
                                args[1], args[2], true)));
                        source = CashBoxWorkbook.CreateExport(repository.GetAll());
                    }
                    else source = OperationalReportCatalog.LoadQaReadOnly(args[1], args[2], sourceKey,
                        qaExportFrom, qaExportTo, string.Empty);
                    if (source.Rows.Count >= 2000)
                        throw new InvalidOperationException("مصدر QA بلغ حد رفض التصدير: " + sourceKey);
                    using (var workbook = new MemoryStream())
                    {
                        ExcelWorkbookWriter.Write(source, sourceKey, workbook);
                        workbook.Position = 0;
                        DataTable restored = ExcelWorkbookReader.Read(workbook);
                        if (restored.Rows.Count != source.Rows.Count || restored.Columns.Count != source.Columns.Count)
                            throw new InvalidOperationException("Excel لم يحافظ على عدد صفوف وأعمدة مصدر QA: " + sourceKey);
                        for (int column = 0; column < source.Columns.Count; column++)
                        {
                            if (restored.Columns[column].ColumnName != source.Columns[column].ColumnName)
                                throw new InvalidOperationException("Excel غيّر عنوان عمود في مصدر QA: " + sourceKey);
                            for (int row = 0; row < source.Rows.Count; row++)
                            {
                                object value = source.Rows[row][column];
                                string expected = value == DBNull.Value ? string.Empty : value is DateTime
                                    ? ((DateTime)value).ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture)
                                    : Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
                                string actual = Convert.ToString(restored.Rows[row][column], System.Globalization.CultureInfo.InvariantCulture);
                                if (!string.Equals(actual, expected, StringComparison.Ordinal))
                                    throw new InvalidOperationException("Excel غيّر قيمة في مصدر QA: " + sourceKey + "، صف " + (row + 1) + "، عمود " + (column + 1));
                            }
                        }
                    }
                }
                string temporaryWorkbook = Path.Combine(Path.GetTempPath(),
                    "pharmacy-qa-excel-export-" + Guid.NewGuid().ToString("N") + ".xlsx");
                try
                {
                    DataTable medicines = OperationalReportCatalog.LoadQaReadOnly(args[1], args[2], "Medicines",
                        qaExportFrom, qaExportTo, string.Empty);
                    using (var output = new FileStream(temporaryWorkbook, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                        ExcelWorkbookWriter.Write(medicines, "الأدوية والأرصدة الحالية", output);
                    DataTable restored = ExcelWorkbookReader.Read(temporaryWorkbook);
                    if (restored.Rows.Count != medicines.Rows.Count || restored.Columns.Count != medicines.Columns.Count)
                        throw new InvalidOperationException("ملف Excel المكتوب على القرص لم يحافظ على حجم مصدر QA.");
                }
                finally
                {
                    if (File.Exists(temporaryWorkbook)) File.Delete(temporaryWorkbook);
                }
                SessionContext.Start("QA Excel Export", "Manager");
                try
                {
                    foreach (string sourceKey in approvedSources)
                    {
                        string serviceFile = Path.Combine(Path.GetTempPath(),
                            "pharmacy-qa-export-service-" + Guid.NewGuid().ToString("N") + ".xlsx");
                        try
                        {
                            DataTable expected;
                            if (sourceKey == "CashBoxes")
                            {
                                var repository = new PharmacyManagement.Infrastructure.SqlServer.SqlCashBoxRepository(() =>
                                    new System.Data.SqlClient.SqlConnection(Database.BuildSettingsConnectionString(args[1], args[2], true)));
                                expected = CashBoxWorkbook.CreateExport(repository.GetAll());
                            }
                            else expected = OperationalReportCatalog.LoadQaReadOnly(args[1], args[2], sourceKey,
                                qaExportFrom, qaExportTo, string.Empty);
                            using (expected)
                            {
                                int rows = ExcelDataExportService.ExportQaReadOnly(args[1], args[2], sourceKey,
                                    qaExportFrom, qaExportTo, string.Empty, serviceFile);
                                if (rows >= ExcelWorkbookWriter.MaximumCompleteExportRows || !File.Exists(serviceFile))
                                    throw new InvalidOperationException("خدمة تصدير QA لم تنشئ ملفًا كاملًا: " + sourceKey);
                                using (DataTable restored = ExcelWorkbookReader.Read(serviceFile))
                                {
                                    if (restored.Rows.Count != rows)
                                        throw new InvalidOperationException("ملف خدمة تصدير QA فقد صفوفًا: " + sourceKey);
                                    VerifyExcelRoundtripValues(expected, restored, sourceKey);
                                    Console.WriteLine("QA Excel service source={0}; Rows={1}; Columns={2}; FileRoundtrip=passed; CellValues=passed; EmptySample={3}.",
                                        sourceKey, rows, restored.Columns.Count, rows == 0);
                                }
                            }
                        }
                        finally
                        {
                            if (File.Exists(serviceFile)) File.Delete(serviceFile);
                        }
                    }
                }
                finally { SessionContext.Clear(); }
                Console.WriteLine("QA Excel export roundtrip complete. Database={0}; Sources={1}; LocalFileRoundtrip=passed; ServiceFileRoundtrip=passed; PersistentWrites=0.", args[2], approvedSources.Length);
                return;
            }

            if (args != null && args.Length == 3 && string.Equals(args[0], "--verify-qa-customer-lookup-concurrency", StringComparison.OrdinalIgnoreCase))
            {
                CustomerLookupService.VerifyQaMissingPhoneLookupSerializes(args[1], args[2]);
                Console.WriteLine("QA customer lookup concurrency complete. Database={0}; PersistentWrites=0.", args[2]);
                return;
            }

            if (args != null && args.Length == 3 && string.Equals(args[0], "--verify-qa-manual-acceptance-readiness", StringComparison.OrdinalIgnoreCase))
            {
                QaManualAcceptanceReadinessResult readiness = QaManualAcceptanceReadiness.Inspect(args[1], args[2]);
                if (!readiness.HasRequiredData)
                {
                    throw new InvalidOperationException("قاعدة QA لا تحتوي الحد الأدنى للاختبار اليدوي: مستخدم واحد ومورد واحد ودواء واحد بمخزون موجب.");
                }

                Console.WriteLine(
                    "QA manual acceptance readiness complete. Database={0}; Employees={1}; Suppliers={2}; StockedMedicines={3}; BarcodeMedicines={4}; LowStockMedicines={5}; InventoryMovements={6}; AuditEvents={7}; PersistentWrites=0.",
                    args[2],
                    readiness.EmployeeCount,
                    readiness.SupplierCount,
                    readiness.StockedMedicineCount,
                    readiness.BarcodeMedicineCount,
                    readiness.LowStockMedicineCount,
                    readiness.InventoryMovementCount,
                    readiness.AuditEventCount);
                return;
            }

            if (args != null && args.Length == 3 && string.Equals(args[0], "--verify-qa-product-identity-return-schema-readonly", StringComparison.OrdinalIgnoreCase))
            {
                ProductIdentityAndReturnsSchemaVerification.VerifyQaReadOnly(args[1], args[2]);
                Console.WriteLine("QA product identity and partial-return schema is healthy. Database={0}; PersistentWrites=0.", args[2]);
                return;
            }

            if (args != null && args.Length == 3 && string.Equals(args[0], "--verify-qa-commercial-return-schema-readonly", StringComparison.OrdinalIgnoreCase))
            {
                CommercialReturnSchemaVerification.VerifyQaReadOnly(args[1], args[2]);
                Console.WriteLine("QA commercial partial-return schema is healthy. Database={0}; PersistentWrites=0.", args[2]);
                return;
            }

            if (args != null && args.Length == 4 && string.Equals(args[0], "--verify-qa-commercial-return-roundtrip", StringComparison.OrdinalIgnoreCase))
            {
                QaCommercialReturnSelfTests.Run(args[1], args[2], args[3]);
                Console.WriteLine("QA commercial sale/return round trip passed. Database={0}; PersistentWrites=0.", args[2]);
                return;
            }

            if (args != null && args.Length == 5 &&
                (args[0]=="--verify-qa-commercial-return-durable-write"||args[0]=="--verify-qa-commercial-return-durable-replay"||args[0]=="--verify-qa-commercial-return-durable-await-stop"))
            {
                QaCommercialReturnSelfTests.RunDurableRecovery(args[1],args[2],args[3],args[4],args[0].EndsWith("-replay",StringComparison.Ordinal),args[0].EndsWith("-await-stop",StringComparison.Ordinal));
                return;
            }

            if (args != null && args.Length == 3 && string.Equals(args[0], "--verify-commercial-return-pending", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
                    CommercialReturnPendingSelfTests.Run(args[1],args[2]);
                }
                catch(Exception error) { Console.Error.WriteLine(error); throw; }
                return;
            }

            if (args != null && args.Length == 5 && args[0] == "--verify-owned-return-print-pdf")
            {
                int ownedDatabaseId;
                if (!int.TryParse(args[4], out ownedDatabaseId)) throw new ArgumentException("Owned database id is required.");
                Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false); AppFonts.Initialize();
                QaCommercialReturnSelfTests.VerifyOwnedPrintPdfAsync(args[1], args[2], args[3], ownedDatabaseId).GetAwaiter().GetResult();
                return;
            }

            if (args != null && args.Length == 6 && args[0] == "--verify-owned-return-print-fixture")
            {
                int ownedDatabaseId;
                if (!int.TryParse(args[4], out ownedDatabaseId)) throw new ArgumentException("Owned database id is required.");
                QaCommercialReturnSelfTests.PrepareOwnedPrintFixture(args[1], args[2], args[3], ownedDatabaseId, args[5]);
                return;
            }

            if (args != null && args.Length == 4 && string.Equals(args[0], "--verify-qa-commercial-return-replay", StringComparison.OrdinalIgnoreCase))
            {
                QaCommercialReturnSelfTests.RunCommittedReplay(args[1], args[2], args[3]);
                return;
            }

            if (args != null && args.Length == 4 && string.Equals(args[0], "--verify-qa-commercial-return-concurrency", StringComparison.OrdinalIgnoreCase))
            {
                QaCommercialReturnSelfTests.RunCommittedConcurrency(args[1], args[2], args[3]);
                Console.WriteLine("Temporary sandbox commercial-return concurrency passed. Database={0}.", args[2]);
                return;
            }

            if (args != null && args.Length == 3 && string.Equals(args[0], "--verify-qa-active-ingredient-search-rollback", StringComparison.OrdinalIgnoreCase))
            {
                ActiveIngredientQaRollbackVerification.VerifySearchRoundTrip(args[1], args[2]);
                Console.WriteLine("QA active-ingredient search round trip passed. Database={0}; PersistentWrites=0.", args[2]);
                return;
            }

            if (args != null && args.Length == 4 && string.Equals(args[0], "--verify-qa-purchase-invoice-rollback", StringComparison.OrdinalIgnoreCase)
                && string.Equals(args[3], "--confirm-qa-purchase-rollback", StringComparison.Ordinal))
            {
                QaPurchaseInvoiceRollbackVerification.Run(args[1], args[2]);
                Console.WriteLine("QA purchase invoice rollback passed. Database={0}; PersistentWrites=0.", args[2]);
                return;
            }

            if (args != null && args.Length == 4 && string.Equals(args[0], "--prepare-qa-purchase-cancellation-ui", StringComparison.OrdinalIgnoreCase))
            {
                string purchaseId = QaPurchaseCancellationSelfTests.PrepareLiveUiFixture(args[1], args[2], args[3]);
                Console.WriteLine("QA live purchase cancellation fixture prepared. PurchaseId={0}; Database={1}.", purchaseId, Database.GetConnectionProfile().DatabaseName);
                return;
            }
            if (args != null && args.Length == 4 && string.Equals(args[0], "--verify-qa-purchase-cancellation-roundtrip", StringComparison.OrdinalIgnoreCase))
            {
                QaPurchaseCancellationSelfTests.Run(args[1], args[2], args[3]);
                Console.WriteLine("QA purchase invoice cancellation round trip passed. Database={0}.", Database.GetConnectionProfile().DatabaseName);
                return;
            }

            if (args != null && args.Length == 4 && string.Equals(args[0], "--verify-qa-inventory-rollback", StringComparison.OrdinalIgnoreCase)
                && string.Equals(args[3], "--confirm-qa-inventory-rollback", StringComparison.Ordinal))
            {
                QaInventoryRollbackVerification.Run(args[1], args[2]);
                Console.WriteLine("QA inventory, movement and audit rollback passed. Database={0}; PersistentWrites=0.", args[2]);
                return;
            }

            if (args != null && args.Length == 2 && string.Equals(args[0], "--install-commercial-schema", StringComparison.OrdinalIgnoreCase))
            {
                // مسار اختبار محمي: تحقق من الاسم والنسخة أولاً، ثم هيئ الأساس التجاري.
                // لا يرحل أرصدة تاريخية ولا يصلح للإنتاج.
                if (!LocalAdministratorAccess.IsCurrentUserAdministrator())
                {
                    throw new UnauthorizedAccessException("تثبيت مخطط الدفعات مسموح فقط لحساب Windows محلي مسؤول على قاعدة اختبار مستعادة.");
                }
                using (var preflightConnection = Database.CreateConnection())
                {
                    CommercialSchemaInstaller.EnsureTargetIsClearlyNamedTestDatabase(preflightConnection);
                    CommercialSchemaInstaller.VerifyBackupForTargetServer(preflightConnection, args[1]);
                }
                Database.EnsureReady();
                using (var connection = Database.CreateConnection())
                {
                    CommercialSchemaInstaller.Install(connection, new CommercialMigrationRequest
                    {
                        VerifiedBackupPath = args[1],
                        IsTestDatabase = true,
                        AcknowledgeNoHistoricalLotBackfill = true
                    });
                }
                return;
            }

            // لا يعتمد هذا الأمر على ملف اتصال التطبيق أو الإعداد المحفوظ؛ الخادم
            // وقاعدة QA إلزاميان حتى لا يتحول تثبيت مخطط الدفعات إلى عملية إنتاجية.
            if (args != null && args.Length == 4 && string.Equals(args[0], "--install-commercial-schema-qa", StringComparison.OrdinalIgnoreCase))
            {
                Database.ValidateQaCoreSchemaReadOnly(args[1], args[2]);
                string qaConnectionString = Database.BuildSettingsConnectionString(args[1], args[2], true);
                using (var connection = new System.Data.SqlClient.SqlConnection(qaConnectionString))
                {
                    CommercialSchemaInstaller.EnsureTargetIsClearlyNamedTestDatabase(connection);
                    CommercialSchemaInstaller.EnsureSchemaInstallPermission(connection);
                    CommercialSchemaInstaller.VerifyBackupForTargetServer(connection, args[3]);
                    CommercialSchemaInstaller.Install(connection, new CommercialMigrationRequest
                    {
                        VerifiedBackupPath = args[3],
                        IsTestDatabase = true,
                        AcknowledgeNoHistoricalLotBackfill = true
                    });
                }
                Console.WriteLine("Commercial schema installed on explicit QA database. Database={0}.", args[2]);
                return;
            }

            // A mistyped or incomplete automation request must never fall through to the interactive login.
            if (IsAutomationCommand(args))
            {
                Console.Error.WriteLine("Unsupported automation command or invalid arguments.");
                throw new ArgumentException("Unsupported automation command or invalid arguments.", "args");
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            // تهيئة التصميم قبل أي شاشة: وضع الواجهة، ثم الخط، ثم محرك الواجهات.
            string sessionFontFamily = UiTheme.LoadFontFamily();
            UiTheme.LoadMode();
            AppFonts.Initialize(sessionFontFamily);

            if (databaseSettingsOnly)
            {
                if (!LocalAdministratorAccess.IsCurrentUserAdministrator())
                {
                    AppNotifications.Show(
                        "يجب تشغيل إعدادات قاعدة البيانات بصلاحية مسؤول Windows.",
                        "إعدادات قاعدة البيانات",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                using (DatabaseSetingsForm settingsForm = new DatabaseSetingsForm())
                {
                    Application.Run(settingsForm);
                }
                return;
            }

            Application.Run(new Login());
        }

        internal static void VerifyExcelRoundtripValues(DataTable source, DataTable restored, string sourceKey)
        {
            if (source == null || restored == null) throw new ArgumentNullException("table");
            if (restored.Rows.Count != source.Rows.Count || restored.Columns.Count != source.Columns.Count)
                throw new InvalidOperationException("Excel table size changed: " + sourceKey);
            for (int column = 0; column < source.Columns.Count; column++)
            {
                if (restored.Columns[column].ColumnName != source.Columns[column].ColumnName)
                    throw new InvalidOperationException("Excel column title changed: " + sourceKey + ", column " + (column + 1));
                for (int row = 0; row < source.Rows.Count; row++)
                {
                    object value = source.Rows[row][column];
                    string expected = value == DBNull.Value ? string.Empty : value is DateTime
                        ? ((DateTime)value).ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture)
                        : Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
                    string actual = Convert.ToString(restored.Rows[row][column], System.Globalization.CultureInfo.InvariantCulture);
                    if (!string.Equals(actual, expected, StringComparison.Ordinal))
                        throw new InvalidOperationException("Excel cell value changed: " + sourceKey + ", row " + (row + 1) + ", column " + (column + 1));
                }
            }
        }

        private static bool IsAutomationCommand(string[] args)
        {
            if (args == null || args.Length == 0 || string.IsNullOrWhiteSpace(args[0]))
            {
                return false;
            }

            string command = args[0];
            return command.StartsWith("--verify-", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-excel-import-errors-ui-english", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-validation-notification-english", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-validation-notification-english-dark", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command,"--owned-voucher-editors-qa",StringComparison.OrdinalIgnoreCase)
                || string.Equals(command,"--owned-voucher-editors-reader",StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--qa-live-inventory-report-ui", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--set-ui-font", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-purchases-ui", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-purchases-ui-light", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-purchases-ui-dark", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-purchases-ui-english", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-purchases-ui-english-dark", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-login-ui", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-login-ui-dark", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-login-ui-english", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-login-ui-english-dark", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-dashboard-ui", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-dashboard-ui-english", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-inventory-operation-ui", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-inventory-operation-ui-english", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-inventory-movement-ui", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-inventory-movement-ui-english", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-inventory-stock-ui", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-inventory-stock-ui-english", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-inventory-stock-ui-dark", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-inventory-stock-ui-english-dark", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-inventory-lots-ui", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-inventory-lots-ui-english", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-inventory-lots-ui-dark", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-inventory-lots-ui-english-dark", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-inventory-summary-ui", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-inventory-summary-ui-english", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-inventory-turnover-ui", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-inventory-turnover-ui-english", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-medicine-ui", StringComparison.OrdinalIgnoreCase)
            || string.Equals(command, "--preview-medicine-ui-dark", StringComparison.OrdinalIgnoreCase)
            || string.Equals(command, "--preview-medicine-ui-english", StringComparison.OrdinalIgnoreCase)
            || string.Equals(command, "--preview-medicine-ui-english-dark", StringComparison.OrdinalIgnoreCase)
            || string.Equals(command, "--preview-quick-search-ui", StringComparison.OrdinalIgnoreCase)
            || string.Equals(command, "--preview-quick-search-ui-dark", StringComparison.OrdinalIgnoreCase)
            || string.Equals(command, "--preview-quick-search-ui-english", StringComparison.OrdinalIgnoreCase)
            || string.Equals(command, "--preview-quick-search-ui-english-dark", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-agent-ui", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-agent-ui-dark", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-agent-ui-english", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-agent-ui-english-dark", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-manufacturer-ui", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-manufacturer-ui-dark", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-manufacturer-ui-english", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-manufacturer-ui-english-dark", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-customer-management-ui", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-customer-management-ui-dark", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-customer-management-ui-english", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-customer-management-ui-english-dark", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-commercial-return-ui", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-commercial-return-ui-dark", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-commercial-return-ui-english", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-commercial-return-ui-english-dark", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-return-history-ui", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-return-history-ui-dark", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-return-history-ui-english", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-return-history-ui-english-dark", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-quarantine-resolution-ui", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-quarantine-resolution-ui-dark", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-quarantine-resolution-ui-english", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-quarantine-resolution-ui-english-dark", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-accounting-period-ui-english", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-accounting-period-ui-english-dark", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-billing-ui", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-billing-ui-dark", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-billing-ui-english", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-billing-ui-english-dark", StringComparison.OrdinalIgnoreCase)
                  || string.Equals(command, "--preview-voucher-ui", StringComparison.OrdinalIgnoreCase)
                  || string.Equals(command, "--preview-voucher-ui-dark", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-database-settings-ui", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-database-settings-ui-dark", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-database-settings-ui-english", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-database-settings-ui-english-dark", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-currency-settings", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-currency-settings-dark", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-currency-settings-english", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-currency-settings-english-dark", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-accounting-period-ui", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-accounting-period-ui-dark", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-period-readiness-ui", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-period-readiness-ui-dark", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-period-readiness-ui-english", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-period-readiness-ui-english-dark", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-accounting-integrity-ui", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-accounting-integrity-ui-dark", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-accounting-integrity-ui-english", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-accounting-integrity-ui-english-dark", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-inventory-integrity-ui", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-inventory-integrity-ui-dark", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-inventory-integrity-ui-english", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-inventory-integrity-ui-english-dark", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-excel-export-ui", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-excel-export-ui-dark", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-excel-export-ui-english", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-excel-export-ui-english-dark", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-cashboxes-ui", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-cashboxes-ui-dark", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-cashboxes-ui-english", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-cashboxes-ui-english-dark", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-medicine-catalog-import-ui", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-medicine-catalog-import-ui-dark", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-medicine-catalog-import-ui-english", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-medicine-catalog-import-ui-english-dark", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-medicine-catalog-errors-ui", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-medicine-catalog-errors-ui-dark", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-medicine-catalog-errors-ui-english", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-medicine-catalog-errors-ui-english-dark", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-report-print-options-ui", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-report-print-options-ui-dark", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-report-print-options-ui-english", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-report-print-options-ui-english-dark", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-report-columns-ui", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-report-columns-ui-dark", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-report-columns-ui-english", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-report-columns-ui-english-dark", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-report-template-name-ui", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-report-template-name-ui-dark", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-report-template-name-ui-english", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-report-template-name-ui-english-dark", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-operational-report-ui", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-operational-report-ui-dark", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-operational-report-ui-english", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--preview-operational-report-ui-english-dark", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--report-runtime-connection", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--initialize-core-schema", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--install-commercial-schema", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "--install-commercial-schema-qa", StringComparison.OrdinalIgnoreCase);
        }

        private static void VerifyQaReadOnlyGuards()
        {
            const string productionLikeName = "PharmacyManagementDb";
            RequireQaGuard(() => Database.ValidateQaCoreSchemaReadOnly(".", productionLikeName));
            RequireQaGuard(() => AccountingService.GetQaJournalIntegrityIssuesReadOnly(".", productionLikeName, DateTime.Today.AddDays(-1), DateTime.Today));
            RequireQaGuard(() => InventoryService.GetQaMovementIntegrityIssuesReadOnly(".", productionLikeName));
            RequireQaGuard(() => AccountingService.GetQaPeriodCloseReadinessReadOnly(".", productionLikeName, DateTime.Today.AddDays(-1), DateTime.Today));
            RequireQaGuard(() => OperationalReportCatalog.LoadQaReadOnly(".", productionLikeName, "Medicines", DateTime.Today.AddDays(-1), DateTime.Today, string.Empty));
            RequireQaGuard(() => InventoryService.GetLotStockSnapshotQaReadOnly(".", productionLikeName, string.Empty, true));
            RequireQaGuard(() => InventoryService.VerifyQaLotStockRollback(".", productionLikeName));
        }

        private static void RequireQaGuard(Action action)
        {
            try
            {
                action();
            }
            catch (UnauthorizedAccessException)
            {
                return;
            }

            throw new InvalidOperationException("فحص QA المباشر قبل اسم قاعدة إنتاجي؛ يجب رفضه قبل إنشاء اتصال.");
        }
    }

    // معاينة UIA داخلية؛ تجاوز OnLoad يمنع Purchases_Load من قراءة بيانات قاعدة البيانات.
    internal sealed class PurchasesUiAutomationPreview : Purchases
    {
        internal PurchasesUiAutomationPreview(bool english) : base(true, english) { }
    }

    // معاينة محرر الأدوية دون تحميل سجلات SQL عند فتح الشاشة.
    internal sealed class MedicineUiAutomationPreview : Medicine
    {
        public MedicineUiAutomationPreview(bool english) : base(true, english)
        {
        }
    }

    // معاينة كلمة مرور وهمية لا تحمل حسابًا فعليًا ولا تحمل الموظفين من SQL.
    internal sealed class AgentUiAutomationPreview : Agent
    {
        public AgentUiAutomationPreview(bool english) : base(true, english)
        {
            Control[] password = Controls.Find("tb6", true);
            if (password.Length == 1) password[0].Text = "Password-Check-123";
        }
    }

    // معاينة نقطة البيع دون تنفيذ حدث التحميل المرتبط بقراءات SQL.
    internal sealed class BillingUiAutomationPreview : Billing
    {
        internal BillingUiAutomationPreview(bool english) : base(true, english) { }
        protected override void OnLoad(EventArgs e)
        {
            // لا تستدعِ base.OnLoad كي يبقى فحص التخطيط والتحرير محليًا فقط.
        }
    }
}











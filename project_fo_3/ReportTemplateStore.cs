// قوالب التقارير تحفظ محلياً لكل مستخدم؛ لا تحتوي SQL ولا تنفذ أي استعلام حر.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace project_fo_3
{
    [Serializable]
    public sealed class ReportTemplate
    {
        public string TemplateId { get; set; }
        public string Name { get; set; }
        public string ReportKey { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public string CostCenter { get; set; }
        public string Branch { get; set; }
        public string Level { get; set; }
        public string ValuationMethod { get; set; }
        public bool IncludePreviousBalance { get; set; }
        public bool ExcludeInventoryValue { get; set; }
        public List<string> VisibleColumns { get; set; }
        public List<string> ColumnOrder { get; set; }
        public bool HasPrintSettings { get; set; }
        public string PrintOrientation { get; set; }
        public string PrintDensity { get; set; }
        public bool PrintCompanyName { get; set; }
        public bool PrintLogo { get; set; }
        public bool PrintTaxNumber { get; set; }
        public bool PrintCommercialRegistration { get; set; }
        public bool PrintMobile { get; set; }
        public bool PrintAddress { get; set; }
        public bool PrintWebsite { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
    }

    internal static class ReportTemplateStore
    {
        private static readonly string[] AllowedReportKeys = { "BalanceSheet", "ProfitLoss", "TrialBalance", "GeneralJournal", "Tax", "CostCenters" };
        private const int MaxTemplateNameLength = 60;

        internal static IReadOnlyList<ReportTemplate> Load()
        {
            return ReadAll(StoragePath, true);
        }

        private static IReadOnlyList<ReportTemplate> ReadAll(string path, bool logFailures)
        {
            List<ReportTemplate> templates;
            return TryReadAll(path, logFailures, out templates) ? (IReadOnlyList<ReportTemplate>)templates : Array.Empty<ReportTemplate>();
        }

        // الفشل في القراءة ليس معناه عدم وجود قوالب؛ يُمنع الحفظ فوق ملف غير قابل للقراءة.
        private static bool TryReadAll(string path, bool logFailures, out List<ReportTemplate> templates)
        {
            bool recoveredFromBackup;
            return TryReadAll(path, logFailures, out templates, out recoveredFromBackup);
        }

        private static bool TryReadAll(string path, bool logFailures, out List<ReportTemplate> templates,
            out bool recoveredFromBackup, bool forWriting = false)
        {
            templates = new List<ReportTemplate>();
            recoveredFromBackup = false;
            if (!File.Exists(path) && !File.Exists(path + ".bak")) return true;
            foreach (string candidate in new[] { path, path + ".bak" })
            {
                try
                {
                    if (!File.Exists(candidate)) continue;
                    using (FileStream stream = File.OpenRead(candidate))
                    {
                        List<ReportTemplate> deserialized =
                            new XmlSerializer(typeof(List<ReportTemplate>)).Deserialize(stream) as List<ReportTemplate>;
                        if (deserialized == null) throw new InvalidDataException("Invalid report template list.");
                        templates = deserialized.Where(IsValid)
                            .OrderBy(template => template.Name, StringComparer.CurrentCultureIgnoreCase)
                            .ToList();
                        recoveredFromBackup = !string.Equals(candidate, path, StringComparison.OrdinalIgnoreCase);
                        return true;
                    }
                }
                catch (Exception exception)
                {
                    if (logFailures) AppDiagnostics.Log("ReportTemplateLoad", exception);
                    if (forWriting && string.Equals(candidate,path,StringComparison.OrdinalIgnoreCase) &&
                        ReportTemplateFileCommit.IsUnavailableRead(exception)) return false;
                }
            }
            return false;
        }

        internal static bool Save(ReportTemplate template)
        {
            return SaveAtPath(StoragePath, template);
        }

        internal static bool SaveAtPath(string path, ReportTemplate template)
        {
            ReportTemplate normalized = Normalize(template);
            if (normalized == null)
            {
                return false;
            }

            try
            {
                path=Path.GetFullPath(path);
                using(var writer=ReportTemplateFileCommit.AcquireWriter(path))
                {
                List<ReportTemplate> templates;
                bool recoveredFromBackup;
                if (!TryReadAll(path, true, out templates, out recoveredFromBackup, true)) return false;
                int existingIndex = templates.FindIndex(item => string.Equals(item.TemplateId, normalized.TemplateId, StringComparison.OrdinalIgnoreCase));
                if (existingIndex < 0)
                {
                    existingIndex = templates.FindIndex(item => string.Equals(item.Name, normalized.Name, StringComparison.CurrentCultureIgnoreCase)
                        && string.Equals(item.ReportKey, normalized.ReportKey, StringComparison.Ordinal));
                }

                if (existingIndex >= 0)
                {
                    normalized.TemplateId = templates[existingIndex].TemplateId;
                    templates[existingIndex] = normalized;
                }
                else
                {
                    templates.Add(normalized);
                }

                WriteAll(path, templates, recoveredFromBackup);
                return true;
                }
            }
            catch (Exception exception)
            {
                AppDiagnostics.Log("ReportTemplateSave", exception);
                return false;
            }
        }

        internal static bool Delete(string templateId)
        {
            return DeleteAtPath(StoragePath, templateId);
        }

        internal static bool DeleteAtPath(string path, string templateId)
        {
            if (string.IsNullOrWhiteSpace(templateId))
            {
                return false;
            }

            try
            {
                path=Path.GetFullPath(path);
                using(var writer=ReportTemplateFileCommit.AcquireWriter(path))
                {
                List<ReportTemplate> templates;
                bool recoveredFromBackup;
                if (!TryReadAll(path, true, out templates, out recoveredFromBackup, true)) return false;
                templates = templates.Where(item => !string.Equals(item.TemplateId, templateId, StringComparison.OrdinalIgnoreCase)).ToList();
                WriteAll(path, templates, recoveredFromBackup);
                return true;
                }
            }
            catch (Exception exception)
            {
                AppDiagnostics.Log("ReportTemplateDelete", exception);
                return false;
            }
        }

        internal static bool IsAllowedReportKey(string reportKey)
        {
            return AllowedReportKeys.Contains(reportKey, StringComparer.Ordinal);
        }

        internal static ReportTemplate Normalize(ReportTemplate template)
        {
            if (template == null || !IsAllowedReportKey(template.ReportKey))
            {
                return null;
            }

            string name = (template.Name ?? string.Empty).Trim();
            if (name.Length == 0 || name.Length > MaxTemplateNameLength || template.ToDate.Date < template.FromDate.Date)
            {
                return null;
            }

            return new ReportTemplate
            {
                TemplateId = Guid.TryParse(template.TemplateId, out Guid parsedId) ? parsedId.ToString("N") : Guid.NewGuid().ToString("N"),
                Name = name,
                ReportKey = template.ReportKey,
                FromDate = template.FromDate.Date,
                ToDate = template.ToDate.Date,
                CostCenter = Limit(template.CostCenter, 120),
                Branch = Limit(template.Branch, 120),
                Level = Limit(template.Level, 20),
                ValuationMethod = Limit(template.ValuationMethod, 80),
                IncludePreviousBalance = template.IncludePreviousBalance,
                ExcludeInventoryValue = template.ExcludeInventoryValue,
                VisibleColumns = NormalizeColumns(template.VisibleColumns),
                ColumnOrder = NormalizeColumns(template.ColumnOrder),
                HasPrintSettings = template.HasPrintSettings,
                PrintOrientation = Enum.TryParse(template.PrintOrientation, out ReportPageOrientation orientation) &&
                    Enum.IsDefined(typeof(ReportPageOrientation), orientation) ? orientation.ToString() : ReportPageOrientation.Automatic.ToString(),
                PrintDensity = Enum.TryParse(template.PrintDensity, out ReportDensity density) &&
                    Enum.IsDefined(typeof(ReportDensity), density) ? density.ToString() : ReportDensity.Standard.ToString(),
                PrintCompanyName = template.PrintCompanyName, PrintLogo = template.PrintLogo,
                PrintTaxNumber = template.PrintTaxNumber, PrintCommercialRegistration = template.PrintCommercialRegistration,
                PrintMobile = template.PrintMobile, PrintAddress = template.PrintAddress, PrintWebsite = template.PrintWebsite,
                UpdatedAtUtc = DateTime.UtcNow
            };
        }

        private static List<string> NormalizeColumns(IEnumerable<string> values)
        {
            return (values ?? Enumerable.Empty<string>())
                .Select(value => Limit(value, 120))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .Take(40)
                .ToList();
        }

        private static void WriteAll(string path, List<ReportTemplate> templates, bool preserveBackup = false)
        {
            ReportTemplateFileCommit.WriteAll(path,templates,preserveBackup);
        }

        internal static IReadOnlyList<ReportTemplate> ReadForTest(string path)
        {
            return ReadAll(path, false);
        }

        internal static bool CanReadForTest(string path, bool forWriting = false)
        {
            List<ReportTemplate> templates;
            bool recovered;
            return TryReadAll(path, false, out templates, out recovered, forWriting);
        }

        internal static void WriteForTest(string path, List<ReportTemplate> templates)
        {
            WriteAll(path, templates);
        }

        private static bool IsValid(ReportTemplate template)
        {
            return Normalize(template) != null;
        }

        private static string Limit(string value, int length)
        {
            string normalized = (value ?? string.Empty).Trim();
            return normalized.Length <= length ? normalized : normalized.Substring(0, length);
        }

        private static string StoragePath
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PharmacyManagement", "report-templates.xml"); }
        }
    }

    internal static class ReportTemplateStoreSelfTests
    {
        internal static void RunAll()
        {
            Require(ReportTemplateStore.IsAllowedReportKey("TrialBalance"), "تقرير ميزان المراجعة يجب أن يكون مسموحاً.");
            Require(!ReportTemplateStore.IsAllowedReportKey("SELECT * FROM dbo.Users"), "لا يجب قبول مفاتيح تقارير حرة.");
            ReportTemplate normalized = ReportTemplateStore.Normalize(new ReportTemplate { Name = "  التقرير الشهري  ", ReportKey = "Tax", FromDate = new DateTime(2026, 1, 1), ToDate = new DateTime(2026, 1, 31) });
            Require(normalized != null && normalized.Name == "التقرير الشهري" && normalized.TemplateId.Length == 32, "تطبيع القالب غير صحيح.");
            normalized.VisibleColumns = new List<string> { "الحساب", "مدين" };
            normalized.ColumnOrder = new List<string> { "مدين", "الحساب" };
            normalized.HasPrintSettings = true;
            normalized.PrintOrientation = ReportPageOrientation.Portrait.ToString();
            normalized.PrintDensity = ReportDensity.Compact.ToString();
            normalized.PrintCompanyName = true;
            normalized.PrintTaxNumber = true;
            Require(ReportTemplateStore.Normalize(new ReportTemplate { Name = "غير صالح", ReportKey = "Tax", FromDate = new DateTime(2026, 2, 1), ToDate = new DateTime(2026, 1, 1) }) == null, "لا يجب حفظ فترة تاريخ غير صالحة.");

            string temporaryPath = Path.Combine(Path.GetTempPath(), "pharmacy-report-template-test-" + Guid.NewGuid().ToString("N") + ".xml");
            try
            {
                ReportTemplateStore.WriteForTest(temporaryPath, new List<ReportTemplate> { normalized });
                IReadOnlyList<ReportTemplate> loaded = ReportTemplateStore.ReadForTest(temporaryPath);
                Require(loaded.Count == 1 && loaded[0].Name == "التقرير الشهري" && loaded[0].ReportKey == "Tax" &&
                    loaded[0].VisibleColumns.SequenceEqual(new[] { "الحساب", "مدين" }) &&
                    loaded[0].ColumnOrder.SequenceEqual(new[] { "مدين", "الحساب" }) &&
                    loaded[0].HasPrintSettings && loaded[0].PrintOrientation == "Portrait" &&
                    loaded[0].PrintDensity == "Compact" && loaded[0].PrintCompanyName && loaded[0].PrintTaxNumber,
                    "فشل حفظ أو استعادة قالب التقرير وخيارات الطباعة.");

                ReportTemplate updated = ReportTemplateStore.Normalize(new ReportTemplate { TemplateId = normalized.TemplateId, Name = "التقرير الشهري المحدث", ReportKey = "Tax", FromDate = new DateTime(2026, 1, 1), ToDate = new DateTime(2026, 2, 28) });
                ReportTemplateStore.WriteForTest(temporaryPath, new List<ReportTemplate> { updated });
                loaded = ReportTemplateStore.ReadForTest(temporaryPath);
                Require(loaded.Count == 1 && loaded[0].Name == "التقرير الشهري المحدث" && loaded[0].ToDate == new DateTime(2026, 2, 28), "فشل الاستبدال الآمن لقالب التقرير.");

                File.WriteAllText(temporaryPath, "<invalid-template");
                Require(ReportTemplateStore.CanReadForTest(temporaryPath), "يجب استعادة النسخة السليمة عند تلف ملف القوالب الأساسي.");
                loaded = ReportTemplateStore.ReadForTest(temporaryPath);
                Require(loaded.Count == 1 && loaded[0].Name == "التقرير الشهري",
                    "الاستعادة يجب أن تستخدم القالب السابق لا قائمة فارغة أو الملف التالف.");
                ReportTemplate another = new ReportTemplate { Name = "تقرير إضافي", ReportKey = "Tax",
                    FromDate = new DateTime(2026, 3, 1), ToDate = new DateTime(2026, 3, 31) };
                Require(ReportTemplateStore.SaveAtPath(temporaryPath, another),
                    "تعذر حفظ قالب جديد بعد استعادة النسخة السابقة.");
                loaded = ReportTemplateStore.ReadForTest(temporaryPath);
                Require(loaded.Count == 2 && loaded.Any(item => item.Name == "التقرير الشهري") &&
                    loaded.Any(item => item.Name == "تقرير إضافي"),
                    "الحفظ بعد الاستعادة أسقط قالبًا سابقًا.");
                IReadOnlyList<ReportTemplate> backup = ReportTemplateStore.ReadForTest(temporaryPath + ".bak");
                Require(backup.Count == 1 && backup[0].Name == "التقرير الشهري",
                    "الحفظ بعد الاستعادة استبدل النسخة السليمة بالملف التالف.");

                File.WriteAllText(temporaryPath, "<invalid-template");
                Require(ReportTemplateStore.DeleteAtPath(temporaryPath, normalized.TemplateId),
                    "تعذر حذف قالب بعد استعادة النسخة السابقة.");
                Require(ReportTemplateStore.ReadForTest(temporaryPath).Count == 0 &&
                    ReportTemplateStore.ReadForTest(temporaryPath + ".bak").Count == 1,
                    "الحذف بعد الاستعادة لم يحفظ النتيجة أو أفسد النسخة السليمة.");

                File.Delete(temporaryPath + ".bak");
                File.WriteAllText(temporaryPath, "<invalid-template");
                Require(!ReportTemplateStore.CanReadForTest(temporaryPath) &&
                    !ReportTemplateStore.SaveAtPath(temporaryPath, another),
                    "يجب رفض حفظ القالب حين يتلف الملف الأساسي ولا توجد نسخة سليمة.");
                Require(File.Exists(temporaryPath), "لا يجوز حذف ملف القالب التالف أثناء محاولة القراءة.");
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
                if (File.Exists(temporaryPath + ".tmp")) File.Delete(temporaryPath + ".tmp");
                if (File.Exists(temporaryPath + ".bak")) File.Delete(temporaryPath + ".bak");
                if (File.Exists(temporaryPath + ".lock")) File.Delete(temporaryPath + ".lock");
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}

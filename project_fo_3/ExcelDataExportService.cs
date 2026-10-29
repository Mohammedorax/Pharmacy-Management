using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using PharmacyManagement.Application;

namespace project_fo_3
{
    // Keeps export authorization, scope and file integrity outside the desktop form.
    internal static class ExcelDataExportService
    {
        private static readonly string[] ApprovedSourceKeys =
            { "Medicines", "SalesLines", "LegacySalesLines", "PurchaseLines", "InventoryMovements", "JournalLines", "CashLedger", "CashBoxes" };

        internal static IReadOnlyList<OperationalReportSource> GetSources()
        {
            return OperationalReportCatalog.GetSources()
                .Where(item => ApprovedSourceKeys.Contains(item.Key, StringComparer.Ordinal))
                .Concat(new[] { new OperationalReportSource("CashBoxes", "تعريفات صناديق النقد", false) }).ToArray();
        }

        internal static string GetSheetName(string sourceKey)
        {
            switch (sourceKey)
            {
                case "Medicines": return "الأدوية والأرصدة الحالية";
                case "SalesLines": return "سطور فواتير البيع";
                case "LegacySalesLines": return "سطور البيع القديمة (دون تاريخ محفوظ)";
                case "PurchaseLines": return "سطور فواتير المشتريات";
                case "InventoryMovements": return "حركات المخزون";
                case "JournalLines": return "سطور القيود المحاسبية";
                case "CashLedger": return "حركات النقد المشترك";
                case "CashBoxes": return "تعريفات صناديق النقد";
                default: throw new InvalidOperationException("نوع بيانات التصدير غير معتمد.");
            }
        }

        internal static int Export(string sourceKey, DateTime fromDate, DateTime toDate, string searchText, string target)
        {
            ValidateRequest(sourceKey, fromDate, toDate, target, searchText);
            string filter = ReportSearchCriteria.Normalize(searchText);
            return WriteSource(sourceKey, target, () =>
            {
                if (sourceKey == "CashBoxes")
                {
                    var actor = new OperationActor
                    {
                        UserName = SessionContext.CurrentUser,
                        UserRole = SessionContext.CurrentRole
                    };
                    return CreateCashBoxTable(ApplicationServices.CashBoxes.GetAll(actor), filter);
                }
                return OperationalReportCatalog.Load(sourceKey, fromDate, toDate, filter);
            });
        }

        // Exercises the same validation and file-writing path against an explicit QA
        // connection without changing the application's saved production connection.
        internal static int ExportQaReadOnly(string serverName, string databaseName, string sourceKey,
            DateTime fromDate, DateTime toDate, string searchText, string target)
        {
            if (string.IsNullOrWhiteSpace(databaseName) ||
                !databaseName.Trim().EndsWith("_qa", StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("تصدير QA المباشر مسموح فقط لقاعدة ينتهي اسمها بـ _qa.");
            ValidateRequest(sourceKey, fromDate, toDate, target, searchText);
            DatabaseSchemaValidationResult schema = Database.ValidateQaCoreSchemaReadOnly(serverName, databaseName);
            if (!string.Equals(schema.DatabaseName, databaseName.Trim(), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("اتصال تصدير QA وصل إلى قاعدة مختلفة عن المطلوبة.");
            string filter = ReportSearchCriteria.Normalize(searchText);
            return WriteSource(sourceKey, target, () =>
            {
                if (sourceKey == "CashBoxes")
                {
                    var repository = new PharmacyManagement.Infrastructure.SqlServer.SqlCashBoxRepository(() =>
                        new System.Data.SqlClient.SqlConnection(Database.BuildSettingsConnectionString(
                            serverName, databaseName, true)));
                    return CreateCashBoxTable(repository.GetAll(), filter);
                }
                return OperationalReportCatalog.LoadQaReadOnly(serverName, databaseName, sourceKey,
                    fromDate, toDate, filter);
            });
        }

        private static int WriteSource(string sourceKey, string target, Func<DataTable> load)
        {
            using (DataTable table = load())
                return ExcelWorkbookWriter.WriteCompleteFile(table, GetSheetName(sourceKey), target);
        }

        private static DataTable CreateCashBoxTable(IReadOnlyList<CashBoxRecord> boxes, string filter)
        {
            var matched = boxes.Where(box => filter.Length == 0 ||
                (box.Code ?? string.Empty).IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                (box.Name ?? string.Empty).IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                (box.EmployeeId ?? string.Empty).IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
            return CashBoxWorkbook.CreateExport(matched);
        }

        internal static void ValidateRequest(string sourceKey, DateTime fromDate, DateTime toDate, string target, string searchText = null)
        {
            if (!PermissionService.CanOpen(AppPermission.Settings))
                throw new UnauthorizedAccessException("تصدير الإعدادات متاح للمدير فقط.");
            if (!ApprovedSourceKeys.Contains(sourceKey, StringComparer.Ordinal))
                throw new InvalidOperationException("نوع البيانات خارج نطاق التصدير المعتمد.");
            if (!OperationalReportCatalog.IsValidDateRange(fromDate, toDate))
                throw new InvalidOperationException("فترة التصدير غير صحيحة.");
            if (string.IsNullOrWhiteSpace(target) ||
                !string.Equals(Path.GetExtension(target), ".xlsx", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("اختر مسار ملف Excel بامتداد xlsx.");
            ReportSearchCriteria.Normalize(searchText);
        }
    }
}

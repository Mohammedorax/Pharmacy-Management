// مصادر تقارير تشغيلي مقيدة: استعلامات قراءة فقط ومصادر معرفة في الشفرة.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Application;

namespace project_fo_3
{
    internal sealed class OperationalReportReadSession
    {
        private readonly IOperationalReportAsyncReadService service;
        private readonly OperationActor actor;
        private readonly Func<bool> current;
        internal OperationalReportReadSession(IOperationalReportAsyncReadService service,OperationActor actor,Func<bool> current)
        {this.service=service??throw new ArgumentNullException(nameof(service));this.actor=new OperationActor{UserName=actor?.UserName,UserRole=actor?.UserRole};this.current=current??throw new ArgumentNullException(nameof(current));}
        internal bool IsCurrent=>current();
        internal void EnsureContext(){if(!IsCurrent)throw new UnauthorizedAccessException("تغيرت جلسة المستخدم؛ أعد تحميل التقرير.");}
        internal async Task<OperationalReportResult> LoadAsync(OperationalReportRequest request,CancellationToken cancellationToken)
        {cancellationToken.ThrowIfCancellationRequested();EnsureContext();var result=await service.LoadAsync(request,new OperationActor{UserName=actor.UserName,UserRole=actor.UserRole},cancellationToken).ConfigureAwait(false);cancellationToken.ThrowIfCancellationRequested();EnsureContext();return result;}
    }
    internal static class OperationalReportCatalog
    {
        private static readonly OperationalReportSource[] Sources =
        {
            new OperationalReportSource("Medicines", "الأدوية والأرصدة الحالية", false),
            new OperationalReportSource("SalesLines", "المبيعات (مؤرخة عند توفر المخطط التجاري)", false, "سطور فواتير البيع"),
            new OperationalReportSource("LegacySalesLines", "سطور البيع القديمة (دون تاريخ محفوظ)", false, "البيع القديم (بلا تاريخ)"),
            new OperationalReportSource("Purchases", "فواتير المشتريات", true),
            new OperationalReportSource("PurchaseLines", "سطور فواتير المشتريات", true),
            new OperationalReportSource("InventoryMovements", "حركات المخزون", true),
            new OperationalReportSource("JournalLines", "سطور اليومية العامة", true),
            new OperationalReportSource("CashLedger", "حركات النقد المشترك", true),
            new OperationalReportSource("DailyPerformance", "الأداء المالي اليومي حسب العملة", true, "المالية حسب العملة"),
            new OperationalReportSource("DailyInventoryActivity", "ملخص حركة المخزون اليومي", true, "المخزون اليومي"),
            new OperationalReportSource("FastSellingToday", "الأصناف الأعلى صافي مبيعًا اليوم", false, "صافي الكمية اليوم"),
            new OperationalReportSource("FastSelling30Days", "الأصناف الأعلى صافي مبيعًا خلال آخر 30 يومًا", false, "صافي الكمية — 30 يومًا"),
            new OperationalReportSource("MostMovementsToday", "الأصناف الأكثر حركات مخزون اليوم", false, "حركات المخزون اليوم")
        };

        internal static IReadOnlyList<OperationalReportSource> GetSources() { return Sources; }

        internal static bool IsAllowedSource(string sourceKey)
        {
            return Sources.Any(source => string.Equals(source.Key, sourceKey, StringComparison.Ordinal));
        }

        internal static bool IsFastMovementSource(string key)
        {
            return key == "FastSellingToday" || key == "FastSelling30Days" || key == "MostMovementsToday";
        }

        internal static bool IsValidDateRange(DateTime fromDate, DateTime toDate)
        {
            return fromDate.Date <= toDate.Date;
        }

        internal static DataTable Load(string sourceKey, DateTime fromDate, DateTime toDate, string searchText)
        {
            var request = CreateRequest(sourceKey, fromDate, toDate, searchText);
            return ToDataTable(ApplicationServices.OperationalReports.Load(request));
        }

        internal static DataTable LoadQaReadOnly(string serverName, string databaseName, string sourceKey, DateTime fromDate, DateTime toDate, string searchText)
        {
            if (string.IsNullOrWhiteSpace(databaseName) || !databaseName.Trim().EndsWith("_qa", StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("فحص مصادر التقارير المباشر مسموح فقط لقاعدة ينتهي اسمها بـ _qa.");
            var request = CreateRequest(sourceKey, fromDate, toDate, searchText);
            var service = ApplicationServices.CreateQaOperationalReportService(serverName, databaseName);
            return ToDataTable(service.Load(request));
        }

        internal static bool SupportsGroupedView(string sourceKey)
        {
            return Enum.TryParse(sourceKey, out OperationalReportKind kind) && OperationalReportRequest.SupportsSummary(kind);
        }

        internal static OperationalReportRequest CreateRequest(string sourceKey, DateTime fromDate, DateTime toDate, string searchText)
        { return CreateRequest(sourceKey, fromDate, toDate, searchText, OperationalReportViewMode.Analytical); }

        internal static OperationalReportRequest CreateRequest(string sourceKey, DateTime fromDate, DateTime toDate, string searchText, OperationalReportViewMode viewMode)
        {
            if (!IsAllowedSource(sourceKey)) throw new InvalidOperationException("مصدر التقرير غير مسموح.");
            if (IsFastMovementSource(sourceKey))
            {
                toDate = DateTime.Today;
                fromDate = sourceKey == "FastSelling30Days" ? toDate.AddDays(-29) : toDate;
            }
            return new OperationalReportRequest((OperationalReportKind)Enum.Parse(typeof(OperationalReportKind), sourceKey),
                fromDate, toDate, searchText, viewMode);
        }

        internal static DataTable ToDataTable(OperationalReportResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            var table = new DataTable();
            try
            {
                foreach (OperationalReportColumn column in result.Columns) table.Columns.Add(column.Name, column.DataType);
                foreach (IReadOnlyList<object> row in result.Rows)
                    table.Rows.Add(row.Select(value => value ?? DBNull.Value).ToArray());
                table.AcceptChanges();
                return table;
            }
            catch { table.Dispose(); throw; }
        }
    }
}


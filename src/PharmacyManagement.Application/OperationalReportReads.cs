using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PharmacyManagement.Application
{
    public enum OperationalReportKind
    {
        Medicines, SalesLines, Purchases, PurchaseLines, InventoryMovements,
        JournalLines, CashLedger, DailyPerformance, LegacySalesLines,
        FastSellingToday, FastSelling30Days, MostMovementsToday, DailyInventoryActivity
    }

    public enum OperationalReportViewMode { Analytical, Summary }

    public sealed class OperationalReportRequest
    {
        public const int MaximumRows = 2000;
        public OperationalReportKind Kind { get; private set; }
        public DateTime FromDate { get; private set; }
        public DateTime ToDateExclusive { get; private set; }
        public string SearchText { get; private set; }
        public OperationalReportViewMode ViewMode { get; private set; }

        public static bool SupportsSummary(OperationalReportKind kind)
        {
            return kind == OperationalReportKind.JournalLines || kind == OperationalReportKind.CashLedger ||
                kind == OperationalReportKind.InventoryMovements || kind == OperationalReportKind.SalesLines ||
                kind == OperationalReportKind.LegacySalesLines || kind == OperationalReportKind.Purchases || kind == OperationalReportKind.PurchaseLines;
        }

        public OperationalReportRequest(OperationalReportKind kind, DateTime fromDate, DateTime toDate, string searchText)
            : this(kind, fromDate, toDate, searchText, OperationalReportViewMode.Analytical) { }

        public OperationalReportRequest(OperationalReportKind kind, DateTime fromDate, DateTime toDate, string searchText, OperationalReportViewMode viewMode)
        {
            if (!Enum.IsDefined(typeof(OperationalReportKind), kind)) throw new InvalidOperationException("مصدر التقرير غير مسموح.");
            if (fromDate.Date < new DateTime(1753, 1, 1) || toDate.Date >= DateTime.MaxValue.Date || fromDate.Date > toDate.Date)
                throw new InvalidOperationException("فترة التقرير غير صحيحة.");
            if (!Enum.IsDefined(typeof(OperationalReportViewMode), viewMode)) throw new InvalidOperationException("طريقة عرض التقرير غير مسموحة.");
            if (viewMode == OperationalReportViewMode.Summary && !SupportsSummary(kind)) throw new InvalidOperationException("هذا المصدر يعرض قائمة أو مؤشرات مجمعة أصلًا؛ اختر مصدر حركات للتقرير المختصر.");
            ViewMode = viewMode;
            Kind = kind;
            FromDate = fromDate.Date;
            ToDateExclusive = toDate.Date.AddDays(1);
            SearchText = ReportSearchCriteria.Normalize(searchText);
        }
    }

    public sealed class OperationalReportColumn
    {
        public string Name { get; private set; }
        public Type DataType { get; private set; }
        public OperationalReportColumn(string name, Type dataType)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("Column name is required.", nameof(name));
            Name = name;
            DataType = dataType ?? throw new ArgumentNullException(nameof(dataType));
        }
    }

    public sealed class OperationalReportResult
    {
        public IReadOnlyList<OperationalReportColumn> Columns { get; private set; }
        public IReadOnlyList<IReadOnlyList<object>> Rows { get; private set; }
        public OperationalReportResult(IEnumerable<OperationalReportColumn> columns, IEnumerable<object[]> rows)
        {
            var schema = (columns ?? throw new ArgumentNullException(nameof(columns))).ToArray();
            if (schema.Length == 0 || schema.Any(column => column == null) ||
                schema.Select(column => column.Name).Distinct(StringComparer.Ordinal).Count() != schema.Length)
                throw new ArgumentException("Report schema must have unique named columns.", nameof(columns));
            var values = new List<IReadOnlyList<object>>();
            foreach (object[] row in rows ?? throw new ArgumentNullException(nameof(rows)))
            {
                if (row == null || row.Length != schema.Length) throw new ArgumentException("Report row does not match its schema.", nameof(rows));
                values.Add(Array.AsReadOnly((object[])row.Clone()));
            }
            Columns = Array.AsReadOnly(schema);
            Rows = values.AsReadOnly();
        }
    }

    public interface IOperationalReportRepository
    {
        OperationalReportResult Load(OperationalReportRequest request);
    }
    public interface IOperationalReportReadService
    {
        OperationalReportResult Load(OperationalReportRequest request);
    }
    public interface IOperationalReportAsyncRepository : IOperationalReportRepository
    {
        Task<OperationalReportResult> LoadAsync(OperationalReportRequest request,CancellationToken cancellationToken);
    }
    public interface IOperationalReportAsyncReadService
    {
        Task<OperationalReportResult> LoadAsync(OperationalReportRequest request,OperationActor actor,CancellationToken cancellationToken);
    }
    public sealed class AuthorizedOperationalReportReadService : IOperationalReportAsyncReadService
    {
        private readonly IOperationalReportAsyncRepository repository;
        private readonly Func<OperationActor,bool> allowed;
        public AuthorizedOperationalReportReadService(IOperationalReportAsyncRepository repository,Func<OperationActor,bool> allowed)
        {this.repository=repository??throw new ArgumentNullException(nameof(repository));this.allowed=allowed??throw new ArgumentNullException(nameof(allowed));}
        public async Task<OperationalReportResult> LoadAsync(OperationalReportRequest request,OperationActor actor,CancellationToken cancellationToken)
        {
            if(request==null)throw new ArgumentNullException(nameof(request));cancellationToken.ThrowIfCancellationRequested();
            var captured=new OperationActor{UserName=actor?.UserName,UserRole=actor?.UserRole};Authorize(captured);
            var result=await repository.LoadAsync(request,cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();Authorize(captured);return result;
        }
        private void Authorize(OperationActor actor)
        {if(string.IsNullOrWhiteSpace(actor.UserName)||string.IsNullOrWhiteSpace(actor.UserRole)||!allowed(actor))throw new UnauthorizedAccessException("تغيرت جلسة المستخدم؛ أعد تحميل التقرير.");}
    }
    public sealed class OperationalReportReadService : IOperationalReportReadService
    {
        private readonly IOperationalReportRepository repository;
        private readonly Func<bool> canRead;
        public OperationalReportReadService(IOperationalReportRepository repository) : this(repository, null) { }
        public OperationalReportReadService(IOperationalReportRepository repository, Func<bool> canRead)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
            this.canRead = canRead ?? (() => false);
        }
        private void Authorize()
        {
            if (!canRead()) throw new UnauthorizedAccessException("لا تملك صلاحية قراءة التقارير أو تغيرت جلسة المستخدم؛ أعد تحميل التقرير.");
        }
        public OperationalReportResult Load(OperationalReportRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            Authorize();
            var result = repository.Load(request);
            Authorize();
            return result;
        }
    }
}
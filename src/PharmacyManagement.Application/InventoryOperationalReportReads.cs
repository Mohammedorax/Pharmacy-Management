using System;
using System.Threading;
using System.Threading.Tasks;

namespace PharmacyManagement.Application
{
    public sealed class InventoryMovementSummaryRequest
    {
        public DateTime FromDate { get; }
        public DateTime ToDate { get; }
        public string SearchText { get; }

        public InventoryMovementSummaryRequest(DateTime fromDate, DateTime toDate, string searchText)
        {
            ValidateDates(fromDate, toDate);
            FromDate = fromDate.Date;
            ToDate = toDate.Date;
            SearchText = string.IsNullOrWhiteSpace(searchText) ? string.Empty : searchText.Trim();
            if (SearchText.Length > 200) throw new InvalidOperationException("نص البحث لا يتجاوز 200 حرف.");
        }

        internal static void ValidateDates(DateTime fromDate, DateTime toDate)
        {
            if (fromDate.Date < new DateTime(1753, 1, 1) || toDate.Date >= DateTime.MaxValue.Date || fromDate.Date > toDate.Date)
                throw new InvalidOperationException("فترة التقرير غير صحيحة.");
        }
    }

    public sealed class InventoryTurnoverRequest
    {
        public DateTime FromDate { get; }
        public DateTime ToDate { get; }
        public int IdleDays { get; }

        public InventoryTurnoverRequest(DateTime fromDate, DateTime toDate, int idleDays)
        {
            InventoryMovementSummaryRequest.ValidateDates(fromDate, toDate);
            if (idleDays < 1 || idleDays > 3650) throw new InvalidOperationException("أيام الركود يجب أن تكون بين 1 و3650 يومًا.");
            FromDate = fromDate.Date;
            ToDate = toDate.Date;
            IdleDays = idleDays;
        }
    }

    public interface IInventoryOperationalReportRepository
    {
        Task<OperationalReportResult> LoadMovementSummaryAsync(InventoryMovementSummaryRequest request, CancellationToken cancellation);
        Task<OperationalReportResult> LoadTurnoverAsync(InventoryTurnoverRequest request, CancellationToken cancellation);
    }

    public interface IInventoryOperationalReportReadService
    {
        Task<OperationalReportResult> LoadMovementSummaryAsync(InventoryMovementSummaryRequest request, OperationActor actor, CancellationToken cancellation);
        Task<OperationalReportResult> LoadTurnoverAsync(InventoryTurnoverRequest request, OperationActor actor, CancellationToken cancellation);
    }

    public sealed class InventoryOperationalReportReadService : IInventoryOperationalReportReadService
    {
        private readonly IInventoryOperationalReportRepository repository;
        private readonly Func<OperationActor, bool> canRead;

        public InventoryOperationalReportReadService(IInventoryOperationalReportRepository repository, Func<OperationActor, bool> canRead)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
            this.canRead = canRead ?? throw new ArgumentNullException(nameof(canRead));
        }

        public Task<OperationalReportResult> LoadMovementSummaryAsync(InventoryMovementSummaryRequest request, OperationActor actor, CancellationToken cancellation)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            return LoadAsync(actor, cancellation, token => repository.LoadMovementSummaryAsync(request, token));
        }

        public Task<OperationalReportResult> LoadTurnoverAsync(InventoryTurnoverRequest request, OperationActor actor, CancellationToken cancellation)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            return LoadAsync(actor, cancellation, token => repository.LoadTurnoverAsync(request, token));
        }

        private async Task<OperationalReportResult> LoadAsync(OperationActor actor, CancellationToken cancellation,
            Func<CancellationToken, Task<OperationalReportResult>> read)
        {
            cancellation.ThrowIfCancellationRequested();
            if (actor == null || string.IsNullOrWhiteSpace(actor.UserName) || string.IsNullOrWhiteSpace(actor.UserRole) ||
                actor.UserName.Length > 200 || actor.UserRole.Length > 50)
                throw new UnauthorizedAccessException("لا تملك صلاحية قراءة تقارير المخزون.");
            var captured = new OperationActor { UserName = actor.UserName, UserRole = actor.UserRole };
            if (!canRead(captured)) throw new UnauthorizedAccessException("لا تملك صلاحية قراءة تقارير المخزون.");
            var result = await read(cancellation).ConfigureAwait(false);
            cancellation.ThrowIfCancellationRequested();
            if (!canRead(captured)) throw new UnauthorizedAccessException("تغيرت صلاحية قراءة تقارير المخزون أثناء الطلب.");
            return result ?? throw new InvalidOperationException("لم يرجع مصدر تقرير المخزون نتيجة.");
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PharmacyManagement.Application
{
    public sealed class InventoryAlertRequest
    {
        public int LowStockThreshold { get; private set; }
        public int ExpiringWithinDays { get; private set; }
        public IReadOnlyList<string> AffectedMedicines { get; private set; }

        public InventoryAlertRequest(int lowStockThreshold, int expiringWithinDays, IEnumerable<string> affectedMedicines)
        {
            if (lowStockThreshold < 0)
                throw new InvalidOperationException("حد المخزون المنخفض لا يمكن أن يكون سالبًا.");
            if (expiringWithinDays < 0 || expiringWithinDays > 3650)
                throw new InvalidOperationException("الأيام قبل الانتهاء يجب أن تكون بين 0 و3650 يومًا.");
            LowStockThreshold = lowStockThreshold;
            ExpiringWithinDays = expiringWithinDays;
            AffectedMedicines = affectedMedicines == null ? null :
                Array.AsReadOnly(affectedMedicines.Where(name => !string.IsNullOrWhiteSpace(name))
                    .Distinct(StringComparer.Ordinal).ToArray());
        }
    }

    public interface IInventoryAlertRepository
    {
        OperationalReportResult Load(InventoryAlertRequest request);
    }

    public interface IAsyncInventoryAlertRepository
    {
        Task<OperationalReportResult> LoadAsync(InventoryAlertRequest request, CancellationToken cancellation);
    }

    public interface IInventoryAlertReadService
    {
        OperationalReportResult GetAlerts(int lowStockThreshold, int expiringWithinDays,
            IEnumerable<string> affectedMedicines = null);
    }

    public interface IInventoryAlertSessionReadService
    {
        Task<OperationalReportResult> GetAlertsAsync(InventoryAlertRequest request, OperationActor actor, CancellationToken cancellation);
    }

    public sealed class InventoryAlertReadService : IInventoryAlertReadService, IInventoryAlertSessionReadService
    {
        private readonly IInventoryAlertRepository repository;
        private readonly Func<OperationActor, bool> canRead;
        public InventoryAlertReadService(IInventoryAlertRepository repository)
            : this(repository, null) { }
        public InventoryAlertReadService(IInventoryAlertRepository repository, Func<OperationActor, bool> canRead)
        { this.repository = repository ?? throw new ArgumentNullException(nameof(repository)); this.canRead = canRead; }

        public OperationalReportResult GetAlerts(int lowStockThreshold, int expiringWithinDays,
            IEnumerable<string> affectedMedicines = null)
        { return repository.Load(new InventoryAlertRequest(lowStockThreshold, expiringWithinDays, affectedMedicines)); }

        public async Task<OperationalReportResult> GetAlertsAsync(InventoryAlertRequest request, OperationActor actor, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (actor == null || string.IsNullOrWhiteSpace(actor.UserName) || string.IsNullOrWhiteSpace(actor.UserRole) ||
                actor.UserName.Length > 200 || actor.UserRole.Length > 50 || canRead == null)
                throw new UnauthorizedAccessException("لا تملك صلاحية قراءة تنبيهات المخزون.");
            var captured = new OperationActor { UserName = actor.UserName, UserRole = actor.UserRole };
            if (!canRead(captured)) throw new UnauthorizedAccessException("لا تملك صلاحية قراءة تنبيهات المخزون.");
            var asyncRepository = repository as IAsyncInventoryAlertRepository;
            if (asyncRepository == null) throw new InvalidOperationException("مصدر التنبيهات لا يدعم القراءة غير المتزامنة.");
            var result = await asyncRepository.LoadAsync(request, cancellation).ConfigureAwait(false);
            cancellation.ThrowIfCancellationRequested();
            if (!canRead(captured)) throw new UnauthorizedAccessException("تغيرت صلاحية قراءة تنبيهات المخزون أثناء الطلب.");
            return result ?? throw new InvalidOperationException("لم يرجع مصدر التنبيهات نتيجة.");
        }
    }
}

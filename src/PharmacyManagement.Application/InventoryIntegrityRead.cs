using System;
using System.Threading;
using System.Threading.Tasks;

namespace PharmacyManagement.Application
{
    public interface IInventoryIntegrityReadRepository
    {
        OperationalReportResult Load();
    }

    public interface IInventoryIntegrityReadService
    {
        OperationalReportResult Load();
    }

    public interface IAsyncInventoryIntegrityReadRepository
    {
        Task<OperationalReportResult> LoadAsync(CancellationToken cancellation);
    }

    public interface IInventoryIntegritySessionReadService
    {
        Task<OperationalReportResult> LoadAsync(OperationActor actor, CancellationToken cancellation);
    }

    public sealed class InventoryIntegrityReadService : IInventoryIntegrityReadService, IInventoryIntegritySessionReadService
    {
        private readonly IInventoryIntegrityReadRepository repository;
        private readonly Func<OperationActor, bool> canRead;
        public InventoryIntegrityReadService(IInventoryIntegrityReadRepository repository)
            : this(repository, null) { }
        public InventoryIntegrityReadService(IInventoryIntegrityReadRepository repository, Func<OperationActor, bool> canRead)
        { this.repository=repository??throw new ArgumentNullException(nameof(repository)); this.canRead=canRead; }
        public OperationalReportResult Load() { return repository.Load(); }

        public async Task<OperationalReportResult> LoadAsync(OperationActor actor, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            if (actor == null || string.IsNullOrWhiteSpace(actor.UserName) || string.IsNullOrWhiteSpace(actor.UserRole) ||
                actor.UserName.Length > 200 || actor.UserRole.Length > 50 || canRead == null)
                throw new UnauthorizedAccessException("لا تملك صلاحية فحص مطابقة المخزون.");
            var captured = new OperationActor { UserName = actor.UserName, UserRole = actor.UserRole };
            if (!canRead(captured)) throw new UnauthorizedAccessException("لا تملك صلاحية فحص مطابقة المخزون.");
            var asyncRepository = repository as IAsyncInventoryIntegrityReadRepository;
            if (asyncRepository == null) throw new InvalidOperationException("مصدر فحص المخزون لا يدعم القراءة غير المتزامنة.");
            var result = await asyncRepository.LoadAsync(cancellation).ConfigureAwait(false);
            cancellation.ThrowIfCancellationRequested();
            if (!canRead(captured)) throw new UnauthorizedAccessException("تغيرت صلاحية فحص المخزون أثناء الطلب.");
            return result ?? throw new InvalidOperationException("لم يرجع فحص المخزون نتيجة.");
        }
    }
}

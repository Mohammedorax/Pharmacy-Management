using System;
using System.Threading;
using System.Threading.Tasks;

namespace PharmacyManagement.Application
{
    public interface ISystemMonitorLogRepository
    {
        OperationalReportResult LoadAudit(int take);
        OperationalReportResult LoadInventoryMovements(int take);
    }

    public interface IAsyncSystemMonitorLogRepository : ISystemMonitorLogRepository
    {
        Task<OperationalReportResult> LoadAuditAsync(int take, CancellationToken cancellation);
        Task<OperationalReportResult> LoadInventoryMovementsAsync(int take, CancellationToken cancellation);
    }

    public interface ISystemMonitorLogReadService
    {
        OperationalReportResult GetRecentAuditLogs(int take = 100);
        OperationalReportResult GetRecentInventoryMovements(int take = 200);
    }

    public interface IAsyncSystemMonitorLogReadService : ISystemMonitorLogReadService
    {
        Task<OperationalReportResult> GetRecentAuditLogsAsync(int take, CancellationToken cancellation);
        Task<OperationalReportResult> GetRecentInventoryMovementsAsync(int take, CancellationToken cancellation);
    }

    public sealed class SystemMonitorLogReadService : IAsyncSystemMonitorLogReadService
    {
        private readonly ISystemMonitorLogRepository repository;
        private readonly Func<bool> authorize;

        // Preserve the existing constructor signature; an absent authorization contract denies reads.
        public SystemMonitorLogReadService(ISystemMonitorLogRepository repository) : this(repository, null) { }
        public SystemMonitorLogReadService(ISystemMonitorLogRepository repository, Func<bool> authorize)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
            this.authorize = authorize;
        }

        private void EnsureAuthorized()
        {
            if (authorize == null || !authorize()) throw new UnauthorizedAccessException("System monitor read is not authorized.");
        }

        private OperationalReportResult Read(Func<OperationalReportResult> read)
        {
            EnsureAuthorized();
            var result = read();
            EnsureAuthorized();
            return result ?? throw new InvalidOperationException("System monitor repository returned no result.");
        }

        private async Task<OperationalReportResult> ReadAsync(Func<IAsyncSystemMonitorLogRepository, Task<OperationalReportResult>> read, CancellationToken cancellation)
        {
            EnsureAuthorized();
            cancellation.ThrowIfCancellationRequested();
            var asyncRepository = repository as IAsyncSystemMonitorLogRepository;
            if (asyncRepository == null) throw new InvalidOperationException("System monitor repository does not support asynchronous reads.");
            var result = await read(asyncRepository).ConfigureAwait(false);
            cancellation.ThrowIfCancellationRequested();
            EnsureAuthorized();
            return result ?? throw new InvalidOperationException("System monitor repository returned no result.");
        }

        public OperationalReportResult GetRecentAuditLogs(int take = 100)
        { return Read(() => repository.LoadAudit(Math.Max(1, Math.Min(take, 500)))); }

        public OperationalReportResult GetRecentInventoryMovements(int take = 200)
        { return Read(() => repository.LoadInventoryMovements(Math.Max(1, Math.Min(take, 1000)))); }

        public Task<OperationalReportResult> GetRecentAuditLogsAsync(int take, CancellationToken cancellation)
        { return ReadAsync(repo => repo.LoadAuditAsync(Math.Max(1, Math.Min(take, 500)), cancellation), cancellation); }

        public Task<OperationalReportResult> GetRecentInventoryMovementsAsync(int take, CancellationToken cancellation)
        { return ReadAsync(repo => repo.LoadInventoryMovementsAsync(Math.Max(1, Math.Min(take, 1000)), cancellation), cancellation); }
    }
}

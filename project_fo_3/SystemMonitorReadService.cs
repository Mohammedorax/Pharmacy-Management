using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Application;

namespace project_fo_3
{
    internal interface ISystemMonitorReadSession
    {
        void EnsureContext();
        Task EnsureAccessAsync(CancellationToken cancellation);
        Task<SystemMonitorReadService.Snapshot> LoadAsync(CancellationToken cancellation);
    }
    internal sealed class SystemMonitorReadSession : ISystemMonitorReadSession
    {
        private readonly HomeOperationalAlertReadSession alerts;
        private readonly IAsyncSystemMonitorLogReadService logs;
        private readonly Func<bool> current;
        private readonly SystemMonitorAccessReadService access;
        internal SystemMonitorReadSession(HomeOperationalAlertReadSession alerts,
            IAsyncSystemMonitorLogReadService logs, Func<bool> current, SystemMonitorAccessReadService access)
        {
            this.alerts = alerts ?? throw new ArgumentNullException(nameof(alerts));
            this.logs = logs ?? throw new ArgumentNullException(nameof(logs));
            this.current = current ?? throw new ArgumentNullException(nameof(current));
            this.access = access ?? throw new ArgumentNullException(nameof(access));
        }
        public Task EnsureAccessAsync(CancellationToken cancellation) => access.EnsureAccessAsync(cancellation);
        public void EnsureContext()
        {
            if (!current()) throw new UnauthorizedAccessException("System monitor context changed.");
            alerts.EnsureContext();
        }
        public async Task<SystemMonitorReadService.Snapshot> LoadAsync(CancellationToken cancellation)
        {
            var data = new SystemMonitorReadService.Snapshot();
            try
            {
                await EnsureAccessAsync(cancellation).ConfigureAwait(false);
                EnsureContext(); cancellation.ThrowIfCancellationRequested();
                data.Alerts = await alerts.LoadAsync(cancellation).ConfigureAwait(false);
                await EnsureAccessAsync(cancellation).ConfigureAwait(false);
                EnsureContext(); cancellation.ThrowIfCancellationRequested();
                data.Audit = OperationalReportCatalog.ToDataTable(
                    await logs.GetRecentAuditLogsAsync(100, cancellation).ConfigureAwait(false));
                await EnsureAccessAsync(cancellation).ConfigureAwait(false);
                EnsureContext(); cancellation.ThrowIfCancellationRequested();
                data.Movements = OperationalReportCatalog.ToDataTable(
                    await logs.GetRecentInventoryMovementsAsync(200, cancellation).ConfigureAwait(false));
                await EnsureAccessAsync(cancellation).ConfigureAwait(false);
                EnsureContext(); cancellation.ThrowIfCancellationRequested();
                return data;
            }
            catch { data.Dispose(); throw; }
        }
    }
    internal static class SystemMonitorReadService
    {
        internal sealed class Snapshot : IDisposable
        {
            internal DataTable Alerts { get; set; }
            internal DataTable Audit { get; set; }
            internal DataTable Movements { get; set; }
            public void Dispose()
            {
                Alerts?.Dispose(); Audit?.Dispose(); Movements?.Dispose();
                Alerts = null; Audit = null; Movements = null;
            }
        }
    }
}


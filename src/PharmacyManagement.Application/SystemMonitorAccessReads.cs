using System;
using System.Threading;
using System.Threading.Tasks;
namespace PharmacyManagement.Application
{
    public sealed class SystemMonitorAccessReadService
    {
        private readonly IAsyncEmployeePermissionRepository repository;
        private readonly Func<bool> current;
        private readonly Func<string,bool> isManager;
        private readonly string user, role, employee;
        private volatile bool accessVerified;
        public bool HasVerifiedAccess => accessVerified && current();
        public SystemMonitorAccessReadService(IAsyncEmployeePermissionRepository repository, Func<bool> current,
            Func<string,bool> isManager, string user, string role, string employee)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
            this.current = current ?? throw new ArgumentNullException(nameof(current));
            this.isManager = isManager ?? throw new ArgumentNullException(nameof(isManager));
            if(string.IsNullOrWhiteSpace(user)||string.IsNullOrWhiteSpace(role))throw new ArgumentException("Monitor actor is required.");
            this.user=user;this.role=role;this.employee=employee;
        }
        private void EnsureCurrent()
        { if(!current())throw new UnauthorizedAccessException("System monitor context changed."); }
        public async Task EnsureAccessAsync(CancellationToken cancellation)
        {
            try
            {
                await EnsureAccessCoreAsync(cancellation).ConfigureAwait(false);
                cancellation.ThrowIfCancellationRequested();
                EnsureCurrent();
                accessVerified = true;
            }
            catch { accessVerified = false; throw; }
        }
        private async Task EnsureAccessCoreAsync(CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();EnsureCurrent();
            // Manager screen access follows the existing application policy; manager overrides cannot be stored.
            if(isManager(role))return;
            var snapshot=string.IsNullOrWhiteSpace(employee)
                ? await repository.GetByUserNameAsync(user,cancellation).ConfigureAwait(false)
                : await repository.GetByEmployeeIdAsync(employee,cancellation).ConfigureAwait(false);
            cancellation.ThrowIfCancellationRequested();EnsureCurrent();
            bool allowed;
            if(snapshot==null||!snapshot.IsAvailable||!string.Equals(snapshot.Role,role,StringComparison.OrdinalIgnoreCase)||
                snapshot.Overrides==null||!snapshot.Overrides.TryGetValue("Screen.SystemMonitor",out allowed)||!allowed)
                throw new UnauthorizedAccessException("System monitor permission was revoked.");
        }
    }
}


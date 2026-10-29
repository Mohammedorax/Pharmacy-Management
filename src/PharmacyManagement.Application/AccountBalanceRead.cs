using System;
using System.Threading.Tasks;
using System.Threading;

namespace PharmacyManagement.Application
{
    public sealed class AccountBalanceReadRequest
    {
        public AccountBalanceReadRequest(string accountCode, DateTime throughDate)
        {
            if (string.IsNullOrWhiteSpace(accountCode) || accountCode.Trim().Length > 20)
                throw new ArgumentException("A valid account code is required.", nameof(accountCode));
            if (throughDate.Date < new DateTime(1753, 1, 1))
                throw new ArgumentOutOfRangeException(nameof(throughDate));
            AccountCode = accountCode.Trim();
            ThroughDate = throughDate.Date;
            ExclusiveEnd = ThroughDate == DateTime.MaxValue.Date ? (DateTime?)null : ThroughDate.AddDays(1);
        }
        public string AccountCode { get; }
        public DateTime ThroughDate { get; }
        public DateTime? ExclusiveEnd { get; }
    }

    public interface IAccountBalanceReadRepository
    {
        decimal Load(AccountBalanceReadRequest request);
        Task<decimal> LoadAsync(AccountBalanceReadRequest request, CancellationToken cancellationToken);
    }
    public interface IAccountBalanceReadService
    {
        Task<decimal> LoadAsync(AccountBalanceReadRequest request);
        Task<decimal> LoadAsync(AccountBalanceReadRequest request, OperationActor actor, CancellationToken cancellationToken);
    }
    public sealed class AccountBalanceReadService : IAccountBalanceReadService
    {
        private readonly IAccountBalanceReadRepository repository;
        private readonly Func<OperationActor, bool> canRead;
        public AccountBalanceReadService(IAccountBalanceReadRepository repository, Func<OperationActor, bool> canRead = null)
        { this.repository = repository ?? throw new ArgumentNullException(nameof(repository)); this.canRead = canRead ?? (_ => false); }
        private void Authorize(OperationActor actor)
        {
            if (actor == null || string.IsNullOrWhiteSpace(actor.UserName) || string.IsNullOrWhiteSpace(actor.UserRole) || !canRead(actor))
                throw new UnauthorizedAccessException("لا تملك صلاحية قراءة رصيد الحساب.");
        }
        // A legacy caller cannot infer authority from machine state; explicit actors are mandatory.
        public Task<decimal> LoadAsync(AccountBalanceReadRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            return LoadAsync(request, null, CancellationToken.None);
        }
        public async Task<decimal> LoadAsync(AccountBalanceReadRequest request, OperationActor actor, CancellationToken cancellationToken)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            var captured = new OperationActor { UserName = actor?.UserName, UserRole = actor?.UserRole };
            Authorize(captured); cancellationToken.ThrowIfCancellationRequested();
            decimal balance = await repository.LoadAsync(request, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested(); Authorize(captured);
            return balance;
        }
    }
}

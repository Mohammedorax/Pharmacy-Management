using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;

namespace PharmacyManagement.Application
{
    public sealed class AccountLookupRow
    {
        public AccountLookupRow(string code, string name, string type, string parentCode, bool active)
        { Code = code; Name = name; Type = type; ParentCode = parentCode; Active = active; }
        public string Code { get; }
        public string Name { get; }
        public string Type { get; }
        public string ParentCode { get; }
        public bool Active { get; }
    }
    public sealed class CostCenterLookupRow
    {
        public CostCenterLookupRow(int id, string name) { Id = id; Name = name; }
        public int Id { get; }
        public string Name { get; }
    }
    public interface IAccountingLookupReadRepository
    {
        IReadOnlyList<AccountLookupRow> LoadAccounts(bool activeOnly);
        IReadOnlyList<CostCenterLookupRow> LoadCostCenters();
        Task<IReadOnlyList<AccountLookupRow>> LoadAccountsAsync(bool activeOnly, CancellationToken cancellationToken);
        Task<IReadOnlyList<CostCenterLookupRow>> LoadCostCentersAsync(CancellationToken cancellationToken);
    }
    public interface IAccountingLookupReadService
    {
        IReadOnlyList<AccountLookupRow> LoadAccounts(bool activeOnly, OperationActor actor);
        Task<IReadOnlyList<AccountLookupRow>> LoadAccountsAsync(bool activeOnly, OperationActor actor);
        IReadOnlyList<CostCenterLookupRow> LoadCostCenters(OperationActor actor);
        Task<IReadOnlyList<AccountLookupRow>> LoadAccountsAsync(bool activeOnly, OperationActor actor, CancellationToken cancellationToken);
        Task<IReadOnlyList<CostCenterLookupRow>> LoadCostCentersAsync(OperationActor actor, CancellationToken cancellationToken);
    }
    public sealed class AccountingLookupReadService : IAccountingLookupReadService
    {
        private readonly IAccountingLookupReadRepository repository;
        private readonly Func<OperationActor, bool> canRead;
        public AccountingLookupReadService(IAccountingLookupReadRepository repository, Func<OperationActor, bool> canRead)
        { this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
          this.canRead = canRead ?? throw new ArgumentNullException(nameof(canRead)); }
        private void Authorize(OperationActor actor)
        {
            if (actor == null || string.IsNullOrWhiteSpace(actor.UserName) || !canRead(actor))
                throw new UnauthorizedAccessException("لا تملك صلاحية قراءة خيارات المحاسبة.");
        }
        private static IReadOnlyList<T> Snapshot<T>(IReadOnlyList<T> rows)
        {
            if (rows == null) throw new InvalidOperationException("Accounting lookup source returned no result.");
            return new List<T>(rows).AsReadOnly();
        }
        private OperationActor CaptureActor(OperationActor actor)
        {
            var current=new OperationActor {UserName=actor?.UserName,UserRole=actor?.UserRole};
            Authorize(current);return current;
        }
        public IReadOnlyList<AccountLookupRow> LoadAccounts(bool activeOnly, OperationActor actor)
        { var current=CaptureActor(actor); var rows=repository.LoadAccounts(activeOnly); Authorize(current); return Snapshot(rows); }
        public async Task<IReadOnlyList<AccountLookupRow>> LoadAccountsAsync(bool activeOnly, OperationActor actor)
        { return await LoadAccountsAsync(activeOnly,actor,CancellationToken.None).ConfigureAwait(false); }
        public async Task<IReadOnlyList<AccountLookupRow>> LoadAccountsAsync(bool activeOnly, OperationActor actor, CancellationToken cancellationToken)
        {
            var current=CaptureActor(actor);cancellationToken.ThrowIfCancellationRequested();
            var rows=await repository.LoadAccountsAsync(activeOnly,cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();Authorize(current);return Snapshot(rows);
        }
        public IReadOnlyList<CostCenterLookupRow> LoadCostCenters(OperationActor actor)
        { var current=CaptureActor(actor); var rows=repository.LoadCostCenters(); Authorize(current); return Snapshot(rows); }
        public async Task<IReadOnlyList<CostCenterLookupRow>> LoadCostCentersAsync(OperationActor actor, CancellationToken cancellationToken)
        {
            var current=CaptureActor(actor);cancellationToken.ThrowIfCancellationRequested();
            var rows=await repository.LoadCostCentersAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();Authorize(current);return Snapshot(rows);
        }
    }
}

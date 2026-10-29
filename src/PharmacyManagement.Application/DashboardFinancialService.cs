using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
namespace PharmacyManagement.Application
{
    public interface IDashboardFinancialReadRepository
    {
        Task<IReadOnlyList<DashboardFinancialFact>> ReadAsync(DateTime from,DateTime to,Action demandAccess,CancellationToken token);
    }
    public interface IDashboardFinancialReadService
    {
        Task<IReadOnlyList<DashboardFinancialFact>> ReadAsync(DateTime from,DateTime to,CancellationToken token);
    }
    public sealed class DashboardFinancialReadService : IDashboardFinancialReadService
    {
        private readonly IDashboardFinancialReadRepository repository;
        private readonly OperationActor actor;
        private readonly Func<OperationActor,bool> policy;
        public DashboardFinancialReadService(IDashboardFinancialReadRepository repository,OperationActor actor,Func<OperationActor,bool> policy)
        {
            this.repository=repository??throw new ArgumentNullException(nameof(repository));
            if(actor==null)throw new ArgumentNullException(nameof(actor));
            this.actor=new OperationActor{UserName=actor.UserName,UserRole=actor.UserRole};
            this.policy=policy??throw new ArgumentNullException(nameof(policy));
        }
        private void Demand()
        {
            if(string.IsNullOrWhiteSpace(actor.UserName)||string.IsNullOrWhiteSpace(actor.UserRole)||
                !policy(new OperationActor{UserName=actor.UserName,UserRole=actor.UserRole}))
                throw new UnauthorizedAccessException("تغيرت الجلسة أو صلاحية لوحة المؤشرات.");
        }
        public async Task<IReadOnlyList<DashboardFinancialFact>> ReadAsync(DateTime from,DateTime to,CancellationToken token)
        {
            token.ThrowIfCancellationRequested();Demand();
            var result=await repository.ReadAsync(from,to,Demand,token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();Demand();return result;
        }
    }
}

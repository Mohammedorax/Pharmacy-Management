using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
namespace PharmacyManagement.Application
{
    public sealed class JournalCurrencyLookupRow
    {
        public JournalCurrencyLookupRow(int id,string code,string symbol,bool system)
        {Id=id;Code=code;Symbol=symbol;IsSystem=system;}
        public int Id{get;} public string Code{get;} public string Symbol{get;} public bool IsSystem{get;}
    }
    public sealed class JournalEditorLookupSnapshot
    {
        public JournalEditorLookupSnapshot(IReadOnlyList<AccountLookupRow> accounts,IReadOnlyList<CostCenterLookupRow> centers,IReadOnlyList<JournalCurrencyLookupRow> currencies)
        {
            if(accounts==null||centers==null||currencies==null||accounts.Any(r=>r==null)||centers.Any(r=>r==null)||currencies.Any(r=>r==null))
                throw new InvalidOperationException("لم ترجع قراءة خيارات القيد بيانات مكتملة.");
            if(currencies.Any(r=>r.Id<=0||string.IsNullOrWhiteSpace(r.Code)||r.Code.Trim().Length>10||(r.Symbol??string.Empty).Trim().Length>20)||currencies.Count(r=>r.IsSystem)!=1)
                throw new CurrencySnapshotUnavailableException();
            Accounts=new List<AccountLookupRow>(accounts).AsReadOnly();CostCenters=new List<CostCenterLookupRow>(centers).AsReadOnly();Currencies=new List<JournalCurrencyLookupRow>(currencies).AsReadOnly();
            SystemCurrency=Currencies.Single(r=>r.IsSystem);
        }
        public IReadOnlyList<AccountLookupRow> Accounts{get;}
        public IReadOnlyList<CostCenterLookupRow> CostCenters{get;}
        public IReadOnlyList<JournalCurrencyLookupRow> Currencies{get;}
        public JournalCurrencyLookupRow SystemCurrency{get;}
    }
    public interface IJournalEditorLookupReadRepository
    {Task<JournalEditorLookupSnapshot> LoadAsync(CancellationToken cancellationToken);}
    public interface IJournalEditorLookupReadService
    {Task<JournalEditorLookupSnapshot> LoadAsync(OperationActor actor,CancellationToken cancellationToken);}
    public sealed class JournalEditorLookupReadService:IJournalEditorLookupReadService
    {
        private readonly IJournalEditorLookupReadRepository repository;private readonly Func<OperationActor,bool> canRead;
        public JournalEditorLookupReadService(IJournalEditorLookupReadRepository repository,Func<OperationActor,bool> canRead)
        {this.repository=repository??throw new ArgumentNullException(nameof(repository));this.canRead=canRead??throw new ArgumentNullException(nameof(canRead));}
        private void Authorize(OperationActor actor)
        {if(actor==null||string.IsNullOrWhiteSpace(actor.UserName)||string.IsNullOrWhiteSpace(actor.UserRole)||!canRead(actor))throw new UnauthorizedAccessException("لا تملك صلاحية قراءة خيارات القيد.");}
        public async Task<JournalEditorLookupSnapshot> LoadAsync(OperationActor actor,CancellationToken cancellationToken)
        {
            var current=new OperationActor{UserName=actor?.UserName,UserRole=actor?.UserRole};Authorize(current);cancellationToken.ThrowIfCancellationRequested();
            var result=await repository.LoadAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();Authorize(current);
            return result??throw new InvalidOperationException("لم ترجع خيارات القيد بيانات صالحة.");
        }
    }
}

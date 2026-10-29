using System;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Application;
namespace PharmacyManagement.Application {
 public interface IPurchaseInventoryModeReadRepository { Task<bool> IsCommercialInventoryEnabledAsync(CancellationToken token); }
 public sealed class PurchaseInventoryModeReadService {
  readonly IPurchaseInventoryModeReadRepository repository;readonly Func<OperationActor,bool> authorized;
  public PurchaseInventoryModeReadService(IPurchaseInventoryModeReadRepository repository,Func<OperationActor,bool> authorized){
   this.repository=repository??throw new ArgumentNullException(nameof(repository));this.authorized=authorized??throw new ArgumentNullException(nameof(authorized));
  }
  OperationActor Require(OperationActor actor){
   if(actor==null||string.IsNullOrWhiteSpace(actor.UserName)||string.IsNullOrWhiteSpace(actor.UserRole)||!authorized(actor))
    throw new UnauthorizedAccessException("لا تملك صلاحية قراءة بيانات المشتريات.");
   return new OperationActor{UserName=actor.UserName,UserRole=actor.UserRole};
  }
  public async Task<bool> IsCommercialInventoryEnabledAsync(OperationActor actor,CancellationToken token){
   var captured=Require(actor);token.ThrowIfCancellationRequested();bool result;
   try{result=await repository.IsCommercialInventoryEnabledAsync(token).ConfigureAwait(false);}catch{token.ThrowIfCancellationRequested();throw;}
   token.ThrowIfCancellationRequested();Require(captured);return result;
  }
 }
}

using System;
using System.Threading;
using System.Threading.Tasks;
namespace PharmacyManagement.Application {
 public enum PurchaseDraftNumberKind { Purchase=0, Detail=1 }
 public interface IPurchaseDraftNumberRepository { Task<long> ReserveAsync(PurchaseDraftNumberKind kind,CancellationToken token); }
 public sealed class PurchaseDraftNumberService {
  readonly IPurchaseDraftNumberRepository repository;readonly Func<OperationActor,bool> authorized;
  public PurchaseDraftNumberService(IPurchaseDraftNumberRepository repository,Func<OperationActor,bool> authorized){
   this.repository=repository??throw new ArgumentNullException(nameof(repository));this.authorized=authorized??throw new ArgumentNullException(nameof(authorized));
  }
  OperationActor Require(OperationActor actor){
   if(actor==null||string.IsNullOrWhiteSpace(actor.UserName)||string.IsNullOrWhiteSpace(actor.UserRole)||!authorized(actor))
    throw new UnauthorizedAccessException("لا تملك صلاحية حجز أرقام المشتريات.");
   return new OperationActor{UserName=actor.UserName,UserRole=actor.UserRole};
  }
  public async Task<long> ReserveAsync(PurchaseDraftNumberKind kind,OperationActor actor,CancellationToken token){
   if(kind!=PurchaseDraftNumberKind.Purchase&&kind!=PurchaseDraftNumberKind.Detail)throw new ArgumentOutOfRangeException(nameof(kind));
   var captured=Require(actor);token.ThrowIfCancellationRequested();long value;
   try{value=await repository.ReserveAsync(kind,token).ConfigureAwait(false);}catch{token.ThrowIfCancellationRequested();throw;}
   token.ThrowIfCancellationRequested();Require(captured);
   if(value<=0)throw new InvalidOperationException("رقم المشتريات المحجوز غير صالح.");
   return value;
  }
 }
}

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Application;
namespace PharmacyManagement.Application {
 public interface IPurchaseLookupReadRepository {
  Task<IReadOnlyList<string>> GetSuppliersAsync(CancellationToken token);
  Task<IReadOnlyList<PurchaseEmployeeChoice>> GetEmployeesAsync(CancellationToken token);
 }
 public sealed class PurchaseLookupReadService {
  readonly IPurchaseLookupReadRepository repository;
  readonly Func<OperationActor,bool> authorized;
  public PurchaseLookupReadService(IPurchaseLookupReadRepository repository,Func<OperationActor,bool> authorized){
   this.repository=repository??throw new ArgumentNullException(nameof(repository));
   this.authorized=authorized??throw new ArgumentNullException(nameof(authorized));
  }
  OperationActor Require(OperationActor actor){
   if(actor==null||string.IsNullOrWhiteSpace(actor.UserName)||string.IsNullOrWhiteSpace(actor.UserRole)||!authorized(actor))
    throw new UnauthorizedAccessException("لا تملك صلاحية قراءة بيانات المشتريات.");
   return new OperationActor{UserName=actor.UserName,UserRole=actor.UserRole};
  }
  public async Task<IReadOnlyList<string>> GetSuppliersAsync(OperationActor actor,CancellationToken token){
   var captured=Require(actor);token.ThrowIfCancellationRequested();
   IReadOnlyList<string> rows;
   try{rows=await repository.GetSuppliersAsync(token).ConfigureAwait(false);}catch{token.ThrowIfCancellationRequested();throw;}
   token.ThrowIfCancellationRequested();Require(captured);
   if(rows==null)throw new InvalidOperationException("قائمة الموردين غير مكتملة.");
   var copy=new List<string>(rows);Require(captured);return copy.AsReadOnly();
  }
  public async Task<IReadOnlyList<PurchaseEmployeeChoice>> GetEmployeesAsync(OperationActor actor,CancellationToken token){
   var captured=Require(actor);token.ThrowIfCancellationRequested();
   IReadOnlyList<PurchaseEmployeeChoice> rows;
   try{rows=await repository.GetEmployeesAsync(token).ConfigureAwait(false);}catch{token.ThrowIfCancellationRequested();throw;}
   token.ThrowIfCancellationRequested();Require(captured);
   if(rows==null)throw new InvalidOperationException("قائمة الموظفين غير مكتملة.");
   var copy=new List<PurchaseEmployeeChoice>();
   foreach(var row in rows){
    if(row==null)throw new InvalidOperationException("بيانات موظف غير مكتملة.");
    copy.Add(new PurchaseEmployeeChoice{EmployeeId=row.EmployeeId,EmployeeName=row.EmployeeName});
   }
   Require(captured);return copy.AsReadOnly();
  }
 }
}

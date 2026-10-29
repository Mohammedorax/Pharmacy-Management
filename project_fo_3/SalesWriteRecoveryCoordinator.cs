using System;
using System.Linq;
using PharmacyManagement.Application;
namespace project_fo_3
{
 internal interface ISalesWriteRecoveryPort
 {
  SalesInvoiceIssueResult Issue(SalesWriteIntent intent,SalesWriteExecutionContext execution);
  SalesReceiptObservation Observe(SalesWriteIntent intent,SalesWriteExecutionContext execution);
  SalesReceiptObservation Abandon(SalesWriteIntent intent,SalesWriteExecutionContext execution);
 }
 internal sealed partial class SalesInvoiceOperation:ISalesWriteRecoveryPort
 {
  static SalesWriteExecutionContext RequiredSalesExecution(SalesWriteExecutionContext execution)=>execution??throw new ArgumentNullException(nameof(execution));
  SalesInvoiceIssueResult ISalesWriteRecoveryPort.Issue(SalesWriteIntent i,SalesWriteExecutionContext execution)=>IssueIdentified(i,null,RequiredSalesExecution(execution));
  SalesReceiptObservation ISalesWriteRecoveryPort.Observe(SalesWriteIntent i,SalesWriteExecutionContext execution)=>ObserveIdentified(i,RequiredSalesExecution(execution));
  SalesReceiptObservation ISalesWriteRecoveryPort.Abandon(SalesWriteIntent i,SalesWriteExecutionContext execution)=>AbandonUnposted(i,null,RequiredSalesExecution(execution));
 }
 internal sealed class SalesWriteRecoveryCoordinator:IDisposable
 {
  readonly string source,employee;readonly long revision;readonly OperationActor owner;readonly SalesPendingStore store;readonly ISalesWriteRecoveryPort operation;
  readonly SalesWriteExecutionContext execution;
  bool disposed;
  internal SalesWriteRecoveryCoordinator(string directory):this(directory,new SalesInvoiceOperation()){}
  internal SalesWriteRecoveryCoordinator(string directory,ISalesWriteRecoveryPort port){
   source=Database.ConnectionString;employee=SessionContext.CurrentEmployeeId;revision=SessionContext.CurrentSessionRevision;owner=new OperationActor{UserName=SessionContext.CurrentUser,UserRole=SessionContext.CurrentRole};
   EnsureCurrent(false);execution=new SalesWriteExecutionContext(source,revision);operation=port??throw new ArgumentNullException(nameof(port));store=new SalesPendingStore(directory,SalesWriteSourceFingerprint.Compute(source),employee,owner.UserName);
  }
  void EnsureCurrent(bool writing){
   if(disposed)throw new ObjectDisposedException(nameof(SalesWriteRecoveryCoordinator));
   if(!SessionContext.IsAuthenticated||Database.ConnectionString!=source||SessionContext.CurrentSessionRevision!=revision||SessionContext.CurrentEmployeeId!=employee||!string.Equals(SessionContext.CurrentUser,owner.UserName,StringComparison.OrdinalIgnoreCase)||!string.Equals(SessionContext.CurrentRole,owner.UserRole,StringComparison.OrdinalIgnoreCase)||!(writing?PermissionService.CanExecute(AppOperation.PostSalesInvoice):PermissionService.CanOpen(AppPermission.Sales)))throw new UnauthorizedAccessException("تغيرت جلسة استرجاع البيع أو صلاحياتها؛ أعد فتح الشاشة.");
  }
  internal SalesWriteIntent Pending(){EnsureCurrent(false);return store.Load();}
  internal SalesReceiptObservation Observe(){EnsureCurrent(false);var pending=store.Load();if(pending==null)return null;var result=operation.Observe(pending,execution);EnsureCurrent(false);if(result!=null&&result.RequestId!=pending.RequestId)throw new InvalidOperationException("هوية نتيجة الاسترجاع غير متطابقة.");return result;}
  internal SalesInvoiceIssueResult Submit(SalesInvoiceIssueRequest request,bool commercialExpected){
   EnsureCurrent(false);var pending=store.Load();
   if(pending!=null){
     var compared=new SalesWriteIntent(pending.RequestId,employee,owner,request,pending.SourceFingerprint,pending.CommercialExpected);
     if(!compared.Fingerprint().SequenceEqual(pending.Fingerprint()))throw new InvalidOperationException("توجد محاولة بيع معلقة ببيانات مختلفة؛ تحقق من نتيجتها قبل إعداد فاتورة جديدة.");
     if(pending.CommercialExpected!=commercialExpected)throw new InvalidOperationException("تغير نمط المخزون منذ تجهيز محاولة البيع؛ تحقق من النتيجة قبل المتابعة.");
    var observed=operation.Observe(pending,execution);EnsureCurrent(false);
    if(observed==null)throw new InvalidOperationException("نتيجة حفظ البيع غير محسومة؛ تحقق أو أغلق المحاولة غير المعتمدة قبل بدء طلب جديد.");
    if(observed.RequestId!=pending.RequestId)throw new InvalidOperationException("هوية نتيجة الاسترجاع غير متطابقة.");
    if(observed.IsAbandoned)throw new SalesAttemptAbandonedException(pending.RequestId);
    if(observed.IsCancelled)throw new InvalidOperationException("اعتمدت المحاولة ثم ألغيت فاتورتها؛ افتحها من السجل للتحقق.");
    return observed.Result;
   }
   EnsureCurrent(true);var intent=new SalesWriteIntent(Guid.NewGuid(),employee,owner,request,SalesWriteSourceFingerprint.Compute(source),commercialExpected);
   store.Save(intent);EnsureCurrent(true);var result=operation.Issue(intent,execution);EnsureCurrent(false);return result;
  }
  static bool SameResult(SalesInvoiceIssueResult a,SalesInvoiceIssueResult b){
   if(a==null||b==null||a.IsCommercial!=b.IsCommercial||a.CustomerId!=b.CustomerId||a.CommercialInvoiceId!=b.CommercialInvoiceId||a.CommercialInvoiceNumber!=b.CommercialInvoiceNumber||a.AccountingSourceId!=b.AccountingSourceId||!(a.BillLineIds??Array.Empty<string>()).SequenceEqual(b.BillLineIds??Array.Empty<string>()))return false;
   return typeof(SalesInvoiceIssueResult).GetProperties().Where(p=>p.PropertyType==typeof(decimal)).All(p=>Equals(p.GetValue(a),p.GetValue(b)));
  }
  internal SalesReceiptObservation CompleteDisplayed(SalesInvoiceIssueResult displayed){
   EnsureCurrent(false);var pending=store.Load();if(pending==null)throw new InvalidOperationException("لا توجد محاولة مطابقة لتأكيد العرض.");
   var observed=operation.Observe(pending,execution);EnsureCurrent(false);
   if(observed==null||observed.RequestId!=pending.RequestId||observed.IsAbandoned||!SameResult(displayed,observed.Result))throw new InvalidOperationException("لم تثبت نتيجة البيع المعروضة؛ احتُفظ بملف الاسترجاع.");
   using(SessionCommitGate.Commit(()=>{EnsureCurrent(false);return true;})){store.RemoveVerified(pending);}return observed;
  }
  internal SalesReceiptObservation ConfirmAbandon(){
   EnsureCurrent(false);var pending=store.Load();if(pending==null)return null;
   var observed=operation.Abandon(pending,execution);EnsureCurrent(false);
   if(observed==null||observed.RequestId!=pending.RequestId)throw new InvalidOperationException("لم تثبت حالة إغلاق المحاولة؛ احتُفظ بالملف.");
   if(observed.IsAbandoned)using(SessionCommitGate.Commit(()=>{EnsureCurrent(false);return true;})){store.RemoveVerified(pending);}return observed;
  }
  public void Dispose(){if(disposed)return;disposed=true;store.Dispose();}
 }
}

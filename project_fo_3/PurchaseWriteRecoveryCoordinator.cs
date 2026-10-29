using System;
using System.Linq;
using PharmacyManagement.Application;
namespace project_fo_3 {
 internal sealed class PurchaseWriteRecoveryCoordinator:IDisposable {
  readonly PurchasePendingStore store;readonly PurchaseInvoiceOperation operation;
  readonly string source,employee;readonly OperationActor owner;readonly long revision;
  internal PurchaseWriteRecoveryCoordinator(string directory):this(directory,new PurchaseInvoiceOperation()) {}
  internal PurchaseWriteRecoveryCoordinator(string directory,PurchaseInvoiceOperation suppliedOperation) {
   source=Database.ConnectionString;employee=SessionContext.CurrentEmployeeId;revision=SessionContext.CurrentSessionRevision;
   owner=new OperationActor{UserName=SessionContext.CurrentUser,UserRole=SessionContext.CurrentRole};
   EnsureCurrent();
   store=new PurchasePendingStore(directory,PurchaseWriteSourceFingerprint.Compute(source),employee,owner.UserName);
   operation=suppliedOperation ?? throw new ArgumentNullException(nameof(suppliedOperation));
  }
  void EnsureCurrent(){
   if(!SessionContext.IsAuthenticated||revision!=SessionContext.CurrentSessionRevision||
      source!=Database.ConnectionString||employee!=SessionContext.CurrentEmployeeId||
      !string.Equals(owner.UserName,SessionContext.CurrentUser,StringComparison.OrdinalIgnoreCase)||
      !string.Equals(owner.UserRole,SessionContext.CurrentRole,StringComparison.OrdinalIgnoreCase)||
      !PermissionService.CanOpen(AppPermission.Purchases))
    throw new UnauthorizedAccessException("تغيرت جلسة استعادة المشتريات؛ أعد فتح الشاشة.");
  }
  internal PurchaseWriteIntent Pending(){EnsureCurrent();return store.Load();}
  internal PurchaseReceiptObservation Observe(){
   EnsureCurrent();var pending=store.Load();if(pending==null)return null;
   var result=operation.ObserveIdentified(pending);EnsureCurrent();return result;
  }
  internal async System.Threading.Tasks.Task<PurchaseReceiptObservation> ObserveAsync(System.Threading.CancellationToken cancellationToken){
   cancellationToken.ThrowIfCancellationRequested();EnsureCurrent();var pending=store.Load();if(pending==null)return null;
   var result=await operation.ObserveIdentifiedAsync(pending,cancellationToken).ConfigureAwait(false);
   cancellationToken.ThrowIfCancellationRequested();EnsureCurrent();return result;
  }
  internal async System.Threading.Tasks.Task<PurchaseReceiptObservation> CompleteDisplayedAsync(PurchaseInvoiceResult displayed,System.Threading.CancellationToken cancellationToken){
   cancellationToken.ThrowIfCancellationRequested();EnsureCurrent();var pending=store.Load();if(pending==null)return null;
   var observed=await operation.ObserveIdentifiedAsync(pending,cancellationToken).ConfigureAwait(false);
   cancellationToken.ThrowIfCancellationRequested();EnsureCurrent();
   if(observed==null||displayed==null||observed.Result.PurchaseId!=displayed.PurchaseId||
      observed.Result.TotalAmount!=displayed.TotalAmount||
      !observed.Result.Lines.Select(x=>x.LineNumber+"|"+x.DetailId).SequenceEqual(displayed.Lines.Select(x=>x.LineNumber+"|"+x.DetailId)))
    throw new InvalidOperationException("لم تثبت نتيجة المحاولة المعروضة؛ احتُفظ بملف الاستعادة.");
   store.RemoveVerified(pending);return observed;
  }
  internal PurchaseInvoiceResult Submit(PurchaseInvoiceRequest request) {
   EnsureCurrent();var pending=store.Load();
   if(pending!=null){
    var snapshot=PurchaseInvoiceValidation.Normalize(request,pending.Snapshot().PurchaseDate.Date);
    snapshot.PurchaseDate=pending.Snapshot().PurchaseDate;
    var compared=new PurchaseWriteIntent(pending.RequestId,employee,owner,snapshot,snapshot.PurchaseDate,pending.SourceFingerprint);
    if(!compared.Fingerprint().SequenceEqual(pending.Fingerprint()))
     throw new InvalidOperationException("توجد محاولة مشتريات معلقة ببيانات مختلفة؛ تحقق من نتيجتها قبل بدء فاتورة جديدة.");
    var observed=operation.ObserveIdentified(pending);EnsureCurrent();
    if(observed==null)throw new InvalidOperationException("نتيجة حفظ المشتريات غير محسومة؛ احتُفظ بالمحاولة للتحقق دون إنشاء طلب جديد.");
    if(observed.IsCancelled)throw new InvalidOperationException("تم تنفيذ المحاولة ثم إلغاء فاتورتها.");
    return observed.Result;
   }
   if(!PermissionService.CanExecute(AppOperation.PostPurchaseInvoice))throw new UnauthorizedAccessException("لا تملك صلاحية اعتماد المشتريات.");
   var intent=new PurchaseWriteIntent(Guid.NewGuid(),employee,owner,request,DateTime.Now,PurchaseWriteSourceFingerprint.Compute(source));
   store.Save(intent);EnsureCurrent();
   return operation.IssueIdentified(intent);
  }
  internal async System.Threading.Tasks.Task<PurchaseInvoiceResult> SubmitAsync(PurchaseInvoiceRequest request,System.Threading.CancellationToken cancellationToken) {
   cancellationToken.ThrowIfCancellationRequested();EnsureCurrent();var pending=store.Load();
   if(pending!=null){
    var snapshot=PurchaseInvoiceValidation.Normalize(request,pending.Snapshot().PurchaseDate.Date);
    snapshot.PurchaseDate=pending.Snapshot().PurchaseDate;
    var compared=new PurchaseWriteIntent(pending.RequestId,employee,owner,snapshot,snapshot.PurchaseDate,pending.SourceFingerprint);
    if(!compared.Fingerprint().SequenceEqual(pending.Fingerprint()))
     throw new InvalidOperationException("توجد محاولة مشتريات معلقة ببيانات مختلفة؛ تحقق من نتيجتها قبل بدء فاتورة جديدة.");
    var observed=await operation.ObserveIdentifiedAsync(pending,cancellationToken).ConfigureAwait(false);cancellationToken.ThrowIfCancellationRequested();EnsureCurrent();
    if(observed==null)throw new InvalidOperationException("نتيجة حفظ المشتريات غير محسومة؛ احتُفظ بالمحاولة للتحقق دون إنشاء طلب جديد.");
    if(observed.IsCancelled)throw new InvalidOperationException("تم تنفيذ المحاولة ثم إلغاء فاتورتها.");
    return observed.Result;
   }
   if(!PermissionService.CanExecute(AppOperation.PostPurchaseInvoice))throw new UnauthorizedAccessException("لا تملك صلاحية اعتماد المشتريات.");
   var intent=new PurchaseWriteIntent(Guid.NewGuid(),employee,owner,request,DateTime.Now,PurchaseWriteSourceFingerprint.Compute(source));
   store.Save(intent);EnsureCurrent();
   return await operation.IssueIdentifiedAsync(intent,cancellationToken).ConfigureAwait(false);
  }
   internal PurchaseReceiptObservation CompleteDisplayed(PurchaseInvoiceResult displayed) {
   EnsureCurrent();var pending=store.Load();if(pending==null)return null;
   var observed=operation.ObserveIdentified(pending);EnsureCurrent();
   if(observed==null||displayed==null||observed.Result.PurchaseId!=displayed.PurchaseId||
      observed.Result.TotalAmount!=displayed.TotalAmount||
      !observed.Result.Lines.Select(x=>x.LineNumber+"|"+x.DetailId).SequenceEqual(displayed.Lines.Select(x=>x.LineNumber+"|"+x.DetailId)))
    throw new InvalidOperationException("لم تثبت نتيجة المحاولة المعروضة؛ احتُفظ بملف الاستعادة.");
   store.RemoveVerified(pending);return observed;
  }
  // Closes an attempt that never reached a financial write, so a later purchase is not blocked
  // forever by a pending file whose result the server does not know.
  internal PurchaseReceiptObservation ConfirmAbandon() {
   EnsureCurrent();var pending=store.Load();if(pending==null)return null;
   var observed=operation.ObserveIdentified(pending);EnsureCurrent();
   if(observed!=null&&observed.Result!=null)return observed;
   var closed=operation.AbandonUnposted(pending);EnsureCurrent();
   if(closed==null||!closed.IsAbandoned)throw new InvalidOperationException("لم تثبت حالة إغلاق المحاولة؛ احتُفظ بالملف.");
   store.RemoveVerified(pending);return closed;
  }
  public void Dispose(){store.Dispose();}
 }
}





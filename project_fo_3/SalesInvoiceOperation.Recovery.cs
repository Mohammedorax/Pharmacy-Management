using System;
using System.Data.SqlClient;
using PharmacyManagement.Application;
namespace project_fo_3
{
 internal sealed class SalesWriteExecutionContext
 {
  internal string Source{get;}
  internal long Revision{get;}
  internal SalesWriteExecutionContext(string source,long revision){if(string.IsNullOrWhiteSpace(source)||revision<0)throw new ArgumentException("سياق تنفيذ البيع غير صالح.");Source=source;Revision=revision;}
 }
 internal sealed class SalesAttemptAbandonedException:InvalidOperationException
 {
  internal Guid RequestId{get;}
  internal SalesAttemptAbandonedException(Guid id):base("أُغلقت محاولة البيع دون اعتماد؛ لا يمكن إعادة إرسالها."){RequestId=id;}
 }
 internal sealed class SalesReceiptObservation
 {
  internal SalesInvoiceIssueResult Result{get;set;}
  internal bool IsCancelled{get;set;}
  internal bool IsAbandoned{get;set;}
  internal Guid RequestId{get;set;}
 }
 internal sealed partial class SalesInvoiceOperation
 {
  Func<bool> CaptureSalesIntentContext(SalesWriteIntent intent,bool writing,out string source,SalesWriteExecutionContext execution=null){
   if(intent==null)throw new ArgumentNullException(nameof(intent));
   source=execution?.Source??Database.ConnectionString;string capturedSource=source;long revision=execution?.Revision??SessionContext.CurrentSessionRevision;var owner=intent.Owner;
   if(intent.SourceFingerprint!=SalesWriteSourceFingerprint.Compute(source))throw new UnauthorizedAccessException("تغير مصدر محاولة البيع.");
   Func<bool> current=()=>SessionContext.IsAuthenticated&&SessionContext.CurrentSessionRevision==revision&&Database.ConnectionString==capturedSource&&SessionContext.CurrentEmployeeId==intent.OwnerEmployeeId&&string.Equals(SessionContext.CurrentUser,owner.UserName,StringComparison.OrdinalIgnoreCase)&&string.Equals(SessionContext.CurrentRole,owner.UserRole,StringComparison.OrdinalIgnoreCase)&&(writing?PermissionService.CanExecute(AppOperation.PostSalesInvoice):PermissionService.CanOpen(AppPermission.Sales));
   if(!current())throw new UnauthorizedAccessException("تغير صاحب محاولة البيع أو صلاحياته.");return current;
  }
  internal SalesInvoiceIssueResult IssueIdentified(SalesWriteIntent intent,Action<SalesInvoiceIssueResult> afterCommit=null,SalesWriteExecutionContext execution=null){
   string source;var current=CaptureSalesIntentContext(intent,true,out source,execution);
   var result=IssueCore(intent.Snapshot(),intent,current,source);if(!current())throw new UnauthorizedAccessException("تغيرت الجلسة بعد حفظ البيع؛ تحقق من المحاولة الأصلية.");afterCommit?.Invoke(result);return result;
  }
  // An absent receipt is an observation, never authority to discard a durable pending attempt.
  internal SalesReceiptObservation ObserveIdentified(SalesWriteIntent intent,SalesWriteExecutionContext execution=null){
   string source;var current=CaptureSalesIntentContext(intent,false,out source,execution);
   using(var c=new SqlConnection(source)){c.Open();if(!current())throw new UnauthorizedAccessException("تغيرت جلسة استرجاع البيع.");using(var tx=c.BeginTransaction()){
    SalesCommitAuthority.Acquire(c,tx,intent,false);
    try{
     SalesInvoiceIssueResult result=null;SalesReceiptObservation observation=null;
     try{result=SalesWriteReceiptStore.AcquireAndRead(c,tx,intent);}catch(SalesAttemptAbandonedException closed){if(closed.RequestId!=intent.RequestId)throw;observation=new SalesReceiptObservation{RequestId=intent.RequestId,IsAbandoned=true};}
     if(!current())throw new UnauthorizedAccessException("تغيرت جلسة استرجاع البيع.");
     if(result!=null)observation=ObserveSavedResult(c,tx,intent,result);
     if(!current())throw new UnauthorizedAccessException("تغيرت جلسة استرجاع البيع.");tx.Rollback();return observation;
    }catch{try{tx.Rollback();}catch{}throw;}
   }}
  }
  static SalesReceiptObservation ObserveSavedResult(SqlConnection c,SqlTransaction tx,SalesWriteIntent intent,SalesInvoiceIssueResult result){
   SalesWriteReceiptStore.VerifyPersisted(c,tx,intent,result);using(var q=new SqlCommand("SELECT TOP(1) 1 FROM dbo.JournalEntriesTb WITH(HOLDLOCK) WHERE SourceType=N'SalesInvoiceCancellation' AND SourceId=@Ref",c,tx)){
    q.Parameters.AddWithValue("@Ref",result.AccountingSourceId);return new SalesReceiptObservation{RequestId=intent.RequestId,Result=result,IsCancelled=q.ExecuteScalar()!=null};
   }
  }
  internal SalesReceiptObservation AbandonUnposted(SalesWriteIntent intent,Action afterCommit=null,SalesWriteExecutionContext execution=null){
   string source;var current=CaptureSalesIntentContext(intent,false,out source,execution);
   using(var c=new SqlConnection(source)){c.Open();if(!current())throw new UnauthorizedAccessException("تغيرت جلسة إغلاق المحاولة.");using(var tx=c.BeginTransaction()){
    SalesCommitAuthority.Acquire(c,tx,intent,false);
    try{
     SalesInvoiceIssueResult saved=null;
     try{saved=SalesWriteReceiptStore.AcquireAndRead(c,tx,intent);}catch(SalesAttemptAbandonedException closed){if(closed.RequestId!=intent.RequestId)throw;if(!current())throw new UnauthorizedAccessException("تغيرت جلسة إغلاق المحاولة.");tx.Rollback();return new SalesReceiptObservation{RequestId=intent.RequestId,IsAbandoned=true};}
     if(!current())throw new UnauthorizedAccessException("تغيرت جلسة إغلاق المحاولة.");
     if(saved!=null){var observed=ObserveSavedResult(c,tx,intent,saved);if(!current())throw new UnauthorizedAccessException("تغيرت جلسة إغلاق المحاولة.");tx.Rollback();return observed;}
     SalesWriteReceiptStore.RecordAbandoned(c,tx,intent);if(!current())throw new UnauthorizedAccessException("تغيرت جلسة إغلاق المحاولة.");using(SessionCommitGate.Commit(current)){tx.Commit();}afterCommit?.Invoke();return new SalesReceiptObservation{RequestId=intent.RequestId,IsAbandoned=true};
    }catch{try{tx.Rollback();}catch{}throw;}
   }}
  }
 }
}

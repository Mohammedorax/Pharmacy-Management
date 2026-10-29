using System;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Application;
namespace project_fo_3
{
    internal sealed class VoucherOperationRecovery : IDisposable
    {
        private readonly VoucherPendingStore store;
        private readonly Func<bool> current;
        private readonly Func<VoucherPostRequest,CancellationToken,Task<VoucherPostResult>> post;
        private readonly Func<VoucherSubmissionStatusRequest,CancellationToken,Task<VoucherSubmissionStatus>> status;
        private bool busy,disposed;
        internal VoucherPendingRequest Pending {get;private set;}
        internal VoucherPostResult Accepted {get;private set;}
        internal bool AcceptedContentMatchesRequest {get;private set;}=true;
        internal Exception CleanupError {get;private set;}
        internal bool CanCorrectRejected {get;private set;}
        internal VoucherOperationRecovery(VoucherPendingStore store,Func<bool> current,Func<VoucherPostRequest,Task<VoucherPostResult>> post,Func<VoucherSubmissionStatusRequest,CancellationToken,Task<VoucherSubmissionStatus>> status)
            : this(store,current,AdaptPost(post),status) { }
        private static Func<VoucherPostRequest,CancellationToken,Task<VoucherPostResult>> AdaptPost(Func<VoucherPostRequest,Task<VoucherPostResult>> post)
        { return post==null?null:new Func<VoucherPostRequest,CancellationToken,Task<VoucherPostResult>>((request,token)=>post(request)); }
        internal VoucherOperationRecovery(VoucherPendingStore store,Func<bool> current,Func<VoucherPostRequest,CancellationToken,Task<VoucherPostResult>> post,Func<VoucherSubmissionStatusRequest,CancellationToken,Task<VoucherSubmissionStatus>> status)
        {
            this.store=store??throw new ArgumentNullException(nameof(store));this.current=current??throw new ArgumentNullException(nameof(current));this.post=post??throw new ArgumentNullException(nameof(post));this.status=status??throw new ArgumentNullException(nameof(status));
            EnsureCurrent();Pending=store.Load();
        }
        private void EnsureCurrent(){if(disposed)throw new ObjectDisposedException(nameof(VoucherOperationRecovery));if(!current())throw new UnauthorizedAccessException("تغيرت جلسة السند؛ أعد الدخول بنفس المستخدم والاتصال للاستعادة.");}
        internal void Prepare(VoucherPendingRequest pending)
        {
            EnsureCurrent();if(busy)throw new InvalidOperationException("طلب السند قيد التنفيذ.");if(pending==null)throw new ArgumentNullException(nameof(pending));
            if(Pending!=null&&!Pending.HasSameState(pending))throw new InvalidOperationException("لا يمكن تبديل طلب السند غير المؤكد.");
            store.Save(pending);Pending=pending;Accepted=null;CleanupError=null;AcceptedContentMatchesRequest=true;CanCorrectRejected=false;
        }
        internal void CorrectRejected(VoucherPendingRequest corrected)
        {
            EnsureCurrent();if(busy||!CanCorrectRejected||Pending==null||Accepted!=null)throw new InvalidOperationException("لا يجوز تعديل سند ذي نتيجة غير محسومة.");
            if(Pending.DraftInputs!=null&&(corrected==null||corrected.DraftInputs==null))throw new InvalidOperationException("صحح هذا الطلب من محرر السند المفصل للحفاظ على مدخلات الشيك والتحويل الأصلية.");
            store.ReplaceRejected(Pending,corrected);Pending=corrected;CanCorrectRejected=false;
        }
        private void ResolveAccepted()
        {try{store.Resolve(Pending.Request.RequestId.Value);Pending=null;CleanupError=null;}catch(Exception error){CleanupError=error;}}
        internal bool RetryAcceptedCleanup(){EnsureCurrent();if(busy||Accepted==null)return false;if(Pending!=null)ResolveAccepted();return Pending==null;}
        internal async Task<VoucherPostResult> ExecuteAsync(CancellationToken token)
        {
            EnsureCurrent();token.ThrowIfCancellationRequested();if(busy||Pending==null)throw new InvalidOperationException("لا يوجد طلب سند ثابت جاهز أو أن طلبًا قيد التنفيذ.");busy=true;
            try
            {
                if(Accepted==null)
                {
                    CanCorrectRejected=false;var request=Pending.Request;var result=await status(new VoucherSubmissionStatusRequest(request),token);
                    token.ThrowIfCancellationRequested();EnsureCurrent();if(result==null)throw new InvalidOperationException("تعذر تحديد حالة اعتماد السند.");
                    VoucherPostResult accepted;
                    if(result.IsPosted)
                    {
                        if(!result.ContentMatches.HasValue)throw new InvalidOperationException("لم تثبت مقارنة السند المعتمد؛ لم يرسل اعتماد جديد.");
                        AcceptedContentMatchesRequest=result.ContentMatches.Value;accepted=new VoucherPostResult(result.VoucherId,result.JournalEntryId,true);
                    }
                    else
                    {
                        try{accepted=await post(request,token);}
                        catch(VoucherPostRejectedException rejection)
                        {
                            if(rejection.RequestId!=request.RequestId.Value)throw;
                            token.ThrowIfCancellationRequested();EnsureCurrent();CanCorrectRejected=true;throw;
                        }
                        catch(VoucherAcceptedRequestConflictException conflict)
                        {
                            if(conflict.RequestId!=request.RequestId.Value)throw;
                            AcceptedContentMatchesRequest=false;accepted=new VoucherPostResult(conflict.VoucherId,conflict.JournalEntryId,true);
                        }
                    }
                    if(accepted==null||accepted.VoucherId<=0||accepted.JournalEntryId<=0)throw new InvalidOperationException("لم ترجع عملية السند معرفات صحيحة.");
                    // Known acceptance survives cancellation/session changes after commit.
                    Accepted=accepted;
                }
                ResolveAccepted();return Accepted;
            }
            finally{busy=false;}
        }
        public void Dispose(){if(!disposed){disposed=true;store.Dispose();}}
    }
}
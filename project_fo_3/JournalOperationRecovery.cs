using System;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Application;

namespace project_fo_3
{
    // The same frozen request is used until acceptance is known. Loading is passive.
    internal sealed class JournalOperationRecovery : IDisposable
    {
        private readonly JournalPendingStore store;
        private readonly Func<bool> current;
        private readonly Func<JournalPostRequest,CancellationToken,Task<int>> post;
        private readonly Func<JournalSubmissionStatusRequest, CancellationToken, Task<JournalSubmissionStatus>> status;
        private bool busy, disposed;
        internal JournalPendingRequest Pending { get; private set; }
        internal int AcceptedId { get; private set; }
        internal bool AcceptedContentMatchesRequest { get; private set; } = true;
        internal Exception CleanupError { get; private set; }
        internal bool CanCorrectRejected { get; private set; }
        internal JournalOperationRecovery(JournalPendingStore store, Func<bool> current,
            Func<JournalPostRequest, Task<int>> post,
            Func<JournalSubmissionStatusRequest, CancellationToken, Task<JournalSubmissionStatus>> status)
            : this(store,current,AdaptPost(post),status) { }
        private static Func<JournalPostRequest,CancellationToken,Task<int>> AdaptPost(Func<JournalPostRequest,Task<int>> post)
        { return post==null?null:new Func<JournalPostRequest,CancellationToken,Task<int>>((request,token)=>post(request)); }
        internal JournalOperationRecovery(JournalPendingStore store,Func<bool> current,
            Func<JournalPostRequest,CancellationToken,Task<int>> post,
            Func<JournalSubmissionStatusRequest,CancellationToken,Task<JournalSubmissionStatus>> status)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.current = current ?? throw new ArgumentNullException(nameof(current));
            this.post = post ?? throw new ArgumentNullException(nameof(post));
            this.status = status ?? throw new ArgumentNullException(nameof(status));
            EnsureCurrent(); Pending = store.Load();
        }
        private void EnsureCurrent()
        {
            if (disposed) throw new ObjectDisposedException(nameof(JournalOperationRecovery));
            if (!current()) throw new UnauthorizedAccessException("تغيرت جلسة طلب القيد؛ أعد الدخول بنفس المستخدم والاتصال للاستعادة.");
        }
        internal void Prepare(JournalPendingRequest pending)
        {
            EnsureCurrent();
            if (busy) throw new InvalidOperationException("طلب القيد قيد التنفيذ.");
            if (pending == null) throw new ArgumentNullException(nameof(pending));
            if (Pending != null && !Pending.HasSameContent(pending.Request)) throw new InvalidOperationException("لا يمكن تبديل طلب القيد غير المؤكد.");
            store.Save(pending); Pending = pending; AcceptedId = 0; CleanupError = null; AcceptedContentMatchesRequest = true; CanCorrectRejected = false;
        }
        internal void CorrectRejected(JournalPendingRequest corrected)
        {
            EnsureCurrent();
            if (busy || !CanCorrectRejected || Pending == null || AcceptedId > 0)
                throw new InvalidOperationException("لا يجوز تعديل طلب ذي نتيجة غير مؤكدة.");
            store.ReplaceRejected(Pending, corrected);
            Pending = corrected; CanCorrectRejected = false;
        }
        private void ResolveAccepted()
        {
            try { store.Resolve(Pending.Request.SourceId); Pending = null; CleanupError = null; }
            catch (Exception error) { CleanupError = error; /* Known commit must stay a success. Keep the immutable record. */ }
        }
        internal bool RetryAcceptedCleanup()
        {
            EnsureCurrent();
            if (busy || AcceptedId <= 0) return false;
            if (Pending != null) ResolveAccepted();
            return Pending == null;
        }
        internal async Task<int> ExecuteAsync(CancellationToken token)
        {
            EnsureCurrent(); token.ThrowIfCancellationRequested();
            if (busy || Pending == null) throw new InvalidOperationException("لا يوجد طلب ثابت جاهز أو أن طلبًا قيد التنفيذ.");
            busy = true;
            try
            {
                if (AcceptedId <= 0)
                {
                    var request = Pending.Request; CanCorrectRejected = false;
                    var result = await status(new JournalSubmissionStatusRequest(request.SourceType, request.SourceId, request), token);
                    token.ThrowIfCancellationRequested(); EnsureCurrent();
                    if (result == null) throw new InvalidOperationException("تعذر تحديد حالة اعتماد القيد.");
                    int id;
                    if (result.IsPosted)
                    {
                        if (!result.ContentMatches.HasValue) throw new InvalidOperationException("لم تثبت مطابقة بيانات القيد المعتمد؛ لم يرسل ترحيل جديد.");
                        AcceptedContentMatchesRequest = result.ContentMatches.Value; id = result.JournalId;
                    }
                    else
                    {
                        try { id = await post(request,token); }
                        catch (JournalPostRejectedException rejection)
                        {
                            if (rejection.SourceType != request.SourceType || rejection.SourceId != request.SourceId) throw;
                            token.ThrowIfCancellationRequested();EnsureCurrent(); CanCorrectRejected = true; throw;
                        }
                        catch (JournalAcceptedRequestConflictException conflict)
                        {
                            if (conflict.SourceType != request.SourceType || conflict.SourceId != request.SourceId) throw;
                            AcceptedContentMatchesRequest = false; id = conflict.JournalId;
                        }
                    }
                    if (id <= 0) throw new InvalidOperationException("لم ترجع عملية القيد معرّفًا صالحًا.");
                    // A known post result stays accepted even if the session/window changes after commit.
                    AcceptedId = id;
                }
                ResolveAccepted();
                return AcceptedId;
            }
            finally { busy = false; }
        }
        public void Dispose() { if (!disposed) { disposed = true; store.Dispose(); } }
    }
}

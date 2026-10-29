using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Application;
using PharmacyManagement.Domain;

namespace project_fo_3
{
    internal static class JournalOperationRecoverySelfTests
    {
        private static int checks;
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); ++checks; }
        private static async Task Reject<T>(Func<Task> action) where T : Exception
        { try { await action().ConfigureAwait(false); } catch (T) { ++checks; return; } throw new InvalidOperationException("Expected " + typeof(T).Name); }
        internal static void Verify() { Run().GetAwaiter().GetResult(); }
        private static async Task Run()
        {
            checks = 0;
            string directory = Path.Combine(Path.GetTempPath(), "PharmacyJournalRecovery-" + Guid.NewGuid().ToString("N"));
            string context = new string('d', 64);
            var pending = JournalPendingRequest.Create(new DateTime(2026, 10, 5), "Manual", "قيد Cash 500 mg", new[] {
                new JournalLineValues("1000", 1250.50m, 0, "مدين A&B", null), new JournalLineValues("2000", 0, 1250.50m, null, 7) });
            int posts = 0, reads = 0, committed = 0;
            Func<JournalSubmissionStatusRequest, CancellationToken, Task<JournalSubmissionStatus>> status = (request, token) => {
                ++reads; Check(request.SourceId == pending.Request.SourceId && request.SourceType == "Manual", "Status changed source identity.");
                return Task.FromResult(new JournalSubmissionStatus(committed,committed>0?(bool?)true:null)); };
            try
            {
                using (var recovery = new JournalOperationRecovery(new JournalPendingStore(directory, context, "Manual"), () => true,
                    request => { ++posts; Check(pending.HasSameContent(request), "Dispatch changed pending payload."); committed = 98;
                        return Task.FromException<int>(new IOException("Lost commit reply")); }, status))
                {
                    Check(posts == 0 && reads == 0 && recovery.Pending == null, "Loading automatically contacted persistence services.");
                    recovery.Prepare(pending);
                    using (var duplicate = new JournalPendingStore(directory, new string('e', 64), "Manual")) Check(duplicate.Load() == null, "Context leaked request.");
                    await Reject<IOException>(async () => { await recovery.ExecuteAsync(CancellationToken.None); }).ConfigureAwait(false);
                    Check(recovery.Pending.HasSameContent(pending.Request) && recovery.AcceptedId == 0, "Unknown post reply discarded pending request.");
                    await Reject<InvalidOperationException>(() => { recovery.Prepare(JournalPendingRequest.Create(pending.Request.Date, "Manual", "different", pending.Request.Lines)); return Task.CompletedTask; }).ConfigureAwait(false);
                }
                using (var restarted = new JournalOperationRecovery(new JournalPendingStore(directory, context, "Manual"), () => true,
                    request => { ++posts; return Task.FromResult(99); }, status))
                {
                    Check(restarted.Pending.HasSameContent(pending.Request) && posts == 1 && reads == 1, "Restore changed identity or automatically posted.");
                    int id = await restarted.ExecuteAsync(CancellationToken.None).ConfigureAwait(false);
                    Check(id == 98 && posts == 1 && reads == 2 && restarted.Pending == null, "Lost reply retry duplicated committed request.");
                    Check(restarted.RetryAcceptedCleanup(), "Accepted cleanup was not idempotent.");
                }
                committed = 0;
                bool current = true;
                var blockedStatus = new TaskCompletionSource<JournalSubmissionStatus>();
                using (var recovery = new JournalOperationRecovery(new JournalPendingStore(directory, context, "Manual"), () => current,
                    request => { ++posts; return Task.FromResult(100); }, (request, token) => blockedStatus.Task))
                {
                    recovery.Prepare(pending); var first = recovery.ExecuteAsync(CancellationToken.None);
                    await Reject<InvalidOperationException>(async () => { await recovery.ExecuteAsync(CancellationToken.None); }).ConfigureAwait(false);
                    current = false; blockedStatus.SetResult(new JournalSubmissionStatus(0));
                    await Reject<UnauthorizedAccessException>(async () => { await first; }).ConfigureAwait(false);
                    Check(posts == 1 && recovery.Pending != null, "Stale status dispatched or deleted request."); current = true;
                }
                var postCompletion = new TaskCompletionSource<int>();
                var duringPost = new JournalOperationRecovery(new JournalPendingStore(directory, context, "Manual"), () => current,
                    request => { ++posts; return postCompletion.Task; }, status);
                var dispatch = duringPost.ExecuteAsync(CancellationToken.None);
                Check(posts == 2 && !dispatch.IsCompleted, "Expected pending post not reached.");
                duringPost.Dispose(); current = false; committed = 102; postCompletion.SetResult(102);
                Check(await dispatch.ConfigureAwait(false) == 102 && duringPost.AcceptedId == 102 && duringPost.CleanupError != null,
                    "Disposal after commit turned known acceptance into failure.");
                current = true;
                using (var recovery = new JournalOperationRecovery(new JournalPendingStore(directory, context, "Manual"), () => current,
                    request => { ++posts; return Task.FromResult(103); }, status))
                {
                    Check(recovery.Pending != null, "Disposal erased unresolved record.");
                    Check(await recovery.ExecuteAsync(CancellationToken.None).ConfigureAwait(false) == 102 && posts == 2,
                        "Reopened cleanup posted known committed request again.");
                }
                using (var recovery = new JournalOperationRecovery(new JournalPendingStore(directory, context, "Manual"), () => true,
                    request => { ++posts; return Task.FromResult(104); }, (request, token) => Task.FromException<JournalSubmissionStatus>(new IOException("Unavailable"))))
                {
                    recovery.Prepare(pending);
                    await Reject<IOException>(async () => { await recovery.ExecuteAsync(CancellationToken.None); }).ConfigureAwait(false);
                    Check(posts == 2 && recovery.Pending != null, "Unavailable status implicitly posted or removed request.");
                }
                using (var cancellation = new CancellationTokenSource())
                using (var recovery = new JournalOperationRecovery(new JournalPendingStore(directory, context, "Manual"), () => true,
                    request => { ++posts; return Task.FromResult(105); }, (request, token) => {
                        Check(token == cancellation.Token, "Cancellation token was lost."); cancellation.Cancel(); return Task.FromResult(new JournalSubmissionStatus(0)); }))
                {
                    await Reject<OperationCanceledException>(async () => { await recovery.ExecuteAsync(cancellation.Token); }).ConfigureAwait(false);
                    Check(posts == 2 && recovery.Pending != null, "Cancellation after read still dispatched.");
                }
                using(var recovery=new JournalOperationRecovery(new JournalPendingStore(directory,context,"Manual"),()=>true,
                    request=>{++posts;return Task.FromResult(110);},(request,token)=>{
                        Check(request.ExpectedRequest!=null&&pending.HasSameContent(request.ExpectedRequest),"Expected payload was not captured.");
                        return Task.FromResult(new JournalSubmissionStatus(111,false));}))
                {
                    Check(await recovery.ExecuteAsync(CancellationToken.None).ConfigureAwait(false)==111&&posts==2&&
                        !recovery.AcceptedContentMatchesRequest&&recovery.Pending==null,"Different accepted content was posted or reported as matching.");
                }
                using(var recovery=new JournalOperationRecovery(new JournalPendingStore(directory,context,"Manual"),()=>true,
                    request=>{++posts;return Task.FromResult(112);},(request,token)=>Task.FromResult(new JournalSubmissionStatus(111))))
                {
                    recovery.Prepare(pending);
                    await Reject<InvalidOperationException>(async()=>{await recovery.ExecuteAsync(CancellationToken.None);}).ConfigureAwait(false);
                    Check(posts==2&&recovery.Pending!=null,"Unproved acceptance cleared or posted pending request.");
                }
                using(var recovery=new JournalOperationRecovery(new JournalPendingStore(directory,context,"Manual"),()=>true,
                    request=>{++posts;return Task.FromException<int>(new JournalAcceptedRequestConflictException(113,request.SourceType,request.SourceId));},
                    (request,token)=>Task.FromResult(new JournalSubmissionStatus(0))))
                {
                    Check(await recovery.ExecuteAsync(CancellationToken.None).ConfigureAwait(false)==113&&posts==3&&
                        !recovery.AcceptedContentMatchesRequest&&recovery.Pending==null,"Known source conflict encouraged another post or claimed matching data.");
                }
                using(var recovery=new JournalOperationRecovery(new JournalPendingStore(directory,context,"Manual"),()=>true,
                    request=>Task.FromException<int>(new JournalAcceptedRequestConflictException(114,"Manual",Guid.NewGuid().ToString("N"))),
                    (request,token)=>Task.FromResult(new JournalSubmissionStatus(0))))
                {
                    recovery.Prepare(pending);
                    await Reject<JournalAcceptedRequestConflictException>(async()=>{await recovery.ExecuteAsync(CancellationToken.None);}).ConfigureAwait(false);
                    Check(recovery.AcceptedId==0&&recovery.Pending.HasSameContent(pending.Request),"Unrelated conflict deleted original pending request.");
                }
                var corrected=JournalPendingRequest.Restore(new JournalPostRequest(pending.Request.Date,"Manual",pending.Request.SourceId,
                    "بيان مُصحّح Cash 500 mg",pending.Request.Lines));
                int correctedPosts=0;
                using(var recovery=new JournalOperationRecovery(new JournalPendingStore(directory,context,"Manual"),()=>true,
                    request=>{
                        ++correctedPosts;
                        if(correctedPosts==1)return Task.FromException<int>(new JournalPostRejectedException(request.SourceType,request.SourceId,new InvalidOperationException("Owned rollback")));
                        Check(corrected.HasSameContent(request),"Corrected submission lost exact payload.");
                        return Task.FromResult(115);
                    },(request,token)=>Task.FromResult(new JournalSubmissionStatus(0))))
                {
                    await Reject<InvalidOperationException>(()=>{recovery.CorrectRejected(corrected);return Task.CompletedTask;}).ConfigureAwait(false);
                    await Reject<JournalPostRejectedException>(async()=>{await recovery.ExecuteAsync(CancellationToken.None);}).ConfigureAwait(false);
                    Check(recovery.CanCorrectRejected&&recovery.Pending.HasSameContent(pending.Request),"Acknowledged rejection discarded original or failed to allow correction.");
                    await Reject<InvalidOperationException>(()=>{recovery.CorrectRejected(JournalPendingRequest.Create(pending.Request.Date,"Manual","new id",pending.Request.Lines));return Task.CompletedTask;}).ConfigureAwait(false);
                    string record=Path.Combine(directory,context+"-Manual.pending");
                    using(var held=new FileStream(record,FileMode.Open,FileAccess.Read,FileShare.Read))
                        await Reject<IOException>(()=>{recovery.CorrectRejected(corrected);return Task.CompletedTask;}).ConfigureAwait(false);
                    Check(recovery.CanCorrectRejected&&recovery.Pending.HasSameContent(pending.Request),"Failed atomic replacement changed in-memory pending request.");
                    recovery.CorrectRejected(corrected);
                    Check(!recovery.CanCorrectRejected&&recovery.Pending.HasSameContent(corrected.Request)&&recovery.Pending.Request.SourceId==pending.Request.SourceId,"Correction changed identity or stayed editable before status check.");
                }
                using(var recovery=new JournalOperationRecovery(new JournalPendingStore(directory,context,"Manual"),()=>true,
                    request=>{++correctedPosts;Check(corrected.HasSameContent(request),"Restart lost corrected payload.");return Task.FromResult(115);},
                    (request,token)=>Task.FromResult(new JournalSubmissionStatus(0))))
                {
                    Check(recovery.Pending.HasSameContent(corrected.Request)&&!recovery.CanCorrectRejected,"Restart silently unlocked a persisted request.");
                    Check(await recovery.ExecuteAsync(CancellationToken.None).ConfigureAwait(false)==115&&correctedPosts==2&&recovery.Pending==null,"Corrected same-ID request did not resolve.");
                }
                using(var recovery=new JournalOperationRecovery(new JournalPendingStore(directory,context,"Manual"),()=>true,
                    request=>Task.FromException<int>(new JournalPostRejectedException("Manual",Guid.NewGuid().ToString("N"),new IOException("Unrelated rejection"))),
                    (request,token)=>Task.FromResult(new JournalSubmissionStatus(0))))
                {
                    recovery.Prepare(pending);
                    await Reject<JournalPostRejectedException>(async()=>{await recovery.ExecuteAsync(CancellationToken.None);}).ConfigureAwait(false);
                    Check(!recovery.CanCorrectRejected&&recovery.Pending.HasSameContent(pending.Request),"Unrelated rejection unlocked frozen request.");
                }
                Console.WriteLine("PASS: " + checks + " journal operation recovery checks: persist/freeze, lost commit reply, passive reopen, no duplicate post, concurrent dispatch denial, stale status/cancellation/unavailable denial, known acceptance after disposal and deferred cleanup; no SQL or UI.");
            }
            finally
            {
                string resolved = Path.GetFullPath(directory), temporaryRoot = Path.GetFullPath(Path.GetTempPath());
                if (!resolved.StartsWith(temporaryRoot, StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(resolved).StartsWith("PharmacyJournalRecovery-")) throw new InvalidOperationException("Cleanup escaped owned temporary root.");
                if (Directory.Exists(resolved)) Directory.Delete(resolved, true);
            }
        }
    }
}

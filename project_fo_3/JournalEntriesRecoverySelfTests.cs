using System;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using PharmacyManagement.Application;
using PharmacyManagement.Domain;

namespace project_fo_3
{
    internal static class JournalEntriesRecoverySelfTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        private static void Invoke(JournalEntriesForm form, string name) => typeof(JournalEntriesForm).GetMethod(name, Private).Invoke(form, null);
        private static T Field<T>(JournalEntriesForm form, string name) => (T)typeof(JournalEntriesForm).GetField(name, Private).GetValue(form);
        private static void Pump(Task task)
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            while (!task.IsCompleted && timer.Elapsed < TimeSpan.FromSeconds(8)) { Application.DoEvents(); System.Threading.Thread.Sleep(5); }
            if (!task.IsCompleted) throw new TimeoutException("Owned recovery editor did not complete."); task.GetAwaiter().GetResult();
        }
        internal static void Verify()
        {
            string originalTheme = Properties.Setings.Default.ThemeMode;
            int cases = 0;
            try
            {
                foreach (string theme in new[] { "Light", "Dark" })
                foreach (bool ltr in new[] { false, true })
                foreach (string kind in new[] { "Manual", "OpeningEntry" })
                {
                    Properties.Setings.Default.ThemeMode = theme; SessionContext.Start("recovery-editor-fixture", "Manager", "QA-RECOVERY");
                    string directory = Path.Combine(Path.GetTempPath(), "PharmacyJournalEditor-" + Guid.NewGuid().ToString("N"));
                    string context = new string('f', 64);
                    var pending = JournalPendingRequest.Create(new DateTime(2026, 10, 5), kind, "قيد مُشكّل Cash 500 mg", new[] {
                        new JournalLineValues("1000",1250.50m,0,"باراسيتامول 500 mg A&B",null),new JournalLineValues("2000",0,1250.50m,null,7) });
                    int reads = 0, posts = 0, committed = 0;
                    try
                    {
                        using (var store = new JournalPendingStore(directory, context, kind)) store.Save(pending);
                        using (var form = new JournalEntriesForm("استعادة قيد", kind, true))
                        {
                            form.ShowInTaskbar = false; form.StartPosition = FormStartPosition.Manual; form.Location = new System.Drawing.Point(-20000, -20000);
                            form.Show(); Application.DoEvents(); UiDirection.Apply(form, ltr);
                            var recovery = new JournalOperationRecovery(new JournalPendingStore(directory, context, kind), () => true,
                                request => { ++posts; Check(pending.HasSameContent(request), "Editor dispatched altered frozen request."); committed = 82;
                                    return Task.FromException<int>(new IOException("Owned lost response fixture")); },
                                (request, token) => { ++reads; return Task.FromResult(new JournalSubmissionStatus(committed,committed>0?(bool?)true:null)); });
                            typeof(JournalEntriesForm).GetField("journalRecovery", Private).SetValue(form, recovery);
                            Invoke(form, "RestoreJournalPendingPresentation"); Invoke(form, "ApplyJournalRecoveryLock");
                            var rows = Field<DataTable>(form, "lines");
                            Check(reads == 0 && posts == 0 && rows.Rows.Count == 2, "Opening editor automatically queried or posted.");
                            Check(Field<TextBox>(form, "headerDescription").Text == pending.Request.Description &&
                                Convert.ToDecimal(rows.Rows[0]["مدين محلي"]) == 1250.50m && Convert.ToInt32(rows.Rows[1]["CostCenterId"]) == 7,
                                "Restored editor changed header, amount or cost center.");
                            Check(!Field<Panel>(form, "journalEditorViewport").Enabled && !Field<DataGridView>(form, "linesGrid").Enabled,
                                "Uncertain editor remained editable.");
                            Invoke(form, "NewEntry"); Invoke(form, "ResetJournalDraft"); Invoke(form, "RemoveSelectedLine"); Invoke(form, "AddLine");
                            Check(rows.Rows.Count == 2 && recovery.Pending.HasSameContent(pending.Request), "New/reset/line command replaced uncertain request.");
                            var first = form.RunJournalWriteAsync(() => recovery.ExecuteAsync(System.Threading.CancellationToken.None), false);
                            Pump(first); Check(first.Result == 0 && posts == 1 && reads == 1 && recovery.Pending != null && rows.Rows.Count == 2,
                                "Lost reply cleared uncertain editor or changed request.");
                            var retry = form.RunJournalWriteAsync(() => recovery.ExecuteAsync(System.Threading.CancellationToken.None), false);
                            Pump(retry); Check(retry.Result == 82 && posts == 1 && reads == 2 && recovery.Pending == null && rows.Rows.Count == 0,
                                "Editor retry duplicated post or failed to clear accepted draft.");
                            Check(Field<Panel>(form, "journalEditorViewport").Enabled && Field<DataGridView>(form, "linesGrid").Enabled,
                                "Accepted draft did not unlock editor.");
                            recovery.Dispose();
                            int rejectedPosts=0;
                            var rejectedRecovery=new JournalOperationRecovery(new JournalPendingStore(directory,context,kind),()=>true,
                                request=>{++rejectedPosts;return rejectedPosts==1?
                                    Task.FromException<int>(new JournalPostRejectedException(request.SourceType,request.SourceId,new InvalidOperationException("Owned precommit rollback"))):
                                    Task.FromResult(83);},(request,token)=>Task.FromResult(new JournalSubmissionStatus(0)));
                            typeof(JournalEntriesForm).GetField("journalRecovery",Private).SetValue(form,rejectedRecovery);
                            rejectedRecovery.Prepare(pending);Invoke(form,"RestoreJournalPendingPresentation");Invoke(form,"ApplyJournalRecoveryLock");
                            var rejected=form.RunJournalWriteAsync(()=>rejectedRecovery.ExecuteAsync(System.Threading.CancellationToken.None),false);Pump(rejected);
                            Check(rejected.Result==0&&rejectedRecovery.CanCorrectRejected&&rows.Rows.Count==2&&
                                Field<Panel>(form,"journalEditorViewport").Enabled&&Field<Label>(form,"statusLabel").Text.Contains("أُكد التراجع"),
                                "Confirmed rejection did not retain editable draft with distinct guidance.");
                            var corrected=JournalPendingRequest.Restore(new JournalPostRequest(pending.Request.Date,kind,pending.Request.SourceId,"بيان مُصحح 500 mg",pending.Request.Lines));
                            rejectedRecovery.CorrectRejected(corrected);Invoke(form,"RestoreJournalPendingPresentation");Invoke(form,"ApplyJournalRecoveryLock");
                            Check(!Field<Panel>(form,"journalEditorViewport").Enabled,"Persisted corrected request was not frozen before recheck.");
                            var correctedWrite=form.RunJournalWriteAsync(()=>rejectedRecovery.ExecuteAsync(System.Threading.CancellationToken.None),false);Pump(correctedWrite);
                            Check(correctedWrite.Result==83&&rejectedPosts==2&&rows.Rows.Count==0&&rejectedRecovery.Pending==null,"Corrected request did not clear after acceptance.");
                            rejectedRecovery.Dispose();
                            var conflictRecovery=new JournalOperationRecovery(new JournalPendingStore(directory,context,kind),()=>true,
                                request=>{throw new InvalidOperationException("Mismatching acceptance must not post.");},
                                (request,token)=>Task.FromResult(new JournalSubmissionStatus(82,false)));
                            typeof(JournalEntriesForm).GetField("journalRecovery",Private).SetValue(form,conflictRecovery);
                            conflictRecovery.Prepare(pending);Invoke(form,"RestoreJournalPendingPresentation");Invoke(form,"ApplyJournalRecoveryLock");
                            var conflict=form.RunJournalWriteAsync(()=>conflictRecovery.ExecuteAsync(System.Threading.CancellationToken.None),false);
                            Pump(conflict);
                            Check(conflict.Result==82&&!conflictRecovery.AcceptedContentMatchesRequest&&rows.Rows.Count==2&&
                                Field<bool>(form,"journalDraftAccepted")&&!Field<Panel>(form,"journalEditorViewport").Enabled&&
                                Field<Label>(form,"statusLabel").Text.Contains("لم تعتمد التعديلات الحالية"),"Different saved data cleared edits or was presented as new success.");
                            Invoke(form,"NewEntry");
                            Check(rows.Rows.Count==0&&Field<Panel>(form,"journalEditorViewport").Enabled,"Explicit new did not unlock mismatching accepted draft.");
                            // Restore another uncertain record, then prove window disposal preserves it.
                            conflictRecovery.Prepare(pending); Invoke(form, "RestoreJournalPendingPresentation"); Invoke(form, "ApplyJournalRecoveryLock");
                            form.Dispose();
                        }
                        using (var reopened = new JournalPendingStore(directory, context, kind))
                        { Check(reopened.Load().HasSameContent(pending.Request), "Window disposal erased pending payload."); reopened.Resolve(pending.Request.SourceId); }
                        ++cases;
                    }
                    finally
                    {
                        string resolved = Path.GetFullPath(directory);
                        if (!resolved.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(resolved).StartsWith("PharmacyJournalEditor-")) throw new InvalidOperationException("Owned cleanup escaped temporary directory.");
                        if (Directory.Exists(resolved)) Directory.Delete(resolved, true);
                    }
                }
                Console.WriteLine("PASS: " + cases + " owned journal recovery editor cases: themes/directions/manual/opening, passive restore, exact local amount/center/header, frozen commands, lost reply/retry without duplicate, unlock after acceptance and disposal persistence. Injected collaborators; actual SaveEntryAsync and SQL end-to-end remain separate.");
            }
            finally { SessionContext.Clear(); Properties.Setings.Default.ThemeMode = originalTheme; }
        }
    }
}

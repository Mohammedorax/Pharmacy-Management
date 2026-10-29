using System;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using PharmacyManagement.Application;
using PharmacyManagement.Domain;

namespace project_fo_3
{
    internal static class QuickJournalRecoverySelfTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static T Field<T>(JournalEntriesQuickForm form, string name) => (T)typeof(JournalEntriesQuickForm).GetField(name, Private).GetValue(form);
        private static void Invoke(JournalEntriesQuickForm form, string name) => typeof(JournalEntriesQuickForm).GetMethod(name, Private).Invoke(form, null);
        private static T Control<T>(Form form, string name) where T : System.Windows.Forms.Control => (T)form.Controls.Find(name, true).Single();
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        private static bool Frozen(JournalEntriesQuickForm form) =>
            Control<Panel>(form, "accountingDataEditor").Enabled &&
            !Control<ComboBox>(form, "quickJournalDebitAccount").Enabled && !Control<ComboBox>(form, "quickJournalCreditAccount").Enabled &&
            Control<AntdUI.Input>(form, "quickJournalAmount").ReadOnly && Control<TextBox>(form, "quickJournalDescription").ReadOnly;
        private static void Pump(Task task)
        {
            var time = System.Diagnostics.Stopwatch.StartNew();
            while (!task.IsCompleted && time.Elapsed < TimeSpan.FromSeconds(8)) { Application.DoEvents(); Thread.Sleep(5); }
            if (!task.IsCompleted) throw new TimeoutException("Owned quick recovery fixture timed out.");
            task.GetAwaiter().GetResult();
        }
        private static DataTable History()
        {
            var rows = new DataTable();
            rows.Columns.Add("رقم القيد"); rows.Columns.Add("التاريخ"); rows.Columns.Add("المصدر");
            rows.Columns.Add("مدين", typeof(decimal)); rows.Columns.Add("دائن", typeof(decimal)); rows.Columns.Add("البيان");
            return rows;
        }
        private static JournalEntriesQuickForm Open(bool english)
        {
            var form = new JournalEntriesQuickForm(true) { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-20000, -20000) };
            form.Show(); form.ApplyQuickPresentation(english); Application.DoEvents(); return form;
        }
        private static void Attach(JournalEntriesQuickForm form, JournalOperationRecovery recovery, JournalPendingRequest pending)
        {
            typeof(JournalEntriesQuickForm).GetField("quickRecovery", Private).SetValue(form, recovery);
            if (recovery.Pending == null) recovery.Prepare(pending);
            Invoke(form, "RestoreQuickPending"); Invoke(form, "ApplyQuickRecoveryLock");
        }
        internal static void Verify()
        {
            string theme = Properties.Setings.Default.ThemeMode;
            string evidence = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(typeof(JournalEntriesQuickForm).Assembly.Location),
                "..", "..", "..", "artifacts", "quick-journal-recovery-ui-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8)));
            Directory.CreateDirectory(evidence);
            int cases = 0;
            try
            {
                foreach (string mode in new[] { "Light", "Dark" })
                foreach (bool english in new[] { false, true })
                {
                    Properties.Setings.Default.ThemeMode = mode;
                    string directory = Path.Combine(Path.GetTempPath(), "PharmacyQuickJournal-" + Guid.NewGuid().ToString("N"));
                    string context = new string('e', 64);
                    var pending = JournalPendingRequest.Create(new DateTime(2026, 10, 5, 13, 45, 6), "Manual", "قيد مُشكّل Cash 500 mg A&B",
                        new[] { new JournalLineValues("1000", 1250.50m, 0, "قيد مُشكّل Cash 500 mg A&B", null),
                            new JournalLineValues("2000", 0, 1250.50m, "قيد مُشكّل Cash 500 mg A&B", null) });
                    int reads = 0, posts = 0, committed = 0;
                    try
                    {
                        using (var form = Open(english))
                        {
                            var recovery = new JournalOperationRecovery(new JournalPendingStore(directory, context, "Manual"), () => true,
                                request => { ++posts; Check(pending.HasSameContent(request), "Frozen quick payload changed."); committed = 92;
                                    return Task.FromException<int>(new IOException("Owned lost reply")); },
                                (request, token) => { ++reads; return Task.FromResult(new JournalSubmissionStatus(committed, committed > 0 ? (bool?)true : null)); });
                            Attach(form, recovery, pending);
                            Check(reads == 0 && posts == 0 && Control<TextBox>(form, "quickJournalDescription").Text == pending.Request.Description &&
                                Control<AntdUI.Input>(form, "quickJournalAmount").Text == 1250.50m.ToString(System.Globalization.CultureInfo.CurrentCulture),
                                "Passive quick restore changed values or dispatched.");
                            Check(Frozen(form) && !Control<AntdUI.Button>(form, "quickJournalNew").Enabled,
                                "Uncertain quick fields or New remained editable.");
                            Invoke(form, "NewQuickDraft"); Check(recovery.Pending.HasSameContent(pending.Request), "New replaced uncertain quick request.");
                            foreach (var size in new[] { new Size(760, 600), new Size(960, 640), new Size(1366, 768), new Size(1920, 1080) })
                            {
                                OwnedTestWindowViewport.ResizeOuter(form, size); form.ApplyQuickJournalLayout();
                                var footer = Control<AntdUI.Panel>(form, "quickJournalActions");
                                var buttons = footer.Controls.OfType<AntdUI.Button>().ToArray();
                                Check(buttons.Length == 4 && buttons.All(button => footer.ClientRectangle.Contains(button.Bounds) && button.Height >= 44 &&
                                    !string.IsNullOrWhiteSpace(button.AccessibleName)), "Quick footer action clipped or lacked accessibility.");
                                Check(buttons.All(button => buttons.Where(other => other != button).All(other => !button.Bounds.IntersectsWith(other.Bounds))),
                                    "Quick footer actions overlapped.");
                                var save = Control<AntdUI.Button>(form, "quickJournalSave"); var create = Control<AntdUI.Button>(form, "quickJournalNew");
                                Check(english ? save.Left < create.Left : save.Left > create.Left, "New did not follow reading direction.");
                                var grid = Control<DataGridView>(form, "accountingDataGrid");
                                Check(grid.RightToLeft == (english ? RightToLeft.No : RightToLeft.Yes) &&
                                    grid.Columns["رقم القيد"].HeaderText == (english ? "Entry no." : "رقم القيد") &&
                                    ((DataTable)grid.DataSource).Columns.Contains("رقم القيد"),
                                    "Quick history display direction/header changed its raw column contract.");
                                var entryColumn = grid.Columns["رقم القيد"];
                                Check(TextRenderer.MeasureText(entryColumn.HeaderText, grid.ColumnHeadersDefaultCellStyle.Font,
                                    new Size(Math.Max(1, entryColumn.Width - 12), int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height <= grid.ColumnHeadersHeight - 8,
                                    "Quick entry-number heading clipped at the supported viewport.");
                                var editor = Control<Panel>(form, "accountingDataEditor");
                                var caption = Control<TextBox>(form, "quickJournalDescription");
                                editor.ScrollControlIntoView(caption);
                                Application.DoEvents();
                                var position = editor.PointToClient(caption.PointToScreen(Point.Empty));
                                Check(position.Y >= 0 && position.Y + caption.Height <= editor.ClientSize.Height && Frozen(form),
                                    "Frozen quick draft cannot scroll to the readable description.");
                                if (size.Width == 760)
                                { using (var bitmap = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size)); bitmap.Save(Path.Combine(evidence, mode + "-" + english + ".png"), ImageFormat.Png); } }
                            }
                            var first = form.RunQuickJournalWriteAsync(() => recovery.ExecuteAsync(CancellationToken.None), request => Task.FromResult(History())); Pump(first);
                            Check(first.Result == 0 && posts == 1 && reads == 1 && recovery.Pending != null &&
                                Frozen(form), "Lost quick reply cleared or unlocked draft.");
                            form.Dispose();
                        }
                        using (var form = Open(english))
                        {
                            var recovery = new JournalOperationRecovery(new JournalPendingStore(directory, context, "Manual"), () => true,
                                request => { ++posts; throw new InvalidOperationException("Accepted quick request must not post."); },
                                (request, token) => { ++reads; return Task.FromResult(new JournalSubmissionStatus(92, true)); });
                            Attach(form, recovery, pending);
                            Check(posts == 1 && reads == 1, "Reopen queried or posted quick request.");
                            var retry = form.RunQuickJournalWriteAsync(() => recovery.ExecuteAsync(CancellationToken.None), request => Task.FromResult(History())); Pump(retry);
                            Check(retry.Result == 92 && posts == 1 && reads == 2 && recovery.Pending == null &&
                                Control<TextBox>(form, "quickJournalDescription").Text.Length == 0 && Control<Panel>(form, "accountingDataEditor").Enabled,
                                "Quick reopen duplicated commit or failed accepted cleanup.");
                            recovery.Dispose();
                            var conflict = new JournalOperationRecovery(new JournalPendingStore(directory, context, "Manual"), () => true,
                                request => { throw new InvalidOperationException("Different accepted quick payload must not post."); },
                                (request, token) => Task.FromResult(new JournalSubmissionStatus(92, false)));
                            Attach(form, conflict, pending);
                            var mismatch = form.RunQuickJournalWriteAsync(() => conflict.ExecuteAsync(CancellationToken.None), request => Task.FromResult(History())); Pump(mismatch);
                            Check(mismatch.Result == 92 && Field<bool>(form, "quickAcceptedReview") &&
                                Control<TextBox>(form, "quickJournalDescription").Text == pending.Request.Description &&
                                Frozen(form), "Different acceptance cleared or falsely accepted quick edits.");
                            Invoke(form, "NewQuickDraft"); Check(!Field<bool>(form, "quickAcceptedReview") && Control<Panel>(form, "accountingDataEditor").Enabled,
                                "Explicit New did not release accepted review.");
                            conflict.Dispose();
                            int attempts = 0;
                            var rejected = new JournalOperationRecovery(new JournalPendingStore(directory, context, "Manual"), () => true,
                                request => { ++attempts; return attempts == 1 ?
                                    Task.FromException<int>(new JournalPostRejectedException(request.SourceType, request.SourceId, new InvalidOperationException("Owned rollback"))) :
                                    Task.FromResult(93); }, (request, token) => Task.FromResult(new JournalSubmissionStatus(0)));
                            Attach(form, rejected, pending);
                            var denied = form.RunQuickJournalWriteAsync(() => rejected.ExecuteAsync(CancellationToken.None), request => Task.FromResult(History())); Pump(denied);
                            Check(denied.Result == 0 && rejected.CanCorrectRejected && Control<Panel>(form, "accountingDataEditor").Enabled,
                                "Confirmed quick rejection failed to retain editable request.");
                            Invoke(form, "NewQuickDraft"); Check(rejected.Pending.Request.SourceId == pending.Request.SourceId && rejected.CanCorrectRejected,
                                "Rejected New discarded quick identity.");
                            var corrected = JournalPendingRequest.Restore(new JournalPostRequest(pending.Request.Date, "Manual", pending.Request.SourceId,
                                "بيان مُصحح 500 mg", pending.Request.Lines.Select(line => new JournalLineValues(line.AccountCode, line.Debit, line.Credit, "بيان مُصحح 500 mg", null)).ToArray()));
                            rejected.CorrectRejected(corrected); Invoke(form, "RestoreQuickPending"); Invoke(form, "ApplyQuickRecoveryLock");
                            var correction = form.RunQuickJournalWriteAsync(() => rejected.ExecuteAsync(CancellationToken.None), request => Task.FromResult(History())); Pump(correction);
                            Check(correction.Result == 93 && attempts == 2 && rejected.Pending == null, "Corrected quick retry did not resolve.");
                        }
                        ++cases;
                    }
                    finally
                    {
                        string path = Path.GetFullPath(directory);
                        if (!path.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase) ||
                            !Path.GetFileName(path).StartsWith("PharmacyQuickJournal-")) throw new InvalidOperationException("Cleanup escaped owned temp root.");
                        if (Directory.Exists(path)) Directory.Delete(path, true);
                    }
                }
                Console.WriteLine("PASS: " + cases + " quick recovery theme/direction cases: passive exact restore, frozen New/editing, lost response, close/reopen without duplicate, mismatch review, confirmed rejection/correction; four measured native frames and four accessible non-overlapping footer actions. Fake SQL collaborators; physical keys/DPI/SQL remain separate.");
                Console.WriteLine("Evidence: " + evidence);
            }
            finally { Properties.Setings.Default.ThemeMode = theme; }
        }
    }
}

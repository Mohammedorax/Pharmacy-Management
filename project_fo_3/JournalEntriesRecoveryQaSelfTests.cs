using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;
using PharmacyManagement.Application;
using PharmacyManagement.Domain;

namespace project_fo_3
{
    internal static class JournalEntriesRecoveryQaSelfTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        private static T Field<T>(JournalEntriesForm form, string name) => (T)typeof(JournalEntriesForm).GetField(name, Private).GetValue(form);
        private static void Pump(Task task)
        {
            var timer = Stopwatch.StartNew();
            while (!task.IsCompleted && timer.Elapsed < TimeSpan.FromSeconds(35)) { Application.DoEvents(); Thread.Sleep(5); }
            if (!task.IsCompleted) throw new TimeoutException("Owned SQL journal editor did not complete."); task.GetAwaiter().GetResult();
        }
        private static JournalEntriesForm Open(string kind)
        {
            var form = new JournalEntriesForm("QA journal editor", kind, false);
            try
            {
                // Load explicitly without a public desktop window; handles belong only to this fixture.
                form.ShowInTaskbar = false; form.StartPosition = FormStartPosition.Manual;
                form.Location = new System.Drawing.Point(-20000, -20000); form.CreateControl();
                var load = form.LoadInitialLookupsAsync(); Pump(load);
                Check(load.Result && Field<bool>(form, "journalOptionsReady"), "Actual SQL editor options not ready.");
                return form;
            }
            catch { form.Dispose(); throw; }
        }
        private static Task<int> Save(JournalEntriesForm form)
            => (Task<int>)typeof(JournalEntriesForm).GetMethod("SaveEntryAsync", Private).Invoke(form, new object[] { false });
        private static void Fill(JournalEntriesForm form, string debit, string credit)
        {
            var rows = Field<DataTable>(form, "lines"); rows.Rows.Clear();
            Field<AntdUI.DatePicker>(form, "entryDate").Value = DateTime.Today;
            Field<TextBox>(form, "headerDescription").Text = "قيد مُتوازن Cash 500 mg A&B";
            string currency = Field<AntdUI.Select>(form, "currency").Text;
            rows.Rows.Add(debit, debit, 1250.50m, 0m, 1250.50m, 0m, currency, 1m, "بدون", DBNull.Value, "", DateTime.Today.ToString("yyyy/MM/dd"), "مدين 500 mg", "حذف");
            rows.Rows.Add(credit, credit, 0m, 1250.50m, 0m, 1250.50m, currency, 1m, "بدون", DBNull.Value, "", DateTime.Today.ToString("yyyy/MM/dd"), "دائن A&B", "حذف");
        }
        private static JournalPostRequest ReadSaved(int id, string kind, string user, string debit, string credit, string expectedSystemCurrencyCode = null)
        {
            DateTime date; string source, description;
            using (var connection = Database.CreateConnection())
            {
                connection.Open();
                using (var command = new SqlCommand("SELECT EntryDate,SourceType,SourceId,Description,CreatedBy FROM dbo.JournalEntriesTb WHERE JournalEntryId=@Id", connection))
                {
                    command.Parameters.AddWithValue("@Id", id);
                    using (var reader = command.ExecuteReader())
                    {
                        Check(reader.Read() && reader.GetString(1) == kind && reader.GetString(4) == user, "Actual editor stored wrong type/actor.");
                        date = reader.GetDateTime(0); source = reader.GetString(2); description = reader.GetString(3);
                        Guid parsed; Check(Guid.TryParseExact(source, "N", out parsed) && parsed != Guid.Empty && date == DateTime.Today &&
                            description == "قيد مُتوازن Cash 500 mg A&B" && !reader.Read(), "Actual editor lost date/source/raw description.");
                    }
                }
                var values = new List<JournalLineValues>();
                using (var command = new SqlCommand("SELECT AccountCode,Debit,Credit,LineDescription,CostCenterId FROM dbo.JournalEntryLinesTb WHERE JournalEntryId=@Id ORDER BY JournalEntryLineId", connection))
                {
                    command.Parameters.AddWithValue("@Id", id);
                    using (var reader = command.ExecuteReader()) while (reader.Read()) values.Add(new JournalLineValues(reader.GetString(0), reader.GetDecimal(1), reader.GetDecimal(2), reader.GetString(3), reader.IsDBNull(4) ? (int?)null : reader.GetInt32(4)));
                }
                Check(values.Count == 2 && values[0].AccountCode == debit && values[0].Debit == 1250.50m && values[0].Credit == 0m &&
                    values[0].Description == "مدين 500 mg" && values[1].AccountCode == credit && values[1].Credit == 1250.50m && values[1].Debit == 0m &&
                    values[1].Description == "دائن A&B" && values.All(v => !v.CostCenterId.HasValue), "Actual editor stored different lines.");
                return new JournalPostRequest(date, kind, source, description, values, expectedSystemCurrencyCode);
            }
        }
        private static int CommitConcurrentOpening(string token, JournalPostSession writer, JournalPostRequest request)
        {
            string database = QaSalesInvoiceSelfTests.RequireOwnedTemporaryQa(token);
            using (var blocker = Database.CreateConnection())
            using (var observer = Database.CreateConnection())
            {
                blocker.Open(); observer.Open();
                Check(blocker.Database == database && observer.Database == database, "Concurrent fixture escaped owned SQL.");
                int blockerId;
                using (var command = new SqlCommand("SELECT @@SPID", blocker)) blockerId = Convert.ToInt32(command.ExecuteScalar());
                using (var transaction = blocker.BeginTransaction())
                {
                    using (var command = new SqlCommand("SELECT COUNT_BIG(*) FROM dbo.JournalEntriesTb WITH (TABLOCKX,HOLDLOCK)", blocker, transaction)) command.ExecuteScalar();
                    var first = Task.Run(() => writer.Service.Post(request, writer.Actor));
                    var second = Task.Run(() => writer.Service.Post(request, writer.Actor));
                    var timer = Stopwatch.StartNew(); bool observed = false;
                    while (timer.Elapsed < TimeSpan.FromSeconds(8))
                    {
                        using (var command = new SqlCommand("WITH Waiters AS (SELECT session_id,blocking_session_id FROM sys.dm_exec_requests WHERE database_id=DB_ID() AND blocking_session_id=@Blocker UNION ALL SELECT r.session_id,r.blocking_session_id FROM sys.dm_exec_requests r JOIN Waiters w ON r.blocking_session_id=w.session_id WHERE r.database_id=DB_ID()) SELECT COUNT(DISTINCT session_id) FROM Waiters OPTION(MAXRECURSION 16)", observer))
                        {
                            command.Parameters.AddWithValue("@Blocker", blockerId);
                            if (Convert.ToInt32(command.ExecuteScalar()) >= 2) { observed = true; break; }
                        }
                        Thread.Sleep(20);
                    }
                    using(var diagnosis=new SqlCommand("SELECT session_id,blocking_session_id,wait_type FROM sys.dm_exec_requests WHERE database_id=DB_ID() AND session_id<>@@SPID AND session_id<>@Blocker",observer))
                    {
                        diagnosis.Parameters.AddWithValue("@Blocker",blockerId);
                        using(var rows=diagnosis.ExecuteReader())while(rows.Read())Console.WriteLine("Owned concurrent SQL wait: session="+Convert.ToInt32(rows[0])+"; blocker="+Convert.ToInt32(rows[1])+"; wait="+Convert.ToString(rows[2])+"; ownedBlocker="+blockerId);
                    }
                    transaction.Rollback();
                    Check(Task.WaitAll(new Task[] { first, second }, TimeSpan.FromSeconds(20)), "Concurrent journal requests did not complete.");
                    Console.WriteLine("Owned concurrent results: observed="+observed+"; first="+first.Result.Id+"/"+first.Result.AlreadyPosted+"; second="+second.Result.Id+"/"+second.Result.AlreadyPosted);
                    Check(observed && first.Result.Id > 0 && first.Result.Id == second.Result.Id && first.Result.AlreadyPosted != second.Result.AlreadyPosted,
                        "Two observed simultaneous SQL writers did not converge on one new committed source.");
                    Console.WriteLine("PASS owned opening GUID: two actual SQL writer sessions observed in the wait chain rooted at the owned lock, then one new commit and one accepted-source result, same id. Two physical devices were not used.");
                    return first.Result.Id;
                }
            }
        }
        internal static void RunCrashChild(string backupPath, string token, string inputPath, string markerPath)
        {
            string database = QaSalesInvoiceSelfTests.RequireOwnedTemporaryQa(token);
            string artifactDirectory = Path.GetFullPath(Path.GetDirectoryName(backupPath));
            string expectedInput = Path.Combine(artifactDirectory, "journal-editor-crash-" + token + ".protected");
            string expectedMarker = Path.Combine(artifactDirectory, "journal-editor-crash-" + token + ".committed");
            Check(Path.GetFullPath(inputPath) == expectedInput && Path.GetFullPath(markerPath) == expectedMarker,
                "Owned crash input/marker escaped the C: QA artifact directory.");
            Check(!File.Exists(markerPath), "Owned crash marker already exists.");
            byte[] encrypted = File.ReadAllBytes(inputPath);
            Check(encrypted.Length > 0 && encrypted.Length <= 16384, "Owned crash input size is invalid.");
            byte[] clear = ProtectedData.Unprotect(encrypted, Encoding.UTF8.GetBytes(token), DataProtectionScope.CurrentUser);
            XElement payload;
            try { payload = XElement.Parse(Encoding.UTF8.GetString(clear)); }
            finally { Array.Clear(clear, 0, clear.Length); }
            Check(payload.Name == "CrashInput" && (string)payload.Attribute("token") == token &&
                DateTime.UtcNow - DateTime.Parse((string)payload.Attribute("utc"), null, System.Globalization.DateTimeStyles.RoundtripKind) < TimeSpan.FromMinutes(10),
                "Owned crash input identity or age is invalid.");
            string user = (string)payload.Element("User"), password = (string)payload.Element("Password");
            string employee = (string)payload.Element("Employee"), debit = (string)payload.Element("Debit");
            string credit = (string)payload.Element("Credit"), currency = (string)payload.Element("Currency");
            string source = (string)payload.Element("Source");
            Guid parsedSource;
            Check(!string.IsNullOrWhiteSpace(user) && !string.IsNullOrEmpty(password) && !string.IsNullOrWhiteSpace(employee) &&
                !string.IsNullOrWhiteSpace(currency) && Guid.TryParseExact(source, "N", out parsedSource) && parsedSource != Guid.Empty,
                "Owned crash request identity or actor is invalid.");
            using (var connection = Database.CreateConnection()) { connection.Open(); Check(connection.Database == database, "Crash child escaped the owned QA database."); QaTaxPolicySelfTests.VerifyBackup(connection, backupPath); }
            var result = ApplicationServices.Authentication.Authenticate(new AuthenticationRequest { UserName = user, Password = password });
            Check(result.Status == AuthenticationStatus.Success && result.EmployeeId == employee && result.Role == "Manager",
                "Owned crash child actor was not verified from SQL.");
            SessionContext.Start(result.UserName, result.Role, result.EmployeeId);
            try
            {
                var writer = ApplicationServices.CreateJournalPostSessionForEditor(() => true);
                var reader = ApplicationServices.CreateJournalSubmissionStatusSession();
                var native = writer.Service as IAsyncJournalPostService;
                Check(writer.IsCurrent && reader.IsCurrent && native != null, "Owned crash child writer/read sessions are not current or async.");
                var request = new JournalPostRequest(DateTime.Today, "OpeningEntry", source, "قيد مُتوازن Cash 500 mg A&B",
                    new[] { new JournalLineValues(debit, 1250.50m, 0m, "مدين 500 mg", null),
                        new JournalLineValues(credit, 0m, 1250.50m, "دائن A&B", null) }, currency);
                var pending = JournalPendingRequest.Restore(request);
                string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PharmacyManagement", "pending-journals");
                var store = new JournalPendingStore(directory, reader.ContextKey, "OpeningEntry");
                var recovery = new JournalOperationRecovery(store, () => writer.IsCurrent && reader.IsCurrent,
                    async (postRequest, cancellation) =>
                    {
                        var committed = await native.PostAsync(postRequest, writer.Actor, cancellation).ConfigureAwait(false);
                        Check(committed != null && committed.Id > 0, "Actual SQL post returned no committed journal id.");
                        new XElement("Committed", new XAttribute("token", token), new XAttribute("database", database),
                            new XAttribute("id", committed.Id), new XAttribute("source", source),
                            new XAttribute("utc", DateTime.UtcNow.ToString("O"))).Save(markerPath);
                        // Hold the child after SQL COMMIT but before JournalOperationRecovery can set AcceptedId or clear the pending store.
                        await Task.Delay(TimeSpan.FromMinutes(2), CancellationToken.None).ConfigureAwait(false);
                        return committed.Id;
                    },
                    (statusRequest, cancellation) => reader.Service.FindAsync(statusRequest, reader.Actor, cancellation));
                Check(recovery.Pending == null, "Owned crash child found a preexisting pending request.");
                recovery.Prepare(pending);
                recovery.ExecuteAsync(CancellationToken.None).GetAwaiter().GetResult();
                throw new InvalidOperationException("Crash child passed the post-COMMIT termination boundary without being killed.");
            }
            finally { SessionContext.Clear(); }
        }
        internal static int[] Verify(string backupPath, string token, string user, string password, string employee, string debit, string credit)
        {
            QaSalesInvoiceSelfTests.RequireOwnedTemporaryQa(token);
            var ids = new List<int>(); var documents = new List<XElement>();
            using (var form = Open("Manual"))
            {
                Check(Field<JournalOperationRecovery>(form, "journalRecovery").Pending == null, "Owned manual slot was unexpectedly occupied.");
                Fill(form, debit, credit);
                var recovery=Field<JournalOperationRecovery>(form,"journalRecovery");
                var rejectedPending=JournalPendingRequest.Create(DateTime.Today,"Manual",Field<TextBox>(form,"headerDescription").Text,
                    new[]{new JournalLineValues(debit,1250.50m,0,"مدين 500 mg",null),new JournalLineValues("QA-MISSING",0,1250.50m,"دائن A&B",null)},Field<string>(form,"journalSystemCurrencyCode"));
                using(var connection=Database.CreateConnection())
                {
                    connection.Open();
                    using(var missing=new SqlCommand("SELECT COUNT(*) FROM dbo.AccountsTb WHERE AccountCode=N'QA-MISSING'",connection))
                        Check(Convert.ToInt32(missing.ExecuteScalar())==0,"Owned missing account is unexpectedly present.");
                }
                recovery.Prepare(rejectedPending); bool rejected=false;
                try{var attempt=recovery.ExecuteAsync(CancellationToken.None);Pump(attempt);}
                catch(JournalPostRejectedException error){rejected=error.SourceId==rejectedPending.Request.SourceId&&error.InnerException is InvalidOperationException;}
                Check(rejected&&recovery.CanCorrectRejected&&recovery.Pending.HasSameContent(rejectedPending.Request),"Actual precommit rollback did not allow same-ID correction.");
                var rejectStatus=ApplicationServices.CreateJournalSubmissionStatusSession();
                Check(!rejectStatus.Service.FindAsync(new JournalSubmissionStatusRequest("Manual",rejectedPending.Request.SourceId,rejectedPending.Request),rejectStatus.Actor,CancellationToken.None).GetAwaiter().GetResult().IsPosted,
                    "Rejected transaction left a journal header.");
                typeof(JournalEntriesForm).GetMethod("ApplyJournalRecoveryLock",Private).Invoke(form,null);
                Check(Field<Panel>(form,"journalEditorViewport").Enabled,"Actual confirmed rejection did not unlock editor.");
                var save = Save(form); Pump(save); Check(save.Result > 0, "Actual corrected manual SaveEntryAsync failed.");
                var request = ReadSaved(save.Result, "Manual", user, debit, credit);
                Check(request.SourceId==rejectedPending.Request.SourceId,"Actual correction generated a replacement GUID.");
                Console.WriteLine("PASS owned confirmed journal rejection: missing second account rolled back inserted header/first line before commit; original encrypted GUID retained, actual SaveEntryAsync corrected from editor values and committed under the same GUID.");
                var statusSession=ApplicationServices.CreateJournalSubmissionStatusSession();
                var matched=statusSession.Service.FindAsync(new JournalSubmissionStatusRequest(request.SourceType,request.SourceId,request),statusSession.Actor,CancellationToken.None).GetAwaiter().GetResult();
                Check(matched.JournalId==save.Result&&matched.ContentMatches==true,"SQL status did not prove matching saved payload.");
                var changed=new JournalPostRequest(request.Date,request.SourceType,request.SourceId,"Changed pending description",request.Lines);
                var different=statusSession.Service.FindAsync(new JournalSubmissionStatusRequest(changed.SourceType,changed.SourceId,changed),statusSession.Actor,CancellationToken.None).GetAwaiter().GetResult();
                Check(different.JournalId==save.Result&&different.ContentMatches==false,"SQL status falsely accepted changed payload.");
                var postSession=ApplicationServices.CreateJournalPostSession();bool conflictDenied=false;
                try{postSession.Service.Post(changed,postSession.Actor);}catch(JournalAcceptedRequestConflictException conflict){conflictDenied=conflict.JournalId==save.Result&&conflict.SourceId==request.SourceId;}
                Check(conflictDenied,"Public SQL post falsely accepted different content for existing GUID.");
                bool ownerDenied=false;
                var repository=new PharmacyManagement.Infrastructure.SqlServer.SqlJournalPostRepository(Database.CreateConnection);
                try{repository.Post(request,new OperationActor {UserName=user+"-different",UserRole="Manager"});}
                catch(UnauthorizedAccessException){ownerDenied=true;}
                catch(JournalPostRejectedException error){throw new InvalidOperationException("Existing journal owner denial was incorrectly classified as a correctable rejected draft.",error);}
                Check(ownerDenied,"Existing journal was not denied to a different repository actor.");
                Console.WriteLine("PASS owned saved journal owner denial: direct repository preserves UnauthorizedAccessException, never a correctable precommit rejection; no extra journal or audit.");
                var exactRetry=postSession.Service.Post(request,postSession.Actor);
                Check(exactRetry.Id==save.Result&&exactRetry.AlreadyPosted,"Exact GUID retry did not retain old acceptance.");
                ReadSaved(save.Result,"Manual",user,debit,credit);
                Console.WriteLine("PASS owned manual GUID comparison: matching status proved content, changed header returned explicit mismatch, direct SQL service denied changed retry with original id, exact retry accepted; original saved data unchanged.");
                Check(Field<JournalOperationRecovery>(form, "journalRecovery").Pending == null && Field<DataTable>(form, "lines").Rows.Count == 0,
                    "Accepted manual editor did not clear draft/store.");
                ids.Add(save.Result); documents.Add(new XElement("Document", new XAttribute("kind", "Manual"), new XAttribute("id", save.Result), new XAttribute("source", request.SourceId), new XAttribute("user", user)));
            }
            string artifactDirectory = Path.GetDirectoryName(Path.GetFullPath(backupPath));
            string proof = Path.Combine(artifactDirectory, "journal-editor-restart-" + token + ".protected");
            string crashInput = Path.Combine(artifactDirectory, "journal-editor-crash-" + token + ".protected");
            string crashMarker = Path.Combine(artifactDirectory, "journal-editor-crash-" + token + ".committed");
            string crashStdout = crashInput + ".stdout.log", crashStderr = crashInput + ".stderr.log";
            try
            {
                string currency;
                using (var form = Open("OpeningEntry"))
                {
                    var original = Field<JournalOperationRecovery>(form, "journalRecovery");
                    Check(original.Pending == null, "Owned opening slot was unexpectedly occupied.");
                    currency = Field<string>(form, "journalSystemCurrencyCode");
                    Check(!string.IsNullOrWhiteSpace(currency), "Owned opening editor has no verified system currency.");
                    original.Dispose();
                }
                string sourceId = Guid.NewGuid().ToString("N");
                var inputPayload = new XElement("CrashInput", new XAttribute("token", token), new XAttribute("utc", DateTime.UtcNow.ToString("O")),
                    new XElement("User", user), new XElement("Password", password), new XElement("Employee", employee),
                    new XElement("Debit", debit), new XElement("Credit", credit), new XElement("Currency", currency), new XElement("Source", sourceId));
                byte[] inputClear = Encoding.UTF8.GetBytes(inputPayload.ToString(SaveOptions.DisableFormatting));
                try { File.WriteAllBytes(crashInput, ProtectedData.Protect(inputClear, Encoding.UTF8.GetBytes(token), DataProtectionScope.CurrentUser)); }
                finally { Array.Clear(inputClear, 0, inputClear.Length); }

                var executable = typeof(JournalEntriesForm).Assembly.Location;
                using (var child = Process.Start(new ProcessStartInfo(executable,
                    "--verify-qa-journal-editor-crash \"" + backupPath + "\" " + token + " \"" + crashInput + "\" \"" + crashMarker + "\" --confirm-owned-qa-journal-editor-crash")
                { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true }))
                {
                    var output = child.StandardOutput.ReadToEndAsync(); var errors = child.StandardError.ReadToEndAsync();
                    var wait = Stopwatch.StartNew();
                    while (!File.Exists(crashMarker) && !child.HasExited && wait.Elapsed < TimeSpan.FromSeconds(90)) Thread.Sleep(25);
                    Check(File.Exists(crashMarker) && !child.HasExited, "Crash child did not reach the verified post-COMMIT/pre-settlement barrier.");
                    var committed = XElement.Load(crashMarker);
                    int committedId = 0; DateTime committedUtc = DateTime.MinValue;
                    Check(committed.Name == "Committed" && (string)committed.Attribute("token") == token &&
                        (string)committed.Attribute("database") == QaSalesInvoiceSelfTests.RequireOwnedTemporaryQa(token) &&
                        (string)committed.Attribute("source") == sourceId && int.TryParse((string)committed.Attribute("id"), out committedId) && committedId > 0 &&
                        DateTime.TryParse((string)committed.Attribute("utc"), null, System.Globalization.DateTimeStyles.RoundtripKind, out committedUtc) &&
                        DateTime.UtcNow - committedUtc < TimeSpan.FromMinutes(2), "Crash marker does not prove this owned committed request.");
                    child.Kill();
                    Check(child.WaitForExit(15000) && child.HasExited, "Committed recovery child did not exit after forced termination.");
                    File.WriteAllText(crashStdout, output.GetAwaiter().GetResult(), Encoding.UTF8);
                    File.WriteAllText(crashStderr, errors.GetAwaiter().GetResult(), Encoding.UTF8);
                    Check(child.ExitCode != 0, "Crash child exited normally instead of being terminated at the committed barrier.");
                    Console.WriteLine("PASS owned process kill: actual application child terminated after SQL COMMIT and before recovery settlement; no graceful completion was observed.");

                    var statusSession = ApplicationServices.CreateJournalSubmissionStatusSession();
                    string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PharmacyManagement", "pending-journals");
                    using (var pendingStore = new JournalPendingStore(directory, statusSession.ContextKey, "OpeningEntry"))
                    {
                        var retained = pendingStore.Load();
                        var saved = ReadSaved(committedId, "OpeningEntry", user, debit, credit, currency);
                        Check(retained != null && retained.Request.SourceId == sourceId && retained.HasSameContent(saved),
                            "Forced process termination recovery mismatch: pending=" + (retained != null) +
                            ", pendingSource=" + (retained == null ? "<none>" : retained.Request.SourceId) +
                            ", expectedSource=" + sourceId + ", sameContent=" + (retained != null && retained.HasSameContent(saved)) +
                            ", pendingPathExists=" + File.Exists(Path.Combine(directory, statusSession.ContextKey + "-OpeningEntry.pending")) +
                            ", context=" + statusSession.ContextKey + ".");
                        ids.Add(committedId); documents.Add(new XElement("Document", new XAttribute("kind", "OpeningEntry"),
                            new XAttribute("id", committedId), new XAttribute("source", saved.SourceId), new XAttribute("user", user)));
                        var payload = new XElement("Restart", new XAttribute("token", token), new XAttribute("utc", DateTime.UtcNow.ToString("O")),
                            new XElement("User", user), new XElement("Password", password), new XElement("Employee", employee), new XElement("Id", committedId),
                            new XElement("Source", saved.SourceId), new XElement("Debit", debit), new XElement("Credit", credit));
                        byte[] clear = Encoding.UTF8.GetBytes(payload.ToString(SaveOptions.DisableFormatting));
                        try { File.WriteAllBytes(proof, ProtectedData.Protect(clear, Encoding.UTF8.GetBytes(token), DataProtectionScope.CurrentUser)); }
                        finally { Array.Clear(clear, 0, clear.Length); }
                    }
                }
                string stdout = proof + ".stdout.log", stderr = proof + ".stderr.log";
                using (var child = Process.Start(new ProcessStartInfo(executable, "--verify-qa-journal-editor-restart \"" + backupPath + "\" " + token + " \"" + proof + "\" --confirm-owned-qa-journal-editor-restart") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true }))
                {
                    var output = child.StandardOutput.ReadToEndAsync(); var errors = child.StandardError.ReadToEndAsync();
                    if (!child.WaitForExit(45000)) { child.Kill(); throw new TimeoutException("Owned restart child did not complete."); }
                    File.WriteAllText(stdout, output.GetAwaiter().GetResult(), Encoding.UTF8); File.WriteAllText(stderr, errors.GetAwaiter().GetResult(), Encoding.UTF8);
                    Check(child.ExitCode == 0 && File.ReadAllText(stdout).Contains("PASS owned restarted journal editor"), "Independent application restart did not recover known commit.");
                }
                using (var verify = Database.CreateConnection())
                {
                    verify.Open();
                    using (var command = new SqlCommand("SELECT COUNT_BIG(*) FROM dbo.JournalEntriesTb WHERE SourceType=N'OpeningEntry' AND SourceId=@Source", verify))
                    { command.Parameters.AddWithValue("@Source", sourceId); Check(Convert.ToInt64(command.ExecuteScalar()) == 1L, "Normal restart duplicated the committed source."); }
                }
                Console.WriteLine("PASS owned kill/restart recovery: normal independent app login, passive encrypted restore and actual SaveEntryAsync returned the original id, cleared the pending record, and left exactly one SQL journal.");
                var quickDocument=QuickJournalRecoveryQaSelfTests.Verify(backupPath,token,user,password,employee,debit,credit);
                ids.Add((int)quickDocument.Attribute("id"));documents.Add(quickDocument);
                var correctedQuickDocument=QuickJournalRecoveryQaSelfTests.VerifyCorrected(token,user,debit,credit);
                ids.Add((int)correctedQuickDocument.Attribute("id"));documents.Add(correctedQuickDocument);
                new XElement("OwnedEditorJournals", new XAttribute("token", token), documents).Save(Path.Combine(Path.GetDirectoryName(backupPath), "owned-editor-journals.xml"));
                Console.WriteLine("PASS owned actual journal editor SQL: Manual SaveEntryAsync corrected a confirmed rejected GUID and committed exact Arabic/local amounts; OpeningEntry real SQL commit with deliberately lost response retained the encrypted request; an independent app process authenticated, restored and called actual SaveEntryAsync without adding a journal. One restart Login audit is expected. No physical input or printing.");
                return ids.ToArray();
            }
            finally { if (File.Exists(proof)) File.Delete(proof); if (File.Exists(crashInput)) File.Delete(crashInput); }
        }
        internal static void VerifyRestartChild(string backupPath, string token, string proof)
        {
            QaSalesInvoiceSelfTests.RequireOwnedTemporaryQa(token);
            Check(Path.GetFullPath(proof) == Path.Combine(Path.GetDirectoryName(Path.GetFullPath(backupPath)), "journal-editor-restart-" + token + ".protected"), "Restart proof escaped owned artifact directory.");
            byte[] encrypted = File.ReadAllBytes(proof); Check(encrypted.Length <= 16384, "Restart proof is too large.");
            byte[] clear = ProtectedData.Unprotect(encrypted, Encoding.UTF8.GetBytes(token), DataProtectionScope.CurrentUser);
            XElement payload; try { payload = XElement.Parse(Encoding.UTF8.GetString(clear)); } finally { Array.Clear(clear, 0, clear.Length); }
            Check(payload.Name == "Restart" && (string)payload.Attribute("token") == token && DateTime.UtcNow - DateTime.Parse((string)payload.Attribute("utc"), null, System.Globalization.DateTimeStyles.RoundtripKind) < TimeSpan.FromMinutes(10), "Restart proof scope/age invalid.");
            using (var connection = Database.CreateConnection()) { connection.Open(); QaTaxPolicySelfTests.VerifyBackup(connection, backupPath); }
            var result = ApplicationServices.Authentication.Authenticate(new AuthenticationRequest { UserName = (string)payload.Element("User"), Password = (string)payload.Element("Password") });
            Check(result.Status == AuthenticationStatus.Success && result.EmployeeId == (string)payload.Element("Employee") && result.Role == "Manager", "Restart actor not verified from owned SQL.");
            SessionContext.Start(result.UserName, result.Role, result.EmployeeId);
            try
            {
                int expected = (int)payload.Element("Id");
                using (var form = Open("OpeningEntry"))
                {
                    var recovery = Field<JournalOperationRecovery>(form, "journalRecovery");
                    Check(recovery.Pending != null && recovery.Pending.Request.SourceId == (string)payload.Element("Source"), "Restart did not passively restore original encrypted identity.");
                    Check(!Field<Panel>(form, "journalEditorViewport").Enabled && Field<DataTable>(form, "lines").Rows.Count == 2, "Restored editor not frozen.");
                    var save = Save(form); Pump(save); Check(save.Result == expected && recovery.Pending == null, "Restart actual save did not resolve existing committed identity.");
                    ReadSaved(expected, "OpeningEntry", result.UserName, (string)payload.Element("Debit"), (string)payload.Element("Credit"));
                }
                Console.WriteLine("PASS owned restarted journal editor: actual SQL login, passive encrypted restoration, frozen editor, actual SaveEntryAsync returned original committed id and removed pending record.");
            }
            finally { SessionContext.Clear(); }
        }
    }
}

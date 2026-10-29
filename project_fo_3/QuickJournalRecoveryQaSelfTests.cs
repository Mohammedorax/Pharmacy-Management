using System;
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
using PharmacyManagement.Infrastructure.SqlServer;

namespace project_fo_3
{
    internal static class QuickJournalRecoveryQaSelfTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private const string Caption = "قيد سريع مُتوازن Cash 500 mg A&B";
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        private static T Field<T>(JournalEntriesQuickForm form, string name) => (T)typeof(JournalEntriesQuickForm).GetField(name, Private).GetValue(form);
        private static T Control<T>(Form form, string name) where T : System.Windows.Forms.Control => (T)form.Controls.Find(name, true).Single();
        private static void Pump(Task task)
        {
            var timer = Stopwatch.StartNew();
            while (!task.IsCompleted && timer.Elapsed < TimeSpan.FromSeconds(35)) { Application.DoEvents(); Thread.Sleep(5); }
            if (!task.IsCompleted) throw new TimeoutException("Owned quick SQL fixture timed out.");
            task.GetAwaiter().GetResult();
        }
        private static JournalEntriesQuickForm Open()
        {
            var form = new JournalEntriesQuickForm(false) { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual,
                Location = new System.Drawing.Point(-20000, -20000) };
            try { form.CreateControl(); var load = form.LoadQuickJournalAccountsAsync(); Pump(load); Check(load.Result, "Actual quick SQL accounts did not load."); return form; }
            catch { form.Dispose(); throw; }
        }
        private static Task<int> Save(JournalEntriesQuickForm form) =>
            (Task<int>)typeof(JournalEntriesQuickForm).GetMethod("SaveQuickEntryAsync", Private).Invoke(form, null);
        private static void Select(ComboBox selector, string code)
        {
            string item = selector.Items.Cast<object>().Select(Convert.ToString).Single(value => value.StartsWith(code + " - ", StringComparison.Ordinal));
            selector.SelectedIndex = selector.Items.IndexOf(item);
        }
        private static void ReadSaved(int id, JournalPostRequest expected, string user)
        {
            var session = ApplicationServices.CreateJournalSubmissionStatusSession();
            var result = session.Service.FindAsync(new JournalSubmissionStatusRequest("Manual", expected.SourceId, expected), session.Actor, CancellationToken.None).GetAwaiter().GetResult();
            Check(result.JournalId == id && result.ContentMatches == true, "Actual saved quick payload differs from frozen request.");
            using (var connection = Database.CreateConnection())
            {
                connection.Open();
                using (var command = new SqlCommand("SELECT CreatedBy FROM dbo.JournalEntriesTb WHERE JournalEntryId=@Id", connection))
                { command.Parameters.AddWithValue("@Id", id); Check(Convert.ToString(command.ExecuteScalar()) == user, "Quick saved actor changed."); }
            }
        }
        internal static XElement Verify(string backupPath, string token, string user, string password, string employee, string debit, string credit)
        {
            QaSalesInvoiceSelfTests.RequireOwnedTemporaryQa(token);
            int committed = 0; JournalPostRequest frozen = null;
            using (var form = Open())
            {
                var original = Field<JournalOperationRecovery>(form, "quickRecovery");
                Check(original != null && original.Pending == null, "Owned quick slot was unexpectedly occupied."); original.Dispose();
                Select(Control<ComboBox>(form, "quickJournalDebitAccount"), debit);
                Select(Control<ComboBox>(form, "quickJournalCreditAccount"), credit);
                Control<AntdUI.Input>(form, "quickJournalAmount").Text = 1250.50m.ToString(System.Globalization.CultureInfo.CurrentCulture);
                Control<TextBox>(form, "quickJournalDescription").Text = Caption;
                var writer = ApplicationServices.CreateJournalPostSession(); var reader = ApplicationServices.CreateJournalSubmissionStatusSession();
                string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PharmacyManagement", "pending-journals", "quick");
                var store = new JournalPendingStore(directory, reader.ContextKey, "Manual");
                var recovery = new JournalOperationRecovery(store, () => writer.IsCurrent && reader.IsCurrent,
                    request => Task.Run((Func<int>)(() =>
                    {
                        Check(store.Load().HasSameContent(request), "Actual quick save was sent before encrypted persistence.");
                        frozen = request; committed = writer.Service.Post(request, writer.Actor).Id;
                        // Deliberately change generation after the actual commit, then lose the response.
                        // This verifies stale-session handling and avoids a modal error dialog in owned automation.
                        SessionContext.Start(user, "Manager", employee);
                        throw new IOException("Owned quick lost response after commit and same-user relogin");
                    })), (request, cancellation) => reader.Service.FindAsync(request, reader.Actor, cancellation));
                typeof(JournalEntriesQuickForm).GetField("quickRecovery", Private).SetValue(form, recovery);
                var save = Save(form); Pump(save);
                Check(save.Result == 0 && committed > 0 && frozen != null && SqlJournalRequestComparison.IsManualIdentity(frozen) &&
                    frozen.Description == Caption && frozen.Lines[0].Debit == 1250.50m && frozen.Lines[1].Credit == 1250.50m &&
                    recovery.Pending != null && recovery.Pending.HasSameContent(frozen), "Actual quick lost reply did not preserve generated GUID/local payload.");
            }
            ReadSaved(committed, frozen, user);
            string proof = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(backupPath)), "quick-journal-restart-" + token + ".protected");
            try
            {
                var payload = new XElement("QuickRestart", new XAttribute("token", token), new XAttribute("utc", DateTime.UtcNow.ToString("O")),
                    new XElement("User", user), new XElement("Password", password), new XElement("Employee", employee),
                    new XElement("Id", committed), new XElement("Source", frozen.SourceId));
                byte[] clear = Encoding.UTF8.GetBytes(payload.ToString(SaveOptions.DisableFormatting));
                try { File.WriteAllBytes(proof, ProtectedData.Protect(clear, Encoding.UTF8.GetBytes(token), DataProtectionScope.CurrentUser)); }
                finally { Array.Clear(clear, 0, clear.Length); }
                using (var child = Process.Start(new ProcessStartInfo(typeof(JournalEntriesQuickForm).Assembly.Location,
                    "--verify-qa-quick-journal-editor-restart \"" + backupPath + "\" " + token + " \"" + proof + "\" --confirm-owned-qa-quick-journal-editor-restart")
                    { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true }))
                {
                    var output = child.StandardOutput.ReadToEndAsync(); var errors = child.StandardError.ReadToEndAsync();
                    if (!child.WaitForExit(45000)) { child.Kill(); throw new TimeoutException("Owned quick restart did not finish."); }
                    File.WriteAllText(proof + ".stdout.log", output.GetAwaiter().GetResult(), Encoding.UTF8);
                    File.WriteAllText(proof + ".stderr.log", errors.GetAwaiter().GetResult(), Encoding.UTF8);
                    Check(child.ExitCode == 0 && File.ReadAllText(proof + ".stdout.log").Contains("PASS owned restarted quick journal"),
                        "Independent quick app recovery failed.");
                }
                ReadSaved(committed, frozen, user);
                Console.WriteLine("PASS owned quick actual SaveQuickEntryAsync: encrypted GUID before SQL, exact local/mixed payload, real commit then deliberately lost response plus same-user session change; independent app authenticated/restored and actual SaveQuickEntryAsync returned original id without another journal. One extra restart Login audit expected; no physical keys/network interruption.");
                return new XElement("Document", new XAttribute("editor", "Quick"), new XAttribute("kind", "Manual"),
                    new XAttribute("id", committed), new XAttribute("source", frozen.SourceId), new XAttribute("user", user));
            }
            finally { if (File.Exists(proof)) File.Delete(proof); }
        }
        internal static XElement VerifyCorrected(string token, string user, string debit, string credit)
        {
            QaSalesInvoiceSelfTests.RequireOwnedTemporaryQa(token);
            using (var form = Open())
            {
                var recovery = Field<JournalOperationRecovery>(form, "quickRecovery");
                Check(recovery.Pending == null, "Owned correction slot not empty.");
                string oldCaption = "مسودة رفض مُؤكد 500 mg";
                var pending = JournalPendingRequest.Create(DateTime.Now, "Manual", oldCaption,
                    new[] { new JournalLineValues(debit, 1250.50m, 0, oldCaption, null), new JournalLineValues("QA-MISSING", 0, 1250.50m, oldCaption, null) }, Field<string>(form, "quickSystemCurrencyCode"));
                recovery.Prepare(pending); bool rejected = false;
                try { var attempt = recovery.ExecuteAsync(CancellationToken.None); Pump(attempt); }
                catch (JournalPostRejectedException error) { rejected = error.SourceId == pending.Request.SourceId && error.InnerException.Message.Contains("QA-MISSING"); }
                Check(rejected && recovery.CanCorrectRejected && recovery.Pending.HasSameContent(pending.Request), "Actual quick rollback did not retain correctable original.");
                Select(Control<ComboBox>(form, "quickJournalDebitAccount"), debit);
                Select(Control<ComboBox>(form, "quickJournalCreditAccount"), credit);
                Control<AntdUI.Input>(form, "quickJournalAmount").Text = 1250.50m.ToString(System.Globalization.CultureInfo.CurrentCulture);
                Control<TextBox>(form, "quickJournalDescription").Text = Caption;
                var expected = new JournalPostRequest(pending.Request.Date, "Manual", pending.Request.SourceId, Caption,
                    new[] { new JournalLineValues(debit, 1250.50m, 0, Caption, null), new JournalLineValues(credit, 0, 1250.50m, Caption, null) });
                var save = Save(form); Pump(save);
                Check(save.Result > 0 && recovery.Pending == null && !Control<TextBox>(form, "quickJournalDescription").ReadOnly,
                    "Actual corrected quick save did not resolve and unlock.");
                ReadSaved(save.Result, expected, user);
                Console.WriteLine("PASS owned corrected quick journal: actual missing-account rollback retained GUID/date, actual SaveQuickEntryAsync captured changed editor values and committed original identity/date, saved-content proof matched; one journal/two lines/one audit.");
                return new XElement("Document", new XAttribute("editor", "QuickCorrection"), new XAttribute("kind", "Manual"),
                    new XAttribute("id", save.Result), new XAttribute("source", pending.Request.SourceId), new XAttribute("user", user));
            }
        }
        internal static void VerifyRestartChild(string backupPath, string token, string proof)
        {
            QaSalesInvoiceSelfTests.RequireOwnedTemporaryQa(token);
            Check(Path.GetFullPath(proof) == Path.Combine(Path.GetDirectoryName(Path.GetFullPath(backupPath)), "quick-journal-restart-" + token + ".protected"),
                "Quick proof escaped owned directory.");
            byte[] encrypted = File.ReadAllBytes(proof); Check(encrypted.Length <= 16384, "Quick proof too large.");
            byte[] clear = ProtectedData.Unprotect(encrypted, Encoding.UTF8.GetBytes(token), DataProtectionScope.CurrentUser);
            XElement payload;
            try { payload = XElement.Parse(Encoding.UTF8.GetString(clear)); } finally { Array.Clear(clear, 0, clear.Length); }
            Check(payload.Name == "QuickRestart" && (string)payload.Attribute("token") == token &&
                DateTime.UtcNow - DateTime.Parse((string)payload.Attribute("utc"), null, System.Globalization.DateTimeStyles.RoundtripKind) < TimeSpan.FromMinutes(10),
                "Quick proof scope or age invalid.");
            using (var connection = Database.CreateConnection()) { connection.Open(); QaTaxPolicySelfTests.VerifyBackup(connection, backupPath); }
            var actor = ApplicationServices.Authentication.Authenticate(new AuthenticationRequest { UserName = (string)payload.Element("User"), Password = (string)payload.Element("Password") });
            Check(actor.Status == AuthenticationStatus.Success && actor.Role == "Manager" && actor.EmployeeId == (string)payload.Element("Employee"), "Quick restart actor not authenticated.");
            SessionContext.Start(actor.UserName, actor.Role, actor.EmployeeId);
            try
            {
                using (var form = Open())
                {
                    var recovery = Field<JournalOperationRecovery>(form, "quickRecovery"); var pending = recovery.Pending;
                    Check(pending != null && pending.Request.SourceId == (string)payload.Element("Source") && pending.Request.Description == Caption &&
                        Control<TextBox>(form, "quickJournalDescription").ReadOnly && Control<AntdUI.Input>(form, "quickJournalAmount").ReadOnly &&
                        !Control<AntdUI.Button>(form, "quickJournalNew").Enabled, "Quick restart did not passively restore frozen original.");
                    int expected = (int)payload.Element("Id"); var save = Save(form); Pump(save);
                    Check(save.Result == expected && recovery.Pending == null && Control<TextBox>(form, "quickJournalDescription").Text.Length == 0,
                        "Actual quick retry did not resolve known commit.");
                    ReadSaved(expected, pending.Request, actor.UserName);
                }
                Console.WriteLine("PASS owned restarted quick journal: real SQL login, original encrypted GUID and read-only payload restored, actual SaveQuickEntryAsync returned committed id and cleared recovery without another post.");
            }
            finally { SessionContext.Clear(); }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using PharmacyManagement.Application;

namespace project_fo_3
{
    internal static class WorkspaceWriteFollowUpSelfTests
    {
        private const string Technical = "QA_WORKSPACE_DETAIL_ONLY_IN_LOG";
        private static readonly Guid Id = new Guid("11111111-1111-1111-1111-111111111111");
        private static readonly Guid CreatedId = new Guid("22222222-2222-2222-2222-222222222222");
        private static readonly Guid EvidenceId = new Guid("33333333-3333-3333-3333-333333333333");
        private static int assertions, cases;
        internal static void Run()
        {
            string theme = Properties.Setings.Default.ThemeMode;
            var culture = System.Threading.Thread.CurrentThread.CurrentCulture;
            if (SessionContext.IsAuthenticated) throw new InvalidOperationException("Owned test needs an empty initial session.");
            assertions = cases = 0;
            SessionContext.Start("QA_WORKSPACE_UI", "Manager");
            try
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = new CultureInfo("ar-SA");
                foreach (string mode in new[] { "Light", "Dark" })
                foreach (bool english in new[] { false, true })
                foreach (string scenario in new[] { "success", "cancel", "unconfirmed", "factoryFailure", "hintFailure", "permissionLost", "preview", "reentry" })
                {
                    if (!SessionContext.IsAuthenticated) SessionContext.Start("QA_WORKSPACE_UI", "Manager");
                    Properties.Setings.Default.ThemeMode = mode;
                    VerifySettingsTaxResult(scenario, english); ++cases;
                }
                foreach (string mode in new[] { "Light", "Dark" })
                foreach (bool english in new[] { false, true })
                foreach (string scenario in new[] { "success", "cancelClose", "writeFailure", "recoveryFailure", "permissionLost", "permissionBeforeSave", "preview" })
                {
                    if (!SessionContext.IsAuthenticated) SessionContext.Start("QA_WORKSPACE_UI", "Manager");
                    Properties.Setings.Default.ThemeMode = mode;
                    VerifyTaxSettingsFollowUp(scenario, english); ++cases;
                }
                foreach (string mode in new[] { "Light", "Dark" })
                foreach (bool english in new[] { false, true })
                foreach (string phase in new[] { "read", "write" })
                foreach (string ending in new[] { "success", "failure", "session", "dispose" })
                {
                    if (!SessionContext.IsAuthenticated) SessionContext.Start("QA_WORKSPACE_UI", "Manager");
                    Properties.Setings.Default.ThemeMode = mode;
                    VerifyTaxSettingsLifetime(phase, ending, english); ++cases;
                }
                foreach (string mode in new[] { "Light", "Dark" })
                foreach (bool english in new[] { false, true })
                foreach (bool cancel in new[] { false, true })
                {
                    if (!SessionContext.IsAuthenticated) SessionContext.Start("QA_WORKSPACE_UI", "Manager");
                    Properties.Setings.Default.ThemeMode = mode;
                    VerifyTaxModalSave(english, cancel); ++cases;
                }
                foreach (string mode in new[] { "Light", "Dark" })
                foreach (bool english in new[] { false, true })
                foreach (string scenario in new[] { "readFailure", "bindFailure", "uiFailure", "success", "writeFailure", "noChange", "permissionLost", "preview" })
                {
                    if (!SessionContext.IsAuthenticated) SessionContext.Start("QA_WORKSPACE_UI", "Manager");
                    Properties.Setings.Default.ThemeMode = mode;
                    VerifySaudiSettingsFollowUp(scenario, english); ++cases;
                }
                foreach (string mode in new[] { "Light", "Dark" })
                foreach (bool english in new[] { false, true })
                {
                    Properties.Setings.Default.ThemeMode = mode;
                    VerifyCurrencyRefresh(english, false); ++cases;
                    VerifyCurrencyRefresh(english, true); ++cases;
                    VerifyPeriodRefresh(english, false); ++cases;
                    VerifyPeriodRefresh(english, true); ++cases;
                }
                foreach (string mode in new[] { "Light", "Dark" })
                foreach (bool english in new[] { false, true })
                foreach (string operation in new[] { "Save", "Delete", "System" })
                foreach (string scenario in new[] { "empty", "readFailure", "systemFailure", "bindFailure", "success", "writeFailure", "dismiss", "permissionLost" })
                {
                    if (scenario == "dismiss" && operation != "Delete") continue;
                    if (!SessionContext.IsAuthenticated) SessionContext.Start("QA_WORKSPACE_UI", "Manager");
                    Properties.Setings.Default.ThemeMode = mode;
                    VerifyCurrencyFollowUp(operation, scenario, english); ++cases;
                }
                ResponsiveLayoutSelfTests.VerifyAccountingPeriodsLayout();
                ResponsiveLayoutSelfTests.VerifyCurrencySettingsLayout();
                Console.WriteLine("PASS focused currency fields/footer/refresh layout checks; no SQL.");
                Console.WriteLine("PASS existing accounting period responsive fields/footer/preview checks with refresh.");
                foreach (string mode in new[] { "Light", "Dark" })
                foreach (bool english in new[] { false, true })
                foreach (string operation in new[] { "CreatePeriod", "CloseSelected", "AddIngredient", "AddEvidence", "DeactivateSelectedEvidence", "ToggleSelectedIngredient", "SaveMapping" })
                foreach (string scenario in new[] { "readFailure", "bindFailure", "success", "writeFailure", "dismiss", "invalidDate", "permissionLost" })
                {
                    if (scenario == "dismiss" && operation != "CloseSelected" && operation != "DeactivateSelectedEvidence") continue;
                    if (scenario == "invalidDate" && operation != "CreatePeriod" && operation != "AddIngredient" && operation != "AddEvidence") continue;
                    if (scenario == "permissionLost" && operation != "AddIngredient" && operation != "ToggleSelectedIngredient") continue;
                    if (!SessionContext.IsAuthenticated) SessionContext.Start("QA_WORKSPACE_UI", "Manager");
                    Properties.Setings.Default.ThemeMode = mode;
                    Verify(operation, scenario, english); ++cases;
                }
            }
            catch (Exception error) { Console.Error.WriteLine("Workspace write follow-up verification failed: " + error); throw; }
            finally
            {
                SessionContext.Clear(); Properties.Setings.Default.ThemeMode = theme;
                System.Threading.Thread.CurrentThread.CurrentCulture = culture;
            }
            Console.WriteLine("PASS workspace write follow-up: cases=" + cases + "; assertions=" + assertions + "; real form handlers and application fakes; ar-SA, both themes/languages; no SQL or desktop input.");
        }

        private static void VerifySettingsTaxResult(string scenario, bool english)
        {
            int opens = 0, reads = 0, writes = 0; bool hintFault = true; Exception childFailure = null;
            SettingsForm owner = null;
            Action open = () => owner.GetType().GetMethod("OpenTaxSettings", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(owner, new object[] { null, EventArgs.Empty });
            Func<bool, TaxSettingsForm> create = direction =>
            {
                ++opens; Assert(direction == english, "Tax dialog must inherit the settings presentation language.");
                if (scenario == "factoryFailure") throw new InvalidOperationException("QA secret tax factory");
                var child = new TaxSettingsForm(false, direction,
                    () => { ++reads; return System.Threading.Tasks.Task.FromResult(new TaxPolicy("SA", true, 15m)); },
                    (rate, enabled, country) =>
                    {
                        ++writes; Assert(rate == 15.25m && enabled && country == "OTHER", "Settings result changed raw saved tax request.");
                        if (scenario == "permissionLost") SessionContext.Clear();
                        if (scenario == "reentry") open();
                        return System.Threading.Tasks.Task.CompletedTask;
                    });
                child.ShowInTaskbar = false; child.Opacity = 0; child.MaximizeBox = false;
                child.Shown += (sender, args) => child.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        if (scenario == "cancel") { child.Close(); return; }
                        if (scenario == "unconfirmed") { child.DialogResult = DialogResult.OK; child.Close(); return; }
                        child.Controls.Find("cmbTaxCountry", true).OfType<ComboBox>().Single().SelectedIndex = 2;
                        child.Controls.Find("numTaxRate", true).OfType<NumericUpDown>().Single().Value = 15.25m;
                        child.Controls.Find("btnSaveTaxSettings", true).OfType<AntdUI.Button>().Single().PerformClick();
                    }
                    catch (Exception error) { childFailure = error; child.Close(); }
                }));
                return child;
            };
            using (owner = new SettingsForm(true, scenario == "preview", english, create))
            {
                owner.ShowInTaskbar = false; owner.Opacity = 0; owner.MaximizeBox = false;
                owner.StartPosition = FormStartPosition.Manual; owner.Location = new Point(-20000, -20000);
                owner.Show(); Application.DoEvents(); OwnedTestWindowViewport.Resize(owner, new Size(640, 500));
                var hint = (Label)Field(owner, "lblHint").GetValue(owner);
                string original = hint.Text; var bounds = owner.Bounds; var windowState = owner.WindowState;
                hint.TextChanged += (sender, args) =>
                {
                    if (scenario == "hintFailure" && writes > 0 && hintFault)
                    { hintFault = false; throw new InvalidOperationException("QA secret saved tax hint"); }
                };
                var notices = Capture(open, true, false);
                if (childFailure != null) throw new InvalidOperationException("Owned settings child failed.", childFailure);
                if (scenario == "preview") { Assert(opens == 0 && reads == 0 && writes == 0 && notices.Count == 0, "Settings preview opened a real tax workflow."); return; }
                Assert(opens == 1, "Settings tax navigation must not open duplicate dialogs.");
                if (scenario == "permissionLost") { Assert(owner.IsDisposed && notices.Count == 0 && reads == 1 && writes == 1, "Settings session loss after modal return must close without result updates."); return; }
                Assert(!owner.IsDisposed && owner.Enabled && owner.Bounds == bounds && owner.WindowState == windowState,
                    "Settings tax dialog changed owner geometry or did not restore it.");
                if (scenario == "cancel") Assert(writes == 0 && notices.Count == 0 && hint.Text == original, "Cancelled tax dialog changed parent hint.");
                else if (scenario == "factoryFailure" || scenario == "unconfirmed")
                    Assert(writes == 0 && notices.Count == 1 && hint.Text == original && !notices[0].Contains("QA secret"), "Unconfirmed tax dialog must not claim a saved parent result.");
                else
                {
                    string expected = english ? "Tax policy saved. Country: OTHER; applies to new invoices." : "تم حفظ سياسة الضريبة. البلد: OTHER؛ تطبق على الفواتير الجديدة.";
                    Assert(reads == 1 && writes == 1 && hint.Text == expected && hint.AccessibleName == expected,
                        "Settings tax result must use the confirmed country and presentation language, including accessibility.");
                    Assert(notices.Count == (scenario == "hintFailure" ? 1 : 0) && notices.All(n => !n.Contains("QA secret")), "Settings follow-up outcome must be singular and nontechnical.");
                    if (scenario == "hintFailure") Assert(notices[0].Contains(english ? "display" : "العرض") && notices[0].Contains("OTHER"), "Saved settings hint failure must preserve the completed outcome and raw reference.");
                    owner.ApplyPresentation(!english); owner.ApplyPresentation(english);
                    Assert(hint.Text == expected && hint.AccessibleName == expected && writes == 1 && reads == 1,
                        "Settings presentation refresh lost the confirmed tax result or repeated a service call.");
                    int required = TextRenderer.MeasureText(hint.Text, hint.Font,
                        new Size(Math.Max(1, hint.Width - hint.Padding.Horizontal), int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height;
                    Assert(hint.Height - hint.Padding.Vertical >= required, "Confirmed settings result clipped at compact width.");
                }
                owner.Close();
            }
        }

        private static void VerifyTaxSettingsFollowUp(string scenario, bool english)
        {
            int reads = 0, writes = 0; bool completed = false, fault = true;
            Func<System.Threading.Tasks.Task<TaxPolicy>> read = () =>
            {
                ++reads;
                if (reads > 1 && scenario == "recoveryFailure")
                    throw new InvalidOperationException("QA secret tax recovery");
                return System.Threading.Tasks.Task.FromResult(new TaxPolicy("SA", true, completed ? 15.25m : 15m));
            };
            Func<decimal, bool, string, System.Threading.Tasks.Task> write = (rate, enabled, country) =>
            {
                ++writes;
                Assert(rate == 15.25m && enabled && country == "SA", "Tax save changed submitted rate, enablement or raw country.");
                if (scenario == "writeFailure" || scenario == "recoveryFailure")
                    throw new InvalidOperationException("QA secret tax save");
                completed = true;
                if (scenario == "permissionLost") SessionContext.Clear();
                return System.Threading.Tasks.Task.CompletedTask;
            };
            using (var form = new TaxSettingsForm(scenario == "preview", english, read, write))
            {
                form.ShowInTaskbar = false; form.Opacity = 0; form.MaximizeBox = false;
                form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-20000, -20000);
                form.Show(); Application.DoEvents();
                var rate = form.Controls.Find("numTaxRate", true).OfType<NumericUpDown>().Single();
                rate.Value = 15.25m;
                form.FormClosing += (sender, args) =>
                {
                    if (!completed || !fault) return;
                    if (scenario == "cancelClose") args.Cancel = true;
                };
                if (scenario == "permissionBeforeSave") SessionContext.Clear();
                Action click = () => form.GetType().GetMethod("SaveSettings", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(form, new object[] { null, EventArgs.Empty });
                var notices = Capture(click, true, false);
                if (scenario == "preview") { Assert(reads == 0 && writes == 0 && notices.Count == 0, "Tax preview touched persistence."); return; }
                if (scenario == "permissionBeforeSave")
                {
                    Assert(reads == 1 && writes == 0 && notices.Count == 0 && form.IsDisposed, "Expired tax session must close before submitting."); return;
                }
                Assert(writes == 1, "Tax save must submit once.");
                if (scenario == "cancelClose")
                {
                    Console.WriteLine("Tax follow-up: " + scenario + "/" + english + "; reads=" + reads + "; notices=" + notices.Count + "; result=" + form.DialogResult + "; disposed=" + form.IsDisposed + "; text=" + string.Join(" | ", notices));
                    Assert(reads == 1 && notices.Count == 1 && notices[0].Contains(english ? "display" : "العرض") &&
                        notices[0].Contains("SA") && !notices[0].Contains("QA secret"),
                        "Returned tax save must distinguish close failure and avoid reload/repeated submission.");
                    Assert(!form.Controls.Find("btnSaveTaxSettings", true).Single().Enabled &&
                        form.DialogResult != DialogResult.OK, "Failed tax close must not expose save or a completed dialog result while still open.");
                    var repeat = Capture(click, true, false);
                    Assert(writes == 1 && repeat.Count == 0, "Tax close failure repeated a returned save.");
                    fault = false; form.Close();
                }
                else if (scenario == "permissionLost")
                    Assert(reads == 1 && notices.Count == 0 && form.IsDisposed, "Late tax permission loss must close without rereading or success.");
                else if (scenario == "success") Assert(form.IsDisposed && reads == 1 && notices.Count == 0, "Successful tax save must close once without reload.");
                else
                {
                    Assert(!completed && reads == 2 && notices.Count == (scenario == "recoveryFailure" ? 2 : 1) &&
                        notices.All(n => !n.Contains("QA secret")), "Tax write exception must retain existing receipt recovery and safe notices.");
                    Assert(form.Controls.Find("btnSaveTaxSettings", true).Single().Enabled == (scenario != "recoveryFailure"),
                        "Tax save availability must follow recovery load outcome.");
                    form.Close();
                }
            }
        }

        private static void VerifyTaxSettingsLifetime(string phase, string ending, bool english)
        {
            int reads = 0, writes = 0;
            var readGate = new System.Threading.Tasks.TaskCompletionSource<TaxPolicy>();
            var writeGate = new System.Threading.Tasks.TaskCompletionSource<bool>();
            Func<System.Threading.Tasks.Task<TaxPolicy>> read = () =>
            {
                ++reads;
                return phase == "read" ? readGate.Task : System.Threading.Tasks.Task.FromResult(new TaxPolicy("SA", true, 15m));
            };
            using (var form = new TaxSettingsForm(false, english, read, (rate, enabled, country) =>
            {
                ++writes;
                Assert(rate == 15.25m && enabled && country == "SA", "Pending tax save changed captured request.");
                return writeGate.Task;
            }))
            {
                form.ShowInTaskbar = false; form.Opacity = 0; form.MaximizeBox = false;
                form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-20000, -20000);
                form.Show(); Application.DoEvents();
                Action click = () => form.GetType().GetMethod("SaveSettings", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(form, new object[] { null, EventArgs.Empty });
                if (phase == "write")
                {
                    form.Controls.Find("numTaxRate", true).OfType<NumericUpDown>().Single().Value = 15.25m;
                    click();
                    form.Close();
                    var status = form.Controls.Find("taxSettingsStatus", true).OfType<Label>().Single();
                    Assert(!form.IsDisposed && status.Text.Contains(english ? "wait" : "انتظر") && status.AccessibleName == status.Text,
                        "Pending tax save must block close with a full accessible wait message.");
                }
                Assert((bool)Field(form, "busy").GetValue(form) && !form.Controls.Find("btnSaveTaxSettings", true).Single().Enabled,
                    "Pending tax operation must keep Save disabled.");
                click();
                Assert(writes == (phase == "write" ? 1 : 0), "Pending tax callback submitted again.");
                if (ending == "session") SessionContext.Clear();
                if (ending == "dispose") form.Dispose();
                var notices = Capture(() =>
                {
                    if (phase == "read")
                    {
                        if (ending == "failure") readGate.SetException(new InvalidOperationException("QA secret delayed tax read"));
                        else readGate.SetResult(new TaxPolicy("SA", true, 15m));
                    }
                    else
                    {
                        if (ending == "failure") writeGate.SetException(new InvalidOperationException("QA secret delayed tax save"));
                        else writeGate.SetResult(true);
                    }
                    var watch = System.Diagnostics.Stopwatch.StartNew();
                    while (!form.IsDisposed && (bool)Field(form, "busy").GetValue(form))
                    {
                        Application.DoEvents();
                        if (watch.ElapsedMilliseconds > 5000) throw new TimeoutException("Owned pending tax operation did not settle.");
                    }
                }, true, false);
                if (ending == "session" || ending == "dispose")
                    Assert(form.IsDisposed && reads == 1 && notices.Count == 0,
                        "Late tax result must not populate a disposed or unauthorized window or trigger another read/notice.");
                else if (ending == "failure")
                {
                    Assert(notices.Count == 1 && notices.All(n => !n.Contains("QA secret")) && reads == (phase == "write" ? 2 : 1),
                        "Delayed tax failure must show one safe notice and preserve receipt recovery.");
                    Assert(form.Controls.Find("btnSaveTaxSettings", true).Single().Enabled == (phase == "write"),
                        "Delayed tax failure must follow recovered load state.");
                    form.Close();
                }
                else if (phase == "write") Assert(form.IsDisposed && reads == 1 && notices.Count == 0, "Delayed returned tax save must close without rereading.");
                else
                {
                    Assert(!form.IsDisposed && (bool)Field(form, "loaded").GetValue(form) && notices.Count == 0,
                        "Current delayed tax read must populate and enable the window.");
                    form.Close();
                }
            }
        }

        private static void VerifyTaxModalSave(bool english, bool cancel)
        {
            int reads = 0, writes = 0; bool fault = cancel; Exception callbackFailure = null; DialogResult result = DialogResult.None;
            using (var owner = new Form { ShowInTaskbar = false, Opacity = 0, StartPosition = FormStartPosition.Manual,
                Location = new Point(-20000, -20000) })
            using (var form = new TaxSettingsForm(false, english,
                () => { ++reads; return System.Threading.Tasks.Task.FromResult(new TaxPolicy("SA", true, 15m)); },
                (rate, enabled, country) => { ++writes; return System.Threading.Tasks.Task.CompletedTask; }))
            {
                owner.Show(); form.ShowInTaskbar = false; form.Opacity = 0; form.MaximizeBox = false;
                form.FormClosing += (sender, args) => { if (writes > 0 && fault) args.Cancel = true; };
                form.Shown += (sender, args) => form.BeginInvoke(new Action(() =>
                {
                    try
                    {
                    form.Controls.Find("numTaxRate", true).OfType<NumericUpDown>().Single().Value = 15.25m;
                    form.Controls.Find("btnSaveTaxSettings", true).OfType<AntdUI.Button>().Single().PerformClick();
                    if (!cancel && form.DialogResult != DialogResult.OK)
                        throw new InvalidOperationException("Successful modal tax save did not preserve OK for the modal loop.");
                    if (cancel)
                    {
                        Assert(form.Visible && form.DialogResult == DialogResult.None &&
                            !form.Controls.Find("btnSaveTaxSettings", true).Single().Enabled,
                            "Cancelled modal tax close must remain open without repeat-save or success result.");
                        fault = false; form.Close();
                    }
                    }
                    catch (Exception error) { callbackFailure = error; fault = false; form.Close(); }
                }));
                var notices = Capture(() => result = form.ShowDialog(owner), true, false);
                if (callbackFailure != null) throw new InvalidOperationException("Owned modal tax callback failed.", callbackFailure);
                Assert(reads == 1 && writes == 1 && notices.Count == (cancel ? 1 : 0) &&
                    result == (cancel ? DialogResult.Cancel : DialogResult.OK),
                    "Tax modal save result, notice count or read/write count changed.");
                Assert(owner.Enabled && !owner.IsDisposed, "Tax modal return did not restore its owner.");
                owner.Close();
            }
        }

        private static void VerifySaudiSettingsFollowUp(string scenario, bool english)
        {
            bool completed = false, fault = true; int reads = 0, writes = 0;
            Func<IReadOnlyList<SaudiCommercialFeature>> read = () =>
            {
                ++reads;
                if (completed && fault && scenario == "readFailure") throw new InvalidOperationException("QA secret feature read");
                return new[] { new SaudiCommercialFeature("QA-{0}", "QA feature", "QA description")
                    { Mode = completed ? SaudiFeatureMode.Simulator : SaudiFeatureMode.Disabled } };
            };
            Action<IReadOnlyDictionary<string, SaudiFeatureMode>, string> write = (changes, reason) =>
            {
                ++writes;
                Assert(changes.Count == 1 && changes["QA-{0}"] == SaudiFeatureMode.Simulator && reason == "QA {0} سبب",
                    "Feature save changed raw key, mode or audit reason.");
                if (scenario == "writeFailure") throw new InvalidOperationException("QA secret feature write");
                completed = true;
                if (scenario == "permissionLost") SessionContext.Clear();
            };
            using (var form = new SaudiCommercialSettingsForm(scenario == "preview", english, read, write))
            {
                form.ShowInTaskbar = false; form.Opacity = 0; form.StartPosition = FormStartPosition.Manual;
                form.Location = new Point(-20000, -20000); form.MaximizeBox = false;
                form.Show(); Application.DoEvents();
                var grid = (DataGridView)Field(form, "grid").GetValue(form);
                foreach (string name in new[] { "saudiCommercialHeader", "saudiCommercialBody", "saudiCommercialCard", "saudiCommercialReasonPanel", "saudiCommercialReasonFrame", "saudiCommercialActions" })
                {
                    var control = form.Controls.Find(name, true).Single();
                    Assert(!string.IsNullOrWhiteSpace(control.AccessibleName) &&
                        (!english || !control.AccessibleName.Any(c => c >= '\u0600' && c <= '\u06ff')),
                        "Feature container needs an explicit accessible name in the presentation language: " + name);
                }
                Assert(grid.Columns["Description"].DefaultCellStyle.WrapMode == DataGridViewTriState.True &&
                    grid.AutoSizeRowsMode == DataGridViewAutoSizeRowsMode.AllCells,
                    "Feature descriptions must wrap and grow rows rather than truncate.");
                var reason = (TextBox)Field(form, "reasonText").GetValue(form);
                reason.Text = "QA {0} سبب";
                if (scenario != "noChange") grid.Rows[0].Cells["RequestedMode"].Value = english ? "Simulator" : "محاكاة";
                grid.RowsAdded += (sender, args) => { if (completed && fault && scenario == "bindFailure") throw new InvalidOperationException("QA secret feature binding"); };
                reason.TextChanged += (sender, args) => { if (completed && fault && scenario == "uiFailure" && reason.Text.Length == 0) throw new InvalidOperationException("QA secret reason presentation"); };
                Action click = () => form.GetType().GetMethod("SaveChanges", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(form, new object[] { null, EventArgs.Empty });
                var messages = Capture(click, true, false);
                if (scenario == "preview") { Assert(reads == 0 && writes == 0 && messages.Count == 0, "Feature preview must not read or save."); return; }
                if (scenario == "noChange") { Assert(writes == 0 && messages.Count == 0, "Unchanged feature settings must not save."); return; }
                Assert(writes == 1, "Feature action must submit once.");
                if (scenario == "permissionLost")
                {
                    Assert(reads == 1 && messages.Count == 0 && form.IsDisposed, "Feature session loss after write must close without rereading or success."); return;
                }
                Assert(messages.Count == 1 && !messages[0].Contains("QA secret"), "Feature outcome must be singular and avoid technical details.");
                if (scenario == "readFailure" || scenario == "bindFailure" || scenario == "uiFailure")
                {
                    Assert(completed && messages[0].Contains(english ? "display" : "العرض") && messages[0].Contains("QA-{0}"),
                        "Returned feature save must distinguish follow-up failure and retain raw key reference.");
                    Assert(!((Control)Field(form, "saveButton").GetValue(form)).Enabled && !grid.Enabled, "Stale feature controls must prevent edits and resubmission.");
                    var repeat = Capture(click, true, false);
                    Assert(writes == 1 && repeat.Count == 0, "Stale feature settings handler repeated a returned save.");
                    form.Close(); fault = false;
                    using (var reopened = new SaudiCommercialSettingsForm(false, english, read, write))
                    {
                        reopened.ShowInTaskbar = false; reopened.Opacity = 0; reopened.MaximizeBox = false;
                        reopened.Show(); Application.DoEvents();
                        Assert(((Control)Field(reopened, "saveButton").GetValue(reopened)).Enabled,
                            "Feature read recovery must restore actions after reopen.");
                        var unchanged = Capture(() => reopened.GetType().GetMethod("SaveChanges", BindingFlags.Instance | BindingFlags.NonPublic)
                            .Invoke(reopened, new object[] { null, EventArgs.Empty }), true, false);
                        Assert(writes == 1 && unchanged.Count == 0, "Reopened current feature modes must not repeat the save.");
                        reopened.Close();
                    }
                }
                else if (scenario == "writeFailure") Assert(!completed && reads == 1 && reason.Text == "QA {0} سبب", "Rejected feature write must retain reason and avoid success/reload.");
                else Assert(completed && reads == 2 && reason.Text.Length == 0, "Successful feature save must reload once and clear its submitted reason.");
                form.Close();
            }
        }

        private static void VerifyCurrencyRefresh(bool english, bool preview)
        {
            int reads = 0, systemReads = 0, writes = 0, shape = 0;
            bool readFault = false, serverSystem = false;
            Func<DataTable> read = () =>
            {
                ++reads; if (readFault) throw new InvalidOperationException("QA refresh secret");
                var table = new DataTable(); table.Columns.Add("الرقم", typeof(int)); table.Columns.Add("الدولة");
                table.Columns.Add("العملة"); table.Columns.Add("الرمز"); table.Columns.Add("الاختصار"); table.Columns.Add("عملة النظام", typeof(bool));
                if (shape != 2 && reads > 1) table.Rows.Add(8, "Other", "Other", "OTH", "O", false);
                if (shape == 0) table.Rows.Add(7, "QA", "Saved", "SAV", "S", serverSystem);
                if (shape != 2 && reads == 1) table.Rows.Add(8, "Other", "Other", "OTH", "O", false);
                return table;
            };
            using (var form = new CurrencySettingsForm(preview, english, read,
                () => { ++systemReads; return new CurrencyInfo { CurrencyCode = "SYS", CurrencyNameAr = "QA" }; },
                row => { ++writes; throw new InvalidOperationException("Refresh must not save."); },
                id => { ++writes; throw new InvalidOperationException("Refresh must not delete."); },
                id => { ++writes; throw new InvalidOperationException("Refresh must not set currency."); }))
            {
                form.ShowInTaskbar = false; form.Opacity = 0; form.StartPosition = FormStartPosition.Manual;
                form.Location = new Point(-20000, -20000); form.MaximizeBox = false;
                form.Show(); Application.DoEvents();
                var refresh = form.Controls.Find("refreshCurrencies", true).OfType<AntdUI.Button>().SingleOrDefault();
                Assert(refresh != null, "Currency window needs a direct Refresh action.");
                string[] fields = { "txtCountry", "txtCurrencyName", "txtCurrencyCode", "txtCurrencySymbol" };
                string[] draft = { "Draft {0}", "مسودة", "QAX", "ر" };
                Action fill = () => { for (int i = 0; i < fields.Length; ++i) SetText(form, fields[i], draft[i]); ((CheckBox)Field(form, "chkSystemCurrency").GetValue(form)).Checked = false; };
                Action retained = () => { for (int i = 0; i < fields.Length; ++i) Assert(((Control)Field(form, fields[i]).GetValue(form)).Text == draft[i], "Currency refresh changed raw draft fields."); };
                fill(); int initialId = (int)Field(form, "selectedCurrencyId").GetValue(form);
                refresh.PerformClick(); retained();
                Assert(writes == 0 && reads == (preview ? 0 : 2) && systemReads == (preview ? 0 : 2), "Refresh must only read once; preview must read nothing.");
                Assert((int)Field(form, "selectedCurrencyId").GetValue(form) == initialId, "Refresh must preserve selected identity despite row reorder.");
                Action f5 = () => typeof(Control).GetMethod("OnKeyDown", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(form, new object[] { new KeyEventArgs(Keys.F5) });
                if (preview) { f5(); retained(); Assert(reads == 0 && writes == 0, "Preview F5 touched persistence."); return; }
                serverSystem = true; refresh.PerformClick(); retained();
                Assert(!((Control)Field(form, "btnDelete").GetValue(form)).Enabled, "Fresh server system-currency state must prevent deletion despite unchecked draft.");
                var systemBlocked = Capture(() => form.GetType().GetMethod("btnDelete_Click", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(form, new object[] { null, EventArgs.Empty }), true, false);
                Assert(writes == 0 && systemBlocked.Count == 1, "Direct deletion must respect fresh server system-currency state.");
                serverSystem = false;
                shape = 1;
                var missing = Capture(() => refresh.PerformClick(), true, false); retained();
                Assert(missing.Count == 1 && (int)Field(form, "selectedCurrencyId").GetValue(form) == 0 && !((Control)Field(form, "btnSave").GetValue(form)).Enabled,
                    "Missing record must preserve draft but block implicit create or stale update.");
                form.GetType().GetMethod("btnSave_Click", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, new object[] { null, EventArgs.Empty });
                Assert(writes == 0, "Missing-record draft was silently resubmitted.");
                Invoke(form, "ClearEditor"); fill();
                refresh.PerformClick(); retained();
                Assert((int)Field(form, "selectedCurrencyId").GetValue(form) == 0 && ((Control)Field(form, "btnSave").GetValue(form)).Enabled,
                    "Explicit new draft must stay new across refresh.");
                shape = 0; refresh.PerformClick();
                var grid = (DataGridView)Field(form, "gridCurrencies").GetValue(form);
                grid.ClearSelection(); var selected = grid.Rows.Cast<DataGridViewRow>().Single(row => Convert.ToInt32(row.Cells["الرقم"].Value) == 7);
                selected.Selected = true;
                form.GetType().GetMethod("gridCurrencies_SelectionChanged", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, new object[] { grid, EventArgs.Empty });
                fill(); readFault = true;
                var failed = Capture(() => refresh.PerformClick(), true, false); retained();
                Assert(failed.Count == 1 && refresh.Enabled && !((Control)Field(form, "btnSave").GetValue(form)).Enabled, "Failed refresh must remain retryable with stale writes blocked.");
                readFault = false; f5(); retained();
                Assert(((Control)Field(form, "btnSave").GetValue(form)).Enabled && (int)Field(form, "selectedCurrencyId").GetValue(form) == 7,
                    "F5 must recover through the same draft-preserving path.");
                shape = 2;
                Capture(() => refresh.PerformClick(), true, false); retained();
                Assert(grid.Rows.Count == 0 && !((Control)Field(form, "btnSave").GetValue(form)).Enabled, "Empty refresh of existing record must keep draft without implicit creation.");
                int before = reads; SessionContext.Clear(); f5();
                Assert(form.IsDisposed && reads == before && writes == 0, "Expired session refresh must close without reads or writes.");
                SessionContext.Start("QA_WORKSPACE_UI", "Manager");
            }
        }

        private static void VerifyCurrencyFollowUp(string operation, string scenario, bool english)
        {
            bool completed = false, faultEnabled = true; int reads = 0, systemReads = 0, writes = 0;
            Func<DataTable> read = () =>
            {
                ++reads;
                if (completed && faultEnabled && scenario == "readFailure") throw new InvalidOperationException("QA secret connection failure");
                var table = new DataTable();
                table.Columns.Add("الرقم", typeof(int)); table.Columns.Add("الدولة"); table.Columns.Add("العملة");
                table.Columns.Add("الرمز"); table.Columns.Add("الاختصار"); table.Columns.Add("عملة النظام", typeof(bool));
                if (!(completed && scenario == "empty")) table.Rows.Add(7, "QA {0}", "Save", "QAX", "ر", false);
                return table;
            };
            Func<CurrencyInfo> system = () =>
            {
                ++systemReads;
                if (completed && faultEnabled && scenario == "systemFailure") throw new InvalidOperationException("QA secret system read failure");
                if (completed && scenario == "empty") return new CurrencyInfo();
                return new CurrencyInfo { CurrencyId = 7, CurrencyCode = "QAX", CurrencyNameAr = "Save", CurrencySymbol = "ر" };
            };
            Action submit = () =>
            {
                ++writes;
                if (scenario == "writeFailure") throw new InvalidOperationException("QA secret write failure");
                completed = true;
                if (scenario == "permissionLost") SessionContext.Clear();
            };
            using (var form = new CurrencySettingsForm(false, english, read, system,
                row => { Assert(row.CurrencyId == 7 && row.CountryNameAr == "QA {0}" && row.CurrencyCode == "QAX", "Currency save changed raw identity or input."); submit(); },
                id => { Assert(id == 7, "Currency delete changed identity."); submit(); },
                id => { Assert(id == 7, "Currency system selection changed identity."); submit(); }))
            {
                form.ShowInTaskbar = false; form.Opacity = 0; form.StartPosition = FormStartPosition.Manual;
                form.Location = new Point(-20000, -20000); form.MaximizeBox = false;
                form.Show(); Application.DoEvents();
                var grid = (DataGridView)Field(form, "gridCurrencies").GetValue(form);
                grid.DataBindingComplete += (sender, args) =>
                {
                    if (completed && faultEnabled && scenario == "bindFailure") throw new InvalidOperationException("QA secret binding failure");
                };
                grid.Rows[0].Selected = true;
                form.GetType().GetMethod("gridCurrencies_SelectionChanged", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(form, new object[] { grid, EventArgs.Empty });
                string handler = operation == "Save" ? "btnSave_Click" : operation == "Delete" ? "btnDelete_Click" : "btnSetSystem_Click";
                Action click = () => form.GetType().GetMethod(handler, BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(form, new object[] { null, EventArgs.Empty });
                var messages = Capture(click, scenario != "dismiss", false);
                if (scenario == "dismiss") { Assert(writes == 0 && messages.Count == 0, "Dismissing currency deletion must not write or report completion."); return; }
                Assert(writes == 1, "Currency action must write once.");
                if (scenario == "permissionLost")
                {
                    Assert(reads == 1 && systemReads == 1 && messages.Count == 0 && form.IsDisposed,
                        "Currency post-write session loss must close without reading or showing success."); return;
                }
                Assert(messages.Count == 1, "Currency result must show a single outcome, not load error plus success.");
                Assert(!messages[0].Contains("QA secret"), "Currency notification leaked technical failure details.");
                if (scenario == "readFailure" || scenario == "systemFailure" || scenario == "bindFailure")
                {
                    Assert(completed && messages[0].Contains(english ? "display" : "العرض"), "Completed currency write must distinguish display refresh failure.");
                    foreach (string action in new[] { "btnSave", "btnDelete", "btnSetSystem" })
                        Assert(!((Control)Field(form, action).GetValue(form)).Enabled, "Stale currencies must disable further writes.");
                    Invoke(form, "ClearEditor");
                    foreach (string staleHandler in new[] { "btnSave_Click", "btnDelete_Click", "btnSetSystem_Click" })
                    {
                        var repeats = Capture(() => form.GetType().GetMethod(staleHandler, BindingFlags.Instance | BindingFlags.NonPublic)
                            .Invoke(form, new object[] { null, EventArgs.Empty }), true, false);
                        Assert(writes == 1 && repeats.Count == 0, "A stale currency handler must not repeat a completed write, even after clearing the editor.");
                    }
                    form.Close();
                    faultEnabled = false;
                    using (var reopened = new CurrencySettingsForm(false, english, read, system,
                        row => { throw new InvalidOperationException("Reopening must not save."); },
                        id => { throw new InvalidOperationException("Reopening must not delete."); },
                        id => { throw new InvalidOperationException("Reopening must not change the system currency."); }))
                    {
                        reopened.ShowInTaskbar = false; reopened.Opacity = 0; reopened.MaximizeBox = false;
                        reopened.StartPosition = FormStartPosition.Manual; reopened.Location = new Point(-20000, -20000);
                        reopened.Show(); Application.DoEvents();
                        Assert(writes == 1 && ((Control)Field(reopened, "btnSave").GetValue(reopened)).Enabled,
                            "Reopening after read recovery must restore actions without repeating the write.");
                        reopened.Close();
                    }
                }
                else if (scenario == "writeFailure") Assert(!completed && reads == 1, "Failed currency write must not refresh or report completion.");
                else Assert(completed && reads == 2 && systemReads == 2, "Successful currency action must refresh both views once.");
                if (scenario == "empty")
                {
                    Assert(grid.Rows.Count == 0 && (int)Field(form, "selectedCurrencyId").GetValue(form) == 0,
                        "An empty currency list must discard the previous editor identity.");
                    foreach (string field in new[] { "txtCountry", "txtCurrencyName", "txtCurrencyCode", "txtCurrencySymbol" })
                        Assert(string.IsNullOrEmpty(((Control)Field(form, field).GetValue(form)).Text), "An empty currency list must clear previous record fields.");
                    Assert(!((Control)Field(form, "btnDelete").GetValue(form)).Enabled && !((Control)Field(form, "btnSetSystem").GetValue(form)).Enabled,
                        "An empty currency list must disable selected-record actions.");
                    var rejected = Capture(click, true, false);
                    Assert(writes == 1 && rejected.Count == 1, "Empty-list editor must validate instead of resubmitting the previous currency.");
                }
                form.Close();
            }
        }

        private static void Verify(string operation, string scenario, bool english)
        {
            bool periods = operation == "CreatePeriod" || operation == "CloseSelected";
            var state = new State { Operation = operation, Scenario = scenario };
            Func<DataTable> read = () =>
            {
                state.Read();
                var table = new DataTable();
                table.Columns.Add("معرف الفترة", typeof(Guid)); table.Columns.Add("من", typeof(DateTime)); table.Columns.Add("إلى", typeof(DateTime));
                table.Columns.Add("الحالة"); table.Columns.Add("وقت الإقفال", typeof(DateTime)); table.Columns.Add("أقفلها"); table.Columns.Add("ملاحظات");
                table.Rows.Add(state.Completed && operation == "CreatePeriod" ? CreatedId : Id,
                    new DateTime(2026, 1, 1), new DateTime(2026, 12, 31), state.Completed && operation == "CloseSelected" ? "مقفلة" : "مفتوحة", DBNull.Value, "", "QA");
                return table;
            };
            using (Form form = periods ? (Form)new AccountingPeriodManagementForm(false, read,
                (from, to, note) => { Assert(from == new DateTime(2026, 1, 1) && to == new DateTime(2026, 12, 31) && note == "QA notes {0}", "Period request changed dates or raw notes."); state.Write(); return CreatedId; },
                (id, note) => { Assert(id == Id && note == "QA notes {0}", "Period close changed identity or raw notes."); state.Write(); })
                : new ActiveIngredientManagementForm(false, new IngredientService(state)))
            {
                if (periods) ((AccountingPeriodManagementForm)form).ApplyPresentation(english);
                else ((ActiveIngredientManagementForm)form).ApplyPresentation(english);
                form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-20000, -20000);
                form.ShowInTaskbar = false; form.Opacity = 0; form.MaximizeBox = false;
                form.Show(); Application.DoEvents();
                var grid = (DataGridView)Field(form, periods ? "grid" : "ingredients").GetValue(form);
                Assert(grid.Rows.Count > 0, "Workspace fixture must bind a real row.");
                grid.CurrentCell = grid.Rows[0].Cells.Cast<DataGridViewCell>().First(c => c.Visible); grid.Rows[0].Selected = true;
                if (periods)
                {
                    AntdDateInput.SetDate((AntdUI.DatePicker)Field(form, "fromDate").GetValue(form), new DateTime(2026, 1, 1));
                    AntdDateInput.SetDate((AntdUI.DatePicker)Field(form, "toDate").GetValue(form), new DateTime(2026, 12, 31));
                    ((TextBox)Field(form, "notes").GetValue(form)).Text = "QA notes {0}";
                }
                else
                {
                    SetText(form, "arabicName", "QA-{0}-شركة"); SetText(form, "latinName", "Save");
                    SetText(form, "sourceTitle", "QA source {0}"); SetText(form, "publisher", "QA publisher");
                    SetText(form, "sourceUrl", "https://example.invalid/qa"); SetText(form, "strength", "5"); SetText(form, "strengthUnit", "mg");
                }
                if (scenario == "invalidDate")
                {
                    if (periods) AntdDateInput.SetDate((AntdUI.DatePicker)Field(form, "toDate").GetValue(form), new DateTime(2025, 12, 31));
                    else { var date = (AntdUI.DatePicker)Field(form, "retrievedOn").GetValue(form); date.Value = null; date.Text = "QA-invalid-date"; }
                }
                if (periods) Assert(Convert.ToString(grid.Rows[0].Cells["من"].FormattedValue).Contains("2026"), "ar-SA grid dates must remain Gregorian.");
                var status = (Control)Field(form, "status").GetValue(form);
                var seenStatus = new List<string>(); status.TextChanged += (s, e) => seenStatus.Add(status.Text);
                EventHandler failBind = (s, e) => { if (state.Completed) throw new IOException(Technical); };
                if (scenario == "bindFailure") grid.DataSourceChanged += failBind;
                var messages = Capture(() => Invoke(form, operation), scenario != "dismiss", operation == "CloseSelected");
                Console.WriteLine("Workspace: " + operation + "/" + scenario + "/" + english + "; writes=" + state.Writes + "; notices=" + messages.Count);
                Assert(!seenStatus.Any(text => text.Contains(Technical)) && messages.All(text => !text.Contains(Technical)), "Technical read/write failure leaked into workspace text.");
                if (scenario == "permissionLost")
                {
                    Assert(state.Writes == 1 && state.Completed && state.Reads == state.ReadsAtWrite,
                        "A revoked session must not reload workspace data after the write returns.");
                    Assert(form.IsDisposed || !form.Visible, "Revoked ingredient workspace must close as before.");
                    return;
                }
                if (scenario == "readFailure" || scenario == "bindFailure")
                {
                    Assert(state.Writes == 1 && state.Completed && messages.Count == 1 &&
                        messages[0].Contains(english ? "Do not repeat the operation" : "لا تكرر العملية"), "Completed save needs one explicit follow-up, not a generic failure or overwritten success.");
                    Guid reference = operation == "CreatePeriod" || operation == "AddIngredient" ? CreatedId : operation == "AddEvidence" || operation == "DeactivateSelectedEvidence" ? EvidenceId : Id;
                    Assert(messages[0].Contains(reference.ToString()), "Follow-up must retain the completed record identity.");
                    Invoke(form, periods ? "UpdateActions" : "IngredientSelectionChanged");
                    Assert(!form.Controls.Find(periods ? "createAccountingPeriod" : "activeIngredientAdd", true).Single().Enabled,
                        "Stale workspace actions must remain disabled after selection repaint.");
                    Capture(() => Invoke(form, operation), true, false);
                    Assert(state.Writes == 1, "Stale direct re-entry must not repeat a write.");
                    state.FailReads = false; grid.DataSourceChanged -= failBind;
                    if(periods) form.Controls.Find("refreshAccountingPeriods",true).OfType<AntdUI.Button>().Single().PerformClick();
                    else Invoke(form,"InitializeData");
                    Assert(form.Controls.Find(periods ? "createAccountingPeriod" : "activeIngredientAdd", true).Single().Enabled,
                        "Successful reload must restore workspace editing.");
                }
                else if (scenario == "writeFailure")
                {
                    Assert(state.Writes == 1 && !state.Completed && messages.Count == 1 &&
                        !messages[0].Contains("Do not repeat the operation"), "Unreturned write must not claim completion.");
                    Assert(((Control)Field(form, periods ? "notes" : operation == "SaveMapping" ? "strength" : "sourceTitle").GetValue(form)).Text ==
                        (periods ? "QA notes {0}" : operation == "SaveMapping" ? "5" : "QA source {0}"), "Rejected write must not clear the editor.");
                }
                else if (scenario == "invalidDate")
                    Assert(state.Writes == 0 && messages.Count == 0 && status.Text.Contains(english ?
                        periods ? "start" : "date" : periods ? "تاريخ" : "تاريخ"), "Invalid dates must retain inline validation and prevent any write.");
                else if (scenario == "dismiss") Assert(state.Writes == 0 && !state.Completed, "Dismissed confirmation must not submit a write.");
                else Assert(state.Writes == 1 && state.Completed && messages.Count == 0, "Ordinary successful save should keep its inline success status.");
                if (periods && scenario == "success")
                {
                    SessionContext.Clear(); Invoke(form, "UpdateActions");
                    Assert(!form.Controls.Find("createAccountingPeriod", true).Single().Enabled &&
                        form.Controls.Find("checkPeriodReadiness", true).Single().Enabled,
                        "Read-only readiness must retain its existing availability independently of write permission.");
                    SessionContext.Start("QA_WORKSPACE_UI", "Manager"); Invoke(form, "UpdateActions");
                }
                if (operation == "CloseSelected" && state.Completed)
                {
                    var repeated = Capture(() => Invoke(form, operation), true, true);
                    Assert(state.Writes == 1 && repeated.Count == 0, "A reloaded closed period must reject direct close re-entry before submitting.");
                }
                Assert(status.AccessibleName == status.Text, "Workspace status must expose its full current text to accessibility.");
                form.Close();
            }
        }

        private static void VerifyPeriodRefresh(bool english, bool preview)
        {
            int reads = 0, writes = 0, shape = 0;
            bool fail = false;
            Func<DataTable> read = () =>
            {
                ++reads;
                if (fail) throw new IOException(Technical);
                var table = new DataTable();
                table.Columns.Add("معرف الفترة", typeof(Guid)); table.Columns.Add("من", typeof(DateTime)); table.Columns.Add("إلى", typeof(DateTime));
                table.Columns.Add("الحالة"); table.Columns.Add("وقت الإقفال", typeof(DateTime)); table.Columns.Add("أقفلها"); table.Columns.Add("ملاحظات");
                if (shape < 2 && reads % 2 == 0) table.Rows.Add(Id, new DateTime(2025, 1, 1), new DateTime(2025, 12, 31), "مفتوحة", DBNull.Value, "", "QA");
                if (shape != 3) table.Rows.Add(CreatedId, new DateTime(2026, 1, 1), new DateTime(2026, 12, 31), "مفتوحة", DBNull.Value, "", "QA");
                if (shape < 2 && reads % 2 != 0) table.Rows.Add(Id, new DateTime(2025, 1, 1), new DateTime(2025, 12, 31), "مفتوحة", DBNull.Value, "", "QA");
                return table;
            };
            using (var font = AppFonts.Create(14F))
            using (var form = new AccountingPeriodManagementForm(preview, read,
                (from, to, note) => { ++writes; throw new InvalidOperationException("Refresh submitted a create."); },
                (id, note) => { ++writes; throw new InvalidOperationException("Refresh submitted a close."); }))
            {
                form.ApplyPresentation(english); form.StartPosition = FormStartPosition.Manual;
                form.Location = new Point(-20000, -20000); form.ShowInTaskbar = false; form.Opacity = 0; form.MaximizeBox = false;
                form.Show(); Application.DoEvents();
                var refresh = form.Controls.Find("refreshAccountingPeriods", true).OfType<AntdUI.Button>().SingleOrDefault();
                Assert(refresh != null, "Accounting periods needs a direct Refresh action.");
                Assert(refresh.Text == (english ? "Refresh" : "تحديث") && !string.IsNullOrWhiteSpace(refresh.AccessibleDescription),
                    "Refresh caption and accessible recovery instruction must follow the selected language.");
                foreach (var pair in new[] {
                    new[] { "gridCard", "Accounting period list", "قائمة الفترات المحاسبية" },
                    new[] { "editorCard", "Accounting period details", "بيانات الفترة المحاسبية" },
                    new[] { "editorFields", "Accounting period details", "بيانات الفترة المحاسبية" },
                    new[] { "notesFrame", "Period notes", "ملاحظات الفترة" },
                    new[] { "footer", "Accounting period actions", "إجراءات الفترات المحاسبية" },
                    new[] { "actions", "Accounting period actions", "إجراءات الفترات المحاسبية" } })
                    Assert(((Control)Field(form, pair[0]).GetValue(form)).AccessibleName == pair[english ? 1 : 2],
                        "Container accessibility must describe its purpose independently of nearby labels or status.");
                var grid = (DataGridView)Field(form, "grid").GetValue(form);
                var fromDate = (AntdUI.DatePicker)Field(form, "fromDate").GetValue(form);
                var toDate = (AntdUI.DatePicker)Field(form, "toDate").GetValue(form);
                var note = (TextBox)Field(form, "notes").GetValue(form);
                AntdDateInput.SetDate(fromDate, new DateTime(2025, 2, 3)); AntdDateInput.SetDate(toDate, new DateTime(2027, 4, 5));
                note.Text = "QA draft {0} ملاحظات Save";
                if (!preview) { grid.CurrentCell = grid.Rows[1].Cells["من"]; grid.Rows[1].Selected = true; }
                refresh.PerformClick();
                Assert(writes == 0 && reads == (preview ? 0 : 2), "Refresh must read once and must never submit a write; preview must read nothing.");
                Assert(note.Text == "QA draft {0} ملاحظات Save" && fromDate.Value == new DateTime(2025, 2, 3) && toDate.Value == new DateTime(2027, 4, 5),
                    "Refresh must preserve raw notes and date drafts.");
                if (!preview)
                {
                    Assert((Guid)grid.CurrentRow.Cells["معرف الفترة"].Value == Id, "Refresh must preserve the selected period by identity.");
                    fail = true;
                    var errors = Capture(() => refresh.PerformClick(), true, false);
                    Assert(errors.Count == 1 && !errors[0].Contains(Technical) && refresh.Enabled &&
                        !form.Controls.Find("createAccountingPeriod", true).Single().Enabled, "Failed refresh must allow retry without enabling stale writes or exposing diagnostics.");
                    fail = false;
                    typeof(Control).GetMethod("OnKeyDown", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, new object[] { new KeyEventArgs(Keys.F5) });
                    Assert(reads == 4 && writes == 0 && form.Controls.Find("createAccountingPeriod", true).Single().Enabled,
                        "F5 must recover stale data through the same read-only action.");
                    shape = 2; refresh.PerformClick();
                    Assert((Guid)grid.CurrentRow.Cells["معرف الفترة"].Value == CreatedId, "Missing selected record must fall back to a current row.");
                    shape = 3; refresh.PerformClick();
                    Assert(grid.Rows.Count == 0 && !form.Controls.Find("closeAccountingPeriod", true).Single().Enabled,
                        "Empty refresh must remove stale selection and disable period close.");
                }
                var actions = (FlowLayoutPanel)Field(form, "actions").GetValue(form);
                foreach (Control action in actions.Controls) action.Font = font;
                foreach (var size in new[] { new Size(760, 640), new Size(1240, 760), new Size(760, 640) })
                {
                    form.ClientSize = size; form.PerformLayout(); actions.PerformLayout(); Application.DoEvents();
                    foreach (Control action in actions.Controls)
                    {
                        int textWidth = TextRenderer.MeasureText(action.Text, action.Font, Size.Empty, TextFormatFlags.SingleLine | TextFormatFlags.NoPadding).Width;
                        Assert(actions.ClientRectangle.Contains(action.Bounds) && action.Width >= textWidth + 32,
                            "Refresh footer bounds: " + action.Name + "; frame=" + form.ClientSize + "; panel=" + actions.ClientRectangle +
                            "; parent=" + actions.Parent.Bounds + "; parentClient=" + actions.Parent.ClientSize + "; parentPadding=" + actions.Parent.Padding + "; dock=" + actions.Dock + "; max=" + actions.MaximumSize + "; height=" + actions.Height + "; type=" + actions.GetType().Name + "; action=" + action.Bounds + "; textWidth=" + textWidth + "; margin=" + action.Margin + "; padding=" + actions.Padding + "; english=" + english);
                        Assert(!actions.Controls.Cast<Control>().Any(other => other != action && other.Bounds.IntersectsWith(action.Bounds)),
                            "Period footer actions must not overlap after compact/wide/compact resize.");
                    }
                }
                Assert(note.Text == "QA draft {0} ملاحظات Save", "Layout must preserve draft notes.");
                if (!preview)
                {
                    int before = reads; SessionContext.Clear();
                    typeof(Control).GetMethod("OnKeyDown", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, new object[] { new KeyEventArgs(Keys.F5) });
                    Assert(reads == before && form.IsDisposed, "Refresh after session end must close without another data read.");
                    SessionContext.Start("QA_WORKSPACE_UI", "Manager");
                }
                else form.Close();
            }
        }

        private sealed class State
        {
            internal string Operation, Scenario; internal bool Completed, FailReads = true; internal int Writes, Reads, ReadsAtWrite;
            internal void Read() { ++Reads; if (Completed && FailReads && Scenario == "readFailure") throw new IOException(Technical); }
            internal void Write() { ++Writes; if (Scenario == "writeFailure") throw new IOException(Technical); Completed = true; ReadsAtWrite = Reads; if (Scenario == "permissionLost") SessionContext.Clear(); }
        }
        private sealed class IngredientService : IActiveIngredientService
        {
            private readonly State state; internal IngredientService(State state) { this.state = state; }
            public IReadOnlyList<ActiveIngredientSummary> GetIngredients() { state.Read(); return new[] { new ActiveIngredientSummary { ActiveIngredientId = state.Completed && state.Operation == "AddIngredient" ? CreatedId : Id, ArabicName = "QA", LatinName = "QA", IsActive = true } }; }
            public IReadOnlyList<string> GetMedicineNames() { state.Read(); return new[] { "QA medicine" }; }
            public IReadOnlyList<ActiveIngredientEvidenceRecord> GetEvidence(Guid id) { state.Read(); return new[] { new ActiveIngredientEvidenceRecord { EvidenceId = EvidenceId, SourceTitle = "QA evidence", Publisher = "QA", IsActive = true } }; }
            public IReadOnlyList<ActiveIngredientMappingRecord> GetMappings(Guid id) { state.Read(); return new[] { new ActiveIngredientMappingRecord { MedicineName = "QA medicine", StrengthValue = 5, StrengthUnit = "mg" } }; }
            private void Submit(OperationActor actor) { Assert(actor.UserName == "QA_WORKSPACE_UI" && actor.UserRole == "Manager", "Ingredient actor contract changed."); state.Write(); }
            public Guid CreateIngredientWithEvidence(string arabic, string latin, ActiveIngredientEvidenceInput evidence, OperationActor actor) { Assert(arabic == "QA-{0}-شركة" && latin == "Save" && evidence.SourceTitle == "QA source {0}", "Ingredient request changed raw names/reference."); Submit(actor); return CreatedId; }
            public Guid AddEvidence(Guid id, ActiveIngredientEvidenceInput evidence, OperationActor actor) { Assert(id == Id && evidence.SourceTitle == "QA source {0}", "Evidence request changed raw identity/reference."); Submit(actor); return EvidenceId; }
            public void DeactivateEvidence(Guid id, OperationActor actor) { Assert(id == EvidenceId, "Evidence deactivation changed identity."); Submit(actor); }
            public void ToggleIngredient(Guid id, OperationActor actor) { Assert(id == Id, "Ingredient toggle changed identity."); Submit(actor); }
            public void SaveMapping(Guid id, string medicine, string strength, string unit, bool primary, OperationActor actor) { Assert(id == Id && medicine == "QA medicine" && strength == "5" && unit == "mg", "Mapping request changed raw data."); Submit(actor); }
        }
        internal static List<string> Capture(Action action, bool approve, bool checkDate)
        {
            var messages = new List<string>(); Exception failure = null;
            using (var timer = new System.Windows.Forms.Timer { Interval = 10 })
            {
                timer.Tick += (s, e) =>
                {
                    var dialog = Application.OpenForms.OfType<AppNotificationForm>().FirstOrDefault(); if (dialog == null) return;
                    try
                    {
                        if (dialog.Controls.Find("notificationYes", true).Length > 0)
                        {
                            if (checkDate) Assert(dialog.BuildCopyText().Contains("2026/01/01") && dialog.BuildCopyText().Contains("2026/12/31"), "ar-SA close confirmation must show Gregorian dates.");
                            dialog.Controls.Find(approve ? "notificationYes" : "notificationNo", true).OfType<AntdUI.Button>().Single().PerformClick();
                        }
                        else { messages.Add(dialog.BuildCopyText()); dialog.Controls.Find("notificationOK", true).OfType<AntdUI.Button>().Single().PerformClick(); }
                    }
                    catch (Exception error) { failure = error; dialog.Close(); }
                };
                timer.Start(); try { action(); } finally { timer.Stop(); }
            }
            if (failure != null) throw new InvalidOperationException("Owned workspace dialog failed.", failure);
            return messages;
        }
        private static FieldInfo Field(Form form, string name) => form.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        private static void SetText(Form form, string name, string text) => ((Control)Field(form, name).GetValue(form)).Text = text;
        private static void Invoke(Form form, string name) { try { form.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, null); } catch (TargetInvocationException error) { throw error.InnerException ?? error; } }
        private static void Assert(bool value, string message) { ++assertions; if (!value) throw new InvalidOperationException(message); }
    }
}

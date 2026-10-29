using System;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace project_fo_3
{
    internal static class PharmacyProfileFollowUpSelfTests
    {
        private static int cases, assertions;
        private static readonly string[] Fields = { "NameAr", "NameEn", "ActivityAr", "ActivityEn", "CommercialRegNo", "LicenseNo", "Mobile", "Phone", "TaxNo", "Address", "InvoiceNotes", "HeaderFooter", "Fax", "PoBox", "Email", "Website", "LogoPath" };
        internal static void Run()
        {
            if (SessionContext.IsAuthenticated) throw new InvalidOperationException("Owned profile verification requires an empty session.");
            string theme = Properties.Setings.Default.ThemeMode; cases = assertions = 0;
            try
            {
                foreach (string mode in new[] { "Light", "Dark" })
                foreach (bool english in new[] { false, true })
                foreach (string scenario in new[] { "unreadableLogo", "validationName", "validationPhone", "validationAddress", "readFailure", "nullRead", "uiFailure", "success", "writeFailure", "auditFailure", "permissionLost", "permissionBeforeSave", "preview", "initialReadFailure", "disposedAfterWrite", "reentry" }.Concat(Fields.Select(n => "mismatch:" + n)))
                {
                    SessionContext.Start("QA_PROFILE_UI", "Manager"); Properties.Setings.Default.ThemeMode = mode;
                    Verify(scenario, english); ++cases;
                }
            }
            finally { SessionContext.Clear(); Properties.Setings.Default.ThemeMode = theme; }
            Console.WriteLine("PASS pharmacy profile follow-up: cases=" + cases + "; assertions=" + assertions + "; 17-field readback, both themes/languages, real handlers and fake persistence; no SQL, filesystem profile save or desktop input.");
        }
        private static void Verify(string scenario, bool english)
        {
            int reads = 0, writes = 0, audits = 0; bool reentered = false, uiFault = true;
            PharmacyProfile saved = null; PharmacyCompanyForm form = null; Action click = null;
            var original = new PharmacyProfile { NameAr = "صيدلية QA {0}", NameEn = "QA Pharmacy {0}", ActivityAr = "نشاط {0}", ActivityEn = "Activity {0}",
                CommercialRegNo = "CR-{0}", LicenseNo = "LIC-{0}", Mobile = "+967700000001", Phone = "+9672000001", TaxNo = "TAX-{0}", Address = "عدن QA {0}",
                InvoiceNotes = "Notes {0}", HeaderFooter = "Footer {0}", Fax = "FAX-{0}", PoBox = "BOX-{0}", Email = "qa{0}@example.test", Website = "example.test/{0}", LogoPath = "" };
            if (scenario == "unreadableLogo")
            {
                string root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "PharmacyProfileFollowUpFixtures");
                System.IO.Directory.CreateDirectory(root);
                original.LogoPath = System.IO.Path.Combine(root, Guid.NewGuid().ToString("N") + ".png");
                System.IO.File.WriteAllBytes(original.LogoPath, new byte[] { 0, 1, 2, 3, 4 });
            }
            Func<PharmacyProfile> read = () =>
            {
                ++reads;
                if (scenario == "initialReadFailure" || (writes > 0 && scenario == "readFailure")) throw new InvalidOperationException("QA secret profile read");
                if (writes > 0 && scenario == "nullRead") return null;
                var result = Clone(saved ?? original);
                if (writes > 0 && scenario.StartsWith("mismatch:", StringComparison.Ordinal))
                    typeof(PharmacyProfile).GetProperty(scenario.Substring(9)).SetValue(result, "Different persisted value", null);
                return result;
            };
            using (form = new PharmacyCompanyForm(scenario == "preview", english, read, profile =>
            {
                ++writes;
                if (scenario == "writeFailure") throw new InvalidOperationException("QA secret profile write");
                saved = Clone(profile);
                if (scenario == "reentry" && !reentered) { reentered = true; click(); }
                if (scenario == "permissionLost") SessionContext.Clear();
                if (scenario == "disposedAfterWrite") form.Dispose();
            }, profile =>
            {
                ++audits;
                if (scenario == "auditFailure") throw new InvalidOperationException("QA secret best-effort audit");
            }))
            {
                form.ShowInTaskbar = false; form.Opacity = 0; form.MaximizeBox = false;
                form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-20000, -20000);
                var initialNotices = Capture(() => { form.Show(); Application.DoEvents(); });
                click = () => Invoke(form, "btnSave_Click");
                var saveButton = form.Controls.Find("btnToolSave", true).Single();
                if (scenario == "initialReadFailure")
                {
                    Assert(initialNotices.Count == 1 && !saveButton.Enabled, "Failed initial profile load must prevent writing over unknown data.");
                    Assert(Capture(click).Count == 0 && writes == 0, "Unloaded profile handler submitted a write."); form.Close(); return;
                }
                Assert(initialNotices.Count == 0, "Owned initial profile read failed unexpectedly.");
                if (scenario == "unreadableLogo")
                {
                    var hint = form.Controls.Find("lblLogoHint", true).Single();
                    Assert(hint.Visible && hint.Text.Contains(english ? "logo" : "الشعار") && hint.AccessibleName == hint.Text && saveButton.Enabled,
                        "Unreadable logo must preserve editable metadata with a clear accessible image hint.");
                }
                string invalidField = scenario == "validationName" ? "nameArEditor" : scenario == "validationPhone" ? "txtPhone" : scenario == "validationAddress" ? "addressEditor" : null;
                if (invalidField != null) ((Control)form.GetType().GetField(invalidField, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form)).Text = "";
                var before = Current(form);
                var strip = form.Controls.Find("__UiStatusStrip", true).OfType<StatusStrip>().FirstOrDefault();
                if (strip != null)
                {
                    var info = strip.Items["__UiStatusInfo"] as ToolStripStatusLabel;
                    if (info != null) info.TextChanged += (sender, args) =>
                    {
                        if (writes > 0 && uiFault && scenario == "uiFailure") { uiFault = false; throw new InvalidOperationException("QA secret profile presentation"); }
                    };
                }
                if (scenario == "permissionBeforeSave") SessionContext.Clear();
                var notices = Capture(click);
                if (scenario == "preview") { Assert(reads == 0 && writes == 0 && audits == 0 && notices.Count == 0 && !saveButton.Enabled, "Profile preview reached persistence or exposed Save."); return; }
                if (scenario == "permissionBeforeSave") { Assert(form.IsDisposed && writes == 0 && reads == 1 && notices.Count == 0, "Expired profile session submitted before checking access."); return; }
                if (invalidField != null)
                {
                    Assert(writes == 0 && reads == 1 && audits == 0 && notices.Count == 1 &&
                        notices[0].Contains(english ? "Enter the Arabic name" : "الرجاء إدخال الاسم") && Equal(before, Current(form)),
                        "Required profile validation must retain its draft/language and avoid persistence."); form.Close(); return;
                }
                Assert(writes == 1, "Profile save must submit exactly once.");
                if (scenario == "permissionLost" || scenario == "disposedAfterWrite")
                { Assert(form.IsDisposed && reads == 1 && audits == 0 && notices.Count == 0, "Late profile result reused a disposed/unauthorized owner."); return; }
                Assert(notices.Count == 1 && !notices[0].Contains("QA secret"), "Profile result must be singular and avoid technical details.");
                Assert(Equal(before, Current(form)), "Profile save changed raw input values.");
                bool unverified = scenario == "readFailure" || scenario == "nullRead" || scenario.StartsWith("mismatch:", StringComparison.Ordinal);
                if (unverified || scenario == "uiFailure")
                {
                    Assert(saved != null && notices[0].Contains(original.NameAr) && notices[0].Contains(english ? (unverified ? "read-back" : "display") : (unverified ? "تطابق القراءة" : "العرض")),
                        "Returned profile write must distinguish unverified readback from display-only failure and retain raw reference.");
                    Assert(!saveButton.Enabled && !form.Controls.Find("btnToolAdd", true).Single().Enabled && !form.Controls.Find("btnToolPrint", true).Single().Enabled,
                        "Unreviewed profile must disable save/new/print.");
                    Assert(audits == (unverified ? 0 : 1), "Profile audit must follow a complete readback match.");
                    var repeated = Capture(click); Invoke(form, "btnAdd_Click");
                    Assert(repeated.Count == 0 && writes == 1 && Equal(before, Current(form)), "Unreviewed profile repeated a write or cleared the draft.");
                }
                else if (scenario == "writeFailure") Assert(saved == null && reads == 1 && audits == 0 && saveButton.Enabled, "Rejected profile write changed the draft/read/audit outcome.");
                else Assert(saved != null && reads == 2 && audits == 1 && saveButton.Enabled, "Verified profile save must complete without repeating persistence.");
                form.Close();
            }
        }
        private static PharmacyProfile Clone(PharmacyProfile source)
        {
            var result = new PharmacyProfile();
            foreach (string name in Fields) { var p = typeof(PharmacyProfile).GetProperty(name); p.SetValue(result, p.GetValue(source, null), null); }
            return result;
        }
        private static bool Equal(PharmacyProfile left, PharmacyProfile right) => Fields.All(n => Equals(typeof(PharmacyProfile).GetProperty(n).GetValue(left, null), typeof(PharmacyProfile).GetProperty(n).GetValue(right, null)));
        private static PharmacyProfile Current(Form form) => (PharmacyProfile)form.GetType().GetMethod("ReadFromForm", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, null);
        private static System.Collections.Generic.List<string> Capture(Action action) => WorkspaceWriteFollowUpSelfTests.Capture(action, true, false);
        private static void Invoke(Form form, string name)
        {
            try { form.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, new object[] { null, EventArgs.Empty }); }
            catch (TargetInvocationException error) { throw error.InnerException ?? error; }
        }
        private static void Assert(bool value, string message) { ++assertions; if (!value) throw new InvalidOperationException(message); }
    }
}

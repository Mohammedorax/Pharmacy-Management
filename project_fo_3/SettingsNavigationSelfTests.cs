using System;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace project_fo_3
{
    internal static class SettingsNavigationSelfTests
    {
        private static int assertions, cases;
        private static readonly string[][] Routes =
        {
            new[] { "basic", "OpenBasicData" }, new[] { "currency", "OpenCurrencySettings" },
            new[] { "compliance", "OpenSaudiComplianceSettings" }, new[] { "accessibility", "OpenAccessibilitySettings" },
            new[] { "language", "OpenLanguageSettings" }, new[] { "devices", "OpenDeviceSettings" },
            new[] { "sales-shortcuts", "OpenSalesShortcuts" }, new[] { "excel", "OpenExcelDataTransfer" },
            new[] { "excel-import", "OpenMedicineCatalogImport" }, new[] { "invoice-drafts", "OpenInvoiceImportDrafts" },
            new[] { "cashboxes", "OpenCashBoxes" }
        };
        internal static void Run()
        {
            if (SessionContext.IsAuthenticated) throw new InvalidOperationException("Owned navigation verification requires an empty session.");
            string theme = Properties.Setings.Default.ThemeMode;
            assertions = cases = 0;
            try
            {
                foreach (string mode in new[] { "Light", "Dark" })
                foreach (bool english in new[] { false, true })
                {
                    Properties.Setings.Default.ThemeMode = mode;
                    VerifyNotificationOwners(english);
                    foreach (string[] route in Routes) VerifyRoute(route, english);
                    VerifyPreview(english);
                    VerifyAccessibility(english);
                }
            }
            finally { SessionContext.Clear(); Properties.Setings.Default.ThemeMode = theme; }
            Console.WriteLine("PASS settings navigation: cases=" + cases + "; assertions=" + assertions + "; all eleven non-tax routes; owned modal handlers and injected factories; no SQL, persistence or desktop input.");
        }
        private static void VerifyRoute(string[] route, bool english)
        {
            SessionContext.Start("QA_SETTINGS_NAV", "Manager");
            int opens = 0; string phase = "cancel"; Exception callbackFailure = null; SettingsForm owner = null;
            Func<string, bool, Form> factory = (key, direction) =>
            {
                ++opens; Assert(key == route[0] && direction == english, "Navigation changed route or presentation language.");
                if (phase == "failure") throw new InvalidOperationException("QA secret settings factory");
                var child = new Form { Text = "Owned navigation fixture", ShowInTaskbar = false, Opacity = 0,
                    StartPosition = FormStartPosition.Manual, Location = new Point(-20000, -20000) };
                child.Shown += (sender, args) => child.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        if (phase == "reentry")
                        {
                            phase = "cancel";
                            Invoke(owner, route[1]);
                            Invoke(owner, "OpenTaxSettings");
                            phase = "reentry";
                        }
                        if (phase == "session") SessionContext.Clear();
                    }
                    catch (Exception error) { callbackFailure = error; }
                    finally { child.Close(); }
                }));
                return child;
            };
            using (owner = new SettingsForm(true, false, english,
                direction => { throw new InvalidOperationException("Tax navigation must be blocked while another settings dialog is open."); }, factory))
            {
                Prepare(owner); var bounds = owner.Bounds; var state = owner.WindowState;
                foreach (string scenario in new[] { "cancel", "failure", "reentry", "session" })
                {
                    phase = scenario; opens = 0; callbackFailure = null;
                    var notices = WorkspaceWriteFollowUpSelfTests.Capture(() => Invoke(owner, route[1]), true, false);
                    if (callbackFailure != null) throw new InvalidOperationException("Owned navigation callback failed.", callbackFailure);
                    Assert(opens == 1, "Navigation opened duplicate dialogs or failed to reset its guard.");
                    if (scenario == "session") Assert(owner.IsDisposed && notices.Count == 0, "Session loss must close the settings owner without a result notice.");
                    else
                    {
                        Assert(!owner.IsDisposed && owner.Enabled && owner.Bounds == bounds && owner.WindowState == state,
                            "Navigation did not restore its owner geometry and enabled state.");
                        Assert(notices.Count == (scenario == "failure" ? 1 : 0) && notices.All(n => !n.Contains("QA secret")),
                            "Navigation must show one safe opening failure and no notices for normal cancellation/reentry.");
                    }
                    ++cases;
                }
            }
            SessionContext.Start("QA_SETTINGS_NAV", "Manager"); opens = 0;
            using (owner = new SettingsForm(true, false, english,
                direction => { throw new InvalidOperationException("Expired session must not open tax."); }, factory))
            {
                Prepare(owner); SessionContext.Clear();
                var notices = WorkspaceWriteFollowUpSelfTests.Capture(() => Invoke(owner, route[1]), true, false);
                Assert(opens == 0 && owner.IsDisposed && notices.Count == 0, "Expired session reached a settings factory."); ++cases;
            }
        }
        private static void VerifyPreview(bool english)
        {
            SessionContext.Start("QA_SETTINGS_NAV", "Manager"); int opens = 0;
            using (var form = new SettingsForm(true, true, english,
                direction => { ++opens; throw new InvalidOperationException("Preview reached tax."); },
                (key, direction) => { ++opens; throw new InvalidOperationException("Preview reached factory."); }))
            {
                Prepare(form);
                foreach (string[] route in Routes)
                {
                    var notices = WorkspaceWriteFollowUpSelfTests.Capture(() => Invoke(form, route[1]), true, false);
                    Assert(opens == 0 && notices.Count == 0 && !form.IsDisposed, "Preview opened a real settings path."); ++cases;
                }
                form.Close();
            }
        }
        private static void VerifyNotificationOwners(bool english)
        {
            foreach (string kind in new[] { "company", "language", "devices", "shortcuts", "accessibility" })
            {
                Form form;
                switch (kind)
                {
                    case "company": form = new PharmacyCompanyForm(true, english); break;
                    case "language": form = new LanguageSettingsForm(true, english); break;
                    case "devices": form = new DeviceSettingsForm(true, english); break;
                    case "shortcuts": form = new SalesShortcutSettingsForm(new SalesShortcutProfileStore(
                        System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "owned-shortcut-preview")), true, english); break;
                    default: form = new AccessibilitySettingsForm(true, english); break;
                }
                using (form)
                {
                    Prepare(form);
                    var notices = WorkspaceWriteFollowUpSelfTests.Capture(() =>
                        UiTheme.ShowOperationIssue(form, "فتح إعدادات النظام", new InvalidOperationException("QA secret owner language")), true, false);
                    Assert(notices.Count == 1 && !notices[0].Contains("QA secret") &&
                        notices[0].Contains(english ? "Open system settings" : "فتح إعدادات النظام"),
                        "Settings notification did not inherit its actual preview owner language: " + kind);
                    if (kind == "company")
                    {
                        var help = WorkspaceWriteFollowUpSelfTests.Capture(() =>
                            form.GetType().GetMethod("btnHelp_Click", BindingFlags.Instance | BindingFlags.NonPublic)
                                .Invoke(form, new object[] { null, EventArgs.Empty }), true, false);
                        Assert(help.Count == 1 && help[0].Contains(english ? "Enter the business details" : "أدخل بيانات الشركة"),
                            "Actual company help did not retain its explicit owner presentation."); ++cases;
                    }
                    form.Close(); ++cases;
                }
            }
        }

        private static void VerifyAccessibility(bool english)
        {
            bool original = ScreenModeSettings.LoadOpeningPreference();
            foreach (bool preview in new[] { false, true })
            {
                int writes = 0; bool submitted = false; string outcome = "success";
                using (var form = new AccessibilitySettingsForm(preview, english, value =>
                {
                    ++writes; submitted = value;
                    if (outcome == "throw") throw new InvalidOperationException("QA secret opening preference");
                    return outcome == "success";
                }))
                {
                    Prepare(form);
                    var body = form.Controls.Find("accessibilityBody", true).OfType<FlowLayoutPanel>().Single();
                    var status = form.Controls.Find("accessibilityStatus", true).OfType<Label>().Single();
                    var save = form.Controls.Find("saveAccessibility", true).OfType<AntdUI.Button>().Single();
                    var centered = form.Controls.Find("openCentered", true).OfType<RadioButton>().Single();
                    var maximized = form.Controls.Find("openMaximized", true).OfType<RadioButton>().Single();
                    Assert(form.Text == (english ? "Default accessibility" : "تسهيلات الاستخدام الافتراضية") &&
                        form.RightToLeft == (english ? RightToLeft.No : RightToLeft.Yes) &&
                        centered.Text == (english ? "Open centered" : "فتح في وسط الصفحة") &&
                        maximized.Text == (english ? "Open maximized" : "فتح كامل"),
                        "Accessibility presentation must inherit the owner without enabling English globally.");
                    Assert(form.AcceptButton == save && form.CancelButton != null && save.Enabled == !preview,
                        "Accessibility preview/dialog button contract changed.");
                    foreach (Size size in new[] { new Size(360, 300), new Size(640, 420), new Size(1024, 680) })
                    {
                        OwnedTestWindowViewport.ResizeOuter(form, size);
                        foreach (string phase in preview ? new[] { "preview" } : new[] { "success", "failure", "throw" })
                        {
                            outcome = phase; int before = writes;
                            maximized.Checked = phase == "success";
                            centered.Checked = !maximized.Checked;
                            save.PerformClick(); Application.DoEvents();
                            Assert(centered.Checked != maximized.Checked, "Opening preference options must remain mutually exclusive.");
                            if (preview) Assert(writes == 0, "Accessibility preview saved an opening preference.");
                            else
                            {
                                Assert(writes == before + 1 && submitted == maximized.Checked, "Accessibility save did not submit its selected mode once.");
                                string expected = phase == "success" ? "تم الحفظ؛ سيطبق الاختيار عند فتح الشاشة التالية." : "تعذر تأكيد حفظ الإعداد؛ حاول مرة أخرى.";
                                Assert(status.Text == (english ? UiLanguageCatalog.Translate(expected) : expected),
                                    "Accessibility save outcome lost its presentation language.");
                            }
                            Assert(status.AccessibleName == status.Text && !status.Text.Contains("QA secret") &&
                                centered.AccessibleName == centered.Text && maximized.AccessibleName == maximized.Text,
                                "Accessibility status/options must expose complete safe accessible text.");
                            int required = TextRenderer.MeasureText(status.Text, status.Font,
                                new Size(Math.Max(1, status.Width - status.Padding.Horizontal), int.MaxValue),
                                TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height;
                            Assert(status.Height >= required && status.Width <= body.ClientSize.Width - body.Padding.Horizontal,
                                "Accessibility operation status clips at the tested size.");
                            ++cases;
                        }
                    }
                    Assert(ScreenModeSettings.LoadOpeningPreference() == original, "Owned accessibility test changed the stored preference.");
                    form.Close();
                }
            }
        }

        private static void Prepare(Form form)
        {
            form.ShowInTaskbar = false; form.Opacity = 0; form.MaximizeBox = false;
            form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-20000, -20000);
            form.Show(); Application.DoEvents();
        }
        private static void Invoke(Form form, string method)
        {
            try { form.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, new object[] { null, EventArgs.Empty }); }
            catch (TargetInvocationException error) { throw error.InnerException ?? error; }
        }
        private static void Assert(bool condition, string message) { ++assertions; if (!condition) throw new InvalidOperationException(message); }
    }
}

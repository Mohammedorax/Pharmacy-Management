using System;
using System.Collections.Generic;
using System.Configuration;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Windows.Forms;

namespace project_fo_3
{
    internal static class AccessibilitySettingsStorageSelfTests
    {
        private sealed class KeyboardPreview : AccessibilitySettingsForm
        {
            internal bool ActivatedDuringTest {get;private set;}
            internal KeyboardPreview()
            {
                ShowInTaskbar=false;Opacity=0;StartPosition=FormStartPosition.Manual;
                Location=new Point(-20000,-20000);
            }
            protected override bool ShowWithoutActivation => true;
            protected override void OnActivated(EventArgs args){ActivatedDuringTest=true;base.OnActivated(args);}
        }

        internal static void VerifyKeyboard(string token)
        {
            string settingsPath=RequireFreshOwnedCopy(token);
            bool original=ScreenModeSettings.LoadOpeningPreference();
            string originalTheme=Properties.Setings.Default.ThemeMode;
            bool initialized=false;
            int cases=0;
            try
            {
                Check(ScreenModeSettings.TrySaveOpeningPreference(!original),"Could not initialize owned keyboard settings.");
                initialized=true;
                foreach(string theme in new[]{"Light","Dark"})
                foreach(bool desired in new[]{false,true})
                {
                    Properties.Setings.Default.ThemeMode=theme;
                    Check(ScreenModeSettings.TrySaveOpeningPreference(!desired),"Could not prepare owned keyboard baseline.");
                    byte[] before=File.ReadAllBytes(settingsPath);
                    using(var form=new KeyboardPreview())
                    {
                        form.Show();Application.DoEvents();
                        Find<RadioButton>(form,"openMaximized").Checked=desired;
                        Find<RadioButton>(form,"openCentered").Checked=!desired;
                        Check(!form.ContainsFocus&&!form.ActivatedDuringTest,"Keyboard fixture activated the desktop window.");
                        using(var fileLock=new FileStream(settingsPath,FileMode.Open,FileAccess.Read,FileShare.None))
                        {
                            Check(ProcessDialogKey(form,Keys.Enter),"Enter was not consumed while saving the locked owned file.");
                            Check(ScreenModeSettings.LoadOpeningPreference()==!desired&&!form.IsDisposed&&
                                Find<RadioButton>(form,"openMaximized").Checked==desired&&
                                Find<Label>(form,"accessibilityStatus").Text=="تعذر تأكيد حفظ الإعداد؛ حاول مرة أخرى.",
                                "Keyboard save failure lost the pending choice, changed memory or lacked feedback.");
                        }
                        Check(before.SequenceEqual(File.ReadAllBytes(settingsPath)),"Keyboard failed save modified existing bytes.");
                        bool handled=ProcessDialogKey(form,Keys.Enter);
                        Properties.Setings.Default.Reload();
                        Console.WriteLine("Accessibility Enter: theme="+theme+"; desired="+desired+"; handled="+handled+
                            "; saved="+ScreenModeSettings.LoadOpeningPreference()+"; disposed="+form.IsDisposed);
                        Check(handled&&ScreenModeSettings.LoadOpeningPreference()==desired&&!form.IsDisposed,
                            "Enter did not persist the pending opening preference and keep the settings form open.");
                        Check(Find<Label>(form,"accessibilityStatus").Text=="تم الحفظ؛ سيطبق الاختيار عند فتح الشاشة التالية.","Keyboard save lost success feedback.");
                        Check(!form.ContainsFocus&&!form.ActivatedDuringTest,"Keyboard save activated the test window.");
                    }
                    using(var form=new KeyboardPreview())
                    {
                        form.Show();Application.DoEvents();
                        Find<RadioButton>(form,"openMaximized").Checked=!desired;
                        Find<RadioButton>(form,"openCentered").Checked=desired;
                        before=File.ReadAllBytes(settingsPath);
                        Check(ProcessDialogKey(form,Keys.Escape),"Escape was not consumed by the close action.");
                        Properties.Setings.Default.Reload();
                        Check(form.IsDisposed&&!form.ActivatedDuringTest&&ScreenModeSettings.LoadOpeningPreference()==desired&&before.SequenceEqual(File.ReadAllBytes(settingsPath)),
                            "Escape saved a pending choice or failed to close the hidden settings form.");
                    }
                    cases++;
                }
            }
            finally
            {
                if(initialized)
                {
                    Properties.Setings.Default.ThemeMode=originalTheme;
                    Check(ScreenModeSettings.TrySaveOpeningPreference(original),"Could not restore owned keyboard defaults.");
                    Properties.Setings.Default.Reload();
                    Check(ScreenModeSettings.LoadOpeningPreference()==original&&Properties.Setings.Default.ThemeMode==originalTheme,"Owned keyboard defaults were not restored.");
                }
            }
            Console.WriteLine("PASS accessibility keyboard: "+cases+" theme/mode cases; Enter file-lock failure/retry persists and stays open; Escape closes without saving; non-activating transparent owned preview, no desktop input or SQL. SettingsPath="+settingsPath);
        }

        private static bool ProcessDialogKey(Form form,Keys key) => (bool)typeof(Form).GetMethod("ProcessDialogKey",
            System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(form,new object[]{key});

        internal static void VerifySaveFileLock(string token)
        {
            string settingsPath = RequireFreshOwnedCopy(token);
            bool original = ScreenModeSettings.LoadOpeningPreference();
            string originalTheme = Properties.Setings.Default.ThemeMode;
            bool initialized = false;
            var results = new List<string>();
            try
            {
                // Force a non-default value so the isolated provider creates its own file.
                Check(ScreenModeSettings.TrySaveOpeningPreference(!original), "Could not initialize the owned settings file.");
                initialized = true;
                Check(File.Exists(settingsPath), "The owned settings provider did not create the expected file.");
                foreach (string theme in new[] { "Light", "Dark" })
                foreach (bool desired in new[] { false, true })
                {
                    Properties.Setings.Default.ThemeMode = theme;
                    Check(ScreenModeSettings.TrySaveOpeningPreference(!desired), "Could not prepare the owned save baseline.");
                    byte[] before = File.ReadAllBytes(settingsPath);
                    using (var form = new AccessibilitySettingsForm
                    {
                        ShowInTaskbar = false, Opacity = 0, StartPosition = FormStartPosition.Manual,
                        Location = new Point(-20000, -20000)
                    })
                    {
                        form.Show();
                        Application.DoEvents();
                        var full = Find<RadioButton>(form, "openMaximized");
                        var centered = Find<RadioButton>(form, "openCentered");
                        var save = Find<AntdUI.Button>(form, "saveAccessibility");
                        var status = Find<Label>(form, "accessibilityStatus");
                        full.Checked = desired;
                        centered.Checked = !desired;
                        using (var fileLock = new FileStream(settingsPath, FileMode.Open, FileAccess.Read, FileShare.None))
                        {
                            save.PerformClick();
                            Application.DoEvents();
                            Check(ScreenModeSettings.LoadOpeningPreference() == !desired,
                                "Failed save changed the in-memory opening preference.");
                            Check(status.Text == "تعذر تأكيد حفظ الإعداد؛ حاول مرة أخرى." &&
                                status.AccessibleName == status.Text && status.ForeColor == UiTheme.Danger,
                                "Real settings write failure was not presented as an accessible error.");
                            Check(!form.IsDisposed && form.Visible && save.Enabled && full.Enabled && centered.Enabled &&
                                full.Checked == desired && centered.Checked == !desired,
                                "Failed save closed the dialog, disabled retry or lost the pending choice.");
                        }

                        Check(before.SequenceEqual(File.ReadAllBytes(settingsPath)),
                            "Failed settings save modified the existing file bytes.");
                        Check(ScreenModeSettings.LoadOpeningPreference() == !desired,
                            "Failed settings save changed memory after releasing the file lock.");
                        // Retry exactly as the user would: no Reload, re-open or re-selection.
                        save.PerformClick();
                        Application.DoEvents();
                        Properties.Setings.Default.Reload();
                        Check(ScreenModeSettings.LoadOpeningPreference() == desired,
                            "Save did not recover after releasing the settings file lock.");
                        Check(status.Text == "تم الحفظ؛ سيطبق الاختيار عند فتح الشاشة التالية." &&
                            status.AccessibleName == status.Text && status.ForeColor == UiTheme.Secondary && !form.IsDisposed,
                            "Recovered settings save lost its accessible success feedback.");
                        results.Add(theme + "/" + (desired ? "maximized" : "centered") +
                            ": failure retained file SHA256=" + Hash(before) + "; pending choice retained; retry without reload persisted.");
                        Find<AntdUI.Button>(form, "closeAccessibility").PerformClick();
                        Application.DoEvents();
                    }
                }
            }
            catch (Exception error)
            {
                Console.Error.WriteLine("Owned accessibility save-failure verification failed: " + error);
                throw;
            }
            finally
            {
                if (initialized)
                {
                    Properties.Setings.Default.ThemeMode = originalTheme;
                    Check(ScreenModeSettings.TrySaveOpeningPreference(original), "Could not restore the owned test defaults.");
                    Properties.Setings.Default.Reload();
                    Check(ScreenModeSettings.LoadOpeningPreference() == original && Properties.Setings.Default.ThemeMode == originalTheme,
                        "Owned test defaults were not restored.");
                }
            }
            Console.WriteLine("Owned settings path: " + settingsPath);
            foreach (string result in results) Console.WriteLine("PASS " + result);
            Console.WriteLine("PASS accessibility settings real file-lock failure/retry: four UI cases, retained bytes and memory, no existing user config modified; owned defaults restored; no SQL.");
            File.WriteAllText(Path.Combine(Application.StartupPath, "save-file-lock-evidence.txt"),
                "SettingsPath=" + settingsPath + Environment.NewLine + string.Join(Environment.NewLine, results) +
                Environment.NewLine + "OwnedDefaultsRestored=True; ExistingUserConfigModified=False; SqlWrites=0.");
        }

        private static string RequireFreshOwnedCopy(string token)
        {
            Guid parsed;
            if (!Guid.TryParseExact(token, "N", out parsed))
                throw new UnauthorizedAccessException("A fresh owned-copy token is required.");
            string expected = Path.Combine(@"C:\PharmacyQA\work", "accessibility-save-lock-" + token);
            if (!string.Equals(Path.GetFullPath(Application.StartupPath).TrimEnd(Path.DirectorySeparatorChar),
                expected, StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("Settings file-lock verification requires a dedicated C-drive owned copy.");
            if (!RuntimeSettingsStore.IsBuildConnectionProfileForced || !Database.GetConnectionProfile().IsTestEnvironment)
                throw new UnauthorizedAccessException("The owned copy must retain the pinned QA build profile.");
            string settingsPath = Path.GetFullPath(ConfigurationManager.OpenExeConfiguration(
                ConfigurationUserLevel.PerUserRoamingAndLocal).FilePath);
            string local = Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData))
                .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!settingsPath.StartsWith(local, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(Path.GetPathRoot(settingsPath), @"C:\", StringComparison.OrdinalIgnoreCase) ||
                settingsPath.IndexOf("_Url_", StringComparison.OrdinalIgnoreCase) < 0 ||
                !string.Equals(Path.GetFileName(settingsPath), "user.config", StringComparison.OrdinalIgnoreCase) ||
                File.Exists(settingsPath))
                throw new UnauthorizedAccessException("Verification requires a fresh URL-scoped C-drive user.config; existing files are never locked.");
            return settingsPath;
        }

        private static string Hash(byte[] bytes)
        {
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty);
        }

        private static T Find<T>(Control form, string name) where T : Control =>
            form.Controls.Find(name, true).OfType<T>().Single();

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}

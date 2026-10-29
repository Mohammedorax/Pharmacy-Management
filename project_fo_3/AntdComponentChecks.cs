using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace project_fo_3
{
    internal static class AntdComponentChecks
    {
        internal static void Run(string evidenceDirectory)
        {
            Directory.CreateDirectory(evidenceDirectory);
            string originalMode = Properties.Setings.Default.ThemeMode;
            string originalFont = Properties.Setings.Default.UiFontFamily;
            try
            {
                VerifyCalendarLocalization();
                VerifyLegacyVisualAssetsRemoved();
                Properties.Setings.Default.UiFontFamily = AppFonts.DefaultConfiguredFamily;
                AppFonts.Initialize(AppFonts.DefaultConfiguredFamily);
                foreach (AppIcon icon in Enum.GetValues(typeof(AppIcon)))
                foreach (int size in new[] { 16, 20, 24 })
                {
                    using (var bitmap = AntdUI.SvgExtend.SvgToBmp(OutlinedIcons.Svg(icon), size, size, Color.Black))
                    {
                        Require(bitmap != null && bitmap.Width == size && bitmap.Height == size, "Icon cannot render: " + icon);
                        bool painted = false;
                        for (int x = 0; x < size && !painted; x++)
                        for (int y = 0; y < size; y++) if (bitmap.GetPixel(x, y).A > 0) { painted = true; break; }
                        Require(painted, "Icon rendered empty: " + icon);
                    }
                }
                Require(OutlinedIcons.Svg(AppIcon.Back, true) == OutlinedIcons.Svg(AppIcon.Forward, false), "Directional icon mapping is wrong.");
                Require(OutlinedIcons.Svg(AppIcon.Print, true) == OutlinedIcons.Svg(AppIcon.Print, false), "Non-directional icon was mirrored.");
                foreach (string mode in new[] { "Light", "Dark" })
                {
                    Properties.Setings.Default.ThemeMode = mode;
                    using (var guide = new AntdComponentsForm())
                    {
                        Render(guide, new Size(960, 640), Path.Combine(evidenceDirectory, "components-" + mode + ".png"));
                        Require(AntdUI.Config.IsDark == (mode == "Dark"), "Theme not synchronized.");
                        Require(guide.Controls.Find("antdArabicInput", true).Single() is TextBox, "Arabic editor lost the accepted native bidi fallback.");
                        Require(guide.Controls.Find("antdArabicSelect", true).Single() is ComboBox, "Arabic selector lost the accepted native bidi fallback.");
                        Require(guide.Controls.Find("antdQuantity", true).Single() is AntdUI.InputNumber, "Quantity editor is not the real AntdUI InputNumber.");
            Require(guide.Controls.Find("antdInvoiceTable", true).Single() is AntdUI.Table, "Invoice grid is not the real AntdUI Table.");

                        foreach (string fieldName in new[] { "antdArabicInputFrame", "antdArabicSelectFrame", "antdQuantity", "antdArabicDate" })
                            Require(guide.Controls.Find(fieldName, true).Single().Height >= 44, "Component guide compressed its input: " + fieldName);
                        using (var standalone = DesktopComponents.Input("sizingProbe", "اسم الدواء", "باراسيتامول 500 mg"))
                        {
                            Require(standalone.Height >= 44 && standalone.Width >= 140, "Shared input factory returned an unusable initial size.");
                            standalone.Height = 60;
                            Require(standalone.Radius == 9 && standalone.Text == "باراسيتامول 500 mg", "Input resize lost its proportional radius or text.");
                        }
                        Require(guide.Controls.Find("antdArabicInput", true).Single().Text == "باراسيتامول 500 mg", "Rendering changed stored text.");
                        ComponentMixedTextSelfTests.Verify(guide, evidenceDirectory, mode);
                        var buttons = Descendants(guide.Controls.Find("outlinedGallery", true).Single()).OfType<AntdUI.Button>().ToArray();
                        Require(buttons.Length == Enum.GetValues(typeof(AppIcon)).Length && buttons.All(b => !string.IsNullOrEmpty(b.AccessibleName)), "Icon gallery/accessibility incomplete.");
                    }
                    using (var settings = new SettingsForm(true, true))
                    {
                        var host = (ThemedCardFlowPanel)settings.Controls.Find("antdSettingsCards", true).Single();
                        Require(host.Controls.Find("antdCard_sales-shortcuts", false).Length == 1, "Sales shortcut settings card is missing.");
                        Require(host.Controls.Count == 12 && host.Controls.Find("antdCard_accessibility", false).Length == 1, "Settings must expose twelve cards including accessibility, without QA tools.");
                        foreach (int width in new[] { 360, 760, 960, 1366 })
                        {
                            Render(settings, new Size(width, 640), Path.Combine(evidenceDirectory, "settings-" + mode + "-" + width + ".png"));
                            foreach (Control card in host.Controls)
                                Require(card.Width > 180 && card.Width <= host.ClientSize.Width, "Card overflow at " + width);
                            var cards = host.Controls.Cast<Control>().ToArray();
                            for (int i = 0; i < cards.Length; i++)
                            for (int j = i + 1; j < cards.Length; j++)
                                Require(!cards[i].Bounds.IntersectsWith(cards[j].Bounds), "Cards overlap at " + width);
                        }
                    }
                }
                Console.WriteLine("PASS: legacy bundled artwork removed; native mixed-text input/select within AntdUI shells plus AntdUI InputNumber/Table rendered; owned editing/undo verified; all registered outlined SVGs at 16/20/24, both themes, twelve production settings cards including accessibility at four widths. Keyboard and physical DPI remain separate interactive gates.");
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("Antd component check: " + exception.Message);
                throw;
            }
            finally
            {
                Properties.Setings.Default.ThemeMode = originalMode;
                Properties.Setings.Default.UiFontFamily = originalFont;
                AppFonts.Initialize(originalFont);
            }
        }

        private static void VerifyCalendarLocalization()
        {
            var currentCulture = System.Globalization.CultureInfo.CurrentCulture.Name;
            var currentUiCulture = System.Globalization.CultureInfo.CurrentUICulture.Name;
            var keyboard = InputLanguage.CurrentInputLanguage.Handle;
            var date = new DateTime(2026, 10, 7);
            using (var host = new Form())
            using (var picker = DesktopComponents.DatePicker("calendarLanguageProbe", "تاريخ الاختبار", date))
            {
                host.Controls.Add(picker); host.CreateControl(); picker.CreateControl();
                var type = typeof(AntdUI.DatePicker).Assembly.GetType("AntdUI.LayeredFormDatePicker", true);
                using (var popup = (Form)Activator.CreateInstance(type, new object[] { picker, new Action<DateTime>(value => { }), null, null }))
                {
                    Func<string, object> read = name => type.GetField(name,
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public).GetValue(popup);
                    var culture = (System.Globalization.CultureInfo)read("Culture");
                    Require(culture.Name == (UiLanguageCatalog.IsEnglishActive ? "en-US" : "ar-EG"), "Calendar lost the active UI language.");
                    Require(culture.DateTimeFormat.Calendar is System.Globalization.GregorianCalendar, "Calendar must remain Gregorian.");
                    Require((string)read("YearFormat") == "yyyy" && (string)read("MonthFormat") == "MMMM", "Calendar heading retained a Chinese date format.");
                    foreach (string field in new[] { "MondayButton", "TuesdayButton", "WednesdayButton", "ThursdayButton", "FridayButton", "SaturdayButton", "SundayButton", "button_text", "OKButton" })
                    {
                        string text = (string)read(field);
                        Require(!string.IsNullOrEmpty(text) && !System.Text.RegularExpressions.Regex.IsMatch(text, "[\u4e00-\u9fff]"), "Calendar label is not localized: " + field);
                    }
                    for (int month = 1; month <= 12; month++)
                        Require(new DateTime(2026, month, 7).ToString((string)read("MonthFormat"), culture) == culture.DateTimeFormat.GetMonthName(month), "Calendar month name changed: " + month);
                    Require(picker.Value == date && picker.Text == "2026/10/07", "Localization changed the selected date.");
                }
            }
            Require(System.Globalization.CultureInfo.CurrentCulture.Name == currentCulture &&
                System.Globalization.CultureInfo.CurrentUICulture.Name == currentUiCulture && InputLanguage.CurrentInputLanguage.Handle == keyboard,
                "Calendar localization changed thread culture or keyboard layout.");
            Console.WriteLine("PASS: calendar months/weekdays/actions localized; Gregorian date, cultures and keyboard unchanged.");
        }
        private static void VerifyLegacyVisualAssetsRemoved()
        {
            string sourceDirectory = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", ".."));
            string repositoryDirectory = Path.GetFullPath(Path.Combine(sourceDirectory, ".."));
            string[] forbiddenFiles =
            {
                "billing-icon.png", "companies-icon.png", "dashboard-icon.png", "employees-icon.png",
                "icon48-1.png", "icon96-1.png", "icons8-nurse-100.png", "icons8-nurse-80.png",
                "medicine-icon.png", "purchases-icon.png", "purchases-icon.svg", "settings-icon.png",
                "shopping-cart.png", "X Neon Purple 💜.png"
            };

            foreach (string directory in new[] { Path.Combine(sourceDirectory, "Resources"), Path.Combine(repositoryDirectory, "images") })
            foreach (string fileName in forbiddenFiles)
                Require(!File.Exists(Path.Combine(directory, fileName)), "Legacy visual asset returned: " + fileName);

            foreach (string resx in Directory.GetFiles(sourceDirectory, "*.resx", SearchOption.TopDirectoryOnly))
            {
                string content = File.ReadAllText(resx);
                Require(!content.Contains("name=\"Icon1\"") &&
                        !content.Contains("pictureBox1.Image") &&
                        !content.Contains("pictureBox5.Image"),
                        "Legacy embedded artwork returned in " + Path.GetFileName(resx));
            }
        }

        private static void Render(Form form, Size size, string path)
        {
            AntdComponentViewportSelfTests.Render(form, size, path);
        }
        private static IEnumerable<Control> Descendants(Control parent)
        {
            foreach (Control child in parent.Controls)
            {
                yield return child;
                foreach (Control nested in Descendants(child)) yield return nested;
            }
        }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}


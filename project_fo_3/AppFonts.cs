using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace project_fo_3
{
    internal static class AppFonts
    {
        public const string OfficialFamilyName = "IBM Plex Sans Arabic";
        public const string DefaultConfiguredFamily = "IBM Plex Sans Arabic";

        private static readonly string[] LegacyIbmFontFiles =
        {
            "IBMPlexSansArabic-Regular.ttf",
            "IBMPlexSansArabic-Medium.ttf",
            "IBMPlexSansArabic-SemiBold.ttf",
            "IBMPlexSansArabic-Bold.ttf",
            "IBMPlexSansArabic-Light.ttf",
            "IBMPlexSansArabic-ExtraLight.ttf",
            "IBMPlexSansArabic-Thin.ttf"
        };

        private static readonly object SyncRoot = new object();
        private static readonly StringComparer FamilyNameComparer = StringComparer.OrdinalIgnoreCase;

        private static PrivateFontCollection privateFonts;
        private static InstalledFontCollection installedFonts;
        private static FontFamily activeFontFamily;
        private static bool initialized;
        private static string configuredFontName;

        public static void Initialize()
        {
            Initialize(UiTheme.LoadFontFamily());
        }

        public static void Initialize(string fontFamily)
        {
            string normalizedRequested = string.IsNullOrWhiteSpace(fontFamily)
                ? DefaultConfiguredFamily
                : fontFamily.Trim();

            lock (SyncRoot)
            {
                if (initialized && FamilyNameComparer.Equals(configuredFontName, normalizedRequested)) return;
                configuredFontName = normalizedRequested;
                initialized = false;
                configuredFontName = configuredFontName ?? UiTheme.LoadFontFamily();
                LoadFamilies();
            }
        }

        public static Font Create(float size)
        {
            return Create(size, FontStyle.Regular, GraphicsUnit.Point);
        }

        public static Font Create(float size, FontStyle style)
        {
            return Create(size, style, GraphicsUnit.Point);
        }

        public static Font Create(float size, FontStyle style, GraphicsUnit unit)
        {
            EnsureLoaded();

            FontFamily family = activeFontFamily ?? FontFamily.GenericSansSerif;
            FontStyle safeStyle = ResolveAvailableStyle(family, style);

            try
            {
                return new Font(family, size, safeStyle, unit);
            }
            catch
            {
                try
                {
                    return new Font(OfficialFamilyName, size, safeStyle, unit);
                }
                catch
                {
                    return new Font(FontFamily.GenericSansSerif, size, safeStyle, unit);
                }
            }
        }

        public static Font CreateLike(Font source, float minimumSize)
        {
            if (source == null)
            {
                return Create(Math.Max(9F, minimumSize), FontStyle.Regular);
            }

            return Create(Math.Max(minimumSize, source.Size), source.Style, source.Unit);
        }

        public static bool IsIconFont(Font font)
        {
            if (font == null || font.FontFamily == null)
            {
                return false;
            }

            string name = font.FontFamily.Name ?? string.Empty;
            return name.IndexOf("Segoe MDL2 Assets", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Marlett", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Webdings", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Wingdings", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static void ApplyTo(Control control, float minimumSize)
        {
            if (control == null)
            {
                return;
            }

            if (!IsIconFont(control.Font))
            {
                control.Font = CreateLike(control.Font, minimumSize);
            }

            DataGridView grid = control as DataGridView;
            if (grid != null)
            {
                ApplyToGrid(grid);
            }

            foreach (Control child in control.Controls)
            {
                ApplyTo(child, minimumSize);
            }
        }

        public static void ApplyToGrid(DataGridView grid)
        {
            if (grid == null)
            {
                return;
            }

            if (!IsIconFont(grid.Font))
            {
                grid.Font = CreateLike(grid.Font, 9F);
            }

            ApplyToCellStyle(grid.ColumnHeadersDefaultCellStyle, 9.5F);
            ApplyToCellStyle(grid.RowHeadersDefaultCellStyle, 9F);
            ApplyToCellStyle(grid.DefaultCellStyle, 9F);
            ApplyToCellStyle(grid.AlternatingRowsDefaultCellStyle, 9F);
            ApplyToCellStyle(grid.RowsDefaultCellStyle, 9F);
        }

        private static void ApplyToCellStyle(DataGridViewCellStyle style, float minimumSize)
        {
            if (style == null || IsIconFont(style.Font))
            {
                return;
            }

            style.Font = CreateLike(style.Font, minimumSize);
        }

        private static void EnsureLoaded()
        {
            if (initialized)
            {
                return;
            }

            lock (SyncRoot)
            {
                if (initialized)
                {
                    return;
                }

                configuredFontName = configuredFontName ?? UiTheme.LoadFontFamily();
                LoadFamilies();
            }
        }

        private static void LoadFamilies()
        {
            // Retain collections while controls still use fonts created from them.
            if (privateFonts == null)
            {
                privateFonts = new PrivateFontCollection();
                foreach (string fontFile in ResolveFontFiles())
                {
                    try { privateFonts.AddFontFile(fontFile); }
                    catch { /* Optional font is invalid; continue to fallback. */ }
                }
            }
            if (installedFonts == null)
            {
                try { installedFonts = new InstalledFontCollection(); }
                catch { /* System fallback remains available. */ }
            }
            FontFamily[] installed = installedFonts != null ? installedFonts.Families : null;
            activeFontFamily = FindFamily(privateFonts.Families, configuredFontName)
                ?? FindFamily(installed, configuredFontName)
                ?? FindFamily(privateFonts.Families, OfficialFamilyName)
                ?? FindFamily(installed, OfficialFamilyName)
                ?? FontFamily.GenericSansSerif;
            initialized = true;
        }
        private static FontFamily FindFamily(FontFamily[] families, string familyName)
        {
            if (families == null || string.IsNullOrWhiteSpace(familyName))
            {
                return null;
            }

            return families.FirstOrDefault(family => FamilyNameComparer.Equals(family.Name, familyName.Trim()));
        }

        private static IEnumerable<string> ResolveFontFiles()
        {
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string startupPath = Application.StartupPath;
            string currentDirectory = Environment.CurrentDirectory;

            string[] roots =
            {
                Path.Combine(baseDirectory, "Resources", "Fonts"),
                Path.Combine(startupPath, "Resources", "Fonts"),
                Path.Combine(currentDirectory, "Resources", "Fonts")
            };

            HashSet<string> files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string root in roots.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!Directory.Exists(root))
                {
                    continue;
                }

                foreach (string legacyFont in LegacyIbmFontFiles)
                {
                    string legacyPath = Path.Combine(root, legacyFont);
                    if (File.Exists(legacyPath))
                    {
                        files.Add(legacyPath);
                    }
                }

                foreach (string fontPath in GetOptionalFontFiles(root, "*.ttf"))
                {
                    files.Add(fontPath);
                }

                foreach (string fontPath in GetOptionalFontFiles(root, "*.otf"))
                {
                    files.Add(fontPath);
                }
            }

            return files;
        }

        private static string[] GetOptionalFontFiles(string root, string pattern)
        {
            try { return Directory.GetFiles(root, pattern); }
            catch (IOException) { return new string[0]; }
            catch (UnauthorizedAccessException) { return new string[0]; }
        }
        private static FontStyle ResolveAvailableStyle(FontFamily family, FontStyle requestedStyle)
        {
            if (family == null)
            {
                return requestedStyle;
            }

            FontStyle decorationStyle = requestedStyle & (FontStyle.Underline | FontStyle.Strikeout);
            bool wantsBold = (requestedStyle & FontStyle.Bold) == FontStyle.Bold;
            bool wantsItalic = (requestedStyle & FontStyle.Italic) == FontStyle.Italic;

            List<FontStyle> baseCandidates = new List<FontStyle>();

            if (wantsBold && wantsItalic)
            {
                baseCandidates.Add(FontStyle.Bold | FontStyle.Italic);
            }

            if (wantsBold)
            {
                baseCandidates.Add(FontStyle.Bold);
            }

            if (wantsItalic)
            {
                baseCandidates.Add(FontStyle.Italic);
            }

            baseCandidates.Add(FontStyle.Regular);

            foreach (FontStyle baseStyle in baseCandidates.Distinct())
            {
                try
                {
                    if (family.IsStyleAvailable(baseStyle))
                    {
                        return baseStyle | decorationStyle;
                    }
                }
                catch
                {
                    return FontStyle.Regular | decorationStyle;
                }
            }

            return FontStyle.Regular | decorationStyle;
        }
    }
}

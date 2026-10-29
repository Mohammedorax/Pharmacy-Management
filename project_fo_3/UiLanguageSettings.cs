// إعداد لغة الواجهة: العربية هي اللغة الافتراضية ولا يُفعّل خيار لم تكتمل ترجمته.
using System;

namespace project_fo_3
{
    internal static class UiLanguageSettings
    {
        internal const string Arabic = "Arabic";
        internal const string English = "English";

        internal static string Load()
        {
            try
            {
                string value = Properties.Setings.Default.UiLanguage;
                return string.Equals(value, English, StringComparison.OrdinalIgnoreCase) ? English : Arabic;
            }
            catch
            {
                return Arabic;
            }
        }

        // لا تُحفظ لغة غير مكتملة حتى لا تتحول الواجهة إلى مزيج مربك من العربية والإنجليزية.
        internal static bool TrySave(string language)
        {
            if (!string.Equals(language, Arabic, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            try
            {
                var settings = Properties.Setings.Default;
                string previous = settings.UiLanguage;
                try
                {
                    settings.UiLanguage = Arabic;
                    settings.Save();
                    return true;
                }
                catch (Exception error)
                {
                    try { settings.UiLanguage = previous; } catch { }
                    AppDiagnostics.Log("UiLanguageSettingsSave", error);
                    return false;
                }
            }
            catch
            {
                return false;
            }
        }

        internal static bool IsEnglishAvailable
        {
            get { return false; }
        }
    }
}

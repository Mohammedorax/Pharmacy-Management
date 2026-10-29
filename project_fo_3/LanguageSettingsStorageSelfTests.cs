using System;
using System.Configuration;
using System.IO;
using System.Linq;

namespace project_fo_3
{
    internal static class LanguageSettingsStorageSelfTests
    {
        internal static void VerifyFileLock(string token)
        {
            string path=RequireFreshOwnedCopy(token);
            var settings=Properties.Setings.Default;
            string original=settings.UiLanguage;bool initialized=false;int failures=0,cases=0;
            try
            {
                foreach(string previous in new[]{UiLanguageSettings.English,"eNgLiSh","Legacy-Language",UiLanguageSettings.Arabic})
                {
                    settings.UiLanguage=previous;settings.Save();initialized=true;
                    Check(File.Exists(path),"Owned language provider did not create the expected file.");
                    byte[] before=File.ReadAllBytes(path);bool saved,preserved;
                    using(var locked=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.None))
                    {
                        saved=UiLanguageSettings.TrySave(UiLanguageSettings.Arabic);
                        preserved=settings.UiLanguage==previous;
                    }
                    bool bytesPreserved=before.SequenceEqual(File.ReadAllBytes(path));
                    Console.WriteLine("Language save failure: previous="+previous+"; saved="+saved+"; memoryPreserved="+preserved+"; bytesPreserved="+bytesPreserved);
                    if(saved||!preserved||!bytesPreserved)failures++;
                    Check(UiLanguageSettings.TrySave(UiLanguageSettings.Arabic),"Language retry after owned lock release failed.");
                    settings.Reload();Check(settings.UiLanguage==UiLanguageSettings.Arabic,"Language retry did not persist Arabic.");
                    byte[] accepted=File.ReadAllBytes(path);
                    Check(!UiLanguageSettings.TrySave(UiLanguageSettings.English)&&!UiLanguageSettings.TrySave("Unknown")&&
                        !UiLanguageSettings.IsEnglishAvailable&&settings.UiLanguage==UiLanguageSettings.Arabic&&accepted.SequenceEqual(File.ReadAllBytes(path)),
                        "Unavailable language policy changed or modified settings.");
                    cases++;
                }
            }
            finally
            {
                if(initialized){settings.UiLanguage=original;settings.Save();settings.Reload();Check(settings.UiLanguage==original,"Owned language default was not restored.");}
            }
            if(failures>0)throw new InvalidOperationException("Language save failure changed memory or disk in "+failures+" of "+cases+" cases.");
            Console.WriteLine("PASS language settings file-lock failure: "+cases+" raw prior values; memory and bytes preserved, retry persisted, unavailable languages refused, owned default restored; no UI/SQL/existing user config modified. SettingsPath="+path);
        }
        private static string RequireFreshOwnedCopy(string token)
        {
            Guid parsed;if(!Guid.TryParseExact(token,"N",out parsed))throw new UnauthorizedAccessException("Owned language token required.");
            string expected=Path.Combine(@"C:\PharmacyQA\work","language-settings-save-lock-"+token);
            if(!string.Equals(Path.GetFullPath(System.Windows.Forms.Application.StartupPath).TrimEnd(Path.DirectorySeparatorChar),expected,StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("Language lock verifier requires a fresh owned C copy.");
            if(!RuntimeSettingsStore.IsBuildConnectionProfileForced||!Database.GetConnectionProfile().IsTestEnvironment)
                throw new UnauthorizedAccessException("Language lock verifier requires the pinned QA profile.");
            string path=Path.GetFullPath(ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal).FilePath);
            string local=Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
            if(!path.StartsWith(local,StringComparison.OrdinalIgnoreCase)||!string.Equals(Path.GetPathRoot(path),@"C:\",StringComparison.OrdinalIgnoreCase)||
                path.IndexOf("_Url_",StringComparison.OrdinalIgnoreCase)<0||!string.Equals(Path.GetFileName(path),"user.config",StringComparison.OrdinalIgnoreCase)||File.Exists(path))
                throw new UnauthorizedAccessException("Language verification refuses an existing or unowned user.config.");
            return path;
        }
        private static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    }
}

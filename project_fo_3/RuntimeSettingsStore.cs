using System;
using System.Configuration;
using System.Data.SqlClient;
using System.IO;
using System.Xml.Linq;

namespace project_fo_3
{
    // إعدادات التشغيل لا تحفظ بجوار الملف التنفيذي لأن MSBuild أو التحديث قد يستبدلهما.
    // لا يحتوي الملف على كلمة مرور؛ التطبيق يدعم اتصال Windows فقط من شاشة الإعدادات الحالية.
    internal sealed class DatabaseConnectionProfile
    {
        public string ServerName { get; private set; }
        public string DatabaseName { get; private set; }
        public bool IntegratedSecurity { get; private set; }

        public bool IsTestEnvironment
        {
            get
            {
                string name = DatabaseName ?? string.Empty;
                return name.EndsWith("_qa", StringComparison.OrdinalIgnoreCase)
                    || name.EndsWith("_test", StringComparison.OrdinalIgnoreCase)
                    || name.EndsWith("_sandbox", StringComparison.OrdinalIgnoreCase);
            }
        }

        public string EnvironmentNameAr { get { return IsTestEnvironment ? "اختبار" : "إنتاج"; } }

        public DatabaseConnectionProfile(string serverName, string databaseName, bool integratedSecurity)
        {
            ServerName = (serverName ?? string.Empty).Trim();
            DatabaseName = (databaseName ?? string.Empty).Trim();
            IntegratedSecurity = integratedSecurity;
        }
    }

    internal static class RuntimeSettingsStore
    {
        private const string FolderName = "PharmacyManagement";
        private const string FileName = "runtime-settings.xml";
        private const string BackupFileName = "runtime-settings.previous.xml";
        private const string ForceBuildConnectionProfileSetting = "ForceBuildConnectionProfile";

        // حزمة QA قد تعمل على نفس جهاز الإنتاج. في هذه الحالة لا يجوز لملف ProgramData
        // المشترك أن يعلو على اتصال QA المضمّن بالحزمة، وإلا تحولت تجربة الاختبار إلى
        // قاعدة التشغيل الفعلية من دون قصد.
        internal static bool IsBuildConnectionProfileForced
        {
            get
            {
                return string.Equals(
                    ConfigurationManager.AppSettings[ForceBuildConnectionProfileSetting],
                    "true",
                    StringComparison.OrdinalIgnoreCase);
            }
        }

        internal static string SettingsPath
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), FolderName, FileName); }
        }

        private static string BackupSettingsPath
        {
            get { return Path.Combine(Path.GetDirectoryName(SettingsPath), BackupFileName); }
        }

        // يميز الإعداد المحفوظ صراحةً عن الإعداد الافتراضي المضمّن مع البناء.
        // وجود هذا الفرق في الواجهة يمنع تشغيل قاعدة مختلفة دون أن يلاحظ المستخدم ذلك.
        internal static bool HasSavedProfile
        {
            get
            {
                if (IsBuildConnectionProfileForced) return false;
                DatabaseConnectionProfile profile;
                return TryLoadProfile(SettingsPath, out profile) || TryLoadProfile(BackupSettingsPath, out profile);
            }
        }

        // يمنع الدخول الصامت إلى قاعدة افتراضية عندما تتلف الإعدادات المحفوظة ونسختها
        // الاحتياطية معاً. تبقى شاشة F1 قادرة على فتح الإعداد الافتراضي لإصلاحها.
        internal static bool HasUnreadableSavedProfile
        {
            get
            {
                if (IsBuildConnectionProfileForced) return false;
                DatabaseConnectionProfile profile;
                return File.Exists(SettingsPath)
                    && !TryLoadProfile(SettingsPath, out profile)
                    && !TryLoadProfile(BackupSettingsPath, out profile);
            }
        }

        internal static string ProfileSourceText
        {
            get
            {
                if (IsBuildConnectionProfileForced) return "حزمة QA ثابتة";
                DatabaseConnectionProfile profile;
                if (TryLoadProfile(SettingsPath, out profile)) return "إعداد محفوظ";
                if (TryLoadProfile(BackupSettingsPath, out profile)) return "نسخة احتياطية مستعادة";
                return "إعداد افتراضي";
            }
        }

        internal static DatabaseConnectionProfile LoadOrDefault(DatabaseConnectionProfile fallback)
        {
            if (fallback == null) throw new ArgumentNullException(nameof(fallback));

            if (IsBuildConnectionProfileForced)
            {
                return fallback;
            }

            DatabaseConnectionProfile profile;
            if (TryLoadProfile(SettingsPath, out profile)) return profile;
            if (TryLoadProfile(BackupSettingsPath, out profile))
            {
                AppDiagnostics.Log("RuntimeSettingsRecovery", new InvalidOperationException("Using the previous valid runtime database profile."));
                return profile;
            }

            return fallback;
        }

        // لا نعدّ الملف إعداداً محفوظاً إلا إذا كان قابلاً للقراءة ومكتملاً؛ وجود ملف
        // تالف لا يجوز أن يجعل الواجهة توحي للمستخدم بأن التطبيق يعمل على اتصاله المختار.
        private static bool TryLoadProfile(string path, out DatabaseConnectionProfile profile)
        {
            profile = null;
            try
            {
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return false;
                XDocument document = XDocument.Load(path, LoadOptions.None);
                XElement root = document.Root;
                if (root == null || root.Name != "PharmacyRuntimeSettings") return false;

                string server = (string)root.Element("ServerName");
                string database = (string)root.Element("DatabaseName");
                bool integrated = !string.Equals((string)root.Element("IntegratedSecurity"), "false", StringComparison.OrdinalIgnoreCase);
                if (string.IsNullOrWhiteSpace(server) || string.IsNullOrWhiteSpace(database)) return false;
                profile = new DatabaseConnectionProfile(server, database, integrated);
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is System.Xml.XmlException)
            {
                AppDiagnostics.Log("RuntimeSettingsRead", ex);
                return false;
            }
        }

        internal static void EnsureConnectionProfileCanBeChanged()
        {
            if (IsBuildConnectionProfileForced)
                throw new InvalidOperationException("حزمة QA الثابتة لا تسمح بتغيير قاعدة التشغيل. استخدم حزمة التشغيل العادية لتعديل الإعدادات.");
        }

        internal static void Save(DatabaseConnectionProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            EnsureConnectionProfileCanBeChanged();
            if (!LocalAdministratorAccess.IsCurrentUserAdministrator())
            {
                throw new UnauthorizedAccessException("تعديل إعدادات التشغيل مسموح فقط لمسؤول Windows المحلي.");
            }

            PublishProfileAtPath(profile,SettingsPath);
        }

        // Common atomic publisher. Production Save still enforces Windows administrator access
        // and the fixed-QA restriction; verification calls this primitive only on owned paths.
        internal static void PublishProfileAtPath(DatabaseConnectionProfile profile,string targetPath)
        {
            if(profile==null||string.IsNullOrWhiteSpace(targetPath))throw new ArgumentNullException();
            using(SessionCommitGate.ChangeSession())
            {
            string folder = Path.GetDirectoryName(targetPath);
            Directory.CreateDirectory(folder);

            string temporaryPath = targetPath + ".tmp";
            string backupPath = Path.Combine(folder,BackupFileName);
            XDocument document = new XDocument(
                new XElement("PharmacyRuntimeSettings",
                    new XAttribute("version", "1"),
                    new XElement("ServerName", profile.ServerName),
                    new XElement("DatabaseName", profile.DatabaseName),
                    new XElement("IntegratedSecurity", profile.IntegratedSecurity ? "true" : "false")));

            try
            {
                document.Save(temporaryPath);
                if (File.Exists(targetPath))
                {
                    // File.Replace يحفظ نسخة سابقة ويستبدل الملف في عملية واحدة على نفس القرص.
                    File.Replace(temporaryPath, targetPath, backupPath, true);
                }
                else
                {
                    File.Move(temporaryPath, targetPath);
                }
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
                SessionContext.Clear();
            }
        }
        internal static DatabaseConnectionProfile FromConnectionString(string connectionString)
        {
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(connectionString);
            return new DatabaseConnectionProfile(builder.DataSource, builder.InitialCatalog, builder.IntegratedSecurity);
        }
    }
}

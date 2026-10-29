using System;
using System.Data;

namespace project_fo_3
{
    // بوابة قراءة ومسار تدقيق للصفحة الرئيسية؛ الواجهة لا تتصل بقاعدة البيانات مباشرة.
    internal static class HomeWorkspaceService
    {
        internal static string GetCurrentDatabaseName()
        {
            DatabaseConnectionProfile profile = Database.GetConnectionProfile();
            return profile.DatabaseName;
        }

        internal static DatabaseStartupState GetStartupState()
        {
            return Database.GetStartupState();
        }

        internal static void RecordSecurityAudit(string action, string module, string details)
        {
            Database.WriteAudit(action, module, details);
        }
    }
}
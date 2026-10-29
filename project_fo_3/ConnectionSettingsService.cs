using System.Data;
using System.Data.SqlClient;
using System.Linq;

namespace project_fo_3
{
    // بوابة إعداد الاتصال: الشاشات تعرض النتائج، وهذه الخدمة تحفظ سلوك Database الحالي.
    internal static class ConnectionSettingsService
    {
        internal static bool IsConnectionProfileFixed { get { return RuntimeSettingsStore.IsBuildConnectionProfileForced; } }
        internal static SqlConnectionStringBuilder LoadSettings() { return Database.LoadSetings(); }
        internal static string[] GetSuggestedServers(bool includeDiscovery) { return Database.GetSuggestedServers(includeDiscovery); }
        internal static DataTable GetDatabases(string serverName, bool integratedSecurity) { return Database.GetDatabases(serverName, integratedSecurity); }
        internal static void TestSettings(string serverName, string databaseName, bool integratedSecurity) { Database.TestSettings(serverName, databaseName, integratedSecurity); }
        internal static DatabaseConnectionProfile GetConnectionProfile() { return Database.GetConnectionProfile(); }
        internal static void SaveSettings(string serverName, string databaseName, bool integratedSecurity) { Database.SaveSettings(serverName, databaseName, integratedSecurity); }
        internal static DatabaseInspectionResult InspectSettings(string serverName, string databaseName, bool integratedSecurity) { return Database.InspectSettings(serverName, databaseName, integratedSecurity); }
        internal static void PrepareSettings(string serverName, string databaseName, bool integratedSecurity) { Database.PrepareSettings(serverName, databaseName, integratedSecurity); }
        internal static bool HasManager() { return Database.HasManager(); }
        internal static string BuildConnectionHelp(System.Exception exception) { return Database.BuildConnectionHelp(exception); }
        internal static string BuildConnectionHelp(System.Exception exception, bool english)
        {
            string help = BuildConnectionHelp(exception);
            return english ? string.Join("\n", help.Split('\n').Select(UiLanguageCatalog.Translate)) : help;
        }
    }
}

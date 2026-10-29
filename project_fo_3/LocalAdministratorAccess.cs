using System.Security.Principal;

namespace project_fo_3
{
    // يجمع فحص صلاحية Windows المحلي لحماية مسارات تهيئة التطبيق الحساسة.
    internal static class LocalAdministratorAccess
    {
        public static bool IsCurrentUserAdministrator()
        {
            try
            {
                using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
                {
                    WindowsPrincipal principal = new WindowsPrincipal(identity);
                    return principal.IsInRole(WindowsBuiltInRole.Administrator);
                }
            }
            catch
            {
                return false;
            }
        }
    }
}

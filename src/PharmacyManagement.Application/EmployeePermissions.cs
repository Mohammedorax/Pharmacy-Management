using System;
using System.Collections.Generic;

namespace PharmacyManagement.Application
{
    // Screen grants share the existing employee override store. Sensitive administration
    // screens are deliberately absent because opening them permits role or connection changes.
    public static class EmployeeScreenPermissionKeys
    {
        private static readonly HashSet<string> DelegableNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "ViewDashboard", "Sales", "Purchases", "ManageMedicines",
            "ManageManufacturers", "Inventory", "Accounting", "SystemMonitor"
        };

        public static string For(string screenName)
        {
            if (!DelegableNames.Contains(screenName ?? string.Empty))
                throw new InvalidOperationException("الشاشة لا تقبل تفويض صلاحية فتحها.");
            return "Screen." + screenName;
        }

        public static bool IsDelegableKey(string key)
        {
            return !string.IsNullOrEmpty(key) && key.StartsWith("Screen.", StringComparison.Ordinal) &&
                DelegableNames.Contains(key.Substring("Screen.".Length));
        }
    }

    public sealed class EmployeePermissionSnapshot
    {
        public bool IsAvailable { get; set; }
        public string Role { get; set; }
        public IReadOnlyDictionary<string, bool> Overrides { get; set; }
    }

    public interface IEmployeePermissionRepository
    {
        EmployeePermissionSnapshot GetByEmployeeId(string employeeId);
        EmployeePermissionSnapshot GetByUserName(string userName);
        void Replace(string employeeId, IReadOnlyDictionary<string, bool> overrides, OperationActor actor);
    }

    public interface IAsyncEmployeePermissionRepository
    {
        System.Threading.Tasks.Task<EmployeePermissionSnapshot> GetByEmployeeIdAsync(string employeeId, System.Threading.CancellationToken cancellation);
        System.Threading.Tasks.Task<EmployeePermissionSnapshot> GetByUserNameAsync(string userName, System.Threading.CancellationToken cancellation);
    }

    public interface IEmployeePermissionService
    {
        EmployeePermissionSnapshot GetByEmployeeId(string employeeId);
        EmployeePermissionSnapshot GetByUserName(string userName);
        void Replace(string employeeId, IDictionary<string, bool> overrides, OperationActor actor);
    }

    public sealed class EmployeePermissionService : IEmployeePermissionService
    {
        private readonly IEmployeePermissionRepository repository;
        private readonly Func<OperationActor, bool> canManage;

        public EmployeePermissionService(IEmployeePermissionRepository repository, Func<OperationActor, bool> canManage)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
            this.canManage = canManage ?? throw new ArgumentNullException(nameof(canManage));
        }

        public EmployeePermissionSnapshot GetByEmployeeId(string employeeId) =>
            repository.GetByEmployeeId(Required(employeeId, 50, "رقم الموظف"));

        public EmployeePermissionSnapshot GetByUserName(string userName) =>
            repository.GetByUserName(Required(userName, 200, "اسم المستخدم"));

        public void Replace(string employeeId, IDictionary<string, bool> overrides, OperationActor actor)
        {
            OperationActor manager = RequiredManager(actor);
            var clean = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            if (overrides != null)
                foreach (KeyValuePair<string, bool> item in overrides)
                {
                    string operation = Required(item.Key, 100, "اسم العملية");
                    if (operation.StartsWith("Screen.", StringComparison.OrdinalIgnoreCase) &&
                        !EmployeeScreenPermissionKeys.IsDelegableKey(operation))
                        throw new InvalidOperationException("الشاشة لا تقبل تفويض صلاحية فتحها.");
                    clean[operation] = item.Value;
                }
            repository.Replace(Required(employeeId, 50, "رقم الموظف"), clean, manager);
        }

        private OperationActor RequiredManager(OperationActor actor)
        {
            return EmployeeAdministrationAuthorization.RequireManager(actor, canManage,
                "لا تملك صلاحية إدارة الصلاحيات التفصيلية.");
        }

        private static string Required(string value, int maximum, string field)
        {
            string normalized = (value ?? string.Empty).Trim();
            if (normalized.Length == 0) throw new InvalidOperationException(field + " مطلوب.");
            if (normalized.Length > maximum) throw new InvalidOperationException(field + " يتجاوز الحد المسموح.");
            return normalized;
        }
    }
}


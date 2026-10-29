using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using PharmacyManagement.Application;

namespace project_fo_3
{
    internal enum AppPermission
    {
        ViewDashboard,
        Sales,
        Purchases,
        ManageMedicines,
        ManageManufacturers,
        ManageEmployees,
        Inventory,
        Accounting,
        Settings,
        SystemMonitor
    }

    internal enum AppOperation
    {
        ManageChartOfAccounts,
        PostAccountingEntry,
        PostCashVoucher,
        ViewAccountingReports,
        PostSalesInvoice,
        IssueCommercialSalesReturn,
        ResolveCommercialReturnQuarantine,
        ManageAccountingPeriods,
        ManageActiveIngredients,
        CancelSalesInvoice,
        PostPurchaseInvoice,
        PostInventoryOperation,
        ManageSalesCustomers,
        CancelPurchaseInvoice
    }

    internal static class PermissionService
    {
        private static readonly HashSet<string> ManagerRoles = CreateRoleSet(
            "Manager", "Admin", "Administrator", "مدير", "مسؤول", "ادمن");

        private static readonly HashSet<string> AccountantRoles = CreateRoleSet(
            "Accountant", "Accounting", "محاسب", "محاسبة");

        private static readonly HashSet<string> InventoryRoles = CreateRoleSet(
            "Inventory", "Stock", "Warehouse", "مخزون", "مستودع");

        private static readonly HashSet<string> SalesRoles = CreateRoleSet(
            "Cashier", "Pharmacist", "Sales", "كاشير", "صيدلي", "مبيعات");

        private static readonly object OverridesSync = new object();
        private const uint OverrideCacheLifetimeMilliseconds = 5000;
        private static int cachedOverrideTick;
        private static string cachedOverrideUser = string.Empty;
        private static string cachedOverrideConnection = string.Empty;
        private static long cachedOverrideRevision = -1;
        private static Dictionary<string, bool> cachedOverrides = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        public static bool CanOpen(AppPermission permission)
        {
            if (!SessionContext.IsAuthenticated)
            {
                return false;
            }

            string role = NormalizeRole(SessionContext.CurrentRole);

            if (ManagerRoles.Contains(role))
            {
                return true;
            }

            if (permission == AppPermission.ManageEmployees || permission == AppPermission.Settings)
                return false;

            string screenKey = "Screen." + permission;
            if (!EmployeeScreenPermissionKeys.IsDelegableKey(screenKey)) return false;
            bool overrideValue;
            bool? selected = TryGetCurrentUserOverride(screenKey, out overrideValue) ? overrideValue : (bool?)null;
            return ResolveScreenAccess(role, permission, selected);
        }

        internal static bool ResolveScreenAccess(string roleName, AppPermission permission, bool? selected)
        {
            string role = NormalizeRole(roleName);
            if (ManagerRoles.Contains(role)) return true;
            if (permission == AppPermission.ManageEmployees || permission == AppPermission.Settings) return false;
            return selected ?? GetRoleDefault(role, permission);
        }

        internal static bool GetRoleDefault(string roleName, AppPermission permission)
        {
            string role = NormalizeRole(roleName);
            if (ManagerRoles.Contains(role)) return true;
            switch (permission)
            {
                case AppPermission.ViewDashboard:
                    return true;
                case AppPermission.Sales:
                    return SalesRoles.Contains(role);
                case AppPermission.Purchases:
                case AppPermission.ManageMedicines:
                case AppPermission.ManageManufacturers:
                    return InventoryRoles.Contains(role) || SalesRoles.Contains(role);
                case AppPermission.Inventory:
                    return InventoryRoles.Contains(role);
                case AppPermission.Accounting:
                    return AccountantRoles.Contains(role);
                case AppPermission.Settings:
                case AppPermission.ManageEmployees:
                case AppPermission.SystemMonitor:
                    return false;
                default:
                    return false;
            }
        }

        public static string GetArabicName(AppPermission permission)
        {
            switch (permission)
            {
                case AppPermission.ViewDashboard:
                    return "لوحة التحكم";
                case AppPermission.Sales:
                    return "المبيعات";
                case AppPermission.Purchases:
                    return "المشتريات";
                case AppPermission.ManageMedicines:
                    return "الأدوية";
                case AppPermission.ManageManufacturers:
                    return "العملاء والموردون";
                case AppPermission.ManageEmployees:
                    return "الموظفون";
                case AppPermission.Inventory:
                    return "المخزون";
                case AppPermission.Accounting:
                    return "المحاسبة";
                case AppPermission.Settings:
                    return "الإعدادات";
                case AppPermission.SystemMonitor:
                    return "مراقبة النظام";
                default:
                    return "القسم";
            }
        }

        public static bool CanExecute(AppOperation operation)
        {
            if (!SessionContext.IsAuthenticated)
            {
                return false;
            }

            string role = NormalizeRole(SessionContext.CurrentRole);
            if (ManagerRoles.Contains(role))
            {
                return true;
            }

            bool overrideValue;
            if (TryGetCurrentUserOverride(operation.ToString(), out overrideValue))
            {
                return overrideValue;
            }

            return CanExecuteByRole(role, operation);
        }

        internal static bool CanManageDetailedPermissions()
        {
            return SessionContext.IsAuthenticated && ManagerRoles.Contains(NormalizeRole(SessionContext.CurrentRole));
        }

        internal static event EventHandler CachedOverridesCleared;
        internal static void ClearCachedOverrides()
        {
            lock (OverridesSync)
            {
                cachedOverrideUser = string.Empty;
                cachedOverrideConnection = string.Empty;
                cachedOverrideRevision = -1;
                cachedOverrides = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            }
            CachedOverridesCleared?.Invoke(null, EventArgs.Empty);
        }

        internal static bool IsManagerRole(string role)
        {
            return ManagerRoles.Contains(NormalizeRole(role));
        }

        internal static bool GetRoleDefault(string role, AppOperation operation)
        {
            return CanExecuteByRole(NormalizeRole(role), operation);
        }

        private static bool CanExecuteByRole(string role, AppOperation operation)
        {
            if (ManagerRoles.Contains(role))
            {
                return true;
            }

            switch (operation)
            {
                case AppOperation.ManageChartOfAccounts:
                    return false;
                case AppOperation.PostAccountingEntry:
                case AppOperation.PostCashVoucher:
                case AppOperation.ViewAccountingReports:
                    return AccountantRoles.Contains(role);
                case AppOperation.PostSalesInvoice:
                    return SalesRoles.Contains(role);
                case AppOperation.IssueCommercialSalesReturn:
                case AppOperation.ResolveCommercialReturnQuarantine:
                case AppOperation.ManageAccountingPeriods:
                    // المرتجع يعدل المخزون والقيد والمستند الإلكتروني، ولا يكفي
                    // له إذن البيع العام حتى تظهر رحلة مراجعة مستقلة في الواجهة.
                    return false;
                case AppOperation.ManageActiveIngredients:
                    return false;
                case AppOperation.CancelSalesInvoice:
                    // إلغاء الفاتورة يؤثر في المخزون والقيد المالي، لذلك يبقى للمسؤول فقط.
                    return false;
                case AppOperation.CancelPurchaseInvoice:
                    // إلغاء المشتريات يغير المخزون والدفعات والقيد؛ يبقى للمسؤول فقط.
                    return false;
                case AppOperation.PostPurchaseInvoice:
                    return InventoryRoles.Contains(role) || SalesRoles.Contains(role);
                case AppOperation.PostInventoryOperation:
                    return InventoryRoles.Contains(role);
                case AppOperation.ManageSalesCustomers:
                    return InventoryRoles.Contains(role) || SalesRoles.Contains(role);
                default:
                    return false;
            }
        }

        private static bool TryGetCurrentUserOverride(string permissionKey, out bool isAllowed)
        {
            return string.IsNullOrWhiteSpace(SessionContext.CurrentEmployeeId)
                ? TryGetCurrentUserOverride(permissionKey, out isAllowed,
                    ApplicationServices.EmployeePermissions.GetByUserName)
                : TryGetCurrentUserOverride(permissionKey, out isAllowed,
                    ApplicationServices.EmployeePermissions.GetByEmployeeId);
        }

        internal static bool TryGetCurrentUserOverride(string permissionKey, out bool isAllowed,
            Func<string, EmployeePermissionSnapshot> loadSnapshot, int? tickCount = null)
        {
            isAllowed = false;
            string userName = string.IsNullOrWhiteSpace(SessionContext.CurrentUser) ? string.Empty : SessionContext.CurrentUser.Trim();
            string employeeId = string.IsNullOrWhiteSpace(SessionContext.CurrentEmployeeId) ? string.Empty : SessionContext.CurrentEmployeeId.Trim();
            if (userName.Length == 0) return false;
            string lookupValue = employeeId.Length > 0 ? employeeId : userName;
            string cacheKey = employeeId.Length > 0 ? "id:" + employeeId : "name:" + userName;
            long revision = SessionContext.CurrentSessionRevision;
            string connection;
            try { connection = Database.ConnectionString; }
            catch { return true; } // An unreadable source must not fall back to a role grant.

            lock (OverridesSync)
            {
                int now = tickCount ?? Environment.TickCount;
                if (!string.Equals(cachedOverrideUser, cacheKey, StringComparison.OrdinalIgnoreCase) ||
                    cachedOverrideRevision != revision || cachedOverrideConnection != connection ||
                    unchecked((uint)(now - cachedOverrideTick)) >= OverrideCacheLifetimeMilliseconds)
                {
                    cachedOverrideUser = cacheKey;
                    cachedOverrideConnection = connection;
                    cachedOverrideRevision = revision;
                    cachedOverrideTick = now;
                    cachedOverrides = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
                    try
                    {
                        var snapshot = loadSnapshot(lookupValue);
                        if (revision != SessionContext.CurrentSessionRevision || connection != Database.ConnectionString)
                        {
                            cachedOverrideUser = string.Empty;
                            cachedOverrideConnection = string.Empty;
                            cachedOverrideRevision = -1;
                            cachedOverrides.Clear();
                            return true;
                        }
                        foreach (KeyValuePair<string, bool> item in snapshot.Overrides)
                        {
                            if (!string.IsNullOrWhiteSpace(item.Key)) cachedOverrides[item.Key] = item.Value;
                        }
                    }
                    catch
                    {
                        // A transient read failure must not turn a stored deny into the role default.
                        // Keep no failed result in the cache so the next access retries the lookup.
                        cachedOverrideUser = string.Empty;
                        cachedOverrideConnection = string.Empty;
                        cachedOverrideRevision = -1;
                        cachedOverrides.Clear();
                        return true;
                    }
                }

                return cachedOverrides.TryGetValue(permissionKey, out isAllowed);
            }
        }

        public static bool EnsureAllowed(AppOperation operation, IWin32Window owner)
        {
            if (CanExecute(operation))
            {
                return true;
            }

            AppNotifications.Show(
                "لا تملك صلاحية تنفيذ " + GetArabicName(operation) + "." + Environment.NewLine +
                "الصلاحية الحالية: " + SessionContext.CurrentRole,
                "الصلاحيات",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            try
            {
                Database.WriteAudit("AccessDenied", "Permissions", "Denied operation: " + operation);
            }
            catch
            {
                // The access decision must stay enforced even if the audit store is unavailable.
            }

            return false;
        }

        public static string GetArabicName(AppOperation operation)
        {
            switch (operation)
            {
                case AppOperation.ManageChartOfAccounts:
                    return "تعديل شجرة الحسابات";
                case AppOperation.PostAccountingEntry:
                    return "ترحيل قيد محاسبي";
                case AppOperation.PostCashVoucher:
                    return "حفظ سند قبض أو صرف";
                case AppOperation.ViewAccountingReports:
                    return "عرض التقارير المحاسبية";
                case AppOperation.PostSalesInvoice:
                    return "اعتماد فاتورة بيع";
                case AppOperation.IssueCommercialSalesReturn:
                    return "اعتماد مرتجع تجاري";
                case AppOperation.ResolveCommercialReturnQuarantine:
                    return "الإفراج أو الإتلاف من مخزون الحجر";
                case AppOperation.ManageAccountingPeriods:
                    return "إدارة الفترات المحاسبية";
                case AppOperation.ManageActiveIngredients:
                    return "إدارة المواد الفعالة";
                case AppOperation.CancelSalesInvoice:
                    return "إلغاء فاتورة بيع معتمدة";
                case AppOperation.CancelPurchaseInvoice:
                    return "إلغاء فاتورة مشتريات معتمدة";
                case AppOperation.PostPurchaseInvoice:
                    return "اعتماد فاتورة مشتريات";
                case AppOperation.PostInventoryOperation:
                    return "تسجيل حركة مخزون";
                case AppOperation.ManageSalesCustomers:
                    return "إدارة بيانات العملاء";
                default:
                    return "هذا الإجراء";
            }
        }

        private static HashSet<string> CreateRoleSet(params string[] roles)
        {
            return new HashSet<string>(roles.Select(NormalizeRole), StringComparer.OrdinalIgnoreCase);
        }

        private static string NormalizeRole(string role)
        {
            return string.IsNullOrWhiteSpace(role) ? string.Empty : role.Trim();
        }
    }
}



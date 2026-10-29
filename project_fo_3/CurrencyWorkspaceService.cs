using System;
using System.Data;
using PharmacyManagement.Application;

namespace project_fo_3
{
    // مصدر موحد لعرض وإدارة العملة في الواجهة؛ يحافظ على قواعد Database الحالية.
    internal static class CurrencyWorkspaceService
    {
        internal static DataTable GetCurrencies()
        {
            var table = new DataTable();
            table.Columns.Add("الرقم", typeof(int));
            table.Columns.Add("الدولة", typeof(string));
            table.Columns.Add("العملة", typeof(string));
            table.Columns.Add("الرمز", typeof(string));
            table.Columns.Add("الاختصار", typeof(string));
            table.Columns.Add("عملة النظام", typeof(bool));
            foreach (CurrencyRecord row in ApplicationServices.CurrencyRead.GetAll())
                table.Rows.Add(row.CurrencyId, (object)row.CountryNameAr ?? DBNull.Value,
                    (object)row.CurrencyNameAr ?? DBNull.Value, (object)row.CurrencyCode ?? DBNull.Value,
                    (object)row.CurrencySymbol ?? DBNull.Value, row.IsSystemCurrency);
            table.AcceptChanges();
            return table;
        }

        internal static CurrencyInfo GetSystemCurrency()
        {
            CurrencyRecord row = ApplicationServices.CurrencyRead.GetSystemCurrency();
            return new CurrencyInfo
            {
                CurrencyId = row.CurrencyId, CountryNameAr = row.CountryNameAr ?? string.Empty,
                CurrencyNameAr = row.CurrencyNameAr ?? string.Empty, CurrencyCode = row.CurrencyCode ?? string.Empty,
                CurrencySymbol = row.CurrencySymbol ?? string.Empty, IsSystemCurrency = row.IsSystemCurrency
            };
        }

        internal static string GetSystemCurrencyDisplay()
        {
            return ApplicationServices.CurrencyRead.GetSystemCurrencyDisplay();
        }

        internal static void DeleteCurrency(int currencyId)
        {
            ExecuteAuthorizedMutation(() => ApplicationServices.CurrencyWrites.Delete(currencyId, CurrentActor()));
        }

        internal static void SetSystemCurrency(int currencyId)
        {
            ExecuteAuthorizedMutation(() => ApplicationServices.CurrencyWrites.SetSystemCurrency(currencyId, CurrentActor()));
        }
        internal static void SaveCurrency(CurrencyInfo currency)
        {
            ExecuteAuthorizedMutation(() => ApplicationServices.CurrencyWrites.Save(currency == null ? null : new CurrencyRecord
            {
                CurrencyId = currency.CurrencyId, CountryNameAr = currency.CountryNameAr,
                CurrencyNameAr = currency.CurrencyNameAr, CurrencyCode = currency.CurrencyCode,
                CurrencySymbol = currency.CurrencySymbol, IsSystemCurrency = currency.IsSystemCurrency
            }, CurrentActor()));
        }
        private static OperationActor CurrentActor() => new OperationActor
        {
            UserName = SessionContext.CurrentUser, UserRole = SessionContext.CurrentRole
        };

        internal static bool CanManageCurrencies => PermissionService.CanOpen(AppPermission.Settings);

        // Authorization precedes validation, connection creation and every write.
        internal static void ExecuteAuthorizedMutation(Action mutation)
        {
            if (!CanManageCurrencies)
                throw new UnauthorizedAccessException("لا تملك صلاحية تعديل العملات أو تغيير عملة النظام.");
            if (mutation == null) throw new ArgumentNullException(nameof(mutation));
            mutation();
        }
    }
}

using System.Data;
using System.Threading;
using System.Threading.Tasks;

namespace project_fo_3
{
    internal static class AccountingLookupDesktopService
    {
        internal static DataTable AcceptCurrent(DataTable result,System.Func<bool> isCurrent,CancellationToken cancellationToken,string deniedMessage)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if(!isCurrent())throw new System.UnauthorizedAccessException(deniedMessage);
                return result;
            }
            catch{result?.Dispose();throw;}
        }
        internal static DataTable LoadAccounts(bool activeOnly)
        {
            var session=ApplicationServices.CreateAccountingReadSession();
            return AcceptCurrent(AccountingLookupTableProjection.Accounts(session.Lookups.LoadAccounts(activeOnly,session.Actor)),()=>session.IsLookupCurrent,CancellationToken.None,"لا تملك صلاحية قراءة خيارات المحاسبة.");
        }
        internal static Task<DataTable> LoadAccountsAsync(bool activeOnly)
            =>LoadAccountsAsync(activeOnly,CancellationToken.None);
        internal static async Task<DataTable> LoadAccountsAsync(bool activeOnly,CancellationToken cancellationToken)
        {
            var session=ApplicationServices.CreateAccountingReadSession();
            var rows=await session.Lookups.LoadAccountsAsync(activeOnly,session.Actor,cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return AcceptCurrent(AccountingLookupTableProjection.Accounts(rows),()=>session.IsLookupCurrent,cancellationToken,"لا تملك صلاحية قراءة خيارات المحاسبة.");
        }
        internal static DataTable LoadCostCenters()
        {
            var session=ApplicationServices.CreateAccountingReadSession();
            return AcceptCurrent(AccountingLookupTableProjection.CostCenters(session.Lookups.LoadCostCenters(session.Actor)),()=>session.IsLookupCurrent,CancellationToken.None,"لا تملك صلاحية قراءة خيارات المحاسبة.");
        }
        internal static async Task<DataTable> LoadCostCentersAsync(CancellationToken cancellationToken)
        {
            var session=ApplicationServices.CreateAccountingReadSession();
            var rows=await session.Lookups.LoadCostCentersAsync(session.Actor,cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return AcceptCurrent(AccountingLookupTableProjection.CostCenters(rows),()=>session.IsLookupCurrent,cancellationToken,"لا تملك صلاحية قراءة خيارات المحاسبة.");
        }
    }
}

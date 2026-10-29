using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Application;
namespace project_fo_3
{
    internal static class JournalEntriesQuickReadService
    {
        internal static DataTable Load(DateTime fromDate,DateTime toDate,string sourceType)
        {
            var session=ApplicationServices.CreateAccountingReadSession();
            return AccountingLookupDesktopService.AcceptCurrent(JournalHistoryTableProjection.Create(session.History.Load(new JournalHistoryReadRequest(fromDate,toDate,sourceType),session.Actor)),()=>session.IsHistoryCurrent,CancellationToken.None,"لا تملك صلاحية قراءة سجل القيود.");
        }
        internal static Task<DataTable> LoadAsync(JournalHistoryReadRequest request)
            =>LoadAsync(request,CancellationToken.None);
        internal static async Task<DataTable> LoadAsync(JournalHistoryReadRequest request,CancellationToken cancellationToken)
        {
            var session=ApplicationServices.CreateAccountingReadSession();
            var rows=await session.History.LoadAsync(request,session.Actor,cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return AccountingLookupDesktopService.AcceptCurrent(JournalHistoryTableProjection.Create(rows),()=>session.IsHistoryCurrent,cancellationToken,"لا تملك صلاحية قراءة سجل القيود.");
        }
    }
}

using System;
using System.Linq;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using PharmacyManagement.Application;
namespace project_fo_3
{
  internal sealed class GeneralLedgerReadSession
  {
    private readonly IGeneralLedgerReadService ledger;
    private readonly IAccountingLookupReadService lookups;
    private readonly OperationActor actor;
    private readonly Func<bool> current;
    private readonly Func<GeneralLedgerReadRequest,CancellationToken,System.Threading.Tasks.Task<PharmacyManagement.Infrastructure.SqlServer.GeneralLedgerTableRead>> tableReader;
        internal GeneralLedgerReadSession(IGeneralLedgerReadService ledger, IAccountingLookupReadService lookups, OperationActor actor, Func<bool> current,
            Func<GeneralLedgerReadRequest,CancellationToken,System.Threading.Tasks.Task<PharmacyManagement.Infrastructure.SqlServer.GeneralLedgerTableRead>> tableReader=null)
        {
            this.ledger=ledger??throw new ArgumentNullException(nameof(ledger)); this.lookups=lookups??throw new ArgumentNullException(nameof(lookups));
            this.actor=new OperationActor{UserName=actor?.UserName,UserRole=actor?.UserRole}; this.current=current??throw new ArgumentNullException(nameof(current));
            this.tableReader=tableReader;
        }
        internal bool IsCurrent => current();
        internal void EnsureContext() { if(!IsCurrent)throw new InvalidOperationException("تغير المستخدم أو اتصال قاعدة البيانات؛ أعد تحميل كشف الحساب."); }
        internal async Task<IReadOnlyList<AccountLookupRow>> LoadAccountsAsync(CancellationToken cancellation)
        { EnsureContext();var rows=await lookups.LoadAccountsAsync(true,actor,cancellation);cancellation.ThrowIfCancellationRequested();EnsureContext();return rows; }
        internal async Task<IReadOnlyList<CostCenterLookupRow>> LoadCostCentersAsync(CancellationToken cancellation)
        { EnsureContext();var rows=await lookups.LoadCostCentersAsync(actor,cancellation);cancellation.ThrowIfCancellationRequested();EnsureContext();return rows; }
  internal async Task<GeneralLedgerReadResult> LoadAsync(GeneralLedgerReadRequest request,CancellationToken cancellation)
  { EnsureContext();var rows=await ledger.LoadAsync(request,actor,cancellation);cancellation.ThrowIfCancellationRequested();EnsureContext();return rows; }
    // The screen reads the rows straight into the table, so the rows are materialised once.
    internal async Task<PharmacyManagement.Infrastructure.SqlServer.GeneralLedgerTableRead> LoadTableAsync(GeneralLedgerReadRequest request,CancellationToken cancellation)
    {
      EnsureContext();
      if(tableReader==null)throw new InvalidOperationException("مسار قراءة الأستاذ الجدولي غير جاهز.");
      var rows=await tableReader(request,cancellation);cancellation.ThrowIfCancellationRequested();EnsureContext();return rows;
    }
    }
    internal static class GeneralLedgerPresentation
    {
        internal static DataTable ToTable(GeneralLedgerReadResult result,bool sortByDocument)
        {
            if(result==null)throw new ArgumentNullException(nameof(result));
            var table=new DataTable();
            string[] names={"التاريخ","رقم المستند","نوع المستند","رقم المرجع","مركز التكلفة","البيان","مدين","دائن","الرصيد"};
            Type[] types={typeof(DateTime),typeof(int),typeof(string),typeof(string),typeof(string),typeof(string),typeof(decimal),typeof(decimal),typeof(decimal)};
            for(int i=0;i<names.Length;i++)table.Columns.Add(names[i],types[i]);
            // The document order is applied on the typed rows, so the table is not copied a second time.
            IEnumerable<GeneralLedgerReadRow> rows=result.Rows;
            if(sortByDocument)rows=result.Rows.OrderBy(row=>row.Date).ThenBy(row=>row.DocumentId);
            foreach(var row in rows)table.Rows.Add(row.Date,row.DocumentId,row.SourceType,row.SourceId,row.CostCenterName,
                (object)row.Description??DBNull.Value,(object)row.Debit??DBNull.Value,(object)row.Credit??DBNull.Value,row.Balance);
            return table;
        }
    }
}

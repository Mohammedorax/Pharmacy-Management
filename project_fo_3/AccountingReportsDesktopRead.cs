using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Application;
namespace project_fo_3
{
    internal sealed class AccountingReportsReadSession
    {
        private readonly AccountingReportsReadService reports;
        private readonly IAccountingLookupReadService lookups;
        private readonly OperationActor actor;
        private readonly Func<bool> current;
        internal AccountingReportsReadSession(AccountingReportsReadService reports,IAccountingLookupReadService lookups,OperationActor actor,Func<bool> current)
        {
            this.reports=reports??throw new ArgumentNullException(nameof(reports));
            this.lookups=lookups??throw new ArgumentNullException(nameof(lookups));
            this.actor=new OperationActor{UserName=actor?.UserName,UserRole=actor?.UserRole};
            this.current=current??throw new ArgumentNullException(nameof(current));
        }
        internal bool IsCurrent=>current();
        internal void EnsureContext(){if(!IsCurrent)throw new InvalidOperationException("تغير المستخدم أو اتصال قاعدة البيانات؛ أعد تحميل التقرير المحاسبي.");}
        internal async Task<OperationalReportResult> LoadAsync(AccountingReportReadRequest request,CancellationToken cancellation)
        {
            EnsureContext();
            var result=await reports.LoadAsync(request,actor,cancellation).ConfigureAwait(false);
            cancellation.ThrowIfCancellationRequested();EnsureContext();return result;
        }
        internal async Task<IReadOnlyList<CostCenterLookupRow>> LoadCostCentersAsync(CancellationToken cancellation)
        {
            EnsureContext();
            var rows=await lookups.LoadCostCentersAsync(actor,cancellation).ConfigureAwait(false);
            cancellation.ThrowIfCancellationRequested();EnsureContext();return rows;
        }
    }
    internal static class AccountingReportsPresentation
    {
        internal static DataTable ToTable(OperationalReportResult result)=>OperationalReportCatalog.ToDataTable(result);
        internal static AccountingReportSummary Summarize(DataTable table)
        {
            var columns=new List<OperationalReportColumn>();
            foreach(DataColumn column in table.Columns)columns.Add(new OperationalReportColumn(column.ColumnName,column.DataType));
            var rows=new List<object[]>();foreach(DataRow row in table.Rows)rows.Add(row.ItemArray);
            return AccountingReportSummary.Calculate(new OperationalReportResult(columns,rows));
        }
    }
}

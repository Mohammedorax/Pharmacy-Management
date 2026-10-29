using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Application;
namespace project_fo_3
{
    internal static class AccountingReportsSessionSelfTests
    {
        internal static void Run()
        {
            var request=new AccountingReportReadRequest("Tax",DateTime.Today,DateTime.Today,null,false,false);
            bool current=true;
            var actor=new OperationActor{UserName="report",UserRole="Manager"};
            var report=new Probe();var lookups=new Lookups();
            var service=new AccountingReportsReadService(report,a=>a.UserName=="report"&&a.UserRole=="Manager");
            var session=new AccountingReportsReadSession(service,lookups,actor,()=>current);
            actor.UserName="changed";
            current=false;
            Reject<InvalidOperationException>(()=>session.LoadAsync(request,CancellationToken.None).GetAwaiter().GetResult());
            Check(report.Calls==0,"Stale report dispatched");
            current=true;
            var loading=session.LoadAsync(request,CancellationToken.None);
            current=false;report.Pending.SetResult(Result());
            Reject<InvalidOperationException>(()=>loading.GetAwaiter().GetResult());
            current=true;
            var centers=session.LoadCostCentersAsync(CancellationToken.None);
            current=false;lookups.Pending.SetResult(new[]{new CostCenterLookupRow(10,"الفرع الرئيسي")});
            Reject<InvalidOperationException>(()=>centers.GetAwaiter().GetResult());
            current=true;
            using(var cancellation=new CancellationTokenSource())
            {
                report.Pending=new TaskCompletionSource<OperationalReportResult>();
                loading=session.LoadAsync(request,cancellation.Token);
                Check(report.Token==cancellation.Token,"Report token changed");
                cancellation.Cancel();report.Pending.SetResult(Result());
                Reject<OperationCanceledException>(()=>loading.GetAwaiter().GetResult());
                lookups.Pending=new TaskCompletionSource<IReadOnlyList<CostCenterLookupRow>>();
                centers=session.LoadCostCentersAsync(cancellation.Token);
                lookups.Pending.SetResult(new CostCenterLookupRow[0]);
                Reject<OperationCanceledException>(()=>centers.GetAwaiter().GetResult());
            }
            using(var table=AccountingReportsPresentation.ToTable(Result()))
                Check(table.Rows.Count==1&&table.Columns[0].DataType==typeof(decimal)&&(decimal)table.Rows[0][0]==10.125m&&table.Rows[0][1]==DBNull.Value,"Report projection changed raw precision/null");
            try
            {
                SessionContext.Clear();
                var anonymous=ApplicationServices.CreateAccountingReportsReadSession();
                Check(!anonymous.IsCurrent,"Anonymous factory authorized");
                Reject<InvalidOperationException>(()=>anonymous.LoadAsync(request,CancellationToken.None).GetAwaiter().GetResult());
                SessionContext.Start("report-factory","Manager","fixture");
                var captured=ApplicationServices.CreateAccountingReportsReadSession();
                Check(captured.IsCurrent,"Manager captured context rejected");
                SessionContext.Start("report-factory","Manager","fixture");
                Check(!captured.IsCurrent,"Same-user new session accepted old factory");
                var fresh=ApplicationServices.CreateAccountingReportsReadSession();
                Check(fresh.IsCurrent,"Fresh factory failed");
                SessionContext.Clear();Check(!fresh.IsCurrent,"Logout retained report context");
            }
            finally{SessionContext.Clear();}
            Console.WriteLine("PASS accounting report session: captured actor, pre/post context, report/lookup cancellation, raw projection, anonymous/same-user/fresh/logout factory; no SQL/native input.");
        }
        private static OperationalReportResult Result()=>new OperationalReportResult(new[]{new OperationalReportColumn("مدين",typeof(decimal)),new OperationalReportColumn("البيان",typeof(string))},new[]{new object[]{10.125m,null}});
        private static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
        private static void Reject<T>(Action action)where T:Exception{try{action();}catch(T){return;}throw new Exception("Expected "+typeof(T).Name);}
        private sealed class Probe:IAccountingReportsReadRepository
        {
            internal int Calls;internal CancellationToken Token;
            internal TaskCompletionSource<OperationalReportResult> Pending=new TaskCompletionSource<OperationalReportResult>();
            public Task<OperationalReportResult> LoadAsync(AccountingReportReadRequest request,CancellationToken token){Calls++;Token=token;return Pending.Task;}
        }
        private sealed class Lookups:IAccountingLookupReadService
        {
            internal TaskCompletionSource<IReadOnlyList<CostCenterLookupRow>> Pending=new TaskCompletionSource<IReadOnlyList<CostCenterLookupRow>>();
            public IReadOnlyList<AccountLookupRow> LoadAccounts(bool activeOnly,OperationActor actor){throw new NotSupportedException();}
            public Task<IReadOnlyList<AccountLookupRow>> LoadAccountsAsync(bool activeOnly,OperationActor actor){throw new NotSupportedException();}
            public IReadOnlyList<CostCenterLookupRow> LoadCostCenters(OperationActor actor){throw new NotSupportedException();}
            public Task<IReadOnlyList<AccountLookupRow>> LoadAccountsAsync(bool activeOnly,OperationActor actor,CancellationToken token){throw new NotSupportedException();}
            public Task<IReadOnlyList<CostCenterLookupRow>> LoadCostCentersAsync(OperationActor actor,CancellationToken token){return Pending.Task;}
        }
    }
}

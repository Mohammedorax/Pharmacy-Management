using System;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Application;

namespace PharmacyManagement.CoreTests
{
    internal static class AccountingReportsReadTests
    {
        internal static int Run()
        {
            int checks=0;
            var actor=new OperationActor { UserName="admin", UserRole="Manager" };
            var request=Request("Tax");
            var probe=new Probe();
            Reject<ArgumentNullException>(()=>new AccountingReportsReadService(null),ref checks);
            Reject<UnauthorizedAccessException>(()=>Read(new AccountingReportsReadService(probe),request,actor),ref checks);
            Check(probe.Calls==0,"Default deny dispatched SQL boundary",ref checks);
            var service=new AccountingReportsReadService(probe,a=>a.UserName=="admin"&&a.UserRole=="Manager");
            Reject<ArgumentNullException>(()=>Read(service,null,actor),ref checks);
            Reject<UnauthorizedAccessException>(()=>Read(service,request,null),ref checks);
            Reject<UnauthorizedAccessException>(()=>Read(service,request,new OperationActor{UserName="admin",UserRole=""}),ref checks);
            Reject<UnauthorizedAccessException>(()=>Read(service,request,new OperationActor{UserName="cashier",UserRole="Cashier"}),ref checks);
            Check(probe.Calls==0,"Unauthorized request reached repository",ref checks);
            foreach(string key in new[]{"Tax","CostCenters","GeneralJournal","TrialBalance","ProfitLoss","BalanceSheet"})
            {
                var input=Request(key);
                var result=Read(service,input,actor);
                Check(ReferenceEquals(probe.Request,input)&&ReferenceEquals(result,probe.Result),"Report contract was rewritten",ref checks);
            }
            using(var cancellation=new CancellationTokenSource())
            {
                cancellation.Cancel();int calls=probe.Calls;
                Reject<OperationCanceledException>(()=>service.LoadAsync(request,actor,cancellation.Token).GetAwaiter().GetResult(),ref checks);
                Check(probe.Calls==calls,"Canceled request dispatched",ref checks);
            }
            bool allowed=true;
            var pending=new Probe{Pending=new TaskCompletionSource<OperationalReportResult>()};
            var guarded=new AccountingReportsReadService(pending,a=>allowed);
            var loading=guarded.LoadAsync(request,actor,CancellationToken.None);
            allowed=false;pending.Pending.SetResult(pending.Result);
            Reject<UnauthorizedAccessException>(()=>loading.GetAwaiter().GetResult(),ref checks);
            pending=new Probe{Pending=new TaskCompletionSource<OperationalReportResult>()};
            guarded=new AccountingReportsReadService(pending,a=>a.UserName=="admin"&&a.UserRole=="Manager");
            var mutable=new OperationActor{UserName="admin",UserRole="Manager"};
            loading=guarded.LoadAsync(request,mutable,CancellationToken.None);
            mutable.UserName="other";mutable.UserRole="Cashier";pending.Pending.SetResult(pending.Result);
            Check(ReferenceEquals(loading.GetAwaiter().GetResult(),pending.Result),"Caller mutated captured identity",ref checks);
            using(var cancellation=new CancellationTokenSource())
            {
                pending=new Probe{Pending=new TaskCompletionSource<OperationalReportResult>()};
                guarded=new AccountingReportsReadService(pending,a=>true);
                loading=guarded.LoadAsync(request,actor,cancellation.Token);
                Check(pending.Token==cancellation.Token,"Repository did not receive cancellation",ref checks);
                cancellation.Cancel();pending.Pending.SetResult(pending.Result);
                Reject<OperationCanceledException>(()=>loading.GetAwaiter().GetResult(),ref checks);
            }
            probe=new Probe{Result=null};service=new AccountingReportsReadService(probe,a=>true);
            Reject<InvalidOperationException>(()=>Read(service,request,actor),ref checks);
            pending=new Probe{Pending=new TaskCompletionSource<OperationalReportResult>()};
            guarded=new AccountingReportsReadService(pending,a=>true);
            loading=guarded.LoadAsync(request,actor,CancellationToken.None);
            pending.Pending.SetException(new TimeoutException("fixture"));
            Reject<TimeoutException>(()=>loading.GetAwaiter().GetResult(),ref checks);
            int connectionAttempts=0;
            var sqlBoundary=new PharmacyManagement.Infrastructure.SqlServer.SqlAccountingReportsReadRepository(()=>{connectionAttempts++;return null;},"1200","1210","2100");
            Reject<ArgumentNullException>(()=>sqlBoundary.LoadAsync(null,CancellationToken.None).GetAwaiter().GetResult(),ref checks);
            using(var cancellation=new CancellationTokenSource())
            {
                cancellation.Cancel();
                Reject<OperationCanceledException>(()=>sqlBoundary.LoadAsync(request,cancellation.Token).GetAwaiter().GetResult(),ref checks);
            }
            Check(connectionAttempts==0,"SQL repository opened for invalid/canceled request",ref checks);
            Reject<InvalidOperationException>(()=>sqlBoundary.LoadAsync(request,CancellationToken.None).GetAwaiter().GetResult(),ref checks);
            Check(connectionAttempts==1,"SQL repository did not validate missing connection",ref checks);
            Console.WriteLine("PASS: "+checks+" accounting report authorization/cancellation/six-mode contracts; no SQL/UI.");
            checks += SummaryChecks();
            return checks;
        }
        private static int SummaryChecks()
        {
            int checks=0;
            Reject<ArgumentNullException>(()=>AccountingReportSummary.Calculate(null),ref checks);
            var culture=System.Threading.Thread.CurrentThread.CurrentCulture;
            try
            {
                foreach(string language in new[]{"en-US","ar-YE","fr-FR"})
                {
                    System.Threading.Thread.CurrentThread.CurrentCulture=new System.Globalization.CultureInfo(language);
                    var columns=new[]{new OperationalReportColumn("مدين",typeof(object)),new OperationalReportColumn("دائن",typeof(object)),new OperationalReportColumn("حركة الحساب مدين",typeof(decimal)),new OperationalReportColumn("حركة الحساب دائن",typeof(decimal)),new OperationalReportColumn("رقم الحساب",typeof(string))};
                    var report=new OperationalReportResult(columns,new[]{new object[]{0.005m,DBNull.Value,7.125m,1.125m,"9999999999999999999999999999999999999999"},new object[]{0.005m,2.345m,null,2m,"1100"},new object[]{"bad",(-0.125m).ToString(),DBNull.Value,0m,"2100"}});
                    var summary=AccountingReportSummary.Calculate(report);
                    Check(summary.RowCount==3,"Summary row count",ref checks);
                    Check(summary.Total("مدين")==0.010m,"Summary rounded rows before aggregation",ref checks);
                    Check(summary.Total("دائن")==2.220m,"Summary null/string/culture conversion",ref checks);
                    Check(summary.MovementNet==-4m,"Summary movement sign",ref checks);
                    Check(summary.Total("الرصيد")==0m,"Missing summary column",ref checks);
                    Check(summary.Total("رقم الحساب")==0m,"Summary aggregated an identifier",ref checks);
                    var empty=AccountingReportSummary.Calculate(new OperationalReportResult(columns,new object[0][]));
                    Check(empty.RowCount==0&&empty.MovementNet==0m,"Empty summary",ref checks);
                    var overflow=new OperationalReportResult(new[]{new OperationalReportColumn("مدين",typeof(decimal))},new[]{new object[]{decimal.MaxValue},new object[]{1m}});
                    Reject<OverflowException>(()=>AccountingReportSummary.Calculate(overflow),ref checks);
                }
            }
            finally{System.Threading.Thread.CurrentThread.CurrentCulture=culture;}
            Console.WriteLine("PASS: "+checks+" report summary checks: fractions, sign, empty/missing/null, culture, identifiers and overflow.");
            return checks;
        }
        private static AccountingReportReadRequest Request(string key)=>new AccountingReportReadRequest(key,new DateTime(2026,1,1),new DateTime(2026,10,4),key=="CostCenters"?(int?)42:null,true,false);
        private static OperationalReportResult Read(AccountingReportsReadService service,AccountingReportReadRequest request,OperationActor actor)=>service.LoadAsync(request,actor,CancellationToken.None).GetAwaiter().GetResult();
        private static void Check(bool condition,string message,ref int checks){if(!condition)throw new Exception(message);checks++;}
        private static void Reject<T>(Action action,ref int checks)where T:Exception{try{action();}catch(T){checks++;return;}throw new Exception("Expected "+typeof(T).Name);}
        private sealed class Probe:IAccountingReportsReadRepository
        {
            internal int Calls;
            internal AccountingReportReadRequest Request;
            internal CancellationToken Token;
            internal TaskCompletionSource<OperationalReportResult> Pending;
            internal OperationalReportResult Result=new OperationalReportResult(new[]{new OperationalReportColumn("مدين",typeof(decimal))},new[]{new object[]{10.125m}});
            public Task<OperationalReportResult> LoadAsync(AccountingReportReadRequest request,CancellationToken cancellationToken)
            {Calls++;Request=request;Token=cancellationToken;return Pending==null?Task.FromResult(Result):Pending.Task;}
        }
    }
}

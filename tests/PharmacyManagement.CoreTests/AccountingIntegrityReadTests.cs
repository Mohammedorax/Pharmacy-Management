using System;
using PharmacyManagement.Application;
using PharmacyManagement.Infrastructure.SqlServer;

namespace PharmacyManagement.CoreTests
{
    internal static class AccountingIntegrityReadTests
    {
        internal static int Run()
        {
            int checks=0;
            DateTime from=new DateTime(2026,9,1,23,59,58,DateTimeKind.Utc),to=new DateTime(2026,9,30,1,2,3,DateTimeKind.Utc);
            var result=new OperationalReportResult(new[]{new OperationalReportColumn("رقم القيد",typeof(int)),
                new OperationalReportColumn("الفرق",typeof(decimal))},new[]{new object[]{1042,50.50m}});
            var repository=new Fake { Result=result };
            var service=new AccountingIntegrityReadService(repository, () => true);
            Check(ReferenceEquals(result,service.Load(from,to)) && repository.Calls==1,ref checks);
            Check(repository.Request.FromDate==from.Date && repository.Request.ToDate==to.Date &&
                repository.Request.FromDate.Kind==DateTimeKind.Utc,ref checks);
            service.Load(from,from);Check(repository.Request.FromDate==repository.Request.ToDate,ref checks);
            service.Load(new DateTime(1753,1,1),new DateTime(9999,12,30));
            Check(repository.Request.ToDate==new DateTime(9999,12,30),ref checks);
            ExpectInvalid(()=>service.Load(to,from),repository,ref checks);
            ExpectInvalid(()=>service.Load(new DateTime(1752,12,31),to),repository,ref checks);
            ExpectInvalid(()=>service.Load(from,DateTime.MaxValue),repository,ref checks);
            var failure=new InvalidOperationException("read failure");repository.Failure=failure;
            bool propagated=false;try {service.Load(from,to);}catch(InvalidOperationException ex){propagated=ReferenceEquals(failure,ex);}
            Check(propagated,ref checks);
            bool rejected=false;try {new AccountingIntegrityReadService(null);}catch(ArgumentNullException){rejected=true;}
            Check(rejected,ref checks);
            bool opened=false;var sql=new SqlAccountingIntegrityReadRepository(()=>{opened=true;throw failure;});
            Check(sql!=null && !opened,ref checks);
            rejected=false;try {sql.Load(null);}catch(ArgumentNullException){rejected=true;}
            Check(rejected && !opened,ref checks);
            rejected=false;try {SqlAccountingIntegrityReadRepository.Load(null,new AccountingIntegrityRequest(from,to));}catch(ArgumentNullException){rejected=true;}
            Check(rejected && !opened,ref checks);
            var denied = new Fake { Result = result };
            Reject<UnauthorizedAccessException>(() => new AccountingIntegrityReadService(denied).Load(from, to), ref checks);
            Check(denied.Calls == 0, ref checks);
            Reject<UnauthorizedAccessException>(() => new AccountingIntegrityReadService(denied, () => false).LoadAsync(from, to, System.Threading.CancellationToken.None).GetAwaiter().GetResult(), ref checks);
            Check(denied.Calls == 0, ref checks);
            bool current = true;
            denied.OnLoad = () => current = false;
            Reject<UnauthorizedAccessException>(() => new AccountingIntegrityReadService(denied, () => current).Load(from, to), ref checks);
            Check(denied.Calls == 1, ref checks);
            current = true; denied.OnLoad = null;
            var authorized = new AccountingIntegrityReadService(denied, () => current);
            Check(ReferenceEquals(result, authorized.LoadAsync(from, to, System.Threading.CancellationToken.None).GetAwaiter().GetResult()), ref checks);
            var delayed = new System.Threading.Tasks.TaskCompletionSource<OperationalReportResult>();
            denied.AsyncResult = delayed.Task;
            var pending = authorized.LoadAsync(from, to, System.Threading.CancellationToken.None);
            Check(!pending.IsCompleted, ref checks);
            current = false; delayed.SetResult(result);
            Reject<UnauthorizedAccessException>(() => pending.GetAwaiter().GetResult(), ref checks);
            current = true;
            delayed = new System.Threading.Tasks.TaskCompletionSource<OperationalReportResult>();
            denied.AsyncResult = delayed.Task;
            using (var canceled = new System.Threading.CancellationTokenSource())
            {
                pending = authorized.LoadAsync(from, to, canceled.Token);
                canceled.Cancel(); delayed.SetResult(result);
                Reject<OperationCanceledException>(() => pending.GetAwaiter().GetResult(), ref checks);
                int calls = denied.Calls;
                Reject<OperationCanceledException>(() => authorized.LoadAsync(from, to, canceled.Token).GetAwaiter().GetResult(), ref checks);
                Check(denied.Calls == calls, ref checks);
            }
            denied.AsyncResult = System.Threading.Tasks.Task.FromResult<OperationalReportResult>(null);
            Reject<InvalidOperationException>(() => authorized.LoadAsync(from, to, System.Threading.CancellationToken.None).GetAwaiter().GetResult(), ref checks);
            using (var canceled = new System.Threading.CancellationTokenSource())
            {
                canceled.Cancel();
                Reject<OperationCanceledException>(() => sql.LoadAsync(new AccountingIntegrityRequest(from, to), canceled.Token).GetAwaiter().GetResult(), ref checks);
                Check(!opened, ref checks);
            }
            Reject<ArgumentNullException>(() => sql.LoadAsync(null, System.Threading.CancellationToken.None).GetAwaiter().GetResult(), ref checks);
            Console.WriteLine("PASS: "+checks+" accounting integrity date/read contracts; no database connection.");
            return checks;
        }
        private static void Reject<T>(Action action, ref int checks) where T : Exception
        {
            bool rejected = false; try { action(); } catch (T) { rejected = true; }
            Check(rejected, ref checks);
        }        private static void ExpectInvalid(Action action,Fake repository,ref int checks)
        {
            int calls=repository.Calls;bool rejected=false;
            try {action();}catch(InvalidOperationException){rejected=true;}
            Check(rejected && repository.Calls==calls,ref checks);
        }
        private static void Check(bool condition,ref int checks)
        {if(!condition)throw new InvalidOperationException("Accounting integrity read contract changed.");checks++;}
        private sealed class Fake : IAccountingIntegrityReadRepository, IAsyncAccountingIntegrityReadRepository
        {
            internal OperationalReportResult Result;
            internal AccountingIntegrityRequest Request;
            internal Exception Failure;
            internal int Calls;
            internal Action OnLoad;
            internal System.Threading.Tasks.Task<OperationalReportResult> AsyncResult;
            public OperationalReportResult Load(AccountingIntegrityRequest request)
            {Calls++;Request=request;OnLoad?.Invoke();if(Failure!=null)throw Failure;return Result;}
            public System.Threading.Tasks.Task<OperationalReportResult> LoadAsync(AccountingIntegrityRequest request, System.Threading.CancellationToken cancellation)
            { Calls++; Request=request; return AsyncResult ?? System.Threading.Tasks.Task.FromResult(Result); }
        }
    }
}

using System;
using PharmacyManagement.Application;
using System.Threading;
using System.Threading.Tasks;

namespace PharmacyManagement.CoreTests
{
    internal static class AccountBalanceReadTests
    {
        internal static int Run()
        {
            int checks = 0;
            var request = new AccountBalanceReadRequest(" 1100 ", new DateTime(2024, 2, 29, 18, 30, 0));
            Check(request.AccountCode == "1100" && request.ThroughDate == new DateTime(2024, 2, 29) &&
                request.ExclusiveEnd == new DateTime(2024, 3, 1), ref checks);
            Check(new AccountBalanceReadRequest("1000", DateTime.MaxValue).ExclusiveEnd == null, ref checks);
            Check(new AccountBalanceReadRequest("1000", new DateTime(1753, 1, 1)).ExclusiveEnd == new DateTime(1753, 1, 2), ref checks);
            foreach (string invalid in new[] { null, " ", new string('1', 21) })
            {
                bool rejected = false;
                try { new AccountBalanceReadRequest(invalid, DateTime.Today); } catch (ArgumentException) { rejected = true; }
                Check(rejected, ref checks);
            }
            bool badDate = false;
            try { new AccountBalanceReadRequest("1100", new DateTime(1752, 12, 31)); } catch (ArgumentOutOfRangeException) { badDate = true; }
            Check(badDate, ref checks);
            bool nullRepository = false;
            try { new AccountBalanceReadService(null); } catch (ArgumentNullException) { nullRepository = true; }
            Check(nullRepository, ref checks);
            var repository = new Fake(); var actor = new OperationActor{UserName="balance-user",UserRole="Manager"};
            Func<OperationActor,bool> policy=value=>value!=null&&value.UserName=="balance-user"&&value.UserRole=="Manager";
            var service = new AccountBalanceReadService(repository,policy);
            bool nullRequest = false;
            try { service.LoadAsync(null); } catch (ArgumentNullException) { nullRequest = true; }
            Check(nullRequest && repository.Calls == 0, ref checks);
            foreach (decimal expected in new[] { 0m, -125.75m, 2.375m })
            {
                repository.Value = expected;
                Check(service.LoadAsync(request,actor,CancellationToken.None).GetAwaiter().GetResult() == expected && ReferenceEquals(repository.Request, request), ref checks);
            }
            repository.Failure = new InvalidOperationException("Simulated storage failure");
            bool propagated = false;
            try { service.LoadAsync(request,actor,CancellationToken.None).GetAwaiter().GetResult(); }
            catch (InvalidOperationException exception) { propagated = ReferenceEquals(exception, repository.Failure); }
            Check(propagated && repository.Calls == 4, ref checks);
            var deniedRepository=new Fake();var denied=new AccountBalanceReadService(deniedRepository);
            Check(Rejects<UnauthorizedAccessException>(()=>denied.LoadAsync(request,actor,CancellationToken.None).GetAwaiter().GetResult())&&deniedRepository.Calls==0,ref checks);
            Check(Rejects<UnauthorizedAccessException>(()=>service.LoadAsync(request).GetAwaiter().GetResult()),ref checks);
            foreach(var invalid in new[]{(OperationActor)null,new OperationActor{UserName="",UserRole="Manager"},new OperationActor{UserName="balance-user",UserRole=""},new OperationActor{UserName="forged",UserRole="Manager"}})
                Check(Rejects<UnauthorizedAccessException>(()=>service.LoadAsync(request,invalid,CancellationToken.None).GetAwaiter().GetResult()),ref checks);
            using(var cancellation=new CancellationTokenSource())
            {
                cancellation.Cancel();var untouched=new Fake();var cancelled=new AccountBalanceReadService(untouched,policy);
                Check(Rejects<OperationCanceledException>(()=>cancelled.LoadAsync(request,actor,cancellation.Token).GetAwaiter().GetResult())&&untouched.Calls==0,ref checks);
            }
            var capturing=new Fake{Pending=new TaskCompletionSource<decimal>()};bool allowed=true;
            var authorized=new AccountBalanceReadService(capturing,value=>allowed&&policy(value));
            var mutable=new OperationActor{UserName="balance-user",UserRole="Manager"};
            var reading=authorized.LoadAsync(request,mutable,CancellationToken.None);mutable.UserName="forged";capturing.Pending.SetResult(2.375m);
            Check(reading.GetAwaiter().GetResult()==2.375m,ref checks);
            capturing.Pending=new TaskCompletionSource<decimal>();reading=authorized.LoadAsync(request,actor,CancellationToken.None);allowed=false;capturing.Pending.SetResult(900m);
            Check(Rejects<UnauthorizedAccessException>(()=>reading.GetAwaiter().GetResult()),ref checks);allowed=true;
            using(var cancellation=new CancellationTokenSource())
            {
                capturing.Pending=new TaskCompletionSource<decimal>();reading=authorized.LoadAsync(request,actor,cancellation.Token);
                Check(capturing.Token==cancellation.Token&&ReferenceEquals(capturing.Request,request),ref checks);
                cancellation.Cancel();capturing.Pending.SetResult(100m);
                Check(Rejects<OperationCanceledException>(()=>reading.GetAwaiter().GetResult()),ref checks);
            }
            Check(Rejects<ArgumentNullException>(()=>authorized.LoadAsync(null,actor,CancellationToken.None).GetAwaiter().GetResult()),ref checks);
            Console.WriteLine("PASS: " + checks + " account balance validation/actor/default denial/revocation/cancellation/identity/error contracts; no database connection.");
            return checks;
        }
        private static bool Rejects<T>(Action action) where T:Exception
        {try{action();return false;}catch(T){return true;}}
        private static void Check(bool condition, ref int checks)
        { if (!condition) throw new InvalidOperationException("Account balance read contract changed."); checks++; }
        private sealed class Fake : IAccountBalanceReadRepository
        {
            internal int Calls; internal decimal Value; internal AccountBalanceReadRequest Request; internal Exception Failure;
            internal TaskCompletionSource<decimal> Pending;internal CancellationToken Token;
            public Task<decimal> LoadAsync(AccountBalanceReadRequest request,CancellationToken cancellationToken)
            {
                Token=cancellationToken;
                if(Pending!=null){Calls++;Request=request;return Pending.Task;}
                return Task.FromResult(Load(request));
            }
            public decimal Load(AccountBalanceReadRequest request)
            { Calls++; Request = request; if (Failure != null) throw Failure; return Value; }
        }
    }
}

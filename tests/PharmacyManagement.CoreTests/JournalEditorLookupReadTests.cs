using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Application;
namespace PharmacyManagement.CoreTests
{
    internal static class JournalEditorLookupReadTests
    {
        private static int checks;private static void Check(bool ok){if(!ok)throw new Exception("Journal editor lookup contract failed.");checks++;}
        private static void Throws<T>(Action action)where T:Exception{try{action();}catch(T){checks++;return;}throw new Exception("Expected "+typeof(T).Name);}
        private sealed class Repository:IJournalEditorLookupReadRepository
        {internal int Calls;internal CancellationToken Token;internal Func<Task<JournalEditorLookupSnapshot>> Read;
         public Task<JournalEditorLookupSnapshot> LoadAsync(CancellationToken token){Calls++;Token=token;return Read();}}
        private static JournalEditorLookupSnapshot Valid()=>new JournalEditorLookupSnapshot(
            new[]{new AccountLookupRow("00100","الصندوق 500 mg","Asset",null,true)},new[]{new CostCenterLookupRow(7,"المركز الرئيسي")},
            new[]{new JournalCurrencyLookupRow(1,"YER","ر.ي",true),new JournalCurrencyLookupRow(2,"USD","$",false)});
        internal static int Run()
        {
            checks=0;var actor=new OperationActor{UserName="qa",UserRole="Manager"};bool allowed=true;
            var repository=new Repository{Read=()=>Task.FromResult(Valid())};var service=new JournalEditorLookupReadService(repository,a=>allowed&&a.UserName=="qa"&&a.UserRole=="Manager");
            var result=service.LoadAsync(actor,CancellationToken.None).GetAwaiter().GetResult();
            Check(result.Accounts[0].Code=="00100"&&result.CostCenters[0].Id==7&&result.SystemCurrency.Code=="YER"&&result.Currencies.Count==2);
            allowed=false;int before=repository.Calls;Throws<UnauthorizedAccessException>(()=>service.LoadAsync(actor,CancellationToken.None).GetAwaiter().GetResult());Check(repository.Calls==before);allowed=true;
            Throws<UnauthorizedAccessException>(()=>service.LoadAsync(null,CancellationToken.None).GetAwaiter().GetResult());
            Throws<UnauthorizedAccessException>(()=>service.LoadAsync(new OperationActor{UserName="forged",UserRole="Manager"},CancellationToken.None).GetAwaiter().GetResult());
            using(var cts=new CancellationTokenSource()){cts.Cancel();before=repository.Calls;Throws<OperationCanceledException>(()=>service.LoadAsync(actor,cts.Token).GetAwaiter().GetResult());Check(repository.Calls==before);}
            var pending=new TaskCompletionSource<JournalEditorLookupSnapshot>();repository.Read=()=>pending.Task;var task=service.LoadAsync(actor,CancellationToken.None);allowed=false;pending.SetResult(Valid());Throws<UnauthorizedAccessException>(()=>task.GetAwaiter().GetResult());allowed=true;
            using(var cts=new CancellationTokenSource()){pending=new TaskCompletionSource<JournalEditorLookupSnapshot>();repository.Read=()=>pending.Task;task=service.LoadAsync(actor,cts.Token);Check(repository.Token==cts.Token);cts.Cancel();pending.SetResult(Valid());Throws<OperationCanceledException>(()=>task.GetAwaiter().GetResult());}
            pending=new TaskCompletionSource<JournalEditorLookupSnapshot>();repository.Read=()=>pending.Task;task=service.LoadAsync(actor,CancellationToken.None);actor.UserName="changed";actor.UserRole="Cashier";pending.SetResult(Valid());Check(task.GetAwaiter().GetResult().SystemCurrency.Code=="YER");actor.UserName="qa";actor.UserRole="Manager";
            repository.Read=()=>Task.FromResult<JournalEditorLookupSnapshot>(null);Throws<InvalidOperationException>(()=>service.LoadAsync(actor,CancellationToken.None).GetAwaiter().GetResult());
            repository.Read=()=>Task.FromException<JournalEditorLookupSnapshot>(new TimeoutException());Throws<TimeoutException>(()=>service.LoadAsync(actor,CancellationToken.None).GetAwaiter().GetResult());
            var accounts=new List<AccountLookupRow>{new AccountLookupRow("00100","Cash","Asset",null,true)};var centers=new List<CostCenterLookupRow>{new CostCenterLookupRow(7,"Main")};var currencies=new List<JournalCurrencyLookupRow>{new JournalCurrencyLookupRow(1,"YER","ر.ي",true)};
            var snapshot=new JournalEditorLookupSnapshot(accounts,centers,currencies);accounts.Clear();centers.Clear();currencies.Clear();Check(snapshot.Accounts.Count==1&&snapshot.CostCenters.Count==1&&snapshot.Currencies.Count==1);
            var valid=Valid();Throws<CurrencySnapshotUnavailableException>(()=>new JournalEditorLookupSnapshot(valid.Accounts,valid.CostCenters,new JournalCurrencyLookupRow[0]));
            Throws<CurrencySnapshotUnavailableException>(()=>new JournalEditorLookupSnapshot(valid.Accounts,valid.CostCenters,new[]{new JournalCurrencyLookupRow(1,"YER","",false)}));
            Throws<CurrencySnapshotUnavailableException>(()=>new JournalEditorLookupSnapshot(valid.Accounts,valid.CostCenters,new[]{new JournalCurrencyLookupRow(1,"YER","",true),new JournalCurrencyLookupRow(2,"USD","",true)}));
            Throws<CurrencySnapshotUnavailableException>(()=>new JournalEditorLookupSnapshot(valid.Accounts,valid.CostCenters,new[]{new JournalCurrencyLookupRow(0,"YER","",true)}));
            Throws<CurrencySnapshotUnavailableException>(()=>new JournalEditorLookupSnapshot(valid.Accounts,valid.CostCenters,new[]{new JournalCurrencyLookupRow(1," ","",true)}));
            Throws<InvalidOperationException>(()=>new JournalEditorLookupSnapshot(null,valid.CostCenters,valid.Currencies));
            Throws<InvalidOperationException>(()=>new JournalEditorLookupSnapshot(new AccountLookupRow[]{null},valid.CostCenters,valid.Currencies));
            Console.WriteLine("PASS journal editor typed options: complete immutable snapshots, default denial, actor capture/revocation, cancellation/forwarding, missing/invalid/ambiguous currency, null and repository errors; no SQL.");return checks;
        }
    }
}

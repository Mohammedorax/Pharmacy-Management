using System;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Application;
namespace PharmacyManagement.CoreTests
{
internal static class SalesCustomerCreditReadTests
{
    static int n;static void Check(bool v,string text){if(!v)throw new Exception(text);n++;}
    static void Reject<T>(Action call,string text)where T:Exception{try{call();}catch(T){n++;return;}throw new Exception(text);}
    sealed class Repo:ISalesCustomerCreditReadRepository{public int Calls;public TaskCompletionSource<SalesCustomerCreditSnapshot> Pending;public CancellationToken Token;public Task<SalesCustomerCreditSnapshot> ReadAsync(SalesCustomerCreditReadRequest r,CancellationToken t){Calls++;Token=t;return Pending.Task;}}
    internal static int Run()
    {
        n=0;
            var r=new Repo();bool allowed=true;var actor=new OperationActor{UserName="u",UserRole="Manager"};
            var s=new SalesCustomerCreditReadService(r,a=>allowed&&a.UserName=="u"&&a.UserRole=="Manager");var req=new SalesCustomerCreditReadRequest(" 00012 ");
            Check(req.CustomerId=="00012","ID normalization");Reject<ArgumentException>(()=>new SalesCustomerCreditReadRequest(" "),"Empty ID");Reject<ArgumentException>(()=>new SalesCustomerCreditReadRequest(new string('x',51)),"ID limit");
            Reject<UnauthorizedAccessException>(()=>s.ReadAsync(req,null,CancellationToken.None),"Unsigned credit");Reject<UnauthorizedAccessException>(()=>s.ReadAsync(req,new OperationActor{UserName="forged",UserRole="Manager"},CancellationToken.None),"Forged credit");Check(r.Calls==0,"Denied storage");
            using(var ct=new CancellationTokenSource()){ct.Cancel();Reject<OperationCanceledException>(()=>s.ReadAsync(req,actor,ct.Token),"Precancel");Check(r.Calls==0,"Canceled storage");}
            foreach(decimal raw in new[]{0m,-1.234567m,987.654321m}){
                r.Pending=new TaskCompletionSource<SalesCustomerCreditSnapshot>();var mutable=new OperationActor{UserName="u",UserRole="Manager"};var read=s.ReadAsync(req,mutable,CancellationToken.None);mutable.UserName="forged";r.Pending.SetResult(new SalesCustomerCreditSnapshot("00012",raw));var got=read.GetAwaiter().GetResult();Check(got.CustomerId=="00012"&&got.Balance==raw,"Raw balance / actor snapshot");
            }
            r.Pending=new TaskCompletionSource<SalesCustomerCreditSnapshot>();var revoked=s.ReadAsync(req,actor,CancellationToken.None);allowed=false;r.Pending.SetResult(new SalesCustomerCreditSnapshot("00012",9m));Reject<UnauthorizedAccessException>(()=>revoked.GetAwaiter().GetResult(),"Revoked balance");allowed=true;
            using(var ct=new CancellationTokenSource()){r.Pending=new TaskCompletionSource<SalesCustomerCreditSnapshot>();var late=s.ReadAsync(req,actor,ct.Token);Check(r.Token==ct.Token,"Credit token");ct.Cancel();r.Pending.SetResult(new SalesCustomerCreditSnapshot("00012",9m));Reject<OperationCanceledException>(()=>late.GetAwaiter().GetResult(),"Late balance");}
            foreach(var result in new[]{null,new SalesCustomerCreditSnapshot("OTHER",9m)}){r.Pending=new TaskCompletionSource<SalesCustomerCreditSnapshot>();var read=s.ReadAsync(req,actor,CancellationToken.None);r.Pending.SetResult(result);Reject<InvalidOperationException>(()=>read.GetAwaiter().GetResult(),"Missing/wrong customer credit");}
            r.Pending=new TaskCompletionSource<SalesCustomerCreditSnapshot>();var failed=s.ReadAsync(req,actor,CancellationToken.None);r.Pending.SetException(new InvalidOperationException("failure"));Reject<InvalidOperationException>(()=>failed.GetAwaiter().GetResult(),"Error swallowed");
            r.Pending=new TaskCompletionSource<SalesCustomerCreditSnapshot>();var recovered=s.ReadAsync(req,actor,CancellationToken.None);r.Pending.SetResult(new SalesCustomerCreditSnapshot("00012",0m));Check(recovered.GetAwaiter().GetResult().Balance==0m,"Recovery");
            Console.WriteLine("PASS customer credit read candidate: "+n+" assertions; actor/authorization/revocation/cancellation, exact customer/raw signed decimals, missing/error/recovery; no SQL, no UI.");return n;
        
    }
}
}
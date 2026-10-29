using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Application;
namespace PharmacyManagement.CoreTests
{
internal static class CustomerReferenceReadTests
{
    static int count;
    static void Check(bool ok,string text){if(!ok)throw new Exception(text);count++;}
    static void Throws<T>(Action action,string text)where T:Exception{try{action();}catch(T){count++;return;}throw new Exception(text);}
    sealed class Repository:ICustomerReferenceReadRepository
    {
        public int Calls;public CustomerReferenceReadRequest Seen;public CancellationToken Token;
        public TaskCompletionSource<CustomerReferenceReadResult> Pending;
        public Task<CustomerReferenceReadResult> ReadAsync(CustomerReferenceReadRequest request,CancellationToken token)
        {Calls++;Seen=request;Token=token;return Pending.Task;}
    }
    internal static int Run()
    {
        count=0;
            var actor=new OperationActor{UserName=" u ",UserRole=" Manager "};bool allowed=true;
            var repo=new Repository();
            var service=new CustomerReferenceReadService(repo,a=>allowed&&a.UserName=="u"&&a.UserRole=="Manager");
            var request=new CustomerReferenceReadRequest(CustomerReferenceKind.Names);
            Throws<UnauthorizedAccessException>(()=>service.ReadAsync(request,null,CancellationToken.None),"Anonymous read");
            Throws<UnauthorizedAccessException>(()=>service.ReadAsync(request,new OperationActor{UserName="other",UserRole="Manager"},CancellationToken.None),"Forged read");
            Check(repo.Calls==0,"Denied storage call");
            using(var cancel=new CancellationTokenSource())
            {cancel.Cancel();Throws<OperationCanceledException>(()=>service.ReadAsync(request,actor,cancel.Token),"Pre-cancel");Check(repo.Calls==0,"Canceled storage call");}
            Throws<ArgumentOutOfRangeException>(()=>new CustomerReferenceReadRequest((CustomerReferenceKind)99),"Unknown kind");
            Throws<ArgumentException>(()=>new CustomerReferenceReadRequest(CustomerReferenceKind.Names,"x"),"List search value");
            foreach(var kind in new[]{CustomerReferenceKind.ById,CustomerReferenceKind.ByPhone,CustomerReferenceKind.ByName})
            {
                int limit=kind==CustomerReferenceKind.ByName?200:50;
                Throws<ArgumentException>(()=>new CustomerReferenceReadRequest(kind,new string('x',limit+1)),"Overlong value");
                Check(new CustomerReferenceReadRequest(kind,new string('x',limit)).Value.Length==limit,"Boundary value");
                var empty=service.ReadAsync(new CustomerReferenceReadRequest(kind,"  "),actor,CancellationToken.None).GetAwaiter().GetResult();
                Check(empty.Customer==null&&empty.Kind==kind&&repo.Calls==0,"Empty lookup contacts storage");
            }
            var raw=new CustomerReferenceSnapshot("0","عميل Pharma 500 mg","0000012","عدن Main 12",1.234567m);
            foreach(CustomerReferenceKind kind in Enum.GetValues(typeof(CustomerReferenceKind)))
            {
                repo.Pending=new TaskCompletionSource<CustomerReferenceReadResult>();
                var r=new CustomerReferenceReadRequest(kind,kind==CustomerReferenceKind.Names||kind==CustomerReferenceKind.Phones?null:"  0000012  ");
                var caller=new OperationActor{UserName="u",UserRole="Manager"};
                var read=service.ReadAsync(r,caller,CancellationToken.None);caller.UserName="other";
                bool lookup=r.IsLookup;var source=new List<string>{" مُحمّد Pharma 500 mg ","0000012"};
                repo.Pending.SetResult(new CustomerReferenceReadResult(kind,lookup?(IEnumerable<string>)new string[0]:source,lookup?raw:null));
                var result=read.GetAwaiter().GetResult();source.Clear();
                Check(result.Kind==kind&&repo.Seen==r,"Typed request/result");
                if(lookup){Check(result.Customer.TotalSpent==1.234567m&&result.Customer.CustomerId=="0"&&result.Customer.CustomerName==raw.CustomerName,"Raw snapshot changed");Check(r.Value=="0000012","Normalization/leading zero");}
                else Check(result.Values.Count==2&&result.Values[0]==" مُحمّد Pharma 500 mg ","Mutable list/raw text changed");
            }
            repo.Pending=new TaskCompletionSource<CustomerReferenceReadResult>();var revoked=service.ReadAsync(request,actor,CancellationToken.None);
            allowed=false;repo.Pending.SetResult(new CustomerReferenceReadResult(CustomerReferenceKind.Names,new[]{"private"},null));
            Throws<UnauthorizedAccessException>(()=>revoked.GetAwaiter().GetResult(),"Post-read revocation");allowed=true;
            using(var cancel=new CancellationTokenSource())
            {
                repo.Pending=new TaskCompletionSource<CustomerReferenceReadResult>();var late=service.ReadAsync(request,actor,cancel.Token);
                Check(repo.Token==cancel.Token,"Token lost");cancel.Cancel();repo.Pending.SetResult(new CustomerReferenceReadResult(CustomerReferenceKind.Names,new[]{"private"},null));
                Throws<OperationCanceledException>(()=>late.GetAwaiter().GetResult(),"Late cancellation");
            }
            repo.Pending=new TaskCompletionSource<CustomerReferenceReadResult>();var missing=service.ReadAsync(request,actor,CancellationToken.None);repo.Pending.SetResult(null);
            Throws<InvalidOperationException>(()=>missing.GetAwaiter().GetResult(),"Missing result accepted");
            repo.Pending=new TaskCompletionSource<CustomerReferenceReadResult>();var wrong=service.ReadAsync(request,actor,CancellationToken.None);repo.Pending.SetResult(new CustomerReferenceReadResult(CustomerReferenceKind.Phones,new[]{"private"},null));
            Throws<InvalidOperationException>(()=>wrong.GetAwaiter().GetResult(),"Cross-kind result accepted");
            repo.Pending=new TaskCompletionSource<CustomerReferenceReadResult>();var failed=service.ReadAsync(request,actor,CancellationToken.None);repo.Pending.SetException(new InvalidOperationException("failure"));
            Throws<InvalidOperationException>(()=>failed.GetAwaiter().GetResult(),"Failure swallowed");
            repo.Pending=new TaskCompletionSource<CustomerReferenceReadResult>();var retry=service.ReadAsync(request,actor,CancellationToken.None);repo.Pending.SetResult(new CustomerReferenceReadResult(CustomerReferenceKind.Names,new string[0],null));
            Check(retry.GetAwaiter().GetResult().Values.Count==0,"Empty recovery");
            Throws<ArgumentException>(()=>new CustomerReferenceReadResult(CustomerReferenceKind.Names,new string[0],raw),"Customer attached to list");
            Throws<ArgumentException>(()=>new CustomerReferenceReadResult(CustomerReferenceKind.ById,new[]{"x"},raw),"List attached to lookup");
            Console.WriteLine("PASS customer reference candidate: "+count+" assertions; five typed reads, authorization/revocation/cancellation, actor/value/result snapshots, raw Arabic/decimal/leading-zero, errors/empty/recovery; no SQL or UI.");
            return count;
        
    }
}
}
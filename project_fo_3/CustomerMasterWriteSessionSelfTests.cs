using System;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Application;
using project_fo_3;
namespace project_fo_3 {
internal static class CustomerMasterWriteSessionSelfTests
{
    static int checks;
    static void Check(bool v,string text){if(!v)throw new Exception(text);checks++;}
    static async Task Throws<T>(Func<Task> run) where T:Exception
    {try{await run();}catch(T){checks++;return;}throw new Exception("Expected "+typeof(T).Name);}
    static OperationActor Owner(string role="Manager")=>new OperationActor{UserName="owner",UserRole=role};
    static CustomerMasterWriteAttempt Attempt()=>new CustomerMasterWriteAttempt(
        new CustomerMasterWriteRequest(Guid.NewGuid(),new SalesCustomer{CustomerName="عربي & 001",PhoneNumber="001234"},Owner()),"source-a");
    sealed class Fake:ICustomerMasterWriteService
    {
        public int Calls; public Action OnCall; public CancellationTokenSource Cancel; public string Role;
        public Task<CustomerMasterWriteReceipt> SubmitAsync(CustomerMasterWriteRequest r,OperationActor a,CancellationToken t)
        {Calls++;Role=a.UserRole;OnCall?.Invoke();Cancel?.Cancel();return Task.FromResult(new CustomerMasterWriteReceipt(r.RequestId,"001",1,DateTime.UtcNow,true,true));}
        public Task<CustomerMasterWriteReceipt> CancelAsync(CustomerMasterWriteRequest r,OperationActor a,CancellationToken t)
        {return SubmitAsync(r,a,t);}
        public Task<CustomerMasterWriteStatus> ResolveAsync(CustomerMasterWriteRequest r,OperationActor a,CancellationToken t)
        {Calls++;Role=a.UserRole;OnCall?.Invoke();Cancel?.Cancel();return Task.FromResult(new CustomerMasterWriteStatus(null));}
    }
    internal static int Run(){try{VerifyAsync().GetAwaiter().GetResult();Console.WriteLine("PASS customer write desktop session: checks="+checks);return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
    static async Task VerifyAsync()
    {
        var f=new Fake();bool current=true;
        var s=new CustomerMasterWriteSession(f,Owner(),"source-a",()=>current);
        var a=Attempt();var anonymous=new CustomerMasterWriteSession(f,new OperationActor(),"source-a",()=>true);Check(!anonymous.IsCurrent,"anonymous denied despite callback");await Throws<UnauthorizedAccessException>(()=>anonymous.ResolveAsync(a,CancellationToken.None));
        var actor=s.Actor;actor.UserName="forged";Check(s.Actor.UserName=="owner","actor copy");
        Check(s.IsCurrent,"initial");current=false;Check(!s.IsCurrent,"revoke");current=true;Check(!s.IsCurrent,"sticky");
        await Throws<UnauthorizedAccessException>(()=>s.SubmitAsync(a,CancellationToken.None));Check(f.Calls==0,"no call revoked");
        var wrong=new CustomerMasterWriteSession(f,Owner(),"source-b",()=>true);
        await Throws<UnauthorizedAccessException>(()=>wrong.ResolveAsync(a,CancellationToken.None));Check(f.Calls==0,"source guard");
        var other=new CustomerMasterWriteSession(f,new OperationActor{UserName="other",UserRole="Manager"},"source-a",()=>true);
        await Throws<UnauthorizedAccessException>(()=>other.CancelAsync(a,CancellationToken.None));Check(f.Calls==0,"owner guard");
        var newRole=new CustomerMasterWriteSession(f,Owner("Administrator"),"source-a",()=>true);
        await Throws<UnauthorizedAccessException>(()=>newRole.SubmitAsync(a,CancellationToken.None));Check(f.Calls==0,"original role submit");
        Check(!(await newRole.ResolveAsync(a,CancellationToken.None)).IsFound,"new role resolve");Check(f.Role=="Administrator","current actor resolve");
        Check((await newRole.CancelAsync(a,CancellationToken.None)).RequestId==a.Request.RequestId,"new role close");
        current=true;f.OnCall=()=>current=false;s=new CustomerMasterWriteSession(f,Owner(),"source-a",()=>current);
        await Throws<CustomerMasterWriteOutcomeUnknownException>(()=>s.SubmitAsync(a,CancellationToken.None));Check(!s.IsCurrent,"post write sticky");
        current=true;s=new CustomerMasterWriteSession(f,Owner(),"source-a",()=>current);
        await Throws<CustomerMasterWriteOutcomeUnknownException>(()=>s.CancelAsync(a,CancellationToken.None));
        current=true;s=new CustomerMasterWriteSession(f,Owner(),"source-a",()=>current);
        await Throws<UnauthorizedAccessException>(()=>s.ResolveAsync(a,CancellationToken.None));
        f.OnCall=null;current=true;s=new CustomerMasterWriteSession(f,Owner(),"source-a",()=>current);
        using(var c=new CancellationTokenSource())
        {c.Cancel();int n=f.Calls;await Throws<OperationCanceledException>(()=>s.SubmitAsync(a,c.Token));Check(n==f.Calls,"pre cancel no call");}
        using(var c=new CancellationTokenSource())
        {f.Cancel=c;Check((await s.SubmitAsync(a,c.Token)).CustomerId=="001","late cancel keeps receipt");}
        using(var c=new CancellationTokenSource())
        {f.Cancel=c;await Throws<OperationCanceledException>(()=>s.ResolveAsync(a,c.Token));}
        f.Cancel=null;Check(a.Request.CustomerName=="عربي & 001","raw snapshot preserved");
    }
}


}

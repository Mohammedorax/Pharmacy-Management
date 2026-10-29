using System;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Application;
namespace PharmacyManagement.CoreTests {
internal static class CustomerMasterWriteTests
{
    static int n;
    static void Check(bool ok,string text){n++;if(!ok)throw new Exception(text);}
    static void Reject<T>(Action action,string text)where T:Exception{try{action();}catch(T){n++;return;}throw new Exception(text);}
    static OperationActor Actor(string role="Manager")=>new OperationActor{UserName="owner",UserRole=role};
    static SalesCustomer Customer(string id="")=>new SalesCustomer{CustomerId=id,CustomerName=" عربي A&B ",PhoneNumber=" 0000123 ",Address=" شارع 500 mg "};
    static CustomerMasterWriteReceipt Receipt(CustomerMasterWriteRequest request,bool expected=true,bool current=true)
        =>new CustomerMasterWriteReceipt(request.RequestId,"C1",1,DateTime.UtcNow,expected,current);
    sealed class Repo:ICustomerMasterWriteRepository
    {
        public int Submit,Resolve,Cancel;public OperationActor Actor;public Action Authorize;public CancellationToken Token;
        public TaskCompletionSource<CustomerMasterWriteReceipt> Pending=new TaskCompletionSource<CustomerMasterWriteReceipt>();
        public TaskCompletionSource<CustomerMasterWriteStatus> Status=new TaskCompletionSource<CustomerMasterWriteStatus>();
        public Task<CustomerMasterWriteReceipt> SubmitAsync(CustomerMasterWriteRequest r,OperationActor a,Action before,CancellationToken token)
        {Submit++;Actor=a;Authorize=before;Token=token;return Pending.Task;}
        public Task<CustomerMasterWriteReceipt> CancelAsync(CustomerMasterWriteRequest r,OperationActor a,Action before,CancellationToken token)
        {Cancel++;Actor=a;Authorize=before;Token=token;return Pending.Task;}
        public Task<CustomerMasterWriteStatus> ResolveAsync(CustomerMasterWriteRequest r,OperationActor a,CancellationToken token)
        {Resolve++;Actor=a;Token=token;return Status.Task;}
        public void Next(){Pending=new TaskCompletionSource<CustomerMasterWriteReceipt>();Status=new TaskCompletionSource<CustomerMasterWriteStatus>();}
    }
    internal static int Run()
    {
        try{
            var dto=Customer();var caller=Actor();var request=new CustomerMasterWriteRequest(Guid.NewGuid(),dto,caller);
            Check(request.Kind==CustomerMasterWriteKind.Create&&request.CustomerName=="عربي A&B"&&request.PhoneNumber=="0000123"&&request.Address=="شارع 500 mg","Normalization/raw fields lost");
            dto.CustomerName="changed";caller.UserName="forged";Check(request.CustomerName=="عربي A&B"&&request.OwnerUser=="owner","Mutable source changed request");
            var copy=request.CopyCustomer();copy.PhoneNumber="new";Check(request.PhoneNumber=="0000123","Copy exposed mutable request");
            Check(new CustomerMasterWriteRequest(Guid.NewGuid(),Customer(" C1 "),Actor()).Kind==CustomerMasterWriteKind.Update,"Update target lost");
            Reject<ArgumentException>(()=>new CustomerMasterWriteRequest(Guid.Empty,Customer(),Actor()),"Empty request identity");
            Reject<ArgumentException>(()=>new CustomerMasterWriteRequest(Guid.NewGuid(),Customer("0"),Actor()),"General customer mutable");
            var bad=Customer();bad.PhoneNumber="000000000";Reject<ArgumentException>(()=>new CustomerMasterWriteRequest(Guid.NewGuid(),bad,Actor()),"General phone allowed");
            foreach(int length in new[]{201,301}){bad=Customer();if(length==201)bad.CustomerName=new string('n',length);else bad.Address=new string('a',length);Reject<ArgumentException>(()=>new CustomerMasterWriteRequest(Guid.NewGuid(),bad,Actor()),"Field maximum lost");}
            var repo=new Repo();bool granted=true;var service=new CustomerMasterWriteService(repo,a=>granted&&a.UserName=="owner",a=>granted&&a.UserName=="owner");
            Reject<UnauthorizedAccessException>(()=>new CustomerMasterWriteService(repo).SubmitAsync(request,Actor(),CancellationToken.None),"Default submit allowed");
            Reject<UnauthorizedAccessException>(()=>new CustomerMasterWriteService(repo).ResolveAsync(request,Actor(),CancellationToken.None),"Default resolve allowed");
            Reject<UnauthorizedAccessException>(()=>service.SubmitAsync(request,null,CancellationToken.None),"Anonymous allowed");
            Reject<UnauthorizedAccessException>(()=>service.SubmitAsync(request,new OperationActor{UserName="other",UserRole="Manager"},CancellationToken.None),"Other owner allowed");
            using(var token=new CancellationTokenSource()){token.Cancel();Reject<OperationCanceledException>(()=>service.SubmitAsync(request,Actor(),token.Token),"Precancel lost");}
            Check(repo.Submit+repo.Resolve+repo.Cancel==0,"Denied request reached repository");
            caller=Actor();var pending=service.SubmitAsync(request,caller,CancellationToken.None);caller.UserName="changed";repo.Authorize();
            Check(repo.Actor.UserName=="owner","Actor not captured");repo.Pending.SetResult(Receipt(request));Check(pending.GetAwaiter().GetResult().RequestId==request.RequestId,"Known receipt lost");
            repo.Next();pending=service.SubmitAsync(request,Actor(),CancellationToken.None);granted=false;Reject<UnauthorizedAccessException>(()=>repo.Authorize(),"Before-send authorization not checked");
            repo.Pending.SetResult(Receipt(request));Reject<CustomerMasterWriteOutcomeUnknownException>(()=>pending.GetAwaiter().GetResult(),"Post-write revocation claimed rejection");granted=true;
            repo.Next();pending=service.SubmitAsync(request,Actor(),CancellationToken.None);repo.Pending.SetException(new Exception("write transport failed"));Reject<CustomerMasterWriteOutcomeUnknownException>(()=>pending.GetAwaiter().GetResult(),"Untyped repository failure considered safe retry");
            repo.Next();pending=service.SubmitAsync(request,Actor(),CancellationToken.None);repo.Pending.SetException(new CustomerMasterWriteRejectedException("not sent"));Reject<CustomerMasterWriteRejectedException>(()=>pending.GetAwaiter().GetResult(),"Known no-send rejection became committed");
            repo.Next();using(var token=new CancellationTokenSource()){pending=service.SubmitAsync(request,Actor(),token.Token);token.Cancel();repo.Pending.SetResult(Receipt(request));Check(pending.GetAwaiter().GetResult().CustomerId=="C1","Late cancel implied rollback");}
            repo.Next();pending=service.SubmitAsync(request,Actor(),CancellationToken.None);repo.Pending.SetResult(Receipt(request,false));Reject<CustomerMasterWriteContentConflictException>(()=>pending.GetAwaiter().GetResult(),"Changed content was accepted as saved");
            foreach(var value in new[]{null,new CustomerMasterWriteReceipt(Guid.NewGuid(),"C2",2,DateTime.UtcNow,true,true)}){
                repo.Next();pending=service.SubmitAsync(request,Actor(),CancellationToken.None);repo.Pending.SetResult(value);Reject<CustomerMasterWriteOutcomeUnknownException>(()=>pending.GetAwaiter().GetResult(),"Unproven receipt was accepted");
            }
            repo.Next();var resolving=service.ResolveAsync(request,Actor("Administrator"),CancellationToken.None);repo.Status.SetResult(new CustomerMasterWriteStatus(Receipt(request,true,false)));
            Check(resolving.GetAwaiter().GetResult().Receipt.CurrentCustomerMatches==false,"Authorized owner could not inspect original-role request or current changes lost");
            Reject<UnauthorizedAccessException>(()=>service.SubmitAsync(request,Actor("Administrator"),CancellationToken.None),"Original request executed with changed audit role");
            repo.Next();resolving=service.ResolveAsync(request,Actor(),CancellationToken.None);granted=false;repo.Status.SetResult(new CustomerMasterWriteStatus(Receipt(request)));Reject<UnauthorizedAccessException>(()=>resolving.GetAwaiter().GetResult(),"Post-resolve authority loss ignored");granted=true;
            repo.Next();using(var token=new CancellationTokenSource()){resolving=service.ResolveAsync(request,Actor(),token.Token);token.Cancel();repo.Status.SetResult(new CustomerMasterWriteStatus(null));Reject<OperationCanceledException>(()=>resolving.GetAwaiter().GetResult(),"Read cancellation lost");}
            repo.Next();resolving=service.ResolveAsync(request,Actor(),CancellationToken.None);repo.Status.SetResult(null);Reject<InvalidOperationException>(()=>resolving.GetAwaiter().GetResult(),"Missing status became absence");
            var attempt=new CustomerMasterWriteAttempt(request,"source-A");Check(attempt.CanDiscard,"Fresh draft could not change");
            Reject<UnauthorizedAccessException>(()=>attempt.BeginSubmit("source-B",Actor(),request.CopyCustomer()),"Different source used");
            Reject<UnauthorizedAccessException>(()=>attempt.BeginSubmit("source-A",new OperationActor{UserName="other",UserRole="Manager"},request.CopyCustomer()),"Different owner used");
            var changed=request.CopyCustomer();changed.Address="edited";Reject<CustomerMasterWriteContentConflictException>(()=>attempt.BeginSubmit("source-A",Actor(),changed),"Edited fields reused identity");
            attempt.BeginSubmit("source-A",Actor(),request.CopyCustomer());Check(!attempt.CanDiscard,"Pending write discardable");
            Reject<InvalidOperationException>(()=>attempt.BeginSubmit("source-A",Actor(),request.CopyCustomer()),"Concurrent submit allowed");
            attempt.ObserveUnknown();Check(!attempt.CanDiscard&&attempt.Phase==CustomerMasterWritePhase.Unknown,"Unknown write discardable");
            Reject<InvalidOperationException>(()=>attempt.BeginSubmit("source-A",Actor(),request.CopyCustomer()),"Unknown write blindly retried");
            attempt.BeginResolve("source-A",Actor("Administrator"));attempt.ObserveStatus(new CustomerMasterWriteStatus(null));
            Check(attempt.Phase==CustomerMasterWritePhase.Ready&&!attempt.CanDiscard&&attempt.Request==request,"Absence allowed a new identity or discarded frozen content");
            attempt.BeginSubmit("source-A",Actor(),request.CopyCustomer());attempt.ObserveRejected();Check(attempt.Phase==CustomerMasterWritePhase.Unknown&&!attempt.CanDiscard,"Retry rejection forgot original unknown result");
            attempt.BeginResolve("source-A",Actor());attempt.ResolutionFailed();Check(attempt.Phase==CustomerMasterWritePhase.Unknown,"Read failure unlocked write");
            attempt.BeginResolve("source-A",Actor());attempt.ObserveStatus(new CustomerMasterWriteStatus(Receipt(request,false)));
            Check(attempt.Phase==CustomerMasterWritePhase.Conflict&&!attempt.CanDiscard,"Conflict considered accepted draft");
            attempt.BeginCancel("source-A",Actor());attempt.ObserveReceipt(Receipt(request));Check(attempt.Phase==CustomerMasterWritePhase.Accepted&&attempt.CanDiscard,"Closing a committed request erased its receipt");
            var canceled=new CustomerMasterWriteReceipt(request.RequestId,"",3,DateTime.UtcNow,true,false,CustomerMasterWriteOutcome.Canceled);
            attempt=new CustomerMasterWriteAttempt(request,"source-A");attempt.BeginCancel("source-A",Actor("Administrator"));attempt.ObserveReceipt(canceled);
            Check(attempt.Phase==CustomerMasterWritePhase.Canceled&&attempt.CanDiscard,"Sealed request could not close");
            repo.Next();pending=service.CancelAsync(request,Actor("Administrator"),CancellationToken.None);repo.Pending.SetResult(canceled);Check(pending.GetAwaiter().GetResult().Outcome==CustomerMasterWriteOutcome.Canceled&&repo.Cancel==1,"Cancel did not preserve sealed result/current role");
            attempt=new CustomerMasterWriteAttempt(request,"source-A");attempt.BeginSubmit("source-A",Actor(),request.CopyCustomer());attempt.ObserveRejected();Check(attempt.Phase==CustomerMasterWritePhase.Ready&&attempt.CanDiscard,"First known rejection froze an unsent draft");
            Reject<ArgumentException>(()=>new CustomerMasterWriteReceipt(Guid.Empty,"C1",1,DateTime.UtcNow,true,true),"Empty receipt identity allowed");
            Reject<ArgumentException>(()=>new CustomerMasterWriteReceipt(request.RequestId,"C1",1,DateTime.UtcNow,true,true,CustomerMasterWriteOutcome.Canceled),"Cancellation claimed customer mutation");
            Console.WriteLine("PASS customer master write candidate: "+n+" assertions; immutable input/actor, default deny, pre-send/post-result authority, unknown/cancel semantics, exact content/source/owner, no new identity after ambiguous absence, sealed cancellation/conflict; no SQL or UI.");return n;
        }catch(Exception){throw;}
    }
}
}

using System;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Application;
using PharmacyManagement.Domain;
namespace PharmacyManagement.CoreTests
{
internal static class JournalNativeAsyncTests
{
    static int checks;
    static void Check(bool ok,string message){++checks;if(!ok)throw new InvalidOperationException(message);}
    class Native : IAsyncJournalPostRepository
    {
        internal int SyncCalls,Calls;internal JournalPostRequest Request;internal OperationActor Actor;internal CancellationToken Token;
        internal TaskCompletionSource<JournalPostResult> Completion=new TaskCompletionSource<JournalPostResult>();
        public JournalPostResult Post(JournalPostRequest r,OperationActor a){++SyncCalls;return new JournalPostResult(7,false);}
        public Task<JournalPostResult> PostAsync(JournalPostRequest r,OperationActor a,CancellationToken t){++Calls;Request=r;Actor=a;Token=t;return Completion.Task;}
    }
    class Legacy : IJournalPostRepository
    {internal int Calls;public JournalPostResult Post(JournalPostRequest r,OperationActor a){++Calls;return new JournalPostResult(7,false);}}
    static JournalPostRequest Request()=>new JournalPostRequest(new DateTime(1900,1,1),"Manual",Guid.NewGuid().ToString("N"),"  بيان أصلي  ",new[]{new JournalLineValues("1000",1m,0m," debit ",null),new JournalLineValues("2000",0m,1m," credit ",null)});
    static OperationActor Actor()=>new OperationActor{UserName=" User ",UserRole=" Manager "};
    static void Denied<T>(Action action) where T:Exception {bool denied=false;try{action();}catch(T){denied=true;}Check(denied,"Expected "+typeof(T).Name);}
    internal static int Run()
    {
        checks=0;
            var native=new Native();bool permitted=true;int authorizations=0;
            var service=new JournalPostService(native,a=>{++authorizations;return permitted;});var actor=Actor();var request=Request();
            using(var cts=new CancellationTokenSource())
            {
                var task=service.PostAsync(request,actor,cts.Token);
                Check(!task.IsCompleted&&native.Calls==1&&native.SyncCalls==0,"Native pending write blocked or called sync repository.");
                Check(authorizations==2,"Two authorizations required before writer.");
                Check(native.Token==cts.Token&&native.Request.SourceId==request.SourceId,"Token or source identity changed.");
                Check(native.Request.Date==request.Date&&native.Request.Description=="بيان أصلي"&&native.Request.Lines[0].Debit==1m&&native.Request.Lines[1].Credit==1m,"Normalized financial request changed.");
                Check(native.Actor.UserName=="User"&&native.Actor.UserRole=="Manager"&&!ReferenceEquals(native.Actor,actor),"Authorized actor not cloned/trimmed.");
                actor.UserName="Changed";Check(native.Actor.UserName=="User","Caller mutated captured writer actor.");
                permitted=false;cts.Cancel();native.Completion.SetResult(new JournalPostResult(93,false));
                Check(task.GetAwaiter().GetResult().Id==93,"Late cancellation/context change lost known acceptance.");
                Check(authorizations==2&&native.SyncCalls==0,"Post-acceptance reauthorization or sync fallback.");
            }
            native=new Native();using(var cts=new CancellationTokenSource()){cts.Cancel();Denied<OperationCanceledException>(()=>new JournalPostService(native,a=>true).PostAsync(Request(),Actor(),cts.Token).GetAwaiter().GetResult());Check(native.Calls==0&&native.SyncCalls==0,"Precancelled request called repository.");}
            var legacy=new Legacy();Denied<InvalidOperationException>(()=>new JournalPostService(legacy,a=>true).PostAsync(Request(),Actor(),CancellationToken.None).GetAwaiter().GetResult());Check(legacy.Calls==0,"Legacy sync fallback ran.");
            native=new Native();authorizations=0;Denied<UnauthorizedAccessException>(()=>new JournalPostService(native,a=>++authorizations==1).PostAsync(Request(),Actor(),CancellationToken.None).GetAwaiter().GetResult());Check(native.Calls==0,"Revoked second authorization reached writer.");
            native=new Native();native.Completion.SetResult(null);Denied<InvalidOperationException>(()=>new JournalPostService(native,a=>true).PostAsync(Request(),Actor(),CancellationToken.None).GetAwaiter().GetResult());
            native=new Native();native.Completion.SetResult(new JournalPostResult(0,false));Denied<InvalidOperationException>(()=>new JournalPostService(native,a=>true).PostAsync(Request(),Actor(),CancellationToken.None).GetAwaiter().GetResult());
            native=new Native();Check(new JournalPostService(native,a=>true).Post(Request(),Actor()).Id==7&&native.SyncCalls==1&&native.Calls==0,"Original synchronous service changed.");
        return checks;
    }
}
}

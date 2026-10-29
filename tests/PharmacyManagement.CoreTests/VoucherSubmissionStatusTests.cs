using System;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Application;
namespace PharmacyManagement.CoreTests
{
    internal static class VoucherSubmissionStatusTests
    {
        private static int checks;
        private static void Check(bool value){if(!value)throw new Exception("Voucher status contract failed.");checks++;}
        private static void Throws<T>(Action action) where T:Exception{try{action();}catch(T){checks++;return;}throw new Exception("Expected "+typeof(T).Name);}
        private sealed class Probe:IVoucherSubmissionStatusRepository
        {
            internal int Calls;internal OperationActor Actor;internal CancellationToken Token;
            internal TaskCompletionSource<VoucherSubmissionStatus> Pending=new TaskCompletionSource<VoucherSubmissionStatus>();
            public Task<VoucherSubmissionStatus> FindAsync(VoucherSubmissionStatusRequest request,OperationActor actor,CancellationToken token)
            {Calls++;Actor=actor;Token=token;return Pending.Task;}
        }
        internal static int Run()
        {
            checks=0;var id=Guid.NewGuid();var actor=new OperationActor{UserName=" reader ",UserRole=" Manager "};
            Func<Guid?,string,VoucherSubmissionStatusRequest> request=(key,currency)=>new VoucherSubmissionStatusRequest(new VoucherPostRequest("ReceiptExternal",new DateTime(2026,1,1),"طرف","2000",10,"ملاحظة","1000",7,currency,key));
            Check(!new VoucherSubmissionStatus(0,0).IsPosted);Check(new VoucherSubmissionStatus(1,2,false).ContentMatches==false);
            foreach(var pair in new[]{new[]{-1,2},new[]{1,0},new[]{0,2}})Throws<ArgumentException>(()=>new VoucherSubmissionStatus(pair[0],pair[1]));
            Throws<ArgumentException>(()=>new VoucherSubmissionStatus(0,0,true));
            Throws<InvalidOperationException>(()=>VoucherSubmissionStatusService.PrepareRequest(request(null,"SAR")));
            Throws<InvalidOperationException>(()=>VoucherSubmissionStatusService.PrepareRequest(request(Guid.Empty,"SAR")));
            Throws<InvalidOperationException>(()=>VoucherSubmissionStatusService.PrepareRequest(request(id,null)));
            var repo=new Probe();Throws<UnauthorizedAccessException>(()=>new VoucherSubmissionStatusService(repo).FindAsync(request(id,"SAR"),actor,CancellationToken.None).GetAwaiter().GetResult());Check(repo.Calls==0);
            bool allowed=true;var service=new VoucherSubmissionStatusService(repo,a=>allowed&&a.UserName.Trim()=="reader"&&a.UserRole.Trim()=="Manager");
            var cts=new CancellationTokenSource();cts.Cancel();Throws<OperationCanceledException>(()=>service.FindAsync(request(id,"SAR"),actor,cts.Token).GetAwaiter().GetResult());Check(repo.Calls==0);
            cts=new CancellationTokenSource();var task=service.FindAsync(request(id,"SAR"),actor,cts.Token);actor.UserName="mutated";Check(repo.Actor.UserName=="reader"&&repo.Actor.UserRole=="Manager"&&repo.Token==cts.Token);
            repo.Pending.SetResult(new VoucherSubmissionStatus(4,5,true));Check(task.GetAwaiter().GetResult().ContentMatches==true);
            actor.UserName=" reader ";repo=new Probe();service=new VoucherSubmissionStatusService(repo,a=>allowed);task=service.FindAsync(request(id,"SAR"),actor,CancellationToken.None);allowed=false;repo.Pending.SetResult(new VoucherSubmissionStatus(4,5,true));Throws<UnauthorizedAccessException>(()=>task.GetAwaiter().GetResult());
            foreach(var result in new[]{null,new VoucherSubmissionStatus(4,5)}){repo=new Probe();service=new VoucherSubmissionStatusService(repo,a=>true);task=service.FindAsync(request(id,"SAR"),actor,CancellationToken.None);repo.Pending.SetResult(result);Throws<InvalidOperationException>(()=>task.GetAwaiter().GetResult());}
            repo=new Probe();service=new VoucherSubmissionStatusService(repo,a=>true);cts=new CancellationTokenSource();task=service.FindAsync(request(id,"SAR"),actor,cts.Token);cts.Cancel();repo.Pending.SetResult(new VoucherSubmissionStatus(4,5,true));Throws<OperationCanceledException>(()=>task.GetAwaiter().GetResult());
            Console.WriteLine("PASS: "+checks+" voucher status identity, payload-proof, actor snapshot, default denial, revocation and cancellation contracts; no SQL.");return checks;
        }
    }
}
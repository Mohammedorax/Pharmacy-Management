using System;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Application;
namespace PharmacyManagement.CoreTests {
 internal static class PurchaseDraftNumberTests {
  static int checks;
  static void Check(bool value){if(!value)throw new Exception("Number check "+checks);checks++;}
  static void Reject<T>(Action action) where T:Exception {try{action();}catch(T){checks++;return;}throw new Exception("Expected "+typeof(T).Name);}
  internal static int Run(){
   checks=0;var repo=new Repo();bool allowed=true;var actor=new OperationActor{UserName="manager",UserRole="Manager"};
   var service=new PurchaseDraftNumberService(repo,a=>allowed&&a.UserName=="manager"&&a.UserRole=="Manager");
   Reject<ArgumentOutOfRangeException>(()=>service.ReserveAsync((PurchaseDraftNumberKind)99,actor,CancellationToken.None).GetAwaiter().GetResult());Check(repo.Calls==0);
   Reject<UnauthorizedAccessException>(()=>service.ReserveAsync(PurchaseDraftNumberKind.Purchase,null,CancellationToken.None).GetAwaiter().GetResult());Check(repo.Calls==0);
   using(var c=new CancellationTokenSource()){c.Cancel();Reject<OperationCanceledException>(()=>service.ReserveAsync(PurchaseDraftNumberKind.Detail,actor,c.Token).GetAwaiter().GetResult());Check(repo.Calls==0);}
   var pending=service.ReserveAsync(PurchaseDraftNumberKind.Detail,actor,CancellationToken.None);actor.UserName="edited";repo.Pending.SetResult(17);Check(pending.GetAwaiter().GetResult()==17&&repo.Kind==PurchaseDraftNumberKind.Detail);
   actor.UserName="manager";repo.Pending=new TaskCompletionSource<long>();pending=service.ReserveAsync(PurchaseDraftNumberKind.Purchase,actor,CancellationToken.None);repo.Pending.SetResult(long.MaxValue);Check(pending.GetAwaiter().GetResult()==long.MaxValue&&repo.Kind==PurchaseDraftNumberKind.Purchase);
   foreach(long invalid in new[]{0L,-1L}){repo.Pending=new TaskCompletionSource<long>();pending=service.ReserveAsync(PurchaseDraftNumberKind.Detail,actor,CancellationToken.None);repo.Pending.SetResult(invalid);Reject<InvalidOperationException>(()=>pending.GetAwaiter().GetResult());}
   repo.Pending=new TaskCompletionSource<long>();pending=service.ReserveAsync(PurchaseDraftNumberKind.Detail,actor,CancellationToken.None);allowed=false;repo.Pending.SetResult(18);Reject<UnauthorizedAccessException>(()=>pending.GetAwaiter().GetResult());
   int calls=repo.Calls;Reject<UnauthorizedAccessException>(()=>service.ReserveAsync(PurchaseDraftNumberKind.Purchase,actor,CancellationToken.None).GetAwaiter().GetResult());Check(repo.Calls==calls);allowed=true;
   using(var c=new CancellationTokenSource()){
    repo.Pending=new TaskCompletionSource<long>();pending=service.ReserveAsync(PurchaseDraftNumberKind.Detail,actor,c.Token);Check(repo.Token==c.Token);c.Cancel();repo.Pending.SetException(new InvalidOperationException("late SQL fault"));Reject<OperationCanceledException>(()=>pending.GetAwaiter().GetResult());
   }
   repo.Pending=new TaskCompletionSource<long>();pending=service.ReserveAsync(PurchaseDraftNumberKind.Purchase,actor,CancellationToken.None);repo.Pending.SetException(new InvalidOperationException("SQL fault"));Reject<InvalidOperationException>(()=>pending.GetAwaiter().GetResult());
   Console.WriteLine("PASS purchase draft number contracts: "+checks+" checks; typed kinds, positive values, actor capture, authorization and cancellation priority.");return checks;
  }
  sealed class Repo:IPurchaseDraftNumberRepository {
   public int Calls;public PurchaseDraftNumberKind Kind;public CancellationToken Token;public TaskCompletionSource<long> Pending=new TaskCompletionSource<long>();
   public Task<long> ReserveAsync(PurchaseDraftNumberKind kind,CancellationToken token){Calls++;Kind=kind;Token=token;return Pending.Task;}
  }
 }
}

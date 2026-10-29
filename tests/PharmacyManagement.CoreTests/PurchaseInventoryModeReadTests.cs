using System;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Application;
namespace PharmacyManagement.CoreTests {
 internal static class PurchaseInventoryModeReadTests {
  static int checks;
  static void Check(bool value){if(!value)throw new Exception("Mode check "+checks);checks++;}
  static void Reject<T>(Action action) where T:Exception {try{action();}catch(T){checks++;return;}throw new Exception("Expected "+typeof(T).Name);}
  internal static int Run(){
   checks=0;var repo=new Repo();bool allowed=true;
   var actor=new OperationActor{UserName="manager",UserRole="Manager"};
   var service=new PurchaseInventoryModeReadService(repo,a=>allowed&&a.UserName=="manager"&&a.UserRole=="Manager");
   Reject<UnauthorizedAccessException>(()=>service.IsCommercialInventoryEnabledAsync(null,CancellationToken.None).GetAwaiter().GetResult());Check(repo.Calls==0);
   using(var c=new CancellationTokenSource()){c.Cancel();Reject<OperationCanceledException>(()=>service.IsCommercialInventoryEnabledAsync(actor,c.Token).GetAwaiter().GetResult());Check(repo.Calls==0);}
   var pending=service.IsCommercialInventoryEnabledAsync(actor,CancellationToken.None);actor.UserName="edited";repo.Pending.SetResult(true);Check(pending.GetAwaiter().GetResult());
   actor.UserName="manager";repo.Pending=new TaskCompletionSource<bool>();pending=service.IsCommercialInventoryEnabledAsync(actor,CancellationToken.None);repo.Pending.SetResult(false);Check(!pending.GetAwaiter().GetResult());
   repo.Pending=new TaskCompletionSource<bool>();pending=service.IsCommercialInventoryEnabledAsync(actor,CancellationToken.None);allowed=false;repo.Pending.SetResult(true);Reject<UnauthorizedAccessException>(()=>pending.GetAwaiter().GetResult());
   int calls=repo.Calls;Reject<UnauthorizedAccessException>(()=>service.IsCommercialInventoryEnabledAsync(actor,CancellationToken.None).GetAwaiter().GetResult());Check(repo.Calls==calls);allowed=true;
   using(var c=new CancellationTokenSource()){
    repo.Pending=new TaskCompletionSource<bool>();pending=service.IsCommercialInventoryEnabledAsync(actor,c.Token);Check(repo.Token==c.Token);c.Cancel();repo.Pending.SetException(new InvalidOperationException("late SQL fault"));Reject<OperationCanceledException>(()=>pending.GetAwaiter().GetResult());
   }
   repo.Pending=new TaskCompletionSource<bool>();pending=service.IsCommercialInventoryEnabledAsync(actor,CancellationToken.None);repo.Pending.SetException(new InvalidOperationException("SQL fault"));Reject<InvalidOperationException>(()=>pending.GetAwaiter().GetResult());
   Console.WriteLine("PASS purchase inventory mode contracts: "+checks+" checks; actor capture, pre/post guards, cancellation priority and explicit read failure.");return checks;
  }
  sealed class Repo:IPurchaseInventoryModeReadRepository {
   public int Calls;public CancellationToken Token;public TaskCompletionSource<bool> Pending=new TaskCompletionSource<bool>();
   public Task<bool> IsCommercialInventoryEnabledAsync(CancellationToken token){Calls++;Token=token;return Pending.Task;}
  }
 }
}

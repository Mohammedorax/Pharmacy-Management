using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Application;
namespace PharmacyManagement.CoreTests { internal static class PurchaseMedicineReadTests
{
 static int checks;
 static void Check(bool value){if(!value)throw new Exception("Check "+checks);checks++;}
 static void Reject<T>(Action action) where T:Exception{try{action();}catch(T){checks++;return;}throw new Exception("Expected "+typeof(T).Name);}
 internal static int Run(){checks=0;
 try{
 var repo=new Repo();bool allowed=true;
 var actor=new OperationActor{UserName="arabic",UserRole="Manager"};
 var service=new PurchaseMedicineReadService(repo,a=>allowed&&a.UserName=="arabic"&&a.UserRole=="Manager");
 Reject<UnauthorizedAccessException>(()=>service.GetNamesAsync("",null,CancellationToken.None).GetAwaiter().GetResult());
 Check(repo.Calls==0);
 var pending=service.GetNamesAsync(" مورد ",actor,CancellationToken.None);
 actor.UserName="edited";
 repo.Names.SetResult(new List<string>{"عربي & 001"});
 Check(pending.GetAwaiter().GetResult()[0]=="عربي & 001");
 actor.UserName="arabic";
 repo.Names=new TaskCompletionSource<IReadOnlyList<string>>();
 pending=service.GetNamesAsync("",actor,CancellationToken.None);
 allowed=false;repo.Names.SetResult(new List<string>());
 Reject<UnauthorizedAccessException>(()=>pending.GetAwaiter().GetResult());
 allowed=true;repo.Names=new TaskCompletionSource<IReadOnlyList<string>>();
 var cts=new CancellationTokenSource();
 pending=service.GetNamesAsync("",actor,cts.Token);
 cts.Cancel();repo.Names.SetResult(new List<string>());
 Reject<OperationCanceledException>(()=>pending.GetAwaiter().GetResult());
 Check(repo.Token==cts.Token);
 repo.Names=new TaskCompletionSource<IReadOnlyList<string>>();
 pending=service.GetNamesAsync("",actor,CancellationToken.None);
 repo.Names.SetResult(null);
 Reject<InvalidOperationException>(()=>pending.GetAwaiter().GetResult());
 var source=new PurchaseMedicineSnapshot{MedicineName="عربي",SupplierName="مورد",StockQuantity=-2,BuyingPrice=7};
 repo.Detail.SetResult(source);
 var row=service.FindAsync(" عربي ",actor,CancellationToken.None).GetAwaiter().GetResult();
 source.StockQuantity=100;source.MedicineName="changed";
 Check(row.StockQuantity==-2&&row.BuyingPrice==7&&row.MedicineName=="عربي");
 Reject<ArgumentException>(()=>service.FindAsync("",actor,CancellationToken.None).GetAwaiter().GetResult());
 foreach(bool detail in new[]{false,true}){
  repo=new Repo();service=new PurchaseMedicineReadService(repo,a=>true);
  using(var cancel=new CancellationTokenSource()){
   Task request=detail?(Task)service.FindAsync("عربي",actor,cancel.Token):service.GetNamesAsync("",actor,cancel.Token);
   cancel.Cancel();
   if(detail)repo.Detail.SetException(new InvalidOperationException("Provider failure while cancellation pending."));
   else repo.Names.SetException(new InvalidOperationException("Provider failure while cancellation pending."));
   Reject<OperationCanceledException>(()=>request.GetAwaiter().GetResult());
  }
 }
 var barcodeRepo=new BarcodeRepo();
 var barcodeService=new MedicineSearchService(barcodeRepo,canReadBarcode:a=>true);
 using(var cancel=new CancellationTokenSource()){
  var request=barcodeService.FindByBarcodeAsync("001",actor,cancel.Token);
  cancel.Cancel();barcodeRepo.Pending.SetException(new InvalidOperationException("Provider failure while cancellation pending."));
  Reject<OperationCanceledException>(()=>request.GetAwaiter().GetResult());
 }
 Console.WriteLine("PASS dependent purchase medicine reads: "+checks+" checks; no SQL.");return checks;
 }catch(Exception e){throw new Exception("Purchase read contract failed.",e);}
 }
 class BarcodeRepo:IMedicineSearchRepository{
  public TaskCompletionSource<MedicineMatch> Pending=new TaskCompletionSource<MedicineMatch>();
  public Task<MedicineMatch> FindByBarcodeAsync(string barcode,CancellationToken token){return Pending.Task;}
  public MedicineMatch FindByBarcode(string barcode){throw new Exception("Unexpected sync.");}
  public Task<IReadOnlyList<MedicineSearchItem>> SearchAsync(string query,CancellationToken token){throw new Exception("Unexpected search.");}
 }
 class Repo:IPurchaseMedicineReadRepository{
 public int Calls;public CancellationToken Token;
 public TaskCompletionSource<IReadOnlyList<string>> Names=new TaskCompletionSource<IReadOnlyList<string>>();
 public TaskCompletionSource<PurchaseMedicineSnapshot> Detail=new TaskCompletionSource<PurchaseMedicineSnapshot>();
 public Task<IReadOnlyList<string>> GetNamesAsync(string supplier,CancellationToken token){Calls++;Token=token;return Names.Task;}
 public Task<PurchaseMedicineSnapshot> FindAsync(string name,CancellationToken token){Calls++;Token=token;return Detail.Task;}
 }
}

}


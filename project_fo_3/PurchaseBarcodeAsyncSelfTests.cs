using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using PharmacyManagement.Application;
namespace project_fo_3
{
 public static class PurchaseBarcodeAsyncSelfTests
 {
  static int checks;
  static void Check(bool value){if(!value)throw new Exception("UI check "+checks);checks++;}
  static object Field(Purchases f,string name)=>typeof(Purchases).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(f);
  static void Pump(Func<bool> ready){var end=DateTime.UtcNow.AddSeconds(5);while(!ready()&&DateTime.UtcNow<end){Application.DoEvents();Thread.Sleep(5);}Check(ready());}
  public static void Run()
  {
   string original=Properties.Setings.Default.ThemeMode;
   try{foreach(string theme in new[]{"Light","Dark"})foreach(bool english in new[]{false,true}){
    Properties.Setings.Default.ThemeMode=theme;RunCase(english);
   }}
   finally{Properties.Setings.Default.ThemeMode=original;}
  }
  private static void RunCase(bool english)
  {
   using(var f=new Purchases(true,english)){
    f.StartPosition=FormStartPosition.Manual;f.Location=new System.Drawing.Point(-32000,-32000);f.Opacity=0;
    f.Show();Application.DoEvents();
    typeof(Purchases).GetField("previewMode",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(f,false);
    typeof(Purchases).GetField("isPurchaseSaved",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(f,false);
    var barcode=(Control)Field(f,"txtBarcode");
    var names=(ComboBox)Field(f,"MediComboCb");
    IntPtr actualComboHandle=names.Handle;
    Check(actualComboHandle!=IntPtr.Zero);
    var supplier=(Control)Field(f,"cmbSupplier");
    string company=supplier.Text;
    var repo=new Reads();var search=new Search();
    var actor=new OperationActor{UserName="owned",UserRole="Manager"};
    f.BarcodeSessionFactory=()=>new PurchaseBarcodeReadSession(search,new PurchaseMedicineReadService(repo,a=>true),actor,()=>true);
    barcode.Text="001";
    typeof(Purchases).GetMethod("txtBarcode_KeyDown",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(f,new object[]{barcode,new KeyEventArgs(Keys.Enter)});
    Check(Field(f,"barcodeReadCancellation")!=null);
    bool responsive=false;f.BeginInvoke(new Action(()=>responsive=true));Pump(()=>responsive);
    Check((bool)typeof(Purchases).GetMethod("RejectPendingBarcodeAction",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(f,null));
    Check(((Label)Field(f,"lblStockInfo")).Text.Contains(english?"Barcode lookup":"جاري البحث"));
    barcode.Text="002";Check(search.Token.IsCancellationRequested);
    search.Completion.SetResult(new MedicineMatch("late",company));
    Pump(()=>Field(f,"barcodeReadCancellation")==null);
    Check(barcode.Text=="002"&&names.Text!="late");
    search=new Search();repo=new Reads();
    f.BarcodeSessionFactory=()=>new PurchaseBarcodeReadSession(search,new PurchaseMedicineReadService(repo,a=>true),actor,()=>true);
    barcode.Text="003";
    typeof(Purchases).GetMethod("txtBarcode_KeyDown",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(f,new object[]{barcode,new KeyEventArgs(Keys.Enter)});
    search.Completion.SetResult(new MedicineMatch("عربي 001",company));
    Pump(()=>repo.NameCalls==1);Check(Field(f,"barcodeReadCancellation")!=null);
    repo.Names.SetResult(new List<string>{"عربي 001"});
    Pump(()=>repo.DetailCalls==1);
    repo.Detail.SetResult(new PurchaseMedicineSnapshot{MedicineName="عربي 001",SupplierName=company,StockQuantity=17,BuyingPrice=5});
    Pump(()=>Field(f,"barcodeReadCancellation")==null);
    Check(barcode.Text==""&&names.Text=="عربي 001");
    Pump(()=>names.RightToLeft==RightToLeft.Yes);
    Check(names.SelectedItem!=null&&names.SelectedItem.ToString()=="عربي 001"&&names.Text=="عربي 001");
    Check(((Label)Field(f,"lblStockInfo")).Text.Contains("17"));
    Check(((Label)Field(f,"lblStockInfo")).Text.Contains(english?"Current stock":"المخزون الحالي"));
    typeof(Purchases).GetField("applyingBarcodeResult",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(f,true);
    names.Items.Add("Latin 001");names.SelectedIndex=1;
    typeof(Purchases).GetField("applyingBarcodeResult",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(f,false);
    search=new Search();repo=new Reads();
    f.BarcodeSessionFactory=()=>new PurchaseBarcodeReadSession(search,new PurchaseMedicineReadService(repo,a=>true),actor,()=>true);
    barcode.Text="direction-race";
    typeof(Purchases).GetMethod("txtBarcode_KeyDown",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(f,new object[]{barcode,new KeyEventArgs(Keys.Enter)});
    Pump(()=>names.RightToLeft==RightToLeft.No);
    Check(names.Text=="Latin 001"&&Field(f,"barcodeReadCancellation")!=null&&!search.Token.IsCancellationRequested);
    barcode.Text="direction-cancel";search.Completion.SetResult(null);
    Pump(()=>Field(f,"barcodeReadCancellation")==null);
    typeof(Purchases).GetField("applyingBarcodeResult",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(f,true);
    names.SelectedIndex=0;
    typeof(Purchases).GetField("applyingBarcodeResult",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(f,false);
    Pump(()=>names.RightToLeft==RightToLeft.Yes);
    foreach(string action in new[]{"btnAddToPurchase_Click","btnConfirmPurchase_Click","btnSaveOnly_Click","btnSaveAndPrint_Click","btnSaveAndPrintA4_Click"}){
     search=new Search();repo=new Reads();
     f.BarcodeSessionFactory=()=>new PurchaseBarcodeReadSession(search,new PurchaseMedicineReadService(repo,a=>true),actor,()=>true);
     barcode.Text="pending-"+action;
     typeof(Purchases).GetMethod("txtBarcode_KeyDown",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(f,new object[]{barcode,new KeyEventArgs(Keys.Enter)});
     int rows=((DataGridView)Field(f,"PurchasesGV")).Rows.Count;
     typeof(Purchases).GetMethod(action,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(f,new object[]{f,EventArgs.Empty});
     Check(((DataGridView)Field(f,"PurchasesGV")).Rows.Count==rows&&!(bool)Field(f,"isPurchaseSaved"));
     Check(!(bool)typeof(Purchases).GetMethod("SavePurchaseToDatabase",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(f,null));
     barcode.Text="cancel";
     search.Completion.SetResult(null);
     Pump(()=>Field(f,"barcodeReadCancellation")==null);
    }
    var quantity=(Control)Field(f,"txtQuantity");
    repo=new Reads();search=new Search();
    f.BarcodeSessionFactory=()=>new PurchaseBarcodeReadSession(search,new PurchaseMedicineReadService(repo,a=>true),actor,()=>true);
    quantity.Text="2";
    Check((bool)Field(f,"quantityReadPending"));
    Pump(()=>repo.DetailCalls==1);
    Check(!(bool)typeof(Purchases).GetMethod("SavePurchaseToDatabase",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(f,null));
    bool quantityResponsive=false;f.BeginInvoke(new Action(()=>quantityResponsive=true));Pump(()=>quantityResponsive);
    var quantityToken=repo.Token;
    quantity.Text="";Check(quantityToken.IsCancellationRequested);
    string statusBefore=((Label)Field(f,"lblStockInfo")).Text;
    repo.Detail.SetResult(new PurchaseMedicineSnapshot{MedicineName="عربي 001",SupplierName=company,StockQuantity=99,BuyingPrice=5});
    Pump(()=>!(bool)Field(f,"quantityReadPending"));
    Check(((Label)Field(f,"lblStockInfo")).Text==statusBefore);
    repo=new Reads();
    f.BarcodeSessionFactory=()=>new PurchaseBarcodeReadSession(search,new PurchaseMedicineReadService(repo,a=>true),actor,()=>true);
    quantity.Text="171";Pump(()=>repo.DetailCalls==1);
    repo.Detail.SetResult(new PurchaseMedicineSnapshot{MedicineName="عربي 001",SupplierName=company,StockQuantity=17,BuyingPrice=5});
    Pump(()=>!(bool)Field(f,"quantityReadPending"));
    Check(((Label)Field(f,"lblStockInfo")).Text.Contains(english?"large":"كبيرة"));
    repo=new Reads();
    f.BarcodeSessionFactory=()=>new PurchaseBarcodeReadSession(search,new PurchaseMedicineReadService(repo,a=>true),actor,()=>true);
    quantity.Text="2";Pump(()=>repo.DetailCalls==1);
    repo.Detail.SetResult(new PurchaseMedicineSnapshot{MedicineName="عربي 001",SupplierName=company,StockQuantity=int.MaxValue,BuyingPrice=5});
    Pump(()=>!(bool)Field(f,"quantityReadPending"));
    Check(!((Label)Field(f,"lblStockInfo")).Text.Contains(english?"large":"كبيرة"));
    Check(((Label)Field(f,"lblStockInfo")).Text.Contains(int.MaxValue.ToString()));
    quantity.Text="";
    bool current=true;
    search=new Search();repo=new Reads();
    f.BarcodeSessionFactory=()=>new PurchaseBarcodeReadSession(search,new PurchaseMedicineReadService(repo,a=>current),actor,()=>current);
    barcode.Text="context-old";
    typeof(Purchases).GetMethod("txtBarcode_KeyDown",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(f,new object[]{barcode,new KeyEventArgs(Keys.Enter)});
    current=false;search.Completion.SetResult(new MedicineMatch("stale-context",company));
    Pump(()=>Field(f,"barcodeReadCancellation")==null);
    Check(barcode.Text=="context-old"&&names.Text!="stale-context"&&repo.NameCalls==0);
    current=true;search=new Search();repo=new Reads();
    f.BarcodeSessionFactory=()=>new PurchaseBarcodeReadSession(search,new PurchaseMedicineReadService(repo,a=>current),actor,()=>current);
    barcode.Text="closing";
    typeof(Purchases).GetMethod("txtBarcode_KeyDown",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(f,new object[]{barcode,new KeyEventArgs(Keys.Enter)});
    f.Dispose();Check(search.Token.IsCancellationRequested);
    search.Completion.SetResult(new MedicineMatch("closed",company));
    for(int i=0;i<20;i++){Application.DoEvents();Thread.Sleep(5);}
    Check(f.IsDisposed&&repo.NameCalls==0);
   }
   Console.WriteLine("PASS purchase barcode async UI: "+checks+" checks; owned offscreen dialog, no SQL.");
  }
  class Search:IMedicineSearchService{
   public CancellationToken Token;public TaskCompletionSource<MedicineMatch> Completion=new TaskCompletionSource<MedicineMatch>();
   public Task<MedicineMatch> FindByBarcodeAsync(string value,OperationActor actor,CancellationToken token){Token=token;return Completion.Task;}
   public MedicineMatch FindByBarcode(string value,OperationActor actor){throw new Exception("Sync barcode.");}
   public Task<IReadOnlyList<MedicineSearchItem>> SearchAsync(string value,OperationActor actor,CancellationToken token){throw new Exception("Unexpected search.");}
  }
  class Reads:IPurchaseMedicineReadRepository{
   public int NameCalls,DetailCalls; public CancellationToken Token;
   public TaskCompletionSource<IReadOnlyList<string>> Names=new TaskCompletionSource<IReadOnlyList<string>>();
   public TaskCompletionSource<PurchaseMedicineSnapshot> Detail=new TaskCompletionSource<PurchaseMedicineSnapshot>();
   public Task<IReadOnlyList<string>> GetNamesAsync(string value,CancellationToken token){NameCalls++;return Names.Task;}
   public Task<PurchaseMedicineSnapshot> FindAsync(string value,CancellationToken token){Token=token;DetailCalls++;return Detail.Task;}
  }
 }
}











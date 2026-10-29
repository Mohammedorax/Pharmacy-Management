using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using PharmacyManagement.Application;
namespace project_fo_3 {
 internal static class InvoiceInventoryAlertStatusSelfTests {
  static int checks;
  static void Check(bool value){if(!value)throw new Exception("Invoice alert UI check "+checks);checks++;}
  sealed class Context:SynchronizationContext {
   readonly Control host;internal Context(Control h){host=h;}
   public override void Post(SendOrPostCallback callback,object state){host.BeginInvoke(new Action(()=>{var prior=Current;SetSynchronizationContext(this);try{callback(state);}finally{SetSynchronizationContext(prior);}}));}
  }
  sealed class Read:IInventoryAlertSessionReadService {
   internal readonly TaskCompletionSource<OperationalReportResult> Pending=new TaskCompletionSource<OperationalReportResult>();
   internal InventoryAlertRequest Request;internal bool Cancelled;
   public Task<OperationalReportResult> GetAlertsAsync(InventoryAlertRequest request,OperationActor actor,CancellationToken cancellation){
    Request=request;cancellation.Register(()=>{Cancelled=true;Pending.TrySetCanceled();});return Pending.Task;
   }
  }
  static OperationalReportResult Result(bool empty=false)=>new OperationalReportResult(new[]{new OperationalReportColumn("نوع التنبيه",typeof(string))},
   empty?new object[0][]:new[]{new object[]{"مخزون منخفض"},new object[]{"قريب الانتهاء"},new object[]{"منتهي"}});
  static void Pump(Task task,Context context){
   var end=DateTime.UtcNow.AddSeconds(6);while(!task.IsCompleted&&DateTime.UtcNow<end){Application.DoEvents();SynchronizationContext.SetSynchronizationContext(context);Thread.Sleep(5);}
   Check(task.IsCompleted);task.GetAwaiter().GetResult();
  }
  static void VerifyOrdering(Context context,bool english){
   foreach(bool oldFails in new[]{false,true})foreach(bool latestFirst in new[]{false,true})foreach(bool sameReference in new[]{false,true})
   using(var form=new Form{StartPosition=FormStartPosition.Manual,Location=new System.Drawing.Point(-32000,-32000),Opacity=0,ShowInTaskbar=false,RightToLeft=english?RightToLeft.No:RightToLeft.Yes})
   using(var status=new Label{Dock=DockStyle.Top})using(var lines=new DataGridView{Dock=DockStyle.Fill,AllowUserToAddRows=false}){
    form.Controls.Add(lines);form.Controls.Add(status);lines.Columns.Add("MedName","MedName");lines.Rows.Add("المورد");form.Show();Application.DoEvents();SynchronizationContext.SetSynchronizationContext(context);
    var actor=new OperationActor{UserName="owned-ordering",UserRole="Manager"};var oldRead=new Read();var latestRead=new Read();
    var oldTask=InvoiceInventoryAlertPresenter.RefreshAsync(status,lines,"فاتورة-001",()=>true,new InventoryAlertReadSession(oldRead,actor,()=>true));
    var latestTask=InvoiceInventoryAlertPresenter.RefreshAsync(status,lines,sameReference?"فاتورة-001":"فاتورة-002",()=>true,new InventoryAlertReadSession(latestRead,actor,()=>true));
    Check(!oldTask.IsCompleted&&!latestTask.IsCompleted);string pending=status.Text;
    Action completeOld=()=>{if(oldFails)oldRead.Pending.SetException(new IOException("Owned old alert failure"));else oldRead.Pending.SetResult(Result());};
    if(latestFirst){latestRead.Pending.SetResult(Result(true));Pump(latestTask,context);completeOld();Pump(oldTask,context);}
    else{completeOld();Pump(oldTask,context);Check(status.Text==pending);latestRead.Pending.SetResult(Result(true));Pump(latestTask,context);}
    Check(status.Text.Contains(english?"No alerts":"لا تنبيهات"));
   }
  }
  internal static void Run(){
   checks=0;string theme=Properties.Setings.Default.ThemeMode;var prior=SynchronizationContext.Current;
   using(var host=new Form{StartPosition=FormStartPosition.Manual,Location=new System.Drawing.Point(-32000,-32000),Opacity=0,ShowInTaskbar=false}){
    host.Show();Application.DoEvents();var context=new Context(host);SynchronizationContext.SetSynchronizationContext(context);Control.CheckForIllegalCrossThreadCalls=true;
    try{
     foreach(string currentTheme in new[]{"Light","Dark"})foreach(bool english in new[]{false,true}){
      Properties.Setings.Default.ThemeMode=currentTheme;
      foreach(string scenario in new[]{"alerts","empty","stale-success","stale-failure","other-invoice","disposed","failure"}){
       using(var form=new Form{StartPosition=FormStartPosition.Manual,Location=new System.Drawing.Point(-32000,-32000),Opacity=0,ShowInTaskbar=false,RightToLeft=english?RightToLeft.No:RightToLeft.Yes})
       using(var status=new Label{Dock=DockStyle.Top})
       using(var lines=new DataGridView{Dock=DockStyle.Fill,AllowUserToAddRows=false}){
        form.Controls.Add(lines);form.Controls.Add(status);lines.Columns.Add("MedName","MedName");lines.Rows.Add("المورد");form.Show();Application.DoEvents();SynchronizationContext.SetSynchronizationContext(context);
        bool current=true,invoice=true;var read=new Read();
        var session=new InventoryAlertReadSession(read,new OperationActor{UserName="owned-alert",UserRole="Manager"},()=>current);
        var task=InvoiceInventoryAlertPresenter.RefreshAsync(status,lines,"المورد-001",()=>invoice,session);
        Check(!task.IsCompleted);Check(read.Request.AffectedMedicines.Single()=="المورد");Check(status.Text.Contains("المورد-001"));
        string pending=status.Text;
        Check(pending.Contains(english?"Checking stock":"جارٍ فحص المخزون"));
        if(scenario.StartsWith("stale"))current=false;
        if(scenario=="other-invoice")invoice=false;
        if(scenario=="disposed"){form.Dispose();Pump(task,context);Check(read.Cancelled);continue;}
        if(scenario=="failure"||scenario=="stale-failure")read.Pending.SetException(new IOException("Owned alert read failure"));
        else read.Pending.SetResult(Result(scenario=="empty"));
        Pump(task,context);
        if(scenario.StartsWith("stale")||scenario=="other-invoice")Check(status.Text==pending);
        else if(scenario=="alerts"){
         Check(status.Text.Contains(english?"Low stock: 1":"مخزون منخفض: 1"));Check(status.Text.Contains(english?"expired: 1":"منتهي: 1"));
        }else if(scenario=="empty")Check(status.Text.Contains(english?"No alerts":"لا تنبيهات"));
        else Check(status.Text.Contains(english?"Support code:":"رمز الدعم:"));
       }
      }
      VerifyOrdering(context,english);
     }
    }finally{Properties.Setings.Default.ThemeMode=theme;SynchronizationContext.SetSynchronizationContext(prior);}
   }
   Console.WriteLine("PASS invoice inventory alert status: "+checks+" owned UI checks; RTL/LTR and themes, raw medicine/reference retained, counts/empty/failure language, stale context/invoice suppression, dispose cancellation and latest-request ordering; no SQL.");
  }
 }
}
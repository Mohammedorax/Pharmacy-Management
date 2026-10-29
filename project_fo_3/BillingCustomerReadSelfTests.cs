using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;
using PharmacyManagement.Application;
namespace project_fo_3
{
internal static class BillingCustomerReadSelfTests
{
    static BindingFlags members=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
    static object originalSettings;static PropertyInfo themeProperty;static string originalTheme;
    static Assembly native;static Type sessionType;static int checks,dialogs;
    static object Call(object obj,string method,params object[] args)
    {try{return obj.GetType().GetMethods(members).Single(m=>m.Name==method&&m.GetParameters().Length==args.Length).Invoke(obj,args);}catch(TargetInvocationException e){throw e.InnerException??e;}}
    static object Field(object obj,string name)=>obj.GetType().GetField(name,members).GetValue(obj);
    static void Field(object obj,string name,object value)=>obj.GetType().GetField(name,members).SetValue(obj,value);
    static void Check(bool condition,string text){checks++;if(!condition)throw new Exception(text);}
    static Task<bool> Task(object form,string name,params object[] args)=>(Task<bool>)Call(form,name,args);
    static bool Pump(Task<bool> task)
    {var until=DateTime.UtcNow.AddSeconds(10);while(!task.IsCompleted&&DateTime.UtcNow<until){Application.DoEvents();Thread.Sleep(2);}Check(task.IsCompleted,"Owned UI timeout");return task.GetAwaiter().GetResult();}
    static void Tick(){for(int i=0;i<8;i++){Application.DoEvents();Thread.Sleep(2);}}
    sealed class ReadCall
    {public CustomerReferenceReadRequest Request;public CancellationToken Token;public TaskCompletionSource<CustomerReferenceReadResult> Pending=new TaskCompletionSource<CustomerReferenceReadResult>();}
    sealed class References:ICustomerReferenceReadRepository
    {
        public List<ReadCall> Calls=new List<ReadCall>();
        public Task<CustomerReferenceReadResult> ReadAsync(CustomerReferenceReadRequest request,CancellationToken token)
        {var call=new ReadCall{Request=request,Token=token};Calls.Add(call);return call.Pending.Task;}
        public void Complete(int index,CustomerReferenceSnapshot customer=null,params string[] values)
        {var call=Calls[index];call.Pending.SetResult(new CustomerReferenceReadResult(call.Request.Kind,values,customer));}
    }
    sealed class CreditCall
    {public SalesCustomerCreditReadRequest Request;public CancellationToken Token;public TaskCompletionSource<SalesCustomerCreditSnapshot> Pending=new TaskCompletionSource<SalesCustomerCreditSnapshot>();}
    sealed class Credits:ISalesCustomerCreditReadRepository
    {
        public List<CreditCall> Calls=new List<CreditCall>();
        public Task<SalesCustomerCreditSnapshot> ReadAsync(SalesCustomerCreditReadRequest request,CancellationToken token)
        {var call=new CreditCall{Request=request,Token=token};Calls.Add(call);return call.Pending.Task;}
        public void Complete(int index,decimal balance){var call=Calls[index];call.Pending.SetResult(new SalesCustomerCreditSnapshot(call.Request.CustomerId,balance));}
    }
    static CustomerReferenceSnapshot Customer(string id,string name="Arabic Pharma",string phone="00123")
        =>new CustomerReferenceSnapshot(id,name,phone," عنوان A&B عربي ",12.3456m);
    sealed class Fixture:IDisposable
    {
        public Form Form;public References References=new References();public Credits Credits=new Credits();
        public bool Current=true,Allowed=true;
        public Fixture(bool english)
        {
            var billing=native.GetType("project_fo_3.Billing",true);
            Form=(Form)billing.GetConstructors(members).Single(c=>c.GetParameters().Length==6).Invoke(new object[]{false,english,null,new Func<string>(()=>"YER"),null,null});
            EventHandler load=(EventHandler)Delegate.CreateDelegate(typeof(EventHandler),Form,billing.GetMethod("Billing_Load",members));
            Form.Load-=load;
            Fresh();
            Form.StartPosition=FormStartPosition.Manual;Form.Location=new Point(-28000,-28000);Form.Opacity=0;Form.ShowInTaskbar=false;Form.Show();Tick();dialogs++;
        }
        public void Fresh()
        {
            Current=true;
            var reference=new CustomerReferenceReadService(References,a=>Allowed&&a.UserName=="owner"&&a.UserRole=="Manager");
            var credit=new SalesCustomerCreditReadService(Credits,a=>Allowed&&a.UserName=="owner"&&a.UserRole=="Manager");
            var actor=new OperationActor{UserName="owner",UserRole="Manager"};
            var session=sessionType.GetConstructors(members).Single(c=>c.GetParameters().Length==4).Invoke(new object[]{reference,credit,actor,new Func<bool>(()=>Current)});
            var property=Form.GetType().GetProperty("CustomerReadSessionFactory",members);
            property.SetValue(Form,Expression.Lambda(property.PropertyType,Expression.Constant(session,sessionType)).Compile());
        }
        public Task<bool> Select(string value,CustomerReferenceKind kind=CustomerReferenceKind.ByName)=>Task(Form,"SelectCustomerAsync",kind,value);
        public bool Ready=>(bool)Call(Form,"CanUseCustomerSelection");
        public string Id=>(string)Field(Form,"_currentCustomerId");
        public void Dispose(){Form.Close();Form.Dispose();Tick();}
    }
    static void Cases(bool english)
    {
        using(var f=new Fixture(english))
        {
            Check(f.References.Calls.Count==0&&f.Credits.Calls.Count==0,"Constructor read source");
            var names=(ComboBox)Field(f.Form,"cmbCustomerName");var phones=(ComboBox)Field(f.Form,"cmbCustomerPhone");
            int originalNames=names.Items.Count;
            var options=Task(f.Form,"LoadCustomerOptionsAsync");
            Check(!options.IsCompleted&&f.References.Calls.Count==2,"Options did not await both sources");
            f.References.Complete(0,null,"A","B");Tick();Check(!options.IsCompleted&&names.Items.Count==originalNames,"Partial options published");
            f.References.Complete(1,null,"000001","000002");Check(Pump(options),"Options rejected");
            Check(names.Items[0].ToString()=="عميل عام"&&phones.Items[0].ToString()=="000000000"&&phones.DropDownStyle==ComboBoxStyle.DropDownList,"General choice/closed phone contract lost");
            var first=f.Select("A");
            Check(((Control)Field(f.Form,"lblStockStatus")).Text==(english?"Loading customer details…":"جارٍ تحميل بيانات العميل…"),"Customer pending presentation mismatch");Check(!first.IsCompleted&&!f.Ready&&!((Control)Field(f.Form,"btnSaveOnly")).Enabled,"Pending customer ready/save enabled");
            Check(!Pump(Task(f.Form,"SaveInvoiceToDatabaseAsync")),"Pending customer reached asynchronous write path");
            var second=f.Select("B");Check(f.References.Calls[2].Token.IsCancellationRequested,"Old lookup not canceled");
            f.References.Complete(2,Customer("A","A","000001"));Check(!Pump(first)&&f.Id!="A","Old lookup applied");
            f.References.Complete(3,Customer("B","B","000002"));Tick();
            Check(f.Credits.Calls.Count==1&&!second.IsCompleted&&!f.Ready,"Credit did not gate selection");

            Check(((Control)Field(f.Form,"lblStockStatus")).Text==(english?"Reading customer credit…":"جارٍ قراءة رصيد العميل…"),"Credit pending presentation mismatch");
            Check(!Pump(Task(f.Form,"SaveInvoiceToDatabaseAsync")),"Pending credit reached asynchronous write path");
            f.Credits.Complete(0,11.50m);Check(Pump(second)&&f.Ready&&f.Id=="B"&&((Control)Field(f.Form,"btnSaveOnly")).Enabled,"Current customer not accepted/save disabled");
            Check(names.Text=="B"&&phones.Text=="000002"&&f.References.Calls.Count==4,"Programmatic selection recursively read source");
            Check((decimal)Field(f.Form,"_displayedCustomerCreditBalance")==11.50m,"Credit value changed");
            Check(((Control)Field(f.Form,"txtCustomerAddress")).Text==" عنوان A&B عربي ","Raw customer address changed");
            Field(f.Form,"_isInvoiceSaved",true);Check((bool)Call(f.Form,"CanProceedWithSalesSave"),"Known accepted invoice unexpectedly requires a fresh customer selection");Field(f.Form,"_isInvoiceSaved",false);
            var oldCredit=f.Select("A");f.References.Complete(4,Customer("A","A","000001"));Tick();
            Check(f.Credits.Calls.Count==2,"Second credit read missing");
            var replacement=f.Select("B");Check(f.Credits.Calls[1].Token.IsCancellationRequested,"Old credit token not canceled");
            f.Credits.Complete(1,999m);Check(!Pump(oldCredit)&&(decimal)Field(f.Form,"_displayedCustomerCreditBalance")==0m,"Late credit applied");
            f.References.Complete(5,Customer("B","B","000002"));Tick();f.Credits.Complete(2,-0.125m);
            Check(Pump(replacement)&&f.Ready&&(decimal)Field(f.Form,"_displayedCustomerCreditBalance")==-0.125m,"Negative/raw credit changed");
            var lateOptions=Task(f.Form,"LoadCustomerOptionsAsync");
            var latest=f.Select("Latest");
            f.References.Complete(8,Customer("C","Latest","000003"));Tick();f.Credits.Complete(3,5.75m);Check(Pump(latest),"Concurrent latest lookup failed");
            f.References.Complete(6,null,"A","B");f.References.Complete(7,null,"000001","000002");Check(Pump(lateOptions),"Late options failed");
            Check(f.Id=="C"&&names.Text=="Latest"&&phones.Text=="000003","Late options replaced selected values");
            var withdrawn=f.Select("private");f.Current=false;f.References.Complete(9,Customer("private"));
            Check(!Pump(withdrawn)&&!f.Ready&&f.Id!="private","Withdrawn context delivered private customer");
            f.Current=true;Check(!f.Ready,"Context reversal revived selection");
            f.Fresh();var deniedBefore=f.References.Calls.Count;f.Allowed=false;Check(!Pump(f.Select("denied"))&&f.References.Calls.Count==deniedBefore&&!f.Ready,"Denied identity reached repository");
            f.Allowed=true;f.Fresh();var general=f.Select("عميل عام");f.References.Complete(10);
            Check(Pump(general)&&f.Ready&&f.Id==string.Empty&&names.Text=="عميل عام"&&phones.Text=="000000000","General customer fallback lost");
            var failed=f.Select("fail");f.References.Calls[11].Pending.SetException(new InvalidOperationException("Owned simulated source failure"));
            Check(!Pump(failed)&&!f.Ready,"Read failure kept save ready");
            Check(((Control)Field(f.Form,"lblStockStatus")).Text==(english?"Could not load the customer. Choose it again.":"تعذر تحميل العميل. أعد اختياره."),"Failure presentation mismatch");
            var retry=f.Select("retry");f.References.Complete(12,Customer("R","retry","000004"));Tick();f.Credits.Complete(4,0m);
            Check(Pump(retry)&&f.Ready,"Recovery failed");
            var missing=f.Select("missing");f.References.Complete(13);Check(!Pump(missing)&&!f.Ready,"Unknown customer accepted");
            var wrongId=f.Select("expected",CustomerReferenceKind.ById);f.References.Complete(14,Customer("other"));Check(!Pump(wrongId)&&!f.Ready&&f.Id!="other","ById result identity mismatch accepted");
            var closed=f.Select("closing");f.Form.Close();f.Form.Dispose();f.References.Complete(15,Customer("secret"));
            Check(!Pump(closed)&&f.References.Calls[15].Token.IsCancellationRequested,"Disposed form published result");
        }
    }
    internal static void Run(){if(RunOwned()!=0)throw new InvalidOperationException("Billing customer read self-tests failed.");}
    static int RunOwned()
    {
        try
        {
            checks=dialogs=0;Application.EnableVisualStyles();Control.CheckForIllegalCrossThreadCalls=true;
            native=typeof(Billing).Assembly;sessionType=native.GetType("project_fo_3.SalesCustomerReferenceReadSession",true);
            var context=native.GetType("project_fo_3.SessionContext",true);context.GetMethod("Start",members).Invoke(null,new object[]{"owner","Manager","EMP"});
            using(var dispatcher=new Form())
            {
                dispatcher.CreateControl();var handle=dispatcher.Handle;
                SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
                var theme=native.GetType("project_fo_3.UiTheme",true);
                foreach(string mode in new[]{"Light","Dark"})
                {
                    var settings=native.GetType("project_fo_3.Properties.Setings",true);
                    var currentSettings=settings.GetProperty("Default",members).GetValue(null,null);
                    if(originalSettings==null){originalSettings=currentSettings;themeProperty=settings.GetProperty("ThemeMode",members);originalTheme=(string)themeProperty.GetValue(currentSettings,null);}
                    themeProperty.SetValue(currentSettings,mode,null);
                    Check((string)theme.GetMethod("LoadMode",members).Invoke(null,null)==mode,"Actual theme mode mismatch");
                    Cases(false);Cases(true);
                }
                context.GetMethod("Clear",members).Invoke(null,null);
            }
            Console.WriteLine("PASS Billing customer native forms: checks="+checks+"; dialogs="+dialogs+"; owned STA/context, async options/selection/credit, cancellation, stale/options race, raw credit, save guard, recovery/denial/general and disposal; no SQL/write/spool/native keyboard.");return 0;
        }
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
        finally{if(originalSettings!=null)themeProperty.SetValue(originalSettings,originalTheme,null);if(native!=null)native.GetType("project_fo_3.SessionContext",true).GetMethod("Clear",members).Invoke(null,null);}
    }
}
}
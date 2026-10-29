using System;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using PharmacyManagement.Application;
using project_fo_3;
static class UnitEditorPendingRecoveryProbe
{
    static readonly BindingFlags Flags=BindingFlags.NonPublic|BindingFlags.Instance;
    static int checks;
    static void Check(bool ok,string why){if(!ok)throw new Exception(why);checks++;}
    static object Field(object o,string n)=>o.GetType().GetField(n,Flags).GetValue(o);
    static Task Call(object o,string n)=>(Task)o.GetType().GetMethod(n,Flags).Invoke(o,null);
    static void Settle(Task task){var until=DateTime.UtcNow.AddSeconds(15);while(!task.IsCompleted&&DateTime.UtcNow<until){Application.DoEvents();Thread.Sleep(2);}Check(task.IsCompleted,"Owned task settles");task.GetAwaiter().GetResult();Application.DoEvents();}
    sealed class Writer:IMedicineUnitCatalogRepository
    {
        internal int Calls;internal MedicineUnitCatalogWrite Request;internal bool Accepted;internal Action<MedicineUnitCatalogWrite> OnWrite;
        public Task<MedicineUnitCatalogSnapshot> GetAsync(string n,CancellationToken t){throw new Exception("Unexpected fake read");}
        public Task<MedicineUnitCatalogSaveResult> SaveAsync(MedicineUnitCatalogWrite r,OperationActor a,Action policy,CancellationToken t)
        {Calls++;Request=r;OnWrite?.Invoke(r);if(Accepted){policy();return Task.FromResult(new MedicineUnitCatalogSaveResult(r.RequestId,r.ExpectedRevision+1,true));}return Task.FromException<MedicineUnitCatalogSaveResult>(new IOException("Owned uncertain response"));}
    }
    static MedicineUnitCatalogEditor Editor(string medicine,string folder)
    {
        var ctor=typeof(MedicineUnitCatalogEditor).GetConstructor(Flags,null,new[]{typeof(string),typeof(bool),typeof(bool),typeof(string)},null);
        return ctor==null?new MedicineUnitCatalogEditor(medicine,false):(MedicineUnitCatalogEditor)ctor.Invoke(new object[]{medicine,false,false,folder});
    }
    [STAThread] static int Main(string[] args)
    {
        try{
            RequireOwnedPrivateDesktop.Assert();Guid owner;
            Check((args.Length==2||args.Length==4)&&Guid.TryParseExact(args[1],"N",out owner)&&args[0]=="PharmacyUnits_"+args[1]+"_qa","Exact owned QA required");
            var cs=new SqlConnectionStringBuilder(Database.ConnectionString);Check(cs.InitialCatalog==args[0],"Pinned QA profile required");
            string medicine;using(var c=new SqlConnection(cs.ConnectionString)){c.Open();using(var cmd=new SqlCommand("SELECT CONVERT(nvarchar(32),value) FROM sys.extended_properties WHERE class=0 AND name=N'QaTest.Owner'",c))Check(Convert.ToString(cmd.ExecuteScalar())==args[1],"Owner marker matches");using(var cmd=new SqlCommand("SELECT TOP(1) MedName FROM dbo.MedicineTb1 ORDER BY MedName",c))medicine=Convert.ToString(cmd.ExecuteScalar());}
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);AppFonts.Initialize();
            SessionContext.Start("owned-unit-recovery-"+args[1],"Manager","UNIT-RECOVERY");
            string mode=args.Length==4?args[2]:"roundtrip";string folder=args.Length==4?Path.GetFullPath(args[3]):Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"owned-pending");if(args.Length==4)Check(folder.StartsWith("D:\\Comp\\Pharmacy-Management-master\\artifacts\\company-demo-20261008\\unit-editor-pending-process-owned-",StringComparison.OrdinalIgnoreCase)&&File.ReadAllText(Path.Combine(folder,"owner.txt"))==args[1],"Owned shared recovery folder required");Guid id;byte[] payload;
            if(mode!="read"){
            using(var form=Editor(medicine,folder)){
                form.ShowInTaskbar=false;var load=Call(form,"LoadAsync");form.Show();Settle(load);Check((bool)Field(form,"ready"),"Live initial metadata loaded");
                ((CheckBox)Field(form,"confirm")).Checked=true;
                var writer=new Writer{OnWrite=r=>{var durable=((MedicineUnitPendingStore)Field(form,"pendingStore")).Load();Check(durable!=null&&durable.RequestId==r.RequestId&&MedicineUnitCatalogValidation.Prepare(durable).Payload.SequenceEqual(r.Payload),"Pending record is durable before dispatching the write");}};var policy=(Func<OperationActor,bool>)Delegate.CreateDelegate(typeof(Func<OperationActor,bool>),form,typeof(MedicineUnitCatalogEditor).GetMethod("CanManage",Flags));
                typeof(MedicineUnitCatalogEditor).GetField("service",Flags).SetValue(form,new MedicineUnitCatalogService(writer,policy));
                Settle(Call(form,"SaveAsync"));Check(writer.Calls==1,"One fake uncertain write");
                id=writer.Request.RequestId;payload=writer.Request.Payload;
                Check(((MedicineUnitCatalogRequest)Field(form,"pending")).RequestId==id,"Original pending request retained before close");
                if(mode=="write"){
                    File.WriteAllLines(Path.Combine(folder,"ui-expected.txt"),new[]{id.ToString("D"),Convert.ToBase64String(payload)});
                    Console.WriteLine("PASS first UI process preserved unknown request before abrupt owned process exit; checks="+checks+"; form disposal/finally deliberately skipped");Console.Out.Flush();Environment.Exit(0);
                }
                form.Close();Application.DoEvents();Check(form.IsDisposed,"Unknown write window can close safely");
            }
            if(mode=="write"){File.WriteAllLines(Path.Combine(folder,"ui-expected.txt"),new[]{id.ToString("D"),Convert.ToBase64String(payload)});Console.WriteLine("PASS first UI process preserved unknown request; checks="+checks);return 0;}
            }else{var expected=File.ReadAllLines(Path.Combine(folder,"ui-expected.txt"));id=Guid.Parse(expected[0]);payload=Convert.FromBase64String(expected[1]);}
            using(var reopened=Editor(medicine,folder)){
                reopened.ShowInTaskbar=false;reopened.Show();Application.DoEvents();
                var pending=(MedicineUnitCatalogRequest)Field(reopened,"pending");
                Console.WriteLine("OBSERVE reopened unit editor: restored="+(pending!=null)+"; originalRequest="+id+"; SQLWrites=False NativeInput=False");
                Check(pending!=null&&pending.RequestId==id,"Uncertain unit request must survive close/reopen with the same identity");
                Check(MedicineUnitCatalogValidation.Prepare(pending).Payload.SequenceEqual(payload),"Recovered canonical request payload unchanged");
                Check(!((DataGridView)Field(reopened,"grid")).Enabled,"Recovered uncertain request is not editable");
                if(mode=="read"){
                    var writer=new Writer{Accepted=true};var policy=(Func<OperationActor,bool>)Delegate.CreateDelegate(typeof(Func<OperationActor,bool>),reopened,typeof(MedicineUnitCatalogEditor).GetMethod("CanManage",Flags));
                    typeof(MedicineUnitCatalogEditor).GetField("service",Flags).SetValue(reopened,new MedicineUnitCatalogService(writer,policy));
                    Check(writer.Calls==0,"Recovered UI does not automatically submit");Settle(Call(reopened,"SaveAsync"));
                    Check(writer.Calls==1&&writer.Request.RequestId==id&&writer.Request.Payload.SequenceEqual(payload),"Explicit verification uses original identity and payload");
                    Check(Field(reopened,"pending")==null,"Known matching acceptance clears pending");
                }
                reopened.Close();Application.DoEvents();
            }
            if(mode=="read"){
                using(var failure=Editor(medicine,folder)){
                    failure.ShowInTaskbar=false;var load=Call(failure,"LoadAsync");failure.Show();Settle(load);Check((bool)Field(failure,"ready"),"Fresh editor is ready for persistence-failure boundary");
                    ((CheckBox)Field(failure,"confirm")).Checked=true;
                    var writer=new Writer();var policy=(Func<OperationActor,bool>)Delegate.CreateDelegate(typeof(Func<OperationActor,bool>),failure,typeof(MedicineUnitCatalogEditor).GetMethod("CanManage",Flags));
                    typeof(MedicineUnitCatalogEditor).GetField("service",Flags).SetValue(failure,new MedicineUnitCatalogService(writer,policy));
                    ((MedicineUnitPendingStore)Field(failure,"pendingStore")).Dispose();
                    Settle(Call(failure,"SaveAsync"));Check(writer.Calls==0&&(bool)Field(failure,"recoveryBlocked"),"Unavailable recovery store blocks dispatch instead of risking an untracked write");
                    failure.Close();Application.DoEvents();Check(failure.IsDisposed,"Blocked recovery window can close safely");
                }
            }
            Console.WriteLine("PASS unit editor uncertain request recovery checks="+checks+"; actual SQL reads, fake write only; isolated local pending folder");return 0;
        }catch(Exception e){Console.Error.WriteLine(e);return 1;}finally{SessionContext.Clear();}
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;
using PharmacyManagement.Application;
namespace project_fo_3
{
    internal static class CustomerMasterWriteUiProbe
    {
        static int checks,dialogs;
        static BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
        static object Field(object form,string name)=>form.GetType().GetField(name,flags).GetValue(form);
        static object Call(object form,string name,params object[] args)
        {try{return form.GetType().GetMethod(name,flags).Invoke(form,args);}catch(TargetInvocationException e){throw e.InnerException??e;}}
        static void Check(bool v,string m){if(!v)throw new Exception(m);checks++;}
        static void Tick(){for(int i=0;i<6;i++){System.Windows.Forms.Application.DoEvents();Thread.Sleep(2);}}
        static void Pump(Task task)
        {var until=DateTime.UtcNow.AddSeconds(10);while(!task.IsCompleted&&DateTime.UtcNow<until)Tick();Check(task.IsCompleted,"UI task timeout");task.GetAwaiter().GetResult();}
        sealed class Read:ISalesCustomerService
        {
            public bool Fail;
            public Task<IReadOnlyList<SalesCustomer>> GetAllAsync(OperationActor actor,CancellationToken token)
            {if(Fail)throw new Exception("owned read failure");return Task.FromResult((IReadOnlyList<SalesCustomer>)new List<SalesCustomer>{new SalesCustomer{CustomerId="001",CustomerName="عربي & Latin 001",PhoneNumber="001234",Address="عنوان"}});}
            public string Create(SalesCustomer customer,OperationActor actor){throw new Exception("Legacy write called");}
            public bool Update(SalesCustomer customer,OperationActor actor){throw new Exception("Legacy write called");}
        }
        sealed class Write:ICustomerMasterWriteService
        {
            public int Submits,Cancels,Resolves;
            public bool Unknown=true,Commit=true,Reject,Pending;
            public TaskCompletionSource<CustomerMasterWriteReceipt> Completion;
            public Dictionary<Guid,CustomerMasterWriteReceipt> Receipts=new Dictionary<Guid,CustomerMasterWriteReceipt>();
            public CustomerMasterWriteRequest Last;
            public Task<CustomerMasterWriteReceipt> SubmitAsync(CustomerMasterWriteRequest r,OperationActor a,CancellationToken token)
            {
                Submits++;Last=r;if(Reject)throw new CustomerMasterWriteRejectedException("known fixture rejection");
                var receipt=new CustomerMasterWriteReceipt(r.RequestId,"001",1,DateTime.UtcNow,true,true);
                if(Commit)Receipts[r.RequestId]=receipt;
                if(Pending){Completion=new TaskCompletionSource<CustomerMasterWriteReceipt>();return Completion.Task;}
                if(Unknown)throw new CustomerMasterWriteOutcomeUnknownException(r.RequestId,new Exception("lost fixture response"));
                return Task.FromResult(receipt);
            }
            public Task<CustomerMasterWriteStatus> ResolveAsync(CustomerMasterWriteRequest r,OperationActor a,CancellationToken token)
            {Resolves++;CustomerMasterWriteReceipt found;Receipts.TryGetValue(r.RequestId,out found);return Task.FromResult(new CustomerMasterWriteStatus(found));}
            public Task<CustomerMasterWriteReceipt> CancelAsync(CustomerMasterWriteRequest r,OperationActor a,CancellationToken token)
            {Cancels++;CustomerMasterWriteReceipt found;if(!Receipts.TryGetValue(r.RequestId,out found)){found=new CustomerMasterWriteReceipt(r.RequestId,"",2,DateTime.UtcNow,true,false,CustomerMasterWriteOutcome.Canceled);Receipts.Add(r.RequestId,found);}return Task.FromResult(found);}
        }
        sealed class Fixture:IDisposable
        {
            public CustomerSupplierManagementForm Form;public Write Writer;public Read Reader;public bool Current=true;
            public Fixture(string directory,Write writer,bool english,Read reader=null)
            {
                Writer=writer;Reader=reader??new Read();
                Form=new CustomerSupplierManagementForm(false,false,english,Reader);
                Form.CustomerPendingDirectoryOverride=directory;
                Form.CustomerMasterWriteSessionFactory=()=>new CustomerMasterWriteSession(Writer,
                    new OperationActor{UserName=SessionContext.CurrentUser,UserRole=SessionContext.CurrentRole},new string('A',64),()=>Current&&SessionContext.IsAuthenticated);
                Form.StartPosition=FormStartPosition.Manual;Form.Location=new Point(-28000,-28000);
                Form.Opacity=0;Form.ShowInTaskbar=false;Form.Show();Tick();dialogs++;
            }
            public CustomerMasterWriteAttempt Attempt=>(CustomerMasterWriteAttempt)Field(Form,"customerWriteAttempt");
            public void Draft()
            {Call(Form,"ClearEditor");((TextBox)Field(Form,"name")).Text="عربي & Latin 001";((AntdUI.Input)Field(Form,"phone")).Text="001234";((TextBox)Field(Form,"address")).Text="عنوان";}
            public void Save()=>Pump((Task)Call(Form,"SaveCustomer"));
            public void Resolve(bool cancel=false)=>Pump((Task)Call(Form,"ResolveCustomerWrite",cancel));
            public void Dispose(){Form.Dispose();Tick();}
        }
        internal static string Run(string directory)
        {
            if(Directory.Exists(directory))throw new Exception("Owned UI recovery directory must be new.");
            Directory.CreateDirectory(directory);string owner=Guid.NewGuid().ToString("N");File.WriteAllText(Path.Combine(directory,"owner"),owner);
            string originalTheme=Properties.Setings.Default.ThemeMode;
            try
            {
                foreach(string theme in new[]{"Light","Dark"})
                foreach(bool english in new[]{false,true})
                {
                    Properties.Setings.Default.ThemeMode=theme;SessionContext.Start("owner","Manager","E");
                    string slot=Path.Combine(directory,theme+(english?"-en":"-ar"));var writer=new Write();Guid id;
                    using(var first=new Fixture(slot,writer,english))
                    {
                        first.Draft();first.Save();id=first.Attempt.Request.RequestId;
                        Check(writer.Submits==1&&first.Attempt.Phase==CustomerMasterWritePhase.Unknown,"unknown locked");
                        Check(((Control)Field(first.Form,"verifyCustomerWriteButton")).Text==(english?"Check saved result":"تحقق من الحفظ"),"translated recovery button");
                        Check(((TextBox)Field(first.Form,"name")).ReadOnly&&!((Control)Field(first.Form,"addButton")).Enabled,"editor locked");
                        Check(((TextBox)Field(first.Form,"name")).Text=="عربي & Latin 001"&&((AntdUI.Input)Field(first.Form,"phone")).Text=="001234","raw inputs");
                        first.Save();Check(writer.Submits==1,"blind save blocked");
                        Call(first.Form,"ClearEditor");Check(first.Attempt.Request.RequestId==id,"new blocked");
                        Check(Directory.GetFiles(slot,"*.pending").Length==1,"durable before close");
                    }
                    using(var reopen=new Fixture(slot,writer,english))
                    {
                        Check(reopen.Attempt.Request.RequestId==id&&reopen.Attempt.Phase==CustomerMasterWritePhase.Unknown,"restart same attempt");
                        reopen.Resolve();Check(reopen.Attempt==null&&writer.Submits==1&&writer.Resolves==1,"receipt resolves without submit");
                        Check(Directory.GetFiles(slot,"*.pending").Length==0,"known receipt removes durable");
                        Check(!((TextBox)Field(reopen.Form,"name")).ReadOnly,"known receipt unlock");
                        writer.Commit=false;reopen.Draft();reopen.Save();var missingId=reopen.Attempt.Request.RequestId;
                        reopen.Resolve();Check(reopen.Attempt.Phase==CustomerMasterWritePhase.Ready&&!reopen.Attempt.CanDiscard,"negative not discard");
                        Check(((Control)Field(reopen.Form,"saveButton")).Enabled&&((TextBox)Field(reopen.Form,"name")).ReadOnly,"same retry frozen");
                        writer.Unknown=false;writer.Commit=true;reopen.Save();
                        Check(writer.Last.RequestId==missingId&&reopen.Attempt==null,"same request retry");
                        writer.Unknown=true;writer.Commit=false;reopen.Draft();reopen.Save();Guid closeId=reopen.Attempt.Request.RequestId;
                        reopen.Resolve(true);Check(reopen.Attempt==null&&writer.Receipts[closeId].Outcome==CustomerMasterWriteOutcome.Canceled,"sealed close");
                        Check(((TextBox)Field(reopen.Form,"name")).Text==""&&Directory.GetFiles(slot,"*.pending").Length==0,"canceled clears");
                        writer.Reject=true;reopen.Draft();reopen.Save();Check(reopen.Attempt==null&&Directory.GetFiles(slot,"*.pending").Length==0,"known rejection editable");
                        writer.Reject=false;writer.Pending=true;writer.Commit=true;reopen.Draft();
                        var pending=(Task)Call(reopen.Form,"SaveCustomer");Check(!pending.IsCompleted&&writer.Completion!=null,"pending async UI");
                        Guid lostId=writer.Last.RequestId;SessionContext.Start("owner","Manager","E");Tick();
                        Check(((TextBox)Field(reopen.Form,"name")).Text==""&&reopen.Attempt==null,"session hides private data");
                        ThreadPool.QueueUserWorkItem(_=>writer.Completion.SetResult(writer.Receipts[lostId]));
                        Pump(pending);Check(Directory.GetFiles(slot,"*.pending").Length==1,"session loss preserves durable");
                        writer.Pending=false;Check((bool)Call(reopen.Form,"InitializeCustomerRecovery"),"new authorized session opens recovery");
                        Check(reopen.Attempt.Request.RequestId==lostId,"reauth restored same");
                        reopen.Resolve();Check(reopen.Attempt==null,"reauth resolves");
                        writer.Unknown=false;reopen.Reader.Fail=true;reopen.Draft();reopen.Save();
                        Check(reopen.Attempt==null&&Directory.GetFiles(slot,"*.pending").Length==0,"saved despite refresh failure");
                        Check((string)Field(reopen.Form,"editingCustomerId")=="001","saved id preserved after refresh failure");
                        reopen.Reader.Fail=false;
                        writer.Unknown=true;writer.Commit=false;reopen.Draft();reopen.Save();
                        reopen.Current=false;Call(reopen.Form,"ApplyWritePermissionState");
                        Check(((TextBox)Field(reopen.Form,"name")).Text==""&&reopen.Attempt==null,"context loss hides inputs");
                        Check(Directory.GetFiles(slot,"*.pending").Length==1,"context loss retains pending file");
                        reopen.Current=true;Check((bool)Call(reopen.Form,"InitializeCustomerRecovery"),"context recovery reload");
                        reopen.Resolve(true);Check(reopen.Attempt==null,"context recovery sealed");
                    }
                }
                return "PASS customer write owned UI recovery: checks="+checks+"; dialogs="+dialogs+"; Arabic/English and Light/Dark; actual form with controlled read/write sources, durable close/reopen, lost response/replay/seal/rejection and worker completion after reauthentication; no native SQL, keyboard, DPI or printing.";
            }
            finally
            {
                Properties.Setings.Default.ThemeMode=originalTheme;SessionContext.Clear();
                if(File.ReadAllText(Path.Combine(directory,"owner"))==owner)
                {
                    foreach(var file in Directory.GetFiles(directory,"*",SearchOption.AllDirectories))File.Delete(file);
                    foreach(var child in Directory.GetDirectories(directory))Directory.Delete(child,false);
                    Directory.Delete(directory,false);
                }
            }
        }
    }
}



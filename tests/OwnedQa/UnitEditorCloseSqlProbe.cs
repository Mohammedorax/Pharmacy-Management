using System;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using PharmacyManagement.Application;
using project_fo_3;

internal static class UnitEditorCloseSqlProbe
{
    static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static int checks;
    static void Check(bool value,string message){if(!value)throw new Exception(message);checks++;}
    static object Scalar(SqlConnection c,SqlTransaction t,string text){using(var cmd=new SqlCommand(text,c,t){CommandTimeout=5})return cmd.ExecuteScalar();}
    static string Fingerprint(SqlConnection c)
    {
        // Complete ordered XML, not counts alone. This probe never changes business rows.
        return Convert.ToString(Scalar(c,null,"SELECT (SELECT * FROM dbo.MedicineTb1 ORDER BY MedName FOR XML RAW,BINARY BASE64,ROOT('rows'))"))
            +Convert.ToString(Scalar(c,null,"SELECT (SELECT * FROM dbo.MedicineUnitCatalogTb ORDER BY CatalogId FOR XML RAW,BINARY BASE64,ROOT('rows'))"))
            +Convert.ToString(Scalar(c,null,"SELECT (SELECT * FROM dbo.MedicineUnitsTb ORDER BY UnitId FOR XML RAW,BINARY BASE64,ROOT('rows'))"))
            +Convert.ToString(Scalar(c,null,"SELECT (SELECT * FROM dbo.MedicineUnitWriteReceiptsTb ORDER BY RequestId FOR XML RAW,BINARY BASE64,ROOT('rows'))"));
    }
    static bool WaitFor(Func<bool> condition,int timeout)
    {
        var clock=Stopwatch.StartNew();
        do{Application.DoEvents();if(condition())return true;Thread.Sleep(20);}while(clock.ElapsedMilliseconds<timeout);
        return condition();
    }
    sealed class PendingWriter:IMedicineUnitCatalogRepository
    {
        internal TaskCompletionSource<MedicineUnitCatalogSaveResult> Pending=new TaskCompletionSource<MedicineUnitCatalogSaveResult>();
        internal MedicineUnitCatalogWrite Request;internal CancellationToken Token;internal Action AuthorizeCommit;internal int Calls;
        public Task<MedicineUnitCatalogSnapshot> GetAsync(string name,CancellationToken token){throw new Exception("Unexpected fake read");}
        public Task<MedicineUnitCatalogSaveResult> SaveAsync(MedicineUnitCatalogWrite request,OperationActor actor,Action authorize,CancellationToken token)
        {Request=request;Token=token;AuthorizeCommit=authorize;Calls++;return Pending.Task;}
    }
    static void VerifyWriteLifetime(string medicine,bool english,bool relogin)
    {
        SessionContext.Start("owned-unit-editor-save","Manager","UNIT-SAVE");
        using(var form=new MedicineUnitCatalogEditor(medicine,english,false,System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"owned-lifetime-pending")))
        {
            form.ShowInTaskbar=false;
            var load=(Task)typeof(MedicineUnitCatalogEditor).GetMethod("LoadAsync",Flags).Invoke(form,null);
            form.Show();Check(WaitFor(()=>load.IsCompleted,15000),"Normal live editor read settles");
            Check((bool)typeof(MedicineUnitCatalogEditor).GetField("ready",Flags).GetValue(form),"Normal live metadata/catalog load remains usable");
            ((CheckBox)typeof(MedicineUnitCatalogEditor).GetField("confirm",Flags).GetValue(form)).Checked=true;
            var repository=new PendingWriter();
            var policy=(Func<OperationActor,bool>)Delegate.CreateDelegate(typeof(Func<OperationActor,bool>),form,typeof(MedicineUnitCatalogEditor).GetMethod("CanManage",Flags));
            typeof(MedicineUnitCatalogEditor).GetField("service",Flags).SetValue(form,new MedicineUnitCatalogService(repository,policy));
            var save=(Task)typeof(MedicineUnitCatalogEditor).GetMethod("SaveAsync",Flags).Invoke(form,null);
            Check(repository.Calls==1&&!save.IsCompleted,"One fake write remains pending through the real UI/service seam");
            Guid requestId=repository.Request.RequestId;
            Check(!((Control)typeof(MedicineUnitCatalogEditor).GetField("close",Flags).GetValue(form)).Enabled,"Close action disabled during save");
            form.Close();Application.DoEvents();
            Check(!form.IsDisposed&&!repository.Token.IsCancellationRequested&&!save.IsCompleted,"Close cannot abort an in-flight save or lose its request");
            repository.Pending.SetException(new System.IO.IOException("Owned simulated uncertain response"));
            Check(WaitFor(()=>save.IsCompleted,3000),"Uncertain save returns to controlled retry state");
            var pending=(MedicineUnitCatalogRequest)typeof(MedicineUnitCatalogEditor).GetField("pending",Flags).GetValue(form);
            Check(pending!=null&&pending.RequestId==requestId,"Uncertain outcome retains original request identity");
            var verify=(Control)typeof(MedicineUnitCatalogEditor).GetField("save",Flags).GetValue(form);
            Check(verify.AccessibleName==verify.Text,"Recovered/retry action accessible name matches its actual meaning");
            Check(TextRenderer.MeasureText(verify.Text,verify.Font,new System.Drawing.Size(int.MaxValue,int.MaxValue),TextFormatFlags.SingleLine|TextFormatFlags.NoPadding).Width+32<=verify.ClientSize.Width,"Retry caption and icon fit the fixed footer action");
            repository.Pending=new TaskCompletionSource<MedicineUnitCatalogSaveResult>();
            save=(Task)typeof(MedicineUnitCatalogEditor).GetMethod("SaveAsync",Flags).Invoke(form,null);
            Check(repository.Calls==2&&repository.Request.RequestId==requestId&&!save.IsCompleted,"Retry sends the same request, not a new identity");
            repository.AuthorizeCommit();
            if(relogin)SessionContext.Start("owned-unit-editor-save","Manager","UNIT-SAVE");
            repository.Pending.SetResult(new MedicineUnitCatalogSaveResult(requestId,repository.Request.ExpectedRevision+1,false));
            Check(WaitFor(()=>save.IsCompleted,3000),"Known fake acceptance returns after retry");
            Check(typeof(MedicineUnitCatalogEditor).GetField("pending",Flags).GetValue(form)==null,"Known acceptance clears pending even after same-user relogin");
            form.Close();Application.DoEvents();Check(form.IsDisposed,"Editor can close after known save outcome");
        }
        SessionContext.Clear();
    }
    static int WaitingReader(SqlConnection c,int blocker,int clientPid)
    {
        using(var cmd=new SqlCommand(@"SELECT COUNT(*) FROM sys.dm_exec_requests r JOIN sys.dm_exec_sessions s ON s.session_id=r.session_id
 WHERE r.blocking_session_id=@blocker AND r.database_id=DB_ID() AND s.host_process_id=@pid AND r.wait_type LIKE 'LCK_M_%'",c){CommandTimeout=5})
        {cmd.Parameters.AddWithValue("@blocker",blocker);cmd.Parameters.AddWithValue("@pid",clientPid);return Convert.ToInt32(cmd.ExecuteScalar());}
    }
    [STAThread] static int Main(string[] args)
    {
        try
        {
            RequireOwnedPrivateDesktop.Assert();
            Guid owner;Check(args.Length==2&&Guid.TryParseExact(args[1],"N",out owner)&&args[0]=="PharmacyUnits_"+args[1]+"_qa","Exact owned unit QA required");
            var builder=new SqlConnectionStringBuilder(Database.ConnectionString);
            Check(builder.InitialCatalog==args[0],"Pinned owned QA profile required");
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);AppFonts.Initialize();
            using(var observer=new SqlConnection(builder.ConnectionString))
            using(var blocker=new SqlConnection(builder.ConnectionString))
            {
                observer.Open();blocker.Open(); int blockerId=Convert.ToInt32(Scalar(blocker,null,"SELECT @@SPID"));
                Check(Convert.ToString(Scalar(observer,null,"SELECT CONVERT(nvarchar(32),value) FROM sys.extended_properties WHERE class=0 AND name=N'QaTest.Owner'"))==args[1],"Owner marker matches");
                string medicine=Convert.ToString(Scalar(observer,null,"SELECT TOP(1) MedName FROM dbo.MedicineTb1 ORDER BY MedName"));
                Check(!string.IsNullOrWhiteSpace(medicine),"Existing QA medicine required");
                string overallBefore=Fingerprint(observer);
                foreach(string theme in new[]{"Light","Dark"})foreach(bool english in new[]{false,true}){
                project_fo_3.Properties.Setings.Default.ThemeMode=theme;
                string before=Fingerprint(observer);bool closed=false,cancelledWhileLocked=false;string disposeError=null;Task load=null;
                SessionContext.Start("owned-unit-editor-close","Manager","UNIT-CLOSE");
                var form=new MedicineUnitCatalogEditor(medicine,english,false,System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"owned-lifetime-pending")); try
                {
                    form.ShowInTaskbar=false;
                    var tx=blocker.BeginTransaction(IsolationLevel.Serializable);
                    try
                    {
                        Scalar(blocker,tx,"SELECT COUNT_BIG(*) FROM dbo.MedicineTb1 WITH(TABLOCKX,HOLDLOCK)");
                        load=(Task)typeof(MedicineUnitCatalogEditor).GetMethod("LoadAsync",Flags).Invoke(form,null);
                        form.Show();Application.DoEvents();
                        Check(WaitFor(()=>WaitingReader(observer,blockerId,Process.GetCurrentProcess().Id)==1,10000),"Actual editor SQL reader must wait on owned blocker");
                        Check(!load.IsCompleted,"Actual load pending before close");
                        var timer=Stopwatch.StartNew();form.Close();Application.DoEvents();closed=form.IsDisposed;
                        cancelledWhileLocked=WaitFor(()=>load.IsCompleted&&WaitingReader(observer,blockerId,Process.GetCurrentProcess().Id)==0,2000);
                        Console.WriteLine("OBSERVE unit editor close while actual SQL blocked: disposed="+closed+"; loadCompletedBeforeUnlock="+cancelledWhileLocked+"; closeAndCancelMs="+timer.ElapsedMilliseconds+"; lockOwner="+blockerId+"; NativeInput=False SQLWrites=False");
                    }
                    finally
                    {
                        tx.Rollback();tx.Dispose();
                        if(load!=null)Check(WaitFor(()=>load.IsCompleted,15000),"Owned editor load settles after blocker rollback");
                        form.Close();Application.DoEvents();
                    }
                    Check(form.IsDisposed,"Owned editor closed after cleanup");
                }
                finally { try { form.Dispose(); } catch(ObjectDisposedException error) { disposeError=error.ToString();Console.WriteLine("OBSERVE repeated owned form dispose failed: "+error.GetType().Name); } }
                SessionContext.Clear();
                Check(Fingerprint(observer)==before,"Medicine/catalog/unit/receipt content unchanged");
                Console.WriteLine("PASS medicine/catalog/unit/receipt content unchanged after owned blocker rollback and form cleanup");
                Check(closed&&cancelledWhileLocked&&disposeError==null,"Read-only unit editor load must close/cancel SQL before blocker release and tolerate repeated disposal");
                VerifyWriteLifetime(medicine,english,false);VerifyWriteLifetime(medicine,english,true);
                }
                Check(Fingerprint(observer)==overallBefore,"All live reads and fake write/retry cases preserve actual QA business content");
            }
            Console.WriteLine("PASS unit editor close under actual SQL lock; checks="+checks+";4 theme/language combinations;8 normal-load/fake-write-retry/known-acceptance cases; no business writes, paper or native input");return 0;
        }
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
        finally{SessionContext.Clear();}
    }
}

using System;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using PharmacyManagement.Application;
class NativeJournalEditorOwnedSql
{
    const BindingFlags P=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
    const BindingFlags S=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static;
    static Assembly app;static int checks;
    static void Check(bool ok,string msg){++checks;if(!ok)throw new InvalidOperationException(msg);}
    static object Field(object o,string n)=>o.GetType().GetField(n,P).GetValue(o);
    static object Property(object o,string n)=>o.GetType().GetProperty(n,P).GetValue(o,null);
    static JournalPostRequest Pending(object recovery){var value=Property(recovery,"Pending");return value==null?null:(JournalPostRequest)Property(value,"Request");}
    static void Pump(Task task){var w=Stopwatch.StartNew();while(!task.IsCompleted&&w.ElapsedMilliseconds<35000){Application.DoEvents();Thread.Sleep(5);}Check(task.IsCompleted,"Owned editor task timed out.");task.GetAwaiter().GetResult();}
    static Type Helper(bool quick)=>app.GetType("project_fo_3."+(quick?"QuickJournalRecoveryQaSelfTests":"JournalEntriesRecoveryQaSelfTests"),true);
    static Form Open(bool quick,string kind){SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());return (Form)Helper(quick).GetMethod("Open",S).Invoke(null,quick?null:new object[]{kind});}
    static Task<int> Save(Form form,bool quick){SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());Check(SynchronizationContext.Current is WindowsFormsSynchronizationContext,"Save requires owned UI context.");return (Task<int>)Helper(quick).GetMethod("Save",S).Invoke(null,new object[]{form});}
    static int Scalar(SqlConnection c,string sql){using(var q=new SqlCommand(sql,c)){q.CommandTimeout=10;return Convert.ToInt32(q.ExecuteScalar());}}
    static string Counts(SqlConnection c)=>Scalar(c,"SELECT COUNT(*) FROM dbo.JournalEntriesTb")+"|"+Scalar(c,"SELECT COUNT(*) FROM dbo.JournalEntryLinesTb")+"|"+Scalar(c,"SELECT COUNT(*) FROM dbo.AuditLogTb");
    static int Blocked(SqlConnection c,int id){using(var q=new SqlCommand("SELECT COUNT(*) FROM sys.dm_exec_requests r CROSS APPLY sys.dm_exec_sql_text(r.sql_handle) t WHERE r.database_id=DB_ID() AND r.blocking_session_id=@Blocker AND r.wait_type LIKE N'LCK_M%' AND t.text LIKE N'%AccountsTb WHERE AccountCode%'",c)){q.Parameters.AddWithValue("@Blocker",id);return Convert.ToInt32(q.ExecuteScalar());}}
    static void Fill(Form form,bool quick)
    {
        if(!quick){Helper(false).GetMethod("Fill",S).Invoke(null,new object[]{form,"1000","2000"});return;}
        foreach(var choice in new[]{new[]{"quickJournalDebitAccount","1000"},new[]{"quickJournalCreditAccount","2000"}})
        {var selector=(ComboBox)form.Controls.Find(choice[0],true).Single();var item=selector.Items.Cast<object>().Single(o=>Convert.ToString(o).StartsWith(choice[1]+" - ",StringComparison.Ordinal));selector.SelectedItem=item;}
        form.Controls.Find("quickJournalAmount",true).Single().Text=1250.50m.ToString(System.Globalization.CultureInfo.CurrentCulture);
        form.Controls.Find("quickJournalDescription",true).Single().Text="قيد سريع أصلي Cash 500 mg A&B";
    }
    static void CleanStore(object store,JournalPostRequest request)
    {
        if(store==null||request==null)return;
        string path=(string)Field(store,"recordPath"),context=(string)Field(store,"contextKey"),kind=(string)Field(store,"sourceType");
        Check(Path.GetFileName(path)==context+"-"+kind+".pending"&&kind==request.SourceType,"Owned pending path mismatch.");
        using(var cleaner=(IDisposable)Activator.CreateInstance(store.GetType(),P,null,new object[]{Path.GetDirectoryName(path),context,kind},null))
        {
            var loaded=cleaner.GetType().GetMethod("Load",P).Invoke(cleaner,null);
            if(loaded!=null){Check(((JournalPostRequest)Property(loaded,"Request")).SourceId==request.SourceId,"Refusing foreign pending cleanup.");cleaner.GetType().GetMethod("Resolve",P).Invoke(cleaner,new object[]{request.SourceId});}
        }
        if(File.Exists(path+".lock"))File.Delete(path+".lock");
    }
    [STAThread] static int Main(string[] args)
    {
        try
        {
            Console.OutputEncoding=new System.Text.UTF8Encoding(false);Console.InputEncoding=new System.Text.UTF8Encoding(false);Check(args.Length==2,"Owned database/token required.");string database=args[0],token=args[1];Guid owner;
            Check(Guid.TryParseExact(token,"N",out owner)&&owner!=Guid.Empty&&System.Text.RegularExpressions.Regex.IsMatch(database,@"^PharmacyRestore_[0-9]{14}_[a-f0-9]{32}_qa$")&&database.EndsWith("_"+token+"_qa",StringComparison.Ordinal),"Invalid owner identity.");
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Control.CheckForIllegalCrossThreadCalls=true;
            app=Assembly.LoadFrom(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"project_fo_3.exe"));
            string connection=(string)app.GetType("project_fo_3.Database",true).GetProperty("ConnectionString",S).GetValue(null,null);
            Check(new SqlConnectionStringBuilder(connection).InitialCatalog==database,"Editor source is not pinned to clone.");
            using(var observer=new SqlConnection(connection))
            using(var host=new Form{ShowInTaskbar=false})
            {
                observer.Open();using(var q=new SqlCommand("SELECT CONVERT(nvarchar(128),value) FROM sys.extended_properties WHERE class=0 AND name=N'QaTest.Owner'",observer))Check(Convert.ToString(q.ExecuteScalar())==token,"Owner marker mismatch.");
                var handle=host.Handle;SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
                foreach(string scenario in new[]{"Manual","OpeningEntry","Quick"})
                {
                    bool quick=scenario=="Quick";string kind=quick?"Manual":scenario;
                    string user="QA Native Editor "+Guid.NewGuid().ToString("N");app.GetType("project_fo_3.SessionContext",true).GetMethod("Start",S).Invoke(null,new object[]{user,"Manager","QA-NATIVE"});
                    string before=Counts(observer);JournalPostRequest original=null;object store=null;Task<int> writing=null;
                    try
                    {
                        using(var form=Open(quick,kind))
                        {
                            Fill(form,quick);object recovery=Field(form,quick?"quickRecovery":"journalRecovery");Check(Pending(recovery)==null,"Owned slot unexpectedly occupied.");store=Field(recovery,"store");
                            using(var blocker=new SqlConnection(connection))
                            {
                                blocker.Open();int blockerId=Scalar(blocker,"SELECT @@SPID");using(var tx=blocker.BeginTransaction())
                                {
                                    try
                                    {
                                        using(var q=new SqlCommand("SELECT TOP(1) AccountCode FROM dbo.AccountsTb WITH(TABLOCKX,HOLDLOCK)",blocker,tx))q.ExecuteScalar();
                                        writing=Save(form,quick);bool observed=false;var w=Stopwatch.StartNew();
                                        while(!observed&&!writing.IsCompleted&&w.ElapsedMilliseconds<10000){Application.DoEvents();observed=Blocked(observer,blockerId)>0;if(!observed)Thread.Sleep(5);}
                                        Check(observed,"Actual "+scenario+" save SQL lock not observed.");original=Pending(recovery);Check(original!=null&&Guid.ParseExact(original.SourceId,"N")!=Guid.Empty,"Stable request not persisted before SQL.");
                                        var cancellation=((CancellationTokenSource)Field(form,quick?"quickRecoveryCancellation":"journalRecoveryReadCancellation")).Token;
                                        int callback=0;host.BeginInvoke((Action)(()=>++callback));Application.DoEvents();Check(callback==1&&!writing.IsCompleted,"Owned UI did not respond while SQL was blocked.");
                                        var nativeHandle=form.Handle; int closing=0;CloseReason reason=CloseReason.None;form.FormClosing+=(sender,e)=>{++closing;reason=e.CloseReason;};form.Close();Check(closing==1&&reason==CloseReason.UserClosing,"Actual user close event missing.");Check(form.IsDisposed&&cancellation.IsCancellationRequested,"Actual user close was blocked or did not cancel write token.");
                                        var stopped=Stopwatch.StartNew();while((!writing.IsCompleted||Blocked(observer,blockerId)>0)&&stopped.ElapsedMilliseconds<8000){Application.DoEvents();Thread.Sleep(5);}
                                        Check(writing.IsCompleted&&Blocked(observer,blockerId)==0&&stopped.ElapsedMilliseconds<8000,"Actual "+scenario+" native write did not stop before unlocking.");
                                        Check(writing.GetAwaiter().GetResult()==0,"Disposed editor accepted blocked write.");
                                        Console.WriteLine("PASS native actual editor cancellation: scenario="+scenario+"; responsive owned UI; SQL request gone before unlock; elapsed_ms="+stopped.ElapsedMilliseconds);
                                    }
                                    finally{tx.Rollback();}
                                }
                            }
                        }
                        Pump(writing);Check(Counts(observer)==before,"Disposed editor committed finance/audit effects.");
                        using(var reopened=Open(quick,kind))
                        {
                            object recovery=Field(reopened,quick?"quickRecovery":"journalRecovery");var restored=Pending(recovery);
                            Check(restored!=null&&restored.SourceId==original.SourceId&&restored.Date==original.Date&&restored.Description==original.Description,"Reopen replaced identity/date/description.");
                            Check(restored.Lines.Count==original.Lines.Count&&restored.Lines.Zip(original.Lines,(a,b)=>a.AccountCode==b.AccountCode&&a.Debit==b.Debit&&a.Credit==b.Credit&&a.Description==b.Description&&a.CostCenterId==b.CostCenterId).All(v=>v),"Reopen changed original local financial lines.");
                            Check(!(bool)Property(recovery,"CanCorrectRejected")&&Counts(observer)==before,"Cancelled request became correctable or reopen wrote.");
                            var resumed=Save(reopened,quick);Pump(resumed);Check(resumed.Result>0&&Pending(recovery)==null,"Original cancelled request did not resume/settle once.");
                            using(var q=new SqlCommand("SELECT COUNT(*) FROM dbo.JournalEntriesTb WHERE SourceType=@Kind AND SourceId=@Source AND CreatedBy=@User",observer)){q.Parameters.AddWithValue("@Kind",kind);q.Parameters.AddWithValue("@Source",original.SourceId);q.Parameters.AddWithValue("@User",user);Check(Convert.ToInt32(q.ExecuteScalar())==1,"Resumed request did not create exactly one journal.");}
                        }
                        Console.WriteLine("PASS native actual editor recovery: scenario="+scenario+"; original encrypted identity/date/Arabic/local lines; passive reopen then one resumed acceptance.");
                    }
                    finally
                    {
                        if(writing!=null&&!writing.IsCompleted)Pump(writing);
                        if(original!=null)
                        {
                            using(var tx=observer.BeginTransaction())using(var q=new SqlCommand("DELETE FROM dbo.JournalEntryLinesTb WHERE JournalEntryId IN(SELECT JournalEntryId FROM dbo.JournalEntriesTb WHERE SourceType=@Kind AND SourceId=@Source AND CreatedBy=@User);DELETE FROM dbo.AuditLogTb WHERE UserName=@User AND ActionName=N'JournalEntryCreated' AND EntityName=N'Accounting';DELETE FROM dbo.JournalEntriesTb WHERE SourceType=@Kind AND SourceId=@Source AND CreatedBy=@User;",observer,tx)){q.Parameters.AddWithValue("@Kind",kind);q.Parameters.AddWithValue("@Source",original.SourceId);q.Parameters.AddWithValue("@User",user);q.ExecuteNonQuery();tx.Commit();}
                            CleanStore(store,original);
                        }
                    }
                    Check(Counts(observer)==before,"Owned editor cleanup changed counts.");
                }
            }
            Console.WriteLine("PASS native journal actual owned editors: checks="+checks+"; Manual/OpeningEntry/Quick; no public windows/native input/printing; controlled manager session.");return 0;
        }
        catch(Exception error){if(error is TargetInvocationException&&error.InnerException!=null)error=error.InnerException;Console.Error.WriteLine(error.ToString());return 1;}
    }
}

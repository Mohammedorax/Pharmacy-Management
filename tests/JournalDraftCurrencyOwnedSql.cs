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
using PharmacyManagement.Infrastructure.SqlServer;
class JournalDraftCurrencyOwnedSql
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
            Console.OutputEncoding=new System.Text.UTF8Encoding(false);Console.InputEncoding=new System.Text.UTF8Encoding(false);Check(args.Length==4&&(args[3]=="persisted-currency"||args[3]=="draft-only")&&(args[2]=="baseline-accepts"||args[2]=="guard-rejects"),"Owned identity and expectation required.");bool baseline=args[2]=="baseline-accepts";bool persisted=args[3]=="persisted-currency";string database=args[0],token=args[1];Guid owner;
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
                foreach(string scenario in new[]{"Manual","OpeningEntry"})
                {
                    bool quick=scenario=="Quick";string kind=quick?"Manual":scenario;
                    string user="QA Native Editor "+Guid.NewGuid().ToString("N");app.GetType("project_fo_3.SessionContext",true).GetMethod("Start",S).Invoke(null,new object[]{user,"Manager","QA-NATIVE"});
                    string before=Counts(observer);JournalPostRequest original=null;object store=null;Task<int> writing=null;
                    int center=0,alternate=0,priorCurrency=Scalar(observer,"SELECT CASE WHEN COUNT(*)=1 THEN MAX(CurrencyId) ELSE 0 END FROM dbo.CurrencySettingsTb WHERE IsSystemCurrency=1");
                    Check(priorCurrency>0,"One original system currency required.");
                    string priorCode;using(var q=new SqlCommand("SELECT CurrencyCode FROM dbo.CurrencySettingsTb WHERE CurrencyId=@Id",observer)){q.Parameters.AddWithValue("@Id",priorCurrency);priorCode=Convert.ToString(q.ExecuteScalar()).Trim();}Check(priorCode.Length>0,"Original currency code missing.");
                    string centerName="QA Journal Currency "+Guid.NewGuid().ToString("N"),alternateCode="Q"+Guid.NewGuid().ToString("N").Substring(0,9).ToUpperInvariant();
                    var currencies=(ICurrencyWriteService)app.GetType("project_fo_3.ApplicationServices",true).GetField("CurrencyWrites",S).GetValue(null);
                    var actor=new OperationActor{UserName=user,UserRole="Manager"};
                    try
                    {
                        using(var form=Open(quick,kind))
                        {
                            using(var q=new SqlCommand("INSERT INTO dbo.CostCentersTb(CostCenterName,IsActive) OUTPUT INSERTED.CostCenterId VALUES(@Name,1)",observer)){q.Parameters.AddWithValue("@Name",centerName);center=Convert.ToInt32(q.ExecuteScalar());}
                            using(var q=new SqlCommand("INSERT INTO dbo.CurrencySettingsTb(CountryNameAr,CurrencyNameAr,CurrencyCode,CurrencySymbol,IsSystemCurrency) OUTPUT INSERTED.CurrencyId VALUES(@Name,@Name,@Code,@Code,0)",observer)){q.Parameters.AddWithValue("@Name",centerName);q.Parameters.AddWithValue("@Code",alternateCode);alternate=Convert.ToInt32(q.ExecuteScalar());}
                            Check(center>0&&alternate>0,"Owned fixtures missing.");
                            Check(string.Equals(Convert.ToString(Property(Field(form,"currency"),"Text")).Trim(),priorCode,StringComparison.OrdinalIgnoreCase),"Actual editor did not load original system currency.");
                            Fill(form,quick);foreach(DataRow row in ((DataTable)Field(form,"lines")).Rows){row["CostCenterId"]=center;row["مركز التكلفة"]=centerName;}
                            object recovery=Field(form,quick?"quickRecovery":"journalRecovery");Check(Pending(recovery)==null,"Owned slot unexpectedly occupied.");store=Field(recovery,"store");
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
                                        Check(original.Lines.Count==2&&original.Lines.All(line=>line.CostCenterId==center),"Actual Save missed positive center.");
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
                        currencies.SetSystemCurrency(alternate,actor);
                        Check(Scalar(observer,"SELECT CurrencyId FROM dbo.CurrencySettingsTb WHERE IsSystemCurrency=1")==alternate,"Actual currency service switch missing.");
                        string changedCounts=Counts(observer);
                        using(var reopened=Open(quick,kind))
                        {
                            object recovery=Field(reopened,quick?"quickRecovery":"journalRecovery");var restored=Pending(recovery);
                            Check(restored!=null&&restored.SourceId==original.SourceId&&restored.Date==original.Date&&restored.Description==original.Description,"Reopen replaced identity/date/description.");
                            Check(restored.Lines.Count==original.Lines.Count&&restored.Lines.Zip(original.Lines,(a,b)=>a.AccountCode==b.AccountCode&&a.Debit==b.Debit&&a.Credit==b.Credit&&a.Description==b.Description&&a.CostCenterId==b.CostCenterId).All(v=>v),"Reopen changed original local financial lines.");
                            Check(JournalPendingRequest.Restore(original).HasSameContent(restored),"Passive reopen changed the complete original request.");
                            if(!baseline)
                            {
                                Check(string.Equals(Convert.ToString(Field(reopened,"journalSystemCurrencyCode")),priorCode,StringComparison.OrdinalIgnoreCase),"Restored editor reassigned its base currency.");
                                Check(((DataTable)Field(reopened,"lines")).Rows.Cast<DataRow>().All(row=>string.Equals(Convert.ToString(row["العملة"]),priorCode,StringComparison.OrdinalIgnoreCase)),"Restored local rows displayed the new system currency.");
                            }
                            Check(!(bool)Property(recovery,"CanCorrectRejected")&&Counts(observer)==changedCounts,"Cancelled request became correctable or reopen wrote.");
                            // Use the reopened editor's real recovery session without its modal error presenter.
                            // The original payload came from actual SaveEntryAsync and encrypted passive reopen.
                            SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
                            var attempt=(Task<int>)recovery.GetType().GetMethod("ExecuteAsync",P).Invoke(recovery,new object[]{CancellationToken.None});
                            bool denied=false;
                            try{Pump(attempt);}catch(JournalPostRejectedException error){denied=error.SourceId==original.SourceId&&error.SourceType==kind&&error.InnerException is InvalidOperationException;}
                            if(baseline){Check(!denied&&attempt.Status==TaskStatus.RanToCompletion&&attempt.Result>0,"Baseline did not reproduce unprotected currency adoption.");}
                            else{
                                Check(denied&&Counts(observer)==changedCounts,"Changed system currency was not rejected with zero financial/audit effects.");
                                var currencyProperty=original.GetType().GetProperty("ExpectedSystemCurrencyCode");Check(currencyProperty!=null&&string.Equals(Convert.ToString(currencyProperty.GetValue(original,null)),priorCode,StringComparison.OrdinalIgnoreCase),"Request did not preserve the original base currency identity.");
                                var pending=Pending(recovery);Check(pending!=null&&pending.SourceId==original.SourceId&&pending.Lines.All(line=>line.CostCenterId==center),"Currency rejection changed identity/center.");
                            }
                            currencies.SetSystemCurrency(priorCurrency,actor);
                            Check(Scalar(observer,"SELECT CurrencyId FROM dbo.CurrencySettingsTb WHERE IsSystemCurrency=1")==priorCurrency,"Original system currency not restored.");
                            SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
                            var resumed=baseline?Task.FromResult(attempt.Result):(Task<int>)recovery.GetType().GetMethod("ExecuteAsync",P).Invoke(recovery,new object[]{CancellationToken.None});Pump(resumed);Check(resumed.Result>0&&Pending(recovery)==null,"Original cancelled request did not resume/settle once.");
                            using(var q=new SqlCommand("SELECT COUNT(*) FROM dbo.JournalEntryLinesTb WHERE JournalEntryId=@Id AND CostCenterId=@Center AND ((AccountCode=N'1000' AND Debit=1250.50 AND Credit=0) OR (AccountCode=N'2000' AND Debit=0 AND Credit=1250.50))",observer)){q.Parameters.AddWithValue("@Id",resumed.Result);q.Parameters.AddWithValue("@Center",center);Check(Convert.ToInt32(q.ExecuteScalar())==2,"Stored positive-center/local values differ.");}
                            currencies.SetSystemCurrency(alternate,actor);
                            string acceptedChangedCounts=Counts(observer);
                            var replaySession=app.GetType("project_fo_3.ApplicationServices",true).GetMethod("CreateJournalPostSession",S,null,Type.EmptyTypes,null).Invoke(null,null);
                            var replay=((IAsyncJournalPostService)Field(replaySession,"Service")).PostAsync(original,(OperationActor)Field(replaySession,"Actor"),CancellationToken.None);Pump(replay);Check(replay.Result.AlreadyPosted&&replay.Result.Id==resumed.Result,"Same-ID replay unstable.");
                            Check(Counts(observer)==acceptedChangedCounts,"Replay under a changed system currency changed financial/audit counts.");
                            if(persisted)
                            {
                                using(var q=new SqlCommand("SELECT SystemCurrencyCode FROM dbo.JournalEntriesTb WHERE JournalEntryId=@Id",observer))
                                {q.Parameters.AddWithValue("@Id",resumed.Result);Check(Convert.ToString(q.ExecuteScalar())==priorCode,"Stored header currency differs from original request.");}
                                var documents=new SqlJournalDocumentReadRepository(()=>new SqlConnection(connection));
                                var syncDoc=documents.Load(new JournalDocumentReadRequest(resumed.Result));
                                var asyncDoc=documents.LoadAsync(new JournalDocumentReadRequest(resumed.Result),CancellationToken.None);Pump(asyncDoc);
                                Check(Convert.ToString(Property(syncDoc,"SystemCurrencyCode"))==priorCode&&Convert.ToString(Property(asyncDoc.Result,"SystemCurrencyCode"))==priorCode,"Document read adopted the new system currency.");
                                Check(syncDoc.Lines.Count==2&&syncDoc.Lines.All(l=>l.CostCenterId==center)&&syncDoc.Lines.Sum(l=>l.Debit)==1250.50m&&syncDoc.Lines.Sum(l=>l.Credit)==1250.50m,"Document read changed original local values or center.");
                                var printService=app.GetType("project_fo_3.JournalDocumentDesktopService",true);
                                Check(Convert.ToString(printService.GetMethod("CurrencyCaption",S).Invoke(null,new object[]{syncDoc,false}))=="عملة القيد: "+priorCode,"Arabic preview currency caption differs from the stored document.");
                                Check(Convert.ToString(printService.GetMethod("CurrencyCaption",S).Invoke(null,new object[]{syncDoc,true}))=="Journal currency: "+priorCode,"English preview currency caption differs from the stored document.");
                                using(var printRows=(DataTable)printService.GetMethod("PrintLines",S).Invoke(null,new object[]{syncDoc}))
                                    Check(printRows.Columns.Count==6&&printRows.Rows.Count==2&&printRows.Rows.Cast<DataRow>().Sum(row=>Convert.ToDecimal(row["مدين محلي"]))==1250.50m&&printRows.Rows.Cast<DataRow>().Sum(row=>Convert.ToDecimal(row["دائن محلي"]))==1250.50m,"Print preparation changed the original local money or table contract.");

                                var differentCurrency=new JournalPostRequest(original.Date,original.SourceType,original.SourceId,original.Description,original.Lines,alternateCode);
                                var differentReplay=((IAsyncJournalPostService)Field(replaySession,"Service")).PostAsync(differentCurrency,(OperationActor)Field(replaySession,"Actor"),CancellationToken.None);
                                bool conflict=false;try{Pump(differentReplay);}catch(JournalAcceptedRequestConflictException){conflict=true;}
                                Check(conflict&&Counts(observer)==acceptedChangedCounts,"Changed-currency payload replay was not rejected without effects.");
                                var statuses=new SqlJournalSubmissionStatusRepository(()=>new SqlConnection(connection));
                                var goodStatus=statuses.FindAsync(new JournalSubmissionStatusRequest(kind,original.SourceId,original),actor,CancellationToken.None);Pump(goodStatus);
                                var badStatus=statuses.FindAsync(new JournalSubmissionStatusRequest(kind,original.SourceId,differentCurrency),actor,CancellationToken.None);Pump(badStatus);
                                Check(goodStatus.Result.JournalId==resumed.Result&&goodStatus.Result.ContentMatches==true&&badStatus.Result.JournalId==resumed.Result&&badStatus.Result.ContentMatches==false,"Submission status did not compare stored currency identity.");
                                using(var q=new SqlCommand("SELECT TOP(1) JournalEntryId FROM dbo.JournalEntriesTb WHERE SystemCurrencyCode IS NULL AND JournalEntryId<>@Id ORDER BY JournalEntryId",observer))
                                {q.Parameters.AddWithValue("@Id",resumed.Result);var oldId=q.ExecuteScalar();Check(oldId!=null&&oldId!=DBNull.Value,"Historical unknown-currency fixture missing.");var historical=documents.Load(new JournalDocumentReadRequest(Convert.ToInt32(oldId)));Check(Property(historical,"SystemCurrencyCode")==null,"Historical currency was inferred during document read.");Check(Convert.ToString(printService.GetMethod("CurrencyCaption",S).Invoke(null,new object[]{historical,false}))=="عملة القيد: غير موثقة (سجل قديم)"&&Convert.ToString(printService.GetMethod("CurrencyCaption",S).Invoke(null,new object[]{historical,true}))=="Journal currency: Unverified (historical record)","Historical preview caption invented a currency.");}
                            }
                            currencies.SetSystemCurrency(priorCurrency,actor);
                            using(var q=new SqlCommand("SELECT COUNT(*) FROM dbo.JournalEntriesTb WHERE SourceType=@Kind AND SourceId=@Source AND CreatedBy=@User",observer)){q.Parameters.AddWithValue("@Kind",kind);q.Parameters.AddWithValue("@Source",original.SourceId);q.Parameters.AddWithValue("@User",user);Check(Convert.ToInt32(q.ExecuteScalar())==1,"Resumed request did not create exactly one journal.");}
                        }
                        Console.WriteLine("PASS draft currency owned editor: scenario="+scenario+"; expectation="+args[2]+"; actual cancelled Save/encrypted identity/positive center; service switch; reopened recovery; original-currency resume; accepted replay while system currency differs.");
                        if(baseline)Console.WriteLine("REPRODUCED unprotected pending local amounts accepted after system currency changed; not product acceptance.");
                    }
                    finally
                    {
                        if(writing!=null&&!writing.IsCompleted)Pump(writing);
                        if(priorCurrency>0&&Scalar(observer,"SELECT CurrencyId FROM dbo.CurrencySettingsTb WHERE IsSystemCurrency=1")!=priorCurrency)currencies.SetSystemCurrency(priorCurrency,actor);
                        if(original!=null)
                        {
                            using(var tx=observer.BeginTransaction())using(var q=new SqlCommand("DELETE FROM dbo.JournalEntryLinesTb WHERE JournalEntryId IN(SELECT JournalEntryId FROM dbo.JournalEntriesTb WHERE SourceType=@Kind AND SourceId=@Source AND CreatedBy=@User);DELETE FROM dbo.AuditLogTb WHERE UserName=@User AND ((ActionName=N'JournalEntryCreated' AND EntityName=N'Accounting') OR (ActionName=N'SystemCurrencyChanged' AND EntityName=N'CurrencySettings'));DELETE FROM dbo.JournalEntriesTb WHERE SourceType=@Kind AND SourceId=@Source AND CreatedBy=@User;",observer,tx)){q.Parameters.AddWithValue("@Kind",kind);q.Parameters.AddWithValue("@Source",original.SourceId);q.Parameters.AddWithValue("@User",user);q.ExecuteNonQuery();tx.Commit();}
                            CleanStore(store,original);
                        }
                        using(var q=new SqlCommand("DELETE FROM dbo.CurrencySettingsTb WHERE CurrencyId=@Currency AND CurrencyCode=@Code AND CountryNameAr=@Name AND IsSystemCurrency=0;DELETE FROM dbo.CostCentersTb WHERE CostCenterId=@Center AND CostCenterName=@Name",observer)){q.Parameters.AddWithValue("@Currency",alternate);q.Parameters.AddWithValue("@Code",alternateCode);q.Parameters.AddWithValue("@Name",centerName);q.Parameters.AddWithValue("@Center",center);q.ExecuteNonQuery();}
                    }
                    Check(Counts(observer)==before,"Owned editor cleanup changed counts.");
                }
            }
            Console.WriteLine("PASS draft currency actual owned editors: checks="+checks+"; Manual/OpeningEntry; no global input/printing; controlled manager session; changed-currency expectation recorded.");return 0;
        }
        catch(Exception error){if(error is TargetInvocationException&&error.InnerException!=null)error=error.InnerException;Console.Error.WriteLine(error.ToString());return 1;}
    }
}

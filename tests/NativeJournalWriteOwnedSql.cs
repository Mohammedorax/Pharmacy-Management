using System;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Application;
using PharmacyManagement.Domain;
using PharmacyManagement.Infrastructure.SqlServer;
class NativeJournalWriteOwnedSql
{
    static int checks;
    static void Check(bool value,string message){++checks;if(!value)throw new InvalidOperationException(message);}
    static int Scalar(SqlConnection c,string sql){using(var q=new SqlCommand(sql,c)){q.CommandTimeout=10;return Convert.ToInt32(q.ExecuteScalar());}}
    static string Counts(SqlConnection c)=>Scalar(c,"SELECT COUNT(*) FROM dbo.JournalEntriesTb")+"|"+Scalar(c,"SELECT COUNT(*) FROM dbo.JournalEntryLinesTb")+"|"+Scalar(c,"SELECT COUNT(*) FROM dbo.AuditLogTb");
    static int Partial(SqlConnection c,JournalPostRequest r,bool lines)
    {
        string sql=lines?"SELECT COUNT(*) FROM dbo.JournalEntryLinesTb WITH(READUNCOMMITTED) WHERE JournalEntryId IN(SELECT JournalEntryId FROM dbo.JournalEntriesTb WITH(READUNCOMMITTED) WHERE SourceType=@Kind AND SourceId=@Identity)":"SELECT COUNT(*) FROM dbo.JournalEntriesTb WITH(READUNCOMMITTED) WHERE SourceType=@Kind AND SourceId=@Identity";
        using(var q=new SqlCommand(sql,c)){q.Parameters.AddWithValue("@Kind",r.SourceType);q.Parameters.AddWithValue("@Identity",r.SourceId);return Convert.ToInt32(q.ExecuteScalar());}
    }
    static int Blocked(SqlConnection c,int blocker,string needle)
    {
        using(var q=new SqlCommand("SELECT COUNT(*) FROM sys.dm_exec_requests r CROSS APPLY sys.dm_exec_sql_text(r.sql_handle) t WHERE r.database_id=DB_ID() AND r.blocking_session_id=@Blocker AND r.wait_type LIKE N'LCK_M%' AND t.text LIKE @Needle",c))
        {q.Parameters.AddWithValue("@Blocker",blocker);q.Parameters.AddWithValue("@Needle","%"+needle+"%");return Convert.ToInt32(q.ExecuteScalar());}
    }
    static void Cancel(string connection,SqlConnection observer,IAsyncJournalPostService service,JournalPostRequest request,OperationActor actor,string phase)
    {
        string before=Counts(observer);
        using(var blocker=new SqlConnection(connection))
        {
            blocker.Open();int blockerId=Scalar(blocker,"SELECT @@SPID");
            using(var tx=blocker.BeginTransaction())
            using(var cts=new CancellationTokenSource())
            {
                try
                {
                    string lockSql=phase=="accounts"?"SELECT TOP(1) AccountCode FROM dbo.AccountsTb WITH(TABLOCKX,HOLDLOCK)":"SELECT TOP(1) AuditId FROM dbo.AuditLogTb WITH(TABLOCKX,HOLDLOCK)";
                    string needle=phase=="accounts"?"AccountsTb WHERE AccountCode":"INSERT INTO dbo.AuditLogTb";
                    using(var q=new SqlCommand(lockSql,blocker,tx))q.ExecuteScalar();
                    var writing=service.PostAsync(request,actor,cts.Token);
                    bool observed=false;var watch=Stopwatch.StartNew();
                    while(!observed&&!writing.IsCompleted&&watch.ElapsedMilliseconds<10000){observed=Blocked(observer,blockerId,needle)>0;if(!observed)Thread.Sleep(5);}
                    Check(observed,"Native "+phase+" SQL lock not observed.");
                    Check(Partial(observer,request,false)==1,"Uncommitted header not observed.");
                    Check(Partial(observer,request,true)==(phase=="accounts"?0:2),"Unexpected partial line stage.");
                    var stop=Stopwatch.StartNew();cts.Cancel();
                    while((!writing.IsCompleted||Blocked(observer,blockerId,needle)>0)&&stop.ElapsedMilliseconds<8000)Thread.Sleep(5);
                    Check(writing.IsCompleted&&Blocked(observer,blockerId,needle)==0&&stop.ElapsedMilliseconds<8000,"Native command did not cancel before unlocking.");
                    OperationCanceledException cancelled=null;try{writing.GetAwaiter().GetResult();}catch(OperationCanceledException error){cancelled=error;}
                    Check(cancelled!=null&&cancelled.CancellationToken==cts.Token,"Cancellation was rejected/correctable or lost original token.");
                    Check(Partial(observer,request,false)==0&&Partial(observer,request,true)==0,"Native partial journal did not roll back before unlocking.");
                    Console.WriteLine("PASS native journal cancellation: kind="+request.SourceType+"; phase="+phase+"; partial header/lines observed; command and rows gone before unlock; elapsed_ms="+stop.ElapsedMilliseconds);
                }
                finally{tx.Rollback();}
            }
        }
        Check(Counts(observer)==before,"Cancellation changed finance/audit counts.");
    }
    static JournalPostRequest Request(string kind,string identity,string account)=>new JournalPostRequest(new DateTime(1900,1,1),kind,identity,"بيان قيد أصلي "+identity,new[]{new JournalLineValues(account,1250.50m,0m,"سطر مدين",null),new JournalLineValues(account,0m,1250.50m,"سطر دائن",null)});
    static int Main(string[] args)
    {
        try
        {
            Check(args.Length==2,"Owned database/token required.");string database=args[0],owner=args[1];Guid ownerGuid;
            Check(Guid.TryParseExact(owner,"N",out ownerGuid)&&ownerGuid!=Guid.Empty&&System.Text.RegularExpressions.Regex.IsMatch(database,@"^PharmacyRestore_[0-9]{14}_[a-f0-9]{32}_qa$")&&database.EndsWith("_"+owner+"_qa",StringComparison.Ordinal),"Invalid owned restore identity.");
            string connection="Data Source=.;Initial Catalog="+database+";Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Pooling=False;Connect Timeout=10;Application Name=Pharmacy.NativeJournalWriteOwnedSql";
            using(var c=new SqlConnection(connection))
            {
                c.Open();using(var q=new SqlCommand("SELECT CONVERT(nvarchar(128),value) FROM sys.extended_properties WHERE class=0 AND name=N'QaTest.Owner'",c))Check(Convert.ToString(q.ExecuteScalar())==owner,"Owner marker mismatch.");
                string before=Counts(c),account;using(var q=new SqlCommand("SELECT TOP(1) AccountCode FROM dbo.AccountsTb WHERE IsActive=1 ORDER BY AccountCode",c))account=Convert.ToString(q.ExecuteScalar());
                Check(!string.IsNullOrEmpty(account),"Active account required.");
                foreach(string kind in new[]{"Manual","OpeningEntry"})
                {
                    string original=Guid.NewGuid().ToString("N"),concurrent=Guid.NewGuid().ToString("N"),user="QA Native Journal "+original;
                    var actor=new OperationActor{UserName=user,UserRole="Manager"};
                    IAsyncJournalPostService service=new JournalPostService(new SqlJournalPostRepository(()=>new SqlConnection(connection),()=>true),a=>a!=null&&a.UserName==user);
                    var request=Request(kind,original,account);
                    try
                    {
                        Cancel(connection,c,service,request,actor,"accounts");
                        Cancel(connection,c,service,request,actor,"audit");
                        var accepted=service.PostAsync(request,actor,CancellationToken.None).GetAwaiter().GetResult();Check(accepted.Id>0&&!accepted.AlreadyPosted,"Same cancelled request did not resume once.");
                        string acceptedCounts=Counts(c);
                        for(int replay=0;replay<2;replay++){var known=service.PostAsync(request,actor,CancellationToken.None).GetAwaiter().GetResult();Check(known.Id==accepted.Id&&known.AlreadyPosted,"Native replay lost accepted ID.");Check(Counts(c)==acceptedCounts,"Replay repeated financial/audit effects.");}
                        bool changedDenied=false;try{service.PostAsync(new JournalPostRequest(request.Date,kind,original,"changed",request.Lines),actor,CancellationToken.None).GetAwaiter().GetResult();}catch(JournalAcceptedRequestConflictException){changedDenied=true;}
                        Check(changedDenied&&Counts(c)==acceptedCounts,"Changed accepted payload was not rejected without writes.");
                        var first=Request(kind,concurrent,account);var tasks=Enumerable.Range(0,4).Select(_=>service.PostAsync(first,actor,CancellationToken.None)).ToArray();
                        Check(Task.WaitAll(tasks,15000),"Concurrent native first submissions timed out.");int id=tasks[0].Result.Id;
                        Check(id>0&&tasks.All(t=>t.Result.Id==id)&&tasks.Count(t=>!t.Result.AlreadyPosted)==1,"Concurrent first submissions created multiple journals.");
                        using(var q=new SqlCommand("SELECT COUNT(*) FROM dbo.JournalEntriesTb WHERE SourceType=@Kind AND SourceId IN(@Original,@Concurrent) AND CreatedBy=@User",c)){q.Parameters.AddWithValue("@Kind",kind);q.Parameters.AddWithValue("@Original",original);q.Parameters.AddWithValue("@Concurrent",concurrent);q.Parameters.AddWithValue("@User",user);Check(Convert.ToInt32(q.ExecuteScalar())==2,"Exact native owned journal cardinality failed.");}
                        Console.WriteLine("PASS native journal replay/concurrency: kind="+kind+"; same original identity resumed once, two replays, changed payload denied, four concurrent first submissions one additional journal.");
                    }
                    finally
                    {
                        using(var tx=c.BeginTransaction())
                        using(var q=new SqlCommand("DELETE FROM dbo.JournalEntryLinesTb WHERE JournalEntryId IN(SELECT JournalEntryId FROM dbo.JournalEntriesTb WHERE SourceType=@Kind AND SourceId IN(@Original,@Concurrent) AND CreatedBy=@User);DELETE FROM dbo.AuditLogTb WHERE UserName=@User AND ActionName=N'JournalEntryCreated' AND EntityName=N'Accounting';DELETE FROM dbo.JournalEntriesTb WHERE SourceType=@Kind AND SourceId IN(@Original,@Concurrent) AND CreatedBy=@User;",c,tx))
                        {q.Parameters.AddWithValue("@Kind",kind);q.Parameters.AddWithValue("@Original",original);q.Parameters.AddWithValue("@Concurrent",concurrent);q.Parameters.AddWithValue("@User",user);q.ExecuteNonQuery();tx.Commit();}
                    }
                    Check(Counts(c)==before,"Owned native journal cleanup changed counts.");
                }
            }
            Console.WriteLine("PASS native journal owned SQL: checks="+checks+"; four cancellations before unlock, two original identities resumed once each, two additional concurrent identities, no UI/physical input.");return 0;
        }
        catch(Exception error){Console.Error.WriteLine(error.ToString());return 1;}
    }
}

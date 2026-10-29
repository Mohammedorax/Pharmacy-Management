using System;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Application;
using PharmacyManagement.Domain;
using PharmacyManagement.Infrastructure.SqlServer;
class JournalCommitContextOwnedSql
{
    static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    static int Scalar(SqlConnection c,string sql){using(var q=new SqlCommand(sql,c)){q.CommandTimeout=10;return Convert.ToInt32(q.ExecuteScalar());}}
    static string Counts(SqlConnection c){return Scalar(c,"SELECT COUNT(*) FROM dbo.JournalEntriesTb")+"|"+Scalar(c,"SELECT COUNT(*) FROM dbo.JournalEntryLinesTb")+"|"+Scalar(c,"SELECT COUNT(*) FROM dbo.AuditLogTb");}
    static int Main(string[] args)
    {
        try
        {
            bool factory=args.Length==4&&args[3]=="factory";Check(args.Length==3||factory,"Expected database, owner token, expectation and optional factory mode.");
            string database=args[0],owner=args[1];bool expected=args[2]=="baseline-accepts";
            Check(expected||args[2]=="guard-rejects","Unknown expectation.");
            Check(System.Text.RegularExpressions.Regex.IsMatch(database,@"^PharmacyRestore_[0-9]{14}_[a-f0-9]{32}_qa$")&&database.EndsWith("_"+owner+"_qa",StringComparison.Ordinal)&&owner.Length==32,"Owned restored QA identity required.");
            string connection="Data Source=.;Initial Catalog="+database+";Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Pooling=False;Connect Timeout=10;Application Name=Pharmacy.JournalContextProbe";
            using(var c=new SqlConnection(connection))
            {
                c.Open();
                using(var q=new SqlCommand("SELECT CONVERT(nvarchar(128),value) FROM sys.extended_properties WHERE class=0 AND name=N'QaTest.Owner'",c))
                    Check(Convert.ToString(q.ExecuteScalar())==owner,"Ownership marker mismatch.");
                string before=Counts(c);
                foreach(string kind in new[]{"Manual","OpeningEntry"})
                {
                    string identity=Guid.NewGuid().ToString("N"),user="QA Journal Context "+identity;
                    string account;using(var q=new SqlCommand("SELECT TOP(1) AccountCode FROM dbo.AccountsTb WHERE IsActive=1 ORDER BY AccountCode",c))account=Convert.ToString(q.ExecuteScalar());
                    Check(!string.IsNullOrEmpty(account),"Active account required.");
                    int current=1;Func<bool> isCurrent=()=>Volatile.Read(ref current)==1;
                    var ctor=typeof(SqlJournalPostRepository).GetConstructor(new[]{typeof(Func<SqlConnection>),typeof(Func<bool>)});
                    var repo=ctor==null?new SqlJournalPostRepository(()=>new SqlConnection(connection)):(SqlJournalPostRepository)ctor.Invoke(new object[]{new Func<SqlConnection>(()=>new SqlConnection(connection)),isCurrent});
                    if(!expected&&!factory)Check(ctor!=null,"Commit context constructor missing.");
                    IJournalPostService service=new JournalPostService(repo,authorized=>isCurrent()&&authorized!=null&&authorized.UserName==user);
                    OperationActor actor=new OperationActor{UserName=user,UserRole="Manager"};
                    Action invalidate=()=>Volatile.Write(ref current,0);string employee=null,bootstrap=null;
                    if(factory)
                    {
                        var assembly=System.Reflection.Assembly.LoadFrom(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"project_fo_3.exe"));
                        var flags=System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic;
                        var databaseType=assembly.GetType("project_fo_3.Database",true);
                        string pinned=(string)databaseType.GetProperty("ConnectionString",flags).GetValue(null,null);
                        Check(new SqlConnectionStringBuilder(pinned).InitialCatalog==database,"Factory source not pinned to owned clone.");
                        var sessionType=assembly.GetType("project_fo_3.SessionContext",true);
                        var start=sessionType.GetMethod("Start",flags);
                        employee="QAN-"+identity.Substring(0,12);bootstrap="QA Auth Bootstrap "+identity;
                        start.Invoke(null,new object[]{bootstrap,"Manager","QA-CONTEXT"});
                        var rootType=assembly.GetType("project_fo_3.ApplicationServices",true);
                        Func<string,object> root=n=>rootType.GetProperty(n,flags)!=null?rootType.GetProperty(n,flags).GetValue(null,null):rootType.GetField(n,flags).GetValue(null);
                        var employees=root("Employees");string password=Guid.NewGuid().ToString("N");
                        employees.GetType().GetMethod("Create").Invoke(employees,new object[]{new EmployeeInput{EmployeeId=employee,UserName=user,Age=30,Salary=0,Phone="000",Role="Manager",NewPassword=password},new OperationActor{UserName=bootstrap,UserRole="Manager"}});
                        Action authenticate=()=>{
                            var auth=root("Authentication");var authenticated=(AuthenticationResult)auth.GetType().GetMethod("Authenticate").Invoke(auth,new object[]{new AuthenticationRequest{UserName=user,Password=password}});
                            Check(authenticated.Status==AuthenticationStatus.Success&&authenticated.EmployeeId==employee&&authenticated.Role=="Manager","Actual owned SQL authentication failed.");
                            start.Invoke(null,new object[]{authenticated.UserName,authenticated.Role,authenticated.EmployeeId});
                        };
                        authenticate();
                        var services=assembly.GetType("project_fo_3.ApplicationServices",true);
                        var session=services.GetMethod("CreateJournalPostSession",flags).Invoke(null,null);
                        var instance=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public;
                        var valid=session.GetType().GetProperty("IsCurrent",instance);
                        Check((bool)valid.GetValue(session,null),"Actual captured writer not current before SQL.");
                        service=(IJournalPostService)session.GetType().GetField("Service",instance).GetValue(session);
                        actor=(OperationActor)session.GetType().GetField("Actor",instance).GetValue(session);
                        invalidate=()=>{authenticate();Check(!(bool)valid.GetValue(session,null),"Same-user session revision did not invalidate captured factory writer.");};
                    }
                    var request=new JournalPostRequest(new DateTime(1900,1,1),kind,identity,"Owned before-commit context "+identity,new[]{new JournalLineValues(account,1m,0m,"debit",null),new JournalLineValues(account,0m,1m,"credit",null)});
                    Task<JournalPostResult> writing=null;Exception error=null;JournalPostResult result=null;
                    try
                    {
                        using(var blocker=new SqlConnection(connection))
                        {
                            blocker.Open();int blockerId=Scalar(blocker,"SELECT @@SPID");
                            using(var transaction=blocker.BeginTransaction())
                            {
                                try
                                {
                                    using(var q=new SqlCommand("SELECT TOP(1) AccountCode FROM dbo.AccountsTb WITH(TABLOCKX,HOLDLOCK)",blocker,transaction))q.ExecuteScalar();
                                    var native=service as IAsyncJournalPostService;Check(native!=null,"Native service missing.");writing=native.PostAsync(request,actor,CancellationToken.None);
                                    bool observed=false;var watch=Stopwatch.StartNew();
                                    while(!observed&&!writing.IsCompleted&&watch.ElapsedMilliseconds<10000)
                                    {
                                        using(var q=new SqlCommand("SELECT COUNT(*) FROM sys.dm_exec_requests r CROSS APPLY sys.dm_exec_sql_text(r.sql_handle) t WHERE r.database_id=DB_ID() AND r.blocking_session_id=@Blocker AND r.wait_type LIKE N'LCK_M%' AND t.text LIKE N'%AccountsTb WHERE AccountCode%'",c))
                                        {q.Parameters.AddWithValue("@Blocker",blockerId);observed=Convert.ToInt32(q.ExecuteScalar())>0;}
                                        if(!observed)Thread.Sleep(10);
                                    }
                                    Check(observed,"Before-commit SQL lock not observed.");invalidate();
                                }
                                finally{transaction.Rollback();}
                            }
                        }
                        Check(writing.Wait(15000),"Owned write timeout.");result=writing.GetAwaiter().GetResult();
                    }
                    catch(AggregateException caught){error=caught.GetBaseException();}
                    catch(Exception caught){error=caught;}
                    try
                    {
                        if(expected)Check(error==null&&result!=null&&result.Id>0,"Baseline did not accept stale context.");
                        else Check(error is UnauthorizedAccessException&&result==null,"Guard did not reject stale context.");
                        using(var q=new SqlCommand("SELECT COUNT(*) FROM dbo.JournalEntriesTb WHERE SourceType=@Kind AND SourceId=@Identity AND CreatedBy=@User",c))
                        {q.Parameters.AddWithValue("@Kind",kind);q.Parameters.AddWithValue("@Identity",identity);q.Parameters.AddWithValue("@User",user);Check(Convert.ToInt32(q.ExecuteScalar())==(expected?1:0),"Unexpected durable journal count.");}
                        if(factory&&!expected)
                        {
                            var assembly=System.Reflection.Assembly.LoadFrom(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"project_fo_3.exe"));
                            var flags=System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic;
                            var fresh=assembly.GetType("project_fo_3.ApplicationServices",true).GetMethod("CreateJournalPostSession",flags).Invoke(null,null);
                            var instance=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic;
                            var freshService=(IAsyncJournalPostService)fresh.GetType().GetField("Service",instance).GetValue(fresh);
                            var freshActor=(OperationActor)fresh.GetType().GetField("Actor",instance).GetValue(fresh);
                            string beforeResume=Counts(c);
                            var accepted=freshService.PostAsync(request,freshActor,CancellationToken.None).GetAwaiter().GetResult();
                            Check(accepted.Id>0&&!accepted.AlreadyPosted,"Fresh authenticated writer did not accept original request.");
                            string once=Counts(c);
                            var replay=freshService.PostAsync(request,freshActor,CancellationToken.None).GetAwaiter().GetResult();
                            Check(replay.Id==accepted.Id&&replay.AlreadyPosted&&Counts(c)==once,"Authenticated replay duplicated effects.");
                            using(var q=new SqlCommand("SELECT COUNT(*) FROM dbo.JournalEntriesTb WHERE SourceType=@Kind AND SourceId=@Identity AND CreatedBy=@User",c))
                            {q.Parameters.AddWithValue("@Kind",kind);q.Parameters.AddWithValue("@Identity",identity);q.Parameters.AddWithValue("@User",user);Check(Convert.ToInt32(q.ExecuteScalar())==1,"Original authenticated identity not accepted once.");}
                            Check(once!=beforeResume,"No persisted acceptance observed.");
                            Console.WriteLine("PASS actual authenticated resume/replay: kind="+kind+"; original request accepted once by fresh factory; replay same id without effects.");
                        }
                        Console.WriteLine("PASS journal context probe: kind="+kind+"; observed_lock=True; factory="+factory+"; stale_accepted="+expected+"; error="+(error==null?"none":error.GetType().Name));
                    }
                    finally
                    {
                        using(var tx=c.BeginTransaction())
                        using(var q=new SqlCommand("DELETE FROM dbo.JournalEntryLinesTb WHERE JournalEntryId IN(SELECT JournalEntryId FROM dbo.JournalEntriesTb WHERE SourceType=@Kind AND SourceId=@Identity AND CreatedBy=@User); DELETE FROM dbo.AuditLogTb WHERE UserName=@User AND ActionName=N'JournalEntryCreated' AND EntityName=N'Accounting'; DELETE FROM dbo.JournalEntriesTb WHERE SourceType=@Kind AND SourceId=@Identity AND CreatedBy=@User;",c,tx))
                        {q.Parameters.AddWithValue("@Kind",kind);q.Parameters.AddWithValue("@Identity",identity);q.Parameters.AddWithValue("@User",user);q.ExecuteNonQuery();tx.Commit();}
                    }
                                        if(employee!=null)
                    {
                        using(var tx=c.BeginTransaction())using(var q=new SqlCommand("DELETE FROM dbo.EmployeeOperationPermissionsTb WHERE EmpId=@Employee;DELETE FROM dbo.EmployeeTb1 WHERE EmpId=@Employee AND EmpName=@User;DELETE FROM dbo.AuditLogTb WHERE UserName IN(@User,@Bootstrap);",c,tx))
                        {q.Parameters.AddWithValue("@Employee",employee);q.Parameters.AddWithValue("@User",user);q.Parameters.AddWithValue("@Bootstrap",bootstrap);q.ExecuteNonQuery();tx.Commit();}
                    }
                    Check(Counts(c)==before,"Financial counts changed after owned cleanup.");
                }
            }
            Console.WriteLine("PASS journal commit context probe; two source types; expected="+args[2]+"; owned cleanup counts unchanged; no desktop UI.");return 0;
        }
        catch(Exception error){Console.Error.WriteLine(error.GetType().Name+": "+error.Message);return 1;}
    }
}

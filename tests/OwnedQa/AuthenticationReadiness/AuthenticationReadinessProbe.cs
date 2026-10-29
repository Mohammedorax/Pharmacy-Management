using System;
using System.Data;
using System.Data.SqlClient;
using System.Reflection;
using System.Threading;
using PharmacyManagement.Application;
using project_fo_3;
static class AuthenticationReadinessProbe
{
 const string Password="OwnedReadiness-2026!";
 static string owner,mode;static string[] databases,connections,users,ids;static int checks,gaps,cases;
 static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
 static SqlConnection Open(string cs){var c=new SqlConnection(cs);c.Open();return c;}
 static object Scalar(SqlConnection c,string sql,params object[] args){using(var q=c.CreateCommand()){q.CommandText=sql;q.CommandTimeout=20;for(int i=0;i<args.Length;i++)q.Parameters.AddWithValue("@p"+i,args[i]);return q.ExecuteScalar();}}
 static AuthenticationResult Auth(int index,string password=Password){return ApplicationServices.CreateAuthenticationSession(connections[index]).Authenticate(new AuthenticationRequest{UserName=users[index],Password=password});}
 static void Successful(AuthenticationResult result,int index){Check(result!=null&&result.Status==AuthenticationStatus.Success&&result.EmployeeId==ids[index]&&result.Role==(index==1?"Cashier":"Manager"),"Exact actual SQL actor "+index);}
 static void Reset(){var method=typeof(Database).GetMethod("ResetOperationalReadiness",BindingFlags.Static|BindingFlags.NonPublic);if(method!=null)method.Invoke(null,null);else Check(mode=="baseline","Current cache invalidation method required");}
 static void Locked(int index,string table,bool expectBlock,string label,Action duringBlocked=null){
  Check(table=="AccountsTb"||table=="CostCentersTb"||table=="TaxSettingsTb"||table=="CurrencySettingsTb","Allowlisted owned table lock");
  using(var blocker=Open(connections[index]))using(var observer=Open(connections[index]))using(var tx=blocker.BeginTransaction()){
   using(var q=blocker.CreateCommand()){q.Transaction=tx;q.CommandText="SELECT COUNT(*) FROM dbo.["+table+"] WITH(TABLOCKX,HOLDLOCK)";q.ExecuteScalar();}
   int spid;using(var q=blocker.CreateCommand()){q.Transaction=tx;q.CommandText="SELECT @@SPID";spid=Convert.ToInt32(q.ExecuteScalar());}
   AuthenticationResult result=null;Exception error=null;var worker=new Thread(()=>{try{result=Auth(index);}catch(Exception ex){error=ex;}});worker.Start();
   bool blocked=false,validationBlocked=false,finishedBeforeRelease=false;
   try{
    var until=DateTime.UtcNow.AddSeconds(5);
    while(worker.IsAlive&&DateTime.UtcNow<until){
     blocked=Convert.ToInt32(Scalar(observer,"SELECT COUNT(*) FROM sys.dm_exec_requests WHERE blocking_session_id=@p0",spid))>0;
     if(blocked){validationBlocked=Convert.ToInt32(Scalar(observer,"SELECT COUNT(*) FROM sys.dm_exec_requests r CROSS APPLY sys.dm_exec_sql_text(r.sql_handle) t WHERE r.blocking_session_id=@p0 AND t.text LIKE N'%AS IsHealthy%'",spid))>0;break;}
     Thread.Sleep(10);
    }
    finishedBeforeRelease=!worker.IsAlive;
    if(blocked&&duringBlocked!=null)duringBlocked();
   }finally{tx.Rollback();Check(worker.Join(15000),"Original lock worker joined after release");}
   if(error!=null)throw error;Successful(result,index);
   if(expectBlock)Check(blocked&&validationBlocked&&!finishedBeforeRelease,"Unvalidated or invalidated connection must perform actual schema validation");
   else{
    bool correct=finishedBeforeRelease&&!blocked;if(!correct)gaps++;
    Check(mode=="baseline"?!correct:correct,"Cached readiness must avoid unrelated lock without caching credentials");
   }
   Check(Convert.ToInt32(Scalar(observer,"SELECT COUNT(*) FROM sys.dm_exec_requests WHERE blocking_session_id=@p0",spid))==0,"No owned blocked request remains");
   cases++;Console.WriteLine("CASE "+label+" schemaBlocked="+validationBlocked+" authenticatedBeforeRelease="+finishedBeforeRelease+" expectedFreshValidation="+expectBlock);
  }
 }
 static void TaxFailureThenRecovery(){
  using(var c=Open(connections[2])){
   var tax=new DataTable();using(var q=c.CreateCommand()){q.CommandText="SELECT * FROM dbo.TaxSettingsTb";using(var a=new SqlDataAdapter(q))a.Fill(tax);}Check(tax.Rows.Count>0,"Original owned tax rows retained");
   Scalar(c,"DELETE dbo.TaxSettingsTb");
   bool failed=false;
   try{try{Auth(2);}catch(InvalidOperationException){failed=true;}Check(failed,"Missing required tax seed fails readiness");}
   finally{using(var bulk=new SqlBulkCopy(c,SqlBulkCopyOptions.KeepIdentity,null)){bulk.DestinationTableName="dbo.TaxSettingsTb";foreach(DataColumn col in tax.Columns)bulk.ColumnMappings.Add(col.ColumnName,col.ColumnName);bulk.WriteToServer(tax);}}
   cases++;Successful(Auth(2),2);cases++;Console.WriteLine("CASE failed validation not cached; original tax rows restored; real SQL authentication recovered");
   Check(Convert.ToInt32(Scalar(c,"SELECT COUNT(*) FROM dbo.CurrencySettingsTb WHERE CurrencyCode=N'USD'"))==1,"Existing USD fixture");
   int original=Convert.ToInt32(Scalar(c,"SELECT CurrencyId FROM dbo.CurrencySettingsTb WHERE IsSystemCurrency=1"));
   using(var tx=c.BeginTransaction()){
    using(var q=c.CreateCommand()){q.Transaction=tx;q.CommandText="UPDATE dbo.CurrencySettingsTb SET IsSystemCurrency=0 WHERE IsSystemCurrency=1; UPDATE dbo.CurrencySettingsTb SET IsSystemCurrency=1 WHERE CurrencyCode=N'USD'";q.ExecuteNonQuery();}tx.Commit();
   }
   bool valid=false;
   try{Reset();try{var result=Auth(2);Successful(result,2);valid=true;}catch(InvalidOperationException){if(mode!="baseline")throw;}}
   finally{using(var tx=c.BeginTransaction()){using(var q=c.CreateCommand()){q.Transaction=tx;q.CommandText="UPDATE dbo.CurrencySettingsTb SET IsSystemCurrency=0 WHERE IsSystemCurrency=1; UPDATE dbo.CurrencySettingsTb SET IsSystemCurrency=1 WHERE CurrencyId=@id";q.Parameters.AddWithValue("@id",original);q.ExecuteNonQuery();}tx.Commit();}Reset();}
   if(!valid)gaps++;Check(mode=="baseline"?!valid:valid,"Selected supported system currency must not prevent fresh login");cases++;Console.WriteLine("CASE USD fresh readiness accepted="+valid+"; original system currency restored");
   Reset();Scalar(c,"UPDATE dbo.CurrencySettingsTb SET IsSystemCurrency=0 WHERE IsSystemCurrency=1");bool absentRejected=false;
   try{try{Auth(2);}catch(InvalidOperationException){absentRejected=true;}Check(absentRejected,"Missing system currency remains rejected");}
   finally{Scalar(c,"UPDATE dbo.CurrencySettingsTb SET IsSystemCurrency=1 WHERE CurrencyId=@p0",original);Reset();}
   cases++;Successful(Auth(2),2);cases++;Console.WriteLine("CASE missing system currency rejected; original row restored; actual authentication recovered");
  }
 }
 static int Main(string[] args){
  try{
   Check(args.Length==5,"Owned owner/A/B/C/mode required");owner=args[0];databases=new[]{args[1],args[2],args[3]};mode=args[4];Check(mode=="baseline"||mode=="fixed","Known mode");connections=new string[3];users=new string[3];ids=new string[3];
   for(int i=0;i<3;i++){
    string side=((char)('A'+i)).ToString();Check(databases[i]=="PharmacyAuthReady_"+owner+"_"+side+"_qa","Exact owned target");
    connections[i]=new SqlConnectionStringBuilder{DataSource=".",InitialCatalog=databases[i],IntegratedSecurity=true,Encrypt=true,TrustServerCertificate=true,Pooling=false}.ConnectionString;
    users[i]="ready-"+owner.Substring(0,8)+"-"+mode+"-"+side;ids[i]="rd-"+owner.Substring(0,8)+"-"+mode+"-"+side;
    using(var c=Open(connections[i])){Check(Convert.ToString(Scalar(c,"SELECT value FROM sys.extended_properties WHERE class=0 AND name=N'QaAuthReady.Owner'"))==owner,"SQL ownership marker");Check(Convert.ToInt32(Scalar(c,"SELECT COUNT(*) FROM dbo.EmployeeTb1 WHERE EmpId=@p0",ids[i]))==0,"Fresh owned actor");Scalar(c,"INSERT dbo.EmployeeTb1(EmpId,EmpName,EmpPassword,EmpRole,EmpSal,EmpAge,EmpPhone) VALUES(@p0,@p1,@p2,@p3,0,30,N'000')",ids[i],users[i],Password,i==1?"Cashier":"Manager");}
   }
   SessionContext.Clear();Successful(Auth(0),0);cases++;
   foreach(string table in new[]{"AccountsTb","CostCentersTb","TaxSettingsTb","CurrencySettingsTb"})Locked(0,table,false,"WarmA-"+table);
   Locked(1,"AccountsTb",true,"UnvalidatedB-does-not-inherit-A");
   Locked(1,"AccountsTb",false,"WarmB-AccountsTb");
   Reset();Locked(0,"AccountsTb",true,"ExplicitInvalidation");
   Reset();Locked(0,"AccountsTb",true,"InvalidationDuringValidation",()=>Reset());
   Locked(0,"AccountsTb",true,"InFlightValidationDoesNotRefillNewGeneration");
   Locked(0,"AccountsTb",false,"NewGenerationWarmA");
   using(var c=Open(connections[1])){Scalar(c,"UPDATE dbo.EmployeeTb1 SET EmpRole=N'Inventory' WHERE EmpId=@p0",ids[1]);var role=Auth(1);Check(role.Status==AuthenticationStatus.Success&&role.Role=="Inventory","Role read remains fresh despite cached schema");Scalar(c,"UPDATE dbo.EmployeeTb1 SET EmpRole=N'Cashier' WHERE EmpId=@p0",ids[1]);}
   cases++;
   using(var c=Open(connections[0])){long before=Convert.ToInt64(Scalar(c,"SELECT COUNT(*) FROM dbo.AuditLogTb"));Check(Auth(0,"OwnedWrongDummy").Status==AuthenticationStatus.InvalidCredentials,"Password still checked each attempt");Check(before==Convert.ToInt64(Scalar(c,"SELECT COUNT(*) FROM dbo.AuditLogTb")),"Wrong password no audit");}cases++;
   TaxFailureThenRecovery();
   Check(gaps==(mode=="baseline"?7:0),"Exact readiness characterization gaps");Console.WriteLine("RESULT mode="+mode+" Cases="+cases+" Checks="+checks+" ProductGaps="+gaps+"; actual SQL, no ProgramData/remember store/UI, tax/currency rows restored");return 0;
  }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}finally{SessionContext.Clear();}
 }
}

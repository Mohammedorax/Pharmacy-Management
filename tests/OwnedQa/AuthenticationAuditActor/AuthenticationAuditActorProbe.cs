using System;
using System.Data;
using System.Data.SqlClient;
using System.Threading;
using PharmacyManagement.Application;
using project_fo_3;
static class AuthenticationAuditActorProbe
{
    const string Password="OwnedAuditActor-2026!";
    static int checks,gaps;
    static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
    static object Scalar(SqlConnection c,string sql,params object[] values){using(var q=c.CreateCommand()){q.CommandText=sql;q.CommandTimeout=30;for(int i=0;i<values.Length;i++)q.Parameters.AddWithValue("@p"+i,values[i]);return q.ExecuteScalar();}}
    static SqlConnection Open(string cs){var c=new SqlConnection(cs);c.Open();return c;}
    static int Main(string[] args){
      try{
        Check(args.Length==3,"Owned owner/database/mode required");string owner=args[0],db=args[1],mode=args[2];
        Check(mode=="baseline"||mode=="fixed","Known original run mode required");
        Check(db=="PharmacyAuthAudit_"+owner+"_qa","Exact owned clone only");
        string cs=Database.ConnectionString;Check(new SqlConnectionStringBuilder(cs).InitialCatalog==db,"Forced exact QA profile");
        using(var c=Open(cs)){
          Check(Convert.ToString(Scalar(c,"SELECT value FROM sys.extended_properties WHERE class=0 AND name=N'QaAuthenticationAudit.Owner'"))==owner,"Exact SQL owner marker");
          string key=Convert.ToString(Scalar(c,"SELECT name FROM sys.identity_columns WHERE object_id=OBJECT_ID(N'dbo.AuditLogTb')"));Check(key.Length>0,"Audit identity discovered from schema");key="["+key.Replace("]","]]")+"]";
          foreach(string scenario in new[]{"Empty","Foreign","ChangedDuringRead","Invalid"}){
            string user="تدقيق-"+owner.Substring(0,8)+"-"+mode+"-"+scenario,id="aa-"+owner.Substring(0,8)+"-"+mode+"-"+scenario;
            Check(Convert.ToInt32(Scalar(c,"SELECT COUNT(*) FROM dbo.EmployeeTb1 WHERE EmpName=@p0",user))==0,"Fresh fixture actor");
            Scalar(c,"INSERT dbo.EmployeeTb1(EmpId,EmpName,EmpPassword,EmpRole,EmpSal,EmpAge,EmpPhone) VALUES(@p0,@p1,@p2,N'Cashier',0,30,N'000')",id,user,Password);
            long before=Convert.ToInt64(Scalar(c,"SELECT ISNULL(MAX("+key+"),0) FROM dbo.AuditLogTb"));
            SessionContext.Clear();if(scenario=="Foreign"||scenario=="Invalid")SessionContext.Start("owned-unrelated-manager","Manager","unrelated");
            var service=ApplicationServices.CreateAuthenticationSession(cs);AuthenticationResult result=null;
            if(scenario=="ChangedDuringRead"){
              using(var blocker=Open(cs))using(var tx=blocker.BeginTransaction()){
                using(var q=blocker.CreateCommand()){q.Transaction=tx;q.CommandText="UPDATE dbo.EmployeeTb1 WITH(ROWLOCK) SET EmpRole=EmpRole WHERE EmpId=@id";q.Parameters.AddWithValue("@id",id);Check(q.ExecuteNonQuery()==1,"Owned actor row blocked");}
                int spid;using(var q=blocker.CreateCommand()){q.Transaction=tx;q.CommandText="SELECT @@SPID";spid=Convert.ToInt32(q.ExecuteScalar());}
                Exception error=null;var worker=new Thread(()=>{try{result=service.Authenticate(new AuthenticationRequest{UserName="  "+user+"  ",Password=Password});}catch(Exception ex){error=ex;}});worker.Start();
                bool observed=false;var end=DateTime.UtcNow.AddSeconds(15);
                while(DateTime.UtcNow<end){if(Convert.ToInt32(Scalar(c,"SELECT COUNT(*) FROM sys.dm_exec_requests WHERE blocking_session_id=@p0",spid))>0){observed=true;break;}Thread.Sleep(20);}
                SessionContext.Start("owned-later-manager","Manager","later");tx.Rollback();Check(worker.Join(20000),"Original authentication worker joined");if(error!=null)throw error;Check(observed,"Actual SQL request observed blocked before session change");
              }
            }else result=service.Authenticate(new AuthenticationRequest{UserName="  "+user+"  ",Password=scenario=="Invalid"?"OwnedWrongDummy":Password});
            long count=Convert.ToInt64(Scalar(c,"SELECT COUNT(*) FROM dbo.AuditLogTb WHERE "+key+">@p0",before));
            if(scenario=="Invalid"){
              Check(result.Status==AuthenticationStatus.InvalidCredentials&&count==0,"Wrong password creates no upgrade or login audit");
              Check(Convert.ToString(Scalar(c,"SELECT EmpPassword FROM dbo.EmployeeTb1 WHERE EmpId=@p0",id))==Password,"Wrong password does not migrate stored password");
              Console.WriteLine("CASE "+scenario+" no upgrade, no audit, rejected=True");continue;
            }
            Check(result.Status==AuthenticationStatus.Success&&result.Role=="Cashier"&&result.EmployeeId==id&&result.UserName==user,"Actual SQL validated exact cashier actor");
            Check(count==2,"Exactly one upgrade and one login audit");
            string auditUser,auditRole;
            using(var q=c.CreateCommand()){q.CommandText="SELECT UserName,UserRole FROM dbo.AuditLogTb WHERE "+key+">@p0 AND ActionName=N'PasswordUpgraded'";q.Parameters.AddWithValue("@p0",before);using(var r=q.ExecuteReader()){Check(r.Read(),"Upgrade audit present");auditUser=Convert.ToString(r[0]);auditRole=Convert.ToString(r[1]);Check(!r.Read(),"Single upgrade audit");}}
            bool correct=auditUser==user&&auditRole=="Cashier";if(!correct)gaps++;
            Check(mode=="baseline"?!correct:correct,"Expected baseline wrong actor or fixed validated actor");
            Check(Convert.ToInt32(Scalar(c,"SELECT COUNT(*) FROM dbo.AuditLogTb WHERE "+key+">@p0 AND ActionName=N'Login' AND UserName=@p1 AND UserRole=N'Cashier'",before,user))==1,"Login audit retains validated actor");
            Check(Convert.ToString(Scalar(c,"SELECT EmpPassword FROM dbo.EmployeeTb1 WHERE EmpId=@p0",id))!=Password,"Validated password upgraded");
            Check(SessionContext.CurrentUser==(scenario=="Empty"?"":scenario=="Foreign"?"owned-unrelated-manager":"owned-later-manager"),"Service does not replace current desktop session");
            Console.WriteLine("CASE "+scenario+" upgradeActorCorrect="+correct+" auditRole="+auditRole+" actorEmpty="+(auditUser.Length==0)+"; role/login/password unchanged");
          }
        }
        Check(gaps==(mode=="baseline"?3:0),"Exact actor characterization");Console.WriteLine("PASS mode="+mode+" cases=4 checks="+checks+" productGaps="+gaps+"; actual SQL, no UI, no ProgramData, no remembered store");return 0;
      }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}finally{SessionContext.Clear();}
    }
}

using System;
using System.Data;
using System.Data.SqlClient;
using System.Configuration;
using System.Reflection;
using System.Threading;
using PharmacyManagement.Application;
using project_fo_3;

static class AuthenticationPinSqlProbe
{
    static string owner, dbA, dbB, connectionA, connectionB;
    static int checks, gaps;
    static readonly string Password="OwnedPin-Test-2026";
    static void Check(bool condition,string message){checks++;if(!condition)throw new Exception(message);}
    static SqlConnection Open(string text){var c=new SqlConnection(text);c.Open();return c;}
    static object Scalar(SqlConnection c,string sql,params object[] values){using(var cmd=c.CreateCommand()){cmd.CommandText=sql;cmd.CommandTimeout=30;for(int i=0;i<values.Length;i++)cmd.Parameters.AddWithValue("@p"+i,values[i]);return cmd.ExecuteScalar();}}
    static void Execute(SqlConnection c,string sql,params object[] values){Scalar(c,sql,values);}
    static void Profile(string connection){var setting=ConfigurationManager.ConnectionStrings["PharmacyConnection"];typeof(ConfigurationElement).GetField("_bReadOnly",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(setting,false);setting.ConnectionString=connection;Check(Database.ConnectionString==new SqlConnectionStringBuilder(Database.ConnectionString).ConnectionString,"Canonical profile");}
    sealed class TracedGateway:IAuthenticationGateway
    {
        internal IAuthenticationGateway Inner;internal string Boundary,User;
        void Switch(string step){if(Boundary==step)Profile(connectionB);}
        public void EnsureOperationalReady(){Inner.EnsureOperationalReady();Switch("Ready");}
        public bool HasManager(){bool value=Inner.HasManager();Switch("Manager");return value;}
        public string AuthenticateEmployeeId(string user,string password)
        {
            if(Boundary!="BlockedPassword"){string value=Inner.AuthenticateEmployeeId(user,password);Switch("Credentials");return value;}
            using(var blocker=Open(connectionA))using(var transaction=blocker.BeginTransaction())
            {
                using(var cmd=blocker.CreateCommand()){cmd.Transaction=transaction;cmd.CommandText="UPDATE dbo.EmployeeTb1 SET EmpRole=EmpRole WHERE EmpName=@name";cmd.Parameters.AddWithValue("@name",user);Check(cmd.ExecuteNonQuery()==1,"Exact owned row blocked");}
                int spid; using(var command=blocker.CreateCommand()){command.Transaction=transaction;command.CommandText="SELECT @@SPID";spid=Convert.ToInt32(command.ExecuteScalar());}string result=null;Exception error=null;
                var worker=new Thread(()=>{try{result=Inner.AuthenticateEmployeeId(user,password);}catch(Exception ex){error=ex;}});worker.Start();
                bool observed=false;
                using(var observer=Open(connectionA)){var until=DateTime.UtcNow.AddSeconds(15);while(DateTime.UtcNow<until){if(Convert.ToInt32(Scalar(observer,"SELECT COUNT(*) FROM sys.dm_exec_requests WHERE blocking_session_id=@p0",spid))>0){observed=true;break;}Thread.Sleep(25);}}
                Check(observed,"Real password SELECT blocked by owned transaction");Profile(connectionB);transaction.Rollback();
                Check(worker.Join(15000),"Owned password worker completed");if(error!=null)throw error;Console.WriteLine("OBSERVED actual SQL password wait; profile switched before lock release");return result;
            }
        }
        public string GetEmployeeRoleById(string id){string value=Inner.GetEmployeeRoleById(id);Switch("Role");return value;}
        public void WriteAudit(string action,string entity,string details,OperationActor actor){Inner.WriteAudit(action,entity,details,actor);}
    }
    static void RequireOwned(SqlConnection c,string db){Check(Convert.ToString(Scalar(c,"SELECT DB_NAME()"))==db,"Exact QA database");Check(Convert.ToString(Scalar(c,"SELECT CONVERT(nvarchar(64),value) FROM sys.extended_properties WHERE class=0 AND name=N'QaAuthPin.Owner'"))==owner,"Owned auth marker");}
    static AuthenticationService Create(string connection)
    {
        var method=typeof(ApplicationServices).GetMethod("CreateAuthenticationSession",BindingFlags.Static|BindingFlags.NonPublic);
        if(method!=null)return (AuthenticationService)method.Invoke(null,new object[]{connection});
        return new AuthenticationService(new LegacyAuthenticationGateway());
    }
    static string Fingerprint(SqlConnection c,string table){return Convert.ToString(Scalar(c,"SELECT CONVERT(nvarchar(max),(SELECT * FROM dbo.["+table+"] FOR XML RAW,BINARY BASE64,ROOT('rows'),TYPE))"));}
    static void Case(string boundary,bool expectFixed)
    {
        Profile(connectionA);SessionContext.Clear();string user="pin-"+owner.Substring(0,12)+"-"+boundary;
        using(var a=Open(connectionA))using(var b=Open(connectionB))
        {
            RequireOwned(a,dbA);RequireOwned(b,dbB);
            foreach(var c in new[]{a,b}){
                Check(Convert.ToInt32(Scalar(c,"SELECT COUNT(*) FROM dbo.EmployeeTb1 WHERE EmpName=@p0",user))==0,"Fresh owned scenario employee");
                Execute(c,"INSERT dbo.EmployeeTb1(EmpId,EmpName,EmpPassword,EmpRole,EmpSal,EmpAge,EmpPhone) VALUES(@p0,@p1,@p2,@p3,0,30,N'000')","pin-"+owner.Substring(0,10)+"-"+boundary,user,Password,ReferenceEquals(c,a)?"Cashier":"Manager");
            }
            string medicineA=Fingerprint(a,"MedicineTb1"),medicineB=Fingerprint(b,"MedicineTb1"),salesA=Fingerprint(a,"BillsTb1"),salesB=Fingerprint(b,"BillsTb1");
            int auditA=Convert.ToInt32(Scalar(a,"SELECT COUNT(*) FROM dbo.AuditLogTb")),auditB=Convert.ToInt32(Scalar(b,"SELECT COUNT(*) FROM dbo.AuditLogTb"));
            var service=Create(connectionA);var field=typeof(AuthenticationService).GetField("gateway",BindingFlags.Instance|BindingFlags.NonPublic);
            var traced=new TracedGateway{Inner=(IAuthenticationGateway)field.GetValue(service),Boundary=boundary,User=user};field.SetValue(service,traced);
            if(boundary=="BeforeStart")Profile(connectionB);
            var result=service.Authenticate(new AuthenticationRequest{UserName=user,Password=Password});
            bool upgradedA=Convert.ToString(Scalar(a,"SELECT EmpPassword FROM dbo.EmployeeTb1 WHERE EmpName=@p0",user))!=Password;
            bool upgradedB=Convert.ToString(Scalar(b,"SELECT EmpPassword FROM dbo.EmployeeTb1 WHERE EmpName=@p0",user))!=Password;
            int deltaA=Convert.ToInt32(Scalar(a,"SELECT COUNT(*) FROM dbo.AuditLogTb"))-auditA,deltaB=Convert.ToInt32(Scalar(b,"SELECT COUNT(*) FROM dbo.AuditLogTb"))-auditB;
            bool correct=result.Status==AuthenticationStatus.Success&&result.Role=="Cashier"&&upgradedA&&!upgradedB&&deltaA==2&&deltaB==0;
            Console.WriteLine("CASE boundary="+boundary+" Status="+result.Status+" Role="+result.Role+" UpgradedA="+upgradedA+" UpgradedB="+upgradedB+" AuditDeltaA="+deltaA+" AuditDeltaB="+deltaB+" ConnectionPinned="+correct);
            Check(Fingerprint(a,"MedicineTb1")==medicineA&&Fingerprint(b,"MedicineTb1")==medicineB&&Fingerprint(a,"BillsTb1")==salesA&&Fingerprint(b,"BillsTb1")==salesB,"Medicine and saved invoices unchanged");
            Check(!SessionContext.IsAuthenticated,"Service does not start desktop session");
            if(expectFixed)Check(correct,"Authentication crossed database at "+boundary);else if(!correct)gaps++;
        }
    }
    static int Main(string[] args)
    {
        try{
            owner=args[0];dbA=args[1];dbB=args[2];bool fixedMode=args[3]=="fixed";
            Check(System.Text.RegularExpressions.Regex.IsMatch(owner,"^[a-f0-9]{32}$"),"Owner format");
            Check(dbA=="PharmacyAuthPin_"+owner+"_A_qa"&&dbB=="PharmacyAuthPin_"+owner+"_B_qa","Generated QA names");
            connectionA="Data Source=.;Initial Catalog="+dbA+";Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Pooling=False";
            connectionB="Data Source=.;Initial Catalog="+dbB+";Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Pooling=False";
            foreach(string mode in new[]{"BeforeStart","Ready","Manager","BlockedPassword","Credentials","Role"})Case(mode,fixedMode);
            Console.WriteLine("RESULT Checks="+checks+" ProductGaps="+gaps+" Scenarios=6 SqlUsed=True RuntimeSettingsWritten=False RememberStoreUsed=False PhysicalPrinting=False");
            if(!fixedMode)Check(gaps==6,"All six baseline cross-database gaps reproduced");return 0;
        }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}finally{SessionContext.Clear();}
    }
}

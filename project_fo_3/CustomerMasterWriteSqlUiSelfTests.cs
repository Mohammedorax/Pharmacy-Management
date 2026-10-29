using System;
using System.IO;
using System.Data.SqlClient;
using System.Drawing;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using PharmacyManagement.Application;
using PharmacyManagement.Infrastructure.SqlServer;
namespace project_fo_3
{
    internal static class CustomerMasterWriteSqlUiProbe
    {
        static int checks;static BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
        static object Field(object form,string name)=>form.GetType().GetField(name,flags).GetValue(form);
        static object Call(object form,string name,params object[] args){try{return form.GetType().GetMethod(name,flags).Invoke(form,args);}catch(TargetInvocationException e){throw e.InnerException??e;}}
        static void Check(bool v,string m){if(!v)throw new Exception(m);checks++;}
        static void Tick(){System.Windows.Forms.Application.DoEvents();Thread.Sleep(2);}
        static void Pump(Task task){var end=DateTime.UtcNow.AddSeconds(20);while(!task.IsCompleted&&DateTime.UtcNow<end)Tick();Check(task.IsCompleted,"native UI timeout");task.GetAwaiter().GetResult();}
        static object Scalar(SqlConnection c,string sql,params object[] values){using(var cmd=new SqlCommand(sql,c)){for(int i=0;i<values.Length;i++)cmd.Parameters.AddWithValue("@p"+i,values[i]);return cmd.ExecuteScalar();}}
        static void Execute(SqlConnection c,string sql){using(var cmd=new SqlCommand(sql,c)){cmd.CommandTimeout=30;cmd.ExecuteNonQuery();}}
        static string Snapshot(SqlConnection c)=>Convert.ToString(Scalar(c,"SELECT CONVERT(nvarchar(max),(SELECT * FROM dbo.CustomersBillsTb ORDER BY CustomerId FOR XML RAW,ROOT('customers'),BINARY BASE64))+CONVERT(nvarchar(max),(SELECT * FROM dbo.AuditLogTb ORDER BY AuditId FOR XML RAW,ROOT('audit'),BINARY BASE64));"));
        static CustomerSupplierManagementForm Open(string directory,bool delayed)
        {
            var form=new CustomerSupplierManagementForm();form.CustomerPendingDirectoryOverride=directory;
            if(delayed)form.CustomerMasterWriteSessionFactory=()=>
            {
                var actual=ApplicationServices.CreateCustomerMasterWriteSession();
                var svc=(CustomerMasterWriteService)Field(actual,"service");
                var submit=(Func<OperationActor,bool>)Field(svc,"canSubmit");var resolve=(Func<OperationActor,bool>)Field(svc,"canResolve");
                var ctor=typeof(SqlCustomerMasterWriteRepository).GetConstructor(flags,null,new[]{typeof(Func<SqlConnection>),typeof(bool),typeof(int)},null);
                var repo=(SqlCustomerMasterWriteRepository)ctor.Invoke(new object[]{new Func<SqlConnection>(()=>new SqlConnection(Database.ConnectionString)),true,1});
                return new CustomerMasterWriteSession(new CustomerMasterWriteService(repo,submit,resolve),actual.Actor,actual.SourceIdentity,()=>actual.IsCurrent);
            };
            form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-28000,-28000);form.Opacity=0;form.ShowInTaskbar=false;form.Show();
            var end=DateTime.UtcNow.AddSeconds(20);while((bool)Field(form,"loadingCustomers")&&DateTime.UtcNow<end)Tick();
            Check(!(bool)Field(form,"loadingCustomers"),"native customer list loading");
            return form;
        }
        static void Draft(CustomerSupplierManagementForm form,string suffix)
        {Call(form,"ClearEditor");((TextBox)Field(form,"name")).Text="عميل SQL عربي & "+suffix;((AntdUI.Input)Field(form,"phone")).Text="001"+suffix;((TextBox)Field(form,"address")).Text="عنوان 500 mg";}
        internal static string Run(string server,string database,string owner)
        {
            if(!System.Text.RegularExpressions.Regex.IsMatch(database,@"\APharmacyConcurrency_[0-9]{14}_[0-9a-f]{8}_sandbox\z")||
                !System.Text.RegularExpressions.Regex.IsMatch(owner,@"\A[0-9a-f]{32}\z")||
                !RuntimeSettingsStore.IsBuildConnectionProfileForced||Database.GetConnectionProfile().DatabaseName!=database)
                throw new Exception("Forced generated owned database required.");
            string directory=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"owned-customer-sql-ui-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);File.WriteAllText(Path.Combine(directory,"owner"),owner);
            try
            {
                using(var c=new SqlConnection(Database.ConnectionString))
                {
                    c.Open();Check(Convert.ToString(Scalar(c,"SELECT CONVERT(nvarchar(64),value) FROM sys.extended_properties WHERE class=0 AND name=N'QaTest.Owner'"))==owner,"owned marker");
                    Check(Convert.ToInt32(Scalar(c,"SELECT COUNT(*) FROM sys.tables WHERE name=N'CustomerMasterRequestsTb'"))==0,"fresh migration");
                    SessionContext.Start("qa-native-customer","Manager","owned-ui");
                    string before=Snapshot(c);
                    using(var missing=Open(directory,false))
                    {Draft(missing,"missing-"+owner.Substring(0,8));Pump((Task)Call(missing,"SaveCustomer"));Check(Field(missing,"customerWriteAttempt")==null,"known missing schema rejected: phase="+((CustomerMasterWriteAttempt)Field(missing,"customerWriteAttempt"))?.Phase+"; blocked="+Field(missing,"customerRecoveryBlocked")+"; status="+((Control)Field(missing,"status")).Text);}
                    Check(Snapshot(c)==before,"missing schema wrote nothing");
                    Execute(c,File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"customer_master_request_identity.candidate.sql")));
                    int customers=Convert.ToInt32(Scalar(c,"SELECT COUNT(*) FROM dbo.CustomersBillsTb")),audit=Convert.ToInt32(Scalar(c,"SELECT COUNT(*) FROM dbo.AuditLogTb"));
                    Guid id;string customerId;
                    using(var first=Open(directory,true))
                    {
                        Draft(first,owner.Substring(0,10));Pump((Task)Call(first,"SaveCustomer"));
                        var attempt=(CustomerMasterWriteAttempt)Field(first,"customerWriteAttempt");Check(attempt!=null&&attempt.Phase==CustomerMasterWritePhase.Unknown,"actual timeout retained unknown");id=attempt.Request.RequestId;
                        Check(Convert.ToInt32(Scalar(c,"SELECT COUNT(*) FROM dbo.CustomerMasterRequestsTb WHERE RequestId=@p0 AND Outcome=1",id))==1,"real committed ledger");
                        customerId=Convert.ToString(Scalar(c,"SELECT CustomerId FROM dbo.CustomerMasterRequestsTb WHERE RequestId=@p0",id));
                        Check(Directory.GetFiles(directory,"*.pending").Length==1,"encrypted pending before close");
                    }
                    using(var reopen=Open(directory,false))
                    {
                        var attempt=(CustomerMasterWriteAttempt)Field(reopen,"customerWriteAttempt");Check(attempt!=null&&attempt.Request.RequestId==id,"native reopen same id");
                        Pump((Task)Call(reopen,"ResolveCustomerWrite",false));
                        Check(Field(reopen,"customerWriteAttempt")==null,"native receipt accepted");Check(Directory.GetFiles(directory,"*.pending").Length==0,"receipt clears encrypted record");
                        Check(Convert.ToInt32(Scalar(c,"SELECT COUNT(*) FROM dbo.CustomersBillsTb"))==customers+1,"one customer only");
                        Check(Convert.ToInt32(Scalar(c,"SELECT COUNT(*) FROM dbo.AuditLogTb"))==audit+1,"one audit only");
                        Check((string)Field(reopen,"editingCustomerId")==customerId,"actual saved id selected");

                    }
                    string stamp=owner.Substring(0,10),managerId="m-"+stamp,managerName="manager-"+stamp,cashierId="c-"+stamp,cashierName="cashier-"+stamp;
                    string password="QA-"+Guid.NewGuid().ToString("N");
                    var bootstrap=new OperationActor{UserName=SessionContext.CurrentUser,UserRole=SessionContext.CurrentRole};
                    ApplicationServices.Employees.Create(new EmployeeInput{EmployeeId=managerId,UserName=managerName,Age=30,Salary=0,Phone="000",Role="Manager",NewPassword=password},bootstrap);
                    ApplicationServices.Employees.Create(new EmployeeInput{EmployeeId=cashierId,UserName=cashierName,Age=25,Salary=0,Phone="001",Role="Cashier",NewPassword=password},bootstrap);
                    var manager=ApplicationServices.Authentication.Authenticate(new AuthenticationRequest{UserName=managerName,Password=password});
                    Check(manager.Status==AuthenticationStatus.Success&&manager.EmployeeId==managerId&&manager.Role=="Manager","manager authenticated SQL");
                    SessionContext.Start(manager.UserName,manager.Role,manager.EmployeeId);
                    var admin=new OperationActor{UserName=manager.UserName,UserRole=manager.Role};
                    var grants=new System.Collections.Generic.Dictionary<string,bool>{
                        {"Screen.Sales",true},{"Screen.ManageManufacturers",true},{"ManageSalesCustomers",true}};
                    ApplicationServices.EmployeePermissions.Replace(cashierId,grants,admin);
                    var cashier=ApplicationServices.Authentication.Authenticate(new AuthenticationRequest{UserName=cashierName,Password=password});
                    Check(cashier.Status==AuthenticationStatus.Success&&cashier.EmployeeId==cashierId&&cashier.Role=="Cashier","cashier authenticated SQL");
                    SessionContext.Start(cashier.UserName,cashier.Role,cashier.EmployeeId);
                    var permitted=ApplicationServices.CreateCustomerMasterWriteSession();Check(permitted.IsCurrent,"stored grant permits root");
                    var deniedRequest=new CustomerMasterWriteRequest(Guid.NewGuid(),new SalesCustomer{CustomerName="permission-"+stamp,PhoneNumber="002"+stamp},permitted.Actor);
                    var deniedAttempt=new CustomerMasterWriteAttempt(deniedRequest,permitted.SourceIdentity);
                    var independentAdmin=new EmployeePermissionService(new SqlEmployeePermissionRepository(()=>new SqlConnection(Database.ConnectionString)),
                        actor=>actor.UserName==manager.UserName&&actor.UserRole==manager.Role);
                    grants["ManageSalesCustomers"]=false;
                    independentAdmin.Replace(cashierId,grants,admin);
                    var refreshDeadline=DateTime.UtcNow.AddMilliseconds(5500);while(DateTime.UtcNow<refreshDeadline)Tick();
                    string denyBefore=Snapshot(c);
                    Check(!permitted.IsCurrent,"persisted operation deny invalidates captured root");
                    bool denied=false;try{Pump(permitted.SubmitAsync(deniedAttempt,CancellationToken.None));}catch(UnauthorizedAccessException){denied=true;}
                    Check(denied,"denied actor rejected before send");Check(Snapshot(c)==denyBefore,"denial no customer audit changes");
                    Check(Convert.ToInt32(Scalar(c,"SELECT COUNT(*) FROM dbo.CustomerMasterRequestsTb WHERE RequestId=@p0",deniedRequest.RequestId))==0,"denied no request ledger");
                    var freshDenied=ApplicationServices.CreateCustomerMasterWriteSession();Check(!freshDenied.IsCurrent,"fresh denied root");
                    using(var deniedForm=Open(directory,false))
                    {Check(!((Control)Field(deniedForm,"saveButton")).Enabled&&((TextBox)Field(deniedForm,"name")).ReadOnly,"native denied form read only");}
                    grants["ManageSalesCustomers"]=true;grants["Screen.Sales"]=false;grants["Screen.ManageManufacturers"]=false;
                    independentAdmin.Replace(cashierId,grants,admin);PermissionService.ClearCachedOverrides();
                    Check(!ApplicationServices.CreateCustomerMasterWriteSession().IsCurrent,"operation alone insufficient without screen");
                    grants["Screen.Sales"]=true;independentAdmin.Replace(cashierId,grants,admin);PermissionService.ClearCachedOverrides();
                    Check(!permitted.IsCurrent,"old session stays revoked after regrant");
                    var renewed=ApplicationServices.CreateCustomerMasterWriteSession();Check(renewed.IsCurrent,"fresh regrant allows new root");
                    SessionContext.Clear();Check(!renewed.IsCurrent,"logout revokes employee root");
                    using(var dbcc=new SqlCommand("DBCC CHECKDB WITH NO_INFOMSGS,TABLERESULTS",c))
                    using(var adapter=new System.Data.SqlClient.SqlDataAdapter(dbcc))
                    using(var table=new System.Data.DataTable())
                    {adapter.Fill(table);Check(table.Rows.Count==0,"post write DBCC");}
                    // Source isolation and cleanup are checked by the owning wrapper.
                    return "PASS customer master owned SQL UI recovery: checks="+checks+"; actual default composition/read/receipt, schema rejection without writes, real commit then SQL timeout, encrypted close/reopen and resolved same request without duplicate customer/audit; actual SQL-authenticated Manager/Cashier and persisted grant/revoke/regrant, no write on denial, post DBCC; hidden owned STA; no native keyboard/DPI/printing.";
                }
            }
            finally
            {
                SessionContext.Clear();
                if(File.ReadAllText(Path.Combine(directory,"owner"))==owner)
                {foreach(var file in Directory.GetFiles(directory))File.Delete(file);Directory.Delete(directory,false);}
            }
        }
    }
}



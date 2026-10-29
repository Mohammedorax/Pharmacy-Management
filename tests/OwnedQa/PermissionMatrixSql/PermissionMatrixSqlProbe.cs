using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using PharmacyManagement.Application;
using project_fo_3;
static class PermissionMatrixSqlProbe
{
 const string Password="OwnedPermissionMatrix-2026!";
 static int checks,denials,logins;
 static string manager,cashier,managerId,cashierId;
 static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
 static object Scalar(SqlConnection c,string sql,params object[] args){using(var q=c.CreateCommand()){q.CommandText=sql;for(int i=0;i<args.Length;i++)q.Parameters.AddWithValue("@p"+i,args[i]);return q.ExecuteScalar();}}
 static void Login(string user,string id,string role){SessionContext.Clear();var r=ApplicationServices.Authentication.Authenticate(new AuthenticationRequest{UserName=user,Password=Password});Check(r.Status==AuthenticationStatus.Success&&r.EmployeeId==id&&r.Role==role,"Actual SQL actor authentication");SessionContext.Start(r.UserName,r.Role,r.EmployeeId);logins++;}
 static OperationActor Actor(){return new OperationActor{UserName=SessionContext.CurrentUser,UserRole=SessionContext.CurrentRole};}
 static void Denied(Action action,string name){bool denied=false;try{action();}catch(UnauthorizedAccessException){denied=true;}Check(denied,"Service must reject before validation: "+name);denials++;}
 static Dictionary<string,bool> Matrix(bool allow){var map=new Dictionary<string,bool>();foreach(AppOperation op in Enum.GetValues(typeof(AppOperation)))map.Add(op.ToString(),allow);foreach(AppPermission screen in Enum.GetValues(typeof(AppPermission)))if(EmployeeScreenPermissionKeys.IsDelegableKey("Screen."+screen))map.Add("Screen."+screen,allow);Check(map.Count==22,"Fourteen operations and eight delegable screens");return map;}
 static void Decisions(bool allow){foreach(AppOperation op in Enum.GetValues(typeof(AppOperation)))Check(PermissionService.CanExecute(op)==allow,"Saved operation decision "+op);foreach(AppPermission screen in Enum.GetValues(typeof(AppPermission)))Check(PermissionService.CanOpen(screen)==(allow&&screen!=AppPermission.Settings&&screen!=AppPermission.ManageEmployees),"Saved screen decision "+screen);}
 static int Main(string[] args){
  try{
   Check(args.Length==3&&args[2]=="fixed","Owned fixture arguments required");string owner=args[0],db=args[1];Check(db=="PharmacyPermissionMatrix_"+owner+"_qa","Owned exact database");
   string cs=Database.ConnectionString;Check(new SqlConnectionStringBuilder(cs).InitialCatalog==db,"Actual runtime pinned to owned QA");
   manager="matrix-manager-"+owner.Substring(0,8);cashier="matrix-cashier-"+owner.Substring(0,8);managerId="mm-"+owner.Substring(0,8);cashierId="mc-"+owner.Substring(0,8);
   using(var c=new SqlConnection(cs)){
    c.Open();Check(Convert.ToString(Scalar(c,"SELECT value FROM sys.extended_properties WHERE class=0 AND name=N'QaPermissionMatrix.Owner'"))==owner,"SQL ownership marker");
    foreach(var row in new[]{new[]{managerId,manager,"Manager"},new[]{cashierId,cashier,"Cashier"}})Scalar(c,"INSERT dbo.EmployeeTb1(EmpId,EmpName,EmpPassword,EmpRole,EmpSal,EmpAge,EmpPhone) VALUES(@p0,@p1,@p2,@p3,0,30,N'000')",row[0],row[1],Password,row[2]);
    foreach(bool allow in new[]{false,true}){
     Login(manager,managerId,"Manager");var map=Matrix(allow);ApplicationServices.EmployeePermissions.Replace(cashierId,map,Actor());
     var saved=ApplicationServices.EmployeePermissions.GetByEmployeeId(cashierId);Check(saved.IsAvailable&&saved.Role=="Cashier"&&saved.Overrides.Count==22&&map.All(pair=>saved.Overrides.ContainsKey(pair.Key)&&saved.Overrides[pair.Key]==pair.Value),"Persisted all22 overrides through actual service");
     Check(Convert.ToInt32(Scalar(c,"SELECT COUNT(*) FROM dbo.EmployeeOperationPermissionsTb WHERE EmpId=@p0",cashierId))==22,"Actual SQL override rows");
     foreach(AppOperation op in Enum.GetValues(typeof(AppOperation)))Check(PermissionService.CanExecute(op),"Manager retains operation "+op);
     foreach(AppPermission screen in Enum.GetValues(typeof(AppPermission)))Check(PermissionService.CanOpen(screen),"Manager retains screen "+screen);
     Login(cashier,cashierId,"Cashier");Decisions(allow);Login(cashier,cashierId,"Cashier");Decisions(allow);
     long audit=Convert.ToInt64(Scalar(c,"SELECT COUNT(*) FROM dbo.AuditLogTb"));long permissions=Convert.ToInt64(Scalar(c,"SELECT COUNT(*) FROM dbo.EmployeeOperationPermissionsTb"));
     foreach(var actor in new[]{Actor(),new OperationActor{UserName=manager,UserRole="Manager"}}){
      Denied(()=>ApplicationServices.Employees.GetAll(actor),"employee directory");Denied(()=>ApplicationServices.Employees.Create(null,actor),"employee create");Denied(()=>ApplicationServices.Employees.Update(null,actor),"employee update");Denied(()=>ApplicationServices.Employees.Delete(cashierId,actor),"employee delete");Denied(()=>ApplicationServices.EmployeePermissions.Replace(cashierId,null,actor),"permission replace");
     }
     if(!allow)Denied(()=>ApplicationServices.SalesInvoices.Issue(null),"denied sales posting");
     else{bool validation=false;try{ApplicationServices.SalesInvoices.Issue(null);}catch(ArgumentNullException){validation=true;}Check(validation,"Granted sales reaches request validation; no invoice posted");}
     Check(audit==Convert.ToInt64(Scalar(c,"SELECT COUNT(*) FROM dbo.AuditLogTb"))&&permissions==Convert.ToInt64(Scalar(c,"SELECT COUNT(*) FROM dbo.EmployeeOperationPermissionsTb")),"Denied/validation calls do not write audit or permissions");
     Console.WriteLine("PASS saved matrix allow="+allow+";14 operations/8 delegable screens, two real cashier authentications, manager retained, forged administrative actors denied");
    }
    Login(manager,managerId,"Manager");
    foreach(string key in new[]{"Screen.Settings","Screen.ManageEmployees"}){bool rejected=false;try{ApplicationServices.EmployeePermissions.Replace(cashierId,new Dictionary<string,bool>{{key,true}},Actor());}catch(InvalidOperationException){rejected=true;}Check(rejected,"Sensitive administration screen is not delegable");}
    Check(ApplicationServices.EmployeePermissions.GetByEmployeeId(cashierId).Overrides.Count==22,"Rejected sensitive grants preserve existing overrides");
    ApplicationServices.EmployeePermissions.Replace(cashierId,new Dictionary<string,bool>(),Actor());Login(cashier,cashierId,"Cashier");
    foreach(AppOperation op in Enum.GetValues(typeof(AppOperation)))Check(PermissionService.CanExecute(op)==(op==AppOperation.PostSalesInvoice||op==AppOperation.PostPurchaseInvoice||op==AppOperation.ManageSalesCustomers),"Independent cashier operation defaults "+op);
    foreach(AppPermission screen in Enum.GetValues(typeof(AppPermission)))Check(PermissionService.CanOpen(screen)==(screen==AppPermission.ViewDashboard||screen==AppPermission.Sales||screen==AppPermission.Purchases||screen==AppPermission.ManageMedicines||screen==AppPermission.ManageManufacturers),"Independent cashier screen defaults "+screen);
    SessionContext.Clear();Denied(()=>ApplicationServices.EmployeePermissions.Replace(cashierId,null,new OperationActor{UserName=manager,UserRole="Manager"}),"signed-out forged permission writer");
    foreach(AppOperation op in Enum.GetValues(typeof(AppOperation)))Check(!PermissionService.CanExecute(op),"Signed-out operation denied "+op);
    foreach(AppPermission screen in Enum.GetValues(typeof(AppPermission)))Check(!PermissionService.CanOpen(screen),"Signed-out screen denied "+screen);
   }
   Console.WriteLine("RESULT Checks="+checks+" DirectServiceDenials="+denials+" ActualSqlLogins="+logins+" PersistedKeys=22 ProductGaps=0; no UI/native input, no financial posting, real SQL permission services");return 0;
  }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}finally{SessionContext.Clear();}
 }
}

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using PharmacyManagement.Application;
namespace project_fo_3
{
 // Read authorization from the same database transaction that owns the financial write.
 // HOLDLOCK keeps employee identity and the complete override range stable through Commit.
 // This works across processes/devices; the UI's five-second cache is not financial authority.
 internal static class SalesCommitAuthority
 {
  internal static void Acquire(SqlConnection connection,SqlTransaction transaction,SalesWriteIntent intent,bool posting)
  {
   if(connection==null||transaction==null||intent==null)throw new ArgumentNullException();
   var actor=intent.Owner;
   using(var employee=new SqlCommand("SELECT EmpName,EmpRole FROM dbo.EmployeeTb1 WITH(HOLDLOCK) WHERE EmpId=@Id;",connection,transaction)){
    employee.Parameters.Add("@Id",SqlDbType.NVarChar,50).Value=intent.OwnerEmployeeId;
    using(var reader=employee.ExecuteReader()){
     if(!reader.Read()||!string.Equals(reader.GetString(0).Trim(),actor.UserName.Trim(),StringComparison.OrdinalIgnoreCase)||
        !string.Equals(Convert.ToString(reader[1]).Trim(),actor.UserRole.Trim(),StringComparison.OrdinalIgnoreCase)||reader.Read())
      throw new UnauthorizedAccessException("تغير حساب صاحب محاولة البيع أو دوره؛ أعد تسجيل الدخول قبل المتابعة.");
    }
   }
   // Managers deliberately retain full permissions, matching the existing administration contract.
   if(PermissionService.IsManagerRole(actor.UserRole))return;
   var overrides=new Dictionary<string,bool>(StringComparer.OrdinalIgnoreCase);
   using(var command=new SqlCommand(@"IF OBJECT_ID(N'dbo.EmployeeOperationPermissionsTb',N'U') IS NULL
 THROW 51120,N'مخطط الصلاحيات غير متاح للتحقق من اعتماد البيع.',1;
SELECT OperationName,IsAllowed FROM dbo.EmployeeOperationPermissionsTb WITH(HOLDLOCK) WHERE EmpId=@Id;",connection,transaction)){
    command.Parameters.Add("@Id",SqlDbType.NVarChar,50).Value=intent.OwnerEmployeeId;
    using(var reader=command.ExecuteReader())while(reader.Read()){
     string key=reader.GetString(0);if(overrides.ContainsKey(key))throw new InvalidOperationException("سجل الصلاحيات غير متسق؛ لم تُعتمد الفاتورة.");
     overrides.Add(key,reader.GetBoolean(1));
    }
   }
   bool value;
   bool read=PermissionService.ResolveScreenAccess(actor.UserRole,AppPermission.Sales,overrides.TryGetValue("Screen.Sales",out value)?(bool?)value:null);
   bool write=overrides.TryGetValue(AppOperation.PostSalesInvoice.ToString(),out value)?value:PermissionService.GetRoleDefault(actor.UserRole,AppOperation.PostSalesInvoice);
   if(!read||(posting&&!write))throw new UnauthorizedAccessException("تغيرت صلاحية البيع؛ لم تُعتمد فاتورة جديدة. تحقق من المحاولة المحفوظة بالصلاحية المناسبة.");
  }
 }
}

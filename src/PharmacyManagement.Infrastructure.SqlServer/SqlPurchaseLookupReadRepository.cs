using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Application;
namespace PharmacyManagement.Infrastructure.SqlServer {
 public sealed class SqlPurchaseLookupReadRepository:IPurchaseLookupReadRepository {
  readonly Func<SqlConnection> factory;
  public SqlPurchaseLookupReadRepository(Func<SqlConnection> factory){this.factory=factory??throw new ArgumentNullException(nameof(factory));}
  public async Task<IReadOnlyList<string>> GetSuppliersAsync(CancellationToken token){
   var rows=new List<string>();
   using(var connection=factory())
   using(var command=new SqlCommand("SELECT DISTINCT CompName FROM dbo.CompanyTb1 WHERE NULLIF(LTRIM(RTRIM(CompName)),N'') IS NOT NULL ORDER BY CompName;",connection)){
    await connection.OpenAsync(token).ConfigureAwait(false);
    using(var reader=await command.ExecuteReaderAsync(token).ConfigureAwait(false))
     while(await reader.ReadAsync(token).ConfigureAwait(false))rows.Add(Convert.ToString(reader[0]));
   }
   return rows.AsReadOnly();
  }
  public async Task<IReadOnlyList<PurchaseEmployeeChoice>> GetEmployeesAsync(CancellationToken token){
   var rows=new List<PurchaseEmployeeChoice>();
   using(var connection=factory())
   using(var command=new SqlCommand("SELECT EmpId, EmpName FROM dbo.EmployeeTb1 ORDER BY EmpName;",connection)){
    await connection.OpenAsync(token).ConfigureAwait(false);
    using(var reader=await command.ExecuteReaderAsync(token).ConfigureAwait(false))
     while(await reader.ReadAsync(token).ConfigureAwait(false))
      rows.Add(new PurchaseEmployeeChoice{EmployeeId=reader.IsDBNull(0)?"":Convert.ToString(reader[0]),EmployeeName=reader.IsDBNull(1)?"":Convert.ToString(reader[1])});
   }
   return rows.AsReadOnly();
  }
 }
}

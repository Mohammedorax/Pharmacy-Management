using System;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Application;
namespace PharmacyManagement.Infrastructure.SqlServer {
 public sealed class SqlPurchaseDraftNumberRepository:IPurchaseDraftNumberRepository {
  readonly Func<SqlConnection> factory;
  public SqlPurchaseDraftNumberRepository(Func<SqlConnection> factory){this.factory=factory??throw new ArgumentNullException(nameof(factory));}
  public async Task<long> ReserveAsync(PurchaseDraftNumberKind kind,CancellationToken token){
   string sql;
   switch(kind){case PurchaseDraftNumberKind.Purchase:sql="SELECT NEXT VALUE FOR dbo.Seq_PurchaseId";break;case PurchaseDraftNumberKind.Detail:sql="SELECT NEXT VALUE FOR dbo.Seq_PurchaseDetailId";break;default:throw new ArgumentOutOfRangeException(nameof(kind));}
   using(var connection=factory())using(var command=new SqlCommand(sql,connection)){
    await connection.OpenAsync(token).ConfigureAwait(false);
    var value=await command.ExecuteScalarAsync(token).ConfigureAwait(false);
    if(value==null||value==DBNull.Value)throw new InvalidOperationException("لم يرجع حجز رقم المشتريات نتيجة.");
    return Convert.ToInt64(value);
   }
  }
 }
}

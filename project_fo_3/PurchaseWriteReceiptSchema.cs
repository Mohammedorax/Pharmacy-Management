using System;
using System.Data.SqlClient;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
namespace project_fo_3 {
 internal static class PurchaseWriteReceiptSchema {
  internal static readonly string ValidationSql=LoadValidation();
  static string LoadValidation(){
   const string marker="-- Receipt compatibility validation v1";
   using(var stream=typeof(PurchaseWriteReceiptSchema).Assembly.GetManifestResourceStream("project_fo_3.PurchaseWriteReceiptSchema.sql")){
    if(stream==null)throw new InvalidOperationException("Receipt schema contract resource is missing.");
    using(var reader=new StreamReader(stream)){
     string script=reader.ReadToEnd();int start=script.IndexOf(marker,StringComparison.Ordinal);
     if(start<0)throw new InvalidOperationException("Receipt schema validation marker is missing.");
     return script.Substring(start);
    }
   }
  }
  internal static void Validate(SqlConnection connection,SqlTransaction transaction){
   using(var command=new SqlCommand(ValidationSql,connection,transaction))command.ExecuteNonQuery();
  }
  internal static async Task ValidateAsync(SqlConnection connection,SqlTransaction transaction,CancellationToken token){
   using(var command=new SqlCommand(ValidationSql,connection,transaction))await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
  }
 }
}
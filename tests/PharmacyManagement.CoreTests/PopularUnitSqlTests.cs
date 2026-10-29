using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using PharmacyManagement.Infrastructure.SqlServer;

// Every mutation is inside a transaction that is always rolled back.
// Runs only against the explicit, uniquely owned, completed multiunit QA clone.
namespace PharmacyManagement.CoreTests
{
internal static class PopularUnitSqlTests
{
 static string cs,medicine;static int passed,failed;
 internal static int Run(string[] args)
 {
  try
  {
   Console.OutputEncoding=System.Text.Encoding.UTF8;
   Guid owner;if(args.Length!=2||!Guid.TryParseExact(args[1],"N",out owner)||args[0]!="PharmacyUnits_"+args[1]+"_qa")throw new Exception("Exact owned units QA required");
   cs="Data Source=.;Initial Catalog="+args[0]+";Integrated Security=True;Pooling=False;Connect Timeout=10;Application Name=Pharmacy popularity rollback matrix";
   using(var c=Open())using(var cmd=new SqlCommand("SELECT CONVERT(nvarchar(100),value) FROM sys.extended_properties WHERE class=0 AND name=N'QaTest.Owner'",c))if(Convert.ToString(cmd.ExecuteScalar())!=args[1])throw new Exception("Owner mismatch");
   using(var c=Open())using(var cmd=new SqlCommand("SELECT MedName FROM dbo.MedicineTb1 WHERE LEFT(MedName,12)=N'__UNIT_COMM_'",c))
   {using(var reader=cmd.ExecuteReader()){if(!reader.Read())throw new Exception("Commercial fixture absent");medicine=reader.GetString(0);if(reader.Read())throw new Exception("Ambiguous owned fixture");}}
   string original=Fingerprint();
   Case("fully returned packages excluded",(c,t)=>Require(Net(c,t)==null,"Zero base net must not appear"));
   Case("positive net with fractional package credits uses base quantities",(c,t)=>{
    decimal qty=AddBaseSale(c,t);
    Require(Net(c,t)==qty,"Expected net base quantity "+qty);
   });
   Case("changed catalog factors and prices do not change net",(c,t)=>{
    decimal qty=AddBaseSale(c,t);
    Require(Exec(c,t,"UPDATE u SET BaseUnitsPerUnit=BaseUnitsPerUnit+7,SellingPrice=SellingPrice+99 FROM dbo.MedicineUnitsTb u JOIN dbo.MedicineUnitCatalogTb c ON c.CatalogId=u.CatalogId WHERE c.MedName=@m AND u.UnitId<>c.BaseUnitId")>0,"No package catalog changed");
    Require(Net(c,t)==qty,"Today's catalog changed historical counts");
   });
   foreach(string column in new[]{"UnitPrice","EnteredQuantity","MedName"})
   {
    string captured=column;
    Case("reject corrupt required return snapshot "+captured,(c,t)=>{
     // Keep the snapshot's own CHECK constraints valid; create a mismatch with
     // its stored invoice so the report guard itself must reject it.
     string assignments=captured=="MedName"?"MedName=N'__INVALID_UNIT_MED__'":captured=="UnitPrice"
      ?"UnitPrice=UnitPrice+1,GrossTotal=ROUND(EnteredQuantity*(UnitPrice+1),2)"
      :"EnteredQuantity=EnteredQuantity+1,BaseQuantity=BaseQuantity+BaseUnitsPerUnit,GrossTotal=ROUND((EnteredQuantity+1)*UnitPrice,2)";
     Require(Exec(c,t,"UPDATE u SET "+assignments+" FROM dbo.InvoiceUnitSnapshotsTb u WHERE u.DocumentKind=N'CommercialReturn' AND u.MedName=@m")>0,"No required return snapshot changed");
     RejectSnapshot(c,t);
    });
   }
   Case("reject missing required package snapshot",(c,t)=>{
    Require(Exec(c,t,"DELETE FROM dbo.InvoiceUnitSnapshotsTb WHERE DocumentKind=N'CommercialReturn' AND MedName=@m")>0,"No owned package snapshots deleted");RejectSnapshot(c,t);
   });
   Case("reject missing required base return sidecar",(c,t)=>{
    Require(Exec(c,t,"DELETE FROM dbo.CommercialReturnUnitSnapshotsTb WHERE MedName=@m")>0,"No owned base sidecars deleted");RejectSnapshot(c,t);
   });
   Case("reject absent required unit table",(c,t)=>{
    Exec(c,t,"EXEC sys.sp_rename N'dbo.InvoiceUnitSnapshotsTb',N'__PopularityRollbackSnapshots'");RejectSnapshot(c,t);
   });
   Case("legacy no-commercial-schema branch uses base movements",(c,t)=>{
    Require(Net(c,t,false)==null,"Fully returned sellable base movements should net zero");
   });
   // Re-read after every rolled-back scenario to prove the owned fixture survived.
   using(var c=Open())using(var t=c.BeginTransaction())
   {
    Require(Convert.ToInt32(Scalar(c,t,"SELECT COUNT(*) FROM dbo.InvoiceUnitSnapshotsTb WHERE DocumentKind=N'CommercialReturn' AND MedName=@m"))>0,"Snapshots not restored");
    Require(Convert.ToInt32(Scalar(c,t,"SELECT COUNT(*) FROM dbo.CommercialReturnUnitSnapshotsTb WHERE MedName=@m"))==3,"Three base return sidecars must remain");
    t.Rollback();
   }
   string after=Fingerprint();Require(original==after,"Owned fixture data changed after rollback matrix");
   Console.WriteLine("PASS identical before/after SHA256="+after+" for all six table projections");
   Console.WriteLine("RESULT popularity matrix passed="+passed+" failed="+failed+"; every mutation rolled back; no work/demo DB, UI, posting or printing");return failed==0?0:1;
  }
  catch(Exception error){Console.Error.WriteLine(error);return 2;}
 }
 static SqlConnection Open(){var c=new SqlConnection(cs);c.Open();return c;}
 static SqlCommand Command(SqlConnection c,SqlTransaction t,string sql){var cmd=new SqlCommand(sql,c,t);cmd.CommandTimeout=10;cmd.Parameters.Add("@m",SqlDbType.NVarChar,200).Value=medicine;return cmd;}
 static int Exec(SqlConnection c,SqlTransaction t,string sql){using(var cmd=Command(c,t,sql))return cmd.ExecuteNonQuery();}
 static object Scalar(SqlConnection c,SqlTransaction t,string sql){using(var cmd=Command(c,t,sql))return cmd.ExecuteScalar();}
 static decimal? Net(SqlConnection c,SqlTransaction t,bool returns=true)
 {
  using(var cmd=Command(c,t,SqlMedicineSearchRepository.BuildPopularQuery(returns).Replace("SELECT TOP (20)","SELECT TOP (2147483647)")))
  {
   cmd.Parameters.Add("@FromDate",SqlDbType.DateTime2).Value=DateTime.Today.AddDays(-29);cmd.Parameters.Add("@ToDate",SqlDbType.DateTime2).Value=DateTime.Today.AddDays(1);
   using(var reader=cmd.ExecuteReader())while(reader.Read())if(Convert.ToString(reader["MedName"])==medicine)return Convert.ToDecimal(reader["NetSold"]);
  }return null;
 }
 static decimal AddBaseSale(SqlConnection c,SqlTransaction t)
 {
  int stock=Convert.ToInt32(Scalar(c,t,"SELECT MedQty FROM dbo.MedicineTb1 WHERE MedName=@m"));Require(stock>=5,"Expected sufficient owned stock fixture");
  // A synthetic movement inside this rollback-only aggregation test; it is not
  // a newly posted invoice or a committed stock operation.
  SqlInventoryMovementWriter.RecordAtDatabaseTime(c,t,medicine,-5,stock,stock-5,"CommercialSalesInvoice",Guid.NewGuid().ToString("D"),1m,2m,"QA rollback aggregation probe","QA",DateTime.Now);
  return 5m;
 }
 static void RejectSnapshot(SqlConnection c,SqlTransaction t){try{Net(c,t);}catch(SqlException e){if(e.Number==51000)return;throw;}throw new Exception("Required missing/corrupt snapshot was silently accepted");}
 static void Case(string name,Action<SqlConnection,SqlTransaction> action)
 {
  try{using(var c=Open())using(var t=c.BeginTransaction()){try{action(c,t);}finally{if(t.Connection!=null)t.Rollback();}}passed++;Console.WriteLine("PASS "+name);}
  catch(Exception error){failed++;Console.WriteLine("FAIL "+name+": "+error.Message);}
 }
 static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
 static string Fingerprint()
 {
  string[] queries={
   "SELECT InvoiceId,IssuedAtUtc FROM dbo.SalesInvoicesTb ORDER BY InvoiceId",
   "SELECT DocumentKind,LineKey,MedName,BaseUnitsPerUnit,EnteredQuantity,UnitPrice,CatalogPrice,BaseQuantity,GrossTotal FROM dbo.InvoiceUnitSnapshotsTb ORDER BY DocumentKind,LineKey",
   "SELECT ReturnInvoiceLineId,MedName,ReturnBaseQuantity,OriginalFactor,OriginalUnitPrice FROM dbo.CommercialReturnUnitSnapshotsTb ORDER BY ReturnInvoiceLineId",
   "SELECT UnitId,BaseUnitsPerUnit,SellingPrice FROM dbo.MedicineUnitsTb ORDER BY UnitId",
   "SELECT CatalogId,MedName,BaseUnitId,Revision FROM dbo.MedicineUnitCatalogTb ORDER BY CatalogId",
   "SELECT MedName,SourceType,SourceId,QuantityDelta,QuantityBefore,QuantityAfter,CreatedAt FROM dbo.InventoryMovementsTb ORDER BY MedName,SourceType,SourceId,CreatedAt,QuantityDelta"};
  var text=new StringBuilder();using(var c=Open())foreach(string query in queries)
  {text.AppendLine(query);using(var cmd=new SqlCommand(query,c))using(var reader=cmd.ExecuteReader())while(reader.Read())
   {for(int i=0;i<reader.FieldCount;i++){object value=reader[i];string s=value is DateTime?((DateTime)value).ToString("O",CultureInfo.InvariantCulture):Convert.ToString(value,CultureInfo.InvariantCulture);text.Append(s.Length).Append(':').Append(s);}text.AppendLine();}}
  using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()))).Replace("-","");
 }
}

}

using System;
using System.Data;
using System.Data.SqlClient;
using System.Reflection;
using System.Threading;
using PharmacyManagement.Application;
using PharmacyManagement.Infrastructure.SqlServer;

// Read-only reproduction through the actual saved-return-history presentation.
static class ReturnHistoryUnitReadProbe
{
 static int Main(string[] args)
 {
  try {
   Console.OutputEncoding=System.Text.Encoding.UTF8;
   Guid owner;
   if((args.Length!=2&&args.Length!=3)||!Guid.TryParseExact(args[1],"N",out owner)||args[0]!="PharmacyUnits_"+args[1]+"_qa"||(args.Length==3&&args[2]!="--isolated-candidate-mapper"))throw new Exception("Exact owned QA identity required");
   string cs="Data Source=.;Initial Catalog="+args[0]+";Integrated Security=True;Pooling=False;Connect Timeout=10;Application Name=Pharmacy owned read-only return-history diagnosis";
   var expected=new DataTable();
   using(var connection=new SqlConnection(cs)) {
    connection.Open();
    using(var command=new SqlCommand("SELECT CONVERT(nvarchar(100),value) FROM sys.extended_properties WHERE class=0 AND name=N'QaTest.Owner'",connection))
     if(Convert.ToString(command.ExecuteScalar())!=args[1])throw new Exception("Owner property mismatch");
    using(var command=new SqlCommand(@"SELECT h.ReturnId,h.OriginalInvoiceId,l.LineNumber,
CASE WHEN basis.ReturnInvoiceLineId IS NOT NULL THEN N'وحدة أساسية من '+basis.OriginalUnitName ELSE u.UnitName END,
l.UnitPrice,l.Quantity,COALESCE(basis.OriginalFactor,1),amounts.GrossTotal,amounts.DiscountTotal,amounts.TaxTotal,amounts.NetTotal
FROM dbo.CommercialReturnHeadersTb h JOIN dbo.SalesInvoicesTb credit ON credit.InvoiceId=h.ReturnInvoiceId AND credit.Status=N'Issued'
JOIN dbo.SalesInvoiceLinesTb l ON l.InvoiceId=credit.InvoiceId
JOIN dbo.CommercialReturnLinesTb amounts ON amounts.ReturnId=h.ReturnId AND amounts.ReturnInvoiceLineId=l.LineId
LEFT JOIN dbo.InvoiceUnitSnapshotsTb u ON u.DocumentKind=N'CommercialReturn' AND u.LineKey=CONVERT(nvarchar(100),l.LineId)
LEFT JOIN dbo.CommercialReturnUnitSnapshotsTb basis ON basis.ReturnInvoiceLineId=l.LineId
WHERE LEFT(l.MedName,12)=N'__UNIT_COMM_' AND (u.LineKey IS NOT NULL OR basis.ReturnInvoiceLineId IS NOT NULL) ORDER BY h.ReturnId,l.LineNumber",connection))
    using(var adapter=new SqlDataAdapter(command))adapter.Fill(expected);
   }
   if(expected.Rows.Count<8)throw new Exception("Owned issued package/base-return cases missing");
   var repository=new SqlCommercialReturnReadRepository(()=>new SqlConnection(cs));
   var assembly=Assembly.LoadFrom(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"project_fo_3.exe"));
   var mapperAssembly=args.Length==3?Assembly.GetExecutingAssembly():assembly;
   var map=mapperAssembly.GetType("project_fo_3.CommercialReturnDesktopPresentation",true).GetMethod("ToPrintDocument",BindingFlags.Static|BindingFlags.NonPublic);
   var printType=assembly.GetType("project_fo_3.CommercialReturnPrintData",true);
   MethodInfo build=null;foreach(var method in printType.GetMethods(BindingFlags.Static|BindingFlags.NonPublic))if(method.Name=="BuildItemTable")build=method;
   int mismatches=0,cases=0,baseRows=0;
   foreach(DataRow expectedLine in expected.Rows) {
    var snapshot=repository.LoadDocumentAsync(new CommercialReturnDocumentReadRequest((Guid)expectedLine[1],(Guid)expectedLine[0]),CancellationToken.None).GetAwaiter().GetResult();
    if(snapshot==null)throw new Exception("Owned saved return missing");
    object document=map.Invoke(null,new object[]{snapshot});
    const BindingFlags properties=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    object header=document.GetType().GetProperty("Header",properties).GetValue(document,null),lines=document.GetType().GetProperty("Lines",properties).GetValue(document,null);
    if(Convert.ToInt32(expectedLine[6])>1)baseRows++;
    foreach(bool english in new[]{false,true})using(var table=(DataTable)build.Invoke(null,new object[]{header,lines,english})) {
     var row=table.Rows[Convert.ToInt32(expectedLine[2])-1];
     string unit=Convert.ToString(row[2]),price=Convert.ToString(row[1]);
     int basis=Convert.ToInt32(expectedLine[6]);
     string expectedPrice=Convert.ToDecimal(expectedLine[4]).ToString("0.##",System.Globalization.CultureInfo.InvariantCulture)+(basis>1?(english?" / ":" لكل ")+basis.ToString(System.Globalization.CultureInfo.InvariantCulture):string.Empty);
     bool correct=unit==Convert.ToString(expectedLine[3])&&price==expectedPrice&&Convert.ToDecimal(row[3])==Convert.ToDecimal(expectedLine[5])
        &&Convert.ToDecimal(row[4])==Convert.ToDecimal(expectedLine[7])&&Convert.ToDecimal(row[5])==Convert.ToDecimal(expectedLine[8])
        &&Convert.ToDecimal(row[7])==Convert.ToDecimal(expectedLine[9])&&Convert.ToDecimal(row[8])==Convert.ToDecimal(expectedLine[10]);
     cases++;Console.WriteLine("HISTORY return="+expectedLine[0]+" english="+english+" expectedUnit="+expectedLine[3]+" actualUnit="+unit+" expectedPrice="+expectedPrice+" actualPrice="+price+" correct="+correct);
     if(!correct)mismatches++;
    }
   }
   if(baseRows<3)throw new Exception("Fractional package base-return fixtures missing");
   Console.WriteLine("SUMMARY saved-return-history rows="+expected.Rows.Count+" cases="+cases+" baseRows="+baseRows+" mismatches="+mismatches+" isolatedMapper="+(args.Length==3)+"; SQL read-only; no UI, input or physical printing");
   return mismatches==0?0:1;
  } catch(Exception error){Console.Error.WriteLine(error);return 2;}
 }
}

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using PharmacyManagement.Application;
using PharmacyManagement.Infrastructure.SqlServer;

// Explicit owned QA only. Every injected inconsistency is rolled back.
namespace PharmacyManagement.CoreTests {
internal static class UnitReportCurrencySqlTests
{
    static string cs,medicine;static Guid packageLine,ordinarySaleLine,baseSource;
    static int passed,failed;
    static readonly string[] Targets={"SalesAnalytical","SalesSummary","FastToday","Fast30Days","Popular"};
    static SqlConnection Open(){var c=new SqlConnection(cs);c.Open();return c;}
    static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
    static SqlCommand Command(SqlConnection c,SqlTransaction t,string sql){var cmd=new SqlCommand(sql,c,t){CommandTimeout=15};cmd.Parameters.AddWithValue("@m",medicine??"");cmd.Parameters.AddWithValue("@package",packageLine);cmd.Parameters.AddWithValue("@sale",ordinarySaleLine);cmd.Parameters.AddWithValue("@baseSource",baseSource);return cmd;}
    static int Exec(SqlConnection c,SqlTransaction t,string sql){using(var cmd=Command(c,t,sql))return cmd.ExecuteNonQuery();}
    static void Read(SqlConnection c,SqlTransaction t,string target)
    {
        string sql=target=="Popular"?SqlMedicineSearchRepository.BuildPopularQuery(true):SqlOperationalReportQueries.Get(
            target=="FastToday"?OperationalReportKind.FastSellingToday:target=="Fast30Days"?OperationalReportKind.FastSelling30Days:OperationalReportKind.SalesLines,
            target=="SalesSummary"?OperationalReportViewMode.Summary:OperationalReportViewMode.Analytical);
        using(var cmd=Command(c,t,sql))
        {
            cmd.Parameters.Add("@Take",SqlDbType.Int).Value=2000;
            cmd.Parameters.Add("@Search",SqlDbType.NVarChar,200).Value=medicine;
            cmd.Parameters.Add("@FromDate",SqlDbType.DateTime).Value=DateTime.Today.AddDays(-29);
            cmd.Parameters.Add("@ToDate",SqlDbType.DateTime).Value=DateTime.Today.AddDays(1);
            cmd.Parameters.Add("@FromUtc",SqlDbType.DateTime2).Value=DateTime.Today.AddDays(-29).ToUniversalTime();
            cmd.Parameters.Add("@ToUtc",SqlDbType.DateTime2).Value=DateTime.Today.AddDays(1).ToUniversalTime();
            using(var reader=cmd.ExecuteReader()){do{while(reader.Read()){} }while(reader.NextResult());}
        }
    }
    static void Reject(SqlConnection c,SqlTransaction t,string target)
    {try{Read(c,t,target);}catch(SqlException error){if(error.Number==51000)return;throw;}throw new Exception("Required snapshot in a different currency was silently accepted");}
    static void Case(string name,Action<SqlConnection,SqlTransaction> action)
    {
        try{using(var c=Open())using(var t=c.BeginTransaction()){try{action(c,t);}finally{if(t.Connection!=null)t.Rollback();}}passed++;Console.WriteLine("PASS "+name);}
        catch(Exception error){failed++;Console.WriteLine("FAIL "+name+": "+error.Message);}
    }
    static string Fingerprint()
    {
        string[] tables={"SalesInvoicesTb","SalesInvoiceLinesTb","InvoiceUnitSnapshotsTb","CommercialReturnUnitSnapshotsTb","MedicineUnitsTb","InventoryMovementsTb"};
        var value=new StringBuilder();using(var c=Open())foreach(string table in tables)
        {
            using(var cmd=new SqlCommand("SELECT CONVERT(nvarchar(max),(SELECT * FROM dbo.["+table+"] FOR XML RAW,BINARY BASE64,ROOT('rows'),TYPE))",c))
            {
                var xml=new System.Xml.XmlDocument();xml.LoadXml(Convert.ToString(cmd.ExecuteScalar()));
                var rows=new List<string>();foreach(System.Xml.XmlNode row in xml.DocumentElement.ChildNodes)rows.Add(row.OuterXml);rows.Sort(StringComparer.Ordinal);
                value.Append(table).Append('\n');foreach(string row in rows)value.Append(row.Length).Append(':').Append(row).Append('\n');
            }
        }
        using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value.ToString()))).Replace("-","");
    }
    internal static int Run(string[] args)
    {
        Console.OutputEncoding=Encoding.UTF8;
        try{
            Guid owner;Require(args.Length==2&&Guid.TryParseExact(args[1],"N",out owner)&&args[0]=="PharmacyUnits_"+args[1]+"_qa","Exact owned units QA required");
            cs="Data Source=.;Initial Catalog="+args[0]+";Integrated Security=True;Pooling=False;Connect Timeout=10;Application Name=Pharmacy report currency rollback QA";
            using(var c=Open())
            {
                using(var cmd=new SqlCommand("SELECT CONVERT(nvarchar(32),value) FROM sys.extended_properties WHERE class=0 AND name=N'QaTest.Owner'",c))Require(Convert.ToString(cmd.ExecuteScalar())==args[1],"Owner marker mismatch");
                using(var cmd=new SqlCommand("SELECT MedName FROM dbo.MedicineTb1 WHERE LEFT(MedName,12)=N'__UNIT_COMM_'",c))using(var reader=cmd.ExecuteReader()){Require(reader.Read(),"Owned commercial medicine missing");medicine=reader.GetString(0);Require(!reader.Read(),"Ambiguous owned medicine");}
                using(var cmd=Command(c,null,@"SELECT TOP(1) l.LineId FROM dbo.SalesInvoiceLinesTb l JOIN dbo.InvoiceUnitSnapshotsTb u ON u.DocumentKind=N'CommercialReturn' AND u.LineKey=CONVERT(nvarchar(100),l.LineId) JOIN dbo.SalesInvoicesTb h ON h.InvoiceId=l.InvoiceId WHERE l.MedName=@m AND l.UnitSnapshotRequired=1 AND u.BaseUnitsPerUnit>1 AND h.CurrencyCode=N'SAR' ORDER BY l.LineId"))using(var reader=cmd.ExecuteReader()){Require(reader.Read(),"Required package return absent");packageLine=reader.GetGuid(0);}
                using(var cmd=Command(c,null,@"SELECT TOP(1) l.LineId FROM dbo.SalesInvoiceLinesTb l JOIN dbo.InvoiceUnitSnapshotsTb u ON u.DocumentKind=N'CommercialSale' AND u.LineKey=CONVERT(nvarchar(100),l.LineId) JOIN dbo.SalesInvoicesTb h ON h.InvoiceId=l.InvoiceId WHERE l.MedName=@m AND l.UnitSnapshotRequired=1 AND u.BaseUnitsPerUnit>1 AND h.CurrencyCode=N'SAR' AND NOT EXISTS(SELECT 1 FROM dbo.CommercialReturnUnitSnapshotsTb b WHERE b.OriginalLineId=l.LineId) ORDER BY l.LineId"))using(var reader=cmd.ExecuteReader()){Require(reader.Read(),"Independent original package sale absent");ordinarySaleLine=reader.GetGuid(0);}
                using(var cmd=Command(c,null,@"SELECT TOP(1) b.OriginalLineId FROM dbo.CommercialReturnUnitSnapshotsTb b JOIN dbo.SalesInvoiceLinesTb l ON l.LineId=b.ReturnInvoiceLineId JOIN dbo.SalesInvoicesTb h ON h.InvoiceId=l.InvoiceId WHERE b.MedName=@m AND h.CurrencyCode=N'SAR' ORDER BY b.OriginalLineId"))using(var reader=cmd.ExecuteReader()){Require(reader.Read(),"Required base return absent");baseSource=reader.GetGuid(0);}
            }
            string before=Fingerprint();
            foreach(string target in Targets){string captured=target;Case("valid immutable units "+target,(c,t)=>Read(c,t,captured));}
            foreach(string scenario in new[]{"ordinarySale","packageReturn","matchingBaseAndOriginal"})foreach(string target in Targets)
            {
                string capturedScenario=scenario,capturedTarget=target;
                Case("reject currency mismatch "+scenario+" / "+target,(c,t)=>{
                    if(capturedScenario=="ordinarySale")Require(Exec(c,t,"UPDATE dbo.InvoiceUnitSnapshotsTb SET CurrencyCode=N'USD' WHERE DocumentKind=N'CommercialSale' AND LineKey=CONVERT(nvarchar(100),@sale)")==1,"Expected one original quote mutation");
                    else if(capturedScenario=="packageReturn")Require(Exec(c,t,"UPDATE dbo.InvoiceUnitSnapshotsTb SET CurrencyCode=N'USD' WHERE DocumentKind=N'CommercialReturn' AND LineKey=CONVERT(nvarchar(100),@package)")==1,"Expected one package return quote mutation");
                    else{
                        Require(Exec(c,t,"UPDATE dbo.CommercialReturnUnitSnapshotsTb SET CurrencyCode=N'USD' WHERE OriginalLineId=@baseSource")>0,"Base sidecars not mutated");
                        Require(Exec(c,t,"UPDATE dbo.InvoiceUnitSnapshotsTb SET CurrencyCode=N'USD' WHERE DocumentKind=N'CommercialSale' AND LineKey=CONVERT(nvarchar(100),@baseSource)")==1,"Original base quote not mutated");
                    }
                    Reject(c,t,capturedTarget);
                });
            }
            Require(before==Fingerprint(),"Rollback currency data fingerprint differs");
            Console.WriteLine("PASS six full tables including CurrencyCode identical before/after SHA256="+before);
            Console.WriteLine("RESULT currency report matrix passed="+passed+" failed="+failed+"; all injected changes rolled back; no work/demo DB or UI/financial posting");return failed==0?0:1;
        }catch(Exception error){Console.Error.WriteLine(error);return 2;}
    }
}

}

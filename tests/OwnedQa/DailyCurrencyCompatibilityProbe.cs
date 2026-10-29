using System;
using System.Data;
using System.Data.SqlClient;
using System.Collections.Generic;
using System.Threading;
using System.IO;
using System.Text;
using PharmacyManagement.Application;
using PharmacyManagement.Infrastructure.SqlServer;

internal static class DailyCurrencyCompatibilityProbe
{
    static int checks;
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;}
    static void Exec(SqlConnection c,SqlTransaction t,string sql){using(var cmd=new SqlCommand(sql,c,t))cmd.ExecuteNonQuery();}
    static DataTable Read(SqlConnection c,SqlTransaction t,string schema,DateTime from,DateTime to)
    {
        string sql=SqlOperationalReportQueries.Get(OperationalReportKind.DailyPerformance).Replace("dbo.",schema+".");
        using(var cmd=new SqlCommand(sql,c,t)){cmd.Parameters.AddWithValue("@Take",2000);cmd.Parameters.AddWithValue("@Search","");cmd.Parameters.AddWithValue("@FromDate",from);cmd.Parameters.AddWithValue("@ToDate",to);var table=new DataTable();using(var a=new SqlDataAdapter(cmd))a.Fill(table);return table;}
    }
    static int Main(string[] args)
    {
        Console.OutputEncoding=Encoding.UTF8;
        try{
            Guid owner;Check(args.Length==2&&Guid.TryParseExact(args[1],"N",out owner)&&args[0]=="PharmacyUnits_"+args[1]+"_qa","Exact owned QA only");
            string schema="daily_"+Guid.NewGuid().ToString("N");
            using(var c=new SqlConnection("Data Source=.;Initial Catalog="+args[0]+";Integrated Security=True;Pooling=False;Application Name=Daily currency isolated schema rollback probe")){
                c.Open();using(var cmd=new SqlCommand("SELECT CONVERT(nvarchar(32),value) FROM sys.extended_properties WHERE class=0 AND name=N'QaTest.Owner'",c))Check(Convert.ToString(cmd.ExecuteScalar())==args[1],"Owned marker");
                using(var t=c.BeginTransaction())try{
                    Exec(c,t,"CREATE SCHEMA "+schema);
                    Exec(c,t,"CREATE TABLE "+schema+@".JournalEntriesTb(JournalEntryId int PRIMARY KEY,EntryDate datetime,SourceType nvarchar(50),SourceId nvarchar(50));
CREATE TABLE "+schema+@".JournalEntryLinesTb(JournalEntryId int,AccountCode nvarchar(20),Debit decimal(18,2),Credit decimal(18,2));
INSERT "+schema+@".JournalEntriesTb VALUES(1,'20300102','SalesInvoice','legacy1'),(2,'20300103','SalesInvoiceCancellation','legacy1');
INSERT "+schema+@".JournalEntryLinesTb VALUES(1,'4000',0,436),(2,'4000',436,0);");
                    DateTime from=new DateTime(2030,1,1),to=new DateTime(2030,2,1);
                    using(var data=Read(c,t,schema,from,to)){Check(data.Rows.Count==2,"Old journal schema remains readable without optional tables or currency column");foreach(DataRow row in data.Rows){Check(row.IsNull("العملة")&&row.IsNull("صافي المبيعات"),"Unknown historical currency is never guessed");Check(Convert.ToInt64(row["قيود دون عملة محفوظة"])==1,"Unknown count visible");}}
                    Exec(c,t,"ALTER TABLE "+schema+".JournalEntriesTb ADD SystemCurrencyCode nvarchar(10) NULL;");
                    Exec(c,t,"UPDATE "+schema+".JournalEntriesTb SET SystemCurrencyCode=N'SAR' WHERE JournalEntryId=1;");
                    using(var data=Read(c,t,schema,from,to)){Check(data.Rows.Count==2,"Cancellation after currency change remains separate by saved date");foreach(DataRow row in data.Rows){Check(Convert.ToString(row["العملة"])=="SAR","Cancellation inherits original saved journal currency");Check(Convert.ToInt64(row["قيود دون عملة محفوظة"])==0,"Saved original currency resolves cancellation");}Check((decimal)data.Rows[0]["صافي المبيعات"]==-436m&&(decimal)data.Rows[1]["صافي المبيعات"]==436m,"Original sale and reversal retain exact values");}
                    Exec(c,t,"UPDATE "+schema+".JournalEntriesTb SET SystemCurrencyCode=N'USD' WHERE JournalEntryId=2;");
                    using(var data=Read(c,t,schema,from,to)){Check(data.Rows[0].IsNull("العملة")&&data.Rows[0].IsNull("صافي المبيعات"),"Contradictory saved reversal currency cannot produce money");Check(Convert.ToInt64(data.Rows[0]["قيود بتعارض العملة"])==1,"Original versus cancellation conflict explicit");}
                    Exec(c,t,"CREATE TABLE "+schema+".LegacySalesInvoiceHeadersTb(InvoiceReference nvarchar(100)); INSERT "+schema+".LegacySalesInvoiceHeadersTb VALUES(N'legacy1');");
                    using(var data=Read(c,t,schema,from,to))Check(data.Rows.Count==2,"Legacy header without lines or unit snapshots remains readable");
                    Exec(c,t,"CREATE TABLE "+schema+@".SalesInvoicesTb(InvoiceId uniqueidentifier PRIMARY KEY,InvoiceType nvarchar(20),CurrencyCode nvarchar(10));
INSERT "+schema+@".SalesInvoicesTb VALUES('11111111-1111-1111-1111-111111111111','Sale','USD');
INSERT "+schema+@".JournalEntriesTb VALUES(3,'20300102','SalesInvoice','11111111-1111-1111-1111-111111111111',NULL);
INSERT "+schema+@".JournalEntryLinesTb VALUES(3,'4000',0,259);");
                    using(var data=Read(c,t,schema,from,to)){Check(data.Rows.Count==3,"Two currencies on same day remain distinct with historical reversal");bool sar=false,usd=false;foreach(DataRow row in data.Rows){if(Convert.ToString(row["العملة"])=="SAR"){sar=true;Check((decimal)row["صافي المبيعات"]==436m,"SAR independent amount");}if(Convert.ToString(row["العملة"])=="USD"){usd=true;Check((decimal)row["صافي المبيعات"]==259m,"USD independent amount");}}Check(sar&&usd,"Both known currencies visible");}
                    using(var data=Read(c,t,schema,new DateTime(2030,1,2),new DateTime(2030,1,3)))Check(data.Rows.Count==2,"Exclusive upper date boundary excludes next-day cancellation");
                    using(var data=Read(c,t,schema,new DateTime(2031,1,1),new DateTime(2031,2,1)))Check(data.Rows.Count==0,"Empty period returns typed empty result");
                }finally{if(t.Connection!=null)t.Rollback();}
                using(var cmd=new SqlCommand("SELECT COUNT(*) FROM sys.schemas WHERE name=@Name",c)){cmd.Parameters.AddWithValue("@Name",schema);Check(Convert.ToInt32(cmd.ExecuteScalar())==0,"Isolated schema and all fixture objects rolled back");}
                if(Enum.IsDefined(typeof(OperationalReportKind),12)){
                    var expected=new Dictionary<DateTime,long[]>();
                    using(var cmd=new SqlCommand("SELECT CreatedAt,SourceType,QuantityDelta FROM dbo.InventoryMovementsTb WHERE CreatedAt>=@FromDate AND CreatedAt<@ToDate",c)){
                        cmd.Parameters.AddWithValue("@FromDate",new DateTime(2020,1,1));cmd.Parameters.AddWithValue("@ToDate",new DateTime(2031,1,1));
                        using(var reader=cmd.ExecuteReader())while(reader.Read()){
                            DateTime day=reader.GetDateTime(0).Date;long[] totals;if(!expected.TryGetValue(day,out totals)){totals=new long[3];expected.Add(day,totals);}totals[0]++;
                            string kind=reader.IsDBNull(1)?null:reader.GetString(1);long quantity=Convert.ToInt64(reader[2]);
                            if(kind=="SalesInvoice"||kind=="CommercialSalesInvoice")totals[1]-=quantity;
                            if(kind=="SalesInvoiceCancellation"||kind=="CommercialSalesReturn")totals[2]+=quantity;
                        }
                    }
                    using(var cmd=new SqlCommand(SqlOperationalReportQueries.Get((OperationalReportKind)12),c)){
                        cmd.Parameters.AddWithValue("@Take",2000);cmd.Parameters.AddWithValue("@Search","");cmd.Parameters.AddWithValue("@FromDate",new DateTime(2020,1,1));cmd.Parameters.AddWithValue("@ToDate",new DateTime(2031,1,1));
                        using(var table=new DataTable()){using(var adapter=new SqlDataAdapter(cmd))adapter.Fill(table);Check(!table.Columns.Contains("العملة")&&table.Rows.Count==expected.Count,"Public daily stock report has one currency-independent row per saved day");
                            foreach(DataRow row in table.Rows){long[] totals;Check(expected.TryGetValue((DateTime)row["اليوم"],out totals),"Stock day exists in raw saved movements");Check(Convert.ToInt64(row["حركات المخزون"])==totals[0],"Raw movement count parity");Check(Convert.ToInt64(row["الوحدات الأساسية المباعة"])==totals[1],"Raw base sold quantity parity");Check(Convert.ToInt64(row["الوحدات المعادة للمخزون"])==totals[2],"Raw sellable returned stock parity");}
                        }
                    }
                    var repository=new SqlOperationalReportRepository(()=>new SqlConnection(c.ConnectionString));
                    foreach(var kind in new[]{OperationalReportKind.DailyPerformance,(OperationalReportKind)12}){
                        var request=new OperationalReportRequest(kind,new DateTime(2020,1,1),new DateTime(2030,12,31),"");
                        var sync=repository.Load(request);var asyncResult=repository.LoadAsync(request,CancellationToken.None).GetAwaiter().GetResult();
                        Check(sync.Columns.Count==asyncResult.Columns.Count&&sync.Rows.Count==asyncResult.Rows.Count,"Actual synchronous and asynchronous SQL repository schemas and row counts match");
                        for(int col=0;col<sync.Columns.Count;col++)Check(sync.Columns[col].Name==asyncResult.Columns[col].Name&&sync.Columns[col].DataType==asyncResult.Columns[col].DataType,"Actual typed SQL column parity");
                        for(int row=0;row<sync.Rows.Count;row++)for(int col=0;col<sync.Columns.Count;col++)Check(Equals(sync.Rows[row][col],asyncResult.Rows[row][col]),"Actual asynchronous SQL read preserves typed values and unknown NULL amounts");
                        if(kind==OperationalReportKind.DailyPerformance){int currency=-1,net=-1;for(int i=0;i<sync.Columns.Count;i++){if(sync.Columns[i].Name=="العملة")currency=i;if(sync.Columns[i].Name=="صافي المبيعات")net=i;}Check(currency>=0&&net>=0,"Typed financial read exposes currency and net columns");bool unknown=false;foreach(var row in sync.Rows)if(row[currency]==null){unknown=true;Check(row[net]==null,"Actual async repository preserves unknown monetary NULL");}Check(unknown,"Owned fixture includes historically unknown currencies");}
                    }
                }
            }
            Console.WriteLine("PASS daily currency schema compatibility checks="+checks+"; optional schemas, frozen cancellation currency, conflicting facts, mixed currency dates; all isolated schema objects rolled back");return 0;
        }catch(Exception e){Console.Error.WriteLine(e);return 2;}
    }
}

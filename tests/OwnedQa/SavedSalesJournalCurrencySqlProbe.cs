using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using PharmacyManagement.Domain;
using PharmacyManagement.Infrastructure.SqlServer;

static class SavedSalesJournalCurrencySqlProbe
{
    static int checks;static string cs;
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;}
    static SqlCommand Command(SqlConnection c,SqlTransaction t,string sql,params object[] values){var cmd=new SqlCommand(sql,c,t);for(int i=0;i<values.Length;i++)cmd.Parameters.AddWithValue("@p"+i,values[i]??DBNull.Value);return cmd;}
    static object Scalar(SqlConnection c,SqlTransaction t,string sql,params object[] values){using(var cmd=Command(c,t,sql,values))return cmd.ExecuteScalar();}
    static int Exec(SqlConnection c,SqlTransaction t,string sql,params object[] values){using(var cmd=Command(c,t,sql,values))return cmd.ExecuteNonQuery();}
    static string Source()=>"currency-"+Guid.NewGuid().ToString("N");
    static string Currency(SqlConnection c,SqlTransaction t,int id)=>Convert.ToString(Scalar(c,t,"SELECT SystemCurrencyCode FROM dbo.JournalEntriesTb WHERE JournalEntryId=@p0",id));
    static JournalLineValues[] Sale()=>new[]{new JournalLineValues("1000",115m,0m,"cash",null),new JournalLineValues("4000",0m,100m,"net",null),new JournalLineValues("2100",0m,15m,"tax",null),new JournalLineValues("5000",33m,0m,"cost",null),new JournalLineValues("1200",0m,33m,"stock",null)};
    static JournalLineValues[] Reverse(IEnumerable<JournalLineValues> lines){var result=new List<JournalLineValues>();foreach(var l in lines)result.Add(new JournalLineValues(l.AccountCode,l.Credit,l.Debit,l.Description,l.CostCenterId));return result.ToArray();}
    static JournalLineValues[] Lines(SqlConnection c,SqlTransaction t,int id){var result=new List<JournalLineValues>();using(var cmd=Command(c,t,"SELECT AccountCode,Debit,Credit,LineDescription,CostCenterId FROM dbo.JournalEntryLinesTb WHERE JournalEntryId=@p0 ORDER BY JournalEntryLineId",id))using(var r=cmd.ExecuteReader())while(r.Read())result.Add(new JournalLineValues(r.GetString(0),r.GetDecimal(1),r.GetDecimal(2),r.IsDBNull(3)?null:r.GetString(3),r.IsDBNull(4)?(int?)null:r.GetInt32(4)));return result.ToArray();}
    static void NoHeader(SqlConnection c,SqlTransaction t,string source){Check(Convert.ToInt32(Scalar(c,t,"SELECT COUNT(*) FROM dbo.JournalEntriesTb WHERE SourceId=@p0",source))==0,"Rejected operation leaves no journal");}
    static void Rejected(Action action,string message){bool rejected=false;try{action();}catch(InvalidOperationException){rejected=true;}Check(rejected,message);}
    static string Fingerprint()
    {
        var text=new StringBuilder();using(var c=new SqlConnection(cs)){c.Open();
            foreach(string table in new[]{"JournalEntriesTb","JournalEntryLinesTb","CurrencySettingsTb","SalesInvoicesTb","AccountingPeriodsTb"})using(var cmd=new SqlCommand("SELECT CONVERT(nvarchar(max),(SELECT * FROM dbo.["+table+"] FOR XML RAW,BINARY BASE64,ROOT('rows'),TYPE))",c)){
                string value=Convert.ToString(cmd.ExecuteScalar());
                if(value.Length==0){Check(Convert.ToInt64(Scalar(c,null,"SELECT COUNT_BIG(*) FROM dbo.["+table+"]"))==0,"Missing XML allowed only for a verified empty table");value="<rows/>";}
                var xml=new XmlDocument();xml.LoadXml(value);var rows=new List<string>();foreach(XmlNode row in xml.DocumentElement.ChildNodes)rows.Add(row.OuterXml);rows.Sort(StringComparer.Ordinal);text.Append(table).Append('\n');foreach(string row in rows)text.Append(row.Length).Append(':').Append(row).Append('\n');
            }
        }
        using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()))).Replace("-","");
    }
    static void EqualLines(SqlConnection c,SqlTransaction t,int id,JournalLineValues[] expected){var actual=Lines(c,t,id);Check(actual.Length==expected.Length,"Journal line count preserved");for(int i=0;i<actual.Length;i++){Check(actual[i].AccountCode==expected[i].AccountCode,"Account preserved");Check(actual[i].Debit==expected[i].Debit&&actual[i].Credit==expected[i].Credit,"Exact debit and credit preserved");Check(actual[i].CostCenterId==expected[i].CostCenterId,"Cost center preserved");}}
    static int Main(string[] args)
    {
        Console.OutputEncoding=Encoding.UTF8;
        try{
            Guid owner;Check(args.Length==2&&Guid.TryParseExact(args[1],"N",out owner)&&args[0]=="PharmacyUnits_"+args[1]+"_qa","Exact owned QA required");
            cs="Data Source=.;Initial Catalog="+args[0]+";Integrated Security=True;Pooling=False;Application Name=Saved sales journal currency rollback matrix";
            using(var c=new SqlConnection(cs)){c.Open();Check(Convert.ToString(Scalar(c,null,"SELECT CONVERT(nvarchar(32),value) FROM sys.extended_properties WHERE class=0 AND name=N'QaTest.Owner'"))==args[1],"Owner marker matches");}
            string before=Fingerprint();
            using(var c=new SqlConnection(cs)){c.Open();using(var t=c.BeginTransaction())try{
                string current=Convert.ToString(Scalar(c,t,"SELECT CurrencyCode FROM dbo.CurrencySettingsTb WHERE IsSystemCurrency=1")).Trim().ToUpperInvariant();Check(current=="SAR","Pinned owned fixture uses SAR");
                string saleSource=Source();var sale=SqlJournalWriter.CreateSalesDocument(c,t,DateTime.Today,"SalesInvoice",saleSource,"owned new sale","owned",Sale(),current);
                Check(!sale.AlreadyPosted&&Currency(c,t,sale.Id)==current,"New legacy sale captures locked current currency");EqualLines(c,t,sale.Id,Sale());
                Check(Convert.ToInt32(Scalar(c,t,"SELECT COUNT(*) FROM dbo.CurrencySettingsTb WHERE CurrencyCode=N'QAZ'"))==0,"Synthetic switched currency is unused");
                Check(Exec(c,t,"UPDATE dbo.CurrencySettingsTb SET CurrencyCode=N'QAZ' WHERE IsSystemCurrency=1")==1,"Current currency temporarily switched");
                var reversed=Reverse(Sale());var cancellation=SqlJournalWriter.CreateSalesDocument(c,t,DateTime.Today,"SalesInvoiceCancellation",saleSource,"owned reverse","owned",reversed);
                Check(Currency(c,t,cancellation.Id)=="SAR","Legacy cancellation freezes original SAR after current currency switch");EqualLines(c,t,cancellation.Id,reversed);
                string stale=Source();Rejected(()=>SqlJournalWriter.CreateSalesDocument(c,t,DateTime.Today,"SalesInvoice",stale,"stale","owned",Sale(),"SAR"),"Stale new draft currency rejected");NoHeader(c,t,stale);
                string fresh=Source();var freshSale=SqlJournalWriter.CreateSalesDocument(c,t,DateTime.Today,"SalesInvoice",fresh,"fresh switched sale","owned",Sale(),"QAZ");Check(Currency(c,t,freshSale.Id)=="QAZ","New sale after switch uses its own currency");
                string unknown=Source();var historical=SqlJournalWriter.Create(c,t,DateTime.Today,"SalesInvoice",unknown,"old untagged sale","owned",Sale());
                var retryUnknown=SqlJournalWriter.CreateSalesDocument(c,t,DateTime.Today,"SalesInvoice",unknown,"retry old","owned",Sale(),"SAR");Check(retryUnknown.AlreadyPosted&&retryUnknown.Id==historical.Id&&Currency(c,t,historical.Id)=="","Accepted unknown history is never backfilled during retry");
                var unknownCancel=SqlJournalWriter.CreateSalesDocument(c,t,DateTime.Today,"SalesInvoiceCancellation",unknown,"unknown reversal","owned",reversed);Check(Currency(c,t,unknownCancel.Id)=="","Unknown original cancellation does not guess current QAZ");
                string missing=Source();Rejected(()=>SqlJournalWriter.CreateSalesDocument(c,t,DateTime.Today,"SalesInvoiceCancellation",missing,"missing original","owned",reversed),"Missing original sale prevents reversal");NoHeader(c,t,missing);
                Rejected(()=>SqlJournalWriter.CreateSalesDocument(c,t,DateTime.Today,"SalesInvoice",Source(),"unbalanced","owned",new[]{new JournalLineValues("1000",10m,0m,"bad",null)},"QAZ"),"Balancing guard retained");

                foreach(string kind in new[]{"SalesInvoice","CommercialSalesReturn"}){
                    string type=kind=="SalesInvoice"?"Sale":"CreditNote";
                    string doc=Convert.ToString(Scalar(c,t,@"SELECT TOP(1) e.SourceId FROM dbo.JournalEntriesTb e JOIN dbo.SalesInvoicesTb h ON CONVERT(nvarchar(50),h.InvoiceId)=e.SourceId WHERE e.SourceType=@p0 AND h.InvoiceType=@p1 AND h.CurrencyCode=N'SAR' ORDER BY e.JournalEntryId",kind,type));Check(doc!="","Owned commercial document available");
                    int oldId=Convert.ToInt32(Scalar(c,t,"SELECT JournalEntryId FROM dbo.JournalEntriesTb WHERE SourceType=@p0 AND SourceId=@p1",kind,doc));var archived=Lines(c,t,oldId);
                    Check(Exec(c,t,"UPDATE dbo.JournalEntriesTb SET SourceId=@p0 WHERE JournalEntryId=@p1",Source(),oldId)==1,"Original QA journal source temporarily isolated");
                    if(kind=="CommercialSalesReturn"){
                        Rejected(()=>SqlJournalWriter.CreateSalesDocument(c,t,DateTime.Today,"SalesInvoice",doc,"wrong saved type","owned",archived),"Credit document cannot be posted as a sale");NoHeader(c,t,doc);
                        string missingCredit=Guid.NewGuid().ToString("D");Rejected(()=>SqlJournalWriter.CreateSalesDocument(c,t,DateTime.Today,kind,missingCredit,"missing saved credit","owned",archived),"Missing credit document rejected");NoHeader(c,t,missingCredit);
                    }else{
                        Rejected(()=>SqlJournalWriter.CreateSalesDocument(c,t,DateTime.Today,kind,doc,"wrong draft currency","owned",archived,"QAZ"),"Saved commercial sale contradicting draft currency rejected");NoHeader(c,t,doc);
                    }
                    var posted=SqlJournalWriter.CreateSalesDocument(c,t,DateTime.Today,kind,doc,"saved commercial currency","owned",archived);Check(Currency(c,t,posted.Id)=="SAR","Saved commercial "+type+" remains SAR while current currency is QAZ");EqualLines(c,t,posted.Id,archived);
                    if(kind=="SalesInvoice"){
                        Check(Exec(c,t,"UPDATE dbo.JournalEntriesTb SET SystemCurrencyCode=N'QAZ' WHERE JournalEntryId=@p0",posted.Id)==1,"Saved original journal currency contradiction injected without changing immutable invoice");
                        Rejected(()=>SqlJournalWriter.CreateSalesDocument(c,t,DateTime.Today,"SalesInvoiceCancellation",doc,"contradictory reverse","owned",Reverse(archived)),"Original journal versus immutable document contradiction rejected");
                        Check(Convert.ToInt32(Scalar(c,t,"SELECT COUNT(*) FROM dbo.JournalEntriesTb WHERE SourceType=N'SalesInvoiceCancellation' AND SourceId=@p0",doc))==0,"Contradictory cancellation leaves no header");
                        Exec(c,t,"UPDATE dbo.JournalEntriesTb SET SystemCurrencyCode=N'SAR' WHERE JournalEntryId=@p0",posted.Id);
                        var commercialCancel=SqlJournalWriter.CreateSalesDocument(c,t,DateTime.Today,"SalesInvoiceCancellation",doc,"saved commercial reversal","owned",Reverse(archived));Check(Currency(c,t,commercialCancel.Id)=="SAR","Commercial cancellation preserves saved SAR after current switch");EqualLines(c,t,commercialCancel.Id,Reverse(archived));
                    }
                    var retry=SqlJournalWriter.CreateSalesDocument(c,t,DateTime.Today,kind,doc,"safe retry","owned",archived);Check(retry.AlreadyPosted&&retry.Id==posted.Id&&Currency(c,t,retry.Id)=="SAR","Commercial retry retains id and currency");
                }
                string quoted=Convert.ToString(Scalar(c,t,@"SELECT TOP(1) e.SourceId FROM dbo.JournalEntriesTb e JOIN dbo.LegacySalesInvoiceHeadersTb h ON h.InvoiceReference=e.SourceId
WHERE e.SourceType=N'SalesInvoice'
AND EXISTS(SELECT 1 FROM dbo.LegacySalesInvoiceLinesTb l JOIN dbo.InvoiceUnitSnapshotsTb u ON u.DocumentKind=N'Sale' AND u.LineKey=l.BillId WHERE l.InvoiceReference=h.InvoiceReference)
AND NOT EXISTS(SELECT 1 FROM dbo.LegacySalesInvoiceLinesTb l JOIN dbo.BillsTb1 b ON b.BillId=l.BillId LEFT JOIN dbo.InvoiceUnitSnapshotsTb u ON u.DocumentKind=N'Sale' AND u.LineKey=b.BillId WHERE l.InvoiceReference=h.InvoiceReference AND (b.UnitSnapshotRequired<>1 OR u.LineKey IS NULL OR u.CurrencyCode<>N'SAR' OR u.BaseQuantity<>b.Quantity OR u.UnitPrice<>b.Sprice)) ORDER BY e.JournalEntryId"));Check(quoted!="","Complete legacy unit snapshot fixture available");
                Check(Exec(c,t,"UPDATE dbo.JournalEntriesTb SET SourceId=@p0 WHERE SourceType=N'SalesInvoiceCancellation' AND SourceId=@p1",Source(),quoted)<=1,"Existing owned unit cancellation temporarily isolated if present");
                int quotedId=Convert.ToInt32(Scalar(c,t,"SELECT JournalEntryId FROM dbo.JournalEntriesTb WHERE SourceType=N'SalesInvoice' AND SourceId=@p0",quoted));
                Check(Exec(c,t,"UPDATE dbo.JournalEntriesTb SET SystemCurrencyCode=NULL WHERE JournalEntryId=@p0",quotedId)==1,"Simulated historical journal lacks currency while complete immutable unit snapshots remain");
                var unitCancel=SqlJournalWriter.CreateSalesDocument(c,t,DateTime.Today,"SalesInvoiceCancellation",quoted,"saved quote reversal","owned",Reverse(Lines(c,t,quotedId)));Check(Currency(c,t,unitCancel.Id)=="SAR","Complete immutable legacy unit snapshots supply original currency");

                DateTime closedDate=new DateTime(2030,1,2);Guid period=Guid.NewGuid();
                Exec(c,t,"INSERT dbo.AccountingPeriodsTb(PeriodId,StartDate,EndDate,Status,ClosedAtUtc,ClosedBy,Notes) VALUES(@p0,@p1,@p1,N'Closed',SYSUTCDATETIME(),N'owned',N'currency rollback matrix')",period,closedDate);
                string closed=Source();Rejected(()=>SqlJournalWriter.CreateSalesDocument(c,t,closedDate,"SalesInvoice",closed,"closed period","owned",Sale(),"QAZ"),"Closed period still rejects new sales");NoHeader(c,t,closed);
                var acceptedRetry=SqlJournalWriter.CreateSalesDocument(c,t,closedDate,"SalesInvoice",saleSource,"old accepted retry","owned",Sale(),"QAZ");Check(acceptedRetry.AlreadyPosted&&acceptedRetry.Id==sale.Id&&Currency(c,t,sale.Id)=="SAR","Accepted retry remains idempotent after period and currency changes");
                bool restricted=false;try{SqlJournalWriter.CreateSalesDocument(c,t,DateTime.Today,"Voucher",Source(),"forbidden bypass","owned",Sale());}catch(ArgumentException){restricted=true;}Check(restricted,"Frozen document currency path is restricted to sales documents");
            }finally{if(t.Connection!=null)t.Rollback();}}
            Check(before==Fingerprint(),"Five complete tables restored after all temporary scenarios");
            Console.WriteLine("PASS saved sales journal currency matrix checks="+checks+"; new legacy capture, frozen sale/credit/cancellation, stale draft and conflicts rejected, immutable legacy quote fallback, unknown history, retry, balance and period guards");
            Console.WriteLine("PASS five full tables restored SHA256="+before+"; all temporary writes rolled back; QA identity counters may advance; no work/demo database");return 0;
        }catch(Exception e){Console.Error.WriteLine(e);return 2;}
    }
}

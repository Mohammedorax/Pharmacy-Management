using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using PharmacyManagement.Application;
namespace project_fo_3
{
 internal static class SalesWriteSourceFingerprint
 {
  internal static string Compute(string source){
   var b=new SqlConnectionStringBuilder(source);
   if(string.IsNullOrWhiteSpace(b.DataSource)||string.IsNullOrWhiteSpace(b.InitialCatalog))throw new InvalidOperationException("مصدر محاولة البيع غير محدد.");
   using(var m=new MemoryStream()){
    using(var w=new BinaryWriter(m,Encoding.UTF8,true)){w.Write("PharmacyManagement.SalesSource.v1");w.Write(b.DataSource);w.Write(b.InitialCatalog);w.Write(b.IntegratedSecurity);w.Write(b.UserID);if(b.IntegratedSecurity)using(var identity=System.Security.Principal.WindowsIdentity.GetCurrent())w.Write(identity.Name);}
    using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(m.ToArray())).Replace("-","");
   }
  }
 }
 internal static class SalesWriteReceiptStore
 {
  internal static void AcquireBusinessSettings(SqlConnection c,SqlTransaction tx){
   using(var q=new SqlCommand(@"IF OBJECT_ID(N'dbo.CommercialOperationalSettingsTb',N'U') IS NOT NULL SELECT SettingKey FROM dbo.CommercialOperationalSettingsTb WITH(HOLDLOCK);
IF OBJECT_ID(N'dbo.SaudiFeatureSettingsTb',N'U') IS NOT NULL SELECT FeatureKey FROM dbo.SaudiFeatureSettingsTb WITH(HOLDLOCK);
IF OBJECT_ID(N'dbo.CommercialEInvoiceSettingsTb',N'U') IS NOT NULL SELECT SettingsId FROM dbo.CommercialEInvoiceSettingsTb WITH(HOLDLOCK);",c,tx))q.ExecuteNonQuery();
  }
  static bool SameUnit(PharmacyManagement.Domain.MedicineUnitSelection expected,PharmacyManagement.Domain.MedicineUnitSelection actual){
   if(expected==null||actual==null)return expected==actual;
   return expected.UnitId==actual.UnitId&&expected.CatalogRevision==actual.CatalogRevision&&expected.Name==actual.Name&&expected.BaseUnitsPerUnit==actual.BaseUnitsPerUnit&&expected.EnteredQuantity==actual.EnteredQuantity&&expected.UnitPrice==actual.UnitPrice&&expected.CatalogPrice==actual.CatalogPrice&&expected.CurrencyCode==actual.CurrencyCode;
  }
  // Read-only shape validation. A missing/incompatible receipt store fails closed.
  const string Shape=@"IF OBJECT_ID(N'dbo.SalesWriteReceiptsTb',N'U') IS NULL THROW 51100,N'مخطط استرجاع البيع غير مثبت.',1;
IF (SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.SalesWriteReceiptsTb'))<>9
OR NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.SalesWriteReceiptsTb') AND name=N'RequestId' AND TYPE_NAME(user_type_id)=N'uniqueidentifier' AND is_nullable=0)
OR NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.SalesWriteReceiptsTb') AND name=N'RequestFingerprint' AND TYPE_NAME(user_type_id)=N'binary' AND max_length=32 AND is_nullable=0)
OR NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.SalesWriteReceiptsTb') AND name=N'SourceFingerprint' AND TYPE_NAME(user_type_id)=N'char' AND max_length=64 AND is_nullable=0)
OR NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.SalesWriteReceiptsTb') AND name=N'OwnerEmployeeId' AND TYPE_NAME(user_type_id)=N'nvarchar' AND max_length=100 AND is_nullable=0)
OR NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.SalesWriteReceiptsTb') AND name=N'OwnerUser' AND TYPE_NAME(user_type_id)=N'nvarchar' AND max_length=400 AND is_nullable=0)
OR NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.SalesWriteReceiptsTb') AND name=N'OwnerRole' AND TYPE_NAME(user_type_id)=N'nvarchar' AND max_length=100 AND is_nullable=0)
OR NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.SalesWriteReceiptsTb') AND name=N'InvoiceReference' AND TYPE_NAME(user_type_id)=N'nvarchar' AND max_length=200 AND is_nullable=0)
OR NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.SalesWriteReceiptsTb') AND name=N'ResultXml' AND TYPE_NAME(user_type_id)=N'nvarchar' AND max_length=-1 AND is_nullable=0)
OR NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.SalesWriteReceiptsTb') AND name=N'CreatedAtUtc' AND TYPE_NAME(user_type_id)=N'datetime2' AND scale=7 AND is_nullable=0)
OR NOT EXISTS(SELECT 1 FROM sys.indexes i JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE i.object_id=OBJECT_ID(N'dbo.SalesWriteReceiptsTb') AND i.is_primary_key=1 AND c.name=N'RequestId' AND ic.key_ordinal=1)
OR (SELECT COUNT(*) FROM sys.indexes i JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id WHERE i.object_id=OBJECT_ID(N'dbo.SalesWriteReceiptsTb') AND i.is_primary_key=1 AND ic.key_ordinal>0)<>1
OR NOT EXISTS(SELECT 1 FROM sys.indexes i JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE i.object_id=OBJECT_ID(N'dbo.SalesWriteReceiptsTb') AND i.is_unique=1 AND i.is_disabled=0 AND i.has_filter=0 AND c.name=N'InvoiceReference' AND ic.key_ordinal=1 AND NOT EXISTS(SELECT 1 FROM sys.index_columns other WHERE other.object_id=i.object_id AND other.index_id=i.index_id AND other.key_ordinal>1))
THROW 51101,N'مخطط استرجاع البيع غير متوافق.',1;";
  internal static SalesInvoiceIssueResult AcquireAndRead(SqlConnection c,SqlTransaction tx,SalesWriteIntent intent){
   using(var q=new SqlCommand(Shape,c,tx))q.ExecuteNonQuery();
   using(var q=new SqlCommand(@"DECLARE @Code int,@Resource nvarchar(255)=N'PharmacyManagement.SalesWrite.'+CONVERT(nvarchar(36),@Id);
EXEC @Code=sys.sp_getapplock @Resource=@Resource,@LockMode=N'Exclusive',@LockOwner=N'Transaction',@LockTimeout=10000;
IF @Code<0 THROW 51102,N'تعذر انتظار نتيجة محاولة بيع أخرى؛ أعد التحقق من النتيجة بعد قليل.',1;
SELECT RequestFingerprint,SourceFingerprint,OwnerEmployeeId,OwnerUser,OwnerRole,InvoiceReference,ResultXml FROM dbo.SalesWriteReceiptsTb WHERE RequestId=@Id;",c,tx)){
    q.Parameters.Add("@Id",SqlDbType.UniqueIdentifier).Value=intent.RequestId;
    using(var r=q.ExecuteReader()){
     if(!r.Read())return null;var owner=intent.Owner;
     if(r.GetString(1)!=intent.SourceFingerprint||r.GetString(2)!=intent.OwnerEmployeeId||!string.Equals(r.GetString(3),owner.UserName,StringComparison.OrdinalIgnoreCase)||!string.Equals(r.GetString(4),owner.UserRole,StringComparison.OrdinalIgnoreCase))throw new UnauthorizedAccessException("إيصال البيع يخص مصدرًا أو منفّذًا آخر.");
     if(!((byte[])r[0]).SequenceEqual(intent.Fingerprint()))throw new InvalidOperationException("تغيرت بيانات محاولة البيع؛ لا تُنشئ فاتورة أخرى قبل التحقق.");
     string xml=r.GetString(6);
     if(xml.StartsWith("<abandoned ",StringComparison.Ordinal)){
      if(xml.Length>4096)throw new InvalidDataException("حالة إلغاء المحاولة غير صالحة.");XElement marker;
      using(var reader=XmlReader.Create(new StringReader(xml),new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=4096}))marker=XElement.Load(reader);
      if(marker.Name!="abandoned"||(string)marker.Attribute("version")!="1"||(string)marker.Attribute("request")!=intent.RequestId.ToString("N")||marker.HasElements||marker.Attributes().Count()!=2||!string.IsNullOrWhiteSpace(marker.Value)||r.GetString(5)!="ABANDONED:"+intent.RequestId.ToString("N"))throw new InvalidDataException("هوية إلغاء المحاولة غير متطابقة.");
      throw new SalesAttemptAbandonedException(intent.RequestId);
     }
     var result=Decode(xml,intent);if(result.AccountingSourceId!=r.GetString(5))throw new InvalidDataException("مرجع إيصال البيع غير متطابق.");return result;
    }
   }
  }
  // Caller holds the request transaction lock and has proved that no receipt exists.
  internal static void RecordAbandoned(SqlConnection c,SqlTransaction tx,SalesWriteIntent intent){
   var owner=intent.Owner;string marker=new XElement("abandoned",new XAttribute("version",1),new XAttribute("request",intent.RequestId.ToString("N"))).ToString(SaveOptions.DisableFormatting);
   using(var q=new SqlCommand("INSERT dbo.SalesWriteReceiptsTb(RequestId,RequestFingerprint,SourceFingerprint,OwnerEmployeeId,OwnerUser,OwnerRole,InvoiceReference,ResultXml) VALUES(@Id,@Hash,@Source,@Employee,@User,@Role,@Ref,@Xml)",c,tx)){
    q.Parameters.Add("@Id",SqlDbType.UniqueIdentifier).Value=intent.RequestId;q.Parameters.Add("@Hash",SqlDbType.Binary,32).Value=intent.Fingerprint();q.Parameters.Add("@Source",SqlDbType.Char,64).Value=intent.SourceFingerprint;
    q.Parameters.Add("@Employee",SqlDbType.NVarChar,50).Value=intent.OwnerEmployeeId;q.Parameters.Add("@User",SqlDbType.NVarChar,200).Value=owner.UserName;q.Parameters.Add("@Role",SqlDbType.NVarChar,50).Value=owner.UserRole;
    q.Parameters.Add("@Ref",SqlDbType.NVarChar,100).Value="ABANDONED:"+intent.RequestId.ToString("N");q.Parameters.Add("@Xml",SqlDbType.NVarChar,-1).Value=marker;q.ExecuteNonQuery();
   }
  }
  internal static void Record(SqlConnection c,SqlTransaction tx,SalesWriteIntent intent,SalesInvoiceIssueResult r){
   VerifyPersisted(c,tx,intent,r);
   var root=new XElement("sale",new XAttribute("version",1),new XAttribute("commercial",r.IsCommercial),new XAttribute("customer",r.CustomerId??""),new XAttribute("reference",r.AccountingSourceId??""),new XAttribute("invoiceId",r.CommercialInvoiceId?.ToString("D")??""),new XAttribute("number",r.CommercialInvoiceNumber?.ToString(CultureInfo.InvariantCulture)??""));
   foreach(var p in typeof(SalesInvoiceIssueResult).GetProperties().Where(p=>p.PropertyType==typeof(decimal)))root.Add(new XAttribute(p.Name,((decimal)p.GetValue(r)).ToString("G29",CultureInfo.InvariantCulture)));
   root.Add(new XElement("lines",(r.BillLineIds??Array.Empty<string>()).Select(id=>new XElement("line",new XAttribute("id",id)))));
   string xml=root.ToString(SaveOptions.DisableFormatting);Decode(xml,intent);var owner=intent.Owner;
   using(var q=new SqlCommand("INSERT dbo.SalesWriteReceiptsTb(RequestId,RequestFingerprint,SourceFingerprint,OwnerEmployeeId,OwnerUser,OwnerRole,InvoiceReference,ResultXml) VALUES(@Id,@Hash,@Source,@Employee,@User,@Role,@Ref,@Xml)",c,tx)){
    q.Parameters.Add("@Id",SqlDbType.UniqueIdentifier).Value=intent.RequestId;q.Parameters.Add("@Hash",SqlDbType.Binary,32).Value=intent.Fingerprint();q.Parameters.Add("@Source",SqlDbType.Char,64).Value=intent.SourceFingerprint;
    q.Parameters.Add("@Employee",SqlDbType.NVarChar,50).Value=intent.OwnerEmployeeId;q.Parameters.Add("@User",SqlDbType.NVarChar,200).Value=owner.UserName;q.Parameters.Add("@Role",SqlDbType.NVarChar,50).Value=owner.UserRole;
    q.Parameters.Add("@Ref",SqlDbType.NVarChar,100).Value=r.AccountingSourceId;q.Parameters.Add("@Xml",SqlDbType.NVarChar,-1).Value=xml;q.ExecuteNonQuery();
   }
  }
  internal static void VerifyPersisted(SqlConnection c,SqlTransaction tx,SalesWriteIntent intent,SalesInvoiceIssueResult r){
   using(var q=new SqlCommand(@"SELECT e.SystemCurrencyCode,e.CreatedBy,SUM(l.Debit-l.Credit) Balance,
SUM(CASE WHEN l.AccountCode IN(N'4000',N'2100') THEN l.Credit-l.Debit ELSE 0 END) Net,
SUM(CASE WHEN l.AccountCode=N'2100' THEN l.Credit-l.Debit ELSE 0 END) Tax,
SUM(CASE WHEN l.AccountCode=N'5000' THEN l.Debit-l.Credit ELSE 0 END) Cost
FROM dbo.JournalEntriesTb e WITH(HOLDLOCK) JOIN dbo.JournalEntryLinesTb l WITH(HOLDLOCK) ON l.JournalEntryId=e.JournalEntryId
WHERE e.SourceType=N'SalesInvoice' AND e.SourceId=@Ref GROUP BY e.JournalEntryId,e.SystemCurrencyCode,e.CreatedBy;",c,tx)){
    q.Parameters.Add("@Ref",SqlDbType.NVarChar,100).Value=r.AccountingSourceId;using(var reader=q.ExecuteReader()){
     if(!reader.Read()||!string.Equals(Convert.ToString(reader[0]),intent.Snapshot().ExpectedCurrencyCode,StringComparison.OrdinalIgnoreCase)||!string.Equals(Convert.ToString(reader[1]),intent.Owner.UserName,StringComparison.OrdinalIgnoreCase)||Convert.ToDecimal(reader[2])!=0||Convert.ToDecimal(reader[3])!=r.NetTotal||Convert.ToDecimal(reader[4])!=r.TaxTotal||Convert.ToDecimal(reader[5])!=r.CostOfGoods||reader.Read())throw new InvalidDataException("قيد إيصال البيع الأصلي غير متطابق.");
    }
   }
   var request=intent.Snapshot();
   if(!r.IsCommercial){
    var mapped=LegacySalesInvoiceRepository.GetMappedInvoice(c,tx,r.AccountingSourceId);if(mapped==null||mapped.TotalAmount!=r.NetTotal||!mapped.BillLineIds.SequenceEqual(r.BillLineIds))throw new InvalidDataException("رابط بنود إيصال البيع الأصلي غير متطابق.");
    for(int n=0;n<r.BillLineIds.Count;n++)if(!SameUnit(request.Lines[n].Unit,MedicineUnitInvoiceBridge.ReadSnapshot(c,tx,"Sale",r.BillLineIds[n])))throw new InvalidDataException("لقطة وحدة إيصال البيع القديم غير متطابقة.");
   }else{
    if(r.AccountingSourceId!=r.CommercialInvoiceId.Value.ToString("D"))throw new InvalidDataException("مرجع المستند التجاري غير متطابق.");
    using(var q=new SqlCommand("SELECT InvoiceNumber,CustomerId,CurrencyCode,GrossTotal,DiscountTotal,TaxTotal,NetTotal,IssuedBy,InvoiceType,Status FROM dbo.SalesInvoicesTb WITH(HOLDLOCK) WHERE InvoiceId=@Id",c,tx)){
     q.Parameters.Add("@Id",SqlDbType.UniqueIdentifier).Value=r.CommercialInvoiceId.Value;using(var reader=q.ExecuteReader()){
      if(!reader.Read()||Convert.ToInt64(reader[0])!=r.CommercialInvoiceNumber||Convert.ToString(reader[1])!=r.CustomerId||!string.Equals(Convert.ToString(reader[2]),request.ExpectedCurrencyCode,StringComparison.OrdinalIgnoreCase)||Convert.ToDecimal(reader[3])!=r.GrossTotal||Convert.ToDecimal(reader[4])!=r.DiscountTotal||Convert.ToDecimal(reader[5])!=r.TaxTotal||Convert.ToDecimal(reader[6])!=r.NetTotal||!string.Equals(Convert.ToString(reader[7]).Trim(),request.EmployeeName.Trim(),StringComparison.OrdinalIgnoreCase)||Convert.ToString(reader[8])!="Sale"||Convert.ToString(reader[9])!="Issued"||reader.Read())throw new InvalidDataException("رأس إيصال البيع التجاري غير متطابق.");
     }
    }
    var lines=new List<Tuple<Guid,string,decimal,decimal,int>>();
    using(var q=new SqlCommand("SELECT LineId,MedName,Quantity,UnitPrice,LineNumber FROM dbo.SalesInvoiceLinesTb WITH(HOLDLOCK) WHERE InvoiceId=@Id ORDER BY LineNumber",c,tx)){
     q.Parameters.Add("@Id",SqlDbType.UniqueIdentifier).Value=r.CommercialInvoiceId.Value;using(var reader=q.ExecuteReader())while(reader.Read())lines.Add(Tuple.Create(reader.GetGuid(0),reader.GetString(1),reader.GetDecimal(2),reader.GetDecimal(3),reader.GetInt32(4)));
    }
    if(lines.Count!=request.Lines.Count)throw new InvalidDataException("عدد بنود الإيصال التجاري غير متطابق.");
    for(int n=0;n<lines.Count;n++){var line=lines[n];var expected=request.Lines[n];if(line.Item5!=n+1||line.Item2!=expected.MedicineName||line.Item3!=(expected.Unit?.EnteredQuantity??expected.Quantity)||line.Item4!=(expected.Unit?.UnitPrice??expected.DisplayedSellingPrice)||!SameUnit(expected.Unit,MedicineUnitInvoiceBridge.ReadSnapshot(c,tx,"CommercialSale",line.Item1.ToString("D"))))throw new InvalidDataException("بند ووحدة الإيصال التجاري غير متطابقين.");}
   }
  }
  static SalesInvoiceIssueResult Decode(string xml,SalesWriteIntent intent){
   if(xml==null||xml.Length>4194304)throw new InvalidDataException("حجم إيصال البيع غير صالح.");XElement e;
   using(var reader=XmlReader.Create(new StringReader(xml),new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=4194304}))e=XElement.Load(reader);
   if(e.Name!="sale"||(string)e.Attribute("version")!="1")throw new InvalidDataException("صيغة إيصال البيع غير صالحة.");
   var ids=e.Element("lines")?.Elements("line").Select(x=>(string)x.Attribute("id")).ToArray();if(ids==null||ids.Length>10000||ids.Any(string.IsNullOrWhiteSpace)||ids.Distinct(StringComparer.Ordinal).Count()!=ids.Length)throw new InvalidDataException("بنود إيصال البيع غير صالحة.");
   string guid=(string)e.Attribute("invoiceId"),number=(string)e.Attribute("number");
   var r=new SalesInvoiceIssueResult{IsCommercial=bool.Parse((string)e.Attribute("commercial")),CustomerId=(string)e.Attribute("customer"),AccountingSourceId=(string)e.Attribute("reference"),CommercialInvoiceId=string.IsNullOrEmpty(guid)?(Guid?)null:Guid.Parse(guid),CommercialInvoiceNumber=string.IsNullOrEmpty(number)?(long?)null:long.Parse(number,CultureInfo.InvariantCulture),BillLineIds=ids};
   foreach(var p in typeof(SalesInvoiceIssueResult).GetProperties().Where(p=>p.PropertyType==typeof(decimal)))p.SetValue(r,decimal.Parse((string)e.Attribute(p.Name),CultureInfo.InvariantCulture));
   var q=intent.Snapshot();if(r.IsCommercial!=intent.CommercialExpected||string.IsNullOrWhiteSpace(r.AccountingSourceId)||r.AccountingSourceId.Length>100||string.IsNullOrWhiteSpace(r.CustomerId)||r.CustomerId.Length>50||r.GrossTotal!=q.DisplayedGrossTotal||r.NetTotal!=q.DisplayedNetTotal||r.GrossTotal<=0||r.DiscountTotal<0||r.TaxTotal<0||r.CostOfGoods<0||r.CustomerCreditApplied<0||r.CashCollected<0||r.CashCollected+r.CustomerCreditApplied!=r.NetTotal)throw new InvalidDataException("نتيجة إيصال البيع لا تطابق المحاولة.");
   if(r.IsCommercial?(!r.CommercialInvoiceId.HasValue||!r.CommercialInvoiceNumber.HasValue||r.CommercialInvoiceNumber<=0||ids.Length!=0):r.CommercialInvoiceId.HasValue||r.CommercialInvoiceNumber.HasValue||ids.Length!=q.Lines.Count)throw new InvalidDataException("هوية مستند إيصال البيع غير متطابقة.");
   return r;
  }
 }
}

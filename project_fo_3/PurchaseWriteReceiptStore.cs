using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using PharmacyManagement.Application;
namespace project_fo_3 {
 internal static class PurchaseWriteSourceFingerprint {
  internal static string Compute(string connectionString){
   var builder=new SqlConnectionStringBuilder(connectionString);
   if(string.IsNullOrWhiteSpace(builder.DataSource)||string.IsNullOrWhiteSpace(builder.InitialCatalog))throw new InvalidOperationException("مصدر قاعدة المشتريات غير محدد.");
   using(var stream=new System.IO.MemoryStream()){
    using(var writer=new System.IO.BinaryWriter(stream,System.Text.Encoding.UTF8,true)){
     writer.Write("PharmacyManagement.PurchaseSource.v1");writer.Write(builder.DataSource);writer.Write(builder.InitialCatalog);
     writer.Write(builder.IntegratedSecurity);writer.Write(builder.UserID);
     if(builder.IntegratedSecurity){using(var identity=System.Security.Principal.WindowsIdentity.GetCurrent())writer.Write(identity.Name);}
    }
    using(var hash=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(hash.ComputeHash(stream.ToArray())).Replace("-","");
   }
  }
 }
 internal static class PurchaseWriteReceiptStore {
  internal static PurchaseInvoiceResult AcquireAndRead(SqlConnection connection,SqlTransaction transaction,PurchaseWriteIntent intent){
   PurchaseWriteReceiptSchema.Validate(connection,transaction);
   using(var command=new SqlCommand(@"
IF OBJECT_ID(N'dbo.PurchaseWriteReceiptsTb',N'U') IS NULL THROW 51080,N'مخطط استعادة المشتريات غير مثبت.',1;
DECLARE @Resource nvarchar(255)=N'PharmacyManagement.PurchaseWrite.'+CONVERT(nvarchar(36),@Id),@Code int;
EXEC @Code=sys.sp_getapplock @Resource=@Resource,@LockMode=N'Exclusive',@LockOwner=N'Transaction',@LockTimeout=10000;
IF @Code<0 THROW 51081,N'تعذر قفل محاولة المشتريات.',1;
SELECT RequestFingerprint,OwnerEmployeeId,OwnerUser,OwnerRole,PurchaseId,ResultXml,SourceFingerprint FROM dbo.PurchaseWriteReceiptsTb WHERE RequestId=@Id;",connection,transaction)){
    command.Parameters.Add("@Id",SqlDbType.UniqueIdentifier).Value=intent.RequestId;
    using(var reader=command.ExecuteReader()){
     if(!reader.Read())return null;
     if(!string.Equals(reader.GetString(6),intent.SourceFingerprint,StringComparison.Ordinal))throw new UnauthorizedAccessException("إيصال المشتريات يخص مصدرًا آخر.");
     var owner=intent.Owner;
     if(!string.Equals(reader.GetString(1),intent.OwnerEmployeeId,StringComparison.Ordinal)||
        !string.Equals(reader.GetString(2),owner.UserName,StringComparison.OrdinalIgnoreCase)||
        !string.Equals(reader.GetString(3),owner.UserRole,StringComparison.OrdinalIgnoreCase))
      throw new UnauthorizedAccessException("إيصال المشتريات يخص منفذًا آخر.");
     if(!((byte[])reader[0]).SequenceEqual(intent.Fingerprint()))throw new InvalidOperationException("تغيرت بيانات محاولة المشتريات؛ لا يمكن إعادة تنفيذها.");
     string xml=reader.GetString(5);
     if(xml.StartsWith("<abandoned ",StringComparison.Ordinal)){
      if(xml.Length>4096)throw new InvalidDataException("حالة إغلاق المحاولة غير صالحة.");XElement marker;
      using(var markerReader=XmlReader.Create(new StringReader(xml),new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=4096}))marker=XElement.Load(markerReader);
      if(marker.Name!="abandoned"||(string)marker.Attribute("version")!="1"||(string)marker.Attribute("request")!=intent.RequestId.ToString("N")||marker.HasElements||marker.Attributes().Count()!=2||!string.IsNullOrWhiteSpace(marker.Value)||reader.GetString(4)!="ABANDONED:"+intent.RequestId.ToString("N"))throw new InvalidDataException("هوية إغلاق المحاولة غير متطابقة.");
      throw new PurchaseAttemptAbandonedException(intent.RequestId);
     }
     var result=Decode(xml,intent.Snapshot().Lines.Count);ValidateOutcome(intent,result);
     if(result.PurchaseId!=reader.GetString(4))throw new InvalidOperationException("نتيجة إيصال المشتريات غير متطابقة.");
     return result;
    }
   }
  }
  internal static bool IsCancelled(SqlConnection connection,SqlTransaction transaction,PurchaseInvoiceResult result){
   using(var command=new SqlCommand("SELECT TotalAmount FROM dbo.PurchasesTb WITH(HOLDLOCK) WHERE PurchaseId=@Id;",connection,transaction)){
    command.Parameters.Add("@Id",SqlDbType.NVarChar,50).Value=result.PurchaseId;
    object total=command.ExecuteScalar();
    if(total==null||total==DBNull.Value||Convert.ToDecimal(total)!=result.TotalAmount)
     throw new InvalidOperationException("الفاتورة المرتبطة بإيصال المشتريات غير موجودة أو غير متطابقة.");
   }
   using(var command=new SqlCommand("SELECT TOP(1) 1 FROM dbo.JournalEntriesTb WITH(HOLDLOCK) WHERE SourceType=N'PurchaseInvoiceCancellation' AND SourceId=@Id;",connection,transaction)){
    command.Parameters.Add("@Id",SqlDbType.NVarChar,50).Value=result.PurchaseId;
    return command.ExecuteScalar()!=null;
   }
  }
  internal static async System.Threading.Tasks.Task<PurchaseInvoiceResult> AcquireAndReadAsync(SqlConnection connection,SqlTransaction transaction,PurchaseWriteIntent intent,System.Threading.CancellationToken cancellationToken){
   await PurchaseWriteReceiptSchema.ValidateAsync(connection,transaction,cancellationToken).ConfigureAwait(false);
   using(var command=new SqlCommand(@"
IF OBJECT_ID(N'dbo.PurchaseWriteReceiptsTb',N'U') IS NULL THROW 51080,N'مخطط استعادة المشتريات غير مثبت.',1;
DECLARE @Resource nvarchar(255)=N'PharmacyManagement.PurchaseWrite.'+CONVERT(nvarchar(36),@Id),@Code int;
EXEC @Code=sys.sp_getapplock @Resource=@Resource,@LockMode=N'Exclusive',@LockOwner=N'Transaction',@LockTimeout=10000;
IF @Code<0 THROW 51081,N'تعذر قفل محاولة المشتريات.',1;
SELECT RequestFingerprint,OwnerEmployeeId,OwnerUser,OwnerRole,PurchaseId,ResultXml,SourceFingerprint FROM dbo.PurchaseWriteReceiptsTb WHERE RequestId=@Id;",connection,transaction)){
    command.Parameters.Add("@Id",SqlDbType.UniqueIdentifier).Value=intent.RequestId;
    using(var reader=await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false)){
     if(!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))return null;
     if(!string.Equals(reader.GetString(6),intent.SourceFingerprint,StringComparison.Ordinal))throw new UnauthorizedAccessException("إيصال المشتريات يخص مصدرًا آخر.");
     var owner=intent.Owner;
     if(!string.Equals(reader.GetString(1),intent.OwnerEmployeeId,StringComparison.Ordinal)||
        !string.Equals(reader.GetString(2),owner.UserName,StringComparison.OrdinalIgnoreCase)||
        !string.Equals(reader.GetString(3),owner.UserRole,StringComparison.OrdinalIgnoreCase))
      throw new UnauthorizedAccessException("إيصال المشتريات يخص منفذًا آخر.");
     if(!((byte[])reader[0]).SequenceEqual(intent.Fingerprint()))throw new InvalidOperationException("تغيرت بيانات محاولة المشتريات؛ لا يمكن إعادة تنفيذها.");
     var result=Decode(reader.GetString(5),intent.Snapshot().Lines.Count);ValidateOutcome(intent,result);
     if(result.PurchaseId!=reader.GetString(4))throw new InvalidOperationException("نتيجة إيصال المشتريات غير متطابقة.");
     return result;
    }
   }
  }
  internal static async System.Threading.Tasks.Task<bool> IsCancelledAsync(SqlConnection connection,SqlTransaction transaction,PurchaseInvoiceResult result,System.Threading.CancellationToken cancellationToken){
   using(var command=new SqlCommand("SELECT TotalAmount FROM dbo.PurchasesTb WITH(HOLDLOCK) WHERE PurchaseId=@Id;",connection,transaction)){
    command.Parameters.Add("@Id",SqlDbType.NVarChar,50).Value=result.PurchaseId;
    object total=await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
    if(total==null||total==DBNull.Value||Convert.ToDecimal(total)!=result.TotalAmount)
     throw new InvalidOperationException("الفاتورة المرتبطة بإيصال المشتريات غير موجودة أو غير متطابقة.");
   }
   using(var command=new SqlCommand("SELECT TOP(1) 1 FROM dbo.JournalEntriesTb WITH(HOLDLOCK) WHERE SourceType=N'PurchaseInvoiceCancellation' AND SourceId=@Id;",connection,transaction)){
    command.Parameters.Add("@Id",SqlDbType.NVarChar,50).Value=result.PurchaseId;
    return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)!=null;
   }
  }
  // Records that the attempt was closed without a financial write, so a later observation of the
  // same request id reports the closed state instead of an unknown outcome.
  internal static void RecordAbandoned(SqlConnection connection,SqlTransaction transaction,PurchaseWriteIntent intent){
   var owner=intent.Owner;
   string marker=new XElement("abandoned",new XAttribute("version",1),new XAttribute("request",intent.RequestId.ToString("N"))).ToString(SaveOptions.DisableFormatting);
   using(var command=new SqlCommand(@"INSERT dbo.PurchaseWriteReceiptsTb(RequestId,RequestFingerprint,OwnerEmployeeId,OwnerUser,OwnerRole,PurchaseId,ResultXml,SourceFingerprint)
 VALUES(@Id,@Hash,@Employee,@User,@Role,@Purchase,@Result,@Source);",connection,transaction)){
    command.Parameters.Add("@Id",SqlDbType.UniqueIdentifier).Value=intent.RequestId;
    command.Parameters.Add("@Source",SqlDbType.Char,64).Value=intent.SourceFingerprint;
    command.Parameters.Add("@Hash",SqlDbType.Binary,32).Value=intent.Fingerprint();
    command.Parameters.Add("@Employee",SqlDbType.NVarChar,50).Value=intent.OwnerEmployeeId;
    command.Parameters.Add("@User",SqlDbType.NVarChar,200).Value=owner.UserName;
    command.Parameters.Add("@Role",SqlDbType.NVarChar,50).Value=owner.UserRole;
    command.Parameters.Add("@Purchase",SqlDbType.NVarChar,50).Value="ABANDONED:"+intent.RequestId.ToString("N");
    command.Parameters.Add("@Result",SqlDbType.NVarChar,-1).Value=marker;command.ExecuteNonQuery();
   }
  }
  internal static void Record(SqlConnection connection,SqlTransaction transaction,PurchaseWriteIntent intent,PurchaseInvoiceResult result){
   var lines=new List<XElement>();
   foreach(var line in result.Lines)lines.Add(new XElement("line",new XAttribute("number",line.LineNumber),new XAttribute("id",line.DetailId)));
   string xml=new XElement("purchase",new XAttribute("version",1),new XAttribute("id",result.PurchaseId),new XAttribute("total",result.TotalAmount.ToString(CultureInfo.InvariantCulture)),lines).ToString(SaveOptions.DisableFormatting);
   Decode(xml,intent.Snapshot().Lines.Count);ValidateOutcome(intent,result);
   using(var command=new SqlCommand(@"INSERT dbo.PurchaseWriteReceiptsTb(RequestId,RequestFingerprint,OwnerEmployeeId,OwnerUser,OwnerRole,PurchaseId,ResultXml,SourceFingerprint)
VALUES(@Id,@Hash,@Employee,@User,@Role,@Purchase,@Result,@Source);",connection,transaction)){
    var owner=intent.Owner;
    command.Parameters.Add("@Id",SqlDbType.UniqueIdentifier).Value=intent.RequestId;
    command.Parameters.Add("@Source",SqlDbType.Char,64).Value=intent.SourceFingerprint;
    command.Parameters.Add("@Hash",SqlDbType.Binary,32).Value=intent.Fingerprint();
    command.Parameters.Add("@Employee",SqlDbType.NVarChar,50).Value=intent.OwnerEmployeeId;
    command.Parameters.Add("@User",SqlDbType.NVarChar,200).Value=owner.UserName;
    command.Parameters.Add("@Role",SqlDbType.NVarChar,50).Value=owner.UserRole;
    command.Parameters.Add("@Purchase",SqlDbType.NVarChar,50).Value=result.PurchaseId;
    command.Parameters.Add("@Result",SqlDbType.NVarChar,-1).Value=xml;command.ExecuteNonQuery();
   }
  }
  internal static async System.Threading.Tasks.Task RecordAsync(SqlConnection connection,SqlTransaction transaction,PurchaseWriteIntent intent,PurchaseInvoiceResult result,System.Threading.CancellationToken cancellationToken){
   var lines=new List<XElement>();
   foreach(var line in result.Lines)lines.Add(new XElement("line",new XAttribute("number",line.LineNumber),new XAttribute("id",line.DetailId)));
   string xml=new XElement("purchase",new XAttribute("version",1),new XAttribute("id",result.PurchaseId),new XAttribute("total",result.TotalAmount.ToString(CultureInfo.InvariantCulture)),lines).ToString(SaveOptions.DisableFormatting);
   Decode(xml,intent.Snapshot().Lines.Count);ValidateOutcome(intent,result);
   using(var command=new SqlCommand(@"INSERT dbo.PurchaseWriteReceiptsTb(RequestId,RequestFingerprint,OwnerEmployeeId,OwnerUser,OwnerRole,PurchaseId,ResultXml,SourceFingerprint)
VALUES(@Id,@Hash,@Employee,@User,@Role,@Purchase,@Result,@Source);",connection,transaction)){
    var owner=intent.Owner;
    command.Parameters.Add("@Id",SqlDbType.UniqueIdentifier).Value=intent.RequestId;
    command.Parameters.Add("@Source",SqlDbType.Char,64).Value=intent.SourceFingerprint;
    command.Parameters.Add("@Hash",SqlDbType.Binary,32).Value=intent.Fingerprint();
    command.Parameters.Add("@Employee",SqlDbType.NVarChar,50).Value=intent.OwnerEmployeeId;
    command.Parameters.Add("@User",SqlDbType.NVarChar,200).Value=owner.UserName;
    command.Parameters.Add("@Role",SqlDbType.NVarChar,50).Value=owner.UserRole;
    command.Parameters.Add("@Purchase",SqlDbType.NVarChar,50).Value=result.PurchaseId;
    command.Parameters.Add("@Result",SqlDbType.NVarChar,-1).Value=xml;await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
   }
  }
  static void ValidateOutcome(PurchaseWriteIntent intent,PurchaseInvoiceResult result){
   var snapshot=intent.Snapshot();
   if(result.TotalAmount!=PurchaseInvoiceValidation.CalculateTotal(snapshot.Lines)||
      !result.Lines.Select(line=>line.LineNumber).SequenceEqual(snapshot.Lines.Select(line=>line.LineNumber)))
    throw new InvalidOperationException("نتيجة إيصال المشتريات لا تطابق الطلب.");
  }  static PurchaseInvoiceResult Decode(string xml,int expectedLines){
   var root=XElement.Parse(xml);decimal total;
   string id=(string)root.Attribute("id");
   if(root.Name!="purchase"||(string)root.Attribute("version")!="1"||string.IsNullOrWhiteSpace(id)||id.Length>50||
      !decimal.TryParse((string)root.Attribute("total"),NumberStyles.Number,CultureInfo.InvariantCulture,out total)||total<=0)
    throw new InvalidOperationException("إيصال المشتريات غير صالح.");
   var results=new List<PurchaseInvoiceLineResult>();var numbers=new HashSet<int>();var ids=new HashSet<string>();
   foreach(var child in root.Elements()){
    int number;long detail;string detailId=(string)child.Attribute("id");
    if(child.Name!="line"||!int.TryParse((string)child.Attribute("number"),out number)||number<=0||!numbers.Add(number)||
       !long.TryParse(detailId,NumberStyles.Integer,CultureInfo.InvariantCulture,out detail)||detail<=0||!ids.Add(detailId))
     throw new InvalidOperationException("بنود إيصال المشتريات غير صالحة.");
    results.Add(new PurchaseInvoiceLineResult{LineNumber=number,DetailId=detailId});
   }
   if(results.Count!=expectedLines)throw new InvalidOperationException("عدد بنود إيصال المشتريات غير متطابق.");
   return new PurchaseInvoiceResult{PurchaseId=id,TotalAmount=total,Lines=results.AsReadOnly()};
  }
 }
}







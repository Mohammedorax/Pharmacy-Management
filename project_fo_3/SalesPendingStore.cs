using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using PharmacyManagement.Application;
using PharmacyManagement.Domain;
namespace project_fo_3
{
 internal sealed class SalesPendingStore:IDisposable
 {
  const int MaximumBytes=4194304;
  readonly string path,source,employee,user;readonly byte[] entropy;FileStream lease;
  internal SalesPendingStore(string directory,string sourceFingerprint,string employeeId,string userName){
   if(string.IsNullOrWhiteSpace(directory)||sourceFingerprint==null||sourceFingerprint.Length!=64||sourceFingerprint.Any(c=>!"0123456789ABCDEF".Contains(c.ToString()))||string.IsNullOrWhiteSpace(employeeId)||employeeId.Length>50||string.IsNullOrWhiteSpace(userName)||userName.Trim().Length>200)throw new ArgumentException("سياق استرجاع البيع غير صالح.");
   source=sourceFingerprint;employee=employeeId;user=userName.Trim();string key;
   using(var m=new MemoryStream()){using(var w=new BinaryWriter(m,Encoding.UTF8,true)){w.Write(source);w.Write(employee);w.Write(user.ToUpperInvariant());}using(var h=SHA256.Create())key=BitConverter.ToString(h.ComputeHash(m.ToArray())).Replace("-","");}
   Directory.CreateDirectory(directory);path=Path.Combine(Path.GetFullPath(directory),key+"-Sales.pending");entropy=Encoding.UTF8.GetBytes("PharmacySalesPending/v1/"+key);
   lease=new FileStream(path+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
  }
  internal string FilePath=>path;
  void Ensure(){if(lease==null)throw new ObjectDisposedException(nameof(SalesPendingStore));}
  void Verify(SalesWriteIntent i){if(i==null||i.SourceFingerprint!=source||i.OwnerEmployeeId!=employee||!string.Equals(i.Owner.UserName,user,StringComparison.OrdinalIgnoreCase))throw new UnauthorizedAccessException("ملف المحاولة يخص مصدرًا أو منفّذًا آخر.");}
  static void Text(BinaryWriter w,string s){w.Write(s!=null);if(s!=null)w.Write(s);}
  static string Text(BinaryReader r)=>r.ReadBoolean()?r.ReadString():null;
  static byte[] Encode(SalesWriteIntent i){
   using(var m=new MemoryStream()){
    using(var w=new BinaryWriter(m,Encoding.UTF8,true)){
     w.Write("PharmacySalesPending/v1");w.Write(i.RequestId.ToByteArray());w.Write(i.SourceFingerprint);w.Write(i.OwnerEmployeeId);var a=i.Owner;w.Write(a.UserName);w.Write(a.UserRole);w.Write(i.CommercialExpected);
     var hash=i.Fingerprint();w.Write(hash);var q=i.Snapshot();
     Text(w,q.ImportSourceSystem);Text(w,q.ImportSourceInvoiceId);w.Write(q.ImportDraftId.HasValue);if(q.ImportDraftId.HasValue)w.Write(q.ImportDraftId.Value.ToByteArray());
     Text(w,q.ImportCurrencyCode);Text(w,q.ExpectedCurrencyCode);w.Write(q.ImportTaxRatePercent.HasValue);if(q.ImportTaxRatePercent.HasValue)w.Write(q.ImportTaxRatePercent.Value);Text(w,q.ImportPriceTaxMode);
     Text(w,q.CustomerId);Text(w,q.CustomerName);Text(w,q.PhoneNumber);Text(w,q.CustomerAddress);Text(w,q.EmployeeName);Text(w,q.CashierEmployeeId);Text(w,q.Notes);w.Write(q.InvoiceDate.ToBinary());
     w.Write(q.DisplayedGrossTotal);w.Write(q.DisplayedNetTotal);w.Write(q.DiscountPercent);w.Write(q.UseCustomerCredit);Text(w,q.CreditCustomerId);w.Write(q.Lines.Count);
     foreach(var l in q.Lines){Text(w,l.MedicineName);w.Write(l.Quantity);w.Write(l.DisplayedSellingPrice);w.Write(l.Unit!=null);if(l.Unit==null)continue;var u=l.Unit;
      w.Write(u.UnitId.ToByteArray());w.Write(u.CatalogRevision);Text(w,u.Name);w.Write(u.BaseUnitsPerUnit);w.Write(u.EnteredQuantity);w.Write(u.UnitPrice);w.Write(u.CatalogPrice);Text(w,u.CurrencyCode);
     }w.Flush();
    }if(m.Length>MaximumBytes)throw new InvalidDataException("حجم محاولة البيع أكبر من الحد المسموح.");return m.ToArray();
   }
  }
  static byte[] Exact(BinaryReader r,int n){var b=r.ReadBytes(n);if(b.Length!=n)throw new EndOfStreamException();return b;}
  static SalesWriteIntent Decode(byte[] clear){
   if(clear.Length==0||clear.Length>MaximumBytes)throw new InvalidDataException("حجم ملف المحاولة غير صالح.");
   using(var m=new MemoryStream(clear,false))using(var r=new BinaryReader(m,Encoding.UTF8)){
    if(r.ReadString()!="PharmacySalesPending/v1")throw new InvalidDataException("إصدار ملف المحاولة غير مدعوم؛ احتُفظ به للتحقق.");
    var id=new Guid(Exact(r,16));string source=r.ReadString(),employee=r.ReadString();var actor=new OperationActor{UserName=r.ReadString(),UserRole=r.ReadString()};bool mode=r.ReadBoolean();var hash=Exact(r,32);
    var q=new SalesInvoiceIssueRequest{ImportSourceSystem=Text(r),ImportSourceInvoiceId=Text(r)};q.ImportDraftId=r.ReadBoolean()?(Guid?)new Guid(Exact(r,16)):null;
    q.ImportCurrencyCode=Text(r);q.ExpectedCurrencyCode=Text(r);q.ImportTaxRatePercent=r.ReadBoolean()?(decimal?)r.ReadDecimal():null;q.ImportPriceTaxMode=Text(r);
    q.CustomerId=Text(r);q.CustomerName=Text(r);q.PhoneNumber=Text(r);q.CustomerAddress=Text(r);q.EmployeeName=Text(r);q.CashierEmployeeId=Text(r);q.Notes=Text(r);q.InvoiceDate=DateTime.FromBinary(r.ReadInt64());
    q.DisplayedGrossTotal=r.ReadDecimal();q.DisplayedNetTotal=r.ReadDecimal();q.DiscountPercent=r.ReadDecimal();q.UseCustomerCredit=r.ReadBoolean();q.CreditCustomerId=Text(r);
    int count=r.ReadInt32();if(count<1||count>10000)throw new InvalidDataException("عدد بنود ملف المحاولة غير صالح.");var lines=new List<SalesInvoiceLineRequest>();
    for(int n=0;n<count;n++){var l=new SalesInvoiceLineRequest{MedicineName=Text(r),Quantity=r.ReadInt32(),DisplayedSellingPrice=r.ReadInt32()};if(r.ReadBoolean())l.Unit=new MedicineUnitSelection(new Guid(Exact(r,16)),r.ReadInt64(),Text(r),r.ReadInt32(),r.ReadDecimal(),r.ReadDecimal(),r.ReadDecimal(),Text(r));lines.Add(l);}
    q.Lines=lines;var intent=new SalesWriteIntent(id,employee,actor,q,source,mode);
    if(m.Position!=m.Length||!hash.SequenceEqual(intent.Fingerprint()))throw new InvalidDataException("بصمة ملف محاولة البيع غير متطابقة.");return intent;
   }
  }
  internal SalesWriteIntent Load(){Ensure();byte[] cipher;
   try{using(var f=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read)){if(f.Length<1||f.Length>MaximumBytes+65536)throw new InvalidDataException("حجم ملف استرجاع البيع غير صالح.");cipher=new byte[(int)f.Length];int n=0,k;while(n<cipher.Length&&(k=f.Read(cipher,n,cipher.Length-n))>0)n+=k;if(n!=cipher.Length)throw new EndOfStreamException();}}catch(FileNotFoundException){return null;}
   byte[] clear=ProtectedData.Unprotect(cipher,entropy,DataProtectionScope.CurrentUser);try{var i=Decode(clear);Verify(i);return i;}finally{Array.Clear(clear,0,clear.Length);}
  }
  internal void Save(SalesWriteIntent intent){Ensure();Verify(intent);var previous=Load();if(previous!=null){if(previous.RequestId==intent.RequestId&&previous.Fingerprint().SequenceEqual(intent.Fingerprint()))return;throw new InvalidOperationException("توجد محاولة بيع معلقة؛ تحقق منها قبل بدء محاولة أخرى.");}
   byte[] clear=Encode(intent),cipher;try{cipher=ProtectedData.Protect(clear,entropy,DataProtectionScope.CurrentUser);}finally{Array.Clear(clear,0,clear.Length);}
   string temp=Path.Combine(Path.GetDirectoryName(path),".sales-"+Guid.NewGuid().ToString("N")+".tmp");
   try{using(var f=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){f.Write(cipher,0,cipher.Length);f.Flush(true);}File.Move(temp,path);}finally{if(File.Exists(temp))File.Delete(temp);}
  }
  // Caller must prove the displayed server receipt before invoking this operation.
  internal void RemoveVerified(SalesWriteIntent verified){Ensure();Verify(verified);var pending=Load();if(pending==null||pending.RequestId!=verified.RequestId||!pending.Fingerprint().SequenceEqual(verified.Fingerprint()))throw new InvalidOperationException("لم تثبت هوية المحاولة؛ احتُفظ بملف الاسترجاع.");File.Delete(path);}
  public void Dispose(){var held=lease;lease=null;held?.Dispose();}
 }
}

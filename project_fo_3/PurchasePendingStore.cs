using PharmacyManagement.Domain;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using System.Collections.Generic;
using PharmacyManagement.Application;
namespace project_fo_3 {
 internal sealed class PurchasePendingStore:IDisposable {
  const int MaximumBytes=4194304;
  readonly string source,employee,user,path; readonly byte[] entropy; FileStream lease;
  internal PurchasePendingStore(string directory,string sourceKey,string employeeId,string userName) {
   if(string.IsNullOrWhiteSpace(directory)||sourceKey==null||sourceKey.Length!=64||
      sourceKey.Any(c=>!"0123456789ABCDEF".Contains(c.ToString()))||string.IsNullOrWhiteSpace(employeeId)||
      employeeId.Length>50||string.IsNullOrWhiteSpace(userName)||userName.Trim().Length>200)
    throw new ArgumentException("سياق استعادة المشتريات غير صالح.");
   source=sourceKey;employee=employeeId;user=userName.Trim();
   string key;
   using(var m=new MemoryStream()) {
    using(var w=new BinaryWriter(m,Encoding.UTF8,true)){w.Write(source);w.Write(employee);w.Write(user.ToUpperInvariant());}
    using(var h=SHA256.Create())key=BitConverter.ToString(h.ComputeHash(m.ToArray())).Replace("-","");
   }
   Directory.CreateDirectory(directory);path=Path.Combine(Path.GetFullPath(directory),key+"-Purchase.pending");
   entropy=Encoding.UTF8.GetBytes("PharmacyPurchasePending/v1/"+key);
   lease=new FileStream(path+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
  }
  void Ensure(){if(lease==null)throw new ObjectDisposedException(nameof(PurchasePendingStore));}
  void Verify(PurchaseWriteIntent i) {
   if(i==null||i.SourceFingerprint!=source||i.OwnerEmployeeId!=employee||
      !string.Equals(i.Owner.UserName,user,StringComparison.OrdinalIgnoreCase))
    throw new InvalidOperationException("المحاولة لا تطابق سياق استعادة المشتريات.");
  }
  internal PurchaseWriteIntent Load() {
   Ensure();byte[] cipher;
   try {using(var f=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read)){
    if(f.Length<=0||f.Length>MaximumBytes+65536)throw new InvalidDataException("حجم ملف الاستعادة غير صالح.");
    cipher=new byte[(int)f.Length];int n=0,k;
    while(n<cipher.Length&&(k=f.Read(cipher,n,cipher.Length-n))>0)n+=k;
    if(n!=cipher.Length)throw new EndOfStreamException();
   }}catch(FileNotFoundException){return null;}
   byte[] clear=ProtectedData.Unprotect(cipher,entropy,DataProtectionScope.CurrentUser);
   try {
    if(clear.Length>MaximumBytes)throw new InvalidDataException();
    using(var m=new MemoryStream(clear,false))using(var r=new BinaryReader(m,Encoding.UTF8)){
     string version=r.ReadString();
     if(version!="PharmacyPurchasePending/v1"&&version!="PharmacyPurchasePending/v2"&&version!="PharmacyPurchasePending/v3")throw new InvalidDataException();
     Guid id=new Guid(r.ReadString());string storedSource=r.ReadString(),storedEmployee=r.ReadString();
     var actor=new OperationActor{UserName=r.ReadString(),UserRole=r.ReadString()};
     byte[] fingerprint=r.ReadBytes(32);if(fingerprint.Length!=32)throw new EndOfStreamException();
     var q=new PurchaseInvoiceRequest {
      DesiredPurchaseId=r.ReadString(),SupplierName=r.ReadString(),EmployeeName=r.ReadString(),ActorRole=r.ReadString(),
      Notes=r.ReadString(),PurchaseDate=DateTime.FromBinary(r.ReadInt64()),CommercialInventoryExpected=r.ReadBoolean(),
      ImportSourceSystem=r.ReadString(),ImportSourceInvoiceId=r.ReadString()
     };
     q.ImportDraftId=r.ReadBoolean()?(Guid?)new Guid(r.ReadString()):null;
     q.ImportCurrencyCode=r.ReadString();q.ImportTaxRatePercent=r.ReadBoolean()?(decimal?)r.ReadDecimal():null;
     q.ImportPaymentMethod=r.ReadString();int count=r.ReadInt32();
     if(count<=0||count>10000)throw new InvalidDataException("عدد بنود الاستعادة غير صالح.");
     var lines=new List<PurchaseInvoiceLineRequest>();
     for(int n=0;n<count;n++)lines.Add(new PurchaseInvoiceLineRequest{
      LineNumber=r.ReadInt32(),DesiredDetailId=r.ReadString(),MedicineName=r.ReadString(),BuyingPrice=r.ReadInt32(),
      Quantity=r.ReadInt32(),ProductionDate=DateTime.FromBinary(r.ReadInt64()),ExpiryDate=DateTime.FromBinary(r.ReadInt64()),
      Gtin=r.ReadString(),BatchNumber=r.ReadString(),SerialNumber=r.ReadString()
     });
     if(version=="PharmacyPurchasePending/v2"||version=="PharmacyPurchasePending/v3")q.ExpectedSystemCurrencyCode=r.ReadString();
     if(version=="PharmacyPurchasePending/v3")foreach(var line in lines)if(r.ReadBoolean())
      line.Unit=new MedicineUnitSelection(new Guid(r.ReadBytes(16)),r.ReadInt64(),r.ReadString(),r.ReadInt32(),r.ReadDecimal(),r.ReadDecimal(),r.ReadDecimal(),r.ReadString());
     q.Lines=lines;if(m.Position!=m.Length)throw new InvalidDataException("بيانات زائدة في ملف الاستعادة.");
     var intent=new PurchaseWriteIntent(id,storedEmployee,actor,q,q.PurchaseDate,storedSource);Verify(intent);
     if(!fingerprint.SequenceEqual(intent.Fingerprint()))throw new InvalidDataException("بصمة الاستعادة غير مطابقة.");
     return intent; // Loading observes a pending request; it never executes a write.
    }
   }finally{Array.Clear(clear,0,clear.Length);}
  }
  internal void Save(PurchaseWriteIntent i) {
   Ensure();Verify(i);var previous=Load();
   if(previous!=null) {
    if(previous.RequestId!=i.RequestId||!previous.Fingerprint().SequenceEqual(i.Fingerprint()))
     throw new InvalidOperationException("يوجد حفظ مشتريات غير محسوم؛ لا يمكن استبداله بمحاولة أخرى.");
    return;
   }
   byte[] cipher;
   using(var m=new MemoryStream()) {
    using(var w=new BinaryWriter(m,Encoding.UTF8,true)){
     var q=i.Snapshot();var a=i.Owner;if(q.Lines.Count>10000)throw new InvalidOperationException("عدد بنود الاستعادة أكبر من المسموح.");
     w.Write("PharmacyPurchasePending/v3");w.Write(i.RequestId.ToString("N"));w.Write(source);w.Write(employee);
     w.Write(a.UserName);w.Write(a.UserRole);w.Write(i.Fingerprint());
     w.Write(q.DesiredPurchaseId);w.Write(q.SupplierName);w.Write(q.EmployeeName);w.Write(q.ActorRole);w.Write(q.Notes);
     w.Write(q.PurchaseDate.ToBinary());w.Write(q.CommercialInventoryExpected);
     w.Write(q.ImportSourceSystem??"");w.Write(q.ImportSourceInvoiceId??"");
     w.Write(q.ImportDraftId.HasValue);if(q.ImportDraftId.HasValue)w.Write(q.ImportDraftId.Value.ToString("N"));
     w.Write(q.ImportCurrencyCode??"");w.Write(q.ImportTaxRatePercent.HasValue);if(q.ImportTaxRatePercent.HasValue)w.Write(q.ImportTaxRatePercent.Value);
     w.Write(q.ImportPaymentMethod??"");w.Write(q.Lines.Count);
     foreach(var l in q.Lines){w.Write(l.LineNumber);w.Write(l.DesiredDetailId);w.Write(l.MedicineName);
      w.Write(l.BuyingPrice);w.Write(l.Quantity);w.Write(l.ProductionDate.ToBinary());w.Write(l.ExpiryDate.ToBinary());
      w.Write(l.Gtin);w.Write(l.BatchNumber);w.Write(l.SerialNumber);}
     w.Write(q.ExpectedSystemCurrencyCode??"");
     foreach(var line in q.Lines){w.Write(line.Unit!=null);if(line.Unit==null)continue;var u=line.Unit;
      w.Write(u.UnitId.ToByteArray());w.Write(u.CatalogRevision);w.Write(u.Name);w.Write(u.BaseUnitsPerUnit);
      w.Write(u.EnteredQuantity);w.Write(u.UnitPrice);w.Write(u.CatalogPrice);w.Write(u.CurrencyCode);}
    }
    if(m.Length>MaximumBytes)throw new InvalidOperationException("بيانات الاستعادة أكبر من المسموح.");
    byte[] clear=m.ToArray();
    try{cipher=ProtectedData.Protect(clear,entropy,DataProtectionScope.CurrentUser);}finally{Array.Clear(clear,0,clear.Length);}
   }
   string temp=Path.Combine(Path.GetDirectoryName(path),Guid.NewGuid().ToString("N")+".tmp");
   try {using(var f=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None,4096,FileOptions.WriteThrough)){
    f.Write(cipher,0,cipher.Length);f.Flush(true);}
    File.Move(temp,path);
   }finally{if(File.Exists(temp))File.Delete(temp);}
  }
  internal void RemoveVerified(PurchaseWriteIntent intent) {
   Ensure();Verify(intent);var previous=Load();if(previous==null)return;
   if(previous.RequestId!=intent.RequestId||!previous.Fingerprint().SequenceEqual(intent.Fingerprint()))
    throw new InvalidOperationException("محاولة الاستعادة لا تطابق النتيجة المثبتة.");
   File.Delete(path);
  }
  public void Dispose(){if(lease!=null){lease.Dispose();lease=null;}}
 }
}



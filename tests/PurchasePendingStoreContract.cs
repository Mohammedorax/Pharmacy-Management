using System;
using System.IO;
using System.Linq;
using PharmacyManagement.Application;
namespace project_fo_3 {
 class PurchasePendingProbe {
  static int checks;static void Check(bool value,string message){if(!value)throw new Exception(message);checks++;}
  static void Reject(Action action){bool denied=false;try{action();}catch{denied=true;}Check(denied,"Expected rejection");}
  static int Main(){
   string dir=Path.Combine(Path.GetTempPath(),"PurchasePendingProbe_"+Guid.NewGuid().ToString("N"));
   try{
    string key=new string('A',64);
    var q=new PurchaseInvoiceRequest{DesiredPurchaseId="001",SupplierName="مورد عربي",EmployeeName="Owner",
     ActorRole="Manager",Notes="ملاحظة\nثانية",PurchaseDate=new DateTime(2026,10,6,12,34,56,DateTimeKind.Unspecified),
     ImportSourceSystem="import",ImportSourceInvoiceId="SRC-1",ImportDraftId=Guid.NewGuid(),
     ExpectedSystemCurrencyCode="YER",ImportCurrencyCode="YER",ImportTaxRatePercent=15m,ImportPaymentMethod="Payable",
     Lines=new[]{new PurchaseInvoiceLineRequest{LineNumber=1,DesiredDetailId="002",MedicineName="دواء",BuyingPrice=7,
      Quantity=2,ProductionDate=new DateTime(2026,1,1),ExpiryDate=new DateTime(2027,1,1),Gtin="123",BatchNumber="دفعة",SerialNumber="S"}}};
    var i=new PurchaseWriteIntent(Guid.NewGuid()," 007 ",new OperationActor{UserName="Owner",UserRole="Manager"},q,q.PurchaseDate,key);
    using(var s=new PurchasePendingStore(dir,key," 007 ","Owner")){
     Check(s.Load()==null,"empty");s.Save(i);s.Save(i);
     Reject(()=>{using(var other=new PurchasePendingStore(dir,key," 007 ","Owner")){}});
     q.Lines[0].Quantity=99;
     var loaded=s.Load();Check(loaded.RequestId==i.RequestId,"nonce");Check(loaded.OwnerEmployeeId==" 007 ","raw employee");
     Check(loaded.Fingerprint().SequenceEqual(i.Fingerprint()),"fingerprint");
     Check(loaded.Snapshot().Lines[0].Quantity==2,"immutable payload");
     Check(loaded.Snapshot().Notes=="ملاحظة\nثانية","Arabic/newline");
     Check(loaded.Snapshot().ImportDraftId==q.ImportDraftId,"import draft");
     Check(loaded.Snapshot().ImportTaxRatePercent==15m,"tax");
     Check(loaded.Snapshot().ExpectedSystemCurrencyCode=="YER","captured currency");
     var changedCurrency=i.Snapshot();changedCurrency.ExpectedSystemCurrencyCode="USD";
     Reject(()=>s.Save(new PurchaseWriteIntent(i.RequestId," 007 ",i.Owner,changedCurrency,q.PurchaseDate,key)));
     Check(loaded.Snapshot().PurchaseDate==q.PurchaseDate,"business clock");
     Reject(()=>s.Save(new PurchaseWriteIntent(Guid.NewGuid()," 007 ",i.Owner,i.Snapshot(),q.PurchaseDate,key)));
     var changed=i.Snapshot();changed.Lines[0].Quantity=3;
     Reject(()=>s.Save(new PurchaseWriteIntent(i.RequestId," 007 ",i.Owner,changed,q.PurchaseDate,key)));
    }
    using(var s=new PurchasePendingStore(dir,key," 007 ","owner"))Check(s.Load().RequestId==i.RequestId,"reopened case-insensitive owner");
    using(var s=new PurchasePendingStore(dir,new string('B',64)," 007 ","Owner")){Check(s.Load()==null,"source isolation");Reject(()=>s.Save(i));}
    using(var s=new PurchasePendingStore(dir,key,"007","Owner")){Check(s.Load()==null,"raw employee isolation");Reject(()=>s.Save(i));}
    string legacyDir=Path.Combine(dir,"legacy");
    var legacyRequest=i.Snapshot();legacyRequest.ExpectedSystemCurrencyCode=null;
    var legacyIntent=new PurchaseWriteIntent(i.RequestId," 007 ",i.Owner,legacyRequest,q.PurchaseDate,key);
    using(var s=new PurchasePendingStore(legacyDir,key," 007 ","Owner"))s.Save(legacyIntent);
    string legacyPath=Directory.GetFiles(legacyDir,"*.pending").Single();
    string fileKey=Path.GetFileName(legacyPath).Replace("-Purchase.pending","");
    byte[] entropy=System.Text.Encoding.UTF8.GetBytes("PharmacyPurchasePending/v1/"+fileKey);
    byte[] clear=System.Security.Cryptography.ProtectedData.Unprotect(File.ReadAllBytes(legacyPath),entropy,System.Security.Cryptography.DataProtectionScope.CurrentUser);
    using(var m=new MemoryStream(clear))using(var reader=new BinaryReader(m,System.Text.Encoding.UTF8)){
     Check(reader.ReadString()=="PharmacyPurchasePending/v2","new store version");
     int headerEnd=(int)m.Position;byte[] oldHeader;
     using(var header=new MemoryStream()){using(var writer=new BinaryWriter(header,System.Text.Encoding.UTF8,true))writer.Write("PharmacyPurchasePending/v1");oldHeader=header.ToArray();}
     Check(clear[clear.Length-1]==0,"unknown currency stored empty");
     byte[] legacyClear=oldHeader.Concat(clear.Skip(headerEnd).Take(clear.Length-headerEnd-1)).ToArray();
     File.WriteAllBytes(legacyPath,System.Security.Cryptography.ProtectedData.Protect(legacyClear,entropy,System.Security.Cryptography.DataProtectionScope.CurrentUser));
     Array.Clear(legacyClear,0,legacyClear.Length);
    }
    Array.Clear(clear,0,clear.Length);
    using(var s=new PurchasePendingStore(legacyDir,key," 007 ","Owner")){
     var old=s.Load();Check(old.RequestId==i.RequestId,"legacy original nonce");
     Check(old.Snapshot().ExpectedSystemCurrencyCode==null,"legacy currency remains unknown");
     Check(old.Fingerprint().SequenceEqual(legacyIntent.Fingerprint()),"legacy fingerprint preserved");
     s.Save(old);
     Reject(()=>s.Save(i));
    }
    string p=Directory.GetFiles(dir,"*.pending").Single();var bytes=File.ReadAllBytes(p);
    Check(!System.Text.Encoding.UTF8.GetString(bytes).Contains("مورد عربي"),"encrypted supplier");
    bytes[bytes.Length/2]^=1;File.WriteAllBytes(p,bytes);
    using(var s=new PurchasePendingStore(dir,key," 007 ","Owner"))Reject(()=>s.Load());
    Console.WriteLine("PASS purchase pending store: "+checks+" checks; DPAPI, lease, reopen, immutable full import payload, source/owner isolation, tamper rejected; no SQL/UI.");return 0;
   }catch(Exception e){Console.Error.WriteLine(e);return 1;}
   finally{if(Directory.Exists(dir))Directory.Delete(dir,true);}
  }
 }
}

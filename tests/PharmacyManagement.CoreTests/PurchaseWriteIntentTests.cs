using System;
using System.Linq;
using System.Globalization;
using PharmacyManagement.Application;
namespace PharmacyManagement.CoreTests {
internal static class PurchaseWriteIntentTests {
 static int checks;static void Check(bool value){if(!value)throw new Exception("Identity check "+checks);checks++;}
 static void Reject<T>(Action action) where T:Exception{try{action();}catch(T){checks++;return;}throw new Exception("Expected "+typeof(T).Name);}
 static PurchaseInvoiceRequest Input()=>new PurchaseInvoiceRequest{DesiredPurchaseId="001",SupplierName="مورد",EmployeeName="manager",ActorRole="Manager",Notes="notes",PurchaseDate=new DateTime(2026,10,6),Lines=new[]{new PurchaseInvoiceLineRequest{LineNumber=1,DesiredDetailId="0001",MedicineName="دواء",BuyingPrice=7,Quantity=2,ProductionDate=new DateTime(2025,1,2),ExpiryDate=new DateTime(2030,1,2)}}};
 internal static int Run(){checks=0;
  var now=new DateTime(2026,10,6);var actor=new OperationActor{UserName="manager",UserRole="Manager"};var request=Input();var id=Guid.NewGuid();
  Reject<ArgumentException>(()=>new PurchaseWriteIntent(Guid.Empty,"0001",actor,request,now));
  Reject<ArgumentException>(()=>new PurchaseWriteIntent(id," ",actor,request,now));
  Reject<UnauthorizedAccessException>(()=>new PurchaseWriteIntent(id,"0001",null,request,now));
  var wrong=Input();wrong.EmployeeName="other";Reject<UnauthorizedAccessException>(()=>new PurchaseWriteIntent(id,"0001",actor,wrong,now));
  wrong=Input();wrong.ActorRole="Cashier";Reject<UnauthorizedAccessException>(()=>new PurchaseWriteIntent(id,"0001",actor,wrong,now));
  var intent=new PurchaseWriteIntent(id,"0001",actor,request,now);var hash=intent.Fingerprint();Check(hash.Length==32);
  request.Lines[0].Quantity=99;request.Notes="mutated";actor.UserName="mutated";Check(intent.Fingerprint().SequenceEqual(hash));
  var snapshot=intent.Snapshot();Check(snapshot.Lines[0].Quantity==2&&snapshot.Notes=="notes");snapshot.Lines[0].Quantity=88;Check(intent.Fingerprint().SequenceEqual(hash));
  var owner=intent.Owner;owner.UserName="edited";Check(intent.Owner.UserName=="manager");
  actor.UserName="manager";
  Check(new PurchaseWriteIntent(Guid.NewGuid(),"0001",actor,Input(),now).Fingerprint().SequenceEqual(hash));
  Check(!new PurchaseWriteIntent(id,"0002",actor,Input(),now).Fingerprint().SequenceEqual(hash));
  var changed=Input();changed.Lines[0].Quantity=3;Check(!new PurchaseWriteIntent(id,"0001",actor,changed,now).Fingerprint().SequenceEqual(hash));
  changed=Input();changed.DesiredPurchaseId="002";Check(!new PurchaseWriteIntent(id,"0001",actor,changed,now).Fingerprint().SequenceEqual(hash));
  changed=Input();changed.PurchaseDate=default(DateTime);var captured=new PurchaseWriteIntent(id," 0001 ",actor,changed,now);Check(captured.Snapshot().PurchaseDate==now&&captured.OwnerEmployeeId==" 0001 ");
  var culture=CultureInfo.CurrentCulture;try{CultureInfo.CurrentCulture=new CultureInfo("ar-SA");Check(intent.Fingerprint().SequenceEqual(hash));}finally{CultureInfo.CurrentCulture=culture;}
  string sourceA=new string('A',64),sourceB=new string('B',64);
  var sourceBound=new PurchaseWriteIntent(id,"0001",actor,Input(),now,sourceA);
  Check(sourceBound.SourceFingerprint==sourceA);
  Check(!sourceBound.Fingerprint().SequenceEqual(new PurchaseWriteIntent(id,"0001",actor,Input(),now,sourceB).Fingerprint()));
  Reject<ArgumentException>(()=>new PurchaseWriteIntent(id,"0001",actor,Input(),now,"invalid"));
  Reject<ArgumentException>(()=>new PurchaseWriteIntent(id,"0001",actor,Input(),now,new string('a',64)));
  var currencyRequest=Input();currencyRequest.ExpectedSystemCurrencyCode=" yer ";
  var currencyIntent=new PurchaseWriteIntent(id,"0001",actor,currencyRequest,now);
  Check(currencyIntent.Snapshot().ExpectedSystemCurrencyCode=="YER");
  Check(!currencyIntent.Fingerprint().SequenceEqual(hash));
  currencyRequest.ExpectedSystemCurrencyCode="USD";Check(currencyIntent.Snapshot().ExpectedSystemCurrencyCode=="YER");
  var currencyChanged=currencyIntent.Snapshot();currencyChanged.ExpectedSystemCurrencyCode="USD";
  Check(!currencyIntent.Fingerprint().SequenceEqual(new PurchaseWriteIntent(id,"0001",actor,currencyChanged,now).Fingerprint()));
  Check(intent.Snapshot().ExpectedSystemCurrencyCode==null);
  currencyChanged.ExpectedSystemCurrencyCode="YER1";Reject<InvalidOperationException>(()=>new PurchaseWriteIntent(id,"0001",actor,currencyChanged,now));
  currencyChanged.ExpectedSystemCurrencyCode="ABCDEFGHIJK";Reject<InvalidOperationException>(()=>new PurchaseWriteIntent(id,"0001",actor,currencyChanged,now));
  return checks;
 }
}
}

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
namespace PharmacyManagement.Application {
 public sealed class PurchaseWriteIntent {
  readonly PurchaseInvoiceRequest request;readonly OperationActor owner;
  public string SourceFingerprint{get;private set;}
  public Guid RequestId{get;private set;}public string OwnerEmployeeId{get;private set;}
  public PurchaseWriteIntent(Guid requestId,string ownerEmployeeId,OperationActor actor,PurchaseInvoiceRequest input,DateTime capturedTime,string sourceFingerprint=null){
   if(requestId==Guid.Empty)throw new ArgumentException("معرف طلب المشتريات مطلوب.",nameof(requestId));
   if(string.IsNullOrWhiteSpace(ownerEmployeeId)||ownerEmployeeId.Length>50)throw new ArgumentException("هوية الموظف مطلوبة.",nameof(ownerEmployeeId));
   if(actor==null||string.IsNullOrWhiteSpace(actor.UserName)||string.IsNullOrWhiteSpace(actor.UserRole))throw new UnauthorizedAccessException("هوية منفذ المشتريات مطلوبة.");
   if(input==null)throw new ArgumentNullException(nameof(input));
   if(sourceFingerprint!=null&&(sourceFingerprint.Length!=64||System.Array.Exists(sourceFingerprint.ToCharArray(),c=>!("0123456789ABCDEF".Contains(c.ToString())))))throw new ArgumentException("بصمة مصدر المشتريات غير صالحة.");
   SourceFingerprint=sourceFingerprint;
   owner=new OperationActor{UserName=actor.UserName.Trim(),UserRole=actor.UserRole.Trim()};
   if(owner.UserName.Length>200||owner.UserRole.Length>50)throw new ArgumentException("هوية منفذ المشتريات أطول من الحد المسموح.");
   DateTime businessTime=input.PurchaseDate==default(DateTime)?capturedTime:input.PurchaseDate;
   var normalized=PurchaseInvoiceValidation.Normalize(input,businessTime.Date);
   if(!string.Equals(normalized.EmployeeName,owner.UserName,StringComparison.OrdinalIgnoreCase)||
      (normalized.ActorRole.Length!=0&&!string.Equals(normalized.ActorRole,owner.UserRole,StringComparison.OrdinalIgnoreCase)))
    throw new UnauthorizedAccessException("منفذ طلب المشتريات لا يطابق الموظف الحالي.");
   normalized.ActorRole=owner.UserRole;
   if(normalized.PurchaseDate==default(DateTime))normalized.PurchaseDate=capturedTime;
   request=normalized;RequestId=requestId;OwnerEmployeeId=ownerEmployeeId;
  }
  public OperationActor Owner=>new OperationActor{UserName=owner.UserName,UserRole=owner.UserRole};
  public PurchaseInvoiceRequest Snapshot()=>PurchaseInvoiceValidation.Normalize(request,request.PurchaseDate.Date);
  public byte[] Fingerprint(){
   using(var stream=new MemoryStream()){
    using(var writer=new BinaryWriter(stream,Encoding.UTF8,true)){
     writer.Write("PharmacyManagement.PurchaseWrite.v2");writer.Write(SourceFingerprint??"");writer.Write(OwnerEmployeeId);
     writer.Write(owner.UserName.ToUpperInvariant());writer.Write(owner.UserRole.ToUpperInvariant());
     writer.Write(request.SupplierName);writer.Write(request.EmployeeName);writer.Write(request.ActorRole);
     writer.Write(request.Notes);writer.Write(request.PurchaseDate.ToBinary());writer.Write(request.CommercialInventoryExpected);
     writer.Write(request.DesiredPurchaseId);
     writer.Write(request.ImportSourceSystem??"");writer.Write(request.ImportSourceInvoiceId??"");
     writer.Write(request.ImportDraftId.HasValue);if(request.ImportDraftId.HasValue)writer.Write(request.ImportDraftId.Value.ToByteArray());
     writer.Write(request.ImportCurrencyCode??"");writer.Write(request.ImportTaxRatePercent.HasValue);if(request.ImportTaxRatePercent.HasValue)writer.Write(request.ImportTaxRatePercent.Value);
     writer.Write(request.ImportPaymentMethod??"");writer.Write(request.Lines.Count);
     foreach(var line in request.Lines){
      writer.Write(line.LineNumber);writer.Write(line.DesiredDetailId);writer.Write(line.MedicineName);
      writer.Write(line.BuyingPrice);writer.Write(line.Quantity);writer.Write(line.ProductionDate.Ticks);writer.Write(line.ExpiryDate.Ticks);
      writer.Write(line.Gtin);writer.Write(line.BatchNumber);writer.Write(line.SerialNumber);
     }
     // Preserve legacy fingerprints when the original request had no currency snapshot.
     if(!string.IsNullOrEmpty(request.ExpectedSystemCurrencyCode)){
      writer.Write("PharmacyManagement.PurchaseWrite.Currency.v1");writer.Write(request.ExpectedSystemCurrencyCode);
     }
     bool hasUnits=false;foreach(var line in request.Lines)if(line.Unit!=null){hasUnits=true;break;}
     if(hasUnits){
      writer.Write("PharmacyManagement.PurchaseWrite.Units.v1");
      foreach(var line in request.Lines){writer.Write(line.Unit!=null);if(line.Unit==null)continue;var u=line.Unit;
       writer.Write(u.UnitId.ToByteArray());writer.Write(u.CatalogRevision);writer.Write(u.Name);writer.Write(u.BaseUnitsPerUnit);
       writer.Write(u.EnteredQuantity.ToString("G29",System.Globalization.CultureInfo.InvariantCulture));
       writer.Write(u.UnitPrice.ToString("G29",System.Globalization.CultureInfo.InvariantCulture));
       writer.Write(u.CatalogPrice.ToString("G29",System.Globalization.CultureInfo.InvariantCulture));writer.Write(u.CurrencyCode);
      }
     }
     writer.Flush();
    }
    using(var hash=SHA256.Create())return hash.ComputeHash(stream.ToArray());
   }
  }
 }
}



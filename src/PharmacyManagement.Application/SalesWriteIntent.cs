using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using PharmacyManagement.Domain;
namespace PharmacyManagement.Application
{
    // Candidate contract only. Server receipt, durable pending storage and UI recovery
    // must be integrated together before this can protect a real POS submission.
    public sealed class SalesWriteIntent
    {
        readonly SalesInvoiceIssueRequest request;
        readonly OperationActor owner;
        public Guid RequestId { get; }
        public string OwnerEmployeeId { get; }
        public string SourceFingerprint { get; }
        public bool CommercialExpected { get; }
        public OperationActor Owner => new OperationActor { UserName=owner.UserName, UserRole=owner.UserRole };
        public SalesWriteIntent(Guid requestId,string employee,OperationActor actor,SalesInvoiceIssueRequest input,string sourceFingerprint,bool commercialExpected=false)
        {
            if(requestId==Guid.Empty)throw new ArgumentException("معرف محاولة البيع مطلوب.");
            if(string.IsNullOrWhiteSpace(employee)||employee.Length>50)throw new ArgumentException("هوية منفّذ البيع مطلوبة.");
            if(actor==null||string.IsNullOrWhiteSpace(actor.UserName)||string.IsNullOrWhiteSpace(actor.UserRole))throw new UnauthorizedAccessException("هوية منفّذ البيع مطلوبة.");
            if(actor.UserName.Trim().Length>200||actor.UserRole.Trim().Length>50)throw new ArgumentException("هوية منفّذ البيع أطول من الحد المسموح.");
            if(sourceFingerprint==null||sourceFingerprint.Length!=64||sourceFingerprint.Any(c=>!"0123456789ABCDEF".Contains(c.ToString())))throw new ArgumentException("بصمة مصدر البيع غير صالحة.");
            if(input==null)throw new ArgumentNullException(nameof(input));
            if(string.IsNullOrWhiteSpace(input.ExpectedCurrencyCode))throw new ArgumentException("لقطة عملة محاولة البيع مطلوبة.");
            if(input.Lines==null||input.Lines.Count==0||input.Lines.Count>10000||input.Lines.Any(l=>l==null))throw new ArgumentException("بنود محاولة البيع غير صالحة.");
            RequestId=requestId;OwnerEmployeeId=employee;SourceFingerprint=sourceFingerprint;
            CommercialExpected=commercialExpected;
            owner=new OperationActor{UserName=actor.UserName.Trim(),UserRole=actor.UserRole.Trim()};
            // Original seller and actual submitting actor are separate identities.
            // A manager may select another seller under the existing permission policy.
            request=Copy(input);
        }
        public SalesInvoiceIssueRequest Snapshot()=>Copy(request);
        static SalesInvoiceIssueRequest Copy(SalesInvoiceIssueRequest q)=>new SalesInvoiceIssueRequest{
            ImportSourceSystem=q.ImportSourceSystem,ImportSourceInvoiceId=q.ImportSourceInvoiceId,ImportDraftId=q.ImportDraftId,
            ImportCurrencyCode=q.ImportCurrencyCode,ExpectedCurrencyCode=q.ExpectedCurrencyCode,
            ImportTaxRatePercent=q.ImportTaxRatePercent,ImportPriceTaxMode=q.ImportPriceTaxMode,
            CustomerId=q.CustomerId,CustomerName=q.CustomerName,PhoneNumber=q.PhoneNumber,CustomerAddress=q.CustomerAddress,
            EmployeeName=q.EmployeeName,CashierEmployeeId=q.CashierEmployeeId,Notes=q.Notes,InvoiceDate=q.InvoiceDate,
            DisplayedGrossTotal=q.DisplayedGrossTotal,DisplayedNetTotal=q.DisplayedNetTotal,DiscountPercent=q.DiscountPercent,
            UseCustomerCredit=q.UseCustomerCredit,CreditCustomerId=q.CreditCustomerId,
            Lines=q.Lines.Select(l=>new SalesInvoiceLineRequest{MedicineName=l.MedicineName?.Trim(),Unit=l.Unit,
                DisplayedSellingPrice=l.DisplayedSellingPrice,Quantity=l.Quantity}).ToArray()
        };
        static void Text(BinaryWriter w,string value){w.Write(value!=null);if(value!=null)w.Write(value);}
        static void Money(BinaryWriter w,decimal value)=>w.Write(value.ToString("G29",CultureInfo.InvariantCulture));
        public byte[] Fingerprint()
        {
            using(var stream=new MemoryStream()){
                using(var w=new BinaryWriter(stream,Encoding.UTF8,true)){
                    w.Write("PharmacyManagement.SalesWrite.v1");w.Write(SourceFingerprint);w.Write(OwnerEmployeeId);
                    w.Write(CommercialExpected);
                    w.Write(owner.UserName.ToUpperInvariant());w.Write(owner.UserRole.ToUpperInvariant());
                    var q=request;Text(w,q.ImportSourceSystem);Text(w,q.ImportSourceInvoiceId);
                    w.Write(q.ImportDraftId.HasValue);if(q.ImportDraftId.HasValue)w.Write(q.ImportDraftId.Value.ToByteArray());
                    Text(w,q.ImportCurrencyCode);Text(w,q.ExpectedCurrencyCode);w.Write(q.ImportTaxRatePercent.HasValue);
                    if(q.ImportTaxRatePercent.HasValue)Money(w,q.ImportTaxRatePercent.Value);Text(w,q.ImportPriceTaxMode);
                    Text(w,q.CustomerId);Text(w,q.CustomerName);Text(w,q.PhoneNumber);Text(w,q.CustomerAddress);
                    Text(w,q.EmployeeName);Text(w,q.CashierEmployeeId);Text(w,q.Notes);w.Write(q.InvoiceDate.ToBinary());
                    Money(w,q.DisplayedGrossTotal);Money(w,q.DisplayedNetTotal);Money(w,q.DiscountPercent);
                    w.Write(q.UseCustomerCredit);Text(w,q.CreditCustomerId);w.Write(q.Lines.Count);
                    foreach(var l in q.Lines){
                        Text(w,l.MedicineName);w.Write(l.Quantity);w.Write(l.DisplayedSellingPrice);w.Write(l.Unit!=null);
                        if(l.Unit==null)continue;var u=l.Unit;w.Write(u.UnitId.ToByteArray());w.Write(u.CatalogRevision);
                        Text(w,u.Name);w.Write(u.BaseUnitsPerUnit);Money(w,u.EnteredQuantity);Money(w,u.UnitPrice);
                        Money(w,u.CatalogPrice);Text(w,u.CurrencyCode);
                    }
                    w.Flush();
                }
                using(var hash=SHA256.Create())return hash.ComputeHash(stream.ToArray());
            }
        }
    }
}

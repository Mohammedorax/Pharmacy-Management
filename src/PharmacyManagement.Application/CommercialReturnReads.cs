using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
namespace PharmacyManagement.Application
{
    public sealed class CommercialReturnHistoryReadRequest
    {
        public Guid OriginalInvoiceId{get;private set;}
        public CommercialReturnHistoryReadRequest(Guid invoiceId){if(invoiceId==Guid.Empty)throw new ArgumentException("Original invoice is required.",nameof(invoiceId));OriginalInvoiceId=invoiceId;}
    }
    public sealed class CommercialReturnDocumentReadRequest
    {
        public Guid OriginalInvoiceId{get;private set;}
        public Guid ReturnId{get;private set;}
        public CommercialReturnDocumentReadRequest(Guid invoiceId,Guid returnId){if(invoiceId==Guid.Empty||returnId==Guid.Empty)throw new ArgumentException("Original invoice and return are required.");OriginalInvoiceId=invoiceId;ReturnId=returnId;}
    }
    public sealed class CommercialReturnDocumentLine
    {
        public string MedicineName{get;private set;}
        public decimal Quantity{get;private set;}public decimal GrossTotal{get;private set;}public decimal DiscountTotal{get;private set;}
        public decimal TaxRate{get;private set;}public decimal TaxTotal{get;private set;}public decimal NetTotal{get;private set;}
        public string UnitName{get;private set;}public decimal UnitPrice{get;private set;}public int PriceBaseQuantity{get;private set;}
        public CommercialReturnDocumentLine(string medicine,decimal quantity,decimal gross,decimal discount,decimal taxRate,decimal tax,decimal net)
            :this(medicine,quantity,gross,discount,taxRate,tax,net,string.Empty,0m,1){}
        public CommercialReturnDocumentLine(string medicine,decimal quantity,decimal gross,decimal discount,decimal taxRate,decimal tax,decimal net,string unitName,decimal unitPrice,int priceBaseQuantity)
        {
            if(unitPrice<0m||priceBaseQuantity<=0)throw new ArgumentOutOfRangeException(nameof(unitPrice),"Invalid saved return price basis.");
            MedicineName=medicine;Quantity=quantity;GrossTotal=gross;DiscountTotal=discount;TaxRate=taxRate;TaxTotal=tax;NetTotal=net;
            UnitName=unitName??string.Empty;UnitPrice=unitPrice;PriceBaseQuantity=priceBaseQuantity;
        }
    }
    public sealed class CommercialReturnDocumentSnapshot
    {
        public Guid ReturnId{get;private set;}public Guid OriginalInvoiceId{get;private set;}
        public long ReturnInvoiceNumber{get;private set;}public long OriginalInvoiceNumber{get;private set;}
        public DateTime IssuedAtUtc{get;private set;}public string RefundMethod{get;private set;}public string ReturnReason{get;private set;}
        public string IssuedBy{get;private set;}public string CurrencyCode{get;private set;}
        public IReadOnlyList<CommercialReturnDocumentLine> Lines{get;private set;}
        public CommercialReturnDocumentSnapshot(Guid returnId,Guid originalInvoiceId,long number,long originalNumber,DateTime issuedAtUtc,string refund,string reason,string user,string currency,IEnumerable<CommercialReturnDocumentLine> lines)
        {
            if(returnId==Guid.Empty||originalInvoiceId==Guid.Empty||number<=0||originalNumber<=0)throw new ArgumentException("Issued document identity is required.");
            var copy=(lines??throw new ArgumentNullException(nameof(lines))).ToArray();if(copy.Length==0||copy.Any(line=>line==null))throw new ArgumentException("Issued document lines are required.");
            ReturnId=returnId;OriginalInvoiceId=originalInvoiceId;ReturnInvoiceNumber=number;OriginalInvoiceNumber=originalNumber;IssuedAtUtc=issuedAtUtc;
            RefundMethod=refund;ReturnReason=reason;IssuedBy=user;CurrencyCode=currency;Lines=Array.AsReadOnly(copy);
        }
    }
    public interface ICommercialReturnReadRepository
    {
        Task<OperationalReportResult> LoadHistoryAsync(CommercialReturnHistoryReadRequest request,CancellationToken cancellationToken);
        Task<CommercialReturnDocumentSnapshot> LoadDocumentAsync(CommercialReturnDocumentReadRequest request,CancellationToken cancellationToken);
    }
    public interface ICommercialReturnReadService
    {
        Task<OperationalReportResult> LoadHistoryAsync(CommercialReturnHistoryReadRequest request,OperationActor actor,CancellationToken cancellationToken);
        Task<CommercialReturnDocumentSnapshot> LoadDocumentAsync(CommercialReturnDocumentReadRequest request,OperationActor actor,CancellationToken cancellationToken);
    }
    public sealed class CommercialReturnReadService : ICommercialReturnReadService
    {
        private readonly ICommercialReturnReadRepository repository;private readonly Func<OperationActor,bool> allowed;
        public CommercialReturnReadService(ICommercialReturnReadRepository repository,Func<OperationActor,bool> allowed)
        {this.repository=repository??throw new ArgumentNullException(nameof(repository));this.allowed=allowed??throw new ArgumentNullException(nameof(allowed));}
        private OperationActor Capture(OperationActor actor)=>new OperationActor{UserName=actor?.UserName,UserRole=actor?.UserRole};
        private void Authorize(OperationActor actor){if(string.IsNullOrWhiteSpace(actor.UserName)||string.IsNullOrWhiteSpace(actor.UserRole)||!allowed(actor))throw new UnauthorizedAccessException("تغيرت جلسة المستخدم؛ أعد تحميل التقرير.");}
        public async Task<OperationalReportResult> LoadHistoryAsync(CommercialReturnHistoryReadRequest request,OperationActor actor,CancellationToken cancellationToken)
        {if(request==null)throw new ArgumentNullException(nameof(request));cancellationToken.ThrowIfCancellationRequested();var captured=Capture(actor);Authorize(captured);var result=await repository.LoadHistoryAsync(request,cancellationToken).ConfigureAwait(false);cancellationToken.ThrowIfCancellationRequested();Authorize(captured);return result;}
        public async Task<CommercialReturnDocumentSnapshot> LoadDocumentAsync(CommercialReturnDocumentReadRequest request,OperationActor actor,CancellationToken cancellationToken)
        {
            if(request==null)throw new ArgumentNullException(nameof(request));cancellationToken.ThrowIfCancellationRequested();var captured=Capture(actor);Authorize(captured);
            var result=await repository.LoadDocumentAsync(request,cancellationToken).ConfigureAwait(false);cancellationToken.ThrowIfCancellationRequested();Authorize(captured);
            if(result==null||result.ReturnId!=request.ReturnId||result.OriginalInvoiceId!=request.OriginalInvoiceId)throw new InvalidOperationException("إشعار المرتجع غير موجود أو لا يطابق الفاتورة الأصلية.");
            return result;
        }
    }
}

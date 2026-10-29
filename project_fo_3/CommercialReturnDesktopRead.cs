using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Application;
namespace project_fo_3
{
    internal sealed class CommercialReturnReadSession
    {
        private readonly ICommercialReturnReadService service;private readonly OperationActor actor;private readonly Func<bool> current;
        internal CommercialReturnReadSession(ICommercialReturnReadService service,OperationActor actor,Func<bool> current)
        {this.service=service??throw new ArgumentNullException(nameof(service));this.actor=new OperationActor{UserName=actor?.UserName,UserRole=actor?.UserRole};this.current=current??throw new ArgumentNullException(nameof(current));}
        internal bool IsCurrent=>current();
        internal void EnsureContext(){if(!IsCurrent)throw new UnauthorizedAccessException("تغيرت جلسة المستخدم؛ أعد تحميل التقرير.");}
        internal async Task<OperationalReportResult> LoadHistoryAsync(CommercialReturnHistoryReadRequest request,CancellationToken token)
        {token.ThrowIfCancellationRequested();EnsureContext();var result=await service.LoadHistoryAsync(request,new OperationActor{UserName=actor.UserName,UserRole=actor.UserRole},token).ConfigureAwait(false);token.ThrowIfCancellationRequested();EnsureContext();return result;}
        internal async Task<CommercialReturnDocumentSnapshot> LoadDocumentAsync(CommercialReturnDocumentReadRequest request,CancellationToken token)
        {token.ThrowIfCancellationRequested();EnsureContext();var result=await service.LoadDocumentAsync(request,new OperationActor{UserName=actor.UserName,UserRole=actor.UserRole},token).ConfigureAwait(false);token.ThrowIfCancellationRequested();EnsureContext();return result;}
    }
    internal static class CommercialReturnDesktopPresentation
    {
        internal static CommercialReturnPrintDocument ToPrintDocument(CommercialReturnDocumentSnapshot snapshot)
        {
            if(snapshot==null)throw new ArgumentNullException(nameof(snapshot));
            return new CommercialReturnPrintDocument{
                Header=new CommercialReturnPrintHeader{ReturnInvoiceNumber=snapshot.ReturnInvoiceNumber,OriginalInvoiceNumber=snapshot.OriginalInvoiceNumber,IssuedAtUtc=snapshot.IssuedAtUtc,RefundMethod=snapshot.RefundMethod,ReturnReason=snapshot.ReturnReason,IssuedBy=snapshot.IssuedBy,CurrencyCode=snapshot.CurrencyCode},
                Lines=snapshot.Lines.Select(line=>new CommercialReturnPrintLine{MedicineName=line.MedicineName,Quantity=line.Quantity,GrossTotal=line.GrossTotal,DiscountTotal=line.DiscountTotal,TaxRate=line.TaxRate,TaxTotal=line.TaxTotal,NetTotal=line.NetTotal,Unit=line.UnitName,UnitPrice=line.UnitPrice,PriceBaseQuantity=line.PriceBaseQuantity}).ToList()
            };
        }
    }
}

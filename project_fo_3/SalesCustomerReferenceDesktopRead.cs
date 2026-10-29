using System;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Application;
namespace project_fo_3
{
    internal sealed class SalesCustomerReferenceReadSession
    {
        private readonly ICustomerReferenceReadService service;
        private readonly ISalesCustomerCreditReadService credit;
        private readonly string user,role;
        private readonly Func<bool> current;
        private int revoked;
        internal SalesCustomerReferenceReadSession(ICustomerReferenceReadService service,OperationActor actor,Func<bool> current)
            :this(service,null,actor,current){}
        internal SalesCustomerReferenceReadSession(ICustomerReferenceReadService service,ISalesCustomerCreditReadService credit,OperationActor actor,Func<bool> current)
        {this.service=service??throw new ArgumentNullException(nameof(service));this.credit=credit;user=actor?.UserName??string.Empty;role=actor?.UserRole??string.Empty;this.current=current??throw new ArgumentNullException(nameof(current));}
        internal bool IsCurrent
        {
            get
            {
                if(Volatile.Read(ref revoked)!=0)return false;
                if(!current()){Interlocked.Exchange(ref revoked,1);return false;}
                return Volatile.Read(ref revoked)==0;
            }
        }
        internal async Task<SalesCustomerCreditSnapshot> ReadCreditAsync(SalesCustomerCreditReadRequest request,CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if(!IsCurrent)throw new UnauthorizedAccessException("تغير سياق قراءة رصيد العميل. أعد تحميل بيانات العملاء.");
            if(credit==null)throw new InvalidOperationException("خدمة قراءة رصيد العميل غير متاحة في الجلسة.");
            var result=await credit.ReadAsync(request,new OperationActor{UserName=user,UserRole=role},token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            if(!IsCurrent)throw new UnauthorizedAccessException("تغير سياق قراءة رصيد العميل. أعد تحميل بيانات العملاء.");
            return result;
        }
        internal async Task<CustomerReferenceReadResult> ReadAsync(CustomerReferenceReadRequest request,CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if(!IsCurrent)throw new UnauthorizedAccessException("تغير سياق قراءة العميل. أعد تحميل بيانات العملاء.");
            var result=await service.ReadAsync(request,new OperationActor{UserName=user,UserRole=role},token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            if(!IsCurrent)throw new UnauthorizedAccessException("تغير سياق قراءة العميل. أعد تحميل بيانات العملاء.");
            return result;
        }
    }
}
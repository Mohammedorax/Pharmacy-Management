using System;
using System.Threading;
using System.Threading.Tasks;
namespace PharmacyManagement.Application
{
    public sealed class SalesCustomerCreditReadRequest
    {
        public string CustomerId { get; }
        public SalesCustomerCreditReadRequest(string customerId)
        {
            CustomerId=(customerId??string.Empty).Trim();
            if(CustomerId.Length==0||CustomerId.Length>50)throw new ArgumentException("رقم العميل مطلوب وبحد أقصى 50 حرفًا.");
        }
    }
    public sealed class SalesCustomerCreditSnapshot
    {
        public string CustomerId { get; }
        public decimal Balance { get; }
        public SalesCustomerCreditSnapshot(string customerId,decimal balance)
        {CustomerId=customerId??throw new ArgumentNullException(nameof(customerId));Balance=balance;}
    }
    public interface ISalesCustomerCreditReadRepository
    {Task<SalesCustomerCreditSnapshot> ReadAsync(SalesCustomerCreditReadRequest request,CancellationToken token);}
    public interface ISalesCustomerCreditReadService
    {Task<SalesCustomerCreditSnapshot> ReadAsync(SalesCustomerCreditReadRequest request,OperationActor actor,CancellationToken token);}
    public sealed class SalesCustomerCreditReadService:ISalesCustomerCreditReadService
    {
        private readonly ISalesCustomerCreditReadRepository repository;
        private readonly Func<OperationActor,bool> canRead;
        public SalesCustomerCreditReadService(ISalesCustomerCreditReadRepository repository,Func<OperationActor,bool> canRead)
        {this.repository=repository??throw new ArgumentNullException(nameof(repository));this.canRead=canRead??throw new ArgumentNullException(nameof(canRead));}
        private void Authorize(OperationActor actor)
        {if(actor.UserName.Length==0||actor.UserRole.Length==0||!canRead(actor))throw new UnauthorizedAccessException("لا تملك صلاحية قراءة رصيد العميل.");}
        public Task<SalesCustomerCreditSnapshot> ReadAsync(SalesCustomerCreditReadRequest request,OperationActor actor,CancellationToken token)
        {
            var current=new OperationActor{UserName=(actor?.UserName??string.Empty).Trim(),UserRole=(actor?.UserRole??string.Empty).Trim()};
            Authorize(current);token.ThrowIfCancellationRequested();
            if(request==null)throw new ArgumentNullException(nameof(request));
            return ReadAuthorizedAsync(request,current,token);
        }
        private async Task<SalesCustomerCreditSnapshot> ReadAuthorizedAsync(SalesCustomerCreditReadRequest request,OperationActor actor,CancellationToken token)
        {
            var result=await repository.ReadAsync(request,token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();Authorize(actor);
            if(result==null||!string.Equals(result.CustomerId,request.CustomerId,StringComparison.Ordinal))throw new InvalidOperationException("لم يرجع مصدر الرصيد نتيجة مطابقة للعميل.");
            return new SalesCustomerCreditSnapshot(result.CustomerId,result.Balance);
        }
    }
}
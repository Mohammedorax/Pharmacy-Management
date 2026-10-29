using System;
using System.Threading;
using System.Threading.Tasks;

namespace PharmacyManagement.Application
{
    // Values are copied from the currently selected issuer profile, not historical invoice identity.
    public sealed class InvoiceIssuerProfile
    {
        public string NameAr { get; }
        public string NameEn { get; }
        public string CommercialRegistration { get; }
        public string Mobile { get; }
        public string Phone { get; }
        public string TaxNumber { get; }
        public string Address { get; }
        public string Website { get; }
        public string LogoPath { get; }
        public InvoiceIssuerProfile(string nameAr,string nameEn,string registration,string mobile,string phone,
            string taxNumber,string address,string website,string logoPath)
        {
            NameAr=nameAr??string.Empty;NameEn=nameEn??string.Empty;CommercialRegistration=registration??string.Empty;
            Mobile=mobile??string.Empty;Phone=phone??string.Empty;TaxNumber=taxNumber??string.Empty;
            Address=address??string.Empty;Website=website??string.Empty;LogoPath=logoPath??string.Empty;
        }
    }
    public interface IInvoiceIssuerReadRepository
    {
        Task<InvoiceIssuerProfile> LoadAsync(CancellationToken cancellationToken);
    }
    public sealed class InvoiceIssuerReadService
    {
        private readonly IInvoiceIssuerReadRepository repository;
        private readonly Func<OperationActor,bool> authorized;
        public InvoiceIssuerReadService(IInvoiceIssuerReadRepository repository,Func<OperationActor,bool> authorized)
        {
            this.repository=repository??throw new ArgumentNullException(nameof(repository));
            this.authorized=authorized??throw new ArgumentNullException(nameof(authorized));
        }
        public async Task<InvoiceIssuerProfile> LoadAsync(OperationActor actor,CancellationToken cancellationToken)
        {
            Require(actor);
            var captured=new OperationActor{UserName=actor.UserName,UserRole=actor.UserRole};
            cancellationToken.ThrowIfCancellationRequested();
            var result=await repository.LoadAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();Require(captured);
            return result;
        }
        private void Require(OperationActor actor)
        {
            if(actor==null||string.IsNullOrWhiteSpace(actor.UserName)||string.IsNullOrWhiteSpace(actor.UserRole)||!authorized(actor))
                throw new UnauthorizedAccessException("لا تملك صلاحية قراءة بيانات المنشأة لطباعة الفاتورة.");
        }
    }
}

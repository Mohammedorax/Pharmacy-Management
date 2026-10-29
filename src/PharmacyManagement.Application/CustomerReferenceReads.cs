using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PharmacyManagement.Application
{
    public enum CustomerReferenceKind { Names, Phones, ById, ByName, ByPhone }
    public sealed class CustomerReferenceReadRequest
    {
        public CustomerReferenceKind Kind { get; }
        public string Value { get; }
        public bool IsLookup => Kind == CustomerReferenceKind.ById || Kind == CustomerReferenceKind.ByName || Kind == CustomerReferenceKind.ByPhone;
        public CustomerReferenceReadRequest(CustomerReferenceKind kind, string value = null)
        {
            if (!Enum.IsDefined(typeof(CustomerReferenceKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            Kind = kind; Value = (value ?? string.Empty).Trim();
            if (!IsLookup && Value.Length != 0) throw new ArgumentException("طلب قائمة العملاء لا يقبل قيمة بحث.");
            int maximum = kind == CustomerReferenceKind.ByName ? 200 : 50;
            if (Value.Length > maximum) throw new ArgumentException("قيمة بحث العميل تتجاوز الحد المسموح.");
        }
    }
    public sealed class CustomerReferenceSnapshot
    {
        public string CustomerId { get; }
        public string CustomerName { get; }
        public string PhoneNumber { get; }
        public string Address { get; }
        public decimal TotalSpent { get; }
        public CustomerReferenceSnapshot(string id, string name, string phone, string address, decimal total)
        { CustomerId=id;CustomerName=name;PhoneNumber=phone;Address=address;TotalSpent=total; }
    }
    public sealed class CustomerReferenceReadResult
    {
        public CustomerReferenceKind Kind { get; }
        public IReadOnlyList<string> Values { get; }
        public CustomerReferenceSnapshot Customer { get; }
        public CustomerReferenceReadResult(CustomerReferenceKind kind, IEnumerable<string> values, CustomerReferenceSnapshot customer)
        {
            if (!Enum.IsDefined(typeof(CustomerReferenceKind),kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            Kind=kind;Values=new List<string>(values ?? throw new ArgumentNullException(nameof(values))).AsReadOnly();Customer=customer;
            bool lookup=kind==CustomerReferenceKind.ById||kind==CustomerReferenceKind.ByName||kind==CustomerReferenceKind.ByPhone;
            if (lookup && Values.Count != 0 || !lookup && customer != null) throw new ArgumentException("نتيجة قراءة العميل لا تطابق نوع الطلب.");
        }
    }
    public interface ICustomerReferenceReadRepository
    { Task<CustomerReferenceReadResult> ReadAsync(CustomerReferenceReadRequest request, CancellationToken token); }
    public interface ICustomerReferenceReadService
    { Task<CustomerReferenceReadResult> ReadAsync(CustomerReferenceReadRequest request, OperationActor actor, CancellationToken token); }
    public sealed class CustomerReferenceReadService : ICustomerReferenceReadService
    {
        private readonly ICustomerReferenceReadRepository repository;
        private readonly Func<OperationActor,bool> canRead;
        public CustomerReferenceReadService(ICustomerReferenceReadRepository repository, Func<OperationActor,bool> canRead)
        {this.repository=repository??throw new ArgumentNullException(nameof(repository));this.canRead=canRead??throw new ArgumentNullException(nameof(canRead));}
        private void Authorize(OperationActor actor)
        {if(actor.UserName.Length==0||actor.UserRole.Length==0||!canRead(actor))throw new UnauthorizedAccessException("لا تملك صلاحية قراءة بيانات العملاء.");}
        public Task<CustomerReferenceReadResult> ReadAsync(CustomerReferenceReadRequest request,OperationActor actor,CancellationToken token)
        {
            var current=new OperationActor{UserName=(actor?.UserName??string.Empty).Trim(),UserRole=(actor?.UserRole??string.Empty).Trim()};
            Authorize(current);token.ThrowIfCancellationRequested();
            if(request==null)throw new ArgumentNullException(nameof(request));
            if(request.IsLookup&&request.Value.Length==0)return Task.FromResult(new CustomerReferenceReadResult(request.Kind,new string[0],null));
            return ReadAuthorizedAsync(request,current,token);
        }
        private async Task<CustomerReferenceReadResult> ReadAuthorizedAsync(CustomerReferenceReadRequest request,OperationActor actor,CancellationToken token)
        {
            var result=await repository.ReadAsync(request,token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();Authorize(actor);
            if(result==null||result.Kind!=request.Kind)throw new InvalidOperationException("لم يرجع مصدر العملاء نتيجة مطابقة للطلب.");
            return new CustomerReferenceReadResult(result.Kind,result.Values,result.Customer);
        }
    }
}
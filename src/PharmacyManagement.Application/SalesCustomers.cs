using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PharmacyManagement.Application
{
    public sealed class SalesCustomer
    {
        public string CustomerId { get; set; }
        public string CustomerName { get; set; }
        public string PhoneNumber { get; set; }
        public string Address { get; set; }
        public decimal TotalSpent { get; set; }
    }

    public interface ISalesCustomerRepository
    {
        IReadOnlyList<SalesCustomer> GetAll();
        Task<IReadOnlyList<SalesCustomer>> GetAllAsync(CancellationToken cancellationToken);
        IReadOnlyList<string> GetCustomerNames();
        IReadOnlyList<string> GetPhoneNumbers();
        SalesCustomer FindById(string customerId);
        SalesCustomer FindByName(string customerName);
        SalesCustomer FindByPhone(string phoneNumber);
        string Create(SalesCustomer customer, OperationActor actor);
        bool Update(SalesCustomer customer, OperationActor actor);
    }

    public interface ISalesCustomerService
    {
        Task<IReadOnlyList<SalesCustomer>> GetAllAsync(OperationActor actor, CancellationToken cancellationToken);
        string Create(SalesCustomer customer, OperationActor actor);
        bool Update(SalesCustomer customer, OperationActor actor);
    }

    public sealed class SalesCustomerService : ISalesCustomerService
    {
        private readonly ISalesCustomerRepository repository;
        private readonly Func<OperationActor, bool> canManageCustomers;
        private readonly Func<OperationActor, bool> canReadCustomers;

        public SalesCustomerService(ISalesCustomerRepository repository, Func<OperationActor, bool> canManageCustomers, Func<OperationActor, bool> canReadCustomers = null)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
            this.canManageCustomers = canManageCustomers ?? throw new ArgumentNullException(nameof(canManageCustomers));
            this.canReadCustomers = canReadCustomers ?? canManageCustomers;
        }

        public Task<IReadOnlyList<SalesCustomer>> GetAllAsync(OperationActor actor, CancellationToken cancellationToken)
        {
            var current = new OperationActor { UserName = (actor?.UserName ?? string.Empty).Trim(),
                UserRole = (actor?.UserRole ?? string.Empty).Trim() };
            if (current.UserName.Length == 0 || current.UserRole.Length == 0 || !canReadCustomers(current))
                throw new UnauthorizedAccessException("لا تملك صلاحية قراءة قائمة العملاء.");
            cancellationToken.ThrowIfCancellationRequested();
            return ReadAuthorizedCustomersAsync(current, cancellationToken);
        }
        private async Task<IReadOnlyList<SalesCustomer>> ReadAuthorizedCustomersAsync(OperationActor actor, CancellationToken cancellationToken)
        {
            var rows = await repository.GetAllAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (!canReadCustomers(actor)) throw new UnauthorizedAccessException("تغيرت صلاحية قراءة العملاء أثناء الطلب.");
            if (rows == null) throw new InvalidOperationException("لم يرجع مصدر العملاء نتيجة.");
            return new List<SalesCustomer>(rows).AsReadOnly();
        }




        public string Create(SalesCustomer customer, OperationActor actor)
        {
            SalesCustomer normalized = NormalizeForSave(customer, requireId: false);
            OperationActor currentActor = NormalizeActor(actor);
            EnsureCanManage(currentActor);
            return repository.Create(normalized, currentActor);
        }

        public bool Update(SalesCustomer customer, OperationActor actor)
        {
            SalesCustomer normalized = NormalizeForSave(customer, requireId: true);
            OperationActor currentActor = NormalizeActor(actor);
            EnsureCanManage(currentActor);
            return repository.Update(normalized, currentActor);
        }

        private void EnsureCanManage(OperationActor actor)
        {
            if (!canManageCustomers(actor))
                throw new UnauthorizedAccessException("لا تملك صلاحية إدارة بيانات العملاء.");
        }

        private static SalesCustomer NormalizeForSave(SalesCustomer value, bool requireId)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            string id = Normalize(value.CustomerId, 50, "رقم العميل");
            string name = Normalize(value.CustomerName, 200, "اسم العميل");
            string phone = Normalize(value.PhoneNumber, 50, "رقم الهاتف");
            string address = Normalize(value.Address, 300, "العنوان");
            if (requireId && id.Length == 0) throw new InvalidOperationException("رقم العميل مطلوب للتعديل.");
            if (name.Length == 0) throw new InvalidOperationException("اسم العميل مطلوب.");
            if (phone.Length == 0) throw new InvalidOperationException("رقم الهاتف مطلوب.");
            if (string.Equals(phone, "000000000", StringComparison.Ordinal)) throw new InvalidOperationException("هذا الرقم محجوز لسجل العميل العام.");
            if (string.Equals(id, "0", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("لا يمكن تعديل سجل العميل العام.");
            return new SalesCustomer { CustomerId = id, CustomerName = name, PhoneNumber = phone, Address = address };
        }

        private static OperationActor NormalizeActor(OperationActor actor)
        {
            return new OperationActor
            {
                UserName = Normalize(string.IsNullOrWhiteSpace(actor?.UserName) ? "Unknown" : actor.UserName, 200, "اسم المستخدم"),
                UserRole = Normalize(string.IsNullOrWhiteSpace(actor?.UserRole) ? "Unknown" : actor.UserRole, 50, "الدور")
            };
        }

        private static string Normalize(string value, int maximumLength, string fieldName)
        {
            string normalized = (value ?? string.Empty).Trim();
            if (normalized.Length > maximumLength)
                throw new InvalidOperationException(fieldName + " يتجاوز الحد المسموح (" + maximumLength + ").");
            return normalized;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PharmacyManagement.Application
{
    public sealed class ManufacturerRecord
    {
        public string CompId { get; set; }
        public string CompName { get; set; }
        public string CompPhone { get; set; }
        public string CompAddress { get; set; }
    }

    public sealed class OperationActor
    {
        public string UserName { get; set; }
        public string UserRole { get; set; }
    }

    public interface IManufacturerRepository
    {
        IReadOnlyList<ManufacturerRecord> GetAll();
        Task<IReadOnlyList<ManufacturerRecord>> GetAllAsync(CancellationToken cancellationToken);
        void Create(ManufacturerRecord manufacturer, OperationActor actor);
        bool Update(ManufacturerRecord manufacturer, OperationActor actor);
        bool Delete(string companyId, OperationActor actor);
    }

    public interface IManufacturerService
    {

        Task<IReadOnlyList<ManufacturerRecord>> GetAllAsync(OperationActor actor, CancellationToken cancellationToken);
        void Create(ManufacturerRecord manufacturer, OperationActor actor);
        bool Update(ManufacturerRecord manufacturer, OperationActor actor);
        bool Delete(string companyId, OperationActor actor);
    }

    public sealed class ManufacturerService : IManufacturerService
    {
        private readonly IManufacturerRepository repository;

        private readonly Func<OperationActor, bool> canManage;
        private readonly Func<OperationActor, bool> canRead;

        public ManufacturerService(IManufacturerRepository repository, Func<OperationActor, bool> canManage, Func<OperationActor, bool> canRead = null)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
            this.canManage = canManage ?? throw new ArgumentNullException(nameof(canManage));
            this.canRead = canRead ?? canManage;
        }


        public Task<IReadOnlyList<ManufacturerRecord>> GetAllAsync(OperationActor actor, CancellationToken cancellationToken)
        {
            OperationActor current = RequireActor(actor, canRead);
            cancellationToken.ThrowIfCancellationRequested();
            return ReadAuthorizedManufacturersAsync(current, cancellationToken);
        }

        private async Task<IReadOnlyList<ManufacturerRecord>> ReadAuthorizedManufacturersAsync(
            OperationActor actor, CancellationToken cancellationToken)
        {
            var rows = await repository.GetAllAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (!canRead(actor))
                throw new UnauthorizedAccessException("تغيرت صلاحية قراءة الموردين أثناء الطلب.");
            if (rows == null)
                throw new InvalidOperationException("لم يرجع مصدر الموردين نتيجة.");
            return new List<ManufacturerRecord>(rows).AsReadOnly();
        }

        public void Create(ManufacturerRecord manufacturer, OperationActor actor)
        {
            OperationActor current = RequireActor(actor);
            repository.Create(Normalize(manufacturer), current);
        }

        public bool Update(ManufacturerRecord manufacturer, OperationActor actor)
        {
            OperationActor current = RequireActor(actor);
            return repository.Update(Normalize(manufacturer), current);
        }

        public bool Delete(string companyId, OperationActor actor)
        {
            OperationActor current = RequireActor(actor);
            string normalizedId = Required(companyId, 50, "رقم الشركة");
            return repository.Delete(normalizedId, current);
        }

        private static ManufacturerRecord Normalize(ManufacturerRecord value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            return new ManufacturerRecord
            {
                CompId = Required(value.CompId, 50, "رقم الشركة"),
                CompName = Required(value.CompName, 200, "اسم الشركة"),
                CompPhone = Required(value.CompPhone, 50, "رقم الهاتف"),
                CompAddress = Required(value.CompAddress, 300, "العنوان")
            };
        }

        private OperationActor RequireActor(OperationActor actor, Func<OperationActor, bool> authorize = null)
        {
            var current = new OperationActor
            {
                UserName = (actor?.UserName ?? string.Empty).Trim(),
                UserRole = (actor?.UserRole ?? string.Empty).Trim()
            };
            if (current.UserName.Length == 0 || current.UserRole.Length == 0 || !(authorize ?? canManage)(current))
                throw new UnauthorizedAccessException("لا تملك صلاحية إدارة بيانات الموردين.");
            return current;
        }

        private static string Required(string value, int maximumLength, string fieldName)
        {
            string normalized = (value ?? string.Empty).Trim();
            if (normalized.Length == 0) throw new InvalidOperationException(fieldName + " مطلوب.");
            if (normalized.Length > maximumLength)
                throw new InvalidOperationException(fieldName + " يتجاوز الحد المسموح (" + maximumLength + ").");
            return normalized;
        }
    }
}

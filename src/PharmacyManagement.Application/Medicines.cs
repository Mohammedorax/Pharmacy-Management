using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PharmacyManagement.Application
{
    public sealed class MedicineRecord
    {
        public string MedName { get; set; }
        public string Barcode { get; set; }
        public int BuyingPrice { get; set; }
        public int SellingPrice { get; set; }
        public int Quantity { get; set; }
        public DateTime ProductionDate { get; set; }
        public DateTime ExpiryDate { get; set; }
        public string CompanyName { get; set; }
    }

    public interface IMedicineRepository
    {
        IReadOnlyList<MedicineRecord> GetAll();
        Task<IReadOnlyList<MedicineRecord>> GetAllAsync(CancellationToken cancellationToken);
        Task<IReadOnlyList<string>> GetNamesAsync(CancellationToken cancellationToken);
        Task<MedicineRecord> FindByNameAsync(string medicineName, CancellationToken cancellationToken);
        IReadOnlyList<string> GetCompanyNames();
        Task<IReadOnlyList<string>> GetCompanyNamesAsync(CancellationToken cancellationToken);
        bool IsBarcodeAvailable(string barcode, string excludedMedicineName);
        Task<bool> IsBarcodeAvailableAsync(string barcode, string excludedMedicineName, CancellationToken cancellationToken);
        void Create(MedicineRecord medicine, OperationActor actor);
        Task CreateAsync(MedicineRecord medicine, OperationActor actor);
        bool Update(MedicineRecord medicine, OperationActor actor);
        Task<bool> UpdateAsync(MedicineRecord medicine, OperationActor actor);
        bool Delete(string medicineName, OperationActor actor);
        Task<bool> DeleteAsync(string medicineName, OperationActor actor);
    }

    public interface IMedicineService
    {
        IReadOnlyList<MedicineRecord> GetAll(OperationActor actor);
        Task<IReadOnlyList<MedicineRecord>> GetAllAsync(OperationActor actor, CancellationToken cancellationToken);
        Task<IReadOnlyList<string>> GetNamesAsync(OperationActor actor, CancellationToken cancellationToken);
        Task<MedicineRecord> FindByNameAsync(string medicineName, OperationActor actor, CancellationToken cancellationToken);
        IReadOnlyList<string> GetCompanyNames(OperationActor actor);
        Task<IReadOnlyList<string>> GetCompanyNamesAsync(OperationActor actor, CancellationToken cancellationToken);
        string GenerateAvailableBarcode(string excludedMedicineName, OperationActor actor);
        Task<string> GenerateAvailableBarcodeAsync(string excludedMedicineName, OperationActor actor, CancellationToken cancellationToken);
        MedicineRecord Create(MedicineRecord medicine, OperationActor actor);
        Task<MedicineRecord> CreateAsync(MedicineRecord medicine, OperationActor actor);
        bool Update(MedicineRecord medicine, OperationActor actor);
        Task<bool> UpdateAsync(MedicineRecord medicine, OperationActor actor);
        bool Delete(string medicineName, OperationActor actor);
        Task<bool> DeleteAsync(string medicineName, OperationActor actor);
    }

    public sealed class MedicineService : IMedicineService
    {
        private readonly IMedicineRepository repository;
        private readonly Func<string> barcodeCandidateFactory;

        private readonly Func<OperationActor, bool> canManage;
        private readonly Func<OperationActor, bool> canRead;

        public MedicineService(IMedicineRepository repository, Func<OperationActor, bool> canManage,
            Func<OperationActor, bool> canRead = null)
            : this(repository, canManage, CreateBarcodeCandidate, canRead)
        {
        }

        public MedicineService(IMedicineRepository repository, Func<OperationActor, bool> canManage,
            Func<string> barcodeCandidateFactory, Func<OperationActor, bool> canRead = null)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
            this.canManage = canManage ?? throw new ArgumentNullException(nameof(canManage));
            this.canRead = canRead ?? canManage;
            this.barcodeCandidateFactory = barcodeCandidateFactory ?? throw new ArgumentNullException(nameof(barcodeCandidateFactory));
        }

        public IReadOnlyList<MedicineRecord> GetAll(OperationActor actor)
        {
            OperationActor current = RequireActor(actor, false);
            var rows = repository.GetAll();
            RequireActor(current, false);
            if (rows == null) throw new InvalidOperationException("لم يرجع مصدر الأدوية نتيجة.");
            var copy = new List<MedicineRecord>();
            foreach (var row in rows)
            {
                if (row == null) throw new InvalidOperationException("نتيجة الأدوية تحتوي سجلًا مفقودًا.");
                copy.Add(new MedicineRecord {
                    MedName=row.MedName, Barcode=row.Barcode, BuyingPrice=row.BuyingPrice,
                    SellingPrice=row.SellingPrice, Quantity=row.Quantity, ProductionDate=row.ProductionDate,
                    ExpiryDate=row.ExpiryDate, CompanyName=row.CompanyName });
            }
            RequireActor(current, false);
            return copy.AsReadOnly();
        }

        public IReadOnlyList<string> GetCompanyNames(OperationActor actor)
        {
            OperationActor current = RequireActor(actor);
            var rows = repository.GetCompanyNames();
            RequireActor(current);
            if (rows == null) throw new InvalidOperationException("لم يرجع مصدر الشركات نتيجة.");
            var copy = new List<string>(rows).AsReadOnly();
            RequireActor(current);
            return copy;
        }

        public Task<IReadOnlyList<MedicineRecord>> GetAllAsync(OperationActor actor, CancellationToken cancellationToken)
        {
            OperationActor current = RequireActor(actor, false);
            cancellationToken.ThrowIfCancellationRequested();
            return ReadAuthorizedListAsync(repository.GetAllAsync(cancellationToken), current, false, cancellationToken);
        }

        public Task<IReadOnlyList<string>> GetNamesAsync(OperationActor actor, CancellationToken cancellationToken)
        {
            OperationActor current = RequireActor(actor, false);
            cancellationToken.ThrowIfCancellationRequested();
            return ReadAuthorizedListAsync(repository.GetNamesAsync(cancellationToken), current, false, cancellationToken);
        }

        public Task<MedicineRecord> FindByNameAsync(string medicineName, OperationActor actor, CancellationToken cancellationToken)
        {
            OperationActor current = RequireActor(actor, false);
            cancellationToken.ThrowIfCancellationRequested();
            // A read key comes from the persisted selector: preserve legacy spaces instead of normalizing its identity.
            if (string.IsNullOrWhiteSpace(medicineName)) throw new InvalidOperationException("اسم الدواء مطلوب.");
            if (medicineName.Length > 200) throw new InvalidOperationException("اسم الدواء يتجاوز الحد المسموح (200).");
            return ReadAuthorizedMedicineAsync(repository.FindByNameAsync(medicineName, cancellationToken), current, cancellationToken);
        }

        public Task<IReadOnlyList<string>> GetCompanyNamesAsync(OperationActor actor, CancellationToken cancellationToken)
        {
            OperationActor current = RequireActor(actor);
            cancellationToken.ThrowIfCancellationRequested();
            return ReadAuthorizedListAsync(repository.GetCompanyNamesAsync(cancellationToken), current, true, cancellationToken);
        }

        private async Task<IReadOnlyList<T>> ReadAuthorizedListAsync<T>(Task<IReadOnlyList<T>> pending,
            OperationActor actor, bool manage, CancellationToken cancellationToken)
        {
            var rows = await pending.ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            RequireActor(actor, manage);
            if (rows == null) throw new InvalidOperationException("لم يرجع مصدر الأدوية نتيجة.");
            return new List<T>(rows).AsReadOnly();
        }

        private async Task<MedicineRecord> ReadAuthorizedMedicineAsync(Task<MedicineRecord> pending,
            OperationActor actor, CancellationToken cancellationToken)
        {
            var row = await pending.ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            RequireActor(actor, false);
            // A null targeted result is a legitimate not-found outcome.
            return row;
        }

        public string GenerateAvailableBarcode(string excludedMedicineName, OperationActor actor)
        {
            OperationActor current = RequireActor(actor);
            return GenerateAvailableBarcodeCore(excludedMedicineName, current);
        }

        public async Task<string> GenerateAvailableBarcodeAsync(string excludedMedicineName, OperationActor actor, CancellationToken cancellationToken)
        {
            OperationActor current = RequireActor(actor);
            cancellationToken.ThrowIfCancellationRequested();
            string excluded = Optional(excludedMedicineName, 200, "اسم الدواء");
            for (int attempt = 0; attempt < 20; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                RequireActor(current);
                string candidate = Optional(barcodeCandidateFactory(), 100, "الباركود");
                if (candidate.Length == 0) continue;
                bool available = await repository.IsBarcodeAvailableAsync(candidate, excluded, cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                RequireActor(current);
                if (available) return candidate;
            }
            throw new InvalidOperationException("تعذر توليد باركود فريد. أعد المحاولة أو أدخل الباركود يدويًا.");
        }

        private string GenerateAvailableBarcodeCore(string excludedMedicineName, OperationActor actor = null)
        {
            string excluded = Optional(excludedMedicineName, 200, "اسم الدواء");
            for (int attempt = 0; attempt < 20; attempt++)
            {
                if (actor != null) RequireActor(actor);
                string candidate = Optional(barcodeCandidateFactory(), 100, "الباركود");
                if (actor != null) RequireActor(actor);
                if (candidate.Length == 0) continue;
                bool available = repository.IsBarcodeAvailable(candidate, excluded);
                if (actor != null) RequireActor(actor);
                if (available) return candidate;
            }

            throw new InvalidOperationException("تعذر توليد باركود فريد. أعد المحاولة أو أدخل الباركود يدويًا.");
        }

        public MedicineRecord Create(MedicineRecord medicine, OperationActor actor)
        {
            OperationActor current = RequireActor(actor);
            MedicineRecord normalized = Normalize(medicine, generateBarcodeWhenMissing: true, actor: current);
            RequireActor(current);
            repository.Create(normalized, current);
            return normalized;
        }

        public bool Update(MedicineRecord medicine, OperationActor actor)
        {
            OperationActor current = RequireActor(actor);
            MedicineRecord normalized = Normalize(medicine, generateBarcodeWhenMissing: false);
            return repository.Update(normalized, current);
        }

        public bool Delete(string medicineName, OperationActor actor)
        {
            OperationActor current = RequireActor(actor);
            return repository.Delete(Required(medicineName, 200, "اسم الدواء"), current);
        }

        public async Task<MedicineRecord> CreateAsync(MedicineRecord medicine, OperationActor actor)
        {
            OperationActor current = RequireActor(actor);
            MedicineRecord normalized = Normalize(medicine, generateBarcodeWhenMissing: false);
            if (normalized.Barcode.Length == 0)
                normalized.Barcode = await GenerateAvailableBarcodeAsync(normalized.MedName, current, CancellationToken.None).ConfigureAwait(false);
            RequireActor(current);
            await repository.CreateAsync(normalized, current).ConfigureAwait(false);
            return normalized;
        }

        public Task<bool> UpdateAsync(MedicineRecord medicine, OperationActor actor)
        {
            OperationActor current = RequireActor(actor);
            MedicineRecord normalized = Normalize(medicine, generateBarcodeWhenMissing: false);
            return repository.UpdateAsync(normalized, current);
        }

        public Task<bool> DeleteAsync(string medicineName, OperationActor actor)
        {
            OperationActor current = RequireActor(actor);
            return repository.DeleteAsync(Required(medicineName, 200, "اسم الدواء"), current);
        }

        private MedicineRecord Normalize(MedicineRecord medicine, bool generateBarcodeWhenMissing, OperationActor actor = null)
        {
            if (medicine == null) throw new ArgumentNullException(nameof(medicine));
            string name = Required(medicine.MedName, 200, "اسم الدواء");
            string barcode = Optional(medicine.Barcode, 100, "الباركود");
            if (generateBarcodeWhenMissing && barcode.Length == 0) barcode = GenerateAvailableBarcodeCore(name, actor);
            if (medicine.BuyingPrice <= 0) throw new InvalidOperationException("سعر الشراء يجب أن يكون أكبر من صفر.");
            if (medicine.SellingPrice <= 0) throw new InvalidOperationException("سعر البيع يجب أن يكون أكبر من صفر.");
            if (medicine.Quantity < 0) throw new InvalidOperationException("الكمية لا يمكن أن تكون سالبة.");
            if (medicine.ProductionDate.Date > medicine.ExpiryDate.Date)
                throw new InvalidOperationException("لا يمكن أن يكون تاريخ الإنتاج بعد تاريخ الانتهاء.");

            return new MedicineRecord
            {
                MedName = name,
                Barcode = barcode,
                BuyingPrice = medicine.BuyingPrice,
                SellingPrice = medicine.SellingPrice,
                Quantity = medicine.Quantity,
                ProductionDate = medicine.ProductionDate,
                ExpiryDate = medicine.ExpiryDate,
                CompanyName = Required(medicine.CompanyName, 200, "الشركة المصنعة")
            };
        }

        private OperationActor RequireActor(OperationActor actor, bool manage = true)
        {
            var current = new OperationActor
            {
                UserName = (actor?.UserName ?? string.Empty).Trim(),
                UserRole = (actor?.UserRole ?? string.Empty).Trim()
            };
            if (current.UserName.Length == 0 || current.UserRole.Length == 0 || !(manage ? canManage(current) : canRead(current)))
                throw new UnauthorizedAccessException("لا تملك صلاحية الوصول إلى بيانات الأدوية.");
            return current;
        }

        private static string Required(string value, int maximumLength, string fieldName)
        {
            string normalized = Optional(value, maximumLength, fieldName);
            if (normalized.Length == 0) throw new InvalidOperationException(fieldName + " مطلوب.");
            return normalized;
        }

        private static string Optional(string value, int maximumLength, string fieldName)
        {
            string normalized = (value ?? string.Empty).Trim();
            if (normalized.Length > maximumLength)
                throw new InvalidOperationException(fieldName + " يتجاوز الحد المسموح (" + maximumLength + ").");
            return normalized;
        }

        private static string CreateBarcodeCandidate()
        {
            string digits = DateTime.UtcNow.Ticks.ToString();
            return digits.Length <= 13 ? digits.PadLeft(13, '0') : digits.Substring(digits.Length - 13, 13);
        }
    }
}



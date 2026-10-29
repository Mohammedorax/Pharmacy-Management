using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PharmacyManagement.Application
{
    public sealed class MedicineBarcodeLengthException : InvalidOperationException
    {
        public MedicineBarcodeLengthException() : base("الباركود أطول من الحد المدعوم (100 حرف). راجع الباركود المدخل.") { }
    }

    public sealed class MedicineBarcodeAmbiguityException : InvalidOperationException
    {
        public const string UserMessage = "هذا الباركود مرتبط بأكثر من صنف. صحح الباركود من إدارة الأدوية أو اختر الصنف بالاسم؛ لم يتم اختيار أي صنف تلقائياً.";
        public MedicineBarcodeAmbiguityException() : base(UserMessage) { }
    }

    public sealed class MedicineMatch
    {
        public string MedicineName { get; }
        public string CompanyName { get; }
        public MedicineMatch(string medicineName, string companyName)
        {
            MedicineName = medicineName;
            CompanyName = companyName;
        }
    }

    public sealed class MedicineSearchItem
    {
        public string MedName { get; set; }
        public string Barcode { get; set; }
        public string CompName { get; set; }
        public decimal? MedQty { get; set; }
        public decimal? Sprice { get; set; }
        public DateTime? ExpDate { get; set; }
        public decimal? NetSold { get; set; }
    }

    public interface IMedicineSearchRepository
    {
        Task<IReadOnlyList<MedicineSearchItem>> SearchAsync(string query, CancellationToken cancellationToken);
        MedicineMatch FindByBarcode(string barcode);
        Task<MedicineMatch> FindByBarcodeAsync(string barcode, CancellationToken cancellationToken);
    }

    public interface IMedicineSearchService
    {
        Task<IReadOnlyList<MedicineSearchItem>> SearchAsync(string query, OperationActor actor, CancellationToken cancellationToken);
        MedicineMatch FindByBarcode(string barcode, OperationActor actor);
        Task<MedicineMatch> FindByBarcodeAsync(string barcode, OperationActor actor, CancellationToken cancellationToken);
    }

    public sealed class MedicineSearchService : IMedicineSearchService
    {
        private readonly IMedicineSearchRepository repository;
        private readonly Func<OperationActor, bool> canReadBarcode;
        private readonly Func<OperationActor, bool> canSearch;
        public MedicineSearchService(IMedicineSearchRepository repository, Func<OperationActor, bool> canReadBarcode = null, Func<OperationActor, bool> canSearch = null)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
            // Both asynchronous entry points deny by default; sync barcode remains a legacy consumer.
            this.canReadBarcode = canReadBarcode ?? (_ => false);
            this.canSearch = canSearch ?? (_ => false);
        }

        public async Task<MedicineMatch> FindByBarcodeAsync(string barcode, OperationActor actor, CancellationToken cancellationToken)
        {
            RequireBarcodeActor(actor);
            var current = new OperationActor { UserName = actor.UserName, UserRole = actor.UserRole };
            cancellationToken.ThrowIfCancellationRequested();
            string value = (barcode ?? string.Empty).Trim();
            if (value.Length == 0) return null;
            if (value.Length > 100)
                throw new MedicineBarcodeLengthException();
            MedicineMatch match;
            try { match = await repository.FindByBarcodeAsync(value, cancellationToken).ConfigureAwait(false); }
            catch { cancellationToken.ThrowIfCancellationRequested(); throw; }
            cancellationToken.ThrowIfCancellationRequested();
            RequireBarcodeActor(current);
            return match;
        }

        private void RequireBarcodeActor(OperationActor actor)
        {
            if (actor == null || string.IsNullOrWhiteSpace(actor.UserName) || string.IsNullOrWhiteSpace(actor.UserRole) || !canReadBarcode(actor))
                throw new UnauthorizedAccessException("لا تملك صلاحية الوصول إلى بيانات الأدوية.");
        }

        public async Task<IReadOnlyList<MedicineSearchItem>> SearchAsync(string query, OperationActor actor, CancellationToken cancellationToken)
        {
            RequireSearchActor(actor);
            var current = new OperationActor { UserName = actor.UserName, UserRole = actor.UserRole };
            cancellationToken.ThrowIfCancellationRequested();
            string value = (query ?? string.Empty).Trim();
            if (value.Length > 200) throw new InvalidOperationException("عبارة البحث أطول من 200 حرف.");
            var result = await repository.SearchAsync(value, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            RequireSearchActor(current);
            return result;
        }

        private void RequireSearchActor(OperationActor actor)
        {
            if (actor == null || string.IsNullOrWhiteSpace(actor.UserName) || string.IsNullOrWhiteSpace(actor.UserRole) || !canSearch(actor))
                throw new UnauthorizedAccessException("لا تملك صلاحية الوصول إلى بيانات الأدوية.");
        }

        public MedicineMatch FindByBarcode(string barcode, OperationActor actor)
        {
            RequireBarcodeActor(actor);
            var current = new OperationActor { UserName=actor.UserName, UserRole=actor.UserRole };
            if (string.IsNullOrWhiteSpace(barcode)) return null;
            string value = barcode.Trim();
            if (value.Length > 100)
                throw new MedicineBarcodeLengthException();
            MedicineMatch result=repository.FindByBarcode(value);
            RequireBarcodeActor(current);
            return result;
        }
    }
}



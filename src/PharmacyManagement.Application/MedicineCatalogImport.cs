using System;
using System.Collections.Generic;

namespace PharmacyManagement.Application
{
    public sealed class MedicineCatalogImportResult
    {
        public int Created { get; set; }
        public int SkippedExisting { get; set; }
    }

    public interface IMedicineCatalogImportRepository
    {
        MedicineCatalogImportResult ImportNew(IReadOnlyList<MedicineRecord> medicines, OperationActor actor);
    }

    public interface IMedicineCatalogImportService
    {
        MedicineCatalogImportResult ImportNew(IReadOnlyList<MedicineRecord> medicines, OperationActor actor);
    }

    // Catalog import never creates stock. Incoming quantities are rejected, and
    // purchase/opening-stock operations remain the only route for inventory value.
    public sealed class MedicineCatalogImportService : IMedicineCatalogImportService
    {
        private readonly IMedicineCatalogImportRepository repository;

        public MedicineCatalogImportService(IMedicineCatalogImportRepository repository)
        { this.repository = repository ?? throw new ArgumentNullException(nameof(repository)); }

        public MedicineCatalogImportResult ImportNew(IReadOnlyList<MedicineRecord> medicines, OperationActor actor)
        {
            string user = (actor?.UserName ?? string.Empty).Trim();
            string role = (actor?.UserRole ?? string.Empty).Trim();
            if (user.Length == 0 || user.Length > 200 || !IsManagerRole(role))
                throw new UnauthorizedAccessException("استيراد تعريفات الأصناف متاح للمدير فقط.");
            if (medicines == null || medicines.Count == 0 || medicines.Count > 2000)
                throw new InvalidOperationException("ملف الأصناف يجب أن يحتوي من 1 إلى 2,000 صف.");

            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var barcodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var normalized = new List<MedicineRecord>(medicines.Count);
            foreach (MedicineRecord item in medicines)
            {
                if (item == null) throw new InvalidOperationException("يوجد صف صنف فارغ.");
                var clean = new MedicineRecord
                {
                    MedName = Required(item.MedName, 200, "اسم الصنف"),
                    Barcode = Required(item.Barcode, 100, "الباركود"),
                    CompanyName = Required(item.CompanyName, 200, "الشركة المصنعة"),
                    BuyingPrice = item.BuyingPrice,
                    SellingPrice = item.SellingPrice,
                    Quantity = item.Quantity,
                    ProductionDate = item.ProductionDate.Date,
                    ExpiryDate = item.ExpiryDate.Date
                };
                if (clean.Quantity != 0)
                    throw new InvalidOperationException("استيراد تعريف الصنف لا ينشئ رصيدًا؛ أدخل الكميات عبر شراء أو رصيد افتتاحي موثق.");
                if (clean.BuyingPrice <= 0 || clean.SellingPrice <= 0)
                    throw new InvalidOperationException("أسعار الصنف يجب أن تكون موجبة.");
                if (clean.ProductionDate.Year < 1900 || clean.ExpiryDate <= clean.ProductionDate)
                    throw new InvalidOperationException("تاريخا إنتاج وانتهاء الصنف غير صالحين.");
                if (!names.Add(clean.MedName) || !barcodes.Add(clean.Barcode))
                    throw new InvalidOperationException("يوجد اسم صنف أو باركود مكرر داخل ملف الاستيراد.");
                normalized.Add(clean);
            }
            return repository.ImportNew(normalized, new OperationActor { UserName = user, UserRole = role });
        }

        private static string Required(string value, int maximum, string field)
        {
            string clean = (value ?? string.Empty).Trim();
            if (clean.Length == 0 || clean.Length > maximum)
                throw new InvalidOperationException(field + " مطلوب وبحد أقصى " + maximum + " حرف.");
            return clean;
        }

        private static bool IsManagerRole(string role)
        {
            return string.Equals(role, "Manager", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(role, "Administrator", StringComparison.OrdinalIgnoreCase) ||
                role == "مدير" || role == "مسؤول" || role == "ادمن";
        }
    }
}

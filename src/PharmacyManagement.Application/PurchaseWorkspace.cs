using System;
using System.Collections.Generic;

namespace PharmacyManagement.Application
{
    public sealed class PurchaseHistoryRecord
    {
        public string PurchaseId { get; set; }
        public string SupplierName { get; set; }
        public DateTime PurchaseDate { get; set; }
        public decimal TotalAmount { get; set; }
        public string EmployeeName { get; set; }
        public string Notes { get; set; }
        public bool IsCancelled { get; set; }
        public string SystemCurrencyCode { get; set; }
    }

    public sealed class PurchaseHistoryLine
    {
        public string MedicineName { get; set; }
        public decimal BuyingPrice { get; set; }
        public decimal Quantity { get; set; }
        public int BaseQuantity { get; set; }
        public string Unit { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime? ProductionDate { get; set; }
        public DateTime? ExpiryDate { get; set; }
    }

    public sealed class PurchaseMedicineSnapshot
    {
        public string MedicineName { get; set; }
        public int StockQuantity { get; set; }
        public int BuyingPrice { get; set; }
        public string SupplierName { get; set; }
    }

    public sealed class PurchaseEmployeeChoice
    {
        public string EmployeeId { get; set; }
        public string EmployeeName { get; set; }
    }

    public interface IPurchaseWorkspaceRepository
    {
        IReadOnlyList<PurchaseHistoryRecord> GetHistory();
        IReadOnlyList<PurchaseHistoryLine> GetHistoryLines(string purchaseId);
        IReadOnlyList<string> GetSupplierNames();
        IReadOnlyList<string> GetMedicineNames(string supplierName);
        IReadOnlyList<PurchaseEmployeeChoice> GetEmployees();
        PurchaseMedicineSnapshot FindMedicine(string medicineName);
    }

    public interface IPurchaseWorkspaceService
    {
        IReadOnlyList<PurchaseHistoryRecord> GetHistory();
        IReadOnlyList<PurchaseHistoryLine> GetHistoryLines(string purchaseId);
        IReadOnlyList<string> GetSupplierNames();
        IReadOnlyList<string> GetMedicineNames(string supplierName = null);
        IReadOnlyList<PurchaseEmployeeChoice> GetEmployees();
        PurchaseMedicineSnapshot FindMedicine(string medicineName);
    }

    public sealed class PurchaseWorkspaceService : IPurchaseWorkspaceService
    {
        private readonly IPurchaseWorkspaceRepository repository;

        public PurchaseWorkspaceService(IPurchaseWorkspaceRepository repository)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public IReadOnlyList<PurchaseHistoryRecord> GetHistory() => Complete(repository.GetHistory(), "سجل المشتريات");

        public IReadOnlyList<PurchaseHistoryLine> GetHistoryLines(string purchaseId) =>
            Complete(repository.GetHistoryLines(Required(purchaseId, 50, "رقم فاتورة المشتريات")), "بنود فاتورة المشتريات");

        public IReadOnlyList<string> GetSupplierNames() => Complete(repository.GetSupplierNames(), "قائمة الموردين");

        public IReadOnlyList<string> GetMedicineNames(string supplierName = null) =>
            Complete(repository.GetMedicineNames(Optional(supplierName, 200, "اسم المورد")), "قائمة الأدوية");

        public IReadOnlyList<PurchaseEmployeeChoice> GetEmployees() => Complete(repository.GetEmployees(), "قائمة الموظفين");

        public PurchaseMedicineSnapshot FindMedicine(string medicineName) =>
            repository.FindMedicine(Required(medicineName, 200, "اسم الدواء"));

        private static IReadOnlyList<T> Complete<T>(IReadOnlyList<T> value, string description)
        {
            if (value == null) throw new InvalidOperationException("تعذر تحميل " + description + " بصورة مكتملة.");
            return value;
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
    }
}

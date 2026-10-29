using PharmacyManagement.Domain;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PharmacyManagement.Application
{
    public sealed class PurchaseInvoiceLineRequest
    {
        public int LineNumber { get; set; }
        public string DesiredDetailId { get; set; }
        public string MedicineName { get; set; }
        public MedicineUnitSelection Unit { get; set; }
        public int BuyingPrice { get; set; }
        public int Quantity { get; set; }
        public DateTime ProductionDate { get; set; }
        public DateTime ExpiryDate { get; set; }
        public string Gtin { get; set; }
        public string BatchNumber { get; set; }
        public string SerialNumber { get; set; }
    }

    public sealed class PurchaseInvoiceRequest
    {
        public string ImportSourceSystem { get; set; }
        public string ImportSourceInvoiceId { get; set; }
        public Guid? ImportDraftId { get; set; }
        public string ImportCurrencyCode { get; set; }
        public string ExpectedSystemCurrencyCode { get; set; }
        public decimal? ImportTaxRatePercent { get; set; }
        public string ImportPaymentMethod { get; set; }
        public string DesiredPurchaseId { get; set; }
        public string SupplierName { get; set; }
        public string EmployeeName { get; set; }
        public string ActorRole { get; set; }
        public string Notes { get; set; }
        public DateTime PurchaseDate { get; set; }
        public bool CommercialInventoryExpected { get; set; }
        public IReadOnlyList<PurchaseInvoiceLineRequest> Lines { get; set; }
    }

    public sealed class PurchaseInvoiceLineResult
    {
        public int LineNumber { get; set; }
        public string DetailId { get; set; }
    }

    public sealed class PurchaseInvoiceResult
    {
        public string PurchaseId { get; set; }
        public decimal TotalAmount { get; set; }
        public IReadOnlyList<PurchaseInvoiceLineResult> Lines { get; set; }
    }

    public sealed class PurchaseInvoiceCancellationRequest
    {
        public string PurchaseId { get; set; }
        public string ActorName { get; set; }
        public string ActorRole { get; set; }
        public string Reason { get; set; }
    }

    public sealed class PurchaseInvoiceCancellationOutcome
    {
        public string PurchaseId { get; set; }
        public decimal TotalAmount { get; set; }
        public int RemovedQuantity { get; set; }
    }

    public interface IPurchaseInvoiceCancellationOperation
    {
        PurchaseInvoiceCancellationOutcome Cancel(PurchaseInvoiceCancellationRequest request);
    }

    public interface IPurchaseInvoiceOperation
    {
        PurchaseInvoiceResult Issue(PurchaseInvoiceRequest request);
    }

    public interface IPurchaseInventoryModeProvider
    {
        bool IsCommercialInventoryEnabled();
    }

    public static class PurchaseInvoiceValidation
    {
        public static PurchaseInvoiceRequest Normalize(PurchaseInvoiceRequest request, DateTime today)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (request.Lines == null || request.Lines.Count == 0)
                throw new InvalidOperationException("يجب إضافة صنف واحد على الأقل قبل حفظ المشتريات.");

            var lines = new List<PurchaseInvoiceLineRequest>(request.Lines.Count);
            var lineNumbers = new HashSet<int>();
            foreach (PurchaseInvoiceLineRequest line in request.Lines)
            {
                if (line == null) throw new InvalidOperationException("يوجد بند مشتريات غير مكتمل.");
                if (line.LineNumber <= 0 || !lineNumbers.Add(line.LineNumber))
                    throw new InvalidOperationException("ترتيب بنود المشتريات غير صالح أو مكرر.");
                if ((line.Unit == null && line.BuyingPrice <= 0) || line.Quantity <= 0 || (line.Unit != null && line.Unit.BaseQuantity != line.Quantity))
                    throw new InvalidOperationException("يوجد بند مشتريات بسعر أو كمية غير صحيحة.");
                if (line.Unit == null) checked { _ = line.BuyingPrice * line.Quantity; }
                if (line.ProductionDate.Date > today.Date)
                    throw new InvalidOperationException("تاريخ الإنتاج لا يمكن أن يكون في المستقبل.");
                if (line.ExpiryDate.Date <= line.ProductionDate.Date)
                    throw new InvalidOperationException("تاريخ الانتهاء يجب أن يكون بعد تاريخ الإنتاج.");
                string batch = Optional(line.BatchNumber, 100, "رقم التشغيلة");
                if (request.CommercialInventoryExpected && batch.Length == 0)
                    throw new InvalidOperationException("رقم التشغيلة مطلوب لكل بند عند تفعيل مخزون الدفعات.");

                lines.Add(new PurchaseInvoiceLineRequest
                {
                    LineNumber = line.LineNumber,
                    DesiredDetailId = Optional(line.DesiredDetailId, 50, "رقم بند المشتريات"),
                    MedicineName = Required(line.MedicineName, 200, "اسم الدواء"),
                    BuyingPrice = line.BuyingPrice,
                    Unit = line.Unit,
                    Quantity = line.Quantity,
                    ProductionDate = line.ProductionDate.Date,
                    ExpiryDate = line.ExpiryDate.Date,
                    Gtin = Optional(line.Gtin, 20, "GTIN"),
                    BatchNumber = batch,
                    SerialNumber = Optional(line.SerialNumber, 100, "الرقم التسلسلي")
                });
            }

            string expectedCurrency = (request.ExpectedSystemCurrencyCode ?? string.Empty).Trim().ToUpperInvariant();
            if (expectedCurrency.Length > 10 || expectedCurrency.Any(c => c < 'A' || c > 'Z'))
                throw new InvalidOperationException("عملة مسودة المشتريات غير صالحة.");
            string importedPayment = (request.ImportPaymentMethod ?? string.Empty).Trim();
            if (importedPayment.Length > 0 && importedPayment != "Cash" && importedPayment != "Payable")
                throw new InvalidOperationException("طريقة سداد المشتريات المستوردة غير مدعومة.");
            if (request.ImportTaxRatePercent.HasValue &&
                (request.ImportTaxRatePercent.Value < 0m || request.ImportTaxRatePercent.Value > 100m))
                throw new InvalidOperationException("نسبة ضريبة المشتريات المستوردة غير صالحة.");
            return new PurchaseInvoiceRequest
            {
                ImportSourceSystem = request.ImportSourceSystem,
                ImportSourceInvoiceId = request.ImportSourceInvoiceId,
                ImportDraftId = request.ImportDraftId,
                ImportCurrencyCode = request.ImportCurrencyCode,
                ExpectedSystemCurrencyCode = expectedCurrency.Length == 0 ? null : expectedCurrency,
                ImportTaxRatePercent = request.ImportTaxRatePercent,
                ImportPaymentMethod = importedPayment,
                DesiredPurchaseId = Optional(request.DesiredPurchaseId, 50, "رقم فاتورة المشتريات"),
                SupplierName = Required(request.SupplierName, 200, "المورد"),
                EmployeeName = Required(request.EmployeeName, 200, "الموظف"),
                ActorRole = Optional(request.ActorRole, 50, "الدور"),
                Notes = Optional(request.Notes, 1000, "الملاحظات"),
                PurchaseDate = request.PurchaseDate,
                CommercialInventoryExpected = request.CommercialInventoryExpected,
                Lines = lines.AsReadOnly()
            };
        }

        public static decimal CalculateTotal(IReadOnlyList<PurchaseInvoiceLineRequest> lines)
        {
            decimal total = 0m;
            foreach (PurchaseInvoiceLineRequest line in lines)
                total = checked(total + (line.Unit == null ? checked(line.BuyingPrice * line.Quantity) : line.Unit.GrossTotal));
            if (total <= 0m) throw new InvalidOperationException("إجمالي المشتريات يجب أن يكون أكبر من صفر.");
            return total;
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

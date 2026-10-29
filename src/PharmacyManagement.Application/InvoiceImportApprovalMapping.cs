using System;
using System.Collections.Generic;

namespace PharmacyManagement.Application
{
    public sealed class InvoiceImportMappingException : InvalidOperationException
    {
        public InvoiceImportMappingException(string arabicMessage, string englishMessage)
            : base(arabicMessage)
        { EnglishMessage = englishMessage; }

        public string EnglishMessage { get; private set; }
    }

    // Converts an explicitly approved import candidate to existing invoice operations.
    // Unsupported legacy SQL precision or payment methods fail before any posting.
    public static class InvoiceImportApprovalMapping
    {
        public static SalesInvoiceIssueRequest Sale(InvoiceImportCandidate candidate, OperationActor actor, Guid? draftId = null)
        {
            Require(candidate, actor, ImportedInvoiceKind.Sale);
            if (candidate.PartyReference.Length == 0 || candidate.PartyPhone.Length == 0)
                throw new InvoiceImportMappingException(
                    "اعتماد البيع المستورد يتطلب معرف عميل مسجلًا وهاتفه من شاشة العملاء.",
                    "The imported sale needs a registered customer ID and phone from the Customers screen.");
            if (candidate.PaymentMethod != "Cash")
                throw new InvoiceImportMappingException(
                    "مسار البيع المستورد الحالي يدعم السداد النقدي الصريح فقط؛ لا تُحوّل فواتير الآجل تلقائيًا.",
                    "Imported sales currently require an explicit Cash payment; credit sales are not converted automatically.");
            if (!candidate.InvoiceDiscountPercent.HasValue || !candidate.TaxRatePercent.HasValue || candidate.PriceTaxMode != "Inclusive")
                throw new InvoiceImportMappingException(
                    "اعتماد البيع يتطلب نسبة خصم وضريبة صريحتين وسعرًا شاملًا للضريبة.",
                    "The imported sale needs explicit discount and tax rates with tax-inclusive prices.");
            decimal gross = 0m;
            var lines = new List<SalesInvoiceLineRequest>();
            foreach (InvoiceImportRow row in candidate.Lines)
            {
                int quantity = Whole(row.Quantity, "كمية البيع", "Sale quantity");
                int price = Whole(row.UnitPrice, "سعر البيع", "Sale price");
                try { gross = checked(gross + checked(price * quantity)); }
                catch (OverflowException)
                {
                    throw new InvoiceImportMappingException(
                        "إجمالي البيع المستورد يتجاوز نطاق الحساب الحالي؛ راجع السعر والكمية.",
                        "The imported sale total exceeds the current calculation range; review price and quantity.");
                }
                lines.Add(new SalesInvoiceLineRequest { MedicineName = row.MedicineName,
                    Quantity = quantity, DisplayedSellingPrice = price });
            }
            decimal discount = candidate.InvoiceDiscountPercent.Value;
            decimal net = Round(gross - Round(gross * discount / 100m));
            if (net <= 0m) throw new InvoiceImportMappingException(
                "صافي البيع المستورد يجب أن يكون أكبر من صفر.",
                "The imported sale net total must be greater than zero.");
            return new SalesInvoiceIssueRequest {
                ImportSourceSystem = candidate.SourceSystem, ImportSourceInvoiceId = candidate.SourceInvoiceId,
                ImportDraftId = draftId, ImportCurrencyCode = candidate.CurrencyCode,
                ImportTaxRatePercent = candidate.TaxRatePercent, ImportPriceTaxMode = candidate.PriceTaxMode,
                CustomerId = candidate.PartyReference, CustomerName = candidate.PartyName,
                PhoneNumber = candidate.PartyPhone, EmployeeName = actor.UserName.Trim(),
                Notes = candidate.Notes, InvoiceDate = candidate.InvoiceDate,
                DiscountPercent = discount, DisplayedGrossTotal = gross, DisplayedNetTotal = net,
                Lines = lines.AsReadOnly()
            };
        }

        public static PurchaseInvoiceRequest Purchase(InvoiceImportCandidate candidate, OperationActor actor,
            bool commercialInventory, Guid? draftId = null)
        {
            Require(candidate, actor, ImportedInvoiceKind.Purchase);
            if (candidate.PaymentMethod != "Payable" && candidate.PaymentMethod != "Cash")
                throw new InvoiceImportMappingException(
                    "اعتماد المشتريات يتطلب طريقة سداد صريحة: Cash أو Payable.",
                    "The imported purchase needs an explicit Cash or Payable payment method.");
            if (candidate.InvoiceDiscountPercent != 0m || candidate.TaxRatePercent != 0m ||
                (candidate.PriceTaxMode != "Inclusive" && candidate.PriceTaxMode != ""))
                throw new InvoiceImportMappingException(
                    "المشتريات المستوردة ذات الخصم أو الضريبة تحتاج مسارًا محاسبيًا موسعًا قبل الاعتماد.",
                    "Imported purchases with a discount or tax need an expanded accounting path before approval.");
            var lines = new List<PurchaseInvoiceLineRequest>();
            foreach (InvoiceImportRow row in candidate.Lines)
            {
                if (!row.ProductionDate.HasValue || !row.ExpiryDate.HasValue ||
                    (commercialInventory && row.BatchNumber.Length == 0))
                    throw new InvoiceImportMappingException(
                        "بند المشتريات المستورد يحتاج تاريخ إنتاج وانتهاء، ورقم تشغيلة عند تفعيل الدفعات.",
                        "The imported purchase line needs production and expiry dates, and a batch number when lot tracking is enabled.");
                lines.Add(new PurchaseInvoiceLineRequest {
                    LineNumber = row.LineNumber, MedicineName = row.MedicineName,
                    Quantity = Whole(row.Quantity, "كمية المشتريات", "Purchase quantity"),
                    BuyingPrice = Whole(row.UnitPrice, "سعر الشراء", "Purchase price"),
                    ProductionDate = row.ProductionDate.Value.Date, ExpiryDate = row.ExpiryDate.Value.Date,
                    BatchNumber = row.BatchNumber, Gtin = row.Gtin, SerialNumber = row.SerialNumber
                });
            }
            return new PurchaseInvoiceRequest {
                ImportSourceSystem = candidate.SourceSystem, ImportSourceInvoiceId = candidate.SourceInvoiceId,
                ImportDraftId = draftId, ImportCurrencyCode = candidate.CurrencyCode,
                ImportTaxRatePercent = candidate.TaxRatePercent, ImportPaymentMethod = candidate.PaymentMethod,
                SupplierName = candidate.PartyName, EmployeeName = actor.UserName.Trim(), ActorRole = actor.UserRole.Trim(),
                Notes = candidate.Notes, PurchaseDate = candidate.InvoiceDate,
                CommercialInventoryExpected = commercialInventory, Lines = lines.AsReadOnly()
            };
        }

        private static void Require(InvoiceImportCandidate candidate, OperationActor actor, ImportedInvoiceKind kind)
        {
            if (candidate == null || candidate.Mode != InvoiceImportMode.ImportAndApprove || candidate.Kind != kind)
                throw new InvoiceImportMappingException(
                    "اعتماد الفاتورة يتطلب مرشحًا من النوع الصحيح بعد مراجعة صريحة.",
                    "Approval requires an explicitly reviewed invoice candidate of the correct kind.");
            if (actor == null || string.IsNullOrWhiteSpace(actor.UserName) || string.IsNullOrWhiteSpace(actor.UserRole))
                throw new UnauthorizedAccessException("هوية المستخدم الذي يعتمد الاستيراد غير صالحة.");
        }
        private static int Whole(decimal value, string field, string englishField)
        {
            if (value <= 0m || value > int.MaxValue || decimal.Truncate(value) != value)
                throw new InvoiceImportMappingException(
                    field + " لا تمثلها قاعدة الفواتير الحالية دون تقريب؛ راجع السطر قبل الاعتماد.",
                    englishField + " cannot be represented by the current invoice database without rounding; review the line before approval.");
            return decimal.ToInt32(value);
        }
        private static decimal Round(decimal value)
        { return decimal.Round(value, 2, MidpointRounding.AwayFromZero); }
    }
}

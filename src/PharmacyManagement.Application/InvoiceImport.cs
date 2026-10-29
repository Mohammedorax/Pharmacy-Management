using System;
using System.Collections.Generic;
using System.Linq;

namespace PharmacyManagement.Application
{
    public enum InvoiceImportMode { Draft = 1, ImportAndApprove = 2 }
    public enum ImportedInvoiceKind { Sale = 1, Purchase = 2 }

    // Staging data only: neither mode is a posted invoice or an inventory movement.
    public sealed class InvoiceImportRow
    {
        public string SourceInvoiceId { get; set; }
        public int LineNumber { get; set; }
        public ImportedInvoiceKind Kind { get; set; }
        public DateTime InvoiceDate { get; set; }
        public string PartyName { get; set; }
        public string CurrencyCode { get; set; }
        public string MedicineName { get; set; }
        public string Barcode { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public string PartyReference { get; set; }
        public string PartyPhone { get; set; }
        public decimal? InvoiceDiscountPercent { get; set; }
        public decimal? TaxRatePercent { get; set; }
        public string PriceTaxMode { get; set; }
        public string Notes { get; set; }
        public DateTime? ProductionDate { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public string BatchNumber { get; set; }
        public string Gtin { get; set; }
        public string SerialNumber { get; set; }
        public string PaymentMethod { get; set; }
    }

    public sealed class InvoiceImportCandidate
    {
        internal InvoiceImportCandidate(InvoiceImportMode mode, string sourceSystem, InvoiceImportRow[] rows)
        {
            Mode = mode;
            SourceSystem = sourceSystem;
            SourceInvoiceId = rows[0].SourceInvoiceId;
            Kind = rows[0].Kind;
            InvoiceDate = rows[0].InvoiceDate;
            PartyName = rows[0].PartyName;
            CurrencyCode = rows[0].CurrencyCode;
            PartyReference = rows[0].PartyReference;
            PartyPhone = rows[0].PartyPhone;
            InvoiceDiscountPercent = rows[0].InvoiceDiscountPercent;
            TaxRatePercent = rows[0].TaxRatePercent;
            PriceTaxMode = rows[0].PriceTaxMode;
            Notes = rows[0].Notes;
            PaymentMethod = rows[0].PaymentMethod;
            // Return copies so caller edits cannot alter a validated candidate.
            this.rows = rows;
        }
        private readonly InvoiceImportRow[] rows;
        public InvoiceImportMode Mode { get; private set; }
        public string SourceSystem { get; private set; }
        public string SourceInvoiceId { get; private set; }
        public ImportedInvoiceKind Kind { get; private set; }
        public DateTime InvoiceDate { get; private set; }
        public string PartyName { get; private set; }
        public string CurrencyCode { get; private set; }
        public string PartyReference { get; private set; }
        public string PartyPhone { get; private set; }
        public decimal? InvoiceDiscountPercent { get; private set; }
        public decimal? TaxRatePercent { get; private set; }
        public string PriceTaxMode { get; private set; }
        public string Notes { get; private set; }
        public string PaymentMethod { get; private set; }
        public IReadOnlyList<InvoiceImportRow> Lines { get { return Array.AsReadOnly(rows.Select(InvoiceImportValidation.Copy).ToArray()); } }
    }

    public static class InvoiceImportSourceIdentity
    {
        public static bool TryNormalize(string sourceSystem, string sourceInvoiceId, out string system, out string invoice)
        {
            system = (sourceSystem ?? string.Empty).Trim();
            invoice = (sourceInvoiceId ?? string.Empty).Trim();
            if (system.Length == 0 && invoice.Length == 0) return false;
            if (system.Length == 0 || invoice.Length == 0 || system.Length > 100 || invoice.Length > 100)
                throw new InvalidOperationException("هوية مصدر الفاتورة المستوردة يجب أن تتضمن اسم النظام ورقم الفاتورة بطول لا يتجاوز 100 حرف لكل منهما.");
            return true;
        }
    }
    public static class InvoiceImportValidation
    {
        public const int MaximumRows = 5000;

        public static IReadOnlyList<InvoiceImportCandidate> Validate(InvoiceImportMode mode, string sourceSystem,
            IEnumerable<InvoiceImportRow> input)
        {
            if (!Enum.IsDefined(typeof(InvoiceImportMode), mode)) throw new ArgumentException("نوع استيراد الفواتير غير صالح.");
            string system = Required(sourceSystem, 100, "اسم النظام المصدر");
            if (input == null) throw new ArgumentNullException(nameof(input));
            var rows = new List<InvoiceImportRow>();
            foreach (InvoiceImportRow item in input)
            {
                if (rows.Count == MaximumRows) throw new InvalidOperationException("الحد الأقصى 5,000 سطر فاتورة.");
                if (item == null) throw new InvalidOperationException("سطر فاتورة مفقود.");
                var row = Copy(item);
                row.SourceInvoiceId = Required(row.SourceInvoiceId, 100, "رقم الفاتورة المصدر");
                row.PartyName = Required(row.PartyName, 200, "اسم العميل أو المورد");
                row.MedicineName = Required(row.MedicineName, 200, "اسم الصنف");
                row.CurrencyCode = Required(row.CurrencyCode, 3, "رمز العملة").ToUpperInvariant();
                if (row.CurrencyCode.Length != 3 || row.CurrencyCode.Any(c => c < 'A' || c > 'Z'))
                    throw new InvalidOperationException("رمز العملة يجب أن يتكون من ثلاثة أحرف إنجليزية.");
                row.Barcode = (row.Barcode ?? string.Empty).Trim();
                if (row.Barcode.Length > 100) throw new InvalidOperationException("باركود الصنف أطول من الحد المسموح.");
                row.PartyReference = Optional(row.PartyReference, 50, "معرّف العميل أو المورد");
                row.PartyPhone = Optional(row.PartyPhone, 50, "هاتف العميل أو المورد");
                row.PriceTaxMode = Optional(row.PriceTaxMode, 12, "نوع احتساب الضريبة");
                row.Notes = Optional(row.Notes, 1000, "ملاحظات الفاتورة");
                row.PaymentMethod = Optional(row.PaymentMethod, 20, "طريقة السداد");
                if (row.PaymentMethod.Length > 0 && row.PaymentMethod != "Cash" && row.PaymentMethod != "Payable")
                    throw new InvalidOperationException("طريقة السداد يجب أن تكون Cash للبيع النقدي أو Payable للشراء الآجل.");
                row.BatchNumber = Optional(row.BatchNumber, 100, "رقم التشغيلة");
                row.Gtin = Optional(row.Gtin, 20, "GTIN");
                row.SerialNumber = Optional(row.SerialNumber, 100, "الرقم التسلسلي");
                if (row.InvoiceDiscountPercent.HasValue &&
                    (row.InvoiceDiscountPercent.Value < 0m || row.InvoiceDiscountPercent.Value >= 100m))
                    throw new InvalidOperationException("خصم الفاتورة يجب أن يكون من صفر إلى أقل من 100%.");
                if (row.TaxRatePercent.HasValue && (row.TaxRatePercent.Value < 0m || row.TaxRatePercent.Value > 100m))
                    throw new InvalidOperationException("نسبة الضريبة يجب أن تكون من صفر إلى 100%.");
                if (row.PriceTaxMode.Length > 0 && row.PriceTaxMode != "Inclusive" && row.PriceTaxMode != "Exclusive")
                    throw new InvalidOperationException("نوع احتساب الضريبة يجب أن يكون Inclusive أو Exclusive.");
                if (row.ProductionDate.HasValue != row.ExpiryDate.HasValue ||
                    (row.ProductionDate.HasValue && row.ExpiryDate.Value.Date <= row.ProductionDate.Value.Date))
                    throw new InvalidOperationException("أدخل تاريخي الإنتاج والانتهاء معًا، ويجب أن يكون الانتهاء بعد الإنتاج.");
                if (!Enum.IsDefined(typeof(ImportedInvoiceKind), row.Kind) || row.LineNumber <= 0 ||
                    row.InvoiceDate < new DateTime(1753, 1, 1) || row.Quantity <= 0 || row.UnitPrice < 0)
                    throw new InvalidOperationException("نوع أو تاريخ أو كمية أو سعر سطر الفاتورة غير صالح.");
                rows.Add(row);
            }
            if (rows.Count == 0) throw new InvalidOperationException("ملف الفواتير لا يحتوي أسطر بيانات.");
            var result = new List<InvoiceImportCandidate>();
            // Source IDs are unique per invoice kind without case distinction. Keep all
            // lines of one source invoice together before the user can approve it.
            // Sale and purchase documents may legitimately share a source number.
            foreach (var kindGroup in rows.GroupBy(row => row.Kind))
            foreach (var group in kindGroup.GroupBy(row => row.SourceInvoiceId, StringComparer.OrdinalIgnoreCase))
            {
                InvoiceImportRow header = group.First();
                var numbers = new HashSet<int>();
                foreach (InvoiceImportRow row in group)
                {
                    if (!numbers.Add(row.LineNumber)) throw new InvalidOperationException("رقم سطر مكرر في الفاتورة: " + header.SourceInvoiceId);
                    if (row.InvoiceDate != header.InvoiceDate || row.PartyName != header.PartyName || row.CurrencyCode != header.CurrencyCode ||
                        row.PartyReference != header.PartyReference || row.PartyPhone != header.PartyPhone ||
                        row.InvoiceDiscountPercent != header.InvoiceDiscountPercent || row.TaxRatePercent != header.TaxRatePercent ||
                        row.PriceTaxMode != header.PriceTaxMode || row.Notes != header.Notes ||
                        row.PaymentMethod != header.PaymentMethod)
                        throw new InvalidOperationException("بيانات رأس الفاتورة غير متطابقة بين أسطرها: " + header.SourceInvoiceId);
                }
                result.Add(new InvoiceImportCandidate(mode, system, group.OrderBy(row => row.LineNumber).ToArray()));
            }
            return result.AsReadOnly();
        }

        internal static InvoiceImportRow Copy(InvoiceImportRow row)
        {
            return new InvoiceImportRow { SourceInvoiceId = row.SourceInvoiceId, LineNumber = row.LineNumber,
                Kind = row.Kind, InvoiceDate = row.InvoiceDate, PartyName = row.PartyName, CurrencyCode = row.CurrencyCode,
                MedicineName = row.MedicineName, Barcode = row.Barcode, Quantity = row.Quantity, UnitPrice = row.UnitPrice,
                PartyReference = row.PartyReference, PartyPhone = row.PartyPhone,
                InvoiceDiscountPercent = row.InvoiceDiscountPercent, TaxRatePercent = row.TaxRatePercent,
                PriceTaxMode = row.PriceTaxMode, Notes = row.Notes, PaymentMethod = row.PaymentMethod,
                ProductionDate = row.ProductionDate, ExpiryDate = row.ExpiryDate,
                BatchNumber = row.BatchNumber, Gtin = row.Gtin, SerialNumber = row.SerialNumber };
        }
        private static string Optional(string value, int length, string field)
        {
            string clean = (value ?? string.Empty).Trim();
            if (clean.Length > length) throw new InvalidOperationException(field + " يتجاوز الطول المسموح.");
            return clean;
        }
        private static string Required(string value, int length, string field)
        {
            string clean = (value ?? string.Empty).Trim();
            if (clean.Length == 0 || clean.Length > length) throw new InvalidOperationException(field + " مطلوب أو يتجاوز الطول المسموح.");
            return clean;
        }
    }
}

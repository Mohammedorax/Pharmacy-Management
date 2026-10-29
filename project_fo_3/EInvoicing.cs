using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace project_fo_3
{
    internal enum EInvoiceDocumentKind { SimplifiedB2C, TaxB2B, CreditNote, DebitNote }
    internal enum EInvoiceTransmissionStatus { Pending, Submitted, Rejected, Failed }

    internal sealed class EInvoiceRequest
    {
        public Guid InvoiceId { get; set; }
        public long InvoiceNumber { get; set; }
        public EInvoiceDocumentKind Kind { get; set; }
        public string SellerName { get; set; }
        public string SellerVatNumber { get; set; }
        public string BuyerName { get; set; }
        public string BuyerVatNumber { get; set; }
        public Guid? OriginalInvoiceId { get; set; }
        public string AdjustmentReason { get; set; }
        public DateTime IssueDateUtc { get; set; }
        public string CurrencyCode { get; set; }
        public decimal InvoiceDiscountPercent { get; set; }
        public InvoiceTotals Totals { get; set; }
        public IReadOnlyList<CommercialInvoiceLine> Lines { get; set; }
        // تستخدم فقط لإشعار دائن مبني على لقطة سطر معتمد. يمنع ذلك أن يغير
        // إعادة التقريب في المزود المحلي الباقي النهائي للمرتجع الجزئي.
        public IReadOnlyList<EInvoiceLineSnapshot> ExactLines { get; set; }

        public EInvoiceRequest()
        {
            SellerName = string.Empty;
            SellerVatNumber = string.Empty;
            BuyerName = string.Empty;
            BuyerVatNumber = string.Empty;
            AdjustmentReason = string.Empty;
            CurrencyCode = "SAR";
            Lines = new List<CommercialInvoiceLine>();
            ExactLines = new List<EInvoiceLineSnapshot>();
        }
    }

    internal sealed class EInvoiceLineSnapshot
    {
        public string MedicineName { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal GrossAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TaxableAmount { get; set; }
        public decimal TaxRatePercent { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal LineTotal { get; set; }
        public CommercialPriceTaxMode PriceTaxMode { get; set; }
    }

    internal sealed class EInvoiceArtifact
    {
        public string Uuid { get; set; }
        public long Counter { get; set; }
        public string PreviousHash { get; set; }
        public string DocumentHash { get; set; }
        public string Xml { get; set; }
        public string QrTlvBase64 { get; set; }
        public EInvoiceTransmissionStatus Status { get; set; }
    }

    internal interface IEInvoiceProvider
    {
        EInvoiceArtifact Create(EInvoiceRequest request, long previousCounter, string previousHash, long nextCounter, string declaredPreviousHash);
    }

    internal static class EInvoiceChainValidator
    {
        public static void ValidateNext(long previousCounter, string previousHash, long nextCounter, string declaredPreviousHash)
        {
            if (nextCounter != previousCounter + 1)
                throw new InvalidOperationException("عداد الفاتورة الإلكترونية يجب أن يتقدم بمقدار واحد ولا يجوز إرجاعه.");
            if (!string.Equals(previousHash ?? string.Empty, declaredPreviousHash ?? string.Empty, StringComparison.Ordinal))
                throw new InvalidOperationException("سلسلة تجزئة الفواتير الإلكترونية غير متصلة.");
        }
    }

    // هذا مزود محاكاة محلي فقط. لا يوقّع XML ولا يتصل بـ ZATCA ولا يصح استخدامه كإثبات امتثال.
    internal sealed class LocalZatcaSandboxProvider : IEInvoiceProvider
    {
        public EInvoiceArtifact Create(EInvoiceRequest request, long previousCounter, string previousHash, long nextCounter, string declaredPreviousHash)
        {
            EInvoiceChainValidator.ValidateNext(previousCounter, previousHash, nextCounter, declaredPreviousHash);
            ValidateRequest(request, nextCounter);
            string uuid = request.InvoiceId == Guid.Empty ? Guid.NewGuid().ToString("D") : request.InvoiceId.ToString("D");
            string canonical = BuildCanonicalPayload(request, uuid, nextCounter, declaredPreviousHash);
            string documentHash = Sha256Base64(canonical);
            string qr = ZatcaTlvQrEncoder.Encode(request.SellerName, request.SellerVatNumber, request.IssueDateUtc, request.Totals.NetTotal, request.Totals.TaxTotal);
            IReadOnlyList<EInvoiceLineSnapshot> lineSnapshots = GetLineSnapshots(request);
            XDocument xml = new XDocument(
                new XElement("CommercialInvoice",
                    new XAttribute("profile", "LOCAL-SANDBOX-NOT-SIGNED"),
                    new XElement("UUID", uuid),
                    new XElement("InvoiceNumber", request.InvoiceNumber),
                    new XElement("Kind", request.Kind.ToString()),
                    new XElement("IssueDateUtc", request.IssueDateUtc.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture)),
                    new XElement("Currency", request.CurrencyCode),
                    new XElement("Seller", new XElement("Name", request.SellerName), new XElement("VatNumber", request.SellerVatNumber)),
                    new XElement("Buyer", new XElement("Name", request.BuyerName), new XElement("VatNumber", request.BuyerVatNumber)),
                    new XElement("OriginalInvoiceId", request.OriginalInvoiceId.HasValue ? request.OriginalInvoiceId.Value.ToString("D") : string.Empty),
                    new XElement("AdjustmentReason", request.AdjustmentReason ?? string.Empty),
                    new XElement("PreviousHash", declaredPreviousHash ?? string.Empty),
                    new XElement("DocumentHash", documentHash),
                    new XElement("Totals",
                        new XElement("Gross", Money(request.Totals.GrossTotal)),
                        new XElement("Discount", Money(request.Totals.DiscountTotal)),
                        new XElement("InvoiceDiscountPercent", request.InvoiceDiscountPercent.ToString("0.####", CultureInfo.InvariantCulture)),
                        new XElement("InvoiceDiscountAmount", Money(request.Totals.DiscountTotal - request.Lines.Sum(x => x.LineDiscount))),
                        new XElement("Tax", Money(request.Totals.TaxTotal)),
                        new XElement("Net", Money(request.Totals.NetTotal))),
                    new XElement("Lines", lineSnapshots.Select((line, index) =>
                        new XElement("Line",
                            new XAttribute("number", index + 1),
                            new XElement("Medicine", line.MedicineName),
                            new XElement("Quantity", line.Quantity.ToString("0.###", CultureInfo.InvariantCulture)),
                            new XElement("UnitPrice", Money(line.UnitPrice)),
                            new XElement("LineDiscount", Money(line.DiscountAmount)),
                            new XElement("AllocatedInvoiceDiscount", "0.00"),
                            new XElement("TaxableAmount", Money(line.TaxableAmount)),
                            new XElement("TaxRate", line.TaxRatePercent.ToString("0.####", CultureInfo.InvariantCulture)),
                            new XElement("TaxAmount", Money(line.TaxAmount)),
                            new XElement("LineTotal", Money(line.LineTotal)))))));

            return new EInvoiceArtifact
            {
                Uuid = uuid,
                Counter = nextCounter,
                PreviousHash = declaredPreviousHash ?? string.Empty,
                DocumentHash = documentHash,
                Xml = xml.Declaration == null ? xml.ToString(SaveOptions.DisableFormatting) : xml.Declaration + xml.ToString(SaveOptions.DisableFormatting),
                QrTlvBase64 = qr,
                Status = EInvoiceTransmissionStatus.Pending
            };
        }

        private static void ValidateRequest(EInvoiceRequest request, long counter)
        {
            if (request == null || request.Totals == null || request.Lines == null || request.Lines.Count == 0)
                throw new InvalidOperationException("طلب الفاتورة الإلكترونية غير مكتمل.");
            if (counter <= 0 || request.InvoiceNumber <= 0)
                throw new InvalidOperationException("عداد الفاتورة ورقمها يجب أن يكونا موجبين.");
            if (string.IsNullOrWhiteSpace(request.SellerName) || string.IsNullOrWhiteSpace(request.SellerVatNumber))
                throw new InvalidOperationException("اسم المورد ورقمه الضريبي مطلوبان قبل إنشاء QR.");
            if (request.Kind == EInvoiceDocumentKind.TaxB2B && (string.IsNullOrWhiteSpace(request.BuyerName) || string.IsNullOrWhiteSpace(request.BuyerVatNumber)))
                throw new InvalidOperationException("الفاتورة الضريبية B2B تحتاج بيانات المشتري ورقمه الضريبي.");
            if ((request.Kind == EInvoiceDocumentKind.CreditNote || request.Kind == EInvoiceDocumentKind.DebitNote) &&
                (!request.OriginalInvoiceId.HasValue || string.IsNullOrWhiteSpace(request.AdjustmentReason)))
                throw new InvalidOperationException("الإشعار الدائن أو المدين يحتاج الفاتورة الأصلية وسبب التعديل.");
            if (string.IsNullOrWhiteSpace(request.CurrencyCode) || request.CurrencyCode.Length != 3)
                throw new InvalidOperationException("رمز العملة ISO-4217 مطلوب.");
            if (!string.Equals(request.CurrencyCode, "SAR", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("مزود فاتورة السعودية المحلي لا يقبل إلا الريال السعودي (SAR).");
            InvoiceTotalsContract.Validate(request.Totals);
            IReadOnlyList<EInvoiceLineSnapshot> lineSnapshots = GetLineSnapshots(request);
            decimal gross = InvoiceTotalsCalculator.Round(lineSnapshots.Sum(x => x.GrossAmount));
            decimal discount = InvoiceTotalsCalculator.Round(lineSnapshots.Sum(x => x.DiscountAmount));
            decimal taxable = InvoiceTotalsCalculator.Round(lineSnapshots.Sum(x => x.TaxableAmount));
            decimal tax = InvoiceTotalsCalculator.Round(lineSnapshots.Sum(x => x.TaxAmount));
            decimal net = InvoiceTotalsCalculator.Round(lineSnapshots.Sum(x => x.LineTotal));
            if (gross != request.Totals.GrossTotal || discount != request.Totals.DiscountTotal || taxable != request.Totals.TaxableTotal || tax != request.Totals.TaxTotal || net != request.Totals.NetTotal)
                throw new InvalidOperationException("إجماليات طلب الفاتورة الإلكترونية لا تطابق بنودها.");
        }

        private static IReadOnlyList<EInvoiceLineSnapshot> GetLineSnapshots(EInvoiceRequest request)
        {
            if (request.ExactLines != null && request.ExactLines.Count > 0)
            {
                if (request.Kind != EInvoiceDocumentKind.CreditNote || request.InvoiceDiscountPercent != 0m || request.ExactLines.Count != request.Lines.Count)
                    throw new InvalidOperationException("لقطة سطور الإشعار الدائن غير صالحة.");
                for (int index = 0; index < request.ExactLines.Count; index++)
                {
                    EInvoiceLineSnapshot line = request.ExactLines[index];
                    CommercialInvoiceLine requestLine = request.Lines[index];
                    if (line == null || string.IsNullOrWhiteSpace(line.MedicineName) || line.Quantity <= 0m || line.UnitPrice < 0m ||
                        line.GrossAmount < 0m || line.DiscountAmount < 0m || line.TaxableAmount < 0m || line.TaxAmount < 0m || line.LineTotal < 0m ||
                        line.DiscountAmount > line.GrossAmount || line.TaxAmount > line.LineTotal ||
                        requestLine == null || line.MedicineName != requestLine.MedicineName || line.Quantity != requestLine.Quantity ||
                        line.UnitPrice != requestLine.UnitPrice || line.TaxRatePercent != requestLine.TaxRatePercent ||
                        line.PriceTaxMode != requestLine.PriceTaxMode || line.DiscountAmount != requestLine.LineDiscount ||
                        InvoiceTotalsCalculator.Round(line.LineTotal - line.TaxAmount) != line.TaxableAmount ||
                        (line.PriceTaxMode == CommercialPriceTaxMode.Inclusive
                            ? InvoiceTotalsCalculator.Round(line.GrossAmount - line.DiscountAmount) != line.LineTotal
                            : InvoiceTotalsCalculator.Round(line.GrossAmount - line.DiscountAmount) != line.TaxableAmount))
                        throw new InvalidOperationException("لقطة سطر الإشعار الدائن لا تتوازن.");
                }
                return request.ExactLines;
            }
            IReadOnlyList<decimal> taxes = InvoiceTotalsCalculator.CalculateLineTaxAmounts(request.Lines, request.InvoiceDiscountPercent);
            IReadOnlyList<decimal> discounts = InvoiceTotalsCalculator.CalculateAllocatedInvoiceDiscounts(request.Lines, request.InvoiceDiscountPercent);
            return request.Lines.Select((line, index) =>
            {
                decimal gross = InvoiceTotalsCalculator.Round(line.Quantity * line.UnitPrice);
                decimal discount = InvoiceTotalsCalculator.Round(line.LineDiscount + discounts[index]);
                decimal afterDiscount = InvoiceTotalsCalculator.Round(gross - discount);
                decimal taxable = line.PriceTaxMode == CommercialPriceTaxMode.Inclusive ? InvoiceTotalsCalculator.Round(afterDiscount - taxes[index]) : afterDiscount;
                return new EInvoiceLineSnapshot { MedicineName = line.MedicineName, Quantity = line.Quantity, UnitPrice = line.UnitPrice, GrossAmount = gross, DiscountAmount = discount, TaxableAmount = taxable, TaxRatePercent = line.TaxRatePercent, TaxAmount = taxes[index], LineTotal = line.PriceTaxMode == CommercialPriceTaxMode.Inclusive ? afterDiscount : InvoiceTotalsCalculator.Round(taxable + taxes[index]), PriceTaxMode = line.PriceTaxMode };
            }).ToList();
        }

        private static string BuildCanonicalPayload(EInvoiceRequest request, string uuid, long counter, string previousHash)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append(uuid).Append('|').Append(request.InvoiceNumber).Append('|').Append(counter).Append('|')
                .Append(request.Kind).Append('|').Append(request.IssueDateUtc.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture)).Append('|')
                .Append(request.CurrencyCode).Append('|').Append(request.SellerName).Append('|').Append(request.SellerVatNumber).Append('|')
                .Append(request.BuyerName).Append('|').Append(request.BuyerVatNumber).Append('|')
                .Append(request.OriginalInvoiceId.HasValue ? request.OriginalInvoiceId.Value.ToString("D") : string.Empty).Append('|').Append(request.AdjustmentReason ?? string.Empty).Append('|')
                .Append(Money(request.Totals.GrossTotal)).Append('|')
                .Append(Money(request.Totals.DiscountTotal)).Append('|').Append(request.InvoiceDiscountPercent.ToString("0.####", CultureInfo.InvariantCulture)).Append('|').Append(Money(request.Totals.TaxTotal)).Append('|')
                .Append(Money(request.Totals.NetTotal)).Append('|').Append(previousHash ?? string.Empty);
            foreach (EInvoiceLineSnapshot line in GetLineSnapshots(request))
                builder.Append('|').Append(line.MedicineName).Append('|').Append(line.Quantity.ToString("0.###", CultureInfo.InvariantCulture)).Append('|').Append(Money(line.UnitPrice)).Append('|').Append(line.PriceTaxMode).Append('|')
                    .Append(Money(line.GrossAmount)).Append('|').Append(Money(line.DiscountAmount)).Append('|').Append(Money(line.TaxableAmount)).Append('|')
                    .Append(line.TaxRatePercent.ToString("0.####", CultureInfo.InvariantCulture)).Append('|').Append(Money(line.TaxAmount)).Append('|').Append(Money(line.LineTotal));
            return builder.ToString();
        }

        private static string Money(decimal amount) { return amount.ToString("0.00", CultureInfo.InvariantCulture); }
        private static string Sha256Base64(string value)
        {
            using (SHA256 sha = SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(value)));
        }
    }

    internal static class ZatcaTlvQrEncoder
    {
        public static string Encode(string sellerName, string vatNumber, DateTime issueDateUtc, decimal total, decimal vat)
        {
            using (var stream = new System.IO.MemoryStream())
            {
                Add(stream, 1, sellerName); Add(stream, 2, vatNumber);
                Add(stream, 3, issueDateUtc.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
                Add(stream, 4, total.ToString("0.00", CultureInfo.InvariantCulture)); Add(stream, 5, vat.ToString("0.00", CultureInfo.InvariantCulture));
                return Convert.ToBase64String(stream.ToArray());
            }
        }

        private static void Add(System.IO.Stream stream, int tag, string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            if (bytes.Length > 255) throw new InvalidOperationException("حقل QR أطول من حد TLV المدعوم.");
            stream.WriteByte((byte)tag); stream.WriteByte((byte)bytes.Length); stream.Write(bytes, 0, bytes.Length);
        }
    }
}

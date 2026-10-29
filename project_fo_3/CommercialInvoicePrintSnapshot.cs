using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace project_fo_3
{
    // لقطة قراءة فقط للقيم المعتمدة؛ لا تعتمد الطباعة التجارية على السلة القابلة للتغيير.
    internal sealed class CommercialInvoicePrintSnapshot
    {
        private readonly InvoiceTotals totals;

        public Guid InvoiceId { get; private set; }
        public long InvoiceNumber { get; private set; }
        public DateTime IssuedAtUtc { get; private set; }
        public string CurrencyCode { get; private set; }
        public decimal ExchangeRate { get; private set; }
        public CommercialPriceTaxMode PriceTaxMode { get; private set; }
        public string CustomerId { get; private set; }
        public string CustomerName { get; private set; }
        public string CustomerPhone { get; private set; }
        public string CustomerAddress { get; private set; }
        public string IssuedBy { get; private set; }
        public string Notes { get; private set; }
        public InvoiceTotals Totals { get { return CopyTotals(totals); } }
        public IReadOnlyList<CommercialInvoicePrintLine> Lines { get; private set; }

        public CommercialInvoicePrintSnapshot(Guid invoiceId, long invoiceNumber, DateTime issuedAtUtc, string currencyCode, decimal exchangeRate,
            string customerId, string customerName, string customerPhone, string customerAddress, string issuedBy, string notes,
            InvoiceTotals invoiceTotals, IEnumerable<CommercialInvoicePrintLine> lines)
        {
            if (invoiceTotals == null) throw new ArgumentNullException(nameof(invoiceTotals));
            InvoiceId = invoiceId;
            InvoiceNumber = invoiceNumber;
            IssuedAtUtc = issuedAtUtc.ToUniversalTime();
            CurrencyCode = string.IsNullOrWhiteSpace(currencyCode) ? string.Empty : currencyCode.Trim().ToUpperInvariant();
            ExchangeRate = exchangeRate;
            PriceTaxMode = invoiceTotals.PriceTaxMode;
            CustomerId = customerId ?? string.Empty;
            CustomerName = customerName ?? string.Empty;
            CustomerPhone = customerPhone ?? string.Empty;
            CustomerAddress = customerAddress ?? string.Empty;
            IssuedBy = issuedBy ?? string.Empty;
            Notes = notes ?? string.Empty;
            totals = CopyTotals(invoiceTotals);
            Lines = new ReadOnlyCollection<CommercialInvoicePrintLine>(new List<CommercialInvoicePrintLine>(lines ?? new CommercialInvoicePrintLine[0]));
        }

        private static InvoiceTotals CopyTotals(InvoiceTotals value)
        {
            return new InvoiceTotals { PriceTaxMode = value.PriceTaxMode, GrossTotal = value.GrossTotal, DiscountTotal = value.DiscountTotal, TaxableTotal = value.TaxableTotal, TaxTotal = value.TaxTotal, NetTotal = value.NetTotal };
        }
    }

    internal sealed class CommercialInvoicePrintLine
    {
        public string MedicineName { get; private set; }
        public decimal Quantity { get; private set; }
        public decimal UnitPrice { get; private set; }
        public string Unit { get; private set; }
        public decimal LineDiscount { get; private set; }
        public decimal AllocatedInvoiceDiscount { get; private set; }
        public decimal TaxRatePercent { get; private set; }
        public decimal TaxAmount { get; private set; }
        public decimal LineTotal { get; private set; }
        public decimal GrossTotal { get; private set; }
        public int PriceBaseQuantity { get; private set; }
        public string UnitPriceDisplay { get { return UnitPrice.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + (PriceBaseQuantity > 1 ? " / " + PriceBaseQuantity.ToString(System.Globalization.CultureInfo.InvariantCulture) : string.Empty); } }

        public CommercialInvoicePrintLine(string medicineName, decimal quantity, decimal unitPrice, decimal lineDiscount,
            decimal allocatedInvoiceDiscount, decimal taxRatePercent, decimal taxAmount, decimal lineTotal, string unit = null, decimal? actualGross = null, int priceBaseQuantity = 1)
        {
            if(priceBaseQuantity<=0)throw new ArgumentOutOfRangeException(nameof(priceBaseQuantity));
            PriceBaseQuantity=priceBaseQuantity;
            MedicineName = medicineName ?? string.Empty;
            Quantity = quantity;
            UnitPrice = unitPrice;
            Unit = unit ?? string.Empty;
            LineDiscount = lineDiscount;
            AllocatedInvoiceDiscount = allocatedInvoiceDiscount;
            TaxRatePercent = taxRatePercent;
            TaxAmount = taxAmount;
            LineTotal = lineTotal;
            GrossTotal = actualGross ?? InvoiceTotalsCalculator.Round(quantity * unitPrice);
        }
    }
}

using System;
using System.Collections.Generic;

namespace PharmacyManagement.Application
{
    public enum SalesHistoryMode
    {
        CustomerSummary,
        LegacyInvoice,
        CommercialInvoice
    }

    public sealed class SalesHistoryRecord
    {
        public Guid? InvoiceId { get; set; }
        public long? InvoiceNumber { get; set; }
        public string InvoiceReference { get; set; }
        public string CustomerId { get; set; }
        public string CustomerName { get; set; }
        public string PhoneNumber { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime IssuedAt { get; set; }
        public string CashierName { get; set; }
        public string Status { get; set; }
        public string Address { get; set; }
        public string Notes { get; set; }
    }

    public sealed class SalesHistoryLine
    {
        public string MedicineName { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Quantity { get; set; }
        public int BaseQuantity { get; set; }
        public string Unit { get; set; }
        public decimal TotalAmount { get; set; }
    }

    public sealed class SalesHistoryPage
    {
        public SalesHistoryMode Mode { get; set; }
        public bool HasLegacyArchive { get; set; }
        public IReadOnlyList<SalesHistoryRecord> Records { get; set; }
    }

    public interface ISalesHistoryRepository
    {
        SalesHistoryPage Load(bool showLegacyInvoices);
        IReadOnlyList<SalesHistoryLine> LoadCustomerLines(string customerId);
        IReadOnlyList<SalesHistoryLine> LoadLegacyInvoiceLines(string invoiceReference);
        IReadOnlyList<SalesHistoryLine> LoadCommercialInvoiceLines(Guid invoiceId);
    }

    public interface ISalesHistoryService
    {
        SalesHistoryPage Load(bool showLegacyInvoices);
        IReadOnlyList<SalesHistoryLine> LoadCustomerLines(string customerId);
        IReadOnlyList<SalesHistoryLine> LoadLegacyInvoiceLines(string invoiceReference);
        IReadOnlyList<SalesHistoryLine> LoadCommercialInvoiceLines(Guid invoiceId);
    }

    public sealed class SalesHistoryService : ISalesHistoryService
    {
        private readonly ISalesHistoryRepository repository;

        public SalesHistoryService(ISalesHistoryRepository repository)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public SalesHistoryPage Load(bool showLegacyInvoices)
        {
            SalesHistoryPage page = repository.Load(showLegacyInvoices);
            if (page == null || page.Records == null) throw new InvalidOperationException("تعذر تحميل سجل المبيعات بصورة مكتملة.");
            return page;
        }

        public IReadOnlyList<SalesHistoryLine> LoadCustomerLines(string customerId) =>
            repository.LoadCustomerLines(Required(customerId, 50, "رقم العميل"));

        public IReadOnlyList<SalesHistoryLine> LoadLegacyInvoiceLines(string invoiceReference) =>
            repository.LoadLegacyInvoiceLines(Required(invoiceReference, 50, "مرجع الفاتورة"));

        public IReadOnlyList<SalesHistoryLine> LoadCommercialInvoiceLines(Guid invoiceId)
        {
            if (invoiceId == Guid.Empty) throw new InvalidOperationException("معرف الفاتورة التجارية غير صالح.");
            return repository.LoadCommercialInvoiceLines(invoiceId);
        }

        private static string Required(string value, int maximumLength, string fieldName)
        {
            string normalized = (value ?? string.Empty).Trim();
            if (normalized.Length == 0) throw new InvalidOperationException(fieldName + " مطلوب.");
            if (normalized.Length > maximumLength) throw new InvalidOperationException(fieldName + " يتجاوز الحد المسموح (" + maximumLength + ").");
            return normalized;
        }
    }
}

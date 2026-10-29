using PharmacyManagement.Domain;
using System;
using System.Collections.Generic;

namespace PharmacyManagement.Application
{
    public sealed class SalesInvoiceLineRequest
    {
        public string MedicineName { get; set; }
        public MedicineUnitSelection Unit { get; set; }
        public int DisplayedSellingPrice { get; set; }
        public int Quantity { get; set; }
    }

    public sealed class SalesInvoiceIssueRequest
    {
        public string ImportSourceSystem { get; set; }
        public string ImportSourceInvoiceId { get; set; }
        public Guid? ImportDraftId { get; set; }
        public string ImportCurrencyCode { get; set; }
        // POS display identity, checked again inside the financial transaction; null retains legacy/import callers.
        public string ExpectedCurrencyCode { get; set; }
        public decimal? ImportTaxRatePercent { get; set; }
        public string ImportPriceTaxMode { get; set; }
        public string CustomerId { get; set; }
        public string CustomerName { get; set; }
        public string PhoneNumber { get; set; }
        public string CustomerAddress { get; set; }
        public string EmployeeName { get; set; }
        public string CashierEmployeeId { get; set; }
        public string Notes { get; set; }
        public DateTime InvoiceDate { get; set; }
        public decimal DisplayedGrossTotal { get; set; }
        public decimal DisplayedNetTotal { get; set; }
        public decimal DiscountPercent { get; set; }
        public bool UseCustomerCredit { get; set; }
        public string CreditCustomerId { get; set; }
        public IReadOnlyList<SalesInvoiceLineRequest> Lines { get; set; }
    }

    public sealed class SalesInvoiceIssueResult
    {
        public bool IsCommercial { get; set; }
        public string CustomerId { get; set; }
        public Guid? CommercialInvoiceId { get; set; }
        public long? CommercialInvoiceNumber { get; set; }
        public string AccountingSourceId { get; set; }
        public IReadOnlyList<string> BillLineIds { get; set; }
        public decimal GrossTotal { get; set; }
        public decimal DiscountTotal { get; set; }
        public decimal TaxTotal { get; set; }
        public decimal NetTotal { get; set; }
        public decimal CostOfGoods { get; set; }
        public decimal CustomerCreditApplied { get; set; }
        public decimal CashCollected { get; set; }
    }

    public interface ISalesInvoiceOperation
    {
        SalesInvoiceIssueResult Issue(SalesInvoiceIssueRequest request);
    }

    public sealed class SalesInvoiceCancellationRequest
    {
        public string InvoiceReference { get; set; }
        public IReadOnlyList<string> BillLineIds { get; set; }
        public string Reason { get; set; }
        public string ActorName { get; set; }
    }

    public sealed class SalesInvoiceCancellationOutcome
    {
        public string OriginalInvoiceReference { get; set; }
        public decimal TotalAmount { get; set; }
        public int RestoredQuantity { get; set; }
    }

    public interface ISalesInvoiceCancellationOperation
    {
        SalesInvoiceCancellationOutcome Cancel(SalesInvoiceCancellationRequest request);
    }
}

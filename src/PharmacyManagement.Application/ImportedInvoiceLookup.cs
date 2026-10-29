namespace PharmacyManagement.Application
{
    // Read side of the imported-invoice identity registry. It lets an approval retry return the
    // original posted reference instead of failing with "already imported" after a lost response.
    public sealed class ImportedInvoiceLookupResult
    {
        public string PostedReference { get; set; }
    }

    public interface IImportedInvoiceLookup
    {
        ImportedInvoiceLookupResult Find(string kind, string sourceSystem, string sourceInvoiceId);
    }
}

using System;
using System.Data;
using System.Data.SqlClient;
using PharmacyManagement.Application;

namespace project_fo_3
{
    // Read-only lookup over ImportedInvoiceSourceTb, used by the approval retry path.
    internal sealed class SqlImportedInvoiceLookup : IImportedInvoiceLookup
    {
        public ImportedInvoiceLookupResult Find(string kind, string sourceSystem, string sourceInvoiceId)
        {
            string system, invoice;
            if (!InvoiceImportSourceIdentity.TryNormalize(sourceSystem, sourceInvoiceId, out system, out invoice)) return null;
            if (kind != "Sale" && kind != "Purchase") return null;
            using (SqlConnection connection = Database.CreateConnection())
            using (SqlCommand command = new SqlCommand(
                @"SELECT TOP(1) PostedReference FROM dbo.ImportedInvoiceSourceTb
WHERE SourceSystem=@System AND InvoiceKind=@Kind AND SourceInvoiceId=@SourceId;", connection))
            {
                command.Parameters.Add("@System", SqlDbType.NVarChar, 100).Value = system;
                command.Parameters.Add("@Kind", SqlDbType.NVarChar, 8).Value = kind;
                command.Parameters.Add("@SourceId", SqlDbType.NVarChar, 100).Value = invoice;
                connection.Open();
                object value = command.ExecuteScalar();
                if (value == null || value == DBNull.Value) return null;
                string reference = Convert.ToString(value);
                return string.IsNullOrWhiteSpace(reference) ? null : new ImportedInvoiceLookupResult { PostedReference = reference };
            }
        }
    }
}

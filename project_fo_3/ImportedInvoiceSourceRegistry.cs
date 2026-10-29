using System;
using System.Data;
using System.Data.SqlClient;
using PharmacyManagement.Application;

namespace project_fo_3
{
    internal static class ImportedInvoiceSourceRegistry
    {
        internal static void Record(SqlConnection connection, SqlTransaction transaction, string kind,
            string sourceSystem, string sourceInvoiceId, string postedReference)
        {
            string system, invoice;
            if (!InvoiceImportSourceIdentity.TryNormalize(sourceSystem, sourceInvoiceId, out system, out invoice)) return;
            if (kind != "Sale" && kind != "Purchase") throw new ArgumentException("Invalid imported invoice kind.", nameof(kind));
            if (string.IsNullOrWhiteSpace(postedReference) || postedReference.Length > 100)
                throw new InvalidOperationException("مرجع الفاتورة المعتمدة غير صالح لاستيرادها.");
            try
            {
                using (var command = new SqlCommand(@"INSERT dbo.ImportedInvoiceSourceTb
(SourceSystem, InvoiceKind, SourceInvoiceId, PostedReference)
VALUES (@System, @Kind, @SourceId, @PostedReference);", connection, transaction))
                {
                    command.Parameters.Add("@System", SqlDbType.NVarChar, 100).Value = system;
                    command.Parameters.Add("@Kind", SqlDbType.NVarChar, 8).Value = kind;
                    command.Parameters.Add("@SourceId", SqlDbType.NVarChar, 100).Value = invoice;
                    command.Parameters.Add("@PostedReference", SqlDbType.NVarChar, 100).Value = postedReference;
                    command.ExecuteNonQuery();
                }
            }
            catch (SqlException error) when (error.Number == 2627 || error.Number == 2601)
            {
                throw new InvalidOperationException("هذه الفاتورة من النظام المصدر معتمدة سابقًا؛ لا يمكن استيرادها مرتين.", error);
            }
            catch (SqlException error) when (error.Number == 208)
            {
                throw new InvalidOperationException("مخطط استيراد الفواتير غير مثبت؛ تعذر اعتماد الفاتورة المستوردة.", error);
            }
        }
    }
}

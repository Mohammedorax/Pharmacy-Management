using System;
using System.Data.SqlClient;

namespace project_fo_3
{
    internal static class ImportedInvoiceRegistryQaSelfTests
    {
        internal static void Run()
        {
            using (SqlConnection connection = Database.CreateConnection())
            {
                connection.Open();
                using (var guard = new SqlCommand("SELECT DB_NAME()", connection))
                    if (!string.Equals(Convert.ToString(guard.ExecuteScalar()), "PharmacyManagement_qa", StringComparison.Ordinal))
                        throw new InvalidOperationException("فحص الاستيراد مسموح على PharmacyManagement_qa فقط.");
                string system = "QA-IMPORT-" + Guid.NewGuid().ToString("N");
                int before = Count(connection, system);
                using (SqlTransaction transaction = connection.BeginTransaction())
                {
                    try
                    {
                        ImportedInvoiceSourceRegistry.Record(connection, transaction, "Sale", system, "A-1", "QA-sale");
                        ImportedInvoiceSourceRegistry.Record(connection, transaction, "Purchase", system, "A-1", "QA-purchase");
                        if (Count(connection, system, transaction) != 2)
                            throw new InvalidOperationException("مصدر البيع والشراء لم يُسجل داخل المعاملة.");
                        bool duplicateRejected = false;
                        try { ImportedInvoiceSourceRegistry.Record(connection, transaction, "Sale", system, "A-1", "QA-sale-duplicate"); }
                        catch (InvalidOperationException error) when (error.Message.Contains("معتمدة سابقًا"))
                        { duplicateRejected = true; }
                        if (!duplicateRejected) throw new InvalidOperationException("تكرار مصدر فاتورة البيع لم يُرفض.");
                    }
                    finally { transaction.Rollback(); }
                }
                if (Count(connection, system) != before)
                    throw new InvalidOperationException("اختبار مصدر الفاتورة ترك سجلًا دائمًا بعد التراجع.");
                Console.WriteLine("PASS: QA import source uniqueness, kind namespace and rollback; retained rows=0.");
            }
        }
        private static int Count(SqlConnection connection, string system, SqlTransaction transaction = null)
        {
            using (var command = new SqlCommand("SELECT COUNT(*) FROM dbo.ImportedInvoiceSourceTb WHERE SourceSystem=@Source", connection, transaction))
            {
                command.Parameters.Add("@Source", System.Data.SqlDbType.NVarChar, 100).Value = system;
                return Convert.ToInt32(command.ExecuteScalar());
            }
        }
    }
}

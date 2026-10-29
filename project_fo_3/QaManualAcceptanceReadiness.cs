using System;
using System.Data.SqlClient;

namespace project_fo_3
{
    // يثبت أن بيانات QA الأساسية متاحة للاختبار اليدوي من دون كشف أسماء العملاء أو الموظفين
    // أو إنشاء أي بيانات. لا يغني عن قبول المستخدم الفعلي أو اختبار الطابعة/القارئ.
    internal sealed class QaManualAcceptanceReadinessResult
    {
        internal long EmployeeCount { get; set; }
        internal long SupplierCount { get; set; }
        internal long StockedMedicineCount { get; set; }
        internal long BarcodeMedicineCount { get; set; }
        internal long LowStockMedicineCount { get; set; }
        internal long InventoryMovementCount { get; set; }
        internal long AuditEventCount { get; set; }

        internal bool HasRequiredData
        {
            get
            {
                return EmployeeCount > 0 && SupplierCount > 0 && StockedMedicineCount > 0;
            }
        }
    }

    internal static class QaManualAcceptanceReadiness
    {
        internal static QaManualAcceptanceReadinessResult Inspect(string serverName, string databaseName)
        {
            EnsureQaDatabase(databaseName);
            string connectionString = Database.BuildSettingsConnectionString(serverName, databaseName, true);

            using (SqlConnection connection = new SqlConnection(connectionString))
            using (SqlCommand command = new SqlCommand(@"
SELECT
    (SELECT COUNT_BIG(*) FROM dbo.EmployeeTb1) AS EmployeeCount,
    (SELECT COUNT_BIG(*) FROM dbo.CompanyTb1) AS SupplierCount,
    (SELECT COUNT_BIG(*) FROM dbo.MedicineTb1 WHERE ISNULL(MedQty, 0) > 0) AS StockedMedicineCount,
    (SELECT COUNT_BIG(*) FROM dbo.MedicineTb1 WHERE NULLIF(LTRIM(RTRIM(ISNULL(Barcode, N''))), N'') IS NOT NULL) AS BarcodeMedicineCount,
    -- يجب أن يطابق حد التنبيه الافتراضي في InventoryService وDatabase.GetOperationalAlerts.
    (SELECT COUNT_BIG(*) FROM dbo.MedicineTb1 WHERE ISNULL(MedQty, 0) BETWEEN 1 AND 10) AS LowStockMedicineCount,
    (SELECT COUNT_BIG(*) FROM dbo.InventoryMovementsTb) AS InventoryMovementCount,
    (SELECT COUNT_BIG(*) FROM dbo.AuditLogTb) AS AuditEventCount;", connection))
            {
                connection.Open();
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (!reader.Read())
                    {
                        throw new InvalidOperationException("فحص جاهزية QA لم يُرجع صفاً واحداً متوقعاً.");
                    }

                    return new QaManualAcceptanceReadinessResult
                    {
                        EmployeeCount = reader.GetInt64(0),
                        SupplierCount = reader.GetInt64(1),
                        StockedMedicineCount = reader.GetInt64(2),
                        BarcodeMedicineCount = reader.GetInt64(3),
                        LowStockMedicineCount = reader.GetInt64(4),
                        InventoryMovementCount = reader.GetInt64(5),
                        AuditEventCount = reader.GetInt64(6)
                    };
                }
            }
        }

        private static void EnsureQaDatabase(string databaseName)
        {
            if (string.IsNullOrWhiteSpace(databaseName) || !databaseName.Trim().EndsWith("_qa", StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException("فحص جاهزية القبول اليدوي مسموح فقط لقاعدة ينتهي اسمها بـ _qa.");
            }
        }
    }
}

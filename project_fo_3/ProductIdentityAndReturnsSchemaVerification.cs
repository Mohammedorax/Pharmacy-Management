using System;
using System.Data;
using System.Data.SqlClient;

namespace project_fo_3
{
    // فحص QA للقراءة فقط: يثبت الحواجز البنيوية ولا يخلط وجود الجداول بجاهزية تنفيذ مرتجع مالي.
    internal static class ProductIdentityAndReturnsSchemaVerification
    {
        internal static void VerifyQaReadOnly(string serverName, string databaseName)
        {
            if (string.IsNullOrWhiteSpace(databaseName) ||
                !(databaseName.EndsWith("_qa", StringComparison.OrdinalIgnoreCase) ||
                  databaseName.EndsWith("_test", StringComparison.OrdinalIgnoreCase) ||
                  databaseName.EndsWith("_sandbox", StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("فحص مخطط المادة الفعالة والمرتجع مسموح لقاعدة QA/Test/Sandbox فقط.");
            }

            using (SqlConnection connection = new SqlConnection(Database.BuildSettingsConnectionString(serverName, databaseName, true)))
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandTimeout = 15;
                command.CommandText = @"
IF OBJECT_ID(N'dbo.PharmacySchemaMigrationRegistryTb', N'U') IS NULL OR OBJECT_ID(N'dbo.CommercialMigrationRegistryTb', N'U') IS NULL
BEGIN
    SELECT CAST(0 AS INT);
    RETURN;
END;

SELECT CAST(CASE WHEN
    OBJECT_ID(N'dbo.ActiveIngredientsTb', N'U') IS NOT NULL AND
    OBJECT_ID(N'dbo.MedicineActiveIngredientsTb', N'U') IS NOT NULL AND
    OBJECT_ID(N'dbo.ActiveIngredientEvidenceTb', N'U') IS NOT NULL AND
    OBJECT_ID(N'dbo.SalesReturnHeadersTb', N'U') IS NOT NULL AND
    OBJECT_ID(N'dbo.SalesReturnLinesTb', N'U') IS NOT NULL AND
    EXISTS (SELECT 1 FROM dbo.PharmacySchemaMigrationRegistryTb WHERE MigrationId = N'0007_product_identity_and_partial_returns') AND
    EXISTS (SELECT 1 FROM dbo.CommercialMigrationRegistryTb WHERE MigrationId = N'0013_active_ingredient_evidence' AND ScriptSha256 = N'481048C74540531D74C8C72E40B6F7AFB3E5E335BC9F7C80439FC08E1F9758D2') AND
    EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_MedicineActiveIngredientsTb_Medicine') AND
    EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_MedicineActiveIngredientsTb_Ingredient') AND
    EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ActiveIngredientEvidenceTb_Ingredient') AND
    EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_SalesReturnHeadersTb_OriginalInvoice') AND
    EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_SalesReturnLinesTb_OriginalMappedBill') AND
    EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_MedicineActiveIngredientsTb_Primary' AND object_id = OBJECT_ID(N'dbo.MedicineActiveIngredientsTb') AND is_unique = 1 AND has_filter = 1) AND
    EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_ActiveIngredientEvidenceTb_Fingerprint' AND object_id = OBJECT_ID(N'dbo.ActiveIngredientEvidenceTb') AND is_unique = 1) AND
    OBJECT_ID(N'dbo.trg_ActiveIngredientEvidenceTb_NoDelete', N'TR') IS NOT NULL AND
    OBJECT_ID(N'dbo.trg_ActiveIngredientEvidenceTb_Immutable', N'TR') IS NOT NULL AND
    EXISTS (SELECT 1 FROM dbo.CommercialSchemaVersionsTb WHERE VersionNo = 9) AND
    EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SalesReturnLinesTb_OriginalBill' AND object_id = OBJECT_ID(N'dbo.SalesReturnLinesTb'))
THEN 1 ELSE 0 END AS INT) AS IsValid;";
                connection.Open();
                if (Convert.ToInt32(command.ExecuteScalar()) != 1)
                {
                    throw new InvalidOperationException("مخطط المادة الفعالة والمرتجع الجزئي في QA غير مكتمل أو لا يحمل القيود المتوقعة.");
                }
            }
        }
    }
}

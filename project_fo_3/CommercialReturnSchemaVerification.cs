using System;
using System.Data;
using System.Data.SqlClient;

namespace project_fo_3
{
    // فحص بنيوي للقراءة فقط؛ لا يفعّل FullCommercial ولا ينشئ فاتورة أو حركة مخزون.
    internal static class CommercialReturnSchemaVerification
    {
        private const string MigrationId = "0008_commercial_partial_returns";
        private const string MigrationSha256 = "B9134323784F9B45154BF86F531C6B305EC3ECEEC643E4BE8982B15E441F89F0";
        private const string IntegrityMigrationId = "0010_commercial_return_integrity_hardening";
        private const string IntegrityMigrationSha256 = "8D02B0CB59FBB6FE688BA24144A78DA23EA8F8C5EC28491DC29AA1453D263161";
        private const string CustomerLedgerMigrationId = "0011_customer_account_ledger";
        private const string CustomerLedgerMigrationSha256 = "5219D4DCBD3F41144BE927FD348DEA552A8CE2A9AD9DEB078A052AA68C95B4A8";
        private const string QuarantineMigrationId = "0012_quarantine_resolution";
        private const string QuarantineMigrationSha256 = "B8D516CC4FC8AD83B69C6F8152EE61B21B8A7A0293DF859522FEED7C97FDFA8A";
        private const string PaymentsMigrationId = "0014_sales_invoice_payments_and_credit_use";
        private const string PaymentsMigrationSha256 = "1BE98A6F198D58F14D955FF934290F3CBF5219257A44B8879994E66B9DB80846";
        private const string PaymentTriggerScopeMigrationId = "0015_sales_payment_trigger_scope";
        private const string PaymentTriggerScopeMigrationSha256 = "625ADFB7DCC87764D65765EEFB1301A0347E100AFC8CEC279FFF8415680C7F6A";

        internal static void VerifyQaReadOnly(string serverName, string databaseName)
        {
            if (string.IsNullOrWhiteSpace(databaseName) ||
                !(databaseName.EndsWith("_qa", StringComparison.OrdinalIgnoreCase) ||
                  databaseName.EndsWith("_test", StringComparison.OrdinalIgnoreCase) ||
                  databaseName.EndsWith("_sandbox", StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("فحص ترحيل المرتجعات التجارية مسموح لقاعدة QA/Test/Sandbox فقط.");
            }

            using (SqlConnection connection = new SqlConnection(Database.BuildSettingsConnectionString(serverName, databaseName, true)))
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandTimeout = 15;
                command.CommandText = @"
IF OBJECT_ID(N'dbo.CommercialMigrationRegistryTb', N'U') IS NULL
BEGIN
    SELECT CAST(0 AS INT);
    RETURN;
END;

SELECT CAST(CASE WHEN
    EXISTS (SELECT 1 FROM dbo.CommercialMigrationRegistryTb WHERE MigrationId = @MigrationId AND ScriptSha256 = @ScriptSha256) AND
    EXISTS (SELECT 1 FROM dbo.CommercialMigrationRegistryTb WHERE MigrationId = @IntegrityMigrationId AND ScriptSha256 = @IntegrityMigrationSha256) AND
    EXISTS (SELECT 1 FROM dbo.CommercialMigrationRegistryTb WHERE MigrationId = @CustomerLedgerMigrationId AND ScriptSha256 = @CustomerLedgerMigrationSha256) AND
    EXISTS (SELECT 1 FROM dbo.CommercialMigrationRegistryTb WHERE MigrationId = @QuarantineMigrationId AND ScriptSha256 = @QuarantineMigrationSha256) AND
    EXISTS (SELECT 1 FROM dbo.CommercialMigrationRegistryTb WHERE MigrationId = @PaymentsMigrationId AND ScriptSha256 = @PaymentsMigrationSha256) AND
    EXISTS (SELECT 1 FROM dbo.CommercialMigrationRegistryTb WHERE MigrationId = @PaymentTriggerScopeMigrationId AND ScriptSha256 = @PaymentTriggerScopeMigrationSha256) AND
    EXISTS (SELECT 1 FROM dbo.CommercialSchemaVersionsTb WHERE VersionNo = 11) AND
    OBJECT_ID(N'dbo.CommercialReturnHeadersTb', N'U') IS NOT NULL AND
    OBJECT_ID(N'dbo.CommercialReturnLinesTb', N'U') IS NOT NULL AND
    OBJECT_ID(N'dbo.CommercialReturnLotAllocationsTb', N'U') IS NOT NULL AND
    OBJECT_ID(N'dbo.CustomerAccountLedgerTb', N'U') IS NOT NULL AND
    OBJECT_ID(N'dbo.SalesInvoicePaymentsTb', N'U') IS NOT NULL AND
    OBJECT_ID(N'dbo.QuarantineResolutionEventsTb', N'U') IS NOT NULL AND
    EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_CommercialReturnHeadersTb_ReturnInvoice' AND parent_object_id=OBJECT_ID(N'dbo.CommercialReturnHeadersTb') AND is_disabled=0 AND is_not_trusted=0) AND
    EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_CommercialReturnHeadersTb_OriginalInvoice' AND parent_object_id=OBJECT_ID(N'dbo.CommercialReturnHeadersTb') AND is_disabled=0 AND is_not_trusted=0) AND
    EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_CommercialReturnLinesTb_Header' AND parent_object_id=OBJECT_ID(N'dbo.CommercialReturnLinesTb') AND is_disabled=0 AND is_not_trusted=0) AND
    EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_CommercialReturnLinesTb_ReturnInvoiceLine' AND parent_object_id=OBJECT_ID(N'dbo.CommercialReturnLinesTb') AND is_disabled=0 AND is_not_trusted=0) AND
    EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_CommercialReturnLinesTb_OriginalLine' AND parent_object_id=OBJECT_ID(N'dbo.CommercialReturnLinesTb') AND is_disabled=0 AND is_not_trusted=0) AND
    EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_CommercialReturnLotAllocationsTb_SaleAllocation' AND parent_object_id=OBJECT_ID(N'dbo.CommercialReturnLotAllocationsTb') AND is_disabled=0 AND is_not_trusted=0) AND
    EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_CustomerAccountLedgerTb_Customer' AND parent_object_id=OBJECT_ID(N'dbo.CustomerAccountLedgerTb') AND is_disabled=0 AND is_not_trusted=0) AND
    EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_CustomerAccountLedgerTb_Journal' AND parent_object_id=OBJECT_ID(N'dbo.CustomerAccountLedgerTb') AND is_disabled=0 AND is_not_trusted=0) AND
    EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_SalesInvoicePaymentsTb_Invoice' AND parent_object_id=OBJECT_ID(N'dbo.SalesInvoicePaymentsTb') AND is_disabled=0 AND is_not_trusted=0) AND
    EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_QuarantineResolutionEventsTb_Allocation' AND parent_object_id=OBJECT_ID(N'dbo.QuarantineResolutionEventsTb') AND is_disabled=0 AND is_not_trusted=0) AND
    EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_QuarantineResolutionEventsTb_Journal' AND parent_object_id=OBJECT_ID(N'dbo.QuarantineResolutionEventsTb') AND is_disabled=0 AND is_not_trusted=0) AND
    EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'UX_CommercialReturnHeadersTb_Request' AND object_id=OBJECT_ID(N'dbo.CommercialReturnHeadersTb') AND is_unique=1) AND
    EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'UX_CommercialReturnLinesTb_ReturnOriginal' AND object_id=OBJECT_ID(N'dbo.CommercialReturnLinesTb') AND is_unique=1) AND
    EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'UX_CommercialReturnLotAllocationsTb_ReturnLineSaleAllocation' AND object_id=OBJECT_ID(N'dbo.CommercialReturnLotAllocationsTb') AND is_unique=1) AND
    EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'UX_CustomerAccountLedgerTb_Source' AND object_id=OBJECT_ID(N'dbo.CustomerAccountLedgerTb') AND is_unique=1) AND
    EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_CustomerAccountLedgerTb_Customer_Date' AND object_id=OBJECT_ID(N'dbo.CustomerAccountLedgerTb')) AND
    EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'UX_SalesInvoicePaymentsTb_Invoice_Method' AND object_id=OBJECT_ID(N'dbo.SalesInvoicePaymentsTb') AND is_unique=1) AND
    EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_SalesInvoicePaymentsTb_Invoice' AND object_id=OBJECT_ID(N'dbo.SalesInvoicePaymentsTb')) AND
    EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'UX_QuarantineResolutionEventsTb_Request' AND object_id=OBJECT_ID(N'dbo.QuarantineResolutionEventsTb') AND is_unique=1) AND
    EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_QuarantineResolutionEventsTb_Allocation_Date' AND object_id=OBJECT_ID(N'dbo.QuarantineResolutionEventsTb')) AND
    EXISTS (SELECT 1 FROM sys.check_constraints WHERE name=N'CK_CommercialReturnHeadersTb_RefundMethod' AND parent_object_id=OBJECT_ID(N'dbo.CommercialReturnHeadersTb') AND is_disabled=0 AND is_not_trusted=0) AND
    EXISTS (SELECT 1 FROM sys.check_constraints WHERE name=N'CK_CommercialReturnLinesTb_Amounts' AND parent_object_id=OBJECT_ID(N'dbo.CommercialReturnLinesTb') AND is_disabled=0 AND is_not_trusted=0) AND
    EXISTS (SELECT 1 FROM sys.check_constraints WHERE name=N'CK_CommercialReturnLotAllocationsTb_Disposition' AND parent_object_id=OBJECT_ID(N'dbo.CommercialReturnLotAllocationsTb') AND is_disabled=0 AND is_not_trusted=0) AND
    EXISTS (SELECT 1 FROM sys.check_constraints WHERE name=N'CK_CustomerAccountLedgerTb_EntryKind' AND parent_object_id=OBJECT_ID(N'dbo.CustomerAccountLedgerTb') AND is_disabled=0 AND is_not_trusted=0) AND
    EXISTS (SELECT 1 FROM sys.check_constraints WHERE name=N'CK_CustomerAccountLedgerTb_Amounts' AND parent_object_id=OBJECT_ID(N'dbo.CustomerAccountLedgerTb') AND is_disabled=0 AND is_not_trusted=0) AND
    EXISTS (SELECT 1 FROM sys.check_constraints WHERE name=N'CK_SalesInvoicePaymentsTb_Method' AND parent_object_id=OBJECT_ID(N'dbo.SalesInvoicePaymentsTb') AND is_disabled=0 AND is_not_trusted=0) AND
    EXISTS (SELECT 1 FROM sys.check_constraints WHERE name=N'CK_SalesInvoicePaymentsTb_Amount' AND parent_object_id=OBJECT_ID(N'dbo.SalesInvoicePaymentsTb') AND is_disabled=0 AND is_not_trusted=0) AND
    EXISTS (SELECT 1 FROM sys.check_constraints WHERE name=N'CK_QuarantineResolutionEventsTb_Type' AND parent_object_id=OBJECT_ID(N'dbo.QuarantineResolutionEventsTb') AND is_disabled=0 AND is_not_trusted=0) AND
    EXISTS (SELECT 1 FROM sys.check_constraints WHERE name=N'CK_QuarantineResolutionEventsTb_Journal' AND parent_object_id=OBJECT_ID(N'dbo.QuarantineResolutionEventsTb') AND is_disabled=0 AND is_not_trusted=0) AND
    EXISTS (SELECT 1 FROM sys.triggers WHERE name=N'trg_CommercialReturnHeadersTb_AppendOnly' AND parent_id=OBJECT_ID(N'dbo.CommercialReturnHeadersTb') AND is_disabled=0) AND
    EXISTS (SELECT 1 FROM sys.triggers WHERE name=N'trg_CommercialReturnLinesTb_AppendOnly' AND parent_id=OBJECT_ID(N'dbo.CommercialReturnLinesTb') AND is_disabled=0) AND
    EXISTS (SELECT 1 FROM sys.triggers WHERE name=N'trg_CommercialReturnLotAllocationsTb_AppendOnly' AND parent_id=OBJECT_ID(N'dbo.CommercialReturnLotAllocationsTb') AND is_disabled=0) AND
    EXISTS (SELECT 1 FROM sys.triggers WHERE name=N'trg_CommercialReturnHeadersTb_InvoiceLink' AND parent_id=OBJECT_ID(N'dbo.CommercialReturnHeadersTb') AND is_disabled=0) AND
    EXISTS (SELECT 1 FROM sys.triggers WHERE name=N'trg_CommercialReturnLinesTb_InvoiceLink' AND parent_id=OBJECT_ID(N'dbo.CommercialReturnLinesTb') AND is_disabled=0) AND
    EXISTS (SELECT 1 FROM sys.triggers WHERE name=N'trg_CommercialReturnLotAllocationsTb_SourceLink' AND parent_id=OBJECT_ID(N'dbo.CommercialReturnLotAllocationsTb') AND is_disabled=0) AND
    EXISTS (SELECT 1 FROM sys.triggers WHERE name=N'trg_CustomerAccountLedgerTb_AppendOnly' AND parent_id=OBJECT_ID(N'dbo.CustomerAccountLedgerTb') AND is_disabled=0) AND
    EXISTS (SELECT 1 FROM sys.triggers WHERE name=N'trg_SalesInvoicePaymentsTb_AppendOnly' AND parent_id=OBJECT_ID(N'dbo.SalesInvoicePaymentsTb') AND is_disabled=0) AND
    EXISTS (SELECT 1 FROM sys.triggers WHERE name=N'trg_SalesInvoicePaymentsTb_Integrity' AND parent_id=OBJECT_ID(N'dbo.SalesInvoicePaymentsTb') AND is_disabled=0) AND
    EXISTS (SELECT 1 FROM sys.triggers WHERE name=N'trg_SalesInvoicesTb_PaymentCompletion' AND parent_id=OBJECT_ID(N'dbo.SalesInvoicesTb') AND is_disabled=0 AND OBJECT_DEFINITION(object_id) LIKE N'%InvoiceType%Sale%') AND
    EXISTS (SELECT 1 FROM sys.triggers WHERE name=N'trg_QuarantineResolutionEventsTb_AppendOnly' AND parent_id=OBJECT_ID(N'dbo.QuarantineResolutionEventsTb') AND is_disabled=0) AND
    EXISTS (SELECT 1 FROM sys.triggers WHERE name=N'trg_QuarantineResolutionEventsTb_Integrity' AND parent_id=OBJECT_ID(N'dbo.QuarantineResolutionEventsTb') AND is_disabled=0)
THEN 1 ELSE 0 END AS INT);";
                command.Parameters.Add("@MigrationId", SqlDbType.NVarChar, 200).Value = MigrationId;
                command.Parameters.Add("@ScriptSha256", SqlDbType.Char, 64).Value = MigrationSha256;
                command.Parameters.Add("@IntegrityMigrationId", SqlDbType.NVarChar, 200).Value = IntegrityMigrationId;
                command.Parameters.Add("@IntegrityMigrationSha256", SqlDbType.Char, 64).Value = IntegrityMigrationSha256;
                command.Parameters.Add("@CustomerLedgerMigrationId", SqlDbType.NVarChar, 200).Value = CustomerLedgerMigrationId;
                command.Parameters.Add("@CustomerLedgerMigrationSha256", SqlDbType.Char, 64).Value = CustomerLedgerMigrationSha256;
                command.Parameters.Add("@QuarantineMigrationId", SqlDbType.NVarChar, 200).Value = QuarantineMigrationId;
                command.Parameters.Add("@QuarantineMigrationSha256", SqlDbType.Char, 64).Value = QuarantineMigrationSha256;
                command.Parameters.Add("@PaymentsMigrationId", SqlDbType.NVarChar, 200).Value = PaymentsMigrationId;
                command.Parameters.Add("@PaymentsMigrationSha256", SqlDbType.Char, 64).Value = PaymentsMigrationSha256;
                command.Parameters.Add("@PaymentTriggerScopeMigrationId", SqlDbType.NVarChar, 200).Value = PaymentTriggerScopeMigrationId;
                command.Parameters.Add("@PaymentTriggerScopeMigrationSha256", SqlDbType.Char, 64).Value = PaymentTriggerScopeMigrationSha256;
                connection.Open();
                if (Convert.ToInt32(command.ExecuteScalar()) != 1)
                    throw new InvalidOperationException("ترحيل المرتجعات التجارية الجزئية في QA غير مكتمل أو لا يطابق البصمة والقيود المتوقعة.");
            }
        }
    }
}

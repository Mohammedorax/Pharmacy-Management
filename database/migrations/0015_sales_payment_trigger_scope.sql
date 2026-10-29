/* Restrict payment-completion enforcement to commercial sale invoices - QA/Test/Sandbox only. */
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @DatabaseName NVARCHAR(128) = LOWER(DB_NAME());
IF @DatabaseName NOT LIKE N'%[_]test' AND @DatabaseName NOT LIKE N'%[_]qa' AND @DatabaseName NOT LIKE N'%[_]sandbox'
    THROW 51000, N'يمكن تشغيل هذا الترحيل فقط على قاعدة ينتهي اسمها بـ _test أو _qa أو _sandbox.', 1;
IF OBJECT_ID(N'dbo.SalesInvoicePaymentsTb', N'U') IS NULL OR OBJECT_ID(N'dbo.trg_SalesInvoicesTb_PaymentCompletion', N'TR') IS NULL
    THROW 51000, N'يتطلب تصحيح نطاق مشغل الدفع تثبيت ترحيل 0014 أولاً.', 1;

BEGIN TRANSACTION;
BEGIN TRY
    DECLARE @LockResult INT;
    EXEC @LockResult = sys.sp_getapplock
        @Resource=N'PharmacyManagement.SalesPaymentTriggerScopeMigration',
        @LockMode=N'Exclusive', @LockOwner=N'Transaction', @LockTimeout=60000;
    IF @LockResult < 0 THROW 51000, N'تعذر الحصول على قفل تصحيح نطاق مشغل الدفع.', 1;

    EXEC(N'ALTER TRIGGER dbo.trg_SalesInvoicesTb_PaymentCompletion ON dbo.SalesInvoicesTb AFTER UPDATE AS BEGIN SET NOCOUNT ON;
IF EXISTS
(
 SELECT 1 FROM inserted i JOIN deleted d ON d.InvoiceId=i.InvoiceId
 OUTER APPLY(SELECT SUM(p.Amount) Paid FROM dbo.SalesInvoicePaymentsTb p WHERE p.InvoiceId=i.InvoiceId) x
 WHERE i.InvoiceType=N''Sale'' AND d.Status=N''Draft'' AND i.Status=N''Issued'' AND ISNULL(x.Paid,0)<>i.NetTotal
)
BEGIN RAISERROR(''Issued sales invoice payment total must equal its net total.'',16,1); ROLLBACK TRANSACTION; RETURN; END END');

    IF OBJECT_ID(N'dbo.CommercialSchemaVersionsTb',N'U') IS NOT NULL AND NOT EXISTS(SELECT 1 FROM dbo.CommercialSchemaVersionsTb WHERE VersionNo=11)
        INSERT dbo.CommercialSchemaVersionsTb(VersionNo) VALUES(11);

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

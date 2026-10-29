/* Preserve explicit imported payment method. QA/Test/Sandbox only. */
SET NOCOUNT ON;
SET XACT_ABORT ON;
DECLARE @DatabaseName NVARCHAR(128) = LOWER(DB_NAME());
IF @DatabaseName NOT LIKE N'%[_]test' AND @DatabaseName NOT LIKE N'%[_]qa' AND @DatabaseName NOT LIKE N'%[_]sandbox'
    THROW 51000, N'يُطبق ترحيل طريقة سداد الفواتير المستوردة في QA/Test/Sandbox فقط قبل قبول الإصدار.', 1;
IF OBJECT_ID(N'dbo.InvoiceImportDraftsTb', N'U') IS NULL OR COL_LENGTH(N'dbo.InvoiceImportDraftsTb', N'PartyReference') IS NULL
    THROW 51000, N'يتطلب حفظ طريقة السداد تطبيق ترحيل بيانات المسودات 0018 أولاً.', 1;
BEGIN TRANSACTION;
BEGIN TRY
    DECLARE @LockResult INT;
    EXEC @LockResult = sys.sp_getapplock @Resource=N'PharmacyManagement.InvoiceImportPaymentMigration',
        @LockMode=N'Exclusive', @LockOwner=N'Transaction', @LockTimeout=60000;
    IF @LockResult < 0 THROW 51000, N'تعذر قفل ترحيل طريقة سداد الفواتير المستوردة.', 1;
    IF COL_LENGTH(N'dbo.InvoiceImportDraftsTb', N'PaymentMethod') IS NULL
        ALTER TABLE dbo.InvoiceImportDraftsTb ADD PaymentMethod NVARCHAR(20) NULL;
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

/* Import source identity, QA/Test/Sandbox preview. Registry insert and invoice posting share one transaction. */
SET NOCOUNT ON;
SET XACT_ABORT ON;
DECLARE @DatabaseName NVARCHAR(128) = LOWER(DB_NAME());
IF @DatabaseName NOT LIKE N'%[_]test' AND @DatabaseName NOT LIKE N'%[_]qa' AND @DatabaseName NOT LIKE N'%[_]sandbox'
    THROW 51000, N'يُطبق ترحيل استيراد الفواتير في QA/Test/Sandbox فقط قبل قبول الإصدار.', 1;
BEGIN TRANSACTION;
BEGIN TRY
    DECLARE @LockResult INT;
    EXEC @LockResult = sys.sp_getapplock @Resource=N'PharmacyManagement.InvoiceImportSourceMigration',
        @LockMode=N'Exclusive', @LockOwner=N'Transaction', @LockTimeout=60000;
    IF @LockResult < 0 THROW 51000, N'تعذر قفل ترحيل مصادر الفواتير.', 1;
    IF OBJECT_ID(N'dbo.ImportedInvoiceSourceTb', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.ImportedInvoiceSourceTb (
            SourceSystem NVARCHAR(100) NOT NULL,
            InvoiceKind NVARCHAR(8) NOT NULL,
            SourceInvoiceId NVARCHAR(100) NOT NULL,
            PostedReference NVARCHAR(100) NOT NULL,
            PostedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_ImportedInvoiceSourceTb_PostedAt DEFAULT SYSUTCDATETIME(),
            CONSTRAINT PK_ImportedInvoiceSourceTb PRIMARY KEY (SourceSystem, InvoiceKind, SourceInvoiceId),
            CONSTRAINT CK_ImportedInvoiceSourceTb_Kind CHECK (InvoiceKind IN (N'Sale',N'Purchase')),
            CONSTRAINT CK_ImportedInvoiceSourceTb_Reference CHECK (LEN(PostedReference)>0)
        );
    END;
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

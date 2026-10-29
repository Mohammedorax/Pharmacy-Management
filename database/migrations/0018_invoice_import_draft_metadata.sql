/* Preserves optional invoice metadata for later review and approval. QA/Test/Sandbox only. */
SET NOCOUNT ON;
SET XACT_ABORT ON;
DECLARE @DatabaseName NVARCHAR(128) = LOWER(DB_NAME());
IF @DatabaseName NOT LIKE N'%[_]test' AND @DatabaseName NOT LIKE N'%[_]qa' AND @DatabaseName NOT LIKE N'%[_]sandbox'
    THROW 51000, N'يُطبق ترحيل بيانات استيراد الفواتير في QA/Test/Sandbox فقط قبل قبول الإصدار.', 1;
IF OBJECT_ID(N'dbo.InvoiceImportDraftsTb', N'U') IS NULL OR OBJECT_ID(N'dbo.InvoiceImportDraftLinesTb', N'U') IS NULL
    THROW 51000, N'يتطلب توسيع مسودات الفواتير تطبيق الترحيل 0017 أولاً.', 1;
BEGIN TRANSACTION;
BEGIN TRY
    DECLARE @LockResult INT;
    EXEC @LockResult = sys.sp_getapplock @Resource=N'PharmacyManagement.InvoiceImportDraftMetadataMigration',
        @LockMode=N'Exclusive', @LockOwner=N'Transaction', @LockTimeout=60000;
    IF @LockResult < 0 THROW 51000, N'تعذر قفل ترحيل بيانات مسودات الفواتير.', 1;

    IF COL_LENGTH(N'dbo.InvoiceImportDraftsTb', N'PartyReference') IS NULL
        ALTER TABLE dbo.InvoiceImportDraftsTb ADD PartyReference NVARCHAR(50) NULL;
    IF COL_LENGTH(N'dbo.InvoiceImportDraftsTb', N'PartyPhone') IS NULL
        ALTER TABLE dbo.InvoiceImportDraftsTb ADD PartyPhone NVARCHAR(50) NULL;
    IF COL_LENGTH(N'dbo.InvoiceImportDraftsTb', N'DiscountPercentText') IS NULL
        ALTER TABLE dbo.InvoiceImportDraftsTb ADD DiscountPercentText NVARCHAR(40) NULL;
    IF COL_LENGTH(N'dbo.InvoiceImportDraftsTb', N'TaxRatePercentText') IS NULL
        ALTER TABLE dbo.InvoiceImportDraftsTb ADD TaxRatePercentText NVARCHAR(40) NULL;
    IF COL_LENGTH(N'dbo.InvoiceImportDraftsTb', N'PriceTaxMode') IS NULL
        ALTER TABLE dbo.InvoiceImportDraftsTb ADD PriceTaxMode NVARCHAR(12) NULL;
    IF COL_LENGTH(N'dbo.InvoiceImportDraftsTb', N'InvoiceNotes') IS NULL
        ALTER TABLE dbo.InvoiceImportDraftsTb ADD InvoiceNotes NVARCHAR(1000) NULL;
    IF COL_LENGTH(N'dbo.InvoiceImportDraftLinesTb', N'ProductionDate') IS NULL
        ALTER TABLE dbo.InvoiceImportDraftLinesTb ADD ProductionDate DATE NULL;
    IF COL_LENGTH(N'dbo.InvoiceImportDraftLinesTb', N'ExpiryDate') IS NULL
        ALTER TABLE dbo.InvoiceImportDraftLinesTb ADD ExpiryDate DATE NULL;
    IF COL_LENGTH(N'dbo.InvoiceImportDraftLinesTb', N'BatchNumber') IS NULL
        ALTER TABLE dbo.InvoiceImportDraftLinesTb ADD BatchNumber NVARCHAR(100) NULL;
    IF COL_LENGTH(N'dbo.InvoiceImportDraftLinesTb', N'Gtin') IS NULL
        ALTER TABLE dbo.InvoiceImportDraftLinesTb ADD Gtin NVARCHAR(20) NULL;
    IF COL_LENGTH(N'dbo.InvoiceImportDraftLinesTb', N'SerialNumber') IS NULL
        ALTER TABLE dbo.InvoiceImportDraftLinesTb ADD SerialNumber NVARCHAR(100) NULL;
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

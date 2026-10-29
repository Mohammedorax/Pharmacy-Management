/*
  Commercial inclusive-price tax mode - TEST DATABASE ONLY
  Adds an immutable invoice snapshot of whether unit prices include VAT.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @DatabaseName NVARCHAR(128) = LOWER(DB_NAME());
IF @DatabaseName NOT LIKE N'%[_]test' AND @DatabaseName NOT LIKE N'%[_]qa' AND @DatabaseName NOT LIKE N'%[_]sandbox'
    THROW 51000, N'يمكن تشغيل هذا الترحيل فقط على قاعدة ينتهي اسمها بـ _test أو _qa أو _sandbox.', 1;
IF OBJECT_ID(N'dbo.SalesInvoicesTb', N'U') IS NULL
    THROW 51000, N'المخطط التجاري غير مثبت. لا يمكن إضافة سياسة تسعير لقاعدة غير مهيأة.', 1;

BEGIN TRANSACTION;
BEGIN TRY
    DECLARE @LockResult INT;
    EXEC @LockResult = sys.sp_getapplock @Resource = N'PharmacyManagement.CommercialMigrations', @LockMode = N'Exclusive', @LockOwner = N'Transaction', @LockTimeout = 60000;
    IF @LockResult < 0 THROW 51000, N'تعذر الحصول على قفل ترحيلات النظام التجاري؛ أعد المحاولة لاحقاً.', 1;

    IF COL_LENGTH(N'dbo.SalesInvoicesTb', N'PriceTaxMode') IS NULL
        ALTER TABLE dbo.SalesInvoicesTb ADD PriceTaxMode NVARCHAR(20) NOT NULL CONSTRAINT DF_SalesInvoicesTb_PriceTaxMode DEFAULT N'Exclusive' WITH VALUES;
    IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_SalesInvoicesTb_PriceTaxMode' AND parent_object_id = OBJECT_ID(N'dbo.SalesInvoicesTb'))
        ALTER TABLE dbo.SalesInvoicesTb ADD CONSTRAINT CK_SalesInvoicesTb_PriceTaxMode CHECK (PriceTaxMode IN (N'Exclusive', N'Inclusive'));

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

/*
  Commercial invoice header total equation - TEST DATABASE ONLY
  It prevents a direct SQL insert from issuing inconsistent header totals.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @DatabaseName NVARCHAR(128) = LOWER(DB_NAME());
IF @DatabaseName NOT LIKE N'%[_]test' AND @DatabaseName NOT LIKE N'%[_]qa' AND @DatabaseName NOT LIKE N'%[_]sandbox'
    THROW 51000, N'This migration may run only on a database ending in _test, _qa, or _sandbox.', 1;
IF COL_LENGTH(N'dbo.SalesInvoicesTb', N'PriceTaxMode') IS NULL
    THROW 51000, N'Apply 0005 before 0006.', 1;

BEGIN TRANSACTION;
BEGIN TRY
    DECLARE @LockResult INT;
    EXEC @LockResult = sys.sp_getapplock @Resource = N'PharmacyManagement.CommercialMigrations', @LockMode = N'Exclusive', @LockOwner = N'Transaction', @LockTimeout = 60000;
    IF @LockResult < 0 THROW 51000, N'Could not acquire the commercial migration lock.', 1;

    IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_SalesInvoicesTb_TotalEquation' AND parent_object_id = OBJECT_ID(N'dbo.SalesInvoicesTb'))
        ALTER TABLE dbo.SalesInvoicesTb ADD CONSTRAINT CK_SalesInvoicesTb_TotalEquation CHECK
        ((PriceTaxMode = N'Inclusive' AND GrossTotal - DiscountTotal = NetTotal) OR
         (PriceTaxMode = N'Exclusive' AND GrossTotal - DiscountTotal + TaxTotal = NetTotal));

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

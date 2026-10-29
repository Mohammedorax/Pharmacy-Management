/*
  Commercial migration registry - TEST DATABASE ONLY
  This migration deliberately does not enable FullCommercial or any live integration.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @DatabaseName NVARCHAR(128) = LOWER(DB_NAME());
IF @DatabaseName NOT LIKE N'%[_]test' AND @DatabaseName NOT LIKE N'%[_]qa' AND @DatabaseName NOT LIKE N'%[_]sandbox'
    THROW 51000, N'يمكن تشغيل ترحيلات النظام التجاري فقط على قاعدة ينتهي اسمها بـ _test أو _qa أو _sandbox.', 1;

IF OBJECT_ID(N'dbo.CommercialSchemaVersionsTb', N'U') IS NULL
    THROW 51000, N'المخطط التجاري غير مثبت. ثبّته أولاً على قاعدة اختبار مستعادة.', 1;

BEGIN TRANSACTION;
BEGIN TRY
    DECLARE @LockResult INT;
    EXEC @LockResult = sys.sp_getapplock
        @Resource = N'PharmacyManagement.CommercialMigrations',
        @LockMode = N'Exclusive',
        @LockOwner = N'Transaction',
        @LockTimeout = 60000;

    IF @LockResult < 0
        THROW 51000, N'تعذر الحصول على قفل ترحيلات النظام التجاري؛ أعد المحاولة بعد اكتمال العملية الأخرى.', 1;

    IF OBJECT_ID(N'dbo.CommercialMigrationRegistryTb', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.CommercialMigrationRegistryTb
        (
            MigrationId NVARCHAR(200) NOT NULL CONSTRAINT PK_CommercialMigrationRegistryTb PRIMARY KEY,
            ScriptSha256 CHAR(64) NOT NULL,
            ToolVersion NVARCHAR(40) NOT NULL,
            AppliedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_CommercialMigrationRegistryTb_AppliedAtUtc DEFAULT SYSUTCDATETIME(),
            AppliedBy NVARCHAR(256) NOT NULL,
            CONSTRAINT CK_CommercialMigrationRegistryTb_Hash CHECK (ScriptSha256 NOT LIKE '%[^0-9A-Fa-f]%')
        );
    END;

    IF NOT EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE name = N'IX_CommercialMigrationRegistryTb_AppliedAtUtc'
          AND object_id = OBJECT_ID(N'dbo.CommercialMigrationRegistryTb'))
        CREATE INDEX IX_CommercialMigrationRegistryTb_AppliedAtUtc
            ON dbo.CommercialMigrationRegistryTb(AppliedAtUtc DESC);

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

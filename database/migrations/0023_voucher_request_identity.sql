/* Durable voucher request identity. Explicit QA/Test/Sandbox migration only.
   Historical vouchers remain NULL; do not infer identities or rewrite financial rows. */
SET NOCOUNT ON;
SET XACT_ABORT ON;
DECLARE @DatabaseName NVARCHAR(128)=LOWER(DB_NAME());
IF @DatabaseName NOT LIKE N'%[_]test' AND @DatabaseName NOT LIKE N'%[_]qa' AND @DatabaseName NOT LIKE N'%[_]sandbox'
    THROW 51000,N'تُطبق هوية طلب السند على QA/Test/Sandbox فقط قبل قبول الإصدار.',1;
IF OBJECT_ID(N'dbo.AccountingVouchersTb',N'U') IS NULL
    THROW 51000,N'يتطلب ترحيل هوية السند المخطط المحاسبي الأساسي.',1;
BEGIN TRANSACTION;
BEGIN TRY
    DECLARE @LockResult INT;
    EXEC @LockResult=sys.sp_getapplock @Resource=N'PharmacyManagement.VoucherRequestIdentityMigration',
        @LockMode=N'Exclusive',@LockOwner=N'Transaction',@LockTimeout=60000;
    IF @LockResult<0 THROW 51000,N'تعذر قفل ترحيل هوية السند.',1;
    IF COL_LENGTH(N'dbo.AccountingVouchersTb',N'RequestId') IS NULL
        ALTER TABLE dbo.AccountingVouchersTb ADD RequestId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'dbo.AccountingVouchersTb',N'RequestCurrencyCode') IS NULL
        ALTER TABLE dbo.AccountingVouchersTb ADD RequestCurrencyCode NVARCHAR(10) NULL;
    IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.AccountingVouchersTb') AND name=N'UX_AccountingVouchersTb_RequestId')
        EXEC(N'CREATE UNIQUE INDEX UX_AccountingVouchersTb_RequestId ON dbo.AccountingVouchersTb(RequestId) WHERE RequestId IS NOT NULL;');
    IF EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.AccountingVouchersTb') AND name=N'UX_AccountingVouchersTb_RequestId'
        AND (is_unique=0 OR has_filter=0 OR is_disabled=1))
        THROW 51000,N'فهرس هوية السند غير صالح.',1;
    IF (SELECT COUNT(*) FROM sys.index_columns c JOIN sys.indexes i ON i.object_id=c.object_id AND i.index_id=c.index_id
        WHERE i.object_id=OBJECT_ID(N'dbo.AccountingVouchersTb') AND i.name=N'UX_AccountingVouchersTb_RequestId' AND c.key_ordinal>0)<>1
        OR NOT EXISTS(SELECT 1 FROM sys.index_columns c JOIN sys.indexes i ON i.object_id=c.object_id AND i.index_id=c.index_id
        JOIN sys.columns k ON k.object_id=c.object_id AND k.column_id=c.column_id
        WHERE i.object_id=OBJECT_ID(N'dbo.AccountingVouchersTb') AND i.name=N'UX_AccountingVouchersTb_RequestId' AND c.key_ordinal=1 AND k.name=N'RequestId')
        THROW 51000,N'مفتاح فهرس هوية السند غير صالح.',1;
    IF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.AccountingVouchersTb') AND name=N'CK_AccountingVouchersTb_RequestIdentity')
        EXEC(N'ALTER TABLE dbo.AccountingVouchersTb WITH CHECK ADD CONSTRAINT CK_AccountingVouchersTb_RequestIdentity CHECK
            ((RequestId IS NULL AND RequestCurrencyCode IS NULL) OR
            (RequestId IS NOT NULL AND RequestId<>''00000000-0000-0000-0000-000000000000'' AND RequestCurrencyCode IS NOT NULL AND LEN(RequestCurrencyCode)>0));');
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
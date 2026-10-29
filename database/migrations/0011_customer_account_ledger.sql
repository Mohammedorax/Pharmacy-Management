/* Individual customer account ledger - TEST DATABASE ONLY. */
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @DatabaseName NVARCHAR(128) = LOWER(DB_NAME());
IF @DatabaseName NOT LIKE N'%[_]test' AND @DatabaseName NOT LIKE N'%[_]qa' AND @DatabaseName NOT LIKE N'%[_]sandbox'
    THROW 51000, N'يمكن تشغيل هذا الترحيل فقط على قاعدة ينتهي اسمها بـ _test أو _qa أو _sandbox.', 1;
IF OBJECT_ID(N'dbo.CommercialReturnHeadersTb', N'U') IS NULL OR OBJECT_ID(N'dbo.JournalEntriesTb', N'U') IS NULL
    THROW 51000, N'يتطلب دفتر ذمم العميل مخطط المرتجعات والمحاسبة قبل تطبيقه.', 1;

BEGIN TRANSACTION;
BEGIN TRY
    DECLARE @LockResult INT;
    EXEC @LockResult = sys.sp_getapplock
        @Resource=N'PharmacyManagement.CustomerAccountLedgerMigration',
        @LockMode=N'Exclusive', @LockOwner=N'Transaction', @LockTimeout=60000;
    IF @LockResult < 0 THROW 51000, N'تعذر الحصول على قفل ترحيل دفتر ذمم العملاء.', 1;

    IF OBJECT_ID(N'dbo.CustomerAccountLedgerTb', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.CustomerAccountLedgerTb
        (
            LedgerEntryId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_CustomerAccountLedgerTb PRIMARY KEY,
            CustomerId NVARCHAR(50) NOT NULL,
            JournalEntryId INT NOT NULL,
            SourceType NVARCHAR(50) NOT NULL,
            SourceId NVARCHAR(50) NOT NULL,
            EntryKind NVARCHAR(30) NOT NULL,
            Debit DECIMAL(18,2) NOT NULL CONSTRAINT DF_CustomerAccountLedgerTb_Debit DEFAULT 0,
            Credit DECIMAL(18,2) NOT NULL CONSTRAINT DF_CustomerAccountLedgerTb_Credit DEFAULT 0,
            EntryDateUtc DATETIME2 NOT NULL,
            CreatedBy NVARCHAR(200) NOT NULL,
            CONSTRAINT FK_CustomerAccountLedgerTb_Customer FOREIGN KEY(CustomerId) REFERENCES dbo.CustomersBillsTb(CustomerId),
            CONSTRAINT FK_CustomerAccountLedgerTb_Journal FOREIGN KEY(JournalEntryId) REFERENCES dbo.JournalEntriesTb(JournalEntryId),
            CONSTRAINT UX_CustomerAccountLedgerTb_Source UNIQUE(SourceType,SourceId,EntryKind),
            CONSTRAINT CK_CustomerAccountLedgerTb_EntryKind CHECK(EntryKind IN(N'CreditGranted',N'CreditUsed',N'Adjustment')),
            CONSTRAINT CK_CustomerAccountLedgerTb_Amounts CHECK(Debit>=0 AND Credit>=0 AND ((Debit>0 AND Credit=0) OR (Credit>0 AND Debit=0)))
        );
    END;
    IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.CustomerAccountLedgerTb') AND name=N'IX_CustomerAccountLedgerTb_Customer_Date')
        CREATE INDEX IX_CustomerAccountLedgerTb_Customer_Date ON dbo.CustomerAccountLedgerTb(CustomerId,EntryDateUtc,LedgerEntryId);
    IF OBJECT_ID(N'dbo.trg_CustomerAccountLedgerTb_AppendOnly',N'TR') IS NULL
        EXEC(N'CREATE TRIGGER dbo.trg_CustomerAccountLedgerTb_AppendOnly ON dbo.CustomerAccountLedgerTb AFTER UPDATE,DELETE AS BEGIN SET NOCOUNT ON; RAISERROR(''Customer account ledger is append-only.'',16,1); ROLLBACK TRANSACTION; RETURN; END');
    IF OBJECT_ID(N'dbo.CommercialSchemaVersionsTb',N'U') IS NOT NULL AND NOT EXISTS(SELECT 1 FROM dbo.CommercialSchemaVersionsTb WHERE VersionNo=7)
        INSERT dbo.CommercialSchemaVersionsTb(VersionNo) VALUES(7);
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

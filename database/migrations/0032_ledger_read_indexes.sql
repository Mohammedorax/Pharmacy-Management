/* Ledger read indexes - the general ledger read drives from the date range instead of scanning
   every line of the account, which shortens the serializable range lock. */
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @DatabaseName NVARCHAR(128) = LOWER(DB_NAME());
IF @DatabaseName NOT LIKE N'%[_]test' AND @DatabaseName NOT LIKE N'%[_]qa' AND @DatabaseName NOT LIKE N'%[_]sandbox'
    THROW 51000, N'يمكن تشغيل هذا الترحيل فقط على قاعدة ينتهي اسمها بـ _test أو _qa أو _sandbox.', 1;
IF OBJECT_ID(N'dbo.JournalEntriesTb', N'U') IS NULL OR OBJECT_ID(N'dbo.JournalEntryLinesTb', N'U') IS NULL
    THROW 51000, N'يتطلب الترحيل مخطط القيود المحاسبية قبل تطبيقه.', 1;

BEGIN TRANSACTION;
BEGIN TRY
    DECLARE @LockResult INT;
    EXEC @LockResult = sys.sp_getapplock
        @Resource=N'PharmacyManagement.LedgerReadIndexesMigration',
        @LockMode=N'Exclusive', @LockOwner=N'Transaction', @LockTimeout=60000;
    IF @LockResult < 0 THROW 51000, N'تعذر الحصول على قفل ترحيل فهارس كشف الحساب.', 1;

    -- The date range of the ledger read can be answered from the entries index.
    IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.JournalEntriesTb') AND name=N'IX_JournalEntriesTb_EntryDate')
        CREATE INDEX IX_JournalEntriesTb_EntryDate ON dbo.JournalEntriesTb(EntryDate, JournalEntryId)
            INCLUDE (SourceType, SourceId, Description);
    -- The line lookup of one journal entry carries the amounts and the cost centre.
    IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.JournalEntryLinesTb') AND name=N'IX_JournalEntryLinesTb_JournalEntry_Account')
        CREATE INDEX IX_JournalEntryLinesTb_JournalEntry_Account ON dbo.JournalEntryLinesTb(JournalEntryId, AccountCode)
            INCLUDE (Debit, Credit, CostCenterId);
    -- The ledger read filters by account inside the selected period.
    IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.JournalEntryLinesTb') AND name=N'IX_JournalEntryLinesTb_Account_JournalEntry')
        CREATE INDEX IX_JournalEntryLinesTb_Account_JournalEntry ON dbo.JournalEntryLinesTb(AccountCode, JournalEntryId)
            INCLUDE (Debit, Credit, CostCenterId, JournalEntryLineId);

    IF OBJECT_ID(N'dbo.CommercialSchemaVersionsTb',N'U') IS NOT NULL AND NOT EXISTS(SELECT 1 FROM dbo.CommercialSchemaVersionsTb WHERE VersionNo=12)
        INSERT dbo.CommercialSchemaVersionsTb(VersionNo) VALUES(12);
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

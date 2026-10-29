/* Full sale cancellation inherits the immutable original cashbox attribution.
   Current cashier assignment and box activation must not rewrite history. */
SET NOCOUNT ON;
SET XACT_ABORT ON;
DECLARE @DatabaseName NVARCHAR(128) = LOWER(DB_NAME());
IF @DatabaseName NOT LIKE N'%[_]test' AND @DatabaseName NOT LIKE N'%[_]qa' AND @DatabaseName NOT LIKE N'%[_]sandbox'
    THROW 51000,N'يُطبق ربط إلغاء النقد على QA/Test/Sandbox فقط قبل قبول الإصدار.',1;
IF OBJECT_ID(N'dbo.CashBoxJournalLinksTb',N'U') IS NULL OR
   OBJECT_ID(N'dbo.trg_CashBoxJournalLinksTb_Integrity',N'TR') IS NULL
    THROW 51000,N'يتطلب ربط إلغاء النقد ترحيل 0021 أولًا.',1;

BEGIN TRANSACTION;
BEGIN TRY
    DECLARE @LockResult INT;
    EXEC @LockResult=sys.sp_getapplock @Resource=N'PharmacyManagement.CashBoxJournalLinksMigration',
        @LockMode=N'Exclusive',@LockOwner=N'Transaction',@LockTimeout=60000;
    IF @LockResult<0 THROW 51000,N'تعذر قفل ترحيل إلغاء نقد الصندوق.',1;

    IF COL_LENGTH(N'dbo.CashBoxJournalLinksTb',N'ReversesJournalEntryLineId') IS NULL
        ALTER TABLE dbo.CashBoxJournalLinksTb ADD ReversesJournalEntryLineId INT NULL;
    IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_CashBoxJournalLinksTb_ReversedLine'
        AND parent_object_id=OBJECT_ID(N'dbo.CashBoxJournalLinksTb'))
        EXEC(N'ALTER TABLE dbo.CashBoxJournalLinksTb ADD CONSTRAINT FK_CashBoxJournalLinksTb_ReversedLine
            FOREIGN KEY(ReversesJournalEntryLineId) REFERENCES dbo.CashBoxJournalLinksTb(JournalEntryLineId);');
    IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name=N'UX_CashBoxJournalLinksTb_ReversedLine'
        AND object_id=OBJECT_ID(N'dbo.CashBoxJournalLinksTb'))
        EXEC(N'CREATE UNIQUE INDEX UX_CashBoxJournalLinksTb_ReversedLine ON dbo.CashBoxJournalLinksTb(ReversesJournalEntryLineId)
            WHERE ReversesJournalEntryLineId IS NOT NULL;');

    EXEC(N'ALTER TRIGGER dbo.trg_CashBoxJournalLinksTb_Integrity ON dbo.CashBoxJournalLinksTb AFTER INSERT AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS(SELECT 1 FROM inserted i
        JOIN dbo.JournalEntryLinesTb l ON l.JournalEntryLineId=i.JournalEntryLineId
        JOIN dbo.JournalEntriesTb e ON e.JournalEntryId=l.JournalEntryId
        JOIN dbo.CashBoxesTb b ON b.CashBoxId=i.CashBoxId
        LEFT JOIN dbo.CashBoxAssignmentsTb a ON a.CashBoxId=i.CashBoxId AND a.EmpId=i.CashierEmployeeId
        WHERE i.ReversesJournalEntryLineId IS NULL AND
          (l.AccountCode<>N''1000'' OR (l.Debit=0 AND l.Credit=0) OR b.IsActive=0 OR a.EmpId IS NULL
           OR e.SourceType=N''SalesInvoiceCancellation''))
        THROW 51012,N''A new cash link requires an active assigned box; cancellation must reference its original link.'',1;

    IF EXISTS(SELECT 1 FROM inserted i
        JOIN dbo.JournalEntryLinesTb l ON l.JournalEntryLineId=i.JournalEntryLineId
        JOIN dbo.JournalEntriesTb e ON e.JournalEntryId=l.JournalEntryId
        LEFT JOIN dbo.CashBoxJournalLinksTb o ON o.JournalEntryLineId=i.ReversesJournalEntryLineId
        LEFT JOIN dbo.JournalEntryLinesTb ol ON ol.JournalEntryLineId=o.JournalEntryLineId
        LEFT JOIN dbo.JournalEntriesTb oe ON oe.JournalEntryId=ol.JournalEntryId
        WHERE i.ReversesJournalEntryLineId IS NOT NULL AND
          (o.JournalEntryLineId IS NULL OR o.ReversesJournalEntryLineId IS NOT NULL
           OR i.CashBoxId<>o.CashBoxId OR i.CashierEmployeeId<>o.CashierEmployeeId
           OR l.AccountCode<>N''1000'' OR ol.AccountCode<>N''1000'' OR (ol.Debit=0 AND ol.Credit=0)
           OR l.Debit<>ol.Credit OR l.Credit<>ol.Debit
           OR e.SourceType<>N''SalesInvoiceCancellation'' OR oe.SourceType<>N''SalesInvoice''
           OR e.SourceId IS NULL OR oe.SourceId IS NULL OR e.SourceId<>oe.SourceId))
        THROW 51014,N''Cancellation cash attribution must exactly reverse the original sale line and cashbox.'',1;
END');
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

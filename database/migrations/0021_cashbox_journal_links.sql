/* Snapshot the cashbox used for a posted cash journal line.
   Historical or unattributed lines stay outside this table and remain visible
   as unassigned in reconciliation. QA/Test/Sandbox until all cash paths pass. */
SET NOCOUNT ON;
SET XACT_ABORT ON;
DECLARE @DatabaseName NVARCHAR(128) = LOWER(DB_NAME());
IF @DatabaseName NOT LIKE N'%[_]test' AND @DatabaseName NOT LIKE N'%[_]qa' AND @DatabaseName NOT LIKE N'%[_]sandbox'
    THROW 51000, N'يُطبق ربط القيود بصناديق النقد على QA/Test/Sandbox فقط قبل قبول الإصدار.', 1;
IF OBJECT_ID(N'dbo.CashBoxesTb', N'U') IS NULL OR OBJECT_ID(N'dbo.CashBoxAssignmentsTb', N'U') IS NULL OR
   OBJECT_ID(N'dbo.JournalEntryLinesTb', N'U') IS NULL OR OBJECT_ID(N'dbo.EmployeeTb1', N'U') IS NULL
    THROW 51000, N'يتطلب ربط النقد ترحيل الصناديق وجدول سطور القيود والموظفين.', 1;

BEGIN TRANSACTION;
BEGIN TRY
    DECLARE @LockResult INT;
    EXEC @LockResult = sys.sp_getapplock @Resource=N'PharmacyManagement.CashBoxJournalLinksMigration',
        @LockMode=N'Exclusive', @LockOwner=N'Transaction', @LockTimeout=60000;
    IF @LockResult < 0 THROW 51000, N'تعذر قفل ترحيل ربط قيود الصناديق.', 1;

    IF OBJECT_ID(N'dbo.CashBoxJournalLinksTb', N'U') IS NULL
        CREATE TABLE dbo.CashBoxJournalLinksTb
        (
            JournalEntryLineId INT NOT NULL CONSTRAINT PK_CashBoxJournalLinksTb PRIMARY KEY,
            CashBoxId UNIQUEIDENTIFIER NOT NULL,
            CashierEmployeeId NVARCHAR(50) NOT NULL,
            LinkedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_CashBoxJournalLinksTb_LinkedAtUtc DEFAULT SYSUTCDATETIME(),
            CONSTRAINT FK_CashBoxJournalLinksTb_Line FOREIGN KEY(JournalEntryLineId) REFERENCES dbo.JournalEntryLinesTb(JournalEntryLineId),
            CONSTRAINT FK_CashBoxJournalLinksTb_Box FOREIGN KEY(CashBoxId) REFERENCES dbo.CashBoxesTb(CashBoxId),
            CONSTRAINT FK_CashBoxJournalLinksTb_Cashier FOREIGN KEY(CashierEmployeeId) REFERENCES dbo.EmployeeTb1(EmpId)
        );
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_CashBoxJournalLinksTb_Box' AND object_id=OBJECT_ID(N'dbo.CashBoxJournalLinksTb'))
        CREATE INDEX IX_CashBoxJournalLinksTb_Box ON dbo.CashBoxJournalLinksTb(CashBoxId,JournalEntryLineId);
    IF OBJECT_ID(N'dbo.trg_CashBoxJournalLinksTb_Integrity',N'TR') IS NULL
        EXEC(N'CREATE TRIGGER dbo.trg_CashBoxJournalLinksTb_Integrity ON dbo.CashBoxJournalLinksTb AFTER INSERT AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM inserted i
        JOIN dbo.JournalEntryLinesTb l ON l.JournalEntryLineId=i.JournalEntryLineId
        JOIN dbo.CashBoxesTb b ON b.CashBoxId=i.CashBoxId
        LEFT JOIN dbo.CashBoxAssignmentsTb a ON a.CashBoxId=i.CashBoxId AND a.EmpId=i.CashierEmployeeId
        WHERE l.AccountCode<>N''1000'' OR (l.Debit=0 AND l.Credit=0) OR b.IsActive=0 OR a.EmpId IS NULL)
        THROW 51012,N''Only an active assigned cashbox may link to a nonzero cash journal line.'',1;
END');
    IF OBJECT_ID(N'dbo.trg_CashBoxJournalLinksTb_AppendOnly',N'TR') IS NULL
        EXEC(N'CREATE TRIGGER dbo.trg_CashBoxJournalLinksTb_AppendOnly ON dbo.CashBoxJournalLinksTb AFTER UPDATE,DELETE AS
BEGIN
    SET NOCOUNT ON;
    THROW 51013,N''Cashbox journal links are append-only.'',1;
END');

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

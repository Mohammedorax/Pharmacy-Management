/* Cashbox identities and one active cashier assignment per box.
   Definitions only: historical cash journal lines remain unassigned.
   QA/Test/Sandbox until financial posting and reconciliation pass acceptance. */
SET NOCOUNT ON;
SET XACT_ABORT ON;
DECLARE @DatabaseName NVARCHAR(128) = LOWER(DB_NAME());
IF @DatabaseName NOT LIKE N'%[_]test' AND @DatabaseName NOT LIKE N'%[_]qa' AND @DatabaseName NOT LIKE N'%[_]sandbox'
    THROW 51000, N'يُطبق ترحيل تعريف صناديق النقد في QA/Test/Sandbox فقط قبل قبول الإصدار.', 1;
IF OBJECT_ID(N'dbo.EmployeeTb1', N'U') IS NULL OR OBJECT_ID(N'dbo.AuditLogTb', N'U') IS NULL
    THROW 51000, N'يتطلب تعريف الصناديق جدول الموظفين وسجل التدقيق.', 1;

BEGIN TRANSACTION;
BEGIN TRY
    DECLARE @LockResult INT;
    EXEC @LockResult = sys.sp_getapplock @Resource=N'PharmacyManagement.CashBoxDefinitionsMigration',
        @LockMode=N'Exclusive', @LockOwner=N'Transaction', @LockTimeout=60000;
    IF @LockResult < 0 THROW 51000, N'تعذر قفل ترحيل صناديق النقد.', 1;

    IF OBJECT_ID(N'dbo.CashBoxesTb', N'U') IS NULL
        CREATE TABLE dbo.CashBoxesTb
        (
            CashBoxId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_CashBoxesTb PRIMARY KEY,
            BoxCode NVARCHAR(30) NOT NULL,
            BoxName NVARCHAR(100) NOT NULL,
            IsActive BIT NOT NULL CONSTRAINT DF_CashBoxesTb_IsActive DEFAULT (1),
            CreatedAt DATETIME2(3) NOT NULL CONSTRAINT DF_CashBoxesTb_CreatedAt DEFAULT SYSUTCDATETIME(),
            UpdatedAt DATETIME2(3) NOT NULL CONSTRAINT DF_CashBoxesTb_UpdatedAt DEFAULT SYSUTCDATETIME()
        );
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'UX_CashBoxesTb_BoxCode' AND object_id=OBJECT_ID(N'dbo.CashBoxesTb'))
        CREATE UNIQUE INDEX UX_CashBoxesTb_BoxCode ON dbo.CashBoxesTb(BoxCode);

    IF OBJECT_ID(N'dbo.CashBoxAssignmentsTb', N'U') IS NULL
        CREATE TABLE dbo.CashBoxAssignmentsTb
        (
            EmpId NVARCHAR(50) NOT NULL CONSTRAINT PK_CashBoxAssignmentsTb PRIMARY KEY,
            CashBoxId UNIQUEIDENTIFIER NOT NULL,
            AssignedAt DATETIME2(3) NOT NULL CONSTRAINT DF_CashBoxAssignmentsTb_AssignedAt DEFAULT SYSUTCDATETIME(),
            AssignedBy NVARCHAR(200) NOT NULL,
            CONSTRAINT FK_CashBoxAssignmentsTb_Employee FOREIGN KEY(EmpId) REFERENCES dbo.EmployeeTb1(EmpId),
            CONSTRAINT FK_CashBoxAssignmentsTb_CashBox FOREIGN KEY(CashBoxId) REFERENCES dbo.CashBoxesTb(CashBoxId)
        );
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'UX_CashBoxAssignmentsTb_CashBoxId' AND object_id=OBJECT_ID(N'dbo.CashBoxAssignmentsTb'))
        CREATE UNIQUE INDEX UX_CashBoxAssignmentsTb_CashBoxId ON dbo.CashBoxAssignmentsTb(CashBoxId);

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

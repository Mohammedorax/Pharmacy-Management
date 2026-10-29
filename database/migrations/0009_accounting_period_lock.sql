/*
  Accounting-period lock - TEST DATABASE ONLY.
  A closed range rejects new financial/inventory documents; historical records
  are never edited by this migration.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @DatabaseName NVARCHAR(128) = LOWER(DB_NAME());
IF @DatabaseName NOT LIKE N'%[_]test' AND @DatabaseName NOT LIKE N'%[_]qa' AND @DatabaseName NOT LIKE N'%[_]sandbox'
    THROW 51000, N'يمكن تشغيل هذا الترحيل فقط على قاعدة ينتهي اسمها بـ _test أو _qa أو _sandbox.', 1;

BEGIN TRANSACTION;
BEGIN TRY
    DECLARE @LockResult INT;
    EXEC @LockResult = sys.sp_getapplock
        @Resource = N'PharmacyManagement.AccountingPeriodLock',
        @LockMode = N'Exclusive', @LockOwner = N'Transaction', @LockTimeout = 60000;
    IF @LockResult < 0
        THROW 51000, N'تعذر الحصول على قفل ترحيل الفترات المحاسبية؛ أعد المحاولة لاحقاً.', 1;

    IF OBJECT_ID(N'dbo.AccountingPeriodsTb', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.AccountingPeriodsTb (
            PeriodId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AccountingPeriodsTb PRIMARY KEY,
            StartDate DATE NOT NULL,
            EndDate DATE NOT NULL,
            Status NVARCHAR(20) NOT NULL CONSTRAINT DF_AccountingPeriodsTb_Status DEFAULT N'Open',
            ClosedAtUtc DATETIME2 NULL,
            ClosedBy NVARCHAR(200) NULL,
            Notes NVARCHAR(500) NULL,
            CONSTRAINT CK_AccountingPeriodsTb_DateRange CHECK (EndDate >= StartDate),
            CONSTRAINT CK_AccountingPeriodsTb_Status CHECK (Status IN (N'Open', N'Closed')),
            CONSTRAINT CK_AccountingPeriodsTb_CloseAudit CHECK ((Status=N'Open' AND ClosedAtUtc IS NULL AND ClosedBy IS NULL) OR (Status=N'Closed' AND ClosedAtUtc IS NOT NULL AND ClosedBy IS NOT NULL))
        );
    END;
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.AccountingPeriodsTb') AND name=N'IX_AccountingPeriodsTb_Status_DateRange')
        CREATE INDEX IX_AccountingPeriodsTb_Status_DateRange ON dbo.AccountingPeriodsTb(Status, StartDate, EndDate);

    IF OBJECT_ID(N'dbo.trg_AccountingPeriodsTb_NoOverlap', N'TR') IS NULL
        EXEC('CREATE TRIGGER dbo.trg_AccountingPeriodsTb_NoOverlap ON dbo.AccountingPeriodsTb AFTER INSERT, UPDATE AS BEGIN SET NOCOUNT ON; IF EXISTS (SELECT 1 FROM inserted i JOIN dbo.AccountingPeriodsTb p ON p.PeriodId<>i.PeriodId AND i.StartDate<=p.EndDate AND p.StartDate<=i.EndDate) BEGIN RAISERROR(''Accounting periods may not overlap.'',16,1); ROLLBACK TRANSACTION; RETURN; END END');

    IF OBJECT_ID(N'dbo.CommercialSchemaVersionsTb', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.CommercialSchemaVersionsTb WHERE VersionNo = 5)
        INSERT INTO dbo.CommercialSchemaVersionsTb (VersionNo) VALUES (5);

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

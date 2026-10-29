SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;
    IF DB_NAME() NOT LIKE N'%[_]qa' AND DB_NAME() NOT LIKE N'%[_]test' AND DB_NAME() NOT LIKE N'%[_]sandbox'
        THROW 51000, N'Quarantine resolution migration is restricted to QA/Test/Sandbox databases.', 1;
    IF OBJECT_ID(N'dbo.CommercialReturnLotAllocationsTb',N'U') IS NULL OR OBJECT_ID(N'dbo.JournalEntriesTb',N'U') IS NULL
        THROW 51000, N'Commercial return and accounting schema must be installed first.', 1;

    DECLARE @LockResult INT;
    EXEC @LockResult=sys.sp_getapplock @Resource=N'PharmacyManagement.QuarantineResolutionMigration',@LockMode=N'Exclusive',@LockOwner=N'Transaction',@LockTimeout=60000;
    IF @LockResult<0 THROW 51000,N'Unable to acquire quarantine resolution migration lock.',1;

    IF OBJECT_ID(N'dbo.QuarantineResolutionEventsTb',N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.QuarantineResolutionEventsTb
        (
            EventId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_QuarantineResolutionEventsTb PRIMARY KEY,
            RequestId NVARCHAR(100) NOT NULL,
            RequestFingerprint CHAR(64) NOT NULL,
            ReturnLotAllocationId UNIQUEIDENTIFIER NOT NULL,
            ResolutionType NVARCHAR(20) NOT NULL,
            Quantity DECIMAL(18,3) NOT NULL,
            UnitCost DECIMAL(18,2) NOT NULL,
            CostAmount DECIMAL(18,2) NOT NULL,
            Reason NVARCHAR(500) NOT NULL,
            DecidedAtUtc DATETIME2 NOT NULL,
            DecidedBy NVARCHAR(200) NOT NULL,
            JournalEntryId INT NULL,
            CONSTRAINT FK_QuarantineResolutionEventsTb_Allocation FOREIGN KEY(ReturnLotAllocationId) REFERENCES dbo.CommercialReturnLotAllocationsTb(ReturnLotAllocationId),
            CONSTRAINT FK_QuarantineResolutionEventsTb_Journal FOREIGN KEY(JournalEntryId) REFERENCES dbo.JournalEntriesTb(JournalEntryId),
            CONSTRAINT UX_QuarantineResolutionEventsTb_Request UNIQUE(RequestId),
            CONSTRAINT CK_QuarantineResolutionEventsTb_Type CHECK(ResolutionType IN(N'ReleaseToStock',N'Dispose')),
            CONSTRAINT CK_QuarantineResolutionEventsTb_Amounts CHECK(Quantity>0 AND UnitCost>=0 AND CostAmount>=0),
            CONSTRAINT CK_QuarantineResolutionEventsTb_Journal CHECK((ResolutionType=N'ReleaseToStock' AND JournalEntryId IS NULL) OR (ResolutionType=N'Dispose' AND ((CostAmount=0 AND JournalEntryId IS NULL) OR (CostAmount>0 AND JournalEntryId IS NOT NULL))))
        );
    END;
    IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.QuarantineResolutionEventsTb') AND name=N'IX_QuarantineResolutionEventsTb_Allocation_Date')
        CREATE INDEX IX_QuarantineResolutionEventsTb_Allocation_Date ON dbo.QuarantineResolutionEventsTb(ReturnLotAllocationId,DecidedAtUtc,EventId);
    IF OBJECT_ID(N'dbo.trg_QuarantineResolutionEventsTb_AppendOnly',N'TR') IS NULL
        EXEC(N'CREATE TRIGGER dbo.trg_QuarantineResolutionEventsTb_AppendOnly ON dbo.QuarantineResolutionEventsTb AFTER UPDATE,DELETE AS BEGIN SET NOCOUNT ON; RAISERROR(''Quarantine resolution events are append-only.'',16,1); ROLLBACK TRANSACTION; RETURN; END');
    IF OBJECT_ID(N'dbo.trg_QuarantineResolutionEventsTb_Integrity',N'TR') IS NULL
        EXEC(N'CREATE TRIGGER dbo.trg_QuarantineResolutionEventsTb_Integrity ON dbo.QuarantineResolutionEventsTb AFTER INSERT AS BEGIN SET NOCOUNT ON;
IF EXISTS(SELECT 1 FROM inserted i LEFT JOIN dbo.CommercialReturnLotAllocationsTb a ON a.ReturnLotAllocationId=i.ReturnLotAllocationId WHERE a.ReturnLotAllocationId IS NULL OR a.Disposition<>N''Quarantine'' OR i.UnitCost<>a.UnitCost OR i.CostAmount<>ROUND(i.Quantity*i.UnitCost,2)) BEGIN RAISERROR(''Quarantine resolution does not match its source allocation.'',16,1); ROLLBACK TRANSACTION; RETURN; END;
IF EXISTS(SELECT 1 FROM dbo.QuarantineResolutionEventsTb e JOIN dbo.CommercialReturnLotAllocationsTb a ON a.ReturnLotAllocationId=e.ReturnLotAllocationId GROUP BY e.ReturnLotAllocationId,a.Quantity HAVING SUM(e.Quantity)>MAX(a.Quantity)) BEGIN RAISERROR(''Quarantine resolution exceeds the quarantined quantity.'',16,1); ROLLBACK TRANSACTION; RETURN; END;
IF EXISTS(SELECT 1 FROM inserted i LEFT JOIN dbo.JournalEntriesTb j ON j.JournalEntryId=i.JournalEntryId WHERE i.ResolutionType=N''Dispose'' AND i.CostAmount>0 AND (j.JournalEntryId IS NULL OR j.SourceType<>N''QuarantineDisposition'' OR j.SourceId<>CONVERT(nvarchar(50),i.EventId))) BEGIN RAISERROR(''Quarantine disposal must reference its accounting entry.'',16,1); ROLLBACK TRANSACTION; RETURN; END END');
    IF OBJECT_ID(N'dbo.CommercialSchemaVersionsTb',N'U') IS NOT NULL AND NOT EXISTS(SELECT 1 FROM dbo.CommercialSchemaVersionsTb WHERE VersionNo=8)
        INSERT dbo.CommercialSchemaVersionsTb(VersionNo) VALUES(8);
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

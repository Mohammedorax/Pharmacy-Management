/*
  Auditable evidence for active-ingredient definitions - QA/Test/Sandbox only.
  Evidence content is immutable. A source can only be deactivated with an
  attributed timestamp so historical medicine decisions remain reviewable.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() NOT LIKE N'%[_]qa' AND DB_NAME() NOT LIKE N'%[_]test' AND DB_NAME() NOT LIKE N'%[_]sandbox'
    THROW 51000, N'Active ingredient evidence migration is restricted to QA/Test/Sandbox databases.', 1;
IF OBJECT_ID(N'dbo.ActiveIngredientsTb', N'U') IS NULL
    THROW 51000, N'Active ingredient schema must be installed first.', 1;

BEGIN TRANSACTION;
BEGIN TRY
    DECLARE @LockResult INT;
    EXEC @LockResult = sys.sp_getapplock
        @Resource = N'PharmacyManagement.ActiveIngredientEvidenceMigration',
        @LockMode = N'Exclusive',
        @LockOwner = N'Transaction',
        @LockTimeout = 60000;
    IF @LockResult < 0
        THROW 51000, N'Unable to acquire active ingredient evidence migration lock.', 1;

    IF OBJECT_ID(N'dbo.ActiveIngredientEvidenceTb', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.ActiveIngredientEvidenceTb
        (
            EvidenceId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ActiveIngredientEvidenceTb PRIMARY KEY,
            ActiveIngredientId UNIQUEIDENTIFIER NOT NULL,
            SourceTitle NVARCHAR(300) NOT NULL,
            Publisher NVARCHAR(200) NULL,
            SourceUrl NVARCHAR(1000) NULL,
            RetrievedOn DATE NULL,
            SourceFingerprint CHAR(64) NOT NULL,
            IsActive BIT NOT NULL CONSTRAINT DF_ActiveIngredientEvidenceTb_IsActive DEFAULT 1,
            CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_ActiveIngredientEvidenceTb_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
            CreatedBy NVARCHAR(200) NOT NULL,
            DeactivatedAtUtc DATETIME2 NULL,
            DeactivatedBy NVARCHAR(200) NULL,
            CONSTRAINT FK_ActiveIngredientEvidenceTb_Ingredient FOREIGN KEY(ActiveIngredientId) REFERENCES dbo.ActiveIngredientsTb(ActiveIngredientId),
            CONSTRAINT UX_ActiveIngredientEvidenceTb_Fingerprint UNIQUE(ActiveIngredientId, SourceFingerprint),
            CONSTRAINT CK_ActiveIngredientEvidenceTb_Title CHECK(LEN(LTRIM(RTRIM(SourceTitle))) > 0),
            CONSTRAINT CK_ActiveIngredientEvidenceTb_Fingerprint CHECK(LEN(SourceFingerprint) = 64),
            CONSTRAINT CK_ActiveIngredientEvidenceTb_CreatedBy CHECK(LEN(LTRIM(RTRIM(CreatedBy))) > 0),
            CONSTRAINT CK_ActiveIngredientEvidenceTb_Deactivation CHECK(
                (IsActive = 1 AND DeactivatedAtUtc IS NULL AND DeactivatedBy IS NULL) OR
                (IsActive = 0 AND DeactivatedAtUtc IS NOT NULL AND LEN(LTRIM(RTRIM(ISNULL(DeactivatedBy, N'')))) > 0))
        );
    END;

    IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ActiveIngredientEvidenceTb') AND name = N'IX_ActiveIngredientEvidenceTb_Ingredient_Active')
        CREATE INDEX IX_ActiveIngredientEvidenceTb_Ingredient_Active
            ON dbo.ActiveIngredientEvidenceTb(ActiveIngredientId, IsActive, CreatedAtUtc DESC);

    IF OBJECT_ID(N'dbo.trg_ActiveIngredientEvidenceTb_NoDelete', N'TR') IS NULL
        EXEC(N'CREATE TRIGGER dbo.trg_ActiveIngredientEvidenceTb_NoDelete ON dbo.ActiveIngredientEvidenceTb INSTEAD OF DELETE AS BEGIN SET NOCOUNT ON; RAISERROR(''Active ingredient evidence cannot be deleted; deactivate it instead.'',16,1); END');

    IF OBJECT_ID(N'dbo.trg_ActiveIngredientEvidenceTb_Immutable', N'TR') IS NULL
        EXEC(N'CREATE TRIGGER dbo.trg_ActiveIngredientEvidenceTb_Immutable ON dbo.ActiveIngredientEvidenceTb AFTER UPDATE AS BEGIN SET NOCOUNT ON;
IF EXISTS(SELECT 1 FROM inserted i JOIN deleted d ON d.EvidenceId=i.EvidenceId WHERE
 d.IsActive<>1 OR i.IsActive<>0 OR d.DeactivatedAtUtc IS NOT NULL OR d.DeactivatedBy IS NOT NULL OR i.DeactivatedAtUtc IS NULL OR LEN(LTRIM(RTRIM(ISNULL(i.DeactivatedBy,N''''))))=0 OR
 i.ActiveIngredientId<>d.ActiveIngredientId OR i.SourceTitle<>d.SourceTitle OR ISNULL(i.Publisher,N'''')<>ISNULL(d.Publisher,N'''') OR ISNULL(i.SourceUrl,N'''')<>ISNULL(d.SourceUrl,N'''') OR
 ISNULL(i.RetrievedOn,CONVERT(date,''19000101''))<>ISNULL(d.RetrievedOn,CONVERT(date,''19000101'')) OR i.SourceFingerprint<>d.SourceFingerprint OR i.CreatedAtUtc<>d.CreatedAtUtc OR i.CreatedBy<>d.CreatedBy)
BEGIN RAISERROR(''Active ingredient evidence content is immutable; only attributed deactivation is allowed.'',16,1); ROLLBACK TRANSACTION; RETURN; END END');

    IF OBJECT_ID(N'dbo.CommercialSchemaVersionsTb', N'U') IS NOT NULL AND NOT EXISTS(SELECT 1 FROM dbo.CommercialSchemaVersionsTb WHERE VersionNo = 9)
        INSERT dbo.CommercialSchemaVersionsTb(VersionNo) VALUES(9);

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

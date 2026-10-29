/*
  Saudi commercial feature settings - TEST DATABASE ONLY
  Requires the commercial schema installed by project_fo_3.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @DatabaseName NVARCHAR(128) = LOWER(DB_NAME());
IF @DatabaseName NOT LIKE N'%[_]test' AND @DatabaseName NOT LIKE N'%[_]qa' AND @DatabaseName NOT LIKE N'%[_]sandbox'
    THROW 51000, N'This migration may run only on a database ending in _test, _qa, or _sandbox.', 1;

IF OBJECT_ID(N'dbo.CommercialSchemaVersionsTb', N'U') IS NULL
    THROW 51000, N'Commercial schema is missing. Install it before applying this migration.', 1;

BEGIN TRANSACTION;
BEGIN TRY
    IF OBJECT_ID(N'dbo.SaudiFeatureSettingsTb', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.SaudiFeatureSettingsTb
        (
            FeatureKey NVARCHAR(80) NOT NULL CONSTRAINT PK_SaudiFeatureSettingsTb PRIMARY KEY,
            FeatureMode NVARCHAR(20) NOT NULL,
            UpdatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_SaudiFeatureSettingsTb_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
            UpdatedBy NVARCHAR(200) NOT NULL CONSTRAINT DF_SaudiFeatureSettingsTb_UpdatedBy DEFAULT N'System',
            CONSTRAINT CK_SaudiFeatureSettingsTb_Key CHECK (FeatureKey IN (N'CommercialSales', N'EInvoice', N'Rsd', N'Nphies')),
            CONSTRAINT CK_SaudiFeatureSettingsTb_Mode CHECK (FeatureMode IN (N'Disabled', N'Simulator', N'Test'))
        );
    END;

    IF OBJECT_ID(N'dbo.SaudiFeatureSettingsAuditTb', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.SaudiFeatureSettingsAuditTb
        (
            AuditId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_SaudiFeatureSettingsAuditTb PRIMARY KEY,
            FeatureKey NVARCHAR(80) NOT NULL,
            PreviousMode NVARCHAR(20) NOT NULL,
            NewMode NVARCHAR(20) NOT NULL,
            ChangeReason NVARCHAR(500) NOT NULL,
            ChangedBy NVARCHAR(200) NOT NULL,
            ChangedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_SaudiFeatureSettingsAuditTb_ChangedAtUtc DEFAULT SYSUTCDATETIME(),
            CONSTRAINT FK_SaudiFeatureSettingsAuditTb_Feature FOREIGN KEY (FeatureKey) REFERENCES dbo.SaudiFeatureSettingsTb(FeatureKey)
        );
    END;

    IF NOT EXISTS (SELECT 1 FROM dbo.SaudiFeatureSettingsTb WHERE FeatureKey = N'CommercialSales')
        INSERT INTO dbo.SaudiFeatureSettingsTb (FeatureKey, FeatureMode) VALUES (N'CommercialSales', N'Disabled');
    IF NOT EXISTS (SELECT 1 FROM dbo.SaudiFeatureSettingsTb WHERE FeatureKey = N'EInvoice')
        INSERT INTO dbo.SaudiFeatureSettingsTb (FeatureKey, FeatureMode) VALUES (N'EInvoice', N'Disabled');
    IF NOT EXISTS (SELECT 1 FROM dbo.SaudiFeatureSettingsTb WHERE FeatureKey = N'Rsd')
        INSERT INTO dbo.SaudiFeatureSettingsTb (FeatureKey, FeatureMode) VALUES (N'Rsd', N'Disabled');
    IF NOT EXISTS (SELECT 1 FROM dbo.SaudiFeatureSettingsTb WHERE FeatureKey = N'Nphies')
        INSERT INTO dbo.SaudiFeatureSettingsTb (FeatureKey, FeatureMode) VALUES (N'Nphies', N'Disabled');

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SaudiFeatureSettingsAuditTb_Feature_ChangedAtUtc' AND object_id = OBJECT_ID(N'dbo.SaudiFeatureSettingsAuditTb'))
        CREATE INDEX IX_SaudiFeatureSettingsAuditTb_Feature_ChangedAtUtc ON dbo.SaudiFeatureSettingsAuditTb(FeatureKey, ChangedAtUtc DESC);

    IF OBJECT_ID(N'dbo.trg_SaudiFeatureSettingsAuditTb_AppendOnly', N'TR') IS NULL
        EXEC(N'CREATE TRIGGER dbo.trg_SaudiFeatureSettingsAuditTb_AppendOnly ON dbo.SaudiFeatureSettingsAuditTb AFTER UPDATE, DELETE AS BEGIN SET NOCOUNT ON; RAISERROR(''Saudi feature-setting audit records are append-only.'', 16, 1); ROLLBACK TRANSACTION; RETURN; END');

    IF NOT EXISTS (SELECT 1 FROM dbo.CommercialSchemaVersionsTb WHERE VersionNo = 3)
        INSERT INTO dbo.CommercialSchemaVersionsTb (VersionNo) VALUES (3);

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

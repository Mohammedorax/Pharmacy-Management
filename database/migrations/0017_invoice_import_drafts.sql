/* Reviewed invoice import drafts. No stock, invoice or accounting posting. QA/Test/Sandbox only. */
SET NOCOUNT ON;
SET XACT_ABORT ON;
DECLARE @DatabaseName NVARCHAR(128) = LOWER(DB_NAME());
IF @DatabaseName NOT LIKE N'%[_]test' AND @DatabaseName NOT LIKE N'%[_]qa' AND @DatabaseName NOT LIKE N'%[_]sandbox'
    THROW 51000, N'يُطبق ترحيل مسودات الفواتير في QA/Test/Sandbox فقط قبل قبول الإصدار.', 1;
IF OBJECT_ID(N'dbo.ImportedInvoiceSourceTb', N'U') IS NULL
    THROW 51000, N'يتطلب حفظ مسودات الفواتير تطبيق ترحيل مصادر الاستيراد 0016 أولاً.', 1;
BEGIN TRANSACTION;
BEGIN TRY
    DECLARE @LockResult INT;
    EXEC @LockResult = sys.sp_getapplock @Resource=N'PharmacyManagement.InvoiceImportDraftMigration',
        @LockMode=N'Exclusive', @LockOwner=N'Transaction', @LockTimeout=60000;
    IF @LockResult < 0 THROW 51000, N'تعذر قفل ترحيل مسودات الفواتير.', 1;
    IF OBJECT_ID(N'dbo.InvoiceImportDraftsTb', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.InvoiceImportDraftsTb (
            DraftId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_InvoiceImportDraftsTb PRIMARY KEY,
            SourceSystem NVARCHAR(100) NOT NULL,
            InvoiceKind NVARCHAR(8) NOT NULL,
            SourceInvoiceId NVARCHAR(100) NOT NULL,
            InvoiceDate DATETIME2(7) NOT NULL,
            PartyName NVARCHAR(200) NOT NULL,
            CurrencyCode CHAR(3) NOT NULL,
            CreatedBy NVARCHAR(200) NOT NULL,
            CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_InvoiceImportDraftsTb_Created DEFAULT SYSUTCDATETIME(),
            Status NVARCHAR(12) NOT NULL CONSTRAINT DF_InvoiceImportDraftsTb_Status DEFAULT N'Draft',
            ApprovedReference NVARCHAR(100) NULL,
            ApprovedAtUtc DATETIME2(3) NULL,
            CONSTRAINT UX_InvoiceImportDraftsTb_Source UNIQUE(SourceSystem,InvoiceKind,SourceInvoiceId),
            CONSTRAINT CK_InvoiceImportDraftsTb_Kind CHECK(InvoiceKind IN(N'Sale',N'Purchase')),
            CONSTRAINT CK_InvoiceImportDraftsTb_Status CHECK(Status IN(N'Draft',N'Approved')),
            CONSTRAINT CK_InvoiceImportDraftsTb_Approval CHECK(
                (Status=N'Draft' AND ApprovedReference IS NULL AND ApprovedAtUtc IS NULL) OR
                (Status=N'Approved' AND ApprovedReference IS NOT NULL AND ApprovedAtUtc IS NOT NULL))
        );
    END;
    IF OBJECT_ID(N'dbo.InvoiceImportDraftLinesTb', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.InvoiceImportDraftLinesTb (
            DraftId UNIQUEIDENTIFIER NOT NULL,
            LineNumber INT NOT NULL,
            MedicineName NVARCHAR(200) NOT NULL,
            Barcode NVARCHAR(100) NOT NULL,
            QuantityText NVARCHAR(40) NOT NULL,
            UnitPriceText NVARCHAR(40) NOT NULL,
            CONSTRAINT PK_InvoiceImportDraftLinesTb PRIMARY KEY(DraftId,LineNumber),
            CONSTRAINT FK_InvoiceImportDraftLinesTb_Draft FOREIGN KEY(DraftId)
                REFERENCES dbo.InvoiceImportDraftsTb(DraftId),
            CONSTRAINT CK_InvoiceImportDraftLinesTb_Line CHECK(LineNumber>0)
        );
    END;
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

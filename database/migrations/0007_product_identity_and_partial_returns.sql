/*
  Product identity and mapped-legacy partial-return foundations - TEST DATABASE ONLY.
  The migration is additive: it neither edits issued invoices nor creates a
  financial, stock, or customer effect. A later service must approve returns
  atomically against the immutable snapshots created here.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @DatabaseName NVARCHAR(128) = LOWER(DB_NAME());
IF @DatabaseName NOT LIKE N'%[_]test' AND @DatabaseName NOT LIKE N'%[_]qa' AND @DatabaseName NOT LIKE N'%[_]sandbox'
    THROW 51000, N'يمكن تشغيل هذا الترحيل فقط على قاعدة ينتهي اسمها بـ _test أو _qa أو _sandbox.', 1;
IF OBJECT_ID(N'dbo.MedicineTb1', N'U') IS NULL OR OBJECT_ID(N'dbo.BillsTb1', N'U') IS NULL OR OBJECT_ID(N'dbo.LegacySalesInvoiceHeadersTb', N'U') IS NULL OR OBJECT_ID(N'dbo.LegacySalesInvoiceLinesTb', N'U') IS NULL
    THROW 51000, N'يتطلب الترحيل مخزوناً وفواتير قديمة مربوطة صراحةً قبل تطبيقه.', 1;

BEGIN TRANSACTION;
BEGIN TRY
    DECLARE @LockResult INT;
    EXEC @LockResult = sys.sp_getapplock
        @Resource = N'PharmacyManagement.ProductIdentityAndReturns',
        @LockMode = N'Exclusive',
        @LockOwner = N'Transaction',
        @LockTimeout = 60000;
    IF @LockResult < 0
        THROW 51000, N'تعذر الحصول على قفل توسعة هوية الصنف والمرتجعات؛ أعد المحاولة لاحقاً.', 1;

    IF OBJECT_ID(N'dbo.ActiveIngredientsTb', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.ActiveIngredientsTb (
            ActiveIngredientId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ActiveIngredientsTb PRIMARY KEY,
            ArabicName NVARCHAR(200) NOT NULL,
            LatinName NVARCHAR(200) NULL,
            IsActive BIT NOT NULL CONSTRAINT DF_ActiveIngredientsTb_IsActive DEFAULT 1,
            CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_ActiveIngredientsTb_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
            CONSTRAINT UX_ActiveIngredientsTb_ArabicName UNIQUE (ArabicName)
        );
    END;

    IF OBJECT_ID(N'dbo.MedicineActiveIngredientsTb', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.MedicineActiveIngredientsTb (
            MedName NVARCHAR(200) NOT NULL,
            ActiveIngredientId UNIQUEIDENTIFIER NOT NULL,
            StrengthValue DECIMAL(18,3) NULL,
            StrengthUnit NVARCHAR(30) NULL,
            IsPrimary BIT NOT NULL CONSTRAINT DF_MedicineActiveIngredientsTb_IsPrimary DEFAULT 0,
            CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_MedicineActiveIngredientsTb_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
            CONSTRAINT PK_MedicineActiveIngredientsTb PRIMARY KEY (MedName, ActiveIngredientId),
            CONSTRAINT FK_MedicineActiveIngredientsTb_Medicine FOREIGN KEY (MedName) REFERENCES dbo.MedicineTb1(MedName),
            CONSTRAINT FK_MedicineActiveIngredientsTb_Ingredient FOREIGN KEY (ActiveIngredientId) REFERENCES dbo.ActiveIngredientsTb(ActiveIngredientId),
            CONSTRAINT CK_MedicineActiveIngredientsTb_Strength CHECK ((StrengthValue IS NULL AND StrengthUnit IS NULL) OR (StrengthValue > 0 AND LEN(LTRIM(RTRIM(ISNULL(StrengthUnit, N'')))) > 0))
        );
    END;
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_MedicineActiveIngredientsTb_Primary' AND object_id = OBJECT_ID(N'dbo.MedicineActiveIngredientsTb'))
        CREATE UNIQUE INDEX UX_MedicineActiveIngredientsTb_Primary ON dbo.MedicineActiveIngredientsTb(MedName) WHERE IsPrimary = 1;
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_MedicineActiveIngredientsTb_Ingredient' AND object_id = OBJECT_ID(N'dbo.MedicineActiveIngredientsTb'))
        CREATE INDEX IX_MedicineActiveIngredientsTb_Ingredient ON dbo.MedicineActiveIngredientsTb(ActiveIngredientId, MedName);

    IF OBJECT_ID(N'dbo.SalesReturnHeadersTb', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.SalesReturnHeadersTb (
            ReturnId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_SalesReturnHeadersTb PRIMARY KEY,
            ReturnReference NVARCHAR(50) NOT NULL,
            OriginalInvoiceReference NVARCHAR(50) NOT NULL,
            RequestId UNIQUEIDENTIFIER NOT NULL,
            RequestFingerprint CHAR(64) NOT NULL,
            RefundMethod NVARCHAR(30) NOT NULL,
            ReturnReason NVARCHAR(500) NOT NULL,
            IssuedAtUtc DATETIME2 NOT NULL,
            IssuedBy NVARCHAR(200) NOT NULL,
            Notes NVARCHAR(1000) NULL,
            CONSTRAINT UX_SalesReturnHeadersTb_Reference UNIQUE (ReturnReference),
            CONSTRAINT UX_SalesReturnHeadersTb_RequestId UNIQUE (RequestId),
            CONSTRAINT FK_SalesReturnHeadersTb_OriginalInvoice FOREIGN KEY (OriginalInvoiceReference) REFERENCES dbo.LegacySalesInvoiceHeadersTb(InvoiceReference),
            CONSTRAINT CK_SalesReturnHeadersTb_RefundMethod CHECK (RefundMethod IN (N'Cash', N'Card', N'Credit', N'OriginalMethod')),
            CONSTRAINT CK_SalesReturnHeadersTb_RequestFingerprint CHECK (LEN(RequestFingerprint) = 64)
        );
    END;
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SalesReturnHeadersTb_OriginalInvoice_IssuedAtUtc' AND object_id = OBJECT_ID(N'dbo.SalesReturnHeadersTb'))
        CREATE INDEX IX_SalesReturnHeadersTb_OriginalInvoice_IssuedAtUtc ON dbo.SalesReturnHeadersTb(OriginalInvoiceReference, IssuedAtUtc DESC);

    IF OBJECT_ID(N'dbo.SalesReturnLinesTb', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.SalesReturnLinesTb (
            ReturnLineId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_SalesReturnLinesTb PRIMARY KEY,
            ReturnId UNIQUEIDENTIFIER NOT NULL,
            OriginalBillId NVARCHAR(50) NOT NULL,
            ReturnQuantity INT NOT NULL,
            UnitPrice DECIMAL(18,2) NOT NULL,
            TaxRate DECIMAL(9,4) NOT NULL,
            TaxAmount DECIMAL(18,2) NOT NULL,
            ReturnTotal DECIMAL(18,2) NOT NULL,
            UnitCost DECIMAL(18,2) NULL,
            CostTotal DECIMAL(18,2) NOT NULL,
            Disposition NVARCHAR(20) NOT NULL,
            CONSTRAINT FK_SalesReturnLinesTb_Header FOREIGN KEY (ReturnId) REFERENCES dbo.SalesReturnHeadersTb(ReturnId),
            CONSTRAINT FK_SalesReturnLinesTb_OriginalMappedBill FOREIGN KEY (OriginalBillId) REFERENCES dbo.LegacySalesInvoiceLinesTb(BillId),
            CONSTRAINT UX_SalesReturnLinesTb_ReturnOriginalBill UNIQUE (ReturnId, OriginalBillId),
            CONSTRAINT CK_SalesReturnLinesTb_Quantity CHECK (ReturnQuantity > 0),
            CONSTRAINT CK_SalesReturnLinesTb_Disposition CHECK (Disposition IN (N'Restock', N'Quarantine', N'Damaged')),
            CONSTRAINT CK_SalesReturnLinesTb_Amounts CHECK (UnitPrice >= 0 AND TaxRate >= 0 AND TaxAmount >= 0 AND ReturnTotal >= 0 AND CostTotal >= 0)
        );
    END;
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SalesReturnLinesTb_OriginalBill' AND object_id = OBJECT_ID(N'dbo.SalesReturnLinesTb'))
        CREATE INDEX IX_SalesReturnLinesTb_OriginalBill ON dbo.SalesReturnLinesTb(OriginalBillId);

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

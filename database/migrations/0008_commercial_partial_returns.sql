/*
  Commercial partial sales returns - TEST DATABASE ONLY.
  Every credit is a new immutable sales document linked to the original issued
  sale.  Values are copied from the original line snapshots; this migration
  never changes historical invoices, lot allocations, or journal entries.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @DatabaseName NVARCHAR(128) = LOWER(DB_NAME());
IF @DatabaseName NOT LIKE N'%[_]test' AND @DatabaseName NOT LIKE N'%[_]qa' AND @DatabaseName NOT LIKE N'%[_]sandbox'
    THROW 51000, N'يمكن تشغيل هذا الترحيل فقط على قاعدة ينتهي اسمها بـ _test أو _qa أو _sandbox.', 1;
IF OBJECT_ID(N'dbo.SalesInvoicesTb', N'U') IS NULL OR OBJECT_ID(N'dbo.SalesInvoiceLinesTb', N'U') IS NULL OR
   OBJECT_ID(N'dbo.LotAllocationsTb', N'U') IS NULL OR OBJECT_ID(N'dbo.InventoryLotsTb', N'U') IS NULL
    THROW 51000, N'يتطلب الترحيل مخطط البيع التجاري والدفعات قبل إضافة المرتجعات الجزئية.', 1;

BEGIN TRANSACTION;
BEGIN TRY
    DECLARE @LockResult INT;
    EXEC @LockResult = sys.sp_getapplock
        @Resource = N'PharmacyManagement.CommercialPartialReturns',
        @LockMode = N'Exclusive', @LockOwner = N'Transaction', @LockTimeout = 60000;
    IF @LockResult < 0
        THROW 51000, N'تعذر الحصول على قفل ترحيل المرتجعات التجارية؛ أعد المحاولة لاحقاً.', 1;

    IF OBJECT_ID(N'dbo.CommercialReturnHeadersTb', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.CommercialReturnHeadersTb (
            ReturnId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_CommercialReturnHeadersTb PRIMARY KEY,
            ReturnInvoiceId UNIQUEIDENTIFIER NOT NULL,
            OriginalInvoiceId UNIQUEIDENTIFIER NOT NULL,
            RequestId NVARCHAR(100) NOT NULL,
            RequestFingerprint CHAR(64) NOT NULL,
            RefundMethod NVARCHAR(30) NOT NULL,
            ReturnReason NVARCHAR(500) NOT NULL,
            IssuedAtUtc DATETIME2 NOT NULL,
            IssuedBy NVARCHAR(200) NOT NULL,
            Notes NVARCHAR(1000) NULL,
            CONSTRAINT UX_CommercialReturnHeadersTb_ReturnInvoice UNIQUE (ReturnInvoiceId),
            CONSTRAINT UX_CommercialReturnHeadersTb_Request UNIQUE (RequestId),
            CONSTRAINT FK_CommercialReturnHeadersTb_ReturnInvoice FOREIGN KEY (ReturnInvoiceId) REFERENCES dbo.SalesInvoicesTb(InvoiceId),
            CONSTRAINT FK_CommercialReturnHeadersTb_OriginalInvoice FOREIGN KEY (OriginalInvoiceId) REFERENCES dbo.SalesInvoicesTb(InvoiceId),
            CONSTRAINT CK_CommercialReturnHeadersTb_RefundMethod CHECK (RefundMethod IN (N'Cash', N'Card', N'Credit', N'OriginalMethod')),
            CONSTRAINT CK_CommercialReturnHeadersTb_Fingerprint CHECK (LEN(RequestFingerprint) = 64)
        );
    END;
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_CommercialReturnHeadersTb_OriginalInvoice_IssuedAtUtc' AND object_id=OBJECT_ID(N'dbo.CommercialReturnHeadersTb'))
        CREATE INDEX IX_CommercialReturnHeadersTb_OriginalInvoice_IssuedAtUtc ON dbo.CommercialReturnHeadersTb(OriginalInvoiceId, IssuedAtUtc DESC);

    IF OBJECT_ID(N'dbo.CommercialReturnLinesTb', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.CommercialReturnLinesTb (
            ReturnLineId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_CommercialReturnLinesTb PRIMARY KEY,
            ReturnId UNIQUEIDENTIFIER NOT NULL,
            ReturnInvoiceLineId UNIQUEIDENTIFIER NOT NULL,
            OriginalLineId UNIQUEIDENTIFIER NOT NULL,
            ReturnQuantity DECIMAL(18,3) NOT NULL,
            GrossTotal DECIMAL(18,2) NOT NULL,
            DiscountTotal DECIMAL(18,2) NOT NULL,
            TaxRate DECIMAL(9,4) NOT NULL,
            TaxTotal DECIMAL(18,2) NOT NULL,
            NetTotal DECIMAL(18,2) NOT NULL,
            UnitCost DECIMAL(18,2) NULL,
            CostTotal DECIMAL(18,2) NOT NULL,
            CONSTRAINT FK_CommercialReturnLinesTb_Header FOREIGN KEY (ReturnId) REFERENCES dbo.CommercialReturnHeadersTb(ReturnId),
            CONSTRAINT FK_CommercialReturnLinesTb_ReturnInvoiceLine FOREIGN KEY (ReturnInvoiceLineId) REFERENCES dbo.SalesInvoiceLinesTb(LineId),
            CONSTRAINT FK_CommercialReturnLinesTb_OriginalLine FOREIGN KEY (OriginalLineId) REFERENCES dbo.SalesInvoiceLinesTb(LineId),
            CONSTRAINT UX_CommercialReturnLinesTb_ReturnOriginal UNIQUE (ReturnId, OriginalLineId),
            CONSTRAINT UX_CommercialReturnLinesTb_ReturnInvoiceLine UNIQUE (ReturnInvoiceLineId),
            CONSTRAINT CK_CommercialReturnLinesTb_Quantity CHECK (ReturnQuantity > 0),
            CONSTRAINT CK_CommercialReturnLinesTb_Amounts CHECK (GrossTotal >= 0 AND DiscountTotal >= 0 AND DiscountTotal <= GrossTotal AND TaxRate >= 0 AND TaxTotal >= 0 AND NetTotal >= 0 AND CostTotal >= 0)
        );
    END;
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_CommercialReturnLinesTb_OriginalLine' AND object_id=OBJECT_ID(N'dbo.CommercialReturnLinesTb'))
        CREATE INDEX IX_CommercialReturnLinesTb_OriginalLine ON dbo.CommercialReturnLinesTb(OriginalLineId);

    IF OBJECT_ID(N'dbo.CommercialReturnLotAllocationsTb', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.CommercialReturnLotAllocationsTb (
            ReturnLotAllocationId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_CommercialReturnLotAllocationsTb PRIMARY KEY,
            ReturnLineId UNIQUEIDENTIFIER NOT NULL,
            OriginalSaleAllocationId UNIQUEIDENTIFIER NOT NULL,
            LotId UNIQUEIDENTIFIER NOT NULL,
            Quantity DECIMAL(18,3) NOT NULL,
            UnitCost DECIMAL(18,2) NOT NULL,
            Disposition NVARCHAR(20) NOT NULL,
            CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_CommercialReturnLotAllocationsTb_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
            CONSTRAINT FK_CommercialReturnLotAllocationsTb_ReturnLine FOREIGN KEY (ReturnLineId) REFERENCES dbo.CommercialReturnLinesTb(ReturnLineId),
            CONSTRAINT FK_CommercialReturnLotAllocationsTb_SaleAllocation FOREIGN KEY (OriginalSaleAllocationId) REFERENCES dbo.LotAllocationsTb(AllocationId),
            CONSTRAINT FK_CommercialReturnLotAllocationsTb_Lot FOREIGN KEY (LotId) REFERENCES dbo.InventoryLotsTb(LotId),
            CONSTRAINT UX_CommercialReturnLotAllocationsTb_ReturnLineSaleAllocation UNIQUE (ReturnLineId, OriginalSaleAllocationId),
            CONSTRAINT CK_CommercialReturnLotAllocationsTb_Quantity CHECK (Quantity > 0),
            CONSTRAINT CK_CommercialReturnLotAllocationsTb_Disposition CHECK (Disposition IN (N'Restock', N'Quarantine', N'Damaged'))
        );
    END;
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_CommercialReturnLotAllocationsTb_SaleAllocation' AND object_id=OBJECT_ID(N'dbo.CommercialReturnLotAllocationsTb'))
        CREATE INDEX IX_CommercialReturnLotAllocationsTb_SaleAllocation ON dbo.CommercialReturnLotAllocationsTb(OriginalSaleAllocationId);

    IF OBJECT_ID(N'dbo.CommercialSchemaVersionsTb', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.CommercialSchemaVersionsTb WHERE VersionNo = 4)
        INSERT INTO dbo.CommercialSchemaVersionsTb (VersionNo) VALUES (4);

    IF OBJECT_ID(N'dbo.trg_CommercialReturnHeadersTb_AppendOnly', N'TR') IS NULL
        EXEC('CREATE TRIGGER dbo.trg_CommercialReturnHeadersTb_AppendOnly ON dbo.CommercialReturnHeadersTb AFTER UPDATE, DELETE AS BEGIN SET NOCOUNT ON; RAISERROR(''Commercial returns are append-only.'', 16, 1); ROLLBACK TRANSACTION; RETURN; END');
    IF OBJECT_ID(N'dbo.trg_CommercialReturnLinesTb_AppendOnly', N'TR') IS NULL
        EXEC('CREATE TRIGGER dbo.trg_CommercialReturnLinesTb_AppendOnly ON dbo.CommercialReturnLinesTb AFTER UPDATE, DELETE AS BEGIN SET NOCOUNT ON; RAISERROR(''Commercial return lines are append-only.'', 16, 1); ROLLBACK TRANSACTION; RETURN; END');
    IF OBJECT_ID(N'dbo.trg_CommercialReturnLotAllocationsTb_AppendOnly', N'TR') IS NULL
        EXEC('CREATE TRIGGER dbo.trg_CommercialReturnLotAllocationsTb_AppendOnly ON dbo.CommercialReturnLotAllocationsTb AFTER UPDATE, DELETE AS BEGIN SET NOCOUNT ON; RAISERROR(''Commercial return lot allocations are append-only.'', 16, 1); ROLLBACK TRANSACTION; RETURN; END');

    -- لا تكفي المفاتيح الخارجية وحدها: نثبت أن الإشعار الدائن وسطره ودفعاته
    -- تتبع الفاتورة وسطر البيع المصرح بهما، حتى عند كتابة SQL مباشرةً.
    IF OBJECT_ID(N'dbo.trg_CommercialReturnHeadersTb_InvoiceLink', N'TR') IS NULL
        EXEC('CREATE TRIGGER dbo.trg_CommercialReturnHeadersTb_InvoiceLink ON dbo.CommercialReturnHeadersTb AFTER INSERT, UPDATE AS BEGIN SET NOCOUNT ON; IF EXISTS (SELECT 1 FROM inserted h LEFT JOIN dbo.SalesInvoicesTb i ON i.InvoiceId=h.ReturnInvoiceId WHERE i.InvoiceId IS NULL OR i.InvoiceType<>''CreditNote'' OR i.OriginalInvoiceId IS NULL OR i.OriginalInvoiceId<>h.OriginalInvoiceId) BEGIN RAISERROR(''Commercial return header must point to its credit note and original sale.'',16,1); ROLLBACK TRANSACTION; RETURN; END END');
    IF OBJECT_ID(N'dbo.trg_CommercialReturnLinesTb_InvoiceLink', N'TR') IS NULL
        EXEC('CREATE TRIGGER dbo.trg_CommercialReturnLinesTb_InvoiceLink ON dbo.CommercialReturnLinesTb AFTER INSERT, UPDATE AS BEGIN SET NOCOUNT ON; IF EXISTS (SELECT 1 FROM inserted x JOIN dbo.CommercialReturnHeadersTb h ON h.ReturnId=x.ReturnId LEFT JOIN dbo.SalesInvoiceLinesTb r ON r.LineId=x.ReturnInvoiceLineId LEFT JOIN dbo.SalesInvoiceLinesTb o ON o.LineId=x.OriginalLineId WHERE r.LineId IS NULL OR o.LineId IS NULL OR r.InvoiceId<>h.ReturnInvoiceId OR o.InvoiceId<>h.OriginalInvoiceId) BEGIN RAISERROR(''Commercial return line must link the original sale line to its credit-note line.'',16,1); ROLLBACK TRANSACTION; RETURN; END END');
    IF OBJECT_ID(N'dbo.trg_CommercialReturnLotAllocationsTb_SourceLink', N'TR') IS NULL
        EXEC('CREATE TRIGGER dbo.trg_CommercialReturnLotAllocationsTb_SourceLink ON dbo.CommercialReturnLotAllocationsTb AFTER INSERT, UPDATE AS BEGIN SET NOCOUNT ON; IF EXISTS (SELECT 1 FROM inserted x JOIN dbo.CommercialReturnLinesTb r ON r.ReturnLineId=x.ReturnLineId LEFT JOIN dbo.LotAllocationsTb s ON s.AllocationId=x.OriginalSaleAllocationId WHERE s.AllocationId IS NULL OR s.InvoiceLineId<>r.OriginalLineId OR s.LotId<>x.LotId OR s.MovementType<>''Sale'') BEGIN RAISERROR(''Commercial return lot must originate from the returned sale line and lot.'',16,1); ROLLBACK TRANSACTION; RETURN; END IF EXISTS (SELECT 1 FROM dbo.CommercialReturnLotAllocationsTb x JOIN dbo.LotAllocationsTb s ON s.AllocationId=x.OriginalSaleAllocationId GROUP BY x.OriginalSaleAllocationId,s.Quantity HAVING SUM(x.Quantity)>MAX(s.Quantity)) BEGIN RAISERROR(''Commercial return lot quantity exceeds the original sale allocation.'',16,1); ROLLBACK TRANSACTION; RETURN; END END');

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

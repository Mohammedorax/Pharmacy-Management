/* Auditable commercial sale payment split and customer-credit redemption - QA/Test/Sandbox only. */
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @DatabaseName NVARCHAR(128) = LOWER(DB_NAME());
IF @DatabaseName NOT LIKE N'%[_]test' AND @DatabaseName NOT LIKE N'%[_]qa' AND @DatabaseName NOT LIKE N'%[_]sandbox'
    THROW 51000, N'يمكن تشغيل هذا الترحيل فقط على قاعدة ينتهي اسمها بـ _test أو _qa أو _sandbox.', 1;
IF OBJECT_ID(N'dbo.SalesInvoicesTb', N'U') IS NULL OR OBJECT_ID(N'dbo.CustomerAccountLedgerTb', N'U') IS NULL
    THROW 51000, N'يتطلب تسجيل دفعات البيع مخطط الفواتير التجارية ودفتر ذمم العملاء.', 1;

BEGIN TRANSACTION;
BEGIN TRY
    DECLARE @LockResult INT;
    EXEC @LockResult = sys.sp_getapplock
        @Resource=N'PharmacyManagement.SalesInvoicePaymentsMigration',
        @LockMode=N'Exclusive', @LockOwner=N'Transaction', @LockTimeout=60000;
    IF @LockResult < 0 THROW 51000, N'تعذر الحصول على قفل ترحيل دفعات البيع.', 1;

    IF OBJECT_ID(N'dbo.SalesInvoicePaymentsTb', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.SalesInvoicePaymentsTb
        (
            PaymentId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_SalesInvoicePaymentsTb PRIMARY KEY,
            InvoiceId UNIQUEIDENTIFIER NOT NULL,
            PaymentMethod NVARCHAR(30) NOT NULL,
            Amount DECIMAL(18,2) NOT NULL,
            CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_SalesInvoicePaymentsTb_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
            CreatedBy NVARCHAR(200) NOT NULL,
            CONSTRAINT FK_SalesInvoicePaymentsTb_Invoice FOREIGN KEY(InvoiceId) REFERENCES dbo.SalesInvoicesTb(InvoiceId),
            CONSTRAINT UX_SalesInvoicePaymentsTb_Invoice_Method UNIQUE(InvoiceId,PaymentMethod),
            CONSTRAINT CK_SalesInvoicePaymentsTb_Method CHECK(PaymentMethod IN(N'Cash',N'CustomerCredit')),
            CONSTRAINT CK_SalesInvoicePaymentsTb_Amount CHECK(Amount>0),
            CONSTRAINT CK_SalesInvoicePaymentsTb_CreatedBy CHECK(LEN(LTRIM(RTRIM(CreatedBy)))>0)
        );
    END;

    IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.SalesInvoicePaymentsTb') AND name=N'IX_SalesInvoicePaymentsTb_Invoice')
        CREATE INDEX IX_SalesInvoicePaymentsTb_Invoice ON dbo.SalesInvoicePaymentsTb(InvoiceId,PaymentMethod) INCLUDE(Amount);

    IF OBJECT_ID(N'dbo.trg_SalesInvoicePaymentsTb_AppendOnly',N'TR') IS NULL
        EXEC(N'CREATE TRIGGER dbo.trg_SalesInvoicePaymentsTb_AppendOnly ON dbo.SalesInvoicePaymentsTb AFTER UPDATE,DELETE AS BEGIN SET NOCOUNT ON; RAISERROR(''Sales invoice payments are append-only.'',16,1); ROLLBACK TRANSACTION; RETURN; END');

    IF OBJECT_ID(N'dbo.trg_SalesInvoicePaymentsTb_Integrity',N'TR') IS NULL
        EXEC(N'CREATE TRIGGER dbo.trg_SalesInvoicePaymentsTb_Integrity ON dbo.SalesInvoicePaymentsTb AFTER INSERT AS BEGIN SET NOCOUNT ON;
IF EXISTS
(
 SELECT 1 FROM inserted i JOIN dbo.SalesInvoicesTb h ON h.InvoiceId=i.InvoiceId
 WHERE h.InvoiceType<>N''Sale'' OR h.Status<>N''Draft'' OR (i.PaymentMethod=N''CustomerCredit'' AND h.CustomerId IS NULL)
)
OR EXISTS
(
 SELECT 1 FROM dbo.SalesInvoicePaymentsTb p JOIN inserted i ON i.InvoiceId=p.InvoiceId
 JOIN dbo.SalesInvoicesTb h ON h.InvoiceId=p.InvoiceId
 GROUP BY p.InvoiceId,h.NetTotal HAVING SUM(p.Amount)>h.NetTotal
)
BEGIN RAISERROR(''Invalid sales invoice payment allocation.'',16,1); ROLLBACK TRANSACTION; RETURN; END END');

    IF OBJECT_ID(N'dbo.trg_SalesInvoicesTb_PaymentCompletion',N'TR') IS NULL
        EXEC(N'CREATE TRIGGER dbo.trg_SalesInvoicesTb_PaymentCompletion ON dbo.SalesInvoicesTb AFTER UPDATE AS BEGIN SET NOCOUNT ON;
IF EXISTS
(
 SELECT 1 FROM inserted i JOIN deleted d ON d.InvoiceId=i.InvoiceId
 OUTER APPLY(SELECT SUM(p.Amount) Paid FROM dbo.SalesInvoicePaymentsTb p WHERE p.InvoiceId=i.InvoiceId) x
 WHERE d.Status=N''Draft'' AND i.Status=N''Issued'' AND ISNULL(x.Paid,0)<>i.NetTotal
)
BEGIN RAISERROR(''Issued sales invoice payment total must equal its net total.'',16,1); ROLLBACK TRANSACTION; RETURN; END END');

    IF OBJECT_ID(N'dbo.CommercialSchemaVersionsTb',N'U') IS NOT NULL AND NOT EXISTS(SELECT 1 FROM dbo.CommercialSchemaVersionsTb WHERE VersionNo=10)
        INSERT dbo.CommercialSchemaVersionsTb(VersionNo) VALUES(10);

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

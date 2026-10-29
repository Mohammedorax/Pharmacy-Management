/*
    بيانات عرض وتجربة لقاعدة PharmacyManagement_qa فقط.
    لا يُشغّل على قاعدة التشغيل ولا ينفّذ أي DELETE أو UPDATE.
    جميع المعرفات التجريبية تبدأ بـ QA- لتسهيل التعرف عليها لاحقاً.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'PharmacyManagement_qa'
BEGIN
    THROW 51000, N'هذا السكربت مخصص حصراً لقاعدة PharmacyManagement_qa.', 1;
END;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.CompanyTb1', N'U') IS NULL
       OR OBJECT_ID(N'dbo.MedicineTb1', N'U') IS NULL
       OR OBJECT_ID(N'dbo.PurchasesTb', N'U') IS NULL
       OR OBJECT_ID(N'dbo.PurchaseDetailsTb', N'U') IS NULL
       OR OBJECT_ID(N'dbo.BillsTb1', N'U') IS NULL
       OR OBJECT_ID(N'dbo.CustomersBillsTb', N'U') IS NULL
       OR OBJECT_ID(N'dbo.InventoryMovementsTb', N'U') IS NULL
       OR OBJECT_ID(N'dbo.AccountsTb', N'U') IS NULL
       OR OBJECT_ID(N'dbo.JournalEntriesTb', N'U') IS NULL
       OR OBJECT_ID(N'dbo.JournalEntryLinesTb', N'U') IS NULL
       OR OBJECT_ID(N'dbo.TaxSettingsTb', N'U') IS NULL
    BEGIN
        THROW 51001, N'مخطط قاعدة الاختبار غير مكتمل. شغّل تهيئة المخطط قبل تعبئة البيانات.', 1;
    END;

    IF EXISTS
    (
        SELECT 1
        FROM (VALUES (N'1000'), (N'1200'), (N'1210'), (N'2100'), (N'4000'), (N'5000')) required(AccountCode)
        WHERE NOT EXISTS
        (
            SELECT 1
            FROM dbo.AccountsTb accountItem
            WHERE accountItem.AccountCode = required.AccountCode
              AND accountItem.IsActive = 1
        )
    )
    BEGIN
        THROW 51002, N'دليل الحسابات في قاعدة الاختبار غير مكتمل أو يحتوي حساباً غير مفعل.', 1;
    END;

    INSERT INTO dbo.CompanyTb1 (CompId, CompName, CompPhone, CompAddress)
    SELECT source.CompId, source.CompName, source.CompPhone, source.CompAddress
    FROM (VALUES
        (N'QA-COMP-001', N'شركة الشفاء للصناعات الدوائية', N'920000101', N'الرياض - حي العليا'),
        (N'QA-COMP-002', N'مؤسسة الحياة الطبية', N'920000202', N'جدة - حي الروضة'),
        (N'QA-COMP-003', N'شركة الرعاية الصحية', N'920000303', N'الدمام - الفيصلية'),
        (N'QA-COMP-004', N'مختبرات الأمان الدوائية', N'920000404', N'مكة المكرمة - العزيزية')
    ) AS source(CompId, CompName, CompPhone, CompAddress)
    WHERE NOT EXISTS (SELECT 1 FROM dbo.CompanyTb1 target WHERE target.CompId = source.CompId);

    INSERT INTO dbo.MedicineTb1 (MedName, Barcode, Bprice, Sprice, MedQty, ExpDate, CompName, ProdDate)
    SELECT source.MedName, source.Barcode, source.Bprice, source.Sprice, source.MedQty,
           source.ExpDate, source.CompName, source.ProdDate
    FROM (VALUES
        (N'باراسيتامول 500 ملغ', N'6281100000011', 6, 10, 106, DATEADD(MONTH, 18, CAST(GETDATE() AS DATE)), N'شركة الشفاء للصناعات الدوائية', DATEADD(MONTH, -6, CAST(GETDATE() AS DATE))),
        (N'إيبوبروفين 400 ملغ', N'6281100000028', 9, 15, 75, DATEADD(MONTH, 16, CAST(GETDATE() AS DATE)), N'شركة الشفاء للصناعات الدوائية', DATEADD(MONTH, -5, CAST(GETDATE() AS DATE))),
        (N'أموكسيسيلين 500 ملغ', N'6281100000035', 18, 28, 62, DATEADD(MONTH, 12, CAST(GETDATE() AS DATE)), N'مؤسسة الحياة الطبية', DATEADD(MONTH, -4, CAST(GETDATE() AS DATE))),
        (N'أوميبرازول 20 ملغ', N'6281100000042', 14, 23, 44, DATEADD(DAY, 21, CAST(GETDATE() AS DATE)), N'مؤسسة الحياة الطبية', DATEADD(MONTH, -7, CAST(GETDATE() AS DATE))),
        (N'فيتامين سي 1000 ملغ', N'6281100000059', 12, 20, 54, DATEADD(MONTH, 14, CAST(GETDATE() AS DATE)), N'شركة الرعاية الصحية', DATEADD(MONTH, -3, CAST(GETDATE() AS DATE))),
        (N'لوراتادين 10 ملغ', N'6281100000066', 11, 18, 44, DATEADD(MONTH, 15, CAST(GETDATE() AS DATE)), N'شركة الرعاية الصحية', DATEADD(MONTH, -4, CAST(GETDATE() AS DATE))),
        (N'أزيثروميسين 250 ملغ', N'6281100000073', 26, 39, 7, DATEADD(MONTH, 7, CAST(GETDATE() AS DATE)), N'مختبرات الأمان الدوائية', DATEADD(MONTH, -2, CAST(GETDATE() AS DATE))),
        (N'محلول ملحي 0.9% 500 مل', N'6281100000080', 5, 9, 62, DATEADD(MONTH, 20, CAST(GETDATE() AS DATE)), N'مختبرات الأمان الدوائية', DATEADD(MONTH, -1, CAST(GETDATE() AS DATE)))
    ) AS source(MedName, Barcode, Bprice, Sprice, MedQty, ExpDate, CompName, ProdDate)
    WHERE NOT EXISTS (SELECT 1 FROM dbo.MedicineTb1 target WHERE target.MedName = source.MedName);

    INSERT INTO dbo.PurchasesTb (PurchaseId, SupplierName, PurchaseDate, TotalAmount, EmployeeName, Notes)
    SELECT source.PurchaseId, source.SupplierName, source.PurchaseDate, source.TotalAmount, source.EmployeeName, source.Notes
    FROM (VALUES
        (N'QA-PUR-20260807-001', N'شركة الشفاء للصناعات الدوائية', DATEADD(DAY, -12, GETDATE()), CAST(1440.00 AS decimal(18,2)), N'مدير الاختبار', N'توريد تجريبي - أصناف مسكنات'),
        (N'QA-PUR-20260807-002', N'مؤسسة الحياة الطبية', DATEADD(DAY, -10, GETDATE()), CAST(1960.00 AS decimal(18,2)), N'مدير الاختبار', N'توريد تجريبي - أدوية الجهاز الهضمي والمضادات'),
        (N'QA-PUR-20260807-003', N'شركة الرعاية الصحية', DATEADD(DAY, -8, GETDATE()), CAST(1380.00 AS decimal(18,2)), N'مدير الاختبار', N'توريد تجريبي - حساسية وفيتامينات'),
        (N'QA-PUR-20260807-004', N'مختبرات الأمان الدوائية', DATEADD(DAY, -6, GETDATE()), CAST(954.00 AS decimal(18,2)), N'مدير الاختبار', N'توريد تجريبي - محاليل ومضادات')
    ) AS source(PurchaseId, SupplierName, PurchaseDate, TotalAmount, EmployeeName, Notes)
    WHERE NOT EXISTS (SELECT 1 FROM dbo.PurchasesTb target WHERE target.PurchaseId = source.PurchaseId);

    INSERT INTO dbo.PurchaseDetailsTb (DetailId, PurchaseId, MedName, BuyingPrice, Quantity, TotalAmount, ProdDate, ExpDate)
    SELECT source.DetailId, source.PurchaseId, source.MedName, source.BuyingPrice, source.Quantity,
           source.TotalAmount, source.ProdDate, source.ExpDate
    FROM (VALUES
        (N'QA-PD-001', N'QA-PUR-20260807-001', N'باراسيتامول 500 ملغ', 6, 120, 720, DATEADD(MONTH, -6, CAST(GETDATE() AS DATE)), DATEADD(MONTH, 18, CAST(GETDATE() AS DATE))),
        (N'QA-PD-002', N'QA-PUR-20260807-001', N'إيبوبروفين 400 ملغ', 9, 80, 720, DATEADD(MONTH, -5, CAST(GETDATE() AS DATE)), DATEADD(MONTH, 16, CAST(GETDATE() AS DATE))),
        (N'QA-PD-003', N'QA-PUR-20260807-002', N'أموكسيسيلين 500 ملغ', 18, 70, 1260, DATEADD(MONTH, -4, CAST(GETDATE() AS DATE)), DATEADD(MONTH, 12, CAST(GETDATE() AS DATE))),
        (N'QA-PD-004', N'QA-PUR-20260807-002', N'أوميبرازول 20 ملغ', 14, 50, 700, DATEADD(MONTH, -7, CAST(GETDATE() AS DATE)), DATEADD(DAY, 21, CAST(GETDATE() AS DATE))),
        (N'QA-PD-005', N'QA-PUR-20260807-003', N'فيتامين سي 1000 ملغ', 12, 60, 720, DATEADD(MONTH, -3, CAST(GETDATE() AS DATE)), DATEADD(MONTH, 14, CAST(GETDATE() AS DATE))),
        (N'QA-PD-006', N'QA-PUR-20260807-003', N'لوراتادين 10 ملغ', 11, 60, 660, DATEADD(MONTH, -4, CAST(GETDATE() AS DATE)), DATEADD(MONTH, 15, CAST(GETDATE() AS DATE))),
        (N'QA-PD-007', N'QA-PUR-20260807-004', N'أزيثروميسين 250 ملغ', 26, 24, 624, DATEADD(MONTH, -2, CAST(GETDATE() AS DATE)), DATEADD(MONTH, 7, CAST(GETDATE() AS DATE))),
        (N'QA-PD-008', N'QA-PUR-20260807-004', N'محلول ملحي 0.9% 500 مل', 5, 66, 330, DATEADD(MONTH, -1, CAST(GETDATE() AS DATE)), DATEADD(MONTH, 20, CAST(GETDATE() AS DATE)))
    ) AS source(DetailId, PurchaseId, MedName, BuyingPrice, Quantity, TotalAmount, ProdDate, ExpDate)
    WHERE NOT EXISTS (SELECT 1 FROM dbo.PurchaseDetailsTb target WHERE target.DetailId = source.DetailId);

    INSERT INTO dbo.CustomersBillsTb (CustomerId, CustomerName, PhoneNumber, CustomerAddress, TotalSpent, InvoiceDate, EmployeeName, InvoiceNotes)
    SELECT source.CustomerId, source.CustomerName, source.PhoneNumber, source.CustomerAddress,
           source.TotalSpent, source.InvoiceDate, source.EmployeeName, source.InvoiceNotes
    FROM (VALUES
        (N'QA-CUST-001', N'أحمد علي', N'0500001001', N'الرياض - النزهة', CAST(436.00 AS decimal(18,2)), DATEADD(DAY, -3, GETDATE()), N'مدير الاختبار', N'عميل تجريبي'),
        (N'QA-CUST-002', N'سارة محمد', N'0500001002', N'جدة - السلامة', CAST(259.00 AS decimal(18,2)), DATEADD(DAY, -2, GETDATE()), N'مدير الاختبار', N'عميل تجريبي'),
        (N'QA-CUST-003', N'عميل نقدي', N'0000000000', N'غير محدد', CAST(989.00 AS decimal(18,2)), DATEADD(DAY, -1, GETDATE()), N'مدير الاختبار', N'فاتورة نقدية تجريبية')
    ) AS source(CustomerId, CustomerName, PhoneNumber, CustomerAddress, TotalSpent, InvoiceDate, EmployeeName, InvoiceNotes)
    WHERE NOT EXISTS (SELECT 1 FROM dbo.CustomersBillsTb target WHERE target.CustomerId = source.CustomerId);

    INSERT INTO dbo.BillsTb1 (BillId, MedName, Sprice, Quantity, TotalAmount, CustomerId)
    SELECT source.BillId, source.MedName, source.Sprice, source.Quantity, source.TotalAmount, source.CustomerId
    FROM (VALUES
        (N'QA-SALE-001', N'باراسيتامول 500 ملغ', 10, 12, 120, N'QA-CUST-001'),
        (N'QA-SALE-001', N'أوميبرازول 20 ملغ', 23, 6, 138, N'QA-CUST-001'),
        (N'QA-SALE-001', N'فيتامين سي 1000 ملغ', 20, 5, 100, N'QA-CUST-001'),
        (N'QA-SALE-001', N'إيبوبروفين 400 ملغ', 15, 4, 60, N'QA-CUST-001'),
        (N'QA-SALE-001', N'لوراتادين 10 ملغ', 18, 1, 18, N'QA-CUST-001'),
        (N'QA-SALE-002', N'أموكسيسيلين 500 ملغ', 28, 8, 224, N'QA-CUST-002'),
        (N'QA-SALE-002', N'باراسيتامول 500 ملغ', 10, 2, 20, N'QA-CUST-002'),
        (N'QA-SALE-002', N'إيبوبروفين 400 ملغ', 15, 1, 15, N'QA-CUST-002'),
        (N'QA-SALE-003', N'أزيثروميسين 250 ملغ', 39, 17, 663, N'QA-CUST-003'),
        (N'QA-SALE-003', N'محلول ملحي 0.9% 500 مل', 9, 4, 36, N'QA-CUST-003'),
        (N'QA-SALE-003', N'لوراتادين 10 ملغ', 18, 15, 270, N'QA-CUST-003'),
        (N'QA-SALE-003', N'فيتامين سي 1000 ملغ', 20, 1, 20, N'QA-CUST-003')
    ) AS source(BillId, MedName, Sprice, Quantity, TotalAmount, CustomerId)
    WHERE NOT EXISTS
    (
        SELECT 1 FROM dbo.BillsTb1 target
        WHERE target.BillId = source.BillId AND target.MedName = source.MedName
    );

    INSERT INTO dbo.InventoryMovementsTb
        (MedName, QuantityDelta, QuantityBefore, QuantityAfter, SourceType, SourceId, UnitCost, UnitPrice, Notes, CreatedBy, CreatedAt)
    SELECT source.MedName, source.QuantityDelta, source.QuantityBefore, source.QuantityAfter,
           source.SourceType, source.SourceId, source.UnitCost, source.UnitPrice,
           source.Notes, source.CreatedBy, source.CreatedAt
    FROM (VALUES
        (N'باراسيتامول 500 ملغ', 120, 0, 120, N'PurchaseInvoice', N'QA-PUR-20260807-001', CAST(6.00 AS decimal(18,2)), NULL, N'توريد تجريبي', N'مدير الاختبار', DATEADD(DAY, -12, GETDATE())),
        (N'إيبوبروفين 400 ملغ', 80, 0, 80, N'PurchaseInvoice', N'QA-PUR-20260807-001', CAST(9.00 AS decimal(18,2)), NULL, N'توريد تجريبي', N'مدير الاختبار', DATEADD(DAY, -12, GETDATE())),
        (N'أموكسيسيلين 500 ملغ', 70, 0, 70, N'PurchaseInvoice', N'QA-PUR-20260807-002', CAST(18.00 AS decimal(18,2)), NULL, N'توريد تجريبي', N'مدير الاختبار', DATEADD(DAY, -10, GETDATE())),
        (N'أوميبرازول 20 ملغ', 50, 0, 50, N'PurchaseInvoice', N'QA-PUR-20260807-002', CAST(14.00 AS decimal(18,2)), NULL, N'توريد تجريبي', N'مدير الاختبار', DATEADD(DAY, -10, GETDATE())),
        (N'فيتامين سي 1000 ملغ', 60, 0, 60, N'PurchaseInvoice', N'QA-PUR-20260807-003', CAST(12.00 AS decimal(18,2)), NULL, N'توريد تجريبي', N'مدير الاختبار', DATEADD(DAY, -8, GETDATE())),
        (N'لوراتادين 10 ملغ', 60, 0, 60, N'PurchaseInvoice', N'QA-PUR-20260807-003', CAST(11.00 AS decimal(18,2)), NULL, N'توريد تجريبي', N'مدير الاختبار', DATEADD(DAY, -8, GETDATE())),
        (N'أزيثروميسين 250 ملغ', 24, 0, 24, N'PurchaseInvoice', N'QA-PUR-20260807-004', CAST(26.00 AS decimal(18,2)), NULL, N'توريد تجريبي', N'مدير الاختبار', DATEADD(DAY, -6, GETDATE())),
        (N'محلول ملحي 0.9% 500 مل', 66, 0, 66, N'PurchaseInvoice', N'QA-PUR-20260807-004', CAST(5.00 AS decimal(18,2)), NULL, N'توريد تجريبي', N'مدير الاختبار', DATEADD(DAY, -6, GETDATE())),
        (N'باراسيتامول 500 ملغ', -12, 120, 108, N'SalesInvoice', N'QA-SALE-001', CAST(6.00 AS decimal(18,2)), CAST(10.00 AS decimal(18,2)), N'مبيعات تجريبية', N'مدير الاختبار', DATEADD(DAY, -3, GETDATE())),
        (N'أوميبرازول 20 ملغ', -6, 50, 44, N'SalesInvoice', N'QA-SALE-001', CAST(14.00 AS decimal(18,2)), CAST(23.00 AS decimal(18,2)), N'مبيعات تجريبية', N'مدير الاختبار', DATEADD(DAY, -3, GETDATE())),
        (N'فيتامين سي 1000 ملغ', -5, 60, 55, N'SalesInvoice', N'QA-SALE-001', CAST(12.00 AS decimal(18,2)), CAST(20.00 AS decimal(18,2)), N'مبيعات تجريبية', N'مدير الاختبار', DATEADD(DAY, -3, GETDATE())),
        (N'إيبوبروفين 400 ملغ', -4, 80, 76, N'SalesInvoice', N'QA-SALE-001', CAST(9.00 AS decimal(18,2)), CAST(15.00 AS decimal(18,2)), N'مبيعات تجريبية', N'مدير الاختبار', DATEADD(DAY, -3, GETDATE())),
        (N'لوراتادين 10 ملغ', -1, 60, 59, N'SalesInvoice', N'QA-SALE-001', CAST(11.00 AS decimal(18,2)), CAST(18.00 AS decimal(18,2)), N'مبيعات تجريبية', N'مدير الاختبار', DATEADD(DAY, -3, GETDATE())),
        (N'أموكسيسيلين 500 ملغ', -8, 70, 62, N'SalesInvoice', N'QA-SALE-002', CAST(18.00 AS decimal(18,2)), CAST(28.00 AS decimal(18,2)), N'مبيعات تجريبية', N'مدير الاختبار', DATEADD(DAY, -2, GETDATE())),
        (N'باراسيتامول 500 ملغ', -2, 108, 106, N'SalesInvoice', N'QA-SALE-002', CAST(6.00 AS decimal(18,2)), CAST(10.00 AS decimal(18,2)), N'مبيعات تجريبية', N'مدير الاختبار', DATEADD(DAY, -2, GETDATE())),
        (N'إيبوبروفين 400 ملغ', -1, 76, 75, N'SalesInvoice', N'QA-SALE-002', CAST(9.00 AS decimal(18,2)), CAST(15.00 AS decimal(18,2)), N'مبيعات تجريبية', N'مدير الاختبار', DATEADD(DAY, -2, GETDATE())),
        (N'أزيثروميسين 250 ملغ', -17, 24, 7, N'SalesInvoice', N'QA-SALE-003', CAST(26.00 AS decimal(18,2)), CAST(39.00 AS decimal(18,2)), N'مبيعات تجريبية', N'مدير الاختبار', DATEADD(DAY, -1, GETDATE())),
        (N'محلول ملحي 0.9% 500 مل', -4, 66, 62, N'SalesInvoice', N'QA-SALE-003', CAST(5.00 AS decimal(18,2)), CAST(9.00 AS decimal(18,2)), N'مبيعات تجريبية', N'مدير الاختبار', DATEADD(DAY, -1, GETDATE())),
        (N'لوراتادين 10 ملغ', -15, 59, 44, N'SalesInvoice', N'QA-SALE-003', CAST(11.00 AS decimal(18,2)), CAST(18.00 AS decimal(18,2)), N'مبيعات تجريبية', N'مدير الاختبار', DATEADD(DAY, -1, GETDATE())),
        (N'فيتامين سي 1000 ملغ', -1, 55, 54, N'SalesInvoice', N'QA-SALE-003', CAST(12.00 AS decimal(18,2)), CAST(20.00 AS decimal(18,2)), N'مبيعات تجريبية', N'مدير الاختبار', DATEADD(DAY, -1, GETDATE()))
    ) AS source(MedName, QuantityDelta, QuantityBefore, QuantityAfter, SourceType, SourceId, UnitCost, UnitPrice, Notes, CreatedBy, CreatedAt)
    WHERE NOT EXISTS
    (
        SELECT 1 FROM dbo.InventoryMovementsTb target
        WHERE target.SourceType = source.SourceType
          AND target.SourceId = source.SourceId
          AND target.MedName = source.MedName
    );

    /*
       تجعل هذه القيود بيانات QA صالحة لتجربة شاشة المحاسبة أيضاً. تستخدم نفس
       الحسابات ومعادلة الضريبة التي يستخدمها AccountingService، ولا تنشئ قيداً
       ثانياً عند إعادة تشغيل السكربت بفضل SourceType و SourceId.
    */
    DECLARE @EffectiveTaxRate DECIMAL(9, 2) = ISNULL(
        (SELECT TOP 1 CASE WHEN IsEnabled = 1 THEN TaxRate ELSE 0 END
         FROM dbo.TaxSettingsTb
         ORDER BY TaxSettingId),
        0);

    DECLARE @SeedOperations TABLE
    (
        SourceType NVARCHAR(50) NOT NULL,
        SourceId NVARCHAR(50) NOT NULL,
        EntryDate DATETIME NOT NULL,
        TotalAmount DECIMAL(18, 2) NOT NULL,
        CostOfGoods DECIMAL(18, 2) NOT NULL,
        Description NVARCHAR(500) NOT NULL,
        PRIMARY KEY (SourceType, SourceId)
    );

    INSERT INTO @SeedOperations (SourceType, SourceId, EntryDate, TotalAmount, CostOfGoods, Description)
    SELECT N'PurchaseInvoice', purchaseItem.PurchaseId, purchaseItem.PurchaseDate,
           purchaseItem.TotalAmount, 0, N'فاتورة شراء تجريبية - ' + purchaseItem.PurchaseId
    FROM dbo.PurchasesTb purchaseItem
    WHERE purchaseItem.PurchaseId LIKE N'QA-PUR-%'

    UNION ALL

    SELECT N'SalesInvoice', saleItem.BillId, ISNULL(MAX(customerItem.InvoiceDate), GETDATE()),
           CAST(SUM(saleItem.TotalAmount) AS DECIMAL(18, 2)),
           CAST(SUM(CAST(saleItem.Quantity AS DECIMAL(18, 2)) * ISNULL(medicineItem.Bprice, 0)) AS DECIMAL(18, 2)),
           N'فاتورة مبيعات تجريبية - ' + saleItem.BillId
    FROM dbo.BillsTb1 saleItem
    LEFT JOIN dbo.CustomersBillsTb customerItem ON customerItem.CustomerId = saleItem.CustomerId
    LEFT JOIN dbo.MedicineTb1 medicineItem ON medicineItem.MedName = saleItem.MedName
    WHERE saleItem.BillId LIKE N'QA-SALE-%'
    GROUP BY saleItem.BillId;

    DECLARE @MissingSeedOperations TABLE
    (
        SourceType NVARCHAR(50) NOT NULL,
        SourceId NVARCHAR(50) NOT NULL,
        EntryDate DATETIME NOT NULL,
        TotalAmount DECIMAL(18, 2) NOT NULL,
        CostOfGoods DECIMAL(18, 2) NOT NULL,
        TaxAmount DECIMAL(18, 2) NOT NULL,
        Description NVARCHAR(500) NOT NULL,
        PRIMARY KEY (SourceType, SourceId)
    );

    INSERT INTO @MissingSeedOperations
        (SourceType, SourceId, EntryDate, TotalAmount, CostOfGoods, TaxAmount, Description)
    SELECT sourceItem.SourceType, sourceItem.SourceId, sourceItem.EntryDate,
           sourceItem.TotalAmount, sourceItem.CostOfGoods,
           CAST(ROUND(sourceItem.TotalAmount * @EffectiveTaxRate / (100 + @EffectiveTaxRate), 2) AS DECIMAL(18, 2)),
           sourceItem.Description
    FROM @SeedOperations sourceItem
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM dbo.JournalEntriesTb existingEntry WITH (UPDLOCK, HOLDLOCK)
        WHERE existingEntry.SourceType = sourceItem.SourceType
          AND existingEntry.SourceId = sourceItem.SourceId
    );

    INSERT INTO dbo.JournalEntriesTb (EntryDate, SourceType, SourceId, Description, CreatedBy)
    SELECT EntryDate, SourceType, SourceId, Description, N'مدير الاختبار'
    FROM @MissingSeedOperations;

    DECLARE @NewJournalEntries TABLE
    (
        JournalEntryId INT NOT NULL PRIMARY KEY,
        SourceType NVARCHAR(50) NOT NULL,
        SourceId NVARCHAR(50) NOT NULL,
        TotalAmount DECIMAL(18, 2) NOT NULL,
        CostOfGoods DECIMAL(18, 2) NOT NULL,
        TaxAmount DECIMAL(18, 2) NOT NULL,
        Description NVARCHAR(500) NOT NULL
    );

    INSERT INTO @NewJournalEntries
        (JournalEntryId, SourceType, SourceId, TotalAmount, CostOfGoods, TaxAmount, Description)
    SELECT entryItem.JournalEntryId, sourceItem.SourceType, sourceItem.SourceId,
           sourceItem.TotalAmount, sourceItem.CostOfGoods, sourceItem.TaxAmount, sourceItem.Description
    FROM @MissingSeedOperations sourceItem
    INNER JOIN dbo.JournalEntriesTb entryItem
        ON entryItem.SourceType = sourceItem.SourceType
       AND entryItem.SourceId = sourceItem.SourceId;

    INSERT INTO dbo.JournalEntryLinesTb
        (JournalEntryId, AccountCode, Debit, Credit, LineDescription, CostCenterId)
    SELECT JournalEntryId, N'1200', TotalAmount - TaxAmount, 0, Description, NULL
    FROM @NewJournalEntries WHERE SourceType = N'PurchaseInvoice'
    UNION ALL
    SELECT JournalEntryId, N'1000', 0, TotalAmount, Description, NULL
    FROM @NewJournalEntries WHERE SourceType = N'PurchaseInvoice'
    UNION ALL
    SELECT JournalEntryId, N'1210', TaxAmount, 0, Description, NULL
    FROM @NewJournalEntries WHERE SourceType = N'PurchaseInvoice' AND TaxAmount > 0
    UNION ALL
    SELECT JournalEntryId, N'1000', TotalAmount, 0, Description, NULL
    FROM @NewJournalEntries WHERE SourceType = N'SalesInvoice'
    UNION ALL
    SELECT JournalEntryId, N'4000', 0, TotalAmount - TaxAmount, Description, NULL
    FROM @NewJournalEntries WHERE SourceType = N'SalesInvoice'
    UNION ALL
    SELECT JournalEntryId, N'2100', 0, TaxAmount, Description, NULL
    FROM @NewJournalEntries WHERE SourceType = N'SalesInvoice' AND TaxAmount > 0
    UNION ALL
    SELECT JournalEntryId, N'5000', CostOfGoods, 0, N'تكلفة ' + Description, NULL
    FROM @NewJournalEntries WHERE SourceType = N'SalesInvoice' AND CostOfGoods > 0
    UNION ALL
    SELECT JournalEntryId, N'1200', 0, CostOfGoods, N'تكلفة ' + Description, NULL
    FROM @NewJournalEntries WHERE SourceType = N'SalesInvoice' AND CostOfGoods > 0;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

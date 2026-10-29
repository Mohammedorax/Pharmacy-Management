SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_NAME() NOT LIKE N'%[_]qa' AND DB_NAME() NOT LIKE N'%[_]test' AND DB_NAME() NOT LIKE N'%[_]sandbox'
 THROW 51120,N'ترحيل فواتير الوحدات مخصص حاليًا لقاعدة اختبار مستقلة.',1;
IF OBJECT_ID(N'dbo.MedicineUnitCatalogTb',N'U') IS NULL THROW 51120,N'يجب تثبيت كتالوج الوحدات أولًا.',1;
BEGIN TRANSACTION;
BEGIN TRY
 DECLARE @LockResult int;
 EXEC @LockResult=sys.sp_getapplock @Resource=N'PharmacyManagement.UnitInvoiceMigration',@LockMode=N'Exclusive',@LockOwner=N'Transaction',@LockTimeout=15000;
 IF @LockResult<0 THROW 51120,N'تعذر قفل ترحيل فواتير الوحدات.',1;
 -- Stock quantities retain their integer base-unit meaning. Only money storage is widened.
 IF EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.BillsTb1') AND name=N'Sprice' AND TYPE_NAME(system_type_id)=N'int')
  BEGIN DECLARE @Nullable_BillsTb1_Sprice nvarchar(16)=CASE WHEN (SELECT is_nullable FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.BillsTb1') AND name=N'Sprice')=1 THEN N' NULL' ELSE N' NOT NULL' END; EXEC(N'ALTER TABLE dbo.BillsTb1 ALTER COLUMN Sprice decimal(18,2)'+@Nullable_BillsTb1_Sprice); END;
 IF EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.BillsTb1') AND name=N'TotalAmount' AND TYPE_NAME(system_type_id)=N'int')
  BEGIN DECLARE @Nullable_BillsTb1_TotalAmount nvarchar(16)=CASE WHEN (SELECT is_nullable FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.BillsTb1') AND name=N'TotalAmount')=1 THEN N' NULL' ELSE N' NOT NULL' END; EXEC(N'ALTER TABLE dbo.BillsTb1 ALTER COLUMN TotalAmount decimal(18,2)'+@Nullable_BillsTb1_TotalAmount); END;
 IF EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.PurchaseDetailsTb') AND name=N'BuyingPrice' AND TYPE_NAME(system_type_id)=N'int')
  BEGIN DECLARE @Nullable_PurchaseDetailsTb_BuyingPrice nvarchar(16)=CASE WHEN (SELECT is_nullable FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.PurchaseDetailsTb') AND name=N'BuyingPrice')=1 THEN N' NULL' ELSE N' NOT NULL' END; EXEC(N'ALTER TABLE dbo.PurchaseDetailsTb ALTER COLUMN BuyingPrice decimal(18,2)'+@Nullable_PurchaseDetailsTb_BuyingPrice); END;
 IF EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.PurchaseDetailsTb') AND name=N'TotalAmount' AND TYPE_NAME(system_type_id)=N'int')
  BEGIN DECLARE @Nullable_PurchaseDetailsTb_TotalAmount nvarchar(16)=CASE WHEN (SELECT is_nullable FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.PurchaseDetailsTb') AND name=N'TotalAmount')=1 THEN N' NULL' ELSE N' NOT NULL' END; EXEC(N'ALTER TABLE dbo.PurchaseDetailsTb ALTER COLUMN TotalAmount decimal(18,2)'+@Nullable_PurchaseDetailsTb_TotalAmount); END;
 IF (SELECT COUNT(*) FROM sys.columns WHERE object_id IN(OBJECT_ID(N'dbo.BillsTb1'),OBJECT_ID(N'dbo.PurchaseDetailsTb')) AND name IN(N'Sprice',N'BuyingPrice',N'TotalAmount') AND TYPE_NAME(system_type_id)=N'decimal' AND precision=18 AND scale=2)<>4
  THROW 51121,N'حقول مال الفواتير غير متوافقة.',1;
 IF COL_LENGTH(N'dbo.BillsTb1',N'UnitSnapshotRequired') IS NULL ALTER TABLE dbo.BillsTb1 ADD UnitSnapshotRequired bit NOT NULL CONSTRAINT DF_Bills_UnitSnapshotRequired DEFAULT(0);
 IF COL_LENGTH(N'dbo.PurchaseDetailsTb',N'UnitSnapshotRequired') IS NULL ALTER TABLE dbo.PurchaseDetailsTb ADD UnitSnapshotRequired bit NOT NULL CONSTRAINT DF_PurchaseDetails_UnitSnapshotRequired DEFAULT(0);
 IF COL_LENGTH(N'dbo.SalesInvoiceLinesTb',N'UnitSnapshotRequired') IS NULL ALTER TABLE dbo.SalesInvoiceLinesTb ADD UnitSnapshotRequired bit NOT NULL CONSTRAINT DF_SalesInvoiceLines_UnitSnapshotRequired DEFAULT(0);
 IF OBJECT_ID(N'dbo.InvoiceUnitSnapshotsTb',N'U') IS NULL
 CREATE TABLE dbo.InvoiceUnitSnapshotsTb(
  DocumentKind nvarchar(30) NOT NULL,LineKey nvarchar(50) NOT NULL,MedName nvarchar(200) NOT NULL,
  UnitId uniqueidentifier NOT NULL,CatalogRevision bigint NOT NULL,UnitName nvarchar(50) NOT NULL,
  BaseUnitsPerUnit int NOT NULL,EnteredQuantity decimal(18,3) NOT NULL,UnitPrice decimal(18,2) NOT NULL,
  CatalogPrice decimal(18,2) NOT NULL,CurrencyCode nvarchar(10) NOT NULL,BaseQuantity int NOT NULL,GrossTotal decimal(18,2) NOT NULL,
  CONSTRAINT PK_InvoiceUnitSnapshots PRIMARY KEY(DocumentKind,LineKey),
  CONSTRAINT CK_InvoiceUnitSnapshots_Kind CHECK(DocumentKind IN(N'Sale',N'Purchase',N'CommercialSale',N'CommercialReturn')),
  CONSTRAINT CK_InvoiceUnitSnapshots_Unit CHECK(CatalogRevision>0 AND LEN(UnitName)>0 AND BaseUnitsPerUnit>0 AND LEN(CurrencyCode)>0),
  CONSTRAINT CK_InvoiceUnitSnapshots_Quantity CHECK(EnteredQuantity>0 AND BaseQuantity>0 AND CONVERT(decimal(28,3),EnteredQuantity)*BaseUnitsPerUnit=BaseQuantity),
  CONSTRAINT CK_InvoiceUnitSnapshots_Money CHECK(UnitPrice>=0 AND CatalogPrice>=0 AND GrossTotal>0 AND ROUND(EnteredQuantity*UnitPrice,2)=GrossTotal)
 );
 IF OBJECT_ID(N'dbo.UnitLotCostTb',N'U') IS NULL
 CREATE TABLE dbo.UnitLotCostTb(
  LotId uniqueidentifier NOT NULL CONSTRAINT PK_UnitLotCost PRIMARY KEY,
  InitialQuantity int NOT NULL,InitialCost decimal(18,2) NOT NULL,RemainingQuantity int NOT NULL,RemainingCost decimal(18,2) NOT NULL,
  CONSTRAINT FK_UnitLotCost_Lot FOREIGN KEY(LotId) REFERENCES dbo.InventoryLotsTb(LotId),
  CONSTRAINT CK_UnitLotCost_Quantity CHECK(InitialQuantity>0 AND RemainingQuantity>=0 AND RemainingQuantity<=InitialQuantity),
  CONSTRAINT CK_UnitLotCost_Money CHECK(InitialCost>=0 AND RemainingCost>=0 AND RemainingCost<=InitialCost AND (RemainingQuantity>0 OR RemainingCost=0))
 );
 IF COL_LENGTH(N'dbo.LotAllocationsTb',N'CostTotal') IS NULL ALTER TABLE dbo.LotAllocationsTb ADD CostTotal decimal(18,2) NULL;
 IF COL_LENGTH(N'dbo.CommercialReturnLotAllocationsTb',N'CostTotal') IS NULL ALTER TABLE dbo.CommercialReturnLotAllocationsTb ADD CostTotal decimal(18,2) NULL;
 IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'PharmacyManagement.UnitInvoiceVersion')
  EXEC sys.sp_addextendedproperty @name=N'PharmacyManagement.UnitInvoiceVersion',@value=1;
 ELSE IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'PharmacyManagement.UnitInvoiceVersion' AND CONVERT(nvarchar(20),value)=N'1')
  THROW 51121,N'إصدار فواتير الوحدات غير متوافق.',1;
 COMMIT;
END TRY
BEGIN CATCH
 IF XACT_STATE()<>0 ROLLBACK;
 THROW;
END CATCH;

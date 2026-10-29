SET NOCOUNT ON;
SET XACT_ABORT ON;
IF RIGHT(DB_NAME(),3)<>N'_qa' AND RIGHT(DB_NAME(),5)<>N'_test' AND RIGHT(DB_NAME(),8)<>N'_sandbox'
 THROW 51000,N'Base-return migration is restricted to an explicit QA/test/sandbox database.',1;
IF OBJECT_ID(N'dbo.CommercialReturnLinesTb',N'U') IS NULL OR COL_LENGTH(N'dbo.SalesInvoiceLinesTb',N'UnitSnapshotRequired') IS NULL
 THROW 51000,N'Commercial returns and unit-invoice migration 0029 must be installed first.',1;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @lockResult int;
 EXEC @lockResult=sys.sp_getapplock @Resource=N'PharmacyManagement.BaseQuantityReturns.v1',@LockMode=N'Exclusive',@LockOwner=N'Transaction',@LockTimeout=15000;
 IF @lockResult<0 THROW 51000,N'Cannot acquire base-return migration lock.',1;
 IF OBJECT_ID(N'dbo.CommercialReturnUnitSnapshotsTb',N'U') IS NULL
 BEGIN
  IF OBJECT_ID(N'dbo.CommercialReturnEInvoiceBlocksTb',N'U') IS NOT NULL OR COL_LENGTH(N'dbo.CommercialReturnLinesTb',N'ReturnBaseQuantity') IS NOT NULL
   THROW 51000,N'Partial base-return schema detected; no automatic repair was applied.',1;
  CREATE TABLE dbo.CommercialReturnUnitSnapshotsTb
  (
   ReturnInvoiceLineId uniqueidentifier NOT NULL CONSTRAINT PK_CommercialReturnUnitSnapshotsTb PRIMARY KEY,
   OriginalLineId uniqueidentifier NOT NULL,
   MedName nvarchar(200) NOT NULL,
   OriginalUnitId uniqueidentifier NOT NULL,
   OriginalCatalogRevision bigint NOT NULL,
   OriginalUnitName nvarchar(50) NOT NULL,
   OriginalFactor int NOT NULL,
   OriginalEnteredQuantity decimal(18,3) NOT NULL,
   OriginalBaseQuantity int NOT NULL,
   OriginalUnitPrice decimal(18,2) NOT NULL,
   OriginalCatalogPrice decimal(18,2) NOT NULL,
   CurrencyCode nvarchar(10) NOT NULL,
   ReturnBaseQuantity int NOT NULL,
   CONSTRAINT FK_CommercialReturnUnitSnapshotsTb_Credit FOREIGN KEY(ReturnInvoiceLineId) REFERENCES dbo.SalesInvoiceLinesTb(LineId),
   CONSTRAINT FK_CommercialReturnUnitSnapshotsTb_Source FOREIGN KEY(OriginalLineId) REFERENCES dbo.SalesInvoiceLinesTb(LineId),
   CONSTRAINT CK_CommercialReturnUnitSnapshotsTb_Quantity CHECK(OriginalCatalogRevision>0 AND OriginalFactor>0 AND OriginalEnteredQuantity>0 AND OriginalBaseQuantity>0 AND OriginalEnteredQuantity*OriginalFactor=OriginalBaseQuantity AND ReturnBaseQuantity>0 AND ReturnBaseQuantity<=OriginalBaseQuantity),
   CONSTRAINT CK_CommercialReturnUnitSnapshotsTb_Money CHECK(OriginalUnitPrice>=0 AND OriginalCatalogPrice>=0),
   CONSTRAINT CK_CommercialReturnUnitSnapshotsTb_Text CHECK(LEN(LTRIM(RTRIM(MedName)))>0 AND LEN(LTRIM(RTRIM(OriginalUnitName)))>0 AND LEN(LTRIM(RTRIM(CurrencyCode)))>0)
  );
  CREATE TABLE dbo.CommercialReturnEInvoiceBlocksTb
  (
   InvoiceId uniqueidentifier NOT NULL CONSTRAINT PK_CommercialReturnEInvoiceBlocksTb PRIMARY KEY,
   BlockReason nvarchar(500) NOT NULL,
   CreatedAtUtc datetime2 NOT NULL CONSTRAINT DF_CommercialReturnEInvoiceBlocksTb_Created DEFAULT SYSUTCDATETIME(),
   CONSTRAINT FK_CommercialReturnEInvoiceBlocksTb_Invoice FOREIGN KEY(InvoiceId) REFERENCES dbo.SalesInvoicesTb(InvoiceId),
   CONSTRAINT CK_CommercialReturnEInvoiceBlocksTb_Reason CHECK(LEN(LTRIM(RTRIM(BlockReason)))>0)
  );
  ALTER TABLE dbo.CommercialReturnLinesTb ADD ReturnBaseQuantity int NULL,
   ReturnQuantityBasis nvarchar(20) NOT NULL CONSTRAINT DF_CommercialReturnLinesTb_Basis DEFAULT N'OriginalUnit';
  EXEC(N'ALTER TABLE dbo.CommercialReturnLinesTb WITH CHECK ADD CONSTRAINT CK_CommercialReturnLinesTb_Basis CHECK
  ((ReturnQuantityBasis=N''OriginalUnit'' AND ReturnBaseQuantity IS NULL) OR (ReturnQuantityBasis=N''Base'' AND ReturnBaseQuantity IS NOT NULL AND ReturnBaseQuantity>0 AND ReturnQuantity=ReturnBaseQuantity));');
  EXEC sys.sp_addextendedproperty @name=N'PharmacyManagement.BaseReturnVersion',@value=1;
 END;
 IF OBJECT_ID(N'dbo.CommercialReturnEInvoiceBlocksTb',N'U') IS NULL OR
  NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'PharmacyManagement.BaseReturnVersion' AND CONVERT(nvarchar(20),value)=N'1')
  THROW 51000,N'Base-return schema version mismatch.',1;
 IF (SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.CommercialReturnUnitSnapshotsTb'))<>13 OR
  NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.CommercialReturnLinesTb') AND name=N'ReturnBaseQuantity' AND TYPE_NAME(system_type_id)=N'int' AND is_nullable=1) OR
  NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.CommercialReturnUnitSnapshotsTb') AND name=N'OriginalUnitPrice' AND TYPE_NAME(system_type_id)=N'decimal' AND precision=18 AND scale=2 AND is_nullable=0) OR
  NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.CommercialReturnLinesTb') AND name=N'CK_CommercialReturnLinesTb_Basis' AND is_disabled=0 AND is_not_trusted=0)
  THROW 51000,N'Base-return schema shape mismatch.',1;
 COMMIT TRANSACTION;
END TRY
BEGIN CATCH
 IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
 THROW;
END CATCH;

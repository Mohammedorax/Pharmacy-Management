SET NOCOUNT ON;
SET XACT_ABORT ON;
IF RIGHT(DB_NAME(),3)<>N'_qa' AND RIGHT(DB_NAME(),5)<>N'_test' AND RIGHT(DB_NAME(),8)<>N'_sandbox'
 THROW 51110,N'Sales recovery migration requires an explicit QA/test/sandbox database.',1;
IF OBJECT_ID(N'dbo.InvoiceUnitSnapshotsTb',N'U') IS NULL OR OBJECT_ID(N'dbo.PurchaseWriteReceiptsTb',N'U') IS NULL
 THROW 51111,N'Install existing unit invoice and recovery migrations before sales recovery.',1;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @Code int;
 EXEC @Code=sys.sp_getapplock @Resource=N'PharmacyManagementSchema',@LockMode=N'Exclusive',@LockOwner=N'Transaction',@LockTimeout=15000;
 IF @Code<0 THROW 51112,N'Cannot acquire schema migration lock.',1;
-- Explicit installation only. Posting and observation validate; they never create/alter schema.
IF OBJECT_ID(N'dbo.SalesWriteReceiptsTb',N'U') IS NULL
BEGIN
 CREATE TABLE dbo.SalesWriteReceiptsTb(
  RequestId uniqueidentifier NOT NULL CONSTRAINT PK_SalesWriteReceiptsTb PRIMARY KEY,
  RequestFingerprint binary(32) NOT NULL,
  SourceFingerprint char(64) NOT NULL,
  OwnerEmployeeId nvarchar(50) NOT NULL,
  OwnerUser nvarchar(200) NOT NULL,
  OwnerRole nvarchar(50) NOT NULL,
  InvoiceReference nvarchar(100) NOT NULL,
  ResultXml nvarchar(max) NOT NULL,
  CreatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_SalesWriteReceiptsTb_Created DEFAULT SYSUTCDATETIME(),
  CONSTRAINT UQ_SalesWriteReceiptsTb_Invoice UNIQUE(InvoiceReference)
 );
END;

IF OBJECT_ID(N'dbo.SalesWriteReceiptsTb',N'U') IS NULL THROW 51100,N'مخطط استرجاع البيع غير مثبت.',1;
IF (SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.SalesWriteReceiptsTb'))<>9
OR NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.SalesWriteReceiptsTb') AND name=N'RequestId' AND TYPE_NAME(user_type_id)=N'uniqueidentifier' AND is_nullable=0)
OR NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.SalesWriteReceiptsTb') AND name=N'RequestFingerprint' AND TYPE_NAME(user_type_id)=N'binary' AND max_length=32 AND is_nullable=0)
OR NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.SalesWriteReceiptsTb') AND name=N'SourceFingerprint' AND TYPE_NAME(user_type_id)=N'char' AND max_length=64 AND is_nullable=0)
OR NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.SalesWriteReceiptsTb') AND name=N'OwnerEmployeeId' AND TYPE_NAME(user_type_id)=N'nvarchar' AND max_length=100 AND is_nullable=0)
OR NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.SalesWriteReceiptsTb') AND name=N'OwnerUser' AND TYPE_NAME(user_type_id)=N'nvarchar' AND max_length=400 AND is_nullable=0)
OR NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.SalesWriteReceiptsTb') AND name=N'OwnerRole' AND TYPE_NAME(user_type_id)=N'nvarchar' AND max_length=100 AND is_nullable=0)
OR NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.SalesWriteReceiptsTb') AND name=N'InvoiceReference' AND TYPE_NAME(user_type_id)=N'nvarchar' AND max_length=200 AND is_nullable=0)
OR NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.SalesWriteReceiptsTb') AND name=N'ResultXml' AND TYPE_NAME(user_type_id)=N'nvarchar' AND max_length=-1 AND is_nullable=0)
OR NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.SalesWriteReceiptsTb') AND name=N'CreatedAtUtc' AND TYPE_NAME(user_type_id)=N'datetime2' AND scale=7 AND is_nullable=0)
OR NOT EXISTS(SELECT 1 FROM sys.indexes i JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE i.object_id=OBJECT_ID(N'dbo.SalesWriteReceiptsTb') AND i.is_primary_key=1 AND c.name=N'RequestId' AND ic.key_ordinal=1)
OR (SELECT COUNT(*) FROM sys.indexes i JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id WHERE i.object_id=OBJECT_ID(N'dbo.SalesWriteReceiptsTb') AND i.is_primary_key=1 AND ic.key_ordinal>0)<>1
OR NOT EXISTS(SELECT 1 FROM sys.indexes i JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE i.object_id=OBJECT_ID(N'dbo.SalesWriteReceiptsTb') AND i.is_unique=1 AND i.is_disabled=0 AND i.has_filter=0 AND c.name=N'InvoiceReference' AND ic.key_ordinal=1 AND NOT EXISTS(SELECT 1 FROM sys.index_columns other WHERE other.object_id=i.object_id AND other.index_id=i.index_id AND other.key_ordinal>1))
THROW 51101,N'مخطط استرجاع البيع غير متوافق.',1;
 COMMIT;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;
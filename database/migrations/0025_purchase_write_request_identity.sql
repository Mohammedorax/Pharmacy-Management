-- Candidate only. Do not apply to production. Receipt must be written in the same transaction as the invoice.
SET XACT_ABORT ON;
IF OBJECT_ID(N'dbo.PurchaseWriteReceiptsTb',N'U') IS NULL
BEGIN
 CREATE TABLE dbo.PurchaseWriteReceiptsTb(
  RequestId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_PurchaseWriteReceiptsTb PRIMARY KEY,
  RequestFingerprint BINARY(32) NOT NULL,
  SourceFingerprint CHAR(64) NOT NULL,
  OwnerEmployeeId NVARCHAR(50) NOT NULL,
  OwnerUser NVARCHAR(200) NOT NULL,
  OwnerRole NVARCHAR(50) NOT NULL,
  PurchaseId NVARCHAR(50) NOT NULL,
  ResultXml NVARCHAR(MAX) NOT NULL,
  CompletedAtUtc DATETIME2(7) NOT NULL CONSTRAINT DF_PurchaseWriteReceiptsTb_CompletedAtUtc DEFAULT SYSUTCDATETIME()
 );
END;



-- Receipt compatibility validation v1
DECLARE @ReceiptObject int=OBJECT_ID(N'dbo.PurchaseWriteReceiptsTb',N'U');
IF @ReceiptObject IS NULL THROW 51080,N'مخطط استعادة المشتريات غير مثبت.',1;
IF (SELECT COUNT(*) FROM sys.columns WHERE object_id=@ReceiptObject)<>9
 OR EXISTS(
  SELECT Expected.Name,Expected.TypeName,Expected.Length,Expected.Scale
  FROM (VALUES
   (N'RequestId',N'uniqueidentifier',16,0),
   (N'RequestFingerprint',N'binary',32,0),
   (N'SourceFingerprint',N'char',64,0),
   (N'OwnerEmployeeId',N'nvarchar',100,0),
   (N'OwnerUser',N'nvarchar',400,0),
   (N'OwnerRole',N'nvarchar',100,0),
   (N'PurchaseId',N'nvarchar',100,0),
   (N'ResultXml',N'nvarchar',-1,0),
   (N'CompletedAtUtc',N'datetime2',8,7)
  ) Expected(Name,TypeName,Length,Scale)
  EXCEPT
  SELECT C.name,T.name,C.max_length,C.scale
  FROM sys.columns C JOIN sys.types T ON C.user_type_id=T.user_type_id
  WHERE C.object_id=@ReceiptObject AND C.is_nullable=0 AND C.is_identity=0
   AND C.is_computed=0 AND T.is_user_defined=0
 )
 OR NOT EXISTS(
  SELECT 1 FROM sys.indexes I
  WHERE I.object_id=@ReceiptObject AND I.is_primary_key=1 AND I.is_unique=1
   AND I.is_disabled=0 AND I.has_filter=0
   AND (SELECT COUNT(*) FROM sys.index_columns K WHERE K.object_id=I.object_id AND K.index_id=I.index_id AND K.key_ordinal>0)=1
   AND EXISTS(SELECT 1 FROM sys.index_columns K JOIN sys.columns C ON C.object_id=K.object_id AND C.column_id=K.column_id
    WHERE K.object_id=I.object_id AND K.index_id=I.index_id AND K.key_ordinal=1 AND C.name=N'RequestId')
 )
 OR NOT EXISTS(
  SELECT 1 FROM sys.default_constraints D JOIN sys.columns C ON C.object_id=D.parent_object_id AND C.column_id=D.parent_column_id
  WHERE D.parent_object_id=@ReceiptObject AND C.name=N'CompletedAtUtc'
   AND LOWER(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(D.definition,N' ',N''),N'(',N''),N')',N''),N'[',N''),N']',N''))=N'sysutcdatetime'
 )
 THROW 51082,N'مخطط استعادة المشتريات غير متوافق؛ يلزم ترحيل معتمد قبل الحفظ.',1;
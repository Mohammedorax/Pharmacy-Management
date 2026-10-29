/* Additive unit catalogue rehearsal. Does not enable unit invoice posting or reinterpret existing stock. */
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_NAME() NOT LIKE N'%[_]qa' AND DB_NAME() NOT LIKE N'%[_]test' AND DB_NAME() NOT LIKE N'%[_]sandbox'
 THROW 51100,N'ترحيل وحدات الأصناف مخصص حاليًا لبيئة الاختبار.',1;
IF OBJECT_ID(N'dbo.MedicineTb1',N'U') IS NULL
 THROW 51100,N'جدول الأصناف الأساسي غير موجود.',1;
BEGIN TRANSACTION;
BEGIN TRY
 DECLARE @LockResult INT;
 EXEC @LockResult=sys.sp_getapplock @Resource=N'PharmacyManagement.MedicineUnitCatalogMigration',@LockMode=N'Exclusive',@LockOwner=N'Transaction',@LockTimeout=15000;
 IF @LockResult<0 THROW 51100,N'تعذر قفل تهيئة وحدات الأصناف.',1;
 IF OBJECT_ID(N'dbo.MedicineUnitCatalogTb',N'U') IS NULL
 CREATE TABLE dbo.MedicineUnitCatalogTb(
  CatalogId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_MedicineUnitCatalog PRIMARY KEY,
  MedName NVARCHAR(200) NOT NULL CONSTRAINT UQ_MedicineUnitCatalog_Medicine UNIQUE,
  BaseUnitId UNIQUEIDENTIFIER NOT NULL,
  Revision BIGINT NOT NULL CONSTRAINT CK_MedicineUnitCatalog_Revision CHECK(Revision>0),
  CurrencyCode NVARCHAR(10) NOT NULL CONSTRAINT CK_MedicineUnitCatalog_Currency CHECK(LEN(LTRIM(RTRIM(CurrencyCode)))>0),
  ConfirmedBaseStockQuantity INT NOT NULL CONSTRAINT CK_MedicineUnitCatalog_Stock CHECK(ConfirmedBaseStockQuantity>=0),
  CreatedAtUtc DATETIME2(7) NOT NULL CONSTRAINT DF_MedicineUnitCatalog_Created DEFAULT SYSUTCDATETIME(),
  UpdatedAtUtc DATETIME2(7) NOT NULL CONSTRAINT DF_MedicineUnitCatalog_Updated DEFAULT SYSUTCDATETIME(),
  CONSTRAINT FK_MedicineUnitCatalog_Medicine FOREIGN KEY(MedName) REFERENCES dbo.MedicineTb1(MedName) ON UPDATE CASCADE
 );
 IF OBJECT_ID(N'dbo.MedicineUnitsTb',N'U') IS NULL
 CREATE TABLE dbo.MedicineUnitsTb(
  UnitId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_MedicineUnits PRIMARY KEY,
  CatalogId UNIQUEIDENTIFIER NOT NULL,
  UnitName NVARCHAR(50) NOT NULL CONSTRAINT CK_MedicineUnits_Name CHECK(LEN(LTRIM(RTRIM(UnitName)))>0),
  BaseUnitsPerUnit INT NOT NULL CONSTRAINT CK_MedicineUnits_Factor CHECK(BaseUnitsPerUnit>0),
  SellingPrice DECIMAL(18,2) NOT NULL CONSTRAINT CK_MedicineUnits_Selling CHECK(SellingPrice>=0),
  BuyingPrice DECIMAL(18,2) NOT NULL CONSTRAINT CK_MedicineUnits_Buying CHECK(BuyingPrice>=0),
  IsActive BIT NOT NULL,
  DisplayOrder INT NOT NULL CONSTRAINT CK_MedicineUnits_Order CHECK(DisplayOrder>=0 AND DisplayOrder<100),
  CONSTRAINT FK_MedicineUnits_Catalog FOREIGN KEY(CatalogId) REFERENCES dbo.MedicineUnitCatalogTb(CatalogId)
 );
 IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.MedicineUnitsTb') AND name=N'UX_MedicineUnits_ActiveName')
 CREATE UNIQUE INDEX UX_MedicineUnits_ActiveName ON dbo.MedicineUnitsTb(CatalogId,UnitName) WHERE IsActive=1;
 IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.MedicineUnitsTb') AND name=N'UX_MedicineUnits_ActiveOrder')
 CREATE UNIQUE INDEX UX_MedicineUnits_ActiveOrder ON dbo.MedicineUnitsTb(CatalogId,DisplayOrder) WHERE IsActive=1;
 IF OBJECT_ID(N'dbo.MedicineUnitWriteReceiptsTb',N'U') IS NULL
 CREATE TABLE dbo.MedicineUnitWriteReceiptsTb(
  RequestId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_MedicineUnitWriteReceipts PRIMARY KEY,
  CatalogId UNIQUEIDENTIFIER NOT NULL,
  MedicineName NVARCHAR(200) NOT NULL,
  ExpectedRevision BIGINT NOT NULL,
  ResultRevision BIGINT NOT NULL,
  RequestFingerprint BINARY(32) NOT NULL,
  Payload VARBINARY(MAX) NOT NULL,
  OwnerUser NVARCHAR(200) NOT NULL,
  OwnerRole NVARCHAR(50) NOT NULL,
  CompletedAtUtc DATETIME2(7) NOT NULL CONSTRAINT DF_MedicineUnitReceipts_Completed DEFAULT SYSUTCDATETIME(),
  CONSTRAINT CK_MedicineUnitReceipts_Revision CHECK(ExpectedRevision>=0 AND ExpectedRevision<9223372036854775807 AND ResultRevision=ExpectedRevision+1),
  CONSTRAINT UQ_MedicineUnitReceipts_Revision UNIQUE(CatalogId,ResultRevision),
  CONSTRAINT FK_MedicineUnitReceipts_Catalog FOREIGN KEY(CatalogId) REFERENCES dbo.MedicineUnitCatalogTb(CatalogId)
 );
 DECLARE @Expected TABLE(TableName SYSNAME,ColumnName SYSNAME,TypeName SYSNAME,Length SMALLINT,Precision TINYINT,Scale TINYINT);
 INSERT @Expected VALUES
 (N'MedicineUnitCatalogTb',N'CatalogId',N'uniqueidentifier',16,0,0),(N'MedicineUnitCatalogTb',N'MedName',N'nvarchar',400,0,0),
 (N'MedicineUnitCatalogTb',N'BaseUnitId',N'uniqueidentifier',16,0,0),(N'MedicineUnitCatalogTb',N'Revision',N'bigint',8,19,0),
 (N'MedicineUnitCatalogTb',N'CurrencyCode',N'nvarchar',20,0,0),(N'MedicineUnitCatalogTb',N'ConfirmedBaseStockQuantity',N'int',4,10,0),
 (N'MedicineUnitCatalogTb',N'CreatedAtUtc',N'datetime2',8,27,7),(N'MedicineUnitCatalogTb',N'UpdatedAtUtc',N'datetime2',8,27,7),
 (N'MedicineUnitsTb',N'UnitId',N'uniqueidentifier',16,0,0),(N'MedicineUnitsTb',N'CatalogId',N'uniqueidentifier',16,0,0),
 (N'MedicineUnitsTb',N'UnitName',N'nvarchar',100,0,0),(N'MedicineUnitsTb',N'BaseUnitsPerUnit',N'int',4,10,0),
 (N'MedicineUnitsTb',N'SellingPrice',N'decimal',9,18,2),(N'MedicineUnitsTb',N'BuyingPrice',N'decimal',9,18,2),
 (N'MedicineUnitsTb',N'IsActive',N'bit',1,1,0),(N'MedicineUnitsTb',N'DisplayOrder',N'int',4,10,0),
 (N'MedicineUnitWriteReceiptsTb',N'RequestId',N'uniqueidentifier',16,0,0),(N'MedicineUnitWriteReceiptsTb',N'CatalogId',N'uniqueidentifier',16,0,0),
 (N'MedicineUnitWriteReceiptsTb',N'MedicineName',N'nvarchar',400,0,0),(N'MedicineUnitWriteReceiptsTb',N'ExpectedRevision',N'bigint',8,19,0),
 (N'MedicineUnitWriteReceiptsTb',N'ResultRevision',N'bigint',8,19,0),(N'MedicineUnitWriteReceiptsTb',N'RequestFingerprint',N'binary',32,0,0),
 (N'MedicineUnitWriteReceiptsTb',N'Payload',N'varbinary',-1,0,0),(N'MedicineUnitWriteReceiptsTb',N'OwnerUser',N'nvarchar',400,0,0),
 (N'MedicineUnitWriteReceiptsTb',N'OwnerRole',N'nvarchar',100,0,0),(N'MedicineUnitWriteReceiptsTb',N'CompletedAtUtc',N'datetime2',8,27,7);
 IF EXISTS(SELECT 1 FROM @Expected E LEFT JOIN sys.columns C ON C.object_id=OBJECT_ID(N'dbo.'+E.TableName) AND C.name=E.ColumnName
  LEFT JOIN sys.types T ON T.user_type_id=C.user_type_id WHERE C.column_id IS NULL OR T.name<>E.TypeName OR T.is_user_defined<>0
  OR C.max_length<>E.Length OR C.precision<>E.Precision OR C.scale<>E.Scale OR C.is_nullable<>0 OR C.is_computed<>0 OR C.is_identity<>0)
  OR (SELECT COUNT(*) FROM sys.columns WHERE object_id IN(OBJECT_ID(N'dbo.MedicineUnitCatalogTb'),OBJECT_ID(N'dbo.MedicineUnitsTb'),OBJECT_ID(N'dbo.MedicineUnitWriteReceiptsTb')))<>26
  THROW 51101,N'مخطط وحدات الأصناف غير متوافق؛ لم يطبق الترحيل.',1;
 IF EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id IN(OBJECT_ID(N'dbo.MedicineUnitCatalogTb'),OBJECT_ID(N'dbo.MedicineUnitsTb'),OBJECT_ID(N'dbo.MedicineUnitWriteReceiptsTb')) AND (is_disabled=1 OR is_not_trusted=1))
  THROW 51101,N'قيود وحدات الأصناف غير معتمدة.',1;
 DECLARE @Checks TABLE(TableName SYSNAME,Name SYSNAME,Definition NVARCHAR(300));
 INSERT @Checks VALUES
 (N'MedicineUnitCatalogTb',N'CK_MedicineUnitCatalog_Revision',N'revision>0'),
 (N'MedicineUnitCatalogTb',N'CK_MedicineUnitCatalog_Currency',N'lenltrimrtrimcurrencycode>0'),
 (N'MedicineUnitCatalogTb',N'CK_MedicineUnitCatalog_Stock',N'confirmedbasestockquantity>=0'),
 (N'MedicineUnitsTb',N'CK_MedicineUnits_Name',N'lenltrimrtrimunitname>0'),
 (N'MedicineUnitsTb',N'CK_MedicineUnits_Factor',N'baseunitsperunit>0'),
 (N'MedicineUnitsTb',N'CK_MedicineUnits_Selling',N'sellingprice>=0'),
 (N'MedicineUnitsTb',N'CK_MedicineUnits_Buying',N'buyingprice>=0'),
 (N'MedicineUnitsTb',N'CK_MedicineUnits_Order',N'displayorder>=0anddisplayorder<100'),
 (N'MedicineUnitWriteReceiptsTb',N'CK_MedicineUnitReceipts_Revision',N'expectedrevision>=0andexpectedrevision<9223372036854775807.andresultrevision=expectedrevision+1');
 IF EXISTS(SELECT 1 FROM @Checks E LEFT JOIN sys.check_constraints C ON C.parent_object_id=OBJECT_ID(N'dbo.'+E.TableName) AND C.name=E.Name
  WHERE C.object_id IS NULL OR C.is_disabled<>0 OR C.is_not_trusted<>0 OR
  LOWER(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(C.definition,N'[',N''),N']',N''),N'(',N''),N')',N''),N' ',N''),CHAR(9),N''),CHAR(10),N''),CHAR(13),N''))<>E.Definition)
  THROW 51101,N'قيد مطلوب للوحدات مفقود أو غير متوافق؛ لم تتغير البيانات.',1;
 DECLARE @Keys TABLE(TableName SYSNAME,Name SYSNAME,PrimaryKey BIT,UniqueConstraint BIT,Columns NVARCHAR(200),Filter NVARCHAR(100));
 INSERT @Keys VALUES
 (N'MedicineUnitCatalogTb',N'PK_MedicineUnitCatalog',1,0,N'CatalogId',N''),
 (N'MedicineUnitCatalogTb',N'UQ_MedicineUnitCatalog_Medicine',0,1,N'MedName',N''),
 (N'MedicineUnitsTb',N'PK_MedicineUnits',1,0,N'UnitId',N''),
 (N'MedicineUnitsTb',N'UX_MedicineUnits_ActiveName',0,0,N'CatalogId,UnitName',N'isactive=1'),
 (N'MedicineUnitsTb',N'UX_MedicineUnits_ActiveOrder',0,0,N'CatalogId,DisplayOrder',N'isactive=1'),
 (N'MedicineUnitWriteReceiptsTb',N'PK_MedicineUnitWriteReceipts',1,0,N'RequestId',N''),
 (N'MedicineUnitWriteReceiptsTb',N'UQ_MedicineUnitReceipts_Revision',0,1,N'CatalogId,ResultRevision',N'');
 IF EXISTS(SELECT 1 FROM @Keys E LEFT JOIN sys.indexes I ON I.object_id=OBJECT_ID(N'dbo.'+E.TableName) AND I.name=E.Name
  WHERE I.index_id IS NULL OR I.is_disabled<>0 OR I.is_unique<>1 OR I.is_primary_key<>E.PrimaryKey OR I.is_unique_constraint<>E.UniqueConstraint
   OR LOWER(REPLACE(REPLACE(REPLACE(REPLACE(ISNULL(I.filter_definition,N''),N'[',N''),N']',N''),N'(',N''),N')',N''))<>E.Filter
   OR STUFF((SELECT N','+C.name FROM sys.index_columns IC JOIN sys.columns C ON C.object_id=IC.object_id AND C.column_id=IC.column_id
     WHERE IC.object_id=I.object_id AND IC.index_id=I.index_id AND IC.key_ordinal>0 ORDER BY IC.key_ordinal FOR XML PATH(''),TYPE).value('.','nvarchar(max)'),1,1,N'')<>E.Columns)
  THROW 51101,N'مفتاح أو فهرس الوحدات غير متوافق.',1;
 DECLARE @ForeignKeys TABLE(TableName SYSNAME,Name SYSNAME,ColumnName SYSNAME,TargetTable SYSNAME,TargetColumn SYSNAME,UpdateAction TINYINT);
 INSERT @ForeignKeys VALUES
 (N'MedicineUnitCatalogTb',N'FK_MedicineUnitCatalog_Medicine',N'MedName',N'MedicineTb1',N'MedName',1),
 (N'MedicineUnitsTb',N'FK_MedicineUnits_Catalog',N'CatalogId',N'MedicineUnitCatalogTb',N'CatalogId',0),
 (N'MedicineUnitWriteReceiptsTb',N'FK_MedicineUnitReceipts_Catalog',N'CatalogId',N'MedicineUnitCatalogTb',N'CatalogId',0);
 IF EXISTS(SELECT 1 FROM @ForeignKeys E LEFT JOIN sys.foreign_keys F ON F.parent_object_id=OBJECT_ID(N'dbo.'+E.TableName) AND F.name=E.Name
  LEFT JOIN sys.foreign_key_columns C ON C.constraint_object_id=F.object_id AND C.constraint_column_id=1
  WHERE F.object_id IS NULL OR F.is_disabled<>0 OR F.is_not_trusted<>0 OR F.update_referential_action<>E.UpdateAction OR F.delete_referential_action<>0
   OR C.parent_column_id<>COLUMNPROPERTY(OBJECT_ID(N'dbo.'+E.TableName),E.ColumnName,'ColumnId') OR C.referenced_object_id<>OBJECT_ID(N'dbo.'+E.TargetTable)
   OR C.referenced_column_id<>COLUMNPROPERTY(OBJECT_ID(N'dbo.'+E.TargetTable),E.TargetColumn,'ColumnId')
   OR (SELECT COUNT(*) FROM sys.foreign_key_columns X WHERE X.constraint_object_id=F.object_id)<>1)
  THROW 51101,N'ربط الوحدات بالصنف غير متوافق.',1;
 IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'PharmacyManagement.MedicineUnitCatalogVersion')
  EXEC sys.sp_addextendedproperty @name=N'PharmacyManagement.MedicineUnitCatalogVersion',@value=1;
 ELSE IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'PharmacyManagement.MedicineUnitCatalogVersion' AND CONVERT(NVARCHAR(20),value)=N'1')
  THROW 51101,N'إصدار مخطط الوحدات غير مدعوم.',1;
 COMMIT TRANSACTION;
END TRY
BEGIN CATCH
 IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
 THROW;
END CATCH;

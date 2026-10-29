/* Customer master request receipts. Candidate only: owned QA/Test/Sandbox.
   Additive metadata; no historical customer, audit or financial row rewrites. */
SET NOCOUNT ON;
SET XACT_ABORT ON;
DECLARE @DatabaseName nvarchar(128)=LOWER(DB_NAME());
IF @DatabaseName NOT LIKE N'%[_]test' AND @DatabaseName NOT LIKE N'%[_]qa' AND @DatabaseName NOT LIKE N'%[_]sandbox'
    THROW 51000,N'تطبق هوية محاولة العميل على QA/Test/Sandbox فقط قبل قبول الإصدار.',1;
IF OBJECT_ID(N'dbo.CustomersBillsTb',N'U') IS NULL OR OBJECT_ID(N'dbo.AuditLogTb',N'U') IS NULL
    OR OBJECT_ID(N'dbo.Seq_CustomerId',N'SO') IS NULL
    THROW 51000,N'يلزم مخطط العميل والتدقيق وتسلسل معرف العميل.',1;
BEGIN TRANSACTION;
BEGIN TRY
    DECLARE @LockResult int;
    EXEC @LockResult=sys.sp_getapplock @Resource=N'PharmacyManagement.CustomerMasterRequestMigration',
        @LockMode=N'Exclusive',@LockOwner=N'Transaction',@LockTimeout=60000;
    IF @LockResult<0 THROW 51000,N'تعذر قفل ترحيل محاولة العميل.',1;
    IF OBJECT_ID(N'dbo.CustomerMasterRequestsTb',N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.CustomerMasterRequestsTb
        (
            RequestId uniqueidentifier NOT NULL,
            RequestKind tinyint NOT NULL,
            TargetCustomerId nvarchar(50) NOT NULL,
            CustomerName nvarchar(200) NOT NULL,
            PhoneNumber nvarchar(50) NOT NULL,
            CustomerAddress nvarchar(300) NOT NULL,
            OwnerUser nvarchar(200) NOT NULL,
            OwnerRole nvarchar(50) NOT NULL,
            Outcome tinyint NOT NULL,
            CustomerId nvarchar(50) NULL,
            AuditId int NOT NULL,
            ExecutedUser nvarchar(200) NOT NULL,
            ExecutedRole nvarchar(50) NOT NULL,
            FinalizedAtUtc datetime2(7) NOT NULL,
            CONSTRAINT PK_CustomerMasterRequestsTb PRIMARY KEY(RequestId),
            CONSTRAINT CK_CustomerMasterRequestsTb_Request CHECK
            (
                RequestId<>'00000000-0000-0000-0000-000000000000'
                AND ((RequestKind=1 AND TargetCustomerId=N'') OR (RequestKind=2 AND LEN(TargetCustomerId)>0 AND TargetCustomerId<>N'0'))
                AND LEN(CustomerName)>0 AND LEN(PhoneNumber)>0 AND PhoneNumber<>N'000000000'
                AND LEN(OwnerUser)>0 AND LEN(OwnerRole)>0 AND LEN(ExecutedUser)>0 AND LEN(ExecutedRole)>0
                AND AuditId>0
                AND ((Outcome=1 AND CustomerId IS NOT NULL AND LEN(CustomerId)>0 AND CustomerId<>N'0') OR (Outcome=2 AND CustomerId IS NULL))
            )
        );
    END;
    DECLARE @Expected TABLE(ColumnName sysname,TypeName sysname,MaxLength smallint,Nullable bit);
    INSERT @Expected VALUES
      (N'RequestId',N'uniqueidentifier',16,0),(N'RequestKind',N'tinyint',1,0),
      (N'TargetCustomerId',N'nvarchar',100,0),(N'CustomerName',N'nvarchar',400,0),(N'PhoneNumber',N'nvarchar',100,0),
      (N'CustomerAddress',N'nvarchar',600,0),(N'OwnerUser',N'nvarchar',400,0),(N'OwnerRole',N'nvarchar',100,0),
      (N'Outcome',N'tinyint',1,0),(N'CustomerId',N'nvarchar',100,1),(N'AuditId',N'int',4,0),
      (N'ExecutedUser',N'nvarchar',400,0),(N'ExecutedRole',N'nvarchar',100,0),(N'FinalizedAtUtc',N'datetime2',8,0);
    IF EXISTS(SELECT 1 FROM @Expected e LEFT JOIN sys.columns c
       ON c.object_id=OBJECT_ID(N'dbo.CustomerMasterRequestsTb') AND c.name=e.ColumnName
       LEFT JOIN sys.types t ON t.user_type_id=c.user_type_id
       WHERE c.column_id IS NULL OR t.name<>e.TypeName OR c.max_length<>e.MaxLength OR c.is_nullable<>e.Nullable
          OR c.is_computed=1 OR c.is_identity=1 OR (e.ColumnName=N'FinalizedAtUtc' AND c.scale<>7))
        THROW 51000,N'جدول محاولة العميل موجود بعقد غير متوافق.',1;
    IF NOT EXISTS(SELECT 1 FROM sys.indexes i JOIN sys.index_columns k ON k.object_id=i.object_id AND k.index_id=i.index_id
      JOIN sys.columns c ON c.object_id=k.object_id AND c.column_id=k.column_id
      WHERE i.object_id=OBJECT_ID(N'dbo.CustomerMasterRequestsTb') AND i.is_primary_key=1 AND i.is_unique=1
        AND i.is_disabled=0 AND k.key_ordinal=1 AND c.name=N'RequestId'
        AND (SELECT COUNT(*) FROM sys.index_columns x WHERE x.object_id=i.object_id AND x.index_id=i.index_id AND x.key_ordinal>0)=1)
        THROW 51000,N'مفتاح محاولة العميل غير صالح.',1;
    IF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.CustomerMasterRequestsTb')
       AND name=N'CK_CustomerMasterRequestsTb_Request' AND is_disabled=0 AND is_not_trusted=0)
        THROW 51000,N'قيد حالة محاولة العميل غير صالح.',1;
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
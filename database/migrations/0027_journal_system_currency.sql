/* Journal currency snapshot. Historical rows remain unknown (NULL).
   Explicit QA/Test/Sandbox rehearsal only; no financial backfill. */
SET NOCOUNT ON;
SET XACT_ABORT ON;
DECLARE @DatabaseName NVARCHAR(128)=LOWER(DB_NAME());
IF @DatabaseName NOT LIKE N'%[_]qa' AND @DatabaseName NOT LIKE N'%[_]test' AND @DatabaseName NOT LIKE N'%[_]sandbox'
 THROW 51000,N'ترحيل عملة القيد مخصص لبيئة الاختبار.',1;
IF OBJECT_ID(N'dbo.JournalEntriesTb',N'U') IS NULL
 THROW 51000,N'المخطط المحاسبي الأساسي غير موجود.',1;
BEGIN TRANSACTION;
BEGIN TRY
 DECLARE @LockResult INT;
 EXEC @LockResult=sys.sp_getapplock @Resource=N'PharmacyManagement.JournalCurrencyMigration',
  @LockMode=N'Exclusive',@LockOwner=N'Transaction',@LockTimeout=60000;
 IF @LockResult<0 THROW 51000,N'تعذر قفل ترحيل عملة القيد.',1;
 IF COL_LENGTH(N'dbo.JournalEntriesTb',N'SystemCurrencyCode') IS NULL
  ALTER TABLE dbo.JournalEntriesTb ADD SystemCurrencyCode NVARCHAR(10) NULL;
 -- Journal currency compatibility validation v1
 IF NOT EXISTS(SELECT 1 FROM sys.columns C JOIN sys.types T ON T.user_type_id=C.user_type_id
  WHERE C.object_id=OBJECT_ID(N'dbo.JournalEntriesTb') AND C.name=N'SystemCurrencyCode'
   AND T.name=N'nvarchar' AND T.is_user_defined=0 AND C.max_length=20 AND C.is_nullable=1
   AND C.is_identity=0 AND C.is_computed=0 AND C.default_object_id=0)
  THROW 51097,N'حقل عملة القيد غير متوافق؛ لم يطبق الترحيل.',1;
 COMMIT TRANSACTION;
END TRY
BEGIN CATCH
 IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
 THROW;
END CATCH;

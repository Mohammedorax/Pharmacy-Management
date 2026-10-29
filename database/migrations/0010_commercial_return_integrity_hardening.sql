/* Commercial return integrity hardening - TEST DATABASE ONLY. */
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @DatabaseName NVARCHAR(128) = LOWER(DB_NAME());
IF @DatabaseName NOT LIKE N'%[_]test' AND @DatabaseName NOT LIKE N'%[_]qa' AND @DatabaseName NOT LIKE N'%[_]sandbox'
    THROW 51000, N'يمكن تشغيل هذا الترحيل فقط على قاعدة ينتهي اسمها بـ _test أو _qa أو _sandbox.', 1;
IF OBJECT_ID(N'dbo.CommercialReturnHeadersTb', N'U') IS NULL OR OBJECT_ID(N'dbo.CommercialReturnLinesTb', N'U') IS NULL OR OBJECT_ID(N'dbo.CommercialReturnLotAllocationsTb', N'U') IS NULL
    THROW 51000, N'يتطلب الترحيل 0008 للمرتجعات التجارية قبل التقوية.', 1;

BEGIN TRANSACTION;
BEGIN TRY
    DECLARE @LockResult INT;
    EXEC @LockResult = sys.sp_getapplock @Resource=N'PharmacyManagement.CommercialReturnIntegrityHardening', @LockMode=N'Exclusive', @LockOwner=N'Transaction', @LockTimeout=60000;
    IF @LockResult < 0 THROW 51000, N'تعذر الحصول على قفل تقوية المرتجعات التجارية؛ أعد المحاولة لاحقاً.', 1;

    EXEC(N'ALTER TRIGGER dbo.trg_CommercialReturnLinesTb_AppendOnly ON dbo.CommercialReturnLinesTb AFTER INSERT, UPDATE, DELETE AS
    BEGIN
        SET NOCOUNT ON;
        IF EXISTS (SELECT 1 FROM deleted)
        BEGIN RAISERROR(''Commercial return lines are append-only.'',16,1); ROLLBACK TRANSACTION; RETURN; END;
        IF EXISTS (SELECT 1 FROM inserted x JOIN dbo.CommercialReturnHeadersTb h ON h.ReturnId=x.ReturnId JOIN dbo.SalesInvoicesTb i ON i.InvoiceId=h.ReturnInvoiceId WHERE i.Status=N''Issued'')
        BEGIN RAISERROR(''Issued commercial returns may not receive new lines.'',16,1); ROLLBACK TRANSACTION; RETURN; END;
    END;');

    EXEC(N'ALTER TRIGGER dbo.trg_CommercialReturnLotAllocationsTb_AppendOnly ON dbo.CommercialReturnLotAllocationsTb AFTER INSERT, UPDATE, DELETE AS
    BEGIN
        SET NOCOUNT ON;
        IF EXISTS (SELECT 1 FROM deleted)
        BEGIN RAISERROR(''Commercial return lot allocations are append-only.'',16,1); ROLLBACK TRANSACTION; RETURN; END;
        IF EXISTS (SELECT 1 FROM inserted x JOIN dbo.CommercialReturnLinesTb r ON r.ReturnLineId=x.ReturnLineId JOIN dbo.CommercialReturnHeadersTb h ON h.ReturnId=r.ReturnId JOIN dbo.SalesInvoicesTb i ON i.InvoiceId=h.ReturnInvoiceId WHERE i.Status=N''Issued'')
        BEGIN RAISERROR(''Issued commercial returns may not receive new lot allocations.'',16,1); ROLLBACK TRANSACTION; RETURN; END;
    END;');

    EXEC(N'ALTER TRIGGER dbo.trg_CommercialReturnLotAllocationsTb_SourceLink ON dbo.CommercialReturnLotAllocationsTb AFTER INSERT, UPDATE AS
    BEGIN
        SET NOCOUNT ON;
        IF EXISTS (SELECT 1 FROM inserted x JOIN dbo.CommercialReturnLinesTb r ON r.ReturnLineId=x.ReturnLineId LEFT JOIN dbo.LotAllocationsTb s ON s.AllocationId=x.OriginalSaleAllocationId WHERE s.AllocationId IS NULL OR s.InvoiceLineId IS NULL OR s.InvoiceLineId<>r.OriginalLineId OR s.LotId<>x.LotId OR s.MovementType<>N''Sale'')
        BEGIN RAISERROR(''Commercial return lot must originate from the returned sale line and lot.'',16,1); ROLLBACK TRANSACTION; RETURN; END;
        IF EXISTS (SELECT 1 FROM dbo.CommercialReturnLotAllocationsTb x JOIN dbo.LotAllocationsTb s ON s.AllocationId=x.OriginalSaleAllocationId GROUP BY x.OriginalSaleAllocationId,s.Quantity HAVING SUM(x.Quantity)>MAX(s.Quantity))
        BEGIN RAISERROR(''Commercial return lot quantity exceeds the original sale allocation.'',16,1); ROLLBACK TRANSACTION; RETURN; END;
    END;');

    IF OBJECT_ID(N'dbo.CommercialSchemaVersionsTb', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.CommercialSchemaVersionsTb WHERE VersionNo=6)
        INSERT INTO dbo.CommercialSchemaVersionsTb (VersionNo) VALUES (6);
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

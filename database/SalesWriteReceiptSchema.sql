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

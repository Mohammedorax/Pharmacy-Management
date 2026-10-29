using System;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Text.RegularExpressions;

namespace project_fo_3
{
    internal sealed class CommercialMigrationRequest
    {
        public string VerifiedBackupPath { get; set; }
        public bool IsTestDatabase { get; set; }
        public bool AcknowledgeNoHistoricalLotBackfill { get; set; }
    }

    // لا يستدعى هذا المُثبّت من بدء التطبيق عمداً؛ تشغيله يتطلب نسخة احتياطية مؤكدة وقاعدة اختبار.
    internal static class CommercialSchemaInstaller
    {
        public static void Validate(CommercialMigrationRequest request)
        {
            if (request == null || !request.IsTestDatabase || !request.AcknowledgeNoHistoricalLotBackfill ||
                string.IsNullOrWhiteSpace(request.VerifiedBackupPath) ||
                !string.Equals(Path.GetExtension(request.VerifiedBackupPath), ".bak", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("لن يتم تثبيت نموذج الدفعات والفواتير التجارية إلا على قاعدة اختبار وبعد اختيار نسخة احتياطية موجودة وتأكيد عدم ترحيل الأرصدة التاريخية تلقائياً.");
            }
        }

        public static void Install(SqlConnection connection, CommercialMigrationRequest request)
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));
            Validate(request);
            bool mustClose = connection.State != ConnectionState.Open;
            try
            {
                if (mustClose) connection.Open();
                AssertClearlyNamedTestDatabase(connection);
                VerifyBackupForTargetServer(connection, request.VerifiedBackupPath);
                using (SqlTransaction transaction = connection.BeginTransaction())
                using (SqlCommand command = new SqlCommand(SchemaSql, connection, transaction))
                {
                    try { command.CommandTimeout = 60; command.ExecuteNonQuery(); ValidateSchema(connection, transaction); transaction.Commit(); }
                    catch { transaction.Rollback(); throw; }
                }
            }
            finally { if (mustClose && connection.State != ConnectionState.Closed) connection.Close(); }
        }

        // RESTORE VERIFYONLY لا يستعيد أو يعدّل البيانات؛ يثبت أن الملف الذي يعتمد عليه
        // التثبيت هو نسخة SQL Server سليمة وعلى الخادم نفسه قبل تنفيذ أي DDL تجاري.
        public static void VerifyBackupForTargetServer(SqlConnection targetConnection, string backupPath)
        {
            if (targetConnection == null) throw new ArgumentNullException(nameof(targetConnection));
            if (string.IsNullOrWhiteSpace(backupPath) ||
                !string.Equals(Path.GetExtension(backupPath), ".bak", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("يلزم تمرير نسخة SQL Server بصيغة .bak موجودة قبل تثبيت المخطط التجاري.");
            }

            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(targetConnection.ConnectionString)
            {
                InitialCatalog = "master"
            };
            using (SqlConnection masterConnection = new SqlConnection(builder.ConnectionString))
            using (SqlCommand command = new SqlCommand("RESTORE VERIFYONLY FROM DISK = @BackupPath;", masterConnection))
            {
                command.CommandTimeout = 120;
                // خدمة SQL Server قد تملك صلاحية ملف النسخة بينما حساب التطبيق لا
                // يستطيع فحصه محلياً. RESTORE VERIFYONLY هو المرجع الفعلي لصحة
                // الملف وإمكان وصول الخادم إليه، لذا لا يسبقه File.Exists مضلل.
                command.Parameters.Add("@BackupPath", SqlDbType.NVarChar, 260).Value = Path.GetFullPath(backupPath);
                masterConnection.Open();
                command.ExecuteNonQuery();
            }
        }

        public static void EnsureTargetIsClearlyNamedTestDatabase(SqlConnection connection)
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));
            bool mustClose = connection.State != ConnectionState.Open;
            try
            {
                if (mustClose) connection.Open();
                AssertClearlyNamedTestDatabase(connection);
            }
            finally { if (mustClose && connection.State != ConnectionState.Closed) connection.Close(); }
        }

        // أمر QA الصريح يعتمد على صلاحية SQL الفعلية، لا على عضوية Windows المحلية.
        // تبقى حواجز اسم قاعدة الاختبار والنسخة المتحققة إلزامية ومستقلة.
        public static void EnsureSchemaInstallPermission(SqlConnection connection)
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));
            bool mustClose = connection.State != ConnectionState.Open;
            try
            {
                if (mustClose) connection.Open();
                using (SqlCommand command = new SqlCommand(SchemaInstallPermissionSql, connection))
                {
                    if (Convert.ToInt32(command.ExecuteScalar()) != 1)
                        throw new UnauthorizedAccessException("حساب SQL الحالي لا يملك صلاحيات إنشاء وتعديل مخطط قاعدة QA المطلوبة للتثبيت.");
                }
            }
            finally { if (mustClose && connection.State != ConnectionState.Closed) connection.Close(); }
        }

        internal const string SchemaInstallPermissionSql = @"SELECT CASE WHEN
HAS_PERMS_BY_NAME(DB_NAME(), 'DATABASE', 'CREATE TABLE') = 1 AND
HAS_PERMS_BY_NAME(DB_NAME(), 'DATABASE', 'ALTER') = 1 AND
HAS_PERMS_BY_NAME(N'dbo', 'SCHEMA', 'ALTER') = 1
THEN 1 ELSE 0 END;";

        public static bool IsInstalled(SqlConnection connection)
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));
            bool mustClose = connection.State != ConnectionState.Open;
            if (mustClose) connection.Open();
            try
            {
                using (SqlCommand command = new SqlCommand("SELECT CASE WHEN OBJECT_ID('dbo.SalesInvoicesTb', 'U') IS NULL THEN 0 ELSE 1 END", connection))
                    return Convert.ToInt32(command.ExecuteScalar()) == 1;
            }
            finally { if (mustClose) connection.Close(); }
        }

        // لا يكفي وجود الجداول: لا يفتح البيع التجاري إلا بعد تسوية كل رصيد مجمع مع دفعاته في بيئة اختبار.
        public static bool IsFullCommercialMode(SqlConnection connection, SqlTransaction transaction)
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));
            const string sql = @"
IF OBJECT_ID('dbo.CommercialOperationalSettingsTb', 'U') IS NULL RETURN;
IF NOT EXISTS (SELECT 1 FROM dbo.CommercialOperationalSettingsTb WHERE SettingKey = N'Mode' AND SettingValue = N'FullCommercial') RETURN;
IF OBJECT_ID('dbo.SaudiFeatureSettingsTb', 'U') IS NULL RETURN;
IF NOT EXISTS (SELECT 1 FROM dbo.SaudiFeatureSettingsTb WHERE FeatureKey = N'CommercialSales' AND FeatureMode = N'Test') RETURN;
IF NOT EXISTS (SELECT 1 FROM dbo.SaudiFeatureSettingsTb WHERE FeatureKey = N'EInvoice' AND FeatureMode IN (N'Simulator', N'Test')) RETURN;
IF OBJECT_ID('dbo.CommercialEInvoiceSettingsTb', 'U') IS NULL RETURN;
IF NOT EXISTS (SELECT 1 FROM dbo.CommercialEInvoiceSettingsTb WHERE SettingsId = 1 AND LocalSandboxEnabled = 1 AND LEN(LTRIM(RTRIM(SellerName))) > 0 AND LEN(LTRIM(RTRIM(SellerVatNumber))) > 0) RETURN;
IF EXISTS (
    SELECT 1 FROM dbo.MedicineTb1 m
    OUTER APPLY (SELECT ISNULL(SUM(l.QuantityRemaining), 0) AS LotQuantity FROM dbo.InventoryLotsTb l WHERE l.MedName = m.MedName) lots
    WHERE CONVERT(DECIMAL(18,3), m.MedQty) <> lots.LotQuantity
) RETURN;
SELECT CAST(1 AS INT);";
            using (SqlCommand command = new SqlCommand(sql, connection, transaction))
            {
                object result = command.ExecuteScalar();
                return result != null && result != DBNull.Value && Convert.ToInt32(result) == 1;
            }
        }

        // يختلف عن جاهزية FullCommercial: نحتاج معرفة أن المشغّل طلب الوضع حتى
        // نمنع عمليات الرصيد المجمع التي قد تخرب تسوية الدفعات قبل الاعتماد.
        public static bool IsFullCommercialModeRequested(SqlConnection connection,SqlTransaction transaction)
            => PharmacyManagement.Infrastructure.SqlServer.SqlInventoryPostRepository.IsFullCommercialModeRequested(connection,transaction);

        // لا نسمح بالرجوع الصامت إلى البيع القديم إذا طلب المشغل الوضع التجاري لكن
        // التسوية لم تكتمل. الوضع Disabled وحده يصرح بالمبيعات القديمة.
        public static void EnsureRequestedCommercialModeIsReady(SqlConnection connection, SqlTransaction transaction)
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));
            if (IsFullCommercialModeRequested(connection, transaction) && !IsFullCommercialMode(connection, transaction))
                throw new InvalidOperationException("تم طلب وضع البيع التجاري، لكن تسوية الرصيد المجمع مع الدفعات غير مكتملة. أوقف البيع وصحح الجرد؛ لن يعود النظام تلقائياً إلى مسار البيع القديم.");
        }

        private static void AssertClearlyNamedTestDatabase(SqlConnection connection)
        {
            string databaseName;
            using (SqlCommand command = new SqlCommand("SELECT DB_NAME()", connection))
                databaseName = Convert.ToString(command.ExecuteScalar()) ?? string.Empty;
            if (!IsClearlyNamedTestDatabase(databaseName))
            {
                throw new InvalidOperationException("يجب أن ينتهي اسم قاعدة الاختبار بـ _test أو _qa أو _sandbox. تم إيقاف التثبيت لحماية بيانات التشغيل.");
            }
        }

        internal static bool IsClearlyNamedTestDatabase(string databaseName)
        {
            return !string.IsNullOrWhiteSpace(databaseName)
                && Regex.IsMatch(databaseName.Trim(), @".+_(test|qa|sandbox)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        private static void ValidateSchema(SqlConnection connection, SqlTransaction transaction)
        {
            const string validationSql = @"SELECT CASE WHEN OBJECT_ID('dbo.CommercialSchemaVersionsTb', 'U') IS NULL OR
 COL_LENGTH('dbo.SalesInvoicesTb', 'InvoiceId') IS NULL OR COL_LENGTH('dbo.SalesInvoicesTb', 'GrossTotal') IS NULL OR
 COL_LENGTH('dbo.SalesInvoiceLinesTb', 'TaxAmount') IS NULL OR COL_LENGTH('dbo.SalesInvoiceLinesTb', 'CostTotal') IS NULL OR COL_LENGTH('dbo.SalesInvoiceLinesTb', 'InvoiceDiscountAllocation') IS NULL OR COL_LENGTH('dbo.SalesInvoicesTb', 'CustomerNameSnapshot') IS NULL OR COL_LENGTH('dbo.SalesInvoicesTb', 'PriceTaxMode') IS NULL OR COL_LENGTH('dbo.InventoryLotsTb', 'BatchNumber') IS NULL OR
 OBJECT_ID('dbo.PrescriptionItemsTb', 'U') IS NULL OR OBJECT_ID('dbo.DispenseItemsTb', 'U') IS NULL OR
 OBJECT_ID('dbo.EInvoiceOutboxTb', 'U') IS NULL OR OBJECT_ID('dbo.CommercialEInvoiceSettingsTb', 'U') IS NULL OR OBJECT_ID('dbo.CommercialCountersTb', 'U') IS NULL OR OBJECT_ID('dbo.SaudiFeatureSettingsTb', 'U') IS NULL OR OBJECT_ID('dbo.SaudiFeatureSettingsAuditTb', 'U') IS NULL OR
 NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_SalesInvoicesTb_Customer') OR NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_SalesInvoicesTb_Patient') OR
 NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_SalesInvoiceLinesTb_Medicine') OR NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_InventoryLotsTb_Medicine') OR
 NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_InventoryLotsTb_PurchaseDetail') THEN 0 ELSE 1 END";
            using (SqlCommand command = new SqlCommand(validationSql, connection, transaction))
            {
                if (Convert.ToInt32(command.ExecuteScalar()) != 1)
                    throw new InvalidOperationException("مخطط النظام التجاري الموجود غير مكتمل أو مختلف عن الإصدار المطلوب؛ لم يتم اعتماد الترحيل.");
            }
        }

        // بيانات تاريخية لا تُنسخ إلى InventoryLots؛ تبدأ الدفعات فقط من الاستلامات التي تُعتمد بعد الترحيل المتحقق.
        internal const string SchemaSql = @"
IF OBJECT_ID('dbo.CommercialSchemaVersionsTb', 'U') IS NULL CREATE TABLE dbo.CommercialSchemaVersionsTb (VersionNo INT NOT NULL PRIMARY KEY, AppliedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_CommercialSchemaVersionsTb_AppliedAtUtc DEFAULT SYSUTCDATETIME());
IF NOT EXISTS (SELECT 1 FROM dbo.CommercialSchemaVersionsTb WHERE VersionNo = 1) INSERT INTO dbo.CommercialSchemaVersionsTb (VersionNo) VALUES (1);
IF NOT EXISTS (SELECT 1 FROM dbo.CommercialSchemaVersionsTb WHERE VersionNo = 2) INSERT INTO dbo.CommercialSchemaVersionsTb (VersionNo) VALUES (2);
IF NOT EXISTS (SELECT 1 FROM dbo.CommercialSchemaVersionsTb WHERE VersionNo = 3) INSERT INTO dbo.CommercialSchemaVersionsTb (VersionNo) VALUES (3);
IF OBJECT_ID('dbo.CommercialOperationalSettingsTb', 'U') IS NULL CREATE TABLE dbo.CommercialOperationalSettingsTb (SettingKey NVARCHAR(50) NOT NULL PRIMARY KEY, SettingValue NVARCHAR(100) NOT NULL, UpdatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_CommercialOperationalSettingsTb_UpdatedAtUtc DEFAULT SYSUTCDATETIME());
IF NOT EXISTS (SELECT 1 FROM dbo.CommercialOperationalSettingsTb WHERE SettingKey = N'Mode') INSERT INTO dbo.CommercialOperationalSettingsTb (SettingKey, SettingValue) VALUES (N'Mode', N'Disabled');
IF OBJECT_ID('dbo.SaudiFeatureSettingsTb', 'U') IS NULL CREATE TABLE dbo.SaudiFeatureSettingsTb (FeatureKey NVARCHAR(80) NOT NULL CONSTRAINT PK_SaudiFeatureSettingsTb PRIMARY KEY, FeatureMode NVARCHAR(20) NOT NULL, UpdatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_SaudiFeatureSettingsTb_UpdatedAtUtc DEFAULT SYSUTCDATETIME(), UpdatedBy NVARCHAR(200) NOT NULL CONSTRAINT DF_SaudiFeatureSettingsTb_UpdatedBy DEFAULT N'System', CONSTRAINT CK_SaudiFeatureSettingsTb_Key CHECK (FeatureKey IN (N'CommercialSales', N'EInvoice', N'Rsd', N'Nphies')), CONSTRAINT CK_SaudiFeatureSettingsTb_Mode CHECK (FeatureMode IN (N'Disabled', N'Simulator', N'Test')));
IF OBJECT_ID('dbo.SaudiFeatureSettingsAuditTb', 'U') IS NULL CREATE TABLE dbo.SaudiFeatureSettingsAuditTb (AuditId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_SaudiFeatureSettingsAuditTb PRIMARY KEY, FeatureKey NVARCHAR(80) NOT NULL, PreviousMode NVARCHAR(20) NOT NULL, NewMode NVARCHAR(20) NOT NULL, ChangeReason NVARCHAR(500) NOT NULL, ChangedBy NVARCHAR(200) NOT NULL, ChangedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_SaudiFeatureSettingsAuditTb_ChangedAtUtc DEFAULT SYSUTCDATETIME(), CONSTRAINT FK_SaudiFeatureSettingsAuditTb_Feature FOREIGN KEY (FeatureKey) REFERENCES dbo.SaudiFeatureSettingsTb(FeatureKey));
IF NOT EXISTS (SELECT 1 FROM dbo.SaudiFeatureSettingsTb WHERE FeatureKey=N'CommercialSales') INSERT INTO dbo.SaudiFeatureSettingsTb (FeatureKey, FeatureMode) VALUES (N'CommercialSales', N'Disabled');
IF NOT EXISTS (SELECT 1 FROM dbo.SaudiFeatureSettingsTb WHERE FeatureKey=N'EInvoice') INSERT INTO dbo.SaudiFeatureSettingsTb (FeatureKey, FeatureMode) VALUES (N'EInvoice', N'Disabled');
IF NOT EXISTS (SELECT 1 FROM dbo.SaudiFeatureSettingsTb WHERE FeatureKey=N'Rsd') INSERT INTO dbo.SaudiFeatureSettingsTb (FeatureKey, FeatureMode) VALUES (N'Rsd', N'Disabled');
IF NOT EXISTS (SELECT 1 FROM dbo.SaudiFeatureSettingsTb WHERE FeatureKey=N'Nphies') INSERT INTO dbo.SaudiFeatureSettingsTb (FeatureKey, FeatureMode) VALUES (N'Nphies', N'Disabled');
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_SaudiFeatureSettingsAuditTb_Feature_ChangedAtUtc' AND object_id=OBJECT_ID(N'dbo.SaudiFeatureSettingsAuditTb')) CREATE INDEX IX_SaudiFeatureSettingsAuditTb_Feature_ChangedAtUtc ON dbo.SaudiFeatureSettingsAuditTb(FeatureKey, ChangedAtUtc DESC);
IF OBJECT_ID('dbo.trg_SaudiFeatureSettingsAuditTb_AppendOnly', 'TR') IS NULL EXEC('CREATE TRIGGER dbo.trg_SaudiFeatureSettingsAuditTb_AppendOnly ON dbo.SaudiFeatureSettingsAuditTb AFTER UPDATE, DELETE AS BEGIN SET NOCOUNT ON; RAISERROR(''Saudi feature-setting audit records are append-only.'', 16, 1); ROLLBACK TRANSACTION; RETURN; END');
IF OBJECT_ID('dbo.CommercialEInvoiceSettingsTb', 'U') IS NULL CREATE TABLE dbo.CommercialEInvoiceSettingsTb (SettingsId INT NOT NULL CONSTRAINT PK_CommercialEInvoiceSettingsTb PRIMARY KEY CONSTRAINT CK_CommercialEInvoiceSettingsTb_Singleton CHECK (SettingsId = 1), SellerName NVARCHAR(200) NULL, SellerVatNumber NVARCHAR(50) NULL, LocalSandboxEnabled BIT NOT NULL CONSTRAINT DF_CommercialEInvoiceSettingsTb_LocalSandboxEnabled DEFAULT 0, UpdatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_CommercialEInvoiceSettingsTb_UpdatedAtUtc DEFAULT SYSUTCDATETIME());
IF NOT EXISTS (SELECT 1 FROM dbo.CommercialEInvoiceSettingsTb WHERE SettingsId = 1) INSERT INTO dbo.CommercialEInvoiceSettingsTb (SettingsId, LocalSandboxEnabled) VALUES (1, 0);
IF OBJECT_ID('dbo.CommercialCountersTb', 'U') IS NULL CREATE TABLE dbo.CommercialCountersTb (CounterKey NVARCHAR(50) NOT NULL PRIMARY KEY, NextValue BIGINT NOT NULL CONSTRAINT CK_CommercialCountersTb_NextValue CHECK (NextValue > 0));
IF NOT EXISTS (SELECT 1 FROM dbo.CommercialCountersTb WHERE CounterKey = N'SalesInvoice') INSERT INTO dbo.CommercialCountersTb (CounterKey, NextValue) VALUES (N'SalesInvoice', 1);
IF OBJECT_ID('dbo.SalesInvoicesTb', 'U') IS NULL
CREATE TABLE dbo.SalesInvoicesTb (
 InvoiceId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY, InvoiceNumber BIGINT NOT NULL, InvoiceType NVARCHAR(30) NOT NULL,
 Status NVARCHAR(20) NOT NULL, OriginalInvoiceId UNIQUEIDENTIFIER NULL, CustomerId NVARCHAR(50) NULL, CustomerNameSnapshot NVARCHAR(200) NULL, CustomerPhoneSnapshot NVARCHAR(50) NULL, CustomerAddressSnapshot NVARCHAR(300) NULL, PatientId NVARCHAR(50) NULL,
 CurrencyCode NVARCHAR(10) NOT NULL, ExchangeRate DECIMAL(18,6) NOT NULL, PriceTaxMode NVARCHAR(20) NOT NULL CONSTRAINT DF_SalesInvoicesTb_PriceTaxMode DEFAULT N'Exclusive', GrossTotal DECIMAL(18,2) NOT NULL,
 DiscountTotal DECIMAL(18,2) NOT NULL, TaxTotal DECIMAL(18,2) NOT NULL, NetTotal DECIMAL(18,2) NOT NULL,
 IssuedAtUtc DATETIME2 NOT NULL, IssuedBy NVARCHAR(200) NOT NULL, Notes NVARCHAR(1000) NULL,
 CONSTRAINT UX_SalesInvoicesTb_Number UNIQUE (InvoiceNumber), CONSTRAINT FK_SalesInvoicesTb_OriginalInvoice FOREIGN KEY (OriginalInvoiceId) REFERENCES dbo.SalesInvoicesTb(InvoiceId),
 CONSTRAINT CK_SalesInvoicesTb_Totals CHECK (GrossTotal >= 0 AND DiscountTotal >= 0 AND TaxTotal >= 0 AND NetTotal >= 0));
IF COL_LENGTH('dbo.SalesInvoicesTb', 'CustomerNameSnapshot') IS NULL ALTER TABLE dbo.SalesInvoicesTb ADD CustomerNameSnapshot NVARCHAR(200) NULL;
IF COL_LENGTH('dbo.SalesInvoicesTb', 'CustomerPhoneSnapshot') IS NULL ALTER TABLE dbo.SalesInvoicesTb ADD CustomerPhoneSnapshot NVARCHAR(50) NULL;
IF COL_LENGTH('dbo.SalesInvoicesTb', 'CustomerAddressSnapshot') IS NULL ALTER TABLE dbo.SalesInvoicesTb ADD CustomerAddressSnapshot NVARCHAR(300) NULL;
IF COL_LENGTH('dbo.SalesInvoicesTb', 'PriceTaxMode') IS NULL ALTER TABLE dbo.SalesInvoicesTb ADD PriceTaxMode NVARCHAR(20) NOT NULL CONSTRAINT DF_SalesInvoicesTb_PriceTaxMode DEFAULT N'Exclusive' WITH VALUES;
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_SalesInvoicesTb_PriceTaxMode' AND parent_object_id = OBJECT_ID(N'dbo.SalesInvoicesTb')) ALTER TABLE dbo.SalesInvoicesTb ADD CONSTRAINT CK_SalesInvoicesTb_PriceTaxMode CHECK (PriceTaxMode IN (N'Exclusive', N'Inclusive'));
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_SalesInvoicesTb_TotalEquation' AND parent_object_id = OBJECT_ID(N'dbo.SalesInvoicesTb')) ALTER TABLE dbo.SalesInvoicesTb ADD CONSTRAINT CK_SalesInvoicesTb_TotalEquation CHECK ((PriceTaxMode = N'Inclusive' AND GrossTotal - DiscountTotal = NetTotal) OR (PriceTaxMode = N'Exclusive' AND GrossTotal - DiscountTotal + TaxTotal = NetTotal));
IF OBJECT_ID('dbo.SalesInvoiceLinesTb', 'U') IS NULL
CREATE TABLE dbo.SalesInvoiceLinesTb (
 LineId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY, InvoiceId UNIQUEIDENTIFIER NOT NULL, LineNumber INT NOT NULL,
 MedName NVARCHAR(200) NOT NULL, Gtin NVARCHAR(20) NULL, Quantity DECIMAL(18,3) NOT NULL,
 UnitPrice DECIMAL(18,2) NOT NULL, DiscountAmount DECIMAL(18,2) NOT NULL, InvoiceDiscountAllocation DECIMAL(18,2) NOT NULL, TaxRate DECIMAL(9,4) NOT NULL,
 TaxAmount DECIMAL(18,2) NOT NULL, LineTotal DECIMAL(18,2) NOT NULL, UnitCost DECIMAL(18,2) NULL, CostTotal DECIMAL(18,2) NOT NULL,
 CONSTRAINT FK_SalesInvoiceLinesTb_Invoice FOREIGN KEY (InvoiceId) REFERENCES dbo.SalesInvoicesTb(InvoiceId),
 CONSTRAINT UX_SalesInvoiceLinesTb_InvoiceLine UNIQUE (InvoiceId, LineNumber),
 CONSTRAINT CK_SalesInvoiceLinesTb_Quantity CHECK (Quantity > 0));
IF COL_LENGTH('dbo.SalesInvoiceLinesTb', 'CostTotal') IS NULL ALTER TABLE dbo.SalesInvoiceLinesTb ADD CostTotal DECIMAL(18,2) NOT NULL CONSTRAINT DF_SalesInvoiceLinesTb_CostTotal DEFAULT 0 WITH VALUES;
IF COL_LENGTH('dbo.SalesInvoiceLinesTb', 'InvoiceDiscountAllocation') IS NULL ALTER TABLE dbo.SalesInvoiceLinesTb ADD InvoiceDiscountAllocation DECIMAL(18,2) NOT NULL CONSTRAINT DF_SalesInvoiceLinesTb_InvoiceDiscountAllocation DEFAULT 0 WITH VALUES;
IF OBJECT_ID('dbo.InventoryLotsTb', 'U') IS NULL
CREATE TABLE dbo.InventoryLotsTb (
 LotId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY, MedName NVARCHAR(200) NOT NULL, Gtin NVARCHAR(20) NULL,
 SerialNumber NVARCHAR(100) NULL, BatchNumber NVARCHAR(100) NOT NULL, ProductionDate DATE NULL, ExpiryDate DATE NOT NULL,
 SupplierName NVARCHAR(200) NULL, PurchaseDetailId NVARCHAR(50) NULL, UnitCost DECIMAL(18,2) NOT NULL,
 QuantityReceived DECIMAL(18,3) NOT NULL, QuantityRemaining DECIMAL(18,3) NOT NULL, IsRecalled BIT NOT NULL CONSTRAINT DF_InventoryLotsTb_IsRecalled DEFAULT 0,
 CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_InventoryLotsTb_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
 CONSTRAINT CK_InventoryLotsTb_Quantity CHECK (QuantityReceived >= 0 AND QuantityRemaining >= 0 AND QuantityRemaining <= QuantityReceived),
 CONSTRAINT CK_InventoryLotsTb_Dates CHECK (ProductionDate IS NULL OR ExpiryDate > ProductionDate));
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_InventoryLotsTb_Fefo' AND object_id=OBJECT_ID('dbo.InventoryLotsTb'))
CREATE INDEX IX_InventoryLotsTb_Fefo ON dbo.InventoryLotsTb(MedName, IsRecalled, ExpiryDate, QuantityRemaining);
IF OBJECT_ID('dbo.LotAllocationsTb', 'U') IS NULL
CREATE TABLE dbo.LotAllocationsTb (
 AllocationId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY, InvoiceLineId UNIQUEIDENTIFIER NULL, LotId UNIQUEIDENTIFIER NOT NULL,
 MovementType NVARCHAR(30) NOT NULL, Quantity DECIMAL(18,3) NOT NULL, UnitCost DECIMAL(18,2) NOT NULL, CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_LotAllocationsTb_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
 CONSTRAINT FK_LotAllocationsTb_Lot FOREIGN KEY (LotId) REFERENCES dbo.InventoryLotsTb(LotId),
 CONSTRAINT FK_LotAllocationsTb_Line FOREIGN KEY (InvoiceLineId) REFERENCES dbo.SalesInvoiceLinesTb(LineId), CONSTRAINT CK_LotAllocationsTb_Quantity CHECK (Quantity > 0));
IF OBJECT_ID('dbo.EInvoiceOutboxTb', 'U') IS NULL
CREATE TABLE dbo.EInvoiceOutboxTb (
 OutboxId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY, InvoiceId UNIQUEIDENTIFIER NOT NULL, Uuid UNIQUEIDENTIFIER NOT NULL,
 CounterValue BIGINT NOT NULL, PreviousHash NVARCHAR(200) NULL, DocumentHash NVARCHAR(200) NOT NULL, XmlPayload NVARCHAR(MAX) NOT NULL,
 QrTlvBase64 NVARCHAR(MAX) NULL, Status NVARCHAR(30) NOT NULL, AttemptCount INT NOT NULL CONSTRAINT DF_EInvoiceOutboxTb_AttemptCount DEFAULT 0,
 LastError NVARCHAR(1000) NULL, CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_EInvoiceOutboxTb_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
 CONSTRAINT UX_EInvoiceOutboxTb_Invoice UNIQUE (InvoiceId), CONSTRAINT UX_EInvoiceOutboxTb_Counter UNIQUE(CounterValue), CONSTRAINT FK_EInvoiceOutboxTb_Invoice FOREIGN KEY (InvoiceId) REFERENCES dbo.SalesInvoicesTb(InvoiceId));
IF OBJECT_ID('dbo.PatientsTb', 'U') IS NULL
CREATE TABLE dbo.PatientsTb (PatientId NVARCHAR(50) NOT NULL PRIMARY KEY, FullName NVARCHAR(200) NOT NULL, Mobile NVARCHAR(50) NULL, Allergies NVARCHAR(MAX) NULL, CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_PatientsTb_CreatedAtUtc DEFAULT SYSUTCDATETIME());
IF OBJECT_ID('dbo.PrescriptionsTb', 'U') IS NULL
CREATE TABLE dbo.PrescriptionsTb (PrescriptionId NVARCHAR(80) NOT NULL PRIMARY KEY, PatientId NVARCHAR(50) NOT NULL, PrescriberName NVARCHAR(200) NOT NULL, IssuedAtUtc DATETIME2 NOT NULL, ExpiresAtUtc DATETIME2 NOT NULL, Status NVARCHAR(30) NOT NULL, RawPayload NVARCHAR(MAX) NULL, CONSTRAINT FK_PrescriptionsTb_Patient FOREIGN KEY (PatientId) REFERENCES dbo.PatientsTb(PatientId));
IF OBJECT_ID('dbo.PrescriptionItemsTb', 'U') IS NULL
CREATE TABLE dbo.PrescriptionItemsTb (PrescriptionItemId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY, PrescriptionId NVARCHAR(80) NOT NULL, DrugCode NVARCHAR(100) NOT NULL, DrugName NVARCHAR(200) NULL, PrescribedQuantity DECIMAL(18,3) NOT NULL, DispensedQuantity DECIMAL(18,3) NOT NULL CONSTRAINT DF_PrescriptionItemsTb_DispensedQuantity DEFAULT 0, CONSTRAINT FK_PrescriptionItemsTb_Prescription FOREIGN KEY (PrescriptionId) REFERENCES dbo.PrescriptionsTb(PrescriptionId), CONSTRAINT CK_PrescriptionItemsTb_Quantity CHECK (PrescribedQuantity > 0 AND DispensedQuantity >= 0 AND DispensedQuantity <= PrescribedQuantity));
IF OBJECT_ID('dbo.DispensesTb', 'U') IS NULL
CREATE TABLE dbo.DispensesTb (DispenseId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY, PrescriptionId NVARCHAR(80) NOT NULL, InvoiceId UNIQUEIDENTIFIER NULL, DispensedBy NVARCHAR(200) NOT NULL, DispensedAtUtc DATETIME2 NOT NULL, AuditNote NVARCHAR(1000) NULL, CONSTRAINT FK_DispensesTb_Prescription FOREIGN KEY (PrescriptionId) REFERENCES dbo.PrescriptionsTb(PrescriptionId), CONSTRAINT FK_DispensesTb_Invoice FOREIGN KEY (InvoiceId) REFERENCES dbo.SalesInvoicesTb(InvoiceId));
IF OBJECT_ID('dbo.DispenseItemsTb', 'U') IS NULL
CREATE TABLE dbo.DispenseItemsTb (DispenseItemId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY, DispenseId UNIQUEIDENTIFIER NOT NULL, PrescriptionItemId UNIQUEIDENTIFIER NOT NULL, Quantity DECIMAL(18,3) NOT NULL, CONSTRAINT FK_DispenseItemsTb_Dispense FOREIGN KEY (DispenseId) REFERENCES dbo.DispensesTb(DispenseId), CONSTRAINT FK_DispenseItemsTb_PrescriptionItem FOREIGN KEY (PrescriptionItemId) REFERENCES dbo.PrescriptionItemsTb(PrescriptionItemId), CONSTRAINT CK_DispenseItemsTb_Quantity CHECK (Quantity > 0));
IF OBJECT_ID('dbo.ClaimsTb', 'U') IS NULL
CREATE TABLE dbo.ClaimsTb (ClaimId NVARCHAR(80) NOT NULL PRIMARY KEY, PatientId NVARCHAR(50) NOT NULL, InvoiceId UNIQUEIDENTIFIER NULL, PayerCode NVARCHAR(80) NOT NULL, Status NVARCHAR(30) NOT NULL, Amount DECIMAL(18,2) NOT NULL, ExternalReference NVARCHAR(200) NULL, LastError NVARCHAR(1000) NULL, CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_ClaimsTb_CreatedAtUtc DEFAULT SYSUTCDATETIME(), CONSTRAINT FK_ClaimsTb_Patient FOREIGN KEY (PatientId) REFERENCES dbo.PatientsTb(PatientId), CONSTRAINT FK_ClaimsTb_Invoice FOREIGN KEY (InvoiceId) REFERENCES dbo.SalesInvoicesTb(InvoiceId));
IF OBJECT_ID('dbo.PriorAuthorizationsTb', 'U') IS NULL
CREATE TABLE dbo.PriorAuthorizationsTb (RequestId NVARCHAR(80) NOT NULL PRIMARY KEY, PatientId NVARCHAR(50) NOT NULL, DrugCode NVARCHAR(100) NOT NULL, Status NVARCHAR(30) NOT NULL, ExternalReference NVARCHAR(200) NULL, LastError NVARCHAR(1000) NULL, CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_PriorAuthorizationsTb_CreatedAtUtc DEFAULT SYSUTCDATETIME(), CONSTRAINT FK_PriorAuthorizationsTb_Patient FOREIGN KEY (PatientId) REFERENCES dbo.PatientsTb(PatientId));
IF OBJECT_ID('dbo.IntegrationOutboxTb', 'U') IS NULL
CREATE TABLE dbo.IntegrationOutboxTb (OutboxId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY, MessageType NVARCHAR(80) NOT NULL, AggregateId NVARCHAR(100) NOT NULL, Payload NVARCHAR(MAX) NOT NULL, Status NVARCHAR(30) NOT NULL, AttemptCount INT NOT NULL CONSTRAINT DF_IntegrationOutboxTb_AttemptCount DEFAULT 0, NextAttemptAtUtc DATETIME2 NULL, LastError NVARCHAR(1000) NULL, CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_IntegrationOutboxTb_CreatedAtUtc DEFAULT SYSUTCDATETIME());
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_SalesInvoicesTb_Customer') ALTER TABLE dbo.SalesInvoicesTb WITH CHECK ADD CONSTRAINT FK_SalesInvoicesTb_Customer FOREIGN KEY (CustomerId) REFERENCES dbo.CustomersBillsTb(CustomerId);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_SalesInvoicesTb_Patient') ALTER TABLE dbo.SalesInvoicesTb WITH CHECK ADD CONSTRAINT FK_SalesInvoicesTb_Patient FOREIGN KEY (PatientId) REFERENCES dbo.PatientsTb(PatientId);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_SalesInvoiceLinesTb_Medicine') ALTER TABLE dbo.SalesInvoiceLinesTb WITH CHECK ADD CONSTRAINT FK_SalesInvoiceLinesTb_Medicine FOREIGN KEY (MedName) REFERENCES dbo.MedicineTb1(MedName);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_InventoryLotsTb_Medicine') ALTER TABLE dbo.InventoryLotsTb WITH CHECK ADD CONSTRAINT FK_InventoryLotsTb_Medicine FOREIGN KEY (MedName) REFERENCES dbo.MedicineTb1(MedName);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_InventoryLotsTb_PurchaseDetail') ALTER TABLE dbo.InventoryLotsTb WITH CHECK ADD CONSTRAINT FK_InventoryLotsTb_PurchaseDetail FOREIGN KEY (PurchaseDetailId) REFERENCES dbo.PurchaseDetailsTb(DetailId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_SalesInvoicesTb_Customer_IssuedAtUtc' AND object_id=OBJECT_ID(N'dbo.SalesInvoicesTb')) CREATE INDEX IX_SalesInvoicesTb_Customer_IssuedAtUtc ON dbo.SalesInvoicesTb(CustomerId, IssuedAtUtc DESC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_SalesInvoicesTb_Patient_IssuedAtUtc' AND object_id=OBJECT_ID(N'dbo.SalesInvoicesTb')) CREATE INDEX IX_SalesInvoicesTb_Patient_IssuedAtUtc ON dbo.SalesInvoicesTb(PatientId, IssuedAtUtc DESC) WHERE PatientId IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_SalesInvoiceLinesTb_MedName' AND object_id=OBJECT_ID(N'dbo.SalesInvoiceLinesTb')) CREATE INDEX IX_SalesInvoiceLinesTb_MedName ON dbo.SalesInvoiceLinesTb(MedName);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_InventoryLotsTb_PurchaseDetailId' AND object_id=OBJECT_ID(N'dbo.InventoryLotsTb')) CREATE INDEX IX_InventoryLotsTb_PurchaseDetailId ON dbo.InventoryLotsTb(PurchaseDetailId) WHERE PurchaseDetailId IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_IntegrationOutboxTb_Status_NextAttemptAtUtc' AND object_id=OBJECT_ID(N'dbo.IntegrationOutboxTb')) CREATE INDEX IX_IntegrationOutboxTb_Status_NextAttemptAtUtc ON dbo.IntegrationOutboxTb(Status, NextAttemptAtUtc) WHERE Status IN (N'Pending', N'Retry');
IF OBJECT_ID('dbo.trg_SalesInvoicesTb_Immutable', 'TR') IS NULL EXEC('CREATE TRIGGER dbo.trg_SalesInvoicesTb_Immutable ON dbo.SalesInvoicesTb AFTER UPDATE, DELETE AS BEGIN SET NOCOUNT ON; IF EXISTS (SELECT 1 FROM deleted WHERE Status = ''Issued'') BEGIN RAISERROR(''Issued invoices are immutable; use a credit or debit note.'', 16, 1); ROLLBACK TRANSACTION; RETURN; END END');
IF OBJECT_ID('dbo.trg_SalesInvoiceLinesTb_Immutable', 'TR') IS NULL EXEC('CREATE TRIGGER dbo.trg_SalesInvoiceLinesTb_Immutable ON dbo.SalesInvoiceLinesTb AFTER INSERT, UPDATE, DELETE AS BEGIN SET NOCOUNT ON; IF EXISTS (SELECT 1 FROM inserted i INNER JOIN dbo.SalesInvoicesTb h ON h.InvoiceId=i.InvoiceId WHERE h.Status=''Issued'') OR EXISTS (SELECT 1 FROM deleted d INNER JOIN dbo.SalesInvoicesTb h ON h.InvoiceId=d.InvoiceId WHERE h.Status=''Issued'') BEGIN RAISERROR(''Issued invoice lines are immutable.'', 16, 1); ROLLBACK TRANSACTION; RETURN; END END');
IF OBJECT_ID('dbo.trg_LotAllocationsTb_Immutable', 'TR') IS NULL EXEC('CREATE TRIGGER dbo.trg_LotAllocationsTb_Immutable ON dbo.LotAllocationsTb AFTER INSERT, UPDATE, DELETE AS BEGIN SET NOCOUNT ON; IF EXISTS (SELECT 1 FROM inserted a INNER JOIN dbo.SalesInvoiceLinesTb l ON l.LineId=a.InvoiceLineId INNER JOIN dbo.SalesInvoicesTb h ON h.InvoiceId=l.InvoiceId WHERE h.Status=''Issued'') OR EXISTS (SELECT 1 FROM deleted a INNER JOIN dbo.SalesInvoiceLinesTb l ON l.LineId=a.InvoiceLineId INNER JOIN dbo.SalesInvoicesTb h ON h.InvoiceId=l.InvoiceId WHERE h.Status=''Issued'') BEGIN RAISERROR(''Issued invoice lot allocations are immutable.'', 16, 1); ROLLBACK TRANSACTION; RETURN; END END');
IF OBJECT_ID('dbo.trg_EInvoiceOutboxTb_Immutable', 'TR') IS NULL EXEC('CREATE TRIGGER dbo.trg_EInvoiceOutboxTb_Immutable ON dbo.EInvoiceOutboxTb AFTER UPDATE, DELETE AS BEGIN SET NOCOUNT ON; RAISERROR(''Electronic invoice artifacts are append-only.'', 16, 1); ROLLBACK TRANSACTION; RETURN; END');
";
    }
}

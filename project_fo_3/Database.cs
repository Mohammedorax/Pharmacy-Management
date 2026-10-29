// شرح الملف: يحتوي على كل منطق الاتصال بـ SQL Server وتهيئة قاعدة البيانات والجداول.
// يقلل تكرار نص الاتصال داخل الشاشات ويسهل تعديل إعدادات الربط من مكان واحد.
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.Sql;
using System.Data.SqlClient;

namespace project_fo_3
{
    // نتيجة قراءة فقط قبل تغيير الاتصال. لا تنشئ قاعدة أو جداول ولا تعدل بيانات.
    internal sealed class DatabaseInspectionResult
    {
        public bool DatabaseExists { get; private set; }
        public int ExistingCoreTableCount { get; private set; }

        public DatabaseInspectionResult(bool databaseExists, int existingCoreTableCount)
        {
            DatabaseExists = databaseExists;
            ExistingCoreTableCount = existingCoreTableCount;
        }
    }

    // حالة بداية مختصرة للواجهة الرئيسية، تستمد من قاعدة التشغيل الحالية فقط.
    internal sealed class DatabaseStartupState
    {
        public long MedicineCount { get; private set; }
        public long CompanyCount { get; private set; }
        public long PurchaseCount { get; private set; }
        public long SaleLineCount { get; private set; }

        public bool IsNewInstallation
        {
            get { return MedicineCount == 0 && CompanyCount == 0 && PurchaseCount == 0 && SaleLineCount == 0; }
        }

        public DatabaseStartupState(long medicineCount, long companyCount, long purchaseCount, long saleLineCount)
        {
            MedicineCount = medicineCount;
            CompanyCount = companyCount;
            PurchaseCount = purchaseCount;
            SaleLineCount = saleLineCount;
        }
    }

    // يدير اتصال SQL Server وإنشاء قاعدة بيانات الصيدلية والجداول الأساسية.
    // الهدف من هذا الملف هو منع تكرار connection string داخل الشاشات وتحسين قابلية النقل بين الأجهزة.
    internal static class Database
    {
        // اسم الاتصال المستخدم داخل App.config.
        private const string ConnectionName = "PharmacyConnection";

        // يمنع إعادة إنشاء القاعدة والجداول في كل عملية بعد نجاح التهيئة.
        private static bool isReady;
        private static readonly object operationalReadinessSync = new object();
        private static readonly HashSet<string> operationalReadyConnections = new HashSet<string>(StringComparer.Ordinal);
        private static long operationalReadinessRevision;

        // يتيح سطر الأوامر وسكربت التثبيت إنشاء قاعدة جديدة من المصدر نفسه الذي يستخدمه التطبيق.
        // لا نحتفظ بنسخة SQL مستقلة حتى لا تنفصل الجداول والقيم الافتراضية عن التطبيق مع الوقت.
        public static DatabaseSchemaValidationResult InitializeCoreSchema(string serverName, string databaseName)
        {
            string connectionString = BuildSettingsConnectionString(serverName, databaseName, true);
            ResetOperationalReadiness();
            EnsureDatabaseExists(connectionString);
            EnsureSchemaExists(connectionString);
            return ValidateOperationalCoreSchema(connectionString);
        }

        public static DatabaseSchemaValidationResult ValidateConfiguredCoreSchema()
        {
            DatabaseSchemaValidationResult result = ValidateOperationalCoreSchema(ConnectionString);
            isReady = true;
            return result;
        }

        // فحص مباشر ومقيد لقاعدة QA: لا ينشئ قاعدة أو جداول ولا يغيّر إعداد اتصال التشغيل.
        internal static DatabaseSchemaValidationResult ValidateQaCoreSchemaReadOnly(string serverName, string databaseName)
        {
            EnsureUserDatabaseName(databaseName);
            if (!databaseName.Trim().EndsWith("_qa", StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException("فحص QA المباشر مسموح فقط لقاعدة ينتهي اسمها بـ _qa.");
            }

            return ValidateCoreSchema(BuildSettingsConnectionString(serverName, databaseName, true));
        }

        private static DatabaseSchemaValidationResult ValidateCoreSchema(string connectionString)
        {
            const string validationSql = @"
SELECT
    DB_NAME() AS DatabaseName,
    CASE WHEN
        OBJECT_ID('dbo.CoreSchemaVersionsTb', 'U') IS NOT NULL AND
        OBJECT_ID('dbo.CompanyTb1', 'U') IS NOT NULL AND
        OBJECT_ID('dbo.EmployeeTb1', 'U') IS NOT NULL AND
        OBJECT_ID('dbo.MedicineTb1', 'U') IS NOT NULL AND
        OBJECT_ID('dbo.BillsTb1', 'U') IS NOT NULL AND
        OBJECT_ID('dbo.PurchasesTb', 'U') IS NOT NULL AND
        OBJECT_ID('dbo.PurchaseDetailsTb', 'U') IS NOT NULL AND
        OBJECT_ID('dbo.CustomersBillsTb', 'U') IS NOT NULL AND
        OBJECT_ID('dbo.PharmacyProfileTb', 'U') IS NOT NULL AND
        OBJECT_ID('dbo.CurrencySettingsTb', 'U') IS NOT NULL AND
        OBJECT_ID('dbo.AuditLogTb', 'U') IS NOT NULL AND
        OBJECT_ID('dbo.InventoryMovementsTb', 'U') IS NOT NULL AND
        OBJECT_ID('dbo.AccountsTb', 'U') IS NOT NULL AND
        OBJECT_ID('dbo.CostCentersTb', 'U') IS NOT NULL AND
        OBJECT_ID('dbo.JournalEntriesTb', 'U') IS NOT NULL AND
        OBJECT_ID('dbo.JournalEntryLinesTb', 'U') IS NOT NULL AND
        OBJECT_ID('dbo.AccountingVouchersTb', 'U') IS NOT NULL AND
        OBJECT_ID('dbo.TaxSettingsTb', 'U') IS NOT NULL AND
        OBJECT_ID('dbo.Seq_BillId', 'SO') IS NOT NULL AND
        OBJECT_ID('dbo.Seq_CustomerId', 'SO') IS NOT NULL AND
        OBJECT_ID('dbo.Seq_PurchaseId', 'SO') IS NOT NULL AND
        OBJECT_ID('dbo.Seq_PurchaseDetailId', 'SO') IS NOT NULL AND
        COL_LENGTH('dbo.EmployeeTb1', 'EmpRole') IS NOT NULL AND
        COL_LENGTH('dbo.MedicineTb1', 'Barcode') IS NOT NULL AND
        COL_LENGTH('dbo.MedicineTb1', 'ProdDate') IS NOT NULL AND
        COL_LENGTH('dbo.PurchaseDetailsTb', 'ProdDate') IS NOT NULL AND
        COL_LENGTH('dbo.PurchaseDetailsTb', 'ExpDate') IS NOT NULL AND
        (SELECT COUNT(1) FROM dbo.CurrencySettingsTb WHERE IsSystemCurrency = 1) = 1 AND
        EXISTS (SELECT 1 FROM dbo.CurrencySettingsTb WHERE IsSystemCurrency = 1 AND NULLIF(LTRIM(RTRIM(CurrencyCode)), N'') IS NOT NULL) AND
        EXISTS (SELECT 1 FROM dbo.TaxSettingsTb) AND
        EXISTS (SELECT 1 FROM dbo.CostCentersTb) AND
        NOT EXISTS (
            SELECT AccountCode
            FROM (VALUES (N'1000'), (N'1100'), (N'1200'), (N'2000'), (N'2100'), (N'4000'), (N'5000')) required(AccountCode)
            WHERE NOT EXISTS (SELECT 1 FROM dbo.AccountsTb a WHERE a.AccountCode = required.AccountCode)
        ) AND
        EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_PurchaseDetails_Purchases') AND
        EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_JournalEntryLines_JournalEntries') AND
        EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_JournalEntryLines_Accounts') AND
        EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_AccountingVouchers_JournalEntries')
    THEN 1 ELSE 0 END AS IsHealthy,
    (SELECT COUNT(1) FROM sys.tables WHERE schema_id = SCHEMA_ID(N'dbo')) AS TableCount,
    (SELECT MAX(VersionNo) FROM dbo.CoreSchemaVersionsTb) AS VersionNo;";

            using (SqlConnection connection = new SqlConnection(connectionString))
            using (SqlCommand command = new SqlCommand(validationSql, connection))
            {
                connection.Open();
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (!reader.Read() || Convert.ToInt32(reader["IsHealthy"]) != 1)
                    {
                        throw new InvalidOperationException("فشل التحقق من تكامل مخطط قاعدة البيانات الأساسية. لا تبدأ التشغيل قبل إصلاح الجداول أو القيم النظامية المفقودة.");
                    }

                    return new DatabaseSchemaValidationResult(
                        Convert.ToString(reader["DatabaseName"]),
                        Convert.ToInt32(reader["TableCount"]),
                        Convert.ToInt32(reader["VersionNo"]));
                }
            }
        }

        // يعيد نص الاتصال الحالي مع اسم قاعدة البيانات.
        public static string ConnectionString
        {
            get
            {
                return GetConnectionString(includeDatabase: true);
            }
        }

        // ينشئ اتصالًا جديدًا لكل عملية قاعدة بيانات لتجنب مشاركة اتصال مفتوح بين أكثر من شاشة.
        public static SqlConnection CreateConnection()
        {
            return new SqlConnection(ConnectionString);
        }

        public static long GetNextSequenceValue(SqlConnection connection, SqlTransaction transaction, string sequenceName)
        {
            string qualifiedName = GetSequenceQualifiedName(sequenceName);
            using (SqlCommand command = new SqlCommand("SELECT NEXT VALUE FOR " + qualifiedName, connection, transaction))
            {
                return Convert.ToInt64(command.ExecuteScalar());
            }
        }

        public static long GetNextSequenceValue(string sequenceName)
        {
            using (SqlConnection connection = CreateConnection())
            {
                connection.Open();
                return GetNextSequenceValue(connection, null, sequenceName);
            }
        }

        private static string GetSequenceQualifiedName(string sequenceName)
        {
            switch (sequenceName)
            {
                case "Seq_BillId":
                    return "dbo.Seq_BillId";
                case "Seq_CustomerId":
                    return "dbo.Seq_CustomerId";
                case "Seq_PurchaseId":
                    return "dbo.Seq_PurchaseId";
                case "Seq_PurchaseDetailId":
                    return "dbo.Seq_PurchaseDetailId";
                default:
                    throw new ArgumentException("Unknown sequence: " + sequenceName, nameof(sequenceName));
            }
        }

        // يتأكد من وجود قاعدة البيانات والجداول الأساسية قبل دخول المستخدم للنظام.
        public static void EnsureReady()
        {
            if (isReady)
            {
                return;
            }

            EnsureDatabaseExists(ConnectionString);
            EnsureSchemaExists(ConnectionString);
            isReady = true;
        }

        // مسار التشغيل اليومي قراءة فقط: لا ينفذ DDL أو seed عند تسجيل دخول المستخدم.
        // تُجرى التهيئة والترقية حصراً عبر مسؤول النظام أو أمر initialize المخصص.
        internal static void ResetOperationalReadiness()
        {
            lock (operationalReadinessSync)
            {
                operationalReadyConnections.Clear();
                operationalReadinessRevision++;
                isReady = false;
            }
        }

        private static DatabaseSchemaValidationResult ValidateOperationalCoreSchema(string connectionString)
        {
            long revision;
            lock (operationalReadinessSync) revision = operationalReadinessRevision;
            DatabaseSchemaValidationResult result;
            try { result = ValidateCoreSchema(connectionString); }
            catch
            {
                lock (operationalReadinessSync)
                    if (revision == operationalReadinessRevision) operationalReadyConnections.Remove(connectionString);
                throw;
            }
            lock (operationalReadinessSync)
                if (revision == operationalReadinessRevision) operationalReadyConnections.Add(connectionString);
            return result;
        }

        internal static void EnsureOperationalReady(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("Operational connection is required.", nameof(connectionString));
            lock (operationalReadinessSync)
                if (operationalReadyConnections.Contains(connectionString)) return;
            // Never hold the cache lock while SQL waits; other connections validate independently.
            ValidateOperationalCoreSchema(connectionString);
        }

        public static void EnsureOperationalReady() => EnsureOperationalReady(ConnectionString);

        // ينشئ قاعدة البيانات إذا لم تكن موجودة على السيرفر المحدد.
        private static void EnsureDatabaseExists(string connectionString)
        {
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(connectionString);

            // اسم القاعدة مستخرج من Initial Catalog ثم نحذفه مؤقتًا للاتصال بسيرفر SQL نفسه.
            string databaseName = builder.InitialCatalog;
            if (string.IsNullOrWhiteSpace(databaseName))
            {
                throw new InvalidOperationException("Database name is missing from PharmacyConnection.");
            }

            EnsureUserDatabaseName(databaseName);

            builder.InitialCatalog = "master";

            using (SqlConnection connection = new SqlConnection(builder.ConnectionString))
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandText = @"
IF DB_ID(@DatabaseName) IS NULL
BEGIN
    DECLARE @Sql NVARCHAR(MAX) = N'CREATE DATABASE ' + QUOTENAME(@DatabaseName);
    EXEC(@Sql);
END";
                command.Parameters.AddWithValue("@DatabaseName", databaseName);

                connection.Open();
                command.ExecuteNonQuery();
            }
        }

        // يقرأ إعدادات الاتصال الحالية من App.config.
        public static SqlConnectionStringBuilder LoadSetings()
        {
            return new SqlConnectionStringBuilder(ConnectionString);
        }

        // يحفظ إعدادات التشغيل خارج مجلد التطبيق حتى لا تمسحها إعادة البناء أو التحديث.
        public static void SaveSetings(string serverName, string databaseName, bool integratedSecurity)
        {
            EnsureUserDatabaseName(databaseName);
            RuntimeSettingsStore.Save(new DatabaseConnectionProfile(serverName, databaseName, integratedSecurity));
            ResetOperationalReadiness();
        }

        public static void SaveSettings(string server, string database, bool integratedSecurity) => SaveSetings(server, database, integratedSecurity);
        public static SqlConnectionStringBuilder LoadSettings() => LoadSetings();

        internal static string BuildSettingsConnectionString(string serverName, string databaseName, bool integratedSecurity)
        {
            EnsureUserDatabaseName(databaseName);
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder
            {
                DataSource = serverName,
                InitialCatalog = databaseName,
                IntegratedSecurity = integratedSecurity,
                ConnectTimeout = 15,
                Encrypt = true,
                TrustServerCertificate = ShouldTrustServerCertificate(serverName)
            };

            return builder.ConnectionString;
        }

        internal static DatabaseConnectionProfile GetConnectionProfile()
        {
            return RuntimeSettingsStore.LoadOrDefault(GetBuildConnectionProfile());
        }

        internal static bool IsTestEnvironment
        {
            get { return GetConnectionProfile().IsTestEnvironment; }
        }

        internal static string EnvironmentNameAr
        {
            get { return GetConnectionProfile().EnvironmentNameAr; }
        }

        // لا يجوز أن يتحول إدخال شاشة الإعدادات أو سطر الأوامر إلى DDL داخل قواعد SQL النظامية.
        // يظل الاتصال بـ master داخلياً فقط لتعداد قواعد المستخدم أو إنشاء قاعدة صيدلية صريحة.
        internal static void EnsureUserDatabaseName(string databaseName)
        {
            string normalized = string.IsNullOrWhiteSpace(databaseName) ? string.Empty : databaseName.Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                throw new InvalidOperationException("اسم قاعدة بيانات الصيدلية مطلوب.");
            }

            if (string.Equals(normalized, "master", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "model", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "msdb", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "tempdb", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("لا يمكن استخدام قاعدة SQL نظامية لبيانات الصيدلية. اختر أو أنشئ قاعدة مستخدم مستقلة.");
            }
        }

        private static bool ShouldTrustServerCertificate(string serverName)
        {
            string normalized = string.IsNullOrWhiteSpace(serverName)
                ? string.Empty
                : serverName.Trim();

            if (string.Equals(normalized, ".", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "(local)", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "localhost", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, Environment.MachineName, StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith(@".\", StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith(@"localhost\", StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith(Environment.MachineName + @"\", StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith(@"(LocalDB)\", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        // يختبر إعدادات اتصال مؤقتة بدون حفظها؛ مفيد لزر Test Connection في نافذة F1.
        public static void TestSetings(string serverName, string databaseName, bool integratedSecurity)
        {
            string connectionString = BuildSettingsConnectionString(serverName, databaseName, integratedSecurity);
            using (SqlConnection connection = new SqlConnection(connectionString))
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandText = "SELECT 1";
                connection.Open();
                command.ExecuteScalar();
            }
        }

        public static void TestSettings(string serverName, string databaseName, bool integratedSecurity) => TestSetings(serverName, databaseName, integratedSecurity);

        // يفحص وجود قاعدة التشغيل وبعض جداولها الأساسية قبل أي DDL.
        // الهدف منع التباس "قاعدة فارغة" مع حذف بيانات قاعدة أخرى.
        internal static DatabaseInspectionResult InspectSettings(string serverName, string databaseName, bool integratedSecurity)
        {
            EnsureUserDatabaseName(databaseName);
            SqlConnectionStringBuilder databaseBuilder = new SqlConnectionStringBuilder(BuildSettingsConnectionString(serverName, databaseName, integratedSecurity));
            SqlConnectionStringBuilder masterBuilder = new SqlConnectionStringBuilder(databaseBuilder.ConnectionString)
            {
                InitialCatalog = "master"
            };

            bool exists;
            using (SqlConnection masterConnection = new SqlConnection(masterBuilder.ConnectionString))
            using (SqlCommand existsCommand = masterConnection.CreateCommand())
            {
                existsCommand.CommandText = "SELECT CASE WHEN DB_ID(@DatabaseName) IS NULL THEN 0 ELSE 1 END";
                existsCommand.Parameters.Add("@DatabaseName", SqlDbType.NVarChar, 128).Value = databaseName.Trim();
                masterConnection.Open();
                exists = Convert.ToInt32(existsCommand.ExecuteScalar()) == 1;
            }

            if (!exists)
            {
                return new DatabaseInspectionResult(false, 0);
            }

            using (SqlConnection databaseConnection = new SqlConnection(databaseBuilder.ConnectionString))
            using (SqlCommand tableCommand = databaseConnection.CreateCommand())
            {
                tableCommand.CommandText = @"
SELECT
    CASE WHEN OBJECT_ID(N'dbo.CompanyTb1', N'U') IS NULL THEN 0 ELSE 1 END +
    CASE WHEN OBJECT_ID(N'dbo.EmployeeTb1', N'U') IS NULL THEN 0 ELSE 1 END +
    CASE WHEN OBJECT_ID(N'dbo.MedicineTb1', N'U') IS NULL THEN 0 ELSE 1 END +
    CASE WHEN OBJECT_ID(N'dbo.BillsTb1', N'U') IS NULL THEN 0 ELSE 1 END";
                databaseConnection.Open();
                int tableCount = Convert.ToInt32(tableCommand.ExecuteScalar());
                return new DatabaseInspectionResult(true, tableCount);
            }
        }

        public static void PrepareSetings(string serverName, string databaseName, bool integratedSecurity)
        {
            RuntimeSettingsStore.EnsureConnectionProfileCanBeChanged();
            string connectionString = BuildSettingsConnectionString(serverName, databaseName, integratedSecurity);
            EnsureDatabaseExists(connectionString);
            EnsureSchemaExists(connectionString);
        }

        public static void PrepareSettings(string serverName, string databaseName, bool integratedSecurity) => PrepareSetings(serverName, databaseName, integratedSecurity);

        // استعلام واحد للقراءة فقط. لا نعرض إرشاد البدء إلا لقاعدة خالية فعلاً من بيانات التشغيل الأساسية.
        internal static DatabaseStartupState GetStartupState()
        {
            using (SqlConnection connection = CreateConnection())
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandText = @"
DECLARE @MedicineCount BIGINT = 0;
DECLARE @CompanyCount BIGINT = 0;
DECLARE @PurchaseCount BIGINT = 0;
DECLARE @SaleLineCount BIGINT = 0;

IF OBJECT_ID(N'dbo.MedicineTb1', N'U') IS NOT NULL SET @MedicineCount = (SELECT COUNT_BIG(*) FROM dbo.MedicineTb1);
IF OBJECT_ID(N'dbo.CompanyTb1', N'U') IS NOT NULL SET @CompanyCount = (SELECT COUNT_BIG(*) FROM dbo.CompanyTb1);
IF OBJECT_ID(N'dbo.PurchasesTb', N'U') IS NOT NULL SET @PurchaseCount = (SELECT COUNT_BIG(*) FROM dbo.PurchasesTb);
IF OBJECT_ID(N'dbo.BillsTb1', N'U') IS NOT NULL SET @SaleLineCount = (SELECT COUNT_BIG(*) FROM dbo.BillsTb1);

SELECT @MedicineCount, @CompanyCount, @PurchaseCount, @SaleLineCount;";
                connection.Open();
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (!reader.Read())
                    {
                        return new DatabaseStartupState(0, 0, 0, 0);
                    }

                    return new DatabaseStartupState(
                        reader.GetInt64(0),
                        reader.GetInt64(1),
                        reader.GetInt64(2),
                        reader.GetInt64(3));
                }
            }
        }

        // يتحقق من مطابقة اسم الموظف وكلمة المرور مع جدول EmployeeTb1.
        public static bool ValidateEmployeeCredentials(string employeeName, string password)
        {
            return AuthenticateEmployeeId(employeeName, password).Length > 0;
        }

        // Returns the exact EmpId whose password was checked; subsequent permission reads use this key.
        public static string AuthenticateEmployeeId(string employeeName, string password)
        {
            if (string.IsNullOrWhiteSpace(employeeName) || password == null) return string.Empty;
            return AuthenticateEmployeeId(employeeName, password, ConnectionString);
        }

        internal static string AuthenticateEmployeeId(string employeeName, string password, string connectionString)
        {
            if (string.IsNullOrWhiteSpace(employeeName) || password == null)
            {
                return string.Empty;
            }

            using (SqlConnection connection = new SqlConnection(connectionString))
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandText = @"
SELECT TOP 1 EmpId, EmpPassword, EmpName, EmpRole
FROM dbo.EmployeeTb1
WHERE EmpName = @EmpName
ORDER BY EmpId";
                command.Parameters.Add("@EmpName", SqlDbType.NVarChar, 200).Value = employeeName.Trim();

                connection.Open();
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (!reader.Read())
                    {
                        return string.Empty;
                    }

                    string employeeId = Convert.ToString(reader["EmpId"]);
                    string storedPassword = Convert.ToString(reader["EmpPassword"]);
                    string authenticatedUserName = Convert.ToString(reader["EmpName"]);
                    string authenticatedRole = Convert.ToString(reader["EmpRole"]);
                    bool valid = PasswordSecurity.VerifyPassword(password, storedPassword, out bool needsUpgrade);
                    reader.Close();

                    if (valid && needsUpgrade)
                    {
                        UpgradeEmployeePassword(connection, employeeId, password, authenticatedUserName, authenticatedRole);
                    }

                    return valid ? employeeId : string.Empty;
                }
            }
        }

        private static void UpgradeEmployeePassword(SqlConnection connection, string employeeId, string plainPassword, string authenticatedUserName, string authenticatedRole)
        {
            if (string.IsNullOrWhiteSpace(employeeId))
            {
                return;
            }

            using (SqlCommand updateCommand = connection.CreateCommand())
            {
                updateCommand.CommandText = @"
UPDATE dbo.EmployeeTb1
SET EmpPassword = @EmpPassword
WHERE EmpId = @EmpId";
                updateCommand.Parameters.Add("@EmpPassword", SqlDbType.NVarChar, 200).Value = PasswordSecurity.HashLegacyPasswordForMigration(plainPassword);
                updateCommand.Parameters.Add("@EmpId", SqlDbType.NVarChar, 100).Value = employeeId;
                updateCommand.ExecuteNonQuery();
            }

            WriteAudit("PasswordUpgraded", "Security", "Employee password was migrated to hashed storage.", authenticatedUserName, authenticatedRole, connection.ConnectionString);
        }

        public static string GetEmployeeRole(string employeeName)
        {
            if (string.IsNullOrWhiteSpace(employeeName))
            {
                return string.Empty;
            }

            using (SqlConnection connection = CreateConnection())
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandText = @"
SELECT TOP 1 ISNULL(NULLIF(EmpRole, N''), N'')
FROM dbo.EmployeeTb1
WHERE EmpName = @EmpName
ORDER BY EmpId";
                command.Parameters.Add("@EmpName", SqlDbType.NVarChar, 200).Value = employeeName.Trim();

                connection.Open();
                object result = command.ExecuteScalar();
                return result == null || result == DBNull.Value ? string.Empty : result.ToString();
            }
        }

        public static string GetEmployeeRoleById(string employeeId)
        {
            if (string.IsNullOrWhiteSpace(employeeId)) return string.Empty;
            return GetEmployeeRoleById(employeeId, ConnectionString);
        }

        internal static string GetEmployeeRoleById(string employeeId, string connectionString)
        {
            if (string.IsNullOrWhiteSpace(employeeId)) return string.Empty;
            using (SqlConnection connection = new SqlConnection(connectionString))
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandText = @"SELECT ISNULL(NULLIF(EmpRole,N''),N'')
FROM dbo.EmployeeTb1 WHERE EmpId=@EmpId;";
                command.Parameters.Add("@EmpId", SqlDbType.NVarChar, 50).Value = employeeId.Trim();
                connection.Open();
                object result = command.ExecuteScalar();
                return result == null || result == DBNull.Value ? string.Empty : Convert.ToString(result);
            }
        }

        public static bool HasManager() => HasManager(ConnectionString);

        internal static bool HasManager(string connectionString)
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandText = @"
SELECT COUNT(1)
FROM dbo.EmployeeTb1
WHERE EmpRole IN (N'Manager', N'Admin', N'Administrator', N'مدير', N'مسؤول', N'ادمن')";
                connection.Open();
                return Convert.ToInt32(command.ExecuteScalar()) > 0;
            }
        }

        public static bool IsEmployeeUsernameAvailable(string employeeName, string excludedEmployeeId = null)
        {
            if (string.IsNullOrWhiteSpace(employeeName))
            {
                return false;
            }

            using (SqlConnection connection = CreateConnection())
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandText = @"
SELECT COUNT(1)
FROM dbo.EmployeeTb1
WHERE LOWER(LTRIM(RTRIM(EmpName))) = LOWER(@EmpName)
  AND (@ExcludedEmployeeId IS NULL OR EmpId <> @ExcludedEmployeeId)";
                command.Parameters.Add("@EmpName", SqlDbType.NVarChar, 200).Value = employeeName.Trim();
                command.Parameters.Add("@ExcludedEmployeeId", SqlDbType.NVarChar, 50).Value =
                    string.IsNullOrWhiteSpace(excludedEmployeeId) ? (object)DBNull.Value : excludedEmployeeId.Trim();

                connection.Open();
                return Convert.ToInt32(command.ExecuteScalar()) == 0;
            }
        }

        public static bool IsOnlyManager(string employeeId)
        {
            if (string.IsNullOrWhiteSpace(employeeId))
            {
                return false;
            }

            using (SqlConnection connection = CreateConnection())
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandText = @"
SELECT CASE
    WHEN EXISTS (
        SELECT 1
        FROM dbo.EmployeeTb1
        WHERE EmpId = @EmpId
          AND EmpRole IN (N'Manager', N'Admin', N'Administrator', N'مدير', N'مسؤول', N'ادمن'))
     AND NOT EXISTS (
        SELECT 1
        FROM dbo.EmployeeTb1
        WHERE EmpId <> @EmpId
          AND EmpRole IN (N'Manager', N'Admin', N'Administrator', N'مدير', N'مسؤول', N'ادمن'))
    THEN 1 ELSE 0 END";
                command.Parameters.Add("@EmpId", SqlDbType.NVarChar, 50).Value = employeeId.Trim();

                connection.Open();
                return Convert.ToInt32(command.ExecuteScalar()) == 1;
            }
        }

        public static void CreateEmployee(
            string employeeId,
            string employeeName,
            int employeeAge,
            int employeeSalary,
            string employeePhone,
            string password,
            string employeeRole)
        {
            ValidateEmployeeMutation(employeeId, employeeName, employeeAge, employeeSalary, employeePhone, password, employeeRole, true);

            using (SqlConnection connection = CreateConnection())
            {
                connection.Open();
                using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        EnsureEmployeeIdentityAvailable(connection, transaction, employeeId, employeeName, null);
                        using (SqlCommand command = new SqlCommand(@"
INSERT INTO dbo.EmployeeTb1 (EmpId, EmpName, EmpSal, EmpAge, EmpPhone, EmpPassword, EmpRole)
VALUES (@EmpId, @EmpName, @EmpSal, @EmpAge, @EmpPhone, @EmpPassword, @EmpRole)", connection, transaction))
                        {
                            AddEmployeeParameters(command, employeeId, employeeName, employeeAge, employeeSalary, employeePhone, password, employeeRole, true);
                            command.ExecuteNonQuery();
                        }

                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        public static bool UpdateEmployee(
            string employeeId,
            string employeeName,
            int employeeAge,
            int employeeSalary,
            string employeePhone,
            string password,
            string employeeRole)
        {
            bool hasNewPassword = !string.IsNullOrEmpty(password);
            ValidateEmployeeMutation(employeeId, employeeName, employeeAge, employeeSalary, employeePhone, password, employeeRole, hasNewPassword);

            using (SqlConnection connection = CreateConnection())
            {
                connection.Open();
                using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        string currentRole;
                        using (SqlCommand target = new SqlCommand(@"
SELECT EmpRole
FROM dbo.EmployeeTb1 WITH (UPDLOCK, HOLDLOCK)
WHERE EmpId = @EmpId", connection, transaction))
                        {
                            target.Parameters.Add("@EmpId", SqlDbType.NVarChar, 50).Value = employeeId.Trim();
                            object value = target.ExecuteScalar();
                            if (value == null || value == DBNull.Value)
                            {
                                transaction.Rollback();
                                return false;
                            }

                            currentRole = Convert.ToString(value);
                        }

                        EnsureEmployeeIdentityAvailable(connection, transaction, employeeId, employeeName, employeeId);
                        if (IsManagerRole(currentRole) && !IsManagerRole(employeeRole) && !HasAnotherManager(connection, transaction, employeeId))
                        {
                            throw new InvalidOperationException("لا يمكن خفض صلاحية المدير الوحيد. أنشئ مديرًا آخر أولًا.");
                        }

                        using (SqlCommand command = new SqlCommand(@"
UPDATE dbo.EmployeeTb1
SET EmpName = @EmpName,
    EmpSal = @EmpSal,
    EmpAge = @EmpAge,
    EmpPhone = @EmpPhone,
    EmpPassword = CASE WHEN @UpdatePassword = 1 THEN @EmpPassword ELSE EmpPassword END,
    EmpRole = @EmpRole
WHERE EmpId = @EmpId", connection, transaction))
                        {
                            AddEmployeeParameters(command, employeeId, employeeName, employeeAge, employeeSalary, employeePhone, password, employeeRole, hasNewPassword);
                            command.ExecuteNonQuery();
                        }

                        transaction.Commit();
                        return true;
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        public static bool DeleteEmployee(string employeeId)
        {
            if (string.IsNullOrWhiteSpace(employeeId))
            {
                throw new ArgumentException("رقم الموظف مطلوب.", nameof(employeeId));
            }

            using (SqlConnection connection = CreateConnection())
            {
                connection.Open();
                using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        string currentRole;
                        using (SqlCommand target = new SqlCommand(@"
SELECT EmpRole
FROM dbo.EmployeeTb1 WITH (UPDLOCK, HOLDLOCK)
WHERE EmpId = @EmpId", connection, transaction))
                        {
                            target.Parameters.Add("@EmpId", SqlDbType.NVarChar, 50).Value = employeeId.Trim();
                            object value = target.ExecuteScalar();
                            if (value == null || value == DBNull.Value)
                            {
                                transaction.Rollback();
                                return false;
                            }

                            currentRole = Convert.ToString(value);
                        }

                        if (IsManagerRole(currentRole) && !HasAnotherManager(connection, transaction, employeeId))
                        {
                            throw new InvalidOperationException("لا يمكن حذف المدير الوحيد. أنشئ مديرًا آخر أولًا.");
                        }

                        using (SqlCommand command = new SqlCommand("DELETE FROM dbo.EmployeeTb1 WHERE EmpId = @EmpId", connection, transaction))
                        {
                            command.Parameters.Add("@EmpId", SqlDbType.NVarChar, 50).Value = employeeId.Trim();
                            command.ExecuteNonQuery();
                        }

                        transaction.Commit();
                        return true;
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        public static string GetEmployeeName(string employeeId)
        {
            if (string.IsNullOrWhiteSpace(employeeId))
            {
                return string.Empty;
            }

            using (SqlConnection connection = CreateConnection())
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandText = "SELECT EmpName FROM dbo.EmployeeTb1 WHERE EmpId = @EmpId";
                command.Parameters.Add("@EmpId", SqlDbType.NVarChar, 50).Value = employeeId.Trim();
                connection.Open();
                object result = command.ExecuteScalar();
                return result == null || result == DBNull.Value ? string.Empty : Convert.ToString(result);
            }
        }

        public static bool IsEmployeeOperationPermissionsAvailable()
        {
            using (SqlConnection connection = CreateConnection())
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandText = "SELECT CASE WHEN OBJECT_ID(N'dbo.EmployeeOperationPermissionsTb', N'U') IS NULL THEN 0 ELSE 1 END";
                connection.Open();
                return Convert.ToInt32(command.ExecuteScalar()) == 1;
            }
        }

        public static Dictionary<string, bool> GetEmployeeOperationPermissionsForUser(string employeeName)
        {
            return GetEmployeeOperationPermissions("EmpName", employeeName);
        }

        public static Dictionary<string, bool> GetEmployeeOperationPermissions(string employeeId)
        {
            return GetEmployeeOperationPermissions("EmpId", employeeId);
        }

        public static void ReplaceEmployeeOperationPermissions(string employeeId, IDictionary<string, bool> overrides)
        {
            if (!PermissionService.CanManageDetailedPermissions())
            {
                throw new UnauthorizedAccessException("لا تملك صلاحية إدارة الصلاحيات التفصيلية.");
            }

            if (string.IsNullOrWhiteSpace(employeeId))
            {
                throw new ArgumentException("اختر موظفاً قبل حفظ الصلاحيات.", nameof(employeeId));
            }

            Dictionary<string, bool> cleanOverrides = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            if (overrides != null)
            {
                foreach (KeyValuePair<string, bool> item in overrides)
                {
                    AppOperation operation;
                    if (string.IsNullOrWhiteSpace(item.Key) || !Enum.TryParse(item.Key, true, out operation))
                    {
                        throw new ArgumentException("توجد عملية صلاحية غير معروفة.", nameof(overrides));
                    }

                    cleanOverrides[operation.ToString()] = item.Value;
                }
            }

            using (SqlConnection connection = CreateConnection())
            {
                connection.Open();
                using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        if (!TableExists(connection, transaction, "EmployeeOperationPermissionsTb"))
                        {
                            throw new InvalidOperationException("الصلاحيات التفصيلية غير مهيأة بعد. شغّل تهيئة مخطط قاعدة البيانات على قاعدة اختبار أو بعد أخذ نسخة احتياطية معتمدة.");
                        }

                        using (SqlCommand employee = new SqlCommand(@"
SELECT EmpRole
FROM dbo.EmployeeTb1 WITH (UPDLOCK, HOLDLOCK)
WHERE EmpId = @EmpId;", connection, transaction))
                        {
                            employee.Parameters.Add("@EmpId", SqlDbType.NVarChar, 50).Value = employeeId.Trim();
                            object roleValue = employee.ExecuteScalar();
                            if (roleValue == null || roleValue == DBNull.Value)
                            {
                                throw new InvalidOperationException("الموظف المحدد لم يعد موجوداً.");
                            }

                            if (PermissionService.IsManagerRole(Convert.ToString(roleValue)))
                            {
                                throw new InvalidOperationException("حساب المدير يحتفظ بصلاحيات كاملة ولا يقبل تجاوزات تفصيلية.");
                            }
                        }

                        using (SqlCommand delete = new SqlCommand("DELETE FROM dbo.EmployeeOperationPermissionsTb WHERE EmpId = @EmpId", connection, transaction))
                        {
                            delete.Parameters.Add("@EmpId", SqlDbType.NVarChar, 50).Value = employeeId.Trim();
                            delete.ExecuteNonQuery();
                        }

                        foreach (KeyValuePair<string, bool> item in cleanOverrides)
                        {
                            using (SqlCommand insert = new SqlCommand(@"
INSERT INTO dbo.EmployeeOperationPermissionsTb (EmpId, OperationName, IsAllowed, UpdatedBy)
VALUES (@EmpId, @OperationName, @IsAllowed, @UpdatedBy);", connection, transaction))
                            {
                                insert.Parameters.Add("@EmpId", SqlDbType.NVarChar, 50).Value = employeeId.Trim();
                                insert.Parameters.Add("@OperationName", SqlDbType.NVarChar, 100).Value = item.Key;
                                insert.Parameters.Add("@IsAllowed", SqlDbType.Bit).Value = item.Value;
                                insert.Parameters.Add("@UpdatedBy", SqlDbType.NVarChar, 200).Value = SessionContext.CurrentUser;
                                insert.ExecuteNonQuery();
                            }
                        }

                        using (SqlCommand audit = new SqlCommand(@"
INSERT INTO dbo.AuditLogTb (UserName, UserRole, ActionName, EntityName, Details)
VALUES (@UserName, @UserRole, N'EmployeeOperationPermissionsUpdated', N'Permissions', @Details);", connection, transaction))
                        {
                            audit.Parameters.Add("@UserName", SqlDbType.NVarChar, 200).Value = SessionContext.CurrentUser;
                            audit.Parameters.Add("@UserRole", SqlDbType.NVarChar, 50).Value = SessionContext.CurrentRole;
                            audit.Parameters.Add("@Details", SqlDbType.NVarChar, 1000).Value =
                                "تم تحديث صلاحيات العمليات للموظف رقم " + employeeId.Trim() + "، عدد التجاوزات: " + cleanOverrides.Count + ".";
                            audit.ExecuteNonQuery();
                        }

                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        private static Dictionary<string, bool> GetEmployeeOperationPermissions(string employeeColumn, string employeeValue)
        {
            Dictionary<string, bool> result = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(employeeValue))
            {
                return result;
            }

            string condition = string.Equals(employeeColumn, "EmpId", StringComparison.Ordinal) ? "e.EmpId" : "e.EmpName";
            using (SqlConnection connection = CreateConnection())
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandText = @"
IF OBJECT_ID(N'dbo.EmployeeOperationPermissionsTb', N'U') IS NOT NULL
BEGIN
    SELECT p.OperationName, p.IsAllowed
    FROM dbo.EmployeeOperationPermissionsTb p
    INNER JOIN dbo.EmployeeTb1 e ON e.EmpId = p.EmpId
    WHERE " + condition + @" = @EmployeeValue;
END";
                command.Parameters.Add("@EmployeeValue", SqlDbType.NVarChar, 200).Value = employeeValue.Trim();
                connection.Open();
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string operationName = Convert.ToString(reader[0]);
                        if (!string.IsNullOrWhiteSpace(operationName))
                        {
                            result[operationName] = Convert.ToBoolean(reader[1]);
                        }
                    }
                }
            }

            return result;
        }

        private static void ValidateEmployeeMutation(string employeeId, string employeeName, int employeeAge, int employeeSalary, string employeePhone, string password, string employeeRole, bool passwordRequired)
        {
            if (string.IsNullOrWhiteSpace(employeeId) || string.IsNullOrWhiteSpace(employeeName) ||
                string.IsNullOrWhiteSpace(employeePhone) || (passwordRequired && string.IsNullOrEmpty(password)) ||
                employeeAge <= 0 || employeeSalary < 0 || !IsSupportedEmployeeRole(employeeRole))
            {
                throw new ArgumentException("بيانات الموظف غير مكتملة أو غير صالحة.");
            }

            if (passwordRequired)
            {
                PasswordSecurity.ValidatePasswordInput(password);
            }
        }

        private static bool IsSupportedEmployeeRole(string employeeRole)
        {
            return string.Equals(employeeRole, "Manager", StringComparison.OrdinalIgnoreCase)
                || string.Equals(employeeRole, "Cashier", StringComparison.OrdinalIgnoreCase)
                || string.Equals(employeeRole, "Inventory", StringComparison.OrdinalIgnoreCase)
                || string.Equals(employeeRole, "Accountant", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsManagerRole(string employeeRole)
        {
            return string.Equals(employeeRole, "Manager", StringComparison.OrdinalIgnoreCase)
                || string.Equals(employeeRole, "Admin", StringComparison.OrdinalIgnoreCase)
                || string.Equals(employeeRole, "Administrator", StringComparison.OrdinalIgnoreCase)
                || string.Equals(employeeRole, "مدير", StringComparison.Ordinal)
                || string.Equals(employeeRole, "مسؤول", StringComparison.Ordinal)
                || string.Equals(employeeRole, "ادمن", StringComparison.Ordinal);
        }

        private static void EnsureEmployeeIdentityAvailable(SqlConnection connection, SqlTransaction transaction, string employeeId, string employeeName, string excludedEmployeeId)
        {
            using (SqlCommand command = new SqlCommand(@"
SELECT COUNT(1)
FROM dbo.EmployeeTb1 WITH (UPDLOCK, HOLDLOCK)
WHERE (EmpId = @EmpId AND (@ExcludedEmployeeId IS NULL OR EmpId <> @ExcludedEmployeeId))
   OR LOWER(LTRIM(RTRIM(EmpName))) = LOWER(@EmpName)
      AND (@ExcludedEmployeeId IS NULL OR EmpId <> @ExcludedEmployeeId)", connection, transaction))
            {
                command.Parameters.Add("@EmpId", SqlDbType.NVarChar, 50).Value = employeeId.Trim();
                command.Parameters.Add("@EmpName", SqlDbType.NVarChar, 200).Value = employeeName.Trim();
                command.Parameters.Add("@ExcludedEmployeeId", SqlDbType.NVarChar, 50).Value =
                    string.IsNullOrWhiteSpace(excludedEmployeeId) ? (object)DBNull.Value : excludedEmployeeId.Trim();
                if (Convert.ToInt32(command.ExecuteScalar()) > 0)
                {
                    throw new InvalidOperationException("رقم الموظف أو اسم المستخدم مستخدم بالفعل.");
                }
            }
        }

        private static bool HasAnotherManager(SqlConnection connection, SqlTransaction transaction, string employeeId)
        {
            using (SqlCommand command = new SqlCommand(@"
SELECT COUNT(1)
FROM dbo.EmployeeTb1 WITH (UPDLOCK, HOLDLOCK)
WHERE EmpId <> @EmpId
  AND EmpRole IN (N'Manager', N'Admin', N'Administrator', N'مدير', N'مسؤول', N'ادمن')", connection, transaction))
            {
                command.Parameters.Add("@EmpId", SqlDbType.NVarChar, 50).Value = employeeId.Trim();
                return Convert.ToInt32(command.ExecuteScalar()) > 0;
            }
        }

        private static void AddEmployeeParameters(SqlCommand command, string employeeId, string employeeName, int employeeAge, int employeeSalary, string employeePhone, string password, string employeeRole, bool updatePassword)
        {
            command.Parameters.Add("@EmpId", SqlDbType.NVarChar, 50).Value = employeeId.Trim();
            command.Parameters.Add("@EmpName", SqlDbType.NVarChar, 200).Value = employeeName.Trim();
            command.Parameters.Add("@EmpSal", SqlDbType.Int).Value = employeeSalary;
            command.Parameters.Add("@EmpAge", SqlDbType.Int).Value = employeeAge;
            command.Parameters.Add("@EmpPhone", SqlDbType.NVarChar, 50).Value = employeePhone.Trim();
            command.Parameters.Add("@EmpPassword", SqlDbType.NVarChar, 200).Value =
                updatePassword ? (object)PasswordSecurity.HashPassword(password) : DBNull.Value;
            command.Parameters.Add("@UpdatePassword", SqlDbType.Bit).Value = updatePassword;
            command.Parameters.Add("@EmpRole", SqlDbType.NVarChar, 50).Value = employeeRole.Trim();
        }

        public static void CreateBootstrapManager(
            string employeeId,
            string employeeName,
            int employeeAge,
            int employeeSalary,
            string employeePhone,
            string password)
        {
            if (string.IsNullOrWhiteSpace(employeeId)
                || string.IsNullOrWhiteSpace(employeeName)
                || string.IsNullOrWhiteSpace(employeePhone)
                || string.IsNullOrEmpty(password)
                || employeeAge <= 0
                || employeeSalary < 0)
            {
                throw new ArgumentException("بيانات المدير الأول غير مكتملة أو غير صالحة.");
            }

            PasswordSecurity.ValidatePasswordInput(password);

            using (SqlConnection connection = CreateConnection())
            {
                connection.Open();
                using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        using (SqlCommand managerCheck = new SqlCommand(@"
SELECT COUNT(1)
FROM dbo.EmployeeTb1 WITH (UPDLOCK, HOLDLOCK)
WHERE EmpRole IN (N'Manager', N'Admin', N'Administrator', N'مدير', N'مسؤول', N'ادمن')", connection, transaction))
                        {
                            if (Convert.ToInt32(managerCheck.ExecuteScalar()) > 0)
                            {
                                throw new InvalidOperationException("يوجد مدير بالفعل. استخدم شاشة الموظفين لإدارة الصلاحيات.");
                            }
                        }

                        using (SqlCommand duplicateCheck = new SqlCommand(@"
SELECT COUNT(1)
FROM dbo.EmployeeTb1 WITH (UPDLOCK, HOLDLOCK)
WHERE EmpId = @EmpId
   OR LOWER(LTRIM(RTRIM(EmpName))) = LOWER(@EmpName)", connection, transaction))
                        {
                            duplicateCheck.Parameters.Add("@EmpId", SqlDbType.NVarChar, 50).Value = employeeId.Trim();
                            duplicateCheck.Parameters.Add("@EmpName", SqlDbType.NVarChar, 200).Value = employeeName.Trim();
                            if (Convert.ToInt32(duplicateCheck.ExecuteScalar()) > 0)
                            {
                                throw new InvalidOperationException("رقم الموظف أو اسم المستخدم مستخدم بالفعل.");
                            }
                        }

                        using (SqlCommand insert = new SqlCommand(@"
INSERT INTO dbo.EmployeeTb1 (EmpId, EmpName, EmpSal, EmpAge, EmpPhone, EmpPassword, EmpRole)
VALUES (@EmpId, @EmpName, @EmpSal, @EmpAge, @EmpPhone, @EmpPassword, N'Manager')", connection, transaction))
                        {
                            insert.Parameters.Add("@EmpId", SqlDbType.NVarChar, 50).Value = employeeId.Trim();
                            insert.Parameters.Add("@EmpName", SqlDbType.NVarChar, 200).Value = employeeName.Trim();
                            insert.Parameters.Add("@EmpSal", SqlDbType.Int).Value = employeeSalary;
                            insert.Parameters.Add("@EmpAge", SqlDbType.Int).Value = employeeAge;
                            insert.Parameters.Add("@EmpPhone", SqlDbType.NVarChar, 50).Value = employeePhone.Trim();
                            insert.Parameters.Add("@EmpPassword", SqlDbType.NVarChar, 200).Value = PasswordSecurity.HashPassword(password);
                            insert.ExecuteNonQuery();
                        }

                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        public static void WriteAudit(string actionName, string entityName, string details = "")
        {
            WriteAudit(actionName, entityName, details, SessionContext.CurrentUser, SessionContext.CurrentRole);
        }

        internal static void WriteAudit(string actionName, string entityName, string details, string userName, string userRole)
        {
            try { WriteAudit(actionName, entityName, details, userName, userRole, ConnectionString); }
            catch { /* Preserve the established best-effort audit contract. */ }
        }

        internal static void WriteAudit(string actionName, string entityName, string details, string userName, string userRole, string connectionString)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = @"
INSERT INTO dbo.AuditLogTb (UserName, UserRole, ActionName, EntityName, Details)
VALUES (@UserName, @UserRole, @ActionName, @EntityName, @Details)";
                    command.Parameters.Add("@UserName", SqlDbType.NVarChar, 200).Value = userName ?? string.Empty;
                    command.Parameters.Add("@UserRole", SqlDbType.NVarChar, 50).Value = userRole ?? string.Empty;
                    command.Parameters.Add("@ActionName", SqlDbType.NVarChar, 100).Value = actionName;
                    command.Parameters.Add("@EntityName", SqlDbType.NVarChar, 100).Value = entityName;
                    command.Parameters.Add("@Details", SqlDbType.NVarChar, 1000).Value =
                        string.IsNullOrWhiteSpace(details) ? (object)DBNull.Value : details.Trim();

                    connection.Open();
                    command.ExecuteNonQuery();
                }
            }
            catch
            {
                // Audit logging should never block the main pharmacy workflow.
            }
        }

        public static DataTable GetRecentAuditLogs(int take = 100)
        {
            return OperationalReportCatalog.ToDataTable(ApplicationServices.SystemMonitorLogs.GetRecentAuditLogs(take));
        }

        public static DataTable GetOperationalAlerts(int lowStockThreshold = 10, int expiringWithinDays = 30)
        {
            // Keep dashboard consumers compatible while sharing the inventory alert policy.
            DataTable table = InventoryService.GetInventoryAlerts(lowStockThreshold, expiringWithinDays);
            using (table) return HomeOperationalAlertPresentation.FromInventoryTable(table);
        }
        public static DataTable GetRecentInventoryMovements(int take = 200)
        {
            return OperationalReportCatalog.ToDataTable(ApplicationServices.SystemMonitorLogs.GetRecentInventoryMovements(take));
        }

        // يجلب قواعد البيانات المتاحة من السيرفر حتى يستطيع المستخدم اختيار قاعدة موجودة.
        public static DataTable GetDatabases(string serverName, bool integratedSecurity)
        {
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder
            {
                DataSource = serverName,
                InitialCatalog = "master",
                IntegratedSecurity = integratedSecurity,
                ConnectTimeout = 15,
                Encrypt = true,
                TrustServerCertificate = ShouldTrustServerCertificate(serverName)
            };

            using (SqlConnection connection = new SqlConnection(builder.ConnectionString))
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandText = "SELECT name FROM sys.databases WHERE database_id > 4 ORDER BY name";

                using (SqlDataAdapter adapter = new SqlDataAdapter(command))
                {
                    DataTable table = new DataTable();
                    adapter.Fill(table);
                    return table;
                }
            }
        }

        // يعيد أسماء السيرفرات المقترحة والمكتشفة لتسهيل حل خطأ عدم العثور على SQL Server.
        public static string[] GetSuggestedServers(bool includeDiscovery = false)
        {
            List<string> servers = new List<string>();
            HashSet<string> uniqueServers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            AddServer(uniqueServers, servers, ".");
            AddServer(uniqueServers, servers, @".\SQLEXPRESS");
            AddServer(uniqueServers, servers, "localhost");
            AddServer(uniqueServers, servers, @"localhost\SQLEXPRESS");
            AddServer(uniqueServers, servers, Environment.MachineName);
            AddServer(uniqueServers, servers, Environment.MachineName + @"\SQLEXPRESS");
            AddServer(uniqueServers, servers, "(local)");
            AddServer(uniqueServers, servers, @"(LocalDB)\MSSQLLocalDB");

            if (!includeDiscovery)
            {
                return servers.ToArray();
            }

            try
            {
                DataTable discoveredServers = SqlDataSourceEnumerator.Instance.GetDataSources();

                foreach (DataRow row in discoveredServers.Rows)
                {
                    string serverName = row["ServerName"].ToString();
                    string instanceName = row["InstanceName"].ToString();
                    string fullName = string.IsNullOrWhiteSpace(instanceName)
                        ? serverName
                        : serverName + "\\" + instanceName;

                    AddServer(uniqueServers, servers, fullName);
                }
            }
            catch { /* server discovery failed */ }

            return servers.ToArray();
        }

        // يحول أخطاء الاتصال الشائعة إلى رسالة مفهومة للمستخدم.
        public static string BuildConnectionHelp(Exception exception)
        {
            return "تعذر إكمال الاتصال بقاعدة البيانات."
                + "\n\nخطوات الحل:"
                + "\n1) شغّل التطبيق بحساب Windows محلي مسؤول، ثم افتح شاشة الدخول واضغط F1."
                + "\n2) جرّب السيرفرات: . ثم .\\SQLEXPRESS ثم localhost\\SQLEXPRESS."
                + "\n3) تأكد من تشغيل خدمة SQL Server من Services."
                + "\n4) إذا كنت تستخدم اسم مثيل مثل SQLEXPRESS فتأكد من تشغيل SQL Server Browser."
                + "\n5) بعد اختيار الخادم اضغط تحميل قواعد البيانات أو اختبار الاتصال.";
        }

        // ينشئ الجداول الأساسية إذا كانت غير موجودة.
        private static void EnsureSchemaExists(string connectionString)
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandText = @"
IF OBJECT_ID('dbo.CoreSchemaVersionsTb', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.CoreSchemaVersionsTb
    (
        VersionNo INT NOT NULL PRIMARY KEY,
        AppliedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_CoreSchemaVersionsTb_AppliedAtUtc DEFAULT SYSUTCDATETIME()
    );
END;

IF NOT EXISTS (SELECT 1 FROM dbo.CoreSchemaVersionsTb WHERE VersionNo = 2)
BEGIN
    INSERT INTO dbo.CoreSchemaVersionsTb (VersionNo) VALUES (2);
END;

IF OBJECT_ID('dbo.CompanyTb1', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.CompanyTb1
    (
        CompId NVARCHAR(50) NOT NULL PRIMARY KEY,
        CompName NVARCHAR(200) NOT NULL,
        CompPhone NVARCHAR(50) NOT NULL,
        CompAddress NVARCHAR(300) NOT NULL
    );
END;

IF OBJECT_ID('dbo.EmployeeTb1', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.EmployeeTb1
    (
        EmpId NVARCHAR(50) NOT NULL PRIMARY KEY,
        EmpName NVARCHAR(200) NOT NULL,
        EmpSal INT NOT NULL,
        EmpAge INT NOT NULL,
        EmpPhone NVARCHAR(50) NOT NULL,
        EmpPassword NVARCHAR(200) NOT NULL
    );
END;

IF OBJECT_ID('dbo.MedicineTb1', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.MedicineTb1
    (
        MedName NVARCHAR(200) NOT NULL PRIMARY KEY,
        Barcode NVARCHAR(100) NULL,
        Bprice INT NOT NULL,
        Sprice INT NOT NULL,
        MedQty INT NOT NULL,
        ExpDate DATETIME NOT NULL,
        CompName NVARCHAR(200) NOT NULL
    );
END;

IF OBJECT_ID('dbo.BillsTb1', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.BillsTb1
    (
        BillId NVARCHAR(50) NOT NULL,
        MedName NVARCHAR(200) NOT NULL,
        Sprice INT NOT NULL,
        Quantity INT NOT NULL,
        TotalAmount INT NOT NULL,
        CustomerId NVARCHAR(50) NULL
    );
END
ELSE
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.BillsTb1') AND name = 'CustomerId')
    BEGIN
        ALTER TABLE dbo.BillsTb1 ADD CustomerId NVARCHAR(50) NULL;
    END
END;

IF OBJECT_ID('dbo.PurchasesTb', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PurchasesTb
    (
        PurchaseId NVARCHAR(50) NOT NULL PRIMARY KEY,
        SupplierName NVARCHAR(200) NOT NULL,
        PurchaseDate DATETIME NOT NULL,
        TotalAmount DECIMAL(18, 2) NOT NULL,
        EmployeeName NVARCHAR(200) NOT NULL,
        Notes NVARCHAR(MAX) NULL
    );
END;

IF OBJECT_ID('dbo.PurchaseDetailsTb', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PurchaseDetailsTb
    (
        DetailId NVARCHAR(50) NOT NULL PRIMARY KEY,
        PurchaseId NVARCHAR(50) NOT NULL,
        MedName NVARCHAR(200) NOT NULL,
        BuyingPrice INT NOT NULL,
        Quantity INT NOT NULL,
        TotalAmount INT NOT NULL,
        CONSTRAINT FK_PurchaseDetails_Purchases FOREIGN KEY (PurchaseId) REFERENCES dbo.PurchasesTb (PurchaseId)
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PurchaseDetails_PurchaseId' AND object_id = OBJECT_ID('dbo.PurchaseDetailsTb'))
BEGIN
    CREATE INDEX IX_PurchaseDetails_PurchaseId ON dbo.PurchaseDetailsTb (PurchaseId);
END;


IF OBJECT_ID('dbo.CustomersBillsTb', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.CustomersBillsTb
    (
        CustomerId NVARCHAR(50) NOT NULL PRIMARY KEY,
        CustomerName NVARCHAR(200) NOT NULL,
        PhoneNumber NVARCHAR(50) NOT NULL,
        CustomerAddress NVARCHAR(300) NOT NULL,
        TotalSpent DECIMAL(18, 2) NOT NULL,
        InvoiceDate DATETIME NOT NULL,
        EmployeeName NVARCHAR(200) NOT NULL,
        InvoiceNotes NVARCHAR(MAX) NULL
    );
END;

IF OBJECT_ID('dbo.Seq_BillId', 'SO') IS NULL
BEGIN
    DECLARE @SeqStartBill BIGINT;
    DECLARE @SeqSqlBill NVARCHAR(MAX);
    SELECT @SeqStartBill = ISNULL(MAX(TRY_CONVERT(BIGINT, BillId)), 0) + 1
    FROM dbo.BillsTb1
    WHERE TRY_CONVERT(BIGINT, BillId) IS NOT NULL;
    SET @SeqSqlBill = N'CREATE SEQUENCE dbo.Seq_BillId AS BIGINT START WITH ' + CONVERT(NVARCHAR(30), @SeqStartBill) + N' INCREMENT BY 1;';
    EXEC(@SeqSqlBill);
END;

IF OBJECT_ID('dbo.Seq_CustomerId', 'SO') IS NULL
BEGIN
    DECLARE @SeqStartCustomer BIGINT;
    DECLARE @SeqSqlCustomer NVARCHAR(MAX);
    SELECT @SeqStartCustomer = ISNULL(MAX(TRY_CONVERT(BIGINT, CustomerId)), 0) + 1
    FROM dbo.CustomersBillsTb
    WHERE TRY_CONVERT(BIGINT, CustomerId) IS NOT NULL;
    SET @SeqSqlCustomer = N'CREATE SEQUENCE dbo.Seq_CustomerId AS BIGINT START WITH ' + CONVERT(NVARCHAR(30), @SeqStartCustomer) + N' INCREMENT BY 1;';
    EXEC(@SeqSqlCustomer);
END;

IF OBJECT_ID('dbo.Seq_PurchaseId', 'SO') IS NULL
BEGIN
    DECLARE @SeqStartPurchase BIGINT;
    DECLARE @SeqSqlPurchase NVARCHAR(MAX);
    SELECT @SeqStartPurchase = ISNULL(MAX(TRY_CONVERT(BIGINT, PurchaseId)), 0) + 1
    FROM dbo.PurchasesTb
    WHERE TRY_CONVERT(BIGINT, PurchaseId) IS NOT NULL;
    SET @SeqSqlPurchase = N'CREATE SEQUENCE dbo.Seq_PurchaseId AS BIGINT START WITH ' + CONVERT(NVARCHAR(30), @SeqStartPurchase) + N' INCREMENT BY 1;';
    EXEC(@SeqSqlPurchase);
END;

IF OBJECT_ID('dbo.Seq_PurchaseDetailId', 'SO') IS NULL
BEGIN
    DECLARE @SeqStartPurchaseDetail BIGINT;
    DECLARE @SeqSqlPurchaseDetail NVARCHAR(MAX);
    SELECT @SeqStartPurchaseDetail = ISNULL(MAX(TRY_CONVERT(BIGINT, DetailId)), 0) + 1
    FROM dbo.PurchaseDetailsTb
    WHERE TRY_CONVERT(BIGINT, DetailId) IS NOT NULL;
    SET @SeqSqlPurchaseDetail = N'CREATE SEQUENCE dbo.Seq_PurchaseDetailId AS BIGINT START WITH ' + CONVERT(NVARCHAR(30), @SeqStartPurchaseDetail) + N' INCREMENT BY 1;';
    EXEC(@SeqSqlPurchaseDetail);
END;

IF OBJECT_ID('dbo.PharmacyProfileTb', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PharmacyProfileTb
    (
        ProfileKey NVARCHAR(20) NOT NULL PRIMARY KEY,
        NameAr NVARCHAR(200) NOT NULL,
        NameEn NVARCHAR(200) NULL,
        ActivityAr NVARCHAR(200) NULL,
        ActivityEn NVARCHAR(200) NULL,
        CommercialRegNo NVARCHAR(50) NULL,
        LicenseNo NVARCHAR(100) NULL,
        Mobile NVARCHAR(50) NULL,
        Phone NVARCHAR(50) NOT NULL,
        TaxNo NVARCHAR(50) NULL,
        Address NVARCHAR(300) NOT NULL,
        InvoiceNotes NVARCHAR(MAX) NULL,
        HeaderFooter NVARCHAR(MAX) NULL,
        Fax NVARCHAR(50) NULL,
        PoBox NVARCHAR(50) NULL,
        Email NVARCHAR(100) NULL,
        Website NVARCHAR(200) NULL,
        LogoPath NVARCHAR(500) NULL,
        HeaderLogoPath NVARCHAR(500) NULL,
        FooterLogoPath NVARCHAR(500) NULL
    );
END;

IF OBJECT_ID('dbo.CurrencySettingsTb', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.CurrencySettingsTb
    (
        CurrencyId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        CountryNameAr NVARCHAR(100) NOT NULL,
        CurrencyNameAr NVARCHAR(100) NOT NULL,
        CurrencyCode NVARCHAR(10) NOT NULL,
        CurrencySymbol NVARCHAR(20) NULL,
        IsSystemCurrency BIT NOT NULL CONSTRAINT DF_CurrencySettingsTb_IsSystemCurrency DEFAULT 0,
        CreatedAt DATETIME NOT NULL CONSTRAINT DF_CurrencySettingsTb_CreatedAt DEFAULT GETDATE()
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_CurrencySettingsTb_CurrencyCode' AND object_id = OBJECT_ID('dbo.CurrencySettingsTb'))
BEGIN
    CREATE UNIQUE INDEX UX_CurrencySettingsTb_CurrencyCode ON dbo.CurrencySettingsTb (CurrencyCode);
END;

IF NOT EXISTS (SELECT 1 FROM dbo.CurrencySettingsTb)
BEGIN
    INSERT INTO dbo.CurrencySettingsTb (CountryNameAr, CurrencyNameAr, CurrencyCode, CurrencySymbol, IsSystemCurrency)
    VALUES
        (N'السعودية', N'الريال السعودي', N'SAR', N'ر.س', 1),
        (N'اليمن', N'الريال اليمني', N'YER', N'ر.ي', 0),
        (N'الإمارات', N'الدرهم الإماراتي', N'AED', N'د.إ', 0),
        (N'عمان', N'الريال العماني', N'OMR', N'ر.ع', 0),
        (N'قطر', N'الريال القطري', N'QAR', N'ر.ق', 0),
        (N'الكويت', N'الدينار الكويتي', N'KWD', N'د.ك', 0),
        (N'البحرين', N'الدينار البحريني', N'BHD', N'د.ب', 0),
        (N'مصر', N'الجنيه المصري', N'EGP', N'ج.م', 0),
        (N'تركيا', N'الليرة التركية', N'TRY', N'TL', 0),
        (N'الولايات المتحدة', N'الدولار الأمريكي', N'USD', N'$', 0);
END;

IF NOT EXISTS (SELECT 1 FROM dbo.CurrencySettingsTb WHERE IsSystemCurrency = 1)
BEGIN
    UPDATE dbo.CurrencySettingsTb
    SET IsSystemCurrency = 1
    WHERE CurrencyId = (SELECT TOP 1 CurrencyId FROM dbo.CurrencySettingsTb ORDER BY CurrencyId);
END;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.MedicineTb1') AND name = 'ProdDate')
BEGIN
    ALTER TABLE dbo.MedicineTb1 ADD ProdDate DATETIME NULL;
END;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.PurchaseDetailsTb') AND name = 'ProdDate')
BEGIN
    ALTER TABLE dbo.PurchaseDetailsTb ADD ProdDate DATETIME NULL;
END;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.PurchaseDetailsTb') AND name = 'ExpDate')
BEGIN
    ALTER TABLE dbo.PurchaseDetailsTb ADD ExpDate DATETIME NULL;
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_MedicineTb1_CompName' AND object_id = OBJECT_ID('dbo.MedicineTb1'))
BEGIN
    CREATE INDEX IX_MedicineTb1_CompName ON dbo.MedicineTb1 (CompName);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CustomersBillsTb_PhoneNumber' AND object_id = OBJECT_ID('dbo.CustomersBillsTb'))
BEGIN
    CREATE INDEX IX_CustomersBillsTb_PhoneNumber ON dbo.CustomersBillsTb (PhoneNumber);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_EmployeeTb1_EmpName' AND object_id = OBJECT_ID('dbo.EmployeeTb1'))
BEGIN
    CREATE INDEX IX_EmployeeTb1_EmpName ON dbo.EmployeeTb1 (EmpName);
END;

IF OBJECT_ID('dbo.AuditLogTb', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.AuditLogTb
    (
        AuditId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        UserName NVARCHAR(200) NOT NULL,
        UserRole NVARCHAR(50) NOT NULL,
        ActionName NVARCHAR(100) NOT NULL,
        EntityName NVARCHAR(100) NOT NULL,
        Details NVARCHAR(1000) NULL,
        CreatedAt DATETIME NOT NULL CONSTRAINT DF_AuditLogTb_CreatedAt DEFAULT GETDATE()
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AuditLogTb_CreatedAt' AND object_id = OBJECT_ID('dbo.AuditLogTb'))
BEGIN
    CREATE INDEX IX_AuditLogTb_CreatedAt ON dbo.AuditLogTb (CreatedAt DESC);
END;

IF OBJECT_ID('dbo.InventoryMovementsTb', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.InventoryMovementsTb
    (
        MovementId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        MedName NVARCHAR(200) NOT NULL,
        QuantityDelta INT NOT NULL,
        QuantityBefore INT NOT NULL,
        QuantityAfter INT NOT NULL,
        SourceType NVARCHAR(50) NOT NULL,
        SourceId NVARCHAR(50) NULL,
        UnitCost DECIMAL(18, 2) NULL,
        UnitPrice DECIMAL(18, 2) NULL,
        Notes NVARCHAR(500) NULL,
        CreatedBy NVARCHAR(200) NOT NULL,
        CreatedAt DATETIME NOT NULL CONSTRAINT DF_InventoryMovementsTb_CreatedAt DEFAULT GETDATE()
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_InventoryMovementsTb_MedName_CreatedAt' AND object_id = OBJECT_ID('dbo.InventoryMovementsTb'))
BEGIN
    CREATE INDEX IX_InventoryMovementsTb_MedName_CreatedAt ON dbo.InventoryMovementsTb (MedName, CreatedAt DESC);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_InventoryMovementsTb_Source' AND object_id = OBJECT_ID('dbo.InventoryMovementsTb'))
BEGIN
    CREATE INDEX IX_InventoryMovementsTb_Source ON dbo.InventoryMovementsTb (SourceType, SourceId);
END;

IF OBJECT_ID('dbo.AccountsTb', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.AccountsTb
    (
        AccountCode NVARCHAR(20) NOT NULL PRIMARY KEY,
        AccountName NVARCHAR(200) NOT NULL,
        AccountType NVARCHAR(50) NOT NULL,
        ParentAccountCode NVARCHAR(20) NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_AccountsTb_IsActive DEFAULT 1,
        CreatedAt DATETIME NOT NULL CONSTRAINT DF_AccountsTb_CreatedAt DEFAULT GETDATE()
    );
END;

IF OBJECT_ID('dbo.CostCentersTb', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.CostCentersTb
    (
        CostCenterId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        CostCenterName NVARCHAR(200) NOT NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_CostCentersTb_IsActive DEFAULT 1
    );
END;

IF OBJECT_ID('dbo.JournalEntriesTb', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.JournalEntriesTb
    (
        JournalEntryId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        EntryDate DATETIME NOT NULL,
        SourceType NVARCHAR(50) NULL,
        SourceId NVARCHAR(50) NULL,
        Description NVARCHAR(500) NULL,
        CreatedBy NVARCHAR(200) NULL,
        CreatedAt DATETIME NOT NULL CONSTRAINT DF_JournalEntriesTb_CreatedAt DEFAULT GETDATE()
    );
END;

IF OBJECT_ID('dbo.JournalEntryLinesTb', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.JournalEntryLinesTb
    (
        JournalEntryLineId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        JournalEntryId INT NOT NULL,
        AccountCode NVARCHAR(20) NOT NULL,
        Debit DECIMAL(18, 2) NOT NULL CONSTRAINT DF_JournalEntryLinesTb_Debit DEFAULT 0,
        Credit DECIMAL(18, 2) NOT NULL CONSTRAINT DF_JournalEntryLinesTb_Credit DEFAULT 0,
        LineDescription NVARCHAR(500) NULL,
        CostCenterId INT NULL,
        CONSTRAINT FK_JournalEntryLines_JournalEntries FOREIGN KEY (JournalEntryId) REFERENCES dbo.JournalEntriesTb (JournalEntryId),
        CONSTRAINT FK_JournalEntryLines_Accounts FOREIGN KEY (AccountCode) REFERENCES dbo.AccountsTb (AccountCode)
    );
END;

IF OBJECT_ID('dbo.AccountingVouchersTb', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.AccountingVouchersTb
    (
        VoucherId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        VoucherType NVARCHAR(50) NOT NULL,
        VoucherDate DATETIME NOT NULL,
        PartyName NVARCHAR(200) NULL,
        AccountCode NVARCHAR(20) NOT NULL,
        Amount DECIMAL(18, 2) NOT NULL,
        Notes NVARCHAR(500) NULL,
        JournalEntryId INT NOT NULL,
        CreatedBy NVARCHAR(200) NULL,
        RequestId UNIQUEIDENTIFIER NULL,
        RequestCurrencyCode NVARCHAR(10) NULL,
        CreatedAt DATETIME NOT NULL CONSTRAINT DF_AccountingVouchersTb_CreatedAt DEFAULT GETDATE(),
        CONSTRAINT FK_AccountingVouchers_JournalEntries FOREIGN KEY (JournalEntryId) REFERENCES dbo.JournalEntriesTb (JournalEntryId),
        CONSTRAINT CK_AccountingVouchersTb_RequestIdentity CHECK
            ((RequestId IS NULL AND RequestCurrencyCode IS NULL) OR
            (RequestId IS NOT NULL AND RequestId<>'00000000-0000-0000-0000-000000000000' AND RequestCurrencyCode IS NOT NULL AND LEN(RequestCurrencyCode)>0))
    );
    EXEC(N'CREATE UNIQUE INDEX UX_AccountingVouchersTb_RequestId ON dbo.AccountingVouchersTb(RequestId) WHERE RequestId IS NOT NULL;');
END;

IF OBJECT_ID('dbo.TaxSettingsTb', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.TaxSettingsTb
    (
        TaxSettingId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        TaxRate DECIMAL(9, 2) NOT NULL CONSTRAINT DF_TaxSettingsTb_TaxRate DEFAULT 0,
        IsEnabled BIT NOT NULL CONSTRAINT DF_TaxSettingsTb_IsEnabled DEFAULT 0,
        UpdatedAt DATETIME NOT NULL CONSTRAINT DF_TaxSettingsTb_UpdatedAt DEFAULT GETDATE()
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_JournalEntries_Source' AND object_id = OBJECT_ID('dbo.JournalEntriesTb'))
BEGIN
    CREATE UNIQUE INDEX UX_JournalEntries_Source ON dbo.JournalEntriesTb (SourceType, SourceId)
    WHERE SourceType IS NOT NULL AND SourceId IS NOT NULL;
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_AccountingVouchers_JournalEntryId' AND object_id = OBJECT_ID('dbo.AccountingVouchersTb'))
BEGIN
    CREATE UNIQUE INDEX UX_AccountingVouchers_JournalEntryId ON dbo.AccountingVouchersTb (JournalEntryId);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_JournalEntryLines_AccountCode' AND object_id = OBJECT_ID('dbo.JournalEntryLinesTb'))
BEGIN
    CREATE INDEX IX_JournalEntryLines_AccountCode ON dbo.JournalEntryLinesTb (AccountCode);
END;

IF NOT EXISTS (SELECT 1 FROM dbo.TaxSettingsTb)
BEGIN
    INSERT INTO dbo.TaxSettingsTb (TaxRate, IsEnabled) VALUES (0, 0);
END;

IF NOT EXISTS (SELECT 1 FROM dbo.CostCentersTb)
BEGIN
    INSERT INTO dbo.CostCentersTb (CostCenterName) VALUES (N'المركز الرئيسي');
END;

IF NOT EXISTS (SELECT 1 FROM dbo.AccountsTb WHERE AccountCode = N'1')
    INSERT INTO dbo.AccountsTb (AccountCode, AccountName, AccountType, ParentAccountCode) VALUES (N'1', N'الأصول', N'Asset', NULL);
IF NOT EXISTS (SELECT 1 FROM dbo.AccountsTb WHERE AccountCode = N'11')
    INSERT INTO dbo.AccountsTb (AccountCode, AccountName, AccountType, ParentAccountCode) VALUES (N'11', N'الأصول الثابتة', N'Asset', N'1');
IF NOT EXISTS (SELECT 1 FROM dbo.AccountsTb WHERE AccountCode = N'12')
    INSERT INTO dbo.AccountsTb (AccountCode, AccountName, AccountType, ParentAccountCode) VALUES (N'12', N'الأصول المتداولة', N'Asset', N'1');
IF NOT EXISTS (SELECT 1 FROM dbo.AccountsTb WHERE AccountCode = N'13')
    INSERT INTO dbo.AccountsTb (AccountCode, AccountName, AccountType, ParentAccountCode) VALUES (N'13', N'الذمم والأرصدة المدينة', N'Asset', N'1');
IF NOT EXISTS (SELECT 1 FROM dbo.AccountsTb WHERE AccountCode = N'2')
    INSERT INTO dbo.AccountsTb (AccountCode, AccountName, AccountType, ParentAccountCode) VALUES (N'2', N'الخصوم', N'Liability', NULL);
IF NOT EXISTS (SELECT 1 FROM dbo.AccountsTb WHERE AccountCode = N'21')
    INSERT INTO dbo.AccountsTb (AccountCode, AccountName, AccountType, ParentAccountCode) VALUES (N'21', N'الخصوم المتداولة', N'Liability', N'2');
IF NOT EXISTS (SELECT 1 FROM dbo.AccountsTb WHERE AccountCode = N'22')
    INSERT INTO dbo.AccountsTb (AccountCode, AccountName, AccountType, ParentAccountCode) VALUES (N'22', N'ضرائب ورسوم مستحقة', N'Liability', N'2');
IF NOT EXISTS (SELECT 1 FROM dbo.AccountsTb WHERE AccountCode = N'3')
    INSERT INTO dbo.AccountsTb (AccountCode, AccountName, AccountType, ParentAccountCode) VALUES (N'3', N'حقوق الملكية', N'Equity', NULL);
IF NOT EXISTS (SELECT 1 FROM dbo.AccountsTb WHERE AccountCode = N'4')
    INSERT INTO dbo.AccountsTb (AccountCode, AccountName, AccountType, ParentAccountCode) VALUES (N'4', N'الإيرادات', N'Revenue', NULL);
IF NOT EXISTS (SELECT 1 FROM dbo.AccountsTb WHERE AccountCode = N'41')
    INSERT INTO dbo.AccountsTb (AccountCode, AccountName, AccountType, ParentAccountCode) VALUES (N'41', N'إيرادات النشاط', N'Revenue', N'4');
IF NOT EXISTS (SELECT 1 FROM dbo.AccountsTb WHERE AccountCode = N'5')
    INSERT INTO dbo.AccountsTb (AccountCode, AccountName, AccountType, ParentAccountCode) VALUES (N'5', N'المصروفات', N'Expense', NULL);
IF NOT EXISTS (SELECT 1 FROM dbo.AccountsTb WHERE AccountCode = N'51')
    INSERT INTO dbo.AccountsTb (AccountCode, AccountName, AccountType, ParentAccountCode) VALUES (N'51', N'تكلفة البضاعة والمشتريات', N'Expense', N'5');
IF NOT EXISTS (SELECT 1 FROM dbo.AccountsTb WHERE AccountCode = N'52')
    INSERT INTO dbo.AccountsTb (AccountCode, AccountName, AccountType, ParentAccountCode) VALUES (N'52', N'مصروفات تشغيلية', N'Expense', N'5');

IF NOT EXISTS (SELECT 1 FROM dbo.AccountsTb WHERE AccountCode = N'1000')
    INSERT INTO dbo.AccountsTb (AccountCode, AccountName, AccountType, ParentAccountCode) VALUES (N'1000', N'الصندوق الرئيسي', N'Asset', N'12');
IF NOT EXISTS (SELECT 1 FROM dbo.AccountsTb WHERE AccountCode = N'1010')
    INSERT INTO dbo.AccountsTb (AccountCode, AccountName, AccountType, ParentAccountCode) VALUES (N'1010', N'البنك الرئيسي', N'Asset', N'12');
IF NOT EXISTS (SELECT 1 FROM dbo.AccountsTb WHERE AccountCode = N'1100')
    INSERT INTO dbo.AccountsTb (AccountCode, AccountName, AccountType, ParentAccountCode) VALUES (N'1100', N'العملاء', N'Asset', N'13');
IF NOT EXISTS (SELECT 1 FROM dbo.AccountsTb WHERE AccountCode = N'1200')
    INSERT INTO dbo.AccountsTb (AccountCode, AccountName, AccountType, ParentAccountCode) VALUES (N'1200', N'المخزون', N'Asset', N'12');
IF NOT EXISTS (SELECT 1 FROM dbo.AccountsTb WHERE AccountCode = N'1210')
    INSERT INTO dbo.AccountsTb (AccountCode, AccountName, AccountType, ParentAccountCode) VALUES (N'1210', N'ضريبة المدخلات', N'Asset', N'13');
IF NOT EXISTS (SELECT 1 FROM dbo.AccountsTb WHERE AccountCode = N'2000')
    INSERT INTO dbo.AccountsTb (AccountCode, AccountName, AccountType, ParentAccountCode) VALUES (N'2000', N'الموردون', N'Liability', N'21');
IF NOT EXISTS (SELECT 1 FROM dbo.AccountsTb WHERE AccountCode = N'2100')
    INSERT INTO dbo.AccountsTb (AccountCode, AccountName, AccountType, ParentAccountCode) VALUES (N'2100', N'ضريبة المخرجات', N'Liability', N'22');
IF NOT EXISTS (SELECT 1 FROM dbo.AccountsTb WHERE AccountCode = N'3000')
    INSERT INTO dbo.AccountsTb (AccountCode, AccountName, AccountType, ParentAccountCode) VALUES (N'3000', N'رأس المال', N'Equity', N'3');
IF NOT EXISTS (SELECT 1 FROM dbo.AccountsTb WHERE AccountCode = N'3100')
    INSERT INTO dbo.AccountsTb (AccountCode, AccountName, AccountType, ParentAccountCode) VALUES (N'3100', N'الأرباح المحتجزة', N'Equity', N'3');
IF NOT EXISTS (SELECT 1 FROM dbo.AccountsTb WHERE AccountCode = N'4000')
    INSERT INTO dbo.AccountsTb (AccountCode, AccountName, AccountType, ParentAccountCode) VALUES (N'4000', N'المبيعات', N'Revenue', N'41');
IF NOT EXISTS (SELECT 1 FROM dbo.AccountsTb WHERE AccountCode = N'5000')
    INSERT INTO dbo.AccountsTb (AccountCode, AccountName, AccountType, ParentAccountCode) VALUES (N'5000', N'المشتريات / تكلفة البضاعة', N'Expense', N'51');

UPDATE dbo.AccountsTb SET AccountName = N'الأصول', AccountType = N'Asset', ParentAccountCode = NULL WHERE AccountCode = N'1';
UPDATE dbo.AccountsTb SET AccountName = N'الأصول الثابتة', AccountType = N'Asset', ParentAccountCode = N'1' WHERE AccountCode = N'11';
UPDATE dbo.AccountsTb SET AccountName = N'الأصول المتداولة', AccountType = N'Asset', ParentAccountCode = N'1' WHERE AccountCode = N'12';
UPDATE dbo.AccountsTb SET AccountName = N'الذمم والأرصدة المدينة', AccountType = N'Asset', ParentAccountCode = N'1' WHERE AccountCode = N'13';
UPDATE dbo.AccountsTb SET AccountName = N'الخصوم', AccountType = N'Liability', ParentAccountCode = NULL WHERE AccountCode = N'2';
UPDATE dbo.AccountsTb SET AccountName = N'الخصوم المتداولة', AccountType = N'Liability', ParentAccountCode = N'2' WHERE AccountCode = N'21';
UPDATE dbo.AccountsTb SET AccountName = N'ضرائب ورسوم مستحقة', AccountType = N'Liability', ParentAccountCode = N'2' WHERE AccountCode = N'22';
UPDATE dbo.AccountsTb SET AccountName = N'حقوق الملكية', AccountType = N'Equity', ParentAccountCode = NULL WHERE AccountCode = N'3';
UPDATE dbo.AccountsTb SET AccountName = N'الإيرادات', AccountType = N'Revenue', ParentAccountCode = NULL WHERE AccountCode = N'4';
UPDATE dbo.AccountsTb SET AccountName = N'إيرادات النشاط', AccountType = N'Revenue', ParentAccountCode = N'4' WHERE AccountCode = N'41';
UPDATE dbo.AccountsTb SET AccountName = N'المصروفات', AccountType = N'Expense', ParentAccountCode = NULL WHERE AccountCode = N'5';
UPDATE dbo.AccountsTb SET AccountName = N'تكلفة البضاعة والمشتريات', AccountType = N'Expense', ParentAccountCode = N'5' WHERE AccountCode = N'51';
UPDATE dbo.AccountsTb SET AccountName = N'مصروفات تشغيلية', AccountType = N'Expense', ParentAccountCode = N'5' WHERE AccountCode = N'52';
UPDATE dbo.AccountsTb SET AccountName = N'الصندوق الرئيسي', AccountType = N'Asset', ParentAccountCode = N'12' WHERE AccountCode = N'1000';
UPDATE dbo.AccountsTb SET AccountName = N'البنك الرئيسي', AccountType = N'Asset', ParentAccountCode = N'12' WHERE AccountCode = N'1010';
UPDATE dbo.AccountsTb SET AccountName = N'العملاء', AccountType = N'Asset', ParentAccountCode = N'13' WHERE AccountCode = N'1100';
UPDATE dbo.AccountsTb SET AccountName = N'المخزون', AccountType = N'Asset', ParentAccountCode = N'12' WHERE AccountCode = N'1200';
UPDATE dbo.AccountsTb SET AccountName = N'ضريبة المدخلات', AccountType = N'Asset', ParentAccountCode = N'13' WHERE AccountCode = N'1210';
UPDATE dbo.AccountsTb SET AccountName = N'الموردون', AccountType = N'Liability', ParentAccountCode = N'21' WHERE AccountCode = N'2000';
UPDATE dbo.AccountsTb SET AccountName = N'ضريبة المخرجات', AccountType = N'Liability', ParentAccountCode = N'22' WHERE AccountCode = N'2100';
UPDATE dbo.AccountsTb SET AccountName = N'رأس المال', AccountType = N'Equity', ParentAccountCode = N'3' WHERE AccountCode = N'3000';
UPDATE dbo.AccountsTb SET AccountName = N'الأرباح المحتجزة', AccountType = N'Equity', ParentAccountCode = N'3' WHERE AccountCode = N'3100';
UPDATE dbo.AccountsTb SET AccountName = N'المبيعات', AccountType = N'Revenue', ParentAccountCode = N'41' WHERE AccountCode = N'4000';
UPDATE dbo.AccountsTb SET AccountName = N'المشتريات / تكلفة البضاعة', AccountType = N'Expense', ParentAccountCode = N'51' WHERE AccountCode = N'5000';

UPDATE dbo.EmployeeTb1 SET EmpName = N'Ahmed' WHERE EmpName = N'Ahmed ';";

                connection.Open();
                using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        AcquireSchemaMigrationLock(connection, transaction);
                        command.Transaction = transaction;
                        command.ExecuteNonQuery();
                        MigrateEmployeeRoles(connection, transaction);
                        MigrateEmployeeOperationPermissions(connection, transaction);
                        MigrateMedicineBarcode(connection, transaction);
                        MigratePharmacyProfileTable(connection, transaction);
                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        private static void AcquireSchemaMigrationLock(SqlConnection connection, SqlTransaction transaction)
        {
            using (SqlCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"
DECLARE @LockResult INT;
EXEC @LockResult = sys.sp_getapplock
    @Resource = N'PharmacyManagementSchema',
    @LockMode = N'Exclusive',
    @LockOwner = N'Transaction',
    @LockTimeout = 30000;
IF @LockResult < 0
BEGIN
    RAISERROR(N'Unable to acquire schema migration lock.', 16, 1);
END;";
                command.ExecuteNonQuery();
            }
        }

        // يحدّث جدول بيانات الشركة القديم بأعمدة جديدة (أوامر منفصلة لتجنب خطأ compile batch).
        private static void MigrateEmployeeRoles(SqlConnection connection, SqlTransaction transaction)
        {
            ExecuteNonQuery(connection, transaction, "IF COL_LENGTH('dbo.EmployeeTb1', 'EmpRole') IS NULL ALTER TABLE dbo.EmployeeTb1 ADD EmpRole NVARCHAR(50) NULL;");
            ExecuteNonQuery(connection, transaction, @"
UPDATE dbo.EmployeeTb1
SET EmpRole = N'Cashier'
WHERE EmpRole IS NULL OR LTRIM(RTRIM(EmpRole)) = N'';");
            ExecuteNonQuery(connection, transaction, @"
IF EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.EmployeeTb1')
      AND name = 'EmpRole'
      AND is_nullable = 1
)
BEGIN
    ALTER TABLE dbo.EmployeeTb1 ALTER COLUMN EmpRole NVARCHAR(50) NOT NULL;
END;");
            ExecuteNonQuery(connection, transaction, @"
DECLARE @EmployeeRoleDefaultName SYSNAME;
SELECT @EmployeeRoleDefaultName = dc.name
FROM sys.default_constraints dc
INNER JOIN sys.columns c
    ON c.default_object_id = dc.object_id
WHERE dc.parent_object_id = OBJECT_ID('dbo.EmployeeTb1')
  AND c.name = 'EmpRole';

IF @EmployeeRoleDefaultName IS NOT NULL
BEGIN
    DECLARE @DropEmployeeRoleDefaultSql NVARCHAR(MAX);
    SET @DropEmployeeRoleDefaultSql = N'ALTER TABLE dbo.EmployeeTb1 DROP CONSTRAINT ' + QUOTENAME(@EmployeeRoleDefaultName);
    EXEC(@DropEmployeeRoleDefaultSql);
END;

ALTER TABLE dbo.EmployeeTb1 ADD CONSTRAINT DF_EmployeeTb1_EmpRole DEFAULT N'Cashier' FOR EmpRole;");
        }

        private static void MigrateEmployeeOperationPermissions(SqlConnection connection, SqlTransaction transaction)
        {
            ExecuteNonQuery(connection, transaction, @"
IF OBJECT_ID(N'dbo.EmployeeOperationPermissionsTb', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.EmployeeOperationPermissionsTb
    (
        EmpId NVARCHAR(50) NOT NULL,
        OperationName NVARCHAR(100) NOT NULL,
        IsAllowed BIT NOT NULL,
        UpdatedBy NVARCHAR(200) NOT NULL,
        UpdatedAt DATETIME NOT NULL CONSTRAINT DF_EmployeeOperationPermissionsTb_UpdatedAt DEFAULT GETDATE(),
        CONSTRAINT PK_EmployeeOperationPermissionsTb PRIMARY KEY (EmpId, OperationName),
        CONSTRAINT FK_EmployeeOperationPermissionsTb_Employee
            FOREIGN KEY (EmpId) REFERENCES dbo.EmployeeTb1(EmpId) ON DELETE CASCADE
    );
END;");
        }

        private static void MigrateMedicineBarcode(SqlConnection connection, SqlTransaction transaction)
        {
            ExecuteNonQuery(connection, transaction, "IF COL_LENGTH('dbo.MedicineTb1', 'Barcode') IS NULL ALTER TABLE dbo.MedicineTb1 ADD Barcode NVARCHAR(100) NULL;");
            ExecuteNonQuery(connection, transaction, @"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_MedicineTb1_Barcode' AND object_id = OBJECT_ID('dbo.MedicineTb1'))
   AND NOT EXISTS (
       SELECT Barcode
       FROM dbo.MedicineTb1
       WHERE Barcode IS NOT NULL AND Barcode <> N''
       GROUP BY Barcode
       HAVING COUNT(1) > 1
   )
BEGIN
    CREATE UNIQUE INDEX UX_MedicineTb1_Barcode ON dbo.MedicineTb1 (Barcode) WHERE Barcode IS NOT NULL AND Barcode <> N'';
END;");
        }

        private static void MigratePharmacyProfileTable(SqlConnection connection, SqlTransaction transaction)
        {
            if (!TableExists(connection, transaction, "PharmacyProfileTb"))
            {
                return;
            }

            ExecuteNonQuery(connection, transaction, "IF COL_LENGTH('dbo.PharmacyProfileTb', 'NameAr') IS NULL ALTER TABLE dbo.PharmacyProfileTb ADD NameAr NVARCHAR(200) NULL;");
            ExecuteNonQuery(connection, transaction, "IF COL_LENGTH('dbo.PharmacyProfileTb', 'NameEn') IS NULL ALTER TABLE dbo.PharmacyProfileTb ADD NameEn NVARCHAR(200) NULL;");
            ExecuteNonQuery(connection, transaction, "IF COL_LENGTH('dbo.PharmacyProfileTb', 'ActivityAr') IS NULL ALTER TABLE dbo.PharmacyProfileTb ADD ActivityAr NVARCHAR(200) NULL;");
            ExecuteNonQuery(connection, transaction, "IF COL_LENGTH('dbo.PharmacyProfileTb', 'ActivityEn') IS NULL ALTER TABLE dbo.PharmacyProfileTb ADD ActivityEn NVARCHAR(200) NULL;");
            ExecuteNonQuery(connection, transaction, "IF COL_LENGTH('dbo.PharmacyProfileTb', 'CommercialRegNo') IS NULL ALTER TABLE dbo.PharmacyProfileTb ADD CommercialRegNo NVARCHAR(50) NULL;");
            ExecuteNonQuery(connection, transaction, "IF COL_LENGTH('dbo.PharmacyProfileTb', 'LicenseNo') IS NULL ALTER TABLE dbo.PharmacyProfileTb ADD LicenseNo NVARCHAR(100) NULL;");
            ExecuteNonQuery(connection, transaction, "IF COL_LENGTH('dbo.PharmacyProfileTb', 'Mobile') IS NULL ALTER TABLE dbo.PharmacyProfileTb ADD Mobile NVARCHAR(50) NULL;");
            ExecuteNonQuery(connection, transaction, "IF COL_LENGTH('dbo.PharmacyProfileTb', 'TaxNo') IS NULL ALTER TABLE dbo.PharmacyProfileTb ADD TaxNo NVARCHAR(50) NULL;");
            ExecuteNonQuery(connection, transaction, "IF COL_LENGTH('dbo.PharmacyProfileTb', 'InvoiceNotes') IS NULL ALTER TABLE dbo.PharmacyProfileTb ADD InvoiceNotes NVARCHAR(MAX) NULL;");
            ExecuteNonQuery(connection, transaction, "IF COL_LENGTH('dbo.PharmacyProfileTb', 'HeaderFooter') IS NULL ALTER TABLE dbo.PharmacyProfileTb ADD HeaderFooter NVARCHAR(MAX) NULL;");
            ExecuteNonQuery(connection, transaction, "IF COL_LENGTH('dbo.PharmacyProfileTb', 'Fax') IS NULL ALTER TABLE dbo.PharmacyProfileTb ADD Fax NVARCHAR(50) NULL;");
            ExecuteNonQuery(connection, transaction, "IF COL_LENGTH('dbo.PharmacyProfileTb', 'PoBox') IS NULL ALTER TABLE dbo.PharmacyProfileTb ADD PoBox NVARCHAR(50) NULL;");
            ExecuteNonQuery(connection, transaction, "IF COL_LENGTH('dbo.PharmacyProfileTb', 'Website') IS NULL ALTER TABLE dbo.PharmacyProfileTb ADD Website NVARCHAR(200) NULL;");
            ExecuteNonQuery(connection, transaction, "IF COL_LENGTH('dbo.PharmacyProfileTb', 'LogoPath') IS NULL ALTER TABLE dbo.PharmacyProfileTb ADD LogoPath NVARCHAR(500) NULL;");
            ExecuteNonQuery(connection, transaction, "IF COL_LENGTH('dbo.PharmacyProfileTb', 'HeaderLogoPath') IS NULL ALTER TABLE dbo.PharmacyProfileTb ADD HeaderLogoPath NVARCHAR(500) NULL;");
            ExecuteNonQuery(connection, transaction, "IF COL_LENGTH('dbo.PharmacyProfileTb', 'FooterLogoPath') IS NULL ALTER TABLE dbo.PharmacyProfileTb ADD FooterLogoPath NVARCHAR(500) NULL;");
            ExecuteNonQuery(connection, transaction, "IF COL_LENGTH('dbo.CustomersBillsTb', 'InvoiceNotes') IS NULL ALTER TABLE dbo.CustomersBillsTb ADD InvoiceNotes NVARCHAR(MAX) NULL;");

            if (ColumnExists(connection, transaction, "PharmacyProfileTb", "PharmacyName") && ColumnExists(connection, transaction, "PharmacyProfileTb", "NameAr"))
            {
                ExecuteNonQuery(connection, transaction, @"
UPDATE dbo.PharmacyProfileTb
SET NameAr = PharmacyName
WHERE NameAr IS NULL AND PharmacyName IS NOT NULL;");
            }
        }

        private static bool TableExists(SqlConnection connection, SqlTransaction transaction, string tableName)
        {
            using (SqlCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = "SELECT OBJECT_ID(@TableName, 'U')";
                command.Parameters.Add("@TableName", SqlDbType.NVarChar, 128).Value = "dbo." + tableName;
                object result = command.ExecuteScalar();
                return result != null && result != DBNull.Value;
            }
        }

        private static bool ColumnExists(SqlConnection connection, SqlTransaction transaction, string tableName, string columnName)
        {
            using (SqlCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = "SELECT COL_LENGTH(@TableName, @ColumnName)";
                command.Parameters.Add("@TableName", SqlDbType.NVarChar, 128).Value = "dbo." + tableName;
                command.Parameters.Add("@ColumnName", SqlDbType.NVarChar, 128).Value = columnName;
                object result = command.ExecuteScalar();
                return result != null && result != DBNull.Value;
            }
        }

        private static void ExecuteNonQuery(SqlConnection connection, SqlTransaction transaction, string sql)
        {
            using (SqlCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = sql;
                command.ExecuteNonQuery();
            }
        }

        // يجلب بيانات الشركة/الصيدلية المحفوظة.
        public static PharmacyProfile GetPharmacyProfile()
        {
            using (SqlConnection connection = CreateConnection())
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandText = @"
SELECT NameAr, NameEn, ActivityAr, ActivityEn, CommercialRegNo, LicenseNo, Mobile, Phone, TaxNo,
       Address, InvoiceNotes, HeaderFooter, Fax, PoBox, Email, Website, LogoPath
FROM dbo.PharmacyProfileTb
WHERE ProfileKey = @ProfileKey";
                command.Parameters.Add("@ProfileKey", SqlDbType.NVarChar, 20).Value = "MAIN";

                connection.Open();
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return MapPharmacyProfile(reader);
                    }
                }
            }

            return new PharmacyProfile();
        }

        // يحفظ أو يحدّث بيانات الشركة/الصيدلية.
        public static void SavePharmacyProfile(PharmacyProfile profile)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            using (SqlConnection connection = CreateConnection())
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandText = @"
IF EXISTS (SELECT 1 FROM dbo.PharmacyProfileTb WHERE ProfileKey = @ProfileKey)
BEGIN
    UPDATE dbo.PharmacyProfileTb
    SET NameAr = @NameAr, NameEn = @NameEn, ActivityAr = @ActivityAr, ActivityEn = @ActivityEn,
        CommercialRegNo = @CommercialRegNo, LicenseNo = @LicenseNo, Mobile = @Mobile, Phone = @Phone,
        TaxNo = @TaxNo, Address = @Address, InvoiceNotes = @InvoiceNotes, HeaderFooter = @HeaderFooter,
        Fax = @Fax, PoBox = @PoBox, Email = @Email, Website = @Website, LogoPath = @LogoPath
    WHERE ProfileKey = @ProfileKey;
END
ELSE
BEGIN
    INSERT INTO dbo.PharmacyProfileTb
    (ProfileKey, NameAr, NameEn, ActivityAr, ActivityEn, CommercialRegNo, LicenseNo, Mobile, Phone, TaxNo,
     Address, InvoiceNotes, HeaderFooter, Fax, PoBox, Email, Website, LogoPath)
    VALUES
    (@ProfileKey, @NameAr, @NameEn, @ActivityAr, @ActivityEn, @CommercialRegNo, @LicenseNo, @Mobile, @Phone, @TaxNo,
     @Address, @InvoiceNotes, @HeaderFooter, @Fax, @PoBox, @Email, @Website, @LogoPath);
END";
                BindPharmacyProfileParameters(command, profile);
                connection.Open();
                command.ExecuteNonQuery();
            }
        }

        private static PharmacyProfile MapPharmacyProfile(SqlDataReader reader)
        {
            return new PharmacyProfile
            {
                NameAr = ReadString(reader, "NameAr"),
                NameEn = ReadString(reader, "NameEn"),
                ActivityAr = ReadString(reader, "ActivityAr"),
                ActivityEn = ReadString(reader, "ActivityEn"),
                CommercialRegNo = ReadString(reader, "CommercialRegNo"),
                LicenseNo = ReadString(reader, "LicenseNo"),
                Mobile = ReadString(reader, "Mobile"),
                Phone = ReadString(reader, "Phone"),
                TaxNo = ReadString(reader, "TaxNo"),
                Address = ReadString(reader, "Address"),
                InvoiceNotes = ReadString(reader, "InvoiceNotes"),
                HeaderFooter = ReadString(reader, "HeaderFooter"),
                Fax = ReadString(reader, "Fax"),
                PoBox = ReadString(reader, "PoBox"),
                Email = ReadString(reader, "Email"),
                Website = ReadString(reader, "Website"),
                LogoPath = ReadString(reader, "LogoPath")
            };
        }

        private static string ReadString(SqlDataReader reader, string column)
        {
            int ordinal = reader.GetOrdinal(column);
            return reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);
        }

        private static void BindPharmacyProfileParameters(SqlCommand command, PharmacyProfile profile)
        {
            command.Parameters.Add("@ProfileKey", SqlDbType.NVarChar, 20).Value = "MAIN";
            command.Parameters.Add("@NameAr", SqlDbType.NVarChar, 200).Value = profile.NameAr.Trim();
            command.Parameters.Add("@NameEn", SqlDbType.NVarChar, 200).Value = ToDbValue(profile.NameEn);
            command.Parameters.Add("@ActivityAr", SqlDbType.NVarChar, 200).Value = ToDbValue(profile.ActivityAr);
            command.Parameters.Add("@ActivityEn", SqlDbType.NVarChar, 200).Value = ToDbValue(profile.ActivityEn);
            command.Parameters.Add("@CommercialRegNo", SqlDbType.NVarChar, 50).Value = ToDbValue(profile.CommercialRegNo);
            command.Parameters.Add("@LicenseNo", SqlDbType.NVarChar, 100).Value = ToDbValue(profile.LicenseNo);
            command.Parameters.Add("@Mobile", SqlDbType.NVarChar, 50).Value = ToDbValue(profile.Mobile);
            command.Parameters.Add("@Phone", SqlDbType.NVarChar, 50).Value = profile.Phone.Trim();
            command.Parameters.Add("@TaxNo", SqlDbType.NVarChar, 50).Value = ToDbValue(profile.TaxNo);
            command.Parameters.Add("@Address", SqlDbType.NVarChar, 300).Value = profile.Address.Trim();
            command.Parameters.Add("@InvoiceNotes", SqlDbType.NVarChar).Value = ToDbValue(profile.InvoiceNotes);
            command.Parameters.Add("@HeaderFooter", SqlDbType.NVarChar).Value = ToDbValue(profile.HeaderFooter);
            command.Parameters.Add("@Fax", SqlDbType.NVarChar, 50).Value = ToDbValue(profile.Fax);
            command.Parameters.Add("@PoBox", SqlDbType.NVarChar, 50).Value = ToDbValue(profile.PoBox);
            command.Parameters.Add("@Email", SqlDbType.NVarChar, 100).Value = ToDbValue(profile.Email);
            command.Parameters.Add("@Website", SqlDbType.NVarChar, 200).Value = ToDbValue(profile.Website);
            command.Parameters.Add("@LogoPath", SqlDbType.NVarChar, 500).Value = ToDbValue(profile.LogoPath);
        }

        private static object ToDbValue(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? (object)DBNull.Value : value.Trim();
        }

        // يضيف سيرفر للقائمة إذا لم يكن مكررًا.
        public static DataTable GetCurrencies() => CurrencyWorkspaceService.GetCurrencies();

        public static CurrencyInfo GetSystemCurrency() => CurrencyWorkspaceService.GetSystemCurrency();

        public static string GetSystemCurrencyDisplay() => CurrencyWorkspaceService.GetSystemCurrencyDisplay();

        public static void SaveCurrency(CurrencyInfo currency) => CurrencyWorkspaceService.SaveCurrency(currency);

        public static void DeleteCurrency(int currencyId) => CurrencyWorkspaceService.DeleteCurrency(currencyId);

        public static void SetSystemCurrency(int currencyId) => CurrencyWorkspaceService.SetSystemCurrency(currencyId);

        private static void AddServer(HashSet<string> uniqueServers, List<string> servers, string serverName)
        {
            if (!string.IsNullOrWhiteSpace(serverName) && uniqueServers.Add(serverName))
            {
                servers.Add(serverName);
            }
        }

        private static string GetConnectionString(bool includeDatabase)
        {
            DatabaseConnectionProfile profile = GetConnectionProfile();
            if (includeDatabase) return BuildSettingsConnectionString(profile.ServerName, profile.DatabaseName, profile.IntegratedSecurity);
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(
                BuildSettingsConnectionString(profile.ServerName, profile.DatabaseName, profile.IntegratedSecurity));
            if (includeDatabase)
            {
                EnsureUserDatabaseName(builder.InitialCatalog);
            }

            if (!includeDatabase)
            {
                builder.InitialCatalog = string.Empty;
            }

            return builder.ConnectionString;
        }

        private static DatabaseConnectionProfile GetBuildConnectionProfile()
        {
            ConnectionStringSettings settings = ConfigurationManager.ConnectionStrings[ConnectionName];
            if (settings == null || string.IsNullOrWhiteSpace(settings.ConnectionString))
            {
                throw new InvalidOperationException("Missing connection string 'PharmacyConnection' in App.config.");
            }

            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(settings.ConnectionString);
            NormalizeConnectionString(builder, settings.ConnectionString);
            EnsureUserDatabaseName(builder.InitialCatalog);
            return new DatabaseConnectionProfile(builder.DataSource, builder.InitialCatalog, builder.IntegratedSecurity);
        }

        private static void NormalizeConnectionString(SqlConnectionStringBuilder builder, string originalConnectionString)
        {
            builder.Encrypt = true;
            if (builder.ConnectTimeout < 15)
            {
                builder.ConnectTimeout = 15;
            }

            if (!HasExplicitTrustServerCertificate(originalConnectionString))
            {
                builder.TrustServerCertificate = ShouldTrustServerCertificate(builder.DataSource);
            }
        }

        private static bool HasExplicitTrustServerCertificate(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return false;
            }

            foreach (string part in connectionString.Split(';'))
            {
                int equalsIndex = part.IndexOf('=');
                if (equalsIndex <= 0)
                {
                    continue;
                }

                string key = part.Substring(0, equalsIndex).Trim().Replace(" ", string.Empty);
                if (string.Equals(key, "TrustServerCertificate", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

    }

    // بيانات الشركة/الصيدلية للفواتير والتقارير.
    internal sealed class CurrencyInfo
    {
        public int CurrencyId { get; set; }
        public string CountryNameAr { get; set; } = string.Empty;
        public string CurrencyNameAr { get; set; } = string.Empty;
        public string CurrencyCode { get; set; } = string.Empty;
        public string CurrencySymbol { get; set; } = string.Empty;
        public bool IsSystemCurrency { get; set; }
    }

    internal sealed class PharmacyProfile
    {
        public string NameAr { get; set; } = string.Empty;
        public string NameEn { get; set; } = string.Empty;
        public string ActivityAr { get; set; } = string.Empty;
        public string ActivityEn { get; set; } = string.Empty;
        public string CommercialRegNo { get; set; } = string.Empty;
        public string LicenseNo { get; set; } = string.Empty;
        public string Mobile { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string TaxNo { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string InvoiceNotes { get; set; } = string.Empty;
        public string HeaderFooter { get; set; } = string.Empty;
        public string Fax { get; set; } = string.Empty;
        public string PoBox { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Website { get; set; } = string.Empty;
        public string LogoPath { get; set; } = string.Empty;
    }

    internal sealed class DatabaseSchemaValidationResult
    {
        public DatabaseSchemaValidationResult(string databaseName, int tableCount, int versionNo)
        {
            DatabaseName = databaseName ?? string.Empty;
            TableCount = tableCount;
            VersionNo = versionNo;
        }

        public string DatabaseName { get; private set; }
        public int TableCount { get; private set; }
        public int VersionNo { get; private set; }
    }
}

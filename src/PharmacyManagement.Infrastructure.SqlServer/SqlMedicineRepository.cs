using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Application;

namespace PharmacyManagement.Infrastructure.SqlServer
{
    public sealed class SqlMedicineRepository : IMedicineRepository, IMedicineCatalogImportRepository
    {
        private const string DirectorySql = @"SELECT MedName, Barcode, Bprice, Sprice, MedQty, ProdDate, ExpDate, CompName
FROM dbo.MedicineTb1
ORDER BY MedName;";
        private const string DirectoryNamesSql = @"SELECT MedName
FROM dbo.MedicineTb1
ORDER BY MedName;";
        private const string CompanyOptionsSql = @"SELECT DISTINCT CompName
FROM dbo.CompanyTb1
WHERE NULLIF(LTRIM(RTRIM(CompName)), N'') IS NOT NULL
ORDER BY CompName;";
        private readonly Func<SqlConnection> connectionFactory;

        public SqlMedicineRepository(Func<SqlConnection> connectionFactory)
        {
            this.connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        }

        public IReadOnlyList<MedicineRecord> GetAll()
        {
            var medicines = new List<MedicineRecord>();
            using (SqlConnection connection = connectionFactory())
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandText = DirectorySql;
                connection.Open();
                using (SqlDataReader reader = command.ExecuteReader())
                    while (reader.Read()) medicines.Add(Map(reader));
            }
            return medicines;
        }

        public IReadOnlyList<string> GetCompanyNames()
        {
            var companies = new List<string>();
            using (SqlConnection connection = connectionFactory())
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandText = CompanyOptionsSql;
                connection.Open();
                using (SqlDataReader reader = command.ExecuteReader())
                    while (reader.Read()) companies.Add(Convert.ToString(reader[0]));
            }
            return companies;
        }

        public async Task<IReadOnlyList<MedicineRecord>> GetAllAsync(CancellationToken cancellationToken)
        {
            var medicines = new List<MedicineRecord>();
            using (SqlConnection connection = connectionFactory())
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandText = DirectorySql;
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                using (SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) medicines.Add(Map(reader));
            }
            return medicines;
        }

        public async Task<IReadOnlyList<string>> GetNamesAsync(CancellationToken cancellationToken)
        {
            var names = new List<string>();
            using (SqlConnection connection = connectionFactory())
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandText = DirectoryNamesSql;
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                using (SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                        names.Add(Convert.ToString(reader[0]));
            }
            return names;
        }

        public async Task<MedicineRecord> FindByNameAsync(string medicineName, CancellationToken cancellationToken)
        {
            using (SqlConnection connection = connectionFactory())
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandText = DirectorySql.Replace("ORDER BY MedName;", "WHERE MedName=@MedicineName ORDER BY MedName;");
                command.Parameters.Add("@MedicineName", SqlDbType.NVarChar, 200).Value = medicineName;
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                using (SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        MedicineRecord value = Map(reader);
                        // SQL collations can equate Arabic characters/marks that are distinct in the editor.
                        if (string.Equals(value.MedName, medicineName, StringComparison.OrdinalIgnoreCase)) return value;
                    }
            }
            return null;
        }

        public async Task<IReadOnlyList<string>> GetCompanyNamesAsync(CancellationToken cancellationToken)
        {
            var companies = new List<string>();
            using (SqlConnection connection = connectionFactory())
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandText = CompanyOptionsSql;
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                using (SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) companies.Add(Convert.ToString(reader[0]));
            }
            return companies;
        }

        public bool IsBarcodeAvailable(string barcode, string excludedMedicineName)
        {
            if (string.IsNullOrWhiteSpace(barcode)) return true;
            using (SqlConnection connection = connectionFactory())
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandText = @"SELECT COUNT(1)
FROM dbo.MedicineTb1
WHERE Barcode=@Barcode AND MedName<>@ExcludedMedicineName;";
                AddBarcode(command, barcode);
                command.Parameters.Add("@ExcludedMedicineName", SqlDbType.NVarChar, 200).Value =
                    string.IsNullOrWhiteSpace(excludedMedicineName) ? string.Empty : excludedMedicineName.Trim();
                connection.Open();
                return Convert.ToInt32(command.ExecuteScalar()) == 0;
            }
        }

        public async Task<bool> IsBarcodeAvailableAsync(string barcode, string excludedMedicineName, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(barcode)) return true;
            using (SqlConnection connection = connectionFactory())
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandText = @"SELECT COUNT(1)
FROM dbo.MedicineTb1
WHERE Barcode=@Barcode AND MedName<>@ExcludedMedicineName;";
                AddBarcode(command, barcode);
                command.Parameters.Add("@ExcludedMedicineName", SqlDbType.NVarChar, 200).Value =
                    string.IsNullOrWhiteSpace(excludedMedicineName) ? string.Empty : excludedMedicineName.Trim();
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)) == 0;
            }
        }

        public void Create(MedicineRecord medicine, OperationActor actor)
        { CreateAsync(medicine, actor).GetAwaiter().GetResult(); }

        public async Task CreateAsync(MedicineRecord medicine, OperationActor actor)
        {
            using (SqlConnection connection = connectionFactory())
            {
                await connection.OpenAsync().ConfigureAwait(false);
                using (SqlTransaction transaction = await SqlTransactionBoundaryDispatcher.BeginAsync(connection, IsolationLevel.Serializable).ConfigureAwait(false))
                {
                    try
                    {
                        await EnsureLegacyAggregateMutationAllowedAsync(connection, transaction).ConfigureAwait(false);
                        await EnsureNameAvailableAsync(connection, transaction, medicine.MedName).ConfigureAwait(false);
                        await EnsureBarcodeAvailableAsync(connection, transaction, medicine.Barcode, medicine.MedName).ConfigureAwait(false);
                        using (SqlCommand command = new SqlCommand(@"INSERT INTO dbo.MedicineTb1
(MedName, Barcode, Bprice, Sprice, MedQty, ProdDate, ExpDate, CompName)
VALUES (@MedName, @Barcode, @BuyingPrice, @SellingPrice, @Quantity, @ProductionDate, @ExpiryDate, @CompanyName);", connection, transaction))
                        {
                            Bind(command, medicine);
                            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
                        }
                        await InsertAuditAsync(connection, transaction, actor, "MedicineCreated",
                            "تمت إضافة دواء: " + medicine.MedName + "، الكمية: " + medicine.Quantity + "، الشركة: " + medicine.CompanyName + ".").ConfigureAwait(false);
                        await SqlTransactionBoundaryDispatcher.CommitAsync(transaction).ConfigureAwait(false);
                    }
                    catch (Exception operationFailure)
                    {
                        try { await SqlTransactionBoundaryDispatcher.RollbackAsync(transaction).ConfigureAwait(false); }
                        catch (Exception rollbackFailure)
                        { throw new AggregateException("SQL operation failed and rollback could not be confirmed.", operationFailure, rollbackFailure); }
                        throw;
                    }
                }
            }
        }

        public MedicineCatalogImportResult ImportNew(IReadOnlyList<MedicineRecord> medicines, OperationActor actor)
        { return ImportNewCore(medicines, actor, false); }

        // Executes the exact batch SQL in QA, then rolls back document, stock and audit changes.
        public MedicineCatalogImportResult VerifyQaImportRollback(IReadOnlyList<MedicineRecord> medicines, OperationActor actor)
        { return ImportNewCore(medicines, actor, true); }

        private MedicineCatalogImportResult ImportNewCore(IReadOnlyList<MedicineRecord> medicines,
            OperationActor actor, bool rollBackForQa)
        {
            if (medicines == null || medicines.Count == 0)
                throw new ArgumentException("لا توجد أصناف للاستيراد.", nameof(medicines));
            using (SqlConnection connection = connectionFactory())
            {
                connection.Open();
                if (rollBackForQa && !connection.Database.EndsWith("_qa", StringComparison.OrdinalIgnoreCase))
                    throw new UnauthorizedAccessException("فحص الاستيراد مع الاستعادة متاح لقاعدة QA فقط.");
                using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        var result = new MedicineCatalogImportResult();
                        foreach (MedicineRecord medicine in medicines)
                        {
                            if (medicine.Quantity != 0)
                                throw new InvalidOperationException("تعريف الصنف المستورد يجب أن يبدأ برصيد صفر.");
                            if (!CompanyExists(connection, transaction, medicine.CompanyName))
                                throw new InvalidOperationException("الشركة المصنعة غير مسجلة: " + medicine.CompanyName);
                            if (MedicineExists(connection, transaction, medicine.MedName))
                            { result.SkippedExisting++; continue; }
                            EnsureBarcodeAvailable(connection, transaction, medicine.Barcode, medicine.MedName);
                            using (SqlCommand command = new SqlCommand(@"INSERT INTO dbo.MedicineTb1
(MedName, Barcode, Bprice, Sprice, MedQty, ProdDate, ExpDate, CompName)
VALUES (@MedName, @Barcode, @BuyingPrice, @SellingPrice, @Quantity, @ProductionDate, @ExpiryDate, @CompanyName);", connection, transaction))
                            {
                                Bind(command, medicine);
                                command.ExecuteNonQuery();
                            }
                            InsertAudit(connection, transaction, actor, "MedicineCatalogImported",
                                "استيراد تعريف صنف برصيد صفر: " + medicine.MedName + ".");
                            result.Created++;
                        }
                        if (rollBackForQa) transaction.Rollback();
                        else transaction.Commit();
                        return result;
                    }
                    catch { transaction.Rollback(); throw; }
                }
            }
        }

        private static bool CompanyExists(SqlConnection connection, SqlTransaction transaction, string companyName)
        {
            using (SqlCommand command = new SqlCommand(@"SELECT COUNT(1) FROM dbo.CompanyTb1 WITH (UPDLOCK, HOLDLOCK)
WHERE CompName=@CompanyName;", connection, transaction))
            {
                command.Parameters.Add("@CompanyName", SqlDbType.NVarChar, 200).Value = companyName;
                return Convert.ToInt32(command.ExecuteScalar()) > 0;
            }
        }

        public bool Update(MedicineRecord medicine, OperationActor actor)
        { return UpdateAsync(medicine, actor).GetAwaiter().GetResult(); }

        public async Task<bool> UpdateAsync(MedicineRecord medicine, OperationActor actor)
        {
            using (SqlConnection connection = connectionFactory())
            {
                await connection.OpenAsync().ConfigureAwait(false);
                using (SqlTransaction transaction = await SqlTransactionBoundaryDispatcher.BeginAsync(connection, IsolationLevel.Serializable).ConfigureAwait(false))
                {
                    try
                    {
                        await EnsureLegacyAggregateMutationAllowedAsync(connection, transaction).ConfigureAwait(false);
                        if (!await MedicineExistsAsync(connection, transaction, medicine.MedName).ConfigureAwait(false))
                        {
                            await SqlTransactionBoundaryDispatcher.CommitAsync(transaction).ConfigureAwait(false);
                            return false;
                        }
                        await EnsureBarcodeAvailableAsync(connection, transaction, medicine.Barcode, medicine.MedName).ConfigureAwait(false);
                        int affected;
                        using (SqlCommand command = new SqlCommand(@"UPDATE dbo.MedicineTb1
SET Barcode=@Barcode, Bprice=@BuyingPrice, Sprice=@SellingPrice, MedQty=@Quantity,
    ProdDate=@ProductionDate, ExpDate=@ExpiryDate, CompName=@CompanyName
WHERE MedName=@MedName;", connection, transaction))
                        {
                            Bind(command, medicine);
                            affected = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
                        }
                        if (affected > 0)
                            await InsertAuditAsync(connection, transaction, actor, "MedicineUpdated",
                                "تم تعديل دواء: " + medicine.MedName + "، الكمية: " + medicine.Quantity + "، الشركة: " + medicine.CompanyName + ".").ConfigureAwait(false);
                        await SqlTransactionBoundaryDispatcher.CommitAsync(transaction).ConfigureAwait(false);
                        return affected > 0;
                    }
                    catch (Exception operationFailure)
                    {
                        try { await SqlTransactionBoundaryDispatcher.RollbackAsync(transaction).ConfigureAwait(false); }
                        catch (Exception rollbackFailure)
                        { throw new AggregateException("SQL operation failed and rollback could not be confirmed.", operationFailure, rollbackFailure); }
                        throw;
                    }
                }
            }
        }

        public bool Delete(string medicineName, OperationActor actor)
        { return DeleteAsync(medicineName, actor).GetAwaiter().GetResult(); }

        public async Task<bool> DeleteAsync(string medicineName, OperationActor actor)
        {
            using (SqlConnection connection = connectionFactory())
            {
                await connection.OpenAsync().ConfigureAwait(false);
                using (SqlTransaction transaction = await SqlTransactionBoundaryDispatcher.BeginAsync(connection, IsolationLevel.Serializable).ConfigureAwait(false))
                {
                    try
                    {
                        await EnsureLegacyAggregateMutationAllowedAsync(connection, transaction).ConfigureAwait(false);
                        int affected;
                        using (SqlCommand command = new SqlCommand(
                            "DELETE FROM dbo.MedicineTb1 WHERE MedName=@MedName;", connection, transaction))
                        {
                            command.Parameters.Add("@MedName", SqlDbType.NVarChar, 200).Value = medicineName;
                            affected = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
                        }
                        if (affected > 0)
                            await InsertAuditAsync(connection, transaction, actor, "MedicineDeleted", "تم حذف دواء: " + medicineName + ".").ConfigureAwait(false);
                        await SqlTransactionBoundaryDispatcher.CommitAsync(transaction).ConfigureAwait(false);
                        return affected > 0;
                    }
                    catch (Exception operationFailure)
                    {
                        try { await SqlTransactionBoundaryDispatcher.RollbackAsync(transaction).ConfigureAwait(false); }
                        catch (Exception rollbackFailure)
                        { throw new AggregateException("SQL operation failed and rollback could not be confirmed.", operationFailure, rollbackFailure); }
                        throw;
                    }
                }
            }
        }

        private static void EnsureNameAvailable(SqlConnection connection, SqlTransaction transaction, string medicineName)
        { EnsureNameAvailableAsync(connection, transaction, medicineName).GetAwaiter().GetResult(); }

        private static async Task EnsureNameAvailableAsync(SqlConnection connection, SqlTransaction transaction, string medicineName)
        {
            using (SqlCommand command = new SqlCommand(@"SELECT COUNT(1)
FROM dbo.MedicineTb1 WITH (UPDLOCK, HOLDLOCK)
WHERE MedName=@MedName;", connection, transaction))
            {
                command.Parameters.Add("@MedName", SqlDbType.NVarChar, 200).Value = medicineName;
                if (Convert.ToInt32(await command.ExecuteScalarAsync().ConfigureAwait(false)) > 0)
                    throw new InvalidOperationException("يوجد دواء بالاسم نفسه.");
            }
        }

        private static bool MedicineExists(SqlConnection connection, SqlTransaction transaction, string medicineName)
        { return MedicineExistsAsync(connection, transaction, medicineName).GetAwaiter().GetResult(); }

        private static async Task<bool> MedicineExistsAsync(SqlConnection connection, SqlTransaction transaction, string medicineName)
        {
            using (SqlCommand command = new SqlCommand(@"SELECT COUNT(1)
FROM dbo.MedicineTb1 WITH (UPDLOCK, HOLDLOCK)
WHERE MedName=@MedName;", connection, transaction))
            {
                command.Parameters.Add("@MedName", SqlDbType.NVarChar, 200).Value = medicineName;
                return Convert.ToInt32(await command.ExecuteScalarAsync().ConfigureAwait(false)) > 0;
            }
        }

        private static void EnsureBarcodeAvailable(SqlConnection connection, SqlTransaction transaction,
            string barcode, string excludedMedicineName)
        { EnsureBarcodeAvailableAsync(connection, transaction, barcode, excludedMedicineName).GetAwaiter().GetResult(); }

        private static async Task EnsureBarcodeAvailableAsync(SqlConnection connection, SqlTransaction transaction,
            string barcode, string excludedMedicineName)
        {
            if (string.IsNullOrWhiteSpace(barcode)) return;
            using (SqlCommand command = new SqlCommand(@"SELECT COUNT(1)
FROM dbo.MedicineTb1 WITH (UPDLOCK, HOLDLOCK)
WHERE Barcode=@Barcode AND MedName<>@ExcludedMedicineName;", connection, transaction))
            {
                AddBarcode(command, barcode);
                command.Parameters.Add("@ExcludedMedicineName", SqlDbType.NVarChar, 200).Value = excludedMedicineName;
                if (Convert.ToInt32(await command.ExecuteScalarAsync().ConfigureAwait(false)) > 0)
                    throw new InvalidOperationException("الباركود مستخدم لدواء آخر.");
            }
        }

        private static void EnsureLegacyAggregateMutationAllowed(SqlConnection connection, SqlTransaction transaction)
        { EnsureLegacyAggregateMutationAllowedAsync(connection, transaction).GetAwaiter().GetResult(); }

        private static async Task EnsureLegacyAggregateMutationAllowedAsync(SqlConnection connection, SqlTransaction transaction)
        {
            using (SqlCommand command = new SqlCommand(@"IF OBJECT_ID('dbo.CommercialOperationalSettingsTb', 'U') IS NULL
    SELECT CAST(0 AS BIT)
ELSE
    SELECT CAST(CASE WHEN EXISTS (
        SELECT 1 FROM dbo.CommercialOperationalSettingsTb WITH (UPDLOCK, HOLDLOCK)
        WHERE SettingKey=N'Mode' AND SettingValue=N'FullCommercial') THEN 1 ELSE 0 END AS BIT);", connection, transaction))
            {
                if (Convert.ToBoolean(await command.ExecuteScalarAsync().ConfigureAwait(false)))
                    throw new InvalidOperationException("وضع البيع التجاري مطلوب؛ لا يمكن تعديل الرصيد المجمع من شاشة الأدوية. استخدم عملية دفعات موثقة حتى تبقى كمية الصنف متطابقة مع التشغيلات.");
            }
        }

        private static void Bind(SqlCommand command, MedicineRecord medicine)
        {
            command.Parameters.Add("@MedName", SqlDbType.NVarChar, 200).Value = medicine.MedName;
            AddBarcode(command, medicine.Barcode);
            command.Parameters.Add("@BuyingPrice", SqlDbType.Int).Value = medicine.BuyingPrice;
            command.Parameters.Add("@SellingPrice", SqlDbType.Int).Value = medicine.SellingPrice;
            command.Parameters.Add("@Quantity", SqlDbType.Int).Value = medicine.Quantity;
            command.Parameters.Add("@ProductionDate", SqlDbType.DateTime).Value = medicine.ProductionDate;
            command.Parameters.Add("@ExpiryDate", SqlDbType.DateTime).Value = medicine.ExpiryDate;
            command.Parameters.Add("@CompanyName", SqlDbType.NVarChar, 200).Value = medicine.CompanyName;
        }

        private static void AddBarcode(SqlCommand command, string barcode)
        {
            command.Parameters.Add("@Barcode", SqlDbType.NVarChar, 100).Value =
                string.IsNullOrWhiteSpace(barcode) ? (object)DBNull.Value : barcode.Trim();
        }

        private static void InsertAudit(SqlConnection connection, SqlTransaction transaction, OperationActor actor,
            string action, string details)
        { InsertAuditAsync(connection, transaction, actor, action, details).GetAwaiter().GetResult(); }

        private static async Task InsertAuditAsync(SqlConnection connection, SqlTransaction transaction, OperationActor actor,
            string action, string details)
        {
            using (SqlCommand command = new SqlCommand(@"INSERT INTO dbo.AuditLogTb
(UserName, UserRole, ActionName, EntityName, Details)
VALUES (@UserName, @UserRole, @ActionName, N'Medicine', @Details);", connection, transaction))
            {
                command.Parameters.Add("@UserName", SqlDbType.NVarChar, 200).Value = actor.UserName;
                command.Parameters.Add("@UserRole", SqlDbType.NVarChar, 50).Value = actor.UserRole;
                command.Parameters.Add("@ActionName", SqlDbType.NVarChar, 100).Value = action;
                command.Parameters.Add("@Details", SqlDbType.NVarChar, 1000).Value = details;
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        private static MedicineRecord Map(SqlDataReader reader)
        {
            return new MedicineRecord
            {
                MedName = Convert.ToString(reader["MedName"]),
                Barcode = Convert.ToString(reader["Barcode"]),
                BuyingPrice = Convert.ToInt32(reader["Bprice"]),
                SellingPrice = Convert.ToInt32(reader["Sprice"]),
                Quantity = Convert.ToInt32(reader["MedQty"]),
                ProductionDate = reader["ProdDate"] == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(reader["ProdDate"]),
                ExpiryDate = reader["ExpDate"] == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(reader["ExpDate"]),
                CompanyName = Convert.ToString(reader["CompName"])
            };
        }
    }
}

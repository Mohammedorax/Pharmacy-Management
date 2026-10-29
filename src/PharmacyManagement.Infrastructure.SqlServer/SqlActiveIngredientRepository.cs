using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using PharmacyManagement.Application;

namespace PharmacyManagement.Infrastructure.SqlServer
{
    public sealed class SqlActiveIngredientRepository : IActiveIngredientRepository
    {
        private readonly Func<SqlConnection> connectionFactory;

        public SqlActiveIngredientRepository(Func<SqlConnection> connectionFactory)
        {
            this.connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        }

        public IReadOnlyList<ActiveIngredientSummary> GetIngredients()
        {
            var result = new List<ActiveIngredientSummary>();
            using (SqlConnection connection = connectionFactory())
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandText = @"SELECT ai.ActiveIngredientId, ai.ArabicName, ai.LatinName, ai.IsActive,
COUNT(DISTINCT mai.MedName) AS LinkedMedicineCount,
COUNT(DISTINCT CASE WHEN ev.IsActive=1 THEN ev.EvidenceId END) AS ActiveEvidenceCount
FROM dbo.ActiveIngredientsTb ai
LEFT JOIN dbo.MedicineActiveIngredientsTb mai ON mai.ActiveIngredientId=ai.ActiveIngredientId
LEFT JOIN dbo.ActiveIngredientEvidenceTb ev ON ev.ActiveIngredientId=ai.ActiveIngredientId
GROUP BY ai.ActiveIngredientId,ai.ArabicName,ai.LatinName,ai.IsActive
ORDER BY ai.IsActive DESC,ai.ArabicName;";
                connection.Open();
                using (SqlDataReader reader = command.ExecuteReader())
                    while (reader.Read())
                        result.Add(new ActiveIngredientSummary
                        {
                            ActiveIngredientId = reader.GetGuid(reader.GetOrdinal("ActiveIngredientId")),
                            ArabicName = Convert.ToString(reader["ArabicName"]),
                            LatinName = Convert.ToString(reader["LatinName"]),
                            IsActive = Convert.ToBoolean(reader["IsActive"]),
                            LinkedMedicineCount = Convert.ToInt32(reader["LinkedMedicineCount"]),
                            ActiveEvidenceCount = Convert.ToInt32(reader["ActiveEvidenceCount"])
                        });
            }
            return result;
        }

        public IReadOnlyList<string> GetMedicineNames()
        {
            var result = new List<string>();
            using (SqlConnection connection = connectionFactory())
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandText = @"SELECT MedName FROM dbo.MedicineTb1
WHERE MedName IS NOT NULL AND LEN(LTRIM(RTRIM(MedName)))>0 ORDER BY MedName;";
                connection.Open();
                using (SqlDataReader reader = command.ExecuteReader())
                    while (reader.Read()) result.Add(Convert.ToString(reader[0]));
            }
            return result;
        }

        public IReadOnlyList<ActiveIngredientEvidenceRecord> GetEvidence(Guid ingredientId)
        {
            var result = new List<ActiveIngredientEvidenceRecord>();
            using (SqlConnection connection = connectionFactory())
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandText = @"SELECT EvidenceId,SourceTitle,Publisher,SourceUrl,RetrievedOn,IsActive,
CreatedBy,CreatedAtUtc,DeactivatedBy
FROM dbo.ActiveIngredientEvidenceTb
WHERE ActiveIngredientId=@Id ORDER BY IsActive DESC,CreatedAtUtc DESC;";
                command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = ingredientId;
                connection.Open();
                using (SqlDataReader reader = command.ExecuteReader())
                    while (reader.Read())
                        result.Add(new ActiveIngredientEvidenceRecord
                        {
                            EvidenceId = reader.GetGuid(reader.GetOrdinal("EvidenceId")),
                            SourceTitle = Convert.ToString(reader["SourceTitle"]),
                            Publisher = Convert.ToString(reader["Publisher"]),
                            SourceUrl = Convert.ToString(reader["SourceUrl"]),
                            RetrievedOn = reader["RetrievedOn"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(reader["RetrievedOn"]),
                            IsActive = Convert.ToBoolean(reader["IsActive"]),
                            CreatedBy = Convert.ToString(reader["CreatedBy"]),
                            CreatedAtUtc = Convert.ToDateTime(reader["CreatedAtUtc"]),
                            DeactivatedBy = Convert.ToString(reader["DeactivatedBy"])
                        });
            }
            return result;
        }

        public IReadOnlyList<ActiveIngredientMappingRecord> GetMappings(Guid ingredientId)
        {
            var result = new List<ActiveIngredientMappingRecord>();
            using (SqlConnection connection = connectionFactory())
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandText = @"SELECT MedName,StrengthValue,StrengthUnit,IsPrimary,CreatedAtUtc
FROM dbo.MedicineActiveIngredientsTb
WHERE ActiveIngredientId=@Id ORDER BY IsPrimary DESC,MedName;";
                command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = ingredientId;
                connection.Open();
                using (SqlDataReader reader = command.ExecuteReader())
                    while (reader.Read())
                        result.Add(new ActiveIngredientMappingRecord
                        {
                            MedicineName = Convert.ToString(reader["MedName"]),
                            StrengthValue = reader["StrengthValue"] == DBNull.Value ? (decimal?)null : Convert.ToDecimal(reader["StrengthValue"]),
                            StrengthUnit = Convert.ToString(reader["StrengthUnit"]),
                            IsPrimary = Convert.ToBoolean(reader["IsPrimary"]),
                            CreatedAtUtc = Convert.ToDateTime(reader["CreatedAtUtc"])
                        });
            }
            return result;
        }

        public Guid CreateWithEvidence(ActiveIngredientDefinition definition, ActiveIngredientEvidenceInput evidence, OperationActor actor)
        {
            Guid ingredientId = Guid.NewGuid();
            using (SqlConnection connection = connectionFactory())
            {
                connection.Open();
                using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        using (SqlCommand duplicate = new SqlCommand(@"SELECT COUNT(1) FROM dbo.ActiveIngredientsTb WITH(UPDLOCK,HOLDLOCK)
WHERE ArabicName=@ArabicName;", connection, transaction))
                        {
                            duplicate.Parameters.Add("@ArabicName", SqlDbType.NVarChar, 200).Value = definition.ArabicName;
                            if (Convert.ToInt32(duplicate.ExecuteScalar()) > 0)
                                throw new InvalidOperationException("توجد مادة فعالة بالاسم العربي نفسه.");
                        }
                        using (SqlCommand command = new SqlCommand(@"INSERT dbo.ActiveIngredientsTb
(ActiveIngredientId,ArabicName,LatinName,IsActive) VALUES(@Id,@Arabic,@Latin,1);", connection, transaction))
                        {
                            command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = ingredientId;
                            command.Parameters.Add("@Arabic", SqlDbType.NVarChar, 200).Value = definition.ArabicName;
                            command.Parameters.Add("@Latin", SqlDbType.NVarChar, 200).Value =
                                definition.LatinName.Length == 0 ? (object)DBNull.Value : definition.LatinName;
                            command.ExecuteNonQuery();
                        }
                        AddEvidenceInTransaction(connection, transaction, ingredientId, evidence, actor.UserName);
                        InsertAudit(connection, transaction, actor, "ActiveIngredientCreated", "ActiveIngredient",
                            "أضيفت مادة فعالة مع مصدر موثق: " + definition.ArabicName + ".");
                        transaction.Commit();
                        return ingredientId;
                    }
                    catch { transaction.Rollback(); throw; }
                }
            }
        }

        public Guid AddEvidence(Guid ingredientId, ActiveIngredientEvidenceInput evidence, OperationActor actor)
        {
            using (SqlConnection connection = connectionFactory())
            {
                connection.Open();
                using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        EnsureIngredientExists(connection, transaction, ingredientId);
                        Guid evidenceId = AddEvidenceInTransaction(connection, transaction, ingredientId, evidence, actor.UserName);
                        InsertAudit(connection, transaction, actor, "ActiveIngredientEvidenceAdded", "ActiveIngredient",
                            "أضيف مصدر موثق للمادة: " + ingredientId.ToString("D") + ".");
                        transaction.Commit();
                        return evidenceId;
                    }
                    catch { transaction.Rollback(); throw; }
                }
            }
        }

        public void DeactivateEvidence(Guid evidenceId, OperationActor actor)
        {
            using (SqlConnection connection = connectionFactory())
            {
                connection.Open();
                using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        using (SqlCommand command = new SqlCommand(@"UPDATE dbo.ActiveIngredientEvidenceTb
SET IsActive=0,DeactivatedAtUtc=SYSUTCDATETIME(),DeactivatedBy=@By
WHERE EvidenceId=@Id AND IsActive=1;", connection, transaction))
                        {
                            command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = evidenceId;
                            command.Parameters.Add("@By", SqlDbType.NVarChar, 200).Value = actor.UserName;
                            if (command.ExecuteNonQuery() != 1)
                                throw new InvalidOperationException("المصدر معطل مسبقًا أو تغير بواسطة مستخدم آخر.");
                        }
                        InsertAudit(connection, transaction, actor, "ActiveIngredientEvidenceDeactivated", "ActiveIngredient",
                            "عُطل مصدر المادة الفعالة: " + evidenceId.ToString("D") + ".");
                        transaction.Commit();
                    }
                    catch { transaction.Rollback(); throw; }
                }
            }
        }

        public void ToggleIngredient(Guid ingredientId, OperationActor actor)
        {
            using (SqlConnection connection = connectionFactory())
            {
                connection.Open();
                using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        using (SqlCommand command = new SqlCommand(@"UPDATE dbo.ActiveIngredientsTb
SET IsActive=CASE WHEN IsActive=1 THEN 0 ELSE 1 END WHERE ActiveIngredientId=@Id;", connection, transaction))
                        {
                            command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = ingredientId;
                            if (command.ExecuteNonQuery() != 1) throw new InvalidOperationException("تعذر تغيير حالة المادة الفعالة.");
                        }
                        InsertAudit(connection, transaction, actor, "ActiveIngredientToggled", "ActiveIngredient",
                            "تغيرت حالة مادة فعالة بالمعرف: " + ingredientId.ToString("D") + ".");
                        transaction.Commit();
                    }
                    catch { transaction.Rollback(); throw; }
                }
            }
        }

        public void SaveMapping(ActiveIngredientMappingInput mapping, OperationActor actor)
        {
            using (SqlConnection connection = connectionFactory())
            {
                connection.Open();
                using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        EnsureIngredientExists(connection, transaction, mapping.ActiveIngredientId);
                        using (SqlCommand command = new SqlCommand(@"IF @IsPrimary=1
 UPDATE dbo.MedicineActiveIngredientsTb SET IsPrimary=0 WHERE MedName=@MedName;
IF EXISTS(SELECT 1 FROM dbo.MedicineActiveIngredientsTb WITH(UPDLOCK,HOLDLOCK)
 WHERE MedName=@MedName AND ActiveIngredientId=@IngredientId)
 UPDATE dbo.MedicineActiveIngredientsTb SET StrengthValue=@Strength,StrengthUnit=@Unit,IsPrimary=@IsPrimary
 WHERE MedName=@MedName AND ActiveIngredientId=@IngredientId;
ELSE INSERT dbo.MedicineActiveIngredientsTb(MedName,ActiveIngredientId,StrengthValue,StrengthUnit,IsPrimary)
 VALUES(@MedName,@IngredientId,@Strength,@Unit,@IsPrimary);", connection, transaction))
                        {
                            command.Parameters.Add("@MedName", SqlDbType.NVarChar, 200).Value = mapping.MedicineName;
                            command.Parameters.Add("@IngredientId", SqlDbType.UniqueIdentifier).Value = mapping.ActiveIngredientId;
                            SqlParameter strength = command.Parameters.Add("@Strength", SqlDbType.Decimal);
                            strength.Precision = 18; strength.Scale = 3;
                            strength.Value = mapping.StrengthValue.HasValue ? (object)mapping.StrengthValue.Value : DBNull.Value;
                            command.Parameters.Add("@Unit", SqlDbType.NVarChar, 30).Value =
                                mapping.StrengthValue.HasValue ? (object)mapping.StrengthUnit : DBNull.Value;
                            command.Parameters.Add("@IsPrimary", SqlDbType.Bit).Value = mapping.IsPrimary;
                            command.ExecuteNonQuery();
                        }
                        InsertAudit(connection, transaction, actor, "MedicineActiveIngredientSaved", "Medicine",
                            "ربطت مادة فعالة بالصنف: " + mapping.MedicineName + ".");
                        transaction.Commit();
                    }
                    catch { transaction.Rollback(); throw; }
                }
            }
        }

        public static Guid AddEvidenceInTransaction(SqlConnection connection, SqlTransaction transaction,
            Guid ingredientId, ActiveIngredientEvidenceInput evidence, string createdBy)
        {
            if (connection == null || transaction == null || ingredientId == Guid.Empty)
                throw new ArgumentException("بيانات حفظ المصدر غير مكتملة.");
            string title = ActiveIngredientInputValidator.RequiredEvidenceTitle(evidence?.SourceTitle);
            string publisher = ActiveIngredientInputValidator.OptionalPublisher(evidence?.Publisher);
            string url = ActiveIngredientInputValidator.OptionalEvidenceUrl(evidence?.SourceUrl);
            DateTime? retrievedOn = ActiveIngredientInputValidator.OptionalRetrievedOn(evidence?.RetrievedOn);
            string actor = (createdBy ?? string.Empty).Trim();
            if (actor.Length == 0 || actor.Length > 200)
                throw new InvalidOperationException("هوية المستخدم المسجل مطلوبة لحفظ المصدر.");
            Guid evidenceId = Guid.NewGuid();
            using (SqlCommand command = new SqlCommand(@"INSERT dbo.ActiveIngredientEvidenceTb
(EvidenceId,ActiveIngredientId,SourceTitle,Publisher,SourceUrl,RetrievedOn,SourceFingerprint,IsActive,CreatedBy)
VALUES(@EvidenceId,@IngredientId,@Title,@Publisher,@Url,@RetrievedOn,@Fingerprint,1,@CreatedBy);", connection, transaction))
            {
                command.Parameters.Add("@EvidenceId", SqlDbType.UniqueIdentifier).Value = evidenceId;
                command.Parameters.Add("@IngredientId", SqlDbType.UniqueIdentifier).Value = ingredientId;
                command.Parameters.Add("@Title", SqlDbType.NVarChar, 300).Value = title;
                command.Parameters.Add("@Publisher", SqlDbType.NVarChar, 200).Value = publisher.Length == 0 ? (object)DBNull.Value : publisher;
                command.Parameters.Add("@Url", SqlDbType.NVarChar, 1000).Value = url.Length == 0 ? (object)DBNull.Value : url;
                command.Parameters.Add("@RetrievedOn", SqlDbType.Date).Value = retrievedOn.HasValue ? (object)retrievedOn.Value.Date : DBNull.Value;
                command.Parameters.Add("@Fingerprint", SqlDbType.Char, 64).Value = Fingerprint(title, publisher, url, retrievedOn);
                command.Parameters.Add("@CreatedBy", SqlDbType.NVarChar, 200).Value = actor;
                command.ExecuteNonQuery();
            }
            return evidenceId;
        }

        private static void EnsureIngredientExists(SqlConnection connection, SqlTransaction transaction, Guid ingredientId)
        {
            using (SqlCommand command = new SqlCommand(@"SELECT COUNT(1) FROM dbo.ActiveIngredientsTb WITH(UPDLOCK,HOLDLOCK)
WHERE ActiveIngredientId=@Id;", connection, transaction))
            {
                command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = ingredientId;
                if (Convert.ToInt32(command.ExecuteScalar()) != 1)
                    throw new InvalidOperationException("المادة الفعالة المحددة لم تعد موجودة.");
            }
        }

        private static string Fingerprint(string title, string publisher, string url, DateTime? retrievedOn)
        {
            string canonical = title.ToUpperInvariant() + "|" + publisher.ToUpperInvariant() + "|" + url.ToUpperInvariant() + "|" +
                (retrievedOn.HasValue ? retrievedOn.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : string.Empty);
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(canonical))).Replace("-", string.Empty);
        }

        private static void InsertAudit(SqlConnection connection, SqlTransaction transaction, OperationActor actor,
            string action, string entity, string details)
        {
            using (SqlCommand command = new SqlCommand(@"INSERT INTO dbo.AuditLogTb
(UserName,UserRole,ActionName,EntityName,Details)
VALUES(@UserName,@UserRole,@ActionName,@EntityName,@Details);", connection, transaction))
            {
                command.Parameters.Add("@UserName", SqlDbType.NVarChar, 200).Value = actor.UserName;
                command.Parameters.Add("@UserRole", SqlDbType.NVarChar, 50).Value = actor.UserRole;
                command.Parameters.Add("@ActionName", SqlDbType.NVarChar, 100).Value = action;
                command.Parameters.Add("@EntityName", SqlDbType.NVarChar, 100).Value = entity;
                command.Parameters.Add("@Details", SqlDbType.NVarChar, 1000).Value = details;
                command.ExecuteNonQuery();
            }
        }
    }
}

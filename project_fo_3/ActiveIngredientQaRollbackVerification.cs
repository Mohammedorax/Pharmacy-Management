using System;
using System.Data;
using System.Data.SqlClient;
using PharmacyManagement.Application;
using PharmacyManagement.Infrastructure.SqlServer;

namespace project_fo_3
{
    // فحص QA متراجع: يثبت مسار البحث نفسه من دون إدخال بيانات دوائية تجريبية دائمة.
    internal static class ActiveIngredientQaRollbackVerification
    {
        internal static void VerifySearchRoundTrip(string serverName, string databaseName)
        {
            EnsureQaLikeDatabase(databaseName);
            using (SqlConnection connection = new SqlConnection(Database.BuildSettingsConnectionString(serverName, databaseName, true)))
            {
                connection.Open();
                string medicineName;
                Guid ingredientId = Guid.NewGuid();
                string probe = "__QA_ACTIVE_INGREDIENT_PROBE_" + ingredientId.ToString("N").Substring(0, 12).ToUpperInvariant();
                Guid inactiveIngredientId = Guid.NewGuid();
                string inactiveProbe = "__QA_INACTIVE_INGREDIENT_PROBE_" + inactiveIngredientId.ToString("N").Substring(0, 12).ToUpperInvariant();

                using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    bool rolledBack = false;
                    try
                    {
                        medicineName = SelectProbeMedicine(connection, transaction);
                        InsertProbeIngredient(connection, transaction, ingredientId, probe, true);
                        Guid evidenceId = SqlActiveIngredientRepository.AddEvidenceInTransaction(
                            connection, transaction, ingredientId,
                            new ActiveIngredientEvidenceInput
                            {
                                SourceTitle = "QA rollback evidence " + probe,
                                Publisher = "QA verifier",
                                SourceUrl = "https://example.invalid/pharmacy-qa/" + ingredientId.ToString("N"),
                                RetrievedOn = DateTime.Today
                            },
                            "QA active ingredient verifier");
                        VerifyEvidenceLifecycle(connection, transaction, ingredientId, evidenceId);
                        InsertProbeMapping(connection, transaction, medicineName, ingredientId);
                        VerifyApplicationSearchQuery(connection, transaction, medicineName, probe, true);
                        InsertProbeIngredient(connection, transaction, inactiveIngredientId, inactiveProbe, false);
                        InsertProbeMapping(connection, transaction, medicineName, inactiveIngredientId);
                        VerifyApplicationSearchQuery(connection, transaction, medicineName, inactiveProbe, false);
                        transaction.Rollback();
                        rolledBack = true;
                    }
                    catch
                    {
                        if (!rolledBack)
                        {
                            try { transaction.Rollback(); }
                            catch { }
                        }
                        throw;
                    }
                }

                using (SqlCommand command = new SqlCommand("SELECT COUNT(1) FROM dbo.ActiveIngredientsTb WHERE ActiveIngredientId = @ActiveIngredientId;", connection))
                {
                    command.Parameters.Add("@ActiveIngredientId", SqlDbType.UniqueIdentifier).Value = ingredientId;
                    if (Convert.ToInt32(command.ExecuteScalar()) != 0)
                    {
                        throw new InvalidOperationException("فشل تراجع اختبار المادة الفعالة؛ أوقف الفحص ولا تستخدم قاعدة QA حتى مراجعة الأثر.");
                    }
                }
                using (SqlCommand command = new SqlCommand("SELECT COUNT(1) FROM dbo.ActiveIngredientsTb WHERE ActiveIngredientId = @ActiveIngredientId;", connection))
                {
                    command.Parameters.Add("@ActiveIngredientId", SqlDbType.UniqueIdentifier).Value = inactiveIngredientId;
                    if (Convert.ToInt32(command.ExecuteScalar()) != 0)
                    {
                        throw new InvalidOperationException("فشل تراجع اختبار المادة الفعالة غير النشطة؛ أوقف الفحص ولا تستخدم قاعدة QA حتى مراجعة الأثر.");
                    }
                }
                using (SqlCommand command = new SqlCommand("SELECT COUNT(1) FROM dbo.ActiveIngredientEvidenceTb WHERE ActiveIngredientId IN (@First,@Second);", connection))
                {
                    command.Parameters.Add("@First", SqlDbType.UniqueIdentifier).Value = ingredientId;
                    command.Parameters.Add("@Second", SqlDbType.UniqueIdentifier).Value = inactiveIngredientId;
                    if (Convert.ToInt32(command.ExecuteScalar()) != 0)
                        throw new InvalidOperationException("فشل تراجع مصادر المادة الفعالة؛ بقيت بيانات تجريبية في QA.");
                }
            }
        }

        private static void EnsureQaLikeDatabase(string databaseName)
        {
            if (string.IsNullOrWhiteSpace(databaseName) ||
                !(databaseName.EndsWith("_qa", StringComparison.OrdinalIgnoreCase) ||
                  databaseName.EndsWith("_test", StringComparison.OrdinalIgnoreCase) ||
                  databaseName.EndsWith("_sandbox", StringComparison.OrdinalIgnoreCase)))
            {
                throw new UnauthorizedAccessException("فحص البحث المتراجع بالمادة الفعالة مسموح لقاعدة QA/Test/Sandbox فقط.");
            }
        }

        private static string SelectProbeMedicine(SqlConnection connection, SqlTransaction transaction)
        {
            using (SqlCommand command = new SqlCommand(@"
SELECT TOP (1) MedName
FROM dbo.MedicineTb1 WITH (UPDLOCK, HOLDLOCK)
WHERE MedName IS NOT NULL AND LEN(LTRIM(RTRIM(MedName))) > 0
ORDER BY MedName;", connection, transaction))
            {
                command.CommandTimeout = 15;
                object result = command.ExecuteScalar();
                if (result == null || result == DBNull.Value)
                {
                    throw new InvalidOperationException("لا يوجد صنف صالح في QA لاختبار البحث بالمادة الفعالة.");
                }
                return Convert.ToString(result);
            }
        }

        private static void InsertProbeIngredient(SqlConnection connection, SqlTransaction transaction, Guid ingredientId, string probe, bool isActive)
        {
            using (SqlCommand command = new SqlCommand(@"
INSERT INTO dbo.ActiveIngredientsTb (ActiveIngredientId, ArabicName, LatinName, IsActive)
VALUES (@ActiveIngredientId, @ArabicName, @LatinName, @IsActive);", connection, transaction))
            {
                command.Parameters.Add("@ActiveIngredientId", SqlDbType.UniqueIdentifier).Value = ingredientId;
                command.Parameters.Add("@ArabicName", SqlDbType.NVarChar, 200).Value = probe;
                command.Parameters.Add("@LatinName", SqlDbType.NVarChar, 200).Value = probe;
                command.Parameters.Add("@IsActive", SqlDbType.Bit).Value = isActive;
                command.ExecuteNonQuery();
            }
        }

        private static void InsertProbeMapping(SqlConnection connection, SqlTransaction transaction, string medicineName, Guid ingredientId)
        {
            using (SqlCommand command = new SqlCommand(@"
INSERT INTO dbo.MedicineActiveIngredientsTb (MedName, ActiveIngredientId, StrengthValue, StrengthUnit, IsPrimary)
VALUES (@MedName, @ActiveIngredientId, NULL, NULL, 0);", connection, transaction))
            {
                command.Parameters.Add("@MedName", SqlDbType.NVarChar, 200).Value = medicineName;
                command.Parameters.Add("@ActiveIngredientId", SqlDbType.UniqueIdentifier).Value = ingredientId;
                command.ExecuteNonQuery();
            }
        }

        private static void VerifyEvidenceLifecycle(SqlConnection connection, SqlTransaction transaction, Guid ingredientId, Guid evidenceId)
        {
            using (SqlCommand command = new SqlCommand(@"
UPDATE dbo.ActiveIngredientEvidenceTb
SET IsActive=0,DeactivatedAtUtc=SYSUTCDATETIME(),DeactivatedBy=N'QA active ingredient verifier'
WHERE EvidenceId=@EvidenceId AND ActiveIngredientId=@IngredientId AND IsActive=1;
SELECT COUNT(1) FROM dbo.ActiveIngredientEvidenceTb
WHERE EvidenceId=@EvidenceId AND ActiveIngredientId=@IngredientId AND IsActive=0
  AND DeactivatedAtUtc IS NOT NULL AND DeactivatedBy=N'QA active ingredient verifier';", connection, transaction))
            {
                command.Parameters.Add("@EvidenceId", SqlDbType.UniqueIdentifier).Value = evidenceId;
                command.Parameters.Add("@IngredientId", SqlDbType.UniqueIdentifier).Value = ingredientId;
                if (Convert.ToInt32(command.ExecuteScalar()) != 1)
                    throw new InvalidOperationException("لم يحفظ مسار تعطيل مصدر المادة الفعالة أثر التدقيق المتوقع.");
            }

            SqlActiveIngredientRepository.AddEvidenceInTransaction(connection, transaction, ingredientId,
                new ActiveIngredientEvidenceInput
                {
                    SourceTitle = "QA current evidence " + ingredientId.ToString("N"),
                    Publisher = "QA verifier",
                    SourceUrl = "https://example.invalid/pharmacy-qa/current/" + ingredientId.ToString("N"),
                    RetrievedOn = DateTime.Today
                },
                "QA active ingredient verifier");

            using (SqlCommand command = new SqlCommand("SELECT COUNT(1) FROM dbo.ActiveIngredientEvidenceTb WHERE ActiveIngredientId=@IngredientId AND IsActive=1;", connection, transaction))
            {
                command.Parameters.Add("@IngredientId", SqlDbType.UniqueIdentifier).Value = ingredientId;
                if (Convert.ToInt32(command.ExecuteScalar()) != 1)
                    throw new InvalidOperationException("لم يبق مصدر نشط واحد متوقع للمادة التجريبية.");
            }
        }

        private static void VerifyApplicationSearchQuery(SqlConnection connection, SqlTransaction transaction, string medicineName, string probe, bool shouldFind)
        {
            using (SqlCommand command = new SqlCommand(MedicineLookupService.SearchQuery, connection, transaction))
            {
                command.CommandTimeout = 15;
                command.Parameters.Add("@Exact", SqlDbType.NVarChar, 200).Value = probe;
                command.Parameters.Add("@Contains", SqlDbType.NVarChar, 402).Value = "%" + MedicineLookupService.EscapeSearchPattern(probe) + "%";
                command.Parameters.Add("@Prefix", SqlDbType.NVarChar, 401).Value = MedicineLookupService.EscapeSearchPattern(probe) + "%";
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    bool found = false;
                    while (reader.Read())
                    {
                        if (string.Equals(Convert.ToString(reader["MedName"]), medicineName, StringComparison.Ordinal))
                        {
                            found = true;
                            break;
                        }
                    }
                    if (found != shouldFind)
                    {
                        throw new InvalidOperationException(shouldFind
                            ? "استعلام البحث الحالي لم يعثر على الصنف عبر رابط المادة الفعالة النشط التجريبي."
                            : "استعلام البحث الحالي أظهر صنفاً عبر مادة فعالة معطلة.");
                    }
                }
            }
        }
    }
}

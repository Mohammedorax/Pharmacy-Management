using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace project_fo_3
{
    // حالات المزايا المنظّمة. Production محجوب في الإصدار الحالي لأن الربط الحي لم يعتمد بعد.
    internal enum SaudiFeatureMode { Disabled, Simulator, Test, Production }

    internal sealed class SaudiCommercialFeature
    {
        public string FeatureKey { get; private set; }
        public string DisplayName { get; private set; }
        public string Description { get; private set; }
        public SaudiFeatureMode Mode { get; internal set; }

        public SaudiCommercialFeature(string featureKey, string displayName, string description)
        {
            FeatureKey = featureKey;
            DisplayName = displayName;
            Description = description;
            Mode = SaudiFeatureMode.Disabled;
        }
    }

    internal static class SaudiCommercialFeatureSettings
    {
        public const string CommercialSales = "CommercialSales";
        public const string EInvoice = "EInvoice";
        public const string Rsd = "Rsd";
        public const string Nphies = "Nphies";

        public static IReadOnlyList<SaudiCommercialFeature> GetAll()
        {
            using (SqlConnection connection = Database.CreateConnection())
            {
                connection.Open();
                return GetAll(connection, null);
            }
        }

        public static IReadOnlyList<SaudiCommercialFeature> GetAll(SqlConnection connection, SqlTransaction transaction)
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));
            List<SaudiCommercialFeature> features = CreateDefinitions();
            using (SqlCommand command = new SqlCommand(@"
SELECT FeatureKey, FeatureMode
FROM dbo.SaudiFeatureSettingsTb
ORDER BY FeatureKey;", connection, transaction))
            using (SqlDataReader reader = command.ExecuteReader())
            {
                Dictionary<string, SaudiCommercialFeature> map = new Dictionary<string, SaudiCommercialFeature>(StringComparer.OrdinalIgnoreCase);
                foreach (SaudiCommercialFeature feature in features) map[feature.FeatureKey] = feature;
                while (reader.Read())
                {
                    SaudiCommercialFeature feature;
                    if (!map.TryGetValue(reader.GetString(0), out feature)) continue;
                    SaudiFeatureMode mode;
                    if (!Enum.TryParse(reader.GetString(1), true, out mode))
                        throw new InvalidOperationException("حالة ميزة سعودية غير معروفة في قاعدة البيانات.");
                    feature.Mode = mode;
                }
            }
            return features.AsReadOnly();
        }

        public static void ChangeMode(string featureKey, SaudiFeatureMode newMode, string reason)
        {
            ChangeModes(new Dictionary<string, SaudiFeatureMode> { { featureKey, newMode } }, reason);
        }

        public static void ChangeModes(IReadOnlyDictionary<string, SaudiFeatureMode> changes, string reason)
        {
            if (!PermissionService.CanOpen(AppPermission.Settings))
                throw new UnauthorizedAccessException("تعديل المزايا المنظمة مسموح للمدير فقط.");
            if (string.IsNullOrWhiteSpace(reason))
                throw new InvalidOperationException("سبب الإضافة أو الإلغاء مطلوب للتدقيق.");
            if (reason.Trim().Length > 500)
                throw new InvalidOperationException("سبب التغيير طويل جداً.");

            IReadOnlyDictionary<string, SaudiFeatureMode> validated = ValidateBatch(changes);
            using (SqlConnection connection = Database.CreateConnection())
            {
                connection.Open();
                using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        ApplyChanges(connection, transaction, validated, reason.Trim());
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

        // Shared by the production commit and the QA transaction that rolls back before exit.
        internal static void ApplyChanges(SqlConnection connection, SqlTransaction transaction,
            IReadOnlyDictionary<string, SaudiFeatureMode> changes, string reason)
        {
            foreach (KeyValuePair<string, SaudiFeatureMode> change in changes)
            {
                SaudiFeatureMode oldMode = GetModeForUpdate(connection, transaction, change.Key);
                if (oldMode == change.Value) continue;
                using (SqlCommand update = new SqlCommand(@"
UPDATE dbo.SaudiFeatureSettingsTb
SET FeatureMode = @FeatureMode, UpdatedAtUtc = SYSUTCDATETIME(), UpdatedBy = @UpdatedBy
WHERE FeatureKey = @FeatureKey;", connection, transaction))
                {
                    update.Parameters.Add("@FeatureMode", SqlDbType.NVarChar, 20).Value = change.Value.ToString();
                    update.Parameters.Add("@UpdatedBy", SqlDbType.NVarChar, 200).Value = SessionContext.CurrentUser;
                    update.Parameters.Add("@FeatureKey", SqlDbType.NVarChar, 80).Value = change.Key;
                    if (update.ExecuteNonQuery() != 1) throw new InvalidOperationException("تعذر حفظ حالة الميزة.");
                }
                using (SqlCommand audit = new SqlCommand(@"
INSERT INTO dbo.SaudiFeatureSettingsAuditTb
(FeatureKey, PreviousMode, NewMode, ChangeReason, ChangedBy)
VALUES (@FeatureKey, @PreviousMode, @NewMode, @ChangeReason, @ChangedBy);", connection, transaction))
                {
                    audit.Parameters.Add("@FeatureKey", SqlDbType.NVarChar, 80).Value = change.Key;
                    audit.Parameters.Add("@PreviousMode", SqlDbType.NVarChar, 20).Value = oldMode.ToString();
                    audit.Parameters.Add("@NewMode", SqlDbType.NVarChar, 20).Value = change.Value.ToString();
                    audit.Parameters.Add("@ChangeReason", SqlDbType.NVarChar, 500).Value = reason;
                    audit.Parameters.Add("@ChangedBy", SqlDbType.NVarChar, 200).Value = SessionContext.CurrentUser;
                    audit.ExecuteNonQuery();
                }
            }
        }

        internal static IReadOnlyDictionary<string, SaudiFeatureMode> ValidateBatch(IReadOnlyDictionary<string, SaudiFeatureMode> changes)
        {
            if (changes == null || changes.Count == 0)
                throw new ArgumentException("اختر ميزة واحدة على الأقل للتغيير.", nameof(changes));
            var validated = new SortedDictionary<string, SaudiFeatureMode>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, SaudiFeatureMode> change in changes)
            {
                SaudiCommercialFeature definition = FindDefinition(change.Key);
                ValidateRequestedMode(definition.FeatureKey, change.Value);
                if (validated.ContainsKey(definition.FeatureKey))
                    throw new InvalidOperationException("تكررت الميزة السعودية في الطلب.");
                validated.Add(definition.FeatureKey, change.Value);
            }
            return validated;
        }

        public static void EnsureModeAtLeast(SqlConnection connection, SqlTransaction transaction, string featureKey, SaudiFeatureMode minimumMode)
        {
            SaudiFeatureMode current = GetModeForUpdate(connection, transaction, featureKey);
            if (current < minimumMode)
                throw new InvalidOperationException("الميزة " + FindDefinition(featureKey).DisplayName + " غير مفعلة بالمستوى المطلوب.");
        }

        public static void ValidateRequestedMode(string featureKey, SaudiFeatureMode mode)
        {
            FindDefinition(featureKey);
            if (mode == SaudiFeatureMode.Production)
                throw new InvalidOperationException("وضع الإنتاج محجوب في هذا الإصدار حتى تكتمل شهادات واعتمادات الجهة السعودية واختبارات قاعدة الاختبار.");
            if (mode != SaudiFeatureMode.Disabled && mode != SaudiFeatureMode.Simulator && mode != SaudiFeatureMode.Test)
                throw new InvalidOperationException("حالة الميزة السعودية غير صالحة.");
        }

        private static SaudiFeatureMode GetModeForUpdate(SqlConnection connection, SqlTransaction transaction, string featureKey)
        {
            FindDefinition(featureKey);
            using (SqlCommand command = new SqlCommand(@"
SELECT FeatureMode
FROM dbo.SaudiFeatureSettingsTb WITH (UPDLOCK, HOLDLOCK)
WHERE FeatureKey = @FeatureKey;", connection, transaction))
            {
                command.Parameters.Add("@FeatureKey", SqlDbType.NVarChar, 80).Value = featureKey;
                object value = command.ExecuteScalar();
                if (value == null || value == DBNull.Value)
                    throw new InvalidOperationException("إعداد الميزة السعودية غير موجود. ثبّت مخطط الاختبار أولاً.");
                SaudiFeatureMode mode;
                if (!Enum.TryParse(Convert.ToString(value), true, out mode))
                    throw new InvalidOperationException("حالة الميزة السعودية غير صالحة.");
                return mode;
            }
        }

        private static SaudiCommercialFeature FindDefinition(string featureKey)
        {
            foreach (SaudiCommercialFeature feature in CreateDefinitions())
            {
                if (string.Equals(feature.FeatureKey, featureKey, StringComparison.OrdinalIgnoreCase)) return feature;
            }
            throw new InvalidOperationException("ميزة سعودية غير معروفة.");
        }

        private static List<SaudiCommercialFeature> CreateDefinitions()
        {
            return new List<SaudiCommercialFeature>
            {
                new SaudiCommercialFeature(CommercialSales, "البيع التجاري السعودي", "فواتير ودفعات تجارية وفق الإعداد المعتمد."),
                new SaudiCommercialFeature(EInvoice, "فاتورة السعودية", "محاكاة ثم اختبار اعتماد؛ لا يوجد اتصال حي في هذا الإصدار."),
                new SaudiCommercialFeature(Rsd, "رصد الدوائي", "تجهيز محاكاة لتتبع الدواء، ولا يُربط حيًا قبل اعتماد SFDA."),
                new SaudiCommercialFeature(Nphies, "نفيس للتأمين", "محاكاة الأهلية والمطالبات، ولا يُربط حيًا قبل onboarding وشهادات الربط.")
            };
        }
    }
}

using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;

namespace project_fo_3
{
    internal static class TaxPolicyService
    {
        public static TaxPolicy GetEffectivePolicy(SqlConnection connection, SqlTransaction transaction)
        {
            using (TaxSettingsRecovery.Enter())
            {
                TaxSettingsRecovery.EnsureSettled();
                string configuredCountryCode = GetConfiguredCountryCode();
                bool isEnabled;
                decimal rate;
                ReadTaxSettings(connection, transaction, out isEnabled, out rate);
                return new TaxPolicy(configuredCountryCode, isEnabled, rate);
            }
        }
        public static decimal GetEffectiveTaxRate(SqlConnection connection, SqlTransaction transaction)
        {
            return GetEffectivePolicy(connection, transaction).EffectiveRate;
        }

        private static void ReadTaxSettings(SqlConnection connection, SqlTransaction transaction, out bool isEnabled, out decimal rate)
        {
            isEnabled = false;
            rate = 0m;

            using (SqlCommand command = new SqlCommand("SELECT TOP 1 IsEnabled, TaxRate FROM dbo.TaxSettingsTb ORDER BY TaxSettingId", connection, transaction))
            {
                using (SqlDataReader reader = command.ExecuteReader(CommandBehavior.SingleRow))
                {
                    if (!reader.Read())
                    {
                        return;
                    }

                    isEnabled = reader.IsDBNull(0) ? false : Convert.ToBoolean(reader.GetValue(0));
                    if (!reader.IsDBNull(1))
                    {
                        rate = Convert.ToDecimal(reader.GetValue(1));
                    }
                }
            }
        }

        public static string GetConfiguredCountryCode()
        {
            try
            {
                string settingValue = Properties.Setings.Default.TaxCountryCode;
                if (!string.IsNullOrWhiteSpace(settingValue))
                {
                    return settingValue;
                }
            }
            catch (ConfigurationErrorsException)
            {
                // الإعدادات القديمة قديمة/تالف بعضها; نستخدم إعداد التوافق.
            }
            catch
            {
                // لا نمنع التشغيل لخطأ إعدادات محلي.
            }

            try
            {
                string fallback = ConfigurationManager.AppSettings["TaxCountryCode"];
                if (!string.IsNullOrWhiteSpace(fallback))
                {
                    return fallback;
                }
            }
            catch
            {
                // fallback قد لا يكون متاحًا في بعض نسخ التشغيل.
            }

            return "YE";
        }
    }
}

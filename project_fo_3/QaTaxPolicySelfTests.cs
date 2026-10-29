using System;
using System.Data;
using System.Data.SqlClient;

namespace project_fo_3
{
    internal static class QaTaxPolicySelfTests
    {
        public static void Run(string backupPath)
        {
            var profile = Database.GetConnectionProfile();
            if (!RuntimeSettingsStore.IsBuildConnectionProfileForced || profile.DatabaseName != "PharmacyManagement_qa")
                throw new InvalidOperationException("This write test requires a pinned PharmacyManagement_qa package.");
            using (SqlConnection connection = Database.CreateConnection())
            {
                connection.Open();
                using (var command = new SqlCommand("SELECT DB_NAME()", connection))
                    Require((string)command.ExecuteScalar() == "PharmacyManagement_qa", "Actual database is not QA.");
                VerifyBackup(connection, backupPath);
            }
            using (TaxSettingsRecovery.Enter())
            {
                // Never take ownership of an earlier user's unfinished operation.
                TaxSettingsRecovery.EnsureSettled();
                DataRow original = AccountingService.GetTaxSettings();
                Require(original != null, "QA must have an existing tax settings row.");
                decimal originalRate = Convert.ToDecimal(original["TaxRate"]);
                bool originalEnabled = Convert.ToBoolean(original["IsEnabled"]);
                string originalCountry = Properties.Setings.Default.TaxCountryCode;
                string user = SessionContext.CurrentUser;
                string role = SessionContext.CurrentRole;
                bool authenticated = SessionContext.IsAuthenticated;
                SessionContext.Start("QA tax policy verification", "Manager");
                try
                {
                    AccountingService.SaveTaxSettings(15m, true, "SA");
                    Require(EffectiveRate() == 15m, "Enabled SA rate did not apply.");
                    AccountingService.SaveTaxSettings(15m, true, "YE");
                    Require(EffectiveRate() == 0m, "YE did not override enabled DB tax.");
                    AccountingService.SaveTaxSettings(15m, false, "OTHER");
                    Require(EffectiveRate() == 0m, "Disabled tax did not apply.");
                    AccountingService.SaveTaxSettings(15m, true, "YE");
                    StageInterruptedSave(false);
                    RequirePendingBlocked();
                    TaxSettingsRecovery.Recover();
                    Require(TaxPolicyService.GetConfiguredCountryCode() == "YE", "Rollback recovery changed the country.");
                    Require(Convert.ToDecimal(AccountingService.GetTaxSettings()["TaxRate"]) == 15m, "Rolled back SQL tax changed.");
                    Console.WriteLine("QA rolled-back save recovery passed.");
                    StageInterruptedSave(true);
                    RequirePendingBlocked();
                    TaxSettingsRecovery.Recover();
                    Require(TaxPolicyService.GetConfiguredCountryCode() == "SA" && EffectiveRate() == 7.5m,
                        "Committed SQL receipt did not recover the new country/rate.");
                    TaxSettingsRecovery.Recover();
                    Require(EffectiveRate() == 7.5m, "Recovery retry is not idempotent.");
                    Console.WriteLine("QA committed save recovery and retry passed.");
                }
                finally
                {
                    try
                    {
                        TaxSettingsRecovery.Recover();
                        AccountingService.SaveTaxSettings(originalRate, originalEnabled);
                        Properties.Setings.Default.TaxCountryCode = originalCountry;
                        Properties.Setings.Default.Save();
                    }
                    finally
                    {
                        if (authenticated) SessionContext.Start(user, role); else SessionContext.Clear();
                    }
                }
                DataRow restored = AccountingService.GetTaxSettings();
                Require(Convert.ToDecimal(restored["TaxRate"]) == originalRate
                    && Convert.ToBoolean(restored["IsEnabled"]) == originalEnabled
                    && Properties.Setings.Default.TaxCountryCode == originalCountry, "Original tax preferences were not restored.");
                TaxSettingsRecovery.EnsureSettled();
                Console.WriteLine("QA tax policy integration passed; original country/rate/enabled restored. Audit receipts retained.");
            }
        }

        private static decimal EffectiveRate()
        {
            using (SqlConnection connection = Database.CreateConnection())
            {
                connection.Open();
                return AccountingService.GetEffectiveTaxRate(connection, null);
            }
        }

        private static void StageInterruptedSave(bool commit)
        {
            using (SqlConnection connection = Database.CreateConnection())
            {
                connection.Open();
                string operation = TaxSettingsRecovery.Begin(connection, "YE", "SA");
                using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    AccountingService.WriteTaxSettings(connection, transaction, 7.5m, true);
                    TaxSettingsRecovery.WriteReceipt(connection, transaction, operation);
                    if (commit) transaction.Commit();
                    // Disposal rolls back the other case. Deliberately omit local completion.
                }
            }
        }

        private static void RequirePendingBlocked()
        {
            bool blocked = false;
            try { EffectiveRate(); }
            catch (InvalidOperationException) { blocked = true; }
            Require(blocked, "Unsettled tax policy was allowed into a new invoice.");
        }

        internal static void VerifyBackup(SqlConnection connection, string path)
        {
            using (var command = new SqlCommand("RESTORE HEADERONLY FROM DISK = @Path", connection))
            {
                command.CommandTimeout = 120;
                command.Parameters.Add("@Path", SqlDbType.NVarChar, 4000).Value = path;
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    Require(reader.Read() && Convert.ToString(reader["DatabaseName"]) == "PharmacyManagement_qa"
                        && Convert.ToInt32(reader["BackupType"]) == 1, "Backup is not a full QA database backup.");
                    Require(!reader.Read(), "Backup contains multiple sets; use a dedicated QA backup file.");
                }
                command.CommandText = "RESTORE VERIFYONLY FROM DISK = @Path WITH CHECKSUM";
                command.ExecuteNonQuery();
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
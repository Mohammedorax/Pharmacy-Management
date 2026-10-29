using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace project_fo_3
{
    internal static class QaSaudiBatchRollbackVerification
    {
        internal static void Run(string backupPath)
        {
            var profile = Database.GetConnectionProfile();
            if (!RuntimeSettingsStore.IsBuildConnectionProfileForced ||
                profile.DatabaseName != "PharmacyManagement_qa")
                throw new InvalidOperationException("Requires a pinned PharmacyManagement_qa package.");

            bool authenticated = SessionContext.IsAuthenticated;
            string previousUser = SessionContext.CurrentUser;
            string previousRole = SessionContext.CurrentRole;
            using (SqlConnection connection = Database.CreateConnection())
            {
                connection.Open();
                Require(connection.Database == "PharmacyManagement_qa", "Actual connection is not QA.");
                QaTaxPolicySelfTests.VerifyBackup(connection, backupPath);
                SessionContext.Start("QA Saudi batch rollback", "Manager");
                try
                {
                    const string first = SaudiCommercialFeatureSettings.EInvoice;
                    const string second = SaudiCommercialFeatureSettings.Rsd;
                    SaudiFeatureMode originalFirst = ReadMode(connection, null, first);
                    SaudiFeatureMode originalSecond = ReadMode(connection, null, second);
                    long originalAudits = ReadAuditCount(connection, null, first, second);
                    SaudiFeatureMode targetFirst = Toggle(originalFirst);
                    SaudiFeatureMode targetSecond = Toggle(originalSecond);
                    var valid = SaudiCommercialFeatureSettings.ValidateBatch(
                        new Dictionary<string, SaudiFeatureMode>
                        {
                            { first, targetFirst }, { second, targetSecond }
                        });

                    using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                    {
                        try
                        {
                            SaudiCommercialFeatureSettings.ApplyChanges(connection, transaction, valid, "QA rollback verification");
                            Require(ReadMode(connection, transaction, first) == targetFirst &&
                                ReadMode(connection, transaction, second) == targetSecond,
                                "Both requested modes were not updated inside the transaction.");
                            Require(ReadAuditCount(connection, transaction, first, second) == originalAudits + 2,
                                "Both audit rows were not written inside the transaction.");
                        }
                        finally { transaction.Rollback(); }
                    }
                    Require(ReadMode(connection, null, first) == originalFirst &&
                        ReadMode(connection, null, second) == originalSecond &&
                        ReadAuditCount(connection, null, first, second) == originalAudits,
                        "Success-path QA rollback left persistent feature or audit changes.");

                    var injectedFailure = new SortedDictionary<string, SaudiFeatureMode>(StringComparer.OrdinalIgnoreCase)
                    {
                        { first, targetFirst }, { "ZZZ_QA_INJECTED_FAILURE", SaudiFeatureMode.Test }
                    };
                    using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                    {
                        try
                        {
                            bool failed = false;
                            try
                            {
                                SaudiCommercialFeatureSettings.ApplyChanges(connection, transaction, injectedFailure,
                                    "QA injected rollback verification");
                            }
                            catch (InvalidOperationException) { failed = true; }
                            Require(failed, "The injected second-row failure was not raised.");
                            Require(ReadMode(connection, transaction, first) == targetFirst &&
                                ReadAuditCount(connection, transaction, first, second) == originalAudits + 1,
                                "The first-row write did not occur before the injected failure.");
                        }
                        finally { transaction.Rollback(); }
                    }
                    Require(ReadMode(connection, null, first) == originalFirst &&
                        ReadMode(connection, null, second) == originalSecond &&
                        ReadAuditCount(connection, null, first, second) == originalAudits,
                        "Failure-path QA rollback left persistent feature or audit changes.");
                    Console.WriteLine("QA Saudi batch success and injected-failure rollback passed; persistent rows unchanged.");
                }
                finally
                {
                    if (authenticated) SessionContext.Start(previousUser, previousRole);
                    else SessionContext.Clear();
                }
            }
        }

        private static SaudiFeatureMode Toggle(SaudiFeatureMode current)
        {
            return current == SaudiFeatureMode.Disabled ? SaudiFeatureMode.Simulator : SaudiFeatureMode.Disabled;
        }

        private static SaudiFeatureMode ReadMode(SqlConnection connection, SqlTransaction transaction, string key)
        {
            using (SqlCommand command = new SqlCommand(
                "SELECT FeatureMode FROM dbo.SaudiFeatureSettingsTb WHERE FeatureKey=@Key", connection, transaction))
            {
                command.Parameters.Add("@Key", SqlDbType.NVarChar, 80).Value = key;
                object value = command.ExecuteScalar();
                if (value == null || value == DBNull.Value)
                    throw new InvalidOperationException("QA feature row is missing: " + key);
                SaudiFeatureMode mode;
                if (!Enum.TryParse(Convert.ToString(value), true, out mode))
                    throw new InvalidOperationException("QA feature mode is invalid: " + key);
                return mode;
            }
        }

        private static long ReadAuditCount(SqlConnection connection, SqlTransaction transaction, string first, string second)
        {
            using (SqlCommand command = new SqlCommand(
                "SELECT COUNT_BIG(*) FROM dbo.SaudiFeatureSettingsAuditTb WHERE FeatureKey IN (@First,@Second)",
                connection, transaction))
            {
                command.Parameters.Add("@First", SqlDbType.NVarChar, 80).Value = first;
                command.Parameters.Add("@Second", SqlDbType.NVarChar, 80).Value = second;
                return Convert.ToInt64(command.ExecuteScalar());
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}

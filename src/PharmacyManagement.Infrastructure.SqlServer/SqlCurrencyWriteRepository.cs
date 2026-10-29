using System;
using System.Data;
using System.Data.SqlClient;
using PharmacyManagement.Application;

namespace PharmacyManagement.Infrastructure.SqlServer
{
    public sealed class SqlCurrencyWriteRepository : ICurrencyWriteRepository
    {
        private readonly Func<SqlConnection> connectionFactory;
        public SqlCurrencyWriteRepository(Func<SqlConnection> connectionFactory)
        {
            this.connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        }

        public void Save(CurrencyRecord currency, OperationActor actor)
        {
            if (currency == null) throw new ArgumentNullException(nameof(currency));
            Execute((connection, transaction) =>
            {
                if (currency.CurrencyId > 0) RequireExisting(connection, transaction, currency.CurrencyId, false);
                if (currency.IsSystemCurrency) ExecuteSql(connection, transaction, "UPDATE dbo.CurrencySettingsTb SET IsSystemCurrency = 0");
                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText = currency.CurrencyId > 0
                        ? @"UPDATE dbo.CurrencySettingsTb SET CountryNameAr=@CountryNameAr, CurrencyNameAr=@CurrencyNameAr,
CurrencyCode=@CurrencyCode, CurrencySymbol=@CurrencySymbol, IsSystemCurrency=@IsSystemCurrency WHERE CurrencyId=@CurrencyId"
                        : @"INSERT INTO dbo.CurrencySettingsTb (CountryNameAr, CurrencyNameAr, CurrencyCode, CurrencySymbol, IsSystemCurrency)
VALUES (@CountryNameAr, @CurrencyNameAr, @CurrencyCode, @CurrencySymbol, @IsSystemCurrency)";
                    command.Parameters.Add("@CurrencyId", SqlDbType.Int).Value = currency.CurrencyId;
                    command.Parameters.Add("@CountryNameAr", SqlDbType.NVarChar, 100).Value = currency.CountryNameAr;
                    command.Parameters.Add("@CurrencyNameAr", SqlDbType.NVarChar, 100).Value = currency.CurrencyNameAr;
                    command.Parameters.Add("@CurrencyCode", SqlDbType.NVarChar, 10).Value = currency.CurrencyCode;
                    command.Parameters.Add("@CurrencySymbol", SqlDbType.NVarChar, 20).Value =
                        string.IsNullOrWhiteSpace(currency.CurrencySymbol) ? (object)DBNull.Value : currency.CurrencySymbol;
                    command.Parameters.Add("@IsSystemCurrency", SqlDbType.Bit).Value = currency.IsSystemCurrency;
                    command.ExecuteNonQuery();
                }
                EnsureOneSystemCurrency(connection, transaction);
                InsertAudit(connection, transaction, actor, currency.CurrencyId > 0 ? "CurrencyUpdated" : "CurrencyCreated",
                    "تم حفظ العملة: " + currency.CurrencyCode + "؛ عملة النظام: " + currency.IsSystemCurrency + ".");
            });
        }

        public void Delete(int currencyId, OperationActor actor)
        {
            Execute((connection, transaction) =>
            {
                RequireExisting(connection, transaction, currencyId, true);
                using (var command = new SqlCommand("DELETE FROM dbo.CurrencySettingsTb WHERE CurrencyId=@Id", connection, transaction))
                {
                    command.Parameters.Add("@Id", SqlDbType.Int).Value = currencyId;
                    command.ExecuteNonQuery();
                }
                EnsureOneSystemCurrency(connection, transaction);
                InsertAudit(connection, transaction, actor, "CurrencyDeleted", "تم حذف العملة ذات المعرف: " + currencyId + ".");
            });
        }

        public void SetSystemCurrency(int currencyId, OperationActor actor)
        {
            Execute((connection, transaction) =>
            {
                RequireExisting(connection, transaction, currencyId, false);
                ExecuteSql(connection, transaction, "UPDATE dbo.CurrencySettingsTb SET IsSystemCurrency = 0");
                using (var command = new SqlCommand("UPDATE dbo.CurrencySettingsTb SET IsSystemCurrency=1 WHERE CurrencyId=@Id", connection, transaction))
                {
                    command.Parameters.Add("@Id", SqlDbType.Int).Value = currencyId;
                    command.ExecuteNonQuery();
                }
                InsertAudit(connection, transaction, actor, "SystemCurrencyChanged", "تم تعيين عملة النظام ذات المعرف: " + currencyId + ".");
            });
        }

        private void Execute(Action<SqlConnection, SqlTransaction> operation)
        {
            using (SqlConnection connection = connectionFactory())
            {
                connection.Open();
                using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        // Lock the small settings set in a consistent order before any mutation.
                        using (var command = new SqlCommand("SELECT CurrencyId FROM dbo.CurrencySettingsTb WITH (UPDLOCK,HOLDLOCK) ORDER BY CurrencyId", connection, transaction))
                        using (var reader = command.ExecuteReader()) { while (reader.Read()) { } }
                        operation(connection, transaction);
                        transaction.Commit();
                    }
                    catch
                    {
                        try { transaction.Rollback(); } catch { /* Preserve the original failure. */ }
                        throw;
                    }
                }
            }
        }

        private static void RequireExisting(SqlConnection connection, SqlTransaction transaction, int id, bool deleting)
        {
            using (var command = new SqlCommand("SELECT IsSystemCurrency FROM dbo.CurrencySettingsTb WHERE CurrencyId=@Id", connection, transaction))
            {
                command.Parameters.Add("@Id", SqlDbType.Int).Value = id;
                object flag = command.ExecuteScalar();
                if (flag == null) throw new InvalidOperationException("العملة المحددة لم تعد موجودة. حدّث القائمة ثم أعد المحاولة.");
                if (deleting && Convert.ToBoolean(flag))
                    throw new InvalidOperationException("لا يمكن حذف عملة النظام الحالية. عيّن عملة أخرى كعملة النظام أولاً.");
            }
        }

        private static void EnsureOneSystemCurrency(SqlConnection connection, SqlTransaction transaction)
        {
            ExecuteSql(connection, transaction, @"IF NOT EXISTS (SELECT 1 FROM dbo.CurrencySettingsTb WHERE IsSystemCurrency=1)
UPDATE dbo.CurrencySettingsTb SET IsSystemCurrency=1 WHERE CurrencyId=(SELECT TOP 1 CurrencyId FROM dbo.CurrencySettingsTb ORDER BY CurrencyId)");
        }

        private static void ExecuteSql(SqlConnection connection, SqlTransaction transaction, string sql)
        {
            using (var command = new SqlCommand(sql, connection, transaction)) command.ExecuteNonQuery();
        }

        private static void InsertAudit(SqlConnection connection, SqlTransaction transaction, OperationActor actor, string action, string details)
        {
            using (var command = new SqlCommand(@"INSERT INTO dbo.AuditLogTb (UserName,UserRole,ActionName,EntityName,Details)
VALUES (@UserName,@UserRole,@Action,N'CurrencySettings',@Details)", connection, transaction))
            {
                command.Parameters.Add("@UserName", SqlDbType.NVarChar, 200).Value = actor.UserName;
                command.Parameters.Add("@UserRole", SqlDbType.NVarChar, 50).Value = actor.UserRole;
                command.Parameters.Add("@Action", SqlDbType.NVarChar, 100).Value = action;
                command.Parameters.Add("@Details", SqlDbType.NVarChar, 1000).Value = details;
                command.ExecuteNonQuery();
            }
        }
    }
}

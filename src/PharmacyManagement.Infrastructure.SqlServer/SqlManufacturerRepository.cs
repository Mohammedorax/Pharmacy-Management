using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Application;

namespace PharmacyManagement.Infrastructure.SqlServer
{
    public sealed class SqlManufacturerRepository : IManufacturerRepository
    {
        private const string DirectorySql = @"SELECT CompId, CompName, CompPhone, CompAddress
FROM dbo.CompanyTb1
ORDER BY CompName, CompId;";
        private readonly Func<SqlConnection> connectionFactory;

        public SqlManufacturerRepository(Func<SqlConnection> connectionFactory)
        {
            this.connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        }

        public IReadOnlyList<ManufacturerRecord> GetAll()
        {
            var result = new List<ManufacturerRecord>();
            using (SqlConnection connection = connectionFactory())
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandText = DirectorySql;
                connection.Open();
                using (SqlDataReader reader = command.ExecuteReader())
                    while (reader.Read()) result.Add(Map(reader));
            }
            return result;
        }

        public async Task<IReadOnlyList<ManufacturerRecord>> GetAllAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var rows = new List<ManufacturerRecord>();
            using (SqlConnection connection = connectionFactory())
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandText = DirectorySql;
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                using (SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) rows.Add(Map(reader));
            }
            return rows.AsReadOnly();
        }

        public void Create(ManufacturerRecord manufacturer, OperationActor actor)
        {
            ExecuteMutation(manufacturer, actor, "ManufacturerCreated",
                "تمت إضافة شركة مصنعة: " + manufacturer.CompName + "، الرقم: " + manufacturer.CompId + ".",
                @"INSERT INTO dbo.CompanyTb1 (CompId, CompName, CompPhone, CompAddress)
VALUES (@CompId, @CompName, @CompPhone, @CompAddress);", false);
        }

        public bool Update(ManufacturerRecord manufacturer, OperationActor actor)
        {
            return ExecuteMutation(manufacturer, actor, "ManufacturerUpdated",
                "تم تعديل شركة مصنعة: " + manufacturer.CompName + "، الرقم: " + manufacturer.CompId + ".",
                @"UPDATE dbo.CompanyTb1
SET CompName=@CompName, CompPhone=@CompPhone, CompAddress=@CompAddress
WHERE CompId=@CompId;", true);
        }

        public bool Delete(string companyId, OperationActor actor)
        {
            var record = new ManufacturerRecord { CompId = companyId, CompName = string.Empty, CompPhone = string.Empty, CompAddress = string.Empty };
            return ExecuteMutation(record, actor, "ManufacturerDeleted",
                "تم حذف شركة مصنعة. الرقم: " + companyId + ".",
                "DELETE FROM dbo.CompanyTb1 WHERE CompId=@CompId;", true, bindAll: false);
        }

        private bool ExecuteMutation(ManufacturerRecord manufacturer, OperationActor actor, string action,
            string details, string sql, bool returnAffected, bool bindAll = true)
        {
            using (SqlConnection connection = connectionFactory())
            {
                connection.Open();
                using (SqlTransaction transaction = connection.BeginTransaction())
                {
                    try
                    {
                        int affected;
                        using (SqlCommand command = new SqlCommand(sql, connection, transaction))
                        {
                            Bind(command, manufacturer, bindAll);
                            affected = command.ExecuteNonQuery();
                        }
                        if (affected > 0) InsertAudit(connection, transaction, actor, action, details);
                        transaction.Commit();
                        return !returnAffected || affected > 0;
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        private static void Bind(SqlCommand command, ManufacturerRecord value, bool bindAll)
        {
            command.Parameters.Add("@CompId", SqlDbType.NVarChar, 50).Value = value.CompId;
            if (!bindAll) return;
            command.Parameters.Add("@CompName", SqlDbType.NVarChar, 200).Value = value.CompName;
            command.Parameters.Add("@CompPhone", SqlDbType.NVarChar, 50).Value = value.CompPhone;
            command.Parameters.Add("@CompAddress", SqlDbType.NVarChar, 300).Value = value.CompAddress;
        }

        private static void InsertAudit(SqlConnection connection, SqlTransaction transaction, OperationActor actor,
            string action, string details)
        {
            using (SqlCommand command = new SqlCommand(@"INSERT INTO dbo.AuditLogTb
(UserName, UserRole, ActionName, EntityName, Details)
VALUES (@UserName, @UserRole, @ActionName, N'Manufacturer', @Details);", connection, transaction))
            {
                command.Parameters.Add("@UserName", SqlDbType.NVarChar, 200).Value = actor.UserName;
                command.Parameters.Add("@UserRole", SqlDbType.NVarChar, 50).Value = actor.UserRole;
                command.Parameters.Add("@ActionName", SqlDbType.NVarChar, 100).Value = action;
                command.Parameters.Add("@Details", SqlDbType.NVarChar, 1000).Value = details;
                command.ExecuteNonQuery();
            }
        }

        private static ManufacturerRecord Map(SqlDataReader reader)
        {
            return new ManufacturerRecord
            {
                CompId = Convert.ToString(reader["CompId"]),
                CompName = Convert.ToString(reader["CompName"]),
                CompPhone = Convert.ToString(reader["CompPhone"]),
                CompAddress = Convert.ToString(reader["CompAddress"])
            };
        }
    }
}

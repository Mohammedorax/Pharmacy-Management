using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Application;

namespace PharmacyManagement.Infrastructure.SqlServer
{
    public sealed class SqlEmployeePermissionRepository : IEmployeePermissionRepository, IAsyncEmployeePermissionRepository
    {
        private readonly Func<SqlConnection> connectionFactory;

        public SqlEmployeePermissionRepository(Func<SqlConnection> connectionFactory)
        {
            this.connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        }

        public EmployeePermissionSnapshot GetByEmployeeId(string employeeId) => Get("EmpId", employeeId);
        public EmployeePermissionSnapshot GetByUserName(string userName) => Get("EmpName", userName);

        public void Replace(string employeeId, IReadOnlyDictionary<string, bool> overrides, OperationActor actor)
        {
            using (SqlConnection connection = connectionFactory())
            {
                connection.Open();
                using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        if (!TableExists(connection, transaction))
                            throw new InvalidOperationException("الصلاحيات التفصيلية غير مهيأة بعد. شغّل ترقية مخطط قاعدة البيانات أولًا.");
                        using (SqlCommand employee = new SqlCommand(@"SELECT EmpRole FROM dbo.EmployeeTb1 WITH(UPDLOCK,HOLDLOCK)
WHERE EmpId=@Id;", connection, transaction))
                        {
                            employee.Parameters.Add("@Id", SqlDbType.NVarChar, 50).Value = employeeId;
                            object role = employee.ExecuteScalar();
                            if (role == null || role == DBNull.Value) throw new InvalidOperationException("الموظف المحدد لم يعد موجودًا.");
                            if (IsManagerRole(Convert.ToString(role)))
                                throw new InvalidOperationException("حساب المدير يحتفظ بصلاحيات كاملة ولا يقبل تجاوزات تفصيلية.");
                        }
                        using (SqlCommand delete = new SqlCommand("DELETE dbo.EmployeeOperationPermissionsTb WHERE EmpId=@Id;", connection, transaction))
                        {
                            delete.Parameters.Add("@Id", SqlDbType.NVarChar, 50).Value = employeeId;
                            delete.ExecuteNonQuery();
                        }
                        foreach (KeyValuePair<string, bool> item in overrides)
                            using (SqlCommand insert = new SqlCommand(@"INSERT dbo.EmployeeOperationPermissionsTb
(EmpId,OperationName,IsAllowed,UpdatedBy) VALUES(@Id,@Operation,@Allowed,@By);", connection, transaction))
                            {
                                insert.Parameters.Add("@Id", SqlDbType.NVarChar, 50).Value = employeeId;
                                insert.Parameters.Add("@Operation", SqlDbType.NVarChar, 100).Value = item.Key;
                                insert.Parameters.Add("@Allowed", SqlDbType.Bit).Value = item.Value;
                                insert.Parameters.Add("@By", SqlDbType.NVarChar, 200).Value = actor.UserName;
                                insert.ExecuteNonQuery();
                            }
                        using (SqlCommand audit = new SqlCommand(@"INSERT dbo.AuditLogTb
(UserName,UserRole,ActionName,EntityName,Details)
VALUES(@User,@Role,N'EmployeeOperationPermissionsUpdated',N'Permissions',@Details);", connection, transaction))
                        {
                            audit.Parameters.Add("@User", SqlDbType.NVarChar, 200).Value = actor.UserName;
                            audit.Parameters.Add("@Role", SqlDbType.NVarChar, 50).Value = actor.UserRole;
                            audit.Parameters.Add("@Details", SqlDbType.NVarChar, 1000).Value =
                                "تم تحديث صلاحيات العمليات للموظف رقم " + employeeId + "، عدد التجاوزات: " + overrides.Count + ".";
                            audit.ExecuteNonQuery();
                        }
                        transaction.Commit();
                    }
                    catch { transaction.Rollback(); throw; }
                }
            }
        }

        private EmployeePermissionSnapshot Get(string column, string value)
        {
            var overrides = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            using (SqlConnection connection = connectionFactory())
            {
                connection.Open();
                bool available;
                using (SqlCommand schema = new SqlCommand("SELECT CASE WHEN OBJECT_ID(N'dbo.EmployeeOperationPermissionsTb',N'U') IS NULL THEN 0 ELSE 1 END;", connection))
                    available = Convert.ToInt32(schema.ExecuteScalar()) == 1;
                string condition = string.Equals(column, "EmpId", StringComparison.Ordinal) ? "e.EmpId" : "e.EmpName";
                using (SqlCommand command = new SqlCommand(@"SELECT TOP(1) ISNULL(NULLIF(e.EmpRole,N''),N'Cashier')
FROM dbo.EmployeeTb1 e WHERE " + condition + @"=@Value ORDER BY e.EmpId;", connection))
                {
                    command.Parameters.Add("@Value", SqlDbType.NVarChar, 200).Value = value;
                    object roleValue = command.ExecuteScalar();
                    if (roleValue == null || roleValue == DBNull.Value)
                        throw new InvalidOperationException("الموظف المحدد لم يعد موجودًا.");
                    string role = Convert.ToString(roleValue);
                    if (available)
                        using (SqlCommand permission = new SqlCommand(@"SELECT p.OperationName,p.IsAllowed
FROM dbo.EmployeeOperationPermissionsTb p INNER JOIN dbo.EmployeeTb1 e ON e.EmpId=p.EmpId
WHERE " + condition + "=@Value;", connection))
                        {
                            permission.Parameters.Add("@Value", SqlDbType.NVarChar, 200).Value = value;
                            using (SqlDataReader reader = permission.ExecuteReader())
                                while (reader.Read()) overrides[Convert.ToString(reader[0])] = Convert.ToBoolean(reader[1]);
                        }
                    return new EmployeePermissionSnapshot { IsAvailable = available, Role = role, Overrides = overrides };
                }
            }
        }

        public Task<EmployeePermissionSnapshot> GetByEmployeeIdAsync(string employeeId, CancellationToken cancellation) => GetAsync("EmpId", employeeId, cancellation);
        public Task<EmployeePermissionSnapshot> GetByUserNameAsync(string userName, CancellationToken cancellation) => GetAsync("EmpName", userName, cancellation);

        private async Task<EmployeePermissionSnapshot> GetAsync(string column, string value, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(value) || value.Length > 200) throw new ArgumentException("Invalid employee lookup.", nameof(value));
            try
            {
                var overrides = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
                using (var connection = connectionFactory())
                {
                    await connection.OpenAsync(cancellation).ConfigureAwait(false);
                    bool available;
                    using (var schema = new SqlCommand("SELECT CASE WHEN OBJECT_ID(N'dbo.EmployeeOperationPermissionsTb',N'U') IS NULL THEN 0 ELSE 1 END;", connection))
                        available = Convert.ToInt32(await schema.ExecuteScalarAsync(cancellation).ConfigureAwait(false)) == 1;
                    string condition = column == "EmpId" ? "e.EmpId" : "e.EmpName";
                    string role;
                    using (var command = new SqlCommand(@"SELECT TOP(1) ISNULL(NULLIF(e.EmpRole,N''),N'Cashier')
FROM dbo.EmployeeTb1 e WHERE " + condition + @"=@Value ORDER BY e.EmpId;", connection))
                    {
                        command.Parameters.Add("@Value", SqlDbType.NVarChar, 200).Value = value;
                        object roleValue = await command.ExecuteScalarAsync(cancellation).ConfigureAwait(false);
                        if (roleValue == null || roleValue == DBNull.Value) throw new InvalidOperationException("Employee no longer exists.");
                        role = Convert.ToString(roleValue);
                    }
                    if (available)
                        using (var command = new SqlCommand(@"SELECT p.OperationName,p.IsAllowed
FROM dbo.EmployeeOperationPermissionsTb p INNER JOIN dbo.EmployeeTb1 e ON e.EmpId=p.EmpId
WHERE " + condition + "=@Value;", connection))
                        {
                            command.Parameters.Add("@Value", SqlDbType.NVarChar, 200).Value = value;
                            using (var reader = await command.ExecuteReaderAsync(cancellation).ConfigureAwait(false))
                                while (await reader.ReadAsync(cancellation).ConfigureAwait(false))
                                    overrides[Convert.ToString(reader[0])] = Convert.ToBoolean(reader[1]);
                        }
                    cancellation.ThrowIfCancellationRequested();
                    return new EmployeePermissionSnapshot { IsAvailable = available, Role = role, Overrides = overrides };
                }
            }
            catch (SqlException exception) when (cancellation.IsCancellationRequested)
            { throw new OperationCanceledException("Employee permission read cancelled.", exception, cancellation); }
        }
        private static bool TableExists(SqlConnection connection, SqlTransaction transaction)
        {
            using (SqlCommand command = new SqlCommand("SELECT CASE WHEN OBJECT_ID(N'dbo.EmployeeOperationPermissionsTb',N'U') IS NULL THEN 0 ELSE 1 END;", connection, transaction))
                return Convert.ToInt32(command.ExecuteScalar()) == 1;
        }

        private static bool IsManagerRole(string role)
        {
            return string.Equals(role, "Manager", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(role, "Administrator", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(role, "مدير", StringComparison.Ordinal) ||
                string.Equals(role, "مسؤول", StringComparison.Ordinal) ||
                string.Equals(role, "ادمن", StringComparison.Ordinal);
        }
    }
}


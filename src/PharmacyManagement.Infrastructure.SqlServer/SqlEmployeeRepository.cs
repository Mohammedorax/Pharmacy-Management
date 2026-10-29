using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using PharmacyManagement.Application;

namespace PharmacyManagement.Infrastructure.SqlServer
{
    public sealed class SqlEmployeeRepository : IEmployeeRepository
    {
        private readonly Func<SqlConnection> connectionFactory;

        public SqlEmployeeRepository(Func<SqlConnection> connectionFactory)
        {
            this.connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        }

        public IReadOnlyList<EmployeeRecord> GetAll()
        {
            var result = new List<EmployeeRecord>();
            using (SqlConnection connection = connectionFactory())
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandText = @"SELECT EmpId,EmpName,EmpSal,EmpAge,EmpPhone,
ISNULL(NULLIF(EmpRole,N''),N'Cashier') AS EmpRole,
CASE WHEN EmpPassword LIKE N'PBKDF2$%' OR EmpPassword LIKE N'PBKDF2-SHA256$%'
 THEN N'محمية' ELSE N'تحتاج إعادة تعيين' END AS PasswordStatus
FROM dbo.EmployeeTb1 ORDER BY EmpName,EmpId;";
                connection.Open();
                using (SqlDataReader reader = command.ExecuteReader())
                    while (reader.Read())
                        result.Add(new EmployeeRecord
                        {
                            EmployeeId = Convert.ToString(reader["EmpId"]),
                            UserName = Convert.ToString(reader["EmpName"]),
                            Salary = Convert.ToInt32(reader["EmpSal"]),
                            Age = Convert.ToInt32(reader["EmpAge"]),
                            Phone = Convert.ToString(reader["EmpPhone"]),
                            Role = Convert.ToString(reader["EmpRole"]),
                            PasswordStatus = Convert.ToString(reader["PasswordStatus"])
                        });
            }
            return result;
        }

        public void Create(EmployeeMutation employee, OperationActor actor)
        {
            using (SqlConnection connection = connectionFactory())
            {
                connection.Open();
                using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        EnsureIdentityAvailable(connection, transaction, employee, null);
                        using (SqlCommand command = new SqlCommand(@"INSERT dbo.EmployeeTb1
(EmpId,EmpName,EmpSal,EmpAge,EmpPhone,EmpPassword,EmpRole)
VALUES(@Id,@Name,@Salary,@Age,@Phone,@Password,@Role);", connection, transaction))
                        {
                            AddParameters(command, employee);
                            command.ExecuteNonQuery();
                        }
                        InsertAudit(connection, transaction, actor, "EmployeeCreated",
                            "تمت إضافة موظف: " + employee.UserName + "، الرقم: " + employee.EmployeeId + ".");
                        transaction.Commit();
                    }
                    catch { transaction.Rollback(); throw; }
                }
            }
        }

        public string Update(EmployeeMutation employee, OperationActor actor)
        {
            using (SqlConnection connection = connectionFactory())
            {
                connection.Open();
                using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        string originalName;
                        string currentRole;
                        if (!TryLockEmployee(connection, transaction, employee.EmployeeId, out originalName, out currentRole))
                        {
                            transaction.Rollback();
                            return null;
                        }
                        EnsureIdentityAvailable(connection, transaction, employee, employee.EmployeeId);
                        if (IsManagerRole(currentRole) && !IsManagerRole(employee.Role) && !HasAnotherManager(connection, transaction, employee.EmployeeId))
                            throw new InvalidOperationException("لا يمكن خفض صلاحية المدير الوحيد. أنشئ مديرًا آخر أولًا.");
                        using (SqlCommand command = new SqlCommand(@"UPDATE dbo.EmployeeTb1 SET
EmpName=@Name,EmpSal=@Salary,EmpAge=@Age,EmpPhone=@Phone,
EmpPassword=CASE WHEN @UpdatePassword=1 THEN @Password ELSE EmpPassword END,EmpRole=@Role
WHERE EmpId=@Id;", connection, transaction))
                        {
                            AddParameters(command, employee);
                            command.ExecuteNonQuery();
                        }
                        InsertAudit(connection, transaction, actor, "EmployeeUpdated",
                            "تم تعديل بيانات موظف: " + employee.UserName + "، الرقم: " + employee.EmployeeId + ".");
                        transaction.Commit();
                        return originalName;
                    }
                    catch { transaction.Rollback(); throw; }
                }
            }
        }

        public string Delete(string employeeId, OperationActor actor)
        {
            using (SqlConnection connection = connectionFactory())
            {
                connection.Open();
                using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        string originalName;
                        string currentRole;
                        if (!TryLockEmployee(connection, transaction, employeeId, out originalName, out currentRole))
                        {
                            transaction.Rollback();
                            return null;
                        }
                        if (IsManagerRole(currentRole) && !HasAnotherManager(connection, transaction, employeeId))
                            throw new InvalidOperationException("لا يمكن حذف المدير الوحيد. أنشئ مديرًا آخر أولًا.");
                        using (SqlCommand command = new SqlCommand("DELETE dbo.EmployeeTb1 WHERE EmpId=@Id;", connection, transaction))
                        {
                            command.Parameters.Add("@Id", SqlDbType.NVarChar, 50).Value = employeeId;
                            command.ExecuteNonQuery();
                        }
                        InsertAudit(connection, transaction, actor, "EmployeeDeleted", "تم حذف موظف. الرقم: " + employeeId + ".");
                        transaction.Commit();
                        return originalName;
                    }
                    catch { transaction.Rollback(); throw; }
                }
            }
        }

        private static void AddParameters(SqlCommand command, EmployeeMutation employee)
        {
            command.Parameters.Add("@Id", SqlDbType.NVarChar, 50).Value = employee.EmployeeId;
            command.Parameters.Add("@Name", SqlDbType.NVarChar, 200).Value = employee.UserName;
            command.Parameters.Add("@Salary", SqlDbType.Int).Value = employee.Salary;
            command.Parameters.Add("@Age", SqlDbType.Int).Value = employee.Age;
            command.Parameters.Add("@Phone", SqlDbType.NVarChar, 50).Value = employee.Phone;
            command.Parameters.Add("@Role", SqlDbType.NVarChar, 50).Value = employee.Role;
            command.Parameters.Add("@UpdatePassword", SqlDbType.Bit).Value = employee.UpdatePassword;
            command.Parameters.Add("@Password", SqlDbType.NVarChar, 200).Value =
                employee.UpdatePassword ? (object)employee.ProtectedPassword : DBNull.Value;
        }

        private static void EnsureIdentityAvailable(SqlConnection connection, SqlTransaction transaction,
            EmployeeMutation employee, string excludedId)
        {
            using (SqlCommand command = new SqlCommand(@"SELECT COUNT(1) FROM dbo.EmployeeTb1 WITH(UPDLOCK,HOLDLOCK)
WHERE (EmpId=@Id OR LOWER(LTRIM(RTRIM(EmpName)))=LOWER(@Name))
AND (@ExcludedId IS NULL OR EmpId<>@ExcludedId);", connection, transaction))
            {
                command.Parameters.Add("@Id", SqlDbType.NVarChar, 50).Value = employee.EmployeeId;
                command.Parameters.Add("@Name", SqlDbType.NVarChar, 200).Value = employee.UserName;
                command.Parameters.Add("@ExcludedId", SqlDbType.NVarChar, 50).Value = string.IsNullOrEmpty(excludedId) ? (object)DBNull.Value : excludedId;
                if (Convert.ToInt32(command.ExecuteScalar()) > 0)
                    throw new InvalidOperationException("رقم الموظف أو اسم المستخدم مستخدم بالفعل.");
            }
        }

        private static bool TryLockEmployee(SqlConnection connection, SqlTransaction transaction, string id,
            out string name, out string role)
        {
            using (SqlCommand command = new SqlCommand(@"SELECT EmpName,EmpRole FROM dbo.EmployeeTb1 WITH(UPDLOCK,HOLDLOCK)
WHERE EmpId=@Id;", connection, transaction))
            {
                command.Parameters.Add("@Id", SqlDbType.NVarChar, 50).Value = id;
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (!reader.Read()) { name = null; role = null; return false; }
                    name = Convert.ToString(reader[0]); role = Convert.ToString(reader[1]); return true;
                }
            }
        }

        private static bool HasAnotherManager(SqlConnection connection, SqlTransaction transaction, string employeeId)
        {
            using (SqlCommand command = new SqlCommand(@"SELECT COUNT(1) FROM dbo.EmployeeTb1 WITH(UPDLOCK,HOLDLOCK)
WHERE EmpId<>@Id AND EmpRole IN(N'Manager',N'Admin',N'Administrator',N'مدير',N'مسؤول',N'ادمن');", connection, transaction))
            {
                command.Parameters.Add("@Id", SqlDbType.NVarChar, 50).Value = employeeId;
                return Convert.ToInt32(command.ExecuteScalar()) > 0;
            }
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

        private static void InsertAudit(SqlConnection connection, SqlTransaction transaction, OperationActor actor,
            string action, string details)
        {
            using (SqlCommand command = new SqlCommand(@"INSERT dbo.AuditLogTb
(UserName,UserRole,ActionName,EntityName,Details)
VALUES(@User,@Role,@Action,N'Employee',@Details);", connection, transaction))
            {
                command.Parameters.Add("@User", SqlDbType.NVarChar, 200).Value = actor.UserName;
                command.Parameters.Add("@Role", SqlDbType.NVarChar, 50).Value = actor.UserRole;
                command.Parameters.Add("@Action", SqlDbType.NVarChar, 100).Value = action;
                command.Parameters.Add("@Details", SqlDbType.NVarChar, 1000).Value = details;
                command.ExecuteNonQuery();
            }
        }
    }
}

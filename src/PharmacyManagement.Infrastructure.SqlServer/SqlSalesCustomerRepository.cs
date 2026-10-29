using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Application;

namespace PharmacyManagement.Infrastructure.SqlServer
{
    public sealed class SqlSalesCustomerRepository : ISalesCustomerRepository
    {
        private const string DirectorySql = @"SELECT CustomerId, CustomerName, PhoneNumber, CustomerAddress, TotalSpent
FROM dbo.CustomersBillsTb
WHERE CustomerId <> N'0'
ORDER BY CustomerName, CustomerId;";
        private readonly Func<SqlConnection> connectionFactory;

        public SqlSalesCustomerRepository(Func<SqlConnection> connectionFactory)
        {
            this.connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        }

        public IReadOnlyList<SalesCustomer> GetAll()
        {
            var rows = new List<SalesCustomer>();
            using (SqlConnection connection = connectionFactory())
            using (SqlCommand command = new SqlCommand(DirectorySql, connection))
            {
                command.CommandTimeout = 10;
                connection.Open();
                using (SqlDataReader reader = command.ExecuteReader())
                    while (reader.Read()) rows.Add(Map(reader));
            }
            return rows.AsReadOnly();
        }

        public async Task<IReadOnlyList<SalesCustomer>> GetAllAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var rows = new List<SalesCustomer>();
            using (SqlConnection connection = connectionFactory())
            using (SqlCommand command = new SqlCommand(DirectorySql, connection))
            {
                command.CommandTimeout = 10;
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                using (SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) rows.Add(Map(reader));
            }
            return rows.AsReadOnly();
        }

        public IReadOnlyList<string> GetCustomerNames() => ReadDistinctValues("CustomerName");
        public IReadOnlyList<string> GetPhoneNumbers() => ReadDistinctValues("PhoneNumber");

        public SalesCustomer FindById(string customerId)
        {
            return Find("CustomerId", customerId, 50);
        }

        public SalesCustomer FindByName(string customerName)
        {
            return Find("CustomerName", customerName, 200);
        }

        public SalesCustomer FindByPhone(string phoneNumber)
        {
            return Find("PhoneNumber", phoneNumber, 50);
        }

        public string Create(SalesCustomer customer, OperationActor actor)
        {
            using (SqlConnection connection = connectionFactory())
            {
                connection.Open();
                using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        EnsurePhoneAvailable(connection, transaction, customer.PhoneNumber, null);
                        string customerId;
                        using (SqlCommand command = new SqlCommand("SELECT CONVERT(NVARCHAR(50), NEXT VALUE FOR dbo.Seq_CustomerId);", connection, transaction))
                            customerId = Convert.ToString(command.ExecuteScalar());

                        using (SqlCommand command = new SqlCommand(@"INSERT INTO dbo.CustomersBillsTb
(CustomerId, CustomerName, PhoneNumber, CustomerAddress, TotalSpent, InvoiceDate, EmployeeName, InvoiceNotes)
VALUES (@CustomerId, @CustomerName, @PhoneNumber, @Address, 0, SYSUTCDATETIME(), N'إدارة العملاء', NULL);", connection, transaction))
                        {
                            Bind(command, customer, customerId);
                            command.ExecuteNonQuery();
                        }
                        InsertAudit(connection, transaction, actor, "CustomerCreated", customerId, customer.CustomerName);
                        transaction.Commit();
                        return customerId;
                    }
                    catch
                    {
                        try { transaction.Rollback(); } catch { }
                        throw;
                    }
                }
            }
        }

        public bool Update(SalesCustomer customer, OperationActor actor)
        {
            using (SqlConnection connection = connectionFactory())
            {
                connection.Open();
                using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        EnsurePhoneAvailable(connection, transaction, customer.PhoneNumber, customer.CustomerId);
                        int affected;
                        using (SqlCommand command = new SqlCommand(@"UPDATE dbo.CustomersBillsTb
SET CustomerName=@CustomerName, PhoneNumber=@PhoneNumber, CustomerAddress=@Address
WHERE CustomerId=@CustomerId AND CustomerId<>N'0';", connection, transaction))
                        {
                            Bind(command, customer, customer.CustomerId);
                            affected = command.ExecuteNonQuery();
                        }
                        if (affected > 0)
                            InsertAudit(connection, transaction, actor, "CustomerUpdated", customer.CustomerId, customer.CustomerName);
                        transaction.Commit();
                        return affected > 0;
                    }
                    catch
                    {
                        try { transaction.Rollback(); } catch { }
                        throw;
                    }
                }
            }
        }

        private static void EnsurePhoneAvailable(SqlConnection connection, SqlTransaction transaction, string phoneNumber, string exceptCustomerId)
        {
            using (SqlCommand command = new SqlCommand(@"SELECT TOP (1) CustomerId
FROM dbo.CustomersBillsTb WITH (UPDLOCK, HOLDLOCK)
WHERE PhoneNumber=@PhoneNumber AND CustomerId<>N'0'
  AND (@ExceptCustomerId IS NULL OR CustomerId<>@ExceptCustomerId);", connection, transaction))
            {
                command.Parameters.Add("@PhoneNumber", SqlDbType.NVarChar, 50).Value = phoneNumber.Trim();
                command.Parameters.Add("@ExceptCustomerId", SqlDbType.NVarChar, 50).Value = string.IsNullOrWhiteSpace(exceptCustomerId) ? (object)DBNull.Value : exceptCustomerId.Trim();
                if (command.ExecuteScalar() != null)
                    throw new InvalidOperationException("رقم الهاتف مسجل لعميل آخر. ابحث عنه أو استخدم رقمًا مختلفًا.");
            }
        }

        private IReadOnlyList<string> ReadDistinctValues(string columnName)
        {
            string sql = "SELECT DISTINCT " + columnName + " FROM dbo.CustomersBillsTb WHERE CustomerId <> N'0' AND NULLIF(LTRIM(RTRIM(" + columnName + ")), N'') IS NOT NULL ORDER BY " + columnName + ";";
            var values = new List<string>();
            using (SqlConnection connection = connectionFactory())
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.CommandTimeout = 10;
                connection.Open();
                using (SqlDataReader reader = command.ExecuteReader())
                    while (reader.Read())
                    {
                        string value = Convert.ToString(reader[0]).Trim();
                        if (value.Length > 0) values.Add(value);
                    }
            }
            return values.AsReadOnly();
        }

        private SalesCustomer Find(string columnName, string value, int parameterLength)
        {
            string sql = "SELECT TOP (1) CustomerId, CustomerName, PhoneNumber, CustomerAddress, TotalSpent FROM dbo.CustomersBillsTb WHERE " + columnName + " = @Value ORDER BY InvoiceDate DESC, CustomerId;";
            using (SqlConnection connection = connectionFactory())
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.CommandTimeout = 10;
                command.Parameters.Add("@Value", SqlDbType.NVarChar, parameterLength).Value = value;
                connection.Open();
                using (SqlDataReader reader = command.ExecuteReader(CommandBehavior.SingleRow))
                    return reader.Read() ? Map(reader) : null;
            }
        }

        private static void Bind(SqlCommand command, SalesCustomer value, string customerId)
        {
            command.Parameters.Add("@CustomerId", SqlDbType.NVarChar, 50).Value = customerId;
            command.Parameters.Add("@CustomerName", SqlDbType.NVarChar, 200).Value = value.CustomerName;
            command.Parameters.Add("@PhoneNumber", SqlDbType.NVarChar, 50).Value = value.PhoneNumber;
            command.Parameters.Add("@Address", SqlDbType.NVarChar, 300).Value = value.Address ?? string.Empty;
        }

        private static void InsertAudit(SqlConnection connection, SqlTransaction transaction, OperationActor actor,
            string action, string customerId, string customerName)
        {
            using (SqlCommand command = new SqlCommand(@"INSERT INTO dbo.AuditLogTb
(UserName, UserRole, ActionName, EntityName, Details)
VALUES (@UserName, @UserRole, @ActionName, N'Customer', @Details);", connection, transaction))
            {
                command.Parameters.Add("@UserName", SqlDbType.NVarChar, 200).Value = actor.UserName;
                command.Parameters.Add("@UserRole", SqlDbType.NVarChar, 50).Value = actor.UserRole;
                command.Parameters.Add("@ActionName", SqlDbType.NVarChar, 100).Value = action;
                command.Parameters.Add("@Details", SqlDbType.NVarChar, 1000).Value = "معرّف العميل: " + customerId + "، الاسم: " + customerName;
                command.ExecuteNonQuery();
            }
        }

        private static SalesCustomer Map(SqlDataReader reader)
        {
            return new SalesCustomer
            {
                CustomerId = Convert.ToString(reader["CustomerId"]),
                CustomerName = Convert.ToString(reader["CustomerName"]),
                PhoneNumber = Convert.ToString(reader["PhoneNumber"]),
                Address = reader.IsDBNull(reader.GetOrdinal("CustomerAddress")) ? string.Empty : Convert.ToString(reader["CustomerAddress"]),
                TotalSpent = reader.IsDBNull(reader.GetOrdinal("TotalSpent")) ? 0m : Convert.ToDecimal(reader["TotalSpent"])
            };
        }
    }
}

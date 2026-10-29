using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using PharmacyManagement.Application;
using PharmacyManagement.Infrastructure.SqlServer;

namespace PharmacyManagement.CoreTests
{
    internal static class EmployeeTests
    {
        internal static int Run()
        {
            int checks = 0;
            var repository = new FakeRepository();
            var protector = new FakeProtector();
            var service = new EmployeeService(repository, protector, selected => true);
            OperationActor actor = new OperationActor { UserName = " manager ", UserRole = " Manager " };

            service.Create(Valid("123"), actor);
            Check(repository.Last.EmployeeId == "E-1" && repository.Last.UserName == "أمين الصندوق" &&
                repository.Last.Phone == "777" && repository.Last.ProtectedPassword == "protected:123" &&
                repository.Last.UpdatePassword && repository.Actor.UserName == "manager",
                "Employee create normalization or password protection changed.", ref checks);

            EmployeeInput update = Valid(string.Empty);
            repository.MutationName = "old-user";
            Check(service.Update(update, actor) == "old-user" && !repository.Last.UpdatePassword &&
                repository.Last.ProtectedPassword == string.Empty,
                "Employee update unexpectedly replaced the password.", ref checks);
            Check(service.Delete(" E-1 ", actor) == "old-user" && repository.DeletedId == "E-1",
                "Employee delete normalization changed.", ref checks);

            Reject(() => service.Create(Valid("12"), actor), "Password shorter than three symbols was accepted.", ref checks);
            Reject(() => service.Create(Valid(" 123"), actor), "Password with outer whitespace was accepted.", ref checks);
            EmployeeInput invalid = Valid("123"); invalid.Age = 151;
            Reject(() => service.Create(invalid, actor), "Unreasonable employee age was accepted.", ref checks);
            invalid = Valid("123"); invalid.Role = "Owner";
            Reject(() => service.Create(invalid, actor), "Unsupported role was accepted.", ref checks);
            RejectUnauthorized(() => service.Delete("E-1", new OperationActor()), "Blank actor was accepted.", ref checks);

            var permissionRepository = new FakePermissionRepository();
            var permissionService = new EmployeePermissionService(permissionRepository, selected => true);
            permissionService.Replace(" E-1 ", new Dictionary<string, bool> { { "PostSalesInvoice", true } }, actor);
            Check(permissionRepository.EmployeeId == "E-1" && permissionRepository.Overrides["PostSalesInvoice"] &&
                permissionRepository.Actor.UserName == "manager",
                "Employee permission replacement normalization changed.", ref checks);
            permissionService.Replace("E-1", new Dictionary<string, bool>
                { { EmployeeScreenPermissionKeys.For("Sales"), false }, { "PostSalesInvoice", false } }, actor);
            Check(!permissionRepository.Overrides["Screen.Sales"] &&
                !permissionRepository.Overrides["PostSalesInvoice"],
                "Screen and operation overrides were not submitted together.", ref checks);
            Reject(() => permissionService.Replace("E-1", new Dictionary<string, bool>
                { { "Screen.Settings", true } }, actor),
                "Manager-only settings screen was delegated to a cashier.", ref checks);
            Reject(() => permissionService.Replace("E-1", new Dictionary<string, bool>
                { { "Screen.ManageEmployees", true } }, actor),
                "Employee administration screen was delegated to a cashier.", ref checks);
            RejectUnauthorized(() => permissionService.Replace("E-1", new Dictionary<string, bool>(),
                new OperationActor { UserName = "cashier", UserRole = "Cashier" }),
                "Non-manager permission replacement was accepted.", ref checks);
            RejectUnauthorized(() => service.Create(Valid("123"), new OperationActor { UserName = "cashier", UserRole = "Cashier" }),
                "Cashier employee creation was accepted.", ref checks);
            RejectUnauthorized(() => service.GetAll(new OperationActor { UserName="cashier",UserRole="Cashier" }),
                "Cashier read the complete administrative employee records.", ref checks);
            Check(service.GetAll(actor).Count==0, "Verified manager employee read changed.", ref checks);
            RejectUnauthorized(() => service.Update(Valid("123"), new OperationActor { UserName = "cashier", UserRole = "Cashier" }),
                "Cashier employee update was accepted.", ref checks);
            RejectUnauthorized(() => service.Delete("E-1", new OperationActor { UserName = "cashier", UserRole = "Cashier" }),
                "Cashier employee deletion was accepted.", ref checks);
            bool currentManager = false;
            Func<OperationActor, bool> bound = selected => currentManager && selected.UserName == "manager" && selected.UserRole == "Manager";
            var boundEmployees = new EmployeeService(repository, protector, bound);
            var boundPermissions = new EmployeePermissionService(permissionRepository, bound);
            EmployeeMutation original = repository.Last;
            string originalDeleted = repository.DeletedId;
            var originalOverrides = permissionRepository.Overrides;
            RejectUnauthorized(() => boundEmployees.GetAll(actor), "Unverified manager read employee records.", ref checks);
            RejectUnauthorized(() => boundEmployees.Create(null, actor), "Unverified manager reached employee creation.", ref checks);
            RejectUnauthorized(() => boundEmployees.Update(null, actor), "Unverified manager reached employee update.", ref checks);
            RejectUnauthorized(() => boundEmployees.Delete("E-1", actor), "Unverified manager reached employee deletion.", ref checks);
            RejectUnauthorized(() => boundPermissions.Replace("E-1", null, actor), "Unverified manager changed permissions.", ref checks);
            currentManager = true;
            var stale = new OperationActor { UserName = "old-manager", UserRole = "Manager" };
            RejectUnauthorized(() => boundEmployees.Delete("E-1", stale), "Different manager identity was accepted.", ref checks);
            RejectUnauthorized(() => boundPermissions.Replace("E-1", null, stale), "Different manager changed permissions.", ref checks);
            Check(ReferenceEquals(original, repository.Last) && originalDeleted == repository.DeletedId &&
                ReferenceEquals(originalOverrides, permissionRepository.Overrides), "Denied administration touched storage.", ref checks);
            boundEmployees.Create(Valid("123"), actor);
            boundPermissions.Replace("E-1", new Dictionary<string, bool> { { "PostSalesInvoice", false } }, actor);
            Check(repository.Actor.UserName == "manager" && permissionRepository.Actor.UserRole == "Manager" &&
                !permissionRepository.Overrides["PostSalesInvoice"], "Verified normalized manager failed administration.", ref checks);
            return checks;
        }

        internal static void VerifyQaReadOnly(string server, string database)
        {
            string name = (database ?? string.Empty).ToLowerInvariant();
            if (!(name.EndsWith("_qa") || name.EndsWith("_test") || name.EndsWith("_sandbox")))
                throw new InvalidOperationException("Explicit QA database suffix required before connection.");
            var builder = new SqlConnectionStringBuilder
            {
                DataSource = server, InitialCatalog = database, IntegratedSecurity = true, ConnectTimeout = 5,
                ApplicationName = "Pharmacy.CoreTests.Employee.ReadOnly"
            };
            var repository = new SqlEmployeeRepository(() => new SqlConnection(builder.ConnectionString));
            IReadOnlyList<EmployeeRecord> employees = repository.GetAll();
            using (var connection = new SqlConnection(builder.ConnectionString))
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT COUNT(1) FROM dbo.EmployeeTb1;";
                connection.Open();
                if (employees.Count != Convert.ToInt32(command.ExecuteScalar()))
                    throw new InvalidOperationException("Employee typed read count changed.");
            }
            foreach (EmployeeRecord employee in employees)
                if (string.IsNullOrWhiteSpace(employee.EmployeeId) || string.IsNullOrWhiteSpace(employee.UserName))
                    throw new InvalidOperationException("Employee typed read returned an unusable identity.");
            int overrides = 0;
            if (employees.Count > 0)
            {
                var permissions = new SqlEmployeePermissionRepository(() => new SqlConnection(builder.ConnectionString));
                EmployeePermissionSnapshot snapshot = permissions.GetByEmployeeId(employees[0].EmployeeId);
                if (!snapshot.IsAvailable) throw new InvalidOperationException("Employee permission schema is unavailable in QA.");
                overrides = snapshot.Overrides.Count;
            }
            Console.WriteLine("PASS: typed employee and permission reads. Database=" + database +
                "; Employees=" + employees.Count + "; Overrides=" + overrides + "; PersistentWrites=0.");
        }

        internal static void VerifyQaPermissionRoundtrip(string server, string database)
        {
            if (!string.Equals(database, "PharmacyManagement_qa", StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("Permission write test requires PharmacyManagement_qa exactly.");
            var builder = new SqlConnectionStringBuilder
            {
                DataSource = server, InitialCatalog = database, IntegratedSecurity = true, ConnectTimeout = 5,
                Encrypt = true, TrustServerCertificate = true,
                ApplicationName = "Pharmacy.CoreTests.PermissionRoundtrip"
            };
            string suffix = Guid.NewGuid().ToString("N");
            string employeeId = "QA-PERM-" + suffix;
            string userName = "qa-permission-" + suffix;
            string actorName = "qa-permission-test-" + suffix;
            int employeesBefore;
            int overridesBefore;
            using (var connection = new SqlConnection(builder.ConnectionString))
            {
                connection.Open();
                employeesBefore = Count(connection, "SELECT COUNT(*) FROM dbo.EmployeeTb1;");
                overridesBefore = Count(connection, "SELECT COUNT(*) FROM dbo.EmployeeOperationPermissionsTb;");
            }

            bool created = false;
            try
            {
                using (var connection = new SqlConnection(builder.ConnectionString))
                using (var command = new SqlCommand(@"INSERT dbo.EmployeeTb1
(EmpId,EmpName,EmpSal,EmpAge,EmpPhone,EmpPassword,EmpRole)
VALUES(@Id,@Name,0,30,N'',@Password,N'Cashier');", connection))
                {
                    command.Parameters.Add("@Id", SqlDbType.NVarChar, 50).Value = employeeId;
                    command.Parameters.Add("@Name", SqlDbType.NVarChar, 200).Value = userName;
                    command.Parameters.Add("@Password", SqlDbType.NVarChar, 200).Value = "DISABLED$" + suffix;
                    connection.Open();
                    if (command.ExecuteNonQuery() != 1) throw new InvalidOperationException("QA employee fixture was not created.");
                    created = true;
                }

                var repository = new SqlEmployeePermissionRepository(() => new SqlConnection(builder.ConnectionString));
                var service = new EmployeePermissionService(repository, selected => selected.UserName == actorName && selected.UserRole == "Manager");
                var actor = new OperationActor { UserName = actorName, UserRole = "Manager" };
                service.Replace(employeeId, new Dictionary<string, bool>
                {
                    { EmployeeScreenPermissionKeys.For("Sales"), false },
                    { EmployeeScreenPermissionKeys.For("Inventory"), true },
                    { "PostSalesInvoice", false }
                }, actor);
                EmployeePermissionSnapshot saved = service.GetByUserName(userName);
                if (!saved.IsAvailable || saved.Role != "Cashier" || saved.Overrides.Count != 3 ||
                    saved.Overrides["Screen.Sales"] || !saved.Overrides["Screen.Inventory"] ||
                    saved.Overrides["PostSalesInvoice"])
                    throw new InvalidOperationException("QA permission save was not visible through a new user-name read.");

                service.Replace(employeeId, new Dictionary<string, bool>
                {
                    { EmployeeScreenPermissionKeys.For("Sales"), true }
                }, actor);
                EmployeePermissionSnapshot replaced = service.GetByEmployeeId(employeeId);
                if (replaced.Overrides.Count != 1 || !replaced.Overrides["Screen.Sales"])
                    throw new InvalidOperationException("QA permission replacement kept stale overrides or lost the new grant.");
                try
                {
                    service.Replace(employeeId, new Dictionary<string, bool> { { "Screen.Settings", true } }, actor);
                    throw new InvalidOperationException("Sensitive settings screen was delegated in QA.");
                }
                catch (InvalidOperationException error) when (error.Message.Contains("لا تقبل تفويض")) { }
                if (service.GetByUserName(userName).Overrides.Count != 1)
                    throw new InvalidOperationException("Rejected QA permission request changed saved overrides.");
            }
            finally
            {
                if (created)
                {
                    using (var connection = new SqlConnection(builder.ConnectionString))
                    using (var command = new SqlCommand("DELETE dbo.EmployeeTb1 WHERE EmpId=@Id AND EmpName=@Name;", connection))
                    {
                        command.Parameters.Add("@Id", SqlDbType.NVarChar, 50).Value = employeeId;
                        command.Parameters.Add("@Name", SqlDbType.NVarChar, 200).Value = userName;
                        connection.Open();
                        if (command.ExecuteNonQuery() != 1)
                            throw new InvalidOperationException("QA permission fixture cleanup failed: " + employeeId);
                    }
                }
            }
            using (var connection = new SqlConnection(builder.ConnectionString))
            {
                connection.Open();
                if (Count(connection, "SELECT COUNT(*) FROM dbo.EmployeeTb1;") != employeesBefore ||
                    Count(connection, "SELECT COUNT(*) FROM dbo.EmployeeOperationPermissionsTb;") != overridesBefore)
                    throw new InvalidOperationException("QA employee or permission counts changed after cleanup.");
            }
            Console.WriteLine("PASS: QA screen and operation permissions save/read/replace/reject; fixture removed; audit events retained. Database=" + database);
        }

        private static int Count(SqlConnection connection, string sql)
        {
            using (var command = new SqlCommand(sql, connection)) return Convert.ToInt32(command.ExecuteScalar());
        }

        private static EmployeeInput Valid(string password)
        {
            return new EmployeeInput
            {
                EmployeeId = " E-1 ", UserName = " أمين الصندوق ", Salary = 50000, Age = 30,
                Phone = " 777 ", Role = "Cashier", NewPassword = password
            };
        }

        private static void Check(bool condition, string message, ref int checks)
        {
            checks++; if (!condition) throw new InvalidOperationException(message);
        }

        private static void Reject(Action action, string message, ref int checks)
        {
            try { action(); } catch (InvalidOperationException) { checks++; return; }
            throw new InvalidOperationException(message);
        }

        private static void RejectUnauthorized(Action action, string message, ref int checks)
        {
            try { action(); } catch (UnauthorizedAccessException) { checks++; return; }
            throw new InvalidOperationException(message);
        }

        private sealed class FakeProtector : IPasswordProtector
        {
            public string Protect(string password) => "protected:" + password;
        }

        private sealed class FakeRepository : IEmployeeRepository
        {
            internal EmployeeMutation Last;
            internal OperationActor Actor;
            internal string MutationName = "old-user";
            internal string DeletedId;
            public IReadOnlyList<EmployeeRecord> GetAll() => new EmployeeRecord[0];
            public void Create(EmployeeMutation employee, OperationActor actor) { Last = employee; Actor = actor; }
            public string Update(EmployeeMutation employee, OperationActor actor) { Last = employee; Actor = actor; return MutationName; }
            public string Delete(string employeeId, OperationActor actor) { DeletedId = employeeId; Actor = actor; return MutationName; }
        }

        private sealed class FakePermissionRepository : IEmployeePermissionRepository
        {
            internal string EmployeeId;
            internal IReadOnlyDictionary<string, bool> Overrides;
            internal OperationActor Actor;
            public EmployeePermissionSnapshot GetByEmployeeId(string employeeId) =>
                new EmployeePermissionSnapshot { IsAvailable = true, Role = "Cashier", Overrides = new Dictionary<string, bool>() };
            public EmployeePermissionSnapshot GetByUserName(string userName) =>
                new EmployeePermissionSnapshot { IsAvailable = true, Role = "Cashier", Overrides = new Dictionary<string, bool>() };
            public void Replace(string employeeId, IReadOnlyDictionary<string, bool> overrides, OperationActor actor)
            {
                EmployeeId = employeeId; Overrides = overrides; Actor = actor;
            }
        }
    }
}

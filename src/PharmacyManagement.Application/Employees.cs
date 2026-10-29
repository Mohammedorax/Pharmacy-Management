using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace PharmacyManagement.Application
{
    public sealed class EmployeeRecord
    {
        public string EmployeeId { get; set; }
        public string UserName { get; set; }
        public int Salary { get; set; }
        public int Age { get; set; }
        public string Phone { get; set; }
        public string Role { get; set; }
        public string PasswordStatus { get; set; }
    }

    public sealed class EmployeeInput
    {
        public string EmployeeId { get; set; }
        public string UserName { get; set; }
        public int Salary { get; set; }
        public int Age { get; set; }
        public string Phone { get; set; }
        public string Role { get; set; }
        [Browsable(false)] public string NewPassword { get; set; }
    }

    public sealed class EmployeeMutation
    {
        public string EmployeeId { get; set; }
        public string UserName { get; set; }
        public int Salary { get; set; }
        public int Age { get; set; }
        public string Phone { get; set; }
        public string Role { get; set; }
        public string ProtectedPassword { get; set; }
        public bool UpdatePassword { get; set; }
    }

    public interface IPasswordProtector
    {
        string Protect(string password);
    }

    public interface IEmployeeRepository
    {
        IReadOnlyList<EmployeeRecord> GetAll();
        void Create(EmployeeMutation employee, OperationActor actor);
        string Update(EmployeeMutation employee, OperationActor actor);
        string Delete(string employeeId, OperationActor actor);
    }

    public interface IEmployeeService
    {
        IReadOnlyList<EmployeeRecord> GetAll(OperationActor actor);
        void Create(EmployeeInput employee, OperationActor actor);
        string Update(EmployeeInput employee, OperationActor actor);
        string Delete(string employeeId, OperationActor actor);
    }

    public sealed class EmployeeService : IEmployeeService
    {
        public const int MinimumPasswordLength = 3;
        private readonly IEmployeeRepository repository;
        private readonly IPasswordProtector passwordProtector;
        private readonly Func<OperationActor, bool> canManage;

        public EmployeeService(IEmployeeRepository repository, IPasswordProtector passwordProtector, Func<OperationActor, bool> canManage)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
            this.passwordProtector = passwordProtector ?? throw new ArgumentNullException(nameof(passwordProtector));
            this.canManage = canManage ?? throw new ArgumentNullException(nameof(canManage));
        }

        public IReadOnlyList<EmployeeRecord> GetAll(OperationActor actor)
        {
            RequiredActor(actor);
            return repository.GetAll();
        }

        public void Create(EmployeeInput employee, OperationActor actor)
        {
            OperationActor manager = RequiredActor(actor);
            repository.Create(Normalize(employee, true), manager);
        }

        public string Update(EmployeeInput employee, OperationActor actor)
        {
            OperationActor manager = RequiredActor(actor);
            return repository.Update(Normalize(employee, false), manager);
        }

        public string Delete(string employeeId, OperationActor actor)
        {
            OperationActor manager = RequiredActor(actor);
            return repository.Delete(Required(employeeId, 50, "رقم الموظف"), manager);
        }

        private EmployeeMutation Normalize(EmployeeInput input, bool requirePassword)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            string password = input.NewPassword ?? string.Empty;
            bool updatePassword = password.Length > 0;
            if (requirePassword || updatePassword) ValidatePassword(password);
            if (input.Age <= 0 || input.Age > 150) throw new InvalidOperationException("عمر الموظف يجب أن يكون بين 1 و150 سنة.");
            if (input.Salary < 0) throw new InvalidOperationException("راتب الموظف لا يمكن أن يكون سالبًا.");
            string role = Required(input.Role, 50, "صلاحية الموظف");
            if (!IsSupportedRole(role)) throw new InvalidOperationException("صلاحية الموظف غير مدعومة.");
            return new EmployeeMutation
            {
                EmployeeId = Required(input.EmployeeId, 50, "رقم الموظف"),
                UserName = Required(input.UserName, 200, "اسم المستخدم"),
                Salary = input.Salary,
                Age = input.Age,
                Phone = Required(input.Phone, 50, "رقم الهاتف"),
                Role = role,
                UpdatePassword = requirePassword || updatePassword,
                ProtectedPassword = requirePassword || updatePassword ? passwordProtector.Protect(password) : string.Empty
            };
        }

        private static void ValidatePassword(string password)
        {
            if (password == null || password.Length == 0) throw new InvalidOperationException("كلمة المرور مطلوبة.");
            if (!string.Equals(password, password.Trim(), StringComparison.Ordinal))
                throw new InvalidOperationException("لا يمكن أن تبدأ كلمة المرور أو تنتهي بمسافة.");
            if (password.Length < MinimumPasswordLength)
                throw new InvalidOperationException("يجب ألا تقل كلمة المرور عن " + MinimumPasswordLength + " رموز.");
        }

        private static bool IsSupportedRole(string role)
        {
            return string.Equals(role, "Manager", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(role, "Cashier", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(role, "Inventory", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(role, "Accountant", StringComparison.OrdinalIgnoreCase);
        }

        private static string Required(string value, int maximum, string field)
        {
            string normalized = (value ?? string.Empty).Trim();
            if (normalized.Length == 0) throw new InvalidOperationException(field + " مطلوب.");
            if (normalized.Length > maximum) throw new InvalidOperationException(field + " يتجاوز الحد المسموح.");
            return normalized;
        }

        private OperationActor RequiredActor(OperationActor actor)
        {
            return EmployeeAdministrationAuthorization.RequireManager(actor, canManage,
                "إدارة الموظفين متاحة للمدير المسجل فقط.");
        }

    }
    internal static class EmployeeAdministrationAuthorization
    {
        internal static OperationActor RequireManager(OperationActor actor, Func<OperationActor, bool> canManage, string message)
        {
            string user = (actor?.UserName ?? string.Empty).Trim();
            string role = (actor?.UserRole ?? string.Empty).Trim();
            if (user.Length == 0 || user.Length > 200 || !IsManagerRole(role) ||
                !canManage(new OperationActor { UserName = user, UserRole = role }))
                throw new UnauthorizedAccessException(message);
            return new OperationActor { UserName = user, UserRole = role };
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

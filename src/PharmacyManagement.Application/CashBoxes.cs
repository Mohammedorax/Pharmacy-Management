using System;
using System.Collections.Generic;
using System.Linq;

namespace PharmacyManagement.Application
{
    // A definition and cashier assignment, not a balance. Journal allocation is a separate operation.
    public sealed class CashBoxRecord
    {
        public Guid CashBoxId { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public bool IsActive { get; set; }
        public string EmployeeId { get; set; }
        public string EmployeeName { get; set; }
    }

    public sealed class CashBoxInput
    {
        public Guid CashBoxId { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public bool IsActive { get; set; }
        public string EmployeeId { get; set; }
    }

    public interface ICashBoxRepository
    {
        IReadOnlyList<CashBoxRecord> GetAll();
        void SaveBatch(IReadOnlyList<CashBoxRecord> boxes, OperationActor actor);
    }

    public interface ICashBoxService
    {
        IReadOnlyList<CashBoxRecord> GetAll(OperationActor actor);
        IReadOnlyList<Guid> SaveBatch(IEnumerable<CashBoxInput> input, OperationActor actor);
    }

    public sealed class CashBoxService : ICashBoxService
    {
        private readonly ICashBoxRepository repository;
        private readonly Func<OperationActor, bool> canManage;

        public CashBoxService(ICashBoxRepository repository, Func<OperationActor, bool> canManage)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
            this.canManage = canManage ?? throw new ArgumentNullException(nameof(canManage));
        }

        public IReadOnlyList<CashBoxRecord> GetAll(OperationActor actor)
        {
            RequireManager(actor);
            return repository.GetAll();
        }

        public IReadOnlyList<Guid> SaveBatch(IEnumerable<CashBoxInput> input, OperationActor actor)
        {
            OperationActor manager = RequireManager(actor);
            if (input == null) throw new ArgumentNullException(nameof(input));
            CashBoxInput[] source = input.ToArray();
            if (source.Length == 0 || source.Length > 500)
                throw new InvalidOperationException("اختر من 1 إلى 500 صندوق في الدفعة.");

            var result = new List<CashBoxRecord>(source.Length);
            var ids = new HashSet<Guid>();
            var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var employees = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (CashBoxInput item in source)
            {
                if (item == null) throw new InvalidOperationException("يوجد صف صندوق فارغ.");
                string code = Required(item.Code, 30, "رمز الصندوق").ToUpperInvariant();
                if (code.Any(c => !((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '-' || c == '_')))
                    throw new InvalidOperationException("رمز الصندوق يقبل الحروف الإنجليزية والأرقام و - و _ فقط.");
                string name = Required(item.Name, 100, "اسم الصندوق");
                if (name.Any(char.IsControl)) throw new InvalidOperationException("اسم الصندوق يحتوي رمز تحكم غير صالح.");
                string employeeId = (item.EmployeeId ?? string.Empty).Trim();
                if (employeeId.Length > 50) throw new InvalidOperationException("رقم الكاشير يتجاوز الحد المسموح.");
                if (!item.IsActive && employeeId.Length != 0)
                    throw new InvalidOperationException("لا يمكن إسناد كاشير إلى صندوق غير نشط.");
                Guid id = item.CashBoxId == Guid.Empty ? Guid.NewGuid() : item.CashBoxId;
                if (!ids.Add(id) || !codes.Add(code) || (employeeId.Length != 0 && !employees.Add(employeeId)))
                    throw new InvalidOperationException("تتكرر هوية صندوق أو رمزه أو رقم كاشير في الدفعة.");
                result.Add(new CashBoxRecord
                {
                    CashBoxId = id, Code = code, Name = name, IsActive = item.IsActive,
                    EmployeeId = employeeId
                });
            }

            repository.SaveBatch(result, manager);
            return result.Select(item => item.CashBoxId).ToArray();
        }

        private OperationActor RequireManager(OperationActor actor)
        {
            string user = (actor?.UserName ?? string.Empty).Trim();
            string role = (actor?.UserRole ?? string.Empty).Trim();
            if (user.Length == 0 || user.Length > 200 || !IsManagerRole(role) || !canManage(actor))
                throw new UnauthorizedAccessException("إدارة صناديق النقد متاحة للمدير المسجل فقط.");
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

        private static string Required(string value, int maximum, string field)
        {
            string normalized = (value ?? string.Empty).Trim();
            if (normalized.Length == 0 || normalized.Length > maximum)
                throw new InvalidOperationException(field + " مطلوب وبحد أقصى " + maximum + " رمز.");
            return normalized;
        }
    }
}

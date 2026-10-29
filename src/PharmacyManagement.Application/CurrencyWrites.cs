using System;

namespace PharmacyManagement.Application
{
    public interface ICurrencyWriteRepository
    {
        void Save(CurrencyRecord currency, OperationActor actor);
        void Delete(int currencyId, OperationActor actor);
        void SetSystemCurrency(int currencyId, OperationActor actor);
    }

    public interface ICurrencyWriteService
    {
        void Save(CurrencyRecord currency, OperationActor actor);
        void Delete(int currencyId, OperationActor actor);
        void SetSystemCurrency(int currencyId, OperationActor actor);
    }

    public sealed class CurrencyWriteService : ICurrencyWriteService
    {
        private readonly ICurrencyWriteRepository repository;
        private readonly Func<OperationActor, bool> canManage;

        public CurrencyWriteService(ICurrencyWriteRepository repository, Func<OperationActor, bool> canManage)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
            this.canManage = canManage ?? throw new ArgumentNullException(nameof(canManage));
        }

        public void Save(CurrencyRecord currency, OperationActor actor)
        {
            OperationActor authorized = Authorize(actor);
            if (currency == null) throw new ArgumentNullException(nameof(currency));
            if (currency.CurrencyId < 0) throw new InvalidOperationException("معرف العملة غير صالح.");
            var normalized = new CurrencyRecord
            {
                CurrencyId = currency.CurrencyId,
                CountryNameAr = Required(currency.CountryNameAr, 100, "الدولة"),
                CurrencyNameAr = Required(currency.CurrencyNameAr, 100, "اسم العملة"),
                CurrencyCode = Required(currency.CurrencyCode, 10, "رمز العملة").ToUpperInvariant(),
                CurrencySymbol = Optional(currency.CurrencySymbol, 20, "اختصار العملة"),
                IsSystemCurrency = currency.IsSystemCurrency
            };
            repository.Save(normalized, authorized);
        }

        public void Delete(int currencyId, OperationActor actor)
        {
            OperationActor authorized = Authorize(actor);
            repository.Delete(ValidId(currencyId), authorized);
        }

        public void SetSystemCurrency(int currencyId, OperationActor actor)
        {
            OperationActor authorized = Authorize(actor);
            repository.SetSystemCurrency(ValidId(currencyId), authorized);
        }

        private OperationActor Authorize(OperationActor actor)
        {
            if (actor == null || string.IsNullOrWhiteSpace(actor.UserName) || string.IsNullOrWhiteSpace(actor.UserRole) || !canManage(actor))
                throw new UnauthorizedAccessException("لا تملك صلاحية تعديل العملات أو تغيير عملة النظام.");
            return new OperationActor
            {
                UserName = Required(actor.UserName, 200, "اسم المستخدم"),
                UserRole = Required(actor.UserRole, 50, "دور المستخدم")
            };
        }

        private static int ValidId(int id)
        {
            if (id <= 0) throw new InvalidOperationException("حدد عملة صالحة أولاً.");
            return id;
        }

        private static string Required(string text, int max, string field)
        {
            string value = Optional(text, max, field);
            if (value.Length == 0) throw new InvalidOperationException(field + " مطلوب.");
            return value;
        }

        private static string Optional(string text, int max, string field)
        {
            string value = (text ?? string.Empty).Trim();
            if (value.Length > max) throw new InvalidOperationException(field + " يتجاوز الحد المسموح (" + max + ").");
            return value;
        }
    }
}

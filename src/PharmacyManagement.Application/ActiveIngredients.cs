using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;

namespace PharmacyManagement.Application
{
    public sealed class ActiveIngredientSummary
    {
        [Browsable(false)] public Guid ActiveIngredientId { get; set; }
        public string ArabicName { get; set; }
        public string LatinName { get; set; }
        [Browsable(false)] public bool IsActive { get; set; }
        public string StatusText => IsActive ? "نشطة" : "معطلة";
        public int LinkedMedicineCount { get; set; }
        public int ActiveEvidenceCount { get; set; }
    }

    public sealed class ActiveIngredientEvidenceRecord
    {
        [Browsable(false)] public Guid EvidenceId { get; set; }
        public string SourceTitle { get; set; }
        public string Publisher { get; set; }
        public string SourceUrl { get; set; }
        public DateTime? RetrievedOn { get; set; }
        [Browsable(false)] public bool IsActive { get; set; }
        public string StatusText => IsActive ? "نشط" : "معطل";
        public string CreatedBy { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public string DeactivatedBy { get; set; }
    }

    public sealed class ActiveIngredientMappingRecord
    {
        public string MedicineName { get; set; }
        public decimal? StrengthValue { get; set; }
        public string StrengthUnit { get; set; }
        [Browsable(false)] public bool IsPrimary { get; set; }
        public string PrimaryText => IsPrimary ? "نعم" : "لا";
        public DateTime CreatedAtUtc { get; set; }
    }

    public sealed class ActiveIngredientDefinition
    {
        public string ArabicName { get; set; }
        public string LatinName { get; set; }
    }

    public sealed class ActiveIngredientEvidenceInput
    {
        public string SourceTitle { get; set; }
        public string Publisher { get; set; }
        public string SourceUrl { get; set; }
        public DateTime? RetrievedOn { get; set; }
    }

    public sealed class ActiveIngredientMappingInput
    {
        public Guid ActiveIngredientId { get; set; }
        public string MedicineName { get; set; }
        public decimal? StrengthValue { get; set; }
        public string StrengthUnit { get; set; }
        public bool IsPrimary { get; set; }
    }

    public interface IActiveIngredientRepository
    {
        IReadOnlyList<ActiveIngredientSummary> GetIngredients();
        IReadOnlyList<string> GetMedicineNames();
        IReadOnlyList<ActiveIngredientEvidenceRecord> GetEvidence(Guid ingredientId);
        IReadOnlyList<ActiveIngredientMappingRecord> GetMappings(Guid ingredientId);
        Guid CreateWithEvidence(ActiveIngredientDefinition definition, ActiveIngredientEvidenceInput evidence, OperationActor actor);
        Guid AddEvidence(Guid ingredientId, ActiveIngredientEvidenceInput evidence, OperationActor actor);
        void DeactivateEvidence(Guid evidenceId, OperationActor actor);
        void ToggleIngredient(Guid ingredientId, OperationActor actor);
        void SaveMapping(ActiveIngredientMappingInput mapping, OperationActor actor);
    }

    public interface IActiveIngredientService
    {
        IReadOnlyList<ActiveIngredientSummary> GetIngredients();
        IReadOnlyList<string> GetMedicineNames();
        IReadOnlyList<ActiveIngredientEvidenceRecord> GetEvidence(Guid ingredientId);
        IReadOnlyList<ActiveIngredientMappingRecord> GetMappings(Guid ingredientId);
        Guid CreateIngredientWithEvidence(string arabicName, string latinName, ActiveIngredientEvidenceInput evidence, OperationActor actor);
        Guid AddEvidence(Guid ingredientId, ActiveIngredientEvidenceInput evidence, OperationActor actor);
        void DeactivateEvidence(Guid evidenceId, OperationActor actor);
        void ToggleIngredient(Guid ingredientId, OperationActor actor);
        void SaveMapping(Guid ingredientId, string medicineName, string strengthText, string unitText, bool isPrimary, OperationActor actor);
    }

    public sealed class ActiveIngredientApplicationService : IActiveIngredientService
    {
        private readonly IActiveIngredientRepository repository;

        public ActiveIngredientApplicationService(IActiveIngredientRepository repository)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public IReadOnlyList<ActiveIngredientSummary> GetIngredients() => repository.GetIngredients();
        public IReadOnlyList<string> GetMedicineNames() => repository.GetMedicineNames();
        public IReadOnlyList<ActiveIngredientEvidenceRecord> GetEvidence(Guid ingredientId) =>
            repository.GetEvidence(RequiredId(ingredientId, "المادة الفعالة"));
        public IReadOnlyList<ActiveIngredientMappingRecord> GetMappings(Guid ingredientId) =>
            repository.GetMappings(RequiredId(ingredientId, "المادة الفعالة"));

        public Guid CreateIngredientWithEvidence(string arabicName, string latinName,
            ActiveIngredientEvidenceInput evidence, OperationActor actor)
        {
            var definition = new ActiveIngredientDefinition
            {
                ArabicName = ActiveIngredientInputValidator.RequiredArabicName(arabicName),
                LatinName = ActiveIngredientInputValidator.OptionalLatinName(latinName)
            };
            return repository.CreateWithEvidence(definition, Normalize(evidence), Authenticated(actor));
        }

        public Guid AddEvidence(Guid ingredientId, ActiveIngredientEvidenceInput evidence, OperationActor actor)
        {
            return repository.AddEvidence(RequiredId(ingredientId, "المادة الفعالة"), Normalize(evidence), Authenticated(actor));
        }

        public void DeactivateEvidence(Guid evidenceId, OperationActor actor)
        {
            repository.DeactivateEvidence(RequiredId(evidenceId, "المصدر"), Authenticated(actor));
        }

        public void ToggleIngredient(Guid ingredientId, OperationActor actor)
        {
            repository.ToggleIngredient(RequiredId(ingredientId, "المادة الفعالة"), Authenticated(actor));
        }

        public void SaveMapping(Guid ingredientId, string medicineName, string strengthText, string unitText,
            bool isPrimary, OperationActor actor)
        {
            decimal? strength = ActiveIngredientInputValidator.OptionalStrength(strengthText, unitText);
            repository.SaveMapping(new ActiveIngredientMappingInput
            {
                ActiveIngredientId = RequiredId(ingredientId, "المادة الفعالة"),
                MedicineName = ActiveIngredientInputValidator.RequiredMedicineName(medicineName),
                StrengthValue = strength,
                StrengthUnit = strength.HasValue ? ActiveIngredientInputValidator.RequiredStrengthUnit(unitText) : string.Empty,
                IsPrimary = isPrimary
            }, Authenticated(actor));
        }

        private static ActiveIngredientEvidenceInput Normalize(ActiveIngredientEvidenceInput evidence)
        {
            return new ActiveIngredientEvidenceInput
            {
                SourceTitle = ActiveIngredientInputValidator.RequiredEvidenceTitle(evidence?.SourceTitle),
                Publisher = ActiveIngredientInputValidator.OptionalPublisher(evidence?.Publisher),
                SourceUrl = ActiveIngredientInputValidator.OptionalEvidenceUrl(evidence?.SourceUrl),
                RetrievedOn = ActiveIngredientInputValidator.OptionalRetrievedOn(evidence?.RetrievedOn)
            };
        }

        private static OperationActor Authenticated(OperationActor actor)
        {
            string user = (actor?.UserName ?? string.Empty).Trim();
            if (user.Length == 0 || user.Length > 200)
                throw new UnauthorizedAccessException("هوية المستخدم المسجل مطلوبة لإدارة المواد الفعالة.");
            string role = (actor?.UserRole ?? string.Empty).Trim();
            if (role.Length > 50) throw new UnauthorizedAccessException("صلاحية المستخدم غير صالحة.");
            return new OperationActor { UserName = user, UserRole = role.Length == 0 ? "Unknown" : role };
        }

        private static Guid RequiredId(Guid id, string field)
        {
            if (id == Guid.Empty) throw new InvalidOperationException("اختر " + field + " صالحًا.");
            return id;
        }
    }

    public static class ActiveIngredientInputValidator
    {
        public static string RequiredArabicName(string value) => Required(value, 200, "الاسم العربي للمادة الفعالة");
        public static string OptionalLatinName(string value) => Optional(value, 200, "الاسم اللاتيني");
        public static string RequiredEvidenceTitle(string value) => Required(value, 300, "اسم المرجع أو عنوان المصدر");
        public static string OptionalPublisher(string value) => Optional(value, 200, "اسم الجهة الناشرة");
        public static string RequiredMedicineName(string value) => Required(value, 200, "الصنف");
        public static string RequiredStrengthUnit(string value) => Required(value, 30, "وحدة التركيز");

        public static decimal? OptionalStrength(string value, string unit)
        {
            string text = (value ?? string.Empty).Trim();
            string normalizedUnit = (unit ?? string.Empty).Trim();
            if (text.Length == 0 && normalizedUnit.Length == 0) return null;
            decimal strength;
            if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out strength) ||
                strength <= 0m || strength > 999999999999999.999m)
                throw new InvalidOperationException("التركيز يجب أن يكون رقمًا موجبًا صالحًا.");
            if (decimal.Round(strength, 3) != strength)
                throw new InvalidOperationException("التركيز يدعم ثلاث منازل عشرية كحد أقصى؛ لن تُقرّب القيمة الطبية تلقائيًا.");
            RequiredStrengthUnit(normalizedUnit);
            return strength;
        }

        public static string OptionalEvidenceUrl(string value)
        {
            string normalized = Optional(value, 1000, "رابط المصدر");
            if (normalized.Length == 0) return string.Empty;
            Uri uri;
            if (!Uri.TryCreate(normalized, UriKind.Absolute, out uri) ||
                !(string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
                  string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("رابط المصدر يجب أن يكون رابط HTTP أو HTTPS كاملًا.");
            return uri.AbsoluteUri;
        }

        public static DateTime? OptionalRetrievedOn(DateTime? value)
        {
            if (!value.HasValue) return null;
            if (value.Value.Date > DateTime.Today)
                throw new InvalidOperationException("تاريخ الاطلاع على المصدر لا يمكن أن يكون في المستقبل.");
            return value.Value.Date;
        }

        private static string Required(string value, int maximumLength, string fieldName)
        {
            string normalized = Optional(value, maximumLength, fieldName);
            if (normalized.Length == 0) throw new InvalidOperationException(fieldName + " مطلوب.");
            return normalized;
        }

        private static string Optional(string value, int maximumLength, string fieldName)
        {
            string normalized = (value ?? string.Empty).Trim();
            if (normalized.Length > maximumLength)
                throw new InvalidOperationException(fieldName + " يتجاوز الحد المسموح (" + maximumLength + ").");
            return normalized;
        }
    }
}

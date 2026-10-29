using System;
using System.Collections.Generic;
using System.Linq;

namespace project_fo_3
{
    internal sealed class PatientRecord
    {
        public string PatientId { get; set; }
        public string FullName { get; set; }
        public string Mobile { get; set; }
        public IReadOnlyList<string> Allergies { get; set; }

        public PatientRecord()
        {
            PatientId = string.Empty;
            FullName = string.Empty;
            Mobile = string.Empty;
            Allergies = new List<string>();
        }
    }

    internal sealed class PrescriptionRecord
    {
        public string PrescriptionId { get; set; }
        public string PatientId { get; set; }
        public string PrescriberName { get; set; }
        public DateTime IssuedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public IReadOnlyList<PrescriptionItem> Items { get; set; }

        public PrescriptionRecord()
        {
            PrescriptionId = string.Empty;
            PatientId = string.Empty;
            PrescriberName = string.Empty;
            Items = new List<PrescriptionItem>();
        }
    }

    internal sealed class PrescriptionItem
    {
        public string DrugCode { get; set; }
        public string DrugName { get; set; }
        public decimal PrescribedQuantity { get; set; }
        public decimal DispensedQuantity { get; set; }
    }

    internal sealed class GatewayResult
    {
        public bool IsAccepted { get; set; }
        public bool IsRetryable { get; set; }
        public string ExternalReference { get; set; }
        public string UserMessage { get; set; }

        public GatewayResult()
        {
            ExternalReference = string.Empty;
            UserMessage = string.Empty;
        }
    }

    internal interface IPrescriptionGateway
    {
        GatewayResult Validate(PrescriptionRecord prescription, PatientRecord patient);
    }

    internal interface IInsuranceGateway
    {
        GatewayResult CheckEligibility(string patientId, string payerCode);
        GatewayResult SubmitClaim(string claimId, string patientId, decimal amount);
        GatewayResult RequestPriorAuthorization(string requestId, string patientId, string drugCode);
    }

    internal interface IDrugCatalogGateway
    {
        DrugCatalogResult FindByGtin(string gtin);
    }

    internal sealed class DrugCatalogResult
    {
        public bool Found { get; set; }
        public string DrugCode { get; set; }
        public string DrugName { get; set; }
        public string Warning { get; set; }
    }

    // محاكيات واضحة لا تتصل بجهة صحية أو تأمينية؛ تحفظ مسار الواجهة والتدقيق قبل الاعتماد الرسمي.
    internal sealed class SimulatedPrescriptionGateway : IPrescriptionGateway
    {
        private readonly Func<DateTime> _today;

        public SimulatedPrescriptionGateway(Func<DateTime> today = null)
        {
            _today = today ?? (() => DateTime.Today);
        }

        public GatewayResult Validate(PrescriptionRecord prescription, PatientRecord patient)
        {
            if (prescription == null || patient == null || string.IsNullOrWhiteSpace(prescription.PrescriptionId))
                return Reject("بيانات الوصفة أو المريض غير مكتملة.");
            if (string.IsNullOrWhiteSpace(prescription.PatientId) || string.IsNullOrWhiteSpace(patient.PatientId) || prescription.PatientId != patient.PatientId)
                return Reject("الوصفة لا تخص المريض المحدد.");
            if (string.IsNullOrWhiteSpace(prescription.PrescriberName) || prescription.IssuedAt == default(DateTime) || prescription.ExpiresAt < prescription.IssuedAt)
                return Reject("بيانات الواصف أو تواريخ الوصفة غير صالحة.");
            if (prescription.ExpiresAt.Date < _today().Date) return Reject("الوصفة منتهية الصلاحية.");
            if (prescription.Items == null || prescription.Items.Count == 0)
                return Reject("لا توجد كمية قابلة للصرف في هذه الوصفة.");
            if (prescription.Items.Any(item => item == null || string.IsNullOrWhiteSpace(item.DrugCode)) ||
                prescription.Items.GroupBy(item => item.DrugCode.Trim(), StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
                return Reject("لا يمكن أن تحتوي الوصفة على رمز دواء مكرر أو فارغ.");
            foreach (PrescriptionItem item in prescription.Items)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.DrugCode) || item.PrescribedQuantity <= 0m || item.DispensedQuantity < 0m || item.DispensedQuantity > item.PrescribedQuantity)
                    return Reject("يوجد بند وصفة غير صالح أو تم صرفه بأكثر من الموصوف.");
            }
            if (!prescription.Items.Any(x => x.PrescribedQuantity > x.DispensedQuantity)) return Reject("لا توجد كمية قابلة للصرف في هذه الوصفة.");
            return Accept("SIM-RX-" + prescription.PrescriptionId, "تم التحقق محلياً في بيئة المحاكاة.");
        }

        private static GatewayResult Accept(string reference, string message) { return new GatewayResult { IsAccepted = true, ExternalReference = reference, UserMessage = message }; }
        private static GatewayResult Reject(string message) { return new GatewayResult { IsAccepted = false, IsRetryable = false, UserMessage = message }; }
    }

    internal sealed class SimulatedInsuranceGateway : IInsuranceGateway
    {
        public GatewayResult CheckEligibility(string patientId, string payerCode)
        {
            return string.IsNullOrWhiteSpace(patientId) || string.IsNullOrWhiteSpace(payerCode)
                ? Reject("رقم المريض ورمز شركة التأمين مطلوبان.")
                : Accept("SIM-ELIG-" + patientId, "الأهلية مقبولة في المحاكاة فقط.");
        }

        public GatewayResult SubmitClaim(string claimId, string patientId, decimal amount)
        {
            if (string.IsNullOrWhiteSpace(claimId) || string.IsNullOrWhiteSpace(patientId) || amount <= 0m) return Reject("المطالبة غير مكتملة.");
            return Accept("SIM-CLM-" + claimId, "تم قبول المطالبة محلياً؛ لم تُرسل إلى NPHIES.");
        }

        public GatewayResult RequestPriorAuthorization(string requestId, string patientId, string drugCode)
        {
            if (string.IsNullOrWhiteSpace(requestId) || string.IsNullOrWhiteSpace(patientId) || string.IsNullOrWhiteSpace(drugCode)) return Reject("طلب الموافقة المسبقة غير مكتمل.");
            return new GatewayResult { IsAccepted = false, IsRetryable = true, ExternalReference = "SIM-PA-" + requestId, UserMessage = "الموافقة المسبقة معلقة في المحاكاة." };
        }

        private static GatewayResult Accept(string reference, string message) { return new GatewayResult { IsAccepted = true, ExternalReference = reference, UserMessage = message }; }
        private static GatewayResult Reject(string message) { return new GatewayResult { IsAccepted = false, IsRetryable = false, UserMessage = message }; }
    }

    internal sealed class SimulatedDrugCatalogGateway : IDrugCatalogGateway
    {
        public DrugCatalogResult FindByGtin(string gtin)
        {
            return new DrugCatalogResult { Found = false, DrugCode = string.Empty, DrugName = string.Empty, Warning = "لا يوجد اتصال حي بكتالوج دوائي مرخّص." };
        }
    }

    internal sealed class IntegrationOutboxItem
    {
        public string MessageType { get; set; }
        public string AggregateId { get; set; }
        public string Payload { get; set; }
        public int AttemptCount { get; set; }
        public DateTime NextAttemptAtUtc { get; set; }
        public string LastError { get; set; }

        public IntegrationOutboxItem()
        {
            MessageType = string.Empty;
            AggregateId = string.Empty;
            Payload = string.Empty;
            LastError = string.Empty;
        }

        public void RegisterFailure(string error, DateTime nowUtc)
        {
            AttemptCount++;
            LastError = error ?? string.Empty;
            int delayMinutes = Math.Min(60, (int)Math.Pow(2, Math.Min(AttemptCount, 6)));
            NextAttemptAtUtc = nowUtc.AddMinutes(delayMinutes);
        }
    }
}

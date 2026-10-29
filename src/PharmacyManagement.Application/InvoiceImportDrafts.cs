using System;
using System.Collections.Generic;
using System.Linq;

namespace PharmacyManagement.Application
{
    public sealed class InvoiceImportDraftRecord
    {
        public Guid DraftId { get; set; }
        public InvoiceImportCandidate Candidate { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public string Status { get; set; }
        public string ApprovedReference { get; set; }
    }

    public sealed class InvoiceImportDraftSummary
    {
        public Guid DraftId { get; set; }
        public string SourceSystem { get; set; }
        public string SourceInvoiceId { get; set; }
        public ImportedInvoiceKind Kind { get; set; }
        public DateTime InvoiceDate { get; set; }
        public string PartyName { get; set; }
        public string Status { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }

    public interface IInvoiceImportDraftRepository
    {
        IReadOnlyList<Guid> SaveBatch(IReadOnlyList<InvoiceImportCandidate> candidates, string createdBy);
        InvoiceImportDraftRecord Get(Guid draftId);
        IReadOnlyList<InvoiceImportDraftSummary> ListRecent();
    }

    public interface IInvoiceImportDraftService
    {
        IReadOnlyList<Guid> SaveBatch(IReadOnlyList<InvoiceImportCandidate> candidates, OperationActor actor);
        InvoiceImportDraftRecord Get(Guid draftId, OperationActor actor);
        IReadOnlyList<InvoiceImportDraftSummary> ListRecent(OperationActor actor);
    }

    public sealed class InvoiceImportDraftService : IInvoiceImportDraftService
    {
        private readonly IInvoiceImportDraftRepository repository;
        private readonly Func<OperationActor, bool> canManageImports;

        public InvoiceImportDraftService(IInvoiceImportDraftRepository repository, Func<OperationActor, bool> canManageImports)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
            this.canManageImports = canManageImports ?? throw new ArgumentNullException(nameof(canManageImports));
        }

        public IReadOnlyList<Guid> SaveBatch(IReadOnlyList<InvoiceImportCandidate> candidates, OperationActor actor)
        {
            string user = RequireActor(actor);
            if (candidates == null || candidates.Count == 0)
                throw new InvalidOperationException("لا توجد فواتير لحفظها كمسودات.");
            var verified = new List<InvoiceImportCandidate>(candidates.Count);
            var identities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int lineCount = 0;
            foreach (InvoiceImportCandidate candidate in candidates)
            {
                if (candidate == null || candidate.Mode != InvoiceImportMode.Draft)
                    throw new InvalidOperationException("الحفظ كمسودة لا يعتمد فاتورة الاستيراد المباشر.");
                var checkedCandidate = InvoiceImportValidation.Validate(InvoiceImportMode.Draft,
                    candidate.SourceSystem, candidate.Lines).Single();
                string key = checkedCandidate.SourceSystem + "\u001f" + checkedCandidate.Kind + "\u001f" + checkedCandidate.SourceInvoiceId;
                if (!identities.Add(key)) throw new InvalidOperationException("فاتورة مصدر مكررة في دفعة المسودات.");
                lineCount = checked(lineCount + checkedCandidate.Lines.Count);
                if (lineCount > InvoiceImportValidation.MaximumRows)
                    throw new InvalidOperationException("الحد الأقصى 5,000 سطر في دفعة مسودات واحدة.");
                verified.Add(checkedCandidate);
            }
            return repository.SaveBatch(verified.AsReadOnly(), user);
        }

        public IReadOnlyList<InvoiceImportDraftSummary> ListRecent(OperationActor actor)
        {
            RequireActor(actor);
            return repository.ListRecent();
        }

        public InvoiceImportDraftRecord Get(Guid draftId, OperationActor actor)
        {
            RequireActor(actor);
            if (draftId == Guid.Empty) throw new ArgumentException("رقم المسودة غير صالح.", nameof(draftId));
            return repository.Get(draftId);
        }

        private string RequireActor(OperationActor actor)
        {
            string user = (actor?.UserName ?? string.Empty).Trim();
            string role = (actor?.UserRole ?? string.Empty).Trim();
            if (user.Length == 0 || user.Length > 200 || role.Length == 0 || role.Length > 50 ||
                !canManageImports(new OperationActor { UserName = user, UserRole = role }))
                throw new UnauthorizedAccessException("حفظ ومراجعة مسودات الفواتير متاح للمستخدم المخول فقط.");
            return user;
        }
    }
}

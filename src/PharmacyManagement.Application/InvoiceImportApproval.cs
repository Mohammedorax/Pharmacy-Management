using System;
using System.Linq;

namespace PharmacyManagement.Application
{
    public sealed class InvoiceImportApprovalResult
    {
        public ImportedInvoiceKind Kind { get; set; }
        public string SourceInvoiceId { get; set; }
        public string PostedReference { get; set; }
        public decimal TotalAmount { get; set; }
        public Guid? DraftId { get; set; }
    }

    public interface IInvoiceImportApprovalService
    {
        InvoiceImportApprovalResult Approve(InvoiceImportCandidate candidate, OperationActor actor);
        InvoiceImportApprovalResult ApproveDraft(Guid draftId, OperationActor actor);
    }

    // One call approves one document. The invoice operations own its SQL transaction,
    // including stock, journal, source identity and optional draft status.
    public sealed class InvoiceImportApprovalService : IInvoiceImportApprovalService
    {
        private readonly ISalesInvoiceOperation sales;
        private readonly IPurchaseInvoiceOperation purchases;
        private readonly IPurchaseInventoryModeProvider inventoryMode;
        private readonly IInvoiceImportDraftService drafts;
        private readonly Func<OperationActor, bool> canManageImports;
    private readonly IImportedInvoiceLookup lookup;

        public InvoiceImportApprovalService(ISalesInvoiceOperation sales, IPurchaseInvoiceOperation purchases,
            IPurchaseInventoryModeProvider inventoryMode, IInvoiceImportDraftService drafts,
            Func<OperationActor, bool> canManageImports, IImportedInvoiceLookup lookup = null)
        {
            this.sales = sales ?? throw new ArgumentNullException(nameof(sales));
            this.purchases = purchases ?? throw new ArgumentNullException(nameof(purchases));
            this.inventoryMode = inventoryMode ?? throw new ArgumentNullException(nameof(inventoryMode));
            this.drafts = drafts ?? throw new ArgumentNullException(nameof(drafts));
            this.canManageImports = canManageImports ?? throw new ArgumentNullException(nameof(canManageImports));
            this.lookup = lookup;
        }

        public InvoiceImportApprovalResult Approve(InvoiceImportCandidate candidate, OperationActor actor)
        {
            RequireActor(actor);
            if (candidate == null || candidate.Mode != InvoiceImportMode.ImportAndApprove)
                throw new InvalidOperationException("الاعتماد المباشر يتطلب فاتورة معاينة بنمط ImportAndApprove.");
            return Post(candidate, actor, null);
        }

        public InvoiceImportApprovalResult ApproveDraft(Guid draftId, OperationActor actor)
        {
            RequireActor(actor);
            if (draftId == Guid.Empty) throw new ArgumentException("رقم المسودة غير صالح.", nameof(draftId));
            InvoiceImportDraftRecord record = drafts.Get(draftId, actor);
            if (record == null || record.Status != "Draft")
                throw new InvalidOperationException("المسودة غير موجودة أو معتمدة سابقًا.");
            InvoiceImportCandidate approved = InvoiceImportValidation.Validate(InvoiceImportMode.ImportAndApprove,
                record.Candidate.SourceSystem, record.Candidate.Lines).Single();
            return Post(approved, actor, draftId);
        }

        private InvoiceImportApprovalResult Post(InvoiceImportCandidate candidate, OperationActor actor, Guid? draftId)
        {
            // A retry after a lost response must return the original posted reference.
            if (lookup != null)
            {
                ImportedInvoiceLookupResult existing = lookup.Find(
                    candidate.Kind == ImportedInvoiceKind.Sale ? "Sale" : "Purchase",
                    candidate.SourceSystem, candidate.SourceInvoiceId);
                if (existing != null && !string.IsNullOrWhiteSpace(existing.PostedReference))
                    return new InvoiceImportApprovalResult
                    {
                        Kind = candidate.Kind,
                        SourceInvoiceId = candidate.SourceInvoiceId,
                        PostedReference = existing.PostedReference,
                        TotalAmount = 0m,
                        DraftId = draftId
                    };
            }

            if (candidate.Kind == ImportedInvoiceKind.Sale)
            {
                SalesInvoiceIssueRequest request = InvoiceImportApprovalMapping.Sale(candidate, actor, draftId);
                SalesInvoiceIssueResult result = sales.Issue(request);
                return new InvoiceImportApprovalResult { Kind = candidate.Kind,
                    SourceInvoiceId = candidate.SourceInvoiceId, PostedReference = result.AccountingSourceId,
                    TotalAmount = result.NetTotal, DraftId = draftId };
            }
            PurchaseInvoiceRequest purchase = InvoiceImportApprovalMapping.Purchase(candidate, actor,
                inventoryMode.IsCommercialInventoryEnabled(), draftId);
            PurchaseInvoiceResult posted = purchases.Issue(purchase);
            return new InvoiceImportApprovalResult { Kind = candidate.Kind,
                SourceInvoiceId = candidate.SourceInvoiceId, PostedReference = posted.PurchaseId,
                TotalAmount = posted.TotalAmount, DraftId = draftId };
        }

        private void RequireActor(OperationActor actor)
        {
            string user = (actor?.UserName ?? string.Empty).Trim();
            string role = (actor?.UserRole ?? string.Empty).Trim();
            if (user.Length == 0 || user.Length > 200 || role.Length == 0 || role.Length > 50 ||
                !canManageImports(new OperationActor { UserName = user, UserRole = role }))
                throw new UnauthorizedAccessException("اعتماد فواتير Excel متاح للمستخدم المخول فقط.");
        }
    }
}

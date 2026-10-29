using System;
using System.Collections.Generic;
using PharmacyManagement.Application;

namespace PharmacyManagement.CoreTests
{
    internal static class InvoiceImportDraftTests
    {
        internal static int Run()
        {
            int checks = 0;
            var repository = new CapturingRepository();
            var service = new InvoiceImportDraftService(repository, principal => principal.UserRole == "Manager");
            var actor = new OperationActor { UserName = " مدير ", UserRole = "Manager" };
            var source = Row("SOURCE-1");
            InvoiceImportCandidate draft = InvoiceImportValidation.Validate(InvoiceImportMode.Draft,
                "Legacy", new[] { source })[0];
            source.Quantity = 200m;
            IReadOnlyList<Guid> ids = service.SaveBatch(new[] { draft }, actor);
            Check(ids.Count == 1 && ids[0] != Guid.Empty && repository.Saved[0].Lines[0].Quantity == 1.25m,
                "validated draft snapshot saved"); checks++;
            Check(repository.CreatedBy == "مدير", "actor name normalized"); checks++;
            Check(service.Get(ids[0], actor)?.Candidate.SourceInvoiceId == "SOURCE-1", "saved draft is readable"); checks++;
            Check(service.ListRecent(actor).Count == 1, "authorized draft list"); checks++;
            Reject<UnauthorizedAccessException>(() => service.ListRecent(new OperationActor { UserName = "cashier", UserRole = "Cashier" })); checks++;
            Reject<UnauthorizedAccessException>(() => service.Get(ids[0], new OperationActor { UserName = "cashier", UserRole = "Cashier" })); checks++;
            Reject<UnauthorizedAccessException>(() => service.SaveBatch(new[] { draft }, null)); checks++;
            InvoiceImportCandidate direct = InvoiceImportValidation.Validate(InvoiceImportMode.ImportAndApprove,
                "Legacy", new[] { Row("SOURCE-2") })[0];
            Reject<InvalidOperationException>(() => service.SaveBatch(new[] { direct }, actor)); checks++;
            Reject<InvalidOperationException>(() => service.SaveBatch(new[] { draft, draft }, actor)); checks++;
            Reject<InvalidOperationException>(() => service.SaveBatch(new InvoiceImportCandidate[0], actor)); checks++;
            Check(repository.SaveCalls == 1, "rejected requests never reach repository"); checks++;
            Console.WriteLine("PASS: " + checks + " invoice draft service assertions; no database connection.");
            return checks;
        }

        private static InvoiceImportRow Row(string id)
        {
            return new InvoiceImportRow { SourceInvoiceId = id, LineNumber = 1,
                Kind = ImportedInvoiceKind.Sale, InvoiceDate = new DateTime(2026, 9, 29),
                PartyName = "عميل Pharma", CurrencyCode = "YER", MedicineName = "باراسيتامول 500 mg",
                Barcode = "000123", Quantity = 1.25m, UnitPrice = 12.345m };
        }
        private static void Check(bool value, string detail)
        { if (!value) throw new InvalidOperationException("Invoice draft service: " + detail); }
        private static void Reject<T>(Action action) where T : Exception
        {
            try { action(); } catch (T) { return; }
            throw new InvalidOperationException("Invalid invoice draft operation was accepted.");
        }
        private sealed class CapturingRepository : IInvoiceImportDraftRepository
        {
            internal int SaveCalls;
            internal IReadOnlyList<InvoiceImportCandidate> Saved;
            internal string CreatedBy;
            internal Guid Id;
            public IReadOnlyList<Guid> SaveBatch(IReadOnlyList<InvoiceImportCandidate> candidates, string createdBy)
            {
                SaveCalls++; Saved = candidates; CreatedBy = createdBy; Id = Guid.NewGuid();
                return new[] { Id };
            }
            public IReadOnlyList<InvoiceImportDraftSummary> ListRecent()
            {
                return Saved == null ? new InvoiceImportDraftSummary[0] :
                    new[] { new InvoiceImportDraftSummary { DraftId = Id, SourceInvoiceId = Saved[0].SourceInvoiceId } };
            }
            public InvoiceImportDraftRecord Get(Guid draftId)
            {
                return draftId == Id ? new InvoiceImportDraftRecord { DraftId = Id,
                    Candidate = Saved[0], CreatedBy = CreatedBy, Status = "Draft" } : null;
            }
        }
    }
}

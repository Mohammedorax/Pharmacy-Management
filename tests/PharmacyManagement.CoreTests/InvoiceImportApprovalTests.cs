using System;
using System.Collections.Generic;
using PharmacyManagement.Application;

namespace PharmacyManagement.CoreTests
{
    internal static class InvoiceImportApprovalTests
    {
        internal static int Run()
        {
            int checks = 0;
            var actor = new OperationActor { UserName = " Manager ", UserRole = "Manager" };
            InvoiceImportCandidate sale = Candidate(ImportedInvoiceKind.Sale, InvoiceImportMode.ImportAndApprove);
            SalesInvoiceIssueRequest request = InvoiceImportApprovalMapping.Sale(sale, actor);
            Check(request.CustomerId == "C-001" && request.PhoneNumber == "000000000" &&
                request.ImportSourceInvoiceId == "SRC-001" && request.ImportCurrencyCode == "YER" &&
                request.ImportTaxRatePercent == 0m && request.ImportPriceTaxMode == "Inclusive" &&
                request.DisplayedGrossTotal == 200m && request.DisplayedNetTotal == 195m &&
                request.Lines[0].Quantity == 2 && request.EmployeeName == "Manager", "sale mapping and totals"); checks++;
            var fractional = Row(ImportedInvoiceKind.Sale); fractional.Quantity = 1.25m;
            Reject(() => InvoiceImportApprovalMapping.Sale(Validate(fractional), actor), "fractional sale quantity"); checks++;
            fractional = Row(ImportedInvoiceKind.Sale); fractional.UnitPrice = 12.345m;
            Reject(() => InvoiceImportApprovalMapping.Sale(Validate(fractional), actor), "fractional sale price"); checks++;
            fractional = Row(ImportedInvoiceKind.Sale); fractional.PaymentMethod = "Payable";
            Reject(() => InvoiceImportApprovalMapping.Sale(Validate(fractional), actor), "credit sale without posting contract"); checks++;
            fractional = Row(ImportedInvoiceKind.Sale); fractional.TaxRatePercent = null;
            Reject(() => InvoiceImportApprovalMapping.Sale(Validate(fractional), actor), "unknown sale tax snapshot"); checks++;
            fractional = Row(ImportedInvoiceKind.Sale); fractional.Quantity = 1.25m;
            RejectMapping(() => InvoiceImportApprovalMapping.Sale(Validate(fractional), actor),
                "fractional sale has a bilingual, deterministic mapping reason"); checks++;

            InvoiceImportCandidate purchase = Candidate(ImportedInvoiceKind.Purchase, InvoiceImportMode.ImportAndApprove);
            PurchaseInvoiceRequest buying = InvoiceImportApprovalMapping.Purchase(purchase, actor, true);
            Check(buying.ImportSourceSystem == "Legacy" && buying.ImportCurrencyCode == "YER" &&
                buying.CommercialInventoryExpected && buying.Lines[0].BuyingPrice == 100 &&
                buying.Lines[0].BatchNumber == "B-01" && buying.Lines[0].ExpiryDate.Year == 2027 &&
                buying.ActorRole == "Manager" && buying.ImportPaymentMethod == "Payable" &&
                buying.ImportTaxRatePercent == 0m, "purchase lot, payment and tax mapping"); checks++;
            fractional = Row(ImportedInvoiceKind.Purchase); fractional.BatchNumber = "";
            Reject(() => InvoiceImportApprovalMapping.Purchase(Validate(fractional), actor, true), "missing commercial batch"); checks++;
            fractional = Row(ImportedInvoiceKind.Purchase); fractional.PaymentMethod = "Cash";
            Check(InvoiceImportApprovalMapping.Purchase(Validate(fractional), actor, false).ImportPaymentMethod == "Cash",
                "cash purchase retains its explicit settlement"); checks++;
            fractional = Row(ImportedInvoiceKind.Purchase); fractional.PaymentMethod = "";
            Reject(() => InvoiceImportApprovalMapping.Purchase(Validate(fractional), actor, false), "purchase without payment method"); checks++;
            fractional = Row(ImportedInvoiceKind.Purchase); fractional.TaxRatePercent = 5m;
            Reject(() => InvoiceImportApprovalMapping.Purchase(Validate(fractional), actor, false), "taxed purchase without posting contract"); checks++;
            fractional = Row(ImportedInvoiceKind.Purchase); fractional.BatchNumber = "";
            RejectMapping(() => InvoiceImportApprovalMapping.Purchase(Validate(fractional), actor, true),
                "commercial purchase needs a batch before posting"); checks++;

            var sales = new FakeSales(); var purchases = new FakePurchases(); var drafts = new FakeDrafts();
            var service = new InvoiceImportApprovalService(sales, purchases, new FakeMode(), drafts, a => a.UserRole == "Manager");
            InvoiceImportApprovalResult direct = service.Approve(sale, actor);
            Check(sales.Calls == 1 && direct.PostedReference == "S-1" && direct.DraftId == null,
                "direct issue invokes existing sales operation once"); checks++;
            fractional = Row(ImportedInvoiceKind.Sale); fractional.Quantity = 1.25m;
            RejectMapping(() => service.Approve(Validate(fractional), actor),
                "invalid imported sale rejected by operation before writer");
            Check(sales.Calls == 1, "invalid import never reached the sales writer"); checks++;
            Reject(() => service.Approve(Candidate(ImportedInvoiceKind.Sale, InvoiceImportMode.Draft), actor),
                "draft cannot be silently directly posted"); checks++;
            drafts.Record = new InvoiceImportDraftRecord { DraftId = Guid.NewGuid(), Candidate =
                Candidate(ImportedInvoiceKind.Purchase, InvoiceImportMode.Draft), Status = "Draft" };
            InvoiceImportApprovalResult approvedDraft = service.ApproveDraft(drafts.Record.DraftId, actor);
            Check(purchases.Calls == 1 && purchases.Last.ImportDraftId == drafts.Record.DraftId &&
                approvedDraft.DraftId == drafts.Record.DraftId, "draft identity reaches purchase transaction"); checks++;
            drafts.Record.Status = "Approved";
            Reject(() => service.ApproveDraft(drafts.Record.DraftId, actor), "approved draft cannot post again"); checks++;
            var denied = new InvoiceImportApprovalService(sales, purchases, new FakeMode(), drafts, a => false);
            RejectUnauthorized(() => denied.Approve(sale, actor)); checks++;
        var lookup = new FakeLookup { Reference = "S-REC" };
        var retrySales = new FakeSales();
        var retryService = new InvoiceImportApprovalService(retrySales, purchases, new FakeMode(), drafts, a => a.UserRole == "Manager", lookup);
        InvoiceImportApprovalResult replay = retryService.Approve(sale, actor);
        Check(replay.PostedReference == "S-REC" && retrySales.Calls == 0 && lookup.Calls == 1,
            "import retry returns the recorded reference without posting"); checks++;

            Console.WriteLine("PASS: " + checks + " invoice approval mapping and orchestration assertions; no database connection.");
            return checks;
        }
        private static InvoiceImportCandidate Candidate(ImportedInvoiceKind kind, InvoiceImportMode mode)
        { return InvoiceImportValidation.Validate(mode, "Legacy", new[] { Row(kind) })[0]; }
        private static InvoiceImportCandidate Validate(InvoiceImportRow row)
        { return InvoiceImportValidation.Validate(InvoiceImportMode.ImportAndApprove, "Legacy", new[] { row })[0]; }
        private static InvoiceImportRow Row(ImportedInvoiceKind kind)
        {
            return new InvoiceImportRow { Kind = kind, SourceInvoiceId = "SRC-001", LineNumber = 1,
                InvoiceDate = new DateTime(2026, 1, 1), PartyName = "عميل أو مورد", PartyReference = "C-001",
                PartyPhone = "000000000", CurrencyCode = "YER", MedicineName = "باراسيتامول",
                Barcode = "000123", Quantity = 2m, UnitPrice = 100m, InvoiceDiscountPercent = kind == ImportedInvoiceKind.Sale ? 2.5m : 0m,
                TaxRatePercent = 0m, PriceTaxMode = "Inclusive", PaymentMethod = kind == ImportedInvoiceKind.Sale ? "Cash" : "Payable",
                ProductionDate = new DateTime(2025, 1, 1), ExpiryDate = new DateTime(2027, 1, 1), BatchNumber = "B-01" };
        }
        private static void Check(bool pass, string reason) { if (!pass) throw new Exception("Approval: " + reason); }
        private static void Reject(Action action, string reason)
        { try { action(); } catch (InvalidOperationException) { return; } throw new Exception("Approval accepted " + reason); }
        private static void RejectMapping(Action action, string reason)
        {
            try { action(); }
            catch (InvoiceImportMappingException error)
            {
                Check(!string.IsNullOrWhiteSpace(error.Message) && !string.IsNullOrWhiteSpace(error.EnglishMessage), reason);
                return;
            }
            throw new Exception("Approval did not report a mapping reason: " + reason);
        }
        private static void RejectUnauthorized(Action action)
        { try { action(); } catch (UnauthorizedAccessException) { return; } throw new Exception("Unauthorized import was accepted."); }
        private sealed class FakeSales : ISalesInvoiceOperation
        { public int Calls; public SalesInvoiceIssueResult Issue(SalesInvoiceIssueRequest request)
            { Calls++; return new SalesInvoiceIssueResult { AccountingSourceId = "S-1", NetTotal = request.DisplayedNetTotal }; } }
        private sealed class FakePurchases : IPurchaseInvoiceOperation
        { public int Calls; public PurchaseInvoiceRequest Last; public PurchaseInvoiceResult Issue(PurchaseInvoiceRequest request)
            { Calls++; Last = request; return new PurchaseInvoiceResult { PurchaseId = "P-1", TotalAmount = 200m }; } }
        private sealed class FakeMode : IPurchaseInventoryModeProvider
        { public bool IsCommercialInventoryEnabled() { return true; } }
        private sealed class FakeDrafts : IInvoiceImportDraftService
        { public InvoiceImportDraftRecord Record;
            public IReadOnlyList<Guid> SaveBatch(IReadOnlyList<InvoiceImportCandidate> candidates, OperationActor actor)
            { throw new NotSupportedException(); }
            public InvoiceImportDraftRecord Get(Guid id, OperationActor actor) { return Record; }
            public IReadOnlyList<InvoiceImportDraftSummary> ListRecent(OperationActor actor)
            { throw new NotSupportedException(); } }
        private sealed class FakeLookup : IImportedInvoiceLookup
        {
            public string Reference; public int Calls;
            public ImportedInvoiceLookupResult Find(string kind, string sourceSystem, string sourceInvoiceId)
            { Calls++; return string.IsNullOrEmpty(Reference) ? null : new ImportedInvoiceLookupResult { PostedReference = Reference }; }
        }
    }
}

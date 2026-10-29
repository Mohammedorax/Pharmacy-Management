using System;
using System.Collections.Generic;
using System.Linq;
using project_fo_3;

namespace PharmacyManagement.CoreTests
{
    internal static class Program
    {
        private static int checks;
        private static int Main(string[] args)
        {
            try
            {
                if(args.Length==4&&args[0]=="--owned-unit-report-currency-rollback"&&args[3]=="--confirm-owned-temporary-qa-writes")
                {return UnitReportCurrencySqlTests.Run(new[]{args[1],args[2]});}
                if(args.Length==4&&args[0]=="--owned-return-history-units-rollback"&&args[3]=="--confirm-owned-temporary-qa-writes")
                {return ReturnHistoryUnitSqlTests.Run(new[]{args[1],args[2]});}
                if(args.Length==4&&args[0]=="--owned-popular-unit-rollback"&&args[3]=="--confirm-owned-temporary-qa-writes")
                {return PopularUnitSqlTests.Run(new[]{args[1],args[2]});}
                if(args.Length==5&&(args[0]=="--owned-voucher-sql-response-writer"||args[0]=="--owned-voucher-sql-response-reader")&&args[4]=="--confirm-owned-temporary-qa-writes")
                {return VoucherOwnedRecoverySqlTests.Child(args[1],args[2],args[3],args[0]=="--owned-voucher-sql-response-writer");}
                if(args.Length==1&&args[0]=="--verify-voucher-pending-store"){VoucherPendingStoreTests.Run();return 0;}
                if(args.Length==3&&args[0]=="--owned-voucher-inputs-child-read"){VoucherPendingStoreTests.ChildInputsRead(args[1],args[2]);return 0;}
                if(args.Length==3&&args[0]=="--owned-voucher-store-child-read"){VoucherPendingStoreTests.ChildRead(args[1],args[2]);return 0;}
                if(args.Length==5 && (args[0]=="--owned-voucher-request-identity"||args[0]=="--owned-voucher-request-missing-schema") && args[4]=="--confirm-owned-temporary-qa-writes")
                { VoucherRequestIdentitySqlTests.Verify(args[1],args[2],args[3],args[0]=="--owned-voucher-request-missing-schema");return 0; }
                if(args.Length==3 && args[0]=="--qa-general-ledger-readonly"){GeneralLedgerReadSqlTests.VerifyQaReadOnly(args[1],args[2]);return 0;}
                if(args.Length==3 && args[0]=="--qa-inventory-operation-readonly"){InventoryOperationReadSqlTests.Verify(args[1],args[2]);return 0;}
                if (args.Length == 5 && args[0] == "--temporary-qa-inventory-commit" && args[4] == "--confirm-owned-temporary-qa-writes")
                { InventoryPostingSqlTests.VerifyTemporaryQaCommit(args[1],args[2],args[3]);return 0; }
                if (args.Length == 5 && args[0] == "--temporary-qa-accounting-commit" && args[4] == "--confirm-owned-temporary-qa-writes")
                {
                    JournalPostingSqlTests.VerifyTemporaryQaCommit(args[1], args[2], args[3]);
                    return 0;
                }
                if (args.Length == 5 && args[0] == "--qa-account-write-rollback" && args[4] == "--confirm-qa-account-writes")
                {
                    AccountWritingSqlTests.VerifyQaRollback(args[1], args[2], args[3]);
                    return 0;
                }
                if (args.Length == 5 && args[0] == "--qa-voucher-post-rollback" && args[4] == "--confirm-qa-voucher-writes")
                {
                    VoucherPostingSqlTests.VerifyQaRollback(args[1], args[2], args[3]);
                    return 0;
                }
                if (args.Length == 5 && args[0] == "--qa-journal-post-rollback" && args[4] == "--confirm-qa-journal-writes")
                {
                    JournalPostingSqlTests.VerifyQaRollback(args[1], args[2], args[3]);
                    return 0;
                }
                if (args.Length == 4 && args[0] == "--qa-currency-cycle" && args[3] == "--confirm-qa-writes")
                {
                    CurrencyWriteTests.VerifyQaCycle(args[1], args[2]);
                    return 0;
                }
                if (args.Length == 3 && args[0] == "--qa-currencies-readonly")
                {
                    CurrencyReadTests.VerifyQaReadOnly(args[1], args[2]);
                    return 0;
                }
                if (args.Length == 3 && args[0] == "--qa-journal-documents-readonly")
                { JournalDocumentReadTests.VerifyQaReadOnly(args[1], args[2]); return 0; }
                if (args.Length == 3 && args[0] == "--qa-voucher-documents-readonly")
                {
                    VoucherDocumentReadTests.VerifyQaReadOnly(args[1], args[2]);
                    return 0;
                }
                if (args.Length == 3 && args[0] == "--qa-voucher-history-readonly")
                {
                    VoucherHistoryReadTests.VerifyQaReadOnly(args[1], args[2]);
                    return 0;
                }
                if (args.Length == 3 && args[0] == "--qa-journal-preparation-readonly")
                {
                    JournalEntryPreparationTests.VerifyQaReadOnly(args[1], args[2]);
                    return 0;
                }
                if (args.Length == 3 && args[0] == "--qa-accounting-lookups-readonly")
                {
                    AccountingLookupReadTests.VerifyQaReadOnly(args[1], args[2]);
                    return 0;
                }
                if (args.Length == 3 && args[0] == "--qa-journal-history-readonly")
                {
                    JournalHistoryReadTests.VerifyQaReadOnly(args[1], args[2]);
                    return 0;
                }
                if (args.Length == 3 && args[0] == "--qa-search")
                {
                    SearchTests.VerifyQaReadOnly(args[1], args[2]);
                    return 0;
                }
                if (args.Length == 3 && args[0] == "--qa-manufacturers-readonly")
                {
                    ManufacturerTests.VerifyQaReadOnly(args[1], args[2]);
                    return 0;
                }
                if (args.Length == 3 && args[0] == "--qa-medicines-readonly")
                {
                    MedicineTests.VerifyQaReadOnly(args[1], args[2]);
                    return 0;
                }
                if (args.Length == 3 && args[0] == "--qa-catalog-import-rollback")
                {
                    MedicineTests.VerifyQaCatalogImportRollback(args[1], args[2]);
                    return 0;
                }
                if (args.Length == 3 && args[0] == "--qa-active-ingredients-readonly")
                {
                    ActiveIngredientTests.VerifyQaReadOnly(args[1], args[2]);
                    return 0;
                }
                if (args.Length == 3 && args[0] == "--qa-employees-readonly")
                {
                    EmployeeTests.VerifyQaReadOnly(args[1], args[2]);
                    return 0;
                }
                if (args.Length == 3 && args[0] == "--qa-cashbox-rollback")
                {
                    CashBoxTests.VerifyQaRollback(args[1], args[2]);
                    return 0;
                }
                if (args.Length == 3 && args[0] == "--qa-cashbox-roundtrip")
                {
                    CashBoxTests.VerifyQaRoundtrip(args[1], args[2]);
                    return 0;
                }
                if (args.Length == 3 && args[0] == "--qa-cashbox-cancellation-integrity")
                {
                    CashBoxTests.VerifyQaCancellationTriggerRollback(args[1], args[2]);
                    return 0;
                }
                if (args.Length == 3 && args[0] == "--qa-cashbox-journal-rollback")
                {
                    CashBoxTests.VerifyQaJournalLinkRollback(args[1], args[2]);
                    return 0;
                }
                if (args.Length == 4 && args[0] == "--qa-permission-roundtrip" &&
                    args[3] == "--confirm-qa-permission-writes")
                {
                    EmployeeTests.VerifyQaPermissionRoundtrip(args[1], args[2]);
                    return 0;
                }
                if (args.Length == 3 && args[0] == "--qa-sales-customers-readonly")
                {
                    SalesCustomerTests.VerifyQaReadOnly(args[1], args[2]);
                    return 0;
                }
                if (args.Length == 3 && args[0] == "--qa-sales-history-readonly")
                {
                    SalesHistoryTests.VerifyQaReadOnly(args[1], args[2]);
                    return 0;
                }
                if (args.Length == 3 && args[0] == "--qa-purchase-workspace-readonly")
                {
                    PurchaseWorkspaceTests.VerifyQaReadOnly(args[1], args[2]);
                    return 0;
                }
                if (args.Length != 0) throw new ArgumentException("Use no arguments or a supported --qa-*-readonly server database_qa command.");
                checks += InvoiceImportTests.Run();
                checks += InvoiceImportApprovalTests.Run();
                checks += InvoiceImportDraftTests.Run();
                DomainIsIndependent();
                ReferenceInvoices();
                DiscountsConserveMoney();
                TaxPolicyIsSnapshot();
                ReturnsUseOriginalValues();
                FefoAndReturnLimits();
                checks += AuthenticationTests.Run();
                checks += ManufacturerTests.Run();
                checks += MedicineTests.Run();
                checks += ActiveIngredientTests.Run();
                checks += EmployeeTests.Run();
                checks += CashBoxTests.Run();
                checks += SalesCustomerTests.Run();
                checks += PurchaseMedicineReadTests.Run();
                checks += PurchaseLookupReadTests.Run();
                checks += PurchaseInventoryModeReadTests.Run();
                checks += PurchaseDraftNumberTests.Run();
                checks += PurchaseWriteIntentTests.Run();
                checks += MedicineSyncReadTests.Run();
                checks += CustomerMasterWriteTests.Run();
                checks += CustomerReferenceReadTests.Run();
                checks += SalesCustomerCreditReadTests.Run();
                checks += SalesHistoryTests.Run();
                checks += DashboardReadTests.Run();
                checks += CurrencyReadTests.Run();
                checks += CurrencyWriteTests.Run();
                checks += ReportSearchCriteriaTests.Run();
                checks += OperationalReportReadTests.Run();
                checks += SystemMonitorLogReadTests.Run();
                checks += MonitorGuardTests.Run();
                checks += InventoryAlertReadTests.Run();
                checks += InventoryIntegrityReadTests.Run();
                checks += AccountingIntegrityReadTests.Run();
                checks += AccountingReportReadRequestTests.Run();
                checks += AccountingReportsReadTests.Run();
                checks += AccountBalanceReadTests.Run();
                checks += VoucherAmountTests.Run();
                checks += JournalDocumentReadTests.Run();
                checks += VoucherDocumentReadTests.Run();
                checks += VoucherHistoryReadTests.Run();
                checks += QaAccountingWriteFixture.VerifyBackupHeaderContracts();
                checks += JournalPostingTests.Run();
                checks += JournalNativeAsyncTests.Run();
                checks += JournalCurrencySnapshotTests.Run();
                checks += JournalSubmissionRecoveryTests.Run();
                checks += JournalPayloadComparisonTests.Run();
                checks += VoucherPostingTests.Run(); checks += VoucherSubmissionStatusTests.Run();
                checks += AccountWritingTests.Run();
                checks += JournalEntryPreparationTests.Run();
                checks += JournalAmountCalculatorTests.Run();
                checks += InventoryQuantityCalculatorTests.Run();
                checks += MedicineUnitTests.Run();
                checks += MedicineUnitCatalogServiceTests.Run();
                checks += MedicineUnitInvoiceTests.Run();
                checks += InventoryPostingTests.Run();
                checks += InventoryOperationReadTests.Run();
                checks += GeneralLedgerReadTests.Run();
                checks += AccountingLookupReadTests.Run();
                checks += JournalEditorLookupReadTests.Run();
                checks += JournalHistoryReadTests.Run();
                checks += PurchaseWorkspaceTests.Run();
                SearchTests.Run();
                Console.WriteLine("PASS: " + checks + " domain assertions; no database connection used.");
                return 0;
            }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }

        private static void Check(bool value, string message)
        {
            checks++;
            if (!value) throw new InvalidOperationException(message);
        }

        private static void Reject<T>(Action action, string message) where T : Exception
        {
            try { action(); }
            catch (T) { checks++; return; }
            throw new InvalidOperationException(message);
        }

        private static CommercialInvoiceLine Line(decimal price, decimal quantity = 1m, decimal tax = 15m,
            CommercialPriceTaxMode mode = CommercialPriceTaxMode.Exclusive)
        {
            return new CommercialInvoiceLine { MedicineName = "باراسيتامول 500 mg", UnitPrice = price,
                Quantity = quantity, TaxRatePercent = tax, PriceTaxMode = mode };
        }

        private static void DomainIsIndependent()
        {
            var domain = typeof(InvoiceTotalsCalculator).Assembly;
            Check(domain.GetName().Name == "PharmacyManagement.Domain", "Calculations must execute from the extracted assembly.");
            foreach (var reference in domain.GetReferencedAssemblies())
                Check(reference.Name == "mscorlib" || reference.Name == "System" || reference.Name == "System.Core",
                    "Unexpected domain dependency: " + reference.Name);
        }

        private static void ReferenceInvoices()
        {
            var item = Line(100m, 2m); item.LineDiscount = 10m;
            var value = InvoiceTotalsCalculator.Calculate(new[] { item }, 10m);
            Check(value.GrossTotal == 200m && value.DiscountTotal == 29m && value.TaxableTotal == 171m &&
                value.TaxTotal == 25.65m && value.NetTotal == 196.65m, "Exclusive reference changed.");
            value = InvoiceTotalsCalculator.Calculate(new[] { Line(115m, mode: CommercialPriceTaxMode.Inclusive) }, 0m);
            Check(value.NetTotal == 115m && value.TaxableTotal == 100m && value.TaxTotal == 15m, "Inclusive reference changed.");
            value = InvoiceTotalsCalculator.Calculate(new[] { Line(8m, 1.125m, 0m) }, 0m);
            Check(value.NetTotal == 9m, "Fractional quantity lost precision.");
            Reject<InvalidOperationException>(() => InvoiceTotalsCalculator.Calculate(new[] { Line(1m, 1.0001m) }, 0m), "Unsupported quantity scale accepted.");
            Reject<InvalidOperationException>(() => InvoiceTotalsCalculator.Calculate(new[] { Line(1.001m) }, 0m), "Unsupported price scale accepted.");
            Reject<InvalidOperationException>(() => InvoiceTotalsCalculator.Calculate(new CommercialInvoiceLine[0], 0m), "Empty invoice accepted.");
            Check(InvoiceTotalsCalculator.Round(.005m) == .01m && InvoiceTotalsCalculator.Round(-.005m) == -.01m,
                "Money midpoint rounding changed.");
        }

        private static void DiscountsConserveMoney()
        {
            var tiny = Enumerable.Range(0, 6).Select(i => { var l = Line(.01m); l.MedicineName = "Item " + i; return l; }).ToArray();
            var discounts = InvoiceTotalsCalculator.CalculateAllocatedInvoiceDiscounts(tiny, 50m);
            Check(discounts.Sum() == .03m && discounts.All(d => d >= 0m && d <= .01m), "Tiny discounts escaped line bounds.");
            var taxable = Line(100m); var exempt = Line(100m, tax: 0m);
            var a = InvoiceTotalsCalculator.Calculate(new[] { taxable, exempt }, 10m);
            var b = InvoiceTotalsCalculator.Calculate(new[] { exempt, taxable }, 10m);
            Check(a.TaxTotal == 13.5m && a.NetTotal == 193.5m && a.NetTotal == b.NetTotal, "Display order changed tax.");
            var free = Line(0m);
            Check(InvoiceTotalsCalculator.CalculateAllocatedInvoiceDiscounts(new[] { taxable, free }, 100m)[1] == 0m,
                "Free item absorbed discount.");
        }

        private static void TaxPolicyIsSnapshot()
        {
            Check(new TaxPolicy(" ye ", true, 15m).EffectiveRate == 0m, "Yemen must have zero effective tax.");
            Check(new TaxPolicy(null, true, 15m).CountryCode == "YE", "Country default changed.");
            var original = new TaxPolicy("SA", true, 15m);
            var disabled = new TaxPolicy("SA", false, 15m);
            Check(original.EffectiveRate == 15m && disabled.EffectiveRate == 0m && original.IsInEffect,
                "Later policy altered earlier snapshot.");
            Reject<ArgumentOutOfRangeException>(() => new TaxPolicy("SA", true, 15.001m), "Unsupported tax precision accepted.");
        }

        private static void ReturnsUseOriginalValues()
        {
            var source = new CommercialReturnSourceLine { OriginalLineId = Guid.NewGuid(), MedicineName = "دواء",
                Quantity = 3m, UnitPrice = 100m, TaxRate = 15m, TaxTotal = 45m, NetTotal = 345m, UnitCost = 60m, CostTotal = 180m };
            var first = CommercialReturnCalculator.CalculateLine(source, new CommercialReturnPostedLine[0], 1m);
            Check(first.NetTotal == 115m && first.CostTotal == 60m, "First return does not match source.");
            var prior = new[] { new CommercialReturnPostedLine { Quantity = 1m, GrossTotal = 100m,
                TaxTotal = 15m, NetTotal = 115m, CostTotal = 60m } };
            var final = CommercialReturnCalculator.CalculateLine(source, prior, 2m);
            Check(first.NetTotal + final.NetTotal == 345m && first.CostTotal + final.CostTotal == 180m,
                "Returns do not reconcile with original snapshot.");
            Reject<InvalidOperationException>(() => CommercialReturnCalculator.CalculateLine(source, prior, 3m), "Over-return accepted.");
            Reject<InvalidOperationException>(() => CommercialReturnCalculator.CalculateLine(source, prior, .5m), "Unsupported fractional return accepted.");
            Check(CommercialReturnDispositionPolicy.RestoresInventoryValue(CommercialReturnDisposition.Quarantine) &&
                !CommercialReturnDispositionPolicy.RestoresInventoryValue(CommercialReturnDisposition.Damaged), "Disposition accounting changed.");
        }

        private static void FefoAndReturnLimits()
        {
            var date = new DateTime(2026, 9, 21);
            var earlier = new InventoryLotSnapshot { LotId = Guid.NewGuid(), MedicineName = "دواء", ExpiryDate = date.AddDays(2), RemainingQuantity = 2m };
            var later = new InventoryLotSnapshot { LotId = Guid.NewGuid(), MedicineName = "دواء", ExpiryDate = date.AddDays(5), RemainingQuantity = 4m };
            var expired = new InventoryLotSnapshot { LotId = Guid.NewGuid(), MedicineName = "دواء", ExpiryDate = date.AddDays(-1), RemainingQuantity = 99m };
            var allocation = FefoAllocator.Allocate("دواء", 3m, new[] { later, expired, earlier }, date);
            Check(allocation.Count == 2 && allocation[0].LotId == earlier.LotId && allocation[0].Quantity == 2m && allocation[1].Quantity == 1m,
                "FEFO order/expiry changed.");
            Reject<InvalidOperationException>(() => FefoAllocator.Allocate("دواء", 7m, new[] { earlier, later, expired }, date), "Expired stock fulfilled shortage.");
            Reject<InvalidOperationException>(() => LotReturnValidator.Validate(allocation,
                new[] { new LotReturnRequest { LotId = earlier.LotId, Quantity = 1m } },
                new[] { new LotReturnRequest { LotId = earlier.LotId, Quantity = 2m } }), "Cumulative lot over-return accepted.");
        }
    }
}








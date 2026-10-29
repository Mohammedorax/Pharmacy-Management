using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace project_fo_3
{
    internal static class QaCommercialReturnSelfTests
    {
        private const string Confirmation = "--confirm-qa-commercial-return-writes";
        private const string ConcurrencyConfirmation = "--confirm-temporary-concurrency-database-writes";
        private const string User = "QA Commercial Return Verifier";
        private const string Medicine = "__QA_COMMERCIAL_RETURN_ROLLBACK__";
        private const string Customer = "__QA_CUST_ROLLBACK__";

        internal static void Run(string serverName, string databaseName, string confirmation)
        {
            if (!string.Equals(confirmation, Confirmation, StringComparison.Ordinal))
                throw new InvalidOperationException("يلزم تأكيد صريح لاختبار المرتجع التجاري على QA.");
            if (string.IsNullOrWhiteSpace(databaseName) ||
                !(databaseName.EndsWith("_qa", StringComparison.OrdinalIgnoreCase) ||
                  databaseName.EndsWith("_test", StringComparison.OrdinalIgnoreCase) ||
                  databaseName.EndsWith("_sandbox", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("اختبار المرتجع التجاري مسموح لقاعدة QA/Test/Sandbox فقط.");

            string connectionString = Database.BuildSettingsConnectionString(serverName, databaseName, true);
            int beforeMarker;
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                beforeMarker = Convert.ToInt32(Scalar(connection, null,
                    "SELECT COUNT(*) FROM dbo.MedicineTb1 WHERE MedName=N'" + Medicine + "';"));
                if (beforeMarker != 0)
                    throw new InvalidOperationException("وجدت بيانات اختبار سابقة؛ لن يعاد استخدامها أو حذفها تلقائياً.");

                SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable);
                try
                {
                    SessionContext.Start(User, "Manager");
                    DateTime managedPeriodDate=DateTime.UtcNow.Date.AddDays(10);
                    Guid managedPeriodId=AccountingPeriodService.CreateOpen(connection,transaction,managedPeriodDate,managedPeriodDate,"QA managed-period rollback verification");
                    AccountingPeriodService.Close(connection,transaction,managedPeriodId,"QA managed-period rollback verification");
                    Require(Count(connection,transaction,"SELECT COUNT(*) FROM dbo.AccountingPeriodsTb WHERE PeriodId='"+managedPeriodId.ToString("D")+"' AND Status=N'Closed' AND ClosedAtUtc IS NOT NULL AND ClosedBy=N'"+User+"';")==1,
                        "خدمة إدارة الفترة لم تنشئ الفترة وتغلقها بأثر تدقيق مكتمل.");
                    PrepareIsolatedFixture(connection, transaction);
                    VerifyCompetingSequenceLockWaits(connection, transaction);
                    DateTime businessDate = DateTime.UtcNow.Date.AddHours(12);
                    Dictionary<string, decimal> reportBefore = ReadDailyReport(connection, transaction, businessDate);

                    CommercialSaleDraft saleDraft = new CommercialSaleDraft
                    {
                        CurrencyCode = "SAR", ExchangeRate = 1m, IssuedBy = User,
                        CustomerId = Customer, CustomerName = "QA rollback customer",
                        Notes = "QA rollback-only commercial return verification",
                        Lines = new[]
                        {
                            new CommercialInvoiceLine
                            {
                                MedicineName = Medicine, Quantity = 3m, UnitPrice = 10m,
                                LineDiscount = 0m, TaxRatePercent = 15m,
                                PriceTaxMode = CommercialPriceTaxMode.Exclusive
                            }
                        }
                    };
                    CommercialSaleResult sale = CommercialSalesRepository.Issue(connection, transaction, saleDraft, businessDate);
                    AccountingService.PostSalesInvoice(connection, transaction, sale.InvoiceId.ToString("D"),
                        sale.Totals, "QA commercial sale rollback verification", sale.CostOfGoods, businessDate);

                    Guid lineId = Guid.Parse(Convert.ToString(Scalar(connection, transaction,
                        "SELECT LineId FROM dbo.SalesInvoiceLinesTb WHERE InvoiceId='" + sale.InvoiceId.ToString("D") + "';")));
                    Guid allocationId = Guid.Parse(Convert.ToString(Scalar(connection, transaction,
                        "SELECT AllocationId FROM dbo.LotAllocationsTb WHERE InvoiceLineId='" + lineId.ToString("D") + "' AND MovementType=N'Sale';")));

                    CommercialSalesReturnRequest firstRequest = Request(sale.InvoiceId, lineId, allocationId,
                        "qa-return-restock", CommercialReturnDisposition.Restock);
                    CommercialSalesReturnResult first = CommercialSalesReturnService.Issue(connection, transaction, firstRequest, businessDate);
                    Require(!first.IsDuplicate && first.Totals.NetTotal == 11.50m && first.ReturnedCost == 6m,
                        "المرتجع الأول لا يطابق لقطة البيع الأصلية.");

                    CommercialSalesReturnRequest secondRequest = Request(sale.InvoiceId, lineId, allocationId,
                        "qa-return-quarantine", CommercialReturnDisposition.Quarantine, "Credit");
                    CommercialSalesReturnResult second = CommercialSalesReturnService.Issue(connection, transaction, secondRequest, businessDate);
                    Require(!second.IsDuplicate && second.Totals.NetTotal == 11.50m && second.ReturnedCost == 6m,
                        "المرتجع المحجور لا يطابق لقطة البيع الأصلية.");

                    CommercialSalesReturnResult duplicate = CommercialSalesReturnService.Issue(connection, transaction, firstRequest, businessDate);
                    Require(duplicate.IsDuplicate && duplicate.ReturnId == first.ReturnId && duplicate.ReturnInvoiceId == first.ReturnInvoiceId,
                        "إعادة نفس الطلب أنشأت مرتجعاً جديداً بدلاً من إعادة النتيجة المعتمدة.");
                    Require(Count(connection, transaction,
                        "SELECT COUNT(*) FROM dbo.CustomerAccountLedgerTb WHERE CustomerId=N'" + Customer + "' AND SourceType=N'CommercialSalesReturn' AND SourceId='" + second.ReturnInvoiceId.ToString("D") + "' AND EntryKind=N'CreditGranted' AND Debit=0 AND Credit=11.50;") == 1,
                        "دفتر ذمة العميل لم يسجل رصيد المرتجع الآجل مرة واحدة بالقيمة الصحيحة.");
                    Require(CustomerAccountLedgerService.GetBalance(connection, transaction, Customer) == 11.50m,
                        "رصيد العميل الفردي لا يطابق إشعار المرتجع الآجل.");
                    Require(Convert.ToDecimal(Scalar(connection, transaction,
                        "SELECT ISNULL(SUM(l.Credit-l.Debit),0) FROM dbo.JournalEntriesTb e JOIN dbo.JournalEntryLinesTb l ON l.JournalEntryId=e.JournalEntryId WHERE e.SourceType=N'CommercialSalesReturn' AND e.SourceId='" + second.ReturnInvoiceId.ToString("D") + "' AND l.AccountCode=N'" + AccountingService.CustomersAccount + "';")) == 11.50m,
                        "قيد حساب العملاء العام لا يطابق رصيد العميل الفردي.");

                    transaction.Save("BeforeQaCreditRedemption");
                    CommercialSaleResult creditSale = CommercialSalesRepository.Issue(connection, transaction, new CommercialSaleDraft
                    {
                        CurrencyCode = "SAR", ExchangeRate = 1m, IssuedBy = User,
                        CustomerId = Customer, CustomerName = "QA rollback customer", CustomerCreditApplied = 11.50m,
                        Notes = "QA customer-credit redemption rollback verification",
                        Lines = new[]
                        {
                            new CommercialInvoiceLine
                            {
                                MedicineName = Medicine, Quantity = 1m, UnitPrice = 10m, LineDiscount = 0m,
                                TaxRatePercent = 15m, PriceTaxMode = CommercialPriceTaxMode.Exclusive
                            }
                        }
                    }, businessDate);
                    int creditJournalId = AccountingService.PostSalesInvoice(connection, transaction, creditSale.InvoiceId.ToString("D"),
                        creditSale.Totals, "QA customer-credit sale rollback verification", creditSale.CostOfGoods, businessDate, creditSale.CustomerCreditApplied);
                    CustomerAccountLedgerService.RecordCreditUse(connection, transaction, Customer, creditJournalId, creditSale.InvoiceId.ToString("D"),
                        creditSale.CustomerCreditApplied, businessDate, User);
                    CustomerAccountLedgerService.RecordCreditUse(connection, transaction, Customer, creditJournalId, creditSale.InvoiceId.ToString("D"),
                        creditSale.CustomerCreditApplied, businessDate, User);
                    Require(creditSale.CustomerCreditApplied == 11.50m && creditSale.CashCollected == 0m &&
                        CustomerAccountLedgerService.GetBalance(connection, transaction, Customer) == 0m,
                        "استخدام رصيد العميل لم يغط الفاتورة بالكامل أو لم يخفض الرصيد الفردي.");
                    Require(Count(connection, transaction, "SELECT COUNT(*) FROM dbo.SalesInvoicePaymentsTb WHERE InvoiceId='" + creditSale.InvoiceId.ToString("D") + "' AND PaymentMethod=N'CustomerCredit' AND Amount=11.50;") == 1 &&
                        Count(connection, transaction, "SELECT COUNT(*) FROM dbo.SalesInvoicePaymentsTb WHERE InvoiceId='" + creditSale.InvoiceId.ToString("D") + "' AND PaymentMethod=N'Cash';") == 0 &&
                        Count(connection, transaction, "SELECT COUNT(*) FROM dbo.CustomerAccountLedgerTb WHERE SourceType=N'CommercialSalesInvoice' AND SourceId='" + creditSale.InvoiceId.ToString("D") + "' AND EntryKind=N'CreditUsed' AND Debit=11.50 AND Credit=0;") == 1,
                        "تقسيم دفع فاتورة الرصيد أو حركة دفتر العميل غير صحيح أو غير آمن عند التكرار.");
                    Require(Convert.ToDecimal(Scalar(connection, transaction,
                        "SELECT ISNULL(SUM(CASE WHEN l.AccountCode=N'" + AccountingService.CashAccount + "' THEN l.Debit-l.Credit ELSE 0 END),0) FROM dbo.JournalEntryLinesTb l WHERE l.JournalEntryId=" + creditJournalId + ";")) == 0m &&
                        Convert.ToDecimal(Scalar(connection, transaction,
                        "SELECT ISNULL(SUM(l.Debit-l.Credit),0) FROM dbo.JournalEntryLinesTb l WHERE l.JournalEntryId=" + creditJournalId + " AND l.AccountCode=N'" + AccountingService.CustomersAccount + "';")) == 11.50m,
                        "القيد المالي لم يفصل رصيد العميل عن النقد المحصل.");
                    transaction.Rollback("BeforeQaCreditRedemption");
                    Require(CustomerAccountLedgerService.GetBalance(connection, transaction, Customer) == 11.50m,
                        "التراجع إلى نقطة الحفظ لم يستعد رصيد العميل بالكامل.");

                    Guid quarantineAllocationId = Guid.Parse(Convert.ToString(Scalar(connection, transaction,
                        "SELECT a.ReturnLotAllocationId FROM dbo.CommercialReturnLotAllocationsTb a JOIN dbo.CommercialReturnLinesTb l ON l.ReturnLineId=a.ReturnLineId WHERE l.ReturnId='" + second.ReturnId.ToString("D") + "' AND a.Disposition=N'Quarantine';")));
                    transaction.Save("BeforeQaQuarantineDisposal");
                    QuarantineResolutionResult disposal = QuarantineResolutionService.Resolve(connection, transaction,
                        new QuarantineResolutionRequest { RequestId="qa-quarantine-dispose", ReturnLotAllocationId=quarantineAllocationId, ResolutionType=QuarantineResolutionType.Dispose, Quantity=1m, Reason="QA disposal rollback proof", DecidedBy=User }, businessDate);
                    Require(disposal.CostAmount == 6m && disposal.JournalEntryId.HasValue && Count(connection, transaction,
                        "SELECT COUNT(*) FROM dbo.JournalEntryLinesTb WHERE JournalEntryId=" + disposal.JournalEntryId.Value + " AND ((AccountCode=N'" + AccountingService.PurchasesAccount + "' AND Debit=6 AND Credit=0) OR (AccountCode=N'" + AccountingService.InventoryAccount + "' AND Credit=6 AND Debit=0));") == 2,
                        "إتلاف الحجر لم ينشئ قيد استبعاد القيمة المتوازن.");
                    transaction.Rollback("BeforeQaQuarantineDisposal");

                    QuarantineResolutionRequest releaseRequest = new QuarantineResolutionRequest { RequestId="qa-quarantine-release", ReturnLotAllocationId=quarantineAllocationId, ResolutionType=QuarantineResolutionType.ReleaseToStock, Quantity=1m, Reason="QA release proof", DecidedBy=User };
                    QuarantineResolutionResult release = QuarantineResolutionService.Resolve(connection, transaction, releaseRequest, businessDate);
                    QuarantineResolutionResult releaseDuplicate = QuarantineResolutionService.Resolve(connection, transaction, releaseRequest, businessDate);
                    Require(!release.IsDuplicate && releaseDuplicate.IsDuplicate && releaseDuplicate.EventId == release.EventId &&
                        Count(connection, transaction, "SELECT COUNT(*) FROM dbo.QuarantineResolutionEventsTb WHERE RequestId=N'qa-quarantine-release';") == 1,
                        "قرار الإفراج من الحجر لم يحافظ على تكرار الطلب الآمن.");

                    int documentCountBeforeFailure = Count(connection, transaction,
                        "SELECT COUNT(*) FROM dbo.CommercialReturnHeadersTb WHERE OriginalInvoiceId='" + sale.InvoiceId.ToString("D") + "';");
                    bool overReturnRejected = false;
                    try
                    {
                        CommercialSalesReturnRequest excessive = Request(sale.InvoiceId, lineId, allocationId,
                            "qa-return-excess", CommercialReturnDisposition.Damaged);
                        excessive.Lines = new[]
                        {
                            new CommercialSalesReturnRequestLine
                            {
                                OriginalLineId = lineId, Quantity = 2m,
                                Lots = new[] { new CommercialReturnLotRequest { OriginalSaleAllocationId = allocationId, Quantity = 2m, Disposition = CommercialReturnDisposition.Damaged } }
                            }
                        };
                        CommercialSalesReturnService.Issue(connection, transaction, excessive, businessDate);
                    }
                    catch (InvalidOperationException) { overReturnRejected = true; }
                    Require(overReturnRejected && Count(connection, transaction,
                        "SELECT COUNT(*) FROM dbo.CommercialReturnHeadersTb WHERE OriginalInvoiceId='" + sale.InvoiceId.ToString("D") + "';") == documentCountBeforeFailure,
                        "تجاوز الكمية لم يُرفض نظيفاً أو ترك مستنداً جزئياً.");

                    VerifyInjectedFailureRollsBack(connection, transaction, sale.InvoiceId, lineId, allocationId, businessDate);

                    Guid periodId = Guid.NewGuid();
                    Execute(connection, transaction,
                        "INSERT dbo.AccountingPeriodsTb(PeriodId,StartDate,EndDate,Status,ClosedAtUtc,ClosedBy,Notes) VALUES('" +
                        periodId.ToString("D") + "','" + businessDate.ToString("yyyyMMdd") + "','" + businessDate.ToString("yyyyMMdd") +
                        "',N'Closed',SYSUTCDATETIME(),N'" + User + "',N'QA rollback verification');");
                    bool closedPeriodRejected = false;
                    try
                    {
                        CommercialSalesReturnService.Issue(connection, transaction,
                            Request(sale.InvoiceId, lineId, allocationId, "qa-return-closed", CommercialReturnDisposition.Damaged), businessDate);
                    }
                    catch (InvalidOperationException) { closedPeriodRejected = true; }
                    Require(closedPeriodRejected, "الفترة المقفلة لم تمنع اعتماد مرتجع جديد.");

                    bool closedSaleRejected=false;
                    try { AccountingService.PostSalesInvoice(connection,transaction,"qa-closed-sales",sale.Totals,"QA closed-period sales",0m,businessDate); }
                    catch(InvalidOperationException) { closedSaleRejected=true; }
                    bool closedPurchaseRejected=false;
                    try { AccountingService.PostPurchaseInvoice(connection,transaction,"qa-closed-purchase",10m,"QA closed-period purchase",businessDate); }
                    catch(InvalidOperationException) { closedPurchaseRejected=true; }
                    bool closedVoucherRejected=false;
                    try
                    {
                        AccountingService.CreateJournalEntry(connection,transaction,businessDate,"Voucher","qa-closed-voucher","QA closed-period voucher",User,
                            new[] { new JournalLineInput { AccountCode=AccountingService.CashAccount,Debit=10m }, new JournalLineInput { AccountCode=AccountingService.CustomersAccount,Credit=10m } });
                    }
                    catch(InvalidOperationException) { closedVoucherRejected=true; }
                    Require(closedSaleRejected && closedPurchaseRejected && closedVoucherRejected &&
                        Count(connection,transaction,"SELECT COUNT(*) FROM dbo.JournalEntriesTb WHERE SourceId IN(N'qa-closed-sales',N'qa-closed-purchase',N'qa-closed-voucher');")==0,
                        "الفترة المقفلة لم تمنع جميع مسارات القيود الجديدة أو تركت أثراً جزئياً.");
                    // إعادة محاولة مصدر سبق ترحيله تبقى آمنة حتى بعد إقفال تاريخه.
                    AccountingService.PostSalesInvoice(connection,transaction,sale.InvoiceId.ToString("D"),sale.Totals,"QA idempotent retry in closed period",sale.CostOfGoods,businessDate);
                    Execute(connection, transaction, "DELETE dbo.AccountingPeriodsTb WHERE PeriodId='" + periodId.ToString("D") + "';");

                    CommercialSalesReturnRequest completingRequest = Request(sale.InvoiceId, lineId, allocationId,
                        "qa-return-complete", CommercialReturnDisposition.Restock);
                    CommercialSalesReturnResult completingReturn = CommercialSalesReturnService.Issue(
                        connection, transaction, completingRequest, businessDate);
                    Require(!completingReturn.IsDuplicate && completingReturn.Totals.NetTotal == 11.50m && completingReturn.ReturnedCost == 6m &&
                        Convert.ToDecimal(Scalar(connection, transaction,
                            "SELECT ISNULL(SUM(a.Quantity),0) FROM dbo.CommercialReturnLotAllocationsTb a JOIN dbo.CommercialReturnLinesTb l ON l.ReturnLineId=a.ReturnLineId WHERE l.OriginalLineId='" + lineId.ToString("D") + "';")) == 3m,
                        "إكمال المرتجع لم يستنفد الكمية المتبقية من سطر البيع بالقيمة والتكلفة الصحيحتين.");
                    foreach (Guid cashRefundInvoiceId in new[] { first.ReturnInvoiceId, completingReturn.ReturnInvoiceId })
                    {
                        decimal cashRefund = Convert.ToDecimal(Scalar(connection, transaction,
                            "SELECT ISNULL(SUM(l.Credit-l.Debit),0) FROM dbo.JournalEntriesTb e JOIN dbo.JournalEntryLinesTb l ON l.JournalEntryId=e.JournalEntryId WHERE e.SourceType=N'CommercialSalesReturn' AND e.SourceId='" + cashRefundInvoiceId.ToString("D") + "' AND l.AccountCode=N'" + AccountingService.CashAccount + "';"));
                        Require(cashRefund == 11.50m, "قيد رد المبلغ النقدي لا يطابق صافي إشعار المرتجع.");
                    }

                    Require(Count(connection, transaction,
                        "SELECT COUNT(*) FROM dbo.CommercialReturnHeadersTb WHERE OriginalInvoiceId='" + sale.InvoiceId.ToString("D") + "';") == 3,
                        "عدد مستندات المرتجع المعتمدة لا يساوي المرتجعين الجزئيين وطلب إكمال الكمية.");
                    Require(Count(connection, transaction,
                        "SELECT COUNT(*) FROM dbo.JournalEntriesTb WHERE (SourceType=N'SalesInvoice' AND SourceId='" + sale.InvoiceId.ToString("D") +
                        "') OR (SourceType=N'CommercialSalesReturn' AND SourceId IN ('" + first.ReturnInvoiceId.ToString("D") + "','" + second.ReturnInvoiceId.ToString("D") + "','" + completingReturn.ReturnInvoiceId.ToString("D") + "'));") == 4,
                        "عدد القيود لا يطابق البيع والمرتجعات الثلاثة.");
                    Require(Count(connection, transaction, @"SELECT COUNT(*) FROM
(SELECT e.JournalEntryId FROM dbo.JournalEntriesTb e JOIN dbo.JournalEntryLinesTb l ON l.JournalEntryId=e.JournalEntryId
 WHERE (e.SourceType=N'SalesInvoice' AND e.SourceId='" + sale.InvoiceId.ToString("D") + @"')
 OR (e.SourceType=N'CommercialSalesReturn' AND e.SourceId IN ('" + first.ReturnInvoiceId.ToString("D") + "','" + second.ReturnInvoiceId.ToString("D") + "','" + completingReturn.ReturnInvoiceId.ToString("D") + @"'))
 GROUP BY e.JournalEntryId HAVING SUM(l.Debit)<>SUM(l.Credit)) x;") == 0,
                        "أحد قيود البيع أو المرتجع غير متوازن.");
                    Require(Convert.ToInt32(Scalar(connection, transaction,
                        "SELECT MedQty FROM dbo.MedicineTb1 WHERE MedName=N'" + Medicine + "';")) == 3,
                        "الرصيد المتاح يجب أن يضم المرتجعات الصالحة والكمية المفرج عنها بعد إكمال المرتجع.");
                    Require(Convert.ToDecimal(Scalar(connection, transaction,
                        "SELECT QuantityRemaining FROM dbo.InventoryLotsTb WHERE MedName=N'" + Medicine + "';")) == 3m,
                        "رصيد الدفعة المتاح لا يطابق كامل المرتجع الصالح والإفراج من الحجر.");
                    Require(Count(connection, transaction,
                        "SELECT COUNT(*) FROM dbo.InventoryMovementsTb WHERE MedName=N'" + Medicine + "' AND SourceType=N'CommercialSalesInvoice';") == 1 &&
                        Count(connection, transaction,
                        "SELECT COUNT(*) FROM dbo.InventoryMovementsTb WHERE MedName=N'" + Medicine + "' AND SourceType=N'CommercialSalesReturn';") == 2 &&
                        Count(connection, transaction,
                        "SELECT COUNT(*) FROM dbo.InventoryMovementsTb WHERE MedName=N'" + Medicine + "' AND SourceType=N'QuarantineRelease';") == 1,
                        "حركات المخزون يجب أن تسجل البيع والمرتجعات الصالحة والإفراج المعتمد من الحجر.");
                    VerifyReports(connection, transaction, businessDate, reportBefore);
                }
                finally
                {
                    SessionContext.Clear();
                    try { transaction.Rollback(); } catch { }
                    transaction.Dispose();
                }
            }

            using (SqlConnection verification = new SqlConnection(connectionString))
            {
                verification.Open();
                Require(Count(verification, null, "SELECT COUNT(*) FROM dbo.MedicineTb1 WHERE MedName=N'" + Medicine + "';") == beforeMarker &&
                    Count(verification, null, "SELECT COUNT(*) FROM dbo.CommercialReturnHeadersTb WHERE RequestId LIKE N'qa-return-%';") == 0 &&
                    Count(verification, null, "SELECT COUNT(*) FROM dbo.CustomersBillsTb WHERE CustomerId=N'" + Customer + "';") == 0 &&
                    Count(verification, null, "SELECT COUNT(*) FROM dbo.CustomerAccountLedgerTb WHERE CustomerId=N'" + Customer + "';") == 0 &&
                    Count(verification, null, "SELECT COUNT(*) FROM dbo.SalesInvoicePaymentsTb p JOIN dbo.SalesInvoicesTb h ON h.InvoiceId=p.InvoiceId WHERE h.CustomerId=N'" + Customer + "';") == 0 &&
                    Count(verification, null, "SELECT COUNT(*) FROM dbo.QuarantineResolutionEventsTb WHERE RequestId LIKE N'qa-quarantine-%';") == 0 &&
                    Count(verification, null, "SELECT COUNT(*) FROM dbo.AccountingPeriodsTb WHERE Notes=N'QA managed-period rollback verification';") == 0,
                    "ترك اختبار المرتجع بيانات دائمة بعد التراجع.");
            }
        }

        internal static void PrepareOwnedPrintFixture(string serverName, string databaseName, string ownerToken, int databaseId, string confirmation)
        {
            if (confirmation != "--confirm-owned-print-fixture-writes")
                throw new UnauthorizedAccessException("Explicit owned print fixture write confirmation is required.");
            using (var connection = OpenOwnedPrintDatabase(serverName, databaseName, ownerToken, databaseId))
            {
                SessionContext.Start(User, "Manager");
                try
                {
                    using (var transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                    {
                        Require(Count(connection, transaction, "SELECT COUNT(*) FROM dbo.MedicineTb1 WHERE MedName=N'" + Medicine + "';") == 0,
                            "Print fixture already exists; do not seed twice.");
                        PrepareIsolatedFixture(connection, transaction);
                        DateTime date = new DateTime(2026, 10, 18, 12, 34, 56, DateTimeKind.Utc);
                        var sale = CommercialSalesRepository.Issue(connection, transaction, new CommercialSaleDraft
                        {
                            CurrencyCode = "SAR", ExchangeRate = 1m, IssuedBy = User,
                            CustomerId = Customer, CustomerName = "QA print customer",
                            Notes = "SYNTHETIC RETURN PRINT ACCEPTANCE ONLY",
                            Lines = new[] { new CommercialInvoiceLine { MedicineName = Medicine, Quantity = 3m, UnitPrice = 10m,
                                LineDiscount = 0m, TaxRatePercent = 15m, PriceTaxMode = CommercialPriceTaxMode.Exclusive } }
                        }, date);
                        AccountingService.PostSalesInvoice(connection, transaction, sale.InvoiceId.ToString("D"), sale.Totals,
                            "Synthetic return print sale", sale.CostOfGoods, date);
                        Guid line = Guid.Parse(Convert.ToString(Scalar(connection, transaction, "SELECT LineId FROM dbo.SalesInvoiceLinesTb WHERE InvoiceId='" + sale.InvoiceId + "';")));
                        Guid allocation = Guid.Parse(Convert.ToString(Scalar(connection, transaction, "SELECT AllocationId FROM dbo.LotAllocationsTb WHERE InvoiceLineId='" + line + "' AND MovementType=N'Sale';")));
                        var cash = Request(sale.InvoiceId, line, allocation, "qa-print-cash-" + ownerToken, CommercialReturnDisposition.Restock);
                        cash.ReturnReason = "اختبار طباعة معزول — إعادة صالحة للمخزون";
                        var first = CommercialSalesReturnService.Issue(connection, transaction, cash, date);
                        var credit = Request(sale.InvoiceId, line, allocation, "qa-print-credit-" + ownerToken, CommercialReturnDisposition.Quarantine, "Credit");
                        credit.ReturnReason = "اختبار طباعة معزول — رصيد عميل وحجر";
                        var second = CommercialSalesReturnService.Issue(connection, transaction, credit, date.AddMinutes(1));
                        Require(!first.IsDuplicate && !second.IsDuplicate && first.Totals.NetTotal == 11.50m && second.Totals.NetTotal == 11.50m,
                            "Saved print documents do not match the expected synthetic amounts.");
                        Require(Count(connection, transaction, @"SELECT COUNT(*) FROM (SELECT e.JournalEntryId FROM dbo.JournalEntriesTb e
JOIN dbo.JournalEntryLinesTb l ON l.JournalEntryId=e.JournalEntryId WHERE e.SourceId IN ('" + sale.InvoiceId + "','" + first.ReturnInvoiceId + "','" + second.ReturnInvoiceId + @"')
GROUP BY e.JournalEntryId HAVING SUM(l.Debit)<>SUM(l.Credit)) q;") == 0, "Print fixture journal is unbalanced.");
                        transaction.Commit();
                        Console.WriteLine("PASS owned print fixture: InvoiceId={0}; CashReturnId={1}; CreditReturnId={2}; each return=11.50 SAR; UTC=2026-10-18; committed only to owned QA.", sale.InvoiceId, first.ReturnId, second.ReturnId);
                    }
                }
                finally { SessionContext.Clear(); }
            }
        }

        private static SqlConnection OpenOwnedPrintDatabase(string serverName, string databaseName, string ownerToken, int databaseId)
        {
            if (databaseId <= 0 ||
                !System.Text.RegularExpressions.Regex.IsMatch(ownerToken ?? string.Empty, @"\A[a-f0-9]{32}\z") ||
                !System.Text.RegularExpressions.Regex.IsMatch(databaseName ?? string.Empty, @"\APharmacyReturnPrint_[0-9]{14}_[a-f0-9]{8}_qa\z") ||
                !databaseName.EndsWith("_" + ownerToken.Substring(0, 8) + "_qa", StringComparison.Ordinal))
                throw new UnauthorizedAccessException("Owned return-print fixture identity and explicit confirmation are required.");
            var connection = new SqlConnection(Database.BuildSettingsConnectionString(serverName, databaseName, true));
            try
            {
                connection.Open();
                using (var guard = new SqlCommand("SELECT DB_ID(),CONVERT(nvarchar(64),value) FROM sys.extended_properties WHERE class=0 AND name=N'QaTest.Owner';", connection))
                using (var reader = guard.ExecuteReader())
                    if (!reader.Read() || Convert.ToInt32(reader.GetValue(0)) != databaseId || reader.GetString(1) != ownerToken)
                        throw new UnauthorizedAccessException("Return-print sandbox ownership changed.");
                return connection;
            }
            catch { connection.Dispose(); throw; }
        }

        internal static async Task VerifyOwnedPrintPdfAsync(string serverName, string databaseName, string ownerToken, int databaseId)
        {
            var identities = new List<Tuple<Guid, Guid>>();
            using (var connection = OpenOwnedPrintDatabase(serverName, databaseName, ownerToken, databaseId))
            using (var command = new SqlCommand("SELECT OriginalInvoiceId,ReturnId FROM dbo.CommercialReturnHeadersTb WHERE RequestId IN(@Cash,@Credit) ORDER BY IssuedAtUtc;", connection))
            {
                command.Parameters.AddWithValue("@Cash", "qa-print-cash-" + ownerToken);
                command.Parameters.AddWithValue("@Credit", "qa-print-credit-" + ownerToken);
                using (var reader = command.ExecuteReader()) while (reader.Read()) identities.Add(Tuple.Create(reader.GetGuid(0), reader.GetGuid(1)));
            }
            Require(identities.Count == 2, "Expected the two owned saved print notes.");
            string directory = System.IO.Path.Combine(@"D:\PharmacyQA\work", "return-pdf-" + ownerToken);
            if (System.IO.Directory.Exists(directory)) throw new System.IO.IOException("PDF evidence directory must be fresh.");
            System.IO.Directory.CreateDirectory(directory);
            var repository = new PharmacyManagement.Infrastructure.SqlServer.SqlCommercialReturnReadRepository(
                () => new SqlConnection(Database.BuildSettingsConnectionString(serverName, databaseName, true)));
            var previous = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("ar-SA");
                foreach (var identity in identities)
                {
                    var snapshot = await repository.LoadDocumentAsync(new PharmacyManagement.Application.CommercialReturnDocumentReadRequest(identity.Item1, identity.Item2), CancellationToken.None);
                    var print = CommercialReturnDesktopPresentation.ToPrintDocument(snapshot);
                    Require(print.Header.IssuedAtUtc.Year == 2026 && print.Lines.Sum(line => line.NetTotal) == 11.50m, "Saved note values changed before PDF.");
                    foreach (bool english in new[] { false, true })
                    using (var rows = CommercialReturnPrintData.BuildItemTable(print.Header, print.Lines, english))
                    {
                        string details = CommercialReturnPrintData.BuildDocumentDetails(print.Header, english);
                        Require(details.Contains("2026-10-18 12:"), "Return details changed to the ar-SA calendar.");
                        var options = new ReportPrintOptions { Orientation = ReportPageOrientation.Portrait };
                        using (var layout = new DataTablePrintPreview.TablePrintLayout(english ? "Credit note" : "إشعار دائن",
                            CommercialReturnPrintData.BuildSubtitle(print.Header, english), rows, !english, options, details))
                        using (var document = new System.Drawing.Printing.PrintDocument())
                        {
                            document.DefaultPageSettings.Landscape = DataTablePrintPreview.ShouldUseLandscape(options, rows.Columns.Count);
                            document.PrintPage += layout.PrintPage;
                            string path = System.IO.Path.Combine(directory, "return-" + print.Header.ReturnInvoiceNumber + (english ? "-EN.pdf" : "-AR.pdf"));
                            if (!await PdfExportHelper.WritePdfCompleteFileAsync(path, temp => PdfExportHelper.PrintReportPdf(document, temp, "Owned QA credit note"), null, 30000, english))
                                throw new System.IO.IOException("Saved return PDF was not committed.");
                            System.IO.File.WriteAllText(path + ".expected.txt", CommercialReturnPrintData.BuildSubtitle(print.Header, english) + "\n" + details + "\nNetTotal=11.50 SAR", System.Text.Encoding.UTF8);
                            Console.WriteLine("Saved return PDF: " + path);
                        }
                    }
                }
            }
            finally { Thread.CurrentThread.CurrentCulture = previous; }
            Console.WriteLine("PASS saved return PDF producer: two persisted notes, Arabic/English, Gregorian details under ar-SA; production SQL reader, layout and Windows PDF printer; no SQL writes or native UI acceptance.");
        }

        internal static void RunCommittedConcurrency(string serverName, string databaseName, string confirmation)
        {
            string normalized=ValidateCommittedSandbox(databaseName,confirmation);

            string connectionString=Database.BuildSettingsConnectionString(serverName,normalized,true);
            Guid invoiceId; Guid lineId; Guid allocationId; DateTime businessDate=DateTime.UtcNow.Date.AddHours(12);
            SessionContext.Start(User,"Manager");
            try
            {
                using(SqlConnection c=new SqlConnection(connectionString))
                {
                    c.Open();
                    EnsureOwnedCommittedSandbox(c);
                    Require(Count(c,null,"SELECT COUNT(*) FROM dbo.MedicineTb1 WHERE MedName=N'"+Medicine+"';")==0,
                        "قاعدة التزامن المؤقتة تحتوي fixture سابقاً.");
                    using(SqlTransaction t=c.BeginTransaction(IsolationLevel.Serializable))
                    {
                        PrepareIsolatedFixture(c,t);
                        CommercialSaleResult sale=CommercialSalesRepository.Issue(c,t,new CommercialSaleDraft
                        {
                            CurrencyCode="SAR",ExchangeRate=1m,IssuedBy=User,Notes="temporary concurrency sandbox",
                            Lines=new[] { new CommercialInvoiceLine { MedicineName=Medicine,Quantity=3m,UnitPrice=10m,LineDiscount=0m,TaxRatePercent=15m,PriceTaxMode=CommercialPriceTaxMode.Exclusive } }
                        },businessDate);
                        AccountingService.PostSalesInvoice(c,t,sale.InvoiceId.ToString("D"),sale.Totals,"temporary concurrency sale",sale.CostOfGoods,businessDate);
                        invoiceId=sale.InvoiceId;
                        lineId=Guid.Parse(Convert.ToString(Scalar(c,t,"SELECT LineId FROM dbo.SalesInvoiceLinesTb WHERE InvoiceId='"+invoiceId.ToString("D")+"';")));
                        allocationId=Guid.Parse(Convert.ToString(Scalar(c,t,"SELECT AllocationId FROM dbo.LotAllocationsTb WHERE InvoiceLineId='"+lineId.ToString("D")+"' AND MovementType=N'Sale';")));
                        t.Commit();
                    }
                }

                ManualResetEventSlim start=new ManualResetEventSlim(false);
                Func<string,Task<ConcurrencyOutcome>> issue=requestId=>Task.Run(()=>
                {
                    start.Wait();
                    using(SqlConnection c=new SqlConnection(connectionString))
                    {
                        c.Open(); using(SqlTransaction t=c.BeginTransaction(IsolationLevel.Serializable))
                        {
                            try
                            {
                                CommercialSalesReturnRequest request=ConcurrencyRequest(invoiceId,lineId,allocationId,requestId);
                                CommercialSalesReturnResult result=CommercialSalesReturnService.Issue(c,t,request,businessDate);
                                t.Commit(); return new ConcurrencyOutcome { Succeeded=true,ReturnId=result.ReturnId };
                            }
                            catch(InvalidOperationException ex) { try { t.Rollback(); } catch { } return new ConcurrencyOutcome { Error=ex.Message }; }
                        }
                    }
                });
                Task<ConcurrencyOutcome>[] tasks={issue("qa-concurrency-a"),issue("qa-concurrency-b")};
                start.Set(); Task.WaitAll(tasks);
                ConcurrencyOutcome[] outcomes=tasks.Select(x=>x.Result).ToArray();
                Require(outcomes.Count(x=>x.Succeeded)==1 && outcomes.Count(x=>!x.Succeeded)==1,
                    "طلبا المرتجع المتزامنان لم ينتجا نجاحاً واحداً ورفضاً واحداً.");
                using(SqlConnection verification=new SqlConnection(connectionString))
                {
                    verification.Open();
                    Require(Count(verification,null,"SELECT COUNT(*) FROM dbo.CommercialReturnHeadersTb WHERE RequestId IN(N'qa-concurrency-a',N'qa-concurrency-b');")==1 &&
                        Convert.ToInt32(Scalar(verification,null,"SELECT MedQty FROM dbo.MedicineTb1 WHERE MedName=N'"+Medicine+"';"))==2 &&
                        Count(verification,null,"SELECT COUNT(*) FROM dbo.JournalEntriesTb WHERE SourceType=N'CommercialSalesReturn';")==1,
                        "الحالة النهائية بعد التزامن لا تطابق مرتجعاً واحداً مع رصيد وقيد واحدين.");
                }
            }
            finally { SessionContext.Clear(); }
        }

        private static string ValidateCommittedSandbox(string databaseName, string confirmation)
        {
            if (!string.Equals(confirmation, ConcurrencyConfirmation, StringComparison.Ordinal))
                throw new InvalidOperationException("Explicit temporary concurrency write confirmation is required.");
            string name = (databaseName ?? string.Empty).Trim();
            if (!System.Text.RegularExpressions.Regex.IsMatch(name, @"\APharmacyConcurrency_[0-9]{14}_[a-f0-9]{8}_sandbox\z"))
                throw new InvalidOperationException("Committed return QA requires an exact owned concurrency sandbox name.");
            return name;
        }

        private static void EnsureOwnedCommittedSandbox(SqlConnection connection)
        {
            string owner = Convert.ToString(Scalar(connection, null, "SELECT CONVERT(nvarchar(64),value) FROM sys.extended_properties WHERE class=0 AND name=N'QaTest.Owner';"));
            Require(System.Text.RegularExpressions.Regex.IsMatch(owner, @"\A[a-f0-9]{32}\z"), "Committed return QA sandbox ownership is missing.");
        }

        private static CommercialSalesReturnRequest ConcurrencyRequest(Guid invoiceId, Guid lineId, Guid allocationId, string requestId)
        {
            var request = Request(invoiceId, lineId, allocationId, requestId, CommercialReturnDisposition.Restock);
            request.Lines = new[] { new CommercialSalesReturnRequestLine { OriginalLineId=lineId, Quantity=2m,
                Lots=new[] { new CommercialReturnLotRequest { OriginalSaleAllocationId=allocationId, Quantity=2m, Disposition=CommercialReturnDisposition.Restock } } } };
            return request;
        }

        // The harness invokes this in a fresh process after the committed race has exited.
        internal static void RunCommittedReplay(string serverName, string databaseName, string confirmation)
        {
            string name = ValidateCommittedSandbox(databaseName, confirmation);
            string connectionString = Database.BuildSettingsConnectionString(serverName, name, true);
            SessionContext.Start(User, "Manager");
            try
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open(); EnsureOwnedCommittedSandbox(connection);
                    Guid returnId, returnInvoiceId, invoiceId, lineId, allocationId; string requestId; DateTime businessDate;
                    const string query = @"SELECT h.ReturnId,h.ReturnInvoiceId,h.OriginalInvoiceId,h.RequestId,h.IssuedAtUtc,l.OriginalLineId,a.OriginalSaleAllocationId
FROM dbo.CommercialReturnHeadersTb h JOIN dbo.CommercialReturnLinesTb l ON l.ReturnId=h.ReturnId
JOIN dbo.CommercialReturnLotAllocationsTb a ON a.ReturnLineId=l.ReturnLineId
JOIN dbo.SalesInvoiceLinesTb original ON original.LineId=l.OriginalLineId
WHERE h.RequestId IN(N'qa-concurrency-a',N'qa-concurrency-b') AND original.MedName=@Medicine;";
                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.Add("@Medicine", SqlDbType.NVarChar, 200).Value = Medicine;
                        using (var reader = command.ExecuteReader())
                        {
                            Require(reader.Read(), "Committed return fixture is missing in the fresh process.");
                            returnId=reader.GetGuid(0); returnInvoiceId=reader.GetGuid(1); invoiceId=reader.GetGuid(2); requestId=reader.GetString(3);
                            businessDate=DateTime.SpecifyKind(reader.GetDateTime(4),DateTimeKind.Utc); lineId=reader.GetGuid(5); allocationId=reader.GetGuid(6);
                            Require(!reader.Read(), "Committed return fixture is ambiguous.");
                        }
                    }
                    int tableCount;
                    string before = DatabaseRowsFingerprint(connection, out tableCount);
                    using (var transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                    {
                        var duplicate = CommercialSalesReturnService.Issue(connection, transaction, ConcurrencyRequest(invoiceId,lineId,allocationId,requestId), businessDate);
                        Require(duplicate.IsDuplicate && duplicate.ReturnId==returnId && duplicate.ReturnInvoiceId==returnInvoiceId && duplicate.Totals.NetTotal==23m && duplicate.ReturnedCost==12m,
                            "Retry from a new process did not return the original committed result.");
                        transaction.Commit();
                    }
                    int afterTables;
                    Require(DatabaseRowsFingerprint(connection,out afterTables)==before && afterTables==tableCount, "Duplicate committed retry mutated database rows.");
                    bool conflictRejected = false;
                    using (var transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                    {
                        var conflict = ConcurrencyRequest(invoiceId,lineId,allocationId,requestId);
                        conflict.Notes = "Changed payload after committed return";
                        try { CommercialSalesReturnService.Issue(connection,transaction,conflict,businessDate); }
                        catch (InvalidOperationException error) { conflictRejected=error.Message.Contains("بمحتوى مختلف"); }
                        transaction.Rollback();
                    }
                    Require(conflictRejected && DatabaseRowsFingerprint(connection,out afterTables)==before && afterTables==tableCount,
                        "A changed payload reused the committed request identity or altered database rows.");
                    Console.WriteLine("PASS committed return replay: fresh process, same IDs/totals, inert duplicate and conflicting payload rejected; database row fingerprint unchanged; tables="+tableCount+".");
                }
            }
            finally { SessionContext.Clear(); }
        }

        internal static void RunDurableRecovery(string serverName,string databaseName,string directory,string confirmation,bool replay,bool awaitTermination=false)
        {
            string name=ValidateCommittedSandbox(databaseName,confirmation);
            string source=Database.ConnectionString;
            var sourceBuilder=new SqlConnectionStringBuilder(source);
            Require(sourceBuilder.InitialCatalog==name&&sourceBuilder.DataSource==serverName,
                "Durable return QA requires a copied runtime pinned to the owned sandbox.");
            SessionContext.Start(User,"Manager");
            try
            {
                using(var connection=new SqlConnection(source))
                {
                    connection.Open();EnsureOwnedCommittedSandbox(connection);
                    string owner=Convert.ToString(Scalar(connection,null,"SELECT CONVERT(nvarchar(64),value) FROM sys.extended_properties WHERE class=0 AND name=N'QaTest.Owner';"));
                    string full=System.IO.Path.GetFullPath(directory);
                    Require(full.StartsWith(@"C:\",StringComparison.OrdinalIgnoreCase)&&
                        System.IO.Path.GetFileName(full)=="return-pending"&&
                        System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(full))=="qa-restore-"+owner,
                        "Durable return output is not the owned C: run directory.");
                    Guid invoiceId,lineId,allocationId;
                    using(var command=new SqlCommand(@"SELECT i.InvoiceId,l.LineId,a.AllocationId
FROM dbo.SalesInvoicesTb i JOIN dbo.SalesInvoiceLinesTb l ON l.InvoiceId=i.InvoiceId
JOIN dbo.LotAllocationsTb a ON a.InvoiceLineId=l.LineId AND a.MovementType=N'Sale'
WHERE i.InvoiceType=N'Sale' AND i.Status=N'Issued' AND l.MedName=@Medicine;",connection))
                    {
                        command.Parameters.Add("@Medicine",SqlDbType.NVarChar,200).Value=Medicine;
                        using(var reader=command.ExecuteReader())
                        {
                            Require(reader.Read(),"Durable return fixture is absent.");
                            invoiceId=reader.GetGuid(0);lineId=reader.GetGuid(1);allocationId=reader.GetGuid(2);
                            Require(!reader.Read(),"Durable return fixture is ambiguous.");
                        }
                    }
                    using(var store=new CommercialReturnPendingStore(full,SalesWriteSourceFingerprint.Compute(source),User,"Manager",invoiceId))
                    {
                        if(!replay)
                        {
                            Require(store.Load()==null,"Durable return attempt already exists.");
                            var request=Request(invoiceId,lineId,allocationId,Guid.NewGuid().ToString("N"),CommercialReturnDisposition.Restock);
                            DateTime date=DateTime.UtcNow.Date.AddHours(12);
                            var attempt=new CommercialReturnPendingAttempt(SalesWriteSourceFingerprint.Compute(source),User,"Manager",date,request);
                            store.Save(attempt);
                            var result=CommercialSalesReturnService.Issue(attempt.Snapshot(),date,source,SessionContext.CurrentSessionRevision);
                            Require(!result.IsDuplicate&&result.Totals.NetTotal==11.50m&&result.ReturnedCost==6m&&store.Load()!=null,
                                "Durable return did not persist before a real committed submission.");
                            // Synthetic pre-existing closed state, not acceptance of the period-close workflow.
                            using(var command=new SqlCommand(@"INSERT dbo.AccountingPeriodsTb
(PeriodId,StartDate,EndDate,Status,ClosedAtUtc,ClosedBy,Notes)
VALUES(NEWID(),@Date,@Date,N'Closed',SYSUTCDATETIME(),@By,N'QA durable return closed-period fixture');",connection))
                            {
                                command.Parameters.Add("@Date",SqlDbType.Date).Value=date.Date;
                                command.Parameters.Add("@By",SqlDbType.NVarChar,200).Value=User;
                                command.ExecuteNonQuery();
                            }
                            if(awaitTermination)
                            {
                                // The parent stops only this owned QA process while the durable
                                // store lease and SQL connection are still inside their using scopes.
                                using(var process=System.Diagnostics.Process.GetCurrentProcess())
                                {
                                    string checkpoint=System.IO.Path.Combine(full,"process-stop-ready.json");
                                    string temporary=checkpoint+"."+Guid.NewGuid().ToString("N")+".tmp";
                                    string json="{\"Owner\":\""+owner+"\",\"Database\":\""+name+"\",\"ProcessId\":"+
                                        process.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)+
                                        ",\"StartedUtcTicks\":"+process.StartTime.ToUniversalTime().Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture)+
                                        ",\"RequestId\":\""+request.RequestId+"\",\"ReturnId\":\""+result.ReturnId+"\",\"ReturnInvoiceId\":\""+result.ReturnInvoiceId+"\",\"Phase\":\"AfterCommitBeforeStoreDispose\"}";
                                    using(var stream=new System.IO.FileStream(temporary,System.IO.FileMode.CreateNew,System.IO.FileAccess.Write,System.IO.FileShare.None,4096,System.IO.FileOptions.WriteThrough))
                                    {
                                        byte[] bytes=new System.Text.UTF8Encoding(false).GetBytes(json);
                                        stream.Write(bytes,0,bytes.Length);stream.Flush(true);
                                    }
                                    System.IO.File.Move(temporary,checkpoint);
                                }
                                Console.WriteLine("READY owned return interruption: committed receipt, encrypted intent and exclusive store lease retained; awaiting parent termination.");
                                Console.Out.Flush();
                                System.Threading.Thread.Sleep(60000);
                                throw new TimeoutException("The QA parent did not stop the ready return process.");
                            }
                            Console.WriteLine("PASS durable return write: encrypted request saved before SQL; committed receipt retained on disk across process exit; synthetic closed-period fixture prepared.");
                        }
                        else
                        {
                            var attempt=store.Load();Require(attempt!=null,"Durable return was not recovered from disk.");
                            Guid expectedReturn,expectedInvoice;
                            using(var command=new SqlCommand("SELECT ReturnId,ReturnInvoiceId FROM dbo.CommercialReturnHeadersTb WHERE RequestId=@Request;",connection))
                            {
                                command.Parameters.Add("@Request",SqlDbType.NVarChar,100).Value=attempt.Snapshot().RequestId;
                                using(var reader=command.ExecuteReader())
                                {Require(reader.Read(),"Committed return receipt is absent.");expectedReturn=reader.GetGuid(0);expectedInvoice=reader.GetGuid(1);Require(!reader.Read(),"Ambiguous return receipt.");}
                            }
                            int tables;string before=DatabaseRowsFingerprint(connection,out tables);
                            var result=CommercialSalesReturnService.Issue(attempt.Snapshot(),attempt.BusinessDateUtc,source,SessionContext.CurrentSessionRevision);
                            Require(result.IsDuplicate&&result.ReturnId==expectedReturn&&result.ReturnInvoiceId==expectedInvoice&&result.Totals.NetTotal==11.50m&&result.ReturnedCost==6m,
                                "Recovered disk request did not return the original committed receipt.");
                            string checkpoint=System.IO.Path.Combine(full,"process-stop-ready.json");
                            if(System.IO.File.Exists(checkpoint))
                            {
                                string original=System.IO.File.ReadAllText(checkpoint,System.Text.Encoding.UTF8);
                                Require(original.Contains("\"RequestId\":\""+attempt.Snapshot().RequestId+"\"")&&
                                    original.Contains("\"ReturnId\":\""+result.ReturnId+"\"")&&
                                    original.Contains("\"ReturnInvoiceId\":\""+result.ReturnInvoiceId+"\""),
                                    "Recovered receipt differs from the committed result captured before process termination.");
                            }
                            int afterTables;
                            Require(DatabaseRowsFingerprint(connection,out afterTables)==before&&afterTables==tables,"Recovered retry changed database rows.");
                            var newRequest=attempt.Snapshot();newRequest.RequestId=Guid.NewGuid().ToString("N");
                            bool closedRejected=false;
                            try { CommercialSalesReturnService.Issue(newRequest,attempt.BusinessDateUtc,source,SessionContext.CurrentSessionRevision); }
                            catch(InvalidOperationException error) {closedRejected=error is CommercialReturnNotCommittedException&&error.Message.Contains("الفترة المحاسبية المختارة مقفلة");}
                            Require(closedRejected&&DatabaseRowsFingerprint(connection,out afterTables)==before,
                                "A new request bypassed the closed accounting period.");
                            store.RemoveVerified(attempt,result);Require(store.Load()==null,"Verified disk recovery was not resolved.");
                            Console.WriteLine("PASS durable return replay: new process loaded exact disk request, recovered original receipt after period closure, rejected a new request in that period, resolved file, "+tables+" database tables unchanged.");
                        }
                    }
                }
            }
            finally {SessionContext.Clear();}
        }

        private static string DatabaseRowsFingerprint(SqlConnection connection, out int tableCount)
        {
            var tables = new List<string>();
            using (var command = new SqlCommand("SELECT QUOTENAME(s.name)+N'.'+QUOTENAME(t.name) FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id WHERE t.is_ms_shipped=0 ORDER BY s.name,t.name;",connection))
            using (var reader = command.ExecuteReader()) while(reader.Read()) tables.Add(reader.GetString(0));
            tableCount=tables.Count; var content=new System.Text.StringBuilder();
            foreach(string table in tables)
            {
                string xml=Convert.ToString(Scalar(connection,null,"SELECT CONVERT(nvarchar(max),(SELECT * FROM "+table+" FOR XML RAW('r'),ROOT('rows'),BINARY BASE64,TYPE));"));
                if(string.IsNullOrEmpty(xml))
                {
                    Require(Count(connection,null,"SELECT COUNT(*) FROM "+table+";")==0, "Missing fingerprint XML for a populated table.");
                    content.Append(table).Append('\n');
                    continue;
                }
                var document=new System.Xml.XmlDocument(); document.LoadXml(xml);
                var rows=new List<string>(); foreach(System.Xml.XmlNode row in document.DocumentElement.ChildNodes) rows.Add(row.OuterXml);
                rows.Sort(StringComparer.Ordinal); content.Append(table).Append('\n'); foreach(string row in rows) content.Append(row).Append('\n');
            }
            using(var hash=System.Security.Cryptography.SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(content.ToString()))).Replace("-",string.Empty);
        }

        private static CommercialSalesReturnRequest Request(Guid invoiceId, Guid lineId, Guid allocationId, string requestId, CommercialReturnDisposition disposition, string refundMethod = "Cash")
        {
            return new CommercialSalesReturnRequest
            {
                OriginalInvoiceId = invoiceId, RequestId = requestId, RefundMethod = refundMethod,
                ReturnReason = "QA rollback verification", IssuedBy = User,
                Lines = new[]
                {
                    new CommercialSalesReturnRequestLine
                    {
                        OriginalLineId = lineId, Quantity = 1m,
                        Lots = new[] { new CommercialReturnLotRequest { OriginalSaleAllocationId = allocationId, Quantity = 1m, Disposition = disposition } }
                    }
                }
            };
        }

        private static void PrepareIsolatedFixture(SqlConnection connection, SqlTransaction transaction)
        {
            Execute(connection, transaction, @"
UPDATE dbo.CommercialOperationalSettingsTb SET SettingValue=N'FullCommercial',UpdatedAtUtc=SYSUTCDATETIME() WHERE SettingKey=N'Mode';
UPDATE dbo.SaudiFeatureSettingsTb SET FeatureMode=CASE WHEN FeatureKey=N'CommercialSales' THEN N'Test' WHEN FeatureKey=N'EInvoice' THEN N'Simulator' ELSE FeatureMode END,UpdatedAtUtc=SYSUTCDATETIME(),UpdatedBy=N'QA' WHERE FeatureKey IN(N'CommercialSales',N'EInvoice');
UPDATE dbo.CommercialEInvoiceSettingsTb SET SellerName=N'QA Pharmacy',SellerVatNumber=N'310000000000003',LocalSandboxEnabled=1,UpdatedAtUtc=SYSUTCDATETIME() WHERE SettingsId=1;
UPDATE m SET MedQty=CONVERT(int,ISNULL(x.QuantityRemaining,0)) FROM dbo.MedicineTb1 m OUTER APPLY(SELECT SUM(l.QuantityRemaining) QuantityRemaining FROM dbo.InventoryLotsTb l WHERE l.MedName=m.MedName)x;
INSERT dbo.MedicineTb1(MedName,Barcode,Bprice,Sprice,MedQty,ExpDate,CompName,ProdDate) VALUES(N'" + Medicine + @"',N'QA-RETURN-ROLLBACK',6,10,3,DATEADD(year,1,GETDATE()),N'QA',GETDATE());
INSERT dbo.CustomersBillsTb(CustomerId,CustomerName,PhoneNumber,CustomerAddress,TotalSpent,InvoiceDate,EmployeeName,InvoiceNotes) VALUES(N'" + Customer + @"',N'QA rollback customer',N'',N'',0,GETDATE(),N'" + User + @"',N'Rollback-only fixture');
INSERT dbo.InventoryLotsTb(LotId,MedName,BatchNumber,ExpiryDate,UnitCost,QuantityReceived,QuantityRemaining,IsRecalled) VALUES(NEWID(),N'" + Medicine + @"',N'QA-BATCH',DATEADD(year,1,CONVERT(date,GETDATE())),6,3,3,0);");
            Require(Convert.ToBoolean(Scalar(connection, transaction, @"
SELECT CASE WHEN EXISTS(SELECT 1 FROM dbo.MedicineTb1 m OUTER APPLY(SELECT ISNULL(SUM(l.QuantityRemaining),0) q FROM dbo.InventoryLotsTb l WHERE l.MedName=m.MedName)x WHERE CONVERT(decimal(18,3),m.MedQty)<>x.q) THEN 0 ELSE 1 END;")),
                "تعذر عزل رصيد الدفعات المؤقت للاختبار.");
        }

        private static Dictionary<string, decimal> ReadDailyReport(SqlConnection connection, SqlTransaction transaction, DateTime businessDate)
        {
            var values = new Dictionary<string, decimal>();
            foreach (string column in new[] { "فواتير البيع", "إشعارات المرتجع التجاري", "المبيعات دون الضريبة", "المبيعات المعكوسة دون الضريبة", "صافي المبيعات", "صافي التكلفة المسجلة", "صافي ضريبة المبيعات" })
                values.Add(column, 0m);
            using (SqlCommand command = new SqlCommand(DailyPharmacyPerformanceReport.Query, connection, transaction))
            {
                command.Parameters.Add("@FromDate", SqlDbType.DateTime2).Value = businessDate.Date;
                command.Parameters.Add("@ToDate", SqlDbType.DateTime2).Value = businessDate.Date.AddDays(1);
                command.Parameters.Add("@Take", SqlDbType.Int).Value = 10;
                command.Parameters.Add("@Search", SqlDbType.NVarChar, PharmacyManagement.Application.ReportSearchCriteria.MaximumLength).Value = string.Empty;
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    DataTable table = new DataTable();
                    table.Load(reader);
                    DataRow row = null;
                    foreach (DataRow candidate in table.Rows)
                        if (Convert.ToDateTime(candidate["اليوم"]).Date == businessDate.Date) { row = candidate; break; }
                    if (row != null)
                        foreach (string column in values.Keys.ToArray())
                            values[column] = Convert.ToDecimal(row[column]);
                    table.Dispose();
                }
            }
            return values;
        }

        private static void VerifyReports(SqlConnection connection, SqlTransaction transaction, DateTime businessDate, Dictionary<string, decimal> before)
        {
            Dictionary<string, decimal> after = ReadDailyReport(connection, transaction, businessDate);
            // Shared QA can already contain today's documents. Verify only this transaction's delta.
            var expected = new Dictionary<string, decimal>
            {
                { "فواتير البيع", 1m }, { "إشعارات المرتجع التجاري", 3m },
                { "المبيعات دون الضريبة", 30m }, { "المبيعات المعكوسة دون الضريبة", 30m },
                { "صافي المبيعات", 0m }, { "صافي التكلفة المسجلة", 0m }, { "صافي ضريبة المبيعات", 0m }
            };
            foreach (KeyValuePair<string, decimal> value in expected)
                Require(after[value.Key] - before[value.Key] == value.Value,
                    "تغير التقرير اليومي لا يطابق البيع والمرتجعين المعتمدين: " + value.Key);

            using (SqlCommand command = new SqlCommand(MedicineLookupService.BuildPopularQuery(true), connection, transaction))
            {
                command.Parameters.Add("@FromDate", SqlDbType.DateTime2).Value = businessDate.Date;
                command.Parameters.Add("@ToDate", SqlDbType.DateTime2).Value = businessDate.Date.AddDays(1);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        if (!string.Equals(reader["MedName"] as string, Medicine, StringComparison.Ordinal)) continue;
                        Require(Convert.ToDecimal(reader["NetSold"]) == 0m,
                            "ترتيب الأصناف لم يطرح كامل المرتجعات الصالحة والمحجورة من صافي البيع.");
                    }
                    // A fully returned item may be omitted from the positive-sales ranking.
                }
            }
        }

        private static void VerifyInjectedFailureRollsBack(
            SqlConnection connection, SqlTransaction transaction,
            Guid invoiceId, Guid lineId, Guid allocationId, DateTime businessDate)
        {
            const string savepoint = "BeforeQaInjectedReturnFailure";
            int headerCount = Count(connection, transaction,
                "SELECT COUNT(*) FROM dbo.CommercialReturnHeadersTb WHERE OriginalInvoiceId='" + invoiceId.ToString("D") + "';");
            int journalCount = Count(connection, transaction,
                "SELECT COUNT(*) FROM dbo.JournalEntriesTb WHERE SourceType=N'CommercialSalesReturn';");
            int aggregateQuantity = Convert.ToInt32(Scalar(connection, transaction,
                "SELECT MedQty FROM dbo.MedicineTb1 WHERE MedName=N'" + Medicine + "';"));
            decimal lotQuantity = Convert.ToDecimal(Scalar(connection, transaction,
                "SELECT QuantityRemaining FROM dbo.InventoryLotsTb WHERE MedName=N'" + Medicine + "';"));
            transaction.Save(savepoint);
            bool injectedFailureObserved = false;
            try
            {
                CommercialSalesReturnService.Issue(
                    connection, transaction,
                    Request(invoiceId, lineId, allocationId, "qa-return-injected-failure", CommercialReturnDisposition.Restock),
                    businessDate,
                    delegate { throw new InvalidOperationException("QA injected failure after inventory and before accounting."); });
            }
            catch (InvalidOperationException exception)
            {
                injectedFailureObserved = exception.Message.Contains("QA injected failure");
                transaction.Rollback(savepoint);
            }
            Require(injectedFailureObserved,
                "لم يصل اختبار الفشل إلى الحد الواقع بعد المخزون وقبل القيد.");
            Require(Count(connection, transaction,
                    "SELECT COUNT(*) FROM dbo.CommercialReturnHeadersTb WHERE OriginalInvoiceId='" + invoiceId.ToString("D") + "';") == headerCount &&
                Count(connection, transaction,
                    "SELECT COUNT(*) FROM dbo.JournalEntriesTb WHERE SourceType=N'CommercialSalesReturn';") == journalCount &&
                Convert.ToInt32(Scalar(connection, transaction,
                    "SELECT MedQty FROM dbo.MedicineTb1 WHERE MedName=N'" + Medicine + "';")) == aggregateQuantity &&
                Convert.ToDecimal(Scalar(connection, transaction,
                    "SELECT QuantityRemaining FROM dbo.InventoryLotsTb WHERE MedName=N'" + Medicine + "';")) == lotQuantity,
                "ترك الفشل المحقون مستنداً أو قيداً أو تغييراً في رصيد المخزون بعد التراجع.");
        }

        private static void VerifyCompetingSequenceLockWaits(
            SqlConnection ownerConnection, SqlTransaction ownerTransaction)
        {
            CommercialSalesRepository.LockInvoiceNumberSequence(ownerConnection, ownerTransaction);
            bool competingTransactionWaited = false;
            using (SqlConnection competitor = new SqlConnection(ownerConnection.ConnectionString))
            {
                competitor.Open();
                using (SqlTransaction competitorTransaction = competitor.BeginTransaction(IsolationLevel.Serializable))
                using (SqlCommand command = new SqlCommand(@"
SELECT NextValue FROM dbo.CommercialCountersTb WITH (UPDLOCK, HOLDLOCK)
WHERE CounterKey=N'SalesInvoice';", competitor, competitorTransaction))
                {
                    command.CommandTimeout = 2;
                    try { command.ExecuteScalar(); }
                    catch (SqlException exception) { competingTransactionWaited = exception.Number == -2; }
                    finally
                    {
                        try { competitorTransaction.Rollback(); } catch { }
                    }
                }
            }
            Require(competingTransactionWaited,
                "المعاملة المنافسة تجاوزت قفل عداد البيع والمرتجع بدلاً من انتظار العملية المالكة.");
        }

        private static int Count(SqlConnection connection, SqlTransaction transaction, string sql)
        {
            return Convert.ToInt32(Scalar(connection, transaction, sql));
        }

        private static object Scalar(SqlConnection connection, SqlTransaction transaction, string sql)
        {
            using (SqlCommand command = new SqlCommand(sql, connection, transaction))
            {
                command.CommandTimeout = 30;
                return command.ExecuteScalar();
            }
        }

        private static void Execute(SqlConnection connection, SqlTransaction transaction, string sql)
        {
            using (SqlCommand command = new SqlCommand(sql, connection, transaction))
            {
                command.CommandTimeout = 30;
                command.ExecuteNonQuery();
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private sealed class ConcurrencyOutcome
        {
            internal bool Succeeded;
            internal Guid ReturnId;
            internal string Error;
        }
    }
}

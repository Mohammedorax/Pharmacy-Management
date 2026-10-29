using System;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Text;
using PharmacyManagement.Application;

namespace project_fo_3
{
    internal static class QaPurchaseCancellationSelfTests
    {
        private const string Confirmation = "--confirm-qa-purchase-cancellation";
        private const string Actor = "QA purchase cancellation verifier";

        internal static void Run(string backupPath, string ownerToken, string confirmation)
        {
            if (!string.Equals(confirmation, Confirmation, StringComparison.Ordinal))
                throw new InvalidOperationException("يلزم تأكيد اختبار إلغاء المشتريات على نسخة QA مملوكة.");
            string expectedDatabase = QaSalesInvoiceSelfTests.RequireOwnedTemporaryQa(ownerToken);
            string medicine, supplier, currency;
            int initialStock, salePrice;
            object productionBefore, expiryBefore;
            decimal effectiveTax;
            bool commercialMode;
            using (SqlConnection connection = Database.CreateConnection())
            {
                connection.Open();
                Require(connection.Database == expectedDatabase, "اتصال الاختبار ليس بقاعدة QA المؤقتة المملوكة.");
                QaTaxPolicySelfTests.VerifyBackup(connection, backupPath);
                commercialMode = CommercialSchemaInstaller.IsFullCommercialMode(connection, null);
                string selectionSql = commercialMode
                    ? @"SELECT TOP(1) MedName,CompName,MedQty,ProdDate,ExpDate,Sprice FROM dbo.MedicineTb1
WHERE MedQty>0 AND Sprice>0 AND ProdDate<@NewProduction AND ExpDate<@NewExpiry AND ExpDate>@MinimumExpiry ORDER BY MedName;"
                    : @"SELECT TOP(1) MedName,CompName,MedQty,ProdDate,ExpDate,Sprice FROM dbo.MedicineTb1
WHERE MedQty>0 AND ProdDate<@NewProduction AND ExpDate<@NewExpiry ORDER BY MedName;";
                using (var command = new SqlCommand(selectionSql, connection))
                {
                    command.Parameters.Add("@NewProduction", SqlDbType.Date).Value = DateTime.Today.AddDays(-30);
                    command.Parameters.Add("@NewExpiry", SqlDbType.Date).Value = DateTime.Today.AddYears(2);
                    if (commercialMode) command.Parameters.Add("@MinimumExpiry", SqlDbType.Date).Value = DateTime.Today.AddDays(30);
                    using (var reader = command.ExecuteReader())
                    {
                        Require(reader.Read(), "لا يوجد صنف ملائم لاختبار الإلغاء دون تغيير ملخص التواريخ في QA.");
                        medicine = reader.GetString(0); supplier = reader.IsDBNull(1) ? "مورد QA" : reader.GetString(1);
                        initialStock = reader.GetInt32(2); productionBefore = reader.IsDBNull(3) ? (object)DBNull.Value : reader.GetDateTime(3);
                        expiryBefore = reader.IsDBNull(4) ? (object)DBNull.Value : reader.GetDateTime(4);
                        salePrice = Convert.ToInt32(reader[5]);
                    }
                }
                currency = Text(connection, "SELECT TOP(1) CurrencyCode FROM dbo.CurrencySettingsTb WHERE IsSystemCurrency=1 ORDER BY CurrencyId;");
                effectiveTax = AccountingService.GetEffectiveTaxRate(connection, null);
                Require(currency.Length > 0, "عملة النظام غير محددة في QA.");
            }

            SessionContext.Start(Actor, "Manager");
            try
            {
                var request = new PurchaseInvoiceRequest
                {
                    ImportCurrencyCode = currency,
                    ImportTaxRatePercent = effectiveTax,
                    ImportPaymentMethod = "Cash",
                    DesiredPurchaseId = "QA-CXL-" + Guid.NewGuid().ToString("N").Substring(0, 16).ToUpperInvariant(),
                    SupplierName = supplier,
                    EmployeeName = Actor,
                    ActorRole = "Manager",
                    Notes = "QA purchase cancellation round trip; temporary owned clone",
                    PurchaseDate = DateTime.Now.Date,
                    CommercialInventoryExpected = commercialMode,
                    Lines = new[] { new PurchaseInvoiceLineRequest
                    {
                        LineNumber=1, MedicineName=medicine, BuyingPrice=1, Quantity=1,
                        BatchNumber=commercialMode ? "QA-CXL-" + Guid.NewGuid().ToString("N") : string.Empty,
                        ProductionDate=DateTime.Today.AddDays(-30), ExpiryDate=DateTime.Today.AddYears(2)
                    } }
                };
                PurchaseInvoiceResult issued = new PurchaseInvoiceOperation(Database.CreateConnection).IssueForVerificationCommitted(request);
                Require(issued.TotalAmount == 1m && Stock(medicine) == initialStock + 1,
                    "اعتماد فاتورة QA لم يزد المخزون والإجمالي وفق المتوقع.");
                var cancelRequest = new PurchaseInvoiceCancellationRequest
                {
                    PurchaseId = issued.PurchaseId, ActorName = Actor, ActorRole = "Manager", Reason = "اختبار قبول إلغاء شراء على نسخة مؤقتة"
                };
                string financeAfterIssue = FinanceSnapshot();
                var cancellation = new PurchaseInvoiceCancellationOperation(Database.CreateConnection, () => true);
                bool injectedFailureRejected = false;
                try
                {
                    cancellation.CancelForVerificationRollback(cancelRequest, (connection, transaction, outcome) =>
                    {
                        Require(outcome.RemovedQuantity == 1 && Stock(connection, transaction, medicine) == initialStock,
                            "العكس التجريبي لم يخصم المخزون داخل المعاملة.");
                        throw new InvalidOperationException("QA injected purchase cancellation failure");
                    });
                }
                catch (InvalidOperationException error) when (error.Message == "QA injected purchase cancellation failure")
                {
                    injectedFailureRejected = true;
                }
                Require(injectedFailureRejected && Stock(medicine) == initialStock + 1 &&
                    FinanceSnapshot() == financeAfterIssue && CancellationCount(issued.PurchaseId) == 0,
                    "فشل العكس المحقون لم يتراجع بالكامل.");

                int auditCountBeforeSessionChange = Convert.ToInt32(ScalarForId(
                    "SELECT COUNT(*) FROM dbo.AuditLogTb WHERE ActionName=N'PurchaseInvoiceCancelled' AND Details LIKE N'%'+@Id+N'%';", issued.PurchaseId));
                cancellation.BeforeCommitForVerification = () => SessionContext.Clear();
                bool sessionChangeRejected = false;
                try { cancellation.Cancel(cancelRequest); }
                catch (UnauthorizedAccessException) { sessionChangeRejected = true; }
                finally
                {
                    cancellation.BeforeCommitForVerification = null;
                    if (!SessionContext.IsAuthenticated) SessionContext.Start(Actor, "Manager");
                }
                int auditCountAfterSessionChange = Convert.ToInt32(ScalarForId(
                    "SELECT COUNT(*) FROM dbo.AuditLogTb WHERE ActionName=N'PurchaseInvoiceCancelled' AND Details LIKE N'%'+@Id+N'%';", issued.PurchaseId));
                Require(sessionChangeRejected && Stock(medicine) == initialStock + 1 &&
                    FinanceSnapshot() == financeAfterIssue && CancellationCount(issued.PurchaseId) == 0 &&
                    auditCountAfterSessionChange == auditCountBeforeSessionChange,
                    "تغير الجلسة قبل اعتماد إلغاء الشراء لم يتراجع معه المخزون والقيد والتدقيق معاً.");

                PurchaseInvoiceCancellationOutcome result = cancellation.Cancel(cancelRequest);
                Require(result.PurchaseId == issued.PurchaseId && result.TotalAmount == issued.TotalAmount &&
                    result.RemovedQuantity == 1 && Stock(medicine) == initialStock,
                    "العكس النهائي لم يستعد كمية المخزون أو المبلغ المتوقع.");
                Require(FinanceSnapshot() == FinanceSnapshotBeforePurchase(issued.PurchaseId),
                    "صافي حسابات القيد الأصلي والعكسي لم يعد إلى الحالة السابقة.");
                Require(object.Equals(productionBefore, DateValue(medicine, "ProdDate")) &&
                    object.Equals(expiryBefore, DateValue(medicine, "ExpDate")),
                    "إلغاء المشتريات لم يستعد تواريخ المخزون السابقة.");
                Require(CancellationCount(issued.PurchaseId) == 1 &&
                    MovementCount(issued.PurchaseId, "PurchaseInvoiceCancellation") == 1,
                    "سجل العكس أو حركة المخزون غير موجودة أو مكررة.");
                bool duplicateRejected = false;
                try { cancellation.Cancel(cancelRequest); }
                catch (InvalidOperationException) { duplicateRejected = true; }
                Require(duplicateRejected && Stock(medicine) == initialStock && CancellationCount(issued.PurchaseId) == 1,
                    "الإلغاء المتكرر لم يُرفض دون أثر إضافي.");
                using (SqlConnection connection = Database.CreateConnection())
                {
                    connection.Open();
                    Require(Scalar(connection, "SELECT CASE WHEN EXISTS(SELECT 1 FROM dbo.PurchasesTb WHERE PurchaseId=@Id) THEN 1 ELSE 0 END;", issued.PurchaseId) == 1m,
                        "لم تُحفظ الفاتورة الأصلية لأغراض التدقيق.");
                    Require(Scalar(connection, "SELECT COUNT(*) FROM dbo.AuditLogTb WHERE ActionName=N'PurchaseInvoiceCancelled' AND Details LIKE N'%'+@Id+N'%';", issued.PurchaseId) == 1m,
                        "سجل التدقيق لا يوثق إلغاء الفاتورة.");
                    string status = Text(connection, "SELECT CASE WHEN EXISTS(SELECT 1 FROM dbo.JournalEntriesTb WHERE SourceType=N'PurchaseInvoiceCancellation' AND SourceId=@Id) THEN N'Cancelled' ELSE N'Active' END;", issued.PurchaseId);
                    Require(status == "Cancelled", "حالة سجل المشتريات لا تعكس الإلغاء المحفوظ.");
                }
                var payableRequest = new PurchaseInvoiceRequest
                {
                    ImportCurrencyCode = currency,
                    ImportTaxRatePercent = effectiveTax,
                    ImportPaymentMethod = "Payable",
                    DesiredPurchaseId = "QA-CXL-" + Guid.NewGuid().ToString("N").Substring(0, 16).ToUpperInvariant(),
                    SupplierName = supplier,
                    EmployeeName = Actor,
                    ActorRole = "Manager",
                    Notes = "QA payable purchase cancellation round trip; temporary owned clone",
                    PurchaseDate = DateTime.Now.Date,
                    CommercialInventoryExpected = commercialMode,
                    Lines = new[] { new PurchaseInvoiceLineRequest
                    {
                        LineNumber=1, MedicineName=medicine, BuyingPrice=2, Quantity=1,
                        BatchNumber=commercialMode ? "QA-CXL-" + Guid.NewGuid().ToString("N") : string.Empty,
                        ProductionDate=DateTime.Today.AddDays(-30), ExpiryDate=DateTime.Today.AddYears(2)
                    } }
                };
                string payableBaseline = FinanceSnapshot();
                PurchaseInvoiceResult payableIssued = new PurchaseInvoiceOperation(Database.CreateConnection).IssueForVerificationCommitted(payableRequest);
                Require(payableIssued.TotalAmount == 2m && Stock(medicine) == initialStock + 1 && FinanceSnapshot() != payableBaseline,
                    "اعتماد فاتورة المورد الآجلة لم يؤثر في المخزون أو القيد كما هو متوقع.");
                var payableCancellationRequest = new PurchaseInvoiceCancellationRequest
                {
                    PurchaseId = payableIssued.PurchaseId, ActorName = Actor, ActorRole = "Manager", Reason = "اختبار إلغاء شراء آجل على نسخة مؤقتة"
                };
                PurchaseInvoiceCancellationOutcome payableResult = cancellation.Cancel(payableCancellationRequest);
                Require(payableResult.PurchaseId == payableIssued.PurchaseId && payableResult.TotalAmount == payableIssued.TotalAmount &&
                    payableResult.RemovedQuantity == 1 && Stock(medicine) == initialStock &&
                    FinanceSnapshot() == FinanceSnapshotBeforePurchase(payableIssued.PurchaseId) &&
                    CancellationCount(payableIssued.PurchaseId) == 1 &&
                    MovementCount(payableIssued.PurchaseId, "PurchaseInvoiceCancellation") == 1,
                    "إلغاء فاتورة المورد الآجلة لم يعكس القيد والمخزون والحركة بأمان.");
                var settledPayableRequest = new PurchaseInvoiceRequest
                {
                    ImportCurrencyCode = currency,
                    ImportTaxRatePercent = effectiveTax,
                    ImportPaymentMethod = "Payable",
                    DesiredPurchaseId = "QA-CXL-" + Guid.NewGuid().ToString("N").Substring(0, 16).ToUpperInvariant(),
                    SupplierName = supplier,
                    EmployeeName = Actor,
                    ActorRole = "Manager",
                    Notes = "QA supplier settlement cancellation guard; temporary owned clone",
                    PurchaseDate = DateTime.Now.Date,
                    CommercialInventoryExpected = commercialMode,
                    Lines = new[] { new PurchaseInvoiceLineRequest
                    {
                        LineNumber=1, MedicineName=medicine, BuyingPrice=2, Quantity=1,
                        BatchNumber=commercialMode ? "QA-CXL-" + Guid.NewGuid().ToString("N") : string.Empty,
                        ProductionDate=DateTime.Today.AddDays(-30), ExpiryDate=DateTime.Today.AddYears(2)
                    } }
                };
                PurchaseInvoiceResult payableWithSettlement = new PurchaseInvoiceOperation(Database.CreateConnection).IssueForVerificationCommitted(settledPayableRequest);
                Require(Stock(medicine) == initialStock + 1, "فاتورة الاختبار الآجلة لم تضف مخزون المورد المتوقع.");
                int voucherId = AccountingService.CreateVoucher("PaymentExternal", DateTime.Today, supplier,
                    AccountingService.SuppliersAccount, 1m, "QA partial supplier settlement before attempted cancellation");
                Require(voucherId > 0, "لم تُحفظ دفعة المورد اللاحقة في نسخة QA المؤقتة.");
                string financeAfterSupplierSettlement = FinanceSnapshot();
                bool laterSettlementRejected = false;
                try
                {
                    cancellation.Cancel(new PurchaseInvoiceCancellationRequest
                    {
                        PurchaseId = payableWithSettlement.PurchaseId, ActorName = Actor, ActorRole = "Manager",
                        Reason = "رفض إلغاء فاتورة لها تسوية مورد لاحقة"
                    });
                }
                catch (InvalidOperationException error) when (error.Message.Contains("تعذر إثبات أن رصيد المورد"))
                {
                    laterSettlementRejected = true;
                }
                Require(laterSettlementRejected && Stock(medicine) == initialStock + 1 &&
                    FinanceSnapshot() == financeAfterSupplierSettlement &&
                    CancellationCount(payableWithSettlement.PurchaseId) == 0 &&
                    MovementCount(payableWithSettlement.PurchaseId, "PurchaseInvoiceCancellation") == 0,
                    "لم يمنع النظام إلغاء فاتورة مورد بعد تسجيل تسوية مالية لاحقة دون أثر.");
                Console.WriteLine("PASS: cash cancellation rollback, unpaid payable cancellation, and rejection after a real later supplier-payment voucher; inventory and finance invariants held. PurchaseIds=" + issued.PurchaseId + "," + payableIssued.PurchaseId + "," + payableWithSettlement.PurchaseId);
                if (commercialMode)
                {
                    string partialPurchaseId = "QA-CXL-" + Guid.NewGuid().ToString("N").Substring(0, 16).ToUpperInvariant();
                    int stockBeforePartialPurchase = Stock(medicine);
                    PurchaseInvoiceResult partiallyConsumedPurchase = new PurchaseInvoiceOperation(Database.CreateConnection).IssueForVerificationCommitted(
                        new PurchaseInvoiceRequest
                        {
                            ImportCurrencyCode = currency, ImportTaxRatePercent = effectiveTax, ImportPaymentMethod = "Cash",
                            DesiredPurchaseId = partialPurchaseId, SupplierName = supplier, EmployeeName = Actor, ActorRole = "Manager",
                            Notes = "QA partial-lot consumption cancellation guard; temporary owned clone", PurchaseDate = DateTime.Now,
                            CommercialInventoryExpected = true,
                            Lines = new[] { new PurchaseInvoiceLineRequest
                            {
                                LineNumber=1, MedicineName=medicine, BuyingPrice=2, Quantity=2,
                                BatchNumber="QA-CXL-" + Guid.NewGuid().ToString("N"),
                                ProductionDate=DateTime.Today.AddDays(-30), ExpiryDate=DateTime.Today.AddDays(14)
                            } }
                        });
                    Require(partiallyConsumedPurchase.TotalAmount == 4m && Stock(medicine) == stockBeforePartialPurchase + 2,
                        "لم يُعتمد مخزون تشغيلة الاختبار الجزئي كما هو متوقع.");
                    SalesInvoiceIssueResult sale = ApplicationServices.SalesInvoices.Issue(new SalesInvoiceIssueRequest
                    {
                        ImportCurrencyCode = currency, ImportTaxRatePercent = effectiveTax, ImportPriceTaxMode = "Inclusive",
                        CustomerId = string.Empty, CustomerName = "QA walk-in partial lot", PhoneNumber = "000000000",
                        EmployeeName = Actor, InvoiceDate = DateTime.Today, DiscountPercent = 0m,
                        DisplayedGrossTotal = salePrice, DisplayedNetTotal = salePrice,
                        Notes = "QA sale consumes one unit from the newest purchased test lot",
                        Lines = new[] { new SalesInvoiceLineRequest { MedicineName = medicine, DisplayedSellingPrice = salePrice, Quantity = 1 } }
                    });
                    Require(sale.IsCommercial && Stock(medicine) == stockBeforePartialPurchase + 1 &&
                        LotAllocationCount(partialPurchaseId) == 1 && RemainingForPurchaseLot(partialPurchaseId) == 1m,
                        "لم تُسجل عملية البيع كوحدة واحدة مستهلكة من تشغيلة الشراء المقصودة.");
                    string financeAfterPartialSale = FinanceSnapshot();
                    bool partialConsumptionRejected = false;
                    try
                    {
                        cancellation.Cancel(new PurchaseInvoiceCancellationRequest
                        {
                            PurchaseId = partialPurchaseId, ActorName = Actor, ActorRole = "Manager",
                            Reason = "رفض إلغاء فاتورة استهلكت تشغيلتها جزئيًا"
                        });
                    }
                    catch (InvalidOperationException error) when (error.Message.Contains("تشغيلتها"))
                    {
                        partialConsumptionRejected = true;
                    }
                    Require(partialConsumptionRejected && Stock(medicine) == stockBeforePartialPurchase + 1 &&
                        FinanceSnapshot() == financeAfterPartialSale && CancellationCount(partialPurchaseId) == 0 &&
                        MovementCount(partialPurchaseId, "PurchaseInvoiceCancellation") == 0 &&
                        RemainingForPurchaseLot(partialPurchaseId) == 1m,
                        "لم يرفض النظام إلغاء التشغيلة المستهلكة جزئيًا دون أثر إضافي.");
                }
                Console.WriteLine(commercialMode
                    ? "PASS: FullCommercial canceled unconsumed cash/payable lots and refused cancellation after a real one-unit sale from a two-unit purchased lot."
                    : "LIMIT: aggregate-inventory path only; run the guarded owned-clone commercial schema test for purchase lots.");
            }
            finally { SessionContext.Clear(); }
        }

        private static decimal RemainingForPurchaseLot(string purchaseId) => ScalarForId(@"SELECT l.QuantityRemaining FROM dbo.InventoryLotsTb l JOIN dbo.PurchaseDetailsTb d ON d.DetailId=l.PurchaseDetailId WHERE d.PurchaseId=@Id;", purchaseId);

        private static int LotAllocationCount(string purchaseId) => Convert.ToInt32(ScalarForId(@"SELECT COUNT(*) FROM dbo.LotAllocationsTb a JOIN dbo.InventoryLotsTb l ON l.LotId=a.LotId JOIN dbo.PurchaseDetailsTb d ON d.DetailId=l.PurchaseDetailId WHERE d.PurchaseId=@Id AND a.MovementType=N'Sale';", purchaseId));

        private static decimal ScalarForId(string sql, string id)
        {
            using (SqlConnection connection = Database.CreateConnection())
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.Parameters.Add("@Id", SqlDbType.NVarChar, 50).Value = id;
                connection.Open(); return Convert.ToDecimal(command.ExecuteScalar());
            }
        }

        internal static string PrepareLiveUiFixture(string backupPath, string ownerToken, string confirmation)
        {
            if (!string.Equals(confirmation, "--confirm-qa-purchase-cancellation", StringComparison.Ordinal))
                throw new InvalidOperationException("يلزم تأكيد إنشاء فاتورة واجهة على نسخة QA مؤقتة.");
            string expectedDatabase = QaSalesInvoiceSelfTests.RequireOwnedTemporaryQa(ownerToken);
            string medicine, supplier, currency;
            int initialStock;
            bool commercialMode;
            decimal effectiveTax;
            using (SqlConnection connection = Database.CreateConnection())
            {
                connection.Open();
                Require(connection.Database == expectedDatabase, "اتصال فاتورة الواجهة ليس بنسخة QA المملوكة.");
                QaTaxPolicySelfTests.VerifyBackup(connection, backupPath);
                commercialMode = CommercialSchemaInstaller.IsFullCommercialMode(connection, null);
                string sql = commercialMode
                    ? @"SELECT TOP(1) MedName,CompName,MedQty FROM dbo.MedicineTb1 WHERE MedQty>0 AND Sprice>0 AND ProdDate<@Production AND ExpDate>@Expiry ORDER BY MedName;"
                    : @"SELECT TOP(1) MedName,CompName,MedQty FROM dbo.MedicineTb1 WHERE MedQty>0 AND ProdDate<@Production AND ExpDate>@Expiry ORDER BY MedName;";
                using (SqlCommand command = new SqlCommand(sql, connection))
                {
                    command.Parameters.Add("@Production", SqlDbType.Date).Value = DateTime.Today.AddDays(-30);
                    command.Parameters.Add("@Expiry", SqlDbType.Date).Value = DateTime.Today.AddDays(30);
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        Require(reader.Read(), "لا يوجد صنف صالح لفاتورة قبول واجهة الإلغاء.");
                        medicine = reader.GetString(0);
                        supplier = reader.IsDBNull(1) ? "مورد اختبار الواجهة" : reader.GetString(1);
                        initialStock = reader.GetInt32(2);
                    }
                }
                currency = Text(connection, "SELECT TOP(1) CurrencyCode FROM dbo.CurrencySettingsTb WHERE IsSystemCurrency=1 ORDER BY CurrencyId;");
                effectiveTax = AccountingService.GetEffectiveTaxRate(connection, null);
                Require(currency.Length > 0, "عملة النظام غير محددة في نسخة QA.");
            }

            string purchaseId = "QA-CXL-UI-" + Guid.NewGuid().ToString("N").Substring(0, 16).ToUpperInvariant();
            SessionContext.Start(Actor, "Manager");
            try
            {
                PurchaseInvoiceResult result = new PurchaseInvoiceOperation(Database.CreateConnection).IssueForVerificationCommitted(
                    new PurchaseInvoiceRequest
                    {
                        ImportCurrencyCode = currency,
                        ImportTaxRatePercent = effectiveTax,
                        ImportPaymentMethod = "Cash",
                        DesiredPurchaseId = purchaseId,
                        SupplierName = supplier,
                        EmployeeName = Actor,
                        ActorRole = "Manager",
                        Notes = "QA-only live purchase cancellation UI fixture",
                        PurchaseDate = DateTime.Now,
                        CommercialInventoryExpected = commercialMode,
                        Lines = new[] { new PurchaseInvoiceLineRequest
                        {
                            LineNumber = 1, MedicineName = medicine, BuyingPrice = 1, Quantity = 1,
                            BatchNumber = commercialMode ? "QA-CXL-UI-" + Guid.NewGuid().ToString("N") : string.Empty,
                            ProductionDate = DateTime.Today.AddDays(-30), ExpiryDate = DateTime.Today.AddYears(2)
                        } }
                    });
                Require(result.PurchaseId == purchaseId && result.TotalAmount == 1m && Stock(medicine) == initialStock + 1,
                    "فاتورة واجهة QA لم تحفظ بالقيمة أو المخزون المتوقع.");
                Console.WriteLine("UI cancellation fixture ready. PurchaseId=" + purchaseId);
                return purchaseId;
            }
            finally { SessionContext.Clear(); }
        }
        private static string FinanceSnapshot()
        {
            using (SqlConnection connection = Database.CreateConnection())
            using (SqlCommand command = new SqlCommand(@"SELECT AccountCode,CONVERT(decimal(18,2),SUM(Debit-Credit))
FROM dbo.JournalEntryLinesTb GROUP BY AccountCode ORDER BY AccountCode;", connection))
            {
                connection.Open(); return ReadBalances(command);
            }
        }

        private static string FinanceSnapshotBeforePurchase(string purchaseId)
        {
            using (SqlConnection connection = Database.CreateConnection())
            using (SqlCommand command = new SqlCommand(@"SELECT l.AccountCode,CONVERT(decimal(18,2),SUM(l.Debit-l.Credit))
FROM dbo.JournalEntryLinesTb l JOIN dbo.JournalEntriesTb e ON e.JournalEntryId=l.JournalEntryId
WHERE NOT (e.SourceType IN(N'PurchaseInvoice',N'PurchaseInvoiceCancellation') AND e.SourceId=@Id)
GROUP BY l.AccountCode ORDER BY l.AccountCode;", connection))
            {
                command.Parameters.Add("@Id", SqlDbType.NVarChar, 50).Value = purchaseId;
                connection.Open(); return ReadBalances(command);
            }
        }

        private static string ReadBalances(SqlCommand command)
        {
            var result = new StringBuilder();
            using (SqlDataReader reader = command.ExecuteReader())
                while (reader.Read())
                    {
                        decimal balance = Convert.ToDecimal(reader[1]);
                        if (balance != 0m)
                            result.Append(reader.GetString(0)).Append('|')
                                .Append(balance.ToString("0.00", CultureInfo.InvariantCulture)).Append('\n');
                    }
            return result.ToString();
        }

        private static int Stock(string medicine)
        {
            using (SqlConnection connection = Database.CreateConnection())
            {
                connection.Open(); return Convert.ToInt32(Scalar(connection, "SELECT MedQty FROM dbo.MedicineTb1 WHERE MedName=@Id;", medicine));
            }
        }
        private static int Stock(SqlConnection connection, SqlTransaction transaction, string medicine)
        {
            using (SqlCommand command = new SqlCommand("SELECT MedQty FROM dbo.MedicineTb1 WHERE MedName=@Id;", connection, transaction))
            { command.Parameters.Add("@Id", SqlDbType.NVarChar, 200).Value = medicine; return Convert.ToInt32(command.ExecuteScalar()); }
        }
        private static decimal Scalar(SqlConnection connection, string sql, string value)
        {
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.Parameters.Add("@Id", SqlDbType.NVarChar, 50).Value = value;
                return Convert.ToDecimal(command.ExecuteScalar());
            }
        }
        private static string Text(SqlConnection connection, string sql)
        {
            using (SqlCommand command = new SqlCommand(sql, connection)) return Convert.ToString(command.ExecuteScalar()) ?? string.Empty;
        }
        private static string Text(SqlConnection connection, string sql, string id)
        {
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.Parameters.Add("@Id", SqlDbType.NVarChar, 50).Value = id;
                return Convert.ToString(command.ExecuteScalar()) ?? string.Empty;
            }
        }
        private static object DateValue(string medicine, string column)
        {
            if (column != "ProdDate" && column != "ExpDate") throw new ArgumentOutOfRangeException(nameof(column));
            using (SqlConnection connection = Database.CreateConnection())
            using (SqlCommand command = new SqlCommand("SELECT " + column + " FROM dbo.MedicineTb1 WHERE MedName=@Id;", connection))
            {
                command.Parameters.Add("@Id", SqlDbType.NVarChar, 200).Value = medicine;
                connection.Open(); object value = command.ExecuteScalar();
                return value == null || value == DBNull.Value ? (object)DBNull.Value : Convert.ToDateTime(value);
            }
        }
        private static int CancellationCount(string purchaseId) => Count("SELECT COUNT(*) FROM dbo.JournalEntriesTb WHERE SourceType=N'PurchaseInvoiceCancellation' AND SourceId=@Id;", purchaseId);
        private static int MovementCount(string purchaseId, string sourceType) => Count("SELECT COUNT(*) FROM dbo.InventoryMovementsTb WHERE SourceType=@Type AND SourceId=@Id;", purchaseId, sourceType);
        private static int Count(string sql, string purchaseId, string type = null)
        {
            using (SqlConnection connection = Database.CreateConnection())
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.Parameters.Add("@Id", SqlDbType.NVarChar, 50).Value = purchaseId;
                if (type != null) command.Parameters.Add("@Type", SqlDbType.NVarChar, 50).Value = type;
                connection.Open(); return Convert.ToInt32(command.ExecuteScalar());
            }
        }
        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}

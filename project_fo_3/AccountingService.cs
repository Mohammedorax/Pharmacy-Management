using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;

namespace project_fo_3
{
    internal sealed class JournalLineInput
    {
        public string AccountCode { get; set; }
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
        public string Description { get; set; }
        public int? CostCenterId { get; set; }
    }

    internal static class AccountingService
    {
        public const string CashAccount = "1000";
        public const string BankAccount = "1010";
        public const string CustomersAccount = "1100";
        public const string InventoryAccount = "1200";
        public const string SuppliersAccount = "2000";
        public const string OutputTaxAccount = "2100";
        public const string InputTaxAccount = "1210";
        public const string CapitalAccount = "3000";
        public const string RetainedEarningsAccount = "3100";
        public const string SalesAccount = "4000";
        public const string PurchasesAccount = "5000";

        public static void PostSalesInvoice(SqlConnection connection, SqlTransaction transaction, string invoiceId, decimal totalAmount, string description, decimal costOfGoods = 0)
        { PostSalesInvoice(connection, transaction, invoiceId, totalAmount, description, costOfGoods, null); }

        public static void PostSalesInvoice(SqlConnection connection, SqlTransaction transaction, string invoiceId, decimal totalAmount, string description, decimal costOfGoods, string expectedSystemCurrencyCode, DateTime? entryDate = null, decimal? includedTax = null)
        {
            if (totalAmount <= 0 || IsSourcePosted(connection, transaction, "SalesInvoice", invoiceId))
            {
                return;
            }

            decimal taxRate = GetEffectiveTaxRate(connection, transaction);
            decimal taxAmount = includedTax ?? CalculateIncludedTax(totalAmount, taxRate);
            decimal salesAmount = totalAmount - taxAmount;

            List<JournalLineInput> lines = new List<JournalLineInput>
            {
                new JournalLineInput { AccountCode = CashAccount, Debit = totalAmount, Description = description },
                new JournalLineInput { AccountCode = SalesAccount, Credit = salesAmount, Description = description }
            };

            if (taxAmount > 0)
            {
                lines.Add(new JournalLineInput { AccountCode = OutputTaxAccount, Credit = taxAmount, Description = description });
            }

            if (costOfGoods > 0)
            {
                lines.Add(new JournalLineInput { AccountCode = PurchasesAccount, Debit = costOfGoods, Description = "تكلفة " + description });
                lines.Add(new JournalLineInput { AccountCode = InventoryAccount, Credit = costOfGoods, Description = "تكلفة " + description });
            }

            CreateSalesJournalEntry(connection, transaction, entryDate ?? DateTime.Now, "SalesInvoice", invoiceId, description, SessionContext.CurrentUser, lines, expectedSystemCurrencyCode);
        }

        // يعكس القيد الفعلي المحفوظ، ولا يعيد احتساب الضريبة أو التكلفة من إعدادات قد تكون تغيرت لاحقاً.
        public static void PostSalesInvoiceCancellation(
            SqlConnection connection,
            SqlTransaction transaction,
            string originalInvoiceReference,
            string description,
            IEnumerable<JournalLineInput> originalLines,
            DateTime entryDate)
        {
            if (string.IsNullOrWhiteSpace(originalInvoiceReference))
            {
                throw new ArgumentException("مرجع الفاتورة الأصلية مطلوب للإلغاء.", nameof(originalInvoiceReference));
            }

            if (originalLines == null)
            {
                throw new ArgumentNullException(nameof(originalLines));
            }

            if (IsSourcePosted(connection, transaction, "SalesInvoiceCancellation", originalInvoiceReference))
            {
                throw new InvalidOperationException("تم إلغاء هذه الفاتورة مسبقاً.");
            }

            List<JournalLineInput> reversalLines = originalLines
                .Where(line => line != null && !string.IsNullOrWhiteSpace(line.AccountCode) && (line.Debit > 0m || line.Credit > 0m))
                .Select(line => new JournalLineInput
                {
                    AccountCode = line.AccountCode,
                    Debit = line.Credit,
                    Credit = line.Debit,
                    Description = string.IsNullOrWhiteSpace(line.Description) ? description : "عكس: " + line.Description,
                    CostCenterId = line.CostCenterId
                })
                .ToList();

            CreateSalesJournalEntry(
                connection,
                transaction,
                entryDate,
                "SalesInvoiceCancellation",
                originalInvoiceReference,
                description,
                SessionContext.CurrentUser,
                reversalLines);
            PharmacyManagement.Infrastructure.SqlServer.SqlCashBoxJournalRepository.LinkSaleCancellationCash(
                connection, transaction, originalInvoiceReference);
        }

        // الفاتورة التجارية تمرر إجمالياتها الثابتة؛ لا نعيد حساب الضريبة من إعداد قابل للتغيير.
        public static int PostSalesInvoice(SqlConnection connection, SqlTransaction transaction, string invoiceId, InvoiceTotals totals, string description, decimal costOfGoods, DateTime entryDate, decimal customerCreditApplied = 0m)
        {
            if (totals == null) throw new ArgumentNullException(nameof(totals));
            if (totals.NetTotal <= 0) return 0;
            InvoiceTotalsContract.Validate(totals);
            customerCreditApplied = InvoiceTotalsCalculator.Round(customerCreditApplied);
            if (customerCreditApplied < 0m || customerCreditApplied > totals.NetTotal)
                throw new InvalidOperationException("رصيد العميل المستخدم لا يطابق إجمالي الفاتورة.");
            int existingJournalEntryId = GetJournalEntryIdBySource(connection, transaction, "SalesInvoice", invoiceId);
            if (existingJournalEntryId > 0) return existingJournalEntryId;

            decimal cashCollected = InvoiceTotalsCalculator.Round(totals.NetTotal - customerCreditApplied);
            List<JournalLineInput> lines = new List<JournalLineInput>();
            if (cashCollected > 0m)
                lines.Add(new JournalLineInput { AccountCode = CashAccount, Debit = cashCollected, Description = description });
            if (customerCreditApplied > 0m)
                lines.Add(new JournalLineInput { AccountCode = CustomersAccount, Debit = customerCreditApplied, Description = "استخدام رصيد العميل - " + description });
            lines.Add(new JournalLineInput { AccountCode = SalesAccount, Credit = totals.TaxableTotal, Description = description });
            if (totals.TaxTotal > 0)
                lines.Add(new JournalLineInput { AccountCode = OutputTaxAccount, Credit = totals.TaxTotal, Description = description });
            if (costOfGoods > 0)
            {
                lines.Add(new JournalLineInput { AccountCode = PurchasesAccount, Debit = costOfGoods, Description = "تكلفة " + description });
                lines.Add(new JournalLineInput { AccountCode = InventoryAccount, Credit = costOfGoods, Description = "تكلفة " + description });
            }
            return CreateSalesJournalEntry(connection, transaction, entryDate, "SalesInvoice", invoiceId, description, SessionContext.CurrentUser, lines);
        }

        // إشعار دائن مستقل: يعكس فقط القيم الموثقة في سطور المرتجع، ولا يقرأ
        // إعداد الضريبة أو التكلفة المتغيرين لاحقاً. SourceId هو إشعار المرتجع
        // نفسه، فيبقى التكرار آمناً لكل مرتجع جزئي مستقل.
        public static int PostSalesReturnCreditNote(
            SqlConnection connection,
            SqlTransaction transaction,
            string returnInvoiceId,
            InvoiceTotals totals,
            decimal restoredInventoryCost,
            string refundMethod,
            string description,
            DateTime entryDate)
        {
            if (string.IsNullOrWhiteSpace(returnInvoiceId)) throw new ArgumentException("مرجع إشعار المرتجع مطلوب.", nameof(returnInvoiceId));
            List<JournalLineInput> lines = BuildCommercialSalesReturnJournalLines(totals, restoredInventoryCost, refundMethod, description);
            if (lines.Count == 0) return 0;
            int existingJournalEntryId = GetJournalEntryIdBySource(connection, transaction, "CommercialSalesReturn", returnInvoiceId);
            if (existingJournalEntryId > 0) return existingJournalEntryId;
            return CreateSalesJournalEntry(connection, transaction, entryDate, "CommercialSalesReturn", returnInvoiceId, description, SessionContext.CurrentUser, lines);
        }

        internal static List<JournalLineInput> BuildCommercialSalesReturnJournalLines(
            InvoiceTotals totals, decimal restoredInventoryCost, string refundMethod, string description)
        {
            if (totals == null) throw new ArgumentNullException(nameof(totals));
            if (restoredInventoryCost < 0m) throw new InvalidOperationException("تكلفة المرتجع التي ستعود للمخزون لا يمكن أن تكون سالبة.");
            InvoiceTotalsContract.Validate(totals);
            string settlementAccount = GetCommercialSalesReturnSettlementAccount(refundMethod);

            List<JournalLineInput> lines = new List<JournalLineInput>();
            if (totals.TaxableTotal > 0m)
                lines.Add(new JournalLineInput { AccountCode = SalesAccount, Debit = totals.TaxableTotal, Description = description });
            if (totals.NetTotal > 0m)
                lines.Add(new JournalLineInput { AccountCode = settlementAccount, Credit = totals.NetTotal, Description = description });
            if (totals.TaxTotal > 0m)
                lines.Add(new JournalLineInput { AccountCode = OutputTaxAccount, Debit = totals.TaxTotal, Description = description });
            if (restoredInventoryCost > 0m)
            {
                lines.Add(new JournalLineInput { AccountCode = InventoryAccount, Debit = restoredInventoryCost, Description = "تكلفة " + description });
                lines.Add(new JournalLineInput { AccountCode = PurchasesAccount, Credit = restoredInventoryCost, Description = "تكلفة " + description });
            }
            return lines;
        }

        internal static string GetCommercialSalesReturnSettlementAccount(string refundMethod)
        {
            switch ((refundMethod ?? string.Empty).Trim())
            {
                case "Cash":
                case "OriginalMethod":
                    // فواتير البيع التجارية الحالية تقيد التحصيل نقداً؛ لا يوجد بعدُ
                    // حقل لقطة لطريقة دفع الفاتورة الأصلية يسمح بتخمين غير ذلك.
                    return CashAccount;
                case "Card":
                    return BankAccount;
                case "Credit":
                    return CustomersAccount;
                default:
                    throw new InvalidOperationException("طريقة تسوية المرتجع غير معروفة.");
            }
        }

        public static void PostPurchaseInvoice(SqlConnection connection, SqlTransaction transaction, string purchaseId, decimal totalAmount, string description)
        {
            PostPurchaseInvoice(connection, transaction, purchaseId, totalAmount, description, DateTime.Now);
        }

        public static void PostPurchaseInvoice(SqlConnection connection, SqlTransaction transaction, string purchaseId, decimal totalAmount, string description, DateTime entryDate)
        {
            PostPurchaseInvoice(connection, transaction, purchaseId, totalAmount, description, entryDate, SessionContext.CurrentUser);
        }

        public static void PostPurchaseInvoice(SqlConnection connection, SqlTransaction transaction, string purchaseId, decimal totalAmount, string description, DateTime entryDate, string actorName)
        {
            PostPurchaseInvoice(connection, transaction, purchaseId, totalAmount, description, entryDate, actorName, "Cash");
        }

        public static void PostPurchaseInvoice(SqlConnection connection, SqlTransaction transaction, string purchaseId, decimal totalAmount,
            string description, DateTime entryDate, string actorName, string paymentMethod)
        { PostPurchaseInvoiceWithCurrency(connection,transaction,purchaseId,totalAmount,description,entryDate,actorName,paymentMethod,null); }
        public static void PostPurchaseInvoiceWithCurrency(SqlConnection connection, SqlTransaction transaction, string purchaseId, decimal totalAmount,
            string description, DateTime entryDate, string actorName, string paymentMethod, string expectedSystemCurrencyCode)
        {
            string settlementAccount = GetPurchaseSettlementAccount(paymentMethod);
            if (totalAmount <= 0 || IsSourcePosted(connection, transaction, "PurchaseInvoice", purchaseId))
            {
                return;
            }

            decimal taxRate = GetEffectiveTaxRate(connection, transaction);
            decimal taxAmount = CalculateIncludedTax(totalAmount, taxRate);
            decimal inventoryAmount = totalAmount - taxAmount;

            List<JournalLineInput> lines = new List<JournalLineInput>
            {
                new JournalLineInput { AccountCode = InventoryAccount, Debit = inventoryAmount, Description = description },
                new JournalLineInput { AccountCode = settlementAccount, Credit = totalAmount, Description = description }
            };

            if (taxAmount > 0)
            {
                lines.Add(new JournalLineInput { AccountCode = InputTaxAccount, Debit = taxAmount, Description = description });
            }

            if(expectedSystemCurrencyCode!=null)
                PharmacyManagement.Infrastructure.SqlServer.SqlJournalWriter.CreateWithCurrencyAsync(connection,transaction,entryDate,"PurchaseInvoice",purchaseId,description,
                    string.IsNullOrWhiteSpace(actorName)?"Unknown":actorName.Trim(),
                    lines.Select(line=>new PharmacyManagement.Domain.JournalLineValues(line.AccountCode,line.Debit,line.Credit,line.Description,line.CostCenterId)),
                    expectedSystemCurrencyCode,System.Threading.CancellationToken.None).GetAwaiter().GetResult();
            else
                CreateJournalEntry(connection, transaction, entryDate, "PurchaseInvoice", purchaseId, description,
                    string.IsNullOrWhiteSpace(actorName) ? "Unknown" : actorName.Trim(), lines);
        }

        internal static string GetPurchaseSettlementAccount(string paymentMethod)
        {
            switch ((paymentMethod ?? string.Empty).Trim())
            {
                case "":
                case "Cash": return CashAccount;
                case "Payable": return SuppliersAccount;
                default: throw new InvalidOperationException("طريقة سداد المشتريات غير مدعومة.");
            }
        }

        // Reverse the persisted purchase journal as-is so later tax settings cannot alter the cancellation.
        internal static void PostPurchaseInvoiceCancellation(SqlConnection connection, SqlTransaction transaction,
            string purchaseId, string description, IEnumerable<JournalLineInput> originalLines, DateTime entryDate, string actorName, string systemCurrencyCode = null)
        {
            if (string.IsNullOrWhiteSpace(purchaseId)) throw new ArgumentException("مرجع فاتورة الشراء مطلوب.", nameof(purchaseId));
            if (originalLines == null) throw new ArgumentNullException(nameof(originalLines));
            if (IsSourcePosted(connection, transaction, "PurchaseInvoiceCancellation", purchaseId))
                throw new InvalidOperationException("تم إلغاء فاتورة المشتريات مسبقاً.");

            List<JournalLineInput> reversal = originalLines
                .Where(line => line != null && !string.IsNullOrWhiteSpace(line.AccountCode) && (line.Debit > 0m || line.Credit > 0m))
                .Select(line => new JournalLineInput
                {
                    AccountCode = line.AccountCode,
                    Debit = line.Credit,
                    Credit = line.Debit,
                    Description = string.IsNullOrWhiteSpace(line.Description) ? description : "عكس: " + line.Description,
                    CostCenterId = line.CostCenterId
                }).ToList();
            if (reversal.Count < 2 || reversal.Sum(line => line.Debit) <= 0m ||
                reversal.Sum(line => line.Debit) != reversal.Sum(line => line.Credit))
                throw new InvalidOperationException("القيد الأصلي للمشتريات غير مكتمل أو غير متوازن؛ أوقف الإلغاء.");

            PharmacyManagement.Infrastructure.SqlServer.SqlJournalWriter.CreateHistoricalCurrency(connection, transaction,
                entryDate, "PurchaseInvoiceCancellation", purchaseId, description,
                string.IsNullOrWhiteSpace(actorName) ? "Unknown" : actorName.Trim(),
                reversal.Select(line => new PharmacyManagement.Domain.JournalLineValues(line.AccountCode, line.Debit, line.Credit, line.Description, line.CostCenterId)),
                systemCurrencyCode);
        }

        public static int CreateJournalEntry(DateTime entryDate, string sourceType, string sourceId, string description, IEnumerable<JournalLineInput> lines)
        {
            RequireOperationPermission(AppOperation.PostAccountingEntry);
            return JournalPostingDesktopService.Post(entryDate, sourceType, sourceId, description, lines);
        }

        // Sales currencies are transaction snapshots, never inferred from today's settings during a reversal.
        private static int CreateSalesJournalEntry(SqlConnection connection, SqlTransaction transaction, DateTime entryDate,
            string sourceType, string sourceId, string description, string createdBy, IEnumerable<JournalLineInput> lines,
            string expectedSystemCurrencyCode = null)
        {
            return PharmacyManagement.Infrastructure.SqlServer.SqlJournalWriter.CreateSalesDocument(connection, transaction,
                entryDate, sourceType, sourceId, description, createdBy, lines?.Select(line => line == null ? null :
                    new PharmacyManagement.Domain.JournalLineValues(line.AccountCode, line.Debit, line.Credit, line.Description, line.CostCenterId)),
                expectedSystemCurrencyCode).Id;
        }

        public static int CreateJournalEntry(SqlConnection connection, SqlTransaction transaction, DateTime entryDate, string sourceType, string sourceId, string description, string createdBy, IEnumerable<JournalLineInput> lines)
        {
            return PharmacyManagement.Infrastructure.SqlServer.SqlJournalWriter.Create(connection, transaction, entryDate, sourceType, sourceId,
                description, createdBy, lines?.Select(line => line == null ? null :
                    new PharmacyManagement.Domain.JournalLineValues(line.AccountCode, line.Debit, line.Credit, line.Description, line.CostCenterId))).Id;
        }

        public static int CreateVoucher(string voucherType, DateTime voucherDate, string partyName, string accountCode, decimal amount, string notes, string cashAccountCode = CashAccount, int? costCenterId = null, string expectedSystemCurrencyCode = null)
        {
            RequireOperationPermission(AppOperation.PostCashVoucher);
            return VoucherPostingDesktopService.Post(voucherType, voucherDate, partyName, accountCode, amount, notes, cashAccountCode, costCenterId, expectedSystemCurrencyCode);
        }

        public static DataTable GetAccounts(bool activeOnly = false)
        {
            return AccountingLookupDesktopService.LoadAccounts(activeOnly);
        }

        public static DataTable GetJournalEntries(DateTime from, DateTime to, string sourceType)
        {
            return JournalEntriesQuickReadService.Load(from, to, sourceType);
        }

        // فحص قراءة فقط: لا يعدّل القيد ولا يحاول موازنته تلقائياً.
        public static DataTable GetJournalIntegrityIssues(DateTime from, DateTime to)
        {
            return OperationalReportCatalog.ToDataTable(ApplicationServices.AccountingIntegrity.Load(from,to));
        }

        // مسار فحص منفصل للـ QA لا يعتمد على ملف اتصال تشغيل التطبيق ولا يقبل قاعدة إنتاج.
        internal static DataTable GetQaJournalIntegrityIssuesReadOnly(string serverName, string databaseName, DateTime from, DateTime to)
        {
            if (string.IsNullOrWhiteSpace(databaseName) || !databaseName.Trim().EndsWith("_qa", StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException("فحص سلامة القيود المباشر مسموح فقط لقاعدة ينتهي اسمها بـ _qa.");
            }

            using (SqlConnection connection = new SqlConnection(Database.BuildSettingsConnectionString(serverName, databaseName, true)))
            {
                return GetJournalIntegrityIssues(connection, from, to);
            }
        }

        private static DataTable GetJournalIntegrityIssues(SqlConnection connection, DateTime from, DateTime to)
        {
            var request=new PharmacyManagement.Application.AccountingIntegrityRequest(from,to);
            return OperationalReportCatalog.ToDataTable(
                PharmacyManagement.Infrastructure.SqlServer.SqlAccountingIntegrityReadRepository.Load(connection,request));
        }

        // ملخص قرار للإقفال؛ فحص قراءة فقط ولا ينشئ قيد إقفال أو يغير فترة محاسبية.
        public static DataTable GetPeriodCloseReadiness(DateTime from, DateTime to)
        {
            if (to.Date < from.Date)
            {
                throw new InvalidOperationException("تاريخ نهاية الفترة يجب أن يكون بعد تاريخ بدايتها أو مساوياً له.");
            }

            using (SqlConnection connection = Database.CreateConnection())
            {
                return GetPeriodCloseReadiness(connection, from, to);
            }
        }

        // فحص إقفال قراءة فقط لقاعدة QA؛ لا ينشئ قيداً ولا يغيّر حالة أي فترة.
        internal static DataTable GetQaPeriodCloseReadinessReadOnly(string serverName, string databaseName, DateTime from, DateTime to)
        {
            if (to.Date < from.Date)
            {
                throw new InvalidOperationException("تاريخ نهاية الفترة يجب أن يكون بعد تاريخ بدايتها أو مساوياً له.");
            }
            if (string.IsNullOrWhiteSpace(databaseName) || !databaseName.Trim().EndsWith("_qa", StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException("فحص جاهزية الإقفال المباشر مسموح فقط لقاعدة ينتهي اسمها بـ _qa.");
            }

            using (SqlConnection connection = new SqlConnection(Database.BuildSettingsConnectionString(serverName, databaseName, true)))
            {
                return GetPeriodCloseReadiness(connection, from, to);
            }
        }

        private static DataTable GetPeriodCloseReadiness(SqlConnection connection, DateTime from, DateTime to)
        {
            return GetPeriodCloseReadiness(connection, null, from, to);
        }

        internal static DataTable GetPeriodCloseReadiness(SqlConnection connection, SqlTransaction transaction, DateTime from, DateTime to)
        {
            using (SqlCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"
SELECT N'سلامة القيود' AS [الفحص], COUNT(1) AS [عدد الملاحظات],
       CASE WHEN COUNT(1) = 0 THEN N'سليم' ELSE N'يحتاج مراجعة' END AS [الحالة],
       N'قيود بلا أسطر أو غير متوازنة ضمن الفترة.' AS [التفصيل]
FROM (
    SELECT e.JournalEntryId
    FROM dbo.JournalEntriesTb e
    LEFT JOIN dbo.JournalEntryLinesTb l ON l.JournalEntryId = e.JournalEntryId
    WHERE e.EntryDate >= @FromDate AND e.EntryDate < DATEADD(DAY, 1, @ToDate)
    GROUP BY e.JournalEntryId
    HAVING COUNT(l.JournalEntryLineId) = 0 OR ABS(ISNULL(SUM(l.Debit), 0) - ISNULL(SUM(l.Credit), 0)) > 0.004
) journalIssues
UNION ALL
SELECT N'مطابقة المشتريات' AS [الفحص], COUNT(1) AS [عدد الملاحظات],
       CASE WHEN COUNT(1) = 0 THEN N'سليم' ELSE N'يحتاج مراجعة' END AS [الحالة],
       N'إجمالي فاتورة شراء لا يطابق مجموع تفاصيلها.' AS [التفصيل]
FROM (
    SELECT p.PurchaseId
    FROM dbo.PurchasesTb p
    LEFT JOIN dbo.PurchaseDetailsTb d ON d.PurchaseId = p.PurchaseId
    WHERE p.PurchaseDate >= @FromDate AND p.PurchaseDate < DATEADD(DAY, 1, @ToDate)
    GROUP BY p.PurchaseId, p.TotalAmount
    HAVING ABS(p.TotalAmount - ISNULL(SUM(d.TotalAmount), 0)) > 0.01
) purchaseIssues
UNION ALL
SELECT N'أرصدة مخزون سالبة' AS [الفحص], COUNT(1) AS [عدد الملاحظات],
       CASE WHEN COUNT(1) = 0 THEN N'سليم' ELSE N'يحتاج مراجعة' END AS [الحالة],
       N'رصيد حالي أقل من الصفر؛ الفحص يشمل المخزون الحالي.' AS [التفصيل]
FROM dbo.MedicineTb1 WHERE MedQty < 0;";
                command.Parameters.Add("@FromDate", SqlDbType.DateTime).Value = from.Date;
                command.Parameters.Add("@ToDate", SqlDbType.DateTime).Value = to.Date;
                DataTable readiness = Fill(command);
                int inventoryIssues = InventoryService.GetMovementIntegrityIssues(connection, transaction).Rows.Count;
                readiness.Rows.Add("تسلسل حركات المخزون", inventoryIssues, inventoryIssues == 0 ? "سليم" : "يحتاج مراجعة", "تطابق قبل/بعد كل حركة مع الرصيد الحالي؛ الفحص يشمل كل الحركات.");
                return readiness;
            }
        }

        public static DataTable GetVouchers(DateTime from, DateTime to, string voucherType)
        {
            using (SqlConnection connection = Database.CreateConnection())
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandText = @"
SELECT v.VoucherId AS [رقم السند],
       v.VoucherDate AS [التاريخ],
       CASE
           WHEN v.VoucherType = N'ReceiptInternal' THEN N'قبض داخلي'
           WHEN v.VoucherType = N'ReceiptExternal' THEN N'قبض خارجي'
           WHEN v.VoucherType = N'ReceiptMulti' THEN N'قبض متعدد'
           WHEN v.VoucherType = N'PaymentInternal' THEN N'صرف داخلي'
           WHEN v.VoucherType = N'PaymentExternal' THEN N'صرف خارجي'
           WHEN v.VoucherType = N'PaymentMulti' THEN N'صرف متعدد'
           ELSE v.VoucherType
       END AS [نوع السند],
       ISNULL(v.PartyName, N'') AS [الطرف],
       v.AccountCode AS [الحساب],
       v.Amount AS [المبلغ],
       ISNULL(v.Notes, N'') AS [ملاحظات],
       v.JournalEntryId AS [رقم القيد],
       v.CreatedBy AS [المستخدم]
FROM dbo.AccountingVouchersTb v
WHERE v.VoucherDate >= @FromDate
  AND v.VoucherDate < DATEADD(DAY, 1, @ToDate)
  AND (@VoucherType = N'' OR v.VoucherType = @VoucherType)
ORDER BY v.VoucherDate DESC, v.VoucherId DESC";
                command.Parameters.Add("@FromDate", SqlDbType.DateTime).Value = from.Date;
                command.Parameters.Add("@ToDate", SqlDbType.DateTime).Value = to.Date;
                command.Parameters.Add("@VoucherType", SqlDbType.NVarChar, 50).Value = string.IsNullOrWhiteSpace(voucherType) ? string.Empty : voucherType.Trim();
                return Fill(command);
            }
        }

        public static DataTable GetCostCenters()
        {
            return AccountingLookupDesktopService.LoadCostCenters();
        }

        public static DataTable GetLedger(
            string accountCode,
            DateTime from,
            DateTime to,
            string sourceType = null,
            bool includePreviousBalance = true,
            bool includeOpeningEntries = true,
            int? costCenterId = null)
        {
            using (SqlConnection connection = Database.CreateConnection())
            {
                return GetLedger(connection, accountCode, from, to, sourceType, includePreviousBalance, includeOpeningEntries, costCenterId);
            }
        }

        private static DataTable GetLedger(
            SqlConnection connection,
            string accountCode,
            DateTime from,
            DateTime to,
            string sourceType,
            bool includePreviousBalance,
            bool includeOpeningEntries,
            int? costCenterId)
        {
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandText = @"
DECLARE @OpeningBalance DECIMAL(18, 2);
SELECT @OpeningBalance = CASE WHEN @IncludePreviousBalance = 1 THEN ISNULL(SUM(l.Debit - l.Credit), 0) ELSE 0 END
FROM dbo.JournalEntryLinesTb l
INNER JOIN dbo.JournalEntriesTb e ON e.JournalEntryId = l.JournalEntryId
WHERE l.AccountCode = @AccountCode
  AND e.EntryDate < @FromDate
  AND (@SourceType IS NULL OR ISNULL(e.SourceType, N'Manual') = @SourceType)
  AND (@IncludeOpeningEntries = 1 OR ISNULL(e.SourceType, N'Manual') <> N'OpeningEntry')
  AND (@CostCenterId IS NULL OR (@CostCenterId = 0 AND l.CostCenterId IS NULL) OR l.CostCenterId = @CostCenterId);

SELECT e.EntryDate AS [التاريخ],
       e.JournalEntryId AS [رقم المستند],
       ISNULL(e.SourceType, N'Manual') AS [نوع المستند],
       ISNULL(e.SourceId, N'') AS [رقم المرجع],
       ISNULL(c.CostCenterName, N'') AS [مركز التكلفة],
       e.Description AS [البيان],
       l.Debit AS [مدين],
       l.Credit AS [دائن],
       @OpeningBalance + SUM(l.Debit - l.Credit) OVER (ORDER BY e.EntryDate, e.JournalEntryId, l.JournalEntryLineId) AS [الرصيد]
FROM dbo.JournalEntryLinesTb l
INNER JOIN dbo.JournalEntriesTb e ON e.JournalEntryId = l.JournalEntryId
LEFT JOIN dbo.CostCentersTb c ON c.CostCenterId = l.CostCenterId
WHERE l.AccountCode = @AccountCode
  AND e.EntryDate >= @FromDate AND e.EntryDate < DATEADD(DAY, 1, @ToDate)
  AND (@SourceType IS NULL OR ISNULL(e.SourceType, N'Manual') = @SourceType)
  AND (@IncludeOpeningEntries = 1 OR ISNULL(e.SourceType, N'Manual') <> N'OpeningEntry')
  AND (@CostCenterId IS NULL OR (@CostCenterId = 0 AND l.CostCenterId IS NULL) OR l.CostCenterId = @CostCenterId)
ORDER BY e.EntryDate, e.JournalEntryId, l.JournalEntryLineId";
                command.Parameters.Add("@AccountCode", SqlDbType.NVarChar, 20).Value = accountCode;
                command.Parameters.Add("@FromDate", SqlDbType.DateTime).Value = from.Date;
                command.Parameters.Add("@ToDate", SqlDbType.DateTime).Value = to.Date;
                command.Parameters.Add("@SourceType", SqlDbType.NVarChar, 50).Value = string.IsNullOrWhiteSpace(sourceType) ? (object)DBNull.Value : sourceType.Trim();
                command.Parameters.Add("@IncludePreviousBalance", SqlDbType.Bit).Value = includePreviousBalance;
                command.Parameters.Add("@IncludeOpeningEntries", SqlDbType.Bit).Value = includeOpeningEntries;
                command.Parameters.Add("@CostCenterId", SqlDbType.Int).Value = costCenterId.HasValue ? (object)costCenterId.Value : DBNull.Value;
                return Fill(command);
            }
        }

        public static decimal GetAccountBalanceBefore(
            string accountCode,
            DateTime date,
            string sourceType = null,
            bool includeOpeningEntries = true,
            int? costCenterId = null)
        {
            using (SqlConnection connection = Database.CreateConnection())
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandText = @"
SELECT ISNULL(SUM(l.Debit - l.Credit), 0)
FROM dbo.JournalEntryLinesTb l
INNER JOIN dbo.JournalEntriesTb e ON e.JournalEntryId = l.JournalEntryId
WHERE l.AccountCode = @AccountCode
  AND e.EntryDate < @BeforeDate
  AND (@SourceType IS NULL OR ISNULL(e.SourceType, N'Manual') = @SourceType)
  AND (@IncludeOpeningEntries = 1 OR ISNULL(e.SourceType, N'Manual') <> N'OpeningEntry')
  AND (@CostCenterId IS NULL OR (@CostCenterId = 0 AND l.CostCenterId IS NULL) OR l.CostCenterId = @CostCenterId)";
                command.Parameters.Add("@AccountCode", SqlDbType.NVarChar, 20).Value = accountCode;
                command.Parameters.Add("@BeforeDate", SqlDbType.DateTime).Value = date.Date;
                command.Parameters.Add("@SourceType", SqlDbType.NVarChar, 50).Value = string.IsNullOrWhiteSpace(sourceType) ? (object)DBNull.Value : sourceType.Trim();
                command.Parameters.Add("@IncludeOpeningEntries", SqlDbType.Bit).Value = includeOpeningEntries;
                command.Parameters.Add("@CostCenterId", SqlDbType.Int).Value = costCenterId.HasValue ? (object)costCenterId.Value : DBNull.Value;
                connection.Open();
                return Convert.ToDecimal(command.ExecuteScalar());
            }
        }

        public static DataRow GetTaxSettings()
        {
            using (SqlConnection connection = Database.CreateConnection())
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandText = "SELECT TOP 1 TaxSettingId, TaxRate, IsEnabled FROM dbo.TaxSettingsTb ORDER BY TaxSettingId";
                DataTable table = Fill(command);
                return table.Rows.Count == 0 ? null : table.Rows[0];
            }
        }

        public static void SaveTaxSettings(decimal taxRate, bool isEnabled)
        {
            SaveTaxSettings(taxRate, isEnabled, null);
        }

        public static void SaveTaxSettings(decimal taxRate, bool isEnabled, string countryCode)
        {
            if (!PermissionService.CanOpen(AppPermission.Settings))
                throw new UnauthorizedAccessException("تعديل الضريبة متاح لمسؤول الإعدادات فقط.");
            if (countryCode != null && countryCode != "YE" && countryCode != "SA" && countryCode != "OTHER")
                throw new ArgumentException("اختر بلد الضريبة من القائمة.", nameof(countryCode));
            new TaxPolicy(countryCode, isEnabled, taxRate);
            using (TaxSettingsRecovery.Enter())
            {
                TaxSettingsRecovery.Recover();
                string previousCountry = TaxPolicyService.GetConfiguredCountryCode();
                string nextCountry = countryCode ?? previousCountry;
                using (SqlConnection connection = Database.CreateConnection())
                {
                    connection.Open();
                    string operation = TaxSettingsRecovery.Begin(connection, previousCountry, nextCountry);
                    using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                    {
                        WriteTaxSettings(connection, transaction, taxRate, isEnabled);
                        TaxSettingsRecovery.WriteReceipt(connection, transaction, operation);
                        transaction.Commit();
                    }
                    // A failed local save leaves the journal intact; recovery uses the SQL receipt.
                    TaxSettingsRecovery.Complete(nextCountry);
                }
            }
        }

        internal static void WriteTaxSettings(SqlConnection connection, SqlTransaction transaction, decimal taxRate, bool isEnabled)
        {
            using (SqlCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"
DECLARE @TaxSettingId INT;
SELECT TOP 1 @TaxSettingId = TaxSettingId
FROM dbo.TaxSettingsTb WITH (UPDLOCK, HOLDLOCK)
ORDER BY TaxSettingId;

IF @TaxSettingId IS NOT NULL
BEGIN
    UPDATE dbo.TaxSettingsTb
    SET TaxRate = @TaxRate, IsEnabled = @IsEnabled, UpdatedAt = GETDATE()
    WHERE TaxSettingId = @TaxSettingId;
END
ELSE
BEGIN
    INSERT INTO dbo.TaxSettingsTb (TaxRate, IsEnabled) VALUES (@TaxRate, @IsEnabled);
END";
                SqlParameter rateParameter = command.Parameters.Add("@TaxRate", SqlDbType.Decimal);
                rateParameter.Precision = 9; rateParameter.Scale = 2; rateParameter.Value = taxRate;
                command.Parameters.Add("@IsEnabled", SqlDbType.Bit).Value = isEnabled;

                command.ExecuteNonQuery();
            }
        }

        private static void RequireOperationPermission(AppOperation operation)
        {
            if (!PermissionService.CanExecute(operation))
                throw new UnauthorizedAccessException("لا تملك صلاحية تنفيذ " + PermissionService.GetArabicName(operation) + ".");
        }
        public static void SaveAccount(string code,string name,string type,string parentCode,bool isActive)
        {
            RequireOperationPermission(AppOperation.ManageChartOfAccounts);
            AccountWritingDesktopService.Save(new PharmacyManagement.Application.AccountSaveRequest(code,name,type,parentCode,isActive));
        }
        public static void DeleteAccount(string code)
        {
            RequireOperationPermission(AppOperation.ManageChartOfAccounts);
            AccountWritingDesktopService.Delete(code);
        }

        private static bool IsSourcePosted(SqlConnection connection, SqlTransaction transaction, string sourceType, string sourceId)
        {
            if (string.IsNullOrWhiteSpace(sourceType) || string.IsNullOrWhiteSpace(sourceId))
            {
                return false;
            }

            return GetJournalEntryIdBySource(connection, transaction, sourceType, sourceId) > 0;
        }

        private static int GetJournalEntryIdBySource(SqlConnection connection, SqlTransaction transaction, string sourceType, string sourceId)
        {
            return PharmacyManagement.Infrastructure.SqlServer.SqlJournalWriter.GetJournalEntryIdBySource(connection, transaction, sourceType, sourceId);
        }

        public static decimal GetEffectiveTaxRate(SqlConnection connection, SqlTransaction transaction)
        {
            return TaxPolicyService.GetEffectiveTaxRate(connection, transaction);
        }
        internal static decimal CalculateIncludedTax(SqlConnection connection, SqlTransaction transaction, decimal totalAmount)
        {
            return CalculateIncludedTax(totalAmount, GetEffectiveTaxRate(connection, transaction));
        }

        internal static decimal CalculateIncludedTax(decimal totalAmount, decimal taxRate)
        {
            if (taxRate <= 0)
            {
                return 0;
            }

            return InvoiceTotalsCalculator.Round(totalAmount * taxRate / (100 + taxRate));
        }

        private static DataTable Fill(SqlCommand command)
        {
            using (SqlDataAdapter adapter = new SqlDataAdapter(command))
            {
                DataTable table = new DataTable();
                adapter.Fill(table);
                return table;
            }
        }

        private static object ToDbValue(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? (object)DBNull.Value : value.Trim();
        }
    }
}

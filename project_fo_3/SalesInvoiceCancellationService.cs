using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;

namespace project_fo_3
{
    internal sealed class SalesInvoiceCancellationResult
    {
        public string OriginalInvoiceReference { get; private set; }
        public decimal TotalAmount { get; private set; }
        public int RestoredQuantity { get; private set; }

        public SalesInvoiceCancellationResult(string originalInvoiceReference, decimal totalAmount, int restoredQuantity)
        {
            OriginalInvoiceReference = originalInvoiceReference;
            TotalAmount = totalAmount;
            RestoredQuantity = restoredQuantity;
        }
    }

    // الإلغاء يحافظ على الفاتورة الأصلية وحركة البيع وقيدها، ثم ينشئ حركات وقيداً معاكساً داخل معاملة واحدة.
    internal static class SalesInvoiceCancellationService
    {
        private sealed class SaleLine
        {
            public string BillId { get; set; }
            public string MedicineName { get; set; }
            public int Quantity { get; set; }
            public decimal SellingPrice { get; set; }
            public decimal TotalAmount { get; set; }
            public string CustomerId { get; set; }
        }

        public static SalesInvoiceCancellationResult CancelCurrentLegacySale(IEnumerable<string> billLineIds, string reason)
        {
            return CancelCurrentLegacySale(billLineIds, reason, SessionContext.CurrentUser);
        }

        public static SalesInvoiceCancellationResult CancelCurrentLegacySale(IEnumerable<string> billLineIds, string reason, string actorName)
        {
            string actorRole = SessionContext.CurrentRole;
            Func<bool> current = CaptureSessionCurrent(actorName, actorRole);
            return CancelCurrentLegacySale(billLineIds, reason, actorName, actorRole, current, null);
        }

        internal static SalesInvoiceCancellationResult CancelCurrentLegacySale(IEnumerable<string> billLineIds, string reason,
            string actorName, string actorRole, Func<bool> current, Action beforeCommitForVerification)
        {
            if (current == null || !current()) throw new UnauthorizedAccessException("تغيرت جلسة إلغاء فاتورة البيع.");
            List<string> ids = NormalizeIds(billLineIds);
            if (ids.Count == 0)
            {
                throw new UiValidationException("لا توجد بنود فاتورة معتمدة لإلغائها.");
            }

            using (SqlConnection connection = Database.CreateConnection())
            {
                connection.Open();
                using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        SalesInvoiceCancellationResult result = CancelLegacySale(connection, transaction, ids, null, reason, actorName);
                        WriteAudit(connection, transaction, actorName, actorRole, result.OriginalInvoiceReference, result.TotalAmount, result.RestoredQuantity, reason);
                        beforeCommitForVerification?.Invoke();
                        using (SessionCommitGate.Commit(current)) transaction.Commit();
                        return result;
                    }
                    catch
                    {
                        try { transaction.Rollback(); } catch { }
                        throw;
                    }
                }
            }
        }

        // يستأنف فقط الفواتير التي سجّل التطبيق ربطها الصريح بعد هذا التحسين؛ لا يجمع بنوداً تاريخية بالتخمين.
        public static SalesInvoiceCancellationResult CancelMappedLegacySale(string invoiceReference, string reason)
        {
            return CancelMappedLegacySale(invoiceReference, reason, SessionContext.CurrentUser);
        }

        public static SalesInvoiceCancellationResult CancelMappedLegacySale(string invoiceReference, string reason, string actorName)
        {
            string actorRole = SessionContext.CurrentRole;
            Func<bool> current = CaptureSessionCurrent(actorName, actorRole);
            return CancelMappedLegacySale(invoiceReference, reason, actorName, actorRole, current, null);
        }

        internal static SalesInvoiceCancellationResult CancelMappedLegacySale(string invoiceReference, string reason,
            string actorName, string actorRole, Func<bool> current, Action beforeCommitForVerification)
        {
            if (current == null || !current()) throw new UnauthorizedAccessException("تغيرت جلسة إلغاء فاتورة البيع.");
            if (string.IsNullOrWhiteSpace(invoiceReference))
            {
                throw new UiValidationException("مرجع الفاتورة غير صالح للإلغاء.");
            }

            using (SqlConnection connection = Database.CreateConnection())
            {
                connection.Open();
                using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        LegacyMappedSalesInvoice invoice = LegacySalesInvoiceRepository.GetMappedInvoice(connection, transaction, invoiceReference);
                        SalesInvoiceCancellationResult result = CancelLegacySale(connection, transaction, invoice.BillLineIds, invoice.TotalAmount, reason, actorName);
                        WriteAudit(connection, transaction, actorName, actorRole, result.OriginalInvoiceReference, result.TotalAmount, result.RestoredQuantity, reason);
                        beforeCommitForVerification?.Invoke();
                        using (SessionCommitGate.Commit(current)) transaction.Commit();
                        return result;
                    }
                    catch
                    {
                        try { transaction.Rollback(); } catch { }
                        throw;
                    }
                }
            }
        }

        private static SalesInvoiceCancellationResult CancelLegacySale(SqlConnection connection, SqlTransaction transaction, IList<string> ids, decimal? archivedTotalAmount, string reason, string actorName)
        {
            if (string.IsNullOrWhiteSpace(actorName)) throw new UnauthorizedAccessException("هوية منفذ إلغاء الفاتورة مطلوبة.");
            string originalReference = FindOriginalAccountingReference(connection, transaction, ids);
            if (IsCancellationAlreadyPosted(connection, transaction, originalReference))
            {
                throw new UiValidationException("تم إلغاء هذه الفاتورة مسبقاً، ولا يمكن تكرار الإلغاء.");
            }

            List<JournalLineInput> originalJournalLines = LoadOriginalJournalLines(connection, transaction, originalReference);
            if (originalJournalLines.Count == 0)
            {
                throw new UiValidationException("لا يوجد قيد أصلي كامل لهذه الفاتورة، لذلك أُوقف الإلغاء الآلي حفاظاً على سلامة الحسابات.");
            }

            List<SaleLine> saleLines = LoadSaleLines(connection, transaction, ids);
            if (saleLines.Count != ids.Count)
            {
                throw new UiValidationException("بعض بنود الفاتورة لم تعد موجودة؛ أُوقف الإلغاء لمنع عكس جزئي.");
            }

            string customerId = saleLines.Select(line => line.CustomerId ?? string.Empty).Distinct(StringComparer.OrdinalIgnoreCase).SingleOrDefault();
            if (string.IsNullOrWhiteSpace(customerId))
            {
                throw new UiValidationException("لا يمكن إلغاء فاتورة بلا عميل مسجل.");
            }

            decimal lineTotal = saleLines.Sum(line => line.TotalAmount);
            if (lineTotal <= 0m)
            {
                throw new UiValidationException("إجمالي الفاتورة غير صالح للإلغاء.");
            }

            decimal invoiceTotal = GetPostedSalesTotal(originalJournalLines);
            if (archivedTotalAmount.HasValue && archivedTotalAmount.Value != invoiceTotal)
            {
                throw new UiValidationException("إجمالي الفاتورة المؤرشف لا يطابق القيد الأصلي؛ أُوقف الإلغاء لمنع عكس مالي غير صحيح.");
            }

            int restoredQuantity = RestoreStockAndRecordMovements(connection, transaction, saleLines, originalReference, reason, actorName.Trim());
            ReverseCustomerTotal(connection, transaction, customerId, invoiceTotal);
            string description = "إلغاء فاتورة بيع رقم " + originalReference;
            if (!string.IsNullOrWhiteSpace(reason)) description += " - " + reason.Trim();
            AccountingService.PostSalesInvoiceCancellation(connection, transaction, originalReference, description, originalJournalLines, DateTime.Now);
            return new SalesInvoiceCancellationResult(originalReference, invoiceTotal, restoredQuantity);
        }

        // مبلغ العكس المالي هو ما اعتمد فعلياً في القيد (الصافي بعد الخصم)، لا مجرد مجموع أسعار البنود.
        private static decimal GetPostedSalesTotal(IEnumerable<JournalLineInput> originalJournalLines)
        {
            decimal cashDebit = originalJournalLines
                .Where(line => string.Equals(line.AccountCode, AccountingService.CashAccount, StringComparison.OrdinalIgnoreCase))
                .Sum(line => line.Debit);
            if (cashDebit <= 0m)
            {
                throw new UiValidationException("القيد الأصلي لا يتضمن مبلغ بيع نقدي صالحاً؛ أُوقف الإلغاء للحفاظ على سلامة الحسابات.");
            }
            return cashDebit;
        }

        private static List<string> NormalizeIds(IEnumerable<string> billLineIds)
        {
            return (billLineIds ?? Enumerable.Empty<string>())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id.Trim())
                .Where(id => !id.StartsWith("DRAFT-", StringComparison.OrdinalIgnoreCase) && !id.StartsWith("C-", StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string FindOriginalAccountingReference(SqlConnection connection, SqlTransaction transaction, IList<string> ids)
        {
            List<string> references = new List<string>();
            foreach (string id in ids)
            {
                using (SqlCommand command = new SqlCommand(@"
SELECT TOP (1) SourceId
FROM dbo.JournalEntriesTb WITH (UPDLOCK, HOLDLOCK)
WHERE SourceType = N'SalesInvoice' AND SourceId = @SourceId;", connection, transaction))
                {
                    command.Parameters.Add("@SourceId", SqlDbType.NVarChar, 50).Value = id;
                    object value = command.ExecuteScalar();
                    if (value != null && value != DBNull.Value)
                    {
                        references.Add(Convert.ToString(value));
                    }
                }
            }

            if (references.Count != 1)
            {
                throw new UiValidationException("لم يمكن تحديد قيد واحد للفواتير المحددة. الإلغاء متاح للفواتورة الحالية فقط.");
            }

            return references[0];
        }

        private static bool IsCancellationAlreadyPosted(SqlConnection connection, SqlTransaction transaction, string originalReference)
        {
            using (SqlCommand command = new SqlCommand(@"
SELECT TOP (1) 1
FROM dbo.JournalEntriesTb WITH (UPDLOCK, HOLDLOCK)
WHERE SourceType = N'SalesInvoiceCancellation' AND SourceId = @SourceId;", connection, transaction))
            {
                command.Parameters.Add("@SourceId", SqlDbType.NVarChar, 50).Value = originalReference;
                return command.ExecuteScalar() != null;
            }
        }

        private static List<JournalLineInput> LoadOriginalJournalLines(SqlConnection connection, SqlTransaction transaction, string originalReference)
        {
            List<JournalLineInput> lines = new List<JournalLineInput>();
            using (SqlCommand command = new SqlCommand(@"
SELECT l.AccountCode, l.Debit, l.Credit, l.LineDescription, l.CostCenterId
FROM dbo.JournalEntriesTb e WITH (UPDLOCK, HOLDLOCK)
INNER JOIN dbo.JournalEntryLinesTb l WITH (UPDLOCK, HOLDLOCK) ON l.JournalEntryId = e.JournalEntryId
WHERE e.SourceType = N'SalesInvoice' AND e.SourceId = @SourceId
ORDER BY l.JournalEntryLineId;", connection, transaction))
            {
                command.Parameters.Add("@SourceId", SqlDbType.NVarChar, 50).Value = originalReference;
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        lines.Add(new JournalLineInput
                        {
                            AccountCode = Convert.ToString(reader["AccountCode"]),
                            Debit = Convert.ToDecimal(reader["Debit"]),
                            Credit = Convert.ToDecimal(reader["Credit"]),
                            Description = reader["LineDescription"] == DBNull.Value ? string.Empty : Convert.ToString(reader["LineDescription"]),
                            CostCenterId = reader["CostCenterId"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["CostCenterId"])
                        });
                    }
                }
            }
            return lines;
        }

        private static List<SaleLine> LoadSaleLines(SqlConnection connection, SqlTransaction transaction, IList<string> ids)
        {
            List<SaleLine> lines = new List<SaleLine>();
            foreach (string id in ids)
            {
                using (SqlCommand command = new SqlCommand(@"
SELECT BillId, MedName, Sprice, Quantity, TotalAmount, CustomerId
FROM dbo.BillsTb1 WITH (UPDLOCK, HOLDLOCK)
WHERE BillId = @BillId;", connection, transaction))
                {
                    command.Parameters.Add("@BillId", SqlDbType.NVarChar, 50).Value = id;
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (!reader.Read()) continue;
                        int quantity = Convert.ToInt32(reader["Quantity"]);
                        decimal total = Convert.ToDecimal(reader["TotalAmount"]);
                        if (quantity <= 0 || total <= 0m)
                        {
                            throw new UiValidationException("يوجد بند بيع غير صالح للإلغاء.");
                        }
                        lines.Add(new SaleLine
                        {
                            BillId = Convert.ToString(reader["BillId"]),
                            MedicineName = Convert.ToString(reader["MedName"]),
                            SellingPrice = Convert.ToDecimal(reader["Sprice"]),
                            Quantity = quantity,
                            TotalAmount = total,
                            CustomerId = reader["CustomerId"] == DBNull.Value ? string.Empty : Convert.ToString(reader["CustomerId"])
                        });
                    }
                }
            }
            return lines;
        }

        private static int RestoreStockAndRecordMovements(SqlConnection connection, SqlTransaction transaction, IEnumerable<SaleLine> saleLines, string originalReference, string reason, string actorName)
        {
            int restoredQuantity = 0;
            foreach (SaleLine line in saleLines)
            {
                int before = InventoryMovementService.GetCurrentQuantity(connection, transaction, line.MedicineName);
                using (SqlCommand command = new SqlCommand(@"
UPDATE dbo.MedicineTb1
SET MedQty = MedQty + @Quantity
WHERE MedName = @MedName;", connection, transaction))
                {
                    command.Parameters.Add("@Quantity", SqlDbType.Int).Value = line.Quantity;
                    command.Parameters.Add("@MedName", SqlDbType.NVarChar, 200).Value = line.MedicineName;
                    if (command.ExecuteNonQuery() != 1)
                    {
                        throw new UiValidationException("تعذر إعادة المخزون للدواء: {0}", line.MedicineName);
                    }
                }
                InventoryMovementService.Record(
                    connection,
                    transaction,
                    line.MedicineName,
                    line.Quantity,
                    before,
                    checked(before + line.Quantity),
                    "SalesInvoiceCancellation",
                    originalReference,
                    null,
                    line.SellingPrice,
                    string.IsNullOrWhiteSpace(reason) ? "إعادة مخزون بسبب إلغاء فاتورة بيع" : "إعادة مخزون بسبب إلغاء فاتورة بيع: " + reason.Trim(),
                    actorName);
                restoredQuantity = checked(restoredQuantity + line.Quantity);
            }
            return restoredQuantity;
        }

        private static void ReverseCustomerTotal(SqlConnection connection, SqlTransaction transaction, string customerId, decimal invoiceTotal)
        {
            using (SqlCommand command = new SqlCommand(@"
UPDATE dbo.CustomersBillsTb WITH (UPDLOCK, HOLDLOCK)
SET TotalSpent = TotalSpent - @InvoiceTotal
WHERE CustomerId = @CustomerId AND TotalSpent >= @InvoiceTotal;", connection, transaction))
            {
                SqlParameter total = command.Parameters.Add("@InvoiceTotal", SqlDbType.Decimal);
                total.Precision = 18;
                total.Scale = 2;
                total.Value = invoiceTotal;
                command.Parameters.Add("@CustomerId", SqlDbType.NVarChar, 50).Value = customerId;
                if (command.ExecuteNonQuery() != 1)
                {
                    throw new UiValidationException("رصيد العميل لا يسمح بعكس الفاتورة بأمان.");
                }
            }
        }

        private static Func<bool> CaptureSessionCurrent(string actorName, string actorRole)
        {
            long revision = SessionContext.CurrentSessionRevision;
            string employeeId = SessionContext.CurrentEmployeeId;
            string source = Database.ConnectionString;
            return () => SessionContext.IsAuthenticated && SessionContext.CurrentSessionRevision == revision &&
                string.Equals(SessionContext.CurrentUser, actorName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(SessionContext.CurrentRole, actorRole, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(SessionContext.CurrentEmployeeId, employeeId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Database.ConnectionString, source, StringComparison.Ordinal) &&
                PermissionService.CanExecute(AppOperation.CancelSalesInvoice);
        }

        private static void WriteAudit(SqlConnection connection, SqlTransaction transaction, string actorName,
            string actorRole, string originalReference, decimal totalAmount, int restoredQuantity, string reason)
        {
            using (var command = new SqlCommand(@"INSERT INTO dbo.AuditLogTb
(UserName,UserRole,ActionName,EntityName,Details)
VALUES(@User,@Role,N'SalesInvoiceCancelled',N'Billing',@Details);", connection, transaction))
            {
                command.Parameters.Add("@User", SqlDbType.NVarChar, 200).Value = actorName.Trim();
                command.Parameters.Add("@Role", SqlDbType.NVarChar, 50).Value = string.IsNullOrWhiteSpace(actorRole) ? "Unknown" : actorRole.Trim();
                command.Parameters.Add("@Details", SqlDbType.NVarChar, 1000).Value =
                    "تم إلغاء فاتورة بيع مرجعها " + originalReference + "؛ الإجمالي المعكوس: " + totalAmount.ToString("N2") +
                    "؛ الكمية المعادة: " + restoredQuantity + "؛ السبب: " + (string.IsNullOrWhiteSpace(reason) ? "غير محدد" : reason.Trim()) + ".";
                command.ExecuteNonQuery();
            }
        }
    }
}

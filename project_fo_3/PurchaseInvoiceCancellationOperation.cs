using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using PharmacyManagement.Application;

namespace project_fo_3
{
    internal sealed class PurchaseInvoiceCancellationOperation : IPurchaseInvoiceCancellationOperation
    {
        private readonly Func<SqlConnection> connectionFactory;
        private readonly Func<bool> isAuthorized;
        internal Action BeforeCommitForVerification { get; set; }

        internal PurchaseInvoiceCancellationOperation()
            : this(Database.CreateConnection, () => PermissionService.CanExecute(AppOperation.CancelPurchaseInvoice))
        {
        }

        internal PurchaseInvoiceCancellationOperation(Func<SqlConnection> connectionFactory, Func<bool> isAuthorized)
        {
            this.connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
            this.isAuthorized = isAuthorized ?? throw new ArgumentNullException(nameof(isAuthorized));
        }

        public PurchaseInvoiceCancellationOutcome Cancel(PurchaseInvoiceCancellationRequest request)
        {
            if (!isAuthorized()) throw new UnauthorizedAccessException("لا تملك صلاحية إلغاء فاتورة المشتريات.");
            bool authenticated = SessionContext.IsAuthenticated;
            long sessionRevision = SessionContext.CurrentSessionRevision;
            string user = SessionContext.CurrentUser;
            string role = SessionContext.CurrentRole;
            string employeeId = SessionContext.CurrentEmployeeId;
            string source = Database.ConnectionString;
            if (authenticated && (!string.Equals(user, request?.ActorName, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(role, request?.ActorRole, StringComparison.OrdinalIgnoreCase)))
                throw new UnauthorizedAccessException("تغير منفذ إلغاء المشتريات؛ أعد فتح سجل الفواتير قبل المتابعة.");
            Func<bool> current = () => isAuthorized() && (!authenticated ||
                (SessionContext.IsAuthenticated && SessionContext.CurrentSessionRevision == sessionRevision &&
                 string.Equals(SessionContext.CurrentUser, user, StringComparison.OrdinalIgnoreCase) &&
                 string.Equals(SessionContext.CurrentRole, role, StringComparison.OrdinalIgnoreCase) &&
                 string.Equals(SessionContext.CurrentEmployeeId, employeeId, StringComparison.OrdinalIgnoreCase) &&
                 string.Equals(Database.ConnectionString, source, StringComparison.Ordinal)));
            return CancelCore(request, true, null, current);
        }

        internal PurchaseInvoiceCancellationOutcome CancelForVerificationRollback(PurchaseInvoiceCancellationRequest request,
            Action<SqlConnection, SqlTransaction, PurchaseInvoiceCancellationOutcome> verifyBeforeRollback = null)
        {
            return CancelCore(request, false, verifyBeforeRollback, () => true);
        }

        private PurchaseInvoiceCancellationOutcome CancelCore(PurchaseInvoiceCancellationRequest request, bool commit,
            Action<SqlConnection, SqlTransaction, PurchaseInvoiceCancellationOutcome> verifyBeforeRollback, Func<bool> current)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            string purchaseId = Required(request.PurchaseId, 50, "رقم فاتورة المشتريات مطلوب.", "رقم فاتورة المشتريات يتجاوز الحد المسموح ({0}).");
            string actor = Required(request.ActorName, 200, "منفذ الإلغاء مطلوب.", "منفذ الإلغاء يتجاوز الحد المسموح ({0}).");
            string reason = (request.Reason ?? string.Empty).Trim();
            if (reason.Length > 500) throw new UiValidationException("سبب الإلغاء يتجاوز الحد المسموح (500).");

            using (SqlConnection connection = connectionFactory())
            {
                connection.Open();
                using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        DateTime cancellationDate = DateTime.Now;
                        CommercialAccountingPeriodPolicy.EnsureBusinessDateOpen(connection, transaction, cancellationDate);
                        PurchaseHeader header = LoadHeader(connection, transaction, purchaseId);
                        if (HasCancellation(connection, transaction, purchaseId))
                            throw new UiValidationException("تم إلغاء فاتورة المشتريات مسبقاً.");

                        List<PurchaseLine> lines = LoadLines(connection, transaction, purchaseId);
                        if (lines.Count == 0 || lines.Any(line => line.Quantity <= 0 || line.UnitCost <= 0m))
                            throw new UiValidationException("لا توجد بنود صالحة وكاملة لعكس فاتورة المشتريات.");
                        decimal detailTotal = lines.Sum(line => line.TotalAmount);
                        if (detailTotal != header.TotalAmount)
                            throw new UiValidationException("إجمالي الفاتورة لا يطابق تفاصيلها؛ أوقف الإلغاء حفاظاً على سلامة البيانات.");

                        List<JournalLineInput> originalJournal = LoadOriginalJournal(connection, transaction, purchaseId);
                        if (originalJournal.Count < 2 || originalJournal.Sum(line => line.Debit) <= 0m ||
                            originalJournal.Sum(line => line.Debit) != originalJournal.Sum(line => line.Credit))
                            throw new UiValidationException("لا يوجد قيد شراء كامل ومتوازن لهذه الفاتورة؛ أوقف الإلغاء.");
                        EnsurePayableHasNoLaterActivity(connection, transaction, purchaseId, originalJournal);

                        bool commercialMode = CommercialSchemaInstaller.IsFullCommercialMode(connection, transaction);
                        int removedQuantity = RemoveInventory(connection, transaction, purchaseId, lines, commercialMode, actor, cancellationDate, reason);

                        string description = "إلغاء فاتورة مشتريات رقم " + purchaseId +
                            (reason.Length == 0 ? string.Empty : " - " + reason);
                        AccountingService.PostPurchaseInvoiceCancellation(connection, transaction, purchaseId,
                            description, originalJournal, cancellationDate, actor,
                LoadOriginalCurrency(connection, transaction, purchaseId));
                        WriteAudit(connection, transaction, request, purchaseId, header, removedQuantity);

                        var outcome = new PurchaseInvoiceCancellationOutcome
                        {
                            PurchaseId = purchaseId,
                            TotalAmount = header.TotalAmount,
                            RemovedQuantity = removedQuantity
                        };
                        if (verifyBeforeRollback != null) verifyBeforeRollback(connection, transaction, outcome);
                        if (commit)
                        {
                            BeforeCommitForVerification?.Invoke();
                            using (SessionCommitGate.Commit(current)) transaction.Commit();
                        }
                        else transaction.Rollback();
                        return outcome;
                    }
                    catch
                    {
                        try { transaction.Rollback(); } catch { }
                        throw;
                    }
                }
            }
        }

        private static PurchaseHeader LoadHeader(SqlConnection connection, SqlTransaction transaction, string purchaseId)
        {
            using (SqlCommand command = new SqlCommand(@"SELECT SupplierName, PurchaseDate, TotalAmount
FROM dbo.PurchasesTb WITH (UPDLOCK, HOLDLOCK) WHERE PurchaseId=@PurchaseId;", connection, transaction))
            {
                command.Parameters.Add("@PurchaseId", SqlDbType.NVarChar, 50).Value = purchaseId;
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (!reader.Read()) throw new UiValidationException("فاتورة المشتريات المحددة غير موجودة.");
                    return new PurchaseHeader
                    {
                        SupplierName = reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
                        PurchaseDate = reader.GetDateTime(1),
                        TotalAmount = reader.GetDecimal(2)
                    };
                }
            }
        }

        private static List<PurchaseLine> LoadLines(SqlConnection connection, SqlTransaction transaction, string purchaseId)
        {
            var lines = new List<PurchaseLine>();
            using (SqlCommand command = new SqlCommand(@"SELECT DetailId, MedName, BuyingPrice, Quantity, TotalAmount, ProdDate, ExpDate
FROM dbo.PurchaseDetailsTb WITH (UPDLOCK, HOLDLOCK) WHERE PurchaseId=@PurchaseId ORDER BY DetailId;", connection, transaction))
            {
                command.Parameters.Add("@PurchaseId", SqlDbType.NVarChar, 50).Value = purchaseId;
                using (SqlDataReader reader = command.ExecuteReader())
                    while (reader.Read())
                        lines.Add(new PurchaseLine
                        {
                            DetailId = reader.GetString(0),
                            MedicineName = reader.GetString(1),
                            UnitCost = Convert.ToDecimal(reader[2]),
                            Quantity = Convert.ToInt32(reader[3]),
                            TotalAmount = Convert.ToDecimal(reader[4]),
                            ProductionDate = reader.IsDBNull(5) ? (DateTime?)null : reader.GetDateTime(5),
                            ExpiryDate = reader.IsDBNull(6) ? (DateTime?)null : reader.GetDateTime(6)
                        });
            }
            foreach(var line in lines)
                line.HasUnitSnapshot=MedicineUnitInvoiceBridge.ReadSnapshot(connection,transaction,"Purchase",line.DetailId)!=null;
            return lines;
        }

        private static string LoadOriginalCurrency(SqlConnection connection, SqlTransaction transaction, string purchaseId)
        {
            using (SqlCommand command = new SqlCommand(@"SELECT TOP(1) e.SystemCurrencyCode
FROM dbo.JournalEntriesTb e WITH (UPDLOCK, HOLDLOCK)
WHERE e.SourceType=N'PurchaseInvoice' AND e.SourceId=@PurchaseId ORDER BY e.JournalEntryId;", connection, transaction))
            {
                command.Parameters.Add("@PurchaseId", SqlDbType.NVarChar, 50).Value = purchaseId;
                object value = command.ExecuteScalar();
                return value == null || value == DBNull.Value ? null : Convert.ToString(value);
            }
        }
        private static List<JournalLineInput> LoadOriginalJournal(SqlConnection connection, SqlTransaction transaction, string purchaseId)
        {
            var lines = new List<JournalLineInput>();
            using (SqlCommand command = new SqlCommand(@"SELECT l.AccountCode,l.Debit,l.Credit,l.LineDescription,l.CostCenterId
FROM dbo.JournalEntriesTb e WITH (UPDLOCK, HOLDLOCK)
JOIN dbo.JournalEntryLinesTb l WITH (UPDLOCK, HOLDLOCK) ON l.JournalEntryId=e.JournalEntryId
WHERE e.SourceType=N'PurchaseInvoice' AND e.SourceId=@PurchaseId ORDER BY l.JournalEntryLineId;", connection, transaction))
            {
                command.Parameters.Add("@PurchaseId", SqlDbType.NVarChar, 50).Value = purchaseId;
                using (SqlDataReader reader = command.ExecuteReader())
                    while (reader.Read())
                        lines.Add(new JournalLineInput
                        {
                            AccountCode = reader.GetString(0),
                            Debit = Convert.ToDecimal(reader[1]),
                            Credit = Convert.ToDecimal(reader[2]),
                            Description = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                            CostCenterId = reader.IsDBNull(4) ? (int?)null : Convert.ToInt32(reader[4])
                        });
            }
            return lines;
        }

        private static bool HasCancellation(SqlConnection connection, SqlTransaction transaction, string purchaseId)
        {
            using (SqlCommand command = new SqlCommand(@"SELECT TOP(1) 1 FROM dbo.JournalEntriesTb WITH (UPDLOCK,HOLDLOCK)
WHERE SourceType=N'PurchaseInvoiceCancellation' AND SourceId=@PurchaseId;", connection, transaction))
            {
                command.Parameters.Add("@PurchaseId", SqlDbType.NVarChar, 50).Value = purchaseId;
                return command.ExecuteScalar() != null;
            }
        }

        private static void EnsurePayableHasNoLaterActivity(SqlConnection connection, SqlTransaction transaction,
            string purchaseId, IEnumerable<JournalLineInput> originalJournal)
        {
            if (!originalJournal.Any(line => string.Equals(line.AccountCode, AccountingService.SuppliersAccount, StringComparison.OrdinalIgnoreCase)
                && line.Credit > 0m)) return;

            using (SqlCommand command = new SqlCommand(@"SELECT TOP(1) 1
FROM dbo.JournalEntriesTb e WITH (UPDLOCK,HOLDLOCK)
JOIN dbo.JournalEntryLinesTb l WITH (UPDLOCK,HOLDLOCK) ON l.JournalEntryId=e.JournalEntryId
WHERE e.JournalEntryId > (SELECT MAX(JournalEntryId) FROM dbo.JournalEntriesTb WHERE SourceType=N'PurchaseInvoice' AND SourceId=@PurchaseId)
  AND l.AccountCode=@SupplierAccount AND e.SourceType<>N'PurchaseInvoiceCancellation';", connection, transaction))
            {
                command.Parameters.Add("@PurchaseId", SqlDbType.NVarChar, 50).Value = purchaseId;
                command.Parameters.Add("@SupplierAccount", SqlDbType.NVarChar, 50).Value = AccountingService.SuppliersAccount;
                if (command.ExecuteScalar() != null)
                    throw new UiValidationException("تعذر إثبات أن رصيد المورد لم يتأثر بحركات لاحقة؛ أوقف الإلغاء وراجع حساب المورد أولاً.");
            }
        }

        private static int RemoveInventory(SqlConnection connection, SqlTransaction transaction, string purchaseId,
            IList<PurchaseLine> lines, bool commercialMode, string actor, DateTime businessDate, string reason)
        {
            if (!commercialMode) EnsureLegacyDateSummaryUnaffected(connection, transaction, lines);
            int removedQuantity = 0;
            foreach (PurchaseLine line in lines)
            {
                int before = InventoryMovementService.GetCurrentQuantity(connection, transaction, line.MedicineName);
                if (before < line.Quantity)
                    throw new UiValidationException("لا يكفي المخزون الحالي لإلغاء كمية {0}.", line.MedicineName);

                if (commercialMode) MarkUnusedLotCancelled(connection, transaction, line);
                using (SqlCommand command = new SqlCommand(@"UPDATE dbo.MedicineTb1 SET MedQty=MedQty-@Quantity
WHERE MedName=@MedName AND MedQty>=@Quantity;", connection, transaction))
                {
                    command.Parameters.Add("@Quantity", SqlDbType.Int).Value = line.Quantity;
                    command.Parameters.Add("@MedName", SqlDbType.NVarChar, 200).Value = line.MedicineName;
                    if (command.ExecuteNonQuery() != 1) throw new UiValidationException("تغير رصيد المخزون؛ أُوقف الإلغاء.");
                }
                InventoryMovementService.Record(connection, transaction, line.MedicineName, -line.Quantity, before,
                    before - line.Quantity, "PurchaseInvoiceCancellation", purchaseId, line.HasUnitSnapshot ? decimal.Round(line.TotalAmount / line.Quantity,2,MidpointRounding.AwayFromZero) : line.UnitCost, null,
                    reason.Length == 0 ? "عكس استلام فاتورة مشتريات" : "عكس استلام فاتورة مشتريات: " + reason,
                    actor, businessDate);
                removedQuantity = checked(removedQuantity + line.Quantity);
            }
            if (commercialMode)
                foreach (string medicineName in lines.Select(line => line.MedicineName).Distinct(StringComparer.OrdinalIgnoreCase))
                    RecomputeMedicineDates(connection, transaction, purchaseId, medicineName, true);
            return removedQuantity;
        }

        private static void EnsureLegacyDateSummaryUnaffected(SqlConnection connection, SqlTransaction transaction,
            IEnumerable<PurchaseLine> lines)
        {
            foreach (IGrouping<string, PurchaseLine> group in lines.GroupBy(line => line.MedicineName, StringComparer.OrdinalIgnoreCase))
            {
                object production;
                object expiry;
                using (SqlCommand command = new SqlCommand(@"SELECT ProdDate,ExpDate FROM dbo.MedicineTb1 WITH (UPDLOCK,HOLDLOCK)
WHERE MedName=@MedName;", connection, transaction))
                {
                    command.Parameters.Add("@MedName", SqlDbType.NVarChar, 200).Value = group.Key;
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (!reader.Read()) throw new UiValidationException("تعذر التحقق من تواريخ الصنف قبل عكس المشتريات.");
                        production = reader.IsDBNull(0) ? (object)DBNull.Value : reader.GetDateTime(0);
                        expiry = reader.IsDBNull(1) ? (object)DBNull.Value : reader.GetDateTime(1);
                    }
                }
                if (production == DBNull.Value || expiry == DBNull.Value ||
                    group.Any(line => !line.ProductionDate.HasValue || !line.ExpiryDate.HasValue ||
                        line.ProductionDate.Value <= Convert.ToDateTime(production) ||
                        line.ExpiryDate.Value <= Convert.ToDateTime(expiry)))
                    throw new UiValidationException("لا يمكن إلغاء هذه الفاتورة في وضع المخزون المجمع؛ قد تكون قد غيّرت تواريخ ملخص الصنف ولا توجد لقطة سابقة محفوظة. راجع الصنف يدويًا.");
            }
        }

        private static void RecomputeMedicineDates(SqlConnection connection, SqlTransaction transaction,
            string purchaseId, string medicineName, bool commercialMode)
        {
            string sql = commercialMode
                ? @"SELECT MIN(ProductionDate),MIN(ExpiryDate) FROM dbo.InventoryLotsTb WITH (UPDLOCK,HOLDLOCK)
WHERE MedName=@MedName AND QuantityRemaining>0;"
                : @"SELECT MIN(d.ProdDate),MIN(d.ExpDate) FROM dbo.PurchaseDetailsTb d WITH (UPDLOCK,HOLDLOCK)
JOIN dbo.PurchasesTb p WITH (UPDLOCK,HOLDLOCK) ON p.PurchaseId=d.PurchaseId
WHERE d.MedName=@MedName AND d.PurchaseId<>@PurchaseId
AND NOT EXISTS (SELECT 1 FROM dbo.JournalEntriesTb c WHERE c.SourceType=N'PurchaseInvoiceCancellation' AND c.SourceId=p.PurchaseId);";
            object production;
            object expiry;
            using (SqlCommand command = new SqlCommand(sql, connection, transaction))
            {
                command.Parameters.Add("@MedName", SqlDbType.NVarChar, 200).Value = medicineName;
                if (!commercialMode) command.Parameters.Add("@PurchaseId", SqlDbType.NVarChar, 50).Value = purchaseId;
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (!reader.Read()) throw new UiValidationException("تعذر إعادة اشتقاق تواريخ المخزون بعد الإلغاء.");
                    production = reader.IsDBNull(0) ? (object)DBNull.Value : reader.GetDateTime(0);
                    expiry = reader.IsDBNull(1) ? (object)DBNull.Value : reader.GetDateTime(1);
                }
            }
            // MedicineTb1.ExpDate is NOT NULL in the legacy schema. Preserve its current
            // summary when no active purchase/lot date remains instead of writing NULL.
            if (expiry == DBNull.Value)
            {
                using (SqlCommand command = new SqlCommand("SELECT ExpDate FROM dbo.MedicineTb1 WITH (UPDLOCK,HOLDLOCK) WHERE MedName=@MedName;", connection, transaction))
                {
                    command.Parameters.Add("@MedName", SqlDbType.NVarChar, 200).Value = medicineName;
                    object currentExpiry = command.ExecuteScalar();
                    if (currentExpiry == null || currentExpiry == DBNull.Value)
                        throw new UiValidationException("تعذر الحفاظ على تاريخ انتهاء الدواء بعد الإلغاء.");
                    expiry = currentExpiry;
                }
            }
            using (SqlCommand command = new SqlCommand(@"UPDATE dbo.MedicineTb1 SET ProdDate=@ProductionDate,ExpDate=@ExpiryDate WHERE MedName=@MedName;", connection, transaction))
            {
                command.Parameters.Add("@ProductionDate", SqlDbType.DateTime).Value = production;
                command.Parameters.Add("@ExpiryDate", SqlDbType.DateTime).Value = expiry;
                command.Parameters.Add("@MedName", SqlDbType.NVarChar, 200).Value = medicineName;
                if (command.ExecuteNonQuery() != 1) throw new UiValidationException("تعذر تحديث تواريخ الدواء بعد إلغاء فاتورة الشراء.");
            }
        }

        private static void MarkUnusedLotCancelled(SqlConnection connection, SqlTransaction transaction, PurchaseLine line)
        {
            Guid lotId;
            decimal received;
            decimal remaining;
            using (SqlCommand command = new SqlCommand(@"SELECT LotId,QuantityReceived,QuantityRemaining
FROM dbo.InventoryLotsTb WITH (UPDLOCK,HOLDLOCK) WHERE PurchaseDetailId=@DetailId;", connection, transaction))
            {
                command.Parameters.Add("@DetailId", SqlDbType.NVarChar, 50).Value = line.DetailId;
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (!reader.Read()) throw new UiValidationException("التشغيلة المرتبطة ببند الشراء مفقودة؛ أوقف الإلغاء.");
                    lotId = reader.GetGuid(0); received = reader.GetDecimal(1); remaining = reader.GetDecimal(2);
                    if (reader.Read()) throw new UiValidationException("وجدت أكثر من تشغيلة للبند الواحد؛ أوقف الإلغاء للمراجعة.");
                }
            }
            if (received != line.Quantity || remaining != received)
                throw new UiValidationException("لا يمكن إلغاء فاتورة استُهلكت تشغيلتها كليًا أو جزئيًا.");
            decimal? exactCost=UnitLotCostBridge.GetRemainingCost(connection,transaction,lotId,checked((int)remaining));
            if(exactCost.HasValue)UnitLotCostBridge.Consume(connection,transaction,lotId,checked((int)remaining),exactCost.Value);
            using (SqlCommand command = new SqlCommand(@"UPDATE dbo.InventoryLotsTb SET QuantityRemaining=0
WHERE LotId=@LotId AND QuantityRemaining=QuantityReceived;", connection, transaction))
            {
                command.Parameters.Add("@LotId", SqlDbType.UniqueIdentifier).Value = lotId;
                if (command.ExecuteNonQuery() != 1) throw new UiValidationException("تغير رصيد التشغيلة أثناء الإلغاء؛ أعد المحاولة.");
            }
        }

        private static void WriteAudit(SqlConnection connection, SqlTransaction transaction,
            PurchaseInvoiceCancellationRequest request, string purchaseId, PurchaseHeader header, int removedQuantity)
        {
            using (SqlCommand command = new SqlCommand(@"INSERT INTO dbo.AuditLogTb(UserName,UserRole,ActionName,EntityName,Details)
VALUES(@UserName,@UserRole,N'PurchaseInvoiceCancelled',N'Purchases',@Details);", connection, transaction))
            {
                command.Parameters.Add("@UserName", SqlDbType.NVarChar, 200).Value = request.ActorName.Trim();
                command.Parameters.Add("@UserRole", SqlDbType.NVarChar, 50).Value = string.IsNullOrWhiteSpace(request.ActorRole) ? "Unknown" : request.ActorRole.Trim();
                command.Parameters.Add("@Details", SqlDbType.NVarChar, 1000).Value =
                    "فاتورة مشتريات " + purchaseId + " للمورد " + header.SupplierName + "؛ المبلغ المعكوس " + header.TotalAmount.ToString("N2") +
                    "؛ الكمية المعكوسة " + removedQuantity + "؛ السبب: " + (string.IsNullOrWhiteSpace(request.Reason) ? "غير محدد" : request.Reason.Trim());
                command.ExecuteNonQuery();
            }
        }

        private static string Required(string value, int maximum, string requiredTemplate, string lengthTemplate)
        {
            string normalized = (value ?? string.Empty).Trim();
            if (normalized.Length == 0) throw new UiValidationException(requiredTemplate);
            if (normalized.Length > maximum) throw new UiValidationException(lengthTemplate, maximum);
            return normalized;
        }

        private sealed class PurchaseHeader { public string SupplierName; public DateTime PurchaseDate; public decimal TotalAmount; }
        private sealed class PurchaseLine
        {
            public bool HasUnitSnapshot; public string DetailId; public string MedicineName; public decimal UnitCost; public int Quantity; public decimal TotalAmount;
            public DateTime? ProductionDate; public DateTime? ExpiryDate;
        }
    }
}

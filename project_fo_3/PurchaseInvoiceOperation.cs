using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using PharmacyManagement.Application;

namespace project_fo_3
{
    internal sealed class PurchaseAttemptAbandonedException : InvalidOperationException
    {
        internal Guid RequestId { get; }
        internal PurchaseAttemptAbandonedException(Guid requestId) : base("أُغلقت محاولة المشتريات دون اعتماد؛ لا يمكن إعادة إرسالها.") { RequestId = requestId; }
    }

      internal sealed class PurchaseReceiptObservation { internal PurchaseInvoiceResult Result {get;set;} internal bool IsCancelled {get;set;} internal bool IsAbandoned {get;set;} }
    internal sealed partial class PurchaseInvoiceOperation : IPurchaseInvoiceOperation
    {
        private readonly Func<SqlConnection> connectionFactory;

        internal PurchaseInvoiceOperation()
            : this(Database.CreateConnection)
        {
        }

        internal PurchaseInvoiceOperation(Func<SqlConnection> connectionFactory)
        {
            this.connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        }

        public PurchaseInvoiceResult Issue(PurchaseInvoiceRequest request)
        {
            if (!PermissionService.CanExecute(AppOperation.PostPurchaseInvoice))
                throw new UnauthorizedAccessException("لا تملك صلاحية اعتماد فاتورة مشتريات.");
            return IssueCore(request, true);
        }

        internal PurchaseInvoiceResult IssueForVerificationRollback(PurchaseInvoiceRequest request,
            Action<SqlConnection, SqlTransaction, string> verifyBeforeRollback = null)
        {
            return IssueCore(request, false, verifyBeforeRollback);
        }

        internal PurchaseInvoiceResult IssueForVerificationCommitted(PurchaseInvoiceRequest request)
        {
            return IssueCore(request, true);
        }

        internal PurchaseInvoiceResult IssueIdentified(PurchaseWriteIntent identity,Action<PurchaseInvoiceResult> afterCommit=null)
        {
            if(identity==null)throw new ArgumentNullException(nameof(identity));
            var owner=identity.Owner;long revision=SessionContext.CurrentSessionRevision;
            string source=Database.ConnectionString;
            if(!string.Equals(identity.SourceFingerprint,PurchaseWriteSourceFingerprint.Compute(source),StringComparison.Ordinal))throw new UnauthorizedAccessException("مصدر محاولة المشتريات لا يطابق الاتصال الحالي.");
            Func<bool> current=()=>SessionContext.IsAuthenticated&&SessionContext.CurrentSessionRevision==revision&&
                string.Equals(SessionContext.CurrentEmployeeId,identity.OwnerEmployeeId,StringComparison.Ordinal)&&
                string.Equals(SessionContext.CurrentUser,owner.UserName,StringComparison.OrdinalIgnoreCase)&&
                string.Equals(SessionContext.CurrentRole,owner.UserRole,StringComparison.OrdinalIgnoreCase)&&
                string.Equals(Database.ConnectionString,source,StringComparison.Ordinal)&&PermissionService.CanExecute(AppOperation.PostPurchaseInvoice);
            if(!current())throw new UnauthorizedAccessException("تغير منفذ محاولة المشتريات أو صلاحياته.");
            var result=IssueCore(identity.Snapshot(),true,null,identity,current,source);
            afterCommit?.Invoke(result);
            return result;
        }
        // Historical committed receipt only. Null does not authorize discarding or retrying a pending request.
        internal PurchaseInvoiceResult RecoverIdentified(PurchaseWriteIntent identity) { return ObserveIdentified(identity)?.Result; }
        // Closes an attempt that has no server receipt. A committed purchase is never abandoned.
        internal PurchaseReceiptObservation AbandonUnposted(PurchaseWriteIntent identity)
        {
            if (identity == null) throw new ArgumentNullException(nameof(identity));
            var owner = identity.Owner; long revision = SessionContext.CurrentSessionRevision;
            string source = Database.ConnectionString;
            if (!string.Equals(identity.SourceFingerprint, PurchaseWriteSourceFingerprint.Compute(source), StringComparison.Ordinal))
                throw new UnauthorizedAccessException("مصدر محاولة المشتريات لا يطابق الاتصال الحالي.");
            Func<bool> current = () => SessionContext.IsAuthenticated && SessionContext.CurrentSessionRevision == revision &&
                string.Equals(SessionContext.CurrentEmployeeId, identity.OwnerEmployeeId, StringComparison.Ordinal) &&
                string.Equals(SessionContext.CurrentUser, owner.UserName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(SessionContext.CurrentRole, owner.UserRole, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Database.ConnectionString, source, StringComparison.Ordinal) && PermissionService.CanOpen(AppPermission.Purchases);
            if (!current()) throw new UnauthorizedAccessException("تغير صاحب محاولة استعادة المشتريات أو صلاحية فتح الشاشة.");
            using (var connection = connectionFactory())
            {
                if (!string.Equals(new SqlConnectionStringBuilder(connection.ConnectionString).ConnectionString,
                    new SqlConnectionStringBuilder(source).ConnectionString, StringComparison.Ordinal))
                    throw new UnauthorizedAccessException("تغير مصدر استعادة المشتريات.");
                connection.Open();
                if (!current()) throw new UnauthorizedAccessException("تغيرت جلسة استعادة المشتريات.");
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        PurchaseInvoiceResult committed = PurchaseWriteReceiptStore.AcquireAndRead(connection, transaction, identity);
                        if (committed != null)
                        {
                            transaction.Rollback();
                            throw new InvalidOperationException("اعتُمدت فاتورة مشتريات لهذه المحاولة؛ لا يمكن إغلاقها.");
                        }
                        PurchaseWriteReceiptStore.RecordAbandoned(connection, transaction, identity);
                        transaction.Commit();
                        return new PurchaseReceiptObservation { IsAbandoned = true };
                    }
                    catch
                    {
                        try { transaction.Rollback(); } catch { }
                        throw;
                    }
                }
            }
        }

        internal PurchaseReceiptObservation ObserveIdentified(PurchaseWriteIntent identity)
        {
            if(identity==null)throw new ArgumentNullException(nameof(identity));
            var owner=identity.Owner;long revision=SessionContext.CurrentSessionRevision;
            string source=Database.ConnectionString;
            if(!string.Equals(identity.SourceFingerprint,PurchaseWriteSourceFingerprint.Compute(source),StringComparison.Ordinal))
                throw new UnauthorizedAccessException("مصدر استعادة المشتريات لا يطابق الاتصال الحالي.");
            Func<bool> current=()=>SessionContext.IsAuthenticated&&SessionContext.CurrentSessionRevision==revision&&
                string.Equals(SessionContext.CurrentEmployeeId,identity.OwnerEmployeeId,StringComparison.Ordinal)&&
                string.Equals(SessionContext.CurrentUser,owner.UserName,StringComparison.OrdinalIgnoreCase)&&
                string.Equals(SessionContext.CurrentRole,owner.UserRole,StringComparison.OrdinalIgnoreCase)&&
                string.Equals(Database.ConnectionString,source,StringComparison.Ordinal)&&PermissionService.CanOpen(AppPermission.Purchases);
            if(!current())throw new UnauthorizedAccessException("تغير صاحب محاولة استعادة المشتريات أو صلاحية فتح الشاشة.");
            using(var connection=connectionFactory())
            {
                if(!string.Equals(new SqlConnectionStringBuilder(connection.ConnectionString).ConnectionString,
                    new SqlConnectionStringBuilder(source).ConnectionString,StringComparison.Ordinal))
                    throw new UnauthorizedAccessException("تغير مصدر استعادة المشتريات.");
                connection.Open();
                if(!current())throw new UnauthorizedAccessException("تغيرت جلسة استعادة المشتريات.");
                using(var transaction=connection.BeginTransaction())
                {
                    try
                    {
                        var result=PurchaseWriteReceiptStore.AcquireAndRead(connection,transaction,identity);
                        if(!current())throw new UnauthorizedAccessException("تغيرت جلسة استعادة المشتريات.");
                        var observation=result==null?null:new PurchaseReceiptObservation{Result=result,IsCancelled=PurchaseWriteReceiptStore.IsCancelled(connection,transaction,result)};
                        transaction.Rollback();
                        return observation;
                    }
                    catch {try{transaction.Rollback();}catch{}throw;}
                }
            }
        }
        internal async System.Threading.Tasks.Task<PurchaseReceiptObservation> ObserveIdentifiedAsync(PurchaseWriteIntent identity,System.Threading.CancellationToken cancellationToken)
        {
            if(identity==null)throw new ArgumentNullException(nameof(identity));
            var owner=identity.Owner;long revision=SessionContext.CurrentSessionRevision;
            string source=Database.ConnectionString;
            if(!string.Equals(identity.SourceFingerprint,PurchaseWriteSourceFingerprint.Compute(source),StringComparison.Ordinal))
                throw new UnauthorizedAccessException("مصدر استعادة المشتريات لا يطابق الاتصال الحالي.");
            Func<bool> current=()=>SessionContext.IsAuthenticated&&SessionContext.CurrentSessionRevision==revision&&
                string.Equals(SessionContext.CurrentEmployeeId,identity.OwnerEmployeeId,StringComparison.Ordinal)&&
                string.Equals(SessionContext.CurrentUser,owner.UserName,StringComparison.OrdinalIgnoreCase)&&
                string.Equals(SessionContext.CurrentRole,owner.UserRole,StringComparison.OrdinalIgnoreCase)&&
                string.Equals(Database.ConnectionString,source,StringComparison.Ordinal)&&PermissionService.CanOpen(AppPermission.Purchases);
            if(!current())throw new UnauthorizedAccessException("تغير صاحب محاولة استعادة المشتريات أو صلاحية فتح الشاشة.");
            cancellationToken.ThrowIfCancellationRequested();
            await System.Threading.Tasks.Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            if(!current())throw new UnauthorizedAccessException("تغيرت جلسة استعادة المشتريات.");
            using(var connection=connectionFactory())
            {
                if(!string.Equals(new SqlConnectionStringBuilder(connection.ConnectionString).ConnectionString,
                    new SqlConnectionStringBuilder(source).ConnectionString,StringComparison.Ordinal))
                    throw new UnauthorizedAccessException("تغير مصدر استعادة المشتريات.");
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                if(!current())throw new UnauthorizedAccessException("تغيرت جلسة استعادة المشتريات.");
                using(var transaction=connection.BeginTransaction())
                {
                    try
                    {
                        var result=await PurchaseWriteReceiptStore.AcquireAndReadAsync(connection,transaction,identity,cancellationToken).ConfigureAwait(false);
                        if(!current())throw new UnauthorizedAccessException("تغيرت جلسة استعادة المشتريات.");
                        var observation=result==null?null:new PurchaseReceiptObservation{Result=result,IsCancelled=await PurchaseWriteReceiptStore.IsCancelledAsync(connection,transaction,result,cancellationToken).ConfigureAwait(false)};
                        cancellationToken.ThrowIfCancellationRequested();
                        if(!current())throw new UnauthorizedAccessException("تغيرت جلسة استعادة المشتريات.");
                        transaction.Rollback();
                        return observation;
                    }
                    catch {try{transaction.Rollback();}catch{}cancellationToken.ThrowIfCancellationRequested();throw;}
                }
            }
        }
        private PurchaseInvoiceResult IssueCore(PurchaseInvoiceRequest request, bool commit,
            Action<SqlConnection, SqlTransaction, string> verifyBeforeRollback = null, PurchaseWriteIntent identity=null, Func<bool> identityCurrent=null, string expectedSource=null)
        {
            DateTime now = request != null && request.PurchaseDate != default(DateTime) ? request.PurchaseDate : DateTime.Now;
            PurchaseInvoiceRequest normalized = PurchaseInvoiceValidation.Normalize(request, now.Date);
            decimal totalAmount = PurchaseInvoiceValidation.CalculateTotal(normalized.Lines);

            using (SqlConnection connection = connectionFactory())
            {
                if(identity!=null&&!string.Equals(new SqlConnectionStringBuilder(connection.ConnectionString).ConnectionString,new SqlConnectionStringBuilder(expectedSource).ConnectionString,StringComparison.Ordinal))throw new UnauthorizedAccessException("تغير مصدر محاولة المشتريات.");
                connection.Open();
                if(identity!=null&&!identityCurrent())throw new UnauthorizedAccessException("تغيرت جلسة محاولة المشتريات.");
                using (SqlTransaction transaction = connection.BeginTransaction())
                {
                    try
                    {
                        if(identity!=null){
                            if(!identityCurrent())throw new UnauthorizedAccessException("تغيرت جلسة محاولة المشتريات.");
                            var previous=PurchaseWriteReceiptStore.AcquireAndRead(connection,transaction,identity);
                            if(previous!=null){
                                if(!identityCurrent())throw new UnauthorizedAccessException("تغيرت جلسة استعادة المشتريات.");
                                if(PurchaseWriteReceiptStore.IsCancelled(connection,transaction,previous))throw new InvalidOperationException("تم تنفيذ محاولة المشتريات ثم إلغاء الفاتورة؛ لا يمكن إعادة اعتمادها.");
                                transaction.Rollback();return previous;
                            }
                        }
                        CommercialAccountingPeriodPolicy.EnsureBusinessDateOpen(connection, transaction, now);
                        ImportedInvoiceApprovalGuard.EnsureDraftCurrency(connection, transaction, normalized.ExpectedSystemCurrencyCode);
                        ImportedInvoiceApprovalGuard.EnsureCurrency(connection, transaction, normalized.ImportCurrencyCode);
                        ImportedInvoiceApprovalGuard.EnsurePurchaseTax(connection, transaction, normalized.ImportTaxRatePercent);
                        string purchaseId = normalized.DesiredPurchaseId;
                        if (purchaseId.Length == 0 || IdentifierExists(connection, transaction, "PurchasesTb", "PurchaseId", purchaseId))
                            purchaseId = Database.GetNextSequenceValue(connection, transaction, "Seq_PurchaseId").ToString();

                        InsertHeader(connection, transaction, purchaseId, normalized, now, totalAmount);
                        CommercialSchemaInstaller.EnsureRequestedCommercialModeIsReady(connection, transaction);
                        bool commercialEnabled = CommercialSchemaInstaller.IsFullCommercialMode(connection, transaction);
                        if (commercialEnabled != normalized.CommercialInventoryExpected)
                            throw new InvalidOperationException("تغير وضع مخزون الدفعات منذ فتح شاشة المشتريات. أعد فتح الشاشة ثم راجع البنود قبل الحفظ؛ لم تُسجل أي فاتورة.");

                        var reservedIds = new HashSet<long>();
                        var results = new List<PurchaseInvoiceLineResult>(normalized.Lines.Count);
                        foreach (PurchaseInvoiceLineRequest line in normalized.Lines)
                        {
                            string detailId = ResolveDetailId(connection, transaction, line.DesiredDetailId, reservedIds);
                            MedicineUnitInvoiceBridge.ValidateQuote(connection,transaction,line.MedicineName,line.Unit,false,line.Quantity,normalized.ExpectedSystemCurrencyCode ?? Database.GetSystemCurrency().CurrencyCode);
                            InsertLine(connection, transaction, purchaseId, detailId, line);
                            MedicineUnitInvoiceBridge.SaveSnapshot(connection,transaction,"Purchase",detailId,line.MedicineName,line.Unit);
                            int quantityBefore = InventoryMovementService.GetCurrentQuantity(connection, transaction, line.MedicineName);
                            UpdateStock(connection, transaction, line);
                            InventoryMovementService.Record(connection, transaction, line.MedicineName, line.Quantity,
                                quantityBefore, checked(quantityBefore + line.Quantity), "PurchaseInvoice", purchaseId,
                                line.Unit == null ? line.BuyingPrice : decimal.Round(line.Unit.GrossTotal / line.Quantity, 2, MidpointRounding.AwayFromZero), null, line.Unit == null ? "زيادة مخزون من فاتورة مشتريات" : "زيادة مخزون بالوحدة الأساسية؛ " + line.Unit.EnteredQuantity.ToString(System.Globalization.CultureInfo.InvariantCulture) + " " + line.Unit.Name + "؛ تكلفة البند الأصلية " + line.Unit.GrossTotal.ToString(System.Globalization.CultureInfo.InvariantCulture), normalized.EmployeeName, now);
                            if (commercialEnabled)
                                {
                                CreateCommercialLot(connection, transaction, line, detailId, normalized.SupplierName);
                                if(line.Unit!=null)UnitLotCostBridge.Register(connection,transaction,detailId,line.Quantity,line.Unit.GrossTotal);
                            }
                            results.Add(new PurchaseInvoiceLineResult { LineNumber = line.LineNumber, DetailId = detailId });
                        }

                        AccountingService.PostPurchaseInvoiceWithCurrency(connection, transaction, purchaseId, totalAmount,
                            "فاتورة شراء رقم " + purchaseId, now, normalized.EmployeeName,
                            normalized.ImportPaymentMethod.Length == 0 ? "Cash" : normalized.ImportPaymentMethod,
                            normalized.ExpectedSystemCurrencyCode??(string.IsNullOrWhiteSpace(normalized.ImportCurrencyCode)?null:normalized.ImportCurrencyCode));
                        InsertAudit(connection, transaction, normalized, purchaseId, totalAmount);
                        ImportedInvoiceSourceRegistry.Record(connection, transaction, "Purchase", normalized.ImportSourceSystem,
                            normalized.ImportSourceInvoiceId, purchaseId);
                        ImportedInvoiceApprovalGuard.MarkDraftApproved(connection, transaction, normalized.ImportDraftId,
                            "Purchase", normalized.ImportSourceSystem, normalized.ImportSourceInvoiceId, purchaseId);
                        var outcome=new PurchaseInvoiceResult{PurchaseId=purchaseId,TotalAmount=totalAmount,Lines=results.AsReadOnly()};
                        if(identity!=null){
                            if(!identityCurrent())throw new UnauthorizedAccessException("تغيرت جلسة محاولة المشتريات قبل الاعتماد.");
                            PurchaseCommitAuthority.Acquire(connection,transaction,identity,true);
            PurchaseWriteReceiptStore.Record(connection,transaction,identity,outcome);
                        }
                        if (!commit && verifyBeforeRollback != null)
                            verifyBeforeRollback(connection, transaction, purchaseId);
                        if (commit)
            {
                if (identity != null) { using (SessionCommitGate.Commit(identityCurrent)) { transaction.Commit(); } }
                else transaction.Commit();
            }
                        else transaction.Rollback();
                        return outcome;
                    }
                    catch
                    {
                        try{transaction.Rollback();}catch{}
                        throw;
                    }
                }
            }
        }

        private static void InsertHeader(SqlConnection connection, SqlTransaction transaction, string purchaseId,
            PurchaseInvoiceRequest request, DateTime purchaseDate, decimal totalAmount)
        {
            using (SqlCommand command = new SqlCommand(@"INSERT INTO dbo.PurchasesTb
(PurchaseId, SupplierName, PurchaseDate, TotalAmount, EmployeeName, Notes)
VALUES (@PurchaseId, @SupplierName, @PurchaseDate, @TotalAmount, @EmployeeName, @Notes);", connection, transaction))
            {
                command.Parameters.Add("@PurchaseId", SqlDbType.NVarChar, 50).Value = purchaseId;
                command.Parameters.Add("@SupplierName", SqlDbType.NVarChar, 200).Value = request.SupplierName;
                command.Parameters.Add("@PurchaseDate", SqlDbType.DateTime).Value = purchaseDate;
                Decimal(command, "@TotalAmount", totalAmount, 2);
                command.Parameters.Add("@EmployeeName", SqlDbType.NVarChar, 200).Value = request.EmployeeName;
                command.Parameters.Add("@Notes", SqlDbType.NVarChar, 1000).Value = request.Notes.Length == 0 ? (object)DBNull.Value : request.Notes;
                command.ExecuteNonQuery();
            }
        }

        private static void InsertLine(SqlConnection connection, SqlTransaction transaction, string purchaseId,
            string detailId, PurchaseInvoiceLineRequest line)
        {
            using (SqlCommand command = new SqlCommand(@"INSERT INTO dbo.PurchaseDetailsTb
(DetailId, PurchaseId, MedName, BuyingPrice, Quantity, TotalAmount, ProdDate, ExpDate)
VALUES (@DetailId, @PurchaseId, @MedName, @BuyingPrice, @Quantity, @TotalAmount, @ProdDate, @ExpDate);", connection, transaction))
            {
                command.Parameters.Add("@DetailId", SqlDbType.NVarChar, 50).Value = detailId;
                command.Parameters.Add("@PurchaseId", SqlDbType.NVarChar, 50).Value = purchaseId;
                command.Parameters.Add("@MedName", SqlDbType.NVarChar, 200).Value = line.MedicineName;
                Decimal(command, "@BuyingPrice", line.Unit?.UnitPrice ?? line.BuyingPrice, 2);
                command.Parameters.Add("@Quantity", SqlDbType.Int).Value = line.Quantity;
                Decimal(command, "@TotalAmount", line.Unit?.GrossTotal ?? checked(line.BuyingPrice * line.Quantity), 2);
                command.Parameters.Add("@ProdDate", SqlDbType.Date).Value = line.ProductionDate;
                command.Parameters.Add("@ExpDate", SqlDbType.Date).Value = line.ExpiryDate;
                command.ExecuteNonQuery();
            }
        }

        private static void UpdateStock(SqlConnection connection, SqlTransaction transaction, PurchaseInvoiceLineRequest line)
        {
            using (SqlCommand command = new SqlCommand(@"UPDATE dbo.MedicineTb1
SET MedQty=MedQty+@Quantity,
    ProdDate=CASE WHEN ProdDate IS NULL OR ProdDate>@ProdDate THEN @ProdDate ELSE ProdDate END,
    ExpDate=CASE WHEN ExpDate IS NULL OR ExpDate>@ExpDate THEN @ExpDate ELSE ExpDate END
WHERE MedName=@MedName;", connection, transaction))
            {
                command.Parameters.Add("@Quantity", SqlDbType.Int).Value = line.Quantity;
                command.Parameters.Add("@ProdDate", SqlDbType.Date).Value = line.ProductionDate;
                command.Parameters.Add("@ExpDate", SqlDbType.Date).Value = line.ExpiryDate;
                command.Parameters.Add("@MedName", SqlDbType.NVarChar, 200).Value = line.MedicineName;
                if (command.ExecuteNonQuery() != 1)
                    throw new InvalidOperationException("لم يتم العثور على الدواء أثناء تحديث مخزون المشتريات: " + line.MedicineName);
            }
        }

        private static string ResolveDetailId(SqlConnection connection, SqlTransaction transaction, string desiredId, HashSet<long> reservedIds)
        {
            long numeric;
            if (!long.TryParse(desiredId, out numeric) || reservedIds.Contains(numeric) ||
                IdentifierExists(connection, transaction, "PurchaseDetailsTb", "DetailId", desiredId))
            {
                do { numeric = Database.GetNextSequenceValue(connection, transaction, "Seq_PurchaseDetailId"); }
                while (reservedIds.Contains(numeric));
            }
            reservedIds.Add(numeric);
            return numeric.ToString();
        }

        private static bool IdentifierExists(SqlConnection connection, SqlTransaction transaction, string table, string column, string value)
        {
            using (SqlCommand command = new SqlCommand("SELECT COUNT(1) FROM dbo." + table + " WITH (UPDLOCK,HOLDLOCK) WHERE " + column + "=@Value;", connection, transaction))
            {
                command.Parameters.Add("@Value", SqlDbType.NVarChar, 50).Value = value ?? string.Empty;
                return Convert.ToInt32(command.ExecuteScalar()) > 0;
            }
        }

        private static void CreateCommercialLot(SqlConnection connection, SqlTransaction transaction,
            PurchaseInvoiceLineRequest line, string detailId, string supplierName)
        {
            if (line.Gtin.Length > 0) Gs1DataMatrixParser.ParseHumanReadable("(01)" + line.Gtin);
            using (SqlCommand duplicate = new SqlCommand(@"SELECT COUNT(*) FROM dbo.InventoryLotsTb WITH (UPDLOCK,HOLDLOCK)
WHERE MedName=@MedName AND BatchNumber=@BatchNumber AND ExpiryDate=@ExpiryDate
AND ISNULL(SerialNumber,N'')=@SerialNumber;", connection, transaction))
            {
                duplicate.Parameters.Add("@MedName", SqlDbType.NVarChar, 200).Value = line.MedicineName;
                duplicate.Parameters.Add("@BatchNumber", SqlDbType.NVarChar, 100).Value = line.BatchNumber;
                duplicate.Parameters.Add("@ExpiryDate", SqlDbType.Date).Value = line.ExpiryDate;
                duplicate.Parameters.Add("@SerialNumber", SqlDbType.NVarChar, 100).Value = line.SerialNumber;
                if (Convert.ToInt32(duplicate.ExecuteScalar()) > 0)
                    throw new InvalidOperationException("هذه التشغيلة مسجلة بالفعل. استخدم تسوية مخزون موثقة بدلاً من استلامها مرة أخرى.");
            }

            using (SqlCommand insert = new SqlCommand(@"INSERT INTO dbo.InventoryLotsTb
(LotId,MedName,Gtin,SerialNumber,BatchNumber,ProductionDate,ExpiryDate,SupplierName,PurchaseDetailId,UnitCost,QuantityReceived,QuantityRemaining)
VALUES (@LotId,@MedName,@Gtin,@SerialNumber,@BatchNumber,@ProductionDate,@ExpiryDate,@SupplierName,@PurchaseDetailId,@UnitCost,@Quantity,@Quantity);", connection, transaction))
            {
                insert.Parameters.Add("@LotId", SqlDbType.UniqueIdentifier).Value = Guid.NewGuid();
                insert.Parameters.Add("@MedName", SqlDbType.NVarChar, 200).Value = line.MedicineName;
                insert.Parameters.Add("@Gtin", SqlDbType.NVarChar, 20).Value = line.Gtin.Length == 0 ? (object)DBNull.Value : line.Gtin;
                insert.Parameters.Add("@SerialNumber", SqlDbType.NVarChar, 100).Value = line.SerialNumber.Length == 0 ? (object)DBNull.Value : line.SerialNumber;
                insert.Parameters.Add("@BatchNumber", SqlDbType.NVarChar, 100).Value = line.BatchNumber;
                insert.Parameters.Add("@ProductionDate", SqlDbType.Date).Value = line.ProductionDate;
                insert.Parameters.Add("@ExpiryDate", SqlDbType.Date).Value = line.ExpiryDate;
                insert.Parameters.Add("@SupplierName", SqlDbType.NVarChar, 200).Value = supplierName;
                insert.Parameters.Add("@PurchaseDetailId", SqlDbType.NVarChar, 50).Value = detailId;
                Decimal(insert, "@UnitCost", line.Unit == null ? line.BuyingPrice : decimal.Round(line.Unit.GrossTotal / line.Quantity, 2, MidpointRounding.AwayFromZero), 2);
                Decimal(insert, "@Quantity", line.Quantity, 3);
                insert.ExecuteNonQuery();
            }
        }

        private static void InsertAudit(SqlConnection connection, SqlTransaction transaction, PurchaseInvoiceRequest request,
            string purchaseId, decimal totalAmount)
        {
            using (SqlCommand command = new SqlCommand(@"INSERT INTO dbo.AuditLogTb
(UserName,UserRole,ActionName,EntityName,Details)
VALUES (@UserName,@UserRole,N'PurchaseInvoiceSaved',N'Purchases',@Details);", connection, transaction))
            {
                command.Parameters.Add("@UserName", SqlDbType.NVarChar, 200).Value = request.EmployeeName;
                command.Parameters.Add("@UserRole", SqlDbType.NVarChar, 50).Value = request.ActorRole.Length == 0 ? "Unknown" : request.ActorRole;
                command.Parameters.Add("@Details", SqlDbType.NVarChar, 1000).Value =
                    "فاتورة مشتريات محفوظة. الرقم: " + purchaseId + "، المورد: " + request.SupplierName +
                    "، الإجمالي: " + totalAmount.ToString("N2") + "، عدد البنود: " + request.Lines.Count + ".";
                command.ExecuteNonQuery();
            }
        }

        private static void Decimal(SqlCommand command, string name, decimal value, byte scale)
        {
            SqlParameter parameter = command.Parameters.Add(name, SqlDbType.Decimal);
            parameter.Precision = 18;
            parameter.Scale = scale;
            parameter.Value = value;
        }
    }
}









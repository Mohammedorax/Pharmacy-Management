using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using PharmacyManagement.Application;
namespace project_fo_3 {
 internal sealed class PurchaseWriteOutcomeUnknownException:Exception { internal Guid RequestId {get;private set;} internal PurchaseWriteOutcomeUnknownException(Guid id,Exception inner):base("لم تثبت نتيجة اعتماد المشتريات؛ تحقق من المحاولة المحفوظة قبل إعادة الإرسال.",inner){RequestId=id;} }
 internal sealed partial class PurchaseInvoiceOperation {
        internal async System.Threading.Tasks.Task<PurchaseInvoiceResult> IssueIdentifiedAsync(PurchaseWriteIntent identity,System.Threading.CancellationToken cancellationToken,Action<PurchaseInvoiceResult> afterCommit=null)
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
            cancellationToken.ThrowIfCancellationRequested();
            await System.Threading.Tasks.Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            if(!current())throw new UnauthorizedAccessException("تغيرت جلسة حفظ المشتريات.");
            var result=await IssueCoreAsync(identity.Snapshot(),true,null,identity,current,source,cancellationToken).ConfigureAwait(false);
            afterCommit?.Invoke(result);
            return result;
        }
        private async System.Threading.Tasks.Task<PurchaseInvoiceResult> IssueCoreAsync(PurchaseInvoiceRequest request, bool commit,
            Action<SqlConnection, SqlTransaction, string> verifyBeforeRollback = null, PurchaseWriteIntent identity=null, Func<bool> identityCurrent=null, string expectedSource=null,System.Threading.CancellationToken cancellationToken=default(System.Threading.CancellationToken))
        {
            DateTime now = request != null && request.PurchaseDate != default(DateTime) ? request.PurchaseDate : DateTime.Now;
            PurchaseInvoiceRequest normalized = PurchaseInvoiceValidation.Normalize(request, now.Date);
            decimal totalAmount = PurchaseInvoiceValidation.CalculateTotal(normalized.Lines);

            using (SqlConnection connection = connectionFactory())
            {
                if(identity!=null&&!string.Equals(new SqlConnectionStringBuilder(connection.ConnectionString).ConnectionString,new SqlConnectionStringBuilder(expectedSource).ConnectionString,StringComparison.Ordinal))throw new UnauthorizedAccessException("تغير مصدر محاولة المشتريات.");
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                if(identity!=null&&!identityCurrent())throw new UnauthorizedAccessException("تغيرت جلسة محاولة المشتريات.");
                using (SqlTransaction transaction = connection.BeginTransaction())
                {
                    bool commitStarted=false;
                    try
                    {
                        if(identity!=null){
                            if(!identityCurrent())throw new UnauthorizedAccessException("تغيرت جلسة محاولة المشتريات.");
                            var previous=await PurchaseWriteReceiptStore.AcquireAndReadAsync(connection,transaction,identity,cancellationToken).ConfigureAwait(false);
                            if(previous!=null){
                                if(!identityCurrent())throw new UnauthorizedAccessException("تغيرت جلسة استعادة المشتريات.");
                                if(await PurchaseWriteReceiptStore.IsCancelledAsync(connection,transaction,previous,cancellationToken).ConfigureAwait(false))throw new InvalidOperationException("تم تنفيذ محاولة المشتريات ثم إلغاء الفاتورة؛ لا يمكن إعادة اعتمادها.");
                                transaction.Rollback();return previous;
                            }
                        }
                        await PurchaseAsyncBusinessPeriodPolicy.EnsureBusinessDateOpenAsync(connection, transaction, now,cancellationToken).ConfigureAwait(false);
                        await PurchaseAsyncImportGuard.EnsureDraftCurrencyAsync(connection, transaction, normalized.ExpectedSystemCurrencyCode,cancellationToken).ConfigureAwait(false);
                        await PurchaseAsyncImportGuard.EnsureCurrencyAsync(connection, transaction, normalized.ImportCurrencyCode,cancellationToken).ConfigureAwait(false);
                        await PurchaseAsyncAccountingService.EnsurePurchaseTaxAsync(connection, transaction, normalized.ImportTaxRatePercent,cancellationToken).ConfigureAwait(false);
                        string purchaseId = normalized.DesiredPurchaseId;
                        if (purchaseId.Length == 0 || await IdentifierExistsAsync(connection, transaction, "PurchasesTb", "PurchaseId", purchaseId,cancellationToken).ConfigureAwait(false))
                            purchaseId = (await NextPurchaseSequenceAsync(connection, transaction, "Seq_PurchaseId",cancellationToken).ConfigureAwait(false)).ToString();

                        await InsertHeaderAsync(connection, transaction, purchaseId, normalized, now, totalAmount,cancellationToken).ConfigureAwait(false);
                        bool commercialEnabled = await PurchaseAsyncInventoryMode.EnsureRequestedModeAndReadAsync(connection,transaction,cancellationToken).ConfigureAwait(false);
                        if (commercialEnabled != normalized.CommercialInventoryExpected)
                            throw new InvalidOperationException("تغير وضع مخزون الدفعات منذ فتح شاشة المشتريات. أعد فتح الشاشة ثم راجع البنود قبل الحفظ؛ لم تُسجل أي فاتورة.");

                        var reservedIds = new HashSet<long>();
                        var results = new List<PurchaseInvoiceLineResult>(normalized.Lines.Count);
                        foreach (PurchaseInvoiceLineRequest line in normalized.Lines)
                        {
                            string detailId = await ResolveDetailIdAsync(connection, transaction, line.DesiredDetailId, reservedIds,cancellationToken).ConfigureAwait(false);
                            cancellationToken.ThrowIfCancellationRequested();
                            MedicineUnitInvoiceBridge.ValidateQuote(connection,transaction,line.MedicineName,line.Unit,false,line.Quantity,normalized.ExpectedSystemCurrencyCode ?? Database.GetSystemCurrency().CurrencyCode);
                            await InsertLineAsync(connection, transaction, purchaseId, detailId, line,cancellationToken).ConfigureAwait(false);
                            MedicineUnitInvoiceBridge.SaveSnapshot(connection,transaction,"Purchase",detailId,line.MedicineName,line.Unit);
                            int quantityBefore = await PurchaseAsyncInventoryMovementWriter.GetCurrentQuantityAsync(connection, transaction, line.MedicineName,cancellationToken).ConfigureAwait(false);
                            await UpdateStockAsync(connection, transaction, line,cancellationToken).ConfigureAwait(false);
                            await PurchaseAsyncInventoryMovementWriter.RecordAsync(connection, transaction, line.MedicineName, line.Quantity,
                                quantityBefore, checked(quantityBefore + line.Quantity), "PurchaseInvoice", purchaseId,
                                line.Unit == null ? line.BuyingPrice : decimal.Round(line.Unit.GrossTotal / line.Quantity, 2, MidpointRounding.AwayFromZero), null, line.Unit == null ? "زيادة مخزون من فاتورة مشتريات" : "زيادة مخزون بالوحدة الأساسية؛ " + line.Unit.EnteredQuantity.ToString(System.Globalization.CultureInfo.InvariantCulture) + " " + line.Unit.Name + "؛ تكلفة البند الأصلية " + line.Unit.GrossTotal.ToString(System.Globalization.CultureInfo.InvariantCulture), normalized.EmployeeName, now,cancellationToken).ConfigureAwait(false);
                            if (commercialEnabled)
                                {
                                await CreateCommercialLotAsync(connection, transaction, line, detailId, normalized.SupplierName,cancellationToken).ConfigureAwait(false);
                                if(line.Unit!=null)UnitLotCostBridge.Register(connection,transaction,detailId,line.Quantity,line.Unit.GrossTotal);
                            }
                            results.Add(new PurchaseInvoiceLineResult { LineNumber = line.LineNumber, DetailId = detailId });
                        }

                        await PurchaseAsyncAccountingService.PostPurchaseInvoiceWithCurrencyAsync(connection, transaction, purchaseId, totalAmount,
                            "فاتورة شراء رقم " + purchaseId, now, normalized.EmployeeName,
                            normalized.ImportPaymentMethod.Length == 0 ? "Cash" : normalized.ImportPaymentMethod,cancellationToken,normalized.ExpectedSystemCurrencyCode??(string.IsNullOrWhiteSpace(normalized.ImportCurrencyCode)?null:normalized.ImportCurrencyCode)).ConfigureAwait(false);
                        await InsertAuditAsync(connection, transaction, normalized, purchaseId, totalAmount,cancellationToken).ConfigureAwait(false);
                        await PurchaseAsyncImportedSourceRegistry.RecordAsync(connection, transaction, "Purchase", normalized.ImportSourceSystem,
                            normalized.ImportSourceInvoiceId, purchaseId,cancellationToken).ConfigureAwait(false);
                        await PurchaseAsyncImportGuard.MarkDraftApprovedAsync(connection, transaction, normalized.ImportDraftId,
                            "Purchase", normalized.ImportSourceSystem, normalized.ImportSourceInvoiceId, purchaseId,cancellationToken).ConfigureAwait(false);
                        var outcome=new PurchaseInvoiceResult{PurchaseId=purchaseId,TotalAmount=totalAmount,Lines=results.AsReadOnly()};
                        if(identity!=null){
                            if(!identityCurrent())throw new UnauthorizedAccessException("تغيرت جلسة محاولة المشتريات قبل الاعتماد.");
                            await PurchaseWriteReceiptStore.RecordAsync(connection,transaction,identity,outcome,cancellationToken).ConfigureAwait(false);
                        }
                        if (!commit && verifyBeforeRollback != null)
                            verifyBeforeRollback(connection, transaction, purchaseId);
                        cancellationToken.ThrowIfCancellationRequested();
                        if(identity!=null&&!identityCurrent())throw new UnauthorizedAccessException("تغيرت جلسة محاولة المشتريات قبل الاعتماد.");
                        if (commit){commitStarted=true;transaction.Commit();}
                        else transaction.Rollback();
                        return outcome;
                    }
                    catch(Exception error)
                    {
                        try{transaction.Rollback();}catch{}
                        if(commitStarted)throw new PurchaseWriteOutcomeUnknownException(identity==null?Guid.Empty:identity.RequestId,error);
                        cancellationToken.ThrowIfCancellationRequested();
                        throw;
                    }
                }
            }
        }


}}


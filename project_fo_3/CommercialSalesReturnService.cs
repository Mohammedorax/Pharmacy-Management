using System;
using System.Data;
using System.Data.SqlClient;

namespace project_fo_3
{
    internal sealed class CommercialReturnNotCommittedException : InvalidOperationException
    {
        internal readonly string SourceIdentity, RequestId;
        internal readonly Guid InvoiceId;
        internal CommercialReturnNotCommittedException(string source,Guid invoice,string request,Exception cause)
            : base(cause.Message,cause)
        { SourceIdentity=source; InvoiceId=invoice; RequestId=request; }
    }

    // نقطة الاعتماد الوحيدة للمرتجع التجاري. المستودع يبقى طبقة SQL داخل
    // المعاملة فقط، ولا يسمح لمسار واجهة مستقبلي بتجزئة المخزون والقيد.
    internal static class CommercialSalesReturnService
    {
        internal static void EnsureRequestCanBeIssued(CommercialSalesReturnRequest request)
        {
            if (!PermissionService.CanExecute(AppOperation.IssueCommercialSalesReturn))
                throw new UnauthorizedAccessException("لا تملك صلاحية اعتماد مرتجع تجاري.");
            if (request == null || !SessionContext.IsAuthenticated ||
                !string.Equals(request.IssuedBy ?? string.Empty, SessionContext.CurrentUser, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("يجب أن يطابق منفذ المرتجع المستخدم المسجل في الجلسة.");
        }

        public static CommercialSalesReturnResult Issue(CommercialSalesReturnRequest request, DateTime businessDateUtc)
        {
            return Issue(request,businessDateUtc,Database.ConnectionString,SessionContext.CurrentSessionRevision);
        }

        internal static CommercialSalesReturnResult Issue(CommercialSalesReturnRequest request, DateTime businessDateUtc,
            string expectedSource, long expectedSessionRevision)
        {
            Action ensureCurrent=()=>{
                if(Database.ConnectionString!=expectedSource || SessionContext.CurrentSessionRevision!=expectedSessionRevision)
                    throw new InvalidOperationException("تغير سياق المرتجع؛ أعد فتح الشاشة.");
                EnsureRequestCanBeIssued(request);
            };
            ensureCurrent();
            using (SqlConnection connection = new SqlConnection(expectedSource))
            {
                connection.Open();
                ensureCurrent();
                var committed=CommercialSalesReturnRepository.ReadCommittedResult(connection,request);
                if(committed!=null) { ensureCurrent(); return committed; }
                using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    bool commitStarted=false;
                    try
                    {
                        ensureCurrent();
                        CommercialSalesReturnResult result = Issue(connection, transaction, request, businessDateUtc);
                        using(SessionCommitGate.Commit(()=> { ensureCurrent(); return true; }))
                        {
                            commitStarted=true;
                            transaction.Commit();
                        }
                        return result;
                    }
                    catch(Exception error)
                    {
                        bool rollbackConfirmed=false;
                        try { transaction.Rollback(); rollbackConfirmed=true; } catch { }
                        if(!commitStarted&&rollbackConfirmed)
                            throw new CommercialReturnNotCommittedException(SalesWriteSourceFingerprint.Compute(expectedSource),
                                request.OriginalInvoiceId,request.RequestId,error);
                        throw;
                    }
                }
            }
        }

        // نفس نقطة الاعتماد لكن داخل معاملة يملكها المستدعي. تستخدمها فحوص QA
        // المتراجعة كي تختبر الصلاحية والفترة والمستودع والقيد دون تلويث القاعدة.
        internal static CommercialSalesReturnResult Issue(
            SqlConnection connection, SqlTransaction transaction,
            CommercialSalesReturnRequest request, DateTime businessDateUtc)
        {
            return Issue(connection, transaction, request, businessDateUtc, null);
        }

        internal static CommercialSalesReturnResult Issue(
            SqlConnection connection, SqlTransaction transaction,
            CommercialSalesReturnRequest request, DateTime businessDateUtc,
            Action afterInventoryBeforeAccounting)
        {
            if (connection == null || transaction == null)
                throw new ArgumentNullException("اتصال ومعاملة SQL مطلوبان.");
            EnsureRequestCanBeIssued(request);
            CommercialAccountingPeriodPolicy.EnsureBusinessDateOpen(connection, transaction, businessDateUtc);
            return CommercialSalesReturnRepository.Issue(
                connection, transaction, request, businessDateUtc, afterInventoryBeforeAccounting);
        }
    }
}

using System;
using PharmacyManagement.Application;

namespace project_fo_3
{
    internal sealed class SalesInvoiceCancellationOperation : ISalesInvoiceCancellationOperation
    {
        internal Action BeforeCommitForVerification { get; set; }

        public SalesInvoiceCancellationOutcome Cancel(SalesInvoiceCancellationRequest request)
        {
            if (!PermissionService.CanExecute(AppOperation.CancelSalesInvoice))
                throw new UnauthorizedAccessException("لا تملك صلاحية إلغاء فاتورة بيع.");
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (string.IsNullOrWhiteSpace(request.ActorName))
                throw new UnauthorizedAccessException("هوية منفذ إلغاء الفاتورة مطلوبة.");
            if ((request.Reason ?? string.Empty).Trim().Length > 500)
                throw new UiValidationException("سبب الإلغاء يتجاوز الحد المسموح (500).");

            long sessionRevision = SessionContext.CurrentSessionRevision;
            string user = SessionContext.CurrentUser;
            string role = SessionContext.CurrentRole;
            string employeeId = SessionContext.CurrentEmployeeId;
            string source = Database.ConnectionString;
            if (!string.Equals(user, request.ActorName, StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("تغير منفذ إلغاء فاتورة البيع؛ أعد فتح سجل الفواتير قبل المتابعة.");
            Func<bool> current = () => SessionContext.IsAuthenticated &&
                SessionContext.CurrentSessionRevision == sessionRevision &&
                string.Equals(SessionContext.CurrentUser, user, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(SessionContext.CurrentRole, role, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(SessionContext.CurrentEmployeeId, employeeId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Database.ConnectionString, source, StringComparison.Ordinal) &&
                PermissionService.CanExecute(AppOperation.CancelSalesInvoice);
            if (!current()) throw new UnauthorizedAccessException("لا تملك صلاحية إلغاء فاتورة بيع أو تغيرت الجلسة.");

            SalesInvoiceCancellationResult result = string.IsNullOrWhiteSpace(request.InvoiceReference)
                ? SalesInvoiceCancellationService.CancelCurrentLegacySale(request.BillLineIds, request.Reason, request.ActorName, role, current, BeforeCommitForVerification)
                : SalesInvoiceCancellationService.CancelMappedLegacySale(request.InvoiceReference, request.Reason, request.ActorName, role, current, BeforeCommitForVerification);
            return new SalesInvoiceCancellationOutcome
            {
                OriginalInvoiceReference = result.OriginalInvoiceReference,
                TotalAmount = result.TotalAmount,
                RestoredQuantity = result.RestoredQuantity
            };
        }
    }
}

using System;
using System.Data;
using System.IO;
using System.Linq;
using PharmacyManagement.Application;

namespace project_fo_3
{
    internal sealed partial class CommercialSalesReturnForm
    {
        internal string ReturnPendingDirectoryOverride { get; set; }
        internal Func<Guid,DataTable> ReturnLotsLoader { get; set; }
        internal Func<CommercialSalesReturnRequest,DateTime,CommercialSalesReturnResult> ReturnIssuerOverride { get; set; }
        private CommercialReturnPendingStore returnPendingStore;
        private CommercialReturnPendingAttempt returnPendingAttempt;
        private string returnSource, returnOwner, returnRole;
        private long returnSessionRevision;
        private bool returnRecoveryBlocked=true;

        private void ConfigureReturnRecovery()
        {
            if(previewMode)return;
            Disposed+=(s,e)=> { returnPendingStore?.Dispose(); returnPendingStore=null; };
            UiSessionSubscription.Attach(this, InvalidateReturnRecovery);
        }
        private void InvalidateReturnRecovery()
        {
            returnRecoveryBlocked=true;
            returnPendingStore?.Dispose(); returnPendingStore=null;
            returnPendingAttempt=null; pendingRequest=null;
            grid.DataSource=null; reason.Clear();
            grid.Enabled=reason.Enabled=refund.Enabled=issue.Enabled=history.Enabled=quarantine.Enabled=false;
            help.Text=Present("تغيرت جلسة المستخدم أو قاعدة التشغيل. أغلق الشاشة وافتحها مجددًا.");
            LayoutHeader();
            SetReturnRecoveryStatus("تغير سياق المرتجع؛ احتُفظ بأي طلب معلق في سياقه الأصلي.",false);
        }
        private bool ReturnRecoveryCurrent => !returnRecoveryBlocked && returnPendingStore!=null &&
            SessionContext.IsAuthenticated && SessionContext.CurrentSessionRevision==returnSessionRevision &&
            Database.ConnectionString==returnSource &&
            string.Equals(SessionContext.CurrentUser,returnOwner,StringComparison.OrdinalIgnoreCase) &&
            SessionContext.CurrentRole==returnRole;
        private void EnsureReturnRecoveryCurrent()
        {
            if(!ReturnRecoveryCurrent)
            {
                InvalidateReturnRecovery();
                throw new InvalidOperationException("تغير سياق المرتجع؛ أعد فتح الشاشة.");
            }
        }
        internal bool InitializeReturnRecovery()
        {
            if(ReturnRecoveryCurrent)return true;
            returnPendingStore?.Dispose(); returnPendingStore=null; returnRecoveryBlocked=true;
            try
            {
                if(!SessionContext.IsAuthenticated)throw new UnauthorizedAccessException();
                returnSource=Database.ConnectionString; returnOwner=SessionContext.CurrentUser;
                returnRole=SessionContext.CurrentRole; returnSessionRevision=SessionContext.CurrentSessionRevision;
                string directory=ReturnPendingDirectoryOverride??Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"PharmacyManagement","PendingCommercialReturns");
                returnPendingStore=new CommercialReturnPendingStore(directory,SalesWriteSourceFingerprint.Compute(returnSource),returnOwner,returnRole,invoiceId);
                returnPendingAttempt=returnPendingStore.Load();
                pendingRequest=returnPendingAttempt?.Snapshot();
                if(returnPendingAttempt!=null)pendingBusinessDateUtc=returnPendingAttempt.BusinessDateUtc;
                returnRecoveryBlocked=false;
                EnsureReturnRecoveryCurrent();
                return true;
            }
            catch(Exception error)
            {
                returnPendingStore?.Dispose(); returnPendingStore=null; returnRecoveryBlocked=true;
                pendingRequest=null; returnPendingAttempt=null;
                grid.Enabled=reason.Enabled=refund.Enabled=issue.Enabled=false;
                string code=AppDiagnostics.Log("CommercialReturnRecoveryLoad",error);
                SetReturnRecoveryStatus("تعذر فتح استعادة المرتجع. قد تكون الشاشة مفتوحة في نافذة أخرى أو ملف الاستعادة غير متاح. أغلقها ثم أعد المحاولة.",false);
                status.AccessibleDescription=Present("رمز الدعم: ")+code;
                return false;
            }
        }
        private void SaveReturnAttempt(CommercialSalesReturnRequest draft)
        {
            EnsureReturnRecoveryCurrent();
            var candidate=new CommercialReturnPendingAttempt(SalesWriteSourceFingerprint.Compute(returnSource),
                returnOwner,returnRole,DateTime.UtcNow,draft);
            returnPendingStore.Save(candidate);
            EnsureReturnRecoveryCurrent();
            returnPendingAttempt=candidate; pendingRequest=candidate.Snapshot(); pendingBusinessDateUtc=candidate.BusinessDateUtc;
        }
        private void RestorePendingReturnView()
        {
            if(returnPendingAttempt==null)return;
            EnsureReturnRecoveryCurrent();
            var request=returnPendingAttempt.Snapshot();
            var lots=request.Lines.SelectMany(x=>x.Lots).ToDictionary(x=>x.OriginalSaleAllocationId);
            int found=0;
            foreach(System.Windows.Forms.DataGridViewRow row in grid.Rows)
            {
                CommercialReturnLotRequest lot;
                if(lots.TryGetValue((Guid)row.Cells["AllocationId"].Value,out lot))
                {
                    row.Cells["كمية المرتجع"].Value=lot.Quantity;
                    row.Cells["الحالة_اختيار"].Value=lot.Disposition.ToString(); found++;
                }
            }
            if(found!=lots.Count)throw new InvalidDataException("دفعات الطلب المستعاد لا تطابق الفاتورة المعروضة.");
            reason.Text=request.ReturnReason;
            refund.SelectedIndex=CommercialReturnPresentation.RefundMethods.ToList().FindIndex(x=>x.Code==request.RefundMethod);
            help.Text=string.Format(Present("طلب مرتجع معلق: {0}. راجع السجل أو أعد الطلب نفسه دون تعديل."),request.RequestId);
            LayoutHeader();
            SetSubmissionInProgress(false);
            issue.Enabled=PermissionService.CanExecute(AppOperation.IssueCommercialSalesReturn);
        }
        private void SetReturnRecoveryStatus(string message,bool? success)
        {
            UiTheme.SetStatus(status,Present(message),success);
            status.AccessibleName=status.Text;
        }
    }
}

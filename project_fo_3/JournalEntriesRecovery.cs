using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using PharmacyManagement.Application;

namespace project_fo_3
{
    public partial class JournalEntriesForm
    {
        private JournalOperationRecovery journalRecovery;
        private CancellationTokenSource journalRecoveryReadCancellation;
        private void ReleaseJournalRecovery()
        {
            journalRecoveryReadCancellation?.Cancel();
            journalRecovery?.Dispose(); journalRecovery = null;
        }
        private bool InitializeJournalRecovery()
        {
            if (previewMode || journalRecovery != null) return true;
            JournalPendingStore store = null;
            try
            {
                var session = ApplicationServices.CreateJournalSubmissionStatusSession();
                var postSession = ApplicationServices.CreateJournalPostSessionForEditor(()=>!IsDisposed&&!Disposing);
                var native=postSession.Service as IAsyncJournalPostService;
                if(native==null)throw new InvalidOperationException("لا تتوفر كتابة القيود الأصلية غير المتزامنة؛ لم يرسل الحفظ.");
                if (!session.IsCurrent || !postSession.IsCurrent) throw new UnauthorizedAccessException("لا تملك صلاحية استعادة القيد.");
                string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PharmacyManagement", "pending-journals");
                store = new JournalPendingStore(directory, session.ContextKey, entrySourceType);
                journalRecovery = new JournalOperationRecovery(store, () => session.IsCurrent && postSession.IsCurrent,
                    async (request,token) =>
                    {
                        if (request.ExpectedSystemCurrencyCode == null)
                            throw new InvalidOperationException("هذا طلب استعادة قديم بلا عملة موثقة؛ لم يرسل حفظ. احتفظ به وراجع عملته قبل إعادة الترحيل.");
                        return (await native.PostAsync(request,postSession.Actor,token).ConfigureAwait(false)).Id;
                    },
                    (request, token) => session.Service.FindAsync(request, session.Actor, token));
                store = null;
                if (journalRecovery.Pending != null) RestoreJournalPendingPresentation();
                ApplyJournalRecoveryLock(); return true;
            }
            catch (Exception error)
            {
                store?.Dispose(); ReleaseJournalRecovery();
                SetJournalStatus("تعذر فتح سجل استعادة القيد. لم يُرسل طلب حفظ؛ أغلق النافذة الأخرى إن كانت تستخدم الطلب نفسه.");
                if (!IsDisposed && !Disposing) UiTheme.ShowOperationIssue(this, "استعادة القيد", error);
                return false;
            }
        }
        private void RestoreJournalPendingPresentation()
        {
            var request = journalRecovery.Pending.Request;
            journalSystemCurrencyCode = request.ExpectedSystemCurrencyCode;
            lines.Rows.Clear(); entryDate.Value = request.Date; headerDescription.Text = request.Description ?? string.Empty;
            foreach (var line in request.Lines)
            {
                // Pending payload is already in local currency; never recalculate it with new settings.
                string accountName = account.Items.Cast<object>().Select(Convert.ToString)
                    .FirstOrDefault(item => item.StartsWith(line.AccountCode + " - ", StringComparison.Ordinal)) ?? line.AccountCode;
                string center = line.CostCenterId.HasValue ? line.CostCenterId.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : "بدون";
                lines.Rows.Add(line.AccountCode, accountName, line.Debit, line.Credit, line.Debit, line.Credit,
                    request.ExpectedSystemCurrencyCode ?? "غير موثقة", 1m, center, line.CostCenterId.HasValue ? (object)line.CostCenterId.Value : DBNull.Value,
                    string.Empty, request.Date.ToString("yyyy/MM/dd"), line.Description ?? string.Empty, "حذف");
            }
            UpdateTotals();
            SetJournalStatus("استُعيد طلب قيد غير مؤكد بمبالغه المحلية الثابتة. اختر حفظ للتحقق من اعتماده أو إعادة الطلب نفسه؛ لا تُنشئ قيدًا بديلًا.");
        }
        private void ApplyJournalRecoveryLock()
        {
            if (IsDisposed || Disposing) return;
            bool editable = (journalRecovery == null || journalRecovery.Pending == null || journalRecovery.CanCorrectRejected) && !(journalDraftAccepted && journalRecovery != null && !journalRecovery.AcceptedContentMatchesRequest);
            journalEditorViewport.Enabled = linesGrid.Enabled = editable && journalOptionsReady;
        }
        private bool HasPendingJournal()
        {
            if (journalRecovery == null || journalRecovery.Pending == null || journalRecovery.CanCorrectRejected) return false;
            SetJournalStatus(journalRecovery.AcceptedId > 0
                ? "القيد رقم " + journalRecovery.AcceptedId + " معتمد؛ تعذر تنظيف سجل الاستعادة المحلي. اختر جديد لإعادة التنظيف دون ترحيل آخر."
                : "يوجد طلب قيد غير مؤكد. اختر حفظ للتحقق وإعادة الطلب نفسه؛ الإغلاق يحتفظ به للاستعادة.");
            return true;
        }
        private async System.Threading.Tasks.Task<int> ExecuteJournalRecoveryAsync(JournalOperationRecovery recovery)
        {
            var cancellation = new CancellationTokenSource(); journalRecoveryReadCancellation = cancellation;
            try { return await recovery.ExecuteAsync(cancellation.Token); }
            finally
            {
                if (ReferenceEquals(journalRecoveryReadCancellation, cancellation)) journalRecoveryReadCancellation = null;
                cancellation.Dispose();
            }
        }
    }
}

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using PharmacyManagement.Application;
using PharmacyManagement.Domain;

namespace project_fo_3
{
    public partial class JournalEntriesQuickForm
    {
        private JournalOperationRecovery quickRecovery;
        private string quickSystemCurrencyCode;
        private CancellationTokenSource quickRecoveryCancellation;
        private bool quickAcceptedReview;
        private void ApplyQuickHistoryHeadings()
        {
            Grid.Tag = UiTheme.PreserveLocalizedGridDirection;
            Grid.RightToLeft = quickLeftToRight ? RightToLeft.No : RightToLeft.Yes;
            foreach (DataGridViewColumn column in Grid.Columns)
            {
                string name = string.IsNullOrEmpty(column.DataPropertyName) ? column.Name : column.DataPropertyName;
                switch (name)
                {
                    case "رقم القيد": column.HeaderText = QuickText(name, "Entry no."); break;
                    case "التاريخ": column.HeaderText = QuickText(name, "Date"); break;
                    case "المصدر": column.HeaderText = QuickText(name, "Source"); break;
                    case "مدين": column.HeaderText = QuickText(name, "Debit"); break;
                    case "دائن": column.HeaderText = QuickText(name, "Credit"); break;
                    case "البيان": column.HeaderText = QuickText(name, "Description"); break;
                }
            }
        }
        private void ReleaseQuickRecovery()
        {
            quickRecoveryCancellation?.Cancel();
            quickRecovery?.Dispose(); quickRecovery = null;
        }
        private bool InitializeQuickRecovery()
        {
            if (previewMode || quickRecovery != null) return true;
            JournalPendingStore store = null;
            try
            {
                var reader = ApplicationServices.CreateJournalSubmissionStatusSession();
                var writer = ApplicationServices.CreateJournalPostSessionForEditor(()=>!IsDisposed&&!Disposing);
                var native=writer.Service as IAsyncJournalPostService;
                if(native==null)throw new InvalidOperationException("لا تتوفر كتابة القيود الأصلية غير المتزامنة؛ لم يرسل الحفظ.");
                if (!reader.IsCurrent || !writer.IsCurrent) throw new UnauthorizedAccessException("لا تملك صلاحية استعادة القيد.");
                string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "PharmacyManagement", "pending-journals", "quick");
                store = new JournalPendingStore(directory, reader.ContextKey, "Manual");
                quickRecovery = new JournalOperationRecovery(store, () => reader.IsCurrent && writer.IsCurrent,
                    async (request,token) =>
                    {
                        if(request.ExpectedSystemCurrencyCode==null)
                            throw new InvalidOperationException("هذا طلب قيد سريع قديم بلا عملة موثقة؛ لم يرسل حفظ. احتفظ به وراجع عملته.");
                        return (await native.PostAsync(request,writer.Actor,token).ConfigureAwait(false)).Id;
                    },
                    (request, token) => reader.Service.FindAsync(request, reader.Actor, token));
                store = null;
                if (quickRecovery.Pending != null) RestoreQuickPending();
                ApplyQuickRecoveryLock(); return true;
            }
            catch (Exception error)
            {
                store?.Dispose(); ReleaseQuickRecovery();
                SetQuickJournalStatus(QuickText("تعذر فتح سجل استعادة القيد السريع. لم يُرسل حفظ؛ أغلق النافذة الأخرى إن كانت تستخدم المسودة نفسها.",
                    "Could not open quick journal recovery. Nothing was sent; close another window using the same draft."));
                if (!previewMode && !IsDisposed && !Disposing) UiTheme.ShowOperationIssue(this, "استعادة القيد السريع", error);
                return false;
            }
        }
        private void RestoreQuickPending()
        {
            var request = quickRecovery.Pending.Request;
            quickSystemCurrencyCode=request.ExpectedSystemCurrencyCode;
            if (request.SourceType != "Manual" || request.Lines.Count != 2 ||
                request.Lines[0].Debit <= 0 || request.Lines[0].Credit != 0 ||
                request.Lines[1].Credit != request.Lines[0].Debit || request.Lines[1].Debit != 0 ||
                request.Lines.Any(line => line.CostCenterId.HasValue || line.Description != request.Description))
                throw new InvalidOperationException("الطلب المحفوظ لا يطابق حقول القيد السريع؛ لم تُغيّر بياناته ولم يُرسل حفظ.");
            SelectPendingQuickAccount(debitAccount, request.Lines[0].AccountCode);
            SelectPendingQuickAccount(creditAccount, request.Lines[1].AccountCode);
            amount.Text = request.Lines[0].Debit.ToString(CultureInfo.CurrentCulture);
            description.Text = request.Description ?? string.Empty;
            SetQuickJournalStatus(QuickText("استُعيد طلب القيد السريع دون إرساله. اختر حفظ للتحقق من اعتماده أو إعادة الطلب نفسه.",
                "The quick journal request was restored without sending it. Save checks acceptance or retries the same request."));
        }
        private static void SelectPendingQuickAccount(ComboBox field, string code)
        {
            string caption = field.Items.Cast<object>().Select(Convert.ToString)
                .FirstOrDefault(item => item.StartsWith(code + " - ", StringComparison.Ordinal));
            int index = caption == null ? -1 : field.Items.IndexOf(caption);
            field.SelectedIndex = index;
            if (index < 0) field.Text = code;
        }
        private void ApplyQuickRecoveryLock()
        {
            if (IsDisposed || Disposing) return;
            bool editable = !quickAcceptedReview &&
                (quickRecovery == null || quickRecovery.Pending == null || quickRecovery.CanCorrectRejected);
            Editor.Enabled = quickOptionsReady && !quickWriteBusy;
            debitAccount.Enabled = creditAccount.Enabled = quickOptionsReady && editable && !quickWriteBusy;
            amount.Enabled = description.Enabled = quickOptionsReady && !quickWriteBusy;
            amount.ReadOnly = description.ReadOnly = !editable;
            var create = quickActions.Controls["quickJournalNew"];
            if (create != null) create.Enabled = quickOptionsReady && !quickWriteBusy &&
                (quickRecovery == null || quickRecovery.Pending == null || quickRecovery.CanCorrectRejected || quickRecovery.AcceptedId > 0);
        }
        private void NewQuickDraft()
        {
            if (quickWriteBusy || !EnsureQuickEditor()) return;
            if (quickRecovery != null && quickRecovery.Pending != null)
            {
                if (quickRecovery.AcceptedId > 0) quickRecovery.RetryAcceptedCleanup();
                if (quickRecovery.Pending != null && !quickRecovery.CanCorrectRejected)
                {
                    SetQuickJournalStatus(QuickText("احتُفظ بالطلب. اختر حفظ للتحقق من اعتماده؛ لا تنشئ قيدًا بديلًا.",
                        "The request was retained. Save checks acceptance; do not create a replacement entry."));
                    return;
                }
            }
            quickAcceptedReview = false; amount.Text = string.Empty; description.Clear(); acceptedQuickJournalId = 0;
            SetQuickJournalStatus(quickRecovery != null && quickRecovery.CanCorrectRejected
                ? QuickText("صحح الطلب المرفوض؛ سيبقى معرّفه وتاريخه الأصليان.", "Correct the rejected request; its original identity and date are retained.")
                : QuickText("تم تجهيز قيد جديد.", "Ready for a new journal entry."));
            ApplyQuickRecoveryLock();
        }
        private async Task<int> ExecuteQuickRecoveryAsync(JournalOperationRecovery recovery)
        {
            var cancellation = new CancellationTokenSource(); quickRecoveryCancellation = cancellation;
            try { return await recovery.ExecuteAsync(cancellation.Token); }
            finally
            {
                if (ReferenceEquals(quickRecoveryCancellation, cancellation)) quickRecoveryCancellation = null;
                cancellation.Dispose();
            }
        }
        private async Task<int> SaveQuickEntryAsync()
        {
            if (!EnsureQuickEditor() || previewMode || quickWriteBusy) return 0;
            if (quickAcceptedReview)
            {
                SetQuickJournalStatus(QuickText("القيد معتمد ببيانات مختلفة؛ راجع القيد المحفوظ واختر جديد للمتابعة.",
                    "The entry was accepted with different data. Review the saved entry and choose New to continue."));
                return 0;
            }
            if (!PermissionService.EnsureAllowed(AppOperation.PostAccountingEntry, this)) return 0;
            try
            {
                if (!InitializeQuickRecovery()) return 0;
                if (quickRecovery.AcceptedId > 0 && quickRecovery.Pending != null)
                {
                    quickRecovery.RetryAcceptedCleanup(); ApplyQuickRecoveryLock();
                    SetQuickJournalStatus(QuickText("القيد معتمد؛ لم يُرسل ترحيل آخر. اختر جديد بعد تنظيف سجل الاستعادة.",
                        "The entry is accepted; no new posting was sent. Choose New after recovery cleanup."));
                    return quickRecovery.AcceptedId;
                }
                if (quickRecovery.Pending == null || quickRecovery.CanCorrectRejected)
                {
                    decimal value; string debitCode, creditCode;
                    if (!TryReadQuickManualInput(out value, out debitCode, out creditCode)) return 0;
                    string caption = description.Text;
                    var values = new[] { new JournalLineValues(debitCode, value, 0m, caption, null),
                        new JournalLineValues(creditCode, 0m, value, caption, null) };
                    if (quickRecovery.CanCorrectRejected)
                        quickRecovery.CorrectRejected(JournalPendingRequest.Restore(new JournalPostRequest(
                            quickRecovery.Pending.Request.Date, "Manual", quickRecovery.Pending.Request.SourceId, caption, values,quickSystemCurrencyCode)));
                    else quickRecovery.Prepare(JournalPendingRequest.Create(DateTime.Now, "Manual", caption, values,quickSystemCurrencyCode));
                }
                ApplyQuickRecoveryLock(); var recovery = quickRecovery;
                return await RunQuickJournalWriteAsync(() => ExecuteQuickRecoveryAsync(recovery));
            }
            catch (Exception error)
            {
                if (!IsDisposed && !Disposing) UiTheme.ShowOperationIssue(this, "التحقق من القيد السريع", error);
                return 0;
            }
        }
    }
}

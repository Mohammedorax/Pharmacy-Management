using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using PharmacyManagement.Application;
namespace project_fo_3
{
    internal sealed class VoucherEditorRecovery : IDisposable
    {
        internal readonly VoucherOperationRecovery Operation;
        private readonly CancellationTokenSource cancellation=new CancellationTokenSource();
        internal VoucherEditorRecovery(VoucherOperationRecovery operation){Operation=operation??throw new ArgumentNullException(nameof(operation));}
        internal bool Editable=>!Mismatch&&(Operation.Pending==null||Operation.CanCorrectRejected);
        internal bool Mismatch=>Operation.Accepted!=null&&!Operation.AcceptedContentMatchesRequest;
        internal static VoucherEditorRecovery Open(string type)
        { return OpenForEditor(type,null); }
        internal static VoucherEditorRecovery OpenForEditor(string type,Func<bool> editorCurrent)
        {
            var reader=ApplicationServices.CreateVoucherSubmissionStatusSession();var writer=ApplicationServices.CreateVoucherPostSessionForEditor(editorCurrent);
            var asynchronous=writer.Service as IAsyncVoucherPostService;
            if(asynchronous==null)throw new InvalidOperationException("Voucher writer does not support native async posting; recovery was not opened.");
            if(!reader.IsCurrent||!writer.IsCurrent)throw new UnauthorizedAccessException("لا تملك صلاحية استعادة السند.");
            string key=InventoryPendingStore.CreateContextKey(reader.ContextKey,type.Trim().ToLowerInvariant(),"Voucher","");
            string directory=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"PharmacyManagement","pending-vouchers");
            directory=VoucherEditorsOwnedQaSelfTests.DirectoryOverride??directory;
            VoucherPendingStore store=null;
            try
            {
                store=new VoucherPendingStore(directory,key);
                var operation=new VoucherOperationRecovery(store,()=>reader.IsCurrent&&writer.IsCurrent,async (request,token)=>{VoucherEditorsOwnedQaSelfTests.BeforePost(request);var result=await asynchronous.PostAsync(request,writer.Actor,token).ConfigureAwait(false);return VoucherEditorsOwnedQaSelfTests.AfterPost(result,request);},(request,token)=>reader.Service.FindAsync(request,reader.Actor,token));
                if(operation.Pending!=null&&!string.Equals(operation.Pending.Request.Type,type,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("نوع السند المعلق لا يطابق الشاشة.");
                store=null;return new VoucherEditorRecovery(operation);
            }
            finally{store?.Dispose();}
        }
        internal void Prepare(VoucherPostRequest draft,VoucherDraftInputs draftInputs=null)
        {
            if(Mismatch)throw new InvalidOperationException("السند معتمد ببيانات مختلفة؛ لم تعتمد التعديلات. افتح السند المحفوظ للمراجعة.");
            if(Operation.Pending==null)Operation.Prepare(VoucherPendingRequest.Create(draft,draftInputs));
            else if(Operation.CanCorrectRejected)
            {
                var id=Operation.Pending.Request.RequestId;
                Operation.CorrectRejected(VoucherPendingRequest.Restore(new VoucherPostRequest(draft.Type,draft.Date,draft.PartyName,draft.AccountCode,draft.Amount,draft.Notes,draft.CashAccountCode,draft.CostCenterId,draft.ExpectedSystemCurrencyCode,id),draftInputs));
            }
        }
        internal async Task<int> ExecuteAsync()
        {
            var result=await Operation.ExecuteAsync(cancellation.Token);
            if(Mismatch)throw new InvalidOperationException("السند رقم "+result.VoucherId+" معتمد ببيانات مختلفة؛ لم تعتمد التعديلات الحالية. لا تعِد حفظها أو طباعتها كاعتماد جديد.");
            return result.VoucherId;
        }
        internal string Message
        {
            get
            {
                if(Mismatch)return "السند رقم "+Operation.Accepted.VoucherId+" معتمد ببيانات مختلفة؛ افتح السند المحفوظ للمراجعة، ولا تعِد اعتماد التعديلات.";
                if(Operation.Pending==null)return null;
                if(Operation.CanCorrectRejected)return "رُفضت المحاولة وأُكد التراجع. صحح البيانات ثم احفظ بالمعرّف نفسه.";
                var request=Operation.Pending.Request;
                return "طلب سند غير محسوم: "+request.PartyName+"؛ "+request.Amount.ToString("0.##")+" "+request.ExpectedSystemCurrencyCode+". الحفظ يتحقق من الاعتماد ويعيد الطلب نفسه. الإغلاق يحتفظ به.";
            }
        }
        public void Dispose(){cancellation.Cancel();Operation.Dispose();cancellation.Dispose();}
        internal static void SelectAccount(ComboBox control,string code)
        {
            var value=control.Items.Cast<object>().FirstOrDefault(item=>Convert.ToString(item)==code||Convert.ToString(item).StartsWith(code+" - ",StringComparison.Ordinal));
            if(value==null){control.Items.Add(code);value=code;}control.SelectedItem=value;
        }
    }
    public partial class VoucherForm
    {
        private VoucherEditorRecovery voucherRecovery;
        private bool InitializeVoucherRecovery()
        {
            if(voucherPreview||!PermissionService.CanExecute(AppOperation.PostCashVoucher)||voucherRecovery!=null)return true;
            try{voucherRecovery=VoucherEditorRecovery.OpenForEditor(voucherType,()=>!IsDisposed&&!Disposing);RestoreVoucherRecovery();return true;}
            catch(Exception error){SetVoucherStatus(VoucherText("تعذر فتح سجل استعادة السند؛ لم يرسل حفظ. أغلق الشاشة الأخرى للسند ثم أعد المحاولة.","Cannot open voucher recovery. Close the other voucher editor and retry."),true);UiTheme.ShowOperationIssue(this,"استعادة السند",error);return false;}
        }
        private void ReleaseVoucherRecovery(){voucherRecovery?.Dispose();voucherRecovery=null;}
        internal void RestoreVoucherRecovery()
        {
            var request=voucherRecovery?.Operation.Pending?.Request;
            if(request!=null){voucherDate.Value=request.Date;party.Text=request.PartyName??"";amount.Text=request.Amount.ToString("0.##");notes.Text=request.Notes??"";VoucherEditorRecovery.SelectAccount(account,request.AccountCode);}
            ApplyVoucherRecoveryLock();
        }
        internal void ApplyVoucherRecoveryLock()
        {
            if(voucherRecovery==null)return;bool current=VoucherContextCurrent(voucherEditorCurrent);Editor.Enabled=account.Enabled=current&&voucherOptionsReady&&!voucherWriteBusy&&voucherRecovery.Editable;
            if(!current)return;
            var save=Controls.Find("legacyVoucherSave",true).FirstOrDefault();if(save!=null&&voucherRecovery.Mismatch)save.Enabled=false;
            if(voucherRecovery.Message!=null)SetVoucherStatus(voucherRecovery.Message,true);
        }
        internal void AttachVoucherRecovery(VoucherEditorRecovery recovery){ReleaseVoucherRecovery();voucherRecovery=recovery;RestoreVoucherRecovery();}
    }
    public partial class ReceiptVoucherForm
    {
        private VoucherEditorRecovery receiptRecovery;
        private string receiptRecoveryRestoredNotes;
        private bool InitializeReceiptRecovery()
        {
            if(previewMode||!PermissionService.CanExecute(AppOperation.PostCashVoucher)||receiptRecovery!=null)return true;
            try{receiptRecovery=VoucherEditorRecovery.OpenForEditor(voucherType,()=>!IsDisposed&&!Disposing);RestoreReceiptRecovery();return true;}
            catch(Exception error){statusLabel.Text=ReceiptText("تعذر فتح سجل استعادة السند؛ لم يرسل حفظ. أغلق الشاشة الأخرى للسند ثم أعد المحاولة.");UiTheme.ShowOperationIssue(this,"استعادة السند",error);return false;}
        }
        private void ReleaseReceiptRecovery(){receiptRecovery?.Dispose();receiptRecovery=null;receiptRecoveryRestoredNotes=null;}
        internal void RestoreReceiptRecovery()
        {
            var request=receiptRecovery?.Operation.Pending?.Request;
            if(request!=null)
            {
                AntdDateInput.SetDate(voucherDate,request.Date);partyName.Text=request.PartyName??"";amount.Text=localValue.Text=request.Amount.ToString("0.##");exchangeRate.Text="1";
                amount.AccessibleDescription=localValue.AccessibleDescription="مبلغ الطلب المحفوظ بعملته: "+request.ExpectedSystemCurrencyCode;
                var inputs=receiptRecovery.Operation.Pending.DraftInputs;
                if(inputs!=null)
                {
                    partyName.Text=inputs.PartyName??"";amount.Text=inputs.OriginalAmount.ToString();exchangeRate.Text=inputs.ExchangeRate.ToString();
                    amount.AccessibleDescription="المبلغ الأصلي بعملة: "+inputs.CurrencyCode;localValue.AccessibleDescription="المبلغ المحلي الثابت بعملة: "+request.ExpectedSystemCurrencyCode;
                    if(!currency.Items.Contains(inputs.CurrencyCode))currency.Items.Add(inputs.CurrencyCode);currency.SelectedItem=inputs.CurrencyCode;
                    notes.Text=inputs.Notes??"";SetPaymentMethod(inputs.PaymentMethod,paymentMethodsContainer);
                    chequeNumber.Text=inputs.ChequeNumber??"";chequeBank.Text=inputs.ChequeBank??"";if(inputs.ChequeDate.HasValue)AntdDateInput.SetDate(chequeDate,inputs.ChequeDate.Value);chequeStatus.Text=inputs.ChequeStatus??"";
                    if(inputs.Representative!=null){if(!representative.Items.Contains(inputs.Representative))representative.Items.Add(inputs.Representative);representative.SelectedItem=inputs.Representative;}
                    receiptRecoveryRestoredNotes=null;
                }
                else{notes.Text=request.Notes??"";receiptRecoveryRestoredNotes=notes.Text;}VoucherEditorRecovery.SelectAccount(partyAccount,request.AccountCode);VoucherEditorRecovery.SelectAccount(settlementAccount,request.CashAccountCode);
                if(request.CostCenterId.HasValue){var selected=costCenter.Items.Cast<object>().FirstOrDefault(item=>Convert.ToString(item).StartsWith(request.CostCenterId.Value+" - ",StringComparison.Ordinal));if(selected==null){selected=request.CostCenterId.Value+" - مركز مستعاد";costCenter.Items.Add(selected);}costCenter.SelectedItem=selected;}else if(costCenter.Items.Count>0)costCenter.SelectedIndex=0;
            }
            ApplyReceiptRecoveryLock();
        }
        internal void ApplyReceiptRecoveryLock()
        {
            if(receiptRecovery==null)return;bool current=receiptOptionsCurrent!=null&&receiptOptionsCurrent();bool editable=current&&receiptOptionsReady&&!receiptWriteBusy&&receiptRecovery.Editable;voucherEditor.Enabled=editable;history.Enabled=current&&receiptOptionsReady&&!receiptWriteBusy&&(receiptRecovery.Editable||receiptRecovery.Mismatch);
            foreach(Control side in Controls.Find("receiptVoucherSide",true))side.Enabled=editable;
            if(!current)return;
            foreach(Control print in Controls.Find("receiptVoucherAction_Print_طباعة كاشير",true))print.Enabled=receiptOptionsReady&&!receiptWriteBusy&&receiptRecovery.Operation.Pending==null;
            if(receiptRecovery.Message!=null){statusLabel.Text=receiptRecovery.Message;statusLabel.AccessibleDescription=statusLabel.Text;}
        }
        internal void AttachReceiptRecovery(VoucherEditorRecovery recovery){ReleaseReceiptRecovery();receiptRecovery=recovery;RestoreReceiptRecovery();}
    }
}
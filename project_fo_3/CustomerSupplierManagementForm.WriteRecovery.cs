using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Drawing;
using PharmacyManagement.Application;
namespace project_fo_3
{
    internal sealed partial class CustomerSupplierManagementForm
    {
        internal Func<CustomerMasterWriteSession> CustomerMasterWriteSessionFactory { get; set; }
        internal string CustomerPendingDirectoryOverride { get; set; }
        private CustomerMasterWriteAttempt customerWriteAttempt;
        private CustomerMasterWriteSession customerWriteSession;
        private CustomerMasterPendingStore customerPendingStore;
        private bool customerRecoveryBlocked=true;
        private long customerWriteVersion;
        private AntdUI.Button verifyCustomerWriteButton, cancelCustomerWriteButton;
        private bool CustomerRecoveryEnabled => !selectionMode&&!previewMode&&(useDefaultCustomerRead||CustomerMasterWriteSessionFactory!=null);
        private bool CustomerRecoveryLocksEditor => CustomerRecoveryEnabled&&(customerRecoveryBlocked||
            customerWriteAttempt!=null&&!customerWriteAttempt.CanDiscard);
        private bool CanRevealCustomerAttempt => CustomerRecoveryEnabled&&customerWriteAttempt!=null&&
            customerWriteSession!=null&&customerWriteSession.IsCurrent&&customerWriteAttempt.SourceIdentity==customerWriteSession.SourceIdentity&&
            customerWriteAttempt.Request.MatchesOwner(customerWriteSession.Actor,false);
        private void ConfigureCustomerRecovery()
        {
            if(selectionMode||previewMode)return;
            verifyCustomerWriteButton=DesktopComponents.Action("verifyCustomerWrite","تحقق من الحفظ",AppIcon.Refresh,false);
            cancelCustomerWriteButton=DesktopComponents.Action("cancelCustomerWrite","إغلاق المحاولة",AppIcon.Confirm,false);
            verifyCustomerWriteButton.Size=cancelCustomerWriteButton.Size=new Size(144,44);
            verifyCustomerWriteButton.Visible=cancelCustomerWriteButton.Visible=false;
            verifyCustomerWriteButton.Click+=async(s,e)=>await ResolveCustomerWrite(false);
            cancelCustomerWriteButton.Click+=async(s,e)=>await ResolveCustomerWrite(true);
            editorActions.Controls.Add(verifyCustomerWriteButton);editorActions.Controls.Add(cancelCustomerWriteButton);
            Disposed+=(s,e)=>ReleaseCustomerRecovery();
            UiSessionSubscription.Attach(this,ReleaseCustomerRecovery);
        }
        private void ReleaseCustomerRecovery()
        {
            customerWriteVersion++;
            customerPendingStore?.Dispose();customerPendingStore=null;
            customerWriteSession=null;customerWriteAttempt=null;customerRecoveryBlocked=true;
        }
        private void EnsureCustomerRecoveryVisibility()
        {
            if(CustomerRecoveryEnabled&&customerWriteSession!=null&&!customerWriteSession.IsCurrent)
            {ReleaseCustomerRecovery();ResetEditorValues();status.Text=Present("تغيرت جلسة المستخدم. أعد تحميل القائمة.");}
        }
        private bool InitializeCustomerRecovery()
        {
            if(!CustomerRecoveryEnabled)return true;
            if(savingCustomer)return false;
            if(!customerRecoveryBlocked&&customerPendingStore!=null&&customerWriteSession!=null&&customerWriteSession.IsCurrent)return true;
            ReleaseCustomerRecovery();
            try
            {
                var session=CustomerMasterWriteSessionFactory==null?ApplicationServices.CreateCustomerMasterWriteSession():CustomerMasterWriteSessionFactory();
                if(session==null||!session.IsCurrent)throw new UnauthorizedAccessException();
                string directory=CustomerPendingDirectoryOverride??Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"PharmacyManagement","PendingCustomers");
                var store=new CustomerMasterPendingStore(directory,session.SourceIdentity,session.Actor.UserName);
                customerPendingStore=store;customerWriteSession=session;
                customerWriteAttempt=store.Load();customerRecoveryBlocked=false;
                RestoreCustomerRecoveryEditor();ApplyWritePermissionState();
                return true;
            }
            catch(Exception error)
            {
                customerRecoveryBlocked=true;
                AppDiagnostics.Log("CustomerWriteRecoveryLoad",error);
                status.Text=Present("تعذر فتح استعادة العميل. أعد التحديث؛ لم يُرسل حفظ جديد.");
                ApplyWritePermissionState();return false;
            }
        }
        private void ApplyCustomerRecoveryButtons()
        {
            if(verifyCustomerWriteButton==null)return;
            bool pending=CustomerRecoveryEnabled&&customerWriteAttempt!=null&&!customerWriteAttempt.CanDiscard;
            bool available=pending&&!savingCustomer&&!loadingCustomers&&CanRevealCustomerAttempt&&!customerRecoveryBlocked;
            verifyCustomerWriteButton.Visible=cancelCustomerWriteButton.Visible=pending;
            verifyCustomerWriteButton.Enabled=available&&(customerWriteAttempt.Phase==CustomerMasterWritePhase.Unknown||
                customerWriteAttempt.Phase==CustomerMasterWritePhase.Conflict||
                customerWriteAttempt.Phase==CustomerMasterWritePhase.Ready&&customerWriteAttempt.HasUnknownHistory);
            cancelCustomerWriteButton.Enabled=available;
            verifyCustomerWriteButton.Text=Present("تحقق من الحفظ");cancelCustomerWriteButton.Text=Present("إغلاق المحاولة");
            verifyCustomerWriteButton.AccessibleName=verifyCustomerWriteButton.Text;cancelCustomerWriteButton.AccessibleName=cancelCustomerWriteButton.Text;
            bool retry=available&&customerWriteAttempt.Phase==CustomerMasterWritePhase.Ready&&
                customerWriteAttempt.Request.MatchesOwner(customerWriteSession.Actor);
            if(CustomerRecoveryEnabled)
            {saveButton.Enabled=retry||!CustomerRecoveryLocksEditor&&!savingCustomer&&!loadingCustomers&&PermissionService.CanExecute(AppOperation.ManageSalesCustomers);
                saveButton.Text=Present(retry?"إعادة نفس الحفظ":"حفظ");}
        }
        private void RestoreCustomerRecoveryEditor()
        {
            if(!CanRevealCustomerAttempt)return;
            var r=customerWriteAttempt.Request;
            editingCustomerId=r.TargetCustomerId;name.Text=r.CustomerName;phone.Text=r.PhoneNumber;address.Text=r.Address;
            if(customerWriteAttempt.Phase==CustomerMasterWritePhase.Ready&&customerWriteAttempt.HasUnknownHistory)
                status.Text=Present("لم يظهر إثبات للحفظ. أعد نفس المحاولة أو أغلقها قبل إدخال عميل آخر.");
            else if(customerWriteAttempt.Phase==CustomerMasterWritePhase.Conflict)
                status.Text=Present("إثبات المحاولة مختلف. لم تُحفظ هذه البيانات؛ تحقق قبل إجراء جديد.");
            else status.Text=Present("نتيجة حفظ العميل غير مؤكدة. تحقق من الحفظ قبل إجراء جديد.");
            ApplyCustomerRecoveryButtons();
        }
        private async Task SaveCustomerRecoverably()
        {
            if(savingCustomer||loadingCustomers||IsDisposed||Disposing)return;
            if(!InitializeCustomerRecovery())return;
            var session=customerWriteSession;
            var editor=new SalesCustomer{CustomerId=editingCustomerId,CustomerName=name.Text,PhoneNumber=phone.Text,Address=address.Text};
            if(customerWriteAttempt==null)
            {
                try
                {
                    customerWriteAttempt=new CustomerMasterWriteAttempt(new CustomerMasterWriteRequest(Guid.NewGuid(),editor,session.Actor),session.SourceIdentity);
                    customerPendingStore.Save(customerWriteAttempt);
                }
                catch(Exception error)
                {
                    customerRecoveryBlocked=true;AppDiagnostics.Log("CustomerWritePendingSave",error);
                    status.Text=Present("تعذر تجهيز حفظ العميل. راجع البيانات والاستعادة؛ لم يُرسل طلب.");
                    ApplyWritePermissionState();return;
                }
            }
            var attempt=customerWriteAttempt;long version=customerWriteVersion;
            try {attempt.BeginSubmit(session.SourceIdentity,session.Actor,editor);}
            catch(Exception error){AppDiagnostics.Log("CustomerWriteAttemptGuard",error);RestoreCustomerRecoveryEditor();return;}
            savingCustomer=true;reloadButton.Enabled=false;ApplyWritePermissionState();
            status.Text=Present("جارٍ حفظ بيانات العميل…");
            try
            {
                var receipt=await session.SubmitAsync(attempt,CancellationToken.None);
                attempt.ObserveReceipt(receipt);
                if(!CustomerWriteUiCurrent(version,session,attempt))return;
                await CompleteCustomerWrite(attempt,session,version);
            }
            catch(CustomerMasterWriteRejectedException error)
            {
                attempt.ObserveRejected();
                if(!CustomerWriteUiCurrent(version,session,attempt))return;
                if(attempt.CanDiscard)
                {
                    try{customerPendingStore.RemoveResolved(attempt);customerWriteAttempt=null;}
                    catch(Exception cleanup){customerRecoveryBlocked=true;AppDiagnostics.Log("CustomerWriteRejectedCleanup",cleanup);}
                }
                AppDiagnostics.Log("CustomerWriteRejected",error);
                status.Text=Present("لم يُحفظ العميل. راجع البيانات أو تهيئة قاعدة البيانات ثم أعد المحاولة.");
            }
            catch(CustomerMasterWriteContentConflictException error)
            {
                if(attempt.Phase==CustomerMasterWritePhase.Submitting)attempt.ObserveUnknown();
                if(CustomerWriteUiCurrent(version,session,attempt)){AppDiagnostics.Log("CustomerWriteConflict",error);RestoreCustomerRecoveryEditor();}
            }
            catch(Exception error)
            {
                if(attempt.Phase==CustomerMasterWritePhase.Submitting)attempt.ObserveUnknown();
                if(CustomerWriteUiCurrent(version,session,attempt)){AppDiagnostics.Log("CustomerWriteUnknown",error);RestoreCustomerRecoveryEditor();}
            }
            finally
            {
                savingCustomer=false;
                if(!IsDisposed&&!Disposing){reloadButton.Enabled=true;ApplyWritePermissionState();}
            }
        }
        private bool CustomerWriteUiCurrent(long version,CustomerMasterWriteSession session,CustomerMasterWriteAttempt attempt)
        {return !IsDisposed&&!Disposing&&version==customerWriteVersion&&ReferenceEquals(attempt,customerWriteAttempt)&&
            ReferenceEquals(session,customerWriteSession)&&session.IsCurrent;}
        private async Task ResolveCustomerWrite(bool cancel)
        {
            if(savingCustomer||loadingCustomers||!InitializeCustomerRecovery()||!CanRevealCustomerAttempt)return;
            var attempt=customerWriteAttempt;var session=customerWriteSession;long version=customerWriteVersion;
            try
            {
                if(cancel)attempt.BeginCancel(session.SourceIdentity,session.Actor);else attempt.BeginResolve(session.SourceIdentity,session.Actor);
            }
            catch(Exception error){AppDiagnostics.Log("CustomerWriteRecoveryGuard",error);return;}
            savingCustomer=true;reloadButton.Enabled=false;ApplyWritePermissionState();
            status.Text=Present("جارٍ التحقق من محاولة حفظ العميل…");
            try
            {
                if(cancel)attempt.ObserveReceipt(await session.CancelAsync(attempt,CancellationToken.None));
                else attempt.ObserveStatus(await session.ResolveAsync(attempt,CancellationToken.None));
                if(!CustomerWriteUiCurrent(version,session,attempt))return;
                if(attempt.CanDiscard)await CompleteCustomerWrite(attempt,session,version);else RestoreCustomerRecoveryEditor();
            }
            catch(Exception error)
            {
                if(attempt.Phase==CustomerMasterWritePhase.Resolving)attempt.ResolutionFailed();
                else if(attempt.Phase==CustomerMasterWritePhase.Submitting)attempt.ObserveUnknown();
                if(CustomerWriteUiCurrent(version,session,attempt))
                {AppDiagnostics.Log("CustomerWriteRecovery",error);RestoreCustomerRecoveryEditor();}
            }
            finally{savingCustomer=false;if(!IsDisposed&&!Disposing){reloadButton.Enabled=true;ApplyWritePermissionState();}}
        }
        private async Task CompleteCustomerWrite(CustomerMasterWriteAttempt attempt,CustomerMasterWriteSession session,long version)
        {
            customerPendingStore.RemoveResolved(attempt);
            var receipt=attempt.Receipt;
            customerWriteAttempt=null;
            if(receipt!=null&&receipt.Outcome==CustomerMasterWriteOutcome.Committed)
            {
                editingCustomerId=receipt.CustomerId;
                bool refreshed=await LoadCustomers(receipt.CustomerId);
                if(IsDisposed||Disposing||version!=customerWriteVersion||!ReferenceEquals(session,customerWriteSession)||!session.IsCurrent)return;
                status.Text=Present(!receipt.CurrentCustomerMatches
                    ?"ثبت حفظ المحاولة سابقًا وتغير سجل العميل لاحقًا. راجع بيانات القائمة الحالية."
                    :refreshed?"تم حفظ بيانات العميل. أصبح متاحًا الآن للاختيار في فاتورة البيع."
                    :"تم حفظ العميل، لكن تعذر تحديث القائمة. أعد تحميل القائمة.");
            }
            else
            {
                ResetEditorValues();status.Text=Present("أُغلقت المحاولة دون حفظ عميل جديد. يمكنك إدخال بيانات جديدة.");
            }
        }
    }
}



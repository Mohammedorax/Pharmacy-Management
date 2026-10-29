using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using PharmacyManagement.Application;
namespace project_fo_3
{
    public partial class Billing
    {
        private SalesCustomerReferenceReadSession customerReadSession;
        private CancellationTokenSource customerOptionsCancellation,customerSelectionCancellation,customerCreditCancellation;
        private long customerOptionsVersion,customerSelectionVersion,customerCreditVersion;
        private int customerApplying;
        private bool customerOptionsLoading,customerSelectionLoading,customerCreditLoading,customerSelectionReady;
        internal Func<SalesCustomerReferenceReadSession> CustomerReadSessionFactory {get;set;}
        private bool CustomerContextCurrent(SalesCustomerReferenceReadSession session)
            => !IsDisposed&&!Disposing&&session!=null&&ReferenceEquals(session,customerReadSession)&&session.IsCurrent;
        private bool CustomerDraftEditable => !SalesRecoveryLocked&&!previewMode&&!_isInvoiceSaved&&!_isInvoiceCancelled&&!_isSavingInvoice&&!IsDisposed&&!Disposing;
        private SalesCustomerReferenceReadSession CaptureCustomerReadSession()
        {
            if(customerReadSession==null||!customerReadSession.IsCurrent)
                customerReadSession=CustomerReadSessionFactory?.Invoke()??ApplicationServices.CreateSalesCustomerReferenceReadSession();
            return customerReadSession;
        }
        private void CancelCustomerCreditRead()
        {
            ++customerCreditVersion;
            var source=customerCreditCancellation;customerCreditCancellation=null;source?.Cancel();
            customerCreditLoading=false;ResetCustomerCreditDisplay();
        }
        private void CancelCustomerReads()
        {
            ++customerOptionsVersion;++customerSelectionVersion;
            var options=customerOptionsCancellation;customerOptionsCancellation=null;options?.Cancel();
            var selection=customerSelectionCancellation;customerSelectionCancellation=null;selection?.Cancel();
            CancelCustomerCreditRead();customerOptionsLoading=false;customerSelectionLoading=false;
            customerSelectionReady=false;customerReadSession=null;ApplyCustomerReadActions();
        }
        private bool CanUseCustomerSelection()
        {
            if(previewMode)return false;
            return customerSelectionReady&&!customerOptionsLoading&&!customerSelectionLoading&&!customerCreditLoading&&
                CustomerContextCurrent(customerReadSession)&&
                (string.IsNullOrWhiteSpace(_currentCustomerId)||string.Equals(_creditCustomerId,(_currentCustomerId??string.Empty).Trim(),StringComparison.OrdinalIgnoreCase));
        }
        private void ApplyCustomerReadActions()
        {
            UpdateCustomerCreditAvailabilityPresentation();
            if(previewMode||IsDisposed||Disposing||_isInvoiceSaved||_isInvoiceCancelled||_isSavingInvoice)return;
            if(SalesRecoveryLocked){ApplySalesRecoveryLock();return;}
            bool ready=CanUseCustomerSelection()&&!medicineCartPending&&!currencyLoading&&
                (currencyDisplayOverride!=null||(currencySnapshot!=null&&IsCurrencyContextCurrent));
            btnSaveOnly.Enabled=btnPreviewPrint.Enabled=btnPrintA4.Enabled=ready;
            var pdf=flowFooterButtons.Controls["btnSavePdf"];if(pdf!=null)pdf.Enabled=ready;
        }
        private void SetCustomerReadStatus(string message,bool success=false)
        {if(!IsDisposed&&!Disposing){UiTheme.SetStatus(lblStockStatus,Present(message),success);ApplyCustomerReadActions();}}
        internal async Task<bool> LoadCustomerOptionsAsync()
        {
            if(!CustomerDraftEditable)return false;
            ++customerOptionsVersion;long version=customerOptionsVersion;
            var previous=customerOptionsCancellation;previous?.Cancel();
            var cancellation=new CancellationTokenSource();customerOptionsCancellation=cancellation;
            customerOptionsLoading=true;
            SalesCustomerReferenceReadSession session=null;
            try
            {
                session=CaptureCustomerReadSession();
                if(!CustomerContextCurrent(session))throw new UnauthorizedAccessException("أعد تسجيل الدخول لتحميل العملاء.");
                var namesTask=session.ReadAsync(new CustomerReferenceReadRequest(CustomerReferenceKind.Names),cancellation.Token);
                var phonesTask=session.ReadAsync(new CustomerReferenceReadRequest(CustomerReferenceKind.Phones),cancellation.Token);
                await Task.WhenAll(namesTask,phonesTask);
                cancellation.Token.ThrowIfCancellationRequested();
                if(version!=customerOptionsVersion||!CustomerDraftEditable||!CustomerContextCurrent(session))return false;
                customerApplying++;
                try
                {
                    string selectedName=cmbCustomerName.Text,selectedPhone=cmbCustomerPhone.Text;
                    cmbCustomerName.Items.Clear();cmbCustomerName.Items.Add("عميل عام");
                    foreach(string value in namesTask.Result.Values)cmbCustomerName.Items.Add(value);
                    cmbCustomerPhone.Items.Clear();cmbCustomerPhone.Items.Add("000000000");
                    foreach(string value in phonesTask.Result.Values)if(!cmbCustomerPhone.Items.Contains(value))cmbCustomerPhone.Items.Add(value);
                    if(cmbCustomerPhone.DropDownStyle!=ComboBoxStyle.DropDownList)
                    {
                        var values=new AutoCompleteStringCollection();values.AddRange(phonesTask.Result.Values.ToArray());
                        cmbCustomerPhone.AutoCompleteMode=AutoCompleteMode.SuggestAppend;
                        cmbCustomerPhone.AutoCompleteSource=AutoCompleteSource.CustomSource;
                        cmbCustomerPhone.AutoCompleteCustomSource=values;
                    }
                    if(!string.IsNullOrWhiteSpace(selectedName)&&!cmbCustomerName.Items.Contains(selectedName))cmbCustomerName.Items.Add(selectedName);
                    if(!string.IsNullOrWhiteSpace(selectedPhone)&&!cmbCustomerPhone.Items.Contains(selectedPhone))cmbCustomerPhone.Items.Add(selectedPhone);
                    cmbCustomerName.Text=selectedName;cmbCustomerPhone.Text=selectedPhone;
                }
                finally{customerApplying--;}
                return true;
            }
            catch(OperationCanceledException){return false;}
            catch(Exception error)
            {
                if(version==customerOptionsVersion&&!IsDisposed&&!Disposing)
                {customerSelectionReady=false;SetCustomerReadStatus("تعذر تحميل قائمة العملاء. أعد التحديث.");AppDiagnostics.Log("Billing.CustomerOptions",error);}
                return false;
            }
            finally
            {
                if(ReferenceEquals(customerOptionsCancellation,cancellation)){customerOptionsCancellation=null;customerOptionsLoading=false;}
                cancellation.Dispose();ApplyCustomerReadActions();
            }
        }
        internal async Task<bool> SelectCustomerAsync(CustomerReferenceKind kind,string value)
        {
            if(customerApplying!=0||!CustomerDraftEditable)return false;
            ++customerSelectionVersion;long version=customerSelectionVersion;
            var previous=customerSelectionCancellation;previous?.Cancel();CancelCustomerCreditRead();
            var cancellation=new CancellationTokenSource();customerSelectionCancellation=cancellation;
            customerSelectionLoading=true;customerSelectionReady=false;_currentCustomerId=string.Empty;
            RefreshCustomerSummary();SetCustomerReadStatus("جارٍ تحميل بيانات العميل…");
            SalesCustomerReferenceReadSession session=null;
            try
            {
                session=CaptureCustomerReadSession();
                var result=await session.ReadAsync(new CustomerReferenceReadRequest(kind,value),cancellation.Token);
                cancellation.Token.ThrowIfCancellationRequested();
                if(version!=customerSelectionVersion||!CustomerDraftEditable||!CustomerContextCurrent(session))return false;
                if(result.Customer==null)
                {
                    if((kind==CustomerReferenceKind.ByName&&value=="عميل عام")||(kind==CustomerReferenceKind.ByPhone&&value=="000000000"))
                    {
                        customerApplying++;
                        try{cmbCustomerName.Text="عميل عام";cmbCustomerPhone.Text="000000000";txtCustomerAddress.Text="عام";}
                        finally{customerApplying--;}
                        customerSelectionReady=true;RefreshCustomerSummary();SetCustomerReadStatus("تم اختيار العميل العام.",true);return true;
                    }
                    SetCustomerReadStatus("العميل غير موجود. أعد اختياره أو أضفه من شاشة العملاء.");return false;
                }
                var customer=result.Customer;
                if(kind==CustomerReferenceKind.ById&&!string.Equals((customer.CustomerId??string.Empty).Trim(),(value??string.Empty).Trim(),StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("نتيجة العميل لا تطابق العميل المختار.");
                customerApplying++;
                try
                {
                    _currentCustomerId=customer.CustomerId??string.Empty;
                    if(!cmbCustomerName.Items.Contains(customer.CustomerName??string.Empty))cmbCustomerName.Items.Add(customer.CustomerName??string.Empty);
                    if(!cmbCustomerPhone.Items.Contains(customer.PhoneNumber??string.Empty))cmbCustomerPhone.Items.Add(customer.PhoneNumber??string.Empty);
                    cmbCustomerName.Text=customer.CustomerName??string.Empty;cmbCustomerPhone.Text=customer.PhoneNumber??string.Empty;
                    txtCustomerAddress.Text=customer.Address??string.Empty;
                }
                finally{customerApplying--;}
                RefreshCustomerSummary();
                customerSelectionReady=true;
                if(string.IsNullOrWhiteSpace(_currentCustomerId))return true;
                return await LoadCustomerCreditAsync(session);
            }
            catch(OperationCanceledException){return false;}
            catch(Exception error)
            {
                if(version==customerSelectionVersion&&!IsDisposed&&!Disposing)
                {customerSelectionReady=false;SetCustomerReadStatus("تعذر تحميل العميل. أعد اختياره.");AppDiagnostics.Log("Billing.CustomerSelection",error);}
                return false;
            }
            finally
            {
                if(ReferenceEquals(customerSelectionCancellation,cancellation)){customerSelectionCancellation=null;customerSelectionLoading=false;}
                cancellation.Dispose();ApplyCustomerReadActions();
            }
        }
        internal async Task<bool> LoadCustomerCreditAsync(SalesCustomerReferenceReadSession session)
        {
            if(!CustomerDraftEditable||!CustomerContextCurrent(session))return false;
            string id=(_currentCustomerId??string.Empty).Trim();
            CancelCustomerCreditRead();if(id.Length==0)return true;
            long version=customerCreditVersion;
            var cancellation=new CancellationTokenSource();customerCreditCancellation=cancellation;
            customerCreditLoading=true;SetCustomerReadStatus("جارٍ قراءة رصيد العميل…");
            try
            {
                var result=await session.ReadCreditAsync(new SalesCustomerCreditReadRequest(id),cancellation.Token);
                cancellation.Token.ThrowIfCancellationRequested();
                if(version!=customerCreditVersion||!CustomerDraftEditable||!CustomerContextCurrent(session)||
                    !string.Equals(id,(_currentCustomerId??string.Empty).Trim(),StringComparison.OrdinalIgnoreCase))return false;
                _creditCustomerId=id;_displayedCustomerCreditBalance=result.Balance;
                lblCustomerCreditBalance.Text=Present("رصيد العميل المتاح: "+FormatMoney(result.Balance));
                chkUseCustomerCredit.Enabled=result.Balance>0m;chkUseCustomerCredit.Checked=false;
                SetCustomerReadStatus("تم تحميل بيانات العميل ورصيده.",true);return true;
            }
            catch(OperationCanceledException){return false;}
            catch(Exception error)
            {
                if(version==customerCreditVersion&&!IsDisposed&&!Disposing)
                {ResetCustomerCreditDisplay();customerSelectionReady=false;SetCustomerReadStatus("تعذر قراءة رصيد العميل. أعد اختيار العميل.");AppDiagnostics.Log("Billing.CustomerCredit",error);}
                return false;
            }
            finally
            {
                if(ReferenceEquals(customerCreditCancellation,cancellation)){customerCreditCancellation=null;customerCreditLoading=false;}
                cancellation.Dispose();ApplyCustomerReadActions();
            }
        }
    }
}

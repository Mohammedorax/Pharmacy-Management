using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using PharmacyManagement.Application;
using PharmacyManagement.Domain;
namespace project_fo_3
{
 public partial class Billing
 {
  SalesWriteRecoveryCoordinator salesRecovery;
  SalesWriteIntent salesPending;
  bool salesRecoveryInitialized,salesRecoveryBusy,salesRecoveryFault,salesRecoveryStale;
  string salesRecoverySource;long salesRecoveryRevision;
  Panel salesRecoveryBar;Label salesRecoveryMessage;Button salesRecoverButton,salesCloseAttemptButton;
  readonly Dictionary<Control,bool> salesRecoveryEnabled=new Dictionary<Control,bool>();
  internal string SalesRecoveryDirectoryForVerification {get;set;}
  internal ISalesWriteRecoveryPort SalesRecoveryPortForVerification {get;set;}
  bool SalesRecoveryEnabled => unitCatalogEnabled&&currencyDisplayOverride==null;
  private bool CanProceedWithSalesSave() => _isInvoiceSaved||salesPending!=null||CanUseCustomerSelection();
  internal bool SalesRecoveryLocked => SalesRecoveryEnabled && (!salesRecoveryInitialized||salesPending!=null||salesRecoveryBusy||salesRecoveryFault||salesRecoveryStale||(salesRecoveryInitialized&&!SalesRecoveryCurrent));
  internal bool SalesRecoveryHasPending => salesPending!=null;
  bool SalesRecoveryCurrent => !IsDisposed&&!Disposing&&!salesRecoveryStale&&SessionContext.IsAuthenticated&&
   SessionContext.CurrentSessionRevision==salesRecoveryRevision&&Database.ConnectionString==salesRecoverySource&&PermissionService.CanOpen(AppPermission.Sales);
  void InitializeSalesRecoveryControls()
  {
   if(!SalesRecoveryEnabled)return;
   salesRecoveryBar=new Panel{Name="salesRecoveryBar",Dock=DockStyle.Top,Height=92,Padding=new Padding(12),BackColor=Color.FromArgb(255,245,215),Visible=false};
   salesRecoveryMessage=new Label{Name="salesRecoveryStatus",Dock=DockStyle.Top,Height=32,AutoEllipsis=true,ForeColor=Color.FromArgb(90,55,10),RightToLeft=presentationLeftToRight?RightToLeft.No:RightToLeft.Yes,TextAlign=presentationLeftToRight?ContentAlignment.MiddleLeft:ContentAlignment.MiddleRight};
   if(string.Equals(Properties.Setings.Default.ThemeMode,"Dark",StringComparison.OrdinalIgnoreCase)){salesRecoveryBar.BackColor=Color.FromArgb(66,53,28);salesRecoveryMessage.ForeColor=Color.FromArgb(255,235,180);}
   var actions=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=34,FlowDirection=presentationLeftToRight?FlowDirection.LeftToRight:FlowDirection.RightToLeft};
   salesRecoverButton=new Button{Name="salesRecoverResult",Text=presentationLeftToRight?"Check result":"تحقق من النتيجة",AutoSize=true,Height=30};
   salesCloseAttemptButton=new Button{Name="salesCloseUnposted",Text=presentationLeftToRight?"Close unposted attempt":"إغلاق المحاولة غير المعتمدة",AutoSize=true,Height=30};
   actions.Controls.Add(salesRecoverButton);actions.Controls.Add(salesCloseAttemptButton);salesRecoveryBar.Controls.Add(salesRecoveryMessage);salesRecoveryBar.Controls.Add(actions);Controls.Add(salesRecoveryBar);salesRecoveryBar.BringToFront();
   salesRecoverButton.Click+=async(s,e)=>await RecoverSalesAttemptAsync(false);
   salesCloseAttemptButton.Click+=async(s,e)=>await RecoverSalesAttemptAsync(true);
   UiSessionSubscription.Attach(this,InvalidateSalesRecovery);
   FormClosing+=(s,e)=>{if(salesRecoveryBusy){e.Cancel=true;SetSalesRecoveryStatus("جاري التحقق من الحفظ؛ انتظر اكتمال النتيجة قبل إغلاق الشاشة.","Checking the save result; wait before closing this screen.");}};
   Disposed+=(s,e)=>{if(!salesRecoveryBusy){salesRecovery?.Dispose();salesRecovery=null;}};
  }
  void InvalidateSalesRecovery()
  {
   if(!SalesRecoveryEnabled)return;salesRecoveryStale=true;savedUnitPresentation=null;acceptedUnitOrigin=null;ReleaseInvoicePrintJobs();
   SetSalesRecoveryStatus("تغيرت جلسة البيع. أعد فتح الشاشة بحساب صاحب المحاولة للتحقق من نتيجتها.","Sales session changed. Reopen this screen as the attempt owner to check its result.");
   ApplySalesRecoveryLock();if(!salesRecoveryBusy){salesRecovery?.Dispose();salesRecovery=null;}
  }
  void SetSalesRecoveryStatus(string ar,string en)
  {if(IsDisposed||Disposing||salesRecoveryBar==null)return;salesRecoveryMessage.Text=presentationLeftToRight?en:ar;salesRecoveryBar.Visible=true;salesRecoveryBar.BringToFront();}
  IEnumerable<Control> SalesControls(Control root)
  {foreach(Control c in root.Controls){yield return c;foreach(var child in SalesControls(c))yield return child;}}
  void ApplySalesRecoveryLock()
  {
   if(!SalesRecoveryEnabled||IsDisposed||Disposing)return;
   bool locked=SalesRecoveryLocked;
   foreach(var c in SalesControls(this).ToArray()){
    if(c==btnBack||c==salesRecoverButton||c==salesCloseAttemptButton||c==salesRecoveryBar||c==salesRecoveryMessage||salesRecoveryBar?.Contains(c)==true)continue;
    bool input=c is TextBoxBase||c is ComboBox||c is DataGridView||c is ButtonBase||c is AntdUI.Button||c is AntdUI.Input||c is AntdUI.Select||c==medicineUnitPicker;
    if(!input)continue;if(locked){if(!salesRecoveryEnabled.ContainsKey(c))salesRecoveryEnabled.Add(c,c.Enabled);c.Enabled=false;}
    else if(salesRecoveryEnabled.TryGetValue(c,out bool was)){c.Enabled=was;salesRecoveryEnabled.Remove(c);}
   }
   if(salesRecoverButton!=null)salesRecoverButton.Enabled=!salesRecoveryBusy&&!salesRecoveryStale&&salesRecovery!=null&&salesPending!=null;
   if(salesCloseAttemptButton!=null)salesCloseAttemptButton.Enabled=salesRecoverButton.Enabled;
   btnBack.Enabled=!salesRecoveryBusy;
   if(!locked&&salesRecoveryBar!=null)salesRecoveryBar.Visible=false;
  }
  internal Task InitializeSalesRecoveryAsync()
  {
   if(!SalesRecoveryEnabled||salesRecoveryInitialized)return Task.CompletedTask;
   salesRecoverySource=Database.ConnectionString;salesRecoveryRevision=SessionContext.CurrentSessionRevision;
   try{
    string directory=SalesRecoveryDirectoryForVerification??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"PharmacyManagement","pending-sales");
    salesRecovery=SalesRecoveryPortForVerification==null?new SalesWriteRecoveryCoordinator(directory):new SalesWriteRecoveryCoordinator(directory,SalesRecoveryPortForVerification);
    salesPending=salesRecovery.Pending();salesRecoveryInitialized=true;
    if(salesPending!=null){DisplaySalesPending(salesPending,null);SetSalesRecoveryStatus("توجد محاولة بيع محفوظة. تحقق من النتيجة قبل بدء فاتورة أخرى.","A saved sales attempt exists. Check its result before starting another invoice.");}
   }catch(Exception e){salesRecoveryFault=true;salesRecoveryInitialized=true;AppDiagnostics.Log("SalesRecoveryOpen",e);SetSalesRecoveryStatus("تعذر فتح استرجاع البيع. احتُفظ بالملف؛ أغلق نافذة البيع الأخرى أو أعد فتح الشاشة بحساب صاحب المحاولة.","Cannot open sales recovery. The file is retained; close the other sales window or reopen as the attempt owner.");}
   ApplySalesRecoveryLock();return Task.CompletedTask;
  }
  void DisplaySalesPending(SalesWriteIntent intent,SalesInvoiceIssueResult result)
  {
   if(!SalesRecoveryCurrent)throw new UnauthorizedAccessException("تغيرت جلسة استرجاع البيع.");
   var q=intent.Snapshot();using(SessionCommitGate.Commit(()=>SalesRecoveryCurrent)){
    CancelMedicineReads();customerApplying++;try{
     BillingGV.Rows.Clear();cartUnits.Clear();_sessionBillLineIds.Clear();tempTransaction.Clear();_appliedDiscountPercent=q.DiscountPercent;
     for(int n=0;n<q.Lines.Count;n++){var l=q.Lines[n];string key=result==null?"DRAFT-RECOVERY-"+n:result.IsCommercial?"C-"+result.CommercialInvoiceNumber:result.BillLineIds[n];
      decimal price=l.Unit?.UnitPrice??l.DisplayedSellingPrice,quantity=l.Unit?.EnteredQuantity??l.Quantity,total=l.Unit?.GrossTotal??price*quantity;
      int index=BillingGV.Rows.Add(key,l.MedicineName,price,quantity,total);if(l.Unit!=null){cartUnits[BillingGV.Rows[index]]=l.Unit;if(BillingGV.Columns.Contains("Unit"))BillingGV.Rows[index].Cells["Unit"].Value=l.Unit.Name;}
      if(result!=null&&!result.IsCommercial)_sessionBillLineIds.Add(key);
     }
     _currentCustomerId=result?.CustomerId??q.CustomerId;cmbCustomerName.Text=q.CustomerName;cmbCustomerPhone.Text=q.PhoneNumber;txtCustomerAddress.Text=q.CustomerAddress;txtInvoiceNotes.Text=q.Notes;txtDiscountPercent.Text=q.DiscountPercent.ToString();
     CustomerIdLabel.Text=PresentFormat("رقم العميل: {0}",_currentCustomerId);CustomerNameLabel.Text=PresentFormat("اسم العميل: {0}",q.CustomerName);CustomerPhoneLabel.Text=PresentFormat("هاتف العميل: {0}",q.PhoneNumber);CustomerAddressLabel.Text=PresentFormat("عنوان العميل: {0}",q.CustomerAddress);
     EmployeeLabel.Text=PresentFormat("اسم الكاشير: {0}",q.EmployeeName);EmployeeIdLabel.Text=PresentFormat("رقم الكاشير: {0}",q.CashierEmployeeId);TimingLabel.Text=FormatSalesTiming(q.InvoiceDate);
     savedLegacyCurrencyText=q.ExpectedCurrencyCode;_currentCommercialInvoiceNumber=result?.IsCommercial==true?result.CommercialInvoiceNumber:null;
     _currentCommercialInvoiceSnapshot=null;savedUnitPresentation=null;acceptedUnitOrigin=null;_isInvoiceSaved=result!=null;_isInvoiceCancelled=false;
     PriceBDLabel.Text=Present("الإجمالي")+": "+(result?.GrossTotal??q.DisplayedGrossTotal).ToString("N2")+" "+q.ExpectedCurrencyCode;
     PriceADLabel.Text=Present("الصافي")+": "+(result?.NetTotal??q.DisplayedNetTotal).ToString("N2")+" "+q.ExpectedCurrencyCode;
     BillingGV.ReadOnly=result!=null;
    }finally{customerApplying--;}
   }
  }
  async Task FinishSalesRecoveryDisplayAsync(SalesInvoiceIssueResult result,SalesWriteIntent intent)
  {
   if(result==null)throw new InvalidOperationException("لم تتأكد نتيجة الفاتورة.");
   DisplaySalesPending(intent,result);
   if(result.IsCommercial){
    var snapshot=await Task.Run(()=>CommercialInvoiceSnapshotRepository.LoadIssued(result.CommercialInvoiceId.Value));
    if(!SalesRecoveryCurrent)throw new UnauthorizedAccessException("تغيرت جلسة الطباعة.");
    using(SessionCommitGate.Commit(()=>SalesRecoveryCurrent)){_currentCommercialInvoiceSnapshot=snapshot;acceptedUnitOrigin=new MedicineUnitPresentationContext(GetSavedSalesReference(),SavedUnitRowIdentity());savedUnitPresentation=new Dictionary<string,MedicineUnitSelection>();}
   }else{
    var origin=RequireSavedUnitOrigin();var keys=SavedUnitLineKeys();var frozen=await Task.Run(()=>MedicineUnitInvoiceBridge.ReadSavedPresentation("Sale",keys,origin));
    if(!SalesRecoveryCurrent)throw new UnauthorizedAccessException("تغيرت جلسة الطباعة.");using(SessionCommitGate.Commit(()=>SalesRecoveryCurrent)){FreezeSavedUnitPresentation(frozen);}
   }
   GuardFrozenUnitPresentation();
   await Task.Run(()=>salesRecovery.CompleteDisplayed(result));
   if(!SalesRecoveryCurrent)throw new UnauthorizedAccessException("تغيرت جلسة البيع.");
   salesPending=null;btnSaveOnly.Enabled=false;btnSaveOnly.AccessibleDescription=Present("تم حفظ الفاتورة؛ افتح فاتورة جديدة قبل الحفظ مرة أخرى.");SetCancelSavedInvoiceEnabled(!result.IsCommercial);ApplyCommittedCustomerCredit(result.CustomerCreditApplied);
  }
  internal async Task<bool> RecoverSalesAttemptAsync(bool closeUnposted)
  {
   if(salesRecoveryBusy||!SalesRecoveryCurrent||salesRecovery==null||salesPending==null)return false;
   salesRecoveryBusy=true;ApplySalesRecoveryLock();SetSalesRecoveryStatus("جارٍ التحقق من نتيجة المحاولة المحفوظة…","Checking the saved attempt result…");
   try{
    var observation=await Task.Run(()=>closeUnposted?salesRecovery.ConfirmAbandon():salesRecovery.Observe());
    if(!SalesRecoveryCurrent)return false;
    if(observation==null){SetSalesRecoveryStatus("لم تتأكد نتيجة الحفظ. تحقق مجدداً أو أغلق المحاولة غير المعتمدة؛ لن يُعاد إرسالها تلقائياً.","Save result is unconfirmed. Check again or close the unposted attempt; it will not be resent automatically.");return false;}
    if(observation.IsAbandoned){salesPending=null;salesRecoveryFault=false;BillingGV.Rows.Clear();cartUnits.Clear();Clear();SetSalesRecoveryStatus("أُغلقت المحاولة دون اعتماد؛ يمكنك إعداد فاتورة جديدة.","The attempt was closed without posting. You can start a new invoice.");return true;}
    if(observation.IsCancelled){SetSalesRecoveryStatus("اعتمدت الفاتورة ثم ألغيت. راجعها من سجل الفواتير؛ الطباعة محظورة.","This invoice was posted then cancelled. Check invoice history; printing is blocked.");return false;}
    await FinishSalesRecoveryDisplayAsync(observation.Result,salesPending);return true;
   }catch(Exception e){AppDiagnostics.Log("SalesRecoveryCheck",e);SetSalesRecoveryStatus("تعذر تأكيد النتيجة أو تجهيز لقطة الطباعة. احتُفظ بالمحاولة؛ تحقق بعد عودة الاتصال.","Cannot confirm the result or prepare its print snapshot. The attempt is retained; check after connection returns.");return false;}
   finally{salesRecoveryBusy=false;FinishSalesRecoveryActions();}
  }
  void FinishSalesRecoveryActions()
  {
   if(IsDisposed||Disposing||salesRecoveryStale){salesRecovery?.Dispose();salesRecovery=null;return;}
   ApplySalesRecoveryLock();ApplyMedicineReadActions();
   if(_isInvoiceSaved&&salesPending==null){BillingGV.ReadOnly=true;btnSaveOnly.Enabled=false;btnPreviewPrint.Enabled=btnPrintA4.Enabled=true;var pdf=flowFooterButtons.Controls["btnSavePdf"];if(pdf!=null)pdf.Enabled=true;}
   ApplySalesRecoveryLock();
  }
  internal async Task<bool> SaveInvoiceToDatabaseAsync()
  {
   if(!CanProceedWithSalesSave())return false;
   if(!SalesRecoveryEnabled)throw new InvalidOperationException("يتطلب الحفظ شاشة بيع فعلية.");
   await InitializeSalesRecoveryAsync();
   if(salesRecoveryBusy||salesRecoveryFault||!SalesRecoveryCurrent)return false;
   if(salesPending!=null)return await RecoverSalesAttemptAsync(false);
   if(_isInvoiceCancelled)throw new InvalidOperationException("تم إلغاء الفاتورة؛ افتح فاتورة جديدة.");
   if(_isInvoiceSaved){GuardFrozenUnitPresentation();return true;}
   if(medicineCartPending||!CommitBillingCartEdit()||!CanUseCustomerSelection())return false;
   if(!PermissionService.EnsureAllowed(AppOperation.PostSalesInvoice,this))return false;
   ApplyDefaultCustomer();ApplyCurrentCashier();var now=DateTime.Now;
   var request=CreateSalesIssueRequest(_currentCustomerId,string.IsNullOrWhiteSpace(cmbCustomerName.Text)?"عميل عام":cmbCustomerName.Text.Trim(),string.IsNullOrWhiteSpace(cmbCustomerPhone.Text)?"000000000":cmbCustomerPhone.Text.Trim(),string.IsNullOrWhiteSpace(txtCustomerAddress.Text)?"عام":txtCustomerAddress.Text.Trim(),CalculateNetTotal(CalculateGrossTotal()),(SelectEmployee.SelectedValue as ComboBoxItem)?.Text??SessionContext.CurrentUser,now,txtInvoiceNotes.Text.Trim());
   salesRecoveryBusy=true;_isSavingInvoice=true;ApplySalesRecoveryLock();refreshInvoiceState?.Invoke();
   SetSalesRecoveryStatus("جارٍ حفظ الفاتورة. انتظر تأكيد النتيجة…","Saving invoice. Wait for confirmation…");
   try{
    // A bounded single-screen worker executes the existing synchronous financial transaction.
    // It is deliberately not described as native asynchronous SQL. No cancellation aborts Commit.
    var result=await Task.Run(()=>{bool commercial;using(var c=new SqlConnection(salesRecoverySource)){c.Open();commercial=CommercialSchemaInstaller.IsFullCommercialModeRequested(c,null);}return salesRecovery.Submit(request,commercial);});
    if(!SalesRecoveryCurrent)return false;salesPending=salesRecovery.Pending();await FinishSalesRecoveryDisplayAsync(result,salesPending);return true;
   }catch(Exception e){
    AppDiagnostics.Log("SalesRecoverySubmit",e);
    if(SalesRecoveryCurrent){try{salesPending=salesRecovery.Pending();}catch(Exception load){salesRecoveryFault=true;AppDiagnostics.Log("SalesRecoveryPending",load);}}
    SetSalesRecoveryStatus("لم نتمكن من تأكيد اعتماد الفاتورة. تحقق من النتيجة قبل بدء فاتورة أخرى.","Invoice posting could not be confirmed. Check the result before starting another invoice.");return false;
   }finally{_isSavingInvoice=false;salesRecoveryBusy=false;FinishSalesRecoveryActions();refreshInvoiceState?.Invoke();}
  }
 }
}

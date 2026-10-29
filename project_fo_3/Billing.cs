using PharmacyManagement.Domain;
// شرح الملف: شاشة الفواتير والمبيعات.
// تدير إضافة الأدوية للفاتورة، حساب الإجمالي والخصم، الطباعة، وتسجيل بيانات العميل.
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Drawing.Printing;
using System.Drawing;
using System.Data;
using System.Windows.Forms;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Application;

namespace project_fo_3
{
    // شاشة الفواتير والمبيعات.
    // تدير اختيار الأدوية، حساب الفاتورة، الخصم، الطباعة، وتسجيل بيانات العميل مع حماية المخزون عند الإلغاء.
    public partial class Billing : Form
    {
        private AntdUI.Button btnBillingCustomerType;
        private readonly AntdUI.Button btnRefreshMedicineNames = DesktopComponents.Action("billingRefreshMedicines", "تحديث الأصناف", AppIcon.Refresh);
        private int? selectedStockPreview;
        private MedicineUnitPicker medicineUnitPicker;
        private readonly Dictionary<DataGridViewRow,MedicineUnitSelection> cartUnits = new Dictionary<DataGridViewRow,MedicineUnitSelection>();
        private readonly bool unitCatalogEnabled;
        private Dictionary<string,MedicineUnitSelection> savedUnitPresentation;
        private MedicineUnitPresentationContext acceptedUnitOrigin;
        private string SavedUnitRowIdentity()=>PrintedCurrencyText+"|"+string.Join("|",BillingGV.Rows.Cast<DataGridViewRow>().Where(r=>!r.IsNewRow)
            .SelectMany(r=>r.Cells.Cast<DataGridViewCell>().Select(c=>Convert.ToString(c.Value,System.Globalization.CultureInfo.InvariantCulture)??string.Empty)).Select(v=>v.Length+":"+v));
        private MedicineUnitPresentationContext RequireSavedUnitOrigin()
        {
            if(acceptedUnitOrigin==null)acceptedUnitOrigin=new MedicineUnitPresentationContext(GetSavedSalesReference(),SavedUnitRowIdentity());
            if(!acceptedUnitOrigin.Matches(GetSavedSalesReference(),SavedUnitRowIdentity())){savedUnitPresentation=null;throw new InvalidOperationException("تغير سياق الفاتورة المحفوظة؛ أعد فتحها من السجل قبل الطباعة.");}
            return acceptedUnitOrigin;
        }
        private bool SavedUnitPresentationCurrent()
        {
            if(previewMode || !_isInvoiceSaved)return true;
            bool current=!IsDisposed&&!Disposing && acceptedUnitOrigin!=null && acceptedUnitOrigin.Matches(GetSavedSalesReference(),SavedUnitRowIdentity()) && savedUnitPresentation!=null;
            if(!current)savedUnitPresentation=null;return current;
        }
        private void GuardFrozenUnitPresentation()
        {if(!SavedUnitPresentationCurrent())throw new InvalidOperationException("لقطة الفاتورة غير مكتملة أو تغيرت الجلسة؛ أعد فتحها من السجل قبل الطباعة.");}

        private MedicineUnitSelection PrintUnit(DataGridViewRow row)
        {
            if(!_isInvoiceSaved || previewMode)return CartUnit(row);
            GuardFrozenUnitPresentation();
            MedicineUnitSelection unit;
            if(savedUnitPresentation==null || !savedUnitPresentation.TryGetValue(Convert.ToString(row.Cells["BillId"].Value),out unit))
                throw new InvalidOperationException("لم تكتمل لقطة وحدات الفاتورة المحفوظة؛ أعد تحميلها قبل الطباعة.");
            return unit;
        }
        internal void FreezeSavedUnitPresentation(IReadOnlyDictionary<string,MedicineUnitSelection> frozen)
        {
            savedUnitPresentation=null;
            RequireSavedUnitOrigin();
            if(frozen==null)throw new ArgumentNullException(nameof(frozen));
            var verified=new Dictionary<string,MedicineUnitSelection>(StringComparer.Ordinal);
            foreach(DataGridViewRow row in BillingGV.Rows)
            {
                if(row.IsNewRow)continue;string key=Convert.ToString(row.Cells["BillId"].Value);MedicineUnitSelection unit;
                if(string.IsNullOrWhiteSpace(key)||!frozen.TryGetValue(key,out unit) || (CartUnit(row)!=null && unit==null))
                    throw new InvalidOperationException("لقطة وحدات الفاتورة غير مكتملة؛ الطباعة محظورة.");
                if(unit!=null && (unit.UnitPrice!=Convert.ToDecimal(row.Cells["Sprice"].Value) || unit.EnteredQuantity!=Convert.ToDecimal(row.Cells["Quantity"].Value) || unit.GrossTotal!=Convert.ToDecimal(row.Cells["TotalAmount"].Value)))
                    throw new InvalidOperationException("لقطة وحدة الفاتورة لا تطابق السطر المعروض؛ الطباعة محظورة.");
                verified.Add(key,unit);
            }
            if(verified.Count!=frozen.Count)throw new InvalidOperationException("لقطة وحدات الفاتورة لا تطابق عدد السطور المعروضة.");
            savedUnitPresentation=verified;
        }
        private string[] SavedUnitLineKeys()=>BillingGV.Rows.Cast<DataGridViewRow>().Where(r=>!r.IsNewRow).Select(r=>Convert.ToString(r.Cells["BillId"].Value)).ToArray();
        private void LoadSavedUnitPresentation()
        {
            savedUnitPresentation=null;var origin=RequireSavedUnitOrigin();
            FreezeSavedUnitPresentation(MedicineUnitInvoiceBridge.ReadSavedPresentation("Sale",SavedUnitLineKeys(),origin));
        }
        internal MedicineUnitSelection CartUnit(DataGridViewRow row) { MedicineUnitSelection unit; return cartUnits.TryGetValue(row,out unit)?unit:null; }
        private long OtherCartBaseQuantity(string medicine, DataGridViewRow excluded=null) => BillingGV.Rows.Cast<DataGridViewRow>()
            .Where(r=>!r.IsNewRow && r!=excluded && string.Equals(Convert.ToString(r.Cells["MedName"].Value),medicine,StringComparison.OrdinalIgnoreCase))
            .Sum(r=>(long)(CartUnit(r)?.BaseQuantity ?? Convert.ToInt32(r.Cells["Quantity"].Value)));
        private readonly IMedicineService medicineService;
        private readonly IMedicineSearchService medicineSearchService;
        private CancellationTokenSource medicineBarcodeCancellation;
        private long medicineBarcodeVersion, medicineBarcodeSelectionVersion = -1;
        private bool medicineBarcodeLoading, medicineBarcodeApplyingSelection;
        private readonly string currencyDisplayOverride;
        private readonly ICurrencyReadService currencyReadService;
        private CurrencyDisplaySnapshot currencySnapshot;
        private OperationActor currencyActor;
        private string currencyEmployee, savedLegacyCurrencyText;
        private Func<bool> currencyConnectionIsCurrent;
        private CancellationTokenSource currencyCancellation;
        private long currencyReadVersion;
        private bool currencyLoading;
        private CancellationTokenSource medicineNamesCancellation, medicineSelectionCancellation, medicineCartCancellation;
        private long medicineNamesVersion, medicineSelectionVersion;
        private bool medicineNamesLoading, medicineSelectionLoading, medicineCartPending;
        private bool isCommercialInvoiceHistory;
        private bool isLegacyMappedInvoiceHistory;
        private bool invoiceHistoryRequiresReload;
        private DataTable invoiceHistoryAllRows;
        private int invoiceHistoryPageSize = 500;
        private Button invoiceHistoryShowMore;
        private Button invoiceHistoryExport;
        private FlowLayoutPanel invoiceHistoryBar;
        private readonly Func<bool, SalesHistoryPage> salesHistoryLoader;
        private readonly ISalesInvoiceCancellationOperation salesCancellationOperation;
        private readonly bool previewMode;
        private readonly FrequentMedicineCartRecorder frequentMedicineRecorder;
        private SalesShortcutProfile salesShortcutProfile = SalesShortcutProfile.Defaults;
        private readonly bool presentationLeftToRight;
        private TextBox stableInvoiceNotesEditor;
        private bool IsCurrencyContextCurrent => currencyActor != null && SessionContext.IsAuthenticated &&
            string.Equals(currencyActor.UserName, SessionContext.CurrentUser, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(currencyActor.UserRole, SessionContext.CurrentRole, StringComparison.OrdinalIgnoreCase) &&
            currencyEmployee == SessionContext.CurrentEmployeeId && currencyConnectionIsCurrent != null && currencyConnectionIsCurrent();
        private string CurrencyText => currencySnapshot != null && IsCurrencyContextCurrent ? currencySnapshot.DisplayText :
            currencyDisplayOverride ?? Present("عملة غير متاحة");
        internal string PrintedCurrencyText => _currentCommercialInvoiceSnapshot != null ? (string.IsNullOrWhiteSpace(_currentCommercialInvoiceSnapshot.CurrencyCode) ? Present("عملة غير محفوظة") : _currentCommercialInvoiceSnapshot.CurrencyCode) :
            savedLegacyCurrencyText ?? currencyDisplayOverride ?? Present("عملة غير محفوظة");

        private string FormatMoney(decimal amount)
        {
            return $"{amount:N2} {CurrencyText}";
        }

        // ينشئ شاشة الفواتير ويطبق التصميم الموحد.
        internal bool PresentationLeftToRight => presentationLeftToRight;

        public Billing() : this(false, UiLanguageCatalog.IsEnglishActive)
        {
        }

        internal Billing(bool previewMode, bool presentationLeftToRight, IMedicineService medicineService = null, Func<string> currencyDisplay = null, IMedicineSearchService medicineSearchService = null, ICurrencyReadService currencyReadService = null)
            : this(previewMode, presentationLeftToRight, medicineService, currencyDisplay, medicineSearchService, currencyReadService, null) { }

        internal Billing(bool previewMode, bool presentationLeftToRight, IMedicineService medicineService, Func<string> currencyDisplay, IMedicineSearchService medicineSearchService, ICurrencyReadService currencyReadService, FrequentMedicineCartRecorder frequentMedicineRecorder,
            Func<bool, SalesHistoryPage> salesHistoryLoader = null, ISalesInvoiceCancellationOperation salesCancellationOperation = null)
        {
            this.salesHistoryLoader = salesHistoryLoader ?? ApplicationServices.SalesHistory.Load;
            this.salesCancellationOperation = salesCancellationOperation ?? ApplicationServices.SalesInvoiceCancellations;
            unitCatalogEnabled = !previewMode && medicineService == null;
            this.medicineService = medicineService ?? ApplicationServices.Medicines;
            this.medicineSearchService = medicineSearchService ?? ApplicationServices.MedicineSearch;
            this.currencyReadService = currencyReadService ?? ApplicationServices.CurrencyRead;
            currencyDisplayOverride = currencyDisplay?.Invoke() ?? (previewMode ? "ر.ي" : null);
            this.previewMode = previewMode;
            this.presentationLeftToRight = presentationLeftToRight;
            if (!previewMode && SessionContext.IsAuthenticated)
            {
                try { salesShortcutProfile = new SalesShortcutProfileStore(SalesShortcutProfileStore.DefaultDirectory).Load(SessionContext.CurrentUser); }
                catch (Exception error) { AppDiagnostics.Log("BillingShortcutSettingsLoad", error); }
            }
            this.frequentMedicineRecorder = frequentMedicineRecorder;
            // Production captures the signed-in profile once. Injected providers and preview do not write personal preferences by default.
            if (this.frequentMedicineRecorder == null && !previewMode && medicineService == null && SessionContext.IsAuthenticated)
            {
                try { this.frequentMedicineRecorder = new FrequentMedicineCartRecorder(
                    ApplicationServices.CreateFrequentMedicineSession(),
                    error => AppDiagnostics.Log("BillingFrequentPreferencesWrite", error)); }
                catch (Exception error) { AppDiagnostics.Log("BillingFrequentPreferencesCapture", error); }
            }
            InitializeComponent();
            // Layout uses shared pixel/DPI metrics; font autoscale was inflating the minimum height on first Show.
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96F, 96F);
            MinimumSize = new Size(960, 640);
            if (previewMode) Load -= Billing_Load;
            InitializeCustomerCreditControls();
            ConfigureCustomerMasterSelection();
            cmbCustomerName.SelectionChangeCommitted += async (sender,args) =>
            {
                if(customerApplying==0&&!customerSelectionLoading&&!CanUseCustomerSelection())
                    await SelectCustomerAsync(CustomerReferenceKind.ByName,cmbCustomerName.Text);
            };
            cmbCustomerPhone.SelectionChangeCommitted += async (sender,args) =>
            {
                if(customerApplying==0&&!customerSelectionLoading&&!CanUseCustomerSelection())
                    await SelectCustomerAsync(CustomerReferenceKind.ByPhone,cmbCustomerPhone.Text);
            };
            InitializeCustomerTypeIndicator();
            UiTheme.Apply(this);
            ApplySalesValueCaptions();
            if (previewMode) UiTheme.RemoveStatusStrip(this);
            ConfigureSalesInputs();
            txtBarcode.TextChanged += (sender, args) =>
            {
                if (!medicineBarcodeLoading) return;
                CancelMedicineBarcodeRead();
                UiTheme.SetStatus(lblStockStatus, Present("تم تغيير الباركود. اضغط Enter للبحث."));
            };
            InitializeStableInvoiceNotesEditor();
            CommerceWorkspaceTheme.ApplyBilling(this);
            lblProductSection.Cursor = Cursors.Hand;
            lblProductSection.Click += (sender, args) => OpenMedicineQuickSearch();
            ArrangePointOfSaleRightSide();
            ResetQuantityToDefault();
            txtQuantity.KeyDown += txtQuantity_KeyDown;
            AddInvoiceHistoryPreviewButton();
            AddLegacyInvoiceHistoryActions();
            AddCommercialReturnHistoryAction();
            AddSavePdfButton();
            AddCancelSavedInvoiceButton();
            ApplyPresentation();
            // Show localized totals from cached currency before asynchronous startup can yield.
            PriceBDLabel.Text = Present("الإجمالي") + ": " + FormatMoney(0);
            PriceADLabel.Text = Present("الصافي") + ": " + FormatMoney(0);
            TimingLabel.Text = Present("التوقيت:");
            ApplyCustomerReadActions();
            if (previewMode) PopulatePreviewData();
            CommerceWorkspaceTheme.RefreshLayout(this);
            DesktopComponents.AddComboBoxDropdown(MediComboCb, "billingMedicineDropdown", Present("اختيار الصنف"), 2);
            ConfigureWorkflowEnhancements();
            medicineUnitPicker=new MedicineUnitPicker("billingMedicineUnit",presentationLeftToRight);
            txtQuantity.Parent.Controls.Add(medicineUnitPicker);
            BillingGV.RowsRemoved += (sender,args)=> { foreach(var old in cartUnits.Keys.Where(r=>r.DataGridView!=BillingGV).ToArray())cartUnits.Remove(old); };
            Disposed += (sender,args)=>cartUnits.Clear();
            medicineUnitPicker.SelectedIndexChanged += (sender,args)=>txtQuantity_TextChanged(sender,args);
            MedicineUnitPicker.EnsureColumn(BillingGV,presentationLeftToRight);
            ConfigureBillingAction(btnRefreshMedicineNames, "تحديث", "تحديث قائمة الأدوية دون مسح الفاتورة", AppIcon.Refresh);
            btnRefreshMedicineNames.AccessibleName = Present("تحديث الأصناف");
            flowFooterButtons.Controls.Add(btnRefreshMedicineNames);
            flowFooterButtons.Controls.SetChildIndex(btnRefreshMedicineNames, flowFooterButtons.Controls.IndexOf(btnBack));
            for (int i = 0; i < flowFooterButtons.Controls.Count; i++) flowFooterButtons.Controls[i].TabIndex = i;
            btnRefreshMedicineNames.Click += async (sender, args) =>
            {
                if (medicineCartPending || _isSavingInvoice || previewMode) return;
                await LoadCustomerOptionsAsync();
                await LoadMedicineNamesAsync();
                if (currencyDisplayOverride == null) await LoadCurrencySnapshotAsync();
            };
            btnRefreshMedicineNames.Enabled = !previewMode && SessionContext.IsAuthenticated && PermissionService.CanOpen(AppPermission.Sales);
            CommerceWorkspaceTheme.RefreshLayout(this);
            InitializeSalesRecoveryControls();
            FormClosed += (sender, args) => CancelMedicineReads();
            Disposed += (sender, args) => CancelMedicineReads();
            UiSessionSubscription.Attach(this, () =>
            {
                if (previewMode) return;
                CancelMedicineReads();
                ++medicineNamesVersion;
                ++medicineSelectionVersion;
                medicineNamesLoading = false;
                medicineSelectionLoading = false;
                selectedStockPreview = null;
                MediComboCb.Items.Clear();
                MediComboCb.SelectedIndex = -1;
                MediComboCb.Text = string.Empty;
                ApplyMedicineReadActions();
                UiTheme.SetStatus(lblStockStatus, Present("تغيرت جلسة المستخدم. أعد تحميل القائمة."), false);
            });
        }

        // The caption of a value label is translated here because the language pass must not
        // touch a label that may carry a business value.
        private void ApplySalesValueCaptions()
        {
            if (CustomerNameLabel == null) return;
            CustomerNameLabel.Text = Present("اسم العميل:");
            CustomerPhoneLabel.Text = Present("هاتف العميل:");
            CustomerAddressLabel.Text = Present("عنوان العميل:");
            EmployeeLabel.Text = Present("اسم الكاشير:");
            EmployeeIdLabel.Text = Present("رقم الكاشير:");
            CustomerIdLabel.Text = Present("رقم العميل:");
            TimingLabel.Text = Present("التوقيت:");
        }

        private string Present(string arabicText)
        {
            return presentationLeftToRight ? UiLanguageCatalog.Translate(arabicText) : arabicText;
        }

        private string PresentFormat(string template, params object[] values)
        {
            return UiLanguageCatalog.Format(presentationLeftToRight, template, values);
        }

        private string FormatSalesTiming(DateTime value)
        {
            var culture = System.Globalization.CultureInfo.GetCultureInfo(presentationLeftToRight ? "en-US" : "ar-EG");
            return PresentFormat("التاريخ: {0}", value.ToString("dddd, dd MMMM yyyy hh:mm tt", culture));
        }
        private void ApplyPresentation()
        {
            Text = Present("نقطة البيع");
            lblBillingTitle.Text = Present("نقطة البيع");
            lblBillingSubtitle.Text = Present("مسح سريع للباركود، فاتورة واضحة، واعتماد آمن للطباعة");
            lblTabCart.Text = Present("الفاتورة الحالية");
            lblTabHistory.Text = Present("سجل العملاء");
            lblHistoryTitle.Text = Present("سجل فواتير البيع");
            lblHistorySubtitle.Text = Present("انقر مرتين على أي فاتورة لعرض تفاصيلها");
            lblCartTitle.Text = Present("بنود الفاتورة");
            lblBarcode.Text = Present("الباركود");
            lblProductSection.Text = Present("بحث الأصناف (F3)").Replace("(F3)", "(" + salesShortcutProfile[SalesShortcutAction.Search] + ")");
            lblQuantity.Text = Present("الكمية (F4)").Replace("(F4)", "(" + salesShortcutProfile[SalesShortcutAction.Quantity] + ")");
            lblDiscountPercent.Text = Present("نسبة الخصم %");
            lblCashier.Text = Present("الكاشير");
            lblStockStatus.Text = Present("المخزون المتاح — اختر صنفاً أولاً");
            ConfigureBillingAction(btnAddToCart, "إضافة للسلة", "إضافة الصنف إلى الفاتورة", AppIcon.Add);
            ConfigureBillingAction(btnApplyDiscount, "تطبيق", "تطبيق الخصم", AppIcon.Confirm);
            ConfigureBillingAction(btnSaveOnly, presentationLeftToRight ? "Save" : "حفظ فقط", "حفظ الفاتورة دون طباعة", AppIcon.Save, translateText: false);
            ConfigureBillingAction(btnPreviewPrint, presentationLeftToRight ? "Sell and print" : "بيع وطباعة", "إتمام البيع وطباعة الفاتورة", AppIcon.Print, translateText: false);
            ConfigureBillingAction(btnPrintA4, "طباعة A4", "طباعة الفاتورة A4", AppIcon.Print);
            ConfigureBillingAction(btnClearInvoice, "مسح الفاتورة", "مسح الفاتورة الحالية", AppIcon.Refresh);
            ConfigureBillingAction(btnBack, "رجوع", "إغلاق نقطة البيع", AppIcon.Back);
            ConfigureBillingAction(btnBackToCart, "العودة", "العودة إلى الفاتورة", AppIcon.Back);
            if (btnBillingCustomerType != null)
            {
                btnBillingCustomerType.Text = Present("نوع العميل: عميل عام");
                btnBillingCustomerType.AccessibleName = Present("نوع العميل: عميل عام");
                btnBillingCustomerType.AccessibleDescription = Present("يعرض نوع العميل فقط. اضغط لاختيار سجل محفوظ من شاشة العملاء والموردين.");
                btnBillingCustomerType.IconSvg = OutlinedIcons.Svg(AppIcon.User, !presentationLeftToRight);
            }
            SetPresentationText("lblCheckoutTitle", "ملخص الفاتورة");
            SetPresentationText("lblCheckoutPayment", "الدفع عند اعتماد الفاتورة");
            SetPresentationText("lblInvoiceNotes", "ملاحظة داخلية");
            SetPresentationText("btnSavePdf", presentationLeftToRight ? "Save PDF" : "حفظ PDF", false);
            ConfigureBillingAction(flowFooterButtons.Controls["btnSavePdf"] as AntdUI.Button, "حفظ PDF", "حفظ PDF", AppIcon.Save);
            ConfigureBillingAction(flowFooterButtons.Controls["btnCancelSavedInvoice"] as AntdUI.Button, "إلغاء", "إلغاء الفاتورة", AppIcon.Close);
            if (presentationLeftToRight) btnClearInvoice.Text = "Clear";
            SetPresentationText("btnCancelSavedInvoice", presentationLeftToRight ? "Cancel" : "إلغاء", false);
            UiDirection.Apply(this, presentationLeftToRight);
            RightToLeftLayout = !presentationLeftToRight;
            foreach (Label label in new[] { lblBillingTitle, lblBillingSubtitle, lblHistoryTitle, lblHistorySubtitle,
                lblCartTitle, lblBarcode, lblProductSection, lblQuantity, lblDiscountPercent, lblCashier,
                lblStockStatus, PriceBDLabel, PriceADLabel, TimingLabel, CustomerNameLabel, CustomerPhoneLabel,
                CustomerAddressLabel, EmployeeLabel, EmployeeIdLabel, CustomerIdLabel })
                label.TextAlign = presentationLeftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
            MediComboCb.RightToLeft = presentationLeftToRight ? RightToLeft.No : RightToLeft.Yes;

            SelectEmployee.RightToLeft = presentationLeftToRight ? RightToLeft.No : RightToLeft.Yes;
            SelectEmployee.TextAlign = presentationLeftToRight ? HorizontalAlignment.Left : HorizontalAlignment.Right;
            txtInvoiceNotes.RightToLeft = presentationLeftToRight ? RightToLeft.No : RightToLeft.Yes;
            txtInvoiceNotes.TextAlign = presentationLeftToRight ? HorizontalAlignment.Left : HorizontalAlignment.Right;
            if (stableInvoiceNotesEditor != null)
            {
                stableInvoiceNotesEditor.RightToLeft = presentationLeftToRight ? RightToLeft.No : RightToLeft.Yes;
                stableInvoiceNotesEditor.TextAlign = presentationLeftToRight ? HorizontalAlignment.Left : HorizontalAlignment.Right;
            }
            MediComboCb.TextChanged += BillingContent_TextChanged;
            SelectEmployee.TextChanged += BillingContent_TextChanged;
            RefreshContentDirections();
            ApplyBillingGridPresentation();
            UiTheme.ApplyStatusStripPresentation(this, presentationLeftToRight);
        }

        private void ConfigureBillingAction(AntdUI.Button button, string text, string accessibleName, AppIcon icon, bool translateText = true)
        {
            if (button == null) return;
            button.Text = translateText ? Present(text) : text;
            button.AccessibleName = Present(accessibleName);
            button.IconSvg = OutlinedIcons.Svg(icon, !presentationLeftToRight);
            button.IconSize = new Size(18, 18);
            button.IconPosition = presentationLeftToRight ? AntdUI.TAlignMini.Left : AntdUI.TAlignMini.Right;
        }

        private void InitializeStableInvoiceNotesEditor()
        {
            txtInvoiceNotes.Name = "txtInvoiceNotesAntdBacking";
            txtInvoiceNotes.Visible = false;
            stableInvoiceNotesEditor = new TextBox
            {
                Name = "txtInvoiceNotes",
                Multiline = true,
                WordWrap = true,
                AcceptsReturn = true,
                AccessibleName = Present("ملاحظة داخلية للفاتورة"),
                AccessibleRole = AccessibleRole.Text,
                Font = AppFonts.Create(9.5F),
                TabIndex = 6,
                BackColor = UiTheme.Surface,
                ForeColor = UiTheme.Text,
                BorderStyle = BorderStyle.FixedSingle
            };
            stableInvoiceNotesEditor.TextChanged += (sender, args) =>
            {
                if (!string.Equals(txtInvoiceNotes.Text, stableInvoiceNotesEditor.Text, StringComparison.Ordinal))
                    txtInvoiceNotes.Text = stableInvoiceNotesEditor.Text;
            };
            txtInvoiceNotes.TextChanged += (sender, args) =>
            {
                if (!string.Equals(stableInvoiceNotesEditor.Text, txtInvoiceNotes.Text, StringComparison.Ordinal))
                    stableInvoiceNotesEditor.Text = txtInvoiceNotes.Text;
            };
            pnlBillingSidebar.Controls.Add(stableInvoiceNotesEditor);
            UiTheme.ApplyTo(stableInvoiceNotesEditor);
        }

        private void BillingContent_TextChanged(object sender, EventArgs e)
        {
            ApplyContentDirection(sender as Control);
        }

        internal void RefreshContentDirections()
        {
            // Numeric entry must retain LTR glyph order even in an Arabic workspace.
            txtDiscountPercent.RightToLeft = RightToLeft.No;
            txtDiscountPercent.TextAlign = HorizontalAlignment.Left;
            ApplyContentDirection(MediComboCb);
            ApplyContentDirection(SelectEmployee);
            chkUseCustomerCredit.ForeColor = UiTheme.Text;
            chkUseCustomerCredit.BackColor = UiTheme.Surface;
            ApplyBillingGridSizing();
        }

        private void ApplyContentDirection(Control control)
        {
            if (control == null) return;
            char first = (control.Text ?? string.Empty).FirstOrDefault(character => !char.IsWhiteSpace(character));
            bool arabic = first >= '\u0600' && first <= '\u06FF';
            bool rtl = arabic || (first == '\0' && !presentationLeftToRight);
            control.RightToLeft = rtl ? RightToLeft.Yes : RightToLeft.No;
            AntdUI.Select select = control as AntdUI.Select;
            if (select != null) select.TextAlign = rtl ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        }

        private void SetPresentationText(string controlName, string text, bool translateText = true)
        {
            Control control = Controls.Find(controlName, true).FirstOrDefault();
            if (control == null) return;
            control.Text = translateText ? Present(text) : text;
            control.AccessibleName = control.Text;
        }

  private void ApplyBillingGridPresentation()
  {
  foreach (DataGridView grid in new[] { BillingGV, InvoiceHistoryGV })
  {
  if (grid == null) continue;
  grid.RightToLeft = presentationLeftToRight ? RightToLeft.No : RightToLeft.Yes;
  grid.ColumnHeadersDefaultCellStyle.Alignment = presentationLeftToRight ? DataGridViewContentAlignment.MiddleLeft : DataGridViewContentAlignment.MiddleRight;
  grid.DefaultCellStyle.Alignment = presentationLeftToRight ? DataGridViewContentAlignment.MiddleLeft : DataGridViewContentAlignment.MiddleRight;
  }
  ApplyBillingMoneyColumnAlignment();
            SetBillingColumn("BillId", "رمز", "ID");
            SetBillingColumn("MedName", "الصنف", "Medicine");
            SetBillingColumn("Sprice", "سعر الوحدة", "Unit price");
            SetBillingColumn("Quantity", "الكمية", "Quantity");
            SetBillingColumn("TotalAmount", "الإجمالي", "Total");
            SetBillingColumn("RemoveCartItem", "إزالة", "Remove");
            ApplyBillingGridSizing();
        }

  // The money columns are aligned to the end of the reading direction and keep two decimals,
  // so the decimal point does not move between rows.
  private void ApplyBillingMoneyColumnAlignment()
  {
  if (BillingGV.Columns.Count == 0) return;
  DataGridViewContentAlignment money = presentationLeftToRight ? DataGridViewContentAlignment.MiddleRight : DataGridViewContentAlignment.MiddleLeft;
  foreach (string moneyColumn in new[] { "Sprice", "TotalAmount" })
  {
  if (BillingGV.Columns[moneyColumn] == null) continue;
  BillingGV.Columns[moneyColumn].DefaultCellStyle.Format = "N2";
  BillingGV.Columns[moneyColumn].DefaultCellStyle.Alignment = money;
  }
  }

  private void ApplyBillingGridSizing()
  {
            BillingGV.Tag = UiTheme.PreserveCustomGridColumns;
            BillingGV.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            BillingGV.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;
            BillingGV.ColumnHeadersHeight = Math.Max(48, BillingGV.Font.Height * 2 + 12);
            foreach (DataGridViewColumn column in BillingGV.Columns)
            {
                column.MinimumWidth = Math.Min(64, Math.Max(32, BillingGV.ClientSize.Width / 12));
                column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                column.FillWeight = column.Name == "MedName" ? 160 : column.Name == "TotalAmount" ? 95 : 75;
                column.HeaderCell.Style.SelectionBackColor = UiTheme.Primary;
                column.HeaderCell.Style.SelectionForeColor = Color.White;
            }
        }
        private void SetBillingColumn(string name, string arabic, string english)
        {
            if (BillingGV.Columns.Contains(name)) BillingGV.Columns[name].HeaderText = presentationLeftToRight ? english : arabic;
        }

        private void PopulatePreviewData()
        {
            // A preview must not imply that the configured database is connected.
            UiTheme.UseDedicatedStatus(this);
            InitializeBillingGrid();
            string medicine = "باراسيتامول 500 mg";
            BillingGV.Rows.Add("DRAFT-001", medicine, 1200, 2, 2400, Present("إزالة"));
            MediComboCb.Items.Add(medicine);
            MediComboCb.SelectedIndex = 0;
            SelectEmployee.Text = presentationLeftToRight ? "System manager" : "مدير النظام";
            txtQuantity.Text = "2";
            txtDiscountPercent.Text = "0";
            txtInvoiceNotes.Text = string.Empty;
            PriceBDLabel.Text = Present("الإجمالي") + ": " + FormatMoney(2400);
            PriceADLabel.Text = Present("الصافي") + ": " + FormatMoney(2400);
            TimingLabel.Text = Present("التوقيت:") + " " + DateTime.Now.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture);
            EmployeeLabel.Text = Present("الكاشير") + ": " + SelectEmployee.Text;
            lblStockStatus.Text = string.Empty;
            ApplyBillingGridPresentation();
            _isInvoiceSaved = true;
        }

        private void ConfigureSalesInputs()
        {
            ConfigureSalesInput(txtBarcode, "الباركود", "امسح الباركود أو أدخله يدويًا ثم اضغط Enter.");
            ConfigureSalesInput(txtQuantity, "الكمية", "أدخل كمية صحيحة ثم اضغط Enter لإضافة الصنف.");
            ConfigureSalesInput(txtDiscountPercent, "نسبة الخصم", "أدخل نسبة بين صفر وأقل من مئة ثم اضغط تطبيق.");
            txtInvoiceNotes.AccessibleName = Present("ملاحظة داخلية للفاتورة");
            txtInvoiceNotes.AccessibleRole = AccessibleRole.Text;
            txtInvoiceNotes.Multiline = true;
            txtInvoiceNotes.WordWrap = true;
            txtInvoiceNotes.RightToLeft = UiDirection.IsLeftToRight ? RightToLeft.No : RightToLeft.Yes;
            txtInvoiceNotes.TextAlign = UiDirection.IsLeftToRight ? HorizontalAlignment.Left : HorizontalAlignment.Right;
            txtInvoiceNotes.Font = AppFonts.Create(9.5F);
            AntdThemeAdapter.Apply(txtInvoiceNotes);
            MediComboCb.AccessibleName = Present("اختيار الصنف");
            SelectEmployee.AccessibleName = Present("الكاشير");
            txtBarcode.TabIndex = 0;
            MediComboCb.TabIndex = 1;
            txtQuantity.TabIndex = 2;
            btnAddToCart.TabIndex = 3;
            txtDiscountPercent.TabIndex = 4;
            btnApplyDiscount.TabIndex = 5;
            txtInvoiceNotes.TabIndex = 6;
        }

        private void ConfigureSalesInput(AntdUI.Input input, string label, string description)
        {
            input.AccessibleName = Present(label);
            input.AccessibleRole = AccessibleRole.Text;
            input.AccessibleDescription = Present(description);
            input.RightToLeft = RightToLeft.No;
            input.TextAlign = HorizontalAlignment.Left;
            input.Font = AppFonts.Create(10F);
            AntdThemeAdapter.Apply(input);
        }

        private void ConfigureCustomerMasterSelection()
        {
            cmbCustomerName.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbCustomerPhone.DropDownStyle = ComboBoxStyle.DropDownList;
            txtCustomerAddress.ReadOnly = true;
            cmbCustomerName.AccessibleDescription = "اختيار عميل مسجل؛ تعديل البيانات من شاشة العملاء والموردين";
            cmbCustomerPhone.AccessibleDescription = "اختيار رقم هاتف عميل مسجل";
            txtCustomerAddress.AccessibleDescription = "العنوان المحفوظ للعميل؛ يعدل من شاشة العملاء والموردين";
        }
        private async void OpenCustomerManagement()
        {
            if (previewMode) return;
            if (_isInvoiceSaved || _isInvoiceCancelled || _isSavingInvoice) return;
            if (!PermissionService.CanOpen(CustomerSupplierManagementForm.RequiredPermission(true)))
            {
                AppNotifications.Show(UiLanguageCatalog.GetDisplayText("لا تملك صلاحية اختيار عميل للمبيعات."),
                    UiLanguageCatalog.GetDisplayText("الصلاحيات"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            try
            {
                var capturedCustomerContext=CaptureCustomerReadSession();
                using (var dialog = new CustomerSupplierManagementForm(selectionMode: true))
                {
                    if (dialog.ShowDialog(this) != DialogResult.OK || dialog.SelectedCustomer == null) return;
                    if(!CustomerContextCurrent(capturedCustomerContext))return;
                    await LoadCustomerOptionsAsync();
                    if(!CustomerContextCurrent(capturedCustomerContext))return;
                    await SelectCustomerAsync(CustomerReferenceKind.ById,dialog.SelectedCustomer.CustomerId);
                }
            }
            catch (Exception exception) { UiTheme.ShowOperationIssue(this, "اختيار بيانات العميل", exception); }
        }
        private void InitializeCustomerTypeIndicator()
        {
            btnBillingCustomerType = DesktopComponents.Action("btnBillingCustomerType", "نوع العميل: عميل عام", AppIcon.User, false);
            btnBillingCustomerType.AccessibleName = Present("نوع العميل: عميل عام");
            btnBillingCustomerType.AccessibleDescription = Present("يعرض نوع العميل فقط. اضغط لاختيار سجل محفوظ من شاشة العملاء والموردين.");
            btnBillingCustomerType.Click += (sender, args) => OpenCustomerManagement();
            pnlBillingSidebar.Controls.Add(btnBillingCustomerType);
        }

        private void UpdateCustomerTypeIndicator()
        {
            if (btnBillingCustomerType == null) return;

            string phone = cmbCustomerPhone == null ? string.Empty : (cmbCustomerPhone.Text ?? string.Empty).Trim();
            string customerType = !string.IsNullOrWhiteSpace(_currentCustomerId)
                ? "عميل مسجل"
                : (!string.IsNullOrWhiteSpace(phone) && phone != "000000000" ? "عميل غير مسجل" : "عميل عام");
            btnBillingCustomerType.Text = PresentFormat("نوع العميل: {0}", Present(customerType));
            btnBillingCustomerType.AccessibleName = PresentFormat("نوع العميل: {0}", Present(customerType));
            btnBillingCustomerType.AccessibleDescription = PresentFormat("يعرض نوع العميل فقط. اضغط لاختيار سجل محفوظ من شاشة العملاء والموردين. النوع الحالي: {0}", Present(customerType));
        }
        private void InitializeCustomerCreditControls()
        {
            lblCustomerCreditBalance = new Label
            {
                Name = "lblCustomerCreditBalance", Location = new Point(25, 624), Size = new Size(350, 28),
                TextAlign = ContentAlignment.MiddleRight, RightToLeft = RightToLeft.Yes,
                Text = Present("رصيد العميل المتاح: اختر عميلاً مسجلاً")
            };
            chkUseCustomerCredit = new AntdUI.Checkbox
            {
                Name = "chkUseCustomerCredit", Location = new Point(25, 654), Size = new Size(350, 36),
                RightToLeft = RightToLeft.Yes,
                Text = Present("استخدام رصيد العميل في هذه الفاتورة"), Enabled = false
            };
            pnlBillingSidebar.Controls.Add(lblCustomerCreditBalance);
            pnlBillingSidebar.Controls.Add(chkUseCustomerCredit);
            lblCustomerCreditAvailability = new Label
            {
                Name = "lblCustomerCreditAvailability", AutoSize = false, AutoEllipsis = true,
                Font = AppFonts.Create(9F), TextAlign = ContentAlignment.MiddleRight,
                RightToLeft = RightToLeft.Yes
            };
            pnlBillingSidebar.Controls.Add(lblCustomerCreditAvailability);
            UpdateCustomerCreditAvailabilityPresentation();
        }

        private void UpdateCustomerCreditAvailabilityPresentation()
        {
            if (lblCustomerCreditAvailability == null || chkUseCustomerCredit == null || IsDisposed || Disposing) return;
            string reason;
            if (_isSavingInvoice) reason = "جارٍ حفظ الفاتورة.";
            else if (SalesRecoveryLocked) reason = "تحقق من نتيجة الحفظ أولًا.";
            else if (_isInvoiceSaved || _isInvoiceCancelled) reason = "ابدأ فاتورة جديدة لاستخدام الرصيد.";
            else if (customerCreditLoading) reason = "جارٍ تحميل رصيد العميل…";
            else if (customerOptionsLoading || customerSelectionLoading) reason = "جارٍ تحميل بيانات العميل…";
            else if (string.IsNullOrWhiteSpace(_currentCustomerId)) reason = "اختر عميلًا مسجلًا لاستخدام رصيده.";
            else if (!customerSelectionReady || !string.Equals(_creditCustomerId, _currentCustomerId.Trim(), StringComparison.OrdinalIgnoreCase)) reason = "أعد اختيار العميل للتحقق من الرصيد.";
            else if (_displayedCustomerCreditBalance <= 0m) reason = "لا يوجد رصيد متاح للاستخدام.";
            else reason = "يمكن استخدام الرصيد في هذه الفاتورة.";
            string text = Present(reason);
            if (lblCustomerCreditAvailability.Text != text) lblCustomerCreditAvailability.Text = text;
            lblCustomerCreditAvailability.AccessibleName = Present("حالة استخدام رصيد العميل");
            lblCustomerCreditAvailability.AccessibleDescription = text;
            chkUseCustomerCredit.AccessibleDescription = text;
            lblCustomerCreditAvailability.ForeColor = UiTheme.Text;
            lblCustomerCreditAvailability.BackColor = UiTheme.Surface;
            lblCustomerCreditAvailability.RightToLeft = presentationLeftToRight ? RightToLeft.No : RightToLeft.Yes;
            lblCustomerCreditAvailability.TextAlign = presentationLeftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
        }

        private void ResetCustomerCreditDisplay()
        {
            _creditCustomerId = string.Empty;
            _displayedCustomerCreditBalance = 0m;
            if (chkUseCustomerCredit != null) { chkUseCustomerCredit.Checked = false; chkUseCustomerCredit.Enabled = false; }
            if (lblCustomerCreditBalance != null) lblCustomerCreditBalance.Text = Present("رصيد العميل المتاح: اختر عميلاً مسجلاً");
            UpdateCustomerCreditAvailabilityPresentation();
        }



        private void ApplyCommittedCustomerCredit(decimal amount)
        {
            _displayedCustomerCreditBalance = Math.Max(0m, InvoiceTotalsCalculator.Round(_displayedCustomerCreditBalance - amount));
            lblCustomerCreditBalance.Text = PresentFormat("رصيد العميل المتاح: {0}", FormatMoney(_displayedCustomerCreditBalance));
            chkUseCustomerCredit.Checked = false;
            chkUseCustomerCredit.Enabled = _displayedCustomerCreditBalance > 0m;
            UpdateCustomerCreditAvailabilityPresentation();
        }

        // اختصارات الكاشير لا تمرر إلى الحقول حتى يبقى المسح والاعتماد سريعَين.
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            var shortcut = salesShortcutProfile.Resolve(keyData);
            if (shortcut == SalesShortcutAction.Search)
            {
                OpenMedicineQuickSearch();
                return true;
            }
            if (shortcut == SalesShortcutAction.Barcode)
            {
                MediComboCb.Focus();
                MediComboCb.SelectAll();
                return true;
            }

            if (shortcut == SalesShortcutAction.Quantity)
            {
                if (!_isInvoiceSaved && !_isInvoiceCancelled && !_isSavingInvoice)
                {
                    txtQuantity.Focus();
                    txtQuantity.SelectAll();
                }
                return true;
            }
            if (shortcut == SalesShortcutAction.Preview)
            {
                if (btnPreviewPrint.Enabled)
                {
                    btnPreviewPrint.PerformClick();
                }

                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private async void OpenMedicineQuickSearch()
        {
            if (previewMode) return;
            if (_isInvoiceSaved || _isInvoiceCancelled || _isSavingInvoice) return;
            try
            {
                using (var dialog = new MedicineQuickSearchForm(MediComboCb.Text))
                {
                    if (dialog.ShowDialog(this) != DialogResult.OK) return;
                    SelectMedicineByName(dialog.SelectedMedicine.MedicineName);
                    await AddSelectedMedicineFromKeyboardAsync();
                }
            }
            catch (Exception exception) { UiTheme.ShowOperationIssue(this, "البحث السريع", exception); }
        }
        private void ArrangePointOfSaleRightSide()
        {
            bool leftToRight = UiDirection.IsLeftToRight;
            RightToLeft = leftToRight ? RightToLeft.No : RightToLeft.Yes;
            RightToLeftLayout = !leftToRight;

            if (pnlBillingSidebar != null)
            {
                pnlBillingSidebar.Dock = leftToRight ? DockStyle.Left : DockStyle.Right;
                pnlBillingSidebar.BringToFront();
            }

            if (pnlCartHost != null)
            {
                pnlCartHost.Dock = DockStyle.Fill;
            }

            if (pnlInvoiceHistory != null)
            {
                pnlInvoiceHistory.Dock = DockStyle.Fill;
            }

            UiDirection.Apply(this);
        }

        private void AlignBillingInputsRight(Control parent)
        {
            if (parent == null)
            {
                return;
            }

            foreach (Control control in parent.Controls)
            {
                control.RightToLeft = RightToLeft.Yes;

                Label label = control as Label;
                if (label != null)
                {
                    label.TextAlign = ContentAlignment.MiddleRight;
                    continue;
                }

                TextBox textBox = control as TextBox;
                if (textBox != null)
                {
                    textBox.TextAlign = HorizontalAlignment.Right;
                    continue;
                }

                ComboBox comboBox = control as ComboBox;
                if (comboBox != null)
                {
                    comboBox.RightToLeft = RightToLeft.Yes;
                    continue;
                }

                Button button = control as Button;
                if (button != null)
                {
                    button.TextAlign = ContentAlignment.MiddleCenter;
                }
            }
        }

        private void ResetQuantityToDefault()
        {
            if (txtQuantity == null)
            {
                return;
            }

            txtQuantity.TextChanged -= txtQuantity_TextChanged;
            txtQuantity.Text = "1";
            txtQuantity.TextChanged += txtQuantity_TextChanged;
        }

        // يغلق شاشة الدفع والعودة للرئيسية دون إنهاء التطبيق بالكامل.
        private void btnCloseBilling_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void SwitchToCartView()
        {
            pnlCartHost.Visible = true;
            pnlInvoiceHistory.Visible = false;
            // ملخص الاعتماد أصبح لوحة مستقلة في يسار الشاشة، لذلك لا يعتمد
            // على إخفاء حاوية السلة كما كان في التخطيط القديم.
            pnlTotals.Visible = true;
            lblTabCart.Font = AppFonts.Create(10.2F, System.Drawing.FontStyle.Bold);
            lblTabCart.ForeColor = System.Drawing.Color.White;
            lblTabHistory.Font = AppFonts.Create(10.2F, System.Drawing.FontStyle.Regular);
            lblTabHistory.ForeColor = System.Drawing.Color.FromArgb(200, 230, 220);
        }

        private void SwitchToHistoryView()
        {
            pnlCartHost.Visible = false;
            pnlInvoiceHistory.Visible = true;
            pnlTotals.Visible = false;
            lblTabHistory.Font = AppFonts.Create(10.2F, System.Drawing.FontStyle.Bold);
            lblTabHistory.ForeColor = System.Drawing.Color.White;
            lblTabCart.Font = AppFonts.Create(10.2F, System.Drawing.FontStyle.Regular);
            lblTabCart.ForeColor = System.Drawing.Color.FromArgb(200, 230, 220);
            LoadInvoiceHistory();
        }

        private void lblTabCart_Click(object sender, EventArgs e)
        {
            SwitchToCartView();
        }

        private void lblTabHistory_Click(object sender, EventArgs e)
        {
            SwitchToHistoryView();
        }

        private void btnBackToCart_Click(object sender, EventArgs e)
        {
            SwitchToCartView();
        }

        private void LoadInvoiceHistory()
        {
            try { ReloadInvoiceHistory(); }
            catch (Exception ex)
            {
                InvalidateInvoiceHistory();
                UiTheme.ShowOperationFailure(this, "تحميل سجل الفواتير", ex);
            }
        }

        private TextBox historySearch;
        // The history list holds thousands of invoices; a live filter over the bound table keeps
        // searching instant without re-querying SQL on every keystroke.
        private void EnsureHistorySearch()
        {
            if (historySearch != null) return;
            historySearch = new TextBox
            {
                Name = "txtHistorySearch",
                Dock = DockStyle.Top,
                Font = AppFonts.Create(9.5F),
                RightToLeft = RightToLeft.Yes,
                AccessibleName = Present("بحث في سجل الفواتير")
            };
            historySearch.TextChanged += (sender, args) => ApplyHistoryFilter();
            pnlInvoiceHistory.Controls.Add(historySearch);
            historySearch.BringToFront();
        }

        private void ApplyHistoryFilter()
        {
            DataTable all = invoiceHistoryAllRows;
            if (all == null)
            {
                all = InvoiceHistoryGV.DataSource as DataTable;
                if (all == null) return;
                invoiceHistoryAllRows = all;
            }

            string query = (historySearch == null ? string.Empty : historySearch.Text).Trim();
            string filter = string.Empty;
            if (query.Length > 0)
            {
                string escaped = query.Replace("'", "''").Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]");
                var filters = new System.Collections.Generic.List<string>();
                foreach (DataColumn column in all.Columns)
                {
                    if (column.DataType != typeof(string)) continue;
                    filters.Add("[" + column.ColumnName.Replace("]", "]]") + "] LIKE '%" + escaped + "%'");
                }
                filter = filters.Count == 0 ? string.Empty : string.Join(" OR ", filters);
            }

            DataView view = all.DefaultView;
            if (view.RowFilter != filter) view.RowFilter = filter;

            DataTable pageTable = all.Clone();
            int take = Math.Min(invoiceHistoryPageSize, view.Count);
            for (int i = 0; i < take; i++)
            {
                pageTable.ImportRow(view[i].Row);
            }
            InvoiceHistoryGV.DataSource = pageTable;
            if (InvoiceHistoryGV.Columns["العنوان"] != null) InvoiceHistoryGV.Columns["العنوان"].Visible = false;
            if (InvoiceHistoryGV.Columns["ملاحظات"] != null) InvoiceHistoryGV.Columns["ملاحظات"].Visible = false;
            if (InvoiceHistoryGV.Columns["إجمالي إنفاق العميل"] != null) InvoiceHistoryGV.Columns["إجمالي إنفاق العميل"].DefaultCellStyle.Format = "N2";
            if (InvoiceHistoryGV.Columns["إجمالي الفاتورة"] != null) InvoiceHistoryGV.Columns["إجمالي الفاتورة"].DefaultCellStyle.Format = "N2";
            UpdateHistoryShowMore(view.Count > take);
        }

        // Loads the first 500 rows and offers "show more": binding thousands of invoices at once
        // makes the whole sales screen crawl on every open.
        private void UpdateHistoryShowMore(bool hasMore)
        {
            EnsureHistoryBar();
            if (invoiceHistoryShowMore != null)
            {
                invoiceHistoryShowMore.Visible = hasMore;
            }
        }

        private void EnsureHistoryBar()
        {
            if (invoiceHistoryBar != null) return;

            invoiceHistoryBar = new FlowLayoutPanel
            {
                Name = "historyActionsBar",
                Dock = DockStyle.Bottom,
                Height = 38,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false
            };

            invoiceHistoryExport = new Button
            {
                Name = "btnHistoryExport",
                Width = 120,
                Height = 32,
                Text = Present("تصدير Excel"),
                AccessibleName = Present("تصدير السجل إلى ملف CSV"),
                Cursor = Cursors.Hand
            };
            invoiceHistoryExport.Click += (sender, args) => ExportHistoryCsv();

            invoiceHistoryShowMore = new Button
            {
                Name = "btnHistoryShowMore",
                Width = 120,
                Height = 32,
                Text = Present("عرض المزيد"),
                AccessibleName = Present("عرض المزيد من السجل"),
                Cursor = Cursors.Hand
            };
            invoiceHistoryShowMore.Click += (sender, args) =>
            {
                invoiceHistoryPageSize += 500;
                ApplyHistoryFilter();
            };

            invoiceHistoryBar.Controls.Add(invoiceHistoryExport);
            invoiceHistoryBar.Controls.Add(invoiceHistoryShowMore);
            UiTheme.ApplyToolbarButtons(invoiceHistoryExport, invoiceHistoryShowMore);
            pnlInvoiceHistory.Controls.Add(invoiceHistoryBar);
            invoiceHistoryBar.BringToFront();
        }

        private void ExportHistoryCsv()
        {
            DataTable all = invoiceHistoryAllRows;
            if (all == null) return;

            DataView view = all.DefaultView;
            if (view.Count == 0)
            {
                UiTheme.SetFormStatus(this, Present("لا توجد بيانات لتصديرها."), false);
                return;
            }

            using (var dialog = new SaveFileDialog())
            {
                dialog.Filter = "Excel Workbook (*.xlsx)|*.xlsx|CSV (*.csv)|*.csv";
                dialog.DefaultExt = "xlsx";
                dialog.AddExtension = true;
                dialog.FileName = "sales-history-" + DateTime.Now.ToString("yyyyMMdd-HHmm") + ".xlsx";
                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                DataTable filtered = view.ToTable();
                bool wantsCsv = string.Equals(System.IO.Path.GetExtension(dialog.FileName), ".csv", StringComparison.OrdinalIgnoreCase);
                if (wantsCsv)
                {
                    string content = ReportCsvWriter.Serialize(filtered);
                    Exception error;
                    if (!ReportCsvWriter.TryWriteText(dialog.FileName, content, out error))
                    {
                        AppDiagnostics.Log("BillingHistoryExport", error);
                        UiTheme.SetFormStatus(this, Present("تعذر حفظ ملف التصدير. تحقق من صلاحية المجلد."), false);
                        return;
                    }
                }
                else
                {
                    try
                    {
                        ExcelWorkbookWriter.WriteCompleteFile(filtered, "SalesHistory", dialog.FileName);
                    }
                    catch (Exception error)
                    {
                        AppDiagnostics.Log("BillingHistoryExport", error);
                        UiTheme.SetFormStatus(this, Present("تعذر حفظ ملف التصدير. تحقق من صلاحية المجلد."), false);
                        return;
                    }
                }

                UiTheme.SetFormStatus(this, Present("تم تصدير السجل إلى Excel بنجاح."), true);
            }
        }

        private void ReloadInvoiceHistory()
        {
            var historyClock = System.Diagnostics.Stopwatch.StartNew();
        SalesHistoryPage page = salesHistoryLoader(isLegacyMappedInvoiceHistory);
        PerfTrace.Log("history-load", "sales", historyClock.Elapsed.TotalMilliseconds);
        historyClock.Restart();
            isCommercialInvoiceHistory = page.Mode == SalesHistoryMode.CommercialInvoice;
            isLegacyMappedInvoiceHistory = page.Mode == SalesHistoryMode.LegacyInvoice;
            DataTable table = BuildSalesHistoryTable(page);
            invoiceHistoryAllRows = table;
            invoiceHistoryPageSize = 500;
            EnsureHistorySearch();
            ApplyHistoryFilter();
            PerfTrace.Log("history-bind", "sales", historyClock.Elapsed.TotalMilliseconds);

            if (InvoiceHistoryGV.Columns["العنوان"] != null) InvoiceHistoryGV.Columns["العنوان"].Visible = false;
            if (InvoiceHistoryGV.Columns["ملاحظات"] != null) InvoiceHistoryGV.Columns["ملاحظات"].Visible = false;
            if (InvoiceHistoryGV.Columns["الهاتف"] != null) InvoiceHistoryGV.Columns["الهاتف"].Width = 100;
            if (InvoiceHistoryGV.Columns["إجمالي إنفاق العميل"] != null) InvoiceHistoryGV.Columns["إجمالي إنفاق العميل"].DefaultCellStyle.Format = "N2";
            if (InvoiceHistoryGV.Columns["إجمالي الفاتورة"] != null) InvoiceHistoryGV.Columns["إجمالي الفاتورة"].DefaultCellStyle.Format = "N2";

            lblTabHistory.Text = Present(isCommercialInvoiceHistory || isLegacyMappedInvoiceHistory ? "سجل الفواتير" : "سجل العملاء");
            lblHistoryTitle.Text = Present(isCommercialInvoiceHistory ? "سجل فواتير البيع المعتمدة" :
                isLegacyMappedInvoiceHistory ? "الفواتير الجديدة القابلة للاستئناف" : "سجل العملاء");
            Control previewButton = pnlInvoiceHistory.Controls["btnPreviewInvoiceHistory"];
            if (previewButton != null) previewButton.Text = Present(isCommercialInvoiceHistory || isLegacyMappedInvoiceHistory ? "عرض الفاتورة" : "عرض سجل العميل");

            invoiceHistoryRequiresReload = false;
            UpdateInvoiceHistoryActions(table.Rows.Count > 0);
            UpdateLegacyInvoiceHistoryActions(table.Rows.Count > 0, page.HasLegacyArchive);
        }

        private void InvalidateInvoiceHistory()
        {
            invoiceHistoryRequiresReload = true;
            if (IsDisposed || Disposing) return;
            foreach (string name in new[] { "btnPreviewInvoiceHistory", "btnCancelHistoryInvoice", "btnCommercialReturn" })
            {
                Control action = pnlInvoiceHistory.Controls[name];
                if (action != null) { action.Enabled = false; action.AccessibleDescription = Present("تعذر تحديث السجل. أعد فتحه قبل تنفيذ إجراء آخر."); }
            }
        }

        private static DataTable BuildSalesHistoryTable(SalesHistoryPage page)
        {
            DataTable table = new DataTable();
            if (page.Mode == SalesHistoryMode.CommercialInvoice)
            {
                table.Columns.Add("معرف الفاتورة", typeof(Guid)); table.Columns.Add("رقم الفاتورة", typeof(long));
                table.Columns.Add("اسم العميل"); table.Columns.Add("الهاتف"); table.Columns.Add("إجمالي الفاتورة", typeof(decimal));
                table.Columns.Add("تاريخ الإصدار", typeof(DateTime)); table.Columns.Add("الكاشير"); table.Columns.Add("الحالة"); table.Columns.Add("ملاحظات");
                foreach (SalesHistoryRecord item in page.Records)
                    table.Rows.Add(item.InvoiceId.Value, item.InvoiceNumber.Value, item.CustomerName, item.PhoneNumber, item.TotalAmount, item.IssuedAt, item.CashierName, item.Status, item.Notes);
            }
            else if (page.Mode == SalesHistoryMode.LegacyInvoice)
            {
                table.Columns.Add("مرجع الفاتورة"); table.Columns.Add("اسم العميل"); table.Columns.Add("الهاتف");
                table.Columns.Add("إجمالي الفاتورة", typeof(decimal)); table.Columns.Add("تاريخ الإصدار", typeof(DateTime));
                table.Columns.Add("الكاشير"); table.Columns.Add("الحالة"); table.Columns.Add("ملاحظات");
                foreach (SalesHistoryRecord item in page.Records)
                    table.Rows.Add(item.InvoiceReference, item.CustomerName, item.PhoneNumber, item.TotalAmount, item.IssuedAt, item.CashierName, item.Status, item.Notes);
            }
            else
            {
                table.Columns.Add("رقم العميل"); table.Columns.Add("اسم العميل"); table.Columns.Add("الهاتف");
                table.Columns.Add("إجمالي إنفاق العميل", typeof(decimal)); table.Columns.Add("آخر تحديث", typeof(DateTime));
                table.Columns.Add("الكاشير"); table.Columns.Add("العنوان"); table.Columns.Add("ملاحظات");
                foreach (SalesHistoryRecord item in page.Records)
                    table.Rows.Add(item.CustomerId, item.CustomerName, item.PhoneNumber, item.TotalAmount,
                        item.IssuedAt == DateTime.MinValue ? (object)DBNull.Value : item.IssuedAt, item.CashierName, item.Address, item.Notes);
            }
            return table;
        }

        private void UpdateInvoiceHistoryActions(bool hasRows)
        {
            hasRows = hasRows && !invoiceHistoryRequiresReload;
            Control previewButton = pnlInvoiceHistory.Controls["btnPreviewInvoiceHistory"];
            if (previewButton == null)
            {
                return;
            }

            previewButton.Enabled = hasRows;
            previewButton.AccessibleDescription = invoiceHistoryRequiresReload
                ? Present("تعذر تحديث السجل. أعد فتحه قبل تنفيذ إجراء آخر.")
                : hasRows ? Present("يعرض تفاصيل الفاتورة أو سجل العميل المحدد.")
                : Present("لا توجد فواتير محفوظة للعرض حالياً.");
        }

        private void AddLegacyInvoiceHistoryActions()
        {
            if (pnlInvoiceHistory.Controls.ContainsKey("btnToggleLegacyInvoiceHistory"))
            {
                return;
            }

            Button toggleButton = new Button
            {
                Name = "btnToggleLegacyInvoiceHistory",
                Text=Present("الفواتير الحديثة"),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Size = new Size(150, 40),
                Location = new Point(Math.Max(24, btnBackToCart.Left - 328), btnBackToCart.Top),
                Cursor = Cursors.Hand,
                AccessibleDescription = "يعرض الفواتير التي ربطها النظام صراحة بعد التحديث."
            };
            toggleButton.Click += btnToggleLegacyInvoiceHistory_Click;
            pnlInvoiceHistory.Controls.Add(toggleButton);
            toggleButton.BringToFront();
            CommerceWorkspaceTheme.ApplySecondaryAction(toggleButton);

            AccessibleAntdButton cancelButton = new AccessibleAntdButton
            {
                Name = "btnCancelHistoryInvoice",
                Text=Present("إلغاء المحددة"),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Size = new Size(150, 40),
                Location = new Point(Math.Max(24, btnBackToCart.Left - 492), btnBackToCart.Top),
                Cursor = Cursors.Hand,
                Visible = false,
                Enabled = false,
                AccessibleDescription = "ينشئ عكساً موثقاً للمخزون والقيد لفاتورة حديثة محددة."
            };
            cancelButton.Click += btnCancelHistoryInvoice_Click;
            pnlInvoiceHistory.Controls.Add(cancelButton);
            cancelButton.BringToFront();
            CommerceWorkspaceTheme.ApplySecondaryAction(cancelButton);
        }

        private void AddCommercialReturnHistoryAction()
        {
            Button button = new Button { Name="btnCommercialReturn", Text=Present("إشعار مرتجع"), Anchor=AnchorStyles.Top|AnchorStyles.Right, Size=new Size(150,40), Location=new Point(Math.Max(24,btnBackToCart.Left-656),btnBackToCart.Top), Visible=false, Enabled=false, Cursor=Cursors.Hand, AccessibleDescription=Present("ينشئ إشعار مرتجع من فاتورة بيع تجارية محددة.") };
            button.Click += (s,e) => { var row=GetSelectedHistoryRow(InvoiceHistoryGV); Guid id; if(row==null || !Guid.TryParse(Convert.ToString(row.Cells["معرف الفاتورة"].Value),out id)) { AppNotifications.Show(Present("حدد فاتورة بيع تجارية أولاً."), Present("إشعار مرتجع")); return; } using(var form=new CommercialSalesReturnForm(id)) if(form.ShowDialog(this)==DialogResult.OK) LoadInvoiceHistory(); };
            pnlInvoiceHistory.Controls.Add(button); button.BringToFront(); CommerceWorkspaceTheme.ApplySecondaryAction(button);
        }

        private void UpdateLegacyInvoiceHistoryActions(bool hasRows, bool hasLegacyInvoiceArchive)
        {
            hasRows = hasRows && !invoiceHistoryRequiresReload;
            Control toggleButton = pnlInvoiceHistory.Controls["btnToggleLegacyInvoiceHistory"];
            if (toggleButton != null)
            {
                toggleButton.Visible = !isCommercialInvoiceHistory;
                toggleButton.Text = Present(isLegacyMappedInvoiceHistory ? "سجل العملاء" : "الفواتير الحديثة");
                toggleButton.Enabled = !isCommercialInvoiceHistory;
                toggleButton.AccessibleDescription = hasLegacyInvoiceArchive || !isLegacyMappedInvoiceHistory
                    ? Present("ينتقل بين سجل العملاء وسجل الفواتير الحديثة المربوطة.")
                    : Present("لا توجد بعد فواتير حديثة مربوطة؛ الفواتير القديمة لا تُجمع تلقائياً حفاظاً على الدقة.");
            }

            Control cancelButton = pnlInvoiceHistory.Controls["btnCancelHistoryInvoice"];
            if (cancelButton != null)
            {
                cancelButton.Visible = isLegacyMappedInvoiceHistory;
                cancelButton.Enabled = isLegacyMappedInvoiceHistory && hasRows;
                cancelButton.AccessibleDescription = invoiceHistoryRequiresReload ? Present("تعذر تحديث السجل. أعد فتحه قبل تنفيذ إجراء آخر.") :
                    Present("ينشئ عكساً موثقاً للمخزون والقيد لفاتورة حديثة محددة.");
            }
            Control returnButton=pnlInvoiceHistory.Controls["btnCommercialReturn"];
            if(returnButton!=null) { returnButton.Visible=isCommercialInvoiceHistory; returnButton.Enabled=isCommercialInvoiceHistory && hasRows && PermissionService.CanExecute(AppOperation.IssueCommercialSalesReturn);
                returnButton.AccessibleDescription = invoiceHistoryRequiresReload ? Present("تعذر تحديث السجل. أعد فتحه قبل تنفيذ إجراء آخر.") : Present("ينشئ إشعار مرتجع من فاتورة بيع تجارية محددة."); }
        }

        private void btnToggleLegacyInvoiceHistory_Click(object sender, EventArgs e)
        {
            if (isCommercialInvoiceHistory)
            {
                return;
            }

            isLegacyMappedInvoiceHistory = !isLegacyMappedInvoiceHistory;
            LoadInvoiceHistory();
        }

        private void btnCancelHistoryInvoice_Click(object sender, EventArgs e)
        {
            if (!isLegacyMappedInvoiceHistory)
            {
                return;
            }

            if (!PermissionService.EnsureAllowed(AppOperation.CancelSalesInvoice, this))
            {
                return;
            }

            if (invoiceHistoryRequiresReload) { LoadInvoiceHistory(); return; }

            DataGridViewRow row = GetSelectedHistoryRow(InvoiceHistoryGV);
            string invoiceReference = row == null || row.Cells["مرجع الفاتورة"].Value == null
                ? string.Empty
                : row.Cells["مرجع الفاتورة"].Value.ToString();
            if (string.IsNullOrWhiteSpace(invoiceReference))
            {
                AppNotifications.Show(Present("حدد فاتورة حديثة من السجل أولاً."), Present("إلغاء الفاتورة"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string status = row.Cells["الحالة"].Value == null ? string.Empty : row.Cells["الحالة"].Value.ToString();
            if (string.Equals(status, "ملغاة", StringComparison.OrdinalIgnoreCase))
            {
                AppNotifications.Show(Present("هذه الفاتورة ملغاة مسبقاً، ولا يمكن تكرار الإلغاء."), Present("إلغاء الفاتورة"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            DialogResult confirmation = AppNotifications.Show(
                PresentFormat("سيتم إلغاء الفاتورة رقم {0} فقط، وإعادة مخزونها وإنشاء قيد محاسبي معاكس.\nلن تُحذف السجلات الأصلية.\n\nهل تريد المتابعة؟", invoiceReference),
                Present("تأكيد إلغاء الفاتورة"),
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);
            if (confirmation != DialogResult.Yes)
            {
                return;
            }

            SalesInvoiceCancellationOutcome completed = null;
            try
            {
                completed = salesCancellationOperation.Cancel(new SalesInvoiceCancellationRequest
                {
                    InvoiceReference = invoiceReference,
                    Reason = "إلغاء من سجل الفواتير",
                    ActorName = SessionContext.CurrentUser
                });
                invoiceHistoryRequiresReload = true;
                UiTheme.SetStatus(lblStockStatus, PresentFormat("تم إلغاء الفاتورة {0} وإعادة {1} وحدة إلى المخزون.", completed.OriginalInvoiceReference, completed.RestoredQuantity), true);
                ReloadInvoiceHistory();
                AppNotifications.Show(PresentFormat("تم الإلغاء بأمان. المبلغ المعكوس: {0}", FormatMoney(completed.TotalAmount)), Present("تم الإلغاء"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception exception)
            {
                if (completed == null)
                    UiTheme.ShowOperationFailure(this, "إلغاء الفاتورة", exception, preserveValidationMessage: true);
                else
                {
                    InvalidateInvoiceHistory();
                    UiTheme.ShowCompletedOperationFollowUpFailure(this, "إلغاء الفاتورة", completed.OriginalInvoiceReference, exception);
                }
            }
        }

        private void InvoiceHistoryGV_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            ShowInvoiceHistoryPreview(InvoiceHistoryGV.Rows[e.RowIndex]);
        }

        private void AddInvoiceHistoryPreviewButton()
        {
            if (pnlInvoiceHistory.Controls.ContainsKey("btnPreviewInvoiceHistory"))
            {
                return;
            }

            Button previewButton = new Button
            {
                Name = "btnPreviewInvoiceHistory",
                Text=Present("عرض سجل العميل"),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Size = new Size(150, 40),
                Location = new Point(Math.Max(24, btnBackToCart.Left - 164), btnBackToCart.Top),
                Cursor = Cursors.Hand
            };

            previewButton.Click += btnPreviewInvoiceHistory_Click;
            pnlInvoiceHistory.Controls.Add(previewButton);
            previewButton.BringToFront();
            CommerceWorkspaceTheme.ApplySecondaryAction(previewButton);
            UpdateInvoiceHistoryActions(false);
        }

        private void btnPreviewInvoiceHistory_Click(object sender, EventArgs e)
        {
            DataGridViewRow row = GetSelectedHistoryRow(InvoiceHistoryGV);
            if (row == null)
            {
                AppNotifications.Show(Present("حدد عميلاً من السجل ثم اضغط عرض التفاصيل."), Present("سجل العملاء"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            ShowInvoiceHistoryPreview(row);
        }

        private DataGridViewRow GetSelectedHistoryRow(DataGridView grid)
        {
            if (grid.SelectedRows.Count > 0)
            {
                return grid.SelectedRows[0];
            }

            return grid.CurrentRow != null && !grid.CurrentRow.IsNewRow ? grid.CurrentRow : null;
        }

        private void ShowInvoiceHistoryPreview(DataGridViewRow row)
        {
            if (invoiceHistoryRequiresReload) { LoadInvoiceHistory(); return; }
            if (row == null) return;

            if (isCommercialInvoiceHistory)
            {
                ShowCommercialInvoiceHistoryPreview(row);
                return;
            }

            if (isLegacyMappedInvoiceHistory)
            {
                ShowMappedLegacyInvoiceHistoryPreview(row);
                return;
            }

            string customerId = row.Cells["رقم العميل"].Value?.ToString() ?? "";
            if (string.IsNullOrEmpty(customerId)) return;

            string details = "";
            try
            {
                details = FormatSalesHistoryLines(ApplicationServices.SalesHistory.LoadCustomerLines(customerId));
            }
            catch
            {
                AppNotifications.Show(Present("تعذر تحميل سجل العميل. راجع الاتصال ثم أعد المحاولة."), Present("سجل العملاء"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (string.IsNullOrEmpty(details))
                details = Present("لا توجد تفاصيل متاحة.");

            string header = PresentFormat("📋 العميل: {0}\n📅 آخر تحديث: {1}\n💰 إجمالي الإنفاق: {2}\n👤 الكاشير: {3}\n\n--- جميع بنود العميل المسجلة تاريخياً ---\n\n",
                row.Cells["اسم العميل"].Value, row.Cells["آخر تحديث"].Value,
                row.Cells["إجمالي إنفاق العميل"].Value, row.Cells["الكاشير"].Value);

            AppNotifications.Show(header + details, Present("سجل العميل"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ShowMappedLegacyInvoiceHistoryPreview(DataGridViewRow row)
        {
            string invoiceReference = row.Cells["مرجع الفاتورة"].Value == null ? string.Empty : row.Cells["مرجع الفاتورة"].Value.ToString();
            if (string.IsNullOrWhiteSpace(invoiceReference)) return;

            string details = string.Empty;
            try
            {
                details = FormatSalesHistoryLines(ApplicationServices.SalesHistory.LoadLegacyInvoiceLines(invoiceReference));
            }
            catch (Exception ex)
            {
                UiTheme.ShowOperationFailure(this, "عرض الفاتورة", ex);
                return;
            }

            if (string.IsNullOrWhiteSpace(details))
            {
                AppNotifications.Show(Present("تعذر التحقق من جميع بنود الفاتورة؛ الإلغاء محجوب حفاظاً على سلامة المخزون والحسابات."), Present("عرض الفاتورة"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string header = PresentFormat("📋 فاتورة: {0}\n📅 التاريخ: {1}\n💰 الإجمالي: {2}\n👤 الكاشير: {3}\nالحالة: {4}\n\n--- بنود هذه الفاتورة فقط ---\n\n",
                invoiceReference, row.Cells["تاريخ الإصدار"].Value, row.Cells["إجمالي الفاتورة"].Value,
                row.Cells["الكاشير"].Value, Present(Convert.ToString(row.Cells["الحالة"].Value)));
            AppNotifications.Show(header + details, Present("تفاصيل الفاتورة"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // يعرض بنود المستند التجاري المختار نفسه؛ لا نجمع كل فواتير العميل في معاينة واحدة.
        private void ShowCommercialInvoiceHistoryPreview(DataGridViewRow row)
        {
            string invoiceId = row.Cells["معرف الفاتورة"].Value == null ? string.Empty : row.Cells["معرف الفاتورة"].Value.ToString();
            if (string.IsNullOrWhiteSpace(invoiceId)) return;

            string details = string.Empty;
            try
            {
                Guid parsedInvoiceId;
                if (!Guid.TryParse(invoiceId, out parsedInvoiceId))
                {
                    throw new InvalidOperationException("معرف الفاتورة المحددة غير صالح.");
                }
                details = FormatSalesHistoryLines(ApplicationServices.SalesHistory.LoadCommercialInvoiceLines(parsedInvoiceId));
            }
            catch (Exception ex)
            {
                AppNotifications.Show(PresentFormat("تعذر تحميل بنود الفاتورة المعتمدة.\n\n{0}", ConnectionSettingsService.BuildConnectionHelp(ex, presentationLeftToRight)),
                    Present("سجل الفواتير"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(details)) details = Present("لا توجد بنود متاحة لهذه الفاتورة.");
            string header = PresentFormat("فاتورة رقم: {0}\nالعميل: {1}\nتاريخ الإصدار: {2}\nالإجمالي: {3}\nالكاشير: {4}\n\n--- بنود الفاتورة ---\n\n",
                row.Cells["رقم الفاتورة"].Value, row.Cells["اسم العميل"].Value, row.Cells["تاريخ الإصدار"].Value,
                row.Cells["إجمالي الفاتورة"].Value, row.Cells["الكاشير"].Value);
            AppNotifications.Show(header + details, Present("تفاصيل فاتورة البيع"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private string FormatSalesHistoryLines(IReadOnlyList<SalesHistoryLine> lines)
        {
            if (lines == null || lines.Count == 0)
            {
                return string.Empty;
            }

            return string.Join(Environment.NewLine, lines.Select(line => PresentFormat(
                "{0}  |  سعر: {1:N2}  |  كمية: {2:N3}  |  إجمالي: {3:N2}  |  وحدة: {4}",
                line.MedicineName,
                line.UnitPrice,
                line.Quantity,
                line.TotalAmount, string.IsNullOrWhiteSpace(line.Unit)?"—":line.Unit)));
        }


        // زر الرجوع مع تنبيه عند وجود سلة بيع غير محفوظة.
        private void btnBack_Click(object sender, EventArgs e)
        {
            if(salesRecoveryBusy)return;
            if(salesPending!=null||salesRecoveryFault||salesRecoveryStale){Close();return;}
            try
            {
                if (!_isInvoiceSaved && tempTransaction.Count > 0)
                {
                    DialogResult result = AppNotifications.Show(
                        Present("هل أنت متأكد أنك تريد الخروج؟ سيتم إلغاء سلة البيع غير المحفوظة."), Present("تأكيد الخروج"),
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);

                    if (result == DialogResult.Yes)
                    {
                        RollbackTransaction();
                        BillingGV.Rows.Clear();savedUnitPresentation=null;acceptedUnitOrigin=null;
                        CustomizeDataGridView();
                    }
                    else
                    {
                        return;
                    }
                }

                // العودة للشاشة الرئيسية بدون إنشاء نسخة جديدة منها.
                Close();
            }
            catch (Exception ex)
            {
                UiTheme.ShowOperationIssue(this, "الرجوع من الفاتورة", ex);
            }
        }

        private const string V = "اختر الدواء";






        // يجلب سعر بيع الدواء من قاعدة البيانات.
        // حدث تغيير اسم الموظف، تم إلغاء التحديث التلقائي هنا.
        private void SelectEmployee_SelectedIndexChanged(object sender, EventArgs e)
        {
        }







        // دالة لملء ComboBox بأسماء الأدوية
        // يحمل أسماء الأدوية المتاحة إلى قائمة اختيار الدواء.
        private static OperationActor MedicineReadActor() => new OperationActor { UserName = SessionContext.CurrentUser, UserRole = SessionContext.CurrentRole };
        private bool IsMedicineReadSession(OperationActor actor, string employee)
        {
            return !IsDisposed && !Disposing && SessionContext.IsAuthenticated && PermissionService.CanOpen(AppPermission.Sales) &&
                string.Equals(actor.UserName, SessionContext.CurrentUser, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(actor.UserRole, SessionContext.CurrentRole, StringComparison.OrdinalIgnoreCase) && employee == SessionContext.CurrentEmployeeId;
        }
        private Func<bool> CaptureMedicineReadContext(OperationActor actor, string employee)
        {
            long sessionRevision = SessionContext.CurrentSessionRevision;
            var connectionIsCurrent = ApplicationServices.CaptureRuntimeConnectionContext();
            return () => SessionContext.CurrentSessionRevision == sessionRevision && connectionIsCurrent() &&
                IsMedicineReadSession(actor, employee);
        }
        private void CancelMedicineBarcodeRead()
        {
            var cancellation = medicineBarcodeCancellation; medicineBarcodeCancellation = null;
            cancellation?.Cancel(); ++medicineBarcodeVersion;
            if (medicineBarcodeSelectionVersion == medicineSelectionVersion)
            {
                var selection = medicineSelectionCancellation; medicineSelectionCancellation = null; selection?.Cancel();
                ++medicineSelectionVersion; medicineSelectionLoading = false; selectedStockPreview = null;
            }
            medicineBarcodeSelectionVersion = -1; medicineBarcodeLoading = false;
            ApplyMedicineReadActions();
        }
        private void CancelCurrencyRead()
        {
            var cancellation = currencyCancellation; currencyCancellation = null; ++currencyReadVersion;
            currencyLoading = false; cancellation?.Cancel();
        }
        internal async Task<bool> LoadCurrencySnapshotAsync()
        {
            if (IsDisposed || Disposing || previewMode || _isInvoiceSaved || _isInvoiceCancelled || _isSavingInvoice) return false;
            if (currencySnapshot != null) return IsCurrencyContextCurrent; // Freeze identity until the user starts a new draft.
            CancelCurrencyRead();
            var cancellation = new CancellationTokenSource(); currencyCancellation = cancellation;
            long version = ++currencyReadVersion; var actor = MedicineReadActor();
            string employee = SessionContext.CurrentEmployeeId;
            var readContextIsCurrent = CaptureMedicineReadContext(actor, employee);
            currencyLoading = true;
            UiTheme.SetStatus(lblStockStatus, Present("جارٍ التحقق من عملة الفاتورة…"));
            try
            {
                if (!readContextIsCurrent()) return false;
                var value = await currencyReadService.GetSystemCurrencySnapshotAsync(actor, cancellation.Token);
                if (cancellation.IsCancellationRequested || version != currencyReadVersion || !readContextIsCurrent() ||
                    !readContextIsCurrent() || _isInvoiceSaved || _isInvoiceCancelled) return false;
                if (value == null) throw new CurrencySnapshotUnavailableException();
                currencySnapshot = value; currencyActor = actor; currencyEmployee = employee; currencyConnectionIsCurrent = readContextIsCurrent;
                UpdateTotalAmount(); ApplyMedicineReadActions();
                UiTheme.SetStatus(lblStockStatus, Present("تم التحقق من العملة؛ تبقى ثابتة لهذه المسودة."), true); return true;
            }
            catch (OperationCanceledException) { return false; }
            catch (CurrencySnapshotUnavailableException)
            {
                if (version == currencyReadVersion && readContextIsCurrent())
                    UiTheme.SetStatus(lblStockStatus, Present("عملة النظام غير محددة. راجع الإعدادات ثم اضغط تحديث."), false);
                return false;
            }
            catch (Exception exception)
            {
                if (version == currencyReadVersion && readContextIsCurrent()) SetBillingStatusFailure("قراءة عملة الفاتورة", exception);
                return false;
            }
            finally
            {
                if (version == currencyReadVersion && !IsDisposed && !Disposing)
                { currencyLoading = false; if (currencyCancellation == cancellation) currencyCancellation = null; }
                cancellation.Dispose();
            }
        }
        private CurrencyDisplaySnapshot RequireCurrencySnapshot()
        {
            if (currencySnapshot == null || !IsCurrencyContextCurrent || currencyLoading)
                throw new InvalidOperationException("عملة الفاتورة غير متاحة. اضغط تحديث وانتظر التحقق قبل الحفظ.");
            return currencySnapshot;
        }
        private void ResetDraftCurrency()
        {
            CancelCurrencyRead(); currencySnapshot = null; currencyActor = null; savedLegacyCurrencyText = null;
            UpdateTotalAmount(); ApplyMedicineReadActions();
            if (!previewMode && currencyDisplayOverride == null) _ = LoadCurrencySnapshotAsync();
        }
        private void CancelMedicineReads()
        {
            CancelCustomerReads();
            CancelCurrencyRead();
            CancelMedicineBarcodeRead();
            var names = medicineNamesCancellation; medicineNamesCancellation = null;
            var selection = medicineSelectionCancellation; medicineSelectionCancellation = null;
            var cart = medicineCartCancellation; medicineCartCancellation = null;
            names?.Cancel(); selection?.Cancel(); cart?.Cancel();
        }
        private void ApplyMedicineReadActions()
        {
            ApplyCustomerReadActions();
            if (IsDisposed || Disposing) return;
            btnAddToCart.Enabled = (currencyDisplayOverride != null || (currencySnapshot != null && IsCurrencyContextCurrent)) && !medicineNamesLoading && !medicineSelectionLoading && !medicineBarcodeLoading && !medicineCartPending &&
                !_isInvoiceSaved && !_isInvoiceCancelled && !_isSavingInvoice && MediComboCb.SelectedItem != null && selectedStockPreview.HasValue && SessionContext.IsAuthenticated && PermissionService.CanOpen(AppPermission.Sales);
            MediComboCb.Enabled = !medicineNamesLoading && !medicineCartPending;
            if(SalesRecoveryLocked){ApplySalesRecoveryLock();return;}
            btnRefreshMedicineNames.Enabled = !previewMode && !medicineNamesLoading && !medicineCartPending && !_isSavingInvoice && !_isInvoiceSaved && !_isInvoiceCancelled && SessionContext.IsAuthenticated && PermissionService.CanOpen(AppPermission.Sales);
        }
        internal async Task<bool> LoadMedicineNamesAsync()
        {
            if (IsDisposed || Disposing) return false;
            CancelMedicineBarcodeRead();
            medicineNamesCancellation?.Cancel();
            var oldSelection = medicineSelectionCancellation; medicineSelectionCancellation = null; oldSelection?.Cancel();
            medicineCartCancellation?.Cancel();
            var cancellation = new CancellationTokenSource(); medicineNamesCancellation = cancellation;
            long version = ++medicineNamesVersion; ++medicineSelectionVersion;
            var actor = MedicineReadActor(); string employee = SessionContext.CurrentEmployeeId;
            var readContextIsCurrent = CaptureMedicineReadContext(actor, employee);
            medicineNamesLoading = true; selectedStockPreview = null; MediComboCb.Items.Clear(); MediComboCb.Text = Present(V);
            ApplyMedicineReadActions(); UiTheme.SetStatus(lblStockStatus, Present("جارٍ تحميل أسماء الأدوية…"));
            try
            {
                if (!readContextIsCurrent()) throw new UnauthorizedAccessException("لا تملك صلاحية الوصول إلى بيانات الأدوية.");
                var values = await medicineService.GetNamesAsync(actor, cancellation.Token);
                if (cancellation.IsCancellationRequested || version != medicineNamesVersion || !readContextIsCurrent()) return false;
                foreach (var medicineName in values) MediComboCb.Items.Add(medicineName);
                UiTheme.SetStatus(lblStockStatus, Present("المخزون المتاح — اختر صنفاً أولاً")); return true;
            }
            catch (OperationCanceledException) { return false; }
            catch (Exception exception)
            { if (version == medicineNamesVersion && readContextIsCurrent()) { MediComboCb.Items.Clear(); SetBillingStatusFailure("تحميل أسماء الأدوية", exception); } return false; }
            finally
            {
                if (version == medicineNamesVersion && !IsDisposed && !Disposing)
                {
                    medicineNamesLoading = false; medicineSelectionLoading = false;
                    if (medicineNamesCancellation == cancellation) medicineNamesCancellation = null;
                    if (!readContextIsCurrent()) { MediComboCb.Items.Clear(); selectedStockPreview = null; }
                    ApplyMedicineReadActions();
                }
                cancellation.Dispose();
            }
        }
        internal async Task<bool> RefreshSelectedMedicineAsync()
        {
            if (IsDisposed || Disposing) return false;
            lblStockStatus.AccessibleDescription = null;
            medicineSelectionCancellation?.Cancel();
            var cancellation = new CancellationTokenSource(); medicineSelectionCancellation = cancellation;
            long version = ++medicineSelectionVersion;
            var actor = MedicineReadActor(); string employee = SessionContext.CurrentEmployeeId;
            var readContextIsCurrent = CaptureMedicineReadContext(actor, employee);
            string name = MediComboCb.SelectedItem?.ToString(); selectedStockPreview = null;
            medicineUnitPicker?.ResetSelection();
            medicineSelectionLoading = !string.IsNullOrWhiteSpace(name); ApplyMedicineReadActions();
            try
            {
                if (string.IsNullOrWhiteSpace(name)) { UiTheme.SetStatus(lblStockStatus, Present("المخزون المتاح — اختر صنفاً أولاً")); ResetQuantityToDefault(); return false; }
                UiTheme.SetStatus(lblStockStatus, Present("جارٍ التحقق من الصنف…"));
                if (!readContextIsCurrent()) return false;
                var medicine = await medicineService.FindByNameAsync(name, actor, cancellation.Token);
                if (cancellation.IsCancellationRequested || version != medicineSelectionVersion || !readContextIsCurrent() || MediComboCb.SelectedItem?.ToString() != name) return false;
                if (medicine == null) { UiTheme.SetStatus(lblStockStatus, Present("تعذر العثور على الصنف المحدد. حدّث قائمة الأدوية."), false); return false; }
                if(unitCatalogEnabled)
                {
                    var units=await Task.Run(()=>MedicineUnitInvoiceBridge.ReadUnits(name,true),cancellation.Token);
                    if(cancellation.IsCancellationRequested||version!=medicineSelectionVersion||!readContextIsCurrent()||MediComboCb.SelectedItem?.ToString()!=name)return false;
                    medicineUnitPicker.SetUnits(name,units); CommerceWorkspaceTheme.RefreshLayout(this);
                }
                selectedStockPreview = medicine.Quantity; txtQuantity_TextChanged(this, EventArgs.Empty); return true;
            }
            catch (OperationCanceledException) { return false; }
            catch (Exception exception) { if (version == medicineSelectionVersion && readContextIsCurrent()) SetBillingStatusFailure("التحقق من المخزون", exception); return false; }
            finally
            {
                if (version == medicineSelectionVersion && !IsDisposed && !Disposing)
                {
                    medicineSelectionLoading = false;
                    if (medicineSelectionCancellation == cancellation) medicineSelectionCancellation = null;
                    if (!readContextIsCurrent()) selectedStockPreview = null;
                    ApplyMedicineReadActions();
                }
                cancellation.Dispose();
            }
        }

        private void SelectMedicineByName(string medicineName)
        {
            if (string.IsNullOrWhiteSpace(medicineName))
            {
                return;
            }

            for (int index = 0; index < MediComboCb.Items.Count; index++)
            {
                if (string.Equals(MediComboCb.Items[index].ToString(), medicineName, StringComparison.OrdinalIgnoreCase))
                {
                    MediComboCb.SelectedIndex = index;
                    return;
                }
            }

            MediComboCb.Text = medicineName;
        }

        private bool unifiedBarcodeNotFound;
        internal async Task<bool> TrySelectMedicineByBarcodeAsync()
        {
            if (previewMode || IsDisposed || Disposing || medicineNamesLoading || medicineCartPending ||
                _isSavingInvoice || _isInvoiceSaved || _isInvoiceCancelled) return false;
            unifiedBarcodeNotFound = false;
            CancelMedicineBarcodeRead();
            var cancellation = new CancellationTokenSource(); medicineBarcodeCancellation = cancellation;
            long version = ++medicineBarcodeVersion;
            var actor = MedicineReadActor(); string employee = SessionContext.CurrentEmployeeId;
            var readContextIsCurrent = CaptureMedicineReadContext(actor, employee);
            string barcode = txtBarcode.Text;
            // A failed scan must not leave the previous medicine available for an accidental add.
            var oldSelection = medicineSelectionCancellation; medicineSelectionCancellation = null; oldSelection?.Cancel();
            ++medicineSelectionVersion; medicineSelectionLoading = false; selectedStockPreview = null;
            medicineBarcodeApplyingSelection = true;
            string searchText = MediComboCb.Text;
            try { MediComboCb.SelectedIndex = -1; if (unifiedSearchPending) MediComboCb.Text = searchText; }
            finally { medicineBarcodeApplyingSelection = false; }
            medicineBarcodeLoading = true; ApplyMedicineReadActions(); lblStockStatus.AccessibleDescription = null;
            UiTheme.SetStatus(lblStockStatus, Present("جارٍ البحث بالباركود…"));
            try
            {
                if (!readContextIsCurrent()) return false;
                var match = await medicineSearchService.FindByBarcodeAsync(barcode, actor, cancellation.Token);
                if (cancellation.IsCancellationRequested || version != medicineBarcodeVersion || !readContextIsCurrent() ||
                    barcode != txtBarcode.Text || _isSavingInvoice || _isInvoiceSaved || _isInvoiceCancelled) return false;
                if (match == null) { unifiedBarcodeNotFound = true; UiTheme.SetStatus(lblStockStatus, Present("الباركود غير موجود."), false); return false; }
                int index = -1;
                for (int i = 0; i < MediComboCb.Items.Count; i++)
                    if (string.Equals(MediComboCb.Items[i].ToString(), match.MedicineName, StringComparison.OrdinalIgnoreCase)) { index = i; break; }
                if (index < 0) { UiTheme.SetStatus(lblStockStatus, Present("الصنف غير موجود في القائمة الحالية. حدّث الأصناف ثم أعد المسح."), false); return false; }
                medicineBarcodeApplyingSelection = true;
                try { MediComboCb.SelectedIndex = index; }
                finally { medicineBarcodeApplyingSelection = false; }
                var selectedRead = RefreshSelectedMedicineAsync(); medicineBarcodeSelectionVersion = medicineSelectionVersion;
                bool selected = await selectedRead;
                return selected && !cancellation.IsCancellationRequested && version == medicineBarcodeVersion &&
                    readContextIsCurrent() && barcode == txtBarcode.Text &&
                    !_isSavingInvoice && !_isInvoiceSaved && !_isInvoiceCancelled;
            }
            catch (OperationCanceledException) { return false; }
            catch (MedicineBarcodeLengthException)
            {
                if (version == medicineBarcodeVersion && readContextIsCurrent())
                    UiTheme.SetStatus(lblStockStatus, Present("الباركود يتجاوز 100 حرف. راجع القيمة ثم أعد المسح."), false);
                return false;
            }
            catch (MedicineBarcodeAmbiguityException exception)
            {
                if (version == medicineBarcodeVersion && readContextIsCurrent())
                {
                    AppDiagnostics.Log("Billing:BarcodeAmbiguity", exception);
                    UiTheme.SetStatus(lblStockStatus, Present("الباركود مكرر. اختر الصنف بالاسم أو صححه في إدارة الأدوية."), false);
                    lblStockStatus.AccessibleDescription = Present(MedicineBarcodeAmbiguityException.UserMessage);
                }
                return false;
            }
            catch (Exception exception)
            {
                if (version == medicineBarcodeVersion && readContextIsCurrent()) SetBillingStatusFailure("البحث بالباركود", exception);
                return false;
            }
            finally
            {
                if (version == medicineBarcodeVersion && !IsDisposed && !Disposing)
                {
                    medicineBarcodeLoading = false; medicineBarcodeSelectionVersion = -1;
                    if (medicineBarcodeCancellation == cancellation) medicineBarcodeCancellation = null;
                    if (!readContextIsCurrent())
                    {
                        selectedStockPreview = null; medicineBarcodeApplyingSelection = true;
                        try { MediComboCb.SelectedIndex = -1; } finally { medicineBarcodeApplyingSelection = false; }
                    }
                    ApplyMedicineReadActions();
                }
                cancellation.Dispose();
            }
        }

        private async void txtBarcode_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true; e.Handled = true;
            if (await TrySelectMedicineByBarcodeAsync())
            {
                txtBarcode.Clear(); txtQuantity.Focus(); txtQuantity.SelectAll();
            }
        }

        private void MedicineSelectionChanged(object sender, EventArgs args)
        {
            if (previewMode) return;
            MediComboCb_SelectedIndexChanged(sender, EventArgs.Empty);
        }

        // عند تغيير اختيار الصنف
        // يتحقق من الكمية المتاحة عند اختيار دواء.
        private async void MediComboCb_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (previewMode || medicineBarcodeApplyingSelection) return;
            CancelMedicineBarcodeRead();
            await RefreshSelectedMedicineAsync();
        }
        // يضيف الدواء المختار إلى سلة الفاتورة دون تعديل المخزون قبل الحفظ النهائي.
        private async void btnAddToCart_Click(object sender, EventArgs e) { await AddMedicineToCartAsync(); }

        internal async Task<bool> AddMedicineToCartAsync()
        {
            if(SalesRecoveryLocked)return false;
            if (medicineCartPending || medicineNamesLoading || medicineSelectionLoading || medicineBarcodeLoading || IsDisposed || Disposing) return false;
            if (currencyDisplayOverride == null && (currencySnapshot == null || !IsCurrencyContextCurrent))
            {
                UiTheme.SetStatus(lblStockStatus, Present("عملة الفاتورة غير متاحة. اضغط تحديث وانتظر التحقق قبل الحفظ."), false); return false;
            }
            var actor = MedicineReadActor(); string employee = SessionContext.CurrentEmployeeId;
            var readContextIsCurrent = CaptureMedicineReadContext(actor, employee);
            if (!readContextIsCurrent()) return false;
            var cancellation = new CancellationTokenSource(); medicineCartCancellation = cancellation;
            var states = new Dictionary<Control, bool>();
            foreach (Control control in new Control[] { MediComboCb, txtQuantity, txtBarcode, btnAddToCart, btnSaveOnly, btnPreviewPrint, btnPrintA4, btnClearInvoice, btnBack, btnRefreshMedicineNames, BillingGV, medicineUnitPicker })
            { states[control] = control.Enabled; control.Enabled = false; }
            medicineCartPending = true;
            try
            {
                if (_isInvoiceSaved)
                {
                    AppNotifications.Show(Present("تم حفظ الفاتورة بالفعل. افتح فاتورة جديدة قبل إضافة أصناف أخرى."), Present("الفواتير"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return false;
                }

                // التحقق من أن جميع الحقول تم تعبئتها (البيانات الخاصة بالدواء)
                if (MediComboCb.SelectedItem == null)
                {
                    AppNotifications.Show(Present("الرجاء اختيار دواء."), Present("الفواتير"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return false;
                }

                // التحقق من إدخال كمية صحيحة
                if (!MedicineUnitPicker.TryQuantity(txtQuantity.Text, out decimal quantity))
                {
                    AppNotifications.Show(Present("يرجى إدخال كمية صحيحة."));
                    return false;
                }

                string selectedMedicine = MediComboCb.SelectedItem.ToString(); string quantityText = txtQuantity.Text;
                MedicineUnitSelection unit=unitCatalogEnabled?medicineUnitPicker.CaptureSelection(selectedMedicine,quantity):null;
                if(unit==null && quantity!=decimal.Truncate(quantity))throw new InvalidOperationException("الصنف بلا وحدات محفوظة؛ أدخل كمية صحيحة.");
                int baseQuantity=unit?.BaseQuantity??checked((int)quantity);
                Func<bool> cartContextIsCurrent = () => !cancellation.IsCancellationRequested && readContextIsCurrent() &&
                    !IsDisposed && !Disposing && !_isInvoiceSaved && !_isInvoiceCancelled && !_isSavingInvoice &&
                    MediComboCb.SelectedItem?.ToString() == selectedMedicine && txtQuantity.Text == quantityText &&
                    (!unitCatalogEnabled || MedicineUnitPicker.Same(unit,medicineUnitPicker.SelectedUnit));
                UiTheme.SetStatus(lblStockStatus, Present("جارٍ التحقق من الصنف…"));
                MedicineRecord medicine = await medicineService.FindByNameAsync(selectedMedicine, actor, cancellation.Token);
                if (!cartContextIsCurrent()) return false;
                if (medicine == null) { UiTheme.SetStatus(lblStockStatus, Present("تعذر العثور على الصنف المحدد. حدّث قائمة الأدوية."), false); return false; }
                if (!CanSellMedicineByExpiry(medicine))
                {
                    return false;
                }

                // A confirmation dialog pumps messages and may outlive the captured session or selection.
                if (!cartContextIsCurrent()) return false;
                decimal sellingPrice = unit?.UnitPrice ?? medicine.SellingPrice;

                if (sellingPrice <= 0)
                {
                    AppNotifications.Show(Present("تعذر جلب سعر البيع للدواء المحدد."), Present("الفواتير"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }

                int availableQuantity = medicine.Quantity;
                if (baseQuantity > availableQuantity)
                {
                    AppNotifications.Show($"المتاح فقط {availableQuantity} وحدة من {selectedMedicine}.", "المخزون", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }

                if (sellingPrice * quantity > 9999999999999999.99m)
                {
                    AppNotifications.Show(Present("إجمالي بند البيع أكبر من الحد المسموح."), Present("الفواتير"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }

                decimal totalAmount = unit?.GrossTotal ?? checked(sellingPrice * quantity);

                bool medicineExistsInBill = false;
                string billId = string.Empty;
                DataGridViewRow existingRow = null;
                foreach (DataGridViewRow row in BillingGV.Rows)
                {
                    if (row.IsNewRow) continue;
                    if (row.Cells["MedName"].Value != null && row.Cells["MedName"].Value.ToString() == selectedMedicine && MedicineUnitPicker.Same(CartUnit(row),unit) && Convert.ToDecimal(row.Cells["Sprice"].Value)==sellingPrice)
                    {
                        medicineExistsInBill = true;
                        billId = row.Cells["BillId"].Value.ToString();

                        DialogResult dialogResult = AppNotifications.Show(PresentFormat("هذا الصنف موجود في السلة بكمية {0}. هل تريد استبدالها بكمية {1} بسعر الوحدة الحالي {2}؟", Convert.ToString(row.Cells["Quantity"].Value), quantity, FormatMoney(sellingPrice)), Present("تعديل الصنف المكرر"), MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                        if (dialogResult != DialogResult.Yes)
                        {
                            return false;
                        }

                        if (!cartContextIsCurrent()) return false;
                        existingRow = row;
                        break;
                    }
                }

                if(OtherCartBaseQuantity(selectedMedicine,existingRow)+baseQuantity>availableQuantity)
                    throw new InvalidOperationException("مجموع وحدات الصنف في السلة يتجاوز المخزون المتاح.");
                if (!medicineExistsInBill)
                {
                    billId = GenerateDraftBillId();
                }

                if (medicineExistsInBill && existingRow != null)
                {
                    if(unit!=null)cartUnits[existingRow]=unit; else cartUnits.Remove(existingRow);
                    existingRow.Cells["Unit"].Value=unit?.Name??"—";
                    existingRow.Tag = availableQuantity;
                    existingRow.Cells["Sprice"].Value = sellingPrice;
                    existingRow.Cells["Quantity"].Value = quantity;
                    existingRow.Cells["TotalAmount"].Value = totalAmount;
                }
                else
                {
                    int addedRow = BillingGV.Rows.Add(billId, selectedMedicine, sellingPrice, quantity, totalAmount);
                    BillingGV.Rows[addedRow].Tag = availableQuantity;
                    if(unit!=null)cartUnits[BillingGV.Rows[addedRow]]=unit;
                    BillingGV.Rows[addedRow].Cells["Unit"].Value=unit?.Name??"—";
                }

                TrackTemporaryTransaction(selectedMedicine, baseQuantity);

                UpdateTotalAmount();

                MediComboCb.SelectedIndex = -1;
                MediComboCb.Text = V;
                ResetQuantityToDefault();
                MediComboCb.Focus();
                // All cart validation and updates succeeded; preference failure must preserve this outcome.
                try { frequentMedicineRecorder?.TryRecordSuccessfulAddition(selectedMedicine); }
                catch (Exception error) { AppDiagnostics.Log("BillingFrequentPreferencesEnqueue", error); }
                return true;
            }
            catch (OperationCanceledException) { return false; }
            catch (Exception ex)
            {
                if (readContextIsCurrent()) SetBillingStatusFailure("إضافة الصنف إلى الفاتورة", ex);
                return false;
            }
            finally
            {
                medicineCartPending = false;
                if (medicineCartCancellation == cancellation) medicineCartCancellation = null;
                cancellation.Dispose();
                if (!IsDisposed && !Disposing && !_isInvoiceSaved && !_isInvoiceCancelled && !_isSavingInvoice)
                {
                    if (readContextIsCurrent())
                    { foreach (var state in states) if (!state.Key.IsDisposed) state.Key.Enabled = state.Value; ApplyMedicineReadActions(); }
                    else
                    {
                        selectedStockPreview = null;
                        if (SessionContext.IsAuthenticated && PermissionService.CanOpen(AppPermission.Sales))
                            foreach (var state in states)
                                if (!state.Key.IsDisposed && state.Key != btnSaveOnly && state.Key != btnPreviewPrint && state.Key != btnPrintA4 && state.Key != btnAddToCart)
                                    state.Key.Enabled = state.Value;
                        btnBack.Enabled = true;
                        btnSaveOnly.Enabled = btnPreviewPrint.Enabled = btnPrintA4.Enabled = btnAddToCart.Enabled = false;
                        btnRefreshMedicineNames.Enabled = SessionContext.IsAuthenticated && PermissionService.CanOpen(AppPermission.Sales);
                        UiTheme.SetStatus(lblStockStatus, presentationLeftToRight
                            ? "Your session or database changed. Refresh or reopen this screen; the invoice draft was preserved."
                            : "تغيرت الجلسة أو قاعدة البيانات. حدّث البيانات أو أعد فتح الشاشة؛ مسودة الفاتورة محفوظة في الشاشة.", false);
                    }
                }
            }
        }

        // يمنع بيع الأدوية المنتهية، وينبه المستخدم قبل بيع الأدوية القريبة من الانتهاء.
        private bool CanSellMedicineByExpiry(MedicineRecord medicine)
        {
            string medicineName = medicine.MedName; DateTime expiryDate = medicine.ExpiryDate.Date;
            DateTime today = DateTime.Today;
            if (expiryDate.Date < today)
            {
                AppNotifications.Show(
                    PresentFormat("لا يمكن بيع {0} لأن صلاحيته منتهية بتاريخ {1}.", medicineName, expiryDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)),
                    Present("الصلاحية"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return false;
            }

            if (expiryDate.Date <= today.AddDays(30))
            {
                DialogResult confirm = AppNotifications.Show(
                    PresentFormat("تنبيه: {0} تنتهي صلاحيته بتاريخ {1}. هل تريد المتابعة؟", medicineName, expiryDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)),
                    Present("قرب انتهاء الصلاحية"),
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2);
                return confirm == DialogResult.Yes;
            }

            return true;
        }

        // يحفظ الكمية المخصومة مؤقتًا حتى يمكن إرجاعها عند إلغاء الفاتورة.
        private void TrackTemporaryTransaction(string medicineName, int quantity)
        {
            for (int index = 0; index < tempTransaction.Count; index++)
            {
                if (string.Equals(tempTransaction[index].Medicine, medicineName, StringComparison.OrdinalIgnoreCase))
                {
                    tempTransaction[index] = (medicineName, quantity);
                    return;
                }
            }

            tempTransaction.Add((medicineName, quantity));
        }

        // يحذف دواء من قائمة التراجع المؤقتة بعد إزالته من الفاتورة.
        private void RemoveTemporaryTransaction(string medicineName)
        {
            tempTransaction.RemoveAll(item =>
                string.Equals(item.Medicine, medicineName, StringComparison.OrdinalIgnoreCase));
            if(BillingGV.Rows.Cast<DataGridViewRow>().Any(r=>!r.IsNewRow && string.Equals(Convert.ToString(r.Cells["MedName"].Value),medicineName,StringComparison.OrdinalIgnoreCase)))
                TrackTemporaryTransaction(medicineName,checked((int)OtherCartBaseQuantity(medicineName)));
        }






        // تعريف الدالة لتوليد ID عشوائي
        // يولد رقم فاتورة قصير وفريد.
        private string GenerateDraftBillId()
        {
            _draftBillLineSequence++;
            return "DRAFT-" + _draftBillLineSequence.ToString();
        }

        // يحمل بيانات الفواتير والأدوية والموظفين وأرقام العملاء عند فتح الشاشة.
        private async void Billing_Load(object sender, EventArgs e)
        {
            try
            {

                InitializeBillingGrid(); // تأكد من إعداد DataGridView بشكل صحيح
                await InitializeSalesRecoveryAsync();
                if(SalesRecoveryLocked)return;
                PopulateGrid(BillingGV); // تحميل بيانات الفواتير
                PriceBDLabel.Text = Present("الإجمالي") + ": " + FormatMoney(0);
                PriceADLabel.Text = Present("الصافي") + ": " + FormatMoney(0);
                LoadEmployeeNames(); // تحميل أسماء الموظفين في ComboBox
                await LoadCustomerOptionsAsync();
                ApplyCurrentCashier();
                ApplyDefaultCustomer();
                MediComboCb.Text = Present(V);
                await LoadMedicineNamesAsync();
                await LoadCurrencySnapshotAsync();
            }
            catch (Exception ex)
            {
                UiTheme.ShowOperationIssue(this, "تحميل شاشة الفواتير", ex);
            }
        }

        // يحمل أسماء الموظفين إلى قائمة الاختيار.
        private void LoadEmployeeNames()
        {
            SelectEmployee.Items.Clear();
            // The sales cashier is locked to the identity returned by authentication.
            // Do not load employee salaries, phones, ages or other accounts into POS.
            if (!SessionContext.IsAuthenticated || string.IsNullOrWhiteSpace(SessionContext.CurrentEmployeeId)) return;
            SelectEmployee.Items.Add(new ComboBoxItem(SessionContext.CurrentUser, SessionContext.CurrentEmployeeId));
            SelectEmployee.SelectedIndex = 0;
        }

        private void ApplyCurrentCashier()
        {
            string currentUser = SessionContext.CurrentUser;
            ComboBoxItem selectedEmployee = null;

            foreach (object employee in SelectEmployee.Items)
            {
                if (employee is ComboBoxItem item &&
                    string.Equals(item.Text, currentUser, StringComparison.OrdinalIgnoreCase))
                {
                    selectedEmployee = item;
                    break;
                }
            }

            if (selectedEmployee != null)
            {
                SelectEmployee.SelectedValue = selectedEmployee;
                EmployeeLabel.Text = PresentFormat("اسم الكاشير: {0}", selectedEmployee.Text);
                EmployeeIdLabel.Text = PresentFormat("رقم الكاشير: {0}", selectedEmployee.Value);
            }
            else
            {
                SelectEmployee.Text = currentUser;
                EmployeeLabel.Text = PresentFormat("اسم الكاشير: {0}", currentUser);
                EmployeeIdLabel.Text = Present("رقم الكاشير:");
            }

            SelectEmployee.Enabled = false;
            UiTheme.ApplyInfoLabels(EmployeeLabel, EmployeeIdLabel);
        }

        private void ApplyDefaultCustomer()
        {
            if (string.IsNullOrWhiteSpace(cmbCustomerName.Text))
            {
                cmbCustomerName.Text = "عميل عام";
            }

            if (string.IsNullOrWhiteSpace(cmbCustomerPhone.Text))
            {
                cmbCustomerPhone.Text = "000000000";
            }

            if (string.IsNullOrWhiteSpace(txtCustomerAddress.Text))
            {
                txtCustomerAddress.Text = "عام";
            }

            RefreshCustomerSummary();
        }

        private void RefreshCustomerSummary()
        {
            UpdateCustomerTypeIndicator();
            if (CustomerNameLabel == null || CustomerPhoneLabel == null || CustomerAddressLabel == null) return;

            CustomerNameLabel.Text = PresentFormat("اسم العميل: {0}", cmbCustomerName.Text.Trim());
            CustomerPhoneLabel.Text = PresentFormat("هاتف العميل: {0}", cmbCustomerPhone.Text.Trim());
            CustomerAddressLabel.Text = PresentFormat("عنوان العميل: {0}", txtCustomerAddress.Text.Trim());
            if (CustomerIdLabel != null)
            {
                CustomerIdLabel.Text = string.IsNullOrWhiteSpace(_currentCustomerId)
                    ? (cmbCustomerPhone.Text.Trim() == "000000000" ? Present("رقم العميل: عام") : Present("رقم العميل: جديد"))
                    : PresentFormat("رقم العميل: {0}", _currentCustomerId);
            }

            UiTheme.ApplyInfoLabels(CustomerIdLabel, CustomerNameLabel, CustomerPhoneLabel, CustomerAddressLabel);
        }


        // عنصر مساعد لعرض اسم الموظف والاحتفاظ بالمعرف الداخلي.
        public class ComboBoxItem
        {
            // النص الظاهر في القائمة.
            public string Text { get; set; }

            // القيمة المرتبطة بالنص مثل رقم الموظف.
            public string Value { get; set; }

            // ينشئ عنصر قائمة جديدًا.
            public ComboBoxItem(string text, string value)
            {
                Text = text; // الاسم
                Value = value; // ID
            }

            // يعرض النص فقط داخل ComboBox.
            public override string ToString()
            {
                return Text; // يتم عرض الاسم فقط في ComboBox
            }
        }

        // يهيئ سلة الفاتورة الجديدة محليًا؛ لا يقرأ سجلات تاريخية من الشاشة.
        private void PopulateGrid(DataGridView BillingGV)
        {
            try
            {
                // مسح البيانات المعروضة في الجدول فقط
                BillingGV.Rows.Clear();savedUnitPresentation=null;acceptedUnitOrigin=null;
                Clear();
            }
            catch (Exception ex)
            {
                UiTheme.ShowOperationIssue(this, "عرض بنود الفاتورة", ex);
            }
        }


        // دالة لملء DataGridView بالفواتير
        // يهيئ أعمدة جدول الفاتورة.
        private void InitializeBillingGrid()
        {
            // مسح الأعمدة السابقة إذا كانت موجودة
            BillingGV.Columns.Clear();

            // إضافة الأعمدة مع رؤوس الأعمدة
            BillingGV.Columns.Add("BillId", "رمز");
            BillingGV.Columns.Add("MedName", "الصنف");
            BillingGV.Columns.Add("Sprice", "سعر الوحدة");
            BillingGV.Columns.Add("Quantity", "الكمية");
            BillingGV.Columns.Add("TotalAmount", "الإجمالي");
            DataGridViewButtonColumn removeColumn = new DataGridViewButtonColumn
            {
                Name = "RemoveCartItem",
                HeaderText = "إزالة",
                Text = presentationLeftToRight ? "Remove" : "إزالة",
                UseColumnTextForButtonValue = true,
                FlatStyle = FlatStyle.Flat,
                Width = 86,
                MinimumWidth = 78,
                ReadOnly = false
            };
            removeColumn.DefaultCellStyle.BackColor = UiTheme.WarningSurface;
            removeColumn.DefaultCellStyle.ForeColor = UiTheme.Warning;
            removeColumn.DefaultCellStyle.SelectionBackColor = UiTheme.WarningSurface;
            removeColumn.DefaultCellStyle.SelectionForeColor = UiTheme.Warning;
            BillingGV.Columns.Add(removeColumn);
            AddStockPreviewColumn();
            MedicineUnitPicker.EnsureColumn(BillingGV,presentationLeftToRight);
            BillingGV.ReadOnly = false;
            BillingGV.AllowUserToAddRows = false;
            BillingGV.AllowUserToDeleteRows = false;
            BillingGV.EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2;
            BillingGV.Columns["Quantity"].ToolTipText = presentationLeftToRight
                ? "Double-click or press F2 to edit quantity before saving."
                : "انقر مرتين أو اضغط F2 لتعديل الكمية قبل الحفظ.";
            BillingGV.CellBeginEdit -= BillingGV_CellBeginEdit;
            BillingGV.CellBeginEdit += BillingGV_CellBeginEdit;
            BillingGV.CellValidating -= BillingGV_CellValidating;
            BillingGV.CellValidating += BillingGV_CellValidating;
            BillingGV.CellEndEdit -= BillingGV_CellEndEdit;
            BillingGV.CellEndEdit += BillingGV_CellEndEdit;

            // تغيير الخط في رأس الجدول
            BillingGV.ColumnHeadersDefaultCellStyle.Font = AppFonts.Create(9.75F, FontStyle.Bold);

            // جعل الكتابة في النص لجميع الأعمدة
            foreach (DataGridViewColumn column in BillingGV.Columns)
            {
                column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                column.ReadOnly = column.Name != "RemoveCartItem" && column.Name != "Quantity";
            }

  // تحديد عرض الأعمدة
  BillingGV.Columns["BillId"].Width = 80;  // عرض عمود MedID
  BillingGV.Columns["MedName"].Width = 120;  // عرض عمود MedName أكبر من الأعمدة الأخرى
  BillingGV.Columns["Sprice"].Width = 100;  // عرض عمود S.P. OfOneMed
  BillingGV.Columns["Quantity"].Width = 80;  // عرض عمود Quantity
  BillingGV.Columns["TotalAmount"].Width = 120;  // عرض عمود TotalAmount
  // The money columns keep a stable edge and two decimals, so the digits do not jump.
  ApplyBillingMoneyColumnAlignment();
  // The cart shows an empty state with the single action that fills it.
  UiTheme.ApplyDataGridQuality(BillingGV, Present("السلة فارغة — امسح الباركود لإضافة أول صنف"));
  ApplyBillingGridPresentation();
        }




        // عند تغيير النص في TextBox (txtQuantity)

        // يتحقق من كمية الدواء عند تغيير الكمية المطلوبة.
        private bool CommitBillingCartEdit()
        {
            if (!BillingGV.IsCurrentCellInEditMode || BillingGV.CurrentCell == null) return true;
            var cell = BillingGV.CurrentCell;
            return ValidateBillingCartCell(cell.RowIndex, cell.ColumnIndex, cell.EditedFormattedValue) && BillingGV.EndEdit();
        }
        private void BillingGV_CellBeginEdit(object sender, DataGridViewCellCancelEventArgs e)
        {
            if (SalesRecoveryLocked || _isInvoiceSaved || _isInvoiceCancelled || _isSavingInvoice || medicineCartPending || invoicePrintSelectionPending)
                e.Cancel = true;
        }

        private void BillingGV_CellValidating(object sender, DataGridViewCellValidatingEventArgs e)
        {
            e.Cancel = !ValidateBillingCartCell(e.RowIndex, e.ColumnIndex, e.FormattedValue);
        }

        private bool ValidateBillingCartCell(int rowIndex, int columnIndex, object formattedValue)
        {
            if (rowIndex < 0 || BillingGV.Columns[columnIndex].Name != "Quantity") return true;
            DataGridViewRow row = BillingGV.Rows[rowIndex];
            string error = null;
            try
            {
                if(!MedicineUnitPicker.TryQuantity(Convert.ToString(formattedValue),out decimal quantity))
                    throw new InvalidOperationException(Present("أدخل كمية موجبة صالحة."));
                var previous=CartUnit(row); var unit=previous?.WithQuantity(quantity);
                if(unit==null && quantity!=decimal.Truncate(quantity))throw new InvalidOperationException(Present("أدخل كمية صحيحة للصنف بلا وحدات."));
                int baseQuantity=unit?.BaseQuantity??checked((int)quantity);
                decimal gross=unit?.GrossTotal??checked(Convert.ToDecimal(row.Cells["Sprice"].Value)*quantity);
                if(gross>9999999999999999.99m)throw new InvalidOperationException(Present("إجمالي البند أكبر من الحد المسموح."));
                if(row.Tag is int available && OtherCartBaseQuantity(Convert.ToString(row.Cells["MedName"].Value),row)+baseQuantity>available)
                    throw new InvalidOperationException(Present("مجموع كميات الصنف يتجاوز المخزون المتاح."));
            }
            catch(Exception ex) when(ex is InvalidOperationException||ex is ArgumentException||ex is OverflowException) { error=ex.Message; }
            row.Cells["Quantity"].ErrorText = error ?? string.Empty;
            if (error != null) UiTheme.SetStatus(lblStockStatus, error, false);
            return error == null;
        }

        private void BillingGV_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || BillingGV.Columns[e.ColumnIndex].Name != "Quantity") return;
            DataGridViewRow row = BillingGV.Rows[e.RowIndex];
            decimal quantity = Convert.ToDecimal(row.Cells["Quantity"].Value);
            var unit=CartUnit(row)?.WithQuantity(quantity); if(unit!=null)cartUnits[row]=unit;
            row.Cells["Quantity"].ErrorText = string.Empty;
            row.Cells["TotalAmount"].Value = unit?.GrossTotal??checked(Convert.ToDecimal(row.Cells["Sprice"].Value)*quantity);
            TrackTemporaryTransaction(Convert.ToString(row.Cells["MedName"].Value),unit?.BaseQuantity??checked((int)quantity));
            UpdateTotalAmount();
            UiTheme.SetStatus(lblStockStatus, presentationLeftToRight ? "Cart totals updated." : "تم تحديث إجماليات السلة.", true);
        }
        private void txtQuantity_TextChanged(object sender, EventArgs e)
        {
            if (MediComboCb.SelectedItem == null || _isInvoiceSaved || _isInvoiceCancelled) return;
            if (string.IsNullOrWhiteSpace(txtQuantity.Text))
            {
                UiTheme.SetStatus(lblStockStatus, Present("أدخل الكمية، ثم Enter للإضافة."));
                return;
            }
            if (!MedicineUnitPicker.TryQuantity(txtQuantity.Text, out decimal quantity))
            {
                UiTheme.SetStatus(lblStockStatus, Present("أدخل عدداً صحيحاً موجباً للكمية."), false);
                return;
            }
            if (!selectedStockPreview.HasValue)
            {
                UiTheme.SetStatus(lblStockStatus, Present("سيُتحقق من الرصيد عند الإضافة إلى السلة."));
                return;
            }
            int available = selectedStockPreview.Value;
            try { var unit=medicineUnitPicker?.SelectedUnit?.WithQuantity(quantity); if(unit!=null)quantity=unit.BaseQuantity; }
            catch(Exception error) when(error is ArgumentException||error is InvalidOperationException||error is OverflowException) { UiTheme.SetStatus(lblStockStatus,error.Message,false); return; }
            UiTheme.SetStatus(lblStockStatus,
                quantity > available ? PresentFormat("الكمية تتجاوز آخر رصيد معروض: {0}. عدّلها قبل الإضافة.", available)
                : PresentFormat("آخر رصيد معروض: {0} وحدة — Enter للإضافة.", available), quantity <= available);
        }

        private void txtQuantity_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter || e.Modifiers != Keys.None) return;
            e.SuppressKeyPress = true;
            if (!_isInvoiceSaved && !_isInvoiceCancelled && !_isSavingInvoice && btnAddToCart.Enabled)
                btnAddToCart.PerformClick();
        }
        // Print Button

        // قائمة مؤقتة لتمييز وجود سلة بيع غير محفوظة قبل إغلاق الشاشة أو مسح الفاتورة.
        private readonly List<(string Medicine, int Quantity)> tempTransaction = new List<(string Medicine, int Quantity)>();

        // معرّفات بنود الفاتورة الحالية في الجدول؛ تُستخدم لحذف مسودة الفاتورة دون مسح كل سجلّات BillsTb1.
        private readonly HashSet<string> _sessionBillLineIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private string _currentCustomerId = string.Empty;
        private string _creditCustomerId = string.Empty;
        private decimal _displayedCustomerCreditBalance;
        private Label lblCustomerCreditBalance;
        private Label lblCustomerCreditAvailability;
        private AntdUI.Checkbox chkUseCustomerCredit;
        private bool _isInvoiceSaved;
        private bool _isInvoiceCancelled;
        private bool _isSavingInvoice;
        private int _draftBillLineSequence;
        private decimal _appliedDiscountPercent;
        private long? _currentCommercialInvoiceNumber;
        private CommercialInvoicePrintSnapshot _currentCommercialInvoiceSnapshot;

        // عند الضغط على زر إضافة البيانات أو الطباعة
        // يطبع الفاتورة بعد تجهيز المعاملة وتحديث المخزون.
        private async void btnSaveOnly_Click(object sender, EventArgs e)
        {
if (BillingGV.Rows.Count == 0)
            {
                AppNotifications.Show(Present("لا توجد بيانات للحفظ."));
                return;
            }
            if (_isInvoiceCancelled)
            {
                AppNotifications.Show(Present("تم إلغاء هذه الفاتورة. افتح فاتورة جديدة؛ لا يمكن حفظها أو طباعتها كفاتورة بيع معتمدة."), Present("الفواتير"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (_isInvoiceSaved)
            {
                AppNotifications.Show(Present("تم حفظ الفاتورة بالفعل. افتح فاتورة جديدة قبل الحفظ مرة أخرى."), Present("الفواتير"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            try
            {
                if (!await SaveInvoiceToDatabaseAsync())
                {
                    return;
                }
                AppNotifications.Show(PresentFormat("تم الحفظ بنجاح.\nمرجع البيع: {0}", GetSavedSalesReference()), Present("تم"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                if (_isInvoiceSaved)
                {
                    ShowSavedInvoiceFollowUpFailure("تجهيز الفاتورة", ex);
                    return;
                }

                UiTheme.ShowOperationFailure(this, "حفظ الفاتورة", ex, preserveValidationMessage: true);
            }
        }

        private async void btnSaveAndPrint_Click(object sender, EventArgs e)
        {
            if(invoicePrintSelectionPending)return;
if (BillingGV.Rows.Count > 0) // التحقق من وجود بيانات للطباعة
            {
                try
                {
                    if (!await SaveInvoiceToDatabaseAsync())
                    {
                        return;
                    }

                    // إنشاء مستند للطباعة
                using(var selection=await ChooseInvoiceIdentityAsync())
                {
                    if(selection==null)return;invoicePrintSelection=selection;
                    try{
                using (PrintDocument printDoc = new PrintDocument())
                {
                    DeviceSettings.ApplyPreferredPrinter(printDoc);
                    bool useReceipt = DeviceSettings.IsReceiptInvoiceFormat();
                    printDoc.DefaultPageSettings.PaperSize = DeviceSettings.CreateInvoicePaperSize();
                        printDoc.DefaultPageSettings.Margins = useReceipt ? new Margins(5, 5, 5, 5) : new Margins(40, 40, 40, 40);
                        if (useReceipt) { printDoc.BeginPrint += BeginThermalPrint; printDoc.EndPrint += EndThermalPrint; printDoc.PrintPage += PrintSmallSalesReceipt; } else { printDoc.BeginPrint += BeginA4Print; printDoc.EndPrint += EndA4Print; printDoc.PrintPage += PrintPage; }
                        using (PrintPreviewDialog preview = new PrintPreviewDialog { Document = printDoc, Text = useReceipt ? "معاينة طباعة إيصال البيع" : "معاينة طباعة الفاتورة A4" })
                        {
                            ShowInvoicePreview(preview);
                        }
                        if (useReceipt) printDoc.PrintPage -= PrintSmallSalesReceipt; else printDoc.PrintPage -= PrintPage;
                    }
                    }finally{invoicePrintSelection=null;ReleaseInvoicePrintJobs();}
                }
                }
            catch (Exception ex)
            {
                ShowInvoiceOutputFailure("الطباعة", ex);
            }
            }
            else
            {
                AppNotifications.Show(Present("لا توجد بيانات للطباعة."));
            }
        }

        private async void btnSaveAndPrintA4_Click(object sender, EventArgs e)
        {
            if(invoicePrintSelectionPending)return;
if (BillingGV.Rows.Count == 0)
            {
                AppNotifications.Show(Present("لا توجد بيانات للطباعة."));
                return;
            }

            try
            {
                if (!await SaveInvoiceToDatabaseAsync())
                {
                    return;
                }

                using(var selection=await ChooseInvoiceIdentityAsync())
                {
                    if(selection==null)return;invoicePrintSelection=selection;
                    try{
                using (PrintDocument printDoc = new PrintDocument())
                {
                    DeviceSettings.ApplyPreferredPrinter(printDoc);
                    printDoc.DefaultPageSettings.PaperSize = DeviceSettings.CreateA4PaperSize();
                    printDoc.DefaultPageSettings.Margins = new Margins(40, 40, 40, 40);
                    printDoc.BeginPrint += BeginA4Print; printDoc.EndPrint += EndA4Print;
                    printDoc.PrintPage += PrintPage;
                    using (PrintPreviewDialog preview = new PrintPreviewDialog { Document = printDoc, Text=Present("معاينة طباعة الفاتورة A4") })
                    {
                        ShowInvoicePreview(preview);
                    }
                    printDoc.PrintPage -= PrintPage;
                }
                    }finally{invoicePrintSelection=null;ReleaseInvoicePrintJobs();}
                }
            }
            catch (Exception ex)
            {
                ShowInvoiceOutputFailure("طباعة A4", ex);
            }
        }

        private void AddSavePdfButton()
        {
            if (flowFooterButtons.Controls.ContainsKey("btnSavePdf"))
            {
                return;
            }

            AccessibleAntdButton btnSavePdf = new AccessibleAntdButton
            {
                Name = "btnSavePdf",
                Text=Present("حفظ PDF"),
                Size = new Size(155, 42),
                Margin = new Padding(8, 0, 8, 0),
                Cursor = Cursors.Hand
            };

            btnSavePdf.Click += btnSavePdf_Click;
            flowFooterButtons.Controls.Add(btnSavePdf);
            flowFooterButtons.Controls.SetChildIndex(btnSavePdf, Math.Min(3, flowFooterButtons.Controls.Count - 1));
            CommerceWorkspaceTheme.ApplySecondaryAction(btnSavePdf);
        }

        private void AddCancelSavedInvoiceButton()
        {
            if (flowFooterButtons.Controls.ContainsKey("btnCancelSavedInvoice"))
            {
                return;
            }

            AccessibleAntdButton cancelButton = new AccessibleAntdButton
            {
                Name = "btnCancelSavedInvoice",
                Text=Present("إلغاء الفاتورة"),
                Size = new Size(155, 42),
                Margin = new Padding(8, 0, 8, 0),
                Cursor = Cursors.Hand,
                Enabled = false,
                AccessibleDescription = "يعكس الفاتورة الحالية فقط مع المخزون والقيد المحاسبي."
            };
            cancelButton.Click += btnCancelSavedInvoice_Click;
            flowFooterButtons.Controls.Add(cancelButton);
            flowFooterButtons.Controls.SetChildIndex(cancelButton, Math.Min(4, flowFooterButtons.Controls.Count - 1));
            CommerceWorkspaceTheme.ApplySecondaryAction(cancelButton);
        }

        private void SetCancelSavedInvoiceEnabled(bool enabled)
        {
            Control cancelButton = flowFooterButtons.Controls["btnCancelSavedInvoice"];
            if (cancelButton != null)
            {
                cancelButton.Enabled = enabled;
            }
        }

        private void btnCancelSavedInvoice_Click(object sender, EventArgs e)
        {
            if (!PermissionService.EnsureAllowed(AppOperation.CancelSalesInvoice, this))
            {
                return;
            }

            if (_isInvoiceCancelled)
            {
                AppNotifications.Show(Present("هذه الفاتورة ملغاة مسبقاً، ولا يمكن تكرار الإلغاء."), Present("إلغاء الفاتورة"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (!_isInvoiceSaved)
            {
                AppNotifications.Show(Present("احفظ الفاتورة أولاً أو افتح فاتورة معتمدة."), Present("إلغاء الفاتورة"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (_currentCommercialInvoiceNumber.HasValue)
            {
                AppNotifications.Show(Present("الفاتورة التجارية لا تُلغى من هذه الشاشة. استخدم إشعار دائن أو مسار الإلغاء التجاري المعتمد."), Present("إلغاء الفاتورة"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            List<string> billIds = GetCurrentLegacyBillLineIds();
            if (billIds.Count == 0)
            {
                AppNotifications.Show(Present("تعذر تحديد بنود الفاتورة الحالية لإلغائها."), Present("إلغاء الفاتورة"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            DialogResult confirmation = AppNotifications.Show(
                Present("سيتم عكس الفاتورة الحالية فقط: إعادة المخزون وإنشاء قيد محاسبي معاكس.\nلن تُحذف السجلات الأصلية.\n\nهل تريد المتابعة؟"), Present("تأكيد إلغاء الفاتورة"),
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);
            if (confirmation != DialogResult.Yes)
            {
                return;
            }

            SalesInvoiceCancellationOutcome completed = null;
            try
            {
                completed = salesCancellationOperation.Cancel(new SalesInvoiceCancellationRequest
                {
                    BillLineIds = billIds,
                    Reason = "إلغاء من شاشة نقطة البيع",
                    ActorName = SessionContext.CurrentUser
                });
                _isInvoiceCancelled = true;
                SetCancelSavedInvoiceEnabled(false);
                UiTheme.SetStatus(lblStockStatus, PresentFormat("تم إلغاء الفاتورة وإعادة {0} وحدة إلى المخزون.", completed.RestoredQuantity), true);
                AppNotifications.Show(
                    PresentFormat("تم إلغاء الفاتورة بأمان.\nالمرجع: {0}\nالمبلغ المعكوس: {1}\nالكمية المعادة: {2}", completed.OriginalInvoiceReference, FormatMoney(completed.TotalAmount), completed.RestoredQuantity),
                    Present("تم الإلغاء"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception exception)
            {
                if (completed == null)
                    UiTheme.ShowOperationFailure(this, "إلغاء الفاتورة", exception, preserveValidationMessage: true);
                else
                    UiTheme.ShowCompletedOperationFollowUpFailure(this, "إلغاء الفاتورة", completed.OriginalInvoiceReference, exception);
            }
        }

        private List<string> GetCurrentLegacyBillLineIds()
        {
            List<string> ids = new List<string>();
            foreach (DataGridViewRow row in BillingGV.Rows)
            {
                if (row.IsNewRow) continue;
                string id = Convert.ToString(row.Cells["BillId"].Value);
                if (!string.IsNullOrWhiteSpace(id) && !id.StartsWith("DRAFT-", StringComparison.OrdinalIgnoreCase) && !id.StartsWith("C-", StringComparison.OrdinalIgnoreCase))
                {
                    ids.Add(id.Trim());
                }
            }
            return ids.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private async void btnSavePdf_Click(object sender, EventArgs e)
        {
            if(invoicePrintSelectionPending)return;
if (BillingGV.Rows.Count == 0)
            {
                AppNotifications.Show(Present("لا توجد بيانات لحفظ PDF."));
                return;
            }

            try
            {
                if (!await SaveInvoiceToDatabaseAsync())
                {
                    return;
                }

                using(var selection=await ChooseInvoiceIdentityAsync())
                {
                    if(selection==null)return;invoicePrintSelection=selection;
                    try{
                using (PrintDocument printDoc = new PrintDocument())
                {
                    DeviceSettings.ApplyPreferredPrinter(printDoc);
                    printDoc.DefaultPageSettings.PaperSize = new PaperSize("A4", 827, 1169);
                    printDoc.DefaultPageSettings.Margins = new Margins(40, 40, 40, 40);
                    printDoc.BeginPrint += BeginA4Print; printDoc.EndPrint += EndA4Print;
                    printDoc.PrintPage += PrintPage;
                    await PdfExportHelper.SaveAsPdfAsync(printDoc, $"Sales-Invoice-{GetSavedSalesReference()}-{DateTime.Now:yyyyMMdd-HHmm}.pdf", this, presentationLeftToRight, ()=>selection.CanUse);
                    printDoc.PrintPage -= PrintPage;
                }
                    }finally{invoicePrintSelection=null;ReleaseInvoicePrintJobs();}
                }
            }
            catch (Exception ex)
            {
                ShowInvoiceOutputFailure("حفظ PDF", ex);
            }
        }

        private void SetBillingStatusFailure(string operation, Exception exception)
        {
            string supportCode = AppDiagnostics.Log("Billing:" + operation, exception);
            UiTheme.SetStatus(lblStockStatus, string.Format(Present("تعذر {0}. رمز الدعم: {1}"), Present(operation), supportCode), false);
        }

        private void ShowInvoiceOutputFailure(string outputName, Exception exception)
        {
            if (_isInvoiceCancelled)
            {
                AppNotifications.Show(
                    PresentFormat("تم إلغاء الفاتورة الحالية، لذلك لا يمكن {0} كفاتورة بيع معتمدة. افتح فاتورة جديدة.", Present(outputName)),
                    Present("فاتورة ملغاة"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            if (!_isInvoiceSaved)
            {
                UiTheme.ShowOperationFailure(this, outputName, exception, preserveValidationMessage: true);
                return;
            }

            ShowSavedInvoiceFollowUpFailure(outputName, exception);
        }

        private void ShowSavedInvoiceFollowUpFailure(string actionName, Exception exception)
    {
        // The invoice is already committed; a follow-up print failure is a warning, not a save error.
        // It must not block the cashier with a modal dialog or a support code on every sale.
        AppDiagnostics.Log("BillingOutput", exception);
        UiTheme.SetFormStatus(this, Present("تم اعتماد الفاتورة. تعذر إكمال الطباعة الآن — يمكنك إعادة الطباعة من السجل."), false);
    }

        private void PrintSmallSalesReceipt(object sender, PrintPageEventArgs e)
        {
            if(invoicePrintSelection!=null&&!invoicePrintSelection.Guard(e))return;
            if (thermalPrintJob == null) thermalPrintJob = CaptureThermalPrintJob();
            thermalPrintJob.PrintPage(sender, e);
            if(invoicePrintSelection!=null)invoicePrintSelection.Guard(e);
        }

        internal InvoicePrintHeader CaptureInvoicePrintHeader()
        {
            return InvoicePrintHeader.Sales(_currentCommercialInvoiceSnapshot, CustomerIdLabel.Text,
                CustomerNameLabel.Text, CustomerPhoneLabel.Text, CustomerAddressLabel.Text,
                EmployeeIdLabel.Text, EmployeeLabel.Text, TimingLabel.Text);
        }
        private string GetSavedSalesReference()
        {
            if (_currentCommercialInvoiceNumber.HasValue)
            {
                return "C-" + _currentCommercialInvoiceNumber.Value;
            }
            foreach (DataGridViewRow row in BillingGV.Rows)
            {
                if (row.IsNewRow) continue;
                string value = Convert.ToString(row.Cells["BillId"].Value);
                if (!string.IsNullOrWhiteSpace(value) && !value.StartsWith("DRAFT-", StringComparison.OrdinalIgnoreCase))
                {
                    return value;
                }
            }
            return "غير معتمد";
        }

        private string FormatPrintedMoney(decimal value)
        {
            return value.ToString("N2") + " " + PrintedCurrencyText;
        }



        // يمسح حقول الفاتورة والعميل.
        private void Clear()
        {
            if(salesPending!=null||salesRecoveryStale)return;
            CancelCustomerReads();
            _currentCustomerId = string.Empty;
            _currentCommercialInvoiceNumber = null;
            _currentCommercialInvoiceSnapshot = null;
            _isInvoiceSaved = false;
            btnSaveOnly.Enabled = true;
            btnSaveOnly.AccessibleDescription = "حفظ الفاتورة الحالية دون طباعة.";
            _isInvoiceCancelled = false;
            SetCancelSavedInvoiceEnabled(false);
            _appliedDiscountPercent = 0m;
            tempTransaction.Clear();
            _sessionBillLineIds.Clear();
            _draftBillLineSequence = 0;
            BillingGV.ReadOnly = false;
            PriceBDLabel.Text = Present("الإجمالي") + ": " + FormatMoney(0);
            PriceADLabel.Text = Present("الصافي") + ": " + FormatMoney(0);
            txtDiscountPercent.Clear();
            CustomerNameLabel.Text = Present("اسم العميل:");
            CustomerPhoneLabel.Text = Present("هاتف العميل:");
            CustomerAddressLabel.Text = Present("عنوان العميل:");
            EmployeeLabel.Text = Present("اسم الكاشير:");
            EmployeeIdLabel.Text = Present("رقم الكاشير:");
            CustomerIdLabel.Text = Present("رقم العميل:");
            TimingLabel.Text = Present("التوقيت:");
            cmbCustomerName.SelectedIndex = -1;
            cmbCustomerName.Text = "";
            cmbCustomerPhone.Text = "";
            txtCustomerAddress.Clear();
            ApplyDefaultCustomer();
            ApplyCurrentCashier();
            ResetDraftCurrency();
        }




        // ينبه المستخدم عند إغلاق شاشة البيع وفيها سلة غير محفوظة.
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if(salesRecoveryBusy){e.Cancel=true;return;}if(salesPending!=null||salesRecoveryFault||salesRecoveryStale)return;
            if (!_isInvoiceSaved && tempTransaction.Count > 0)
            {
                DialogResult result = AppNotifications.Show(
                    Present("هل أنت متأكد أنك تريد الخروج؟ سيتم إلغاء سلة البيع غير المحفوظة."), Present("تأكيد الخروج"),
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    RollbackTransaction();
                }
                else
                {
                    e.Cancel = true;
                }
            }
        }

       

        // يرسم محتوى الفاتورة على صفحة الطباعة.
        // يرسم محتوى الفاتورة على صفحة الطباعة بشكل احترافي وجميل للغاية.
        private void PrintPage(object sender, PrintPageEventArgs e)
        {
            if(invoicePrintSelection!=null&&!invoicePrintSelection.Guard(e))return;
            if (a4PrintJob == null || a4PrintJob.Completed) a4PrintJob = CaptureA4PrintJob();
            var job = a4PrintJob;
            SalesA4PrintLayout.PrintPage(job,job.Identity,e);
            if(invoicePrintSelection!=null)invoicePrintSelection.Guard(e);
        }






      


        // حدث تغيير التاريخ، متروك للتوسعة المستقبلية.
        private void ExpDate_ValueChanged(object sender, EventArgs e)
        {
        }



        // يحمل بيانات العميل من اسمه.
        // حدث اختيار اسم العميل من القائمة — يملأ الهاتف والعنوان تلقائياً.
        private async void cmbCustomerName_SelectedIndexChanged(object sender,EventArgs e)
        {
            if(customerApplying!=0||cmbCustomerName.SelectedItem==null)return;
            await SelectCustomerAsync(CustomerReferenceKind.ByName,cmbCustomerName.SelectedItem.ToString());
        }



        // يعرض بيانات العميل والموظف على الفاتورة قبل الحفظ.
        private void btnConfirmCustomer_Click(object sender, EventArgs e)
        {
            try
            {
                // التحقق من أن جميع الحقول تم تعبئتها (البيانات الأساسية للعميل)
                ApplyDefaultCustomer();

                if (string.IsNullOrWhiteSpace(_currentCustomerId) && cmbCustomerPhone.Text.Trim() != "000000000")
                {
                    AppNotifications.Show(Present("اختر عميلاً مسجلاً أو أضفه من شاشة إدارة العملاء والموردين أولاً."), Present("بيانات العميل"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                    OpenCustomerManagement();
                    return;
                }

                if (string.IsNullOrWhiteSpace(cmbCustomerName.Text) || // Customer Name
                    string.IsNullOrWhiteSpace(cmbCustomerPhone.Text) || // Customer Phone
                    string.IsNullOrWhiteSpace(txtCustomerAddress.Text)) // Customer Address
                {
                    AppNotifications.Show(Present("يرجى تعبئة بيانات العميل قبل الاعتماد."));
                    return;
                }

                ApplyCurrentCashier();

                RefreshCustomerSummary();
                TimingLabel.Text = FormatSalesTiming(DateTime.Now);

                // تنسيق مربعات معلومات العميل حسب الوضع اللوني الحالي.
                UiTheme.ApplyInfoLabels(
                    CustomerIdLabel,
                    CustomerNameLabel,
                    CustomerPhoneLabel,
                    CustomerAddressLabel,
                    TimingLabel);

                // عرض اسم الموظف و ID الموظف بعد إضافة بيانات العميل
                if (SelectEmployee.SelectedValue is ComboBoxItem selectedItem)
                {
                    EmployeeLabel.Text = PresentFormat("اسم الكاشير: {0}", selectedItem.Text);

                    // عرض EmployeeId الخاص بالموظف
                    EmployeeIdLabel.Text = PresentFormat("رقم الكاشير: {0}", selectedItem.Value);
                    UiTheme.ApplyInfoLabels(EmployeeLabel, EmployeeIdLabel);
                }
                else
                {
                    EmployeeLabel.Text = Present("الكاشير غير محدد.");
                    EmployeeIdLabel.Text = string.Empty; // تفريغ الـ Label الخاص بالـ ID
                }

                AppNotifications.Show(Present("تم اعتماد بيانات العميل بنجاح."), Present("اعتماد"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                UiTheme.ShowOperationIssue(this, "اعتماد بيانات العميل", ex);
            }
        }



    













        // دالة لتحديث المجموع الإجمالي
        // يعيد حساب إجمالي الفاتورة.
        private decimal CalculateGrossTotal()
        {
            decimal total = 0m;
            foreach (DataGridViewRow row in BillingGV.Rows)
            {
                if (row.IsNewRow || row.Cells["TotalAmount"].Value == null)
                {
                    continue;
                }

                total = decimal.Add(total, Convert.ToDecimal(row.Cells["TotalAmount"].Value));
            }

            return total;
        }

        private decimal CalculateNetTotal(decimal grossTotal)
        {
            decimal discountAmount = decimal.Round(
                grossTotal * _appliedDiscountPercent / 100m,
                2,
                MidpointRounding.AwayFromZero);
            return grossTotal - discountAmount;
        }

        private void UpdateTotalAmount()
        {
            try
            {
                decimal totalAmount = CalculateGrossTotal();

                // عرض المجموع في PriceBDLabel
                PriceBDLabel.Text = Present("الإجمالي") + ": " + FormatMoney(totalAmount); // عرض المجموع الإجمالي
                UiTheme.ApplyInfoLabels(PriceBDLabel);

                // تحديث الخصم بعد تحديث المجموع الإجمالي
                UpdateDiscount(totalAmount);
            }
            catch (Exception ex)
            {
                UiTheme.ShowOperationIssue(this, "حساب إجمالي الفاتورة", ex);
            }
        }

        // يحدث قيمة الخصم والإجمالي بعد الخصم.
        private void UpdateDiscount(decimal totalAmount)
        {
            try
            {
                decimal amountAfterDiscount = CalculateNetTotal(totalAmount);
                if (_appliedDiscountPercent > 0)
                {
                    PriceADLabel.Text = Present("الصافي") + ": " + FormatMoney(amountAfterDiscount); // عرض المجموع بعد الخصم
                    UiTheme.ApplyInfoLabels(PriceADLabel);
                }
                else
                {
                    // إذا لم يكن هناك خصم أو الخصم غير صالح
                    PriceADLabel.Text = Present("الصافي") + ": " + FormatMoney(totalAmount); // عرض المجموع الإجمالي بدون خصم
                    UiTheme.ApplyInfoLabels(PriceADLabel);
                }
            }
            catch (Exception ex)
            {
                UiTheme.ShowOperationIssue(this, "تحديث الخصم", ex);
            }
        }


        // يطبق الخصم المدخل على إجمالي الفاتورة.
        private void btnApplyDiscount_Click(object sender, EventArgs e)
        {
            try
            {
                if (_isInvoiceSaved)
                {
                    AppNotifications.Show(Present("تم حفظ الفاتورة بالفعل. لا يمكن تعديل الخصم بعد الحفظ."), Present("الخصم"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // تثبيت نسبة الخصم كقيمة رقمية، لا كنص واجهة قابل للتنسيق أو المسح.
                if (decimal.TryParse(txtDiscountPercent.Text, out decimal discountPercentage) && discountPercentage >= 0 && discountPercentage < 100)
                {
                    decimal totalAmount = CalculateGrossTotal();

                    // إذا كان المجموع الإجمالي أكبر من 0
                    if (totalAmount > 0)
                    {
                        _appliedDiscountPercent = discountPercentage;
                        UpdateTotalAmount();
                    }
                    else
                    {
                        // إذا كان المجموع الإجمالي يساوي 0، عرض رسالة
                        AppNotifications.Show(Present("إجمالي الفاتورة يجب أن يكون أكبر من صفر."));
                    }

                }
                else
                {
                    // إذا كانت قيمة الخصم المدخلة غير صالحة
                    AppNotifications.Show(Present("يرجى إدخال نسبة خصم صحيحة أقل من 100%."));
                }
            }
            catch (Exception ex)
            {
                UiTheme.ShowOperationIssue(this, "تطبيق الخصم", ex);
            }
        }









        // يمسح الفاتورة الحالية بعد تأكيد المستخدم.
        private void btnClearInvoice_Click(object sender, EventArgs e)
        {
            if(SalesRecoveryLocked)return;
            if (BillingGV.Rows.Count == 0)
            {
                ResetForm();
                return;
            }

            if (_isInvoiceSaved)
            {
                ResetForm();
                AppNotifications.Show(Present("تم فتح فاتورة بيع جديدة."), Present("المبيعات"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            DialogResult dialogResult = AppNotifications.Show(Present("هل تريد مسح الفاتورة الحالية؟ لم يتم تعديل المخزون قبل الحفظ."), Present("تأكيد مسح الفاتورة"), MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (dialogResult == DialogResult.Yes)
            {
                try
                {
                    RollbackTransaction();

                    // مسح النصوص والبيانات المعروضة على الواجهة
                    ResetForm();

                    // إعادة تخصيص الجدول بناءً على وجود أو عدم وجود بيانات
                    CustomizeDataGridView();

                    AppNotifications.Show(Present("تم مسح الفاتورة."));
                }
                catch (Exception ex)
                {
                UiTheme.ShowOperationIssue(this, "مسح الفاتورة", ex);
                }
            }
            else
            {
                AppNotifications.Show(Present("تم إلغاء عملية المسح."));
            }
        }

        // يلغي سلة البيع غير المحفوظة. المخزون لا يتغير قبل الحفظ النهائي.
        private void RollbackTransaction()
        {
            tempTransaction.Clear();
            _sessionBillLineIds.Clear();
        }

        // يعيد الشاشة إلى حالتها الافتراضية.
        private void ResetForm()
        {
            if(SalesRecoveryLocked)return;
            CancelCustomerReads();
            _currentCustomerId = string.Empty;
            _currentCommercialInvoiceNumber = null;
            _currentCommercialInvoiceSnapshot = null;
            _isInvoiceSaved = false;
            _isInvoiceCancelled = false;
            SetCancelSavedInvoiceEnabled(false);
            _appliedDiscountPercent = 0m;
            tempTransaction.Clear();
            _sessionBillLineIds.Clear();
            _draftBillLineSequence = 0;
            BillingGV.ReadOnly = false;
            // مسح جميع الحقول والبيانات المعروضة في الواجهة
            txtDiscountPercent.Clear();
            ResetQuantityToDefault();
            BillingGV.Rows.Clear();savedUnitPresentation=null;acceptedUnitOrigin=null;
            CustomerNameLabel.Text = Present("اسم العميل:");
            CustomerPhoneLabel.Text = Present("هاتف العميل:");
            CustomerAddressLabel.Text = Present("عنوان العميل:");
            EmployeeLabel.Text = Present("اسم الكاشير:");
            EmployeeIdLabel.Text = Present("رقم الكاشير:");
            CustomerIdLabel.Text = Present("رقم العميل:");
            TimingLabel.Text = Present("التوقيت:");
            cmbCustomerName.SelectedIndex = -1;
            cmbCustomerName.Text = "";
            cmbCustomerPhone.Text = "";
            txtCustomerAddress.Clear();
            ApplyDefaultCustomer();
            ApplyCurrentCashier();
            MediComboCb.Text = V;
            lblStockStatus.Text = Present("المخزون المتاح — اختر صنفاً أولاً");
            UiTheme.ApplyInfoLabels(
                CustomerNameLabel,
                CustomerPhoneLabel,
                CustomerAddressLabel,
                EmployeeLabel,
                EmployeeIdLabel,
                CustomerIdLabel,
                TimingLabel,
                PriceBDLabel,
                PriceADLabel);
            UiTheme.SetStatus(lblStockStatus, lblStockStatus.Text);
            ResetDraftCurrency();
        }

        // يحذف دواء من سلة الفاتورة قبل الحفظ.
        private void BillingGV_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            // الحذف لا ينفذ إلا من زر صريح؛ النقر على الصف يبقى آمناً للاستعراض.
            if (e.RowIndex >= 0 && e.ColumnIndex >= 0 && BillingGV.Columns[e.ColumnIndex].Name == "RemoveCartItem")
            {
                if (_isInvoiceSaved)
                {
                    AppNotifications.Show(Present("تم حفظ الفاتورة بالفعل. افتح فاتورة جديدة لتعديل السلة."), Present("الفواتير"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // الحصول على البيانات من الصف المحدد
                string medName = BillingGV.Rows[e.RowIndex].Cells["MedName"].Value.ToString(); // اسم الدواء
                decimal quantity = Convert.ToDecimal(BillingGV.Rows[e.RowIndex].Cells["Quantity"].Value); // الكمية

                // عرض رسالة تأكيد للمستخدم قبل حذف الصف
                DialogResult dialogResult = AppNotifications.Show($"هل تريد حذف {quantity} من {medName} من السلة؟",
                                                            "تأكيد حذف الصنف", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (dialogResult == DialogResult.Yes)
                {
                    try
                    {
                        BillingGV.Rows.RemoveAt(e.RowIndex);
                        RemoveTemporaryTransaction(medName);

                        CustomizeDataGridView();
                        UpdateTotalAmount();
                        AppNotifications.Show(Present("تم حذف الصنف من السلة."));
                    }
                    catch (Exception ex)
                    {
                        UiTheme.ShowOperationFailure(this, "حذف الصنف من السلة", ex);
                    }
                }
            }
        }

        // يضبط مظهر جدول الفاتورة.
        private void CustomizeDataGridView()
        {
            UiTheme.ApplyTo(BillingGV);
            CommerceWorkspaceTheme.StyleGrid(BillingGV);
        }








        // QA-only compatibility contract. Ordinary POS uses the durable identified recovery boundary.
        internal SalesInvoiceIssueRequest PrepareQaVerificationSalesRequest(string medicine, int price, string temporaryDatabaseOwnerToken)
        {
            QaSalesInvoiceSelfTests.RequireOwnedTemporaryQa(temporaryDatabaseOwnerToken);
            currencySnapshot = currencyReadService.GetSystemCurrencySnapshotAsync(MedicineReadActor(), CancellationToken.None).GetAwaiter().GetResult();
            currencyActor = MedicineReadActor(); currencyEmployee = SessionContext.CurrentEmployeeId; currencyConnectionIsCurrent = CaptureMedicineReadContext(currencyActor, currencyEmployee);
            InitializeBillingGrid();
            _appliedDiscountPercent = 5m;
            BillingGV.Rows.Add("DRAFT-QA", medicine, price, 1, price);
            return CreateSalesIssueRequest(null, "QA invoice verification", "000000000", "", CalculateNetTotal(price),
                SessionContext.CurrentUser, DateTime.Now, "QA currency change verification");
        }
        internal string SaveQaVerificationInvoice(string medicine, int price, string temporaryDatabaseOwnerToken)
        {
            PrepareQaVerificationSalesRequest(medicine, price, temporaryDatabaseOwnerToken);
            SaveDetailsToDatabase(null, "QA invoice verification", "000000000", "", CalculateNetTotal(price),
                SessionContext.CurrentUser, DateTime.Now, "QA plan verification: retain audit trail");
            if (!_isInvoiceSaved) throw new InvalidOperationException("QA invoice was not saved.");
            return Convert.ToString(BillingGV.Rows[0].Cells["BillId"].Value);
        }
        // يحفظ الفاتورة عبر حد عملية مستقل عن WinForms؛ تبقى الشاشة مسؤولة عن العرض فقط.
        internal SalesInvoiceIssueRequest CreateSalesIssueRequest(string customerId, string customerName, string phoneNumber, string customerAddress,
            decimal totalAmount, string employeeName, DateTime invoiceDate, string invoiceNotes)
        {
            var rows = BillingGV.Rows.Cast<DataGridViewRow>().Where(row => !row.IsNewRow).ToList();
            var lines = rows.Select(row => new SalesInvoiceLineRequest
            {
                MedicineName = Convert.ToString(row.Cells["MedName"].Value),
                Unit = CartUnit(row),
                DisplayedSellingPrice = CartUnit(row)==null?Convert.ToInt32(row.Cells["Sprice"].Value ?? 0):0,
                Quantity = CartUnit(row)?.BaseQuantity??Convert.ToInt32(row.Cells["Quantity"].Value ?? 0)
            }).ToList();
            return new SalesInvoiceIssueRequest
            {
                ExpectedCurrencyCode = RequireCurrencySnapshot().CurrencyCode,
                CustomerId = customerId,
                CustomerName = customerName,
                PhoneNumber = phoneNumber,
                CustomerAddress = customerAddress,
                EmployeeName = employeeName,
                CashierEmployeeId = (SelectEmployee.SelectedValue as ComboBoxItem)?.Value,
                Notes = invoiceNotes,
                InvoiceDate = invoiceDate,
                DisplayedGrossTotal = InvoiceTotalsCalculator.Round(CalculateGrossTotal()),
                DisplayedNetTotal = totalAmount,
                DiscountPercent = _appliedDiscountPercent,
                UseCustomerCredit = chkUseCustomerCredit != null && chkUseCustomerCredit.Checked,
                CreditCustomerId = _creditCustomerId,
                Lines = lines
            };
        }

        private void SaveDetailsToDatabase(string customerId, string customerName, string phoneNumber, string customerAddress, decimal totalAmount, string employeeName, DateTime invoiceDate, string invoiceNotes = "")
        {
            var rows = BillingGV.Rows.Cast<DataGridViewRow>().Where(row => !row.IsNewRow).ToList();
            var request = CreateSalesIssueRequest(customerId, customerName, phoneNumber, customerAddress, totalAmount, employeeName, invoiceDate, invoiceNotes);

            SalesInvoiceIssueResult result;
            try
            {
                result = ApplicationServices.SalesInvoices.Issue(request);
            }
            catch (Exception exception)
            {
                _sessionBillLineIds.Clear();
                throw new InvalidOperationException("تعذر حفظ الفاتورة: " + exception.Message, exception);
            }

            _isInvoiceSaved = true;
            savedLegacyCurrencyText = currencySnapshot.DisplayText;
            btnSaveOnly.Enabled = false;
            btnSaveOnly.AccessibleDescription = "تم حفظ الفاتورة؛ افتح فاتورة جديدة قبل الحفظ مرة أخرى.";
            _currentCustomerId = result.CustomerId;
            CustomerIdLabel.Text = PresentFormat("رقم العميل: {0}", _currentCustomerId);
            UpdateCustomerTypeIndicator();
            tempTransaction.Clear();
            BillingGV.ReadOnly = true;

            if (result.IsCommercial)
            {
                _currentCommercialInvoiceNumber = result.CommercialInvoiceNumber;
                ApplyCommittedCustomerCredit(result.CustomerCreditApplied);
                string displayId = "C-" + result.CommercialInvoiceNumber;
                foreach (DataGridViewRow row in rows) row.Cells["BillId"].Value = displayId;
                InvoiceInventoryAlertPresenter.Refresh(lblStockStatus, BillingGV, displayId,
                    () => _isInvoiceSaved && !_isInvoiceCancelled && _currentCommercialInvoiceNumber == result.CommercialInvoiceNumber);
                try
                {
                    _currentCommercialInvoiceSnapshot = CommercialInvoiceSnapshotRepository.LoadIssued(result.CommercialInvoiceId.Value);
                    acceptedUnitOrigin=new MedicineUnitPresentationContext(GetSavedSalesReference(),SavedUnitRowIdentity());
                    savedUnitPresentation=new Dictionary<string,MedicineUnitSelection>();
                }
                catch (Exception snapshotException)
                {
                    _currentCommercialInvoiceSnapshot = null;
                    throw new InvalidOperationException("تم اعتماد الفاتورة التجارية رقم " + displayId +
                        "، لكن تعذر التحقق من لقطة الطباعة المعتمدة. الطباعة محظورة؛ أعد فتح الفاتورة من سجل الفواتير بعد استعادة اتصال قاعدة البيانات.", snapshotException);
                }
                return;
            }

            _sessionBillLineIds.Clear();
            for (int index = 0; index < result.BillLineIds.Count; index++)
            {
                _sessionBillLineIds.Add(result.BillLineIds[index]);
                rows[index].Cells["BillId"].Value = result.BillLineIds[index];
            }
            LoadSavedUnitPresentation();
            InvoiceInventoryAlertPresenter.Refresh(lblStockStatus, BillingGV, result.AccountingSourceId,
                () => _isInvoiceSaved && !_isInvoiceCancelled && _currentCustomerId == result.CustomerId);
            SetCancelSavedInvoiceEnabled(true);
        }

        // دالة لاستخراج القيمة بعد العلامة ":"
        // يستخرج القيمة بعد علامة النقطتين من النصوص المعروضة.
        private string ExtractValue(string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            // تقسيم النص بناءً على ":"
            string[] parts = input.Split(':');
            if (parts.Length > 1)
            {
                return parts[1].Trim(); // القيمة بعد ":"
            }

            return input; // إذا لم يكن النص يحتوي على ":"
        }

        // دالة لاستخراج اسم الموظف بعد "Employee Name:"

        // يستخرج اسم الموظف من نص الليبل.
        private string ExtractEmployeeName(string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            // تقسيم النص بناءً على "Employee Name:" فقط
            string[] parts = input.Split(':');
            if (parts.Length > 1)
            {
                return parts[1].Trim(); // القيمة بعد ":" ستكون اسم الموظف
            }

            return input; // إذا لم يحتوي النص على "Employee Name:"، يتم إرجاع النص كما هو
        }

        // دالة لاستخراج رقم الهاتف بعد "Customer Phone:"
        // يستخرج رقم هاتف العميل من نص الليبل.
        private string ExtractCustomerPhone(string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            // تقسيم النص بناءً على "Customer Phone:"
            string[] parts = input.Split(':');
            if (parts.Length > 1)
            {
                return parts[1].Trim(); // القيمة بعد ":"
            }

            return input; // إذا لم يكن النص يحتوي على ":"
        }




        // يملأ بيانات العميل بناءً على رقم الهاتف.



        // يملأ بيانات العميل عند مغادرة حقل الهاتف.
        private async void cmbCustomerPhone_Leave(object sender,EventArgs e)
        {
            if(customerApplying!=0||customerSelectionLoading)return;
            await SelectCustomerAsync(CustomerReferenceKind.ByPhone,cmbCustomerPhone.Text.Trim());
        }


        // يحمل أسماء العملاء السابقين إلى ComboBox اسم العميل.


        // يحمل أرقام العملاء للاقتراحات.


        // يملأ بيانات العميل عند اختيار رقم هاتف من القائمة.
        private async void cmbCustomerPhone_SelectedIndexChanged(object sender,EventArgs e)
        {
            if(customerApplying!=0)return;
            await SelectCustomerAsync(CustomerReferenceKind.ByPhone,cmbCustomerPhone.Text.Trim());
        }



        private void MarkNewCustomer(string message)
        {
            _currentCustomerId = string.Empty;
            CustomerIdLabel.Text = Present("رقم العميل: جديد");
            RefreshCustomerSummary();
            ResetCustomerCreditDisplay();
            if (!string.IsNullOrWhiteSpace(message)) AppNotifications.Show(message);
        }

    }
}





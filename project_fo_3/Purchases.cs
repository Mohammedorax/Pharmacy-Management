using PharmacyManagement.Domain;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Printing;
using System.Linq;
using System.Windows.Forms;
using PharmacyManagement.Application;

namespace project_fo_3
{
    public partial class Purchases : Form
    {
        private readonly HashSet<string> _sessionPurchaseDetailIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private string currentPurchaseId = string.Empty;
        private MedicineUnitPicker medicineUnitPicker;
        private readonly Dictionary<DataGridViewRow,MedicineUnitSelection> cartUnits=new Dictionary<DataGridViewRow,MedicineUnitSelection>();
        private bool UnitCatalogEnabled => !previewMode && BarcodeSessionFactory==new Func<PurchaseBarcodeReadSession>(ApplicationServices.CreatePurchaseBarcodeReadSession);
        private Dictionary<string,MedicineUnitSelection> savedUnitPresentation;
        private MedicineUnitPresentationContext acceptedUnitOrigin;
        private string SavedUnitRowIdentity()=>CurrencyText+"|"+string.Join("|",PurchasesGV.Rows.Cast<DataGridViewRow>().Where(r=>!r.IsNewRow)
            .SelectMany(r=>r.Cells.Cast<DataGridViewCell>().Select(c=>Convert.ToString(c.Value,System.Globalization.CultureInfo.InvariantCulture)??string.Empty)).Select(v=>v.Length+":"+v));
        private MedicineUnitPresentationContext RequireSavedUnitOrigin()
        {
            if(acceptedUnitOrigin==null)acceptedUnitOrigin=new MedicineUnitPresentationContext(currentPurchaseId,SavedUnitRowIdentity());
            if(!acceptedUnitOrigin.Matches(currentPurchaseId,SavedUnitRowIdentity())){savedUnitPresentation=null;throw new InvalidOperationException("تغير سياق الفاتورة المحفوظة؛ أعد فتحها من السجل قبل الطباعة.");}
            return acceptedUnitOrigin;
        }
        private bool SavedUnitPresentationCurrent()
        {
            if(previewMode || !isPurchaseSaved)return true;
            bool current=!IsDisposed&&!Disposing && acceptedUnitOrigin!=null && acceptedUnitOrigin.Matches(currentPurchaseId,SavedUnitRowIdentity()) && savedUnitPresentation!=null;
            if(!current)savedUnitPresentation=null;return current;
        }
        private void GuardFrozenUnitPresentation()
        {if(!SavedUnitPresentationCurrent())throw new InvalidOperationException("لقطة الفاتورة غير مكتملة أو تغيرت الجلسة؛ أعد فتحها من السجل قبل الطباعة.");}

        private MedicineUnitSelection PrintUnit(DataGridViewRow row)
        {
            if(!isPurchaseSaved || previewMode)return CartUnit(row);
            GuardFrozenUnitPresentation();
            MedicineUnitSelection unit;
            if(savedUnitPresentation==null || !savedUnitPresentation.TryGetValue(Convert.ToString(row.Cells["DetailId"].Value),out unit))
                throw new InvalidOperationException("لم تكتمل لقطة وحدات الفاتورة المحفوظة؛ أعد تحميلها قبل الطباعة.");
            return unit;
        }
        internal void FreezeSavedUnitPresentation(IReadOnlyDictionary<string,MedicineUnitSelection> frozen)
        {
            savedUnitPresentation=null;
            RequireSavedUnitOrigin();
            if(frozen==null)throw new ArgumentNullException(nameof(frozen));
            var verified=new Dictionary<string,MedicineUnitSelection>(StringComparer.Ordinal);
            foreach(DataGridViewRow row in PurchasesGV.Rows)
            {
                if(row.IsNewRow)continue;string key=Convert.ToString(row.Cells["DetailId"].Value);MedicineUnitSelection unit;
                if(string.IsNullOrWhiteSpace(key)||!frozen.TryGetValue(key,out unit) || (CartUnit(row)!=null && unit==null))
                    throw new InvalidOperationException("لقطة وحدات الفاتورة غير مكتملة؛ الطباعة محظورة.");
                if(unit!=null && (unit.UnitPrice!=Convert.ToDecimal(row.Cells["BuyingPrice"].Value) || unit.EnteredQuantity!=Convert.ToDecimal(row.Cells["Quantity"].Value) || unit.GrossTotal!=Convert.ToDecimal(row.Cells["TotalAmount"].Value)))
                    throw new InvalidOperationException("لقطة وحدة الفاتورة لا تطابق السطر المعروض؛ الطباعة محظورة.");
                verified.Add(key,unit);
            }
            if(verified.Count!=frozen.Count)throw new InvalidOperationException("لقطة وحدات الفاتورة لا تطابق عدد السطور المعروضة.");
            savedUnitPresentation=verified;
        }
        private string[] SavedUnitLineKeys()=>PurchasesGV.Rows.Cast<DataGridViewRow>().Where(r=>!r.IsNewRow).Select(r=>Convert.ToString(r.Cells["DetailId"].Value)).ToArray();
        private void LoadSavedUnitPresentation()
        {
            savedUnitPresentation=null;var origin=RequireSavedUnitOrigin();
            FreezeSavedUnitPresentation(MedicineUnitInvoiceBridge.ReadSavedPresentation("Purchase",SavedUnitLineKeys(),origin));
        }
        private async System.Threading.Tasks.Task LoadSavedUnitPresentationAsync(System.Threading.CancellationToken token)
        {
            savedUnitPresentation=null;var origin=RequireSavedUnitOrigin();var keys=SavedUnitLineKeys();
            var frozen=await System.Threading.Tasks.Task.Run(()=>MedicineUnitInvoiceBridge.ReadSavedPresentation("Purchase",keys,origin),token);
            token.ThrowIfCancellationRequested();if(IsDisposed||Disposing || !origin.Matches(currentPurchaseId,SavedUnitRowIdentity()))throw new OperationCanceledException(token);
            FreezeSavedUnitPresentation(frozen);
        }
        internal MedicineUnitSelection CartUnit(DataGridViewRow row) { MedicineUnitSelection unit; return cartUnits.TryGetValue(row,out unit)?unit:null; }
        private bool isPurchaseSaved;
        private bool isSavingPurchase;
        private PurchaseWriteRecoveryCoordinator purchaseRecovery;
        private bool recoveredPurchaseCancelled;
        private bool purchaseRecoveryUnresolved;
        private System.Threading.CancellationTokenSource purchaseRecoveryCancellation;
        private System.Threading.CancellationTokenSource purchaseWriteCancellation;
        private DateTime? purchaseBusinessDate;
        private string purchaseSystemCurrencyCode;
        internal string PurchaseRecoveryDirectory = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"PharmacyManagement","PendingPurchases");
        private bool isSyncingSupplierSelection;
        private System.Threading.CancellationTokenSource barcodeReadCancellation;
        private long barcodeReadVersion;
        private bool applyingBarcodeResult;
        private bool changingContentDirection;
        internal Func<PurchaseBarcodeReadSession> BarcodeSessionFactory = ApplicationServices.CreatePurchaseBarcodeReadSession;
        private readonly bool previewMode;
        private readonly bool presentationLeftToRight;
        // لا يُفعّل إلا بعد تثبيت المخطط التجاري المحمي على قاعدة اختبار.
        private bool _commercialInventoryEnabled;
        private string CurrencyText => purchaseSystemCurrencyCode ?? string.Empty;

        private string FormatMoney(decimal amount)
        {
            return $"{amount:N2} {CurrencyText}";
        }

        internal bool PresentationLeftToRight => presentationLeftToRight;

        public Purchases() : this(false, UiLanguageCatalog.IsEnglishActive)
        {
        }

        internal Purchases(bool previewMode) : this(previewMode, UiLanguageCatalog.IsEnglishActive)
        {
        }

        private bool purchaseHistoryRequiresReload;
        private readonly Func<IReadOnlyList<PurchaseHistoryRecord>> purchaseHistoryLoader;
        private readonly IPurchaseInvoiceCancellationOperation purchaseCancellationOperation;

        internal Purchases(bool previewMode, bool presentationLeftToRight,
            Func<IReadOnlyList<PurchaseHistoryRecord>> purchaseHistoryLoader = null, IPurchaseInvoiceCancellationOperation purchaseCancellationOperation = null)
        {
            this.purchaseHistoryLoader = purchaseHistoryLoader ?? ApplicationServices.PurchaseWorkspace.GetHistory;
            this.purchaseCancellationOperation = purchaseCancellationOperation ?? ApplicationServices.PurchaseInvoiceCancellations;
            this.previewMode = previewMode;
            this.presentationLeftToRight = presentationLeftToRight;
            InitializeComponent();
            PurchaseHistoryGV.Tag = UiTheme.PreserveLocalizedGridDirection;
            PurchasesGV.FontChanged += (sender, args) => ApplyPurchaseGridSizing();
            PurchasesGV.SizeChanged += (sender, args) => ApplyPurchaseGridSizing();
            PurchasesGV.RowsAdded += (sender, args) => ApplyPurchaseGridSizing();
            PurchasesGV.RowsRemoved += (sender, args) => ApplyPurchaseGridSizing();
            txtQuantity.TextChanged += (sender,args)=> { if(!applyingBarcodeResult)CancelBarcodeRead(); };
            txtBarcode.TextChanged += (sender,args)=> { if(!applyingBarcodeResult){CancelBarcodeRead();CancelQuantityRead();CancelSelectionRead();CancelNamesRead();} };
            MediComboCb.TextChanged += (sender,args)=> { if(!applyingBarcodeResult){CancelBarcodeRead();CancelQuantityRead();if(!changingContentDirection)CancelActiveSelectionRead();} };
            cmbSupplier.TextChanged += (sender,args)=> { if(!applyingBarcodeResult){CancelBarcodeRead();CancelQuantityRead();CancelSelectionRead();CancelActiveNamesRead();} };
            Disposed += (sender,args)=> { purchaseWriteCancellation?.Cancel();purchaseRecoveryCancellation?.Cancel();purchaseRecovery?.Dispose();CancelStartupLookups();CancelBarcodeRead();CancelQuantityRead();CancelSelectionRead();CancelNamesRead(); };
            // Shared commerce layout uses DPI metrics; font autoscale must not inflate its minimum on first Show.
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96F, 96F);
            MinimumSize = new Size(960, 640);
            if (previewMode) Load -= Purchases_Load;
            UiTheme.Apply(this);
            ApplyPurchaseValueCaptions();
            if (previewMode) UiTheme.RemoveStatusStrip(this);
            ConfigureAntdPurchaseInputs();
            DesktopComponents.AddComboBoxDropdown(MediComboCb, "purchaseMedicineDropdown", Present("اختيار الصنف"), 2);
            CommerceWorkspaceTheme.ApplyPurchases(this);
            ConfigureWorkflowEnhancements();
            medicineUnitPicker=new MedicineUnitPicker("purchaseMedicineUnit",presentationLeftToRight);
            txtQuantity.Parent.Controls.Add(medicineUnitPicker);
            PurchasesGV.RowsRemoved += (sender,args)=> { foreach(var old in cartUnits.Keys.Where(r=>r.DataGridView!=PurchasesGV).ToArray())cartUnits.Remove(old); };
            Disposed += (sender,args)=>cartUnits.Clear();
            medicineUnitPicker.SelectedIndexChanged += (sender,args)=> { if(!isPurchaseSaved&&!isSavingPurchase)txtQuantity.Text="1"; };
            MedicineUnitPicker.EnsureColumn(PurchasesGV,presentationLeftToRight);
            lblProductSection.Cursor = Cursors.Hand;
            lblProductSection.Click += (sender, args) => OpenMedicineQuickSearch();
            AddPurchaseHistoryPreviewButton();
            AddPurchaseHistoryCancelButton();
            PurchaseHistoryGV.SelectionChanged += (sender, args) => UpdatePurchaseHistoryActions(PurchaseHistoryGV.Rows.Count > 0);
            AddSavePdfButton();
            ApplyPresentation();
            if (previewMode) PopulatePreviewData();
            CommerceWorkspaceTheme.RefreshLayout(this);
        }

        // The caption of a value label is translated here because the language pass must not
        // touch a label that may carry a business value.
        private void ApplyPurchaseValueCaptions()
        {
            if (lblPurchaseTiming == null) return;
            lblPurchaseTiming.Text = Present("التوقيت");
            lblPurchaseSupplier.Text = Present("المورد:");
            lblPurchaseEmployee.Text = Present("الموظف:");
        }

        private string Present(string arabicText)
        {
            return presentationLeftToRight ? UiLanguageCatalog.Translate(arabicText) : arabicText;
        }

        private string PresentFormat(string template, params object[] values)
        {
            return UiLanguageCatalog.Format(presentationLeftToRight, template, values);
        }

        private void ApplyPresentation()
        {
            Text = Present("المشتريات");
            lblPurchasesTitle.Text = Present("المشتريات");
            lblPurchasesSubtitle.Text = Present("إضافة فواتير الموردين وتحديث المخزون");
            lblTabCart.Text = Present("الفاتورة الحالية");
            lblTabHistory.Text = Present("سجل فواتير المشتريات");
            lblHistoryTitle.Text = Present("سجل فواتير المشتريات");
            lblHistorySubtitle.Text = Present("انقر مرتين على أي فاتورة لعرض الأصناف والتواريخ");
            lblCartTitle.Text = Present("بنود المشتريات — استخدم زر إزالة لحذف الصنف");
            lblSidebarTitle.Text = Present("الإدخال السريع");
            lblSupplier.Text = Present("المورد");
            lblBarcode.Text = Present("الباركود");
            lblProductSection.Text = Present("بحث الأصناف (F3)");
            lblQuantity.Text = Present("الكمية");
            lblProdDate.Text = Present("تاريخ الإنتاج");
            lblExpDate.Text = Present("تاريخ الانتهاء");
            lblCashier.Text = Present("الموظف");
            lblNotes.Text = Present("ملاحظات");
            lblStockInfo.Text = Present("المخزون الحالي — اختر صنفاً أولاً");

            ConfigurePurchaseAction(btnAddToPurchase, "إضافة للقائمة", "إضافة الصنف إلى فاتورة الشراء", AppIcon.Add);
            ConfigurePurchaseAction(btnConfirmPurchase, "اعتماد بيانات المشتريات", "اعتماد فاتورة الشراء", AppIcon.Confirm);
            ConfigurePurchaseAction(btnBackToPurchase, "العودة", "العودة إلى فاتورة المشتريات", AppIcon.Back);
            ConfigurePurchaseAction(btnSaveOnly, "حفظ فقط", "حفظ فاتورة المشتريات", AppIcon.Save);
            ConfigurePurchaseAction(btnPreviewPrint, "طباعة كاشير", "حفظ وطباعة إيصال المشتريات", AppIcon.Print);
            ConfigurePurchaseAction(btnPrintA4, "طباعة A4", "حفظ وطباعة فاتورة A4", AppIcon.Print);
            ConfigurePurchaseAction(btnClearPurchase, "مسح القائمة", "مسح فاتورة المشتريات", AppIcon.Refresh);
            ConfigurePurchaseAction(btnBack, "رجوع", "إغلاق شاشة المشتريات", AppIcon.Back);
            AntdUI.Button pdf = Controls.Find("btnSavePdf", true).OfType<AntdUI.Button>().FirstOrDefault();
            if (pdf != null) ConfigurePurchaseAction(pdf, "حفظ PDF", "حفظ فاتورة المشتريات PDF", AppIcon.Save);
            AntdUI.Button historyPreview = Controls.Find("btnPreviewPurchaseHistory", true).OfType<AntdUI.Button>().FirstOrDefault();
            if (historyPreview != null) ConfigurePurchaseAction(historyPreview, "معاينة الفاتورة", "معاينة فاتورة المشتريات المحددة", AppIcon.Search);
            AntdUI.Button historyCancel = Controls.Find("btnCancelPurchaseHistory", true).OfType<AntdUI.Button>().FirstOrDefault();
            if (historyCancel != null) ConfigurePurchaseAction(historyCancel, "إلغاء الفاتورة", "إلغاء فاتورة المشتريات المحددة", AppIcon.Close);

            UiDirection.Apply(this, presentationLeftToRight);
            RightToLeftLayout = !presentationLeftToRight;
            foreach (Label label in new[] { lblPurchasesTitle, lblPurchasesSubtitle, lblHistoryTitle, lblHistorySubtitle,
                lblCartTitle, lblSidebarTitle, lblSupplier, lblBarcode, lblProductSection, lblQuantity,
                lblProdDate, lblExpDate, lblCashier, lblNotes, lblStockInfo, PriceBDLabel, PriceADLabel,
                lblPurchaseEmployee, lblPurchaseSupplier, lblPurchaseTiming })
                label.TextAlign = presentationLeftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
            cmbSupplier.RightToLeft = presentationLeftToRight ? RightToLeft.No : RightToLeft.Yes;
            cmbSupplier.TextAlign = presentationLeftToRight ? HorizontalAlignment.Left : HorizontalAlignment.Right;
            MediComboCb.RightToLeft = presentationLeftToRight ? RightToLeft.No : RightToLeft.Yes;

            SelectEmployee.RightToLeft = presentationLeftToRight ? RightToLeft.No : RightToLeft.Yes;
            SelectEmployee.TextAlign = presentationLeftToRight ? HorizontalAlignment.Left : HorizontalAlignment.Right;
            txtPurchaseNotesInput.RightToLeft = presentationLeftToRight ? RightToLeft.No : RightToLeft.Yes;
            txtPurchaseNotesInput.TextAlign = presentationLeftToRight ? HorizontalAlignment.Left : HorizontalAlignment.Right;
            cmbSupplier.TextChanged += PurchaseContent_TextChanged;
            MediComboCb.TextChanged += PurchaseContent_TextChanged;
            SelectEmployee.TextChanged += PurchaseContent_TextChanged;
            RefreshContentDirections();
            UiTheme.ApplyStatusStripPresentation(this, presentationLeftToRight);
            ApplyPurchaseGridPresentation();
            UiTheme.ApplyDataGridQuality(PurchaseHistoryGV, Present("لا توجد فواتير مشتريات محفوظة للعرض حالياً."));

            if (previewMode)
            {
                foreach (Control control in new Control[] { btnAddToPurchase, btnConfirmPurchase, btnSaveOnly,
                    btnPreviewPrint, btnPrintA4, btnClearPurchase }) control.Enabled = false;
            }
        }

        private void ConfigurePurchaseAction(AntdUI.Button button, string text, string accessibleName, AppIcon icon)
        {
            if (button == null) return;
            button.Text = Present(text);
            button.AccessibleName = Present(accessibleName);
            button.IconSvg = OutlinedIcons.Svg(icon, !presentationLeftToRight);
            button.IconSize = new Size(18, 18);
            button.IconPosition = presentationLeftToRight ? AntdUI.TAlignMini.Left : AntdUI.TAlignMini.Right;
        }

        private void PurchaseContent_TextChanged(object sender, EventArgs e)
        {
            if(!changingContentDirection)ApplyContentDirection(sender as Control);
        }

        internal void RefreshContentDirections()
        {
            ApplyContentDirection(cmbSupplier);
            ApplyContentDirection(MediComboCb);
            ApplyContentDirection(SelectEmployee);
            ApplyPurchaseGridSizing();
            ApplyPurchaseHistoryColumnPresentation();
        }

        private void ApplyContentDirection(Control control)
        {
            if (control == null) return;
            string value = control.Text ?? string.Empty;
            char first = value.FirstOrDefault(character => !char.IsWhiteSpace(character));
            bool arabic = first >= '\u0600' && first <= '\u06FF';
            bool rightToLeft = arabic || (first == '\0' && !presentationLeftToRight);
            var nativeCombo=control as ComboBox;
            RightToLeft desired=rightToLeft ? RightToLeft.Yes : RightToLeft.No;
            if(nativeCombo!=null&&nativeCombo.IsHandleCreated&&nativeCombo.RightToLeft!=desired)
            {
                int selectedIndex=nativeCombo.SelectedIndex;
                nativeCombo.BeginInvoke(new Action(()=>{
                    if(nativeCombo.IsDisposed||nativeCombo.Disposing||nativeCombo.Text!=value||nativeCombo.SelectedIndex!=selectedIndex)return;
                    bool previousChanging=changingContentDirection;
                    try
                    {
                        changingContentDirection=true;
                        nativeCombo.RightToLeft=desired;
                        if(selectedIndex>=0&&selectedIndex<nativeCombo.Items.Count)nativeCombo.SelectedIndex=selectedIndex;
                        if(nativeCombo.Text!=value)nativeCombo.Text=value;
                    }
                    finally{changingContentDirection=previousChanging;}
                }));
                return;
            }
            RightToLeft direction=rightToLeft ? RightToLeft.Yes : RightToLeft.No;
            if(control.RightToLeft!=direction)
            {
                var combo=control as ComboBox;
                int selected=combo==null?-1:combo.SelectedIndex;
                bool previousChanging=changingContentDirection;
                try
                {
                    changingContentDirection=true;
                    control.RightToLeft=direction;
                    if(combo!=null&&!combo.IsDisposed)
                    {
                        if(selected>=0&&selected<combo.Items.Count)combo.SelectedIndex=selected;
                        if(combo.Text!=value)combo.Text=value;
                    }
                }
                finally{changingContentDirection=previousChanging;}
            }
            AntdUI.Select select = control as AntdUI.Select;
            if (select != null) select.TextAlign = rightToLeft ? HorizontalAlignment.Right : HorizontalAlignment.Left;
            TextBox textBox = control as TextBox;
            if (textBox != null) textBox.TextAlign = rightToLeft ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        }

        private void ApplyPurchaseGridPresentation()
        {
            ApplyGridDirection(PurchasesGV);
            ApplyGridDirection(PurchaseHistoryGV);
            SetColumnHeader(PurchasesGV, "DetailId", "رمز", "ID");
            SetColumnHeader(PurchasesGV, "MedName", "الصنف", "Medicine");
            SetColumnHeader(PurchasesGV, "BuyingPrice", "سعر الشراء", "Price");
            SetColumnHeader(PurchasesGV, "Quantity", "الكمية", "Qty");
            SetColumnHeader(PurchasesGV, "ProdDate", "تاريخ الإنتاج", "Production date");
            SetColumnHeader(PurchasesGV, "ExpDate", "تاريخ الانتهاء", "Expiry date");
            SetColumnHeader(PurchasesGV, "TotalAmount", "الإجمالي", "Total");
            SetColumnHeader(PurchasesGV, "RemoveCartItem", "إزالة", "Remove");
            var remove = PurchasesGV.Columns["RemoveCartItem"] as DataGridViewButtonColumn;
            if (remove != null) remove.Text = presentationLeftToRight ? "Remove" : "إزالة";
            ApplyPurchaseGridSizing();
        }

        private bool applyingPurchaseGridSizing;
        private bool rebuildingPurchaseGrid;
        private void ApplyPurchaseGridSizing()
        {
            if (IsDisposed || Disposing || PurchasesGV.IsDisposed || PurchasesGV.Disposing ||
                rebuildingPurchaseGrid || PurchasesGV.Columns.Count == 0 || applyingPurchaseGridSizing) return;
            // Columns.Clear raises RowsRemoved before its old header cells are fully detached.
            // Sizing is valid only for an attached, complete cart schema, never a rebuild midpoint.
            if(new[]{"DetailId","MedName","BuyingPrice","Quantity","ProdDate","ExpDate","TotalAmount","RemoveCartItem"}
                .Any(name=>!PurchasesGV.Columns.Contains(name)) ||
                PurchasesGV.Columns.Cast<DataGridViewColumn>().Any(column=>column.DataGridView!=PurchasesGV || column.HeaderCell.DataGridView!=PurchasesGV)) return;
            applyingPurchaseGridSizing = true;
            try
            {
            PurchasesGV.Tag = UiTheme.PreserveCustomGridColumns;
            // Measure constraints before distributing the available viewport width.
            PurchasesGV.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            foreach (DataGridViewColumn column in PurchasesGV.Columns)
                column.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            PurchasesGV.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;
            PurchasesGV.ColumnHeadersHeight = Math.Max(52, PurchasesGV.Font.Height * 2 + 16);
            PurchasesGV.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            PurchasesGV.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
            foreach (DataGridViewColumn column in PurchasesGV.Columns)
            {
                bool dateColumn = column.Name == "ProdDate" || column.Name == "ExpDate";
                column.MinimumWidth = dateColumn
                    ? TextRenderer.MeasureText("8888-88-88", column.InheritedStyle.Font ?? PurchasesGV.Font,
                        Size.Empty, TextFormatFlags.SingleLine | TextFormatFlags.NoPadding).Width + 18
                    : _commercialInventoryEnabled ? 64 : 40;
                column.HeaderCell.Style.Padding = new Padding(2, 0, 2, 0);
                Font headerFont = column.HeaderCell.InheritedStyle.Font ?? PurchasesGV.Font;
                int headerWordWidth = column.HeaderText.Split(new[] { ' ', '\r', '\n' },
                    StringSplitOptions.RemoveEmptyEntries)
                    .Select(word => TextRenderer.MeasureText(word, headerFont, Size.Empty,
                        TextFormatFlags.SingleLine | TextFormatFlags.NoPadding).Width)
                    .DefaultIfEmpty(0).Max();
                column.MinimumWidth = Math.Max(column.MinimumWidth, headerWordWidth + 8);
                if (column.Name == "MedName")
                {
                    int wordWidth = PurchasesGV.Rows.Cast<DataGridViewRow>().Where(row => !row.IsNewRow)
                        .SelectMany(row => Convert.ToString(row.Cells[column.Index].FormattedValue,
                            System.Globalization.CultureInfo.InvariantCulture).Split(new[] { ' ', '\r', '\n' },
                                StringSplitOptions.RemoveEmptyEntries))
                        .Select(word => TextRenderer.MeasureText(word, column.InheritedStyle.Font ?? PurchasesGV.Font,
                            Size.Empty, TextFormatFlags.SingleLine | TextFormatFlags.NoPadding).Width)
                        .DefaultIfEmpty(0).Max();
                    column.MinimumWidth = Math.Max(column.MinimumWidth, Math.Min(160, wordWidth + 12));
                }
                if (dateColumn) column.DefaultCellStyle.WrapMode = DataGridViewTriState.False;
                column.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                column.FillWeight = column.Name == "MedName" ? 160
                    : column.Name == "ProdDate" || column.Name == "ExpDate" ? 105
                    : column.Name == "TotalAmount" ? 95 : column.Name == "RemoveCartItem" ? 90 : 70;
                column.HeaderCell.Style.SelectionBackColor = UiTheme.Primary;
                column.HeaderCell.Style.SelectionForeColor = Color.White;
            }
            int available = Math.Max(0, PurchasesGV.ClientSize.Width -
                (PurchasesGV.RowHeadersVisible ? PurchasesGV.RowHeadersWidth : 0) -
                SystemInformation.VerticalScrollBarWidth - 2);
            // Keep editable quantities, units, complete dates, totals and removal in view.
            // Restore supporting columns when the viewport grows again.
            foreach (string name in new[] { "PreviousBuyingPrice", "DetailId" })
                if (PurchasesGV.Columns.Contains(name)) PurchasesGV.Columns[name].Visible = true;
            foreach (string name in new[] { "PreviousBuyingPrice", "DetailId" })
                if (PurchasesGV.Columns.Contains(name) && PurchasesGV.Columns.Cast<DataGridViewColumn>()
                    .Where(column => column.Visible).Sum(column => column.MinimumWidth) > available)
                    PurchasesGV.Columns[name].Visible = false;
            DataGridViewColumn[] visibleColumns = PurchasesGV.Columns.Cast<DataGridViewColumn>()
                .Where(column => column.Visible).ToArray();
            int minimumTotal = visibleColumns.Sum(column => column.MinimumWidth);
            if (minimumTotal > available)
            {
                var medicine = PurchasesGV.Columns["MedName"];
                medicine.MinimumWidth = Math.Max(48, medicine.MinimumWidth - (minimumTotal - available));
                minimumTotal = visibleColumns.Sum(column => column.MinimumWidth);
            }
            int extra = Math.Max(0, available - minimumTotal);
            double weightTotal = visibleColumns.Sum(column => (double)column.FillWeight);
            int allocated = 0;
            for (int index = 0; index < visibleColumns.Length; index++)
            {
                DataGridViewColumn column = visibleColumns[index];
                int share = index == visibleColumns.Length - 1 ? extra - allocated
                    : (int)Math.Floor(extra * column.FillWeight / Math.Max(1, weightTotal));
                allocated += share;
                column.Width = column.MinimumWidth + share;
            }
            if (PurchasesGV.Rows.Count > 0) PurchasesGV.AutoResizeRows(DataGridViewAutoSizeRowsMode.AllCells);
            }
            finally { applyingPurchaseGridSizing = false; }
        }
        private void ApplyGridDirection(DataGridView grid)
        {
            grid.RightToLeft = presentationLeftToRight ? RightToLeft.No : RightToLeft.Yes;
            grid.ColumnHeadersDefaultCellStyle.Alignment = presentationLeftToRight
                ? DataGridViewContentAlignment.MiddleLeft : DataGridViewContentAlignment.MiddleRight;
            grid.DefaultCellStyle.Alignment = presentationLeftToRight
                ? DataGridViewContentAlignment.MiddleLeft : DataGridViewContentAlignment.MiddleRight;
        }

        private void SetColumnHeader(DataGridView grid, string name, string arabic, string english)
        {
            if (grid.Columns.Contains(name)) grid.Columns[name].HeaderText = presentationLeftToRight ? english : arabic;
        }

        private void PopulatePreviewData()
        {
            _commercialInventoryEnabled = false;
            InitializePurchaseGrid();
            string previewMedicine = presentationLeftToRight ? "Paracetamol 500 mg" : "باراسيتامول 500 mg";
            PurchasesGV.Rows.Add("P-001", previewMedicine, 850, 12,
                DateTime.Today.AddMonths(-2).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), DateTime.Today.AddYears(2).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), 10200, Present("إزالة"));
            cmbSupplier.Text = presentationLeftToRight ? "Al Shifa Supplier" : "مورد الشفاء — Al Shifa";
            MediComboCb.Items.Add(previewMedicine);
            MediComboCb.SelectedIndex = 0;
            SelectEmployee.Text = presentationLeftToRight ? "System manager" : "مدير النظام";
            txtQuantity.Text = "12";
            txtPurchaseNotesInput.Text = presentationLeftToRight ? "Preview delivery — Batch 500" : "توريد تجريبي — Batch 500";
            ResetPurchaseDates();
            UpdateTotalAmount();
            lblPurchaseSupplier.Text = Present("المورد") + ": " + cmbSupplier.Text;
            lblPurchaseEmployee.Text = Present("الموظف") + ": " + SelectEmployee.Text;
            lblPurchaseTiming.Text = Present("التوقيت") + ": " + (purchaseBusinessDate ?? DateTime.Now).ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture);
            ApplyPurchaseGridPresentation();
            // تمنع مطالبة إلغاء مسودة عند إغلاق المعاينة؛ لم تُنشأ معاملة أو فاتورة فعلية.
            isPurchaseSaved = true;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            CommerceWorkspaceTheme.FitToWorkingArea(this);
        }

        private void ConfigureAntdPurchaseInputs()
        {
            ConfigurePurchaseInput(txtBarcode, "الباركود", "الباركود الممسوح ضوئيًا أو المدخل يدويًا.");
            ConfigurePurchaseInput(txtQuantity, "الكمية", "أدخل كمية صحيحة للصنف المحدد.");

            cmbSupplier.AccessibleName = Present("المورد");
            cmbSupplier.AccessibleDescription = Present("اختر مورداً مسجلاً أو اكتب اسم مورد جديد.");
            cmbSupplier.List = false;
            cmbSupplier.Font = AppFonts.Create(10F);
            AntdThemeAdapter.Apply(cmbSupplier);
            MediComboCb.AccessibleName = Present("اسم الدواء");
            MediComboCb.AccessibleRole = AccessibleRole.ComboBox;
            dtpProdDate.AccessibleName = Present("تاريخ الإنتاج");
            dtpExpDate.AccessibleName = Present("تاريخ الانتهاء");
            SelectEmployee.AccessibleName = Present("الموظف المسؤول");
            SelectEmployee.AccessibleRole = AccessibleRole.ComboBox;
            txtPurchaseNotesInput.AccessibleName = Present("ملاحظات فاتورة الشراء");
            txtPurchaseNotesInput.AccessibleRole = AccessibleRole.Text;
            txtPurchaseNotesInput.Font = AppFonts.Create(9.5F);
            txtPurchaseNotesInput.Multiline = true;
            txtPurchaseNotesInput.WordWrap = true;
            UiTheme.ApplyTo(txtPurchaseNotesInput);
            txtPurchaseNotesInput.RightToLeft = UiDirection.IsLeftToRight ? RightToLeft.No : RightToLeft.Yes;
            txtPurchaseNotesInput.TextAlign = UiDirection.IsLeftToRight ? HorizontalAlignment.Left : HorizontalAlignment.Right;
            btnAddToPurchase.AccessibleName = Present("إضافة الصنف إلى فاتورة الشراء");
            btnConfirmPurchase.AccessibleName = Present("اعتماد فاتورة الشراء");

            // ترتيب لوحة المفاتيح يتبع إدخال الفاتورة من المورد حتى الاعتماد.
            cmbSupplier.TabIndex = 0;
            txtBarcode.TabIndex = 1;
            MediComboCb.TabIndex = 2;
            txtQuantity.TabIndex = 3;
            btnAddToPurchase.TabIndex = 4;
            dtpProdDate.TabIndex = 5;
            dtpExpDate.TabIndex = 6;
            SelectEmployee.TabIndex = 7;
            txtPurchaseNotesInput.TabIndex = 8;
            btnConfirmPurchase.TabIndex = 9;
        }

        private void ConfigurePurchaseInput(AntdUI.Input input, string label, string description)
        {
            input.AccessibleName = Present(label);
            input.AccessibleRole = AccessibleRole.Text;
            input.AccessibleDescription = Present(description);
            input.RightToLeft = RightToLeft.No;
            input.TextAlign = HorizontalAlignment.Left;
            input.Font = AppFonts.Create(10F);
            input.Size = new Size(input.Width, UiTheme.StandardInputHeight);
            AntdThemeAdapter.Apply(input);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.F3 || keyData == (Keys.Control | Keys.K))
            {
                OpenMedicineQuickSearch();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private async void OpenMedicineQuickSearch()
        {
            if(RejectPendingBarcodeAction())return;
            if (previewMode || isPurchaseSaved || isSavingPurchase) return;
            try
            {
                using (var dialog = new MedicineQuickSearchForm(MediComboCb.Text))
                {
                    if (dialog.ShowDialog(this) != DialogResult.OK) return;
                    var selected = dialog.SelectedMedicine;
                    try { isSyncingSupplierSelection = true; SelectSupplierByName(selected.CompanyName); }
                    finally { isSyncingSupplierSelection = false; }
                    if(!await LoadMedicineNamesAsync(selected.CompanyName))return;
                    SelectMedicineByName(selected.MedicineName);
                    await AddSelectedMedicineFromKeyboardAsync();
                }
            }
            catch (Exception exception) { UiTheme.ShowOperationIssue(this, "البحث السريع", exception); }
        }
        private void btnClosePurchases_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void SwitchToPurchaseCartView()
        {
            pnlPurchaseCartHost.Visible = true;
            pnlPurchaseHistory.Visible = false;
            lblTabCart.Font = AppFonts.Create(10.2F, FontStyle.Bold);
            lblTabCart.ForeColor = Color.White;
            lblTabHistory.Font = AppFonts.Create(10.2F, FontStyle.Regular);
            lblTabHistory.ForeColor = Color.FromArgb(200, 230, 220);
        }

        private void SwitchToPurchaseHistoryView()
        {
            pnlPurchaseCartHost.Visible = false;
            pnlPurchaseHistory.Visible = true;
            lblTabHistory.Font = AppFonts.Create(10.2F, FontStyle.Bold);
            lblTabHistory.ForeColor = Color.White;
            lblTabCart.Font = AppFonts.Create(10.2F, FontStyle.Regular);
            lblTabCart.ForeColor = Color.FromArgb(200, 230, 220);
            LoadPurchaseHistory();
        }

        private void lblTabCart_Click(object sender, EventArgs e)
        {
            SwitchToPurchaseCartView();
        }

        private void lblTabHistory_Click(object sender, EventArgs e)
        {
            SwitchToPurchaseHistoryView();
        }

        private void btnBackToPurchase_Click(object sender, EventArgs e)
        {
            SwitchToPurchaseCartView();
        }

        private void LoadPurchaseHistory()
        {
            try { ReloadPurchaseHistory(); }
            catch (Exception ex)
            {
                InvalidatePurchaseHistory();
                UiTheme.ShowOperationFailure(this, "تحميل سجل المشتريات", ex);
            }
        }

        private void ReloadPurchaseHistory()
        {
            IReadOnlyList<PurchaseHistoryRecord> records = purchaseHistoryLoader();
            DataTable dt = new DataTable();
            dt.Columns.Add("رقم الفاتورة");
            dt.Columns.Add("المورد");
            dt.Columns.Add("التاريخ", typeof(DateTime));
            dt.Columns.Add("الإجمالي", typeof(decimal));
            dt.Columns.Add("الموظف");
            dt.Columns.Add("ملاحظات");
            dt.Columns.Add("الحالة");
            dt.Columns.Add("العملة");
            foreach (PurchaseHistoryRecord record in records)
            {
                dt.Rows.Add(record.PurchaseId, record.SupplierName, record.PurchaseDate,
                    record.TotalAmount, record.EmployeeName, record.Notes,
                    record.IsCancelled ? Present("ملغاة") : Present("معتمدة"),
                    record.SystemCurrencyCode ?? Present("غير محدد"));
            }
            PurchaseHistoryGV.DataSource = dt;
            ApplyPurchaseGridPresentation();
            SetColumnHeader(PurchaseHistoryGV, "رقم الفاتورة", "رقم الفاتورة", "Invoice");
            SetColumnHeader(PurchaseHistoryGV, "المورد", "المورد", "Supplier");
            SetColumnHeader(PurchaseHistoryGV, "التاريخ", "التاريخ", "Date");
            SetColumnHeader(PurchaseHistoryGV, "الإجمالي", "الإجمالي", "Total");
            SetColumnHeader(PurchaseHistoryGV, "الموظف", "الموظف", "Employee");
            SetColumnHeader(PurchaseHistoryGV, "الحالة", "الحالة", "Status");
            SetColumnHeader(PurchaseHistoryGV, "العملة", "العملة", "Currency");
            if (PurchaseHistoryGV.Columns["ملاحظات"] != null)
                PurchaseHistoryGV.Columns["ملاحظات"].Visible = false;
            if (PurchaseHistoryGV.Columns["الإجمالي"] != null)
                PurchaseHistoryGV.Columns["الإجمالي"].DefaultCellStyle.Format = "N2";
            if (PurchaseHistoryGV.Columns["التاريخ"] != null)
                PurchaseHistoryGV.Columns["التاريخ"].DefaultCellStyle.Format = "yyyy-MM-dd HH:mm";
            if (PurchaseHistoryGV.Columns["الحالة"] != null)
                PurchaseHistoryGV.Columns["الحالة"].MinimumWidth = 84;
            ApplyPurchaseHistoryColumnPresentation();
            purchaseHistoryRequiresReload = false;
            UpdatePurchaseHistoryActions(dt.Rows.Count > 0);
        }

        private void InvalidatePurchaseHistory()
        {
            purchaseHistoryRequiresReload = true;
            if (IsDisposed || Disposing) return;
            UpdatePurchaseHistoryActions(false);
        }

        private void ApplyPurchaseHistoryColumnPresentation()
        {
            DataGridViewColumn date = PurchaseHistoryGV.Columns["التاريخ"];
            if (date != null)
            {
                date.DefaultCellStyle.Format = "yyyy-MM-dd HH:mm";
                date.DefaultCellStyle.FormatProvider = System.Globalization.CultureInfo.InvariantCulture;
                date.DefaultCellStyle.WrapMode = DataGridViewTriState.False;
                date.MinimumWidth = TextRenderer.MeasureText("2099-12-31 23:59", date.InheritedStyle.Font ?? PurchaseHistoryGV.Font,
                    Size.Empty, TextFormatFlags.SingleLine | TextFormatFlags.NoPadding).Width + 20;
            }
            foreach (string name in new[] { "العملة", "الحالة" })
            {
                DataGridViewColumn column = PurchaseHistoryGV.Columns[name];
                if (column == null) continue;
                string longest = name == "العملة" ? Present("غير محدد") : Present("ملغاة");
                column.DefaultCellStyle.WrapMode = DataGridViewTriState.False;
                column.MinimumWidth = Math.Max(84, TextRenderer.MeasureText(longest, column.InheritedStyle.Font ?? PurchaseHistoryGV.Font,
                    Size.Empty, TextFormatFlags.SingleLine | TextFormatFlags.NoPadding).Width + 20);
            }
        }

        private void UpdatePurchaseHistoryActions(bool hasRows)
        {
            hasRows = hasRows && !purchaseHistoryRequiresReload;
            Control previewButton = pnlPurchaseHistory.Controls["btnPreviewPurchaseHistory"];
            if (previewButton == null)
            {
                return;
            }

            previewButton.Enabled = hasRows;
            previewButton.AccessibleDescription = purchaseHistoryRequiresReload ? Present("تعذر تحديث السجل. أعد فتحه قبل تنفيذ إجراء آخر.")
                : hasRows ? Present("يعرض تفاصيل فاتورة المشتريات المحددة.")
                : Present("لا توجد فواتير مشتريات محفوظة للعرض حالياً.");
            AntdUI.Button cancelButton = pnlPurchaseHistory.Controls["btnCancelPurchaseHistory"] as AntdUI.Button;
            if (cancelButton != null)
            {
                DataGridViewRow row = GetSelectedPurchaseHistoryRow();
                bool cancelled = row != null && row.Cells["الحالة"] != null &&
                    string.Equals(Convert.ToString(row.Cells["الحالة"].Value), Present("ملغاة"), StringComparison.Ordinal);
                cancelButton.Enabled = hasRows && row != null && !cancelled && PermissionService.CanExecute(AppOperation.CancelPurchaseInvoice);
                cancelButton.AccessibleDescription = purchaseHistoryRequiresReload ? Present("تعذر تحديث السجل. أعد فتحه قبل تنفيذ إجراء آخر.")
                    : cancelled ? Present("هذه الفاتورة ملغاة مسبقاً.")
                    : !hasRows || row == null ? Present("حدد فاتورة من السجل أولاً.")
                    : cancelButton.Enabled ? Present("يعكس القيد والمخزون لفاتورة المشتريات بعد التأكيد.")
                    : Present("إلغاء الفواتير متاح للمدير فقط.");
            }
        }

        private void PurchaseHistoryGV_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            ShowPurchaseHistoryPreview(PurchaseHistoryGV.Rows[e.RowIndex]);
        }

        private void AddPurchaseHistoryPreviewButton()
        {
            if (pnlPurchaseHistory.Controls.ContainsKey("btnPreviewPurchaseHistory"))
            {
                return;
            }

            AntdUI.Button previewButton = DesktopComponents.Action("btnPreviewPurchaseHistory", "معاينة الفاتورة", AppIcon.Search);
            previewButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            previewButton.Size = new Size(150, 40);
            previewButton.Location = new Point(Math.Max(24, btnBackToPurchase.Left - 164), btnBackToPurchase.Top);

            previewButton.Click += btnPreviewPurchaseHistory_Click;
            pnlPurchaseHistory.Controls.Add(previewButton);
            previewButton.BringToFront();
            CommerceWorkspaceTheme.ApplySecondaryAction(previewButton);
            UpdatePurchaseHistoryActions(false);
        }

        private void AddPurchaseHistoryCancelButton()
        {
            if (pnlPurchaseHistory.Controls.ContainsKey("btnCancelPurchaseHistory")) return;
            AntdUI.Button button = DesktopComponents.Action("btnCancelPurchaseHistory", "إلغاء الفاتورة", AppIcon.Close);
            button.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            button.Size = new Size(156, 40);
            button.Location = new Point(Math.Max(24, btnBackToPurchase.Left - 328), btnBackToPurchase.Top);
            button.Click += btnCancelPurchaseHistory_Click;
            pnlPurchaseHistory.Controls.Add(button);
            button.BringToFront();
            CommerceWorkspaceTheme.ApplyDestructiveAction(button);
            UpdatePurchaseHistoryActions(false);
        }

        private void btnCancelPurchaseHistory_Click(object sender, EventArgs e)
        {
            if (!PermissionService.EnsureAllowed(AppOperation.CancelPurchaseInvoice, this)) return;
            if (purchaseHistoryRequiresReload) { LoadPurchaseHistory(); return; }
            DataGridViewRow row = GetSelectedPurchaseHistoryRow();
            if (row == null)
            {
                AppNotifications.Show(Present("حدد فاتورة من السجل أولاً."), Present("إلغاء الفاتورة"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (string.Equals(Convert.ToString(row.Cells["الحالة"].Value), Present("ملغاة"), StringComparison.Ordinal))
            {
                AppNotifications.Show(Present("هذه الفاتورة ملغاة مسبقاً."), Present("إلغاء الفاتورة"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string purchaseId = Convert.ToString(row.Cells["رقم الفاتورة"].Value);
            string confirmation = Present("سيُعكس القيد وتُخصم الكمية من المخزون. سيُرفض الإلغاء إذا استُهلكت التشغيلة أو لم يكفِ الرصيد. هل تريد المتابعة؟");
            if (AppNotifications.Show(confirmation, Present("تأكيد إلغاء فاتورة مشتريات"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            PurchaseInvoiceCancellationOutcome completed = null;
            try
            {
                completed = purchaseCancellationOperation.Cancel(
                    new PurchaseInvoiceCancellationRequest
                    {
                        PurchaseId = purchaseId,
                        ActorName = SessionContext.CurrentUser,
                        ActorRole = SessionContext.CurrentRole,
                        Reason = "إلغاء معتمد من سجل المشتريات"
                    });
                purchaseHistoryRequiresReload = true;
                ReloadPurchaseHistory();
                UiTheme.SetFormStatus(this, Present("تم إلغاء الفاتورة وعكس ") + completed.RemovedQuantity + Present(" وحدة بنجاح."), success: true);
            }
            catch (Exception exception)
            {
                if (completed == null)
                    UiTheme.ShowOperationFailure(this, "إلغاء فاتورة المشتريات", exception, preserveValidationMessage: true);
                else
                {
                    InvalidatePurchaseHistory();
                    UiTheme.ShowCompletedOperationFollowUpFailure(this, "إلغاء فاتورة المشتريات", completed.PurchaseId, exception);
                }
            }
        }

        private void btnPreviewPurchaseHistory_Click(object sender, EventArgs e)
        {
            DataGridViewRow row = GetSelectedPurchaseHistoryRow();
            if (row == null)
            {
                AppNotifications.Show(Present("حدد فاتورة من سجل المشتريات ثم اضغط معاينة."), Present("معاينة الفاتورة"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            ShowPurchaseHistoryPreview(row);
        }

        private DataGridViewRow GetSelectedPurchaseHistoryRow()
        {
            if (PurchaseHistoryGV.SelectedRows.Count > 0)
            {
                return PurchaseHistoryGV.SelectedRows[0];
            }

            return PurchaseHistoryGV.CurrentRow != null && !PurchaseHistoryGV.CurrentRow.IsNewRow ? PurchaseHistoryGV.CurrentRow : null;
        }

        private void ShowPurchaseHistoryPreview(DataGridViewRow row)
        {
            if (purchaseHistoryRequiresReload) { LoadPurchaseHistory(); return; }
            if (row == null) return;

            string purchaseId = row.Cells["رقم الفاتورة"].Value?.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(purchaseId)) return;

            string details = "";
            try
            {
                IReadOnlyList<PurchaseHistoryLine> lines = ApplicationServices.PurchaseWorkspace.GetHistoryLines(purchaseId);
                details = FormatPurchaseHistoryLines(lines);
            }
            catch (Exception ex)
            {
                UiTheme.ShowOperationIssue(this, "تحميل تفاصيل فاتورة المشتريات", ex);
                return;
            }

            if (string.IsNullOrWhiteSpace(details))
                details = Present("لا توجد تفاصيل متاحة.");

            string header = PresentFormat("📋 المورد: {0}\n📅 التاريخ: {1}\n💰 الإجمالي: {2} {3}\n👤 الموظف: {4}\n\n--- بنود فاتورة المشتريات ---\n\n",
                row.Cells["المورد"].Value, row.Cells["التاريخ"].Value, row.Cells["الإجمالي"].Value,
                row.Cells["العملة"].Value, row.Cells["الموظف"].Value);

            AppNotifications.Show(header + details, Present("تفاصيل فاتورة المشتريات"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private string FormatPurchaseHistoryLines(IReadOnlyList<PurchaseHistoryLine> lines)
        {
            return string.Join(Environment.NewLine, lines.Select(line => PresentFormat(
                "{0}  |  سعر: {1:N2}  |  كمية: {2:N3}  |  إجمالي: {3:N2}  |  إنتاج: {4}  |  انتهاء: {5}  |  وحدة: {6}",
                line.MedicineName, line.BuyingPrice, line.Quantity, line.TotalAmount,
                line.ProductionDate.HasValue ? line.ProductionDate.Value.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) : string.Empty,
                line.ExpiryDate.HasValue ? line.ExpiryDate.Value.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) : string.Empty,
                string.IsNullOrWhiteSpace(line.Unit) ? "—" : line.Unit)));
        }

        private void btnBack_Click(object sender, EventArgs e)
        {
            Close();
        }

        private async void Purchases_Load(object sender, EventArgs e)
        {
            startupLookupsReady=false;fullStartupPending=true;
            try
            {
                var startupSession=BarcodeSessionFactory();
                if(!await LoadPurchaseLookupsAsync()||!startupSession.IsCurrent){startupLookupsReady=false;return;}
                startupLookupsReady=false;
                ApplyCurrentCashier();
                ResetPurchaseDates();
                UpdateTotalAmount();
                if(!await LoadMedicineNamesAsync())return;
                if(!startupSession.IsCurrent){
                    UiTheme.SetStatus(lblStockInfo,Present("تغيرت جلسة المشتريات. أعد البحث."),false);
                    return;
                }
                MediComboCb.SelectedIndex = -1;
                MediComboCb.Text = Present("اختر الدواء");
                purchaseRecoveryCancellation=new System.Threading.CancellationTokenSource();
                await RestorePendingPurchaseAsync(purchaseRecoveryCancellation.Token);
                startupLookupsReady=true;
            }
            catch(OperationCanceledException){startupLookupsReady=false;}
            catch (Exception ex)
            {
                startupLookupsReady=false;
                if(!IsDisposed&&!Disposing)UiTheme.ShowOperationIssue(this, "تحميل شاشة المشتريات", ex);
            }
            finally{fullStartupPending=false;purchaseRecoveryCancellation?.Dispose();purchaseRecoveryCancellation=null;}
        }
        private void InitializePurchaseGrid()
        {
            rebuildingPurchaseGrid=true;
            try
            {
            PurchasesGV.Columns.Clear();
            PurchasesGV.Columns.Add("DetailId", "رمز");
            PurchasesGV.Columns.Add("MedName", "الصنف");
            if (_commercialInventoryEnabled)
            {
                PurchasesGV.Columns.Add("Gtin", "GTIN");
                PurchasesGV.Columns.Add("BatchNumber", "رقم التشغيلة");
                PurchasesGV.Columns.Add("SerialNumber", "الرقم التسلسلي");
            }
            PurchasesGV.Columns.Add("BuyingPrice", "سعر الشراء");
            PurchasesGV.Columns.Add("Quantity", "الكمية");
            PurchasesGV.Columns.Add("ProdDate", "تاريخ الإنتاج");
            PurchasesGV.Columns.Add("ExpDate", "تاريخ الانتهاء");
            PurchasesGV.Columns.Add("TotalAmount", "الإجمالي");
            DataGridViewButtonColumn removeColumn = new DataGridViewButtonColumn
            {
                Name = "RemoveCartItem",
                HeaderText = "إزالة",
                Text = "إزالة",
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
            PurchasesGV.Columns.Add(removeColumn);
            MedicineUnitPicker.EnsureColumn(PurchasesGV,presentationLeftToRight);
            AddPurchasePriceComparisonColumn();
            PurchasesGV.ReadOnly = false;
            PurchasesGV.AllowUserToAddRows = false;
            PurchasesGV.AllowUserToDeleteRows = false;
            foreach (DataGridViewColumn column in PurchasesGV.Columns)
            {
                column.ReadOnly = true;
            }
            PurchasesGV.Columns["BuyingPrice"].ReadOnly = false;
            PurchasesGV.Columns["Quantity"].ReadOnly = false;
            PurchasesGV.EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2;
            PurchasesGV.Columns["Quantity"].ToolTipText = presentationLeftToRight
                ? "Double-click or press F2 to edit quantity before saving."
                : "انقر مرتين أو اضغط F2 لتعديل الكمية قبل الحفظ.";
            PurchasesGV.CellBeginEdit -= PurchasesGV_CellBeginEdit;
            PurchasesGV.CellBeginEdit += PurchasesGV_CellBeginEdit;
            PurchasesGV.Columns["RemoveCartItem"].ReadOnly = false;
            if (_commercialInventoryEnabled)
            {
                PurchasesGV.Columns["Gtin"].ReadOnly = false;
                PurchasesGV.Columns["BatchNumber"].ReadOnly = false;
                PurchasesGV.Columns["SerialNumber"].ReadOnly = false;
            }
            PurchasesGV.CellValidating -= PurchasesGV_CellValidating;
            PurchasesGV.CellValidating += PurchasesGV_CellValidating;
            PurchasesGV.CellEndEdit -= PurchasesGV_CellEndEdit;
            PurchasesGV.CellEndEdit += PurchasesGV_CellEndEdit;

            PurchasesGV.ColumnHeadersDefaultCellStyle.Font = AppFonts.Create(9.75F, FontStyle.Bold);
            foreach (DataGridViewColumn column in PurchasesGV.Columns)
            {
                column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            }

            PurchasesGV.Columns["DetailId"].Width = 80;
            PurchasesGV.Columns["MedName"].Width = 140;
            if (_commercialInventoryEnabled)
            {
                PurchasesGV.Columns["Gtin"].Width = 120;
                PurchasesGV.Columns["BatchNumber"].Width = 110;
                PurchasesGV.Columns["SerialNumber"].Width = 120;
            }
            PurchasesGV.Columns["BuyingPrice"].Width = 90;
            PurchasesGV.Columns["Quantity"].Width = 70;
            PurchasesGV.Columns["ProdDate"].Width = 100;
            PurchasesGV.Columns["ExpDate"].Width = 100;
            PurchasesGV.Columns["TotalAmount"].Width = 110;
            }
            finally { rebuildingPurchaseGrid=false; }
            ApplyPurchaseGridPresentation();
        }

        private bool IsCommercialInventoryEnabled()
        {
            return ApplicationServices.PurchaseInventoryMode.IsCommercialInventoryEnabled();
        }

        private System.Threading.CancellationTokenSource namesReadCancellation;
        private long namesReadVersion;
        private bool namesReadPending;
        private void CancelNamesRead()
        {
            namesReadVersion++;namesReadPending=false;
            var previous=namesReadCancellation;namesReadCancellation=null;
            if(previous!=null){previous.Cancel();previous.Dispose();}
        }
        private void CancelActiveNamesRead(){if(namesReadCancellation!=null)CancelNamesRead();}
        private async System.Threading.Tasks.Task<bool> LoadMedicineNamesAsync(string supplierName=null)
        {
            if(previewMode||IsDisposed||Disposing||isPurchaseSaved||isSavingPurchase)return false;
            CancelNamesRead();CancelBarcodeRead();CancelSelectionRead();CancelQuantityRead();
            long version=namesReadVersion;namesReadPending=true;
            string document=currentPurchaseId;
            System.Threading.CancellationTokenSource request=null;
            try
            {
                await System.Threading.Tasks.Task.Yield();
                if(IsDisposed||Disposing||version!=namesReadVersion||currentPurchaseId!=document||isPurchaseSaved||isSavingPurchase)return false;
                string supplier=cmbSupplier.Text;
                if(!string.IsNullOrWhiteSpace(supplierName)&&!string.Equals(supplierName.Trim(),supplier.Trim(),StringComparison.OrdinalIgnoreCase))return false;
                request=new System.Threading.CancellationTokenSource();namesReadCancellation=request;
                try{applyingBarcodeResult=true;MediComboCb.Items.Clear();MediComboCb.SelectedIndex=-1;txtQuantity.Clear();}
                finally{applyingBarcodeResult=false;}
                var session=BarcodeSessionFactory();
                UiTheme.SetStatus(lblStockInfo,Present("جاري قراءة الصنف. انتظر اكتمال القراءة أو غيّر الإدخال لإلغائها."),false);
                var names=await session.Medicines.GetNamesAsync(supplierName,session.Actor,request.Token);
                if(request.IsCancellationRequested||IsDisposed||Disposing||version!=namesReadVersion||!session.IsCurrent||
                    cmbSupplier.Text!=supplier||currentPurchaseId!=document||isPurchaseSaved||isSavingPurchase)return false;
                try{applyingBarcodeResult=true;foreach(string name in names)MediComboCb.Items.Add(name);}
                finally{applyingBarcodeResult=false;}
                UiTheme.SetStatus(lblStockInfo,Present("تم تحميل الأصناف. اختر الصنف وأدخل الكمية."),true);
                return true;
            }
            catch(OperationCanceledException){return false;}
            catch(UnauthorizedAccessException){if(!IsDisposed&&!Disposing&&version==namesReadVersion)UiTheme.SetStatus(lblStockInfo,Present("تغيرت جلسة المشتريات. أعد البحث."),false);return false;}
            catch(Exception ex){if(!IsDisposed&&!Disposing&&version==namesReadVersion)SetPurchaseStatusFailure("تحميل الأدوية",ex);return false;}
            finally{if(version==namesReadVersion){namesReadPending=false;namesReadCancellation=null;if(request!=null)request.Dispose();}}
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

        private void SelectSupplierByName(string supplierName)
        {
            if (string.IsNullOrWhiteSpace(supplierName))
            {
                return;
            }

            for (int index = 0; index < cmbSupplier.Items.Count; index++)
            {
                if (string.Equals(cmbSupplier.Items[index].ToString(), supplierName, StringComparison.OrdinalIgnoreCase))
                {
                    cmbSupplier.SelectedIndex = index;
                    return;
                }
            }

            cmbSupplier.SelectedIndex = -1;
            cmbSupplier.Text = supplierName;
        }

        private bool readingMedicinePrice;
        private bool RejectPendingBarcodeAction()
        {
            if(fullStartupPending){UiTheme.SetStatus(lblStockInfo,Present("جاري تحميل بيانات المشتريات. انتظر اكتمال التحميل."),false);return true;}
            if(reservingPurchaseNumber){UiTheme.SetStatus(lblStockInfo,Present("جاري حجز رقم فاتورة المشتريات. انتظر اكتمال الحجز."),false);return true;}
            if(!startupLookupsReady){UiTheme.SetStatus(lblStockInfo,Present(startupLookupCancellation!=null?"جاري تحميل الموردين والموظفين. انتظر اكتمال التحميل.":"لم يكتمل تحميل بيانات المشتريات. أغلق الشاشة وأعد فتحها."),false);return true;}
            if(barcodeReadCancellation==null&&!quantityReadPending&&!selectionReadPending&&!namesReadPending&&startupLookupCancellation==null)return false;
            UiTheme.SetStatus(lblStockInfo,Present((readingMedicinePrice||quantityReadPending||selectionReadPending||namesReadPending||startupLookupCancellation!=null)?"جاري قراءة الصنف. انتظر اكتمال القراءة أو غيّر الإدخال لإلغائها.":"جاري البحث بالباركود. انتظر اكتمال القراءة أو غيّر الإدخال لإلغائها."),false);
            return true;
        }
        private void CancelBarcodeRead()
        {
            readingMedicinePrice=false;reservingPurchaseNumber=false;
            barcodeReadVersion++;
            var pending=barcodeReadCancellation;
            barcodeReadCancellation=null;
            if(pending!=null){pending.Cancel();pending.Dispose();}
        }

        private async void txtBarcode_KeyDown(object sender, KeyEventArgs e)
        {
            if(e.KeyCode!=Keys.Enter)return;
            e.Handled=e.SuppressKeyPress=true;
            await TrySelectMedicineByBarcodeAsync();
        }
        private bool unifiedBarcodeNotFound;
        internal async System.Threading.Tasks.Task<bool> TrySelectMedicineByBarcodeAsync()
        {
            unifiedBarcodeNotFound=false;
            if(previewMode||IsDisposed||Disposing||isPurchaseSaved||isSavingPurchase||purchaseRecoveryUnresolved||fullStartupPending||!startupLookupsReady||startupLookupCancellation!=null)return false;
            CancelNamesRead();
            CancelSelectionRead();
            CancelQuantityRead();
            CancelBarcodeRead();
            var cancellation=new System.Threading.CancellationTokenSource();
            barcodeReadCancellation=cancellation;
            long version=barcodeReadVersion;
            string barcode=txtBarcode.Text, purchase=currentPurchaseId;
            string supplier=cmbSupplier.Text, medicine=MediComboCb.Text;
            bool saved=isPurchaseSaved;
            var session=BarcodeSessionFactory();
            try
            {
                var match=await session.Barcode.FindByBarcodeAsync(barcode,session.Actor,cancellation.Token);
                if(cancellation.IsCancellationRequested||IsDisposed||Disposing||version!=barcodeReadVersion||
                    !session.IsCurrent||barcode!=txtBarcode.Text||purchase!=currentPurchaseId||saved!=isPurchaseSaved||
                    supplier!=cmbSupplier.Text||medicine!=MediComboCb.Text)return false;
                if(match==null)
                {
                    unifiedBarcodeNotFound=true;
                    UiTheme.SetStatus(lblStockInfo,Present("لم يتم العثور على باركود لهذا الصنف."),false);
                    return false;
                }
                var names=await session.Medicines.GetNamesAsync(match.CompanyName,session.Actor,cancellation.Token);
                var detail=await session.Medicines.FindAsync(match.MedicineName,session.Actor,cancellation.Token);
                var units=UnitCatalogEnabled?await System.Threading.Tasks.Task.Run(()=>MedicineUnitInvoiceBridge.ReadUnits(match.MedicineName,false),cancellation.Token):null;
                if(cancellation.IsCancellationRequested||IsDisposed||Disposing||version!=barcodeReadVersion||
                    !session.IsCurrent||barcode!=txtBarcode.Text||purchase!=currentPurchaseId||saved!=isPurchaseSaved||
                    supplier!=cmbSupplier.Text||medicine!=MediComboCb.Text)return false;
                if(detail==null||!string.Equals(detail.MedicineName,match.MedicineName,StringComparison.OrdinalIgnoreCase)||
                    !string.Equals(detail.SupplierName,match.CompanyName,StringComparison.OrdinalIgnoreCase)||
                    !names.Any(name=>string.Equals(name,match.MedicineName,StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException(Present("تغير الصنف أثناء القراءة. أعد البحث بالباركود."));
                try
                {
                    applyingBarcodeResult=true;
                    isSyncingSupplierSelection=true;
                    SelectSupplierByName(match.CompanyName);
                    MediComboCb.Items.Clear();
                    foreach(string name in names)MediComboCb.Items.Add(name);
                    SelectMedicineByName(match.MedicineName);
                    if(UnitCatalogEnabled){medicineUnitPicker.SetUnits(match.MedicineName,units);txtQuantity.Text="1";CommerceWorkspaceTheme.RefreshLayout(this);}
                    else if(!unifiedSearchPending)txtQuantity.Clear();
                    UiTheme.SetStatus(lblStockInfo,Present("المخزون الحالي")+": "+detail.StockQuantity.ToString("N0"),true);
                    txtBarcode.Clear();
                }
                finally{applyingBarcodeResult=false;isSyncingSupplierSelection=false;}
                if(!unifiedSearchPending){txtQuantity.Focus();txtQuantity.SelectAll();}
                return true;
            }
            catch(OperationCanceledException){return false;}
            catch(Exception exception)
            {
                if(IsDisposed||Disposing||version!=barcodeReadVersion||!session.IsCurrent||barcode!=txtBarcode.Text)return false;
                if(unifiedSearchPending)
                {
                    AppDiagnostics.Log("Purchases:UnifiedSearch",exception);
                    UiTheme.SetStatus(lblStockInfo,Present(exception is MedicineBarcodeAmbiguityException
                        ? "الباركود مكرر. اختر الصنف بالاسم أو صححه في إدارة الأدوية."
                        : "تعذر البحث عن الصنف. أعد المحاولة."),false);
                }
                else UiTheme.ShowOperationIssue(this,"البحث بالباركود",exception);
                MediComboCb.Focus();
                MediComboCb.SelectAll();
                return false;
            }
            finally
            {
                if(ReferenceEquals(barcodeReadCancellation,cancellation))barcodeReadCancellation=null;
                cancellation.Dispose();
            }
        }
        private System.Threading.CancellationTokenSource startupLookupCancellation;
        private long startupLookupVersion;
        private bool startupLookupsReady=true;
        private bool fullStartupPending;
        private void CancelStartupLookups()
        {
            startupLookupVersion++;
            var previous=startupLookupCancellation;startupLookupCancellation=null;
            if(previous!=null){previous.Cancel();previous.Dispose();}
        }
        private async System.Threading.Tasks.Task<bool> LoadPurchaseLookupsAsync()
        {
            if(previewMode||IsDisposed||Disposing||isPurchaseSaved||isSavingPurchase)return false;
            CancelStartupLookups();
            startupLookupsReady=false;
            var request=new System.Threading.CancellationTokenSource();startupLookupCancellation=request;
            long version=startupLookupVersion;string document=currentPurchaseId;
            try
            {
                var session=BarcodeSessionFactory();
                if(session.Lookups==null)throw new InvalidOperationException(Present("مصدر بيانات بداية المشتريات غير متاح."));
                UiTheme.SetStatus(lblStockInfo,Present("جاري قراءة الصنف. انتظر اكتمال القراءة أو غيّر الإدخال لإلغائها."),false);
                if(session.InventoryMode==null)throw new InvalidOperationException(Present("مصدر بيانات بداية المشتريات غير متاح."));
                bool commercialMode=await session.InventoryMode.IsCommercialInventoryEnabledAsync(session.Actor,request.Token);
                if(session.Currency==null)throw new InvalidOperationException("مصدر قراءة عملة المشتريات غير متاح.");
                var currency=await session.Currency.GetSystemCurrencySnapshotAsync(session.Actor,request.Token);
                var suppliers=await session.Lookups.GetSuppliersAsync(session.Actor,request.Token);
                var employees=await session.Lookups.GetEmployeesAsync(session.Actor,request.Token);
                if(request.IsCancellationRequested||IsDisposed||Disposing||version!=startupLookupVersion||
                    !session.IsCurrent||currentPurchaseId!=document||isPurchaseSaved||isSavingPurchase)return false;
                try
                {
                    applyingBarcodeResult=true;isSyncingSupplierSelection=true;
                    if(purchaseSystemCurrencyCode==null)purchaseSystemCurrencyCode=currency.CurrencyCode.Trim().ToUpperInvariant();
                    _commercialInventoryEnabled=commercialMode;InitializePurchaseGrid();
                    cmbSupplier.Items.Clear();SelectEmployee.Items.Clear();
                    foreach(string name in suppliers)cmbSupplier.Items.Add(name);
                    foreach(var employee in employees)SelectEmployee.Items.Add(new ComboBoxItem(employee.EmployeeName,employee.EmployeeId));
                }
                finally{isSyncingSupplierSelection=false;applyingBarcodeResult=false;}
                startupLookupsReady=true;
                return true;
            }
            catch(OperationCanceledException){return false;}
            catch(UnauthorizedAccessException){if(!IsDisposed&&!Disposing&&version==startupLookupVersion)UiTheme.SetStatus(lblStockInfo,Present("تغيرت جلسة المشتريات. أعد البحث."),false);return false;}
            catch(Exception ex){if(!IsDisposed&&!Disposing&&version==startupLookupVersion)SetPurchaseStatusFailure("تحميل شاشة المشتريات",ex);return false;}
            finally{if(ReferenceEquals(startupLookupCancellation,request)){startupLookupCancellation=null;request.Dispose();}}
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
                lblPurchaseEmployee.Text = $"{Present("الموظف")}: {selectedEmployee.Text}";
            }
            else
            {
                SelectEmployee.Text = currentUser;
                lblPurchaseEmployee.Text = $"{Present("الموظف")}: {currentUser}";
            }

            SelectEmployee.Enabled = false;
            UiTheme.ApplyInfoLabels(lblPurchaseEmployee);
        }

        private string GetCurrentCashierName()
        {
            if (SelectEmployee.SelectedValue is ComboBoxItem empItem)
            {
                return empItem.Text;
            }

            return string.IsNullOrWhiteSpace(SessionContext.CurrentUser) ? "غير محدد" : SessionContext.CurrentUser;
        }

        private string GetSupplierName()
        {
            // النص هو المرجع: يمكن للمستخدم كتابة مورد جديد بعد تحديد مورد من القائمة.
            string supplierName = cmbSupplier.Text;
            return string.IsNullOrWhiteSpace(supplierName) ? string.Empty : supplierName.Trim();
        }

        private System.Threading.CancellationTokenSource selectionReadCancellation;
        private long selectionReadVersion;
        private bool selectionReadPending;
        private void CancelSelectionRead()
        {
            selectionReadVersion++;selectionReadPending=false;
            var previous=selectionReadCancellation;selectionReadCancellation=null;
            if(previous!=null){previous.Cancel();previous.Dispose();}
        }
        private void CancelActiveSelectionRead()
        {
            if(selectionReadCancellation!=null)CancelSelectionRead();
        }
        private async void MediComboCb_SelectedIndexChanged(object sender, EventArgs e)
        {
            if(IsDisposed||Disposing||MediComboCb.IsDisposed||MediComboCb.Disposing||previewMode||applyingBarcodeResult||changingContentDirection)return;
            CancelSelectionRead();
            medicineUnitPicker?.ResetSelection();
            if(isPurchaseSaved||isSavingPurchase)return;
            if(MediComboCb.SelectedItem==null)
            {
                UiTheme.SetStatus(lblStockInfo,Present("المخزون الحالي — اختر صنفاً أولاً"));
                txtQuantity.Clear();return;
            }
            string selectedMedicine=Convert.ToString(MediComboCb.SelectedItem),selectionText=MediComboCb.Text;
            string document=currentPurchaseId,supplier=cmbSupplier.Text;
            long version=selectionReadVersion;selectionReadPending=true;
            System.Threading.CancellationTokenSource request=null;
            try
            {
                await System.Threading.Tasks.Task.Yield();
                if(IsDisposed||Disposing||version!=selectionReadVersion||isPurchaseSaved||isSavingPurchase||
                    Convert.ToString(MediComboCb.SelectedItem)!=selectedMedicine||MediComboCb.Text!=selectionText||
                    currentPurchaseId!=document||cmbSupplier.Text!=supplier)return;
                request=new System.Threading.CancellationTokenSource();selectionReadCancellation=request;
                var session=BarcodeSessionFactory();
                UiTheme.SetStatus(lblStockInfo,Present("جاري قراءة الصنف. انتظر اكتمال القراءة أو غيّر الإدخال لإلغائها."),false);
                var medicine=await session.Medicines.FindAsync(selectedMedicine,session.Actor,request.Token);
                var units=UnitCatalogEnabled?await System.Threading.Tasks.Task.Run(()=>MedicineUnitInvoiceBridge.ReadUnits(selectedMedicine,false),request.Token):null;
                if(request.IsCancellationRequested||IsDisposed||Disposing||version!=selectionReadVersion||!session.IsCurrent||
                    isPurchaseSaved||isSavingPurchase||Convert.ToString(MediComboCb.SelectedItem)!=selectedMedicine||
                    MediComboCb.Text!=selectionText||currentPurchaseId!=document||cmbSupplier.Text!=supplier)return;
                if(medicine==null||!string.Equals(medicine.MedicineName,selectedMedicine,StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(Present("لم يعد الدواء المحدد موجودًا. حدّث القائمة ثم أعد المحاولة."));
                if(UnitCatalogEnabled){medicineUnitPicker.SetUnits(selectedMedicine,units);txtQuantity.Text="1";CommerceWorkspaceTheme.RefreshLayout(this);}
                UiTheme.SetStatus(lblStockInfo,string.Format(Present("المخزون الحالي: {0} وحدة | سعر الشراء: {1}"),medicine.StockQuantity,FormatMoney(medicineUnitPicker?.SelectedUnit?.UnitPrice??medicine.BuyingPrice)));
                if(!string.IsNullOrEmpty(medicine.SupplierName))
                {
                    try{isSyncingSupplierSelection=true;applyingBarcodeResult=true;SelectSupplierByName(medicine.SupplierName);}
                    finally{isSyncingSupplierSelection=false;applyingBarcodeResult=false;}

                }
                if(!string.IsNullOrEmpty(txtQuantity.Text))txtQuantity_TextChanged(txtQuantity,EventArgs.Empty);
            }
            catch(OperationCanceledException){}
            catch(UnauthorizedAccessException){if(!IsDisposed&&!Disposing&&version==selectionReadVersion)UiTheme.SetStatus(lblStockInfo,Present("تغيرت جلسة المشتريات. أعد البحث."),false);}
            catch(Exception ex){if(!IsDisposed&&!Disposing&&version==selectionReadVersion)SetPurchaseStatusFailure("التحقق من المخزون",ex);}
            finally
            {
                if(version==selectionReadVersion){selectionReadPending=false;selectionReadCancellation=null;if(request!=null)request.Dispose();}
            }
        }
        private System.Threading.CancellationTokenSource quantityReadCancellation;
        private long quantityReadVersion;
        private bool quantityReadPending;
        private void CancelQuantityRead()
        {
            quantityReadVersion++;quantityReadPending=false;
            var previous=quantityReadCancellation;quantityReadCancellation=null;
            if(previous!=null){previous.Cancel();previous.Dispose();}
        }
        private async void txtQuantity_TextChanged(object sender, EventArgs e)
        {
            if(previewMode||applyingBarcodeResult)return;
            CancelQuantityRead();
            string value=txtQuantity.Text,medicineName=Convert.ToString(MediComboCb.SelectedItem),supplier=cmbSupplier.Text,document=currentPurchaseId;
            if(isPurchaseSaved||isSavingPurchase||string.IsNullOrEmpty(medicineName)||
                !MedicineUnitPicker.TryQuantity(value,out decimal quantity))return;
            try { var unit=medicineUnitPicker?.SelectedUnit?.WithQuantity(quantity); if(unit!=null)quantity=unit.BaseQuantity; }
            catch(Exception error) when(error is ArgumentException||error is InvalidOperationException||error is OverflowException) { UiTheme.SetStatus(lblStockInfo,error.Message,false);return; }
            long version=quantityReadVersion;quantityReadPending=true;
            System.Threading.CancellationTokenSource request=null;
            try
            {
                // Let the existing text-change cancellation hook finish before opening this read.
                await System.Threading.Tasks.Task.Yield();
                if(IsDisposed||Disposing||version!=quantityReadVersion||barcodeReadCancellation!=null||
                    txtQuantity.Text!=value||Convert.ToString(MediComboCb.SelectedItem)!=medicineName||
                    cmbSupplier.Text!=supplier||currentPurchaseId!=document||isPurchaseSaved||isSavingPurchase)return;
                var session=BarcodeSessionFactory();
                request=new System.Threading.CancellationTokenSource();quantityReadCancellation=request;
                var medicine=await session.Medicines.FindAsync(medicineName,session.Actor,request.Token);
                if(request.IsCancellationRequested||IsDisposed||Disposing||version!=quantityReadVersion||
                    !session.IsCurrent||txtQuantity.Text!=value||Convert.ToString(MediComboCb.SelectedItem)!=medicineName||
                    cmbSupplier.Text!=supplier||currentPurchaseId!=document||isPurchaseSaved||isSavingPurchase)return;
                int available=medicine==null?0:medicine.StockQuantity;
                if(available>0 && quantity>(decimal)available*10)
                    UiTheme.SetStatus(lblStockInfo,Present("تنبيه: الكمية المطلوبة كبيرة جداً!"),false);
                else if(medicine!=null)
                    UiTheme.SetStatus(lblStockInfo,string.Format(Present("المخزون الحالي: {0} وحدة | سعر الشراء: {1}"),available,FormatMoney(medicineUnitPicker?.SelectedUnit?.UnitPrice??medicine.BuyingPrice)));
                else
                    UiTheme.SetStatus(lblStockInfo,string.Format(Present("المخزون الحالي: {0}"),available),true);
            }
            catch(OperationCanceledException){}
            catch(UnauthorizedAccessException){if(!IsDisposed&&!Disposing&&version==quantityReadVersion)UiTheme.SetStatus(lblStockInfo,Present("تغيرت جلسة المشتريات. أعد البحث."),false);}
            catch(Exception ex){if(!IsDisposed&&!Disposing&&version==quantityReadVersion)SetPurchaseStatusFailure("قراءة المخزون",ex);}
            finally
            {
                if(version==quantityReadVersion){quantityReadPending=false;quantityReadCancellation=null;if(request!=null)request.Dispose();}
            }
        }
        private bool reservingPurchaseNumber;
        private string CapturePurchaseDraftState()
        {
            var state=new System.Text.StringBuilder();
            Action<object> add=value=>{string text=Convert.ToString(value,System.Globalization.CultureInfo.InvariantCulture)??string.Empty;state.Append(text.Length).Append(':').Append(text);};
            add(GetSupplierName());add(GetCurrentCashierName());add(txtPurchaseNotesInput.Text);
            add(_commercialInventoryEnabled);add(MediComboCb.Text);add(txtQuantity.Text);
            add(dtpProdDate.Value.HasValue?(object)dtpProdDate.Value.Value.Ticks:null);add(dtpExpDate.Value.HasValue?(object)dtpExpDate.Value.Value.Ticks:null);
            add(PurchasesGV.Columns.Count);add(PurchasesGV.Rows.Count);
            foreach(DataGridViewColumn column in PurchasesGV.Columns)add(column.Name);
            foreach(DataGridViewRow row in PurchasesGV.Rows){
                add(row.IsNewRow);
                foreach(DataGridViewCell cell in row.Cells)add(cell.Value);
            }
            return state.ToString();
        }
        private async System.Threading.Tasks.Task<bool> EnsurePurchaseNumberAsync()
        {
            if(!string.IsNullOrEmpty(currentPurchaseId))return true;
            if(previewMode||IsDisposed||Disposing||isPurchaseSaved||isSavingPurchase||RejectPendingBarcodeAction())return false;
            if(!PermissionService.CanExecute(AppOperation.PostPurchaseInvoice))return false;
            string document=currentPurchaseId,state=CapturePurchaseDraftState();
            CancelBarcodeRead();
            var request=new System.Threading.CancellationTokenSource();
            barcodeReadCancellation=request;reservingPurchaseNumber=true;
            long version=barcodeReadVersion;
            try{
                var session=BarcodeSessionFactory();
                if(session.Numbers==null)throw new InvalidOperationException(Present("مصدر بيانات بداية المشتريات غير متاح."));
                UiTheme.SetStatus(lblStockInfo,Present("جاري حجز رقم فاتورة المشتريات. انتظر اكتمال الحجز."),false);
                long number=await session.Numbers.ReserveAsync(PurchaseDraftNumberKind.Purchase,session.Actor,request.Token);
                if(request.IsCancellationRequested||IsDisposed||Disposing||version!=barcodeReadVersion||
                    !session.IsCurrent||isPurchaseSaved||isSavingPurchase||currentPurchaseId!=document||
                    !PermissionService.CanExecute(AppOperation.PostPurchaseInvoice)||CapturePurchaseDraftState()!=state)return false;
                currentPurchaseId=number.ToString(System.Globalization.CultureInfo.InvariantCulture);
                return true;
            }
            catch(OperationCanceledException){return false;}
            catch(UnauthorizedAccessException){
                if(!IsDisposed&&!Disposing&&version==barcodeReadVersion)UiTheme.SetStatus(lblStockInfo,Present("تغيرت جلسة المشتريات. أعد البحث."),false);
                return false;
            }
            catch(Exception ex){
                if(!IsDisposed&&!Disposing&&version==barcodeReadVersion)SetPurchaseStatusFailure("حجز رقم فاتورة المشتريات",ex);
                return false;
            }
            finally{
                if(ReferenceEquals(barcodeReadCancellation,request)){barcodeReadCancellation=null;reservingPurchaseNumber=false;request.Dispose();}
            }
        }
        private string GenerateUniquePurchaseId()
        {
            try
            {
                return PurchaseDraftSequenceService.GetNextPurchaseId().ToString();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("تعذر حجز رقم فاتورة المشتريات.",ex);
            }
        }

        private string GenerateUniqueDetailId()
        {
            try
            {
                long nextId = PurchaseDraftSequenceService.GetNextPurchaseDetailId();

                HashSet<long> usedIds = new HashSet<long>();
                foreach (DataGridViewRow row in PurchasesGV.Rows)
                {
                    if (row.IsNewRow) continue;
                    if (row.Cells["DetailId"].Value != null &&
                        long.TryParse(row.Cells["DetailId"].Value.ToString(), out long existingId))
                    {
                        usedIds.Add(existingId);
                    }
                }

                while (usedIds.Contains(nextId))
                {
                    nextId = PurchaseDraftSequenceService.GetNextPurchaseDetailId();
                }

                return nextId.ToString();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("تعذر حجز رقم بند المشتريات.",ex);
            }
        }

        private void ResetPurchaseDates()
        {
            dtpProdDate.Value = DateTime.Today;
            dtpExpDate.Value = DateTime.Today.AddYears(1);
        }

        private async void btnAddToPurchase_Click(object sender, EventArgs e) { await AddMedicineToPurchaseAsync(); }

        private async System.Threading.Tasks.Task AddMedicineToPurchaseAsync()
        {
            if (!dtpProdDate.Value.HasValue || !dtpExpDate.Value.HasValue) { RefreshPurchaseDateWarnings(); return; }
            if(RejectPendingBarcodeAction())return; 
            if(isPurchaseSaved||isSavingPurchase||IsDisposed||Disposing)return;
            System.Threading.CancellationTokenSource addRead=null;
            try
            {
                if (MediComboCb.SelectedItem == null)
                {
                    AppNotifications.Show(Present("يرجى اختيار دواء."));
                    return;
                }

                if (!MedicineUnitPicker.TryQuantity(txtQuantity.Text, out decimal quantity))
                {
                    AppNotifications.Show(Present("يرجى إدخال كمية صحيحة."));
                    return;
                }

                string selectedMedicine = Convert.ToString(MediComboCb.SelectedItem);
                var unit=UnitCatalogEnabled?medicineUnitPicker.CaptureSelection(selectedMedicine,quantity):null;
                if(unit==null && quantity!=decimal.Truncate(quantity))throw new InvalidOperationException("أدخل كمية صحيحة للصنف بلا وحدات محفوظة.");
                int baseQuantity=unit?.BaseQuantity??checked((int)quantity);
                var session=BarcodeSessionFactory();
                string quantityText=txtQuantity.Text,supplierText=cmbSupplier.Text;
                string document=currentPurchaseId;
                int rowCount=PurchasesGV.Rows.Count;
                var production=dtpProdDate.Value;var expiry=dtpExpDate.Value;
                CancelBarcodeRead();
                addRead=new System.Threading.CancellationTokenSource();
                barcodeReadCancellation=addRead;
                readingMedicinePrice=true;
                long version=barcodeReadVersion;
                UiTheme.SetStatus(lblStockInfo,Present("جاري قراءة الصنف. انتظر اكتمال القراءة أو غيّر الإدخال لإلغائها."),false);
                var snapshot=await session.Medicines.FindAsync(selectedMedicine,session.Actor,addRead.Token);
                if(addRead.IsCancellationRequested||IsDisposed||Disposing||version!=barcodeReadVersion||
                    !session.IsCurrent||isPurchaseSaved||isSavingPurchase||currentPurchaseId!=document||
                    Convert.ToString(MediComboCb.SelectedItem)!=selectedMedicine||txtQuantity.Text!=quantityText||
                    cmbSupplier.Text!=supplierText||PurchasesGV.Rows.Count!=rowCount||
                    dtpProdDate.Value!=production||dtpExpDate.Value!=expiry||(UnitCatalogEnabled&&!MedicineUnitPicker.Same(unit,medicineUnitPicker.SelectedUnit)))return;
                if(snapshot==null||!string.Equals(snapshot.MedicineName,selectedMedicine,StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(Present("لم يعد الدواء المحدد موجودًا. حدّث القائمة ثم أعد المحاولة."));
                decimal buyingPrice=unit?.UnitPrice??snapshot.BuyingPrice;

                if (buyingPrice <= 0)
                {
                    AppNotifications.Show(Present("فشل في جلب سعر الشراء للدواء المحدد."));
                    return;
                }

                if (buyingPrice * quantity > 9999999999999999.99m)
                {
                    AppNotifications.Show(Present("إجمالي بند الشراء أكبر من الحد المسموح."), Present("المشتريات"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                decimal totalAmount = unit?.GrossTotal??checked(buyingPrice * quantity);
                DateTime prodDate = dtpProdDate.Value.GetValueOrDefault(DateTime.Today).Date;
                DateTime expDate = dtpExpDate.Value.GetValueOrDefault(DateTime.Today.AddYears(1)).Date;

                if (prodDate > DateTime.Today)
                {
                    AppNotifications.Show(Present("تاريخ الإنتاج لا يمكن أن يكون في المستقبل."));
                    return;
                }

                if (expDate <= prodDate)
                {
                    AppNotifications.Show(Present("تاريخ الانتهاء يجب أن يكون بعد تاريخ الإنتاج."));
                    return;
                }

                bool medicineExists = false;
                foreach (DataGridViewRow row in PurchasesGV.Rows)
                {
                    if (row.IsNewRow) continue;
                    bool hasSameMedicine = row.Cells["MedName"].Value != null
                        && row.Cells["MedName"].Value.ToString() == selectedMedicine;
                    DateTime existingProductionDate;
                    DateTime existingExpiryDate;
                    bool hasSameDates = DateTime.TryParse(Convert.ToString(row.Cells["ProdDate"].Value), out existingProductionDate)
                        && DateTime.TryParse(Convert.ToString(row.Cells["ExpDate"].Value), out existingExpiryDate)
                        && existingProductionDate.Date == prodDate
                        && existingExpiryDate.Date == expDate;

                    if (!_commercialInventoryEnabled && hasSameMedicine && hasSameDates && MedicineUnitPicker.Same(CartUnit(row),unit) && Convert.ToDecimal(row.Cells["BuyingPrice"].Value)==buyingPrice)
                    {
                        medicineExists = true;
                        DialogResult dialogResult = AppNotifications.Show(Present("هذا الدواء موجود بالفعل. هل تريد تعديل الكمية؟"), Present("تعديل الكمية"), MessageBoxButtons.YesNo);
                        if (dialogResult == DialogResult.Yes)
                        {
                            if(addRead.IsCancellationRequested||!session.IsCurrent||isPurchaseSaved||isSavingPurchase||
                                currentPurchaseId!=document||txtQuantity.Text!=quantityText||
                                Convert.ToString(MediComboCb.SelectedItem)!=selectedMedicine||cmbSupplier.Text!=supplierText||
                                dtpProdDate.Value!=production||dtpExpDate.Value!=expiry||(UnitCatalogEnabled&&!MedicineUnitPicker.Same(unit,medicineUnitPicker.SelectedUnit))||row.DataGridView!=PurchasesGV)return;
                            if (!AcceptPurchaseLineTotal(totalAmount, row)) return;
                            row.Cells["Quantity"].Value = quantity;
                            row.Cells["TotalAmount"].Value = totalAmount;
                            if(unit!=null)cartUnits[row]=unit; else cartUnits.Remove(row);
                            row.Cells["Unit"].Value=unit?.Name??"—";
                            row.Tag = buyingPrice;
                            row.Cells["BuyingPrice"].Value = buyingPrice;
                            row.Cells["ProdDate"].Value = prodDate.ToString("yyyy-MM-dd");
                            row.Cells["ExpDate"].Value = expDate.ToString("yyyy-MM-dd");
                        }
                        else
                        {
                            return;
                        }
                        break;
                    }
                }


                if (!medicineExists)
                {
                    if (!AcceptPurchaseLineTotal(totalAmount, null)) return;
                    if(session.Numbers==null)throw new InvalidOperationException(Present("مصدر بيانات بداية المشتريات غير متاح."));
                    var usedDetailIds=new HashSet<long>();
                    foreach(DataGridViewRow existing in PurchasesGV.Rows){
                        long used;
                        if(!existing.IsNewRow&&long.TryParse(Convert.ToString(existing.Cells["DetailId"].Value),out used))usedDetailIds.Add(used);
                    }
                    long reservedDetail;
                    do{reservedDetail=await session.Numbers.ReserveAsync(PurchaseDraftNumberKind.Detail,session.Actor,addRead.Token);}
                    while(usedDetailIds.Contains(reservedDetail));
                    string detailId=reservedDetail.ToString();
                    if(addRead.IsCancellationRequested||!session.IsCurrent||isPurchaseSaved||isSavingPurchase||
                        currentPurchaseId!=document||txtQuantity.Text!=quantityText||
                        Convert.ToString(MediComboCb.SelectedItem)!=selectedMedicine||cmbSupplier.Text!=supplierText||
                        dtpProdDate.Value!=production||dtpExpDate.Value!=expiry||(UnitCatalogEnabled&&!MedicineUnitPicker.Same(unit,medicineUnitPicker.SelectedUnit))||PurchasesGV.Rows.Count!=rowCount)return;
                    if (!AcceptPurchaseLineTotal(totalAmount, null)) return;
                    int rowIndex = PurchasesGV.Rows.Add();
                    DataGridViewRow newRow = PurchasesGV.Rows[rowIndex];
                    newRow.Cells["DetailId"].Value = detailId;
                    newRow.Cells["MedName"].Value = selectedMedicine;
                    newRow.Tag = buyingPrice;
                    if(unit!=null)cartUnits[newRow]=unit;
                    newRow.Cells["Unit"].Value=unit?.Name??"—";
                    newRow.Cells["BuyingPrice"].Value = buyingPrice;
                    newRow.Cells["Quantity"].Value = quantity;
                    newRow.Cells["ProdDate"].Value = prodDate.ToString("yyyy-MM-dd");
                    newRow.Cells["ExpDate"].Value = expDate.ToString("yyyy-MM-dd");
                    newRow.Cells["TotalAmount"].Value = totalAmount;
                    if (_commercialInventoryEnabled)
                    {
                        newRow.Cells["Gtin"].Value = string.Empty;
                        newRow.Cells["BatchNumber"].Value = string.Empty;
                        newRow.Cells["SerialNumber"].Value = string.Empty;
                    }
                }
                UpdateTotalAmount();

                MediComboCb.SelectedIndex = -1;
                MediComboCb.Text = Present("اختر الدواء");
                txtQuantity.Clear();
                ResetPurchaseDates();
            }
            catch (OperationCanceledException) {}
            catch (UnauthorizedAccessException) {if(!IsDisposed&&!Disposing)UiTheme.SetStatus(lblStockInfo,Present("تغيرت جلسة المشتريات. أعد البحث."),false);}
            catch (Exception ex)
            {
                if(!IsDisposed&&!Disposing)UiTheme.ShowOperationIssue(this, "إضافة الصنف إلى المشتريات", ex);
            }
            finally
            {
                if(addRead!=null&&ReferenceEquals(barcodeReadCancellation,addRead))
                {barcodeReadCancellation=null;readingMedicinePrice=false;addRead.Dispose();}
            }
        }

        private int GetBuyingPrice(string medName)
        {
            try
            {
                return ApplicationServices.PurchaseWorkspace.FindMedicine(medName)?.BuyingPrice ?? 0;
            }
            catch (Exception ex)
            {
                UiTheme.ShowOperationIssue(this, "جلب سعر الشراء", ex);
            }
            return 0;
        }

        private void UpdateTotalAmount()
        {
            decimal totalAmount = 0m;
            foreach (DataGridViewRow row in PurchasesGV.Rows)
            {
                if (row.IsNewRow) continue;
                if (row.Cells["TotalAmount"].Value != null)
                {
                    totalAmount += Convert.ToDecimal(row.Cells["TotalAmount"].Value);
                }
            }
            PriceBDLabel.Text = $"{Present("الإجمالي")}: {FormatMoney(totalAmount)}";
            PriceADLabel.Text = $"{Present("الصافي")}: {FormatMoney(totalAmount)}";
            UiTheme.ApplyInfoLabels(PriceBDLabel, PriceADLabel);
            CommerceWorkspaceTheme.RefreshTotals(this);
        }

        private void PurchasesGV_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if(isSavingPurchase||purchaseRecoveryUnresolved)return;
            if (e.RowIndex < 0 || e.ColumnIndex < 0 || PurchasesGV.Columns[e.ColumnIndex].Name != "RemoveCartItem") return;
            if (isPurchaseSaved)
            {
                AppNotifications.Show(Present("تم اعتماد فاتورة المشتريات. افتح فاتورة جديدة قبل تعديل البنود."), Present("المشتريات"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (e.RowIndex >= 0 && e.ColumnIndex >= 0 && PurchasesGV.Columns[e.ColumnIndex].Name == "RemoveCartItem")
            {
                DialogResult result = AppNotifications.Show(Present("هل تريد حذف هذا الصنف من القائمة؟"), Present("تأكيد الحذف"), MessageBoxButtons.YesNo);
                if (result == DialogResult.Yes)
                {
                    PurchasesGV.Rows.RemoveAt(e.RowIndex);
                    UpdateTotalAmount();
                }
            }
        }

        private void RollbackPurchaseTransaction()
        {
            _sessionPurchaseDetailIds.Clear();
            currentPurchaseId = string.Empty;
            isPurchaseSaved = false;
        }

        private void Clear()
        {
            if(purchaseRecovery!=null&&purchaseRecovery.Pending()!=null)throw new InvalidOperationException(Present("توجد محاولة حفظ غير محسومة؛ تحقق من نتيجتها قبل مسح الفاتورة."));
            recoveredPurchaseCancelled=false;purchaseBusinessDate=null;purchaseRecoveryUnresolved=false;
            PriceBDLabel.Text = $"{Present("الإجمالي")}: {FormatMoney(0)}";
            PriceADLabel.Text = $"{Present("الصافي")}: {FormatMoney(0)}";
            PurchasesGV.Rows.Clear();savedUnitPresentation=null;acceptedUnitOrigin=null;
            cmbSupplier.SelectedIndex = -1;
            cmbSupplier.Text = "";
            txtPurchaseNotesInput.Clear();
            currentPurchaseId = string.Empty;
            isPurchaseSaved = false;
            PurchasesGV.ReadOnly = false;
            lblPurchaseSupplier.Text = Present("المورد") + ":";
            ApplyCurrentCashier();
             lblPurchaseTiming.Text = Present("التوقيت") + ":";
            if(!previewMode){
                purchaseSystemCurrencyCode=null;
                _ = ReloadNewPurchaseCurrencyAsync();
            }
        }
        private async System.Threading.Tasks.Task ReloadNewPurchaseCurrencyAsync()
        {
            if(await LoadPurchaseLookupsAsync()){
                ApplyCurrentCashier();UpdateTotalAmount();
            }
        }

        private void btnClearPurchase_Click(object sender, EventArgs e)
        {
            if(purchaseRecoveryUnresolved){UiTheme.SetStatus(lblStockInfo,Present("نتيجة الحفظ غير محسومة؛ أعد فتح الشاشة للتحقق. احتُفظ بالمحاولة."),false);return;}
            if (PurchasesGV.Rows.Count == 0)
            {
                Clear();
                return;
            }

            if (isPurchaseSaved)
            {
                Clear();
                AppNotifications.Show(Present("تم فتح فاتورة مشتريات جديدة."), Present("المشتريات"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (PurchasesGV.Rows.Count > 0)
            {
                DialogResult result = AppNotifications.Show(Present("هل تريد مسح جميع البنود؟"), Present("تأكيد المسح"), MessageBoxButtons.YesNo);
                if (result == DialogResult.Yes)
                {
                    RollbackPurchaseTransaction();
                    Clear();
                }
            }
        }

        private async void btnConfirmPurchase_Click(object sender, EventArgs e)
        {
            if(RejectPendingBarcodeAction())return;
            if (!PermissionService.EnsureAllowed(AppOperation.PostPurchaseInvoice, this))
            {
                return;
            }

            if (PurchasesGV.Rows.Count == 0)
            {
                AppNotifications.Show(Present("لا توجد بنود في القائمة."));
                return;
            }

            if (string.IsNullOrWhiteSpace(GetSupplierName()))
            {
                AppNotifications.Show(Present("يرجى اختيار مورد."));
                return;
            }

            try
            {
                string supplierName = GetSupplierName();
                string employeeName = GetCurrentCashierName();

                if (string.IsNullOrEmpty(currentPurchaseId)&&!purchaseRecoveryUnresolved)
                {
                    if(!await EnsurePurchaseNumberAsync())return;
                }

                if (!isPurchaseSaved || purchaseRecoveryUnresolved)
                {
                    if (!await SavePurchaseToDatabaseAsync())
                    {
                        return;
                    }
                }

                lblPurchaseSupplier.Text = Present("المورد") + ": " + supplierName;
                lblPurchaseEmployee.Text = Present("الموظف") + ": " + employeeName;
                lblPurchaseTiming.Text = FormatPurchaseTiming(purchaseBusinessDate ?? DateTime.Now);

                UiTheme.ApplyInfoLabels(lblPurchaseSupplier, lblPurchaseEmployee, lblPurchaseTiming);
                RefreshPurchaseHistoryIfVisible();

                AppNotifications.Show(string.Format(Present("تم حفظ واعتماد المشتريات بنجاح.\nرقم الفاتورة: {0}"), currentPurchaseId), Present("نجاح"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch(OperationCanceledException){ }
            catch (Exception ex)
            {
                if(IsDisposed||Disposing)return;
                ShowPurchaseSaveFailure("اعتماد فاتورة المشتريات",ex);
            }
        }

        private async void btnSaveOnly_Click(object sender, EventArgs e)
        {
            if(RejectPendingBarcodeAction())return;
            if (!PermissionService.EnsureAllowed(AppOperation.PostPurchaseInvoice, this))
            {
                return;
            }

            if (PurchasesGV.Rows.Count == 0)
            {
                AppNotifications.Show(Present("لا توجد بنود في القائمة."));
                return;
            }
            if (string.IsNullOrWhiteSpace(GetSupplierName()))
            {
                AppNotifications.Show(Present("يرجى اختيار مورد."));
                return;
            }

            try
            {
                if (isPurchaseSaved)
                {
                    if(purchaseRecoveryUnresolved && !await SettleSavedPurchaseAsync())return;
                    AppNotifications.Show(Present("تم حفظ هذه المشتريات بالفعل."), Present("حفظ"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                if (string.IsNullOrEmpty(currentPurchaseId)&&!purchaseRecoveryUnresolved)
                {
                    if(!await EnsurePurchaseNumberAsync())return;
                }
                if (!await SavePurchaseToDatabaseAsync())
                {
                    return;
                }
                UpdatePurchaseLabels();
                RefreshPurchaseHistoryIfVisible();
                AppNotifications.Show(string.Format(Present("تم حفظ المشتريات بنجاح.\nرقم الفاتورة: {0}"), currentPurchaseId), Present("حفظ"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch(OperationCanceledException){ }
            catch (Exception ex)
            {
                if(IsDisposed||Disposing)return;
                ShowPurchaseSaveFailure("حفظ فاتورة المشتريات",ex);
            }
        }

        private async void btnSaveAndPrint_Click(object sender, EventArgs e)
        {
            if(invoicePrintSelectionPending)return;
            if(RejectPendingBarcodeAction())return;
            if (!PermissionService.EnsureAllowed(AppOperation.PostPurchaseInvoice, this))
            {
                return;
            }

            if (PurchasesGV.Rows.Count == 0)
            {
                AppNotifications.Show(Present("لا توجد بيانات للطباعة."));
                return;
            }
            if (string.IsNullOrWhiteSpace(GetSupplierName()))
            {
                AppNotifications.Show(Present("يرجى اختيار مورد."));
                return;
            }

            try
            {
                if (string.IsNullOrEmpty(currentPurchaseId)&&!purchaseRecoveryUnresolved)
                {
                    if(!await EnsurePurchaseNumberAsync())return;
                }

                if (!isPurchaseSaved || purchaseRecoveryUnresolved)
                {
                    if (!await SavePurchaseToDatabaseAsync())
                    {
                        return;
                    }
                }
                UpdatePurchaseLabels();
                RefreshPurchaseHistoryIfVisible();

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
                    if (useReceipt) { printDoc.BeginPrint += BeginPurchaseThermalPrint; printDoc.EndPrint += EndPurchaseThermalPrint; printDoc.PrintPage += PrintSmallPurchaseReceipt; } else { printDoc.BeginPrint += BeginPurchaseA4Print; printDoc.EndPrint += EndPurchaseA4Print; printDoc.PrintPage += PrintPurchasePage; }
                    using (PrintPreviewDialog preview = new PrintPreviewDialog { Document = printDoc, Text = useReceipt ? Present("معاينة طباعة إيصال المشتريات") : Present("معاينة طباعة فاتورة المشتريات A4") })
                    {
                        ShowInvoicePreview(preview);
                    }
                    if (useReceipt) printDoc.PrintPage -= PrintSmallPurchaseReceipt; else printDoc.PrintPage -= PrintPurchasePage;
                }
                    }finally{invoicePrintSelection=null;ReleaseInvoicePrintJobs();}
                }
            }
            catch (Exception ex)
            {
                ShowPurchaseOutputFailure("الطباعة", ex);
            }
        }

        private async void btnSaveAndPrintA4_Click(object sender, EventArgs e)
        {
            if(invoicePrintSelectionPending)return;
            if(RejectPendingBarcodeAction())return;
            if (!PermissionService.EnsureAllowed(AppOperation.PostPurchaseInvoice, this))
            {
                return;
            }

            if (PurchasesGV.Rows.Count == 0)
            {
                AppNotifications.Show(Present("لا توجد بيانات للطباعة."));
                return;
            }
            if (string.IsNullOrWhiteSpace(GetSupplierName()))
            {
                AppNotifications.Show(Present("يرجى اختيار مورد."));
                return;
            }

            try
            {
                if (string.IsNullOrEmpty(currentPurchaseId)&&!purchaseRecoveryUnresolved)
                {
                    if(!await EnsurePurchaseNumberAsync())return;
                }

                if (!isPurchaseSaved || purchaseRecoveryUnresolved)
                {
                    if (!await SavePurchaseToDatabaseAsync())
                    {
                        return;
                    }
                }
                UpdatePurchaseLabels();
                RefreshPurchaseHistoryIfVisible();

                using(var selection=await ChooseInvoiceIdentityAsync())
                {
                    if(selection==null)return;invoicePrintSelection=selection;
                    try{
                using (PrintDocument printDoc = new PrintDocument())
                {
                    DeviceSettings.ApplyPreferredPrinter(printDoc);
                    printDoc.DefaultPageSettings.PaperSize = DeviceSettings.CreateA4PaperSize();
                    printDoc.DefaultPageSettings.Margins = new Margins(40, 40, 40, 40);
                    printDoc.BeginPrint += BeginPurchaseA4Print; printDoc.EndPrint += EndPurchaseA4Print;
                    printDoc.PrintPage += PrintPurchasePage;
                    using (PrintPreviewDialog preview = new PrintPreviewDialog { Document = printDoc, Text = "معاينة طباعة فاتورة المشتريات A4" })
                    {
                        ShowInvoicePreview(preview);
                    }
                    printDoc.PrintPage -= PrintPurchasePage;
                }
                    }finally{invoicePrintSelection=null;ReleaseInvoicePrintJobs();}
                }
            }
            catch (Exception ex)
            {
                ShowPurchaseOutputFailure("طباعة A4", ex);
            }
        }

        private bool AcceptPurchaseLineTotal(decimal lineTotal, DataGridViewRow replacedRow)
        {
            if (!CommitPurchaseCartEdit()) return false;
            decimal total = lineTotal;
            foreach (DataGridViewRow row in PurchasesGV.Rows)
            {
                if (row.IsNewRow || row == replacedRow) continue;
                decimal amount;
                if (!decimal.TryParse(Convert.ToString(row.Cells["TotalAmount"].Value), out amount) || amount < 0)
                {
                    UiTheme.SetStatus(lblStockInfo, presentationLeftToRight ? "Review the cart totals before adding an item." : "راجع إجماليات السلة قبل إضافة الصنف.", false);
                    return false;
                }
                total += amount;
                if (total > 9999999999999999.99m) break;
            }
            if (lineTotal < 0 || total > 9999999999999999.99m)
            {
                UiTheme.SetStatus(lblStockInfo, presentationLeftToRight ? "The invoice total exceeds the allowed limit. Reduce the quantity or use another invoice." : "إجمالي الفاتورة أكبر من الحد المسموح. خفّض الكمية أو استخدم فاتورة أخرى.", false);
                return false;
            }
            return true;
        }

        private bool CommitPurchaseCartEdit()
        {
            if (!PurchasesGV.IsCurrentCellInEditMode || PurchasesGV.CurrentCell == null) return true;
            var cell = PurchasesGV.CurrentCell;
            return ValidatePurchaseCartCell(cell.RowIndex, cell.ColumnIndex, cell.EditedFormattedValue) && PurchasesGV.EndEdit();
        }
        private void PurchasesGV_CellBeginEdit(object sender, DataGridViewCellCancelEventArgs e)
        {
            if (isPurchaseSaved || isSavingPurchase || purchaseRecoveryUnresolved || recoveredPurchaseCancelled || invoicePrintSelectionPending)
                e.Cancel = true;
        }

        private void PurchasesGV_CellValidating(object sender, DataGridViewCellValidatingEventArgs e)
        {
            e.Cancel = !ValidatePurchaseCartCell(e.RowIndex, e.ColumnIndex, e.FormattedValue);
        }

        private bool ValidatePurchaseCartCell(int rowIndex, int columnIndex, object formattedValue)
        {
            if (rowIndex < 0) return true;
            string column = PurchasesGV.Columns[columnIndex].Name;
            if (column != "BuyingPrice" && column != "Quantity") return true;
            DataGridViewRow row = PurchasesGV.Rows[rowIndex];
            string error = null;
            try
            {
                if(!MedicineUnitPicker.TryQuantity(Convert.ToString(formattedValue),out decimal value))throw new InvalidOperationException(Present("أدخل سعرًا أو كمية موجبة صالحة."));
                decimal quantity=column=="Quantity"?value:Convert.ToDecimal(row.Cells["Quantity"].Value);
                decimal price=column=="BuyingPrice"?value:Convert.ToDecimal(row.Cells["BuyingPrice"].Value);
                var unit=CartUnit(row)?.WithQuantity(quantity).WithPrice(price);
                if(unit==null && (quantity!=decimal.Truncate(quantity)||price!=decimal.Truncate(price)))
                    throw new InvalidOperationException(Present("الصنف بلا وحدات محفوظة؛ أدخل سعرًا وكمية صحيحين."));
                decimal total=unit?.GrossTotal??checked(quantity*price);
                decimal otherTotal=PurchasesGV.Rows.Cast<DataGridViewRow>().Where(r=>!r.IsNewRow&&r!=row).Sum(r=>Convert.ToDecimal(r.Cells["TotalAmount"].Value??0));
                if(total+otherTotal>9999999999999999.99m)throw new InvalidOperationException(Present("إجمالي الفاتورة أكبر من الحد المسموح."));
            }
            catch(Exception ex) when(ex is InvalidOperationException||ex is ArgumentException||ex is OverflowException) { error=ex.Message; }
            row.Cells[column].ErrorText = error ?? string.Empty;
            if (error != null) UiTheme.SetStatus(lblStockInfo, error, false);
            return error == null;
        }

        private void PurchasesGV_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            string column = PurchasesGV.Columns[e.ColumnIndex].Name;
            if (column != "BuyingPrice" && column != "Quantity") return;
            DataGridViewRow row = PurchasesGV.Rows[e.RowIndex];
            row.Cells[column].ErrorText = string.Empty;
            decimal buyingPrice=Convert.ToDecimal(row.Cells["BuyingPrice"].Value);
            decimal quantity=Convert.ToDecimal(row.Cells["Quantity"].Value);
            var unit=CartUnit(row)?.WithQuantity(quantity).WithPrice(buyingPrice); if(unit!=null)cartUnits[row]=unit;
            row.Cells["TotalAmount"].Value=unit?.GrossTotal??checked(buyingPrice*quantity);
            UpdateTotalAmount();
            UiTheme.SetStatus(lblStockInfo, presentationLeftToRight ? "Cart totals updated." : "تم تحديث إجماليات السلة.", true);
        }
        private void AddSavePdfButton()
        {
            if (flowFooterButtons.Controls.ContainsKey("btnSavePdf"))
            {
                return;
            }

            AntdUI.Button btnSavePdf = DesktopComponents.Action("btnSavePdf", "حفظ PDF", AppIcon.Save);
            btnSavePdf.Size = new Size(155, 42);
            btnSavePdf.Margin = new Padding(8, 0, 8, 0);

            btnSavePdf.Click += btnSavePdf_Click;
            flowFooterButtons.Controls.Add(btnSavePdf);
            flowFooterButtons.Controls.SetChildIndex(btnSavePdf, Math.Min(3, flowFooterButtons.Controls.Count - 1));
            CommerceWorkspaceTheme.ApplySecondaryAction(btnSavePdf);
        }

        // The unposted attempt is closed explicitly, so a pending file whose result the server does
        // not know cannot block every later purchase.
        private void AddClosePurchaseAttemptButton()
        {
            if (flowFooterButtons.Controls.ContainsKey("btnClosePurchaseAttempt")) return;
            AntdUI.Button close = DesktopComponents.Action("btnClosePurchaseAttempt", "إغلاق المحاولة غير المعتمدة", AppIcon.Close);
            close.Size = new Size(215, 42);
            close.Margin = new Padding(8, 0, 8, 0);
            close.Visible = false;
            close.Click += CloseUnpostedPurchaseAttemptAsync;
            flowFooterButtons.Controls.Add(close);
            CommerceWorkspaceTheme.ApplySecondaryAction(close);
        }

        private void SetPurchaseAttemptCloseVisible(bool visible)
        {
            AddClosePurchaseAttemptButton();
            AntdUI.Button close = flowFooterButtons.Controls["btnClosePurchaseAttempt"] as AntdUI.Button;
            if (close != null) close.Visible = visible;
        }

        private void CloseUnpostedPurchaseAttemptAsync(object sender, EventArgs e)
        {
            if (purchaseRecovery == null) return;
            try
            {
                PurchaseReceiptObservation closed = purchaseRecovery.ConfirmAbandon();
                if (closed == null || !closed.IsAbandoned) return;
                recoveredPurchaseCancelled = false;
                currentPurchaseId = null;
                Clear();
                SetPurchaseAttemptCloseVisible(false);
                UiTheme.SetStatus(lblStockInfo, Present("أُغلقت المحاولة غير المعتمدة دون فاتورة؛ يمكنك بدء فاتورة جديدة."), true);
            }
            catch (UnauthorizedAccessException) { throw; }
            catch (Exception error)
            {
                AppDiagnostics.Log("PurchaseAttemptClose", error);
                UiTheme.SetStatus(lblStockInfo, Present("تعذر إغلاق المحاولة غير المعتمدة؛ تحقق من الاتصال ثم أعد المحاولة."), false);
            }
        }

        private async void btnSavePdf_Click(object sender, EventArgs e)
        {
            if(invoicePrintSelectionPending)return;
            if(RejectPendingBarcodeAction())return;
            if (!PermissionService.EnsureAllowed(AppOperation.PostPurchaseInvoice, this))
            {
                return;
            }

            if (PurchasesGV.Rows.Count == 0)
            {
                AppNotifications.Show(Present("لا توجد بيانات لحفظ PDF."));
                return;
            }
            if (string.IsNullOrWhiteSpace(GetSupplierName()))
            {
                AppNotifications.Show(Present("يرجى اختيار مورد."));
                return;
            }

            try
            {
                if (string.IsNullOrEmpty(currentPurchaseId)&&!purchaseRecoveryUnresolved)
                {
                    if(!await EnsurePurchaseNumberAsync())return;
                }

                if (!isPurchaseSaved || purchaseRecoveryUnresolved)
                {
                    if (!await SavePurchaseToDatabaseAsync())
                    {
                        return;
                    }
                }

                UpdatePurchaseLabels();
                RefreshPurchaseHistoryIfVisible();

                using(var selection=await ChooseInvoiceIdentityAsync())
                {
                    if(selection==null)return;invoicePrintSelection=selection;
                    try{
                using (PrintDocument printDoc = new PrintDocument())
                {
                    DeviceSettings.ApplyPreferredPrinter(printDoc);
                    printDoc.DefaultPageSettings.PaperSize = new PaperSize("A4", 827, 1169);
                    printDoc.DefaultPageSettings.Margins = new Margins(40, 40, 40, 40);
                    printDoc.BeginPrint += BeginPurchaseA4Print; printDoc.EndPrint += EndPurchaseA4Print;
                    printDoc.PrintPage += PrintPurchasePage;
                    await PdfExportHelper.SaveAsPdfAsync(printDoc, $"Purchase-Invoice-{currentPurchaseId}-{DateTime.Now:yyyyMMdd-HHmm}.pdf", this, presentationLeftToRight, ()=>selection.CanUse);
                    printDoc.PrintPage -= PrintPurchasePage;
                }
                    }finally{invoicePrintSelection=null;ReleaseInvoicePrintJobs();}
                }
            }
            catch (Exception ex)
            {
                ShowPurchaseOutputFailure("حفظ PDF", ex);
            }
        }

        private void SetPurchaseStatusFailure(string operation, Exception exception)
        {
            string supportCode = AppDiagnostics.Log("Purchases:" + operation, exception);
            UiTheme.SetStatus(lblStockInfo, PresentFormat("تعذر {0}. رمز الدعم: {1}", Present(operation), supportCode), false);
        }

        private void ShowPurchaseOutputFailure(string outputName, Exception exception)
        {
            if (!isPurchaseSaved)
            {
                UiTheme.ShowOperationFailure(this, outputName, exception, preserveValidationMessage: true);
                return;
            }

            string supportCode = AppDiagnostics.Log("PurchasesOutput", exception);
            AppNotifications.Show(
                string.Format(Present("تم اعتماد فاتورة المشتريات {0}. تعذر {1} الآن — يمكنك إعادة الطباعة من السجل."), currentPurchaseId, Present(outputName), supportCode),
                Present("الفاتورة معتمدة"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private string FormatPurchaseTiming(DateTime value)
        {
            // Match sales presentation without changing the caller's culture or stored date.
            var culture = System.Globalization.CultureInfo.GetCultureInfo(presentationLeftToRight ? "en-US" : "ar-EG");
            return Present("التاريخ") + ": " + value.ToString("dddd, dd MMMM yyyy hh:mm tt", culture);
        }

        private void UpdatePurchaseLabels()
        {
            try
            {
                string supplierName = GetSupplierName();
                if (string.IsNullOrWhiteSpace(supplierName))
                {
                    supplierName = "غير محدد";
                }
                string employeeName = GetCurrentCashierName();

                lblPurchaseSupplier.Text = Present("المورد") + ": " + supplierName;
                lblPurchaseEmployee.Text = Present("الموظف") + ": " + employeeName;
                lblPurchaseTiming.Text = FormatPurchaseTiming(purchaseBusinessDate ?? DateTime.Now);

                UiTheme.ApplyInfoLabels(lblPurchaseSupplier, lblPurchaseEmployee, lblPurchaseTiming);
            }
            catch (Exception ex)
            {
                AppDiagnostics.Log("PurchasesLabels", ex);
                UiTheme.SetFormStatus(this, "تم اعتماد الفاتورة، لكن تعذر تحديث بعض عناصر العرض.", success: false);
            }
        }

        private void RefreshPurchaseHistoryIfVisible()
        {
            if (pnlPurchaseHistory.Visible)
            {
                LoadPurchaseHistory();
            }
        }

        private void ShowPurchaseSaveFailure(string operation,Exception exception)
        {
            if(IsDisposed||Disposing)return;
            Exception leaf=exception;bool sql=false;
            for(Exception current=exception;current!=null;current=current.InnerException){
                if(current is System.Data.SqlClient.SqlException)sql=true;
                leaf=current;
            }
            if(!sql&&leaf is InvalidOperationException){
                AppNotifications.Show(this,Present(leaf.Message),Present(operation),MessageBoxButtons.OK,MessageBoxIcon.Warning);
                return;
            }
            UiTheme.ShowOperationIssue(this,operation,exception,presentationLeftToRight);
        }

        private bool SavePurchaseToDatabase()
        {
            if(recoveredPurchaseCancelled)throw new InvalidOperationException(Present("فاتورة هذه المحاولة ملغاة؛ ابدأ فاتورة جديدة بعد مسح العرض."));
            if(RejectPendingBarcodeAction())return false;
            if (!CommitPurchaseCartEdit()) return false;
            if (!PermissionService.EnsureAllowed(AppOperation.PostPurchaseInvoice, this))
                return false;
            if (isPurchaseSaved) { if(savedUnitPresentation==null)LoadSavedUnitPresentation();return true; }
            if (isSavingPurchase)
                throw new InvalidOperationException("جاري حفظ المشتريات حالياً. الرجاء الانتظار.");

            isSavingPurchase = true; refreshInvoiceState?.Invoke();
            try
            {
                PurchaseInvoiceRequest request = BuildPurchaseInvoiceRequest();
                if(purchaseRecovery==null)purchaseRecovery=new PurchaseWriteRecoveryCoordinator(PurchaseRecoveryDirectory);
                purchaseRecoveryUnresolved=true;
                PurchaseInvoiceResult result = purchaseRecovery.Submit(request);
                purchaseBusinessDate=purchaseRecovery.Pending()?.Snapshot().PurchaseDate;
                currentPurchaseId = result.PurchaseId;
                foreach (PurchaseInvoiceLineResult lineResult in result.Lines)
                {
                    int rowIndex = lineResult.LineNumber - 1;
                    if (rowIndex >= 0 && rowIndex < PurchasesGV.Rows.Count)
                        PurchasesGV.Rows[rowIndex].Cells["DetailId"].Value = lineResult.DetailId;
                }

                isPurchaseSaved = true;
                LoadSavedUnitPresentation();
                string alertPurchaseId = currentPurchaseId;
                InvoiceInventoryAlertPresenter.Refresh(lblStockInfo, PurchasesGV, alertPurchaseId,
                    () => isPurchaseSaved && currentPurchaseId == alertPurchaseId);
                PurchasesGV.ReadOnly = true;
                var settled=purchaseRecovery.CompleteDisplayed(result);purchaseRecoveryUnresolved=false;
                if(settled!=null && settled.IsCancelled){recoveredPurchaseCancelled=true;isPurchaseSaved=false;throw new InvalidOperationException(Present("فاتورة هذه المحاولة ملغاة؛ ابدأ فاتورة جديدة بعد مسح العرض."));}
                return true;
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException("تعذر حفظ المشتريات: " + exception.Message, exception);
            }
            finally
            {
                isSavingPurchase = false;
                if(purchaseRecovery!=null){try{purchaseRecoveryUnresolved=purchaseRecovery.Pending()!=null;}catch{purchaseRecoveryUnresolved=true;}}
            }
        }

        private async System.Threading.Tasks.Task<bool> SettleSavedPurchaseAsync()
        {
            if(!isPurchaseSaved || purchaseRecovery==null)throw new InvalidOperationException(Present("لم تثبت نتيجة المحاولة المعروضة؛ احتُفظ بملف الاستعادة."));
            if(isSavingPurchase)throw new InvalidOperationException(Present("جاري حفظ المشتريات حالياً. الرجاء الانتظار."));
            string draft=CapturePurchaseDraftState();
            var cancellation=new System.Threading.CancellationTokenSource();purchaseWriteCancellation=cancellation;isSavingPurchase=true; refreshInvoiceState?.Invoke();
            try
            {
                var observed=await purchaseRecovery.ObserveAsync(cancellation.Token);
                cancellation.Token.ThrowIfCancellationRequested();
                if(IsDisposed||Disposing||draft!=CapturePurchaseDraftState())throw new InvalidOperationException(Present("تغيرت مسودة المشتريات أثناء الحفظ؛ احتُفظ بالمحاولة للتحقق."));
                if(observed==null||observed.Result.PurchaseId!=currentPurchaseId||observed.Result.Lines.Count!=PurchasesGV.Rows.Count)
                    throw new InvalidOperationException(Present("لم تثبت نتيجة المحاولة المعروضة؛ احتُفظ بملف الاستعادة."));
                decimal displayedTotal=0;
                foreach(var line in observed.Result.Lines)
                {
                    int index=line.LineNumber-1;
                    if(index<0||index>=PurchasesGV.Rows.Count||Convert.ToString(PurchasesGV.Rows[index].Cells["DetailId"].Value)!=line.DetailId)
                        throw new InvalidOperationException(Present("لم تثبت نتيجة المحاولة المعروضة؛ احتُفظ بملف الاستعادة."));
                    displayedTotal+=Convert.ToDecimal(PurchasesGV.Rows[index].Cells["TotalAmount"].Value);
                }
                if(displayedTotal!=observed.Result.TotalAmount)throw new InvalidOperationException(Present("لم تثبت نتيجة المحاولة المعروضة؛ احتُفظ بملف الاستعادة."));
                await LoadSavedUnitPresentationAsync(cancellation.Token);
                observed=await purchaseRecovery.CompleteDisplayedAsync(observed.Result,cancellation.Token) ?? observed;
                cancellation.Token.ThrowIfCancellationRequested();if(IsDisposed||Disposing)throw new OperationCanceledException(cancellation.Token);
                purchaseRecoveryUnresolved=false;recoveredPurchaseCancelled=observed.IsCancelled;isPurchaseSaved=!observed.IsCancelled;
                if(observed.IsCancelled)throw new InvalidOperationException(Present("فاتورة هذه المحاولة ملغاة؛ ابدأ فاتورة جديدة بعد مسح العرض."));
                return true;
            }
            finally
            {
                isSavingPurchase=false;
                if(ReferenceEquals(purchaseWriteCancellation,cancellation))purchaseWriteCancellation=null;
                cancellation.Dispose();
                try{purchaseRecoveryUnresolved=purchaseRecovery.Pending()!=null;}catch{purchaseRecoveryUnresolved=true;}
            }
        }

        private async System.Threading.Tasks.Task<bool> SavePurchaseToDatabaseAsync()
        {
            if(recoveredPurchaseCancelled)throw new InvalidOperationException(Present("فاتورة هذه المحاولة ملغاة؛ ابدأ فاتورة جديدة بعد مسح العرض."));
            if(RejectPendingBarcodeAction())return false;
            if (!CommitPurchaseCartEdit()) return false;
            if (!PermissionService.EnsureAllowed(AppOperation.PostPurchaseInvoice, this))
                return false;
            if (isPurchaseSaved) { if(purchaseRecoveryUnresolved)return await SettleSavedPurchaseAsync();if(savedUnitPresentation==null)await LoadSavedUnitPresentationAsync(System.Threading.CancellationToken.None);return true; }
            if (isSavingPurchase)
                throw new InvalidOperationException("جاري حفظ المشتريات حالياً. الرجاء الانتظار.");

            string draft=CapturePurchaseDraftState();bool readOnlyBefore=PurchasesGV.ReadOnly;
            var cancellation=new System.Threading.CancellationTokenSource();purchaseWriteCancellation=cancellation;
            isSavingPurchase = true; refreshInvoiceState?.Invoke();PurchasesGV.ReadOnly=true;
            try
            {
                PurchaseInvoiceRequest request = BuildPurchaseInvoiceRequest();
                if(purchaseRecovery==null)purchaseRecovery=new PurchaseWriteRecoveryCoordinator(PurchaseRecoveryDirectory);
                purchaseRecoveryUnresolved=true;
                var operationOrigin=new MedicineUnitPresentationContext(string.Empty,string.Empty);
                PurchaseInvoiceResult result = await purchaseRecovery.SubmitAsync(request,cancellation.Token);
                cancellation.Token.ThrowIfCancellationRequested();
                if(IsDisposed||Disposing||draft!=CapturePurchaseDraftState())throw new InvalidOperationException(Present("تغيرت مسودة المشتريات أثناء الحفظ؛ احتُفظ بالمحاولة للتحقق."));
                purchaseBusinessDate=purchaseRecovery.Pending()?.Snapshot().PurchaseDate;
                currentPurchaseId = result.PurchaseId;
                foreach (PurchaseInvoiceLineResult lineResult in result.Lines)
                {
                    int rowIndex = lineResult.LineNumber - 1;
                    if (rowIndex >= 0 && rowIndex < PurchasesGV.Rows.Count)
                        PurchasesGV.Rows[rowIndex].Cells["DetailId"].Value = lineResult.DetailId;
                }

                isPurchaseSaved = true;
                acceptedUnitOrigin=operationOrigin.WithDocument(currentPurchaseId,SavedUnitRowIdentity());
                await LoadSavedUnitPresentationAsync(cancellation.Token);
                string alertPurchaseId = currentPurchaseId;
                InvoiceInventoryAlertPresenter.Refresh(lblStockInfo, PurchasesGV, alertPurchaseId,
                    () => isPurchaseSaved && currentPurchaseId == alertPurchaseId);
                PurchasesGV.ReadOnly = true;
                var settled=await purchaseRecovery.CompleteDisplayedAsync(result,cancellation.Token);
                cancellation.Token.ThrowIfCancellationRequested();if(IsDisposed||Disposing)throw new OperationCanceledException(cancellation.Token);
                purchaseRecoveryUnresolved=false;
                if(settled!=null && settled.IsCancelled){recoveredPurchaseCancelled=true;isPurchaseSaved=false;throw new InvalidOperationException(Present("فاتورة هذه المحاولة ملغاة؛ ابدأ فاتورة جديدة بعد مسح العرض."));}
                return true;
            }
            catch(OperationCanceledException){throw;}
            catch (Exception exception)
            {
                throw new InvalidOperationException("تعذر حفظ المشتريات: " + exception.Message, exception);
            }
            finally
            {
                isSavingPurchase = false;
                if(!IsDisposed&&!Disposing&&!isPurchaseSaved)PurchasesGV.ReadOnly=readOnlyBefore;
                if(ReferenceEquals(purchaseWriteCancellation,cancellation))purchaseWriteCancellation=null;
                cancellation.Dispose();
                if(purchaseRecovery!=null){try{purchaseRecoveryUnresolved=purchaseRecovery.Pending()!=null;}catch{purchaseRecoveryUnresolved=true;}}
            }
        }

        private void ApplyPurchaseReceiptFate(PurchaseReceiptObservation observed)
        {
            recoveredPurchaseCancelled=observed.IsCancelled;isPurchaseSaved=!observed.IsCancelled;
            UiTheme.SetStatus(lblStockInfo,observed.IsCancelled?Present("تم تنفيذ المحاولة ثم إلغاء الفاتورة."):Present("تم التحقق من حفظ الفاتورة واستعادة نتيجتها."),!observed.IsCancelled);
        }

        private void RestorePendingPurchase()
        {
            if(purchaseRecovery==null)purchaseRecovery=new PurchaseWriteRecoveryCoordinator(PurchaseRecoveryDirectory);
            var pending=purchaseRecovery.Pending();if(pending==null)return;
            purchaseRecoveryUnresolved=true;
            var request=pending.Snapshot();purchaseBusinessDate=request.PurchaseDate;purchaseSystemCurrencyCode=request.ExpectedSystemCurrencyCode;
            if(request.CommercialInventoryExpected!=_commercialInventoryEnabled)
                throw new InvalidOperationException(Present("تغير وضع مخزون الدفعات عن محاولة الحفظ المحفوظة؛ احتُفظ بها للتحقق."));
            applyingBarcodeResult=true;
            try {
                currentPurchaseId=request.DesiredPurchaseId;cmbSupplier.Text=request.SupplierName;txtPurchaseNotesInput.Text=request.Notes;
                PurchasesGV.Rows.Clear();savedUnitPresentation=null;acceptedUnitOrigin=null;
                foreach(var line in request.Lines) {
                    int index=PurchasesGV.Rows.Add();var row=PurchasesGV.Rows[index];
                    row.Cells["DetailId"].Value=line.DesiredDetailId;row.Cells["MedName"].Value=line.MedicineName;
                    row.Cells["BuyingPrice"].Value=line.Unit?.UnitPrice??line.BuyingPrice;row.Cells["Quantity"].Value=line.Unit?.EnteredQuantity??line.Quantity;
                    if(line.Unit!=null)cartUnits[row]=line.Unit;row.Cells["Unit"].Value=line.Unit?.Name??"—";
                    row.Cells["ProdDate"].Value=line.ProductionDate.ToString("yyyy-MM-dd");
                    row.Cells["ExpDate"].Value=line.ExpiryDate.ToString("yyyy-MM-dd");
                    row.Cells["TotalAmount"].Value=line.Unit?.GrossTotal??(decimal)line.BuyingPrice*line.Quantity;
                    if(_commercialInventoryEnabled){row.Cells["Gtin"].Value=line.Gtin;row.Cells["BatchNumber"].Value=line.BatchNumber;row.Cells["SerialNumber"].Value=line.SerialNumber;}
                }
                UpdateTotalAmount();PurchasesGV.ReadOnly=true;
            } finally {applyingBarcodeResult=false;}
            var observed=purchaseRecovery.Observe();
            if(observed==null){SetPurchaseAttemptCloseVisible(true);UiTheme.SetStatus(lblStockInfo,Present("لم تثبت نتيجة الحفظ. يمكنك إغلاق المحاولة غير المعتمدة أو إعادة المحاولة نفسها."),false);return;}
            currentPurchaseId=observed.Result.PurchaseId;
            foreach(var line in observed.Result.Lines)PurchasesGV.Rows[line.LineNumber-1].Cells["DetailId"].Value=line.DetailId;
            recoveredPurchaseCancelled=observed.IsCancelled;isPurchaseSaved=!observed.IsCancelled;
            if(isPurchaseSaved)LoadSavedUnitPresentation();
            UpdatePurchaseLabels();
            UiTheme.SetStatus(lblStockInfo,observed.IsCancelled?Present("تم تنفيذ المحاولة ثم إلغاء الفاتورة."):Present("تم التحقق من حفظ الفاتورة واستعادة نتيجتها."),!observed.IsCancelled);
            observed=purchaseRecovery.CompleteDisplayed(observed.Result) ?? observed;purchaseRecoveryUnresolved=false;
            ApplyPurchaseReceiptFate(observed);
        }

        private async System.Threading.Tasks.Task RestorePendingPurchaseAsync(System.Threading.CancellationToken cancellationToken)
        {
            if(purchaseRecovery==null)purchaseRecovery=new PurchaseWriteRecoveryCoordinator(PurchaseRecoveryDirectory);
            var pending=purchaseRecovery.Pending();if(pending==null)return;
            purchaseRecoveryUnresolved=true;
            var request=pending.Snapshot();purchaseBusinessDate=request.PurchaseDate;purchaseSystemCurrencyCode=request.ExpectedSystemCurrencyCode;
            if(request.CommercialInventoryExpected!=_commercialInventoryEnabled)
                throw new InvalidOperationException(Present("تغير وضع مخزون الدفعات عن محاولة الحفظ المحفوظة؛ احتُفظ بها للتحقق."));
            applyingBarcodeResult=true;
            try {
                currentPurchaseId=request.DesiredPurchaseId;cmbSupplier.Text=request.SupplierName;txtPurchaseNotesInput.Text=request.Notes;
                PurchasesGV.Rows.Clear();savedUnitPresentation=null;acceptedUnitOrigin=null;
                foreach(var line in request.Lines) {
                    int index=PurchasesGV.Rows.Add();var row=PurchasesGV.Rows[index];
                    row.Cells["DetailId"].Value=line.DesiredDetailId;row.Cells["MedName"].Value=line.MedicineName;
                    row.Cells["BuyingPrice"].Value=line.Unit?.UnitPrice??line.BuyingPrice;row.Cells["Quantity"].Value=line.Unit?.EnteredQuantity??line.Quantity;
                    if(line.Unit!=null)cartUnits[row]=line.Unit;row.Cells["Unit"].Value=line.Unit?.Name??"—";
                    row.Cells["ProdDate"].Value=line.ProductionDate.ToString("yyyy-MM-dd");
                    row.Cells["ExpDate"].Value=line.ExpiryDate.ToString("yyyy-MM-dd");
                    row.Cells["TotalAmount"].Value=line.Unit?.GrossTotal??(decimal)line.BuyingPrice*line.Quantity;
                    if(_commercialInventoryEnabled){row.Cells["Gtin"].Value=line.Gtin;row.Cells["BatchNumber"].Value=line.BatchNumber;row.Cells["SerialNumber"].Value=line.SerialNumber;}
                }
                UpdateTotalAmount();PurchasesGV.ReadOnly=true;
            } finally {applyingBarcodeResult=false;}
            var operationOrigin=new MedicineUnitPresentationContext(string.Empty,string.Empty);
            var observed=await purchaseRecovery.ObserveAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();if(IsDisposed||Disposing)throw new OperationCanceledException(cancellationToken);
            if(observed==null){SetPurchaseAttemptCloseVisible(true);UiTheme.SetStatus(lblStockInfo,Present("لم تثبت نتيجة الحفظ. يمكنك إغلاق المحاولة غير المعتمدة أو إعادة المحاولة نفسها."),false);return;}
            currentPurchaseId=observed.Result.PurchaseId;
            foreach(var line in observed.Result.Lines)PurchasesGV.Rows[line.LineNumber-1].Cells["DetailId"].Value=line.DetailId;
            recoveredPurchaseCancelled=observed.IsCancelled;isPurchaseSaved=!observed.IsCancelled;
            if(isPurchaseSaved)acceptedUnitOrigin=operationOrigin.WithDocument(currentPurchaseId,SavedUnitRowIdentity());
            if(isPurchaseSaved)await LoadSavedUnitPresentationAsync(cancellationToken);
            UpdatePurchaseLabels();
            UiTheme.SetStatus(lblStockInfo,observed.IsCancelled?Present("تم تنفيذ المحاولة ثم إلغاء الفاتورة."):Present("تم التحقق من حفظ الفاتورة واستعادة نتيجتها."),!observed.IsCancelled);
            observed=await purchaseRecovery.CompleteDisplayedAsync(observed.Result,cancellationToken) ?? observed;
            cancellationToken.ThrowIfCancellationRequested();if(IsDisposed||Disposing)throw new OperationCanceledException(cancellationToken);
            purchaseRecoveryUnresolved=false;
            ApplyPurchaseReceiptFate(observed);
        }

        private PurchaseInvoiceRequest BuildPurchaseInvoiceRequest()
        {
            var lines = new List<PurchaseInvoiceLineRequest>();
            int lineNumber = 0;
            foreach (DataGridViewRow row in PurchasesGV.Rows)
            {
                if (row.IsNewRow) continue;
                lineNumber++;
                lines.Add(new PurchaseInvoiceLineRequest
                {
                    LineNumber = lineNumber,
                    DesiredDetailId = Convert.ToString(row.Cells["DetailId"].Value),
                    MedicineName = Convert.ToString(row.Cells["MedName"].Value),
                    Unit=CartUnit(row),
                    BuyingPrice=CartUnit(row)==null?Convert.ToInt32(row.Cells["BuyingPrice"].Value??0):0,
                    Quantity=CartUnit(row)?.BaseQuantity??Convert.ToInt32(row.Cells["Quantity"].Value??0),
                    ProductionDate = DateTime.Parse(Convert.ToString(row.Cells["ProdDate"].Value)),
                    ExpiryDate = DateTime.Parse(Convert.ToString(row.Cells["ExpDate"].Value)),
                    Gtin = _commercialInventoryEnabled ? Convert.ToString(row.Cells["Gtin"].Value) : string.Empty,
                    BatchNumber = _commercialInventoryEnabled ? Convert.ToString(row.Cells["BatchNumber"].Value) : string.Empty,
                    SerialNumber = _commercialInventoryEnabled ? Convert.ToString(row.Cells["SerialNumber"].Value) : string.Empty
                });
            }

            return new PurchaseInvoiceRequest
            {
                ExpectedSystemCurrencyCode = purchaseSystemCurrencyCode,
                DesiredPurchaseId = currentPurchaseId,
                SupplierName = GetSupplierName(),
                EmployeeName = GetCurrentCashierName(),
                ActorRole = SessionContext.CurrentRole,
                Notes = txtPurchaseNotesInput.Text,
                PurchaseDate = DateTime.Now,
                CommercialInventoryExpected = _commercialInventoryEnabled,
                Lines = lines.AsReadOnly()
            };
        }
        internal InvoicePrintHeader CaptureInvoicePrintHeader()
        {
            return InvoicePrintHeader.Purchase(lblPurchaseSupplier.Text, lblPurchaseEmployee.Text, lblPurchaseTiming.Text);
        }
        private void PrintPurchasePage(object sender, PrintPageEventArgs e)
        {
            if(invoicePrintSelection!=null&&!invoicePrintSelection.Guard(e))return;
            if (purchaseA4PrintJob == null || purchaseA4PrintJob.Completed) purchaseA4PrintJob = CapturePurchaseA4PrintJob();
            purchaseA4PrintJob.PrintPage(sender, e);
            if(invoicePrintSelection!=null)invoicePrintSelection.Guard(e);
        }
        private void PrintSmallPurchaseReceipt(object sender, PrintPageEventArgs e)
        {
            if(invoicePrintSelection!=null&&!invoicePrintSelection.Guard(e))return;
            if (purchaseThermalPrintJob == null) purchaseThermalPrintJob = CapturePurchaseThermalPrintJob();
            purchaseThermalPrintJob.PrintPage(sender, e);
            if(invoicePrintSelection!=null)invoicePrintSelection.Guard(e);
        }

        private void Purchases_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (previewMode || applyingBarcodeResult || changingContentDirection) return;
            if (PurchasesGV.Rows.Count > 0 && !isPurchaseSaved)
            {
                DialogResult result = AppNotifications.Show(
                    purchaseRecoveryUnresolved?Present("نتيجة الحفظ غير محسومة. ستبقى المحاولة محفوظة للتحقق عند إعادة فتح الشاشة. هل تريد الخروج؟"):"هل أنت متأكد أنك تريد الخروج؟ سيتم إلغاء المشتريات الحالية.",
                    "تأكيد الخروج",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    RollbackPurchaseTransaction();
                }
                else
                {
                    e.Cancel = true;
                }
            }
        }

        private void SelectEmployee_SelectedIndexChanged(object sender, EventArgs e)
        {
        }

        private void SupplierSelectionChanged(object sender, AntdUI.IntEventArgs e)
        {
            cmbSupplier_SelectedIndexChanged(sender, EventArgs.Empty);
        }

        private async void cmbSupplier_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (isSyncingSupplierSelection)
            {
                return;
            }

            if (previewMode)
            {
                lblStockInfo.Text = Present("معاينة محلية — لم تُقرأ قاعدة البيانات");
                UiTheme.SetStatus(lblStockInfo, lblStockInfo.Text);
                return;
            }

            string supplierName = cmbSupplier.SelectedValue?.ToString();
            if(!await LoadMedicineNamesAsync(supplierName))return;
            MediComboCb.SelectedIndex = -1;
            MediComboCb.Text = Present("اختر الدواء");
            txtQuantity.Clear();

            if (string.IsNullOrWhiteSpace(supplierName))
            {
                lblStockInfo.Text = Present("المخزون الحالي — اختر مورداً أو أدخل باركود");
            }
            else
            {
            lblStockInfo.Text = PresentFormat("تم عرض أصناف المورد: {0}", supplierName);
            }
            UiTheme.SetStatus(lblStockInfo, lblStockInfo.Text);
        }

        public class ComboBoxItem
        {
            public string Text { get; set; }
            public string Value { get; set; }

            public ComboBoxItem(string text, string value)
            {
                Text = text;
                Value = value;
            }

            public override string ToString()
            {
                return Text;
            }
        }
    }
}

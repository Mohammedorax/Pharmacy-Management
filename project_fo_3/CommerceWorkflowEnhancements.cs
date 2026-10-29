using System;
using System.Drawing;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace project_fo_3
{
    internal static class CommerceInvoiceState
    {
        internal static string Describe(bool english, bool preview, bool saving, bool cancelled, bool unresolved, bool saved, string reference)
        {
            string template = preview ? "معاينة فقط — لم تُحفظ فاتورة" : saving ? "جارٍ الحفظ…" : unresolved ? "نتيجة الحفظ غير محسومة — أعد الفتح للتحقق" : cancelled ? "ملغاة" : saved ? "محفوظة — {0}" : "مسودة — Enter لإضافة الصنف المحدد";
            return UiLanguageCatalog.Format(english, template, reference);
        }

        internal static Action Attach(Form owner, Label label, Func<string> state)
        {
            EventHandler refresh = (s, e) =>
            {
                if (owner.IsDisposed || owner.Disposing || label.IsDisposed) return;
                string text = state();
                if (label.Text != text) label.Text = text;
                label.AccessibleDescription = text;
            };
            var timer = new Timer { Interval = 150 };
            timer.Tick += refresh;
            timer.Start();
            owner.Disposed += (s, e) => { timer.Stop(); timer.Dispose(); };
            refresh(owner, EventArgs.Empty);
            return () => { refresh(owner, EventArgs.Empty); if (!label.IsDisposed) label.Refresh(); };
        }
    }

    public partial class Billing
    {
        private bool unifiedSearchPending;
        private long unifiedInputRevision;
        private async Task ResolveUnifiedSearchAsync(MedicineSearchRequestEventArgs request)
        {
            if (IsDisposed || Disposing || unifiedSearchPending || keyboardAddPending || previewMode || _isInvoiceSaved || _isInvoiceCancelled || _isSavingInvoice || medicineCartPending || medicineNamesLoading || medicineBarcodeLoading) return;
            unifiedSearchPending = true;
            long revision = unifiedInputRevision, session = SessionContext.CurrentSessionRevision;
            string quantity = txtQuantity.Text;
            try
            {
                txtBarcode.Text = request.Query;
                bool found = await TrySelectMedicineByBarcodeAsync();
                if (IsDisposed || Disposing || session != SessionContext.CurrentSessionRevision || revision != unifiedInputRevision || quantity != txtQuantity.Text) return;
                if (!found)
                {
                    // Only a confirmed absence permits name fallback. An error,
                    // ambiguous barcode or expired session must stop the operation.
                    if (!unifiedBarcodeNotFound) return;
                    if (request.Suggestion == null || !MediComboCb.Items.Contains(request.Suggestion))
                    {
                        Label status = lblStockStatus;
                        UiTheme.SetStatus(status, Present("لا توجد أصناف مطابقة للاسم أو الباركود."), false);
                        return;
                    }
                    MediComboCb.SelectedItem = request.Suggestion;
                }
                await AddSelectedMedicineFromKeyboardAsync();
                if (!IsDisposed && !Disposing) { MediComboCb.Focus(); MediComboCb.SelectAll(); }
            }
            catch (Exception error) { if (!IsDisposed && !Disposing) UiTheme.ShowOperationIssue(this, "البحث وإضافة الصنف", error); }
            finally { unifiedSearchPending = false; }
        }
        private bool keyboardAddPending;
        private Action refreshInvoiceState;
        private void ConfigureWorkflowEnhancements()
        {
            MediComboCb.ConfigureSearch(presentationLeftToRight);
            MediComboCb.SearchRequested += async (s, e) => await ResolveUnifiedSearchAsync(e);
            MediComboCb.TextChanged += (s, e) =>
            {
                if (medicineBarcodeApplyingSelection) return;
                unifiedInputRevision++;
                if (unifiedSearchPending) { CancelMedicineBarcodeRead(); medicineSelectionCancellation?.Cancel(); medicineCartCancellation?.Cancel(); }
            };
            MediComboCb.AccessibleName = Present("اسم الصنف أو الباركود");
            MediComboCb.AccessibleDescription = Present("اكتب الاسم أو امسح الباركود. Enter للإضافة، والأسهم لاختيار اسم.");
            MediComboCb.TabIndex = 0;
            txtBarcode.TabStop = false;
            MediComboCb.CommitRequested += async (s, e) => { if (!unifiedSearchPending) await AddSelectedMedicineFromKeyboardAsync(); };
            refreshInvoiceState = CommerceInvoiceState.Attach(this, lblBillingSubtitle, () =>
            {
                UpdateCustomerCreditAvailabilityPresentation();
                return CommerceInvoiceState.Describe(presentationLeftToRight, previewMode, _isSavingInvoice, _isInvoiceCancelled, false, _isInvoiceSaved, GetSavedSalesReference());
            });
        }
        private async Task AddSelectedMedicineFromKeyboardAsync()
        {
            if (keyboardAddPending || previewMode || _isInvoiceSaved || _isInvoiceCancelled || _isSavingInvoice || medicineCartPending || medicineNamesLoading || medicineBarcodeLoading) return;
            keyboardAddPending = true;
            try
            {
                string selected = Convert.ToString(MediComboCb.SelectedItem);
                if (string.IsNullOrWhiteSpace(selected)) return;
                CancelMedicineBarcodeRead();
                if (!await RefreshSelectedMedicineAsync() || selected != Convert.ToString(MediComboCb.SelectedItem) || IsDisposed || Disposing) return;
                if (string.IsNullOrWhiteSpace(txtQuantity.Text)) ResetQuantityToDefault();
                await AddMedicineToCartAsync();
            }
            catch (Exception error) { if (!IsDisposed && !Disposing) UiTheme.ShowOperationIssue(this, "إضافة الصنف", error); }
            finally { keyboardAddPending = false; }
        }
        private void AddStockPreviewColumn()
        {
            var column = new DataGridViewTextBoxColumn { Name = "AvailableStock", HeaderText = Present("آخر رصيد"), ReadOnly = true,
                ToolTipText = Present("آخر رصيد تمت قراءته؛ يُتحقق من المخزون الحالي عند الحفظ.") };
            BillingGV.Columns.Add(column);
            BillingGV.CellFormatting -= FormatStockPreview;
            BillingGV.CellFormatting += FormatStockPreview;
        }
        private void FormatStockPreview(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || BillingGV.Columns[e.ColumnIndex].Name != "AvailableStock") return;
            e.Value = BillingGV.Rows[e.RowIndex].Tag is int stock ? stock.ToString("N0") : "—";
            e.FormattingApplied = true;
        }
    }

    public partial class Purchases
    {
        private bool unifiedSearchPending;
        private long unifiedInputRevision;
        private async Task ResolveUnifiedSearchAsync(MedicineSearchRequestEventArgs request)
        {
            if (IsDisposed || Disposing || unifiedSearchPending || keyboardAddPending || previewMode || isPurchaseSaved || isSavingPurchase || purchaseRecoveryUnresolved || namesReadPending || fullStartupPending || barcodeReadCancellation != null) return;
            unifiedSearchPending = true;
            long revision = unifiedInputRevision, session = SessionContext.CurrentSessionRevision;
            string quantity = txtQuantity.Text;
            try
            {
                txtBarcode.Text = request.Query;
                bool found = await TrySelectMedicineByBarcodeAsync();
                if (IsDisposed || Disposing || session != SessionContext.CurrentSessionRevision || revision != unifiedInputRevision || quantity != txtQuantity.Text) return;
                if (!found)
                {
                    // Only a confirmed absence permits name fallback. An error,
                    // ambiguous barcode or expired session must stop the operation.
                    if (!unifiedBarcodeNotFound) return;
                    if (request.Suggestion == null || !MediComboCb.Items.Contains(request.Suggestion))
                    {
                        Label status = lblStockInfo;
                        UiTheme.SetStatus(status, Present("لا توجد أصناف مطابقة للاسم أو الباركود."), false);
                        return;
                    }
                    MediComboCb.SelectedItem = request.Suggestion;
                }
                await AddSelectedMedicineFromKeyboardAsync();
                if (!IsDisposed && !Disposing) { MediComboCb.Focus(); MediComboCb.SelectAll(); }
            }
            catch (Exception error) { if (!IsDisposed && !Disposing) UiTheme.ShowOperationIssue(this, "البحث وإضافة الصنف", error); }
            finally { unifiedSearchPending = false; }
        }
        private bool keyboardAddPending;
        private Action refreshInvoiceState;
        private ErrorProvider purchaseDateWarnings;
        private void ConfigureWorkflowEnhancements()
        {
            MediComboCb.ConfigureSearch(presentationLeftToRight);
            MediComboCb.SearchRequested += async (s, e) => await ResolveUnifiedSearchAsync(e);
            MediComboCb.TextChanged += (s, e) =>
            {
                if (applyingBarcodeResult) return;
                unifiedInputRevision++;
                if (unifiedSearchPending) { CancelBarcodeRead(); CancelSelectionRead(); CancelQuantityRead(); }
            };
            MediComboCb.AccessibleName = Present("اسم الصنف أو الباركود");
            MediComboCb.AccessibleDescription = Present("اكتب الاسم أو امسح الباركود. Enter للإضافة، والأسهم لاختيار اسم.");
            // The hidden legacy barcode input must not participate in keyboard order.
            Control[] purchaseInputs = { cmbSupplier, MediComboCb, txtQuantity, btnAddToPurchase,
                dtpProdDate, dtpExpDate, SelectEmployee, txtPurchaseNotesInput, btnConfirmPurchase };
            for (int index = 0; index < purchaseInputs.Length; index++)
            {
                purchaseInputs[index].TabIndex = index;
                purchaseInputs[index].TabStop = true;
            }
            txtBarcode.TabStop = false;
            MediComboCb.CommitRequested += async (s, e) => { if (!unifiedSearchPending) await AddSelectedMedicineFromKeyboardAsync(); };
            txtQuantity.KeyDown += async (s, e) =>
            {
                if (e.KeyCode != Keys.Enter) return;
                e.Handled = e.SuppressKeyPress = true;
                if (!unifiedSearchPending) await AddSelectedMedicineFromKeyboardAsync();
            };
            refreshInvoiceState = CommerceInvoiceState.Attach(this, lblPurchasesSubtitle, () => CommerceInvoiceState.Describe(presentationLeftToRight, previewMode, isSavingPurchase, recoveredPurchaseCancelled, purchaseRecoveryUnresolved, isPurchaseSaved, Convert.ToString(currentPurchaseId, CultureInfo.InvariantCulture)));
            purchaseDateWarnings = new ErrorProvider { ContainerControl = this, BlinkStyle = ErrorBlinkStyle.NeverBlink };
            dtpProdDate.ValueChanged += (s, e) => RefreshPurchaseDateWarnings();
            dtpExpDate.ValueChanged += (s, e) => RefreshPurchaseDateWarnings();
            Disposed += (s, e) => purchaseDateWarnings.Dispose();
            RefreshPurchaseDateWarnings();
        }
        private async Task AddSelectedMedicineFromKeyboardAsync()
        {
            if (keyboardAddPending || previewMode || isPurchaseSaved || isSavingPurchase || purchaseRecoveryUnresolved || namesReadPending || fullStartupPending || barcodeReadCancellation != null) return;
            keyboardAddPending = true;
            try
            {
                // Selection reads are advisory; the existing add path reads the price again and guards the session.
                if (string.IsNullOrWhiteSpace(txtQuantity.Text)) txtQuantity.Text = "1";
                CancelSelectionRead(); CancelQuantityRead();
                await AddMedicineToPurchaseAsync();
            }
            finally { keyboardAddPending = false; }
        }
        private void RefreshPurchaseDateWarnings()
        {
            if (purchaseDateWarnings == null) return;
            var production = dtpProdDate.Value; var expiry = dtpExpDate.Value;
            string productionWarning = !production.HasValue ? (Present("تاريخ الإنتاج مطلوب."))
                : production.Value.Date > DateTime.Today ? (Present("تاريخ الإنتاج لا يمكن أن يكون في المستقبل.")) : string.Empty;
            string expiryWarning = !expiry.HasValue ? (Present("تاريخ الانتهاء مطلوب."))
                : production.HasValue && expiry.Value.Date <= production.Value.Date ? (Present("الانتهاء يجب أن يكون بعد الإنتاج."))
                : expiry.Value.Date <= DateTime.Today ? (Present("تنبيه: الصنف منتهي الصلاحية.")) : string.Empty;
            purchaseDateWarnings.SetError(dtpProdDate, productionWarning);
            purchaseDateWarnings.SetError(dtpExpDate, expiryWarning);
            dtpProdDate.AccessibleDescription = productionWarning;
            dtpExpDate.AccessibleDescription = expiryWarning;
        }
        private void AddPurchasePriceComparisonColumn()
        {
            PurchasesGV.Columns.Add(new DataGridViewTextBoxColumn { Name = "PreviousBuyingPrice", ReadOnly = true,
                HeaderText = Present("السابق / التغير"),
                ToolTipText = Present("سعر هذه الوحدة المسجل عند إضافتها / التغير في سعرها.") });
            PurchasesGV.CellFormatting -= FormatPurchasePriceComparison;
            PurchasesGV.CellFormatting += FormatPurchasePriceComparison;
        }
        private void FormatPurchasePriceComparison(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || PurchasesGV.Columns[e.ColumnIndex].Name != "PreviousBuyingPrice") return;
            var row = PurchasesGV.Rows[e.RowIndex];
            e.FormattingApplied = true;
            e.CellStyle.ForeColor = UiTheme.Text;
            decimal previous, current;
            // The immutable quote records the cost of this package unit, including
            // restored invoices. A legacy row has only the cost captured when added.
            var unit = CartUnit(row);
            object recorded = unit == null ? row.Tag : (object)unit.CatalogPrice;
            if (!decimal.TryParse(Convert.ToString(recorded, CultureInfo.CurrentCulture),
                    NumberStyles.Number, CultureInfo.CurrentCulture, out previous) || previous < 0m ||
                !decimal.TryParse(Convert.ToString(row.Cells["BuyingPrice"].Value, CultureInfo.CurrentCulture),
                    NumberStyles.Number, CultureInfo.CurrentCulture, out current) || current < 0m)
            {
                e.Value = "—";
                return;
            }
            decimal change = current - previous;
            e.Value = previous.ToString("N2", CultureInfo.CurrentCulture) + Environment.NewLine +
                (change > 0m ? "↑ " : change < 0m ? "↓ " : "= ") +
                Math.Abs(change).ToString("N2", CultureInfo.CurrentCulture);
            e.CellStyle.ForeColor = change > 0m ? UiTheme.Warning : change < 0m ? UiTheme.Primary : UiTheme.Text;
        }
    }
}


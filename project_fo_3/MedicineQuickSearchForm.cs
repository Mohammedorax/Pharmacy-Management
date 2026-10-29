using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using PharmacyManagement.Application;
using System.Threading;
using System.Windows.Forms;

namespace project_fo_3
{
    internal sealed partial class MedicineQuickSearchForm : Form
    {
        private readonly System.Windows.Forms.Timer searchDelay = new System.Windows.Forms.Timer { Interval = 250 };
        private CancellationTokenSource pendingSearch;
        private int searchVersion;
        private readonly bool previewMode;
        private readonly MedicineQuickSearchSession searchSession;
        private readonly bool presentationLeftToRight;
        private readonly FrequentMedicineDesktopSession frequentSession;
        private bool updatingSuggestionMode;
        private bool PersonalSuggestions => suggestionMode.SelectedIndex == 1 && string.IsNullOrWhiteSpace(searchBox.Text);
        private bool CurrentContext => searchSession.IsCurrent && (frequentSession == null || frequentSession.IsCurrent);

        private sealed class SuggestionRow
        {
            internal readonly MedicineSearchItem Item;
            public string MedName => Item.MedName;
            public string Barcode => Item.Barcode;
            public string CompName => Item.CompName;
            public decimal? MedQty => Item.MedQty;
            public decimal? Sprice => Item.Sprice;
            public DateTime? ExpDate => Item.ExpDate;
            public decimal? NetSold => Item.NetSold;
            public int? ChoiceCount { get; }
            internal SuggestionRow(MedicineSearchItem item, int? count) { Item = item; ChoiceCount = count; }
        }
        internal MedicineLookupResult SelectedMedicine { get; private set; }

        internal MedicineQuickSearchForm(string initialSearch = "") : this(initialSearch, false, UiLanguageCatalog.IsEnglishActive) { }

        internal MedicineQuickSearchForm(string initialSearch, bool previewMode) : this(initialSearch, previewMode, UiLanguageCatalog.IsEnglishActive) { }

        internal MedicineQuickSearchForm(string initialSearch, bool previewMode, bool presentationLeftToRight, MedicineQuickSearchSession searchSession = null)
            : this(initialSearch, previewMode, presentationLeftToRight, searchSession, null) { }

        internal MedicineQuickSearchForm(string initialSearch, bool previewMode, bool presentationLeftToRight, MedicineQuickSearchSession searchSession, FrequentMedicineDesktopSession frequentSession)
        {
            this.previewMode = previewMode;
            this.searchSession = previewMode ? null : searchSession ?? ApplicationServices.CreateMedicineQuickSearchSession();
            this.presentationLeftToRight = presentationLeftToRight;
            this.frequentSession = previewMode ? null : frequentSession ?? (searchSession == null
                ? ApplicationServices.CreateFrequentMedicineSession() : null);
            InitializeComponent();
            resultsGrid.Tag = UiTheme.PreserveLocalizedCustomGridColumns;
            UiTheme.Apply(this);
            UiTheme.ApplyDataGridQuality(resultsGrid, "اكتب عبارة بحث لعرض الأصناف");
            // The shared grid decorator enables generated columns; this search owns its six
            // ordered columns and must not duplicate them when asynchronous results bind.
            resultsGrid.AutoGenerateColumns = false;
            resultsGrid.EnableHeadersVisualStyles = false;
            resultsGrid.ColumnHeadersDefaultCellStyle.BackColor = UiTheme.Surface;
            resultsGrid.ColumnHeadersDefaultCellStyle.ForeColor = UiTheme.Text;
            resultsGrid.ColumnHeadersDefaultCellStyle.SelectionBackColor = UiTheme.Surface;
            resultsGrid.ColumnHeadersDefaultCellStyle.SelectionForeColor = UiTheme.Text;
            var layout = Controls["quickSearchLayout"];
            layout.BackColor = UiTheme.Background;
            foreach (Control label in layout.Controls)
                if (label is Label) { label.BackColor = UiTheme.Background; label.ForeColor = UiTheme.Text; }
            var footer = layout.Controls["quickSearchFooter"];
            footer.BackColor = UiTheme.Background;
            var actions = (FlowLayoutPanel)footer.Controls["quickSearchActions"];
            actions.BackColor = UiTheme.Background;
            actions.RightToLeft = RightToLeft.No;
            actions.FlowDirection = presentationLeftToRight ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;
            DesktopComponents.StyleOutlinedFooterAction(selectButton);
            DesktopComponents.StyleOutlinedFooterAction(closeButton);
            DesktopComponents.StyleNativeTextFrame(searchFrame, searchBox);
            ApplyPresentation();
            suggestionMode.SelectedIndexChanged += (sender, args) =>
            {
                if (!updatingSuggestionMode) QueueSearch();
            };
            KeyDown += (sender, args) =>
            {
                if (args.KeyCode == Keys.Escape) { args.SuppressKeyPress = true; Close(); }
            };
            searchBox.Text = initialSearch ?? string.Empty;
            ApplySearchTextDirection();
            searchBox.TextChanged += (sender, args) => { ApplySearchTextDirection(); QueueSearch(); };
            searchDelay.Tick += (sender, args) => RunSearch();
            searchBox.KeyDown += (sender, args) =>
            {
                if (args.KeyCode == Keys.Enter)
                {
                    args.SuppressKeyPress = true;
                    if (selectButton.Enabled && resultsGrid.CurrentRow != null) SelectResult();
                    else RunSearch();
                }
                else if (args.KeyCode == Keys.Down && selectButton.Enabled) { args.SuppressKeyPress = true; resultsGrid.Focus(); }
            };
            resultsGrid.KeyDown += (sender, args) =>
            {
                if (args.KeyCode == Keys.Enter) { args.SuppressKeyPress = true; SelectResult(); }
            };
            resultsGrid.CellDoubleClick += (sender, args) => { if (args.RowIndex >= 0) SelectResult(); };
            selectButton.Click += (sender, args) => SelectResult();
            Shown += (sender, args) =>
            {
                searchBox.Focus();
                if (!previewMode) { searchBox.SelectAll(); QueueSearch(); }
                else { searchBox.SelectionStart = searchBox.TextLength; searchBox.SelectionLength = 0; }
            };
            Resize += (sender, args) => ApplyResponsiveColumns();
            Shown += (sender, args) =>
            {
                ApplyPresentation();
                ApplyResponsiveColumns();
            };
            if (previewMode) PopulatePreviewResults();
            else UiSessionSubscription.Attach(this, () =>
            {
                if (!CurrentContext) RejectInvalidSearchContext();
            });
        }

        private string Present(string arabicText)
        {
            return presentationLeftToRight ? UiLanguageCatalog.Translate(arabicText) : arabicText;
        }

        private void ApplyPresentation()
        {
            Text = Present("البحث السريع عن الأصناف");
            Label title = Controls.Find("quickSearchTitle", true).FirstOrDefault() as Label;
            Label resultsTitle = Controls.Find("quickSearchResultsTitle", true).FirstOrDefault() as Label;
            if (title != null) title.Text = Present("البحث السريع عن الأصناف");
            if (resultsTitle != null) resultsTitle.Text = Present("نتائج الأصناف");
            updatingSuggestionMode = true;
            int previousMode = Math.Max(0, suggestionMode.SelectedIndex);
            suggestionMode.Items.Clear();
            suggestionMode.Items.Add(Present("الأكثر بيعًا"));
            suggestionMode.Items.Add(Present("اختياراتي المتكررة"));
            suggestionMode.SelectedIndex = previousMode;
            updatingSuggestionMode = false;
            suggestionMode.AccessibleName = Present("نوع الاقتراحات");
            suggestionMode.Enabled = !previewMode && frequentSession != null && CurrentContext && string.IsNullOrWhiteSpace(searchBox.Text);
            DesktopComponents.StyleNativeSelectFrame(suggestionFrame, suggestionMode, presentationLeftToRight);
            searchBox.AccessibleName = Present("الاسم أو الباركود أو الشركة");
            resultsGrid.AccessibleName = Present("نتائج البحث السريع عن الأصناف");
            resultsGrid.AccessibleDescription = Present("استخدم سهم لأسفل ثم Enter لاختيار الصنف المحدد.");
            statusLabel.AccessibleName = Present("حالة البحث السريع");
            selectButton.Text = Present("اختيار الصنف");
            selectButton.AccessibleName = Present("اختيار الصنف المحدد");
            selectButton.AccessibleDescription = Present("يفتح الصنف المحدد في البيع أو الشراء.");
            closeButton.Text = Present("إغلاق");
            closeButton.AccessibleName = Present("إغلاق البحث السريع");
            selectButton.IconPosition = closeButton.IconPosition = presentationLeftToRight
                ? AntdUI.TAlignMini.Left : AntdUI.TAlignMini.Right;
            statusLabel.Text = previewMode
                ? Present("معاينة البحث دون اتصال بقاعدة البيانات.")
                : Present("ابحث بالاسم أو الباركود أو الشركة. Enter للاختيار، Esc للإغلاق.");

            UiDirection.Apply(this, presentationLeftToRight);
            if (title != null) title.TextAlign = presentationLeftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
            if (resultsTitle != null) resultsTitle.TextAlign = presentationLeftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
            statusLabel.TextAlign = presentationLeftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
            ApplySearchTextDirection();
            FlowLayoutPanel actions = Controls.Find("quickSearchActions", true).FirstOrDefault() as FlowLayoutPanel;
            if (actions != null)
            {
                actions.RightToLeft = RightToLeft.No;
                actions.FlowDirection = presentationLeftToRight ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;
            }
            UiTheme.ApplyStatusStripPresentation(this, presentationLeftToRight);
            ApplyGridPresentation();
        }

        private void ApplySearchTextDirection()
        {
            UiDirection.ApplyContentDirection(searchBox, presentationLeftToRight);
        }

        private void ApplyGridPresentation()
        {
            resultsGrid.RightToLeft = presentationLeftToRight ? RightToLeft.No : RightToLeft.Yes;
            resultsGrid.ColumnHeadersDefaultCellStyle.Alignment = presentationLeftToRight
                ? DataGridViewContentAlignment.MiddleLeft : DataGridViewContentAlignment.MiddleRight;
            resultsGrid.DefaultCellStyle.Alignment = presentationLeftToRight
                ? DataGridViewContentAlignment.MiddleLeft : DataGridViewContentAlignment.MiddleRight;
            resultsGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            resultsGrid.ColumnHeadersHeight = 58;
            string[] logical = { "MedName", "Barcode", "CompName", "MedQty", "Sprice", "ExpDate", "NetSold", "ChoiceCount" };
            for (int displayIndex = 0; displayIndex < logical.Length; displayIndex++)
            {
                string columnName = logical[displayIndex];
                resultsGrid.Columns[columnName].DisplayIndex = displayIndex;
            }
            SetHeader("MedName", "الصنف");
            SetHeader("Barcode", "الباركود");
            SetHeader("CompName", "الشركة/المورد");
            SetHeader("MedQty", "الرصيد المسجل");
            SetHeader("Sprice", "سعر البيع");
            SetHeader("ExpDate", "انتهاء الصنف");
            SetHeader("NetSold", "صافي المباع (30 يومًا)");
            resultsGrid.Columns["ChoiceCount"].HeaderText = Present("مرات الإضافة");
        }

        private void SetHeader(string columnName, string arabicText)
        {
            if (resultsGrid.Columns.Contains(columnName)) resultsGrid.Columns[columnName].HeaderText = Present(arabicText);
        }

        private void ApplyResponsiveColumns()
        {
            if (resultsGrid == null || resultsGrid.ClientSize.Width <= 0) return;
            int width = resultsGrid.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 2;
            bool compact = width < 500;
            bool medium = width >= 500 && width < 760;
            resultsGrid.Columns["MedName"].Visible = true;
            resultsGrid.Columns["Barcode"].Visible = !compact;
            resultsGrid.Columns["CompName"].Visible = !compact;
            resultsGrid.Columns["MedQty"].Visible = true;
            resultsGrid.Columns["Sprice"].Visible = true;
            resultsGrid.Columns["ExpDate"].Visible = true;
            resultsGrid.Columns["NetSold"].Visible = !previewMode && !compact && string.IsNullOrWhiteSpace(searchBox.Text) && !PersonalSuggestions;
            resultsGrid.Columns["ChoiceCount"].Visible = !previewMode && !compact && PersonalSuggestions;

            DataGridViewColumn[] visible = resultsGrid.Columns.Cast<DataGridViewColumn>().Where(column => column.Visible).ToArray();
            resultsGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            int[] weights = compact ? new[] { 38, 17, 19, 26 }
                : visible.Length == 7 ? new[] { 22, 16, 16, 9, 10, 14, 13 }
                : medium ? new[] { 25, 19, 19, 11, 12, 14 }
                : new[] { 25, 18, 18, 10, 12, 17 };
            int used = 0;
            for (int index = 0; index < visible.Length; index++)
            {
                visible[index].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                visible[index].MinimumWidth = 48;
                int columnWidth = index == visible.Length - 1
                    ? Math.Max(48, width - used)
                    : Math.Max(48, width * weights[Math.Min(index, weights.Length - 1)] / 100);
                visible[index].Width = columnWidth;
                used += columnWidth;
            }
            DataGridViewColumn expiry = resultsGrid.Columns["ExpDate"];
            int dateWidth = TextRenderer.MeasureText("2099-12-31", resultsGrid.DefaultCellStyle.Font ?? resultsGrid.Font).Width
                + resultsGrid.DefaultCellStyle.Padding.Horizontal + 12;
            if (expiry.Width < dateWidth)
            {
                int extra = dateWidth - expiry.Width;
                DataGridViewColumn medicine = resultsGrid.Columns["MedName"];
                medicine.Width = Math.Max(medicine.MinimumWidth, medicine.Width - extra);
                expiry.Width = dateWidth;
            }
            resultsGrid.ScrollBars = visible.Sum(column => column.Width) > width ? ScrollBars.Both : ScrollBars.Vertical;
            ApplyGridPresentation();
        }

        private void PopulatePreviewResults()
        {
            resultsGrid.DataSource = new BindingList<MedicineSearchItem>(new List<MedicineSearchItem>
            {
                new MedicineSearchItem
                {
                    MedName = "باراسيتامول 500 mg", Barcode = "6290000000001", CompName = "Pharma — شركة محلية",
                    MedQty = 24m, Sprice = 1000m, ExpDate = new DateTime(2028, 1, 1), NetSold = 18m
                }
            });
            selectButton.Enabled = false;
            ApplyResponsiveColumns();
        }

        private void QueueSearch()
        {
            if (previewMode) return;
            suggestionMode.Enabled = frequentSession != null && CurrentContext && string.IsNullOrWhiteSpace(searchBox.Text);
            searchVersion++;
            pendingSearch?.Cancel();
            searchDelay.Stop();
            ClearResults();
            statusLabel.Text = PersonalSuggestions ? Present("جارٍ تحميل اختياراتك…")
                : string.IsNullOrWhiteSpace(searchBox.Text) ? Present("جارٍ تحميل الأكثر مبيعًا…") : Present("جارٍ البحث…");
            searchDelay.Start();
        }

        private async void RunSearch() { await RunSearchAsync(); }

        internal async System.Threading.Tasks.Task RunSearchAsync()
        {
            if (previewMode || IsDisposed || Disposing) return;
            if (!CurrentContext) { RejectInvalidSearchContext(); return; }
            searchDelay.Stop();
            pendingSearch?.Cancel();
            int version = ++searchVersion;
            ClearResults();
            string query = searchBox.Text.Trim();
            bool showPersonal = PersonalSuggestions; bool showPopular = query.Length == 0 && !showPersonal;
            var cancellation = new CancellationTokenSource();
            pendingSearch = cancellation;
            statusLabel.Text = Present("جارٍ البحث…");
            try
            {
                IReadOnlyList<MedicineSearchItem> items;
                IReadOnlyDictionary<string, int> counts = null;
                if (showPersonal)
                {
                    if (frequentSession == null) throw new InvalidOperationException("Frequent suggestions unavailable.");
                    var snapshot = await frequentSession.ReadAsync(cancellation.Token);
                    items = snapshot.Items; counts = snapshot.Counts;
                }
                else items = await searchSession.Service.SearchAsync(query, searchSession.Actor, cancellation.Token);
                if (IsDisposed || Disposing || cancellation.IsCancellationRequested || version != searchVersion) return;
                if (!CurrentContext) { RejectInvalidSearchContext(); return; }
                resultsGrid.Columns["NetSold"].Visible = showPopular;
                resultsGrid.DataSource = new BindingList<SuggestionRow>(items.Select(item => new SuggestionRow(item,
                    counts == null ? (int?)null : counts[item.MedName])).ToList());
                ApplyResponsiveColumns();
                selectButton.Enabled = items.Count > 0;
                if (showPersonal)
                {
                    statusLabel.Text = items.Count == 0
                        ? Present("لا توجد اختيارات متكررة متاحة. اكتب للبحث عن أي صنف.")
                        : Present("اختياراتك المتكررة؛ الرصيد والسعر الحاليان. تحقق الصلاحية عند الإضافة.");
                }
                else if (showPopular)
                {
                    statusLabel.Text = items.Count == 0
                        ? Present("لا توجد اقتراحات من حركة آخر 30 يومًا. اكتب للبحث عن أي صنف.")
                        : Present("الأكثر مبيعًا: صافي حركة آخر 30 يومًا بعد الإلغاءات. تحقق الصلاحية عند الإضافة.");
                }
                else
                statusLabel.Text = items.Count == 0 ? Present("لا توجد أصناف مطابقة. عدّل عبارة البحث.")
                    : items.Count == 100 ? Present("أول 100 نتيجة؛ ضيّق البحث. الرصيد وتاريخ الصنف لا يثبتان صلاحية الدفعات.")
                    : Present("النتائج: ") + items.Count + Present(". الرصيد وتاريخ الصنف لا يثبتان صلاحية الدفعات.");
            }
            catch (OperationCanceledException) { }
            catch (Exception exception)
            {
                if (!IsDisposed && version == searchVersion)
                {
                    if (!CurrentContext) { RejectInvalidSearchContext(); return; }
                    string code = AppDiagnostics.Log("MedicineQuickSearch", exception);
                    UiTheme.SetStatus(statusLabel, (presentationLeftToRight ? "Search failed. Retry with Enter. Support code: " : "تعذر البحث. أعد المحاولة بـ Enter. رمز الدعم: ") + code, false);
                }
            }
            finally
            {
                if (ReferenceEquals(pendingSearch, cancellation)) pendingSearch = null;
                cancellation.Dispose();
            }
        }

        private void RejectInvalidSearchContext()
        {
            searchDelay.Stop(); pendingSearch?.Cancel(); ++searchVersion;
            SelectedMedicine = null; ClearResults(); searchBox.Enabled = false; suggestionMode.Enabled = false;
            UiTheme.SetStatus(statusLabel, presentationLeftToRight ? "Your session or search access changed. Reopen this window." : "تغيرت الجلسة أو صلاحية البحث. أعد فتح النافذة.", false);
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            if (!previewMode && !IsDisposed && !Disposing && !CurrentContext)
                RejectInvalidSearchContext();
        }

        private void ClearResults()
        {
            selectButton.Enabled = false;
            resultsGrid.DataSource = null;
        }

        private void SelectResult()
        {
            if (previewMode) return;
            if (!CurrentContext) { RejectInvalidSearchContext(); return; }
            if (!selectButton.Enabled || resultsGrid.CurrentRow == null) return;
            var adapter = resultsGrid.CurrentRow.DataBoundItem as SuggestionRow;
            var row = adapter == null ? resultsGrid.CurrentRow.DataBoundItem as MedicineSearchItem : adapter.Item;
            if (row == null) return;
            SelectedMedicine = new MedicineLookupResult(row.MedName, row.CompName);
            DialogResult = DialogResult.OK;
            Close();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !IsDisposed)
            {
                searchVersion++;
                searchDelay.Stop();
                searchDelay.Dispose();
                pendingSearch?.Cancel();
                ClearResults();
            }
            base.Dispose(disposing);
        }
    }
}




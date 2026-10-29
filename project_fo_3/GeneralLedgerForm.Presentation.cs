using System;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace project_fo_3
{
    public partial class GeneralLedgerForm
    {
        // AntdUI 2.4.11 formats Value using CurrentCulture but parses text invariantly.
        // Keep this calendar correction local to the ledger, including Enter and handle recreation.
        private sealed class LedgerDatePicker : AntdUI.DatePicker
        {
            public LedgerDatePicker() { }

            public override string Text
            {
                get { return base.Text; }
                set
                {
                    // Value/Format setters and handle creation all write through this property.
                    // Standard "u" formatting uses the Gregorian calendar even outside
                    // ar-SA's supported calendar range. It does not convert DateTime to UTC.
                    if (Value.HasValue && value == Value.Value.ToString(Format, CultureInfo.InvariantCulture))
                        value = ReportCsvWriter.DateText(Value.Value.Date);
                    base.Text = value;
                }
            }

            protected override void OnSetText(string text, bool isempty)
            {
                DateTime date;
                if (EnabledValueTextChange && !isempty && TryReadLedgerDate(this, out date)) Value = date;
            }
            protected override void OnLostFocus(EventArgs e)
            {
                NormalizeValidInput();
                base.OnLostFocus(e);
            }

            private void NormalizeValidInput()
            {
                DateTime date;
                if (TryReadLedgerDate(this, out date))
                {
                    Value = date;
                    Text = ReportCsvWriter.DateText(date);
                }
            }

            protected override bool ProcessCmdKey(ref Message message, Keys keyData)
            {
                if (keyData != Keys.Return) return base.ProcessCmdKey(ref message, keyData);
                NormalizeValidInput();
                // Close the old popup so reopening reflects the date just entered.
                ExpandDrop = false;
                return true;
            }
        }

        internal static bool CloseLedgerDatePickerPopup(AntdUI.DatePicker picker)
        {
            if (picker == null || !picker.ExpandDrop) return false;
            picker.ExpandDrop = false;
            return true;
        }

        private static bool TryReadLedgerDate(AntdUI.DatePicker picker, out DateTime date)
        {
            date = default(DateTime);
            DateTime parsed;
            if (!DateTime.TryParseExact((picker.Text ?? string.Empty).Trim(),
                new[] { "yyyy-MM-dd", "yyyy-M-d", "yyyy/MM/dd", "yyyy/M/d" },
                CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed)) return false;
            if ((picker.MinDate.HasValue && parsed.Date < picker.MinDate.Value.Date) ||
                (picker.MaxDate.HasValue && parsed.Date > picker.MaxDate.Value.Date)) return false;
            date = parsed.Date;
            return true;
        }

        internal string LedgerPeriodText(DateTime start, DateTime end)
        {
            string startText = ReportCsvWriter.DateText(start.Date), endText = ReportCsvWriter.DateText(end.Date);
            // Isolate each ISO date from the surrounding Arabic subtitle's bidi direction.
            if (!ledgerLeftToRight) { startText = "\u200E" + startText + "\u200E"; endText = "\u200E" + endText + "\u200E"; }
            return string.Format(LedgerText("الفترة: {0} - {1}"), startText, endText);
        }
        private bool ledgerPresentationReady, layingOutLedgerActions;
        private bool ledgerLeftToRight = UiDirection.IsLeftToRight;
        private decimal summaryPrevious, summaryDebit, summaryCredit, summaryEnding;
        private string ledgerStatusKey = "جار تحميل خيارات كشف الحساب...";
        private object[] ledgerStatusArguments = new object[0];
        private bool? ledgerStatusSuccess;

        private string LedgerText(string arabic)
        {
            return ledgerLeftToRight ? UiLanguageCatalog.Translate(arabic) : arabic;
        }

        private void SetLedgerStatus(string arabic, bool? success, params object[] arguments)
        {
            statusLabel.AccessibleDescription = string.Empty;
            ledgerStatusKey = arabic;
            ledgerStatusArguments = arguments == null ? new object[0] : (object[])arguments.Clone();
            ledgerStatusSuccess = success;
            RestoreLedgerStatus();
        }

        private void RestoreLedgerStatus()
        {
            UiTheme.SetStatus(statusLabel, string.Format(LedgerText(ledgerStatusKey), ledgerStatusArguments), ledgerStatusSuccess);
            statusLabel.AccessibleName = statusLabel.Text;
        }

        internal void RestoreLedgerPresentation()
        {
            if (ledgerPresentationReady) ApplyPresentation(ledgerLeftToRight);
        }

        internal void ApplyPresentation(bool english)
        {
            ledgerLeftToRight = english;
            if (!ledgerPresentationReady || IsDisposed || Disposing) return;
            bool previousBinding = bindingLedgerFilters;
            bindingLedgerFilters = true;
            SuspendLayout();
            try
            {
                UiDirection.Apply(this, english);
                Text = LedgerText("حركة حساب خلال فترة");
                RestoreLedgerCaptions(this);
                RestoreLedgerSelect(sortBy, new[] { "حسب التاريخ", "حسب التاريخ ثم رقم المستند" });
                RestoreLedgerSelect(documentType, new[] { "الكل", "قيد يدوي", "سند", "فاتورة بيع", "فاتورة شراء", "قيد افتتاحي" });
                includeOpening.Text = LedgerText("الرصيد الافتتاحي");
                includePrevious.Text = LedgerText("الرصيد السابق");
                RestoreCostCenterCaptions();
                foreach (var entry in new[] {
                    Tuple.Create((Control)account, "رقم الحساب"), Tuple.Create((Control)costCenter, "مركز التكلفة"),
                    Tuple.Create((Control)fromDate, "من تاريخ"), Tuple.Create((Control)toDate, "إلى تاريخ"),
                    Tuple.Create((Control)sortBy, "ترتيب حسب"), Tuple.Create((Control)documentType, "نوع المستند") })
                {
                    entry.Item1.AccessibleName = LedgerText(entry.Item2);
                    var selector = entry.Item1 as ComboBox;
                    if (selector == null) continue;
                    var frame = Controls.Find(selector.Name + "Frame", true).OfType<AntdUI.Panel>().FirstOrDefault();
                    if (frame == null) continue;
                    DesktopComponents.StyleNativeSelectFrame(frame, selector, english);
                    UiDirection.Apply(selector, english);
                    frame.AccessibleName = entry.Item1.AccessibleName;
                    var arrow = frame.Controls.OfType<AntdUI.Button>().FirstOrDefault();
                    if (arrow != null) arrow.AccessibleName = (english ? "Open list: " : "فتح قائمة: ") + entry.Item1.AccessibleName;
                }
                summaryCards.BackColor = UiTheme.SurfaceVariant;
                summaryCards.RightToLeft = RightToLeft.No;
                RenderSummary(summaryPrevious, summaryDebit, summaryCredit, summaryEnding);
                ApplyLedgerGridPresentation();
                RestoreLedgerStatus();
                LayoutLedgerActions();
            }
            finally { bindingLedgerFilters = previousBinding; ResumeLayout(true); }
        }

        private void RestoreLedgerCaptions(Control parent)
        {
            foreach (Control control in parent.Controls)
            {
                var original = control.Tag as string;
                if (control is Label && original != null && original != "UiSurface")
                    control.Text = LedgerText(original);
                var action = control as AntdUI.Button;
                if (action != null && original != null)
                {
                    action.Text = LedgerText(original);
                    action.AccessibleName = action.Text;
                    action.AccessibleDescription = LedgerText("إجراء دفتر الأستاذ العام");
                    action.IconPosition = ledgerLeftToRight ? AntdUI.TAlignMini.Left : AntdUI.TAlignMini.Right;
                    DesktopComponents.StyleOutlinedFooterAction(action);
                }
                RestoreLedgerCaptions(control);
            }
        }

        private void RestoreLedgerSelect(AntdUI.Select selector, string[] arabic)
        {
            string[] display = arabic.Select(LedgerText).ToArray();
            if (selector.Items.Cast<object>().Select(Convert.ToString).SequenceEqual(display))
            {
                if (selector.SelectedIndex >= 0 && selector.SelectedIndex < display.Length)
                    selector.Text = display[selector.SelectedIndex];
                return;
            }
            int selected = selector.SelectedIndex;
            selector.Items.Clear();
            foreach (string text in display) selector.Items.Add(text);
            selector.SelectedIndex = selected;
            if (selected >= 0 && selected < display.Length) selector.Text = display[selected];
        }

        private void RestoreCostCenterCaptions()
        {
            string[] captions = { "الكل", "بدون مركز تكلفة" };
            int selected = costCenter.SelectedIndex;
            for (int i = 0; i < captions.Length && i < costCenter.Items.Count; i++)
            {
                string raw = Convert.ToString(costCenter.Items[i]);
                if (raw == captions[i] || raw == UiLanguageCatalog.Translate(captions[i]))
                    costCenter.Items[i] = LedgerText(captions[i]);
            }
            costCenter.SelectedIndex = selected;
        }

        private void ApplyLedgerGridPresentation()
        {
            grid.Tag = UiTheme.PreserveLocalizedCustomGridColumns;
            FormatGrid();
            foreach (Label empty in grid.Controls.OfType<Label>().Where(label => label.Name == "__GridEmptyState"))
            { empty.Text = LedgerText("لا توجد حركات لهذا الحساب"); empty.AccessibleName = empty.Text; }
            foreach (DataGridViewColumn column in grid.Columns)
                column.HeaderText = LedgerText(column.Name);
            if (grid.Columns.Contains("التاريخ"))
                grid.Columns["التاريخ"].DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            if (grid.Columns.Contains("نوع المستند"))
            {
                grid.Columns["نوع المستند"].DefaultCellStyle.WrapMode = DataGridViewTriState.True;
                grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.DisplayedCellsExceptHeaders;
            }
            if (grid.Columns.Contains("البيان"))
                grid.Columns["البيان"].DefaultCellStyle.Alignment = ledgerLeftToRight
                    ? DataGridViewContentAlignment.MiddleLeft : DataGridViewContentAlignment.MiddleRight;
        }

        private string LedgerSourceDisplay(string code)
        {
            string[] captions = { "الكل", "قيد يدوي", "سند", "فاتورة بيع", "فاتورة شراء", "قيد افتتاحي" };
            int source = Array.IndexOf(LedgerSourceTypeCodes, code);
            if (source > 0) return LedgerText(captions[source]);
            switch (code)
            {
                case "SalesInvoiceCancellation": return LedgerText("إلغاء فاتورة بيع");
                case "PurchaseInvoiceCancellation": return LedgerText("إلغاء فاتورة شراء");
                case "CommercialSalesReturn": return LedgerText("إشعار مرتجع تجاري");
                default: return code;
            }
        }

        private void FormatLedgerSourceCell(object sender, DataGridViewCellFormattingEventArgs args)
        {
            if (args.ColumnIndex >= 0 && grid.Columns[args.ColumnIndex].Name == "التاريخ" && args.Value is DateTime)
            {
                args.Value = ReportCsvWriter.DateText((DateTime)args.Value);
                args.FormattingApplied = true;
                return;
            }
            if (args.ColumnIndex < 0 || grid.Columns[args.ColumnIndex].Name != "نوع المستند") return;
            string raw = Convert.ToString(args.Value);
            string display = LedgerSourceDisplay(raw);
            if (string.Equals(raw, display, StringComparison.Ordinal)) return;
            args.Value = display;
            args.FormattingApplied = true;
        }

        private void LayoutLedgerActions()
        {
            if (layingOutLedgerActions || IsDisposed || Disposing) return;
            var actions = Controls.Find("generalLedgerActions", true).OfType<FlowLayoutPanel>().FirstOrDefault();
            if (actions == null || actions.Parent == null) return;
            layingOutLedgerActions = true;
            try
            {
                int width = actions.Padding.Horizontal;
                foreach (var action in actions.Controls.OfType<AntdUI.Button>())
                {
                    action.Width = Math.Max(104, TextRenderer.MeasureText(action.Text, action.Font).Width + 48);
                    width += action.Width + action.Margin.Horizontal;
                }
                int statusWidth = Math.Max(160, Math.Min(480, TextRenderer.MeasureText(statusLabel.Text, statusLabel.Font).Width + statusLabel.Padding.Horizontal));
                bool stacked = actions.Parent.ClientSize.Width - actions.Parent.Padding.Horizontal < width + statusWidth;
                actions.Parent.Height = stacked ? 104 : 68;
                actions.RightToLeft = RightToLeft.No;
                actions.FlowDirection = ledgerLeftToRight ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;
                actions.Dock = stacked ? DockStyle.Bottom : ledgerLeftToRight ? DockStyle.Left : DockStyle.Right;
                actions.Height = 60;
                if (!stacked) actions.Width = width;
            }
            finally { layingOutLedgerActions = false; }
        }
    }
}


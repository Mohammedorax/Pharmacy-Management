using System;
using System.Drawing;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace project_fo_3
{
    internal sealed class MedicineSearchRequestEventArgs : EventArgs
    {
        internal MedicineSearchRequestEventArgs(string query, object suggestion) { Query = query; Suggestion = suggestion; }
        internal string Query { get; }
        internal object Suggestion { get; }
    }

    // The suggestions stay inside the workspace, strictly below the edit field.
    internal sealed class MedicineSearchComboBox : ComboBox, IMessageFilter
    {
        private SearchResultsWindow popup;
        private ListBox results;
        private readonly List<object> matchedItems = new List<object>();
        private readonly Dictionary<string, object[]> queryResults = new Dictionary<string, object[]>(StringComparer.Ordinal);
        private readonly Queue<string> queryOrder = new Queue<string>();
        private object[] stringSource;
        private CompareInfo queryComparison;

        private bool CanReuseQueryResults(CompareInfo comparison)
        {
            bool unchanged = stringSource != null && stringSource.Length == Items.Count;
            if (unchanged)
                for (int index = 0; index < stringSource.Length; index++)
                    if (!ReferenceEquals(stringSource[index], Items[index])) { unchanged = false; break; }
            if (!unchanged)
            {
                stringSource = null;
                queryResults.Clear(); queryOrder.Clear();
                var source = new object[Items.Count];
                for (int index = 0; index < source.Length; index++)
                {
                    object item = Items[index];
                    // ToString on a mutable object may change without a collection event.
                    if (!(item is string)) return false;
                    source[index] = item;
                }
                stringSource = source;
            }
            if (!ReferenceEquals(queryComparison, comparison))
            {
                queryResults.Clear(); queryOrder.Clear(); queryComparison = comparison;
            }
            return true;
        }
        private Label emptyResults;
        private bool searchLeftToRight;
        private Form owner;
        private bool selecting, showingResults;
        private string resultsInputText;
        internal event EventHandler CommitRequested;
        internal event EventHandler<MedicineSearchRequestEventArgs> SearchRequested;
        private bool explicitResultChoice;
        internal bool ResultsVisible => popup != null && popup.Visible;

        internal void ConfigureSearch(bool? leftToRight = null)
        {
            searchLeftToRight = leftToRight ?? UiDirection.IsLeftToRight;
            DropDownStyle = ComboBoxStyle.DropDown;
            AutoCompleteMode = AutoCompleteMode.None;
            AccessibleDescription = searchLeftToRight
                ? "Type to search. Up/Down selects a result; Enter adds it to the cart."
                : "اكتب للبحث. الأسهم لاختيار نتيجة، وEnter لإضافتها إلى السلة.";
        }

        protected override void OnTextChanged(EventArgs e)
        {
            if (!selecting) explicitResultChoice = false;
            base.OnTextChanged(e);
        }

        protected override void OnTextUpdate(EventArgs e)
        {
            base.OnTextUpdate(e);
            if (!selecting) { explicitResultChoice = false; ShowResults(true); }
        }

        protected override void OnDropDown(EventArgs e)
        {
            DroppedDown = false;
            ShowResults(false);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x14f && m.WParam != IntPtr.Zero) { ShowResults(false); return; } // CB_SHOWDROPDOWN
            if (m.Msg == 0x201 && Enabled) // Native arrow must never open the upward list.
            {
                int x = unchecked((short)((long)m.LParam & 0xffff));
                bool arrow = RightToLeft == RightToLeft.Yes ? x < SystemInformation.VerticalScrollBarWidth + 5
                    : x >= Width - SystemInformation.VerticalScrollBarWidth - 5;
                if (arrow) { Focus(); ShowResults(false); return; }
            }
            base.WndProc(ref m);
        }

        internal void ShowResults(bool filter)
        {
            if (!Enabled || !Visible || IsDisposed) return;
            EnsurePopup();
            if (owner == null || owner.IsDisposed) return;
            // ListBox refreshes its native items even when the assigned font is equal.
            // Keep actual font changes while avoiding a full rebuild on every keystroke.
            if (!results.Font.Equals(Font)) results.Font = Font;
            results.RightToLeft = RightToLeft;
            string query = filter ? Text.Trim() : string.Empty;
            var comparison = CultureInfo.CurrentCulture.CompareInfo;
            int previousCount = results.Items.Count;
            bool cacheable = CanReuseQueryResults(comparison);
            object[] cached;
            matchedItems.Clear();
            if (cacheable && queryResults.TryGetValue(query, out cached)) matchedItems.AddRange(cached);
            else
            {
                foreach (object item in Items)
                {
                    if (query.Length != 0 && comparison.IndexOf(Convert.ToString(item), query, CompareOptions.IgnoreCase) < 0) continue;
                    matchedItems.Add(item);
                }
                // Bound both the number and size of retained result arrays. Broad queries
                // still return every match; they simply do not consume this small cache.
                if (cacheable && matchedItems.Count <= 4096)
                {
                    if (queryResults.Count == 8) queryResults.Remove(queryOrder.Dequeue());
                    queryResults.Add(query, matchedItems.ToArray()); queryOrder.Enqueue(query);
                }
            }
            bool unchanged = matchedItems.Count == previousCount;
            for (int index = 0; unchanged && index < matchedItems.Count; index++)
                unchanged = matchedItems[index] is string && ReferenceEquals(matchedItems[index], results.Items[index]);
            if (!unchanged) results.BeginUpdate();
            try
            {
                if (!unchanged)
                {
                    results.Items.Clear();
                    results.Items.AddRange(matchedItems.ToArray());
                }
                if (results.Items.Count > 0)
                {
                    // Prefer the complete name before a substring match.
                    int exact = matchedItems.FindIndex(item => string.Equals(Convert.ToString(item), query, StringComparison.CurrentCultureIgnoreCase));
                    results.SelectedIndex = exact >= 0 ? exact : 0;
                }
            }
            finally { if (!unchanged) results.EndUpdate(); }
            // The popup can outlive a theme or explicit window-language change.
            results.BackColor = emptyResults.BackColor = popup.BackColor = UiTheme.Surface;
            results.ForeColor = emptyResults.ForeColor = UiTheme.Text;
            results.AccessibleName = AccessibleName + " — " + (searchLeftToRight ? "Results" : "نتائج البحث");
            bool hasMatches = results.Items.Count > 0;
            results.Visible = hasMatches;
            emptyResults.Text = searchLeftToRight ? "No matching items." : "لا توجد أصناف مطابقة.";
            emptyResults.Font = Font;
            emptyResults.RightToLeft = RightToLeft;
            emptyResults.Visible = !hasMatches;
            var point = PointToScreen(new Point(0, Height + 2));
            var work = Screen.FromControl(this).WorkingArea;
            int available = work.Bottom - point.Y - 8;
            if (available < results.ItemHeight + 4) { HideResults(); return; }
            int height = Math.Min(available, Math.Max(1, Math.Min(8, results.Items.Count)) * results.ItemHeight + 4);
            int width = Math.Min(Math.Max(Width, 260), work.Width - 16);
            popup.SetBounds(Math.Max(work.Left + 8, Math.Min(point.X, work.Right - width - 8)), point.Y, width, height);
            resultsInputText = Text;
            showingResults = true;
            try { if (!popup.Visible) popup.Show(owner); }
            finally { showingResults = false; }
        }

        private void EnsurePopup()
        {
            if (popup != null) return;
            owner = FindForm();
            if (owner == null) return;
            popup = new SearchResultsWindow { Name = Name + "SearchResults", FormBorderStyle = FormBorderStyle.FixedSingle,
                ControlBox = false, ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, AutoScaleMode = AutoScaleMode.None };
            results = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false, BorderStyle = BorderStyle.None,
                Font = Font, BackColor = UiTheme.Surface, ForeColor = UiTheme.Text,
                RightToLeft = RightToLeft, AccessibleName = AccessibleName + " — " + (searchLeftToRight ? "Results" : "نتائج البحث"), TabStop = false };
            results.MouseClick += (s, e) => { explicitResultChoice = true; SelectResult(); Focus(); };
            results.MouseDoubleClick += (s, e) => { explicitResultChoice = true; CommitChoice(); };
            results.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter) { e.Handled = e.SuppressKeyPress = true; CommitChoice(); }
                else if (e.KeyCode == Keys.Escape) { e.Handled = e.SuppressKeyPress = true; HideResults(); Focus(); }
            };
            emptyResults = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter,
                BackColor = UiTheme.Surface, ForeColor = UiTheme.Text, TabStop = false };
            popup.Controls.Add(emptyResults);
            popup.Controls.Add(results);
            owner.Deactivate += OwnerDeactivate;
            owner.Resize += OwnerDeactivate;
            owner.LocationChanged += OwnerDeactivate;
            Application.AddMessageFilter(this);
        }

        private void OwnerDeactivate(object sender, EventArgs e) { if (!showingResults) HideResults(); }
        internal void HideResults() { if (popup != null && !popup.IsDisposed) popup.Visible = false; }
        private bool SelectResult()
        {
            if (results == null || results.SelectedItem == null) return false;
            // Programmatic text/source changes do not raise TextUpdate. Never commit
            // a popup choice that belongs to the previous input or item collection.
            if (!string.Equals(Text, resultsInputText, StringComparison.Ordinal) || !Items.Contains(results.SelectedItem))
            {
                ShowResults(true);
                return false;
            }
            selecting = true;
            try { SelectedItem = results.SelectedItem; SelectionStart = Text.Length; SelectionLength = 0; }
            finally { selecting = false; HideResults(); }
            return true;
        }
        internal void CommitChoice()
        {
            if (!Enabled) return;
            if (SearchRequested != null && !explicitResultChoice)
            {
                // A scanner can send an alphanumeric barcode that also matches part
                // of a name. Let the host resolve the barcode before using that name.
                if (ResultsVisible && (!string.Equals(Text, resultsInputText, StringComparison.Ordinal) ||
                    (results.SelectedItem != null && !Items.Contains(results.SelectedItem)))) { ShowResults(true); return; }
                string query = Text.Trim();
                if (query.Length == 0) { ShowResults(true); return; }
                object suggestion = ResultsVisible ? results.SelectedItem : SelectedItem;
                HideResults();
                SearchRequested.Invoke(this, new MedicineSearchRequestEventArgs(query, suggestion));
                return;
            }
            if (ResultsVisible && !SelectResult()) return;
            // Free text is never treated as a selected database item.
            if (SelectedIndex < 0 || !string.Equals(Text, Convert.ToString(SelectedItem), StringComparison.CurrentCultureIgnoreCase)) { ShowResults(true); return; }
            HideResults();
            CommitRequested?.Invoke(this, EventArgs.Empty);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Enter) { CommitChoice(); return true; }
            if (keyData == Keys.Escape && ResultsVisible) { HideResults(); return true; }
            if (keyData == Keys.F4 || keyData == (Keys.Alt | Keys.Down)) { ShowResults(false); return true; }
            if (keyData == Keys.Down || keyData == Keys.Up)
            {
                if (!ResultsVisible) ShowResults(true);
                else if (results.Items.Count > 0)
                {
                    explicitResultChoice = true;
                    results.SelectedIndex = Math.Max(0, Math.Min(results.Items.Count - 1, results.SelectedIndex + (keyData == Keys.Down ? 1 : -1)));
                }
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        public bool PreFilterMessage(ref Message m)
        {
            if (ResultsVisible && (m.Msg == 0x201 || m.Msg == 0x204))
            {
                var clicked = Control.FromChildHandle(m.HWnd);
                if (clicked != this && clicked != results && clicked != popup && (clicked == null || !Contains(clicked))) HideResults();
            }
            return false;
        }
        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); if (!Enabled) HideResults(); }
        protected override void OnLocationChanged(EventArgs e) { base.OnLocationChanged(e); if (!showingResults) HideResults(); }
        protected override void OnSizeChanged(EventArgs e) { base.OnSizeChanged(e); if (!showingResults) HideResults(); }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Application.RemoveMessageFilter(this);
                if (owner != null) { owner.Deactivate -= OwnerDeactivate; owner.Resize -= OwnerDeactivate; owner.LocationChanged -= OwnerDeactivate; }
                matchedItems.Clear(); queryResults.Clear(); queryOrder.Clear(); stringSource = null; queryComparison = null;
                popup?.Dispose();
            }
            base.Dispose(disposing);
        }

        private sealed class SearchResultsWindow : Form
        {
            protected override bool ShowWithoutActivation => true;
            protected override CreateParams CreateParams { get { var p = base.CreateParams; p.ExStyle |= 0x08000000; return p; } }
            protected override void WndProc(ref Message m)
            {
                if (m.Msg == 0x21) { m.Result = new IntPtr(3); return; }
                base.WndProc(ref m);
            }
        }
    }
}



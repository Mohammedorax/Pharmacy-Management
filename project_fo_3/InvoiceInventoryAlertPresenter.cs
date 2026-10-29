using System;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace project_fo_3
{
    // A post-commit read must never change the result of saving the invoice.
    internal static class InvoiceInventoryAlertPresenter
    {
        private sealed class RefreshState
        {
            internal object Current;
        }

        // Keep each status label independent without extending its lifetime.
        private static readonly ConditionalWeakTable<Label, RefreshState> refreshStates =
            new ConditionalWeakTable<Label, RefreshState>();

        internal static string FormatStatus(string template, bool english, params object[] values)
        {
            string translated = english ? UiLanguageCatalog.Translate(template) : template;
            return string.Format(CultureInfo.CurrentCulture, translated, values);
        }

        internal static void Refresh(Label status, DataGridView lines, string reference, Func<bool> isCurrentInvoice)
        { _ = RefreshAsync(status, lines, reference, isCurrentInvoice); }

        internal static async Task RefreshAsync(Label status, DataGridView lines, string reference, Func<bool> isCurrentInvoice, InventoryAlertReadSession suppliedSession = null)
        {
            bool english = status?.FindForm()?.RightToLeft == RightToLeft.No;
            string pending = FormatStatus("تم الحفظ {0} — جارٍ فحص المخزون والصلاحية…", english, reference);
            if (status == null || status.IsDisposed || !status.IsHandleCreated || lines == null || lines.IsDisposed) return;
            var state = refreshStates.GetValue(status, ignored => new RefreshState());
            var request = new object();
            state.Current = request;
            var cancellation = new CancellationTokenSource();
            InventoryAlertReadSession session = null;
            Form owner = status.FindForm();
            FormClosedEventHandler cancelOnClose = (sender, args) => cancellation.Cancel();
            EventHandler cancelOnDispose = (sender, args) => cancellation.Cancel();
            if (owner != null) owner.FormClosed += cancelOnClose;
            status.Disposed += cancelOnDispose;
            try
            {
                // Headless QA saves do not start background UI work.
                string[] names = lines.Rows.Cast<DataGridViewRow>()
                    .Where(row => !row.IsNewRow)
                    .Select(row => Convert.ToString(row.Cells["MedName"].Value))
                    .Distinct(StringComparer.Ordinal).ToArray();
                UiTheme.SetStatus(status, pending, true);
                if (!ReferenceEquals(state.Current, request)) return;
                session = suppliedSession ?? ApplicationServices.CreateInvoiceInventoryAlertReadSession();
                var result = await session.LoadAsync(10, 30, names, cancellation.Token);
                using (DataTable alerts = InventoryStockPresentation.ToTable(result))
                {
                    session.EnsureContext();
                    if (!ReferenceEquals(state.Current, request) || status.IsDisposed || !status.IsHandleCreated || !isCurrentInvoice() || status.Text != pending) return;
                    int expired = 0, soon = 0, low = 0;
                    foreach (DataRow row in alerts.Rows)
                    {
                        string type = Convert.ToString(row["نوع التنبيه"]);
                        if (type == "منتهي") expired++;
                        else if (type == "قريب الانتهاء") soon++;
                        else if (type == "مخزون منخفض") low++;
                    }
                    string message = alerts.Rows.Count == 0
                        ? FormatStatus("تم الحفظ — لا تنبيهات لأصناف الفاتورة.", english)
                        : FormatStatus("تم الحفظ — مخزون منخفض: {0}، قريب الانتهاء: {1}، منتهي: {2}. التفاصيل في تنبيهات المخزون.", english, low, soon, expired);
                    UiTheme.SetStatus(status, message, alerts.Rows.Count == 0);
                }
            }
            catch (Exception exception)
            {
                if (exception is OperationCanceledException || !ReferenceEquals(state.Current, request)) return;
                if (session != null) { try { session.EnsureContext(); } catch (InvalidOperationException) { return; } }
                string supportCode = AppDiagnostics.Log("InvoiceInventoryAlerts", exception);
                if (!status.IsDisposed && status.IsHandleCreated && status.Text == pending && isCurrentInvoice())
                    UiTheme.SetStatus(status, FormatStatus("تم حفظ الفاتورة؛ تعذر تحديث تنبيهات المخزون. رمز الدعم: {0}", english, supportCode), false);
            }
            finally
            {
                if (owner != null) owner.FormClosed -= cancelOnClose;
                status.Disposed -= cancelOnDispose;
                if (ReferenceEquals(state.Current, request)) state.Current = null;
                cancellation.Dispose();
            }
        }
    }
}

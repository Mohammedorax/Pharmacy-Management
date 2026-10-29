using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using PharmacyManagement.Application;

namespace project_fo_3
{
    internal static class CancellationFollowUpSelfTests
    {
        private const string Reference = "QA-{0}-شركة Save";
        private const string Technical = "QA_REFRESH_DETAIL_MUST_STAY_IN_LOG";
        private static int assertions, cases;

        internal static void Run()
        {
            string theme = Properties.Setings.Default.ThemeMode;
            bool opening = Properties.Setings.Default.OpenScreensMaximized;
            if (SessionContext.IsAuthenticated) throw new InvalidOperationException("Owned test process must begin without a session.");
            assertions = cases = 0;
            SessionContext.Start("QA_CANCEL_UI_ONLY", "Manager");
            try
            {
                Properties.Setings.Default.OpenScreensMaximized = false;
                foreach (string mode in new[] { "Light", "Dark" })
                foreach (bool english in new[] { false, true })
                {
                    Properties.Setings.Default.ThemeMode = mode;
                    foreach (bool purchase in new[] { false, true })
                    foreach (string scenario in new[] { "readFailure", "bindFailure", "success", "validation", "dismiss" })
                    {
                        VerifyHistory(purchase, english, scenario);
                        ++cases;
                    }
                    foreach (string scenario in new[] { "statusFailure", "success", "validation", "dismiss" })
                    {
                        VerifyCurrentSale(english, scenario);
                        ++cases;
                    }
                }
            }
            catch (Exception error)
            {
                Console.Error.WriteLine("Cancellation follow-up verification failed: " + error);
                throw;
            }
            finally
            {
                SessionContext.Clear();
                Properties.Setings.Default.ThemeMode = theme;
                Properties.Setings.Default.OpenScreensMaximized = opening;
            }
            Console.WriteLine("PASS cancellation follow-up: cases=" + cases + "; assertions=" + assertions +
                "; real history/cancel handlers with injected application boundaries; no SQL, financial writes or desktop input.");
        }

        private static void VerifyHistory(bool purchase, bool english, string scenario)
        {
            int calls = 0, reads = 0;
            bool committed = false, failRefresh = scenario == "readFailure";
            Action cancel = () =>
            {
                ++calls;
                if (scenario == "validation") throw new UiValidationException("سبب الإلغاء يتجاوز الحد المسموح (500).");
                committed = true;
            };
            Func<bool, SalesHistoryPage> salesRead = legacy =>
            {
                ++reads;
                if (committed && failRefresh) throw new IOException(Technical);
                return new SalesHistoryPage { Mode = SalesHistoryMode.LegacyInvoice, HasLegacyArchive = true,
                    Records = new[] { new SalesHistoryRecord { InvoiceReference = Reference, CustomerName = "QA customer",
                        TotalAmount = 12.50m, IssuedAt = new DateTime(2026, 10, 18), Status = committed ? "ملغاة" : "معتمدة" } } };
            };
            Func<IReadOnlyList<PurchaseHistoryRecord>> purchaseRead = () =>
            {
                ++reads;
                if (committed && failRefresh) throw new IOException(Technical);
                return new[] { new PurchaseHistoryRecord { PurchaseId = Reference, SupplierName = "QA supplier",
                    PurchaseDate = new DateTime(2026, 10, 18), TotalAmount = 12.50m, IsCancelled = committed, SystemCurrencyCode = "SAR" } };
            };
            using (Form form = purchase
                ? (Form)new Purchases(true, english, purchaseRead, new PurchaseCancellation(cancel))
                : new Billing(true, english, null, null, null, null, null, salesRead, new SalesCancellation(cancel)))
            {
                form.StartPosition = FormStartPosition.Manual;
                form.Location = new Point(-20000, -20000);
                form.ShowInTaskbar = false; form.Opacity = 0; form.MaximizeBox = false;
                if (!purchase) Field(form, "isLegacyMappedInvoiceHistory").SetValue(form, true);
                form.Show(); Application.DoEvents();
                Invoke(form, purchase ? "LoadPurchaseHistory" : "LoadInvoiceHistory");
                var grid = (DataGridView)Field(form, purchase ? "PurchaseHistoryGV" : "InvoiceHistoryGV").GetValue(form);
                grid.BindingContext = form.BindingContext;
                Assert(grid.Rows.Count == 1, "History fixture must bind one real row without SQL.");
                grid.CurrentCell = grid.Rows[0].Cells[0]; grid.Rows[0].Selected = true;
                EventHandler bindFailure = (sender, args) => { if (committed) throw new IOException(Technical); };
                if (scenario == "bindFailure") grid.DataSourceChanged += bindFailure;
                string handler = purchase ? "btnCancelPurchaseHistory_Click" : "btnCancelHistoryInvoice_Click";
                var messages = CaptureDialogs(() => Invoke(form, handler, null, EventArgs.Empty), scenario != "dismiss");
                Console.WriteLine("History cancellation: " + (purchase ? "Purchase" : "Sale") + "/" + english + "/" + scenario +
                    "; calls=" + calls + "; reads=" + reads + "; messages=" + messages.Count);
                if (scenario == "readFailure" || scenario == "bindFailure")
                {
                    Assert(calls == 1 && committed, "Cancellation application boundary must return exactly once.");
                    Assert(messages.Count == 1, "A failed refresh after completed cancellation must show one follow-up, not failure then success.");
                    string warning = messages.Single();
                    Assert(warning.Contains(Reference) && !warning.Contains(Technical) &&
                        warning.Contains(english ? "Do not repeat the operation" : "لا تكرر العملية"),
                        "Committed cancellation must retain reference and explicit no-repeat guidance without technical details.");
                    string name = purchase ? "btnCancelPurchaseHistory" : "btnCancelHistoryInvoice";
                    Assert(!form.Controls.Find(name, true).Single().Enabled, "Stale history cancellation must be disabled.");
                    Invoke(form, purchase ? "UpdatePurchaseHistoryActions" : "UpdateInvoiceHistoryActions", true);
                    if (!purchase) Invoke(form, "UpdateLegacyInvoiceHistoryActions", true, true);
                    Assert(!form.Controls.Find(name, true).Single().Enabled, "Selection/action repaint must not enable cancellation on stale history.");
                    Assert(form.Controls.Find(name, true).Single().AccessibleDescription == (english ?
                        "The history could not be refreshed. Reopen it before taking another action." : "تعذر تحديث السجل. أعد فتحه قبل تنفيذ إجراء آخر."),
                        "Stale history actions need a truthful localized accessible reason.");
                    CaptureDialogs(() => Invoke(form, handler, null, EventArgs.Empty), true);
                    Assert(calls == 1, "Direct re-entry on stale history must not repeat cancellation.");
                    int readsBeforePreview = reads;
                    CaptureDialogs(() => Invoke(form, purchase ? "PurchaseHistoryGV_CellDoubleClick" : "InvoiceHistoryGV_CellDoubleClick",
                        null, new DataGridViewCellEventArgs(0, 0)), true);
                    Assert(calls == 1 && reads == readsBeforePreview + 1,
                        "Double-click on stale history must reload before opening details, with no cancellation re-entry.");
                    failRefresh = false; grid.DataSourceChanged -= bindFailure;
                    Invoke(form, purchase ? "LoadPurchaseHistory" : "LoadInvoiceHistory");
                    Assert(grid.Rows.Count == 1 && Convert.ToString(grid.Rows[0].Cells["الحالة"].Value) ==
                        (purchase && english ? "Cancelled" : "ملغاة"), "Successful retry must reload the cancelled record.");
                    Assert(form.Controls.Find(purchase ? "btnPreviewPurchaseHistory" : "btnPreviewInvoiceHistory", true).Single().Enabled,
                        "Successful refresh must restore access to invoice review.");
                    CaptureDialogs(() => Invoke(form, handler, null, EventArgs.Empty), true);
                    Assert(calls == 1, "Reloaded cancelled record must not repeat the operation.");
                }
                else if (scenario == "validation")
                {
                    Assert(calls == 1 && !committed && reads == 1 && messages.Count == 1,
                        "Rejected cancellation must not reload history or claim completion.");
                    Assert(messages[0].Contains(english ? "The cancellation reason exceeds the allowed length (500)." :
                        "سبب الإلغاء يتجاوز الحد المسموح (500).") && !messages[0].Contains("Do not repeat the operation"),
                        "Pre-completion validation must preserve the localized validation path.");
                }
                else if (scenario == "dismiss")
                    Assert(calls == 0 && reads == 1 && messages.Count == 0, "Dismissed confirmation must not submit or refresh.");
                else
                    Assert(calls == 1 && committed && reads == 2 && messages.Count == (purchase ? 0 : 1),
                        "Successful cancellation must keep its ordinary status/confirmation and one refresh.");
                form.Close();
            }
        }

        private static void VerifyCurrentSale(bool english, string scenario)
        {
            int calls = 0;
            bool committed = false;
            var operation = new SalesCancellation(() =>
            {
                ++calls;
                if (scenario == "validation") throw new UiValidationException("سبب الإلغاء يتجاوز الحد المسموح (500).");
                committed = true;
            });
            using (var form = new Billing(true, english, null, null, null, null, null, null, operation))
            {
                form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-20000, -20000);
                form.ShowInTaskbar = false; form.Opacity = 0; form.MaximizeBox = false;
                form.Show(); Application.DoEvents();
                var grid = (DataGridView)Field(form, "BillingGV").GetValue(form);
                if (grid.Rows.Count == 0) grid.Rows.Add();
                foreach (DataGridViewRow row in grid.Rows)
                    if (!row.IsNewRow) row.Cells["BillId"].Value = "QA-LINE-" + row.Index;
                Field(form, "_isInvoiceSaved").SetValue(form, true);
                Field(form, "_isInvoiceCancelled").SetValue(form, false);
                Field(form, "_currentCommercialInvoiceNumber").SetValue(form, null);
                var status = (Control)Field(form, "lblStockStatus").GetValue(form);
                EventHandler failStatus = (sender, args) => { if (committed) throw new IOException(Technical); };
                if (scenario == "statusFailure") status.TextChanged += failStatus;
                var messages = CaptureDialogs(() => Invoke(form, "btnCancelSavedInvoice_Click", null, EventArgs.Empty), scenario != "dismiss");
                Console.WriteLine("Current sale cancellation: " + english + "/" + scenario + "; calls=" + calls + "; messages=" + messages.Count);
                if (scenario == "dismiss")
                    Assert(calls == 0 && !committed && !(bool)Field(form, "_isInvoiceCancelled").GetValue(form), "Dismissed current sale must remain unchanged.");
                else if (scenario == "validation")
                    Assert(calls == 1 && !committed && messages.Count == 1 && !(bool)Field(form, "_isInvoiceCancelled").GetValue(form),
                        "Rejected current sale cancellation must not claim completion.");
                else
                {
                    Assert(calls == 1 && committed && messages.Count == 1 && (bool)Field(form, "_isInvoiceCancelled").GetValue(form),
                        "Returned current sale cancellation must keep its committed UI state.");
                    if (scenario == "statusFailure")
                        Assert(messages[0].Contains(Reference) && !messages[0].Contains(Technical) &&
                            messages[0].Contains(english ? "Do not repeat the operation" : "لا تكرر العملية"),
                            "A post-cancellation status error must show completion guidance.");
                    status.TextChanged -= failStatus;
                    Assert(!form.Controls.Find("btnCancelSavedInvoice", true).Single().Enabled, "Completed current sale must disable cancellation.");
                    Invoke(form, "SetCancelSavedInvoiceEnabled", true);
                    CaptureDialogs(() => Invoke(form, "btnCancelSavedInvoice_Click", null, EventArgs.Empty), true);
                    Assert(calls == 1, "Committed current sale must reject direct or re-enabled re-entry.");
                }
                form.Close();
            }
        }

        private static List<string> CaptureDialogs(Action action, bool approve)
        {
            var messages = new List<string>();
            Exception failure = null;
            using (var timer = new System.Windows.Forms.Timer { Interval = 10 })
            {
                timer.Tick += (sender, args) =>
                {
                    var dialog = Application.OpenForms.OfType<AppNotificationForm>().FirstOrDefault();
                    if (dialog == null) return;
                    try
                    {
                        var yes = dialog.Controls.Find("notificationYes", true).OfType<AntdUI.Button>().FirstOrDefault();
                        if (yes != null)
                            dialog.Controls.Find(approve ? "notificationYes" : "notificationNo", true).OfType<AntdUI.Button>().Single().PerformClick();
                        else
                        {
                            messages.Add(dialog.BuildCopyText());
                            dialog.Controls.Find("notificationOK", true).OfType<AntdUI.Button>().Single().PerformClick();
                        }
                    }
                    catch (Exception error) { failure = error; dialog.Close(); }
                };
                timer.Start();
                try { action(); } finally { timer.Stop(); }
            }
            if (failure != null) throw new InvalidOperationException("Owned dialog callback failed.", failure);
            return messages;
        }

        private static FieldInfo Field(Form form, string name) => form.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        private static void Invoke(Form form, string name, params object[] args)
        {
            try { form.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, args); }
            catch (TargetInvocationException error) { throw error.InnerException ?? error; }
        }
        private static void Assert(bool value, string message) { ++assertions; if (!value) throw new InvalidOperationException(message); }
        private sealed class SalesCancellation : ISalesInvoiceCancellationOperation
        {
            private readonly Action submit;
            internal SalesCancellation(Action submit) { this.submit = submit; }
            public SalesInvoiceCancellationOutcome Cancel(SalesInvoiceCancellationRequest request)
            {
                Assert(request.ActorName == "QA_CANCEL_UI_ONLY" &&
                    (request.InvoiceReference == Reference && request.Reason == "إلغاء من سجل الفواتير" ||
                     string.IsNullOrWhiteSpace(request.InvoiceReference) && request.BillLineIds != null && request.BillLineIds.All(id => id.StartsWith("QA-LINE-", StringComparison.Ordinal)) &&
                     request.Reason == "إلغاء من شاشة نقطة البيع"), "Sales cancellation must retain the raw target, actor and audit reason.");
                submit(); return new SalesInvoiceCancellationOutcome { OriginalInvoiceReference = Reference, TotalAmount = 12.50m, RestoredQuantity = 2 };
            }
        }
        private sealed class PurchaseCancellation : IPurchaseInvoiceCancellationOperation
        {
            private readonly Action submit;
            internal PurchaseCancellation(Action submit) { this.submit = submit; }
            public PurchaseInvoiceCancellationOutcome Cancel(PurchaseInvoiceCancellationRequest request)
            {
                Assert(request.PurchaseId == Reference && request.ActorName == "QA_CANCEL_UI_ONLY" && request.ActorRole == "Manager" &&
                    request.Reason == "إلغاء معتمد من سجل المشتريات", "Purchase cancellation must retain the raw target, actor, role and audit reason.");
                submit(); return new PurchaseInvoiceCancellationOutcome { PurchaseId = Reference, TotalAmount = 12.50m, RemovedQuantity = 2 };
            }
        }
    }
}

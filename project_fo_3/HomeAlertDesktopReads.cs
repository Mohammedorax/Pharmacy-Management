using System;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace project_fo_3
{
    public partial class Home
    {
        internal Func<HomeOperationalAlertReadSession> HomeAlertSessionFactory = ApplicationServices.CreateHomeOperationalAlertReadSession;
        internal Action<Form> HomeAlertDialogPresenter = null;
        private CancellationTokenSource homeAlertCancellation, homeDetailCancellation;
        private int homeAlertGeneration, homeDetailGeneration;
        private Form activeHomeAlertDialog;

        private void CancelHomeAlertReads()
        {
            homeAlertGeneration++; homeDetailGeneration++;
            var alerts = homeAlertCancellation; homeAlertCancellation = null;
            var details = homeDetailCancellation; homeDetailCancellation = null;
            if (alerts != null) { alerts.Cancel(); alerts.Dispose(); }
            if (details != null) { details.Cancel(); details.Dispose(); }
            if (activeHomeAlertDialog != null && !activeHomeAlertDialog.IsDisposed) activeHomeAlertDialog.Close();
        }

        private void ClearHomeAlertCards()
        {
            foreach (Control control in alertStrip.Controls.Cast<Control>().ToArray()) control.Dispose();
            alertStrip.Controls.Clear(); alertStrip.Visible = false;
            LayoutHomeAlertCards(); LayoutHomeTiles();
        }

        private void InvalidateHomeAlertReads()
        {
            CancelHomeAlertReads();
            if (!IsDisposed && !Disposing) { ClearHomeAlertCards(); UpdateSessionHeader(); }
        }

        private void ShowHomeAlertStatus(string text)
        {
            ClearHomeAlertCards(); alertStrip.Visible = true;
            alertStrip.Controls.Add(new Label { Name = "homeAlertStatus", Tag = text, Text = Present(text),
                AccessibleName = Present(text), AutoSize = false, Width = 280, Height = 44,
                TextAlign = presentationLeftToRight ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight,
                ForeColor = UiTheme.MutedText, Font = AppFonts.Create(10F), Margin = Padding.Empty });
            LayoutHomeAlertCards(); LayoutHomeTiles();
        }

        private async void RefreshAlertStrip()
        { await RefreshHomeAlertsAsync(); }

        internal async Task RefreshHomeAlertsAsync()
        {
            if (IsDisposed || Disposing || alertStrip == null) return;
            CancelHomeAlertReads(); ClearHomeAlertCards();
            if (!PermissionService.CanOpen(AppPermission.SystemMonitor)) return;
            int generation = homeAlertGeneration;
            var cancellation = new CancellationTokenSource(); homeAlertCancellation = cancellation;
            ShowHomeAlertStatus("جارٍ تحميل تنبيهات المخزون...");
            try
            {
                var session = HomeAlertSessionFactory();
                using (var alerts = await session.LoadAsync(cancellation.Token))
                {
                    if (IsDisposed || Disposing || generation != homeAlertGeneration) return;
                    session.EnsureContext();
                    if (!PermissionService.CanOpen(AppPermission.SystemMonitor)) { ClearHomeAlertCards(); return; }
                    ClearHomeAlertCards();
                    int expired = alerts.Select("[Type] = 'Expired'").Length;
                    int expiring = alerts.Select("[Type] = 'Expiring Soon'").Length;
                    int lowStock = alerts.Select("[Type] = 'Low Stock'").Length;
                    if (expired + expiring + lowStock == 0) return;
                    alertStrip.Visible = true; alertStrip.Height = AlertStripHeight;
                    if (expired > 0) alertStrip.Controls.Add(CreateAlertCard("أصناف منتهية", expired, UiTheme.Danger, "Expired"));
                    if (expiring > 0) alertStrip.Controls.Add(CreateAlertCard("قرب الانتهاء", expiring, UiTheme.Warning, "Expiring Soon"));
                    if (lowStock > 0) alertStrip.Controls.Add(CreateAlertCard("مخزون منخفض", lowStock, UiTheme.Primary, "Low Stock"));
                    LayoutHomeAlertCards(); LayoutHomeTiles();
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception error)
            {
                if (!IsDisposed && !Disposing && generation == homeAlertGeneration)
                {
                    AppDiagnostics.Log("HomeAlerts", error);
                    if (!PermissionService.CanOpen(AppPermission.SystemMonitor)) ClearHomeAlertCards();
                    else ShowHomeAlertStatus("تعذر تحميل التنبيهات؛ أعد فتح مراقبة النظام أو حاول مجددًا.");
                }
            }
            finally
            {
                if (ReferenceEquals(homeAlertCancellation, cancellation))
                { homeAlertCancellation = null; cancellation.Dispose(); }
            }
        }

        private async void OpenAlertDetails(string alertType, string title, Color accent)
        { await OpenHomeAlertDetailsAsync(alertType, accent); }

        internal async Task OpenHomeAlertDetailsAsync(string alertType, Color accent)
        {
            if (IsDisposed || Disposing || !PermissionService.CanOpen(AppPermission.SystemMonitor)) return;
            if (alertType != "Expired" && alertType != "Expiring Soon" && alertType != "Low Stock") return;
            var previous = homeDetailCancellation;
            if (previous != null) { previous.Cancel(); previous.Dispose(); }
            int generation = ++homeDetailGeneration;
            var cancellation = new CancellationTokenSource(); homeDetailCancellation = cancellation;
            try
            {
                var session = HomeAlertSessionFactory();
                using (var alerts = await session.LoadAsync(cancellation.Token))
                using (var filtered = alerts.Clone())
                {
                    if (IsDisposed || Disposing || generation != homeDetailGeneration) return;
                    session.EnsureContext();
                    if (!PermissionService.CanOpen(AppPermission.SystemMonitor)) return;
                    foreach (DataRow row in alerts.Select("[Type] = '" + alertType + "'")) filtered.ImportRow(row);
                    using (var dialog = CreateAlertDetailsDialog(alertType, filtered, accent, Screen.FromControl(this).WorkingArea.Size))
                    using (var contextTimer = new System.Windows.Forms.Timer { Interval = 250 })
                    {
                        activeHomeAlertDialog = dialog;
                        try
                        {
                            session.EnsureContext();
                            contextTimer.Tick += (sender, args) =>
                            {
                                if (dialog.IsDisposed || !dialog.Visible) return;
                                try
                                {
                                    session.EnsureContext();
                                    if (!PermissionService.CanOpen(AppPermission.SystemMonitor)) InvalidateHomeAlertReads();
                                }
                                catch (InvalidOperationException) { InvalidateHomeAlertReads(); }
                            };
                            contextTimer.Start();
                            ScreenModeSettings.ApplyOpeningPreference(dialog, this);
                            if (HomeAlertDialogPresenter == null) dialog.ShowDialog(this);
                            else HomeAlertDialogPresenter(dialog);
                        }
                        finally { if (ReferenceEquals(activeHomeAlertDialog, dialog)) activeHomeAlertDialog = null; }
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception error)
            {
                if (!IsDisposed && !Disposing && generation == homeDetailGeneration && PermissionService.CanOpen(AppPermission.SystemMonitor))
                    UiTheme.ShowOperationIssue(this, "عرض تفاصيل التنبيه", error, presentationLeftToRight);
            }
            finally
            {
                if (ReferenceEquals(homeDetailCancellation, cancellation))
                { homeDetailCancellation = null; cancellation.Dispose(); }
            }
            if (!IsDisposed && !Disposing && generation == homeDetailGeneration) await RefreshHomeAlertsAsync();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using PharmacyManagement.Application;

namespace project_fo_3
{
    internal sealed class InventoryIntegrityReadSession
    {
        private readonly PharmacyManagement.Application.IInventoryIntegritySessionReadService service;
        private readonly PharmacyManagement.Application.OperationActor actor;
        private readonly Func<bool> contextMatches;
        internal InventoryIntegrityReadSession(PharmacyManagement.Application.IInventoryIntegritySessionReadService service,
            PharmacyManagement.Application.OperationActor actor, Func<bool> contextMatches)
        { this.service = service ?? throw new ArgumentNullException(nameof(service)); this.actor = actor ?? throw new ArgumentNullException(nameof(actor)); this.contextMatches = contextMatches ?? throw new ArgumentNullException(nameof(contextMatches)); }
        internal void EnsureContext()
        { if (!contextMatches()) throw new InvalidOperationException("تغير المستخدم أو اتصال قاعدة البيانات؛ أعد فتح فحص مطابقة المخزون."); }
        internal async Task<PharmacyManagement.Application.OperationalReportResult> LoadAsync(CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested(); EnsureContext();
            var result = await service.LoadAsync(new PharmacyManagement.Application.OperationActor
                { UserName = actor.UserName, UserRole = actor.UserRole }, cancellation).ConfigureAwait(false);
            cancellation.ThrowIfCancellationRequested(); EnsureContext(); return result;
        }
    }

    internal sealed class InventoryOperationalReportReadSession
    {
        private readonly PharmacyManagement.Application.IInventoryOperationalReportReadService service;
        private readonly PharmacyManagement.Application.OperationActor actor;
        private readonly Func<bool> contextMatches;
        internal InventoryOperationalReportReadSession(PharmacyManagement.Application.IInventoryOperationalReportReadService service,
            PharmacyManagement.Application.OperationActor actor, Func<bool> contextMatches)
        { this.service = service ?? throw new ArgumentNullException(nameof(service)); this.actor = actor ?? throw new ArgumentNullException(nameof(actor)); this.contextMatches = contextMatches ?? throw new ArgumentNullException(nameof(contextMatches)); }
        internal void EnsureContext()
        { if (!contextMatches()) throw new InvalidOperationException("تغير المستخدم أو اتصال قاعدة البيانات؛ أعد فتح تقرير المخزون."); }
        internal Task<PharmacyManagement.Application.OperationalReportResult> LoadMovementSummaryAsync(
            PharmacyManagement.Application.InventoryMovementSummaryRequest request, CancellationToken cancellation) =>
            LoadAsync(token => service.LoadMovementSummaryAsync(request, actor, token), cancellation);
        internal Task<PharmacyManagement.Application.OperationalReportResult> LoadTurnoverAsync(
            PharmacyManagement.Application.InventoryTurnoverRequest request, CancellationToken cancellation) =>
            LoadAsync(token => service.LoadTurnoverAsync(request, actor, token), cancellation);
        private async Task<PharmacyManagement.Application.OperationalReportResult> LoadAsync(
            Func<CancellationToken, Task<PharmacyManagement.Application.OperationalReportResult>> load, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested(); EnsureContext();
            var result = await load(cancellation).ConfigureAwait(false);
            cancellation.ThrowIfCancellationRequested(); EnsureContext(); return result;
        }
    }

    internal sealed partial class InventoryMovementSummaryForm
    {
        private InventoryOperationalReportReadSession summaryReadSession;
        private CancellationTokenSource summaryReadCancellation;
        private int summaryReadGeneration;
        private void CancelSummaryRead()
        {
            summaryReadGeneration++; var previous = summaryReadCancellation; summaryReadCancellation = null;
            if (previous != null) { previous.Cancel(); previous.Dispose(); }
        }
        private void ClearSummaryResults()
        {
            BindGrid(new DataTable());
            SetSummary(Metric("عدد الأصناف", "-"), Metric("إجمالي الوارد", "-"), Metric("إجمالي الصادر", "-"), Metric("صافي الحركة", "-"));
        }
        private void InvalidateSummaryResults()
        {
            if (IsDisposed || Disposing) return;
            CancelSummaryRead(); ClearSummaryResults();
            UiTheme.SetStatus(StatusLabel, Present("تغيرت المرشحات؛ اضغط تحديث لقراءة التقرير مجددًا."), null);
        }
        internal async Task RefreshMovementSummaryAsync()
        {
            DateTime from, to;
            if (!AntdDateInput.TryRead(fromDate, out from) || !AntdDateInput.TryRead(toDate, out to))
            { InvalidateSummaryResults(); UiTheme.SetStatus(StatusLabel, Present("اختر تاريخ البداية والنهاية قبل عرض التقرير."), false); return; }
            if (!TryValidateDateRange(from, to)) { InvalidateSummaryResults(); return; }
            if (!loadData) { UiTheme.SetStatus(StatusLabel, Present("معاينة ملخص حركة المخزون؛ لم تُقرأ قاعدة البيانات."), null); return; }
            CancelSummaryRead(); int generation = summaryReadGeneration;
            var cancellation = new CancellationTokenSource(); summaryReadCancellation = cancellation;
            ClearSummaryResults();
            var request = new PharmacyManagement.Application.InventoryMovementSummaryRequest(from, to, search.Text);
            UiTheme.SetStatus(StatusLabel, Present("جارٍ تحميل بيانات المخزون..."), null);
            try
            {
                var session = summaryReadSession ?? (summaryReadSession = ApplicationServices.CreateInventoryOperationalReportReadSession());
                var result = await session.LoadMovementSummaryAsync(request, cancellation.Token);
                if (IsDisposed || Disposing || generation != summaryReadGeneration || request.SearchText != search.Text.Trim()) return;
                DateTime currentFrom, currentTo;
                if (!AntdDateInput.TryRead(fromDate, out currentFrom) || !AntdDateInput.TryRead(toDate, out currentTo) ||
                    currentFrom.Date != request.FromDate || currentTo.Date != request.ToDate) return;
                session.EnsureContext();
                var table = InventoryStockPresentation.ToTable(result);
                SetEmptyGridHint(Present("لا توجد حركات أصناف مطابقة للفترة والبحث المحددين."));
                BindGrid(table,session.EnsureContext);
                SetSummary(Metric("عدد الأصناف", table.Rows.Count.ToString("N0")),
                    Metric("إجمالي الوارد", SumDecimal(table, "إجمالي الوارد").ToString("#,##0.###")),
                    Metric("إجمالي الصادر", SumDecimal(table, "إجمالي الصادر").ToString("#,##0.###")),
                    Metric("صافي الحركة", SumDecimal(table, "صافي الحركة").ToString("#,##0.###")));
                SetLoadedStatus(table, "تم تحميل التقرير الإجمالي.");
            }
            catch (OperationCanceledException) { }
            catch (Exception error)
            { if (!IsDisposed && !Disposing && generation == summaryReadGeneration) UiTheme.SetStatus(StatusLabel, Present("تعذر تحميل التقرير: ") + error.Message, false); }
        }
    }

    internal sealed partial class InventoryTurnoverForm
    {
        private InventoryOperationalReportReadSession turnoverReadSession;
        private CancellationTokenSource turnoverReadCancellation;
        private int turnoverReadGeneration;
        private void CancelTurnoverRead()
        {
            turnoverReadGeneration++; var previous = turnoverReadCancellation; turnoverReadCancellation = null;
            if (previous != null) { previous.Cancel(); previous.Dispose(); }
        }
        private void ClearTurnoverResults()
        {
            BindGrid(new DataTable());
            SetSummary(Metric("عدد الأصناف", "-"), Metric("إجمالي الصادر", "-"), Metric("إجمالي الوارد", "-"), Metric("الأصناف الراكدة", "-"));
        }
        private void InvalidateTurnoverResults()
        {
            if (IsDisposed || Disposing) return;
            CancelTurnoverRead(); ClearTurnoverResults();
            UiTheme.SetStatus(StatusLabel, Present("تغيرت المرشحات؛ اضغط تحديث لقراءة التقرير مجددًا."), null);
        }
        internal async Task RefreshTurnoverAsync()
        {
            DateTime from, to;
            if (!AntdDateInput.TryRead(fromDate, out from) || !AntdDateInput.TryRead(toDate, out to))
            { InvalidateTurnoverResults(); UiTheme.SetStatus(StatusLabel, Present("اختر تاريخ البداية والنهاية قبل عرض معدل الدوران."), false); return; }
            if (!TryValidateDateRange(from, to)) { InvalidateTurnoverResults(); return; }
            int threshold = ParseNonNegativeInt(idleDays, "أيام الركود");
            if (threshold < 1 || threshold > 3650)
            { InvalidateTurnoverResults(); UiTheme.SetStatus(StatusLabel, Present("أيام الركود يجب أن تكون بين 1 و3650 يومًا."), false); idleDays.Focus(); return; }
            if (!loadData) { UiTheme.SetStatus(StatusLabel, Present("معاينة معدل الدوران؛ لم تُقرأ قاعدة البيانات."), null); return; }
            CancelTurnoverRead(); int generation = turnoverReadGeneration;
            var cancellation = new CancellationTokenSource(); turnoverReadCancellation = cancellation;
            ClearTurnoverResults();
            var request = new PharmacyManagement.Application.InventoryTurnoverRequest(from, to, threshold);
            UiTheme.SetStatus(StatusLabel, Present("جارٍ تحميل بيانات المخزون..."), null);
            try
            {
                var session = turnoverReadSession ?? (turnoverReadSession = ApplicationServices.CreateInventoryOperationalReportReadSession());
                var result = await session.LoadTurnoverAsync(request, cancellation.Token);
                if (IsDisposed || Disposing || generation != turnoverReadGeneration) return;
                DateTime currentFrom, currentTo;
                if (!AntdDateInput.TryRead(fromDate, out currentFrom) || !AntdDateInput.TryRead(toDate, out currentTo) ||
                    currentFrom.Date != request.FromDate || currentTo.Date != request.ToDate ||
                    ParseNonNegativeInt(idleDays, "أيام الركود") != request.IdleDays) return;
                session.EnsureContext();
                var table = InventoryStockPresentation.ToTable(result);
                SetEmptyGridHint(Present("لا توجد أصناف مطابقة لفترة تقرير الدوران."));
                BindGrid(table,session.EnsureContext);
                SetSummary(Metric("عدد الأصناف", table.Rows.Count.ToString("N0")),
                    Metric("إجمالي الصادر", SumDecimal(table, "الصادر خلال الفترة").ToString("#,##0.###")),
                    Metric("إجمالي الوارد", SumDecimal(table, "الوارد خلال الفترة").ToString("#,##0.###")),
                    Metric("الأصناف الراكدة", CountClassification(table, "راكد").ToString("N0")));
                SetLoadedStatus(table, "تم تحميل معدل الدوران والركود.");
            }
            catch (OperationCanceledException) { }
            catch (Exception error)
            { if (!IsDisposed && !Disposing && generation == turnoverReadGeneration) UiTheme.SetStatus(StatusLabel, Present("تعذر تحميل معدل الدوران: ") + error.Message, false); }
        }
    }

    internal sealed class InventoryAlertReadSession
    {
        private readonly PharmacyManagement.Application.IInventoryAlertSessionReadService service;
        private readonly PharmacyManagement.Application.OperationActor actor;
        private readonly Func<bool> contextMatches;

        internal InventoryAlertReadSession(PharmacyManagement.Application.IInventoryAlertSessionReadService service,
            PharmacyManagement.Application.OperationActor actor, Func<bool> contextMatches)
        {
            this.service = service ?? throw new ArgumentNullException(nameof(service));
            this.actor = actor ?? throw new ArgumentNullException(nameof(actor));
            this.contextMatches = contextMatches ?? throw new ArgumentNullException(nameof(contextMatches));
        }

        internal void EnsureContext()
        {
            if (!contextMatches()) throw new InvalidOperationException("تغير المستخدم أو اتصال قاعدة البيانات؛ أعد فتح شاشة التنبيهات.");
        }

        internal async Task<PharmacyManagement.Application.OperationalReportResult> LoadAsync(int lowStockThreshold,
            int expiringWithinDays, CancellationToken cancellation)
        { return await LoadAsync(lowStockThreshold, expiringWithinDays, null, cancellation).ConfigureAwait(false); }

        internal async Task<PharmacyManagement.Application.OperationalReportResult> LoadAsync(int lowStockThreshold,
            int expiringWithinDays, IEnumerable<string> affectedMedicines, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            EnsureContext();
            var request = new PharmacyManagement.Application.InventoryAlertRequest(lowStockThreshold, expiringWithinDays, affectedMedicines);
            var result = await service.GetAlertsAsync(request,
                new PharmacyManagement.Application.OperationActor { UserName = actor.UserName, UserRole = actor.UserRole }, cancellation)
                .ConfigureAwait(false);
            cancellation.ThrowIfCancellationRequested();
            EnsureContext();
            return result;
        }
    }

    internal sealed partial class InventoryAlertsForm
    {
        private InventoryAlertReadSession alertReadSession;
        private CancellationTokenSource alertReadCancellation;
        private int alertReadGeneration;

        private void CancelAlertRead()
        {
            alertReadGeneration++;
            var previous = alertReadCancellation;
            alertReadCancellation = null;
            if (previous != null) { previous.Cancel(); previous.Dispose(); }
        }

        private void SetAlertOutputReady(bool ready)
        {
            foreach (string name in new[] { "inventoryAlertsPrint", "inventoryAlertsExport" })
                foreach (Control button in Controls.Find(name, true)) button.Enabled = ready;
        }

        private void ClearAlertResults()
        {
            SetAlertOutputReady(false);
            BindGrid(new DataTable());
            SetSummary(Metric("عدد التنبيهات", "-"), Metric("منتهي", "-"),
                Metric("قريب الانتهاء", "-"), Metric("مخزون منخفض", "-"));
        }

        private void InvalidateAlertResults()
        {
            if (IsDisposed || Disposing) return;
            CancelAlertRead();
            ClearAlertResults();
            UiTheme.SetStatus(StatusLabel, Present("تغيرت حدود التنبيه؛ اضغط تحديث لقراءة النتيجة الجديدة."), null);
        }

        internal async Task RefreshAlertsAsync()
        {
            int stockLimit = ParseNonNegativeInt(lowStockThreshold, "حد المخزون المنخفض");
            int expiryLimit = ParseNonNegativeInt(expiringDays, "الأيام قبل الانتهاء");
            if (expiryLimit > 3650)
            {
                UiTheme.SetStatus(StatusLabel, Present("الأيام قبل الانتهاء يجب أن تكون بين 0 و3650 يومًا؛ الصفر يعني اليوم فقط."), false);
                expiringDays.Focus();
                return;
            }
            if (!loadData)
            {
                UiTheme.SetStatus(StatusLabel, Present("معاينة تنبيهات المخزون؛ لم تُقرأ قاعدة البيانات."), null);
                return;
            }

            CancelAlertRead();
            int generation = alertReadGeneration;
            var cancellation = new CancellationTokenSource();
            alertReadCancellation = cancellation;
            ClearAlertResults();
            UiTheme.SetStatus(StatusLabel, Present("جارٍ تحميل تنبيهات المخزون..."), null);
            try
            {
                var session = alertReadSession ?? (alertReadSession = ApplicationServices.CreateInventoryAlertReadSession());
                var result = await session.LoadAsync(stockLimit, expiryLimit, cancellation.Token);
                if (IsDisposed || Disposing || generation != alertReadGeneration) return;
                if (ParseNonNegativeInt(lowStockThreshold, "حد المخزون المنخفض") != stockLimit ||
                    ParseNonNegativeInt(expiringDays, "الأيام قبل الانتهاء") != expiryLimit) return;
                session.EnsureContext();
                var table = InventoryStockPresentation.ToTable(result);
                BindGrid(table,session.EnsureContext);
                HighlightRows();
                SetSummary(Metric("عدد التنبيهات", table.Rows.Count.ToString("N0")),
                    Metric("منتهي", CountByType(table, "منتهي").ToString("N0")),
                    Metric("قريب الانتهاء", CountByType(table, "قريب الانتهاء").ToString("N0")),
                    Metric("مخزون منخفض", CountByType(table, "مخزون منخفض").ToString("N0")));
                SetLoadedStatus(table, "تم تحميل تنبيهات المخزون.");
                if (alertPreferenceLoadWarning != null) UiTheme.SetStatus(StatusLabel, StatusLabel.Text + " " + Present(alertPreferenceLoadWarning), null);
                SetAlertOutputReady(table.Rows.Count > 0);
            }
            catch (OperationCanceledException) { }
            catch (Exception error)
            {
                if (!IsDisposed && !Disposing && generation == alertReadGeneration)
                    UiTheme.SetStatus(StatusLabel, Present("تعذر تحميل التنبيهات: ") + error.Message, false);
            }
        }
    }

    internal sealed partial class InventoryMovementForm
    {
        private InventoryPostingSession movementSession;
        private CancellationTokenSource movementCancellation;
        private int movementGeneration;
        private bool bindingMovementMedicine;
        private bool movementLookupReady;
        private bool movementResultReady;

        private void CancelMovementRead()
        {
            movementGeneration++;
            var previous = movementCancellation;
            movementCancellation = null;
            if (previous != null) { previous.Cancel(); previous.Dispose(); }
        }

        private void SetMovementOutputReady(bool ready)
        {
            movementResultReady = ready;
            foreach (string name in new[] { "inventoryMovementPrint", "inventoryMovementExport" })
                foreach (System.Windows.Forms.Control button in Controls.Find(name, true)) button.Enabled = ready;
        }

        private void ClearMovementResults()
        {
            SetMovementOutputReady(false);
            BindGrid(new DataTable());
            SetSummary(Metric("عدد الحركات", "-"), Metric("إجمالي الوارد", "-"),
                Metric("إجمالي الصادر", "-"), Metric("صافي الحركة", "-"));
        }

        private void InvalidateMovementResults()
        {
            if (bindingMovementMedicine || IsDisposed || Disposing) return;
            CancelMovementRead();
            ClearMovementResults();
            UiTheme.SetStatus(StatusLabel, Present("اختر الصنف والفترة، ثم اضغط تحديث لعرض حركات المخزون."), null);
        }

        private void EnableMovementFilters(bool enabled)
        {
            medicine.Enabled = fromDate.Enabled = toDate.Enabled = enabled;
            foreach (System.Windows.Forms.Control button in Controls.Find("inventoryMovementRefresh", true)) button.Enabled = enabled;
        }

        private async Task LoadMovementAsync()
        {
            CancelMovementRead();
            int generation = movementGeneration;
            var cancellation = new CancellationTokenSource();
            movementCancellation = cancellation;
            ClearMovementResults();
            EnableMovementFilters(false);
            UiTheme.SetStatus(StatusLabel, Present("جارٍ تحميل بيانات المخزون..."), null);
            try
            {
                if (movementSession == null) movementSession = InventoryPostingDesktopService.CaptureSession();
                var rows = await movementSession.LoadMedicinesAsync(cancellation.Token);
                if (IsDisposed || Disposing || generation != movementGeneration) return;
                movementSession.EnsureContext();
                var table = new DataTable();
                table.Columns.Add("MedName"); table.Columns.Add("DisplayName"); table.Columns.Add("MedQty", typeof(int));
                foreach (var row in rows) table.Rows.Add(row.Name, row.DisplayName, row.Quantity);
                bindingMovementMedicine = true;
                try { InventoryMedicineSelection.Bind(medicine, table); }
                finally { bindingMovementMedicine = false; }
                movementLookupReady = true;
                EnableMovementFilters(true);
                await RefreshMovementAsync();
            }
            catch (OperationCanceledException) { }
            catch (Exception error)
            {
                if (!IsDisposed && !Disposing && generation == movementGeneration)
                    UiTheme.SetStatus(StatusLabel, Present("تعذر تحميل حركة الصنف: ") + error.Message, false);
            }
            finally { if (!IsDisposed && !Disposing) EnableMovementFilters(true); }
        }

        private async Task RefreshMovementOrLookupAsync()
        {
            if (loadData && !movementLookupReady) await LoadMovementAsync();
            else await RefreshMovementAsync();
        }

        internal async Task RefreshMovementAsync()
        {
            CancelMovementRead();
            int generation = movementGeneration;
            ClearMovementResults();
            string selected = SelectedMedicine();
            if (string.IsNullOrWhiteSpace(selected))
            {
                UiTheme.SetStatus(StatusLabel, Present("اختر صنفًا لعرض حركته."), null);
                return;
            }
            DateTime from, to;
            if (!AntdDateInput.TryRead(fromDate, out from) || !AntdDateInput.TryRead(toDate, out to))
            {
                UiTheme.SetStatus(StatusLabel, Present("اختر تاريخ البداية والنهاية قبل عرض الحركة."), false);
                return;
            }
            if (!TryValidateDateRange(from, to)) return;
            if (!loadData && movementSession == null)
            {
                UiTheme.SetStatus(StatusLabel, Present("معاينة حركة الصنف؛ لم تُقرأ قاعدة البيانات."), null);
                return;
            }
            var cancellation = new CancellationTokenSource();
            movementCancellation = cancellation;
            UiTheme.SetStatus(StatusLabel, Present("جارٍ تحميل بيانات المخزون..."), null);
            try
            {
                var session = movementSession ?? throw new InvalidOperationException("سياق قراءة المخزون غير جاهز.");
                var snapshot = await session.LoadSnapshotAsync(new InventoryOperationReadRequest(selected, from, to), cancellation.Token);
                if (IsDisposed || Disposing || generation != movementGeneration || selected != SelectedMedicine()) return;
                DateTime currentFrom, currentTo;
                if (!AntdDateInput.TryRead(fromDate, out currentFrom) || !AntdDateInput.TryRead(toDate, out currentTo) ||
                    currentFrom != from || currentTo != to) return;
                session.EnsureContext();
                PresentMovementTable(InventoryMovementPresentation.ToTable(snapshot),session.EnsureContext);
            }
            catch (OperationCanceledException) { }
            catch (Exception error)
            {
                if (!IsDisposed && !Disposing && generation == movementGeneration)
                    UiTheme.SetStatus(StatusLabel, Present("تعذر تحميل حركة الصنف: ") + error.Message, false);
            }
        }
    }
}


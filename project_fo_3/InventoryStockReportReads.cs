using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using PharmacyManagement.Application;

namespace project_fo_3
{
    internal sealed class InventoryStockReadSession
    {
        private readonly IInventoryStockReadService service;
        private readonly OperationActor actor;
        private readonly Func<bool> contextMatches;
        internal InventoryStockReadSession(IInventoryStockReadService service, OperationActor actor, Func<bool> contextMatches)
        { this.service = service ?? throw new ArgumentNullException(nameof(service)); this.actor = actor ?? throw new ArgumentNullException(nameof(actor)); this.contextMatches = contextMatches ?? throw new ArgumentNullException(nameof(contextMatches)); }
        internal void EnsureContext()
        { if (!contextMatches()) throw new InvalidOperationException("تغير المستخدم أو اتصال قاعدة البيانات؛ أعد فتح كشف المخزون."); }
        internal async Task<OperationalReportResult> LoadAsync(InventoryStockReadRequest request, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested(); EnsureContext();
            var result = await service.LoadAsync(request, new OperationActor { UserName = actor.UserName, UserRole = actor.UserRole }, cancellation).ConfigureAwait(false);
            cancellation.ThrowIfCancellationRequested(); EnsureContext(); return result;
        }
    }

    internal static class InventoryStockPresentation
    {
        internal static DataTable ToTable(OperationalReportResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            var table = new DataTable();
            foreach (var column in result.Columns) table.Columns.Add(new DataColumn(column.Name, column.DataType) { AllowDBNull = true });
            foreach (var row in result.Rows)
            {
                var values = new object[row.Count];
                for (int index = 0; index < row.Count; index++) values[index] = row[index] ?? DBNull.Value;
                table.Rows.Add(values);
            }
            return table;
        }
    }

    internal sealed partial class InventoryStockReportForm
    {
        private InventoryStockReadSession stockReadSession;
        private CancellationTokenSource stockReadCancellation;
        private int stockReadGeneration;
        private bool stockReadStarted;
        private bool stockResultReady;

        private void CancelStockRead()
        {
            stockReadGeneration++;
            var old = stockReadCancellation; stockReadCancellation = null;
            if (old != null) { old.Cancel(); old.Dispose(); }
        }
        private void SetStockOutputReady(bool ready)
        {
            stockResultReady = ready;
            foreach (string name in new[] { "inventoryStockPrint", "inventoryStockExport" })
                foreach (Control button in Controls.Find(name, true)) button.Enabled = ready;
        }
        private void ClearStockResult()
        {
            SetStockOutputReady(false); BindGrid(new DataTable());
            SetSummary(Metric(lotMode ? "عدد التشغيلات" : "عدد الأصناف", "-"), Metric("إجمالي الكمية", "-"),
                Metric("قيمة التكلفة", "-"), Metric(lotMode ? "كمية قابلة للصرف" : "قيمة البيع", "-"));
        }
        private void InvalidateStockResult()
        {
            if (!stockReadStarted || IsDisposed || Disposing) return;
            CancelStockRead(); ClearStockResult();
            UiTheme.SetStatus(StatusLabel, Present(lotMode ? "ابحث أو اضغط تحديث لعرض أرصدة التشغيلات وصلاحياتها." : "ابحث عن صنف أو اضغط تحديث لعرض أرصدة المخزون الحالية."), null);
        }
        private void RefreshStockReport() { _ = RefreshStockReportAsync(); }

        internal async Task RefreshStockReportAsync()
        {
            if (!loadData)
            {
                UiTheme.SetStatus(StatusLabel, Present(lotMode ? "معاينة جرد التشغيلات؛ لم تُقرأ قاعدة البيانات." : "معاينة كشف الجرد؛ لم تُقرأ قاعدة البيانات."), null);
                return;
            }
            stockReadStarted = true;
            CancelStockRead(); int generation = stockReadGeneration;
            var cancellation = new CancellationTokenSource(); stockReadCancellation = cancellation;
            ClearStockResult();
            var request = new InventoryStockReadRequest(lotMode ? InventoryStockReadMode.CommercialLots : InventoryStockReadMode.Basic,
                search.Text, includeZero.Checked);
            UiTheme.SetStatus(StatusLabel, Present("جارٍ تحميل بيانات المخزون..."), null);
            try
            {
                var session = stockReadSession ?? (stockReadSession = stockSessionFactory());
                var result = await session.LoadAsync(request, cancellation.Token);
                if (IsDisposed || Disposing || generation != stockReadGeneration ||
                    request.SearchText != search.Text.Trim() || request.IncludeZero != includeZero.Checked) return;
                session.EnsureContext();
                DataTable table = InventoryStockPresentation.ToTable(result);
                SetEmptyGridHint(Present(lotMode ? "لا توجد تشغيلات مطابقة؛ في الوضع القديم استخدم كشف الجرد المجمع." : "لا توجد أصناف مطابقة للبحث وخيار العرض المحددين."));
                BindGrid(table,session.EnsureContext);
                SetSummary(Metric(lotMode ? "عدد التشغيلات" : "عدد الأصناف", table.Rows.Count.ToString("N0")),
                    Metric("إجمالي الكمية", lotMode ? SumDecimal(table, "الكمية الحالية").ToString("N3") : SumDecimal(table, "الكمية الحالية").ToString("#,##0.###")),
                    Metric("قيمة التكلفة", FormatMoney(SumDecimal(table, "قيمة التكلفة"))),
                    Metric(lotMode ? "كمية قابلة للصرف" : "قيمة البيع", lotMode ? SumDecimal(table, "الكمية القابلة للصرف").ToString("N3") : FormatMoney(SumDecimal(table, "قيمة البيع"))));
                SetLoadedStatus(table, lotMode ? "تم تحميل جرد التشغيلات." : "تم تحميل كشف الجرد.");
                SetStockOutputReady(table.Rows.Count > 0);
            }
            catch (OperationCanceledException) { }
            catch (Exception error)
            {
                if (!IsDisposed && !Disposing && generation == stockReadGeneration)
                    UiTheme.SetStatus(StatusLabel, Present(lotMode ? "تعذر تحميل جرد التشغيلات: " : "تعذر تحميل كشف الجرد: ") + error.Message, false);
            }
        }
    }
}


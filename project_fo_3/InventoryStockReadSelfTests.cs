using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using PharmacyManagement.Application;

namespace project_fo_3
{
    internal static class InventoryStockReadSelfTests
    {
        internal static void Run()
        {
            string originalTheme = Properties.Setings.Default.ThemeMode;
            try
            {
                foreach (string theme in new[] { "Light", "Dark" })
                foreach (bool leftToRight in new[] { false, true })
                foreach (bool lotMode in new[] { false, true })
                    VerifyScreen(theme, leftToRight, lotMode);
            }
            finally { Properties.Setings.Default.ThemeMode = originalTheme; }
            VerifyProjection();
            RunNotification();
            Console.WriteLine("PASS inventory stock async reads: both report schemas, live filter cancellation, late-result rejection, retry, empty output, changed context, disposal, both themes and directions; mock reads only.");
        }

        internal static void RunNotification()
        {
            string theme = Properties.Setings.Default.ThemeMode;
            try
            {
                foreach (string mode in new[] { "Light", "Dark" })
                foreach (bool english in new[] { false, true })
                foreach (bool lots in new[] { false, true })
                {
                    Properties.Setings.Default.ThemeMode = mode;
                    SessionContext.Start("stock-notification", "Manager", "fixture");
                    var reader = new Probe(); int captures = 0;
                    Func<InventoryStockReadSession> factory = () =>
                    {
                        captures++; long revision = SessionContext.CurrentSessionRevision;
                        return new InventoryStockReadSession(reader, new OperationActor { UserName = "stock-reader", UserRole = "Administrator" }, () => revision == SessionContext.CurrentSessionRevision);
                    };
                    using (var form = new InventoryStockReportForm(lots, true, factory))
                    {
                        form.ShowInTaskbar = false; form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-20000, -20000);
                        form.ApplyPresentation(english); form.Show(); Application.DoEvents();
                        reader.Pending[0].SetResult(Fixture(lots)); Finish(reader.Tasks[0]); Application.DoEvents();
                        var grid = Require<DataGridView>(form, "inventoryDataGrid");
                        var print = Require<AntdUI.Button>(form, "inventoryStockPrint"); var export = Require<AntdUI.Button>(form, "inventoryStockExport");
                        if (grid.Rows.Count != 1 || !print.Enabled) throw new Exception("Stock notification fixture was not loaded.");
                        SessionContext.Start("stock-notification", "Manager", "fixture");
                        if (grid.Rows.Count != 0 || print.Enabled || export.Enabled) throw new Exception("Stock notification retained loaded output.");
                        var retry = form.RefreshStockReportAsync(); reader.Pending[1].SetResult(Fixture(lots)); Finish(retry);
                        if (captures != 2 || grid.Rows.Count != 1) throw new Exception("Stock notification failed to capture fresh session and recover.");
                        var stale = form.RefreshStockReportAsync(); SessionContext.Start("stock-notification", "Manager", "fixture");
                        if (!reader.Tokens[2].IsCancellationRequested || grid.Rows.Count != 0 || print.Enabled || export.Enabled) throw new Exception("Stock notification retained pending read/output.");
                        reader.Pending[2].SetResult(Fixture(lots)); Finish(stale);
                        if (grid.Rows.Count != 0) throw new Exception("Stock late result revived old output.");
                        var fresh = form.RefreshStockReportAsync(); reader.Pending[3].SetResult(Fixture(lots)); Finish(fresh);
                        if (captures != 3 || grid.Rows.Count != 1 || !print.Enabled || !export.Enabled) throw new Exception("Stock fresh retry did not recover output.");
                    }
                }
            }
            finally { SessionContext.Clear(); Properties.Setings.Default.ThemeMode = theme; }
            Console.WriteLine("PASS stock/lot notification: loaded/pending output cleared, cancellation, late rejection, fresh session retry; both themes/directions; no SQL/native keys.");
        }
        private static void VerifyScreen(string theme, bool leftToRight, bool lotMode)
        {
            Properties.Setings.Default.ThemeMode = theme;
            using (var form = new InventoryStockReportForm(lotMode, false))
            {
                form.ShowInTaskbar = false;
                form.StartPosition = FormStartPosition.Manual;
                form.Location = new Point(-20000, -20000);
                form.ApplyPresentation(leftToRight);
                var reader = new Probe();
                bool contextMatches = true;
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                typeof(InventoryStockReportForm).GetField("stockReadSession", flags).SetValue(form,
                    new InventoryStockReadSession(reader, new OperationActor { UserName = "stock-reader", UserRole = "Administrator" }, () => contextMatches));
                typeof(InventoryStockReportForm).GetField("loadData", flags).SetValue(form, true);
                form.Show(); Application.DoEvents();
                if (reader.Pending.Count != 1) throw new Exception("Initial stock read was not dispatched.");
                var grid = Require<DataGridView>(form, "inventoryDataGrid");
                var print = Require<AntdUI.Button>(form, "inventoryStockPrint");
                var export = Require<AntdUI.Button>(form, "inventoryStockExport");
                if (print.Enabled || export.Enabled || grid.Rows.Count != 0) throw new Exception("Pending stock read exposed stale output.");
                bool pumped = false; form.BeginInvoke(new Action(() => pumped = true)); Application.DoEvents();
                if (!pumped) throw new Exception("Pending stock read blocked the UI message loop.");

                var search = Require<TextBox>(form, "inventoryStockSearch");
                search.Text = "  باراسيتامول 500 mg  ";
                if (!reader.Tokens[0].IsCancellationRequested) throw new Exception("Search change did not cancel stock read.");
                var searchTask = form.RefreshStockReportAsync();
                if (reader.Requests[1].SearchText != "باراسيتامول 500 mg") throw new Exception("Stock read did not trim the visible search.");
                var includeZero = Require<AntdUI.Checkbox>(form, "inventoryStockIncludeZero");
                includeZero.Checked = false;
                if (!reader.Tokens[1].IsCancellationRequested || print.Enabled || export.Enabled || grid.Rows.Count != 0)
                    throw new Exception("Include-zero change retained or failed to cancel stale output.");
                var currentTask = form.RefreshStockReportAsync();
                if (reader.Requests[2].Mode != (lotMode ? InventoryStockReadMode.CommercialLots : InventoryStockReadMode.Basic) || reader.Requests[2].IncludeZero)
                    throw new Exception("Stock read ignored the active mode/include-zero filter.");
                reader.Pending[2].SetResult(Fixture(lotMode)); Finish(currentTask);
                reader.Pending[0].SetResult(Fixture(lotMode, "obsolete")); Finish(reader.Tasks[0]);
                reader.Pending[1].SetResult(Fixture(lotMode, "obsolete")); Finish(searchTask);
                string[] expected = lotMode
                    ? new[] { "الصنف", "الباركود", "الشركة", "رقم التشغيلة", "GTIN", "المورد", "الكمية المستلمة", "الكمية الحالية", "الكمية القابلة للصرف", "تكلفة الوحدة", "سعر البيع", "قيمة التكلفة", "قيمة البيع", "تاريخ الإنتاج", "تاريخ الانتهاء", "حالة التشغيلة", "معرف التشغيلة" }
                    : new[] { "الصنف", "الباركود", "الشركة", "الوحدة", "الكمية الحالية", "سعر التكلفة", "سعر البيع", "قيمة التكلفة", "قيمة البيع", "تاريخ الإنتاج", "تاريخ الانتهاء", "الحالة" };
                if (grid.Rows.Count != 1 || grid.Columns.Count != expected.Length || !grid.Columns.Cast<DataGridViewColumn>().Select(column => column.Name).SequenceEqual(expected) ||
                    Convert.ToString(grid.Rows[0].Cells["الصنف"].Value) != "باراسيتامول 500 mg" || !print.Enabled || !export.Enabled)
                    throw new Exception("Stock result changed order/schema or accepted an obsolete response.");
                if (grid.Rows[0].Cells["تاريخ الانتهاء"].Value != DBNull.Value) throw new Exception("Stock result did not preserve SQL NULL.");
                if (lotMode && (decimal)grid.Rows[0].Cells["الكمية القابلة للصرف"].Value != 1.125m) throw new Exception("Lot issueable quantity lost decimal precision.");
                if (!lotMode && (decimal)grid.Rows[0].Cells["قيمة التكلفة"].Value != 6.25m) throw new Exception("Basic inventory money calculation changed.");

                var failed = form.RefreshStockReportAsync();
                reader.Pending[3].SetException(new InvalidOperationException("Simulated stock read failure")); Finish(failed);
                if (grid.Rows.Count != 0 || print.Enabled || export.Enabled || !Require<Label>(form, "inventoryDataStatus").Text.Contains("Simulated stock read failure"))
                    throw new Exception("Stock read failure retained output or obscured retry status.");
                var retry = form.RefreshStockReportAsync(); reader.Pending[4].SetResult(Fixture(lotMode)); Finish(retry);
                if (grid.Rows.Count != 1 || !print.Enabled) throw new Exception("Stock report retry did not restore a valid result.");
                var changed = form.RefreshStockReportAsync(); contextMatches = false;
                reader.Pending[5].SetResult(Fixture(lotMode)); Finish(changed);
                if (grid.Rows.Count != 0 || print.Enabled || export.Enabled) throw new Exception("Changed stock context accepted results.");
                contextMatches = true;
                var empty = form.RefreshStockReportAsync(); reader.Pending[6].SetResult(EmptyFixture(lotMode)); Finish(empty);
                if (grid.Rows.Count != 0 || print.Enabled || export.Enabled) throw new Exception("Empty stock result enabled output.");
                var closing = form.RefreshStockReportAsync(); var closingToken = reader.Tokens[7]; form.Dispose();
                if (!closingToken.IsCancellationRequested) throw new Exception("Closing stock report did not cancel read.");
                reader.Pending[7].SetResult(Fixture(lotMode)); Finish(closing);
            }
        }

        private static void VerifyProjection()
        {
            foreach (bool lotMode in new[] { false, true })
            {
                DataTable table = InventoryStockPresentation.ToTable(Fixture(lotMode));
                if (table.Columns.Count != (lotMode ? 17 : 12) || table.Rows.Count != 1 || table.Rows[0]["تاريخ الانتهاء"] != DBNull.Value)
                    throw new Exception("Typed report projection changed columns, row count, or NULL values.");
                if (lotMode && (Guid)table.Rows[0]["معرف التشغيلة"] == Guid.Empty) throw new Exception("Lot identity was not retained.");
            }
        }

        private static OperationalReportResult Fixture(bool lotMode, string medicineName = "باراسيتامول 500 mg")
        {
            string[] names = lotMode
                ? new[] { "الصنف", "الباركود", "الشركة", "رقم التشغيلة", "GTIN", "المورد", "الكمية المستلمة", "الكمية الحالية", "الكمية القابلة للصرف", "تكلفة الوحدة", "سعر البيع", "قيمة التكلفة", "قيمة البيع", "تاريخ الإنتاج", "تاريخ الانتهاء", "حالة التشغيلة", "معرف التشغيلة" }
                : new[] { "الصنف", "الباركود", "الشركة", "الوحدة", "الكمية الحالية", "سعر التكلفة", "سعر البيع", "قيمة التكلفة", "قيمة البيع", "تاريخ الإنتاج", "تاريخ الانتهاء", "الحالة" };
            Type[] types = lotMode
                ? new[] { typeof(string), typeof(string), typeof(string), typeof(string), typeof(string), typeof(string), typeof(decimal), typeof(decimal), typeof(decimal), typeof(decimal), typeof(decimal), typeof(decimal), typeof(decimal), typeof(DateTime), typeof(DateTime), typeof(string), typeof(Guid) }
                : new[] { typeof(string), typeof(string), typeof(string), typeof(string), typeof(int), typeof(decimal), typeof(decimal), typeof(decimal), typeof(decimal), typeof(DateTime), typeof(DateTime), typeof(string) };
            object[] values = lotMode
                ? new object[] { medicineName, "123456", "الشركة", "LOT-1", null, null, 2.250m, 2.125m, 1.125m, 1.25m, 2.50m, 2.66m, 5.31m, null, null, "متاحة", Guid.NewGuid() }
                : new object[] { medicineName, "123456", "الشركة", "الرصيد الرئيسي", 5, 1.25m, 2.50m, 6.25m, 12.50m, null, null, "مخزون منخفض" };
            var columns = names.Select((name, index) => new OperationalReportColumn(name, types[index]));
            return new OperationalReportResult(columns, new[] { values });
        }

        private static OperationalReportResult EmptyFixture(bool lotMode)
        {
            var populated = Fixture(lotMode);
            return new OperationalReportResult(populated.Columns, new object[0][]);
        }

        private static void Finish(Task task)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(10);
            while (!task.IsCompleted)
            {
                if (DateTime.UtcNow > deadline) throw new Exception("Stock read continuation did not complete.");
                Application.DoEvents(); Thread.Sleep(1);
            }
            task.GetAwaiter().GetResult();
        }

        private static T Require<T>(Control root, string name) where T : Control
        {
            Control control = root.Controls.Find(name, true).FirstOrDefault();
            if (control == null) throw new Exception("Missing stock report control: " + name);
            return (T)control;
        }

        private sealed class Probe : IInventoryStockReadService
        {
            internal readonly List<InventoryStockReadRequest> Requests = new List<InventoryStockReadRequest>();
            internal readonly List<CancellationToken> Tokens = new List<CancellationToken>();
            internal readonly List<TaskCompletionSource<OperationalReportResult>> Pending = new List<TaskCompletionSource<OperationalReportResult>>();
            internal readonly List<Task> Tasks = new List<Task>();
            public Task<OperationalReportResult> LoadAsync(InventoryStockReadRequest request, OperationActor actor, CancellationToken cancellation)
            {
                if (actor.UserName != "stock-reader" || actor.UserRole != "Administrator") throw new Exception("Captured stock actor changed.");
                Requests.Add(request); Tokens.Add(cancellation);
                var result = new TaskCompletionSource<OperationalReportResult>(); Pending.Add(result); Tasks.Add(result.Task); return result.Task;
            }
        }
    }
}
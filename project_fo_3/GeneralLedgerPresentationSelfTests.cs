using System;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using PharmacyManagement.Application;

namespace project_fo_3
{
    internal static class GeneralLedgerPresentationSelfTests
    {
        internal static void Run()
        {
            VerifyCsvDelivery();
            string originalTheme = Properties.Setings.Default.ThemeMode;
            int layouts = 0;
            try
            {
                foreach (string theme in new[] { "Light", "Dark" })
                foreach (bool english in new[] { false, true })
                {
                    Properties.Setings.Default.ThemeMode = theme;
                    using (var form = new Preview())
                    {
                        ShowOwned(form, english);
                        using (var csvTable = new DataTable())
                        {
                            csvTable.Columns.Add("البيان");
                            csvTable.Rows.Add("باراسيتامول 500 mg, \"A\"");
                            csvTable.Rows.Add("=SUM(A1:A2)");
                            string csv = form.BuildLedgerCsvContent(csvTable);
                            string expected = (english ? "\"Description\"" : "\"البيان\"") + Environment.NewLine +
                                "\"باراسيتامول 500 mg, \"\"A\"\"\"" + Environment.NewLine + "\"'=SUM(A1:A2)\"" + Environment.NewLine;
                            Check(csv == expected, "Ledger CSV language, literal business text, quoting or formula protection changed.");
                            Check(csvTable.Columns[0].ColumnName == "البيان" && (string)csvTable.Rows[1][0] == "=SUM(A1:A2)", "Ledger CSV mutated source data.");
                        }
                        form.MaximumSize = Size.Empty;
                        foreach (Size size in new[] { new Size(760, 600), new Size(960, 640), new Size(1366, 768), new Size(1920, 1080) })
                        {
                            OwnedTestWindowViewport.Resize(form, size); form.PerformLayout(); Application.DoEvents();
                            Check(form.ClientSize == size, "Ledger software layout did not use the requested size");
                            var title = Find<Label>(form, "generalLedgerTitle");
                            var summary = Find<FlowLayoutPanel>(form, "generalLedgerSummary");
                            var actions = Find<FlowLayoutPanel>(form, "generalLedgerActions");
                            var host = Find<Panel>(form, "generalLedgerActionsHost");
                            var status = Find<Label>(form, "generalLedgerStatus");
                            var grid = Find<DataGridView>(form, "generalLedgerGrid");
                            Check(title.Text == (english ? "Account activity" : "حركة حساب خلال فترة") &&
                                title.TextAlign == (english ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight), "Ledger title language/leading edge");
                            foreach (var label in Descendants(form).OfType<Label>().Where(label => label.Tag is string && (string)label.Tag != "UiSurface"))
                            {
                                Check(!english || !Regex.IsMatch(label.Text, "[\\u0600-\\u06ff]"), "Untranslated static ledger label: " + label.Text);
                                Check(TextRenderer.MeasureText(label.Text, label.Font).Width <= label.ClientSize.Width,
                                    "Ledger caption is clipped at " + size + ": " + label.Text);
                            }
                            Check(summary.Controls.Count == 3 && summary.RightToLeft == RightToLeft.No &&
                                summary.FlowDirection == (english ? FlowDirection.LeftToRight : FlowDirection.RightToLeft), "Ledger summary has a double directional mirror");
                            Check(actions.RightToLeft == RightToLeft.No && actions.Controls.Count == 4, "Ledger action flow double mirror/count");
                            var view = actions.Controls.OfType<AntdUI.Button>().Single(action => Convert.ToString(action.Tag) == "عرض النتيجة");
                            var export = actions.Controls.OfType<AntdUI.Button>().Single(action => Convert.ToString(action.Tag) == "تصدير Excel");
                            var close = actions.Controls.OfType<AntdUI.Button>().Single(action => Convert.ToString(action.Tag) == "خروج");
                            Check(english ? view.Left < close.Left : view.Left > close.Left, "Ledger primary action is not at the reading edge");
                            Check(export.Text == (english ? "Export Excel" : "تصدير Excel") && export.AccessibleName == export.Text,
                                "Ledger export action does not identify its Excel format in the active language.");
                            foreach (Control action in actions.Controls)
                            {
                                Check(actions.ClientRectangle.Contains(action.Bounds), "Ledger action leaves the footer at " + size);
                                Check(action.Width >= TextRenderer.MeasureText(action.Text, action.Font).Width + 48, "Ledger action caption/icon is clipped");
                            }
                            Check(!status.Bounds.IntersectsWith(actions.Bounds) && host.ClientRectangle.Contains(status.Bounds) &&
                                host.Bottom == form.ClientSize.Height && grid.Bottom <= host.Top && grid.Height >= 100,
                                "Ledger status/actions/data overlap at " + size);
                            Check(status.Text == (english ? "Loading account statement options..." : "جار تحميل خيارات كشف الحساب...") &&
                                status.AccessibleName == status.Text, "Ledger initial status language/accessibility");
                            Check(Find<Label>(grid, "__GridEmptyState").Text == (english ? "No transactions for this account" : "لا توجد حركات لهذا الحساب"), "Ledger empty label language");
                            Check(Find<AntdUI.Select>(form, "generalLedgerSortBy").Items.Cast<object>().Select(Convert.ToString).SequenceEqual(
                                english ? new[] { "By date", "By date and document number" } : new[] { "حسب التاريخ", "حسب التاريخ ثم رقم المستند" }), "Ledger sorting choices were not localized");
                            Check(Find<AntdUI.Select>(form, "generalLedgerDocumentType").Items.Cast<object>().Select(Convert.ToString).SequenceEqual(
                                english ? new[] { "All", "Manual entry", "Voucher", "Sales invoice", "Purchase invoice", "Opening entry" } :
                                new[] { "الكل", "قيد يدوي", "سند", "فاتورة بيع", "فاتورة شراء", "قيد افتتاحي" }), "Ledger document choices were not localized");
                            foreach (string selectorName in new[] { "generalLedgerSortBy", "generalLedgerDocumentType" })
                            {
                                var selector = Find<AntdUI.Select>(form, selectorName);
                                Check(selector.Text == Convert.ToString(selector.Items[selector.SelectedIndex]), "Ledger selected caption did not follow its localized choice");
                            }
                            layouts++;
                        }
                        form.Close();
                    }
                }
                Console.WriteLine("PASS ledger presentation: " + layouts + " actual-size layouts, all tagged static labels, choices/empty/status, measured captions/actions, one RTL/LTR flow and nonoverlapping adaptive footer; no SQL/native input/DPI acceptance.");
            }
            finally { Properties.Setings.Default.ThemeMode = originalTheme; }
        }

        internal static void VerifyCsvDelivery()
        {
            string token = Guid.NewGuid().ToString("N");
            string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "PharmacyLedgerCsv-" + token);
            System.IO.Directory.CreateDirectory(directory);
            string ownerPath = System.IO.Path.Combine(directory, "owner");
            System.IO.File.WriteAllText(ownerPath, token, System.Text.Encoding.UTF8);
            string path = System.IO.Path.Combine(directory, "statement.csv");
            try
            {
                using (var form = new Preview())
                using (var table = new DataTable())
                using (var dialog = new SaveFileDialog())
                {
                    var exportButton = Find<AntdUI.Button>(form, "generalLedgerExport");
                    foreach (bool english in new[] { false, true })
                    {
                        form.ApplyPresentation(english);
                        Check(exportButton.Text == (english ? "Export Excel" : "تصدير Excel") && exportButton.AccessibleName == exportButton.Text,
                            "Ledger export action does not accurately announce Excel in both languages.");
                    }
                    GeneralLedgerForm.ConfigureLedgerExcelDialog(dialog);
                    Check(dialog.DefaultExt == "xlsx" && dialog.AddExtension && dialog.FileName == "GeneralLedger.xlsx" &&
                        dialog.Filter.Contains("*.xlsx") && !dialog.Filter.Contains("*.csv"),
                        "Ledger save dialog does not offer Excel with the correct extension and default name.");

                    table.Columns.Add("التاريخ", typeof(DateTime));
                    table.Columns.Add("البيان", typeof(string));
                    table.Columns.Add("مدين", typeof(decimal));
                    table.Columns.Add("نوع المستند", typeof(string));
                    table.Rows.Add(new DateTime(2026, 10, 17), "عنصر، اختبار \"أ\"", -12.50m, "PurchaseInvoice");
                    table.Rows.Add(new DateTime(2026, 10, 18), "=SUM(A1:A2)", 0m, DBNull.Value);
                    table.Columns["نوع المستند"].ReadOnly = true;
                    string excelPath = System.IO.Path.Combine(directory, "statement.xlsx");
                    foreach (bool english in new[] { false, true })
                    {
                        form.ApplyPresentation(english);
                        using (var snapshot = form.CreateLedgerOutputSnapshot(table))
                        {
                            Check((string)snapshot.Rows[0]["نوع المستند"] == (english ? "Purchase invoice" : "فاتورة شراء") &&
                                snapshot.Rows[1].IsNull("نوع المستند") && snapshot.Columns["نوع المستند"].ReadOnly,
                                "Read-only ledger output lost its localized source type, null value or column contract.");
                        }
                        Check(table.Columns["نوع المستند"].ReadOnly && (string)table.Rows[0]["نوع المستند"] == "PurchaseInvoice" &&
                            table.Rows[0].RowState == DataRowState.Added, "Ledger localization mutated the source table.");
                        form.WriteLedgerExcelFile(table, excelPath);
                        using (var excelStream = System.IO.File.OpenRead(excelPath))
                        using (var zip = new System.IO.Compression.ZipArchive(excelStream, System.IO.Compression.ZipArchiveMode.Read))
                        using (var reader = new System.IO.StreamReader(zip.GetEntry("xl/worksheets/sheet1.xml").Open()))
                        {
                            var xml = new System.Xml.XmlDocument(); xml.LoadXml(reader.ReadToEnd());
                            var ns = new System.Xml.XmlNamespaceManager(xml.NameTable);
                            ns.AddNamespace("s", "http://schemas.openxmlformats.org/spreadsheetml/2006/main");
                            Check(xml.SelectSingleNode("//s:sheetView", ns).Attributes["rightToLeft"].Value == (english ? "0" : "1"), "Excel direction differs from ledger.");
                            Check(xml.SelectSingleNode("//s:c[@r='A1']/s:is/s:t", ns).InnerText == (english ? "Date" : "التاريخ"), "Excel header language differs from ledger.");
                            Check(xml.SelectSingleNode("//s:c[@r='A2']/s:is/s:t", ns).InnerText == "2026-10-17", "Excel Gregorian date changed.");
                            Check(xml.SelectSingleNode("//s:c[@r='C2']/s:v", ns).InnerText == "-12.50", "Excel decimal amount changed.");
                            Check(xml.SelectSingleNode("//s:c[@r='D2']/s:is/s:t", ns).InnerText == (english ? "Purchase invoice" : "فاتورة شراء"),
                                "Read-only source type was not localized in Excel.");
                            Check(xml.SelectNodes("//s:f", ns).Count == 0 && xml.SelectSingleNode("//s:c[@r='B3']/s:is/s:t", ns).InnerText == "=SUM(A1:A2)", "Excel business text became a formula.");
                        }
                    }
                    byte[] excelOriginal = System.IO.File.ReadAllBytes(excelPath);
                    bool excelFailed = false;
                    using (new System.IO.FileStream(excelPath, System.IO.FileMode.Open, System.IO.FileAccess.ReadWrite, System.IO.FileShare.None))
                        try { form.WriteLedgerExcelFile(table, excelPath); }
                        catch (System.IO.IOException) { excelFailed = true; }
                    Check(excelFailed && System.IO.File.ReadAllBytes(excelPath).SequenceEqual(excelOriginal) && !form.IsDisposed && table.Rows.Count == 2 && (string)table.Rows[1][1] == "=SUM(A1:A2)", "Excel lock failure changed file/report/source.");
                    Check(!System.IO.Directory.GetFiles(directory, "*.tmp-*").Any(), "Excel lock failure left a temporary file.");
                    form.WriteLedgerExcelFile(table, excelPath);
                    Console.WriteLine("PASS ledger Excel delivery: XLSX-only localized action/dialog; RTL/LTR OOXML, Gregorian dates, typed decimal, inert formula text, locked-file preservation and retry.");
                    string content = form.BuildLedgerCsvContent(table);
                    Exception writeError;
                    Check(ReportCsvWriter.TryWriteText(path, content, out writeError) && writeError == null,
                        "Ledger CSV could not be written to an owned local destination.");
                    byte[] bytes = System.IO.File.ReadAllBytes(path);
                    Check(bytes.Length > 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF &&
                        System.IO.File.ReadAllText(path, System.Text.Encoding.UTF8) == content &&
                        content.Contains("\"2026-10-17\"") && content.Contains("\"-12.50\"") &&
                        content.Contains("\"'=SUM(A1:A2)\""),
                        "Ledger CSV bytes lost the UTF-8 BOM, ISO date, numeric amount, or formula-injection protection.");

                    byte[] original = System.IO.File.ReadAllBytes(path);
                    bool failed;
                    using (new System.IO.FileStream(path, System.IO.FileMode.Open, System.IO.FileAccess.ReadWrite, System.IO.FileShare.None))
                        failed = !ReportCsvWriter.TryWriteText(path, content + "retry", out writeError) && writeError != null;
                    Check(failed && System.IO.File.ReadAllBytes(path).SequenceEqual(original) && !form.IsDisposed &&
                        table.Columns.Count == 4 && table.Rows.Count == 2,
                        "Failed CSV replacement changed the prior file, disposed the report, or mutated its source data.");
                    Check(!System.IO.Directory.GetFiles(directory, "*.tmp").Any(),
                        "Failed CSV output left an unowned temporary file behind.");
                }
                Console.WriteLine("PASS legacy CSV serializer compatibility: UTF-8 BOM, ISO dates, numeric values, formula protection, atomic replace failure preserves prior file/report; ledger UI exports XLSX.");
            }
            finally
            {
                if (System.IO.File.Exists(ownerPath) && System.IO.File.ReadAllText(ownerPath, System.Text.Encoding.UTF8).TrimStart('\uFEFF') == token)
                    System.IO.Directory.Delete(directory, true);
            }
        }

        internal static void SavePreviews(string directory)
        {
            string originalTheme = Properties.Setings.Default.ThemeMode;
            try
            {
                foreach (string theme in new[] { "Light", "Dark" })
                foreach (bool english in new[] { false, true })
                foreach (Size size in new[] { new Size(760, 600), new Size(1366, 768) })
                {
                    Properties.Setings.Default.ThemeMode = theme;
                    using (var form = new Preview())
                    using (DataTable table = GeneralLedgerPresentation.ToTable(new GeneralLedgerReadResult(new[] {
                        new GeneralLedgerReadRow(new DateTime(2026, 10, 3), 1, "Manual", "0001", "الفرع الرئيسي", "باراسيتامول 500 mg", 10.125m, null, 60.125m),
                        new GeneralLedgerReadRow(new DateTime(2026, 10, 4), 2, "PurchaseInvoice", "0002", "الفرع الرئيسي", "صيدلية المهندس", null, 1.25m, 58.875m)
                    }, 50m), false))
                    {
                        ShowOwned(form, english);
                        Find<DataGridView>(form, "generalLedgerGrid").DataSource = table;
                        typeof(GeneralLedgerForm).GetMethod("RenderSummary", BindingFlags.Instance | BindingFlags.NonPublic)
                            .Invoke(form, new object[] { 50m, 10.125m, 1.25m, 58.875m });
                        form.ApplyPresentation(english);
                        OwnedTestWindowViewport.Resize(form, size); form.PerformLayout(); Application.DoEvents();
                        using (var bitmap = new Bitmap(form.Width, form.Height))
                        {
                            form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                            bitmap.Save(System.IO.Path.Combine(directory, "ledger-presentation-" + theme + "-" + (english ? "LTR" : "RTL") + "-" + size.Width + ".png"));
                        }
                        form.Close();
                    }
                }
                Console.WriteLine("PASS eight ledger software previews with dummy mixed Arabic and raw-code data; outputs disabled, no SQL/native input.");
            }
            finally { Properties.Setings.Default.ThemeMode = originalTheme; }
        }

        private static void ShowOwned(GeneralLedgerForm form, bool english)
        {
            form.ShowInTaskbar = false; form.StartPosition = FormStartPosition.Manual;
            form.Location = new Point(-20000, -20000);
            form.ApplyPresentation(english); form.Show(); Application.DoEvents();
        }
        private static System.Collections.Generic.IEnumerable<Control> Descendants(Control parent)
        {
            foreach (Control child in parent.Controls) { yield return child; foreach (var nested in Descendants(child)) yield return nested; }
        }
        private static T Find<T>(Control parent, string name) where T : Control
        {
            return (T)parent.Controls.Find(name, true).Single();
        }
        private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private sealed class Preview : GeneralLedgerForm
        {
            public Preview()
            {
                var account = Find<ComboBox>(this, "generalLedgerAccount");
                account.Items.Add("1100 - الصندوق Cash"); account.SelectedIndex = 0; account.Enabled = true;
                var centers = Find<ComboBox>(this, "generalLedgerCostCenter");
                centers.Items.Add("الكل"); centers.Items.Add("بدون مركز تكلفة"); centers.Items.Add("10 - الفرع الرئيسي");
                centers.SelectedIndex = 0; centers.Enabled = true;
            }
            protected override void OnLoad(EventArgs e) { /* presentation only: no SQL or Form.Load */ }
        }
    }
}

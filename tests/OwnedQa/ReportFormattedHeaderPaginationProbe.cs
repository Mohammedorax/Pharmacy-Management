using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Printing;
using System.Linq;
using System.Reflection;
using project_fo_3;
static class ReportRowPaginationProbe
{
    static int checks;
    static void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
    static readonly BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;
    static int Main()
    {
        try
        {
            AppFonts.Initialize();
            foreach (bool rtl in new[] { true, false })
            foreach (ReportDensity density in new[] { ReportDensity.Standard, ReportDensity.Compact })
            foreach (int width in new[] { 360, 720 })
            foreach (bool grouped in new[] { false, true })
            {
                var table = new DataTable();
                table.Columns.Add("Day", typeof(string)); table.Columns.Add("Currency", typeof(string)); table.Columns.Add("Id", typeof(int)); table.Columns.Add("Name", typeof(string)); table.Columns.Add("Quantity", typeof(decimal)); table.Columns.Add("Price", typeof(decimal)); table.Columns.Add("Total", typeof(decimal));
                if (grouped) { table.Columns["Day"].ExtendedProperties["RepeatOnPrint"] = true; table.Columns["Currency"].ExtendedProperties["RepeatOnPrint"] = true; }
                for (int i = 0; i < 60; i++) table.Rows.Add("2026-10-13", i % 2 == 0 ? "SAR" : "USD", i + 1, i % 3 == 0 ? String.Join(" ", Enumerable.Repeat(rtl ? "اسم صنف عربي طويل كامل Paracetamol 500 mg" : "A long medicine name Paracetamol 500 mg", 3)) : "Short " + i, 1.25m, 24m, 30m);
                using (var layout = new DataTablePrintPreview.TablePrintLayout("Report", "", table, rtl, new ReportPrintOptions { Density = density }))
                using (var image = new Bitmap(1200, 500))
                using (Graphics graphics = Graphics.FromImage(image))
                using (Font font = AppFonts.Create(density == ReportDensity.Compact ? 7F : 8F, FontStyle.Regular))
                using (var format = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center, FormatFlags = rtl ? StringFormatFlags.DirectionRightToLeft : 0, Trimming = StringTrimming.None })
                {
                    var layoutTable = (DataTable)layout.GetType().GetField("table", Hidden).GetValue(layout);
                    var groups = (List<List<DataColumn>>)layout.GetType().GetMethod("BuildColumnGroups", Hidden).Invoke(layout, new object[] { width });
                    var expectedCounts = new List<int>();
                    foreach (var columns in groups)
                    {
                        if (grouped) Check(columns.Any(c => c.ColumnName == "Day") && columns.Any(c => c.ColumnName == "Currency"), "Day/currency repeat in each group");
                        var widths = (int[])layout.GetType().GetMethod("GetColumnWidths", Hidden).Invoke(layout, new object[] { columns, width });
                        int used = 0, count = 0;
                        int capacity = 260 - 32 - 24 - (density == ReportDensity.Compact ? 22 : 26) - 28;
                        foreach (DataRow row in layoutTable.Rows)
                        {
                            int height = density == ReportDensity.Compact ? 20 : 24;
                            for (int c = 0; c < columns.Count; c++) height = Math.Max(height, (int)Math.Ceiling(graphics.MeasureString(Convert.ToString(row[columns[c]]).Trim(), font, new SizeF(Math.Max(1, widths[c] - 4), 100000), format).Height) + 4);
                            Check(height <= capacity, "Fixture row fits a fresh page");
                            if (used + height > capacity) { expectedCounts.Add(count); count = 0; used = 0; }
                            used += height; count++;
                        }
                        if (count > 0) expectedCounts.Add(count);
                    }
                    int pageIndex = 0; bool more;
                    do
                    {
                        graphics.Clear(Color.White);
                        int before = layout.RenderedRowCount;
                        var page = new PrintPageEventArgs(graphics, new Rectangle(20, 20, width, 260), new Rectangle(0, 0, 1200, 500), new PageSettings());
                        layout.PrintPage(null, page);
                        Check(pageIndex < expectedCounts.Count && layout.RenderedRowCount - before == expectedCounts[pageIndex], "Independent page boundary and complete row count; page=" + pageIndex);
                        more = page.HasMorePages; pageIndex++;
                        Check(pageIndex <= expectedCounts.Count, "Pagination terminates without duplicate empty pages");
                    } while (more);
                    Check(pageIndex == expectedCounts.Count && layout.RenderedRowCount == 60 * groups.Count, "All 60 rows exactly once per horizontal group");
                    Check(table.Rows.Count == 60 && (decimal)table.Rows[59]["Total"] == 30m, "Original rows and money preserved");
                    layout.Reset(); Check(layout.RenderedRowCount == 0, "Reset clears print progress");
                    Console.WriteLine("PASS pagination " + rtl + "/" + density + "/" + width + "/repeat=" + grouped + "; groups=" + groups.Count + "; pages=" + pageIndex);
                }
            }
            var huge = new DataTable(); huge.Columns.Add("Name", typeof(string)); huge.Rows.Add(new string('W', 20000));
            foreach (bool rtl in new[] { true, false })
            foreach (ReportDensity density in new[] { ReportDensity.Standard, ReportDensity.Compact })
            using (var layout = new DataTablePrintPreview.TablePrintLayout("Report", "", huge, rtl, new ReportPrintOptions { Density = density }))
            using (var image = new Bitmap(1200, 500))
            using (Graphics graphics = Graphics.FromImage(image))
            {
                bool refused = false;
                try { layout.PrintPage(null, new PrintPageEventArgs(graphics, new Rectangle(20, 20, 360, 260), new Rectangle(0, 0, 1200, 500), new PageSettings())); }
                catch (InvalidOperationException error) { refused = error.Message.Contains(rtl ? "صف كامل" : "complete row"); }
                Check(refused && layout.RenderedRowCount == 0, "Oversized row rejected without clipping, committing progress or endless pagination");
            }
            foreach (bool rtl in new[] { true, false })
            foreach (ReportDensity density in new[] { ReportDensity.Standard, ReportDensity.Compact })
            foreach (int width in new[] { 260, 760 })
            {
                var table = new DataTable();
                table.Columns.Add("Name", typeof(string)).Caption = String.Join(" ", Enumerable.Repeat(rtl ? "اسم الصنف كاملًا مع وصف الوحدة والعبوة" : "Complete product heading with unit and package description", 3));
                for (int i = 0; i < 60; i++) table.Rows.Add(i + ": " + (i % 3 == 0 ? String.Join(" ", Enumerable.Repeat(rtl ? "اسم دواء طويل Paracetamol 500 mg كامل" : "Complete long medicine Paracetamol 500 mg", 3)) : "Short medicine"));
                using (var layout = new DataTablePrintPreview.TablePrintLayout("Report", "", table, rtl, new ReportPrintOptions { Density = density }))
                using (var image = new Bitmap(900, 500))
                using (Graphics graphics = Graphics.FromImage(image))
                using (Font headerFont = AppFonts.Create(density == ReportDensity.Compact ? 7F : 8F, FontStyle.Bold))
                using (Font rowFont = AppFonts.Create(density == ReportDensity.Compact ? 7F : 8F, FontStyle.Regular))
                using (var format = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center, FormatFlags = rtl ? StringFormatFlags.DirectionRightToLeft : 0, Trimming = StringTrimming.None })
                {
                    int headerHeight = Math.Max(density == ReportDensity.Compact ? 22 : 26, (int)Math.Ceiling(graphics.MeasureString(table.Columns[0].Caption, headerFont, new SizeF(width - 4, 100000), format).Height) + 4);
                    int capacity = 260 - 32 - 24 - headerHeight - 28, used = 0, count = 0;
                    var expected = new List<int>();
                    foreach (DataRow row in table.Rows)
                    {
                        int height = Math.Max(density == ReportDensity.Compact ? 20 : 24, (int)Math.Ceiling(graphics.MeasureString((string)row[0], rowFont, new SizeF(width - 4, 100000), format).Height) + 4);
                        Check(height <= capacity, "Long-heading fixture row fits a fresh page");
                        if (used + height > capacity) { expected.Add(count); used = 0; count = 0; }
                        used += height; count++;
                    }
                    if (count > 0) expected.Add(count);
                    int pages = 0; bool more;
                    do
                    {
                        graphics.Clear(Color.White); int before = layout.RenderedRowCount;
                        var page = new PrintPageEventArgs(graphics, new Rectangle(20, 20, width, 260), new Rectangle(0, 0, 900, 500), new PageSettings());
                        layout.PrintPage(null, page);
                        Check(pages < expected.Count && layout.RenderedRowCount - before == expected[pages], "Complete long header and long rows share independent page capacity");
                        more = page.HasMorePages; pages++; Check(pages <= expected.Count, "Long-heading pagination terminates");
                    } while (more);
                    Check(pages == expected.Count && layout.RenderedRowCount == 60, "Long-heading all60 rows once");
                }
            }
            foreach (bool rtl in new[] { true, false })
            foreach (ReportDensity density in new[] { ReportDensity.Standard, ReportDensity.Compact })
            {
                var table = new DataTable(); table.Columns.Add("Name", typeof(string)).Caption = new string('W', 10000); table.Rows.Add("Short row");
                using (var layout = new DataTablePrintPreview.TablePrintLayout("Report", "", table, rtl, new ReportPrintOptions { Density = density }))
                using (var image = new Bitmap(900, 500))
                using (Graphics graphics = Graphics.FromImage(image))
                {
                    bool refused = false;
                    try { layout.PrintPage(null, new PrintPageEventArgs(graphics, new Rectangle(20, 20, 260, 260), new Rectangle(0, 0, 900, 500), new PageSettings())); }
                    catch (InvalidOperationException error) { refused = error.Message.Contains(rtl ? "ترويسة التقرير" : "report header"); }
                    Check(refused && layout.RenderedRowCount == 0, "Oversized header rejected with correct language and no row progress");
                }
            }
            Console.WriteLine("PASS pagination checks=" + checks + "; SqlUsed=False UiUsed=False PhysicalPrint=False"); return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}

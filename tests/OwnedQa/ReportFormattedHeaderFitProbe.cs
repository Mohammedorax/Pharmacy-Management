using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Reflection;
using project_fo_3;
static class ReportHeaderHeightProbe
{
    static int checks, failures;
    static readonly BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;
    static List<int> Borders(Bitmap image, Rectangle area)
    {
        var result = new List<int>();
        for (int y = area.Top; y < Math.Min(area.Bottom, image.Height); y++)
        {
            int gray = 0;
            for (int x = area.Left + 2; x < area.Right - 2; x++) { Color c = image.GetPixel(x, y); if (c.R == 185 && c.G == 185 && c.B == 185) gray++; }
            if (gray > (area.Width - 4) * .25) result.Add(y);
        }
        return result;
    }
    static void Check(bool value, string message) { checks++; if (!value) { failures++; Console.WriteLine("FAIL " + message); } }
    static int Main(string[] args)
    {
        try
        {
            string output = Path.GetFullPath(args[0]);
            if (!output.StartsWith("D:\\Comp\\Pharmacy-Management-master\\artifacts\\company-demo-20261008\\report-header-height-", StringComparison.OrdinalIgnoreCase) || !File.Exists(Path.Combine(output, "owner.txt"))) throw new Exception("Owned evidence directory required");
            AppFonts.Initialize();
            foreach (string kind in new[] { "financial", "stock" })
            foreach (bool rtl in new[] { true, false })
            foreach (ReportDensity density in new[] { ReportDensity.Standard, ReportDensity.Compact })
            foreach (int width in new[] { 260, 760 })
            {
                var table = new DataTable(); table.ReadXml(Path.Combine(args[1], kind + ".xml"));
                foreach (DataColumn column in table.Columns) if (!rtl) column.Caption = UiLanguageCatalog.Translate(column.ColumnName);
                table.Columns["اليوم"].ExtendedProperties["RepeatOnPrint"] = true;
                if (table.Columns.Contains("العملة")) table.Columns["العملة"].ExtendedProperties["RepeatOnPrint"] = true;
                using (var layout = new DataTablePrintPreview.TablePrintLayout(rtl ? "فحص ترويسات التقرير الفعلي" : "Actual report headings", "", table, rtl, new ReportPrintOptions { Density = density }))
                using (var bitmap = new Bitmap(900, 1400))
                using (Graphics graphics = Graphics.FromImage(bitmap))
                using (Font font = AppFonts.Create(density == ReportDensity.Compact ? 7F : 8F, FontStyle.Bold))
                using (var format = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center, FormatFlags = rtl ? StringFormatFlags.DirectionRightToLeft : 0, Trimming = StringTrimming.None })
                {
                    int pages = 0; bool more; bool saved = false;
                    do
                    {
                        graphics.Clear(Color.White);
                        int groupIndex = (int)layout.GetType().GetField("currentColumnGroup", Hidden).GetValue(layout);
                        var area = new Rectangle(20, 20, width, 1250);
                        var page = new PrintPageEventArgs(graphics, area, new Rectangle(0, 0, 900, 1400), new PageSettings());
                        layout.PrintPage(null, page);
                        var groups = (List<List<DataColumn>>)layout.GetType().GetField("columnGroups", Hidden).GetValue(layout);
                        var columns = groups[groupIndex];
                        var widths = (int[])layout.GetType().GetMethod("GetColumnWidths", Hidden).Invoke(layout, new object[] { columns, width });
                        int required = density == ReportDensity.Compact ? 22 : 26;
                        for (int i = 0; i < columns.Count; i++) required = Math.Max(required, (int)Math.Ceiling(graphics.MeasureString(columns[i].Caption, font, new SizeF(Math.Max(1, widths[i] - 4), 100000), format).Height) + 4);
                        var borders = Borders(bitmap, area);
                        string key = kind + "-" + (rtl ? "ar" : "en") + "-" + density + "-" + width;
                        Check(borders.Count >= 2, key + " header border measurement");
                        int actual = borders.Count >= 2 ? borders[1] - borders[0] : 0;
                        Check(actual >= required, key + " group=" + groupIndex + "; actual=" + actual + "; full=" + required + "; captions=" + String.Join(" | ", columns.Select(c => c.Caption)));
                        if (required > (density == ReportDensity.Compact ? 22 : 26) && !saved) { bitmap.Save(Path.Combine(output, key + "-full-header.png")); saved = true; }
                        pages++; more = page.HasMorePages;
                        if (pages > 80) throw new Exception("Pagination failed to terminate");
                    } while (more);
                    var finalGroups = (List<List<DataColumn>>)layout.GetType().GetField("columnGroups", Hidden).GetValue(layout);
                    Check(layout.RenderedRowCount == table.Rows.Count * finalGroups.Count, "Every original fixture row exactly once per horizontal group");
                    Check(finalGroups.SelectMany(group => group).Select(column => column.ColumnName).Distinct().Count() == table.Columns.Count, "All actual daily column identities covered");
                    Console.WriteLine("CASE " + kind + "/" + rtl + "/" + density + "/" + width + "; pages=" + pages);
                }
            }
            Console.WriteLine("RESULT header checks=" + checks + "; failures=" + failures + "; ActualDailyColumnCaptions=True SqlUsed=False UiUsed=False PhysicalPrint=False");
            return failures == 0 ? 0 : 1;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 2; }
    }
}

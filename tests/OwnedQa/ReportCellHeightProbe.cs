using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Reflection;
using project_fo_3;

static class ReportCellHeightProbe
{
    static int checks, failures;
    static void Check(bool ok, string message) { checks++; if (!ok) { failures++; Console.WriteLine("FAIL " + message); } else Console.WriteLine("PASS " + message); }
    static List<int> HorizontalBorders(Bitmap image, Rectangle area)
    {
        var result = new List<int>();
        for (int y = area.Top; y < Math.Min(area.Bottom, image.Height); y++)
        {
            int gray = 0;
            for (int x = area.Left + 2; x < area.Right - 2; x++)
            {
                Color c = image.GetPixel(x, y);
                if (c.R == 185 && c.G == 185 && c.B == 185) gray++;
            }
            // A clipped text line may overpaint the name-cell border. The two
            // other cells still identify the full-width grid row uniquely.
            if (gray > (area.Width - 4) * .25) result.Add(y);
        }
        return result;
    }
    [STAThread] static int Main(string[] args)
    {
        try
        {
            if (args.Length != 1) throw new ArgumentException("One owned evidence directory required");
            string directory = Path.GetFullPath(args[0]);
            if (!directory.StartsWith("D:\\Comp\\Pharmacy-Management-master\\artifacts\\company-demo-20261008\\report-cell-height-", StringComparison.OrdinalIgnoreCase) || !File.Exists(Path.Combine(directory, "owner.txt"))) throw new InvalidOperationException("Owned evidence directory required");
            AppFonts.Initialize();
            foreach (bool rtl in new[] { true, false })
            foreach (ReportDensity density in new[] { ReportDensity.Standard, ReportDensity.Compact })
            foreach (int width in new[] { 720, 1000 })
            foreach (bool longText in new[] { false, true })
            {
                string text = longText ? String.Join(" ", Enumerable.Repeat(rtl ? "باراسيتامول 500 ملغ — Paracetamol 500 mg عبوة باسم تجاري طويل كامل دون قص" : "Paracetamol 500 mg extended product description with a full readable name", 3)) : (rtl ? "دواء قصير" : "Short medicine");
                var table = new DataTable(); table.Columns.Add("Id", typeof(string)); table.Columns.Add("Name", typeof(string)); table.Columns.Add("Price", typeof(decimal)); table.Rows.Add("112", text, 24m);
                using (var layout = new DataTablePrintPreview.TablePrintLayout(rtl ? "فحص ارتفاع صف التقرير" : "Report row height", "", table, rtl, new ReportPrintOptions { Density = density }))
                using (var bitmap = new Bitmap(1200, 1400))
                using (Graphics graphics = Graphics.FromImage(bitmap))
                using (Font font = AppFonts.Create(density == ReportDensity.Compact ? 7F : 8F, FontStyle.Regular))
                using (var format = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center, FormatFlags = rtl ? StringFormatFlags.DirectionRightToLeft : 0, Trimming = StringTrimming.None })
                {
                    graphics.Clear(Color.White);
                    var area = new Rectangle(20, 20, width, 1250);
                    var page = new PrintPageEventArgs(graphics, area, new Rectangle(0, 0, 1200, 1400), new PageSettings());
                    layout.PrintPage(null, page);
                    var layoutTable = (DataTable)typeof(DataTablePrintPreview.TablePrintLayout).GetField("table", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(layout);
                    var columns = layoutTable.Columns.Cast<DataColumn>().ToList();
                    var widths = (int[])typeof(DataTablePrintPreview.TablePrintLayout).GetMethod("GetColumnWidths", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(layout, new object[] { columns, width });
                    // Independent full-text measurement; no ellipsis or finite-height fitting.
                    int required = (int)Math.Ceiling(graphics.MeasureString(text, font, new SizeF(Math.Max(1, widths[1] - 4), 100000), format).Height) + 4;
                    var borders = HorizontalBorders(bitmap, area);
                    string key = (rtl ? "ar" : "en") + "-" + density + "-" + width + "-" + (longText ? "long" : "short");
                    Check(borders.Count >= 3, key + " detects header and row boundaries");
                    int actual = borders.Count >= 3 ? borders[2] - borders[1] : 0;
                    Check(actual >= required, key + " full cell height: actual=" + actual + "; required=" + required);
                    Check(layout.RenderedRowCount == 1 && !page.HasMorePages, key + " exactly one row and terminating page");
                    Check((string)table.Rows[0]["Name"] == text && (decimal)table.Rows[0]["Price"] == 24m, key + " source text and money unchanged");
                    bitmap.Save(Path.Combine(directory, key + ".png"));
                }
            }
            Console.WriteLine("RESULT checks=" + checks + "; failures=" + failures + "; SqlUsed=False UiUsed=False PhysicalPrint=False");
            return failures == 0 ? 0 : 1;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 2; }
    }
}

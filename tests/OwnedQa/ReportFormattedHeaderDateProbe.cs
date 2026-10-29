using System;
using System.Globalization;
using System.Reflection;
using project_fo_3;
static class ReportDateFormatProbe
{
    static int checks, failures;
    static void Check(bool ok, string why) { checks++; if (!ok) { failures++; Console.WriteLine("FAIL " + why); } }
    static int Main()
    {
        CultureInfo culture = CultureInfo.CurrentCulture, uiCulture = CultureInfo.CurrentUICulture;
        try
        {
            MethodInfo format = typeof(DataTablePrintPreview.TablePrintLayout).GetMethod("SafeCellValue", BindingFlags.NonPublic | BindingFlags.Static);
            if (format == null) throw new Exception("Actual shared renderer formatter not found");
            object[][] dates = {
                new object[] { new DateTime(2026,10,13), "2026-10-13" },
                new object[] { new DateTime(2024,2,29), "2024-02-29" },
                new object[] { new DateTime(2026,10,13,13,5,9,DateTimeKind.Utc), "2026-10-13 13:05:09" },
                new object[] { new DateTime(2026,10,13,13,5,9,DateTimeKind.Local), "2026-10-13 13:05:09" },
                new object[] { new DateTime(2026,10,13,13,5,9,DateTimeKind.Unspecified), "2026-10-13 13:05:09" },
                new object[] { new DateTime(2026,10,13,0,0,1), "2026-10-13 00:00:01" },
                new object[] { new DateTime(2026,10,13,13,5,9).AddMilliseconds(125), "2026-10-13 13:05:09" },
                new object[] { DateTime.MinValue, "0001-01-01" },
                new object[] { DateTime.MaxValue, "9999-12-31 23:59:59" }
            };
            foreach (string name in new[] { "en-US", "ar-SA", "zh-CN", "th-TH" })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name); CultureInfo.CurrentUICulture = CultureInfo.CurrentCulture;
                foreach (object[] entry in dates)
                {
                    string actual = null, error = null;
                    try { actual = (string)format.Invoke(null, new[] { entry[0] }); }
                    catch (TargetInvocationException exception) { error = exception.InnerException.GetType().Name; }
                    Check(error == null && actual == (string)entry[1], name + ": full date/time fields agree with explicit report format; expected=" + entry[1] + "; actual=" + actual + "; error=" + error);
                    Check(ReportCsvWriter.Cell(entry[0]) == "\"" + entry[1] + "\"", name + ": existing typed CSV date contract remains unchanged");
                }
                object[][] controls = {
                    new object[] { null, "" }, new object[] { DBNull.Value, "" },
                    new object[] { "باراسيتامول 500 mg", "باراسيتامول 500 mg" },
                    new object[] { "  alpha" + Environment.NewLine + "beta  ", "alpha beta" },
                    new object[] { "13/10/2026 12:00 AM", "13/10/2026 12:00 AM" },
                    new object[] { "十月 500 mg", "十月 500 mg" },
                    new object[] { 12.50m, "12" + CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator + "50" },
                    new object[] { -7, "-7" }
                };
                foreach (object[] entry in controls) Check((string)format.Invoke(null, new[] { entry[0] }) == (string)entry[1], name + ": non-date text, null or numeric display preserved");
            }
            Console.WriteLine("RESULT date checks=" + checks + "; failures=" + failures + "; Cultures=4 DateValues=9 NonDateControls=8 SqlUsed=False UiUsed=False PhysicalPrint=False");
            return failures == 0 ? 0 : 1;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 2; }
        finally { CultureInfo.CurrentCulture = culture; CultureInfo.CurrentUICulture = uiCulture; }
    }
}

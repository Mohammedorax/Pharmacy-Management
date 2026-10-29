using System;
using System.Data;
using System.Globalization;
using System.Reflection;
using System.Windows.Forms;
using project_fo_3;
static class ReportGridDateCultureProbe
{
    static int checks, failures, reads;
    static readonly BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;
    static void Set(object target, string name, object value) { target.GetType().GetField(name, Hidden).SetValue(target, value); }
    static void Check(bool ok, string why) { checks++; if (!ok) { failures++; Console.WriteLine("FAIL " + why); } }
    [STAThread] static int Main()
    {
        CultureInfo culture = CultureInfo.CurrentCulture, uiCulture = CultureInfo.CurrentUICulture;
        try
        {
            RequireOwnedPrivateDesktop.Assert(); Console.OutputEncoding = new System.Text.UTF8Encoding(false);
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false); AppFonts.Initialize();
            foreach (string name in new[] { "en-US", "ar-SA", "zh-CN", "th-TH" })
            foreach (bool english in new[] { false, true })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name); CultureInfo.CurrentUICulture = CultureInfo.CurrentCulture;
                SessionContext.Start("owned-report-grid-date", "Manager", "GRID-DATE");
                using (var form = new OperationalReportBuilderForm(true, () => { reads++; throw new Exception("No report read session permitted"); }, () => Array.Empty<OperationalReportTemplate>()))
                using (var table = new DataTable())
                {
                    form.ApplyPresentation(english);
                    table.Columns.Add("اليوم", typeof(DateTime)); table.Columns.Add("تاريخ الحركة", typeof(DateTime)); table.Columns.Add("الإجمالي", typeof(decimal)); table.Columns.Add("العملة", typeof(string));
                    table.Rows.Add(new DateTime(2026, 10, 13), new DateTime(2026, 10, 13, 13, 5, 9), 12.50m, "SAR");
                    table.Rows.Add(new DateTime(2024, 2, 29), new DateTime(2024, 2, 29), -7.25m, "USD");
                    table.Rows.Add(DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value);
                    Set(form, "currentTable", table); Set(form, "loadedSourceKey", "DailyPerformance"); Set(form, "loadedUsesDates", true); Set(form, "loadedFromDate", new DateTime(2026, 1, 1)); Set(form, "loadedToDate", new DateTime(2026, 10, 13));
                    var grid = (DataGridView)form.Controls.Find("operationalReportGrid", true)[0];
                    IntPtr handle = form.Handle; grid.BindingContext = new BindingContext();
                    form.GetType().GetMethod("BindReportTable", Hidden).Invoke(form, new object[] { table });
                    if (grid.Rows.Count != 3 || grid.Columns["اليوم"].ValueType != typeof(DateTime)) throw new Exception("Owned hidden grid fixture did not bind typed date rows");
                    string[,] expected = { { "2026-10-13", "2026-10-13 13:05:09" }, { "2024-02-29", "2024-02-29" } };
                    string[] columns = { "اليوم", "تاريخ الحركة" };
                    string key = name + "/English=" + english;
                    for (int row = 0; row < 2; row++) for (int column = 0; column < 2; column++)
                    {
                        object raw = grid.Rows[row].Cells[columns[column]].Value;
                        string actual = Convert.ToString(grid.Rows[row].Cells[columns[column]].FormattedValue);
                        Check(actual == expected[row, column], key + ": grid date; expected=" + expected[row, column] + "; actual=" + actual);
                        Check(ReportCsvWriter.Cell(raw) == "\"" + expected[row, column] + "\"", key + ": existing CSV date agrees with original typed value");
                    }
                    Check((DateTime)grid.Rows[0].Cells["تاريخ الحركة"].Value == new DateTime(2026, 10, 13, 13, 5, 9) && (DateTime)table.Rows[1]["اليوم"] == new DateTime(2024, 2, 29), key + ": raw timestamps and leap day unchanged");
                    Check((decimal)grid.Rows[0].Cells["الإجمالي"].Value == 12.50m && table.Rows[2].IsNull("الإجمالي"), key + ": original money and NULL unchanged");
                    Check(Convert.ToString(grid.Rows[2].Cells["اليوم"].FormattedValue) == "—" && Convert.ToString(grid.Rows[2].Cells["تاريخ الحركة"].FormattedValue) == "—", key + ": original empty-date marker preserved");
                    Check(reads == 0 && !form.Visible && CultureInfo.CurrentCulture.Name == name, key + ": exact culture, no read session or visible user window");
                }
            }
            Console.WriteLine("RESULT grid date checks=" + checks + "; failures=" + failures + "; MatrixCases=8 ReadSessions=" + reads + " SqlUsed=False NativeInput=False PhysicalPrint=False");
            return failures == 0 ? 0 : 1;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 2; }
        finally { SessionContext.Clear(); CultureInfo.CurrentCulture = culture; CultureInfo.CurrentUICulture = uiCulture; }
    }
}

using System;
using System.Data;
using System.Globalization;
using System.Reflection;
using System.Windows.Forms;
using PharmacyManagement.Application;
using project_fo_3;
static class ReportPeriodCultureProbe
{
    static int checks, failures, factoryCalls;
    static readonly BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;
    static void Set(object target, string name, object value) { target.GetType().GetField(name, Hidden).SetValue(target, value); }
    static object Get(object target, string name) { return target.GetType().GetField(name, Hidden).GetValue(target); }
    static void Check(bool ok, string why) { checks++; if (!ok) { failures++; Console.WriteLine("FAIL " + why); } }
    [STAThread] static int Main()
    {
        CultureInfo culture = CultureInfo.CurrentCulture, uiCulture = CultureInfo.CurrentUICulture;
        try
        {
            RequireOwnedPrivateDesktop.Assert(); Console.OutputEncoding = new System.Text.UTF8Encoding(false); Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false); AppFonts.Initialize();
            foreach (string name in new[] { "en-US", "ar-SA", "zh-CN", "th-TH" })
            foreach (bool english in new[] { false, true })
            foreach (bool usesDates in new[] { false, true })
            foreach (bool capped in new[] { false, true })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name); CultureInfo.CurrentUICulture = CultureInfo.CurrentCulture;
                SessionContext.Start("owned-report-period", "Manager", "PERIOD-CULTURE");
                using (var form = new OperationalReportBuilderForm(true, () => { factoryCalls++; throw new Exception("No read session is permitted in period presentation fixture"); }, () => Array.Empty<OperationalReportTemplate>()))
                using (var table = new DataTable())
                {
                    form.ApplyPresentation(english);
                    table.Columns.Add("Value", typeof(int));
                    if (capped) for (int i = 0; i < OperationalReportRequest.MaximumRows; i++) table.Rows.Add(i);
                    var from = new DateTime(2026, 1, 1); var to = new DateTime(2026, 10, 13);
                    Set(form, "currentTable", table); Set(form, "loadedSourceKey", usesDates ? "DailyPerformance" : "MostMovementsToday");
                    Set(form, "loadedUsesDates", usesDates); Set(form, "loadedFromDate", from); Set(form, "loadedToDate", to);
                    DateTime today = DateTime.Today;
                    form.GetType().GetMethod("UpdateLoadedPresentation", Hidden).Invoke(form, null);
                    string actual = (string)Get(form, "loadedReportPeriod");
                    string expected = usesDates ? (english ? UiLanguageCatalog.Translate("الفترة") : "الفترة") + ": 2026/01/01 — 2026/10/13"
                        : (english ? UiLanguageCatalog.Translate("تقرير تشغيلي") : "تقرير تشغيلي") + " — " + today.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture);
                    if (capped) expected += " — " + (english ? UiLanguageCatalog.Translate("تم تطبيق حد العرض الآمن؛ المخرجات تشمل المعروض فقط.") : "تم تطبيق حد العرض الآمن؛ المخرجات تشمل المعروض فقط.");
                    string key = name + "/English=" + english + "/Dates=" + usesDates + "/Capped=" + capped;
                    Check(actual == expected, key + "; expected=" + expected + "; actual=" + actual);
                    Check((DateTime)Get(form, "loadedFromDate") == from && (DateTime)Get(form, "loadedToDate") == to, key + ": original date bounds untouched");
                    Check(factoryCalls == 0 && !form.Visible, key + ": no read session or visible user window");
                    Check(CultureInfo.CurrentCulture.Name == name && !String.IsNullOrWhiteSpace((string)Get(form, "loadedReportTitle")), key + ": exact test culture and original title retained");
                }
            }
            Console.WriteLine("RESULT period checks=" + checks + "; failures=" + failures + "; MatrixCases=32 FactoryCalls=" + factoryCalls + " SqlUsed=False NativeInput=False PhysicalPrint=False");
            return failures == 0 ? 0 : 1;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 2; }
        finally { SessionContext.Clear(); CultureInfo.CurrentCulture = culture; CultureInfo.CurrentUICulture = uiCulture; }
    }
}

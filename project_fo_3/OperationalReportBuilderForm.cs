// منشئ تقارير تشغيلي مقيد: مصادر بيانات معرفة في الشفرة فقط، واستعلامات للقراءة فقط.
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Xml.Serialization;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Application;

namespace project_fo_3
{
    internal sealed class OperationalReportSource
    {
        internal OperationalReportSource(string key, string title, bool usesDateRange, string pickerTitle = null)
        {
            Key = key;
            Title = title;
            UsesDateRange = usesDateRange;
            PickerTitle = pickerTitle ?? title;
        }

        internal string Key { get; private set; }
        internal string Title { get; private set; }
        internal bool UsesDateRange { get; private set; }
        internal string PickerTitle { get; private set; }
        public override string ToString() { return PickerTitle; }
    }

    [Serializable]
    public sealed class OperationalReportTemplate
    {
        public string TemplateId { get; set; }
        public string Name { get; set; }
        public string SourceKey { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public string SearchText { get; set; }
        public string ViewMode { get; set; }
        public List<string> VisibleColumns { get; set; }
        public List<string> ColumnOrder { get; set; }
        public bool HasPrintSettings { get; set; }
        public string PrintOrientation { get; set; }
        public string PrintDensity { get; set; }
        public bool PrintCompanyName { get; set; }
        public bool PrintLogo { get; set; }
        public bool PrintTaxNumber { get; set; }
        public bool PrintCommercialRegistration { get; set; }
        public bool PrintMobile { get; set; }
        public bool PrintAddress { get; set; }
        public bool PrintWebsite { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
    }

    internal static class OperationalReportTemplateStore
    {
        internal static IReadOnlyList<OperationalReportTemplate> Load()
        {
            return ReadAll(StoragePath, true);
        }

        private static IReadOnlyList<OperationalReportTemplate> ReadAll(string path, bool logFailures)
        {
            List<OperationalReportTemplate> templates;
            return TryReadAll(path, logFailures, out templates) ? (IReadOnlyList<OperationalReportTemplate>)templates : Array.Empty<OperationalReportTemplate>();
        }

        private static bool TryReadAll(string path, bool logFailures, out List<OperationalReportTemplate> templates)
        {
            bool recoveredFromBackup;
            return TryReadAll(path, logFailures, out templates, out recoveredFromBackup);
        }

        private static bool TryReadAll(string path, bool logFailures, out List<OperationalReportTemplate> templates,
            out bool recoveredFromBackup, bool forWriting = false)
        {
            templates = new List<OperationalReportTemplate>();
            recoveredFromBackup = false;
            if (!File.Exists(path) && !File.Exists(path + ".bak")) return true;
            foreach (string candidate in new[] { path, path + ".bak" })
            {
                try
                {
                    if (!File.Exists(candidate)) continue;
                    using (FileStream stream = File.OpenRead(candidate))
                    {
                        List<OperationalReportTemplate> deserialized =
                            new XmlSerializer(typeof(List<OperationalReportTemplate>)).Deserialize(stream) as List<OperationalReportTemplate>;
                        if (deserialized == null) throw new InvalidDataException("Invalid operational report template list.");
                        templates = deserialized.Select(Normalize).Where(item => item != null)
                            .OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
                        recoveredFromBackup = !string.Equals(candidate, path, StringComparison.OrdinalIgnoreCase);
                        return true;
                    }
                }
                catch (Exception exception)
                {
                    if (logFailures) AppDiagnostics.Log("OperationalReportTemplateLoad", exception);
                    if (forWriting && string.Equals(candidate,path,StringComparison.OrdinalIgnoreCase) &&
                        ReportTemplateFileCommit.IsUnavailableRead(exception)) return false;
                }
            }
            return false;
        }

        internal static bool Save(OperationalReportTemplate template)
        {
            return SaveAtPath(StoragePath, template);
        }

        internal static bool SaveAtPath(string path, OperationalReportTemplate template)
        {
            if (template == null) return false;
            try { ReportSearchCriteria.Normalize(template.SearchText); }
            catch (InvalidOperationException) { return false; }
            OperationalReportTemplate normalized = Normalize(template);
            if (normalized == null) return false;
            try
            {
                path=Path.GetFullPath(path);
                using(var writer=ReportTemplateFileCommit.AcquireWriter(path))
                {
                List<OperationalReportTemplate> templates;
                bool recoveredFromBackup;
                if (!TryReadAll(path, true, out templates, out recoveredFromBackup, true)) return false;
                int index = templates.FindIndex(item => string.Equals(item.TemplateId, normalized.TemplateId, StringComparison.OrdinalIgnoreCase));
                if (index < 0) index = templates.FindIndex(item => string.Equals(item.Name, normalized.Name, StringComparison.CurrentCultureIgnoreCase) && string.Equals(item.SourceKey, normalized.SourceKey, StringComparison.Ordinal));
                if (index >= 0)
                {
                    normalized.TemplateId = templates[index].TemplateId;
                    templates[index] = normalized;
                }
                else templates.Add(normalized);
                WriteAll(path, templates, recoveredFromBackup);
                return true;
                }
            }
            catch (Exception exception)
            {
                AppDiagnostics.Log("OperationalReportTemplateSave", exception);
                return false;
            }
        }

        internal static bool Delete(string templateId)
        {
            return DeleteAtPath(StoragePath, templateId);
        }

        internal static bool DeleteAtPath(string path, string templateId)
        {
            if (string.IsNullOrWhiteSpace(templateId)) return false;
            try
            {
                path=Path.GetFullPath(path);
                using(var writer=ReportTemplateFileCommit.AcquireWriter(path))
                {
                List<OperationalReportTemplate> templates;
                bool recoveredFromBackup;
                if (!TryReadAll(path, true, out templates, out recoveredFromBackup, true)) return false;
                WriteAll(path, templates.Where(item => !string.Equals(item.TemplateId, templateId, StringComparison.OrdinalIgnoreCase)).ToList(), recoveredFromBackup);
                return true;
                }
            }
            catch (Exception exception)
            {
                AppDiagnostics.Log("OperationalReportTemplateDelete", exception);
                return false;
            }
        }

        internal static OperationalReportTemplate Normalize(OperationalReportTemplate template)
        {
            if (template == null || !OperationalReportCatalog.IsAllowedSource(template.SourceKey)) return null;
            string name = (template.Name ?? string.Empty).Trim();
            if (name.Length == 0 || name.Length > 60 || template.ToDate.Date < template.FromDate.Date) return null;
            return new OperationalReportTemplate
            {
                TemplateId = Guid.TryParse(template.TemplateId, out Guid id) ? id.ToString("N") : Guid.NewGuid().ToString("N"),
                Name = name,
                SourceKey = template.SourceKey,
                FromDate = template.FromDate.Date,
                ToDate = template.ToDate.Date,
                // Preserve imported/older template text for correction; never
                // load a broader search by silently dropping its suffix.
                SearchText = (template.SearchText ?? string.Empty).Trim(),
                ViewMode = Enum.TryParse(template.ViewMode, out OperationalReportViewMode viewMode) && Enum.IsDefined(typeof(OperationalReportViewMode), viewMode) ? viewMode.ToString() : OperationalReportViewMode.Analytical.ToString(),
                VisibleColumns = NormalizeColumns(template.VisibleColumns),
                ColumnOrder = NormalizeColumns(template.ColumnOrder),
                HasPrintSettings = template.HasPrintSettings,
                PrintOrientation = Enum.TryParse(template.PrintOrientation, out ReportPageOrientation orientation) &&
                    Enum.IsDefined(typeof(ReportPageOrientation), orientation) ? orientation.ToString() : ReportPageOrientation.Automatic.ToString(),
                PrintDensity = Enum.TryParse(template.PrintDensity, out ReportDensity density) &&
                    Enum.IsDefined(typeof(ReportDensity), density) ? density.ToString() : ReportDensity.Standard.ToString(),
                PrintCompanyName = template.PrintCompanyName, PrintLogo = template.PrintLogo,
                PrintTaxNumber = template.PrintTaxNumber,
                PrintCommercialRegistration = template.PrintCommercialRegistration,
                PrintMobile = template.PrintMobile, PrintAddress = template.PrintAddress,
                PrintWebsite = template.PrintWebsite,
                UpdatedAtUtc = DateTime.UtcNow
            };
        }

        private static void WriteAll(string path, List<OperationalReportTemplate> templates, bool preserveBackup = false)
        {
            ReportTemplateFileCommit.WriteAll(path,templates,preserveBackup);
        }

        internal static bool CanReadForTest(string path, bool forWriting = false)
        {
            List<OperationalReportTemplate> templates;
            bool recovered;
            return TryReadAll(path, false, out templates, out recovered, forWriting);
        }

        internal static IReadOnlyList<OperationalReportTemplate> ReadForTest(string path)
        {
            return ReadAll(path, false);
        }

        private static List<string> NormalizeColumns(IEnumerable<string> values)
        {
            return (values ?? Enumerable.Empty<string>()).Select(item => Limit(item, 120)).Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.Ordinal).Take(40).ToList();
        }

        private static string Limit(string value, int length)
        {
            string clean = (value ?? string.Empty).Trim();
            return clean.Length <= length ? clean : clean.Substring(0, length);
        }

        private static string StoragePath
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PharmacyManagement", "operational-report-templates.xml"); }
        }
    }


    internal static class OperationalReportSelfTests
    {
        internal static void RunAll()
        {
            Require(OperationalReportCatalog.IsAllowedSource("Medicines") &&
                OperationalReportCatalog.IsAllowedSource("CashLedger"),
                "مصدر الأدوية وكشف النقد المشترك يجب أن يكونا مسموحين.");
            Require(!OperationalReportCatalog.IsAllowedSource("SELECT * FROM dbo.EmployeeTb1"), "لا يجب قبول SQL كمصدر تقرير.");
            Require(DailyPharmacyPerformanceReport.Query.Contains("CommercialSalesReturn") &&
                DailyPharmacyPerformanceReport.Query.Contains("[إشعارات المرتجع التجاري]") &&
                DailyPharmacyPerformanceReport.Query.Contains("[العملة]") && !DailyPharmacyPerformanceReport.Query.Contains("[حركات المخزون]") &&
                DailyPharmacyPerformanceReport.StockQuery.Contains("[الوحدات المعادة للمخزون]"),
                "التقرير المالي يفصل العملات ويعرض المرتجع في الإيراد والتكلفة، والمخزون في تقرير مستقل.");
            OperationalReportTemplate valid = OperationalReportTemplateStore.Normalize(new OperationalReportTemplate
            {
                Name = "  مخزون شهري  ", SourceKey = "InventoryMovements", FromDate = new DateTime(2026, 1, 1), ToDate = new DateTime(2026, 1, 31),
                SearchText = "باراسيتامول", VisibleColumns = new List<string> { "الصنف", "بعد" }, ColumnOrder = new List<string> { "بعد", "الصنف" }
            });
            Require(valid != null && valid.Name == "مخزون شهري" && valid.TemplateId.Length == 32 && valid.VisibleColumns.Count == 2, "تطبيع قالب التقرير التشغيلي غير صحيح.");
            valid.SearchText = new string('س', 121);
            Require(OperationalReportTemplateStore.Normalize(valid).SearchText == valid.SearchText,
                "يجب إظهار نص بحث القالب القديم كاملًا لتصحيحه، لا اقتطاعه بصمت.");
            valid.SearchText = "باراسيتامول";
            valid.HasPrintSettings = true;
            valid.PrintOrientation = ReportPageOrientation.Landscape.ToString();
            valid.PrintDensity = ReportDensity.Compact.ToString();
            valid.PrintCompanyName = true;
            valid.PrintTaxNumber = true;
            using (var stream = new MemoryStream())
            {
                new XmlSerializer(typeof(OperationalReportTemplate)).Serialize(stream, valid);
                stream.Position = 0;
                var restored = (OperationalReportTemplate)new XmlSerializer(typeof(OperationalReportTemplate)).Deserialize(stream);
                restored = OperationalReportTemplateStore.Normalize(restored);
                Require(restored.HasPrintSettings && restored.PrintOrientation == "Landscape" &&
                    restored.PrintDensity == "Compact" && restored.PrintCompanyName && restored.PrintTaxNumber,
                    "فشل حفظ خيارات عرض وطباعة قالب التقرير.");
            }
            VerifyTemplateBackupRecovery(valid);

            Require(OperationalReportTemplateStore.Normalize(new OperationalReportTemplate { Name = "x", SourceKey = "Unknown", FromDate = DateTime.Today, ToDate = DateTime.Today }) == null, "لا يجب قبول مصدر غير معرف.");

            string malformedPath = Path.Combine(Path.GetTempPath(), "pharmacy-operational-template-test-" + Guid.NewGuid().ToString("N") + ".xml");
            try
            {
                File.WriteAllText(malformedPath, "<invalid-template");
                Require(!OperationalReportTemplateStore.CanReadForTest(malformedPath), "يجب تمييز ملف القالب التشغيلي التالف بدلاً من استبداله بقائمة فارغة.");
                Require(File.Exists(malformedPath), "لا يجوز حذف ملف القالب التشغيلي التالف أثناء محاولة القراءة.");
            }
            finally
            {
                if (File.Exists(malformedPath)) File.Delete(malformedPath);
            }
        }

        private static void VerifyTemplateBackupRecovery(OperationalReportTemplate original)
        {
            string path = Path.Combine(Path.GetTempPath(), "pharmacy-operational-recovery-" + Guid.NewGuid().ToString("N") + ".xml");
            var extra = new OperationalReportTemplate { Name = "Additional owned template", SourceKey = "Medicines",
                FromDate = original.FromDate, ToDate = original.ToDate, SearchText = "000-A" };
            try
            {
                WriteRecoveryFixture(path + ".bak", new[] { original });
                string backup = File.ReadAllText(path + ".bak");
                File.WriteAllText(path, "<invalid-primary");
                Require(OperationalReportTemplateStore.CanReadForTest(path), "Readable operational backup was not recovered.");
                var loaded = OperationalReportTemplateStore.ReadForTest(path);
                Require(loaded.Count == 1 && loaded[0].TemplateId == original.TemplateId && loaded[0].Name == original.Name &&
                    loaded[0].SearchText == original.SearchText && loaded[0].VisibleColumns.SequenceEqual(original.VisibleColumns) &&
                    loaded[0].ColumnOrder.SequenceEqual(original.ColumnOrder) && loaded[0].PrintOrientation == original.PrintOrientation,
                    "Operational recovery changed identity, raw text or print/column settings.");
                Require(File.ReadAllText(path) == "<invalid-primary" && File.ReadAllText(path + ".bak") == backup,
                    "Read-only recovery changed primary or backup.");

                Require(OperationalReportTemplateStore.SaveAtPath(path, extra), "Operational save after recovery failed.");
                Require(OperationalReportTemplateStore.ReadForTest(path).Count == 2 && File.ReadAllText(path + ".bak") == backup,
                    "Operational save lost a recovered template or overwrote the readable backup.");

                File.WriteAllText(path, "<invalid-primary");
                Require(OperationalReportTemplateStore.DeleteAtPath(path, original.TemplateId), "Operational delete after recovery failed.");
                Require(OperationalReportTemplateStore.ReadForTest(path).Count == 0 && File.ReadAllText(path + ".bak") == backup,
                    "Operational delete failed or damaged the backup.");
                Require(OperationalReportTemplateStore.CanReadForTest(path) && OperationalReportTemplateStore.ReadForTest(path).Count == 0,
                    "Valid empty primary resurrected deleted templates.");

                File.WriteAllText(path, "<invalid-primary"); File.WriteAllText(path + ".bak", "<invalid-backup");
                Require(!OperationalReportTemplateStore.CanReadForTest(path) &&
                    !OperationalReportTemplateStore.SaveAtPath(path, extra) &&
                    !OperationalReportTemplateStore.DeleteAtPath(path, original.TemplateId), "Both unreadable files allowed a write.");
                Require(File.ReadAllText(path) == "<invalid-primary" && File.ReadAllText(path + ".bak") == "<invalid-backup",
                    "Refused write modified unreadable files.");

                File.Delete(path); WriteRecoveryFixture(path + ".bak", new[] { original });
                backup = File.ReadAllText(path + ".bak");
                Require(OperationalReportTemplateStore.ReadForTest(path).Count == 1 && !File.Exists(path),
                    "Backup-only read failed or created a primary file.");
                Require(OperationalReportTemplateStore.SaveAtPath(path, extra) && OperationalReportTemplateStore.ReadForTest(path).Count == 2 &&
                    File.ReadAllText(path + ".bak") == backup, "Backup-only save lost templates or changed backup.");

                string primary = File.ReadAllText(path);
                using (FileStream held = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    Require(OperationalReportTemplateStore.ReadForTest(path).Count == 1, "Locked primary did not fall back to backup.");
                    Require(!OperationalReportTemplateStore.SaveAtPath(path, extra) && File.ReadAllText(path + ".bak") == backup,
                        "Locked write succeeded or changed the readable backup.");
                }
                Require(File.ReadAllText(path) == primary, "Locked primary was modified.");
                Console.WriteLine("PASS operational template backup recovery cases=7; no SQL/UI/user template store.");
            }
            finally
            {
                foreach (string ownedPath in new[] { path, path + ".bak", path + ".tmp", path + ".lock" })
                    if (File.Exists(ownedPath)) File.Delete(ownedPath);
            }
        }

        private static void WriteRecoveryFixture(string path, IEnumerable<OperationalReportTemplate> templates)
        {
            using (FileStream stream = File.Create(path))
                new XmlSerializer(typeof(List<OperationalReportTemplate>)).Serialize(stream, templates.ToList());
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }

    internal sealed class OperationalReportBuilderForm : Form
    {
        private readonly bool previewMode;
        private readonly AntdUI.Select source = DesktopComponents.Select("operationalReportSource", "مصدر التقرير");
        private readonly TextBox search = new TextBox();
        private readonly ComboBox reportViewMode = new NativeThemedSelect { Name = "operationalReportViewMode", DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly AntdUI.Panel reportViewModeFrame;
        private readonly AntdUI.Panel searchFrame;
        private readonly AntdUI.DatePicker fromDate = DesktopComponents.DatePicker("operationalReportFrom", "من تاريخ", DateTime.Today);
        private readonly AntdUI.DatePicker toDate = DesktopComponents.DatePicker("operationalReportTo", "إلى تاريخ", DateTime.Today);
        private readonly ComboBox templates = new NativeThemedSelect { Name = "operationalReportTemplates", AccessibleName = "قوالب التقارير الجاهزة والمحفوظة" };
        private readonly AntdUI.Panel templatesFrame;
        private readonly DataGridView grid = new DataGridView();
        private readonly Label status = new Label();
        private bool leftToRight;
        private DataTable currentTable;
        private string loadedReportTitle;
        private string loadedReportPeriod;
        private List<string> visibleColumns;
        private List<string> columnOrder;
        private ReportPrintOptions currentPrintOptions;
        private int reportLoadGeneration;
        private readonly Func<OperationalReportReadSession> reportSessionFactory;
        private readonly Func<IReadOnlyList<OperationalReportTemplate>> templateLoader;
        private OperationalReportReadSession loadedReportSession;
        private OperationalReportRequest loadedReportRequest;
        private CancellationTokenSource reportReadCancellation;
        private FlowLayoutPanel templateActions;
        private FlowLayoutPanel actions;
        private readonly AntdUI.Panel templateContext = DesktopComponents.Card("operationalReportTemplateContext");
        // Native multiline viewing preserves Arabic and wraps long uninterrupted names.
        private readonly TextBox templateDetails = new TextBox
        {
            Name = "operationalReportTemplateDetails", Multiline = true, ReadOnly = true,
            WordWrap = true, BorderStyle = BorderStyle.None, Dock = DockStyle.Fill,
            ScrollBars = ScrollBars.None, Font = AppFonts.Create(9.5F)
        };
        private bool applyingPresentation;
        private string statusSource;
        private bool? statusSuccess;
        private bool showingResultStatus;
        private bool layingOutActions;
        private string loadedSourceKey;
        private DateTime loadedFromDate;
        private DateTime loadedToDate;
        private bool loadedUsesDates;

        internal OperationalReportBuilderForm(bool previewMode = false,Func<OperationalReportReadSession> sessionFactory=null, Func<IReadOnlyList<OperationalReportTemplate>> templateLoader=null)
        {
            this.previewMode = previewMode;
            reportSessionFactory=sessionFactory??ApplicationServices.CreateOperationalReportReadSession;
            this.templateLoader = templateLoader ?? (() => OperationalReportTemplateStore.Load());
            leftToRight = UiDirection.IsLeftToRight;
            templatesFrame = DesktopComponents.NativeSelectFrame(templates, "operationalReportTemplatesFrame", "قوالب التقارير الجاهزة والمحفوظة");
            searchFrame = DesktopComponents.NativeTextFrame(search, "operationalReportSearchFrame");
            reportViewModeFrame = DesktopComponents.NativeSelectFrame(reportViewMode, "operationalReportViewModeFrame", "طريقة عرض التقرير" );
            search.TextChanged += (sender, args) =>
            {
                if (!search.Focused) UiDirection.ApplyContentDirection(search, leftToRight);
            };
            search.Leave += (sender, args) => UiDirection.ApplyContentDirection(search, leftToRight);
            Text = "منشئ التقارير التشغيلي";
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = true;
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(760, 560);
            Size = new Size(1120, 720);
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            KeyPreview = true;
            BuildLayout();
            UiTheme.Apply(this);
            ApplyVisualTheme();
            FitToWorkingArea();
            LayoutActionRows();
            foreach (OperationalReportSource item in OperationalReportCatalog.GetSources()) source.Items.Add(item);
            source.SelectedIndex = 0;
            fromDate.Value = new DateTime(DateTime.Today.Year, 1, 1);
            toDate.Value = DateTime.Today;
            if (previewMode) templates.Items.Add(new TemplateChoice("القوالب الجاهزة والمحفوظة", null));
            else RefreshTemplates();
            templates.SelectedIndex = 0;
            ApplyPresentation(leftToRight);
            UpdateDateAvailability();
            source.SelectedIndexChanged += (s, e) =>
            {
                if (applyingPresentation) return;
                visibleColumns = null; columnOrder = null; UpdateDateAvailability(); LoadData();
            };
            FormClosed += (s, e) => { CancelOperationalRead();ClearLoadedReport(); };
            UiSessionSubscription.Attach(this,()=>
            {
                if(loadedReportSession==null&&reportReadCancellation==null)return;
                CancelOperationalRead();ClearLoadedReport();SetReportStatus("تغيرت جلسة المستخدم؛ أعد تحميل التقرير.",false);
            });
            fromDate.ValueChanged+=(s,e)=>OperationalFiltersChanged();
            toDate.ValueChanged+=(s,e)=>OperationalFiltersChanged();
            search.TextChanged+=(s,e)=>OperationalFiltersChanged();
            reportViewMode.SelectedIndexChanged += (s, e) =>
            {
                if (applyingPresentation) return;
                visibleColumns = null; columnOrder = null;
                OperationalFiltersChanged(); LoadData();
            };
            SetOperationalOutputReady(false);
            Load += (s, e) => { FitToWorkingArea(); if (previewMode) ShowPreviewData(); else LoadData(); };
            Resize += (s, e) => LayoutActionRows();
            templates.SelectedIndexChanged += (s, e) =>
            {
                if (applyingPresentation) return;
                UiDirection.ApplyContentDirection(templates, leftToRight);
                UpdateTemplateContext();
            };
            KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); };
        }

        private void BuildLayout()
        {
            Panel header = new Panel { Name = "operationalReportHeader", Dock = DockStyle.Top, Height = 76,
                Padding = new Padding(20, 8, 20, 8) };
            Label title = new Label { Name = "operationalReportTitle", Dock = DockStyle.Top, Height = 38,
                Text = Text, TextAlign = ContentAlignment.MiddleRight, Font = AppFonts.Create(16F, FontStyle.Bold) };
            Label subtitle = new Label { Name = "operationalReportSubtitle", Dock = DockStyle.Fill,
                Text = "اختر المصدر والفترة، ثم اعرض النتائج أو استخدم قالبًا محفوظًا.", TextAlign = ContentAlignment.MiddleRight };
            header.Controls.Add(subtitle);
            header.Controls.Add(title);

            TableLayoutPanel filters = new TableLayoutPanel { Name = "operationalReportFilters", Dock = DockStyle.Top, Height = 162,
                ColumnCount = 2, RowCount = 3, Padding = new Padding(18, 4, 18, 4) };
            filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            filters.RowStyles.Add(new RowStyle(SizeType.Percent, 33.333F));
            filters.RowStyles.Add(new RowStyle(SizeType.Percent, 33.333F));
            filters.RowStyles.Add(new RowStyle(SizeType.Percent, 33.334F));
            search.AccessibleName = "بحث في نتائج التقرير";
            search.Name = "operationalReportSearch";
            search.Text = string.Empty;
            AddField(filters, "operationalReportSourceField", "مصدر البيانات", source, 0, 0);
            AddField(filters, "operationalReportSearchField", "بحث", searchFrame, 1, 0);
            AddField(filters, "operationalReportFromField", "من تاريخ", fromDate, 0, 1);
            AddField(filters, "operationalReportToField", "إلى تاريخ", toDate, 1, 1);
            AddField(filters, "operationalReportViewModeField", "طريقة العرض", reportViewModeFrame, 0, 2);
            filters.SetColumnSpan(filters.Controls["operationalReportViewModeField"], 2);

            templateActions = new FlowLayoutPanel { Name = "operationalReportTemplateActions", Dock = DockStyle.Top,
                Height = 58, FlowDirection = FlowDirection.RightToLeft, WrapContents = true,
                Padding = new Padding(16, 5, 16, 5) };
            templatesFrame.Width = 260;
            templatesFrame.Height = 44;
            templateActions.Controls.Add(CreateButton("saveReportTemplate", "حفظ", AppIcon.Save, SaveTemplate));
            templateActions.Controls.Add(CreateButton("applyReportTemplate", "تطبيق", AppIcon.Confirm, ApplyTemplate));
            templateActions.Controls.Add(CreateButton("customizeReportColumns", "تخصيص الأعمدة", AppIcon.Settings, CustomizeColumns));
            templateActions.Controls.Add(CreateButton("deleteReportTemplate", "حذف", AppIcon.Delete, DeleteTemplate));
            templateActions.Controls.Add(templatesFrame);
            templateContext.Dock = DockStyle.Top;
            templateContext.Height = 0;
            templateContext.Visible = false;
            templateContext.Padding = new Padding(20, 4, 20, 4);
            templateContext.Controls.Add(templateDetails);

            actions = new FlowLayoutPanel { Name = "operationalReportActions", Dock = DockStyle.Bottom, Height = 64,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false, Padding = new Padding(16, 8, 16, 8) };
            actions.Controls.Add(CreateButton("showOperationalReport", "عرض النتيجة", AppIcon.Report, LoadData));
            actions.Controls.Add(CreateButton("exportOperationalCsv", "تصدير Excel", AppIcon.Save, ExportCsv));
            actions.Controls.Add(CreateButton("printOperationalReport", "طباعة", AppIcon.Print, Print));
            actions.Controls.Add(CreateButton("closeOperationalReport", "إغلاق", AppIcon.Close, () => Close()));
            status.Name = "operationalReportStatus";
            status.Dock = DockStyle.Bottom;
            status.Height = 40;
            status.Padding = new Padding(20, 4, 20, 4);
            status.TextAlign = ContentAlignment.MiddleRight;
            status.Font = AppFonts.Create(9.5F, FontStyle.Regular);
            status.AccessibleName = "حالة التقرير";

            grid.Name = "operationalReportGrid";
            // This screen owns bilingual presentation; legacy Arabic rebinding must not override it.
            grid.Tag = UiTheme.PreserveLocalizedCustomGridColumns;
            grid.Dock = DockStyle.Fill;
            grid.ReadOnly = true;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AllowUserToOrderColumns = true;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            UiTheme.ApplyDataGridQuality(grid, "لا توجد نتائج مطابقة للفلاتر المحددة.");
            grid.CellFormatting += FormatReportDateCell;

            Controls.Add(grid);
            Controls.Add(status);
            Controls.Add(actions);
            Controls.Add(templateContext);
            Controls.Add(templateActions);
            Controls.Add(filters);
            Controls.Add(header);
        }

        private void FitToWorkingArea()
        {
            Rectangle area = Screen.FromControl(this).WorkingArea;
            MaximumSize = area.Size;
            MinimumSize = new Size(Math.Min(760, area.Width), Math.Min(560, area.Height));
            Size bounded = new Size(Math.Min(Size.Width, area.Width), Math.Min(Size.Height, area.Height));
            if (bounded != Size) Size = bounded;
        }

        private void ApplyVisualTheme()
        {
            Panel header = (Panel)Controls["operationalReportHeader"];
            header.BackColor = UiTheme.Background;
            header.Controls["operationalReportTitle"].BackColor = UiTheme.Background;
            header.Controls["operationalReportTitle"].ForeColor = UiTheme.Text;
            header.Controls["operationalReportSubtitle"].BackColor = UiTheme.Background;
            header.Controls["operationalReportSubtitle"].ForeColor = UiTheme.MutedText;
            var filters = (TableLayoutPanel)Controls["operationalReportFilters"];
            filters.BackColor = UiTheme.Surface;
            foreach (TableLayoutPanel field in filters.Controls.OfType<TableLayoutPanel>())
            {
                field.BackColor = UiTheme.Surface;
                foreach (Label caption in field.Controls.OfType<Label>())
                { caption.BackColor = UiTheme.Surface; caption.ForeColor = UiTheme.Text; }
            }
            WinFormsShapeAdapter.ApplyCard(filters);
            DesktopComponents.StyleNativeTextFrame(searchFrame, search);
            AntdThemeAdapter.Apply(source);
            AntdThemeAdapter.Apply(fromDate);
            AntdThemeAdapter.Apply(toDate);
            DesktopComponents.StyleNativeSelectFrame(templatesFrame, templates, leftToRight);
            templateActions.BackColor = UiTheme.Surface;
            AntdThemeAdapter.Apply(templateContext);
            templateContext.Shadow = 0;
            templateDetails.BackColor = UiTheme.Surface;
            templateDetails.ForeColor = UiTheme.Text;
            templateDetails.BorderStyle = BorderStyle.None;
            actions.BackColor = UiTheme.Background;
            status.BackColor = UiTheme.Background;
            status.ForeColor = UiTheme.MutedText;
            foreach (AntdUI.Button button in templateActions.Controls.OfType<AntdUI.Button>())
                DesktopComponents.StyleOutlinedFooterAction(button, button.Name == "deleteReportTemplate");
            foreach (AntdUI.Button button in actions.Controls.OfType<AntdUI.Button>())
                DesktopComponents.StyleOutlinedFooterAction(button);
        }

        private string Localize(string arabicText)
        {
            return leftToRight ? UiLanguageCatalog.Translate(arabicText) : arabicText;
        }

        internal void ApplyPresentation(bool useLeftToRight)
        {
            leftToRight = useLeftToRight;
            applyingPresentation = true;
            try
            {
                string selectedSourceKey = SelectedSource()?.Key;
                string selectedTemplateId = SelectedTemplate()?.Template?.TemplateId;
                Text = Localize("منشئ التقارير التشغيلي");
                Controls["operationalReportHeader"].Controls["operationalReportTitle"].Text = Text;
                Controls["operationalReportHeader"].Controls["operationalReportSubtitle"].Text =
                    Localize("اختر المصدر والفترة، ثم اعرض النتائج أو استخدم قالبًا محفوظًا.");
                var filters = (TableLayoutPanel)Controls["operationalReportFilters"];
                SetFieldCaption(filters, "operationalReportSourceField", Localize("مصدر البيانات"));
                SetFieldCaption(filters, "operationalReportSearchField", Localize("بحث"));
                SetFieldCaption(filters, "operationalReportFromField", Localize("من تاريخ"));
                SetFieldCaption(filters, "operationalReportToField", Localize("إلى تاريخ"));
                SetFieldCaption(filters, "operationalReportViewModeField", Localize("طريقة العرض"));
                reportViewMode.AccessibleName = Localize("طريقة عرض التقرير" );
                reportViewModeFrame.Controls["operationalReportViewModeFrameDropdown"].AccessibleName = reportViewMode.AccessibleName;
                source.AccessibleName = Localize("مصدر التقرير");
                search.AccessibleName = Localize("بحث في نتائج التقرير");
                fromDate.AccessibleName = Localize("من تاريخ");
                toDate.AccessibleName = Localize("إلى تاريخ");
                templates.AccessibleName = Localize("قوالب التقارير الجاهزة والمحفوظة");
                templatesFrame.Controls["operationalReportTemplatesFrameDropdown"].AccessibleName = templates.AccessibleName;

                source.SelectedIndex = -1;
                source.Items.Clear();
                foreach (OperationalReportSource item in OperationalReportCatalog.GetSources())
                    source.Items.Add(new OperationalReportSource(item.Key, Localize(item.Title), item.UsesDateRange, Localize(item.PickerTitle)));
                int sourceIndex = source.Items.Cast<OperationalReportSource>().ToList()
                    .FindIndex(item => string.Equals(item.Key, selectedSourceKey, StringComparison.Ordinal));
                source.SelectedIndex = sourceIndex >= 0 ? sourceIndex : 0;
                source.Text = Convert.ToString(source.Items[source.SelectedIndex]);
                RefreshViewModes();

                List<TemplateChoice> savedChoices = templates.Items.Cast<object>()
                    .OfType<TemplateChoice>().Where(choice => choice.Template != null).ToList();
                templates.SelectedIndex = -1;
                templates.Items.Clear();
                templates.Items.Add(new TemplateChoice(Localize("القوالب الجاهزة والمحفوظة"), null));
                foreach (TemplateChoice choice in savedChoices)
                {
                    if (choice.Preset != null) AddPresetChoice(choice.Preset);
                    else templates.Items.Add(new TemplateChoice(choice.Template.Name + " — " + GetSourcePickerTitle(choice.Template.SourceKey), choice.Template));
                }
                int templateIndex = templates.Items.Cast<object>().OfType<TemplateChoice>().ToList()
                    .FindIndex(choice => choice.Template != null &&
                        string.Equals(choice.Template.TemplateId, selectedTemplateId, StringComparison.OrdinalIgnoreCase));
                templates.SelectedIndex = templateIndex >= 0 ? templateIndex : 0;
                templates.Text = Convert.ToString(templates.Items[templates.SelectedIndex]);

                SetAction("saveReportTemplate", Localize("حفظ"), Localize("حفظ قالب التقرير"));
                SetAction("applyReportTemplate", Localize("تطبيق"), Localize("تطبيق قالب التقرير المحدد"));
                SetAction("customizeReportColumns", Localize("تخصيص الأعمدة"), Localize("تخصيص أعمدة التقرير"));
                SetAction("deleteReportTemplate", Localize("حذف"), Localize("حذف قالب التقرير المحدد"));
                SetAction("showOperationalReport", Localize("عرض النتيجة"), Localize("عرض نتائج التقرير"));
                SetAction("exportOperationalCsv", Localize("تصدير Excel"), Localize("تصدير التقرير إلى Excel"));
                SetAction("printOperationalReport", Localize("طباعة"), Localize("طباعة التقرير"));
                SetAction("closeOperationalReport", Localize("إغلاق"), Localize("إغلاق منشئ التقارير التشغيلي"));
                status.AccessibleName = Localize("حالة التقرير");
                // Preserve the live column objects/layout while WinForms recreates RTL handles.
                // A new result schema is generated only by BindReportTable.
                if (grid.Columns.Count > 0) grid.AutoGenerateColumns = false;
                UiDirection.Apply(this, leftToRight);
                UiTheme.UseDedicatedStatus(this);
                UiDirection.ApplyContentDirection(search, leftToRight);
                grid.RightToLeft = leftToRight ? RightToLeft.No : RightToLeft.Yes;
                UiTheme.ApplyDataGridQuality(grid, Localize("لا توجد نتائج مطابقة للفلاتر المحددة."));
                UpdateSourceContext();
                UpdateTemplateContext();
                LayoutActionRows();
                ApplyVisualTheme();
                UpdateLoadedPresentation();
                PresentReportStatus();
            }
            finally { applyingPresentation = false; }
        }

        private static void SetFieldCaption(TableLayoutPanel filters, string fieldName, string text)
        {
            var field = filters.Controls.Find(fieldName, true).FirstOrDefault() as TableLayoutPanel;
            Label caption = field?.GetControlFromPosition(0, 0) as Label;
            if (caption != null) caption.Text = text;
        }

        private void SetAction(string name, string text, string accessibleName)
        {
            AntdUI.Button button = Controls.Find(name, true).FirstOrDefault() as AntdUI.Button;
            if (button == null) return;
            button.Text = text;
            button.AccessibleName = accessibleName;
            button.IconPosition = leftToRight ? AntdUI.TAlignMini.Left : AntdUI.TAlignMini.Right;
        }

        private void LayoutActionRows()
        {
            if (templateActions == null || actions == null || layingOutActions) return;
            layingOutActions = true;
            try
            {
                var filters = (TableLayoutPanel)Controls["operationalReportFilters"];
                int captionWidth = Math.Max(80, filters.Controls.OfType<TableLayoutPanel>()
                    .SelectMany(field => field.Controls.OfType<Label>())
                    .Select(caption => TextRenderer.MeasureText(caption.Text, caption.Font).Width + 12)
                    .DefaultIfEmpty(80).Max());
                foreach (TableLayoutPanel field in filters.Controls.OfType<TableLayoutPanel>())
                    field.ColumnStyles[0].Width = captionWidth;
                var header = (Panel)Controls["operationalReportHeader"];
                var subtitle = (Label)header.Controls["operationalReportSubtitle"];
                int subtitleHeight = TextRenderer.MeasureText(subtitle.Text, subtitle.Font,
                    new Size(Math.Max(1, header.ClientSize.Width - header.Padding.Horizontal), int.MaxValue),
                    TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height;
                // Keep the full source context visible at compact sizes. Only the
                // heading repeated in the window caption yields its vertical space.
                bool compact = ClientSize.Height < 600;
                var title = header.Controls["operationalReportTitle"];
                title.Visible = !compact;
                subtitle.Visible = true;
                header.Height = header.Padding.Vertical + (compact ? 0 : title.Height) +
                    Math.Max(22, subtitleHeight + 4);
                filters.Height = compact ? 144 : 162;
                int actionWidth = Math.Max(1, ClientSize.Width - templateActions.Padding.Horizontal);
                int buttonsWidth = 0;
                foreach (AntdUI.Button button in templateActions.Controls.OfType<AntdUI.Button>())
                {
                    button.Width = Math.Max(86, TextRenderer.MeasureText(button.Text, button.Font).Width + 48);
                    buttonsWidth += button.Width + button.Margin.Horizontal;
                }
                // Leave room for FlowLayoutPanel's native rounding and internal spacing.
                templatesFrame.Width = Math.Min(560, Math.Max(200, actionWidth - buttonsWidth - templatesFrame.Margin.Horizontal - 12));
                int rowsHeight = 0, rowWidth = 0, rowHeight = 0;
                foreach (Control action in templateActions.Controls)
                {
                    int width = action.Width + action.Margin.Horizontal;
                    if (rowWidth > 0 && rowWidth + width > actionWidth)
                    { rowsHeight += rowHeight; rowWidth = 0; rowHeight = 0; }
                    rowWidth += width;
                    rowHeight = Math.Max(rowHeight, action.Height + action.Margin.Vertical);
                }
                templateActions.Height = templateActions.Padding.Vertical + rowsHeight + rowHeight;
                if (templateContext.Visible)
                {
                    templateContext.Width = ClientSize.Width;
                    templateContext.PerformLayout();
                    int lines = templateDetails.GetLineFromCharIndex(templateDetails.TextLength) + 1;
                    int lineHeight = templateDetails.Font.Height;
                    if (lines > 1)
                        lineHeight = Math.Max(lineHeight, templateDetails.GetPositionFromCharIndex(
                            templateDetails.GetFirstCharIndexFromLine(1)).Y - templateDetails.GetPositionFromCharIndex(0).Y);
                    templateContext.Height = templateContext.Padding.Vertical + lines * lineHeight + 6;
                }
                // RightToLeftLayout already mirrors the form for Arabic.
                templateActions.FlowDirection = FlowDirection.LeftToRight;
                actions.FlowDirection = FlowDirection.LeftToRight;
                int textWidth = Math.Max(1, ClientSize.Width - status.Padding.Horizontal);
                int textHeight = TextRenderer.MeasureText(status.Text, status.Font, new Size(textWidth, int.MaxValue),
                    TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height;
                status.Height = Math.Max(40, textHeight + status.Padding.Vertical + 4);
            }
            finally { layingOutActions = false; }
        }

        private void SetReportStatus(string arabicText, bool? success)
        {
            statusSource = arabicText;
            statusSuccess = success;
            showingResultStatus = false;
            PresentReportStatus();
        }

        private void PresentReportStatus()
        {
            string message = Localize(statusSource ?? string.Empty);
            if (showingResultStatus && currentTable != null)
            {
                message = Localize("النتائج") + ": " + currentTable.Rows.Count.ToString("N0") +
                    (currentTable.Rows.Count >= 2000 ? Localize(" (تم تطبيق حد العرض الآمن)") : string.Empty);
                if (OperationalReportCatalog.IsFastMovementSource(loadedSourceKey)) message += " — " + Localize(
                    loadedSourceKey == "MostMovementsToday"
                    ? "عدد سجلات الحركة، وليس مجموع الكميات. الترتيب تنازلي ثم اسم الصنف."
                    : "صافي الكمية بعد الإلغاءات والمرتجعات، بما فيها التالف. الفترة بتوقيت الجهاز وتشمل اليوم؛ الصافي الموجب فقط.");
            }
            string summaryExplanation = LoadedSummaryExplanation();
            if (showingResultStatus && summaryExplanation.Length > 0) message += " — " + summaryExplanation;
            UiTheme.SetStatus(status, message, statusSuccess);
            status.AccessibleName = message;
            status.AccessibleDescription = message;
            LayoutActionRows();
        }

        private void UpdateLoadedPresentation()
        {
            foreach (DataGridViewColumn column in grid.Columns)
                column.HeaderText = Localize(column.Name);
            if (loadedSourceKey == null) return;
            loadedReportTitle = GetSourceTitle(loadedSourceKey);
            if (OperationalReportCatalog.SupportsGroupedView(loadedSourceKey) && loadedReportRequest != null)
                loadedReportTitle += " — " + GetViewModeTitle(loadedReportRequest.ViewMode);
            loadedReportPeriod = loadedUsesDates
                ? Localize("الفترة") + ": " + loadedFromDate.ToString("yyyy/MM/dd", System.Globalization.CultureInfo.InvariantCulture) + " — " + loadedToDate.ToString("yyyy/MM/dd", System.Globalization.CultureInfo.InvariantCulture)
                : Localize("تقرير تشغيلي") + " — " + DateTime.Today.ToString("yyyy/MM/dd", System.Globalization.CultureInfo.InvariantCulture);

            if (currentTable != null && currentTable.Rows.Count >= OperationalReportRequest.MaximumRows)
                loadedReportPeriod += " — " + Localize("تم تطبيق حد العرض الآمن؛ المخرجات تشمل المعروض فقط.");
        }

        private void ShowPreviewData()
        {
            ClearLoadedReport();
            currentTable = new DataTable();
            currentTable.Columns.Add("الصنف");
            currentTable.Columns.Add("الكمية");
            currentTable.Columns.Add("الإجمالي");
            currentTable.Rows.Add("باراسيتامول 500 mg", "2", "1,250.50 ر.ي");
            BindReportTable(currentTable);
            SetOperationalOutputReady(currentTable.Rows.Count > 0);
            SetReportStatus("معاينة ببيانات وهمية؛ لم تُقرأ قاعدة البيانات.", true);
            status.AccessibleName = status.Text;
        }

        private void UpdateDateAvailability()
        {
            OperationalReportSource selected = SelectedSource();
            if (selected != null && OperationalReportCatalog.IsFastMovementSource(selected.Key))
            {
                var period = OperationalReportCatalog.CreateRequest(selected.Key, DateTime.Today, DateTime.Today, string.Empty);
                bool wasApplying = applyingPresentation;
                applyingPresentation = true;
                try { fromDate.Value = period.FromDate; toDate.Value = period.ToDateExclusive.AddDays(-1); }
                finally { applyingPresentation = wasApplying; }
            }
            bool enabled = selected != null && selected.UsesDateRange;
            fromDate.Enabled = enabled;
            toDate.Enabled = enabled;
            search.Enabled = selected == null || selected.Key != "DailyPerformance";
            RefreshViewModes();
            UpdateSourceContext();
        }

        private void UpdateSourceContext()
        {
            OperationalReportSource selected = SelectedSource();
            string fullTitle = selected == null ? string.Empty : GetSourceTitle(selected.Key);
            source.AccessibleDescription = fullTitle;
            Controls["operationalReportHeader"].Controls["operationalReportSubtitle"].Text =
                selected == null ? Localize("اختر المصدر والفترة، ثم اعرض النتائج أو استخدم قالبًا محفوظًا.")
                : Localize("مصدر البيانات") + ": " + fullTitle;
            LayoutActionRows();
        }

        private void ClearLoadedReport()
        {
            grid.AutoGenerateColumns = true;
            grid.DataSource = null;
            currentTable?.Dispose();
            currentTable = null;
            loadedReportSession=null;loadedReportRequest=null;SetOperationalOutputReady(false);
            loadedReportTitle = null;
            loadedReportPeriod = null;
            loadedSourceKey = null;
        }
        private async void LoadData(){if(previewMode){ShowPreviewData();return;}await LoadReportAsync();}
        private void SetOperationalOutputReady(bool ready)
        {
            foreach(string name in new[]{"exportOperationalCsv","printOperationalReport","customizeReportColumns"})
                foreach(Control action in Controls.Find(name,true))action.Enabled=ready;
        }
        private void CancelOperationalRead()
        {
            reportLoadGeneration++;var previous=reportReadCancellation;reportReadCancellation=null;
            if(previous!=null){previous.Cancel();previous.Dispose();}
        }
        private void OperationalFiltersChanged()
        {
            if(applyingPresentation||previewMode||(currentTable==null&&reportReadCancellation==null))return;
            CancelOperationalRead();ClearLoadedReport();SetReportStatus("تغيرت المرشحات؛ أعد عرض التقرير.",null);
        }
        private bool OperationalFiltersMatch(OperationalReportRequest request)
        {
            if(request==null||SelectedSource()?.Key!=request.Kind.ToString()||SelectedViewMode()!=request.ViewMode)return false;
            if (OperationalReportCatalog.IsFastMovementSource(request.Kind.ToString()))
            {
                var current = OperationalReportCatalog.CreateRequest(request.Kind.ToString(), DateTime.Today, DateTime.Today, search.Text);
                return current.FromDate == request.FromDate && current.ToDateExclusive == request.ToDateExclusive && current.SearchText == request.SearchText;
            }
            return (fromDate.Value??DateTime.Today).Date==request.FromDate&&(toDate.Value??DateTime.Today).Date.AddDays(1)==request.ToDateExclusive&&
                ReportSearchCriteria.Normalize(search.Enabled?search.Text:string.Empty)==request.SearchText;
        }
        internal Func<bool> CaptureOperationalOutputContext()
        {
            var table=currentTable;var session=loadedReportSession;var request=loadedReportRequest;
            return ()=>!IsDisposed&&!Disposing&&table!=null&&ReferenceEquals(table,currentTable)&&ReferenceEquals(session,loadedReportSession)&&ReferenceEquals(request,loadedReportRequest)&&session!=null&&session.IsCurrent&&OperationalFiltersMatch(request)&&table.Rows.Count>0;
        }
        private bool EnsureOperationalOutput(Func<bool> current)
        {
            if(ReportPreviewLifetime.ContextCurrent(current))return true;
            if(!IsDisposed&&!Disposing){ClearLoadedReport();SetReportStatus("لا توجد بيانات صالحة للطباعة أو التصدير؛ أعد عرض التقرير.",false);}return false;
        }
        internal async Task<bool> LoadReportAsync()
        {
            if(previewMode||IsDisposed||Disposing)return false;
            var selected=SelectedSource();if(selected==null)return false;
            if(selected.UsesDateRange&&(!fromDate.Value.HasValue||!toDate.Value.HasValue))
            {SetReportStatus("اختر تاريخ البداية والنهاية قبل عرض التقرير.",false);return false;}
            if(selected.UsesDateRange&&!OperationalReportCatalog.IsValidDateRange(fromDate.Value.Value,toDate.Value.Value))
            {SetReportStatus("تاريخ البداية يجب أن يسبق تاريخ النهاية أو يساويه.",false);return false;}
            UpdateDateAvailability(); // Refresh fixed periods when the day has changed.
            OperationalReportRequest request;
            try{request=OperationalReportCatalog.CreateRequest(selected.Key,fromDate.Value??DateTime.Today,toDate.Value??DateTime.Today,search.Enabled?search.Text:string.Empty,SelectedViewMode());}
            catch(InvalidOperationException error){SetReportStatus(error.Message,false);return false;}
            CancelOperationalRead();int generation=reportLoadGeneration;ClearLoadedReport();
            var cancellation=reportReadCancellation=new CancellationTokenSource();var token=cancellation.Token;
            OperationalReportReadSession session=null;SetReportStatus("جار تحميل التقرير...",null);
            try
            {
                session=reportSessionFactory();session.EnsureContext();
                var result=await session.LoadAsync(request,token);token.ThrowIfCancellationRequested();
                if(IsDisposed||Disposing||generation!=reportLoadGeneration||!OperationalFiltersMatch(request))return false;
                session.EnsureContext();currentTable=OperationalReportCatalog.ToDataTable(result);
                loadedReportSession=session;loadedReportRequest=request;loadedSourceKey=selected.Key;
                loadedFromDate=request.FromDate;loadedToDate=request.ToDateExclusive.AddDays(-1);loadedUsesDates=selected.UsesDateRange||OperationalReportCatalog.IsFastMovementSource(selected.Key);
                BindReportTable(currentTable);SetOperationalOutputReady(currentTable.Rows.Count>0);
                showingResultStatus=true;statusSuccess=true;PresentReportStatus();status.AccessibleName=status.Text;return true;
            }
            catch(OperationCanceledException){return false;}
            catch(Exception error)
            {
                if(IsDisposed||Disposing||generation!=reportLoadGeneration)return false;
                ClearLoadedReport();
                if(session!=null&&!ReportPreviewLifetime.ContextCurrent(()=>session.IsCurrent))SetReportStatus("تغيرت جلسة المستخدم؛ أعد تحميل التقرير.",false);
                else{string code=AppDiagnostics.Log("OperationalReportLoad",error);SetReportStatus("تعذر تحميل التقرير.",false);status.AccessibleDescription=status.Text+" "+code;}
                return false;
            }
            finally{if(ReferenceEquals(reportReadCancellation,cancellation))reportReadCancellation=null;cancellation.Dispose();}
        }
        private void RefreshTemplates(string selectedName = null)
        {
            templates.Items.Clear();
            templates.Items.Add(new TemplateChoice(Localize("القوالب الجاهزة والمحفوظة"), null));
            foreach (OperationalReportTemplate template in templateLoader()) templates.Items.Add(new TemplateChoice(template.Name + " — " + GetSourcePickerTitle(template.SourceKey), template));
            foreach (OperationalReportPreset preset in OperationalReportPresets.GetAll()) AddPresetChoice(preset);
            templates.SelectedIndex = 0;
            if (!string.IsNullOrWhiteSpace(selectedName))
            {
                for (int index = 1; index < templates.Items.Count; index++)
                {
                    TemplateChoice choice = templates.Items[index] as TemplateChoice;
                    if (choice != null && choice.Template != null && string.Equals(choice.Template.Name, selectedName, StringComparison.CurrentCultureIgnoreCase)) { templates.SelectedIndex = index; break; }
                }
            }
            UpdateTemplateContext();
        }

        private void AddPresetChoice(OperationalReportPreset preset)
        {
            string name = leftToRight ? preset.EnglishTitle : preset.ArabicTitle;
            templates.Items.Add(new TemplateChoice(Localize("جاهز") + ": " + name + " — " + GetSourcePickerTitle(preset.SourceKey), null, preset, leftToRight));
        }

        private void UpdateTemplateContext()
        {
            TemplateChoice choice = SelectedTemplate();
            OperationalReportTemplate selected = choice?.Template;
            foreach (Control action in Controls.Find("deleteReportTemplate", true)) action.Enabled = selected != null && choice.Preset == null;
            templateContext.Visible = selected != null;
            string details = selected == null ? string.Empty :
                Localize("القالب المحدد") + ": " + selected.Name + Environment.NewLine +
                Localize("مصدر القالب") + ": " + GetSourceTitle(selected.SourceKey) + " — " +
                Localize("طريقة العرض") + ": " + GetViewModeTitle(TemplateViewMode(selected));
            templateDetails.Text = details;
            UiDirection.ApplyContentDirection(templateDetails, leftToRight);
            templateDetails.AccessibleName = Localize("تفاصيل القالب المحدد");
            templateDetails.AccessibleDescription = details;
            templates.AccessibleDescription = details;
            if (selected == null) templateContext.Height = 0;
            LayoutActionRows();
        }

        private OperationalReportTemplate CaptureTemplateSnapshot(string name, string searchText)
        {
            OperationalReportSource selected = SelectedSource();
            return new OperationalReportTemplate
            {
                Name = name, SourceKey = selected == null ? string.Empty : selected.Key,
                FromDate = fromDate.Value ?? DateTime.Today, ToDate = toDate.Value ?? DateTime.Today, SearchText = searchText,
                ViewMode = SelectedViewMode().ToString(),
                VisibleColumns = grid.Columns.Cast<DataGridViewColumn>().Where(column => column.Visible).Select(column => column.Name).ToList(),
                ColumnOrder = grid.Columns.Cast<DataGridViewColumn>().OrderBy(column => column.DisplayIndex).Select(column => column.Name).ToList(),
                HasPrintSettings = currentPrintOptions != null,
                PrintOrientation = (currentPrintOptions?.Orientation ?? ReportPageOrientation.Automatic).ToString(),
                PrintDensity = (currentPrintOptions?.Density ?? ReportDensity.Standard).ToString(),
                PrintCompanyName = currentPrintOptions?.ShowCompanyName ?? false,
                PrintLogo = currentPrintOptions?.ShowLogo ?? false,
                PrintTaxNumber = currentPrintOptions?.ShowTaxNumber ?? false,
                PrintCommercialRegistration = currentPrintOptions?.ShowCommercialRegistration ?? false,
                PrintMobile = currentPrintOptions?.ShowMobile ?? false,
                PrintAddress = currentPrintOptions?.ShowAddress ?? false,
                PrintWebsite = currentPrintOptions?.ShowWebsite ?? false
            };
        }

        private void SaveTemplate()
        {
            if (previewMode) { SetReportStatus("معاينة فقط؛ لم يُحفظ قالب.", null); status.AccessibleName = status.Text; return; }
            string searchText;
            try { searchText = ReportSearchCriteria.Normalize(search.Text); }
            catch (InvalidOperationException exception)
            {
                SetReportStatus(exception.Message, false);
                status.AccessibleName = status.Text;
                search.Focus();
                return;
            }
            string name;
            if (!ReportTemplateNameDialog.TryGetName(this, Text, out name, leftToRight)) return;
            OperationalReportTemplate template = CaptureTemplateSnapshot(name, searchText);
            if (!OperationalReportTemplateStore.Save(template))
            {
                SetReportStatus("تعذر حفظ القالب. تحقق من الاسم والفترة.", false);
                ShowReportMessage("تعذر حفظ القالب. تحقق من الاسم والفترة.", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            RefreshTemplates(name);
            SetReportStatus("تم حفظ القالب لهذا المستخدم؛ يشمل الفلاتر وخيارات العرض فقط.", true);
            ShowReportMessage("تم حفظ القالب لهذا المستخدم؛ يشمل الفلاتر وخيارات العرض فقط.", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ApplyTemplate()
        {
            if (previewMode) { SetReportStatus("معاينة فقط؛ لم يُطبق قالب.", null); status.AccessibleName = status.Text; return; }
            TemplateChoice choice = SelectedTemplate();
            if (choice == null || choice.Template == null) { ShowReportMessage("اختر قالباً محفوظاً أولاً.", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            OperationalReportTemplate template = choice.Template;
            if (TemplateViewMode(template) == OperationalReportViewMode.Summary && !OperationalReportCatalog.SupportsGroupedView(template.SourceKey))
            { ShowReportMessage("مصدر هذا القالب لا يدعم العرض المجمع.", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            int sourceIndex = OperationalReportCatalog.GetSources().ToList().FindIndex(item => string.Equals(item.Key, template.SourceKey, StringComparison.Ordinal));
            if (sourceIndex < 0) { ShowReportMessage("مصدر هذا القالب لم يعد متاحاً.", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            ApplyTemplateFields(template, sourceIndex);
            LoadData();
        }

        private void ApplyTemplateFields(OperationalReportTemplate template, int sourceIndex)
        {
            bool wasApplying = applyingPresentation;
            applyingPresentation = true;
            try
            {
                source.SelectedIndex = sourceIndex;
                fromDate.Value = template.FromDate;
                toDate.Value = template.ToDate;
                search.Text = template.SearchText ?? string.Empty;
                RefreshViewModes(TemplateViewMode(template));
                visibleColumns = template.VisibleColumns == null ? new List<string>() : template.VisibleColumns.ToList();
                columnOrder = template.ColumnOrder == null ? new List<string>() : template.ColumnOrder.ToList();
                currentPrintOptions = template.HasPrintSettings ? new ReportPrintOptions
                {
                    Orientation = Enum.TryParse(template.PrintOrientation, out ReportPageOrientation orientation) ? orientation : ReportPageOrientation.Automatic,
                    Density = Enum.TryParse(template.PrintDensity, out ReportDensity density) ? density : ReportDensity.Standard,
                    ShowCompanyName = template.PrintCompanyName, ShowLogo = template.PrintLogo,
                    ShowTaxNumber = template.PrintTaxNumber, ShowCommercialRegistration = template.PrintCommercialRegistration,
                    ShowMobile = template.PrintMobile, ShowAddress = template.PrintAddress, ShowWebsite = template.PrintWebsite
                } : null;
                UpdateDateAvailability();
            }
            finally { applyingPresentation = wasApplying; }
        }

        private void DeleteTemplate()
        {
            if (previewMode) { SetReportStatus("معاينة فقط؛ لم يُحذف قالب.", null); status.AccessibleName = status.Text; return; }
            TemplateChoice choice = SelectedTemplate();
            if (choice == null || choice.Template == null) { ShowReportMessage("اختر قالباً محفوظاً أولاً.", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            if (choice.Preset != null) { SetReportStatus("القالب الجاهز لا يُحذف؛ يمكنك حفظ إعداداته باسم جديد.", null); return; }
            if (ShowReportMessage("حذف القالب «{0}»؟ لن يؤثر ذلك على بيانات قاعدة البيانات.", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, choice.Template.Name) != DialogResult.Yes) return;
            if (!OperationalReportTemplateStore.Delete(choice.Template.TemplateId))
            {
                SetReportStatus("تعذر حذف القالب.", false);
                ShowReportMessage("تعذر حذف القالب.", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            RefreshTemplates();
            SetReportStatus("تم حذف القالب المحلي؛ لم تتغير بيانات النظام.", true);
        }

        private void CustomizeColumns()
        {
            if (previewMode) { SetReportStatus("معاينة فقط؛ لم تُعدل الأعمدة.", null); status.AccessibleName = status.Text; return; }
            if (grid.Columns.Count == 0) { ShowReportMessage("اعرض نتيجة التقرير أولاً.", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            List<string> selectedVisible;
            List<string> selectedOrder;
            if (!ReportColumnLayoutDialog.TryGetLayout(this, grid.Columns.Cast<DataGridViewColumn>(), out selectedVisible, out selectedOrder, leftToRight, RequiredIdentityColumns())) return;
            visibleColumns = selectedVisible;
            columnOrder = selectedOrder;
            ApplyColumnLayout();
        }

        private static void FormatReportDateCell(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (!(e.Value is DateTime)) return;
            e.Value = ReportCsvWriter.DateText((DateTime)e.Value);
            e.FormattingApplied = true;
        }
        private void BindReportTable(DataTable table)
        {
            grid.AutoGenerateColumns = true;
            grid.DataSource = table;
            grid.AutoGenerateColumns = false;
            grid.RightToLeft = leftToRight ? RightToLeft.No : RightToLeft.Yes;
            ApplyColumnLayout();
            foreach (DataGridViewColumn column in grid.Columns)
            {
                column.SortMode = DataGridViewColumnSortMode.NotSortable;
                column.DefaultCellStyle.NullValue = "—";
                if (column.ValueType == typeof(decimal) || column.ValueType == typeof(double)) column.DefaultCellStyle.Format = new[] { "الكمية", "الوارد", "الصادر", "صافي الكمية", "صافي الكمية المباعة" }.Contains(column.Name) ? "N3" : "N2";
            }
            UpdateLoadedPresentation();
        }

        private string[] RequiredIdentityColumns()
        {
            // A financial row cannot be interpreted without its saved day and currency.
            if (loadedSourceKey == "DailyPerformance") return new[] { "اليوم", "العملة" };
            if (loadedSourceKey == "SalesLines") return new[] { "رقم الفاتورة", "الصنف", "العملة", "نوع الفاتورة" };
            return new string[0];
        }

        private void ApplyColumnLayout()
        {
            if (grid.Columns.Count == 0) return;
            HashSet<string> columns = new HashSet<string>(grid.Columns.Cast<DataGridViewColumn>().Select(column => column.Name), StringComparer.Ordinal);
            HashSet<string> visible = visibleColumns == null || visibleColumns.Count == 0 ? null : new HashSet<string>(visibleColumns.Where(columns.Contains), StringComparer.Ordinal);
            if (visible != null && visible.Count == 0) visible = null;
            foreach (DataGridViewColumn column in grid.Columns) column.Visible = visible == null || visible.Contains(column.Name) || RequiredIdentityColumns().Contains(column.Name);
            if (columnOrder == null) return;
            int index = 0;
            foreach (string name in columnOrder.Where(columns.Contains).Distinct(StringComparer.Ordinal)) grid.Columns[name].DisplayIndex = index++;
        }

        private void ExportCsv()
        {
            if (previewMode) { SetReportStatus("معاينة فقط؛ لم يُنشأ ملف.", null); status.AccessibleName = status.Text; return; }
            var outputContext=CaptureOperationalOutputContext();if(!EnsureOperationalOutput(outputContext))return;
            try
            {
                using (DataTable exportTable = GetVisibleTable())
                using (SaveFileDialog dialog = new SaveFileDialog { Filter = "Excel Workbook (*.xlsx)|*.xlsx", DefaultExt = "xlsx", AddExtension = true, FileName = "OperationalReport.xlsx" })
                {
                    if (exportTable.Columns.Count == 0)
                    {
                        ShowReportMessage("يجب إظهار عمود واحد على الأقل.", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
                    if (dialog.ShowDialog(this) != DialogResult.OK) return;
                    if(!EnsureOperationalOutput(outputContext))return;
                    ExcelWorkbookWriter.WriteReportCompleteFile(exportTable, Text, dialog.FileName, leftToRight);
                    SetReportStatus("تم تصدير التقرير بنجاح.", true);
                    status.AccessibleName = status.Text;
                }
            }
            catch (Exception exception)
            {
                SetReportStatus("تعذر تأكيد تصدير التقرير؛ تحقق من الملف والمسار قبل إعادة المحاولة.", false);
                AppNotifications.ShowFileOutputIssue(this, "تصدير التقرير", exception, leftToRight);
            }
        }

        private void Print()
        {
            if (previewMode) { SetReportStatus("معاينة فقط؛ لم تُفتح الطباعة.", null); status.AccessibleName = status.Text; return; }
            var outputContext=CaptureOperationalOutputContext();if(!EnsureOperationalOutput(outputContext))return;
            try
            {
                if (currentTable == null || currentTable.Rows.Count == 0)
                { SetReportStatus("اعرض تقريرًا ذا نتائج قبل الطباعة.", false); return; }
                using (DataTable table = GetVisibleTable())
                {
                    if (table.Columns.Count == 0)
                    {
                        SetReportStatus("يجب إظهار عمود واحد على الأقل.", false);
                        return;
                    }
                    ReportPrintOptions options;
                    if (DataTablePrintPreview.ShowWithOptions(this, loadedReportTitle ?? Text,
                        loadedReportPeriod ?? string.Empty, table, currentPrintOptions, out options, leftToRight,documentDetails:LoadedSummaryExplanation(),isCurrent:outputContext) && options != null)
                        currentPrintOptions = options.Copy();
                }
            }
            catch (Exception exception)
            {
                SetReportStatus("تعذر فتح معاينة الطباعة.", false);
                UiTheme.ShowOperationIssue(this, "طباعة التقرير", exception, leftToRight);
            }
        }

        private string FormatReportMessage(string arabicText, params object[] values)
        {
            return values.Length == 0 ? Localize(arabicText) : string.Format(Localize(arabicText), values);
        }

        private DialogResult ShowReportMessage(string arabicText, MessageBoxButtons buttons, MessageBoxIcon icon, params object[] values)
        {
            return AppNotifications.Show(this, FormatReportMessage(arabicText, values), Text, buttons, icon);
        }
        private DataTable GetVisibleTable()
        {
            if (currentTable == null) return null;
            DataTable result = new DataTable(currentTable.TableName) { Locale = currentTable.Locale };
            DataGridViewColumn[] columns = grid.Columns.Cast<DataGridViewColumn>()
                .Where(column => (column.Visible || RequiredIdentityColumns().Contains(column.Name)) && currentTable.Columns.Contains(column.Name))
                .OrderBy(column => column.DisplayIndex).ToArray();
            foreach (DataGridViewColumn column in columns)
            {
                DataColumn sourceColumn = currentTable.Columns[column.Name];
                result.Columns.Add(sourceColumn.ColumnName, sourceColumn.DataType).Caption = Localize(sourceColumn.ColumnName);
                if (RequiredIdentityColumns().Contains(sourceColumn.ColumnName))
                    result.Columns[sourceColumn.ColumnName].ExtendedProperties["RepeatOnPrint"] = true;
            }
            if (columns.Length == 0) return result;
            foreach (DataRow row in currentTable.Rows)
            {
                DataRow destination = result.NewRow();
                foreach (DataGridViewColumn column in columns) destination[column.Name] = row[column.Name];
                result.Rows.Add(destination);
            }
            return result;
        }

        private static AntdUI.Button CreateButton(string name, string text, AppIcon icon, Action action)
        {
            AntdUI.Button button = DesktopComponents.Action(name, text, icon);
            button.Size = new Size(name == "customizeReportColumns" ? 152 : 126, 44);
            button.Margin = new Padding(4, 0, 4, 0);
            button.Click += (s, e) => action();
            return button;
        }

        private OperationalReportSource SelectedSource()
        {
            int index = source.SelectedIndex;
            return index >= 0 && index < source.Items.Count ? source.Items[index] as OperationalReportSource : null;
        }

        private TemplateChoice SelectedTemplate()
        {
            int index = templates.SelectedIndex;
            return index >= 0 && index < templates.Items.Count ? templates.Items[index] as TemplateChoice : null;
        }

        private static void AddField(TableLayoutPanel host, string name, string label, Control control, int column, int row)
        {
            var panel = new TableLayoutPanel { Name = name, Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1,
                Padding = new Padding(4, 2, 4, 2), Margin = Padding.Empty };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            var caption = new Label { Text = label, Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight, Font = AppFonts.Create(9F, FontStyle.Bold) };
            control.Dock = DockStyle.Fill;
            control.Font = AppFonts.Create(9.5F, FontStyle.Regular);
            control.Margin = new Padding(4, 3, 4, 3);
            panel.Controls.Add(caption, 0, 0);
            panel.Controls.Add(control, 1, 0);
            host.Controls.Add(panel, column, row);
        }

        private string GetSourceTitle(string key)
        {
            OperationalReportSource source = OperationalReportCatalog.GetSources().FirstOrDefault(item => string.Equals(item.Key, key, StringComparison.Ordinal));
            return source == null ? Localize("مصدر غير معروف") : Localize(source.Title);
        }

        private string GetSourcePickerTitle(string key)
        {
            OperationalReportSource source = OperationalReportCatalog.GetSources().FirstOrDefault(item => item.Key == key);
            return source == null ? Localize("مصدر غير معروف") : Localize(source.PickerTitle);
        }

        private OperationalReportViewMode SelectedViewMode()
        {
            var choice = reportViewMode.SelectedItem as ViewModeChoice;
            return choice == null ? OperationalReportViewMode.Analytical : choice.Mode;
        }

        private string GetViewModeTitle(OperationalReportViewMode mode)
        {
            return mode == OperationalReportViewMode.Summary ? Localize("مختصر مجمّع") : Localize("تحليلي — حركة بحركة");
        }

        private static OperationalReportViewMode TemplateViewMode(OperationalReportTemplate template)
        {
            return Enum.TryParse(template.ViewMode, out OperationalReportViewMode mode) &&
                Enum.IsDefined(typeof(OperationalReportViewMode), mode) ? mode : OperationalReportViewMode.Analytical;
        }

        private void RefreshViewModes(OperationalReportViewMode? preferred = null)
        {
            OperationalReportViewMode selected = preferred ?? SelectedViewMode();
            var selectedSource = SelectedSource();
            bool grouped = selectedSource != null && OperationalReportCatalog.SupportsGroupedView(selectedSource.Key);
            bool wasApplying = applyingPresentation;
            applyingPresentation = true;
            try
            {
                reportViewMode.Items.Clear();
                if (grouped)
                {
                    reportViewMode.Items.Add(new ViewModeChoice(OperationalReportViewMode.Analytical, GetViewModeTitle(OperationalReportViewMode.Analytical)));
                    reportViewMode.Items.Add(new ViewModeChoice(OperationalReportViewMode.Summary, GetViewModeTitle(OperationalReportViewMode.Summary)));
                    reportViewMode.SelectedIndex = selected == OperationalReportViewMode.Summary ? 1 : 0;
                    reportViewMode.AccessibleDescription = Localize("المختصر يجمع حسب الحساب أو الصنف؛ التحليلي يعرض كل حركة.");
                }
                else
                {
                    string title = selectedSource?.Key == "Medicines" ? Localize("قائمة الأصناف") : Localize("مؤشرات مجمعة أصلًا");
                    reportViewMode.Items.Add(new ViewModeChoice(OperationalReportViewMode.Analytical, title));
                    reportViewMode.SelectedIndex = 0;
                    reportViewMode.AccessibleDescription = Localize("هذا المصدر قائمة أو مؤشرات مجمعة أصلًا؛ اختر مصدر حركات لتغيير طريقة العرض.");
                }
                reportViewMode.Enabled = grouped;
                reportViewModeFrame.Enabled = grouped;
            }
            finally { applyingPresentation = wasApplying; }
        }

        private string LoadedSummaryExplanation()
        {
            if (loadedSourceKey == "DailyPerformance") return Localize("صف مستقل لكل يوم وعملة محفوظة؛ المبالغ الفارغة لقيود بلا عملة أو بعملة متعارضة. الربح قبل مصروفات التشغيل، والإلغاء بتاريخ قيده.");
            if (loadedSourceKey == "DailyInventoryActivity") return Localize("الكميات بوحدات المخزون الأساسية حسب تاريخ الحركة المحفوظ؛ المعاد للمخزون يشمل الصالح فقط، دون تكرار حسب العملة.");
            if (loadedReportRequest?.ViewMode != OperationalReportViewMode.Summary) return string.Empty;
            if (loadedSourceKey == "SalesLines") return Localize("المجاميع منفصلة حسب العملة والحالة ونوع الفاتورة؛ ليست صافي المبيعات.");
            if (loadedSourceKey == "JournalLines" || loadedSourceKey == "CashLedger")
                return Localize("المبالغ حسب العملة المسجلة؛ العملة غير المحددة لا تعني عملة النظام الحالية.");
            if (loadedSourceKey == "Purchases" || loadedSourceKey == "PurchaseLines") return Localize("الإجمالي مجموع السطور؛ لا يكرر إجمالي رأس الفاتورة.");
            return string.Empty;
        }

        private sealed class ViewModeChoice
        {
            internal ViewModeChoice(OperationalReportViewMode mode, string title) { Mode = mode; Title = title; }
            internal OperationalReportViewMode Mode { get; }
            private string Title { get; }
            public override string ToString() { return Title; }
        }

        private sealed class TemplateChoice
        {
            private readonly OperationalReportTemplate savedTemplate;
            private readonly bool leftToRight;
            internal TemplateChoice(string text, OperationalReportTemplate template) : this(text, template, null, false) { }
            internal TemplateChoice(string text, OperationalReportTemplate template, OperationalReportPreset preset, bool leftToRight)
            { Text = text; savedTemplate = template; Preset = preset; this.leftToRight = leftToRight; }
            internal OperationalReportPreset Preset { get; }
            internal OperationalReportTemplate Template { get { return Preset == null ? savedTemplate : Preset.CreateTemplate(DateTime.Today, leftToRight); } }
            private string Text { get; set; }
            public override string ToString() { return Text; }
        }
    }
}



using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml;
using PharmacyManagement.Application;

namespace project_fo_3
{
    // A single-sheet OOXML export. Values are written as typed numbers or literal
    // strings, never as formulas, so names beginning with '=' remain inert in Excel.
    internal static class ExcelWorkbookWriter
    {
        internal const int MaximumCompleteExportRows = 2000;
        internal const string TextColumnsProperty = "ExcelTextColumns";
        private const string ReportPresentationProperty = "ExcelReportPresentation";
        private const string RightToLeftProperty = "ExcelRightToLeft";
        private const string ReportNumberFormat = "#,##0.00##########################";
        private const string Spreadsheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private const string Relationships = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        private const string ContentTypes = "http://schemas.openxmlformats.org/package/2006/content-types";
        private const string PackageRelationships = "http://schemas.openxmlformats.org/package/2006/relationships";

        internal static void Write(DataTable table, string sheetName, Stream output)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            if (output == null || !output.CanWrite) throw new ArgumentException("مخرج Excel غير قابل للكتابة.", nameof(output));
            if (table.Columns.Count == 0 || table.Columns.Count > 16384 || table.Rows.Count > 1048575)
                throw new InvalidOperationException("عدد الصفوف أو الأعمدة يتجاوز حدود ورقة Excel.");
            string name = NormalizeSheetName(sheetName);
            var textColumns = GetTextColumns(table);
            bool reportPresentation = Equals(table.ExtendedProperties[ReportPresentationProperty], true);
            bool rightToLeft = !Equals(table.ExtendedProperties[RightToLeftProperty], false);
            bool hasTextStyle = textColumns.Count > 0;
            bool includeStyles = hasTextStyle || reportPresentation;
            using (var archive = new ZipArchive(output, ZipArchiveMode.Create, true, Encoding.UTF8))
            {
                WriteXml(archive, "[Content_Types].xml", writer =>
                {
                    writer.WriteStartElement("Types", ContentTypes);
                    writer.WriteStartElement("Default", ContentTypes); writer.WriteAttributeString("Extension", "rels");
                    writer.WriteAttributeString("ContentType", "application/vnd.openxmlformats-package.relationships+xml"); writer.WriteEndElement();
                    writer.WriteStartElement("Default", ContentTypes); writer.WriteAttributeString("Extension", "xml");
                    writer.WriteAttributeString("ContentType", "application/xml"); writer.WriteEndElement();
                    Override(writer, "/xl/workbook.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml");
                    Override(writer, "/xl/worksheets/sheet1.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml");
                    if (includeStyles) Override(writer, "/xl/styles.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml");
                    writer.WriteEndElement();
                });
                WriteXml(archive, "_rels/.rels", writer =>
                    WriteRelationship(writer, "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument", "xl/workbook.xml"));
                WriteXml(archive, "xl/_rels/workbook.xml.rels", writer =>
                    WriteRelationship(writer, Relationships + "/worksheet", "worksheets/sheet1.xml", includeStyles));
                if (reportPresentation) WriteReportStyles(archive);
                else if (hasTextStyle) WriteTextStyles(archive);
                WriteXml(archive, "xl/workbook.xml", writer =>
                {
                    writer.WriteStartElement("workbook", Spreadsheet);
                    writer.WriteAttributeString("xmlns", "r", null, Relationships);
                    writer.WriteStartElement("sheets", Spreadsheet);
                    writer.WriteStartElement("sheet", Spreadsheet);
                    writer.WriteAttributeString("name", name);
                    writer.WriteAttributeString("sheetId", "1");
                    writer.WriteAttributeString("r", "id", Relationships, "rId1");
                    writer.WriteEndElement(); writer.WriteEndElement(); writer.WriteEndElement();
                });
                WriteXml(archive, "xl/worksheets/sheet1.xml", writer =>
                {
                    writer.WriteStartElement("worksheet", Spreadsheet);
                    writer.WriteStartElement("sheetViews", Spreadsheet);
                    writer.WriteStartElement("sheetView", Spreadsheet);
                    writer.WriteAttributeString("workbookViewId", "0");
                    writer.WriteAttributeString("rightToLeft", rightToLeft ? "1" : "0");
                    if (reportPresentation)
                    {
                        writer.WriteStartElement("pane", Spreadsheet);
                        writer.WriteAttributeString("ySplit", "1");
                        writer.WriteAttributeString("topLeftCell", "A2");
                        writer.WriteAttributeString("activePane", "bottomLeft");
                        writer.WriteAttributeString("state", "frozen");
                        writer.WriteEndElement();
                    }
                    writer.WriteEndElement(); writer.WriteEndElement();
                    writer.WriteStartElement("cols", Spreadsheet);
                    for (int i = 0; i < table.Columns.Count; i++)
                    {
                        writer.WriteStartElement("col", Spreadsheet);
                        writer.WriteAttributeString("min", (i + 1).ToString(CultureInfo.InvariantCulture));
                        writer.WriteAttributeString("max", (i + 1).ToString(CultureInfo.InvariantCulture));
                        double width = reportPresentation ? GetReportColumnWidth(table, table.Columns[i])
                            : Math.Min(36, Math.Max(14, table.Columns[i].ColumnName.Length + 5));
                        writer.WriteAttributeString("width", width.ToString(CultureInfo.InvariantCulture));
                        if (textColumns.Contains(i)) writer.WriteAttributeString("style", "1");
                        writer.WriteAttributeString("customWidth", "1"); writer.WriteEndElement();
                    }
                    writer.WriteEndElement();
                    writer.WriteStartElement("sheetData", Spreadsheet);
                    WriteRow(writer, 1, table.Columns.Cast<DataColumn>().Select(column => (object)(reportPresentation ? column.Caption : column.ColumnName)).ToArray(), null, reportPresentation);
                    for (int i = 0; i < table.Rows.Count; i++)
                        WriteRow(writer, i + 2, table.Rows[i].ItemArray, textColumns, reportPresentation);
                    writer.WriteEndElement();
                    writer.WriteStartElement("autoFilter", Spreadsheet);
                    writer.WriteAttributeString("ref", "A1:" + ColumnName(table.Columns.Count) + (table.Rows.Count + 1).ToString(CultureInfo.InvariantCulture));
                    writer.WriteEndElement(); writer.WriteEndElement();
                }, reportPresentation);
            }
        }

        internal static int WriteCompleteFile(DataTable table, string sheetName, string target)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            if (table.Rows.Count >= MaximumCompleteExportRows) return table.Rows.Count;
            return WriteCompleteFileCore(table, sheetName, target);
        }

        internal static int WriteReportCompleteFile(DataTable table, string sheetName, string target, bool leftToRight)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            if (!string.Equals(Path.GetExtension(target), ".xlsx", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("اختر ملف Excel بامتداد xlsx.", nameof(target));
            using (DataTable snapshot = table.Copy())
            {
                snapshot.ExtendedProperties[ReportPresentationProperty] = true;
                snapshot.ExtendedProperties[RightToLeftProperty] = !leftToRight;
                return WriteCompleteFileCore(snapshot, sheetName, target);
            }
        }

        private static int WriteCompleteFileCore(DataTable table, string sheetName, string target)
        {
            if (string.IsNullOrWhiteSpace(target)) throw new ArgumentException("مسار Excel غير صالح.", nameof(target));

            string temporary = target + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    Write(table, sheetName, output);
                if (File.Exists(target)) File.Replace(temporary, target, null);
                else File.Move(temporary, target);
                return table.Rows.Count;
            }
            finally
            {
                if (File.Exists(temporary))
                {
                    try { File.Delete(temporary); }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
            }
        }

        private static HashSet<int> GetTextColumns(DataTable table)
        {
            object configured = table.ExtendedProperties[TextColumnsProperty];
            if (configured == null) return new HashSet<int>();
            int[] indexes = configured as int[];
            if (indexes == null || indexes.Any(index => index < 0 || index >= table.Columns.Count))
                throw new InvalidOperationException("أعمدة نص Excel المحددة غير صالحة.");
            return new HashSet<int>(indexes);
        }

        private static void WriteRow(XmlWriter writer, int number, object[] cells, ISet<int> textColumns = null, bool reportDates = false)
        {
            writer.WriteStartElement("row", Spreadsheet);
            writer.WriteAttributeString("r", number.ToString(CultureInfo.InvariantCulture));
            if (reportDates && number == 1)
            {
                writer.WriteAttributeString("ht", "30");
                writer.WriteAttributeString("customHeight", "1");
            }
            for (int i = 0; i < cells.Length; i++)
            {
                object value = cells[i];
                writer.WriteStartElement("c", Spreadsheet);
                writer.WriteAttributeString("r", ColumnName(i + 1) + number.ToString(CultureInfo.InvariantCulture));
                if (reportDates && number == 1) writer.WriteAttributeString("s", "2");
                else if (textColumns != null && textColumns.Contains(i)) writer.WriteAttributeString("s", "1");
                else if (reportDates && value is decimal) writer.WriteAttributeString("s", "3");
                if (value is byte || value is short || value is int || value is long || value is decimal || value is float || value is double)
                {
                    writer.WriteStartElement("v", Spreadsheet);
                    writer.WriteString(Convert.ToString(value, CultureInfo.InvariantCulture));
                    writer.WriteEndElement();
                }
                else
                {
                    string text = value == null || value == DBNull.Value ? string.Empty : value is DateTime
                        ? (reportDates ? ReportCsvWriter.DateText((DateTime)value) : ((DateTime)value).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture))
                        : Convert.ToString(value, CultureInfo.CurrentCulture) ?? string.Empty;
                    if (text.Length > 32767) throw new InvalidOperationException("قيمة نصية تتجاوز حد خلية Excel.");
                    writer.WriteAttributeString("t", "inlineStr");
                    writer.WriteStartElement("is", Spreadsheet);
                    writer.WriteStartElement("t", Spreadsheet);
                    writer.WriteAttributeString("xml", "space", "http://www.w3.org/XML/1998/namespace", "preserve");
                    writer.WriteString(text);
                    writer.WriteEndElement(); writer.WriteEndElement();
                }
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        private static string ColumnName(int number)
        {
            string name = string.Empty;
            while (number > 0) { number--; name = (char)('A' + number % 26) + name; number /= 26; }
            return name;
        }

        private static string NormalizeSheetName(string value)
        {
            string name = (value ?? string.Empty).Trim();
            foreach (char forbidden in new[] { '[', ']', ':', '*', '?', '/', '\\' }) name = name.Replace(forbidden, '-');
            if (name.Length > 31) name = name.Substring(0, 31);
            return name.Length == 0 ? "بيانات" : name;
        }

        private static void Override(XmlWriter writer, string part, string type)
        {
            writer.WriteStartElement("Override", ContentTypes); writer.WriteAttributeString("PartName", part);
            writer.WriteAttributeString("ContentType", type); writer.WriteEndElement();
        }

        private static void WriteRelationship(XmlWriter writer, string type, string target, bool includeStyles = false)
        {
            writer.WriteStartElement("Relationships", PackageRelationships);
            writer.WriteStartElement("Relationship", PackageRelationships); writer.WriteAttributeString("Id", "rId1");
            writer.WriteAttributeString("Type", type); writer.WriteAttributeString("Target", target);
            writer.WriteEndElement();
            if (includeStyles)
            {
                writer.WriteStartElement("Relationship", PackageRelationships); writer.WriteAttributeString("Id", "rId2");
                writer.WriteAttributeString("Type", Relationships + "/styles"); writer.WriteAttributeString("Target", "styles.xml");
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        private static double GetReportColumnWidth(DataTable table, DataColumn column)
        {
            int width = Math.Max(14, column.Caption.Length + 3);
            foreach (DataRow row in table.Rows)
            {
                object value = row[column];
                if (value == DBNull.Value) continue;
                string text = value is DateTime ? ReportCsvWriter.DateText((DateTime)value)
                    : value is decimal ? ((decimal)value).ToString(ReportNumberFormat, CultureInfo.InvariantCulture)
                    : Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
                width = Math.Max(width, text.Length + 3);
            }
            return Math.Min(column.DataType == typeof(string) ? 36 : 48, width);
        }

        private static void WriteReportStyles(ZipArchive archive)
        {
            WriteXml(archive, "xl/styles.xml", writer =>
            {
                writer.WriteStartElement("styleSheet", Spreadsheet);
                writer.WriteStartElement("numFmts", Spreadsheet); writer.WriteAttributeString("count", "1");
                writer.WriteStartElement("numFmt", Spreadsheet); writer.WriteAttributeString("numFmtId", "164");
                writer.WriteAttributeString("formatCode", ReportNumberFormat);
                writer.WriteEndElement(); writer.WriteEndElement();
                writer.WriteStartElement("fonts", Spreadsheet); writer.WriteAttributeString("count", "2");
                foreach (bool header in new[] { false, true })
                {
                    writer.WriteStartElement("font", Spreadsheet);
                    if (header) { writer.WriteStartElement("b", Spreadsheet); writer.WriteEndElement(); }
                    writer.WriteStartElement("sz", Spreadsheet); writer.WriteAttributeString("val", "11"); writer.WriteEndElement();
                    writer.WriteStartElement("color", Spreadsheet); writer.WriteAttributeString("rgb", header ? "FFFFFFFF" : "FF1F2F2B"); writer.WriteEndElement();
                    writer.WriteStartElement("name", Spreadsheet); writer.WriteAttributeString("val", "Arial"); writer.WriteEndElement();
                    writer.WriteEndElement();
                }
                writer.WriteEndElement();
                writer.WriteStartElement("fills", Spreadsheet); writer.WriteAttributeString("count", "3");
                foreach (string pattern in new[] { "none", "gray125", "solid" })
                {
                    writer.WriteStartElement("fill", Spreadsheet);
                    writer.WriteStartElement("patternFill", Spreadsheet); writer.WriteAttributeString("patternType", pattern);
                    if (pattern == "solid")
                    {
                        // UiTheme's light PrimaryDark keeps white report headings readable.
                        writer.WriteStartElement("fgColor", Spreadsheet); writer.WriteAttributeString("rgb", "FF1B5B4E"); writer.WriteEndElement();
                    }
                    writer.WriteEndElement(); writer.WriteEndElement();
                }
                writer.WriteEndElement();
                writer.WriteStartElement("borders", Spreadsheet); writer.WriteAttributeString("count", "1");
                writer.WriteStartElement("border", Spreadsheet); writer.WriteEndElement(); writer.WriteEndElement();
                writer.WriteStartElement("cellStyleXfs", Spreadsheet); writer.WriteAttributeString("count", "1");
                WriteReportCellStyle(writer, "0", false, false); writer.WriteEndElement();
                writer.WriteStartElement("cellXfs", Spreadsheet); writer.WriteAttributeString("count", "4");
                WriteReportCellStyle(writer, "0", false, false);
                WriteReportCellStyle(writer, "49", false, false);
                WriteReportCellStyle(writer, "0", true, false);
                WriteReportCellStyle(writer, "164", false, true);
                writer.WriteEndElement();
                writer.WriteStartElement("cellStyles", Spreadsheet); writer.WriteAttributeString("count", "1");
                writer.WriteStartElement("cellStyle", Spreadsheet); writer.WriteAttributeString("name", "Normal");
                writer.WriteAttributeString("xfId", "0"); writer.WriteAttributeString("builtinId", "0");
                writer.WriteEndElement(); writer.WriteEndElement(); writer.WriteEndElement();
            });
        }

        private static void WriteReportCellStyle(XmlWriter writer, string format, bool header, bool number)
        {
            writer.WriteStartElement("xf", Spreadsheet); writer.WriteAttributeString("numFmtId", format);
            writer.WriteAttributeString("fontId", header ? "1" : "0"); writer.WriteAttributeString("fillId", header ? "2" : "0");
            writer.WriteAttributeString("borderId", "0"); writer.WriteAttributeString("xfId", "0");
            if (format != "0") writer.WriteAttributeString("applyNumberFormat", "1");
            if (header)
            {
                writer.WriteAttributeString("applyFont", "1"); writer.WriteAttributeString("applyFill", "1");
            }
            if (header || number)
            {
                writer.WriteAttributeString("applyAlignment", "1"); writer.WriteStartElement("alignment", Spreadsheet);
                writer.WriteAttributeString("horizontal", header ? "center" : "right");
                writer.WriteAttributeString("vertical", "center");
                if (header) writer.WriteAttributeString("wrapText", "1");
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        private static void WriteTextStyles(ZipArchive archive)
        {
            WriteXml(archive, "xl/styles.xml", writer =>
            {
                writer.WriteStartElement("styleSheet", Spreadsheet);
                writer.WriteStartElement("fonts", Spreadsheet); writer.WriteAttributeString("count", "1");
                writer.WriteStartElement("font", Spreadsheet); writer.WriteEndElement(); writer.WriteEndElement();
                writer.WriteStartElement("fills", Spreadsheet); writer.WriteAttributeString("count", "2");
                foreach (string pattern in new[] { "none", "gray125" })
                {
                    writer.WriteStartElement("fill", Spreadsheet);
                    writer.WriteStartElement("patternFill", Spreadsheet); writer.WriteAttributeString("patternType", pattern);
                    writer.WriteEndElement(); writer.WriteEndElement();
                }
                writer.WriteEndElement();
                writer.WriteStartElement("borders", Spreadsheet); writer.WriteAttributeString("count", "1");
                writer.WriteStartElement("border", Spreadsheet); writer.WriteEndElement(); writer.WriteEndElement();
                writer.WriteStartElement("cellStyleXfs", Spreadsheet); writer.WriteAttributeString("count", "1");
                writer.WriteStartElement("xf", Spreadsheet); writer.WriteAttributeString("numFmtId", "0");
                writer.WriteAttributeString("fontId", "0"); writer.WriteAttributeString("fillId", "0");
                writer.WriteAttributeString("borderId", "0"); writer.WriteEndElement(); writer.WriteEndElement();
                writer.WriteStartElement("cellXfs", Spreadsheet); writer.WriteAttributeString("count", "2");
                foreach (string format in new[] { "0", "49" })
                {
                    writer.WriteStartElement("xf", Spreadsheet); writer.WriteAttributeString("numFmtId", format);
                    writer.WriteAttributeString("fontId", "0"); writer.WriteAttributeString("fillId", "0");
                    writer.WriteAttributeString("borderId", "0"); writer.WriteAttributeString("xfId", "0");
                    if (format == "49") writer.WriteAttributeString("applyNumberFormat", "1");
                    writer.WriteEndElement();
                }
                writer.WriteEndElement();
                writer.WriteStartElement("cellStyles", Spreadsheet); writer.WriteAttributeString("count", "1");
                writer.WriteStartElement("cellStyle", Spreadsheet); writer.WriteAttributeString("name", "Normal");
                writer.WriteAttributeString("xfId", "0"); writer.WriteAttributeString("builtinId", "0");
                writer.WriteEndElement(); writer.WriteEndElement();
                writer.WriteEndElement();
            });
        }

        private static void WriteXml(ZipArchive archive, string path, Action<XmlWriter> write, bool preserveLineEndings = false)
        {
            ZipArchiveEntry entry = archive.CreateEntry(path, CompressionLevel.Optimal);
            using (Stream stream = entry.Open())
            using (XmlWriter writer = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = new UTF8Encoding(false), CloseOutput = false,
                NewLineHandling = preserveLineEndings ? NewLineHandling.Entitize : NewLineHandling.Replace }))
            {
                writer.WriteStartDocument(); write(writer); writer.WriteEndDocument();
            }
        }
    }

    internal static class ExcelWorkbookSelfTests
    {
        internal static void WriteFixture(string path)
        {
            var table = Fixture();
            using (var output = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
                ExcelWorkbookWriter.Write(table, "الأدوية", output);
        }

        internal static void Run()
        {
            var table = Fixture();
            using (var stream = new MemoryStream())
            {
                ExcelWorkbookWriter.Write(table, "الأدوية", stream);
                stream.Position = 0;
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Read, true))
                {
                    if (archive.GetEntry("[Content_Types].xml") == null || archive.GetEntry("xl/workbook.xml") == null ||
                        archive.GetEntry("xl/worksheets/sheet1.xml") == null)
                        throw new InvalidOperationException("حزمة Excel لا تحتوي أجزاء المصنف الأساسية.");
                    var sheet = new XmlDocument();
                    using (Stream entry = archive.GetEntry("xl/worksheets/sheet1.xml").Open()) sheet.Load(entry);
                    var ns = new XmlNamespaceManager(sheet.NameTable);
                    ns.AddNamespace("s", "http://schemas.openxmlformats.org/spreadsheetml/2006/main");
                    XmlNode text = sheet.SelectSingleNode("//s:c[@r='A2']/s:is/s:t", ns);
                    XmlNode number = sheet.SelectSingleNode("//s:c[@r='B2']/s:v", ns);
                    XmlNode date = sheet.SelectSingleNode("//s:c[@r='C2']/s:is/s:t", ns);
                    XmlNode view = sheet.SelectSingleNode("//s:sheetView", ns);
                    if (text == null || text.InnerText != "=باراسيتامول 500 mg" ||
                        number == null || number.InnerText != "123.45" ||
                        date == null || date.InnerText != "2026-09-24 00:00:00" ||
                        view == null || view.Attributes["rightToLeft"].Value != "1" ||
                        sheet.SelectNodes("//s:f", ns).Count != 0)
                        throw new InvalidOperationException("تصدير Excel غيّر نوعًا أو نصًا أو أنشأ صيغة قابلة للتنفيذ.");
                }
                stream.Position = 0;
                DataTable imported = ExcelWorkbookReader.Read(stream);
                if (imported.Columns.Count != 3 || imported.Rows.Count != 1 ||
                    Convert.ToString(imported.Rows[0]["الصنف"]) != "=باراسيتامول 500 mg" ||
                    Convert.ToString(imported.Rows[0]["السعر"]) != "123.45" ||
                    Convert.ToString(imported.Rows[0]["التاريخ"]) != "2026-09-24 00:00:00")
                    throw new InvalidOperationException("قراءة Excel لم تُعد القيم النصية والرقمية كما كُتبت.");
            }
            VerifyInvoiceImportWorkbook();
            VerifyPostableInvoiceWorkbook();
            VerifyRejectsAmbiguousCells();
            MedicineCatalogWorkbookSelfTests.Run();
            VerifyMedicineCatalogTemplateTextStyles();
            VerifyCashBoxWorkbook();
            VerifyCompleteFileBoundary();
            VerifyExportServiceGuards();
            Console.WriteLine("Excel workbook package, Arabic roundtrip, formula isolation, medicine and cashbox templates passed.");
        }

        private static void VerifyMedicineCatalogTemplateTextStyles()
        {
            foreach (bool withRow in new[] { false, true })
            using (DataTable table = MedicineCatalogWorkbook.CreateTemplate())
            using (var stream = new MemoryStream())
            {
                if (withRow) table.Rows.Add("=صنف تجريبي 500 mg", "000123456789", "50", "100",
                    "2026-01-01", "2028-01-01", "شركة تجريبية");
                ExcelWorkbookWriter.Write(table, "الأصناف", stream);
                stream.Position = 0;
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Read, true))
                {
                    if (archive.GetEntry("xl/styles.xml") == null)
                        throw new InvalidOperationException("Catalog template lacks future-entry text protection.");
                    var styles = new XmlDocument();
                    using (Stream part = archive.GetEntry("xl/styles.xml").Open()) styles.Load(part);
                    var styleNames = new XmlNamespaceManager(styles.NameTable);
                    styleNames.AddNamespace("s", "http://schemas.openxmlformats.org/spreadsheetml/2006/main");
                    if (styles.SelectSingleNode("/s:styleSheet/s:cellXfs/s:xf[2]", styleNames)?.Attributes["numFmtId"]?.Value != "49")
                        throw new InvalidOperationException("Catalog template text style is not Excel text format49.");
                    var sheet = new XmlDocument();
                    using (Stream part = archive.GetEntry("xl/worksheets/sheet1.xml").Open()) sheet.Load(part);
                    var ns = new XmlNamespaceManager(sheet.NameTable);
                    ns.AddNamespace("s", "http://schemas.openxmlformats.org/spreadsheetml/2006/main");
                    for (int column = 1; column <= 7; column++)
                    {
                        bool text = column == 1 || column == 2 || column == 5 || column == 6 || column == 7;
                        XmlNode node = sheet.SelectSingleNode("/s:worksheet/s:cols/s:col[@min='" + column + "']", ns);
                        if (node == null || (text ? node.Attributes["style"]?.Value != "1" : node.Attributes["style"] != null))
                            throw new InvalidOperationException("Catalog template changed identifier/date or numeric-price formatting.");
                    }
                    if (sheet.SelectNodes("//s:f", ns).Count != 0)
                        throw new InvalidOperationException("Catalog template converted literal text into a formula.");
                }
                stream.Position = 0;
                using (DataTable read = ExcelWorkbookReader.Read(stream))
                {
                    if (read.Columns.Count != 7 || read.Rows.Count != (withRow ? 1 : 0))
                        throw new InvalidOperationException("Catalog template shape changed.");
                    if (withRow && (Convert.ToString(read.Rows[0][1]) != "000123456789" ||
                        Convert.ToString(read.Rows[0][4]) != "2026-01-01" || Convert.ToString(read.Rows[0][5]) != "2028-01-01" ||
                        Convert.ToString(read.Rows[0][2]) != "50" || Convert.ToString(read.Rows[0][3]) != "100"))
                        throw new InvalidOperationException("Catalog template leading-zero/date/price roundtrip changed.");
                }
            }
            Console.WriteLine("PASS catalog empty/populated template text formats; barcode/date roundtrip; numeric-price columns preserved.");
        }
        private static void VerifyCashBoxWorkbook()
        {
            Guid id = Guid.NewGuid();
            var original = new[] { new CashBoxRecord
            {
                CashBoxId = id, Code = "MAIN-01", Name = "صندوق الكاشير A",
                IsActive = true, EmployeeId = "0007"
            } };
            using (DataTable exported = CashBoxWorkbook.CreateExport(original))
            using (var stream = new MemoryStream())
            {
                ExcelWorkbookWriter.Write(exported, "CashBoxes", stream);
                VerifyCashBoxTextStyles(stream);
                stream.Position = 0;
                using (DataTable imported = ExcelWorkbookReader.Read(stream))
                {
                    var parsed = CashBoxWorkbook.Parse(imported, original);
                    if (parsed.Count != 1 || parsed[0].CashBoxId != id ||
                        parsed[0].Code != "MAIN-01" || parsed[0].Name != "صندوق الكاشير A" ||
                        parsed[0].EmployeeId != "0007" || !parsed[0].IsActive)
                        throw new InvalidOperationException("Cashbox Excel roundtrip changed identity or literal text.");
                    imported.Columns.Remove("CashBoxId");
                    if (CashBoxWorkbook.Parse(imported, original)[0].CashBoxId != id)
                        throw new InvalidOperationException("Cashbox Excel code did not resolve the existing ID.");
                    DataRow duplicate = imported.NewRow();
                    duplicate.ItemArray = (object[])imported.Rows[0].ItemArray.Clone();
                    imported.Rows.Add(duplicate);
                    bool rejected = false;
                    try { CashBoxWorkbook.Parse(imported, original); }
                    catch (InvalidOperationException) { rejected = true; }
                    if (!rejected) throw new InvalidOperationException("Duplicate cashbox import was accepted.");
                    imported.Rows.RemoveAt(1);
                    imported.Rows[0]["IsActive"] = "FALSE";
                    rejected = false;
                    try { CashBoxWorkbook.Parse(imported, original); }
                    catch (InvalidOperationException) { rejected = true; }
                    if (!rejected) throw new InvalidOperationException("Inactive cashbox kept a cashier assignment.");
                }
            }
        }

        private static void VerifyCashBoxTextStyles(MemoryStream stream)
        {
            stream.Position = 0;
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Read, true))
            {
                if (archive.GetEntry("xl/styles.xml") == null)
                    throw new InvalidOperationException("قالب الصناديق لا يحمي معرّفات الكاشير النصية.");
                var worksheet = new XmlDocument();
                using (Stream part = archive.GetEntry("xl/worksheets/sheet1.xml").Open()) worksheet.Load(part);
                var ns = new XmlNamespaceManager(worksheet.NameTable);
                ns.AddNamespace("s", "http://schemas.openxmlformats.org/spreadsheetml/2006/main");
                foreach (string column in new[] { "1", "2", "3", "4", "5" })
                {
                    XmlNode node = worksheet.SelectSingleNode("/s:worksheet/s:cols/s:col[@min='" + column + "']", ns);
                    if (node?.Attributes["style"]?.Value != "1")
                        throw new InvalidOperationException("قالب الصناديق لم ينسق عمودًا نصيًا: " + column);
                }
                if (worksheet.SelectSingleNode("//s:c[@r='E2']", ns)?.Attributes["s"]?.Value != "1")
                    throw new InvalidOperationException("رقم الكاشير في صف Excel غير محمي بتنسيق النص.");
            }
        }

        private static void VerifyInvoiceImportWorkbook()
        {
            foreach (bool english in new[] { false, true })
            {
                DataTable template = InvoiceImportWorkbook.CreateTemplate(english);
                template.Rows.Add(english ? "Sale" : "بيع", "INV-001", "1", "2026-09-29", "عميل Pharma",
                    "YER", "باراسيتامول 500 mg", "000123", "1.25", "12.345",
                    "C-001", "000000000", "2.5", "0", english ? "Inclusive" : "شامل", "مراجعة QA",
                    "2025-01-01", "2027-01-01", "B-01", "01234567890128", "SER-01", english ? "Cash" : "نقدي");
                var legacy = new DataTable();
                for (int column = 0; column < 10; column++) legacy.Columns.Add(template.Columns[column].ColumnName);
                legacy.Rows.Add(english ? "Sale" : "بيع", "OLD-001", "1", "2026-09-29", "عميل Pharma",
                    "YER", "باراسيتامول 500 mg", "000123", "1.25", "12.345");
                var legacyCandidate = InvoiceImportWorkbook.Parse(legacy, PharmacyManagement.Application.InvoiceImportMode.Draft, "Legacy")[0];
                if (legacyCandidate.PartyReference != "" || legacyCandidate.Lines[0].BatchNumber != "")
                    throw new InvalidOperationException("القالب القديم لم يُقرأ كتوافقية بمعلومات اختيارية فارغة.");
                var priorTemplate = template.Copy();
                priorTemplate.Columns.RemoveAt(21);
                if (InvoiceImportWorkbook.Parse(priorTemplate, PharmacyManagement.Application.InvoiceImportMode.Draft, "Legacy")[0].PaymentMethod != "")
                    throw new InvalidOperationException("القالب الموسع السابق لم يُقرأ مع طريقة سداد فارغة.");
                using (var stream = new MemoryStream())
                {
                    ExcelWorkbookWriter.Write(template, template.TableName, stream);
                    VerifyInvoiceTemplateTextStyles(stream);
                    stream.Position = 0;
                    DataTable read = ExcelWorkbookReader.Read(stream);
                    foreach (PharmacyManagement.Application.InvoiceImportMode mode in new[] {
                        PharmacyManagement.Application.InvoiceImportMode.Draft,
                        PharmacyManagement.Application.InvoiceImportMode.ImportAndApprove })
                    {
                        var candidates = InvoiceImportWorkbook.Parse(read, mode, "Legacy");
                        if (candidates.Count != 1 || candidates[0].Lines.Count != 1 ||
                            candidates[0].Lines[0].Quantity != 1.25m || candidates[0].Lines[0].UnitPrice != 12.345m ||
                            candidates[0].Lines[0].Barcode != "000123" || candidates[0].Mode != mode ||
                            candidates[0].PartyReference != "C-001" || candidates[0].InvoiceDiscountPercent != 2.5m ||
                            candidates[0].TaxRatePercent != 0m || candidates[0].PriceTaxMode != "Inclusive" ||
                            candidates[0].PaymentMethod != "Cash" ||
                            candidates[0].Lines[0].BatchNumber != "B-01" ||
                            candidates[0].Lines[0].ExpiryDate != new DateTime(2027, 1, 1))
                            throw new InvalidOperationException("قالب الفواتير غيّر بيانات الفاتورة أو نمط استيرادها.");
                    }
                    read.Rows[0][2] = "not-an-integer";
                    try { InvoiceImportWorkbook.Parse(read, PharmacyManagement.Application.InvoiceImportMode.Draft, "Legacy"); }
                    catch (InvalidOperationException error)
                    {
                        if (!error.Message.Contains("صف Excel 2")) throw;
                        continue;
                    }
                    throw new InvalidOperationException("رقم سطر فاتورة تالف اجتاز معاينة Excel.");
                }
            }
        }

        private static void VerifyInvoiceTemplateTextStyles(MemoryStream stream)
        {
            stream.Position = 0;
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Read, true))
            {
                ZipArchiveEntry styleEntry = archive.GetEntry("xl/styles.xml");
                if (styleEntry == null) throw new InvalidOperationException("قالب الفواتير بلا تنسيق نصي للأعمدة الحساسة.");
                var styles = new XmlDocument();
                using (Stream part = styleEntry.Open()) styles.Load(part);
                var ns = new XmlNamespaceManager(styles.NameTable);
                ns.AddNamespace("s", "http://schemas.openxmlformats.org/spreadsheetml/2006/main");
                XmlNode textFormat = styles.SelectSingleNode("/s:styleSheet/s:cellXfs/s:xf[2]", ns);
                if (textFormat?.Attributes["numFmtId"]?.Value != "49")
                    throw new InvalidOperationException("نمط أعمدة الفواتير ليس تنسيق Excel النصي.");
                var worksheet = new XmlDocument();
                using (Stream part = archive.GetEntry("xl/worksheets/sheet1.xml").Open()) worksheet.Load(part);
                var sheetNs = new XmlNamespaceManager(worksheet.NameTable);
                sheetNs.AddNamespace("s", "http://schemas.openxmlformats.org/spreadsheetml/2006/main");
                foreach (string column in new[] { "2", "4", "8", "11", "12", "17", "18", "19", "20", "21" })
                {
                    XmlNode node = worksheet.SelectSingleNode("/s:worksheet/s:cols/s:col[@min='" + column + "']", sheetNs);
                    if (node?.Attributes["style"]?.Value != "1")
                        throw new InvalidOperationException("قالب الفواتير لا يحمي عمود الهوية أو التاريخ: " + column);
                }
                foreach (string column in new[] { "3", "9", "10", "13", "14" })
                {
                    XmlNode node = worksheet.SelectSingleNode("/s:worksheet/s:cols/s:col[@min='" + column + "']", sheetNs);
                    if (node?.Attributes["style"] != null)
                        throw new InvalidOperationException("قالب الفواتير جعل عمود الحساب نصًا: " + column);
                }
                var relationships = new XmlDocument();
                using (Stream part = archive.GetEntry("xl/_rels/workbook.xml.rels").Open()) relationships.Load(part);
                if (relationships.SelectSingleNode("//*[local-name()='Relationship' and @Target='styles.xml']") == null)
                    throw new InvalidOperationException("علاقة تنسيق القالب مفقودة.");
            }
        }

        private static void VerifyPostableInvoiceWorkbook()
        {
            string path = Path.Combine(Path.GetTempPath(), "pharmacy-postable-import-" + Guid.NewGuid().ToString("N") + ".xlsx");
            DataTable table = InvoiceImportWorkbook.CreateTemplate(false);
            table.Rows.Add("بيع", "INV-001", "1", "2026-09-29", "عميل QA", "YER", "باراسيتامول 500 mg",
                "000123", "2", "100", "C-001", "000000000", "0", "0", "شامل", "مراجعة",
                "2025-01-01", "2027-01-01", "B-01", "01234567890128", "SER-01", "نقدي");
            table.Rows.Add("بيع", "inv-001", "2", "2026-09-29", "عميل QA", "YER", "دواء آخر",
                "000124", "1", "50", "C-001", "000000000", "0", "0", "شامل", "مراجعة",
                "2025-01-01", "2027-01-01", "B-02", "01234567890129", "SER-02", "نقدي");
            table.Rows.Add("شراء", "INV-001", "1", "2026-09-29", "مورد QA", "YER", "باراسيتامول 500 mg",
                "000123", "3", "40", "S-001", "000000000", "0", "0", "شامل", "مراجعة",
                "2025-01-01", "2027-01-01", "B-03", "01234567890128", "SER-03", "آجل");
            try
            {
                ExcelWorkbookWriter.WriteCompleteFile(table, table.TableName, path);
                var candidates = InvoiceImportWorkbook.Read(path, InvoiceImportMode.ImportAndApprove, "Legacy");
                if (candidates.Count != 2 || candidates[0].Lines.Count != 2 || candidates[1].Lines.Count != 1 ||
                    candidates[0].Lines[0].Barcode != "000123" || candidates[0].Lines[1].Barcode != "000124")
                    throw new InvalidOperationException("ملف الفواتير الحقيقي غيّر التجميع أو الباركود عند القراءة.");
                var actor = new OperationActor { UserName = "QA Manager", UserRole = "Manager" };
                SalesInvoiceIssueRequest sale = InvoiceImportApprovalMapping.Sale(candidates[0], actor);
                PurchaseInvoiceRequest purchase = InvoiceImportApprovalMapping.Purchase(candidates[1], actor, true);
                if (sale.Lines.Count != 2 || sale.DisplayedGrossTotal != 250m || sale.DisplayedNetTotal != 250m ||
                    sale.ImportSourceInvoiceId != "INV-001" || purchase.Lines[0].BatchNumber != "B-03" ||
                    purchase.Lines[0].Quantity != 3 || !purchase.CommercialInventoryExpected)
                    throw new InvalidOperationException("ملف الفواتير لم ينتج طلبات اعتماد بيع وشراء متطابقة.");
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }
        private static void VerifyRejectsAmbiguousCells()
        {
            foreach (string type in new[] { "e", "b" })
                ExpectInvalidWorksheet("invalid typed cell " + type, (sheet, ns) =>
                {
                    XmlNode cell = sheet.SelectSingleNode("//s:c[@r='A2']", ns);
                    cell.RemoveAll();
                    var reference = sheet.CreateAttribute("r"); reference.Value = "A2"; cell.Attributes.Append(reference);
                    var kind = sheet.CreateAttribute("t"); kind.Value = type; cell.Attributes.Append(kind);
                    XmlElement value = sheet.CreateElement("v", ns.LookupNamespace("s"));
                    value.InnerText = type == "e" ? "#N/A" : "not-a-boolean";
                    cell.AppendChild(value);
                });
            ExpectInvalidWorksheet("duplicate cell", (sheet, ns) =>
            {
                XmlNode cell = sheet.SelectSingleNode("//s:c[@r='A2']", ns);
                XmlNode duplicate = cell.CloneNode(true);
                duplicate.SelectSingleNode("s:is/s:t", ns).InnerText = "صنف آخر";
                cell.ParentNode.AppendChild(duplicate);
            });
            foreach (string reference in new[] { "A", "A0", "A2junk", "A1048577", "ZZZZZZZZZZZZZZZZZZ2" })
                ExpectInvalidWorksheet(reference, (sheet, ns) =>
                    sheet.SelectSingleNode("//s:c[@r='A2']", ns).Attributes["r"].Value = reference);
        }

        private static void ExpectInvalidWorksheet(string description, Action<XmlDocument, XmlNamespaceManager> mutate)
        {
            using (var stream = new MemoryStream())
            {
                ExcelWorkbookWriter.Write(Fixture(), "الأدوية", stream);
                stream.Position = 0;
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Update, true))
                {
                    ZipArchiveEntry entry = archive.GetEntry("xl/worksheets/sheet1.xml");
                    var sheet = new XmlDocument();
                    using (Stream part = entry.Open()) sheet.Load(part);
                    var ns = new XmlNamespaceManager(sheet.NameTable);
                    ns.AddNamespace("s", "http://schemas.openxmlformats.org/spreadsheetml/2006/main");
                    mutate(sheet, ns);
                    entry.Delete();
                    using (Stream part = archive.CreateEntry("xl/worksheets/sheet1.xml").Open()) sheet.Save(part);
                }
                stream.Position = 0;
                try { using (DataTable ignored = ExcelWorkbookReader.Read(stream)) { } }
                catch (InvalidOperationException) { return; }
                throw new InvalidOperationException("EXCEL_AMBIGUOUS_CELL_ACCEPTED: " + description);
            }
        }
        private static void VerifyCompleteFileBoundary()
        {
            string target = Path.Combine(Path.GetTempPath(), "pharmacy-excel-boundary-" + Guid.NewGuid().ToString("N") + ".xlsx");
            var table = new DataTable();
            table.Columns.Add("الصنف", typeof(string));
            for (int index = 0; index < ExcelWorkbookWriter.MaximumCompleteExportRows - 1; index++)
                table.Rows.Add("صنف " + index);
            try
            {
                int written = ExcelWorkbookWriter.WriteCompleteFile(table, "الأدوية", target);
                if (written != 1999 || ExcelWorkbookReader.Read(target).Rows.Count != 1999)
                    throw new InvalidOperationException("تصدير Excel رفض نتيجة كاملة دون حد الصفوف.");
                byte[] original = File.ReadAllBytes(target);
                table.Rows.Add("الصف 2000");
                int rejected = ExcelWorkbookWriter.WriteCompleteFile(table, "الأدوية", target);
                if (rejected != 2000 || !original.SequenceEqual(File.ReadAllBytes(target)))
                    throw new InvalidOperationException("رفض Excel عند 2000 صف غيّر الملف الموجود.");
            }
            finally
            {
                if (File.Exists(target)) File.Delete(target);
            }
        }

        private static void VerifyExportServiceGuards()
        {
            string previousUser = SessionContext.CurrentUser;
            string previousRole = SessionContext.CurrentRole;
            bool wasAuthenticated = SessionContext.IsAuthenticated;
            string target = Path.Combine(Path.GetTempPath(), "pharmacy-export-guard-" + Guid.NewGuid().ToString("N") + ".xlsx");
            try
            {
                SessionContext.Clear();
                ExpectExportRejection<UnauthorizedAccessException>(() =>
                    ExcelDataExportService.ValidateRequest("Medicines", DateTime.Today, DateTime.Today, target));
                SessionContext.Start("excel-check", "Cashier");
                ExpectExportRejection<UnauthorizedAccessException>(() =>
                    ExcelDataExportService.ValidateRequest("Medicines", DateTime.Today, DateTime.Today, target));
                SessionContext.Start("excel-check", "Manager");
                if (ExcelDataExportService.GetSources().Count != 8 ||
                    ExcelDataExportService.GetSheetName("LegacySalesLines") != "سطور البيع القديمة (دون تاريخ محفوظ)" ||
                    ExcelDataExportService.GetSources().Single(item => item.Key == "LegacySalesLines").UsesDateRange ||
                    ExcelDataTransferForm.ImportDestinationForSource("LegacySalesLines") != ExcelDataTransferForm.ImportDestination.None ||
                    ExcelDataExportService.GetSheetName("CashLedger") != "حركات النقد المشترك" ||
                    ExcelDataExportService.GetSheetName("CashBoxes") != "تعريفات صناديق النقد")
                    throw new InvalidOperationException("مصادر تصدير المدير تجاوزت الفئات المعتمدة.");
                ExcelDataExportService.ValidateRequest("Medicines", DateTime.Today, DateTime.Today, target);
                ExcelDataExportService.ValidateRequest("CashLedger", DateTime.Today, DateTime.Today, target);
                ExcelDataExportService.ValidateRequest("CashBoxes", DateTime.Today, DateTime.Today, target);
                  foreach (string source in ExcelDataExportService.GetSources().Select(item => item.Key))
                  {
                      ExcelDataExportService.ValidateRequest(source, DateTime.Today, DateTime.Today, target, new string('س', 120));
                      ExpectExportRejection<InvalidOperationException>(() =>
                          ExcelDataExportService.ValidateRequest(source, DateTime.Today, DateTime.Today, target, new string('س', 121)));
                  }
                ExpectExportRejection<InvalidOperationException>(() =>
                    ExcelDataExportService.ValidateRequest("DailyPerformance", DateTime.Today, DateTime.Today, target));
                ExpectExportRejection<InvalidOperationException>(() =>
                    ExcelDataExportService.ValidateRequest("Medicines", DateTime.Today, DateTime.Today.AddDays(-1), target));
                ExpectExportRejection<InvalidOperationException>(() =>
                    ExcelDataExportService.ValidateRequest("Medicines", DateTime.Today, DateTime.Today, target + ".txt"));
                if (File.Exists(target))
                    throw new InvalidOperationException("فحص صلاحية التصدير أنشأ ملفًا دون طلب.");
            }
            finally
            {
                if (wasAuthenticated) SessionContext.Start(previousUser, previousRole);
                else SessionContext.Clear();
            }
        }

        private static void ExpectExportRejection<TException>(Action action) where TException : Exception
        {
            try { action(); }
            catch (TException) { return; }
            throw new InvalidOperationException("تصدير Excel قبل طلبًا غير مسموح: " + typeof(TException).Name);
        }

        private static DataTable Fixture()
        {
            var table = new DataTable();
            table.Columns.Add("الصنف", typeof(string));
            table.Columns.Add("السعر", typeof(decimal));
            table.Columns.Add("التاريخ", typeof(DateTime));
            table.Rows.Add("=باراسيتامول 500 mg", 123.45m, new DateTime(2026, 9, 24));
            return table;
        }
    }
}

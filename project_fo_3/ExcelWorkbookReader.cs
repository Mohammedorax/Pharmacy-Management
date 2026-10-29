using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml;

namespace project_fo_3
{
    // Parses one worksheet into text values for preview and validation only.
    // Reading a workbook never evaluates formulas or performs a database write.
    internal static class ExcelWorkbookReader
    {
        private const string Spreadsheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private const string Relationships = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        private const int MaximumRows = 5000;
        private const int MaximumColumns = 64;
        private const long MaximumPartBytes = 20L * 1024 * 1024;

        internal static DataTable Read(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) throw new FileNotFoundException("ملف Excel غير موجود.", path);
            var info = new FileInfo(path);
            if (info.Length > 10L * 1024 * 1024) throw new InvalidOperationException("ملف Excel أكبر من الحد المسموح 10 MB.");
            using (Stream input = File.OpenRead(path)) return Read(input);
        }

        internal static DataTable Read(Stream input)
        {
            if (input == null || !input.CanRead) throw new ArgumentException("ملف Excel غير قابل للقراءة.", nameof(input));
            using (var archive = new ZipArchive(input, ZipArchiveMode.Read, true))
            {
                XmlDocument workbook = LoadPart(archive, "xl/workbook.xml");
                XmlDocument relationships = LoadPart(archive, "xl/_rels/workbook.xml.rels");
                var names = new XmlNamespaceManager(workbook.NameTable);
                names.AddNamespace("s", Spreadsheet); names.AddNamespace("r", Relationships);
                XmlNode sheet = workbook.SelectSingleNode("/s:workbook/s:sheets/s:sheet[1]", names);
                if (sheet == null) throw new InvalidOperationException("لا توجد ورقة بيانات في ملف Excel.");
                XmlAttribute relationId = sheet.Attributes["id", Relationships];
                if (relationId == null) throw new InvalidOperationException("ارتباط ورقة Excel غير موجود.");
                XmlNode relation = relationships.SelectNodes("//*[local-name()='Relationship']")
                    .Cast<XmlNode>().FirstOrDefault(node => node.Attributes["Id"]?.Value == relationId.Value);
                string target = relation?.Attributes["Target"]?.Value;
                if (string.IsNullOrWhiteSpace(target) || target.Contains("..") || target.Contains(":") || target.Contains("\\"))
                    throw new InvalidOperationException("مسار ورقة Excel غير صالح.");
                string sheetPath = target.StartsWith("/", StringComparison.Ordinal) ? target.TrimStart('/') : "xl/" + target;
                List<string> shared = ReadSharedStrings(archive);
                XmlDocument worksheet = LoadPart(archive, sheetPath);
                var sheetNames = new XmlNamespaceManager(worksheet.NameTable);
                sheetNames.AddNamespace("s", Spreadsheet);
                XmlNodeList rows = worksheet.SelectNodes("/s:worksheet/s:sheetData/s:row", sheetNames);
                if (rows == null || rows.Count == 0) throw new InvalidOperationException("ورقة Excel فارغة.");
                if (rows.Count > MaximumRows + 1) throw new InvalidOperationException("ورقة Excel تتجاوز 5,000 صف بيانات.");
                var table = new DataTable();
                string[] headers = ReadRow(rows[0], sheetNames, shared);
                if (headers.Length == 0 || headers.Length > MaximumColumns) throw new InvalidOperationException("عدد أعمدة Excel غير مدعوم.");
                for (int i = 0; i < headers.Length; i++)
                {
                    string title = headers[i].Trim();
                    if (title.Length == 0 || table.Columns.Contains(title))
                        throw new InvalidOperationException("عناوين أعمدة Excel يجب أن تكون فريدة وغير فارغة.");
                    table.Columns.Add(title, typeof(string));
                }
                for (int rowIndex = 1; rowIndex < rows.Count; rowIndex++)
                {
                    string[] values = ReadRow(rows[rowIndex], sheetNames, shared);
                    if (values.Length > headers.Length) throw new InvalidOperationException("يوجد صف يتجاوز أعمدة العنوان في Excel.");
                    if (values.All(string.IsNullOrWhiteSpace)) continue;
                    DataRow row = table.NewRow();
                    for (int i = 0; i < values.Length; i++) row[i] = values[i];
                    table.Rows.Add(row);
                }
                return table;
            }
        }

        private static string[] ReadRow(XmlNode row, XmlNamespaceManager names, IList<string> shared)
        {
            var values = new List<string>();
            var occupiedColumns = new HashSet<int>();
            XmlNodeList cells = row.SelectNodes("s:c", names);
            foreach (XmlNode cell in cells)
            {
                if (cell.SelectSingleNode("s:f", names) != null)
                    throw new InvalidOperationException("ملفات الاستيراد التي تحتوي معادلات غير مدعومة. حوّل النتائج إلى قيم ثابتة.");
                string reference = cell.Attributes["r"]?.Value ?? string.Empty;
                int index = ColumnIndex(reference);
                if (index < 0 || index >= MaximumColumns) throw new InvalidOperationException("عنوان خلية Excel غير صالح أو يتجاوز 64 عمودًا.");
                if (!occupiedColumns.Add(index))
                    throw new InvalidOperationException("يتكرر عنوان خلية داخل صف Excel. صحح الملف قبل الاستيراد: " + reference);
                while (values.Count <= index) values.Add(string.Empty);
                string type = cell.Attributes["t"]?.Value;
                string value = cell.SelectSingleNode("s:v", names)?.InnerText ?? string.Empty;
                if (type == "e")
                    throw new InvalidOperationException("تحتوي خلية Excel على خطأ. صحح الخلية قبل الاستيراد: " + reference);
                if (type == "b" && value != "0" && value != "1")
                    throw new InvalidOperationException("قيمة منطقية غير صالحة في خلية Excel: " + reference);
                if (type == "s")
                {
                    int sharedIndex;
                    if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out sharedIndex) ||
                        sharedIndex < 0 || sharedIndex >= shared.Count)
                        throw new InvalidOperationException("مرجع النص المشترك في Excel غير صالح.");
                    value = shared[sharedIndex];
                }
                else if (type == "inlineStr")
                    value = string.Concat(cell.SelectNodes("s:is//s:t", names).Cast<XmlNode>().Select(node => node.InnerText));
                else if (type == "b") value = value == "1" ? "TRUE" : "FALSE";
                values[index] = value;
            }
            return values.ToArray();
        }

        private static int ColumnIndex(string reference)
        {
            int column = 0, position = 0;
            while (position < reference.Length)
            {
                char letter = char.ToUpperInvariant(reference[position]);
                if (letter < 'A' || letter > 'Z') break;
                column = column * 26 + letter - 'A' + 1;
                if (column > MaximumColumns) return -1;
                position++;
            }
            if (position == 0 || position == reference.Length || reference[position] == '0') return -1;
            int row = 0;
            for (; position < reference.Length; position++)
            {
                char digit = reference[position];
                if (digit < '0' || digit > '9') return -1;
                row = row * 10 + digit - '0';
                if (row > 1048576) return -1;
            }
            return row > 0 ? column - 1 : -1;
        }

        private static List<string> ReadSharedStrings(ZipArchive archive)
        {
            ZipArchiveEntry entry = archive.GetEntry("xl/sharedStrings.xml");
            if (entry == null) return new List<string>();
            XmlDocument document = LoadPart(archive, entry.FullName);
            var names = new XmlNamespaceManager(document.NameTable); names.AddNamespace("s", Spreadsheet);
            return document.SelectNodes("/s:sst/s:si", names).Cast<XmlNode>()
                .Select(node => string.Concat(node.SelectNodes(".//s:t", names).Cast<XmlNode>().Select(text => text.InnerText))).ToList();
        }

        private static XmlDocument LoadPart(ZipArchive archive, string path)
        {
            ZipArchiveEntry entry = archive.GetEntry(path);
            if (entry == null || entry.Length > MaximumPartBytes)
                throw new InvalidOperationException("جزء مطلوب من ملف Excel مفقود أو كبير جدًا.");
            using (Stream stream = entry.Open())
            using (XmlReader reader = XmlReader.Create(stream, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null,
                MaxCharactersInDocument = MaximumPartBytes, CloseInput = false
            }))
            {
                var document = new XmlDocument { XmlResolver = null };
                document.Load(reader);
                return document;
            }
        }
    }
}

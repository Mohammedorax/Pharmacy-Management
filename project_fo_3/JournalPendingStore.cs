using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using PharmacyManagement.Application;
using PharmacyManagement.Domain;

namespace project_fo_3
{
    // Recovery is passive: loading never posts or changes a database.
    internal sealed class JournalPendingStore : IDisposable
    {
        private const int MaximumBytes = 1048576;
        private readonly string contextKey, sourceType, recordPath;
        private readonly byte[] entropy;
        private FileStream lease;
        internal JournalPendingStore(string directory, string contextKey, string sourceType)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("مسار الاستعادة مطلوب.");
            if (contextKey == null || !System.Text.RegularExpressions.Regex.IsMatch(contextKey, "^[a-f0-9]{64}$"))
                throw new ArgumentException("هوية سياق القيد غير صالحة.");
            if (sourceType != "Manual" && sourceType != "OpeningEntry") throw new ArgumentException("نوع القيد غير صالح.");
            this.contextKey = contextKey; this.sourceType = sourceType;
            Directory.CreateDirectory(directory);
            recordPath = Path.Combine(Path.GetFullPath(directory), contextKey + "-" + sourceType + ".pending");
            entropy = Encoding.UTF8.GetBytes("PharmacyJournalPending/v1/" + contextKey + "/" + sourceType);
            lease = new FileStream(recordPath + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        private void EnsureLease() { if (lease == null) throw new ObjectDisposedException(nameof(JournalPendingStore)); }
        private static string Money(decimal amount) => amount.ToString(CultureInfo.InvariantCulture);
        private static XElement TextField(string name, string value)
            => value == null ? new XElement(name, new XAttribute("null", "1")) : new XElement(name, value);
        private static string ReadText(XElement field)
        {
            if (field == null || field.HasElements || field.Attributes().Any(a => a.Name != "null") ||
                (field.HasAttributes && ((string)field.Attribute("null") != "1" || field.Value.Length != 0)))
                throw new InvalidDataException("بنية حقل القيد المستعاد غير صالحة.");
            return field.HasAttributes ? null : field.Value;
        }
        private static void Fields(XElement parent, params string[] names)
        {
            if (parent.Elements().Count() != names.Length || names.Any(n => parent.Elements(n).Count() != 1))
                throw new InvalidDataException("حقول القيد المستعاد مفقودة أو مكررة.");
        }
        private static decimal ReadMoney(XElement field)
        {
            decimal value;
            if (!decimal.TryParse(ReadText(field), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out value)) throw new InvalidDataException("مبلغ القيد المستعاد غير صالح.");
            return value;
        }
        internal JournalPendingRequest Load()
        {
            EnsureLease();
            byte[] encrypted;
            try
            {
                using (var stream = new FileStream(recordPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    if (stream.Length <= 0 || stream.Length > MaximumBytes + 65536) throw new InvalidDataException("حجم ملف استعادة القيد غير صالح.");
                    encrypted = new byte[(int)stream.Length];
                    int offset = 0, read;
                    while (offset < encrypted.Length && (read = stream.Read(encrypted, offset, encrypted.Length - offset)) > 0) offset += read;
                    if (offset != encrypted.Length) throw new EndOfStreamException();
                }
            }
            catch (FileNotFoundException) { return null; }
            byte[] clear = ProtectedData.Unprotect(encrypted, entropy, DataProtectionScope.CurrentUser);
            try
            {
                if (clear.Length > MaximumBytes) throw new InvalidDataException("ملف القيد يتجاوز حجم الاستعادة المسموح.");
                using (var stream = new MemoryStream(clear, false))
                using (var reader = XmlReader.Create(stream, new XmlReaderSettings {
                    DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = MaximumBytes }))
                {
                    var root = XDocument.Load(reader).Root;
                    if (root == null || root.Name != "JournalPending" || root.Attributes().Count() != 3 ||
                        ((string)root.Attribute("version") != "1" && (string)root.Attribute("version") != "2") || (string)root.Attribute("context") != contextKey ||
                        (string)root.Attribute("source") != sourceType) throw new InvalidDataException("سياق ملف القيد غير مطابق.");
                    bool hasCurrency = (string)root.Attribute("version") == "2";
                    if (hasCurrency) Fields(root, "SourceId", "Date", "Description", "Lines", "ExpectedSystemCurrencyCode");
                    else Fields(root, "SourceId", "Date", "Description", "Lines");
                    string expectedCurrency = hasCurrency ? ReadText(root.Element("ExpectedSystemCurrencyCode")) : null;
                    if (hasCurrency && string.IsNullOrWhiteSpace(expectedCurrency))
                        throw new InvalidDataException("عملة القيد المستعاد مفقودة.");
                    DateTime date;
                    if (!DateTime.TryParseExact(ReadText(root.Element("Date")), "O", CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind, out date)) throw new InvalidDataException("تاريخ القيد المستعاد غير صالح.");
                    var lines = root.Element("Lines");
                    if (lines.HasAttributes || lines.Elements().Count() > 2000 || lines.Elements().Any(e => e.Name != "Line"))
                        throw new InvalidDataException("بنية سطور القيد المستعاد غير صالحة.");
                    var values = lines.Elements().Select(line => {
                        if (line.HasAttributes) throw new InvalidDataException("سمات سطر القيد غير صالحة.");
                        Fields(line, "Account", "Debit", "Credit", "Description", "CostCenter");
                        string center = ReadText(line.Element("CostCenter")); int parsed;
                        if (center != null && !int.TryParse(center, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
                            throw new InvalidDataException("مركز تكلفة القيد المستعاد غير صالح.");
                        return new JournalLineValues(ReadText(line.Element("Account")), ReadMoney(line.Element("Debit")),
                            ReadMoney(line.Element("Credit")), ReadText(line.Element("Description")),
                            center == null ? (int?)null : int.Parse(center, CultureInfo.InvariantCulture));
                    }).ToList();
                    return JournalPendingRequest.Restore(new JournalPostRequest(date, sourceType,
                        ReadText(root.Element("SourceId")), ReadText(root.Element("Description")), values, expectedCurrency));
                }
            }
            finally { Array.Clear(clear, 0, clear.Length); }
        }
        internal void Save(JournalPendingRequest pending)
        {
            EnsureLease();
            if (pending == null) throw new ArgumentNullException(nameof(pending));
            var request = pending.Request;
            if (request.SourceType != sourceType || request.Lines.Count > 2000) throw new InvalidOperationException("طلب القيد لا يطابق ملف الاستعادة أو يتجاوز حده.");
            var existing = Load();
            if (existing != null)
            {
                if (!existing.HasSameContent(request)) throw new InvalidOperationException("يوجد قيد غير مؤكد؛ لا يمكن استبداله بطلب مختلف.");
                return;
            }
            WriteRecord(request, false);
        }
        internal void ReplaceRejected(JournalPendingRequest expected, JournalPendingRequest corrected)
        {
            EnsureLease();
            if (expected == null || corrected == null) throw new ArgumentNullException();
            var request = corrected.Request; var existing = Load();
            if (existing == null || !existing.HasSameContent(expected.Request) || request.SourceType != sourceType ||
                request.SourceId != existing.Request.SourceId || request.Lines.Count > 2000)
                throw new InvalidOperationException("تصحيح القيد لا يطابق الطلب المحفوظ أو معرّفه.");
            WriteRecord(request, true);
        }
        private void WriteRecord(JournalPostRequest request, bool replace)
        {
            var root = new XElement("JournalPending", new XAttribute("version", request.ExpectedSystemCurrencyCode == null ? "1" : "2"), new XAttribute("context", contextKey),
                new XAttribute("source", sourceType), TextField("SourceId", request.SourceId), TextField("Date", request.Date.ToString("O", CultureInfo.InvariantCulture)),
                TextField("Description", request.Description), new XElement("Lines", request.Lines.Select(line =>
                    new XElement("Line", TextField("Account", line.AccountCode), TextField("Debit", Money(line.Debit)),
                        TextField("Credit", Money(line.Credit)), TextField("Description", line.Description),
                        TextField("CostCenter", line.CostCenterId.HasValue ? line.CostCenterId.Value.ToString(CultureInfo.InvariantCulture) : null)))));
            if (request.ExpectedSystemCurrencyCode != null)
                root.Add(TextField("ExpectedSystemCurrencyCode", request.ExpectedSystemCurrencyCode));
            byte[] encrypted;
            using (var memory = new MemoryStream())
            {
                using (var writer = XmlWriter.Create(memory, new XmlWriterSettings { Encoding = new UTF8Encoding(false), CloseOutput = false,
                    NewLineHandling = NewLineHandling.Entitize })) new XDocument(root).Save(writer);
                if (memory.Length > MaximumBytes) throw new InvalidOperationException("القيد يتجاوز حجم الاستعادة المسموح.");
                byte[] clear = memory.ToArray();
                try { encrypted = ProtectedData.Protect(clear, entropy, DataProtectionScope.CurrentUser); }
                finally { Array.Clear(clear, 0, clear.Length); }
            }
            string temporary = recordPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                { stream.Write(encrypted, 0, encrypted.Length); stream.Flush(true); }
                if (replace) File.Replace(temporary, recordPath, null);
                else File.Move(temporary, recordPath);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        internal void Resolve(string sourceId)
        {
            EnsureLease(); var existing = Load();
            if (existing == null) return;
            if (existing.Request.SourceId != sourceId) throw new InvalidOperationException("نتيجة القيد لا تطابق الطلب المحفوظ.");
            File.Delete(recordPath);
        }
        public void Dispose() { if (lease != null) { lease.Dispose(); lease = null; } }
    }
}

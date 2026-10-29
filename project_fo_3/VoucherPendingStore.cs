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
    internal sealed class VoucherPendingStore : IDisposable
    {
        private const int MaximumBytes = 1048576;
        private readonly string contextKey, sourceType, recordPath;
        private readonly byte[] entropy;
        private FileStream lease;
        internal VoucherPendingStore(string directory, string contextKey)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("مسار الاستعادة مطلوب.");
            if (contextKey == null || !System.Text.RegularExpressions.Regex.IsMatch(contextKey, "^[a-f0-9]{64}$"))
                throw new ArgumentException("هوية سياق السند غير صالحة.");
            const string sourceType = "Voucher";
            this.contextKey = contextKey; this.sourceType = sourceType;
            Directory.CreateDirectory(directory);
            recordPath = Path.Combine(Path.GetFullPath(directory), contextKey + "-" + sourceType + ".pending");
            entropy = Encoding.UTF8.GetBytes("PharmacyVoucherPending/v1/" + contextKey + "/" + sourceType);
            lease = new FileStream(recordPath + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        private void EnsureLease() { if (lease == null) throw new ObjectDisposedException(nameof(VoucherPendingStore)); }
        private static string Money(decimal amount) => amount.ToString(CultureInfo.InvariantCulture);
        private static XElement TextField(string name, string value)
            => value == null ? new XElement(name, new XAttribute("null", "1")) : new XElement(name, value);
        private static string ReadText(XElement field)
        {
            if (field == null || field.HasElements || field.Attributes().Any(a => a.Name != "null") ||
                (field.HasAttributes && ((string)field.Attribute("null") != "1" || field.Value.Length != 0)))
                throw new InvalidDataException("بنية حقل السند المستعاد غير صالحة.");
            return field.HasAttributes ? null : field.Value;
        }
        private static void Fields(XElement parent, params string[] names)
        {
            if (parent.Elements().Count() != names.Length || names.Any(n => parent.Elements(n).Count() != 1))
                throw new InvalidDataException("حقول السند المستعاد مفقودة أو مكررة.");
        }
        private static decimal ReadMoney(XElement field)
        {
            decimal value;
            if (!decimal.TryParse(ReadText(field), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out value)) throw new InvalidDataException("مبلغ السند المستعاد غير صالح.");
            return value;
        }
        private static XElement WriteInputs(VoucherDraftInputs inputs)
        {
            if(inputs==null)return TextField("DraftInputs",null);
            return new XElement("DraftInputs",TextField("OriginalAmount",Money(inputs.OriginalAmount)),TextField("ExchangeRate",Money(inputs.ExchangeRate)),TextField("CurrencyCode",inputs.CurrencyCode),TextField("PaymentMethod",inputs.PaymentMethod),TextField("Notes",inputs.Notes),TextField("ChequeNumber",inputs.ChequeNumber),TextField("ChequeBank",inputs.ChequeBank),TextField("ChequeDate",inputs.ChequeDate.HasValue?inputs.ChequeDate.Value.ToString("O",CultureInfo.InvariantCulture):null),TextField("ChequeStatus",inputs.ChequeStatus),TextField("Representative",inputs.Representative),TextField("PartyName",inputs.PartyName));
        }
        private static VoucherDraftInputs ReadInputs(XElement element)
        {
            if(element==null)return null;
            if(element.HasAttributes){if(ReadText(element)==null)return null;throw new InvalidDataException("مدخلات السند الأصلية غير صالحة.");}
            Fields(element,"OriginalAmount","ExchangeRate","CurrencyCode","PaymentMethod","Notes","ChequeNumber","ChequeBank","ChequeDate","ChequeStatus","Representative","PartyName");
            DateTime date;string text=ReadText(element.Element("ChequeDate"));if(text!=null&&!DateTime.TryParseExact(text,"O",CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out date))throw new InvalidDataException("تاريخ الشيك المستعاد غير صالح.");
            return new VoucherDraftInputs(ReadMoney(element.Element("OriginalAmount")),ReadMoney(element.Element("ExchangeRate")),ReadText(element.Element("CurrencyCode")),ReadText(element.Element("PaymentMethod")),ReadText(element.Element("Notes")),ReadText(element.Element("ChequeNumber")),ReadText(element.Element("ChequeBank")),text==null?(DateTime?)null:DateTime.ParseExact(text,"O",CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind),ReadText(element.Element("ChequeStatus")),ReadText(element.Element("Representative")),ReadText(element.Element("PartyName")));
        }
        internal VoucherPendingRequest Load()
        {
            EnsureLease();
            byte[] encrypted;
            try
            {
                using (var stream = new FileStream(recordPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    if (stream.Length <= 0 || stream.Length > MaximumBytes + 65536) throw new InvalidDataException("حجم ملف استعادة السند غير صالح.");
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
                if (clear.Length > MaximumBytes) throw new InvalidDataException("ملف السند يتجاوز حجم الاستعادة المسموح.");
                using (var stream = new MemoryStream(clear, false))
                using (var reader = XmlReader.Create(stream, new XmlReaderSettings {
                    DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = MaximumBytes }))
                {
                    var root = XDocument.Load(reader).Root;
                    if (root == null || root.Name != "VoucherPending" || root.Attributes().Count() != 3 ||
                        ((string)root.Attribute("version") != "1" && (string)root.Attribute("version") != "2") || (string)root.Attribute("context") != contextKey ||
                        (string)root.Attribute("source") != sourceType) throw new InvalidDataException("سياق ملف السند غير مطابق.");
                    if((string)root.Attribute("version")=="1")Fields(root,"RequestId","Date","Type","Party","Account","Cash","Amount","Notes","CostCenter","Currency");
                    else Fields(root,"RequestId","Date","Type","Party","Account","Cash","Amount","Notes","CostCenter","Currency","DraftInputs");
                    DateTime date;Guid id;int center;
                    if(!DateTime.TryParseExact(ReadText(root.Element("Date")),"O",CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out date)||!Guid.TryParseExact(ReadText(root.Element("RequestId")),"N",out id))throw new InvalidDataException("هوية أو تاريخ السند المستعاد غير صالح.");
                    string cost=ReadText(root.Element("CostCenter"));
                    if(cost!=null&&!int.TryParse(cost,NumberStyles.Integer,CultureInfo.InvariantCulture,out center))throw new InvalidDataException("مركز تكلفة السند المستعاد غير صالح.");
                    return VoucherPendingRequest.Restore(new VoucherPostRequest(ReadText(root.Element("Type")),date,ReadText(root.Element("Party")),ReadText(root.Element("Account")),ReadMoney(root.Element("Amount")),ReadText(root.Element("Notes")),ReadText(root.Element("Cash")),cost==null?(int?)null:int.Parse(cost,CultureInfo.InvariantCulture),ReadText(root.Element("Currency")),id),ReadInputs(root.Element("DraftInputs")));
                }
            }
            finally { Array.Clear(clear, 0, clear.Length); }
        }
        internal void Save(VoucherPendingRequest pending)
        {
            EnsureLease();
            if (pending == null) throw new ArgumentNullException(nameof(pending));
            var request = pending.Request;
            VoucherPendingRequest.Restore(request,pending.DraftInputs);
            var existing = Load();
            if (existing != null)
            {
                if (!existing.HasSameState(pending)) throw new InvalidOperationException("يوجد قيد غير مؤكد؛ لا يمكن استبداله بطلب مختلف.");
                return;
            }
            WriteRecord(pending,false);
        }
        internal void ReplaceRejected(VoucherPendingRequest expected,VoucherPendingRequest corrected)
        {
            EnsureLease();if(expected==null||corrected==null)throw new ArgumentNullException();
            var existing=Load();
            if(existing==null||!existing.HasSameState(expected)||existing.Request.RequestId!=corrected.Request.RequestId)
                throw new InvalidOperationException("تصحيح السند لا يطابق الطلب المحفوظ أو معرّفه.");
            WriteRecord(corrected,true);
        }
        private void WriteRecord(VoucherPendingRequest pending,bool replace)
        {
            var request=pending.Request;
            var root=new XElement("VoucherPending",new XAttribute("version","2"),new XAttribute("context",contextKey),new XAttribute("source",sourceType),
                TextField("RequestId",request.RequestId.Value.ToString("N")),TextField("Date",request.Date.ToString("O",CultureInfo.InvariantCulture)),TextField("Type",request.Type),TextField("Party",request.PartyName),TextField("Account",request.AccountCode),TextField("Cash",request.CashAccountCode),TextField("Amount",Money(request.Amount)),TextField("Notes",request.Notes),TextField("CostCenter",request.CostCenterId.HasValue?request.CostCenterId.Value.ToString(CultureInfo.InvariantCulture):null),TextField("Currency",request.ExpectedSystemCurrencyCode),WriteInputs(pending.DraftInputs));
            byte[] encrypted;
            using (var memory = new MemoryStream())
            {
                using (var writer = XmlWriter.Create(memory, new XmlWriterSettings { Encoding = new UTF8Encoding(false), CloseOutput = false,
                    NewLineHandling = NewLineHandling.Entitize })) new XDocument(root).Save(writer);
                if (memory.Length > MaximumBytes) throw new InvalidOperationException("السند يتجاوز حجم الاستعادة المسموح.");
                byte[] clear = memory.ToArray();
                try { encrypted = ProtectedData.Protect(clear, entropy, DataProtectionScope.CurrentUser); }
                finally { Array.Clear(clear, 0, clear.Length); }
            }
            string temporary = recordPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                { stream.Write(encrypted, 0, encrypted.Length); stream.Flush(true); }
                if(replace)File.Replace(temporary,recordPath,null);else File.Move(temporary, recordPath);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        internal void Resolve(Guid requestId)
        {
            EnsureLease(); var existing = Load();
            if (existing == null) return;
            if (existing.Request.RequestId != requestId) throw new InvalidOperationException("نتيجة السند لا تطابق الطلب المحفوظ.");
            File.Delete(recordPath);
        }
        public void Dispose() { if (lease != null) { lease.Dispose(); lease = null; } }
    }
}

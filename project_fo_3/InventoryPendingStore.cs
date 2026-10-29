using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Security.Cryptography;
using System.Xml;
using System.Xml.Linq;
using PharmacyManagement.Application;

namespace project_fo_3
{
    // One lease per login/target/mode. The immutable pending record survives a process restart.
    internal sealed class InventoryPendingStore : IDisposable
    {
        private readonly string contextKey;
        private readonly InventoryPostKind kind;
        private readonly string recordPath;
        private FileStream lease;
        internal string ContextKey => contextKey;
        internal InventoryPostKind Kind => kind;
        internal static string CreateContextKey(string connectionString,string userName,string role,string employeeId)
        {
            using(var stream=new MemoryStream())
            using(var sha=SHA256.Create())
            {
                using(var writer=new BinaryWriter(stream,Encoding.UTF8,true))
                {
                    writer.Write(connectionString??string.Empty);
                    foreach(var value in new[]{userName,role,employeeId})writer.Write((value??string.Empty).Trim().ToLowerInvariant());
                }
                return BitConverter.ToString(sha.ComputeHash(stream.ToArray())).Replace("-",string.Empty).ToLowerInvariant();
            }
        }
        internal InventoryPendingStore(string directory, string contextKey, InventoryPostKind kind)
        {
            if(string.IsNullOrWhiteSpace(directory))throw new ArgumentException("مسار الاستعادة مطلوب.",nameof(directory));
            if(contextKey==null||!System.Text.RegularExpressions.Regex.IsMatch(contextKey,"^[a-f0-9]{64}$"))
                throw new ArgumentException("هوية سياق الاستعادة غير صالحة.",nameof(contextKey));
            if(!Enum.IsDefined(typeof(InventoryPostKind),kind))throw new ArgumentException("نوع الحركة غير صالح.",nameof(kind));
            this.contextKey=contextKey;this.kind=kind;
            Directory.CreateDirectory(directory);
            recordPath=Path.Combine(Path.GetFullPath(directory),contextKey+"-"+((int)kind).ToString(CultureInfo.InvariantCulture)+".xml");
            lease=new FileStream(recordPath+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        }
        private void EnsureLease(){if(lease==null)throw new ObjectDisposedException(nameof(InventoryPendingStore));}
        internal InventoryPostRequest Load()
        {
            EnsureLease();
            FileStream record;
            try{record=new FileStream(recordPath,FileMode.Open,FileAccess.Read,FileShare.Read);}
            catch(FileNotFoundException){return null;}
            using(var stream=record)
            {
                if(stream.Length>32768)throw new InvalidDataException("ملف طلب المخزون يتجاوز الحجم المسموح؛ لم يرسل طلب.");
                var settings=new XmlReaderSettings {DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=32768};
                using(var reader=XmlReader.Create(stream,settings))
                {
                    var doc=XDocument.Load(reader);var root=doc.Root;
                    if(root==null||root.Name!="InventoryPending"||(string)root.Attribute("version")!="1"||
                        (string)root.Attribute("context")!=contextKey||(string)root.Attribute("kind")!=((int)kind).ToString(CultureInfo.InvariantCulture))
                        throw new InvalidDataException("ملف استعادة الحركة لا يطابق سياقها.");
                    if(System.Linq.Enumerable.Count(root.Attributes())!=4||(string)root.Attribute("sha256")!=Digest(root))
                        throw new InvalidDataException("بصمة ملف طلب المخزون غير مطابقة؛ لم يرسل طلب.");
                    var expected=new[]{"SourceId","Medicine","Quantity","UnitAmount","Notes"};
                    if(System.Linq.Enumerable.Count(root.Elements())!=expected.Length)throw new InvalidDataException("بنية ملف الحركة غير صالحة.");
                    foreach(var name in expected)
                    {
                        if(System.Linq.Enumerable.Count(root.Elements(name))!=1)throw new InvalidDataException("حقل استعادة الحركة مفقود أو مكرر.");
                        var field=root.Element(name);
                        if(field.HasElements||field.HasAttributes)throw new InvalidDataException("بنية حقل استعادة الحركة غير صالحة.");
                    }
                    int quantity;
                    if(!int.TryParse((string)root.Element("Quantity"),NumberStyles.Integer,CultureInfo.InvariantCulture,out quantity))
                        throw new InvalidDataException("كمية الحركة المستعادة غير صالحة.");
                    decimal? amount=null;string money=(string)root.Element("UnitAmount");
                    if(!string.IsNullOrEmpty(money))
                    {
                        decimal value;
                        if(!decimal.TryParse(money,NumberStyles.AllowLeadingSign|NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out value))
                            throw new InvalidDataException("مبلغ الحركة المستعادة غير صالح.");
                        amount=value;
                    }
                    return InventoryPostService.PrepareRequest(new InventoryPostRequest(kind,(string)root.Element("SourceId"),
                        (string)root.Element("Medicine"),quantity,amount,(string)root.Element("Notes")));
                }
            }
        }
        internal void Save(InventoryPostRequest request)
        {
            EnsureLease();request=InventoryPostService.PrepareRequest(request);
            if(request.Kind!=kind)throw new InvalidOperationException("نوع الطلب لا يطابق نافذة الاستعادة.");
            var existing=Load();
            if(existing!=null)
            {
                if(existing.SourceId!=request.SourceId||!existing.HasSameContent(request))
                    throw new InvalidOperationException("يوجد طلب غير مؤكد؛ لا يمكن استبداله بحركة أخرى.");
                return;
            }
            var root=new XElement("InventoryPending",new XAttribute("version","1"),new XAttribute("context",contextKey),
                new XAttribute("kind",((int)kind).ToString(CultureInfo.InvariantCulture)),new XElement("SourceId",request.SourceId),
                new XElement("Medicine",request.MedicineName),new XElement("Quantity",request.Quantity.ToString(CultureInfo.InvariantCulture)),
                new XElement("UnitAmount",request.UnitAmount.HasValue?request.UnitAmount.Value.ToString(CultureInfo.InvariantCulture):string.Empty),
                new XElement("Notes",request.Notes));
            root.SetAttributeValue("sha256",Digest(root));
            string temporary=recordPath+"."+Guid.NewGuid().ToString("N")+".tmp";
            try
            {
                using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                {
                    WriteXml(root,stream);
                    stream.Flush(true);
                }
                File.Move(temporary,recordPath);
            }
            finally{if(File.Exists(temporary))File.Delete(temporary);}
        }
        private static void WriteXml(XElement root,Stream stream)
        {
            using(var writer=XmlWriter.Create(stream,new XmlWriterSettings {Encoding=new UTF8Encoding(false),CloseOutput=false,NewLineHandling=NewLineHandling.Entitize}))
                new XDocument(root).Save(writer);
        }
        private static string Digest(XElement root)
        {
            var copy=new XElement(root);copy.Attribute("sha256")?.Remove();
            using(var stream=new MemoryStream())
            using(var sha=SHA256.Create())
            {
                WriteXml(copy,stream);
                return BitConverter.ToString(sha.ComputeHash(stream.ToArray())).Replace("-",string.Empty).ToLowerInvariant();
            }
        }
        internal void Resolve(string sourceId)
        {
            EnsureLease();var existing=Load();if(existing==null)return;
            if(existing.SourceId!=sourceId)throw new InvalidOperationException("مرجع نتيجة الحركة لا يطابق الطلب المحفوظ.");
            File.Delete(recordPath);
        }
        public void Dispose(){if(lease!=null){lease.Dispose();lease=null;}}
    }
}

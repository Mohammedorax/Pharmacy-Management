using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using PharmacyManagement.Application;
namespace project_fo_3
{
    // Persist before sending. Restoring never submits, and always starts with an unknown outcome.
    internal sealed class CustomerMasterPendingStore : IDisposable
    {
        private const int MaximumBytes=65536;
        private readonly string source, owner, recordPath;
        private readonly byte[] entropy;
        private FileStream lease;
        internal CustomerMasterPendingStore(string directory,string sourceIdentity,string ownerUser)
        {
            if(string.IsNullOrWhiteSpace(directory)||sourceIdentity==null||
                !System.Text.RegularExpressions.Regex.IsMatch(sourceIdentity,"^[A-Fa-f0-9]{64}$")||
                string.IsNullOrWhiteSpace(ownerUser)||ownerUser.Trim().Length>200)throw new ArgumentException("سياق استعادة العميل غير صالح.");
            source=sourceIdentity;owner=ownerUser.Trim();
            string key;
            using(var hash=SHA256.Create())
                key=BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(source+"\n"+owner.ToUpperInvariant()))).Replace("-","").ToLowerInvariant();
            Directory.CreateDirectory(directory);
            recordPath=Path.Combine(Path.GetFullPath(directory),key+"-CustomerMaster.pending");
            entropy=Encoding.UTF8.GetBytes("PharmacyCustomerMasterPending/v1/"+key);
            lease=new FileStream(recordPath+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        }
        private void EnsureLease(){if(lease==null)throw new ObjectDisposedException(nameof(CustomerMasterPendingStore));}
        private void Verify(CustomerMasterWriteAttempt attempt)
        {
            if(attempt==null||attempt.SourceIdentity!=source||
                !string.Equals(attempt.Request.OwnerUser,owner,StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("المحاولة لا تطابق سياق استعادة العميل.");
        }
        private static string Field(XElement root,string name)
        {
            var element=root.Element(name);
            if(element==null||element.HasElements||element.HasAttributes)throw new InvalidDataException("حقل استعادة العميل غير صالح.");
            return element.Value;
        }
        internal CustomerMasterWriteAttempt Load()
        {
            EnsureLease();byte[] encrypted;
            try
            {
                using(var stream=new FileStream(recordPath,FileMode.Open,FileAccess.Read,FileShare.Read))
                {
                    if(stream.Length<=0||stream.Length>MaximumBytes+65536)throw new InvalidDataException("حجم استعادة العميل غير صالح.");
                    encrypted=new byte[(int)stream.Length];int offset=0,count;
                    while(offset<encrypted.Length&&(count=stream.Read(encrypted,offset,encrypted.Length-offset))>0)offset+=count;
                    if(offset!=encrypted.Length)throw new EndOfStreamException();
                }
            }
            catch(FileNotFoundException){return null;}
            byte[] clear=ProtectedData.Unprotect(encrypted,entropy,DataProtectionScope.CurrentUser);
            try
            {
                if(clear.Length>MaximumBytes)throw new InvalidDataException("حجم بيانات استعادة العميل غير صالح.");
                using(var memory=new MemoryStream(clear,false))
                using(var reader=XmlReader.Create(memory,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=MaximumBytes}))
                {
                    var root=XDocument.Load(reader).Root;
                    string[] fields={"RequestId","TargetId","Name","Phone","Address","OwnerUser","OwnerRole"};
                    if(root==null||root.Name!="CustomerMasterPending"||root.Attributes().Count()!=2||
                        (string)root.Attribute("version")!="1"||(string)root.Attribute("source")!=source||
                        root.Elements().Count()!=fields.Length||fields.Any(n=>root.Elements(n).Count()!=1))
                        throw new InvalidDataException("ملف استعادة العميل غير مطابق.");
                    Guid id;if(!Guid.TryParseExact(Field(root,"RequestId"),"N",out id))throw new InvalidDataException("معرف الاستعادة غير صالح.");
                    var request=new CustomerMasterWriteRequest(id,new SalesCustomer{CustomerId=Field(root,"TargetId"),
                        CustomerName=Field(root,"Name"),PhoneNumber=Field(root,"Phone"),Address=Field(root,"Address")},
                        new OperationActor{UserName=Field(root,"OwnerUser"),UserRole=Field(root,"OwnerRole")});
                    var attempt=new CustomerMasterWriteAttempt(request,source);Verify(attempt);
                    attempt.BeginSubmit(source,new OperationActor{UserName=request.OwnerUser,UserRole=request.OwnerRole},request.CopyCustomer());
                    attempt.ObserveUnknown();
                    return attempt;
                }
            }
            finally{Array.Clear(clear,0,clear.Length);}
        }
        internal void Save(CustomerMasterWriteAttempt attempt)
        {
            EnsureLease();Verify(attempt);
            var existing=Load();
            if(existing!=null)
            {
                if(existing.Request.RequestId!=attempt.Request.RequestId||
                    existing.Request.OwnerRole!=attempt.Request.OwnerRole||
                    !existing.Request.MatchesEditor(attempt.Request.CopyCustomer()))
                    throw new InvalidOperationException("يوجد حفظ عميل غير مؤكد؛ لا يمكن استبداله بمحاولة مختلفة.");
                return;
            }
            var r=attempt.Request;
            var root=new XElement("CustomerMasterPending",new XAttribute("version","1"),new XAttribute("source",source),
                new XElement("RequestId",r.RequestId.ToString("N")),new XElement("TargetId",r.TargetCustomerId),
                new XElement("Name",r.CustomerName),new XElement("Phone",r.PhoneNumber),new XElement("Address",r.Address),
                new XElement("OwnerUser",r.OwnerUser),new XElement("OwnerRole",r.OwnerRole));
            byte[] encrypted;
            using(var memory=new MemoryStream())
            {
                using(var writer=XmlWriter.Create(memory,new XmlWriterSettings{Encoding=new UTF8Encoding(false),CloseOutput=false,NewLineHandling=NewLineHandling.Entitize}))new XDocument(root).Save(writer);
                if(memory.Length>MaximumBytes)throw new InvalidOperationException("بيانات استعادة العميل أكبر من المسموح.");
                byte[] clear=memory.ToArray();
                try{encrypted=ProtectedData.Protect(clear,entropy,DataProtectionScope.CurrentUser);}
                finally{Array.Clear(clear,0,clear.Length);}
            }
            string temporary=Path.Combine(Path.GetDirectoryName(recordPath),Guid.NewGuid().ToString("N")+".tmp");
            try
            {
                using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None,4096,FileOptions.WriteThrough))
                {stream.Write(encrypted,0,encrypted.Length);stream.Flush(true);}
                File.Move(temporary,recordPath);
            }
            finally{if(File.Exists(temporary))File.Delete(temporary);}
        }
        internal void RemoveResolved(CustomerMasterWriteAttempt attempt)
        {
            EnsureLease();Verify(attempt);
            if(!attempt.CanDiscard)throw new InvalidOperationException("لا يمكن إزالة استعادة عميل ذي نتيجة غير محسومة.");
            var existing=Load();
            if(existing==null)return;
            if(existing.Request.RequestId!=attempt.Request.RequestId||
                !existing.Request.MatchesEditor(attempt.Request.CopyCustomer())||
                existing.Request.OwnerRole!=attempt.Request.OwnerRole)
                throw new InvalidOperationException("نتيجة العميل لا تطابق المحاولة المحفوظة.");
            File.Delete(recordPath);
        }
        public void Dispose(){if(lease!=null){lease.Dispose();lease=null;}}
    }
}


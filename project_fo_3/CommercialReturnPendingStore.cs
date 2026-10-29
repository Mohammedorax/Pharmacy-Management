using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace project_fo_3
{
    internal sealed class CommercialReturnPendingAttempt
    {
        internal readonly string Source, Owner, Role;
        internal readonly DateTime BusinessDateUtc;
        private readonly CommercialSalesReturnRequest request;
        internal CommercialReturnPendingAttempt(string source, string owner, string role, DateTime date, CommercialSalesReturnRequest value)
        {
            if (source == null || !System.Text.RegularExpressions.Regex.IsMatch(source, @"\A[A-Fa-f0-9]{64}\z") ||
                string.IsNullOrWhiteSpace(owner) || owner.Length > 200 || string.IsNullOrWhiteSpace(role) || role.Length > 100 ||
                date.Kind != DateTimeKind.Utc || value == null || value.OriginalInvoiceId == Guid.Empty ||
                string.IsNullOrWhiteSpace(value.RequestId) || value.RequestId.Length > 100 ||
                !string.Equals(value.IssuedBy, owner, StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(value.ReturnReason) || value.ReturnReason.Length > 500 || (value.Notes ?? "").Length > 1000 ||
                !CommercialReturnPresentation.RefundMethods.Any(x => x.Code == value.RefundMethod) ||
                value.Lines == null || value.Lines.Count == 0 || value.Lines.Count > 2000)
                throw new InvalidDataException("بيانات استعادة المرتجع غير صالحة.");
            var allocations = new HashSet<Guid>(); var lines = new HashSet<Guid>();
            foreach (var line in value.Lines)
            {
                if (line == null || line.OriginalLineId == Guid.Empty || !lines.Add(line.OriginalLineId) ||
                    (line.BaseQuantity.HasValue ? line.BaseQuantity.Value <= 0 || line.Quantity != 0 : line.Quantity <= 0) ||
                    decimal.Round(line.Quantity, 3) != line.Quantity || line.Lots == null || line.Lots.Count == 0)
                    throw new InvalidDataException("سطور استعادة المرتجع غير صالحة.");
                foreach (var lot in line.Lots)
                    if (lot == null || lot.OriginalSaleAllocationId == Guid.Empty || !allocations.Add(lot.OriginalSaleAllocationId) ||
                        allocations.Count > 10000 || lot.Quantity <= 0 || decimal.Round(lot.Quantity, 3) != lot.Quantity ||
                        !Enum.IsDefined(typeof(CommercialReturnDisposition), lot.Disposition))
                        throw new InvalidDataException("دفعات استعادة المرتجع غير صالحة.");
            }
            Source = source.ToUpperInvariant(); Owner = owner; Role = role; BusinessDateUtc = date; request = Copy(value);
        }
        internal CommercialSalesReturnRequest Snapshot() => Copy(request);
        private static CommercialSalesReturnRequest Copy(CommercialSalesReturnRequest value)
        {
            return new CommercialSalesReturnRequest {
                OriginalInvoiceId=value.OriginalInvoiceId, RequestId=value.RequestId, RefundMethod=value.RefundMethod,
                ReturnReason=value.ReturnReason, IssuedBy=value.IssuedBy, Notes=value.Notes,
                Lines=value.Lines.Select(line => new CommercialSalesReturnRequestLine {
                    OriginalLineId=line.OriginalLineId, Quantity=line.Quantity, BaseQuantity=line.BaseQuantity,
                    Lots=line.Lots.Select(lot => new CommercialReturnLotRequest { OriginalSaleAllocationId=lot.OriginalSaleAllocationId,
                        Quantity=lot.Quantity, Disposition=lot.Disposition }).ToArray()
                }).ToArray()
            };
        }
    }

    // Loading is passive. DPAPI protects the exact request; a lease prevents concurrent editors.
    internal sealed class CommercialReturnPendingStore : IDisposable
    {
        private const int MaximumBytes = 4194304;
        private readonly string source, owner, role, path;
        private readonly Guid invoice;
        private readonly byte[] entropy;
        private FileStream lease;
        internal string FilePath => path;
        internal CommercialReturnPendingStore(string directory, string source, string owner, string role, Guid invoice)
        {
            if (string.IsNullOrWhiteSpace(directory) || source == null ||
                !System.Text.RegularExpressions.Regex.IsMatch(source, @"\A[A-Fa-f0-9]{64}\z") ||
                string.IsNullOrWhiteSpace(owner) || owner.Length > 200 || string.IsNullOrWhiteSpace(role) || invoice == Guid.Empty)
                throw new ArgumentException("سياق استعادة المرتجع غير صالح.");
            this.source=source.ToUpperInvariant(); this.owner=owner; this.role=role; this.invoice=invoice;
            string key;
            using (var hash=SHA256.Create())
                key=BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(this.source+"\n"+owner.ToUpperInvariant()+"\n"+invoice.ToString("D")))).Replace("-","");
            Directory.CreateDirectory(directory);
            path=Path.Combine(Path.GetFullPath(directory),key+"-CommercialReturn.pending");
            entropy=Encoding.UTF8.GetBytes("PharmacyCommercialReturnPending/v1/"+key);
            lease=new FileStream(path+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        }
        private void Ensure() { if (lease==null) throw new ObjectDisposedException(nameof(CommercialReturnPendingStore)); }
        private void Verify(CommercialReturnPendingAttempt attempt)
        {
            if (attempt==null || attempt.Source!=source || !string.Equals(attempt.Owner,owner,StringComparison.OrdinalIgnoreCase) ||
                attempt.Role!=role || attempt.Snapshot().OriginalInvoiceId!=invoice)
                throw new InvalidDataException("ملف المرتجع لا يطابق المستخدم أو قاعدة التشغيل أو الفاتورة.");
        }
        private static void Text(BinaryWriter writer, string value) { writer.Write(value!=null); if(value!=null)writer.Write(value); }
        private static string Text(BinaryReader reader) => reader.ReadBoolean()?reader.ReadString():null;
        private static byte[] Exact(BinaryReader reader, int count)
        {
            var bytes=reader.ReadBytes(count); if(bytes.Length!=count)throw new EndOfStreamException(); return bytes;
        }
        private static byte[] Encode(CommercialReturnPendingAttempt attempt)
        {
            using(var memory=new MemoryStream())
            {
                using(var writer=new BinaryWriter(memory,Encoding.UTF8,true))
                {
                    writer.Write("PharmacyCommercialReturnPending/v1"); writer.Write(attempt.Source);
                    writer.Write(attempt.Owner); writer.Write(attempt.Role); writer.Write(attempt.BusinessDateUtc.ToBinary());
                    var request=attempt.Snapshot(); writer.Write(request.OriginalInvoiceId.ToByteArray());
                    writer.Write(request.RequestId); writer.Write(request.RefundMethod); writer.Write(request.ReturnReason);
                    writer.Write(request.IssuedBy); Text(writer,request.Notes); writer.Write(request.Lines.Count);
                    foreach(var line in request.Lines)
                    {
                        writer.Write(line.OriginalLineId.ToByteArray()); writer.Write(line.Quantity);
                        writer.Write(line.BaseQuantity.HasValue); if(line.BaseQuantity.HasValue)writer.Write(line.BaseQuantity.Value);
                        writer.Write(line.Lots.Count);
                        foreach(var lot in line.Lots)
                        { writer.Write(lot.OriginalSaleAllocationId.ToByteArray()); writer.Write(lot.Quantity); writer.Write((int)lot.Disposition); }
                    }
                    writer.Flush();
                }
                if(memory.Length>MaximumBytes)throw new InvalidDataException("حجم ملف استعادة المرتجع أكبر من المسموح.");
                return memory.ToArray();
            }
        }
        private static CommercialReturnPendingAttempt Decode(byte[] bytes)
        {
            using(var memory=new MemoryStream(bytes,false))
            using(var reader=new BinaryReader(memory,Encoding.UTF8))
            {
                if(reader.ReadString()!="PharmacyCommercialReturnPending/v1")throw new InvalidDataException("إصدار استعادة المرتجع غير مدعوم.");
                string source=reader.ReadString(), owner=reader.ReadString(), role=reader.ReadString();
                DateTime date=DateTime.FromBinary(reader.ReadInt64());
                var request=new CommercialSalesReturnRequest { OriginalInvoiceId=new Guid(Exact(reader,16)), RequestId=reader.ReadString(),
                    RefundMethod=reader.ReadString(), ReturnReason=reader.ReadString(), IssuedBy=reader.ReadString(), Notes=Text(reader) };
                int count=reader.ReadInt32(); if(count<1||count>2000)throw new InvalidDataException("عدد سطور الاستعادة غير صالح.");
                var lines=new List<CommercialSalesReturnRequestLine>(); int totalLots=0;
                for(int i=0;i<count;i++)
                {
                    var line=new CommercialSalesReturnRequestLine { OriginalLineId=new Guid(Exact(reader,16)), Quantity=reader.ReadDecimal() };
                    line.BaseQuantity=reader.ReadBoolean()?(int?)reader.ReadInt32():null;
                    int lots=reader.ReadInt32(); if(lots<1||lots>10000-totalLots)throw new InvalidDataException("عدد دفعات الاستعادة غير صالح.");
                    totalLots+=lots; var values=new List<CommercialReturnLotRequest>();
                    for(int n=0;n<lots;n++)values.Add(new CommercialReturnLotRequest { OriginalSaleAllocationId=new Guid(Exact(reader,16)),
                        Quantity=reader.ReadDecimal(), Disposition=(CommercialReturnDisposition)reader.ReadInt32() });
                    line.Lots=values; lines.Add(line);
                }
                request.Lines=lines;
                if(memory.Position!=memory.Length)throw new InvalidDataException("بيانات إضافية في ملف استعادة المرتجع.");
                return new CommercialReturnPendingAttempt(source,owner,role,date,request);
            }
        }
        internal CommercialReturnPendingAttempt Load()
        {
            Ensure(); byte[] encrypted;
            try
            {
                using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
                {
                    if(stream.Length<1||stream.Length>MaximumBytes+65536)throw new InvalidDataException("حجم ملف استعادة المرتجع غير صالح.");
                    encrypted=new byte[(int)stream.Length]; int offset=0,count;
                    while(offset<encrypted.Length&&(count=stream.Read(encrypted,offset,encrypted.Length-offset))>0)offset+=count;
                    if(offset!=encrypted.Length)throw new EndOfStreamException();
                }
            }
            catch(FileNotFoundException) { return null; }
            byte[] clear=ProtectedData.Unprotect(encrypted,entropy,DataProtectionScope.CurrentUser);
            try
            {
                if(clear.Length==0||clear.Length>MaximumBytes)throw new InvalidDataException("بيانات الاستعادة أكبر من المسموح.");
                var result=Decode(clear); Verify(result); return result;
            }
            finally { Array.Clear(clear,0,clear.Length); }
        }
        internal void Save(CommercialReturnPendingAttempt attempt)
        {
            Ensure(); Verify(attempt); byte[] clear=Encode(attempt);
            try
            {
                var previous=Load();
                if(previous!=null)
                {
                    if(!Encode(previous).SequenceEqual(clear))throw new InvalidOperationException("يوجد طلب مرتجع معلق؛ لا يمكن استبداله بطلب آخر.");
                    return;
                }
                byte[] encrypted=ProtectedData.Protect(clear,entropy,DataProtectionScope.CurrentUser);
                string temporary=Path.Combine(Path.GetDirectoryName(path),Guid.NewGuid().ToString("N")+".tmp");
                try
                {
                    using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None,4096,FileOptions.WriteThrough))
                    { stream.Write(encrypted,0,encrypted.Length); stream.Flush(true); }
                    File.Move(temporary,path);
                }
                finally { if(File.Exists(temporary))File.Delete(temporary); }
            }
            finally { Array.Clear(clear,0,clear.Length); }
        }
        internal void RemoveVerified(CommercialReturnPendingAttempt attempt, CommercialSalesReturnResult result)
        {
            Ensure(); Verify(attempt);
            if(result==null||result.ReturnId==Guid.Empty||result.ReturnInvoiceId==Guid.Empty||result.ReturnInvoiceNumber<=0)
                throw new InvalidOperationException("لم تُثبت نتيجة المرتجع؛ احتُفظ بطلب الاستعادة.");
            var previous=Load();
            if(previous==null||!Encode(previous).SequenceEqual(Encode(attempt)))
                throw new InvalidOperationException("نتيجة المرتجع لا تطابق الطلب المحفوظ.");
            File.Delete(path);
        }
        internal void RemoveConfirmedNotCommitted(CommercialReturnPendingAttempt attempt,CommercialReturnNotCommittedException proof)
        {
            Ensure(); Verify(attempt); var request=attempt.Snapshot();
            if(proof==null||proof.SourceIdentity!=source||proof.InvoiceId!=request.OriginalInvoiceId||proof.RequestId!=request.RequestId)
                throw new InvalidOperationException("لم يثبت تراجع محاولة المرتجع.");
            var previous=Load();
            if(previous==null||!Encode(previous).SequenceEqual(Encode(attempt)))
                throw new InvalidOperationException("إثبات التراجع لا يطابق طلب الاستعادة.");
            File.Delete(path);
        }
        public void Dispose() { var held=lease; lease=null; held?.Dispose(); }
    }
}

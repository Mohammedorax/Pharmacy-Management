using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using PharmacyManagement.Application;

namespace project_fo_3
{
    // Passive, single-owner recovery. Loading never invokes SQL or posts a request.
    internal sealed class MedicineUnitPendingStore : IDisposable
    {
        const int MaximumBytes=131072;
        readonly string contextKey,medicine,path;
        readonly byte[] entropy;
        FileStream lease;
        internal static string DefaultDirectory=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"PharmacyManagement","unit-catalog-pending");
        internal static string CreateContextKey(string connection,string user,string role,string employee,string medicine)
        {
            using(var stream=new MemoryStream())using(var sha=SHA256.Create()){
                using(var writer=new BinaryWriter(stream,Encoding.UTF8,true)){
                    writer.Write(connection??"");foreach(var identity in new[]{user,role,employee})writer.Write((identity??"").Trim().ToLowerInvariant());writer.Write(medicine??"");
                }
                return BitConverter.ToString(sha.ComputeHash(stream.ToArray())).Replace("-","").ToLowerInvariant();
            }
        }
        internal MedicineUnitPendingStore(string directory,string contextKey,string medicine)
        {
            if(string.IsNullOrWhiteSpace(directory)||contextKey==null||!System.Text.RegularExpressions.Regex.IsMatch(contextKey,"^[a-f0-9]{64}$")||string.IsNullOrWhiteSpace(medicine))throw new ArgumentException("هوية استعادة الوحدات غير صالحة.");
            this.contextKey=contextKey;this.medicine=medicine;
            Directory.CreateDirectory(directory);path=Path.Combine(Path.GetFullPath(directory),contextKey+".pending");
            entropy=Encoding.UTF8.GetBytes("PharmacyMedicineUnitPending/v1/"+contextKey);
            lease=new FileStream(path+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        }
        void EnsureLease(){if(lease==null)throw new ObjectDisposedException(nameof(MedicineUnitPendingStore));}
        static Guid GuidValue(BinaryReader reader){var bytes=reader.ReadBytes(16);if(bytes.Length!=16)throw new InvalidDataException("طلب الوحدات غير مكتمل.");return new Guid(bytes);}
        static decimal Price(BinaryReader reader){decimal value;if(!decimal.TryParse(reader.ReadString(),NumberStyles.Number,CultureInfo.InvariantCulture,out value))throw new InvalidDataException("سعر الوحدة المحفوظ غير صالح.");return value;}
        static MedicineUnitCatalogRequest Decode(Guid id,byte[] payload)
        {
            using(var stream=new MemoryStream(payload,false))using(var reader=new BinaryReader(stream,Encoding.UTF8,true)){
                if(reader.ReadString()!="Pharmacy.MedicineUnitCatalog.v1")throw new InvalidDataException("إصدار طلب الوحدات غير مدعوم.");
                var result=new MedicineUnitCatalogRequest{RequestId=id,MedicineName=reader.ReadString(),ExpectedRevision=reader.ReadInt64(),BaseUnitId=GuidValue(reader),CurrencyCode=reader.ReadString()};
                bool initial=reader.ReadBoolean();result.ExpectedInitialStockQuantity=initial?(int?)reader.ReadInt32():null;result.ConfirmInitialStockUnit=result.ExpectedRevision==0;
                int count=reader.ReadInt32();if(count<1||count>100)throw new InvalidDataException("عدد الوحدات المحفوظ غير صالح.");
                var units=new List<MedicineUnitInput>();
                for(int i=0;i<count;i++)units.Add(new MedicineUnitInput{Id=GuidValue(reader),Name=reader.ReadString(),BaseUnitsPerUnit=reader.ReadInt32(),SellingPrice=Price(reader),BuyingPrice=Price(reader)});
                result.Units=units.AsReadOnly();
                if(stream.Position!=stream.Length||!MedicineUnitCatalogValidation.Prepare(result).Payload.SequenceEqual(payload))throw new InvalidDataException("محتوى طلب الوحدات غير مطابق.");
                return result;
            }
        }
        internal MedicineUnitCatalogRequest Load()
        {
            EnsureLease();byte[] encrypted;
            try{using(var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read)){
                if(file.Length==0||file.Length>MaximumBytes)throw new InvalidDataException("حجم سجل استعادة الوحدات غير صالح.");
                encrypted=new byte[(int)file.Length];int offset=0,n;while(offset<encrypted.Length&&(n=file.Read(encrypted,offset,encrypted.Length-offset))>0)offset+=n;
                if(offset!=encrypted.Length)throw new InvalidDataException("سجل استعادة الوحدات غير مكتمل.");
            }}catch(FileNotFoundException){return null;}
            byte[] plain=ProtectedData.Unprotect(encrypted,entropy,DataProtectionScope.CurrentUser);
            if(plain.Length>MaximumBytes)throw new InvalidDataException("سجل استعادة الوحدات يتجاوز الحد المسموح.");
            using(var stream=new MemoryStream(plain,false))using(var reader=new BinaryReader(stream,Encoding.UTF8,true)){
                if(reader.ReadString()!="PharmacyMedicineUnitPending/v1"||reader.ReadString()!=contextKey)throw new InvalidDataException("سياق استعادة الوحدات غير مطابق.");
                Guid id=GuidValue(reader);int length=reader.ReadInt32();if(length<1||length>MaximumBytes||length!=stream.Length-stream.Position)throw new InvalidDataException("محتوى استعادة الوحدات غير مكتمل.");
                var result=Decode(id,reader.ReadBytes(length));
                if(!string.Equals(result.MedicineName,medicine,StringComparison.Ordinal))throw new InvalidDataException("صنف طلب الوحدات المحفوظ غير مطابق.");
                return result;
            }
        }
        internal void Save(MedicineUnitCatalogRequest request)
        {
            EnsureLease();var prepared=MedicineUnitCatalogValidation.Prepare(request);
            if(!string.Equals(prepared.MedicineName,medicine,StringComparison.Ordinal))throw new InvalidOperationException("طلب الوحدات لا يطابق صنف الاستعادة.");
            var existing=Load();
            if(existing!=null){if(existing.RequestId!=request.RequestId||!MedicineUnitCatalogValidation.Prepare(existing).Fingerprint.SequenceEqual(prepared.Fingerprint))throw new InvalidOperationException("يوجد طلب وحدات غير مؤكد؛ لا يمكن استبداله بطلب مختلف.");return;}
            byte[] plain;
            using(var stream=new MemoryStream()){
                using(var writer=new BinaryWriter(stream,Encoding.UTF8,true)){writer.Write("PharmacyMedicineUnitPending/v1");writer.Write(contextKey);writer.Write(request.RequestId.ToByteArray());var payload=prepared.Payload;writer.Write(payload.Length);writer.Write(payload);}
                plain=stream.ToArray();
            }
            var encrypted=ProtectedData.Protect(plain,entropy,DataProtectionScope.CurrentUser);
            if(encrypted.Length>MaximumBytes)throw new InvalidDataException("طلب الوحدات يتجاوز حجم الاستعادة المسموح.");
            string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            try{
                using(var file=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){file.Write(encrypted,0,encrypted.Length);file.Flush(true);}
                File.Move(temporary,path);
            }finally{if(File.Exists(temporary))File.Delete(temporary);}
        }
        internal void Resolve(MedicineUnitCatalogRequest expected)
        {
            EnsureLease();if(expected==null)throw new ArgumentNullException(nameof(expected));var existing=Load();if(existing==null)return;
            if(existing.RequestId!=expected.RequestId||!MedicineUnitCatalogValidation.Prepare(existing).Fingerprint.SequenceEqual(MedicineUnitCatalogValidation.Prepare(expected).Fingerprint))throw new InvalidOperationException("نتيجة الوحدات لا تطابق الطلب المحفوظ.");
            File.Delete(path);
        }
        public void Dispose(){var held=lease;lease=null;held?.Dispose();}
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Domain;

namespace PharmacyManagement.Application
{
    public sealed class MedicineUnitInput
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public int BaseUnitsPerUnit { get; set; }
        public decimal SellingPrice { get; set; }
        public decimal BuyingPrice { get; set; }
    }
    public sealed class MedicineUnitCatalogRequest
    {
        public Guid RequestId { get; set; }
        public string MedicineName { get; set; }
        public long ExpectedRevision { get; set; }
        public Guid BaseUnitId { get; set; }
        public string CurrencyCode { get; set; }
        public bool ConfirmInitialStockUnit { get; set; }
        public int? ExpectedInitialStockQuantity { get; set; }
        public IReadOnlyList<MedicineUnitInput> Units { get; set; }
    }
    public sealed class MedicineUnitCatalogWrite
    {
        public Guid RequestId { get; }
        public string MedicineName { get; }
        public long ExpectedRevision { get; }
        public Guid BaseUnitId { get; }
        public string CurrencyCode { get; }
        public int? ExpectedInitialStockQuantity { get; }
        public IReadOnlyList<MedicineUnitDefinition> Units { get; }
        private readonly byte[] payload, fingerprint;
        public byte[] Payload => (byte[])payload.Clone();
        public byte[] Fingerprint => (byte[])fingerprint.Clone();
        internal MedicineUnitCatalogWrite(MedicineUnitCatalogRequest request, MedicineUnitDefinition[] units)
        {
            RequestId=request.RequestId; MedicineName=request.MedicineName; ExpectedRevision=request.ExpectedRevision;
            BaseUnitId=request.BaseUnitId; CurrencyCode=units[0].CurrencyCode;
            ExpectedInitialStockQuantity=request.ExpectedInitialStockQuantity;
            Units=Array.AsReadOnly(units);
            using(var stream=new MemoryStream())
            {
                using(var writer=new BinaryWriter(stream,Encoding.UTF8,true))
                {
                    writer.Write("Pharmacy.MedicineUnitCatalog.v1"); writer.Write(MedicineName);
                    writer.Write(ExpectedRevision); writer.Write(BaseUnitId.ToByteArray()); writer.Write(CurrencyCode);
                    writer.Write(ExpectedInitialStockQuantity.HasValue);
                    if(ExpectedInitialStockQuantity.HasValue)writer.Write(ExpectedInitialStockQuantity.Value);
                    writer.Write(units.Length);
                    foreach(var unit in units)
                    {
                        writer.Write(unit.Id.ToByteArray()); writer.Write(unit.Name); writer.Write(unit.BaseUnitsPerUnit);
                        writer.Write(unit.SellingPrice.ToString("G29",CultureInfo.InvariantCulture));
                        writer.Write(unit.BuyingPrice.ToString("G29",CultureInfo.InvariantCulture));
                    }
                }
                payload=stream.ToArray();
            }
            using(var hash=SHA256.Create())fingerprint=hash.ComputeHash(payload);
        }
    }
    public static class MedicineUnitCatalogValidation
    {
        public static MedicineUnitCatalogWrite Prepare(MedicineUnitCatalogRequest request)
        {
            if(request==null)throw new ArgumentNullException(nameof(request));
            if(request.RequestId==Guid.Empty)throw new InvalidOperationException("رقم محاولة حفظ الوحدات مطلوب.");
            // This is a persisted medicine key; do not silently trim legacy names.
            if(string.IsNullOrWhiteSpace(request.MedicineName)||request.MedicineName.Length>200)
                throw new InvalidOperationException("اسم الصنف غير صالح.");
            if(request.ExpectedRevision<0||request.ExpectedRevision==long.MaxValue)
                throw new InvalidOperationException("إصدار إعدادات الوحدات غير صالح.");
            if(request.BaseUnitId==Guid.Empty)throw new InvalidOperationException("اختر الوحدة الأساسية للمخزون.");
            if(request.ExpectedRevision==0)
            {
                if(!request.ConfirmInitialStockUnit||!request.ExpectedInitialStockQuantity.HasValue||request.ExpectedInitialStockQuantity.Value<0)
                    throw new InvalidOperationException("أكد أن الرصيد الحالي مقاس بالوحدة الأساسية المختارة قبل تهيئة الوحدات.");
            }
            else if(request.ConfirmInitialStockUnit||request.ExpectedInitialStockQuantity.HasValue)
                throw new InvalidOperationException("لا يمكن إعادة تفسير رصيد المخزون عند تعديل وحدات الصنف.");
            if(request.Units==null||request.Units.Count==0||request.Units.Count>100)
                throw new InvalidOperationException("أدخل من وحدة واحدة إلى 100 وحدة للصنف.");
            var units=new List<MedicineUnitDefinition>();var ids=new HashSet<Guid>();
            var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(var input in request.Units)
            {
                if(input==null)throw new InvalidOperationException("يوجد تعريف وحدة غير مكتمل.");
                var unit=new MedicineUnitDefinition(input.Id,request.ExpectedRevision+1,input.Name,input.BaseUnitsPerUnit,
                    input.SellingPrice,input.BuyingPrice,request.CurrencyCode);
                if(!ids.Add(unit.Id)||!names.Add(unit.Name))throw new InvalidOperationException("اسم أو هوية الوحدة مكررة.");
                units.Add(unit);
            }
            var baseUnit=units.SingleOrDefault(u=>u.Id==request.BaseUnitId);
            if(baseUnit==null||baseUnit.BaseUnitsPerUnit!=1)
                throw new InvalidOperationException("الوحدة الأساسية يجب أن تكون ضمن القائمة ومعاملها 1.");
            // Copy the mutable caller before any asynchronous operation starts.
            var copy=new MedicineUnitCatalogRequest {RequestId=request.RequestId,MedicineName=request.MedicineName,
                ExpectedRevision=request.ExpectedRevision,BaseUnitId=request.BaseUnitId,
                ExpectedInitialStockQuantity=request.ExpectedInitialStockQuantity};
            return new MedicineUnitCatalogWrite(copy,units.ToArray());
        }
    }
    public sealed class MedicineUnitCatalogSnapshot
    {
        public Guid CatalogId { get; }
        public string MedicineName { get; }
        public Guid BaseUnitId { get; }
        public long Revision { get; }
        public int CurrentBaseStockQuantity { get; }
        public int ConfirmedInitialBaseStockQuantity { get; }
        public string CurrencyCode { get; }
        public IReadOnlyList<MedicineUnitDefinition> Units { get; }
        public MedicineUnitCatalogSnapshot(Guid catalogId,string medicineName,Guid baseUnitId,long revision,int currentStock,
            int confirmedStock,string currency,IEnumerable<MedicineUnitDefinition> units)
        {
            var copy=(units??throw new ArgumentNullException(nameof(units))).ToArray();
            if(catalogId==Guid.Empty||revision<=0||string.IsNullOrWhiteSpace(medicineName)||currentStock<0||confirmedStock<0||
                copy.Length==0||copy.Any(u=>u==null||u.Revision!=revision||u.CurrencyCode!=currency)||
                copy.Select(u=>u.Id).Distinct().Count()!=copy.Length||!copy.Any(u=>u.Id==baseUnitId&&u.BaseUnitsPerUnit==1))
                throw new InvalidOperationException("تعذر قراءة إعدادات وحدات الصنف بصورة متسقة.");
            CatalogId=catalogId;MedicineName=medicineName;BaseUnitId=baseUnitId;Revision=revision;
            CurrentBaseStockQuantity=currentStock;ConfirmedInitialBaseStockQuantity=confirmedStock;CurrencyCode=currency;
            Units=Array.AsReadOnly(copy);
        }
    }
    public sealed class MedicineUnitCatalogSaveResult
    {
        public Guid RequestId { get; }
        public long Revision { get; }
        public bool AlreadySaved { get; }
        public MedicineUnitCatalogSaveResult(Guid requestId,long revision,bool alreadySaved)
        {RequestId=requestId;Revision=revision;AlreadySaved=alreadySaved;}
    }
    public sealed class MedicineUnitCatalogConflictException : InvalidOperationException
    {public MedicineUnitCatalogConflictException():this("تغيرت إعدادات الوحدات؛ أعد تحميلها وراجع التعديلات قبل الحفظ.") {} public MedicineUnitCatalogConflictException(string message):base(message) {}}
    public sealed class MedicineUnitRequestConflictException : InvalidOperationException
    {public MedicineUnitRequestConflictException():base("رقم محاولة حفظ الوحدات مستخدم بمحتوى أو مستخدم مختلف؛ راجع نتيجة المحاولة الأصلية.") {}}
    public interface IMedicineUnitCatalogRepository
    {
        Task<MedicineUnitCatalogSnapshot> GetAsync(string medicineName,CancellationToken cancellationToken);
        Task<MedicineUnitCatalogSaveResult> SaveAsync(MedicineUnitCatalogWrite request,OperationActor actor,
            Action authorizeCommit,CancellationToken cancellationToken);
    }
    public sealed class MedicineUnitCatalogService
    {
        private readonly IMedicineUnitCatalogRepository repository;
        private readonly Func<OperationActor,bool> canManage,canRead;
        public MedicineUnitCatalogService(IMedicineUnitCatalogRepository repository,Func<OperationActor,bool> canManage,
            Func<OperationActor,bool> canRead=null)
        {this.repository=repository??throw new ArgumentNullException(nameof(repository));this.canManage=canManage??throw new ArgumentNullException(nameof(canManage));this.canRead=canRead??canManage;}
        private Action Authorization(OperationActor actor,bool write,out OperationActor captured)
        {
            string user=(actor?.UserName??string.Empty).Trim(),role=(actor?.UserRole??string.Empty).Trim();
            Action check=()=>{
                if(user.Length==0||user.Length>200||role.Length==0||role.Length>50||
                    !(write?canManage:canRead)(new OperationActor {UserName=user,UserRole=role}))
                    throw new UnauthorizedAccessException("لا تملك صلاحية الوصول إلى وحدات الأصناف.");
            };
            check();captured=new OperationActor {UserName=user,UserRole=role};return check;
        }
        public async Task<MedicineUnitCatalogSnapshot> GetAsync(string medicineName,OperationActor actor,CancellationToken cancellationToken)
        {
            OperationActor captured;var authorize=Authorization(actor,false,out captured);
            cancellationToken.ThrowIfCancellationRequested();
            if(string.IsNullOrWhiteSpace(medicineName)||medicineName.Length>200)throw new InvalidOperationException("اسم الصنف غير صالح.");
            var result=await repository.GetAsync(medicineName,cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();authorize();
            if(result!=null&&!string.Equals(result.MedicineName,medicineName,StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("مصدر الوحدات أعاد إعدادات صنف مختلف؛ لم تُعتمد النتيجة.");
            return result;
        }
        public async Task<MedicineUnitCatalogSaveResult> SaveAsync(MedicineUnitCatalogRequest request,OperationActor actor,CancellationToken cancellationToken)
        {
            OperationActor captured;var authorize=Authorization(actor,true,out captured);
            cancellationToken.ThrowIfCancellationRequested();var prepared=MedicineUnitCatalogValidation.Prepare(request);
            authorize();var result=await repository.SaveAsync(prepared,captured,authorize,cancellationToken).ConfigureAwait(false);
            // Once a write commits, return its receipt even if cancellation arrives afterward.
            // Authorization is checked by the repository before commit, not used to disguise success as failure.
            if(result==null||result.RequestId!=prepared.RequestId||result.Revision!=prepared.ExpectedRevision+1)
                throw new InvalidOperationException("تعذر تأكيد نتيجة حفظ وحدات الصنف؛ احتفظ برقم المحاولة وأعد الاستعلام عنها.");
            return result;
        }
    }
}

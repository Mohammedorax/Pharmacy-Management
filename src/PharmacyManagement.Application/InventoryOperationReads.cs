using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PharmacyManagement.Application
{
    public sealed class InventoryMedicineLookupRow
    {
        public InventoryMedicineLookupRow(string name,string displayName,int quantity){Name=name;DisplayName=displayName;Quantity=quantity;}
        public string Name {get;}
        public string DisplayName {get;}
        public int Quantity {get;}
    }
    public sealed class InventoryMovementReadRow
    {
        public InventoryMovementReadRow(DateTime createdAt,string medicineName,string sourceType,string sourceId,decimal delta,
            decimal? before,decimal? after,decimal? unitCost,decimal? unitPrice,string userName,string notes)
        {CreatedAt=createdAt;MedicineName=medicineName;SourceType=sourceType;SourceId=sourceId;Delta=delta;Before=before;After=after;UnitCost=unitCost;UnitPrice=unitPrice;UserName=userName;Notes=notes;}
        public DateTime CreatedAt {get;}
        public string MedicineName {get;}
        public string SourceType {get;}
        public string SourceId {get;}
        public decimal Delta {get;}
        public decimal? Before {get;}
        public decimal? After {get;}
        public decimal? UnitCost {get;}
        public decimal? UnitPrice {get;}
        public string UserName {get;}
        public string Notes {get;}
    }
    public sealed class InventoryOperationReadRequest
    {
        public InventoryOperationReadRequest(string medicineName,DateTime from,DateTime to)
        {
            MedicineName=string.IsNullOrWhiteSpace(medicineName)?string.Empty:medicineName.Trim();
            if(MedicineName.Length==0||MedicineName.Length>200)throw new InvalidOperationException("اسم الصنف مطلوب ولا يتجاوز 200 حرف.");
            From=from.Date;To=to.Date;
            if(From>To||From<new DateTime(1753,1,1)||To>=new DateTime(9999,12,31))
                throw new InvalidOperationException("نطاق تاريخ حركات المخزون غير صالح.");
        }
        public string MedicineName {get;}
        public DateTime From {get;}
        public DateTime To {get;}
    }
    public sealed class InventoryOperationReadSnapshot
    {
        public InventoryOperationReadSnapshot(string medicineName,int currentQuantity,IReadOnlyList<InventoryMovementReadRow> movements)
        {
            MedicineName=medicineName;CurrentQuantity=currentQuantity;
            if(movements==null)throw new ArgumentNullException(nameof(movements));
            Movements=new List<InventoryMovementReadRow>(movements).AsReadOnly();
        }
        public string MedicineName {get;}
        public int CurrentQuantity {get;}
        public IReadOnlyList<InventoryMovementReadRow> Movements {get;}
    }
    public interface IInventoryOperationReadRepository
    {
        Task<IReadOnlyList<InventoryMedicineLookupRow>> LoadMedicinesAsync(CancellationToken cancellation);
        Task<InventoryOperationReadSnapshot> LoadSnapshotAsync(InventoryOperationReadRequest request,CancellationToken cancellation);
    }
    public interface IInventoryOperationReadService
    {
        Task<IReadOnlyList<InventoryMedicineLookupRow>> LoadMedicinesAsync(OperationActor actor,CancellationToken cancellation);
        Task<InventoryOperationReadSnapshot> LoadSnapshotAsync(InventoryOperationReadRequest request,OperationActor actor,CancellationToken cancellation);
    }
    public sealed class InventoryOperationReadService : IInventoryOperationReadService
    {
        private readonly IInventoryOperationReadRepository repository;
        private readonly Func<OperationActor,bool> canRead;
        public InventoryOperationReadService(IInventoryOperationReadRepository repository,Func<OperationActor,bool> canRead)
        {this.repository=repository??throw new ArgumentNullException(nameof(repository));this.canRead=canRead??throw new ArgumentNullException(nameof(canRead));}
        private OperationActor Capture(OperationActor actor)
        {
            if(actor==null||string.IsNullOrWhiteSpace(actor.UserName)||string.IsNullOrWhiteSpace(actor.UserRole)||actor.UserName.Length>200||actor.UserRole.Length>50)
                throw new UnauthorizedAccessException("لا تملك صلاحية قراءة حركة المخزون.");
            return new OperationActor {UserName=actor.UserName,UserRole=actor.UserRole};
        }
        private void Authorize(OperationActor actor)
        {if(!canRead(actor))throw new UnauthorizedAccessException("لا تملك صلاحية قراءة حركة المخزون.");}
        public async Task<IReadOnlyList<InventoryMedicineLookupRow>> LoadMedicinesAsync(OperationActor actor,CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();var captured=Capture(actor);Authorize(captured);
            var rows=await repository.LoadMedicinesAsync(cancellation).ConfigureAwait(false);
            cancellation.ThrowIfCancellationRequested();Authorize(captured);
            if(rows==null)throw new InvalidOperationException("لم يرجع مصدر أصناف المخزون نتيجة.");
            return new List<InventoryMedicineLookupRow>(rows).AsReadOnly();
        }
        public async Task<InventoryOperationReadSnapshot> LoadSnapshotAsync(InventoryOperationReadRequest request,OperationActor actor,CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();var captured=Capture(actor);Authorize(captured);
            if(request==null)throw new ArgumentNullException(nameof(request));
            var result=await repository.LoadSnapshotAsync(request,cancellation).ConfigureAwait(false);
            cancellation.ThrowIfCancellationRequested();Authorize(captured);
            if(result==null||!string.Equals(result.MedicineName,request.MedicineName,StringComparison.Ordinal))
                throw new InvalidOperationException("نتيجة قراءة حركة المخزون لا تطابق الصنف المطلوب.");
            return result;
        }
    }
}

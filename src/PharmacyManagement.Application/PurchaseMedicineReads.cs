using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
namespace PharmacyManagement.Application
{
    public interface IPurchaseMedicineReadRepository
    {
        Task<IReadOnlyList<string>> GetNamesAsync(string supplier, CancellationToken token);
        Task<PurchaseMedicineSnapshot> FindAsync(string name, CancellationToken token);
    }
    public sealed class PurchaseMedicineReadService
    {
        private readonly IPurchaseMedicineReadRepository repository;
        private readonly Func<OperationActor,bool> authorized;
        public PurchaseMedicineReadService(IPurchaseMedicineReadRepository repository, Func<OperationActor,bool> authorized)
        {this.repository=repository??throw new ArgumentNullException(nameof(repository));this.authorized=authorized??throw new ArgumentNullException(nameof(authorized));}
        private OperationActor Require(OperationActor actor)
        {
            if(actor==null||string.IsNullOrWhiteSpace(actor.UserName)||string.IsNullOrWhiteSpace(actor.UserRole)||!authorized(actor))
                throw new UnauthorizedAccessException("لا تملك صلاحية قراءة أصناف المشتريات.");
            return new OperationActor{UserName=actor.UserName,UserRole=actor.UserRole};
        }
        public async Task<IReadOnlyList<string>> GetNamesAsync(string supplier, OperationActor actor, CancellationToken token)
        {
            var captured=Require(actor);token.ThrowIfCancellationRequested();
            string value=(supplier??"").Trim();
            if(value.Length>200)throw new ArgumentException("اسم المورد أطول من الحد.");
            IReadOnlyList<string> rows;
            try{rows=await repository.GetNamesAsync(value,token).ConfigureAwait(false);}
            catch{token.ThrowIfCancellationRequested();throw;}
            token.ThrowIfCancellationRequested();Require(captured);
            if(rows==null)throw new InvalidOperationException("لم يرجع مصدر الأصناف نتيجة.");
            var copy=new List<string>(rows);Require(captured);
            return copy.AsReadOnly();
        }
        public async Task<PurchaseMedicineSnapshot> FindAsync(string name, OperationActor actor, CancellationToken token)
        {
            var captured=Require(actor);token.ThrowIfCancellationRequested();
            string value=(name??"").Trim();
            if(value.Length==0||value.Length>200)throw new ArgumentException("اسم الصنف مطلوب ضمن الحد.");
            PurchaseMedicineSnapshot row;
            try{row=await repository.FindAsync(value,token).ConfigureAwait(false);}
            catch{token.ThrowIfCancellationRequested();throw;}
            token.ThrowIfCancellationRequested();Require(captured);
            if(row==null)return null;
            return new PurchaseMedicineSnapshot{MedicineName=row.MedicineName,StockQuantity=row.StockQuantity,BuyingPrice=row.BuyingPrice,SupplierName=row.SupplierName};
        }
    }
}


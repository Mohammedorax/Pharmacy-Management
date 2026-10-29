using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
namespace PharmacyManagement.Application
{
    public interface IFrequentMedicineSuggestionRepository
    {
        Task<IReadOnlyList<MedicineSearchItem>> ReadNamedAsync(IReadOnlyList<string> canonicalNames,CancellationToken token);
    }
    public sealed class FrequentMedicineSuggestionService
    {
        private readonly IFrequentMedicineSuggestionRepository repository;
        private readonly Func<OperationActor,bool> canRead;
        public FrequentMedicineSuggestionService(IFrequentMedicineSuggestionRepository repository,Func<OperationActor,bool> canRead=null)
        { this.repository=repository??throw new ArgumentNullException(nameof(repository));this.canRead=canRead??(_=>false); }
        private void Require(OperationActor actor)
        {
            if(actor==null||string.IsNullOrWhiteSpace(actor.UserName)||string.IsNullOrWhiteSpace(actor.UserRole)||!canRead(actor))
                throw new UnauthorizedAccessException("Current medicine search access is required.");
        }
        public async Task<IReadOnlyList<MedicineSearchItem>> ReadNamedAsync(IReadOnlyList<string> canonicalNames,OperationActor actor,CancellationToken token)
        {
            Require(actor);token.ThrowIfCancellationRequested();
            var captured=new OperationActor{UserName=actor.UserName,UserRole=actor.UserRole};
            if(canonicalNames==null||canonicalNames.Count>20)throw new ArgumentException("Provide at most20 canonical names.");
            var names=canonicalNames.ToArray();
            if(names.Any(x=>string.IsNullOrWhiteSpace(x)||x.Length>200)||names.Distinct(StringComparer.Ordinal).Count()!=names.Length)
                throw new ArgumentException("Canonical medicine names must be valid and unique.");
            if(names.Length==0){Require(captured);return new List<MedicineSearchItem>().AsReadOnly();}
            IReadOnlyList<MedicineSearchItem> rows;
            try { rows=await repository.ReadNamedAsync(Array.AsReadOnly(names),token).ConfigureAwait(false); }
            catch { token.ThrowIfCancellationRequested();Require(captured);throw; }
            token.ThrowIfCancellationRequested();Require(captured);
            if(rows==null||rows.Count>20)throw new InvalidOperationException("Invalid named medicine result size.");
            var result=new Dictionary<string,MedicineSearchItem>(StringComparer.Ordinal);
            foreach(var row in rows)
            {
                if(row==null||!names.Contains(row.MedName,StringComparer.Ordinal)||result.ContainsKey(row.MedName))
                    throw new InvalidOperationException("Named medicine results do not match the requested catalogue identities.");
                result.Add(row.MedName,new MedicineSearchItem{MedName=row.MedName,Barcode=row.Barcode,CompName=row.CompName,
                    MedQty=row.MedQty,Sprice=row.Sprice,ExpDate=row.ExpDate});
            }
            return names.Where(name=>result.ContainsKey(name)&&result[name].MedQty.GetValueOrDefault()>0&&result[name].Sprice.GetValueOrDefault()>0)
                .Select(name=>result[name]).ToList().AsReadOnly();
        }
    }
}

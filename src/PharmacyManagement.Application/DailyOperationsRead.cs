using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PharmacyManagement.Application
{
    public sealed class DailyOperationRecord
    {
        public DailyOperationRecord(string kind, string reference, string displayReference, DateTime date, string party, decimal amount, string employee, string status)
        { Kind=kind;Reference=reference;DisplayReference=displayReference;Date=date;Party=party;Amount=amount;Employee=employee;Status=status; }
        public string Kind { get; }
        public string Reference { get; }
        public string DisplayReference { get; }
        public DateTime Date { get; }
        public string Party { get; }
        public decimal Amount { get; }
        public string Employee { get; }
        public string Status { get; }
    }
    public sealed class DailyOperationColumn
    {
        public DailyOperationColumn(string name,Type type){Name=name;ValueType=type;}
        public string Name { get; }
        public Type ValueType { get; }
    }
    public sealed class DailyOperationDocument
    {
        public DailyOperationDocument(IEnumerable<DailyOperationColumn> columns,IEnumerable<object[]> rows)
        {
            Columns=new ReadOnlyCollection<DailyOperationColumn>(columns.ToList());
            Rows=new ReadOnlyCollection<IReadOnlyList<object>>(rows.Select(r=>(IReadOnlyList<object>)new ReadOnlyCollection<object>((object[])r.Clone())).ToList());
        }
        public IReadOnlyList<DailyOperationColumn> Columns { get; }
        public IReadOnlyList<IReadOnlyList<object>> Rows { get; }
    }
    public interface IDailyOperationsReadRepository
    {
        Task<IReadOnlyList<DailyOperationRecord>> LoadAsync(string kind,DateTime day,Action demandAccess,CancellationToken token);
        Task<DailyOperationDocument> ReadLinesAsync(string kind,string reference,Action demandAccess,CancellationToken token);
    }
    public sealed class DailyOperationsReadService
    {
        private static readonly string[] Kinds={"Sale","CommercialSale","Purchase","Voucher"};
        private readonly IDailyOperationsReadRepository repository;
        private readonly OperationActor actor;
        private readonly Func<OperationActor,string,bool> canRead;
        private readonly Func<bool> isCurrent;
        public DailyOperationsReadService(IDailyOperationsReadRepository repository,OperationActor actor,Func<OperationActor,string,bool> canRead,Func<bool> isCurrent)
        {
            this.repository=repository??throw new ArgumentNullException(nameof(repository));
            this.actor=actor==null?null:new OperationActor{UserName=actor.UserName,UserRole=actor.UserRole};
            this.canRead=canRead??throw new ArgumentNullException(nameof(canRead));this.isCurrent=isCurrent??throw new ArgumentNullException(nameof(isCurrent));
        }
        private bool Allowed(string kind)=>Kinds.Contains(kind)&&actor!=null&&!string.IsNullOrWhiteSpace(actor.UserName)&&!string.IsNullOrWhiteSpace(actor.UserRole)&&isCurrent()&&canRead(actor,kind);
        private void Require(string kind){if(!Allowed(kind))throw new UnauthorizedAccessException("تغيرت الجلسة أو صلاحية قراءة المستند.");}
        public async Task<IReadOnlyList<DailyOperationRecord>> LoadAsync(DateTime day,CancellationToken token)
        {
            if(day.Date<new DateTime(1753,1,1)||day.Date==DateTime.MaxValue.Date)throw new ArgumentOutOfRangeException(nameof(day));
            token.ThrowIfCancellationRequested();if(!isCurrent())throw new UnauthorizedAccessException("تغيرت جلسة سجل العمليات.");
            var rows=new List<DailyOperationRecord>();
            foreach(string kind in Kinds)
            {
                token.ThrowIfCancellationRequested();if(!Allowed(kind))continue;
                var result=await repository.LoadAsync(kind,day.Date,()=>Require(kind),token).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();Require(kind);rows.AddRange(result);
            }
            token.ThrowIfCancellationRequested();
            if(!isCurrent()||rows.Any(row=>!Allowed(row.Kind)))throw new UnauthorizedAccessException("تغيرت جلسة سجل العمليات.");
            return new ReadOnlyCollection<DailyOperationRecord>(rows.OrderByDescending(row=>row.Date).ThenBy(row=>row.Kind).ThenBy(row=>row.Reference).ToList());
        }
        public async Task<DailyOperationDocument> ReadLinesAsync(string kind,string reference,CancellationToken token)
        {
            token.ThrowIfCancellationRequested();Require(kind);
            if(string.IsNullOrWhiteSpace(reference)||reference.Length>50)throw new ArgumentException("مرجع المستند غير صالح.",nameof(reference));
            var result=await repository.ReadLinesAsync(kind,reference,()=>Require(kind),token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();Require(kind);return result;
        }
    }
}

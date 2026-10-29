using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using PharmacyManagement.Domain;
namespace PharmacyManagement.Application
{
    public sealed class JournalDocumentReadRequest
    {
        public JournalDocumentReadRequest(int id) { if(id<=0) throw new ArgumentOutOfRangeException(nameof(id)); Id=id; }
        public int Id { get; }
    }
    public sealed class JournalDocumentLine
    {
        public JournalDocumentLine(int id,string code,string name,decimal debit,decimal credit,string description,int? center,string centerName)
        { Id=id;AccountCode=code;AccountName=name;Debit=debit;Credit=credit;Description=description;CostCenterId=center;CostCenterName=centerName; }
        public int Id { get; }
        public string AccountCode { get; }
        public string AccountName { get; }
        public decimal Debit { get; }
        public decimal Credit { get; }
        public string Description { get; }
        public int? CostCenterId { get; }
        public string CostCenterName { get; }
    }
    public sealed class JournalDocumentSnapshot
    {
        public JournalDocumentSnapshot(int id,DateTime date,string sourceType,string sourceId,string description,string createdBy,IEnumerable<JournalDocumentLine> lines)
            : this(id,date,sourceType,sourceId,description,createdBy,lines,null) {}
        public JournalDocumentSnapshot(int id,DateTime date,string sourceType,string sourceId,string description,string createdBy,IEnumerable<JournalDocumentLine> lines,string systemCurrencyCode)
        {
            if(id<=0) throw new ArgumentOutOfRangeException(nameof(id));
            if(lines==null) throw new ArgumentNullException(nameof(lines));
            if(systemCurrencyCode!=null && (string.IsNullOrWhiteSpace(systemCurrencyCode)||systemCurrencyCode.Trim().Length>10))
                throw new ArgumentException("عملة القيد المحفوظ غير صالحة.",nameof(systemCurrencyCode));
            SystemCurrencyCode=systemCurrencyCode==null?null:systemCurrencyCode.Trim().ToUpperInvariant();
            Id=id;Date=date;SourceType=sourceType;SourceId=sourceId;Description=description;CreatedBy=createdBy;
            Lines=new List<JournalDocumentLine>(lines).AsReadOnly();
        }
        public int Id { get; }
        public DateTime Date { get; }
        public string SourceType { get; }
        public string SourceId { get; }
        public string Description { get; }
        public string CreatedBy { get; }
        public string SystemCurrencyCode { get; }
        public IReadOnlyList<JournalDocumentLine> Lines { get; }
    }
    public interface IJournalDocumentReadRepository { JournalDocumentSnapshot Load(JournalDocumentReadRequest request); Task<JournalDocumentSnapshot> LoadAsync(JournalDocumentReadRequest request,CancellationToken cancellationToken); }
    public interface IJournalDocumentReadService { Task<JournalDocumentSnapshot> LoadAsync(JournalDocumentReadRequest request,OperationActor actor); Task<JournalDocumentSnapshot> LoadAsync(JournalDocumentReadRequest request,OperationActor actor,CancellationToken cancellationToken); }
    public sealed class JournalDocumentReadService : IJournalDocumentReadService
    {
        private readonly IJournalDocumentReadRepository repository;
        private readonly Func<OperationActor,bool> canRead;
        public JournalDocumentReadService(IJournalDocumentReadRepository repository,Func<OperationActor,bool> canRead)
        {this.repository=repository??throw new ArgumentNullException(nameof(repository));this.canRead=canRead??throw new ArgumentNullException(nameof(canRead));}
        private void Authorize(OperationActor actor)
        {if(actor==null||string.IsNullOrWhiteSpace(actor.UserName)||string.IsNullOrWhiteSpace(actor.UserRole)||!canRead(actor)) throw new UnauthorizedAccessException("لا تملك صلاحية قراءة القيد المحفوظ.");}
        public Task<JournalDocumentSnapshot> LoadAsync(JournalDocumentReadRequest request,OperationActor actor)=>LoadAsync(request,actor,CancellationToken.None);
        public async Task<JournalDocumentSnapshot> LoadAsync(JournalDocumentReadRequest request,OperationActor actor,CancellationToken cancellationToken)
        {
            var captured=actor==null?null:new OperationActor {UserName=actor.UserName,UserRole=actor.UserRole};
            Authorize(captured);if(request==null)throw new ArgumentNullException(nameof(request));
            cancellationToken.ThrowIfCancellationRequested();
            var document=await repository.LoadAsync(request,cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            Authorize(captured);
            if(document==null||document.Id!=request.Id)throw new InvalidOperationException("القيد المحفوظ غير موجود أو لا يطابق طلب الطباعة.");
            if(document.Lines.Count==0||document.Lines.Any(line=>line==null||line.Id<=0)||document.Lines.Select(line=>line.Id).Distinct().Count()!=document.Lines.Count)
                throw new InvalidOperationException("سطور القيد المحفوظ غير مكتملة؛ لم تُنشأ معاينة طباعة.");
            var prepared=JournalEntryPreparation.Prepare(document.Lines.Select(line=>new JournalLineValues(line.AccountCode,line.Debit,line.Credit,line.Description,line.CostCenterId)));
            if(prepared.Count!=document.Lines.Count)throw new InvalidOperationException("القيد المحفوظ يحتوي سطورًا غير صالحة للطباعة.");
            for(int i=0;i<prepared.Count;i++)
                if(prepared[i].Debit!=document.Lines[i].Debit||prepared[i].Credit!=document.Lines[i].Credit)
                    throw new InvalidOperationException("دقة القيد المحفوظ غير متوافقة؛ لم يُعد حسابه للطباعة.");
            return document;
        }
    }
}

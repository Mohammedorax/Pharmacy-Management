using System;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Domain;

namespace PharmacyManagement.Application
{
    public sealed class VoucherPostRequest
    {
        public VoucherPostRequest(string type, DateTime date, string partyName, string accountCode, decimal amount, string notes,
            string cashAccountCode, int? costCenterId, string expectedSystemCurrencyCode = null)
            : this(type,date,partyName,accountCode,amount,notes,cashAccountCode,costCenterId,expectedSystemCurrencyCode,null) { }
        public VoucherPostRequest(string type, DateTime date, string partyName, string accountCode, decimal amount, string notes,
            string cashAccountCode, int? costCenterId, string expectedSystemCurrencyCode, Guid? requestId)
        { Type=type;Date=date;PartyName=partyName;AccountCode=accountCode;Amount=amount;Notes=notes;
          CashAccountCode=cashAccountCode;CostCenterId=costCenterId;ExpectedSystemCurrencyCode=expectedSystemCurrencyCode; RequestId=requestId; }
        public Guid? RequestId { get; }
        public string Type { get; }
        public DateTime Date { get; }
        public string PartyName { get; }
        public string AccountCode { get; }
        public decimal Amount { get; }
        public string Notes { get; }
        public string CashAccountCode { get; }
        public int? CostCenterId { get; }
        public string ExpectedSystemCurrencyCode { get; }
    }
    public sealed class VoucherPostResult
    {
        public VoucherPostResult(int voucherId, int journalEntryId) : this(voucherId,journalEntryId,false) { }
        public VoucherPostResult(int voucherId, int journalEntryId, bool alreadyPosted) { VoucherId=voucherId;JournalEntryId=journalEntryId;AlreadyPosted=alreadyPosted; }
        public bool AlreadyPosted { get; }
        public int VoucherId { get; }
        public int JournalEntryId { get; }
    }
    public interface IVoucherPostRepository
    { VoucherPostResult Post(DateTime date, PreparedVoucher voucher, OperationActor actor); }
    public interface IIdempotentVoucherPostRepository : IVoucherPostRepository
    { VoucherPostResult Post(Guid requestId, DateTime date, PreparedVoucher voucher, OperationActor actor); }
    public interface IAsyncIdempotentVoucherPostRepository : IIdempotentVoucherPostRepository
    { Task<VoucherPostResult> PostAsync(Guid requestId, DateTime date, PreparedVoucher voucher, OperationActor actor, CancellationToken token); }
    public sealed class VoucherAcceptedRequestConflictException : InvalidOperationException
    {
        public VoucherAcceptedRequestConflictException(Guid requestId, int voucherId, int journalEntryId)
            : base("السند معتمد سابقًا ببيانات مختلفة؛ لم تُعتمد التعديلات الحالية.")
        { RequestId=requestId;VoucherId=voucherId;JournalEntryId=journalEntryId; }
        public Guid RequestId { get; }
        public int VoucherId { get; }
        public int JournalEntryId { get; }
    }
    public interface IVoucherPostService
    { VoucherPostResult Post(VoucherPostRequest request, OperationActor actor); }
    public interface IAsyncVoucherPostService : IVoucherPostService
    { Task<VoucherPostResult> PostAsync(VoucherPostRequest request, OperationActor actor, CancellationToken token); }
    public sealed class VoucherPostService : IAsyncVoucherPostService
    {
        private readonly IVoucherPostRepository repository;
        private readonly Func<OperationActor,bool> canPost;
        public VoucherPostService(IVoucherPostRepository repository, Func<OperationActor,bool> canPost)
        { this.repository=repository??throw new ArgumentNullException(nameof(repository));this.canPost=canPost??throw new ArgumentNullException(nameof(canPost)); }
        private void Authorize(OperationActor actor)
        {
            if(actor==null||string.IsNullOrWhiteSpace(actor.UserName)||string.IsNullOrWhiteSpace(actor.UserRole)||!canPost(actor))
                throw new UnauthorizedAccessException("لا تملك صلاحية اعتماد السندات المحاسبية.");
        }
        private static void Length(string value,int max,string field)
        { if(value!=null && value.Trim().Length>max)throw new InvalidOperationException(field+" يتجاوز الحد المسموح."); }
        public static PreparedVoucher PrepareRequest(VoucherPostRequest request)
        {
            if(request==null)throw new ArgumentNullException(nameof(request));
            if(request.Date<new DateTime(1753,1,1)||request.Date>new DateTime(9999,12,31,23,59,59,997))
                throw new InvalidOperationException("تاريخ السند خارج نطاق التخزين المحاسبي.");
            var voucher=VoucherPreparation.Prepare(request.Type,request.PartyName,request.AccountCode,request.CashAccountCode,
                request.Amount,request.Notes,request.CostCenterId,request.ExpectedSystemCurrencyCode);
            Length(voucher.PartyName,200,"اسم الطرف");Length(voucher.AccountCode,20,"الحساب المقابل");
            Length(voucher.CashAccountCode,20,"حساب الصندوق");Length(voucher.Notes,500,"ملاحظات السند");Length(voucher.Description,500,"بيان القيد");
            if(voucher.Amount>9999999999999999.99m)throw new InvalidOperationException("قيمة السند تتجاوز الحد المسموح.");
            if(request.RequestId.HasValue && (request.RequestId.Value==Guid.Empty || voucher.ExpectedSystemCurrencyCode==null))
                throw new InvalidOperationException("هوية السند أو لقطة عملته غير صالحة.");
            return voucher;
        }
        private PreparedVoucher PrepareAuthorizedRequest(VoucherPostRequest request,OperationActor actor,out OperationActor snapshot)
        {
            Authorize(actor);
            snapshot=new OperationActor {UserName=actor.UserName.Trim(),UserRole=actor.UserRole.Trim()};
            Length(snapshot.UserName,200,"المستخدم");Length(snapshot.UserRole,50,"الدور");
            var voucher=PrepareRequest(request);
            Authorize(snapshot);
            return voucher;
        }
        public async Task<VoucherPostResult> PostAsync(VoucherPostRequest request,OperationActor actor,CancellationToken token)
        {
            OperationActor snapshot;var voucher=PrepareAuthorizedRequest(request,actor,out snapshot);
            token.ThrowIfCancellationRequested();
            if(!request.RequestId.HasValue)throw new InvalidOperationException("Native async voucher posting requires a durable request identity.");
            var asynchronous=repository as IAsyncIdempotentVoucherPostRepository;
            if(asynchronous==null)throw new InvalidOperationException("Voucher repository does not support native async posting; no write was sent.");
            var result=await asynchronous.PostAsync(request.RequestId.Value,request.Date,voucher,snapshot,token).ConfigureAwait(false);
            // Known commit survives cancellation/permission changes after acceptance.
            if(result==null||result.VoucherId<=0||result.JournalEntryId<=0)throw new InvalidOperationException("لم يُعد اعتماد السند معرفات صحيحة.");
            return result;
        }
        public VoucherPostResult Post(VoucherPostRequest request, OperationActor actor)
        {
            OperationActor snapshot;var voucher=PrepareAuthorizedRequest(request,actor,out snapshot);
            VoucherPostResult result;
            if(request.RequestId.HasValue)
            {
                if(request.RequestId.Value==Guid.Empty)throw new InvalidOperationException("معرف طلب السند غير صالح.");
                if(voucher.ExpectedSystemCurrencyCode==null)throw new InvalidOperationException("يتطلب طلب السند الثابت لقطة عملة المسودة.");
                var durable=repository as IIdempotentVoucherPostRepository;
                if(durable==null)throw new InvalidOperationException("مستودع السندات لا يدعم هوية الطلب الدائمة؛ لم يُرسل الحفظ.");
                result=durable.Post(request.RequestId.Value,request.Date,voucher,snapshot);
            }
            else result=repository.Post(request.Date,voucher,snapshot);
            // A permission change after a known commit must not turn acceptance into a retryable failure.
            if(result==null||result.VoucherId<=0||result.JournalEntryId<=0)throw new InvalidOperationException("لم يُعد اعتماد السند معرفات صحيحة.");
            return result;
        }
    }
}

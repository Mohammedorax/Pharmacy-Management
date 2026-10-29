using System;
using System.Threading;
using System.Threading.Tasks;
namespace PharmacyManagement.Application
{
    public sealed class VoucherSubmissionStatusRequest
    {
        public VoucherSubmissionStatusRequest(VoucherPostRequest expectedRequest) { ExpectedRequest=expectedRequest; }
        public VoucherPostRequest ExpectedRequest { get; }
    }
    public sealed class VoucherSubmissionStatus
    {
        public VoucherSubmissionStatus(int voucherId,int journalEntryId,bool? contentMatches=null)
        {
            if(voucherId<0||journalEntryId<0||(voucherId==0)!=(journalEntryId==0)||voucherId==0&&contentMatches.HasValue)
                throw new ArgumentException("نتيجة حالة السند غير صالحة.");
            VoucherId=voucherId;JournalEntryId=journalEntryId;ContentMatches=contentMatches;
        }
        public int VoucherId { get; }
        public int JournalEntryId { get; }
        public bool IsPosted => VoucherId>0;
        public bool? ContentMatches { get; }
    }
    public interface IVoucherSubmissionStatusRepository
    { Task<VoucherSubmissionStatus> FindAsync(VoucherSubmissionStatusRequest request,OperationActor actor,CancellationToken token); }
    public sealed class VoucherSubmissionStatusService
    {
        private readonly IVoucherSubmissionStatusRepository repository;
        private readonly Func<OperationActor,bool> canRead;
        public VoucherSubmissionStatusService(IVoucherSubmissionStatusRepository repository,Func<OperationActor,bool> canRead=null)
        {this.repository=repository??throw new ArgumentNullException(nameof(repository));this.canRead=canRead??(a=>false);}
        private void Authorize(OperationActor actor)
        {if(actor==null||string.IsNullOrWhiteSpace(actor.UserName)||string.IsNullOrWhiteSpace(actor.UserRole)||!canRead(actor))throw new UnauthorizedAccessException("لا تملك صلاحية التحقق من اعتماد السند.");}
        public static VoucherSubmissionStatusRequest PrepareRequest(VoucherSubmissionStatusRequest request)
        {
            if(request==null)throw new ArgumentNullException(nameof(request));
            var expected=request.ExpectedRequest;
            if(expected==null||!expected.RequestId.HasValue)throw new InvalidOperationException("يلزم طلب سند ثابت للمقارنة.");
            VoucherPostService.PrepareRequest(expected);
            return new VoucherSubmissionStatusRequest(expected);
        }
        public async Task<VoucherSubmissionStatus> FindAsync(VoucherSubmissionStatusRequest request,OperationActor actor,CancellationToken token)
        {
            token.ThrowIfCancellationRequested();Authorize(actor);
            var captured=new OperationActor{UserName=actor.UserName.Trim(),UserRole=actor.UserRole.Trim()};
            var prepared=PrepareRequest(request);Authorize(captured);
            var result=await repository.FindAsync(prepared,captured,token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();Authorize(captured);
            if(result==null||result.IsPosted&&!result.ContentMatches.HasValue)throw new InvalidOperationException("لم تثبت قراءة حالة السند مقارنة المحتوى.");
            return result;
        }
    }
}
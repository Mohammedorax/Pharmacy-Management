using System;
using System.Threading.Tasks;
using System.Threading;

namespace PharmacyManagement.Application
{
    public sealed class VoucherDocumentReadRequest
    {
        public VoucherDocumentReadRequest(int voucherId)
        {
            if (voucherId <= 0) throw new ArgumentOutOfRangeException(nameof(voucherId));
            VoucherId = voucherId;
        }
        public int VoucherId { get; }
    }

    public sealed class VoucherDocumentSnapshot
    {
        public VoucherDocumentSnapshot(int voucherId, DateTime date, string type, string partyName,
            string accountCode, decimal amount, string notes, int? journalEntryId, string createdBy)
            : this(voucherId,date,type,partyName,accountCode,amount,notes,journalEntryId,createdBy,null) {}
        public VoucherDocumentSnapshot(int voucherId, DateTime date, string type, string partyName,
            string accountCode, decimal amount, string notes, int? journalEntryId, string createdBy,string systemCurrencyCode)
        {
            if (voucherId <= 0) throw new ArgumentOutOfRangeException(nameof(voucherId));
            if(systemCurrencyCode!=null&&(string.IsNullOrWhiteSpace(systemCurrencyCode)||systemCurrencyCode.Trim().Length>10))
                throw new ArgumentException("عملة القيد المحفوظ غير صالحة.",nameof(systemCurrencyCode));
            SystemCurrencyCode=systemCurrencyCode==null?null:systemCurrencyCode.Trim().ToUpperInvariant();
            VoucherId = voucherId; Date = date; Type = type ?? string.Empty; PartyName = partyName ?? string.Empty;
            AccountCode = accountCode ?? string.Empty; Amount = amount; Notes = notes ?? string.Empty;
            JournalEntryId = journalEntryId; CreatedBy = createdBy ?? string.Empty;
        }
        public int VoucherId { get; }
        public DateTime Date { get; }
        public string Type { get; }
        public string PartyName { get; }
        public string AccountCode { get; }
        public decimal Amount { get; }
        public string Notes { get; }
        public int? JournalEntryId { get; }
        public string CreatedBy { get; }
        public string SystemCurrencyCode { get; }
    }

    public interface IVoucherDocumentReadRepository
    {
        VoucherDocumentSnapshot Load(VoucherDocumentReadRequest request);
        Task<VoucherDocumentSnapshot> LoadAsync(VoucherDocumentReadRequest request, CancellationToken cancellationToken);
    }
    public interface IVoucherDocumentReadService
    {
        Task<VoucherDocumentSnapshot> LoadAsync(VoucherDocumentReadRequest request, OperationActor actor);
        Task<VoucherDocumentSnapshot> LoadAsync(VoucherDocumentReadRequest request, OperationActor actor, CancellationToken cancellationToken);
    }
    public sealed class VoucherDocumentReadService : IVoucherDocumentReadService
    {
        private readonly IVoucherDocumentReadRepository repository;
        private readonly Func<OperationActor, bool> canRead;
        public VoucherDocumentReadService(IVoucherDocumentReadRepository repository, Func<OperationActor, bool> canRead)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
            this.canRead = canRead ?? throw new ArgumentNullException(nameof(canRead));
        }
        private void Authorize(OperationActor actor)
        {
            if (actor == null || string.IsNullOrWhiteSpace(actor.UserName) || string.IsNullOrWhiteSpace(actor.UserRole) || !canRead(actor))
                throw new UnauthorizedAccessException("لا تملك صلاحية قراءة السند المحاسبي.");
        }
        public Task<VoucherDocumentSnapshot> LoadAsync(VoucherDocumentReadRequest request, OperationActor actor)
            => LoadAsync(request, actor, CancellationToken.None);
        public async Task<VoucherDocumentSnapshot> LoadAsync(VoucherDocumentReadRequest request, OperationActor actor, CancellationToken cancellationToken)
        {
            var snapshot = actor == null ? null : new OperationActor { UserName = actor.UserName, UserRole = actor.UserRole };
            Authorize(snapshot);
            if (request == null) throw new ArgumentNullException(nameof(request));
            cancellationToken.ThrowIfCancellationRequested();
            var result = await repository.LoadAsync(request, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            Authorize(snapshot);
            if (result == null) throw new InvalidOperationException("السند المحفوظ غير موجود؛ لم تُنشأ معاينة طباعة.");
            if (result.VoucherId != request.VoucherId)
                throw new InvalidOperationException("هوية السند المقروء لا تطابق طلب الطباعة.");
            return result;
        }
    }
}


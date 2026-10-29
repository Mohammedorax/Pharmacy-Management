using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
namespace PharmacyManagement.Application
{
    public sealed class VoucherHistoryReadRequest
    {
        public VoucherHistoryReadRequest(DateTime from, DateTime to, string type)
        {
            if (from.Date < new DateTime(1753, 1, 1) || to.Date < from.Date) throw new ArgumentException("Invalid voucher history date range.");
            Type = (type ?? string.Empty).Trim();
            if (Type.Length > 50) throw new ArgumentException("Voucher type exceeds its storage limit.");
            From = from.Date; To = to.Date;
            ExclusiveEnd = To == DateTime.MaxValue.Date ? (DateTime?)null : To.AddDays(1);
        }
        public DateTime From { get; }
        public DateTime To { get; }
        public DateTime? ExclusiveEnd { get; }
        public string Type { get; }
    }
    public interface IVoucherHistoryReadRepository
    { IReadOnlyList<VoucherDocumentSnapshot> Load(VoucherHistoryReadRequest request);
      Task<IReadOnlyList<VoucherDocumentSnapshot>> LoadAsync(VoucherHistoryReadRequest request,CancellationToken cancellationToken); }
    public interface IVoucherHistoryReadService
    {
        IReadOnlyList<VoucherDocumentSnapshot> Load(VoucherHistoryReadRequest request, OperationActor actor);
        Task<IReadOnlyList<VoucherDocumentSnapshot>> LoadAsync(VoucherHistoryReadRequest request, OperationActor actor);
        Task<IReadOnlyList<VoucherDocumentSnapshot>> LoadAsync(VoucherHistoryReadRequest request,OperationActor actor,CancellationToken cancellationToken);
    }
    public sealed class VoucherHistoryReadService : IVoucherHistoryReadService
    {
        private readonly IVoucherHistoryReadRepository repository;
        private readonly Func<OperationActor, bool> canRead;
        public VoucherHistoryReadService(IVoucherHistoryReadRepository repository, Func<OperationActor, bool> canRead)
        { this.repository = repository ?? throw new ArgumentNullException(nameof(repository)); this.canRead = canRead ?? throw new ArgumentNullException(nameof(canRead)); }
        private void Authorize(OperationActor actor)
        { if (actor == null || string.IsNullOrWhiteSpace(actor.UserName) || !canRead(actor)) throw new UnauthorizedAccessException("لا تملك صلاحية قراءة سجل السندات."); }
        private OperationActor CaptureActor(OperationActor actor)
        {
            var snapshot = actor == null ? null : new OperationActor { UserName = actor.UserName, UserRole = actor.UserRole };
            Authorize(snapshot);
            return snapshot;
        }
        private IReadOnlyList<VoucherDocumentSnapshot> LoadCaptured(VoucherHistoryReadRequest request, OperationActor snapshot)
        {
            Authorize(snapshot);
            var rows = repository.Load(request); Authorize(snapshot);
            if (rows == null) throw new InvalidOperationException("Voucher history source returned no result.");
            return new List<VoucherDocumentSnapshot>(rows).AsReadOnly();
        }
        public IReadOnlyList<VoucherDocumentSnapshot> Load(VoucherHistoryReadRequest request, OperationActor actor)
        {
            var snapshot = CaptureActor(actor);
            if (request == null) throw new ArgumentNullException(nameof(request));
            return LoadCaptured(request, snapshot);
        }
        public Task<IReadOnlyList<VoucherDocumentSnapshot>> LoadAsync(VoucherHistoryReadRequest request, OperationActor actor)
            => LoadAsync(request,actor,CancellationToken.None);
        public async Task<IReadOnlyList<VoucherDocumentSnapshot>> LoadAsync(VoucherHistoryReadRequest request,OperationActor actor,CancellationToken cancellationToken)
        {
            var snapshot=CaptureActor(actor);
            if(request==null)throw new ArgumentNullException(nameof(request));
            cancellationToken.ThrowIfCancellationRequested();
            var rows=await repository.LoadAsync(request,cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();Authorize(snapshot);
            if(rows==null)throw new InvalidOperationException("Voucher history source returned no result.");
            return new List<VoucherDocumentSnapshot>(rows).AsReadOnly();
        }
    }
}

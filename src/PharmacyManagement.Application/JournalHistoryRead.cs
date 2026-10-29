using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;

namespace PharmacyManagement.Application
{
    public sealed class JournalHistoryReadRequest
    {
        public JournalHistoryReadRequest(DateTime from, DateTime to, string sourceType)
        {
            if (from.Date < new DateTime(1753, 1, 1)) throw new ArgumentOutOfRangeException(nameof(from));
            if (to.Date < from.Date) throw new ArgumentOutOfRangeException(nameof(to));
            SourceType = (sourceType ?? string.Empty).Trim();
            if (SourceType.Length > 50) throw new ArgumentException("Source type exceeds its stored length.", nameof(sourceType));
            From = from.Date; To = to.Date;
            ExclusiveEnd = To == DateTime.MaxValue.Date ? (DateTime?)null : To.AddDays(1);
        }
        public DateTime From { get; }
        public DateTime To { get; }
        public DateTime? ExclusiveEnd { get; }
        public string SourceType { get; }
    }
    public sealed class JournalHistoryRow
    {
        public JournalHistoryRow(int id, DateTime date, string sourceType, string sourceId,
            string description, decimal? debit, decimal? credit, string createdBy)
        { Id = id; Date = date; SourceType = sourceType; SourceId = sourceId; Description = description;
            Debit = debit; Credit = credit; CreatedBy = createdBy; }
        public int Id { get; }
        public DateTime Date { get; }
        public string SourceType { get; }
        public string SourceId { get; }
        public string Description { get; }
        public decimal? Debit { get; }
        public decimal? Credit { get; }
        public string CreatedBy { get; }
    }
    public interface IJournalHistoryReadRepository
    {
        IReadOnlyList<JournalHistoryRow> Load(JournalHistoryReadRequest request);
        Task<IReadOnlyList<JournalHistoryRow>> LoadAsync(JournalHistoryReadRequest request, CancellationToken cancellationToken);
    }
    public interface IJournalHistoryReadService
    {
        IReadOnlyList<JournalHistoryRow> Load(JournalHistoryReadRequest request, OperationActor actor);
        Task<IReadOnlyList<JournalHistoryRow>> LoadAsync(JournalHistoryReadRequest request, OperationActor actor);
        Task<IReadOnlyList<JournalHistoryRow>> LoadAsync(JournalHistoryReadRequest request, OperationActor actor, CancellationToken cancellationToken);
    }
    public sealed class JournalHistoryReadService : IJournalHistoryReadService
    {
        private readonly IJournalHistoryReadRepository repository;
        private readonly Func<OperationActor, bool> canRead;
        public JournalHistoryReadService(IJournalHistoryReadRepository repository, Func<OperationActor, bool> canRead)
        { this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
            this.canRead = canRead ?? throw new ArgumentNullException(nameof(canRead)); }
        private void Authorize(OperationActor actor)
        {
            if (actor == null || string.IsNullOrWhiteSpace(actor.UserName) || !canRead(actor))
                throw new UnauthorizedAccessException("لا تملك صلاحية قراءة سجل القيود.");
        }
        private static IReadOnlyList<JournalHistoryRow> Snapshot(IReadOnlyList<JournalHistoryRow> rows)
        {
            if (rows == null) throw new InvalidOperationException("Journal history source returned no result.");
            return new List<JournalHistoryRow>(rows).AsReadOnly();
        }
        private OperationActor CaptureActor(OperationActor actor)
        {
            var current=new OperationActor {UserName=actor?.UserName,UserRole=actor?.UserRole};
            Authorize(current);return current;
        }
        public IReadOnlyList<JournalHistoryRow> Load(JournalHistoryReadRequest request, OperationActor actor)
        {
            var current=CaptureActor(actor);if(request==null)throw new ArgumentNullException(nameof(request));
            var rows=repository.Load(request);Authorize(current);return Snapshot(rows);
        }
        public async Task<IReadOnlyList<JournalHistoryRow>> LoadAsync(JournalHistoryReadRequest request, OperationActor actor)
        { return await LoadAsync(request,actor,CancellationToken.None).ConfigureAwait(false); }
        public async Task<IReadOnlyList<JournalHistoryRow>> LoadAsync(JournalHistoryReadRequest request, OperationActor actor, CancellationToken cancellationToken)
        {
            var current=CaptureActor(actor);if(request==null)throw new ArgumentNullException(nameof(request));
            cancellationToken.ThrowIfCancellationRequested();
            var rows=await repository.LoadAsync(request,cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();Authorize(current);return Snapshot(rows);
        }
    }
}

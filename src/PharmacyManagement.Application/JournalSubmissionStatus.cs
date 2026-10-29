using System;
using System.Threading;
using System.Threading.Tasks;

namespace PharmacyManagement.Application
{
    public sealed class JournalSubmissionStatusRequest
    {
        public JournalSubmissionStatusRequest(string sourceType, string sourceId, JournalPostRequest expectedRequest = null)
        { SourceType = sourceType; SourceId = sourceId; ExpectedRequest = expectedRequest; }
        public string SourceType { get; }
        public string SourceId { get; }
        public JournalPostRequest ExpectedRequest { get; }
    }
    public sealed class JournalSubmissionStatus
    {
        public JournalSubmissionStatus(int journalId, bool? contentMatches = null)
        {
            if (journalId < 0) throw new ArgumentOutOfRangeException(nameof(journalId));
            if (journalId == 0 && contentMatches.HasValue) throw new ArgumentException("لا تقارن بيانات قيد غير معتمد.");
            JournalId = journalId; ContentMatches = contentMatches;
        }
        public int JournalId { get; }
        public bool IsPosted => JournalId > 0;
        public bool? ContentMatches { get; }
    }
    public interface IJournalSubmissionStatusRepository
    { Task<JournalSubmissionStatus> FindAsync(JournalSubmissionStatusRequest request, OperationActor actor, CancellationToken token); }
    public sealed class JournalSubmissionStatusService
    {
        private readonly IJournalSubmissionStatusRepository repository;
        private readonly Func<OperationActor, bool> canRead;
        public JournalSubmissionStatusService(IJournalSubmissionStatusRepository repository, Func<OperationActor, bool> canRead = null)
        { this.repository = repository ?? throw new ArgumentNullException(nameof(repository)); this.canRead = canRead ?? (actor => false); }
        private void Authorize(OperationActor actor)
        {
            if (actor == null || string.IsNullOrWhiteSpace(actor.UserName) || string.IsNullOrWhiteSpace(actor.UserRole) || !canRead(actor))
                throw new UnauthorizedAccessException("لا تملك صلاحية التحقق من اعتماد القيد.");
        }
        public static JournalSubmissionStatusRequest PrepareRequest(JournalSubmissionStatusRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            string type = request.SourceType == null ? null : request.SourceType.Trim();
            string id = request.SourceId == null ? null : request.SourceId.Trim();
            if (string.IsNullOrWhiteSpace(type) || type.Length > 50 || string.IsNullOrWhiteSpace(id) || id.Length > 50)
                throw new InvalidOperationException("مصدر القيد ومعرّف طلبه مطلوبان ضمن حدود التخزين.");
            var expected = request.ExpectedRequest == null ? null : JournalPostService.PrepareRequest(request.ExpectedRequest);
            if (expected != null && (expected.SourceType != type || expected.SourceId != id))
                throw new InvalidOperationException("بيانات المقارنة لا تطابق هوية طلب القيد.");
            return new JournalSubmissionStatusRequest(type, id, expected);
        }
        public async Task<JournalSubmissionStatus> FindAsync(JournalSubmissionStatusRequest request, OperationActor actor, CancellationToken token)
        {
            token.ThrowIfCancellationRequested(); Authorize(actor);
            var captured = new OperationActor { UserName = actor.UserName.Trim(), UserRole = actor.UserRole.Trim() };
            var prepared = PrepareRequest(request); Authorize(captured);
            var result = await repository.FindAsync(prepared, captured, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested(); Authorize(captured);
            if (result == null) throw new InvalidOperationException("لم تُعد قراءة حالة القيد نتيجة صالحة.");
            if (prepared.ExpectedRequest != null && result.IsPosted && !result.ContentMatches.HasValue)
                throw new InvalidOperationException("لم تثبت قراءة القيد مطابقة بياناته للطلب الحالي.");
            return result;
        }
    }
}

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using PharmacyManagement.Domain;

namespace PharmacyManagement.Application
{
    public sealed class JournalPostRequest
    {
        public JournalPostRequest(DateTime date, string sourceType, string sourceId, string description, IEnumerable<JournalLineValues> lines)
            : this(date, sourceType, sourceId, description, lines, null) { }
        public JournalPostRequest(DateTime date, string sourceType, string sourceId, string description, IEnumerable<JournalLineValues> lines, string expectedSystemCurrencyCode)
        {
            Date = date; SourceType = sourceType; SourceId = sourceId; Description = description;
            ExpectedSystemCurrencyCode = expectedSystemCurrencyCode;
            if (lines == null) throw new ArgumentNullException(nameof(lines));
            Lines = new List<JournalLineValues>(lines).AsReadOnly();
        }
        public DateTime Date { get; }
        public string SourceType { get; }
        public string SourceId { get; }
        public string Description { get; }
        public IReadOnlyList<JournalLineValues> Lines { get; }
        // Null identifies a legacy request whose base currency was never captured.
        public string ExpectedSystemCurrencyCode { get; }
    }
    public sealed class JournalPostResult
    {
        public JournalPostResult(int id, bool alreadyPosted) { Id = id; AlreadyPosted = alreadyPosted; }
        public int Id { get; }
        public bool AlreadyPosted { get; }
    }
    public interface IJournalPostRepository
    { JournalPostResult Post(JournalPostRequest request, OperationActor actor); }
    public interface IJournalPostService
    { JournalPostResult Post(JournalPostRequest request, OperationActor actor); }
    public interface IAsyncJournalPostRepository : IJournalPostRepository
    { Task<JournalPostResult> PostAsync(JournalPostRequest request, OperationActor actor, CancellationToken token); }
    public interface IAsyncJournalPostService : IJournalPostService
    { Task<JournalPostResult> PostAsync(JournalPostRequest request, OperationActor actor, CancellationToken token); }
    public sealed class JournalPostService : IAsyncJournalPostService
    {
        private readonly IJournalPostRepository repository;
        private readonly Func<OperationActor, bool> canPost;
        public JournalPostService(IJournalPostRepository repository, Func<OperationActor, bool> canPost)
        { this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
          this.canPost = canPost ?? throw new ArgumentNullException(nameof(canPost)); }
        private void Authorize(OperationActor actor)
        {
            if (actor == null || string.IsNullOrWhiteSpace(actor.UserName) || string.IsNullOrWhiteSpace(actor.UserRole) || !canPost(actor))
                throw new UnauthorizedAccessException("لا تملك صلاحية اعتماد القيود المحاسبية.");
        }
        private static string Text(string text, int max, string field)
        {
            string value = string.IsNullOrWhiteSpace(text) ? null : text.Trim();
            if (value != null && value.Length > max) throw new InvalidOperationException(field + " يتجاوز الحد المسموح.");
            return value;
        }
        public static JournalPostRequest PrepareRequest(JournalPostRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (request.Date < new DateTime(1753, 1, 1) || request.Date > new DateTime(9999, 12, 31, 23, 59, 59, 997))
                throw new InvalidOperationException("تاريخ القيد خارج نطاق التخزين المحاسبي.");
            var prepared = JournalEntryPreparation.Prepare(request.Lines);
            foreach (var line in prepared)
            {
                Text(line.AccountCode, 20, "رقم الحساب"); Text(line.Description, 500, "بيان السطر");
            }
            string expectedCurrency = null;
            if (request.ExpectedSystemCurrencyCode != null)
            {
                expectedCurrency = Text(request.ExpectedSystemCurrencyCode, 10, "عملة النظام للقيد");
                if (expectedCurrency == null) throw new InvalidOperationException("لقطة عملة النظام للقيد فارغة.");
                expectedCurrency = expectedCurrency.ToUpperInvariant();
            }
            var normalized = new JournalPostRequest(request.Date, Text(request.SourceType, 50, "مصدر القيد"),
                Text(request.SourceId, 50, "معرف المصدر"), Text(request.Description, 500, "بيان القيد"), prepared, expectedCurrency);

            return normalized;
        }
        private JournalPostRequest PrepareAuthorizedRequest(JournalPostRequest request,OperationActor actor,out OperationActor authorized)
        {
            Authorize(actor);
            authorized = new OperationActor { UserName = Text(actor.UserName, 200, "المستخدم"), UserRole = Text(actor.UserRole, 50, "الدور") };
            var normalized = PrepareRequest(request);
            Authorize(authorized);return normalized;
        }
        public async Task<JournalPostResult> PostAsync(JournalPostRequest request,OperationActor actor,CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            OperationActor authorized;var normalized=PrepareAuthorizedRequest(request,actor,out authorized);
            var native=repository as IAsyncJournalPostRepository;
            if(native==null)throw new InvalidOperationException("مستودع القيود لا يدعم الكتابة الأصلية غير المتزامنة؛ لم يُرسل الاعتماد.");
            token.ThrowIfCancellationRequested();
            var result=await native.PostAsync(normalized,authorized,token).ConfigureAwait(false);
            if(result==null||result.Id<=0)throw new InvalidOperationException("لم يُعد اعتماد القيد معرفًا صالحًا.");
            return result;
        }
        public JournalPostResult Post(JournalPostRequest request, OperationActor actor)
        {
            OperationActor authorized;var normalized=PrepareAuthorizedRequest(request,actor,out authorized);
            // Authorization after a successful repository commit must not turn a known
            // acceptance into a failure that encourages the user to post it again.
            var result = repository.Post(normalized, authorized);
            if (result == null || result.Id <= 0) throw new InvalidOperationException("لم يُعد اعتماد القيد معرفًا صالحًا.");
            return result;
        }
    }
}

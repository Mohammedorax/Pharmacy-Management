using System;

namespace PharmacyManagement.Application
{
    public sealed class AccountingIntegrityRequest
    {
        public DateTime FromDate { get; private set; }
        public DateTime ToDate { get; private set; }
        public AccountingIntegrityRequest(DateTime from, DateTime to)
        {
            if(from.Date<new DateTime(1753,1,1) || from.Date>to.Date || to.Date>=DateTime.MaxValue.Date)
                throw new InvalidOperationException("فترة فحص سلامة القيود غير صحيحة.");
            FromDate=from.Date; ToDate=to.Date;
        }
    }
    public interface IAccountingIntegrityReadRepository
    {
        OperationalReportResult Load(AccountingIntegrityRequest request);
    }
    public interface IAccountingIntegrityReadService
    {
        OperationalReportResult Load(DateTime from, DateTime to);
    }
    public interface IAsyncAccountingIntegrityReadRepository
    {
        System.Threading.Tasks.Task<OperationalReportResult> LoadAsync(AccountingIntegrityRequest request, System.Threading.CancellationToken cancellation);
    }
    public interface IAsyncAccountingIntegrityReadService
    {
        System.Threading.Tasks.Task<OperationalReportResult> LoadAsync(DateTime from, DateTime to, System.Threading.CancellationToken cancellation);
    }
    public sealed class AccountingIntegrityReadService : IAccountingIntegrityReadService, IAsyncAccountingIntegrityReadService
    {
        private readonly IAccountingIntegrityReadRepository repository;
        private readonly Func<bool> canRead;
        public AccountingIntegrityReadService(IAccountingIntegrityReadRepository repository) : this(repository, null) { }
        public AccountingIntegrityReadService(IAccountingIntegrityReadRepository repository, Func<bool> canRead)
        { this.repository = repository ?? throw new ArgumentNullException(nameof(repository)); this.canRead = canRead ?? (() => false); }
        private void Authorize()
        {
            if (!canRead()) throw new UnauthorizedAccessException("لا تملك صلاحية فحص سلامة القيود أو تغيرت جلسة المستخدم؛ أعد فتح الفحص.");
        }
        public OperationalReportResult Load(DateTime from, DateTime to)
        {
            var request = new AccountingIntegrityRequest(from, to);
            Authorize();
            var result = repository.Load(request);
            Authorize();
            return result ?? throw new InvalidOperationException("لم يرجع فحص سلامة القيود نتيجة.");
        }
        public async System.Threading.Tasks.Task<OperationalReportResult> LoadAsync(DateTime from, DateTime to, System.Threading.CancellationToken cancellation)
        {
            var request = new AccountingIntegrityRequest(from, to);
            cancellation.ThrowIfCancellationRequested(); Authorize();
            var asyncRepository = repository as IAsyncAccountingIntegrityReadRepository;
            if (asyncRepository == null) throw new InvalidOperationException("مصدر فحص سلامة القيود لا يدعم القراءة غير المتزامنة.");
            var result = await asyncRepository.LoadAsync(request, cancellation).ConfigureAwait(false);
            cancellation.ThrowIfCancellationRequested(); Authorize();
            return result ?? throw new InvalidOperationException("لم يرجع فحص سلامة القيود نتيجة.");
        }
    }
}
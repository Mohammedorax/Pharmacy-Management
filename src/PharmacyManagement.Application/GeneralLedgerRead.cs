using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PharmacyManagement.Application
{
    public sealed class GeneralLedgerReadRequest
    {
        public GeneralLedgerReadRequest(string accountCode, DateTime fromDate, DateTime toDate, string sourceType,
            bool includePreviousBalance, bool includeOpeningEntries, int? costCenterId)
        {
            if (string.IsNullOrWhiteSpace(accountCode) || accountCode.Trim().Length > 20)
                throw new ArgumentException("رقم الحساب غير صالح.", nameof(accountCode));
            if (fromDate.Date < new DateTime(1753, 1, 1) || fromDate.Date > toDate.Date || toDate.Date == DateTime.MaxValue.Date)
                throw new ArgumentException("فترة كشف الحساب غير صالحة.");
            string source = string.IsNullOrWhiteSpace(sourceType) ? null : sourceType.Trim();
            if (source != null && !new[] { "Manual", "Voucher", "SalesInvoice", "PurchaseInvoice", "OpeningEntry" }.Contains(source))
                throw new ArgumentException("نوع مصدر القيد غير صالح.", nameof(sourceType));
            // Zero is the existing explicit 'without cost center' filter; null means all.
            if (costCenterId.HasValue && costCenterId.Value < 0) throw new ArgumentException("مركز التكلفة غير صالح.", nameof(costCenterId));
            AccountCode = accountCode.Trim(); FromDate = fromDate.Date; ToDate = toDate.Date;
            SourceType = source; IncludePreviousBalance = includePreviousBalance;
            IncludeOpeningEntries = includeOpeningEntries; CostCenterId = costCenterId;
        }
        public string AccountCode { get; }
        public DateTime FromDate { get; }
        public DateTime ToDate { get; }
        public DateTime ExclusiveEnd => ToDate.AddDays(1);
        public string SourceType { get; }
        public bool IncludePreviousBalance { get; }
        public bool IncludeOpeningEntries { get; }
        public int? CostCenterId { get; }
    }
    public sealed class GeneralLedgerReadRow
    {
        public GeneralLedgerReadRow(DateTime date, int documentId, string sourceType, string sourceId,
            string costCenterName, string description, decimal? debit, decimal? credit, decimal balance)
        {
            Date = date; DocumentId = documentId; SourceType = sourceType; SourceId = sourceId;
            CostCenterName = costCenterName; Description = description; Debit = debit; Credit = credit; Balance = balance;
        }
        public DateTime Date { get; }
        public int DocumentId { get; }
        public string SourceType { get; }
        public string SourceId { get; }
        public string CostCenterName { get; }
        public string Description { get; }
        public decimal? Debit { get; }
        public decimal? Credit { get; }
        public decimal Balance { get; }
    }
    public sealed class GeneralLedgerReadResult
    {
        public GeneralLedgerReadResult(IEnumerable<GeneralLedgerReadRow> rows, decimal previousBalance)
        {
            var snapshot = (rows ?? throw new ArgumentNullException(nameof(rows))).ToList();
            if (snapshot.Any(row => row == null)) throw new ArgumentException("صف كشف الحساب غير صالح.", nameof(rows));
            Rows = snapshot.AsReadOnly(); PreviousBalance = previousBalance;
            PeriodDebit = snapshot.Sum(row => row.Debit ?? 0m);
            PeriodCredit = snapshot.Sum(row => row.Credit ?? 0m);
            // Preserve the final balance of the original accounting sequence, before presentation sorting.
            EndingBalance = snapshot.Count == 0 ? previousBalance : snapshot[snapshot.Count - 1].Balance;
        }
        public IReadOnlyList<GeneralLedgerReadRow> Rows { get; }
        public decimal PreviousBalance { get; }
        public decimal PeriodDebit { get; }
        public decimal PeriodCredit { get; }
        public decimal EndingBalance { get; }
    }

    public interface IGeneralLedgerReadRepository
    {
        Task<GeneralLedgerReadResult> LoadAsync(GeneralLedgerReadRequest request, CancellationToken cancellationToken);

    }
    public interface IGeneralLedgerReadService
    {
        Task<GeneralLedgerReadResult> LoadAsync(GeneralLedgerReadRequest request, OperationActor actor, CancellationToken cancellationToken);

    }
    public sealed class GeneralLedgerReadService : IGeneralLedgerReadService
    {
        private readonly IGeneralLedgerReadRepository repository;
        private readonly Func<OperationActor, bool> canRead;
        public GeneralLedgerReadService(IGeneralLedgerReadRepository repository, Func<OperationActor, bool> canRead = null)
        { this.repository = repository ?? throw new ArgumentNullException(nameof(repository)); this.canRead = canRead ?? (_ => false); }
        private void Authorize(OperationActor actor)
        {
            if (string.IsNullOrWhiteSpace(actor.UserName) || string.IsNullOrWhiteSpace(actor.UserRole) || !canRead(actor))
                throw new UnauthorizedAccessException("لا تملك صلاحية قراءة الأستاذ العام.");
        }
        public async Task<GeneralLedgerReadResult> LoadAsync(GeneralLedgerReadRequest request, OperationActor actor, CancellationToken cancellationToken)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            var captured = new OperationActor { UserName = actor?.UserName, UserRole = actor?.UserRole };
            Authorize(captured); cancellationToken.ThrowIfCancellationRequested();
            var result = await repository.LoadAsync(request, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested(); Authorize(captured);
            if (result == null) throw new InvalidOperationException("لم يعد مصدر الأستاذ العام نتيجة.");
            return result;
        }

    }
}
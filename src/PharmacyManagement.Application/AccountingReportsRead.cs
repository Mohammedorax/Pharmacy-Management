using System;
using System.Threading;
using System.Threading.Tasks;

namespace PharmacyManagement.Application
{
    public interface IAccountingReportsReadRepository
    {
        Task<OperationalReportResult> LoadAsync(AccountingReportReadRequest request, CancellationToken cancellationToken);
    }

    public sealed class AccountingReportsReadService
    {
        private readonly IAccountingReportsReadRepository repository;
        private readonly Func<OperationActor, bool> canRead;

        public AccountingReportsReadService(IAccountingReportsReadRepository repository, Func<OperationActor, bool> canRead = null)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
            this.canRead = canRead ?? (_ => false);
        }

        private void Authorize(OperationActor actor)
        {
            if (actor == null || string.IsNullOrWhiteSpace(actor.UserName) ||
                string.IsNullOrWhiteSpace(actor.UserRole) || !canRead(actor))
                throw new UnauthorizedAccessException("لا تملك صلاحية قراءة التقارير المحاسبية.");
        }

        public async Task<OperationalReportResult> LoadAsync(AccountingReportReadRequest request,
            OperationActor actor, CancellationToken cancellationToken)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            var captured = new OperationActor { UserName = actor?.UserName, UserRole = actor?.UserRole };
            Authorize(captured);
            cancellationToken.ThrowIfCancellationRequested();
            var result = await repository.LoadAsync(request, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            Authorize(captured);
            if (result == null) throw new InvalidOperationException("لم يُرجع مصدر التقرير نتيجة صالحة.");
            return result;
        }
    }

    // Preserve the report's existing aggregation semantics; display rounding belongs to presentation.
    public sealed class AccountingReportSummary
    {
        private readonly System.Collections.Generic.Dictionary<string, decimal> totals;
        public int RowCount { get; private set; }
        public decimal MovementNet { get { return Total("حركة الحساب دائن") - Total("حركة الحساب مدين"); } }
        private AccountingReportSummary(int rowCount, System.Collections.Generic.Dictionary<string, decimal> totals)
        { RowCount = rowCount; this.totals = totals; }
        public decimal Total(string columnName)
        {
            if (columnName == null) throw new ArgumentNullException(nameof(columnName));
            decimal total; return totals.TryGetValue(columnName, out total) ? total : 0m;
        }
        public static AccountingReportSummary Calculate(OperationalReportResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            var totals = new System.Collections.Generic.Dictionary<string, decimal>(StringComparer.Ordinal);
            var supported = new System.Collections.Generic.HashSet<string>(new[] { "مدين", "دائن", "الصافي", "صافي الضريبة", "الرصيد", "الرصيد الافتتاحي مدين", "الرصيد الافتتاحي دائن", "الرصيد السابق مدين", "الرصيد السابق دائن", "حركة الحساب مدين", "حركة الحساب دائن", "رصيد الحساب مدين", "رصيد الحساب دائن" }, StringComparer.Ordinal);
            for (int column = 0; column < result.Columns.Count; column++)
            {
                if (!supported.Contains(result.Columns[column].Name)) continue;
                decimal total = 0m;
                foreach (var row in result.Rows)
                {
                    if (row[column] == null || Convert.IsDBNull(row[column])) continue;
                    decimal value;
                    if (decimal.TryParse(Convert.ToString(row[column]), out value)) total += value;
                }
                totals.Add(result.Columns[column].Name, total);
            }
            return new AccountingReportSummary(result.Rows.Count, totals);
        }
    }}

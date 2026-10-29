using System;
using System.Collections.Generic;

namespace PharmacyManagement.Application
{
    // Read models intentionally contain only data needed by the desktop dashboard.
    // They keep SQL and WinForms types out of the application contract.
    public sealed class DashboardCustomerSummary
    {
        public string CustomerId { get; set; }
        public string CustomerName { get; set; }
        public string CustomerAddress { get; set; }
        public string PhoneNumber { get; set; }
        public decimal TotalSpent { get; set; }
        public DateTime? InvoiceDate { get; set; }
        public string EmployeeName { get; set; }
    }

    public sealed class DashboardSalesTrendPoint
    {
        public DateTime SaleDay { get; set; }
        public decimal DailyTotal { get; set; }
    }

    public interface IDashboardReadRepository
    {
        int GetCompanyCount();
        int GetEmployeeCount();
        decimal GetSalesTotal(string cashierName);
        IReadOnlyList<string> GetCashierNames();
        IReadOnlyList<DashboardCustomerSummary> GetCustomerSummaries(string cashierName);
        IReadOnlyList<DashboardSalesTrendPoint> GetRecentSalesTrend(string cashierName);
    }

    public interface IDashboardReadService
    {
        int GetCompanyCount();
        int GetEmployeeCount();
        decimal GetSalesTotal(string cashierName);
        IReadOnlyList<string> GetCashierNames();
        IReadOnlyList<DashboardCustomerSummary> GetCustomerSummaries(string cashierName);
        IReadOnlyList<DashboardSalesTrendPoint> GetRecentSalesTrend(string cashierName);
    }

    public interface IAsyncDashboardReadRepository
    {
        System.Threading.Tasks.Task<int> GetCompanyCountAsync(Action demandAccess, System.Threading.CancellationToken token);
        System.Threading.Tasks.Task<int> GetEmployeeCountAsync(Action demandAccess, System.Threading.CancellationToken token);
        System.Threading.Tasks.Task<decimal> GetSalesTotalAsync(string cashierName, Action demandAccess, System.Threading.CancellationToken token);
        System.Threading.Tasks.Task<IReadOnlyList<string>> GetCashierNamesAsync(Action demandAccess, System.Threading.CancellationToken token);
        System.Threading.Tasks.Task<IReadOnlyList<DashboardCustomerSummary>> GetCustomerSummariesAsync(string cashierName, Action demandAccess, System.Threading.CancellationToken token);
        System.Threading.Tasks.Task<IReadOnlyList<DashboardSalesTrendPoint>> GetRecentSalesTrendAsync(string cashierName, Action demandAccess, System.Threading.CancellationToken token);
    }

    public interface IAsyncDashboardReadService
    {
        System.Threading.Tasks.Task<int> GetCompanyCountAsync(System.Threading.CancellationToken token);
        System.Threading.Tasks.Task<int> GetEmployeeCountAsync(System.Threading.CancellationToken token);
        System.Threading.Tasks.Task<decimal> GetSalesTotalAsync(string cashierName, System.Threading.CancellationToken token);
        System.Threading.Tasks.Task<IReadOnlyList<string>> GetCashierNamesAsync(System.Threading.CancellationToken token);
        System.Threading.Tasks.Task<IReadOnlyList<DashboardCustomerSummary>> GetCustomerSummariesAsync(string cashierName, System.Threading.CancellationToken token);
        System.Threading.Tasks.Task<IReadOnlyList<DashboardSalesTrendPoint>> GetRecentSalesTrendAsync(string cashierName, System.Threading.CancellationToken token);
    }

    public sealed class DashboardReadService : IDashboardReadService, IAsyncDashboardReadService
    {
        private readonly IDashboardReadRepository repository;
        private readonly OperationActor actor;
        private readonly Func<OperationActor, bool> canRead;

        // Compatibility construction never grants implicit access.
        public DashboardReadService(IDashboardReadRepository repository) : this(repository, null, null) { }

        public DashboardReadService(IDashboardReadRepository repository, OperationActor actor, Func<OperationActor, bool> canRead)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
            this.actor = actor == null ? null : new OperationActor { UserName = actor.UserName, UserRole = actor.UserRole };
            this.canRead = canRead ?? (_ => false);
        }

        private void RequireAccess()
        {
            if (actor == null || string.IsNullOrWhiteSpace(actor.UserName) || string.IsNullOrWhiteSpace(actor.UserRole)
                || !canRead(new OperationActor { UserName = actor.UserName, UserRole = actor.UserRole }))
                throw new UnauthorizedAccessException("تغيرت الجلسة أو صلاحية قراءة لوحة المؤشرات.");
        }

        private T Read<T>(Func<T> read)
        {
            RequireAccess();
            T result = read();
            RequireAccess();
            return result;
        }

        private async System.Threading.Tasks.Task<T> ReadAsync<T>(Func<IAsyncDashboardReadRepository, Action, System.Threading.Tasks.Task<T>> read, System.Threading.CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            RequireAccess();
            var asynchronous = repository as IAsyncDashboardReadRepository;
            if (asynchronous == null) throw new InvalidOperationException("Dashboard repository does not support asynchronous reads.");
            Action demand = () => { token.ThrowIfCancellationRequested(); RequireAccess(); };
            T result = await read(asynchronous, demand).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            RequireAccess();
            return result;
        }

        public int GetCompanyCount() => Read(() => repository.GetCompanyCount());
        public int GetEmployeeCount() => Read(() => repository.GetEmployeeCount());
        public decimal GetSalesTotal(string cashierName) => Read(() => repository.GetSalesTotal(NormalizeCashier(cashierName)));
        public IReadOnlyList<string> GetCashierNames() => Read(() => repository.GetCashierNames());
        public IReadOnlyList<DashboardCustomerSummary> GetCustomerSummaries(string cashierName) => Read(() => repository.GetCustomerSummaries(NormalizeCashier(cashierName)));
        public IReadOnlyList<DashboardSalesTrendPoint> GetRecentSalesTrend(string cashierName) => Read(() => repository.GetRecentSalesTrend(NormalizeCashier(cashierName)));

        public System.Threading.Tasks.Task<int> GetCompanyCountAsync(System.Threading.CancellationToken token) => ReadAsync((r, demand) => r.GetCompanyCountAsync(demand, token), token);
        public System.Threading.Tasks.Task<int> GetEmployeeCountAsync(System.Threading.CancellationToken token) => ReadAsync((r, demand) => r.GetEmployeeCountAsync(demand, token), token);
        public System.Threading.Tasks.Task<decimal> GetSalesTotalAsync(string cashierName, System.Threading.CancellationToken token) => ReadAsync((r, demand) => r.GetSalesTotalAsync(NormalizeCashier(cashierName), demand, token), token);
        public System.Threading.Tasks.Task<IReadOnlyList<string>> GetCashierNamesAsync(System.Threading.CancellationToken token) => ReadAsync((r, demand) => r.GetCashierNamesAsync(demand, token), token);
        public System.Threading.Tasks.Task<IReadOnlyList<DashboardCustomerSummary>> GetCustomerSummariesAsync(string cashierName, System.Threading.CancellationToken token) => ReadAsync((r, demand) => r.GetCustomerSummariesAsync(NormalizeCashier(cashierName), demand, token), token);
        public System.Threading.Tasks.Task<IReadOnlyList<DashboardSalesTrendPoint>> GetRecentSalesTrendAsync(string cashierName, System.Threading.CancellationToken token) => ReadAsync((r, demand) => r.GetRecentSalesTrendAsync(NormalizeCashier(cashierName), demand, token), token);

        private static string NormalizeCashier(string cashierName)
        {
            string value = (cashierName ?? string.Empty).Trim();
            if (value.Length > 200) throw new InvalidOperationException("اسم الكاشير يتجاوز الحد المسموح (200).");
            return value;
        }
    }
}

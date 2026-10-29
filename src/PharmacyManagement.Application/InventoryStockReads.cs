using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PharmacyManagement.Application
{
    public enum InventoryStockReadMode { Basic, CommercialLots }

    public sealed class InventoryStockReadRequest
    {
        public InventoryStockReadRequest(InventoryStockReadMode mode, string searchText, bool includeZero)
        {
            if (!Enum.IsDefined(typeof(InventoryStockReadMode), mode)) throw new InvalidOperationException("نوع كشف المخزون غير صالح.");
            SearchText = string.IsNullOrWhiteSpace(searchText) ? string.Empty : searchText.Trim();
            if (SearchText.Length > 200) throw new InvalidOperationException("نص البحث لا يتجاوز 200 حرف.");
            Mode = mode; IncludeZero = includeZero;
        }
        public InventoryStockReadMode Mode { get; }
        public string SearchText { get; }
        public bool IncludeZero { get; }
    }

    public interface IInventoryStockReadRepository
    {
        Task<OperationalReportResult> LoadAsync(InventoryStockReadRequest request, CancellationToken cancellation);
    }

    public interface IInventoryStockReadService
    {
        Task<OperationalReportResult> LoadAsync(InventoryStockReadRequest request, OperationActor actor, CancellationToken cancellation);
    }

    public sealed class InventoryStockReadService : IInventoryStockReadService
    {
        private readonly IInventoryStockReadRepository repository;
        private readonly Func<OperationActor, bool> canRead;
        public InventoryStockReadService(IInventoryStockReadRepository repository, Func<OperationActor, bool> canRead)
        { this.repository = repository ?? throw new ArgumentNullException(nameof(repository)); this.canRead = canRead ?? throw new ArgumentNullException(nameof(canRead)); }

        public async Task<OperationalReportResult> LoadAsync(InventoryStockReadRequest request, OperationActor actor, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (actor == null || string.IsNullOrWhiteSpace(actor.UserName) || string.IsNullOrWhiteSpace(actor.UserRole) ||
                actor.UserName.Length > 200 || actor.UserRole.Length > 50)
                throw new UnauthorizedAccessException("لا تملك صلاحية قراءة المخزون.");
            var captured = new OperationActor { UserName = actor.UserName, UserRole = actor.UserRole };
            if (!canRead(captured)) throw new UnauthorizedAccessException("لا تملك صلاحية قراءة المخزون.");
            var result = await repository.LoadAsync(request, cancellation).ConfigureAwait(false);
            cancellation.ThrowIfCancellationRequested();
            if (!canRead(captured)) throw new UnauthorizedAccessException("تغيرت صلاحية قراءة المخزون أثناء الطلب.");
            if (result == null) throw new InvalidOperationException("لم يرجع مصدر المخزون نتيجة.");
            return result;
        }
    }
}

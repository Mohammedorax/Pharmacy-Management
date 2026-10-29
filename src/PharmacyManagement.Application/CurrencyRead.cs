using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PharmacyManagement.Application
{
    public sealed class CurrencyRecord
    {
        public int CurrencyId { get; set; }
        public string CountryNameAr { get; set; }
        public string CurrencyNameAr { get; set; }
        public string CurrencyCode { get; set; }
        public string CurrencySymbol { get; set; }
        public bool IsSystemCurrency { get; set; }
    }

    public sealed class CurrencySnapshotUnavailableException : InvalidOperationException
    {
        public CurrencySnapshotUnavailableException() : base("عملة النظام غير محددة أو غير صالحة. راجع إعدادات العملات.") { }
    }

    // A known identity copied from settings; no invented country or display-only fallback.
    public sealed class CurrencyDisplaySnapshot
    {
        public int CurrencyId { get; }
        public string CurrencyCode { get; }
        public string CurrencySymbol { get; }
        public string DisplayText => CurrencySymbol.Length > 0 ? CurrencySymbol : CurrencyCode;
        public CurrencyDisplaySnapshot(CurrencyRecord record)
        {
            if (record == null || record.CurrencyId <= 0 || !record.IsSystemCurrency ||
                string.IsNullOrWhiteSpace(record.CurrencyCode) || record.CurrencyCode.Trim().Length > 10 ||
                (record.CurrencySymbol ?? string.Empty).Trim().Length > 20)
                throw new CurrencySnapshotUnavailableException();
            CurrencyId = record.CurrencyId;
            CurrencyCode = record.CurrencyCode.Trim();
            CurrencySymbol = (record.CurrencySymbol ?? string.Empty).Trim();
        }
    }

    public interface ICurrencyReadRepository
    {
        IReadOnlyList<CurrencyRecord> GetAll();
        CurrencyRecord GetSystemCurrency();
        Task<CurrencyRecord> GetSystemCurrencyAsync(CancellationToken cancellationToken);
    }

    public interface ICurrencyReadService
    {
        IReadOnlyList<CurrencyRecord> GetAll();
        CurrencyRecord GetSystemCurrency();
        string GetSystemCurrencyDisplay();
        Task<CurrencyDisplaySnapshot> GetSystemCurrencySnapshotAsync(OperationActor actor, CancellationToken cancellationToken);
    }

    public sealed class CurrencyReadService : ICurrencyReadService
    {
        private readonly ICurrencyReadRepository repository;
        private readonly Func<OperationActor,bool> canReadSnapshot;
        public CurrencyReadService(ICurrencyReadRepository repository, Func<OperationActor,bool> canReadSnapshot = null)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
            this.canReadSnapshot = canReadSnapshot ?? (_ => false);
        }

        public async Task<CurrencyDisplaySnapshot> GetSystemCurrencySnapshotAsync(OperationActor actor, CancellationToken cancellationToken)
        {
            RequireSnapshotActor(actor);
            var current = new OperationActor { UserName=actor.UserName,UserRole=actor.UserRole };
            cancellationToken.ThrowIfCancellationRequested();
            CurrencyRecord record = await repository.GetSystemCurrencyAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            RequireSnapshotActor(current);
            return new CurrencyDisplaySnapshot(record);
        }
        private void RequireSnapshotActor(OperationActor actor)
        {
            if(actor==null || string.IsNullOrWhiteSpace(actor.UserName) || string.IsNullOrWhiteSpace(actor.UserRole) || !canReadSnapshot(actor))
                throw new UnauthorizedAccessException("لا تملك صلاحية قراءة عملة النظام.");
        }

        public IReadOnlyList<CurrencyRecord> GetAll() => repository.GetAll();
        public CurrencyRecord GetSystemCurrency() => repository.GetSystemCurrency() ?? new CurrencyRecord();

        public string GetSystemCurrencyDisplay()
        {
            try
            {
                CurrencyRecord currency = GetSystemCurrency();
                if (!string.IsNullOrWhiteSpace(currency.CurrencySymbol)) return currency.CurrencySymbol.Trim();
                if (!string.IsNullOrWhiteSpace(currency.CurrencyCode)) return currency.CurrencyCode.Trim();
            }
            catch
            {
                // Preserve the existing display-only fallback when settings cannot be read.
            }
            return "ر.س";
        }
    }
}

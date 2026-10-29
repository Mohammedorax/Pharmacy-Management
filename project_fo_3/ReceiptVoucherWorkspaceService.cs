using System;
using System.Collections.Generic;
using System.Data;

namespace project_fo_3
{
    // قراءة بيانات السند في طبقة مستقلة، بحيث لا ترتبط عناصر WinForms بخدمات الحسابات مباشرة.
    internal sealed class ReceiptVoucherInitialDataSnapshot
    {
        internal ReceiptVoucherInitialDataSnapshot(IList<string> accounts, IList<string> costCenters, IList<string> currencies, string systemCurrencyCode, DataTable history)
        {
            Accounts = new List<string>(accounts ?? new List<string>());
            CostCenters = new List<string>(costCenters ?? new List<string>());
            Currencies = new List<string>(currencies ?? new List<string>());
            SystemCurrencyCode = systemCurrencyCode ?? string.Empty;
            History = history ?? new DataTable();
        }

        internal IList<string> Accounts { get; private set; }
        internal IList<string> CostCenters { get; private set; }
        internal IList<string> Currencies { get; private set; }
        internal string SystemCurrencyCode { get; private set; }
        internal DataTable History { get; private set; }
    }

    internal static class ReceiptVoucherWorkspaceService
    {
        internal static async System.Threading.Tasks.Task<ReceiptVoucherInitialDataSnapshot> LoadInitialAsync(
            string voucherType, System.Threading.CancellationToken cancellationToken)
        {
            // Capture both readers before the first await: one connection/session for the entire editor load.
            var lookups = ApplicationServices.CreateJournalEditorReadSession();
            var history = ApplicationServices.CreateVoucherHistoryReadSession();
            cancellationToken.ThrowIfCancellationRequested();
            if (!lookups.IsCurrent || !history.IsCurrent) throw new UnauthorizedAccessException("تغيرت جلسة خيارات السند.");
            var snapshot = await lookups.Service.LoadAsync(lookups.Actor, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!lookups.IsCurrent || !history.IsCurrent) throw new UnauthorizedAccessException("تغيرت جلسة خيارات السند.");
            var rows = await history.Service.LoadAsync(HistoryRequest(voucherType), history.Actor, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!lookups.IsCurrent || !history.IsCurrent) throw new UnauthorizedAccessException("تغيرت جلسة خيارات السند.");
            var accounts = new List<string>(); var centers = new List<string>(); var currencies = new List<string>();
            foreach (var row in snapshot.Accounts) accounts.Add(row.Code + " - " + row.Name);
            foreach (var row in snapshot.CostCenters) centers.Add(row.Id + " - " + row.Name);
            foreach (var row in snapshot.Currencies) if (!currencies.Contains(row.Code.Trim())) currencies.Add(row.Code.Trim());
            return new ReceiptVoucherInitialDataSnapshot(accounts, centers, currencies, snapshot.SystemCurrency.Code.Trim(), VoucherTableProjection.Create(rows));
        }

        internal static DataTable LoadHistory(string voucherType)
        {
            var session=ApplicationServices.CreateVoucherHistoryReadSession();
            return VoucherTableProjection.Create(session.Service.Load(HistoryRequest(voucherType),session.Actor));
        }
        internal static async System.Threading.Tasks.Task<DataTable> LoadHistoryAsync(string voucherType)
            => await LoadHistoryAsync(voucherType,System.Threading.CancellationToken.None);
        internal static async System.Threading.Tasks.Task<DataTable> LoadHistoryAsync(string voucherType,System.Threading.CancellationToken cancellationToken)
        {
            var session=ApplicationServices.CreateVoucherHistoryReadSession();
            var rows=await session.Service.LoadAsync(HistoryRequest(voucherType),session.Actor,cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();if(!session.IsCurrent)throw new UnauthorizedAccessException("تغيرت جلسة سجل السندات.");
            return VoucherTableProjection.Create(rows);
        }
        private static PharmacyManagement.Application.VoucherHistoryReadRequest HistoryRequest(string type)
            => new PharmacyManagement.Application.VoucherHistoryReadRequest(DateTime.Today.AddMonths(-1), DateTime.Today, type);

    }
}

using System;
using System.Collections.Generic;
using System.Data;

namespace project_fo_3
{
    // يجمع خيارات القيد في لقطة مستقلة عن الواجهة كي يبقى فتح نموذج القيود مستجيبًا.
    internal sealed class JournalEntryLookupSnapshot
    {
        internal JournalEntryLookupSnapshot(IList<string> accounts, IList<string> currencies, string systemCurrencyCode, IList<string> costCenters = null)
        {
            Accounts = new List<string>(accounts ?? new List<string>());
            Currencies = new List<string>(currencies ?? new List<string>());
            SystemCurrencyCode = systemCurrencyCode ?? string.Empty;
            CostCenters = new List<string>(costCenters ?? new List<string>());
        }

        internal IList<string> CostCenters { get; private set; }
        internal IList<string> Accounts { get; private set; }
        internal IList<string> Currencies { get; private set; }
        internal string SystemCurrencyCode { get; private set; }
    }

    internal static class JournalEntryLookupService
    {
        internal static async System.Threading.Tasks.Task<JournalEntryLookupSnapshot> LoadAsync(System.Threading.CancellationToken cancellationToken)
        {
            var session=ApplicationServices.CreateJournalEditorReadSession();
            var snapshot=await session.Service.LoadAsync(session.Actor,cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();if(!session.IsCurrent)throw new UnauthorizedAccessException("تغيرت جلسة خيارات القيد.");
            var accounts=new List<string>();var currencies=new List<string>();var centers=new List<string>();
            foreach(var row in snapshot.Accounts)accounts.Add(row.Code+" - "+row.Name);
            foreach(var row in snapshot.Currencies)if(!currencies.Contains(row.Code.Trim()))currencies.Add(row.Code.Trim());
            foreach(var row in snapshot.CostCenters)centers.Add(row.Id+" - "+row.Name);
            return new JournalEntryLookupSnapshot(accounts,currencies,snapshot.SystemCurrency.Code.Trim(),centers);
        }
        internal static async System.Threading.Tasks.Task<DataTable> LoadQuickAccountsAsync(System.Threading.CancellationToken token)
        {
            var session=ApplicationServices.CreateJournalEditorReadSession();
            var snapshot=await session.Service.LoadAsync(session.Actor,token);
            token.ThrowIfCancellationRequested();
            if(!session.IsCurrent)throw new UnauthorizedAccessException("تغيرت جلسة خيارات القيد السريع.");
            var rows=AccountingLookupTableProjection.Accounts(snapshot.Accounts);
            rows.ExtendedProperties["SystemCurrencyCode"]=snapshot.SystemCurrency.Code.Trim().ToUpperInvariant();
            return rows;
        }
        internal static JournalEntryLookupSnapshot Load()
        {
            var accounts = new List<string>();
            var currencies = new List<string>();
            DataTable accountRows = AccountingLookupDesktopService.LoadAccounts(true);
            foreach (DataRow row in accountRows.Rows)
            {
                string code = Convert.ToString(row["AccountCode"]);
                string name = Convert.ToString(row["AccountName"]);
                if (!string.IsNullOrWhiteSpace(code)) accounts.Add(code + " - " + name);
            }

            string systemCode = string.Empty;
            try
            {
                CurrencyInfo systemCurrency = CurrencyWorkspaceService.GetSystemCurrency();
                systemCode = string.IsNullOrWhiteSpace(systemCurrency.CurrencyCode) ? systemCurrency.CurrencySymbol : systemCurrency.CurrencyCode;
                DataTable currencyRows = CurrencyWorkspaceService.GetCurrencies();
                foreach (DataRow row in currencyRows.Rows)
                {
                    string code = Convert.ToString(row[3]);
                    if (!string.IsNullOrWhiteSpace(code) && !currencies.Contains(code)) currencies.Add(code);
                }
            }
            catch
            {
                systemCode = CurrencyWorkspaceService.GetSystemCurrencyDisplay();
            }

            return new JournalEntryLookupSnapshot(accounts, currencies, systemCode);
        }
    }
}

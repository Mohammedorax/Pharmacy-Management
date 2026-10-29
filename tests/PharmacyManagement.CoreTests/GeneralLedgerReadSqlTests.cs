using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading;
using PharmacyManagement.Application;
using PharmacyManagement.Infrastructure.SqlServer;
namespace PharmacyManagement.CoreTests
{
    internal static class GeneralLedgerReadSqlTests
    {
        internal static void VerifyQaReadOnly(string server, string database)
        {
            string name = (database ?? "").ToLowerInvariant();
            if (!(name.EndsWith("_qa") || name.EndsWith("_test") || name.EndsWith("_sandbox"))) throw new Exception("Explicit QA suffix required.");
            var builder = new SqlConnectionStringBuilder { DataSource = server, InitialCatalog = database, IntegratedSecurity = true,
                ConnectTimeout = 5, ApplicationName = "Pharmacy.CoreTests.Ledger.ReadOnly" };
            var seed = ReadSeed(builder.ConnectionString, database);
            if (seed.Count == 0) throw new Exception("QA ledger requires existing entries for comparison.");
            var accounts = seed.GroupBy(row => row.Account).OrderByDescending(group => group.Count()).Take(3).Select(group => group.Key).ToArray();
            DateTime first = seed.Min(row => row.Date).Date, last = seed.Max(row => row.Date).Date;
            DateTime from = first.AddDays(Math.Min(30, (last - first).Days));
            int? center = seed.Where(row => row.CenterId.HasValue).Select(row => row.CenterId).FirstOrDefault();
            var centers = new List<int?> { null, 0 }; if (center.HasValue) centers.Add(center);
            var repository = new SqlGeneralLedgerReadRepository(() => new SqlConnection(builder.ConnectionString));
            int cases = 0, compared = 0;
            foreach (string account in accounts)
            foreach (bool previous in new[] { false, true })
            foreach (bool opening in new[] { false, true })
            foreach (string source in new[] { (string)null, "Manual", "Voucher", "SalesInvoice", "PurchaseInvoice", "OpeningEntry" })
            foreach (int? costCenter in centers)
            {
                var request = new GeneralLedgerReadRequest(account, from, last, source, previous, opening, costCenter);
                var raw = seed.Where(row => row.Account == account && row.Date.Date <= last && (source == null || row.SourceType == source) &&
                    (opening || row.SourceType != "OpeningEntry") && (!costCenter.HasValue || (costCenter == 0 ? !row.CenterId.HasValue : row.CenterId == costCenter)))
                    .OrderBy(row => row.Date).ThenBy(row => row.Document).ThenBy(row => row.Line).ToArray();
                decimal before = previous ? raw.Where(row => row.Date < from).Sum(row => (row.Debit - row.Credit) ?? 0m) : 0m;
                var expected = new List<GeneralLedgerReadRow>(); decimal running = before;
                foreach (var row in raw.Where(row => row.Date >= from))
                {
                    running += (row.Debit - row.Credit) ?? 0m;
                    expected.Add(new GeneralLedgerReadRow(row.Date, row.Document, row.SourceType, row.SourceId,
                        row.CenterName, row.Description, row.Debit, row.Credit, running));
                }
                var actual = repository.LoadAsync(request, CancellationToken.None).GetAwaiter().GetResult();
                if (actual.PreviousBalance != before || actual.EndingBalance != running || actual.Rows.Count != expected.Count ||
                    actual.PeriodDebit != expected.Sum(row => row.Debit ?? 0m) || actual.PeriodCredit != expected.Sum(row => row.Credit ?? 0m))
                    throw new Exception("Ledger aggregate/count parity failed at case " + cases);
                for (int i = 0; i < expected.Count; i++)
                {
                    var a = actual.Rows[i]; var e = expected[i];
                    if (a.Date != e.Date || a.DocumentId != e.DocumentId || a.SourceType != e.SourceType || a.SourceId != e.SourceId ||
                        a.CostCenterName != e.CostCenterName || a.Description != e.Description || a.Debit != e.Debit || a.Credit != e.Credit || a.Balance != e.Balance)
                        throw new Exception("Ledger raw row/order parity failed at case " + cases);
                }
                cases++; compared += expected.Count;
            }
            var empty = repository.LoadAsync(new GeneralLedgerReadRequest(accounts[0], last.AddDays(1), last.AddDays(2), null, true, true, null), CancellationToken.None).GetAwaiter().GetResult();
            if (empty.Rows.Count != 0 || empty.EndingBalance != empty.PreviousBalance) throw new Exception("Empty ledger closing balance changed.");
            if (!SameSeed(seed, ReadSeed(builder.ConnectionString, database))) throw new Exception("QA ledger source changed during comparison; evidence rejected.");
            Console.WriteLine("PASS QA ledger read-only: " + cases + " filter cases; " + compared + " rows compared to independent raw-line calculation; empty period; source unchanged; no SQL writes.");
        }
        private static List<Raw> ReadSeed(string connectionString, string database)
        {
            var rows = new List<Raw>();
            using (var connection = new SqlConnection(connectionString)) using (var command = connection.CreateCommand())
            {
                connection.Open(); command.CommandText = "SELECT DB_NAME()";
                if (!string.Equals(Convert.ToString(command.ExecuteScalar()), database, StringComparison.OrdinalIgnoreCase)) throw new Exception("Actual QA database mismatch.");
                command.CommandText = @"SELECT l.AccountCode,e.EntryDate,e.JournalEntryId,l.JournalEntryLineId,ISNULL(e.SourceType,N'Manual'),ISNULL(e.SourceId,N''),
l.CostCenterId,ISNULL(c.CostCenterName,N''),e.Description,l.Debit,l.Credit FROM dbo.JournalEntryLinesTb l
JOIN dbo.JournalEntriesTb e ON e.JournalEntryId=l.JournalEntryId LEFT JOIN dbo.CostCentersTb c ON c.CostCenterId=l.CostCenterId
ORDER BY l.JournalEntryLineId";
                using (var reader = command.ExecuteReader()) while (reader.Read()) rows.Add(new Raw {
                    Account=reader.GetString(0), Date=reader.GetDateTime(1), Document=reader.GetInt32(2), Line=reader.GetInt32(3), SourceType=reader.GetString(4), SourceId=reader.GetString(5),
                    CenterId=reader.IsDBNull(6)?(int?)null:reader.GetInt32(6), CenterName=reader.GetString(7), Description=reader.IsDBNull(8)?null:reader.GetString(8),
                    Debit=reader.IsDBNull(9)?(decimal?)null:reader.GetDecimal(9), Credit=reader.IsDBNull(10)?(decimal?)null:reader.GetDecimal(10) });
            }
            return rows;
        }
        private static bool SameSeed(List<Raw> before, List<Raw> after)
        {
            if (before.Count != after.Count) return false;
            for (int i=0;i<before.Count;i++) {var a=before[i];var b=after[i];if(a.Account!=b.Account||a.Date!=b.Date||a.Document!=b.Document||a.Line!=b.Line||a.SourceType!=b.SourceType||a.SourceId!=b.SourceId||a.CenterId!=b.CenterId||a.CenterName!=b.CenterName||a.Description!=b.Description||a.Debit!=b.Debit||a.Credit!=b.Credit)return false;}
            return true;
        }
        private sealed class Raw
        { internal string Account,SourceType,SourceId,CenterName,Description;internal DateTime Date;internal int Document,Line;internal int? CenterId;internal decimal? Debit,Credit; }
    }
}
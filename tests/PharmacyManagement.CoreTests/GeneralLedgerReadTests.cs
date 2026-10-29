using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Application;
namespace PharmacyManagement.CoreTests
{
    internal static class GeneralLedgerReadTests
    {
        internal static int Run()
        {
            int checks = 0;
            var request = Request(" 1100 ", null, 0);
            Check(request.AccountCode == "1100" && request.CostCenterId == 0 && request.ExclusiveEnd == new DateTime(2024, 3, 1), ref checks);
            foreach (string source in new[] { null, "Manual", "Voucher", "SalesInvoice", "PurchaseInvoice", "OpeningEntry" })
                Check(Request("1100", source, null).SourceType == source, ref checks);
            Check(Request("1100", " Manual ", 2).SourceType == "Manual", ref checks);
            foreach (string bad in new[] { null, " ", new string('1', 21) })
                Check(Rejects<ArgumentException>(() => Request(bad, null, null)), ref checks);
            Check(Rejects<ArgumentException>(() => Request("1100", "Unknown", null)), ref checks);
            Check(Rejects<ArgumentException>(() => Request("1100", null, -1)), ref checks);
            Check(Rejects<ArgumentException>(() => new GeneralLedgerReadRequest("1100", DateTime.MinValue, DateTime.Today, null, true, true, null)), ref checks);
            Check(Rejects<ArgumentException>(() => new GeneralLedgerReadRequest("1100", DateTime.Today, DateTime.Today.AddDays(-1), null, true, true, null)), ref checks);
            Check(Rejects<ArgumentException>(() => new GeneralLedgerReadRequest("1100", DateTime.Today, DateTime.MaxValue, null, true, true, null)), ref checks);
            var rows = new List<GeneralLedgerReadRow> {
                new GeneralLedgerReadRow(new DateTime(2024,2,29), 2, "Manual", "A", "", null, 100.125m, null, 50.125m),
                new GeneralLedgerReadRow(new DateTime(2024,2,29), 1, "Voucher", "B", "", "باراسيتامول 500 mg", null, 25.005m, 25.120m) };
            var result = new GeneralLedgerReadResult(rows, -50m); rows.Clear();
            Check(result.Rows.Count == 2 && result.Rows[0].Description == null && result.Rows[0].Credit == null, ref checks);
            Check(result.PeriodDebit == 100.125m && result.PeriodCredit == 25.005m && result.PreviousBalance == -50m && result.EndingBalance == 25.120m, ref checks);
            Check(result.Rows[0].DocumentId == 2 && result.Rows[1].DocumentId == 1, ref checks);
            Check(new GeneralLedgerReadResult(new GeneralLedgerReadRow[0], -50m).EndingBalance == -50m, ref checks);
            Check(Rejects<ArgumentNullException>(() => new GeneralLedgerReadResult(null, 0)), ref checks);
            Check(Rejects<ArgumentException>(() => new GeneralLedgerReadResult(new GeneralLedgerReadRow[] { null }, 0)), ref checks);
            var repository = new Probe();
            var actor = new OperationActor { UserName = "ledger", UserRole = "Manager" };
            bool allowed = true;
            var service = new GeneralLedgerReadService(repository, value => allowed && value.UserName == "ledger" && value.UserRole == "Manager");
            Check(Rejects<ArgumentNullException>(() => service.LoadAsync(null, actor, CancellationToken.None).GetAwaiter().GetResult()), ref checks);
            Check(Rejects<UnauthorizedAccessException>(() => new GeneralLedgerReadService(repository).LoadAsync(request, actor, CancellationToken.None).GetAwaiter().GetResult()), ref checks);
            Check(Rejects<UnauthorizedAccessException>(() => service.LoadAsync(request, null, CancellationToken.None).GetAwaiter().GetResult()), ref checks);
            Check(repository.Calls == 0, ref checks);
            using (var canceled = new CancellationTokenSource())
            {
                canceled.Cancel();
                Check(Rejects<OperationCanceledException>(() => service.LoadAsync(request, actor, canceled.Token).GetAwaiter().GetResult()) && repository.Calls == 0, ref checks);
            }
            var reading = service.LoadAsync(request, actor, CancellationToken.None);
            actor.UserName = "changed"; repository.Pending.SetResult(result);
            Check(ReferenceEquals(reading.GetAwaiter().GetResult(), result) && ReferenceEquals(repository.Request, request), ref checks);
            actor.UserName = "ledger"; repository.Pending = new TaskCompletionSource<GeneralLedgerReadResult>();
            reading = service.LoadAsync(request, actor, CancellationToken.None); allowed = false; repository.Pending.SetResult(result);
            Check(Rejects<UnauthorizedAccessException>(() => reading.GetAwaiter().GetResult()), ref checks); allowed = true;
            using (var canceled = new CancellationTokenSource())
            {
                repository.Pending = new TaskCompletionSource<GeneralLedgerReadResult>();
                reading = service.LoadAsync(request, actor, canceled.Token);
                Check(repository.Token == canceled.Token, ref checks); canceled.Cancel(); repository.Pending.SetResult(result);
                Check(Rejects<OperationCanceledException>(() => reading.GetAwaiter().GetResult()), ref checks);
            }
            repository.Pending = new TaskCompletionSource<GeneralLedgerReadResult>(); reading = service.LoadAsync(request, actor, CancellationToken.None); repository.Pending.SetResult(null);
            Check(Rejects<InvalidOperationException>(() => reading.GetAwaiter().GetResult()), ref checks);
            repository.Pending = new TaskCompletionSource<GeneralLedgerReadResult>(); reading = service.LoadAsync(request, actor, CancellationToken.None); repository.Pending.SetException(new TimeoutException("fixture"));
            Check(Rejects<TimeoutException>(() => reading.GetAwaiter().GetResult()), ref checks);
            Console.WriteLine("PASS: " + checks + " general ledger request/summary/identity/authorization/revocation/cancellation/error contracts; no SQL.");
            return checks;
        }
        private static GeneralLedgerReadRequest Request(string account, string source, int? costCenter)
            => new GeneralLedgerReadRequest(account, new DateTime(2024,2,1,12,0,0), new DateTime(2024,2,29,23,0,0), source, true, true, costCenter);
        private static bool Rejects<T>(Action action) where T:Exception { try { action(); return false; } catch(T) { return true; } }
        private static void Check(bool condition, ref int count) { if (!condition) throw new Exception("General ledger contract failed."); count++; }
        private sealed class Probe : IGeneralLedgerReadRepository
        {
            internal int Calls; internal GeneralLedgerReadRequest Request; internal CancellationToken Token;
            internal TaskCompletionSource<GeneralLedgerReadResult> Pending = new TaskCompletionSource<GeneralLedgerReadResult>();
            public Task<GeneralLedgerReadResult> LoadAsync(GeneralLedgerReadRequest request, CancellationToken cancellationToken)
            { Calls++; Request = request; Token = cancellationToken; return Pending.Task; }
        }
    }
}
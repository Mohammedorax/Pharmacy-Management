using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Application;
using PharmacyManagement.Infrastructure.SqlServer;
namespace PharmacyManagement.CoreTests
{
    internal static class VoucherHistoryReadTests
    {
        internal static int Run()
        {
            int count = 0;
            var request = new VoucherHistoryReadRequest(new DateTime(2024, 2, 1, 12, 0, 0), new DateTime(2024, 2, 29), " ReceiptExternal ");
            Check(request.From == new DateTime(2024, 2, 1) && request.ExclusiveEnd == new DateTime(2024, 3, 1) && request.Type == "ReceiptExternal", ref count);
            Check(new VoucherHistoryReadRequest(new DateTime(1753, 1, 1), DateTime.MaxValue, null).ExclusiveEnd == null, ref count);
            Reject<ArgumentException>(() => new VoucherHistoryReadRequest(DateTime.Today, DateTime.Today.AddDays(-1), ""), ref count);
            Reject<ArgumentException>(() => new VoucherHistoryReadRequest(new DateTime(1752, 1, 1), DateTime.Today, ""), ref count);
            Reject<ArgumentException>(() => new VoucherHistoryReadRequest(DateTime.Today, DateTime.Today, new string('x', 51)), ref count);
            var repo = new Fake(); bool allowed = false;
            var service = new VoucherHistoryReadService(repo, actor => allowed && actor.UserName == "admin" && actor.UserRole == "Manager");
            var manager = new OperationActor { UserName = "admin", UserRole = "Manager" };
            Reject<UnauthorizedAccessException>(() => service.Load(request, manager), ref count);
            allowed = true;
            Reject<UnauthorizedAccessException>(() => service.Load(request, null), ref count);
            Reject<UnauthorizedAccessException>(() => service.Load(request, new OperationActor { UserName = "other", UserRole = "Manager" }), ref count);
            Reject<ArgumentNullException>(() => service.Load(null, manager), ref count);
            Check(repo.Calls == 0, ref count);
            var result = service.LoadAsync(request, manager).GetAwaiter().GetResult();
            Check(result.Count == 0 && repo.Calls == 1 && ReferenceEquals(repo.Request, request), ref count);
            repo.Rows.Add(new VoucherDocumentSnapshot(42, request.From, "ReceiptExternal", "عميل Pharmacy", "1100", 25.37m, "", 99, "admin"));
            result = service.Load(request, manager); repo.Rows.Clear();
            Check(result.Count == 1 && result[0].Amount == 25.37m, ref count);
            repo.Fail = true; Reject<InvalidOperationException>(() => service.Load(request, manager), ref count);
            repo.Fail = false; repo.AfterRead = () => allowed = false;
            Reject<UnauthorizedAccessException>(() => service.Load(request, manager), ref count);
            allowed = true;
            foreach (bool asynchronous in new[] { false, true })
            {
                manager.UserName = "admin"; manager.UserRole = "Manager";
                repo.AfterRead = () => { manager.UserName = "other"; manager.UserRole = "Cashier"; };
                result = asynchronous ? service.LoadAsync(request, manager).GetAwaiter().GetResult() : service.Load(request, manager);
                Check(result.Count == 0 && manager.UserName == "other", ref count);
            }
            repo.AfterRead = null;
            manager.UserName="admin";manager.UserRole="Manager";allowed=true;
            using(var cancelled=new CancellationTokenSource())
            {
                cancelled.Cancel();int before=repo.Calls;
                Reject<OperationCanceledException>(()=>service.LoadAsync(request,manager,cancelled.Token).GetAwaiter().GetResult(),ref count);
                Check(repo.Calls==before,ref count);
            }
            var pending=new TaskCompletionSource<IReadOnlyList<VoucherDocumentSnapshot>>();repo.Async=token=>pending.Task;
            var reading=service.LoadAsync(request,manager,CancellationToken.None);allowed=false;pending.SetResult(new List<VoucherDocumentSnapshot>());
            Reject<UnauthorizedAccessException>(()=>reading.GetAwaiter().GetResult(),ref count);allowed=true;
            using(var cancel=new CancellationTokenSource())
            {
                pending=new TaskCompletionSource<IReadOnlyList<VoucherDocumentSnapshot>>();reading=service.LoadAsync(request,manager,cancel.Token);
                Check(repo.Token==cancel.Token,ref count);cancel.Cancel();pending.SetResult(new List<VoucherDocumentSnapshot>());
                Reject<OperationCanceledException>(()=>reading.GetAwaiter().GetResult(),ref count);
            }
            pending=new TaskCompletionSource<IReadOnlyList<VoucherDocumentSnapshot>>();reading=service.LoadAsync(request,manager,CancellationToken.None);
            manager.UserName="other";manager.UserRole="Cashier";pending.SetResult(new List<VoucherDocumentSnapshot>());
            Check(reading.GetAwaiter().GetResult().Count==0,ref count);manager.UserName="admin";manager.UserRole="Manager";
            repo.Async=token=>Task.FromResult<IReadOnlyList<VoucherDocumentSnapshot>>(null);
            Reject<InvalidOperationException>(()=>service.LoadAsync(request,manager,CancellationToken.None).GetAwaiter().GetResult(),ref count);
            var expectedError=new TimeoutException("controlled timeout");repo.Async=token=>{throw expectedError;};
            try{service.LoadAsync(request,manager,CancellationToken.None).GetAwaiter().GetResult();throw new InvalidOperationException("Missing repository failure");}
            catch(TimeoutException error){Check(ReferenceEquals(error,expectedError),ref count);}
            repo.Async=null;repo.Rows.Add(new VoucherDocumentSnapshot(7,request.From,"ReceiptExternal","عميل","1100",5m,"",9,"admin"));
            result=service.LoadAsync(request,manager,CancellationToken.None).GetAwaiter().GetResult();repo.Rows.Clear();Check(result.Count==1,ref count);
            Console.WriteLine("PASS: " + count + " voucher history range/read/authorization contracts; no database connection.");
            return count;
        }
        private static void Check(bool value, ref int count) { if (!value) throw new InvalidOperationException("Voucher history contract changed."); count++; }
        private static void Reject<T>(Action action, ref int count) where T : Exception
        { try { action(); } catch (T) { count++; return; } throw new InvalidOperationException("Expected " + typeof(T).Name); }
        internal static void VerifyQaReadOnly(string server, string database)
        {
            if (string.IsNullOrWhiteSpace(database) || !database.EndsWith("_qa", StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("Explicit _qa database required before connecting.");
            var builder = new SqlConnectionStringBuilder { DataSource = server, InitialCatalog = database,
                IntegratedSecurity = true, ConnectTimeout = 10, ApplicationName = "Pharmacy.CoreTests.VoucherHistory.ReadOnly" };
            var source = new DataTable();
            using (var connection = new SqlConnection(builder.ConnectionString))
            using (var command = connection.CreateCommand())
            {
                connection.Open(); command.CommandText = "SELECT DB_NAME();";
                if (!string.Equals(Convert.ToString(command.ExecuteScalar()), database, StringComparison.OrdinalIgnoreCase))
                    throw new UnauthorizedAccessException("QA identity mismatch.");
                command.CommandText = "SELECT VoucherId, VoucherDate, VoucherType, PartyName, AccountCode, Amount, Notes, JournalEntryId, CreatedBy FROM dbo.AccountingVouchersTb;";
                using (var adapter = new SqlDataAdapter(command)) adapter.Fill(source);
            }
            var repo = new SqlVoucherHistoryReadRepository(() => new SqlConnection(builder.ConnectionString));
            int cases = 0;
            foreach (var request in new[] {
                new VoucherHistoryReadRequest(new DateTime(1753, 1, 1), DateTime.MaxValue, ""),
                new VoucherHistoryReadRequest(DateTime.Today.AddMonths(-1), DateTime.Today, "ReceiptExternal"),
                new VoucherHistoryReadRequest(DateTime.Today, DateTime.Today, "PaymentInternal") })
            {
                var expected = source.Rows.Cast<DataRow>().Where(row => (DateTime)row["VoucherDate"] >= request.From &&
                    (!request.ExclusiveEnd.HasValue || (DateTime)row["VoucherDate"] < request.ExclusiveEnd.Value) &&
                    (request.Type == "" || string.Equals(Convert.ToString(row["VoucherType"]), request.Type, StringComparison.OrdinalIgnoreCase)))
                    .OrderByDescending(row => (DateTime)row["VoucherDate"]).ThenByDescending(row => (int)row["VoucherId"]).ToArray();
                var service=new VoucherHistoryReadService(repo,actor=>actor.UserName=="qa"&&actor.UserRole=="Manager");
                var actual=service.LoadAsync(request,new OperationActor{UserName="qa",UserRole="Manager"},CancellationToken.None).GetAwaiter().GetResult();
                if (actual.Count != expected.Length) throw new InvalidOperationException("QA history count differs from independently filtered source.");
                for (int index = 0; index < actual.Count; index++)
                    if (actual[index].VoucherId != (int)expected[index]["VoucherId"] || actual[index].Amount != (decimal)expected[index]["Amount"] ||
                        actual[index].Date!=(DateTime)expected[index]["VoucherDate"] || actual[index].Type!=Convert.ToString(expected[index]["VoucherType"]) ||
                        actual[index].PartyName!=Convert.ToString(expected[index]["PartyName"]) || actual[index].AccountCode!=Convert.ToString(expected[index]["AccountCode"]) ||
                        actual[index].Notes!=Convert.ToString(expected[index]["Notes"]) || actual[index].CreatedBy!=Convert.ToString(expected[index]["CreatedBy"]) ||
                        actual[index].JournalEntryId!=(expected[index].IsNull("JournalEntryId")?(int?)null:Convert.ToInt32(expected[index]["JournalEntryId"])))
                        throw new InvalidOperationException("QA history identity/order/amount differs.");
                cases++;
            }
            Console.WriteLine("PASS: QA voucher history cases=" + cases + "; SourceRows=" + source.Rows.Count +
                "; PersistentWrites=0. Empty source does not prove row mapping.");
        }
        private sealed class Fake : IVoucherHistoryReadRepository
        {
            internal int Calls; internal VoucherHistoryReadRequest Request; internal bool Fail; internal Action AfterRead;
            internal List<VoucherDocumentSnapshot> Rows = new List<VoucherDocumentSnapshot>();
            internal CancellationToken Token;internal Func<CancellationToken,Task<IReadOnlyList<VoucherDocumentSnapshot>>> Async;
            public Task<IReadOnlyList<VoucherDocumentSnapshot>> LoadAsync(VoucherHistoryReadRequest request,CancellationToken cancellationToken)
            {Token=cancellationToken;if(Async!=null){Calls++;Request=request;return Async(cancellationToken);}return Task.FromResult(Load(request));}
            public IReadOnlyList<VoucherDocumentSnapshot> Load(VoucherHistoryReadRequest request)
            { Calls++; Request = request; if (Fail) throw new InvalidOperationException("Simulated history failure"); AfterRead?.Invoke(); return Rows; }
        }
    }
}

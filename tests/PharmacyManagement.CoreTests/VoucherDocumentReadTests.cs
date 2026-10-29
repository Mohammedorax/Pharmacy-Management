using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Data;
using System.Data.SqlClient;
using PharmacyManagement.Application;
using PharmacyManagement.Infrastructure.SqlServer;

namespace PharmacyManagement.CoreTests
{
    internal static class VoucherDocumentReadTests
    {
        internal static int Run()
        {
            int checks = 0;
            Reject<ArgumentOutOfRangeException>(() => new VoucherDocumentReadRequest(0), ref checks);
            Reject<ArgumentOutOfRangeException>(() => new VoucherDocumentReadRequest(-1), ref checks);
            var repo = new Fake(); bool allowed = false;
            Reject<ArgumentNullException>(() => new VoucherDocumentReadService(null, actor => true), ref checks);
            Reject<ArgumentNullException>(() => new VoucherDocumentReadService(repo, null), ref checks);
            var service = new VoucherDocumentReadService(repo, actor => allowed && actor.UserName == "admin" && actor.UserRole == "Manager");
            var manager = new OperationActor { UserName = "admin", UserRole = "Manager" };
            var request = new VoucherDocumentReadRequest(42);
            Reject<UnauthorizedAccessException>(() => service.LoadAsync(null, manager).GetAwaiter().GetResult(), ref checks);
            allowed = true;
            Reject<UnauthorizedAccessException>(() => service.LoadAsync(request, null).GetAwaiter().GetResult(), ref checks);
            Reject<UnauthorizedAccessException>(() => service.LoadAsync(request, new OperationActor { UserName = "admin", UserRole = "Cashier" }).GetAwaiter().GetResult(), ref checks);
            Reject<UnauthorizedAccessException>(() => service.LoadAsync(request, new OperationActor { UserName = "other", UserRole = "Manager" }).GetAwaiter().GetResult(), ref checks);
            Reject<ArgumentNullException>(() => service.LoadAsync(null, manager).GetAwaiter().GetResult(), ref checks);
            Check(repo.Calls == 0, ref checks);
            var original = new VoucherDocumentSnapshot(42, new DateTime(2024, 2, 29, 13, 45, 0), "PaymentInternal",
                "  صيدلية Pharmacy 500 mg  ", "1000", 1250.37m, "شيك — Bank 123", 99, "admin");
            repo.Value = original;
            var result = service.LoadAsync(request, manager).GetAwaiter().GetResult();
            Check(ReferenceEquals(result, original) && ReferenceEquals(repo.Request, request) && repo.Calls == 1, ref checks);
            Check(result.PartyName == "  صيدلية Pharmacy 500 mg  " && result.Amount == 1250.37m && result.Date.Hour == 13 &&
                result.JournalEntryId == 99 && result.Notes == "شيك — Bank 123", ref checks);
            Check(typeof(VoucherDocumentSnapshot).GetProperties().All(property => !property.CanWrite), ref checks);
            Check(original.SystemCurrencyCode==null,ref checks);
            var usdDocument=new VoucherDocumentSnapshot(original.VoucherId,original.Date,original.Type,original.PartyName,
                original.AccountCode,original.Amount,original.Notes,original.JournalEntryId,original.CreatedBy," usd ");
            Check(usdDocument.SystemCurrencyCode=="USD",ref checks);
            Check(usdDocument.PartyName==original.PartyName&&usdDocument.Amount==original.Amount&&usdDocument.Notes==original.Notes&&usdDocument.JournalEntryId==original.JournalEntryId,ref checks);
            foreach(string invalid in new[]{""," ","ABCDEFGHIJK"})
                Reject<ArgumentException>(()=>new VoucherDocumentSnapshot(42,original.Date,original.Type,original.PartyName,original.AccountCode,original.Amount,original.Notes,original.JournalEntryId,original.CreatedBy,invalid),ref checks);
            repo.Value=usdDocument;
            Check(service.LoadAsync(request,manager).GetAwaiter().GetResult().SystemCurrencyCode=="USD",ref checks);
            repo.Value=original;
            repo.Value = null;
            Reject<InvalidOperationException>(() => service.LoadAsync(request, manager).GetAwaiter().GetResult(), ref checks);
            repo.Value = new VoucherDocumentSnapshot(7, DateTime.Today, "ReceiptExternal", "Other", "1100", 1m, "", null, "");
            Reject<InvalidOperationException>(() => service.LoadAsync(request, manager).GetAwaiter().GetResult(), ref checks);
            repo.Value = original; var error = new InvalidOperationException("Simulated read failure"); repo.Failure = error;
            bool exactError = false;
            try { service.LoadAsync(request, manager).GetAwaiter().GetResult(); }
            catch (InvalidOperationException caught) { exactError = ReferenceEquals(error, caught); }
            Check(exactError, ref checks);
            repo.Failure = null; repo.BeforeReturn = () => allowed = false;
            Reject<UnauthorizedAccessException>(() => service.LoadAsync(request, manager).GetAwaiter().GetResult(), ref checks);
            allowed = true; manager.UserName = "admin"; manager.UserRole = "Manager";
            repo.BeforeReturn = () => { manager.UserName = "other"; manager.UserRole = "Cashier"; };
            result = service.LoadAsync(request, manager).GetAwaiter().GetResult();
            Check(ReferenceEquals(result, original) && manager.UserName == "other", ref checks);
            manager.UserName="admin";manager.UserRole="Manager";repo.BeforeReturn=null;
            Reject<UnauthorizedAccessException>(()=>service.LoadAsync(request,new OperationActor{UserName="admin",UserRole=""},CancellationToken.None).GetAwaiter().GetResult(),ref checks);
            using(var cancellation=new CancellationTokenSource())
            {
                int calls=repo.Calls;cancellation.Cancel();Reject<OperationCanceledException>(()=>service.LoadAsync(request,manager,cancellation.Token).GetAwaiter().GetResult(),ref checks);Check(repo.Calls==calls,ref checks);
            }
            repo.Pending=new TaskCompletionSource<VoucherDocumentSnapshot>();
            var pending=service.LoadAsync(request,manager,CancellationToken.None);allowed=false;repo.Pending.SetResult(original);
            Reject<UnauthorizedAccessException>(()=>pending.GetAwaiter().GetResult(),ref checks);allowed=true;
            using(var cancellation=new CancellationTokenSource())
            {
                repo.Pending=new TaskCompletionSource<VoucherDocumentSnapshot>();pending=service.LoadAsync(request,manager,cancellation.Token);
                Check(repo.Token==cancellation.Token&&ReferenceEquals(repo.Request,request),ref checks);cancellation.Cancel();repo.Pending.SetResult(original);
                Reject<OperationCanceledException>(()=>pending.GetAwaiter().GetResult(),ref checks);
            }
            repo.Pending=new TaskCompletionSource<VoucherDocumentSnapshot>();pending=service.LoadAsync(request,manager,CancellationToken.None);manager.UserName="forged";manager.UserRole="Cashier";repo.Pending.SetResult(original);
            Check(ReferenceEquals(pending.GetAwaiter().GetResult(),original),ref checks);
            Console.WriteLine("PASS: " + checks + " saved voucher actor/identity/cancellation/read contracts; no database connection.");
            return checks;
        }
        internal static void VerifyQaReadOnly(string server, string database)
        {
            if (string.IsNullOrWhiteSpace(database) || !database.EndsWith("_qa", StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("An explicitly named _qa database is required before connecting.");
            var builder = new SqlConnectionStringBuilder { DataSource = server, InitialCatalog = database,
                IntegratedSecurity = true, ConnectTimeout = 10, ApplicationName = "Pharmacy.CoreTests.Voucher.ReadOnly" };
            var baseline = new DataTable();
            using (var connection = new SqlConnection(builder.ConnectionString))
            using (var command = connection.CreateCommand())
            {
                connection.Open(); command.CommandText = "SELECT DB_NAME();";
                if (!string.Equals(Convert.ToString(command.ExecuteScalar()), database, StringComparison.OrdinalIgnoreCase))
                    throw new UnauthorizedAccessException("QA connection identity does not match.");
                command.CommandText = @"SELECT TOP (20) VoucherId, VoucherDate, VoucherType, PartyName, AccountCode,
Amount, Notes, JournalEntryId, CreatedBy FROM dbo.AccountingVouchersTb ORDER BY VoucherId DESC;";
                using (var adapter = new SqlDataAdapter(command)) adapter.Fill(baseline);
            }
            var repo = new SqlVoucherDocumentReadRepository(() => new SqlConnection(builder.ConnectionString));
            var service = new VoucherDocumentReadService(repo, actor => actor.UserName == "qa-reader" && actor.UserRole == "Manager");
            var user = new OperationActor { UserName = "qa-reader", UserRole = "Manager" };
            int compared = 0;
            foreach (DataRow row in baseline.Rows)
            {
                var result = service.LoadAsync(new VoucherDocumentReadRequest((int)row["VoucherId"]), user).GetAwaiter().GetResult();
                if (result.VoucherId != (int)row["VoucherId"] || result.Date != (DateTime)row["VoucherDate"] ||
                    result.Type != Convert.ToString(row["VoucherType"]) || result.PartyName != Convert.ToString(row["PartyName"]) ||
                    result.AccountCode != Convert.ToString(row["AccountCode"]) || result.Amount != (decimal)row["Amount"] ||
                    result.Notes != Convert.ToString(row["Notes"]) || result.CreatedBy != Convert.ToString(row["CreatedBy"]) ||
                    result.JournalEntryId != (row.IsNull("JournalEntryId") ? (int?)null : (int)row["JournalEntryId"]))
                    throw new InvalidOperationException("QA saved voucher differs from independent source query.");
                compared++;
            }
            bool missingChecked = false;
            if (!baseline.Rows.Cast<DataRow>().Any(row => (int)row["VoucherId"] == int.MaxValue))
            {
                if (repo.Load(new VoucherDocumentReadRequest(int.MaxValue)) != null)
                    throw new InvalidOperationException("Unexpected QA voucher identity at the missing-record probe.");
                missingChecked = true;
            }
            Console.WriteLine("PASS: QA saved vouchers compared=" + compared + "; MissingRecord=" + missingChecked +
                "; PersistentWrites=0. Empty sample does not validate row mapping.");
        }

        private static void Check(bool value, ref int checks)
        { if (!value) throw new InvalidOperationException("Saved voucher read contract changed."); checks++; }
        private static void Reject<T>(Action action, ref int checks) where T : Exception
        { try { action(); } catch (T) { checks++; return; } throw new InvalidOperationException("Expected " + typeof(T).Name); }
        private sealed class Fake : IVoucherDocumentReadRepository
        {
            internal int Calls; internal VoucherDocumentReadRequest Request; internal VoucherDocumentSnapshot Value;
            internal Exception Failure; internal Action BeforeReturn;
            internal TaskCompletionSource<VoucherDocumentSnapshot> Pending;internal CancellationToken Token;
            public Task<VoucherDocumentSnapshot> LoadAsync(VoucherDocumentReadRequest request,CancellationToken cancellationToken)
            {Token=cancellationToken;if(Pending!=null){Calls++;Request=request;return Pending.Task;}return Task.FromResult(Load(request));}
            public VoucherDocumentSnapshot Load(VoucherDocumentReadRequest request)
            { Calls++; Request = request; if (Failure != null) throw Failure; BeforeReturn?.Invoke(); return Value; }
        }
    }
}


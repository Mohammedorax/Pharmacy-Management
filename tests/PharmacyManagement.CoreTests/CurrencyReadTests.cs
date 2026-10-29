using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Application;
using PharmacyManagement.Infrastructure.SqlServer;

namespace PharmacyManagement.CoreTests
{
    internal static class CurrencyReadTests
    {
        internal static int Run()
        {
            int checks = 0;
            VerifyAsyncSnapshot(ref checks);
            var repo = new Fake();
            var service = new CurrencyReadService(repo);
            Check(service.GetSystemCurrency().CurrencyId == 0, ref checks);
            Check(service.GetSystemCurrencyDisplay() == "ر.س", ref checks);
            repo.System = new CurrencyRecord { CurrencySymbol = " ر.ي ", CurrencyCode = "YER" };
            Check(service.GetSystemCurrencyDisplay() == "ر.ي", ref checks);
            Check(repo.System.CurrencySymbol == " ر.ي ", ref checks);
            repo.System.CurrencySymbol = null;
            repo.System.CurrencyCode = " YER ";
            Check(service.GetSystemCurrencyDisplay() == "YER", ref checks);
            Check(ReferenceEquals(service.GetAll(), repo.Rows), ref checks);
            repo.Fail = true;
            Check(service.GetSystemCurrencyDisplay() == "ر.س", ref checks);
            bool rejected = false;
            try { service.GetAll(); } catch (InvalidOperationException) { rejected = true; }
            Check(rejected, ref checks);
            rejected = false;
            try { service.GetSystemCurrency(); } catch (InvalidOperationException) { rejected = true; }
            Check(rejected, ref checks);
            int connections = 0;
            new SqlCurrencyReadRepository(() => { connections++; throw new InvalidOperationException(); });
            Check(connections == 0, ref checks);
            Console.WriteLine("PASS: " + checks + " currency legacy/async immutable snapshot authorization/cancellation/identity/error contracts; no database connection.");
            return checks;
        }

        private static void VerifyAsyncSnapshot(ref int checks)
        {
            var actor=new OperationActor{UserName="currency-reader",UserRole="Cashier"};
            var repo=new Fake(); var denied=new CurrencyReadService(repo);
            Reject<UnauthorizedAccessException>(()=>denied.GetSystemCurrencySnapshotAsync(actor,CancellationToken.None).GetAwaiter().GetResult(),ref checks);
            var service=new CurrencyReadService(repo,current=>current.UserName=="currency-reader" && current.UserRole=="Cashier");
            Reject<UnauthorizedAccessException>(()=>service.GetSystemCurrencySnapshotAsync(null,CancellationToken.None).GetAwaiter().GetResult(),ref checks);
            Reject<UnauthorizedAccessException>(()=>service.GetSystemCurrencySnapshotAsync(new OperationActor{UserName="forged",UserRole="Cashier"},CancellationToken.None).GetAwaiter().GetResult(),ref checks);
            using(var canceled=new CancellationTokenSource())
            { canceled.Cancel(); Reject<OperationCanceledException>(()=>service.GetSystemCurrencySnapshotAsync(actor,canceled.Token).GetAwaiter().GetResult(),ref checks); }
            Check(repo.AsyncCalls==0,ref checks);
            repo.Pending=new TaskCompletionSource<CurrencyRecord>();
            using(var cancellation=new CancellationTokenSource())
            {
                var pending=service.GetSystemCurrencySnapshotAsync(actor,cancellation.Token);
                Check(!pending.IsCompleted && repo.AsyncCalls==1 && repo.Token==cancellation.Token,ref checks);
                actor.UserName="mutated";
                var row=new CurrencyRecord{CurrencyId=7,CurrencyCode=" YER ",CurrencySymbol=" ر.ي ",IsSystemCurrency=true};
                repo.Pending.SetResult(row); var snapshot=pending.GetAwaiter().GetResult();
                Check(snapshot.CurrencyId==7 && snapshot.CurrencyCode=="YER" && snapshot.CurrencySymbol=="ر.ي" && snapshot.DisplayText=="ر.ي",ref checks);
                row.CurrencyCode="SAR"; row.CurrencySymbol="ر.س"; row.CurrencyId=9;
                Check(snapshot.CurrencyId==7 && snapshot.CurrencyCode=="YER" && snapshot.DisplayText=="ر.ي",ref checks);
            }
            actor.UserName="currency-reader";
            foreach(var row in new CurrencyRecord[]{null,new CurrencyRecord{CurrencyId=0,CurrencyCode="YER",IsSystemCurrency=true},
                new CurrencyRecord{CurrencyId=7,CurrencyCode="YER",IsSystemCurrency=false},new CurrencyRecord{CurrencyId=7,CurrencyCode=" ",CurrencySymbol="ر.س",IsSystemCurrency=true},
                new CurrencyRecord{CurrencyId=7,CurrencyCode=new string('X',11),IsSystemCurrency=true},new CurrencyRecord{CurrencyId=7,CurrencyCode="YER",CurrencySymbol=new string('X',21),IsSystemCurrency=true}})
            {
                repo=new Fake{System=row};service=new CurrencyReadService(repo,_=>true);
                Reject<CurrencySnapshotUnavailableException>(()=>service.GetSystemCurrencySnapshotAsync(actor,CancellationToken.None).GetAwaiter().GetResult(),ref checks);
            }
            repo=new Fake{System=new CurrencyRecord{CurrencyId=7,CurrencyCode=" YER ",CurrencySymbol=null,IsSystemCurrency=true}};
            service=new CurrencyReadService(repo,_=>true);
            Check(service.GetSystemCurrencySnapshotAsync(actor,CancellationToken.None).GetAwaiter().GetResult().DisplayText=="YER",ref checks);
            repo.Fail=true; Reject<InvalidOperationException>(()=>service.GetSystemCurrencySnapshotAsync(actor,CancellationToken.None).GetAwaiter().GetResult(),ref checks);
            repo=new Fake{Pending=new TaskCompletionSource<CurrencyRecord>()}; bool permitted=true;
            service=new CurrencyReadService(repo,_=>permitted);var revoked=service.GetSystemCurrencySnapshotAsync(actor,CancellationToken.None);
            permitted=false;repo.Pending.SetResult(new CurrencyRecord{CurrencyId=7,CurrencyCode="YER",IsSystemCurrency=true});
            Reject<UnauthorizedAccessException>(()=>revoked.GetAwaiter().GetResult(),ref checks);
            repo=new Fake{Pending=new TaskCompletionSource<CurrencyRecord>()};service=new CurrencyReadService(repo,_=>true);
            using(var cancellation=new CancellationTokenSource())
            {
                var canceled=service.GetSystemCurrencySnapshotAsync(actor,cancellation.Token);cancellation.Cancel();
                repo.Pending.SetResult(new CurrencyRecord{CurrencyId=7,CurrencyCode="YER",IsSystemCurrency=true});
                Reject<OperationCanceledException>(()=>canceled.GetAwaiter().GetResult(),ref checks);
            }
            int connections=0;
            using(var canceled=new CancellationTokenSource())
            {
                canceled.Cancel();var sql=new SqlCurrencyReadRepository(()=>{connections++;throw new Exception("connection must not be created");});
                Reject<OperationCanceledException>(()=>sql.GetSystemCurrencyAsync(canceled.Token).GetAwaiter().GetResult(),ref checks);
            }
            Check(connections==0,ref checks);
        }
        private static void Reject<T>(Action action,ref int checks) where T:Exception
        { bool rejected=false;try{action();}catch(T){rejected=true;}Check(rejected,ref checks); }

        internal static void VerifyQaReadOnly(string server, string database)
        {
            string name = (database ?? string.Empty).ToLowerInvariant();
            if (!(name.EndsWith("_qa") || name.EndsWith("_test") || name.EndsWith("_sandbox")))
                throw new InvalidOperationException("Explicit QA database suffix required before connection.");
            var builder = new SqlConnectionStringBuilder { DataSource = server, InitialCatalog = database,
                IntegratedSecurity = true, ConnectTimeout = 5, ApplicationName = "Pharmacy.CoreTests.Currency.ReadOnly" };
            var repo = new SqlCurrencyReadRepository(() => new SqlConnection(builder.ConnectionString));
            var rows = repo.GetAll();
            var system = repo.GetSystemCurrency();
            int index = 0;
            using (var connection = new SqlConnection(builder.ConnectionString))
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT CurrencyId, CountryNameAr, CurrencyNameAr, CurrencyCode, CurrencySymbol, IsSystemCurrency FROM dbo.CurrencySettingsTb ORDER BY IsSystemCurrency DESC, CountryNameAr";
                connection.Open();
                using (var reader = command.ExecuteReader())
                    while (reader.Read())
                    {
                        if (index >= rows.Count) throw new InvalidOperationException("Currency row count changed.");
                        CurrencyRecord row = rows[index++];
                        if (row.CurrencyId != Convert.ToInt32(reader[0]) || row.CountryNameAr != Value(reader[1]) ||
                            row.CurrencyNameAr != Value(reader[2]) || row.CurrencyCode != Value(reader[3]) ||
                            row.CurrencySymbol != Value(reader[4]) || row.IsSystemCurrency != Convert.ToBoolean(reader[5]))
                            throw new InvalidOperationException("Currency field mapping changed.");
                    }
                if (index != rows.Count) throw new InvalidOperationException("Currency row count changed.");
                command.CommandText = "SELECT TOP 1 CurrencyId FROM dbo.CurrencySettingsTb WHERE IsSystemCurrency = 1 ORDER BY CurrencyId";
                object id = command.ExecuteScalar();
                if ((id == null ? 0 : Convert.ToInt32(id)) != (system == null ? 0 : system.CurrencyId))
                    throw new InvalidOperationException("System currency selection changed.");
            }
            var asynchronous=repo.GetSystemCurrencyAsync(CancellationToken.None).GetAwaiter().GetResult();
            if((system==null)!=(asynchronous==null))throw new InvalidOperationException("Async currency absence differs.");
            if(system!=null && (system.CurrencyId!=asynchronous.CurrencyId || system.CountryNameAr!=asynchronous.CountryNameAr ||
                system.CurrencyNameAr!=asynchronous.CurrencyNameAr || system.CurrencyCode!=asynchronous.CurrencyCode ||
                system.CurrencySymbol!=asynchronous.CurrencySymbol || system.IsSystemCurrency!=asynchronous.IsSystemCurrency))
                throw new InvalidOperationException("Async system currency fields differ.");
            var actor=new OperationActor{UserName="qa-read",UserRole="Manager"};
            var authorized=new CurrencyReadService(repo,current=>current.UserName=="qa-read" && current.UserRole=="Manager");
            bool valid=system!=null && system.CurrencyId>0 && system.IsSystemCurrency && !string.IsNullOrWhiteSpace(system.CurrencyCode) &&
                system.CurrencyCode.Trim().Length<=10 && (system.CurrencySymbol??"").Trim().Length<=20;
            if(valid)
            {
                var snapshot=authorized.GetSystemCurrencySnapshotAsync(actor,CancellationToken.None).GetAwaiter().GetResult();
                if(snapshot.CurrencyId!=system.CurrencyId || snapshot.CurrencyCode!=system.CurrencyCode.Trim() ||
                    snapshot.DisplayText!=(string.IsNullOrWhiteSpace(system.CurrencySymbol)?system.CurrencyCode.Trim():system.CurrencySymbol.Trim()))
                    throw new InvalidOperationException("Validated currency snapshot differs from stored identity.");
            }
            else
            { int ignored=0;Reject<CurrencySnapshotUnavailableException>(()=>authorized.GetSystemCurrencySnapshotAsync(actor,CancellationToken.None).GetAwaiter().GetResult(),ref ignored); }
            Console.WriteLine("PASS: currency fields/order/system selection, sync/async six-field parity and validated snapshot; Rows="+rows.Count+"; SnapshotAvailable="+valid+"; PersistentWrites=0.");
        }
        private static string Value(object value) => value == DBNull.Value ? null : Convert.ToString(value);
        private static void Check(bool value, ref int count) { count++; if (!value) throw new InvalidOperationException("Currency read contract failed: " + count); }
        private sealed class Fake : ICurrencyReadRepository
        {
            internal CurrencyRecord System;
            internal bool Fail;
            internal readonly IReadOnlyList<CurrencyRecord> Rows = new[] { new CurrencyRecord { CurrencyId = 7, CurrencyNameAr = "ريال يمني" } };
            public IReadOnlyList<CurrencyRecord> GetAll() { if (Fail) throw new InvalidOperationException(); return Rows; }
            internal int AsyncCalls; internal CancellationToken Token; internal TaskCompletionSource<CurrencyRecord> Pending;
            public Task<CurrencyRecord> GetSystemCurrencyAsync(CancellationToken token)
            { AsyncCalls++; Token=token; if(Fail) return Task.FromException<CurrencyRecord>(new InvalidOperationException("read failed")); return Pending?.Task ?? Task.FromResult(System); }
            public CurrencyRecord GetSystemCurrency() { if (Fail) throw new InvalidOperationException(); return System; }
        }
    }
}

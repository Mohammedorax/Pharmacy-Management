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
    internal static class SearchTests
    {
        private static void VerifySyncBarcodeAuthorization()
        {
            Require(typeof(IMedicineSearchService).GetMethod("FindByBarcode",new[]{typeof(string)})==null,
                "Actorless sync barcode contract survived.");
            var repository=new ControlledSearch();
            var actor=SearchActor();
            var denied=new MedicineSearchService(repository);
            Reject<UnauthorizedAccessException>(()=>denied.FindByBarcode("001",actor));
            Require(repository.Calls==0,"Default denied barcode reached storage.");
            bool allowed=true;
            var service=new MedicineSearchService(repository, candidate=>allowed&&candidate.UserName=="search-test"&&candidate.UserRole=="Manager");
            Reject<UnauthorizedAccessException>(()=>service.FindByBarcode("001",null));
            repository.BeforeSyncBarcode=()=>allowed=false;
            Reject<UnauthorizedAccessException>(()=>service.FindByBarcode("001",actor));
            Require(repository.Calls==1,"Post-read revocation changed query count.");
            allowed=true;
            repository.BeforeSyncBarcode=()=>{actor.UserName="changed";actor.UserRole="Accountant";};
            var match=service.FindByBarcode(" 001 ",actor);
            Require(match.MedicineName=="دواء"&&repository.LastQuery=="001","Captured actor/raw barcode identity changed.");
            Console.WriteLine("PASS sync barcode actor: absent actorless contract, default/null denial, post-read revocation, actor snapshot and leading zero; no SQL.");
        }
        private static OperationActor SearchActor() => new OperationActor { UserName = "search-test", UserRole = "Manager" };

        internal static void Run()
        {
            VerifyAsyncBarcodeContracts();
            VerifySyncBarcodeAuthorization();
            VerifySearchAuthorization();
            foreach (var reference in typeof(MedicineSearchService).Assembly.GetReferencedAssemblies())
                Require(reference.Name == "mscorlib" || reference.Name == "System" || reference.Name == "System.Core" || reference.Name == "PharmacyManagement.Domain",
                    "Application layer acquired infrastructure/UI dependency: " + reference.Name);
            foreach (var reference in typeof(PharmacyManagement.Domain.JournalEntryPreparation).Assembly.GetReferencedAssemblies())
                Require(reference.Name == "mscorlib" || reference.Name == "System" || reference.Name == "System.Core",
                    "Domain layer acquired application/infrastructure/UI dependency: " + reference.Name);
            var repository = new ControlledSearch();
            var service = new MedicineSearchService(repository, canReadBarcode: candidate => candidate.UserName == "search-test" && candidate.UserRole == "Manager", canSearch: candidate => candidate.UserName == "search-test" && candidate.UserRole == "Manager");
            Require(service.FindByBarcode("  ", SearchActor()) == null && repository.Calls == 0, "Blank barcode reached storage.");
            Reject<InvalidOperationException>(() => service.FindByBarcode(new string('1', 101), SearchActor()));
            Reject<InvalidOperationException>(() => service.SearchAsync(new string('a', 201), SearchActor(), CancellationToken.None).GetAwaiter().GetResult());
            Require(repository.Calls == 0, "Invalid search reached storage.");
            service.FindByBarcode(" 012345 ", SearchActor());
            Require(repository.LastQuery == "012345", "Barcode lost its leading zero or whitespace was retained.");
            var cancellation = new CancellationTokenSource();
            var pending = service.SearchAsync(" باراسيتامول 500 mg ", SearchActor(), cancellation.Token);
            Require(repository.LastQuery == "باراسيتامول 500 mg", "Mixed text changed before storage.");
            cancellation.Cancel();
            repository.Completion.SetResult(new List<MedicineSearchItem>().AsReadOnly());
            Reject<OperationCanceledException>(() => pending.GetAwaiter().GetResult());
            var cancelledRepository = new ControlledSearch();
            Reject<OperationCanceledException>(() => new MedicineSearchService(cancelledRepository, canSearch: _ => true)
                .SearchAsync("test", SearchActor(), cancellation.Token).GetAwaiter().GetResult());
            Require(cancelledRepository.Calls == 0, "Already-cancelled search reached storage.");
            cancellation.Dispose();

            using (var table = new DataTable())
            {
                table.Columns.Add("MedName"); table.Columns.Add("CompName");
                using (var reader = table.CreateDataReader()) Require(SqlMedicineSearchRepository.ReadUniqueBarcodeMatch(reader) == null, "Empty match invented an item.");
                table.Rows.Add("دواء", "مورد");
                using (var reader = table.CreateDataReader()) Require(SqlMedicineSearchRepository.ReadUniqueBarcodeMatch(reader).MedicineName == "دواء", "Unique match lost Arabic.");
                table.Rows.Add("دواء آخر", "مورد");
                using (var reader = table.CreateDataReader()) Reject<InvalidOperationException>(() => SqlMedicineSearchRepository.ReadUniqueBarcodeMatch(reader));
            }
            Console.WriteLine("PASS: search validation, cancellation, typed results and duplicate-barcode contracts.");
        }

        private static void VerifySearchAuthorization()
        {
            var repository = new ControlledSearch(); var actor = SearchActor();
            var denied = new MedicineSearchService(repository);
            Reject<UnauthorizedAccessException>(() => denied.SearchAsync("", actor, CancellationToken.None).GetAwaiter().GetResult());
            bool allowed = true;
            var service = new MedicineSearchService(repository, canReadBarcode: candidate => candidate.UserName == "search-test" && candidate.UserRole == "Manager", canSearch: candidate => allowed && candidate.UserName == "search-test" && candidate.UserRole == "Manager");
            Reject<UnauthorizedAccessException>(() => service.SearchAsync("test", null, CancellationToken.None).GetAwaiter().GetResult());
            Reject<UnauthorizedAccessException>(() => service.SearchAsync("test", new OperationActor { UserName="spoof",UserRole="Manager" }, CancellationToken.None).GetAwaiter().GetResult());
            Require(repository.Calls == 0,"Unauthorized search reached storage.");
            var pending = service.SearchAsync(" باراسيتامول 500 mg ",actor,CancellationToken.None);
            actor.UserName = "caller-mutated";
            repository.Completion.SetResult(new List<MedicineSearchItem>{new MedicineSearchItem {MedName="دواء 500 mg"}}.AsReadOnly());
            Require(pending.GetAwaiter().GetResult()[0].MedName == "دواء 500 mg","Caller mutation changed approved actor or Arabic result.");
            repository = new ControlledSearch();
            service = new MedicineSearchService(repository, canReadBarcode: candidate => candidate.UserName == "search-test" && candidate.UserRole == "Manager", canSearch: _ => allowed);
            pending = service.SearchAsync("",SearchActor(),CancellationToken.None);
            allowed = false; repository.Completion.SetResult(new List<MedicineSearchItem>().AsReadOnly());
            Reject<UnauthorizedAccessException>(() => pending.GetAwaiter().GetResult());
            Console.WriteLine("PASS search: default deny, null/forged actor, actor snapshot and post-read revocation; no SQL.");
        }
        private static void VerifyAsyncBarcodeContracts()
        {
            var actor = new OperationActor { UserName = "cashier", UserRole = "Cashier" };
            var repository = new ControlledSearch();
            var denied = new MedicineSearchService(repository);
            Reject<UnauthorizedAccessException>(() => denied.FindByBarcodeAsync(" ", actor, CancellationToken.None).GetAwaiter().GetResult());
            var service = new MedicineSearchService(repository, current => current.UserName == "cashier" && current.UserRole == "Cashier");
            Reject<UnauthorizedAccessException>(() => service.FindByBarcodeAsync("00001", null, CancellationToken.None).GetAwaiter().GetResult());
            Reject<UnauthorizedAccessException>(() => service.FindByBarcodeAsync("00001", new OperationActor { UserName = "spoof", UserRole = "Cashier" }, CancellationToken.None).GetAwaiter().GetResult());
            Require(service.FindByBarcodeAsync(" ", actor, CancellationToken.None).GetAwaiter().GetResult() == null, "Blank async barcode reached storage.");
            Reject<MedicineBarcodeLengthException>(() => service.FindByBarcodeAsync(new string('0',101), actor, CancellationToken.None).GetAwaiter().GetResult());
            using (var cancelled = new CancellationTokenSource())
            {
                cancelled.Cancel();
                Reject<OperationCanceledException>(() => service.FindByBarcodeAsync("00001", actor, cancelled.Token).GetAwaiter().GetResult());
            }
            Require(repository.Calls == 0, "Denied, invalid or cancelled barcode reached storage.");
            using (var cancellation = new CancellationTokenSource())
            {
                var pending = service.FindByBarcodeAsync(" 000012345 ", actor, cancellation.Token);
                Require(!pending.IsCompleted && repository.LastQuery == "000012345" && repository.BarcodeToken == cancellation.Token,
                    "Async barcode lost leading zeros/token or completed before storage.");
                actor.UserName = "changed"; // Caller mutation must not rewrite the actor already approved for this request.
                repository.BarcodeCompletion.SetResult(new MedicineMatch("دواء 500 mg", "شركة"));
                var match = pending.GetAwaiter().GetResult();
                Require(match.MedicineName == "دواء 500 mg" && match.CompanyName == "شركة", "Async match altered Arabic/mixed text.");
            }
            actor.UserName = "cashier";
            repository = new ControlledSearch(); bool authorized = true;
            service = new MedicineSearchService(repository, current => authorized && current.UserName == "cashier");
            var revoked = service.FindByBarcodeAsync("00001", actor, CancellationToken.None);
            authorized = false; repository.BarcodeCompletion.SetResult(new MedicineMatch("PRIVATE", ""));
            Reject<UnauthorizedAccessException>(() => revoked.GetAwaiter().GetResult());
            repository = new ControlledSearch(); service = new MedicineSearchService(repository, _ => true);
            using (var cancellation = new CancellationTokenSource())
            {
                var pending = service.FindByBarcodeAsync("00001", actor, cancellation.Token);
                cancellation.Cancel(); repository.BarcodeCompletion.SetResult(new MedicineMatch("LATE", ""));
                Reject<OperationCanceledException>(() => pending.GetAwaiter().GetResult());
            }
            repository = new ControlledSearch(); service = new MedicineSearchService(repository, _ => true);
            var failed = service.FindByBarcodeAsync("00001", actor, CancellationToken.None);
            repository.BarcodeCompletion.SetException(new InvalidOperationException("duplicate barcode"));
            Reject<InvalidOperationException>(() => failed.GetAwaiter().GetResult());
            repository = new ControlledSearch(); service = new MedicineSearchService(repository, _ => true);
            var missing = service.FindByBarcodeAsync("00001", actor, CancellationToken.None); repository.BarcodeCompletion.SetResult(null);
            Require(missing.GetAwaiter().GetResult() == null, "Missing barcode invented an item.");
            Console.WriteLine("PASS: async barcode actor/default-deny/revocation/snapshot/validation/leading-zero/cancellation/error/missing contracts; no SQL.");
        }

        internal static void VerifyQaReadOnly(string server, string database)
        {
            string name = (database ?? "").ToLowerInvariant();
            if (!(name.EndsWith("_qa") || name.EndsWith("_test") || name.EndsWith("_sandbox")))
                throw new InvalidOperationException("Explicit QA database suffix required before connection.");
            var config = new SqlConnectionStringBuilder { DataSource = server, InitialCatalog = database,
                IntegratedSecurity = true, ConnectTimeout = 5, ApplicationName = "Pharmacy.CoreTests.ReadOnly" };
            var repository = new SqlMedicineSearchRepository(() => new SqlConnection(config.ConnectionString));
            var service = new MedicineSearchService(repository, canReadBarcode: candidate => candidate.UserName == "search-test" && candidate.UserRole == "Manager", canSearch: candidate => candidate.UserName == "search-test" && candidate.UserRole == "Manager");
            foreach (string query in new[] { "", "باراسيتامول", "%_[~", "500", "__no_matching_item__" })
            {
                var actual = service.SearchAsync(query, SearchActor(), CancellationToken.None).GetAwaiter().GetResult();
                using (var connection = new SqlConnection(config.ConnectionString))
                using (var command = connection.CreateCommand())
                {
                    connection.Open();
                    if (query.Length == 0)
                    {
                        command.CommandText = "SELECT CASE WHEN OBJECT_ID(N'dbo.CommercialReturnHeadersTb', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.SalesInvoicesTb', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.SalesInvoiceLinesTb', N'U') IS NOT NULL THEN 1 ELSE 0 END;";
                        bool returns = Convert.ToInt32(command.ExecuteScalar()) == 1;
                        command.CommandText = SqlMedicineSearchRepository.BuildPopularQuery(returns);
                        command.Parameters.Add("@FromDate", SqlDbType.DateTime2).Value = DateTime.Today.AddDays(-29);
                        command.Parameters.Add("@ToDate", SqlDbType.DateTime2).Value = DateTime.Today.AddDays(1);
                    }
                    else
                    {
                        command.CommandText = SqlMedicineSearchRepository.SearchQuery;
                        string escaped = SqlMedicineSearchRepository.EscapeSearchPattern(query);
                        command.Parameters.Add("@Exact", SqlDbType.NVarChar, 200).Value = query;
                        command.Parameters.Add("@Contains", SqlDbType.NVarChar, 402).Value = "%" + escaped + "%";
                        command.Parameters.Add("@Prefix", SqlDbType.NVarChar, 401).Value = escaped + "%";
                    }
                    using (var reference = new DataTable())
                    {
                        using (var reader = command.ExecuteReader()) reference.Load(reader);
                        Require(reference.Rows.Count == actual.Count, "Result count changed during extraction.");
                        for (int i = 0; i < actual.Count; i++)
                        {
                            var row = reference.Rows[i]; var item = actual[i];
                            Require(Convert.ToString(row["MedName"]) == item.MedName && Convert.ToString(row["Barcode"]) == item.Barcode &&
                                Convert.ToString(row["CompName"]) == item.CompName, "Identity/order changed during typed mapping.");
                            Require(Number(row["MedQty"]) == item.MedQty && Number(row["Sprice"]) == item.Sprice,
                                "Stock/price precision changed during typed mapping.");
                            Require((row["ExpDate"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(row["ExpDate"])) == item.ExpDate,
                                "Expiry changed during typed mapping.");
                            if (query.Length == 0) Require(Number(row["NetSold"]) == item.NetSold, "Popular totals changed.");
                        }
                    }
                }
            }
            var barcodes = new List<string> { "__missing_barcode__" };
            using (var connection = new SqlConnection(config.ConnectionString))
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT DISTINCT Barcode FROM dbo.MedicineTb1 WHERE Barcode IS NOT NULL AND LTRIM(RTRIM(Barcode)) <> N'';";
                connection.Open(); using (var reader = command.ExecuteReader()) while (reader.Read()) barcodes.Add(Convert.ToString(reader[0]));
            }
            var actor = new OperationActor { UserName = "qa-read", UserRole = "Manager" };
            var authorized = new MedicineSearchService(repository, current => current.UserName == actor.UserName && current.UserRole == actor.UserRole);
            foreach (string barcode in barcodes)
            {
                MedicineMatch expected = null, actual = null; string expectedError = null, actualError = null;
                try { expected = repository.FindByBarcode(barcode); } catch (InvalidOperationException error) { expectedError = error.Message; }
                try { actual = authorized.FindByBarcodeAsync(barcode, actor, CancellationToken.None).GetAwaiter().GetResult(); }
                catch (InvalidOperationException error) { actualError = error.Message; }
                Require(expectedError == actualError, "Async barcode ambiguity/error differs from sync reference.");
                Require((expected == null) == (actual == null), "Async barcode absence differs from sync reference.");
                if (expected != null) Require(expected.MedicineName == actual.MedicineName && expected.CompanyName == actual.CompanyName, "Async barcode identity differs from sync reference.");
            }
            Console.WriteLine("PASS: typed SQL search and sync/async barcode identity/ambiguity match. Database=" + database + "; BarcodeProbes=" + barcodes.Count + "; PersistentWrites=0.");
        }

        private static decimal? Number(object value) => value == DBNull.Value ? (decimal?)null : Convert.ToDecimal(value);
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static void Reject<T>(Action action) where T : Exception
        {
            try { action(); } catch (T) { return; }
            throw new InvalidOperationException("Expected " + typeof(T).Name);
        }
        private sealed class ControlledSearch : IMedicineSearchRepository
        {
            internal int Calls;
            internal string LastQuery;
            internal Action BeforeSyncBarcode;
            internal readonly TaskCompletionSource<IReadOnlyList<MedicineSearchItem>> Completion = new TaskCompletionSource<IReadOnlyList<MedicineSearchItem>>();
            public Task<IReadOnlyList<MedicineSearchItem>> SearchAsync(string query, CancellationToken token)
            { Calls++; LastQuery = query; return Completion.Task; }
            internal readonly TaskCompletionSource<MedicineMatch> BarcodeCompletion = new TaskCompletionSource<MedicineMatch>();
            internal CancellationToken BarcodeToken;
            public Task<MedicineMatch> FindByBarcodeAsync(string barcode, CancellationToken token)
            { Calls++; LastQuery = barcode; BarcodeToken = token; return BarcodeCompletion.Task; }
            public MedicineMatch FindByBarcode(string barcode)
            { Calls++; LastQuery = barcode; BeforeSyncBarcode?.Invoke(); return new MedicineMatch("دواء", "مورد"); }
        }
    }
}



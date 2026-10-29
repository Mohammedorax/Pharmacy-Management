using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using PharmacyManagement.Application;
using PharmacyManagement.Infrastructure.SqlServer;

namespace PharmacyManagement.CoreTests
{
    internal static class AccountingLookupReadTests
    {
        private static int checks;
        private static void Check(bool value) { if (!value) throw new Exception("Accounting lookup contract failed."); checks++; }
        private static void Throws<T>(Action action) where T : Exception
        { try { action(); } catch (T) { checks++; return; } throw new Exception("Expected " + typeof(T).Name); }
        private sealed class Repository : IAccountingLookupReadRepository
        {
            internal int Calls; internal bool ActiveOnly;
            internal Func<System.Threading.CancellationToken,System.Threading.Tasks.Task<IReadOnlyList<AccountLookupRow>>> AsyncAccounts;
            internal Func<System.Threading.CancellationToken,System.Threading.Tasks.Task<IReadOnlyList<CostCenterLookupRow>>> AsyncCenters;
            internal System.Threading.CancellationToken LastToken;
            public System.Threading.Tasks.Task<IReadOnlyList<AccountLookupRow>> LoadAccountsAsync(bool activeOnly,System.Threading.CancellationToken token)
            { LastToken=token;return AsyncAccounts==null?System.Threading.Tasks.Task.Run(()=>LoadAccounts(activeOnly),token):AsyncAccounts(token); }
            public System.Threading.Tasks.Task<IReadOnlyList<CostCenterLookupRow>> LoadCostCentersAsync(System.Threading.CancellationToken token)
            { LastToken=token;return AsyncCenters==null?System.Threading.Tasks.Task.Run(()=>LoadCostCenters(),token):AsyncCenters(token); }
            internal Func<IReadOnlyList<AccountLookupRow>> Accounts;
            internal Func<IReadOnlyList<CostCenterLookupRow>> Centers;
            public IReadOnlyList<AccountLookupRow> LoadAccounts(bool activeOnly) { Calls++; ActiveOnly = activeOnly; return Accounts(); }
            public IReadOnlyList<CostCenterLookupRow> LoadCostCenters() { Calls++; return Centers(); }
        }
        private static void VerifyCapturedReadActor()
        {
            using(var begun=new System.Threading.ManualResetEventSlim())
            using(var release=new System.Threading.ManualResetEventSlim())
            {
                var row=new AccountLookupRow("1100","صندوق Cash","Asset",null,true);
                var repo=new Repository{Accounts=()=>{begun.Set();if(!release.Wait(TimeSpan.FromSeconds(3)))throw new Exception("Read actor fixture timed out.");return new[]{row};}};
                var actor=new OperationActor{UserName="qa",UserRole="Manager"};
                var service=new AccountingLookupReadService(repo,current=>current.UserName=="qa");
                var pending=service.LoadAccountsAsync(false,actor);
                try
                {
                    Check(begun.Wait(TimeSpan.FromSeconds(3)));
                    actor.UserName="changed-caller";release.Set();
                    Check(pending.GetAwaiter().GetResult()[0].Code==row.Code);
                }
                finally{release.Set();}
            }
        }

        private static void VerifyCancellableReads()
        {
            foreach(bool accounts in new[]{true,false})
            {
                var accountReply=new System.Threading.Tasks.TaskCompletionSource<IReadOnlyList<AccountLookupRow>>();
                var centerReply=new System.Threading.Tasks.TaskCompletionSource<IReadOnlyList<CostCenterLookupRow>>();
                var repo=new Repository{Accounts=()=>{throw new Exception("Async lookup called sync storage.");},Centers=()=>{throw new Exception("Async lookup called sync storage.");},
                    AsyncAccounts=token=>accountReply.Task,AsyncCenters=token=>centerReply.Task};
                bool allowed=true;var actor=new OperationActor{UserName="qa",UserRole="Manager"};
                var service=new AccountingLookupReadService(repo,_=>allowed);
                using(var cancellation=new System.Threading.CancellationTokenSource())
                {
                    System.Threading.Tasks.Task pending=accounts?(System.Threading.Tasks.Task)service.LoadAccountsAsync(false,actor,cancellation.Token):service.LoadCostCentersAsync(actor,cancellation.Token);
                    Check(!pending.IsCompleted&&repo.Calls==0&&repo.LastToken==cancellation.Token);
                    cancellation.Cancel();accountReply.SetResult(new AccountLookupRow[0]);centerReply.SetResult(new CostCenterLookupRow[0]);
                    Throws<OperationCanceledException>(()=>pending.GetAwaiter().GetResult());
                }
                using(var cancelled=new System.Threading.CancellationTokenSource())
                {
                    cancelled.Cancel();
                    Throws<OperationCanceledException>(()=>{if(accounts)service.LoadAccountsAsync(false,actor,cancelled.Token).GetAwaiter().GetResult();else service.LoadCostCentersAsync(actor,cancelled.Token).GetAwaiter().GetResult();});
                }
                accountReply=new System.Threading.Tasks.TaskCompletionSource<IReadOnlyList<AccountLookupRow>>();centerReply=new System.Threading.Tasks.TaskCompletionSource<IReadOnlyList<CostCenterLookupRow>>();
                System.Threading.Tasks.Task revoked=accounts?(System.Threading.Tasks.Task)service.LoadAccountsAsync(false,actor,System.Threading.CancellationToken.None):service.LoadCostCentersAsync(actor,System.Threading.CancellationToken.None);
                allowed=false;accountReply.SetResult(new AccountLookupRow[0]);centerReply.SetResult(new CostCenterLookupRow[0]);
                Throws<UnauthorizedAccessException>(()=>revoked.GetAwaiter().GetResult());
            }
        }

        internal static int Run()
        {
            checks = 0;
            VerifyCapturedReadActor();
            VerifyCancellableReads();
            var row = new AccountLookupRow(" 1100 ", "الصندوق Cash & صندوق", "Asset", null, false);
            var center = new CostCenterLookupRow(7, "فرع Branch 500 mg");
            var accounts = new List<AccountLookupRow> { row }; var centers = new List<CostCenterLookupRow> { center };
            var repo = new Repository { Accounts = () => accounts, Centers = () => centers };
            var actor = new OperationActor { UserName = "qa", UserRole = "Manager" };
            var service = new AccountingLookupReadService(repo, _ => true);
            var a = service.LoadAccounts(true, actor); var c = service.LoadCostCenters(actor);
            Check(repo.ActiveOnly && a[0].Code == " 1100 " && a[0].ParentCode == null && !a[0].Active);
            Check(a[0].Name == row.Name && a[0].Type == "Asset" && c[0].Name == center.Name && c[0].Id == 7);
            accounts.Clear(); centers.Clear(); Check(a.Count == 1 && c.Count == 1);
            Throws<NotSupportedException>(() => ((IList<AccountLookupRow>)a).Clear());
            Throws<NotSupportedException>(() => ((IList<CostCenterLookupRow>)c).Clear());
            Check(service.LoadAccounts(false, actor).Count == 0 && !repo.ActiveOnly);
            Check(service.LoadCostCenters(actor).Count == 0);
            int calls = repo.Calls;
            Throws<UnauthorizedAccessException>(() => service.LoadAccounts(true, null));
            Throws<UnauthorizedAccessException>(() => service.LoadCostCenters(new OperationActor { UserName = " " }));
            var denied = new AccountingLookupReadService(repo, _ => false);
            Throws<UnauthorizedAccessException>(() => denied.LoadAccountsAsync(true, actor).GetAwaiter().GetResult());
            Throws<UnauthorizedAccessException>(() => denied.LoadCostCenters(actor)); Check(repo.Calls == calls);
            repo.Accounts = () => null; repo.Centers = () => null;
            Throws<InvalidOperationException>(() => service.LoadAccounts(true, actor));
            Throws<InvalidOperationException>(() => service.LoadAccountsAsync(true, actor).GetAwaiter().GetResult());
            Throws<InvalidOperationException>(() => service.LoadCostCenters(actor));
            repo.Accounts = () => { throw new InvalidOperationException("read failed"); };
            Throws<InvalidOperationException>(() => service.LoadAccountsAsync(false, actor).GetAwaiter().GetResult());
            bool allowed = true;
            var changing = new AccountingLookupReadService(repo, _ => allowed);
            repo.Accounts = () => { allowed = false; return new List<AccountLookupRow>(); };
            repo.Centers = () => { allowed = false; return new List<CostCenterLookupRow>(); };
            Throws<UnauthorizedAccessException>(() => changing.LoadAccounts(true, actor)); allowed = true;
            Throws<UnauthorizedAccessException>(() => changing.LoadAccountsAsync(true, actor).GetAwaiter().GetResult()); allowed = true;
            Throws<UnauthorizedAccessException>(() => changing.LoadCostCenters(actor));
            repo.Accounts = () => new List<AccountLookupRow> { row };
            Check(service.LoadAccountsAsync(false, actor).GetAwaiter().GetResult()[0].Name == row.Name);
            Throws<ArgumentNullException>(() => new AccountingLookupReadService(null, _ => true));
            Throws<ArgumentNullException>(() => new AccountingLookupReadService(repo, null));
            Throws<ArgumentNullException>(() => new SqlAccountingLookupReadRepository(null));
            Console.WriteLine("PASS: " + checks + " accounting lookup data/authorization contracts; no database connection.");
            return checks;
        }
        internal static void VerifyQaReadOnly(string server, string database)
        {
            if (string.IsNullOrWhiteSpace(database) || !database.EndsWith("_qa", StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("Accounting lookup integration only accepts _qa.");
            var builder = new SqlConnectionStringBuilder { DataSource = server, InitialCatalog = database,
                IntegratedSecurity = true, Encrypt = false, TrustServerCertificate = true, ConnectTimeout = 15 };
            using (var connection = new SqlConnection(builder.ConnectionString))
            {
                connection.Open(); using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT DB_NAME()";
                    if (!string.Equals(Convert.ToString(command.ExecuteScalar()), database, StringComparison.OrdinalIgnoreCase))
                        throw new UnauthorizedAccessException("Unexpected QA database.");
                    var service = new AccountingLookupReadService(new SqlAccountingLookupReadRepository(() => new SqlConnection(builder.ConnectionString)), _ => true);
                    var actor = new OperationActor { UserName = "QA-readonly", UserRole = "Manager" };
                    int compared = 0;
                    foreach (bool activeOnly in new[] { false, true })
                    {
                        command.Parameters.Clear(); command.CommandText = @"SELECT AccountCode, AccountName, AccountType, ParentAccountCode, IsActive
FROM dbo.AccountsTb WHERE (@ActiveOnly = 0 OR IsActive = 1) ORDER BY AccountCode";
                        command.Parameters.Add("@ActiveOnly", SqlDbType.Bit).Value = activeOnly;
                        var expected = new DataTable(); using (var adapter = new SqlDataAdapter(command)) adapter.Fill(expected);
                        foreach(var actual in new[]{service.LoadAccounts(activeOnly,actor),service.LoadAccountsAsync(activeOnly,actor).GetAwaiter().GetResult()})
                        {
                        if (actual.Count != expected.Rows.Count) throw new Exception("QA account count differs.");
                        for (int i = 0; i < actual.Count; i++)
                        {
                            var e = expected.Rows[i]; var r = actual[i];
                            if (r.Code != (string)e[0] || r.Name != (string)e[1] || r.Type != (string)e[2] ||
                                r.ParentCode != (e.IsNull(3) ? null : (string)e[3]) || r.Active != (bool)e[4])
                                throw new Exception("QA account row or ordering differs.");
                        }
                        compared += actual.Count;
                        }
                    }
                    command.Parameters.Clear(); command.CommandText = "SELECT CostCenterId, CostCenterName FROM dbo.CostCentersTb WHERE IsActive = 1 ORDER BY CostCenterName";
                    var expectedCenters = new DataTable(); using (var adapter = new SqlDataAdapter(command)) adapter.Fill(expectedCenters);
                    var actualCenters = service.LoadCostCenters(actor);
                    var asyncCenters=service.LoadCostCentersAsync(actor,System.Threading.CancellationToken.None).GetAwaiter().GetResult();
                    if(asyncCenters.Count!=expectedCenters.Rows.Count)throw new Exception("QA async cost-center count differs.");
                    for(int i=0;i<asyncCenters.Count;i++)if(asyncCenters[i].Id!=(int)expectedCenters.Rows[i][0]||asyncCenters[i].Name!=(string)expectedCenters.Rows[i][1])throw new Exception("QA async cost-center data differs.");
                    if (actualCenters.Count != expectedCenters.Rows.Count) throw new Exception("QA cost center count differs.");
                    for (int i = 0; i < actualCenters.Count; i++)
                        if (actualCenters[i].Id != (int)expectedCenters.Rows[i][0] || actualCenters[i].Name != (string)expectedCenters.Rows[i][1])
                            throw new Exception("QA cost center row or ordering differs.");
                    Console.WriteLine("PASS: typed accounting lookups match legacy query results; AccountRowsCompared=" + compared +
                        "; CostCentersCompared=" + actualCenters.Count + "; PersistentWrites=0.");
                }
            }
        }
    }
}

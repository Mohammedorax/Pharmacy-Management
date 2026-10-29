using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using PharmacyManagement.Application;
using PharmacyManagement.Infrastructure.SqlServer;

namespace PharmacyManagement.CoreTests
{
    internal static class JournalHistoryReadTests
    {
        private static int checks;
        private static void Check(bool condition) { if (!condition) throw new Exception("Journal history contract failed."); checks++; }
        private static void Throws<T>(Action action) where T : Exception
        { try { action(); } catch (T) { checks++; return; } throw new Exception("Expected " + typeof(T).Name); }
        private sealed class Repository : IJournalHistoryReadRepository
        {
            internal int Calls;
            internal Func<JournalHistoryReadRequest,System.Threading.CancellationToken,System.Threading.Tasks.Task<IReadOnlyList<JournalHistoryRow>>> AsyncRead;
            internal System.Threading.CancellationToken LastToken;
            public System.Threading.Tasks.Task<IReadOnlyList<JournalHistoryRow>> LoadAsync(JournalHistoryReadRequest request,System.Threading.CancellationToken token)
            {LastToken=token;return AsyncRead==null?System.Threading.Tasks.Task.Run(()=>Load(request),token):AsyncRead(request,token);}
            internal Func<JournalHistoryReadRequest, IReadOnlyList<JournalHistoryRow>> Read;
            public IReadOnlyList<JournalHistoryRow> Load(JournalHistoryReadRequest request) { Calls++; return Read(request); }
        }
        private static void VerifyCapturedReadActor()
        {
            using(var begun=new System.Threading.ManualResetEventSlim())
            using(var release=new System.Threading.ManualResetEventSlim())
            {
                var row=new JournalHistoryRow(42,DateTime.Today,"Manual","source","قيد",10m,10m,"qa-user");
                var repo=new Repository{Read=request=>{begun.Set();if(!release.Wait(TimeSpan.FromSeconds(3)))throw new Exception("Read actor fixture timed out.");return new[]{row};}};
                var actor=new OperationActor{UserName="qa-user",UserRole="Manager"};
                var service=new JournalHistoryReadService(repo,current=>current.UserName=="qa-user");
                var pending=service.LoadAsync(new JournalHistoryReadRequest(DateTime.Today,DateTime.Today,""),actor);
                try
                {
                    Check(begun.Wait(TimeSpan.FromSeconds(3)));
                    actor.UserName="changed-caller";release.Set();
                    Check(pending.GetAwaiter().GetResult()[0].Id==row.Id);
                }
                finally{release.Set();}
            }
        }

        private static void VerifyCancellableReads()
        {
            var reply=new System.Threading.Tasks.TaskCompletionSource<IReadOnlyList<JournalHistoryRow>>();
            var repo=new Repository{Read=request=>{throw new Exception("Async history called sync storage.");},AsyncRead=(request,token)=>reply.Task};
            bool allowed=true;var actor=new OperationActor{UserName="qa-user",UserRole="Manager"};
            var service=new JournalHistoryReadService(repo,_=>allowed);
            var input=new JournalHistoryReadRequest(DateTime.Today,DateTime.Today,"");
            using(var cancellation=new System.Threading.CancellationTokenSource())
            {
                var pending=service.LoadAsync(input,actor,cancellation.Token);
                Check(!pending.IsCompleted&&repo.Calls==0&&repo.LastToken==cancellation.Token);
                cancellation.Cancel();reply.SetResult(new JournalHistoryRow[0]);
                Throws<OperationCanceledException>(()=>pending.GetAwaiter().GetResult());
            }
            using(var cancelled=new System.Threading.CancellationTokenSource())
            {
                cancelled.Cancel();Throws<OperationCanceledException>(()=>service.LoadAsync(input,actor,cancelled.Token).GetAwaiter().GetResult());
            }
            reply=new System.Threading.Tasks.TaskCompletionSource<IReadOnlyList<JournalHistoryRow>>();
            var revoked=service.LoadAsync(input,actor,System.Threading.CancellationToken.None);allowed=false;reply.SetResult(new JournalHistoryRow[0]);
            Throws<UnauthorizedAccessException>(()=>revoked.GetAwaiter().GetResult());
        }

        internal static int Run()
        {
            checks = 0;
            VerifyCapturedReadActor();
            VerifyCancellableReads();
            var request = new JournalHistoryReadRequest(new DateTime(2024, 2, 28, 12, 0, 0), new DateTime(2024, 2, 29, 23, 0, 0), " Manual ");
            Check(request.From == new DateTime(2024, 2, 28) && request.To == new DateTime(2024, 2, 29) && request.ExclusiveEnd == new DateTime(2024, 3, 1) && request.SourceType == "Manual");
            Check(new JournalHistoryReadRequest(new DateTime(1753, 1, 1), DateTime.MaxValue, null).ExclusiveEnd == null);
            Throws<ArgumentOutOfRangeException>(() => new JournalHistoryReadRequest(new DateTime(1752, 12, 31), DateTime.Today, ""));
            Throws<ArgumentOutOfRangeException>(() => new JournalHistoryReadRequest(DateTime.Today, DateTime.Today.AddDays(-1), ""));
            Throws<ArgumentException>(() => new JournalHistoryReadRequest(DateTime.Today, DateTime.Today, new string('x', 51)));
            var row = new JournalHistoryRow(42, new DateTime(2024, 2, 29, 23, 12, 34), "Manual", " raw-source ", "صيدلية المُهندس 500 mg", -1.234m, null, null);
            Check(row.Date.TimeOfDay == new TimeSpan(23, 12, 34) && row.SourceId == " raw-source " && row.Debit == -1.234m && row.Credit == null && row.CreatedBy == null);
            var list = new List<JournalHistoryRow> { row };
            var repo = new Repository { Read = _ => list };
            var actor = new OperationActor { UserName = "qa-user", UserRole = "Admin" };
            var service = new JournalHistoryReadService(repo, _ => true);
            var result = service.Load(request, actor); list.Clear(); Check(result.Count == 1 && result[0] == row);
            Throws<NotSupportedException>(() => ((IList<JournalHistoryRow>)result).Add(row));
            int calls = repo.Calls;
            Throws<UnauthorizedAccessException>(() => service.Load(request, null));
            Throws<UnauthorizedAccessException>(() => service.Load(request, new OperationActor { UserName = " " }));
            Throws<UnauthorizedAccessException>(() => new JournalHistoryReadService(repo, _ => false).Load(request, actor));
            Check(repo.Calls == calls);
            Throws<ArgumentNullException>(() => service.Load(null, actor));
            Throws<ArgumentNullException>(() => service.LoadAsync(null, actor).GetAwaiter().GetResult());
            repo.Read = _ => null;
            Throws<InvalidOperationException>(() => service.Load(request, actor));
            repo.Read = _ => { throw new InvalidOperationException("Read failure"); };
            Throws<InvalidOperationException>(() => service.LoadAsync(request, actor).GetAwaiter().GetResult());
            bool allowed = true;
            var changing = new JournalHistoryReadService(repo, _ => allowed);
            repo.Read = _ => { allowed = false; return new List<JournalHistoryRow>(); };
            Throws<UnauthorizedAccessException>(() => changing.Load(request, actor));
            allowed = true;
            Throws<UnauthorizedAccessException>(() => changing.LoadAsync(request, actor).GetAwaiter().GetResult());
            repo.Read = _ => new List<JournalHistoryRow>();
            Check(service.LoadAsync(request, actor).GetAwaiter().GetResult().Count == 0);
            repo.Read = _ => new List<JournalHistoryRow> { row };
            Check(service.LoadAsync(request, actor).GetAwaiter().GetResult()[0].Description == row.Description);
            Throws<ArgumentNullException>(() => new JournalHistoryReadService(null, _ => true));
            Throws<ArgumentNullException>(() => new JournalHistoryReadService(repo, null));
            Throws<ArgumentNullException>(() => new SqlJournalHistoryReadRepository(null));
            Console.WriteLine("PASS: " + checks + " journal history range/read/authorization contracts; no database connection.");
            return checks;
        }
        private sealed class Expected
        {
            internal int Id, Lines; internal DateTime Date; internal string Source, SourceId, Description, User;
            internal decimal? Debit, Credit;
        }
        internal static void VerifyQaReadOnly(string server, string database)
        {
            if (string.IsNullOrWhiteSpace(database) || !database.EndsWith("_qa", StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("Journal history integration only accepts _qa.");
            var builder = new SqlConnectionStringBuilder { DataSource = server, InitialCatalog = database,
                IntegratedSecurity = true, Encrypt = false, TrustServerCertificate = true, ConnectTimeout = 15 };
            var headers = new Dictionary<int, Expected>();
            int lineCount = 0;
            using (var connection = new SqlConnection(builder.ConnectionString))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT DB_NAME()";
                    if (!string.Equals(Convert.ToString(command.ExecuteScalar()), database, StringComparison.OrdinalIgnoreCase))
                        throw new UnauthorizedAccessException("Unexpected QA database.");
                    command.CommandText = "SELECT JournalEntryId, EntryDate, SourceType, SourceId, Description, CreatedBy FROM dbo.JournalEntriesTb";
                    using (var reader = command.ExecuteReader()) while (reader.Read())
                        headers.Add(reader.GetInt32(0), new Expected { Id = reader.GetInt32(0), Date = reader.GetDateTime(1),
                            Source = reader.IsDBNull(2) ? null : reader.GetString(2), SourceId = reader.IsDBNull(3) ? "" : reader.GetString(3),
                            Description = reader.IsDBNull(4) ? null : reader.GetString(4), User = reader.IsDBNull(5) ? null : reader.GetString(5) });
                    command.CommandText = "SELECT JournalEntryId, Debit, Credit FROM dbo.JournalEntryLinesTb";
                    using (var reader = command.ExecuteReader()) while (reader.Read())
                    {
                        lineCount++; Expected header;
                        if (!headers.TryGetValue(reader.GetInt32(0), out header)) continue;
                        header.Lines++;
                        if (!reader.IsDBNull(1)) header.Debit = (header.Debit ?? 0m) + reader.GetDecimal(1);
                        if (!reader.IsDBNull(2)) header.Credit = (header.Credit ?? 0m) + reader.GetDecimal(2);
                    }
                }
            }
            var repository = new SqlJournalHistoryReadRepository(() => new SqlConnection(builder.ConnectionString));
            var requests = new List<JournalHistoryReadRequest>
            {
                new JournalHistoryReadRequest(new DateTime(1753,1,1), DateTime.MaxValue, ""),
                new JournalHistoryReadRequest(DateTime.Today.AddMonths(-1), DateTime.Today, "Manual"),
                new JournalHistoryReadRequest(DateTime.Today, DateTime.Today, "")
            };
            foreach (DateTime day in headers.Values.Select(x => x.Date.Date).Distinct().OrderBy(x => x).Take(3))
                requests.Add(new JournalHistoryReadRequest(day, day, ""));
            int compared = 0;
            foreach (var request in requests)
            {
                var expected = headers.Values.Where(x => x.Lines > 0 && x.Date >= request.From &&
                    (!request.ExclusiveEnd.HasValue || x.Date < request.ExclusiveEnd.Value) &&
                    (request.SourceType.Length == 0 || string.Equals(x.Source, request.SourceType, StringComparison.OrdinalIgnoreCase)))
                    .OrderByDescending(x => x.Date).ThenByDescending(x => x.Id).ToList();
                foreach(var actual in new[]{repository.Load(request),repository.LoadAsync(request,System.Threading.CancellationToken.None).GetAwaiter().GetResult()})
                {
                if (actual.Count != expected.Count) throw new Exception("QA journal history row count differs.");
                for (int i = 0; i < actual.Count; i++)
                {
                    var a = actual[i]; var e = expected[i];
                    if (a.Id != e.Id || a.Date != e.Date || a.SourceType != (e.Source ?? "Manual") || a.SourceId != e.SourceId ||
                        a.Description != e.Description || a.CreatedBy != e.User || a.Debit != e.Debit || a.Credit != e.Credit)
                        throw new Exception("QA journal history metadata/totals differ.");
                    compared++;
                }
                }
            }
            Console.WriteLine("PASS: QA journal history; Profiles=" + requests.Count + "; Headers=" + headers.Count +
                "; Lines=" + lineCount + "; ComparedRows=" + compared + "; PersistentWrites=0.");
        }
    }
}

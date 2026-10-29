using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using PharmacyManagement.Application;

namespace project_fo_3
{
    // Uses only a token-verified restored QA database; the shared runner owns its lifecycle.
    internal sealed partial class GeneralLedgerReadQaSelfTests
    {
        private readonly string database;
        private readonly string account;
        private readonly List<Raw> seed;
        private readonly GeneralLedgerReadSession original;
        private readonly GeneralLedgerReadRequest request;
        private int cases, compared;
        private GeneralLedgerReadQaSelfTests(string token, int acceptedId)
        {
            database = QaSalesInvoiceSelfTests.RequireOwnedTemporaryQa(token);
            using (var connection = Database.CreateConnection())
            {
                connection.Open(); Check(connection.Database == database, "Ledger fixture escaped owned QA.");
                using (var command = new SqlCommand("SELECT TOP (1) AccountCode FROM dbo.JournalEntryLinesTb WHERE JournalEntryId=@Id ORDER BY JournalEntryLineId", connection))
                {
                    command.Parameters.Add("@Id", SqlDbType.Int).Value = acceptedId;
                    account = Convert.ToString(command.ExecuteScalar());
                }
            }
            Check(!string.IsNullOrWhiteSpace(account), "Owned accepted journal account is missing.");
            seed = ReadSeed(); Check(seed.Count > 0, "Owned ledger comparison seed is empty.");
            original = ApplicationServices.CreateGeneralLedgerReadSession(); Check(original.IsCurrent, "Ledger root did not capture a current authorized actor.");
            var accounts = Await(original.LoadAccountsAsync(CancellationToken.None));
            Check(accounts.Any(row => row.Code == account), "Owned ledger account lookup differs from journal.");
            Await(original.LoadCostCentersAsync(CancellationToken.None));
            DateTime first = seed.Min(row => row.Date).Date, last = seed.Max(row => row.Date).Date;
            request = new GeneralLedgerReadRequest(account, first.AddDays(Math.Min(30, (last-first).Days)), last, null, true, true, null);
        }
        internal static GeneralLedgerReadQaSelfTests Create(string token, int acceptedId)
        { return new GeneralLedgerReadQaSelfTests(token, acceptedId); }
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        private static T Await<T>(Task<T> task)
        {
            var timer = Stopwatch.StartNew();
            while (!task.IsCompleted)
            {
                if (timer.Elapsed > TimeSpan.FromSeconds(25)) throw new TimeoutException("Owned ledger UI-context completion timed out.");
                Application.DoEvents(); Thread.Sleep(1);
            }
            return task.GetAwaiter().GetResult();
        }
        private List<Raw> ReadSeed()
        {
            var rows = new List<Raw>();
            using (var connection = Database.CreateConnection())
            {
                connection.Open(); Check(connection.Database == database, "Ledger seed escaped owned QA.");
                using (var command = new SqlCommand(@"SELECT e.EntryDate,e.JournalEntryId,l.JournalEntryLineId,ISNULL(e.SourceType,N'Manual'),ISNULL(e.SourceId,N''),
l.CostCenterId,ISNULL(c.CostCenterName,N''),e.Description,l.Debit,l.Credit
FROM dbo.JournalEntryLinesTb l JOIN dbo.JournalEntriesTb e ON e.JournalEntryId=l.JournalEntryId
LEFT JOIN dbo.CostCentersTb c ON c.CostCenterId=l.CostCenterId WHERE l.AccountCode=@Account
ORDER BY e.EntryDate,e.JournalEntryId,l.JournalEntryLineId", connection))
                {
                    command.Parameters.Add("@Account", SqlDbType.NVarChar, 20).Value = account;
                    using (var reader = command.ExecuteReader()) while (reader.Read()) rows.Add(new Raw {
                        Date=reader.GetDateTime(0), Id=reader.GetInt32(1), Line=reader.GetInt32(2), Source=reader.GetString(3), Reference=reader.GetString(4),
                        Center=reader.IsDBNull(5)?(int?)null:reader.GetInt32(5), CenterName=reader.GetString(6), Description=reader.IsDBNull(7)?null:reader.GetString(7),
                        Debit=reader.IsDBNull(8)?(decimal?)null:reader.GetDecimal(8), Credit=reader.IsDBNull(9)?(decimal?)null:reader.GetDecimal(9) });
                }
            }
            return rows;
        }
        internal void VerifyCurrent(int? positiveCenter = null)
        {
            var centers=new List<int?>{null,0};
            if(positiveCenter.HasValue){Check(positiveCenter.Value>0&&seed.Any(row=>row.Center==positiveCenter),"Positive ledger center has no seeded line.");centers.Add(positiveCenter);}
            foreach (bool previous in new[]{false,true}) foreach (bool opening in new[]{false,true})
            foreach (string source in new[]{(string)null,"Manual","Voucher","SalesInvoice","PurchaseInvoice","OpeningEntry"})
            foreach (int? center in centers)
            {
                var filter = new GeneralLedgerReadRequest(account,request.FromDate,request.ToDate,source,previous,opening,center);
                VerifyResult(filter, Await(original.LoadAsync(filter,CancellationToken.None)));
            }
            var empty = new GeneralLedgerReadRequest(account,request.ToDate.AddDays(1),request.ToDate.AddDays(2),null,true,true,null);
            VerifyResult(empty,Await(original.LoadAsync(empty,CancellationToken.None)));
            if(positiveCenter.HasValue)
            {
                foreach(bool previous in new[]{false,true})
                {
                    var pastCenter=new GeneralLedgerReadRequest(account,empty.FromDate,empty.ToDate,null,previous,true,positiveCenter);
                    var result=Await(original.LoadAsync(pastCenter,CancellationToken.None));VerifyResult(pastCenter,result);
                    Check(result.Rows.Count==0&&result.EndingBalance==result.PreviousBalance&&(!previous||result.PreviousBalance!=0),"Positive center empty period lost its actual previous balance.");
                }
            }
            VerifyUnchanged();
            Console.WriteLine("PASS owned ledger captured root SQL parity: "+cases+" filter/empty cases, "+compared+" raw rows; previous/period/ending/order/null/text; authorized account/center lookups; positiveCenter="+positiveCenter.HasValue+"; no writes from reads.");
        }
        internal void VerifyPositiveCostCenter(string token,int acceptedId)
        {
            Check(QaSalesInvoiceSelfTests.RequireOwnedTemporaryQa(token)==database,"Positive center scope differs from owned database.");
            var line=seed.Single(row=>row.Id==acceptedId&&row.Source=="QAJournalAcceptance"&&row.Reference==token);
            Check(!line.Center.HasValue,"Owned ledger positive-center line was not initially unassigned.");
            string name="مركز مُختبر 500 mg "+token;int center=0;bool committed=false;
            string centersBefore=CenterSnapshot();
            try
            {
                using(var connection=Database.CreateConnection())
                {
                    connection.Open();Check(connection.Database==database,"Center fixture escaped owned QA.");
                    using(var transaction=connection.BeginTransaction())
                    {
                        using(var insert=new SqlCommand("INSERT INTO dbo.CostCentersTb(CostCenterName,IsActive) OUTPUT INSERTED.CostCenterId VALUES(@Name,1)",connection,transaction))
                        {insert.Parameters.Add("@Name",SqlDbType.NVarChar,200).Value=name;center=Convert.ToInt32(insert.ExecuteScalar());}
                        using(var update=new SqlCommand(@"UPDATE l SET CostCenterId=@Center FROM dbo.JournalEntryLinesTb l
JOIN dbo.JournalEntriesTb e ON e.JournalEntryId=l.JournalEntryId
WHERE l.JournalEntryLineId=@Line AND l.CostCenterId IS NULL AND l.AccountCode=@Account AND e.JournalEntryId=@Id
AND e.SourceType=N'QAJournalAcceptance' AND e.SourceId=@Token AND e.CreatedBy=@User",connection,transaction))
                        {
                            AddOwned(update,token,acceptedId,line.Line,center,name);
                            Check(update.ExecuteNonQuery()==1,"Positive center did not target exactly one owned fixture line.");
                        }
                        transaction.Commit();committed=true;
                    }
                }
                var positive=Create(token,acceptedId);
                var options=Await(positive.original.LoadCostCentersAsync(CancellationToken.None));
                Check(options.Count(row=>row.Id==center&&row.Name==name)==1,"Active owned center lookup lost exact caption/id.");
                positive.VerifyCurrent(center);
            }
            finally
            {
                if(committed)using(var connection=Database.CreateConnection())
                {
                    connection.Open();Check(connection.Database==database,"Center cleanup escaped owned QA.");
                    using(var transaction=connection.BeginTransaction())
                    {
                        using(var restore=new SqlCommand(@"UPDATE l SET CostCenterId=NULL FROM dbo.JournalEntryLinesTb l
JOIN dbo.JournalEntriesTb e ON e.JournalEntryId=l.JournalEntryId
WHERE l.JournalEntryLineId=@Line AND l.CostCenterId=@Center AND l.AccountCode=@Account AND e.JournalEntryId=@Id
AND e.SourceType=N'QAJournalAcceptance' AND e.SourceId=@Token AND e.CreatedBy=@User",connection,transaction))
                        {AddOwned(restore,token,acceptedId,line.Line,center,name);Check(restore.ExecuteNonQuery()==1,"Owned center link cleanup did not restore exactly one line.");}
                        using(var remove=new SqlCommand("DELETE FROM dbo.CostCentersTb WHERE CostCenterId=@Center AND CostCenterName=@Name",connection,transaction))
                        {remove.Parameters.Add("@Center",SqlDbType.Int).Value=center;remove.Parameters.Add("@Name",SqlDbType.NVarChar,200).Value=name;Check(remove.ExecuteNonQuery()==1,"Owned center cleanup did not remove exactly one center.");}
                        transaction.Commit();
                    }
                }
            }
            VerifyUnchanged();Check(CenterSnapshot()==centersBefore,"Existing center rows changed after temporary fixture.");
            Console.WriteLine("PASS owned ledger positive cost center: token-owned accepted fixture line temporarily assigned, all/unassigned/positive filters and exact active mixed-Arabic caption compared; original line/center rows restored; no extra journal/audit.");
        }
        private void AddOwned(SqlCommand command,string token,int id,int line,int center,string name)
        {
            command.Parameters.Add("@Center",SqlDbType.Int).Value=center;command.Parameters.Add("@Line",SqlDbType.Int).Value=line;
            command.Parameters.Add("@Id",SqlDbType.Int).Value=id;command.Parameters.Add("@Account",SqlDbType.NVarChar,20).Value=account;
            command.Parameters.Add("@Token",SqlDbType.NVarChar,50).Value=token;command.Parameters.Add("@User",SqlDbType.NVarChar,200).Value=SessionContext.CurrentUser;
        }
        private string CenterSnapshot()
        {
            using(var connection=Database.CreateConnection())
            {
                connection.Open();Check(connection.Database==database,"Center snapshot escaped owned QA.");
                using(var command=new SqlCommand("SELECT CONVERT(nvarchar(max),(SELECT * FROM dbo.CostCentersTb ORDER BY CostCenterId FOR XML RAW('r'),ROOT('rows'),TYPE))",connection))
                    return Convert.ToString(command.ExecuteScalar());
            }
        }
        private void VerifyResult(GeneralLedgerReadRequest filter, GeneralLedgerReadResult actual)
        {
            var selected=seed.Where(row => (filter.SourceType==null||row.Source==filter.SourceType) &&
                (filter.IncludeOpeningEntries||row.Source!="OpeningEntry") &&
                (!filter.CostCenterId.HasValue||(filter.CostCenterId==0&&!row.Center.HasValue)||row.Center==filter.CostCenterId)).ToArray();
            decimal previous=filter.IncludePreviousBalance?selected.Where(row=>row.Date<filter.FromDate).Sum(row=>(row.Debit-row.Credit)??0m):0m;
            var expected=selected.Where(row=>row.Date>=filter.FromDate&&row.Date<filter.ExclusiveEnd).ToArray();
            decimal running=previous;
            Check(actual!=null&&actual.Rows.Count==expected.Length&&actual.PreviousBalance==previous,"Owned ledger count/previous parity failed.");
            for(int i=0;i<expected.Length;i++)
            {
                var row=expected[i];var got=actual.Rows[i];running+=(row.Debit-row.Credit)??0m;
                Check(got.Date==row.Date&&got.DocumentId==row.Id&&got.SourceType==row.Source&&got.SourceId==row.Reference&&got.CostCenterName==row.CenterName&&
                    got.Description==row.Description&&got.Debit==row.Debit&&got.Credit==row.Credit&&got.Balance==running,"Owned ledger exact row/order/running parity failed.");
            }
            Check(actual.EndingBalance==running&&actual.PeriodDebit==expected.Sum(row=>row.Debit??0m)&&actual.PeriodCredit==expected.Sum(row=>row.Credit??0m),"Owned ledger totals parity failed.");
            cases++;compared+=expected.Length;
        }
        private void WaitBlocked(SqlConnection observer, int blocker)
        {
            Check(observer.Database==database,"Ledger SQL observer escaped owned QA.");
            var timer=Stopwatch.StartNew();
            while(timer.Elapsed<TimeSpan.FromSeconds(8))
            {
                using(var command=new SqlCommand(@"WITH owned_waiters AS (
SELECT session_id FROM sys.dm_exec_requests WHERE database_id=DB_ID() AND blocking_session_id=@Blocker
UNION ALL SELECT r.session_id FROM sys.dm_exec_requests r JOIN owned_waiters w ON r.blocking_session_id=w.session_id WHERE r.database_id=DB_ID())
SELECT r.session_id,r.blocking_session_id,r.wait_type FROM sys.dm_exec_requests r JOIN owned_waiters w ON r.session_id=w.session_id
CROSS APPLY sys.dm_exec_sql_text(r.sql_handle) t
WHERE r.wait_type LIKE N'LCK%' AND t.text LIKE N'%DECLARE @OpeningBalance DECIMAL%' OPTION(MAXRECURSION 32)",observer))
                {
                    command.Parameters.Add("@Blocker",SqlDbType.Int).Value=blocker;
                    using(var reader=command.ExecuteReader())if(reader.Read())
                    {
                        int session=Convert.ToInt32(reader[0]),parent=Convert.ToInt32(reader[1]);string wait=reader.GetString(2);
                        Check(!reader.Read(),"More than one ledger request entered this owned blocker chain.");
                        Console.WriteLine("Owned ledger SQL wait: session="+session+"; blocker="+parent+"; wait="+wait+"; ownedBlocker="+blocker+"; observed_ms="+timer.ElapsedMilliseconds);
                        return;
                    }
                }
                Thread.Sleep(20);
            }
            throw new TimeoutException("Ledger command was not observed inside the owned SQL blocker chain.");
        }
        internal void VerifyCancellation(SqlConnection observer,int blocker)
        {
            using(var cancellation=new CancellationTokenSource())
            {
                var pending=original.LoadAsync(request,cancellation.Token);WaitBlocked(observer,blocker);
                Check(!pending.IsCompleted,"Observed locked ledger unexpectedly completed.");cancellation.Cancel();
                bool canceled=false;try{Await(pending);}catch(OperationCanceledException){canceled=true;}
                Check(canceled,"Observed ledger lock did not cancel through the desktop root.");
            }
            Console.WriteLine("PASS owned ledger cancellation: actual captured desktop/root SQL command observed under owned header lock, canceled before releasing blocker; no result/write.");
        }
        internal Task<GeneralLedgerReadResult> BeginLockedRead(SqlConnection observer,int blocker)
        {
            var pending=original.LoadAsync(request,CancellationToken.None);WaitBlocked(observer,blocker);
            Check(!pending.IsCompleted,"Stale ledger read was not pending at observed lock.");return pending;
        }
        internal void VerifyStale(Task<GeneralLedgerReadResult> pending)
        {
            Check(!original.IsCurrent,"Same-user reauthentication retained old ledger root.");
            bool denied=false;try{Await(pending);}catch(UnauthorizedAccessException){denied=true;}
            Check(denied,"Actual delayed SQL ledger result escaped post-read authorization after reauthentication.");
            Console.WriteLine("PASS owned ledger stale SQL response denied: same-user actual reauthentication while command waited in observed blocker chain; old result not delivered.");
        }
        internal void VerifyFresh()
        {
            var fresh=ApplicationServices.CreateGeneralLedgerReadSession();Check(fresh.IsCurrent,"Fresh ledger root is not authorized.");
            VerifyResult(request,Await(fresh.LoadAsync(request,CancellationToken.None)));VerifyUnchanged();
        }
        internal void VerifySignedOut()
        {
            var fresh=ApplicationServices.CreateGeneralLedgerReadSession();Check(!fresh.IsCurrent,"Anonymous ledger factory is current.");
            bool denied=false;try{Await(fresh.LoadAsync(request,CancellationToken.None));}catch(InvalidOperationException){denied=true;}
            Check(denied,"Signed-out desktop ledger root was not denied.");
            Console.WriteLine("PASS owned ledger fresh recovery and signed-out factory denial; accounting rows unchanged, no journal/audit writes from reads.");
        }
        private void VerifyUnchanged()
        {
            var after=ReadSeed();Check(after.Count==seed.Count,"Ledger raw seed count changed.");
            for(int i=0;i<seed.Count;i++){var a=seed[i];var b=after[i];Check(a.Date==b.Date&&a.Id==b.Id&&a.Line==b.Line&&a.Source==b.Source&&a.Reference==b.Reference&&a.Center==b.Center&&
                a.CenterName==b.CenterName&&a.Description==b.Description&&a.Debit==b.Debit&&a.Credit==b.Credit,"Ledger raw seed changed.");}
        }
        private sealed class Raw
        {internal DateTime Date;internal int Id,Line;internal string Source,Reference,CenterName,Description;internal int? Center;internal decimal? Debit,Credit;}
    }
}

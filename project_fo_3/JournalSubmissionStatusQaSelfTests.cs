using System;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Application;

namespace project_fo_3
{
    internal static class JournalSubmissionStatusQaSelfTests
    {
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        private static void Deny(Action action)
        { try { action(); } catch (UnauthorizedAccessException) { return; } throw new InvalidOperationException("Expected recovery authorization rejection."); }
        private static void WaitBlocked(SqlConnection observer, int blocker)
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            while (timer.Elapsed < TimeSpan.FromSeconds(8))
            {
                using (var command = new SqlCommand("SELECT COUNT(*) FROM sys.dm_exec_requests WHERE database_id=DB_ID() AND blocking_session_id=@Blocker", observer))
                {
                    command.Parameters.AddWithValue("@Blocker", blocker);
                    if (Convert.ToInt32(command.ExecuteScalar()) > 0) return;
                }
                Thread.Sleep(20);
            }
            throw new TimeoutException("Recovery reader was not observed waiting for the owned SQL lock.");
        }
        internal static void Verify(string ownerToken, int acceptedId, string user, Action reauthenticate)
        {
            string database = QaSalesInvoiceSelfTests.RequireOwnedTemporaryQa(ownerToken);
            var request = new JournalSubmissionStatusRequest("QAJournalAcceptance", ownerToken);
            var ledgerFixture = GeneralLedgerReadQaSelfTests.Create(ownerToken,acceptedId);
            ledgerFixture.VerifyCurrent();
            ledgerFixture.VerifyPositiveCostCenter(ownerToken,acceptedId);
            ledgerFixture.VerifyConcurrentSnapshot(ownerToken,acceptedId);
            ledgerFixture.VerifyReadPerformance(ownerToken);
            var session = ApplicationServices.CreateJournalSubmissionStatusSession();
            Check(session.IsCurrent && session.ContextKey.Length == 64, "Recovery session did not capture authenticated context.");
            Check(session.Service.FindAsync(request, session.Actor, CancellationToken.None).GetAwaiter().GetResult().JournalId == acceptedId,
                "Accepted journal status differs from committed source.");
            Check(!session.Service.FindAsync(new JournalSubmissionStatusRequest("QAJournalAcceptance", ownerToken + "-ABSENT"), session.Actor,
                CancellationToken.None).GetAwaiter().GetResult().IsPosted, "Absent journal was incorrectly accepted.");
            Deny(() => session.Service.FindAsync(request, new OperationActor { UserName = user, UserRole = "Cashier" }, CancellationToken.None).GetAwaiter().GetResult());
            Deny(() => session.Service.FindAsync(request, new OperationActor { UserName = "other-user", UserRole = "Manager" }, CancellationToken.None).GetAwaiter().GetResult());
            // Exercise the repository's owner boundary independently of the root's actor policy.
            var ownerRepository = new PharmacyManagement.Infrastructure.SqlServer.SqlJournalSubmissionStatusRepository(Database.CreateConnection);
            Deny(() => ownerRepository.FindAsync(request, new OperationActor { UserName = "QA-other-" + ownerToken, UserRole = "Manager" },
                CancellationToken.None).GetAwaiter().GetResult());
            using (var observer = Database.CreateConnection())
            using (var blocker = Database.CreateConnection())
            {
                observer.Open(); blocker.Open();
                Check(observer.Database == database && blocker.Database == database, "Status fixture escaped its owned database.");
                int blockerId;
                using (var command = new SqlCommand("SELECT @@SPID", blocker)) blockerId = Convert.ToInt32(command.ExecuteScalar());
                using (var transaction = blocker.BeginTransaction())
                {
                    using (var command = new SqlCommand("SELECT COUNT_BIG(*) FROM dbo.JournalEntriesTb WITH (TABLOCKX,HOLDLOCK)", blocker, transaction)) command.ExecuteScalar();
                    using (var cancellation = new CancellationTokenSource())
                    {
                        Task<JournalSubmissionStatus> pending = session.Service.FindAsync(request, session.Actor, cancellation.Token);
                        WaitBlocked(observer, blockerId);
                        Check(!pending.IsCompleted, "Locked recovery read returned prematurely.");
                        cancellation.Cancel();
                        bool canceled = false;
                        try { pending.GetAwaiter().GetResult(); } catch (OperationCanceledException) { canceled = true; }
                        Check(canceled, "SQL recovery read did not cancel under an observed lock.");
                    }
                    ledgerFixture.VerifyCancellation(observer,blockerId);
                    transaction.Rollback();
                }
                using (var transaction = blocker.BeginTransaction())
                {
                    using (var command = new SqlCommand("SELECT COUNT_BIG(*) FROM dbo.JournalEntriesTb WITH (TABLOCKX,HOLDLOCK)", blocker, transaction)) command.ExecuteScalar();
                    Task<JournalSubmissionStatus> pending = session.Service.FindAsync(request, session.Actor, CancellationToken.None);
                    WaitBlocked(observer, blockerId);
                    var ledgerPending=ledgerFixture.BeginLockedRead(observer,blockerId);
                    reauthenticate();
                    Check(!session.IsCurrent, "Same-user reauthentication retained old recovery context.");
                    transaction.Rollback();
                    Deny(() => pending.GetAwaiter().GetResult());
                    ledgerFixture.VerifyStale(ledgerPending);
                }
            }
            var fresh = ApplicationServices.CreateJournalSubmissionStatusSession();
            Check(fresh.ContextKey == session.ContextKey && fresh.IsCurrent, "Persistent context changed for the same profile/user despite new login revision.");
            Check(fresh.Service.FindAsync(request, fresh.Actor, CancellationToken.None).GetAwaiter().GetResult().JournalId == acceptedId,
                "Fresh session did not recover the saved identity.");
            ledgerFixture.VerifyFresh();
            SessionContext.Clear();
            ledgerFixture.VerifySignedOut();
            Deny(() => fresh.Service.FindAsync(request, fresh.Actor, CancellationToken.None).GetAwaiter().GetResult());
            reauthenticate();
            ledgerFixture.VerifyFresh();
            Console.WriteLine("PASS owned QA journal recovery status: committed and absent sources, wrong role/user/record owner denial, observed SQL lock cancellation, stale result rejected after same-user reauthentication, fresh recovery and signed-out denial. Reads only; no new journal/audit rows.");
        }
    }
}

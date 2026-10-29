using System;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Application;
using PharmacyManagement.Domain;
using PharmacyManagement.Infrastructure.SqlServer;

namespace PharmacyManagement.CoreTests
{
    internal static class JournalPayloadComparisonTests
    {
        private static int checks;
        private static void Check(bool value) { if (!value) throw new Exception("Journal payload contract failed."); ++checks; }
        private static void Reject<T>(Action action) where T : Exception
        { try { action(); } catch (T) { ++checks; return; } throw new Exception("Expected " + typeof(T).Name); }
        private sealed class Probe : IJournalSubmissionStatusRepository
        {
            internal JournalSubmissionStatus Result; internal JournalSubmissionStatusRequest Request;
            public Task<JournalSubmissionStatus> FindAsync(JournalSubmissionStatusRequest request, OperationActor actor, CancellationToken token)
            { Request = request; return Task.FromResult(Result); }
        }
        internal static int Run()
        {
            checks = 0; string id = "123456781234123412341234567890ab";
            var date = new DateTime(2026, 10, 5, 9, 10, 11, 1);
            var lines = new[] { new JournalLineValues(" 1000 ", 10.005m, 0, " مُشكّل Cash 500 mg ", null), new JournalLineValues("2000", 0, 10.005m, null, 7) };
            var expected = new JournalPostRequest(date, "Manual", id, " قيد A&B ", lines);
            var storedDate = new DateTime(2026, 10, 5, 9, 10, 11);
            var stored = new[] { new JournalLineValues("1000", 10m, 0, "مُشكّل Cash 500 mg", null), new JournalLineValues("2000", 0, 10m, null, 7) };
            Check(SqlJournalRequestComparison.Matches(expected, storedDate, "قيد A&B", stored));
            Check(!SqlJournalRequestComparison.Matches(expected, storedDate.AddDays(1), "قيد A&B", stored));
            Check(!SqlJournalRequestComparison.Matches(expected, storedDate, "changed", stored));
            Check(!SqlJournalRequestComparison.Matches(expected, storedDate, "قيد A&B", new[] { stored[1], stored[0] }));
            Check(!SqlJournalRequestComparison.Matches(expected, storedDate, "قيد A&B", new[] { stored[0] }));
            Check(!SqlJournalRequestComparison.Matches(expected, storedDate, "قيد A&B", new[] { new JournalLineValues("3000",10m,0,stored[0].Description,null),stored[1] }));
            Check(!SqlJournalRequestComparison.Matches(expected, storedDate, "قيد A&B", new[] { new JournalLineValues("1000",20m,0,stored[0].Description,null), new JournalLineValues("2000",0,20m,null,7) }));
            Check(!SqlJournalRequestComparison.Matches(expected, storedDate, "قيد A&B", new[] { new JournalLineValues("1000",10m,0,"different",null),stored[1] }));
            Check(!SqlJournalRequestComparison.Matches(expected, storedDate, "قيد A&B", new[] { stored[0],new JournalLineValues("2000",0,10m,null,8) }));
            Check(!SqlJournalRequestComparison.Matches(expected, storedDate, "قيد A&B", new JournalLineValues[] { null,stored[1] }));
            Reject<ArgumentNullException>(() => SqlJournalRequestComparison.Matches(null, storedDate, null, stored));
            Reject<ArgumentNullException>(() => SqlJournalRequestComparison.Matches(expected, storedDate, null, null));
            Check(SqlJournalRequestComparison.IsManualIdentity(expected));
            Check(!SqlJournalRequestComparison.IsManualIdentity(new JournalPostRequest(date,"Manual",null,null,lines)));
            Check(!SqlJournalRequestComparison.IsManualIdentity(new JournalPostRequest(date,"SalesInvoice",id,null,lines)));
            Check(!SqlJournalRequestComparison.IsManualIdentity(null));
            var normalized = JournalSubmissionStatusService.PrepareRequest(new JournalSubmissionStatusRequest(" Manual ", id, expected));
            Check(normalized.ExpectedRequest != expected && normalized.ExpectedRequest.Description == "قيد A&B" && normalized.ExpectedRequest.Lines[0].Debit == 10m);
            Reject<InvalidOperationException>(() => JournalSubmissionStatusService.PrepareRequest(new JournalSubmissionStatusRequest("OpeningEntry", id, expected)));
            Reject<InvalidOperationException>(() => JournalSubmissionStatusService.PrepareRequest(new JournalSubmissionStatusRequest("Manual", Guid.NewGuid().ToString("N"), expected)));
            Reject<ArgumentException>(() => new JournalSubmissionStatus(0,true));
            Reject<ArgumentException>(() => new JournalAcceptedRequestConflictException(0,"Manual",id));
            Reject<ArgumentException>(() => new JournalAcceptedRequestConflictException(1,"SalesInvoice",id));
            var conflict = new JournalAcceptedRequestConflictException(82,"Manual",id);
            Check(conflict.JournalId == 82 && conflict.SourceType == "Manual" && conflict.SourceId == id);
            var probe = new Probe { Result = new JournalSubmissionStatus(82) };
            var service = new JournalSubmissionStatusService(probe, candidate => candidate != null && candidate.UserRole == "Manager");
            var actor = new OperationActor { UserName = "manager", UserRole = "Manager" };
            Reject<InvalidOperationException>(() => service.FindAsync(normalized, actor, CancellationToken.None).GetAwaiter().GetResult());
            Check(probe.Request.ExpectedRequest.SourceId == id);
            probe.Result = new JournalSubmissionStatus(82,true);
            Check(service.FindAsync(normalized, actor, CancellationToken.None).GetAwaiter().GetResult().ContentMatches == true);
            probe.Result = new JournalSubmissionStatus(82,false);
            Check(service.FindAsync(normalized, actor, CancellationToken.None).GetAwaiter().GetResult().ContentMatches == false);
            probe.Result = new JournalSubmissionStatus(0);
            Check(!service.FindAsync(normalized, actor, CancellationToken.None).GetAwaiter().GetResult().IsPosted);
            Console.WriteLine("PASS: " + checks + " journal saved-payload comparison and expected identity/missing-proof contracts; existing SQL datetime/text/rounding semantics, no SQL connection.");
            return checks;
        }
    }
}

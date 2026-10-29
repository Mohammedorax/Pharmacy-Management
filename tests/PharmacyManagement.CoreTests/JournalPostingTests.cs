using System;
using System.Collections.Generic;
using PharmacyManagement.Application;
using PharmacyManagement.Domain;
using PharmacyManagement.Infrastructure.SqlServer;

namespace PharmacyManagement.CoreTests
{
    internal static class JournalPostingTests
    {
        private static int checks;
        private static void Check(bool value) { if (!value) throw new Exception("Journal posting contract failed."); checks++; }
        private static void Throws<T>(Action action) where T : Exception
        { try { action(); } catch (T) { checks++; return; } throw new Exception("Expected " + typeof(T).Name); }
        private sealed class Repository : IJournalPostRepository
        {
            internal int Calls; internal JournalPostRequest Request; internal OperationActor Actor;
            internal Func<JournalPostResult> Save = () => new JournalPostResult(42, false);
            public JournalPostResult Post(JournalPostRequest request, OperationActor actor)
            { Calls++; Request = request; Actor = actor; return Save(); }
        }
        private static List<JournalLineValues> Lines()
            => new List<JournalLineValues> { new JournalLineValues(" 1000 ", 10.005m, 0, "بيان Cash 500 mg", null),
                new JournalLineValues("1100", 0, 10.005m, null, 7) };
        internal static int Run()
        {
            checks = 0; var input = Lines(); var date = new DateTime(2026, 10, 1, 15, 31, 29);
            var request = new JournalPostRequest(date, " Manual ", " retry-123 ", " بيان mixed 500 mg ", input);
            input.Clear(); Check(request.Lines.Count == 2 && request.Lines[0].AccountCode == " 1000 " && request.Date == date);
            Throws<NotSupportedException>(() => ((IList<JournalLineValues>)request.Lines).Clear());
            Throws<ArgumentNullException>(() => new JournalPostRequest(date, null, null, null, null));
            var repo = new Repository(); var actor = new OperationActor { UserName = "tester", UserRole = "Manager" };
            var service = new JournalPostService(repo, _ => true);
            var result = service.Post(request, actor);
            Check(result.Id == 42 && !result.AlreadyPosted && repo.Calls == 1);
            Check(repo.Request.SourceType == "Manual" && repo.Request.SourceId == "retry-123" && repo.Request.Description == "بيان mixed 500 mg");
            Check(repo.Request.Date == date && repo.Request.Lines[0].Debit == 10m && repo.Request.Lines[1].Credit == 10m);
            Check(repo.Request.Lines[0].AccountCode == "1000" && repo.Request.Lines[0].Description == "بيان Cash 500 mg" && repo.Request.Lines[1].CostCenterId == 7);
            Check(!ReferenceEquals(repo.Actor, actor) && repo.Actor.UserName == "tester" && repo.Actor.UserRole == "Manager");
            actor.UserName = "changed"; Check(repo.Actor.UserName == "tester"); actor.UserName = "tester";
            int calls = repo.Calls;
            Throws<UnauthorizedAccessException>(() => service.Post(request, null));
            Throws<UnauthorizedAccessException>(() => service.Post(request, new OperationActor { UserName = " ", UserRole = "Manager" }));
            Throws<UnauthorizedAccessException>(() => service.Post(request, new OperationActor { UserName = "tester", UserRole = " " }));
            Throws<UnauthorizedAccessException>(() => new JournalPostService(repo, _ => false).Post(request, actor));
            Check(repo.Calls == calls);
            Throws<ArgumentNullException>(() => service.Post(null, actor));
            foreach (var badDate in new[] { new DateTime(1752, 12, 31), DateTime.MaxValue })
                Throws<InvalidOperationException>(() => service.Post(new JournalPostRequest(badDate, null, null, null, Lines()), actor));
            Throws<InvalidOperationException>(() => service.Post(new JournalPostRequest(date, new string('x', 51), null, null, Lines()), actor));
            Throws<InvalidOperationException>(() => service.Post(new JournalPostRequest(date, null, new string('x', 51), null, Lines()), actor));
            Throws<InvalidOperationException>(() => service.Post(new JournalPostRequest(date, null, null, new string('x', 501), Lines()), actor));
            Throws<InvalidOperationException>(() => service.Post(new JournalPostRequest(date, null, null, null, new[] {
                new JournalLineValues(new string('1',21), 10,0,null,null), new JournalLineValues("1100",0,10,null,null) }), actor));
            Throws<InvalidOperationException>(() => service.Post(new JournalPostRequest(date, null, null, null, new[] {
                new JournalLineValues("1000",10,0,new string('x',501),null),new JournalLineValues("1100",0,10,null,null) }), actor));
            Throws<InvalidOperationException>(() => service.Post(new JournalPostRequest(date, null, null, null, new[] {
                new JournalLineValues("1000",-5,10,null,null),new JournalLineValues("1100",20,5,null,null) }), actor));
            Check(repo.Calls == calls);
            int auth = 0; var changing = new JournalPostService(repo, _ => ++auth == 1);
            Throws<UnauthorizedAccessException>(() => changing.Post(request, actor)); Check(repo.Calls == calls);
            repo.Save = () => new JournalPostResult(42, true); Check(service.Post(request, actor).AlreadyPosted);
            repo.Save = () => null; Throws<InvalidOperationException>(() => service.Post(request, actor));
            repo.Save = () => new JournalPostResult(0, false); Throws<InvalidOperationException>(() => service.Post(request, actor));
            repo.Save = () => { throw new InvalidOperationException("transaction failed"); };
            Throws<InvalidOperationException>(() => service.Post(request, actor));
            bool allowed = true; changing = new JournalPostService(repo, _ => allowed);
            repo.Save = () => { allowed = false; return new JournalPostResult(43, false); };
            Check(changing.Post(request, actor).Id == 43);
            repo.Save = () => new JournalPostResult(44, false);
            result = service.Post(new JournalPostRequest(new DateTime(1753,1,1), " ", null, null, Lines()), actor);
            Check(result.Id == 44 && repo.Request.SourceType == null && repo.Request.SourceId == null && repo.Request.Description == null);
            calls = repo.Calls;
            Throws<InvalidOperationException>(() => service.Post(new JournalPostRequest(date, "Manual", null, null, new[] {
                new JournalLineValues("1000", 10000000000000000m, 0m, null, null),
                new JournalLineValues("1100", 0m, 10000000000000000m, null, null) }), actor));
            Check(repo.Calls == calls);
            decimal limit = JournalEntryPreparation.MaximumLineAmount;
            result = service.Post(new JournalPostRequest(date, "Manual", null, null, new[] {
                new JournalLineValues("1000", limit, 0m, null, null),
                new JournalLineValues("1100", 0m, limit, null, null) }), actor);
            Check(result.Id == 44 && repo.Request.Lines[0].Debit == limit && repo.Request.Lines[1].Credit == limit);
            result = service.Post(new JournalPostRequest(date, "Manual", null, null, new[] {
                new JournalLineValues("1000", limit + .004m, 0m, null, null),
                new JournalLineValues("1100", 0m, limit + .004m, null, null) }), actor);
            Check(repo.Request.Lines[0].Debit == limit && repo.Request.Lines[1].Credit == limit);
            calls = repo.Calls;
            Throws<JournalAmountException>(() => service.Post(new JournalPostRequest(date, "Manual", null, null, new[] {
                new JournalLineValues("1000", limit + .005m, 0m, null, null),
                new JournalLineValues("1100", 0m, limit + .005m, null, null) }), actor));
            Check(repo.Calls == calls);
            Throws<JournalAmountException>(() => SqlJournalWriter.Create(null, null, date, "Manual", null, null, "mock", new[] {
                new JournalLineValues("1000", limit + .01m, 0m, null, null),
                new JournalLineValues("1100", 0m, limit + .01m, null, null) }));
            result = service.Post(new JournalPostRequest(date, "Manual", null, null, new[] {
                new JournalLineValues("1000", limit, 0m, null, null), new JournalLineValues("1000", limit, 0m, null, null),
                new JournalLineValues("1100", 0m, limit, null, null), new JournalLineValues("1100", 0m, limit, null, null) }), actor);
            Check(result.Id == 44 && repo.Request.Lines.Count == 4);
            Throws<JournalAmountException>(() => JournalEntryPreparation.ValidateLineAmounts(0m, limit + .01m));
            Throws<JournalAmountException>(() => JournalEntryPreparation.ValidateLineAmounts(-1m, 0m));
            Throws<ArgumentNullException>(() => new JournalPostService(null, _ => true));
            Throws<ArgumentNullException>(() => new JournalPostService(repo, null));
            Throws<ArgumentNullException>(() => new SqlJournalPostRepository(null));
            Console.WriteLine("PASS: " + checks + " journal posting request/permission/result contracts; no database connection.");
            return checks;
        }
    }
}

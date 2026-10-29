using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Application;
using PharmacyManagement.Domain;
using PharmacyManagement.Infrastructure.SqlServer;

namespace PharmacyManagement.CoreTests
{
    internal static class JournalSubmissionRecoveryTests
    {
        private static int checks;
        private static void Check(bool value) { if (!value) throw new Exception("Journal recovery contract failed."); checks++; }
        private static void Throws<T>(Action action) where T : Exception
        { try { action(); } catch (T) { checks++; return; } throw new Exception("Expected " + typeof(T).Name); }
        private static List<JournalLineValues> Lines() => new List<JournalLineValues> {
            new JournalLineValues(" 1000 ",10.005m,0,"مدين 500 mg",null),new JournalLineValues("2000",0,10.005m,"دائن",7)};
        private sealed class Probe : IJournalSubmissionStatusRepository
        {
            internal int Calls;
            internal JournalSubmissionStatusRequest Request;
            internal OperationActor Actor;
            internal CancellationToken Token;
            internal TaskCompletionSource<JournalSubmissionStatus> Pending=new TaskCompletionSource<JournalSubmissionStatus>();
            public Task<JournalSubmissionStatus> FindAsync(JournalSubmissionStatusRequest request,OperationActor actor,CancellationToken token)
            {Calls++;Request=request;Actor=actor;Token=token;return Pending.Task;}
        }
        internal static int Run()
        {
            checks=0;DateTime date=new DateTime(2026,1,15);var source=Lines();
            var pending=JournalPendingRequest.Create(date,"Manual"," مُتوازن 500 mg ",source);
            Guid id;Check(Guid.TryParseExact(pending.Request.SourceId,"N",out id)&&id!=Guid.Empty);
            Check(pending.Request.SourceType=="Manual"&&pending.Request.Date==date&&pending.Request.Description=="مُتوازن 500 mg");
            Check(pending.Request.Lines.Count==2&&pending.Request.Lines[0].AccountCode=="1000"&&pending.Request.Lines[0].Debit==10.00m&&pending.Request.Lines[1].Credit==10.00m);
            source.Clear();Check(pending.Request.Lines.Count==2);
            var restored=JournalPendingRequest.Restore(new JournalPostRequest(date,"Manual",pending.Request.SourceId,pending.Request.Description,pending.Request.Lines));
            Check(restored.Request.SourceId==pending.Request.SourceId&&restored.HasSameContent(pending.Request));
            Check(!pending.HasSameContent(JournalPendingRequest.Create(date,"Manual","مُتوازن 500 mg",Lines()).Request));
            Check(!pending.HasSameContent(new JournalPostRequest(date.AddDays(1),"Manual",pending.Request.SourceId,pending.Request.Description,pending.Request.Lines)));
            Check(!pending.HasSameContent(new JournalPostRequest(date,"OpeningEntry",pending.Request.SourceId,pending.Request.Description,pending.Request.Lines)));
            Check(!pending.HasSameContent(new JournalPostRequest(date,"Manual",pending.Request.SourceId,"بيان مختلف",pending.Request.Lines)));
            Check(!pending.HasSameContent(new JournalPostRequest(date,"Manual",pending.Request.SourceId,pending.Request.Description,new[]{
                new JournalLineValues("1000",10m,0,"مدين 500 mg",null),new JournalLineValues("2000",0,10m,"دائن",8)})));
            Check(!pending.HasSameContent(new JournalPostRequest(date,"Manual",pending.Request.SourceId,pending.Request.Description,new[]{
                new JournalLineValues("1000",11m,0,"مدين 500 mg",null),new JournalLineValues("2000",0,11m,"دائن",7)})));
            Check(JournalPendingRequest.Create(date,"OpeningEntry",null,Lines()).Request.SourceType=="OpeningEntry");
            Throws<ArgumentNullException>(()=>JournalPendingRequest.Restore(null));
            foreach(string identity in new[]{null,"",Guid.Empty.ToString("N"),"not-an-id"})
                Throws<InvalidOperationException>(()=>JournalPendingRequest.Restore(new JournalPostRequest(date,"Manual",identity,null,Lines())));
            Throws<InvalidOperationException>(()=>JournalPendingRequest.Restore(new JournalPostRequest(date,"SalesInvoice",Guid.NewGuid().ToString("N"),null,Lines())));
            Throws<InvalidOperationException>(()=>JournalPendingRequest.Create(date,"Manual",null,new[]{new JournalLineValues("1000",1,0,null,null)}));
            var repo=new Probe();bool allowed=true;
            var actor=new OperationActor{UserName="recovery",UserRole="Manager"};
            var service=new JournalSubmissionStatusService(repo,a=>allowed&&a.UserName=="recovery"&&a.UserRole=="Manager");
            var request=new JournalSubmissionStatusRequest(" Manual "," "+pending.Request.SourceId+" ");
            Check(!new JournalSubmissionStatus(0).IsPosted&&new JournalSubmissionStatus(42).IsPosted);
            Throws<ArgumentOutOfRangeException>(()=>new JournalSubmissionStatus(-1));
            Throws<ArgumentNullException>(()=>new JournalSubmissionStatusService(null));
            Throws<ArgumentNullException>(()=>new SqlJournalSubmissionStatusRepository(null));
            Throws<UnauthorizedAccessException>(()=>new JournalSubmissionStatusService(repo).FindAsync(request,actor,CancellationToken.None).GetAwaiter().GetResult());
            Throws<UnauthorizedAccessException>(()=>service.FindAsync(request,null,CancellationToken.None).GetAwaiter().GetResult());
            Throws<UnauthorizedAccessException>(()=>service.FindAsync(request,new OperationActor{UserName="other",UserRole="Manager"},CancellationToken.None).GetAwaiter().GetResult());
            Throws<UnauthorizedAccessException>(()=>service.FindAsync(request,new OperationActor{UserName="recovery",UserRole="Cashier"},CancellationToken.None).GetAwaiter().GetResult());
            Throws<ArgumentNullException>(()=>service.FindAsync(null,actor,CancellationToken.None).GetAwaiter().GetResult());
            foreach(var invalid in new[]{new JournalSubmissionStatusRequest(null,"x"),new JournalSubmissionStatusRequest("Manual"," "),
                new JournalSubmissionStatusRequest(new string('x',51),"x"),new JournalSubmissionStatusRequest("Manual",new string('x',51))})
                Throws<InvalidOperationException>(()=>service.FindAsync(invalid,actor,CancellationToken.None).GetAwaiter().GetResult());
            using(var cancellation=new CancellationTokenSource())
            {cancellation.Cancel();Throws<OperationCanceledException>(()=>service.FindAsync(request,actor,cancellation.Token).GetAwaiter().GetResult());}
            Check(repo.Calls==0);
            var reading=service.FindAsync(request,actor,CancellationToken.None);
            actor.UserName="changed";repo.Pending.SetResult(new JournalSubmissionStatus(42));
            Check(reading.GetAwaiter().GetResult().JournalId==42&&repo.Actor.UserName=="recovery"&&repo.Request.SourceType=="Manual"&&repo.Request.SourceId==pending.Request.SourceId);
            actor.UserName="recovery";repo.Pending=new TaskCompletionSource<JournalSubmissionStatus>();
            reading=service.FindAsync(request,actor,CancellationToken.None);allowed=false;repo.Pending.SetResult(new JournalSubmissionStatus(42));
            Throws<UnauthorizedAccessException>(()=>reading.GetAwaiter().GetResult());allowed=true;
            using(var cancellation=new CancellationTokenSource())
            {
                repo.Pending=new TaskCompletionSource<JournalSubmissionStatus>();reading=service.FindAsync(request,actor,cancellation.Token);
                Check(repo.Token==cancellation.Token);cancellation.Cancel();repo.Pending.SetResult(new JournalSubmissionStatus(0));
                Throws<OperationCanceledException>(()=>reading.GetAwaiter().GetResult());
            }
            repo.Pending=new TaskCompletionSource<JournalSubmissionStatus>();reading=service.FindAsync(request,actor,CancellationToken.None);repo.Pending.SetResult(null);
            Throws<InvalidOperationException>(()=>reading.GetAwaiter().GetResult());
            repo.Pending=new TaskCompletionSource<JournalSubmissionStatus>();reading=service.FindAsync(request,actor,CancellationToken.None);repo.Pending.SetException(new InvalidOperationException("read failed"));
            Throws<InvalidOperationException>(()=>reading.GetAwaiter().GetResult());
            repo.Pending=new TaskCompletionSource<JournalSubmissionStatus>();reading=service.FindAsync(request,actor,CancellationToken.None);repo.Pending.SetResult(new JournalSubmissionStatus(0));
            Check(!reading.GetAwaiter().GetResult().IsPosted);
            Console.WriteLine("PASS: "+checks+" journal immutable identity/payload and authorized/cancellable status contracts; no database connection.");
            return checks;
        }
    }
}

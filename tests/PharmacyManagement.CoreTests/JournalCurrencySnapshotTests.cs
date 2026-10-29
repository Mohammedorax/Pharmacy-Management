using System;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Application;
using PharmacyManagement.Domain;
using PharmacyManagement.Infrastructure.SqlServer;
namespace PharmacyManagement.CoreTests
{
    internal static class JournalCurrencySnapshotTests
    {
        private static int checks;
        private static void Check(bool value, string message) { ++checks; if (!value) throw new InvalidOperationException(message); }
        private static void Reject(Action action) { bool rejected=false;try{action();}catch(InvalidOperationException){rejected=true;}Check(rejected,"Invalid currency snapshot accepted."); }
        private static readonly JournalLineValues[] Lines={new JournalLineValues("1000",1.25m,0m,"مدين",7),new JournalLineValues("2000",0m,1.25m,"دائن",7)};
        private static JournalPostRequest Request(string currency) => new JournalPostRequest(new DateTime(2026,10,7),"Manual","0123456789abcdef0123456789abcdef","بيان الأصل",Lines,currency);
        private sealed class Repository : IAsyncJournalPostRepository
        {
            internal JournalPostRequest Last;internal int Calls;
            public JournalPostResult Post(JournalPostRequest request,OperationActor actor){Last=request;++Calls;return new JournalPostResult(41,false);}
            public Task<JournalPostResult> PostAsync(JournalPostRequest request,OperationActor actor,CancellationToken token){token.ThrowIfCancellationRequested();return Task.FromResult(Post(request,actor));}
        }
        internal static int Run()
        {
            checks=0;
            var legacy=new JournalPostRequest(new DateTime(2026,10,7),"Manual","0123456789abcdef0123456789abcdef","بيان الأصل",Lines);
            Check(JournalPostService.PrepareRequest(legacy).ExpectedSystemCurrencyCode==null,"Legacy unknown currency was invented.");
            var captured=JournalPostService.PrepareRequest(Request(" yer "));
            Check(captured.ExpectedSystemCurrencyCode=="YER","Currency snapshot was not normalized.");
            Check(captured.SourceId==legacy.SourceId&&captured.Date==legacy.Date&&captured.Lines[0].Debit==1.25m&&captured.Lines[1].Credit==1.25m&&captured.Lines[0].CostCenterId==7,"Capturing currency changed money/identity/center.");
            var pending=JournalPendingRequest.Restore(captured);
            Check(pending.HasSameContent(Request("yEr")),"Equivalent currency identity failed content matching.");
            Check(!pending.HasSameContent(Request("USD")),"Changed currency matched the same pending payload.");
            Check(!pending.HasSameContent(legacy),"Captured currency silently matched an unknown legacy currency.");
            var fresh=JournalPendingRequest.Create(legacy.Date,"OpeningEntry",legacy.Description,Lines,"usd");
            Check(fresh.Request.ExpectedSystemCurrencyCode=="USD"&&Guid.ParseExact(fresh.Request.SourceId,"N")!=Guid.Empty,"Fresh snapshot or identity missing.");
            Reject(()=>JournalPostService.PrepareRequest(Request("")));
            Reject(()=>JournalPostService.PrepareRequest(Request("  ")));
            Reject(()=>JournalPostService.PrepareRequest(Request("ABCDEFGHIJK")));
            var repository=new Repository();var service=new JournalPostService(repository,actor=>true);var user=new OperationActor{UserName="Owner",UserRole="Manager"};
            service.Post(Request("yer"),user);Check(repository.Last.ExpectedSystemCurrencyCode=="YER","Sync dispatch lost currency.");
            service.PostAsync(Request(" usd "),user,CancellationToken.None).GetAwaiter().GetResult();Check(repository.Last.ExpectedSystemCurrencyCode=="USD","Native async dispatch lost currency.");
            int calls=repository.Calls;Reject(()=>service.Post(Request(" "),user));Check(repository.Calls==calls,"Invalid snapshot reached writer.");
            service.Post(legacy,user);Check(repository.Last.ExpectedSystemCurrencyCode==null,"Legacy API changed its unknown-currency contract.");
            Check(SqlJournalRequestComparison.Matches(captured,captured.Date,captured.Description,captured.Lines," yer "),"Stored currency normalization rejected equivalent content.");
            Check(!SqlJournalRequestComparison.Matches(captured,captured.Date,captured.Description,captured.Lines,"USD"),"Different stored currency matched an accepted request.");
            Check(!SqlJournalRequestComparison.Matches(captured,captured.Date,captured.Description,captured.Lines,null),"Unknown historical currency proved a known request currency.");
            Check(SqlJournalRequestComparison.Matches(legacy,legacy.Date,legacy.Description,legacy.Lines,"YER"),"Legacy accepted request could not settle by its known original payload.");
            var documentLines=new[]{new JournalDocumentLine(1,"1000","مدين",1.25m,0m,"مدين",7,"مركز"),new JournalDocumentLine(2,"2000","دائن",0m,1.25m,"دائن",7,"مركز")};
            var oldDocument=new JournalDocumentSnapshot(41,legacy.Date,"Manual",legacy.SourceId,legacy.Description,"Owner",documentLines);
            Check(oldDocument.SystemCurrencyCode==null,"Legacy document invented a currency.");
            var newDocument=new JournalDocumentSnapshot(41,legacy.Date,"Manual",legacy.SourceId,legacy.Description,"Owner",documentLines," yer ");
            Check(newDocument.SystemCurrencyCode=="YER"&&newDocument.Lines[0].Debit==1.25m&&newDocument.Lines[1].Credit==1.25m&&newDocument.Lines[0].CostCenterId==7&&newDocument.Id==41,"Document snapshot changed currency or financial payload.");
            foreach(string invalid in new[]{"","  ","ABCDEFGHIJK"})
            {
                bool rejected=false;try{new JournalDocumentSnapshot(41,legacy.Date,"Manual",legacy.SourceId,legacy.Description,"Owner",documentLines,invalid);}catch(ArgumentException){rejected=true;}
                Check(rejected,"Invalid persisted document currency was accepted.");
            }
            return checks;
        }
    }
}

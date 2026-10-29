using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Application;
using PharmacyManagement.Domain;

internal static class MedicineUnitCatalogServiceTests
{
    static int checks;
    static void Require(bool value,string message){checks++;if(!value)throw new Exception(message);}
    static void Reject<T>(Action action,string message) where T:Exception
    {checks++;try{action();}catch(T){return;}throw new Exception("Expected "+typeof(T).Name+": "+message);}
    static OperationActor Actor()=>new OperationActor {UserName="UnitEditor",UserRole="Manager"};
    static MedicineUnitCatalogRequest Request()
    {
        Guid id=Guid.NewGuid();return new MedicineUnitCatalogRequest {RequestId=Guid.NewGuid(),MedicineName="دواء 500 mg",ExpectedRevision=0,BaseUnitId=id,CurrencyCode="YER",ConfirmInitialStockUnit=true,ExpectedInitialStockQuantity=100,
            Units=new[]{new MedicineUnitInput {Id=id,Name="حبة",BaseUnitsPerUnit=1,SellingPrice=2.50m,BuyingPrice=1.25m},new MedicineUnitInput {Id=Guid.NewGuid(),Name="شريط",BaseUnitsPerUnit=10,SellingPrice=24m,BuyingPrice=12m}}};
    }
    static MedicineUnitCatalogSnapshot Snapshot(string medicine)
    {
        var write=MedicineUnitCatalogValidation.Prepare(Request());
        return new MedicineUnitCatalogSnapshot(Guid.NewGuid(),medicine,write.BaseUnitId,1,100,100,"YER",write.Units);
    }
    sealed class Fake : IMedicineUnitCatalogRepository
    {
        internal int Reads,Writes;
        internal MedicineUnitCatalogWrite Write;
        internal OperationActor Actor;
        internal Task<MedicineUnitCatalogSnapshot> Read=Task.FromResult<MedicineUnitCatalogSnapshot>(null);
        internal Func<MedicineUnitCatalogWrite,Action,CancellationToken,Task<MedicineUnitCatalogSaveResult>> Save;
        public Task<MedicineUnitCatalogSnapshot> GetAsync(string name,CancellationToken token){Reads++;return Read;}
        public Task<MedicineUnitCatalogSaveResult> SaveAsync(MedicineUnitCatalogWrite request,OperationActor actor,Action commit,CancellationToken token)
        {Writes++;Write=request;Actor=actor;if(Save!=null)return Save(request,commit,token);commit();return Task.FromResult(new MedicineUnitCatalogSaveResult(request.RequestId,request.ExpectedRevision+1,false));}
    }
    public static int Run()
    {
        checks=0;
        var fake=new Fake();var denied=new MedicineUnitCatalogService(fake,actor=>false);
        Reject<UnauthorizedAccessException>(()=>denied.GetAsync("دواء",Actor(),CancellationToken.None).GetAwaiter().GetResult(),"Default deny reads");
        Reject<UnauthorizedAccessException>(()=>denied.SaveAsync(Request(),Actor(),CancellationToken.None).GetAwaiter().GetResult(),"Default deny writes");
        Require(fake.Reads==0&&fake.Writes==0,"Denied calls must not reach storage");
        var service=new MedicineUnitCatalogService(fake,actor=>actor.UserName=="UnitEditor"&&actor.UserRole=="Manager");
        Reject<UnauthorizedAccessException>(()=>service.GetAsync("دواء",null,CancellationToken.None).GetAwaiter().GetResult(),"Missing actor");
        using(var canceled=new CancellationTokenSource())
        {canceled.Cancel();Reject<OperationCanceledException>(()=>service.SaveAsync(Request(),Actor(),canceled.Token).GetAwaiter().GetResult(),"Canceled before write");Require(fake.Writes==0,"Canceled request never reaches repository");}
        var request=Request();var prepared=MedicineUnitCatalogValidation.Prepare(request);
        string originalName=prepared.Units[0].Name;decimal originalPrice=prepared.Units[0].SellingPrice;
        request.Units[0].Name="Changed";request.Units[0].SellingPrice=999;
        Require(prepared.Units[0].Name==originalName&&prepared.Units[0].SellingPrice==originalPrice,"Prepared definitions are immutable snapshots");
        var payload=prepared.Payload;payload[0]^=255;var hash=prepared.Fingerprint;hash[0]^=255;
        Require(prepared.Payload[0]!=payload[0]&&prepared.Fingerprint[0]!=hash[0],"Payload and fingerprint cannot be mutated by caller");
        request=Request();prepared=MedicineUnitCatalogValidation.Prepare(request);request.Units[0].SellingPrice=2.500m;
        Require(prepared.Fingerprint.SequenceEqual(MedicineUnitCatalogValidation.Prepare(request).Fingerprint),"Equivalent decimal scales have the same request identity");
        request.Units[0].SellingPrice=2.51m;
        Require(!prepared.Fingerprint.SequenceEqual(MedicineUnitCatalogValidation.Prepare(request).Fingerprint),"Changed price changes fingerprint");
        request=Request();request.ConfirmInitialStockUnit=false;
        Reject<InvalidOperationException>(()=>MedicineUnitCatalogValidation.Prepare(request),"Initial base-stock interpretation requires explicit confirmation");
        request=Request();request.Units[1].Name=" حبة ";
        Reject<InvalidOperationException>(()=>MedicineUnitCatalogValidation.Prepare(request),"Duplicate normalized names");
        request=Request();request.Units[1].Id=request.Units[0].Id;
        Reject<InvalidOperationException>(()=>MedicineUnitCatalogValidation.Prepare(request),"Duplicate unit identities");
        request=Request();request.BaseUnitId=request.Units[1].Id;
        Reject<InvalidOperationException>(()=>MedicineUnitCatalogValidation.Prepare(request),"Base factor must equal one");
        request=Request();request.ExpectedRevision=1;
        Reject<InvalidOperationException>(()=>MedicineUnitCatalogValidation.Prepare(request),"Editing must not reconfirm or reinterpret opening stock");
        request=Request();request.ExpectedRevision=long.MaxValue;
        Reject<InvalidOperationException>(()=>MedicineUnitCatalogValidation.Prepare(request),"Revision overflow");
        request=Request();request.Units[1].SellingPrice=1.001m;
        Reject<InvalidOperationException>(()=>MedicineUnitCatalogValidation.Prepare(request),"Reject price truncation before storage");
        var pendingRead=new TaskCompletionSource<MedicineUnitCatalogSnapshot>();fake=new Fake {Read=pendingRead.Task};bool allowed=true;
        service=new MedicineUnitCatalogService(fake,actor=>allowed&&actor.UserName=="UnitEditor");var actorSource=Actor();
        var readTask=service.GetAsync("دواء 500 mg",actorSource,CancellationToken.None);actorSource.UserName="Other";pendingRead.SetResult(Snapshot("دواء 500 mg"));
        Require(readTask.GetAwaiter().GetResult().Revision==1,"Read captures actor before awaiting storage");
        pendingRead=new TaskCompletionSource<MedicineUnitCatalogSnapshot>();fake.Read=pendingRead.Task;
        readTask=service.GetAsync("دواء 500 mg",Actor(),CancellationToken.None);allowed=false;pendingRead.SetResult(Snapshot("دواء 500 mg"));
        Reject<UnauthorizedAccessException>(()=>readTask.GetAwaiter().GetResult(),"Revoked reader cannot receive late data");
        var pendingSave=new TaskCompletionSource<MedicineUnitCatalogSaveResult>();Action authorize=null;
        fake=new Fake {Save=(write,commit,token)=>{authorize=commit;return pendingSave.Task;}};allowed=true;
        service=new MedicineUnitCatalogService(fake,a=>allowed);request=Request();var writeTask=service.SaveAsync(request,Actor(),CancellationToken.None);
        request.Units[0].SellingPrice=999;Require(fake.Write.Units[0].SellingPrice==2.50m,"Async write captures input before awaiting");
        allowed=false;try{authorize();throw new Exception("Revoked commit was allowed");}catch(UnauthorizedAccessException ex){pendingSave.SetException(ex);}
        Reject<UnauthorizedAccessException>(()=>writeTask.GetAwaiter().GetResult(),"Authorization is checked before commit");
        using(var afterCommitCancel=new CancellationTokenSource())
        {
            fake=new Fake {Save=(write,commit,token)=>{commit();afterCommitCancel.Cancel();return Task.FromResult(new MedicineUnitCatalogSaveResult(write.RequestId,1,false));}};
            service=new MedicineUnitCatalogService(fake,a=>true);
            Require(service.SaveAsync(Request(),Actor(),afterCommitCancel.Token).GetAwaiter().GetResult().Revision==1,"Postcommit cancellation must not hide successful receipt");
        }
        fake=new Fake {Read=Task.FromResult(Snapshot("صنف آخر"))};service=new MedicineUnitCatalogService(fake,a=>true);
        Reject<InvalidOperationException>(()=>service.GetAsync("دواء 500 mg",Actor(),CancellationToken.None).GetAwaiter().GetResult(),"Wrong medicine returned by data source");
        Console.WriteLine("PASS unit catalogue service checks="+checks+"; immutable requests, authorization, cancellation and consistency; no SQL.");return checks;
    }
}

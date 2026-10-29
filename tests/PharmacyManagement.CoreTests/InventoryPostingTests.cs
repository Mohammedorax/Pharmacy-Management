using System;
using PharmacyManagement.Application;

namespace PharmacyManagement.CoreTests
{
    internal static class InventoryPostingTests
    {
        private static int checks;
        private static void Check(bool condition){if(!condition)throw new Exception("Inventory posting contract failed.");checks++;}
        private static void Throws<T>(Action action)where T:Exception {try{action();}catch(T){checks++;return;}throw new Exception("Expected "+typeof(T).Name);}
        private sealed class Repository:IInventoryPostRepository
        {
            internal int Calls;internal InventoryPostRequest Request;internal OperationActor Actor;
            internal Func<InventoryPostRequest,InventoryPostResult> Save=r=>new InventoryPostResult(r.SourceId,10,15,5);
            public InventoryPostResult Post(InventoryPostRequest request,OperationActor actor){Calls++;Request=request;Actor=actor;return Save(request);}
        }
        internal static int Run()
        {
            checks=0;var repo=new Repository();var service=new InventoryPostService(repo,_=>true);
            var actor=new OperationActor{UserName="tester",UserRole="Manager"};
            var request=new InventoryPostRequest(InventoryPostKind.Receipt," test-id "," باراسيتامول 500 mg ",5,10.005m," سبب الحركة ");
            Check(request.HasSameContent(new InventoryPostRequest(request.Kind,"other-id","باراسيتامول 500 mg",5,10.005m,"سبب الحركة")));
            Check(!request.HasSameContent(null));
            Check(!request.HasSameContent(new InventoryPostRequest(request.Kind,"x",request.MedicineName,6,request.UnitAmount,request.Notes)));
            Check(!request.HasSameContent(new InventoryPostRequest(InventoryPostKind.Issue,"x",request.MedicineName,5,request.UnitAmount,request.Notes)));
            Check(!request.HasSameContent(new InventoryPostRequest(request.Kind,"x","other",5,request.UnitAmount,request.Notes)));
            Check(!request.HasSameContent(new InventoryPostRequest(request.Kind,"x",request.MedicineName,5,null,request.Notes)));
            Check(!request.HasSameContent(new InventoryPostRequest(request.Kind,"x",request.MedicineName,5,request.UnitAmount,"other")));
            Check(!new InventoryPostResult("x",0,1,1).AlreadyPosted && new InventoryPostResult("x",0,1,1,true).AlreadyPosted);
            Throws<InvalidOperationException>(()=>service.Post(new InventoryPostRequest(InventoryPostKind.Receipt,"غير صالح","drug",1,null,"reason"),actor));
            var result=service.Post(request,actor);
            Check(result.SourceId=="test-id"&&result.Before==10&&result.After==15&&result.Delta==5);
            Check(repo.Request.MedicineName=="باراسيتامول 500 mg"&&repo.Request.Notes=="سبب الحركة"&&repo.Request.UnitAmount==10.005m);
            Check(repo.Request.SourceType=="StockReceipt"&&repo.Request.AuditAction=="StockReceiptSaved");
            Check(!ReferenceEquals(repo.Actor,actor));actor.UserName="changed";Check(repo.Actor.UserName=="tester");actor.UserName="tester";
            int calls=repo.Calls;
            Throws<UnauthorizedAccessException>(()=>service.Post(request,null));
            Throws<UnauthorizedAccessException>(()=>new InventoryPostService(repo,_=>false).Post(request,actor));
            Throws<UnauthorizedAccessException>(()=>service.Post(request,new OperationActor{UserName=" ",UserRole="Manager"}));
            Check(repo.Calls==calls);
            Throws<ArgumentNullException>(()=>service.Post(null,actor));
            Throws<InvalidOperationException>(()=>service.Post(new InventoryPostRequest((InventoryPostKind)99,"x","drug",1,null,"reason"),actor));
            foreach(var kind in new[]{InventoryPostKind.OpeningStock,InventoryPostKind.Adjustment,InventoryPostKind.Receipt,InventoryPostKind.Issue})
            {
                bool balance=kind==InventoryPostKind.OpeningStock||kind==InventoryPostKind.Adjustment;
                service.Post(new InventoryPostRequest(kind,"x","drug",balance?0:1,null,"reason"),actor);Check(repo.Request.Kind==kind);
                Throws<InvalidOperationException>(()=>service.Post(new InventoryPostRequest(kind,"x","drug",balance?-1:0,null,"reason"),actor));
            }
            foreach(var id in new[]{"",new string('x',51)})Throws<InvalidOperationException>(()=>service.Post(new InventoryPostRequest(InventoryPostKind.Receipt,id,"drug",1,null,"reason"),actor));
            foreach(var name in new[]{"",new string('x',201)})Throws<InvalidOperationException>(()=>service.Post(new InventoryPostRequest(InventoryPostKind.Receipt,"x",name,1,null,"reason"),actor));
            foreach(var notes in new[]{"",new string('x',501)})Throws<InvalidOperationException>(()=>service.Post(new InventoryPostRequest(InventoryPostKind.Receipt,"x","drug",1,null,notes),actor));
            foreach(var amount in new[]{-1m,10000000000000000m})Throws<InvalidOperationException>(()=>service.Post(new InventoryPostRequest(InventoryPostKind.Receipt,"x","drug",1,amount,"reason"),actor));
            Throws<InvalidOperationException>(()=>service.Post(new InventoryPostRequest(InventoryPostKind.Adjustment,"x","drug",1,1m,"reason"),actor));
            service.Post(new InventoryPostRequest(InventoryPostKind.Issue,"x","drug",int.MaxValue,9999999999999999.99m,new string('n',500)),actor);Check(repo.Request.Quantity==int.MaxValue);
            int authorized=0;calls=repo.Calls;
            Throws<UnauthorizedAccessException>(()=>new InventoryPostService(repo,_=>++authorized==1).Post(request,actor));Check(repo.Calls==calls);
            bool allowed=true;repo.Save=r=>{allowed=false;return new InventoryPostResult(r.SourceId,10,15,5);};
            Check(new InventoryPostService(repo,_=>allowed).Post(request,actor).After==15);
            repo.Save=r=>null;Throws<InvalidOperationException>(()=>service.Post(request,actor));
            repo.Save=r=>new InventoryPostResult("wrong",0,0,0);Throws<InvalidOperationException>(()=>service.Post(request,actor));
            repo.Save=r=>throw new InvalidOperationException("repository failure");Throws<InvalidOperationException>(()=>service.Post(request,actor));
            Console.WriteLine("PASS: "+checks+" inventory posting validation, captured actor and acceptance contracts; no SQL.");return checks;
        }
    }
}

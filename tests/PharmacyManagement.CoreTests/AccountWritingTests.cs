using System;
using System.Collections.Generic;
using PharmacyManagement.Domain;
using PharmacyManagement.Application;
using PharmacyManagement.Infrastructure.SqlServer;

namespace PharmacyManagement.CoreTests
{
    internal static class AccountWritingTests
    {
        private static int checks;
        private static void Check(bool value){if(!value)throw new Exception("Account writing contract failed.");checks++;}
        private static void Throws<T>(Action action)where T:Exception{try{action();}catch(T){checks++;return;}throw new Exception("Expected "+typeof(T).Name);}
        private sealed class Repository:IAccountWriteRepository
        {
            internal int Calls;internal AccountSaveRequest Request;internal OperationActor Actor;
            internal Func<string,AccountWriteResult> Result=code=>new AccountWriteResult(code,true);
            public AccountWriteResult Save(AccountSaveRequest request,OperationActor actor){Calls++;Request=request;Actor=actor;return Result(request.Code);}
            public AccountWriteResult Delete(string code,OperationActor actor){Calls++;Actor=actor;return Result(code);}
        }
        private static AccountSaveRequest Request(string code=" A ",string name=" صندوق Cash 500 mg ",string type=null,string parent=null)
            =>new AccountSaveRequest(code,name,type,parent,false);
        internal static int Run()
        {
            checks=0;var parents=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase){{"A",null},{"B","A"},{"C","B"},{"X","Y"},{"Y","X"},{"R","missing"}};
            AccountHierarchy.ValidateParent("new"," C ",parents);checks++;
            AccountHierarchy.ValidateParent("B","a",parents);checks++;
            AccountHierarchy.ValidateParent("A",null,parents);checks++;
            foreach(var parent in new[]{"A","C","X","R","missing"})Throws<InvalidOperationException>(()=>AccountHierarchy.ValidateParent("A",parent,parents));
            Throws<ArgumentNullException>(()=>AccountHierarchy.ValidateParent("A",null,null));
            Throws<InvalidOperationException>(()=>AccountHierarchy.ValidateParent(" ",null,parents));
            var repo=new Repository();var actor=new OperationActor{UserName=" tester ",UserRole=" Manager "};var service=new AccountWriteService(repo,_=>true);
            var result=service.Save(Request(),actor);
            Check(result.Code=="A"&&result.Changed&&repo.Request.Name=="صندوق Cash 500 mg"&&repo.Request.Type=="Asset"&&!repo.Request.IsActive&&repo.Request.ParentCode==null);
            Check(repo.Actor.UserName=="tester"&&repo.Actor.UserRole=="Manager");actor.UserName="changed";Check(repo.Actor.UserName=="tester");actor.UserName=" tester ";
            foreach(var type in new[]{"Asset","Liability","Equity","Revenue","Expense"})
            {service.Save(Request(type:type.ToLowerInvariant()),actor);Check(repo.Request.Type==type);}
            service.Save(Request(parent:" P "),actor);Check(repo.Request.ParentCode=="P");
            int calls=repo.Calls;
            Throws<UnauthorizedAccessException>(()=>service.Save(Request(),null));Throws<UnauthorizedAccessException>(()=>service.Delete("A",null));
            Throws<UnauthorizedAccessException>(()=>new AccountWriteService(repo,_=>false).Save(Request(),actor));
            Throws<UnauthorizedAccessException>(()=>new AccountWriteService(repo,_=>false).Delete("A",actor));
            Throws<UnauthorizedAccessException>(()=>service.Save(Request(),new OperationActor{UserName=" ",UserRole="Manager"}));
            Throws<UnauthorizedAccessException>(()=>service.Delete("A",new OperationActor{UserName="tester",UserRole=" "}));
            Throws<ArgumentNullException>(()=>service.Save(null,actor));
            foreach(var code in new[]{" ",new string('a',21)}){Throws<InvalidOperationException>(()=>service.Save(Request(code:code),actor));Throws<InvalidOperationException>(()=>service.Delete(code,actor));}
            foreach(var name in new[]{" ",new string('a',201)})Throws<InvalidOperationException>(()=>service.Save(Request(name:name),actor));
            Throws<InvalidOperationException>(()=>service.Save(Request(type:"Wrong"),actor));
            Throws<InvalidOperationException>(()=>service.Save(Request(parent:new string('a',21)),actor));
            Throws<InvalidOperationException>(()=>service.Save(Request(parent:" a "),actor));
            Throws<InvalidOperationException>(()=>service.Save(Request(),new OperationActor{UserName=new string('a',201),UserRole="Manager"}));
            Throws<InvalidOperationException>(()=>service.Delete("A",new OperationActor{UserName="tester",UserRole=new string('a',51)}));
            Check(repo.Calls==calls);
            int auth=0;Throws<UnauthorizedAccessException>(()=>new AccountWriteService(repo,_=>++auth==1).Save(Request(),actor));Check(repo.Calls==calls);
            auth=0;Throws<UnauthorizedAccessException>(()=>new AccountWriteService(repo,_=>++auth==1).Delete("A",actor));Check(repo.Calls==calls);
            Check(service.Delete(" A ",actor).Code=="A");
            repo.Result=code=>new AccountWriteResult(code,false);Check(!service.Delete("A",actor).Changed);Throws<InvalidOperationException>(()=>service.Save(Request(),actor));
            repo.Result=code=>null;Throws<InvalidOperationException>(()=>service.Save(Request(),actor));Throws<InvalidOperationException>(()=>service.Delete("A",actor));
            repo.Result=code=>new AccountWriteResult("other",true);Throws<InvalidOperationException>(()=>service.Save(Request(),actor));Throws<InvalidOperationException>(()=>service.Delete("A",actor));
            repo.Result=code=>{throw new InvalidOperationException("rollback");};Throws<InvalidOperationException>(()=>service.Save(Request(),actor));
            bool allowed=true;repo.Result=code=>{allowed=false;return new AccountWriteResult(code,true);};Check(new AccountWriteService(repo,_=>allowed).Save(Request(),actor).Changed);
            repo.Result=code=>new AccountWriteResult(code,true);Check(service.Save(Request(new string('a',20),new string('ن',200)),actor).Code.Length==20);
            Throws<ArgumentNullException>(()=>new AccountWriteService(null,_=>true));Throws<ArgumentNullException>(()=>new AccountWriteService(repo,null));Throws<ArgumentNullException>(()=>new SqlAccountWriteRepository(null));
            Console.WriteLine("PASS: "+checks+" account hierarchy, validation, authorization, captured actor and acceptance contracts; no database connection.");return checks;
        }
    }
}

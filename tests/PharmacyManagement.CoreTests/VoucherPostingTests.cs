using System;
using PharmacyManagement.Domain;
using PharmacyManagement.Application;
using PharmacyManagement.Infrastructure.SqlServer;

namespace PharmacyManagement.CoreTests
{
    internal static class VoucherPostingTests
    {
        private static int checks;
        private static void Check(bool value) {if(!value)throw new Exception("Voucher posting contract failed.");checks++;}
        private static void Throws<T>(Action action) where T:Exception
        {try {action();}catch(T){checks++;return;}throw new Exception("Expected "+typeof(T).Name);}
        private sealed class Repository : IVoucherPostRepository
        {
            internal int Calls;internal PreparedVoucher Voucher;internal DateTime Date;internal OperationActor Actor;
            internal Func<VoucherPostResult> Save=()=>new VoucherPostResult(41,42);
            public VoucherPostResult Post(DateTime date,PreparedVoucher voucher,OperationActor actor)
            {Calls++;Date=date;Voucher=voucher;Actor=actor;return Save();}
        }
        private static VoucherPostRequest Request(decimal amount=10.005m, string type="ReceiptExternal", string account=" 1100 ",
            string cash=null, string party="عميل Cash 500 mg",string notes=" بيان السند ",int? center=7,DateTime? date=null)
            => new VoucherPostRequest(type,date??new DateTime(2026,10,1,12,30,45),party,account,amount,notes,cash,center);
        private sealed class DurableRepository : IIdempotentVoucherPostRepository
        {
            internal int LegacyCalls, DurableCalls; internal Guid Identity; internal PreparedVoucher Voucher;
            public VoucherPostResult Post(DateTime date, PreparedVoucher voucher, OperationActor actor)
            { LegacyCalls++; throw new Exception("A durable request reached the legacy writer."); }
            public VoucherPostResult Post(Guid requestId, DateTime date, PreparedVoucher voucher, OperationActor actor)
            { DurableCalls++; Identity=requestId; Voucher=voucher; return new VoucherPostResult(91,92,true); }
        }
        private sealed class AsyncRepository : IAsyncIdempotentVoucherPostRepository
        {
            internal int Calls,SyncCalls;internal Guid Identity;internal OperationActor Actor;internal PreparedVoucher Voucher;internal System.Threading.CancellationToken Token;
            internal Func<System.Threading.Tasks.Task<VoucherPostResult>> Save=()=>System.Threading.Tasks.Task.FromResult(new VoucherPostResult(91,92,true));
            public VoucherPostResult Post(DateTime date,PreparedVoucher voucher,OperationActor actor){SyncCalls++;throw new Exception("Async request reached legacy writer.");}
            public VoucherPostResult Post(Guid identity,DateTime date,PreparedVoucher voucher,OperationActor actor){SyncCalls++;throw new Exception("Async request reached synchronous durable writer.");}
            public System.Threading.Tasks.Task<VoucherPostResult> PostAsync(Guid identity,DateTime date,PreparedVoucher voucher,OperationActor actor,System.Threading.CancellationToken token)
            {Calls++;Identity=identity;Actor=actor;Voucher=voucher;Token=token;return Save();}
        }
        private static void VerifyNativeAsyncContract()
        {
            var actor=new OperationActor {UserName=" tester ",UserRole="Manager"};var id=Guid.NewGuid();
            var request=new VoucherPostRequest("ReceiptExternal",new DateTime(2026,10,1),"المورد","1100",10.005m,"raw A&B","1000",null," yer ",id);
            using(var cancellation=new System.Threading.CancellationTokenSource())
            {
                var repository=new AsyncRepository();var service=new VoucherPostService(repository,_=>true);
                var pending=new System.Threading.Tasks.TaskCompletionSource<VoucherPostResult>();repository.Save=()=>pending.Task;
                var writing=service.PostAsync(request,actor,cancellation.Token);
                Check(!writing.IsCompleted&&repository.Calls==1&&repository.SyncCalls==0);
                Check(repository.Identity==id&&repository.Token==cancellation.Token);
                Check(repository.Voucher.Amount==10m&&repository.Voucher.ExpectedSystemCurrencyCode=="YER"&&repository.Voucher.PartyName=="المورد");
                actor.UserName="changed";Check(repository.Actor.UserName=="tester");actor.UserName=" tester ";
                pending.SetResult(new VoucherPostResult(91,92,true));Check(writing.GetAwaiter().GetResult().AlreadyPosted);
                cancellation.Cancel();int calls=repository.Calls;
                Throws<OperationCanceledException>(()=>service.PostAsync(request,actor,cancellation.Token).GetAwaiter().GetResult());Check(repository.Calls==calls);
            }
            var legacy=new Repository();Throws<InvalidOperationException>(()=>new VoucherPostService(legacy,_=>true).PostAsync(request,actor,System.Threading.CancellationToken.None).GetAwaiter().GetResult());Check(legacy.Calls==0);
            var durable=new DurableRepository();Throws<InvalidOperationException>(()=>new VoucherPostService(durable,_=>true).PostAsync(request,actor,System.Threading.CancellationToken.None).GetAwaiter().GetResult());Check(durable.DurableCalls==0&&durable.LegacyCalls==0);
            var asynchronous=new AsyncRepository();var native=new VoucherPostService(asynchronous,_=>true);
            Throws<InvalidOperationException>(()=>native.PostAsync(Request(),actor,System.Threading.CancellationToken.None).GetAwaiter().GetResult());Check(asynchronous.Calls==0);
            int authorizations=0;Throws<UnauthorizedAccessException>(()=>new VoucherPostService(asynchronous,_=>++authorizations==1).PostAsync(request,actor,System.Threading.CancellationToken.None).GetAwaiter().GetResult());Check(asynchronous.Calls==0);
            asynchronous.Save=()=>System.Threading.Tasks.Task.FromResult<VoucherPostResult>(null);Throws<InvalidOperationException>(()=>native.PostAsync(request,actor,System.Threading.CancellationToken.None).GetAwaiter().GetResult());
            asynchronous.Save=()=>System.Threading.Tasks.Task.FromResult(new VoucherPostResult(0,92));Throws<InvalidOperationException>(()=>native.PostAsync(request,actor,System.Threading.CancellationToken.None).GetAwaiter().GetResult());
            using(var lateCancellation=new System.Threading.CancellationTokenSource())
            {
                bool allowed=true;asynchronous.Save=()=>{allowed=false;lateCancellation.Cancel();return System.Threading.Tasks.Task.FromResult(new VoucherPostResult(93,94));};
                var accepted=new VoucherPostService(asynchronous,_=>allowed).PostAsync(request,actor,lateCancellation.Token).GetAwaiter().GetResult();
                Check(accepted.VoucherId==93&&accepted.JournalEntryId==94&&lateCancellation.IsCancellationRequested);
            }
            Check(asynchronous.SyncCalls==0);
        }
        private static void VerifyDurableRequestContract()
        {
            var actor=new OperationActor {UserName="tester",UserRole="Manager"};
            Guid id=Guid.NewGuid(); var request=new VoucherPostRequest("ReceiptExternal",new DateTime(2026,10,1),"طرف","1100",10.005m,"ملاحظة","1000",7," yer ",id);
            Check(request.RequestId==id);
            var oldRepository=new Repository();var oldService=new VoucherPostService(oldRepository,_=>true);
            Throws<InvalidOperationException>(()=>oldService.Post(request,actor));Check(oldRepository.Calls==0);
            var repository=new DurableRepository();var service=new VoucherPostService(repository,_=>true);
            var result=service.Post(request,actor);
            Check(repository.Identity==id&&repository.DurableCalls==1&&repository.LegacyCalls==0&&result.AlreadyPosted);
            Check(repository.Voucher.Amount==10m&&repository.Voucher.ExpectedSystemCurrencyCode=="YER"&&repository.Voucher.CostCenterId==7);
            service.Post(request,actor);Check(repository.Identity==id&&repository.DurableCalls==2&&repository.LegacyCalls==0);
            Throws<InvalidOperationException>(()=>service.Post(new VoucherPostRequest("ReceiptExternal",request.Date,"طرف","1100",10,"","1000",null,"YER",Guid.Empty),actor));
            Throws<InvalidOperationException>(()=>service.Post(new VoucherPostRequest("ReceiptExternal",request.Date,"طرف","1100",10,"","1000",null,null,id),actor));
            Throws<UnauthorizedAccessException>(()=>new VoucherPostService(repository,_=>false).Post(request,actor));
            Check(repository.DurableCalls==2&&repository.LegacyCalls==0);
            Check(new VoucherPostResult(1,2).AlreadyPosted==false);
            Check(Request().RequestId==null);
            Check(typeof(VoucherPostRequest).GetConstructor(new[]{typeof(string),typeof(DateTime),typeof(string),typeof(string),typeof(decimal),typeof(string),typeof(string),typeof(int?),typeof(string)})!=null);
            Check(typeof(VoucherPostResult).GetConstructor(new[]{typeof(int),typeof(int)})!=null);
            Check(typeof(SqlVoucherPostRepository).GetMethod("PostInTransaction",new[]{typeof(System.Data.SqlClient.SqlConnection),typeof(System.Data.SqlClient.SqlTransaction),typeof(DateTime),typeof(PreparedVoucher),typeof(OperationActor)})!=null);
            var conflict=new VoucherAcceptedRequestConflictException(id,91,92);
            Check(conflict.RequestId==id&&conflict.VoucherId==91&&conflict.JournalEntryId==92);
        }
        internal static int Run()
        {
            checks=0; VerifyDurableRequestContract(); VerifyNativeAsyncContract();
            foreach(var type in new[] {"ReceiptExternal","ReceiptInternal","ReceiptMulti","PaymentExternal","PaymentInternal","PaymentMulti"})
            {
                var value=VoucherPreparation.Prepare(type,"عميل Cash 500 mg"," 1100 "," 1010 ",1250.505m,"بيان",7);
                Check(value.Type==type&&value.AccountCode=="1100"&&value.CashAccountCode=="1010"&&value.Amount==1250.50m);
                bool receipt=type.StartsWith("Receipt");
                Check(value.Lines.Count==2&&value.Lines[0].AccountCode==(receipt?"1010":"1100")&&value.Lines[0].Debit==1250.50m&&value.Lines[0].Credit==0);
                Check(value.Lines[1].AccountCode==(receipt?"1100":"1010")&&value.Lines[1].Debit==0&&value.Lines[1].Credit==1250.50m&&value.Lines[1].CostCenterId==7&&value.Lines[1].Description=="بيان");
                string expected=(receipt?"سند قبض":"سند صرف")+(type.EndsWith("Multi")?" متعدد":type.EndsWith("Internal")?" داخلي":" خارجي")+" - عميل Cash 500 mg";
                Check(value.Description==expected);
            }
            foreach(var sample in new[] {new[] {10.005m,10m},new[] {10.015m,10.02m},new[] {1250.50m,1250.50m},new[] {0.015m,0.02m}})
                Check(VoucherPreparation.Prepare(null,null,"1100",null,sample[0],null,null).Amount==sample[1]);
            Throws<InvalidOperationException>(()=>VoucherPreparation.Prepare("ReceiptExternalGarbage",null,"1100",null,10,null,null));
            Throws<InvalidOperationException>(()=>VoucherPreparation.Prepare("ReceiptPayment",null,"1100",null,10,null,null));
            var defaultValue=VoucherPreparation.Prepare(" ",null,"1100"," ",10,null,null);
            Check(defaultValue.Type=="ReceiptExternal"&&defaultValue.CashAccountCode=="1000"&&defaultValue.PartyName==null&&defaultValue.Notes==null);
            Check(VoucherPreparation.Prepare(" paymentinternal ",null,"A","b",10,null,null).Type=="paymentinternal");
            var repository=new Repository();var actor=new OperationActor {UserName=" tester ",UserRole=" Manager "};
            var service=new VoucherPostService(repository,_=>true);var request=Request();var result=service.Post(request,actor);
            Check(result.VoucherId==41&&result.JournalEntryId==42&&repository.Date==request.Date&&repository.Voucher.Amount==10m);
            Check(repository.Voucher.PartyName==request.PartyName&&repository.Voucher.Notes==request.Notes&&repository.Actor.UserName=="tester"&&repository.Actor.UserRole=="Manager");
            actor.UserName="changed";Check(repository.Actor.UserName=="tester");actor.UserName=" tester ";
            Check(repository.Voucher.ExpectedSystemCurrencyCode==null);
            var currencyRequest=new VoucherPostRequest("ReceiptExternal",request.Date,"طرف","1100",10.005m,"بيان","1000",7," yer ");
            service.Post(currencyRequest,actor);
            Check(currencyRequest.ExpectedSystemCurrencyCode==" yer "&&repository.Voucher.ExpectedSystemCurrencyCode=="YER"&&repository.Voucher.Amount==10m);
            int currencyCalls=repository.Calls;
            foreach(string invalid in new[]{""," ","Y3R","ر.ي",new string('A',11)})
                Throws<InvalidOperationException>(()=>service.Post(new VoucherPostRequest("ReceiptExternal",request.Date,"طرف","1100",10,"","1000",null,invalid),actor));
            Check(repository.Calls==currencyCalls);
            int calls=repository.Calls;
            Throws<UnauthorizedAccessException>(()=>service.Post(request,null));
            Throws<UnauthorizedAccessException>(()=>service.Post(request,new OperationActor {UserName=" ",UserRole="Manager"}));
            Throws<UnauthorizedAccessException>(()=>service.Post(request,new OperationActor {UserName="tester",UserRole=" "}));
            Throws<UnauthorizedAccessException>(()=>new VoucherPostService(repository,_=>false).Post(request,actor));
            Throws<ArgumentNullException>(()=>service.Post(null,actor));
            foreach(var amount in new[] {0m,-1m,0.005m,10000000000000000m,decimal.MaxValue})
                Throws<InvalidOperationException>(()=>service.Post(Request(amount),actor));
            foreach(var date in new[] {new DateTime(1752,12,31),DateTime.MaxValue})
                Throws<InvalidOperationException>(()=>service.Post(Request(date:date),actor));
            foreach(var account in new[] {" ","1000",new string('a',21)})
                Throws<InvalidOperationException>(()=>service.Post(Request(account:account),actor));
            Throws<InvalidOperationException>(()=>service.Post(Request(account:" ABC ",cash:"abc"),actor));
            Throws<InvalidOperationException>(()=>service.Post(Request(cash:new string('a',21)),actor));
            Throws<InvalidOperationException>(()=>service.Post(Request(notes:new string('a',501)),actor));
            Throws<InvalidOperationException>(()=>service.Post(Request(party:new string('a',201)),actor));
            Throws<InvalidOperationException>(()=>service.Post(Request(center:0),actor));
            Throws<InvalidOperationException>(()=>service.Post(Request(center:-1),actor));
            Throws<InvalidOperationException>(()=>service.Post(Request(type:"Unknown"),actor));
            Throws<InvalidOperationException>(()=>service.Post(request,new OperationActor {UserName=new string('a',201),UserRole="Manager"}));
            Throws<InvalidOperationException>(()=>service.Post(request,new OperationActor {UserName="tester",UserRole=new string('a',51)}));
            Check(repository.Calls==calls);
            int auth=0;
            Throws<UnauthorizedAccessException>(()=>new VoucherPostService(repository,_=>++auth==1).Post(request,actor));Check(repository.Calls==calls);
            repository.Save=()=>null;Throws<InvalidOperationException>(()=>service.Post(request,actor));
            repository.Save=()=>new VoucherPostResult(0,42);Throws<InvalidOperationException>(()=>service.Post(request,actor));
            repository.Save=()=>new VoucherPostResult(41,0);Throws<InvalidOperationException>(()=>service.Post(request,actor));
            repository.Save=()=>{throw new InvalidOperationException("rollback");};Throws<InvalidOperationException>(()=>service.Post(request,actor));
            bool allowed=true;repository.Save=()=>{allowed=false;return new VoucherPostResult(43,44);};
            Check(new VoucherPostService(repository,_=>allowed).Post(request,actor).VoucherId==43);
            repository.Save=()=>new VoucherPostResult(45,46);
            Check(service.Post(Request(9999999999999999.99m,date:new DateTime(1753,1,1),party:null,notes:null,center:null),actor).VoucherId==45);
            Throws<ArgumentNullException>(()=>new VoucherPostService(null,_=>true));
            Throws<ArgumentNullException>(()=>new VoucherPostService(repository,null));
            Throws<ArgumentNullException>(()=>new SqlVoucherPostRepository(null));
            Console.WriteLine("PASS: "+checks+" voucher posting direction, precision, validation, permission and acceptance contracts; no database connection.");
            return checks;
        }
    }
}

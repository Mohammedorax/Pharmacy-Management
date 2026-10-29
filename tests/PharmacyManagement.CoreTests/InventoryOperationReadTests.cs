using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Application;

namespace PharmacyManagement.CoreTests
{
    internal static class InventoryOperationReadTests
    {
        private static int checks;
        private static void Check(bool value){if(!value)throw new Exception("Inventory operation read contract failed.");checks++;}
        private static void Throws<T>(Action action)where T:Exception{try{action();}catch(T){checks++;return;}throw new Exception("Expected "+typeof(T).Name);}
        private sealed class Repository:IInventoryOperationReadRepository
        {
            internal int Calls;internal CancellationToken Token;
            internal readonly List<InventoryMedicineLookupRow> Rows=new List<InventoryMedicineLookupRow>{new InventoryMedicineLookupRow("دواء 500 mg","دواء 500 mg - شركة",-2)};
            internal Func<InventoryOperationReadRequest,Task<InventoryOperationReadSnapshot>> Read=r=>Task.FromResult(new InventoryOperationReadSnapshot(r.MedicineName,-2,new List<InventoryMovementReadRow>()));
            public Task<IReadOnlyList<InventoryMedicineLookupRow>> LoadMedicinesAsync(CancellationToken token){Calls++;Token=token;return Task.FromResult((IReadOnlyList<InventoryMedicineLookupRow>)Rows);}
            public Task<InventoryOperationReadSnapshot> LoadSnapshotAsync(InventoryOperationReadRequest request,CancellationToken token){Calls++;Token=token;return Read(request);}
        }
        internal static int Run()
        {
            checks=0;var repo=new Repository();var actor=new OperationActor {UserName="original-user",UserRole="original-role"};
            var request=new InventoryOperationReadRequest(" دواء 500 mg ",new DateTime(2026,1,1,9,0,0),new DateTime(2026,1,2,22,0,0));
            Check(request.MedicineName=="دواء 500 mg"&&request.From==new DateTime(2026,1,1)&&request.To==new DateTime(2026,1,2));
            foreach(string invalid in new[]{null," ",new string('x',201)})Throws<InvalidOperationException>(()=>new InventoryOperationReadRequest(invalid,request.From,request.To));
            Throws<InvalidOperationException>(()=>new InventoryOperationReadRequest("drug",request.To,request.From));
            Throws<InvalidOperationException>(()=>new InventoryOperationReadRequest("drug",new DateTime(1752,12,31),request.To));
            Throws<InvalidOperationException>(()=>new InventoryOperationReadRequest("drug",request.From,new DateTime(9999,12,31)));
            var denied=new InventoryOperationReadService(repo,_=>false);
            Throws<UnauthorizedAccessException>(()=>denied.LoadMedicinesAsync(actor,CancellationToken.None).GetAwaiter().GetResult());
            Throws<UnauthorizedAccessException>(()=>denied.LoadSnapshotAsync(request,actor,CancellationToken.None).GetAwaiter().GetResult());Check(repo.Calls==0);
            var service=new InventoryOperationReadService(repo,_=>true);
            Throws<UnauthorizedAccessException>(()=>service.LoadSnapshotAsync(request,new OperationActor{UserName="user"},CancellationToken.None).GetAwaiter().GetResult());
            Throws<ArgumentNullException>(()=>service.LoadSnapshotAsync(null,actor,CancellationToken.None).GetAwaiter().GetResult());Check(repo.Calls==0);
            using(var cancellation=new CancellationTokenSource())
            {
                cancellation.Cancel();Throws<OperationCanceledException>(()=>service.LoadMedicinesAsync(actor,cancellation.Token).GetAwaiter().GetResult());Check(repo.Calls==0);
            }
            var rows=service.LoadMedicinesAsync(actor,CancellationToken.None).GetAwaiter().GetResult();repo.Rows.Clear();Check(rows.Count==1&&rows[0].Quantity==-2);
            var pending=new TaskCompletionSource<InventoryOperationReadSnapshot>();repo.Read=_=>pending.Task;
            bool allowed=true;var names=new List<string>();service=new InventoryOperationReadService(repo,a=>{names.Add(a.UserName);return allowed;});
            var task=service.LoadSnapshotAsync(request,actor,CancellationToken.None);Check(!task.IsCompleted);actor.UserName="mutated-caller";
            pending.SetResult(new InventoryOperationReadSnapshot(request.MedicineName,-2,new List<InventoryMovementReadRow>()));
            Check(task.GetAwaiter().GetResult().CurrentQuantity==-2&&names.Count==2&&names[0]=="original-user"&&names[1]=="original-user");
            pending=new TaskCompletionSource<InventoryOperationReadSnapshot>();task=service.LoadSnapshotAsync(request,actor,CancellationToken.None);allowed=false;
            pending.SetResult(new InventoryOperationReadSnapshot(request.MedicineName,3,new List<InventoryMovementReadRow>()));Throws<UnauthorizedAccessException>(()=>task.GetAwaiter().GetResult());
            allowed=true;
            using(var cancellation=new CancellationTokenSource())
            {
                pending=new TaskCompletionSource<InventoryOperationReadSnapshot>();task=service.LoadSnapshotAsync(request,actor,cancellation.Token);Check(repo.Token==cancellation.Token);cancellation.Cancel();
                pending.SetResult(new InventoryOperationReadSnapshot(request.MedicineName,3,new List<InventoryMovementReadRow>()));Throws<OperationCanceledException>(()=>task.GetAwaiter().GetResult());
            }
            repo.Read=_=>Task.FromResult(new InventoryOperationReadSnapshot("different medicine",3,new List<InventoryMovementReadRow>()));Throws<InvalidOperationException>(()=>service.LoadSnapshotAsync(request,actor,CancellationToken.None).GetAwaiter().GetResult());
            repo.Read=_=>Task.FromResult<InventoryOperationReadSnapshot>(null);Throws<InvalidOperationException>(()=>service.LoadSnapshotAsync(request,actor,CancellationToken.None).GetAwaiter().GetResult());
            Console.WriteLine("PASS: "+checks+" inventory operation read validation/authorization/cancellation/captured actor/result contracts; no SQL.");return checks;
        }
    }
}

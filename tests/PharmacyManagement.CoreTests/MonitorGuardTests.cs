using System;using System.Threading;using System.Threading.Tasks;using PharmacyManagement.Application;
class MonitorGuardTests {
static int assertions; static void Check(bool ok,string name){if(!ok)throw new Exception(name);assertions++;Console.WriteLine("PASS "+name);}
static void Expect<T>(Action call,string name)where T:Exception{bool caught=false;try{call();}catch(T){caught=true;}Check(caught,name);}
static OperationalReportResult Result()=>new OperationalReportResult(new[]{new OperationalReportColumn("Details",typeof(string))},new[]{new object[]{"مراقبة — Pharma 500 mg"}});
sealed class Repository:IAsyncSystemMonitorLogRepository{
public int Calls,Take;public CancellationToken Token;public Action DuringRead;public OperationalReportResult Next=Result();public TaskCompletionSource<OperationalReportResult> Pending;
OperationalReportResult Read(int take){Calls++;Take=take;DuringRead?.Invoke();return Next;}
public OperationalReportResult LoadAudit(int take)=>Read(take);public OperationalReportResult LoadInventoryMovements(int take)=>Read(take);
Task<OperationalReportResult> Async(int take,CancellationToken token){Calls++;Take=take;Token=token;Pending=new TaskCompletionSource<OperationalReportResult>();return Pending.Task;}
public Task<OperationalReportResult> LoadAuditAsync(int take,CancellationToken token)=>Async(take,token);public Task<OperationalReportResult> LoadInventoryMovementsAsync(int take,CancellationToken token)=>Async(take,token);
}
static OperationalReportResult Sync(SystemMonitorLogReadService service,bool audit,int take)=>audit?service.GetRecentAuditLogs(take):service.GetRecentInventoryMovements(take);
static Task<OperationalReportResult> Async(SystemMonitorLogReadService service,bool audit,int take,CancellationToken token)=>audit?service.GetRecentAuditLogsAsync(take,token):service.GetRecentInventoryMovementsAsync(take,token);
internal static int Run(){assertions=0;try{
Expect<ArgumentNullException>(()=>new SystemMonitorLogReadService(null),"null repository");
foreach(bool audit in new[]{true,false}){
var repo=new Repository();var service=new SystemMonitorLogReadService(repo);
Expect<UnauthorizedAccessException>(()=>Sync(service,audit,100),"sync absent policy denies "+audit);
Expect<UnauthorizedAccessException>(()=>Async(service,audit,100,CancellationToken.None).GetAwaiter().GetResult(),"async absent policy denies "+audit);
Check(repo.Calls==0,"absent policy never reaches repository "+audit);
service=new SystemMonitorLogReadService(repo,()=>false);
Expect<UnauthorizedAccessException>(()=>Sync(service,audit,100),"sync explicit denial "+audit);
Expect<UnauthorizedAccessException>(()=>Async(service,audit,100,CancellationToken.None).GetAwaiter().GetResult(),"async explicit denial "+audit);
Check(repo.Calls==0,"explicit denial never reaches repository "+audit);
bool allowed=true;service=new SystemMonitorLogReadService(repo,()=>allowed);
var raw=Sync(service,audit,int.MinValue);Check(repo.Take==1&&ReferenceEquals(raw,repo.Next),"sync minimum and raw result "+audit);
raw=Sync(service,audit,int.MaxValue);Check(repo.Take==(audit?500:1000)&&ReferenceEquals(raw,repo.Next),"sync maximum and raw result "+audit);
repo.DuringRead=()=>allowed=false;
Expect<UnauthorizedAccessException>(()=>Sync(service,audit,100),"sync post-read revocation "+audit);
repo.DuringRead=null;allowed=true;repo.Next=null;
Expect<InvalidOperationException>(()=>Sync(service,audit,100),"sync null result rejected "+audit);
repo.Next=Result();
using(var cancel=new CancellationTokenSource()){
int before=repo.Calls;cancel.Cancel();
Expect<OperationCanceledException>(()=>Async(service,audit,100,cancel.Token).GetAwaiter().GetResult(),"pre-canceled read "+audit);
Check(repo.Calls==before,"pre-cancellation avoids repository "+audit);}
using(var cancel=new CancellationTokenSource()){
var pending=Async(service,audit,int.MinValue,cancel.Token);
Check(!pending.IsCompleted&&repo.Take==1&&repo.Token==cancel.Token,"async wait, minimum and exact token "+audit);
allowed=false;repo.Pending.SetResult(repo.Next);
Expect<UnauthorizedAccessException>(()=>pending.GetAwaiter().GetResult(),"async post-read revocation "+audit);}
allowed=true;
using(var cancel=new CancellationTokenSource()){
var pending=Async(service,audit,int.MaxValue,cancel.Token);Check(repo.Take==(audit?500:1000),"async maximum "+audit);
cancel.Cancel();repo.Pending.SetResult(repo.Next);
Expect<OperationCanceledException>(()=>pending.GetAwaiter().GetResult(),"post-read cancellation "+audit);}
var current=Async(service,audit,100,CancellationToken.None);repo.Pending.SetResult(repo.Next);Check(ReferenceEquals(current.GetAwaiter().GetResult(),repo.Next),"async current raw result "+audit);
current=Async(service,audit,100,CancellationToken.None);repo.Pending.SetResult(null);Expect<InvalidOperationException>(()=>current.GetAwaiter().GetResult(),"async null rejected "+audit);
}
Console.WriteLine("CHECKS="+assertions+"; no SQL or UI.");return assertions;
}catch(Exception ex){Console.Error.WriteLine(ex);throw;}}
}



using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Application;
internal static class DashboardAuthorizationContractTests
{
 static int checks,cases;
 static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
 sealed class Repository:IDashboardReadRepository,IAsyncDashboardReadRepository
 {
  internal int Calls;internal string Cashier;internal Action During;internal TaskCompletionSource<bool> Pending;
  T Read<T>(T value){Calls++;During?.Invoke();return value;}
  async Task<T> ReadAsync<T>(T value,Action demand,CancellationToken token){Calls++;demand();if(Pending!=null)await Pending.Task;During?.Invoke();return value;}
  public int GetCompanyCount()=>Read(17);
  public int GetEmployeeCount()=>Read(23);
  public decimal GetSalesTotal(string cashier){Cashier=cashier;return Read(123.45m);}
  public IReadOnlyList<string> GetCashierNames()=>Read<IReadOnlyList<string>>(new[]{"cashier"});
  public IReadOnlyList<DashboardCustomerSummary> GetCustomerSummaries(string cashier){Cashier=cashier;return Read<IReadOnlyList<DashboardCustomerSummary>>(new[]{new DashboardCustomerSummary{CustomerName="عميل A&B",TotalSpent=123.45m}});}
  public IReadOnlyList<DashboardSalesTrendPoint> GetRecentSalesTrend(string cashier){Cashier=cashier;return Read<IReadOnlyList<DashboardSalesTrendPoint>>(new[]{new DashboardSalesTrendPoint{SaleDay=new DateTime(2026,10,14),DailyTotal=123.45m}});}
  public Task<int> GetCompanyCountAsync(Action demand,CancellationToken token)=>ReadAsync(17,demand,token);
  public Task<int> GetEmployeeCountAsync(Action demand,CancellationToken token)=>ReadAsync(23,demand,token);
  public Task<decimal> GetSalesTotalAsync(string cashier,Action demand,CancellationToken token){Cashier=cashier;return ReadAsync(123.45m,demand,token);}
  public Task<IReadOnlyList<string>> GetCashierNamesAsync(Action demand,CancellationToken token)=>ReadAsync<IReadOnlyList<string>>(new[]{"cashier"},demand,token);
  public Task<IReadOnlyList<DashboardCustomerSummary>> GetCustomerSummariesAsync(string cashier,Action demand,CancellationToken token){Cashier=cashier;return ReadAsync<IReadOnlyList<DashboardCustomerSummary>>(new[]{new DashboardCustomerSummary{CustomerName="عميل A&B",TotalSpent=123.45m}},demand,token);}
  public Task<IReadOnlyList<DashboardSalesTrendPoint>> GetRecentSalesTrendAsync(string cashier,Action demand,CancellationToken token){Cashier=cashier;return ReadAsync<IReadOnlyList<DashboardSalesTrendPoint>>(new[]{new DashboardSalesTrendPoint{SaleDay=new DateTime(2026,10,14),DailyTotal=123.45m}},demand,token);}
 }
 static OperationActor Actor()=>new OperationActor{UserName="owned-manager",UserRole="Manager"};
 static object Sync(DashboardReadService s,int method){switch(method){case 0:return s.GetCompanyCount();case 1:return s.GetEmployeeCount();case 2:return s.GetSalesTotal("  cashier  ");case 3:return s.GetCashierNames();case 4:return s.GetCustomerSummaries("  cashier  ");default:return s.GetRecentSalesTrend("  cashier  ");}}
 static Task Async(DashboardReadService s,int method,CancellationToken token){switch(method){case 0:return s.GetCompanyCountAsync(token);case 1:return s.GetEmployeeCountAsync(token);case 2:return s.GetSalesTotalAsync("  cashier  ",token);case 3:return s.GetCashierNamesAsync(token);case 4:return s.GetCustomerSummariesAsync("  cashier  ",token);default:return s.GetRecentSalesTrendAsync("  cashier  ",token);}}
 static void Denied(Action action,string message){bool denied=false;try{action();}catch(UnauthorizedAccessException){denied=true;}Check(denied,message);}
 internal static int Run(){
  for(int method=0;method<6;method++){
   var repository=new Repository();var denied=new DashboardReadService(repository);Denied(()=>Sync(denied,method),"Default sync denies");Denied(()=>Async(denied,method,CancellationToken.None).GetAwaiter().GetResult(),"Default async denies");Check(repository.Calls==0,"Default denial does not touch repository");cases+=2;
   foreach(var actor in new[]{(OperationActor)null,new OperationActor{UserName=" ",UserRole="Manager"},new OperationActor{UserName="owned-manager",UserRole=" "}}){var invalid=new DashboardReadService(repository,actor,_=>true);Denied(()=>Sync(invalid,method),"Invalid actor sync");Denied(()=>Async(invalid,method,CancellationToken.None).GetAwaiter().GetResult(),"Invalid actor async");cases+=2;}
   Check(repository.Calls==0,"Invalid actor never reads");
   var missingPolicy=new DashboardReadService(repository,Actor(),null);Denied(()=>Sync(missingPolicy,method),"No implicit sync policy");Denied(()=>Async(missingPolicy,method,CancellationToken.None).GetAwaiter().GetResult(),"No implicit async policy");cases+=2;
   bool current=true;repository=new Repository();var allowed=new DashboardReadService(repository,Actor(),_=>current);Check(Sync(allowed,method)!=null,"Authorized sync returns value");Async(allowed,method,CancellationToken.None).GetAwaiter().GetResult();Check(repository.Calls==2,"Authorized sync and async both read");if(method==2||method>=4)Check(repository.Cashier=="cashier","Cashier normalization preserved");cases+=2;
   current=false;Denied(()=>Sync(allowed,method),"Revoked sync denies before read");Denied(()=>Async(allowed,method,CancellationToken.None).GetAwaiter().GetResult(),"Revoked async denies before read");Check(repository.Calls==2,"Revoked context does not touch repository");cases+=2;
   current=true;repository.During=()=>current=false;Denied(()=>Sync(allowed,method),"Revoked sync result denied after repository");current=true;Denied(()=>Async(allowed,method,CancellationToken.None).GetAwaiter().GetResult(),"Revoked async result denied after repository");cases+=2;
   var original=Actor();repository=new Repository();var captured=new DashboardReadService(repository,original,a=>{bool valid=a.UserName=="owned-manager"&&a.UserRole=="Manager";a.UserName="policy-mutated";a.UserRole="policy-mutated";return valid;});original.UserName="forged";original.UserRole="forged";Sync(captured,method);Async(captured,method,CancellationToken.None).GetAwaiter().GetResult();Check(repository.Calls==2,"Actor and policy argument mutation cannot change captured identity");cases+=2;
   repository=new Repository();using(var token=new CancellationTokenSource()){token.Cancel();bool cancelled=false;try{Async(new DashboardReadService(repository,Actor(),_=>true),method,token.Token).GetAwaiter().GetResult();}catch(OperationCanceledException ex){cancelled=ex.CancellationToken==token.Token;}Check(cancelled&&repository.Calls==0,"Pre-cancelled async keeps token and avoids repository");cases++;}
   repository=new Repository{Pending=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously)};using(var token=new CancellationTokenSource()){var task=Async(new DashboardReadService(repository,Actor(),_=>true),method,token.Token);Check(!task.IsCompleted,"Delayed async is actually pending");token.Cancel();repository.Pending.SetResult(true);bool cancelled=false;try{task.GetAwaiter().GetResult();}catch(OperationCanceledException ex){cancelled=ex.CancellationToken==token.Token;}Check(cancelled,"Late async result cannot bypass cancellation");cases++;}
  }
  var normalization=new Repository();var service=new DashboardReadService(normalization,Actor(),_=>true);
  foreach(string invalid in new[]{new string('x',201)," "+new string('x',201)+" "}){bool rejected=false;try{service.GetSalesTotal(invalid);}catch(InvalidOperationException){rejected=true;}Check(rejected,"Long cashier rejected");}Check(normalization.Calls==0,"Long cashier never reaches SQL");cases+=2;
  service.GetSalesTotal(null);Check(normalization.Cashier==string.Empty,"Null cashier compatibility");cases++;
  return checks;
 }
}

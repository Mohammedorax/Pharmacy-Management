using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Application;
namespace PharmacyManagement.CoreTests { internal static class PurchaseLookupReadTests {
 static int checks;
 static void Check(bool x){if(!x)throw new Exception("Check "+checks);checks++;}
 static void Denied(Action action){try{action();throw new Exception("Expected denial");}catch(UnauthorizedAccessException){checks++;}}
 static void Cancelled(Action action){try{action();throw new Exception("Expected cancellation");}catch(OperationCanceledException){checks++;}}
 internal static int Run(){checks=0;
  try{
   var actor=new OperationActor{UserName="raw user",UserRole="Cashier"};
   var repo=new Repo();bool allowed=true;
   var service=new PurchaseLookupReadService(repo,a=>allowed&&a.UserName=="raw user"&&a.UserRole=="Cashier");
   var names=service.GetSuppliersAsync(actor,CancellationToken.None).GetAwaiter().GetResult();
   Check(names.Count==1&&names[0]=="  مورد & 001  ");
   repo.Suppliers.Add("later");Check(names.Count==1);
   var employees=service.GetEmployeesAsync(actor,CancellationToken.None).GetAwaiter().GetResult();
   Check(employees.Count==1&&employees[0].EmployeeId=="0001"&&employees[0].EmployeeName=="موظف & 01");
   repo.Employees[0].EmployeeName="changed";Check(employees[0].EmployeeName=="موظف & 01");
   allowed=false;int calls=repo.Calls;
   Denied(()=>service.GetSuppliersAsync(actor,CancellationToken.None).GetAwaiter().GetResult());
   Denied(()=>service.GetEmployeesAsync(actor,CancellationToken.None).GetAwaiter().GetResult());Check(repo.Calls==calls);
   allowed=true;repo.After=()=>allowed=false;
   Denied(()=>service.GetSuppliersAsync(actor,CancellationToken.None).GetAwaiter().GetResult());
   allowed=true;Denied(()=>service.GetEmployeesAsync(actor,CancellationToken.None).GetAwaiter().GetResult());
   allowed=true;repo.After=()=>actor.UserName="changed";
   Check(service.GetSuppliersAsync(actor,CancellationToken.None).GetAwaiter().GetResult()!=null);
   actor.UserName="raw user";repo.After=null;
   using(var c=new CancellationTokenSource()){
    c.Cancel();calls=repo.Calls;
    Cancelled(()=>service.GetSuppliersAsync(actor,c.Token).GetAwaiter().GetResult());Check(repo.Calls==calls);
   }
   using(var c=new CancellationTokenSource()){
    repo.After=()=>{c.Cancel();throw new InvalidOperationException("late backend failure");};
    Cancelled(()=>service.GetSuppliersAsync(actor,c.Token).GetAwaiter().GetResult());
   }
   using(var c=new CancellationTokenSource()){
    repo.After=()=>{c.Cancel();throw new InvalidOperationException("late backend failure");};
    Cancelled(()=>service.GetEmployeesAsync(actor,c.Token).GetAwaiter().GetResult());
   }
   repo.After=null;repo.NullRows=true;
   bool rejected=false;try{service.GetEmployeesAsync(actor,CancellationToken.None).GetAwaiter().GetResult();}catch(InvalidOperationException){rejected=true;}Check(rejected);
   Console.WriteLine("PASS purchase lookup contracts: "+checks+" checks; raw values, detached rows, pre/post authorization, captured actor and cancellation; no SQL.");
   return checks;
  }catch(Exception e){throw new Exception("Purchase lookup contracts failed",e);}
 }
 class Repo:IPurchaseLookupReadRepository {
  public int Calls;public Action After;public bool NullRows;
  public List<string> Suppliers=new List<string>{"  مورد & 001  "};
  public List<PurchaseEmployeeChoice> Employees=new List<PurchaseEmployeeChoice>{new PurchaseEmployeeChoice{EmployeeId="0001",EmployeeName="موظف & 01"}};
  public Task<IReadOnlyList<string>> GetSuppliersAsync(CancellationToken t){Calls++;try{After?.Invoke();return Task.FromResult<IReadOnlyList<string>>(NullRows?null:Suppliers);}catch(Exception e){return Task.FromException<IReadOnlyList<string>>(e);}}
  public Task<IReadOnlyList<PurchaseEmployeeChoice>> GetEmployeesAsync(CancellationToken t){Calls++;try{After?.Invoke();return Task.FromResult<IReadOnlyList<PurchaseEmployeeChoice>>(NullRows?null:Employees);}catch(Exception e){return Task.FromException<IReadOnlyList<PurchaseEmployeeChoice>>(e);}}
 }
}

}

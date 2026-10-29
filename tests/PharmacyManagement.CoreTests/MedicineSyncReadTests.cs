using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Application;
namespace PharmacyManagement.CoreTests {
internal static class MedicineSyncReadTests
{
 static int checks;
 static void Check(bool v,string m){if(!v)throw new Exception(m);checks++;}
 static void Throws<T>(Action action)where T:Exception{try{action();}catch(T){checks++;return;}throw new Exception("Expected "+typeof(T).Name);}
 sealed class Repo:IMedicineRepository
 {
  public int Calls;public Action After;public bool Available=true;
  public IReadOnlyList<MedicineRecord> Rows=new List<MedicineRecord>{new MedicineRecord{MedName=" دواء عربي A&B ",Barcode="0000123",BuyingPrice=5,SellingPrice=7,Quantity=9,ProductionDate=new DateTime(2025,1,2),ExpiryDate=new DateTime(2027,3,4),CompanyName=" مصنع عربي "}};
  public IReadOnlyList<string> Companies=new List<string>{" مصنع عربي & 001 "};
  public IReadOnlyList<string> Names=new List<string>{" دواء عربي A&B "," دواء آخر "};
  public IReadOnlyList<MedicineRecord> GetAll(){Calls++;After?.Invoke();return Rows;}
  public IReadOnlyList<string> GetCompanyNames(){Calls++;After?.Invoke();return Companies;}
  public bool IsBarcodeAvailable(string b,string excluded){Calls++;After?.Invoke();return Available;}
  public Task<IReadOnlyList<MedicineRecord>> GetAllAsync(CancellationToken t){throw new NotSupportedException();}
  public Task<IReadOnlyList<string>> GetNamesAsync(CancellationToken t){Calls++;After?.Invoke();return Task.FromResult(Names);}
  public Task<MedicineRecord> FindByNameAsync(string n,CancellationToken t){throw new NotSupportedException();}
  public Task<IReadOnlyList<string>> GetCompanyNamesAsync(CancellationToken t){throw new NotSupportedException();}
  public Task<bool> IsBarcodeAvailableAsync(string b,string n,CancellationToken t){throw new NotSupportedException();}
  public void Create(MedicineRecord m,OperationActor a){Calls++;}
  public Task CreateAsync(MedicineRecord m,OperationActor a){throw new NotSupportedException();}
  public bool Update(MedicineRecord m,OperationActor a){throw new NotSupportedException();}
  public Task<bool> UpdateAsync(MedicineRecord m,OperationActor a){throw new NotSupportedException();}
  public bool Delete(string n,OperationActor a){throw new NotSupportedException();}
  public Task<bool> DeleteAsync(string n,OperationActor a){throw new NotSupportedException();}
 }
 static OperationActor Actor()=>new OperationActor{UserName="owner",UserRole="Manager"};
 internal static int Run()
 {
  checks=0;
  try
  {
   bool allowed=true;var repo=new Repo();var svc=new MedicineService(repo,a=>allowed,()=> "0000001234567",a=>allowed);
   repo.After=()=>allowed=false;
   Throws<UnauthorizedAccessException>(()=>svc.GetAll(Actor()));
   allowed=true;Throws<UnauthorizedAccessException>(()=>svc.GetCompanyNames(Actor()));
   allowed=true;Throws<UnauthorizedAccessException>(()=>svc.GenerateAvailableBarcode(null,Actor()));
   allowed=true;Throws<UnauthorizedAccessException>(()=>svc.GetNamesAsync(Actor(),CancellationToken.None).GetAwaiter().GetResult());
   Check(repo.Calls==4,"no extra probes after revocation");
   repo.After=null;allowed=true;var rows=svc.GetAll(Actor());var companies=svc.GetCompanyNames(Actor());var names=svc.GetNamesAsync(Actor(),CancellationToken.None).GetAwaiter().GetResult();
   Check(rows[0].MedName==" دواء عربي A&B "&&rows[0].Barcode=="0000123"&&rows[0].CompanyName==" مصنع عربي ","raw fields");
   Check(rows[0].BuyingPrice==5&&rows[0].SellingPrice==7&&rows[0].Quantity==9&&rows[0].ExpiryDate==new DateTime(2027,3,4),"raw values");
   ((List<MedicineRecord>)repo.Rows)[0].MedName="changed";((List<MedicineRecord>)repo.Rows).Clear();
   Check(rows.Count==1&&rows[0].MedName==" دواء عربي A&B ","detached record snapshot");
   ((List<string>)repo.Companies).Clear();Check(companies.Count==1&&companies[0]==" مصنع عربي & 001 ","detached companies");
   ((List<string>)repo.Names).Clear();Check(names.Count==2&&names[0]==" دواء عربي A&B "&&names[1]==" دواء آخر ","detached medicine names");
   repo.Rows=null;Throws<InvalidOperationException>(()=>svc.GetAll(Actor()));
   repo.Rows=new List<MedicineRecord>{null};Throws<InvalidOperationException>(()=>svc.GetAll(Actor()));
   repo.Companies=null;Throws<InvalidOperationException>(()=>svc.GetCompanyNames(Actor()));
   repo.Names=null;Throws<InvalidOperationException>(()=>svc.GetNamesAsync(Actor(),CancellationToken.None).GetAwaiter().GetResult());
   var mutable=Actor();var exact=new MedicineService(repo,a=>a.UserName=="owner"&&a.UserRole=="Manager",()=> "0000001234567",a=>a.UserName=="owner"&&a.UserRole=="Manager");
   repo.Rows=new List<MedicineRecord>();repo.After=()=>mutable.UserName="forged";
   Check(exact.GetAll(mutable).Count==0,"captured actor survives caller mutation");
   mutable=Actor();repo.Companies=new List<string>();Check(exact.GetCompanyNames(mutable).Count==0,"company actor captured");
   mutable=Actor();Check(exact.GenerateAvailableBarcode(null,mutable)=="0000001234567","barcode actor captured");
   int before=repo.Calls;allowed=false;Throws<UnauthorizedAccessException>(()=>svc.GetAll(Actor()));Check(repo.Calls==before,"pre deny no repo");
   Throws<UnauthorizedAccessException>(()=>svc.GetNamesAsync(Actor(),CancellationToken.None));Check(repo.Calls==before,"name pre deny no repo");
   Throws<UnauthorizedAccessException>(()=>svc.GetCompanyNames(null));Check(repo.Calls==before,"null actor no repo");
   allowed=true;repo.Available=false;repo.After=()=>allowed=false;Throws<UnauthorizedAccessException>(()=>svc.GenerateAvailableBarcode(null,Actor()));Check(repo.Calls==before+1,"unavailable result revoke stops next attempt");
   allowed=true;repo.Available=true;repo.After=null;Check(svc.GenerateAvailableBarcode("old",Actor())=="0000001234567","authorized barcode");
   allowed=true;repo.After=()=>allowed=false;before=repo.Calls;
   Throws<UnauthorizedAccessException>(()=>svc.Create(new MedicineRecord{MedName="new",CompanyName="factory",BuyingPrice=1,SellingPrice=2,Quantity=1,ProductionDate=DateTime.Today,ExpiryDate=DateTime.Today.AddYears(1)},Actor()));
   Check(repo.Calls==before+1,"auto barcode revoke prevents Create");
   allowed=true;repo.After=null;before=repo.Calls;
   var factoryRevokes=new MedicineService(repo,a=>allowed,()=>{allowed=false;return "0000001234567";},a=>allowed);
   Throws<UnauthorizedAccessException>(()=>factoryRevokes.GenerateAvailableBarcode(null,Actor()));Check(repo.Calls==before,"candidate revocation no SQL probe");
   allowed=true;int candidates=0;repo.Available=false;
   var exhausted=new MedicineService(repo,a=>allowed,()=>{candidates++;return "0000001234567";},a=>allowed);
   Throws<InvalidOperationException>(()=>exhausted.GenerateAvailableBarcode("old",Actor()));
   Check(candidates==20&&repo.Calls==before+20,"twenty attempt limit preserved");
   Console.WriteLine("PASS medicine synchronous reads: checks="+checks+"; pre/post authority, captured actor, detached raw snapshots/null rejection, barcode revocation between probes and no create after revoked auto barcode; no SQL or Desktop.");return checks;
  }
  catch(Exception){throw;}
 }
}


}


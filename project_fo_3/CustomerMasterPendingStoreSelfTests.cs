using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using PharmacyManagement.Application;
using project_fo_3;
namespace project_fo_3 {
internal static class CustomerMasterPendingStoreSelfTests
{
    static int checks;
    static void Check(bool v,string m){if(!v)throw new Exception(m);checks++;}
    static void Throws<T>(Action a) where T:Exception
    {try{a();}catch(T){checks++;return;}throw new Exception("Expected "+typeof(T).Name);}
    static CustomerMasterWriteAttempt Attempt(string source,string user="owner",string role="Manager",string name="عربي & Latin 001",string target="")
    {return new CustomerMasterWriteAttempt(new CustomerMasterWriteRequest(Guid.NewGuid(),new SalesCustomer{CustomerId=target,CustomerName=name,PhoneNumber="001234",Address="عنوان\nثان\r\nثالث"},new OperationActor{UserName=user,UserRole=role}),source);}
    internal static int Run(string path)
    {
        string directory=Path.GetFullPath(path),token=Guid.NewGuid().ToString("N");
        if(Directory.Exists(directory)){Console.Error.WriteLine("Owned store test directory must be new.");return 1;}
        Directory.CreateDirectory(directory);File.WriteAllText(Path.Combine(directory,"owner"),token);
        try
        {
            string source=new string('A',64);var a=Attempt(source);
            using(var store=new CustomerMasterPendingStore(directory,source,"owner"))
            {
                Check(store.Load()==null,"empty");store.Save(a);store.Save(a);
                var loaded=store.Load();Check(loaded.Request.RequestId==a.Request.RequestId,"id");
                Check(loaded.Phase==CustomerMasterWritePhase.Unknown&&loaded.HasUnknownHistory&&!loaded.CanDiscard,"restored unknown");
                Check(loaded.Request.CustomerName==a.Request.CustomerName&&loaded.Request.PhoneNumber=="001234"&&loaded.Request.Address==a.Request.Address,"raw fields");
                Throws<IOException>(()=>{using(var competing=new CustomerMasterPendingStore(directory,source,"owner")){}});
                Throws<InvalidOperationException>(()=>store.Save(Attempt(source)));Throws<InvalidOperationException>(()=>store.Save(Attempt(new string('B',64))));
                Throws<InvalidOperationException>(()=>store.Save(Attempt(source,"other")));Throws<InvalidOperationException>(()=>store.RemoveResolved(loaded));
                loaded.BeginResolve(source,new OperationActor{UserName="owner",UserRole="Administrator"});loaded.ObserveStatus(new CustomerMasterWriteStatus(null));
                Throws<InvalidOperationException>(()=>store.RemoveResolved(loaded));
                var bytes=File.ReadAllBytes(Directory.GetFiles(directory,"*.pending").Single());
                Check(!System.Text.Encoding.UTF8.GetString(bytes).Contains("Latin 001"),"encrypted");
            }
            using(var other=new CustomerMasterPendingStore(directory,source,"other")){Check(other.Load()==null,"other owner isolated");}
            using(var other=new CustomerMasterPendingStore(directory,new string('B',64),"owner")){Check(other.Load()==null,"other source isolated");}
            using(var reopened=new CustomerMasterPendingStore(directory,source,"OWNER"))
            {
                var loaded=reopened.Load();Check(loaded.Request.RequestId==a.Request.RequestId,"restart owner case");
                Check(loaded.Request.OwnerRole=="Manager","old role preserved");
                loaded.BeginCancel(source,new OperationActor{UserName="owner",UserRole="Administrator"});
                loaded.ObserveReceipt(new CustomerMasterWriteReceipt(a.Request.RequestId,"",1,DateTime.UtcNow,true,false,CustomerMasterWriteOutcome.Canceled));
                reopened.RemoveResolved(loaded);Check(reopened.Load()==null,"sealed close clear");
                var update=Attempt(source,target:"001");reopened.Save(update);
                var retry=reopened.Load();Check(retry.Request.TargetCustomerId=="001"&&retry.Request.Kind==CustomerMasterWriteKind.Update,"update");
                var file=Directory.GetFiles(directory,"*.pending").Single();byte[] original=File.ReadAllBytes(file),bad=(byte[])original.Clone();bad[bad.Length/2]^=0x80;File.WriteAllBytes(file,bad);
                Throws<CryptographicException>(()=>reopened.Load());Throws<CryptographicException>(()=>reopened.Save(Attempt(source)));
                Check(File.ReadAllBytes(file).SequenceEqual(bad),"corrupt not overwritten");File.WriteAllBytes(file,original);
                retry.BeginResolve(source,new OperationActor{UserName="owner",UserRole="Manager"});
                retry.ObserveStatus(new CustomerMasterWriteStatus(new CustomerMasterWriteReceipt(retry.Request.RequestId,"001",2,DateTime.UtcNow,true,false)));
                reopened.RemoveResolved(retry);Check(reopened.Load()==null,"known historical commit clear");
                var rejected=Attempt(source);reopened.Save(rejected);
                rejected.BeginSubmit(source,new OperationActor{UserName="owner",UserRole="Manager"},rejected.Request.CopyCustomer());rejected.ObserveRejected();
                reopened.RemoveResolved(rejected);Check(reopened.Load()==null,"fresh rejected clear");
            }
            var disposed=new CustomerMasterPendingStore(directory,source,"owner");disposed.Dispose();disposed.Dispose();
            Throws<ObjectDisposedException>(()=>disposed.Load());Throws<ObjectDisposedException>(()=>disposed.Save(a));
            Check(Directory.GetFiles(directory,"*.tmp").Length==0,"atomic temp cleanup");
            Console.WriteLine("PASS customer pending durable store: checks="+checks+"; DPAPI, restart, owner/source separation, exclusive lease, unknown/conflict discard guards, corrupt fail closed, rejected/committed/sealed cleanup; owned directory only.");return 0;
        }
        catch(Exception e){Console.Error.WriteLine(e);return 1;}
        finally
        {
            if(File.ReadAllText(Path.Combine(directory,"owner"))==token)
            {foreach(var file in Directory.GetFiles(directory)){File.Delete(file);}Directory.Delete(directory,false);}
        }
    }
}

}

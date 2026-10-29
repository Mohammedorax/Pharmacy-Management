using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Diagnostics;
using PharmacyManagement.Application;
using project_fo_3;
internal static class InventoryPendingStoreProbe
{
    private static void Check(bool value,string message){if(!value)throw new Exception(message);}
    private static void Reject(Action action,string message){bool rejected=false;try{action();}catch(Exception error){if(error is IOException||error is InvalidDataException||error is InvalidOperationException||error is XmlException)rejected=true;else throw;}Check(rejected,message);}
    private static void RunChild(string option,string directory)
    {
        using(var child=Process.Start(new ProcessStartInfo(Process.GetCurrentProcess().MainModule.FileName,option+" \""+directory+"\"") {UseShellExecute=false,CreateNoWindow=true}))
        {
            if(!child.WaitForExit(10000)){child.Kill();throw new Exception("Independent process test timed out.");}
            Check(child.ExitCode==0,"Independent process lock/restoration failed.");
        }
    }
    private static int Main(string[] args)
    {
        try
        {
            if(args[0]=="--journal-lock-child")
            {
                try{using(var store=new JournalPendingStore(args[1],new string('c',64),"Manual")){}return 2;}
                catch(IOException){return 0;}
            }
            if(args[0]=="--journal-restore-child")
            {
                using(var store=new JournalPendingStore(args[1],new string('c',64),"Manual"))
                {
                    var restored=store.Load();
                    Check(restored!=null&&restored.Request.SourceId=="123456781234123412341234567890ab"&&
                        restored.Request.Description=="صيدلية المهندس"&&restored.Request.Lines[0].Debit==1250.50m&&
                        restored.Request.Lines[0].Description=="باراسيتامول 500 mg\r\nA&B"&&restored.Request.Lines[1].CostCenterId==7,
                        "Independent process changed decrypted journal request.");
                }
                return 0;
            }
            if(args[0]=="--lock-child")
            {
                try{using(var store=new InventoryPendingStore(args[1],new string('a',64),InventoryPostKind.Receipt)){}return 2;}
                catch(IOException){return 0;}
            }
            if(args[0]=="--restore-child")
            {
                using(var store=new InventoryPendingStore(args[1],new string('a',64),InventoryPostKind.Receipt))
                {
                    var loaded=store.Load();
                    Check(loaded.SourceId=="restore-source"&&loaded.Quantity==3&&loaded.UnitAmount==12.345m&&loaded.MedicineName=="باراسيتامول 500 mg"&&
                        loaded.Notes=="مراجعة & سبب <الحركة>\r\nسطر ثانٍ\nMixed 500 mg","Independent process changed restored business values.");
                }
                return 0;
            }
            string directory=args[0];Directory.CreateDirectory(directory);string key=new string('a',64), other=new string('b',64);
            var request=new InventoryPostRequest(InventoryPostKind.Receipt,"restore-source","باراسيتامول 500 mg",3,12.345m,"مراجعة & سبب <الحركة>\r\nسطر ثانٍ\nMixed 500 mg");
            using(var store=new InventoryPendingStore(directory,key,request.Kind))
            {
                Check(store.Load()==null,"Unexpected preexisting pending record.");store.Save(request);store.Save(request);
                Reject(()=>{using(var duplicate=new InventoryPendingStore(directory,key,request.Kind)){}},"Two windows acquired the same pending slot.");
                RunChild("--lock-child",directory);
                Reject(()=>store.Save(new InventoryPostRequest(request.Kind,"other-source",request.MedicineName,3,12.345m,request.Notes)),"Pending request was overwritten.");
                Reject(()=>store.Resolve("other-source"),"Unrelated result deleted pending record.");
                using(var isolated=new InventoryPendingStore(directory,other,request.Kind))Check(isolated.Load()==null,"Context leaked to another user/target.");
            }
            RunChild("--restore-child",directory);
            using(var restarted=new InventoryPendingStore(directory,key,request.Kind))
            {
                var loaded=restarted.Load();Check(loaded.SourceId==request.SourceId&&loaded.HasSameContent(request),"Restart changed Arabic, source, decimal or quantity.");
                restarted.Resolve(request.SourceId);Check(restarted.Load()==null,"Known result left an active pending record.");
                Reject(()=>restarted.Save(new InventoryPostRequest(request.Kind,"invalid","drug",0,null,"reason")),"Invalid draft persisted.");
                Check(restarted.Load()==null,"Rejected draft created a record.");
            }
            string path=Path.Combine(directory,key+"-2.xml");
            using(var saved=new InventoryPendingStore(directory,key,request.Kind))saved.Save(request);
            string valid=File.ReadAllText(path);
            File.WriteAllText(path,valid.Replace("restore-source","changed-source"));
            using(var store=new InventoryPendingStore(directory,key,request.Kind))Reject(()=>store.Load(),"A valid source change bypassed the integrity digest.");
            foreach(Action<XElement> mutate in new Action<XElement>[] {
                root=>root.SetAttributeValue("context",other),
                root=>root.Element("SourceId").Value="different-valid-source",
                root=>root.Element("Medicine").Value="different valid medicine",
                root=>root.Add(new XElement("Quantity","3")),
                root=>root.Element("Medicine").Add(new XElement("unexpected","altered")),
                root=>root.Element("Quantity").Value="2147483648",
                root=>root.Element("UnitAmount").Value="1e30"
            })
            {
                var document=XDocument.Parse(valid);mutate(document.Root);File.WriteAllText(path,document.ToString());
                using(var store=new InventoryPendingStore(directory,key,request.Kind))Reject(()=>store.Load(),"Wrong context, duplicate/nested data, quantity overflow or invalid money was accepted.");
            }
            File.Delete(path);
            foreach(string invalid in new[]{"<broken>","<!DOCTYPE InventoryPending [<!ENTITY e SYSTEM 'file:///unavailable'>]><InventoryPending>&e;</InventoryPending>",new string('x',32769)})
            {
                File.WriteAllText(path,invalid);
                using(var store=new InventoryPendingStore(directory,key,request.Kind))
                {
                    Reject(()=>store.Load(),"Malformed/DTD/oversize record was accepted.");
                    Reject(()=>store.Save(request),"Corrupt pending record was overwritten.");
                    Check(File.ReadAllText(path)==invalid,"Invalid record was silently changed.");
                }
                File.Delete(path);
            }
            File.WriteAllText(path+".orphan.tmp","interrupted write");
            using(var store=new InventoryPendingStore(directory,key,request.Kind)){Check(store.Load()==null,"Interrupted temporary file became a request.");store.Save(request);Check(store.Load().HasSameContent(request),"Recovery after interrupted temporary write failed.");}
            JournalPendingStoreSelfTests.Verify();
            JournalOperationRecoverySelfTests.Verify();
            var journalRequest=JournalPendingRequest.Restore(new JournalPostRequest(new DateTime(2026,10,5),"Manual",
                "123456781234123412341234567890ab","صيدلية المهندس",new[]{
                new PharmacyManagement.Domain.JournalLineValues("1000",1250.50m,0,"باراسيتامول 500 mg\r\nA&B",null),
                new PharmacyManagement.Domain.JournalLineValues("2000",0,1250.50m,null,7)}));
            using(var journalStore=new JournalPendingStore(directory,new string('c',64),"Manual"))
            {
                journalStore.Save(journalRequest);RunChild("--journal-lock-child",directory);
            }
            RunChild("--journal-restore-child",directory);
            using(var journalStore=new JournalPendingStore(directory,new string('c',64),"Manual"))
                journalStore.Resolve(journalRequest.Request.SourceId);
            Console.WriteLine("PASS journal encrypted pending payload: independent-process lease and passive restore; no posting or SQL.");
            Console.WriteLine("PASS: restart/Arabic/exact decimal, exclusive lease across two processes, context isolation, immutable source, matched resolution, invalid drafts, corrupt/DTD/oversize/duplicate/nested/overflow records and interrupted temporary writes; temp files only, no SQL.");
            return 0;
        }
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
}

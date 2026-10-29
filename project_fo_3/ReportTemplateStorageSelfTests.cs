using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace project_fo_3
{
    internal static class ReportTemplateStorageSelfTests
    {
        private static int failures,cases;
        internal static void Run()
        {
            failures=cases=0;
            string directory=Path.Combine(@"C:\PharmacyQA\work","template-storage-"+Guid.NewGuid().ToString("N"));
            if(Directory.Exists(directory))throw new IOException("Fresh template fixture already exists.");
            Directory.CreateDirectory(directory);Console.WriteLine("Template fixtures: "+directory);
            Verify(directory,"accounting",(path,id,name)=>ReportTemplateStore.SaveAtPath(path,new ReportTemplate
                {TemplateId=id,Name=name,ReportKey="Tax",FromDate=new DateTime(2026,1,1),ToDate=new DateTime(2026,1,31)}),
                ReportTemplateStore.DeleteAtPath,path=>ReportTemplateStore.ReadForTest(path).ToDictionary(item=>item.TemplateId,item=>item.Name),ReportTemplateStore.CanReadForTest);
            Verify(directory,"operational",(path,id,name)=>OperationalReportTemplateStore.SaveAtPath(path,new OperationalReportTemplate
                {TemplateId=id,Name=name,SourceKey="Medicines",FromDate=new DateTime(2026,1,1),ToDate=new DateTime(2026,1,31)}),
                OperationalReportTemplateStore.DeleteAtPath,path=>OperationalReportTemplateStore.ReadForTest(path).ToDictionary(item=>item.TemplateId,item=>item.Name),OperationalReportTemplateStore.CanReadForTest);
            if(failures>0)throw new InvalidOperationException("Template file safety failed in "+failures+" of "+cases+" cases.");
            Console.WriteLine("PASS template file safety: "+cases+" cases across accounting/operational stores; foreign temp ownership, writer lease, locked target cleanup, read-only backup fallback, recovery and unreadable-file refusal; owned C fixtures only, no UI/SQL/user settings.");
        }
        private static void Verify(string directory,string family,Func<string,string,string,bool> save,
            Func<string,string,bool> delete,Func<string,Dictionary<string,string>> read,Func<string,bool,bool> canRead)
        {
            string a=Guid.NewGuid().ToString("N"),b=Guid.NewGuid().ToString("N"),c=Guid.NewGuid().ToString("N");
            RunCase(family+" foreign temp",()=>
            {
                string path=Path.Combine(directory,family+"-foreign.xml");byte[] sentinel=Encoding.UTF8.GetBytes("another producer owns this temporary file");
                File.WriteAllBytes(path+".tmp",sentinel);
                Check(save(path,a,"قالب أول"),"Save failed beside unrelated temporary file.");
                Check(File.Exists(path+".tmp")&&File.ReadAllBytes(path+".tmp").SequenceEqual(sentinel),"Save overwrote or removed another producer's fixed .tmp file.");
                Check(read(path).Count==1&&read(path)[a]=="قالب أول","Saved template did not round-trip.");NoOwnedTemps(path);
            });
            RunCase(family+" busy lease",()=>
            {
                string path=Path.Combine(directory,family+"-lease.xml");Check(save(path,a,"A"),"Could not seed lease fixture.");byte[] before=File.ReadAllBytes(path);
                using(var lease=new FileStream(path+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None))
                {
                    Check(!save(path,b,"B")&&!delete(path,a),"Busy writer lease did not reject a save/delete.");
                    Check(File.ReadAllBytes(path).SequenceEqual(before),"Busy writer changed primary bytes.");
                }
                Check(save(path,b,"B")&&read(path).Count==2&&read(path).ContainsKey(a),"Save did not recover after lease release.");
                Check(delete(path,a)&&read(path).Count==1&&read(path).ContainsKey(b),"Delete did not recover after lease release.");NoOwnedTemps(path);
            });
            RunCase(family+" locked destination",()=>
            {
                string path=Path.Combine(directory,family+"-locked.xml");Check(save(path,a,"A")&&save(path,b,"B"),"Could not seed target fixture.");
                byte[] before=File.ReadAllBytes(path),backup=File.ReadAllBytes(path+".bak");
                using(var locked=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
                {
                    Check(!save(path,c,"C"),"Locked destination allowed replacement.");
                    Check(File.ReadAllBytes(path).SequenceEqual(before)&&File.ReadAllBytes(path+".bak").SequenceEqual(backup),"Failed replacement changed primary or backup.");
                    Check(!File.Exists(path+".tmp"),"Failed replacement left a fixed staging file.");NoOwnedTemps(path);
                }
                Check(save(path,c,"C")&&read(path).Count==3,"Retry after target unlock lost templates.");
            });
            RunCase(family+" external writer lease",()=>
            {
                string path=Path.Combine(directory,family+"-external.xml");Check(save(path,a,"A"),"Could not seed external lease fixture.");
                byte[] before=File.ReadAllBytes(path);
                string ready=path+".ready",release=path+".release";
                string script=@"$ErrorActionPreference='Stop'; $lease=[IO.File]::Open('__LOCK__',[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None); try { [IO.File]::WriteAllText('__READY__'+'.tmp',[string]$PID); [IO.File]::Move('__READY__'+'.tmp','__READY__'); $deadline=[DateTime]::UtcNow.AddSeconds(20); while(-not [IO.File]::Exists('__RELEASE__')) { if([DateTime]::UtcNow -gt $deadline){exit 2}; Start-Sleep -Milliseconds 20 } } finally { $lease.Dispose() }";
                script=script.Replace("__LOCK__",(path+".lock").Replace("'","''")).Replace("__READY__",ready.Replace("'","''")).Replace("__RELEASE__",release.Replace("'","''"));
                var start=new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),@"WindowsPowerShell\v1.0\powershell.exe"),
                    "-NoProfile -NonInteractive -EncodedCommand "+Convert.ToBase64String(Encoding.Unicode.GetBytes(script)))
                    {UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden};
                using(var worker=Process.Start(start))
                {
                    Console.WriteLine("External template lease worker: "+family+"; PID="+worker.Id);
                    try
                    {
                        var wait=Stopwatch.StartNew();
                        while(!File.Exists(ready)&&!worker.HasExited&&wait.ElapsedMilliseconds<10000)Thread.Sleep(10);
                        Check(File.Exists(ready)&&!worker.HasExited,"Owned external lease worker did not become live and ready.");
                        Check(File.ReadAllText(ready)==worker.Id.ToString(),"External lease readiness identity mismatch.");
                        Check(!save(path,b,"B")&&!delete(path,a)&&File.ReadAllBytes(path).SequenceEqual(before),"External lease allowed a store mutation.");
                        Check(read(path).ContainsKey(a),"External writer lease blocked read-only template access.");
                    }
                    finally
                    {
                        File.WriteAllText(release,"release owned fixture");
                        Check(worker.WaitForExit(25000)&&worker.ExitCode==0,"Owned external lease worker did not exit cleanly.");
                    }
                }
                Check(save(path,b,"B")&&read(path).Count==2,"Retry after external lease release lost templates.");NoOwnedTemps(path);
            });
            RunCase(family+" recovery",()=>
            {
                string path=Path.Combine(directory,family+"-recovery.xml");Check(save(path,a,"A")&&save(path,b,"B"),"Could not seed recovery fixture.");
                byte[] backup=File.ReadAllBytes(path+".bak");File.WriteAllText(path,"<invalid");
                Check(read(path).Count==1&&read(path).ContainsKey(a),"Readable backup was not recovered.");
                Check(save(path,c,"C")&&read(path).Count==2&&read(path).ContainsKey(a)&&read(path).ContainsKey(c),"Recovery save lost the backup template.");
                Check(File.ReadAllBytes(path+".bak").SequenceEqual(backup),"Recovery save replaced the readable backup with damaged primary.");NoOwnedTemps(path);
            });
            RunCase(family+" unavailable primary",()=>
            {
                string path=Path.Combine(directory,family+"-unavailable.xml");Check(save(path,a,"A")&&save(path,b,"B"),"Could not seed unavailable fixture.");
                byte[] before=File.ReadAllBytes(path),backup=File.ReadAllBytes(path+".bak");
                using(var locked=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.None))
                {
                    Check(canRead(path,false)&&read(path).Count==1,"Read-only backup fallback was lost.");
                    Check(!canRead(path,true),"Unavailable primary was treated as safe input for writing from older backup.");
                    Check(!save(path,c,"C")&&!delete(path,a),"Unavailable primary allowed a backup-based mutation.");NoOwnedTemps(path);
                }
                Check(File.ReadAllBytes(path).SequenceEqual(before)&&File.ReadAllBytes(path+".bak").SequenceEqual(backup),"Unavailable primary or readable backup changed.");
            });
            RunCase(family+" unreadable files",()=>
            {
                string path=Path.Combine(directory,family+"-unreadable.xml");File.WriteAllText(path,"<bad-primary");File.WriteAllText(path+".bak","<bad-backup");
                byte[] before=File.ReadAllBytes(path),backup=File.ReadAllBytes(path+".bak");
                Check(!save(path,c,"C")&&!delete(path,a),"Unreadable primary/backup allowed mutation.");
                Check(File.ReadAllBytes(path).SequenceEqual(before)&&File.ReadAllBytes(path+".bak").SequenceEqual(backup),"Unreadable files were replaced.");NoOwnedTemps(path);
            });
        }
        private static void RunCase(string name,Action action)
        {cases++;try{action();Console.WriteLine("PASS "+name);}catch(Exception error){failures++;Console.WriteLine("FAIL "+name+": "+error.Message);}}
        private static void NoOwnedTemps(string path)
        {Check(!Directory.GetFiles(Path.GetDirectoryName(path),"."+Path.GetFileName(path)+".*.tmp").Any(),"Owned unique temporary file remained.");}
        private static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    }
}

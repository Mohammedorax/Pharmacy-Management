using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using PharmacyManagement.Application;
using project_fo_3;
namespace PharmacyManagement.CoreTests
{
    internal static class VoucherPendingStoreTests
    {
        private static int checks;
        private static void Check(bool value,[System.Runtime.CompilerServices.CallerLineNumber]int line=0){if(!value)throw new Exception("Voucher pending store/recovery test failed at line "+line+" after "+checks+" checks.");checks++;}
        private static void Throws<T>(Action action) where T:Exception{try{action();}catch(T){checks++;return;}throw new Exception("Expected "+typeof(T).Name);}
        private static VoucherPendingRequest Request(Guid id,string party=" طرف عربي 500 mg\r\nA&B ",int? center=7)=>VoucherPendingRequest.Restore(new VoucherPostRequest("PaymentMulti",new DateTime(2026,10,6,12,30,45).AddMilliseconds(2),party," 2000 ",10.015m," ملاحظة \r\n<text> "," 1000 ",center," sar ",id));
        private static string OwnedPath(string token){Guid id;if(!Guid.TryParseExact(token,"N",out id)||id==Guid.Empty)throw new UnauthorizedAccessException();return Path.Combine(Path.GetTempPath(),"PharmacyVoucherStore-"+token);}
        internal static void ChildRead(string token,string requestId)
        {
            string directory=OwnedPath(token);if(File.ReadAllText(Path.Combine(directory,"owner"))!=token)throw new UnauthorizedAccessException();
            using(var store=new VoucherPendingStore(directory,new string('a',64)))Check(store.Load().HasSameContent(Request(Guid.ParseExact(requestId,"N")).Request));
            Console.WriteLine("PASS independently loaded encrypted voucher request.");
        }
        private static VoucherDraftInputs Inputs(decimal amount=10.005m,decimal rate=2.5m)=>new VoucherDraftInputs(amount,rate,"USD","Cheque"," ملاحظات أصلية\r\nA&B ","000127","بنك عربي 500 mg",new DateTime(2026,10,7),"قيد التحصيل","مندوب 01","");
        internal static void ChildInputsRead(string token,string requestId)
        {
            string directory=OwnedPath(token);if(File.ReadAllText(Path.Combine(directory,"owner"))!=token)throw new UnauthorizedAccessException();
            using(var store=new VoucherPendingStore(directory,new string('a',64)))
            {var loaded=store.Load();Check(loaded.Request.RequestId==Guid.ParseExact(requestId,"N")&&loaded.DraftInputs.Equals(Inputs())&&loaded.Request.Amount==25.01m);}
            Console.WriteLine("PASS independently loaded original voucher conversion/cheque inputs.");
        }
        private static Guid GuidFromFile(string directory,string context){using(var store=new VoucherPendingStore(directory,context))return store.Load().Request.RequestId.Value;}
        internal static void Run()
        {
            checks=0;string token=Guid.NewGuid().ToString("N"),directory=OwnedPath(token),context=new string('a',64),file=Path.Combine(directory,context+"-Voucher.pending");
            Directory.CreateDirectory(directory);File.WriteAllText(Path.Combine(directory,"owner"),token);var pending=Request(Guid.NewGuid());
            try
            {
                Check(pending.Request.Amount==10.02m&&pending.Request.ExpectedSystemCurrencyCode=="SAR"&&pending.Request.CashAccountCode=="1000");
                Check(!pending.HasSameContent(Request(Guid.NewGuid()).Request));Check(!pending.HasSameContent(Request(pending.Request.RequestId.Value,"changed").Request));Check(!pending.HasSameContent(Request(pending.Request.RequestId.Value,center:null).Request));
                Throws<ArgumentException>(()=>new VoucherPendingStore(directory,"bad"));
                using(var store=new VoucherPendingStore(directory,context))
                {
                    Check(store.Load()==null);Throws<IOException>(()=>{using(var duplicate=new VoucherPendingStore(directory,context)){} });Throws<ArgumentNullException>(()=>store.Save(null));
                    store.Save(pending);Check(store.Load().HasSameContent(pending.Request));var original=File.ReadAllBytes(file);Check(!Encoding.UTF8.GetString(original).Contains("VoucherPending")&&!Encoding.UTF8.GetString(original).Contains("طرف عربي"));
                    store.Save(pending);Check(original.SequenceEqual(File.ReadAllBytes(file)));Throws<InvalidOperationException>(()=>store.Save(Request(Guid.NewGuid())));Throws<InvalidOperationException>(()=>store.Resolve(Guid.NewGuid()));
                    var damaged=(byte[])original.Clone();damaged[damaged.Length/2]^=0x40;File.WriteAllBytes(file,damaged);Throws<CryptographicException>(()=>store.Load());Throws<CryptographicException>(()=>store.Save(pending));Check(damaged.SequenceEqual(File.ReadAllBytes(file)));File.WriteAllBytes(file,original);
                    using(var other=new VoucherPendingStore(directory,new string('b',64))){Check(other.Load()==null);File.WriteAllBytes(Path.Combine(directory,new string('b',64)+"-Voucher.pending"),original);Throws<CryptographicException>(()=>other.Load());}
                    var xml=Encoding.UTF8.GetBytes("<!DOCTYPE VoucherPending [<!ENTITY x SYSTEM 'file:///nonexistent'>]><VoucherPending>&x;</VoucherPending>");File.WriteAllBytes(file,ProtectedData.Protect(xml,Encoding.UTF8.GetBytes("PharmacyVoucherPending/v1/"+context+"/Voucher"),DataProtectionScope.CurrentUser));Throws<System.Xml.XmlException>(()=>store.Load());File.WriteAllBytes(file,new byte[1048576+65537]);Throws<InvalidDataException>(()=>store.Load());File.WriteAllBytes(file,original);Check(!Directory.GetFiles(directory,"*.tmp").Any());
                }
                var info=new ProcessStartInfo(System.Reflection.Assembly.GetExecutingAssembly().Location,"--owned-voucher-store-child-read "+token+" "+pending.Request.RequestId.Value.ToString("N")){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
                using(var child=Process.Start(info)){if(!child.WaitForExit(10000)){child.Kill();throw new Exception("Child restore timed out");}Check(child.ExitCode==0);Check(child.StandardOutput.ReadToEnd().Contains("PASS independently loaded"));}
                using(var store=new VoucherPendingStore(directory,context)){store.Resolve(pending.Request.RequestId.Value);Check(store.Load()==null);}
                int posts=0;bool accepted=false;Guid frozen=Guid.Empty;
                Func<VoucherSubmissionStatusRequest,CancellationToken,Task<VoucherSubmissionStatus>> read=(request,cancel)=>Task.FromResult(accepted?new VoucherSubmissionStatus(41,42,true):new VoucherSubmissionStatus(0,0));
                using(var operation=new VoucherOperationRecovery(new VoucherPendingStore(directory,context),()=>true,request=>{posts++;frozen=request.RequestId.Value;accepted=true;throw new IOException("Simulated lost commit response");},read))
                {operation.Prepare(pending);Throws<IOException>(()=>operation.ExecuteAsync(CancellationToken.None).GetAwaiter().GetResult());Check(operation.Pending!=null&&operation.Accepted==null);}
                using(var operation=new VoucherOperationRecovery(new VoucherPendingStore(directory,context),()=>true,request=>{posts++;return Task.FromResult(new VoucherPostResult(99,100));},read))
                {Check(operation.Pending.HasSameContent(pending.Request));var result=operation.ExecuteAsync(CancellationToken.None).GetAwaiter().GetResult();Check(result.VoucherId==41&&result.JournalEntryId==42&&result.AlreadyPosted&&posts==1&&frozen==pending.Request.RequestId);Check(operation.Pending==null);}
                bool current=true;var status=new TaskCompletionSource<VoucherSubmissionStatus>();
                using(var operation=new VoucherOperationRecovery(new VoucherPendingStore(directory,context),()=>current,request=>{posts++;return Task.FromResult(new VoucherPostResult(1,2));},(request,cancel)=>status.Task))
                {operation.Prepare(pending);var task=operation.ExecuteAsync(CancellationToken.None);Throws<InvalidOperationException>(()=>operation.Prepare(pending));current=false;status.SetResult(new VoucherSubmissionStatus(0,0));Throws<UnauthorizedAccessException>(()=>task.GetAwaiter().GetResult());Check(posts==1&&operation.Pending!=null);}
                using(var store=new VoucherPendingStore(directory,context)){store.Resolve(pending.Request.RequestId.Value);}
                using(var operation=new VoucherOperationRecovery(new VoucherPendingStore(directory,context),()=>true,request=>{posts++;return Task.FromResult(new VoucherPostResult(1,2));},(request,cancel)=>Task.FromResult(new VoucherSubmissionStatus(41,42,false))))
                {operation.Prepare(pending);Check(operation.ExecuteAsync(CancellationToken.None).GetAwaiter().GetResult().VoucherId==41&&!operation.AcceptedContentMatchesRequest&&posts==1);}
                using(var operation=new VoucherOperationRecovery(new VoucherPendingStore(directory,context),()=>true,request=>Task.FromResult(new VoucherPostResult(1,2)),(request,cancel)=>Task.FromResult(new VoucherSubmissionStatus(41,42))))
                {operation.Prepare(pending);Throws<InvalidOperationException>(()=>operation.ExecuteAsync(CancellationToken.None).GetAwaiter().GetResult());Check(operation.Pending!=null);}
                using(var store=new VoucherPendingStore(directory,context)){store.Resolve(pending.Request.RequestId.Value);}
                current=true;var cancelSource=new CancellationTokenSource();
                using(var operation=new VoucherOperationRecovery(new VoucherPendingStore(directory,context),()=>current,request=>{current=false;cancelSource.Cancel();return Task.FromResult(new VoucherPostResult(61,62));},(request,cancel)=>Task.FromResult(new VoucherSubmissionStatus(0,0))))
                {operation.Prepare(pending);var result=operation.ExecuteAsync(cancelSource.Token).GetAwaiter().GetResult();Check(result.VoucherId==61&&operation.Accepted.JournalEntryId==62&&operation.Pending==null);}
                using(var operation=new VoucherOperationRecovery(new VoucherPendingStore(directory,context),()=>true,request=>{posts++;return Task.FromResult(new VoucherPostResult(1,2));},(request,cancel)=>Task.FromResult(new VoucherSubmissionStatus(0,0))))
                {operation.Prepare(pending);cancelSource=new CancellationTokenSource();cancelSource.Cancel();Throws<OperationCanceledException>(()=>operation.ExecuteAsync(cancelSource.Token).GetAwaiter().GetResult());Check(posts==1&&operation.Pending!=null);}
                using(var store=new VoucherPendingStore(directory,context)){store.Resolve(pending.Request.RequestId.Value);}
                using(var operation=new VoucherOperationRecovery(new VoucherPendingStore(directory,context),()=>true,request=>{throw new VoucherAcceptedRequestConflictException(request.RequestId.Value,71,72);},(request,cancel)=>Task.FromResult(new VoucherSubmissionStatus(0,0))))
                {operation.Prepare(pending);Check(operation.ExecuteAsync(CancellationToken.None).GetAwaiter().GetResult().VoucherId==71&&!operation.AcceptedContentMatchesRequest);}
                using(var operation=new VoucherOperationRecovery(new VoucherPendingStore(directory,context),()=>true,request=>{throw new VoucherAcceptedRequestConflictException(Guid.NewGuid(),71,72);},(request,cancel)=>Task.FromResult(new VoucherSubmissionStatus(0,0))))
                {operation.Prepare(pending);Throws<VoucherAcceptedRequestConflictException>(()=>operation.ExecuteAsync(CancellationToken.None).GetAwaiter().GetResult());Check(operation.Pending!=null&&operation.Accepted==null);}
                using(var operation=new VoucherOperationRecovery(new VoucherPendingStore(directory,context),()=>true,request=>Task.FromResult(new VoucherPostResult(1,2)),(request,cancel)=>Task.FromResult(new VoucherSubmissionStatus(81,82,true))))
                {
                    using(var held=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.None))
                    {Check(operation.ExecuteAsync(CancellationToken.None).GetAwaiter().GetResult().VoucherId==81&&operation.CleanupError!=null&&operation.Pending!=null);}
                    Check(operation.RetryAcceptedCleanup()&&operation.Pending==null&&operation.CleanupError==null);
                }
                Throws<ArgumentException>(()=>new VoucherPostRejectedException(Guid.Empty,new Exception()));Throws<ArgumentNullException>(()=>new VoucherPostRejectedException(Guid.NewGuid(),null));
                bool reject=true;
                using(var operation=new VoucherOperationRecovery(new VoucherPendingStore(directory,context),()=>true,request=>{if(reject)throw new VoucherPostRejectedException(request.RequestId.Value,new InvalidOperationException("proven refusal"));return Task.FromResult(new VoucherPostResult(91,92));},(request,cancel)=>Task.FromResult(new VoucherSubmissionStatus(0,0))))
                {
                    operation.Prepare(pending);Throws<InvalidOperationException>(()=>operation.CorrectRejected(Request(pending.Request.RequestId.Value,"corrected")));
                    Throws<VoucherPostRejectedException>(()=>operation.ExecuteAsync(CancellationToken.None).GetAwaiter().GetResult());Check(operation.CanCorrectRejected);
                    Throws<InvalidOperationException>(()=>operation.CorrectRejected(Request(Guid.NewGuid(),"corrected")));Check(operation.Pending.HasSameContent(pending.Request));
                    var corrected=Request(pending.Request.RequestId.Value,"corrected");operation.CorrectRejected(corrected);Check(!operation.CanCorrectRejected&&operation.Pending.HasSameContent(corrected.Request));
                    reject=false;Check(operation.ExecuteAsync(CancellationToken.None).GetAwaiter().GetResult().VoucherId==91&&operation.Pending==null);
                }
                using(var operation=new VoucherOperationRecovery(new VoucherPendingStore(directory,context),()=>true,request=>{throw new IOException("unknown outcome");},(request,cancel)=>Task.FromResult(new VoucherSubmissionStatus(0,0))))
                {operation.Prepare(pending);Throws<IOException>(()=>operation.ExecuteAsync(CancellationToken.None).GetAwaiter().GetResult());Check(!operation.CanCorrectRejected);Throws<InvalidOperationException>(()=>operation.CorrectRejected(Request(pending.Request.RequestId.Value,"corrected")));}
                using(var store=new VoucherPendingStore(directory,context)){store.Resolve(pending.Request.RequestId.Value);}
                using(var operation=new VoucherOperationRecovery(new VoucherPendingStore(directory,context),()=>true,request=>{throw new VoucherPostRejectedException(Guid.NewGuid(),new Exception());},(request,cancel)=>Task.FromResult(new VoucherSubmissionStatus(0,0))))
                {operation.Prepare(pending);Throws<VoucherPostRejectedException>(()=>operation.ExecuteAsync(CancellationToken.None).GetAwaiter().GetResult());Check(!operation.CanCorrectRejected);}
                using(var store=new VoucherPendingStore(directory,context)){store.Resolve(pending.Request.RequestId.Value);}
                current=true;
                using(var operation=new VoucherOperationRecovery(new VoucherPendingStore(directory,context),()=>current,request=>{current=false;throw new VoucherPostRejectedException(request.RequestId.Value,new Exception());},(request,cancel)=>Task.FromResult(new VoucherSubmissionStatus(0,0))))
                {operation.Prepare(pending);Throws<UnauthorizedAccessException>(()=>operation.ExecuteAsync(CancellationToken.None).GetAwaiter().GetResult());Check(!operation.CanCorrectRejected);}
                using(var store=new VoucherPendingStore(directory,context))
                {
                    store.Resolve(pending.Request.RequestId.Value);var inputs=Inputs();var identity=Guid.NewGuid();var value=new VoucherPostRequest("PaymentMulti",new DateTime(2026,10,6),"حساب الطرف","2000",25.0125m,inputs.BuildNotes(),"1000",7,"YER",identity);
                    var detailed=VoucherPendingRequest.Restore(value,inputs);store.Save(detailed);Check(store.Load().HasSameState(detailed));Check(store.Load().DraftInputs.OriginalAmount==10.005m&&store.Load().DraftInputs.ExchangeRate==2.5m);
                    var bytes=File.ReadAllBytes(file);var changed=VoucherPendingRequest.Restore(value,Inputs(5.0025m,5m));Check(changed.HasSameContent(value)&&!changed.HasSameState(detailed));Throws<InvalidOperationException>(()=>store.Save(changed));Check(bytes.SequenceEqual(File.ReadAllBytes(file)));
                    Throws<InvalidOperationException>(()=>VoucherPendingRequest.Restore(new VoucherPostRequest(value.Type,value.Date,value.PartyName,value.AccountCode,26,value.Notes,value.CashAccountCode,7,"YER",identity),inputs));
                    Throws<InvalidOperationException>(()=>new VoucherDraftInputs(10,0,"USD","Cash",null,null,null,null,null,null,null));
                    Throws<InvalidOperationException>(()=>new VoucherDraftInputs(10,1,"USD","Cheque",null,"1",null,null,null,null,null));
                    Throws<InvalidOperationException>(()=>new VoucherDraftInputs(10,1,"USD","Unknown",null,null,null,null,null,null,null));
                }
                using(var store=new VoucherPendingStore(directory,context))
                {
                    var expected=store.Load();var updated=VoucherPendingRequest.Restore(expected.Request,Inputs(5.0025m,5m));store.ReplaceRejected(expected,updated);Check(store.Load().HasSameState(updated));store.ReplaceRejected(updated,expected);Check(store.Load().HasSameState(expected));
                }
                using(var operation=new VoucherOperationRecovery(new VoucherPendingStore(directory,context),()=>true,value=>{throw new VoucherPostRejectedException(value.RequestId.Value,new Exception("QA refusal"));},(value,cancel)=>Task.FromResult(new VoucherSubmissionStatus(0,0))))
                {
                    var expected=operation.Pending;Throws<VoucherPostRejectedException>(()=>operation.ExecuteAsync(CancellationToken.None).GetAwaiter().GetResult());
                    Throws<InvalidOperationException>(()=>operation.CorrectRejected(VoucherPendingRequest.Restore(expected.Request)));Check(operation.CanCorrectRejected&&operation.Pending.HasSameState(expected));
                    operation.CorrectRejected(VoucherPendingRequest.Restore(expected.Request,Inputs(5.0025m,5m)));Check(operation.Pending.DraftInputs.OriginalAmount==5.0025m&&!operation.CanCorrectRejected);
                    Throws<VoucherPostRejectedException>(()=>operation.ExecuteAsync(CancellationToken.None).GetAwaiter().GetResult());operation.CorrectRejected(expected);Check(operation.Pending.HasSameState(expected));
                }
                var inputInfo=new ProcessStartInfo(System.Reflection.Assembly.GetExecutingAssembly().Location,"--owned-voucher-inputs-child-read "+token+" "+GuidFromFile(directory,context).ToString("N")){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
                using(var child=Process.Start(inputInfo)){if(!child.WaitForExit(10000)){child.Kill();throw new Exception("Input child timed out");}Check(child.ExitCode==0);Check(child.StandardOutput.ReadToEnd().Contains("PASS independently loaded original"));}
                using(var store=new VoucherPendingStore(directory,context))
                {
                    var loaded=store.Load();store.Resolve(loaded.Request.RequestId.Value);store.Save(pending);
                    byte[] clear=ProtectedData.Unprotect(File.ReadAllBytes(file),Encoding.UTF8.GetBytes("PharmacyVoucherPending/v1/"+context+"/Voucher"),DataProtectionScope.CurrentUser);
                    var xml=System.Xml.Linq.XDocument.Parse(Encoding.UTF8.GetString(clear));xml.Root.SetAttributeValue("version","1");xml.Root.Element("DraftInputs").Remove();
                    using(var memory=new MemoryStream()){using(var writer=System.Xml.XmlWriter.Create(memory,new System.Xml.XmlWriterSettings{Encoding=new UTF8Encoding(false),CloseOutput=false,NewLineHandling=System.Xml.NewLineHandling.Entitize}))xml.Save(writer);File.WriteAllBytes(file,ProtectedData.Protect(memory.ToArray(),Encoding.UTF8.GetBytes("PharmacyVoucherPending/v1/"+context+"/Voucher"),DataProtectionScope.CurrentUser));}Array.Clear(clear,0,clear.Length);
                    Check(store.Load().DraftInputs==null&&store.Load().HasSameContent(pending.Request));var legacy=File.ReadAllBytes(file);store.Save(pending);Check(legacy.SequenceEqual(File.ReadAllBytes(file)));
                }
                Console.WriteLine("PASS: "+checks+" encrypted voucher store/recovery checks, owned independent-process reload and simulated lost response; no SQL/UI.");
            }
            finally{if(Directory.Exists(directory)&&File.ReadAllText(Path.Combine(directory,"owner"))==token)Directory.Delete(directory,true);}
        }
    }
}
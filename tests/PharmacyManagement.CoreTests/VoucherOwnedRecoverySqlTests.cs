using System;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Application;
using PharmacyManagement.Infrastructure.SqlServer;
using project_fo_3;
namespace PharmacyManagement.CoreTests
{
    internal static class VoucherOwnedRecoverySqlTests
    {
        private static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        private static Func<SqlConnection> Factory(string database,string token)
        {
            Guid owner;Require(Guid.TryParseExact(token,"N",out owner)&&owner!=Guid.Empty&&System.Text.RegularExpressions.Regex.IsMatch(database,"^PharmacyRestore_[0-9]{14}_"+token+"_qa$"),"Owned clone identity required.");
            var builder=new SqlConnectionStringBuilder{DataSource=".",InitialCatalog=database,IntegratedSecurity=true,Encrypt=true,TrustServerCertificate=true,Pooling=false,ConnectTimeout=10,ApplicationName="Pharmacy.QaVoucherRecovery."+token};
            Func<SqlConnection> factory=()=>new SqlConnection(builder.ConnectionString);
            using(var connection=factory())
            {
                connection.Open();using(var command=new SqlCommand("SELECT DB_NAME(),CONVERT(nvarchar(32),value) FROM sys.extended_properties WHERE class=0 AND name=N'QaTest.Owner'",connection))using(var reader=command.ExecuteReader())Require(reader.Read()&&reader.GetString(0)==database&&reader.GetString(1)==token&&!reader.Read(),"Owned clone property required.");
            }
            return factory;
        }
        private static string DirectoryPath(string token,Guid requestId)
        {Guid owner;Require(Guid.TryParseExact(token,"N",out owner)&&owner!=Guid.Empty&&requestId!=Guid.Empty,"Invalid owned temporary identity.");return Path.Combine(Path.GetTempPath(),"PharmacyVoucherSqlRecovery-"+token+"-"+requestId.ToString("N"));}
        private static string Context(string database,string user)
        {using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes("QA/Voucher/./"+database+"/"+user))).Replace("-","").ToLowerInvariant();}
        internal static int Child(string database,string token,string identity,bool writer)
        {
            Guid requestId;Require(Guid.TryParseExact(identity,"N",out requestId)&&requestId!=Guid.Empty,"Invalid request identity.");
            var factory=Factory(database,token);string directory=DirectoryPath(token,requestId);Require(File.ReadAllText(Path.Combine(directory,"owner"))==token,"Owned recovery folder required.");
            var actor=new OperationActor{UserName="QA-VoucherRequest-"+token,UserRole="Manager"};var status=new VoucherSubmissionStatusService(new SqlVoucherSubmissionStatusRepository(factory),a=>a.UserName==actor.UserName&&a.UserRole==actor.UserRole);
            var service=new VoucherPostService(new SqlVoucherPostRepository(factory),a=>a.UserName==actor.UserName&&a.UserRole==actor.UserRole);
            int writes=0;VoucherPostResult committed=null;
            using(var operation=new VoucherOperationRecovery(new VoucherPendingStore(directory,Context(database,actor.UserName)),()=>true,request=>
            {
                writes++;if(!writer)throw new InvalidOperationException("Reader attempted a new financial write.");
                committed=service.Post(request,actor);
                File.WriteAllText(Path.Combine(directory,"commit-proof"),requestId.ToString("N")+"|"+committed.VoucherId+"|"+committed.JournalEntryId+"|"+Process.GetCurrentProcess().Id);
                throw new IOException("Controlled suppression after real SQL commit and before controller acceptance.");
            },(request,cancel)=>status.FindAsync(request,actor,cancel)))
            {
                Require(operation.Pending!=null&&operation.Pending.Request.RequestId==requestId,"Persisted request missing/mismatched.");
                if(writer)
                {
                    try{operation.ExecuteAsync(CancellationToken.None).GetAwaiter().GetResult();throw new Exception("Response was not suppressed.");}
                    catch(IOException){Require(committed!=null&&!committed.AlreadyPosted&&writes==1&&operation.Accepted==null&&operation.Pending!=null,"Writer did not leave unknown outcome after real commit.");Console.WriteLine("PASS actual SQL committed; response deliberately suppressed; pending retained.");return 41;}
                }
                var result=operation.ExecuteAsync(CancellationToken.None).GetAwaiter().GetResult();
                Require(writes==0&&result.AlreadyPosted&&operation.AcceptedContentMatchesRequest&&operation.Pending==null&&operation.CleanupError==null,"Reader did not recover without posting.");
                File.WriteAllText(Path.Combine(directory,"recovery-proof"),requestId.ToString("N")+"|"+result.VoucherId+"|"+result.JournalEntryId+"|"+Process.GetCurrentProcess().Id);
                Console.WriteLine("PASS independent process recovered exact SQL acceptance without posting.");return 0;
            }
        }
        private static int RunChild(string flag,string database,string token,Guid id,string directory,string label)
        {
            var info=new ProcessStartInfo(System.Reflection.Assembly.GetExecutingAssembly().Location,flag+" "+database+" "+token+" "+id.ToString("N")+" --confirm-owned-temporary-qa-writes"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
            using(var child=Process.Start(info))
            {
                var output=child.StandardOutput.ReadToEndAsync();var error=child.StandardError.ReadToEndAsync();
                if(!child.WaitForExit(30000)){child.Kill();child.WaitForExit();throw new Exception("Owned SQL child timed out.");}
                File.WriteAllText(Path.Combine(directory,label+".stdout.log"),output.GetAwaiter().GetResult());File.WriteAllText(Path.Combine(directory,label+".stderr.log"),error.GetAwaiter().GetResult());return child.ExitCode;
            }
        }
        internal static void VerifyCorrection(string database,string token,VoucherPostRequest valid)
        {
            var factory=Factory(database,token);var actor=new OperationActor{UserName="QA-VoucherRequest-"+token,UserRole="Manager"};
            var service=new VoucherPostService(new SqlVoucherPostRepository(factory),a=>a.UserName==actor.UserName&&a.UserRole==actor.UserRole);
            var status=new VoucherSubmissionStatusService(new SqlVoucherSubmissionStatusRepository(factory),a=>a.UserName==actor.UserName&&a.UserRole==actor.UserRole);
            string directory=DirectoryPath(token,valid.RequestId.Value),missingAccount="QAX"+token.Substring(0,12);Require(!Directory.Exists(directory),"Correction folder already exists.");Directory.CreateDirectory(directory);File.WriteAllText(Path.Combine(directory,"owner"),token);
            try
            {
                using(var connection=factory())
                {
                    connection.Open();using(var command=new SqlCommand("SELECT COUNT(*) FROM dbo.AccountsTb WHERE AccountCode=@Code",connection)){command.Parameters.Add("@Code",SqlDbType.NVarChar,20).Value=missingAccount;Require(Convert.ToInt32(command.ExecuteScalar())==0,"Refusal account must be absent.");}
                }
                var pending=VoucherPendingRequest.Restore(new VoucherPostRequest(valid.Type,valid.Date,valid.PartyName,missingAccount,valid.Amount,valid.Notes,valid.CashAccountCode,valid.CostCenterId,valid.ExpectedSystemCurrencyCode,valid.RequestId));
                using(var operation=new VoucherOperationRecovery(new VoucherPendingStore(directory,Context(database,actor.UserName)),()=>true,request=>Task.FromResult(service.Post(request,actor)),(request,cancel)=>status.FindAsync(request,actor,cancel)))
                {
                    operation.Prepare(pending);bool rejected=false;
                    try{operation.ExecuteAsync(CancellationToken.None).GetAwaiter().GetResult();}catch(VoucherPostRejectedException refusal){Require(refusal.RequestId==valid.RequestId&&refusal.InnerException!=null,"Refusal identity missing.");rejected=true;}
                    Require(rejected&&operation.CanCorrectRejected&&operation.Accepted==null,"Known rollback did not allow correction.");
                    Require(!status.FindAsync(new VoucherSubmissionStatusRequest(pending.Request),actor,CancellationToken.None).GetAwaiter().GetResult().IsPosted,"Rejected request persisted a voucher.");
                    operation.CorrectRejected(VoucherPendingRequest.Restore(valid));Require(!operation.CanCorrectRejected&&operation.Pending.Request.RequestId==valid.RequestId,"Correction changed GUID.");
                    var accepted=operation.ExecuteAsync(CancellationToken.None).GetAwaiter().GetResult();Require(accepted.VoucherId>0&&accepted.JournalEntryId>0&&!accepted.AlreadyPosted&&operation.Pending==null,"Corrected request did not commit once.");
                    Require(service.Post(valid,actor).AlreadyPosted,"Corrected GUID retry did not return saved acceptance.");
                    Console.WriteLine("PASS SQL proven rollback and same-GUID corrected acceptance: voucher="+accepted.VoucherId+", journal="+accepted.JournalEntryId+".");
                }
            }
            finally{if(Directory.Exists(directory)&&File.ReadAllText(Path.Combine(directory,"owner"))==token)Directory.Delete(directory,true);}
        }
        internal static void Verify(string database,string token,VoucherPostRequest request,string evidenceDirectory)
        {
            Factory(database,token);var id=request.RequestId.Value;string directory=DirectoryPath(token,id);Require(!Directory.Exists(directory),"Owned recovery directory already exists.");Directory.CreateDirectory(directory);File.WriteAllText(Path.Combine(directory,"owner"),token);
            try
            {
                string context=Context(database,"QA-VoucherRequest-"+token);
                using(var store=new VoucherPendingStore(directory,context))store.Save(VoucherPendingRequest.Restore(request));
                Require(RunChild("--owned-voucher-sql-response-writer",database,token,id,directory,"writer")==41,"Writer did not exit with controlled lost response.");
                using(var store=new VoucherPendingStore(directory,context))Require(store.Load().HasSameContent(request),"Pending request changed after commit.");
                string[] commit=File.ReadAllText(Path.Combine(directory,"commit-proof")).Split('|');
                Require(RunChild("--owned-voucher-sql-response-reader",database,token,id,directory,"reader")==0,"Reader recovery failed.");
                string[] recovery=File.ReadAllText(Path.Combine(directory,"recovery-proof")).Split('|');
                Require(commit.Length==4&&recovery.Length==4&&commit[0]==id.ToString("N")&&commit[0]==recovery[0]&&commit[1]==recovery[1]&&commit[2]==recovery[2]&&commit[3]!=recovery[3]&&commit[3]!=Process.GetCurrentProcess().Id.ToString()&&recovery[3]!=Process.GetCurrentProcess().Id.ToString(),"Independent process acceptance proof mismatch.");
                using(var store=new VoucherPendingStore(directory,context))Require(store.Load()==null,"Recovered pending file was not removed.");
                Directory.CreateDirectory(evidenceDirectory);foreach(string name in new[]{"commit-proof","recovery-proof","writer.stdout.log","writer.stderr.log","reader.stdout.log","reader.stderr.log"})File.Copy(Path.Combine(directory,name),Path.Combine(evidenceDirectory,name));
                Console.WriteLine("PASS SQL recovery: parent="+Process.GetCurrentProcess().Id+", writer="+commit[3]+", reader="+recovery[3]+", voucher="+commit[1]+", journal="+commit[2]+"; one commit, independent recovery without posting.");
            }
            finally{if(Directory.Exists(directory)&&File.ReadAllText(Path.Combine(directory,"owner"))==token)Directory.Delete(directory,true);}
        }
    }
}
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Application;
using PharmacyManagement.Domain;
using PharmacyManagement.Infrastructure.SqlServer;
namespace PharmacyManagement.CoreTests
{
    internal static class VoucherRequestIdentitySqlTests
    {
        static int checks;
        static void Check(bool value,string message){if(!value)throw new Exception(message);checks++;}
        static void Throws<T>(Action action)where T:Exception {try{action();}catch(T){checks++;return;}throw new Exception("Expected "+typeof(T).Name);}
        static int Scalar(SqlConnection c,SqlTransaction t,string sql){using(var q=new SqlCommand(sql,c,t)){q.CommandTimeout=15;return Convert.ToInt32(q.ExecuteScalar());}}
        static void Exec(SqlConnection c,SqlTransaction t,string sql){using(var q=new SqlCommand(sql,c,t)){q.CommandTimeout=15;q.ExecuteNonQuery();}}
        static string Snapshot(SqlConnection c)
        {
            var entries=new[]{
                "SELECT VoucherId,VoucherType,VoucherDate,PartyName,AccountCode,Amount,Notes,JournalEntryId,CreatedBy,CreatedAt FROM dbo.AccountingVouchersTb ORDER BY VoucherId",
                "SELECT * FROM dbo.JournalEntriesTb ORDER BY JournalEntryId",
                "SELECT * FROM dbo.JournalEntryLinesTb ORDER BY JournalEntryLineId",
                "SELECT * FROM dbo.AuditLogTb ORDER BY AuditId",
                "SELECT * FROM dbo.AccountingPeriodsTb ORDER BY PeriodId",
                "SELECT * FROM dbo.CurrencySettingsTb ORDER BY CurrencyCode",
                "SELECT * FROM dbo.CostCentersTb ORDER BY CostCenterId"};
            var all=new StringBuilder();foreach(string sql in entries)using(var q=new SqlCommand("SELECT CONVERT(nvarchar(max),("+sql+" FOR XML RAW('r'),ROOT('rows'),BINARY BASE64,TYPE))",c))all.Append(Convert.ToString(q.ExecuteScalar(),CultureInfo.InvariantCulture));
            using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(all.ToString()))).Replace("-","");
        }
        internal static void Verify(string server,string database,string token,bool missingSchemaOnly)
        {
            Guid owner;if(server!="."||!Guid.TryParseExact(token,"N",out owner)||!System.Text.RegularExpressions.Regex.IsMatch(database,"^PharmacyRestore_[0-9]{14}_"+token+"_qa$"))throw new UnauthorizedAccessException("Owned restored QA target required.");
            var builder=new SqlConnectionStringBuilder{DataSource=server,InitialCatalog=database,IntegratedSecurity=true,Encrypt=true,TrustServerCertificate=true,Pooling=false,ConnectTimeout=10,ApplicationName="Pharmacy.QaVoucherRequest."+token};
            Func<SqlConnection> factory=()=>new SqlConnection(builder.ConnectionString);
            using(var c=factory())
            {
                c.Open();using(var q=new SqlCommand("SELECT DB_NAME(),CONVERT(nvarchar(32),value) FROM sys.extended_properties WHERE class=0 AND name=N'QaTest.Owner'",c))using(var r=q.ExecuteReader())
                    if(!r.Read()||r.GetString(0)!=database||r.GetString(1)!=token||r.Read())throw new UnauthorizedAccessException("QA ownership not verified.");
                Check(Scalar(c,null,"SELECT COUNT(*) FROM sys.sysprocesses WHERE dbid=DB_ID() AND spid<>@@SPID AND spid>50")==0,"Unexpected QA sessions.");
                var accounts=new List<string>();using(var q=new SqlCommand("SELECT TOP 3 AccountCode FROM dbo.AccountsTb WHERE IsActive=1 ORDER BY AccountCode",c))using(var r=q.ExecuteReader())while(r.Read())accounts.Add(r.GetString(0));
                Check(accounts.Count==3,"Three active QA accounts required.");string currency;using(var q=new SqlCommand("SELECT CurrencyCode FROM dbo.CurrencySettingsTb WHERE IsSystemCurrency=1",c))using(var r=q.ExecuteReader()){Check(r.Read(),"System currency absent.");currency=r.GetString(0);Check(!r.Read(),"System currency ambiguous.");}
                int center=Scalar(c,null,"SELECT ISNULL(MIN(CostCenterId),0) FROM dbo.CostCentersTb WHERE IsActive=1");Check(center>0,"Positive active QA cost center required.");
                var actor=new OperationActor{UserName="QA-VoucherRequest-"+token,UserRole="Manager"};var date=new DateTime(1900,1,1,12,30,45).AddMilliseconds(2);
                Func<Guid,string,decimal,string,string,int?,string,VoucherPostRequest> request=(id,type,amount,party,notes,cost,code)=>new VoucherPostRequest(type,date,party,accounts[1],amount,notes,accounts[0],cost,code,id);
                var prepared=VoucherPreparation.Prepare("ReceiptExternal","طرف عربي Cash 500 mg",accounts[1],accounts[0],10.005m,"ملاحظة &",center,currency);
                string initial=Snapshot(c);
                if(missingSchemaOnly)
                {
                    using(var tx=c.BeginTransaction())
                    {
                        try
                        {
                            if(Scalar(c,tx,"SELECT COUNT(*) FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.AccountingVouchersTb') AND name=N'UX_AccountingVouchersTb_RequestId'")>0)Exec(c,tx,"DROP INDEX UX_AccountingVouchersTb_RequestId ON dbo.AccountingVouchersTb");
                            bool rejected=false;try{SqlVoucherPostRepository.PostInTransaction(c,tx,date,prepared,actor,Guid.NewGuid());}catch(SqlException error){if(error.Number!=207&&error.Number!=308)throw;rejected=true;}
                            Check(rejected,"Missing migration did not refuse durable posting.");
                        }
                        finally{tx.Rollback();}
                    }
                    Check(Snapshot(c)==initial,"Missing schema failure changed old data.");Console.WriteLine("PASS durable voucher missing-schema refusal; no ledger/voucher/audit change.");return;
                }
                var service=new VoucherPostService(new SqlVoucherPostRepository(factory),a=>a!=null&&a.UserRole=="Manager");
                int countBefore=Scalar(c,null,"SELECT COUNT(*) FROM dbo.AccountingVouchersTb"),journalBefore=Scalar(c,null,"SELECT COUNT(*) FROM dbo.JournalEntriesTb"),auditBefore=Scalar(c,null,"SELECT COUNT(*) FROM dbo.AuditLogTb");
                var identities=new List<Guid>();var accepted=new List<VoucherPostResult>();
                try
                {
                    foreach(string type in new[]{"ReceiptExternal","ReceiptInternal","ReceiptMulti","PaymentExternal","PaymentInternal","PaymentMulti"})
                    {
                        var id=Guid.NewGuid();identities.Add(id);var value=request(id,type,type.StartsWith("Receipt")?10.005m:10.015m,"  طرف عربي Cash 500 mg  ","  ملاحظة &  ",center,currency);
                        var result=service.Post(value,actor);accepted.Add(result);
                        var statusService=new VoucherSubmissionStatusService(new SqlVoucherSubmissionStatusRepository(factory),a=>a!=null&&a.UserRole=="Manager");
                        var status=statusService.FindAsync(new VoucherSubmissionStatusRequest(value),actor,CancellationToken.None).GetAwaiter().GetResult();
                        Check(status.IsPosted&&status.VoucherId==result.VoucherId&&status.JournalEntryId==result.JournalEntryId&&status.ContentMatches==true,"Status did not prove exact accepted payload.");
                        var changed=request(id,type,value.Amount,"changed party",value.Notes,center,currency);
                        Check(statusService.FindAsync(new VoucherSubmissionStatusRequest(changed),actor,CancellationToken.None).GetAwaiter().GetResult().ContentMatches==false,"Changed header falsely matched.");
                        changed=request(id,type,value.Amount,value.PartyName,value.Notes,null,currency);
                        Check(statusService.FindAsync(new VoucherSubmissionStatusRequest(changed),actor,CancellationToken.None).GetAwaiter().GetResult().ContentMatches==false,"Changed center falsely matched.");
                        bool denied=false;try{statusService.FindAsync(new VoucherSubmissionStatusRequest(value),new OperationActor{UserName="other-owner",UserRole="Manager"},CancellationToken.None).GetAwaiter().GetResult();}catch(UnauthorizedAccessException){denied=true;}Check(denied,"Status exposed another owner's voucher.");
                        Check(!statusService.FindAsync(new VoucherSubmissionStatusRequest(request(Guid.NewGuid(),type,value.Amount,value.PartyName,value.Notes,center,currency)),actor,CancellationToken.None).GetAwaiter().GetResult().IsPosted,"Absent identity falsely accepted.");
                        Check(!result.AlreadyPosted&&result.VoucherId>0&&result.JournalEntryId>0,"Initial acceptance invalid.");
                        var repeat=service.Post(value,actor);Check(repeat.AlreadyPosted&&repeat.VoucherId==result.VoucherId&&repeat.JournalEntryId==result.JournalEntryId,"Retry created a different voucher.");
                        using(var q=new SqlCommand("SELECT RequestId,RequestCurrencyCode,Amount,VoucherDate FROM dbo.AccountingVouchersTb WHERE VoucherId=@Id",c)){q.Parameters.Add("@Id",SqlDbType.Int).Value=result.VoucherId;using(var r=q.ExecuteReader())Check(r.Read()&&r.GetGuid(0)==id&&r.GetString(1)==currency.Trim().ToUpperInvariant()&&r.GetDecimal(2)==(type.StartsWith("Receipt")?10m:10.02m)&&r.GetDateTime(3)==new SqlDateTime(date).Value,"Saved identity/currency/rounding/date differs.");}
                        Throws<VoucherAcceptedRequestConflictException>(()=>service.Post(request(id,type,value.Amount,"طرف مختلف",value.Notes,center,currency),actor));
                        Throws<VoucherAcceptedRequestConflictException>(()=>service.Post(request(id,type,value.Amount+1,value.PartyName,value.Notes,center,currency),actor));
                        Throws<VoucherAcceptedRequestConflictException>(()=>service.Post(request(id,type,value.Amount,value.PartyName,"بيان مختلف",center,currency),actor));
                        Throws<VoucherAcceptedRequestConflictException>(()=>service.Post(request(id,type,value.Amount,value.PartyName,value.Notes,null,currency),actor));
                        Throws<VoucherAcceptedRequestConflictException>(()=>service.Post(request(id,type,value.Amount,value.PartyName,value.Notes,center,currency=="USD"?"YER":"USD"),actor));
                        Throws<VoucherAcceptedRequestConflictException>(()=>service.Post(new VoucherPostRequest(type,date,value.PartyName,accounts[1],value.Amount,value.Notes,accounts[2],center,currency,id),actor));
                        Throws<UnauthorizedAccessException>(()=>service.Post(value,new OperationActor{UserName="OtherOwner-"+token,UserRole="Manager"}));
                    }
                    Check(Scalar(c,null,"SELECT COUNT(*) FROM dbo.AccountingVouchersTb")==countBefore+6&&Scalar(c,null,"SELECT COUNT(*) FROM dbo.JournalEntriesTb")==journalBefore+6&&Scalar(c,null,"SELECT COUNT(*) FROM dbo.AuditLogTb")==auditBefore+6,"Replays/conflicts/ownership created additional records.");
                    var race=Guid.NewGuid();identities.Add(race);VoucherPostResult first;Task<VoucherPostResult> second=null;
                    using(var blocker=factory())
                    {
                        blocker.Open();int spid=Scalar(blocker,null,"SELECT @@SPID");using(var tx=blocker.BeginTransaction())
                        {
                            try
                            {
                                first=SqlVoucherPostRepository.PostInTransaction(blocker,tx,date,prepared,actor,race);accepted.Add(first);
                                var raceRequest=request(race,"ReceiptExternal",10.005m,"طرف عربي Cash 500 mg","ملاحظة &",center,currency);second=Task.Run(()=>service.Post(raceRequest,actor));
                                bool observed=false;DateTime deadline=DateTime.UtcNow.AddSeconds(10);
                                while(DateTime.UtcNow<deadline&&!second.IsCompleted){using(var q=new SqlCommand("SELECT COUNT(*) FROM sys.dm_exec_requests WHERE database_id=DB_ID() AND blocking_session_id=@Id AND wait_type LIKE N'LCK%';",c)){q.Parameters.Add("@Id",SqlDbType.Int).Value=spid;observed=Convert.ToInt32(q.ExecuteScalar())>0;}if(observed)break;Thread.Sleep(25);}
                                Check(observed&&!second.IsCompleted,"Concurrent request was not observed waiting on the first transaction.");Console.WriteLine("PASS voucher request blocking chain observed; blocker_spid="+spid);tx.Commit();
                            }
                            catch{try{tx.Rollback();}catch{};if(second!=null)try{second.Wait(10000);}catch{};throw;}
                        }
                    }
                    Check(second.Wait(10000),"Concurrent retry timed out.");Check(second.Result.AlreadyPosted&&second.Result.VoucherId==first.VoucherId&&second.Result.JournalEntryId==first.JournalEntryId,"Concurrent identity differs.");
                    using(var tx=c.BeginTransaction())
                    {
                        try
                        {
                            Exec(c,tx,"UPDATE dbo.CurrencySettingsTb SET IsSystemCurrency=0;");
                            var replay=SqlVoucherPostRepository.PostInTransaction(c,tx,date,prepared,actor,race);Check(replay.AlreadyPosted&&replay.VoucherId==first.VoucherId,"Currency change rejected accepted retry.");
                            Throws<InvalidOperationException>(()=>SqlVoucherPostRepository.PostInTransaction(c,tx,date,prepared,actor,Guid.NewGuid()));
                        }
                        finally{tx.Rollback();}
                    }
                    using(var tx=c.BeginTransaction())
                    {
                        try
                        {
                            using(var q=new SqlCommand("INSERT dbo.AccountingPeriodsTb(PeriodId,StartDate,EndDate,Status,ClosedAtUtc,ClosedBy,Notes) VALUES(@Id,@Date,@Date,N'Closed',SYSUTCDATETIME(),@User,N'owned voucher request QA')",c,tx)){q.Parameters.Add("@Id",SqlDbType.UniqueIdentifier).Value=Guid.NewGuid();q.Parameters.Add("@Date",SqlDbType.Date).Value=date.Date;q.Parameters.Add("@User",SqlDbType.NVarChar,200).Value=actor.UserName;q.ExecuteNonQuery();}
                            Check(SqlVoucherPostRepository.PostInTransaction(c,tx,date,prepared,actor,race).AlreadyPosted,"Closed period rejected known acceptance.");
                            Throws<InvalidOperationException>(()=>SqlVoucherPostRepository.PostInTransaction(c,tx,date,prepared,actor,Guid.NewGuid()));
                        }
                        finally{tx.Rollback();}
                    }
                    using(var tx=c.BeginTransaction())
                    {
                        try
                        {
                            Exec(c,tx,"CREATE TRIGGER dbo.trg_QAVoucherRequestAudit_"+token+" ON dbo.AuditLogTb AFTER INSERT AS BEGIN THROW 51088,N'controlled voucher audit refusal',1; END");
                            bool refused=false;try{SqlVoucherPostRepository.PostInTransaction(c,tx,date,prepared,actor,Guid.NewGuid());}catch(SqlException error){if(error.Number!=51088)throw;refused=true;}Check(refused,"Audit failure was not propagated.");
                        }
                        finally{try{tx.Rollback();}catch(InvalidOperationException){};}
                    }
                    Check(Scalar(c,null,"SELECT COUNT(*) FROM dbo.AccountingVouchersTb")==countBefore+7&&Scalar(c,null,"SELECT COUNT(*) FROM dbo.JournalEntriesTb")==journalBefore+7&&Scalar(c,null,"SELECT COUNT(*) FROM dbo.AuditLogTb")==auditBefore+7,"Failure or concurrency duplicated records.");
                    var lostResponseId=Guid.NewGuid();identities.Add(lostResponseId);
                    var recoveryRequest=request(lostResponseId,"PaymentMulti",15.015m,"طرف استعادة SQL 500 mg","بعد commit دون رد",center,currency);
                    VoucherOwnedRecoverySqlTests.Verify(database,token,recoveryRequest,System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"..","..","..","..","artifacts","voucher-sql-recovery-"+token));
                    Check(Scalar(c,null,"SELECT COUNT(*) FROM dbo.AccountingVouchersTb")==countBefore+8&&Scalar(c,null,"SELECT COUNT(*) FROM dbo.JournalEntriesTb")==journalBefore+8&&Scalar(c,null,"SELECT COUNT(*) FROM dbo.AuditLogTb")==auditBefore+8,"Independent recovery created additional financial rows.");
                    var correctionId=Guid.NewGuid();identities.Add(correctionId);
                    VoucherOwnedRecoverySqlTests.VerifyCorrection(database,token,request(correctionId,"PaymentInternal",21.015m,"طرف تصحيح","تراجع مثبت",center,currency));
                    Check(Scalar(c,null,"SELECT COUNT(*) FROM dbo.AccountingVouchersTb")==countBefore+9&&Scalar(c,null,"SELECT COUNT(*) FROM dbo.JournalEntriesTb")==journalBefore+9&&Scalar(c,null,"SELECT COUNT(*) FROM dbo.AuditLogTb")==auditBefore+9,"Correction failure or retry added extra financial records.");
                    using(var q=new SqlCommand("UPDATE dbo.JournalEntriesTb SET SourceType=N'QA-corrupt-link' WHERE JournalEntryId=@Id AND CreatedBy=@User",c)){q.Parameters.Add("@Id",SqlDbType.Int).Value=accepted[0].JournalEntryId;q.Parameters.Add("@User",SqlDbType.NVarChar,200).Value=actor.UserName;Check(q.ExecuteNonQuery()==1,"Owned corruption fixture missing.");}
                    try
                    {
                        bool classified=false;try{service.Post(request(identities[0],"ReceiptExternal",10.005m,"  طرف عربي Cash 500 mg  ","  ملاحظة &  ",center,currency),actor);}catch(InvalidOperationException error){Check(!(error is VoucherPostRejectedException),"Existing accepted link failure allowed correction.");classified=true;}Check(classified,"Existing corrupt link not refused.");
                    }
                    finally{using(var q=new SqlCommand("UPDATE dbo.JournalEntriesTb SET SourceType=N'Voucher' WHERE JournalEntryId=@Id AND CreatedBy=@User",c)){q.Parameters.Add("@Id",SqlDbType.Int).Value=accepted[0].JournalEntryId;q.Parameters.Add("@User",SqlDbType.NVarChar,200).Value=actor.UserName;q.ExecuteNonQuery();}}
                }
                finally
                {
                    // Only documents whose GUID and creator belong to this run; never remove unrelated rows.
                    using(var tx=c.BeginTransaction())
                    {
                        foreach(Guid id in identities)using(var q=new SqlCommand(@"DECLARE @Journal int,@Voucher int; SELECT @Journal=JournalEntryId,@Voucher=VoucherId FROM dbo.AccountingVouchersTb WHERE RequestId=@Request AND CreatedBy=@User;
IF @Voucher IS NOT NULL BEGIN DELETE dbo.JournalEntryLinesTb WHERE JournalEntryId=@Journal; DELETE dbo.AccountingVouchersTb WHERE VoucherId=@Voucher AND RequestId=@Request AND CreatedBy=@User; DELETE dbo.JournalEntriesTb WHERE JournalEntryId=@Journal AND CreatedBy=@User AND SourceType=N'Voucher' AND SourceId=CONVERT(nvarchar(50),@Voucher); END;",c,tx)){q.Parameters.Add("@Request",SqlDbType.UniqueIdentifier).Value=id;q.Parameters.Add("@User",SqlDbType.NVarChar,200).Value=actor.UserName;q.ExecuteNonQuery();}
                        using(var q=new SqlCommand("DELETE dbo.AuditLogTb WHERE UserName=@User AND UserRole=N'Manager' AND ActionName=N'AccountingVoucherCreated' AND EntityName=N'AccountingVouchers'",c,tx)){q.Parameters.Add("@User",SqlDbType.NVarChar,200).Value=actor.UserName;q.ExecuteNonQuery();}tx.Commit();
                    }
                }
                Check(Snapshot(c)==initial,"Old voucher/journal/audit/period/currency/center data changed after owned cleanup.");Console.WriteLine("PASS durable voucher SQL: "+checks+" checks, six types, exact retry, changed payload/owner refusal, positive center, two-session lock wait, currency/period acceptance, audit rollback; old rows hash="+initial);
            }
        }
    }
}
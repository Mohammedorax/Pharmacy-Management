using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using PharmacyManagement.Application;
using PharmacyManagement.Domain;
using PharmacyManagement.Infrastructure.SqlServer;

namespace PharmacyManagement.CoreTests
{
    internal static class JournalPostingSqlTests
    {
        private static int Scalar(SqlConnection connection, SqlTransaction transaction, string sql)
        { using (var command = new SqlCommand(sql, connection, transaction)) return Convert.ToInt32(command.ExecuteScalar()); }
        private static int[] Counts(SqlConnection connection, SqlTransaction transaction)
            => new[] { Scalar(connection, transaction, "SELECT COUNT(*) FROM dbo.JournalEntriesTb"),
                Scalar(connection, transaction, "SELECT COUNT(*) FROM dbo.JournalEntryLinesTb"),
                Scalar(connection, transaction, "SELECT COUNT(*) FROM dbo.AuditLogTb"),
                Scalar(connection, transaction, "SELECT COUNT(*) FROM dbo.AccountingPeriodsTb") };
        private static void Equal(int[] expected, int[] actual)
        { for (int i = 0; i < expected.Length; i++) if (expected[i] != actual[i]) throw new Exception("QA rollback left changed row counts."); }
        private static JournalPostRequest Request(DateTime date, string source, string debit, string credit)
            => new JournalPostRequest(date, "QA.JournalPosting", source, "اختبار تراجع محاسبي QA", new[] {
                new JournalLineValues(debit,10.005m,0,"بيان Cash 500 mg",null),new JournalLineValues(credit,0,10.005m,null,null) });
        internal static void VerifyTemporaryQaCommit(string server,string database,string token)
        {
            Guid owner;
            if(server!="."||!Guid.TryParseExact(token,"N",out owner)||
                !System.Text.RegularExpressions.Regex.IsMatch(database,"^PharmacyRestore_[0-9]{14}_"+token+"_qa$"))
                throw new UnauthorizedAccessException("Committed tests require the owned temporary QA restore target.");
            var builder=new SqlConnectionStringBuilder {DataSource=server,InitialCatalog=database,IntegratedSecurity=true,
                Encrypt=true,TrustServerCertificate=true,Pooling=false,ConnectTimeout=10};
            Func<SqlConnection> connectionFactory=()=>new SqlConnection(builder.ConnectionString);
            using(var connection=connectionFactory())
            {
                connection.Open();
                using(var command=connection.CreateCommand())
                {
                    command.CommandText="SELECT DB_NAME(),CONVERT(nvarchar(32),value) FROM sys.extended_properties WHERE class=0 AND name=N'QaTest.Owner'";
                    using(var reader=command.ExecuteReader())
                        if(!reader.Read()||reader.GetString(0)!=database||reader.GetString(1)!=token||reader.Read())
                            throw new UnauthorizedAccessException("Temporary QA database ownership not verified.");
                    command.CommandText="SELECT COUNT(*) FROM sys.sysprocesses WHERE dbid=DB_ID() AND spid<>@@SPID AND spid>50";
                    if(Convert.ToInt32(command.ExecuteScalar())!=0)throw new UnauthorizedAccessException("Unexpected temporary QA sessions.");
                }
                var codes=new List<string>();
                using(var command=new SqlCommand("SELECT TOP 2 AccountCode FROM dbo.AccountsTb WHERE IsActive=1 ORDER BY AccountCode",connection))
                using(var reader=command.ExecuteReader())while(reader.Read())codes.Add(reader.GetString(0));
                if(codes.Count!=2)throw new Exception("Two active restored QA accounts required.");
                int vouchersBefore=Scalar(connection,null,"SELECT COUNT(*) FROM dbo.AccountingVouchersTb");
                var before=Counts(connection,null);
                var actor=new OperationActor {UserName="QA-Commit-"+token,UserRole="Manager"};
                Func<OperationActor,bool> authorized=a=>a.UserName==actor.UserName&&a.UserRole=="Manager";
                var service=new JournalPostService(new SqlJournalPostRepository(connectionFactory),authorized);
                var date=new DateTime(1900,1,1,12,30,45);
                var request=Request(date,token+"-commit",codes[0],codes[1]);
                var accepted=service.Post(request,actor);
                if(accepted.Id<=0||accepted.AlreadyPosted)throw new Exception("Public journal Commit did not return a new identity.");
                // A retry with the same immutable source models a missing acknowledgement;
                // it is not a network-disconnection injection.
                var retried=service.Post(request,actor);
                if(retried.Id!=accepted.Id||!retried.AlreadyPosted)throw new Exception("Committed source retry created a second journal.");
                using(var ready=new System.Threading.CountdownEvent(2))
                using(var start=new System.Threading.ManualResetEventSlim(false))
                {
                    Func<SqlConnection> racingConnection=()=> {ready.Signal();if(!start.Wait(TimeSpan.FromSeconds(15)))throw new TimeoutException("Race release timed out.");return connectionFactory();};
                    var racingService=new JournalPostService(new SqlJournalPostRepository(racingConnection),authorized);
                    var racingRequest=Request(date,token+"-race",codes[0],codes[1]);
                    var first=System.Threading.Tasks.Task.Run(()=>racingService.Post(racingRequest,actor));
                    var second=System.Threading.Tasks.Task.Run(()=>racingService.Post(racingRequest,actor));
                    if(!ready.Wait(TimeSpan.FromSeconds(10)))throw new TimeoutException("Both posting requests did not reach the connection barrier.");
                    start.Set();
                    if(!System.Threading.Tasks.Task.WaitAll(new System.Threading.Tasks.Task[]{first,second},TimeSpan.FromSeconds(30)))throw new TimeoutException("Concurrent posting did not complete.");
                    if(first.Result.Id!=second.Result.Id||first.Result.AlreadyPosted==second.Result.AlreadyPosted)
                        throw new Exception("Concurrent same-source posts did not converge on one committed journal.");
                }
                var voucherService=new VoucherPostService(new SqlVoucherPostRepository(connectionFactory),authorized);
                foreach(var type in new[]{"ReceiptExternal","ReceiptInternal","ReceiptMulti","PaymentExternal","PaymentInternal","PaymentMulti"})
                {
                    decimal amount=type.StartsWith("Receipt")?10.005m:10.015m;
                    var result=voucherService.Post(new VoucherPostRequest(type,date,"QA عميل Cash",codes[1],amount,"اختبار اعتماد",codes[0],null),actor);
                    using(var command=new SqlCommand("SELECT Amount,JournalEntryId FROM dbo.AccountingVouchersTb WHERE VoucherId=@Id",connection))
                    {
                        command.Parameters.Add("@Id",SqlDbType.Int).Value=result.VoucherId;
                        using(var reader=command.ExecuteReader())
                            if(!reader.Read()||reader.GetDecimal(0)!=(type.StartsWith("Receipt")?10m:10.02m)||reader.GetInt32(1)!=result.JournalEntryId||reader.Read())
                                throw new Exception("Public voucher Commit was not visible from the independent verification connection.");
                    }
                }
                var expected=(int[])before.Clone();expected[0]+=8;expected[1]+=16;expected[2]+=8;
                Equal(expected,Counts(connection,null));
                if(Scalar(connection,null,"SELECT COUNT(*) FROM dbo.AccountingVouchersTb")!=vouchersBefore+6)
                    throw new Exception("The six committed voucher variants changed an unexpected number of rows.");
                using(var command=new SqlCommand("SELECT COUNT(*),SUM(l.Debit)-SUM(l.Credit) FROM dbo.JournalEntriesTb j JOIN dbo.JournalEntryLinesTb l ON l.JournalEntryId=j.JournalEntryId WHERE j.CreatedBy=@Actor",connection))
                {
                    command.Parameters.Add("@Actor",SqlDbType.NVarChar,200).Value=actor.UserName;
                    using(var reader=command.ExecuteReader())
                        if(!reader.Read()||reader.GetInt32(0)!=16||reader.GetDecimal(1)!=0m)
                            throw new Exception("Committed accounting rows are missing or unbalanced.");
                }
                Console.WriteLine("PASS: owned temporary QA restore; public Commit persisted 2 journals and 6 vouchers with 16 balanced lines and 8 audit records; committed retry and two simultaneous SQL requests converged on one source. Temporary records are deleted with the owned database by the orchestrating script; original QA is unchanged. Voucher lost-ack retry and two physical devices remain separate gates.");
            }
        }
        internal static void VerifyQaRollback(string server, string database, string backupPath)
        {
            using (var connection = QaAccountingWriteFixture.Open(server, database, backupPath))
            {
                var builder = new SqlConnectionStringBuilder(connection.ConnectionString);
                var codes=new List<string>();
                using (var command=new SqlCommand("SELECT TOP 2 AccountCode FROM dbo.AccountsTb WHERE IsActive=1 ORDER BY AccountCode",connection))
                using (var reader=command.ExecuteReader()) while(reader.Read()) codes.Add(reader.GetString(0));
                if(codes.Count!=2) throw new Exception("Two active QA accounts are required.");
                string token=Guid.NewGuid().ToString("N"), missing="QA_MISSING_"+token.Substring(0,8);
                using (var command=new SqlCommand("SELECT COUNT(*) FROM dbo.AccountsTb WHERE AccountCode=@Code",connection))
                { command.Parameters.Add("@Code",SqlDbType.NVarChar,20).Value=missing; if(Convert.ToInt32(command.ExecuteScalar())!=0) throw new Exception("Missing account fixture is not unique."); }
                var actor=new OperationActor {UserName="QA-Journal-"+token,UserRole="Manager"};
                var date=new DateTime(1900,1,1,12,30,45); var baseline=Counts(connection,null);
                int accepted;
                using(var transaction=connection.BeginTransaction())
                {
                    try
                    {
                        SqlBusinessPeriodPolicy.EnsureBusinessDateOpen(connection,transaction,date);
                        var request=Request(date,token,codes[0],codes[1]);
                        var result=SqlJournalPostRepository.PostInTransaction(connection,transaction,request,actor); accepted=result.Id;
                        if(accepted<=0||result.AlreadyPosted) throw new Exception("QA journal was not newly accepted.");
                        var current=Counts(connection,transaction);
                        if(current[0]!=baseline[0]+1||current[1]!=baseline[1]+2||current[2]!=baseline[2]+1) throw new Exception("Journal/lines/audit were not written together.");
                        using(var command=new SqlCommand("SELECT EntryDate,SourceType,SourceId,Description,CreatedBy FROM dbo.JournalEntriesTb WHERE JournalEntryId=@Id",connection,transaction))
                        {
                            command.Parameters.Add("@Id",SqlDbType.Int).Value=accepted;
                            using(var reader=command.ExecuteReader())
                                if(!reader.Read()||reader.GetDateTime(0)!=date||reader.GetString(1)!="QA.JournalPosting"||reader.GetString(2)!=token||reader.GetString(3)!=request.Description||reader.GetString(4)!=actor.UserName)
                                    throw new Exception("QA journal header date, source, description or actor differs.");
                        }
                        using(var command=new SqlCommand("SELECT AccountCode,Debit,Credit,LineDescription FROM dbo.JournalEntryLinesTb WHERE JournalEntryId=@Id ORDER BY JournalEntryLineId",connection,transaction))
                        {
                            command.Parameters.Add("@Id",SqlDbType.Int).Value=accepted;
                            using(var reader=command.ExecuteReader())
                            {
                                if(!reader.Read()||reader.GetString(0)!=codes[0]||reader.GetDecimal(1)!=10m||reader.GetDecimal(2)!=0m||reader.GetString(3)!="بيان Cash 500 mg") throw new Exception("QA debit mapping or rounding differs.");
                                if(!reader.Read()||reader.GetString(0)!=codes[1]||reader.GetDecimal(1)!=0m||reader.GetDecimal(2)!=10m||!reader.IsDBNull(3)||reader.Read()) throw new Exception("QA credit mapping or null differs.");
                            }
                        }
                        using(var command=new SqlCommand("INSERT INTO dbo.AccountingPeriodsTb(PeriodId,StartDate,EndDate,Status,ClosedAtUtc,ClosedBy,Notes) VALUES(@Id,@Date,@Date,N'Closed',SYSUTCDATETIME(),@User,N'QA rollback fixture')",connection,transaction))
                        { command.Parameters.Add("@Id",SqlDbType.UniqueIdentifier).Value=Guid.NewGuid();command.Parameters.Add("@Date",SqlDbType.Date).Value=date.Date;command.Parameters.Add("@User",SqlDbType.NVarChar,200).Value=actor.UserName;command.ExecuteNonQuery(); }
                        var retry=SqlJournalPostRepository.PostInTransaction(connection,transaction,request,actor);
                        if(retry.Id!=accepted||!retry.AlreadyPosted) throw new Exception("Existing source was not returned through a now-closed period.");
                        current=Counts(connection,transaction);
                        if(current[0]!=baseline[0]+1||current[1]!=baseline[1]+2||current[2]!=baseline[2]+1) throw new Exception("Retry duplicated a journal or audit.");
                        bool refused=false;
                        try { SqlJournalPostRepository.PostInTransaction(connection,transaction,Request(date,token+"-closed",codes[0],codes[1]),actor); }
                        catch(InvalidOperationException) {refused=true;}
                        if(!refused) throw new Exception("A new journal crossed the closed period.");
                        current=Counts(connection,transaction);
                        if(current[0]!=baseline[0]+1||current[1]!=baseline[1]+2||current[2]!=baseline[2]+1) throw new Exception("Closed-period rejection changed the saved journal or audit.");
                    }
                    finally {transaction.Rollback();}
                }
                Equal(baseline,Counts(connection,null));
                bool partialRefused=false;
                var service=new JournalPostService(new SqlJournalPostRepository(()=>new SqlConnection(builder.ConnectionString)), _=>true);
                try {service.Post(Request(date,token+"-missing",codes[0],missing),actor);}
                catch(InvalidOperationException) {partialRefused=true;}
                if(!partialRefused) throw new Exception("The owning repository did not reject and roll back after the first line.");
                Equal(baseline,Counts(connection,null));
                string trigger="QaJournalAudit_"+token;
                using(var transaction=connection.BeginTransaction())
                {
                    try
                    {
                        // This trigger exists only in the rollback transaction and targets this unique test actor.
                        using(var command=new SqlCommand("CREATE TRIGGER dbo.["+trigger+"] ON dbo.AuditLogTb AFTER INSERT AS BEGIN IF EXISTS(SELECT 1 FROM inserted WHERE UserName=N'"+actor.UserName+"') THROW 51010,N'QA audit failure fixture',1; END",connection,transaction)) command.ExecuteNonQuery();
                        bool failed=false;
                        try {SqlJournalPostRepository.PostInTransaction(connection,transaction,Request(date,token+"-audit",codes[0],codes[1]),actor);}
                        catch(SqlException error) {if(error.Number!=51010)throw;failed=true;}
                        if(!failed)throw new Exception("Audit failure did not fail the posting transaction.");
                    }
                    finally {transaction.Rollback();}
                }
                Equal(baseline,Counts(connection,null));
                if(Scalar(connection,null,"SELECT COUNT(*) FROM sys.triggers WHERE name=N'"+trigger+"'")!=0)throw new Exception("QA trigger survived rollback.");
                using(var transaction=connection.BeginTransaction())
                {
                    try
                    {
                        const decimal maximum=9999999999999999.99m;
                        var invalid=new JournalPostRequest(date,"QA.JournalPosting",token+"-oversize","QA storage bound",new[] {
                            new JournalLineValues(codes[0],maximum+0.01m,0,null,null),
                            new JournalLineValues(codes[1],0,maximum+0.01m,null,null) });
                        bool refused=false;
                        try {SqlJournalPostRepository.PostInTransaction(connection,transaction,invalid,actor);}
                        catch(JournalAmountException) {refused=true;}
                        if(!refused)throw new Exception("SQL posting path accepted an amount beyond DECIMAL(18,2).");
                        Equal(baseline,Counts(connection,transaction));
                        var boundary=new JournalPostRequest(date,"QA.JournalPosting",token+"-maximum","QA exact storage bound",new[] {
                            new JournalLineValues(codes[0],maximum,0,null,null),new JournalLineValues(codes[1],0,maximum,null,null) });
                        var posted=SqlJournalPostRepository.PostInTransaction(connection,transaction,boundary,actor);
                        using(var command=new SqlCommand("SELECT Debit,Credit FROM dbo.JournalEntryLinesTb WHERE JournalEntryId=@Id ORDER BY JournalEntryLineId",connection,transaction))
                        {
                            command.Parameters.Add("@Id",SqlDbType.Int).Value=posted.Id;
                            using(var reader=command.ExecuteReader())
                                if(!reader.Read()||reader.GetDecimal(0)!=maximum||reader.GetDecimal(1)!=0||
                                    !reader.Read()||reader.GetDecimal(0)!=0||reader.GetDecimal(1)!=maximum||reader.Read())
                                    throw new Exception("The exact DECIMAL(18,2) boundary was changed by SQL storage.");
                        }
                        var expected=(int[])baseline.Clone();expected[0]++;expected[1]+=2;expected[2]++;
                        Equal(expected,Counts(connection,transaction));
                    }
                    finally {transaction.Rollback();}
                }
                Equal(baseline,Counts(connection,null));
                Console.WriteLine("PASS: actual SQL DECIMAL(18,2) maximum stored exactly; oversized posting rejected before row writes; boundary transaction rolled back.");
                Console.WriteLine("PASS: QA journal/lines/audit, rounding, retry through closed period, closed-new rejection, partial-line and audit failure rollbacks; PersistentRowWrites=0. Identity counters may advance; public Post commit and two-device concurrency are separate gates.");
            }
        }
    }
}

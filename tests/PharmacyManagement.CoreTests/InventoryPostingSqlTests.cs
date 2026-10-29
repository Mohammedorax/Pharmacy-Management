using System;
using System.Data;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Application;
using PharmacyManagement.Infrastructure.SqlServer;

namespace PharmacyManagement.CoreTests
{
    internal static class InventoryPostingSqlTests
    {
        private static int Scalar(SqlConnection connection,string sql)
        {using(var command=new SqlCommand(sql,connection))return Convert.ToInt32(command.ExecuteScalar());}
        private static int Quantity(SqlConnection connection,string medicine)
        {using(var command=new SqlCommand("SELECT MedQty FROM dbo.MedicineTb1 WHERE MedName=@Name",connection)){command.Parameters.Add("@Name",SqlDbType.NVarChar,200).Value=medicine;return Convert.ToInt32(command.ExecuteScalar());}}
        private static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
        private static void Rejected(Action action)
        {try{action();}catch(InvalidOperationException){return;}throw new Exception("Changed same-source inventory request was accepted.");}
        internal static void VerifyTemporaryQaCommit(string server,string database,string token)
        {
            Guid owner;
            if(server!="."||!Guid.TryParseExact(token,"N",out owner)||!System.Text.RegularExpressions.Regex.IsMatch(database,"^PharmacyRestore_[0-9]{14}_"+token+"_qa$"))
                throw new UnauthorizedAccessException("Inventory Commit tests require the owned temporary QA restore.");
            var builder=new SqlConnectionStringBuilder{DataSource=server,InitialCatalog=database,IntegratedSecurity=true,Encrypt=true,TrustServerCertificate=true,Pooling=false,ConnectTimeout=10};
            Func<SqlConnection> connections=()=>new SqlConnection(builder.ConnectionString);
            using(var verify=connections())
            {
                verify.Open();
                using(var command=verify.CreateCommand())
                {
                    command.CommandText="SELECT DB_NAME(),CONVERT(nvarchar(32),value) FROM sys.extended_properties WHERE class=0 AND name=N'QaTest.Owner'";
                    using(var reader=command.ExecuteReader())Check(reader.Read()&&reader.GetString(0)==database&&reader.GetString(1)==token&&!reader.Read(),"Temporary QA owner marker differs.");
                }
                Check(Scalar(verify,"SELECT COUNT(*) FROM sys.sysprocesses WHERE dbid=DB_ID() AND spid<>@@SPID AND spid>50")==0,"Unexpected temporary QA session.");
                Check(!SqlInventoryPostRepository.IsFullCommercialModeRequested(verify,null),"This test requires aggregate inventory QA mode.");
                string medicine;int before;
                using(var command=new SqlCommand("SELECT TOP(1) MedName,MedQty FROM dbo.MedicineTb1 WHERE MedQty>=0 AND MedQty<2147483600 ORDER BY MedName",verify))
                using(var reader=command.ExecuteReader()){Check(reader.Read(),"No valid restored stock fixture.");medicine=reader.GetString(0);before=reader.GetInt32(1);}
                int movementsBefore=Scalar(verify,"SELECT COUNT(*) FROM dbo.InventoryMovementsTb"),auditBefore=Scalar(verify,"SELECT COUNT(*) FROM dbo.AuditLogTb");
                var actor=new OperationActor{UserName="QA-Stock-"+token,UserRole="Manager"};
                Func<OperationActor,bool> authorized=a=>a!=null&&a.UserName==actor.UserName&&a.UserRole==actor.UserRole;
                var service=new InventoryPostService(new SqlInventoryPostRepository(connections),authorized);
                Func<string,InventoryPostKind,int,decimal?,string,InventoryPostRequest> request=(suffix,kind,quantity,amount,notes)=>new InventoryPostRequest(kind,token+"-"+suffix,medicine,quantity,amount,notes);
                var firstRequest=request("first",InventoryPostKind.Receipt,3,10.005m,"توريد عربي 500 mg");
                var first=service.Post(firstRequest,actor);
                Check(Quantity(verify,medicine)==before+3&&first.Before==before&&first.After==before+3&&first.Delta==3&&!first.AlreadyPosted,"Public stock Commit differs from independent SQL read.");
                var recreatedService=new InventoryPostService(new SqlInventoryPostRepository(connections),authorized);
                var retry=recreatedService.Post(firstRequest,actor);
                Check(retry.AlreadyPosted&&retry.Before==first.Before&&retry.After==first.After&&retry.Delta==first.Delta&&Quantity(verify,medicine)==before+3,
                    "A committed inventory retry posted the movement twice.");
                Rejected(()=>service.Post(request("first",InventoryPostKind.Receipt,4,10.005m,firstRequest.Notes),actor));
                Rejected(()=>service.Post(request("first",InventoryPostKind.Issue,3,10.005m,firstRequest.Notes),actor));
                Rejected(()=>service.Post(request("first",InventoryPostKind.Receipt,3,null,firstRequest.Notes),actor));
                Rejected(()=>service.Post(request("first",InventoryPostKind.Receipt,3,10.005m,"changed"),actor));
                Rejected(()=>service.Post(request("first",InventoryPostKind.Receipt,3,12m,firstRequest.Notes),actor));
                Rejected(()=>service.Post(new InventoryPostRequest(firstRequest.Kind,firstRequest.SourceId,"other drug",3,firstRequest.UnitAmount,firstRequest.Notes),actor));
                Rejected(()=>new InventoryPostService(new SqlInventoryPostRepository(connections),_=>true).Post(firstRequest,new OperationActor{UserName="other actor",UserRole="Manager"}));
                service.Post(request("next",InventoryPostKind.Receipt,2,null,"حركة لاحقة"),actor);
                retry=recreatedService.Post(firstRequest,actor);
                Check(retry.After==before+3&&Quantity(verify,medicine)==before+5,"Retry used current stock instead of the committed snapshot.");
                Race(connections,authorized,actor,request("race",InventoryPostKind.Receipt,1,null,"shared race"),request("race",InventoryPostKind.Receipt,1,null,"shared race"),true);
                Check(Quantity(verify,medicine)==before+6,"Concurrent same-source inventory request duplicated stock.");
                Race(connections,authorized,actor,request("race-a",InventoryPostKind.Receipt,2,null,"race a"),request("race-b",InventoryPostKind.Receipt,4,null,"race b"),false);
                Check(Quantity(verify,medicine)==before+12,"Concurrent distinct inventory writes lost an update.");
                foreach(var kind in new[]{InventoryPostKind.OpeningStock,InventoryPostKind.Adjustment,InventoryPostKind.Issue,InventoryPostKind.Receipt})
                {
                    int quantity=kind==InventoryPostKind.OpeningStock?before+20:kind==InventoryPostKind.Adjustment?before+17:3;
                    var input=request(kind.ToString(),kind,quantity,kind==InventoryPostKind.Issue?(decimal?)12.345m:null,"variant "+kind);
                    var saved=service.Post(input,actor);var again=recreatedService.Post(input,actor);
                    Check(!saved.AlreadyPosted&&again.AlreadyPosted&&saved.Before==again.Before&&saved.After==again.After&&saved.Delta==again.Delta,"Inventory variant retry changed its snapshot.");
                }
                Check(Quantity(verify,medicine)==before+17,"Final stock differs after four committed variants.");
                Check(Scalar(verify,"SELECT COUNT(*) FROM dbo.InventoryMovementsTb")==movementsBefore+9&&Scalar(verify,"SELECT COUNT(*) FROM dbo.AuditLogTb")==auditBefore+9,"Unexpected movement or audit count.");
                Guid periodId=Guid.NewGuid();
                using(var command=new SqlCommand(@"INSERT INTO dbo.AccountingPeriodsTb(PeriodId,StartDate,EndDate,Status,ClosedAtUtc,ClosedBy,Notes)
VALUES(@Id,CONVERT(date,GETDATE()),CONVERT(date,GETDATE()),N'Closed',SYSUTCDATETIME(),@Actor,N'Owned QA closed inventory period');",verify))
                {
                    command.Parameters.Add("@Id",SqlDbType.UniqueIdentifier).Value=periodId;
                    command.Parameters.Add("@Actor",SqlDbType.NVarChar,200).Value=actor.UserName;
                    command.ExecuteNonQuery();
                }
                foreach(var kind in new[]{InventoryPostKind.OpeningStock,InventoryPostKind.Adjustment,InventoryPostKind.Issue,InventoryPostKind.Receipt})
                {
                    bool closedRejected=false;
                    try{service.Post(request("c"+(int)kind,kind,1,null,"closed period fixture"),actor);}
                    catch(InventoryPostNotCommittedException error){if(error.SourceId==token+"-c"+(int)kind&&error.Message.Contains("مقفلة"))closedRejected=true;else throw;}
                    Check(closedRejected,"Closed accounting period allowed a stock write: "+kind);
                }
                Check(Quantity(verify,medicine)==before+17&&Scalar(verify,"SELECT COUNT(*) FROM dbo.InventoryMovementsTb")==movementsBefore+9&&Scalar(verify,"SELECT COUNT(*) FROM dbo.AuditLogTb")==auditBefore+9,"Closed-period rejection changed stock, movements or audit.");
                var closedRetry=recreatedService.Post(firstRequest,actor);
                Check(closedRetry.AlreadyPosted&&closedRetry.After==before+3,"A closed period blocked confirmation of an already committed movement.");
                using(var command=new SqlCommand("UPDATE dbo.AccountingPeriodsTb SET Status=N'Open',ClosedAtUtc=NULL,ClosedBy=NULL WHERE PeriodId=@Id",verify))
                {command.Parameters.Add("@Id",SqlDbType.UniqueIdentifier).Value=periodId;Check(command.ExecuteNonQuery()==1,"Could not reopen owned test period.");}
                VerifyPeriodCloseSerialization(connections,verify,periodId,request("period-race",InventoryPostKind.Receipt,1,null,"period lock fixture"),actor);
                Check(Quantity(verify,medicine)==before+17&&Scalar(verify,"SELECT COUNT(*) FROM dbo.InventoryMovementsTb")==movementsBefore+9&&Scalar(verify,"SELECT COUNT(*) FROM dbo.AuditLogTb")==auditBefore+9,"Period close race left an uncommitted stock movement.");
                using(var command=new SqlCommand("UPDATE dbo.AccountingPeriodsTb SET Status=N'Open',ClosedAtUtc=NULL,ClosedBy=NULL WHERE PeriodId=@Id",verify))
                {command.Parameters.Add("@Id",SqlDbType.UniqueIdentifier).Value=periodId;Check(command.ExecuteNonQuery()==1,"Could not reopen period after serialized closure.");}
                string renamedPeriod="QA_Period_"+token;bool renamed=false;
                try
                {
                    using(var command=new SqlCommand("EXEC sys.sp_rename N'dbo.AccountingPeriodsTb',@Name,N'OBJECT'",verify))
                    {command.Parameters.Add("@Name",SqlDbType.NVarChar,128).Value=renamedPeriod;command.ExecuteNonQuery();renamed=true;}
                    bool missingRejected=false;
                    try{service.Post(request("no-period",InventoryPostKind.Receipt,1,null,"missing policy fixture"),actor);}
                    catch(InventoryPostNotCommittedException error){if(error.InnerException is SqlException&&error.Message.Contains("حماية الفترات المحاسبية"))missingRejected=true;else throw;}
                    Check(missingRejected,"A missing accounting period table allowed an inventory write.");
                }
                finally
                {
                    if(renamed)using(var command=new SqlCommand("EXEC sys.sp_rename @Name,N'AccountingPeriodsTb',N'OBJECT'",verify))
                    {command.Parameters.Add("@Name",SqlDbType.NVarChar,128).Value="dbo."+renamedPeriod;command.ExecuteNonQuery();}
                }
                Check(Quantity(verify,medicine)==before+17&&Scalar(verify,"SELECT COUNT(*) FROM dbo.InventoryMovementsTb")==movementsBefore+9&&Scalar(verify,"SELECT COUNT(*) FROM dbo.AuditLogTb")==auditBefore+9,"Missing period policy changed stock, movement or audit.");
                DateTime serverStart;
                using(var command=new SqlCommand("SELECT GETDATE()",verify))serverStart=Convert.ToDateTime(command.ExecuteScalar());
                var inOpenPeriod=service.Post(request("open-period",InventoryPostKind.Receipt,1,null,"open period fixture"),actor);
                using(var command=new SqlCommand("SELECT CreatedAt,GETDATE() FROM dbo.InventoryMovementsTb WHERE SourceId=@Id",verify))
                {
                    command.Parameters.Add("@Id",SqlDbType.NVarChar,50).Value=inOpenPeriod.SourceId;
                    using(var reader=command.ExecuteReader())Check(reader.Read()&&reader.GetDateTime(0)>=serverStart&&reader.GetDateTime(0)<=reader.GetDateTime(1)&&!reader.Read(),"Stored movement clock differs from the SQL local business calendar.");
                }
                Check(!inOpenPeriod.AlreadyPosted&&Quantity(verify,medicine)==before+18,"Reopened period did not allow a new movement.");
                Check(Scalar(verify,"SELECT COUNT(*) FROM dbo.InventoryMovementsTb")==movementsBefore+10&&Scalar(verify,"SELECT COUNT(*) FROM dbo.AuditLogTb")==auditBefore+10,"Open-period movement did not commit exactly once.");
                Console.WriteLine("PASS: four manual inventory modes rejected in a closed server-local business day without stock, movement or audit changes; committed retry remains confirmable; reopened period allows one new committed movement.");

                Console.WriteLine("PASS: owned temporary QA; 10 committed stock movements and audits, four modes, recreated-service retries, changed-source rejection, concurrent same-source deduplication and distinct-source stock locking. No real network disconnect or two physical devices. Temporary database is deleted by the owning script.");
            }
        }
        private static void VerifyPeriodCloseSerialization(Func<SqlConnection> connections,SqlConnection observer,Guid periodId,InventoryPostRequest request,OperationActor actor)
        {
            Task closing=null;var closingSession=new TaskCompletionSource<int>();
            try
            {
                using(var writer=connections())
                {
                    writer.Open();int writerSession=Scalar(writer,"SELECT @@SPID");
                    using(var transaction=writer.BeginTransaction())
                    {
                        try
                        {
                            SqlInventoryPostRepository.PostInTransaction(writer,transaction,request,actor);
                            closing=Task.Run(()=>
                            {
                                try
                                {
                                    using(var connection=connections())
                                    {
                                        connection.Open();closingSession.SetResult(Scalar(connection,"SELECT @@SPID"));
                                        using(var command=new SqlCommand("UPDATE dbo.AccountingPeriodsTb SET Status=N'Closed',ClosedAtUtc=SYSUTCDATETIME(),ClosedBy=@Actor WHERE PeriodId=@Id",connection))
                                        {
                                            command.CommandTimeout=20;
                                            command.Parameters.Add("@Actor",SqlDbType.NVarChar,200).Value=actor.UserName;
                                            command.Parameters.Add("@Id",SqlDbType.UniqueIdentifier).Value=periodId;
                                            Check(command.ExecuteNonQuery()==1,"Concurrent period close did not affect the owned period.");
                                        }
                                    }
                                }
                                catch(Exception error){closingSession.TrySetException(error);throw;}
                            });
                            if(!closingSession.Task.Wait(TimeSpan.FromSeconds(5)))throw new TimeoutException("Period close connection did not start.");
                            int session=closingSession.Task.Result;bool observed=false;var deadline=DateTime.UtcNow.AddSeconds(8);
                            while(DateTime.UtcNow<deadline&&!closing.IsCompleted)
                            {
                                using(var command=new SqlCommand("SELECT COUNT(*) FROM sys.dm_exec_requests WHERE session_id=@Closing AND blocking_session_id=@Writer AND wait_type LIKE N'LCK_M%'",observer))
                                {
                                    command.Parameters.Add("@Closing",SqlDbType.Int).Value=session;
                                    command.Parameters.Add("@Writer",SqlDbType.Int).Value=writerSession;
                                    if(Convert.ToInt32(command.ExecuteScalar())==1){observed=true;break;}
                                }
                                Thread.Sleep(20);
                            }
                            Check(observed,"No actual SQL lock blocked period closing until the inventory transaction finished.");
                        }
                        finally{transaction.Rollback();}
                    }
                }
            }
            finally
            {
                if(closing!=null&&!closing.Wait(TimeSpan.FromSeconds(25)))throw new TimeoutException("Period closing did not finish after inventory rollback.");
            }
            Console.WriteLine("PASS: actual SQL lock wait observed for period closure; closure completed only after the inventory transaction rolled back.");
        }

        private static void Race(Func<SqlConnection> connections,Func<OperationActor,bool> authorized,OperationActor actor,InventoryPostRequest first,InventoryPostRequest second,bool sameSource)
        {
            using(var ready=new CountdownEvent(2))using(var release=new ManualResetEventSlim(false))
            {
                Func<SqlConnection> factory=()=>{ready.Signal();if(!release.Wait(TimeSpan.FromSeconds(15)))throw new TimeoutException("Inventory race release timed out.");return connections();};
                var service=new InventoryPostService(new SqlInventoryPostRepository(factory),authorized);
                var one=Task.Run(()=>service.Post(first,actor));var two=Task.Run(()=>service.Post(second,actor));
                if(!ready.Wait(TimeSpan.FromSeconds(10)))throw new TimeoutException("Both inventory requests did not reach the connection barrier.");release.Set();
                if(!Task.WaitAll(new Task[]{one,two},TimeSpan.FromSeconds(30)))throw new TimeoutException("Inventory race did not finish.");
                if(sameSource)Check(one.Result.Before==two.Result.Before&&one.Result.After==two.Result.After&&one.Result.AlreadyPosted!=two.Result.AlreadyPosted,"Same-source race created two stock movements.");
            }
        }
    }
}

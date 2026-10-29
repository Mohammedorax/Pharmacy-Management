using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Application;

namespace project_fo_3
{
    internal sealed partial class GeneralLedgerReadQaSelfTests
    {
        internal void VerifyConcurrentSnapshot(string token,int openingId)
        {
            Check(QaSalesInvoiceSelfTests.RequireOwnedTemporaryQa(token)==database,"Ledger contention escaped owned QA.");
            string user=SessionContext.CurrentUser;int periodId;DateTime openingDate,periodDate;
            var amounts=new List<OwnedAmount>();
            using(var connection=Database.CreateConnection())
            {
                connection.Open();Check(connection.Database==database,"Contention seed escaped owned QA.");
                using(var command=new SqlCommand("SELECT JournalEntryId,EntryDate,SourceId FROM dbo.JournalEntriesTb WHERE SourceType=N'QAJournalAcceptance' AND SourceId IN(@Token,@Async) AND CreatedBy=@User ORDER BY SourceId",connection))
                {
                    command.Parameters.Add("@Token",SqlDbType.NVarChar,50).Value=token;command.Parameters.Add("@Async",SqlDbType.NVarChar,50).Value=token+"-A";
                    command.Parameters.Add("@User",SqlDbType.NVarChar,200).Value=user;
                    var headers=new Dictionary<string,Tuple<int,DateTime>>();
                    using(var reader=command.ExecuteReader())while(reader.Read())headers.Add(reader.GetString(2),Tuple.Create(reader.GetInt32(0),reader.GetDateTime(1)));
                    Check(headers.Count==2&&headers.ContainsKey(token)&&headers.ContainsKey(token+"-A")&&headers[token].Item1==openingId,"Contention requires exactly the two owned posted sources.");
                    openingDate=headers[token].Item2;periodId=headers[token+"-A"].Item1;periodDate=headers[token+"-A"].Item2;
                }
                Check(openingDate.Date==periodDate.Date,"Owned contention documents require a common initial day.");
                using(var command=new SqlCommand("SELECT JournalEntryLineId,JournalEntryId,Debit,Credit FROM dbo.JournalEntryLinesTb WHERE JournalEntryId IN(@Opening,@Period) ORDER BY JournalEntryId,JournalEntryLineId",connection))
                {
                    command.Parameters.Add("@Opening",SqlDbType.Int).Value=openingId;command.Parameters.Add("@Period",SqlDbType.Int).Value=periodId;
                    using(var reader=command.ExecuteReader())while(reader.Read())amounts.Add(new OwnedAmount{Line=reader.GetInt32(0),Journal=reader.GetInt32(1),Debit=reader.GetDecimal(2),Credit=reader.GetDecimal(3)});
                }
            }
            Check(amounts.Count==4&&amounts.GroupBy(row=>row.Journal).All(group=>group.Count()==2&&group.Sum(row=>row.Debit-row.Credit)==0m)&&
                amounts.All(row=>(row.Debit>0&&row.Credit==0)||(row.Credit>0&&row.Debit==0)),"Owned contention lines are not exactly two balanced two-line documents.");
            string before=OwnedSnapshot(token,user,openingId,periodId);
            DateTime prior=periodDate.Date.AddDays(-1);bool dateChanged=false;
            Task<int> writer=null;Task<GeneralLedgerReadResult> reading=null;
            try
            {
                ChangeOwnedDate(token,user,openingId,openingDate,prior);dateChanged=true;
                var scene=Create(token,openingId);
                var filter=new GeneralLedgerReadRequest(account,periodDate.Date,periodDate.Date,null,true,true,null);
                var old=Await(scene.original.LoadAsync(filter,CancellationToken.None));scene.VerifyResult(filter,old);
                using(var observer=Database.CreateConnection())using(var blocker=Database.CreateConnection())
                {
                    observer.Open();blocker.Open();Check(observer.Database==database&&blocker.Database==database,"Snapshot observer/blocker escaped owned QA.");
                    int blockerId;using(var id=new SqlCommand("SELECT @@SPID",blocker))blockerId=Convert.ToInt32(id.ExecuteScalar());
                    var timer=Stopwatch.StartNew();
                    using(var transaction=blocker.BeginTransaction())
                    {
                        bool released=false;
                        try
                        {
                            // Opening SQL does not join centers; the following rows SELECT does.
                            using(var hold=new SqlCommand("SELECT COUNT_BIG(*) FROM dbo.CostCentersTb WITH(TABLOCKX,HOLDLOCK)",blocker,transaction))hold.ExecuteScalar();
                            reading=scene.original.LoadAsync(filter,CancellationToken.None);
                            int readerId=WaitAtRowsSelect(observer,blockerId);
                            Check(!reading.IsCompleted,"Rows-phase reader completed despite observed owned center lock.");
                            var writerId=new TaskCompletionSource<int>();
                            writer=Task.Run(()=>WriteOwnedDelta(token,user,openingId,periodId,amounts,writerId));
                            int writerSession=Await(writerId.Task);
                            WaitForWriter(observer,writerSession,readerId);
                            Check(!writer.IsCompleted,"Concurrent writer committed before serializable reader completed.");
                            transaction.Rollback();released=true;
                            var acceptedOld=Await(reading);scene.VerifyResult(filter,acceptedOld);
                            Check(acceptedOld.PreviousBalance==old.PreviousBalance&&acceptedOld.PeriodDebit==old.PeriodDebit&&acceptedOld.PeriodCredit==old.PeriodCredit&&acceptedOld.EndingBalance==old.EndingBalance,
                                "Reader mixed an old opening balance with modified period values.");
                            Check(Await(writer)==4,"Concurrent writer did not atomically update exactly four owned lines.");
                            var changed=Create(token,openingId);var acceptedNew=Await(changed.original.LoadAsync(filter,CancellationToken.None));changed.VerifyResult(filter,acceptedNew);
                            Check(acceptedNew.PreviousBalance==old.PreviousBalance+1m&&acceptedNew.PeriodDebit==old.PeriodDebit+1m&&acceptedNew.PeriodCredit==old.PeriodCredit&&acceptedNew.EndingBalance==old.EndingBalance+2m,
                                "Fresh reader did not receive the complete committed new opening/period values.");
                            Console.WriteLine("PASS owned ledger concurrent snapshot: reader="+readerId+"; writer="+writerSession+"; rowsBlocker="+blockerId+"; fixture_elapsed_ms="+timer.ElapsedMilliseconds+
                                "; opening and rows old together, writer blocked on reader then committed four balanced owned lines, fresh root sees both deltas; no mixed result. Artificial contention timing is not product performance.");
                        }
                        finally{if(!released)transaction.Rollback();}
                    }
                }
            }
            finally
            {
                // Release/drain before restoring. Only the four token/user/id-owned rows can change.
                if(reading!=null)try{Await(reading);}catch(Exception error){Console.WriteLine("Owned snapshot read drain: "+error.GetType().Name);}
                if(writer!=null)try{Await(writer);}catch(Exception error){Console.WriteLine("Owned snapshot writer drain: "+error.GetType().Name);}
                if(dateChanged)
                {
                    RestoreOwnedAmounts(token,user,openingId,periodId,amounts);
                    ChangeOwnedDate(token,user,openingId,prior,openingDate);
                }
            }
            Check(OwnedSnapshot(token,user,openingId,periodId)==before,"Owned contention header/line values did not restore exactly.");
            VerifyUnchanged();
            Console.WriteLine("PASS owned ledger snapshot restoration: exact original header/line XML and raw ledger seed restored; no additional journal or audit, source unchanged checked by outer gate.");
        }
        private int WaitAtRowsSelect(SqlConnection observer,int blocker)
        {
            var timer=Stopwatch.StartNew();
            while(timer.Elapsed<TimeSpan.FromSeconds(8))
            {
                using(var command=new SqlCommand(@"SELECT r.session_id,r.wait_type,SUBSTRING(t.text,r.statement_start_offset/2+1,
(CASE WHEN r.statement_end_offset=-1 THEN DATALENGTH(t.text) ELSE r.statement_end_offset END-r.statement_start_offset)/2+1)
FROM sys.dm_exec_requests r CROSS APPLY sys.dm_exec_sql_text(r.sql_handle) t
WHERE r.database_id=DB_ID() AND r.blocking_session_id=@Blocker AND r.wait_type LIKE N'LCK%'
AND t.text LIKE N'%DECLARE @OpeningBalance DECIMAL%'",observer))
                {
                    command.Parameters.Add("@Blocker",SqlDbType.Int).Value=blocker;
                    using(var reader=command.ExecuteReader())if(reader.Read())
                    {
                        int id=Convert.ToInt32(reader[0]);string wait=reader.GetString(1),statement=reader.GetString(2).Trim();
                        Check(statement.StartsWith("SELECT e.EntryDate",StringComparison.OrdinalIgnoreCase),"Owned snapshot wait was not at the rows SELECT after opening computation.");
                        Check(!reader.Read(),"Multiple ledger readers entered the snapshot fixture.");
                        Console.WriteLine("Owned ledger snapshot phase: reader="+id+"; rowsBlocker="+blocker+"; phase=rows-select; wait="+wait+"; observed_ms="+timer.ElapsedMilliseconds);
                        return id;
                    }
                }
                Thread.Sleep(10);
            }
            throw new TimeoutException("Actual ledger reader was not observed at its second SELECT under the owned center lock.");
        }
        private void WaitForWriter(SqlConnection observer,int writer,int reader)
        {
            var timer=Stopwatch.StartNew();
            while(timer.Elapsed<TimeSpan.FromSeconds(8))
            {
                using(var command=new SqlCommand("SELECT wait_type FROM sys.dm_exec_requests WHERE database_id=DB_ID() AND session_id=@Writer AND blocking_session_id=@Reader AND wait_type LIKE N'LCK%'",observer))
                {
                    command.Parameters.Add("@Writer",SqlDbType.Int).Value=writer;command.Parameters.Add("@Reader",SqlDbType.Int).Value=reader;
                    var wait=command.ExecuteScalar();if(wait!=null&&wait!=DBNull.Value)
                    {Console.WriteLine("Owned ledger snapshot writer wait: writer="+writer+"; reader="+reader+"; wait="+Convert.ToString(wait)+"; observed_ms="+timer.ElapsedMilliseconds);return;}
                }
                Thread.Sleep(10);
            }
            throw new TimeoutException("Owned concurrent writer was not observed waiting on the serializable ledger reader.");
        }
        private int WriteOwnedDelta(string token,string user,int opening,int period,List<OwnedAmount> amounts,TaskCompletionSource<int> id)
        {
            try
            {
                using(var connection=Database.CreateConnection())
                {
                    connection.Open();Check(connection.Database==database,"Writer escaped owned QA.");
                    using(var command=new SqlCommand("SELECT @@SPID",connection))id.SetResult(Convert.ToInt32(command.ExecuteScalar()));
                    using(var transaction=connection.BeginTransaction())
                    {
                        int total=0;
                        foreach(var row in amounts.OrderBy(row=>row.Journal==opening?0:1).ThenBy(row=>row.Line))
                            total+=SetOwnedAmount(connection,transaction,token,user,opening,row,true);
                        transaction.Commit();return total;
                    }
                }
            }
            catch(Exception error){id.TrySetException(error);throw;}
        }
        private void RestoreOwnedAmounts(string token,string user,int opening,int period,List<OwnedAmount> amounts)
        {
            using(var connection=Database.CreateConnection())
            {
                connection.Open();Check(connection.Database==database,"Writer restoration escaped owned QA.");
                using(var transaction=connection.BeginTransaction())
                {
                    foreach(var row in amounts)SetOwnedAmount(connection,transaction,token,user,opening,row,false);
                    transaction.Commit();
                }
            }
        }
        private int SetOwnedAmount(SqlConnection connection,SqlTransaction transaction,string token,string user,int opening,OwnedAmount row,bool delta)
        {
            using(var command=new SqlCommand(@"UPDATE l SET Debit=@Debit,Credit=@Credit FROM dbo.JournalEntryLinesTb l
JOIN dbo.JournalEntriesTb e ON e.JournalEntryId=l.JournalEntryId
WHERE l.JournalEntryLineId=@Line AND e.JournalEntryId=@Id AND e.SourceType=N'QAJournalAcceptance' AND e.SourceId=@Source AND e.CreatedBy=@User
AND ((l.Debit=@OldDebit AND l.Credit=@OldCredit) OR (l.Debit=@NewDebit AND l.Credit=@NewCredit))",connection,transaction))
            {
                decimal debit=row.Debit+(row.Debit>0?1m:0m),credit=row.Credit+(row.Credit>0?1m:0m);
                command.CommandTimeout=10;
                Money(command,"@Debit",delta?debit:row.Debit);Money(command,"@Credit",delta?credit:row.Credit);
                Money(command,"@OldDebit",row.Debit);Money(command,"@OldCredit",row.Credit);Money(command,"@NewDebit",debit);Money(command,"@NewCredit",credit);
                command.Parameters.Add("@Line",SqlDbType.Int).Value=row.Line;command.Parameters.Add("@Id",SqlDbType.Int).Value=row.Journal;
                command.Parameters.Add("@Source",SqlDbType.NVarChar,50).Value=token+(row.Journal==opening?"":"-A");command.Parameters.Add("@User",SqlDbType.NVarChar,200).Value=user;
                int affected=command.ExecuteNonQuery();Check(affected==1,"Amount mutation/restoration did not target exactly one known owned line.");return affected;
            }
        }
        private static void Money(SqlCommand command,string name,decimal value)
        {var parameter=command.Parameters.Add(name,SqlDbType.Decimal);parameter.Precision=18;parameter.Scale=2;parameter.Value=value;}
        private void ChangeOwnedDate(string token,string user,int opening,DateTime expected,DateTime date)
        {
            using(var connection=Database.CreateConnection())
            {
                connection.Open();Check(connection.Database==database,"Date mutation escaped owned QA.");
                using(var command=new SqlCommand("UPDATE dbo.JournalEntriesTb SET EntryDate=@Date WHERE JournalEntryId=@Id AND SourceType=N'QAJournalAcceptance' AND SourceId=@Source AND CreatedBy=@User AND EntryDate=@Expected",connection))
                {
                    command.Parameters.Add("@Date",SqlDbType.DateTime).Value=date;command.Parameters.Add("@Expected",SqlDbType.DateTime).Value=expected;
                    command.Parameters.Add("@Id",SqlDbType.Int).Value=opening;command.Parameters.Add("@Source",SqlDbType.NVarChar,50).Value=token;
                    command.Parameters.Add("@User",SqlDbType.NVarChar,200).Value=user;Check(command.ExecuteNonQuery()==1,"Owned date mutation/restoration did not match exact original document.");
                }
            }
        }
        private string OwnedSnapshot(string token,string user,int opening,int period)
        {
            using(var connection=Database.CreateConnection())
            {
                connection.Open();Check(connection.Database==database,"Owned snapshot escaped QA.");
                using(var command=new SqlCommand(@"SELECT CONVERT(nvarchar(max),(SELECT * FROM dbo.JournalEntriesTb
WHERE (JournalEntryId=@Opening AND SourceId=@Token OR JournalEntryId=@Period AND SourceId=@Async) AND SourceType=N'QAJournalAcceptance' AND CreatedBy=@User
ORDER BY JournalEntryId FOR XML RAW('r'),ROOT('headers'),TYPE))+
CONVERT(nvarchar(max),(SELECT * FROM dbo.JournalEntryLinesTb WHERE JournalEntryId IN(@Opening,@Period) ORDER BY JournalEntryLineId FOR XML RAW('r'),ROOT('lines'),TYPE))",connection))
                {
                    command.Parameters.Add("@Opening",SqlDbType.Int).Value=opening;command.Parameters.Add("@Period",SqlDbType.Int).Value=period;
                    command.Parameters.Add("@Token",SqlDbType.NVarChar,50).Value=token;command.Parameters.Add("@Async",SqlDbType.NVarChar,50).Value=token+"-A";
                    command.Parameters.Add("@User",SqlDbType.NVarChar,200).Value=user;return Convert.ToString(command.ExecuteScalar());
                }
            }
        }
        private sealed class OwnedAmount{internal int Line,Journal;internal decimal Debit,Credit;}
    }
}

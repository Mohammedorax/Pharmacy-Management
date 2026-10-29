using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using PharmacyManagement.Application;
using PharmacyManagement.Domain;
using PharmacyManagement.Infrastructure.SqlServer;

namespace PharmacyManagement.CoreTests
{
    internal static class VoucherPostingSqlTests
    {
        private static int Scalar(SqlConnection connection,SqlTransaction transaction,string sql)
        {using(var command=new SqlCommand(sql,connection,transaction))return Convert.ToInt32(command.ExecuteScalar());}
        private static int[] Counts(SqlConnection connection,SqlTransaction transaction)
            =>new[] {Scalar(connection,transaction,"SELECT COUNT(*) FROM dbo.AccountingVouchersTb"),
                Scalar(connection,transaction,"SELECT COUNT(*) FROM dbo.JournalEntriesTb"),Scalar(connection,transaction,"SELECT COUNT(*) FROM dbo.JournalEntryLinesTb"),
                Scalar(connection,transaction,"SELECT COUNT(*) FROM dbo.AuditLogTb"),Scalar(connection,transaction,"SELECT COUNT(*) FROM dbo.AccountingPeriodsTb")};
        private static void Equal(int[] expected,int[] actual)
        {for(int i=0;i<expected.Length;i++)if(expected[i]!=actual[i])throw new Exception("Voucher QA counts differ after rollback.");}
        internal static void VerifyQaRollback(string server,string database,string backupPath)
        {
            using(var connection=QaAccountingWriteFixture.Open(server,database,backupPath))
            {
                var codes=new List<string>();
                using(var command=new SqlCommand("SELECT TOP 2 AccountCode FROM dbo.AccountsTb WHERE IsActive=1 ORDER BY AccountCode",connection))
                using(var reader=command.ExecuteReader())while(reader.Read())codes.Add(reader.GetString(0));
                if(codes.Count!=2)throw new Exception("Two active accounts required.");
                int? center;
                using(var command=new SqlCommand("SELECT TOP 1 CostCenterId FROM dbo.CostCentersTb WHERE IsActive=1 ORDER BY CostCenterId",connection))
                {var value=command.ExecuteScalar();center=value==null||value==DBNull.Value?(int?)null:Convert.ToInt32(value);}
                string token=Guid.NewGuid().ToString("N"),missing="QA_MISSING_"+token.Substring(0,8);
                using(var command=new SqlCommand("SELECT COUNT(*) FROM dbo.AccountsTb WHERE AccountCode=@Code",connection))
                {command.Parameters.Add("@Code",SqlDbType.NVarChar,20).Value=missing;if(Convert.ToInt32(command.ExecuteScalar())!=0)throw new Exception("Missing account is not unique.");}
                var actor=new OperationActor {UserName="QA-Voucher-"+token,UserRole="Manager"};
                var date=new DateTime(1900,1,1,12,30,45);var baseline=Counts(connection,null);int variants=0;
                foreach(var type in new[] {"ReceiptExternal","ReceiptInternal","ReceiptMulti","PaymentExternal","PaymentInternal","PaymentMulti"})
                {
                    bool receipt=type.StartsWith("Receipt");decimal expected=receipt?10m:10.02m;
                    var prepared=VoucherPreparation.Prepare(type,"  عميل Cash 500 mg  ",codes[1],codes[0],receipt?10.005m:10.015m,"  بيان عربي  ",center);
                    using(var transaction=connection.BeginTransaction())
                    {
                        try
                        {
                            var result=SqlVoucherPostRepository.PostInTransaction(connection,transaction,date,prepared,actor);
                            if(result.VoucherId<=0||result.JournalEntryId<=0)throw new Exception("Missing accepted voucher identifiers.");
                            var expectedCounts=(int[])baseline.Clone();expectedCounts[0]++;expectedCounts[1]++;expectedCounts[2]+=2;expectedCounts[3]++;
                            Equal(expectedCounts,Counts(connection,transaction));
                            using(var command=new SqlCommand(@"SELECT v.VoucherType,v.VoucherDate,v.PartyName,v.AccountCode,v.Amount,v.Notes,v.JournalEntryId,v.CreatedBy,
j.SourceType,j.SourceId,j.Description FROM dbo.AccountingVouchersTb v JOIN dbo.JournalEntriesTb j ON v.JournalEntryId=j.JournalEntryId WHERE v.VoucherId=@Id",connection,transaction))
                            {
                                command.Parameters.Add("@Id",SqlDbType.Int).Value=result.VoucherId;
                                using(var reader=command.ExecuteReader())
                                    if(!reader.Read()||reader.GetString(0)!=type||reader.GetDateTime(1)!=date||reader.GetString(2)!="عميل Cash 500 mg"||
                                        reader.GetString(3)!=codes[1]||reader.GetDecimal(4)!=expected||reader.GetString(5)!="بيان عربي"||reader.GetInt32(6)!=result.JournalEntryId||
                                        reader.GetString(7)!=actor.UserName||reader.GetString(8)!="Voucher"||reader.GetString(9)!=result.VoucherId.ToString(System.Globalization.CultureInfo.InvariantCulture)||
                                        reader.GetString(10)!=prepared.Description.Trim())throw new Exception("Voucher/header/journal source mapping differs.");
                            }
                            using(var command=new SqlCommand("SELECT AccountCode,Debit,Credit,LineDescription,CostCenterId FROM dbo.JournalEntryLinesTb WHERE JournalEntryId=@Id ORDER BY JournalEntryLineId",connection,transaction))
                            {
                                command.Parameters.Add("@Id",SqlDbType.Int).Value=result.JournalEntryId;
                                using(var reader=command.ExecuteReader())
                                {
                                    for(int line=0;line<2;line++)
                                    {
                                        string code=line==0?(receipt?codes[0]:codes[1]):(receipt?codes[1]:codes[0]);
                                        if(!reader.Read()||reader.GetString(0)!=code||reader.GetDecimal(1)!=(line==0?expected:0)||reader.GetDecimal(2)!=(line==1?expected:0)||
                                            reader.GetString(3)!="بيان عربي"||(center.HasValue?reader.GetInt32(4)!=center.Value:!reader.IsDBNull(4)))throw new Exception("Voucher journal direction, rounding or cost center differs.");
                                    }
                                    if(reader.Read())throw new Exception("Unexpected voucher lines.");
                                }
                            }
                            using(var command=new SqlCommand("SELECT UserRole,ActionName,EntityName FROM dbo.AuditLogTb WHERE UserName=@User",connection,transaction))
                            {
                                command.Parameters.Add("@User",SqlDbType.NVarChar,200).Value=actor.UserName;
                                using(var reader=command.ExecuteReader())if(!reader.Read()||reader.GetString(0)!=actor.UserRole||reader.GetString(1)!="AccountingVoucherCreated"||reader.GetString(2)!="AccountingVouchers"||reader.Read())throw new Exception("Voucher actor/audit mapping differs.");
                            }
                            variants++;
                        }
                        finally {transaction.Rollback();}
                    }
                    Equal(baseline,Counts(connection,null));
                }
                var valid=VoucherPreparation.Prepare("ReceiptExternal","QA",codes[1],codes[0],10,null,null);
                using(var transaction=connection.BeginTransaction())
                {
                    try
                    {
                        using(var command=new SqlCommand("INSERT INTO dbo.AccountingPeriodsTb(PeriodId,StartDate,EndDate,Status,ClosedAtUtc,ClosedBy,Notes) VALUES(@Id,@Date,@Date,N'Closed',SYSUTCDATETIME(),@User,N'QA voucher rollback')",connection,transaction))
                        {command.Parameters.Add("@Id",SqlDbType.UniqueIdentifier).Value=Guid.NewGuid();command.Parameters.Add("@Date",SqlDbType.Date).Value=date.Date;command.Parameters.Add("@User",SqlDbType.NVarChar,200).Value=actor.UserName;command.ExecuteNonQuery();}
                        bool refused=false;try {SqlVoucherPostRepository.PostInTransaction(connection,transaction,date,valid,actor);}catch(InvalidOperationException){refused=true;}
                        if(!refused)throw new Exception("Voucher crossed closed period.");var expected=(int[])baseline.Clone();expected[4]++;Equal(expected,Counts(connection,transaction));
                    }
                    finally {transaction.Rollback();}
                }
                Equal(baseline,Counts(connection,null));
                var service=new VoucherPostService(new SqlVoucherPostRepository(()=>new SqlConnection(connection.ConnectionString)),_=>true);
                bool partialFailed=false;
                try {service.Post(new VoucherPostRequest("ReceiptExternal",date,"QA",missing,10,null,codes[0],null),actor);}catch(InvalidOperationException){partialFailed=true;}
                if(!partialFailed)throw new Exception("Owning voucher repository accepted missing second-line account.");Equal(baseline,Counts(connection,null));
                foreach(var table in new[] {"AccountingVouchersTb","AuditLogTb"})
                {
                    string trigger="QaVoucherFailure_"+token;
                    using(var transaction=connection.BeginTransaction())
                    {
                        try
                        {
                            string actorColumn=table=="AuditLogTb"?"UserName":"CreatedBy";
                            using(var command=new SqlCommand("CREATE TRIGGER dbo.["+trigger+"] ON dbo.["+table+"] AFTER INSERT AS BEGIN IF EXISTS(SELECT 1 FROM inserted WHERE "+actorColumn+"=N'"+actor.UserName+"') THROW 51011,N'QA voucher failure fixture',1; END",connection,transaction))command.ExecuteNonQuery();
                            bool failed=false;try {SqlVoucherPostRepository.PostInTransaction(connection,transaction,date,valid,actor);}catch(SqlException error){if(error.Number!=51011)throw;failed=true;}
                            if(!failed)throw new Exception("Voucher insert or audit failure was ignored.");
                        }
                        finally {transaction.Rollback();}
                    }
                    Equal(baseline,Counts(connection,null));
                    if(Scalar(connection,null,"SELECT COUNT(*) FROM sys.triggers WHERE name=N'"+trigger+"'")!=0)throw new Exception("Voucher failure trigger survived rollback.");
                }
                Console.WriteLine("PASS: QA voucher variants="+variants+"; amount/journal/directions/source/actor/audit/cost center matched; closed-period, partial-line, voucher-insert and audit failures rolled back. PersistentRowWrites=0; identity counters may advance. Successful public Commit, lost-ack retry, two-device and physical printing acceptance remain separate.");
            }
        }
    }
}

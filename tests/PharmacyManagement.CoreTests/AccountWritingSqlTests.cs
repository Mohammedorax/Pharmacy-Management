using System;
using System.Data;
using System.Data.SqlClient;
using PharmacyManagement.Application;
using PharmacyManagement.Domain;
using PharmacyManagement.Infrastructure.SqlServer;

namespace PharmacyManagement.CoreTests
{
    internal static class AccountWritingSqlTests
    {
        private static int Scalar(SqlConnection connection,SqlTransaction transaction,string sql)
        {using(var command=new SqlCommand(sql,connection,transaction))return Convert.ToInt32(command.ExecuteScalar());}
        private static int[] Counts(SqlConnection connection,SqlTransaction transaction)
            =>new[]{Scalar(connection,transaction,"SELECT COUNT(*) FROM dbo.AccountsTb"),Scalar(connection,transaction,"SELECT COUNT(*) FROM dbo.AuditLogTb"),
                Scalar(connection,transaction,"SELECT COUNT(*) FROM dbo.JournalEntriesTb"),Scalar(connection,transaction,"SELECT COUNT(*) FROM dbo.JournalEntryLinesTb")};
        private static void Equal(int[] left,int[] right)
        {for(int i=0;i<left.Length;i++)if(left[i]!=right[i])throw new Exception("Account QA counts changed unexpectedly.");}
        private static void Refused(Action action)
        {try{action();}catch(InvalidOperationException){return;}throw new Exception("Account integrity operation was not refused.");}
        internal static void VerifyQaRollback(string server,string database,string backupPath)
        {
            using(var connection=QaAccountingWriteFixture.Open(server,database,backupPath))
            {
                string token=Guid.NewGuid().ToString("N"),root="QA_A_"+token.Substring(0,12),child="QA_B_"+token.Substring(0,12),missing="QA_M_"+token.Substring(0,12);
                var actor=new OperationActor{UserName="QA-Account-"+token,UserRole="Manager"};var baseline=Counts(connection,null);
                foreach(string code in new[]{root,child,missing})using(var command=new SqlCommand("SELECT COUNT(*) FROM dbo.AccountsTb WHERE AccountCode=@Code",connection))
                {command.Parameters.Add("@Code",SqlDbType.NVarChar,20).Value=code;if(Convert.ToInt32(command.ExecuteScalar())!=0)throw new Exception("Account fixture collision.");}
                string cash;using(var command=new SqlCommand("SELECT TOP 1 AccountCode FROM dbo.AccountsTb WHERE IsActive=1 ORDER BY AccountCode",connection))
                {cash=Convert.ToString(command.ExecuteScalar());if(string.IsNullOrWhiteSpace(cash))throw new Exception("An active QA account is required.");}
                var parentRequest=new AccountSaveRequest(root,"حساب QA Cash 500 mg","Asset",null,true);
                var childRequest=new AccountSaveRequest(child,"حساب فرعي","Asset",root,true);
                using(var transaction=connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        SqlAccountWriteRepository.SaveInTransaction(connection,transaction,parentRequest,actor);
                        SqlAccountWriteRepository.SaveInTransaction(connection,transaction,childRequest,actor);
                        SqlAccountWriteRepository.SaveInTransaction(connection,transaction,new AccountSaveRequest(child,"تحديث الاسم العربي","Asset",root,true),actor);
                        var expected=(int[])baseline.Clone();expected[0]+=2;expected[1]+=3;Equal(expected,Counts(connection,transaction));
                        using(var command=new SqlCommand("SELECT AccountName,AccountType,ParentAccountCode,IsActive FROM dbo.AccountsTb WHERE AccountCode=@Code",connection,transaction))
                        {command.Parameters.Add("@Code",SqlDbType.NVarChar,20).Value=child;using(var reader=command.ExecuteReader())if(!reader.Read()||reader.GetString(0)!="تحديث الاسم العربي"||reader.GetString(1)!="Asset"||reader.GetString(2)!=root||!reader.GetBoolean(3))throw new Exception("Account update mapping differs.");}
                        Refused(()=>SqlAccountWriteRepository.SaveInTransaction(connection,transaction,new AccountSaveRequest(root,"should not save","Asset",child,true),actor));
                        Refused(()=>SqlAccountWriteRepository.SaveInTransaction(connection,transaction,new AccountSaveRequest(root,"should not save","Asset",missing,true),actor));
                        Refused(()=>SqlAccountWriteRepository.DeleteInTransaction(connection,transaction,root,actor));Equal(expected,Counts(connection,transaction));
                        using(var command=new SqlCommand("SELECT ParentAccountCode FROM dbo.AccountsTb WHERE AccountCode=@Code",connection,transaction))
                        {command.Parameters.Add("@Code",SqlDbType.NVarChar,20).Value=root;if(command.ExecuteScalar()!=DBNull.Value)throw new Exception("Rejected cycle changed parent.");}
                        int journal=SqlJournalWriter.Create(connection,transaction,new DateTime(1900,1,1),"QA.Account",token,"QA account history guard",actor.UserName,
                            new[]{new JournalLineValues(child,1,0,null,null),new JournalLineValues(cash,0,1,null,null)}).Id;
                        Refused(()=>SqlAccountWriteRepository.DeleteInTransaction(connection,transaction,child,actor));
                        using(var command=new SqlCommand("DELETE FROM dbo.JournalEntryLinesTb WHERE JournalEntryId=@Id;DELETE FROM dbo.JournalEntriesTb WHERE JournalEntryId=@Id;",connection,transaction))
                        {command.Parameters.Add("@Id",SqlDbType.Int).Value=journal;command.ExecuteNonQuery();}
                        if(!SqlAccountWriteRepository.DeleteInTransaction(connection,transaction,child,actor).Changed||!SqlAccountWriteRepository.DeleteInTransaction(connection,transaction,root,actor).Changed)
                            throw new Exception("Unused leaf/root deletion failed.");
                        if(SqlAccountWriteRepository.DeleteInTransaction(connection,transaction,root,actor).Changed)throw new Exception("Absent account deletion reported a write.");
                        expected=(int[])baseline.Clone();expected[1]+=5;Equal(expected,Counts(connection,transaction));
                        using(var command=new SqlCommand("SELECT UserRole,EntityName,ActionName FROM dbo.AuditLogTb WHERE UserName=@User",connection,transaction))
                        {command.Parameters.Add("@User",SqlDbType.NVarChar,200).Value=actor.UserName;int count=0;using(var reader=command.ExecuteReader())while(reader.Read())
                            {if(reader.GetString(0)!="Manager"||reader.GetString(1)!="Accounts"||(reader.GetString(2)!="ChartOfAccountSaved"&&reader.GetString(2)!="ChartOfAccountDeleted"))throw new Exception("Account audit actor/action differs.");count++;}if(count!=5)throw new Exception("Missing account audit events.");}
                    }
                    finally{transaction.Rollback();}
                }
                Equal(baseline,Counts(connection,null));
                var service=new AccountWriteService(new SqlAccountWriteRepository(()=>new SqlConnection(connection.ConnectionString)),_=>true);
                Refused(()=>service.Save(new AccountSaveRequest(root,"QA","Asset",missing,true),actor));Equal(baseline,Counts(connection,null));
                foreach(bool deletion in new[]{false,true})
                {
                    string trigger="QaAccountAudit_"+token;
                    using(var transaction=connection.BeginTransaction(IsolationLevel.Serializable))
                    {
                        try
                        {
                            if(deletion)SqlAccountWriteRepository.SaveInTransaction(connection,transaction,parentRequest,actor);
                            string action=deletion?"ChartOfAccountDeleted":"ChartOfAccountSaved";
                            using(var command=new SqlCommand("CREATE TRIGGER dbo.["+trigger+"] ON dbo.AuditLogTb AFTER INSERT AS BEGIN IF EXISTS(SELECT 1 FROM inserted WHERE UserName=N'"+actor.UserName+"' AND ActionName=N'"+action+"') THROW 51012,N'QA account audit failure',1; END",connection,transaction))command.ExecuteNonQuery();
                            bool failed=false;
                            try {if(deletion)SqlAccountWriteRepository.DeleteInTransaction(connection,transaction,root,actor);else SqlAccountWriteRepository.SaveInTransaction(connection,transaction,parentRequest,actor);}
                            catch(SqlException error){if(error.Number!=51012)throw;failed=true;}
                            if(!failed)throw new Exception("Account audit failure did not fail the mutation.");
                        }
                        finally{transaction.Rollback();}
                    }
                    Equal(baseline,Counts(connection,null));
                    if(Scalar(connection,null,"SELECT COUNT(*) FROM sys.triggers WHERE name=N'"+trigger+"'")!=0)throw new Exception("Account audit trigger survived rollback.");
                }
                Console.WriteLine("PASS: QA account create/update/delete, captured audit, missing delete, child/history protections, descendant cycle/missing ancestor rejection and save/delete audit failure rollback. PersistentRowWrites=0; identity counters may advance; two-device concurrency and public successful Commit remain separate.");
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using PharmacyManagement.Application;
using PharmacyManagement.Domain;

namespace PharmacyManagement.Infrastructure.SqlServer
{
    public sealed class SqlAccountWriteRepository : IAccountWriteRepository
    {
        private readonly Func<SqlConnection> connectionFactory;
        public SqlAccountWriteRepository(Func<SqlConnection> connectionFactory){this.connectionFactory=connectionFactory??throw new ArgumentNullException(nameof(connectionFactory));}
        private AccountWriteResult Execute(Func<SqlConnection,SqlTransaction,AccountWriteResult> operation)
        {
            using(var connection=connectionFactory())
            {connection.Open();using(var transaction=connection.BeginTransaction(IsolationLevel.Serializable))
                {try{var result=operation(connection,transaction);transaction.Commit();return result;}catch{try{transaction.Rollback();}catch{}throw;}}}
        }
        public AccountWriteResult Save(AccountSaveRequest request,OperationActor actor)
        {if(request==null)throw new ArgumentNullException(nameof(request));if(actor==null)throw new ArgumentNullException(nameof(actor));return Execute((connection,transaction)=>SaveInTransaction(connection,transaction,request,actor));}
        public AccountWriteResult Delete(string code,OperationActor actor)
        {if(actor==null)throw new ArgumentNullException(nameof(actor));return Execute((connection,transaction)=>DeleteInTransaction(connection,transaction,code,actor));}
        public static AccountWriteResult SaveInTransaction(SqlConnection connection,SqlTransaction transaction,AccountSaveRequest request,OperationActor actor)
        {
            if(request==null)throw new ArgumentNullException(nameof(request));if(actor==null)throw new ArgumentNullException(nameof(actor));
            var parents=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
            using(var command=new SqlCommand("SELECT AccountCode,ParentAccountCode FROM dbo.AccountsTb WITH (UPDLOCK,HOLDLOCK)",connection,transaction))
            using(var reader=command.ExecuteReader())while(reader.Read())parents.Add(reader.GetString(0),reader.IsDBNull(1)?null:reader.GetString(1));
            AccountHierarchy.ValidateParent(request.Code,request.ParentCode,parents);
            using(var command=new SqlCommand(@"IF EXISTS (SELECT 1 FROM dbo.AccountsTb WHERE AccountCode=@Code)
UPDATE dbo.AccountsTb SET AccountName=@Name,AccountType=@Type,ParentAccountCode=@Parent,IsActive=@Active WHERE AccountCode=@Code;
ELSE INSERT INTO dbo.AccountsTb(AccountCode,AccountName,AccountType,ParentAccountCode,IsActive) VALUES(@Code,@Name,@Type,@Parent,@Active);",connection,transaction))
            {
                command.Parameters.Add("@Code",SqlDbType.NVarChar,20).Value=request.Code;
                command.Parameters.Add("@Name",SqlDbType.NVarChar,200).Value=request.Name;
                command.Parameters.Add("@Type",SqlDbType.NVarChar,50).Value=request.Type;
                command.Parameters.Add("@Parent",SqlDbType.NVarChar,20).Value=(object)request.ParentCode??DBNull.Value;
                command.Parameters.Add("@Active",SqlDbType.Bit).Value=request.IsActive;command.ExecuteNonQuery();
            }
            Audit(connection,transaction,actor,"ChartOfAccountSaved","تم حفظ الحساب: "+request.Code+"؛ نشط: "+request.IsActive+".");
            return new AccountWriteResult(request.Code,true);
        }
        public static AccountWriteResult DeleteInTransaction(SqlConnection connection,SqlTransaction transaction,string code,OperationActor actor)
        {
            if(actor==null)throw new ArgumentNullException(nameof(actor));
            // Prevent a posting from resolving the account while deletion checks are in progress.
            using(var command=new SqlCommand("SELECT AccountCode FROM dbo.AccountsTb WITH (XLOCK,HOLDLOCK) WHERE AccountCode=@Code",connection,transaction))
            {command.Parameters.Add("@Code",SqlDbType.NVarChar,20).Value=code;if(command.ExecuteScalar()==null)return new AccountWriteResult(code,false);}
            foreach(var check in new[]{new[]{"SELECT COUNT(1) FROM dbo.JournalEntryLinesTb WITH (UPDLOCK,HOLDLOCK) WHERE AccountCode=@Code","لا يمكن حذف الحساب لأنه يحتوي على حركات. يمكن تعطيله بدلا من الحذف."},
                new[]{"SELECT COUNT(1) FROM dbo.AccountsTb WITH (UPDLOCK,HOLDLOCK) WHERE ParentAccountCode=@Code","لا يمكن حذف الحساب لأنه يحتوي على حسابات فرعية."}})
                using(var command=new SqlCommand(check[0],connection,transaction))
                {command.Parameters.Add("@Code",SqlDbType.NVarChar,20).Value=code;if(Convert.ToInt32(command.ExecuteScalar())>0)throw new InvalidOperationException(check[1]);}
            using(var command=new SqlCommand("DELETE FROM dbo.AccountsTb WHERE AccountCode=@Code",connection,transaction))
            {command.Parameters.Add("@Code",SqlDbType.NVarChar,20).Value=code;if(command.ExecuteNonQuery()!=1)throw new InvalidOperationException("تعذر حذف الحساب المحدد.");}
            Audit(connection,transaction,actor,"ChartOfAccountDeleted","تم حذف الحساب: "+code+".");return new AccountWriteResult(code,true);
        }
        private static void Audit(SqlConnection connection,SqlTransaction transaction,OperationActor actor,string action,string details)
        {
            using(var command=new SqlCommand("INSERT INTO dbo.AuditLogTb(UserName,UserRole,ActionName,EntityName,Details) VALUES(@User,@Role,@Action,N'Accounts',@Details)",connection,transaction))
            {command.Parameters.Add("@User",SqlDbType.NVarChar,200).Value=actor.UserName;command.Parameters.Add("@Role",SqlDbType.NVarChar,50).Value=actor.UserRole;
                command.Parameters.Add("@Action",SqlDbType.NVarChar,200).Value=action;command.Parameters.Add("@Details",SqlDbType.NVarChar,1000).Value=details;command.ExecuteNonQuery();}
        }
    }
}

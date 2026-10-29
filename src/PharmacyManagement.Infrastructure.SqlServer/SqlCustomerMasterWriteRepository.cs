using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Application;
namespace PharmacyManagement.Infrastructure.SqlServer
{
    public sealed class SqlCustomerMasterWriteRepository:ICustomerMasterWriteRepository
    {
        private readonly Func<SqlConnection> connectionFactory;
        private readonly bool ownedPostCommitDelay;
        private readonly int commandTimeout;
        public SqlCustomerMasterWriteRepository(Func<SqlConnection> connectionFactory):this(connectionFactory,false,30){}
        internal SqlCustomerMasterWriteRepository(Func<SqlConnection> connectionFactory,bool ownedPostCommitDelay,int commandTimeout)
        {
            this.connectionFactory=connectionFactory??throw new ArgumentNullException(nameof(connectionFactory));
            if(commandTimeout<=0||commandTimeout>30)throw new ArgumentOutOfRangeException(nameof(commandTimeout));
            this.ownedPostCommitDelay=ownedPostCommitDelay;this.commandTimeout=commandTimeout;
        }
        private const string Projection=@"
SELECT r.RequestId,r.RequestKind,r.TargetCustomerId,r.CustomerName,r.PhoneNumber,r.CustomerAddress,r.OwnerUser,r.OwnerRole,
       r.Outcome,r.CustomerId,r.AuditId,r.ExecutedUser,r.ExecutedRole,r.FinalizedAtUtc,
       a.UserName,a.UserRole,a.ActionName,a.EntityName,a.Details,
       c.CustomerId,c.CustomerName,c.PhoneNumber,c.CustomerAddress
FROM dbo.CustomerMasterRequestsTb r
LEFT JOIN dbo.AuditLogTb a ON a.AuditId=r.AuditId
LEFT JOIN dbo.CustomersBillsTb c ON c.CustomerId=r.CustomerId
WHERE r.RequestId=@RequestId;";
        private const string LockSql=@"
DECLARE @LockResult int,@Resource nvarchar(255)=N'PharmacyManagement.CustomerWrite.'+CONVERT(nvarchar(36),@RequestId);
EXEC @LockResult=sys.sp_getapplock @Resource=@Resource,@LockMode=N'Exclusive',@LockOwner=N'Transaction',@LockTimeout=10000;
IF @LockResult<0 THROW 51068,N'تعذر قفل محاولة العميل؛ لم تنفذ كتابة من هذا الطلب.',1;";
        private static string SubmitSql(bool delay)=>@"
SET NOCOUNT ON;SET XACT_ABORT ON;
BEGIN TRY
  BEGIN TRANSACTION;
"+LockSql+@"
  DECLARE @Present bit=0,@CustomerId nvarchar(50),@AuditId int,@Outcome tinyint,
          @StoredUser nvarchar(200),@StoredRole nvarchar(50),@AuditAction nvarchar(100),@AuditDetails nvarchar(1000);
  SELECT @Present=1,@StoredUser=OwnerUser,@StoredRole=OwnerRole
  FROM dbo.CustomerMasterRequestsTb WITH(UPDLOCK,HOLDLOCK) WHERE RequestId=@RequestId;
  IF @Present=1
  BEGIN
    IF @StoredUser COLLATE Latin1_General_100_BIN2<>@OwnerUser COLLATE Latin1_General_100_BIN2
       OR DATALENGTH(@StoredUser)<>DATALENGTH(@OwnerUser)
       OR @StoredRole COLLATE Latin1_General_100_BIN2<>@OwnerRole COLLATE Latin1_General_100_BIN2
       OR DATALENGTH(@StoredRole)<>DATALENGTH(@OwnerRole)
       THROW 51062,N'محاولة العميل لا تخص هذا المالك.',1;
  END
  ELSE
  BEGIN
    IF @Cancel=1
    BEGIN
      SET @Outcome=2;SET @CustomerId=NULL;
      SET @AuditAction=N'CustomerWriteCanceled';SET @AuditDetails=@CanceledAuditDetails;
    END
    ELSE
    BEGIN
      IF EXISTS(SELECT 1 FROM dbo.CustomersBillsTb WITH(UPDLOCK,HOLDLOCK)
        WHERE PhoneNumber=@Phone AND CustomerId<>N'0' AND (@Kind=1 OR CustomerId<>@TargetId))
        THROW 51063,N'رقم الهاتف مسجل لعميل آخر. ابحث عنه أو استخدم رقمًا مختلفًا.',1;
      IF @Kind=1
      BEGIN
        SELECT @CustomerId=CONVERT(nvarchar(50),NEXT VALUE FOR dbo.Seq_CustomerId);
        IF @CustomerId IS NULL OR LEN(@CustomerId)=0 OR @CustomerId=N'0'
          THROW 51064,N'تسلسل معرف العميل غير صالح.',1;
        INSERT dbo.CustomersBillsTb(CustomerId,CustomerName,PhoneNumber,CustomerAddress,TotalSpent,InvoiceDate,EmployeeName,InvoiceNotes)
        VALUES(@CustomerId,@Name,@Phone,@Address,0,SYSUTCDATETIME(),N'إدارة العملاء',NULL);
        SET @AuditAction=N'CustomerCreated';
      END
      ELSE
      BEGIN
        SELECT @CustomerId=CustomerId FROM dbo.CustomersBillsTb WITH(UPDLOCK,HOLDLOCK) WHERE CustomerId=@TargetId AND CustomerId<>N'0';
        IF @CustomerId IS NULL THROW 51061,N'لم يعد سجل العميل موجودًا. حدّث القائمة ثم حاول مجددًا.',1;
        UPDATE dbo.CustomersBillsTb SET CustomerName=@Name,PhoneNumber=@Phone,CustomerAddress=@Address WHERE CustomerId=@CustomerId AND CustomerId<>N'0';
        SET @AuditAction=N'CustomerUpdated';
      END;
      SET @Outcome=1;
      SET @AuditDetails=N'معرّف العميل: '+@CustomerId+N'، الاسم: '+@Name;
    END;
    INSERT dbo.AuditLogTb(UserName,UserRole,ActionName,EntityName,Details)
      VALUES(@ExecutedUser,@ExecutedRole,@AuditAction,N'Customer',@AuditDetails);
    SET @AuditId=CONVERT(int,SCOPE_IDENTITY());
    INSERT dbo.CustomerMasterRequestsTb
      (RequestId,RequestKind,TargetCustomerId,CustomerName,PhoneNumber,CustomerAddress,OwnerUser,OwnerRole,
       Outcome,CustomerId,AuditId,ExecutedUser,ExecutedRole,FinalizedAtUtc)
      VALUES(@RequestId,@Kind,@TargetId,@Name,@Phone,@Address,@OwnerUser,@OwnerRole,
       @Outcome,@CustomerId,@AuditId,@ExecutedUser,@ExecutedRole,SYSUTCDATETIME());
  END;
  COMMIT TRANSACTION;
"+(delay?"WAITFOR DELAY '00:00:03';":"")+Projection+@"
END TRY
BEGIN CATCH
  IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
  THROW;
END CATCH;";
        private static string ResolveSql=>@"
SET NOCOUNT ON;SET XACT_ABORT ON;
BEGIN TRY
  BEGIN TRANSACTION;
"+LockSql+Projection+@"
  COMMIT TRANSACTION;
END TRY
BEGIN CATCH
  IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
  THROW;
END CATCH;";
        private static void Bind(SqlCommand command,CustomerMasterWriteRequest request,OperationActor actor,bool cancel)
        {
            command.Parameters.Add("@RequestId",SqlDbType.UniqueIdentifier).Value=request.RequestId;
            command.Parameters.Add("@Kind",SqlDbType.TinyInt).Value=(byte)request.Kind;
            command.Parameters.Add("@TargetId",SqlDbType.NVarChar,50).Value=request.TargetCustomerId;
            command.Parameters.Add("@Name",SqlDbType.NVarChar,200).Value=request.CustomerName;
            command.Parameters.Add("@Phone",SqlDbType.NVarChar,50).Value=request.PhoneNumber;
            command.Parameters.Add("@Address",SqlDbType.NVarChar,300).Value=request.Address;
            command.Parameters.Add("@OwnerUser",SqlDbType.NVarChar,200).Value=request.OwnerUser;
            command.Parameters.Add("@OwnerRole",SqlDbType.NVarChar,50).Value=request.OwnerRole;
            command.Parameters.Add("@ExecutedUser",SqlDbType.NVarChar,200).Value=actor.UserName;
            command.Parameters.Add("@ExecutedRole",SqlDbType.NVarChar,50).Value=actor.UserRole;
            command.Parameters.Add("@Cancel",SqlDbType.Bit).Value=cancel;
            command.Parameters.Add("@CanceledAuditDetails",SqlDbType.NVarChar,1000).Value=CancelDetails(request.RequestId);
        }
        private static string CancelDetails(Guid id)=>"محاولة حفظ العميل أغلقت دون كتابة: "+id.ToString("D");
        private static string Text(SqlDataReader reader,int index)=>reader.IsDBNull(index)?null:reader.GetString(index);
        private static CustomerMasterWriteReceipt Map(SqlDataReader reader,CustomerMasterWriteRequest request)
        {
            if(reader.GetGuid(0)!=request.RequestId)throw new InvalidOperationException("معرف استجابة المحاولة مختلف.");
            string owner=reader.GetString(6),role=reader.GetString(7);
            if(owner!=request.OwnerUser||role!=request.OwnerRole)throw new UnauthorizedAccessException("المحاولة لا تخص مالك الطلب.");
            var kind=(CustomerMasterWriteKind)reader.GetByte(1);var outcome=(CustomerMasterWriteOutcome)reader.GetByte(8);
            string customerId=Text(reader,9),name=reader.GetString(3),phone=reader.GetString(4),address=reader.GetString(5);
            string action=outcome==CustomerMasterWriteOutcome.Canceled?"CustomerWriteCanceled":kind==CustomerMasterWriteKind.Create?"CustomerCreated":"CustomerUpdated";
            string details=outcome==CustomerMasterWriteOutcome.Canceled?CancelDetails(request.RequestId):"معرّف العميل: "+customerId+"، الاسم: "+name;
            if(!Enum.IsDefined(typeof(CustomerMasterWriteKind),kind)||!Enum.IsDefined(typeof(CustomerMasterWriteOutcome),outcome)||
               Text(reader,14)!=reader.GetString(11)||Text(reader,15)!=reader.GetString(12)||Text(reader,16)!=action||
               Text(reader,17)!="Customer"||Text(reader,18)!=details)
                throw new InvalidOperationException("سجل محاولة العميل لا يملك تدقيقًا مطابقًا.");
            bool expected=kind==request.Kind&&reader.GetString(2)==request.TargetCustomerId&&name==request.CustomerName&&phone==request.PhoneNumber&&address==request.Address;
            bool current=outcome==CustomerMasterWriteOutcome.Committed&&customerId==Text(reader,19)&&name==Text(reader,20)&&phone==Text(reader,21)&&address==(Text(reader,22)??string.Empty);
            return new CustomerMasterWriteReceipt(request.RequestId,customerId,reader.GetInt32(10),reader.GetDateTime(13),expected,current,outcome);
        }
        private async Task ValidateSchemaAsync(SqlConnection connection,CancellationToken token)
        {
            var expected=new Dictionary<string,string>(StringComparer.Ordinal){
                {"RequestId","uniqueidentifier:16:0"},{"RequestKind","tinyint:1:0"},{"TargetCustomerId","nvarchar:100:0"},
                {"CustomerName","nvarchar:400:0"},{"PhoneNumber","nvarchar:100:0"},{"CustomerAddress","nvarchar:600:0"},
                {"OwnerUser","nvarchar:400:0"},{"OwnerRole","nvarchar:100:0"},{"Outcome","tinyint:1:0"},
                {"CustomerId","nvarchar:100:1"},{"AuditId","int:4:0"},{"ExecutedUser","nvarchar:400:0"},
                {"ExecutedRole","nvarchar:100:0"},{"FinalizedAtUtc","datetime2:8:0"}};
            using(var command=new SqlCommand(@"SELECT c.name,t.name,c.max_length,c.is_nullable,c.is_computed,c.is_identity,c.scale
FROM sys.columns c JOIN sys.types t ON t.user_type_id=c.user_type_id WHERE c.object_id=OBJECT_ID(N'dbo.CustomerMasterRequestsTb');",connection))
            {
                command.CommandTimeout=10;
                using(var reader=await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                while(await reader.ReadAsync(token).ConfigureAwait(false))
                {
                    string name=reader.GetString(0),shape;
                    if(expected.TryGetValue(name,out shape))
                    {
                        string actual=reader.GetString(1)+":"+reader.GetInt16(2)+":"+(reader.GetBoolean(3)?"1":"0");
                        if(actual!=shape||reader.GetBoolean(4)||reader.GetBoolean(5)||name=="FinalizedAtUtc"&&reader.GetByte(6)!=7)
                            throw new CustomerMasterWriteRejectedException("مخطط محاولة العميل غير متوافق؛ راجع ترحيل قاعدة البيانات.");
                        expected.Remove(name);
                    }
                }
            }
            if(expected.Count!=0)throw new CustomerMasterWriteRejectedException("يلزم تثبيت ترحيل محاولة العميل قبل الحفظ أو التحقق.");
            using(var command=new SqlCommand(@"SELECT CASE WHEN
EXISTS(SELECT 1 FROM sys.indexes i JOIN sys.index_columns k ON k.object_id=i.object_id AND k.index_id=i.index_id
JOIN sys.columns c ON c.object_id=k.object_id AND c.column_id=k.column_id
WHERE i.object_id=OBJECT_ID(N'dbo.CustomerMasterRequestsTb') AND i.is_primary_key=1 AND i.is_unique=1 AND i.is_disabled=0
AND k.key_ordinal=1 AND c.name=N'RequestId'
AND (SELECT COUNT(*) FROM sys.index_columns x WHERE x.object_id=i.object_id AND x.index_id=i.index_id AND x.key_ordinal>0)=1)
AND EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.CustomerMasterRequestsTb')
 AND name=N'CK_CustomerMasterRequestsTb_Request' AND is_disabled=0 AND is_not_trusted=0)
THEN 1 ELSE 0 END;",connection))
            {
                command.CommandTimeout=10;
                if(Convert.ToInt32(await command.ExecuteScalarAsync(token).ConfigureAwait(false))!=1)
                    throw new CustomerMasterWriteRejectedException("مفتاح أو قيد محاولة العميل غير صالح.");
            }
            if(ownedPostCommitDelay)
            {
                using(var command=new SqlCommand("SELECT DB_NAME(),CONVERT(nvarchar(64),value) FROM sys.extended_properties WHERE class=0 AND name=N'QaTest.Owner';",connection))
                using(var reader=await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                {
                    if(!await reader.ReadAsync(token).ConfigureAwait(false)||
                      !System.Text.RegularExpressions.Regex.IsMatch(reader.GetString(0),@"\APharmacyConcurrency_[0-9]{14}_[0-9a-f]{8}_sandbox\z")||
                      !System.Text.RegularExpressions.Regex.IsMatch(reader.GetString(1),@"\A[0-9a-f]{32}\z"))
                        throw new CustomerMasterWriteRejectedException("حقن تأخير الالتزام مسموح في نسخة مملوكة مولدة فقط.");
                }
            }
        }
        public Task<CustomerMasterWriteReceipt> SubmitAsync(CustomerMasterWriteRequest request,OperationActor actor,Action authorizeBeforeSend,CancellationToken token)
            =>FinalizeAsync(request,actor,authorizeBeforeSend,token,false);
        public Task<CustomerMasterWriteReceipt> CancelAsync(CustomerMasterWriteRequest request,OperationActor actor,Action authorizeBeforeSend,CancellationToken token)
            =>FinalizeAsync(request,actor,authorizeBeforeSend,token,true);
        private async Task<CustomerMasterWriteReceipt> FinalizeAsync(CustomerMasterWriteRequest request,OperationActor actor,Action authorizeBeforeSend,CancellationToken token,bool cancel)
        {
            if(request==null)throw new ArgumentNullException(nameof(request));
            bool sent=false;
            try
            {
                using(var connection=connectionFactory())
                {
                    await connection.OpenAsync(token).ConfigureAwait(false);await ValidateSchemaAsync(connection,token).ConfigureAwait(false);
                    token.ThrowIfCancellationRequested();authorizeBeforeSend();token.ThrowIfCancellationRequested();
                    using(var command=new SqlCommand(SubmitSql(ownedPostCommitDelay),connection))
                    {
                        command.CommandTimeout=commandTimeout;Bind(command,request,actor,cancel);
                        sent=true;
                        using(var reader=await command.ExecuteReaderAsync(CancellationToken.None).ConfigureAwait(false))
                        {
                            if(!await reader.ReadAsync(CancellationToken.None).ConfigureAwait(false))throw new InvalidOperationException("لم يرجع مصدر الحفظ إثباتًا.");
                            var receipt=Map(reader,request);
                            if(await reader.ReadAsync(CancellationToken.None).ConfigureAwait(false)||await reader.NextResultAsync(CancellationToken.None).ConfigureAwait(false))
                                throw new InvalidOperationException("إثبات المحاولة ليس فريدًا.");
                            return receipt;
                        }
                    }
                }
            }
            catch(CustomerMasterWriteRejectedException){throw;}
            catch(SqlException error)when(error.Number==51061||error.Number==51062||error.Number==51063||error.Number==51064||error.Number==51068)
            {throw new CustomerMasterWriteRejectedException(error.Message,error);}
            catch(Exception error)
            {
                if(sent)throw new CustomerMasterWriteOutcomeUnknownException(request.RequestId,error);
                throw new CustomerMasterWriteRejectedException("لم ترسل كتابة العميل إلى SQL؛ أصلح السبب ثم حاول بنفس المحاولة.",error);
            }
        }
        public async Task<CustomerMasterWriteStatus> ResolveAsync(CustomerMasterWriteRequest request,OperationActor actor,CancellationToken token)
        {
            if(request==null)throw new ArgumentNullException(nameof(request));
            using(var connection=connectionFactory())
            {
                await connection.OpenAsync(token).ConfigureAwait(false);await ValidateSchemaAsync(connection,token).ConfigureAwait(false);
                using(var command=new SqlCommand(ResolveSql,connection))
                {
                    command.CommandTimeout=10;Bind(command,request,actor,false);
                    using(var reader=await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                    {
                        CustomerMasterWriteReceipt receipt=null;
                        if(await reader.ReadAsync(token).ConfigureAwait(false))
                        {
                            receipt=Map(reader,request);
                            if(await reader.ReadAsync(token).ConfigureAwait(false))throw new InvalidOperationException("حالة المحاولة ليست فريدة.");
                        }
                        if(await reader.NextResultAsync(token).ConfigureAwait(false))throw new InvalidOperationException("حالة المحاولة متعددة النتائج.");
                        token.ThrowIfCancellationRequested();return new CustomerMasterWriteStatus(receipt);
                    }
                }
            }
        }
    }
}
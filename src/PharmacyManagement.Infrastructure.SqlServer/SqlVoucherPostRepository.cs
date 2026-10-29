using System;
using System.Threading;
using System.Threading.Tasks;
using System.Data;
using System.Data.SqlClient;
using PharmacyManagement.Application;
using PharmacyManagement.Domain;

namespace PharmacyManagement.Infrastructure.SqlServer
{
    public sealed class SqlVoucherPostRepository : IAsyncIdempotentVoucherPostRepository
    {
        private readonly Func<SqlConnection> connectionFactory;
        private readonly Func<bool> contextCurrent;
        public SqlVoucherPostRepository(Func<SqlConnection> connectionFactory)
            : this(connectionFactory,()=>true) { }
        public SqlVoucherPostRepository(Func<SqlConnection> connectionFactory,Func<bool> contextCurrent)
        {
            this.connectionFactory=connectionFactory??throw new ArgumentNullException(nameof(connectionFactory));
            this.contextCurrent=contextCurrent??throw new ArgumentNullException(nameof(contextCurrent));
        }
        private void EnsureCurrent()
        {
            if(!contextCurrent())throw new UnauthorizedAccessException("تغيرت جلسة السند أو مصدره أو صلاحيته قبل الاعتماد.");
        }
        public VoucherPostResult Post(DateTime date, PreparedVoucher voucher, OperationActor actor)
        { return PostCore(date,voucher,actor,null); }
        public VoucherPostResult Post(Guid requestId, DateTime date, PreparedVoucher voucher, OperationActor actor)
        { return PostAsync(requestId,date,voucher,actor,CancellationToken.None).GetAwaiter().GetResult(); }
        public async Task<VoucherPostResult> PostAsync(Guid requestId, DateTime date, PreparedVoucher voucher, OperationActor actor, CancellationToken token)
        {
            if(requestId==Guid.Empty)throw new ArgumentException("Empty voucher request identity.",nameof(requestId));
            if(voucher==null||voucher.ExpectedSystemCurrencyCode==null)throw new InvalidOperationException("يتطلب طلب السند الثابت لقطة عملة المسودة.");
            return await PostCoreAsync(date,voucher,actor,requestId,token).ConfigureAwait(false);
        }
        private VoucherPostResult PostCore(DateTime date, PreparedVoucher voucher, OperationActor actor, Guid? requestId)
        { return PostCoreAsync(date,voucher,actor,requestId,CancellationToken.None).GetAwaiter().GetResult(); }
        private sealed class AttemptState { internal bool RequestWasAbsent; }
        private async Task<VoucherPostResult> PostCoreAsync(DateTime date, PreparedVoucher voucher, OperationActor actor, Guid? requestId, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            using(var connection=connectionFactory())
            {
                EnsureCurrent();
                await connection.OpenAsync(token).ConfigureAwait(false);
                using(var transaction=connection.BeginTransaction())
                {
                    bool commitStarted=false;var attempt=new AttemptState();
                    try
                    {
                        var result=await PostInTransactionCoreAsync(connection,transaction,date,voucher,actor,requestId,attempt,token).ConfigureAwait(false);
                        token.ThrowIfCancellationRequested();
                        EnsureCurrent();
                        commitStarted=true;transaction.Commit();return result;
                    }
                    catch(Exception error)
                    {
                        bool rollbackConfirmed=false;try{transaction.Rollback();rollbackConfirmed=true;}catch{/* Keep uncertain outcomes immutable. */}
                        if(!commitStarted&&token.IsCancellationRequested&&(error is SqlException||error is OperationCanceledException))
                            throw new OperationCanceledException("Voucher write cancelled before commit.",error,token);
                        if(requestId.HasValue&&attempt.RequestWasAbsent&&!commitStarted&&rollbackConfirmed&&!token.IsCancellationRequested&&!(error is OperationCanceledException)&&!(error is UnauthorizedAccessException)&&!(error is VoucherAcceptedRequestConflictException))
                            throw new VoucherPostRejectedException(requestId.Value,error);
                        throw;
                    }
                }
            }
        }
        public static VoucherPostResult PostInTransaction(SqlConnection connection, SqlTransaction transaction, DateTime date,
            PreparedVoucher voucher, OperationActor actor)
        { return PostInTransaction(connection,transaction,date,voucher,actor,null); }
        public static VoucherPostResult PostInTransaction(SqlConnection connection, SqlTransaction transaction, DateTime date,
            PreparedVoucher voucher, OperationActor actor, Guid? requestId)
        {
            return PostInTransactionCoreAsync(connection,transaction,date,voucher,actor,requestId,new AttemptState(),CancellationToken.None).GetAwaiter().GetResult();
        }
        private static async Task<VoucherPostResult> PostInTransactionCoreAsync(SqlConnection connection,SqlTransaction transaction,DateTime date,PreparedVoucher voucher,OperationActor actor,Guid? requestId,AttemptState attempt,CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if(voucher==null)throw new ArgumentNullException(nameof(voucher));
            if(actor==null)throw new ArgumentNullException(nameof(actor));
            if(requestId.HasValue)
            {
                if(requestId.Value==Guid.Empty||voucher.ExpectedSystemCurrencyCode==null)throw new InvalidOperationException("هوية السند أو لقطة عملته غير صالحة.");
                var existing=await ReadExistingRequestAsync(connection,transaction,requestId.Value,date,voucher,actor,token).ConfigureAwait(false);
                if(existing!=null)return existing;
                attempt.RequestWasAbsent=true;
            }
            await EnsureExpectedSystemCurrencyAsync(connection,transaction,voucher.ExpectedSystemCurrencyCode,token).ConfigureAwait(false);
            var journal=await SqlJournalWriter.CreateWithCurrencyAsync(connection,transaction,date,"Voucher",null,voucher.Description,actor.UserName,voucher.Lines,voucher.ExpectedSystemCurrencyCode,token).ConfigureAwait(false);
            int id;
            using(var command=new SqlCommand(@"DECLARE @CreatedVoucher TABLE (VoucherId int);
INSERT INTO dbo.AccountingVouchersTb (VoucherType, VoucherDate, PartyName, AccountCode, Amount, Notes, JournalEntryId, CreatedBy"+(requestId.HasValue?", RequestId, RequestCurrencyCode":"")+@")
OUTPUT INSERTED.VoucherId INTO @CreatedVoucher
VALUES (@VoucherType, @VoucherDate, @PartyName, @AccountCode, @Amount, @Notes, @JournalEntryId, @CreatedBy"+(requestId.HasValue?", @RequestId, @RequestCurrencyCode":"")+@");
SELECT VoucherId FROM @CreatedVoucher;",connection,transaction))
            {
                if(requestId.HasValue)
                {
                    command.Parameters.Add("@RequestId",SqlDbType.UniqueIdentifier).Value=requestId.Value;
                    command.Parameters.Add("@RequestCurrencyCode",SqlDbType.NVarChar,10).Value=voucher.ExpectedSystemCurrencyCode;
                }
                command.Parameters.Add("@VoucherType",SqlDbType.NVarChar,50).Value=voucher.Type;
                command.Parameters.Add("@VoucherDate",SqlDbType.DateTime).Value=date;
                command.Parameters.Add("@PartyName",SqlDbType.NVarChar,200).Value=DbText(voucher.PartyName);
                command.Parameters.Add("@AccountCode",SqlDbType.NVarChar,20).Value=voucher.AccountCode;
                var amount=command.Parameters.Add("@Amount",SqlDbType.Decimal);amount.Precision=18;amount.Scale=2;amount.Value=voucher.Amount;
                command.Parameters.Add("@Notes",SqlDbType.NVarChar,500).Value=DbText(voucher.Notes);
                command.Parameters.Add("@JournalEntryId",SqlDbType.Int).Value=journal.Id;
                command.Parameters.Add("@CreatedBy",SqlDbType.NVarChar,200).Value=DbText(actor.UserName);
                id=Convert.ToInt32(await command.ExecuteScalarAsync(token).ConfigureAwait(false));
            }
            using(var command=new SqlCommand(@"UPDATE dbo.JournalEntriesTb
SET SourceId = @VoucherId
WHERE JournalEntryId = @JournalEntryId;",connection,transaction))
            {
                command.Parameters.Add("@VoucherId",SqlDbType.NVarChar,50).Value=id.ToString(System.Globalization.CultureInfo.InvariantCulture);
                command.Parameters.Add("@JournalEntryId",SqlDbType.Int).Value=journal.Id;
                if(await command.ExecuteNonQueryAsync(token).ConfigureAwait(false)!=1)throw new InvalidOperationException("تعذر ربط السند بالقيد المحاسبي.");
            }
            using(var command=new SqlCommand(@"INSERT INTO dbo.AuditLogTb (UserName, UserRole, ActionName, EntityName, Details)
VALUES (@UserName, @UserRole, N'AccountingVoucherCreated', N'AccountingVouchers', @Details)",connection,transaction))
            {
                command.Parameters.Add("@UserName",SqlDbType.NVarChar,200).Value=actor.UserName;
                command.Parameters.Add("@UserRole",SqlDbType.NVarChar,50).Value=actor.UserRole;
                command.Parameters.Add("@Details",SqlDbType.NVarChar,1000).Value="تم إنشاء سند محاسبي. الرقم: "+id+"؛ النوع: "+voucher.Type+
                    "؛ القيمة: "+voucher.Amount.ToString("0.##")+"؛ الحساب المقابل: "+voucher.AccountCode+".";
                await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
            return new VoucherPostResult(id,journal.Id);
        }
        private static async Task<VoucherPostResult> ReadExistingRequestAsync(SqlConnection connection,SqlTransaction transaction,Guid requestId,DateTime date,PreparedVoucher voucher,OperationActor actor,CancellationToken token)
        {
            int id,journalId; bool matches;
            // The explicit filtered index both requires the migration and protects the absent-key range through commit.
            using(var command=new SqlCommand(@"SELECT VoucherId,VoucherDate,VoucherType,PartyName,AccountCode,Amount,Notes,JournalEntryId,CreatedBy,RequestCurrencyCode
FROM dbo.AccountingVouchersTb WITH(UPDLOCK,HOLDLOCK,INDEX(UX_AccountingVouchersTb_RequestId))
WHERE RequestId=@RequestId AND RequestId IS NOT NULL",connection,transaction))
            {
                command.Parameters.Add("@RequestId",SqlDbType.UniqueIdentifier).Value=requestId;
                using(var reader=await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                {
                    if(!(await reader.ReadAsync(token).ConfigureAwait(false)))return null;
                    id=reader.GetInt32(0);journalId=reader.GetInt32(7);
                    if(string.IsNullOrWhiteSpace(actor.UserName)||!string.Equals(reader.IsDBNull(8)?null:reader.GetString(8),actor.UserName.Trim(),StringComparison.OrdinalIgnoreCase))
                        throw new UnauthorizedAccessException("السند المعتمد لا يخص مستخدم الطلب.");
                    matches=new System.Data.SqlTypes.SqlDateTime(date).Value==reader.GetDateTime(1)&&
                        voucher.Type==reader.GetString(2)&&NormalizeText(voucher.PartyName)==NormalizeText(reader.IsDBNull(3)?null:reader.GetString(3))&&
                        voucher.AccountCode==reader.GetString(4)&&voucher.Amount==reader.GetDecimal(5)&&
                        NormalizeText(voucher.Notes)==NormalizeText(reader.IsDBNull(6)?null:reader.GetString(6))&&
                        voucher.ExpectedSystemCurrencyCode==(reader.IsDBNull(9)?null:reader.GetString(9));
                    if((await reader.ReadAsync(token).ConfigureAwait(false)))throw new InvalidOperationException("هوية السند ليست فريدة.");
                }
            }
            if(!matches)throw new VoucherAcceptedRequestConflictException(requestId,id,journalId);
            var journalRequest=new JournalPostRequest(date,"Voucher",id.ToString(System.Globalization.CultureInfo.InvariantCulture),voucher.Description,voucher.Lines);
            if(!(await SqlJournalRequestComparison.ExistingMatchesAsync(connection,transaction,journalRequest,actor,journalId,token).ConfigureAwait(false)))throw new VoucherAcceptedRequestConflictException(requestId,id,journalId);
            using(var command=new SqlCommand("SELECT SourceType,SourceId FROM dbo.JournalEntriesTb WHERE JournalEntryId=@Id",connection,transaction))
            {
                command.Parameters.Add("@Id",SqlDbType.Int).Value=journalId;
                using(var reader=await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                    if(!(await reader.ReadAsync(token).ConfigureAwait(false))||reader.IsDBNull(0)||reader.GetString(0)!="Voucher"||reader.IsDBNull(1)||reader.GetString(1)!=journalRequest.SourceId)
                        throw new InvalidOperationException("ربط السند المعتمد بالقيد غير صالح.");
            }
            return new VoucherPostResult(id,journalId,true);
        }
        private static string NormalizeText(string value) => string.IsNullOrWhiteSpace(value)?null:value.Trim();
        private static async Task EnsureExpectedSystemCurrencyAsync(SqlConnection connection,SqlTransaction transaction,string expectedCode,CancellationToken token)
        {
            // Legacy callers already pass base amounts without a draft currency snapshot.
            // New desktop drafts provide their captured code. Keep the currency stable through commit.
            if(expectedCode==null)return;
            using(var command=new SqlCommand("SELECT CurrencyCode FROM dbo.CurrencySettingsTb WITH(TABLOCK,HOLDLOCK) WHERE IsSystemCurrency=1",connection,transaction))
            using(var reader=await command.ExecuteReaderAsync(token).ConfigureAwait(false))
            {
                if(!(await reader.ReadAsync(token).ConfigureAwait(false))||reader.IsDBNull(0)||!string.Equals(reader.GetString(0).Trim(),expectedCode,StringComparison.OrdinalIgnoreCase)||(await reader.ReadAsync(token).ConfigureAwait(false)))
                    throw new InvalidOperationException("تغيرت عملة النظام أو إعدادها غير صالح؛ أعد تحميل السند ومراجعة المبلغ قبل الحفظ.");
            }
        }
        private static object DbText(string value) => string.IsNullOrWhiteSpace(value)?(object)DBNull.Value:value.Trim();
    }
}

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Application;
using PharmacyManagement.Domain;
namespace PharmacyManagement.Infrastructure.SqlServer
{
    public sealed class SqlVoucherSubmissionStatusRepository : IVoucherSubmissionStatusRepository
    {
        private readonly Func<SqlConnection> connectionFactory;
        public SqlVoucherSubmissionStatusRepository(Func<SqlConnection> connectionFactory)
        {this.connectionFactory=connectionFactory??throw new ArgumentNullException(nameof(connectionFactory));}
        private static string Text(string value)=>string.IsNullOrWhiteSpace(value)?null:value.Trim();
        private static string ReadText(SqlDataReader reader,int index)=>reader.IsDBNull(index)?null:reader.GetString(index);
        public async Task<VoucherSubmissionStatus> FindAsync(VoucherSubmissionStatusRequest request,OperationActor actor,CancellationToken token)
        {
            token.ThrowIfCancellationRequested();request=VoucherSubmissionStatusService.PrepareRequest(request);
            if(actor==null||string.IsNullOrWhiteSpace(actor.UserName))throw new UnauthorizedAccessException("مستخدم قراءة حالة السند مطلوب.");
            var expected=request.ExpectedRequest;var voucher=VoucherPostService.PrepareRequest(expected);
            using(var connection=connectionFactory())
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using(var transaction=connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        int id=0,journalId=0;bool matches=false;
                        using(var command=new SqlCommand(@"SELECT VoucherId,VoucherDate,VoucherType,PartyName,AccountCode,Amount,Notes,JournalEntryId,CreatedBy,RequestCurrencyCode
FROM dbo.AccountingVouchersTb WITH(HOLDLOCK,INDEX(UX_AccountingVouchersTb_RequestId))
WHERE RequestId=@RequestId AND RequestId IS NOT NULL",connection,transaction))
                        {
                            command.Parameters.Add("@RequestId",SqlDbType.UniqueIdentifier).Value=expected.RequestId.Value;
                            using(var reader=await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                            {
                                if(await reader.ReadAsync(token).ConfigureAwait(false))
                                {
                                    if(!string.Equals(ReadText(reader,8),actor.UserName.Trim(),StringComparison.OrdinalIgnoreCase))throw new UnauthorizedAccessException("السند المعتمد لا يخص مستخدم طلب الاستعادة.");
                                    id=reader.GetInt32(0);journalId=reader.GetInt32(7);
                                    if(id<=0||journalId<=0)throw new InvalidOperationException("معرفات السند المعتمد غير صالحة.");
                                    matches=new SqlDateTime(expected.Date).Value==reader.GetDateTime(1)&&voucher.Type==reader.GetString(2)&&Text(voucher.PartyName)==Text(ReadText(reader,3))&&voucher.AccountCode==reader.GetString(4)&&voucher.Amount==reader.GetDecimal(5)&&Text(voucher.Notes)==Text(ReadText(reader,6))&&voucher.ExpectedSystemCurrencyCode==ReadText(reader,9);
                                    if(await reader.ReadAsync(token).ConfigureAwait(false))throw new InvalidOperationException("هوية السند ليست فريدة.");
                                }
                            }
                        }
                        if(id>0)
                        {
                            DateTime date;string description;
                            using(var command=new SqlCommand("SELECT EntryDate,Description,CreatedBy,SourceType,SourceId FROM dbo.JournalEntriesTb WITH(HOLDLOCK) WHERE JournalEntryId=@Id",connection,transaction))
                            {
                                command.Parameters.Add("@Id",SqlDbType.Int).Value=journalId;
                                using(var reader=await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                                {
                                    if(!await reader.ReadAsync(token).ConfigureAwait(false))throw new InvalidOperationException("قيد السند المعتمد غير موجود.");
                                    if(!string.Equals(ReadText(reader,2),actor.UserName.Trim(),StringComparison.OrdinalIgnoreCase))throw new UnauthorizedAccessException("قيد السند لا يخص مستخدم طلب الاستعادة.");
                                    if(ReadText(reader,3)!="Voucher"||ReadText(reader,4)!=id.ToString(System.Globalization.CultureInfo.InvariantCulture))throw new InvalidOperationException("ربط السند بالقيد غير صالح.");
                                    date=reader.GetDateTime(0);description=ReadText(reader,1);
                                }
                            }
                            var lines=new List<JournalLineValues>();
                            using(var command=new SqlCommand("SELECT AccountCode,Debit,Credit,LineDescription,CostCenterId FROM dbo.JournalEntryLinesTb WITH(HOLDLOCK) WHERE JournalEntryId=@Id ORDER BY JournalEntryLineId",connection,transaction))
                            {
                                command.Parameters.Add("@Id",SqlDbType.Int).Value=journalId;
                                using(var reader=await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                                    while(await reader.ReadAsync(token).ConfigureAwait(false))lines.Add(new JournalLineValues(reader.GetString(0),reader.GetDecimal(1),reader.GetDecimal(2),ReadText(reader,3),reader.IsDBNull(4)?(int?)null:reader.GetInt32(4)));
                            }
                            var journal=new JournalPostRequest(expected.Date,"Voucher",id.ToString(System.Globalization.CultureInfo.InvariantCulture),voucher.Description,voucher.Lines);
                            matches=SqlJournalRequestComparison.Matches(journal,date,description,lines)&&matches;
                        }
                        token.ThrowIfCancellationRequested();transaction.Commit();return new VoucherSubmissionStatus(id,journalId,id>0?(bool?)matches:null);
                    }
                    catch
                    {
                        try{transaction.Rollback();}catch{/* Preserve the original failure. */}
                        if(token.IsCancellationRequested)throw new OperationCanceledException(token);throw;
                    }
                }
            }
        }
    }
}
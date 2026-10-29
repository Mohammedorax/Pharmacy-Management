using System;
using System.Threading;
using System.Threading.Tasks;
using System.Data;
using System.Data.SqlClient;
using PharmacyManagement.Application;

namespace PharmacyManagement.Infrastructure.SqlServer
{
    public sealed class SqlJournalPostRepository : IAsyncJournalPostRepository
    {
        private readonly Func<SqlConnection> connectionFactory;
        private readonly Func<bool> contextCurrent;
        public SqlJournalPostRepository(Func<SqlConnection> connectionFactory)
            : this(connectionFactory,()=>true) { }
        public SqlJournalPostRepository(Func<SqlConnection> connectionFactory,Func<bool> contextCurrent)
        {
            this.connectionFactory=connectionFactory??throw new ArgumentNullException(nameof(connectionFactory));
            this.contextCurrent=contextCurrent??throw new ArgumentNullException(nameof(contextCurrent));
        }
        private void EnsureCurrent()
        {
            if(!contextCurrent())throw new UnauthorizedAccessException("تغيرت جلسة القيد أو مصدره أو صلاحيته قبل الاعتماد.");
        }
        public JournalPostResult Post(JournalPostRequest request, OperationActor actor)
        { return PostAsync(request,actor,CancellationToken.None).GetAwaiter().GetResult(); }
        public async Task<JournalPostResult> PostAsync(JournalPostRequest request,OperationActor actor,CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (actor == null) throw new ArgumentNullException(nameof(actor));
            using (var connection = connectionFactory())
            {
                EnsureCurrent();await connection.OpenAsync(token).ConfigureAwait(false);
                using (var transaction = connection.BeginTransaction())
                {
                    bool commitStarted = false;
                    try
                    {
                        var result = await PostInTransactionAsync(connection, transaction, request, actor,token).ConfigureAwait(false);
                        token.ThrowIfCancellationRequested();EnsureCurrent();commitStarted = true; transaction.Commit(); return result;
                    }
                    catch (Exception error)
                    {
                        bool rollbackConfirmed = false;
                        try { transaction.Rollback(); rollbackConfirmed = true; } catch { /* Preserve an unknown outcome. */ }
                        if(!commitStarted&&token.IsCancellationRequested&&(error is SqlException||error is OperationCanceledException))
                            throw new OperationCanceledException("Journal write cancelled before commit.",error,token);
                        if (!commitStarted && rollbackConfirmed && !token.IsCancellationRequested && !(error is OperationCanceledException) && SqlJournalRequestComparison.IsManualIdentity(request) &&
                            !(error is JournalAcceptedRequestConflictException) && !(error is UnauthorizedAccessException))
                            throw new JournalPostRejectedException(request.SourceType, request.SourceId, error);
                        throw;
                    }
                }
            }
        }
        public static JournalPostResult PostInTransaction(SqlConnection connection, SqlTransaction transaction, JournalPostRequest request, OperationActor actor)
        { return PostInTransactionAsync(connection,transaction,request,actor,CancellationToken.None).GetAwaiter().GetResult(); }
        public static async Task<JournalPostResult> PostInTransactionAsync(SqlConnection connection, SqlTransaction transaction, JournalPostRequest request, OperationActor actor,CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (actor == null) throw new ArgumentNullException(nameof(actor));
            var result = await SqlJournalWriter.CreateWithCurrencyAsync(connection, transaction, request.Date, request.SourceType, request.SourceId,
                request.Description, actor.UserName, request.Lines,request.ExpectedSystemCurrencyCode,token).ConfigureAwait(false);
            if(result.AlreadyPosted&&SqlJournalRequestComparison.IsManualIdentity(request))
                if(!await SqlJournalRequestComparison.ExistingMatchesAsync(connection,transaction,request,actor,result.Id,token).ConfigureAwait(false))
                    throw new JournalAcceptedRequestConflictException(result.Id,request.SourceType,request.SourceId);
            if (!result.AlreadyPosted)
            {
                using (var command = new SqlCommand(@"INSERT INTO dbo.AuditLogTb (UserName, UserRole, ActionName, EntityName, Details)
VALUES (@UserName, @UserRole, N'JournalEntryCreated', N'Accounting', @Details)", connection, transaction))
                {
                    command.Parameters.Add("@UserName", SqlDbType.NVarChar, 200).Value = actor.UserName;
                    command.Parameters.Add("@UserRole", SqlDbType.NVarChar, 50).Value = actor.UserRole;
                    command.Parameters.Add("@Details", SqlDbType.NVarChar, 1000).Value = "تم إنشاء قيد محاسبي. رقم القيد: " + result.Id +
                        "؛ المصدر: " + (string.IsNullOrWhiteSpace(request.SourceType) ? "Manual" : request.SourceType.Trim()) + ".";
                    await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }
            }
            return result;
        }
    }
}

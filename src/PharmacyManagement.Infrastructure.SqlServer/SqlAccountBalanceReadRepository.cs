using System;
using System.Data;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Application;

namespace PharmacyManagement.Infrastructure.SqlServer
{
    public sealed class SqlAccountBalanceReadRepository : IAccountBalanceReadRepository
    {
        private const string BalanceSql = @"SELECT ISNULL(SUM(l.Debit - l.Credit), 0)
FROM dbo.JournalEntryLinesTb l
INNER JOIN dbo.JournalEntriesTb e ON e.JournalEntryId = l.JournalEntryId
WHERE l.AccountCode = @AccountCode
  AND (@ExclusiveEnd IS NULL OR e.EntryDate < @ExclusiveEnd);";
        private readonly Func<SqlConnection> connectionFactory;
        public SqlAccountBalanceReadRepository(Func<SqlConnection> connectionFactory)
        { this.connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory)); }
        public async Task<decimal> LoadAsync(AccountBalanceReadRequest request, CancellationToken cancellationToken)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using (var connection = connectionFactory()) using (var command = connection.CreateCommand())
                {
                    command.CommandText = BalanceSql; command.CommandTimeout = 15;
                    command.Parameters.Add("@AccountCode", SqlDbType.NVarChar, 20).Value = request.AccountCode;
                    command.Parameters.Add("@ExclusiveEnd", SqlDbType.DateTime2).Value = (object)request.ExclusiveEnd ?? DBNull.Value;
                    await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                    object value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();
                    return Convert.ToDecimal(value);
                }
            }
            catch (SqlException error) when (cancellationToken.IsCancellationRequested)
            { throw new OperationCanceledException("Account balance read was canceled.", error, cancellationToken); }
        }
        public decimal Load(AccountBalanceReadRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            using (var connection = connectionFactory())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = BalanceSql;
                command.Parameters.Add("@AccountCode", SqlDbType.NVarChar, 20).Value = request.AccountCode;
                command.Parameters.Add("@ExclusiveEnd", SqlDbType.DateTime2).Value = (object)request.ExclusiveEnd ?? DBNull.Value;
                connection.Open();
                return Convert.ToDecimal(command.ExecuteScalar());
            }
        }
    }
}

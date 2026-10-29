using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Application;

namespace PharmacyManagement.Infrastructure.SqlServer
{
    public sealed class SqlAccountingLookupReadRepository : IAccountingLookupReadRepository
    {
        private const string AccountsSql = @"SELECT AccountCode, AccountName, AccountType, ParentAccountCode, IsActive
FROM dbo.AccountsTb WHERE (@ActiveOnly = 0 OR IsActive = 1) ORDER BY AccountCode";
        private const string CostCentersSql = @"SELECT CostCenterId, CostCenterName
FROM dbo.CostCentersTb WHERE IsActive = 1 ORDER BY CostCenterName";
        private readonly Func<SqlConnection> connectionFactory;
        public SqlAccountingLookupReadRepository(Func<SqlConnection> connectionFactory)
        { this.connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory)); }
        public IReadOnlyList<AccountLookupRow> LoadAccounts(bool activeOnly)
        {
            using (var connection = connectionFactory())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = AccountsSql;
                command.Parameters.Add("@ActiveOnly", SqlDbType.Bit).Value = activeOnly;
                connection.Open(); var rows = new List<AccountLookupRow>();
                using (var reader = command.ExecuteReader()) while (reader.Read())
                    rows.Add(new AccountLookupRow(reader.GetString(0), reader.GetString(1), reader.GetString(2),
                        reader.IsDBNull(3) ? null : reader.GetString(3), reader.GetBoolean(4)));
                return rows.AsReadOnly();
            }
        }
        public async Task<IReadOnlyList<AccountLookupRow>> LoadAccountsAsync(bool activeOnly, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using (var connection = connectionFactory())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = AccountsSql;
                command.Parameters.Add("@ActiveOnly", SqlDbType.Bit).Value = activeOnly;
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false); var rows = new List<AccountLookupRow>();
                using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false)) while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    rows.Add(new AccountLookupRow(reader.GetString(0), reader.GetString(1), reader.GetString(2),
                        reader.IsDBNull(3) ? null : reader.GetString(3), reader.GetBoolean(4)));
                return rows.AsReadOnly();
            }
        }
        public IReadOnlyList<CostCenterLookupRow> LoadCostCenters()
        {
            using (var connection = connectionFactory())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = CostCentersSql;
                connection.Open(); var rows = new List<CostCenterLookupRow>();
                using (var reader = command.ExecuteReader()) while (reader.Read())
                    rows.Add(new CostCenterLookupRow(reader.GetInt32(0), reader.GetString(1)));
                return rows.AsReadOnly();
            }
        }
        public async Task<IReadOnlyList<CostCenterLookupRow>> LoadCostCentersAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using (var connection = connectionFactory())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = CostCentersSql;
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false); var rows = new List<CostCenterLookupRow>();
                using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false)) while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    rows.Add(new CostCenterLookupRow(reader.GetInt32(0), reader.GetString(1)));
                return rows.AsReadOnly();
            }
        }
    }
}

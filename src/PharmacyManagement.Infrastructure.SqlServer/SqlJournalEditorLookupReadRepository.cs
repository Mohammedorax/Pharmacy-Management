using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Application;
namespace PharmacyManagement.Infrastructure.SqlServer
{
    public sealed class SqlJournalEditorLookupReadRepository:IJournalEditorLookupReadRepository
    {
        private readonly Func<SqlConnection> connectionFactory;
        public SqlJournalEditorLookupReadRepository(Func<SqlConnection> connectionFactory)
        {this.connectionFactory=connectionFactory??throw new ArgumentNullException(nameof(connectionFactory));}
        public async Task<JournalEditorLookupSnapshot> LoadAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using(var connection=connectionFactory())using(var command=connection.CreateCommand())
            {
                command.CommandText=@"SELECT AccountCode,AccountName,AccountType,ParentAccountCode,IsActive FROM dbo.AccountsTb WHERE IsActive=1 ORDER BY AccountCode;
SELECT CostCenterId,CostCenterName FROM dbo.CostCentersTb WHERE IsActive=1 ORDER BY CostCenterName;
SELECT CurrencyId,CurrencyCode,CurrencySymbol,IsSystemCurrency FROM dbo.CurrencySettingsTb ORDER BY IsSystemCurrency DESC,CountryNameAr;";
                command.CommandTimeout=15;await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                using(var reader=await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                {
                    var accounts=new List<AccountLookupRow>();var centers=new List<CostCenterLookupRow>();var currencies=new List<JournalCurrencyLookupRow>();
                    while(await reader.ReadAsync(cancellationToken).ConfigureAwait(false))accounts.Add(new AccountLookupRow(reader.GetString(0),reader.GetString(1),reader.GetString(2),reader.IsDBNull(3)?null:reader.GetString(3),reader.GetBoolean(4)));
                    if(!await reader.NextResultAsync(cancellationToken).ConfigureAwait(false))throw new InvalidOperationException("Missing cost center result.");
                    while(await reader.ReadAsync(cancellationToken).ConfigureAwait(false))centers.Add(new CostCenterLookupRow(reader.GetInt32(0),reader.GetString(1)));
                    if(!await reader.NextResultAsync(cancellationToken).ConfigureAwait(false))throw new InvalidOperationException("Missing currency result.");
                    while(await reader.ReadAsync(cancellationToken).ConfigureAwait(false))currencies.Add(new JournalCurrencyLookupRow(reader.GetInt32(0),reader.IsDBNull(1)?null:reader.GetString(1),reader.IsDBNull(2)?null:reader.GetString(2),reader.GetBoolean(3)));
                    cancellationToken.ThrowIfCancellationRequested();return new JournalEditorLookupSnapshot(accounts,centers,currencies);
                }
            }
        }
    }
}

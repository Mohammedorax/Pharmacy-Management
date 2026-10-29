using System;
using System.Data;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Application;

namespace PharmacyManagement.Infrastructure.SqlServer
{
    public sealed class SqlVoucherDocumentReadRepository : IVoucherDocumentReadRepository
    {
        private const string DocumentSql = @"SELECT v.VoucherId, v.VoucherDate, v.VoucherType, v.PartyName, v.AccountCode,
v.Amount, v.Notes, v.JournalEntryId, v.CreatedBy, j.SystemCurrencyCode
FROM dbo.AccountingVouchersTb v
LEFT JOIN dbo.JournalEntriesTb j ON j.JournalEntryId = v.JournalEntryId
WHERE v.VoucherId = @VoucherId;";
        private readonly Func<SqlConnection> connectionFactory;
        public SqlVoucherDocumentReadRepository(Func<SqlConnection> connectionFactory)
        { this.connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory)); }
        public async Task<VoucherDocumentSnapshot> LoadAsync(VoucherDocumentReadRequest request,CancellationToken cancellationToken)
        {
            if(request==null)throw new ArgumentNullException(nameof(request));cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using(var connection=connectionFactory())using(var command=connection.CreateCommand())
                {
                    command.CommandText=DocumentSql;command.CommandTimeout=15;
                    command.Parameters.Add("@VoucherId",SqlDbType.Int).Value=request.VoucherId;
                    await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                    using(var reader=await command.ExecuteReaderAsync(System.Data.CommandBehavior.SingleRow,cancellationToken).ConfigureAwait(false))
                    {
                        if(!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))return null;
                        var snapshot=ReadSnapshot(reader,true);cancellationToken.ThrowIfCancellationRequested();return snapshot;
                    }
                }
            }
            catch(SqlException error) when(cancellationToken.IsCancellationRequested)
            {throw new OperationCanceledException("Voucher document read was canceled.",error,cancellationToken);}
        }
        public VoucherDocumentSnapshot Load(VoucherDocumentReadRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            using (var connection = connectionFactory())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = DocumentSql;
                command.Parameters.Add("@VoucherId", SqlDbType.Int).Value = request.VoucherId;
                connection.Open();
                using (var reader = command.ExecuteReader(CommandBehavior.SingleRow))
                {
                    if (!reader.Read()) return null;
                    return ReadSnapshot(reader,true);
                }
            }
        }
        internal static VoucherDocumentSnapshot ReadSnapshot(SqlDataReader reader)
        { return ReadSnapshot(reader,false); }
        private static VoucherDocumentSnapshot ReadSnapshot(SqlDataReader reader,bool includeSystemCurrency)
        {
                    return new VoucherDocumentSnapshot(reader.GetInt32(0), reader.GetDateTime(1), Text(reader, 2),
                        Text(reader, 3), Text(reader, 4), reader.GetDecimal(5), Text(reader, 6),
                        reader.IsDBNull(7) ? (int?)null : reader.GetInt32(7), Text(reader, 8),includeSystemCurrency&&!reader.IsDBNull(9)?reader.GetString(9):null);
        }
        private static string Text(SqlDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);
    }
}


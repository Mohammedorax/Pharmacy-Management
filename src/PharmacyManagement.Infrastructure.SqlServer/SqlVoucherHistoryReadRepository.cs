using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Application;
namespace PharmacyManagement.Infrastructure.SqlServer
{
    public sealed class SqlVoucherHistoryReadRepository : IVoucherHistoryReadRepository
    {
        private const string HistorySql=@"SELECT VoucherId, VoucherDate, VoucherType, PartyName, AccountCode,
Amount, Notes, JournalEntryId, CreatedBy FROM dbo.AccountingVouchersTb
WHERE VoucherDate >= @From AND (@End IS NULL OR VoucherDate < @End)
AND (@Type = N'' OR VoucherType = @Type) ORDER BY VoucherDate DESC, VoucherId DESC;";
        private readonly Func<SqlConnection> factory;
        public SqlVoucherHistoryReadRepository(Func<SqlConnection> factory)
        { this.factory = factory ?? throw new ArgumentNullException(nameof(factory)); }
        public IReadOnlyList<VoucherDocumentSnapshot> Load(VoucherHistoryReadRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            var rows = new List<VoucherDocumentSnapshot>();
            using (var connection = factory())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = HistorySql;
                command.Parameters.Add("@From", SqlDbType.DateTime2).Value = request.From;
                command.Parameters.Add("@End", SqlDbType.DateTime2).Value = (object)request.ExclusiveEnd ?? DBNull.Value;
                command.Parameters.Add("@Type", SqlDbType.NVarChar, 50).Value = request.Type;
                connection.Open();
                using (var reader = command.ExecuteReader())
                    while (reader.Read()) rows.Add(SqlVoucherDocumentReadRepository.ReadSnapshot(reader));
            }
            return rows.AsReadOnly();
        }
        public async Task<IReadOnlyList<VoucherDocumentSnapshot>> LoadAsync(VoucherHistoryReadRequest request,CancellationToken cancellationToken)
        {
            if(request==null)throw new ArgumentNullException(nameof(request));
            cancellationToken.ThrowIfCancellationRequested();
            var rows=new List<VoucherDocumentSnapshot>();
            try
            {
                using(var connection=factory())using(var command=connection.CreateCommand())
                {
                    command.CommandText=HistorySql;command.CommandTimeout=15;
                    command.Parameters.Add("@From",SqlDbType.DateTime2).Value=request.From;
                    command.Parameters.Add("@End",SqlDbType.DateTime2).Value=(object)request.ExclusiveEnd??DBNull.Value;
                    command.Parameters.Add("@Type",SqlDbType.NVarChar,50).Value=request.Type;
                    await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                    using(var reader=await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                        while(await reader.ReadAsync(cancellationToken).ConfigureAwait(false))rows.Add(SqlVoucherDocumentReadRepository.ReadSnapshot(reader));
                }
                cancellationToken.ThrowIfCancellationRequested();return rows.AsReadOnly();
            }
            catch(SqlException error) when(cancellationToken.IsCancellationRequested)
            {throw new OperationCanceledException("Voucher history read was canceled.",error,cancellationToken);}
        }
    }
}

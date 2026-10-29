using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using System.Data.SqlClient;
using PharmacyManagement.Application;
namespace PharmacyManagement.Infrastructure.SqlServer
{
    public sealed class SqlJournalDocumentReadRepository : IJournalDocumentReadRepository
    {
        private const string DocumentSql=@"SELECT e.JournalEntryId,e.EntryDate,e.SourceType,e.SourceId,e.Description,e.CreatedBy,
l.JournalEntryLineId,l.AccountCode,a.AccountName,l.Debit,l.Credit,l.LineDescription,l.CostCenterId,c.CostCenterName,e.SystemCurrencyCode
FROM dbo.JournalEntriesTb e
LEFT JOIN dbo.JournalEntryLinesTb l ON l.JournalEntryId=e.JournalEntryId
LEFT JOIN dbo.AccountsTb a ON a.AccountCode=l.AccountCode
LEFT JOIN dbo.CostCentersTb c ON c.CostCenterId=l.CostCenterId
WHERE e.JournalEntryId=@Id ORDER BY l.JournalEntryLineId;";
        private readonly Func<SqlConnection> factory;
        public SqlJournalDocumentReadRepository(Func<SqlConnection> factory){this.factory=factory??throw new ArgumentNullException(nameof(factory));}
        public JournalDocumentSnapshot Load(JournalDocumentReadRequest request)
        {
            if(request==null)throw new ArgumentNullException(nameof(request));
            using(var connection=factory())
            using(var command=connection.CreateCommand())
            {
                command.CommandText=DocumentSql;
                command.Parameters.Add("@Id",SqlDbType.Int).Value=request.Id;
                connection.Open();
                using(var reader=command.ExecuteReader())
                {
                    if(!reader.Read())return null;
                    int id=reader.GetInt32(0);DateTime date=reader.GetDateTime(1);
                    string type=Text(reader,2),source=Text(reader,3),description=Text(reader,4),user=Text(reader,5);
                    string systemCurrencyCode=Text(reader,14);
                    var lines=new List<JournalDocumentLine>();
                    do
                    {
                        if(!reader.IsDBNull(6))lines.Add(new JournalDocumentLine(reader.GetInt32(6),Text(reader,7),Text(reader,8),
                            reader.GetDecimal(9),reader.GetDecimal(10),Text(reader,11),reader.IsDBNull(12)?(int?)null:reader.GetInt32(12),Text(reader,13)));
                    }while(reader.Read());
                    return new JournalDocumentSnapshot(id,date,type,source,description,user,lines,systemCurrencyCode);
                }
            }
        }
        public async Task<JournalDocumentSnapshot> LoadAsync(JournalDocumentReadRequest request,CancellationToken cancellationToken)
        {
            if(request==null)throw new ArgumentNullException(nameof(request));cancellationToken.ThrowIfCancellationRequested();
            try
            {
            using(var connection=factory())using(var command=connection.CreateCommand())
            {
                command.CommandText=DocumentSql;command.CommandTimeout=15;command.Parameters.Add("@Id",SqlDbType.Int).Value=request.Id;
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                using(var reader=await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                {
                    if(!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))return null;
                    int id=reader.GetInt32(0);DateTime date=reader.GetDateTime(1);
                    string type=Text(reader,2),source=Text(reader,3),description=Text(reader,4),user=Text(reader,5);
                    string systemCurrencyCode=Text(reader,14);
                    var lines=new List<JournalDocumentLine>();
                    do
                    {
                        if(!reader.IsDBNull(6))lines.Add(new JournalDocumentLine(reader.GetInt32(6),Text(reader,7),Text(reader,8),
                            reader.GetDecimal(9),reader.GetDecimal(10),Text(reader,11),reader.IsDBNull(12)?(int?)null:reader.GetInt32(12),Text(reader,13)));
                    }while(await reader.ReadAsync(cancellationToken).ConfigureAwait(false));
                    cancellationToken.ThrowIfCancellationRequested();return new JournalDocumentSnapshot(id,date,type,source,description,user,lines,systemCurrencyCode);
                }
            }
            }
            catch(SqlException error) when(cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException("Saved journal read was canceled.",error,cancellationToken);
            }
        }
        private static string Text(SqlDataReader reader,int ordinal)=>reader.IsDBNull(ordinal)?null:reader.GetString(ordinal);
    }
}

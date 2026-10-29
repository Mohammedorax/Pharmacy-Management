using System;
using System.Data;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Application;

namespace PharmacyManagement.Infrastructure.SqlServer
{
    public sealed class SqlInvoiceIssuerReadRepository : IInvoiceIssuerReadRepository
    {
        private readonly Func<SqlConnection> connectionFactory;
        public SqlInvoiceIssuerReadRepository(Func<SqlConnection> connectionFactory)
        {this.connectionFactory=connectionFactory??throw new ArgumentNullException(nameof(connectionFactory));}
        public async Task<InvoiceIssuerProfile> LoadAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using(var connection=connectionFactory())using(var command=connection.CreateCommand())
            {
                command.CommandText=@"SELECT NameAr, NameEn, CommercialRegNo, Mobile, Phone, TaxNo, Address, Website, LogoPath
FROM dbo.PharmacyProfileTb WHERE ProfileKey = @ProfileKey";
                command.Parameters.Add("@ProfileKey",SqlDbType.NVarChar,20).Value="MAIN";command.CommandTimeout=10;
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                using(var reader=await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                {
                    if(!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))return null;
                    cancellationToken.ThrowIfCancellationRequested();
                    return new InvoiceIssuerProfile(Text(reader,"NameAr"),Text(reader,"NameEn"),Text(reader,"CommercialRegNo"),
                        Text(reader,"Mobile"),Text(reader,"Phone"),Text(reader,"TaxNo"),Text(reader,"Address"),Text(reader,"Website"),Text(reader,"LogoPath"));
                }
            }
        }
        private static string Text(SqlDataReader reader,string name)
        {int index=reader.GetOrdinal(name);return reader.IsDBNull(index)?string.Empty:reader.GetString(index);}
    }
}

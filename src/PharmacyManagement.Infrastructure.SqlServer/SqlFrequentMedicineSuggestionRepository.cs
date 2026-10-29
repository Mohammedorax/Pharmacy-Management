using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Application;
namespace PharmacyManagement.Infrastructure.SqlServer
{
    public sealed class SqlFrequentMedicineSuggestionRepository:IFrequentMedicineSuggestionRepository
    {
        private readonly Func<SqlConnection> connectionFactory;
        public SqlFrequentMedicineSuggestionRepository(Func<SqlConnection> connectionFactory)
        {this.connectionFactory=connectionFactory??throw new ArgumentNullException(nameof(connectionFactory));}
        public async Task<IReadOnlyList<MedicineSearchItem>> ReadNamedAsync(IReadOnlyList<string> canonicalNames,CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if(canonicalNames==null||canonicalNames.Count>20)throw new ArgumentException("Provide at most20 names.");
            var names=canonicalNames.ToArray();
            if(names.Any(x=>string.IsNullOrWhiteSpace(x)||x.Length>200)||names.Distinct(StringComparer.Ordinal).Count()!=names.Length)
                throw new ArgumentException("Invalid canonical names.");
            if(names.Length==0)return new List<MedicineSearchItem>().AsReadOnly();
            using(var connection=connectionFactory())
            using(var command=connection.CreateCommand())
            {
                command.CommandTimeout=10;
                var parameters=new List<string>();
                for(int i=0;i<names.Length;i++){
                    string parameter="@Name"+i.ToString(CultureInfo.InvariantCulture);
                    parameters.Add(parameter);command.Parameters.Add(parameter,SqlDbType.NVarChar,200).Value=names[i];
                }
                command.CommandText="SELECT TOP(21) MedName,ISNULL(Barcode,N'') Barcode,CompName,MedQty,Sprice,ExpDate FROM dbo.MedicineTb1 WHERE MedName IN("+
                    string.Join(",",parameters)+") AND MedQty>0 AND Sprice>0;";
                await connection.OpenAsync(token).ConfigureAwait(false);
                var rows=new List<MedicineSearchItem>();
                using(var reader=await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                {
                    while(await reader.ReadAsync(token).ConfigureAwait(false))
                    {
                        if(rows.Count>=20)throw new InvalidOperationException("Catalogue names are not unique.");
                        rows.Add(new MedicineSearchItem{MedName=Convert.ToString(reader["MedName"],CultureInfo.InvariantCulture),
                            Barcode=Convert.ToString(reader["Barcode"],CultureInfo.InvariantCulture),CompName=Convert.ToString(reader["CompName"],CultureInfo.InvariantCulture),
                            MedQty=reader["MedQty"]==DBNull.Value?(decimal?)null:Convert.ToDecimal(reader["MedQty"],CultureInfo.InvariantCulture),
                            Sprice=reader["Sprice"]==DBNull.Value?(decimal?)null:Convert.ToDecimal(reader["Sprice"],CultureInfo.InvariantCulture),
                            ExpDate=reader["ExpDate"]==DBNull.Value?(DateTime?)null:Convert.ToDateTime(reader["ExpDate"],CultureInfo.InvariantCulture)});
                    }
                }
                token.ThrowIfCancellationRequested();
                return rows.AsReadOnly();
            }
        }
    }
}

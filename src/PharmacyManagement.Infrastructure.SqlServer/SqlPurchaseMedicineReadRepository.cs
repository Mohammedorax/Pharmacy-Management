using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Application;
namespace PharmacyManagement.Infrastructure.SqlServer
{
    public sealed class SqlPurchaseMedicineReadRepository:IPurchaseMedicineReadRepository
    {
        private readonly Func<SqlConnection> connectionFactory;
        public SqlPurchaseMedicineReadRepository(Func<SqlConnection> connectionFactory)
        {this.connectionFactory=connectionFactory??throw new ArgumentNullException(nameof(connectionFactory));}
        public async Task<IReadOnlyList<string>> GetNamesAsync(string supplier,CancellationToken token)
        {
            var rows=new List<string>();
            using(var connection=connectionFactory())
            using(var command=connection.CreateCommand())
            {
                command.CommandText=string.IsNullOrWhiteSpace(supplier)?"SELECT MedName FROM dbo.MedicineTb1 ORDER BY MedName;":"SELECT MedName FROM dbo.MedicineTb1 WHERE CompName=@SupplierName ORDER BY MedName;";
                if(!string.IsNullOrWhiteSpace(supplier))command.Parameters.Add("@SupplierName",SqlDbType.NVarChar,200).Value=supplier;
                await connection.OpenAsync(token).ConfigureAwait(false);
                using(var reader=await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                    while(await reader.ReadAsync(token).ConfigureAwait(false))rows.Add(reader.IsDBNull(0)?"":Convert.ToString(reader[0]));
            }
            return rows.AsReadOnly();
        }
        public async Task<PurchaseMedicineSnapshot> FindAsync(string name,CancellationToken token)
        {
            using(var connection=connectionFactory())
            using(var command=connection.CreateCommand())
            {
                command.CommandText="SELECT MedName, MedQty, Bprice, CompName FROM dbo.MedicineTb1 WHERE MedName=@MedName;";
                command.Parameters.Add("@MedName",SqlDbType.NVarChar,200).Value=name;
                await connection.OpenAsync(token).ConfigureAwait(false);
                using(var reader=await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                {
                    if(!await reader.ReadAsync(token).ConfigureAwait(false))return null;
                    var row=new PurchaseMedicineSnapshot{MedicineName=reader.IsDBNull(0)?"":Convert.ToString(reader[0]),StockQuantity=Convert.ToInt32(reader[1]),BuyingPrice=Convert.ToInt32(reader[2]),SupplierName=reader.IsDBNull(3)?"":Convert.ToString(reader[3])};
                    while(await reader.ReadAsync(token).ConfigureAwait(false)){}
                    return row;
                }
            }
        }
    }
}

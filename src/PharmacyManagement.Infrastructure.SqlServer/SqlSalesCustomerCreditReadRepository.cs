using System;
using System.Data;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Application;
namespace PharmacyManagement.Infrastructure.SqlServer
{
    public sealed class SqlSalesCustomerCreditReadRepository:ISalesCustomerCreditReadRepository
    {
        private readonly Func<SqlConnection> connectionFactory;
        public SqlSalesCustomerCreditReadRepository(Func<SqlConnection> factory)
        {connectionFactory=factory??throw new ArgumentNullException(nameof(factory));}
        public async Task<SalesCustomerCreditSnapshot> ReadAsync(SalesCustomerCreditReadRequest request,CancellationToken token)
        {
            if(request==null)throw new ArgumentNullException(nameof(request));
            token.ThrowIfCancellationRequested();
            const string sql=@"IF OBJECT_ID(N'dbo.CustomerAccountLedgerTb',N'U') IS NULL SELECT CAST(0 AS DECIMAL(18,2));
ELSE SELECT ISNULL(SUM(Credit-Debit),0) FROM dbo.CustomerAccountLedgerTb WHERE CustomerId=@CustomerId;";
            using(var connection=connectionFactory())
            using(var command=new SqlCommand(sql,connection))
            {
                command.CommandTimeout=30;
                command.Parameters.Add("@CustomerId",SqlDbType.NVarChar,50).Value=request.CustomerId;
                try
                {
                    await connection.OpenAsync(token).ConfigureAwait(false);
                    object value=await command.ExecuteScalarAsync(token).ConfigureAwait(false);
                    token.ThrowIfCancellationRequested();
                    return new SalesCustomerCreditSnapshot(request.CustomerId,Convert.ToDecimal(value));
                }
                catch(SqlException)when(token.IsCancellationRequested){throw new OperationCanceledException(token);}
            }
        }
    }
}
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Application;
namespace PharmacyManagement.Infrastructure.SqlServer
{
    public sealed class SqlCustomerReferenceReadRepository : ICustomerReferenceReadRepository
    {
        private readonly Func<SqlConnection> connectionFactory;
        public SqlCustomerReferenceReadRepository(Func<SqlConnection> factory)
        {connectionFactory=factory??throw new ArgumentNullException(nameof(factory));}
        public async Task<CustomerReferenceReadResult> ReadAsync(CustomerReferenceReadRequest request,CancellationToken token)
        {
            if(request==null)throw new ArgumentNullException(nameof(request));
            token.ThrowIfCancellationRequested();
            if(request.IsLookup&&request.Value.Length==0)return new CustomerReferenceReadResult(request.Kind,new string[0],null);
            string column;
            switch(request.Kind)
            {
                case CustomerReferenceKind.Names:case CustomerReferenceKind.ByName:column="CustomerName";break;
                case CustomerReferenceKind.Phones:case CustomerReferenceKind.ByPhone:column="PhoneNumber";break;
                case CustomerReferenceKind.ById:column="CustomerId";break;
                default:throw new ArgumentOutOfRangeException(nameof(request));
            }
            string sql=request.IsLookup
                ? "SELECT TOP (1) CustomerId, CustomerName, PhoneNumber, CustomerAddress, TotalSpent FROM dbo.CustomersBillsTb WHERE " + column + " = @Value ORDER BY InvoiceDate DESC, CustomerId;"
                : "SELECT DISTINCT " + column + " FROM dbo.CustomersBillsTb WHERE CustomerId <> N'0' AND NULLIF(LTRIM(RTRIM(" + column + ")), N'') IS NOT NULL ORDER BY " + column + ";";
            var values=new List<string>();CustomerReferenceSnapshot customer=null;
            using(var connection=connectionFactory())
            using(var command=new SqlCommand(sql,connection))
            {
                command.CommandTimeout=10;
                if(request.IsLookup)command.Parameters.Add("@Value",SqlDbType.NVarChar,request.Kind==CustomerReferenceKind.ByName?200:50).Value=request.Value;
                try
                {
                    await connection.OpenAsync(token).ConfigureAwait(false);
                    using(var reader=await command.ExecuteReaderAsync(request.IsLookup?CommandBehavior.SingleRow:CommandBehavior.Default,token).ConfigureAwait(false))
                    {
                        if(request.IsLookup)
                        {
                            if(await reader.ReadAsync(token).ConfigureAwait(false))customer=new CustomerReferenceSnapshot(
                                Convert.ToString(reader["CustomerId"]),Convert.ToString(reader["CustomerName"]),Convert.ToString(reader["PhoneNumber"]),
                                reader.IsDBNull(reader.GetOrdinal("CustomerAddress"))?string.Empty:Convert.ToString(reader["CustomerAddress"]),
                                reader.IsDBNull(reader.GetOrdinal("TotalSpent"))?0m:Convert.ToDecimal(reader["TotalSpent"]));
                        }
                        else while(await reader.ReadAsync(token).ConfigureAwait(false))
                        {string value=Convert.ToString(reader[0]).Trim();if(value.Length>0)values.Add(value);}
                    }
                }
                catch(SqlException)when(token.IsCancellationRequested){throw new OperationCanceledException(token);}
            }
            token.ThrowIfCancellationRequested();
            return new CustomerReferenceReadResult(request.Kind,values,customer);
        }
    }
}
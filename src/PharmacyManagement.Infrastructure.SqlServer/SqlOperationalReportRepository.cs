using System;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Application;

namespace PharmacyManagement.Infrastructure.SqlServer
{
    public sealed class SqlOperationalReportRepository : IOperationalReportAsyncRepository
    {
        private readonly Func<SqlConnection> connectionFactory;
        public SqlOperationalReportRepository(Func<SqlConnection> connectionFactory)
        { this.connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory)); }

        public async Task<OperationalReportResult> LoadAsync(OperationalReportRequest request,CancellationToken cancellationToken)
        {
            if(request==null)throw new ArgumentNullException(nameof(request));cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using(var connection=connectionFactory())using(var command=connection.CreateCommand())
                {
                    command.CommandTimeout=30;command.CommandText=SqlOperationalReportQueries.Get(request.Kind, request.ViewMode);
                    command.Parameters.Add("@Take",SqlDbType.Int).Value=OperationalReportRequest.MaximumRows;
                    command.Parameters.Add("@FromDate",SqlDbType.DateTime).Value=request.FromDate;
                    command.Parameters.Add("@ToDate",SqlDbType.DateTime).Value=request.ToDateExclusive;
                    command.Parameters.Add("@Search",SqlDbType.NVarChar,ReportSearchCriteria.MaximumLength).Value=request.SearchText;
                    AddReturnDateParameters(command, request);
                    await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                    using(var reader=await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                    {
                        while(reader.FieldCount==0&&await reader.NextResultAsync(cancellationToken).ConfigureAwait(false)){}
                        var columns=Enumerable.Range(0,reader.FieldCount).Select(i=>new OperationalReportColumn(reader.GetName(i),reader.GetFieldType(i))).ToArray();
                        var rows=new List<object[]>();
                        while(await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                        {
                            var values=new object[reader.FieldCount];reader.GetValues(values);
                            for(int i=0;i<values.Length;i++)if(values[i]==DBNull.Value)values[i]=null;
                            rows.Add(values);
                        }
                        cancellationToken.ThrowIfCancellationRequested();return new OperationalReportResult(columns,rows);
                    }
                }
            }
            catch(SqlException error) when(cancellationToken.IsCancellationRequested)
            {throw new OperationCanceledException("Operational report read was canceled.",error,cancellationToken);}
        }
        private static void AddReturnDateParameters(SqlCommand command, OperationalReportRequest request)
        {
            if (request.Kind != OperationalReportKind.FastSellingToday && request.Kind != OperationalReportKind.FastSelling30Days) return;
            command.Parameters.Add("@FromUtc", SqlDbType.DateTime2).Value = DateTime.SpecifyKind(request.FromDate, DateTimeKind.Local).ToUniversalTime();
            command.Parameters.Add("@ToUtc", SqlDbType.DateTime2).Value = DateTime.SpecifyKind(request.ToDateExclusive, DateTimeKind.Local).ToUniversalTime();
        }
        public OperationalReportResult Load(OperationalReportRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            using (SqlConnection connection = connectionFactory())
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandTimeout = 30;
                command.Parameters.Add("@Take", SqlDbType.Int).Value = OperationalReportRequest.MaximumRows;
                command.Parameters.Add("@FromDate", SqlDbType.DateTime).Value = request.FromDate;
                command.Parameters.Add("@ToDate", SqlDbType.DateTime).Value = request.ToDateExclusive;
                command.Parameters.Add("@Search", SqlDbType.NVarChar, ReportSearchCriteria.MaximumLength).Value = request.SearchText;
                AddReturnDateParameters(command, request);
                command.CommandText = SqlOperationalReportQueries.Get(request.Kind, request.ViewMode);
                using (var table = new DataTable())
                using (var adapter = new SqlDataAdapter(command))
                {
                    adapter.Fill(table);
                    return new OperationalReportResult(
                        table.Columns.Cast<DataColumn>().Select(column => new OperationalReportColumn(column.ColumnName, column.DataType)),
                        table.Rows.Cast<DataRow>().Select(row => row.ItemArray.Select(value => value == DBNull.Value ? null : value).ToArray()));
                }
            }
        }
    }
}


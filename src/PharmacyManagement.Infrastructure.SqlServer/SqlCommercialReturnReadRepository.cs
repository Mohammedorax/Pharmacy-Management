using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Application;
namespace PharmacyManagement.Infrastructure.SqlServer
{
    public sealed class SqlCommercialReturnReadRepository : ICommercialReturnReadRepository
    {
        private readonly Func<SqlConnection> connectionFactory;
        public SqlCommercialReturnReadRepository(Func<SqlConnection> connectionFactory){this.connectionFactory=connectionFactory??throw new ArgumentNullException(nameof(connectionFactory));}
        private const string HistorySql=@"SELECT h.ReturnId AS [معرف المرتجع], credit.InvoiceNumber AS [رقم الإشعار], original.InvoiceNumber AS [رقم الفاتورة الأصلية],
h.IssuedAtUtc AS [تاريخ الاعتماد UTC], h.RefundMethod AS [طريقة الاسترداد], h.ReturnReason AS [السبب],
credit.GrossTotal AS [الإجمالي], credit.DiscountTotal AS [الخصم], credit.TaxTotal AS [الضريبة], credit.NetTotal AS [الصافي],
credit.CurrencyCode AS [العملة], h.IssuedBy AS [المنفذ]
FROM dbo.CommercialReturnHeadersTb h
JOIN dbo.SalesInvoicesTb credit ON credit.InvoiceId = h.ReturnInvoiceId AND credit.InvoiceType = N'CreditNote' AND credit.Status = N'Issued'
JOIN dbo.SalesInvoicesTb original ON original.InvoiceId = h.OriginalInvoiceId AND original.InvoiceType = N'Sale'
WHERE h.OriginalInvoiceId = @OriginalInvoiceId
ORDER BY h.IssuedAtUtc DESC, credit.InvoiceNumber DESC;";
        private const string DocumentSql=@"SELECT credit.InvoiceNumber, original.InvoiceNumber, h.IssuedAtUtc, h.RefundMethod, h.ReturnReason, h.IssuedBy, credit.CurrencyCode, h.ReturnId, h.OriginalInvoiceId
FROM dbo.CommercialReturnHeadersTb h
JOIN dbo.SalesInvoicesTb credit ON credit.InvoiceId = h.ReturnInvoiceId AND credit.InvoiceType = N'CreditNote' AND credit.Status = N'Issued'
JOIN dbo.SalesInvoicesTb original ON original.InvoiceId = h.OriginalInvoiceId AND original.InvoiceType = N'Sale'
WHERE h.ReturnId = @ReturnId AND h.OriginalInvoiceId = @OriginalInvoiceId;
SELECT creditLine.MedName, line.ReturnQuantity, line.GrossTotal, line.DiscountTotal, line.TaxRate, line.TaxTotal, line.NetTotal, N'' AS UnitName, creditLine.UnitPrice, CAST(1 AS int) AS PriceBaseQuantity
FROM dbo.CommercialReturnLinesTb line
JOIN dbo.CommercialReturnHeadersTb h ON h.ReturnId = line.ReturnId
JOIN dbo.SalesInvoiceLinesTb creditLine ON creditLine.LineId = line.ReturnInvoiceLineId
JOIN dbo.SalesInvoicesTb credit ON credit.InvoiceId = h.ReturnInvoiceId AND credit.Status = N'Issued'
WHERE line.ReturnId = @ReturnId AND h.OriginalInvoiceId = @OriginalInvoiceId
ORDER BY creditLine.LineNumber;";
        private static string BuildDocumentSql(Guid originalInvoiceId,Guid returnId)
        {
            // Scope the shared immutable-snapshot guard to this document. These
            // literals come from Guid values; all document reads stay parameterized.
            string scope="b.InvoiceId IN(SELECT ReturnInvoiceId FROM dbo.CommercialReturnHeadersTb WHERE ReturnId=CONVERT(uniqueidentifier,N'"+returnId.ToString("D")+"') AND OriginalInvoiceId=CONVERT(uniqueidentifier,N'"+originalInvoiceId.ToString("D")+"'))";
            string escapedScope=scope.Replace("'","''");
            string guard=SqlUnitReportProjection.Guard("SalesInvoiceLinesTb","LineId","CASE","Quantity","UnitPrice",true)
                .Replace("FROM dbo.SalesInvoiceLinesTb WHERE UnitSnapshotRequired=1","FROM dbo.SalesInvoiceLinesTb b WHERE "+escapedScope+" AND b.UnitSnapshotRequired=1")
                .Replace("WHERE b.UnitSnapshotRequired=1","WHERE "+escapedScope+" AND b.UnitSnapshotRequired=1")
                .Replace("u.UnitPrice<>b.UnitPrice OR ","u.CurrencyCode<>h.CurrencyCode OR u.UnitPrice<>b.UnitPrice OR ")
                .Replace("AND r.OriginalUnitPrice=b.UnitPrice","AND r.CurrencyCode=h.CurrencyCode AND r.OriginalUnitPrice=b.UnitPrice");
            string unit=DocumentSql.Replace("N'' AS UnitName","COALESCE(u.UnitName,N'') AS UnitName")
                .Replace("JOIN dbo.SalesInvoiceLinesTb creditLine ON creditLine.LineId = line.ReturnInvoiceLineId",
                    "JOIN dbo.SalesInvoiceLinesTb creditLine ON creditLine.LineId = line.ReturnInvoiceLineId LEFT JOIN dbo.InvoiceUnitSnapshotsTb u ON u.DocumentKind=N'CommercialReturn' AND u.LineKey=CONVERT(nvarchar(100),creditLine.LineId)");
            string basic=unit.Replace("COALESCE(u.UnitName,N'') AS UnitName","CASE WHEN basis.ReturnInvoiceLineId IS NOT NULL THEN N'وحدة أساسية من '+basis.OriginalUnitName ELSE COALESCE(u.UnitName,N'') END AS UnitName")
                .Replace("CAST(1 AS int) AS PriceBaseQuantity","COALESCE(basis.OriginalFactor,1) AS PriceBaseQuantity")
                .Replace("WHERE line.ReturnId = @ReturnId","LEFT JOIN dbo.CommercialReturnUnitSnapshotsTb basis ON basis.ReturnInvoiceLineId=creditLine.LineId WHERE line.ReturnId = @ReturnId");
            Func<string,string> execute=query=>"EXEC sys.sp_executesql N'"+query.Replace("'","''")+"',N'@ReturnId uniqueidentifier,@OriginalInvoiceId uniqueidentifier',@ReturnId=@ReturnId,@OriginalInvoiceId=@OriginalInvoiceId;";
            return guard+"IF OBJECT_ID(N'dbo.InvoiceUnitSnapshotsTb',N'U') IS NOT NULL BEGIN IF OBJECT_ID(N'dbo.CommercialReturnUnitSnapshotsTb',N'U') IS NOT NULL BEGIN "+execute(basic)+" END ELSE BEGIN "+execute(unit)+" END END ELSE BEGIN "+execute(DocumentSql)+" END;";
        }
        public async Task<OperationalReportResult> LoadHistoryAsync(CommercialReturnHistoryReadRequest request,CancellationToken cancellationToken)
        {
            if(request==null)throw new ArgumentNullException(nameof(request));cancellationToken.ThrowIfCancellationRequested();
            try{
                using(var connection=connectionFactory())using(var command=connection.CreateCommand()){
                    command.CommandText=HistorySql;command.CommandTimeout=15;command.Parameters.Add("@OriginalInvoiceId",SqlDbType.UniqueIdentifier).Value=request.OriginalInvoiceId;
                    await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                    using(var reader=await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false)){
                        var columns=Enumerable.Range(0,reader.FieldCount).Select(i=>new OperationalReportColumn(reader.GetName(i),reader.GetFieldType(i))).ToArray();
                        var rows=new List<object[]>();while(await reader.ReadAsync(cancellationToken).ConfigureAwait(false)){var values=new object[reader.FieldCount];reader.GetValues(values);for(int i=0;i<values.Length;i++)if(values[i]==DBNull.Value)values[i]=null;rows.Add(values);}
                        cancellationToken.ThrowIfCancellationRequested();return new OperationalReportResult(columns,rows);
                    }
                }
            }catch(SqlException error)when(cancellationToken.IsCancellationRequested){throw new OperationCanceledException("Return history read canceled.",error,cancellationToken);}
        }
        public async Task<CommercialReturnDocumentSnapshot> LoadDocumentAsync(CommercialReturnDocumentReadRequest request,CancellationToken cancellationToken)
        {
            if(request==null)throw new ArgumentNullException(nameof(request));cancellationToken.ThrowIfCancellationRequested();
            try{
                using(var connection=connectionFactory())using(var command=connection.CreateCommand()){
                    command.CommandText=BuildDocumentSql(request.OriginalInvoiceId,request.ReturnId);command.CommandTimeout=15;command.Parameters.Add("@OriginalInvoiceId",SqlDbType.UniqueIdentifier).Value=request.OriginalInvoiceId;
                    command.Parameters.Add("@ReturnId",SqlDbType.UniqueIdentifier).Value=request.ReturnId;
                    await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                    using(var reader=await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false)){
                        if(!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))return null;
                        long number=reader.GetInt64(0),originalNumber=reader.GetInt64(1);DateTime issued=reader.GetDateTime(2);
                        string refund=reader.GetString(3),reason=reader.GetString(4),user=reader.GetString(5),currency=reader.GetString(6);
                        Guid returnId=reader.GetGuid(7),invoiceId=reader.GetGuid(8);var lines=new List<CommercialReturnDocumentLine>();
                        if(!await reader.NextResultAsync(cancellationToken).ConfigureAwait(false))throw new InvalidOperationException("Missing issued return lines.");
                        while(await reader.ReadAsync(cancellationToken).ConfigureAwait(false))lines.Add(new CommercialReturnDocumentLine(reader.GetString(0),reader.GetDecimal(1),reader.GetDecimal(2),reader.GetDecimal(3),reader.GetDecimal(4),reader.GetDecimal(5),reader.GetDecimal(6),reader.GetString(7),reader.GetDecimal(8),reader.GetInt32(9)));
                        cancellationToken.ThrowIfCancellationRequested();return new CommercialReturnDocumentSnapshot(returnId,invoiceId,number,originalNumber,issued,refund,reason,user,currency,lines);
                    }
                }
            }catch(SqlException error)when(cancellationToken.IsCancellationRequested){throw new OperationCanceledException("Return document read canceled.",error,cancellationToken);}
        }
    }
}


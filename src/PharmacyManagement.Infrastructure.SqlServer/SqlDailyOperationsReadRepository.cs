using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Application;
namespace PharmacyManagement.Infrastructure.SqlServer
{
    public sealed class SqlDailyOperationsReadRepository : IDailyOperationsReadRepository
    {
        private readonly string connection;
        public SqlDailyOperationsReadRepository(string connection){this.connection=connection??throw new ArgumentNullException(nameof(connection));}
        private const string PurchaseSql = @"SELECT TOP (1001) p.PurchaseId,p.PurchaseDate,p.SupplierName,p.TotalAmount,p.EmployeeName,
CASE WHEN EXISTS(SELECT 1 FROM dbo.JournalEntriesTb j WHERE j.SourceType=N'PurchaseInvoiceCancellation' AND j.SourceId=p.PurchaseId) THEN N'Cancelled' ELSE N'Saved' END
FROM dbo.PurchasesTb p WHERE p.PurchaseDate>=@From AND p.PurchaseDate<@End ORDER BY p.PurchaseDate DESC,p.PurchaseId DESC;";
        private const string SaleSql = @"IF OBJECT_ID(N'dbo.LegacySalesInvoiceHeadersTb',N'U') IS NOT NULL
SELECT TOP (1001) h.InvoiceReference,h.InvoiceDate,h.CustomerNameSnapshot,h.TotalAmount,h.EmployeeName,
CASE WHEN EXISTS(SELECT 1 FROM dbo.JournalEntriesTb j WHERE j.SourceType=N'SalesInvoiceCancellation' AND j.SourceId=h.InvoiceReference) THEN N'Cancelled' ELSE N'Saved' END
FROM dbo.LegacySalesInvoiceHeadersTb h WHERE h.InvoiceDate>=@From AND h.InvoiceDate<@End ORDER BY h.InvoiceDate DESC,h.InvoiceReference DESC;";
        private const string CommercialSql = @"IF OBJECT_ID(N'dbo.SalesInvoicesTb',N'U') IS NOT NULL
SELECT TOP (1001) CONVERT(nvarchar(36),InvoiceId),IssuedAtUtc,CustomerNameSnapshot,NetTotal,IssuedBy,Status,InvoiceNumber
FROM dbo.SalesInvoicesTb WHERE InvoiceType=N'Sale' AND IssuedAtUtc>=@From AND IssuedAtUtc<@End ORDER BY IssuedAtUtc DESC,InvoiceNumber DESC;";
        private const string VoucherSql = @"SELECT TOP (1001) CONVERT(nvarchar(50),VoucherId),VoucherDate,PartyName,Amount,CreatedBy,VoucherType
FROM dbo.AccountingVouchersTb WHERE VoucherDate>=@From AND VoucherDate<@End ORDER BY VoucherDate DESC,VoucherId DESC;";

        public async Task<IReadOnlyList<DailyOperationRecord>> LoadAsync(string kind,DateTime day,Action demandAccess,CancellationToken token)
        {
            string sql=kind=="Purchase"?PurchaseSql:kind=="Voucher"?VoucherSql:kind=="Sale"?SaleSql:kind=="CommercialSale"?CommercialSql:throw new ArgumentException("Invalid document kind.",nameof(kind));
            var items=new List<DailyOperationRecord>();
            using(var db=new SqlConnection(connection))
            using(var command=new SqlCommand(sql,db))
            {
                command.CommandTimeout=30;bool utc=kind=="CommercialSale";
                command.Parameters.Add("@From",SqlDbType.DateTime).Value=utc?day.Date.ToUniversalTime():day.Date;
                command.Parameters.Add("@End",SqlDbType.DateTime).Value=utc?day.Date.AddDays(1).ToUniversalTime():day.Date.AddDays(1);
                token.ThrowIfCancellationRequested();demandAccess();await db.OpenAsync(token).ConfigureAwait(false);demandAccess();
                using(var reader=await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                {
                    while(await reader.ReadAsync(token).ConfigureAwait(false))
                    {
                        items.Add(new DailyOperationRecord(kind,Convert.ToString(reader[0]),utc?"C-"+Convert.ToString(reader[6]):Convert.ToString(reader[0]),
                            utc?DateTime.SpecifyKind(Convert.ToDateTime(reader[1]),DateTimeKind.Utc).ToLocalTime():Convert.ToDateTime(reader[1]),
                            Convert.ToString(reader[2]),Convert.ToDecimal(reader[3]),Convert.ToString(reader[4]),Convert.ToString(reader[5])));
                    }
                }
                token.ThrowIfCancellationRequested();demandAccess();
            }
            return items.AsReadOnly();
        }
        public async Task<DailyOperationDocument> ReadLinesAsync(string kind,string reference,Action demandAccess,CancellationToken token)
        {
            string sql;
            if(kind=="Purchase")sql="SELECT MedName,BuyingPrice,Quantity,TotalAmount,ProdDate,ExpDate FROM dbo.PurchaseDetailsTb WHERE PurchaseId=@Id ORDER BY DetailId;";
            else if(kind=="Sale")sql="SELECT b.MedName,b.Sprice,b.Quantity,b.TotalAmount FROM dbo.LegacySalesInvoiceLinesTb l JOIN dbo.BillsTb1 b ON b.BillId=l.BillId WHERE l.InvoiceReference=@Id ORDER BY l.LineNumber;";
            else if(kind=="CommercialSale")sql="SELECT MedName,UnitPrice,Quantity,LineTotal FROM dbo.SalesInvoiceLinesTb WHERE InvoiceId=@Id ORDER BY LineNumber;";
            else if(kind=="Voucher")sql="SELECT VoucherType,AccountCode,Amount,Notes,JournalEntryId FROM dbo.AccountingVouchersTb WHERE VoucherId=@Id;";
            else throw new ArgumentException("Invalid document kind.",nameof(kind));
            var columns=new List<DailyOperationColumn>();var rows=new List<object[]>();
            using(var db=new SqlConnection(connection))
            using(var command=new SqlCommand(sql,db))
            {
                command.CommandTimeout=30;
                if(kind=="CommercialSale")command.Parameters.Add("@Id",SqlDbType.UniqueIdentifier).Value=Guid.Parse(reference);
                else if(kind=="Voucher")command.Parameters.Add("@Id",SqlDbType.Int).Value=int.Parse(reference);
                else command.Parameters.Add("@Id",SqlDbType.NVarChar,50).Value=reference;
                token.ThrowIfCancellationRequested();demandAccess();await db.OpenAsync(token).ConfigureAwait(false);demandAccess();
                using(var reader=await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                {
                    for(int i=0;i<reader.FieldCount;i++)columns.Add(new DailyOperationColumn(reader.GetName(i),reader.GetFieldType(i)));
                    while(await reader.ReadAsync(token).ConfigureAwait(false)){var values=new object[reader.FieldCount];reader.GetValues(values);rows.Add(values);}
                }
            }
            token.ThrowIfCancellationRequested();demandAccess();return new DailyOperationDocument(columns,rows);
        }
    }
}

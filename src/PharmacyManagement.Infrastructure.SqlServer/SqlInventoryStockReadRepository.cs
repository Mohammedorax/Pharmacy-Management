using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Application;

namespace PharmacyManagement.Infrastructure.SqlServer
{
    public sealed class SqlInventoryStockReadRepository : IInventoryStockReadRepository
    {
        private readonly Func<SqlConnection> connectionFactory;
        public SqlInventoryStockReadRepository(Func<SqlConnection> connectionFactory)
        { this.connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory)); }

        public async Task<OperationalReportResult> LoadAsync(InventoryStockReadRequest request, CancellationToken cancellation)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            cancellation.ThrowIfCancellationRequested();
            using (var connection = connectionFactory())
            using (var command = connection.CreateCommand())
            {
                command.CommandTimeout = 30;
                if (request.Mode == InventoryStockReadMode.Basic)
                {
                    command.CommandText = @"SELECT MedName AS [الصنف], ISNULL(NULLIF(Barcode, N''), N'-') AS [الباركود], CompName AS [الشركة], N'الرصيد الرئيسي' AS [الوحدة], MedQty AS [الكمية الحالية], Bprice AS [سعر التكلفة], Sprice AS [سعر البيع التقديري], CAST(CAST(MedQty AS DECIMAL(18, 2)) * Bprice AS DECIMAL(18, 2)) AS [قيمة التكلفة], CAST(CAST(MedQty AS DECIMAL(18, 2)) * Sprice AS DECIMAL(18, 2)) AS [قيمة البيع], ProdDate AS [تاريخ الإنتاج], ExpDate AS [تاريخ الانتهاء], CASE WHEN MedQty <= 0 THEN N'نفد الرصيد' WHEN ExpDate < CAST(GETDATE() AS DATE) THEN N'منتهي' WHEN ExpDate <= DATEADD(DAY, 30, CAST(GETDATE() AS DATE)) THEN N'قريب الانتهاء' WHEN MedQty <= 10 THEN N'مخزون منخفض' ELSE N'طبيعي' END AS [الحالة] FROM dbo.MedicineTb1 WHERE (@IncludeZero = 1 OR MedQty > 0) AND (@Search = N'' OR MedName LIKE N'%' + @Search + N'%' OR ISNULL(Barcode, N'') LIKE N'%' + @Search + N'%' OR CompName LIKE N'%' + @Search + N'%') ORDER BY MedName;";
                }
                else
                {
                    using (var schema = connection.CreateCommand())
                    {
                        schema.CommandText = "SELECT OBJECT_ID(N'dbo.InventoryLotsTb', N'U')";
                        await connection.OpenAsync(cancellation).ConfigureAwait(false);
                        var exists = await schema.ExecuteScalarAsync(cancellation).ConfigureAwait(false);
                        if (exists == null || exists == DBNull.Value)
                            throw new InvalidOperationException("كشف التشغيلات يتطلب مخطط الدفعات التجاري.");
                    }
                    command.CommandText = @"SELECT l.MedName AS [الصنف], m.Barcode AS [الباركود], m.CompName AS [الشركة], l.BatchNumber AS [رقم التشغيلة], l.Gtin AS [GTIN], l.SupplierName AS [المورد], l.QuantityReceived AS [الكمية المستلمة], l.QuantityRemaining AS [الكمية الحالية], CASE WHEN l.IsRecalled = 0 AND l.QuantityRemaining > 0 AND l.ExpiryDate >= CAST(GETDATE() AS DATE) THEN l.QuantityRemaining ELSE CAST(0 AS DECIMAL(18,3)) END AS [الكمية القابلة للصرف], l.UnitCost AS [تكلفة الوحدة], m.Sprice AS [سعر البيع التقديري], CAST(l.QuantityRemaining * l.UnitCost AS DECIMAL(18,2)) AS [قيمة التكلفة], CAST(l.QuantityRemaining * m.Sprice AS DECIMAL(18,2)) AS [قيمة البيع], l.ProductionDate AS [تاريخ الإنتاج], l.ExpiryDate AS [تاريخ الانتهاء], CASE WHEN l.IsRecalled = 1 THEN N'مسحوبة' WHEN l.QuantityRemaining <= 0 THEN N'نفدت' WHEN l.ExpiryDate < CAST(GETDATE() AS DATE) THEN N'منتهية' WHEN l.ExpiryDate <= DATEADD(DAY, 30, CAST(GETDATE() AS DATE)) THEN N'قريبة الانتهاء' ELSE N'متاحة' END AS [حالة التشغيلة], l.LotId AS [معرف التشغيلة] FROM dbo.InventoryLotsTb l INNER JOIN dbo.MedicineTb1 m ON m.MedName = l.MedName WHERE (@IncludeZero = 1 OR l.QuantityRemaining > 0) AND (@Search = N'' OR l.MedName LIKE N'%' + @Search + N'%' OR ISNULL(m.Barcode, N'') LIKE N'%' + @Search + N'%' OR m.CompName LIKE N'%' + @Search + N'%' OR l.BatchNumber LIKE N'%' + @Search + N'%' OR ISNULL(l.Gtin, N'') LIKE N'%' + @Search + N'%' OR ISNULL(l.SupplierName, N'') LIKE N'%' + @Search + N'%') ORDER BY l.MedName, l.ExpiryDate, l.BatchNumber, l.LotId;";
                }
                if(request.Mode==InventoryStockReadMode.CommercialLots)
                {
                    using(var metadata=connection.CreateCommand())
                    {
                        metadata.CommandText="SELECT CASE WHEN OBJECT_ID(N'dbo.UnitLotCostTb',N'U') IS NULL THEN 0 ELSE 1 END";
                        bool tracked=Convert.ToInt32(await metadata.ExecuteScalarAsync(cancellation).ConfigureAwait(false))==1;
                        command.CommandText=SqlUnitLotValuation.Build(command.CommandText,tracked);
                    }
                }
                command.Parameters.Add("@Search", SqlDbType.NVarChar, 200).Value = request.SearchText;
                command.Parameters.Add("@IncludeZero", SqlDbType.Bit).Value = request.IncludeZero;
                if (connection.State != ConnectionState.Open) await connection.OpenAsync(cancellation).ConfigureAwait(false);
                using (var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellation).ConfigureAwait(false))
                {
                    var columns = new List<OperationalReportColumn>();
                    for (int index = 0; index < reader.FieldCount; index++) columns.Add(new OperationalReportColumn(reader.GetName(index), reader.GetFieldType(index)));
                    var rows = new List<object[]>();
                    while (await reader.ReadAsync(cancellation).ConfigureAwait(false))
                    {
                        var values = new object[reader.FieldCount];
                        reader.GetValues(values);
                        for (int index = 0; index < values.Length; index++) if (values[index] == DBNull.Value) values[index] = null;
                        rows.Add(values);
                    }
                    return new OperationalReportResult(columns, rows);
                }
            }
        }
    }
    public static class SqlUnitLotValuation
    {
        public static string Build(string original,bool tracked)
        {
            string missing=tracked?"IF EXISTS(SELECT 1 FROM dbo.InventoryLotsTb l JOIN dbo.PurchaseDetailsTb p ON p.DetailId=l.PurchaseDetailId LEFT JOIN dbo.UnitLotCostTb c ON c.LotId=l.LotId WHERE p.UnitSnapshotRequired=1 AND c.LotId IS NULL) THROW 51000,N'سجل تكلفة تشغيلة الوحدة مفقود؛ أوقف الكشف.',1;":"IF EXISTS(SELECT 1 FROM dbo.InventoryLotsTb l JOIN dbo.PurchaseDetailsTb p ON p.DetailId=l.PurchaseDetailId WHERE p.UnitSnapshotRequired=1) THROW 51000,N'جدول تكلفة وحدات التشغيلات مفقود؛ أوقف الكشف.',1;";
            string required="IF COL_LENGTH(N'dbo.PurchaseDetailsTb',N'UnitSnapshotRequired') IS NOT NULL EXEC(N'"+missing.Replace("'","''")+"');";
            if(!tracked)return required+original;
            string guard=@"IF EXISTS(SELECT 1 FROM dbo.InventoryLotsTb l JOIN dbo.UnitLotCostTb c ON c.LotId=l.LotId WHERE c.RemainingQuantity<>l.QuantityRemaining)
THROW 51000,N'كمية تكلفة التشغيلة لا تطابق المخزون؛ أوقف كشف القيمة.',1;";
            return required+guard+original.Replace("CAST(l.QuantityRemaining * l.UnitCost AS DECIMAL(18,2)) AS [قيمة التكلفة]","COALESCE(c.RemainingCost,CAST(l.QuantityRemaining * l.UnitCost AS DECIMAL(18,2))) AS [قيمة التكلفة]")
                .Replace("FROM dbo.InventoryLotsTb l","FROM dbo.InventoryLotsTb l LEFT JOIN dbo.UnitLotCostTb c ON c.LotId=l.LotId");
        }
    }

}

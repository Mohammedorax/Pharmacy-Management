using System;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Threading;
using PharmacyManagement.Application;
using PharmacyManagement.Infrastructure.SqlServer;

namespace PharmacyManagement.CoreTests
{
    internal static class InventoryOperationReadSqlTests
    {
        private static object Value(object value)=>value==DBNull.Value?null:value;
        private static decimal? Money(object value)=>value==null||value==DBNull.Value?(decimal?)null:Convert.ToDecimal(value,CultureInfo.InvariantCulture);
        private static string Caption(string source)
        {switch(source){case "SalesInvoice":return "فاتورة بيع";case "PurchaseInvoice":return "فاتورة شراء";case "OpeningStock":return "مخزون أول المدة";case "StockAdjustment":return "تسوية مخزون";case "StockReceipt":return "توريد مخزني";case "StockIssue":return "صرف مخزني";default:return source;}}
        internal static void Verify(string server,string database)
        {
            if(server!="."||!string.Equals(database,"PharmacyManagement_qa",StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("Inventory read comparison only permits local PharmacyManagement_qa.");
            var builder=new SqlConnectionStringBuilder {DataSource=server,InitialCatalog=database,IntegratedSecurity=true,Encrypt=true,TrustServerCertificate=true,ConnectTimeout=15,ApplicationIntent=ApplicationIntent.ReadOnly};
            int factoryCalls=0;
            var repository=new SqlInventoryOperationReadRepository(()=>{factoryCalls++;return new SqlConnection(builder.ConnectionString);});
            using(var cancellation=new CancellationTokenSource())
            {
                cancellation.Cancel();bool rejected=false;
                try{repository.LoadMedicinesAsync(cancellation.Token).GetAwaiter().GetResult();}catch(OperationCanceledException){rejected=true;}
                if(!rejected||factoryCalls!=0)throw new Exception("Cancelled lookup opened SQL.");
            }
            using(var baseline=new SqlConnection(builder.ConnectionString))
            {
                baseline.Open();var expectedLookup=new DataTable();
                using(var command=baseline.CreateCommand())
                {
                    command.CommandText="SELECT MedName, MedName + N' - ' + CompName AS DisplayName, MedQty FROM dbo.MedicineTb1 ORDER BY MedName;";
                    using(var adapter=new SqlDataAdapter(command))adapter.Fill(expectedLookup);
                }
                var actualLookup=repository.LoadMedicinesAsync(CancellationToken.None).GetAwaiter().GetResult();
                if(actualLookup.Count!=expectedLookup.Rows.Count)throw new Exception("Medicine lookup count differs.");
                int movements=0;
                for(int i=0;i<actualLookup.Count;i++)
                {
                    var actual=actualLookup[i];var expected=expectedLookup.Rows[i];
                    if(!Equals(actual.Name,Value(expected[0]))||!Equals(actual.DisplayName,Value(expected[1]))||actual.Quantity!=Convert.ToInt32(expected[2]))
                        throw new Exception("Medicine lookup value/order differs.");
                    var request=new InventoryOperationReadRequest(actual.Name,DateTime.Today.AddMonths(-3),DateTime.Today);
                    var snapshot=repository.LoadSnapshotAsync(request,CancellationToken.None).GetAwaiter().GetResult();
                    var rows=new DataTable();
                    using(var command=baseline.CreateCommand())
                    {
                        command.CommandText=@"SELECT CreatedAt AS [التاريخ], MedName AS [الصنف],
CASE SourceType WHEN N'SalesInvoice' THEN N'فاتورة بيع' WHEN N'PurchaseInvoice' THEN N'فاتورة شراء'
WHEN N'OpeningStock' THEN N'مخزون أول المدة' WHEN N'StockAdjustment' THEN N'تسوية مخزون'
WHEN N'StockReceipt' THEN N'توريد مخزني' WHEN N'StockIssue' THEN N'صرف مخزني' ELSE SourceType END AS [نوع الحركة],
SourceId AS [رقم المستند], CASE WHEN QuantityDelta>0 THEN QuantityDelta ELSE 0 END AS [الوارد],
CASE WHEN QuantityDelta<0 THEN ABS(QuantityDelta) ELSE 0 END AS [الصادر], QuantityDelta AS [صافي الحركة],
QuantityBefore AS [الرصيد قبل], QuantityAfter AS [الرصيد بعد], UnitCost AS [تكلفة الوحدة],
UnitPrice AS [سعر الوحدة], CreatedBy AS [المستخدم], Notes AS [ملاحظات]
FROM dbo.InventoryMovementsTb WHERE MedName=@MedName AND CreatedAt>=@FromDate AND CreatedAt<DATEADD(DAY,1,@ToDate)
ORDER BY CreatedAt DESC, MovementId DESC;";
                        command.Parameters.Add("@MedName",SqlDbType.NVarChar,200).Value=request.MedicineName;
                        command.Parameters.Add("@FromDate",SqlDbType.DateTime).Value=request.From;
                        command.Parameters.Add("@ToDate",SqlDbType.DateTime).Value=request.To;
                        using(var adapter=new SqlDataAdapter(command))adapter.Fill(rows);
                    }
                    if(snapshot.CurrentQuantity!=actual.Quantity||snapshot.Movements.Count!=rows.Rows.Count)throw new Exception("Snapshot balance/count differs from baseline.");
                    for(int j=0;j<rows.Rows.Count;j++)
                    {
                        var a=snapshot.Movements[j];var e=rows.Rows[j];
                        if(a.CreatedAt!=(DateTime)e[0]||!Equals(a.MedicineName,Value(e[1]))||!Equals(Caption(a.SourceType),Value(e[2]))||!Equals(a.SourceId,Value(e[3]))||
                            Money(e[4])!=(a.Delta>0?a.Delta:0)||Money(e[5])!=(a.Delta<0?-a.Delta:0)||Money(e[6])!=a.Delta||Money(e[7])!=a.Before||Money(e[8])!=a.After||
                            Money(e[9])!=a.UnitCost||Money(e[10])!=a.UnitPrice||!Equals(a.UserName,Value(e[11]))||!Equals(a.Notes,Value(e[12])))
                            throw new Exception("Movement date/order/value/nullability differs from legacy query.");
                    }
                    movements+=rows.Rows.Count;
                }
                bool missingRejected=false;
                try{repository.LoadSnapshotAsync(new InventoryOperationReadRequest("QA-missing-"+Guid.NewGuid().ToString("N"),DateTime.Today,DateTime.Today),CancellationToken.None).GetAwaiter().GetResult();}
                catch(InvalidOperationException error){missingRejected=error.Message=="الصنف غير موجود في قاعدة البيانات.";}
                if(!missingRejected)throw new Exception("Missing medicine became a fabricated zero balance.");
                Console.WriteLine("PASS QA inventory operation async read parity; Medicines="+actualLookup.Count+"; Movements="+movements+"; cancelled-before-open and missing medicine checked; PersistentWrites=0.");
            }
        }
    }
}

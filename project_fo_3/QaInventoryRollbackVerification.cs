using System;
using System.Data;
using System.Data.SqlClient;

namespace project_fo_3
{
    internal static class QaInventoryRollbackVerification
    {
        internal static void Run(string serverName, string databaseName)
        {
            if (string.IsNullOrWhiteSpace(databaseName) ||
                !databaseName.Trim().EndsWith("_qa", StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("اختبار حركة المخزون المتراجعة مسموح على قاعدة QA فقط.");

            string connectionString = Database.BuildSettingsConnectionString(serverName, databaseName, true);
            string sourceId = "QA-INV-" + Guid.NewGuid().ToString("N");
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                if (!string.Equals(connection.Database, databaseName.Trim(), StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("قاعدة الاتصال لا تطابق قاعدة QA المطلوبة.");
                if (CommercialSchemaInstaller.IsFullCommercialModeRequested(connection, null))
                    throw new InvalidOperationException("هذا الفحص مخصص لمسار المخزون اليدوي؛ وضع QA التجاري مفعل.");

                string medicineName;
                int quantityBefore;
                using (var command = new SqlCommand(@"
SELECT TOP (1) MedName, MedQty FROM dbo.MedicineTb1
WHERE MedQty >= 0 AND MedQty < 2147483647 ORDER BY MedName;", connection))
                using (var reader = command.ExecuteReader())
                {
                    if (!reader.Read()) throw new InvalidOperationException("لا يوجد صنف صالح لفحص QA.");
                    medicineName = Convert.ToString(reader[0]);
                    quantityBefore = Convert.ToInt32(reader[1]);
                }

                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        var service=new PharmacyManagement.Application.InventoryPostService(new TransactionRepository(connection,transaction),_=>true);
                        var actor=new PharmacyManagement.Application.OperationActor {UserName="QAVerifier",UserRole="QAVerifier"};
                        var accepted=service.Post(new PharmacyManagement.Application.InventoryPostRequest(PharmacyManagement.Application.InventoryPostKind.Receipt,
                            sourceId,medicineName,1,1m,"QA rollback inventory verification"),actor);
                        if(accepted.Before!=quantityBefore||accepted.After!=quantityBefore+1||accepted.Delta!=1)
                            throw new InvalidOperationException("Typed inventory result differs from SQL.");
                        if (Count(connection, transaction, "SELECT MedQty FROM dbo.MedicineTb1 WHERE MedName=@Name;", "@Name", medicineName) != quantityBefore + 1 ||
                            Count(connection, transaction, "SELECT COUNT(*) FROM dbo.InventoryMovementsTb WHERE SourceId=@SourceId;", "@SourceId", sourceId) != 1 ||
                            Count(connection, transaction, "SELECT COUNT(*) FROM dbo.AuditLogTb WHERE CHARINDEX(@SourceId, Details)>0;", "@SourceId", sourceId) != 1)
                            throw new InvalidOperationException("لم تُكتب الكمية والحركة والتدقيق داخل المعاملة نفسها.");
                        string maximumSource=sourceId+"-max", overflowSource=sourceId+"-over";
                        InventoryService.ExecuteStockOperation(connection,transaction,"StockAdjustment",maximumSource,
                            medicineName,int.MaxValue,null,null,null,"QA maximum quantity fixture","تسوية مخزون","StockAdjustmentSaved","QAVerifier","QAVerifier");
                        if(Count(connection,transaction,"SELECT MedQty FROM dbo.MedicineTb1 WHERE MedName=@Name;","@Name",medicineName)!=int.MaxValue||
                            Count(connection,transaction,"SELECT QuantityBefore FROM dbo.InventoryMovementsTb WHERE SourceId=@SourceId;","@SourceId",maximumSource)!=quantityBefore+1||
                            Count(connection,transaction,"SELECT QuantityAfter FROM dbo.InventoryMovementsTb WHERE SourceId=@SourceId;","@SourceId",maximumSource)!=int.MaxValue||
                            Count(connection,transaction,"SELECT QuantityDelta FROM dbo.InventoryMovementsTb WHERE SourceId=@SourceId;","@SourceId",maximumSource)!=int.MaxValue-quantityBefore-1)
                            throw new InvalidOperationException("SQL inventory boundary or signed movement differs.");
                        bool rejected=false;
                        try {InventoryService.ExecuteStockOperation(connection,transaction,"StockReceipt",overflowSource,
                            medicineName,null,1,1m,null,"QA overflow fixture","توريد مخزني يدوي","StockReceiptSaved","QAVerifier","QAVerifier");}
                        catch(PharmacyManagement.Domain.InventoryQuantityException){rejected=true;}
                        if(!rejected||Count(connection,transaction,"SELECT MedQty FROM dbo.MedicineTb1 WHERE MedName=@Name;","@Name",medicineName)!=int.MaxValue||
                            Count(connection,transaction,"SELECT COUNT(*) FROM dbo.InventoryMovementsTb WHERE SourceId=@SourceId;","@SourceId",overflowSource)!=0||
                            Count(connection,transaction,"SELECT COUNT(*) FROM dbo.AuditLogTb WHERE CHARINDEX(@SourceId,Details)>0;","@SourceId",overflowSource)!=0)
                            throw new InvalidOperationException("Overflow rejection changed stock, movement or audit.");
                    }
                    finally { transaction.Rollback(); }
                }

                if (Count(connection, null, "SELECT MedQty FROM dbo.MedicineTb1 WHERE MedName=@Name;", "@Name", medicineName) != quantityBefore ||
                    Count(connection, null, "SELECT COUNT(*) FROM dbo.InventoryMovementsTb WHERE SourceId=@SourceId;", "@SourceId", sourceId) != 0 ||
                    Count(connection, null, "SELECT COUNT(*) FROM dbo.InventoryMovementsTb WHERE SourceId=@SourceId;", "@SourceId", sourceId+"-max") != 0 ||
                    Count(connection, null, "SELECT COUNT(*) FROM dbo.AuditLogTb WHERE CHARINDEX(@SourceId, Details)>0;", "@SourceId", sourceId) != 0)
                    throw new InvalidOperationException("فشل استرجاع اختبار QA؛ بقي رصيد أو حركة أو سجل تدقيق.");
            }
        }

        private sealed class TransactionRepository : PharmacyManagement.Application.IInventoryPostRepository
        {
            private readonly SqlConnection connection;private readonly SqlTransaction transaction;
            internal TransactionRepository(SqlConnection connection,SqlTransaction transaction){this.connection=connection;this.transaction=transaction;}
            public PharmacyManagement.Application.InventoryPostResult Post(PharmacyManagement.Application.InventoryPostRequest request,PharmacyManagement.Application.OperationActor actor)
                => PharmacyManagement.Infrastructure.SqlServer.SqlInventoryPostRepository.PostInTransaction(connection,transaction,request,actor);
        }
        private static int Count(SqlConnection connection, SqlTransaction transaction, string sql, string parameterName, string value)
        {
            using (var command = new SqlCommand(sql, connection, transaction))
            {
                command.Parameters.Add(parameterName, SqlDbType.NVarChar, 200).Value = value;
                return Convert.ToInt32(command.ExecuteScalar());
            }
        }
    }
}

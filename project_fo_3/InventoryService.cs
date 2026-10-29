using System;
using PharmacyManagement.Infrastructure.SqlServer;
using System.Data;
using System.Data.SqlClient;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace project_fo_3
{
    internal static class InventoryService
    {
        internal static bool IsLegacyManualStockOperationAllowed()
        {
            using (SqlConnection connection = Database.CreateConnection())
            {
                connection.Open();
                return !CommercialSchemaInstaller.IsFullCommercialModeRequested(connection, null);
            }
        }

        public static DataTable GetMedicineLookup()
        {
            using (SqlConnection connection = Database.CreateConnection())
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandText = @"
SELECT MedName,
       MedName + N' - ' + CompName AS DisplayName,
       MedQty
FROM dbo.MedicineTb1
ORDER BY MedName;";

                using (SqlDataAdapter adapter = new SqlDataAdapter(command))
                {
                    DataTable table = new DataTable();
                    adapter.Fill(table);
                    return table;
                }
            }
        }

        public static DataTable GetStockSnapshot(string searchText, bool includeZeroQuantity)
        {
            using (SqlConnection connection = Database.CreateConnection())
                return GetStockSnapshot(connection, searchText, includeZeroQuantity);
        }

        internal static DataTable GetStockSnapshotQaReadOnly(string serverName, string databaseName,
            string searchText, bool includeZeroQuantity)
        {
            if (string.IsNullOrWhiteSpace(databaseName) ||
                !databaseName.Trim().EndsWith("_qa", StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("فحص كشف الجرد مسموح فقط لقاعدة QA.");
            using (var connection = new SqlConnection(Database.BuildSettingsConnectionString(serverName, databaseName, true)))
                return GetStockSnapshot(connection, searchText, includeZeroQuantity);
        }

        private static DataTable GetStockSnapshot(SqlConnection connection, string searchText, bool includeZeroQuantity)
        {
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandText = @"
SELECT MedName AS [الصنف],
       ISNULL(NULLIF(Barcode, N''), N'-') AS [الباركود],
       CompName AS [الشركة],
       N'الرصيد الرئيسي' AS [الوحدة],
       MedQty AS [الكمية الحالية],
       Bprice AS [سعر التكلفة],
       Sprice AS [سعر البيع التقديري],
       CAST(CAST(MedQty AS DECIMAL(18, 2)) * Bprice AS DECIMAL(18, 2)) AS [قيمة التكلفة],
       CAST(CAST(MedQty AS DECIMAL(18, 2)) * Sprice AS DECIMAL(18, 2)) AS [قيمة البيع],
       ProdDate AS [تاريخ الإنتاج],
       ExpDate AS [تاريخ الانتهاء],
       CASE
           WHEN MedQty <= 0 THEN N'نفد الرصيد'
           WHEN ExpDate < CAST(GETDATE() AS DATE) THEN N'منتهي'
           WHEN ExpDate <= DATEADD(DAY, 30, CAST(GETDATE() AS DATE)) THEN N'قريب الانتهاء'
           WHEN MedQty <= 10 THEN N'مخزون منخفض'
           ELSE N'طبيعي'
       END AS [الحالة]
FROM dbo.MedicineTb1
WHERE (@IncludeZero = 1 OR MedQty > 0)
  AND (
      @Search = N''
      OR MedName LIKE N'%' + @Search + N'%'
      OR ISNULL(Barcode, N'') LIKE N'%' + @Search + N'%'
      OR CompName LIKE N'%' + @Search + N'%'
  )
ORDER BY MedName;";
                command.Parameters.Add("@Search", SqlDbType.NVarChar, 200).Value = NormalizeSearch(searchText);
                command.Parameters.Add("@IncludeZero", SqlDbType.Bit).Value = includeZeroQuantity;
                using (SqlDataAdapter adapter = new SqlDataAdapter(command))
                {
                    DataTable table = new DataTable();
                    adapter.Fill(table);
                    return table;
                }
            }
        }

        public static DataTable GetLotStockSnapshot(string searchText, bool includeZeroQuantity)
        {
            using (SqlConnection connection = Database.CreateConnection())
                return GetLotStockSnapshot(connection, searchText, includeZeroQuantity);
        }

        internal static DataTable GetLotStockSnapshotQaReadOnly(string serverName, string databaseName,
            string searchText, bool includeZeroQuantity)
        {
            if (string.IsNullOrWhiteSpace(databaseName) ||
                !databaseName.Trim().EndsWith("_qa", StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("فحص جرد التشغيلات مسموح فقط لقاعدة QA.");
            using (var connection = new SqlConnection(Database.BuildSettingsConnectionString(serverName, databaseName, true)))
                return GetLotStockSnapshot(connection, searchText, includeZeroQuantity);
        }

        private static DataTable GetLotStockSnapshot(SqlConnection connection, string searchText,
            bool includeZeroQuantity, SqlTransaction transaction = null)
        {
            if (connection.State != ConnectionState.Open) connection.Open();
            using (var schema = new SqlCommand("SELECT OBJECT_ID(N'dbo.InventoryLotsTb', N'U')", connection, transaction))
                if (schema.ExecuteScalar() == DBNull.Value)
                    throw new InvalidOperationException("كشف التشغيلات يتطلب مخطط الدفعات التجاري.");

            using (SqlCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandTimeout = 30;
                command.CommandText = @"
SELECT l.MedName AS [الصنف], m.Barcode AS [الباركود], m.CompName AS [الشركة],
       l.BatchNumber AS [رقم التشغيلة], l.Gtin AS [GTIN], l.SupplierName AS [المورد],
       l.QuantityReceived AS [الكمية المستلمة], l.QuantityRemaining AS [الكمية الحالية],
       CASE WHEN l.IsRecalled = 0 AND l.QuantityRemaining > 0
                 AND l.ExpiryDate >= CAST(GETDATE() AS DATE)
            THEN l.QuantityRemaining ELSE CAST(0 AS DECIMAL(18,3)) END AS [الكمية القابلة للصرف],
       l.UnitCost AS [تكلفة الوحدة], m.Sprice AS [سعر البيع التقديري],
       CAST(l.QuantityRemaining * l.UnitCost AS DECIMAL(18,2)) AS [قيمة التكلفة],
       CAST(l.QuantityRemaining * m.Sprice AS DECIMAL(18,2)) AS [قيمة البيع],
       l.ProductionDate AS [تاريخ الإنتاج], l.ExpiryDate AS [تاريخ الانتهاء],
       CASE
           WHEN l.IsRecalled = 1 THEN N'مسحوبة'
           WHEN l.QuantityRemaining <= 0 THEN N'نفدت'
           WHEN l.ExpiryDate < CAST(GETDATE() AS DATE) THEN N'منتهية'
           WHEN l.ExpiryDate <= DATEADD(DAY, 30, CAST(GETDATE() AS DATE)) THEN N'قريبة الانتهاء'
           ELSE N'متاحة'
       END AS [حالة التشغيلة],
       l.LotId AS [معرف التشغيلة]
FROM dbo.InventoryLotsTb l
INNER JOIN dbo.MedicineTb1 m ON m.MedName = l.MedName
WHERE (@IncludeZero = 1 OR l.QuantityRemaining > 0)
  AND (@Search = N'' OR l.MedName LIKE N'%' + @Search + N'%'
       OR ISNULL(m.Barcode, N'') LIKE N'%' + @Search + N'%'
       OR m.CompName LIKE N'%' + @Search + N'%'
       OR l.BatchNumber LIKE N'%' + @Search + N'%'
       OR ISNULL(l.Gtin, N'') LIKE N'%' + @Search + N'%'
       OR ISNULL(l.SupplierName, N'') LIKE N'%' + @Search + N'%')
ORDER BY l.MedName, l.ExpiryDate, l.BatchNumber, l.LotId;";
                using(var metadata=new SqlCommand("SELECT CASE WHEN OBJECT_ID(N'dbo.UnitLotCostTb',N'U') IS NULL THEN 0 ELSE 1 END",connection,transaction))
                    command.CommandText=SqlUnitLotValuation.Build(command.CommandText,Convert.ToInt32(metadata.ExecuteScalar())==1);
                command.Parameters.Add("@Search", SqlDbType.NVarChar, 200).Value = NormalizeSearch(searchText);
                command.Parameters.Add("@IncludeZero", SqlDbType.Bit).Value = includeZeroQuantity;
                var table = new DataTable();
                using (var adapter = new SqlDataAdapter(command)) adapter.Fill(table);
                return table;
            }
        }

        internal static void VerifyQaLotStockRollback(string serverName, string databaseName)
        {
            if (string.IsNullOrWhiteSpace(databaseName) ||
                !databaseName.Trim().EndsWith("_qa", StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("فحص جرد التشغيلات الكتابي مسموح فقط لقاعدة QA.");

            Guid lotId = Guid.NewGuid();
            string batch = "QA-LOT-" + lotId.ToString("N").Substring(0, 12);
            using (var connection = new SqlConnection(Database.BuildSettingsConnectionString(serverName, databaseName, true)))
            {
                connection.Open();
                using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        string medicine;
                        using (var choose = new SqlCommand("SELECT TOP (1) MedName FROM dbo.MedicineTb1 ORDER BY MedName", connection, transaction))
                            medicine = Convert.ToString(choose.ExecuteScalar());
                        if (string.IsNullOrWhiteSpace(medicine))
                            throw new InvalidOperationException("قاعدة QA لا تحتوي صنفًا لفحص جرد التشغيلات.");

                        using (var insert = new SqlCommand(@"
INSERT dbo.InventoryLotsTb
    (LotId, MedName, BatchNumber, ExpiryDate, UnitCost, QuantityReceived, QuantityRemaining, IsRecalled)
VALUES (@LotId, @MedName, @Batch, @Expiry, @UnitCost, @Received, @Remaining, 0);", connection, transaction))
                        {
                            insert.Parameters.Add("@LotId", SqlDbType.UniqueIdentifier).Value = lotId;
                            insert.Parameters.Add("@MedName", SqlDbType.NVarChar, 200).Value = medicine;
                            insert.Parameters.Add("@Batch", SqlDbType.NVarChar, 100).Value = batch;
                            insert.Parameters.Add("@Expiry", SqlDbType.Date).Value = DateTime.Today.AddDays(90);
                            insert.Parameters.Add("@UnitCost", SqlDbType.Decimal).Value = 1.50m;
                            insert.Parameters.Add("@Received", SqlDbType.Decimal).Value = 3.000m;
                            insert.Parameters.Add("@Remaining", SqlDbType.Decimal).Value = 2.250m;
                            insert.ExecuteNonQuery();
                        }

                        DataTable available = GetLotStockSnapshot(connection, batch, false, transaction);
                        if (available.Rows.Count != 1 ||
                            Convert.ToDecimal(available.Rows[0]["الكمية الحالية"]) != 2.250m ||
                            Convert.ToDecimal(available.Rows[0]["الكمية القابلة للصرف"]) != 2.250m ||
                            Convert.ToDecimal(available.Rows[0]["قيمة التكلفة"]) != 3.38m ||
                            Convert.ToString(available.Rows[0]["حالة التشغيلة"]) != "متاحة")
                            throw new InvalidOperationException("جرد التشغيلة المتاحة غيّر الكمية أو التقريب أو الحالة.");

                        using (var recall = new SqlCommand("UPDATE dbo.InventoryLotsTb SET IsRecalled = 1 WHERE LotId = @LotId", connection, transaction))
                        {
                            recall.Parameters.Add("@LotId", SqlDbType.UniqueIdentifier).Value = lotId;
                            recall.ExecuteNonQuery();
                        }
                        DataTable recalled = GetLotStockSnapshot(connection, batch, false, transaction);
                        if (recalled.Rows.Count != 1 ||
                            Convert.ToDecimal(recalled.Rows[0]["الكمية القابلة للصرف"]) != 0m ||
                            Convert.ToString(recalled.Rows[0]["حالة التشغيلة"]) != "مسحوبة")
                            throw new InvalidOperationException("تشغيلة مسحوبة ظهرت ككمية قابلة للصرف.");

                        using (var expire = new SqlCommand(@"
UPDATE dbo.InventoryLotsTb SET IsRecalled = 0, ExpiryDate = @Yesterday WHERE LotId = @LotId", connection, transaction))
                        {
                            expire.Parameters.Add("@LotId", SqlDbType.UniqueIdentifier).Value = lotId;
                            expire.Parameters.Add("@Yesterday", SqlDbType.Date).Value = DateTime.Today.AddDays(-1);
                            expire.ExecuteNonQuery();
                        }
                        DataTable expired = GetLotStockSnapshot(connection, batch, false, transaction);
                        if (expired.Rows.Count != 1 ||
                            Convert.ToDecimal(expired.Rows[0]["الكمية القابلة للصرف"]) != 0m ||
                            Convert.ToString(expired.Rows[0]["حالة التشغيلة"]) != "منتهية")
                            throw new InvalidOperationException("تشغيلة منتهية ظهرت ككمية قابلة للصرف.");

                        using (var deplete = new SqlCommand("UPDATE dbo.InventoryLotsTb SET QuantityRemaining = 0 WHERE LotId = @LotId", connection, transaction))
                        {
                            deplete.Parameters.Add("@LotId", SqlDbType.UniqueIdentifier).Value = lotId;
                            deplete.ExecuteNonQuery();
                        }
                        DataTable hidden = GetLotStockSnapshot(connection, batch, false, transaction);
                        DataTable visible = GetLotStockSnapshot(connection, batch, true, transaction);
                        if (hidden.Rows.Count != 0 || visible.Rows.Count != 1 ||
                            Convert.ToString(visible.Rows[0]["حالة التشغيلة"]) != "نفدت")
                            throw new InvalidOperationException("فلتر التشغيلات الصفرية أو حالتها غير صحيح.");
                    }
                    finally { transaction.Rollback(); }
                }

                using (var residue = new SqlCommand("SELECT COUNT(*) FROM dbo.InventoryLotsTb WHERE LotId = @LotId", connection))
                {
                    residue.Parameters.Add("@LotId", SqlDbType.UniqueIdentifier).Value = lotId;
                    if (Convert.ToInt32(residue.ExecuteScalar()) != 0)
                        throw new InvalidOperationException("بقيت تشغيلة QA بعد استعادة المعاملة.");
                }
            }
        }

        public static DataTable GetMedicineMovement(string medicineName, DateTime fromDate, DateTime toDate)
        {
            using (SqlConnection connection = Database.CreateConnection())
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandText = @"
SELECT CreatedAt AS [التاريخ],
       MedName AS [الصنف],
       " + SourceLabelExpression("InventoryMovementsTb") + @" AS [نوع الحركة],
       SourceId AS [رقم المستند],
       CASE WHEN QuantityDelta > 0 THEN QuantityDelta ELSE 0 END AS [الوارد],
       CASE WHEN QuantityDelta < 0 THEN ABS(QuantityDelta) ELSE 0 END AS [الصادر],
       QuantityDelta AS [صافي الحركة],
       QuantityBefore AS [الرصيد قبل],
       QuantityAfter AS [الرصيد بعد],
       UnitCost AS [تكلفة الوحدة],
       UnitPrice AS [سعر الوحدة],
       CreatedBy AS [المستخدم],
       Notes AS [ملاحظات]
FROM dbo.InventoryMovementsTb
WHERE MedName = @MedName
  AND CreatedAt >= @FromDate
  AND CreatedAt < DATEADD(DAY, 1, @ToDate)
ORDER BY CreatedAt DESC, MovementId DESC;";
                command.Parameters.Add("@MedName", SqlDbType.NVarChar, 200).Value = RequireMedicineName(medicineName);
                AddDateParameters(command, fromDate, toDate);

                using (SqlDataAdapter adapter = new SqlDataAdapter(command))
                {
                    DataTable table = new DataTable();
                    adapter.Fill(table);
                    return table;
                }
            }
        }

        public static DataTable GetMovementSummary(DateTime fromDate, DateTime toDate, string searchText)
        {
            using (SqlConnection connection = Database.CreateConnection())
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandText = @"
SELECT m.MedName AS [الصنف],
       m.CompName AS [الشركة],
       m.MedQty AS [الرصيد الحالي],
       ISNULL(SUM(CASE WHEN mv.QuantityDelta > 0 THEN mv.QuantityDelta ELSE 0 END), 0) AS [إجمالي الوارد],
       ISNULL(SUM(CASE WHEN mv.QuantityDelta < 0 THEN ABS(mv.QuantityDelta) ELSE 0 END), 0) AS [إجمالي الصادر],
       ISNULL(SUM(mv.QuantityDelta), 0) AS [صافي الحركة],
       COUNT(mv.MovementId) AS [عدد الحركات],
       MAX(mv.CreatedAt) AS [آخر حركة]
FROM dbo.MedicineTb1 m
LEFT JOIN dbo.InventoryMovementsTb mv
    ON mv.MedName = m.MedName
   AND mv.CreatedAt >= @FromDate
   AND mv.CreatedAt < DATEADD(DAY, 1, @ToDate)
WHERE (
      @Search = N''
      OR m.MedName LIKE N'%' + @Search + N'%'
      OR m.CompName LIKE N'%' + @Search + N'%'
      OR ISNULL(m.Barcode, N'') LIKE N'%' + @Search + N'%'
)
GROUP BY m.MedName, m.CompName, m.MedQty
ORDER BY m.MedName;";
                command.Parameters.Add("@Search", SqlDbType.NVarChar, 200).Value = NormalizeSearch(searchText);
                AddDateParameters(command, fromDate, toDate);

                using (SqlDataAdapter adapter = new SqlDataAdapter(command))
                {
                    DataTable table = new DataTable();
                    adapter.Fill(table);
                    return table;
                }
            }
        }

        public static DataTable GetInventoryAlerts(int lowStockThreshold, int expiringWithinDays, IEnumerable<string> affectedMedicines = null)
        {
            return OperationalReportCatalog.ToDataTable(
                ApplicationServices.InventoryAlerts.GetAlerts(lowStockThreshold, expiringWithinDays, affectedMedicines));
        }

        internal static DataTable GetInventoryAlertsQaReadOnly(string serverName, string databaseName,
            int lowStockThreshold, int expiringWithinDays)
        {
            if (string.IsNullOrWhiteSpace(databaseName) ||
                !databaseName.Trim().EndsWith("_qa", StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("فحص تنبيهات المخزون مسموح فقط لقاعدة QA.");
            var service = new PharmacyManagement.Application.InventoryAlertReadService(
                new PharmacyManagement.Infrastructure.SqlServer.SqlInventoryAlertRepository(() =>
                    new SqlConnection(Database.BuildSettingsConnectionString(serverName, databaseName, true))));
            return OperationalReportCatalog.ToDataTable(service.GetAlerts(lowStockThreshold, expiringWithinDays));
        }

        public static DataTable GetTurnoverReport(DateTime fromDate, DateTime toDate, int idleDays)
        {
            if (fromDate.Date > toDate.Date)
                throw new InvalidOperationException("تاريخ البداية يجب ألا يتجاوز تاريخ النهاية.");
            if (idleDays < 1 || idleDays > 3650)
                throw new InvalidOperationException("أيام الركود يجب أن تكون بين 1 و3650 يومًا.");
            using (SqlConnection connection = Database.CreateConnection())
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandText = @"
WITH MovementWindow AS
(
    SELECT MedName,
           SUM(CASE WHEN QuantityDelta > 0 THEN QuantityDelta ELSE 0 END) AS ReceivedQty,
           SUM(CASE WHEN QuantityDelta < 0 THEN ABS(QuantityDelta) ELSE 0 END) AS IssuedQty,
           MAX(CASE WHEN QuantityDelta < 0 THEN CreatedAt ELSE NULL END) AS LastIssueAt,
           MAX(CreatedAt) AS LastMovementAt
    FROM dbo.InventoryMovementsTb
    WHERE CreatedAt >= @FromDate
      AND CreatedAt < DATEADD(DAY, 1, @ToDate)
    GROUP BY MedName
)
SELECT m.MedName AS [الصنف],
       m.CompName AS [الشركة],
       m.MedQty AS [الرصيد الحالي],
       ISNULL(w.ReceivedQty, 0) AS [الوارد خلال الفترة],
       ISNULL(w.IssuedQty, 0) AS [الصادر خلال الفترة],
       ISNULL(w.ReceivedQty, 0) - ISNULL(w.IssuedQty, 0) AS [صافي الحركة],
       CAST(
           CASE
               WHEN (m.MedQty + ISNULL(w.IssuedQty, 0)) <= 0 THEN 0
               ELSE ISNULL(w.IssuedQty, 0) * 1.0 / NULLIF((m.MedQty + ISNULL(w.IssuedQty, 0)) / 2.0, 0)
           END AS DECIMAL(18, 2)
       ) AS [معدل الدوران التقريبي],
       w.LastIssueAt AS [آخر صرف],
       w.LastMovementAt AS [آخر حركة],
       CASE
           WHEN m.MedQty > 0 AND (w.LastIssueAt IS NULL OR w.LastIssueAt < DATEADD(DAY, -@IdleDays, GETDATE())) THEN N'راكد'
           WHEN m.MedQty <= 10 THEN N'منخفض'
           WHEN ISNULL(w.IssuedQty, 0) > 0 THEN N'نشط'
           ELSE N'بدون حركة'
       END AS [التصنيف]
FROM dbo.MedicineTb1 m
LEFT JOIN MovementWindow w ON w.MedName = m.MedName
ORDER BY [التصنيف], m.MedName;";
                command.Parameters.Add("@IdleDays", SqlDbType.Int).Value = idleDays;
                AddDateParameters(command, fromDate, toDate);

                using (SqlDataAdapter adapter = new SqlDataAdapter(command))
                {
                    DataTable table = new DataTable();
                    adapter.Fill(table);
                    return table;
                }
            }
        }

        // فحص قراءة فقط لتسلسل الحركات ومطابقة الرصيد الحالي مع آخر حركة مسجلة.
        public static DataTable GetMovementIntegrityIssues()
        {
            return OperationalReportCatalog.ToDataTable(ApplicationServices.InventoryIntegrity.Load());
        }

        // مسار فحص منفصل للـ QA لا يعتمد على ملف تشغيل التطبيق ولا يقبل قاعدة إنتاج.
        internal static DataTable GetQaMovementIntegrityIssuesReadOnly(string serverName, string databaseName)
        {
            if (string.IsNullOrWhiteSpace(databaseName) || !databaseName.Trim().EndsWith("_qa", StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException("فحص سلامة المخزون المباشر مسموح فقط لقاعدة ينتهي اسمها بـ _qa.");
            }

            using (SqlConnection connection = new SqlConnection(Database.BuildSettingsConnectionString(serverName, databaseName, true)))
            {
                return GetMovementIntegrityIssues(connection);
            }
        }

        internal static DataTable GetMovementIntegrityIssues(SqlConnection connection)
        {
            return GetMovementIntegrityIssues(connection, null);
        }

        internal static DataTable GetMovementIntegrityIssues(SqlConnection connection, SqlTransaction transaction)
        {
            return OperationalReportCatalog.ToDataTable(
                PharmacyManagement.Infrastructure.SqlServer.SqlInventoryIntegrityReadRepository.Load(connection,transaction));
        }

        public static int GetCurrentQuantity(string medicineName)
        {
            using (SqlConnection connection = Database.CreateConnection())
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandText = "SELECT MedQty FROM dbo.MedicineTb1 WHERE MedName = @MedName";
                command.Parameters.Add("@MedName", SqlDbType.NVarChar, 200).Value = RequireMedicineName(medicineName);

                connection.Open();
                object value = command.ExecuteScalar();
                if (value == null || value == DBNull.Value)
                {
                    throw new InvalidOperationException("الصنف غير موجود في قاعدة البيانات.");
                }

                return Convert.ToInt32(value);
            }
        }

        public static void ApplyOpeningStock(string medicineName,int openingQuantity,string notes)
            => PostInventory(PharmacyManagement.Application.InventoryPostKind.OpeningStock,medicineName,openingQuantity,null,notes);
        public static void ApplyAdjustment(string medicineName,int countedQuantity,string notes)
            => PostInventory(PharmacyManagement.Application.InventoryPostKind.Adjustment,medicineName,countedQuantity,null,notes);
        public static void ApplyManualReceipt(string medicineName,int quantity,decimal? unitCost,string notes)
            => PostInventory(PharmacyManagement.Application.InventoryPostKind.Receipt,medicineName,quantity,unitCost,notes);
        public static void ApplyManualIssue(string medicineName,int quantity,decimal? unitPrice,string notes)
            => PostInventory(PharmacyManagement.Application.InventoryPostKind.Issue,medicineName,quantity,unitPrice,notes);
        private static void PostInventory(PharmacyManagement.Application.InventoryPostKind kind,string medicineName,int quantity,decimal? amount,string notes)
        {
            var request=new PharmacyManagement.Application.InventoryPostRequest(kind,"INV-"+Guid.NewGuid().ToString("N"),medicineName,quantity,amount,notes);
            var actor=new PharmacyManagement.Application.OperationActor {UserName=SessionContext.CurrentUser,UserRole=SessionContext.CurrentRole};
            ApplicationServices.InventoryPosting.Post(request,actor);
        }
        internal static void ExecuteStockOperation(SqlConnection connection,SqlTransaction transaction,string sourceType,string sourceId,string medicineName,int? finalQuantity,int? quantityDelta,decimal? unitCost,decimal? unitPrice,string notes,string defaultNotes,string auditAction,string actorName,string actorRole)
            => PharmacyManagement.Infrastructure.SqlServer.SqlInventoryPostRepository.ExecuteStockOperation(connection,transaction,sourceType,sourceId,medicineName,finalQuantity,quantityDelta,unitCost,unitPrice,notes,defaultNotes,auditAction,actorName,actorRole);
        private static void AddDateParameters(SqlCommand command, DateTime fromDate, DateTime toDate)
        {
            DateTime normalizedFrom = fromDate.Date;
            DateTime normalizedTo = toDate.Date < normalizedFrom ? normalizedFrom : toDate.Date;
            command.Parameters.Add("@FromDate", SqlDbType.DateTime).Value = normalizedFrom;
            command.Parameters.Add("@ToDate", SqlDbType.DateTime).Value = normalizedTo;
        }

        private static string NormalizeSearch(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private static string RequireMedicineName(string medicineName)
        {
            if (string.IsNullOrWhiteSpace(medicineName))
            {
                throw new InvalidOperationException("اختر الصنف أولاً.");
            }

            return medicineName.Trim();
        }

        private static string SourceLabelExpression(string alias)
        {
            return @"
CASE " + alias + @".SourceType
    WHEN N'SalesInvoice' THEN N'فاتورة بيع'
    WHEN N'PurchaseInvoice' THEN N'فاتورة شراء'
    WHEN N'OpeningStock' THEN N'مخزون أول المدة'
    WHEN N'StockAdjustment' THEN N'تسوية مخزون'
    WHEN N'StockReceipt' THEN N'توريد مخزني'
    WHEN N'StockIssue' THEN N'صرف مخزني'
    ELSE " + alias + @".SourceType
END";
        }
    }
}

using System;
using System.Data;
using System.Data.SqlClient;
using PharmacyManagement.Application;
using PharmacyManagement.Domain;
using project_fo_3;

namespace PharmacyManagement.Infrastructure.SqlServer
{
    public sealed class SqlInventoryPostRepository : IInventoryPostRepository
    {
        private readonly Func<SqlConnection> connectionFactory;
        public SqlInventoryPostRepository(Func<SqlConnection> connectionFactory)
        { this.connectionFactory=connectionFactory??throw new ArgumentNullException(nameof(connectionFactory)); }
        public InventoryPostResult Post(InventoryPostRequest request,OperationActor actor)
        {
            using(var connection=connectionFactory())
            {
                connection.Open();
                using(var transaction=connection.BeginTransaction())
                {
                    bool newSource=false,commitAttempted=false;
                    try
                    {
                        var result=PostInTransactionCore(connection,transaction,request,actor,()=>newSource=true);
                        commitAttempted=true;transaction.Commit();return result;
                    }
                    catch(Exception error)
                    {
                        bool rolledBack=false;
                        try{transaction.Rollback();rolledBack=true;}catch{}
                        if(newSource&&!commitAttempted&&rolledBack)
                            throw new InventoryPostNotCommittedException(request.SourceId,error.Message,error);
                        throw;
                    }
                }
            }
        }
        public static bool IsFullCommercialModeRequested(SqlConnection connection,SqlTransaction transaction)
        {
            if(connection==null)throw new ArgumentNullException(nameof(connection));
            const string sql=@"IF OBJECT_ID('dbo.CommercialOperationalSettingsTb', 'U') IS NULL SELECT CAST(0 AS BIT)
ELSE SELECT CAST(CASE WHEN EXISTS (SELECT 1 FROM dbo.CommercialOperationalSettingsTb WITH (UPDLOCK, HOLDLOCK) WHERE SettingKey = N'Mode' AND SettingValue = N'FullCommercial') THEN 1 ELSE 0 END AS BIT);";
            using(var command=new SqlCommand(sql,connection,transaction))return Convert.ToBoolean(command.ExecuteScalar());
        }
        public static InventoryPostResult PostInTransaction(SqlConnection connection,SqlTransaction transaction,InventoryPostRequest request,OperationActor actor)
            => PostInTransactionCore(connection,transaction,request,actor,null);
        private static InventoryPostResult PostInTransactionCore(SqlConnection connection,SqlTransaction transaction,InventoryPostRequest request,OperationActor actor,Action onNewSource)
        {
            if(request==null)throw new ArgumentNullException(nameof(request));
            if(actor==null)throw new ArgumentNullException(nameof(actor));
            LockRequest(connection,transaction,request.SourceId);
            var existing=FindCommittedRequest(connection,transaction,request,actor);
            if(existing!=null)return existing;
            onNewSource?.Invoke();
            bool setBalance=request.Kind==InventoryPostKind.OpeningStock||request.Kind==InventoryPostKind.Adjustment;
            return ExecuteStockOperation(connection,transaction,request.SourceType,request.SourceId,request.MedicineName,
                setBalance?(int?)request.Quantity:null,setBalance?(int?)null:(request.Kind==InventoryPostKind.Issue?-request.Quantity:request.Quantity),
                request.Kind==InventoryPostKind.Receipt?request.UnitAmount:null,request.Kind==InventoryPostKind.Issue?request.UnitAmount:null,
                request.Notes,request.Caption,request.AuditAction,actor.UserName,actor.UserRole);
        }
        private static void LockRequest(SqlConnection connection,SqlTransaction transaction,string sourceId)
        {
            if(!System.Text.RegularExpressions.Regex.IsMatch(sourceId??string.Empty,"^[A-Za-z0-9_-]{1,50}$"))
                throw new InvalidOperationException("مرجع حركة المخزون غير صالح.");
            using(var command=new SqlCommand(@"DECLARE @Result int;
EXEC @Result=sys.sp_getapplock @Resource=@Resource,@LockMode=N'Exclusive',@LockOwner=N'Transaction',@LockTimeout=15000;
SELECT @Result;",connection,transaction))
            {
                command.Parameters.Add("@Resource",SqlDbType.NVarChar,255).Value="Pharmacy.InventoryPost."+sourceId.ToUpperInvariant();
                if(Convert.ToInt32(command.ExecuteScalar())<0)
                    throw new InvalidOperationException("تعذر تأكيد حركة المخزون الآن؛ أعد محاولة الطلب نفسه.");
            }
        }
        private static InventoryPostResult FindCommittedRequest(SqlConnection connection,SqlTransaction transaction,InventoryPostRequest request,OperationActor actor)
        {
            InventoryPostResult result=null;
            using(var command=new SqlCommand(@"SELECT SourceId,SourceType,MedName,QuantityBefore,QuantityAfter,QuantityDelta,UnitCost,UnitPrice,Notes,CreatedBy,@UnitAmount AS RequestedUnitAmount
FROM dbo.InventoryMovementsTb WHERE SourceId=@SourceId AND SourceType IN (N'OpeningStock',N'StockAdjustment',N'StockReceipt',N'StockIssue');",connection,transaction))
            {
                command.Parameters.Add("@SourceId",SqlDbType.NVarChar,50).Value=request.SourceId;
                var amount=command.Parameters.Add("@UnitAmount",SqlDbType.Decimal);amount.Precision=18;amount.Scale=2;amount.Value=request.UnitAmount.HasValue?(object)request.UnitAmount.Value:DBNull.Value;
                using(var reader=command.ExecuteReader())
                {
                    if(!reader.Read())return null;
                    int before=reader.GetInt32(3),after=reader.GetInt32(4),delta=reader.GetInt32(5);
                    bool balance=request.Kind==InventoryPostKind.OpeningStock||request.Kind==InventoryPostKind.Adjustment;
                    object expected=reader.GetValue(10);
                    bool same=string.Equals(reader.GetString(0),request.SourceId,StringComparison.Ordinal)&&
                        string.Equals(reader.GetString(1),request.SourceType,StringComparison.Ordinal)&&
                        string.Equals(reader.GetString(2),request.MedicineName,StringComparison.Ordinal)&&
                        string.Equals(reader.IsDBNull(8)?null:reader.GetString(8),request.Notes,StringComparison.Ordinal)&&
                        string.Equals(reader.GetString(9),actor.UserName,StringComparison.Ordinal)&&
                        (balance?after==request.Quantity:delta==(request.Kind==InventoryPostKind.Issue?-request.Quantity:request.Quantity))&&
                        Equals(reader.GetValue(6),request.Kind==InventoryPostKind.Receipt?expected:DBNull.Value)&&
                        Equals(reader.GetValue(7),request.Kind==InventoryPostKind.Issue?expected:DBNull.Value);
                    if(!same||reader.Read())throw new InvalidOperationException("مرجع الحركة مستخدم لبيانات مختلفة أو سجل غير متسق؛ راجع الحركة المحفوظة.");
                    var change=InventoryQuantityCalculator.Calculate(before,after,null);
                    if(change.Delta!=delta)throw new InvalidOperationException("سجل حركة المخزون غير متسق؛ لا يمكن تأكيد إعادة الطلب.");
                    result=new InventoryPostResult(request.SourceId,before,after,delta,true);
                }
            }
            using(var audit=new SqlCommand(@"SELECT COUNT(*) FROM dbo.AuditLogTb WHERE EntityName=N'Inventory' AND ActionName=@Action AND UserName=@Actor
AND CHARINDEX(N'مرجع الحركة: '+@SourceId+N'،',Details)>0;",connection,transaction))
            {
                audit.Parameters.Add("@Action",SqlDbType.NVarChar,100).Value=request.AuditAction;
                audit.Parameters.Add("@Actor",SqlDbType.NVarChar,200).Value=actor.UserName;
                audit.Parameters.Add("@SourceId",SqlDbType.NVarChar,50).Value=request.SourceId;
                if(Convert.ToInt32(audit.ExecuteScalar())!=1)throw new InvalidOperationException("سجل اعتماد حركة المخزون غير متسق؛ لا يمكن تأكيد إعادة الطلب.");
            }
            return result;
        }

        public static InventoryPostResult ExecuteStockOperation(SqlConnection connection, SqlTransaction transaction,
            string sourceType, string sourceId, string medicineName, int? finalQuantity, int? quantityDelta,
            decimal? unitCost, decimal? unitPrice, string notes, string defaultNotes, string auditAction,
            string actorName, string actorRole)
        {
            CommercialInventoryOperationPolicy.EnsureLegacyAggregateOperationAllowed(
                IsFullCommercialModeRequested(connection, transaction));

            // Legacy movement dates use SQL GETDATE (server-local), not the workstation clock.
            // Capture one value for both the period guard and the persisted movement.
            DateTime movementDate;
            using(var clock=new SqlCommand("SELECT GETDATE();",connection,transaction))movementDate=Convert.ToDateTime(clock.ExecuteScalar());
            SqlBusinessPeriodPolicy.EnsureBusinessDateOpen(connection,transaction,movementDate);
            int before = SqlInventoryMovementWriter.GetCurrentQuantity(connection, transaction, medicineName);
            var quantityChange = PharmacyManagement.Domain.InventoryQuantityCalculator.Calculate(before, finalQuantity, quantityDelta);
            int after = quantityChange.After;

            using (SqlCommand update = new SqlCommand(@"
UPDATE dbo.MedicineTb1 SET MedQty = @Quantity WHERE MedName = @MedName;", connection, transaction))
            {
                update.Parameters.Add("@Quantity", SqlDbType.Int).Value = after;
                update.Parameters.Add("@MedName", SqlDbType.NVarChar, 200).Value = medicineName;
                if (update.ExecuteNonQuery() != 1)
                    throw new InvalidOperationException("تعذر تحديث رصيد الصنف.");
            }

            int delta = quantityChange.Delta;
            SqlInventoryMovementWriter.RecordAtDatabaseTime(connection, transaction, medicineName, delta, before, after,
                sourceType, sourceId, unitCost, unitPrice,
                string.IsNullOrWhiteSpace(notes) ? defaultNotes : notes.Trim(), actorName, movementDate);

            using (SqlCommand audit = new SqlCommand(@"
INSERT INTO dbo.AuditLogTb (UserName, UserRole, ActionName, EntityName, Details)
VALUES (@UserName, @UserRole, @ActionName, N'Inventory', @Details);", connection, transaction))
            {
                audit.Parameters.Add("@UserName", SqlDbType.NVarChar, 200).Value = actorName;
                audit.Parameters.Add("@UserRole", SqlDbType.NVarChar, 50).Value = actorRole;
                audit.Parameters.Add("@ActionName", SqlDbType.NVarChar, 100).Value = auditAction;
                audit.Parameters.Add("@Details", SqlDbType.NVarChar, 1000).Value =
                    $"{defaultNotes}. مرجع الحركة: {sourceId}، الصنف: {medicineName}، قبل: {before}، بعد: {after}، الفرق: {delta}. السبب: {notes}.";
                audit.ExecuteNonQuery();
            }
            return new InventoryPostResult(sourceId,before,after,delta);
        }

    }
}
using System;
using System.Data;
using System.Data.SqlClient;

namespace project_fo_3
{
    internal static class PurchaseAsyncInventoryMovementWriter
    {
        public static async System.Threading.Tasks.Task<int> GetCurrentQuantityAsync(SqlConnection connection, SqlTransaction transaction, string medicineName, System.Threading.CancellationToken cancellationToken)
        {
            using (SqlCommand command = new SqlCommand(@"
SELECT MedQty
FROM dbo.MedicineTb1 WITH (UPDLOCK, ROWLOCK)
WHERE MedName = @MedName", connection, transaction))
            {
                command.Parameters.Add("@MedName", SqlDbType.NVarChar, 200).Value = medicineName;
                object value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                if (value == null || value == DBNull.Value)
                {
                    throw new InvalidOperationException("لم يتم العثور على الدواء أثناء فحص المخزون: " + medicineName);
                }

                return Convert.ToInt32(value);
            }
        }

        public static async System.Threading.Tasks.Task RecordAsync(
            SqlConnection connection,
            SqlTransaction transaction,
            string medicineName,
            int quantityDelta,
            int quantityBefore,
            int quantityAfter,
            string sourceType,
            string sourceId,
            decimal? unitCost,
            decimal? unitPrice,
            string notes,
            string createdBy, System.Threading.CancellationToken cancellationToken)
        {
            await RecordCoreAsync(
                connection, transaction, medicineName, quantityDelta, quantityBefore, quantityAfter,
                sourceType, sourceId, unitCost, unitPrice, notes, createdBy, null,cancellationToken).ConfigureAwait(false);
        }

        public static async System.Threading.Tasks.Task RecordAsync(
            SqlConnection connection,
            SqlTransaction transaction,
            string medicineName,
            int quantityDelta,
            int quantityBefore,
            int quantityAfter,
            string sourceType,
            string sourceId,
            decimal? unitCost,
            decimal? unitPrice,
            string notes,
            string createdBy,
            DateTime businessDateUtc, System.Threading.CancellationToken cancellationToken)
        {
            await RecordCoreAsync(
                connection, transaction, medicineName, quantityDelta, quantityBefore, quantityAfter,
                sourceType, sourceId, unitCost, unitPrice, notes, createdBy, businessDateUtc.ToUniversalTime(),cancellationToken).ConfigureAwait(false);
        }

        // Preserve the existing GETDATE calendar for legacy manual movements.
        public static async System.Threading.Tasks.Task RecordAtDatabaseTimeAsync(SqlConnection connection,SqlTransaction transaction,string medicineName,
            int quantityDelta,int quantityBefore,int quantityAfter,string sourceType,string sourceId,
            decimal? unitCost,decimal? unitPrice,string notes,string createdBy,DateTime databaseTime, System.Threading.CancellationToken cancellationToken)
        {
            await RecordCoreAsync(connection,transaction,medicineName,quantityDelta,quantityBefore,quantityAfter,
                sourceType,sourceId,unitCost,unitPrice,notes,createdBy,databaseTime,cancellationToken).ConfigureAwait(false);
        }
        private static async System.Threading.Tasks.Task RecordCoreAsync(
            SqlConnection connection,
            SqlTransaction transaction,
            string medicineName,
            int quantityDelta,
            int quantityBefore,
            int quantityAfter,
            string sourceType,
            string sourceId,
            decimal? unitCost,
            decimal? unitPrice,
            string notes,
            string createdBy,
            DateTime? createdAtUtc, System.Threading.CancellationToken cancellationToken)
        {
            string sql = createdAtUtc.HasValue
                ? @"
INSERT INTO dbo.InventoryMovementsTb
    (MedName, QuantityDelta, QuantityBefore, QuantityAfter, SourceType, SourceId, UnitCost, UnitPrice, Notes, CreatedBy, CreatedAt)
VALUES
    (@MedName, @QuantityDelta, @QuantityBefore, @QuantityAfter, @SourceType, @SourceId, @UnitCost, @UnitPrice, @Notes, @CreatedBy, @CreatedAt);"
                : @"
INSERT INTO dbo.InventoryMovementsTb
    (MedName, QuantityDelta, QuantityBefore, QuantityAfter, SourceType, SourceId, UnitCost, UnitPrice, Notes, CreatedBy)
VALUES
    (@MedName, @QuantityDelta, @QuantityBefore, @QuantityAfter, @SourceType, @SourceId, @UnitCost, @UnitPrice, @Notes, @CreatedBy);";

            using (SqlCommand command = new SqlCommand(sql, connection, transaction))
            {
                command.Parameters.Add("@MedName", SqlDbType.NVarChar, 200).Value = medicineName;
                command.Parameters.Add("@QuantityDelta", SqlDbType.Int).Value = quantityDelta;
                command.Parameters.Add("@QuantityBefore", SqlDbType.Int).Value = quantityBefore;
                command.Parameters.Add("@QuantityAfter", SqlDbType.Int).Value = quantityAfter;
                command.Parameters.Add("@SourceType", SqlDbType.NVarChar, 50).Value = sourceType;
                command.Parameters.Add("@SourceId", SqlDbType.NVarChar, 50).Value = string.IsNullOrWhiteSpace(sourceId) ? (object)DBNull.Value : sourceId;
                command.Parameters.Add("@UnitCost", SqlDbType.Decimal).Value = unitCost.HasValue ? (object)unitCost.Value : DBNull.Value;
                command.Parameters["@UnitCost"].Precision = 18;
                command.Parameters["@UnitCost"].Scale = 2;
                command.Parameters.Add("@UnitPrice", SqlDbType.Decimal).Value = unitPrice.HasValue ? (object)unitPrice.Value : DBNull.Value;
                command.Parameters["@UnitPrice"].Precision = 18;
                command.Parameters["@UnitPrice"].Scale = 2;
                command.Parameters.Add("@Notes", SqlDbType.NVarChar, 500).Value = string.IsNullOrWhiteSpace(notes) ? (object)DBNull.Value : notes;
                command.Parameters.Add("@CreatedBy", SqlDbType.NVarChar, 200).Value = string.IsNullOrWhiteSpace(createdBy) ? "System" : createdBy;
                if (createdAtUtc.HasValue)
                {
                    command.Parameters.Add("@CreatedAt", SqlDbType.DateTime2).Value = createdAtUtc.Value;
                }
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }
}

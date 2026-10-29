using System;
using System.Data.SqlClient;
using PharmacyManagement.Infrastructure.SqlServer;
namespace project_fo_3
{
    internal static class InventoryMovementService
    {
        public static int GetCurrentQuantity(SqlConnection connection,SqlTransaction transaction,string medicineName)
            => SqlInventoryMovementWriter.GetCurrentQuantity(connection,transaction,medicineName);
        public static void Record(SqlConnection connection,SqlTransaction transaction,string medicineName,int quantityDelta,int quantityBefore,int quantityAfter,string sourceType,string sourceId,decimal? unitCost,decimal? unitPrice,string notes,string createdBy)
            => SqlInventoryMovementWriter.Record(connection,transaction,medicineName,quantityDelta,quantityBefore,quantityAfter,sourceType,sourceId,unitCost,unitPrice,notes,createdBy);
        public static void Record(SqlConnection connection,SqlTransaction transaction,string medicineName,int quantityDelta,int quantityBefore,int quantityAfter,string sourceType,string sourceId,decimal? unitCost,decimal? unitPrice,string notes,string createdBy,DateTime businessDateUtc)
            => SqlInventoryMovementWriter.Record(connection,transaction,medicineName,quantityDelta,quantityBefore,quantityAfter,sourceType,sourceId,unitCost,unitPrice,notes,createdBy,businessDateUtc);
    }
}
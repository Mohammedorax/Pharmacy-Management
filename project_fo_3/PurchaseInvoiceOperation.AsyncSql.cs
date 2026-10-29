using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Application;
namespace project_fo_3 {
 internal sealed partial class PurchaseInvoiceOperation {
        private static async Task InsertHeaderAsync(SqlConnection connection, SqlTransaction transaction, string purchaseId,
            PurchaseInvoiceRequest request, DateTime purchaseDate, decimal totalAmount, CancellationToken cancellationToken)
        {
            using (SqlCommand command = new SqlCommand(@"INSERT INTO dbo.PurchasesTb
(PurchaseId, SupplierName, PurchaseDate, TotalAmount, EmployeeName, Notes)
VALUES (@PurchaseId, @SupplierName, @PurchaseDate, @TotalAmount, @EmployeeName, @Notes);", connection, transaction))
            {
                command.Parameters.Add("@PurchaseId", SqlDbType.NVarChar, 50).Value = purchaseId;
                command.Parameters.Add("@SupplierName", SqlDbType.NVarChar, 200).Value = request.SupplierName;
                command.Parameters.Add("@PurchaseDate", SqlDbType.DateTime).Value = purchaseDate;
                Decimal(command, "@TotalAmount", totalAmount, 2);
                command.Parameters.Add("@EmployeeName", SqlDbType.NVarChar, 200).Value = request.EmployeeName;
                command.Parameters.Add("@Notes", SqlDbType.NVarChar, 1000).Value = request.Notes.Length == 0 ? (object)DBNull.Value : request.Notes;
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }


        private static async Task InsertLineAsync(SqlConnection connection, SqlTransaction transaction, string purchaseId,
            string detailId, PurchaseInvoiceLineRequest line, CancellationToken cancellationToken)
        {
            using (SqlCommand command = new SqlCommand(@"INSERT INTO dbo.PurchaseDetailsTb
(DetailId, PurchaseId, MedName, BuyingPrice, Quantity, TotalAmount, ProdDate, ExpDate)
VALUES (@DetailId, @PurchaseId, @MedName, @BuyingPrice, @Quantity, @TotalAmount, @ProdDate, @ExpDate);", connection, transaction))
            {
                command.Parameters.Add("@DetailId", SqlDbType.NVarChar, 50).Value = detailId;
                command.Parameters.Add("@PurchaseId", SqlDbType.NVarChar, 50).Value = purchaseId;
                command.Parameters.Add("@MedName", SqlDbType.NVarChar, 200).Value = line.MedicineName;
                Decimal(command, "@BuyingPrice", line.Unit?.UnitPrice ?? line.BuyingPrice, 2);
                command.Parameters.Add("@Quantity", SqlDbType.Int).Value = line.Quantity;
                Decimal(command, "@TotalAmount", line.Unit?.GrossTotal ?? checked(line.BuyingPrice * line.Quantity), 2);
                command.Parameters.Add("@ProdDate", SqlDbType.Date).Value = line.ProductionDate;
                command.Parameters.Add("@ExpDate", SqlDbType.Date).Value = line.ExpiryDate;
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }


        private static async Task UpdateStockAsync(SqlConnection connection, SqlTransaction transaction, PurchaseInvoiceLineRequest line, CancellationToken cancellationToken)
        {
            using (SqlCommand command = new SqlCommand(@"UPDATE dbo.MedicineTb1
SET MedQty=MedQty+@Quantity,
    ProdDate=CASE WHEN ProdDate IS NULL OR ProdDate>@ProdDate THEN @ProdDate ELSE ProdDate END,
    ExpDate=CASE WHEN ExpDate IS NULL OR ExpDate>@ExpDate THEN @ExpDate ELSE ExpDate END
WHERE MedName=@MedName;", connection, transaction))
            {
                command.Parameters.Add("@Quantity", SqlDbType.Int).Value = line.Quantity;
                command.Parameters.Add("@ProdDate", SqlDbType.Date).Value = line.ProductionDate;
                command.Parameters.Add("@ExpDate", SqlDbType.Date).Value = line.ExpiryDate;
                command.Parameters.Add("@MedName", SqlDbType.NVarChar, 200).Value = line.MedicineName;
                if (await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) != 1)
                    throw new InvalidOperationException("لم يتم العثور على الدواء أثناء تحديث مخزون المشتريات: " + line.MedicineName);
            }
        }


        private static async Task<string> ResolveDetailIdAsync(SqlConnection connection, SqlTransaction transaction, string desiredId, HashSet<long> reservedIds, CancellationToken cancellationToken)
        {
            long numeric;
            if (!long.TryParse(desiredId, out numeric) || reservedIds.Contains(numeric) ||
                await IdentifierExistsAsync(connection, transaction, "PurchaseDetailsTb", "DetailId", desiredId,cancellationToken).ConfigureAwait(false))
            {
                do { numeric = await NextPurchaseSequenceAsync(connection,transaction,"Seq_PurchaseDetailId",cancellationToken).ConfigureAwait(false); }
                while (reservedIds.Contains(numeric));
            }
            reservedIds.Add(numeric);
            return numeric.ToString();
        }


        private static async Task<bool> IdentifierExistsAsync(SqlConnection connection, SqlTransaction transaction, string table, string column, string value, CancellationToken cancellationToken)
        {
            using (SqlCommand command = new SqlCommand("SELECT COUNT(1) FROM dbo." + table + " WITH (UPDLOCK,HOLDLOCK) WHERE " + column + "=@Value;", connection, transaction))
            {
                command.Parameters.Add("@Value", SqlDbType.NVarChar, 50).Value = value ?? string.Empty;
                return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)) > 0;
            }
        }


        private static async Task CreateCommercialLotAsync(SqlConnection connection, SqlTransaction transaction,
            PurchaseInvoiceLineRequest line, string detailId, string supplierName, CancellationToken cancellationToken)
        {
            if (line.Gtin.Length > 0) Gs1DataMatrixParser.ParseHumanReadable("(01)" + line.Gtin);
            using (SqlCommand duplicate = new SqlCommand(@"SELECT COUNT(*) FROM dbo.InventoryLotsTb WITH (UPDLOCK,HOLDLOCK)
WHERE MedName=@MedName AND BatchNumber=@BatchNumber AND ExpiryDate=@ExpiryDate
AND ISNULL(SerialNumber,N'')=@SerialNumber;", connection, transaction))
            {
                duplicate.Parameters.Add("@MedName", SqlDbType.NVarChar, 200).Value = line.MedicineName;
                duplicate.Parameters.Add("@BatchNumber", SqlDbType.NVarChar, 100).Value = line.BatchNumber;
                duplicate.Parameters.Add("@ExpiryDate", SqlDbType.Date).Value = line.ExpiryDate;
                duplicate.Parameters.Add("@SerialNumber", SqlDbType.NVarChar, 100).Value = line.SerialNumber;
                if (Convert.ToInt32(await duplicate.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)) > 0)
                    throw new InvalidOperationException("هذه التشغيلة مسجلة بالفعل. استخدم تسوية مخزون موثقة بدلاً من استلامها مرة أخرى.");
            }

            using (SqlCommand insert = new SqlCommand(@"INSERT INTO dbo.InventoryLotsTb
(LotId,MedName,Gtin,SerialNumber,BatchNumber,ProductionDate,ExpiryDate,SupplierName,PurchaseDetailId,UnitCost,QuantityReceived,QuantityRemaining)
VALUES (@LotId,@MedName,@Gtin,@SerialNumber,@BatchNumber,@ProductionDate,@ExpiryDate,@SupplierName,@PurchaseDetailId,@UnitCost,@Quantity,@Quantity);", connection, transaction))
            {
                insert.Parameters.Add("@LotId", SqlDbType.UniqueIdentifier).Value = Guid.NewGuid();
                insert.Parameters.Add("@MedName", SqlDbType.NVarChar, 200).Value = line.MedicineName;
                insert.Parameters.Add("@Gtin", SqlDbType.NVarChar, 20).Value = line.Gtin.Length == 0 ? (object)DBNull.Value : line.Gtin;
                insert.Parameters.Add("@SerialNumber", SqlDbType.NVarChar, 100).Value = line.SerialNumber.Length == 0 ? (object)DBNull.Value : line.SerialNumber;
                insert.Parameters.Add("@BatchNumber", SqlDbType.NVarChar, 100).Value = line.BatchNumber;
                insert.Parameters.Add("@ProductionDate", SqlDbType.Date).Value = line.ProductionDate;
                insert.Parameters.Add("@ExpiryDate", SqlDbType.Date).Value = line.ExpiryDate;
                insert.Parameters.Add("@SupplierName", SqlDbType.NVarChar, 200).Value = supplierName;
                insert.Parameters.Add("@PurchaseDetailId", SqlDbType.NVarChar, 50).Value = detailId;
                Decimal(insert, "@UnitCost", line.Unit == null ? line.BuyingPrice : decimal.Round(line.Unit.GrossTotal / line.Quantity, 2, MidpointRounding.AwayFromZero), 2);
                Decimal(insert, "@Quantity", line.Quantity, 3);
                await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }


        private static async Task InsertAuditAsync(SqlConnection connection, SqlTransaction transaction, PurchaseInvoiceRequest request,
            string purchaseId, decimal totalAmount, CancellationToken cancellationToken)
        {
            using (SqlCommand command = new SqlCommand(@"INSERT INTO dbo.AuditLogTb
(UserName,UserRole,ActionName,EntityName,Details)
VALUES (@UserName,@UserRole,N'PurchaseInvoiceSaved',N'Purchases',@Details);", connection, transaction))
            {
                command.Parameters.Add("@UserName", SqlDbType.NVarChar, 200).Value = request.EmployeeName;
                command.Parameters.Add("@UserRole", SqlDbType.NVarChar, 50).Value = request.ActorRole.Length == 0 ? "Unknown" : request.ActorRole;
                command.Parameters.Add("@Details", SqlDbType.NVarChar, 1000).Value =
                    "فاتورة مشتريات محفوظة. الرقم: " + purchaseId + "، المورد: " + request.SupplierName +
                    "، الإجمالي: " + totalAmount.ToString("N2") + "، عدد البنود: " + request.Lines.Count + ".";
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        private static async Task<long> NextPurchaseSequenceAsync(SqlConnection connection,SqlTransaction transaction,string sequenceName,CancellationToken cancellationToken){
            if(sequenceName!="Seq_PurchaseId"&&sequenceName!="Seq_PurchaseDetailId")throw new ArgumentException("Unknown purchase sequence.",nameof(sequenceName));
            using(var command=new SqlCommand("SELECT NEXT VALUE FOR dbo."+sequenceName,connection,transaction))
                return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
        }
 }
}

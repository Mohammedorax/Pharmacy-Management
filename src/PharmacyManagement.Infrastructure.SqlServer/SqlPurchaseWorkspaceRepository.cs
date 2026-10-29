using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using PharmacyManagement.Application;

namespace PharmacyManagement.Infrastructure.SqlServer
{
    public sealed class SqlPurchaseWorkspaceRepository : IPurchaseWorkspaceRepository
    {
        private readonly Func<SqlConnection> connectionFactory;

        public SqlPurchaseWorkspaceRepository(Func<SqlConnection> connectionFactory)
        {
            this.connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        }

        public IReadOnlyList<PurchaseHistoryRecord> GetHistory()
        {
            var rows = new List<PurchaseHistoryRecord>();
            using (SqlConnection connection = connectionFactory())
            using (SqlCommand command = new SqlCommand(@"SELECT p.PurchaseId, p.SupplierName, p.PurchaseDate, p.TotalAmount, p.EmployeeName, p.Notes,
CASE WHEN EXISTS (SELECT 1 FROM dbo.JournalEntriesTb c WHERE c.SourceType=N'PurchaseInvoiceCancellation' AND c.SourceId=p.PurchaseId) THEN 1 ELSE 0 END
, original.SystemCurrencyCode
FROM dbo.PurchasesTb p
OUTER APPLY (SELECT CASE WHEN COUNT(*)=1 THEN MAX(j.SystemCurrencyCode) ELSE NULL END AS SystemCurrencyCode
 FROM dbo.JournalEntriesTb j WHERE j.SourceType=N'PurchaseInvoice' AND j.SourceId=p.PurchaseId) original
ORDER BY p.PurchaseDate DESC;", connection))
            {
                connection.Open();
                using (SqlDataReader reader = command.ExecuteReader())
                    while (reader.Read())
                        rows.Add(new PurchaseHistoryRecord
                        {
                            PurchaseId = Text(reader, 0), SupplierName = Text(reader, 1),
                            PurchaseDate = reader.GetDateTime(2), TotalAmount = Convert.ToDecimal(reader[3]),
                            EmployeeName = Text(reader, 4), Notes = Text(reader, 5), IsCancelled = Convert.ToInt32(reader[6]) == 1,
                            SystemCurrencyCode = reader.IsDBNull(7) ? null : Text(reader, 7)
                        });
            }
            return rows.AsReadOnly();
        }

        public IReadOnlyList<PurchaseHistoryLine> GetHistoryLines(string purchaseId)
        {
            var rows = new List<PurchaseHistoryLine>();
            var keys=new List<string>();
            var required=new List<bool>();
            using (SqlConnection connection = connectionFactory())
            using (SqlCommand command = new SqlCommand(@"SELECT MedName, BuyingPrice, Quantity, TotalAmount, ProdDate, ExpDate, CONVERT(nvarchar(100),DetailId), {SnapshotRequired}
FROM dbo.PurchaseDetailsTb WHERE PurchaseId=@PurchaseId ORDER BY DetailId;", connection))
            {
                command.Parameters.Add("@PurchaseId", SqlDbType.NVarChar, 50).Value = purchaseId;
                connection.Open();
                command.CommandText=command.CommandText.Replace("{SnapshotRequired}",SavedHistoryUnitReader.HasRequiredFlag(connection,"PurchaseDetailsTb")?"UnitSnapshotRequired":"CAST(0 AS bit)");
                using (SqlDataReader reader = command.ExecuteReader())
                    while (reader.Read())
                    {
                        keys.Add(Text(reader,6));
                        required.Add(reader.GetBoolean(7));
                        rows.Add(new PurchaseHistoryLine
                        {
                            MedicineName = Text(reader, 0), BuyingPrice = Convert.ToDecimal(reader[1]),
                            Quantity = Convert.ToDecimal(reader[2]), BaseQuantity=Convert.ToInt32(reader[2]), Unit=string.Empty, TotalAmount = Convert.ToDecimal(reader[3]),
                            ProductionDate = NullableDate(reader, 4), ExpiryDate = NullableDate(reader, 5)
                        });
                    }
                var units=SavedHistoryUnitReader.Load(connection,"Purchase",keys);
                for(int i=0;i<rows.Count;i++)
                {
                    SavedHistoryUnit saved;if(!units.TryGetValue(keys[i],out saved))
                    {if(required[i])throw new InvalidOperationException("لقطة وحدة سطر المشتريات مفقودة؛ لم يعرض السطر كوحدة أساسية.");continue;}
                    if(!string.Equals(saved.MedicineName,rows[i].MedicineName,StringComparison.OrdinalIgnoreCase) || saved.Price!=rows[i].BuyingPrice || saved.BaseQuantity!=rows[i].BaseQuantity)
                        throw new InvalidOperationException("لقطة الوحدة لا تطابق سطر المشتريات المحفوظ.");
                    rows[i].Quantity=saved.Quantity;rows[i].Unit=saved.Name;
                }
            }
            return rows.AsReadOnly();
        }

        public IReadOnlyList<string> GetSupplierNames()
        {
            var rows = new List<string>();
            using (SqlConnection connection = connectionFactory())
            using (SqlCommand command = new SqlCommand(@"SELECT DISTINCT CompName FROM dbo.CompanyTb1
WHERE NULLIF(LTRIM(RTRIM(CompName)),N'') IS NOT NULL ORDER BY CompName;", connection))
            {
                connection.Open();
                using (SqlDataReader reader = command.ExecuteReader())
                    while (reader.Read()) rows.Add(Convert.ToString(reader[0]));
            }
            return rows.AsReadOnly();
        }

        public IReadOnlyList<string> GetMedicineNames(string supplierName)
        {
            var rows = new List<string>();
            using (SqlConnection connection = connectionFactory())
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandText = string.IsNullOrWhiteSpace(supplierName)
                    ? "SELECT MedName FROM dbo.MedicineTb1 ORDER BY MedName;"
                    : "SELECT MedName FROM dbo.MedicineTb1 WHERE CompName=@SupplierName ORDER BY MedName;";
                if (!string.IsNullOrWhiteSpace(supplierName))
                    command.Parameters.Add("@SupplierName", SqlDbType.NVarChar, 200).Value = supplierName;
                connection.Open();
                using (SqlDataReader reader = command.ExecuteReader())
                    while (reader.Read()) rows.Add(Convert.ToString(reader[0]));
            }
            return rows.AsReadOnly();
        }

        public IReadOnlyList<PurchaseEmployeeChoice> GetEmployees()
        {
            var rows = new List<PurchaseEmployeeChoice>();
            using (SqlConnection connection = connectionFactory())
            using (SqlCommand command = new SqlCommand("SELECT EmpId, EmpName FROM dbo.EmployeeTb1 ORDER BY EmpName;", connection))
            {
                connection.Open();
                using (SqlDataReader reader = command.ExecuteReader())
                    while (reader.Read()) rows.Add(new PurchaseEmployeeChoice { EmployeeId = Text(reader, 0), EmployeeName = Text(reader, 1) });
            }
            return rows.AsReadOnly();
        }

        public PurchaseMedicineSnapshot FindMedicine(string medicineName)
        {
            using (SqlConnection connection = connectionFactory())
            using (SqlCommand command = new SqlCommand(@"SELECT MedName, MedQty, Bprice, CompName
FROM dbo.MedicineTb1 WHERE MedName=@MedName;", connection))
            {
                command.Parameters.Add("@MedName", SqlDbType.NVarChar, 200).Value = medicineName;
                connection.Open();
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (!reader.Read()) return null;
                    return new PurchaseMedicineSnapshot
                    {
                        MedicineName = Text(reader, 0), StockQuantity = Convert.ToInt32(reader[1]),
                        BuyingPrice = Convert.ToInt32(reader[2]), SupplierName = Text(reader, 3)
                    };
                }
            }
        }

        private static string Text(IDataRecord reader, int index) => reader.IsDBNull(index) ? string.Empty : Convert.ToString(reader[index]);
        private static DateTime? NullableDate(IDataRecord reader, int index) => reader.IsDBNull(index) ? (DateTime?)null : Convert.ToDateTime(reader[index]);
    }
}

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using PharmacyManagement.Application;

namespace PharmacyManagement.Infrastructure.SqlServer
{
    public sealed class SqlSalesHistoryRepository : ISalesHistoryRepository
    {
        private readonly Func<SqlConnection> connectionFactory;

        public SqlSalesHistoryRepository(Func<SqlConnection> connectionFactory)
        {
            this.connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        }

        public SalesHistoryPage Load(bool showLegacyInvoices)
        {
            using (SqlConnection connection = connectionFactory())
            {
                connection.Open();
                bool commercial = IsFullCommercialMode(connection);
                bool hasLegacy = !commercial && HasLegacyArchive(connection);
                if (commercial) return LoadCommercial(connection);
                if (showLegacyInvoices) return LoadLegacy(connection, hasLegacy);
                return LoadCustomers(connection, hasLegacy);
            }
        }

        public IReadOnlyList<SalesHistoryLine> LoadCustomerLines(string customerId)
        {
            return LoadLines(@"SELECT MedName, Sprice, Quantity, TotalAmount, CONVERT(nvarchar(100),BillId), {SnapshotRequired} FROM dbo.BillsTb1
WHERE CustomerId = @Value ORDER BY BillId;", "@Value", SqlDbType.NVarChar, 50, customerId, "Sale");
        }

        public IReadOnlyList<SalesHistoryLine> LoadLegacyInvoiceLines(string invoiceReference)
        {
            return LoadLines(@"SELECT b.MedName, b.Sprice, b.Quantity, b.TotalAmount, CONVERT(nvarchar(100),b.BillId), {SnapshotRequired}
FROM dbo.LegacySalesInvoiceLinesTb l INNER JOIN dbo.BillsTb1 b ON b.BillId = l.BillId
WHERE l.InvoiceReference = @Value ORDER BY l.LineNumber;", "@Value", SqlDbType.NVarChar, 50, invoiceReference, "Sale");
        }

        public IReadOnlyList<SalesHistoryLine> LoadCommercialInvoiceLines(Guid invoiceId)
        {
            return LoadLines(@"SELECT MedName, UnitPrice AS Sprice, Quantity, LineTotal AS TotalAmount, CONVERT(nvarchar(100),LineId), {SnapshotRequired}
FROM dbo.SalesInvoiceLinesTb WHERE InvoiceId = @Value ORDER BY LineNumber;",
                "@Value", SqlDbType.UniqueIdentifier, 0, invoiceId, "CommercialSale");
        }

        private SalesHistoryPage LoadCommercial(SqlConnection connection)
        {
            const string sql = @"SELECT InvoiceId, InvoiceNumber, CustomerNameSnapshot, CustomerPhoneSnapshot,
NetTotal, IssuedAtUtc, IssuedBy, Status, Notes FROM dbo.SalesInvoicesTb
WHERE InvoiceType=N'Sale' AND Status=N'Issued' ORDER BY IssuedAtUtc DESC;";
            var rows = new List<SalesHistoryRecord>();
            using (SqlCommand command = new SqlCommand(sql, connection))
            using (SqlDataReader reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    rows.Add(new SalesHistoryRecord
                    {
                        InvoiceId = reader.GetGuid(0), InvoiceNumber = reader.GetInt64(1),
                        CustomerName = Text(reader, 2), PhoneNumber = Text(reader, 3),
                        TotalAmount = reader.GetDecimal(4), IssuedAt = reader.GetDateTime(5),
                        CashierName = Text(reader, 6), Status = Text(reader, 7), Notes = Text(reader, 8)
                    });
                }
            }
            return Page(SalesHistoryMode.CommercialInvoice, false, rows);
        }

        private SalesHistoryPage LoadLegacy(SqlConnection connection, bool hasLegacy)
        {
            var rows = new List<SalesHistoryRecord>();
            if (hasLegacy)
            {
                const string sql = @"SELECT h.InvoiceReference, h.CustomerNameSnapshot, h.CustomerPhoneSnapshot,
h.TotalAmount, h.InvoiceDate, h.EmployeeName,
CASE WHEN EXISTS (SELECT 1 FROM dbo.JournalEntriesTb c WHERE c.SourceType=N'SalesInvoiceCancellation' AND c.SourceId=h.InvoiceReference)
THEN N'ملغاة' ELSE N'معتمدة' END AS Status, h.InvoiceNotes
FROM dbo.LegacySalesInvoiceHeadersTb h ORDER BY h.InvoiceDate DESC, h.InvoiceReference DESC;";
                using (SqlCommand command = new SqlCommand(sql, connection))
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        rows.Add(new SalesHistoryRecord
                        {
                            InvoiceReference = Text(reader, 0), CustomerName = Text(reader, 1), PhoneNumber = Text(reader, 2),
                            TotalAmount = reader.GetDecimal(3), IssuedAt = reader.GetDateTime(4), CashierName = Text(reader, 5),
                            Status = Text(reader, 6), Notes = Text(reader, 7)
                        });
                    }
                }
            }
            return Page(SalesHistoryMode.LegacyInvoice, hasLegacy, rows);
        }

        private SalesHistoryPage LoadCustomers(SqlConnection connection, bool hasLegacy)
        {
            const string sql = @"SELECT CustomerId, CustomerName, PhoneNumber, TotalSpent, InvoiceDate,
EmployeeName, CustomerAddress, InvoiceNotes FROM dbo.CustomersBillsTb ORDER BY InvoiceDate DESC;";
            var rows = new List<SalesHistoryRecord>();
            using (SqlCommand command = new SqlCommand(sql, connection))
            using (SqlDataReader reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    rows.Add(new SalesHistoryRecord
                    {
                        CustomerId = Text(reader, 0), CustomerName = Text(reader, 1), PhoneNumber = Text(reader, 2),
                        TotalAmount = reader.IsDBNull(3) ? 0m : Convert.ToDecimal(reader[3]),
                        IssuedAt = reader.IsDBNull(4) ? DateTime.MinValue : reader.GetDateTime(4),
                        CashierName = Text(reader, 5), Address = Text(reader, 6), Notes = Text(reader, 7)
                    });
                }
            }
            return Page(SalesHistoryMode.CustomerSummary, hasLegacy, rows);
        }

        private IReadOnlyList<SalesHistoryLine> LoadLines(string sql, string parameterName, SqlDbType type, int size, object value, string kind)
        {
            var rows = new List<SalesHistoryLine>();
            var keys = new List<string>();
            var required=new List<bool>();
            using (SqlConnection connection = connectionFactory())
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                SqlParameter parameter = size > 0 ? command.Parameters.Add(parameterName, type, size) : command.Parameters.Add(parameterName, type);
                parameter.Value = value;
                connection.Open();
                bool hasFlag=SavedHistoryUnitReader.HasRequiredFlag(connection,kind=="Sale"?"BillsTb1":"SalesInvoiceLinesTb");
                string flag=hasFlag?(sql.Contains("b.BillId")?"b.UnitSnapshotRequired":"UnitSnapshotRequired"):"CAST(0 AS bit)";
                command.CommandText=sql.Replace("{SnapshotRequired}",flag);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        keys.Add(Text(reader,4));
                        required.Add(reader.GetBoolean(5));
                        rows.Add(new SalesHistoryLine
                        {
                            MedicineName = Text(reader, 0), UnitPrice = Convert.ToDecimal(reader[1]),
                            Quantity = Convert.ToDecimal(reader[2]), BaseQuantity = kind == "Sale" ? Convert.ToInt32(reader[2]) : 0, Unit=string.Empty, TotalAmount = Convert.ToDecimal(reader[3])
                        });
                    }
                }
                var units=SavedHistoryUnitReader.Load(connection,kind,keys);
                for(int i=0;i<rows.Count;i++)
                {
                    SavedHistoryUnit saved;
                    if(!units.TryGetValue(keys[i],out saved))
                    {
                        if(required[i])throw new InvalidOperationException("لقطة وحدة سطر المبيعات مفقودة؛ لم يعرض السطر كوحدة أساسية.");
                        if(kind == "CommercialSale")
                        {
                            if(rows[i].Quantity != decimal.Truncate(rows[i].Quantity) || rows[i].Quantity > int.MaxValue) throw new InvalidOperationException("سطر البيع التجاري يفتقد وحدة محفوظة قابلة للتحويل.");
                            rows[i].BaseQuantity=checked((int)rows[i].Quantity);
                        }
                        continue;
                    }
                    if(!string.Equals(saved.MedicineName,rows[i].MedicineName,StringComparison.OrdinalIgnoreCase) || saved.Price!=rows[i].UnitPrice ||
                        (kind=="Sale" ? saved.BaseQuantity!=rows[i].BaseQuantity : saved.Quantity!=rows[i].Quantity))
                        throw new InvalidOperationException("لقطة الوحدة لا تطابق سطر سجل المبيعات المحفوظ.");
                    rows[i].Unit=saved.Name;rows[i].Quantity=saved.Quantity;rows[i].BaseQuantity=saved.BaseQuantity;
                }
            }
            return rows.AsReadOnly();
        }

        private static bool IsFullCommercialMode(SqlConnection connection)
        {
            const string sql = @"
IF OBJECT_ID('dbo.CommercialOperationalSettingsTb', 'U') IS NULL RETURN;
IF NOT EXISTS (SELECT 1 FROM dbo.CommercialOperationalSettingsTb WHERE SettingKey=N'Mode' AND SettingValue=N'FullCommercial') RETURN;
IF OBJECT_ID('dbo.SaudiFeatureSettingsTb', 'U') IS NULL RETURN;
IF NOT EXISTS (SELECT 1 FROM dbo.SaudiFeatureSettingsTb WHERE FeatureKey=N'CommercialSales' AND FeatureMode=N'Test') RETURN;
IF NOT EXISTS (SELECT 1 FROM dbo.SaudiFeatureSettingsTb WHERE FeatureKey=N'EInvoice' AND FeatureMode IN (N'Simulator',N'Test')) RETURN;
IF OBJECT_ID('dbo.CommercialEInvoiceSettingsTb', 'U') IS NULL RETURN;
IF NOT EXISTS (SELECT 1 FROM dbo.CommercialEInvoiceSettingsTb WHERE SettingsId=1 AND LocalSandboxEnabled=1 AND LEN(LTRIM(RTRIM(SellerName)))>0 AND LEN(LTRIM(RTRIM(SellerVatNumber)))>0) RETURN;
IF EXISTS (SELECT 1 FROM dbo.MedicineTb1 m OUTER APPLY
(SELECT ISNULL(SUM(l.QuantityRemaining),0) AS LotQuantity FROM dbo.InventoryLotsTb l WHERE l.MedName=m.MedName) lots
WHERE CONVERT(DECIMAL(18,3),m.MedQty)<>lots.LotQuantity) RETURN;
SELECT CAST(1 AS INT);";
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                object value = command.ExecuteScalar();
                return value != null && value != DBNull.Value && Convert.ToInt32(value) == 1;
            }
        }

        private static bool HasLegacyArchive(SqlConnection connection)
        {
            using (SqlCommand command = new SqlCommand(@"SELECT CASE WHEN OBJECT_ID(N'dbo.LegacySalesInvoiceHeadersTb',N'U') IS NOT NULL
AND OBJECT_ID(N'dbo.LegacySalesInvoiceLinesTb',N'U') IS NOT NULL THEN 1 ELSE 0 END;", connection))
                return Convert.ToInt32(command.ExecuteScalar()) == 1;
        }

        private static SalesHistoryPage Page(SalesHistoryMode mode, bool hasLegacy, List<SalesHistoryRecord> rows)
        {
            return new SalesHistoryPage { Mode = mode, HasLegacyArchive = hasLegacy, Records = rows.AsReadOnly() };
        }

        private static string Text(IDataRecord reader, int index)
        {
            return reader.IsDBNull(index) ? string.Empty : Convert.ToString(reader[index]);
        }
    }
    internal sealed class SavedHistoryUnit
    {
        internal string MedicineName; internal string Name; internal decimal Quantity; internal decimal Price; internal int BaseQuantity;
    }
    internal static class SavedHistoryUnitReader
    {
        internal static bool HasRequiredFlag(SqlConnection c,string table)
        {
            using(var cmd=new SqlCommand("SELECT CASE WHEN COL_LENGTH(@table,N'UnitSnapshotRequired') IS NULL THEN 0 ELSE 1 END",c))
            {cmd.Parameters.Add("@table",SqlDbType.NVarChar,256).Value="dbo."+table;return Convert.ToInt32(cmd.ExecuteScalar())==1;}
        }
        internal static Dictionary<string,SavedHistoryUnit> Load(SqlConnection c,string kind,IList<string> keys)
        {
            var result=new Dictionary<string,SavedHistoryUnit>(StringComparer.OrdinalIgnoreCase);
            using(var check=new SqlCommand("SELECT CASE WHEN OBJECT_ID(N'dbo.InvoiceUnitSnapshotsTb',N'U') IS NULL THEN 0 ELSE 1 END",c))
                if(Convert.ToInt32(check.ExecuteScalar())==0)return result;
            for(int offset=0;offset<keys.Count;offset+=500)
            {
                using(var cmd=c.CreateCommand())
                {
                    var names=new List<string>();
                    for(int i=offset;i<keys.Count && i<offset+500;i++)
                    {string name="@key"+(i-offset);names.Add(name);cmd.Parameters.Add(name,SqlDbType.NVarChar,100).Value=keys[i];}
                    cmd.Parameters.Add("@kind",SqlDbType.NVarChar,30).Value=kind;
                    cmd.CommandText="SELECT LineKey,MedName,UnitName,EnteredQuantity,UnitPrice,BaseQuantity,BaseUnitsPerUnit FROM dbo.InvoiceUnitSnapshotsTb WHERE DocumentKind=@kind AND LineKey IN ("+string.Join(",",names)+");";
                    using(var r=cmd.ExecuteReader())while(r.Read())
                    {
                        var unit=new SavedHistoryUnit{MedicineName=r.GetString(1),Name=r.GetString(2),Quantity=r.GetDecimal(3),Price=r.GetDecimal(4),BaseQuantity=r.GetInt32(5)};
                        if(PharmacyManagement.Domain.MedicineUnitCalculator.ToBaseQuantity(unit.Quantity,r.GetInt32(6))!=unit.BaseQuantity || unit.Price<0m || string.IsNullOrWhiteSpace(unit.Name))
                            throw new InvalidOperationException("لقطة وحدة محفوظة غير متسقة؛ أوقف تحميل المستند.");
                        string key=r.GetString(0);
                        if(result.ContainsKey(key)) throw new InvalidOperationException("لقطة وحدة مكررة لسطر المستند.");
                        result.Add(key,unit);
                    }
                }
            }
            return result;
        }
    }

}

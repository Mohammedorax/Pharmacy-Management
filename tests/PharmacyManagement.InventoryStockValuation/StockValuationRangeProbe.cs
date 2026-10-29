using System;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using PharmacyManagement.Application;
using PharmacyManagement.Infrastructure.SqlServer;
using project_fo_3;

internal static class StockValuationRangeProbe
{
    static int assertions;
    static void Check(bool condition, string message)
    { assertions++; if (!condition) throw new InvalidOperationException(message); }
    static bool IsArithmeticOverflow(Action read)
    {
        try { read(); return false; }
        catch (SqlException error) { if (error.Number != 8115) throw; return true; }
    }
    static void CheckValues(OperationalReportResult result, DataTable legacy, int quantity, int buy, int sell)
    {
        string[] columns = { "الصنف", "الباركود", "الشركة", "الوحدة", "الكمية الحالية", "سعر التكلفة", "سعر البيع", "قيمة التكلفة", "قيمة البيع", "تاريخ الإنتاج", "تاريخ الانتهاء", "الحالة" };
        Check(result.Rows.Count == 1 && legacy.Rows.Count == 1, "One actual saved fixture required");
        Check(result.Columns.Select(c => c.Name).SequenceEqual(columns), "Twelve typed columns/order changed");
        Check(legacy.Columns.Cast<DataColumn>().Select(c => c.ColumnName).SequenceEqual(columns), "Twelve legacy columns/order changed");
        Check(result.Columns[7].DataType == typeof(decimal) && result.Columns[8].DataType == typeof(decimal), "Typed valuation type changed");
        Check(legacy.Columns[7].DataType == typeof(decimal) && legacy.Columns[8].DataType == typeof(decimal), "Legacy valuation type changed");
        decimal expectedCost = (decimal)quantity * buy, expectedSale = (decimal)quantity * sell;
        Check((decimal)result.Rows[0][7] == expectedCost && (decimal)result.Rows[0][8] == expectedSale, "Typed valuation differs from independent decimal arithmetic");
        Check((decimal)legacy.Rows[0][7] == expectedCost && (decimal)legacy.Rows[0][8] == expectedSale, "Legacy valuation differs from independent decimal arithmetic");
        for (int index = 0; index < columns.Length; index++)
            Check(Equals(result.Rows[0][index] ?? DBNull.Value, legacy.Rows[0][index]), "Legacy/typed column parity changed: " + columns[index]);
    }
    static int Main(string[] args)
    {
        try
        {
            Check(args.Length == 5 && args[4] == "--confirm-owned-temporary-qa-writes", "Explicit owned QA authorization required");
            string database = args[0], token = args[1], output = args[2], mode = args[3];
            Guid owner;
            Check(Guid.TryParseExact(token, "N", out owner) && Regex.IsMatch(database, "^PharmacyRestore_[0-9]{14}_" + token + "_qa$"), "Strict owned clone identity required");
            Check(mode == "--expect-overflow" || mode == "--expect-valuations", "Explicit baseline or fixed expectation required");
            string connection = "Data Source=.;Initial Catalog=" + database + ";Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Pooling=False";
            Check(new SqlConnectionStringBuilder(Database.ConnectionString).InitialCatalog == database, "Runtime not pinned to owned clone");
            string company;
            using (var c = new SqlConnection(connection))
            {
                c.Open();
                using (var q = new SqlCommand("SELECT CONVERT(nvarchar(32),value) FROM sys.extended_properties WHERE class=0 AND name=N'QaTest.Owner'", c))
                    Check(Convert.ToString(q.ExecuteScalar()) == token, "Owned clone marker required before any fixture write");
                using (var q = new SqlCommand("SELECT SettingValue FROM dbo.CommercialOperationalSettingsTb WHERE SettingKey=N'Mode'", c))
                    Check(Convert.ToString(q.ExecuteScalar()) == "Disabled", "Basic aggregate inventory mode required");
                using (var q = new SqlCommand("SELECT TOP(1) CompName FROM dbo.CompanyTb1 ORDER BY CompName", c))
                    company = Convert.ToString(q.ExecuteScalar());
            }
            Check(!string.IsNullOrWhiteSpace(company), "Existing company fixture required");
            var writer = new SqlMedicineRepository(() => new SqlConnection(connection));
            var reader = new InventoryStockReadService(new SqlInventoryStockReadRepository(() => new SqlConnection(connection)), actor => true);
            var principal = new OperationActor { UserName = "admin", UserRole = "Manager" };
            int[][] values = mode == "--expect-overflow"
                ? new[] { new[] { 50000, 50000, 60000 } }
                : new[] { new[] { 1, 50, 100 }, new[] { 46340, 46340, 46340 }, new[] { 46341, 46341, 46341 }, new[] { 50000, 50000, 60000 }, new[] { 50000, int.MaxValue, int.MaxValue }, new[] { 0, int.MaxValue, int.MaxValue } };
            string prefix = "QA-STOCK-RANGE-" + token.Substring(0, 12) + "-";
            for (int index = 0; index < values.Length; index++)
            {
                int quantity = values[index][0], buy = values[index][1], sell = values[index][2];
                string name = prefix + index.ToString("D2");
                writer.Create(new MedicineRecord { MedName = name, Barcode = "00" + token.Substring(0, 12) + index.ToString("D2"), Quantity = quantity, BuyingPrice = buy, SellingPrice = sell, CompanyName = company, ProductionDate = DateTime.Today.AddMonths(-1), ExpiryDate = DateTime.Today.AddYears(1) }, principal);
                var request = new InventoryStockReadRequest(InventoryStockReadMode.Basic, name, true);
                if (mode == "--expect-overflow")
                {
                    bool typed = IsArithmeticOverflow(() => reader.LoadAsync(request, principal, CancellationToken.None).GetAwaiter().GetResult());
                    bool legacy = IsArithmeticOverflow(() => InventoryService.GetStockSnapshotQaReadOnly(".", database, name, true));
                    Check(typed && legacy, "Actual typed and legacy repositories did not reproduce SQL8115");
                    Console.WriteLine("PASS baseline actual stock valuation overflow: typed=8115 legacy=8115 quantity=50000 buy=50000 sell=60000; expected decimal cost=2500000000 sale=3000000000");
                }
                else
                {
                    var result = reader.LoadAsync(request, principal, CancellationToken.None).GetAwaiter().GetResult();
                    using (var legacy = InventoryService.GetStockSnapshotQaReadOnly(".", database, name, true)) CheckValues(result, legacy, quantity, buy, sell);
                    var nonzero = reader.LoadAsync(new InventoryStockReadRequest(InventoryStockReadMode.Basic, name, false), principal, CancellationToken.None).GetAwaiter().GetResult();
                    Check(nonzero.Rows.Count == (quantity == 0 ? 0 : 1), "Include-zero filter changed");
                    Console.WriteLine("PASS actual stock valuation range: case=" + index + " quantity=" + quantity + " cost=" + ((decimal)quantity * buy) + " sale=" + ((decimal)quantity * sell));
                }
            }
            string summary = "PASS STOCK_VALUATION mode=" + mode + " cases=" + values.Length + " assertions=" + assertions + "; actual owned SQL repositories; source database untouched; no UI/physical printing.";
            File.WriteAllText(Path.Combine(output, "result.txt"), summary);
            Console.WriteLine(summary);
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}

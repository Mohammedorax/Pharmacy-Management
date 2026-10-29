using System;
using System.Data;
using System.Threading;
using PharmacyManagement.Application;
using PharmacyManagement.Infrastructure.SqlServer;

namespace project_fo_3
{
    internal static class InventoryStockReadQaVerification
    {
        internal static void RunReadOnly(string serverName, string databaseName)
        {
            if (string.IsNullOrWhiteSpace(databaseName) || !databaseName.Trim().EndsWith("_qa", StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("مقارنة قراءة الجرد مسموحة فقط لقاعدة QA.");
            string connectionString = Database.BuildSettingsConnectionString(serverName, databaseName, true);
            var service = new InventoryStockReadService(
                new SqlInventoryStockReadRepository(() => new System.Data.SqlClient.SqlConnection(connectionString)), principal => true);
            var actor = new OperationActor { UserName = "qa-readonly-verifier", UserRole = "Administrator" };
            CompareMode(service, actor, serverName, databaseName, InventoryStockReadMode.Basic);
            CompareMode(service, actor, serverName, databaseName, InventoryStockReadMode.CommercialLots);
        }

        private static void CompareMode(IInventoryStockReadService service, OperationActor actor, string server, string database,
            InventoryStockReadMode mode)
        {
            DataTable baseline = LegacyRead(server, database, mode, string.Empty, true);
            Compare(service, actor, mode, string.Empty, true, baseline);
            DataTable nonzeroBaseline = LegacyRead(server, database, mode, string.Empty, false);
            Compare(service, actor, mode, string.Empty, false, nonzeroBaseline);
            if (mode == InventoryStockReadMode.Basic && baseline.Rows.Count > 0)
            {
                DataRow sample = baseline.Rows[0];
                string name = Convert.ToString(sample["الصنف"]);
                string arabicSearch = name.Substring(0, Math.Min(3, name.Length));
                Compare(service, actor, mode, arabicSearch, true, LegacyRead(server, database, mode, arabicSearch, true));
                string barcode = Convert.ToString(sample["الباركود"]);
                if (!string.IsNullOrWhiteSpace(barcode) && barcode != "-")
                    Compare(service, actor, mode, barcode, true, LegacyRead(server, database, mode, barcode, true));
                string company = Convert.ToString(sample["الشركة"]);
                if (!string.IsNullOrWhiteSpace(company))
                    Compare(service, actor, mode, company, true, LegacyRead(server, database, mode, company, true));
            }
            Console.WriteLine("PASS QA stock parity; Mode={0}; Rows={1}; IncludeZeroRows={2}; PersistentWrites=0.", mode, baseline.Rows.Count, nonzeroBaseline.Rows.Count);
        }

        private static DataTable LegacyRead(string server, string database, InventoryStockReadMode mode, string search, bool includeZero)
        {
            return mode == InventoryStockReadMode.Basic
                ? InventoryService.GetStockSnapshotQaReadOnly(server, database, search, includeZero)
                : InventoryService.GetLotStockSnapshotQaReadOnly(server, database, search, includeZero);
        }

        private static void Compare(IInventoryStockReadService service, OperationActor actor, InventoryStockReadMode mode,
            string search, bool includeZero, DataTable baseline)
        {
            var request = new InventoryStockReadRequest(mode, search, includeZero);
            OperationalReportResult actual = service.LoadAsync(request, actor, CancellationToken.None).GetAwaiter().GetResult();
            if (actual.Columns.Count != baseline.Columns.Count || actual.Rows.Count != baseline.Rows.Count)
                throw new InvalidOperationException("مقارنة QA للجرد اختلفت في عدد الأعمدة أو الصفوف: " + mode + ", البحث=" + search);
            for (int column = 0; column < baseline.Columns.Count; column++)
            {
                DataColumn expected = baseline.Columns[column];
                OperationalReportColumn observed = actual.Columns[column];
                if (!string.Equals(expected.ColumnName, observed.Name, StringComparison.Ordinal) || expected.DataType != observed.DataType)
                    throw new InvalidOperationException("مقارنة QA للجرد اختلفت في اسم/نوع العمود " + column + ": " + mode);
            }
            for (int row = 0; row < baseline.Rows.Count; row++)
            for (int column = 0; column < baseline.Columns.Count; column++)
            {
                object expected = baseline.Rows[row][column];
                object observed = actual.Rows[row][column];
                if (expected == DBNull.Value) expected = null;
                if (observed == DBNull.Value) observed = null;
                if (!Equals(expected, observed))
                    throw new InvalidOperationException("مقارنة QA للجرد اختلفت في القيمة عند الصف " + row + " والعمود " + baseline.Columns[column].ColumnName);
            }
        }
    }
}
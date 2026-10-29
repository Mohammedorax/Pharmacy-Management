using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using PharmacyManagement.Application;

namespace project_fo_3
{
    internal sealed class HomeOperationalAlertReadSession
    {
        private readonly InventoryAlertReadSession inventory;
        internal HomeOperationalAlertReadSession(InventoryAlertReadSession inventory)
        { this.inventory = inventory ?? throw new ArgumentNullException(nameof(inventory)); }

        internal void EnsureContext() { inventory.EnsureContext(); }

        internal async Task<DataTable> LoadAsync(CancellationToken cancellation)
        {
            var result = await inventory.LoadAsync(10, 30, cancellation).ConfigureAwait(false);
            cancellation.ThrowIfCancellationRequested();
            EnsureContext();
            var table = HomeOperationalAlertPresentation.ToTable(result);
            try
            {
                cancellation.ThrowIfCancellationRequested();
                EnsureContext();
                return table;
            }
            catch { table.Dispose(); throw; }
        }
    }

    internal static class HomeOperationalAlertPresentation
    {
        internal static DataTable ToTable(OperationalReportResult result)
        {
            using (DataTable table = InventoryStockPresentation.ToTable(result))
                return FromInventoryTable(table);
        }

        internal static DataTable FromInventoryTable(DataTable source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            // Preserve the source owned by its report/session; only this copy is renamed.
            using (DataTable table = source.Copy())
            {
                string[] sourceNames = { "نوع التنبيه", "الصنف", "الكمية", "تاريخ الانتهاء", "الشركة", "التفاصيل" };
                string[] targetNames = { "Type", "Medicine", "Quantity", "Expiry", "Company", "Message" };
                for (int index = 0; index < sourceNames.Length; index++)
                {
                    if (!table.Columns.Contains(sourceNames[index]))
                        throw new InvalidOperationException("Missing inventory alert column: " + sourceNames[index]);
                    table.Columns[sourceNames[index]].ColumnName = targetNames[index];
                }
                foreach (DataRow row in table.Rows)
                {
                    string type = Convert.ToString(row["Type"]);
                    if (type == "منتهي") row["Type"] = "Expired";
                    else if (type == "قريب الانتهاء") row["Type"] = "Expiring Soon";
                    else if (type == "مخزون منخفض") row["Type"] = "Low Stock";
                }
                table.DefaultView.Sort = "Type, Expiry, Medicine";
                return table.DefaultView.ToTable();
            }
        }
    }
}

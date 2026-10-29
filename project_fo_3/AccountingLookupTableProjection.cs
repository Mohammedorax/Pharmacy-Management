using System;
using System.Collections.Generic;
using System.Data;
using PharmacyManagement.Application;

namespace project_fo_3
{
    internal static class AccountingLookupTableProjection
    {
        internal static DataTable Accounts(IReadOnlyList<AccountLookupRow> rows)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));
            var table = new DataTable();
            table.Columns.Add("AccountCode", typeof(string)); table.Columns.Add("AccountName", typeof(string));
            table.Columns.Add("AccountType", typeof(string)); table.Columns.Add("ParentAccountCode", typeof(string));
            table.Columns.Add("IsActive", typeof(bool));
            foreach (var row in rows) table.Rows.Add(row.Code, row.Name, row.Type, (object)row.ParentCode ?? DBNull.Value, row.Active);
            return table;
        }
        internal static DataTable CostCenters(IReadOnlyList<CostCenterLookupRow> rows)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));
            var table = new DataTable(); table.Columns.Add("CostCenterId", typeof(int)); table.Columns.Add("CostCenterName", typeof(string));
            foreach (var row in rows) table.Rows.Add(row.Id, row.Name);
            return table;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Data;
using PharmacyManagement.Application;

namespace project_fo_3
{
    internal static class JournalHistoryTableProjection
    {
        internal static DataTable Create(IEnumerable<JournalHistoryRow> rows)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));
            var table = new DataTable();
            table.Columns.Add("رقم القيد", typeof(int)); table.Columns.Add("التاريخ", typeof(DateTime));
            table.Columns.Add("المصدر", typeof(string)); table.Columns.Add("رقم المصدر", typeof(string));
            table.Columns.Add("البيان", typeof(string)); table.Columns.Add("مدين", typeof(decimal));
            table.Columns.Add("دائن", typeof(decimal)); table.Columns.Add("المستخدم", typeof(string));
            foreach (var row in rows)
                table.Rows.Add(row.Id, row.Date, (object)row.SourceType ?? DBNull.Value, (object)row.SourceId ?? DBNull.Value,
                    (object)row.Description ?? DBNull.Value, (object)row.Debit ?? DBNull.Value,
                    (object)row.Credit ?? DBNull.Value, (object)row.CreatedBy ?? DBNull.Value);
            return table;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using PharmacyManagement.Application;

namespace project_fo_3
{
    // Excel columns are stable English identifiers; the screen owns localized labels.
    internal static class CashBoxWorkbook
    {
        internal static DataTable CreateExport(IReadOnlyList<CashBoxRecord> boxes)
        {
            if (boxes == null) throw new ArgumentNullException(nameof(boxes));
            var table = new DataTable();
            foreach (string column in new[] { "CashBoxId", "Code", "Name", "IsActive", "EmployeeId" })
                table.Columns.Add(column);
            // Exported rows are also the user's re-import template. Preserve cashier
            // identifiers such as 0007 when the workbook is edited in Excel.
            table.ExtendedProperties[ExcelWorkbookWriter.TextColumnsProperty] = new[] { 0, 1, 2, 3, 4 };
            foreach (CashBoxRecord box in boxes)
                table.Rows.Add(box.CashBoxId.ToString("D"), box.Code, box.Name,
                    box.IsActive ? "TRUE" : "FALSE", box.EmployeeId ?? string.Empty);
            return table;
        }

        internal static IReadOnlyList<CashBoxInput> Parse(DataTable sheet, IReadOnlyList<CashBoxRecord> saved)
        {
            if (sheet == null) throw new ArgumentNullException(nameof(sheet));
            foreach (string header in new[] { "Code", "Name", "IsActive", "EmployeeId" })
                if (!sheet.Columns.Contains(header))
                    throw new InvalidOperationException("قالب الصناديق يحتاج الأعمدة: Code, Name, IsActive, EmployeeId.");
            if (sheet.Rows.Count == 0 || sheet.Rows.Count > 500)
                throw new InvalidOperationException("يقبل استيراد الصناديق من 1 إلى 500 صف.");
            var existing = (saved ?? new CashBoxRecord[0]).ToDictionary(item => item.Code, StringComparer.OrdinalIgnoreCase);
            var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var employees = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var items = new List<CashBoxInput>();
            foreach (DataRow row in sheet.Rows)
            {
                string code = Convert.ToString(row["Code"]).Trim().ToUpperInvariant();
                string name = Convert.ToString(row["Name"]).Trim();
                string employee = Convert.ToString(row["EmployeeId"]).Trim();
                if (code.Length == 0 || code.Length > 30 ||
                    code.Any(c => !((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '-' || c == '_')))
                    throw new InvalidOperationException("رمز صندوق Excel غير صالح.");
                if (name.Length == 0 || name.Length > 100 || name.Any(char.IsControl))
                    throw new InvalidOperationException("اسم صندوق Excel غير صالح.");
                if (employee.Length > 50) throw new InvalidOperationException("رقم الكاشير في Excel طويل جدًا.");
                if (!codes.Add(code) || (employee.Length > 0 && !employees.Add(employee)))
                    throw new InvalidOperationException("يتكرر رمز صندوق أو رقم كاشير في ملف Excel.");
                string active = Convert.ToString(row["IsActive"]).Trim();
                bool enabled;
                if (active == "1" || active.Equals("TRUE", StringComparison.OrdinalIgnoreCase) || active == "نعم") enabled = true;
                else if (active == "0" || active.Equals("FALSE", StringComparison.OrdinalIgnoreCase) || active == "لا") enabled = false;
                else throw new InvalidOperationException("حالة الصندوق يجب أن تكون TRUE أو FALSE.");
                if (!enabled && employee.Length > 0)
                    throw new InvalidOperationException("لا يمكن إسناد كاشير إلى صندوق غير نشط.");
                Guid id = Guid.Empty;
                if (sheet.Columns.Contains("CashBoxId"))
                {
                    string value = Convert.ToString(row["CashBoxId"]).Trim();
                    if (value.Length > 0 && !Guid.TryParse(value, out id))
                        throw new InvalidOperationException("معرف الصندوق في Excel غير صالح.");
                }
                if (id == Guid.Empty && existing.TryGetValue(code, out CashBoxRecord match))
                    id = match.CashBoxId;
                items.Add(new CashBoxInput
                {
                    CashBoxId = id, Code = code, Name = name, IsActive = enabled, EmployeeId = employee
                });
            }
            return items;
        }
    }
}

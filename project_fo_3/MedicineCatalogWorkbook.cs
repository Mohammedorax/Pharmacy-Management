using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using PharmacyManagement.Application;

namespace project_fo_3
{
    internal enum MedicineCatalogValidationCode
    {
        RowCount, DuplicateColumn, MissingColumn, RequiredFields, PositivePrices,
        InvalidDates, ProductionDateBefore1900, NonzeroQuantity, DuplicateMedicine, NoRows
    }

    internal sealed class MedicineCatalogValidationIssue
    {
        internal MedicineCatalogValidationCode Code { get; set; }
        internal int RowNumber { get; set; }
        internal string Detail { get; set; }

        internal string Format(bool english)
        {
            string message = FormatMessage(english);
            return RowNumber > 0 ? (english ? "Row " : "السطر ") +
                RowNumber.ToString(CultureInfo.InvariantCulture) + ": " + message : message;
        }

        internal string FormatMessage(bool english)
        {
            string message;
            switch (Code)
            {
                case MedicineCatalogValidationCode.RowCount:
                    message = english ? "The file must contain 1 to 2,000 medicine rows." : "يجب أن يحتوي الملف من 1 إلى 2,000 صف صنف."; break;
                case MedicineCatalogValidationCode.DuplicateColumn:
                    message = (english ? "Duplicate column: " : "يوجد عمود مكرر: ") + Detail; break;
                case MedicineCatalogValidationCode.MissingColumn:
                    message = (english ? "Missing required column: " : "العمود المطلوب مفقود: ") + ColumnName(Detail, english); break;
                case MedicineCatalogValidationCode.RequiredFields:
                    message = english ? "Medicine name, barcode and company are required within the allowed lengths." : "اسم الصنف والباركود والشركة مطلوبة ضمن حدود الطول المسموحة."; break;
                case MedicineCatalogValidationCode.PositivePrices:
                    message = english ? "Purchase and sale prices must be positive whole numbers." : "سعر الشراء والبيع يجب أن يكونا رقمين صحيحين موجبين."; break;
                case MedicineCatalogValidationCode.InvalidDates:
                    message = english ? "Enter valid production and expiry dates in yyyy-MM-dd format." : "استخدم تاريخ إنتاج وانتهاء صحيحًا بصيغة yyyy-MM-dd."; break;
                case MedicineCatalogValidationCode.ProductionDateBefore1900:
                    message = english ? "Production date must be in 1900 or later." : "تاريخ الإنتاج يجب أن يكون من سنة 1900 أو بعدها."; break;
                case MedicineCatalogValidationCode.NonzeroQuantity:
                    message = english ? "Stock is not imported; enter quantity through a documented purchase or opening balance." : "لا يُستورد رصيد؛ أدخل الكمية عبر شراء أو رصيد افتتاحي موثق."; break;
                case MedicineCatalogValidationCode.DuplicateMedicine:
                    message = english ? "Medicine name or barcode is duplicated in the file." : "اسم الصنف أو الباركود مكرر داخل الملف."; break;
                case MedicineCatalogValidationCode.NoRows:
                    message = english ? "No medicine rows can be imported." : "لا توجد صفوف أصناف قابلة للاستيراد."; break;
                default: throw new InvalidOperationException("Unknown medicine catalog validation code.");
            }
            return message;
        }

        private static string ColumnName(string key, bool english)
        {
            switch (key)
            {
                case "name": return english ? "Medicine name" : "اسم الدواء";
                case "barcode": return english ? "Barcode" : "الباركود";
                case "buy": return english ? "Purchase price" : "سعر الشراء";
                case "sell": return english ? "Sale price" : "سعر البيع";
                case "production": return english ? "Production date" : "تاريخ الإنتاج";
                case "expiry": return english ? "Expiry date" : "تاريخ الانتهاء";
                case "company": return english ? "Manufacturer" : "الشركة المصنعة";
                default: throw new InvalidOperationException("Unknown medicine catalog column key.");
            }
        }
    }
    internal sealed class MedicineCatalogWorkbookPreview
    {
        internal DataTable Table { get; set; }
        internal IReadOnlyList<MedicineRecord> Medicines { get; set; }
        internal IReadOnlyList<string> Errors { get; set; }
        internal IReadOnlyList<MedicineCatalogValidationIssue> Issues { get; set; }
        internal bool IsValid { get { return Errors.Count == 0 && Medicines.Count > 0; } }
    }

    internal static class MedicineCatalogWorkbook
    {
        internal static DataTable CreateTemplate()
        {
            var table = new DataTable("الأصناف");
            foreach (string title in new[] { "اسم الدواء", "الباركود", "سعر الشراء", "سعر البيع",
                "تاريخ الإنتاج", "تاريخ الانتهاء", "الشركة المصنعة" }) table.Columns.Add(title);
            // Protect future barcode/date entries when the template is edited in Excel.
            // Prices retain General formatting so users can enter numeric values normally.
            table.ExtendedProperties[ExcelWorkbookWriter.TextColumnsProperty] = new[] { 0, 1, 4, 5, 6 };
            return table;
        }

        internal static MedicineCatalogWorkbookPreview Read(string path)
        { return Parse(ExcelWorkbookReader.Read(path)); }

        internal static MedicineCatalogWorkbookPreview Parse(DataTable table)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            var issues = new List<MedicineCatalogValidationIssue>();
            var medicines = new List<MedicineRecord>();
            if (table.Rows.Count == 0 || table.Rows.Count > 2000)
                issues.Add(new MedicineCatalogValidationIssue { Code = MedicineCatalogValidationCode.RowCount });
            var columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < table.Columns.Count; index++)
            {
                string key = HeaderKey(table.Columns[index].ColumnName);
                if (key.Length == 0) continue;
                if (columns.ContainsKey(key)) issues.Add(new MedicineCatalogValidationIssue { Code = MedicineCatalogValidationCode.DuplicateColumn, Detail = table.Columns[index].ColumnName });
                else columns.Add(key, index);
            }
            foreach (string key in new[] { "name", "barcode", "buy", "sell", "production", "expiry", "company" })
                if (!columns.ContainsKey(key)) issues.Add(new MedicineCatalogValidationIssue { Code = MedicineCatalogValidationCode.MissingColumn, Detail = key });
            if (issues.Count > 0) return Result(table, medicines, issues);

            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var barcodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < table.Rows.Count; index++)
            {
                DataRow row = table.Rows[index];
                if (row.ItemArray.All(value => string.IsNullOrWhiteSpace(Convert.ToString(value)))) continue;
                int rowNumber = index + 2;
                string name = Cell(row, columns, "name");
                string barcode = Cell(row, columns, "barcode");
                string company = Cell(row, columns, "company");
                int buy, sell;
                DateTime production, expiry;
                if (name.Length == 0 || name.Length > 200 || barcode.Length == 0 || barcode.Length > 100 ||
                    company.Length == 0 || company.Length > 200)
                { issues.Add(new MedicineCatalogValidationIssue { Code = MedicineCatalogValidationCode.RequiredFields, RowNumber = rowNumber }); continue; }
                if (!int.TryParse(Cell(row, columns, "buy"), NumberStyles.Integer, CultureInfo.InvariantCulture, out buy) ||
                    !int.TryParse(Cell(row, columns, "sell"), NumberStyles.Integer, CultureInfo.InvariantCulture, out sell) ||
                    buy <= 0 || sell <= 0)
                { issues.Add(new MedicineCatalogValidationIssue { Code = MedicineCatalogValidationCode.PositivePrices, RowNumber = rowNumber }); continue; }
                if (!TryDate(Cell(row, columns, "production"), out production) ||
                    !TryDate(Cell(row, columns, "expiry"), out expiry) ||
                    expiry <= production)
                { issues.Add(new MedicineCatalogValidationIssue { Code = MedicineCatalogValidationCode.InvalidDates, RowNumber = rowNumber }); continue; }
                if (production.Year < 1900)
                { issues.Add(new MedicineCatalogValidationIssue { Code = MedicineCatalogValidationCode.ProductionDateBefore1900, RowNumber = rowNumber }); continue; }
                if (columns.ContainsKey("quantity") && Cell(row, columns, "quantity") != string.Empty &&
                    Cell(row, columns, "quantity") != "0")
                { issues.Add(new MedicineCatalogValidationIssue { Code = MedicineCatalogValidationCode.NonzeroQuantity, RowNumber = rowNumber }); continue; }
                if (!names.Add(name) || !barcodes.Add(barcode))
                { issues.Add(new MedicineCatalogValidationIssue { Code = MedicineCatalogValidationCode.DuplicateMedicine, RowNumber = rowNumber }); continue; }
                medicines.Add(new MedicineRecord { MedName = name, Barcode = barcode,
                    BuyingPrice = buy, SellingPrice = sell, Quantity = 0,
                    ProductionDate = production, ExpiryDate = expiry, CompanyName = company });
            }
            if (medicines.Count == 0 && issues.Count == 0) issues.Add(new MedicineCatalogValidationIssue { Code = MedicineCatalogValidationCode.NoRows });
            return Result(table, medicines, issues);
        }

        private static MedicineCatalogWorkbookPreview Result(DataTable table, IReadOnlyList<MedicineRecord> medicines,
            IReadOnlyList<MedicineCatalogValidationIssue> issues)
        { return new MedicineCatalogWorkbookPreview { Table = table, Medicines = medicines, Issues = issues, Errors = issues.Select(issue => issue.Format(false)).ToArray() }; }

        private static string Cell(DataRow row, IDictionary<string, int> columns, string key)
        { return columns.ContainsKey(key) ? (Convert.ToString(row[columns[key]]) ?? string.Empty).Trim() : string.Empty; }

        private static bool TryDate(string value, out DateTime date)
        {
            if (DateTime.TryParseExact(value, new[] { "yyyy-MM-dd", "yyyy/MM/dd", "yyyy-MM-dd HH:mm:ss" },
                CultureInfo.InvariantCulture, DateTimeStyles.None, out date)) return true;
            double serial;
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out serial) &&
                serial >= 30000 && serial <= 2958465)
            { date = DateTime.FromOADate(serial).Date; return true; }
            date = default(DateTime); return false;
        }

        private static string HeaderKey(string value)
        {
            string text = (value ?? string.Empty).Trim().Replace(" ", string.Empty).ToLowerInvariant();
            switch (text)
            {
                case "اسمالدواء": case "الصنف": case "medicinename": case "medname": return "name";
                case "الباركود": case "barcode": return "barcode";
                case "سعرالشراء": case "buyprice": return "buy";
                case "سعرالبيع": case "sellprice": return "sell";
                case "تاريخالإنتاج": case "productiondate": case "proddate": return "production";
                case "تاريخالانتهاء": case "expirydate": case "expdate": return "expiry";
                case "الشركةالمصنعة": case "الشركة": case "company": case "companyname": return "company";
                case "الكمية": case "الرصيدالحالي": case "quantity": return "quantity";
                default: return string.Empty;
            }
        }

    }

    internal static class MedicineCatalogWorkbookSelfTests
    {
        internal static void Run()
        {
            DataTable table = MedicineCatalogWorkbook.CreateTemplate();
            using (var stream = new MemoryStream())
            {
                ExcelWorkbookWriter.Write(table, "الأصناف", stream);
                stream.Position = 0;
                DataTable reopened = ExcelWorkbookReader.Read(stream);
                if (reopened.Columns.Count != 7 || reopened.Rows.Count != 0)
                    throw new InvalidOperationException("ملف قالب الأصناف الفارغ لم يُفتح بأعمدته المطلوبة.");
            }
            table.Rows.Add("باراسيتامول 500 mg", "6281234567890", "10", "15",
                "2026-01-01", "2027-01-01", "شركة الدواء");
            MedicineCatalogWorkbookPreview valid = MedicineCatalogWorkbook.Parse(table);
            if (!valid.IsValid || valid.Medicines.Count != 1 || valid.Medicines[0].Quantity != 0)
                throw new InvalidOperationException("معاينة قالب الأصناف لم تنتج تعريفًا صحيحًا برصيد صفر.");
            string path = Path.Combine(Path.GetTempPath(), "pharmacy-catalog-import-" + Guid.NewGuid().ToString("N") + ".xlsx");
            try
            {
                using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    ExcelWorkbookWriter.Write(table, "الأصناف", output);
                MedicineCatalogWorkbookPreview reopened = MedicineCatalogWorkbook.Read(path);
                if (!reopened.IsValid || reopened.Medicines.Count != 1 ||
                    reopened.Medicines[0].MedName != "باراسيتامول 500 mg")
                    throw new InvalidOperationException("مسار ملف XLSX لم يحافظ على بيانات الصنف العربية.");
            }
            finally { if (File.Exists(path)) File.Delete(path); }
            table.Columns.Add("الكمية");
            table.Rows[0]["الكمية"] = "3";
            if (MedicineCatalogWorkbook.Parse(table).IsValid)
                throw new InvalidOperationException("قالب الأصناف قبل استيراد رصيدًا مباشرًا.");
            table.Rows[0]["الكمية"] = "0";
            table.Rows.Add("دواء آخر", "6281234567890", "10", "15", "2026-01-01", "2027-01-01", "شركة الدواء", "0");
            if (MedicineCatalogWorkbook.Parse(table).IsValid)
                throw new InvalidOperationException("قالب الأصناف قبل باركودًا مكررًا.");
            DataTable fixture = MedicineCatalogWorkbook.CreateTemplate();
            fixture.Rows.Add("باراسيتامول 500 mg", "6281234567890", "10", "15",
                "2026-01-01", "2027-01-01", "شركة الدواء");
            CheckIssue(MedicineCatalogWorkbook.CreateTemplate(), MedicineCatalogValidationCode.RowCount,
                "يجب أن يحتوي الملف من 1 إلى 2,000 صف صنف.", "The file must contain 1 to 2,000 medicine rows.");
            DataTable missing = fixture.Copy(); missing.Columns.Remove("الباركود");
            CheckIssue(missing, MedicineCatalogValidationCode.MissingColumn,
                "العمود المطلوب مفقود: الباركود", "Missing required column: Barcode");
            DataTable duplicateColumn = fixture.Copy(); duplicateColumn.Columns.Add("MedName");
            CheckIssue(duplicateColumn, MedicineCatalogValidationCode.DuplicateColumn,
                "يوجد عمود مكرر: MedName", "Duplicate column: MedName");
            DataTable required = fixture.Copy(); required.Rows[0]["اسم الدواء"] = string.Empty;
            CheckIssue(required, MedicineCatalogValidationCode.RequiredFields,
                "السطر 2: اسم الصنف والباركود والشركة مطلوبة ضمن حدود الطول المسموحة.",
                "Row 2: Medicine name, barcode and company are required within the allowed lengths.");
            DataTable price = fixture.Copy(); price.Rows[0]["سعر الشراء"] = "0";
            CheckIssue(price, MedicineCatalogValidationCode.PositivePrices,
                "السطر 2: سعر الشراء والبيع يجب أن يكونا رقمين صحيحين موجبين.",
                "Row 2: Purchase and sale prices must be positive whole numbers.");
            DataTable date = fixture.Copy(); date.Rows[0]["تاريخ الانتهاء"] = "2025-01-01";
            CheckIssue(date, MedicineCatalogValidationCode.InvalidDates,
                "السطر 2: استخدم تاريخ إنتاج وانتهاء صحيحًا بصيغة yyyy-MM-dd.",
                "Row 2: Enter valid production and expiry dates in yyyy-MM-dd format.");
            DataTable earlyDate = fixture.Copy(); earlyDate.Rows[0]["تاريخ الإنتاج"] = "1899-12-31";
            CheckIssue(earlyDate, MedicineCatalogValidationCode.ProductionDateBefore1900,
                "السطر 2: تاريخ الإنتاج يجب أن يكون من سنة 1900 أو بعدها.",
                "Row 2: Production date must be in 1900 or later.");
            DataTable stock = fixture.Copy(); stock.Columns.Add("الكمية"); stock.Rows[0]["الكمية"] = "3";
            CheckIssue(stock, MedicineCatalogValidationCode.NonzeroQuantity,
                "السطر 2: لا يُستورد رصيد؛ أدخل الكمية عبر شراء أو رصيد افتتاحي موثق.",
                "Row 2: Stock is not imported; enter quantity through a documented purchase or opening balance.");
            DataTable duplicateMedicine = fixture.Copy();
            duplicateMedicine.Rows.Add("دواء آخر", "6281234567890", "10", "15", "2026-01-01", "2027-01-01", "شركة الدواء");
            MedicineCatalogWorkbookPreview duplicatePreview = MedicineCatalogWorkbook.Parse(duplicateMedicine);
            if (duplicatePreview.IsValid || duplicatePreview.Issues.Count == 0 ||
                duplicatePreview.Issues[0].Code != MedicineCatalogValidationCode.DuplicateMedicine ||
                duplicatePreview.Issues[0].RowNumber != 3 ||
                duplicatePreview.Issues[0].Format(true) != "Row 3: Medicine name or barcode is duplicated in the file.")
                throw new InvalidOperationException("تكرار الصنف لم يحتفظ برقم السطر أو رسالة اللغة الثانية.");
            DataTable maximum = MedicineCatalogWorkbook.CreateTemplate();
            for (int row = 0; row < 2000; row++)
                maximum.Rows.Add("دواء " + row.ToString(CultureInfo.InvariantCulture),
                    "QA-BC-" + row.ToString(CultureInfo.InvariantCulture), "10", "15",
                    "2026-01-01", "2027-01-01", "شركة الدواء");
            MedicineCatalogWorkbookPreview atLimit = MedicineCatalogWorkbook.Parse(maximum);
            if (!atLimit.IsValid || atLimit.Medicines.Count != 2000)
                throw new InvalidOperationException("حد استيراد الأصناف 2,000 صف رفض ملفًا صالحًا.");
            maximum.Rows.Add("دواء زائد", "QA-BC-EXTRA", "10", "15",
                "2026-01-01", "2027-01-01", "شركة الدواء");
            MedicineCatalogWorkbookPreview overLimit = MedicineCatalogWorkbook.Parse(maximum);
            if (overLimit.IsValid || overLimit.Issues[0].Code != MedicineCatalogValidationCode.RowCount)
                throw new InvalidOperationException("حد استيراد الأصناف تجاوز 2,000 صف دون رفض.");
            DataTable blank = MedicineCatalogWorkbook.CreateTemplate(); blank.Rows.Add(blank.NewRow());
            CheckIssue(blank, MedicineCatalogValidationCode.NoRows,
                "لا توجد صفوف أصناف قابلة للاستيراد.", "No medicine rows can be imported.");
        }
        private static void CheckIssue(DataTable table, MedicineCatalogValidationCode expectedCode,
            string arabic, string english)
        {
            MedicineCatalogWorkbookPreview result = MedicineCatalogWorkbook.Parse(table);
            if (result.IsValid || result.Issues.Count == 0 || result.Issues[0].Code != expectedCode ||
                result.Errors[0] != arabic || result.Issues[0].Format(false) != arabic ||
                result.Issues[0].Format(true) != english)
                throw new InvalidOperationException("تعذر مطابقة رمز التحقق ورسائله للغتين: " + expectedCode);
        }
    }
}

// محرك استيراد CSV محمي: معاينة محلية، وكتابة QA فقط داخل معاملة واحدة.
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;

namespace project_fo_3
{    internal enum QaCsvImportKind { Companies, Medicines }

    internal sealed class QaCsvImportIssue
    {
        private readonly string template;
        private readonly object[] values;
        internal QaCsvImportIssue(string template, params object[] values)
        { this.template = template; this.values = values; }
        internal string Display(bool english)
        {
            // Only nested issue/field templates are translated. Raw identifiers,
            // barcodes and names are inserted verbatim after translating the template.
            object[] rendered = values.Select(value => value is QaCsvImportIssue
                ? (object)((QaCsvImportIssue)value).Display(english) : value).ToArray();
            return UiLanguageCatalog.Format(english, template, rendered);
        }
    }

    internal sealed class QaCsvImportPreview
    {
        private readonly List<QaCsvImportIssue> issues;
        internal QaCsvImportPreview(DataTable rows, List<QaCsvImportIssue> issues)
        { Rows = rows; this.issues = issues; Errors = issues.Select(issue => issue.Display(false)).ToList(); }
        internal DataTable Rows { get; private set; }
        internal List<string> Errors { get; private set; }
        internal string[] DisplayErrors(bool english) { return issues.Select(issue => issue.Display(english)).ToArray(); }
        internal bool IsValid { get { return Errors.Count == 0 && Rows.Rows.Count > 0; } }
    }

    internal sealed class QaCsvImportResult
    {
        internal QaCsvImportResult(int imported, int skipped) { Imported = imported; Skipped = skipped; }
        internal int Imported { get; private set; }
        internal int Skipped { get; private set; }
    }

    internal static class QaCsvImportService
    {
        internal static QaCsvImportPreview Preview(QaCsvImportKind kind, string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) throw new FileNotFoundException("ملف CSV غير موجود.", path);
            return PreviewRecords(kind, ReadCsv(path));
        }

        internal static QaCsvImportPreview PreviewExcel(QaCsvImportKind kind, string path)
        {
            DataTable workbook = ExcelWorkbookReader.Read(path);
            var records = new List<string[]> { workbook.Columns.Cast<DataColumn>().Select(column => column.ColumnName).ToArray() };
            records.AddRange(workbook.Rows.Cast<DataRow>().Select(row => row.ItemArray.Select(Convert.ToString).ToArray()));
            return PreviewRecords(kind, records);
        }

        internal static QaCsvImportPreview PreviewRecords(QaCsvImportKind kind, List<string[]> records)
        {
            List<QaCsvImportIssue> errors = new List<QaCsvImportIssue>();
            DataTable table = CreateTable(kind);
            if (records.Count == 0) { errors.Add(new QaCsvImportIssue("الملف فارغ.")); return new QaCsvImportPreview(table, errors); }
            if (records.Count > 2001)
            { errors.Add(new QaCsvImportIssue("ملف الاستيراد يتجاوز 2,000 صف بيانات؛ قسمه إلى دفعات أصغر.")); return new QaCsvImportPreview(table, errors); }
            Dictionary<string, int> indexes = BuildIndexes(records[0]);
            string[] required = kind == QaCsvImportKind.Companies
                ? new[] { "id", "name", "phone", "address" }
                : new[] { "name", "barcode", "buyprice", "sellprice", "quantity", "productiondate", "expirydate", "company" };
            foreach (string key in required) if (!indexes.ContainsKey(key)) errors.Add(new QaCsvImportIssue("العمود المطلوب مفقود: {0}", new QaCsvImportIssue(DisplayName(key))));
            if (errors.Count > 0) return new QaCsvImportPreview(table, errors);

            HashSet<string> unique = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
            HashSet<string> barcodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int rowIndex = 1; rowIndex < records.Count; rowIndex++)
            {
                string[] values = records[rowIndex];
                if (values.All(value => string.IsNullOrWhiteSpace(value))) continue;
                DataRow row = table.NewRow();
                foreach (DataColumn column in table.Columns) row[column.ColumnName] = Read(values, indexes, column.ColumnName);
                string identifier = kind == QaCsvImportKind.Companies ? Convert.ToString(row["id"]).Trim() : Convert.ToString(row["name"]).Trim();
                QaCsvImportIssue error = ValidateRow(kind, row, identifier, unique);
                if (error == null && kind == QaCsvImportKind.Medicines &&
                    !barcodes.Add(Convert.ToString(row["barcode"]).Trim()))
                    error = new QaCsvImportIssue("يوجد باركود مكرر داخل الملف: {0}", Convert.ToString(row["barcode"]).Trim());
                if (error != null) errors.Add(new QaCsvImportIssue("السطر {0}: {1}", (rowIndex + 1).ToString(CultureInfo.InvariantCulture), error));
                table.Rows.Add(row);
            }
            if (table.Rows.Count == 0) errors.Add(new QaCsvImportIssue("لا توجد صفوف بيانات بعد العنوان."));
            return new QaCsvImportPreview(table, errors);
        }

        internal static bool CanImportIntoCurrentDatabase()
        {
            DatabaseConnectionProfile profile = Database.GetConnectionProfile();
            return profile.IsTestEnvironment && profile.DatabaseName.EndsWith("_qa", StringComparison.OrdinalIgnoreCase);
        }
        internal static QaCsvImportResult Import(QaCsvImportKind kind, QaCsvImportPreview preview)
        {
            if (preview == null || !preview.IsValid) throw new InvalidOperationException("اعرض ملفاً صحيحاً قبل الاستيراد.");
            if (!PermissionService.CanOpen(AppPermission.Settings))
                throw new UnauthorizedAccessException("اعتماد الاستيراد متاح للمدير فقط.");
            DatabaseConnectionProfile profile = Database.GetConnectionProfile();
            if (!profile.IsTestEnvironment || !profile.DatabaseName.EndsWith("_qa", StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("الاستيراد مسموح فقط إلى قاعدة اختبار اسمها ينتهي بـ _qa. لم تُعدّل أي بيانات.");

            using (SqlConnection connection = Database.CreateConnection())
            {
                connection.Open();
                using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        if (kind == QaCsvImportKind.Medicines)
                        {
                            CommercialInventoryOperationPolicy.EnsureLegacyAggregateOperationAllowed(CommercialSchemaInstaller.IsFullCommercialModeRequested(connection, transaction));
                        }
                        int imported = 0;
                        int skipped = 0;
                        foreach (DataRow row in preview.Rows.Rows)
                        {
                            bool inserted = kind == QaCsvImportKind.Companies ? InsertCompany(connection, transaction, row) : InsertMedicine(connection, transaction, row);
                            if (inserted) imported++; else skipped++;
                        }
                        transaction.Commit();
                        Database.WriteAudit("QaCsvImport", kind.ToString(), "QA CSV import. Imported=" + imported + "; SkippedDuplicates=" + skipped + ".");
                        return new QaCsvImportResult(imported, skipped);
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        private static bool InsertCompany(SqlConnection connection, SqlTransaction transaction, DataRow row)
        {
            using (SqlCommand command = new SqlCommand(@"
IF EXISTS (SELECT 1 FROM dbo.CompanyTb1 WITH (UPDLOCK, HOLDLOCK) WHERE CompId = @Id) SELECT CAST(0 AS bit);
ELSE BEGIN INSERT INTO dbo.CompanyTb1 (CompId, CompName, CompPhone, CompAddress) VALUES (@Id, @Name, @Phone, @Address); SELECT CAST(1 AS bit); END", connection, transaction))
            {
                command.Parameters.Add("@Id", SqlDbType.NVarChar, 50).Value = Convert.ToString(row["id"]).Trim();
                command.Parameters.Add("@Name", SqlDbType.NVarChar, 200).Value = Convert.ToString(row["name"]).Trim();
                command.Parameters.Add("@Phone", SqlDbType.NVarChar, 50).Value = Convert.ToString(row["phone"]).Trim();
                command.Parameters.Add("@Address", SqlDbType.NVarChar, 300).Value = Convert.ToString(row["address"]).Trim();
                return Convert.ToBoolean(command.ExecuteScalar());
            }
        }

        private static bool InsertMedicine(SqlConnection connection, SqlTransaction transaction, DataRow row)
        {
            using (SqlCommand company = new SqlCommand("SELECT COUNT(1) FROM dbo.CompanyTb1 WHERE CompName = @Company", connection, transaction))
            {
                company.Parameters.Add("@Company", SqlDbType.NVarChar, 200).Value = Convert.ToString(row["company"]).Trim();
                if (Convert.ToInt32(company.ExecuteScalar()) == 0) throw new InvalidOperationException("الشركة غير موجودة للأدوية المستوردة: " + Convert.ToString(row["company"]).Trim());
            }
            using (SqlCommand command = new SqlCommand(@"
IF EXISTS (SELECT 1 FROM dbo.MedicineTb1 WITH (UPDLOCK, HOLDLOCK) WHERE MedName = @Name OR (@Barcode <> N'' AND Barcode = @Barcode)) SELECT CAST(0 AS bit);
ELSE BEGIN INSERT INTO dbo.MedicineTb1 (MedName, Barcode, Bprice, Sprice, MedQty, ProdDate, ExpDate, CompName) VALUES (@Name, @Barcode, @BuyPrice, @SellPrice, @Quantity, @ProductionDate, @ExpiryDate, @Company); SELECT CAST(1 AS bit); END", connection, transaction))
            {
                command.Parameters.Add("@Name", SqlDbType.NVarChar, 200).Value = Convert.ToString(row["name"]).Trim();
                command.Parameters.Add("@Barcode", SqlDbType.NVarChar, 100).Value = Convert.ToString(row["barcode"]).Trim();
                command.Parameters.Add("@BuyPrice", SqlDbType.Int).Value = int.Parse(Convert.ToString(row["buyprice"]), CultureInfo.InvariantCulture);
                command.Parameters.Add("@SellPrice", SqlDbType.Int).Value = int.Parse(Convert.ToString(row["sellprice"]), CultureInfo.InvariantCulture);
                command.Parameters.Add("@Quantity", SqlDbType.Int).Value = int.Parse(Convert.ToString(row["quantity"]), CultureInfo.InvariantCulture);
                DateTime productionDate;
                DateTime expiryDate;
                if (!TryParseImportDate(Convert.ToString(row["productiondate"]), out productionDate) ||
                    !TryParseImportDate(Convert.ToString(row["expirydate"]), out expiryDate))
                {
                    throw new FormatException("تواريخ ملف CSV يجب أن تكون بصيغة yyyy-MM-dd أو yyyy/MM/dd.");
                }
                command.Parameters.Add("@ProductionDate", SqlDbType.DateTime).Value = productionDate;
                command.Parameters.Add("@ExpiryDate", SqlDbType.DateTime).Value = expiryDate;
                command.Parameters.Add("@Company", SqlDbType.NVarChar, 200).Value = Convert.ToString(row["company"]).Trim();
                return Convert.ToBoolean(command.ExecuteScalar());
            }
        }

        private static QaCsvImportIssue ValidateRow(QaCsvImportKind kind, DataRow row, string identifier, HashSet<string> unique)
        {
            if (string.IsNullOrWhiteSpace(identifier)) return new QaCsvImportIssue("معرّف الصف مطلوب.");
            if (!unique.Add(identifier)) return new QaCsvImportIssue("يوجد تكرار داخل الملف: {0}", identifier);
            if (kind == QaCsvImportKind.Companies)
            {
                if (Convert.ToString(row["name"]).Trim().Length == 0 || Convert.ToString(row["phone"]).Trim().Length == 0 || Convert.ToString(row["address"]).Trim().Length == 0) return new QaCsvImportIssue("كل حقول الشركة مطلوبة.");
                if (identifier.Length > 50 || Convert.ToString(row["name"]).Trim().Length > 200 ||
                    Convert.ToString(row["phone"]).Trim().Length > 50 || Convert.ToString(row["address"]).Trim().Length > 300)
                    return new QaCsvImportIssue("أحد حقول الشركة يتجاوز الطول المسموح.");
                return null;
            }
            int value;
            if (Convert.ToString(row["barcode"]).Trim().Length == 0 || Convert.ToString(row["company"]).Trim().Length == 0) return new QaCsvImportIssue("الباركود والشركة مطلوبان.");
            if (identifier.Length > 200 || Convert.ToString(row["barcode"]).Trim().Length > 100 ||
                Convert.ToString(row["company"]).Trim().Length > 200) return new QaCsvImportIssue("أحد حقول الدواء يتجاوز الطول المسموح.");
            if (!int.TryParse(Convert.ToString(row["buyprice"]), NumberStyles.Integer, CultureInfo.InvariantCulture, out value) || value <= 0) return new QaCsvImportIssue("سعر الشراء يجب أن يكون موجباً.");
            if (!int.TryParse(Convert.ToString(row["sellprice"]), NumberStyles.Integer, CultureInfo.InvariantCulture, out value) || value <= 0) return new QaCsvImportIssue("سعر البيع يجب أن يكون موجباً.");
            if (!int.TryParse(Convert.ToString(row["quantity"]), NumberStyles.Integer, CultureInfo.InvariantCulture, out value) || value < 0) return new QaCsvImportIssue("الكمية يجب أن تكون صفراً أو أكثر.");
            DateTime production;
            DateTime expiry;
            if (!TryParseImportDate(Convert.ToString(row["productiondate"]), out production) || !TryParseImportDate(Convert.ToString(row["expirydate"]), out expiry) || production.Date > expiry.Date) return new QaCsvImportIssue("تواريخ الإنتاج والانتهاء غير صالحة. استخدم yyyy-MM-dd أو yyyy/MM/dd.");
            return null;
        }

        private static bool TryParseImportDate(string value, out DateTime date)
        {
            return DateTime.TryParseExact(
                (value ?? string.Empty).Trim(),
                new[] { "yyyy-MM-dd", "yyyy/MM/dd" },
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces,
                out date);
        }

        private static DataTable CreateTable(QaCsvImportKind kind)
        {
            DataTable table = new DataTable();
            foreach (string column in kind == QaCsvImportKind.Companies ? new[] { "id", "name", "phone", "address" } : new[] { "name", "barcode", "buyprice", "sellprice", "quantity", "productiondate", "expirydate", "company" }) table.Columns.Add(column);
            return table;
        }

        private static Dictionary<string, int> BuildIndexes(string[] header)
        {
            Dictionary<string, int> indexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < header.Length; index++)
            {
                string key = NormalizeHeader(header[index]);
                if (!string.IsNullOrWhiteSpace(key) && !indexes.ContainsKey(key)) indexes.Add(key, index);
            }
            return indexes;
        }

        private static string NormalizeHeader(string value)
        {
            string header = (value ?? string.Empty).Trim().Replace(" ", string.Empty).ToLowerInvariant();
            if (header == "id" || header == "companyid" || header == "رقمالشركة") return "id";
            if (header == "name" || header == "companyname" || header == "medicine" || header == "medicinename" || header == "اسمالشركة" || header == "اسمالدواء") return "name";
            if (header == "phone" || header == "الهاتف") return "phone";
            if (header == "address" || header == "العنوان") return "address";
            if (header == "barcode" || header == "الباركود") return "barcode";
            if (header == "buyprice" || header == "purchaseprice" || header == "سعرالشراء") return "buyprice";
            if (header == "sellprice" || header == "saleprice" || header == "سعرالبيع") return "sellprice";
            if (header == "quantity" || header == "الكمية") return "quantity";
            if (header == "productiondate" || header == "prodDate".ToLowerInvariant() || header == "تاريخالإنتاج") return "productiondate";
            if (header == "expirydate" || header == "expdate" || header == "تاريخالانتهاء") return "expirydate";
            if (header == "company" || header == "الشركة") return "company";
            return string.Empty;
        }

        private static string DisplayName(string key)
        {
            switch (key) { case "id": return "رقم الشركة"; case "name": return "الاسم"; case "phone": return "الهاتف"; case "address": return "العنوان"; case "barcode": return "الباركود"; case "buyprice": return "سعر الشراء"; case "sellprice": return "سعر البيع"; case "quantity": return "الكمية"; case "productiondate": return "تاريخ الإنتاج"; case "expirydate": return "تاريخ الانتهاء"; default: return "الشركة"; }
        }

        private static string Read(string[] values, Dictionary<string, int> indexes, string key)
        {
            int index;
            return indexes.TryGetValue(key, out index) && index < values.Length ? (values[index] ?? string.Empty).Trim() : string.Empty;
        }

        private static List<string[]> ReadCsv(string path)
        {
            List<string[]> records = new List<string[]>();
            using (StreamReader reader = new StreamReader(path, true))
            {
                string line;
                System.Text.StringBuilder record = new System.Text.StringBuilder();
                while ((line = reader.ReadLine()) != null)
                {
                    if (record.Length > 0) record.AppendLine();
                    record.Append(line);
                    if (!IsCompleteCsvRecord(record.ToString())) continue;
                    records.Add(ParseCsvLine(record.ToString()));
                    record.Clear();
                }
                if (record.Length > 0)
                {
                    throw new FormatException("يوجد حقل محاط بعلامات اقتباس غير مكتمل في ملف CSV.");
                }
            }
            return records;
        }

        private static bool IsCompleteCsvRecord(string record)
        {
            bool quoted = false;
            for (int index = 0; index < (record ?? string.Empty).Length; index++)
            {
                if (record[index] != '"') continue;
                if (quoted && index + 1 < record.Length && record[index + 1] == '"')
                {
                    index++;
                    continue;
                }
                quoted = !quoted;
            }
            return !quoted;
        }

        private static string[] ParseCsvLine(string line)
        {
            List<string> cells = new List<string>();
            System.Text.StringBuilder value = new System.Text.StringBuilder();
            bool quoted = false;
            for (int index = 0; index < (line ?? string.Empty).Length; index++)
            {
                char character = line[index];
                if (character == '"' && quoted && index + 1 < line.Length && line[index + 1] == '"') { value.Append('"'); index++; }
                else if (character == '"') quoted = !quoted;
                else if (character == ',' && !quoted) { cells.Add(value.ToString()); value.Clear(); }
                else value.Append(character);
            }
            cells.Add(value.ToString());
            return cells.ToArray();
        }
    }

    internal static class QaCsvImportSelfTests
    {
        internal static void RunAll()
        {
            VerifyValidationPresentation();
            string companiesPath = Path.Combine(Path.GetTempPath(), "pharmacy-qa-import-" + Guid.NewGuid().ToString("N") + ".csv");
            string excelPath = Path.ChangeExtension(companiesPath, ".xlsx");
            try
            {
                File.WriteAllText(companiesPath, "CompanyId,CompanyName,Phone,Address\r\nQA-C01,شركة الاختبار,700000000,عدن\r\n");
                QaCsvImportPreview companies = QaCsvImportService.Preview(QaCsvImportKind.Companies, companiesPath);
                Require(companies.IsValid && companies.Rows.Rows.Count == 1, "فشل تحليل CSV الشركات.");
                File.WriteAllText(companiesPath, "MedicineName,Barcode,BuyPrice,SellPrice,Quantity,ProductionDate,ExpiryDate,Company\r\nدواء الاختبار,6281234567890,10,15,3,2026-01-01,2027-01-01,شركة الاختبار\r\n");
                QaCsvImportPreview medicines = QaCsvImportService.Preview(QaCsvImportKind.Medicines, companiesPath);
                Require(medicines.IsValid && medicines.Rows.Rows.Count == 1, "فشل تحليل CSV الأدوية.");
                File.WriteAllText(companiesPath, "CompanyId,CompanyName,Phone,Address\r\nQA-C02,\"شركة \"\"الاختبار\"\"\",700000001,\"عدن\r\nالمنصورة\"\r\n");
                QaCsvImportPreview quotedCompany = QaCsvImportService.Preview(QaCsvImportKind.Companies, companiesPath);
                Require(quotedCompany.IsValid && Convert.ToString(quotedCompany.Rows.Rows[0]["address"]).Contains("المنصورة"), "فشل تحليل حقل CSV متعدد الأسطر.");
                File.WriteAllText(companiesPath, "MedicineName,Barcode,BuyPrice,SellPrice,Quantity,ProductionDate,ExpiryDate,Company\r\nدواء الاختبار,6281234567890,10,15,3,2026/01/01,2027/01/01,شركة الاختبار\r\n");
                Require(QaCsvImportService.Preview(QaCsvImportKind.Medicines, companiesPath).IsValid, "يجب قبول تاريخ CSV غير الملتبس بصيغة yyyy/MM/dd.");
                File.WriteAllText(companiesPath, "CompanyId,CompanyName,Phone,Address\r\nQA-C01,شركة,700,عدن\r\nQA-C01,شركة,700,عدن\r\n");
                Require(!QaCsvImportService.Preview(QaCsvImportKind.Companies, companiesPath).IsValid, "يجب رفض التكرار داخل ملف CSV.");
                File.WriteAllText(companiesPath, "MedicineName,Barcode,BuyPrice,SellPrice,Quantity,ProductionDate,ExpiryDate,Company\r\nدواء أول,6281234567890,10,15,0,2026-01-01,2027-01-01,شركة الاختبار\r\nدواء ثان,6281234567890,10,15,0,2026-01-01,2027-01-01,شركة الاختبار\r\n");
                Require(!QaCsvImportService.Preview(QaCsvImportKind.Medicines, companiesPath).IsValid,
                    "يجب رفض الباركود المكرر داخل ملف الأصناف قبل بدء المعاملة.");
                var excelRows = new DataTable();
                foreach (string name in new[] { "MedicineName", "Barcode", "BuyPrice", "SellPrice", "Quantity", "ProductionDate", "ExpiryDate", "Company" })
                    excelRows.Columns.Add(name);
                excelRows.Rows.Add("دواء الاختبار", "6281234567890", "10", "15", "3", "2026-01-01", "2027-01-01", "شركة الاختبار");
                using (var output = new FileStream(excelPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    ExcelWorkbookWriter.Write(excelRows, "الأدوية", output);
                QaCsvImportPreview excelPreview = QaCsvImportService.PreviewExcel(QaCsvImportKind.Medicines, excelPath);
                Require(excelPreview.IsValid && excelPreview.Rows.Rows.Count == 1 &&
                    Convert.ToString(excelPreview.Rows.Rows[0]["name"]) == "دواء الاختبار", "فشلت معاينة استيراد Excel العربية.");
            }
            finally
            {
                if (File.Exists(companiesPath)) File.Delete(companiesPath);
                if (File.Exists(excelPath)) File.Delete(excelPath);
            }
        }

        private static void VerifyValidationPresentation()
        {
            var originalCulture = System.Threading.Thread.CurrentThread.CurrentCulture;
            try
            {
                foreach (string culture in new[] { "ar-SA", "en-US" })
                {
                    System.Threading.Thread.CurrentThread.CurrentCulture = new CultureInfo(culture);
                    string[] companyHeader = { "CompanyId", "CompanyName", "Phone", "Address" };
                    string[] medicineHeader = { "MedicineName", "Barcode", "BuyPrice", "SellPrice", "Quantity", "ProductionDate", "ExpiryDate", "Company" };
                    const string raw = "حفظ{0} / Save";
                    var duplicate = QaCsvImportService.PreviewRecords(QaCsvImportKind.Companies,
                        new List<string[]> { companyHeader, new[] { raw, "شركة", "700", "عدن" }, new[] { raw, "شركة", "700", "عدن" } });
                    Require(!duplicate.IsValid && duplicate.Errors.Single() == "السطر 3: يوجد تكرار داخل الملف: " + raw &&
                        duplicate.DisplayErrors(true).Single() == "Row 3: Duplicate entry in the file: " + raw &&
                        duplicate.DisplayErrors(false).SequenceEqual(duplicate.Errors) && Convert.ToString(duplicate.Rows.Rows[1]["id"]) == raw,
                        "Structured duplicate presentation changed Arabic errors or raw identifier braces.");
                    var invalidRows = new[] {
                        new[] { "", "B", "10", "15", "0", "2026-01-01", "2027-01-01", "شركة" },
                        new[] { "دواء", "", "10", "15", "0", "2026-01-01", "2027-01-01", "شركة" },
                        new[] { "دواء", "B", "0", "15", "0", "2026-01-01", "2027-01-01", "شركة" },
                        new[] { "دواء", "B", "10", "0", "0", "2026-01-01", "2027-01-01", "شركة" },
                        new[] { "دواء", "B", "10", "15", "-1", "2026-01-01", "2027-01-01", "شركة" },
                        new[] { "دواء", "B", "10", "15", "0", "2028-01-01", "2027-01-01", "شركة" }
                    };
                    string[] expected = { "A row identifier is required.", "Barcode and company are required.", "Purchase price must be positive.",
                        "Selling price must be positive.", "Quantity must be zero or greater.", "Production or expiry dates are invalid. Use yyyy-MM-dd or yyyy/MM/dd." };
                    for (int index = 0; index < invalidRows.Length; index++)
                    {
                        var invalid = QaCsvImportService.PreviewRecords(QaCsvImportKind.Medicines, new List<string[]> { medicineHeader, invalidRows[index] });
                        Require(!invalid.IsValid && invalid.DisplayErrors(true).Single() == "Row 2: " + expected[index], "Validation rule or English row message changed: " + index);
                    }
                    var barcode = QaCsvImportService.PreviewRecords(QaCsvImportKind.Medicines,
                        new List<string[]> { medicineHeader, new[] { "أ", raw, "10", "15", "0", "2026-01-01", "2027-01-01", "شركة" },
                            new[] { "ب", raw, "10", "15", "0", "2026-01-01", "2027-01-01", "شركة" } });
                    Require(barcode.DisplayErrors(true).Single() == "Row 3: Duplicate barcode in the file: " + raw, "Barcode values must remain verbatim.");
                    var missing = QaCsvImportService.PreviewRecords(QaCsvImportKind.Companies, new List<string[]> { new[] { "CompanyId", "CompanyName", "Address" } });
                    Require(missing.DisplayErrors(true).Single() == "Required column is missing: Phone", "Missing-column captions must be translated separately from data.");
                    var empty = QaCsvImportService.PreviewRecords(QaCsvImportKind.Companies, new List<string[]>());
                    Require(empty.Errors.Single() == "الملف فارغ." && empty.DisplayErrors(true).Single() == "The file is empty.", "Empty-file error changed.");
                }
            }
            finally { System.Threading.Thread.CurrentThread.CurrentCulture = originalCulture; }
            Console.WriteLine("PASS structured import validation: ar-SA/en-US, Arabic compatibility, required fields/prices/quantity/dates, duplicates/missing columns/empty and raw braces; no SQL/files.");
        }

        internal static void RunMedicineCatalogRollback(string backupPath)
        {
            DatabaseConnectionProfile profile = Database.GetConnectionProfile();
            if (!RuntimeSettingsStore.IsBuildConnectionProfileForced || profile.DatabaseName != "PharmacyManagement_qa")
                throw new InvalidOperationException("استيراد كتالوج QA يتطلب حزمة متصلة حصرًا بقاعدة PharmacyManagement_qa.");
            string company;
            long medicinesBefore, auditBefore;
            using (SqlConnection connection = Database.CreateConnection())
            {
                connection.Open();
                QaTaxPolicySelfTests.VerifyBackup(connection, backupPath);
                using (var command = new SqlCommand("SELECT TOP (1) CompName FROM dbo.CompanyTb1 ORDER BY CompName;", connection))
                    company = Convert.ToString(command.ExecuteScalar());
                if (string.IsNullOrWhiteSpace(company)) throw new InvalidOperationException("لا توجد شركة QA صالحة لقالب الصنف.");
                medicinesBefore = Scalar(connection, "SELECT COUNT_BIG(*) FROM dbo.MedicineTb1;");
                auditBefore = Scalar(connection, "SELECT COUNT_BIG(*) FROM dbo.AuditLogTb;");
            }
            string name = "دواء تحقق XLSX " + Guid.NewGuid().ToString("N");
            string barcode = "QA-XLSX-" + Guid.NewGuid().ToString("N").Substring(0, 20);
            string path = Path.Combine(Path.GetTempPath(), "pharmacy-qa-catalog-import-" + Guid.NewGuid().ToString("N") + ".xlsx");
            try
            {
                using (DataTable workbook = MedicineCatalogWorkbook.CreateTemplate())
                {
                    workbook.Rows.Add(name, barcode, "10", "15", DateTime.Today.AddDays(-10).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        DateTime.Today.AddYears(1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), company);
                    using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                        ExcelWorkbookWriter.Write(workbook, workbook.TableName, output);
                }
                MedicineCatalogWorkbookPreview preview = MedicineCatalogWorkbook.Read(path);
                if (!preview.IsValid || preview.Medicines.Count != 1 || preview.Medicines[0].MedName != name || preview.Medicines[0].Quantity != 0)
                    throw new InvalidOperationException("لم يحافظ ملف XLSX الحقيقي على تعريف الصنف أو قاعدة الرصيد الصفري.");
                var repository = new PharmacyManagement.Infrastructure.SqlServer.SqlMedicineRepository(Database.CreateConnection);
                var adapter = new QaRollbackMedicineCatalogRepository(repository);
                var service = new PharmacyManagement.Application.MedicineCatalogImportService(adapter);
                var result = service.ImportNew(preview.Medicines,
                    new PharmacyManagement.Application.OperationActor { UserName = "QA XLSX verifier", UserRole = "Manager" });
                if (result.Created != 1 || result.SkippedExisting != 0)
                    throw new InvalidOperationException("خدمة تعريف الأصناف لم تنفذ إضافة الصنف التجريبية داخل معاملة QA.");
            }
            finally { if (File.Exists(path)) File.Delete(path); }
            using (SqlConnection connection = Database.CreateConnection())
            {
                connection.Open();
                long medicinesAfter = Scalar(connection, "SELECT COUNT_BIG(*) FROM dbo.MedicineTb1;");
                long auditAfter = Scalar(connection, "SELECT COUNT_BIG(*) FROM dbo.AuditLogTb;");
                using (var command = new SqlCommand("SELECT COUNT_BIG(*) FROM dbo.MedicineTb1 WHERE MedName=@Name OR Barcode=@Barcode;", connection))
                {
                    command.Parameters.Add("@Name", SqlDbType.NVarChar, 200).Value = name;
                    command.Parameters.Add("@Barcode", SqlDbType.NVarChar, 100).Value = barcode;
                    if (Convert.ToInt64(command.ExecuteScalar()) != 0 || medicinesAfter != medicinesBefore || auditAfter != auditBefore)
                        throw new InvalidOperationException("اختبار استيراد كتالوج QA ترك صفًا أو سجل تدقيق دائمًا.");
                }
            }
            Console.WriteLine("PASS: physical Arabic XLSX catalog file parsed, manager service and SQL insert executed, transaction rolled back; medicine/audit rows unchanged.");
        }

        internal static void RunMedicineCatalogCommittedOnOwnedRestore(string backupPath, string ownerToken)
        {
            DatabaseConnectionProfile profile = Database.GetConnectionProfile();
            System.Text.RegularExpressions.Match ownedName = System.Text.RegularExpressions.Regex.Match(
                profile.DatabaseName ?? string.Empty, @"^PharmacyRestore_(?<time>\d{14})_(?<token>[0-9a-f]{32})_qa$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
            if (!RuntimeSettingsStore.IsBuildConnectionProfileForced || !ownedName.Success ||
                !string.Equals(ownedName.Groups["token"].Value, ownerToken, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("استيراد الصنف المعتمد يتطلب نسخة QA مؤقتة يطابق رمز ملكيتها اسم القاعدة.");
            if (string.IsNullOrWhiteSpace(backupPath) || !File.Exists(backupPath))
                throw new FileNotFoundException("نسخة QA الاحتياطية المطلوبة غير موجودة.", backupPath);

            string company;
            using (SqlConnection connection = Database.CreateConnection())
            {
                connection.Open();
                QaTaxPolicySelfTests.VerifyBackup(connection, backupPath);
                using (var owner = new SqlCommand("SELECT CONVERT(nvarchar(128), value) FROM sys.extended_properties WHERE class=0 AND name=N'QaTest.Owner';", connection))
                {
                    string actualOwner = Convert.ToString(owner.ExecuteScalar());
                    if (!string.Equals(actualOwner, ownerToken, StringComparison.OrdinalIgnoreCase))
                        throw new UnauthorizedAccessException("رمز ملكية نسخة QA المؤقتة غير مطابق.");
                }
                using (var command = new SqlCommand("SELECT TOP (1) CompName FROM dbo.CompanyTb1 ORDER BY CompName;", connection))
                    company = Convert.ToString(command.ExecuteScalar());
                if (string.IsNullOrWhiteSpace(company)) throw new InvalidOperationException("لا توجد شركة صالحة في نسخة QA المؤقتة.");
            }

            string name = "دواء استيراد محفوظ " + Guid.NewGuid().ToString("N");
            string barcode = "QA-XLSX-COMMIT-" + Guid.NewGuid().ToString("N").Substring(0, 16);
            DateTime productionDate = DateTime.Today.AddDays(-10);
            DateTime expiryDate = DateTime.Today.AddYears(1);
            string path = Path.Combine(Path.GetTempPath(), "pharmacy-qa-catalog-commit-" + Guid.NewGuid().ToString("N") + ".xlsx");
            try
            {
                using (DataTable workbook = MedicineCatalogWorkbook.CreateTemplate())
                {
                    workbook.Rows.Add(name, barcode, "10", "15", productionDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        expiryDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), company);
                    using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                        ExcelWorkbookWriter.Write(workbook, workbook.TableName, output);
                }
                MedicineCatalogWorkbookPreview preview = MedicineCatalogWorkbook.Read(path);
                if (!preview.IsValid || preview.Medicines.Count != 1 || preview.Medicines[0].MedName != name ||
                    preview.Medicines[0].Barcode != barcode || preview.Medicines[0].Quantity != 0)
                    throw new InvalidOperationException("ملف XLSX لم يحتفظ بالقيم المطلوبة لاستيراد تعريف الصنف.");

                long medicineCountBefore, auditCountBefore;
                using (SqlConnection connection = Database.CreateConnection())
                {
                    connection.Open();
                    medicineCountBefore = Scalar(connection, "SELECT COUNT_BIG(*) FROM dbo.MedicineTb1;");
                    auditCountBefore = Scalar(connection, "SELECT COUNT_BIG(*) FROM dbo.AuditLogTb;");
                }
                System.Windows.Forms.Application.EnableVisualStyles();
                System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
                AppFonts.Initialize();
                SessionContext.Start("QA XLSX commit verifier", "Admin");
                try
                {
                    using (var form = new MedicineCatalogImportForm(false))
                    {
                        form.Show(); form.Activate(); System.Windows.Forms.Application.DoEvents();
                        WaitForUiTask(form.LoadFileAsync(path), "catalog XLSX preview");
                        var approve = (AntdUI.Button)typeof(MedicineCatalogImportForm).GetField("approve",
                            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(form);
                        var grid = (System.Windows.Forms.DataGridView)typeof(MedicineCatalogImportForm).GetField("grid",
                            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(form);
                        if (!approve.Enabled || grid.Rows.Count != 1)
                            throw new InvalidOperationException("The actual catalog form did not enable one valid workbook row.");
                        using (var confirm = new System.Windows.Forms.Timer { Interval = 250 })
                        {
                            confirm.Tick += (sender, args) =>
                            {
                                IntPtr foreground = GetForegroundWindow();
                                var className = new System.Text.StringBuilder(128);
                                if (foreground != IntPtr.Zero && GetClassName(foreground, className, className.Capacity) > 0 &&
                                    string.Equals(className.ToString(), "#32770", StringComparison.Ordinal))
                                    System.Windows.Forms.SendKeys.SendWait("{ENTER}");
                            };
                            confirm.Start();
                            approve.PerformClick();
                            var busyField = typeof(MedicineCatalogImportForm).GetField("operationInProgress",
                                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                            var timer = System.Diagnostics.Stopwatch.StartNew();
                            while (timer.ElapsedMilliseconds < 20000 && (bool)busyField.GetValue(form))
                            {
                                System.Windows.Forms.Application.DoEvents();
                                System.Threading.Thread.Sleep(10);
                            }
                            confirm.Stop();
                            if ((bool)busyField.GetValue(form)) throw new TimeoutException("Catalog form did not finish the committed import.");
                        }
                        string statusSource = Convert.ToString(typeof(MedicineCatalogImportForm).GetField("statusSource",
                            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(form));
                        bool? statusSuccess = (bool?)typeof(MedicineCatalogImportForm).GetField("statusSuccess",
                            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(form);
                        if (statusSource != "اكتمل الاستيراد: {0} صنف جديد، {1} اسم موجود دون تعديل." || statusSuccess != true || approve.Enabled)
                            throw new InvalidOperationException("The catalog form did not report one successful import and disable duplicate approval.");
                    }
                }
                finally { SessionContext.Clear(); }
                Console.WriteLine("PASS: actual catalog form loaded physical XLSX, confirmed and committed one medicine through its application service on owned temporary QA restore; success state verified.");
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }
        private static void WaitForUiTask(System.Threading.Tasks.Task task, string operation)
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            while (!task.IsCompleted && timer.ElapsedMilliseconds < 15000)
            {
                System.Windows.Forms.Application.DoEvents();
                System.Threading.Thread.Sleep(10);
            }
            if (!task.IsCompleted) throw new TimeoutException("Timed out waiting for " + operation + ".");
            task.GetAwaiter().GetResult();
        }
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        private static extern int GetClassName(IntPtr window, System.Text.StringBuilder className, int maxCount);
        private static long Scalar(SqlConnection connection, string sql)
        { using (var command = new SqlCommand(sql, connection)) return Convert.ToInt64(command.ExecuteScalar()); }

        private sealed class QaRollbackMedicineCatalogRepository : PharmacyManagement.Application.IMedicineCatalogImportRepository
        {
            private readonly PharmacyManagement.Infrastructure.SqlServer.SqlMedicineRepository repository;
            internal QaRollbackMedicineCatalogRepository(PharmacyManagement.Infrastructure.SqlServer.SqlMedicineRepository repository)
            { this.repository = repository; }
            public PharmacyManagement.Application.MedicineCatalogImportResult ImportNew(
                IReadOnlyList<PharmacyManagement.Application.MedicineRecord> medicines,
                PharmacyManagement.Application.OperationActor actor)
            { return repository.VerifyQaImportRollback(medicines, actor); }
        }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }

}




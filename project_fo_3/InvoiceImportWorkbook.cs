using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using PharmacyManagement.Application;

namespace project_fo_3
{
    internal static class InvoiceImportWorkbook
    {
        private static readonly string[] Arabic = { "نوع الفاتورة", "رقم الفاتورة المصدر", "رقم السطر", "تاريخ الفاتورة", "العميل أو المورد", "العملة", "اسم الدواء", "الباركود", "الكمية", "سعر الوحدة" };
        private static readonly string[] English = { "Invoice kind", "Source invoice ID", "Line number", "Invoice date", "Customer or supplier", "Currency", "Medicine name", "Barcode", "Quantity", "Unit price" };
        private static readonly string[] ArabicExtra = { "معرف العميل أو المورد", "هاتف العميل أو المورد", "خصم الفاتورة %", "الضريبة %", "نمط السعر الضريبي", "ملاحظات الفاتورة", "تاريخ الإنتاج", "تاريخ الانتهاء", "رقم التشغيلة", "GTIN", "الرقم التسلسلي" };
        private static readonly string[] EnglishExtra = { "Customer or supplier ID", "Customer or supplier phone", "Invoice discount %", "Tax rate %", "Tax price mode", "Invoice notes", "Production date", "Expiry date", "Batch number", "GTIN", "Serial number" };
        private const string ArabicPayment = "طريقة السداد";
        private const string EnglishPayment = "Payment method";

        internal static DataTable CreateTemplate(bool english)
        {
            var table = new DataTable(english ? "Invoice import" : "استيراد الفواتير");
            foreach (string heading in english ? English : Arabic) table.Columns.Add(heading, typeof(string));
            foreach (string heading in english ? EnglishExtra : ArabicExtra) table.Columns.Add(heading, typeof(string));
            table.Columns.Add(english ? EnglishPayment : ArabicPayment, typeof(string));
            // These columns are identifiers, ISO dates or literal text. Preformatting
            // future Excel entries prevents loss of leading zeros and date conversion.
            // Quantities, prices and percentages remain numeric for Excel calculations.
            table.ExtendedProperties[ExcelWorkbookWriter.TextColumnsProperty] =
                new[] { 0, 1, 3, 4, 5, 6, 7, 10, 11, 14, 15, 16, 17, 18, 19, 20, 21 };
            return table;
        }

        internal static IReadOnlyList<InvoiceImportCandidate> Read(string path, InvoiceImportMode mode, string sourceSystem)
        {
            return Parse(ExcelWorkbookReader.Read(path), mode, sourceSystem);
        }

        internal static IReadOnlyList<InvoiceImportCandidate> Parse(DataTable table, InvoiceImportMode mode, string sourceSystem)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            bool legacy = table.Columns.Count == Arabic.Length;
            bool hasPayment = table.Columns.Count == Arabic.Length + ArabicExtra.Length + 1;
            if (!legacy && table.Columns.Count != Arabic.Length + ArabicExtra.Length && !hasPayment)
                throw new InvalidOperationException("قالب الفواتير يجب أن يحتوي 10 أو 21 أو 22 عمودًا معتمدًا.");
            bool arabic = HeaderMatches(table, Arabic) && (legacy || HeaderMatches(table, ArabicExtra, Arabic.Length)) &&
                (!hasPayment || table.Columns[21].ColumnName.Trim() == ArabicPayment);
            if (!arabic && !(HeaderMatches(table, English) && (legacy || HeaderMatches(table, EnglishExtra, English.Length)) &&
                (!hasPayment || table.Columns[21].ColumnName.Trim() == EnglishPayment)))
                throw new InvalidOperationException("عناوين قالب الفواتير غير مطابقة؛ نزّل قالب الاستيراد المعتمد.");
            var rows = new List<InvoiceImportRow>();
            for (int index = 0; index < table.Rows.Count; index++)
            {
                DataRow cells = table.Rows[index];
                int excelRow = index + 2;
                try
                {
                    string kind = Text(cells, 0);
                    ImportedInvoiceKind invoiceKind;
                    if (kind == "بيع" || kind.Equals("Sale", StringComparison.OrdinalIgnoreCase)) invoiceKind = ImportedInvoiceKind.Sale;
                    else if (kind == "شراء" || kind.Equals("Purchase", StringComparison.OrdinalIgnoreCase)) invoiceKind = ImportedInvoiceKind.Purchase;
                    else throw new InvalidOperationException("نوع الفاتورة يجب أن يكون بيع/شراء أو Sale/Purchase.");
                    int line;
                    if (!int.TryParse(Text(cells, 2), NumberStyles.None, CultureInfo.InvariantCulture, out line))
                        throw new InvalidOperationException("رقم السطر عدد صحيح موجب.");
                    DateTime date;
                    if (!DateTime.TryParseExact(Text(cells, 3), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
                        throw new InvalidOperationException("تاريخ الفاتورة يجب أن يكون yyyy-MM-dd.");
                    decimal quantity = Decimal(cells, 8, "الكمية");
                    decimal price = Decimal(cells, 9, "سعر الوحدة");
                    rows.Add(new InvoiceImportRow { Kind = invoiceKind, SourceInvoiceId = Text(cells, 1), LineNumber = line,
                        InvoiceDate = date, PartyName = Text(cells, 4), CurrencyCode = Text(cells, 5),
                        MedicineName = Text(cells, 6), Barcode = Text(cells, 7), Quantity = quantity, UnitPrice = price,
                        PartyReference = legacy ? null : Text(cells, 10), PartyPhone = legacy ? null : Text(cells, 11),
                        InvoiceDiscountPercent = legacy ? null : OptionalDecimal(cells, 12, "خصم الفاتورة"),
                        TaxRatePercent = legacy ? null : OptionalDecimal(cells, 13, "الضريبة"),
                        PriceTaxMode = legacy ? null : TaxMode(Text(cells, 14)), Notes = legacy ? null : Text(cells, 15),
                        ProductionDate = legacy ? null : OptionalDate(cells, 16, "تاريخ الإنتاج"),
                        ExpiryDate = legacy ? null : OptionalDate(cells, 17, "تاريخ الانتهاء"),
                        BatchNumber = legacy ? null : Text(cells, 18), Gtin = legacy ? null : Text(cells, 19),
                        SerialNumber = legacy ? null : Text(cells, 20),
                        PaymentMethod = hasPayment ? Payment(Text(cells, 21)) : null });
                }
                catch (InvalidOperationException error)
                {
                    throw new InvalidOperationException("صف Excel " + excelRow + ": " + error.Message, error);
                }
            }
            return InvoiceImportValidation.Validate(mode, sourceSystem, rows);
        }
        private static bool HeaderMatches(DataTable table, string[] headings, int offset = 0)
        {
            for (int i = 0; i < headings.Length; i++)
                if (!string.Equals(table.Columns[i + offset].ColumnName.Trim(), headings[i], StringComparison.Ordinal)) return false;
            return true;
        }
        private static string Text(DataRow row, int column) { return Convert.ToString(row[column], CultureInfo.InvariantCulture).Trim(); }
        private static decimal Decimal(DataRow row, int column, string field)
        {
            decimal result;
            if (!decimal.TryParse(Text(row, column), NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture, out result))
                throw new InvalidOperationException(field + " يجب أن يكون رقمًا عشريًا بالفاصلة النقطة مثل 12.50.");
            return result;
        }
        private static decimal? OptionalDecimal(DataRow row, int column, string field)
        {
            return Text(row, column).Length == 0 ? (decimal?)null : Decimal(row, column, field);
        }
        private static DateTime? OptionalDate(DataRow row, int column, string field)
        {
            string value = Text(row, column);
            if (value.Length == 0) return null;
            DateTime date;
            if (!DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
                throw new InvalidOperationException(field + " يجب أن يكون yyyy-MM-dd.");
            return date;
        }
        private static string TaxMode(string value)
        {
            if (value == "شامل") return "Inclusive";
            if (value == "غير شامل") return "Exclusive";
            return value;
        }
        private static string Payment(string value)
        {
            if (value == "نقدي") return "Cash";
            if (value == "آجل") return "Payable";
            return value;
        }
    }
}

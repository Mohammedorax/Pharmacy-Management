using System;
using System.Linq;
using System.Collections.Generic;
using System.Data;

namespace project_fo_3
{
    // يمثل قيم الإشعار المعتمدة للعرض والطباعة فقط؛ لا يعيد حساب الضريبة أو الخصم.
    internal sealed class CommercialReturnPrintHeader
    {
        public long ReturnInvoiceNumber { get; set; }
        public long OriginalInvoiceNumber { get; set; }
        public DateTime IssuedAtUtc { get; set; }
        public string RefundMethod { get; set; }
        public string ReturnReason { get; set; }
        public string IssuedBy { get; set; }
        public string CurrencyCode { get; set; }
    }

    internal sealed class CommercialReturnPrintLine
    {
        public string MedicineName { get; set; }
        public decimal Quantity { get; set; }
        public string Unit { get; set; }
        public decimal UnitPrice { get; set; }
        public int PriceBaseQuantity { get; set; } = 1;
        public decimal GrossTotal { get; set; }
        public decimal DiscountTotal { get; set; }
        public decimal TaxRate { get; set; }
        public decimal TaxTotal { get; set; }
        public decimal NetTotal { get; set; }
    }

    internal static class CommercialReturnPrintData
    {
        internal static DataTable BuildTable(CommercialReturnPrintHeader header, IEnumerable<CommercialReturnPrintLine> lines)
        {
            if (header == null) throw new ArgumentNullException(nameof(header));
            if (header.ReturnInvoiceNumber <= 0 || header.OriginalInvoiceNumber <= 0)
                throw new InvalidOperationException("رقم الإشعار والفاتورة الأصلية مطلوبان للطباعة.");

            DataTable table = new DataTable("CommercialReturnCreditNote");
            table.Columns.Add("رقم الإشعار", typeof(long));
            table.Columns.Add("رقم الفاتورة الأصلية", typeof(long));
            table.Columns.Add("تاريخ الاعتماد UTC", typeof(DateTime));
            table.Columns.Add("الصنف", typeof(string));
            table.Columns.Add("الكمية", typeof(decimal));
            table.Columns.Add("الإجمالي", typeof(decimal));
            table.Columns.Add("الخصم", typeof(decimal));
            table.Columns.Add("نسبة الضريبة", typeof(decimal));
            table.Columns.Add("الضريبة", typeof(decimal));
            table.Columns.Add("الصافي", typeof(decimal));
            table.Columns.Add("طريقة الاسترداد", typeof(string));
            table.Columns.Add("سبب المرتجع", typeof(string));
            table.Columns.Add("المنفذ", typeof(string));
            table.Columns.Add("العملة", typeof(string));
            table.Columns.Add("الوحدة",typeof(string));
            table.Columns.Add("السعر",typeof(decimal));
            table.Columns.Add("أساس السعر (وحدات أساسية)",typeof(int));

            foreach (CommercialReturnPrintLine line in lines ?? new CommercialReturnPrintLine[0])
            {
                if (line == null || line.Quantity <= 0m) throw new InvalidOperationException("سطر إشعار المرتجع غير صالح للطباعة.");
                table.Rows.Add(header.ReturnInvoiceNumber, header.OriginalInvoiceNumber, header.IssuedAtUtc,
                    line.MedicineName ?? string.Empty, line.Quantity, line.GrossTotal, line.DiscountTotal,
                    line.TaxRate, line.TaxTotal, line.NetTotal, CommercialReturnPresentation.RefundMethodDisplay(header.RefundMethod),
                    header.ReturnReason ?? string.Empty, header.IssuedBy ?? string.Empty, header.CurrencyCode ?? string.Empty, string.IsNullOrWhiteSpace(line.Unit) ? "—" : line.Unit, line.UnitPrice, line.PriceBaseQuantity);
            }

            if (table.Rows.Count == 0) throw new InvalidOperationException("لا توجد سطور معتمدة لإشعار المرتجع.");
            return table;
        }

        internal static DataTable BuildItemTable(CommercialReturnPrintHeader header, IEnumerable<CommercialReturnPrintLine> lines, bool leftToRight)
        {
            using(DataTable full = BuildTable(header, lines, leftToRight))
            {
                DataTable items = new DataTable("CommercialReturnItems");
                int[] order={3,15,14,4,5,6,7,8,9};
                foreach(int index in order) items.Columns.Add(full.Columns[index].ColumnName,index==15 ? typeof(string) : full.Columns[index].DataType);
                foreach(DataRow source in full.Rows)
                {
                    object[] values=order.Select(index=>source[index]).ToArray();
                    int basis=Convert.ToInt32(source[16]);
                    if(basis<=0)throw new InvalidOperationException("أساس سعر المرتجع غير صالح للطباعة.");
                    values[1]=Convert.ToDecimal(source[15]).ToString("0.##",System.Globalization.CultureInfo.InvariantCulture)+(basis>1 ? (leftToRight ? " / " : " لكل ")+basis.ToString(System.Globalization.CultureInfo.InvariantCulture) : string.Empty);
                    items.Rows.Add(values);
                }
                return items;
            }
        }

        internal static string BuildDocumentDetails(CommercialReturnPrintHeader header, bool leftToRight)
        {
            if(header==null) throw new ArgumentNullException(nameof(header));
            string template="تاريخ الاعتماد UTC: {0}\nطريقة الاسترداد: {1} | المنفذ: {2}\nسبب المرتجع: {3}";
            if(leftToRight) template=UiLanguageCatalog.Translate("تاريخ الاعتماد UTC: {0}\nطريقة الاسترداد: {1} | المنفذ: {2}\nسبب المرتجع: {3}");
            string refund=CommercialReturnPresentation.RefundMethodDisplay(header.RefundMethod);
            if(leftToRight) refund=UiLanguageCatalog.Translate(refund);
            return string.Format(template,header.IssuedAtUtc.ToString("yyyy-MM-dd HH:mm:ss",System.Globalization.CultureInfo.InvariantCulture),refund,
                header.IssuedBy??string.Empty,header.ReturnReason??string.Empty);
        }

        internal static DataTable BuildTable(CommercialReturnPrintHeader header, IEnumerable<CommercialReturnPrintLine> lines, bool leftToRight)
        {
            DataTable table = BuildTable(header, lines);
            if (!leftToRight) return table;
            DataColumn refundMethod = table.Columns["طريقة الاسترداد"];
            foreach (DataColumn column in table.Columns)
                column.ColumnName = column.ColumnName=="أساس السعر (وحدات أساسية)" ? "Price basis (base units)" : column.ColumnName=="السعر" ? "Price" : UiLanguageCatalog.Translate(column.ColumnName);
            foreach (DataRow row in table.Rows)
                row[refundMethod] = UiLanguageCatalog.Translate(Convert.ToString(row[refundMethod]));
            return table;
        }

        internal static string BuildSubtitle(CommercialReturnPrintHeader header, bool leftToRight)
        {
            if (!leftToRight) return BuildSubtitle(header);
            if (header == null) throw new ArgumentNullException(nameof(header));
            return string.Format(UiLanguageCatalog.Translate("إشعار دائن رقم {0} | فاتورة أصلية رقم {1} | {2}"),
                header.ReturnInvoiceNumber, header.OriginalInvoiceNumber, header.CurrencyCode ?? string.Empty);
        }

        internal static string BuildSubtitle(CommercialReturnPrintHeader header)
        {
            if (header == null) throw new ArgumentNullException(nameof(header));
            return "إشعار دائن رقم " + header.ReturnInvoiceNumber + " | فاتورة أصلية رقم " + header.OriginalInvoiceNumber +
                " | " + (header.CurrencyCode ?? string.Empty);
        }
    }
}

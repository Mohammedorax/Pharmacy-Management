using System;
using System.Collections.Generic;
using System.Data;
using PharmacyManagement.Application;
namespace project_fo_3
{
    internal static class VoucherTableProjection
    {
        internal static DataTable Create(IEnumerable<VoucherDocumentSnapshot> documents)
        {
            if (documents == null) throw new ArgumentNullException(nameof(documents));
            var table = new DataTable();
            table.Columns.Add("رقم السند", typeof(int)); table.Columns.Add("التاريخ", typeof(DateTime));
            table.Columns.Add("نوع السند", typeof(string)); table.Columns.Add("الطرف", typeof(string));
            table.Columns.Add("الحساب", typeof(string)); table.Columns.Add("المبلغ", typeof(decimal));
            table.Columns.Add("ملاحظات", typeof(string)); table.Columns.Add("رقم القيد", typeof(int));
            table.Columns.Add("المستخدم", typeof(string));
            foreach (var item in documents)
                table.Rows.Add(item.VoucherId, item.Date, TypeDisplay(item.Type), item.PartyName, item.AccountCode,
                    item.Amount, item.Notes, (object)item.JournalEntryId ?? DBNull.Value, item.CreatedBy);
            return table;
        }
        internal static string CurrencyCaption(VoucherDocumentSnapshot document,bool english)
        {
            if(document==null)throw new ArgumentNullException(nameof(document));
            return (english?"Voucher currency: ":"عملة السند: ")+(document.SystemCurrencyCode??
                (english?"Unverified (historical record)":"غير موثقة (سجل قديم)"));
        }
        internal static string PrintSubtitle(VoucherDocumentSnapshot document,bool english)
        {
            if(document==null)throw new ArgumentNullException(nameof(document));
            return document.PartyName+" - "+document.Amount.ToString("N2")+" - "+CurrencyCaption(document,english);
        }
        internal static string TypeDisplay(string type)
        {
            switch ((type ?? string.Empty).ToUpperInvariant())
            {
                case "RECEIPTINTERNAL": return "قبض داخلي";
                case "RECEIPTEXTERNAL": return "قبض خارجي";
                case "RECEIPTMULTI": return "قبض متعدد";
                case "PAYMENTINTERNAL": return "صرف داخلي";
                case "PAYMENTEXTERNAL": return "صرف خارجي";
                case "PAYMENTMULTI": return "صرف متعدد";
                default: return type;
            }
        }
    }
}


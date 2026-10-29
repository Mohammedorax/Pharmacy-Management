using System;
using System.Collections.Generic;

namespace PharmacyManagement.Domain
{
    public sealed class PreparedVoucher
    {
        internal PreparedVoucher(string type, string party, string account, string cash, decimal amount, string notes,
            int? center, string description, IReadOnlyList<JournalLineValues> lines, string expectedSystemCurrencyCode)
        { Type=type; PartyName=party; AccountCode=account; CashAccountCode=cash; Amount=amount; Notes=notes;
          CostCenterId=center; Description=description; Lines=lines; ExpectedSystemCurrencyCode=expectedSystemCurrencyCode; }
        public string Type { get; }
        public string PartyName { get; }
        public string AccountCode { get; }
        public string CashAccountCode { get; }
        public decimal Amount { get; }
        public string Notes { get; }
        public int? CostCenterId { get; }
        public string ExpectedSystemCurrencyCode { get; }
        public string Description { get; }
        public IReadOnlyList<JournalLineValues> Lines { get; }
    }
    public static class VoucherPreparation
    {
        public static PreparedVoucher Prepare(string type, string party, string account, string cash, decimal amount, string notes, int? center, string expectedSystemCurrencyCode = null)
        {
            string currency=null;
            if(expectedSystemCurrencyCode!=null)
            {
                currency=expectedSystemCurrencyCode.Trim().ToUpperInvariant();
                if(currency.Length==0||currency.Length>10)throw new InvalidOperationException("رمز عملة المسودة غير صالح.");
                foreach(char character in currency)if(character<'A'||character>'Z')throw new InvalidOperationException("رمز عملة المسودة غير صالح.");
            }
            if (amount <= 0) throw new InvalidOperationException("قيمة السند يجب أن تكون أكبر من صفر.");
            string normalizedType=string.IsNullOrWhiteSpace(type)?"ReceiptExternal":type.Trim();
            bool receipt=false, known=false;
            foreach(var value in new[] { "ReceiptExternal", "ReceiptInternal", "ReceiptMulti", "PaymentExternal", "PaymentInternal", "PaymentMulti" })
                if(string.Equals(value,normalizedType,StringComparison.OrdinalIgnoreCase)) { known=true;receipt=value.StartsWith("Receipt",StringComparison.Ordinal);break; }
            if(!known) throw new InvalidOperationException("نوع السند المحاسبي غير مدعوم.");
            if(string.IsNullOrWhiteSpace(account)) throw new InvalidOperationException("اختر الحساب المقابل للسند.");
            string partyAccount=account.Trim(), settlement=string.IsNullOrWhiteSpace(cash)?"1000":cash.Trim();
            if(string.Equals(partyAccount,settlement,StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("لا يمكن أن يكون الحساب المقابل هو نفس حساب الصندوق/البنك.");
            if(center.HasValue && center.Value<=0) throw new InvalidOperationException("مركز التكلفة غير صحيح.");
            string scope=normalizedType.IndexOf("Multi",StringComparison.OrdinalIgnoreCase)>=0?" متعدد":
                normalizedType.IndexOf("Internal",StringComparison.OrdinalIgnoreCase)>=0?" داخلي":" خارجي";
            string description=(receipt?"سند قبض":"سند صرف")+scope+" - "+party;
            var lines=JournalEntryPreparation.Prepare(receipt?new[] {
                new JournalLineValues(settlement,amount,0,notes,center),new JournalLineValues(partyAccount,0,amount,notes,center) }:
                new[] {new JournalLineValues(partyAccount,amount,0,notes,center),new JournalLineValues(settlement,0,amount,notes,center)});
            // Persist the same prepared amount as the journal, never let SQL round a raw midpoint differently.
            return new PreparedVoucher(normalizedType,party,partyAccount,settlement,lines[0].Debit,notes,center,description,lines,currency);
        }
    }
}

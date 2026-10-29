using System;
using System.Collections.Generic;

namespace PharmacyManagement.Domain
{
    public sealed class JournalAmountException : InvalidOperationException
    {
        public JournalAmountException(string message) : base(message) { }
        public JournalAmountException(string message, Exception inner) : base(message, inner) { }
    }

    public sealed class JournalDraftTotals
    {
        internal JournalDraftTotals(decimal debit, decimal credit)
        {
            Debit = debit;
            Credit = credit;
            Difference = debit - credit;
        }
        public decimal Debit { get; }
        public decimal Credit { get; }
        public decimal Difference { get; }
    }

    public static class JournalAmountCalculator
    {
        public static decimal ToLocal(decimal amount, decimal exchangeRate)
        {
            if (amount < 0m) throw new JournalAmountException("مبلغ القيد لا يقبل قيمة سالبة.");
            if (exchangeRate <= 0m) throw new JournalAmountException("سعر التحويل يجب أن يكون موجبًا.");
            try
            {
                // Preserve posting's two-decimal, midpoint-to-even calculation.
                return Math.Round(amount * exchangeRate, 2, MidpointRounding.ToEven);
            }
            catch (OverflowException error)
            {
                throw new JournalAmountException("المبلغ بعد التحويل يتجاوز مجال الحساب؛ خفّض المبلغ أو سعر التحويل.", error);
            }
        }

        public static decimal Preview(decimal debit, decimal credit, decimal exchangeRate)
        {
            if (debit < 0m || credit < 0m) throw new JournalAmountException("قيم المدين والدائن لا تقبل أرقامًا سالبة.");
            // Editing may temporarily fill both fields; line validation still rejects that state.
            return ToLocal(Math.Max(debit, credit), exchangeRate);
        }

        public static JournalDraftTotals Sum(IEnumerable<JournalLineValues> lines)
        {
            if (lines == null) throw new ArgumentNullException(nameof(lines));
            decimal debit = 0m, credit = 0m;
            try
            {
                foreach (var line in lines)
                {
                    if (line == null) throw new JournalAmountException("سطر القيد غير مكتمل.");
                    if (line.Debit < 0m || line.Credit < 0m)
                        throw new JournalAmountException("قيم المدين والدائن لا تقبل أرقامًا سالبة.");
                    debit += line.Debit;
                    credit += line.Credit;
                }
                return new JournalDraftTotals(debit, credit);
            }
            catch (OverflowException error)
            {
                throw new JournalAmountException("إجمالي القيد يتجاوز مجال الحساب؛ راجع مبالغ السطور.", error);
            }
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;

namespace PharmacyManagement.Domain
{
    public sealed class JournalLineValues
    {
        public JournalLineValues(string accountCode, decimal debit, decimal credit, string description, int? costCenterId)
        { AccountCode = accountCode; Debit = debit; Credit = credit; Description = description; CostCenterId = costCenterId; }
        public string AccountCode { get; }
        public decimal Debit { get; }
        public decimal Credit { get; }
        public string Description { get; }
        public int? CostCenterId { get; }
    }
    public static class JournalEntryPreparation
    {
        public const decimal MaximumLineAmount = 9999999999999999.99m;

        public static void ValidateLineAmounts(decimal debit, decimal credit)
        {
            if (debit < 0m || credit < 0m)
                throw new JournalAmountException("قيم المدين والدائن لا تقبل أرقامًا سالبة.");
            if (debit > MaximumLineAmount || credit > MaximumLineAmount)
                throw new JournalAmountException("مبلغ سطر القيد يتجاوز حد التخزين المحاسبي؛ خفّض المبلغ أو سعر التحويل.");
        }
        public static IReadOnlyList<JournalLineValues> Prepare(IEnumerable<JournalLineValues> lines)
        {
            if (lines == null) throw new ArgumentNullException(nameof(lines));
            var prepared = new List<JournalLineValues>();
            foreach (var line in lines)
            {
                // Keep the existing blank-row contract used by grid-based drafts.
                if (line == null || string.IsNullOrWhiteSpace(line.AccountCode)) continue;
                if (line.Debit < 0m || line.Credit < 0m)
                    throw new InvalidOperationException("قيم المدين والدائن لا تقبل أرقامًا سالبة.");
                if (line.Debit > 0m && line.Credit > 0m)
                    throw new InvalidOperationException("اختر مدينًا أو دائنًا لكل سطر قيد، وليس كليهما.");
                if (line.Debit == 0m && line.Credit == 0m) continue;
                decimal debit = Math.Round(line.Debit, 2), credit = Math.Round(line.Credit, 2);
                ValidateLineAmounts(debit, credit);
                prepared.Add(new JournalLineValues(line.AccountCode.Trim(), debit, credit, line.Description, line.CostCenterId));
            }
            decimal debitTotal = prepared.Sum(line => line.Debit), creditTotal = prepared.Sum(line => line.Credit);
            if (prepared.Count < 2 || debitTotal <= 0m || creditTotal <= 0m || debitTotal != creditTotal)
                throw new InvalidOperationException("القيد غير متوازن. يجب أن يساوي مجموع المدين مجموع الدائن.");
            return prepared.AsReadOnly();
        }
    }
}

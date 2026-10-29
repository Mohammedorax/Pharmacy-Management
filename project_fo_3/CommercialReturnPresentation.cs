using System;
using System.Collections.Generic;

namespace project_fo_3
{
    internal sealed class CommercialReturnChoice
    {
        internal CommercialReturnChoice(string code, string label)
        {
            Code = code;
            Label = label;
        }

        public string Code { get; private set; }
        public string Label { get; private set; }
        public override string ToString() { return Label; }
    }

    // تفصل تسمية الواجهة العربية عن القيم الثابتة المخزنة في قاعدة البيانات.
    internal static class CommercialReturnPresentation
    {
        private static readonly CommercialReturnChoice[] refundMethods =
        {
            new CommercialReturnChoice("OriginalMethod", "نقداً (المسار الأصلي الحالي)"),
            new CommercialReturnChoice("Cash", "نقداً"),
            new CommercialReturnChoice("Card", "بطاقة"),
            new CommercialReturnChoice("Credit", "رصيد العميل")
        };

        private static readonly CommercialReturnChoice[] dispositions =
        {
            new CommercialReturnChoice("Restock", "إعادة إلى المخزون"),
            new CommercialReturnChoice("Quarantine", "حجر"),
            new CommercialReturnChoice("Damaged", "تالف")
        };

        internal static IReadOnlyList<CommercialReturnChoice> RefundMethods { get { return refundMethods; } }
        internal static IReadOnlyList<CommercialReturnChoice> Dispositions { get { return dispositions; } }

        internal static decimal ParseQuantity(string value, decimal available)
        {
            decimal quantity;
            if (!decimal.TryParse(value, out quantity) || quantity < 0m || quantity != decimal.Truncate(quantity))
                throw new InvalidOperationException("كمية المرتجع يجب أن تكون عدداً صحيحاً غير سالب. أدخل صفراً لاستبعاد الدفعة.");
            if (quantity > available)
                throw new InvalidOperationException("كمية المرتجع تتجاوز المتاح للدفعة.");
            return quantity;
        }

        internal static string RefundMethodDisplay(string code)
        {
            return Display(refundMethods, code);
        }

        internal static string DispositionDisplay(string code)
        {
            return Display(dispositions, code);
        }

        private static string Display(IEnumerable<CommercialReturnChoice> choices, string code)
        {
            foreach (CommercialReturnChoice choice in choices)
            {
                if (string.Equals(choice.Code, code, StringComparison.Ordinal)) return choice.Label;
            }

            return string.IsNullOrWhiteSpace(code) ? string.Empty : code;
        }
    }
}

using System;

namespace PharmacyManagement.Domain
{
    // Stock remains in whole smallest units. Prices belong to the selected package;
    // they must never be divided and rounded back into a legacy integer price.
    public sealed class MedicineUnitDefinition
    {
        public Guid Id { get; }
        public long Revision { get; }
        public string Name { get; }
        public int BaseUnitsPerUnit { get; }
        public decimal SellingPrice { get; }
        public decimal BuyingPrice { get; }
        public string CurrencyCode { get; }

        public MedicineUnitDefinition(Guid id, long revision, string name, int baseUnitsPerUnit,
            decimal sellingPrice, decimal buyingPrice, string currencyCode)
        {
            if (id == Guid.Empty || revision <= 0) throw new InvalidOperationException("هوية وحدة الصنف غير صالحة.");
            string normalized = (name ?? string.Empty).Trim();
            if (normalized.Length == 0 || normalized.Length > 50) throw new InvalidOperationException("اسم الوحدة مطلوب وبحد أقصى 50 حرفًا.");
            if (baseUnitsPerUnit <= 0) throw new InvalidOperationException("معامل تحويل الوحدة يجب أن يكون عددًا صحيحًا موجبًا من الوحدة الأساسية.");
            MedicineUnitCalculator.EnsureMoney(sellingPrice);
            MedicineUnitCalculator.EnsureMoney(buyingPrice);
            string code = (currencyCode ?? string.Empty).Trim().ToUpperInvariant();
            if (code.Length == 0 || code.Length > 10) throw new InvalidOperationException("عملة أسعار الوحدة غير صالحة.");
            Id = id; Revision = revision; Name = normalized; BaseUnitsPerUnit = baseUnitsPerUnit;
            SellingPrice = sellingPrice; BuyingPrice = buyingPrice; CurrencyCode = code;
        }
    }

    // Immutable values to persist with a line. A later catalogue edit cannot rewrite its stock/money meaning.
    public sealed class MedicineUnitLineSnapshot
    {
        public Guid UnitId { get; }
        public long UnitRevision { get; }
        public string UnitName { get; }
        public int BaseUnitsPerUnit { get; }
        public decimal Quantity { get; }
        public decimal UnitPrice { get; }
        public string CurrencyCode { get; }
        public int BaseQuantity { get; }
        public decimal GrossTotal { get; }
        internal MedicineUnitLineSnapshot(MedicineUnitDefinition unit, decimal quantity, decimal price, int baseQuantity, decimal gross)
        {
            UnitId = unit.Id; UnitRevision = unit.Revision; UnitName = unit.Name; BaseUnitsPerUnit = unit.BaseUnitsPerUnit;
            Quantity = quantity; UnitPrice = price; CurrencyCode = unit.CurrencyCode; BaseQuantity = baseQuantity; GrossTotal = gross;
        }
    }

    public static class MedicineUnitCalculator
    {
        private const decimal MaximumMoney = 9999999999999999.99m; // decimal(18,2)
        public static int ToBaseQuantity(decimal quantity, int baseUnitsPerUnit)
        {
            if (quantity <= 0 || decimal.Round(quantity, 3) != quantity)
                throw new InvalidOperationException("كمية الوحدة يجب أن تكون موجبة وبحد أقصى ثلاث منازل عشرية.");
            if (baseUnitsPerUnit <= 0) throw new InvalidOperationException("معامل تحويل الوحدة غير صالح.");
            decimal converted;
            try { converted = checked(quantity * baseUnitsPerUnit); }
            catch (OverflowException) { throw new InvalidOperationException("كمية المخزون الناتجة تتجاوز الحد المسموح."); }
            if (converted > int.MaxValue || converted != decimal.Truncate(converted))
                throw new InvalidOperationException("الكمية يجب أن تنتج عددًا صحيحًا من الوحدة الأساسية ضمن حد المخزون.");
            return decimal.ToInt32(converted);
        }

        public static MedicineUnitLineSnapshot CreateLine(MedicineUnitDefinition unit, decimal quantity, decimal price, string currencyCode)
        {
            if (unit == null) throw new ArgumentNullException(nameof(unit));
            if (!string.Equals(unit.CurrencyCode, (currencyCode ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("عملة سعر الوحدة تختلف عن عملة الفاتورة؛ حدّث الأسعار قبل الحفظ.");
            EnsureMoney(price);
            int baseQuantity = ToBaseQuantity(quantity, unit.BaseUnitsPerUnit);
            decimal gross;
            try { gross = decimal.Round(checked(quantity * price), 2, MidpointRounding.AwayFromZero); }
            catch (OverflowException) { throw new InvalidOperationException("قيمة بند الوحدة تتجاوز الحد المسموح."); }
            if (gross <= 0 || gross > MaximumMoney) throw new InvalidOperationException("إجمالي بند الوحدة غير صالح.");
            return new MedicineUnitLineSnapshot(unit, quantity, price, baseQuantity, gross);
        }

        // An already returned amount is checked in base units, including mixed package returns.
        public static int ReturnBaseQuantity(MedicineUnitLineSnapshot original, decimal quantity, int alreadyReturnedBaseQuantity)
        {
            if (original == null) throw new ArgumentNullException(nameof(original));
            if (alreadyReturnedBaseQuantity < 0 || alreadyReturnedBaseQuantity > original.BaseQuantity)
                throw new InvalidOperationException("رصيد الإرجاع السابق غير صالح.");
            int returned = ToBaseQuantity(quantity, original.BaseUnitsPerUnit);
            if (returned > original.BaseQuantity - alreadyReturnedBaseQuantity)
                throw new InvalidOperationException("كمية الإرجاع تتجاوز المتبقي من البند الأصلي.");
            return returned;
        }

        internal static void EnsureMoney(decimal price)
        {
            if (price < 0 || price > MaximumMoney || decimal.Round(price, 2) != price)
                throw new InvalidOperationException("سعر الوحدة يجب أن يكون غير سالب وبحد أقصى منزلتين عشريتين ضمن حد تخزين المبلغ.");
        }
    }
}

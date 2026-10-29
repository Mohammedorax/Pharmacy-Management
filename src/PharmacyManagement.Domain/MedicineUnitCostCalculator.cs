using System;
namespace PharmacyManagement.Domain
{
    public static class MedicineUnitCostCalculator
    {
        // Allocate from persisted remaining cost, not a rounded per-base-unit price.
        // The final base unit carries the exact remaining cents, including after partial returns.
        public static decimal AllocateRemaining(decimal remainingCost, int remainingQuantity, int quantity)
        {
            MedicineUnitCalculator.EnsureMoney(remainingCost);
            if (remainingQuantity <= 0 || quantity <= 0 || quantity > remainingQuantity)
                throw new InvalidOperationException("كمية توزيع تكلفة المخزون غير صالحة.");
            return quantity == remainingQuantity ? remainingCost : decimal.Round(remainingCost * quantity / remainingQuantity, 2, MidpointRounding.AwayFromZero);
        }
    }
}

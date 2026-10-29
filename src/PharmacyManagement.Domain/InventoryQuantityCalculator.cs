using System;
namespace PharmacyManagement.Domain
{
    public sealed class InventoryQuantityException : InvalidOperationException
    { public InventoryQuantityException(string message) : base(message) { } }
    public sealed class InventoryQuantityChange
    {
        internal InventoryQuantityChange(int before,int after,int delta) {Before=before;After=after;Delta=delta;}
        public int Before {get;}
        public int After {get;}
        public int Delta {get;}
    }
    public static class InventoryQuantityCalculator
    {
        public static InventoryQuantityChange Calculate(int before,int? finalQuantity,int? quantityDelta)
        {
            // Preserve the existing final-balance precedence and absent-delta no-op.
            long after=finalQuantity.HasValue ? finalQuantity.Value : (long)before+quantityDelta.GetValueOrDefault();
            if(after<0)throw new InventoryQuantityException("لا يمكن تنفيذ العملية لأن الرصيد سيصبح أقل من صفر.");
            if(after>int.MaxValue)throw new InventoryQuantityException("الرصيد الناتج يتجاوز حد تخزين الكمية.");
            long delta=after-before;
            if(delta<int.MinValue||delta>int.MaxValue)throw new InventoryQuantityException("فرق حركة المخزون يتجاوز حد تخزين الكمية.");
            return new InventoryQuantityChange(before,(int)after,(int)delta);
        }
    }
}
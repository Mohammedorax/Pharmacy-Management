using System;
using PharmacyManagement.Domain;

namespace PharmacyManagement.Domain
{
    // A quote captured when the user selected a package. Never refresh it silently on posting.
    public sealed class MedicineUnitSelection
    {
        public Guid UnitId { get; }
        public long CatalogRevision { get; }
        public string Name { get; }
        public int BaseUnitsPerUnit { get; }
        public decimal EnteredQuantity { get; }
        public decimal UnitPrice { get; }
        public decimal CatalogPrice { get; }
        public string CurrencyCode { get; }
        public int BaseQuantity { get; }
        public decimal GrossTotal { get; }
        public MedicineUnitSelection(Guid unitId, long catalogRevision, string name, int baseUnitsPerUnit,
            decimal enteredQuantity, decimal unitPrice, decimal catalogPrice, string currencyCode)
        {
            var definition = new MedicineUnitDefinition(unitId, catalogRevision, name, baseUnitsPerUnit,
                catalogPrice, catalogPrice, currencyCode);
            var line = MedicineUnitCalculator.CreateLine(definition, enteredQuantity, unitPrice, currencyCode);
            UnitId=line.UnitId; CatalogRevision=line.UnitRevision; Name=line.UnitName;
            BaseUnitsPerUnit=line.BaseUnitsPerUnit; EnteredQuantity=line.Quantity; UnitPrice=line.UnitPrice;
            CatalogPrice=catalogPrice; CurrencyCode=line.CurrencyCode; BaseQuantity=line.BaseQuantity; GrossTotal=line.GrossTotal;
        }
        public MedicineUnitSelection WithQuantity(decimal quantity) => new MedicineUnitSelection(UnitId,CatalogRevision,Name,BaseUnitsPerUnit,quantity,UnitPrice,CatalogPrice,CurrencyCode);
        public MedicineUnitSelection WithPrice(decimal price) => new MedicineUnitSelection(UnitId,CatalogRevision,Name,BaseUnitsPerUnit,EnteredQuantity,price,CatalogPrice,CurrencyCode);
        public override string ToString() => Name;
    }
}

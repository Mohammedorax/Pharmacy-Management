using System;
using PharmacyManagement.Domain;

internal static class MedicineUnitTests
{
    static int checks;
    static void Require(bool value, string description) { checks++; if (!value) throw new InvalidOperationException(description); }
    static void Reject(Action action, string description)
    {
        checks++;
        try { action(); } catch (InvalidOperationException) { return; }
        throw new Exception("Expected rejection: " + description);
    }
    static MedicineUnitDefinition Unit(int factor, decimal price=100m, long revision=1)
    { return new MedicineUnitDefinition(Guid.NewGuid(), revision, "شريط", factor, price, 70m, "YER"); }
    public static int Run()
    {
        try
        {
            var tablet = Unit(1, 2.50m); var strip = Unit(10, 24m); var carton = Unit(120, 270m);
            var a = MedicineUnitCalculator.CreateLine(tablet, 3, tablet.SellingPrice, "YER");
            var b = MedicineUnitCalculator.CreateLine(strip, 2, strip.SellingPrice, "YER");
            var c = MedicineUnitCalculator.CreateLine(carton, 1, carton.SellingPrice, "YER");
            Require(a.BaseQuantity == 3 && a.GrossTotal == 7.50m, "Three tablets");
            Require(b.BaseQuantity == 20 && b.GrossTotal == 48m, "Two strips");
            Require(c.BaseQuantity == 120 && c.GrossTotal == 270m, "One carton");
            Require(a.BaseQuantity+b.BaseQuantity+c.BaseQuantity==143, "Mixed-unit sale removes exactly 143 base units");
            Require(a.GrossTotal+b.GrossTotal+c.GrossTotal==325.50m, "Independent selected-package prices total 325.50");
            Require(MedicineUnitCalculator.ToBaseQuantity(.5m, 10) == 5, "Half strip is five tablets");
            Require(MedicineUnitCalculator.ToBaseQuantity(.125m, 120) == 15, "One eighth carton is fifteen tablets");
            Require(MedicineUnitCalculator.ReturnBaseQuantity(c,.25m,60)==30, "Return uses original package factor");
            var edited = Unit(240, 500m, 2);
            Require(c.BaseUnitsPerUnit==120 && c.UnitPrice==270m && c.UnitRevision==1, "Catalogue edits cannot change snapshots");
            Require(MedicineUnitCalculator.CreateLine(strip,.5m,1.01m,"YER").GrossTotal==.51m,"Half cent rounds away from zero");
            Reject(()=>MedicineUnitCalculator.ToBaseQuantity(.5m,1),"Fractional tablet");
            Reject(()=>MedicineUnitCalculator.ToBaseQuantity(.0001m,10000),"More than three quantity decimals");
            Reject(()=>MedicineUnitCalculator.ToBaseQuantity(0,10),"Zero quantity");
            Reject(()=>MedicineUnitCalculator.ToBaseQuantity(-1,10),"Negative quantity");
            Reject(()=>MedicineUnitCalculator.ToBaseQuantity(1,0),"Zero factor");
            Reject(()=>MedicineUnitCalculator.ToBaseQuantity(2,int.MaxValue),"Stock overflow");
            Reject(()=>MedicineUnitCalculator.ToBaseQuantity(decimal.MaxValue,int.MaxValue),"Decimal overflow");
            Reject(()=>MedicineUnitCalculator.CreateLine(strip,1,1.001m,"YER"),"Price precision");
            Reject(()=>MedicineUnitCalculator.CreateLine(strip,1,-1,"YER"),"Negative price");
            Reject(()=>MedicineUnitCalculator.CreateLine(strip,1,0,"YER"),"Zero value line");
            Reject(()=>MedicineUnitCalculator.CreateLine(strip,1,24,"SAR"),"Currency mismatch");
            Reject(()=>MedicineUnitCalculator.ReturnBaseQuantity(c,.75m,60),"Over return");
            Reject(()=>MedicineUnitCalculator.ReturnBaseQuantity(c,.5m,-1),"Invalid return history");
            Reject(()=>Unit(-1),"Negative conversion factor");
            Require(new MedicineUnitDefinition(Guid.NewGuid(),1,"حبة",1,1,1,"ريال").CurrencyCode=="ريال","Preserve existing custom currency-code contract");
            Reject(()=>new MedicineUnitDefinition(Guid.NewGuid(),1,"حبة",1,1,1,"TOOLONGCODE"),"Currency-code storage length");
            Require(MedicineUnitCalculator.ToBaseQuantity(int.MaxValue,1)==int.MaxValue,"Maximum stored stock quantity");
            for(int factor=1;factor<=120;factor++)for(int qty=1;qty<=10;qty++)
            {
                int baseQuantity=MedicineUnitCalculator.ToBaseQuantity(qty,factor);
                Require(baseQuantity==qty*factor,"Whole package conversion");
                Require(InventoryQuantityCalculator.Calculate(2000,null,-baseQuantity).After==2000-qty*factor,"Converted stock decrement");
            }
            Console.WriteLine("PASS medicine-unit domain checks="+checks+"; no SQL/UI integration claimed.");return checks;
        }
        catch { throw; }
    }
}

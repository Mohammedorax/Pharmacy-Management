using System;
using PharmacyManagement.Domain;
namespace PharmacyManagement.CoreTests
{
    internal static class InventoryQuantityCalculatorTests
    {
        private static int checks;
        private static void Equal(int before,int? final,int? delta,int after,int difference)
        {
            var result=InventoryQuantityCalculator.Calculate(before,final,delta);
            if(result.Before!=before||result.After!=after||result.Delta!=difference)throw new Exception("Inventory quantity mapping differs.");
            checks++;
        }
        private static void Reject(int before,int? final,int? delta,string message)
        {
            try {InventoryQuantityCalculator.Calculate(before,final,delta);}
            catch(InventoryQuantityException error) {if(error.Message!=message)throw;checks++;return;}
            throw new Exception("Invalid inventory storage quantity accepted.");
        }
        internal static int Run()
        {
            checks=0;
            Equal(10,null,5,15,5);Equal(10,null,-3,7,-3);Equal(10,2,null,2,-8);Equal(10,0,null,0,-10);
            Equal(0,null,null,0,0);Equal(10,4,100,4,-6);Equal(int.MaxValue,null,0,int.MaxValue,0);
            Equal(0,int.MaxValue,null,int.MaxValue,int.MaxValue);Equal(int.MaxValue,0,null,0,-2147483647);
            Equal(-5,0,null,0,5);Equal(-1,null,1,0,1);
            Reject(0,null,-1,"لا يمكن تنفيذ العملية لأن الرصيد سيصبح أقل من صفر.");
            Reject(int.MaxValue,null,1,"الرصيد الناتج يتجاوز حد تخزين الكمية.");
            Reject(int.MaxValue,null,int.MaxValue,"الرصيد الناتج يتجاوز حد تخزين الكمية.");
            Reject(int.MinValue,int.MaxValue,null,"فرق حركة المخزون يتجاوز حد تخزين الكمية.");
            Reject(int.MinValue,0,null,"فرق حركة المخزون يتجاوز حد تخزين الكمية.");
            Reject(1,int.MinValue,null,"لا يمكن تنفيذ العملية لأن الرصيد سيصبح أقل من صفر.");
            Reject(int.MinValue,null,int.MaxValue,"لا يمكن تنفيذ العملية لأن الرصيد سيصبح أقل من صفر.");
            Console.WriteLine("PASS: 18 inventory quantity range, signed movement, precedence and repair contracts; no SQL.");
            return checks;
        }
    }
}
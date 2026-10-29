using System;
using System.Linq;
using PharmacyManagement.Application;
using PharmacyManagement.Domain;
internal static class MedicineUnitInvoiceTests
{
    static int checks;
    static void Check(bool ok,string name){checks++;if(!ok)throw new Exception(name);}
    static void Reject(Action action){checks++;try{action();}catch(InvalidOperationException){return;}throw new Exception("Expected unit contract rejection");}
    internal static int Run()
    {
        var id=Guid.NewGuid();var unit=new MedicineUnitSelection(id,4,"شريط أصلي",10,2.5m,10.97m,10.97m,"YER");
        Check(unit.BaseQuantity==25&&unit.GrossTotal==27.43m,"Package money separate from base quantity");
        var edited=unit.WithQuantity(1.2m).WithPrice(24m);
        Check(edited.BaseQuantity==12&&edited.GrossTotal==28.80m&&unit.EnteredQuantity==2.5m,"Immutable edit");
        Check(edited.CatalogRevision==4&&edited.Name==unit.Name&&edited.CatalogPrice==10.97m,"Quote remains original across manual price edits");
        Reject(()=>unit.WithQuantity(1.25m));Reject(()=>unit.WithPrice(1.001m));
        var actor=new OperationActor{UserName="UnitTest",UserRole="Admin"};
        var line=new PurchaseInvoiceLineRequest{LineNumber=1,MedicineName="دواء",Quantity=25,Unit=unit,ProductionDate=DateTime.Today,ExpiryDate=DateTime.Today.AddYears(1)};
        var request=new PurchaseInvoiceRequest{SupplierName="Supplier",EmployeeName=actor.UserName,ActorRole=actor.UserRole,PurchaseDate=DateTime.Today,Lines=new[]{line}};
        var normalized=PurchaseInvoiceValidation.Normalize(request,DateTime.Today);
        Check(normalized.Lines[0].Unit==unit&&PurchaseInvoiceValidation.CalculateTotal(normalized.Lines)==27.43m,"Normalization preserves immutable financial unit");
        line.Quantity=24;Reject(()=>PurchaseInvoiceValidation.Normalize(request,DateTime.Today));line.Quantity=25;
        var intent=new PurchaseWriteIntent(Guid.NewGuid(),"1",actor,request,DateTime.Today);
        var hash=intent.Fingerprint();line.Unit=unit.WithPrice(11m);
        Check(intent.Snapshot().Lines[0].Unit.UnitPrice==10.97m&&hash.SequenceEqual(intent.Fingerprint()),"Caller mutation cannot change write identity");
        var other=new PurchaseWriteIntent(Guid.NewGuid(),"1",actor,request,DateTime.Today);
        Check(!hash.SequenceEqual(other.Fingerprint()),"Package price participates in write identity");
        line.Unit=new MedicineUnitSelection(id,5,"شريط معدل",5,5m,10.97m,10.97m,"YER");
        Check(!hash.SequenceEqual(new PurchaseWriteIntent(Guid.NewGuid(),"1",actor,request,DateTime.Today).Fingerprint()),"Conversion/revision participates in write identity");
        decimal cost=100m;int remaining=3;decimal[] expected={33.33m,33.34m,33.33m};
        for(int i=0;i<3;i++){decimal take=MedicineUnitCostCalculator.AllocateRemaining(cost,remaining,1);Check(take==expected[i],"Exact split cost "+i);cost-=take;remaining--;}
        Check(cost==0m,"Full lot cost conserved");
        Check(MedicineUnitCostCalculator.AllocateRemaining(0.01m,3,1)==0m,"Subcent lot share retained for later allocation");
        Check(MedicineUnitCostCalculator.AllocateRemaining(9999999999999999.99m,int.MaxValue,int.MaxValue)==9999999999999999.99m,"Final maximum amount exact");
        Reject(()=>MedicineUnitCostCalculator.AllocateRemaining(1.001m,3,1));Reject(()=>MedicineUnitCostCalculator.AllocateRemaining(1m,3,4));
        Reject(()=>MedicineUnitCostCalculator.AllocateRemaining(-1m,3,1));Reject(()=>MedicineUnitCostCalculator.AllocateRemaining(1m,0,1));
        var random=new Random(12029);
        for(int i=0;i<100;i++)
        {
            int count=random.Next(1,1000),left=count;decimal original=random.Next(0,100000)/100m,leftCost=original,sum=0;
            while(left>0){int take=random.Next(1,left+1);decimal allocated=MedicineUnitCostCalculator.AllocateRemaining(leftCost,left,take);sum+=allocated;leftCost-=allocated;left-=take;}
            Check(sum==original&&leftCost==0m,"Independent random cost conservation "+i);
        }
        Console.WriteLine("PASS medicine-unit invoice contracts and exact cost checks="+checks+"; no SQL.");return checks;
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using PharmacyManagement.Application;
using project_fo_3;
static class UnitPendingStoreProbe
{
    static int checks;static void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;}
    static bool Reject(Action action){try{action();return false;}catch(IOException){return true;}catch(InvalidDataException){return true;}catch(InvalidOperationException){return true;}catch(CryptographicException){return true;}}
    static string Key(string medicine,string user="cashier",string role="Manager",string employee="0007",string connection="owned.invalid/units_qa")=>MedicineUnitPendingStore.CreateContextKey(connection,user,role,employee,medicine);
    static MedicineUnitCatalogRequest Request(Guid id,string medicine)
    {
        var units=new List<MedicineUnitInput>();for(int i=0;i<100;i++)units.Add(new MedicineUnitInput{Id=Guid.NewGuid(),Name="وحدة مشكّلة 500 mg "+i,BaseUnitsPerUnit=i+1,SellingPrice=25.50m+i,BuyingPrice=12.25m+i});
        return new MedicineUnitCatalogRequest{RequestId=id,MedicineName=medicine,ExpectedRevision=0,BaseUnitId=units[0].Id,CurrencyCode="SAR",ConfirmInitialStockUnit=true,ExpectedInitialStockQuantity=40,Units=units.AsReadOnly()};
    }
    static int Main(string[] args)
    {
        try{
            Guid owner;Check(args.Length==3&&Guid.TryParseExact(args[1],"N",out owner),"Owned marker and mode required");
            string folder=Path.GetFullPath(args[0]);Check(folder.StartsWith("D:\\Comp\\Pharmacy-Management-master\\artifacts\\company-demo-20261008\\unit-pending-store-owned-",StringComparison.OrdinalIgnoreCase)&&File.ReadAllText(Path.Combine(folder,"owner.txt"))==args[1],"Only explicit owned artifact directory may be changed");
            string medicine="صنف تجريبي 500 mg",key=Key(medicine),file=Path.Combine(folder,key+".pending");
            Check(Key(medicine," CASHIER ","manager","0007")==key,"User/role whitespace and case do not lose recovery");
            if(args[2]=="write"){
                var request=Request(Guid.NewGuid(),medicine);var canonical=MedicineUnitCatalogValidation.Prepare(request).Payload;
                using(var store=new MedicineUnitPendingStore(folder,key,medicine)){
                    Check(store.Load()==null,"Initially no pending record");store.Save(request);var frozen=File.ReadAllBytes(file);
                    var loaded=store.Load();Check(loaded.RequestId==request.RequestId&&MedicineUnitCatalogValidation.Prepare(loaded).Payload.SequenceEqual(canonical),"100-unit Arabic/decimal/quantity request roundtrips exactly");
                    Check(Reject(()=>{using(var competing=new MedicineUnitPendingStore(folder,key,medicine)){};}),"Competing window cannot acquire the same request lease");
                    store.Save(request);Check(File.ReadAllBytes(file).SequenceEqual(frozen),"Same request does not replace encrypted record");
                    var changed=store.Load();changed.RequestId=Guid.NewGuid();Check(Reject(()=>store.Save(changed)),"Another request identity cannot replace unresolved request");
                    changed=store.Load();changed.Units[0].SellingPrice+=1;Check(Reject(()=>store.Save(changed)),"Changed content cannot replace unresolved request");
                    Check(File.ReadAllBytes(file).SequenceEqual(frozen),"Rejected replacement preserves original ciphertext");
                    Check(Reject(()=>store.Resolve(changed)),"Changed content cannot clear original request");
                    request.Units[0].Name="mutated caller";Check(MedicineUnitCatalogValidation.Prepare(store.Load()).Payload.SequenceEqual(canonical),"Store freezes mutable caller input");
                    File.WriteAllLines(Path.Combine(folder,"expected.txt"),new[]{loaded.RequestId.ToString("D"),Convert.ToBase64String(canonical)});
                }
                Console.WriteLine("PASS unit pending store write checks="+checks+"; record retained for independent process; SQL=False UI=False");
            }else if(args[2]=="read"){
                var expected=File.ReadAllLines(Path.Combine(folder,"expected.txt"));
                using(var store=new MedicineUnitPendingStore(folder,key,medicine)){
                    var restored=store.Load();Check(restored.RequestId==Guid.Parse(expected[0])&&Convert.ToBase64String(MedicineUnitCatalogValidation.Prepare(restored).Payload)==expected[1],"Independent process restores original identity and canonical bytes");
                    foreach(string other in new[]{Key(medicine,"other-user"),Key(medicine,role:"Cashier"),Key(medicine,employee:"0008"),Key("other-medicine"),Key(medicine,connection:"other.invalid/units_qa")}){
                        using(var isolated=new MedicineUnitPendingStore(folder,other,medicine))Check(isolated.Load()==null,"Another user/role/employee/medicine/target cannot see the request");
                    }
                    var frozen=File.ReadAllBytes(file);string foreign=Key(medicine,"foreign");string foreignFile=Path.Combine(folder,foreign+".pending");
                    try{File.WriteAllBytes(foreignFile,frozen);using(var foreignStore=new MedicineUnitPendingStore(folder,foreign,medicine))Check(Reject(()=>foreignStore.Load()),"Copied ciphertext fails the other context's encryption binding");}finally{File.Delete(foreignFile);}
                    using(var wrongMedicine=new MedicineUnitPendingStore(folder,Key("different"),"different"))Check(wrongMedicine.Load()==null,"Separate medicine has separate path");
                    foreach(int corruption in new[]{0,1,2}){
                        try{
                            byte[] broken=corruption==0?new byte[0]:corruption==1?(byte[])frozen.Clone():new byte[131073];if(corruption==1)broken[broken.Length/2]^=0x55;
                            File.WriteAllBytes(file,broken);Check(Reject(()=>store.Load()),"Corrupt/empty/oversized record fails closed");Check(File.ReadAllBytes(file).SequenceEqual(broken),"Failed read does not erase evidence");
                        }finally{File.WriteAllBytes(file,frozen);}
                    }
                    var wrong=store.Load();wrong.RequestId=Guid.NewGuid();Check(Reject(()=>store.Resolve(wrong)),"Unrelated acceptance cannot clear request");
                    store.Resolve(restored);Check(store.Load()==null&&!File.Exists(file),"Known matching outcome clears record");
                    store.Save(Request(Guid.NewGuid(),medicine));Check(store.Load()!=null,"A new request is allowed after resolution");store.Resolve(store.Load());store.Dispose();store.Dispose();
                    Check(Reject(()=>store.Load()),"Disposed store rejects further use");
                }
                Console.WriteLine("PASS unit pending store independent read/context/tamper/resolve checks="+checks+"; SQL=False UI=False");
            }else throw new Exception("Unsupported mode");
            return 0;
        }catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
}

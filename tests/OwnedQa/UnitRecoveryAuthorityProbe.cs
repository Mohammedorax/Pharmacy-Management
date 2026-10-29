using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using PharmacyManagement.Application;
using project_fo_3;
static class UnitRecoveryAuthorityProbe
{
    static readonly BindingFlags Flags=BindingFlags.NonPublic|BindingFlags.Instance;
    static int checks,failures;
    static object Field(object o,string n)=>o.GetType().GetField(n,Flags).GetValue(o);
    static void Check(bool ok,string message){checks++;if(!ok){failures++;Console.WriteLine("FAIL authority: "+message);}}
    static void Policy(bool allow){PermissionService.ClearCachedOverrides();bool value;PermissionService.TryGetCurrentUserOverride("Screen.ManageMedicines",out value,id=>new EmployeePermissionSnapshot{IsAvailable=true,Role="Cashier",Overrides=new Dictionary<string,bool>{{"Screen.ManageMedicines",allow}}});Check(PermissionService.CanOpen(AppPermission.ManageMedicines)==allow,"Real policy cache resolves requested grant/deny");}
    [STAThread]static int Main(string[] args){try{
        RequireOwnedPrivateDesktop.Assert();Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);AppFonts.Initialize();
        string folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"owned-authority-pending");string user="owned-unit-authority",employee="UNIT-AUTH",medicine="صنف صلاحية تجريبي";
        SessionContext.Start(user,"Cashier",employee);
        string key=MedicineUnitPendingStore.CreateContextKey(Database.ConnectionString,user,"Cashier",employee,medicine);Guid unit=Guid.NewGuid(),request=Guid.NewGuid();
        using(var store=new MedicineUnitPendingStore(folder,key,medicine))store.Save(new MedicineUnitCatalogRequest{RequestId=request,MedicineName=medicine,ExpectedRevision=0,BaseUnitId=unit,CurrencyCode="SAR",ConfirmInitialStockUnit=true,ExpectedInitialStockQuantity=40,Units=new[]{new MedicineUnitInput{Id=unit,Name="وحدة سعر محفوظ",BaseUnitsPerUnit=1,SellingPrice=777.50m,BuyingPrice=123.25m}}});
        string path=Path.Combine(folder,key+".pending");byte[] before=File.ReadAllBytes(path);
        Policy(false);
        using(var denied=new MedicineUnitCatalogEditor(medicine,false,false,folder)){
            denied.ShowInTaskbar=false;denied.Show();Application.DoEvents();
            Check(!(bool)Field(denied,"ready")&&Field(denied,"pending")==null,"Denied constructor must not load the private pending request");
            Check(((DataGridView)Field(denied,"grid")).Rows.Count==0,"Denied constructor must not display stored prices");
            Check(!((Control)Field(denied,"save")).Enabled,"Denied restore must not enable verify/save");denied.Close();
        }
        Check(File.ReadAllBytes(path).SequenceEqual(before),"Denied opening preserves original encrypted request");
        foreach(bool relogin in new[]{false,true}){
            SessionContext.Start(user,"Cashier",employee);Policy(true);
            using(var form=new MedicineUnitCatalogEditor(medicine,false,false,folder)){
                form.ShowInTaskbar=false;form.Show();Application.DoEvents();
                Check((bool)Field(form,"ready")&&((DataGridView)Field(form,"grid")).Rows.Count==1,"Authorized user restores pending data");
                if(relogin)SessionContext.Start(user,"Cashier",employee);
                Policy(false);typeof(Form).GetMethod("OnActivated",Flags).Invoke(form,new object[]{EventArgs.Empty});Application.DoEvents();
                Check(((DataGridView)Field(form,"grid")).Rows.Count==0&&((Label)Field(form,"header")).Text.Length==0,"Revocation/relogin must clear visible previous prices and header");
                Check(!((Control)Field(form,"save")).Enabled,"Revoked context cannot verify/post");
                Check(((MedicineUnitCatalogRequest)Field(form,"pending")).RequestId==request,"Private pending identity retained for original authorized context");form.Close();
            }
            Check(File.ReadAllBytes(path).SequenceEqual(before),"Revocation does not erase or replace durable request");
        }
        SessionContext.Start(user,"Cashier",employee);Policy(true);
        using(var restored=new MedicineUnitCatalogEditor(medicine,false,false,folder)){Check(((MedicineUnitCatalogRequest)Field(restored,"pending")).RequestId==request,"Later authorized window can recover original request");}
        Console.WriteLine("RESULT unit recovery authority checks="+checks+"; failures="+failures+"; SQL=False NativeInput=False PhysicalPrint=False");return failures==0?0:1;
    }catch(Exception e){Console.Error.WriteLine(e);return 1;}finally{SessionContext.Clear();}}
}

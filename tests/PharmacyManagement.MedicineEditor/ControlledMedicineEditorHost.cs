using System;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.Collections.Generic;
using PharmacyManagement.Application;

// External test host: actual Medicine form and SessionContext, pending fake service, no SQL or authentication claim.
class ControlledMedicineEditorHost {
    static BindingFlags stat=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    static BindingFlags ins=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    [STAThread] static int Main(string[] args) {
        Console.OutputEncoding=System.Text.Encoding.UTF8;
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        string directory=AppDomain.CurrentDomain.BaseDirectory;
        var assembly=Assembly.LoadFrom(System.IO.Path.Combine(directory,"project_fo_3.exe"));
        var session=assembly.GetType("project_fo_3.SessionContext");
        var clear=session.GetMethod("Clear",stat);
        try {
            bool english=args.Any(arg=>arg.IndexOf("english",StringComparison.OrdinalIgnoreCase)>=0);
            bool dark=args.Any(arg=>arg.IndexOf("dark",StringComparison.OrdinalIgnoreCase)>=0);
            assembly.GetType("project_fo_3.AppFonts").GetMethod("Initialize",stat,null,Type.EmptyTypes,null).Invoke(null,null);
            var settingsType=assembly.GetType("project_fo_3.Properties.Setings");
            object settings=settingsType.GetProperty("Default",stat).GetValue(null,null);
            settingsType.GetProperty("ThemeMode",ins).SetValue(settings,dark?"Dark":"Light",null);
            session.GetMethod("Start",stat).Invoke(null,new object[]{"medicine-native-controlled","Manager","NATIVE-TEST"});
            var fixture=assembly.GetType("project_fo_3.ResponsiveLayoutSelfTests+PendingMedicineService");
            object service=Activator.CreateInstance(fixture,true);
            var formType=assembly.GetType("project_fo_3.Medicine");
            using(var form=(Form)Activator.CreateInstance(formType,ins,null,new object[]{false,english,service,new Func<bool>(()=>false),new Action<string>(text=>{})},null))
            using(var timer=new Timer()) {
                var medicines=(List<TaskCompletionSource<IReadOnlyList<MedicineRecord>>>)fixture.GetField("Medicines",ins).GetValue(service);
                var companies=(List<TaskCompletionSource<IReadOnlyList<string>>>)fixture.GetField("Companies",ins).GetValue(service);
                var row=new MedicineRecord{MedName="باراسيتامول 500 mg",Barcode="6290000000001",BuyingPrice=800,SellingPrice=1000,Quantity=24,ProductionDate=new DateTime(2026,1,1),ExpiryDate=new DateTime(2028,1,1),CompanyName="شركة محفوظة — Pharma 500"};
                int medicineRequest=0,companyRequest=0;
                timer.Interval=25;
                timer.Tick+=(sender,e)=>{
                    while(companyRequest<companies.Count)companies[companyRequest++].SetResult(new[]{"شركة محفوظة — Pharma 500"});
                    while(medicineRequest<medicines.Count)medicines[medicineRequest++].SetResult(new[]{row});
                };
                form.StartPosition=FormStartPosition.CenterScreen;form.Size=new Size(1024,768);
                timer.Start();Application.Run(form);timer.Stop();
                if((int)fixture.GetField("Creates",ins).GetValue(service)!=0 ||
                   (int)fixture.GetField("Updates",ins).GetValue(service)!=0 ||
                   (int)fixture.GetField("Deletes",ins).GetValue(service)!=0)
                   throw new Exception("Native editor driver invoked a mutation.");
                System.IO.File.WriteAllText(System.IO.Path.Combine(directory,"native-host-"+(english?"english":"arabic")+"-"+(dark?"dark":"light")+".json"), "{\"Passed\":true,\"Creates\":0,\"Updates\":0,\"Deletes\":0,\"AuthenticationExercised\":false,\"SqlExercised\":false}");
                Console.WriteLine("PASS controlled native editor host: zero SQL and zero mutations; actual SessionContext/form, fake service; authentication not exercised.");
            }
            return 0;
        } catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
        finally{clear.Invoke(null,null);}
    }
}

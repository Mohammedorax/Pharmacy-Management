using System;using System.IO;using System.Reflection;using System.Windows.Forms;
class ReceiptThemeDirectionTests {
 const BindingFlags All=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;static int checks;
 static void Check(bool value,string message){if(!value)throw new Exception(message);checks++;}
 [STAThread]static int Main(string[] args){object settings=null;PropertyInfo theme=null;string old=null;
 try{
  Console.OutputEncoding=new System.Text.UTF8Encoding(false);Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
  string folder=Path.GetDirectoryName(Path.GetFullPath(args[0])),fallback=Path.GetFullPath(args[1]);
  AppDomain.CurrentDomain.AssemblyResolve+=(sender,e)=>{string n=new AssemblyName(e.Name).Name+".dll",p=Path.Combine(folder,n);if(!File.Exists(p))p=Path.Combine(fallback,n);return File.Exists(p)?Assembly.LoadFrom(p):null;};
  foreach(string n in new[]{"PharmacyManagement.Domain","PharmacyManagement.Application","PharmacyManagement.Infrastructure.SqlServer"})Assembly.LoadFrom(Path.Combine(folder,n+".dll"));
  var app=Assembly.LoadFrom(args[0]);var settingType=app.GetType("project_fo_3.Properties.Setings",true);settings=settingType.GetProperty("Default",All).GetValue(null);theme=settingType.GetProperty("ThemeMode",All);old=(string)theme.GetValue(settings);
  var receipt=app.GetType("project_fo_3.ReceiptVoucherForm",true);var direction=app.GetType("project_fo_3.UiDirection",true).GetMethod("Apply",All,null,new[]{typeof(Form),typeof(bool)},null);var palette=app.GetType("project_fo_3.UiTheme",true).GetMethod("Apply",All,null,new[]{typeof(Form)},null);var caption=receipt.GetMethod("ApplyReceiptCurrencyPresentation",All);
  foreach(string initial in new[]{"Light","Dark"})foreach(bool english in new[]{false,true})foreach(string kind in new[]{"ReceiptExternal","PaymentExternal"}){
   theme.SetValue(settings,initial);
   using(var form=(Form)Activator.CreateInstance(receipt,All,null,new object[]{kind,true},null)){
    direction.Invoke(null,new object[]{form,english});caption.Invoke(form,new object[]{english});form.StartPosition=FormStartPosition.Manual;form.Location=new System.Drawing.Point(-32000,-32000);form.ShowInTaskbar=false;form.Opacity=0;form.Show();Application.DoEvents();
    var bank=(TextBox)form.Controls.Find("receiptVoucherChequeBank",true)[0];bank.Text="المورد";
    int created=0,destroyed=0;form.HandleCreated+=(s,e)=>created++;form.HandleDestroyed+=(s,e)=>destroyed++;
    foreach(string changed in new[]{initial=="Light"?"Dark":"Light",initial}){
     theme.SetValue(settings,changed);palette.Invoke(null,new object[]{form});Application.DoEvents();
     Check(form.RightToLeft==(english?RightToLeft.No:RightToLeft.Yes)&&form.RightToLeftLayout==!english,"Theme reset explicit receipt direction.");
     Check(created==0&&destroyed==0,"Theme recreated receipt HWND without a language change.");
     Check(bank.Text=="المورد","Theme translated a raw bank value.");
     var label=(Label)form.Controls.Find("receiptVoucherLocalValueCaption",true)[0];Check(label.Text.Contains(english?"Base amount":"قيمة النظام"),"Theme reset explicit currency caption language.");
    }
   }
  }
  var language=app.GetType("project_fo_3.UiLanguageCatalog",true).GetProperty("IsEnglishActive",All);
  bool globalEnglish=(bool)language.GetValue(null);
  foreach(string initial in new[]{"Light","Dark"})foreach(string kind in new[]{"ReceiptExternal","PaymentExternal"}){
   theme.SetValue(settings,initial);
   using(var form=(Form)Activator.CreateInstance(receipt,All,null,new object[]{kind,true},null)){
    Check(receipt.GetProperty("ReceiptPresentationDirection",All).GetValue(form)==null,"Default receipt unexpectedly acquired an explicit direction.");
    palette.Invoke(null,new object[]{form});
    Check(form.RightToLeft==(globalEnglish?RightToLeft.No:RightToLeft.Yes)&&form.RightToLeftLayout==!globalEnglish,"Default receipt diverged from global direction.");
   }
  }
  if(args.Length>2&&args[2]=="full"){
   var matrix=app.GetType("project_fo_3.ResponsiveLayoutSelfTests",true);
   foreach(string name in new[]{"VerifyReceiptSidebarNativeOnly","VerifyReceiptNativeViewportOnly","VerifyReceiptCurrencyCaptionOnly"}){Console.WriteLine("BEGIN focused "+name);matrix.GetMethod(name,All).Invoke(null,null);Console.WriteLine("PASS focused "+name);}
  }
  Check(Application.OpenForms.Count==0,"Owned test form remained open.");Console.WriteLine("PASS receipt theme direction: "+checks+" checks; eight owned theme/direction/type cases, two live switches each; no HWND recreation, raw known-key bank value and local currency caption retained; no SQL/native input.");return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}finally{if(theme!=null&&old!=null)theme.SetValue(settings,old);}}
}
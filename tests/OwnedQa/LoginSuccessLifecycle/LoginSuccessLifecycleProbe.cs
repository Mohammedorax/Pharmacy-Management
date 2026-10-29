using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Diagnostics;
using System.Windows.Forms;
using project_fo_3;
static class LoginSuccessLifecycleProbe
{
    const string Password = "OwnedLifecycleDummy-2026!";
    static readonly BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    static int checks, gaps;
    static void Check(bool condition,string message){checks++;if(!condition)throw new Exception(message);}
    static void Pump(int ms){var w=Stopwatch.StartNew();while(w.ElapsedMilliseconds<ms){Application.DoEvents();Thread.Sleep(5);}}
    static T Field<T>(Login f,string name){return (T)typeof(Login).GetField(name,Hidden).GetValue(f);}
    [STAThread] static int Main(string[] args)
    {
        try{
            RequireOwnedPrivateDesktop.Assert();Console.OutputEncoding=new UTF8Encoding(false);
            Check(args.Length==3,"database/mode/owned folder required");
            Check(args[1]=="Baseline"||args[1]=="Fixed","Known mode required");
            var cs=new System.Data.SqlClient.SqlConnectionStringBuilder(Database.ConnectionString);
            Check(cs.InitialCatalog==args[0]&&args[0].StartsWith("PharmacyLoginLifecycle_")&&args[0].EndsWith("_qa"),"Exact isolated database required");
            string store=(string)typeof(RememberMeService).GetMethod("GetDataPath",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{"remembered-user.dat"});
            Check(Path.GetFileName(Path.GetDirectoryName(store))==args[2]&&args[2].StartsWith("PharmacyManagement-QaLifecycle-"),"Only instrumented owned remember folder allowed");
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);AppFonts.Initialize();
            Exception threadError=null;Application.ThreadException+=(s,e)=>threadError=e.Exception;
            int cases=0;
            foreach(string role in new[]{"Manager","Cashier"})foreach(bool remember in new[]{false,true})foreach(bool reveal in new[]{false,true}){
                string user="lifecycle_"+role.ToLowerInvariant();
                RememberMeService.Clear();SessionContext.Clear();
                project_fo_3.Properties.Setings.Default.ThemeMode=reveal?"Dark":"Light";
                using(var login=new Login(false,false,reveal))using(var timer=new System.Windows.Forms.Timer{Interval=30}){
                    login.ShowInTaskbar=false;login.StartPosition=FormStartPosition.Manual;login.Location=new System.Drawing.Point(-20000,-20000);login.Show();
                    var name=Field<ComboBox>(login,"username");var pass=Field<TextBox>(login,"password");var option=Field<CheckBox>(login,"chkRememberMe");
                    name.Text=user;pass.Text=Password;option.Checked=remember;
                    if(reveal)typeof(Login).GetMethod("TogglePasswordVisibility",Hidden).Invoke(login,new object[]{null,EventArgs.Empty});
                    bool reached=false,emptyAtHome=false,returned=false;Exception callbackError=null;
                    var wait=Stopwatch.StartNew();
                    timer.Tick+=(s,e)=>{
                        try{
                            var home=Application.OpenForms.Cast<Form>().OfType<Home>().FirstOrDefault();
                            if(home!=null&&home.Visible){
                                timer.Stop();Check(SessionContext.IsAuthenticated&&SessionContext.CurrentUser==user&&SessionContext.CurrentRole==role,"Actual Login authenticated expected SQL actor");
                                reached=true;emptyAtHome=pass.Text.Length==0;
                                Check(remember?File.Exists(store):!File.Exists(store),"Actual remember choice persisted");
                                if(remember){
                                    string payload=Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(store),null,DataProtectionScope.CurrentUser));
                                    Check(payload=="v3\n"+Convert.ToBase64String(Encoding.UTF8.GetBytes(user)),"DPAPI payload contains username only");
                                }
                                home.Close();
                            }else if(wait.ElapsedMilliseconds>30000){timer.Stop();callbackError=new Exception("Original Login did not reach Home");login.Close();}
                        }catch(Exception ex){callbackError=ex;timer.Stop();foreach(var f in Application.OpenForms.Cast<Form>().Where(f=>f!=login).ToArray())f.Close();}
                    };
                    timer.Start();typeof(Login).GetMethod("button1_Click_1",Hidden).Invoke(login,new object[]{null,EventArgs.Empty});
                    while(!reached&&callbackError==null&&wait.ElapsedMilliseconds<32000)Pump(20);
                    Pump(100);timer.Stop();if(callbackError!=null)throw callbackError;if(threadError!=null)throw threadError;
                    Check(reached,"Successful real Login opened Home");
                    returned=login.Visible&&!SessionContext.IsAuthenticated;
                    Check(returned,"Home close returned to Login and cleared session");
                    bool cleared=emptyAtHome&&pass.Text.Length==0;
                    if(!cleared)gaps++;
                    Check(args[1]=="Baseline"?!cleared:cleared,"Expected baseline retained password / fixed cleared password");
                    if(args[1]=="Fixed")Check(pass.PasswordChar==DesktopDesignTokens.PasswordMaskCharacter&&!Field<bool>(login,"isPasswordVisible"),"Successful Login resets password visibility");
                    Console.WriteLine("CASE role="+role+" remember="+remember+" revealed="+reveal+" emptyAtHome="+emptyAtHome+" emptyOnReturn="+(pass.Text.Length==0)+" sessionCleared="+!SessionContext.IsAuthenticated+"; actual SQL, framework event, no native input");
                    login.Close();
                }
                using(var reopened=new Login(false,false,reveal)){
                    Check(Field<ComboBox>(reopened,"username").Text==(remember?user:string.Empty),"Reopened Login restores only chosen username");
                    Check(Field<TextBox>(reopened,"password").Text.Length==0,"Reopened Login password empty");
                    Check(Field<CheckBox>(reopened,"chkRememberMe").Checked==remember,"Reopened remember choice");
                }
                RememberMeService.Clear();Check(!File.Exists(store),"Owned remembered file cleared");cases++;
            }
            Check(cases==8&&gaps==(args[1]=="Baseline"?8:0),"Exact lifecycle matrix result");
            Console.WriteLine("PASS mode="+args[1]+" cases="+cases+" checks="+checks+" productGaps="+gaps+"; source fixture changes only remember folder constant; production profile untouched");return 0;
        }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}finally{SessionContext.Clear();}
    }
}

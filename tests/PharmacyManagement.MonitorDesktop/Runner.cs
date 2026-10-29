using System;using System.IO;using System.Reflection;using System.Windows.Forms;
class Runner
{
 [STAThread]static int Main(string[] args)
 {
  Console.OutputEncoding=System.Text.Encoding.UTF8;
  Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
  try
  {
   var main=typeof(project_fo_3.SystemMonitorForm).Assembly;
   if(args.Length<1||!string.Equals(Path.GetFullPath(args[0]),main.Location,StringComparison.OrdinalIgnoreCase))
    throw new Exception("Selected application does not match the loaded production form.");
   if(main.GetType("project_fo_3.MonitorDesktopProbe",false)!=null)
    throw new Exception("Shipping application still contains the monitor fixture.");
   typeof(project_fo_3.MonitorDesktopProbe).GetMethod(args.Length>1?args[1]:"Run",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,null);
   Console.WriteLine("MonitorFixtureInShippingAssembly=False; ActualFormFromSelectedMain=True");
   return 0;
  }
  catch(Exception e){Console.Error.WriteLine(e);return 1;}
 }
}

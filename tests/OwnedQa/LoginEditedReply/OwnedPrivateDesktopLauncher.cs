using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
internal static class OwnedPrivateDesktopLauncher
{
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct StartupInfo {
        public int cb;public string reserved,desktop,title;public uint x,y,xSize,ySize,xCount,yCount,fill,flags;
        public short show,reservedBytes;public IntPtr reservedPointer,input,output,error;
    }
    [StructLayout(LayoutKind.Sequential)] struct ProcessInfo {public IntPtr process,thread;public uint processId,threadId;}
    [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern IntPtr CreateDesktopW(string name,IntPtr device,IntPtr mode,uint flags,uint access,IntPtr security);
    [DllImport("user32.dll",SetLastError=true)] static extern bool CloseDesktop(IntPtr desktop);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern bool CreateProcessW(string application,StringBuilder command,IntPtr processSecurity,IntPtr threadSecurity,bool inherit,uint flags,IntPtr environment,string directory,ref StartupInfo startup,out ProcessInfo process);
    [DllImport("kernel32.dll",SetLastError=true)] static extern uint WaitForSingleObject(IntPtr handle,uint timeout);
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool GetExitCodeProcess(IntPtr process,out uint code);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
    static string Json(string value)=>"\""+value.Replace("\\","\\\\").Replace("\"","\\\"")+"\"";
    static string Quote(string value){
        var text=new StringBuilder("\"");int slashes=0;
        foreach(char c in value){if(c=='\\'){slashes++;continue;}if(c=='\"'){text.Append('\\',slashes*2+1);text.Append(c);}else{text.Append('\\',slashes);text.Append(c);}slashes=0;}
        text.Append('\\',slashes*2);return text.Append('"').ToString();
    }
    static int Main(string[] args){
        IntPtr desktop=IntPtr.Zero;ProcessInfo info=default(ProcessInfo);bool childCompleted=false;
        try{
            if(args.Length!=2)throw new ArgumentException("Owned script and evidence directory required.");
            string root=Path.GetFullPath(args[1]);string script=Path.GetFullPath(args[0]);
            string allowed="D:\\Comp\\Pharmacy-Management-master\\artifacts\\company-demo-20261008\\";
            if(!root.StartsWith(allowed,StringComparison.OrdinalIgnoreCase)||script!=Path.Combine(root,"Run-Isolated.ps1")||!File.Exists(script))throw new InvalidOperationException("Owned scoped wrapper required.");
            string name="PharmacyQa_"+Guid.NewGuid().ToString("N");
            desktop=CreateDesktopW(name,IntPtr.Zero,IntPtr.Zero,0,0x1ff,IntPtr.Zero);
            if(desktop==IntPtr.Zero)throw new Win32Exception();
            Environment.SetEnvironmentVariable("PHARMACY_QA_DESKTOP_NAME",name);
            string application="C:\\Program Files\\PowerShell\\7\\pwsh.exe";
            var startup=new StartupInfo{cb=Marshal.SizeOf(typeof(StartupInfo)),desktop="winsta0\\"+name,flags=1,show=0};
            var command=new StringBuilder(Quote(application)+" -NoProfile -File "+Quote(script));
            if(!CreateProcessW(application,command,IntPtr.Zero,IntPtr.Zero,false,0x08000000,IntPtr.Zero,root,ref startup,out info))throw new Win32Exception();
            using(var child=Process.GetProcessById((int)info.processId)){
                File.WriteAllText(Path.Combine(root,"desktop-owner.json"),"{\"LauncherProcessId\":"+Process.GetCurrentProcess().Id+",\"ChildProcessId\":"+info.processId+",\"ChildStartUtcTicks\":"+child.StartTime.ToUniversalTime().Ticks+",\"DesktopName\":"+Json(name)+",\"SwitchedInputDesktop\":false,\"InheritedHandles\":false}",Encoding.UTF8);
            }
            Console.WriteLine("START owned private desktop child pid="+info.processId+"; desktop="+name+"; input desktop unchanged.");
            while(true){uint wait=WaitForSingleObject(info.process,1000);if(wait==0)break;if(wait!=258)throw new Win32Exception();}
            childCompleted=true;uint code;if(!GetExitCodeProcess(info.process,out code))throw new Win32Exception();
            CloseHandle(info.thread);info.thread=IntPtr.Zero;CloseHandle(info.process);info.process=IntPtr.Zero;
            bool closed=CloseDesktop(desktop);if(closed)desktop=IntPtr.Zero;
            File.WriteAllText(Path.Combine(root,"desktop-terminal.json"),"{\"State\":"+Json(code==0?"completed":"failed")+",\"ChildExitCode\":"+code+",\"DesktopHandleClosed\":"+closed.ToString().ToLowerInvariant()+",\"SwitchedInputDesktop\":false,\"FullPlanComplete\":false}",Encoding.UTF8);
            Console.WriteLine("END owned private desktop; childExit="+code+"; desktopHandleClosed="+closed);return (int)code;
        }catch(Exception error){Console.Error.WriteLine(error);return 1;}
        finally{
            if(info.thread!=IntPtr.Zero)CloseHandle(info.thread);
            if(info.process!=IntPtr.Zero)CloseHandle(info.process);
            // Never close an owned desktop while an observed child is still running.
            if(desktop!=IntPtr.Zero&&(childCompleted||info.processId==0))CloseDesktop(desktop);
        }
    }
}

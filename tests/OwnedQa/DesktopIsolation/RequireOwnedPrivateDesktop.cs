internal static class RequireOwnedPrivateDesktop
{
    [System.Runtime.InteropServices.DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
    [System.Runtime.InteropServices.DllImport("user32.dll",SetLastError=true)] static extern System.IntPtr GetThreadDesktop(uint thread);
    [System.Runtime.InteropServices.DllImport("user32.dll",CharSet=System.Runtime.InteropServices.CharSet.Unicode,SetLastError=true)] static extern bool GetUserObjectInformationW(System.IntPtr handle,int index,System.Text.StringBuilder text,uint length,out uint needed);
    internal static void Assert(){
        string expected=System.Environment.GetEnvironmentVariable("PHARMACY_QA_DESKTOP_NAME");
        if(string.IsNullOrWhiteSpace(expected)||!System.Text.RegularExpressions.Regex.IsMatch(expected,"^PharmacyQa_[a-f0-9]{32}$"))throw new System.InvalidOperationException("Owned private desktop expectation missing.");
        var name=new System.Text.StringBuilder(256);uint needed;
        if(!GetUserObjectInformationW(GetThreadDesktop(GetCurrentThreadId()),2,name,512,out needed))throw new System.ComponentModel.Win32Exception();
        if(name.ToString()!=expected)throw new System.InvalidOperationException("Fixture is not on expected private desktop: "+name);
        System.Console.WriteLine("PASS actual private thread desktop="+name+"; no input desktop switch.");
    }
}

using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using PharmacyManagement.Application;
using project_fo_3;

// Characterization only. No successful authentication, SQL, or remembered-user access.
// The preview constructor skips storage; only its preview flag is then changed so
// the real submit handler can await an owned, explicitly delayed collaborator.
internal static class LoginCloseDuringAuthenticationProbe
{
    private sealed class DelayedAuthentication : IAuthenticationService
    {
        internal readonly ManualResetEventSlim Entered = new ManualResetEventSlim();
        internal readonly ManualResetEventSlim Release = new ManualResetEventSlim();
        internal int Calls;
        internal string Mode;
        public AuthenticationResult Authenticate(AuthenticationRequest request)
        {
            Interlocked.Increment(ref Calls);
            Entered.Set();
            if (!Release.Wait(TimeSpan.FromSeconds(15))) throw new TimeoutException("Owned authentication release missing.");
            if (Mode == "Exception") throw new InvalidOperationException("Owned delayed authentication failure.");
            return AuthenticationResult.Failed(Mode == "Invalid" ? AuthenticationStatus.InvalidCredentials : AuthenticationStatus.ManagerSetupRequired);
        }
    }

    private delegate bool EnumWindow(IntPtr window, IntPtr parameter);
    private sealed class CompletionContext : SynchronizationContext
    {
        private readonly SynchronizationContext inner;
        internal int Started;
        internal int Completed;
        internal CompletionContext(SynchronizationContext inner) { this.inner = inner ?? throw new InvalidOperationException("UI synchronization context missing."); }
        public override void Post(SendOrPostCallback callback, object state) { inner.Post(callback, state); }
        public override void OperationStarted() { Interlocked.Increment(ref Started); }
        public override void OperationCompleted() { Interlocked.Increment(ref Completed); }
    }
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindow callback, IntPtr parameter);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr window, StringBuilder name, int count);
    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            RequireOwnedPrivateDesktop.Assert(); Application.EnableVisualStyles();
            AppFonts.Initialize();
            var fake = new DelayedAuthentication { Mode = args.Length == 0 ? "Setup" : args[0] };
            var serviceField = typeof(ApplicationServices).GetField("Authentication", BindingFlags.Static | BindingFlags.NonPublic);
            if (serviceField == null) throw new InvalidOperationException("Authentication field missing.");
            serviceField.SetValue(null, fake);
            if (!ReferenceEquals(serviceField.GetValue(null), fake)) throw new InvalidOperationException("Owned collaborator was not installed.");
            using (var context = new ApplicationContext())
            using (var login = new Login(false, true, false))
            using (var timer = new System.Windows.Forms.Timer { Interval = 50 })
            {
                login.ShowInTaskbar = false;
                login.StartPosition = FormStartPosition.Manual;
                login.Location = new System.Drawing.Point(-20000, -20000);
                login.Show();
                var completion = new CompletionContext(SynchronizationContext.Current);
                typeof(Login).GetField("previewMode", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(login, false);
                bool closedBeforeRelease = false;
                int staleDialogs = 0;
                int ticks = 0;
                Exception asynchronousError = null;
                ThreadExceptionEventHandler onError = (sender, args) => { asynchronousError = args.Exception; context.ExitThread(); };
                Application.ThreadException += onError;
                timer.Tick += (sender, args) =>
                {
                    ticks++;
                    if (fake.Entered.IsSet && !closedBeforeRelease)
                    {
                        login.Close();
                        closedBeforeRelease = login.IsDisposed;
                        if (!closedBeforeRelease) { asynchronousError = new Exception("Owned login did not close."); context.ExitThread(); return; }
                        fake.Release.Set();
                    }
                    if (closedBeforeRelease)
                    {
                        EnumWindows((window, parameter) =>
                        {
                            uint process;
                            GetWindowThreadProcessId(window, out process);
                            if (process != (uint)Process.GetCurrentProcess().Id) return true;
                            var className = new StringBuilder(128);
                            GetClassName(window, className, className.Capacity);
                            if (className.ToString() != "#32770") return true;
                            staleDialogs++;
                            // Dismiss only this fixture process's observed stale message box.
                            PostMessage(window, 0x0010, IntPtr.Zero, IntPtr.Zero);
                            return true;
                        }, IntPtr.Zero);
                        if (staleDialogs > 0 || (ticks > 80 && completion.Completed == 1)) context.ExitThread();
                    }
                    if (ticks > 200) { asynchronousError = new TimeoutException("Owned characterization timed out."); context.ExitThread(); }
                };
                timer.Start();
                SynchronizationContext.SetSynchronizationContext(completion);
                typeof(Login).GetMethod("button1_Click_1", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(login, new object[] { login, EventArgs.Empty });
                Application.Run(context);
                timer.Stop();
                Application.ThreadException -= onError;
                fake.Release.Set();
                if (asynchronousError != null) throw asynchronousError;
                if (fake.Calls != 1 || !closedBeforeRelease) throw new Exception("Characterization prerequisites were not satisfied.");
                if (staleDialogs == 0 && (completion.Started != 1 || completion.Completed != 1)) throw new Exception("Submit handler completion not observed.");
                Console.WriteLine("OBSERVED Mode=" + fake.Mode + "; ClosedBeforeAuthenticationReturn=true; Calls=1; StaleDialogs=" + staleDialogs + "; SessionAuthenticated=" + SessionContext.IsAuthenticated);
                Console.WriteLine("HANDLER Started=" + completion.Started + "; Completed=" + completion.Completed);
                Console.WriteLine(staleDialogs > 0 ? "GAP_REPRODUCED stale authentication UI after login closed" : "NO_STALE_DIALOG_OBSERVED within owned observation window");
                return staleDialogs == 0 ? 0 : 1;
            }
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        finally { SessionContext.Clear(); }
    }
}

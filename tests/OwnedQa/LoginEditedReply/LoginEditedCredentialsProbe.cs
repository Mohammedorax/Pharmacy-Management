using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using PharmacyManagement.Application;
using project_fo_3;
static class LoginEditedCredentialsProbe
{
    sealed class Delayed : IAuthenticationService
    {
        internal readonly ManualResetEventSlim Entered = new ManualResetEventSlim(), Release = new ManualResetEventSlim();
        internal int Calls;
        internal string Outcome;
        public AuthenticationResult Authenticate(AuthenticationRequest request)
        {
            Interlocked.Increment(ref Calls); Entered.Set();
            if (!Release.Wait(TimeSpan.FromSeconds(15))) throw new TimeoutException("Owned release missing");
            if (Outcome == "Exception") throw new InvalidOperationException("Owned authentication failure");
            return AuthenticationResult.Failed(Outcome == "Setup" ? AuthenticationStatus.ManagerSetupRequired : AuthenticationStatus.InvalidCredentials);
        }
    }
    sealed class Completion : SynchronizationContext
    {
        readonly SynchronizationContext inner;
        internal int Started, Completed;
        internal Completion(SynchronizationContext inner) { this.inner = inner ?? throw new Exception("UI context missing"); }
        public override void Post(SendOrPostCallback callback, object state) { inner.Post(callback, state); }
        public override void OperationStarted() { Interlocked.Increment(ref Started); }
        public override void OperationCompleted() { Interlocked.Increment(ref Completed); }
    }
    delegate bool EnumWindow(IntPtr window, IntPtr parameter);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindow callback, IntPtr parameter);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr window, StringBuilder name, int count);
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
    static readonly BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;
    [STAThread] static int Main(string[] args)
    {
        try
        {
            RequireOwnedPrivateDesktop.Assert(); Console.OutputEncoding = new UTF8Encoding(false);
            string mode = args.Length == 0 ? "Password" : args[0];
            string outcome = args.Length < 2 ? "Invalid" : args[1];
            if (mode != "Password" && mode != "Username" && mode != "Both" && mode != "Unchanged") throw new Exception("Known owned mode required");
            if (outcome != "Invalid" && outcome != "Exception" && outcome != "Setup") throw new Exception("Known failure outcome required");
            Application.EnableVisualStyles(); AppFonts.Initialize(); SessionContext.Clear();
            var fake = new Delayed { Outcome = outcome };
            var service = typeof(ApplicationServices).GetField("Authentication", BindingFlags.Static | BindingFlags.NonPublic);
            service.SetValue(null, fake);
            using (var context = new ApplicationContext())
            using (var login = new Login(false, true, false))
            using (var timer = new System.Windows.Forms.Timer { Interval = 30 })
            {
                var username = (ComboBox)typeof(Login).GetField("username", Hidden).GetValue(login);
                var password = (TextBox)typeof(Login).GetField("password", Hidden).GetValue(login);
                username.Text = "owned-old-user"; password.Text = "owned-old-dummy";
                login.ShowInTaskbar = false; login.StartPosition = FormStartPosition.Manual; login.Location = new System.Drawing.Point(-20000, -20000); login.Show();
                typeof(Login).GetField("previewMode", Hidden).SetValue(login, false);
                var completion = new Completion(SynchronizationContext.Current);
                bool changed = false, captured = false, retained = false, nameRetained = false, passwordRetained = false, authenticated = false;
                int ticks = 0, dialogs = 0, throttle = -1, nameLength = -1, passwordLength = -1; Exception asynchronousError = null;
                string expectedName = mode == "Password" || mode == "Unchanged" ? "owned-old-user" : "owned-new-user";
                string expectedPassword = mode == "Username" || mode == "Unchanged" ? "owned-old-dummy" : "owned-new-dummy";
                ThreadExceptionEventHandler onError = (sender, eventArgs) => { asynchronousError = eventArgs.Exception; context.ExitThread(); };
                Application.ThreadException += onError;
                timer.Tick += (sender, eventArgs) =>
                {
                    ticks++;
                    if (fake.Entered.IsSet && !changed)
                    {
                        username.Text = expectedName; password.Text = expectedPassword; changed = true; fake.Release.Set();
                    }
                    if (changed)
                    {
                        EnumWindows((window, parameter) =>
                        {
                            uint process; GetWindowThreadProcessId(window, out process);
                            if (process != (uint)Process.GetCurrentProcess().Id) return true;
                            var name = new StringBuilder(128); GetClassName(window, name, name.Capacity);
                            if (name.ToString() == "#32770") { dialogs++; PostMessage(window, 0x0010, IntPtr.Zero, IntPtr.Zero); }
                            return true;
                        }, IntPtr.Zero);
                        if (completion.Completed == 1 && ticks > 15)
                        {
                            if (login.IsDisposed || login.Disposing) { asynchronousError = new Exception("Login ended before live reply boundary"); context.ExitThread(); return; }
                            nameRetained = username.Text == expectedName; passwordRetained = password.Text == expectedPassword;
                            retained = nameRetained && passwordRetained; nameLength = username.Text.Length; passwordLength = password.Text.Length;
                            throttle = (int)typeof(Login).GetField("consecutiveFailedLoginAttempts", Hidden).GetValue(login);
                            authenticated = SessionContext.IsAuthenticated; captured = true;
                            context.ExitThread();
                        }
                    }
                    if (ticks > 400) { asynchronousError = new TimeoutException("Owned submit observation timed out"); context.ExitThread(); }
                };
                SynchronizationContext.SetSynchronizationContext(completion); timer.Start();
                typeof(Login).GetMethod("button1_Click_1", Hidden).Invoke(login, new object[] { login, EventArgs.Empty });
                Application.Run(context); timer.Stop(); Application.ThreadException -= onError; fake.Release.Set();
                if (asynchronousError != null) throw asynchronousError;
                if (!changed || !captured || fake.Calls != 1 || completion.Started != 1 || completion.Completed != 1) throw new Exception("Actual live submit boundary not observed");
                Console.WriteLine("RESULT edited login Mode=" + mode + "; Calls=1 Started=1 Completed=1 LatestInputsRetained=" + retained + "; FailureThrottle=" + throttle + "; ObservedOwnedDialogs=" + dialogs + "; Authenticated=" + authenticated + "; SuccessfulAuthentication=False RememberStoreAccess=False SqlUsed=False NativeInput=False");
                Console.WriteLine("INPUT_BOUNDARY LiveBeforeTeardown=True NameRetained=" + nameRetained + "; PasswordRetained=" + passwordRetained + "; NameLength=" + nameLength + "; PasswordLength=" + passwordLength);
                bool expected = mode == "Unchanged"
                    ? nameRetained && (outcome == "Invalid" ? passwordLength == 0 && throttle == 1 : retained && throttle == 0) && dialogs == 1 && !authenticated
                    : retained && throttle == 0 && dialogs == 0 && !authenticated;
                Console.WriteLine("CONTRACT Outcome=" + outcome + "; Edited=" + (mode != "Unchanged") + "; ExpectedBehavior=" + expected);
                return expected ? 0 : 1;
            }
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 2; }
        finally { SessionContext.Clear(); }
    }
}
